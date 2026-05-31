using System;
using System.Collections.Generic;
using System.Globalization;
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
    public sealed class AutoStackServiceDiagnostics
    {
        public string LastDecision { get; set; }
        public string LastInventorySignature { get; set; }
        public string LastPendingItemIds { get; set; }
        public DateTime? LastDecisionUtc { get; set; }

        public AutoStackServiceDiagnostics()
        {
            LastDecision = string.Empty;
            LastInventorySignature = string.Empty;
            LastPendingItemIds = string.Empty;
        }
    }

    public static class AutoStackService
    {
        private const long CheckIntervalTicks = 5;
        private const long PendingSettleTicks = 120;
        private const long InventoryOpenExecutionSettleTicks = 3;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, int> LastStackTotals = new Dictionary<int, int>();
        private static readonly List<int> PendingItemIds = new List<int>();
        private static bool _baselineInitialized;
        private static long _lastScanTick = -CheckIntervalTicks;
        private static long _pendingSinceTick;
        private static long _lastPendingChangeTick = -1;
        private static string _lastDecision = string.Empty;
        private static string _lastInventorySignature = string.Empty;
        private static string _lastPendingItemIds = string.Empty;
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
                RuntimeDiagnostics.RecordError("AutoStackService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "auto-stack-service-error",
                    TimeSpan.FromSeconds(10),
                    "AutoStackService",
                    "Auto stack service failed; exception swallowed.", error);
            }
        }

        internal static List<int> FindIncreasedItemTypesForTesting(IDictionary<int, int> previous, IDictionary<int, int> current)
        {
            return FindIncreasedItemTypes(previous, current);
        }

        internal static List<int> FindPickupIncreasedItemTypesForTesting(IDictionary<int, int> previous, IDictionary<int, int> current, ISet<int> eligibleItemTypes)
        {
            return FindIncreasedItemTypes(previous, current, eligibleItemTypes);
        }

        internal static InputActionRequest BuildAutoStackRequestForTesting(IReadOnlyList<int> itemIds, IReadOnlyList<int> slots, string signature, int slotCount, int stackTotal)
        {
            return BuildAutoStackRequest(itemIds, slots, signature, slotCount, stackTotal);
        }

        internal static bool TryBuildInventoryItemSignatureForTesting(
            GameStateSnapshot gameState,
            IReadOnlyList<int> itemIds,
            out string signature,
            out List<int> slots,
            out int slotCount,
            out int stackTotal)
        {
            return TryBuildInventoryItemSignature(gameState, itemIds, out signature, out slots, out slotCount, out stackTotal);
        }

        internal static bool IsExecutionBlockedForTesting(GameStateSnapshot snapshot, long tick)
        {
            string reason;
            return TryGetExecutionBlockedReason(snapshot, tick, out reason);
        }

        internal static bool IsInventoryOpenSettlePendingForTesting(long currentTick, long lastPendingChangeTick)
        {
            return IsWithinInventoryOpenSettleWindow(currentTick, lastPendingChangeTick);
        }

        internal static bool HasPendingAutomationWork()
        {
            return HasPendingItemIds();
        }

        public static AutoStackServiceDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new AutoStackServiceDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastInventorySignature = _lastInventorySignature,
                    LastPendingItemIds = _lastPendingItemIds,
                    LastDecisionUtc = _lastDecisionUtc
                };
            }
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            if (!settingsSnapshot.InventoryAutoStackEnabled)
            {
                ClearTracking("disabled");
                return;
            }

            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            var quickBagCleanupYieldActive = QuickBagOpenService.IsCleanupYieldActiveForAutomation(tick);
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
                ClearTracking("player unavailable");
                return;
            }

            if (ShouldScan(tick, quickBagCleanupYieldActive))
            {
                Dictionary<int, int> current;
                HashSet<int> eligibleItemTypes;
                string readMessage;
                if (!TryReadInventoryStackTotals(gameState, out current, out eligibleItemTypes, out readMessage))
                {
                    RecordDecision(readMessage, string.Empty, JoinInts(GetPendingItemIds()));
                    return;
                }

                if (IsUnsafeUiOpenForAutoStack(gameState))
                {
                    UpdateBaselineAndDetectIncreases(current, eligibleItemTypes);
                    RecordDecision(
                        "unsafe UI open",
                        string.Empty,
                        JoinInts(GetPendingItemIds()));
                }
                else
                {
                    var added = UpdateBaselineAndDetectIncreases(current, eligibleItemTypes);
                    if (added.Count > 0)
                    {
                        AddPendingItemIds(added, tick);
                    }

                    if (added.Count > 0 && IsPlayerInventoryOpen(gameState))
                    {
                        RecordDecision("detected picked item stack increase while inventory open", string.Empty, JoinInts(GetPendingItemIds()));
                    }
                    else if (added.Count > 0)
                    {
                        RecordDecision("detected picked item stack increase", string.Empty, JoinInts(GetPendingItemIds()));
                    }
                }
            }

            if (!HasPendingItemIds())
            {
                return;
            }

            string blockReason;
            if (TryGetExecutionBlockedReason(gameState, tick, out blockReason))
            {
                RecordDecision(blockReason, string.Empty, JoinInts(GetPendingItemIds()));
                return;
            }

            if (IsQueueBusy(queue))
            {
                RecordDecision("queue busy", string.Empty, JoinInts(GetPendingItemIds()));
                return;
            }

            var pendingIds = GetPendingItemIds();
            string signature;
            int slotCount;
            int stackTotal;
            List<int> slots;
            if (!TryBuildInventoryItemSignature(gameState, pendingIds, out signature, out slots, out slotCount, out stackTotal) ||
                slotCount <= 0 ||
                stackTotal <= 0)
            {
                if (ShouldKeepPending(tick))
                {
                    RecordDecision("waiting for picked item to settle into inventory", string.Empty, JoinInts(pendingIds));
                    return;
                }

                ClearPendingItemIds();
                RecordDecision("picked item no longer present", string.Empty, JoinInts(pendingIds));
                return;
            }

            var request = BuildAutoStackRequest(pendingIds, slots, signature, slotCount, stackTotal);
            queue.Enqueue(request);
            ClearPendingItemIds();
            RecordDecision("submitted auto stack request", signature, JoinInts(pendingIds));
        }

        private static InputActionRequest BuildAutoStackRequest(IReadOnlyList<int> itemIds, IReadOnlyList<int> slots, string signature, int slotCount, int stackTotal)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Chest,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.InventoryAutoStack,
                Description = "Auto stack picked up items",
                Timeout = TimeSpan.FromSeconds(3),
                AdmissionKey = FeatureIds.InventoryAutoStack
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryAutoStack;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["AutoStackItemIds"] = JoinInts(itemIds);
            request.Metadata["AutoStackInventorySlots"] = JoinInts(slots);
            request.Metadata["InventorySignature"] = signature ?? string.Empty;
            request.Metadata["MovableSlotCount"] = slotCount.ToString(CultureInfo.InvariantCulture);
            request.Metadata["MovableStackTotal"] = stackTotal.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AllowPlayerInventoryOpen"] = "true";
            return request;
        }

        private static bool TryReadInventoryStackTotals(GameStateSnapshot gameState, out Dictionary<int, int> totals, out HashSet<int> eligibleItemTypes, out string message)
        {
            totals = new Dictionary<int, int>();
            eligibleItemTypes = new HashSet<int>();
            message = string.Empty;
            var inventory = gameState == null ? null : gameState.Inventory;
            if (inventory == null || inventory.Items == null || inventory.Items.Count <= 0)
            {
                message = "inventory snapshot unavailable";
                return false;
            }

            var max = Math.Min(58, inventory.Items.Count);
            for (var slot = 0; slot < max; slot++)
            {
                var item = inventory.Items[slot];
                if (item == null || item.Type <= 0 || item.Stack <= 0)
                {
                    continue;
                }

                int current;
                totals.TryGetValue(item.Type, out current);
                totals[item.Type] = current + item.Stack;
                if (IsAutoStackPickupEligibleItem(item))
                {
                    eligibleItemTypes.Add(item.Type);
                }
            }

            return true;
        }

        private static bool TryBuildInventoryItemSignature(
            GameStateSnapshot gameState,
            IReadOnlyList<int> itemIds,
            out string signature,
            out List<int> slots,
            out int slotCount,
            out int stackTotal)
        {
            signature = string.Empty;
            slots = new List<int>();
            slotCount = 0;
            stackTotal = 0;

            var idSet = BuildPositiveItemIdSet(itemIds);
            var inventory = gameState == null ? null : gameState.Inventory;
            if (idSet.Count == 0 || inventory == null || inventory.Items == null)
            {
                return false;
            }

            var builder = new System.Text.StringBuilder();
            for (var index = 0; index < inventory.Items.Count; index++)
            {
                var item = inventory.Items[index];
                if (!IsAutoStackPickupEligibleItem(item) || !idSet.Contains(item.Type))
                {
                    continue;
                }

                slots.Add(item.SlotIndex);
                stackTotal += item.Stack;
                if (builder.Length > 0)
                {
                    builder.Append('|');
                }

                builder.Append(item.SlotIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(item.Type.ToString(CultureInfo.InvariantCulture));
                builder.Append('x');
                builder.Append(Math.Max(0, item.Stack).ToString(CultureInfo.InvariantCulture));
            }

            slotCount = slots.Count;
            signature = builder.ToString();
            return true;
        }

        private static bool IsMovableInventoryItem(InventoryItemSnapshot item)
        {
            return item != null &&
                   item.Type > 0 &&
                   item.Stack > 0 &&
                   !item.Favorited;
        }

        private static bool IsAutoStackPickupEligibleItem(InventoryItemSnapshot item)
        {
            return IsMovableInventoryItem(item) &&
                   !IsEquipmentLikeItem(item) &&
                   IsStackableItem(item);
        }

        private static bool IsStackableItem(InventoryItemSnapshot item)
        {
            return item != null &&
                   item.MaxStack > 1;
        }

        private static bool IsEquipmentLikeItem(InventoryItemSnapshot item)
        {
            return item != null &&
                   (item.Accessory ||
                    item.WingSlot > -1 ||
                    item.Defense > 0);
        }

        private static List<int> UpdateBaselineAndDetectIncreases(Dictionary<int, int> current, ISet<int> eligibleItemTypes)
        {
            lock (SyncRoot)
            {
                if (!_baselineInitialized)
                {
                    ReplaceBaselineLocked(current);
                    _baselineInitialized = true;
                    _lastScanTick = Math.Max(_lastScanTick, 0);
                    return new List<int>();
                }

                var added = FindIncreasedItemTypes(LastStackTotals, current, eligibleItemTypes);
                ReplaceBaselineLocked(current);
                return added;
            }
        }

        private static List<int> FindIncreasedItemTypes(IDictionary<int, int> previous, IDictionary<int, int> current)
        {
            return FindIncreasedItemTypes(previous, current, null);
        }

        private static List<int> FindIncreasedItemTypes(IDictionary<int, int> previous, IDictionary<int, int> current, ISet<int> eligibleItemTypes)
        {
            var result = new List<int>();
            if (current == null || current.Count <= 0)
            {
                return result;
            }

            var keys = new List<int>(current.Keys);
            keys.Sort();
            for (var index = 0; index < keys.Count; index++)
            {
                var itemType = keys[index];
                if (itemType <= 0)
                {
                    continue;
                }

                if (eligibleItemTypes != null && !eligibleItemTypes.Contains(itemType))
                {
                    continue;
                }

                var currentCount = current[itemType];
                int previousCount;
                if (previous == null || !previous.TryGetValue(itemType, out previousCount))
                {
                    previousCount = 0;
                }

                if (currentCount > previousCount)
                {
                    result.Add(itemType);
                }
            }

            return result;
        }

        private static void ReplaceBaselineLocked(Dictionary<int, int> source)
        {
            LastStackTotals.Clear();
            if (source == null)
            {
                return;
            }

            foreach (var pair in source)
            {
                if (pair.Key > 0 && pair.Value > 0)
                {
                    LastStackTotals[pair.Key] = pair.Value;
                }
            }
        }

        private static void AddPendingItemIds(IReadOnlyList<int> itemIds, long tick)
        {
            if (itemIds == null || itemIds.Count <= 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                var hasValidItemId = false;
                for (var index = 0; index < itemIds.Count; index++)
                {
                    var itemId = itemIds[index];
                    if (itemId <= 0)
                    {
                        continue;
                    }

                    hasValidItemId = true;
                    if (!PendingItemIds.Contains(itemId))
                    {
                        PendingItemIds.Add(itemId);
                    }
                }

                if (PendingItemIds.Count > 0 && _pendingSinceTick <= 0)
                {
                    _pendingSinceTick = tick;
                }

                if (hasValidItemId && PendingItemIds.Count > 0)
                {
                    _lastPendingChangeTick = tick;
                }
            }
        }

        private static bool HasPendingItemIds()
        {
            lock (SyncRoot)
            {
                return PendingItemIds.Count > 0;
            }
        }

        private static List<int> GetPendingItemIds()
        {
            lock (SyncRoot)
            {
                return new List<int>(PendingItemIds);
            }
        }

        private static void ClearPendingItemIds()
        {
            lock (SyncRoot)
            {
                PendingItemIds.Clear();
                _pendingSinceTick = 0;
                _lastPendingChangeTick = -1;
            }
        }

        private static bool ShouldKeepPending(long tick)
        {
            lock (SyncRoot)
            {
                if (PendingItemIds.Count <= 0)
                {
                    return false;
                }

                if (_pendingSinceTick <= 0)
                {
                    _pendingSinceTick = tick;
                    return true;
                }

                return tick >= _pendingSinceTick &&
                       tick - _pendingSinceTick <= PendingSettleTicks;
            }
        }

        private static bool ShouldScan(long tick, bool force)
        {
            lock (SyncRoot)
            {
                if (force || !_baselineInitialized || tick - _lastScanTick >= CheckIntervalTicks || tick < _lastScanTick)
                {
                    _lastScanTick = tick;
                    return true;
                }

                return false;
            }
        }

        private static void ClearTracking(string decision)
        {
            lock (SyncRoot)
            {
                LastStackTotals.Clear();
                PendingItemIds.Clear();
                _baselineInitialized = false;
                _pendingSinceTick = 0;
                _lastPendingChangeTick = -1;
                _lastScanTick = -CheckIntervalTicks;
                _lastDecision = decision ?? string.Empty;
                _lastInventorySignature = string.Empty;
                _lastPendingItemIds = string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
            }
        }

        private static bool IsPlayerInventoryOpen(GameStateSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.Ui != null &&
                   snapshot.Ui.PlayerInventoryOpen;
        }

        private static bool IsUnsafeUiOpenForAutoStack(GameStateSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.Ui != null &&
                   (snapshot.Ui.IsInMainMenu ||
                    snapshot.Ui.ChatOpen ||
                    snapshot.Ui.NpcChatOpen ||
                    snapshot.Ui.ChestOpen);
        }

        private static bool TryGetExecutionBlockedReason(GameStateSnapshot snapshot, long tick, out string reason)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                reason = "blocked: not in world";
                return true;
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                reason = "blocked: mouse item held";
                return true;
            }

            if (snapshot.Ui != null)
            {
                if (snapshot.Ui.IsInMainMenu)
                {
                    reason = "blocked: main menu";
                    return true;
                }

                if (snapshot.Ui.ChatOpen)
                {
                    reason = "blocked: chat open";
                    return true;
                }

                if (snapshot.Ui.NpcChatOpen)
                {
                    reason = "blocked: NPC chat open";
                    return true;
                }

                if (snapshot.Ui.ChestOpen)
                {
                    reason = "blocked: chest open";
                    return true;
                }

                if (snapshot.Ui.PlayerInventoryOpen && IsPendingInventoryOpenSettleWindow(tick))
                {
                    reason = "waiting for inventory-open auto stack settle";
                    return true;
                }
            }

            reason = string.Empty;
            return false;
        }

        private static bool IsPendingInventoryOpenSettleWindow(long tick)
        {
            lock (SyncRoot)
            {
                return PendingItemIds.Count > 0 &&
                       IsWithinInventoryOpenSettleWindow(tick, _lastPendingChangeTick);
            }
        }

        private static bool IsWithinInventoryOpenSettleWindow(long tick, long lastPendingChangeTick)
        {
            return lastPendingChangeTick >= 0 &&
                   tick >= lastPendingChangeTick &&
                   tick - lastPendingChangeTick < InventoryOpenExecutionSettleTicks;
        }

        private static bool IsQueueBusy(InputActionQueue queue)
        {
            var queueSnapshot = queue == null ? null : queue.GetFastState();
            return queueSnapshot == null ||
                   queueSnapshot.PendingCount > 0 || queueSnapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static void RecordDecision(string decision, string inventorySignature, string pendingItemIds)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastInventorySignature = inventorySignature ?? string.Empty;
                _lastPendingItemIds = pendingItemIds ?? string.Empty;
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

        private static HashSet<int> BuildPositiveItemIdSet(IReadOnlyList<int> itemIds)
        {
            var result = new HashSet<int>();
            if (itemIds == null)
            {
                return result;
            }

            for (var index = 0; index < itemIds.Count; index++)
            {
                var itemId = itemIds[index];
                if (itemId > 0)
                {
                    result.Add(itemId);
                }
            }

            return result;
        }
    }
}
