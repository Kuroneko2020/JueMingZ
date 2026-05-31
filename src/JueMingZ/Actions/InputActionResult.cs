using System;

namespace JueMingZ.Actions
{
    public sealed class InputActionResult
    {
        public Guid RequestId { get; set; }
        public InputActionKind Kind { get; set; }
        public InputActionStatus Status { get; set; }
        public string Message { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }
        public long DurationMs { get; set; }
        public string SourceFeatureId { get; set; }
        public string Scenario { get; set; }
        public string SourceHotkey { get; set; }
        public string SourceKind { get; set; }
        public string SourceUi { get; set; }
        public string ButtonId { get; set; }
        public string ButtonLabel { get; set; }
        public string ResultCode { get; set; }
        public bool ActionEventRecorded { get; set; }
        public Exception Error { get; set; }

        public static InputActionResult FromRequest(InputActionRequest request, InputActionStatus status, string message, DateTime startedUtc, Exception error = null)
        {
            return new InputActionResult
            {
                RequestId = request.RequestId,
                Kind = request.Kind,
                Status = status,
                Message = message ?? string.Empty,
                StartedUtc = startedUtc,
                FinishedUtc = DateTime.UtcNow,
                DurationMs = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds,
                SourceFeatureId = request.SourceFeatureId ?? string.Empty,
                Scenario = GetMetadata(request, "Scenario"),
                SourceHotkey = GetMetadata(request, "SourceHotkey"),
                SourceKind = GetMetadata(request, "SourceKind"),
                SourceUi = GetMetadata(request, "SourceUi"),
                ButtonId = GetMetadata(request, "ButtonId"),
                ButtonLabel = GetMetadata(request, "ButtonLabel"),
                ResultCode = MapResultCode(status),
                Error = error
            };
        }

        public static InputActionResult FromExecution(InputActionExecution execution, InputActionStatus status, string message, Exception error = null)
        {
            var finishedUtc = DateTime.UtcNow;
            var request = execution == null ? null : execution.Request;
            var startedUtc = execution == null ? finishedUtc : execution.StartedUtc;
            return new InputActionResult
            {
                RequestId = request == null ? Guid.Empty : request.RequestId,
                Kind = request == null ? InputActionKind.None : request.Kind,
                Status = status,
                Message = message ?? string.Empty,
                StartedUtc = startedUtc,
                FinishedUtc = finishedUtc,
                DurationMs = (long)(finishedUtc - startedUtc).TotalMilliseconds,
                SourceFeatureId = request == null ? string.Empty : request.SourceFeatureId ?? string.Empty,
                Scenario = request == null ? string.Empty : GetMetadata(request, "Scenario"),
                SourceHotkey = request == null ? string.Empty : GetMetadata(request, "SourceHotkey"),
                SourceKind = request == null ? string.Empty : GetMetadata(request, "SourceKind"),
                SourceUi = request == null ? string.Empty : GetMetadata(request, "SourceUi"),
                ButtonId = request == null ? string.Empty : GetMetadata(request, "ButtonId"),
                ButtonLabel = request == null ? string.Empty : GetMetadata(request, "ButtonLabel"),
                ResultCode = GetExecutionResultCode(execution, status),
                ActionEventRecorded = GetExecutionBool(execution, "ActionEventRecorded"),
                Error = error
            };
        }

        public static string MapResultCode(InputActionStatus status)
        {
            switch (status)
            {
                case InputActionStatus.Succeeded:
                    return DiagnosticResultCode.Succeeded.ToString();
                case InputActionStatus.AttemptedButUnverified:
                    return DiagnosticResultCode.AttemptedButUnverified.ToString();
                case InputActionStatus.NotApplicable:
                    return DiagnosticResultCode.NotApplicable.ToString();
                case InputActionStatus.BlockedByUi:
                    return DiagnosticResultCode.BlockedByUi.ToString();
                case InputActionStatus.TimedOut:
                    return DiagnosticResultCode.TimedOut.ToString();
                case InputActionStatus.NotImplemented:
                    return DiagnosticResultCode.NotImplemented.ToString();
                case InputActionStatus.Failed:
                case InputActionStatus.Cancelled:
                case InputActionStatus.BlockedByHigherPriority:
                default:
                    return DiagnosticResultCode.Failed.ToString();
            }
        }

        private static string GetExecutionResultCode(InputActionExecution execution, InputActionStatus status)
        {
            if (execution != null && execution.State != null)
            {
                string value;
                if (execution.State.TryGetValue("ResultCode", out value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return MapResultCode(status);
        }

        private static bool GetExecutionBool(InputActionExecution execution, string key)
        {
            if (execution == null || execution.State == null)
            {
                return false;
            }

            string value;
            return execution.State.TryGetValue(key, out value) &&
                   string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string GetMetadata(InputActionRequest request, string key)
        {
            if (request == null || request.Metadata == null)
            {
                return string.Empty;
            }

            string value;
            return request.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }
    }
}
