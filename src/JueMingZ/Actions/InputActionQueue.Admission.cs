using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Actions
{
    public sealed partial class InputActionQueue
    {
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

        private static int GetRequestMetadataInt(InputActionRequest request, string key)
        {
            var value = GetRequestMetadata(request, key);
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }
    }
}
