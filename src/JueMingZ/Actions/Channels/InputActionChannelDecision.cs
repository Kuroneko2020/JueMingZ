using System;

namespace JueMingZ.Actions.Channels
{
    public sealed class InputActionChannelDecision
    {
        public Guid RequestId { get; set; }
        public InputActionKind Kind { get; set; }
        public string SourceFeatureId { get; set; }
        public string Scenario { get; set; }
        public bool Allowed { get; set; }
        public InputActionChannel RequiredChannels { get; set; }
        public InputActionChannel ConflictChannels { get; set; }
        public InputActionChannel OccupiedChannels { get; set; }
        public InputActionChannel BlockingChannels { get; set; }
        public string OwnerSummary { get; set; }
        public string BridgeBusySummary { get; set; }
        public string Reason { get; set; }

        public InputActionChannelDecision()
        {
            SourceFeatureId = string.Empty;
            Scenario = string.Empty;
            OwnerSummary = string.Empty;
            BridgeBusySummary = string.Empty;
            Reason = string.Empty;
        }

        public string Summary
        {
            get
            {
                return (Allowed ? "allowed" : "blocked") +
                       " required=" + InputActionChannelFormatter.Format(RequiredChannels) +
                       " blocking=" + InputActionChannelFormatter.Format(BlockingChannels) +
                       (string.IsNullOrWhiteSpace(Reason) ? string.Empty : " reason=" + Reason);
            }
        }
    }
}
