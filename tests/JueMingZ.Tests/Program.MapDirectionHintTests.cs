using System;
using System.Linq;
using JueMingZ.Actions;
using JueMingZ.Automation.MapEnhancement;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Input;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FeatureCatalogExposesMapDirectionHintConfig()
        {
            var registry = FeatureRegistry.CreateDefault();
            AssertMapDirectionHintFeature(
                registry,
                FeatureIds.MapRareCreatureDirection,
                "稀有生物显示方向",
                "装备生命体分析仪时显示稀有生物方向。",
                true);
            AssertMapDirectionHintFeature(
                registry,
                FeatureIds.MapTravellingMerchantDirection,
                "旅商显示方向",
                "显示旅商方位。",
                false);
        }

        private static void MapDirectionHintConfigDefaultsFeatureSyncAndRuntimeSnapshot()
        {
            var settings = AppSettings.CreateDefault();
            if (settings.MapRareCreatureDirectionEnabled ||
                settings.MapTravellingMerchantDirectionEnabled)
            {
                throw new InvalidOperationException("Map direction hints must default to off.");
            }

            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (snapshot.MapRareCreatureDirectionEnabled ||
                snapshot.MapTravellingMerchantDirectionEnabled)
            {
                throw new InvalidOperationException("Runtime snapshot must carry default disabled map direction hint flags.");
            }

            settings.MapRareCreatureDirectionEnabled = true;
            settings.MapTravellingMerchantDirectionEnabled = true;
            snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (!snapshot.MapRareCreatureDirectionEnabled ||
                !snapshot.MapTravellingMerchantDirectionEnabled)
            {
                throw new InvalidOperationException("Runtime snapshot must carry enabled map direction hint flags.");
            }

            var restore = PushTemporaryConfigDirectory("map-direction-hints-config");
            try
            {
                WriteConfigJson(ConfigService.AppSettingsPath, settings);
                WriteConfigJson(ConfigService.FeatureSettingsPath, FeatureSettings.CreateDefault());
                WriteConfigJson(ConfigService.HotkeySettingsPath, HotkeySettings.CreateDefault());
                ConfigService.Initialize();

                AssertFeatureEnabled(FeatureIds.MapRareCreatureDirection, "rare creature direction feature sync");
                AssertFeatureEnabled(FeatureIds.MapTravellingMerchantDirection, "travelling merchant direction feature sync");
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void RuntimeSettingsProviderSignatureTracksMapDirectionHintToggles()
        {
            ConfigService.ResetSettingsForTesting();
            RuntimeSettingsSnapshotProvider.ResetForTesting();
            try
            {
                var settings = ConfigService.AppSettings;
                settings.MapRareCreatureDirectionEnabled = false;
                settings.MapTravellingMerchantDirectionEnabled = false;
                var first = RuntimeSettingsSnapshotProvider.GetCurrent();
                if (first.MapRareCreatureDirectionEnabled ||
                    first.MapTravellingMerchantDirectionEnabled)
                {
                    throw new InvalidOperationException("Provider baseline must start with disabled map direction hints.");
                }

                settings.MapRareCreatureDirectionEnabled = true;
                settings.MapTravellingMerchantDirectionEnabled = true;
                var second = RuntimeSettingsSnapshotProvider.GetCurrent();
                if (object.ReferenceEquals(first, second) ||
                    !second.MapRareCreatureDirectionEnabled ||
                    !second.MapTravellingMerchantDirectionEnabled)
                {
                    throw new InvalidOperationException("Runtime settings signature must rebuild when map direction hint toggles change on the same settings object.");
                }
            }
            finally
            {
                RuntimeSettingsSnapshotProvider.ResetForTesting();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void LegacyMapEnhancementPageIncludesMapDirectionHintRows()
        {
            var tooltips = LegacyMainWindow.GetMapDirectionHintButtonTooltipsForTesting();
            if (tooltips == null || tooltips.Length != 4)
            {
                throw new InvalidOperationException("Map direction hint tooltip test contract must expose four button slots.");
            }

            AssertStringEquals(tooltips[0], "装备生命体分析仪时显示稀有生物方向", "rare creature direction on tooltip");
            AssertStringEquals(tooltips[1], string.Empty, "rare creature direction off tooltip");
            AssertStringEquals(tooltips[2], "显示旅商方位", "travelling merchant direction on tooltip");
            AssertStringEquals(tooltips[3], string.Empty, "travelling merchant direction off tooltip");

            var expectedHeight = LegacyUiMetrics.RowHeight * 9 +
                                 LegacyUiMetrics.SettingRowGap * 8 +
                                 LegacyMainWindow.CalculateMapMarkerListHeightForTesting(0) +
                                 24;
            if (LegacyMainWindow.CalculateMapEnhancementContentHeightForTesting() != expectedHeight)
            {
                throw new InvalidOperationException("Map enhancement content height must include rare creature and travelling merchant direction rows.");
            }
        }

        private static void LegacyMapDirectionHintHandlersToggleSettings()
        {
            var restore = PushTemporaryConfigDirectory("map-direction-hints-handlers");
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                settings.MapRareCreatureDirectionEnabled = false;
                settings.MapTravellingMerchantDirectionEnabled = false;

                DispatchMapDirectionHintToggle("map-rare-creature-direction-mode:On");
                if (!settings.MapRareCreatureDirectionEnabled ||
                    LegacyUiActionService.DispatchedCommandCountLast != 1)
                {
                    throw new InvalidOperationException("Rare creature direction command must dispatch once and enable the setting.");
                }

                AssertSaveSummaryUsesTestDirectory(ConfigService.LastSaveSummary, "rare creature direction toggle");

                DispatchMapDirectionHintToggle("map-travelling-merchant-direction-mode:On");
                if (!settings.MapTravellingMerchantDirectionEnabled ||
                    LegacyUiActionService.DispatchedCommandCountLast != 1)
                {
                    throw new InvalidOperationException("Travelling merchant direction command must dispatch once and enable the setting.");
                }

                AssertSaveSummaryUsesTestDirectory(ConfigService.LastSaveSummary, "travelling merchant direction toggle");
            }
            finally
            {
                LegacyUiInput.ResetInteractionState();
                LegacyUiInput.ResetActionUpdateGateStateForTesting();
                LegacyUiActionService.ResetActionUpdateDiagnosticsForTesting();
                restore();
            }
        }

        private static void LegacyMapEnhancementLayoutTracksMapDirectionHintState()
        {
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 200);

            var first = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("map_enhancement", window, content, 0, settings);
            settings.MapRareCreatureDirectionEnabled = true;
            var rareChanged = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("map_enhancement", window, content, 0, settings);
            if (rareChanged.PageStateSignature == first.PageStateSignature ||
                rareChanged.RebuildCount <= first.RebuildCount)
            {
                throw new InvalidOperationException("Map enhancement page layout must dirty when rare creature direction state changes.");
            }

            settings.MapTravellingMerchantDirectionEnabled = true;
            var travellingChanged = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("map_enhancement", window, content, 0, settings);
            if (travellingChanged.PageStateSignature == rareChanged.PageStateSignature ||
                travellingChanged.RebuildCount <= rareChanged.RebuildCount)
            {
                throw new InvalidOperationException("Map enhancement page layout must dirty when travelling merchant direction state changes.");
            }
        }

        private static void MapDirectionHintTargetServiceBuildsSnapshotAndHonorsCadence()
        {
            var settings = AppSettings.CreateDefault();
            var gameState = new GameStateSnapshot
            {
                IsInWorld = true,
                IsInMainMenu = false
            };
            var provider = new MapDirectionHintTestProvider(new[]
            {
                new MapDirectionHintNpcObservation
                {
                    Active = true,
                    Type = 616,
                    WhoAmI = 7,
                    CenterX = 160f,
                    CenterY = 96f,
                    DisplayName = "稀有生物",
                    Rarity = 3,
                    TownNpc = false,
                    Homeless = false,
                    HomeTileX = -1,
                    HomeTileY = -1,
                    Hidden = false
                },
                new MapDirectionHintNpcObservation
                {
                    Active = true,
                    Type = 368,
                    WhoAmI = 12,
                    CenterX = 800f,
                    CenterY = 320f,
                    DisplayName = "旅商",
                    Rarity = 0,
                    TownNpc = true,
                    Homeless = false,
                    HomeTileX = 44,
                    HomeTileY = 88,
                    Hidden = false
                },
                new MapDirectionHintNpcObservation
                {
                    Active = true,
                    Type = 999,
                    WhoAmI = 16,
                    CenterX = 640f,
                    CenterY = 320f,
                    DisplayName = "隐藏稀有",
                    Rarity = 5,
                    Hidden = true
                },
                new MapDirectionHintNpcObservation
                {
                    Active = false,
                    Type = 1000,
                    WhoAmI = 18,
                    CenterX = 640f,
                    CenterY = 320f,
                    DisplayName = "未激活",
                    Rarity = 5
                }
            });

            MapDirectionHintTargetService.ResetForTesting();
            MapDirectionHintTargetService.SetObservationProviderForTesting(provider);
            try
            {
                MapDirectionHintTargetService.Tick(RuntimeSettingsSnapshot.FromSettings(settings), gameState, 90);
                var disabled = MapDirectionHintTargetService.GetSnapshot();
                if (provider.ReadCount != 0 ||
                    disabled.Enabled ||
                    !string.Equals(disabled.Status, "disabled", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Disabled map direction hints must not scan NPCs.");
                }

                settings.MapRareCreatureDirectionEnabled = true;
                MapDirectionHintTargetService.Tick(RuntimeSettingsSnapshot.FromSettings(settings), gameState, 100);
                var first = MapDirectionHintTargetService.GetSnapshot();
                if (provider.ReadCount != 1 ||
                    !first.Enabled ||
                    !first.RareCreatureEnabled ||
                    first.TravellingMerchantEnabled ||
                    first.LastScanTick != 100 ||
                    first.NextScanTick != 100 + MapDirectionHintTargetService.ScanCadenceTicks ||
                    first.NpcCount != 4 ||
                    first.ActiveNpcCount != 3 ||
                    first.RareCandidateCount != 1 ||
                    first.TownNpcCount != 1 ||
                    first.HiddenNpcCount != 1)
                {
                    throw new InvalidOperationException("Map direction hint target snapshot did not capture expected NPC observation counters.");
                }

                var rare = first.Npcs[0];
                if (!rare.Active ||
                    rare.Type != 616 ||
                    rare.WhoAmI != 7 ||
                    !string.Equals(rare.DisplayName, "稀有生物", StringComparison.Ordinal) ||
                    rare.Rarity != 3 ||
                    rare.Hidden)
                {
                    throw new InvalidOperationException("Map direction hint NPC observation fields are incomplete.");
                }

                MapDirectionHintTargetService.Tick(RuntimeSettingsSnapshot.FromSettings(settings), gameState, 110);
                if (provider.ReadCount != 1)
                {
                    throw new InvalidOperationException("Map direction hint target service must honor scan cadence.");
                }

                settings.MapTravellingMerchantDirectionEnabled = true;
                MapDirectionHintTargetService.Tick(RuntimeSettingsSnapshot.FromSettings(settings), gameState, 115);
                var second = MapDirectionHintTargetService.GetSnapshot();
                if (provider.ReadCount != 2 ||
                    !second.TravellingMerchantEnabled ||
                    second.LastScanTick != 115)
                {
                    throw new InvalidOperationException("Map direction hint target service must rescan on cadence and carry both feature flags.");
                }
            }
            finally
            {
                MapDirectionHintTargetService.ResetForTesting();
            }
        }

        private static void MapDirectionHintProjectionClampsEllipseQuadrantsAndCorners()
        {
            var screen = new XnaRectangle(0, 0, 800, 600);
            var right = MapDirectionHintProjection.ClampToEllipseEdge(screen, new XnaVector2(1600f, 300f), 40f, 30f);
            var left = MapDirectionHintProjection.ClampToEllipseEdge(screen, new XnaVector2(-800f, 300f), 40f, 30f);
            var top = MapDirectionHintProjection.ClampToEllipseEdge(screen, new XnaVector2(400f, -900f), 40f, 30f);
            var bottom = MapDirectionHintProjection.ClampToEllipseEdge(screen, new XnaVector2(400f, 1200f), 40f, 30f);
            var corner = MapDirectionHintProjection.ClampToEllipseEdge(screen, new XnaVector2(1600f, 900f), 40f, 30f);

            AssertVectorClose(right.Position, 760f, 300f, "right ellipse edge");
            AssertVectorClose(left.Position, 40f, 300f, "left ellipse edge");
            AssertVectorClose(top.Position, 400f, 30f, "top ellipse edge");
            AssertVectorClose(bottom.Position, 400f, 570f, "bottom ellipse edge");
            if (corner.TargetInside ||
                corner.Position.X <= 400f ||
                corner.Position.X >= 760f ||
                corner.Position.Y <= 300f ||
                corner.Position.Y >= 570f)
            {
                throw new InvalidOperationException("Corner ellipse projection must stay on the ellipse instead of hard-clamping to a screen corner.");
            }
        }

        private static void MapDirectionHintProjectionHandlesVisibilityArrowAndDistance()
        {
            if (!MapDirectionHintProjection.IsWorldPointOnScreen(
                    new XnaVector2(500f, 400f),
                    new XnaVector2(100f, 100f),
                    800,
                    600,
                    0) ||
                MapDirectionHintProjection.IsWorldPointOnScreen(
                    new XnaVector2(950f, 400f),
                    new XnaVector2(100f, 100f),
                    800,
                    600,
                    0))
            {
                throw new InvalidOperationException("Map direction hint screen visibility check must use world-to-screen coordinates.");
            }

            var anchor = MapDirectionHintProjection.BuildPlayerArrowAnchor(
                new XnaVector2(400f, 300f),
                new XnaVector2(1000f, 300f),
                42f);
            if (!anchor.HasDirection)
            {
                throw new InvalidOperationException("Player arrow anchor must expose a direction for off-center targets.");
            }

            AssertVectorClose(anchor.Position, 442f, 300f, "player arrow anchor");
            AssertVectorClose(anchor.Direction, 1f, 0f, "player arrow direction");

            var noDirection = MapDirectionHintProjection.BuildPlayerArrowAnchor(
                new XnaVector2(400f, 300f),
                new XnaVector2(400f, 300f),
                42f);
            if (noDirection.HasDirection)
            {
                throw new InvalidOperationException("Player arrow anchor must fail closed when target overlaps the player.");
            }

            AssertStringEquals(MapDirectionHintProjection.FormatApproxTileDistance(0f), "约0格", "zero distance text");
            AssertStringEquals(MapDirectionHintProjection.FormatApproxTileDistance(10f), "约1格", "near distance text");
            AssertStringEquals(MapDirectionHintProjection.FormatApproxTileDistance(1280f), "约80格", "far distance text");
        }

        private static void MapTravellingMerchantResolverSelectsNpcId368AndHidesOnScreen()
        {
            var merchant = CreateTravellingMerchant(12, 368, true, 400f, 300f);
            var inactive = CreateTravellingMerchant(8, 368, false, 1200f, 300f);
            var nonMerchant = CreateTravellingMerchant(7, 17, true, 1200f, 300f);
            var town = MapTravellingMerchantTownResolver.ResolveForTesting(
                merchant,
                new[] { inactive, nonMerchant, merchant },
                new MapTravellingMerchantPylonSnapshot[0],
                CreateWorldContext());
            var target = MapTravellingMerchantDirectionTargetResolver.BuildTargetForTesting(merchant, town, 120);
            MapTravellingMerchantDirectionProjection projection;
            if (!MapTravellingMerchantDirectionTargetResolver.TryBuildProjectionForTesting(
                    target,
                    CreateScreenContext(0f, 0f, 800, 600, 100f, 300f),
                    out projection) ||
                !projection.OnScreen ||
                projection.ShouldDraw ||
                !string.Equals(projection.Status, "onScreenHidden", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Travelling merchant direction must hide edge label while the merchant is on screen.");
            }

            var snapshot = MapDirectionHintTargetService.BuildSnapshotForTesting(
                new[] { inactive, nonMerchant, merchant },
                false,
                true,
                120,
                135,
                "test");
            if (snapshot.TravellingMerchantTarget == null ||
                !snapshot.TravellingMerchantTarget.Active ||
                snapshot.TravellingMerchantTarget.Type != MapTravellingMerchantDirectionTargetResolver.TravellingMerchantNpcType ||
                snapshot.TravellingMerchantTarget.WhoAmI != 12)
            {
                throw new InvalidOperationException("Travelling merchant target service must select the active NPCID 368 observation.");
            }
        }

        private static void MapTravellingMerchantEdgeLabelUsesEllipseAndThreeLines()
        {
            var merchant = CreateTravellingMerchant(12, 368, true, 1600f, 300f);
            var town = MapTravellingMerchantTownResolver.ResolveForTesting(
                merchant,
                new[] { merchant },
                new[]
                {
                    new MapTravellingMerchantPylonSnapshot
                    {
                        TileX = 101,
                        TileY = 19,
                        PylonType = 5
                    }
                },
                CreateWorldContext());
            var target = MapTravellingMerchantDirectionTargetResolver.BuildTargetForTesting(merchant, town, 220);
            MapTravellingMerchantDirectionProjection projection;
            if (!MapTravellingMerchantDirectionTargetResolver.TryBuildProjectionForTesting(
                    target,
                    CreateScreenContext(0f, 0f, 800, 600, 0f, 300f),
                    out projection) ||
                projection.OnScreen ||
                !projection.ShouldDraw)
            {
                throw new InvalidOperationException("Off-screen travelling merchant must produce an edge label projection.");
            }

            AssertStringEquals(projection.LabelLine1, "旅商", "travelling merchant label line 1");
            AssertStringEquals(projection.LabelLine2, "约100格", "travelling merchant label line 2");
            AssertStringEquals(projection.LabelLine3, "沙漠城镇", "travelling merchant label line 3");
            AssertStringEquals(town.Source, MapTravellingMerchantTownResolver.SourcePylon, "travelling merchant pylon source");
            AssertStringEquals(town.Confidence, "high", "travelling merchant pylon confidence");
            AssertStringEquals(town.MatchedPylonType, "Desert", "travelling merchant matched pylon type");
            if (projection.EdgeX <= 400f ||
                projection.EdgeX >= 800f ||
                projection.EdgeY <= 0f ||
                projection.EdgeY >= 600f)
            {
                throw new InvalidOperationException("Travelling merchant edge label must use ellipse projection instead of hard screen corners.");
            }

            var layout = MapDirectionHintOverlay.BuildTravellingMerchantLabelLayoutForTesting(projection, 800, 600);
            if (layout.Width <= 0 ||
                layout.Height <= 0 ||
                layout.X < 0 ||
                layout.Y < 0 ||
                layout.X + layout.Width > 800 ||
                layout.Y + layout.Height > 600)
            {
                throw new InvalidOperationException("Travelling merchant label layout must stay inside the screen.");
            }
        }

        private static void MapTravellingMerchantTownResolverUsesClusterBiomeAndUnknownSources()
        {
            var merchant = CreateTravellingMerchant(12, 368, true, 8000f, 1600f);
            var towns = new[]
            {
                merchant,
                CreateTownNpc(20, 496f, 100f, 496, 100),
                CreateTownNpc(21, 499f, 103f, 499, 103)
            };

            var cluster = MapTravellingMerchantTownResolver.ResolveForTesting(
                merchant,
                towns,
                new MapTravellingMerchantPylonSnapshot[0],
                CreateWorldContext());
            AssertStringEquals(cluster.Source, MapTravellingMerchantTownResolver.SourceTownCluster, "travelling merchant town cluster source");
            AssertStringEquals(cluster.Confidence, "medium", "travelling merchant town cluster confidence");
            AssertStringEquals(cluster.Label, "森林城镇", "travelling merchant town cluster label");
            if (cluster.NearbyTownNpcCount != 2)
            {
                throw new InvalidOperationException("Travelling merchant town cluster source must count nearby housed town NPCs.");
            }

            var oceanMerchant = CreateTravellingMerchant(12, 368, true, 80f * 16f, 95f * 16f);
            var pointBiome = MapTravellingMerchantTownResolver.ResolveForTesting(
                oceanMerchant,
                new[] { oceanMerchant },
                new MapTravellingMerchantPylonSnapshot[0],
                CreateWorldContext());
            AssertStringEquals(pointBiome.Source, MapTravellingMerchantTownResolver.SourcePointBiome, "travelling merchant point biome source");
            AssertStringEquals(pointBiome.Confidence, "low", "travelling merchant point biome confidence");
            AssertStringEquals(pointBiome.Label, "海洋附近", "travelling merchant point biome label");

            var unknown = MapTravellingMerchantTownResolver.ResolveForTesting(
                oceanMerchant,
                new[] { oceanMerchant },
                new MapTravellingMerchantPylonSnapshot[0],
                MapTravellingMerchantWorldContext.Unavailable());
            AssertStringEquals(unknown.Source, MapTravellingMerchantTownResolver.SourceUnknown, "travelling merchant unknown source");
            AssertStringEquals(unknown.Confidence, "none", "travelling merchant unknown confidence");
            AssertStringEquals(unknown.Label, "环境未知", "travelling merchant unknown label");
        }

        private static void MapTravellingMerchantDiagnosticsWriteRuntimeSnapshotJson()
        {
            var snapshot = new DiagnosticSnapshot
            {
                MapTravellingMerchantDirectionEnabled = true,
                MapTravellingMerchantDirectionStatus = "targetReady",
                MapTravellingMerchantDirectionMessage = "travelling merchant target resolved",
                MapTravellingMerchantDirectionTargetActive = true,
                MapTravellingMerchantDirectionTargetWhoAmI = 12,
                MapTravellingMerchantDirectionTargetType = 368,
                MapTravellingMerchantDirectionTargetName = "旅商",
                MapTravellingMerchantDirectionTargetWorldX = 1600d,
                MapTravellingMerchantDirectionTargetWorldY = 300d,
                MapTravellingMerchantDirectionOnScreen = false,
                MapTravellingMerchantDirectionDistancePixels = 1600d,
                MapTravellingMerchantDirectionDistanceText = "约100格",
                MapTravellingMerchantDirectionTownLabel = "沙漠城镇",
                MapTravellingMerchantDirectionTownLabelSource = "pylon",
                MapTravellingMerchantDirectionTownLabelConfidence = "high",
                MapTravellingMerchantDirectionMatchedPylonType = "Desert",
                MapTravellingMerchantDirectionMatchedPylonDistanceTiles = 1.25d,
                MapTravellingMerchantDirectionLastScanTick = 220,
                MapTravellingMerchantDirectionLastScanAgeTicks = 9,
                MapTravellingMerchantDirectionDrawStatus = "drawn"
            };
            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"MapTravellingMerchantDirectionEnabled\": true");
            AssertContains(json, "\"MapTravellingMerchantDirectionTargetActive\": true");
            AssertContains(json, "\"MapTravellingMerchantDirectionTargetType\": 368");
            AssertContains(json, "\"MapTravellingMerchantDirectionOnScreen\": false");
            AssertContains(json, "\"MapTravellingMerchantDirectionDistanceText\": \"约100格\"");
            AssertContains(json, "\"MapTravellingMerchantDirectionTownLabelSource\": \"pylon\"");
            AssertContains(json, "\"MapTravellingMerchantDirectionTownLabelConfidence\": \"high\"");
            AssertContains(json, "\"MapTravellingMerchantDirectionMatchedPylonType\": \"Desert\"");
            AssertContains(json, "\"MapTravellingMerchantDirectionMatchedPylonDistanceTiles\": 1.25");
            AssertContains(json, "\"MapTravellingMerchantDirectionLastScanAgeTicks\": 9");
        }

        private static void MapRareCreatureGatesLifeformAnalyzerAndHiddenInfo()
        {
            var observations = new[]
            {
                CreateRareCreature(8, 616, true, 160f, 0f, 3, false)
            };

            var noAnalyzer = MapRareCreatureDirectionTargetResolver.Resolve(
                true,
                observations,
                CreateRarePlayerContext(false, false, 0f, 0f),
                300);
            if (noAnalyzer.Active ||
                !string.Equals(noAnalyzer.Status, "gateBlocked", StringComparison.Ordinal) ||
                !string.Equals(noAnalyzer.GateReason, "lifeformAnalyzerMissing", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Rare creature direction must fail closed without lifeform analyzer capability.");
            }

            var hidden = MapRareCreatureDirectionTargetResolver.Resolve(
                true,
                observations,
                CreateRarePlayerContext(true, true, 0f, 0f),
                301);
            if (hidden.Active ||
                !hidden.InfoAccessoryHidden ||
                !string.Equals(hidden.GateReason, "infoAccessoryHidden", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Rare creature direction must respect hidden lifeform analyzer info state.");
            }

            var disabled = MapRareCreatureDirectionTargetResolver.Resolve(
                false,
                observations,
                CreateRarePlayerContext(true, false, 0f, 0f),
                302);
            if (disabled.Enabled ||
                !string.Equals(disabled.Status, "disabled", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Rare creature direction resolver must expose a disabled target when the setting is off.");
            }
        }

        private static void MapRareCreatureResolverSelectsHighestRarityWithinRadius()
        {
            var observations = new[]
            {
                CreateRareCreature(2, 600, true, 96f, 0f, 4, false),
                CreateRareCreature(3, 601, true, 640f, 0f, 5, false),
                CreateRareCreature(4, 602, true, 120f, 0f, 5, false),
                CreateRareCreature(5, 603, true, 1299.5f, 0f, 6, false),
                CreateRareCreature(6, 604, true, 1300f, 0f, 9, false),
                CreateRareCreature(7, 605, true, 40f, 0f, 10, true),
                CreateRareCreature(8, 606, false, 40f, 0f, 11, false)
            };

            var target = MapRareCreatureDirectionTargetResolver.Resolve(
                true,
                observations,
                CreateRarePlayerContext(true, false, 0f, 0f),
                320);
            if (!target.Active ||
                target.WhoAmI != 5 ||
                target.Rarity != 6 ||
                Math.Abs(target.DistancePixels - 1299.5f) > 0.01f)
            {
                throw new InvalidOperationException("Rare creature direction must select the highest rarity active visible target inside 1300px.");
            }

            var tie = MapRareCreatureDirectionTargetResolver.Resolve(
                true,
                new[]
                {
                    CreateRareCreature(20, 620, true, 640f, 0f, 5, false),
                    CreateRareCreature(21, 621, true, 96f, 0f, 5, false)
                },
                CreateRarePlayerContext(true, false, 0f, 0f),
                321);
            if (!tie.Active ||
                tie.WhoAmI != 21 ||
                !string.Equals(tie.DistanceText, "约6格", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Rare creature direction must use distance as the same-rarity tie-break.");
            }

            var none = MapRareCreatureDirectionTargetResolver.Resolve(
                true,
                new[] { CreateRareCreature(30, 630, true, 1300f, 0f, 2, false) },
                CreateRarePlayerContext(true, false, 0f, 0f),
                322);
            if (none.Active ||
                !string.Equals(none.Status, "targetUnavailable", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Rare creature direction must keep the original strict 1300px boundary.");
            }
        }

        private static void MapRareCreatureProjectionDrawsArrowAndWeakensOnScreen()
        {
            var target = MapRareCreatureDirectionTargetResolver.Resolve(
                true,
                new[] { CreateRareCreature(11, 700, true, 1600f, 300f, 4, false) },
                CreateRarePlayerContext(true, false, 400f, 300f),
                340);
            MapRareCreatureDirectionProjection projection;
            if (!MapRareCreatureDirectionTargetResolver.TryBuildProjectionForTesting(
                    target,
                    CreateScreenContext(0f, 0f, 800, 600, 400f, 300f),
                    out projection) ||
                projection.OnScreen ||
                !projection.ShouldDraw ||
                !projection.ShouldDrawLabel ||
                !string.Equals(projection.ArrowGlyph, "→", StringComparison.Ordinal) ||
                !string.Equals(projection.LabelLine1, "稀有生物", StringComparison.Ordinal) ||
                !string.Equals(projection.LabelLine2, "约75格", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Off-screen rare creature target must draw a player-side arrow with name and distance label.");
            }

            var layout = MapDirectionHintOverlay.BuildRareCreatureLabelLayoutForTesting(projection, 800, 600);
            if (layout.Width <= 0 ||
                layout.Height <= 0 ||
                layout.X < 0 ||
                layout.Y < 0 ||
                layout.X + layout.Width > 800 ||
                layout.Y + layout.Height > 600)
            {
                throw new InvalidOperationException("Rare creature label layout must stay inside the screen.");
            }

            var onScreenTarget = MapRareCreatureDirectionTargetResolver.Resolve(
                true,
                new[] { CreateRareCreature(12, 701, true, 500f, 300f, 4, false) },
                CreateRarePlayerContext(true, false, 400f, 300f),
                341);
            MapRareCreatureDirectionProjection onScreen;
            if (!MapRareCreatureDirectionTargetResolver.TryBuildProjectionForTesting(
                    onScreenTarget,
                    CreateScreenContext(0f, 0f, 800, 600, 400f, 300f),
                    out onScreen) ||
                !onScreen.OnScreen ||
                !onScreen.ShouldDraw ||
                onScreen.ShouldDrawLabel ||
                !string.Equals(onScreen.Status, "onScreenArrowOnly", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("On-screen rare creature target must weaken to an arrow without name or distance label.");
            }
        }

        private static void MapRareCreatureDiagnosticsRecordGateTargetAndProjection()
        {
            MapDirectionHintDiagnostics.ResetForTesting();
            var target = MapRareCreatureDirectionTargetResolver.Resolve(
                true,
                new[] { CreateRareCreature(12, 710, true, 1600f, 300f, 6, false) },
                CreateRarePlayerContext(true, false, 400f, 300f),
                360);
            MapDirectionHintDiagnostics.RecordRareCreatureTarget(target);
            MapRareCreatureDirectionProjection projection;
            MapRareCreatureDirectionTargetResolver.TryBuildProjectionForTesting(
                target,
                CreateScreenContext(0f, 0f, 800, 600, 400f, 300f),
                out projection);
            MapDirectionHintDiagnostics.RecordRareCreatureProjection(projection, "drawnWithLabel");

            var snapshot = MapDirectionHintDiagnostics.GetSnapshot();
            if (!snapshot.MapRareCreatureDirectionEnabled ||
                !snapshot.MapRareCreatureDirectionTargetActive ||
                snapshot.MapRareCreatureDirectionTargetRarity != 6 ||
                !string.Equals(snapshot.MapRareCreatureDirectionGateReason, "ready", StringComparison.Ordinal) ||
                !string.Equals(snapshot.MapRareCreatureDirectionDrawStatus, "drawnWithLabel", StringComparison.Ordinal) ||
                !snapshot.MapRareCreatureDirectionShouldDrawLabel ||
                !string.Equals(snapshot.MapRareCreatureDirectionArrowGlyph, "→", StringComparison.Ordinal) ||
                snapshot.MapRareCreatureDirectionLastScanTick != 360)
            {
                throw new InvalidOperationException("Rare creature diagnostics must retain gate, target, rarity, distance, on-screen and draw projection state.");
            }
        }

        private static void MapRareCreatureDiagnosticsWriteRuntimeSnapshotJson()
        {
            var snapshot = new DiagnosticSnapshot
            {
                MapDirectionHintTargetScanCadenceTicks = 15,
                MapRareCreatureDirectionEnabled = true,
                MapRareCreatureDirectionStatus = "targetReady",
                MapRareCreatureDirectionMessage = "rare creature target resolved",
                MapRareCreatureDirectionGateReason = "ready",
                MapRareCreatureDirectionHasLifeformAnalyzer = true,
                MapRareCreatureDirectionInfoAccessoryHidden = false,
                MapRareCreatureDirectionTargetActive = true,
                MapRareCreatureDirectionTargetWhoAmI = 12,
                MapRareCreatureDirectionTargetType = 710,
                MapRareCreatureDirectionTargetName = "金蠕虫",
                MapRareCreatureDirectionTargetRarity = 6,
                MapRareCreatureDirectionTargetWorldX = 1600d,
                MapRareCreatureDirectionTargetWorldY = 300d,
                MapRareCreatureDirectionOnScreen = false,
                MapRareCreatureDirectionShouldDrawLabel = true,
                MapRareCreatureDirectionDistancePixels = 1200d,
                MapRareCreatureDirectionDistanceText = "约75格",
                MapRareCreatureDirectionArrowScreenX = 446d,
                MapRareCreatureDirectionArrowScreenY = 300d,
                MapRareCreatureDirectionDirectionX = 1d,
                MapRareCreatureDirectionDirectionY = 0d,
                MapRareCreatureDirectionArrowGlyph = "→",
                MapRareCreatureDirectionLabelLine1 = "金蠕虫",
                MapRareCreatureDirectionLabelLine2 = "约75格",
                MapRareCreatureDirectionLastScanTick = 360,
                MapRareCreatureDirectionLastScanAgeTicks = 12,
                MapRareCreatureDirectionDrawStatus = "drawnWithLabel"
            };
            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"MapDirectionHintTargetScanCadenceTicks\": 15");
            AssertContains(json, "\"MapRareCreatureDirectionEnabled\": true");
            AssertContains(json, "\"MapRareCreatureDirectionGateReason\": \"ready\"");
            AssertContains(json, "\"MapRareCreatureDirectionHasLifeformAnalyzer\": true");
            AssertContains(json, "\"MapRareCreatureDirectionInfoAccessoryHidden\": false");
            AssertContains(json, "\"MapRareCreatureDirectionTargetActive\": true");
            AssertContains(json, "\"MapRareCreatureDirectionTargetRarity\": 6");
            AssertContains(json, "\"MapRareCreatureDirectionOnScreen\": false");
            AssertContains(json, "\"MapRareCreatureDirectionShouldDrawLabel\": true");
            AssertContains(json, "\"MapRareCreatureDirectionDistanceText\": \"约75格\"");
            AssertContains(json, "\"MapRareCreatureDirectionArrowGlyph\": \"→\"");
            AssertContains(json, "\"MapRareCreatureDirectionLastScanAgeTicks\": 12");
            AssertContains(json, "\"MapRareCreatureDirectionDrawStatus\": \"drawnWithLabel\"");
        }

        private static void AssertMapDirectionHintFeature(
            FeatureRegistry registry,
            string featureId,
            string displayName,
            string description,
            bool requiresPlayerState)
        {
            FeatureDefinition feature;
            if (!registry.TryGet(featureId, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected map direction hint feature to be registered: " + featureId);
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented || feature.DefaultEnabled)
            {
                throw new InvalidOperationException("Map direction hint features must be visible, implemented, and disabled by default.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.MapEnhancement ||
                feature.UserCategory != FeatureUserCategory.MapEnhancement ||
                feature.MultiplayerSupport != FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
            {
                throw new InvalidOperationException("Map direction hint metadata must stay in the map enhancement bucket.");
            }

            if (feature.RequiredActions.Count != 1 ||
                feature.RequiredActions[0] != InputActionKind.None ||
                !feature.RequiredGameState.Contains(GameStateKind.Npcs) ||
                !feature.RequiredGameState.Contains(GameStateKind.World) ||
                !feature.RequiredGameState.Contains(GameStateKind.UiState) ||
                (requiresPlayerState && !feature.RequiredGameState.Contains(GameStateKind.Player)))
            {
                throw new InvalidOperationException("Map direction hints must be display-only and declare their read-only game-state dependencies.");
            }

            AssertStringEquals(feature.DisplayName, displayName, featureId + " display name");
            AssertStringEquals(feature.Description, description, featureId + " description");
        }

        private static MapDirectionHintNpcObservation CreateTravellingMerchant(
            int whoAmI,
            int type,
            bool active,
            float centerX,
            float centerY)
        {
            return new MapDirectionHintNpcObservation
            {
                Active = active,
                Type = type,
                WhoAmI = whoAmI,
                CenterX = centerX,
                CenterY = centerY,
                DisplayName = "旅商",
                TownNpc = true,
                Homeless = false,
                HomeTileX = (int)Math.Round(centerX / 16f),
                HomeTileY = (int)Math.Round(centerY / 16f),
                Hidden = false
            };
        }

        private static MapDirectionHintNpcObservation CreateRareCreature(
            int whoAmI,
            int type,
            bool active,
            float centerX,
            float centerY,
            int rarity,
            bool hidden)
        {
            return new MapDirectionHintNpcObservation
            {
                Active = active,
                Type = type,
                WhoAmI = whoAmI,
                CenterX = centerX,
                CenterY = centerY,
                DisplayName = "稀有生物",
                Rarity = rarity,
                TownNpc = false,
                Homeless = false,
                HomeTileX = -1,
                HomeTileY = -1,
                Hidden = hidden
            };
        }

        private static MapRareCreatureDirectionPlayerContext CreateRarePlayerContext(
            bool hasLifeformAnalyzer,
            bool infoAccessoryHidden,
            float centerX,
            float centerY)
        {
            return new MapRareCreatureDirectionPlayerContext
            {
                Available = true,
                HasLifeformAnalyzer = hasLifeformAnalyzer,
                InfoAccessoryHidden = infoAccessoryHidden,
                PlayerCenterX = centerX,
                PlayerCenterY = centerY
            };
        }

        private static MapDirectionHintNpcObservation CreateTownNpc(
            int whoAmI,
            float tileX,
            float tileY,
            int homeTileX,
            int homeTileY)
        {
            return new MapDirectionHintNpcObservation
            {
                Active = true,
                Type = 22 + whoAmI,
                WhoAmI = whoAmI,
                CenterX = tileX * 16f,
                CenterY = tileY * 16f,
                DisplayName = "城镇NPC",
                TownNpc = true,
                Homeless = false,
                HomeTileX = homeTileX,
                HomeTileY = homeTileY,
                Hidden = false
            };
        }

        private static MapTravellingMerchantWorldContext CreateWorldContext()
        {
            return new MapTravellingMerchantWorldContext
            {
                HasWorldFacts = true,
                MaxTilesX = 8400,
                MaxTilesY = 2400,
                WorldSurfaceTileY = 300d
            };
        }

        private static MapDirectionHintScreenContext CreateScreenContext(
            float screenX,
            float screenY,
            int screenWidth,
            int screenHeight,
            float playerCenterX,
            float playerCenterY)
        {
            return new MapDirectionHintScreenContext
            {
                ScreenX = screenX,
                ScreenY = screenY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                PlayerCenterX = playerCenterX,
                PlayerCenterY = playerCenterY
            };
        }

        private static void AssertFeatureEnabled(string featureId, string label)
        {
            bool enabled;
            if (ConfigService.FeatureSettings == null ||
                ConfigService.FeatureSettings.EnabledByFeatureId == null ||
                !ConfigService.FeatureSettings.EnabledByFeatureId.TryGetValue(featureId, out enabled) ||
                !enabled)
            {
                throw new InvalidOperationException("Feature settings must synchronize " + label + ".");
            }
        }

        private static void DispatchMapDirectionHintToggle(string elementId)
        {
            LegacyUiInput.ResetInteractionState();
            LegacyUiInput.ResetActionUpdateGateStateForTesting();
            LegacyUiActionService.ResetActionUpdateDiagnosticsForTesting();
            LegacyUiInput.EnqueueClick(
                new LegacyUiElement
                {
                    Id = elementId,
                    Label = "开启",
                    Kind = "button",
                    Rect = new LegacyUiRect(8, 8, 64, 24),
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

            LegacyUiActionService.Update(new InputActionQueue(), null);
        }

        private static void AssertVectorClose(XnaVector2 actual, float expectedX, float expectedY, string label)
        {
            const float tolerance = 0.01f;
            if (Math.Abs(actual.X - expectedX) > tolerance ||
                Math.Abs(actual.Y - expectedY) > tolerance)
            {
                throw new InvalidOperationException(
                    label + " expected (" + expectedX + "," + expectedY + ") but got (" + actual.X + "," + actual.Y + ").");
            }
        }

        private sealed class MapDirectionHintTestProvider : IMapDirectionHintNpcObservationProvider
        {
            private readonly MapDirectionHintNpcObservation[] _observations;

            public MapDirectionHintTestProvider(MapDirectionHintNpcObservation[] observations)
            {
                _observations = observations ?? new MapDirectionHintNpcObservation[0];
            }

            public int ReadCount { get; private set; }

            public bool TryRead(out MapDirectionHintNpcObservation[] observations, out string message)
            {
                ReadCount++;
                observations = new MapDirectionHintNpcObservation[_observations.Length];
                for (var index = 0; index < _observations.Length; index++)
                {
                    observations[index] = _observations[index] == null ? null : _observations[index].Clone();
                }

                message = "test";
                return true;
            }
        }
    }
}
