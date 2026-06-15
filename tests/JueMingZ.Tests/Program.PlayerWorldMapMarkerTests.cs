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
                PlayerWorldMapMarkersUiOnlyActionCount = 2,
                MapMarkerPickerOpen = true,
                MapMarkerPickerLastDraw = new DateTime(2026, 6, 15, 1, 4, 5, DateTimeKind.Utc),
                MapMarkerPickerLastFullscreenDraw = new DateTime(2026, 6, 15, 1, 4, 7, DateTimeKind.Utc),
                MapMarkerPickerDrawRoute = "fullscreenMap",
                MapMarkerPickerDrawSkippedReason = "spriteBatchUnavailable",
                MapMarkerPickerLastClick = new DateTime(2026, 6, 15, 1, 4, 6, DateTimeKind.Utc),
                MapMarkerPickerLastCloseReason = "rightClickClose",
                PlayerWorldMapMarkersLastReadUtc = new DateTime(2026, 6, 15, 1, 2, 3, DateTimeKind.Utc),
                PlayerWorldMapMarkersLastWriteUtc = new DateTime(2026, 6, 15, 1, 3, 4, DateTimeKind.Utc)
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
            AssertContains(json, "\"PlayerWorldMapMarkersUiOnlyActionCount\": 2");
            AssertContains(json, "\"MapMarkerPickerOpen\": true");
            AssertContains(json, "\"MapMarkerPickerLastDraw\": \"2026-06-15T01:04:05.0000000Z\"");
            AssertContains(json, "\"MapMarkerPickerLastFullscreenDraw\": \"2026-06-15T01:04:07.0000000Z\"");
            AssertContains(json, "\"MapMarkerPickerDrawRoute\": \"fullscreenMap\"");
            AssertContains(json, "\"MapMarkerPickerDrawSkippedReason\": \"spriteBatchUnavailable\"");
            AssertContains(json, "\"MapMarkerPickerLastClick\": \"2026-06-15T01:04:06.0000000Z\"");
            AssertContains(json, "\"MapMarkerPickerLastCloseReason\": \"rightClickClose\"");
            AssertContains(json, "\"PlayerWorldMapMarkersLastReadUtc\": \"2026-06-15T01:02:03.0000000Z\"");
            AssertContains(json, "\"PlayerWorldMapMarkersLastWriteUtc\": \"2026-06-15T01:03:04.0000000Z\"");
        }

        private static void LegacyMapEnhancementPageIncludesMapCustomMarkersRow()
        {
            var expectedHeight = LegacyUiMetrics.RowHeight * 7 +
                                 LegacyUiMetrics.SettingRowGap * 7 +
                                 LegacyMainWindow.CalculateMapMarkerListHeightForTesting(0) +
                                 24;
            if (LegacyMainWindow.CalculateMapEnhancementContentHeightForTesting() != expectedHeight)
            {
                throw new InvalidOperationException("Map enhancement content height must include custom marker row.");
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
            var emptyHeight = LegacyMainWindow.CalculateMapMarkerListHeightForTesting(0);
            var threeHeight = LegacyMainWindow.CalculateMapMarkerListHeightForTesting(3);
            if (threeHeight <= emptyHeight)
            {
                throw new InvalidOperationException("Map custom marker list height must grow with marker count.");
            }

            var inputId = LegacyMainWindow.BuildMapMarkerNameInputId("marker-a");
            AssertStringEquals(inputId, "map-custom-marker:name-input:marker-a", "map marker name input id");

            var tooltips = LegacyMainWindow.GetMapMarkerActionTooltipsForTesting();
            if (tooltips == null ||
                tooltips.Length != 5 ||
                !string.Equals(tooltips[0], "双击输入，限10个字", StringComparison.Ordinal) ||
                !string.Equals(tooltips[1], "地图跳转到标记位置", StringComparison.Ordinal) ||
                !string.Equals(tooltips[2], "分析可达路径", StringComparison.Ordinal) ||
                !string.Equals(tooltips[3], "消耗虫洞药水*1 回忆药水*1传送", StringComparison.Ordinal) ||
                !string.Equals(tooltips[4], "暂未实现", StringComparison.Ordinal))
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

        private static void MapFullscreenJumpTargetClampsPositionAndScale()
        {
            var target = MapFullscreenCompat.BuildJumpTargetForTesting(99999, -20, 8400, 2400, 999f);
            if (!target.Succeeded ||
                !string.Equals(target.ResultCode, "ok", StringComparison.Ordinal) ||
                target.TileX != 8399 ||
                target.TileY != 0 ||
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

        private static void MapFullscreenJumpTargetFailsWithoutWorldDimensions()
        {
            var target = MapFullscreenCompat.BuildJumpTargetForTesting(10, 20, 0, 2400, 2f);
            if (target.Succeeded ||
                !string.Equals(target.ResultCode, "worldUnavailable", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Fullscreen map jump target must fail soft when world dimensions are unavailable.");
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
            var point = MapCustomMarkerMapCompat.ScreenToTile(
                640,
                360,
                new Microsoft.Xna.Framework.Vector2(4200f, 1200f),
                2f,
                1280,
                720,
                8400,
                2400);
            if (point.TileX != 4200 || point.TileY != 1200)
            {
                throw new InvalidOperationException("Fullscreen map center screen point should map back to fullscreen map center tile.");
            }

            var clamped = MapCustomMarkerMapCompat.ScreenToTile(
                -99999,
                99999,
                new Microsoft.Xna.Framework.Vector2(4200f, 1200f),
                2f,
                1280,
                720,
                8400,
                2400);
            if (clamped.TileX < 0 || clamped.TileY < 0 || clamped.TileX >= 8400 || clamped.TileY >= 2400)
            {
                throw new InvalidOperationException("Fullscreen map mouse tile conversion must clamp to world bounds.");
            }
        }

        private static void MapCustomMarkerStyleWhitelistAndPickerClamp()
        {
            if (PlayerWorldMapMarkerStyles.All.Count != 8 ||
                !string.Equals(PlayerWorldMapMarkerStyles.GetDisplayName(999999), "火把", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Map custom marker styles must keep the frozen whitelist and torch fallback.");
            }

            var panel = MapCustomMarkerStylePickerOverlay.CalculatePanelRect(1260, 700, 1280, 720);
            if (panel.Right > 1272 || panel.Bottom > 712 || panel.X < 8 || panel.Y < 8)
            {
                throw new InvalidOperationException("Map custom marker style picker must clamp inside the screen.");
            }
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
