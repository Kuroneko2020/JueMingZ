using JueMingZ.Compat;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementVisibilityService
    {
        public static MapQuickAnnouncementVisibilityDecision Evaluate(
            MapQuickAnnouncementVisibilityRequest request)
        {
            request = request ?? new MapQuickAnnouncementVisibilityRequest();

            TerrariaTileVisibilityEvidence evidence;
            if (!TerrariaTileVisibilityCompat.TryReadVisibilityEvidence(
                    request.TileX,
                    request.TileY,
                    request.RawTile,
                    ResolveTileType(request.Tile),
                    ResolveTileStyle(request.Tile),
                    ResolveFrameX(request.Tile),
                    ResolveFrameY(request.Tile),
                    ResolveWallType(request.Wall),
                    ResolveLiquidType(request.Tile),
                    request.PerspectivePlayer,
                    out evidence))
            {
                evidence = TerrariaTileVisibilityEvidence.Unavailable("compatReadFailed");
            }

            return EvaluateWithEvidence(request, evidence);
        }

        internal static MapQuickAnnouncementVisibilityDecision EvaluateForTesting(
            MapQuickAnnouncementVisibilityRequest request,
            TerrariaTileVisibilityEvidence evidence)
        {
            return EvaluateWithEvidence(request ?? new MapQuickAnnouncementVisibilityRequest(), evidence);
        }

        private static MapQuickAnnouncementVisibilityDecision EvaluateWithEvidence(
            MapQuickAnnouncementVisibilityRequest request,
            TerrariaTileVisibilityEvidence evidence)
        {
            evidence = evidence ?? TerrariaTileVisibilityEvidence.Unavailable("evidenceMissing");

            var decision = new MapQuickAnnouncementVisibilityDecision();
            decision.Tile = EvaluateTile(request, evidence, decision);
            decision.Wall = EvaluateWall(request, evidence, decision);
            decision.Liquid = EvaluateLiquid(request, evidence, decision);
            decision.Circuit = EvaluateCircuit(request, decision);
            EvaluateEmptyAir(request, evidence, decision);

            if (!evidence.ReadSucceeded)
            {
                decision.AddReason("compat:" + evidence.FailureReason);
            }

            return decision;
        }

        private static MapQuickAnnouncementLayerVisibility EvaluateTile(
            MapQuickAnnouncementVisibilityRequest request,
            TerrariaTileVisibilityEvidence evidence,
            MapQuickAnnouncementVisibilityDecision decision)
        {
            if (request.Tile == null || !request.Tile.Active)
            {
                return MapQuickAnnouncementVisibilityDecision.Hidden(
                    MapQuickAnnouncementVisibilityLayer.Tile,
                    "tile:notPresent");
            }

            if (!evidence.ReadSucceeded)
            {
                return MapQuickAnnouncementVisibilityDecision.Unavailable(
                    MapQuickAnnouncementVisibilityLayer.Tile,
                    evidence.FailureReason);
            }

            // Echo-native IDs are the user's explicit exception; echo coating is
            // only a hidden flag and must continue through the fail-closed path.
            if (evidence.EchoNativeTile)
            {
                decision.AddReason("tile:echoNative");
                return MapQuickAnnouncementVisibilityDecision.EchoNativeAllowed(
                    MapQuickAnnouncementVisibilityLayer.Tile,
                    "tile:echoNative");
            }

            if (evidence.TileHidden && !evidence.ShouldShowInvisibleBlocksAndWalls)
            {
                decision.AddReason("tile:hiddenWithoutEchoView");
                return MapQuickAnnouncementVisibilityDecision.Hidden(
                    MapQuickAnnouncementVisibilityLayer.Tile,
                    "tile:hiddenWithoutEchoView");
            }

            if (evidence.TileHidden)
            {
                decision.AddReason("tile:echoView");
            }

            var visibleReason = ResolveTileVisibleReason(evidence);
            if (!string.IsNullOrWhiteSpace(visibleReason))
            {
                decision.AddReason(visibleReason);
                return MapQuickAnnouncementVisibilityDecision.Visible(
                    MapQuickAnnouncementVisibilityLayer.Tile,
                    visibleReason);
            }

            decision.AddReason("tile:noVisibleEvidence");
            return MapQuickAnnouncementVisibilityDecision.Hidden(
                MapQuickAnnouncementVisibilityLayer.Tile,
                "tile:noVisibleEvidence");
        }

        private static MapQuickAnnouncementLayerVisibility EvaluateWall(
            MapQuickAnnouncementVisibilityRequest request,
            TerrariaTileVisibilityEvidence evidence,
            MapQuickAnnouncementVisibilityDecision decision)
        {
            if (request.Wall == null || !request.Wall.Active)
            {
                return MapQuickAnnouncementVisibilityDecision.Hidden(
                    MapQuickAnnouncementVisibilityLayer.Wall,
                    "wall:notPresent");
            }

            if (!evidence.ReadSucceeded)
            {
                return MapQuickAnnouncementVisibilityDecision.Unavailable(
                    MapQuickAnnouncementVisibilityLayer.Wall,
                    evidence.FailureReason);
            }

            if (evidence.WallBlockedByFullTile)
            {
                decision.AddReason("wall:blockedByFullTile");
                return MapQuickAnnouncementVisibilityDecision.Hidden(
                    MapQuickAnnouncementVisibilityLayer.Wall,
                    "wall:blockedByFullTile");
            }

            if (evidence.EchoNativeWall)
            {
                decision.AddReason("wall:echoNative");
                return MapQuickAnnouncementVisibilityDecision.EchoNativeAllowed(
                    MapQuickAnnouncementVisibilityLayer.Wall,
                    "wall:echoNative");
            }

            if (evidence.WallHidden && !evidence.ShouldShowInvisibleBlocksAndWalls)
            {
                decision.AddReason("wall:hiddenWithoutEchoView");
                return MapQuickAnnouncementVisibilityDecision.Hidden(
                    MapQuickAnnouncementVisibilityLayer.Wall,
                    "wall:hiddenWithoutEchoView");
            }

            if (evidence.WallHidden)
            {
                decision.AddReason("wall:echoView");
            }

            var visibleReason = ResolveWallVisibleReason(evidence);
            if (!string.IsNullOrWhiteSpace(visibleReason))
            {
                decision.AddReason(visibleReason);
                return MapQuickAnnouncementVisibilityDecision.Visible(
                    MapQuickAnnouncementVisibilityLayer.Wall,
                    visibleReason);
            }

            decision.AddReason("wall:noVisibleEvidence");
            return MapQuickAnnouncementVisibilityDecision.Hidden(
                MapQuickAnnouncementVisibilityLayer.Wall,
                "wall:noVisibleEvidence");
        }

        private static MapQuickAnnouncementLayerVisibility EvaluateLiquid(
            MapQuickAnnouncementVisibilityRequest request,
            TerrariaTileVisibilityEvidence evidence,
            MapQuickAnnouncementVisibilityDecision decision)
        {
            if (request.Tile == null || !request.Tile.HasLiquid)
            {
                return MapQuickAnnouncementVisibilityDecision.Hidden(
                    MapQuickAnnouncementVisibilityLayer.Liquid,
                    "liquid:notPresent");
            }

            if (!evidence.ReadSucceeded)
            {
                return MapQuickAnnouncementVisibilityDecision.Unavailable(
                    MapQuickAnnouncementVisibilityLayer.Liquid,
                    evidence.FailureReason);
            }

            if (evidence.TileHidden && !evidence.ShouldShowInvisibleBlocksAndWalls)
            {
                decision.AddReason("liquid:hiddenTileWithoutEchoView");
                return MapQuickAnnouncementVisibilityDecision.Hidden(
                    MapQuickAnnouncementVisibilityLayer.Liquid,
                    "liquid:hiddenTileWithoutEchoView");
            }

            if (evidence.TileHidden)
            {
                decision.AddReason("liquid:echoView");
            }

            if (evidence.HasVisibleLight)
            {
                decision.AddReason("liquid:lighting");
                return MapQuickAnnouncementVisibilityDecision.Visible(
                    MapQuickAnnouncementVisibilityLayer.Liquid,
                    "liquid:lighting");
            }

            if (evidence.LiquidSelfVisible)
            {
                decision.AddReason("liquid:selfVisible");
                return MapQuickAnnouncementVisibilityDecision.Visible(
                    MapQuickAnnouncementVisibilityLayer.Liquid,
                    "liquid:selfVisible");
            }

            decision.AddReason("liquid:noVisibleEvidence");
            return MapQuickAnnouncementVisibilityDecision.Hidden(
                MapQuickAnnouncementVisibilityLayer.Liquid,
                "liquid:noVisibleEvidence");
        }

        private static MapQuickAnnouncementLayerVisibility EvaluateCircuit(
            MapQuickAnnouncementVisibilityRequest request,
            MapQuickAnnouncementVisibilityDecision decision)
        {
            if (request.Tile == null ||
                !(request.Tile.RedWire ||
                  request.Tile.BlueWire ||
                  request.Tile.GreenWire ||
                  request.Tile.YellowWire ||
                  request.Tile.Actuator))
            {
                return MapQuickAnnouncementVisibilityDecision.Hidden(
                    MapQuickAnnouncementVisibilityLayer.Circuit,
                    "circuit:notPresent");
            }

            // Circuit-only is intentionally separate from tile visibility so a
            // dark wire never carries the same cell's hidden tile/wall/liquid name.
            decision.AddReason("circuit:userException");
            return MapQuickAnnouncementVisibilityDecision.CircuitOnly("circuit:userException");
        }

        private static void EvaluateEmptyAir(
            MapQuickAnnouncementVisibilityRequest request,
            TerrariaTileVisibilityEvidence evidence,
            MapQuickAnnouncementVisibilityDecision decision)
        {
            if (request == null ||
                decision == null ||
                (request.Tile != null && request.Tile.HasAnyLayer) ||
                (request.Wall != null && request.Wall.Active))
            {
                return;
            }

            if (evidence != null && evidence.ReadSucceeded && evidence.HasVisibleLight)
            {
                decision.EmptyAirVisible = true;
                decision.EmptyAirReason = "air:lighting";
                decision.AddReason("air:lighting");
            }
        }

        private static string ResolveTileVisibleReason(TerrariaTileVisibilityEvidence evidence)
        {
            if (evidence.HasVisibleLight)
            {
                return "tile:lighting";
            }

            if (evidence.TileFullbright)
            {
                return "tile:fullbright";
            }

            if (evidence.DangerSenseHighlighted)
            {
                return "tile:dangerSense";
            }

            if (evidence.SpelunkerHighlighted)
            {
                return "tile:spelunker";
            }

            if (evidence.BiomeSightHighlighted)
            {
                return "tile:biomeSight";
            }

            if (evidence.TileHasGlowMask)
            {
                return "tile:glowMask";
            }

            if (evidence.TileHasFlame)
            {
                return "tile:flame";
            }

            if (evidence.TileIgnoresLightConditions)
            {
                return "tile:ignoreLight";
            }

            return string.Empty;
        }

        private static string ResolveWallVisibleReason(TerrariaTileVisibilityEvidence evidence)
        {
            if (evidence.HasVisibleLight)
            {
                return "wall:lighting";
            }

            if (evidence.WallFullbright)
            {
                return "wall:fullbright";
            }

            return string.Empty;
        }

        private static int ResolveTileType(MapQuickAnnouncementTileTarget tile)
        {
            return tile == null ? -1 : tile.TileType;
        }

        private static int ResolveTileStyle(MapQuickAnnouncementTileTarget tile)
        {
            return tile == null ? -1 : tile.TileStyle;
        }

        private static int ResolveFrameX(MapQuickAnnouncementTileTarget tile)
        {
            return tile == null ? 0 : tile.FrameX;
        }

        private static int ResolveFrameY(MapQuickAnnouncementTileTarget tile)
        {
            return tile == null ? 0 : tile.FrameY;
        }

        private static int ResolveWallType(MapQuickAnnouncementWallTarget wall)
        {
            return wall == null ? -1 : wall.WallType;
        }

        private static int ResolveLiquidType(MapQuickAnnouncementTileTarget tile)
        {
            return tile == null ? -1 : tile.LiquidType;
        }
    }
}
