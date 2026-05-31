namespace JueMingZ.Actions.Channels
{
    public sealed class InputActionChannelSnapshot
    {
        public static readonly InputActionChannelSnapshot Empty = new InputActionChannelSnapshot();

        public int LeaseCount { get; set; }
        public InputActionChannel OccupiedChannels { get; set; }
        public string OccupiedChannelNames { get; set; }
        public InputActionChannel RunningLeaseChannels { get; set; }
        public string RunningLeaseChannelNames { get; set; }
        public string OwnerSummary { get; set; }
        public string BridgeBusySummary { get; set; }
        public InputActionChannel BridgeBusyChannels { get; set; }
        public string BridgeBusyChannelNames { get; set; }

        public InputActionChannelSnapshot()
        {
            OccupiedChannelNames = InputActionChannelFormatter.Format(InputActionChannel.None);
            RunningLeaseChannelNames = InputActionChannelFormatter.Format(InputActionChannel.None);
            OwnerSummary = string.Empty;
            BridgeBusySummary = string.Empty;
            BridgeBusyChannelNames = InputActionChannelFormatter.Format(InputActionChannel.None);
        }
    }

    public sealed class InputActionChannelFastState
    {
        public static readonly InputActionChannelFastState Empty = new InputActionChannelFastState();

        public int LeaseCount { get; set; }
        public bool HasLease { get; set; }
        public InputActionChannel OccupiedChannels { get; set; }
        public InputActionChannel RunningLeaseChannels { get; set; }
        public InputActionChannel BridgeBusyChannels { get; set; }
        public bool IsBridgeBusy { get; set; }
        public int OccupiedChannelCount { get; set; }
        public int RunningLeaseChannelCount { get; set; }
        public int BridgeBusyChannelCount { get; set; }
    }
}
