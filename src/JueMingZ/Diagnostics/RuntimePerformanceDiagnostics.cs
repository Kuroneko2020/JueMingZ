namespace JueMingZ.Diagnostics
{
    // Rolling performance counters stay in memory; event files are written only after hitch thresholds pass.
    public static class RuntimePerformanceDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private const int RecentWindowCapacity = 600;
        private static readonly RecentPerformanceSample[] RecentWindow =
            new RecentPerformanceSample[RecentWindowCapacity];
        private static int _recentWindowNextIndex;
        private static int _recentWindowCount;
        private static double _recentRuntimeUpdateSumMs;
        private static double _recentGameStateReadSumMs;
        private static double _recentActionQueueUpdateSumMs;
        private static double _recentInputActionUpdateSumMs;
        private static double _recentInformationDrawSumMs;

        public static long RuntimeUpdateCount { get; private set; }
        public static double LastRuntimeUpdateMs { get; private set; }
        public static double LastUpdateStartGapMs { get; private set; }
        public static double LastGameStateReadMs { get; private set; }
        public static double LastActionQueueUpdateMs { get; private set; }
        public static double LastInputActionUpdateMs { get; private set; }
        public static double LastInformationDrawMs { get; private set; }
        public static double AverageRuntimeUpdateMs { get; private set; }
        public static int RecentWindowCapacitySamples { get { return RecentWindowCapacity; } }
        public static int RecentWindowSampleCount { get; private set; }
        public static double RecentRuntimeUpdateAverageMs { get; private set; }
        public static double RecentGameStateReadAverageMs { get; private set; }
        public static double RecentActionQueueUpdateAverageMs { get; private set; }
        public static double RecentInputActionUpdateAverageMs { get; private set; }
        public static double RecentInformationDrawAverageMs { get; private set; }
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
        public static long PerformanceOperationEventCount { get; private set; }
        public static string LastPerformanceOperationScenario { get; private set; }
        public static System.DateTime? LastPerformanceOperationUtc { get; private set; }
        public static double LastPerformanceOperationElapsedMs { get; private set; }
        public static double LastPerformanceOperationThresholdMs { get; private set; }
        public static string LastPerformanceOperationReason { get; private set; }
        public static string LastPerformanceOperationOwnerSummary { get; private set; }

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
            Record(
                runtimeUpdateMs,
                updateStartGapMs,
                gameStateReadMs,
                actionQueueUpdateMs,
                inputActionUpdateMs,
                0d,
                slowestStageName,
                slowestStageElapsedMs,
                slowestOperationName,
                slowestOperationElapsedMs);
        }

        public static void Record(
            double runtimeUpdateMs,
            double updateStartGapMs,
            double gameStateReadMs,
            double actionQueueUpdateMs,
            double inputActionUpdateMs,
            double informationDrawMs,
            string slowestStageName,
            double slowestStageElapsedMs,
            string slowestOperationName,
            double slowestOperationElapsedMs)
        {
            // Normal ticks only update in-memory counters here; performance event
            // writes stay behind PerformanceHitchRecorder threshold checks.
            lock (SyncRoot)
            {
                RuntimeUpdateCount++;
                LastRuntimeUpdateMs = runtimeUpdateMs;
                LastUpdateStartGapMs = updateStartGapMs;
                LastGameStateReadMs = gameStateReadMs;
                LastActionQueueUpdateMs = actionQueueUpdateMs;
                LastInputActionUpdateMs = inputActionUpdateMs;
                LastInformationDrawMs = informationDrawMs;
                LastSlowestStageName = slowestStageName ?? string.Empty;
                LastSlowestStageElapsedMs = slowestStageElapsedMs;
                LastSlowestOperationName = slowestOperationName ?? string.Empty;
                LastSlowestOperationElapsedMs = slowestOperationElapsedMs;
                AverageRuntimeUpdateMs = RuntimeUpdateCount <= 1
                    ? runtimeUpdateMs
                    : AverageRuntimeUpdateMs + ((runtimeUpdateMs - AverageRuntimeUpdateMs) / RuntimeUpdateCount);
                RecordRecentWindow(
                    runtimeUpdateMs,
                    gameStateReadMs,
                    actionQueueUpdateMs,
                    inputActionUpdateMs,
                    informationDrawMs);
            }
        }

        public static void RecordHitch(PerformanceHitchSample sample, string reason, string performanceEventsPath)
        {
            // Called only after hitch threshold and throttle checks. Do not use
            // this path as a per-tick diagnostic append point.
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

        public static void RecordOperation(PerformanceOperationSample sample, string performanceEventsPath)
        {
            // Operation samples share the slow-path contract; callers must fast
            // check thresholds before building owner summaries or metadata.
            if (sample == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                PerformanceOperationEventCount++;
                PerformanceEventsPath = performanceEventsPath ?? string.Empty;
                LastPerformanceOperationScenario = sample.Scenario ?? string.Empty;
                LastPerformanceOperationUtc = sample.UtcNow;
                LastPerformanceOperationElapsedMs = sample.ElapsedMs;
                LastPerformanceOperationThresholdMs = sample.ThresholdMs;
                LastPerformanceOperationReason = sample.Reason ?? string.Empty;
                LastPerformanceOperationOwnerSummary = sample.OwnerSummary ?? string.Empty;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                RuntimeUpdateCount = 0;
                LastRuntimeUpdateMs = 0d;
                LastUpdateStartGapMs = 0d;
                LastGameStateReadMs = 0d;
                LastActionQueueUpdateMs = 0d;
                LastInputActionUpdateMs = 0d;
                LastInformationDrawMs = 0d;
                AverageRuntimeUpdateMs = 0d;
                LastSlowestStageName = string.Empty;
                LastSlowestStageElapsedMs = 0d;
                LastSlowestOperationName = string.Empty;
                LastSlowestOperationElapsedMs = 0d;
                PerformanceHitchCount = 0;
                PerformanceEventsPath = string.Empty;
                LastPerformanceHitchReason = string.Empty;
                LastPerformanceHitchUtc = null;
                LastPerformanceHitchUpdateGapMs = 0d;
                LastPerformanceHitchRuntimeUpdateMs = 0d;
                LastPerformanceHitchGameStateReadMs = 0d;
                LastPerformanceHitchActionQueueUpdateMs = 0d;
                LastPerformanceHitchInputActionUpdateMs = 0d;
                LastPerformanceHitchInformationDrawMs = 0d;
                LastPerformanceHitchSlowestStageName = string.Empty;
                LastPerformanceHitchSlowestStageMs = 0d;
                LastPerformanceHitchSlowestOperationName = string.Empty;
                LastPerformanceHitchSlowestOperationMs = 0d;
                PerformanceOperationEventCount = 0;
                LastPerformanceOperationScenario = string.Empty;
                LastPerformanceOperationUtc = null;
                LastPerformanceOperationElapsedMs = 0d;
                LastPerformanceOperationThresholdMs = 0d;
                LastPerformanceOperationReason = string.Empty;
                LastPerformanceOperationOwnerSummary = string.Empty;
                ClearRecentWindow();
            }
        }

        private static void RecordRecentWindow(
            double runtimeUpdateMs,
            double gameStateReadMs,
            double actionQueueUpdateMs,
            double inputActionUpdateMs,
            double informationDrawMs)
        {
            var replacing = _recentWindowCount >= RecentWindowCapacity;
            if (replacing)
            {
                var old = RecentWindow[_recentWindowNextIndex];
                _recentRuntimeUpdateSumMs -= old.RuntimeUpdateMs;
                _recentGameStateReadSumMs -= old.GameStateReadMs;
                _recentActionQueueUpdateSumMs -= old.ActionQueueUpdateMs;
                _recentInputActionUpdateSumMs -= old.InputActionUpdateMs;
                _recentInformationDrawSumMs -= old.InformationDrawMs;
            }
            else
            {
                _recentWindowCount++;
            }

            RecentWindow[_recentWindowNextIndex] = new RecentPerformanceSample
            {
                RuntimeUpdateMs = runtimeUpdateMs,
                GameStateReadMs = gameStateReadMs,
                ActionQueueUpdateMs = actionQueueUpdateMs,
                InputActionUpdateMs = inputActionUpdateMs,
                InformationDrawMs = informationDrawMs
            };
            _recentWindowNextIndex++;
            if (_recentWindowNextIndex >= RecentWindowCapacity)
            {
                _recentWindowNextIndex = 0;
            }

            _recentRuntimeUpdateSumMs += runtimeUpdateMs;
            _recentGameStateReadSumMs += gameStateReadMs;
            _recentActionQueueUpdateSumMs += actionQueueUpdateMs;
            _recentInputActionUpdateSumMs += inputActionUpdateMs;
            _recentInformationDrawSumMs += informationDrawMs;

            RecentWindowSampleCount = _recentWindowCount;
            RecentRuntimeUpdateAverageMs = Average(_recentRuntimeUpdateSumMs, _recentWindowCount);
            RecentGameStateReadAverageMs = Average(_recentGameStateReadSumMs, _recentWindowCount);
            RecentActionQueueUpdateAverageMs = Average(_recentActionQueueUpdateSumMs, _recentWindowCount);
            RecentInputActionUpdateAverageMs = Average(_recentInputActionUpdateSumMs, _recentWindowCount);
            RecentInformationDrawAverageMs = Average(_recentInformationDrawSumMs, _recentWindowCount);
        }

        private static void ClearRecentWindow()
        {
            System.Array.Clear(RecentWindow, 0, RecentWindow.Length);
            _recentWindowNextIndex = 0;
            _recentWindowCount = 0;
            _recentRuntimeUpdateSumMs = 0d;
            _recentGameStateReadSumMs = 0d;
            _recentActionQueueUpdateSumMs = 0d;
            _recentInputActionUpdateSumMs = 0d;
            _recentInformationDrawSumMs = 0d;
            RecentWindowSampleCount = 0;
            RecentRuntimeUpdateAverageMs = 0d;
            RecentGameStateReadAverageMs = 0d;
            RecentActionQueueUpdateAverageMs = 0d;
            RecentInputActionUpdateAverageMs = 0d;
            RecentInformationDrawAverageMs = 0d;
        }

        private static double Average(double sum, int count)
        {
            return count <= 0 ? 0d : sum / count;
        }

        private struct RecentPerformanceSample
        {
            public double RuntimeUpdateMs;
            public double GameStateReadMs;
            public double ActionQueueUpdateMs;
            public double InputActionUpdateMs;
            public double InformationDrawMs;
        }
    }
}
