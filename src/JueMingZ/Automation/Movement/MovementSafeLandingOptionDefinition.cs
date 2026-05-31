namespace JueMingZ.Automation.Movement
{
    public sealed class MovementSafeLandingOptionDefinition
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Tooltip { get; set; }
        public bool DefaultEnabled { get; set; }

        public MovementSafeLandingOptionDefinition()
        {
            Id = string.Empty;
            Label = string.Empty;
            Tooltip = string.Empty;
        }
    }
}
