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
        private static void FeatureCatalogExposesDeathHistory()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.MapDeathHistory, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected death history feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented || !feature.DefaultEnabled)
            {
                throw new InvalidOperationException("Death history must be visible, implemented, and default visible.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.MapEnhancement ||
                feature.UserCategory != FeatureUserCategory.MapEnhancement)
            {
                throw new InvalidOperationException("Death history must stay in the map enhancement domain and category.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.None ||
                feature.RequiredActions.Count != 1 ||
                feature.RequiredActions[0] != InputActionKind.None)
            {
                throw new InvalidOperationException("Death history must be a fixed information row without action queue requirements.");
            }
        }

        private static void PlayerWorldDeathHistorySummaryShowsZeroForMissingFiles()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldDeathHistoryCache.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();
                var summary = PlayerWorldDeathHistoryCache.ReadSummaryForPairForTesting(identity.PairId);
                if (!summary.IdentityResolved ||
                    summary.DeathCount != 0 ||
                    summary.PageCount != 0 ||
                    !string.Equals(summary.Status, "missing", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Missing death history files must display zero deaths.");
                }

                AssertStringEquals(
                    LegacyMainWindow.BuildMapDeathHistoryCountTextForTesting(summary),
                    "0 次",
                    "death history missing count text");

                var page = PlayerWorldDeathHistoryCache.ReadPageForPairForTesting(identity.PairId, 0, LegacyMainWindow.GetMapDeathHistoryPageSizeForTesting());
                if (page.Records.Count != 0 || page.PageCount != 0)
                {
                    throw new InvalidOperationException("Missing death history page must be empty.");
                }
            });
        }

        private static void PlayerWorldDeathHistorySummaryPrefersDeathSummaryFile()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldDeathHistoryCache.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();
                var summaryPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathSummaryFileName);
                string message;
                if (!PlayerWorldFeatureDataStore.TryWriteJsonForTesting(
                        summaryPath,
                        new PlayerWorldDeathSummaryFile
                        {
                            PairId = identity.PairId,
                            DeathCount = 9,
                            LastWriteSucceeded = true,
                            LastWriteStatus = "saved"
                        },
                        out message))
                {
                    throw new InvalidOperationException("Failed to write death summary for test: " + message);
                }

                var summary = PlayerWorldDeathHistoryCache.ReadSummaryForPairForTesting(identity.PairId);
                if (summary.DeathCount != 9 || summary.HistoryReadFailed || summary.SummaryReadFailed)
                {
                    throw new InvalidOperationException("Death history row must prefer death-summary.json for count display.");
                }

                AssertStringEquals(
                    LegacyMainWindow.BuildMapDeathHistoryCountTextForTesting(summary),
                    "9 次",
                    "death history summary count text");
            });
        }

        private static void PlayerWorldDeathHistoryCachePaginatesAllRecordsByTime()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldDeathHistoryCache.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();
                var deathPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(deathPath));

                for (var index = 14; index >= 0; index--)
                {
                    WriteDeathHistoryEventLine(
                        identity.PairId,
                        deathPath,
                        "event-" + index,
                        "death-" + index,
                        new DateTime(2026, 6, 14, 1, index, 0, DateTimeKind.Utc));
                }

                var first = PlayerWorldDeathHistoryCache.ReadPageForPairForTesting(identity.PairId, 0, 6);
                if (first.PageCount != 3 || first.Records.Count != 6)
                {
                    throw new InvalidOperationException("Death history must paginate all records.");
                }

                AssertStringEquals(first.Records[0].EventId, "event-0", "death history first page earliest event");
                AssertStringEquals(first.Records[5].EventId, "event-5", "death history first page sixth event");

                var last = PlayerWorldDeathHistoryCache.ReadPageForPairForTesting(identity.PairId, 999, 6);
                if (last.PageIndex != 2 || last.Records.Count != 3)
                {
                    throw new InvalidOperationException("Death history must clamp to the final page and expose all remaining records.");
                }

                AssertStringEquals(last.Records[0].EventId, "event-12", "death history last page first event");
                AssertStringEquals(last.Records[2].EventId, "event-14", "death history last page final event");
            });
        }

        private static void PlayerWorldDeathHistoryCacheSkipsCorruptJsonlLines()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldDeathHistoryCache.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();
                var deathPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(deathPath));
                WriteDeathHistoryEventLine(identity.PairId, deathPath, "event-1", "valid before corrupt", new DateTime(2026, 6, 14, 1, 0, 0, DateTimeKind.Utc));
                File.AppendAllText(deathPath, "{not-json" + Environment.NewLine, Encoding.UTF8);
                WriteDeathHistoryEventLine(identity.PairId, deathPath, "event-2", "valid after corrupt", new DateTime(2026, 6, 14, 1, 1, 0, DateTimeKind.Utc));

                var page = PlayerWorldDeathHistoryCache.ReadPageForPairForTesting(identity.PairId, 0, 10);
                if (!page.HistoryReadFailed ||
                    page.Records.Count != 2 ||
                    page.Message.IndexOf("invalidLine", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new InvalidOperationException("Death history cache must skip corrupt jsonl lines and keep valid records visible.");
                }

                AssertStringEquals(page.Records[1].EventId, "event-2", "death history corrupt line keeps later valid event");
            });
        }

        private static void LegacyMapDeathHistoryPopupRegistersAsModalAndStateMoves()
        {
            PlayerWorldDeathHistoryCache.ResetForTesting();
            LegacyMainWindow.ResetMapDeathHistoryPopupForTesting();
            LegacyUiOverlayCoordinator.Current.ResetForTesting();

            LegacyMainWindow.ToggleMapDeathHistoryPopup();
            if (!LegacyMainWindow.IsMapDeathHistoryPopupOpen() ||
                LegacyMainWindow.GetMapDeathHistoryPageIndex() != 0)
            {
                throw new InvalidOperationException("Death history popup must open at page zero.");
            }

            LegacyMainWindow.MoveMapDeathHistoryPage(2);
            if (LegacyMainWindow.GetMapDeathHistoryPageIndex() != 2)
            {
                throw new InvalidOperationException("Death history page state must move by command.");
            }

            LegacyUiOverlayCoordinator.Current.BeginFrame("map_enhancement");
            var area = LegacyScrollArea.Create(new LegacyUiRect(20, 30, 520, 220), LegacyMainWindow.CalculateMapEnhancementContentHeightForTesting(), 0);
            if (!LegacyMainWindow.RegisterMapDeathHistoryPopupOverlayForTesting(area, new LegacyUiRect(area.Viewport.Right - 80, area.Viewport.Y + 40, 68, 24), 0))
            {
                throw new InvalidOperationException("Death history popup must register through the overlay coordinator.");
            }

            LegacyUiOverlayCoordinator.Current.EndFrame();
            if (LegacyUiOverlayCoordinator.Current.LastStackSignature == 0)
            {
                throw new InvalidOperationException("Death history modal must contribute to the overlay stack signature.");
            }

            LegacyMainWindow.CloseMapDeathHistoryPopup();
            if (LegacyMainWindow.IsMapDeathHistoryPopupOpen())
            {
                throw new InvalidOperationException("Death history popup must close without keeping modal state active.");
            }
        }

        private static void PlayerWorldDeathHistoryDiagnosticsWrittenToSnapshot()
        {
            var snapshot = new DiagnosticSnapshot
            {
                PlayerWorldDeathHistoryLastStatus = "loaded",
                PlayerWorldDeathHistoryLastMessage = "loaded",
                PlayerWorldDeathHistoryLastPairId = "pair-history",
                PlayerWorldDeathHistoryDeathCount = 11,
                PlayerWorldDeathHistoryTotalEventCount = 12,
                PlayerWorldDeathHistoryPageIndex = 1,
                PlayerWorldDeathHistoryPageCount = 2,
                PlayerWorldDeathHistorySummaryReadFailed = true,
                PlayerWorldDeathHistoryPageReadFailed = true,
                PlayerWorldDeathHistoryLastReadUtc = new DateTime(2026, 6, 14, 4, 5, 6, DateTimeKind.Utc)
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"PlayerWorldDeathHistoryLastStatus\": \"loaded\"");
            AssertContains(json, "\"PlayerWorldDeathHistoryLastPairId\": \"pair-history\"");
            AssertContains(json, "\"PlayerWorldDeathHistoryDeathCount\": 11");
            AssertContains(json, "\"PlayerWorldDeathHistoryTotalEventCount\": 12");
            AssertContains(json, "\"PlayerWorldDeathHistoryPageIndex\": 1");
            AssertContains(json, "\"PlayerWorldDeathHistoryPageCount\": 2");
            AssertContains(json, "\"PlayerWorldDeathHistorySummaryReadFailed\": true");
            AssertContains(json, "\"PlayerWorldDeathHistoryPageReadFailed\": true");
            AssertContains(json, "\"PlayerWorldDeathHistoryLastReadUtc\": \"2026-06-14T04:05:06.0000000Z\"");
        }

        private static void WriteDeathHistoryEventLine(string pairId, string path, string eventId, string deathText, DateTime utc)
        {
            var deathEvent = PlayerWorldDeathEventBuilder.BuildForTesting(
                pairId,
                new PlayerWorldDeathSourceSnapshot { SourceKind = PlayerWorldDeathSourceKind.Custom, SourceCustomReason = deathText },
                16f,
                32f,
                deathText,
                1d,
                0,
                false,
                utc);
            deathEvent.EventId = eventId;
            File.AppendAllText(path, PlayerWorldDeathRecorder.SerializeEventForTesting(deathEvent) + Environment.NewLine, Encoding.UTF8);
        }
    }
}
