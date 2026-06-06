using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public string LastDetectedItemIds { get; set; }
        public long PendingSinceTick { get; set; }
        public long LastPendingChangeTick { get; set; }
        public string LastPendingClearReason { get; set; }
        public string PendingTransactionState { get; set; }
        public int PendingRetryCount { get; set; }
        public string LastSubmitRequestId { get; set; }
        public string LastResult { get; set; }
        public string LastUnverifiedReason { get; set; }
        public string InventoryTransactionSlots { get; set; }
        public string InventoryTransactionBlockingReason { get; set; }
        public string ActionResultDeliveryMode { get; set; }
        public DateTime? LastDecisionUtc { get; set; }

        public AutoStackServiceDiagnostics()
        {
            LastDecision = string.Empty;
            LastInventorySignature = string.Empty;
            LastPendingItemIds = string.Empty;
            LastDetectedItemIds = string.Empty;
            LastPendingClearReason = string.Empty;
            PendingTransactionState = string.Empty;
            LastSubmitRequestId = string.Empty;
            LastResult = string.Empty;
            LastUnverifiedReason = string.Empty;
            InventoryTransactionSlots = string.Empty;
            InventoryTransactionBlockingReason = string.Empty;
            ActionResultDeliveryMode = string.Empty;
            LastPendingChangeTick = -1;
        }
    }

    public static class AutoStackService
    {
        private const long CheckIntervalTicks = 5;
        private const long PendingSettleTicks = 120;
        private const long InventoryOpenExecutionSettleTicks = 3;
        private const long SubmittedResultWaitTicks = 120;
        private const long RetryBackoffTicks = 60;
        private const long NoNearbyContainerRetryTicks = 180;
        private const long PendingNotApplicableTtlTicks = 600;
        private const int MaxRetryCount = 3;
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
        private static string _lastDetectedItemIds = string.Empty;
        private static string _lastPendingClearReason = string.Empty;
        private static AutoStackTransactionState _pendingTransactionState = AutoStackTransactionState.None;
        private static int _pendingRetryCount;
        private static Guid _activeSubmitRequestId = Guid.Empty;
        private static string _lastSubmitRequestId = string.Empty;
        private static string _lastResult = string.Empty;
        private static string _lastUnverifiedReason = string.Empty;
        private static string _inventoryTransactionSlots = string.Empty;
        private static string _inventoryTransactionBlockingReason = string.Empty;
        private static string _actionResultDeliveryMode = string.Empty;
        private static long _lastSubmitTick = -1;
        private static long _nextRetryTick = -1;
        private static DateTime? _lastDecisionUtc;

        private enum AutoStackTransactionState
        {
            None,
            Detected,
            PendingSettle,
            ReadyToSubmit,
            Admitted,
            WaitingForResult,
            VerifiedMoved,
            AttemptedButUnverified,
            RetryPending,
            NotApplicable,
            Failed,
            Expired
        }

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
            lock (SyncRoot)
            {
                return PendingItemIds.Count > 0 || _activeSubmitRequestId != Guid.Empty;
            }
        }

        internal static void ResetForTesting()
        {
            ClearTracking("reset");
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
                    LastDetectedItemIds = _lastDetectedItemIds,
                    PendingSinceTick = _pendingSinceTick,
                    LastPendingChangeTick = _lastPendingChangeTick,
                    LastPendingClearReason = _lastPendingClearReason,
                    PendingTransactionState = _pendingTransactionState.ToString(),
                    PendingRetryCount = _pendingRetryCount,
                    LastSubmitRequestId = _lastSubmitRequestId,
                    LastResult = _lastResult,
                    LastUnverifiedReason = _lastUnverifiedReason,
                    InventoryTransactionSlots = _inventoryTransactionSlots,
                    InventoryTransactionBlockingReason = _inventoryTransactionBlockingReason,
                    ActionResultDeliveryMode = _actionResultDeliveryMode,
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

                // Stack increases are transaction anchors. A later baseline refresh
                // must not erase them while UI, queue, or verification is unsafe.
                var added = UpdateBaselineAndDetectIncreases(current, eligibleItemTypes);
                if (added.Count > 0)
                {
                    AddPendingItemIds(added, tick);
                }

                var unsafeUiOpen = IsUnsafeUiOpenForAutoStack(gameState);
                if (unsafeUiOpen)
                {
                    RecordDecision(
                        added.Count > 0
                            ? "unsafe UI open; detected picked item stack increase and retained pending transaction"
                            : "unsafe UI open",
                        string.Empty,
                        JoinInts(GetPendingItemIds()),
                        JoinInts(added));
                    return;
                }
                else
                {
                    if (added.Count > 0 && IsPlayerInventoryOpen(gameState))
                    {
                        RecordDecision("detected picked item stack increase while inventory open", string.Empty, JoinInts(GetPendingItemIds()), JoinInts(added));
                    }
                    else if (added.Count > 0)
                    {
                        RecordDecision("detected picked item stack increase", string.Empty, JoinInts(GetPendingItemIds()), JoinInts(added));
                    }
                }
            }

            if (TryHandleSubmittedActionResult(queue, tick))
            {
                return;
            }

            if (!HasPendingItemIds())
            {
                return;
            }

            string blockReason;
            if (TryGetExecutionBlockedReason(gameState, tick, out blockReason))
            {
                SetTransactionBlockingReason(blockReason);
                RecordDecision(blockReason, string.Empty, JoinInts(GetPendingItemIds()));
                return;
            }

            if (IsQueueBusy(queue))
            {
                SetTransactionBlockingReason("queue busy");
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
                    SetTransactionState(AutoStackTransactionState.PendingSettle);
                    RecordDecision("waiting for picked item to settle into inventory", string.Empty, JoinInts(pendingIds));
                    return;
                }

                ClearPendingItemIds("picked item no longer present", AutoStackTransactionState.NotApplicable);
                RecordDecision("picked item no longer present", string.Empty, JoinInts(pendingIds));
                return;
            }

            // Queue admission starts QuickStack, but it is not proof that items
            // moved. Keep the pending transaction until terminal verification.
            SetTransactionState(AutoStackTransactionState.ReadyToSubmit);
            var request = BuildAutoStackRequest(pendingIds, slots, signature, slotCount, stackTotal);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                SetTransactionBlockingReason(admission == null ? "admission denied: unknown" : "admission denied: " + admission.Reason);
                RecordDecision("auto stack admission denied: " + (admission == null ? "unknown" : admission.Reason), signature, JoinInts(pendingIds));
                return;
            }

            var submittedRequestId = ResolveSubmittedRequestId(request, admission);
            MarkSubmitted(submittedRequestId, slots, tick);
            RecordDecision(
                admission.Decision == InputActionAdmissionDecision.CoalescedPending
                    ? "coalesced auto stack request"
                    : "submitted auto stack request",
                signature,
                JoinInts(pendingIds));
        }

        private static InputActionRequest BuildAutoStackRequest(IReadOnlyList<int> itemIds, IReadOnlyList<int> slots, string signature, int slotCount, int stackTotal)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Chest,
                Priority = InputActionPriority.Low,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.InventoryAutoStack,
                Description = "Auto stack picked up items",
                QueueTimeout = TimeSpan.FromSeconds(2),
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

        private static bool TryHandleSubmittedActionResult(InputActionQueue queue, long tick)
        {
            Guid activeRequestId;
            long submittedTick;
            long nextRetryTick;
            lock (SyncRoot)
            {
                activeRequestId = _activeSubmitRequestId;
                submittedTick = _lastSubmitTick;
                nextRetryTick = _nextRetryTick;
            }

            if (activeRequestId != Guid.Empty)
            {
                var operationStart = Stopwatch.GetTimestamp();
                var performanceReason = string.Empty;
                InputActionResult result;
                if (queue != null && queue.TryGetResultByRequestId(activeRequestId, out result))
                {
                    HandleSubmittedActionResult(result, tick);
                    performanceReason = result == null ? "resultMissing" : "result:" + result.Status;
                    RecordInventoryTransactionVerifyPerformance(operationStart, activeRequestId, performanceReason);
                    return true;
                }

                if (submittedTick < 0 || tick - submittedTick <= SubmittedResultWaitTicks)
                {
                    SetTransactionState(AutoStackTransactionState.WaitingForResult);
                    RecordDecision("waiting for auto stack action result", string.Empty, JoinInts(GetPendingItemIds()));
                    RecordInventoryTransactionVerifyPerformance(operationStart, activeRequestId, "waitingForResult");
                    return true;
                }

                RegisterRetry(
                    tick,
                    "auto stack action result unavailable for request " + activeRequestId,
                    AutoStackTransactionState.Failed,
                    RetryBackoffTicks);
                RecordInventoryTransactionVerifyPerformance(operationStart, activeRequestId, "resultUnavailable");
                return true;
            }

            if (nextRetryTick >= 0 && tick < nextRetryTick)
            {
                SetTransactionState(AutoStackTransactionState.RetryPending);
                RecordDecision("waiting for auto stack retry backoff", string.Empty, JoinInts(GetPendingItemIds()));
                return true;
            }

            return false;
        }

        private static void RecordInventoryTransactionVerifyPerformance(long operationStart, Guid requestId, string reason)
        {
            var elapsedMs = PerformanceHitchRecorder.ElapsedMilliseconds(operationStart, Stopwatch.GetTimestamp());
            if (!PerformanceHitchRecorder.ShouldRecordOperationFast(elapsedMs, PerformanceHitchRecorder.InventoryTransactionVerifyThresholdMs))
            {
                return;
            }

            string state;
            string slots;
            string blockingReason;
            int pendingCount;
            int retryCount;
            lock (SyncRoot)
            {
                state = _pendingTransactionState.ToString();
                slots = _inventoryTransactionSlots;
                blockingReason = _inventoryTransactionBlockingReason;
                pendingCount = PendingItemIds.Count;
                retryCount = _pendingRetryCount;
            }

            var metadata =
                "requestId=" + (requestId == Guid.Empty ? string.Empty : requestId.ToString()) +
                ";state=" + state +
                ";pendingCount=" + pendingCount.ToString(CultureInfo.InvariantCulture) +
                ";retry=" + retryCount.ToString(CultureInfo.InvariantCulture) +
                ";slots=" + slots;

            PerformanceHitchRecorder.RecordOperationIfNeeded(
                "Performance.InventoryTransaction.Verify",
                elapsedMs,
                PerformanceHitchRecorder.InventoryTransactionVerifyThresholdMs,
                TrimSummary(reason ?? string.Empty, 256),
                TrimSummary(blockingReason, 512),
                TrimSummary(metadata, 512));
        }

        private static void HandleSubmittedActionResult(InputActionResult result, long tick)
        {
            RecordSubmittedResult(result);
            if (result == null)
            {
                RegisterRetry(tick, "auto stack result missing", AutoStackTransactionState.Failed, RetryBackoffTicks);
                return;
            }

            if (result.Status == InputActionStatus.Succeeded)
            {
                // A succeeded result only clears the submitted snapshot. Newer
                // detected items stay pending so their recovery anchor survives.
                if (HasPendingChangedAfterLastSubmit())
                {
                    lock (SyncRoot)
                    {
                        _pendingTransactionState = AutoStackTransactionState.Detected;
                        _pendingRetryCount = 0;
                        _lastSubmitTick = -1;
                        _nextRetryTick = -1;
                    }

                    RecordDecision("verified previous auto stack request; newer pending transaction remains", string.Empty, JoinInts(GetPendingItemIds()));
                    return;
                }

                ClearPendingItemIds("verified auto stack request succeeded", AutoStackTransactionState.VerifiedMoved);
                RecordDecision("verified auto stack request succeeded", string.Empty, string.Empty);
                return;
            }

            if (result.Status == InputActionStatus.NotApplicable)
            {
                var message = string.IsNullOrWhiteSpace(result.Message) ? "not applicable" : result.Message;
                var retryTicks = LooksLikeNoNearbyContainers(message)
                    ? NoNearbyContainerRetryTicks
                    : RetryBackoffTicks;
                RegisterRetry(tick, message, AutoStackTransactionState.NotApplicable, retryTicks);
                return;
            }

            if (result.Status == InputActionStatus.AttemptedButUnverified)
            {
                RegisterRetry(tick, string.IsNullOrWhiteSpace(result.Message) ? "attempted but unverified" : result.Message, AutoStackTransactionState.AttemptedButUnverified, RetryBackoffTicks);
                return;
            }

            if (result.Status == InputActionStatus.BlockedByUi ||
                result.Status == InputActionStatus.Failed ||
                result.Status == InputActionStatus.TimedOut ||
                result.Status == InputActionStatus.Cancelled)
            {
                RegisterRetry(tick, string.IsNullOrWhiteSpace(result.Message) ? result.Status.ToString() : result.Message, AutoStackTransactionState.Failed, RetryBackoffTicks);
                return;
            }

            RegisterRetry(tick, result.Status + ": " + (result.Message ?? string.Empty), AutoStackTransactionState.Failed, RetryBackoffTicks);
        }

        private static void RegisterRetry(long tick, string reason, AutoStackTransactionState sourceState, long retryDelayTicks)
        {
            var pendingIds = GetPendingItemIds();
            if (pendingIds.Count == 0)
            {
                ClearPendingItemIds("auto stack result arrived after pending cleared: " + reason, sourceState);
                RecordDecision("auto stack result ignored after pending cleared: " + reason, string.Empty, string.Empty);
                return;
            }

            bool stop;
            int retryCount;
            lock (SyncRoot)
            {
                _activeSubmitRequestId = Guid.Empty;
                _lastSubmitTick = -1;
                _lastUnverifiedReason = reason ?? string.Empty;
                _inventoryTransactionBlockingReason = reason ?? string.Empty;
                if (_pendingSinceTick <= 0)
                {
                    _pendingSinceTick = tick;
                }

                var age = tick >= _pendingSinceTick ? tick - _pendingSinceTick : 0;
                stop = _pendingRetryCount >= MaxRetryCount ||
                       age > PendingNotApplicableTtlTicks;
                if (!stop)
                {
                    // Unverified, blocked, or unavailable QuickStack attempts wait
                    // with backoff instead of refreshing the baseline as success.
                    _pendingRetryCount++;
                    _nextRetryTick = tick + Math.Max(1, retryDelayTicks);
                    _pendingTransactionState = AutoStackTransactionState.RetryPending;
                }
                else
                {
                    _nextRetryTick = -1;
                    _pendingTransactionState = sourceState == AutoStackTransactionState.NotApplicable
                        ? AutoStackTransactionState.Expired
                        : AutoStackTransactionState.Failed;
                }

                retryCount = _pendingRetryCount;
            }

            if (stop)
            {
                ClearPendingItemIds("auto stack stopped after retry limit: " + reason, AutoStackTransactionState.Expired);
                RecordDecision("auto stack stopped after retry limit: " + reason, string.Empty, JoinInts(pendingIds));
                return;
            }

            RecordDecision("auto stack retry pending: " + reason + " retry=" + retryCount.ToString(CultureInfo.InvariantCulture), string.Empty, JoinInts(pendingIds));
        }

        private static Guid ResolveSubmittedRequestId(InputActionRequest request, InputActionAdmissionResult admission)
        {
            if (admission != null &&
                admission.Decision == InputActionAdmissionDecision.CoalescedPending &&
                admission.CoalescedRequestId != Guid.Empty)
            {
                return admission.CoalescedRequestId;
            }

            return request == null ? Guid.Empty : request.RequestId;
        }

        private static void MarkSubmitted(Guid requestId, IReadOnlyList<int> slots, long tick)
        {
            lock (SyncRoot)
            {
                _activeSubmitRequestId = requestId;
                _lastSubmitRequestId = requestId == Guid.Empty ? string.Empty : requestId.ToString();
                _lastSubmitTick = tick;
                _nextRetryTick = -1;
                _pendingTransactionState = AutoStackTransactionState.Admitted;
                _inventoryTransactionSlots = JoinInts(slots);
                _inventoryTransactionBlockingReason = string.Empty;
                _actionResultDeliveryMode = "RequestIdLookup";
            }
        }

        private static void RecordSubmittedResult(InputActionResult result)
        {
            if (result == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _activeSubmitRequestId = Guid.Empty;
                _actionResultDeliveryMode = "RequestIdLookup";
                _lastResult = result.Status + ":" + (result.ResultCode ?? string.Empty) + ":" + TrimSummary(result.Message, 160);
                if (result.Status == InputActionStatus.AttemptedButUnverified ||
                    result.Status == InputActionStatus.NotApplicable ||
                    result.Status == InputActionStatus.BlockedByUi ||
                    result.Status == InputActionStatus.Failed ||
                    result.Status == InputActionStatus.TimedOut ||
                    result.Status == InputActionStatus.Cancelled)
                {
                    _lastUnverifiedReason = result.Message ?? string.Empty;
                    _inventoryTransactionBlockingReason = result.Message ?? string.Empty;
                }
            }
        }

        private static bool HasPendingChangedAfterLastSubmit()
        {
            lock (SyncRoot)
            {
                return _lastSubmitTick >= 0 &&
                       _lastPendingChangeTick > _lastSubmitTick &&
                       PendingItemIds.Count > 0;
            }
        }

        private static void SetTransactionState(AutoStackTransactionState state)
        {
            lock (SyncRoot)
            {
                _pendingTransactionState = state;
            }
        }

        private static void SetTransactionBlockingReason(string reason)
        {
            lock (SyncRoot)
            {
                _inventoryTransactionBlockingReason = reason ?? string.Empty;
            }
        }

        private static bool LooksLikeNoNearbyContainers(string message)
        {
            return !string.IsNullOrWhiteSpace(message) &&
                   (message.IndexOf("nearby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("chest", StringComparison.OrdinalIgnoreCase) >= 0);
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
                    _lastPendingClearReason = string.Empty;
                    if (_activeSubmitRequestId == Guid.Empty)
                    {
                        _pendingTransactionState = AutoStackTransactionState.Detected;
                        _inventoryTransactionBlockingReason = string.Empty;
                    }
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

        private static void ClearPendingItemIds(string reason)
        {
            ClearPendingItemIds(reason, AutoStackTransactionState.None);
        }

        private static void ClearPendingItemIds(string reason, AutoStackTransactionState finalState)
        {
            lock (SyncRoot)
            {
                // Only final transaction states may clear the recovery anchor.
                // Submit-time cleanup would hide picked items that still need retry.
                PendingItemIds.Clear();
                _pendingSinceTick = 0;
                _lastPendingChangeTick = -1;
                _lastPendingClearReason = reason ?? string.Empty;
                _pendingTransactionState = finalState;
                if (finalState == AutoStackTransactionState.None ||
                    finalState == AutoStackTransactionState.VerifiedMoved ||
                    finalState == AutoStackTransactionState.NotApplicable)
                {
                    _pendingRetryCount = 0;
                }
                _activeSubmitRequestId = Guid.Empty;
                _lastSubmitTick = -1;
                _nextRetryTick = -1;
                _inventoryTransactionSlots = string.Empty;
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

                // A just-picked item may need a short settle window before its
                // inventory slots are readable; keep pending instead of dropping it.
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
                _lastDetectedItemIds = string.Empty;
                _lastPendingClearReason = decision ?? string.Empty;
                _pendingTransactionState = AutoStackTransactionState.None;
                _pendingRetryCount = 0;
                _activeSubmitRequestId = Guid.Empty;
                _lastSubmitRequestId = string.Empty;
                _lastResult = string.Empty;
                _lastUnverifiedReason = string.Empty;
                _inventoryTransactionSlots = string.Empty;
                _inventoryTransactionBlockingReason = string.Empty;
                _actionResultDeliveryMode = string.Empty;
                _lastSubmitTick = -1;
                _nextRetryTick = -1;
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
            RecordDecision(decision, inventorySignature, pendingItemIds, string.Empty);
        }

        private static void RecordDecision(string decision, string inventorySignature, string pendingItemIds, string detectedItemIds)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastInventorySignature = inventorySignature ?? string.Empty;
                _lastPendingItemIds = pendingItemIds ?? string.Empty;
                _lastDetectedItemIds = detectedItemIds ?? string.Empty;
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

        private static string TrimSummary(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0 || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength) + "...";
        }
    }
}
