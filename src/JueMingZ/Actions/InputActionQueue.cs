using System;
using System.Collections.Generic;
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
        private const int MaxRecentResults = 12;
        private readonly object _syncRoot = new object();
        private readonly List<InputActionRequest> _pending = new List<InputActionRequest>();
        private readonly List<InputActionResult> _recentResults = new List<InputActionResult>();
        private readonly Dictionary<InputActionKind, IInputActionExecutor> _executors;
        private readonly InputActionChannelArbiter _channelArbiter = new InputActionChannelArbiter();
        private InputActionExecution _running;
        private InputActionChannelLease _runningChannelLease;
        private InputActionChannelDecision _lastChannelDecision;
        private InputActionAdmissionResult _lastAdmissionResult;
        private int _blockedPendingCount;
        private int _expiredPendingCount;
        private string _lastPendingExpiryReason = string.Empty;
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
                _pending.Add(request);
            }

            Logger.Debug("InputActionQueue", "Input action enqueued: " + request.Kind + " / " + request.Description);
            return request.RequestId;
        }

        public bool TryEnqueue(InputActionRequest request, out InputActionAdmissionResult admission)
        {
            NormalizeRequest(request);

            lock (_syncRoot)
            {
                ExpirePendingLocked(DateTime.UtcNow);
                admission = BuildAdmissionLocked(request, DateTime.UtcNow);
                _lastAdmissionResult = admission;
                if (admission == null || !admission.Accepted)
                {
                    Logger.Debug(
                        "InputActionQueue",
                        "Input action admission denied: " +
                        (admission == null ? "unknown" : admission.Summary));
                    return false;
                }

                _pending.Add(request);
            }

            Logger.Debug("InputActionQueue", "Input action admitted and enqueued: " + request.Kind + " / " + request.Description);
            return true;
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
                cancelled += _pending.RemoveAll(request =>
                    string.Equals(request.SourceFeatureId ?? string.Empty, safeSource, StringComparison.OrdinalIgnoreCase));

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
            }
        }

        public bool CanSubmit(InputActionRequest request, GameStateSnapshot snapshot, out InputActionChannelDecision decision)
        {
            NormalizeRequest(request);
            lock (_syncRoot)
            {
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
                    ActionQueueLastAdmissionReason = _lastAdmissionResult == null ? string.Empty : _lastAdmissionResult.Reason,
                    ActionQueueExpiredPendingCount = _expiredPendingCount,
                    ActionQueueLastPendingExpiryReason = _lastPendingExpiryReason
                };
            }
        }

        public InputActionQueueFastState GetFastState()
        {
            lock (_syncRoot)
            {
                var channelState = _channelArbiter.GetFastState();
                return new InputActionQueueFastState
                {
                    PendingCount = _pending.Count,
                    UpdateCount = _updateCount,
                    HasRunningAction = _running != null,
                    RunningActionKindValue = _running == null ? InputActionKind.None : _running.Request.Kind,
                    RunningActionSource = _running == null ? string.Empty : _running.Request.SourceFeatureId ?? string.Empty,
                    LastInputActionUpdateMs = _lastInputActionUpdateMs,
                    LastResult = _lastResult,
                    ChannelLeaseCount = channelState.LeaseCount,
                    HasChannelLease = channelState.HasLease,
                    OccupiedChannelsValue = channelState.OccupiedChannels,
                    RunningLeaseChannelsValue = channelState.RunningLeaseChannels,
                    BridgeBusyChannelsValue = channelState.BridgeBusyChannels,
                    IsBridgeBusy = channelState.IsBridgeBusy,
                    OccupiedChannelCount = channelState.OccupiedChannelCount,
                    RunningLeaseChannelCount = channelState.RunningLeaseChannelCount,
                    BridgeBusyChannelCount = channelState.BridgeBusyChannelCount
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
            if (_running != null)
            {
                return null;
            }

            ExpirePendingLocked(DateTime.UtcNow);
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

            string duplicateSummary;
            if (TryFindDuplicatePendingOrRunningLocked(request, out duplicateSummary))
            {
                result.Accepted = false;
                result.Decision = InputActionAdmissionDecision.DeniedDuplicatePendingOrRunning;
                result.DuplicatePendingOrRunning = true;
                result.Reason = "duplicatePendingOrRunning:" + duplicateSummary;
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

        private bool TryFindDuplicatePendingOrRunningLocked(InputActionRequest request, out string summary)
        {
            summary = string.Empty;
            if (request == null)
            {
                return false;
            }

            if (_running != null &&
                _running.Request != null &&
                _running.Request.RequestId != request.RequestId &&
                IsDuplicateRequest(request, _running.Request))
            {
                summary = "running:" + BuildRequestOwnerSummary(_running.Request);
                return true;
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
                    summary = "pending:" + BuildRequestOwnerSummary(pending);
                    return true;
                }
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
        public bool IsBridgeBusy { get; set; }
        public int OccupiedChannelCount { get; set; }
        public int RunningLeaseChannelCount { get; set; }
        public int BridgeBusyChannelCount { get; set; }
        public string RunningActionSource { get; set; }

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
        public string ActionQueueLastAdmissionReason { get; set; }
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
            ActionQueueLastAdmissionReason = string.Empty;
            ActionQueueLastPendingExpiryReason = string.Empty;
        }
    }
}
