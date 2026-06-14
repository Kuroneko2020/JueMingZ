using System;
using System.IO;
using System.Text;
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
        private static void FeatureCatalogExposesWorldDayCount()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.MapWorldDayCount, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected world day count feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented || !feature.DefaultEnabled)
            {
                throw new InvalidOperationException("World day count must be visible, implemented, and default visible.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.MapEnhancement ||
                feature.UserCategory != FeatureUserCategory.MapEnhancement)
            {
                throw new InvalidOperationException("World day count must stay in the map enhancement domain and category.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.None ||
                feature.RequiredActions.Count != 1 ||
                feature.RequiredActions[0] != InputActionKind.None)
            {
                throw new InvalidOperationException("World day count must be a fixed information row without action queue requirements.");
            }
        }

        private static void PlayerWorldPlaytimeMissingFileShowsZeroDays()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldPlaytimeService.ResetForTesting();
                PlayerWorldPlaytimeCache.ResetForTesting();
                PlayerWorldPlaytimeDiagnostics.ResetForTesting();

                var identity = BuildResolvedDeathIdentity();
                var read = PlayerWorldPlaytimeCache.ReadForPairForTesting(identity.PairId);
                if (!read.IdentityResolved ||
                    read.WholeDayCount != 0 ||
                    !string.Equals(read.Status, "missing", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Missing playtime.json must display zero world days.");
                }

                AssertStringEquals(
                    LegacyMainWindow.BuildMapWorldDayCountTextForTesting(read),
                    "0 天",
                    "world day count missing text");
                AssertStringEquals(
                    LegacyMainWindow.GetMapWorldDayCountTooltipForTesting(),
                    "当前玩家-世界累计游戏天数",
                    "world day count tooltip");
            });
        }

        private static void PlayerWorldPlaytimeAccumulatesObservedWorldClockDelta()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldPlaytimeService.ResetForTesting();
                PlayerWorldPlaytimeCache.ResetForTesting();
                PlayerWorldPlaytimeDiagnostics.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();

                ProcessPlaytimeSample(identity.PairId, true, 0d, 300d, 0, false);
                ProcessPlaytimeSample(identity.PairId, true, 30000d, 300d, 60, false);
                ProcessPlaytimeSample(identity.PairId, false, 6000d, 300d, 120, false);
                var result = ProcessPlaytimeSample(identity.PairId, true, 6000d, 300d, 180, true);

                if (!result.Accumulated ||
                    result.WholeDayCount != 1 ||
                    result.TotalGameTicks < PlayerWorldPlaytimeConstants.FullDayTicks)
                {
                    throw new InvalidOperationException("World day count must accumulate observed world clock delta into full days.");
                }

                var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.PlaytimeFileName);
                var file = ReadJsonFile<PlayerWorldPlaytimeFile>(path);
                if (file.WholeDayCount != 1 ||
                    file.TotalGameTicks < PlayerWorldPlaytimeConstants.FullDayTicks ||
                    !string.Equals(file.TimeSemantics, PlayerWorldPlaytimeConstants.TimeSemantics, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("playtime.json must persist whole days and the time semantics contract.");
                }

                AssertStringEquals(
                    LegacyMainWindow.BuildMapWorldDayCountTextForTesting(PlayerWorldPlaytimeCache.ReadForPairForTesting(identity.PairId)),
                    "1 天",
                    "world day count persisted text");
            });
        }

        private static void PlayerWorldPlaytimeSwitchesPairWithoutMergingTotals()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldPlaytimeService.ResetForTesting();
                PlayerWorldPlaytimeCache.ResetForTesting();

                var first = BuildResolvedDeathIdentity();
                PlayerWorldIdentityResolution second;
                if (!PlayerWorldIdentityResolver.TryResolveForTesting(
                        BuildIdentityFacts(
                            @"C:\Players\PlaytimeB.plr",
                            "Playtime B",
                            @"C:\Worlds\PlaytimeB.wld",
                            "Playtime World B",
                            "77777777-7777-7777-7777-777777777777",
                            "playtime-b-map",
                            777),
                        out second))
                {
                    throw new InvalidOperationException("Expected second playtime identity to resolve.");
                }

                ProcessPlaytimeSample(first.PairId, true, 1000d, 60d, 0, false);
                ProcessPlaytimeSample(first.PairId, true, 7000d, 60d, 60, true);
                ProcessPlaytimeSample(second.PairId, true, 2000d, 60d, 120, false);
                ProcessPlaytimeSample(second.PairId, true, 10000d, 60d, 180, true);

                var firstPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(first.PairId, PlayerWorldFeatureDataRoot.PlaytimeFileName);
                var secondPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(second.PairId, PlayerWorldFeatureDataRoot.PlaytimeFileName);
                var firstFile = ReadJsonFile<PlayerWorldPlaytimeFile>(firstPath);
                var secondFile = ReadJsonFile<PlayerWorldPlaytimeFile>(secondPath);

                AssertNear(firstFile.TotalGameTicks, 6000d, "first pair playtime ticks");
                AssertNear(secondFile.TotalGameTicks, 8000d, "second pair playtime ticks");
                AssertStringEquals(firstFile.PairId, first.PairId, "first pair id");
                AssertStringEquals(secondFile.PairId, second.PairId, "second pair id");
            });
        }

        private static void PlayerWorldPlaytimeRejectsBackwardsAndAbnormalClockJumps()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldPlaytimeService.ResetForTesting();
                PlayerWorldPlaytimeDiagnostics.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();

                ProcessPlaytimeSample(identity.PairId, true, 1000d, 1d, 0, false);
                var backwards = ProcessPlaytimeSample(identity.PairId, true, 800d, 1d, 60, true);
                if (backwards.Accumulated ||
                    backwards.TotalGameTicks != 0d ||
                    !string.Equals(backwards.LastSkippedDeltaReason, "timeBackwards", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("World day count must skip backwards time without accumulating.");
                }

                PlayerWorldPlaytimeService.ResetForTesting();
                ProcessPlaytimeSample(identity.PairId, true, 1000d, 1d, 120, false);
                var jump = ProcessPlaytimeSample(identity.PairId, true, 40000d, 1d, 180, true);
                if (jump.Accumulated ||
                    jump.TotalGameTicks != 0d ||
                    !string.Equals(jump.LastSkippedDeltaReason, "abnormalJump", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("World day count must skip abnormal world-clock jumps.");
                }
            });
        }

        private static void PlayerWorldPlaytimeSafeWriteFailureKeepsExistingFile()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldPlaytimeService.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();
                var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.PlaytimeFileName);
                string message;
                if (!PlayerWorldFeatureDataStore.TryWriteJsonForTesting(
                        path,
                        new PlayerWorldPlaytimeFile
                        {
                            PairId = identity.PairId,
                            TotalGameTicks = 123d,
                            WholeDayCount = 0,
                            TimeSemantics = PlayerWorldPlaytimeConstants.TimeSemantics
                        },
                        out message))
                {
                    throw new InvalidOperationException("Expected initial playtime write to succeed: " + message);
                }

                var originalJson = File.ReadAllText(path, Encoding.UTF8);
                PlayerWorldPlaytimeUpdateResult result;
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    ProcessPlaytimeSample(identity.PairId, true, 0d, 60d, 0, false);
                    result = ProcessPlaytimeSample(identity.PairId, true, 6000d, 60d, 60, true);
                }

                if (!result.WriteFailed)
                {
                    throw new InvalidOperationException("Expected locked playtime replacement to fail.");
                }

                var afterFailureJson = File.ReadAllText(path, Encoding.UTF8);
                AssertStringEquals(afterFailureJson, originalJson, "playtime file bytes after failed replacement");
            });
        }

        private static void PlayerWorldPlaytimeClockReaderReadsTerrariaWorldClock()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousDayTime = Terraria.Main.dayTime;
            var previousTime = Terraria.Main.time;
            var previousDayRate = Terraria.Main.dayRate;
            try
            {
                Terraria.Main.dayTime = false;
                Terraria.Main.time = 1234.5d;
                Terraria.Main.dayRate = 60;

                PlayerWorldClockSample sample;
                string message;
                if (!PlayerWorldPlaytimeClockReader.TryReadCurrent(77, out sample, out message))
                {
                    throw new InvalidOperationException("Expected world clock reader to read fake Terraria.Main: " + message);
                }

                if (sample.DayTime ||
                    Math.Abs(sample.WorldTime - 1234.5d) > 0.0001d ||
                    Math.Abs(sample.DayRate - 60d) > 0.0001d ||
                    sample.RuntimeTick != 77)
                {
                    throw new InvalidOperationException("World clock reader must read dayTime, time, dayRate, and runtime tick.");
                }
            }
            finally
            {
                Terraria.Main.dayTime = previousDayTime;
                Terraria.Main.time = previousTime;
                Terraria.Main.dayRate = previousDayRate;
                restoreRuntimeTypes();
            }
        }

        private static void PlayerWorldPlaytimeDiagnosticsWrittenToSnapshot()
        {
            var snapshot = new DiagnosticSnapshot
            {
                PlayerWorldPlaytimeLastStatus = "accumulated",
                PlayerWorldPlaytimeLastMessage = "pending flush",
                PlayerWorldPlaytimeLastPairId = "pair-playtime",
                PlayerWorldPlaytimeTotalGameTicks = 90000d,
                PlayerWorldPlaytimeWholeDayCount = 1,
                PlayerWorldPlaytimeReadFailed = true,
                PlayerWorldPlaytimeWriteFailed = true,
                PlayerWorldPlaytimeLastDeltaGameTicks = 3600d,
                PlayerWorldPlaytimeLastSkippedDeltaReason = "abnormalJump",
                PlayerWorldPlaytimeLastSampleUtc = new DateTime(2026, 6, 14, 5, 6, 7, DateTimeKind.Utc),
                PlayerWorldPlaytimeLastWriteUtc = new DateTime(2026, 6, 14, 5, 7, 8, DateTimeKind.Utc)
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"PlayerWorldPlaytimeLastStatus\": \"accumulated\"");
            AssertContains(json, "\"PlayerWorldPlaytimeLastPairId\": \"pair-playtime\"");
            AssertContains(json, "\"PlayerWorldPlaytimeTotalGameTicks\": 90000");
            AssertContains(json, "\"PlayerWorldPlaytimeWholeDayCount\": 1");
            AssertContains(json, "\"PlayerWorldPlaytimeReadFailed\": true");
            AssertContains(json, "\"PlayerWorldPlaytimeWriteFailed\": true");
            AssertContains(json, "\"PlayerWorldPlaytimeLastDeltaGameTicks\": 3600");
            AssertContains(json, "\"PlayerWorldPlaytimeLastSkippedDeltaReason\": \"abnormalJump\"");
            AssertContains(json, "\"PlayerWorldPlaytimeLastSampleUtc\": \"2026-06-14T05:06:07.0000000Z\"");
            AssertContains(json, "\"PlayerWorldPlaytimeLastWriteUtc\": \"2026-06-14T05:07:08.0000000Z\"");
        }

        private static PlayerWorldPlaytimeUpdateResult ProcessPlaytimeSample(
            string pairId,
            bool dayTime,
            double worldTime,
            double dayRate,
            long runtimeTick,
            bool forceFlush)
        {
            return PlayerWorldPlaytimeService.ProcessSampleForTesting(
                pairId,
                PlayerWorldPlaytimeClockReader.BuildSampleForTesting(
                    dayTime,
                    worldTime,
                    dayRate,
                    runtimeTick,
                    new DateTime(2026, 6, 14, 6, 0, 0, DateTimeKind.Utc).AddSeconds(runtimeTick)),
                forceFlush);
        }
    }
}
