using System;
using System.Collections.Generic;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI;

namespace JueMingZ.Automation.WorldAutomation
{
    // Auto-mining overlay draws cached target state only; drawing must not select tiles or enqueue mining work.
    public static class AutoMiningOverlayService
    {
        public static void DrawWorldOverlay(object spriteBatch)
        {
            try
            {
                var snapshot = AutoMiningService.GetOverlaySnapshot();
                if (snapshot == null || snapshot.Tiles == null || snapshot.Tiles.Count <= 0)
                {
                    return;
                }

                InformationWorldContext context;
                string skip;
                if (!InformationWorldContextProvider.TryBuild(out context, out skip))
                {
                    return;
                }

                Type mainType;
                object tiles;
                int maxTilesX;
                int maxTilesY;
                string message;
                if (!AutoMiningCompat.TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
                {
                    return;
                }

                object player;
                AutoMiningCompat.TryGetLocalPlayer(out player, out message);
                var reachProfile = new AutoMiningCompat.MiningReachProfile();
                // Resolve vanilla reach once per overlay draw; per-tile reflection here is a frame-rate failure mode.
                var reachAvailable = player != null &&
                                     AutoMiningCompat.TryBuildMiningTakeoverReachProfile(player, snapshot.PickTileBoost, out reachProfile);
                var matchGroup = AutoMiningTileMatchGroup.ForSeedTileType(snapshot.TileType);
                var activeLookup = new Dictionary<long, AutoMiningTile>();
                for (var index = 0; index < snapshot.Tiles.Count; index++)
                {
                    var tile = snapshot.Tiles[index];
                    int actualType;
                    if (tile == null ||
                        !AutoMiningCompat.TryReadActiveTileMatchingGroup(tiles, tile.X, tile.Y, matchGroup, out actualType))
                    {
                        continue;
                    }

                    activeLookup[((long)tile.X << 32) ^ (uint)tile.Y] = new AutoMiningTile(tile.X, tile.Y, actualType);
                }

                for (var index = 0; index < snapshot.Tiles.Count; index++)
                {
                    var tile = snapshot.Tiles[index];
                    int actualType;
                    if (tile == null ||
                        !AutoMiningCompat.TryReadActiveTileMatchingGroup(tiles, tile.X, tile.Y, matchGroup, out actualType))
                    {
                        continue;
                    }

                    var frontier = AutoMiningTargetSelector.IsImmediateFrontierTile(
                        activeLookup,
                        tile.X,
                        tile.Y);
                    var reachable = reachAvailable &&
                                    frontier &&
                                    AutoMiningCompat.CanMineTileWithPickaxe(
                                        reachProfile,
                                        tile.X,
                                        tile.Y,
                                        actualType,
                                        snapshot.PickPower);
                    DrawTile(spriteBatch, context, tile.X, tile.Y, reachable);
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("AutoMiningOverlayService.DrawWorldOverlay", error);
                LogThrottle.ErrorThrottled(
                    "auto-mining-overlay-error",
                    TimeSpan.FromSeconds(10),
                    "AutoMiningOverlayService",
                    "Auto mining overlay draw failed; exception swallowed.", error);
            }
        }

        private static void DrawTile(object spriteBatch, InformationWorldContext context, int tileX, int tileY, bool reachable)
        {
            var x = (int)Math.Round(tileX * AutoMiningCompat.TileSize - context.ScreenX);
            var y = (int)Math.Round(tileY * AutoMiningCompat.TileSize - context.ScreenY);
            var style = ResolveTileStyle(reachable);
            var drawStyle = ResolveTileDrawStyle(style);

            // Green and red share the same reach predicate above; keep this as a single translucent fill
            // so selected clusters stay readable without per-tile grid borders or extra draw calls.
            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, x, y, AutoMiningCompat.TileSize, AutoMiningCompat.TileSize, drawStyle.R, drawStyle.G, drawStyle.B, drawStyle.FillAlpha);
        }

        private static AutoMiningOverlayTileStyle ResolveTileStyle(bool reachable)
        {
            // Contract: reachable/frontier tiles are green; selected-but-unreachable tiles are red.
            // AlphaBlend still blends against the already-lit world pixels, so keep the fill transparent but
            // high enough that cave darkness does not erase the marker. Visibility comes from hue, not borders.
            // BorderAlpha stays zero because per-tile borders turn dense veins into a grid.
            if (reachable)
            {
                return new AutoMiningOverlayTileStyle
                {
                    R = 150,
                    G = 216,
                    B = 138,
                    FillAlpha = 64,
                    BorderAlpha = 0
                };
            }

            return new AutoMiningOverlayTileStyle
            {
                R = 240,
                G = 160,
                B = 142,
                FillAlpha = 64,
                BorderAlpha = 0
            };
        }

        private static AutoMiningOverlayTileStyle ResolveTileDrawStyle(AutoMiningOverlayTileStyle style)
        {
            // Terraria's active interface SpriteBatch uses premultiplied AlphaBlend semantics:
            // lowering alpha alone keeps pale RGB too bright, while over-premultiplying makes dark caves swallow it.
            // Premultiply only this overlay's source RGB; do not change other UI rendering contracts here.
            return new AutoMiningOverlayTileStyle
            {
                R = PremultiplyForAlphaBlend(style.R, style.FillAlpha),
                G = PremultiplyForAlphaBlend(style.G, style.FillAlpha),
                B = PremultiplyForAlphaBlend(style.B, style.FillAlpha),
                FillAlpha = style.FillAlpha,
                BorderAlpha = style.BorderAlpha
            };
        }

        private static int PremultiplyForAlphaBlend(int component, int alpha)
        {
            var clampedComponent = ClampColorComponent(component);
            var clampedAlpha = ClampColorComponent(alpha);
            return (clampedComponent * clampedAlpha + 127) / 255;
        }

        private static int ClampColorComponent(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 255 ? 255 : value;
        }

        internal static AutoMiningOverlayTileStyle ResolveTileStyleForTesting(bool reachable)
        {
            return ResolveTileStyle(reachable);
        }

        internal static AutoMiningOverlayTileStyle ResolveTileDrawStyleForTesting(bool reachable)
        {
            return ResolveTileDrawStyle(ResolveTileStyle(reachable));
        }

        internal struct AutoMiningOverlayTileStyle
        {
            public int R;
            public int G;
            public int B;
            public int FillAlpha;
            public int BorderAlpha;
        }
    }
}
