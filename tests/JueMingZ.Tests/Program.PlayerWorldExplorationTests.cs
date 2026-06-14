using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.Input;
using JueMingZ.Records;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FeatureCatalogExposesRevealedAreaRatio()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.MapRevealedAreaRatio, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected revealed area ratio feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented || !feature.DefaultEnabled)
            {
                throw new InvalidOperationException("Revealed area ratio must be visible, implemented, and default visible.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.MapEnhancement ||
                feature.UserCategory != FeatureUserCategory.MapEnhancement)
            {
                throw new InvalidOperationException("Revealed area ratio must stay in the map enhancement domain and category.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.None ||
                feature.RequiredActions.Count != 1 ||
                feature.RequiredActions[0] != InputActionKind.None)
            {
                throw new InvalidOperationException("Revealed area ratio must be a fixed information row without action queue requirements.");
            }
        }

        private static void PlayerWorldExplorationSmallMapCountsRevealedTiles()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = new FakeExplorationMapReader(5, 4);
                reader.Reveal(0, 0).Reveal(1, 1).Reveal(2, 2).Reveal(3, 0).Reveal(4, 3);

                var result = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 0, true);
                if (!result.ScanComplete ||
                    result.TotalTileCount != 20 ||
                    result.RevealedTileCount != 5 ||
                    Math.Abs(result.RevealedPercent - 25d) > 0.0001d)
                {
                    throw new InvalidOperationException("Revealed area ratio must count a small fake map correctly.");
                }

                var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.ExplorationSummaryFileName);
                var file = ReadJsonFile<PlayerWorldExplorationSummaryFile>(path);
                if (!file.ScanComplete ||
                    file.RevealedTileCount != 5 ||
                    file.TotalTileCount != 20 ||
                    !string.Equals(file.ScanSemantics, PlayerWorldExplorationConstants.ScanSemantics, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("exploration-summary.json must persist revealed counts and semantics.");
                }

                var read = PlayerWorldExplorationCache.ReadForPairForTesting(identity.PairId);
                AssertStringEquals(
                    LegacyMainWindow.BuildMapRevealedAreaRatioTextForTesting(read),
                    "25.00%",
                    "revealed area ratio text");
                AssertStringEquals(
                    LegacyMainWindow.GetMapRevealedAreaRatioTooltipForTesting(),
                    "点击打开详情",
                    "revealed area ratio tooltip");
            });
        }

        private static void PlayerWorldExplorationCursorResumesFromPersistedSummary()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = FakeExplorationMapReader.CreateStriped(20, 30);

                var first = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 20, 30, reader, 0, true);
                if (first.TilesScannedThisTick != PlayerWorldExplorationConstants.PerformanceScanTileCap ||
                    first.ScanComplete ||
                    first.NextTileIndex != PlayerWorldExplorationConstants.PerformanceScanTileCap)
                {
                    throw new InvalidOperationException("First revealed-area chunk must persist the scan cursor at the performance tile cap.");
                }

                PlayerWorldExplorationService.ResetForTesting();
                PlayerWorldExplorationCache.ResetForTesting();
                var second = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 20, 30, reader, 10, true);
                if (!second.ScanComplete ||
                    second.ScannedTileCount != 600 ||
                    second.NextTileIndex != 600 ||
                    second.RevealedTileCount != reader.RevealedCount)
                {
                    throw new InvalidOperationException("Revealed-area scan must resume from persisted cursor and finish the map.");
                }
            });
        }

        private static void PlayerWorldExplorationWorldSizeChangeResetsSummary()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.ExplorationSummaryFileName);
                string message;
                if (!PlayerWorldFeatureDataStore.TryWriteJsonForTesting(
                        path,
                        new PlayerWorldExplorationSummaryFile
                        {
                            PairId = identity.PairId,
                            WorldWidth = 10,
                            WorldHeight = 10,
                            TotalTileCount = 100,
                            RevealedTileCount = 80,
                            WorkingRevealedTileCount = 80,
                            ScannedTileCount = 100,
                            NextTileIndex = 100,
                            ScanComplete = true,
                            LastCompletedScanUtc = "2026-06-14T00:00:00.000Z",
                            ScanSemantics = PlayerWorldExplorationConstants.ScanSemantics
                        },
                        out message))
                {
                    throw new InvalidOperationException("Expected initial exploration summary write to succeed: " + message);
                }

                var reader = new FakeExplorationMapReader(12, 10);
                var result = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 12, 10, reader, 0, true);
                var file = ReadJsonFile<PlayerWorldExplorationSummaryFile>(path);
                if (!result.ScanComplete ||
                    file.WorldWidth != 12 ||
                    file.WorldHeight != 10 ||
                    file.TotalTileCount != 120 ||
                    file.RevealedTileCount != 0 ||
                    !string.Equals(file.LastResetReason, "worldSizeChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("World size changes must reset exploration summary instead of reusing stale counts.");
                }
            });
        }

        private static void PlayerWorldExplorationPerformanceModeUsesTimeBudgetAndBackoff()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = new FakeExplorationMapReader(200, 100);
                reader.DelayMilliseconds = 1;

                var result = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, reader, 0, false);
                if (result.TilesScannedThisTick >= PlayerWorldExplorationConstants.PerformanceScanTileCap ||
                    result.TilesScannedThisTick >= PlayerWorldExplorationConstants.ScanTileBudget ||
                    result.ScanComplete ||
                    !result.BackoffApplied ||
                    result.CurrentCadenceTicks != PlayerWorldExplorationConstants.PerformanceBackoffScanCadenceTicks ||
                    result.LastScanElapsedMs <= 0d ||
                    result.TimeBudgetMs != PlayerWorldExplorationConstants.PerformanceScanTimeBudgetMs)
                {
                    throw new InvalidOperationException("Performance revealed-area scans must stop on the time budget and back off after an over-budget slice.");
                }
            });
        }

        private static void PlayerWorldExplorationFastModeAdvancesMoreThanPerformanceMode()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var performanceReader = FakeExplorationMapReader.CreateStriped(200, 100);

                var performance = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, performanceReader, 0, false);

                ResetExplorationTestState();
                var fastReader = FakeExplorationMapReader.CreateStriped(200, 100);
                PlayerWorldExplorationService.SetMode(PlayerWorldExplorationScanModes.Fast);
                var fast = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, fastReader, 0, false);

                if (performance.TilesScannedThisTick != PlayerWorldExplorationConstants.PerformanceScanTileCap ||
                    fast.TilesScannedThisTick != PlayerWorldExplorationConstants.FastScanTileCap ||
                    fast.TilesScannedThisTick <= performance.TilesScannedThisTick ||
                    fast.CurrentCadenceTicks != PlayerWorldExplorationConstants.FastScanCadenceTicks ||
                    !string.Equals(fast.ScanMode, PlayerWorldExplorationScanModes.Fast, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Fast revealed-area mode must advance significantly more tiles than performance mode while still returning after one slice.");
                }
            });
        }

        private static void PlayerWorldExplorationFastModeCompleteStaysIdleWithoutRescan()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = FakeExplorationMapReader.CreateStriped(5, 4);
                PlayerWorldExplorationService.SetMode(PlayerWorldExplorationScanModes.Fast);

                var first = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 0, true);
                if (!first.ScanComplete || reader.ReadCount != 20)
                {
                    throw new InvalidOperationException("Expected fast revealed-area scan to complete the small map.");
                }

                var readCountAfterComplete = reader.ReadCount;
                var later = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 5000, false);
                if (!later.ScanComplete ||
                    later.TilesScannedThisTick != 0 ||
                    reader.ReadCount != readCountAfterComplete ||
                    !string.Equals(later.ControlState, PlayerWorldExplorationControlStates.IdleComplete, StringComparison.Ordinal) ||
                    !string.Equals(later.ScanMode, PlayerWorldExplorationScanModes.Fast, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Fast revealed-area scans must stay idle after completion instead of reviving automatic rescans.");
                }
            });
        }

        private static void PlayerWorldExplorationPauseStopsTileReadsInBothModes()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                AssertPauseStopsTileReads(PlayerWorldExplorationScanModes.Performance);
                AssertPauseStopsTileReads(PlayerWorldExplorationScanModes.Fast);
            });
        }

        private static void PlayerWorldExplorationCompleteStaysIdlePastLegacyRescanCadence()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = FakeExplorationMapReader.CreateStriped(5, 4);

                var first = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 0, true);
                if (!first.ScanComplete || reader.ReadCount != 20)
                {
                    throw new InvalidOperationException("Expected the small revealed-area map to complete in one scan chunk.");
                }

                var readCountAfterComplete = reader.ReadCount;
                var later = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 5000, false);
                if (!later.ScanComplete ||
                    later.TilesScannedThisTick != 0 ||
                    reader.ReadCount != readCountAfterComplete ||
                    !string.Equals(later.Status, "idleComplete", StringComparison.Ordinal) ||
                    !string.Equals(later.ControlState, PlayerWorldExplorationControlStates.IdleComplete, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Completed revealed-area scans must stay idle instead of auto-rescanning after the old cadence window.");
                }

                var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.ExplorationSummaryFileName);
                var file = ReadJsonFile<PlayerWorldExplorationSummaryFile>(path);
                if (string.Equals(file.LastResetReason, "rescan", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("exploration-summary.json must not record the removed automatic rescan reset reason.");
                }
            });
        }

        private static void PlayerWorldExplorationPauseStopsTileReadsAndStartResumesCursor()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = FakeExplorationMapReader.CreateStriped(200, 100);

                var first = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, reader, 0, true);
                if (first.NextTileIndex != PlayerWorldExplorationConstants.PerformanceScanTileCap)
                {
                    throw new InvalidOperationException("Expected the first scan chunk to leave an incomplete cursor.");
                }

                var paused = PlayerWorldExplorationService.PauseScanning();
                if (!string.Equals(paused.ControlState, PlayerWorldExplorationControlStates.PausedByUser, StringComparison.Ordinal) ||
                    !paused.HasCursor)
                {
                    throw new InvalidOperationException("PauseScanning must enter PausedByUser and keep the incomplete cursor.");
                }

                var readCountAfterPause = reader.ReadCount;
                for (var tick = 10; tick <= 40; tick += 10)
                {
                    var pausedTick = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, reader, tick, false);
                    if (!string.Equals(pausedTick.Status, "pausedByUser", StringComparison.Ordinal) ||
                        pausedTick.NextTileIndex != PlayerWorldExplorationConstants.PerformanceScanTileCap ||
                        reader.ReadCount != readCountAfterPause)
                    {
                        throw new InvalidOperationException("Paused revealed-area scans must not read map tiles or advance the cursor.");
                    }
                }

                var started = PlayerWorldExplorationService.StartScanning();
                if (!string.Equals(started.ControlState, PlayerWorldExplorationControlStates.Scanning, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("StartScanning must leave the paused state.");
                }

                var resumed = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, reader, 50, false);
                if (resumed.NextTileIndex != PlayerWorldExplorationConstants.PerformanceScanTileCap * 2L ||
                    resumed.TilesScannedThisTick != PlayerWorldExplorationConstants.PerformanceScanTileCap ||
                    reader.ReadCount != readCountAfterPause + PlayerWorldExplorationConstants.PerformanceScanTileCap)
                {
                    throw new InvalidOperationException("StartScanning must resume an incomplete cursor instead of resetting progress.");
                }
            });
        }

        private static void PlayerWorldExplorationIdleStartPerformsManualRefresh()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = FakeExplorationMapReader.CreateStriped(5, 4);

                var first = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 0, true);
                if (!first.ScanComplete)
                {
                    throw new InvalidOperationException("Expected first revealed-area scan to complete.");
                }

                var readCountAfterComplete = reader.ReadCount;
                var started = PlayerWorldExplorationService.StartScanning();
                if (!started.ManualRefreshPending)
                {
                    throw new InvalidOperationException("StartScanning from idle complete must request a manual refresh.");
                }

                var refreshed = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 10, true);
                if (!refreshed.ScanComplete ||
                    reader.ReadCount != readCountAfterComplete + 20 ||
                    !string.Equals(refreshed.ControlState, PlayerWorldExplorationControlStates.IdleComplete, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Manual refresh must scan the completed map once and return to idle complete.");
                }

                var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.ExplorationSummaryFileName);
                var file = ReadJsonFile<PlayerWorldExplorationSummaryFile>(path);
                AssertStringEquals(file.LastResetReason, "manualRefresh", "manual revealed-area refresh reset reason");
            });
        }

        private static void PlayerWorldExplorationPairChangeDoesNotReviveOldPairScan()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var firstIdentity = BuildResolvedDeathIdentity();
                var secondPairId = firstIdentity.PairId + "-other";
                var firstReader = FakeExplorationMapReader.CreateStriped(5, 4);
                var secondReader = FakeExplorationMapReader.CreateStriped(200, 100);

                var firstComplete = PlayerWorldExplorationService.ProcessScanForTesting(firstIdentity.PairId, 5, 4, firstReader, 0, true);
                if (!firstComplete.ScanComplete)
                {
                    throw new InvalidOperationException("Expected first pair to complete before switching pairs.");
                }

                var second = PlayerWorldExplorationService.ProcessScanForTesting(secondPairId, 200, 100, secondReader, 10, true);
                if (second.ScanComplete ||
                    second.NextTileIndex != PlayerWorldExplorationConstants.PerformanceScanTileCap ||
                    secondReader.ReadCount != PlayerWorldExplorationConstants.PerformanceScanTileCap)
                {
                    throw new InvalidOperationException("New pair must start its own scan context from the beginning.");
                }

                var firstReadCount = firstReader.ReadCount;
                var firstAgain = PlayerWorldExplorationService.ProcessScanForTesting(firstIdentity.PairId, 5, 4, firstReader, 20, false);
                if (!firstAgain.ScanComplete ||
                    firstAgain.TilesScannedThisTick != 0 ||
                    firstReader.ReadCount != firstReadCount ||
                    !string.Equals(firstAgain.ControlState, PlayerWorldExplorationControlStates.IdleComplete, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Returning to an old completed pair must stay idle instead of reviving its scan.");
                }
            });
        }

        private static void PlayerWorldExplorationModeSwitchDoesNotClearCompletedResult()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = FakeExplorationMapReader.CreateStriped(5, 4);

                var first = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 0, true);
                if (!first.ScanComplete)
                {
                    throw new InvalidOperationException("Expected completed revealed-area result before switching modes.");
                }

                var switched = PlayerWorldExplorationService.SetMode(PlayerWorldExplorationScanModes.Fast);
                if (!string.Equals(switched.ScanMode, PlayerWorldExplorationScanModes.Fast, StringComparison.Ordinal) ||
                    !switched.ScanComplete)
                {
                    throw new InvalidOperationException("SetMode must switch mode without clearing a completed result.");
                }

                var readCountAfterSwitch = reader.ReadCount;
                var later = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 5000, false);
                if (!later.ScanComplete ||
                    reader.ReadCount != readCountAfterSwitch ||
                    !string.Equals(later.ScanMode, PlayerWorldExplorationScanModes.Fast, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Switching exploration scan mode must not force a rescan or clear completed counts.");
                }
            });
        }

        private static void PlayerWorldExplorationLegacySummaryDefaultsToPerformanceState()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.ExplorationSummaryFileName);
                string message;
                if (!PlayerWorldFeatureDataStore.TryWriteJsonForTesting(
                    path,
                    new PlayerWorldExplorationSummaryFile
                    {
                        PairId = identity.PairId,
                        WorldWidth = 5,
                        WorldHeight = 4,
                        TotalTileCount = 20,
                        RevealedTileCount = 7,
                        WorkingRevealedTileCount = 7,
                        ScannedTileCount = 20,
                        NextTileIndex = 20,
                        ScanComplete = true,
                        LastCompletedScanUtc = "2026-06-14T00:00:00.000Z",
                        LastScannedTileBudget = PlayerWorldExplorationConstants.ScanTileBudget,
                        ScanSemantics = "mainMapIsRevealed;chunked4096tilesPer10ticks;publishedValueUsesLastCompleteScanWhenAvailable"
                    },
                    out message))
                {
                    throw new InvalidOperationException("Expected legacy exploration summary write to succeed: " + message);
                }

                var reader = FakeExplorationMapReader.CreateStriped(5, 4);
                var result = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 5000, false);
                if (!result.ScanComplete ||
                    result.RevealedTileCount != 7 ||
                    result.TilesScannedThisTick != 0 ||
                    reader.ReadCount != 0 ||
                    !string.Equals(result.ScanMode, PlayerWorldExplorationScanModes.Performance, StringComparison.Ordinal) ||
                    !string.Equals(result.ControlState, PlayerWorldExplorationControlStates.IdleComplete, StringComparison.Ordinal) ||
                    !result.AutoRescanDisabled)
                {
                    throw new InvalidOperationException("Legacy exploration summaries must default to performance idle state without reviving automatic rescans.");
                }
            });
        }

        private static void PlayerWorldExplorationDiagnosticsWrittenToSnapshot()
        {
            var snapshot = new DiagnosticSnapshot
            {
                PlayerWorldExplorationLastStatus = "scanning",
                PlayerWorldExplorationLastMessage = "scan in progress",
                PlayerWorldExplorationLastPairId = "pair-exploration",
                PlayerWorldExplorationWorldWidth = 100,
                PlayerWorldExplorationWorldHeight = 50,
                PlayerWorldExplorationTotalTileCount = 5000,
                PlayerWorldExplorationRevealedTileCount = 2048,
                PlayerWorldExplorationWorkingRevealedTileCount = 2100,
                PlayerWorldExplorationScannedTileCount = 4096,
                PlayerWorldExplorationNextTileIndex = 4096,
                PlayerWorldExplorationLastScannedTileBudget = 4096,
                PlayerWorldExplorationScanMode = PlayerWorldExplorationScanModes.Performance,
                PlayerWorldExplorationControlState = PlayerWorldExplorationControlStates.Scanning,
                PlayerWorldExplorationPausedByUser = false,
                PlayerWorldExplorationIdleComplete = false,
                PlayerWorldExplorationLastScanElapsedMs = 0.34d,
                PlayerWorldExplorationLastScanTileCount = 512,
                PlayerWorldExplorationCurrentTimeBudgetMs = PlayerWorldExplorationConstants.PerformanceScanTimeBudgetMs,
                PlayerWorldExplorationCurrentCadenceTicks = PlayerWorldExplorationConstants.PerformanceBackoffScanCadenceTicks,
                PlayerWorldExplorationBackoffApplied = true,
                PlayerWorldExplorationLastUserCommand = "setMode:Performance",
                PlayerWorldExplorationAutoRescanDisabled = true,
                PlayerWorldExplorationRevealedPercent = 40.96d,
                PlayerWorldExplorationScanComplete = false,
                PlayerWorldExplorationReadFailed = true,
                PlayerWorldExplorationWriteFailed = true,
                PlayerWorldExplorationLastScanUtc = new DateTime(2026, 6, 14, 6, 7, 8, DateTimeKind.Utc),
                PlayerWorldExplorationLastCompletedScanUtc = new DateTime(2026, 6, 14, 6, 8, 9, DateTimeKind.Utc),
                PlayerWorldExplorationLastWriteUtc = new DateTime(2026, 6, 14, 6, 9, 10, DateTimeKind.Utc)
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"PlayerWorldExplorationLastStatus\": \"scanning\"");
            AssertContains(json, "\"PlayerWorldExplorationLastPairId\": \"pair-exploration\"");
            AssertContains(json, "\"PlayerWorldExplorationWorldWidth\": 100");
            AssertContains(json, "\"PlayerWorldExplorationTotalTileCount\": 5000");
            AssertContains(json, "\"PlayerWorldExplorationRevealedTileCount\": 2048");
            AssertContains(json, "\"PlayerWorldExplorationWorkingRevealedTileCount\": 2100");
            AssertContains(json, "\"PlayerWorldExplorationScannedTileCount\": 4096");
            AssertContains(json, "\"PlayerWorldExplorationLastScannedTileBudget\": 4096");
            AssertContains(json, "\"PlayerWorldExplorationScanMode\": \"Performance\"");
            AssertContains(json, "\"PlayerWorldExplorationControlState\": \"Scanning\"");
            AssertContains(json, "\"PlayerWorldExplorationPausedByUser\": false");
            AssertContains(json, "\"PlayerWorldExplorationIdleComplete\": false");
            AssertContains(json, "\"PlayerWorldExplorationLastScanElapsedMs\": 0.34");
            AssertContains(json, "\"PlayerWorldExplorationLastScanTileCount\": 512");
            AssertContains(json, "\"PlayerWorldExplorationCurrentTimeBudgetMs\": 0.35");
            AssertContains(json, "\"PlayerWorldExplorationCurrentCadenceTicks\": 90");
            AssertContains(json, "\"PlayerWorldExplorationBackoffApplied\": true");
            AssertContains(json, "\"PlayerWorldExplorationLastUserCommand\": \"setMode:Performance\"");
            AssertContains(json, "\"PlayerWorldExplorationAutoRescanDisabled\": true");
            AssertContains(json, "\"PlayerWorldExplorationRevealedPercent\": 40.96");
            AssertContains(json, "\"PlayerWorldExplorationScanComplete\": false");
            AssertContains(json, "\"PlayerWorldExplorationReadFailed\": true");
            AssertContains(json, "\"PlayerWorldExplorationWriteFailed\": true");
            AssertContains(json, "\"PlayerWorldExplorationLastScanUtc\": \"2026-06-14T06:07:08.0000000Z\"");
            AssertContains(json, "\"PlayerWorldExplorationLastCompletedScanUtc\": \"2026-06-14T06:08:09.0000000Z\"");
            AssertContains(json, "\"PlayerWorldExplorationLastWriteUtc\": \"2026-06-14T06:09:10.0000000Z\"");
        }

        private static void LegacyMapRevealedAreaTooltipAndDetailsLines()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = new FakeExplorationMapReader(5, 4);
                reader.Reveal(0, 0).Reveal(1, 1).Reveal(2, 2).Reveal(3, 0).Reveal(4, 3);

                PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 5, 4, reader, 0, true);
                var read = PlayerWorldExplorationCache.ReadForPairForTesting(identity.PairId);
                var tooltip = LegacyMainWindow.BuildMapRevealedAreaRatioTooltipLinesForTesting(read);
                if (tooltip == null || tooltip.Length != 1 || !string.Equals(tooltip[0], "点击打开详情", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Revealed-area ratio hover tooltip must only invite the details click.");
                }

                var lines = LegacyMainWindow.BuildMapRevealedAreaDetailsLinesForTesting(read);
                if (lines == null ||
                    lines.Length != 3 ||
                    !string.Equals(lines[0], "当前玩家-世界地图揭示区域占比", StringComparison.Ordinal) ||
                    !string.Equals(lines[1], "已揭示 5 / 20", StringComparison.Ordinal) ||
                    lines[2].IndexOf("上次统计", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Revealed-area details popup must preserve the former three-line tooltip meaning.");
                }

                var identityFailed = new PlayerWorldExplorationReadResult
                {
                    IdentityResolved = false,
                    Status = "identityUnavailable"
                };
                if (LegacyMainWindow.IsMapRevealedAreaDetailsStartEnabledForTesting(identityFailed) ||
                    !string.Equals(LegacyMainWindow.GetMapRevealedAreaDetailsControlButtonTextForTesting(identityFailed), "开始扫描", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Revealed-area details must disable start when identity is unavailable.");
                }
            });
        }

        private static void LegacyMapRevealedAreaDetailsPopupRegistersAsModal()
        {
            PlayerWorldExplorationCache.ResetForTesting();
            LegacyMainWindow.ResetMapRevealedAreaDetailsPopupForTesting();
            LegacyUiOverlayCoordinator.Current.ResetForTesting();

            var exploration = new PlayerWorldExplorationReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "memory",
                PairId = "pair-ui",
                TotalTileCount = 100,
                RevealedTileCount = 25,
                ScannedTileCount = 60,
                ScanMode = PlayerWorldExplorationScanModes.Performance,
                ControlState = PlayerWorldExplorationControlStates.Scanning,
                ScanComplete = false
            };

            LegacyMainWindow.ToggleMapRevealedAreaDetailsPopup();
            if (!LegacyMainWindow.IsMapRevealedAreaDetailsPopupOpen())
            {
                throw new InvalidOperationException("Revealed-area details popup must open by toggle.");
            }

            var coordinator = LegacyUiOverlayCoordinator.Current;
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("page:button", "Page", "button", new LegacyUiRect(20, 30, 120, 80))
            };
            var area = LegacyScrollArea.Create(new LegacyUiRect(20, 30, 520, 220), LegacyMainWindow.CalculateMapEnhancementContentHeightForTesting(), 0);
            coordinator.BeginFrame("map_enhancement");
            if (!LegacyMainWindow.RegisterMapRevealedAreaDetailsPopupOverlayForTesting(area, new LegacyUiRect(area.Viewport.Right - 90, area.Viewport.Y + 40, 82, 24), exploration))
            {
                throw new InvalidOperationException("Revealed-area details popup must register through the overlay coordinator.");
            }

            var childMouse = new LegacyMouseSnapshot
            {
                X = area.Viewport.Right - 108,
                Y = area.Viewport.Bottom - 32,
                LeftPressed = true,
                ReadAvailable = true
            };
            coordinator.DrawOverlays(null, childMouse, new LegacyUiRect(0, 0, 640, 360), "map_enhancement", AppSettings.CreateDefault(), elements);
            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, childMouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, childMouse, out blocked);
            coordinator.EndFrame();

            if (coordinator.LastStackSignature == 0)
            {
                throw new InvalidOperationException("Revealed-area details modal must contribute to the overlay stack signature.");
            }

            if (hovered == null || !string.Equals(hovered.Id, "map-revealed-area-ratio:pause", StringComparison.Ordinal) ||
                blocked || !string.Equals(clickId, "map-revealed-area-ratio:pause", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Revealed-area details modal child buttons must win hit-test over the modal blocker.");
            }

            var lowerMouse = new LegacyMouseSnapshot
            {
                X = area.Viewport.X + 30,
                Y = area.Viewport.Y + 20,
                LeftPressed = true,
                ReadAvailable = true
            };
            blocked = false;
            clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, lowerMouse, out blocked);
            if (!blocked || clickId.Length != 0)
            {
                throw new InvalidOperationException("Revealed-area details modal must block lower page clicks inside popup bounds.");
            }

            LegacyMainWindow.CloseMapRevealedAreaDetailsPopup();
            if (LegacyMainWindow.IsMapRevealedAreaDetailsPopupOpen())
            {
                throw new InvalidOperationException("Revealed-area details popup must close cleanly.");
            }

            coordinator.ResetForTesting();
        }

        private static void LegacyMapRevealedAreaCommandsDrivePopupAndScanControl()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                ResetLegacyUiMapRevealedAreaCommandState();

                EnqueueLegacyMapRevealedAreaCommandForTesting("map-revealed-area-ratio:toggle");
                LegacyUiActionService.Update(new InputActionQueue(), null);
                if (!LegacyMainWindow.IsMapRevealedAreaDetailsPopupOpen())
                {
                    throw new InvalidOperationException("map-revealed-area-ratio:toggle must open the details popup.");
                }

                EnqueueLegacyMapRevealedAreaCommandForTesting("map-revealed-area-ratio:toggle");
                LegacyUiActionService.Update(new InputActionQueue(), null);
                if (LegacyMainWindow.IsMapRevealedAreaDetailsPopupOpen())
                {
                    throw new InvalidOperationException("map-revealed-area-ratio:toggle must close an already open details popup.");
                }

                EnqueueLegacyMapRevealedAreaCommandForTesting("map-revealed-area-ratio:mode:fast");
                LegacyUiActionService.Update(new InputActionQueue(), null);
                var fast = PlayerWorldExplorationService.GetControlSnapshot();
                if (fast == null || !string.Equals(fast.ScanMode, PlayerWorldExplorationScanModes.Fast, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("map-revealed-area-ratio:mode:fast must switch the scan service to Fast mode.");
                }

                EnqueueLegacyMapRevealedAreaCommandForTesting("map-revealed-area-ratio:pause");
                LegacyUiActionService.Update(new InputActionQueue(), null);
                var paused = PlayerWorldExplorationService.GetControlSnapshot();
                if (paused == null || !string.Equals(paused.ControlState, PlayerWorldExplorationControlStates.PausedByUser, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("map-revealed-area-ratio:pause must pause the scan service.");
                }

                EnqueueLegacyMapRevealedAreaCommandForTesting("map-revealed-area-ratio:start");
                LegacyUiActionService.Update(new InputActionQueue(), null);
                var started = PlayerWorldExplorationService.GetControlSnapshot();
                if (started == null ||
                    !string.Equals(started.ControlState, PlayerWorldExplorationControlStates.Scanning, StringComparison.Ordinal) ||
                    !string.Equals(started.LastUserCommand, "start", StringComparison.Ordinal) ||
                    LegacyUiActionService.DispatchedCommandCountLast != 1)
                {
                    throw new InvalidOperationException("map-revealed-area-ratio:start must start the scan service through the Legacy UI command path.");
                }
            });
        }

        private static void LegacyMapRevealedAreaLayoutTracksPopupAndScanState()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                LegacyMainWindow.ResetMapRevealedAreaDetailsPopupForTesting();
                LegacyMainWindow.ResetPageLayoutCacheForTesting();
                var settings = AppSettings.CreateDefault();
                var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
                var content = new LegacyUiRect(58, 134, 520, 200);

                var first = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("map_enhancement", window, content, 0, settings);
                LegacyMainWindow.ToggleMapRevealedAreaDetailsPopup();
                var popupChanged = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("map_enhancement", window, content, 0, settings);
                if (popupChanged.PageStateSignature == first.PageStateSignature ||
                    popupChanged.RebuildCount <= first.RebuildCount)
                {
                    throw new InvalidOperationException("Map enhancement layout signature must include revealed-area details popup state.");
                }

                var identity = BuildResolvedDeathIdentity();
                var reader = FakeExplorationMapReader.CreateStriped(200, 100);
                PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, reader, 0, true);
                PlayerWorldExplorationCache.ReadForPairForTesting(identity.PairId);
                var scanChanged = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("map_enhancement", window, content, 0, settings);
                if (scanChanged.PageStateSignature == popupChanged.PageStateSignature ||
                    scanChanged.RebuildCount <= popupChanged.RebuildCount)
                {
                    throw new InvalidOperationException("Map enhancement layout signature must include revealed-area exploration cache and control state.");
                }
            });
        }

        private static void AssertPauseStopsTileReads(string mode)
        {
            ResetExplorationTestState();
            var identity = BuildResolvedDeathIdentity();
            var reader = FakeExplorationMapReader.CreateStriped(200, 100);
            PlayerWorldExplorationService.SetMode(mode);

            var first = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, reader, 0, true);
            if (first.ScanComplete || first.TilesScannedThisTick <= 0)
            {
                throw new InvalidOperationException("Expected revealed-area scan to create an incomplete cursor before pause.");
            }

            PlayerWorldExplorationService.PauseScanning();
            var readCountAfterPause = reader.ReadCount;
            var paused = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, reader, 10, false);
            if (!string.Equals(paused.Status, "pausedByUser", StringComparison.Ordinal) ||
                paused.TilesScannedThisTick != 0 ||
                reader.ReadCount != readCountAfterPause ||
                !string.Equals(paused.ScanMode, PlayerWorldExplorationScanModes.Normalize(mode), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Paused revealed-area scans must not read map tiles in " + mode + " mode.");
            }
        }

        private static void ResetExplorationTestState()
        {
            PlayerWorldExplorationService.ResetForTesting();
            PlayerWorldExplorationCache.ResetForTesting();
            PlayerWorldExplorationDiagnostics.ResetForTesting();
        }

        private static void ResetLegacyUiMapRevealedAreaCommandState()
        {
            LegacyMainWindow.ResetMapRevealedAreaDetailsPopupForTesting();
            LegacyUiInput.ResetInteractionState();
            LegacyUiInput.ResetActionUpdateGateStateForTesting();
            LegacyUiActionService.ResetActionUpdateDiagnosticsForTesting();
            LegacyUiCommand drained;
            while (LegacyUiInput.TryDrainCommand(out drained))
            {
            }
        }

        private static void EnqueueLegacyMapRevealedAreaCommandForTesting(string elementId)
        {
            LegacyUiInput.EnqueueClick(
                new LegacyUiElement
                {
                    Id = elementId,
                    Label = "揭示区域",
                    Kind = "button",
                    Rect = new LegacyUiRect(8, 8, 96, 24),
                    Enabled = true
                },
                new LegacyMouseSnapshot
                {
                    X = 16,
                    Y = 16,
                    LeftDown = true,
                    LeftPressed = true,
                    ReadAvailable = true,
                    WindowHit = true
                },
                true);
        }

        private sealed class FakeExplorationMapReader : IPlayerWorldExplorationMapReader
        {
            private readonly HashSet<int> _revealed = new HashSet<int>();

            public FakeExplorationMapReader(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; private set; }
            public int Height { get; private set; }
            public int RevealedCount { get { return _revealed.Count; } }
            public int ReadCount { get; private set; }
            public int DelayMilliseconds { get; set; }

            public static FakeExplorationMapReader CreateStriped(int width, int height)
            {
                var reader = new FakeExplorationMapReader(width, height);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        if ((x + y) % 3 == 0)
                        {
                            reader.Reveal(x, y);
                        }
                    }
                }

                return reader;
            }

            public FakeExplorationMapReader Reveal(int x, int y)
            {
                if (x >= 0 && y >= 0 && x < Width && y < Height)
                {
                    _revealed.Add(ToIndex(x, y));
                }

                return this;
            }

            public bool TryReadDimensions(out int width, out int height, out string message)
            {
                width = Width;
                height = Height;
                message = "ok";
                return width > 0 && height > 0;
            }

            public bool TryIsRevealed(int x, int y, out bool revealed, out string message)
            {
                if (DelayMilliseconds > 0)
                {
                    Thread.Sleep(DelayMilliseconds);
                }

                ReadCount++;
                if (x < 0 || y < 0 || x >= Width || y >= Height)
                {
                    revealed = false;
                    message = "coordinateOutOfRange";
                    return false;
                }

                revealed = _revealed.Contains(ToIndex(x, y));
                message = "ok";
                return true;
            }

            private int ToIndex(int x, int y)
            {
                return y * Width + x;
            }
        }
    }
}
