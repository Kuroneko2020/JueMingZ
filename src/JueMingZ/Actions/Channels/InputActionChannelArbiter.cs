using System;
using System.Collections.Generic;
using System.Text;

namespace JueMingZ.Actions.Channels
{
    public sealed class InputActionChannelArbiter
    {
        // Channel ownership gates admission and start boundaries; the queue still owns
        // the single running action contract.
        private readonly Dictionary<Guid, InputActionChannelLease> _leases = new Dictionary<Guid, InputActionChannelLease>();

        public bool CanAcquire(InputActionRequest request, out InputActionChannelDecision decision)
        {
            var profile = InputActionChannelResolver.Resolve(request);
            return CanAcquire(request, profile, out decision);
        }

        public bool TryAcquire(InputActionRequest request, out InputActionChannelLease lease, out InputActionChannelDecision decision)
        {
            lease = InputActionChannelLease.None;
            var profile = InputActionChannelResolver.Resolve(request);
            if (!CanAcquire(request, profile, out decision))
            {
                return false;
            }

            var required = profile.EffectiveRequiredChannels;
            if (required == InputActionChannel.None)
            {
                return true;
            }

            lease = new InputActionChannelLease
            {
                RequestId = request == null ? Guid.Empty : request.RequestId,
                Kind = request == null ? InputActionKind.None : request.Kind,
                SourceFeatureId = profile.SourceFeatureId,
                Scenario = profile.Scenario,
                RequiredChannels = required,
                ConflictChannels = profile.ConflictChannels,
                AcquiredUtc = DateTime.UtcNow,
                IsAcquired = true,
                OwnerSummary = BuildLeaseOwnerSummary(request, profile)
            };
            _leases[lease.LeaseId] = lease;
            return true;
        }

        public void Release(InputActionChannelLease lease, string reason)
        {
            if (lease == null || !lease.IsAcquired)
            {
                return;
            }

            _leases.Remove(lease.LeaseId);
            lease.IsAcquired = false;
        }

        public void ReleaseAll(string reason)
        {
            foreach (var lease in _leases.Values)
            {
                if (lease != null)
                {
                    lease.IsAcquired = false;
                }
            }

            _leases.Clear();
        }

        public bool IsAnyChannelBusy(InputActionChannel channels)
        {
            if (channels == InputActionChannel.None)
            {
                return false;
            }

            return (GetOccupiedChannels() & channels) != 0;
        }

        public InputActionChannelSnapshot GetSnapshot()
        {
            var leaseChannels = GetLeaseChannels();
            var bridgeBusy = GetBridgeBusyChannels();
            var occupied = leaseChannels | bridgeBusy;
            return new InputActionChannelSnapshot
            {
                LeaseCount = _leases.Count,
                OccupiedChannels = occupied,
                OccupiedChannelNames = InputActionChannelFormatter.Format(occupied),
                RunningLeaseChannels = leaseChannels,
                RunningLeaseChannelNames = InputActionChannelFormatter.Format(leaseChannels),
                OwnerSummary = BuildOwnerSummary(),
                BridgeBusySummary = BuildBridgeBusySummary(),
                BridgeBusyChannels = bridgeBusy,
                BridgeBusyChannelNames = InputActionChannelFormatter.Format(bridgeBusy)
            };
        }

        public InputActionChannelFastState GetFastState()
        {
            var leaseChannels = GetLeaseChannels();
            var bridgeBusy = GetBridgeBusyChannels();
            var occupied = leaseChannels | bridgeBusy;
            return new InputActionChannelFastState
            {
                LeaseCount = _leases.Count,
                HasLease = _leases.Count > 0,
                OccupiedChannels = occupied,
                RunningLeaseChannels = leaseChannels,
                BridgeBusyChannels = bridgeBusy,
                IsBridgeBusy = bridgeBusy != InputActionChannel.None,
                OccupiedChannelCount = CountKnownChannels(occupied),
                RunningLeaseChannelCount = CountKnownChannels(leaseChannels),
                BridgeBusyChannelCount = CountKnownChannels(bridgeBusy)
            };
        }

        private bool CanAcquire(InputActionRequest request, InputActionChannelProfile profile, out InputActionChannelDecision decision)
        {
            var required = profile.EffectiveRequiredChannels;
            var conflicts = profile.ConflictChannels;
            var occupied = GetOccupiedChannels();
            var blocking = FindBlockingChannels(required, conflicts, profile.AllowStartWhenBridgeBusy);
            var allowed = blocking == InputActionChannel.None;
            decision = new InputActionChannelDecision
            {
                RequestId = request == null ? Guid.Empty : request.RequestId,
                Kind = request == null ? InputActionKind.None : request.Kind,
                SourceFeatureId = profile.SourceFeatureId,
                Scenario = profile.Scenario,
                Allowed = allowed,
                RequiredChannels = required,
                ConflictChannels = conflicts,
                OccupiedChannels = occupied,
                BlockingChannels = blocking,
                OwnerSummary = BuildOwnerSummary(),
                BridgeBusySummary = BuildBridgeBusySummary(),
                Reason = allowed
                    ? "available"
                    : "blockedBy:" + InputActionChannelFormatter.Format(blocking)
            };
            return allowed;
        }

        private InputActionChannel FindBlockingChannels(InputActionChannel required, InputActionChannel conflicts, bool allowBridgeBusy)
        {
            if (required == InputActionChannel.None)
            {
                return InputActionChannel.None;
            }

            var blocking = InputActionChannel.None;
            foreach (var lease in _leases.Values)
            {
                if (lease == null || !lease.IsAcquired)
                {
                    continue;
                }

                if ((required & InputActionChannel.GlobalExclusive) != 0 ||
                    (lease.RequiredChannels & InputActionChannel.GlobalExclusive) != 0)
                {
                    blocking |= lease.RequiredChannels;
                    continue;
                }

                if ((required & lease.RequiredChannels) != 0)
                {
                    blocking |= required & lease.RequiredChannels;
                }

                if ((conflicts & lease.RequiredChannels) != 0)
                {
                    blocking |= conflicts & lease.RequiredChannels;
                }

                if ((required & lease.ConflictChannels) != 0)
                {
                    blocking |= required & lease.ConflictChannels;
                }
            }

            if (!allowBridgeBusy)
            {
                var bridgeBusy = GetBridgeBusyChannels();
                if (bridgeBusy != InputActionChannel.None)
                {
                    var bridgeConflicts = InputActionChannelResolver.ResolveDefaultConflictChannels(bridgeBusy);
                    if ((required & InputActionChannel.GlobalExclusive) != 0)
                    {
                        blocking |= bridgeBusy;
                    }

                    if ((required & bridgeBusy) != 0)
                    {
                        blocking |= required & bridgeBusy;
                    }

                    if ((conflicts & bridgeBusy) != 0)
                    {
                        blocking |= conflicts & bridgeBusy;
                    }

                    if ((required & bridgeConflicts) != 0)
                    {
                        blocking |= required & bridgeConflicts;
                    }
                }
            }

            return blocking;
        }

        private InputActionChannel GetOccupiedChannels()
        {
            return GetLeaseChannels() | GetBridgeBusyChannels();
        }

        private InputActionChannel GetLeaseChannels()
        {
            var channels = InputActionChannel.None;
            foreach (var lease in _leases.Values)
            {
                if (lease != null && lease.IsAcquired)
                {
                    channels |= lease.RequiredChannels;
                }
            }

            return channels;
        }

        private static InputActionChannel GetBridgeBusyChannels()
        {
            var channels = InputActionChannel.None;
            // Bridge request ids are live owners even without a running channel lease.
            // They must block competing item-use writers until the bridge releases them.
            if (ItemUseBridge.PendingRequestId != Guid.Empty)
            {
                channels |= InputActionChannel.UseItem | InputActionChannel.BridgeItemUse;
            }

            if (UseItemPulseBridge.HasActivePulse)
            {
                channels |= InputActionChannel.UseItem | InputActionChannel.BridgeUseItemPulse;
            }

            return channels;
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

        private string BuildOwnerSummary()
        {
            if (_leases.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var lease in _leases.Values)
            {
                if (lease == null || !lease.IsAcquired)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(lease.OwnerSummary);
            }

            return builder.ToString();
        }

        private static string BuildBridgeBusySummary()
        {
            var builder = new StringBuilder();
            var itemUseRequestId = ItemUseBridge.PendingRequestId;
            if (itemUseRequestId != Guid.Empty)
            {
                builder.Append("ItemUseBridge:").Append(itemUseRequestId);
            }

            var pulseRequestId = UseItemPulseBridge.ActiveRequestId;
            if (pulseRequestId != Guid.Empty)
            {
                if (builder.Length > 0)
                {
                    builder.Append("; ");
                }

                builder.Append("UseItemPulseBridge:").Append(pulseRequestId);
            }

            return builder.ToString();
        }

        private static string BuildLeaseOwnerSummary(InputActionRequest request, InputActionChannelProfile profile)
        {
            var kind = request == null ? InputActionKind.None : request.Kind;
            var source = string.IsNullOrWhiteSpace(profile.SourceFeatureId) ? "unknown" : profile.SourceFeatureId;
            return kind + ":" + source + ":" + InputActionChannelFormatter.Format(profile.EffectiveRequiredChannels);
        }
    }
}
