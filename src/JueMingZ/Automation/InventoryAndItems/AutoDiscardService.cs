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
    public sealed class AutoDiscardServiceDiagnostics
    {
        public string LastDecision { get; set; }
        public string LastInventorySignature { get; set; }
        public string LastDiscardItemIds { get; set; }
        public DateTime? LastDecisionUtc { get; set; }

        public AutoDiscardServiceDiagnostics()
        {
            LastDecision = string.Empty;
            LastInventorySignature = string.Empty;
            LastDiscardItemIds = string.Empty;
        }
    }

    public static class AutoDiscardService
    {
        private const long CheckIntervalTicks = 15;
        private static readonly object SyncRoot = new object();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static string _lastDecision = string.Empty;
        private static string _lastInventorySignature = string.Empty;
        private static string _lastDiscardItemIds = string.Empty;
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
                RuntimeDiagnostics.RecordError("AutoDiscardService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "auto-discard-service-error",
                    TimeSpan.FromSeconds(10),
                    "AutoDiscardService",
                    "Auto discard service failed; exception swallowed.", error);
            }
        }

        internal static List<int> NormalizeAutoDiscardItemIdsForTesting(IList<int> itemIds)
        {
            return NormalizeAutoDiscardItemIds(itemIds);
        }

        internal static InputActionRequest BuildAutoDiscardRequestForTesting(
            IReadOnlyList<AutoDiscardInventoryCandidate> candidates,
            string signature)
        {
            return BuildAutoDiscardRequest(candidates, signature);
        }

        internal static bool TryFindDiscardableInventoryCandidatesForTesting(
            GameStateSnapshot gameState,
            ICollection<int> itemIds,
            out List<AutoDiscardInventoryCandidate> candidates,
            out string signature,
            out string message)
        {
            return TryFindDiscardableInventoryCandidates(gameState, itemIds, out candidates, out signature, out message);
        }

        public static AutoDiscardServiceDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new AutoDiscardServiceDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastInventorySignature = _lastInventorySignature,
                    LastDiscardItemIds = _lastDiscardItemIds,
                    LastDecisionUtc = _lastDecisionUtc
                };
            }
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var settings = settingsSnapshot.SourceSettings ?? AppSettings.CreateDefault();
            if (!settingsSnapshot.InventoryAutoDiscardEnabled)
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

            if (IsExecutionBlocked(gameState))
            {
                RecordDecision("blocked by UI or unsafe mouse item", string.Empty, string.Empty);
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

            var itemIds = NormalizeAutoDiscardItemIds(settings.InventoryAutoDiscardItemIds);
            if (itemIds.Count == 0)
            {
                RecordDecision("auto discard list empty", string.Empty, string.Empty);
                return;
            }

            var itemIdSet = new HashSet<int>(itemIds);
            List<AutoDiscardInventoryCandidate> candidates;
            string signature;
            string inventoryMessage;
            if (!TryFindDiscardableInventoryCandidates(gameState, itemIdSet, out candidates, out signature, out inventoryMessage))
            {
                RecordDecision(inventoryMessage, string.Empty, JoinInts(itemIds));
                return;
            }

            if (candidates.Count == 0)
            {
                RecordDecision(string.IsNullOrWhiteSpace(inventoryMessage) ? "no list item in inventory" : inventoryMessage, signature, JoinInts(itemIds));
                return;
            }

            var request = BuildAutoDiscardRequest(candidates, signature);
            queue.Enqueue(request);
            RecordDecision("submitted auto discard request", signature, JoinInts(itemIds));
        }

        private static InputActionRequest BuildAutoDiscardRequest(
            IReadOnlyList<AutoDiscardInventoryCandidate> candidates,
            string signature)
        {
            candidates = candidates ?? new List<AutoDiscardInventoryCandidate>();
            var request = new InputActionRequest
            {
                Kind = InputActionKind.TrashSlot,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.InventoryAutoDiscard,
                Description = "Auto discard listed items",
                Timeout = TimeSpan.FromSeconds(3),
                AdmissionKey = FeatureIds.InventoryAutoDiscard
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryAutoDiscard;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["AutoDiscardItemIds"] = JoinCandidateItemIds(candidates);
            request.Metadata["AutoDiscardInventorySlots"] = JoinCandidateSlots(candidates);
            request.Metadata["InventorySignature"] = signature ?? string.Empty;
            request.Metadata["DiscardSlotCount"] = candidates.Count.ToString(CultureInfo.InvariantCulture);
            request.Metadata["DiscardStackTotal"] = SumCandidateStacks(candidates).ToString(CultureInfo.InvariantCulture);
            return request;
        }

        private static bool TryFindDiscardableInventoryCandidates(
            GameStateSnapshot gameState,
            ICollection<int> itemIds,
            out List<AutoDiscardInventoryCandidate> candidates,
            out string signature,
            out string message)
        {
            candidates = new List<AutoDiscardInventoryCandidate>();
            signature = string.Empty;
            message = string.Empty;
            if (itemIds == null || itemIds.Count == 0)
            {
                message = "Auto discard item list is empty.";
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

                candidates.Add(new AutoDiscardInventoryCandidate
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
                message = "No auto discard list items found in inventory.";
            }

            return true;
        }

        private static List<int> NormalizeAutoDiscardItemIds(IList<int> itemIds)
        {
            var result = new List<int>();
            if (itemIds == null)
            {
                return result;
            }

            var seen = new HashSet<int>();
            for (var index = 0; index < itemIds.Count; index++)
            {
                var itemId = itemIds[index];
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

        private static bool IsExecutionBlocked(GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                return true;
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                return true;
            }

            if (snapshot.Ui == null)
            {
                return false;
            }

            return snapshot.Ui.IsInMainMenu ||
                   snapshot.Ui.ChatOpen ||
                   snapshot.Ui.NpcChatOpen ||
                   snapshot.Ui.PlayerInventoryOpen ||
                   snapshot.Ui.ChestOpen;
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
                _lastDiscardItemIds = string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
            }
        }

        private static void RecordDecision(string decision, string inventorySignature, string discardItemIds)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastInventorySignature = inventorySignature ?? string.Empty;
                _lastDiscardItemIds = discardItemIds ?? string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
            }
        }

        private static string JoinCandidateSlots(IReadOnlyList<AutoDiscardInventoryCandidate> candidates)
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

        private static string JoinCandidateItemIds(IReadOnlyList<AutoDiscardInventoryCandidate> candidates)
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

        private static int SumCandidateStacks(IReadOnlyList<AutoDiscardInventoryCandidate> candidates)
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

        private static string BuildInventorySignature(IReadOnlyList<AutoDiscardInventoryCandidate> candidates)
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
