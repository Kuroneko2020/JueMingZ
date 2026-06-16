using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Automation.MapEnhancement;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.Hooks;
using JueMingZ.Input;
using JueMingZ.Records;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using Microsoft.Xna.Framework;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FeatureCatalogExposesMapCustomMarkersConfig()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.MapCustomMarkers, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected map custom markers feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented || feature.DefaultEnabled)
            {
                throw new InvalidOperationException("Map custom markers must be visible, implemented, and disabled by default.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.MapEnhancement ||
                feature.UserCategory != FeatureUserCategory.MapEnhancement ||
                feature.MultiplayerSupport != FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
            {
                throw new InvalidOperationException("Map custom markers metadata must stay in the frozen map enhancement bucket.");
            }

            if (feature.RequiredActions.Count != 1 ||
                feature.RequiredActions[0] != InputActionKind.None ||
                feature.RequiredGameState.Count != 3 ||
                !feature.RequiredGameState.Contains(GameStateKind.Map) ||
                !feature.RequiredGameState.Contains(GameStateKind.World) ||
                !feature.RequiredGameState.Contains(GameStateKind.UiState))
            {
                throw new InvalidOperationException("Map custom markers must not require actions and must require map/world/UI state.");
            }

            FeatureDefinition enhancedMap;
            if (!registry.TryGet("map.enhanced_map", out enhancedMap) ||
                enhancedMap == null ||
                enhancedMap.IsImplemented)
            {
                throw new InvalidOperationException("map.enhanced_map must remain an unimplemented placeholder.");
            }
        }

        private static void MapCustomMarkersConfigDefaultsAndFeatureSync()
        {
            var settings = AppSettings.CreateDefault();
            if (settings.MapCustomMarkersEnabled)
            {
                throw new InvalidOperationException("Map custom markers must default to off.");
            }

            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (snapshot.MapCustomMarkersEnabled)
            {
                throw new InvalidOperationException("Runtime snapshot must carry default disabled map custom marker flag.");
            }

            settings.MapCustomMarkersEnabled = true;
            snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (!snapshot.MapCustomMarkersEnabled)
            {
                throw new InvalidOperationException("Runtime snapshot must carry enabled map custom marker flag.");
            }

            var restore = PushTemporaryConfigDirectory("map-custom-markers");
            try
            {
                WriteConfigJson(ConfigService.AppSettingsPath, settings);
                WriteConfigJson(ConfigService.FeatureSettingsPath, FeatureSettings.CreateDefault());
                WriteConfigJson(ConfigService.HotkeySettingsPath, HotkeySettings.CreateDefault());
                ConfigService.Initialize();

                bool enabled;
                if (ConfigService.FeatureSettings == null ||
                    ConfigService.FeatureSettings.EnabledByFeatureId == null ||
                    !ConfigService.FeatureSettings.EnabledByFeatureId.TryGetValue(FeatureIds.MapCustomMarkers, out enabled) ||
                    !enabled)
                {
                    throw new InvalidOperationException("Feature settings must synchronize map custom marker config.");
                }

                if (ConfigService.CountAppSettingsEnabledFeatures() <= 0)
                {
                    throw new InvalidOperationException("Map custom markers must count as an enabled appsettings feature.");
                }
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void PlayerWorldMapMarkersPathUsesPlayerWorldPair()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var path = PlayerWorldMapMarkerStore.BuildPathForTesting("pair-map-marker");
                AssertStringEquals(Path.GetFileName(path), PlayerWorldFeatureDataRoot.MapMarkersFileName, "map marker file name");
                AssertContains(path, Path.Combine("player-worlds", "pair-map-marker"));
            });
        }

        private static void PlayerWorldMapMarkersRoundTripNormalizesNameAndIcon()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldMapMarkerCache.ResetForTesting();
                var marker = CreateMapMarker("marker-1", 123, 456, 9999, "12345678901", 0);
                var write = PlayerWorldMapMarkerStore.SaveForPairForTesting(
                    "pair-roundtrip",
                    8400,
                    2400,
                    new List<PlayerWorldMapMarkerRecord> { marker },
                    "roundtrip");

                if (!write.Succeeded || write.MarkerCount != 1)
                {
                    throw new InvalidOperationException("Map marker save should succeed.");
                }

                var read = PlayerWorldMapMarkerStore.ReadForPairForTesting("pair-roundtrip");
                if (!read.Succeeded ||
                    read.MarkerCount != 1 ||
                    read.WorldSizeX != 8400 ||
                    read.WorldSizeY != 2400)
                {
                    throw new InvalidOperationException("Map marker read should roundtrip world size and marker count.");
                }

                var saved = read.Markers[0];
                AssertStringEquals(saved.MarkerId, "marker-1", "marker id");
                AssertStringEquals(saved.Name, "1234567890", "trimmed marker name");
                if (saved.IconItemId != PlayerWorldMapMarkerConstants.DefaultIconItemId)
                {
                    throw new InvalidOperationException("Unknown marker icon must fall back to torch.");
                }
            });
        }

        private static void PlayerWorldMapMarkersLegacyFallenStarIconMapsToBed()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                if (PlayerWorldMapMarkerConstants.IsAllowedIconItemId(PlayerWorldMapMarkerConstants.LegacyFallenStarIconItemId) ||
                    PlayerWorldMapMarkerConstants.NormalizeIconItemId(PlayerWorldMapMarkerConstants.LegacyFallenStarIconItemId) != PlayerWorldMapMarkerConstants.ReplacementBedIconItemId ||
                    PlayerWorldMapMarkerConstants.NormalizeIconItemId(PlayerWorldMapMarkerConstants.ReplacementBedIconItemId) != PlayerWorldMapMarkerConstants.ReplacementBedIconItemId)
                {
                    throw new InvalidOperationException("Legacy fallen-star marker icons must normalize to the replacement bed icon without keeping 75 in the active whitelist.");
                }

                var marker = CreateMapMarker("legacy-fallen-star", 1, 2, PlayerWorldMapMarkerConstants.LegacyFallenStarIconItemId, "legacy", 0);
                var write = PlayerWorldMapMarkerStore.SaveForPairForTesting(
                    "pair-legacy-icon",
                    100,
                    100,
                    new List<PlayerWorldMapMarkerRecord> { marker },
                    "legacyIcon");
                if (!write.Succeeded)
                {
                    throw new InvalidOperationException("Legacy map marker icon seed save should succeed.");
                }

                var read = PlayerWorldMapMarkerStore.ReadForPairForTesting("pair-legacy-icon");
                if (read.Markers[0].IconItemId != PlayerWorldMapMarkerConstants.ReplacementBedIconItemId ||
                    !string.Equals(PlayerWorldMapMarkerStyles.GetDisplayName(PlayerWorldMapMarkerConstants.LegacyFallenStarIconItemId), "床", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Legacy fallen-star markers must display and persist through the replacement bed icon.");
                }
            });
        }

        private static void PlayerWorldMapMarkersAllowEmptyName()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var marker = CreateMapMarker("marker-empty-name", 1, 2, 48, string.Empty, 0);
                var write = PlayerWorldMapMarkerStore.SaveForPairForTesting(
                    "pair-empty-name",
                    100,
                    200,
                    new List<PlayerWorldMapMarkerRecord> { marker },
                    "emptyName");
                if (!write.Succeeded)
                {
                    throw new InvalidOperationException("Empty marker name should save.");
                }

                var read = PlayerWorldMapMarkerStore.ReadForPairForTesting("pair-empty-name");
                AssertStringEquals(read.Markers[0].Name, string.Empty, "empty marker name");
            });
        }

        private static void PlayerWorldMapMarkersCreatedMarkerDefaultsToTimestampName()
        {
            var point = new MapCustomMarkerMapPoint
            {
                TileX = 320,
                TileY = 200,
                ScreenX = 740,
                ScreenY = 460,
                ScreenWidth = 1280,
                ScreenHeight = 720,
                WorldSizeX = 8400,
                WorldSizeY = 2400,
                TransformSource = "fullscreenDrawMouse",
                FallbackReason = string.Empty,
                MapTopLeftX = 99f,
                MapTopLeftY = 55f,
                MapScale = 2f,
                CurrentMapFullscreenPosX = 4200f,
                CurrentMapFullscreenPosY = 1200f,
                CurrentMapScale = 2f,
                CurrentGameUpdateCount = 456,
                TransformAgeUpdates = 1
            };

            var placement = MapCustomMarkerInteractionService.CreatePlacementForTesting(point);
            var marker = MapCustomMarkerInteractionService.BuildMarkerRecordForTesting(
                placement,
                48,
                new DateTime(2026, 6, 16, 1, 2, 3, DateTimeKind.Utc),
                new DateTime(2026, 6, 16, 9, 5, 0));

            if (marker == null ||
                marker.TileX != 320 ||
                marker.TileY != 200 ||
                marker.IconItemId != 48)
            {
                throw new InvalidOperationException("Map marker record creation must preserve the frozen placement and selected icon.");
            }

            AssertStringEquals(marker.Name, "2606160905", "default marker timestamp name");
            AssertStringEquals(
                marker.Name,
                PlayerWorldMapMarkerConstants.FormatDefaultName(new DateTime(2026, 6, 16, 9, 5, 0)),
                "default marker name helper");
            AssertStringEquals(marker.CreatedUtc, "2026-06-16T01:02:03.0000000Z", "created marker CreatedUtc");
            AssertStringEquals(marker.UpdatedUtc, marker.CreatedUtc, "created marker UpdatedUtc");
            if (marker.Name.Length != PlayerWorldMapMarkerConstants.MaxNameTextUnits)
            {
                throw new InvalidOperationException("Default marker timestamp name must fit the 10-text-unit name limit exactly.");
            }
        }

        private static void PlayerWorldMapMarkersSeparatePairs()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldMapMarkerStore.SaveForPairForTesting(
                    "pair-first",
                    100,
                    100,
                    new List<PlayerWorldMapMarkerRecord> { CreateMapMarker("first", 1, 1, 8, "first", 0) },
                    "seed");
                PlayerWorldMapMarkerStore.SaveForPairForTesting(
                    "pair-second",
                    100,
                    100,
                    new List<PlayerWorldMapMarkerRecord> { CreateMapMarker("second", 2, 2, 48, "second", 0) },
                    "seed");

                var first = PlayerWorldMapMarkerStore.ReadForPairForTesting("pair-first");
                var second = PlayerWorldMapMarkerStore.ReadForPairForTesting("pair-second");
                AssertStringEquals(first.Markers[0].MarkerId, "first", "first pair marker");
                AssertStringEquals(second.Markers[0].MarkerId, "second", "second pair marker");
            });
        }

        private static void PlayerWorldMapMarkersSaveFailureKeepsExistingFile()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var pairId = "pair-save-failure";
                var path = PlayerWorldMapMarkerStore.BuildPathForTesting(pairId);
                var original = new List<PlayerWorldMapMarkerRecord>
                {
                    CreateMapMarker("original", 1, 2, 8, "old", 0)
                };
                var first = PlayerWorldMapMarkerStore.SaveForPairForTesting(pairId, 100, 100, original, "seed");
                if (!first.Succeeded)
                {
                    throw new InvalidOperationException("Initial marker save should succeed.");
                }

                var originalJson = File.ReadAllText(path, Encoding.UTF8);
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var failed = PlayerWorldMapMarkerStore.SaveForPairForTesting(
                        pairId,
                        100,
                        100,
                        new List<PlayerWorldMapMarkerRecord> { CreateMapMarker("new", 3, 4, 48, "new", 0) },
                        "lockedWrite");
                    if (failed.Succeeded || !string.Equals(failed.Status, "writeFailed", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Locked marker file should fail safe write.");
                    }
                }

                var afterFailureJson = File.ReadAllText(path, Encoding.UTF8);
                AssertStringEquals(afterFailureJson, originalJson, "map marker file bytes after failed replacement");
            });
        }

        private static void PlayerWorldMapMarkersRejectLimitOverflow()
        {
            if (PlayerWorldMapMarkerConstants.MaxMarkersPerPair != 120 ||
                PlayerWorldMapMarkerConstants.MaxCachedMarkers != PlayerWorldMapMarkerConstants.MaxMarkersPerPair)
            {
                throw new InvalidOperationException("Map marker per-pair and cache limits must stay at 120.");
            }

            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var markers = new List<PlayerWorldMapMarkerRecord>();
                for (var index = 0; index < PlayerWorldMapMarkerConstants.MaxMarkersPerPair + 1; index++)
                {
                    markers.Add(CreateMapMarker("marker-" + index, index, index, 8, string.Empty, index));
                }

                var result = PlayerWorldMapMarkerStore.SaveForPairForTesting("pair-limit", 100, 100, markers, "limit");
                if (result.Succeeded || !result.LimitExceeded || !string.Equals(result.Status, "limitExceeded", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Map marker store must reject files above per-pair marker limit.");
                }

                var path = PlayerWorldMapMarkerStore.BuildPathForTesting("pair-limit");
                if (File.Exists(path))
                {
                    throw new InvalidOperationException("Limit overflow must not write map marker file.");
                }
            });
        }

        private static void PlayerWorldMapMarkersCorruptJsonFailsSoft()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var path = PlayerWorldMapMarkerStore.BuildPathForTesting("pair-corrupt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{not-json", Encoding.UTF8);

                var read = PlayerWorldMapMarkerStore.ReadForPairForTesting("pair-corrupt");
                if (read.Succeeded || !read.ReadFailed || !string.Equals(read.Status, "readFailed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Corrupt map marker json must fail soft without throwing.");
                }
            });
        }

        private static void PlayerWorldMapMarkersRenameAndDeleteUpdateStore()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var pairId = "pair-rename-delete";
                var seed = PlayerWorldMapMarkerStore.SaveForPairForTesting(
                    pairId,
                    4200,
                    1200,
                    new List<PlayerWorldMapMarkerRecord>
                    {
                        CreateMapMarker("rename-me", 10, 11, 48, "old", 0),
                        CreateMapMarker("keep-me", 12, 13, 8, string.Empty, 1)
                    },
                    "seed");
                if (!seed.Succeeded)
                {
                    throw new InvalidOperationException("Map marker seed save should succeed.");
                }

                var rename = PlayerWorldMapMarkerStore.RenameMarkerForPairForTesting(pairId, "rename-me", "12345678901");
                if (!rename.Succeeded || !rename.Changed || !string.Equals(rename.Operation, "rename", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Map marker rename should save a changed file.");
                }

                var afterRename = PlayerWorldMapMarkerStore.ReadForPairForTesting(pairId);
                AssertStringEquals(afterRename.Markers[0].Name, "1234567890", "renamed marker normalized name");
                if (string.Equals(afterRename.Markers[0].UpdatedUtc, afterRename.Markers[0].CreatedUtc, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Map marker rename should refresh UpdatedUtc.");
                }

                var delete = PlayerWorldMapMarkerStore.DeleteMarkerForPairForTesting(pairId, "rename-me");
                if (!delete.Succeeded || !delete.Changed || !string.Equals(delete.Operation, "delete", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Map marker delete should save a changed file.");
                }

                var afterDelete = PlayerWorldMapMarkerStore.ReadForPairForTesting(pairId);
                if (afterDelete.MarkerCount != 1 ||
                    !string.Equals(afterDelete.Markers[0].MarkerId, "keep-me", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Map marker delete must remove only the requested marker.");
                }
            });
        }

        private static void PlayerWorldMapMarkersDiagnosticsWrittenToSnapshot()
        {
            var snapshot = new DiagnosticSnapshot
            {
                PlayerWorldMapMarkersEnabled = true,
                PlayerWorldMapMarkersLastStatus = "saved",
                PlayerWorldMapMarkersLastMessage = "saved",
                PlayerWorldMapMarkersLastPairId = "pair-marker",
                PlayerWorldMapMarkersCount = 3,
                PlayerWorldMapMarkersReadFailed = true,
                PlayerWorldMapMarkersWriteFailed = true,
                PlayerWorldMapMarkersLimitExceeded = true,
                PlayerWorldMapMarkersCulledByCacheLimit = true,
                PlayerWorldMapMarkersLastOperation = "rename",
                PlayerWorldMapMarkersLastUiAction = "jump",
                PlayerWorldMapMarkersLastJumpResult = "jumped",
                MapMarkerLastJumpRequestedTileX = 101,
                MapMarkerLastJumpRequestedTileY = 202,
                MapMarkerLastJumpWrittenMapPosX = 101,
                MapMarkerLastJumpWrittenMapPosY = 202,
                MapMarkerLastJumpScale = 2,
                MapMarkerLastJumpReleasedUiCapture = true,
                MapMarkerLastJumpClearedPanState = false,
                MapMarkerLastJumpConsumedButtonPulse = true,
                MapMarkerLastJumpVanillaMapInputHandoff = true,
                MapMarkerLastBlockedReason = "f5Visible",
                MapMarkerLastTransformRoute = "fullscreenMap",
                MapMarkerLastTransformScreenWidth = 1280,
                MapMarkerLastTransformScreenHeight = 720,
                MapMarkerLastTransformMapTopLeftX = 12.5,
                MapMarkerLastTransformMapTopLeftY = 24.5,
                MapMarkerLastTransformScale = 2,
                MapMarkerLastTransformMapFullscreenPosX = 4100.25,
                MapMarkerLastTransformMapFullscreenPosY = 1200.5,
                MapMarkerLastTransformGameUpdateCount = 12345,
                MapMarkerLastTransformUtc = new DateTime(2026, 6, 15, 1, 4, 4, DateTimeKind.Utc),
                MapMarkerLastRightClickMouseX = 640,
                MapMarkerLastRightClickMouseY = 360,
                MapMarkerLastRightClickTileX = 313,
                MapMarkerLastRightClickTileY = 167,
                MapMarkerLastRightClickTransformSource = "fullscreenTransform",
                MapMarkerLastRightClickFallbackReason = string.Empty,
                MapMarkerLastRightClickMapFullscreenPosX = 4100.25,
                MapMarkerLastRightClickMapFullscreenPosY = 1200.5,
                MapMarkerLastRightClickMapScale = 2,
                MapMarkerLastRightClickTransformAgeUpdates = 1,
                PlayerWorldMapMarkersUiOnlyActionCount = 2,
                MapMarkerPickerOpen = true,
                MapMarkerPickerAnchorScreenX = 640,
                MapMarkerPickerAnchorScreenY = 360,
                MapMarkerPickerPanelX = 652,
                MapMarkerPickerPanelY = 372,
                MapMarkerPickerPanelClamped = true,
                MapMarkerPickerLastDraw = new DateTime(2026, 6, 15, 1, 4, 5, DateTimeKind.Utc),
                MapMarkerPickerLastFullscreenDraw = new DateTime(2026, 6, 15, 1, 4, 7, DateTimeKind.Utc),
                MapMarkerPickerDrawRoute = "fullscreenMap",
                MapMarkerPickerDrawSkippedReason = "spriteBatchUnavailable",
                MapMarkerPickerLastClick = new DateTime(2026, 6, 15, 1, 4, 6, DateTimeKind.Utc),
                MapMarkerPickerLastCloseReason = "rightClickClose",
                PlayerWorldMapMarkersLastReadUtc = new DateTime(2026, 6, 15, 1, 2, 3, DateTimeKind.Utc),
                PlayerWorldMapMarkersLastWriteUtc = new DateTime(2026, 6, 15, 1, 3, 4, DateTimeKind.Utc),
                MapMarkerTraceEventsPath = "diagnostics/map-marker-events-20260616.jsonl",
                MapMarkerLastTraceEventWrittenAtUtc = new DateTime(2026, 6, 15, 1, 5, 4, DateTimeKind.Utc),
                MapMarkerLastTraceEventType = "markerCreate",
                MapMarkerLastTraceMarkerId = "marker-trace"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"PlayerWorldMapMarkersEnabled\": true");
            AssertContains(json, "\"PlayerWorldMapMarkersLastStatus\": \"saved\"");
            AssertContains(json, "\"PlayerWorldMapMarkersLastPairId\": \"pair-marker\"");
            AssertContains(json, "\"PlayerWorldMapMarkersCount\": 3");
            AssertContains(json, "\"PlayerWorldMapMarkersReadFailed\": true");
            AssertContains(json, "\"PlayerWorldMapMarkersWriteFailed\": true");
            AssertContains(json, "\"PlayerWorldMapMarkersLimitExceeded\": true");
            AssertContains(json, "\"PlayerWorldMapMarkersCulledByCacheLimit\": true");
            AssertContains(json, "\"PlayerWorldMapMarkersLastOperation\": \"rename\"");
            AssertContains(json, "\"PlayerWorldMapMarkersLastUiAction\": \"jump\"");
            AssertContains(json, "\"PlayerWorldMapMarkersLastJumpResult\": \"jumped\"");
            AssertContains(json, "\"MapMarkerLastJumpRequestedTileX\": 101");
            AssertContains(json, "\"MapMarkerLastJumpRequestedTileY\": 202");
            AssertContains(json, "\"MapMarkerLastJumpWrittenMapPosX\": 101");
            AssertContains(json, "\"MapMarkerLastJumpWrittenMapPosY\": 202");
            AssertContains(json, "\"MapMarkerLastJumpScale\": 2");
            AssertContains(json, "\"MapMarkerLastJumpReleasedUiCapture\": true");
            AssertContains(json, "\"MapMarkerLastJumpClearedPanState\": false");
            AssertContains(json, "\"MapMarkerLastJumpConsumedButtonPulse\": true");
            AssertContains(json, "\"MapMarkerLastJumpVanillaMapInputHandoff\": true");
            AssertContains(json, "\"MapMarkerLastBlockedReason\": \"f5Visible\"");
            AssertContains(json, "\"MapMarkerLastTransformRoute\": \"fullscreenMap\"");
            AssertContains(json, "\"MapMarkerLastTransformScreenWidth\": 1280");
            AssertContains(json, "\"MapMarkerLastTransformScreenHeight\": 720");
            AssertContains(json, "\"MapMarkerLastTransformMapTopLeftX\": 12.5");
            AssertContains(json, "\"MapMarkerLastTransformMapTopLeftY\": 24.5");
            AssertContains(json, "\"MapMarkerLastTransformScale\": 2");
            AssertContains(json, "\"MapMarkerLastTransformMapFullscreenPosX\": 4100.25");
            AssertContains(json, "\"MapMarkerLastTransformMapFullscreenPosY\": 1200.5");
            AssertContains(json, "\"MapMarkerLastTransformGameUpdateCount\": 12345");
            AssertContains(json, "\"MapMarkerLastTransformUtc\": \"2026-06-15T01:04:04.0000000Z\"");
            AssertContains(json, "\"MapMarkerLastRightClickMouseX\": 640");
            AssertContains(json, "\"MapMarkerLastRightClickMouseY\": 360");
            AssertContains(json, "\"MapMarkerLastRightClickTileX\": 313");
            AssertContains(json, "\"MapMarkerLastRightClickTileY\": 167");
            AssertContains(json, "\"MapMarkerLastRightClickTransformSource\": \"fullscreenTransform\"");
            AssertContains(json, "\"MapMarkerLastRightClickFallbackReason\": \"\"");
            AssertContains(json, "\"MapMarkerLastRightClickMapFullscreenPosX\": 4100.25");
            AssertContains(json, "\"MapMarkerLastRightClickMapFullscreenPosY\": 1200.5");
            AssertContains(json, "\"MapMarkerLastRightClickMapScale\": 2");
            AssertContains(json, "\"MapMarkerLastRightClickTransformAgeUpdates\": 1");
            AssertContains(json, "\"PlayerWorldMapMarkersUiOnlyActionCount\": 2");
            AssertContains(json, "\"MapMarkerPickerOpen\": true");
            AssertContains(json, "\"MapMarkerPickerAnchorScreenX\": 640");
            AssertContains(json, "\"MapMarkerPickerAnchorScreenY\": 360");
            AssertContains(json, "\"MapMarkerPickerPanelX\": 652");
            AssertContains(json, "\"MapMarkerPickerPanelY\": 372");
            AssertContains(json, "\"MapMarkerPickerPanelClamped\": true");
            AssertContains(json, "\"MapMarkerPickerLastDraw\": \"2026-06-15T01:04:05.0000000Z\"");
            AssertContains(json, "\"MapMarkerPickerLastFullscreenDraw\": \"2026-06-15T01:04:07.0000000Z\"");
            AssertContains(json, "\"MapMarkerPickerDrawRoute\": \"fullscreenMap\"");
            AssertContains(json, "\"MapMarkerPickerDrawSkippedReason\": \"spriteBatchUnavailable\"");
            AssertContains(json, "\"MapMarkerPickerLastClick\": \"2026-06-15T01:04:06.0000000Z\"");
            AssertContains(json, "\"MapMarkerPickerLastCloseReason\": \"rightClickClose\"");
            AssertContains(json, "\"PlayerWorldMapMarkersLastReadUtc\": \"2026-06-15T01:02:03.0000000Z\"");
            AssertContains(json, "\"PlayerWorldMapMarkersLastWriteUtc\": \"2026-06-15T01:03:04.0000000Z\"");
            AssertContains(json, "\"MapMarkerTraceEventsPath\": \"diagnostics/map-marker-events-20260616.jsonl\"");
            AssertContains(json, "\"MapMarkerLastTraceEventWrittenAtUtc\": \"2026-06-15T01:05:04.0000000Z\"");
            AssertContains(json, "\"MapMarkerLastTraceEventType\": \"markerCreate\"");
            AssertContains(json, "\"MapMarkerLastTraceMarkerId\": \"marker-trace\"");
        }

        private static void PlayerWorldMapMarkerDiagnosticsRecordsUiActionAndJumpState()
        {
            var before = PlayerWorldMapMarkerDiagnostics.GetSnapshot();

            PlayerWorldMapMarkerDiagnostics.RecordUiAction("jump", "jumped", false);
            PlayerWorldMapMarkerDiagnostics.RecordJumpState(123, 456, 12.5f, 34.5f, 2f, true, true, true, true);
            PlayerWorldMapMarkerDiagnostics.RecordUiAction("navigate", "uiOnlyNotImplemented", true);

            var after = PlayerWorldMapMarkerDiagnostics.GetSnapshot();
            AssertStringEquals(after.LastUiAction, "navigate", "last map marker UI action");
            AssertStringEquals(after.LastJumpResult, "jumped", "last map marker jump result");
            if (after.LastJumpRequestedTileX != 123 ||
                after.LastJumpRequestedTileY != 456 ||
                Math.Abs(after.LastJumpWrittenMapPosX - 12.5f) > 0.0001f ||
                Math.Abs(after.LastJumpWrittenMapPosY - 34.5f) > 0.0001f ||
                Math.Abs(after.LastJumpScale - 2f) > 0.0001f ||
                !after.LastJumpReleasedUiCapture ||
                !after.LastJumpClearedPanState ||
                !after.LastJumpConsumedButtonPulse ||
                !after.LastJumpVanillaMapInputHandoff ||
                after.UiOnlyActionCount != before.UiOnlyActionCount + 1)
            {
                throw new InvalidOperationException("Map marker diagnostics must record jump state and count UI-only placeholder actions.");
            }
        }

        private static void PlayerWorldMapMarkerDiagnosticsRecordsCoordinateTransformState()
        {
            PlayerWorldMapMarkerDiagnostics.RecordFullscreenTransform(new MapCustomMarkerFullscreenTransformSnapshot
            {
                HasTransform = true,
                Route = "fullscreenMap",
                ScreenWidth = 2133,
                ScreenHeight = 1141,
                MapTopLeftX = -3986.33f,
                MapTopLeftY = 27.568f,
                MapScale = 1.198f,
                MapFullscreenPosX = 4480.5f,
                MapFullscreenPosY = 560.25f,
                GameUpdateCount = 67890,
                Utc = new DateTime(2026, 6, 15, 2, 7, 8, DateTimeKind.Utc)
            });

            PlayerWorldMapMarkerDiagnostics.RecordRightClick(new MapCustomMarkerMapPoint
            {
                ScreenX = 1531,
                ScreenY = 755,
                TileX = 4482,
                TileY = 558,
                TransformSource = "fallback",
                FallbackReason = "viewStateMismatch",
                CurrentMapFullscreenPosX = 4482.75f,
                CurrentMapFullscreenPosY = 559.5f,
                CurrentMapScale = 1.25f,
                TransformAgeUpdates = 3
            });

            var after = PlayerWorldMapMarkerDiagnostics.GetSnapshot();
            if (!string.Equals(after.LastTransformRoute, "fullscreenMap", StringComparison.Ordinal) ||
                after.LastTransformScreenWidth != 2133 ||
                after.LastTransformScreenHeight != 1141 ||
                Math.Abs(after.LastTransformMapTopLeftX - -3986.33f) > 0.001f ||
                Math.Abs(after.LastTransformMapTopLeftY - 27.568f) > 0.001f ||
                Math.Abs(after.LastTransformScale - 1.198f) > 0.001f ||
                Math.Abs(after.LastTransformMapFullscreenPosX - 4480.5f) > 0.001f ||
                Math.Abs(after.LastTransformMapFullscreenPosY - 560.25f) > 0.001f ||
                after.LastTransformGameUpdateCount != 67890 ||
                after.LastTransformUtc != new DateTime(2026, 6, 15, 2, 7, 8, DateTimeKind.Utc) ||
                after.LastRightClickMouseX != 1531 ||
                after.LastRightClickMouseY != 755 ||
                after.LastRightClickTileX != 4482 ||
                after.LastRightClickTileY != 558 ||
                !string.Equals(after.LastRightClickTransformSource, "fallback", StringComparison.Ordinal) ||
                !string.Equals(after.LastRightClickFallbackReason, "viewStateMismatch", StringComparison.Ordinal) ||
                Math.Abs(after.LastRightClickMapFullscreenPosX - 4482.75f) > 0.001f ||
                Math.Abs(after.LastRightClickMapFullscreenPosY - 559.5f) > 0.001f ||
                Math.Abs(after.LastRightClickMapScale - 1.25f) > 0.001f ||
                after.LastRightClickTransformAgeUpdates != 3)
            {
                throw new InvalidOperationException("Map marker diagnostics must record transform view-state and right-click fallback context for user-returned snapshots.");
            }
        }

        private static void PlayerWorldMapMarkerTraceEventJsonIncludesCoordinateContext()
        {
            var json = PlayerWorldMapMarkerTraceRecorder.BuildEventJsonForTesting(new PlayerWorldMapMarkerTraceEvent
            {
                UtcNow = new DateTime(2026, 6, 16, 3, 4, 5, DateTimeKind.Utc),
                RuntimeVersion = "0.test",
                EventType = "markerCreate",
                PairId = "pair-trace",
                MarkerId = "marker-trace",
                IconItemId = 48,
                WriteAttempted = true,
                WriteSucceeded = true,
                WriteStatus = "saved",
                WriteMessage = "ok",
                TileX = 5,
                TileY = 7,
                ScreenX = 110,
                ScreenY = 214,
                ScreenWidth = 1280,
                ScreenHeight = 720,
                WorldSizeX = 8400,
                WorldSizeY = 2400,
                TransformSource = "fullscreenTransform",
                FallbackReason = string.Empty,
                MapTopLeftX = 100f,
                MapTopLeftY = 200f,
                MapScale = 2f,
                CurrentMapFullscreenPosX = 4200.25f,
                CurrentMapFullscreenPosY = 1200.5f,
                CurrentMapScale = 2f,
                CurrentGameUpdateCount = 55,
                TransformAgeUpdates = 0
            });

            AssertContains(json, "\"scenario\":\"MapCustomMarker.CoordinateTrace\"");
            AssertContains(json, "\"runtimeVersion\":\"0.test\"");
            AssertContains(json, "\"eventType\":\"markerCreate\"");
            AssertContains(json, "\"pairId\":\"pair-trace\"");
            AssertContains(json, "\"markerId\":\"marker-trace\"");
            AssertContains(json, "\"iconItemId\":48");
            AssertContains(json, "\"mouseX\":110");
            AssertContains(json, "\"mouseY\":214");
            AssertContains(json, "\"tileX\":5");
            AssertContains(json, "\"tileY\":7");
            AssertContains(json, "\"screenWidth\":1280");
            AssertContains(json, "\"screenHeight\":720");
            AssertContains(json, "\"source\":\"fullscreenTransform\"");
            AssertContains(json, "\"mapTopLeftX\":100");
            AssertContains(json, "\"currentMapFullscreenPosX\":4200.25");
            AssertContains(json, "\"currentGameUpdateCount\":55");
            AssertContains(json, "\"transformAgeUpdates\":0");
            AssertContains(json, "\"tileCenterScreenX\":111");
            AssertContains(json, "\"tileCenterScreenY\":215");
            AssertContains(json, "\"tileCenterDeltaX\":1");
            AssertContains(json, "\"tileCenterDeltaY\":1");
            AssertContains(json, "\"attempted\":true");
            AssertContains(json, "\"succeeded\":true");
            AssertContains(json, "\"status\":\"saved\"");
            AssertContains(json, "\"draw\":{\"attempted\":false");
            AssertContains(json, "\"deltaFromRightClickX\":0");
        }

        private static void PlayerWorldMapMarkerTraceDrawEventIncludesScreenDelta()
        {
            var createSample = new PlayerWorldMapMarkerTraceEvent
            {
                UtcNow = new DateTime(2026, 6, 16, 3, 4, 5, DateTimeKind.Utc),
                RuntimeVersion = "0.test",
                EventType = "markerCreate",
                PairId = "pair-trace",
                MarkerId = "marker-draw",
                IconItemId = 48,
                WriteAttempted = true,
                WriteSucceeded = true,
                WriteStatus = "saved",
                WriteMessage = "ok",
                TileX = 5,
                TileY = 7,
                ScreenX = 110,
                ScreenY = 214,
                ScreenWidth = 1280,
                ScreenHeight = 720,
                WorldSizeX = 8400,
                WorldSizeY = 2400,
                TransformSource = "fullscreenTransform",
                MapTopLeftX = 100f,
                MapTopLeftY = 200f,
                MapScale = 2f,
                CurrentMapFullscreenPosX = 4200.25f,
                CurrentMapFullscreenPosY = 1200.5f,
                CurrentMapScale = 2f,
                CurrentGameUpdateCount = 55,
                TransformAgeUpdates = 0
            };

            var drawSample = PlayerWorldMapMarkerTraceRecorder.BuildDrawEventForTesting(
                createSample,
                120,
                180,
                40,
                60,
                1280,
                720,
                true,
                string.Empty);
            var json = PlayerWorldMapMarkerTraceRecorder.BuildEventJsonForTesting(drawSample);

            AssertContains(json, "\"eventType\":\"markerDraw\"");
            AssertContains(json, "\"markerId\":\"marker-draw\"");
            AssertContains(json, "\"mouseX\":110");
            AssertContains(json, "\"mouseY\":214");
            AssertContains(json, "\"draw\":{\"attempted\":true");
            AssertContains(json, "\"visible\":true");
            AssertContains(json, "\"screenWidth\":1280");
            AssertContains(json, "\"screenHeight\":720");
            AssertContains(json, "\"regionX\":120");
            AssertContains(json, "\"regionY\":180");
            AssertContains(json, "\"regionWidth\":40");
            AssertContains(json, "\"regionHeight\":60");
            AssertContains(json, "\"centerScreenX\":140");
            AssertContains(json, "\"centerScreenY\":210");
            AssertContains(json, "\"deltaFromRightClickX\":30");
            AssertContains(json, "\"deltaFromRightClickY\":-4");
            AssertContains(json, "\"skippedReason\":\"\"");
        }

        private static void LegacyMapEnhancementPageIncludesMapCustomMarkersRow()
        {
            var expectedHeight = LegacyUiMetrics.RowHeight * 7 +
                                 LegacyUiMetrics.SettingRowGap * 6 +
                                 LegacyMainWindow.CalculateMapMarkerListHeightForTesting(0) +
                                 24;
            if (LegacyMainWindow.CalculateMapEnhancementContentHeightForTesting() != expectedHeight)
            {
                throw new InvalidOperationException("Map enhancement content height must include custom marker row.");
            }

            var expectedMarkerListY = LegacyUiMetrics.RowHeight * 5 + LegacyUiMetrics.SettingRowGap * 4;
            if (LegacyMainWindow.CalculateMapMarkerListContentYForTesting() != expectedMarkerListY)
            {
                throw new InvalidOperationException("Map custom marker list must start directly after the main title row without an extra setting gap.");
            }

            var tooltips = LegacyMainWindow.GetMapCustomMarkersButtonTooltipsForTesting();
            if (tooltips == null ||
                tooltips.Length != 2 ||
                !string.Equals(tooltips[0], "右键大地图试试吧", StringComparison.Ordinal) ||
                !string.Equals(tooltips[1], string.Empty, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map custom marker on/off tooltip contract changed.");
            }
        }

        private static void LegacyMapCustomMarkerListLayoutAndPlaceholders()
        {
            LegacyMainWindow.ResetMapCustomMarkerPaginationForTesting();
            var emptyHeight = LegacyMainWindow.CalculateMapMarkerListHeightForTesting(0);
            var threeHeight = LegacyMainWindow.CalculateMapMarkerListHeightForTesting(3);
            if (threeHeight <= emptyHeight)
            {
                throw new InvalidOperationException("Map custom marker list height must grow with marker count.");
            }

            if (emptyHeight != LegacyMainWindow.CalculateMapMarkerListBodyHeightForTesting(0) ||
                emptyHeight != 0 ||
                threeHeight != LegacyMainWindow.CalculateMapMarkerListBodyHeightForTesting(3) ||
                LegacyMainWindow.GetMapMarkerListTopGapForTesting() != 0 ||
                LegacyMainWindow.GetMapMarkerListHorizontalInsetForTesting() != 0)
            {
                throw new InvalidOperationException("Map custom marker list height must omit the duplicate subtitle row, attach to the main row, and keep a silent zero-height empty state.");
            }

            var inputId = LegacyMainWindow.BuildMapMarkerNameInputId("marker-a");
            AssertStringEquals(inputId, "map-custom-marker:name-input:marker-a", "map marker name input id");
            AssertStringEquals(
                LegacyMainWindow.BuildMapMarkerConfirmCommandIdForTesting("marker-a"),
                "map-custom-marker:confirm-name:marker-a",
                "map marker confirm command id");
            AssertStringEquals(
                LegacyMainWindow.GetMapMarkerListVisualContractForTesting(),
                "attached-link-card+same-width+paged-10+empty-silent+focused-confirm",
                "map marker list visual contract");
            if (LegacyMainWindow.GetMapMarkerListPageSizeForTesting() != 10 ||
                LegacyMainWindow.GetMapMarkerPageCountForTesting(0) != 0 ||
                LegacyMainWindow.GetMapMarkerPageCountForTesting(10) != 1 ||
                LegacyMainWindow.GetMapMarkerPageCountForTesting(11) != 2 ||
                LegacyMainWindow.GetMapMarkerPageCountForTesting(PlayerWorldMapMarkerConstants.MaxMarkersPerPair) != 12)
            {
                throw new InvalidOperationException("Map marker list must paginate by 10 rows and reach 12 pages at the 120 marker limit.");
            }

            var tenBodyHeight = LegacyMainWindow.CalculateMapMarkerListBodyHeightForTesting(10);
            if (LegacyMainWindow.CalculateMapMarkerListHeightForTesting(13) != tenBodyHeight)
            {
                throw new InvalidOperationException("Map marker list first page height must be capped at 10 visible rows.");
            }

            LegacyMainWindow.MoveMapCustomMarkerPage(1);
            if (LegacyMainWindow.CalculateMapMarkerListHeightForTesting(13) != LegacyMainWindow.CalculateMapMarkerListBodyHeightForTesting(3))
            {
                throw new InvalidOperationException("Map marker list second page height must use only remaining visible rows.");
            }

            LegacyMainWindow.MoveMapCustomMarkerPage(99);
            LegacyMainWindow.CalculateMapMarkerListHeightForTesting(13);
            if (LegacyMainWindow.GetMapCustomMarkerPageIndex() != 1)
            {
                throw new InvalidOperationException("Map marker list page index must clamp when data shrinks or the user over-advances.");
            }

            AssertStringEquals(
                LegacyMainWindow.BuildMapCustomMarkersLabelForTesting(PlayerWorldMapMarkerConstants.MaxMarkersPerPair - 1),
                "地图标记",
                "map marker normal label");
            AssertStringEquals(
                LegacyMainWindow.BuildMapCustomMarkersLabelForTesting(PlayerWorldMapMarkerConstants.MaxMarkersPerPair),
                "地图标记（已到标记上限）",
                "map marker limit label");
            LegacyMainWindow.ResetMapCustomMarkerPaginationForTesting();

            LegacyTextInput.ClearFocus();
            if (LegacyMainWindow.ShouldShowMapMarkerConfirmButtonForTesting("marker-a"))
            {
                throw new InvalidOperationException("Map marker confirm button must be hidden until the marker name input is focused.");
            }

            var unfocusedActions = LegacyMainWindow.GetMapMarkerVisibleActionIdsForTesting("marker-a");
            if (Array.IndexOf(unfocusedActions, LegacyMainWindow.BuildMapMarkerConfirmCommandIdForTesting("marker-a")) >= 0 ||
                Array.IndexOf(unfocusedActions, "map-custom-marker:jump:marker-a") < 0 ||
                Array.IndexOf(unfocusedActions, "map-custom-marker:delete:marker-a") < 0)
            {
                throw new InvalidOperationException("Map marker unfocused row must keep marker actions but not register the confirm button.");
            }

            LegacyTextInput.Focus(inputId, "draft");
            try
            {
                if (!LegacyMainWindow.ShouldShowMapMarkerConfirmButtonForTesting("marker-a"))
                {
                    throw new InvalidOperationException("Map marker confirm button must be visible while the marker name input is focused.");
                }

                var focusedActions = LegacyMainWindow.GetMapMarkerVisibleActionIdsForTesting("marker-a");
                if (Array.IndexOf(focusedActions, LegacyMainWindow.BuildMapMarkerConfirmCommandIdForTesting("marker-a")) < 0)
                {
                    throw new InvalidOperationException("Map marker focused row must register the confirm button.");
                }
            }
            finally
            {
                LegacyTextInput.ClearFocus();
            }

            var tooltips = LegacyMainWindow.GetMapMarkerActionTooltipsForTesting();
            if (tooltips == null ||
                tooltips.Length != 6 ||
                !string.Equals(tooltips[0], "双击输入，限10个字", StringComparison.Ordinal) ||
                !string.Equals(tooltips[1], "确认保存名称", StringComparison.Ordinal) ||
                !string.Equals(tooltips[2], "地图跳转到标记位置", StringComparison.Ordinal) ||
                !string.Equals(tooltips[3], "分析可达路径", StringComparison.Ordinal) ||
                !string.Equals(tooltips[4], "消耗虫洞药水*1 回忆药水*1传送", StringComparison.Ordinal) ||
                !string.Equals(tooltips[5], "暂未实现", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map marker list tooltip contract changed.");
            }

            if (LegacyUiActionService.IsMapCustomMarkerUiOnlyActionForTesting("jump") ||
                !LegacyUiActionService.IsMapCustomMarkerUiOnlyActionForTesting("navigate") ||
                !LegacyUiActionService.IsMapCustomMarkerUiOnlyActionForTesting("teleport") ||
                !LegacyUiActionService.IsMapCustomMarkerUiOnlyActionForTesting("autopilot") ||
                LegacyUiActionService.IsMapCustomMarkerUiOnlyActionForTesting("delete"))
            {
                throw new InvalidOperationException("Only navigation, teleport and autopilot must stay UI-only while jump and delete are implemented.");
            }
        }

        private static void MapCustomMarkerConfirmNameCommandSavesAndClearsFocus()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var restoreRuntimeMain = PushMapMarkerRuntimeMainTypeForTesting(typeof(Terraria.Main));
                var restoreIdentity = PushFakePlayerWorldIdentity(
                    Path.Combine(root, "Players", "MarkerPlayer.plr"),
                    "MarkerPlayer",
                    Path.Combine(root, "Worlds", "MarkerWorld.wld"),
                    "MarkerWorld",
                    "marker-world-uid",
                    "MarkerWorld.map",
                    7654,
                    8400,
                    2400);
                try
                {
                    PlayerWorldIdentityResolution identity;
                    if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out identity) ||
                        identity == null ||
                        !identity.IsResolved ||
                        string.IsNullOrWhiteSpace(identity.PairId))
                    {
                        throw new InvalidOperationException("Expected map marker confirm test identity to resolve.");
                    }

                    var seed = PlayerWorldMapMarkerStore.SaveForPairForTesting(
                        identity.PairId,
                        8400,
                        2400,
                        new List<PlayerWorldMapMarkerRecord> { CreateMapMarker("confirm-me", 10, 20, 48, "old", 0) },
                        "seedConfirm");
                    if (!seed.Succeeded)
                    {
                        throw new InvalidOperationException("Map marker confirm seed save should succeed.");
                    }

                    var inputId = LegacyMainWindow.BuildMapMarkerNameInputId("confirm-me");
                    LegacyTextInput.Focus(inputId, "confirmed");
                    LegacyUiInput.ResetActionUpdateGateStateForTesting();
                    LegacyUiActionService.ResetActionUpdateDiagnosticsForTesting();

                    LegacyUiInput.EnqueueClick(
                        new LegacyUiElement
                        {
                            Id = LegacyMainWindow.BuildMapMarkerConfirmCommandIdForTesting("confirm-me"),
                            Label = "地图标记:确认",
                            Kind = "button",
                            Rect = new LegacyUiRect(0, 0, 42, 24),
                            Enabled = true
                        },
                        new LegacyMouseSnapshot
                        {
                            X = 1,
                            Y = 1,
                            LeftPressed = true,
                            ReadAvailable = true,
                            ReadMode = "test"
                        },
                        true);
                    LegacyUiActionService.Update(new InputActionQueue(), null);

                    if (LegacyUiActionService.DispatchedCommandCountLast != 1 ||
                        LegacyTextInput.IsFocused(inputId))
                    {
                        throw new InvalidOperationException("Map marker confirm button must dispatch once and clear the active name input.");
                    }

                    var after = PlayerWorldMapMarkerStore.ReadForPairForTesting(identity.PairId);
                    AssertStringEquals(after.Markers[0].Name, "confirmed", "confirmed map marker name");
                }
                finally
                {
                    LegacyTextInput.ClearFocus();
                    LegacyUiInput.ResetActionUpdateGateStateForTesting();
                    LegacyUiActionService.ResetActionUpdateDiagnosticsForTesting();
                    restoreIdentity();
                    restoreRuntimeMain();
                }
            });
        }

        private static Action PushMapMarkerRuntimeMainTypeForTesting(Type mainType)
        {
            var runtimeMainField = typeof(TerrariaRuntimeTypes).GetField("_mainType", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (runtimeMainField == null)
            {
                throw new InvalidOperationException("Terraria runtime main type cache field missing.");
            }

            var previousRuntimeMain = runtimeMainField.GetValue(null);
            runtimeMainField.SetValue(null, mainType);
            return () => runtimeMainField.SetValue(null, previousRuntimeMain);
        }

        private static void MapFullscreenJumpTargetClampsPositionAndScale()
        {
            var target = MapFullscreenCompat.BuildJumpTargetForTesting(99999, -20, 8400, 2400, 999f);
            if (!target.Succeeded ||
                !string.Equals(target.ResultCode, "ok", StringComparison.Ordinal) ||
                target.TileX != 8399 ||
                target.TileY != 0 ||
                Math.Abs(target.WrittenMapPosX - 8399f) > 0.0001f ||
                Math.Abs(target.WrittenMapPosY - 0f) > 0.0001f ||
                Math.Abs(target.Scale - MapFullscreenCompat.MaxJumpScale) > 0.0001f)
            {
                throw new InvalidOperationException("Fullscreen map jump target must clamp tile position and scale.");
            }

            var defaultScale = MapFullscreenCompat.BuildJumpTargetForTesting(10, 20, 8400, 2400, float.NaN);
            if (!defaultScale.Succeeded || Math.Abs(defaultScale.Scale - MapFullscreenCompat.DefaultJumpScale) > 0.0001f)
            {
                throw new InvalidOperationException("Fullscreen map jump target must fall back to the default scale for invalid scale input.");
            }
        }

        private static void MapFullscreenJumpClearsPanState()
        {
            var state = MapFullscreenCompat.BuildClearedPanStateForTesting(
                new Microsoft.Xna.Framework.Vector2(100f, 200f),
                456,
                234);
            if (state == null ||
                !state.Cleared ||
                state.PanTargetMapFullscreen ||
                state.ResetMapFull ||
                Math.Abs(state.PanTargetMapFullscreenEnd.X - 100f) > 0.0001f ||
                Math.Abs(state.PanTargetMapFullscreenEnd.Y - 200f) > 0.0001f ||
                Math.Abs(state.GrabMapX - 456f) > 0.0001f ||
                Math.Abs(state.GrabMapY - 234f) > 0.0001f)
            {
                throw new InvalidOperationException("Map fullscreen jump must clear vanilla pan target, reset flag, and drag anchors without touching player state.");
            }
        }

        private static void MapFullscreenJumpTargetFailsWithoutWorldDimensions()
        {
            var target = MapFullscreenCompat.BuildJumpTargetForTesting(10, 20, 0, 2400, 2f);
            if (target.Succeeded ||
                !string.Equals(target.ResultCode, "worldUnavailable", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Fullscreen map jump target must fail soft when world dimensions are unavailable.");
            }
        }

        private static void MapCustomMarkerJumpReleaseClosesF5AndUiCapture()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
                ResetUiInputFrameTestState();
                var player = new Terraria.Player();
                Terraria.Main.LocalPlayer = player;
                Terraria.Main.player[0] = player;
                Terraria.Main.myPlayer = 0;
                LegacyMainUiState.SetVisible(true);
                Terraria.Main.GameUpdateCount = 42;
                Terraria.Main.mouseLeft = true;
                Terraria.Main.mouseLeftRelease = true;
                Terraria.Main.mouseInterface = true;
                Terraria.Main.blockMouse = true;
                player.mouseInterface = true;
                player.controlUseItem = true;
                player.releaseUseItem = false;
                player.channel = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = true;

                var release = LegacyMainUiState.HideForMapCustomMarkerJumpAndReleaseCapture();
                if (release == null ||
                    !release.F5WasVisible ||
                    !release.ReleasedUiCapture ||
                    !release.ConsumedJumpButtonPulse ||
                    !release.VanillaMapInputHandoff ||
                    LegacyMainUiState.Visible ||
                    !Terraria.Main.mouseInterface ||
                    !Terraria.Main.blockMouse ||
                    Terraria.Main.mouseLeft ||
                    Terraria.Main.mouseLeftRelease ||
                    !player.mouseInterface ||
                    player.controlUseItem ||
                    !player.releaseUseItem ||
                    player.channel ||
                    Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft ||
                    Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft)
                {
                    throw new InvalidOperationException("Map marker jump must close F5, release Legacy UI capture, and consume the triggering left-click pulse.");
                }

                Terraria.Main.mouseLeft = true;
                Terraria.Main.mouseLeftRelease = true;
                player.controlUseItem = true;
                player.releaseUseItem = false;
                player.channel = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = true;
                TerrariaUiMouseCompat.UpdateActiveTriggerSuppressionPrefixGuard();
                if (Terraria.Main.mouseLeft ||
                    Terraria.Main.mouseLeftRelease ||
                    player.controlUseItem ||
                    !player.releaseUseItem ||
                    player.channel ||
                    Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft ||
                    Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft)
                {
                    throw new InvalidOperationException("Map marker jump button suppression must keep the consumed pulse blocked until the physical left button is released.");
                }

                Terraria.Main.mouseLeft = false;
                Terraria.Main.mouseLeftRelease = false;
                player.controlUseItem = false;
                player.releaseUseItem = true;
                player.channel = false;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = false;
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = false;
                TerrariaUiMouseCompat.UpdateActiveTriggerSuppressionPrefixGuard();

                Terraria.Main.mouseLeft = true;
                Terraria.Main.mouseLeftRelease = true;
                player.controlUseItem = true;
                player.releaseUseItem = false;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = true;
                TerrariaUiMouseCompat.UpdateActiveTriggerSuppressionPrefixGuard();
                if (!Terraria.Main.mouseLeft ||
                    !Terraria.Main.mouseLeftRelease ||
                    !player.controlUseItem ||
                    player.releaseUseItem ||
                    !Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft ||
                    !Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft)
                {
                    throw new InvalidOperationException("Map marker jump suppression must stop after release so vanilla fullscreen map input can take over.");
                }
            }
            finally
            {
                LegacyMainUiState.SetVisible(false);
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                restoreRuntimeTypes();
            }
        }

        private static void MapCustomMarkerRightClickEdgeOpensOnce()
        {
            if (!MapCustomMarkerInteractionService.ShouldOpenPickerForTesting(true, true, false, true, false))
            {
                throw new InvalidOperationException("Right button down edge should open style picker.");
            }

            if (MapCustomMarkerInteractionService.ShouldOpenPickerForTesting(true, true, false, true, true))
            {
                throw new InvalidOperationException("Held right button should not reopen style picker every tick.");
            }

            if (MapCustomMarkerInteractionService.ShouldOpenPickerForTesting(false, true, true, true, false) ||
                MapCustomMarkerInteractionService.ShouldOpenPickerForTesting(true, false, true, true, false))
            {
                throw new InvalidOperationException("Disabled or blocked custom marker input must not open picker.");
            }
        }

        private static void MapCustomMarkerRightClickReleaseGateRequiresReleaseBeforeClose()
        {
            if (MapCustomMarkerInteractionService.ShouldClosePickerForTesting(true, true, true, false, true))
            {
                throw new InvalidOperationException("Opening right click must not immediately close the style picker before the button is released.");
            }

            if (!MapCustomMarkerInteractionService.ShouldReleaseRightCloseGateForTesting(true, false))
            {
                throw new InvalidOperationException("Right close gate must release when the physical right button is up.");
            }

            if (!MapCustomMarkerInteractionService.ShouldClosePickerForTesting(true, true, true, false, false))
            {
                throw new InvalidOperationException("A new right click after release should close the open style picker.");
            }
        }

        private static void MapCustomMarkerFullscreenCoordinateClamp()
        {
            const int expectedTileX = 320;
            const int expectedTileY = 200;
            const float mapScale = 2f;
            var mapTopLeft = new Microsoft.Xna.Framework.Vector2(100f, 60f);
            var mouseX = (int)(mapTopLeft.X + (expectedTileX + 0.5f) * mapScale);
            var mouseY = (int)(mapTopLeft.Y + (expectedTileY + 0.5f) * mapScale);
            var point = MapCustomMarkerMapCompat.ScreenToTileFromTransformForTesting(
                mouseX,
                mouseY,
                mapTopLeft,
                mapScale,
                8400,
                2400);
            if (point.TileX != expectedTileX || point.TileY != expectedTileY ||
                !string.Equals(point.TransformSource, "fullscreenTransform", StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(point.FallbackReason))
            {
                throw new InvalidOperationException("Fullscreen map transform should convert screen coordinates back to the original overlay tile.");
            }

            var projected = ProjectMapOverlayTileToScreenForTesting(
                new Microsoft.Xna.Framework.Vector2(point.TileX + 0.5f, point.TileY + 0.5f),
                Microsoft.Xna.Framework.Vector2.Zero,
                mapTopLeft,
                mapScale);
            if (Math.Abs(projected.X - mouseX) > 0.001f || Math.Abs(projected.Y - mouseY) > 0.001f)
            {
                throw new InvalidOperationException("Fullscreen map marker coordinates must round-trip with the MapOverlayDrawContext draw path.");
            }

            var clamped = MapCustomMarkerMapCompat.ScreenToTileFromTransformForTesting(
                -99999,
                99999,
                new Microsoft.Xna.Framework.Vector2(100f, 60f),
                2f,
                8400,
                2400);
            if (clamped.TileX < 0 || clamped.TileY < 0 || clamped.TileX >= 8400 || clamped.TileY >= 2400)
            {
                throw new InvalidOperationException("Fullscreen map mouse tile conversion must clamp to world bounds.");
            }

            MapCustomMarkerMapCompat.ResetFullscreenTransformForTesting();
            MapCustomMarkerMapPoint cachedPoint;
            string fallbackReason;
            if (MapCustomMarkerMapCompat.TryScreenToTileFromLastTransformForTesting(740, 460, 1280, 720, 8400, 2400, out cachedPoint, out fallbackReason) ||
                !string.Equals(fallbackReason, "noRecentFullscreenTransform", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Fullscreen map coordinate conversion must report fallback when no draw transform has been cached.");
            }

            var testFullscreenPos = new Microsoft.Xna.Framework.Vector2(4200f, 1200f);
            var invalidScaleTransform = MapCustomMarkerMapCompat.RecordFullscreenTransformForTesting(
                mapTopLeft,
                0f,
                1280,
                720,
                "fullscreenMap",
                testFullscreenPos,
                10);
            if (invalidScaleTransform.HasTransform ||
                MapCustomMarkerMapCompat.TryScreenToTileFromLastTransformForTesting(740, 460, 1280, 720, 8400, 2400, out cachedPoint, out fallbackReason) ||
                !string.Equals(fallbackReason, "scaleMismatch", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Fullscreen map coordinate conversion must distinguish invalid cached map scale from a missing transform.");
            }

            var transform = MapCustomMarkerMapCompat.RecordFullscreenTransformForTesting(
                mapTopLeft,
                mapScale,
                1280,
                720,
                "fullscreenMap",
                testFullscreenPos,
                20);
            if (!transform.HasTransform ||
                !MapCustomMarkerMapCompat.TryScreenToTileFromLastTransformForTesting(mouseX, mouseY, 1280, 720, 8400, 2400, out cachedPoint, out fallbackReason) ||
                cachedPoint.TileX != expectedTileX ||
                cachedPoint.TileY != expectedTileY ||
                !string.Equals(cachedPoint.TransformSource, "fullscreenTransform", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Fullscreen map coordinate conversion must prefer the cached draw transform.");
            }

            transform = MapCustomMarkerMapCompat.RecordFullscreenTransformForTesting(
                mapTopLeft,
                mapScale,
                2133,
                1141,
                "fullscreenMap",
                testFullscreenPos,
                30);
            if (!transform.HasTransform ||
                !MapCustomMarkerMapCompat.TryScreenToTileFromLastTransformForTesting(mouseX, mouseY, 2560, 1369, 8400, 2400, out cachedPoint, out fallbackReason) ||
                cachedPoint.TileX != expectedTileX ||
                cachedPoint.TileY != expectedTileY ||
                !string.Equals(cachedPoint.TransformSource, "fullscreenTransform", StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(cachedPoint.FallbackReason))
            {
                throw new InvalidOperationException("Fullscreen map coordinate conversion must treat UI scale-equivalent screen size as the same coordinate space instead of falling back.");
            }

            if (MapCustomMarkerMapCompat.TryScreenToTileFromLastTransformForTesting(740, 460, 1024, 720, 8400, 2400, out cachedPoint, out fallbackReason) ||
                !string.Equals(fallbackReason, "screenSizeMismatch", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Fullscreen map coordinate conversion must reject stale transform cache when screen size changed.");
            }

            var fullscreenPos = new Microsoft.Xna.Framework.Vector2(4200f, 1200f);
            var fallbackScreenWidth = 1280;
            var fallbackScreenHeight = 720;
            var fallbackMouseX = 740;
            var fallbackMouseY = 460;
            var fallbackPoint = MapCustomMarkerMapCompat.ScreenToTile(
                fallbackMouseX,
                fallbackMouseY,
                fullscreenPos,
                mapScale,
                fallbackScreenWidth,
                fallbackScreenHeight,
                8400,
                2400);
            var fallbackMapTopLeft = MapCustomMarkerMapCompat.BuildFallbackMapTopLeftForTesting(
                fullscreenPos,
                mapScale,
                fallbackScreenWidth,
                fallbackScreenHeight);
            var equivalentTransformPoint = MapCustomMarkerMapCompat.ScreenToTileFromTransformForTesting(
                fallbackMouseX,
                fallbackMouseY,
                fallbackMapTopLeft,
                mapScale,
                8400,
                2400);
            if (fallbackPoint.TileX != equivalentTransformPoint.TileX ||
                fallbackPoint.TileY != equivalentTransformPoint.TileY ||
                !string.Equals(fallbackPoint.TransformSource, "fallback", StringComparison.Ordinal) ||
                !string.Equals(fallbackPoint.FallbackReason, "fullscreenTransformUnavailable", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Fullscreen map fallback must derive the same overlay origin as the cached draw transform route.");
            }
        }

        private static void MapCustomMarkerFullscreenDrawMouseSampleWinsOverUpdateMouse()
        {
            const float mapScale = 2.5f;
            var mapPos = new Microsoft.Xna.Framework.Vector2(4196.5f, 560.688f);
            var mapTopLeft = new Microsoft.Xna.Framework.Vector2(-10091.25f, -893.72f);
            var drawMouseX = 720;
            var drawMouseY = 503;
            var updateMouseX = 760;
            var updateMouseY = 503;

            MapCustomMarkerMapCompat.ResetFullscreenTransformForTesting();
            MapCustomMarkerMapCompat.RecordFullscreenTransformForTesting(
                mapTopLeft,
                mapScale,
                800,
                1016,
                "fullscreenMap",
                mapPos,
                500);
            var drawSample = MapCustomMarkerMapCompat.RecordFullscreenDrawMousePointForTesting(
                drawMouseX,
                drawMouseY,
                8400,
                2400);
            if (drawSample == null ||
                drawSample.TileX != 4324 ||
                drawSample.TileY != 558 ||
                !string.Equals(drawSample.TransformSource, "fullscreenDrawMouse", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Fullscreen draw mouse sample must cache the same tile position the original map draw saw.");
            }

            string fallbackReason;
            long transformAgeUpdates;
            var resolved = MapCustomMarkerMapCompat.ResolveFullscreenMouseTileForTesting(
                updateMouseX,
                updateMouseY,
                800,
                1016,
                8400,
                2400,
                mapPos,
                mapScale,
                501,
                out fallbackReason,
                out transformAgeUpdates);
            if (resolved.TileX != drawSample.TileX ||
                resolved.TileY != drawSample.TileY ||
                resolved.ScreenX != drawMouseX ||
                resolved.ScreenY != drawMouseY ||
                transformAgeUpdates != 1 ||
                !string.Equals(resolved.TransformSource, "fullscreenDrawMouse", StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(resolved.FallbackReason))
            {
                throw new InvalidOperationException("Right-click map marker placement must prefer the draw-phase mouse tile sample over reinterpreting Update mouse coordinates.");
            }

            var transformOnly = MapCustomMarkerMapCompat.ScreenToTileFromTransformForTesting(
                updateMouseX,
                updateMouseY,
                mapTopLeft,
                mapScale,
                8400,
                2400);
            if (transformOnly.TileX == resolved.TileX)
            {
                throw new InvalidOperationException("Test setup must prove that the old Update-mouse transform path would pick a different tile.");
            }
        }

        private static void MapCustomMarkerFullscreenCoordinateFreshness()
        {
            var oldMapPos = new Microsoft.Xna.Framework.Vector2(4200f, 1200f);
            var draggedMapPos = new Microsoft.Xna.Framework.Vector2(4212f, 1216f);
            var mouseX = 740;
            var mouseY = 460;
            var oldTileX = 320;
            var oldTileY = 200;
            var latestTileX = 360;
            var latestTileY = 240;
            var oldScale = 2f;
            var zoomedScale = 2.25f;
            var oldMapTopLeft = new Microsoft.Xna.Framework.Vector2(
                mouseX - (oldTileX + 0.5f) * oldScale,
                mouseY - (oldTileY + 0.5f) * oldScale);
            var latestMapTopLeft = new Microsoft.Xna.Framework.Vector2(
                mouseX - (latestTileX + 0.5f) * zoomedScale,
                mouseY - (latestTileY + 0.5f) * zoomedScale);

            MapCustomMarkerMapCompat.ResetFullscreenTransformForTesting();
            MapCustomMarkerMapCompat.RecordFullscreenTransformForTesting(
                oldMapTopLeft,
                oldScale,
                1280,
                720,
                "fullscreenMap",
                oldMapPos,
                100);

            MapCustomMarkerMapPoint cachedPoint;
            string fallbackReason;
            long transformAgeUpdates;
            if (MapCustomMarkerMapCompat.TryScreenToTileFromLastTransformForTesting(
                    mouseX,
                    mouseY,
                    1280,
                    720,
                    8400,
                    2400,
                    oldMapPos,
                    zoomedScale,
                    103,
                    out cachedPoint,
                    out fallbackReason,
                    out transformAgeUpdates) ||
                !string.Equals(fallbackReason, "viewStateMismatch", StringComparison.Ordinal) ||
                transformAgeUpdates != 3)
            {
                throw new InvalidOperationException("Map marker coordinate conversion must reject stale transform cache after wheel zoom and record its update age.");
            }

            if (MapCustomMarkerMapCompat.TryScreenToTileFromLastTransformForTesting(
                    mouseX,
                    mouseY,
                    1280,
                    720,
                    8400,
                    2400,
                    draggedMapPos,
                    oldScale,
                    104,
                    out cachedPoint,
                    out fallbackReason,
                    out transformAgeUpdates) ||
                !string.Equals(fallbackReason, "viewStateMismatch", StringComparison.Ordinal) ||
                transformAgeUpdates != 4)
            {
                throw new InvalidOperationException("Map marker coordinate conversion must reject stale transform cache after fullscreen map drag.");
            }

            MapCustomMarkerMapCompat.RecordFullscreenTransformForTesting(
                latestMapTopLeft,
                zoomedScale,
                1280,
                720,
                "fullscreenMap",
                draggedMapPos,
                105);
            if (!MapCustomMarkerMapCompat.TryScreenToTileFromLastTransformForTesting(
                    mouseX,
                    mouseY,
                    1280,
                    720,
                    8400,
                    2400,
                    draggedMapPos,
                    zoomedScale,
                    105,
                    out cachedPoint,
                    out fallbackReason,
                    out transformAgeUpdates) ||
                cachedPoint.TileX != latestTileX ||
                cachedPoint.TileY != latestTileY ||
                transformAgeUpdates != 0 ||
                !string.Equals(cachedPoint.TransformSource, "fullscreenTransform", StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(cachedPoint.FallbackReason))
            {
                throw new InvalidOperationException("Map marker coordinate conversion must use the latest fullscreen draw transform after drag or zoom refreshes the view.");
            }
        }

        private static void MapCustomMarkerPendingPlacementFreezesRightClickTile()
        {
            var point = new MapCustomMarkerMapPoint
            {
                TileX = 320,
                TileY = 200,
                ScreenX = 740,
                ScreenY = 460,
                ScreenWidth = 1280,
                ScreenHeight = 720,
                WorldSizeX = 8400,
                WorldSizeY = 2400,
                TransformSource = "fullscreenTransform",
                FallbackReason = string.Empty,
                MapTopLeftX = 99f,
                MapTopLeftY = 55f,
                MapScale = 2f,
                CurrentMapFullscreenPosX = 4200f,
                CurrentMapFullscreenPosY = 1200f,
                CurrentMapScale = 2f,
                CurrentGameUpdateCount = 456,
                TransformAgeUpdates = 1
            };

            var placement = MapCustomMarkerInteractionService.CreatePlacementForTesting(point);
            point.TileX = 999;
            point.TileY = 888;
            point.ScreenX = 111;
            point.ScreenY = 222;
            point.ScreenWidth = 640;
            point.ScreenHeight = 480;
            point.TransformSource = "fallback";
            point.FallbackReason = "viewStateMismatch";
            point.MapTopLeftX = 1f;
            point.MapTopLeftY = 2f;
            point.MapScale = 3f;
            point.CurrentMapFullscreenPosX = 4300f;
            point.CurrentMapFullscreenPosY = 1300f;
            point.CurrentMapScale = 3f;
            point.CurrentGameUpdateCount = 789;
            point.TransformAgeUpdates = 9;

            if (placement == null ||
                placement.TileX != 320 ||
                placement.TileY != 200 ||
                placement.ScreenX != 740 ||
                placement.ScreenY != 460 ||
                placement.ScreenWidth != 1280 ||
                placement.ScreenHeight != 720 ||
                placement.WorldSizeX != 8400 ||
                placement.WorldSizeY != 2400 ||
                !string.Equals(placement.TransformSource, "fullscreenTransform", StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(placement.FallbackReason) ||
                Math.Abs(placement.MapTopLeftX - 99f) > 0.0001f ||
                Math.Abs(placement.MapTopLeftY - 55f) > 0.0001f ||
                Math.Abs(placement.MapScale - 2f) > 0.0001f ||
                Math.Abs(placement.CurrentMapFullscreenPosX - 4200f) > 0.0001f ||
                Math.Abs(placement.CurrentMapFullscreenPosY - 1200f) > 0.0001f ||
                Math.Abs(placement.CurrentMapScale - 2f) > 0.0001f ||
                placement.CurrentGameUpdateCount != 456 ||
                placement.TransformAgeUpdates != 1)
            {
                throw new InvalidOperationException("Map marker pending placement must freeze the right-click tile, screen anchor, and transform context before picker-time view changes.");
            }
        }

        private static void MapCustomMarkerStyleWhitelistAndPickerClamp()
        {
            var hasBed = false;
            var hasFallenStar = false;
            for (var index = 0; index < PlayerWorldMapMarkerStyles.All.Count; index++)
            {
                var style = PlayerWorldMapMarkerStyles.All[index];
                if (style.IconItemId == PlayerWorldMapMarkerConstants.ReplacementBedIconItemId &&
                    string.Equals(style.DisplayName, "床", StringComparison.Ordinal))
                {
                    hasBed = true;
                }

                if (style.IconItemId == PlayerWorldMapMarkerConstants.LegacyFallenStarIconItemId ||
                    string.Equals(style.DisplayName, "坠星", StringComparison.Ordinal))
                {
                    hasFallenStar = true;
                }
            }

            if (PlayerWorldMapMarkerStyles.All.Count != 8 ||
                !hasBed ||
                hasFallenStar ||
                !string.Equals(PlayerWorldMapMarkerStyles.GetDisplayName(999999), "火把", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map custom marker styles must keep eight active icons, replace fallen star with bed, and keep torch fallback.");
            }

            var anchoredPanel = MapCustomMarkerStylePickerOverlay.CalculatePanelRect(420, 240, 1280, 720);
            if (anchoredPanel.X != 420 ||
                anchoredPanel.Y != 240 ||
                MapCustomMarkerStylePickerOverlay.CalculatePanelClampedForTesting(420, 240, 1280, 720))
            {
                throw new InvalidOperationException("Map custom marker style picker must sit directly at the right-click anchor unless clamped by the screen edge.");
            }

            var panel = MapCustomMarkerStylePickerOverlay.CalculatePanelRect(1260, 700, 1280, 720);
            if (panel.Right > 1272 || panel.Bottom > 712 || panel.X < 8 || panel.Y < 8)
            {
                throw new InvalidOperationException("Map custom marker style picker must clamp inside the screen.");
            }

            if (!MapCustomMarkerStylePickerOverlay.CalculatePanelClampedForTesting(1260, 700, 1280, 720))
            {
                throw new InvalidOperationException("Map custom marker style picker must report panel clamp for diagnostics.");
            }

            AssertStringEquals(
                MapCustomMarkerStylePickerOverlay.GetVisualContractForTesting(),
                "icon-cells-only",
                "map marker picker visual contract");
        }

        private static void MapCustomMarkerFullscreenPickerDrawRouteUsesPostFullscreenMapDraw()
        {
            var routes = MapCustomMarkerStylePickerOverlay.GetDrawRoutesForTesting();
            if (routes == null ||
                routes.Length != 2 ||
                !string.Equals(routes[0], "uiOverlay", StringComparison.Ordinal) ||
                !string.Equals(routes[1], "fullscreenMap", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map marker picker must keep separate UI overlay and fullscreen map draw routes.");
            }

            if (!MapCustomMarkerStylePickerOverlay.ShouldUseFullscreenMapDrawRouteForTesting(true, true))
            {
                throw new InvalidOperationException("Fullscreen map picker draw route must run when picker and fullscreen map are both active.");
            }

            if (MapCustomMarkerStylePickerOverlay.ShouldUseFullscreenMapDrawRouteForTesting(false, true) ||
                MapCustomMarkerStylePickerOverlay.ShouldUseFullscreenMapDrawRouteForTesting(true, false))
            {
                throw new InvalidOperationException("Fullscreen map picker draw route must be idle unless both picker and fullscreen map are active.");
            }

            AssertStringEquals(
                MapCustomMarkerFullscreenMapDrawInstaller.GetHookTargetNameForTesting(),
                "Terraria.Main.OnPostFullscreenMapDraw",
                "fullscreen map picker draw hook target");
        }

        private static Microsoft.Xna.Framework.Vector2 ProjectMapOverlayTileToScreenForTesting(
            Microsoft.Xna.Framework.Vector2 tilePosition,
            Microsoft.Xna.Framework.Vector2 mapPosition,
            Microsoft.Xna.Framework.Vector2 mapOffset,
            float mapScale)
        {
            return (tilePosition - mapPosition) * mapScale + mapOffset;
        }

        private static PlayerWorldMapMarkerRecord CreateMapMarker(
            string markerId,
            int tileX,
            int tileY,
            int iconItemId,
            string name,
            int sortOrder)
        {
            return new PlayerWorldMapMarkerRecord
            {
                MarkerId = markerId,
                TileX = tileX,
                TileY = tileY,
                IconItemId = iconItemId,
                Name = name,
                CreatedUtc = "2026-06-15T00:00:00.0000000Z",
                UpdatedUtc = "2026-06-15T00:00:00.0000000Z",
                SortOrder = sortOrder
            };
        }
    }
}
