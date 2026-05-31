using System;

namespace JueMingZ.Actions.Channels
{
    public sealed class InputActionChannelLease
    {
        public static readonly InputActionChannelLease None = new InputActionChannelLease
        {
            IsAcquired = false,
            RequiredChannels = InputActionChannel.None,
            ConflictChannels = InputActionChannel.None,
            SourceFeatureId = string.Empty,
            Scenario = string.Empty,
            OwnerSummary = "None"
        };

        public Guid LeaseId { get; set; }
        public Guid RequestId { get; set; }
        public InputActionKind Kind { get; set; }
        public string SourceFeatureId { get; set; }
        public string Scenario { get; set; }
        public InputActionChannel RequiredChannels { get; set; }
        public InputActionChannel ConflictChannels { get; set; }
        public DateTime AcquiredUtc { get; set; }
        public bool IsAcquired { get; set; }
        public string OwnerSummary { get; set; }

        public InputActionChannelLease()
        {
            LeaseId = Guid.NewGuid();
            SourceFeatureId = string.Empty;
            Scenario = string.Empty;
            OwnerSummary = string.Empty;
        }
    }
}
