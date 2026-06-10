using System;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Runtime
{
    internal static partial class RuntimeDiagnosticSnapshotBuilder
    {
        private static void WritePerformanceAndConfig(DiagnosticSnapshot snapshot, RuntimeDiagnosticSnapshotSource source)
        {
            var configSave = ConfigService.LastSaveSummary;
            var configAppSave = configSave == null ? null : configSave.AppSettings;
            var configFeatureSave = configSave == null ? null : configSave.FeatureSettings;
            var configHotkeySave = configSave == null ? null : configSave.HotkeySettings;

            snapshot.RuntimeUpdateCount = RuntimePerformanceDiagnostics.RuntimeUpdateCount;
            snapshot.AverageRuntimeUpdateMs = RuntimePerformanceDiagnostics.AverageRuntimeUpdateMs;
            snapshot.LastRuntimeUpdateMs = RuntimePerformanceDiagnostics.LastRuntimeUpdateMs;
            snapshot.LastUpdateStartGapMs = RuntimePerformanceDiagnostics.LastUpdateStartGapMs;
            snapshot.LastGameStateReadMs = RuntimePerformanceDiagnostics.LastGameStateReadMs;
            snapshot.LastActionQueueUpdateMs = RuntimePerformanceDiagnostics.LastActionQueueUpdateMs;
            snapshot.LastInputActionUpdateMs = RuntimePerformanceDiagnostics.LastInputActionUpdateMs;
            snapshot.LastInformationDrawMs = RuntimePerformanceDiagnostics.LastInformationDrawMs;
            snapshot.RecentPerformanceWindowCapacitySamples = RuntimePerformanceDiagnostics.RecentWindowCapacitySamples;
            snapshot.RecentPerformanceWindowSampleCount = RuntimePerformanceDiagnostics.RecentWindowSampleCount;
            snapshot.RecentRuntimeUpdateAverageMs = RuntimePerformanceDiagnostics.RecentRuntimeUpdateAverageMs;
            snapshot.RecentGameStateReadAverageMs = RuntimePerformanceDiagnostics.RecentGameStateReadAverageMs;
            snapshot.RecentActionQueueUpdateAverageMs = RuntimePerformanceDiagnostics.RecentActionQueueUpdateAverageMs;
            snapshot.RecentInputActionUpdateAverageMs = RuntimePerformanceDiagnostics.RecentInputActionUpdateAverageMs;
            snapshot.RecentInformationDrawAverageMs = RuntimePerformanceDiagnostics.RecentInformationDrawAverageMs;
            snapshot.UiTextFastPathHitCount = UiTextRenderer.AnchorFreeFastPathHitCount;
            snapshot.UiTextFallbackCount = UiTextRenderer.AnchorFreeFastPathFallbackCount;
            snapshot.LastSlowestStageName = RuntimePerformanceDiagnostics.LastSlowestStageName;
            snapshot.LastSlowestStageElapsedMs = RuntimePerformanceDiagnostics.LastSlowestStageElapsedMs;
            snapshot.LastSlowestOperationName = RuntimePerformanceDiagnostics.LastSlowestOperationName;
            snapshot.LastSlowestOperationElapsedMs = RuntimePerformanceDiagnostics.LastSlowestOperationElapsedMs;
            snapshot.PerformanceEventsPath = string.IsNullOrWhiteSpace(RuntimePerformanceDiagnostics.PerformanceEventsPath)
                ? PerformanceHitchRecorder.PerformanceEventsPath
                : RuntimePerformanceDiagnostics.PerformanceEventsPath;
            snapshot.PerformanceHitchCount = RuntimePerformanceDiagnostics.PerformanceHitchCount;
            snapshot.LastPerformanceHitchUtc = RuntimePerformanceDiagnostics.LastPerformanceHitchUtc;
            snapshot.LastPerformanceHitchReason = RuntimePerformanceDiagnostics.LastPerformanceHitchReason;
            snapshot.LastPerformanceHitchUpdateGapMs = RuntimePerformanceDiagnostics.LastPerformanceHitchUpdateGapMs;
            snapshot.LastPerformanceHitchRuntimeUpdateMs = RuntimePerformanceDiagnostics.LastPerformanceHitchRuntimeUpdateMs;
            snapshot.LastPerformanceHitchGameStateReadMs = RuntimePerformanceDiagnostics.LastPerformanceHitchGameStateReadMs;
            snapshot.LastPerformanceHitchActionQueueUpdateMs = RuntimePerformanceDiagnostics.LastPerformanceHitchActionQueueUpdateMs;
            snapshot.LastPerformanceHitchInputActionUpdateMs = RuntimePerformanceDiagnostics.LastPerformanceHitchInputActionUpdateMs;
            snapshot.LastPerformanceHitchInformationDrawMs = RuntimePerformanceDiagnostics.LastPerformanceHitchInformationDrawMs;
            snapshot.LastPerformanceHitchSlowestStageName = RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestStageName;
            snapshot.LastPerformanceHitchSlowestStageMs = RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestStageMs;
            snapshot.LastPerformanceHitchSlowestOperationName = RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestOperationName;
            snapshot.LastPerformanceHitchSlowestOperationMs = RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestOperationMs;
            snapshot.PerformanceOperationEventCount = RuntimePerformanceDiagnostics.PerformanceOperationEventCount;
            snapshot.LastPerformanceOperationScenario = RuntimePerformanceDiagnostics.LastPerformanceOperationScenario;
            snapshot.LastPerformanceOperationUtc = RuntimePerformanceDiagnostics.LastPerformanceOperationUtc;
            snapshot.LastPerformanceOperationElapsedMs = RuntimePerformanceDiagnostics.LastPerformanceOperationElapsedMs;
            snapshot.LastPerformanceOperationThresholdMs = RuntimePerformanceDiagnostics.LastPerformanceOperationThresholdMs;
            snapshot.LastPerformanceOperationReason = RuntimePerformanceDiagnostics.LastPerformanceOperationReason;
            snapshot.LastPerformanceOperationOwnerSummary = RuntimePerformanceDiagnostics.LastPerformanceOperationOwnerSummary;
            snapshot.ReflectionCacheReady = TerrariaMemberCache.IsInitialized;
            snapshot.ReflectionCacheMissCount = TerrariaMemberCache.CacheMissCount;
            snapshot.ReflectionCacheLastMissKey = TerrariaMemberCache.LastMissKey;
            snapshot.ReflectionCacheLastMissUtc = TerrariaMemberCache.LastMissUtc;
            snapshot.ReflectionCacheLastError = TerrariaMemberCache.LastError;
            snapshot.InputCompatReady = TerrariaInputCompat.InputCompatReady;
            snapshot.SelectedItemGetterReady = TerrariaInputCompat.SelectedItemGetterReady;
            snapshot.SelectedItemSelectorReady = TerrariaInputCompat.SelectedItemSelectorReady;
            snapshot.SelectedItemAccessorReady = TerrariaInputCompat.SelectedItemAccessorReady;
            snapshot.PlayerTypeName = TerrariaInputCompat.PlayerTypeName;
            snapshot.LastInputCompatError = TerrariaInputCompat.LastInputCompatError;
            snapshot.ConfigLastSaveUtc = configSave == null ? (DateTime?)null : configSave.Utc;
            snapshot.ConfigLastSaveSucceeded = configSave != null && configSave.Succeeded;
            snapshot.ConfigLastSaveSummary = configSave == null ? string.Empty : configSave.Summary;
            snapshot.ConfigLastSaveAppSettingsSucceeded = configAppSave != null && configAppSave.Succeeded;
            snapshot.ConfigLastSaveAppSettingsPath = configAppSave == null ? string.Empty : configAppSave.Path;
            snapshot.ConfigLastSaveAppSettingsError = configAppSave == null ? string.Empty : configAppSave.Error;
            snapshot.ConfigLastSaveFeatureSettingsSucceeded = configFeatureSave != null && configFeatureSave.Succeeded;
            snapshot.ConfigLastSaveFeatureSettingsPath = configFeatureSave == null ? string.Empty : configFeatureSave.Path;
            snapshot.ConfigLastSaveFeatureSettingsError = configFeatureSave == null ? string.Empty : configFeatureSave.Error;
            snapshot.ConfigLastSaveHotkeySettingsSucceeded = configHotkeySave != null && configHotkeySave.Succeeded;
            snapshot.ConfigLastSaveHotkeySettingsPath = configHotkeySave == null ? string.Empty : configHotkeySave.Path;
            snapshot.ConfigLastSaveHotkeySettingsError = configHotkeySave == null ? string.Empty : configHotkeySave.Error;
        }
    }
}
