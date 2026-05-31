using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.InventoryAndItems
{
    public sealed class AutoDepositCoinsServiceDiagnostics
    {
        public string LastDecision { get; set; }
        public string LastInventorySignature { get; set; }
        public string LastCoinItemIds { get; set; }
        public DateTime? LastDecisionUtc { get; set; }

        public AutoDepositCoinsServiceDiagnostics()
        {
            LastDecision = string.Empty;
            LastInventorySignature = string.Empty;
            LastCoinItemIds = string.Empty;
        }
    }

    public static class AutoDepositCoinsService
    {
        private const long CheckIntervalTicks = 15;
        private const long SameSignatureRetryTicks = 180;
        private static readonly object SyncRoot = new object();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static long _lastSubmittedTick = -SameSignatureRetryTicks;
        private static string _lastSubmittedSignature = string.Empty;
        private static string _lastDecision = string.Empty;
        private static string _lastInventorySignature = string.Empty;
        private static string _lastCoinItemIds = string.Empty;
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
                RuntimeDiagnostics.RecordError("AutoDepositCoinsService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "auto-deposit-coins-service-error",
                    TimeSpan.FromSeconds(10),
                    "AutoDepositCoinsService",
                    "Auto deposit coins service failed; exception swallowed.", error);
            }
        }

        public static void ClearState(string decision)
        {
            ClearTracking(decision);
        }

        internal static InputActionRequest BuildAutoDepositCoinsRequestForTesting(
            IReadOnlyList<int> coinItemIds,
            IReadOnlyList<int> slots,
            string signature,
            int slotCount,
            int stackTotal)
        {
            return BuildAutoDepositCoinsRequest(coinItemIds, slots, signature, slotCount, stackTotal);
        }

        internal static bool TryFindCoinInventorySlotsForTesting(
            GameStateSnapshot gameState,
            out List<int> coinItemIds,
            out List<int> slots,
            out string signature,
            out int slotCount,
            out int stackTotal,
            out string message)
        {
            return TryFindCoinInventorySlots(gameState, out coinItemIds, out slots, out signature, out slotCount, out stackTotal, out message);
        }

        public static AutoDepositCoinsServiceDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new AutoDepositCoinsServiceDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastInventorySignature = _lastInventorySignature,
                    LastCoinItemIds = _lastCoinItemIds,
                    LastDecisionUtc = _lastDecisionUtc
                };
            }
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            if (!settingsSnapshot.InventoryAutoDepositCoinsEnabled)
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

            List<int> coinItemIds;
            List<int> slots;
            string signature;
            int slotCount;
            int stackTotal;
            string inventoryMessage;
            if (!TryFindCoinInventorySlots(gameState, out coinItemIds, out slots, out signature, out slotCount, out stackTotal, out inventoryMessage))
            {
                RecordDecision(inventoryMessage, string.Empty, string.Empty);
                return;
            }

            if (slotCount <= 0 || stackTotal <= 0)
            {
                RecordDecision(string.IsNullOrWhiteSpace(inventoryMessage) ? "no coins in inventory" : inventoryMessage, signature, JoinInts(coinItemIds));
                return;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                RecordDecision("local player unavailable", signature, JoinInts(coinItemIds));
                return;
            }

            int nearbyBankCount;
            if (!AutoDepositCoinsCompat.TryGetNearbyBankCount(player, out nearbyBankCount))
            {
                RecordDecision("nearby bank scan unavailable", signature, JoinInts(coinItemIds));
                return;
            }

            if (nearbyBankCount <= 0)
            {
                RecordDecision("no nearby bank containers in coin deposit range", signature, JoinInts(coinItemIds));
                return;
            }

            if (ShouldDelaySameSignatureRetry(signature, tick))
            {
                RecordDecision("waiting before retrying same coin stack signature", signature, JoinInts(coinItemIds));
                return;
            }

            var request = BuildAutoDepositCoinsRequest(coinItemIds, slots, signature, slotCount, stackTotal);
            queue.Enqueue(request);
            MarkSubmittedSignature(signature, tick);
            RecordDecision("submitted auto deposit coins request", signature, JoinInts(coinItemIds));
        }

        private static InputActionRequest BuildAutoDepositCoinsRequest(
            IReadOnlyList<int> coinItemIds,
            IReadOnlyList<int> slots,
            string signature,
            int slotCount,
            int stackTotal)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Chest,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.InventoryAutoDepositCoins,
                Description = "Auto deposit coins",
                Timeout = TimeSpan.FromSeconds(3),
                AdmissionKey = FeatureIds.InventoryAutoDepositCoins
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryAutoDepositCoins;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["AutoDepositCoinItemIds"] = JoinInts(coinItemIds);
            request.Metadata["AutoDepositCoinInventorySlots"] = JoinInts(slots);
            request.Metadata["InventorySignature"] = signature ?? string.Empty;
            request.Metadata["MovableSlotCount"] = slotCount.ToString(CultureInfo.InvariantCulture);
            request.Metadata["MovableStackTotal"] = stackTotal.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AllowPlayerInventoryOpen"] = "true";
            request.Metadata["AutoDepositCoinsTransferPath"] = "ChestUI.MoveCoinsNearbyBanks";
            request.Metadata["AutoDepositCoinsEmptyBankFallback"] = "PiggyBankFirstCoin";
            return request;
        }

        private static bool TryFindCoinInventorySlots(
            GameStateSnapshot gameState,
            out List<int> coinItemIds,
            out List<int> slots,
            out string signature,
            out int slotCount,
            out int stackTotal,
            out string message)
        {
            coinItemIds = new List<int>();
            slots = new List<int>();
            signature = string.Empty;
            slotCount = 0;
            stackTotal = 0;
            message = string.Empty;

            var inventory = gameState == null || gameState.Inventory == null
                ? null
                : gameState.Inventory.Items;
            if (inventory == null || inventory.Count <= 0)
            {
                message = "inventory snapshot unavailable";
                return false;
            }

            var builder = new StringBuilder();
            var max = Math.Min(58, inventory.Count);
            for (var index = 0; index < max; index++)
            {
                var item = inventory[index];
                if (!IsCoinInventoryItem(item))
                {
                    continue;
                }

                var slot = item.SlotIndex >= 0 && item.SlotIndex < 58 ? item.SlotIndex : index;
                slots.Add(slot);
                stackTotal += item.Stack;
                if (!coinItemIds.Contains(item.Type))
                {
                    coinItemIds.Add(item.Type);
                }

                if (builder.Length > 0)
                {
                    builder.Append('|');
                }

                builder.Append(slot.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(item.Type.ToString(CultureInfo.InvariantCulture));
                builder.Append('x');
                builder.Append(Math.Max(0, item.Stack).ToString(CultureInfo.InvariantCulture));
            }

            slotCount = slots.Count;
            signature = builder.ToString();
            if (slotCount <= 0)
            {
                message = "no coins in inventory";
            }

            return true;
        }

        private static bool IsCoinInventoryItem(InventoryItemSnapshot item)
        {
            return item != null &&
                   item.Type >= 71 &&
                   item.Type <= 74 &&
                   item.Stack > 0 &&
                   !item.Favorited;
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

        private static bool ShouldDelaySameSignatureRetry(string signature, long tick)
        {
            lock (SyncRoot)
            {
                return !string.IsNullOrWhiteSpace(signature) &&
                       string.Equals(_lastSubmittedSignature, signature, StringComparison.Ordinal) &&
                       tick >= _lastSubmittedTick &&
                       tick - _lastSubmittedTick < SameSignatureRetryTicks;
            }
        }

        private static void MarkSubmittedSignature(string signature, long tick)
        {
            lock (SyncRoot)
            {
                _lastSubmittedSignature = signature ?? string.Empty;
                _lastSubmittedTick = tick;
            }
        }

        private static void ClearTracking(string decision)
        {
            lock (SyncRoot)
            {
                _lastScanTick = -CheckIntervalTicks;
                _lastSubmittedTick = -SameSignatureRetryTicks;
                _lastSubmittedSignature = string.Empty;
                _lastDecision = decision ?? string.Empty;
                _lastInventorySignature = string.Empty;
                _lastCoinItemIds = string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
            }
        }

        private static void RecordDecision(string decision, string inventorySignature, string coinItemIds)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastInventorySignature = inventorySignature ?? string.Empty;
                _lastCoinItemIds = coinItemIds ?? string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
            }
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
    }
}
