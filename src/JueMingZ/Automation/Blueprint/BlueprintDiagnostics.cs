using System;
using System.Globalization;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Blueprint
{
    internal sealed class BlueprintPerformanceCounterSnapshot
    {
        public BlueprintPerformanceCounterSnapshot()
        {
            Scenario = string.Empty;
            LastReason = string.Empty;
        }

        public string Scenario { get; set; }
        public long Count { get; set; }
        public double LastElapsedMs { get; set; }
        public double AverageElapsedMs { get; set; }
        public long SlowEventCount { get; set; }
        public string LastReason { get; set; }
        public DateTime? LastRecordedUtc { get; set; }

        public BlueprintPerformanceCounterSnapshot Clone()
        {
            return new BlueprintPerformanceCounterSnapshot
            {
                Scenario = Scenario ?? string.Empty,
                Count = Count,
                LastElapsedMs = LastElapsedMs,
                AverageElapsedMs = AverageElapsedMs,
                SlowEventCount = SlowEventCount,
                LastReason = LastReason ?? string.Empty,
                LastRecordedUtc = LastRecordedUtc
            };
        }
    }

    internal sealed class BlueprintDiagnosticsSnapshot
    {
        public BlueprintDiagnosticsSnapshot()
        {
            TemplateReadStatus = string.Empty;
            TemplateReadMessage = string.Empty;
            AutoPlacementLastFailureReason = string.Empty;
            LastPerformanceScenario = string.Empty;
            ProjectionResolve = new BlueprintPerformanceCounterSnapshot();
            MaterialsResolve = new BlueprintPerformanceCounterSnapshot();
            AutoPlacementCandidateScan = new BlueprintPerformanceCounterSnapshot();
        }

        public string TemplateReadStatus { get; set; }
        public string TemplateReadMessage { get; set; }
        public int TemplateCount { get; set; }
        public int InstanceCount { get; set; }
        public int VisibleInstanceCount { get; set; }
        public int HiddenInstanceCount { get; set; }
        public int EffectiveProjectionLayerCount { get; set; }
        public int ErasedProjectionLayerCount { get; set; }
        public int MaterialMissingItemCount { get; set; }
        public int MaterialMissingStackTotal { get; set; }
        public bool AutoPlacementEnabled { get; set; }
        public int AutoPlacementCandidateCount { get; set; }
        public string AutoPlacementLastFailureReason { get; set; }
        public BlueprintPerformanceCounterSnapshot ProjectionResolve { get; set; }
        public BlueprintPerformanceCounterSnapshot MaterialsResolve { get; set; }
        public BlueprintPerformanceCounterSnapshot AutoPlacementCandidateScan { get; set; }
        public long SlowEventCount { get; set; }
        public string LastPerformanceScenario { get; set; }
        public double LastPerformanceElapsedMs { get; set; }
    }

    internal static class BlueprintDiagnostics
    {
        private const int TemplateCountCacheMs = 1000;
        private const string ProjectionResolveScenario = "Blueprint.Projection.Resolve";
        private const string MaterialsResolveScenario = "Blueprint.Materials.Resolve";
        private const string AutoPlacementCandidateScanScenario = "Blueprint.AutoPlacement.CandidateScan";
        private static readonly object SyncRoot = new object();
        private static BlueprintPerformanceCounterSnapshot _projectionResolve =
            CreateCounter(ProjectionResolveScenario);
        private static BlueprintPerformanceCounterSnapshot _materialsResolve =
            CreateCounter(MaterialsResolveScenario);
        private static BlueprintPerformanceCounterSnapshot _autoPlacementCandidateScan =
            CreateCounter(AutoPlacementCandidateScanScenario);
        private static DateTime _templateCountReadUtc = DateTime.MinValue;
        private static int _templateCount;
        private static string _templateReadStatus = "notRead";
        private static string _templateReadMessage = string.Empty;

        public static void RecordProjectionResolve(BlueprintProjectionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            RecordCounter(
                _projectionResolve,
                snapshot.LastResolveElapsedMs,
                PerformanceHitchRecorder.BlueprintProjectionResolveThresholdMs,
                snapshot.ResultCode,
                "instances=" + snapshot.InstanceCount.ToString(CultureInfo.InvariantCulture) +
                ";visible=" + snapshot.VisibleInstanceCount.ToString(CultureInfo.InvariantCulture) +
                ";layers=" + snapshot.EffectiveLayerCount.ToString(CultureInfo.InvariantCulture),
                "worldPairKey=" + snapshot.WorldPairKey +
                ";worldKey=" + snapshot.WorldKey +
                ";status=" + snapshot.ResultCode);
        }

        public static void RecordMaterialResolve(BlueprintMaterialSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            RecordCounter(
                _materialsResolve,
                snapshot.LastResolveElapsedMs,
                PerformanceHitchRecorder.BlueprintMaterialsResolveThresholdMs,
                snapshot.ResultCode,
                "items=" + snapshot.RequiredItemCount.ToString(CultureInfo.InvariantCulture) +
                ";missingItems=" + snapshot.MissingItemCount.ToString(CultureInfo.InvariantCulture) +
                ";missingStack=" + snapshot.MissingStackTotal.ToString(CultureInfo.InvariantCulture),
                "worldPairKey=" + snapshot.WorldPairKey +
                ";worldKey=" + snapshot.WorldKey +
                ";projectionStatus=" + snapshot.ProjectionResultCode +
                ";status=" + snapshot.ResultCode);
        }

        public static void RecordAutoPlacementCandidateScan(BlueprintAutoPlacementSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            RecordCounter(
                _autoPlacementCandidateScan,
                snapshot.LastResolveElapsedMs,
                PerformanceHitchRecorder.BlueprintAutoPlacementCandidateScanThresholdMs,
                snapshot.ResultCode,
                "enabled=" + (snapshot.Enabled ? "true" : "false") +
                ";candidates=" + snapshot.CandidateCount.ToString(CultureInfo.InvariantCulture),
                "worldPairKey=" + snapshot.WorldPairKey +
                ";worldKey=" + snapshot.WorldKey +
                ";projectionStatus=" + snapshot.ProjectionResultCode +
                ";status=" + snapshot.ResultCode);
        }

        public static BlueprintDiagnosticsSnapshot BuildSnapshot(
            BlueprintProjectionSnapshot projection,
            BlueprintMaterialSnapshot materials,
            BlueprintEraseRegionSnapshot erase,
            BlueprintAutoPlacementSnapshot autoPlacement)
        {
            lock (SyncRoot)
            {
                RefreshTemplateCountIfNeededLocked();
                var snapshot = new BlueprintDiagnosticsSnapshot
                {
                    TemplateReadStatus = _templateReadStatus ?? string.Empty,
                    TemplateReadMessage = _templateReadMessage ?? string.Empty,
                    TemplateCount = _templateCount,
                    InstanceCount = projection == null ? 0 : projection.InstanceCount,
                    VisibleInstanceCount = projection == null ? 0 : projection.VisibleInstanceCount,
                    HiddenInstanceCount = projection == null ? 0 : projection.HiddenInstanceCount,
                    EffectiveProjectionLayerCount = projection == null ? 0 : projection.EffectiveLayerCount,
                    ErasedProjectionLayerCount = projection == null ? 0 : projection.ErasedLayerCount,
                    MaterialMissingItemCount = materials == null ? 0 : materials.MissingItemCount,
                    MaterialMissingStackTotal = materials == null ? 0 : materials.MissingStackTotal,
                    AutoPlacementEnabled = autoPlacement != null && autoPlacement.Enabled,
                    AutoPlacementCandidateCount = autoPlacement == null ? 0 : autoPlacement.CandidateCount,
                    AutoPlacementLastFailureReason = ResolveAutoPlacementFailureReason(autoPlacement),
                    ProjectionResolve = _projectionResolve.Clone(),
                    MaterialsResolve = _materialsResolve.Clone(),
                    AutoPlacementCandidateScan = _autoPlacementCandidateScan.Clone()
                };
                snapshot.SlowEventCount =
                    snapshot.ProjectionResolve.SlowEventCount +
                    snapshot.MaterialsResolve.SlowEventCount +
                    snapshot.AutoPlacementCandidateScan.SlowEventCount;
                ApplyLastPerformance(snapshot, snapshot.ProjectionResolve);
                ApplyLastPerformance(snapshot, snapshot.MaterialsResolve);
                ApplyLastPerformance(snapshot, snapshot.AutoPlacementCandidateScan);
                return snapshot;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _projectionResolve = CreateCounter(ProjectionResolveScenario);
                _materialsResolve = CreateCounter(MaterialsResolveScenario);
                _autoPlacementCandidateScan = CreateCounter(AutoPlacementCandidateScanScenario);
                _templateCountReadUtc = DateTime.MinValue;
                _templateCount = 0;
                _templateReadStatus = "notRead";
                _templateReadMessage = string.Empty;
            }
        }

        private static BlueprintPerformanceCounterSnapshot CreateCounter(string scenario)
        {
            return new BlueprintPerformanceCounterSnapshot
            {
                Scenario = scenario ?? string.Empty
            };
        }

        private static void RecordCounter(
            BlueprintPerformanceCounterSnapshot counter,
            double elapsedMs,
            double thresholdMs,
            string reason,
            string ownerSummary,
            string metadata)
        {
            if (counter == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                counter.Count++;
                counter.LastElapsedMs = elapsedMs < 0 ? 0 : elapsedMs;
                counter.AverageElapsedMs += (counter.LastElapsedMs - counter.AverageElapsedMs) / counter.Count;
                counter.LastReason = reason ?? string.Empty;
                counter.LastRecordedUtc = DateTime.UtcNow;
                if (PerformanceHitchRecorder.ShouldRecordOperationFast(counter.LastElapsedMs, thresholdMs))
                {
                    counter.SlowEventCount++;
                    PerformanceHitchRecorder.RecordOperationIfNeeded(
                        counter.Scenario,
                        counter.LastElapsedMs,
                        thresholdMs,
                        reason,
                        ownerSummary,
                        metadata);
                }
            }
        }

        private static void RefreshTemplateCountIfNeededLocked()
        {
            var now = DateTime.UtcNow;
            if ((now - _templateCountReadUtc).TotalMilliseconds < TemplateCountCacheMs)
            {
                return;
            }

            _templateCountReadUtc = now;
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateLibrarySnapshot library;
                var load = store.TryLoad(out library);
                _templateReadStatus = load == null ? "unknown" : load.ResultCode ?? string.Empty;
                _templateReadMessage = load == null ? string.Empty : load.Message ?? string.Empty;
                _templateCount = load != null && load.Succeeded && library != null && library.Templates != null
                    ? library.Templates.Count
                    : 0;
            }
            catch (Exception error)
            {
                _templateReadStatus = "exception";
                _templateReadMessage = error.Message;
                _templateCount = 0;
            }
        }

        private static string ResolveAutoPlacementFailureReason(BlueprintAutoPlacementSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "snapshotMissing";
            }

            var status = snapshot.ResultCode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(status) ||
                string.Equals(status, "idle", StringComparison.Ordinal) ||
                string.Equals(status, "ready", StringComparison.Ordinal) ||
                string.Equals(status, "disabled", StringComparison.Ordinal) ||
                string.Equals(status, "submitted", StringComparison.Ordinal) ||
                string.Equals(status, "succeeded", StringComparison.Ordinal) ||
                string.Equals(status, "waitingForProjectionChange", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(snapshot.Message))
            {
                return status;
            }

            return status + ": " + snapshot.Message;
        }

        private static void ApplyLastPerformance(
            BlueprintDiagnosticsSnapshot snapshot,
            BlueprintPerformanceCounterSnapshot counter)
        {
            if (snapshot == null || counter == null || !counter.LastRecordedUtc.HasValue)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(snapshot.LastPerformanceScenario) ||
                counter.LastRecordedUtc.Value >= ResolveLastRecordedUtc(snapshot))
            {
                snapshot.LastPerformanceScenario = counter.Scenario ?? string.Empty;
                snapshot.LastPerformanceElapsedMs = counter.LastElapsedMs;
            }
        }

        private static DateTime ResolveLastRecordedUtc(BlueprintDiagnosticsSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.LastPerformanceScenario))
            {
                return DateTime.MinValue;
            }

            if (string.Equals(snapshot.LastPerformanceScenario, ProjectionResolveScenario, StringComparison.Ordinal))
            {
                return snapshot.ProjectionResolve.LastRecordedUtc ?? DateTime.MinValue;
            }

            if (string.Equals(snapshot.LastPerformanceScenario, MaterialsResolveScenario, StringComparison.Ordinal))
            {
                return snapshot.MaterialsResolve.LastRecordedUtc ?? DateTime.MinValue;
            }

            if (string.Equals(snapshot.LastPerformanceScenario, AutoPlacementCandidateScanScenario, StringComparison.Ordinal))
            {
                return snapshot.AutoPlacementCandidateScan.LastRecordedUtc ?? DateTime.MinValue;
            }

            return DateTime.MinValue;
        }
    }
}
