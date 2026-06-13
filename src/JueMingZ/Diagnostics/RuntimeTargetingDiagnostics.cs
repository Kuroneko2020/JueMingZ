using System;

namespace JueMingZ.Diagnostics
{
    public static class RuntimeTargetingDiagnostics
    {
        private static readonly object SyncRoot = new object();

        public static bool DiagnosticInputSkipped { get; private set; }
        public static string DiagnosticInputGateStatus { get; private set; } = string.Empty;
        public static string DiagnosticInputSkipReason { get; private set; } = string.Empty;
        public static DateTime? DiagnosticInputSkipUtc { get; private set; }

        public static void RecordDiagnosticInputSkipped(string reason)
        {
            lock (SyncRoot)
            {
                DiagnosticInputSkipped = true;
                DiagnosticInputGateStatus = "skipped";
                DiagnosticInputSkipReason = reason ?? string.Empty;
                DiagnosticInputSkipUtc = DateTime.UtcNow;
            }
        }

        public static void RecordDiagnosticInputAvailable()
        {
            lock (SyncRoot)
            {
                DiagnosticInputSkipped = false;
                DiagnosticInputGateStatus = "available";
                DiagnosticInputSkipReason = string.Empty;
                DiagnosticInputSkipUtc = null;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                DiagnosticInputSkipped = false;
                DiagnosticInputGateStatus = string.Empty;
                DiagnosticInputSkipReason = string.Empty;
                DiagnosticInputSkipUtc = null;
            }
        }
    }
}
