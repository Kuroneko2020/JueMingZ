using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Records;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void InformationChestLabelsCacheSignatureChangesWithModeAndKnownKeys()
        {
            WithTemporaryBehaviorStore(() =>
            {
                var context = CreateInformationChestRecordContext("player-a", "world-a", "same-world#42");
                var settings = AppSettings.CreateDefault();

                bool added;
                string message;
                if (!PlayerWorldBehaviorStore.TryRecordOpenedChest(
                        new PlayerWorldBehaviorContext { PlayerKey = context.PlayerRecordKey, WorldKey = context.WorldRecordKey },
                        10,
                        20,
                        "test",
                        out added,
                        out message) ||
                    !added)
                {
                    throw new InvalidOperationException("Expected first opened chest record to be stored: " + message);
                }

                var opened = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(context, settings, "Opened");
                var always = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(context, settings, "Always");
                if (string.Equals(opened, always, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected chest label cache signature to include display mode.");
                }

                if (!PlayerWorldBehaviorStore.TryRecordOpenedChest(
                        new PlayerWorldBehaviorContext { PlayerKey = context.PlayerRecordKey, WorldKey = context.WorldRecordKey },
                        12,
                        20,
                        "test",
                        out added,
                        out message) ||
                    !added)
                {
                    throw new InvalidOperationException("Expected second opened chest record to be stored: " + message);
                }

                var openedAfterKnownChest = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(context, settings, "Opened");
                if (string.Equals(opened, openedAfterKnownChest, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected chest label cache signature to change when opened chest records change.");
                }
            });
        }

        private static void InformationChestAlwaysDirtyCacheTracksMovementWorldAndStyle()
        {
            WithTemporaryBehaviorStore(() =>
            {
                var settings = AppSettings.CreateDefault();
                var context = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1400f, 2300f, "world#42", "world-a", "player-a", 100);
                var smallMove = CreateInformationChestCacheContext(1055f, 2048f, 800, 600, 1406f, 2300f, "world#42", "world-a", "player-a", 101);

                var signature = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(context, settings, "Always");
                var smallMoveSignature = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(smallMove, settings, "Always");
                if (!string.Equals(signature, smallMoveSignature, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected Always chest cache signature to survive movement within the same chunk.");
                }

                string reason;
                if (InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, smallMove, settings, "Always", out reason) ||
                    !string.Equals(reason, "cacheHit", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected Always chest cache to hit for small movement, got " + reason + ".");
                }

                var screenChunkMove = CreateInformationChestCacheContext(1088f, 2048f, 800, 600, 1406f, 2300f, "world#42", "world-a", "player-a", 101);
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, screenChunkMove, settings, "Always", out reason) ||
                    !string.Equals(reason, "screenChunkChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected screen chunk movement to dirty Always chest cache, got " + reason + ".");
                }

                var playerChunkMove = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1408f, 2300f, "world#42", "world-a", "player-a", 101);
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, playerChunkMove, settings, "Always", out reason) ||
                    !string.Equals(reason, "playerChunkChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected player chunk movement to dirty Always chest cache, got " + reason + ".");
                }

                var resized = CreateInformationChestCacheContext(1024f, 2048f, 801, 600, 1400f, 2300f, "world#42", "world-a", "player-a", 101);
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, resized, settings, "Always", out reason) ||
                    !string.Equals(reason, "screenSizeChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected screen size changes to dirty Always chest cache, got " + reason + ".");
                }

                var otherWorld = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1400f, 2300f, "world#43", "world-b", "player-a", 101);
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, otherWorld, settings, "Always", out reason) ||
                    !string.Equals(reason, "worldChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected world changes to dirty Always chest cache, got " + reason + ".");
                }

                var styled = AppSettings.CreateDefault();
                styled.InformationChestNameFontScale = 0.81d;
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, context, styled, "Always", out reason) ||
                    !string.Equals(reason, "styleChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected chest label style changes to dirty Always chest cache, got " + reason + ".");
                }
            });
        }

        private static void InformationChestAlwaysDirtyCacheKeepsSafeRefresh()
        {
            var settings = AppSettings.CreateDefault();
            var context = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1400f, 2300f, "world#42", "world-a", "player-a", 100);

            string reason;
            if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(0, context, settings, "Always", 100, context, settings, "Always", out reason) ||
                !string.Equals(reason, "initial", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected initial Always chest cache scan, got " + reason + ".");
            }

            if (InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 399, context, settings, "Always", out reason) ||
                !string.Equals(reason, "cacheHit", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Always chest cache to hit before the safety refresh tick, got " + reason + ".");
            }

            if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 400, context, settings, "Always", out reason) ||
                !string.Equals(reason, "safeRefresh", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Always chest cache safety refresh at 300 ticks, got " + reason + ".");
            }
        }

        private static void InformationChestAlwaysCacheCountersIgnoreOpenedMode()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1400f, 2300f, "world#42", "world-a", "player-a", 100);

                    InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    var diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysScanCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysScanCacheHitCount != 0 ||
                        !string.Equals(diagnostics.ChestAlwaysLastDirtyReason, "initial", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected first Always chest scan to record one miss and initial dirty reason.");
                    }

                    context.GameUpdateCount = 101;
                    InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysScanCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysScanCacheHitCount != 1)
                    {
                        throw new InvalidOperationException("Expected repeated Always scan to hit cache.");
                    }

                    context.GameUpdateCount = 102;
                    InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Opened");
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysScanCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysScanCacheHitCount != 1)
                    {
                        throw new InvalidOperationException("Expected Opened mode lookup not to mutate Always cache counters.");
                    }

                    context.GameUpdateCount = 103;
                    InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysScanCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysScanCacheHitCount != 2)
                    {
                        throw new InvalidOperationException("Expected Always cache to survive switching through Opened mode.");
                    }
                }
                finally
                {
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationChestAlwaysTypedScanDiagnosticsTrackFallbackTiles()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                FakeChestMain.ConfigureChest(5, 6, 21, 0);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    var count = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    var diagnostics = InformationOverlayService.GetDiagnostics();
                    if (count != 1)
                    {
                        throw new InvalidOperationException("Expected fake Always chest scan to find one chest, got " + count.ToString(CultureInfo.InvariantCulture) + ".");
                    }

                    if (diagnostics.ChestAlwaysTilesVisitedLast <= 0)
                    {
                        throw new InvalidOperationException("Expected Always chest scan diagnostics to record visited tiles.");
                    }

                    if (string.IsNullOrWhiteSpace(diagnostics.ChestAlwaysTypedTileFastPathStatus) ||
                        diagnostics.ChestAlwaysTypedTileFastPathStatus.IndexOf("fallback=", StringComparison.Ordinal) < 0)
                    {
                        throw new InvalidOperationException("Expected Always chest scan diagnostics to record typed/fallback tile status.");
                    }

                    if (diagnostics.ChestAlwaysNameCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysNameCacheHitCount != 0)
                    {
                        throw new InvalidOperationException("Expected first Always chest name resolve to miss the name cache.");
                    }
                }
                finally
                {
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationChestAlwaysNameCacheReusesAcrossDirtyScans()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                FakeChestMain.ConfigureChest(5, 6, 21, 0);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    if (InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always") != 1)
                    {
                        throw new InvalidOperationException("Expected first fake Always chest scan to find one chest.");
                    }

                    var moved = CreateInformationChestCacheContext(64f, 0f, 320, 240, 128f, 112f, "fake-world#1", "fake-world-record", "player-a", 101);
                    moved.MainType = typeof(FakeChestMain);
                    if (InformationOverlayService.GetChestLabelCountForTesting(moved, settings, "Always") != 1)
                    {
                        throw new InvalidOperationException("Expected dirty fake Always chest scan to keep the same visible chest.");
                    }

                    var diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysNameCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysNameCacheHitCount != 1)
                    {
                        throw new InvalidOperationException(
                            "Expected Always chest name cache to miss once and hit once, got miss=" +
                            diagnostics.ChestAlwaysNameCacheMissCount.ToString(CultureInfo.InvariantCulture) +
                            " hit=" +
                            diagnostics.ChestAlwaysNameCacheHitCount.ToString(CultureInfo.InvariantCulture) +
                            ".");
                    }
                }
                finally
                {
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationChestAlwaysPartialScanPublishesStableSnapshots()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.SetChestAlwaysPartialScanBudgetForTesting(10);
                FakeChestMain.ConfigureChest(5, 6, 21, 0);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    var firstCount = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    var diagnostics = InformationOverlayService.GetDiagnostics();
                    if (firstCount != 0 ||
                        diagnostics.ChestAlwaysPartialScanPendingCount <= 0 ||
                        diagnostics.ChestAlwaysStableSnapshotId != 0)
                    {
                        throw new InvalidOperationException("Expected initial partial Always scan to return no stable labels while pending.");
                    }

                    var completedCount = CompleteAlwaysChestPartialScanForTesting(context, settings, 1, 1);
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (completedCount != 1 ||
                        diagnostics.ChestAlwaysPartialScanPendingCount != 0 ||
                        diagnostics.ChestAlwaysPartialScanFrameCount <= 1 ||
                        diagnostics.ChestAlwaysStableSnapshotId != 1)
                    {
                        throw new InvalidOperationException(
                            "Expected initial partial Always scan to publish stable snapshot 1, count=" +
                            completedCount.ToString(CultureInfo.InvariantCulture) +
                            " pending=" +
                            diagnostics.ChestAlwaysPartialScanPendingCount.ToString(CultureInfo.InvariantCulture) +
                            " frames=" +
                            diagnostics.ChestAlwaysPartialScanFrameCount.ToString(CultureInfo.InvariantCulture) +
                            " stable=" +
                            diagnostics.ChestAlwaysStableSnapshotId.ToString(CultureInfo.InvariantCulture) +
                            ".");
                    }

                    var moved = CreateInformationChestCacheContext(64f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", context.GameUpdateCount + 1);
                    moved.MainType = typeof(FakeChestMain);
                    var pendingMoveCount = InformationOverlayService.GetChestLabelCountForTesting(moved, settings, "Always");
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (pendingMoveCount != 1 ||
                        diagnostics.ChestAlwaysPartialScanPendingCount <= 0 ||
                        diagnostics.ChestAlwaysStableSnapshotId != 1)
                    {
                        throw new InvalidOperationException("Expected dirty partial Always scan to keep drawing previous stable snapshot while pending.");
                    }

                    var movedCompletedCount = CompleteAlwaysChestPartialScanForTesting(moved, settings, 2, 1);
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (movedCompletedCount != 1 ||
                        diagnostics.ChestAlwaysPartialScanPendingCount != 0 ||
                        diagnostics.ChestAlwaysStableSnapshotId != 2)
                    {
                        throw new InvalidOperationException("Expected dirty partial Always scan to publish a second stable snapshot.");
                    }
                }
                finally
                {
                    InformationOverlayService.SetChestAlwaysPartialScanBudgetForTesting(0);
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationChestOpenedLabelsFollowCurrentContainerExistence()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                FakeChestMain.ConfigureEmptyWorld(24, 24);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);
                    RecordOpenedChestForTesting(context, 5, 6);

                    var missingCount = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Opened");
                    if (missingCount != 0)
                    {
                        throw new InvalidOperationException("Expected opened chest record with no current container tile to draw no labels.");
                    }

                    FakeChestMain.ConfigureChest(5, 6, 21, 10);
                    context.GameUpdateCount++;
                    var restoredCount = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Opened");
                    if (restoredCount != 1)
                    {
                        throw new InvalidOperationException("Expected opened chest label to reappear when the current container returns on the same coordinate.");
                    }

                    FakeChestMain.ConfigureEmptyWorld(24, 24);
                    context.GameUpdateCount++;
                    var removedCount = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Opened");
                    if (removedCount != 0)
                    {
                        throw new InvalidOperationException("Expected cached opened chest label to hide after the current container tile is removed.");
                    }

                    FakeChestMain.ConfigureChest(5, 6, 21, 10);
                    context.GameUpdateCount++;
                    var secondRestoredCount = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Opened");
                    if (secondRestoredCount != 1)
                    {
                        throw new InvalidOperationException("Expected cached opened chest label to follow the current tile when a valid container is placed back.");
                    }
                }
                finally
                {
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationChestAlwaysCachedLabelsFollowCurrentContainerExistence()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                FakeChestMain.ConfigureChest(5, 6, 21, 10);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    var initialCount = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    if (initialCount != 1)
                    {
                        throw new InvalidOperationException("Expected Always chest scan to cache one visible container.");
                    }

                    FakeChestMain.ConfigureEmptyWorld(24, 24);
                    context.GameUpdateCount++;
                    var removedCount = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    if (removedCount != 0)
                    {
                        throw new InvalidOperationException("Expected Always cache hit to hide the label after the current container tile is removed.");
                    }

                    FakeChestMain.ConfigureChest(5, 6, 21, 10);
                    context.GameUpdateCount++;
                    var restoredCount = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    if (restoredCount != 1)
                    {
                        throw new InvalidOperationException("Expected Always cache hit to show the label again when the current container returns.");
                    }
                }
                finally
                {
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationChestAlwaysPartialPendingFiltersInvalidStableSnapshot()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.SetChestAlwaysPartialScanBudgetForTesting(10);
                FakeChestMain.ConfigureChest(5, 6, 21, 0);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    var completedCount = CompleteAlwaysChestPartialScanForTesting(context, settings, 1, 1);
                    if (completedCount != 1)
                    {
                        throw new InvalidOperationException("Expected partial Always scan setup to publish one stable label.");
                    }

                    FakeChestMain.ConfigureEmptyWorld(24, 24);
                    var moved = CreateInformationChestCacheContext(64f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", context.GameUpdateCount + 1);
                    moved.MainType = typeof(FakeChestMain);
                    var pendingCount = InformationOverlayService.GetChestLabelCountForTesting(moved, settings, "Always");
                    var diagnostics = InformationOverlayService.GetDiagnostics();
                    if (pendingCount != 0 ||
                        diagnostics.ChestAlwaysPartialScanPendingCount <= 0 ||
                        diagnostics.ChestAlwaysStableSnapshotId != 1)
                    {
                        throw new InvalidOperationException("Expected dirty partial Always scan to filter an invalid previous stable chest label while pending.");
                    }
                }
                finally
                {
                    InformationOverlayService.SetChestAlwaysPartialScanBudgetForTesting(0);
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static int CompleteAlwaysChestPartialScanForTesting(InformationWorldContext context, AppSettings settings, long expectedStableSnapshotId, int expectedCount)
        {
            var count = 0;
            for (var attempt = 0; attempt < 200; attempt++)
            {
                context.GameUpdateCount++;
                count = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                var diagnostics = InformationOverlayService.GetDiagnostics();
                if (diagnostics.ChestAlwaysPartialScanPendingCount == 0 &&
                    diagnostics.ChestAlwaysStableSnapshotId >= expectedStableSnapshotId)
                {
                    return count;
                }
            }

            throw new InvalidOperationException(
                "Timed out waiting for partial Always chest scan to publish stable snapshot " +
                expectedStableSnapshotId.ToString(CultureInfo.InvariantCulture) +
                " with expected count " +
                expectedCount.ToString(CultureInfo.InvariantCulture) +
                ".");
        }

        private static void PlayerWorldBehaviorRecordsIsolateOpenedChests()
        {
            WithTemporaryBehaviorStore(() =>
            {
                var playerAWorldA = new PlayerWorldBehaviorContext
                {
                    PlayerKey = "player:a",
                    WorldKey = "world:a",
                    PlayerName = "A",
                    WorldName = "A World"
                };
                var playerBWorldA = new PlayerWorldBehaviorContext
                {
                    PlayerKey = "player:b",
                    WorldKey = "world:a",
                    PlayerName = "B",
                    WorldName = "A World"
                };
                var playerAWorldB = new PlayerWorldBehaviorContext
                {
                    PlayerKey = "player:a",
                    WorldKey = "world:b",
                    PlayerName = "A",
                    WorldName = "B World"
                };

                bool added;
                string message;
                if (!PlayerWorldBehaviorStore.TryRecordOpenedChest(playerAWorldA, 10, 20, "test", out added, out message) || !added)
                {
                    throw new InvalidOperationException("Expected opened chest record for player A/world A: " + message);
                }

                if (!PlayerWorldBehaviorStore.ContainsOpenedChest(playerAWorldA, 10, 20))
                {
                    throw new InvalidOperationException("Expected player A/world A to read its opened chest.");
                }

                if (PlayerWorldBehaviorStore.ContainsOpenedChest(playerBWorldA, 10, 20))
                {
                    throw new InvalidOperationException("Expected player B in the same world not to read player A's opened chest.");
                }

                if (PlayerWorldBehaviorStore.ContainsOpenedChest(playerAWorldB, 10, 20))
                {
                    throw new InvalidOperationException("Expected player A in another world not to read world A's opened chest.");
                }

                var fileA = PlayerWorldBehaviorStore.BuildScopedFileNameForTesting(playerAWorldA.PlayerKey, playerAWorldA.WorldKey);
                var fileB = PlayerWorldBehaviorStore.BuildScopedFileNameForTesting(playerBWorldA.PlayerKey, playerBWorldA.WorldKey);
                if (string.Equals(fileA, fileB, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected different player-world pairs to use different behavior files.");
                }
            });
        }

        private static void LegacyOpenedChestKeysMigrateToCurrentPlayerWorldOnly()
        {
            WithTemporaryBehaviorStore(() =>
            {
                var settings = AppSettings.CreateDefault();
                settings.InformationKnownChestKeys.Add("same-world#42|10|20");
                settings.InformationKnownChestKeys.Add("other-world#43|30|40");

                var current = CreateInformationChestRecordContext("player-a", "world-a", "same-world#42");
                var imported = InformationOverlayService.ImportLegacyKnownChestsForTesting(current, settings);
                if (imported != 1)
                {
                    throw new InvalidOperationException("Expected one legacy opened chest to migrate into the current player-world record.");
                }

                var currentBehavior = new PlayerWorldBehaviorContext { PlayerKey = current.PlayerRecordKey, WorldKey = current.WorldRecordKey };
                if (!PlayerWorldBehaviorStore.ContainsOpenedChest(currentBehavior, 10, 20))
                {
                    throw new InvalidOperationException("Expected migrated legacy chest to be readable for the current player-world.");
                }

                var otherPlayer = CreateInformationChestRecordContext("player-b", "world-a", "same-world#42");
                var otherBehavior = new PlayerWorldBehaviorContext { PlayerKey = otherPlayer.PlayerRecordKey, WorldKey = otherPlayer.WorldRecordKey };
                if (PlayerWorldBehaviorStore.ContainsOpenedChest(otherBehavior, 10, 20))
                {
                    throw new InvalidOperationException("Expected migrated legacy chest not to leak to another player in the same world.");
                }

                if (settings.InformationKnownChestKeys.Count != 1 ||
                    !string.Equals(settings.InformationKnownChestKeys[0], "other-world#43|30|40", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected migrated legacy key to be removed while other worlds remain.");
                }
            });
        }

        private static void InformationChestLabelsKeepSupportedContainerFamiliesVisible()
        {
            WithTemporaryBehaviorStore(() =>
            {
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var tileTypes = new[]
                    {
                        21,
                        467,
                        441,
                        468
                    };

                    for (var index = 0; index < tileTypes.Length; index++)
                    {
                        InformationOverlayService.ResetChestLabelCacheForTesting();
                        FakeChestMain.ConfigureChest(5, 6, tileTypes[index], 0);
                        var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", (ulong)(100 + index));
                        context.MainType = typeof(FakeChestMain);
                        var count = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                        if (count != 1)
                        {
                            throw new InvalidOperationException("Expected supported container tile type " + tileTypes[index].ToString(CultureInfo.InvariantCulture) + " to remain visible.");
                        }
                    }

                    InformationOverlayService.ResetChestLabelCacheForTesting();
                    FakeChestMain.ConfigureDresser(5, 6, 88, 1);
                    var dresserContext = CreateInformationChestCacheContext(0f, 0f, 320, 240, 112f, 112f, "fake-world#1", "fake-world-record", "player-a", 200);
                    dresserContext.MainType = typeof(FakeChestMain);
                    var dresserCount = InformationOverlayService.GetChestLabelCountForTesting(dresserContext, settings, "Always");
                    if (dresserCount != 1)
                    {
                        throw new InvalidOperationException("Expected supported dresser container tile to remain visible.");
                    }
                }
                finally
                {
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static InformationWorldContext CreateInformationChestRecordContext(string playerKey, string worldKey, string legacyWorldKey)
        {
            return new InformationWorldContext
            {
                WorldKey = legacyWorldKey,
                PlayerRecordKey = playerKey,
                WorldRecordKey = worldKey,
                PlayerName = playerKey,
                WorldName = legacyWorldKey,
                ScreenX = 1280f,
                ScreenY = 640f,
                ScreenWidth = 1920,
                ScreenHeight = 1080
            };
        }

        private static InformationWorldContext CreateInformationChestCacheContext(
            float screenX,
            float screenY,
            int screenWidth,
            int screenHeight,
            float playerCenterX,
            float playerCenterY,
            string worldKey,
            string worldRecordKey,
            string playerRecordKey,
            ulong gameUpdateCount)
        {
            return new InformationWorldContext
            {
                LocalPlayer = new object(),
                ScreenX = screenX,
                ScreenY = screenY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                PlayerCenterX = playerCenterX,
                PlayerCenterY = playerCenterY,
                WorldKey = worldKey,
                WorldRecordKey = worldRecordKey,
                PlayerRecordKey = playerRecordKey,
                PlayerName = playerRecordKey,
                WorldName = worldKey,
                GameUpdateCount = gameUpdateCount
            };
        }

        private static void RecordOpenedChestForTesting(InformationWorldContext context, int chestX, int chestY)
        {
            bool added;
            string message;
            if (!PlayerWorldBehaviorStore.TryRecordOpenedChest(
                    new PlayerWorldBehaviorContext { PlayerKey = context.PlayerRecordKey, WorldKey = context.WorldRecordKey },
                    chestX,
                    chestY,
                    "test",
                    out added,
                    out message) ||
                !added)
            {
                throw new InvalidOperationException("Expected opened chest record to be stored: " + message);
            }
        }

        private static class FakeChestMain
        {
            public static FakeChestTile[,] tile = new FakeChestTile[1, 1];
            public static bool[] tileContainer = new bool[1024];
            public static int maxTilesX = 1;
            public static int maxTilesY = 1;

            public static void ConfigureEmptyWorld(int width, int height)
            {
                maxTilesX = Math.Max(1, width);
                maxTilesY = Math.Max(1, height);
                tile = new FakeChestTile[maxTilesX, maxTilesY];
                tileContainer = new bool[1024];
            }

            public static void ConfigureChest(int chestX, int chestY, int tileType, int style)
            {
                maxTilesX = Math.Max(24, chestX + 4);
                maxTilesY = Math.Max(24, chestY + 4);
                tile = new FakeChestTile[maxTilesX, maxTilesY];
                tileContainer = new bool[1024];
                if (tileType >= 0 && tileType < tileContainer.Length)
                {
                    tileContainer[tileType] = true;
                }

                SetTile(chestX, chestY, tileType, style * 36, 0);
                SetTile(chestX + 1, chestY, tileType, style * 36 + 18, 0);
                SetTile(chestX, chestY + 1, tileType, style * 36, 18);
                SetTile(chestX + 1, chestY + 1, tileType, style * 36 + 18, 18);
            }

            public static void ConfigureDresser(int chestX, int chestY, int tileType, int style)
            {
                maxTilesX = Math.Max(24, chestX + 5);
                maxTilesY = Math.Max(24, chestY + 4);
                tile = new FakeChestTile[maxTilesX, maxTilesY];
                tileContainer = new bool[1024];
                if (tileType >= 0 && tileType < tileContainer.Length)
                {
                    tileContainer[tileType] = true;
                }

                var frameX = style * 54;
                SetTile(chestX, chestY, tileType, frameX, 0);
                SetTile(chestX + 1, chestY, tileType, frameX + 18, 0);
                SetTile(chestX + 2, chestY, tileType, frameX + 36, 0);
                SetTile(chestX, chestY + 1, tileType, frameX, 18);
                SetTile(chestX + 1, chestY + 1, tileType, frameX + 18, 18);
                SetTile(chestX + 2, chestY + 1, tileType, frameX + 36, 18);
            }

            public static void Reset()
            {
                tile = new FakeChestTile[1, 1];
                tileContainer = new bool[1024];
                maxTilesX = 1;
                maxTilesY = 1;
            }

            private static void SetTile(int x, int y, int tileType, int frameX, int frameY)
            {
                tile[x, y] = new FakeChestTile
                {
                    IsActive = true,
                    type = tileType,
                    frameX = frameX,
                    frameY = frameY
                };
            }
        }

        private sealed class FakeChestTile
        {
            public bool IsActive { get; set; }
            public int type;
            public int frameX;
            public int frameY;
        }

        private static class FakeTileHighlightMain
        {
            public static FakeChestTile[,] tile = new FakeChestTile[1, 1];

            public static void ConfigureLifeCrystalGroup()
            {
                tile = new FakeChestTile[32, 32];
                SetTile(4, 4, 12);
                SetTile(5, 4, 12);
                SetTile(4, 5, 12);
                SetTile(5, 5, 12);
                SetTile(10, 10, 639);
            }

            public static void Reset()
            {
                tile = new FakeChestTile[1, 1];
            }

            private static void SetTile(int x, int y, int tileType)
            {
                tile[x, y] = new FakeChestTile
                {
                    IsActive = true,
                    type = tileType
                };
            }
        }

        private static void WithTemporaryBehaviorStore(Action action)
        {
            var directory = Path.Combine(Path.GetTempPath(), "JueMingZ.Tests", "behavior-" + Guid.NewGuid().ToString("N"));
            try
            {
                PlayerWorldBehaviorStore.SetBehaviorDirectoryForTesting(directory);
                action();
            }
            finally
            {
                PlayerWorldBehaviorStore.ResetForTesting();
                TryDeleteDirectory(directory);
            }
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
            }
        }

        private static void InformationChestKeyParsingSurvivesWorldRenameWithSameId()
        {
            int x;
            int y;
            if (!InformationOverlayService.TryParseChestKeyForTesting("old-name#12345|88|91", "new-name#12345", out x, out y) ||
                x != 88 ||
                y != 91)
            {
                throw new InvalidOperationException("Expected opened chest keys to survive display-name-only world changes.");
            }

            if (InformationOverlayService.TryParseChestKeyForTesting("old-name#12345|88|91", "other-world#54321", out x, out y))
            {
                throw new InvalidOperationException("Expected opened chest keys from a different world id to stay isolated.");
            }
        }

        private static void InformationChestTileFallbackDetectsBasicContainerIds()
        {
            if (!InformationOverlayService.IsChestTileTypeForTesting(21) ||
                !InformationOverlayService.IsChestTileTypeForTesting(467))
            {
                throw new InvalidOperationException("Expected chest tile fallback to recognize vanilla container tile ids.");
            }

            if (InformationOverlayService.IsChestTileTypeForTesting(12))
            {
                throw new InvalidOperationException("Expected chest tile fallback to reject life crystal tile ids.");
            }
        }

        private static void InformationChestTileFallbackIncludesDressersAndExcludesDisplayContainers()
        {
            if (!InformationOverlayService.IsChestTileTypeForTesting(88) ||
                !InformationOverlayService.IsChestTileTypeForTesting(441) ||
                !InformationOverlayService.IsChestTileTypeForTesting(468))
            {
                throw new InvalidOperationException("Expected chest labels to include dressers and fake container tile ids.");
            }

            if (InformationOverlayService.IsChestTileTypeForTesting(470) ||
                InformationOverlayService.IsChestTileTypeForTesting(475) ||
                InformationOverlayService.IsChestTileTypeForTesting(128) ||
                InformationOverlayService.IsChestTileTypeForTesting(269))
            {
                throw new InvalidOperationException("Expected chest labels to reject display dolls, hat racks, and mannequin tile ids.");
            }
        }

        private static void InformationChestTileFallbackNormalizesTwoByTwoFrameOrigin()
        {
            int x;
            int y;
            if (!InformationOverlayService.TryNormalizeChestOriginFromFrameForTesting(101, 201, 18, 18, out x, out y) ||
                x != 100 ||
                y != 200)
            {
                throw new InvalidOperationException("Expected bottom-right chest frame tile to normalize to the chest origin.");
            }

            if (!InformationOverlayService.TryNormalizeChestOriginFromFrameForTesting(100, 200, 0, 0, out x, out y) ||
                x != 100 ||
                y != 200)
            {
                throw new InvalidOperationException("Expected top-left chest frame tile to keep its origin.");
            }
        }

        private static void InformationDresserChestLabelsUseThreeByTwoFrameRules()
        {
            int x;
            int y;
            if (!InformationOverlayService.TryNormalizeChestOriginFromFrameForTesting(88, 102, 201, 36, 18, out x, out y) ||
                x != 100 ||
                y != 200)
            {
                throw new InvalidOperationException("Expected bottom-right dresser frame tile to normalize with 3x2 dresser geometry.");
            }

            if (InformationOverlayService.BuildChestTileStyleForTesting(88, 54 * 2 + 36) != 2)
            {
                throw new InvalidOperationException("Expected dresser style to use 54px frame width.");
            }

            if (InformationOverlayService.BuildChestTileStyleForTesting(21, 36 * 2 + 18) != 2)
            {
                throw new InvalidOperationException("Expected normal chest style to keep 36px frame width.");
            }

            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                FakeChestMain.ConfigureDresser(5, 6, 88, 1);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 112f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    var count = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    if (count != 1)
                    {
                        throw new InvalidOperationException("Expected one 3x2 dresser to produce one chest label, got " + count.ToString(CultureInfo.InvariantCulture) + ".");
                    }
                }
                finally
                {
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationDresserDisplayNameAvoidsMapObjectOptionBleed()
        {
            var name = InformationOverlayService.ResolveChestTileDisplayNameForTesting(88, 30);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Expected dresser display name to resolve to a dresser name or fallback.");
            }

            if (name.IndexOf("梳妆", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("Dresser", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("Expected dresser display name to use dresser naming, got " + name + ".");
            }

            if (string.Equals(name, "机关", StringComparison.Ordinal) ||
                string.Equals(name, "雕像", StringComparison.Ordinal) ||
                string.Equals(name, "长椅", StringComparison.Ordinal) ||
                string.Equals(name, "熔炉", StringComparison.Ordinal) ||
                string.Equals(name, "未录制的音乐盒", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected dresser display name not to bleed into unrelated map object names, got " + name + ".");
            }
        }

        private static void InformationChestDisplayNameAvoidsMapObjectOptionBleed()
        {
            var ivyName = InformationOverlayService.ResolveChestTileDisplayNameForTesting(21, 10);
            AssertChestDisplayNameDoesNotBleedIntoMapObject(ivyName, "primary chest style 10");
            AssertChestDisplayNameIsIvyOrGenericFallback(ivyName, "primary chest style 10");

            var trappedIvyName = InformationOverlayService.ResolveChestTileDisplayNameForTesting(441, 10);
            AssertChestDisplayNameDoesNotBleedIntoMapObject(trappedIvyName, "fake primary chest style 10");
            AssertChestDisplayNameIsIvyOrGenericFallback(trappedIvyName, "fake primary chest style 10");

            var secondaryGoldName = InformationOverlayService.ResolveChestTileDisplayNameForTesting(467, 4);
            AssertChestDisplayNameDoesNotBleedIntoMapObject(secondaryGoldName, "secondary chest style 4");
        }

        private static void AssertChestDisplayNameDoesNotBleedIntoMapObject(string name, string label)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Expected " + label + " to resolve to a non-empty chest display name.");
            }

            if (string.Equals(name, "猩红祭坛", StringComparison.Ordinal) ||
                string.Equals(name, "Crimson Altar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "恶魔祭坛", StringComparison.Ordinal) ||
                string.Equals(name, "Demon Altar", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected " + label + " not to bleed into map object names, got " + name + ".");
            }
        }

        private static void AssertChestDisplayNameIsIvyOrGenericFallback(string name, string label)
        {
            if (string.Equals(name, "宝箱", StringComparison.Ordinal) ||
                string.Equals(name, "Chest", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (name.IndexOf("常春藤", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Ivy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            throw new InvalidOperationException("Expected " + label + " to resolve to an ivy chest name or generic fallback, got " + name + ".");
        }

        private static void InformationChestLabelsFrameLimitAllowsDenseRooms()
        {
            if (InformationOverlayService.MaxChestLabelsPerFrameForTesting() < 240)
            {
                throw new InvalidOperationException("Expected chest label frame limit to support dense chest rooms beyond the old 80-label cap.");
            }
        }

        private static void InformationNpcLabelSnapshotReusesMovementOnly()
        {
            if (!InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    42,
                    25,
                    100,
                    false,
                    false,
                    false,
                    false))
            {
                throw new InvalidOperationException("Expected NPC label snapshot refresh to reuse text/color when only position and non-eligibility life changed.");
            }

            if (InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    42,
                    0,
                    100,
                    false,
                    false,
                    false,
                    false))
            {
                throw new InvalidOperationException("Expected dead NPC snapshot to dirty the label cache.");
            }

            if (InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    42,
                    80,
                    100,
                    false,
                    true,
                    false,
                    false))
            {
                throw new InvalidOperationException("Expected friendly-state changes to dirty the NPC label cache.");
            }

            if (InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    true,
                    false))
            {
                throw new InvalidOperationException("Expected hidden-state changes to dirty the NPC label cache.");
            }

            if (InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    true))
            {
                throw new InvalidOperationException("Expected critter classification changes to dirty the NPC label cache.");
            }

            if (InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    43,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false))
            {
                throw new InvalidOperationException("Expected NPC type changes to dirty the label cache.");
            }
        }

        private static void InformationChestLabelsDrawOrderPrioritizesScreenCenter()
        {
            var context = new InformationWorldContext
            {
                ScreenX = 1000f,
                ScreenY = 1000f,
                ScreenWidth = 800,
                ScreenHeight = 600
            };
            var sorted = InformationOverlayService.SortChestLabelIndicesForTesting(
                context,
                new[] { 1010f, 1400f, 1780f, 500f },
                new[] { 1300f, 1300f, 1300f, 1300f });

            if (sorted.Length != 4 || sorted[0] != 1)
            {
                throw new InvalidOperationException("Expected chest label draw order to prioritize labels nearest the current screen center.");
            }

            if (sorted[sorted.Length - 1] != 3)
            {
                throw new InvalidOperationException("Expected off-screen padding labels to be drawn after labels inside the current screen.");
            }
        }

        private static void InformationChestLabelSortCacheDirtiesOnSourceAndMovementThreshold()
        {
            if (InformationOverlayService.ShouldRefreshChestLabelSortForTesting(
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u,
                    1063f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u))
            {
                throw new InvalidOperationException("Expected chest label sort cache to survive movement below the refresh threshold.");
            }

            if (!InformationOverlayService.ShouldRefreshChestLabelSortForTesting(
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u,
                    1064f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u))
            {
                throw new InvalidOperationException("Expected chest label sort cache to dirty once player movement reaches the threshold.");
            }

            if (!InformationOverlayService.ShouldRefreshChestLabelSortForTesting(
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u,
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    2u))
            {
                throw new InvalidOperationException("Expected chest label sort cache to dirty when the source label snapshot changes.");
            }

            if (!InformationOverlayService.ShouldRefreshChestLabelSortForTesting(
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u,
                    1000f,
                    1000f,
                    64f,
                    0f,
                    800,
                    600,
                    1u))
            {
                throw new InvalidOperationException("Expected chest label sort cache to dirty when screen center moves by the threshold.");
            }
        }

        private static void InformationChestLabelCacheCullCoversBucketMovement()
        {
            var context = new InformationWorldContext
            {
                LocalPlayer = new object(),
                ScreenX = 1000f,
                ScreenY = 1000f,
                ScreenWidth = 800,
                ScreenHeight = 600,
                PlayerCenterX = 1400f,
                PlayerCenterY = 1300f
            };

            if (!InformationOverlayService.CanCacheChestLabelForTesting(context, 1895f, 1300f))
            {
                throw new InvalidOperationException("Expected chest cache cull to prefetch labels that can enter the draw cull before the next screen bucket refresh.");
            }

            if (InformationOverlayService.CanCacheChestLabelForTesting(context, 1900f, 1300f))
            {
                throw new InvalidOperationException("Expected chest cache cull to keep a finite padding budget for dense-room performance.");
            }
        }


    }
}
