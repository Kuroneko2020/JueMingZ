using System;
using System.Collections.Generic;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Actions
{
    public sealed partial class InputActionQueue
    {
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
                    LastResult = _resultStore.LastResult,
                    RecentResultsCount = _resultStore.Count,
                    LastInputActionUpdateMs = _lastInputActionUpdateMs,
                    RecentActionLine1 = _resultStore.GetRecentResultLineFromNewest(0),
                    RecentActionLine2 = _resultStore.GetRecentResultLineFromNewest(1),
                    RecentActionLine3 = _resultStore.GetRecentResultLineFromNewest(2),
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
                    LastResult = _resultStore.LastResult,
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
    }
}
