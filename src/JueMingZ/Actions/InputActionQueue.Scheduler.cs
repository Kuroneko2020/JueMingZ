using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Actions
{
    public sealed partial class InputActionQueue
    {
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
    }
}
