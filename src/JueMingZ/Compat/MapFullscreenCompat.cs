using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class MapFullscreenCompat
    {
        internal const float DefaultJumpScale = 2.0f;
        internal const float MinJumpScale = 0.5f;
        internal const float MaxJumpScale = 16.0f;

        public static bool TryJumpToTile(int tileX, int tileY, out MapFullscreenJumpResult result)
        {
            result = new MapFullscreenJumpResult();

            try
            {
                if (Main.gameMenu || Main.maxTilesX <= 0 || Main.maxTilesY <= 0)
                {
                    result.ResultCode = "worldUnavailable";
                    result.Message = "fullscreen map jump failed because world dimensions are unavailable";
                    return false;
                }

                var target = BuildJumpTargetForTesting(tileX, tileY, Main.maxTilesX, Main.maxTilesY, DefaultJumpScale);
                if (!target.Succeeded)
                {
                    result = target;
                    return false;
                }

                // This compat is intentionally limited to fullscreen-map UI state.
                // Do not expand it into player teleporting, inventory use, buffs,
                // tile edits, or network writes.
                Main.mapFullscreen = true;
                Main.mapFullscreenPos = new Vector2(target.TileX, target.TileY);
                Main.mapFullscreenScale = target.Scale;

                result = target;
                result.ResultCode = "jumped";
                result.Message = "fullscreen map centered on custom marker";
                return true;
            }
            catch (Exception error)
            {
                result.ResultCode = "exception";
                result.Message = "fullscreen map jump failed: " + error.Message;
                return false;
            }
        }

        internal static MapFullscreenJumpResult BuildJumpTargetForTesting(
            int tileX,
            int tileY,
            int maxTilesX,
            int maxTilesY,
            float scale)
        {
            var result = new MapFullscreenJumpResult
            {
                Scale = ClampScale(scale)
            };

            if (maxTilesX <= 0 || maxTilesY <= 0)
            {
                result.ResultCode = "worldUnavailable";
                result.Message = "world dimensions are unavailable";
                return result;
            }

            result.Succeeded = true;
            result.TileX = Clamp(tileX, 0, maxTilesX - 1);
            result.TileY = Clamp(tileY, 0, maxTilesY - 1);
            result.WorldSizeX = maxTilesX;
            result.WorldSizeY = maxTilesY;
            result.ResultCode = "ok";
            result.Message = "ok";
            return result;
        }

        private static float ClampScale(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                return DefaultJumpScale;
            }

            if (scale < MinJumpScale)
            {
                return MinJumpScale;
            }

            return scale > MaxJumpScale ? MaxJumpScale : scale;
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

    internal sealed class MapFullscreenJumpResult
    {
        public bool Succeeded { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
        public float Scale { get; set; }

        public MapFullscreenJumpResult()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
        }
    }
}
