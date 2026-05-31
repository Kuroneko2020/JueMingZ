namespace JueMingZ.Actions.Channels
{
    public sealed class InputActionChannelProfile
    {
        public InputActionChannel RequiredChannels { get; set; }
        public InputActionChannel ConflictChannels { get; set; }
        public bool GlobalExclusive { get; set; }
        public bool AllowStartWhenBridgeBusy { get; set; }
        public bool AllowWhileUiMouseCaptured { get; set; }
        public bool AllowWhileInventoryOpen { get; set; }
        public string SourceFeatureId { get; set; }
        public string Scenario { get; set; }
        public string Reason { get; set; }

        public InputActionChannelProfile()
        {
            RequiredChannels = InputActionChannel.None;
            ConflictChannels = InputActionChannel.None;
            SourceFeatureId = string.Empty;
            Scenario = string.Empty;
            Reason = string.Empty;
        }

        public InputActionChannel EffectiveRequiredChannels
        {
            get
            {
                return GlobalExclusive
                    ? RequiredChannels | InputActionChannel.GlobalExclusive
                    : RequiredChannels;
            }
        }
    }
}
