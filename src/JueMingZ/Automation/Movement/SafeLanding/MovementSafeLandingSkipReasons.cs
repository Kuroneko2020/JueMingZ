namespace JueMingZ.Automation.Movement
{
    internal static class MovementSafeLandingSkipReasons
    {
        public const string ConfigDisabled = "configDisabled";
        public const string CapabilityUnavailable = "capabilityUnavailable";
        public const string TimingNotReady = "timingNotReady";
        public const string NotImplemented = "notImplemented";
        public const string PlanUnavailable = "planUnavailable";
        public const string PlayerNotControllable = "playerNotControllable";
        public const string TextInputFocused = "textInputFocused";
        public const string QueueBusy = "queueBusy";
        public const string Cooldown = "cooldown";
        public const string AlreadySafe = "alreadySafe";
    }
}
