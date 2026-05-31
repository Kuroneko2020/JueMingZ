using System;
using System.Collections.Generic;

namespace JueMingZ.Actions
{
    public sealed class InputActionExecution
    {
        public InputActionRequest Request { get; set; }
        public InputActionStatus Status { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime LastUpdateUtc { get; set; }
        public DateTime? FinishedUtc { get; set; }
        public string Message { get; set; }
        public Exception Error { get; set; }
        public int UpdateCount { get; set; }
        public Dictionary<string, string> State { get; set; }

        public InputActionExecution()
        {
            Status = InputActionStatus.Pending;
            Message = string.Empty;
            State = new Dictionary<string, string>();
        }
    }
}
