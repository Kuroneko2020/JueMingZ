using System;
using System.IO;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.Records;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FeatureCatalogExposesPersistentDeathMarkersConfig()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.MapPersistentDeathMarkers, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected persistent death markers feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Persistent death markers must be visible and implemented.");
            }

            if (feature.DefaultEnabled)
            {
                throw new InvalidOperationException("Persistent death markers must default to disabled.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.MapEnhancement ||
                feature.UserCategory != FeatureUserCategory.MapEnhancement)
            {
                throw new InvalidOperationException("Persistent death markers must stay in the map enhancement domain and category.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.None ||
                feature.RequiredActions.Count != 1 ||
                feature.RequiredActions[0] != InputActionKind.None)
            {
                throw new InvalidOperationException("Persistent death markers must be a standard switch without action queue requirements.");
            }
        }

        private static void PersistentDeathMarkersConfigDefaultsAndFeatureSync()
        {
            var settings = AppSettings.CreateDefault();
            if (settings.MapPersistentDeathMarkersEnabled)
            {
                throw new InvalidOperationException("Persistent death markers must default to off.");
            }

            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (snapshot.MapPersistentDeathMarkersEnabled)
            {
                throw new InvalidOperationException("Runtime snapshot must carry the default disabled persistent death marker flag.");
            }

            settings.MapPersistentDeathMarkersEnabled = true;
            snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (!snapshot.MapPersistentDeathMarkersEnabled)
            {
                throw new InvalidOperationException("Runtime snapshot must carry enabled persistent death marker flag.");
            }

            var restore = PushTemporaryConfigDirectory("map-persistent-death-markers");
            try
            {
                WriteConfigJson(ConfigService.AppSettingsPath, settings);
                WriteConfigJson(ConfigService.FeatureSettingsPath, FeatureSettings.CreateDefault());
                WriteConfigJson(ConfigService.HotkeySettingsPath, HotkeySettings.CreateDefault());
                ConfigService.Initialize();

                bool enabled;
                if (ConfigService.FeatureSettings == null ||
                    ConfigService.FeatureSettings.EnabledByFeatureId == null ||
                    !ConfigService.FeatureSettings.EnabledByFeatureId.TryGetValue(FeatureIds.MapPersistentDeathMarkers, out enabled) ||
                    !enabled)
                {
                    throw new InvalidOperationException("Feature settings must synchronize persistent death marker config.");
                }

                if (ConfigService.CountAppSettingsEnabledFeatures() <= 0)
                {
                    throw new InvalidOperationException("Persistent death markers must count as an enabled appsettings feature.");
                }
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void LegacyMapPersistentDeathMarkersTooltipMatchesRequestedWording()
        {
            var tooltips = LegacyMainWindow.GetMapPersistentDeathMarkersButtonTooltipsForTesting();
            if (tooltips == null || tooltips.Length != 2)
            {
                throw new InvalidOperationException("Persistent death marker tooltip test contract must expose two button slots.");
            }

            AssertStringEquals(tooltips[0], "大地图常驻显示死亡点（仅显示最近256次）", "persistent death markers on tooltip");
            AssertStringEquals(tooltips[1], string.Empty, "persistent death markers off tooltip");
        }

        private static void PlayerWorldDeathMarkerCacheReadsOwnJsonlAndLimitsMarkers()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldDeathMarkerCache.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();
                var deathPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(deathPath));

                WriteDeathMarkerEventLine(identity.PairId, deathPath, "event-1", 16f, 32f, "first");
                WriteDeathMarkerEventLine(identity.PairId, deathPath, "event-2", 48f, 64f, "second");
                WriteDeathMarkerEventLine(identity.PairId, deathPath, "event-3", 80f, 96f, "third");

                var result = PlayerWorldDeathMarkerCache.ReadMarkersForPairForTesting(identity.PairId, 2);
                if (!result.Succeeded ||
                    result.HistoryReadFailed ||
                    !result.CulledByLimit ||
                    result.TotalEventCount != 3 ||
                    result.MarkerCount != 2 ||
                    result.Markers.Count != 2)
                {
                    throw new InvalidOperationException("Death marker cache must read own jsonl and keep the newest capped markers.");
                }

                AssertStringEquals(result.Markers[0].EventId, "event-2", "death marker limit keeps event-2");
                AssertStringEquals(result.Markers[1].EventId, "event-3", "death marker limit keeps event-3");
                AssertContains(result.Markers[1].Tooltip, "third");
                if (Math.Abs(result.Markers[1].TilePosition.X - 5f) > 0.001f ||
                    Math.Abs(result.Markers[1].TilePosition.Y - 6f) > 0.001f)
                {
                    throw new InvalidOperationException("Death marker cache must convert world coordinates to tile coordinates.");
                }

                if (PlayerWorldDeathMarkerCache.DefaultMaxMarkers != 256)
                {
                    throw new InvalidOperationException("Persistent death marker display cache must stay capped at the latest 256 deaths.");
                }

                for (var index = 4; index <= 257; index++)
                {
                    WriteDeathMarkerEventLine(identity.PairId, deathPath, "event-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), 16f * index, 32f, "death-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                result = PlayerWorldDeathMarkerCache.ReadMarkersForPairForTesting(identity.PairId, PlayerWorldDeathMarkerCache.DefaultMaxMarkers);
                if (!result.Succeeded ||
                    !result.CulledByLimit ||
                    result.TotalEventCount != 257 ||
                    result.MarkerCount != 256 ||
                    result.Markers.Count != 256)
                {
                    throw new InvalidOperationException("Persistent death marker display must keep only the newest 256 markers while preserving the full death history file.");
                }

                AssertStringEquals(result.Markers[0].EventId, "event-2", "death marker 257-display first visible event");
                AssertStringEquals(result.Markers[result.Markers.Count - 1].EventId, "event-257", "death marker 257-display last visible event");
            });
        }

        private static void PlayerWorldDeathMarkerCacheFlagsCorruptJsonlWithoutThrowing()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldDeathMarkerCache.ResetForTesting();
                var identity = BuildResolvedDeathIdentity();
                var deathPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(deathPath));
                WriteDeathMarkerEventLine(identity.PairId, deathPath, "event-1", 16f, 32f, "valid before corrupt");
                File.AppendAllText(deathPath, "{not-json" + Environment.NewLine, Encoding.UTF8);

                var result = PlayerWorldDeathMarkerCache.ReadMarkersForPairForTesting(identity.PairId, 32);
                if (result.Succeeded ||
                    !result.HistoryReadFailed ||
                    result.MarkerCount != 1 ||
                    result.Message.IndexOf("invalidLine", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new InvalidOperationException("Death marker cache must flag corrupt jsonl and preserve already parsed markers.");
                }
            });
        }

        private static void PlayerWorldDeathMarkerDiagnosticsWrittenToSnapshot()
        {
            var snapshot = new DiagnosticSnapshot
            {
                PlayerWorldDeathMarkerLayerInstalled = true,
                PlayerWorldDeathMarkerLayerMessage = "layer installed for test",
                PlayerWorldDeathMarkerLastStatus = "loaded",
                PlayerWorldDeathMarkerLastMessage = "loaded",
                PlayerWorldDeathMarkerLastPairId = "pair-marker",
                PlayerWorldDeathMarkerCachedCount = 7,
                PlayerWorldDeathMarkerDrawnCount = 3,
                PlayerWorldDeathMarkerCulledByLimit = true,
                PlayerWorldDeathMarkerHistoryReadFailed = true,
                PlayerWorldDeathMarkerLastDrawUtc = new DateTime(2026, 6, 14, 3, 4, 5, DateTimeKind.Utc)
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"PlayerWorldDeathMarkerLayerInstalled\": true");
            AssertContains(json, "\"PlayerWorldDeathMarkerLayerMessage\": \"layer installed for test\"");
            AssertContains(json, "\"PlayerWorldDeathMarkerLastStatus\": \"loaded\"");
            AssertContains(json, "\"PlayerWorldDeathMarkerLastPairId\": \"pair-marker\"");
            AssertContains(json, "\"PlayerWorldDeathMarkerCachedCount\": 7");
            AssertContains(json, "\"PlayerWorldDeathMarkerDrawnCount\": 3");
            AssertContains(json, "\"PlayerWorldDeathMarkerCulledByLimit\": true");
            AssertContains(json, "\"PlayerWorldDeathMarkerHistoryReadFailed\": true");
            AssertContains(json, "\"PlayerWorldDeathMarkerLastDrawUtc\": \"2026-06-14T03:04:05.0000000Z\"");
        }

        private static void WriteDeathMarkerEventLine(string pairId, string path, string eventId, float worldX, float worldY, string deathText)
        {
            var deathEvent = PlayerWorldDeathEventBuilder.BuildForTesting(
                pairId,
                new PlayerWorldDeathSourceSnapshot { SourceKind = PlayerWorldDeathSourceKind.Custom, SourceCustomReason = deathText },
                worldX,
                worldY,
                deathText,
                1d,
                0,
                false,
                new DateTime(2026, 6, 14, 1, 2, 3, DateTimeKind.Utc));
            deathEvent.EventId = eventId;
            File.AppendAllText(path, PlayerWorldDeathRecorder.SerializeEventForTesting(deathEvent) + Environment.NewLine, Encoding.UTF8);
        }
    }
}
