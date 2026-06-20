using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Common;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions
{
    public sealed partial class InputActionQueue
    {
        // The queue owns admission, ordering, cleanup leases, and terminal receipts.
        // Actual input or inventory mutation must stay inside executors and Compat.
        private readonly object _syncRoot = new object();
        private readonly List<InputActionRequest> _pending = new List<InputActionRequest>();
        private readonly InputActionResultStore _resultStore = new InputActionResultStore();
        private readonly Dictionary<InputActionKind, IInputActionExecutor> _executors;
        private readonly InputActionChannelArbiter _channelArbiter = new InputActionChannelArbiter();
        private InputActionExecution _running;
        private InputActionChannelLease _runningChannelLease;
        private InputActionCleanupLease _cleanupLease;
        private InputActionChannelDecision _lastChannelDecision;
        private InputActionAdmissionResult _lastAdmissionResult;
        private int _blockedPendingCount;
        private int _expiredPendingCount;
        private string _lastPendingExpiryReason = string.Empty;
        private int _supersededPendingCount;
        private int _coalescedPendingCount;
        private string _lastSchedulerSelectedRequest = string.Empty;
        private string _lastSchedulerSupersededRequest = string.Empty;
        private string _lastSchedulerFairnessBucket = string.Empty;
        private string _lastCleanupOwner = string.Empty;
        private string _lastCleanupReason = string.Empty;
        private int _directEnqueueCount;
        private string _lastDirectEnqueueKind = string.Empty;
        private string _lastDirectEnqueueSource = string.Empty;
        private string _lastDirectEnqueueScenario = string.Empty;
        private string _lastDirectEnqueueAdmissionKey = string.Empty;
        private string _lastDirectEnqueueRequiredChannels = string.Empty;
        private long _updateCount;
        private long _lastInputActionUpdateMs;

        public InputActionQueue()
        {
            _executors = CreateDefaultExecutors();
        }

        internal InputActionQueue(Dictionary<InputActionKind, IInputActionExecutor> executors)
        {
            _executors = executors ?? CreateDefaultExecutors();
        }

        internal IReadOnlyList<InputActionRequest> GetPendingRequestsForTesting()
        {
            lock (_syncRoot)
            {
                return new List<InputActionRequest>(_pending);
            }
        }

        public long UpdateCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _updateCount;
                }
            }
        }

        public Guid Enqueue(InputActionRequest request)
        {
            NormalizeRequest(request);

            lock (_syncRoot)
            {
                var profile = InputActionChannelResolver.Resolve(request);
                _directEnqueueCount++;
                _lastDirectEnqueueKind = request.Kind.ToString();
                _lastDirectEnqueueSource = request.SourceFeatureId ?? string.Empty;
                _lastDirectEnqueueScenario = profile.Scenario;
                _lastDirectEnqueueAdmissionKey = request.AdmissionKey ?? string.Empty;
                _lastDirectEnqueueRequiredChannels = InputActionChannelFormatter.Format(profile.EffectiveRequiredChannels);
                _pending.Add(request);
            }

            Logger.Debug("InputActionQueue", "Input action enqueued: " + request.Kind + " / " + request.Description);
            return request.RequestId;
        }

        public bool TryEnqueue(InputActionRequest request, out InputActionAdmissionResult admission)
        {
            // TryEnqueue is the admission contract for new triggers, not a raw list add.
            // It may reject, supersede, or coalesce before the request becomes pending.
            var operationStart = Stopwatch.GetTimestamp();
            NormalizeRequest(request);

            var accepted = false;
            lock (_syncRoot)
            {
                var now = DateTime.UtcNow;
                ExpireCleanupLeaseLocked(now);
                ExpirePendingLocked(now);
                admission = BuildAdmissionLocked(request, now);
                _lastAdmissionResult = admission;
                if (admission != null && admission.Accepted)
                {
                    ApplyAcceptedAdmissionLocked(request, admission, now);
                    accepted = true;
                }
            }

            RecordAdmissionPerformance(operationStart, request, admission);
            if (!accepted)
            {
                Logger.Debug(
                    "InputActionQueue",
                    "Input action admission denied: " +
                    (admission == null ? "unknown" : admission.Summary));
                return false;
            }

            Logger.Debug("InputActionQueue", "Input action admission accepted: " + request.Kind + " / " + request.Description + " / " + admission.Decision);
            return true;
        }

        private static void RecordAdmissionPerformance(long operationStart, InputActionRequest request, InputActionAdmissionResult admission)
        {
            var elapsedMs = PerformanceHitchRecorder.ElapsedMilliseconds(operationStart, Stopwatch.GetTimestamp());
            if (!PerformanceHitchRecorder.ShouldRecordOperationFast(elapsedMs, PerformanceHitchRecorder.ActionQueueAdmissionThresholdMs))
            {
                return;
            }

            var reason = admission == null
                ? "unknown"
                : admission.Decision + ":" + (admission.Reason ?? string.Empty);
            var ownerSummary = admission == null
                ? string.Empty
                : FirstNonEmpty(
                    admission.OwnerSummary,
                    admission.RunningConflictSummary,
                    admission.PendingConflictSummary,
                    admission.BridgeBusySummary);
            var metadata = request == null
                ? string.Empty
                : "kind=" + request.Kind +
                  ";source=" + (request.SourceFeatureId ?? string.Empty) +
                  ";scenario=" + (admission == null ? string.Empty : admission.Scenario) +
                  ";key=" + (request.AdmissionKey ?? string.Empty);

            PerformanceHitchRecorder.RecordOperationIfNeeded(
                "Performance.ActionQueue.Admission",
                elapsedMs,
                PerformanceHitchRecorder.ActionQueueAdmissionThresholdMs,
                TrimSummary(reason, 256),
                TrimSummary(ownerSummary, 512),
                TrimSummary(metadata, 512));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index];
                }
            }

            return string.Empty;
        }

        public void Update()
        {
            Update(null);
        }

        public void Update(GameStateSnapshot snapshot)
        {
            InputActionResult completed = null;
            var started = DateTime.UtcNow;

            lock (_syncRoot)
            {
                ExpireCleanupLeaseLocked(DateTime.UtcNow);
                _updateCount++;
                ExpirePendingLocked(DateTime.UtcNow);

                if (_running == null)
                {
                    completed = TryStartNextActionLocked(snapshot);
                    if (completed != null)
                    {
                        CompleteLocked(completed);
                    }
                }

                if (_running == null)
                {
                    _lastInputActionUpdateMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
                    return;
                }

                _running.UpdateCount++;
                _running.LastUpdateUtc = DateTime.UtcNow;

                completed = UpdateRunningActionLocked(snapshot);
                if (completed == null)
                {
                    _lastInputActionUpdateMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
                    return;
                }

                CompleteLocked(completed);
                _lastInputActionUpdateMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
            }
        }

        public bool TryStartNextAction()
        {
            lock (_syncRoot)
            {
                ExpireCleanupLeaseLocked(DateTime.UtcNow);
                var result = TryStartNextActionLocked(null);
                if (result != null)
                {
                    CompleteLocked(result);
                    return true;
                }

                return _running != null;
            }
        }

        public int CancelBySource(string sourceFeatureId)
        {
            var safeSource = sourceFeatureId ?? string.Empty;
            var cancelled = 0;

            lock (_syncRoot)
            {
                var now = DateTime.UtcNow;
                ExpireCleanupLeaseLocked(now);
                for (var index = _pending.Count - 1; index >= 0; index--)
                {
                    var request = _pending[index];
                    if (request == null ||
                        !string.Equals(request.SourceFeatureId ?? string.Empty, safeSource, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _pending.RemoveAt(index);
                    RecordResultLocked(InputActionResult.FromRequest(
                        request,
                        InputActionStatus.Cancelled,
                        "Pending action cancelled by source: " + safeSource,
                        request.CreatedUtc));
                    cancelled++;
                }

                if (_running != null &&
                    string.Equals(_running.Request.SourceFeatureId ?? string.Empty, safeSource, StringComparison.OrdinalIgnoreCase))
                {
                    var result = CancelRunningLocked(
                        InputActionStatus.Cancelled,
                        "Action cancelled by source: " + safeSource);
                    CompleteLocked(result);
                    cancelled++;
                }
            }

            return cancelled;
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _pending.Clear();
                if (_running != null)
                {
                    var result = CancelRunningLocked(
                        InputActionStatus.Cancelled,
                        "Action queue cleared.");
                    CompleteLocked(result);
                }
                else
                {
                    _channelArbiter.ReleaseAll("Action queue cleared.");
                    _runningChannelLease = null;
                }

                _cleanupLease = null;
            }
        }

        public bool CanSubmit(InputActionRequest request, GameStateSnapshot snapshot, out InputActionChannelDecision decision)
        {
            NormalizeRequest(request);
            lock (_syncRoot)
            {
                ExpireCleanupLeaseLocked(DateTime.UtcNow);
                if (TryBuildCleanupBlockedDecisionLocked(request, out decision))
                {
                    return false;
                }

                return _channelArbiter.CanAcquire(request, out decision);
            }
        }

        public bool IsSourcePendingOrRunning(string sourceFeatureId)
        {
            var safeSource = sourceFeatureId ?? string.Empty;
            lock (_syncRoot)
            {
                for (var index = 0; index < _pending.Count; index++)
                {
                    var request = _pending[index];
                    if (request != null &&
                        string.Equals(request.SourceFeatureId ?? string.Empty, safeSource, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return _running != null &&
                       _running.Request != null &&
                       string.Equals(_running.Request.SourceFeatureId ?? string.Empty, safeSource, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsAnyChannelBusy(InputActionChannel channels)
        {
            lock (_syncRoot)
            {
                ExpireCleanupLeaseLocked(DateTime.UtcNow);
                if (IsCleanupLeaseBlockingChannelsLocked(channels))
                {
                    return true;
                }

                return _channelArbiter.IsAnyChannelBusy(channels);
            }
        }

        public InputActionChannelSnapshot GetChannelSnapshot()
        {
            lock (_syncRoot)
            {
                return _channelArbiter.GetSnapshot();
            }
        }

        public InputActionExecution GetRunningAction()
        {
            lock (_syncRoot)
            {
                return _running;
            }
        }

        public IReadOnlyList<InputActionResult> GetRecentResults()
        {
            lock (_syncRoot)
            {
                return _resultStore.CopyRecentResults();
            }
        }

        public bool TryGetResultByRequestId(Guid requestId, out InputActionResult result)
        {
            lock (_syncRoot)
            {
                return _resultStore.TryGetResultByRequestId(requestId, out result);
            }
        }
        public bool HasRunningOrPendingActionFast()
        {
            lock (_syncRoot)
            {
                return _pending.Count > 0 || _running != null;
            }
        }


        private static void NormalizeRequest(InputActionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (request.RequestId == Guid.Empty)
            {
                request.RequestId = Guid.NewGuid();
            }

            if (request.CreatedUtc == default(DateTime))
            {
                request.CreatedUtc = DateTime.UtcNow;
            }

            if (request.Metadata == null)
            {
                request.Metadata = new Dictionary<string, string>();
            }

            if (string.IsNullOrWhiteSpace(request.AdmissionKey))
            {
                request.AdmissionKey = BuildDefaultAdmissionKey(request);
            }

            if (request.QueueTimeout > TimeSpan.Zero &&
                request.QueueExpiresUtc == default(DateTime))
            {
                request.QueueExpiresUtc = request.CreatedUtc + request.QueueTimeout;
            }

            if (request.Timeout <= TimeSpan.Zero)
            {
                request.Timeout = TimeSpan.FromSeconds(5);
            }
        }

        private static string BuildDefaultAdmissionKey(InputActionRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            var source = request.SourceFeatureId ?? string.Empty;
            var scenario = GetRequestMetadata(request, ActionMetadataKeys.Scenario);
            if (string.IsNullOrWhiteSpace(source))
            {
                return "request|" +
                       request.RequestId +
                       "|" + request.Kind +
                       "|" + scenario;
            }

            return source +
                   "|" + request.Kind +
                   "|" + scenario;
        }

        private static string BuildRequestOwnerSummary(InputActionRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            return request.Kind +
                   ":" + (request.SourceFeatureId ?? string.Empty) +
                   ":" + (request.AdmissionKey ?? string.Empty);
        }

        private static string GetRequestMetadata(InputActionRequest request, string key)
        {
            if (request == null || request.Metadata == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string value;
            return request.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string TrimSummary(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0 || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private IInputActionExecutor GetExecutor(InputActionKind kind)
        {
            IInputActionExecutor executor;
            return _executors.TryGetValue(kind, out executor)
                ? executor
                : new NotImplementedActionExecutor(kind);
        }

        private static Dictionary<InputActionKind, IInputActionExecutor> CreateDefaultExecutors()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            Register(executors, new DiagnosticNoopActionExecutor());
            Register(executors, new MouseTargetActionExecutor());
            Register(executors, new MouseTargetDryRunActionExecutor());
            Register(executors, new SelectHotbarSlotActionExecutor());
            Register(executors, new UseSelectedItemActionExecutor());
            Register(executors, new UseHotbarItemActionExecutor());
            Register(executors, new UseInventoryItemActionExecutor());
            Register(executors, new QuickActionExecutor(InputActionKind.QuickHeal, "QuickHeal", "Manual.QuickHeal"));
            Register(executors, new QuickActionExecutor(InputActionKind.QuickMana, "QuickMana", "Manual.QuickMana"));
            Register(executors, new QuickActionExecutor(InputActionKind.QuickBuff, "QuickBuff", "Manual.QuickBuff"));
            Register(executors, new BuffPotionDirectUseExecutor());
            Register(executors, new NpcInteractActionExecutor());
            Register(executors, new TileInteractActionExecutor());
            Register(executors, new InventorySlotActionExecutor());
            Register(executors, new ChestActionExecutor());
            Register(executors, new ShopActionExecutor());
            Register(executors, new ReforgeActionExecutor());
            Register(executors, new TrashSlotActionExecutor());
            Register(executors, new JumpActionExecutor());
            Register(executors, new DashActionExecutor());
            Register(executors, new RawInputActionExecutor());
            Register(executors, new PlayerRenameActionExecutor());
            Register(executors, new BlueprintAutoPlaceActionExecutor());
            return executors;
        }

        private static void Register(Dictionary<InputActionKind, IInputActionExecutor> executors, IInputActionExecutor executor)
        {
            executors[executor.Kind] = executor;
        }

    }
}
