namespace JueMingZ.Automation.WorldAutomation
{
    public sealed class TravelMenuToggleResult
    {
        public bool Succeeded { get; set; }
        public bool Enabled { get; set; }
        public bool OpenedMenu { get; set; }
        public bool Restored { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }

        public TravelMenuToggleResult()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
            Detail = string.Empty;
        }
    }
}
