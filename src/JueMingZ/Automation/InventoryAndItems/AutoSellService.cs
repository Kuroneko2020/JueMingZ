using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.InventoryAndItems
{
    public sealed class AutoSellServiceDiagnostics
    {
        public string LastDecision { get; set; }
        public string LastInventorySignature { get; set; }
        public string LastSellItemIds { get; set; }
        public DateTime? LastDecisionUtc { get; set; }

        public AutoSellServiceDiagnostics()
        {
            LastDecision = string.Empty;
            LastInventorySignature = string.Empty;
            LastSellItemIds = string.Empty;
        }
    }

    public static class AutoSellService
    {
        private const long CheckIntervalTicks = 15;
        private static readonly object SyncRoot = new object();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static string _lastDecision = string.Empty;
        private static string _lastInventorySignature = string.Empty;
        private static string _lastSellItemIds = string.Empty;
        private static DateTime? _lastDecisionUtc;

        public static void Tick(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState)
        {
            Tick(queue, gameState, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            try
            {
                TickCore(queue, gameState, runtimeState, settingsSnapshot);
            }
            catch (Exception error)
            {
                RecordDecision("exception:" + error.GetType().Name, string.Empty, string.Empty);
                RuntimeDiagnostics.RecordError("AutoSellService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "auto-sell-service-error",
                    TimeSpan.FromSeconds(10),
                    "AutoSellService",
                    "Auto sell service failed; exception swallowed.", error);
            }
        }

        internal static List<int> NormalizeAutoSellItemIdsForTesting(IList<int> itemIds)
        {
            return NormalizeAutoSellItemIds(itemIds);
        }

        internal static InputActionRequest BuildAutoSellRequestForTesting(
            AutoSellShopTarget target,
            IReadOnlyList<AutoSellInventoryCandidate> candidates,
            string signature)
        {
            return BuildAutoSellRequest(target, candidates, signature);
        }

        internal static bool TryFindSellableInventoryCandidatesForTesting(
            GameStateSnapshot gameState,
            ICollection<int> itemIds,
            out List<AutoSellInventoryCandidate> candidates,
            out string signature,
            out string message)
        {
            return TryFindSellableInventoryCandidates(gameState, itemIds, out candidates, out signature, out message);
        }

        public static AutoSellServiceDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new AutoSellServiceDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastInventorySignature = _lastInventorySignature,
                    LastSellItemIds = _lastSellItemIds,
                    LastDecisionUtc = _lastDecisionUtc
                };
            }
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var settings = settingsSnapshot.SourceSettings ?? AppSettings.CreateDefault();
            if (!settingsSnapshot.InventoryAutoSellEnabled)
            {
                ClearTracking("disabled");
                return;
            }

            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            if (!ShouldScan(tick))
            {
                return;
            }

            if (queue == null)
            {
                RecordDecision("queue unavailable", string.Empty, string.Empty);
                return;
            }

            if (gameState == null ||
                !gameState.IsInWorld ||
                gameState.Player == null ||
                !gameState.Player.Exists ||
                !gameState.Player.Active ||
                gameState.Player.Dead ||
                gameState.Player.Ghost)
            {
                RecordDecision("player unavailable", string.Empty, string.Empty);
                return;
            }

            var blockedReason = GetExecutionBlockedReason(gameState);
            if (!string.IsNullOrEmpty(blockedReason))
            {
                RecordDecision(blockedReason, string.Empty, string.Empty);
                return;
            }

            if (AutoStackService.HasPendingAutomationWork())
            {
                RecordDecision("waiting for auto stack", string.Empty, string.Empty);
                return;
            }

            if (IsQueueBusy(queue))
            {
                RecordDecision("queue busy", string.Empty, string.Empty);
                return;
            }

            var itemIds = NormalizeAutoSellItemIds(settings.InventoryAutoSellItemIds);
            if (itemIds.Count == 0)
            {
                RecordDecision("auto sell list empty", string.Empty, string.Empty);
                return;
            }

            var itemIdSet = new HashSet<int>(itemIds);
            List<AutoSellInventoryCandidate> candidates;
            string signature;
            string inventoryMessage;
            if (!TryFindSellableInventoryCandidates(gameState, itemIdSet, out candidates, out signature, out inventoryMessage))
            {
                RecordDecision(inventoryMessage, string.Empty, JoinInts(itemIds));
                return;
            }

            if (candidates.Count == 0)
            {
                RecordDecision(string.IsNullOrWhiteSpace(inventoryMessage) ? "no list item in inventory" : inventoryMessage, signature, JoinInts(itemIds));
                return;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                RecordDecision("local player unavailable", signature, JoinInts(itemIds));
                return;
            }

            AutoSellShopTarget target;
            string npcMessage;
            if (!AutoSellCompat.TryFindReachableShopNpc(player, out target, out npcMessage))
            {
                RecordDecision(npcMessage, signature, JoinInts(itemIds));
                return;
            }

            var request = BuildAutoSellRequest(target, candidates, signature);
            queue.Enqueue(request);
            RecordDecision("submitted auto sell request", signature, JoinInts(itemIds));
        }

        private static InputActionRequest BuildAutoSellRequest(
            AutoSellShopTarget target,
            IReadOnlyList<AutoSellInventoryCandidate> candidates,
            string signature)
        {
            target = target ?? new AutoSellShopTarget();
            candidates = candidates ?? new List<AutoSellInventoryCandidate>();
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Shop,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.InventoryAutoSell,
                Description = "Auto sell listed items",
                Timeout = TimeSpan.FromSeconds(3),
                AdmissionKey = FeatureIds.InventoryAutoSell
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryAutoSell;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["NpcIndex"] = target.NpcIndex.ToString(CultureInfo.InvariantCulture);
            request.Metadata["NpcType"] = target.NpcType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ShopIndex"] = target.ShopIndex.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ShopNpcName"] = target.Name ?? string.Empty;
            request.Metadata["AutoSellItemIds"] = JoinCandidateItemIds(candidates);
            request.Metadata["AutoSellInventorySlots"] = JoinCandidateSlots(candidates);
            request.Metadata["InventorySignature"] = signature ?? string.Empty;
            request.Metadata["SellSlotCount"] = candidates.Count.ToString(CultureInfo.InvariantCulture);
            request.Metadata["SellStackTotal"] = SumCandidateStacks(candidates).ToString(CultureInfo.InvariantCulture);
            return request;
        }

        private static bool TryFindSellableInventoryCandidates(
            GameStateSnapshot gameState,
            ICollection<int> itemIds,
            out List<AutoSellInventoryCandidate> candidates,
            out string signature,
            out string message)
        {
            candidates = new List<AutoSellInventoryCandidate>();
            signature = string.Empty;
            message = string.Empty;
            if (itemIds == null || itemIds.Count == 0)
            {
                message = "Auto sell item list is empty.";
                return true;
            }

            var inventory = gameState == null || gameState.Inventory == null
                ? null
                : gameState.Inventory.Items;
            if (inventory == null)
            {
                message = "Inventory snapshot unavailable.";
                return false;
            }

            for (var index = 0; index < inventory.Count; index++)
            {
                var item = inventory[index];
                if (item == null ||
                    item.Type <= 0 ||
                    item.Stack <= 0 ||
                    item.Favorited ||
                    IsCoin(item.Type) ||
                    !itemIds.Contains(item.Type))
                {
                    continue;
                }

                candidates.Add(new AutoSellInventoryCandidate
                {
                    Slot = item.SlotIndex,
                    ItemType = item.Type,
                    ItemName = item.Name ?? string.Empty,
                    Stack = item.Stack
                });
            }

            signature = BuildInventorySignature(candidates);
            if (candidates.Count == 0)
            {
                message = "No auto sell list items found in inventory.";
            }

            return true;
        }

        private static List<int> NormalizeAutoSellItemIds(IList<int> itemIds)
        {
            var source = itemIds;
            if (source == null)
            {
                source = AutoSellCompat.DefaultAutoSellItemIds;
            }

            var result = new List<int>();
            var seen = new HashSet<int>();
            for (var index = 0; index < source.Count; index++)
            {
                var itemId = source[index];
                if (itemId <= 0 || IsCoin(itemId) || !seen.Add(itemId))
                {
                    continue;
                }

                result.Add(itemId);
            }

            return result;
        }

        private static bool ShouldScan(long tick)
        {
            lock (SyncRoot)
            {
                if (tick - _lastScanTick >= CheckIntervalTicks || tick < _lastScanTick)
                {
                    _lastScanTick = tick;
                    return true;
                }

                return false;
            }
        }

        private static string GetExecutionBlockedReason(GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                return "blocked: not in world";
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                return "blocked: mouse item held";
            }

            if (snapshot.Ui == null)
            {
                return string.Empty;
            }

            if (snapshot.Ui.IsInMainMenu)
            {
                return "blocked: main menu";
            }

            if (snapshot.Ui.ChatOpen)
            {
                return "blocked: chat open";
            }

            if (snapshot.Ui.NpcChatOpen)
            {
                return "blocked: NPC chat open";
            }

            if (snapshot.Ui.PlayerInventoryOpen)
            {
                return "blocked: player inventory UI open";
            }

            if (snapshot.Ui.ChestOpen)
            {
                return "blocked: chest UI open";
            }

            return string.Empty;
        }

        private static bool IsQueueBusy(InputActionQueue queue)
        {
            var queueSnapshot = queue == null ? null : queue.GetFastState();
            return queueSnapshot == null ||
                   queueSnapshot.PendingCount > 0 || queueSnapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static void ClearTracking(string decision)
        {
            lock (SyncRoot)
            {
                _lastScanTick = -CheckIntervalTicks;
                _lastDecision = decision ?? string.Empty;
                _lastInventorySignature = string.Empty;
                _lastSellItemIds = string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
            }
        }

        private static void RecordDecision(string decision, string inventorySignature, string sellItemIds)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastInventorySignature = inventorySignature ?? string.Empty;
                _lastSellItemIds = sellItemIds ?? string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
            }
        }

        private static string JoinCandidateSlots(IReadOnlyList<AutoSellInventoryCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return string.Empty;
            }

            var parts = new string[candidates.Count];
            for (var index = 0; index < candidates.Count; index++)
            {
                parts[index] = candidates[index].Slot.ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(",", parts);
        }

        private static string JoinCandidateItemIds(IReadOnlyList<AutoSellInventoryCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return string.Empty;
            }

            var itemIds = new List<int>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var itemId = candidates[index].ItemType;
                if (itemId > 0 && !itemIds.Contains(itemId))
                {
                    itemIds.Add(itemId);
                }
            }

            return JoinInts(itemIds);
        }

        private static int SumCandidateStacks(IReadOnlyList<AutoSellInventoryCandidate> candidates)
        {
            var total = 0;
            if (candidates == null)
            {
                return total;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                total += Math.Max(0, candidates[index].Stack);
            }

            return total;
        }

        private static string BuildInventorySignature(IReadOnlyList<AutoSellInventoryCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return string.Empty;
            }

            var parts = new string[candidates.Count];
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                parts[index] = candidate.Slot.ToString(CultureInfo.InvariantCulture) +
                               ":" +
                               candidate.ItemType.ToString(CultureInfo.InvariantCulture) +
                               "x" +
                               candidate.Stack.ToString(CultureInfo.InvariantCulture);
            }

            return string.Join("|", parts);
        }

        private static string JoinInts(IReadOnlyList<int> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            var parts = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                parts[index] = values[index].ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(",", parts);
        }

        private static bool IsCoin(int itemType)
        {
            return itemType >= 71 && itemType <= 74;
        }
    }
}
