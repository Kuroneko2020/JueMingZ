using System.Diagnostics;
using JueMingZ.Actions;
using JueMingZ.GameState;

namespace JueMingZ.Runtime
{
    internal sealed class RuntimeTickContext
    {
        private readonly long _runtimeStartTimestamp;
        private long _runtimeStopTimestamp;

        public RuntimeTickContext(long runtimeStartTimestamp)
        {
            _runtimeStartTimestamp = runtimeStartTimestamp;
            ActionSnapshot = InputActionQueueFastState.Empty;
        }

        public GameStateSnapshot GameState { get; set; }
        public RuntimeSettingsSnapshot SettingsSnapshot { get; set; }
        public InputActionQueueFastState ActionSnapshot { get; set; }
        public long UpdateTick { get; set; }
        public bool DiagnosticSnapshotDue { get; set; }
        public double UpdateStartGapMs { get; set; }
        public double GameStateReadMs { get; set; }
        public double ActionQueueUpdateMs { get; set; }
        public double InputActionUpdateMs { get; set; }
        public string SlowestStageName { get; private set; }
        public double SlowestStageElapsedMs { get; private set; }
        public string SlowestOperationName { get; private set; }
        public double SlowestOperationElapsedMs { get; private set; }

        public double RuntimeElapsedMs
        {
            get
            {
                var endTimestamp = _runtimeStopTimestamp == 0 ? Stopwatch.GetTimestamp() : _runtimeStopTimestamp;
                return GetElapsedMilliseconds(_runtimeStartTimestamp, endTimestamp);
            }
        }

        public void StopRuntimeWatch()
        {
            if (_runtimeStopTimestamp == 0)
            {
                _runtimeStopTimestamp = Stopwatch.GetTimestamp();
            }
        }

        public void RecordStageTiming(string stageName, double elapsedMs)
        {
            if (elapsedMs <= SlowestStageElapsedMs)
            {
                return;
            }

            SlowestStageName = stageName ?? string.Empty;
            SlowestStageElapsedMs = elapsedMs;
        }

        public void RecordOperationTiming(string operationName, double elapsedMs)
        {
            if (elapsedMs <= SlowestOperationElapsedMs)
            {
                return;
            }

            SlowestOperationName = operationName ?? string.Empty;
            SlowestOperationElapsedMs = elapsedMs;
        }

        public static double GetElapsedMilliseconds(long startTimestamp, long endTimestamp)
        {
            if (startTimestamp <= 0 || endTimestamp < startTimestamp)
            {
                return 0d;
            }

            return (endTimestamp - startTimestamp) * 1000d / Stopwatch.Frequency;
        }
    }
}
