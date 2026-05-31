using System;

namespace JueMingZ.Actions
{
    public sealed class InputActionExecutionStepResult
    {
        public InputActionStatus Status { get; set; }
        public string Message { get; set; }
        public Exception Error { get; set; }

        public bool IsTerminal
        {
            get
            {
                return Status == InputActionStatus.Succeeded ||
                       Status == InputActionStatus.AttemptedButUnverified ||
                       Status == InputActionStatus.NotApplicable ||
                       Status == InputActionStatus.Failed ||
                       Status == InputActionStatus.TimedOut ||
                       Status == InputActionStatus.Cancelled ||
                       Status == InputActionStatus.NotImplemented ||
                       Status == InputActionStatus.BlockedByUi ||
                       Status == InputActionStatus.BlockedByHigherPriority;
            }
        }

        public static InputActionExecutionStepResult Running(string message)
        {
            return new InputActionExecutionStepResult { Status = InputActionStatus.Running, Message = message ?? string.Empty };
        }

        public static InputActionExecutionStepResult Complete(InputActionStatus status, string message, Exception error = null)
        {
            return new InputActionExecutionStepResult
            {
                Status = status,
                Message = message ?? string.Empty,
                Error = error
            };
        }
    }
}
