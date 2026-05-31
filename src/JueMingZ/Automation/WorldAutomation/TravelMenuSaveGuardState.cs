namespace JueMingZ.Automation.WorldAutomation
{
    public sealed class TravelMenuSaveGuardState
    {
        public bool GuardApplied { get; set; }
        public string SaveKind { get; set; }
        public string Message { get; set; }

        public TravelMenuSaveGuardState()
        {
            SaveKind = string.Empty;
            Message = string.Empty;
        }
    }
}
