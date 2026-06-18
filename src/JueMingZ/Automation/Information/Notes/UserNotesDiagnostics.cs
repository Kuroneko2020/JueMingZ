using System;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Information.Notes
{
    public static class UserNotesDiagnostics
    {
        private static Action<string, string, bool, string> _observerForTesting;
        private static bool _recordActionEvents = true;

        public static void RecordOperation(string scenario, UserNotesOperationResult result, string noteId)
        {
            var operation = string.IsNullOrWhiteSpace(scenario) ? "Ui.Notes.Operation" : scenario;
            var succeeded = result != null && result.Succeeded;
            var code = result == null ? "unknown" : result.ResultCode;
            var message = result == null ? string.Empty : result.Message;
            var observer = _observerForTesting;
            if (observer != null)
            {
                observer(operation, code, succeeded, noteId ?? string.Empty);
            }

            if (!_recordActionEvents)
            {
                return;
            }

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                operation,
                "UserNotes",
                string.Empty,
                succeeded ? "Succeeded" : "Failed",
                code,
                message,
                0,
                "{}",
                "{}",
                "{\"featureId\":\"information.user_notes\",\"noteId\":\"" + EscapeJson(noteId) + "\"}",
                "Ui",
                "Notes",
                string.Empty,
                string.Empty);
        }

        internal static void SetObserverForTesting(Action<string, string, bool, string> observer)
        {
            _observerForTesting = observer;
        }

        internal static void SetRecordActionEventsForTesting(bool enabled)
        {
            _recordActionEvents = enabled;
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
