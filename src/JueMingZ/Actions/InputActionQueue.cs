using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Common;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions
{
    public sealed class InputActionQueue
    {
        // The queue owns admission, ordering, cleanup leases, and terminal receipts.
        // Actual input or inventory mutation must stay inside executors and Compat.
        private const int MaxRecentResults = 12;
        private static readonly TimeSpan DefaultCleanupLeaseDuration = TimeSpan.FromMilliseconds(250);
        private readonly object _syncRoot = new object();
        private readonly List<InputActionRequest> _pending = new List<InputActionRequest>();
        private readonly List<InputActionResult> _recentResults = new List<InputActionResult>();
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
        private InputActionResult _lastResult;
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
                return InputActionDiagnostics.CopyRecentResults(_recentResults);
            }
        }

        public bool TryGetResultByRequestId(Guid requestId, out InputActionResult result)
        {
            lock (_syncRoot)
            {
                result = null;
                if (requestId == Guid.Empty)
                {
                    return false;
                }

                if (_lastResult != null && _lastResult.RequestId == requestId)
                {
                    result = _lastResult;
                    return true;
                }

                // Terminal result lookup is a recovery receipt path, not just a
                // diagnostics convenience for the snapshot writer.
                for (var index = _recentResults.Count - 1; index >= 0; index--)
                {
                    var candidate = _recentResults[index];
                    if (candidate != null && candidate.RequestId == requestId)
                    {
                        result = candidate;
                        return true;
                    }
                }

                return false;
            }
        }

        public InputActionQueueSnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                var channelSnapshot = _channelArbiter.GetSnapshot();
                return new InputActionQueueSnapshot
                {
                    PendingCount = _pending.Count,
                    UpdateCount = _updateCount,
                    RunningAction = _running == null ? string.Empty : _running.Request.Kind + ": " + _running.Request.Description,
                    RunningActionKind = _running == null ? string.Empty : _running.Request.Kind.ToString(),
                    RunningActionSource = _running == null ? string.Empty : _running.Request.SourceFeatureId ?? string.Empty,
                    RunningActionStatus = _running == null ? string.Empty : _running.Status.ToString(),
                    LastResult = _lastResult,
                    RecentResultsCount = _recentResults.Count,
                    LastInputActionUpdateMs = _lastInputActionUpdateMs,
                    RecentActionLine1 = GetRecentResultLineFromNewest(0),
                    RecentActionLine2 = GetRecentResultLineFromNewest(1),
                    RecentActionLine3 = GetRecentResultLineFromNewest(2),
                    ActionQueueChannelLeaseCount = channelSnapshot.LeaseCount,
                    ActionQueueRunningChannels = channelSnapshot.OccupiedChannelNames,
                    ActionQueueOccupiedChannels = channelSnapshot.OccupiedChannelNames,
                    ActionQueueBridgeBusyChannels = channelSnapshot.BridgeBusyChannelNames,
                    ActionQueueRunningLeaseChannels = channelSnapshot.RunningLeaseChannelNames,
                    ActionQueueBlockedPendingCount = _blockedPendingCount,
                    ActionQueueLastChannelDecision = _lastChannelDecision == null ? string.Empty : _lastChannelDecision.Summary,
                    ActionQueueLastChannelBlockedReason = _lastChannelDecision == null || _lastChannelDecision.Allowed ? string.Empty : _lastChannelDecision.Reason,
                    ActionQueueChannelOwnerSummary = channelSnapshot.OwnerSummary,
                    ActionQueueBridgeBusySummary = channelSnapshot.BridgeBusySummary,
                    ActionQueuePendingChannelSummary = BuildPendingChannelSummaryLocked(),
                    ActionQueuePendingOwnerSummary = BuildPendingOwnerSummaryLocked(),
                    ActionQueueLastAdmissionStatus = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.Status,
                    ActionQueueLastAdmissionDecision = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.Decision.ToString(),
                    ActionQueueLastAdmissionReason = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.Reason,
                    ActionQueueLastAdmissionKind = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.Kind.ToString(),
                    ActionQueueLastAdmissionSource = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.SourceFeatureId ?? string.Empty,
                    ActionQueueLastAdmissionScenario = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.Scenario ?? string.Empty,
                    ActionQueueLastAdmissionKey = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.AdmissionKey ?? string.Empty,
                    ActionQueueLastAdmissionRequiredChannels = _lastAdmissionResult == null ? InputActionChannelFormatter.Format(InputActionChannel.None) : InputActionChannelFormatter.Format(_lastAdmissionResult.RequiredChannels),
                    ActionQueueLastAdmissionBlockingChannels = _lastAdmissionResult == null ? InputActionChannelFormatter.Format(InputActionChannel.None) : InputActionChannelFormatter.Format(_lastAdmissionResult.BlockingChannels),
                    ActionQueueLastAdmissionConflictChannels = _lastAdmissionResult == null ? InputActionChannelFormatter.Format(InputActionChannel.None) : InputActionChannelFormatter.Format(_lastAdmissionResult.ConflictChannels),
                    ActionQueueLastAdmissionPendingConflictSummary = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.PendingConflictSummary ?? string.Empty,
                    ActionQueueLastAdmissionRunningConflictSummary = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.RunningConflictSummary ?? string.Empty,
                    ActionQueueLastAdmissionBridgeBusySummary = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.BridgeBusySummary ?? string.Empty,
                    ActionQueueLastAdmissionOwnerSummary = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.OwnerSummary ?? string.Empty,
                    ActionQueueLastAdmissionSupersededRequestId = _lastAdmissionResult == null || _lastAdmissionResult.SupersededRequestId == Guid.Empty ? string.Empty : _lastAdmissionResult.SupersededRequestId.ToString(),
                    ActionQueueLastAdmissionCoalescedRequestId = _lastAdmissionResult == null || _lastAdmissionResult.CoalescedRequestId == Guid.Empty ? string.Empty : _lastAdmissionResult.CoalescedRequestId.ToString(),
                    ActionQueueSupersededPendingCount = _supersededPendingCount,
                    ActionQueueCoalescedPendingCount = _coalescedPendingCount,
                    SchedulerLastSelectedRequest = _lastSchedulerSelectedRequest,
                    SchedulerLastSupersededRequest = _lastSchedulerSupersededRequest,
                    SchedulerLastFairnessBucket = _lastSchedulerFairnessBucket,
                    BackgroundRequestCoalescedCount = _coalescedPendingCount,
                    ExpiredPendingDroppedCount = _expiredPendingCount,
                    ActionQueueCleanupLeaseCount = IsCleanupLeaseActiveLocked(DateTime.UtcNow) ? 1 : 0,
                    ActionQueueCleanupLeaseChannels = IsCleanupLeaseActiveLocked(DateTime.UtcNow) ? InputActionChannelFormatter.Format(_cleanupLease.Channels) : InputActionChannelFormatter.Format(InputActionChannel.None),
                    ActionQueueLastCleanupOwner = _lastCleanupOwner,
                    ActionQueueLastCleanupReason = _lastCleanupReason,
                    ActionQueueDirectEnqueueCount = _directEnqueueCount,
                    ActionQueueLastDirectEnqueueKind = _lastDirectEnqueueKind,
                    ActionQueueLastDirectEnqueueSource = _lastDirectEnqueueSource,
                    ActionQueueLastDirectEnqueueScenario = _lastDirectEnqueueScenario,
                    ActionQueueLastDirectEnqueueAdmissionKey = _lastDirectEnqueueAdmissionKey,
                    ActionQueueLastDirectEnqueueRequiredChannels = _lastDirectEnqueueRequiredChannels,
                    ActionQueueExpiredPendingCount = _expiredPendingCount,
                    ActionQueueLastPendingExpiryReason = _lastPendingExpiryReason
                };
            }
        }

        public InputActionQueueFastState GetFastState()
        {
            lock (_syncRoot)
            {
                ExpireCleanupLeaseLocked(DateTime.UtcNow);
                var channelState = _channelArbiter.GetFastState();
                var cleanupChannels = IsCleanupLeaseActiveLocked(DateTime.UtcNow)
                    ? _cleanupLease.Channels
                    : InputActionChannel.None;
                var occupiedChannels = channelState.OccupiedChannels | cleanupChannels;
                return new InputActionQueueFastState
                {
                    PendingCount = _pending.Count,
                    UpdateCount = _updateCount,
                    HasRunningAction = _running != null,
                    RunningActionKindValue = _running == null ? InputActionKind.None : _running.Request.Kind,
                    RunningActionSource = _running == null ? string.Empty : _running.Request.SourceFeatureId ?? string.Empty,
                    LastInputActionUpdateMs = _lastInputActionUpdateMs,
                    LastResult = _lastResult,
                    ChannelLeaseCount = channelState.LeaseCount + (cleanupChannels == InputActionChannel.None ? 0 : 1),
                    HasChannelLease = channelState.HasLease || cleanupChannels != InputActionChannel.None,
                    OccupiedChannelsValue = occupiedChannels,
                    RunningLeaseChannelsValue = channelState.RunningLeaseChannels,
                    BridgeBusyChannelsValue = channelState.BridgeBusyChannels,
                    IsBridgeBusy = channelState.IsBridgeBusy,
                    OccupiedChannelCount = CountKnownChannels(occupiedChannels),
                    RunningLeaseChannelCount = channelState.RunningLeaseChannelCount,
                    BridgeBusyChannelCount = channelState.BridgeBusyChannelCount,
                    CleanupLeaseChannelsValue = cleanupChannels,
                    CleanupLeaseOwner = cleanupChannels == InputActionChannel.None ? string.Empty : _cleanupLease.OwnerSummary
                };
            }
        }
        public bool HasRunningOrPendingActionFast()
        {
            lock (_syncRoot)
            {
                return _pending.Count > 0 || _running != null;
            }
        }


        private InputActionResult TryStartNextActionLocked(GameStateSnapshot snapshot)
        {
            // Priority and bucket sorting only choose the next pending request; active
            // running actions are not preempted here.
            if (_running != null)
            {
                return null;
            }

            ExpirePendingLocked(DateTime.UtcNow);
            ExpireCleanupLeaseLocked(DateTime.UtcNow);
            if (_pending.Count == 0)
            {
                return null;
            }

            InputActionChannelDecision selectedDecision;
            var request = SelectNextStartableActionLocked(out selectedDecision);
            if (request == null)
            {
                return null;
            }

            if (selectedDecision != null)
            {
                _lastChannelDecision = selectedDecision;
            }

            _lastSchedulerSelectedRequest = BuildRequestOwnerSummary(request);
            _lastSchedulerFairnessBucket = InputActionScheduler.ResolveBucketName(request);

            InputActionChannelLease lease;
            InputActionChannelDecision acquireDecision;
            if (!_channelArbiter.TryAcquire(request, out lease, out acquireDecision))
            {
                _lastChannelDecision = acquireDecision;
                return null;
            }

            _pending.Remove(request);
            _running = new InputActionExecution
            {
                Request = request,
                Status = InputActionStatus.Running,
                StartedUtc = DateTime.UtcNow,
                LastUpdateUtc = DateTime.UtcNow,
                Message = "Running"
            };
            _runningChannelLease = lease;
            if (_running.State != null)
            {
                _running.State["ChannelRequired"] = InputActionChannelFormatter.Format(acquireDecision.RequiredChannels);
                _running.State["ChannelConflicts"] = InputActionChannelFormatter.Format(acquireDecision.ConflictChannels);
                _running.State["ChannelDecision"] = acquireDecision.Summary;
            }

            _lastChannelDecision = acquireDecision;
            Logger.Info("InputActionQueue", "Input action started: " + request.Kind + " / " + request.Description);

            var executor = GetExecutor(request.Kind);
            InputActionExecutionStepResult step;
            try
            {
                step = executor.Start(_running, snapshot);
            }
            catch (Exception error)
            {
                _running.Status = InputActionStatus.Failed;
                _running.Message = "Action start failed: " + error.Message;
                _running.Error = error;
                Logger.Error("InputActionQueue", "Input action start failed: " + request.Kind, error);
                return InputActionResult.FromExecution(_running, InputActionStatus.Failed, _running.Message, error);
            }

            _running.Status = step.Status;
            _running.Message = step.Message ?? string.Empty;
            _running.Error = step.Error;
            if (step.IsTerminal)
            {
                return InputActionResult.FromExecution(_running, step.Status, step.Message, step.Error);
            }

            return null;
        }

        private InputActionRequest SelectNextStartableActionLocked(out InputActionChannelDecision selectedDecision)
        {
            selectedDecision = null;
            var ordered = new List<InputActionRequest>(_pending);
            ordered.Sort(InputActionScheduler.ComparePriorityThenCreated);

            var blocked = 0;
            InputActionChannelDecision lastBlocked = null;
            for (var index = 0; index < ordered.Count; index++)
            {
                var request = ordered[index];
                InputActionChannelDecision decision;
                if (TryBuildCleanupBlockedDecisionLocked(request, out decision))
                {
                    blocked++;
                    lastBlocked = decision;
                    continue;
                }

                if (_channelArbiter.CanAcquire(request, out decision))
                {
                    selectedDecision = decision;
                    _blockedPendingCount = blocked;
                    if (lastBlocked != null)
                    {
                        _lastChannelDecision = lastBlocked;
                    }

                    return request;
                }

                blocked++;
                lastBlocked = decision;
            }

            _blockedPendingCount = blocked;
            if (lastBlocked != null)
            {
                _lastChannelDecision = lastBlocked;
            }

            return null;
        }

        private InputActionAdmissionResult BuildAdmissionLocked(InputActionRequest request, DateTime now)
        {
            var profile = InputActionChannelResolver.Resolve(request);
            var pendingSummary = BuildPendingConflictSummaryLocked(request, profile);
            var result = new InputActionAdmissionResult
            {
                Accepted = true,
                Decision = InputActionAdmissionDecision.Accepted,
                RequestId = request.RequestId,
                Kind = request.Kind,
                SourceFeatureId = request.SourceFeatureId ?? string.Empty,
                Scenario = profile.Scenario,
                AdmissionKey = request.AdmissionKey ?? string.Empty,
                RequiredChannels = profile.EffectiveRequiredChannels,
                ConflictChannels = profile.ConflictChannels,
                BlockingChannels = InputActionChannel.None,
                Reason = "accepted",
                PendingConflictSummary = pendingSummary
            };

            if (IsExpiredBeforeStart(request, now))
            {
                result.Accepted = false;
                result.Decision = InputActionAdmissionDecision.DeniedExpiredBeforeEnqueue;
                result.ExpiredBeforeEnqueue = true;
                result.Reason = "expiredBeforeEnqueue";
                return result;
            }

            InputActionChannelDecision cleanupDecision;
            if (TryBuildCleanupBlockedDecisionLocked(request, out cleanupDecision))
            {
                // Cleanup leases protect post-action restore windows; later requests that
                // share those channels must wait instead of racing the executor cleanup.
                result.Accepted = false;
                result.Decision = InputActionAdmissionDecision.DeniedCleanupLease;
                result.CleanupLeaseBlocked = true;
                result.BlockingChannels = cleanupDecision.BlockingChannels;
                result.OwnerSummary = cleanupDecision.OwnerSummary;
                result.Reason = cleanupDecision.Reason;
                return result;
            }

            InputActionRequest duplicateRunning;
            if (TryFindDuplicateRunningLocked(request, out duplicateRunning))
            {
                result.Accepted = false;
                result.Decision = InputActionAdmissionDecision.DeniedDuplicatePendingOrRunning;
                result.DuplicatePendingOrRunning = true;
                result.RunningConflictSummary = "running:" + BuildRequestOwnerSummary(duplicateRunning);
                result.OwnerSummary = result.RunningConflictSummary;
                result.Reason = "duplicateRunning:" + BuildRequestOwnerSummary(duplicateRunning);
                return result;
            }

            InputActionRequest duplicatePending;
            if (TryFindDuplicatePendingLocked(request, out duplicatePending))
            {
                // Duplicate policies only rewrite not-yet-started work. A running owner
                // must finish, timeout, or be cancelled through its executor cleanup path.
                var pendingOwner = "pending:" + BuildRequestOwnerSummary(duplicatePending);
                result.PendingConflictSummary = string.IsNullOrWhiteSpace(result.PendingConflictSummary)
                    ? pendingOwner
                    : result.PendingConflictSummary + "; " + pendingOwner;
                result.OwnerSummary = pendingOwner;

                if (request.DuplicatePolicy == InputActionDuplicatePolicy.SupersedePending)
                {
                    result.Accepted = true;
                    result.Decision = InputActionAdmissionDecision.SupersededPending;
                    result.SupersededPending = true;
                    result.SupersededRequestId = duplicatePending.RequestId;
                    result.Reason = "supersededPending:" + BuildRequestOwnerSummary(duplicatePending);
                    return result;
                }

                if (request.DuplicatePolicy == InputActionDuplicatePolicy.CoalescePending)
                {
                    result.Accepted = true;
                    result.Decision = InputActionAdmissionDecision.CoalescedPending;
                    result.CoalescedPending = true;
                    result.CoalescedRequestId = duplicatePending.RequestId;
                    result.CoalescedCount = GetRequestMetadataInt(duplicatePending, "AdmissionCoalescedCount") + 1;
                    result.Reason = "coalescedPending:" + BuildRequestOwnerSummary(duplicatePending);
                    return result;
                }

                result.Accepted = false;
                result.Decision = InputActionAdmissionDecision.DeniedDuplicatePendingOrRunning;
                result.DuplicatePendingOrRunning = true;
                result.Reason = "duplicatePending:" + BuildRequestOwnerSummary(duplicatePending);
                return result;
            }

            InputActionRequest preemptedPending;
            if (TryFindPreemptableBackgroundPendingLocked(request, profile, out preemptedPending))
            {
                // User commands may supersede conflicting background pending requests only
                // before they start; this branch must never preempt the active running action.
                result.Accepted = true;
                result.Decision = InputActionAdmissionDecision.SupersededPending;
                result.SupersededPending = true;
                result.SupersededRequestId = preemptedPending.RequestId;
                result.OwnerSummary = "pending:" + BuildRequestOwnerSummary(preemptedPending);
                result.Reason = "supersededBackgroundPending:" + BuildRequestOwnerSummary(preemptedPending);
                return result;
            }

            InputActionChannelDecision channelDecision;
            if (!_channelArbiter.CanAcquire(request, out channelDecision))
            {
                result.Accepted = false;
                result.Decision = IsBridgeBusyDecision(channelDecision)
                    ? InputActionAdmissionDecision.DeniedBridgeBusy
                    : InputActionAdmissionDecision.DeniedRunningChannelConflict;
                result.BlockingChannels = channelDecision == null ? InputActionChannel.None : channelDecision.BlockingChannels;
                result.RunningConflictSummary = channelDecision == null ? string.Empty : channelDecision.OwnerSummary;
                result.BridgeBusySummary = channelDecision == null ? string.Empty : channelDecision.BridgeBusySummary;
                result.Reason = channelDecision == null ? "channelBlocked:unknown" : channelDecision.Reason;
                return result;
            }

            result.RunningConflictSummary = channelDecision == null ? string.Empty : channelDecision.OwnerSummary;
            result.BridgeBusySummary = channelDecision == null ? string.Empty : channelDecision.BridgeBusySummary;
            return result;
        }

        private static bool IsBridgeBusyDecision(InputActionChannelDecision decision)
        {
            return decision != null &&
                   !string.IsNullOrWhiteSpace(decision.BridgeBusySummary) &&
                   (decision.BlockingChannels & (InputActionChannel.UseItem |
                                                InputActionChannel.BridgeItemUse |
                                                 InputActionChannel.BridgeUseItemPulse)) != 0;
        }

        private void ApplyAcceptedAdmissionLocked(InputActionRequest request, InputActionAdmissionResult admission, DateTime now)
        {
            if (request == null || admission == null || !admission.Accepted)
            {
                return;
            }

            if (admission.Decision == InputActionAdmissionDecision.SupersededPending)
            {
                var superseded = RemovePendingByIdLocked(admission.SupersededRequestId);
                if (superseded != null)
                {
                    _supersededPendingCount++;
                    _lastSchedulerSupersededRequest = BuildRequestOwnerSummary(superseded);
                    var message = "Pending action superseded by newer request. newRequestId=" + request.RequestId;
                    RecordResultLocked(InputActionResult.FromRequest(
                        superseded,
                        InputActionStatus.Cancelled,
                        message,
                        superseded.CreatedUtc));
                }

                _pending.Add(request);
                return;
            }

            if (admission.Decision == InputActionAdmissionDecision.CoalescedPending)
            {
                var pending = FindPendingByIdLocked(admission.CoalescedRequestId);
                if (pending == null)
                {
                    _pending.Add(request);
                    return;
                }

                _coalescedPendingCount++;
                MergePendingRequestLocked(pending, request, admission.CoalescedCount, now);
                return;
            }

            _pending.Add(request);
        }

        private InputActionRequest FindPendingByIdLocked(Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                return null;
            }

            for (var index = 0; index < _pending.Count; index++)
            {
                var pending = _pending[index];
                if (pending != null && pending.RequestId == requestId)
                {
                    return pending;
                }
            }

            return null;
        }

        private InputActionRequest RemovePendingByIdLocked(Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                return null;
            }

            for (var index = 0; index < _pending.Count; index++)
            {
                var pending = _pending[index];
                if (pending == null || pending.RequestId != requestId)
                {
                    continue;
                }

                _pending.RemoveAt(index);
                return pending;
            }

            return null;
        }

        private static void MergePendingRequestLocked(
            InputActionRequest pending,
            InputActionRequest incoming,
            int coalescedCount,
            DateTime now)
        {
            if (pending == null || incoming == null)
            {
                return;
            }

            var requestId = pending.RequestId;
            pending.Kind = incoming.Kind;
            pending.Priority = incoming.Priority;
            pending.DuplicatePolicy = incoming.DuplicatePolicy;
            pending.SourceFeatureId = incoming.SourceFeatureId ?? string.Empty;
            pending.Description = incoming.Description ?? string.Empty;
            pending.CreatedUtc = incoming.CreatedUtc;
            pending.QueueTimeout = incoming.QueueTimeout;
            pending.QueueExpiresUtc = incoming.QueueExpiresUtc;
            pending.AdmissionKey = incoming.AdmissionKey ?? string.Empty;
            pending.Timeout = incoming.Timeout;
            // IsExclusive is legacy metadata; channels and bridge ownership decide whether
            // a request conflicts with other work.
            pending.IsExclusive = incoming.IsExclusive;
            pending.RequiredChannels = incoming.RequiredChannels;
            pending.ConflictChannels = incoming.ConflictChannels;
            pending.Metadata = incoming.Metadata == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(incoming.Metadata, StringComparer.Ordinal);
            pending.RequestId = requestId;
            pending.Metadata["AdmissionCoalescedCount"] = coalescedCount.ToString(CultureInfo.InvariantCulture);
            pending.Metadata["AdmissionLastCoalescedIncomingRequestId"] = incoming.RequestId.ToString();
            pending.Metadata["AdmissionLastCoalescedUtc"] = now.ToString("O", CultureInfo.InvariantCulture);
        }

        private bool TryFindDuplicateRunningLocked(InputActionRequest request, out InputActionRequest duplicate)
        {
            duplicate = null;
            if (request == null)
            {
                return false;
            }

            if (_running != null &&
                _running.Request != null &&
                _running.Request.RequestId != request.RequestId &&
                IsDuplicateRequest(request, _running.Request))
            {
                duplicate = _running.Request;
                return true;
            }

            return false;
        }

        private bool TryFindDuplicatePendingLocked(InputActionRequest request, out InputActionRequest duplicate)
        {
            duplicate = null;
            if (request == null)
            {
                return false;
            }

            for (var index = 0; index < _pending.Count; index++)
            {
                var pending = _pending[index];
                if (pending == null || pending.RequestId == request.RequestId)
                {
                    continue;
                }

                if (IsDuplicateRequest(request, pending))
                {
                    duplicate = pending;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindPreemptableBackgroundPendingLocked(
            InputActionRequest request,
            InputActionChannelProfile requestProfile,
            out InputActionRequest preempted)
        {
            preempted = null;
            if (!InputActionScheduler.IsUserExplicitCommand(request) ||
                requestProfile == null ||
                _pending.Count == 0)
            {
                return false;
            }

            for (var index = 0; index < _pending.Count; index++)
            {
                var pending = _pending[index];
                if (pending == null ||
                    pending.RequestId == request.RequestId ||
                    !InputActionScheduler.IsBackgroundAutomation(pending))
                {
                    continue;
                }

                var pendingProfile = InputActionChannelResolver.Resolve(pending);
                if (FindPendingOverlap(requestProfile, pendingProfile) == InputActionChannel.None)
                {
                    continue;
                }

                preempted = pending;
                return true;
            }

            return false;
        }

        private static bool IsDuplicateRequest(InputActionRequest left, InputActionRequest right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(left.AdmissionKey) &&
                !string.IsNullOrWhiteSpace(right.AdmissionKey) &&
                string.Equals(left.AdmissionKey, right.AdmissionKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var leftSource = left.SourceFeatureId ?? string.Empty;
            var rightSource = right.SourceFeatureId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(leftSource) || string.IsNullOrWhiteSpace(rightSource))
            {
                return false;
            }

            return left.Kind == right.Kind &&
                   string.Equals(leftSource, rightSource, StringComparison.OrdinalIgnoreCase);
        }

        private string BuildPendingConflictSummaryLocked(InputActionRequest request, InputActionChannelProfile profile)
        {
            if (request == null || profile == null || _pending.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var index = 0; index < _pending.Count; index++)
            {
                var pending = _pending[index];
                if (pending == null || pending.RequestId == request.RequestId)
                {
                    continue;
                }

                var pendingProfile = InputActionChannelResolver.Resolve(pending);
                var overlap = FindPendingOverlap(profile, pendingProfile);
                if (overlap == InputActionChannel.None)
                {
                    continue;
                }

                parts.Add(BuildRequestOwnerSummary(pending) + ":" + InputActionChannelFormatter.Format(overlap));
            }

            return TrimSummary(string.Join("; ", parts.ToArray()), 512);
        }

        private static InputActionChannel FindPendingOverlap(InputActionChannelProfile requestProfile, InputActionChannelProfile pendingProfile)
        {
            if (requestProfile == null || pendingProfile == null)
            {
                return InputActionChannel.None;
            }

            var required = requestProfile.EffectiveRequiredChannels;
            var conflicts = requestProfile.ConflictChannels;
            var pendingRequired = pendingProfile.EffectiveRequiredChannels;
            var pendingConflicts = pendingProfile.ConflictChannels;
            if (required == InputActionChannel.None || pendingRequired == InputActionChannel.None)
            {
                return InputActionChannel.None;
            }

            if ((required & InputActionChannel.GlobalExclusive) != 0 ||
                (pendingRequired & InputActionChannel.GlobalExclusive) != 0)
            {
                return pendingRequired;
            }

            return (required & pendingRequired) |
                   (conflicts & pendingRequired) |
                   (required & pendingConflicts);
        }

        private bool TryBuildCleanupBlockedDecisionLocked(InputActionRequest request, out InputActionChannelDecision decision)
        {
            decision = null;
            if (!IsCleanupLeaseActiveLocked(DateTime.UtcNow) || request == null)
            {
                return false;
            }

            var profile = InputActionChannelResolver.Resolve(request);
            var blocking = FindCleanupBlockingChannels(profile);
            if (blocking == InputActionChannel.None)
            {
                return false;
            }

            decision = new InputActionChannelDecision
            {
                RequestId = request.RequestId,
                Kind = request.Kind,
                SourceFeatureId = profile.SourceFeatureId,
                Scenario = profile.Scenario,
                Allowed = false,
                RequiredChannels = profile.EffectiveRequiredChannels,
                ConflictChannels = profile.ConflictChannels,
                OccupiedChannels = _cleanupLease.Channels,
                BlockingChannels = blocking,
                OwnerSummary = _cleanupLease.OwnerSummary,
                BridgeBusySummary = string.Empty,
                Reason = "blockedByCleanupLease:" + _cleanupLease.OwnerSummary
            };
            return true;
        }

        private InputActionChannel FindCleanupBlockingChannels(InputActionChannelProfile profile)
        {
            if (profile == null || !IsCleanupLeaseActiveLocked(DateTime.UtcNow))
            {
                return InputActionChannel.None;
            }

            var required = profile.EffectiveRequiredChannels;
            var conflicts = profile.ConflictChannels;
            var cleanupChannels = _cleanupLease.Channels;
            if (required == InputActionChannel.None || cleanupChannels == InputActionChannel.None)
            {
                return InputActionChannel.None;
            }

            if ((required & InputActionChannel.GlobalExclusive) != 0 ||
                (cleanupChannels & InputActionChannel.GlobalExclusive) != 0)
            {
                return cleanupChannels;
            }

            return (required & cleanupChannels) |
                   (conflicts & cleanupChannels);
        }

        private bool IsCleanupLeaseBlockingChannelsLocked(InputActionChannel channels)
        {
            return channels != InputActionChannel.None &&
                   IsCleanupLeaseActiveLocked(DateTime.UtcNow) &&
                   (_cleanupLease.Channels & channels) != 0;
        }

        private void MaybeCreateCleanupLeaseLocked(InputActionResult result)
        {
            if (result == null ||
                _running == null ||
                _running.Request == null ||
                !ShouldCreateCleanupLease(result.Status))
            {
                return;
            }

            var profile = InputActionChannelResolver.Resolve(_running.Request);
            var channels = profile.EffectiveRequiredChannels;
            if (channels == InputActionChannel.None)
            {
                return;
            }

            _cleanupLease = new InputActionCleanupLease
            {
                RequestId = result.RequestId,
                Kind = result.Kind,
                SourceFeatureId = result.SourceFeatureId ?? string.Empty,
                Scenario = result.Scenario ?? string.Empty,
                Channels = channels,
                OwnerSummary = BuildRequestOwnerSummary(_running.Request),
                Reason = result.Status + ":" + (result.Message ?? string.Empty),
                ExpiresUtc = DateTime.UtcNow + DefaultCleanupLeaseDuration
            };
            _lastCleanupOwner = _cleanupLease.OwnerSummary;
            _lastCleanupReason = _cleanupLease.Reason;
        }

        private static bool ShouldCreateCleanupLease(InputActionStatus status)
        {
            return status == InputActionStatus.AttemptedButUnverified ||
                   status == InputActionStatus.Failed ||
                   status == InputActionStatus.TimedOut;
        }

        private void ExpireCleanupLeaseLocked(DateTime now)
        {
            if (_cleanupLease == null)
            {
                return;
            }

            if (now < _cleanupLease.ExpiresUtc)
            {
                return;
            }

            _lastCleanupOwner = _cleanupLease.OwnerSummary;
            _lastCleanupReason = "expired:" + _cleanupLease.Reason;
            _cleanupLease = null;
        }

        private bool IsCleanupLeaseActiveLocked(DateTime now)
        {
            return _cleanupLease != null && now < _cleanupLease.ExpiresUtc;
        }

        private void ExpirePendingLocked(DateTime now)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            for (var index = _pending.Count - 1; index >= 0; index--)
            {
                var request = _pending[index];
                if (!IsExpiredBeforeStart(request, now))
                {
                    continue;
                }

                _pending.RemoveAt(index);
                var waitedMs = (long)(now - request.CreatedUtc).TotalMilliseconds;
                var message = "Action expired before start after waiting in queue. waitedMs=" +
                              waitedMs.ToString(CultureInfo.InvariantCulture) +
                              ", admissionKey=" + (request.AdmissionKey ?? string.Empty);
                _expiredPendingCount++;
                _lastPendingExpiryReason = message;
                var result = InputActionResult.FromRequest(
                    request,
                    InputActionStatus.TimedOut,
                    message,
                    request.CreatedUtc);
                RecordResultLocked(result);
            }
        }

        private static bool IsExpiredBeforeStart(InputActionRequest request, DateTime now)
        {
            return request != null &&
                   request.QueueTimeout > TimeSpan.Zero &&
                   request.QueueExpiresUtc != default(DateTime) &&
                   now >= request.QueueExpiresUtc;
        }

        private string BuildPendingChannelSummaryLocked()
        {
            var channels = InputActionChannel.None;
            for (var index = 0; index < _pending.Count; index++)
            {
                var request = _pending[index];
                if (request == null)
                {
                    continue;
                }

                channels |= InputActionChannelResolver.Resolve(request).EffectiveRequiredChannels;
            }

            return InputActionChannelFormatter.Format(channels);
        }

        private string BuildPendingOwnerSummaryLocked()
        {
            if (_pending.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var index = 0; index < _pending.Count; index++)
            {
                var request = _pending[index];
                if (request == null)
                {
                    continue;
                }

                var profile = InputActionChannelResolver.Resolve(request);
                parts.Add(BuildRequestOwnerSummary(request) + ":" + InputActionChannelFormatter.Format(profile.EffectiveRequiredChannels));
            }

            return TrimSummary(string.Join("; ", parts.ToArray()), 512);
        }

        private InputActionResult UpdateRunningActionLocked(GameStateSnapshot snapshot)
        {
            if (_running == null || _running.Request == null)
            {
                return null;
            }

            var timeout = _running.Request.Timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : _running.Request.Timeout;
            if (DateTime.UtcNow - _running.StartedUtc > timeout)
            {
                return CancelRunningLocked(InputActionStatus.TimedOut, "Action timed out.");
            }

            var executor = GetExecutor(_running.Request.Kind);
            var step = executor.Update(_running, snapshot);
            _running.Status = step.Status;
            _running.Message = step.Message ?? string.Empty;
            _running.Error = step.Error;
            return step.IsTerminal
                ? InputActionResult.FromExecution(_running, step.Status, step.Message, step.Error)
                : null;
        }

        private InputActionResult CancelRunningLocked(InputActionStatus finalStatus, string reason)
        {
            if (_running == null)
            {
                return null;
            }

            var running = _running;
            var message = reason ?? "Action cancelled.";
            Exception error = null;
            try
            {
                var step = GetExecutor(running.Request.Kind).Cancel(running, reason);
                if (step != null)
                {
                    if (!string.IsNullOrWhiteSpace(step.Message))
                    {
                        message = step.Message;
                    }

                    error = step.Error;
                    running.Message = message;
                    running.Error = error;
                }
                else
                {
                    running.Message = message;
                }
            }
            catch (Exception cancelError)
            {
                error = cancelError;
                message = (string.IsNullOrWhiteSpace(reason) ? "Action cancelled." : reason) +
                          " Executor cancel cleanup failed: " + cancelError.Message;
                running.Message = message;
                running.Error = cancelError;
                Logger.Error(
                    "InputActionQueue",
                    "Input action cancel cleanup failed; completing action as " + finalStatus + ".",
                    cancelError);
            }

            return InputActionResult.FromExecution(running, finalStatus, message, error);
        }

        private void CompleteLocked(InputActionResult result)
        {
            if (result == null)
            {
                return;
            }

            if (_running != null)
            {
                _running.Status = result.Status;
                _running.FinishedUtc = result.FinishedUtc;
                _running.Message = result.Message;
                _running.Error = result.Error;
            }

            if (_runningChannelLease != null)
            {
                _channelArbiter.Release(_runningChannelLease, result.Status.ToString());
                _runningChannelLease = null;
            }

            // Failed or unverified terminal states keep a short channel lease before
            // recording the receipt, so resource recovery is visible to later admission.
            MaybeCreateCleanupLeaseLocked(result);
            RecordResultLocked(result);
            _running = null;
        }

        private void RecordResultLocked(InputActionResult result)
        {
            if (result == null)
            {
                return;
            }

            _lastResult = result;
            _recentResults.Add(result);
            // Recent terminal results are transaction receipts for callers such as
            // AutoStack; keep this as state, not just as diagnostic logging.
            while (_recentResults.Count > MaxRecentResults)
            {
                _recentResults.RemoveAt(0);
            }

            Logger.Info(
                "InputActionQueue",
                "Input action finished: " + result.Kind + " / " + result.Status + " / " + result.Message);
            DiagnosticActionRecorder.RecordQueueResult(result);
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

        private static int GetRequestMetadataInt(InputActionRequest request, string key)
        {
            var value = GetRequestMetadata(request, key);
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static int CountKnownChannels(InputActionChannel channels)
        {
            var count = 0;
            var value = (int)(channels & InputActionChannelFormatter.AllKnown);
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            var unknown = channels & ~InputActionChannelFormatter.AllKnown;
            return unknown == InputActionChannel.None ? count : count + 1;
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
            return executors;
        }

        private static void Register(Dictionary<InputActionKind, IInputActionExecutor> executors, IInputActionExecutor executor)
        {
            executors[executor.Kind] = executor;
        }

        private sealed class InputActionCleanupLease
        {
            public Guid RequestId { get; set; }
            public InputActionKind Kind { get; set; }
            public string SourceFeatureId { get; set; }
            public string Scenario { get; set; }
            public InputActionChannel Channels { get; set; }
            public string OwnerSummary { get; set; }
            public string Reason { get; set; }
            public DateTime ExpiresUtc { get; set; }

            public InputActionCleanupLease()
            {
                SourceFeatureId = string.Empty;
                Scenario = string.Empty;
                OwnerSummary = string.Empty;
                Reason = string.Empty;
            }
        }

        private string GetRecentResultLineFromNewest(int newestIndex)
        {
            var index = _recentResults.Count - 1 - newestIndex;
            if (index < 0 || index >= _recentResults.Count)
            {
                return string.Empty;
            }

            var result = _recentResults[index];
            var time = result.FinishedUtc == default(DateTime)
                ? string.Empty
                : result.FinishedUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " ";
            return "[" + time.Trim() + "] " + result.Kind + " " + result.Status + ": " + (result.Message ?? string.Empty);
        }
    }

    public sealed class InputActionQueueFastState
    {
        public static readonly InputActionQueueFastState Empty = new InputActionQueueFastState();

        public int PendingCount { get; set; }
        public long UpdateCount { get; set; }
        public bool HasRunningAction { get; set; }
        public InputActionKind RunningActionKindValue { get; set; }
        public long LastInputActionUpdateMs { get; set; }
        public InputActionResult LastResult { get; set; }
        public int ChannelLeaseCount { get; set; }
        public bool HasChannelLease { get; set; }
        public InputActionChannel OccupiedChannelsValue { get; set; }
        public InputActionChannel RunningLeaseChannelsValue { get; set; }
        public InputActionChannel BridgeBusyChannelsValue { get; set; }
        public InputActionChannel CleanupLeaseChannelsValue { get; set; }
        public bool IsBridgeBusy { get; set; }
        public int OccupiedChannelCount { get; set; }
        public int RunningLeaseChannelCount { get; set; }
        public int BridgeBusyChannelCount { get; set; }
        public string RunningActionSource { get; set; }
        public string CleanupLeaseOwner { get; set; }

        public string RunningActionKind
        {
            get { return HasRunningAction ? RunningActionKindValue.ToString() : string.Empty; }
        }

        public string OccupiedChannels
        {
            get { return InputActionChannelFormatter.Format(OccupiedChannelsValue); }
        }

        public string BridgeBusyChannels
        {
            get { return InputActionChannelFormatter.Format(BridgeBusyChannelsValue); }
        }

        public string CleanupLeaseChannels
        {
            get { return InputActionChannelFormatter.Format(CleanupLeaseChannelsValue); }
        }

        public string LastActionKind
        {
            get { return LastResult == null ? string.Empty : LastResult.Kind.ToString(); }
        }

        public string LastActionResultCode
        {
            get { return LastResult == null ? string.Empty : LastResult.ResultCode ?? string.Empty; }
        }

        public string LastActionResult
        {
            get
            {
                if (LastResult == null)
                {
                    return string.Empty;
                }

                return LastResult.Status + ": " + LastResult.Message;
            }
        }

        public InputActionQueueFastState()
        {
            RunningActionKindValue = InputActionKind.None;
            OccupiedChannelsValue = InputActionChannel.None;
            RunningLeaseChannelsValue = InputActionChannel.None;
            BridgeBusyChannelsValue = InputActionChannel.None;
            CleanupLeaseChannelsValue = InputActionChannel.None;
            CleanupLeaseOwner = string.Empty;
        }
    }

    public sealed class InputActionQueueSnapshot
    {
        public static readonly InputActionQueueSnapshot Empty = new InputActionQueueSnapshot();

        public int PendingCount { get; set; }
        public long UpdateCount { get; set; }
        public string RunningAction { get; set; }
        public string RunningActionKind { get; set; }
        public string RunningActionSource { get; set; }
        public string RunningActionStatus { get; set; }
        public InputActionResult LastResult { get; set; }
        public string LastActionKind { get { return LastResult == null ? string.Empty : LastResult.Kind.ToString(); } }
        public string LastActionResultCode { get { return LastResult == null ? string.Empty : LastResult.ResultCode ?? string.Empty; } }
        public long LastActionDurationMs { get { return LastResult == null ? 0 : LastResult.DurationMs; } }
        public int RecentResultsCount { get; set; }
        public long LastInputActionUpdateMs { get; set; }
        public string RecentActionLine1 { get; set; }
        public string RecentActionLine2 { get; set; }
        public string RecentActionLine3 { get; set; }
        public int ActionQueueChannelLeaseCount { get; set; }
        public string ActionQueueRunningChannels { get; set; }
        public string ActionQueueOccupiedChannels { get; set; }
        public string ActionQueueBridgeBusyChannels { get; set; }
        public string ActionQueueRunningLeaseChannels { get; set; }
        public int ActionQueueBlockedPendingCount { get; set; }
        public string ActionQueueLastChannelDecision { get; set; }
        public string ActionQueueLastChannelBlockedReason { get; set; }
        public string ActionQueueChannelOwnerSummary { get; set; }
        public string ActionQueueBridgeBusySummary { get; set; }
        public string ActionQueuePendingChannelSummary { get; set; }
        public string ActionQueuePendingOwnerSummary { get; set; }
        public string ActionQueueLastAdmissionStatus { get; set; }
        public string ActionQueueLastAdmissionDecision { get; set; }
        public string ActionQueueLastAdmissionReason { get; set; }
        public string ActionQueueLastAdmissionKind { get; set; }
        public string ActionQueueLastAdmissionSource { get; set; }
        public string ActionQueueLastAdmissionScenario { get; set; }
        public string ActionQueueLastAdmissionKey { get; set; }
        public string ActionQueueLastAdmissionRequiredChannels { get; set; }
        public string ActionQueueLastAdmissionBlockingChannels { get; set; }
        public string ActionQueueLastAdmissionConflictChannels { get; set; }
        public string ActionQueueLastAdmissionPendingConflictSummary { get; set; }
        public string ActionQueueLastAdmissionRunningConflictSummary { get; set; }
        public string ActionQueueLastAdmissionBridgeBusySummary { get; set; }
        public string ActionQueueLastAdmissionOwnerSummary { get; set; }
        public string ActionQueueLastAdmissionSupersededRequestId { get; set; }
        public string ActionQueueLastAdmissionCoalescedRequestId { get; set; }
        public int ActionQueueSupersededPendingCount { get; set; }
        public int ActionQueueCoalescedPendingCount { get; set; }
        public string SchedulerLastSelectedRequest { get; set; }
        public string SchedulerLastSupersededRequest { get; set; }
        public string SchedulerLastFairnessBucket { get; set; }
        public int BackgroundRequestCoalescedCount { get; set; }
        public int ExpiredPendingDroppedCount { get; set; }
        public int ActionQueueCleanupLeaseCount { get; set; }
        public string ActionQueueCleanupLeaseChannels { get; set; }
        public string ActionQueueLastCleanupOwner { get; set; }
        public string ActionQueueLastCleanupReason { get; set; }
        public int ActionQueueDirectEnqueueCount { get; set; }
        public string ActionQueueLastDirectEnqueueKind { get; set; }
        public string ActionQueueLastDirectEnqueueSource { get; set; }
        public string ActionQueueLastDirectEnqueueScenario { get; set; }
        public string ActionQueueLastDirectEnqueueAdmissionKey { get; set; }
        public string ActionQueueLastDirectEnqueueRequiredChannels { get; set; }
        public int ActionQueueExpiredPendingCount { get; set; }
        public string ActionQueueLastPendingExpiryReason { get; set; }

        public string LastActionResult
        {
            get
            {
                if (LastResult == null)
                {
                    return string.Empty;
                }

                return LastResult.Status + ": " + LastResult.Message;
            }
        }

        public string LastActionStatus
        {
            get { return LastResult == null ? "none" : LastResult.Status.ToString(); }
        }

        public string LastActionMessage
        {
            get { return LastResult == null ? string.Empty : LastResult.Message ?? string.Empty; }
        }

        public InputActionQueueSnapshot()
        {
            RunningAction = string.Empty;
            RunningActionKind = string.Empty;
            RunningActionSource = string.Empty;
            RunningActionStatus = string.Empty;
            RecentActionLine1 = string.Empty;
            RecentActionLine2 = string.Empty;
            RecentActionLine3 = string.Empty;
            ActionQueueRunningChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueOccupiedChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueBridgeBusyChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueRunningLeaseChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastChannelDecision = string.Empty;
            ActionQueueLastChannelBlockedReason = string.Empty;
            ActionQueueChannelOwnerSummary = string.Empty;
            ActionQueueBridgeBusySummary = string.Empty;
            ActionQueuePendingChannelSummary = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueuePendingOwnerSummary = string.Empty;
            ActionQueueLastAdmissionStatus = string.Empty;
            ActionQueueLastAdmissionDecision = string.Empty;
            ActionQueueLastAdmissionReason = string.Empty;
            ActionQueueLastAdmissionKind = string.Empty;
            ActionQueueLastAdmissionSource = string.Empty;
            ActionQueueLastAdmissionScenario = string.Empty;
            ActionQueueLastAdmissionKey = string.Empty;
            ActionQueueLastAdmissionRequiredChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastAdmissionBlockingChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastAdmissionConflictChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastAdmissionPendingConflictSummary = string.Empty;
            ActionQueueLastAdmissionRunningConflictSummary = string.Empty;
            ActionQueueLastAdmissionBridgeBusySummary = string.Empty;
            ActionQueueLastAdmissionOwnerSummary = string.Empty;
            ActionQueueLastAdmissionSupersededRequestId = string.Empty;
            ActionQueueLastAdmissionCoalescedRequestId = string.Empty;
            SchedulerLastSelectedRequest = string.Empty;
            SchedulerLastSupersededRequest = string.Empty;
            SchedulerLastFairnessBucket = string.Empty;
            ActionQueueCleanupLeaseChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastCleanupOwner = string.Empty;
            ActionQueueLastCleanupReason = string.Empty;
            ActionQueueLastDirectEnqueueKind = string.Empty;
            ActionQueueLastDirectEnqueueSource = string.Empty;
            ActionQueueLastDirectEnqueueScenario = string.Empty;
            ActionQueueLastDirectEnqueueAdmissionKey = string.Empty;
            ActionQueueLastDirectEnqueueRequiredChannels = string.Empty;
            ActionQueueLastPendingExpiryReason = string.Empty;
        }
    }
}
