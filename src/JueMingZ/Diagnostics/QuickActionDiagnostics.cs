namespace JueMingZ.Diagnostics
{
    public static class QuickActionDiagnostics
    {
        private static readonly object SyncRoot = new object();

        public static string LastKind { get; private set; } = string.Empty;
        public static string LastStatus { get; private set; } = string.Empty;
        public static string LastResultCode { get; private set; } = string.Empty;
        public static string LastMessage { get; private set; } = string.Empty;

        public static void Record(string kind, string status, string resultCode, string message)
        {
            lock (SyncRoot)
            {
                LastKind = kind ?? string.Empty;
                LastStatus = status ?? string.Empty;
                LastResultCode = resultCode ?? string.Empty;
                LastMessage = message ?? string.Empty;
            }
        }
    }
}
