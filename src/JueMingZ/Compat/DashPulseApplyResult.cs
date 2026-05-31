using System;

namespace JueMingZ.Compat
{
    public sealed class DashPulseApplyResult
    {
        public bool Applied { get; set; }
        public bool Queued { get; set; }
        public Guid RequestId { get; set; }
        public int Direction { get; set; }
        public string Mode { get; set; }
        public string Message { get; set; }
        public DashInputProfile BeforeProfile { get; set; }
        public DashInputProfile AfterProfile { get; set; }

        public DashPulseApplyResult()
        {
            Mode = string.Empty;
            Message = string.Empty;
        }
    }
}
