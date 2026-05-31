namespace JueMingZ.Diagnostics
{
    public static class RuntimePerformanceDiagnostics
    {
        private static readonly object SyncRoot = new object();

        public static long RuntimeUpdateCount { get; private set; }
        public static double LastRuntimeUpdateMs { get; private set; }
        public static double LastUpdateStartGapMs { get; private set; }
        public static double LastGameStateReadMs { get; private set; }
        public static double LastActionQueueUpdateMs { get; private set; }
        public static double LastInputActionUpdateMs { get; private set; }
        public static double AverageRuntimeUpdateMs { get; private set; }
        public static string LastSlowestStageName { get; private set; }
        public static double LastSlowestStageElapsedMs { get; private set; }
        public static string LastSlowestOperationName { get; private set; }
        public static double LastSlowestOperationElapsedMs { get; private set; }
        public static long PerformanceHitchCount { get; private set; }
        public static string PerformanceEventsPath { get; private set; }
        public static string LastPerformanceHitchReason { get; private set; }
        public static System.DateTime? LastPerformanceHitchUtc { get; private set; }
        public static double LastPerformanceHitchUpdateGapMs { get; private set; }
        public static double LastPerformanceHitchRuntimeUpdateMs { get; private set; }
        public static double LastPerformanceHitchGameStateReadMs { get; private set; }
        public static double LastPerformanceHitchActionQueueUpdateMs { get; private set; }
        public static double LastPerformanceHitchInputActionUpdateMs { get; private set; }
        public static double LastPerformanceHitchInformationDrawMs { get; private set; }
        public static string LastPerformanceHitchSlowestStageName { get; private set; }
        public static double LastPerformanceHitchSlowestStageMs { get; private set; }
        public static string LastPerformanceHitchSlowestOperationName { get; private set; }
        public static double LastPerformanceHitchSlowestOperationMs { get; private set; }

        public static void Record(
            double runtimeUpdateMs,
            double updateStartGapMs,
            double gameStateReadMs,
            double actionQueueUpdateMs,
            double inputActionUpdateMs,
            string slowestStageName,
            double slowestStageElapsedMs,
            string slowestOperationName,
            double slowestOperationElapsedMs)
        {
            lock (SyncRoot)
            {
                RuntimeUpdateCount++;
                LastRuntimeUpdateMs = runtimeUpdateMs;
                LastUpdateStartGapMs = updateStartGapMs;
                LastGameStateReadMs = gameStateReadMs;
                LastActionQueueUpdateMs = actionQueueUpdateMs;
                LastInputActionUpdateMs = inputActionUpdateMs;
                LastSlowestStageName = slowestStageName ?? string.Empty;
                LastSlowestStageElapsedMs = slowestStageElapsedMs;
                LastSlowestOperationName = slowestOperationName ?? string.Empty;
                LastSlowestOperationElapsedMs = slowestOperationElapsedMs;
                AverageRuntimeUpdateMs = RuntimeUpdateCount <= 1
                    ? runtimeUpdateMs
                    : AverageRuntimeUpdateMs + ((runtimeUpdateMs - AverageRuntimeUpdateMs) / RuntimeUpdateCount);
            }
        }

        public static void RecordHitch(PerformanceHitchSample sample, string reason, string performanceEventsPath)
        {
            if (sample == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                PerformanceHitchCount++;
                PerformanceEventsPath = performanceEventsPath ?? string.Empty;
                LastPerformanceHitchReason = reason ?? string.Empty;
                LastPerformanceHitchUtc = sample.UtcNow;
                LastPerformanceHitchUpdateGapMs = sample.UpdateStartGapMs;
                LastPerformanceHitchRuntimeUpdateMs = sample.RuntimeUpdateMs;
                LastPerformanceHitchGameStateReadMs = sample.GameStateReadMs;
                LastPerformanceHitchActionQueueUpdateMs = sample.ActionQueueUpdateMs;
                LastPerformanceHitchInputActionUpdateMs = sample.InputActionUpdateMs;
                LastPerformanceHitchInformationDrawMs = sample.InformationLastDrawElapsedMs;
                LastPerformanceHitchSlowestStageName = sample.SlowestStageName ?? string.Empty;
                LastPerformanceHitchSlowestStageMs = sample.SlowestStageElapsedMs;
                LastPerformanceHitchSlowestOperationName = sample.SlowestOperationName ?? string.Empty;
                LastPerformanceHitchSlowestOperationMs = sample.SlowestOperationElapsedMs;
            }
        }
    }
}
