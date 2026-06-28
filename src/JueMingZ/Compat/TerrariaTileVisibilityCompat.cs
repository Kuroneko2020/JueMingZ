using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ID;

namespace JueMingZ.Compat
{
    internal sealed class TerrariaTileVisibilityEvidence
    {
        public TerrariaTileVisibilityEvidence()
        {
            ReadSucceeded = true;
            FailureReason = string.Empty;
        }

        public bool ReadSucceeded { get; set; }
        public string FailureReason { get; set; }
        public bool LightingAvailable { get; set; }
        public byte LightR { get; set; }
        public byte LightG { get; set; }
        public byte LightB { get; set; }
        public bool EchoVisibilityAvailable { get; set; }
        public bool ShouldShowInvisibleBlocksAndWalls { get; set; }
        public bool TileHidden { get; set; }
        public bool WallHidden { get; set; }
        public bool TileFullbright { get; set; }
        public bool WallFullbright { get; set; }
        public bool DangerSenseHighlighted { get; set; }
        public bool SpelunkerHighlighted { get; set; }
        public bool BiomeSightHighlighted { get; set; }
        public bool TileHasGlowMask { get; set; }
        public bool TileHasFlame { get; set; }
        public bool TileIgnoresLightConditions { get; set; }
        public bool EchoNativeTile { get; set; }
        public bool EchoNativeWall { get; set; }
        public bool WallBlockedByFullTile { get; set; }
        public bool LiquidSelfVisible { get; set; }

        public bool HasVisibleLight
        {
            get { return LightingAvailable && (LightR > 0 || LightG > 0 || LightB > 0); }
        }

        public static TerrariaTileVisibilityEvidence Unavailable(string reason)
        {
            return new TerrariaTileVisibilityEvidence
            {
                ReadSucceeded = false,
                FailureReason = string.IsNullOrWhiteSpace(reason) ? "unavailable" : reason.Trim()
            };
        }
    }

    internal static class TerrariaTileVisibilityCompat
    {
        private static readonly object DangerousMethodSyncRoot = new object();
        private static bool _dangerousMethodResolved;
        private static MethodInfo _dangerousMethod;
        private static int _dangerousMethodResolveCount;

        public static bool TryReadVisibilityEvidence(
            int tileX,
            int tileY,
            Tile tile,
            int tileType,
            int tileStyle,
            int frameX,
            int frameY,
            int wallType,
            int liquidType,
            object perspectivePlayer,
            out TerrariaTileVisibilityEvidence evidence)
        {
            evidence = null;
            if (tile == null)
            {
                evidence = TerrariaTileVisibilityEvidence.Unavailable("tileMissing");
                return false;
            }

            try
            {
                var result = new TerrariaTileVisibilityEvidence();
                Color light;
                if (!TryReadLighting(tileX, tileY, out light))
                {
                    evidence = TerrariaTileVisibilityEvidence.Unavailable("lightingUnavailable");
                    return false;
                }

                result.LightingAvailable = true;
                result.LightR = light.R;
                result.LightG = light.G;
                result.LightB = light.B;

                bool showInvisible;
                if (!TryReadShowInvisible(out showInvisible))
                {
                    evidence = TerrariaTileVisibilityEvidence.Unavailable("echoVisibilityUnavailable");
                    return false;
                }

                result.EchoVisibilityAvailable = true;
                result.ShouldShowInvisibleBlocksAndWalls = showInvisible;

                result.TileHidden = tile.invisibleBlock();
                result.WallHidden = tile.invisibleWall();
                result.TileFullbright = tile.fullbrightBlock();
                result.WallFullbright = tile.fullbrightWall();
                result.EchoNativeTile = IsEchoNativeTile(tileType, tileStyle);
                result.EchoNativeWall = IsEchoNativeWall(wallType);
                result.LiquidSelfVisible = liquidType == 1 || liquidType == 3;

                bool hasGlowMask;
                bool hasFlame;
                bool ignoresLight;
                if (!TryReadTileDrawSets(tileType, out hasGlowMask, out hasFlame, out ignoresLight))
                {
                    evidence = TerrariaTileVisibilityEvidence.Unavailable("tileDrawSetsUnavailable");
                    return false;
                }

                result.TileHasGlowMask = hasGlowMask;
                result.TileHasFlame = hasFlame;
                result.TileIgnoresLightConditions = ignoresLight;
                result.WallBlockedByFullTile = IsWallBlockedByFullTile(tile, showInvisible);

                var player = ResolvePerspectivePlayer(perspectivePlayer);
                bool dangerHighlighted;
                string dangerFailure;
                if (!TryReadDangerSenseHighlight(player, tile, tileType, out dangerHighlighted, out dangerFailure))
                {
                    evidence = TerrariaTileVisibilityEvidence.Unavailable(dangerFailure);
                    return false;
                }

                result.DangerSenseHighlighted = dangerHighlighted;
                result.SpelunkerHighlighted = player != null &&
                                              player.findTreasure &&
                                              Main.IsTileSpelunkable((ushort)Math.Max(0, tileType), (short)frameX, (short)frameY);
                if (player != null && player.biomeSight)
                {
                    var biomeColor = Color.White;
                    result.BiomeSightHighlighted = Main.IsTileBiomeSightable(
                        (ushort)Math.Max(0, tileType),
                        (short)frameX,
                        (short)frameY,
                        ref biomeColor);
                }

                evidence = result;
                return true;
            }
            catch (Exception error)
            {
                evidence = TerrariaTileVisibilityEvidence.Unavailable(error.GetType().Name);
                return false;
            }
        }

        internal static bool IsEchoNativeTileForTesting(int tileType, int tileStyle)
        {
            return IsEchoNativeTile(tileType, tileStyle);
        }

        internal static bool IsEchoNativeWallForTesting(int wallType)
        {
            return IsEchoNativeWall(wallType);
        }

        internal static bool IsWallBlockedByFullTileForProjection(Tile tile)
        {
            bool showInvisible;
            return TryReadShowInvisible(out showInvisible) &&
                   IsWallBlockedByFullTile(tile, showInvisible);
        }

        internal static void ResetDangerousPredicateCacheForTesting()
        {
            lock (DangerousMethodSyncRoot)
            {
                _dangerousMethodResolved = false;
                _dangerousMethod = null;
                _dangerousMethodResolveCount = 0;
            }
        }

        internal static int DangerousPredicateResolveCountForTesting
        {
            get
            {
                lock (DangerousMethodSyncRoot)
                {
                    return _dangerousMethodResolveCount;
                }
            }
        }

        internal static bool TryResolveDangerousPredicateForTesting()
        {
            MethodInfo method;
            return TryResolveDangerousTileMethod(out method);
        }

        internal static bool TryReadDangerSenseHighlightForTesting(
            Player player,
            Tile tile,
            int tileType,
            out bool highlighted,
            out string failureReason)
        {
            return TryReadDangerSenseHighlight(player, tile, tileType, out highlighted, out failureReason);
        }

        private static bool TryReadLighting(int tileX, int tileY, out Color light)
        {
            light = Color.Black;
            try
            {
                light = Lighting.GetColor(tileX, tileY);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadShowInvisible(out bool showInvisible)
        {
            showInvisible = false;
            try
            {
                showInvisible = Main.ShouldShowInvisibleBlocksAndWalls();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadTileDrawSets(
            int tileType,
            out bool hasGlowMask,
            out bool hasFlame,
            out bool ignoresLight)
        {
            hasGlowMask = false;
            hasFlame = false;
            ignoresLight = false;

            if (tileType < 0)
            {
                return true;
            }

            short glowMask;
            if (!TryGetShort(Main.tileGlowMask, tileType, out glowMask))
            {
                return false;
            }

            bool flame;
            if (!TryGetBool(Main.tileFlame, tileType, out flame))
            {
                return false;
            }

            bool ignore;
            if (!TryGetBool(TileID.Sets.IgnoreDrawLightConditions, tileType, out ignore))
            {
                return false;
            }

            hasGlowMask = glowMask != -1;
            hasFlame = flame;
            ignoresLight = ignore;
            return true;
        }

        private static bool TryReadDangerSenseHighlight(
            Player player,
            Tile tile,
            int tileType,
            out bool highlighted,
            out string failureReason)
        {
            highlighted = false;
            failureReason = string.Empty;
            if (player == null || !player.dangerSense || tile == null || tileType < 0)
            {
                return true;
            }

            MethodInfo method;
            if (!TryResolveDangerousTileMethod(out method) || method == null)
            {
                failureReason = "dangerousPredicateUnavailable";
                return false;
            }

            try
            {
                var raw = method.Invoke(null, new object[] { player, tile, (ushort)tileType });
                highlighted = raw is bool && (bool)raw;
                return true;
            }
            catch (Exception error)
            {
                failureReason = "dangerousPredicateInvokeFailed:" + error.GetType().Name;
                highlighted = false;
                return false;
            }
        }

        private static bool TryResolveDangerousTileMethod(out MethodInfo method)
        {
            lock (DangerousMethodSyncRoot)
            {
                if (_dangerousMethodResolved)
                {
                    method = _dangerousMethod;
                    return method != null;
                }

                _dangerousMethodResolved = true;
                _dangerousMethodResolveCount++;
                try
                {
                    _dangerousMethod = typeof(TileDrawing).GetMethod(
                        "IsTileDangerous",
                        BindingFlags.NonPublic | BindingFlags.Static,
                        null,
                        new[] { typeof(Player), typeof(Tile), typeof(ushort) },
                        null);
                }
                catch
                {
                    _dangerousMethod = null;
                }

                method = _dangerousMethod;
                return method != null;
            }
        }

        private static Player ResolvePerspectivePlayer(object perspectivePlayer)
        {
            var typed = perspectivePlayer as Player;
            if (typed != null)
            {
                return typed;
            }

            try
            {
                if (Main.SceneMetrics != null && Main.SceneMetrics.PerspectivePlayer != null)
                {
                    return Main.SceneMetrics.PerspectivePlayer;
                }
            }
            catch
            {
            }

            try
            {
                return Main.LocalPlayer;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsEchoNativeTile(int tileType, int tileStyle)
        {
            switch (tileType)
            {
                case TileID.EchoBlock:
                case TileID.StinkbugHousingBlockerEcho:
                case TileID.LargePilesEcho:
                case TileID.LargePiles2Echo:
                case TileID.SmallPiles2x1Echo:
                case TileID.SmallPiles1x1Echo:
                case TileID.PlantDetritus3x2Echo:
                case TileID.PlantDetritus2x2Echo:
                case TileID.PotsEcho:
                case TileID.EchoMonolith:
                case TileID.Stalactite1x1Echo:
                case TileID.Stalactite1x2Echo:
                case TileID.JunglePlantsEcho:
                case TileID.FallenLogEcho:
                case TileID.OasisPlantsEcho:
                case TileID.TerragrimShrineEcho:
                case TileID.BooksEcho:
                    return true;
                case TileID.Platforms:
                    return tileStyle == 48;
                default:
                    return false;
            }
        }

        private static bool IsEchoNativeWall(int wallType)
        {
            return (wallType >= 246 && wallType <= 311) ||
                   wallType == 314 ||
                   wallType == WallID.EchoWall;
        }

        private static bool IsWallBlockedByFullTile(Tile tile, bool showInvisible)
        {
            if (tile == null ||
                !tile.active() ||
                tile.blockType() != 0 ||
                tile.halfBrick() ||
                tile.slope() != 0 ||
                tile.inActive() ||
                (tile.invisibleBlock() && !showInvisible))
            {
                return false;
            }

            var type = tile.type;
            bool frameImportant;
            if (TryGetBool(Main.tileFrameImportant, type, out frameImportant) && frameImportant)
            {
                return false;
            }

            bool drawsWalls;
            if (TryGetBool(TileID.Sets.DrawsWalls, type, out drawsWalls) && drawsWalls)
            {
                return false;
            }

            bool solid;
            bool solidTop;
            return TryGetBool(Main.tileSolid, type, out solid) &&
                   TryGetBool(Main.tileSolidTop, type, out solidTop) &&
                   solid &&
                   !solidTop;
        }

        private static bool TryGetBool(bool[] values, int index, out bool value)
        {
            value = false;
            if (values == null || index < 0 || index >= values.Length)
            {
                return false;
            }

            value = values[index];
            return true;
        }

        private static bool TryGetShort(short[] values, int index, out short value)
        {
            value = 0;
            if (values == null || index < 0 || index >= values.Length)
            {
                return false;
            }

            value = values[index];
            return true;
        }
    }
}
