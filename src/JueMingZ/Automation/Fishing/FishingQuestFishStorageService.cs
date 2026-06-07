using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Fishing
{
    // Quest fish storage waits for caught-item evidence and queues inventory work; it never edits item stacks directly.
    internal static class FishingQuestFishStorageService
    {
        private const long AllModeInventorySettleTicks = 120;
        private static readonly object SyncRoot = new object();
        private static long _lastCheckTick;
        private static Guid _lastRequestId = Guid.Empty;
        private static Guid _lastAllRequestId = Guid.Empty;
        private static int _cooldownTicks;
        private static int _lastItemId;
        private static int _lastSlotCount;
        private static long _allStorePendingSinceTick;
        private static string _pendingAllInventorySignature = string.Empty;
        private static string _lastMode = string.Empty;
        private static string _lastInventorySignature = string.Empty;
        private static string _lastPendingItemIds = string.Empty;
        private static string _lastDiagnosticMessage = string.Empty;
        private static bool _allStorePending;
        private static readonly Dictionary<Guid, int> PendingPullCatchItemIds = new Dictionary<Guid, int>();
        private static readonly List<int> PendingCaughtItemIds = new List<int>();

        public static int CooldownTicks
        {
            get { lock (SyncRoot) { return _cooldownTicks; } }
        }

        public static int LastItemId
        {
            get { lock (SyncRoot) { return _lastItemId; } }
        }

        public static int LastSlotCount
        {
            get { lock (SyncRoot) { return _lastSlotCount; } }
        }

        public static string LastMode
        {
            get { lock (SyncRoot) { return _lastMode; } }
        }

        public static string LastInventorySignature
        {
            get { lock (SyncRoot) { return _lastInventorySignature; } }
        }

        public static string LastPendingItemIds
        {
            get { lock (SyncRoot) { return _lastPendingItemIds; } }
        }

        public static string LastDiagnosticMessage
        {
            get { lock (SyncRoot) { return _lastDiagnosticMessage; } }
        }

        internal static bool HasResidualState
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastRequestId != Guid.Empty ||
                           _lastAllRequestId != Guid.Empty ||
                           _allStorePending ||
                           PendingPullCatchItemIds.Count > 0 ||
                           PendingCaughtItemIds.Count > 0;
                }
            }
        }

        public static void RegisterExpectedCaughtItem(Guid pullRequestId, int itemId)
        {
            if (pullRequestId == Guid.Empty || itemId <= 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                PendingPullCatchItemIds[pullRequestId] = itemId;
            }
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, FishingRuntimeState fishingState, string mode, long tick)
        {
            var normalizedMode = FishingAutoStoreModes.Normalize(mode, false);
            lock (SyncRoot)
            {
                _cooldownTicks = Math.Max(0, 10 - (int)(tick - _lastCheckTick));
                _lastMode = normalizedMode;
            }

            if (!FishingAutoStoreModes.IsEnabled(normalizedMode) || queue == null || fishingState == null || !fishingState.SessionActive)
            {
                RecordStoreDiagnostics(normalizedMode, string.Empty, string.Empty, "disabled or no active fishing session");
                ResetAllTracking();
                return;
            }

            if (snapshot == null || !snapshot.IsInWorld || snapshot.Player == null ||
                !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                RecordStoreDiagnostics(normalizedMode, string.Empty, string.Empty, "player unavailable for fishing storage");
                return;
            }

            if (IsBlockedForFishingStorage(snapshot))
            {
                RecordStoreDiagnostics(normalizedMode, string.Empty, string.Empty, "blocked by menu/chat/NPC chat or unsafe mouse item");
                return;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                RecordStoreDiagnostics(normalizedMode, string.Empty, string.Empty, "local player reflection unavailable");
                return;
            }

            if (string.Equals(normalizedMode, FishingAutoStoreModes.All, StringComparison.OrdinalIgnoreCase))
            {
                TickAllMode(queue, player, tick);
                return;
            }

            ResetAllTracking();
            TickQuestFishMode(queue, player, tick);
        }

        private static void TickQuestFishMode(InputActionQueue queue, object player, long tick)
        {
            if (IsQueueBusy(queue) || !TryMarkCheckTick(tick))
            {
                RecordStoreDiagnostics(FishingAutoStoreModes.QuestFish, string.Empty, string.Empty, "queue busy or check throttled");
                return;
            }

            bool finished;
            if (!QuestFishStorageCompat.TryIsAnglerQuestFinished(out finished) || finished)
            {
                RecordStoreDiagnostics(FishingAutoStoreModes.QuestFish, string.Empty, string.Empty, finished ? "angler quest already finished" : "angler quest finished flag unavailable");
                return;
            }

            int questFishId;
            string message;
            if (!QuestFishStorageCompat.TryGetCurrentAnglerQuestFishId(out questFishId, out message))
            {
                RecordStoreDiagnostics(FishingAutoStoreModes.QuestFish, string.Empty, string.Empty, message);
                return;
            }

            bool isQuestFish;
            if (!QuestFishStorageCompat.TryIsQuestFish(questFishId, out isQuestFish) || !isQuestFish)
            {
                RecordStoreDiagnostics(FishingAutoStoreModes.QuestFish, string.Empty, string.Empty, "current angler quest item is not marked as quest fish");
                return;
            }

            List<int> slots;
            if (!QuestFishStorageCompat.TryFindInventoryQuestFishSlots(player, questFishId, out slots) || slots.Count == 0)
            {
                RecordLast(questFishId, 0);
                RecordStoreDiagnostics(FishingAutoStoreModes.QuestFish, string.Empty, string.Empty, "no movable quest fish slots");
                return;
            }

            bool nearbyContains;
            if (!QuestFishStorageCompat.TryNearbyContainersContainQuestFish(player, questFishId, out nearbyContains) || !nearbyContains)
            {
                RecordLast(questFishId, slots.Count);
                RecordStoreDiagnostics(FishingAutoStoreModes.QuestFish, string.Empty, string.Empty, nearbyContains ? "nearby containers do not contain matching quest fish" : "nearby container scan unavailable");
                return;
            }

            var request = new InputActionRequest
            {
                Kind = InputActionKind.Chest,
                Priority = InputActionPriority.Low,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.FishingAutoStoreQuestFish,
                Description = "Fishing auto store quest fish",
                QueueTimeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.FishingAutoStoreQuestFish + "|quest",
                Timeout = TimeSpan.FromSeconds(3)
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.FishingAutoStoreQuestFish;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["QuestFishItemId"] = questFishId.ToString(CultureInfo.InvariantCulture);
            request.Metadata["QuestFishInventorySlots"] = string.Join(",", slots);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordStoreDiagnostics(FishingAutoStoreModes.QuestFish, string.Empty, questFishId.ToString(CultureInfo.InvariantCulture), "quest fish storage admission denied: " + (admission == null ? "unknown" : admission.Reason));
                return;
            }

            lock (SyncRoot)
            {
                _lastRequestId = request.RequestId;
            }

            RecordLast(questFishId, slots.Count);
            RecordStoreDiagnostics(FishingAutoStoreModes.QuestFish, string.Empty, questFishId.ToString(CultureInfo.InvariantCulture), "submitted quest fish storage request");
        }

        private static void TickAllMode(InputActionQueue queue, object player, long tick)
        {
            if (!TryMarkCheckTick(tick) && !HasAllPending())
            {
                return;
            }

            var caughtItemIds = GetPendingCaughtItemIds();
            var pendingIds = JoinInts(caughtItemIds);
            if (caughtItemIds.Count == 0)
            {
                RecordStoreDiagnostics(FishingAutoStoreModes.All, string.Empty, string.Empty, "no caught item ids pending");
                return;
            }

            string signature;
            int slotCount;
            int stackTotal;
            if (!QuestFishStorageCompat.TryBuildInventoryItemSignature(player, caughtItemIds, out signature, out slotCount, out stackTotal) || slotCount == 0 || stackTotal <= 0)
            {
                if (ShouldKeepAllModePendingForInventorySettle(tick))
                {
                    RecordLast(caughtItemIds[caughtItemIds.Count - 1], 0);
                    RecordStoreDiagnostics(FishingAutoStoreModes.All, string.Empty, pendingIds, "waiting for caught item to settle into inventory");
                    return;
                }

                ClearPendingCaughtItems();
                RecordLast(0, 0);
                RecordStoreDiagnostics(FishingAutoStoreModes.All, string.Empty, pendingIds, "caught item not found after settle window");
                return;
            }

            if (IsQueueBusy(queue))
            {
                RecordStoreDiagnostics(FishingAutoStoreModes.All, signature, pendingIds, "queue busy");
                return;
            }

            List<int> slots;
            int currentStackTotal;
            if (!QuestFishStorageCompat.TryFindInventoryItemSlots(player, caughtItemIds, out slots, out currentStackTotal) || slots.Count == 0)
            {
                if (ShouldKeepAllModePendingForInventorySettle(tick))
                {
                    RecordLast(caughtItemIds[caughtItemIds.Count - 1], 0);
                    RecordStoreDiagnostics(FishingAutoStoreModes.All, signature, pendingIds, "matching inventory slots not ready");
                    return;
                }

                ClearPendingCaughtItems();
                RecordLast(0, 0);
                RecordStoreDiagnostics(FishingAutoStoreModes.All, signature, pendingIds, "matching inventory slots missing after settle window");
                return;
            }

            var request = new InputActionRequest
            {
                Kind = InputActionKind.Chest,
                Priority = InputActionPriority.Low,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.FishingAutoStoreQuestFish,
                Description = "Fishing auto store caught items",
                QueueTimeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.FishingAutoStoreQuestFish + "|all",
                Timeout = TimeSpan.FromSeconds(3)
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.FishingAutoStoreAll;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["FishingStoreMode"] = FishingAutoStoreModes.All;
            request.Metadata["CaughtItemIds"] = JoinInts(caughtItemIds);
            request.Metadata["CaughtInventorySlots"] = JoinInts(slots);
            request.Metadata["InventorySignature"] = signature;
            request.Metadata["MovableSlotCount"] = slotCount.ToString(CultureInfo.InvariantCulture);
            request.Metadata["MovableStackTotal"] = stackTotal.ToString(CultureInfo.InvariantCulture);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordLast(caughtItemIds.Count == 0 ? 0 : caughtItemIds[caughtItemIds.Count - 1], slots.Count);
                RecordStoreDiagnostics(FishingAutoStoreModes.All, signature, pendingIds, "all caught storage admission denied: " + (admission == null ? "unknown" : admission.Reason));
                return;
            }

            lock (SyncRoot)
            {
                _lastAllRequestId = request.RequestId;
                _allStorePending = false;
                _pendingAllInventorySignature = signature;
                _allStorePendingSinceTick = 0;
                PendingCaughtItemIds.Clear();
                _lastItemId = caughtItemIds.Count == 0 ? 0 : caughtItemIds[caughtItemIds.Count - 1];
                _lastSlotCount = slots.Count;
            }
            RecordStoreDiagnostics(FishingAutoStoreModes.All, signature, pendingIds, "submitted caught item storage request");
        }

        public static void OnActionCompleted(InputActionResult result, long tick)
        {
            if (result == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (string.Equals(result.Scenario, ScenarioNames.FishingAutoFishPull, StringComparison.Ordinal))
                {
                    int caughtItemId;
                    if (PendingPullCatchItemIds.TryGetValue(result.RequestId, out caughtItemId))
                    {
                        PendingPullCatchItemIds.Remove(result.RequestId);
                        if (IsCatchPullUsableForStorage(result.Status) && caughtItemId > 0 && !PendingCaughtItemIds.Contains(caughtItemId))
                        {
                            PendingCaughtItemIds.Add(caughtItemId);
                            _allStorePending = true;
                            if (_allStorePendingSinceTick <= 0)
                            {
                                _allStorePendingSinceTick = tick;
                            }

                            _lastItemId = caughtItemId;
                            _lastSlotCount = PendingCaughtItemIds.Count;
                        }
                    }
                }

                if (string.Equals(result.Scenario, ScenarioNames.FishingAutoEquipmentApply, StringComparison.Ordinal) ||
                    string.Equals(result.Scenario, ScenarioNames.FishingAutoEquipmentRestore, StringComparison.Ordinal) ||
                    string.Equals(result.Scenario, ScenarioNames.FishingAutoLoadoutSwitch, StringComparison.Ordinal) ||
                    string.Equals(result.Scenario, ScenarioNames.FishingAutoLoadoutRestore, StringComparison.Ordinal))
                {
                    _allStorePending = false;
                    _pendingAllInventorySignature = string.Empty;
                    _allStorePendingSinceTick = 0;
                    PendingCaughtItemIds.Clear();
                }

                if (result.RequestId == _lastRequestId)
                {
                    _lastRequestId = Guid.Empty;
                }

                if (result.RequestId == _lastAllRequestId)
                {
                    _lastAllRequestId = Guid.Empty;
                    _allStorePending = false;
                    _pendingAllInventorySignature = string.Empty;
                    _allStorePendingSinceTick = 0;
                }
            }
        }

        private static bool IsCatchPullUsableForStorage(InputActionStatus status)
        {
            return status == InputActionStatus.Succeeded ||
                   status == InputActionStatus.AttemptedButUnverified;
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

        private static bool TryMarkCheckTick(long tick)
        {
            lock (SyncRoot)
            {
                if (tick - _lastCheckTick < 10)
                {
                    return false;
                }

                _lastCheckTick = tick;
                _cooldownTicks = 10;
                return true;
            }
        }

        private static bool HasAllPending()
        {
            lock (SyncRoot)
            {
                return _allStorePending || PendingCaughtItemIds.Count > 0;
            }
        }

        private static bool ShouldKeepAllModePendingForInventorySettle(long tick)
        {
            lock (SyncRoot)
            {
                if (PendingCaughtItemIds.Count == 0)
                {
                    return false;
                }

                if (_allStorePendingSinceTick <= 0)
                {
                    _allStorePendingSinceTick = tick;
                    return true;
                }

                return tick >= _allStorePendingSinceTick &&
                       tick - _allStorePendingSinceTick <= AllModeInventorySettleTicks;
            }
        }

        private static List<int> GetPendingCaughtItemIds()
        {
            lock (SyncRoot)
            {
                return new List<int>(PendingCaughtItemIds);
            }
        }

        private static void ClearPendingCaughtItems()
        {
            lock (SyncRoot)
            {
                PendingCaughtItemIds.Clear();
                _allStorePending = false;
                _pendingAllInventorySignature = string.Empty;
                _allStorePendingSinceTick = 0;
            }
        }

        private static void ResetAllTracking()
        {
            lock (SyncRoot)
            {
                _pendingAllInventorySignature = string.Empty;
                _allStorePending = false;
                _allStorePendingSinceTick = 0;
                PendingPullCatchItemIds.Clear();
                PendingCaughtItemIds.Clear();
            }
        }

        private static bool IsQueueBusy(InputActionQueue queue)
        {
            var queueSnapshot = queue == null ? null : queue.GetFastState();
            return queueSnapshot == null ||
                   queueSnapshot.PendingCount > 0 || queueSnapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static bool IsBlockedForFishingStorage(GameStateSnapshot snapshot)
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
                   snapshot.Ui.NpcChatOpen;
        }

        private static void RecordLast(int itemId, int slotCount)
        {
            lock (SyncRoot)
            {
                _lastItemId = itemId;
                _lastSlotCount = slotCount;
            }
        }

        private static void RecordStoreDiagnostics(string mode, string inventorySignature, string pendingItemIds, string message)
        {
            lock (SyncRoot)
            {
                _lastMode = mode ?? string.Empty;
                _lastInventorySignature = inventorySignature ?? string.Empty;
                _lastPendingItemIds = pendingItemIds ?? string.Empty;
                _lastDiagnosticMessage = message ?? string.Empty;
            }
        }
    }
}
