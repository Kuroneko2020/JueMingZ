using JueMingZ.Config;

namespace JueMingZ.Automation.Information
{
    internal static class InformationOverlayDiagnosticsWriter
    {
        private static readonly object SyncRoot = new object();
        private static InformationOverlayDiagnostics LastFrame = new InformationOverlayDiagnostics();

        internal static InformationOverlayDiagnostics GetSnapshot()
        {
            lock (SyncRoot)
            {
                var fishingCatchDiagnostics = InformationFishingCatchDiagnostics.ReadSnapshot();
                // Snapshot consumers read existing counters only; diagnostics
                // must not refresh labels, scan tiles, or rebuild layouts.
                return new InformationOverlayDiagnostics
                {
                    EnabledSummary = LastFrame.EnabledSummary,
                    NpcLabelsDrawn = LastFrame.NpcLabelsDrawn,
                    ChestLabelsDrawn = LastFrame.ChestLabelsDrawn,
                    SignTextLabelsDrawn = LastFrame.SignTextLabelsDrawn,
                    TombstoneTextLabelsDrawn = LastFrame.TombstoneTextLabelsDrawn,
                    TileHighlightsDrawn = LastFrame.TileHighlightsDrawn,
                    StatusLinesDrawn = LastFrame.StatusLinesDrawn,
                    LastDrawElapsedMs = LastFrame.LastDrawElapsedMs,
                    LastSkipReason = LastFrame.LastSkipReason,
                    SignTextLayoutCacheHitCount = InformationSignTextLayoutCache.HitCount,
                    SignTextLayoutCacheMissCount = InformationSignTextLayoutCache.MissCount,
                    WorldLabelSnapshotRefreshCount = InformationNpcLabelService.WorldLabelSnapshotRefreshCount +
                                                     InformationChestLabelService.SnapshotRefreshCount,
                    NpcLabelSnapshotRefreshCount = InformationNpcLabelService.NpcLabelSnapshotRefreshCount,
                    ChestLabelSnapshotRefreshCount = InformationChestLabelService.SnapshotRefreshCount,
                    ChestLabelSortRefreshCount = InformationChestLabelService.SortRefreshCount,
                    ChestAlwaysScanCacheHitCount = InformationChestLabelService.AlwaysScanCacheHitCount,
                    ChestAlwaysScanCacheMissCount = InformationChestLabelService.AlwaysScanCacheMissCount,
                    ChestAlwaysLastDirtyReason = InformationChestLabelService.AlwaysLastDirtyReason,
                    ChestAlwaysSafeRefreshCount = InformationChestLabelService.AlwaysSafeRefreshCount,
                    ChestAlwaysTilesVisitedLast = InformationChestLabelService.AlwaysTilesVisitedLast,
                    ChestAlwaysTypedTileFastPathStatus = InformationChestLabelService.AlwaysTypedTileFastPathStatus,
                    ChestAlwaysNameCacheHitCount = InformationChestLabelService.AlwaysNameCacheHitCount,
                    ChestAlwaysNameCacheMissCount = InformationChestLabelService.AlwaysNameCacheMissCount,
                    ChestAlwaysPartialScanFrameCount = InformationChestLabelService.AlwaysPartialScanFrameCount,
                    ChestAlwaysPartialScanPendingCount = InformationChestLabelService.AlwaysPartialScanPendingCount,
                    ChestAlwaysStableSnapshotId = InformationChestLabelService.AlwaysStableSnapshotId,
                    WorldContextCacheHitCount = InformationWorldContextProvider.CacheHitCount,
                    WorldContextCacheMissCount = InformationWorldContextProvider.CacheMissCount,
                    WorldContextProfile = InformationWorldContextProvider.LastProfile,
                    WorldContextFileDataRefreshCount = InformationWorldContextProvider.FileDataRefreshCount,
                    StatusLineCacheHitCount = InformationStatusLineService.CacheHitCount,
                    StatusLineCacheMissCount = InformationStatusLineService.CacheMissCount,
                    FishingCatchEarlyCacheHitCount = fishingCatchDiagnostics.EarlyCacheHitCount,
                    FishingCatchEarlyCacheMissCount = fishingCatchDiagnostics.EarlyCacheMissCount,
                    FishingWaterScanCount = fishingCatchDiagnostics.WaterScanCount,
                    FishingConditionsReadCount = fishingCatchDiagnostics.ConditionsReadCount,
                    FishingBobberObserverFreshInactiveSkipCount = InformationBobberLocator.ObserverFreshInactiveSkipCount,
                    FishingProjectileFallbackScanCount = InformationBobberLocator.ProjectileFallbackScanCount
                };
            }
        }

        internal static double GetLastDrawElapsedMs()
        {
            lock (SyncRoot)
            {
                return LastFrame.LastDrawElapsedMs;
            }
        }

        internal static void UpdateWorldOverlay(
            int npcLabels,
            int chestLabels,
            int signTextLabels,
            int tombstoneTextLabels,
            int tileHighlights,
            double elapsedMs,
            string skipReason)
        {
            lock (SyncRoot)
            {
                UpdateEnabledSummary();
                LastFrame.NpcLabelsDrawn = npcLabels;
                LastFrame.ChestLabelsDrawn = chestLabels;
                LastFrame.SignTextLabelsDrawn = signTextLabels;
                LastFrame.TombstoneTextLabelsDrawn = tombstoneTextLabels;
                LastFrame.TileHighlightsDrawn = tileHighlights;
                RecordDraw(elapsedMs, skipReason);
            }
        }

        internal static void UpdateStatusPanel(int statusLines, double elapsedMs, string skipReason)
        {
            lock (SyncRoot)
            {
                UpdateEnabledSummary();
                LastFrame.StatusLinesDrawn = statusLines;
                RecordDraw(elapsedMs, skipReason);
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                LastFrame = new InformationOverlayDiagnostics();
            }
        }

        private static void UpdateEnabledSummary()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            LastFrame.EnabledSummary = InformationStatusSummaryBuilder.BuildEnabledSummary(settings);
        }

        private static void RecordDraw(double elapsedMs, string skipReason)
        {
            LastFrame.LastDrawElapsedMs = elapsedMs;
            LastFrame.LastSkipReason = skipReason ?? string.Empty;
        }
    }
}
