using System;
using System.Collections.Generic;
using System.IO;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
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
                    "当前玩家-世界地图揭示区域占比",
                    "revealed area ratio tooltip");
            });
        }

        private static void PlayerWorldExplorationCursorResumesFromPersistedSummary()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = FakeExplorationMapReader.CreateStriped(100, 50);

                var first = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 100, 50, reader, 0, true);
                if (first.TilesScannedThisTick != PlayerWorldExplorationConstants.ScanTileBudget ||
                    first.ScanComplete ||
                    first.NextTileIndex != PlayerWorldExplorationConstants.ScanTileBudget)
                {
                    throw new InvalidOperationException("First revealed-area chunk must persist the scan cursor at the fixed budget.");
                }

                PlayerWorldExplorationService.ResetForTesting();
                PlayerWorldExplorationCache.ResetForTesting();
                var second = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 100, 50, reader, 10, true);
                if (!second.ScanComplete ||
                    second.ScannedTileCount != 5000 ||
                    second.NextTileIndex != 5000 ||
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

        private static void PlayerWorldExplorationScanBudgetIsFixed()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                ResetExplorationTestState();
                var identity = BuildResolvedDeathIdentity();
                var reader = new FakeExplorationMapReader(200, 100);

                var result = PlayerWorldExplorationService.ProcessScanForTesting(identity.PairId, 200, 100, reader, 0, false);
                if (result.TilesScannedThisTick != PlayerWorldExplorationConstants.ScanTileBudget ||
                    result.ScannedTileCount != PlayerWorldExplorationConstants.ScanTileBudget ||
                    result.ScanComplete)
                {
                    throw new InvalidOperationException("Revealed-area scan must keep a fixed per-tick tile budget on large worlds.");
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
            AssertContains(json, "\"PlayerWorldExplorationRevealedPercent\": 40.96");
            AssertContains(json, "\"PlayerWorldExplorationScanComplete\": false");
            AssertContains(json, "\"PlayerWorldExplorationReadFailed\": true");
            AssertContains(json, "\"PlayerWorldExplorationWriteFailed\": true");
            AssertContains(json, "\"PlayerWorldExplorationLastScanUtc\": \"2026-06-14T06:07:08.0000000Z\"");
            AssertContains(json, "\"PlayerWorldExplorationLastCompletedScanUtc\": \"2026-06-14T06:08:09.0000000Z\"");
            AssertContains(json, "\"PlayerWorldExplorationLastWriteUtc\": \"2026-06-14T06:09:10.0000000Z\"");
        }

        private static void ResetExplorationTestState()
        {
            PlayerWorldExplorationService.ResetForTesting();
            PlayerWorldExplorationCache.ResetForTesting();
            PlayerWorldExplorationDiagnostics.ResetForTesting();
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
