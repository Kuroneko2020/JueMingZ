using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace JueMingZ.Diagnostics
{
    public sealed class PerformanceHitchSample
    {
        public DateTime UtcNow { get; set; }
        public string TestRunId { get; set; }
        public string RuntimeVersion { get; set; }
        public long UpdateCount { get; set; }
        public double UpdateStartGapMs { get; set; }
        public double RuntimeUpdateMs { get; set; }
        public double GameStateReadMs { get; set; }
        public double ActionQueueUpdateMs { get; set; }
        public double InputActionUpdateMs { get; set; }
        public string SlowestStageName { get; set; }
        public double SlowestStageElapsedMs { get; set; }
        public string SlowestOperationName { get; set; }
        public double SlowestOperationElapsedMs { get; set; }
        public double InformationLastDrawElapsedMs { get; set; }
        public string InformationEnabledSummary { get; set; }
        public string InformationLastSkipReason { get; set; }
        public bool FishingAutomationNeedsTick { get; set; }
        public bool FishingDisplayNeedsCatchResolver { get; set; }
        public bool FishingHasResidualState { get; set; }
        public bool LateBootstrapCompleted { get; set; }
        public bool IsInWorld { get; set; }
        public bool IsInMainMenu { get; set; }
        public string NetModeDescription { get; set; }
        public bool DiagnosticsOverlayVisible { get; set; }
        public bool LegacyMainUiVisible { get; set; }
        public string LegacyMainUiPageId { get; set; }
        public int CombatAimAssistRadius { get; set; }
        public int CursorAimRadius { get; set; }
        public int PlayerAimRadius { get; set; }
        public bool CombatAimMarkerEnabled { get; set; }
        public bool CombatEquipmentWarningEnabled { get; set; }
        public int PendingActionCount { get; set; }
        public string RunningActionKind { get; set; }
        public string LastActionKind { get; set; }
        public string LastActionResultCode { get; set; }
        public string ActionQueueOccupiedChannels { get; set; }
        public string ActionQueueBridgeBusyChannels { get; set; }

        public PerformanceHitchSample()
        {
            TestRunId = string.Empty;
            RuntimeVersion = string.Empty;
            SlowestStageName = string.Empty;
            SlowestOperationName = string.Empty;
            InformationEnabledSummary = string.Empty;
            InformationLastSkipReason = string.Empty;
            NetModeDescription = string.Empty;
            LegacyMainUiPageId = string.Empty;
            RunningActionKind = string.Empty;
            LastActionKind = string.Empty;
            LastActionResultCode = string.Empty;
            ActionQueueOccupiedChannels = string.Empty;
            ActionQueueBridgeBusyChannels = string.Empty;
        }
    }

    public sealed class PerformanceOperationSample
    {
        public DateTime UtcNow { get; set; }
        public string TestRunId { get; set; }
        public string RuntimeVersion { get; set; }
        public string Scenario { get; set; }
        public double ElapsedMs { get; set; }
        public double ThresholdMs { get; set; }
        public string Reason { get; set; }
        public string OwnerSummary { get; set; }
        public string Metadata { get; set; }

        public PerformanceOperationSample()
        {
            TestRunId = string.Empty;
            RuntimeVersion = string.Empty;
            Scenario = string.Empty;
            Reason = string.Empty;
            OwnerSummary = string.Empty;
            Metadata = string.Empty;
        }
    }

    public static class PerformanceHitchRecorder
    {
        // Thresholds gate the only performance-events append stream; normal frames update counters in memory.
        public const double UpdateStartGapThresholdMs = 50d;
        public const double RuntimeUpdateThresholdMs = 25d;
        public const double GameStateReadThresholdMs = 10d;
        public const double ActionQueueUpdateThresholdMs = 10d;
        public const double InputActionUpdateThresholdMs = 10d;
        public const double InformationDrawThresholdMs = 10d;
        public const double ActionQueueAdmissionThresholdMs = ActionQueueUpdateThresholdMs;
        public const double ItemCheckWriterResolveThresholdMs = InputActionUpdateThresholdMs;
        public const double InventoryTransactionVerifyThresholdMs = ActionQueueUpdateThresholdMs;
        public const double SevereUpdateStartGapThresholdMs = 125d;
        public const double SevereRuntimeUpdateThresholdMs = 50d;

        private static readonly object SyncRoot = new object();
        private static readonly object WriteQueueSyncRoot = new object();
        private static readonly System.Collections.Generic.Queue<PendingPerformanceEventWrite> PendingWrites =
            new System.Collections.Generic.Queue<PendingPerformanceEventWrite>();
        private const int MaxPendingWrites = 512;
        private static readonly TimeSpan MinRecordInterval = TimeSpan.FromMilliseconds(250);
        private static DateTime _lastRecordUtc = DateTime.MinValue;
        private static DateTime _lastWriteFailureUtc = DateTime.MinValue;
        private static bool _writeWorkerScheduled;

        public static string PerformanceEventsPath
        {
            get
            {
                return Path.Combine(
                    DiagnosticSnapshotWriter.DiagnosticsDirectory,
                    "performance-events-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");
            }
        }

        public static bool ShouldRecord(PerformanceHitchSample sample)
        {
            return sample != null &&
                   ShouldRecordFast(
                       sample.UpdateStartGapMs,
                       sample.RuntimeUpdateMs,
                       sample.GameStateReadMs,
                       sample.ActionQueueUpdateMs,
                       sample.InputActionUpdateMs,
                       sample.InformationLastDrawElapsedMs);
        }

        public static bool ShouldRecordFast(
            double updateStartGapMs,
            double runtimeUpdateMs,
            double gameStateReadMs,
            double actionQueueUpdateMs,
            double inputActionUpdateMs,
            double informationLastDrawElapsedMs)
        {
            return updateStartGapMs >= UpdateStartGapThresholdMs ||
                   runtimeUpdateMs >= RuntimeUpdateThresholdMs ||
                   gameStateReadMs >= GameStateReadThresholdMs ||
                   actionQueueUpdateMs >= ActionQueueUpdateThresholdMs ||
                   inputActionUpdateMs >= InputActionUpdateThresholdMs ||
                   informationLastDrawElapsedMs >= InformationDrawThresholdMs;
        }

        public static bool ShouldRecordOperationFast(double elapsedMs, double thresholdMs)
        {
            return thresholdMs > 0d &&
                   !double.IsNaN(elapsedMs) &&
                   !double.IsInfinity(elapsedMs) &&
                   elapsedMs >= thresholdMs;
        }

        public static double ElapsedMilliseconds(long startTimestamp, long endTimestamp)
        {
            if (endTimestamp <= startTimestamp)
            {
                return 0d;
            }

            return (endTimestamp - startTimestamp) * 1000d / Stopwatch.Frequency;
        }

        public static string BuildReason(PerformanceHitchSample sample)
        {
            if (sample == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            AppendReason(builder, sample.UpdateStartGapMs >= UpdateStartGapThresholdMs, "updateGap");
            AppendReason(builder, sample.RuntimeUpdateMs >= RuntimeUpdateThresholdMs, "runtimeUpdate");
            AppendReason(builder, sample.GameStateReadMs >= GameStateReadThresholdMs, "gameStateRead");
            AppendReason(builder, sample.ActionQueueUpdateMs >= ActionQueueUpdateThresholdMs, "actionQueueUpdate");
            AppendReason(builder, sample.InputActionUpdateMs >= InputActionUpdateThresholdMs, "inputActionUpdate");
            AppendReason(builder, sample.InformationLastDrawElapsedMs >= InformationDrawThresholdMs, "informationDraw");
            return builder.ToString();
        }

        public static void RecordIfNeeded(PerformanceHitchSample sample)
        {
            if (sample == null)
            {
                return;
            }

            // performance-events is an over-threshold stream only; normal runtime
            // frames must not enqueue JSON writes just to refresh diagnostics.
            var reason = BuildReason(sample);
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            if (sample.UtcNow == default(DateTime))
            {
                sample.UtcNow = DateTime.UtcNow;
            }

            if (!ReserveRecordSlot(sample))
            {
                return;
            }

            var path = PerformanceEventsPath;
            RuntimePerformanceDiagnostics.RecordHitch(sample, reason, path);

            try
            {
                EnqueueWrite(path, BuildEventJson(sample, reason));
            }
            catch (Exception error)
            {
                if (DateTime.UtcNow - _lastWriteFailureUtc > TimeSpan.FromSeconds(30))
                {
                    _lastWriteFailureUtc = DateTime.UtcNow;
                    Logger.Warn("PerformanceHitchRecorder", "Performance hitch event write failed: " + error.Message);
                }
            }
        }

        public static void RecordIfNeeded(
            double updateStartGapMs,
            double runtimeUpdateMs,
            double gameStateReadMs,
            double actionQueueUpdateMs,
            double inputActionUpdateMs,
            double informationLastDrawElapsedMs,
            Func<PerformanceHitchSample> sampleFactory)
        {
            if (!ShouldRecordFast(
                updateStartGapMs,
                runtimeUpdateMs,
                gameStateReadMs,
                actionQueueUpdateMs,
                inputActionUpdateMs,
                informationLastDrawElapsedMs))
            {
                return;
            }

            RecordIfNeeded(sampleFactory == null ? null : sampleFactory());
        }

        public static void RecordOperationIfNeeded(
            string scenario,
            double elapsedMs,
            double thresholdMs,
            string reason,
            string ownerSummary,
            string metadata)
        {
            // Slow operation samples share the same threshold and throttle contract
            // as hitch samples, so admission/verification hot paths stay quiet.
            if (!ShouldRecordOperationFast(elapsedMs, thresholdMs))
            {
                return;
            }

            var sample = new PerformanceOperationSample
            {
                UtcNow = DateTime.UtcNow,
                Scenario = scenario ?? string.Empty,
                ElapsedMs = elapsedMs,
                ThresholdMs = thresholdMs,
                Reason = reason ?? string.Empty,
                OwnerSummary = ownerSummary ?? string.Empty,
                Metadata = metadata ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(sample.Scenario))
            {
                sample.Scenario = "Performance.Operation";
            }

            if (!ReserveOperationRecordSlot(sample))
            {
                return;
            }

            var path = PerformanceEventsPath;
            RuntimePerformanceDiagnostics.RecordOperation(sample, path);

            try
            {
                EnqueueWrite(path, BuildOperationEventJson(sample));
            }
            catch (Exception error)
            {
                if (DateTime.UtcNow - _lastWriteFailureUtc > TimeSpan.FromSeconds(30))
                {
                    _lastWriteFailureUtc = DateTime.UtcNow;
                    Logger.Warn("PerformanceHitchRecorder", "Performance operation event write failed: " + error.Message);
                }
            }
        }

        private static bool ReserveRecordSlot(PerformanceHitchSample sample)
        {
            var severe =
                sample.UpdateStartGapMs >= SevereUpdateStartGapThresholdMs ||
                sample.RuntimeUpdateMs >= SevereRuntimeUpdateThresholdMs;

            lock (SyncRoot)
            {
                if (!severe && sample.UtcNow - _lastRecordUtc < MinRecordInterval)
                {
                    return false;
                }

                _lastRecordUtc = sample.UtcNow;
                return true;
            }
        }

        private static bool ReserveOperationRecordSlot(PerformanceOperationSample sample)
        {
            var severe = sample != null &&
                         sample.ElapsedMs >= Math.Max(sample.ThresholdMs * 2d, sample.ThresholdMs + 10d);

            lock (SyncRoot)
            {
                var now = sample == null || sample.UtcNow == default(DateTime)
                    ? DateTime.UtcNow
                    : sample.UtcNow;
                if (!severe && now - _lastRecordUtc < MinRecordInterval)
                {
                    return false;
                }

                _lastRecordUtc = now;
                return true;
            }
        }

        private static void AppendReason(StringBuilder builder, bool condition, string reason)
        {
            if (!condition || string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("+");
            }

            builder.Append(reason);
        }

        private static void EnqueueWrite(string path, string line)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrEmpty(line))
            {
                return;
            }

            lock (WriteQueueSyncRoot)
            {
                if (PendingWrites.Count >= MaxPendingWrites)
                {
                    PendingWrites.Dequeue();
                }

                PendingWrites.Enqueue(new PendingPerformanceEventWrite(path, line));
                if (_writeWorkerScheduled)
                {
                    return;
                }

                _writeWorkerScheduled = true;
                ThreadPool.QueueUserWorkItem(FlushPendingWrites);
            }
        }

        private static void FlushPendingWrites(object ignored)
        {
            while (true)
            {
                PendingPerformanceEventWrite[] batch;
                lock (WriteQueueSyncRoot)
                {
                    if (PendingWrites.Count == 0)
                    {
                        _writeWorkerScheduled = false;
                        return;
                    }

                    batch = PendingWrites.ToArray();
                    PendingWrites.Clear();
                }

                try
                {
                    WriteBatch(batch);
                }
                catch (Exception error)
                {
                    if (DateTime.UtcNow - _lastWriteFailureUtc > TimeSpan.FromSeconds(30))
                    {
                        _lastWriteFailureUtc = DateTime.UtcNow;
                        Logger.Warn("PerformanceHitchRecorder", "Performance hitch async write failed: " + error.Message);
                    }
                }
            }
        }

        private static void WriteBatch(PendingPerformanceEventWrite[] batch)
        {
            if (batch == null || batch.Length == 0)
            {
                return;
            }

            Directory.CreateDirectory(DiagnosticSnapshotWriter.DiagnosticsDirectory);
            var index = 0;
            while (index < batch.Length)
            {
                var path = batch[index].Path;
                var builder = new StringBuilder();
                do
                {
                    builder.AppendLine(batch[index].Line);
                    index++;
                }
                while (index < batch.Length && string.Equals(path, batch[index].Path, StringComparison.OrdinalIgnoreCase));

                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(builder.ToString());
                }
            }
        }

        private static string BuildEventJson(PerformanceHitchSample sample, string reason)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "time", sample.UtcNow.ToString("o", CultureInfo.InvariantCulture), true);
            AppendString(builder, "testRunId", sample.TestRunId, true);
            AppendString(builder, "scenario", "Performance.Hitch", true);
            AppendString(builder, "runtimeVersion", sample.RuntimeVersion, true);
            AppendRaw(builder, "updateCount", sample.UpdateCount.ToString(CultureInfo.InvariantCulture), true);
            AppendString(builder, "reason", reason, true);
            AppendRaw(builder, "thresholds", BuildThresholdsJson(), true);
            AppendRaw(builder, "timing", BuildTimingJson(sample), true);
            AppendRaw(builder, "slowestStage", BuildSlowestStageJson(sample), true);
            AppendRaw(builder, "slowestOperation", BuildSlowestOperationJson(sample), true);
            AppendRaw(builder, "state", BuildStateJson(sample), true);
            AppendRaw(builder, "features", BuildFeaturesJson(sample), true);
            AppendRaw(builder, "actionQueue", BuildActionQueueJson(sample), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildOperationEventJson(PerformanceOperationSample sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "time", sample.UtcNow.ToString("o", CultureInfo.InvariantCulture), true);
            AppendString(builder, "testRunId", sample.TestRunId, true);
            AppendString(builder, "scenario", sample.Scenario, true);
            AppendString(builder, "runtimeVersion", sample.RuntimeVersion, true);
            AppendRaw(builder, "elapsedMs", DoubleRaw(sample.ElapsedMs), true);
            AppendRaw(builder, "thresholdMs", DoubleRaw(sample.ThresholdMs), true);
            AppendString(builder, "reason", sample.Reason, true);
            AppendString(builder, "ownerSummary", TrimForEvent(sample.OwnerSummary, 512), true);
            AppendString(builder, "metadata", TrimForEvent(sample.Metadata, 512), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildThresholdsJson()
        {
            return "{" +
                   "\"updateStartGapMs\":" + DoubleRaw(UpdateStartGapThresholdMs) + "," +
                   "\"runtimeUpdateMs\":" + DoubleRaw(RuntimeUpdateThresholdMs) + "," +
                   "\"gameStateReadMs\":" + DoubleRaw(GameStateReadThresholdMs) + "," +
                   "\"actionQueueUpdateMs\":" + DoubleRaw(ActionQueueUpdateThresholdMs) + "," +
                   "\"inputActionUpdateMs\":" + DoubleRaw(InputActionUpdateThresholdMs) + "," +
                   "\"informationDrawMs\":" + DoubleRaw(InformationDrawThresholdMs) +
                   "}";
        }

        private static string BuildTimingJson(PerformanceHitchSample sample)
        {
            return "{" +
                   "\"updateStartGapMs\":" + DoubleRaw(sample.UpdateStartGapMs) + "," +
                   "\"runtimeUpdateMs\":" + DoubleRaw(sample.RuntimeUpdateMs) + "," +
                   "\"gameStateReadMs\":" + DoubleRaw(sample.GameStateReadMs) + "," +
                   "\"actionQueueUpdateMs\":" + DoubleRaw(sample.ActionQueueUpdateMs) + "," +
                   "\"inputActionUpdateMs\":" + DoubleRaw(sample.InputActionUpdateMs) + "," +
                   "\"informationLastDrawElapsedMs\":" + DoubleRaw(sample.InformationLastDrawElapsedMs) +
                   "}";
        }

        private static string BuildSlowestStageJson(PerformanceHitchSample sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "name", sample.SlowestStageName, true);
            AppendRaw(builder, "elapsedMs", DoubleRaw(sample.SlowestStageElapsedMs), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildSlowestOperationJson(PerformanceHitchSample sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "name", sample.SlowestOperationName, true);
            AppendRaw(builder, "elapsedMs", DoubleRaw(sample.SlowestOperationElapsedMs), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildStateJson(PerformanceHitchSample sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "lateBootstrapCompleted", BoolRaw(sample.LateBootstrapCompleted), true);
            AppendRaw(builder, "isInWorld", BoolRaw(sample.IsInWorld), true);
            AppendRaw(builder, "isInMainMenu", BoolRaw(sample.IsInMainMenu), true);
            AppendString(builder, "netModeDescription", sample.NetModeDescription, true);
            AppendRaw(builder, "diagnosticsOverlayVisible", BoolRaw(sample.DiagnosticsOverlayVisible), true);
            AppendRaw(builder, "legacyMainUiVisible", BoolRaw(sample.LegacyMainUiVisible), true);
            AppendString(builder, "legacyMainUiPageId", sample.LegacyMainUiPageId, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildFeaturesJson(PerformanceHitchSample sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "combatAimAssistRadius", sample.CombatAimAssistRadius.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "cursorAimRadius", sample.CursorAimRadius.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "playerAimRadius", sample.PlayerAimRadius.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "combatAimMarkerEnabled", BoolRaw(sample.CombatAimMarkerEnabled), true);
            AppendRaw(builder, "combatEquipmentWarningEnabled", BoolRaw(sample.CombatEquipmentWarningEnabled), true);
            AppendString(builder, "informationEnabledSummary", sample.InformationEnabledSummary, true);
            AppendString(builder, "informationLastSkipReason", sample.InformationLastSkipReason, true);
            AppendRaw(builder, "fishingAutomationNeedsTick", BoolRaw(sample.FishingAutomationNeedsTick), true);
            AppendRaw(builder, "fishingDisplayNeedsCatchResolver", BoolRaw(sample.FishingDisplayNeedsCatchResolver), true);
            AppendRaw(builder, "fishingHasResidualState", BoolRaw(sample.FishingHasResidualState), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildActionQueueJson(PerformanceHitchSample sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "pendingActionCount", sample.PendingActionCount.ToString(CultureInfo.InvariantCulture), true);
            AppendString(builder, "runningActionKind", sample.RunningActionKind, true);
            AppendString(builder, "lastActionKind", sample.LastActionKind, true);
            AppendString(builder, "lastActionResultCode", sample.LastActionResultCode, true);
            AppendString(builder, "occupiedChannels", sample.ActionQueueOccupiedChannels, true);
            AppendString(builder, "bridgeBusyChannels", sample.ActionQueueBridgeBusyChannels, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"");
            builder.Append(EscapeJson(name));
            builder.Append("\": \"");
            builder.Append(EscapeJson(value ?? string.Empty));
            builder.Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string rawValue, bool comma)
        {
            builder.Append("\"");
            builder.Append(EscapeJson(name));
            builder.Append("\": ");
            builder.Append(string.IsNullOrWhiteSpace(rawValue) ? "null" : rawValue);
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string DoubleRaw(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string TrimForEvent(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0 || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private struct PendingPerformanceEventWrite
        {
            public PendingPerformanceEventWrite(string path, string line)
            {
                Path = path;
                Line = line;
            }

            public readonly string Path;
            public readonly string Line;
        }
    }
}
