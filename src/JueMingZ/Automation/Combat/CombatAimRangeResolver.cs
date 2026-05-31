using System;
using JueMingZ.Config;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAimRangeResolver
    {
        public const string RangeModeCursorSlider = "CursorSlider";
        public const string RangeModePlayerScreen = "PlayerScreen";
        public const int PlayerScreenMarginTiles = 10;
        private const int MinPlayerScreenRadiusTiles = 50;
        private const int MaxPlayerScreenRadiusTiles = 140;

        public static CombatAimRangeResolveResult Resolve(
            AppSettings settings,
            CombatAimReadResult readResult,
            bool hasPlayerCenter,
            float playerCenterX,
            float playerCenterY)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var origin = CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin);
            var cursorRadius = Clamp(settings.CursorAimRadius, 0, 50);
            var playerRadius = Clamp(settings.PlayerAimRadius, 0, 50);
            var result = new CombatAimRangeResolveResult
            {
                AimRangeOrigin = origin,
                CursorAimRadius = cursorRadius,
                PlayerAimRadius = playerRadius,
                PlayerScreenMarginTiles = PlayerScreenMarginTiles
            };

            if (cursorRadius <= 0)
            {
                result.Enabled = false;
                result.RadiusTiles = 0;
                result.RangeMode = string.Equals(origin, CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase)
                    ? RangeModePlayerScreen
                    : RangeModeCursorSlider;
                result.DisabledReason = "radiusOff";
                if (readResult != null)
                {
                    result.RangeCenterWorldX = readResult.CursorWorldX;
                    result.RangeCenterWorldY = readResult.CursorWorldY;
                }

                return result;
            }

            if (string.Equals(origin, CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase))
            {
                result.RangeMode = RangeModePlayerScreen;
                if (!hasPlayerCenter)
                {
                    result.Enabled = false;
                    result.DisabledReason = "playerCenterUnavailable";
                    return result;
                }

                result.RangeCenterWorldX = playerCenterX;
                result.RangeCenterWorldY = playerCenterY;
                var screenRadius = ResolvePlayerScreenRadiusTiles(readResult, playerCenterX, playerCenterY);
                result.PlayerScreenRadiusTiles = screenRadius;
                result.RadiusTiles = screenRadius;
                result.RadiusPixels = screenRadius * 16f;
                result.Enabled = screenRadius > 0;
                return result;
            }

            result.RangeMode = RangeModeCursorSlider;
            result.RadiusTiles = cursorRadius;
            result.RadiusPixels = cursorRadius * 16f;
            result.Enabled = cursorRadius > 0;
            if (readResult != null)
            {
                result.RangeCenterWorldX = readResult.CursorWorldX;
                result.RangeCenterWorldY = readResult.CursorWorldY;
            }

            return result;
        }

        private static int ResolvePlayerScreenRadiusTiles(CombatAimReadResult readResult, float playerCenterX, float playerCenterY)
        {
            if (readResult == null || readResult.ScreenWidth <= 0 || readResult.ScreenHeight <= 0)
            {
                return MinPlayerScreenRadiusTiles;
            }

            var left = readResult.ScreenPositionX;
            var top = readResult.ScreenPositionY;
            var right = readResult.ScreenPositionX + readResult.ScreenWidth;
            var bottom = readResult.ScreenPositionY + readResult.ScreenHeight;
            var maxPixels = Math.Max(
                Math.Max(Distance(playerCenterX, playerCenterY, left, top), Distance(playerCenterX, playerCenterY, right, top)),
                Math.Max(Distance(playerCenterX, playerCenterY, left, bottom), Distance(playerCenterX, playerCenterY, right, bottom)));
            var tiles = (int)Math.Ceiling(maxPixels / 16f) + PlayerScreenMarginTiles;
            return Clamp(tiles, MinPlayerScreenRadiusTiles, MaxPlayerScreenRadiusTiles);
        }

        private static float Distance(float ax, float ay, float bx, float by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
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
}
