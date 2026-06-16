using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Automation.MapEnhancement;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Ui;
using JueMingZ.Records;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using Microsoft.Xna.Framework;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FeatureCatalogExposesMapFootprintsConfig()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.MapFootprints, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected map footprints feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented || feature.DefaultEnabled)
            {
                throw new InvalidOperationException("Map footprints must be visible, implemented, and disabled by default.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.MapEnhancement ||
                feature.UserCategory != FeatureUserCategory.MapEnhancement ||
                feature.MultiplayerSupport != FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
            {
                throw new InvalidOperationException("Map footprints metadata must stay in the frozen map enhancement bucket.");
            }

            if (feature.RequiredActions.Count != 1 ||
                feature.RequiredActions[0] != InputActionKind.None ||
                feature.RequiredGameState.Count != 3 ||
                !feature.RequiredGameState.Contains(GameStateKind.Map) ||
                !feature.RequiredGameState.Contains(GameStateKind.World) ||
                !feature.RequiredGameState.Contains(GameStateKind.UiState))
            {
                throw new InvalidOperationException("Map footprints must not require actions and must require map/world/UI state.");
            }

            AssertStringEquals(feature.DisplayName, "足迹", "map footprints display name");
            AssertStringEquals(feature.Description, "在大地图展示当前玩家-世界足迹。", "map footprints description");
        }

        private static void MapFootprintsDisplayConfigDefaultsAndFeatureSync()
        {
            var settings = AppSettings.CreateDefault();
            if (settings.MapFootprintsDisplayEnabled)
            {
                throw new InvalidOperationException("Map footprints display must default to off.");
            }

            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (snapshot.MapFootprintsDisplayEnabled)
            {
                throw new InvalidOperationException("Runtime snapshot must carry default disabled map footprints display flag.");
            }

            settings.MapFootprintsDisplayEnabled = true;
            snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (!snapshot.MapFootprintsDisplayEnabled)
            {
                throw new InvalidOperationException("Runtime snapshot must carry enabled map footprints display flag.");
            }

            var restore = PushTemporaryConfigDirectory("map-footprints-display");
            try
            {
                WriteConfigJson(ConfigService.AppSettingsPath, settings);
                WriteConfigJson(ConfigService.FeatureSettingsPath, FeatureSettings.CreateDefault());
                WriteConfigJson(ConfigService.HotkeySettingsPath, HotkeySettings.CreateDefault());
                ConfigService.Initialize();

                bool enabled;
                if (ConfigService.FeatureSettings == null ||
                    ConfigService.FeatureSettings.EnabledByFeatureId == null ||
                    !ConfigService.FeatureSettings.EnabledByFeatureId.TryGetValue(FeatureIds.MapFootprints, out enabled) ||
                    !enabled)
                {
                    throw new InvalidOperationException("Feature settings must synchronize map footprints display config.");
                }
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void LegacyMapEnhancementPageIncludesMapFootprintsDisplayRow()
        {
            var tooltips = LegacyMainWindow.GetMapFootprintsDisplayButtonTooltipsForTesting();
            if (tooltips == null ||
                tooltips.Length != 2 ||
                !string.Equals(tooltips[0], "在大地图展示足迹", StringComparison.Ordinal) ||
                !string.Equals(tooltips[1], string.Empty, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map footprints display on/off tooltip contract changed.");
            }

            var expectedHeight = LegacyUiMetrics.RowHeight * 8 +
                                 LegacyUiMetrics.SettingRowGap * 7 +
                                 LegacyMainWindow.CalculateMapMarkerListHeightForTesting(0) +
                                 24;
            if (LegacyMainWindow.CalculateMapEnhancementContentHeightForTesting() != expectedHeight)
            {
                throw new InvalidOperationException("Map enhancement content height must include footprints display row.");
            }
        }

        private static void PlayerWorldFootprintsPathUsesPlayerWorldPair()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var path = PlayerWorldFootprintStore.BuildPathForTesting("pair-footprint");
                AssertStringEquals(Path.GetFileName(path), PlayerWorldFeatureDataRoot.FootprintsFileName, "footprints file name");
                AssertContains(path, Path.Combine("player-worlds", "pair-footprint"));
            });
        }

        private static void PlayerWorldFootprintsRoundTripNormalizesRetentionModel()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldFootprintCache.ResetForTesting();
                PlayerWorldFootprintDiagnostics.ResetForTesting();
                var max = PlayerWorldFootprintConstants.MaxRetainedTicks;
                var file = new PlayerWorldFootprintFile
                {
                    PairId = "pair-retention",
                    WorldSizeX = 8400,
                    WorldSizeY = 2400,
                    TimelineEndTicks = max + 40L,
                    MaxRetainedTicks = max,
                    Segments = new List<PlayerWorldFootprintSegment>
                    {
                        CreateFootprintSegment(
                            "segment-1",
                            "seed",
                            new PlayerWorldFootprintPoint
                            {
                                TileX = -10d,
                                TileY = 12d,
                                StartTicks = 1L,
                                DurationTicks = 5L
                            },
                            new PlayerWorldFootprintPoint
                            {
                                TileX = 12.5d,
                                TileY = 34.25d,
                                StartTicks = max + 10L,
                                DurationTicks = 30L,
                                Flags = 2
                            })
                    }
                };

                var write = PlayerWorldFootprintStore.SaveFileForPairForTesting("pair-retention", file, "seed");
                if (!write.Succeeded ||
                    !write.RetentionTrimmed ||
                    write.SegmentCount != 1 ||
                    write.PointCount != 1)
                {
                    throw new InvalidOperationException("Footprint save should normalize and trim old points to the retention window.");
                }

                var read = PlayerWorldFootprintStore.ReadForPairForTesting("pair-retention");
                if (!read.Succeeded ||
                    read.SegmentCount != 1 ||
                    read.PointCount != 1 ||
                    read.WorldSizeX != 8400 ||
                    read.WorldSizeY != 2400 ||
                    read.MaxRetainedTicks != PlayerWorldFootprintConstants.MaxRetainedTicks)
                {
                    throw new InvalidOperationException("Footprint read should roundtrip normalized world size and retention metadata.");
                }

                var point = read.Segments[0].Points[0];
                AssertNear(point.TileX, 12.5d, "footprint retained point tile x");
                AssertNear(point.TileY, 34.25d, "footprint retained point tile y");
                AssertLongEquals(point.StartTicks, max + 10L, "footprint retained point start");
                AssertLongEquals(point.DurationTicks, 30L, "footprint retained point duration");
                if (point.Flags != 2 ||
                    read.TimelineStartTicks != max + 10L ||
                    read.TimelineEndTicks != max + 40L)
                {
                    throw new InvalidOperationException("Footprint timeline and flags must survive normalization.");
                }
            });
        }

        private static void PlayerWorldFootprintsSeparatePairs()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldFootprintStore.SaveForPairForTesting(
                    "pair-footprint-first",
                    100,
                    100,
                    new List<PlayerWorldFootprintSegment> { CreateFootprintSegment("first", string.Empty, CreateFootprintPoint(1d, 2d, 0L, 6L)) },
                    "seed");
                PlayerWorldFootprintStore.SaveForPairForTesting(
                    "pair-footprint-second",
                    100,
                    100,
                    new List<PlayerWorldFootprintSegment> { CreateFootprintSegment("second", string.Empty, CreateFootprintPoint(3d, 4d, 0L, 6L)) },
                    "seed");

                var first = PlayerWorldFootprintStore.ReadForPairForTesting("pair-footprint-first");
                var second = PlayerWorldFootprintStore.ReadForPairForTesting("pair-footprint-second");
                AssertStringEquals(first.Segments[0].SegmentId, "first", "first pair footprint segment");
                AssertStringEquals(second.Segments[0].SegmentId, "second", "second pair footprint segment");
            });
        }

        private static void PlayerWorldFootprintsSaveFailureKeepsExistingFile()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var pairId = "pair-footprint-save-failure";
                var path = PlayerWorldFootprintStore.BuildPathForTesting(pairId);
                var first = PlayerWorldFootprintStore.SaveForPairForTesting(
                    pairId,
                    100,
                    100,
                    new List<PlayerWorldFootprintSegment> { CreateFootprintSegment("original", string.Empty, CreateFootprintPoint(1d, 2d, 0L, 6L)) },
                    "seed");
                if (!first.Succeeded)
                {
                    throw new InvalidOperationException("Initial footprint save should succeed.");
                }

                var originalJson = File.ReadAllText(path, Encoding.UTF8);
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var failed = PlayerWorldFootprintStore.SaveForPairForTesting(
                        pairId,
                        100,
                        100,
                        new List<PlayerWorldFootprintSegment> { CreateFootprintSegment("new", string.Empty, CreateFootprintPoint(5d, 6d, 0L, 6L)) },
                        "lockedWrite");
                    if (failed.Succeeded || !string.Equals(failed.Status, "writeFailed", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Locked footprint file should fail safe write.");
                    }
                }

                var afterFailureJson = File.ReadAllText(path, Encoding.UTF8);
                AssertStringEquals(afterFailureJson, originalJson, "footprint file bytes after failed replacement");
            });
        }

        private static void PlayerWorldFootprintsCorruptJsonFailsSoft()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var path = PlayerWorldFootprintStore.BuildPathForTesting("pair-footprint-corrupt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{not-json", Encoding.UTF8);

                var read = PlayerWorldFootprintStore.ReadForPairForTesting("pair-footprint-corrupt");
                if (read.Succeeded || !read.ReadFailed || !string.Equals(read.Status, "readFailed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Corrupt footprints json must fail soft without throwing.");
                }
            });
        }

        private static void PlayerWorldFootprintsPairMismatchFailsSoft()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var path = PlayerWorldFootprintStore.BuildPathForTesting("pair-footprint-real");
                string message;
                if (!PlayerWorldFeatureDataStore.TryWriteJsonForTesting(
                        path,
                        new PlayerWorldFootprintFile
                        {
                            PairId = "pair-footprint-other",
                            Segments = new List<PlayerWorldFootprintSegment>
                            {
                                CreateFootprintSegment("mismatch", string.Empty, CreateFootprintPoint(1d, 1d, 0L, 6L))
                            }
                        },
                        out message))
                {
                    throw new InvalidOperationException("Pair mismatch seed write should succeed: " + message);
                }

                var read = PlayerWorldFootprintStore.ReadForPairForTesting("pair-footprint-real");
                if (read.Succeeded ||
                    !read.ReadFailed ||
                    !string.Equals(read.Status, "readFailed", StringComparison.Ordinal) ||
                    !string.Equals(read.Message, "pairMismatch", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Footprint store must fail soft on pair mismatch.");
                }
            });
        }

        private static void PlayerWorldFootprintsIdentityUnavailableDoesNotWriteUnknown()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var write = PlayerWorldFootprintStore.SaveForPairForTesting(
                    string.Empty,
                    100,
                    100,
                    new List<PlayerWorldFootprintSegment> { CreateFootprintSegment("unknown", string.Empty, CreateFootprintPoint(1d, 1d, 0L, 6L)) },
                    "identityUnavailable");
                if (write.Succeeded ||
                    write.IdentityResolved ||
                    !string.Equals(write.Status, "identityUnavailable", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Footprint store must fail closed when pair id is unavailable.");
                }

                var playerWorlds = Path.Combine(root, PlayerWorldFeatureDataRoot.PlayerWorldDirectoryName);
                if (Directory.Exists(playerWorlds) &&
                    Directory.GetFiles(playerWorlds, "*", SearchOption.AllDirectories).Length != 0)
                {
                    throw new InvalidOperationException("Identity-unavailable footprint writes must not create player-world files.");
                }
            });
        }

        private static void PlayerWorldFootprintRecorderDisplayOffStillRecords()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetFootprintRuntimeForTesting();
                var settings = AppSettings.CreateDefault();
                if (settings.MapFootprintsDisplayEnabled)
                {
                    throw new InvalidOperationException("Map footprints display must be off for this regression sample.");
                }

                var start = new DateTime(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc);
                PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(10d, 20d, null, false),
                    0L,
                    "pair-footprint-runtime-display-off",
                    8400,
                    2400,
                    start);
                var second = PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(10d, 20d, null, false),
                    PlayerWorldFootprintService.SampleCadenceTicks,
                    "pair-footprint-runtime-display-off",
                    8400,
                    2400,
                    start.AddMilliseconds(100));

                if (!second.Succeeded ||
                    !second.IdleMerged ||
                    second.PointCount != 1 ||
                    second.LastPointDurationTicks != PlayerWorldFootprintService.SampleCadenceTicks)
                {
                    throw new InvalidOperationException("Footprint recorder must accumulate while display-only switch is off.");
                }

                var diagnostics = PlayerWorldFootprintDiagnostics.GetSnapshotForTesting();
                AssertStringEquals(diagnostics.LastDecision, "idleMerged", "footprint runtime display-off decision");
                AssertLongEquals(diagnostics.LastPointDurationTicks, PlayerWorldFootprintService.SampleCadenceTicks, "footprint runtime display-off duration");

                PlayerWorldFootprintService.FlushPending();
                var read = PlayerWorldFootprintStore.ReadForPairForTesting("pair-footprint-runtime-display-off");
                if (!read.Succeeded || read.SegmentCount != 1 || read.PointCount != 1)
                {
                    throw new InvalidOperationException("Display-off recorder sample must flush to the current pair footprints file.");
                }

                AssertLongEquals(read.Segments[0].Points[0].DurationTicks, PlayerWorldFootprintService.SampleCadenceTicks, "display-off flushed duration");
            });
        }

        private static void PlayerWorldFootprintRecorderUiStatesDoNotBlockRecording()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetFootprintRuntimeForTesting();
                var blockedUi = new UiStateSnapshot
                {
                    GameInputAvailable = false,
                    PlayerInventoryOpen = true,
                    ChatOpen = true,
                    ChestOpen = true,
                    NpcChatOpen = true,
                    HasBlockingUi = true
                };
                var start = new DateTime(2026, 6, 16, 12, 5, 0, DateTimeKind.Utc);

                PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(30d, 40d, blockedUi, false),
                    0L,
                    "pair-footprint-runtime-ui-gates",
                    8400,
                    2400,
                    start);
                var second = PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(30d, 40d, blockedUi, false),
                    PlayerWorldFootprintService.SampleCadenceTicks,
                    "pair-footprint-runtime-ui-gates",
                    8400,
                    2400,
                    start.AddMilliseconds(100));

                if (!second.Succeeded ||
                    !second.IdleMerged ||
                    second.TimelineEndTicks != PlayerWorldFootprintService.SampleCadenceTicks)
                {
                    throw new InvalidOperationException("F5/chat/chest/NPC/text-focus style UI gates must not stop normal world-tick footprint recording.");
                }
            });
        }

        private static void PlayerWorldFootprintRecorderWallClockGapBreaksWithoutDuration()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetFootprintRuntimeForTesting();
                var start = new DateTime(2026, 6, 16, 12, 10, 0, DateTimeKind.Utc);
                PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(50d, 60d, null, false),
                    0L,
                    "pair-footprint-runtime-gap",
                    8400,
                    2400,
                    start);
                PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(50d, 60d, null, false),
                    PlayerWorldFootprintService.SampleCadenceTicks,
                    "pair-footprint-runtime-gap",
                    8400,
                    2400,
                    start.AddMilliseconds(100));

                var gap = PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(50d, 60d, null, false),
                    PlayerWorldFootprintService.SampleCadenceTicks * 2L,
                    "pair-footprint-runtime-gap",
                    8400,
                    2400,
                    start.AddMinutes(10));
                if (!gap.SegmentBreak ||
                    !string.Equals(gap.BreakReason, "wallClockGap", StringComparison.Ordinal) ||
                    gap.TimelineEndTicks != PlayerWorldFootprintService.SampleCadenceTicks ||
                    gap.LastPointDurationTicks != 0L)
                {
                    throw new InvalidOperationException("Sleep/resume style wall-clock gaps must start a segment without adding idle duration.");
                }

                var resumed = PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(50d, 60d, null, false),
                    PlayerWorldFootprintService.SampleCadenceTicks * 3L,
                    "pair-footprint-runtime-gap",
                    8400,
                    2400,
                    start.AddMinutes(10).AddMilliseconds(100));
                if (!resumed.IdleMerged ||
                    resumed.SegmentCount != 2 ||
                    resumed.TimelineEndTicks != PlayerWorldFootprintService.SampleCadenceTicks * 2L ||
                    resumed.LastPointDurationTicks != PlayerWorldFootprintService.SampleCadenceTicks)
                {
                    throw new InvalidOperationException("Recording must resume from the new segment after a no-tick wall-clock gap.");
                }
            });
        }

        private static void PlayerWorldFootprintRecorderPairSwitchFlushesPreviousPair()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetFootprintRuntimeForTesting();
                var start = new DateTime(2026, 6, 16, 12, 20, 0, DateTimeKind.Utc);
                PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(70d, 80d, null, false),
                    0L,
                    "pair-footprint-runtime-first",
                    8400,
                    2400,
                    start);
                PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(70d, 80d, null, false),
                    PlayerWorldFootprintService.SampleCadenceTicks,
                    "pair-footprint-runtime-first",
                    8400,
                    2400,
                    start.AddMilliseconds(100));

                var switched = PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(90d, 100d, null, false),
                    PlayerWorldFootprintService.SampleCadenceTicks * 2L,
                    "pair-footprint-runtime-second",
                    4200,
                    1200,
                    start.AddMilliseconds(200));
                if (!switched.Succeeded ||
                    !string.Equals(switched.PairId, "pair-footprint-runtime-second", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Pair switch should seed the new pair after flushing the previous pair.");
                }

                var first = PlayerWorldFootprintStore.ReadForPairForTesting("pair-footprint-runtime-first");
                if (!first.Succeeded ||
                    first.SegmentCount != 1 ||
                    first.PointCount != 1 ||
                    first.Segments[0].Points[0].DurationTicks != PlayerWorldFootprintService.SampleCadenceTicks)
                {
                    throw new InvalidOperationException("Pair switch must flush the previous pair before recording the new pair.");
                }
            });
        }

        private static void PlayerWorldFootprintRecorderPairSwitchFlushFailureKeepsMemory()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetFootprintRuntimeForTesting();
                var pairId = "pair-footprint-runtime-flush-failure";
                var seedWrite = PlayerWorldFootprintStore.SaveForPairForTesting(
                    pairId,
                    8400,
                    2400,
                    new List<PlayerWorldFootprintSegment> { CreateFootprintSegment("seed", string.Empty, CreateFootprintPoint(1d, 1d, 0L, 6L)) },
                    "seed");
                if (!seedWrite.Succeeded)
                {
                    throw new InvalidOperationException("Flush failure regression seed write should succeed.");
                }

                var start = new DateTime(2026, 6, 16, 12, 25, 0, DateTimeKind.Utc);
                PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(70d, 80d, null, false),
                    0L,
                    pairId,
                    8400,
                    2400,
                    start);
                PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(70d, 80d, null, false),
                    PlayerWorldFootprintService.SampleCadenceTicks,
                    pairId,
                    8400,
                    2400,
                    start.AddMilliseconds(100));

                var path = PlayerWorldFootprintStore.BuildPathForTesting(pairId);
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var switched = PlayerWorldFootprintService.TickForTesting(
                        CreateFootprintSnapshot(90d, 100d, null, false),
                        PlayerWorldFootprintService.SampleCadenceTicks * 2L,
                        "pair-footprint-runtime-after-failure",
                        4200,
                        1200,
                        start.AddMilliseconds(200));
                    if (switched.Succeeded ||
                        !switched.WriteFailed ||
                        !string.Equals(switched.PairId, pairId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Pair switch flush failure must fail closed on the dirty old pair.");
                    }

                    PlayerWorldFootprintReadResult memory;
                    int signature;
                    if (!PlayerWorldFootprintService.TryGetInMemoryForPair(pairId, out memory, out signature) ||
                        memory == null ||
                        memory.PointCount <= 1 ||
                        signature == 0)
                    {
                        throw new InvalidOperationException("Dirty footprint memory must remain available after a pair-switch flush failure.");
                    }
                }
            });
        }

        private static void PlayerWorldFootprintRecorderThresholdAndDirectionPoints()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetFootprintRuntimeForTesting();
                var start = new DateTime(2026, 6, 16, 12, 30, 0, DateTimeKind.Utc);
                PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(100d, 100d, null, false),
                    0L,
                    "pair-footprint-runtime-direction",
                    8400,
                    2400,
                    start);
                var moved = PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(104d, 100d, null, false),
                    PlayerWorldFootprintService.SampleCadenceTicks,
                    "pair-footprint-runtime-direction",
                    8400,
                    2400,
                    start.AddMilliseconds(100));
                if (!moved.PointAdded || moved.PointCount != 2)
                {
                    throw new InvalidOperationException("Footprint recorder must add a point once movement reaches the distance threshold.");
                }

                var turned = PlayerWorldFootprintService.TickForTesting(
                    CreateFootprintSnapshot(104d, 102.5d, null, false),
                    PlayerWorldFootprintService.SampleCadenceTicks * 2L,
                    "pair-footprint-runtime-direction",
                    8400,
                    2400,
                    start.AddMilliseconds(200));
                if (!turned.PointAdded ||
                    !string.Equals(turned.Status, "directionPointAdded", StringComparison.Ordinal) ||
                    turned.PointCount != 3)
                {
                    throw new InvalidOperationException("Sharp direction changes should add a footprint point before the distance threshold.");
                }

                PlayerWorldFootprintReadResult memory;
                int signature;
                if (!PlayerWorldFootprintService.TryGetInMemoryForPair("pair-footprint-runtime-direction", out memory, out signature) ||
                    memory == null ||
                    memory.Segments[0].Points[2].Flags != 2 ||
                    signature == 0)
                {
                    throw new InvalidOperationException("Footprint in-memory API must expose the direction-change point for the draw stage.");
                }
            });
        }

        private static void MapFootprintRenderCacheBuildsLinesWithoutCrossSegmentConnection()
        {
            var read = new PlayerWorldFootprintReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "memory",
                PairId = "pair-footprint-render",
                WorldSizeX = 8400,
                WorldSizeY = 2400,
                TimelineStartTicks = 0L,
                TimelineEndTicks = 40L,
                SegmentCount = 2,
                PointCount = 5,
                Segments = new List<PlayerWorldFootprintSegment>
                {
                    CreateFootprintSegment(
                        "segment-a",
                        "seed",
                        CreateFootprintPoint(1d, 2d, 0L, 6L),
                        CreateFootprintPoint(5d, 2d, 6L, 6L),
                        CreateFootprintPoint(9d, 4d, 12L, 6L)),
                    CreateFootprintSegment(
                        "segment-b",
                        "wallClockGap",
                        CreateFootprintPoint(100d, 100d, 30L, 6L),
                        CreateFootprintPoint(104d, 104d, 36L, 6L))
                }
            };

            var snapshot = MapFootprintRenderCache.BuildSnapshotForTesting(read, true, "memory", 12345);
            if (!snapshot.DisplayEnabled ||
                !string.Equals(snapshot.Status, "ready", StringComparison.Ordinal) ||
                snapshot.LineCount != 3 ||
                snapshot.RenderedSegmentCount != 2 ||
                snapshot.DataSignature != 12345)
            {
                throw new InvalidOperationException("Footprint render cache must build one line chain per segment.");
            }

            if (snapshot.Lines[0].SegmentIndex != 0 ||
                snapshot.Lines[1].SegmentIndex != 0 ||
                !snapshot.Lines[1].IsSegmentEnd ||
                snapshot.Lines[2].SegmentIndex != 1 ||
                !snapshot.Lines[2].IsSegmentEnd)
            {
                throw new InvalidOperationException("Footprint render cache must mark segment boundaries for no-connect drawing.");
            }

            AssertNear(snapshot.Lines[1].EndTileX, 9d, "first segment last line end x");
            AssertNear(snapshot.Lines[2].StartTileX, 100d, "second segment first line start x");
        }

        private static void MapFootprintRenderCacheDisplayOffClearsDrawOnly()
        {
            PlayerWorldFootprintDiagnostics.ResetForTesting();
            var read = new PlayerWorldFootprintReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "memory",
                PairId = "pair-footprint-render-hidden",
                SegmentCount = 1,
                PointCount = 2,
                Segments = new List<PlayerWorldFootprintSegment>
                {
                    CreateFootprintSegment(
                        "segment-hidden",
                        "seed",
                        CreateFootprintPoint(1d, 1d, 0L, 6L),
                        CreateFootprintPoint(5d, 5d, 6L, 6L))
                }
            };

            var snapshot = MapFootprintRenderCache.BuildSnapshotForTesting(read, false, "memory", 99);
            MapFootprintRenderCache.PublishForTesting(snapshot);
            var diagnostics = PlayerWorldFootprintDiagnostics.GetSnapshotForTesting();
            if (snapshot.DisplayEnabled ||
                snapshot.LineCount != 0 ||
                !string.Equals(snapshot.Status, "displayHidden", StringComparison.Ordinal) ||
                diagnostics.MapFootprintsDisplayEnabled ||
                diagnostics.MapFootprintsRenderCacheLineCount != 0 ||
                !string.Equals(diagnostics.MapFootprintsRenderCacheStatus, "displayHidden", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Display-off map footprints must clear draw cache without changing recorder semantics.");
            }
        }

        private static void MapFootprintRenderDrawPlanCullsThinsAndLimits()
        {
            var read = new PlayerWorldFootprintReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "memory",
                PairId = "pair-footprint-render-plan",
                SegmentCount = 2,
                PointCount = 6,
                Segments = new List<PlayerWorldFootprintSegment>
                {
                    CreateFootprintSegment(
                        "segment-visible",
                        "seed",
                        CreateFootprintPoint(0d, 0d, 0L, 6L),
                        CreateFootprintPoint(0.5d, 0d, 6L, 6L),
                        CreateFootprintPoint(10d, 0d, 12L, 6L),
                        CreateFootprintPoint(20d, 0d, 18L, 6L)),
                    CreateFootprintSegment(
                        "segment-offscreen",
                        "abnormalJump",
                        CreateFootprintPoint(200d, 200d, 30L, 6L),
                        CreateFootprintPoint(210d, 210d, 36L, 6L))
                }
            };

            var snapshot = MapFootprintRenderCache.BuildSnapshotForTesting(read, true, "memory", 123);
            var transform = new MapFootprintDrawTransform
            {
                MapPosition = Microsoft.Xna.Framework.Vector2.Zero,
                MapOffset = Microsoft.Xna.Framework.Vector2.Zero,
                MapScale = 1f,
                Opacity = 1f
            };
            var screen = new Rectangle(0, 0, 100, 100);
            var fullPlan = MapFootprintRenderCache.BuildDrawPlanForTesting(snapshot, transform, screen, 10, 5f);
            if (fullPlan.DrawnLineCount != 2 ||
                fullPlan.ThinnedLineCount != 1 ||
                fullPlan.CulledLineCount != 1 ||
                fullPlan.DrawLimitHit)
            {
                throw new InvalidOperationException("Footprint draw plan must thin tiny segments, cull offscreen lines, and draw visible commands.");
            }

            var limitedPlan = MapFootprintRenderCache.BuildDrawPlanForTesting(snapshot, transform, screen, 1, 5f);
            if (!limitedPlan.DrawLimitHit ||
                limitedPlan.DrawnLineCount != 1 ||
                limitedPlan.DrawLimitSkippedLineCount <= 0)
            {
                throw new InvalidOperationException("Footprint draw plan must stop at the single-frame draw limit.");
            }
        }

        private static void MapFootprintPlaybackDefaultsToLatestPausedAndScreenSpaceLayout()
        {
            MapFootprintPlaybackState.ResetForTesting();
            var snapshot = CreatePlaybackRenderSnapshot("pair-footprint-playback-default", 0L, 120L);
            var state = MapFootprintPlaybackState.Advance(
                snapshot,
                true,
                new DateTime(2026, 6, 16, 13, 0, 0, DateTimeKind.Utc));
            if (!state.Visible ||
                !state.Paused ||
                state.PlaybackRate != 1 ||
                state.CursorTicks != 120L ||
                !state.IsAtLatest)
            {
                throw new InvalidOperationException("Map footprint playback must open at latest end, paused, at 1x.");
            }

            var layout = MapFootprintPlaybackOverlay.CalculateLayoutForTesting(1280, 720);
            var sameLayout = MapFootprintPlaybackOverlay.CalculateLayoutForTesting(1280, 720);
            if (layout.Bar.X != sameLayout.Bar.X ||
                layout.Bar.Y != sameLayout.Bar.Y ||
                layout.Bar.Width != sameLayout.Bar.Width ||
                layout.Bar.Height != sameLayout.Bar.Height)
            {
                throw new InvalidOperationException("Playback overlay layout must depend on screen size only.");
            }

            if (layout.Bar.Bottom > 720 - 8 ||
                layout.Bar.Y < 720 - 120 ||
                layout.Track.Width <= 80)
            {
                throw new InvalidOperationException("Playback overlay must stay near the bottom safe margin with a usable progress track.");
            }

            var trackHit = MapFootprintPlaybackOverlay.HitTestForTesting(layout, layout.Track.CenterX, layout.Track.CenterY);
            if (!trackHit.BarHovered || !string.Equals(trackHit.Target, MapFootprintPlaybackHitTargets.Track, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Playback overlay hit-test must recognize progress track hits.");
            }

            var outside = MapFootprintPlaybackOverlay.HitTestForTesting(layout, layout.Bar.X - 4, layout.Bar.Y + 4);
            if (outside.BarHovered || !string.Equals(outside.Target, MapFootprintPlaybackHitTargets.Outside, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Playback overlay hit-test must leave outside fullscreen map input untouched.");
            }
        }

        private static void MapFootprintPlaybackHandlesRateDragAndInputHandoff()
        {
            MapFootprintPlaybackState.ResetForTesting();
            var snapshot = CreatePlaybackRenderSnapshot("pair-footprint-playback-input", 0L, 100L);
            var now = new DateTime(2026, 6, 16, 13, 5, 0, DateTimeKind.Utc);
            MapFootprintPlaybackState.Advance(snapshot, true, now);
            var layout = MapFootprintPlaybackOverlay.CalculateLayoutForTesting(900, 600);

            var outside = MapFootprintPlaybackState.HandleInput(
                layout,
                new LegacyMouseSnapshot
                {
                    X = layout.Bar.X - 12,
                    Y = layout.Bar.Y + 8,
                    LeftDown = true,
                    ScrollDelta = 0
                },
                snapshot,
                true,
                now.AddMilliseconds(16));
            if (outside.MouseCaptured || outside.ClickConsumed)
            {
                throw new InvalidOperationException("Playback overlay must not consume input outside the control bar.");
            }

            MapFootprintPlaybackState.HandleInput(
                layout,
                new LegacyMouseSnapshot
                {
                    X = layout.Bar.X - 12,
                    Y = layout.Bar.Y + 8,
                    LeftDown = false
                },
                snapshot,
                true,
                now.AddMilliseconds(24));

            var rateIndex = 2;
            var rateButton = layout.RateButtons[rateIndex];
            var rateClick = MapFootprintPlaybackState.HandleInput(
                layout,
                new LegacyMouseSnapshot
                {
                    X = rateButton.CenterX,
                    Y = rateButton.CenterY,
                    LeftDown = true
                },
                snapshot,
                true,
                now.AddMilliseconds(32));
            if (!rateClick.MouseCaptured ||
                !rateClick.ClickConsumed ||
                rateClick.State.PlaybackRate != layout.RateValues[rateIndex] ||
                !rateClick.State.Paused)
            {
                throw new InvalidOperationException("Playback rate buttons must consume only their click and preserve paused state.");
            }

            MapFootprintPlaybackState.HandleInput(
                layout,
                new LegacyMouseSnapshot
                {
                    X = rateButton.CenterX,
                    Y = rateButton.CenterY,
                    LeftDown = false
                },
                snapshot,
                true,
                now.AddMilliseconds(48));

            var trackPressX = layout.Track.X + layout.Track.Width / 2;
            var dragStart = MapFootprintPlaybackState.HandleInput(
                layout,
                new LegacyMouseSnapshot
                {
                    X = trackPressX,
                    Y = layout.Track.CenterY,
                    LeftDown = true
                },
                snapshot,
                true,
                now.AddMilliseconds(64));
            if (!dragStart.MouseCaptured ||
                !dragStart.ClickConsumed ||
                !dragStart.State.Dragging ||
                dragStart.State.CursorTicks < 45L ||
                dragStart.State.CursorTicks > 55L)
            {
                throw new InvalidOperationException("Dragging the playback track must capture input and seek the cursor.");
            }

            var dragEnd = MapFootprintPlaybackState.HandleInput(
                layout,
                new LegacyMouseSnapshot
                {
                    X = layout.Track.Right,
                    Y = layout.Track.CenterY,
                    LeftDown = false
                },
                snapshot,
                true,
                now.AddMilliseconds(80));
            if (!dragEnd.MouseCaptured ||
                dragEnd.State.Dragging ||
                dragEnd.State.CursorTicks != 100L ||
                !dragEnd.State.IsAtLatest)
            {
                throw new InvalidOperationException("Playback drag release must seek, stop dragging, and capture only the release frame.");
            }

            var afterReleaseOutside = MapFootprintPlaybackState.HandleInput(
                layout,
                new LegacyMouseSnapshot
                {
                    X = layout.Bar.X - 12,
                    Y = layout.Bar.Y + 8,
                    LeftDown = false
                },
                snapshot,
                true,
                now.AddMilliseconds(96));
            if (afterReleaseOutside.MouseCaptured)
            {
                throw new InvalidOperationException("Playback overlay must hand input back after drag release outside the bar.");
            }
        }

        private static void MapFootprintPlaybackDrawPlanSlicesCurrentLine()
        {
            var read = new PlayerWorldFootprintReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "memory",
                PairId = "pair-footprint-playback-slice",
                WorldSizeX = 8400,
                WorldSizeY = 2400,
                TimelineStartTicks = 0L,
                TimelineEndTicks = 120L,
                SegmentCount = 1,
                PointCount = 3,
                Segments = new List<PlayerWorldFootprintSegment>
                {
                    CreateFootprintSegment(
                        "segment-playback",
                        "seed",
                        CreateFootprintPoint(0d, 0d, 0L, 6L),
                        CreateFootprintPoint(10d, 0d, 60L, 6L),
                        CreateFootprintPoint(20d, 0d, 120L, 6L))
                }
            };
            var snapshot = MapFootprintRenderCache.BuildSnapshotForTesting(read, true, "memory", 77);
            var transform = new MapFootprintDrawTransform
            {
                MapPosition = Microsoft.Xna.Framework.Vector2.Zero,
                MapOffset = Microsoft.Xna.Framework.Vector2.Zero,
                MapScale = 1f,
                Opacity = 1f
            };
            var screen = new Rectangle(0, 0, 100, 100);

            var startPlan = MapFootprintRenderCache.BuildDrawPlanForTesting(snapshot, transform, screen, 10, 0.5f, 0L);
            if (startPlan.DrawnLineCount != 0)
            {
                throw new InvalidOperationException("Playback cursor at timeline start must not draw future lines.");
            }

            var slicedPlan = MapFootprintRenderCache.BuildDrawPlanForTesting(snapshot, transform, screen, 10, 0.5f, 90L);
            if (slicedPlan.DrawnLineCount != 2 ||
                slicedPlan.Commands == null ||
                slicedPlan.Commands.Length != 2 ||
                slicedPlan.CursorTicks != 90L)
            {
                throw new InvalidOperationException("Playback cursor in the middle of a segment must draw the full past line and one partial line.");
            }

            AssertNear(slicedPlan.Commands[1].Start.X, 10d, "sliced line start x");
            AssertNear(slicedPlan.Commands[1].End.X, 15d, "sliced line interpolated end x");
            AssertNear(slicedPlan.Commands[1].End.Y, 0d, "sliced line interpolated end y");

            var latestPlan = MapFootprintRenderCache.BuildDrawPlanForTesting(snapshot, transform, screen, 10, 0.5f, 120L);
            if (latestPlan.DrawnLineCount != 2 || latestPlan.DrawLimitHit)
            {
                throw new InvalidOperationException("Playback cursor at latest end must draw the full cached route.");
            }
        }

        private static void PlayerWorldFootprintsDiagnosticsWrittenToSnapshot()
        {
            PlayerWorldFootprintDiagnostics.ResetForTesting();
            try
            {
                var recordUtc = new DateTime(2026, 6, 16, 14, 0, 0, DateTimeKind.Utc);
                var writeUtc = recordUtc.AddSeconds(2);
                PlayerWorldFootprintDiagnostics.RecordRuntime(new PlayerWorldFootprintRecordResult
                {
                    Succeeded = true,
                    IdentityResolved = true,
                    Recorded = true,
                    PointAdded = true,
                    Flushed = true,
                    Status = "pointAdded",
                    Decision = "pointAdded",
                    Message = "added one point",
                    PairId = "pair-footprint-diagnostics",
                    SegmentCount = 3,
                    PointCount = 9,
                    TimelineStartTicks = 0L,
                    TimelineEndTicks = PlayerWorldFootprintConstants.TicksPerSecond * 60L * 60L,
                    LastPointTileX = 123.5d,
                    LastPointTileY = 456.25d,
                    LastPointDurationTicks = 42L,
                    LastRecordRuntimeTick = 9001L,
                    LastRecordUtc = recordUtc,
                    LastWriteUtc = writeUtc
                });
                PlayerWorldFootprintDiagnostics.RecordRenderCache(
                    "ready",
                    "cache ready",
                    "pair-footprint-diagnostics",
                    "memory",
                    true,
                    3,
                    9,
                    8,
                    12345,
                    true);
                PlayerWorldFootprintDiagnostics.RecordMapDraw(
                    "ready",
                    "draw ready",
                    "pair-footprint-diagnostics",
                    8,
                    5,
                    2,
                    1,
                    3,
                    true);
                PlayerWorldFootprintDiagnostics.RecordPlaybackOverlay(
                    "ready",
                    "overlay ready",
                    "pair-footprint-diagnostics",
                    true,
                    60,
                    160L,
                    100L,
                    200L,
                    false,
                    true,
                    true,
                    true,
                    "dragging");

                var snapshot = RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
                {
                    Initialized = true,
                    Version = "test-footprint-diagnostics"
                });

                if (!snapshot.MapFootprintsDisplayEnabled ||
                    !snapshot.PlayerWorldFootprintsIsRecording ||
                    snapshot.PlayerWorldFootprintsMaxRetainedHours != PlayerWorldFootprintConstants.MaxRetainedHours ||
                    snapshot.PlayerWorldFootprintsSegmentCount != 3 ||
                    snapshot.PlayerWorldFootprintsPointCount != 9 ||
                    snapshot.PlayerWorldFootprintsBreakCount != 2 ||
                    snapshot.MapFootprintsRenderCacheLineCount != 8 ||
                    snapshot.MapFootprintsDrawnLineCount != 5 ||
                    !snapshot.MapFootprintsDrawLimitHit ||
                    !snapshot.MapFootprintsPlaybackPaused ||
                    snapshot.MapFootprintsPlaybackRate != 60 ||
                    snapshot.MapFootprintsPlaybackCursorTicks != 160L ||
                    snapshot.MapFootprintsPlaybackTimelineStartTicks != 100L ||
                    snapshot.MapFootprintsPlaybackLatestTicks != 200L ||
                    snapshot.MapFootprintsPlaybackAtLatest)
                {
                    throw new InvalidOperationException("Footprint diagnostics snapshot did not expose recorder/render/playback state.");
                }

                AssertStringEquals(snapshot.PlayerWorldFootprintsLastDecision, "pointAdded", "footprint snapshot decision");
                AssertStringEquals(snapshot.PlayerWorldFootprintsLastPairId, "pair-footprint-diagnostics", "footprint snapshot pair id");
                AssertStringEquals(snapshot.PlayerWorldFootprintsLastFlushStatus, "saved", "footprint snapshot flush status");
                AssertNear(snapshot.PlayerWorldFootprintsRetainedHours, 1d, "footprint retained hours");
                AssertNear(snapshot.PlayerWorldFootprintsLastPointTileX, 123.5d, "footprint last point x");
                AssertNear(snapshot.PlayerWorldFootprintsLastPointTileY, 456.25d, "footprint last point y");
                AssertNear(snapshot.MapFootprintsPlaybackProgress, 0.6d, "footprint playback progress");

                var json = InvokeDiagnosticSnapshotJson(snapshot);
                AssertContains(json, "\"MapFootprintsDisplayEnabled\": true");
                AssertContains(json, "\"PlayerWorldFootprintsLastDecision\": \"pointAdded\"");
                AssertContains(json, "\"PlayerWorldFootprintsLastPairId\": \"pair-footprint-diagnostics\"");
                AssertContains(json, "\"PlayerWorldFootprintsMaxRetainedHours\": 200");
                AssertContains(json, "\"PlayerWorldFootprintsRetainedHours\": 1");
                AssertContains(json, "\"MapFootprintsRenderCacheStatus\": \"ready\"");
                AssertContains(json, "\"MapFootprintsDrawLimitSkippedLineCount\": 3");
                AssertContains(json, "\"MapFootprintsPlaybackTimelineStartTicks\": 100");
                AssertContains(json, "\"MapFootprintsPlaybackProgress\": 0.6");
                AssertContains(json, "\"MapFootprintsPlaybackAtLatest\": false");
            }
            finally
            {
                PlayerWorldFootprintDiagnostics.ResetForTesting();
            }
        }

        private static PlayerWorldFootprintSegment CreateFootprintSegment(
            string segmentId,
            string breakReason,
            params PlayerWorldFootprintPoint[] points)
        {
            var segment = new PlayerWorldFootprintSegment
            {
                SegmentId = segmentId,
                BreakReason = breakReason,
                Points = new List<PlayerWorldFootprintPoint>(points ?? new PlayerWorldFootprintPoint[0])
            };

            if (segment.Points.Count > 0)
            {
                segment.StartTicks = segment.Points[0].StartTicks;
                segment.EndTicks = segment.Points[0].StartTicks + segment.Points[0].DurationTicks;
                for (var index = 1; index < segment.Points.Count; index++)
                {
                    segment.StartTicks = Math.Min(segment.StartTicks, segment.Points[index].StartTicks);
                    segment.EndTicks = Math.Max(segment.EndTicks, segment.Points[index].StartTicks + segment.Points[index].DurationTicks);
                }
            }

            return segment;
        }

        private static PlayerWorldFootprintPoint CreateFootprintPoint(double tileX, double tileY, long startTicks, long durationTicks)
        {
            return new PlayerWorldFootprintPoint
            {
                TileX = tileX,
                TileY = tileY,
                StartTicks = startTicks,
                DurationTicks = durationTicks
            };
        }

        private static MapFootprintRenderSnapshot CreatePlaybackRenderSnapshot(string pairId, long startTicks, long endTicks)
        {
            return new MapFootprintRenderSnapshot
            {
                DisplayEnabled = true,
                Status = "ready",
                Message = "ready",
                Source = "memory",
                PairId = pairId,
                TimelineStartTicks = startTicks,
                TimelineEndTicks = endTicks,
                DataSignature = pairId == null ? 0 : pairId.GetHashCode(),
                Lines = new MapFootprintRenderLine[0]
            };
        }

        private static GameStateSnapshot CreateFootprintSnapshot(
            double tileX,
            double tileY,
            UiStateSnapshot ui,
            bool playerDead)
        {
            return new GameStateSnapshot
            {
                IsInWorld = true,
                IsInMainMenu = false,
                Player = new PlayerStateSnapshot
                {
                    Exists = true,
                    Active = true,
                    Dead = playerDead,
                    Ghost = false,
                    PositionX = (float)(tileX * 16d),
                    PositionY = (float)(tileY * 16d),
                    CenterX = (float)(tileX * 16d),
                    CenterY = (float)(tileY * 16d)
                },
                Ui = ui ?? new UiStateSnapshot { GameInputAvailable = true }
            };
        }

        private static void ResetFootprintRuntimeForTesting()
        {
            PlayerWorldFootprintService.ResetForTesting();
            PlayerWorldFootprintCache.ResetForTesting();
            PlayerWorldFootprintDiagnostics.ResetForTesting();
            MapFootprintRenderCache.ResetForTesting();
        }
    }
}
