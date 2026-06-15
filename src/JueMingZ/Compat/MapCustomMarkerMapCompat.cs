using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class MapCustomMarkerMapCompat
    {
        private const int FullscreenMapMinTileX = 10;
        private const int FullscreenMapMinTileY = 10;

        public static bool TryReadFullscreenMapMouseTile(out MapCustomMarkerMapPoint point, out string message)
        {
            point = new MapCustomMarkerMapPoint();
            message = string.Empty;

            try
            {
                if (!Main.mapFullscreen)
                {
                    message = "fullscreen map is not open";
                    return false;
                }

                if (Main.gameMenu || Main.maxTilesX <= 0 || Main.maxTilesY <= 0 || Main.mapFullscreenScale <= 0f)
                {
                    message = "fullscreen map context unavailable";
                    return false;
                }

                point = ScreenToTile(
                    Main.mouseX,
                    Main.mouseY,
                    Main.mapFullscreenPos,
                    Main.mapFullscreenScale,
                    Math.Max(1, Main.screenWidth),
                    Math.Max(1, Main.screenHeight),
                    Main.maxTilesX,
                    Main.maxTilesY);
                message = "ok";
                return true;
            }
            catch (Exception error)
            {
                message = "fullscreen map mouse tile read failed: " + error.Message;
                return false;
            }
        }

        internal static MapCustomMarkerMapPoint ScreenToTile(
            int mouseX,
            int mouseY,
            Vector2 fullscreenPos,
            float scale,
            int screenWidth,
            int screenHeight,
            int maxTilesX,
            int maxTilesY)
        {
            if (scale <= 0f)
            {
                scale = 1f;
            }

            var minX = FullscreenMapMinTileX;
            var minY = FullscreenMapMinTileY;
            var offsetX = -fullscreenPos.X * scale + screenWidth / 2f + minX * scale;
            var offsetY = -fullscreenPos.Y * scale + screenHeight / 2f + minY * scale;
            var tileX = (int)Math.Floor((mouseX - offsetX) / scale + minX);
            var tileY = (int)Math.Floor((mouseY - offsetY) / scale + minY);

            return new MapCustomMarkerMapPoint
            {
                TileX = Clamp(tileX, 0, Math.Max(0, maxTilesX - 1)),
                TileY = Clamp(tileY, 0, Math.Max(0, maxTilesY - 1)),
                ScreenX = mouseX,
                ScreenY = mouseY,
                WorldSizeX = Math.Max(0, maxTilesX),
                WorldSizeY = Math.Max(0, maxTilesY)
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }

    internal sealed class MapCustomMarkerMapPoint
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
    }
}
