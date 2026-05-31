using System;
using System.Collections.Generic;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI;

namespace JueMingZ.Automation.WorldAutomation
{
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
                var activeLookup = new Dictionary<long, AutoMiningTile>();
                for (var index = 0; index < snapshot.Tiles.Count; index++)
                {
                    var tile = snapshot.Tiles[index];
                    if (tile == null ||
                        !AutoMiningCompat.IsActiveTileOfType(tiles, tile.X, tile.Y, snapshot.TileType))
                    {
                        continue;
                    }

                    activeLookup[((long)tile.X << 32) ^ (uint)tile.Y] = new AutoMiningTile(tile.X, tile.Y);
                }

                var pulse = 145 + (int)(Math.Abs(Math.Sin(context.GameUpdateCount / 10d)) * 90d);
                for (var index = 0; index < snapshot.Tiles.Count; index++)
                {
                    var tile = snapshot.Tiles[index];
                    if (tile == null ||
                        !AutoMiningCompat.IsActiveTileOfType(tiles, tile.X, tile.Y, snapshot.TileType))
                    {
                        continue;
                    }

                    var reachable = player != null &&
                                    AutoMiningCompat.CanMineTileWithPickaxe(
                                        player,
                                        tile.X,
                                        tile.Y,
                                        snapshot.TileType,
                                        snapshot.PickPower,
                                        snapshot.PickTileBoost) &&
                                    AutoMiningTargetSelector.IsImmediateFrontierTile(
                                        activeLookup,
                                        new AutoMiningTile(tile.X, tile.Y));
                    DrawTile(spriteBatch, context, tile.X, tile.Y, reachable, pulse);
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

        private static void DrawTile(object spriteBatch, InformationWorldContext context, int tileX, int tileY, bool reachable, int pulse)
        {
            var x = (int)Math.Round(tileX * AutoMiningCompat.TileSize - context.ScreenX);
            var y = (int)Math.Round(tileY * AutoMiningCompat.TileSize - context.ScreenY);
            var r = reachable ? 82 : 236;
            var g = reachable ? 238 : 82;
            var b = reachable ? 126 : 82;
            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, x + 1, y + 1, AutoMiningCompat.TileSize - 2, AutoMiningCompat.TileSize - 2, r, g, b, 55);
            UiPrimitiveRenderer.DrawRectBorder(spriteBatch, x, y, AutoMiningCompat.TileSize, AutoMiningCompat.TileSize, 1, r, g, b, Math.Min(245, pulse));
            UiPrimitiveRenderer.DrawRectBorder(spriteBatch, x + 2, y + 2, AutoMiningCompat.TileSize - 4, AutoMiningCompat.TileSize - 4, 1, 255, 255, 255, reachable ? 70 : 52);
        }
    }
}
