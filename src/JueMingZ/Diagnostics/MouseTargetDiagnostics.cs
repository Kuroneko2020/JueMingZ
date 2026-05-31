namespace JueMingZ.Diagnostics
{
    public static class MouseTargetDiagnostics
    {
        private static readonly object SyncRoot = new object();

        public static string LastStatus { get; private set; } = string.Empty;
        public static string LastResultCode { get; private set; } = string.Empty;
        public static string LastMessage { get; private set; } = string.Empty;

        public static void Record(string status, string resultCode, string message)
        {
            lock (SyncRoot)
            {
                LastStatus = status ?? string.Empty;
                LastResultCode = resultCode ?? string.Empty;
                LastMessage = message ?? string.Empty;
            }
        }
    }
}
