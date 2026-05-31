using System;

namespace JueMingZ.Diagnostics
{
    public static class RuntimeDiagnostics
    {
        private static readonly object SyncRoot = new object();

        public static DateTime? LastUpdateUtc { get; private set; }
        public static long UpdateCount { get; private set; }
        public static bool LateBootstrapCompleted { get; private set; }
        public static string LastActionResult { get; private set; } = string.Empty;
        public static string LastError { get; private set; } = string.Empty;
        public static string LastErrorSource { get; private set; } = string.Empty;

        public static void RecordUpdate(long updateCount, bool lateBootstrapCompleted, string lastActionResult)
        {
            lock (SyncRoot)
            {
                LastUpdateUtc = DateTime.UtcNow;
                UpdateCount = updateCount;
                LateBootstrapCompleted = lateBootstrapCompleted;
                LastActionResult = lastActionResult ?? string.Empty;
            }
        }

        public static void RecordError(string source, Exception error)
        {
            lock (SyncRoot)
            {
                LastErrorSource = source ?? string.Empty;
                LastError = error == null ? string.Empty : error.ToString();
            }
        }

        public static void ClearError()
        {
            lock (SyncRoot)
            {
                LastErrorSource = string.Empty;
                LastError = string.Empty;
            }
        }
    }
}
