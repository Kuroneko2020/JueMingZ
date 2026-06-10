using System;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Actions
{
    public sealed partial class InputActionQueue
    {
        private static readonly TimeSpan DefaultCleanupLeaseDuration = TimeSpan.FromMilliseconds(250);

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
    }
}
