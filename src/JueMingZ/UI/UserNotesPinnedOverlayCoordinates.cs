using System;
using JueMingZ.Compat;

namespace JueMingZ.UI
{
    internal struct UserNotesPinnedOverlayCoordinateContext
    {
        public int ScreenWidth;
        public int ScreenHeight;
        public double UiScaleX;
        public double UiScaleY;
        public double UiTranslateX;
        public double UiTranslateY;
        public bool UiScaleActive;
        public string CoordinateMode;

        public int ScreenToUiX(int value)
        {
            return ScreenToUiCoordinate(value, UiScaleX, UiTranslateX);
        }

        public int ScreenToUiY(int value)
        {
            return ScreenToUiCoordinate(value, UiScaleY, UiTranslateY);
        }

        public bool TryResolveOsClientPoint(DiagnosticMouseState raw, out int x, out int y)
        {
            x = -1;
            y = -1;
            if (raw == null || !raw.OsReadAvailable || raw.OsClientMouseX < 0 || raw.OsClientMouseY < 0)
            {
                return false;
            }

            x = raw.OsClientMouseX;
            y = raw.OsClientMouseY;
            return true;
        }

        private static int ScreenToUiCoordinate(int value, double scale, double translate)
        {
            if (value < 0)
            {
                return value;
            }

            if (scale <= 0.01d)
            {
                return value;
            }

            return (int)Math.Round((value - translate) / scale);
        }
    }

    internal static class UserNotesPinnedOverlayCoordinates
    {
        private const string ScreenCoordinateMode = "ScreenUnscaled";

        public static UserNotesPinnedOverlayCoordinateContext ResolveCurrentScreenContext()
        {
            return ResolveScreenContext(DiagnosticMouseStateReader.Read(), SafeScreenWidth(), SafeScreenHeight());
        }

        public static UserNotesPinnedOverlayCoordinateContext ResolveScreenContext(DiagnosticMouseState raw)
        {
            return ResolveScreenContext(raw, SafeScreenWidth(), SafeScreenHeight());
        }

        public static UserNotesPinnedOverlayCoordinateContext ResolveScreenContext(
            DiagnosticMouseState raw,
            int screenWidth,
            int screenHeight)
        {
            raw = raw ?? new DiagnosticMouseState();
            double scaleX;
            double scaleY;
            var scaleActive = TryResolveUiScaleTransform(raw, out scaleX, out scaleY);
            return new UserNotesPinnedOverlayCoordinateContext
            {
                ScreenWidth = Math.Max(1, screenWidth),
                ScreenHeight = Math.Max(1, screenHeight),
                UiScaleX = scaleX,
                UiScaleY = scaleY,
                UiTranslateX = raw.UiTranslateX,
                UiTranslateY = raw.UiTranslateY,
                UiScaleActive = scaleActive,
                CoordinateMode = ScreenCoordinateMode
            };
        }

        public static int ScreenToUiCoordinate(int value, double scale, double translate)
        {
            if (value < 0)
            {
                return value;
            }

            if (scale <= 0.01d)
            {
                return value;
            }

            return (int)Math.Round((value - translate) / scale);
        }

        public static bool TryResolveUiScaleTransform(DiagnosticMouseState raw, out double scaleX, out double scaleY)
        {
            scaleX = raw != null && raw.UiScaleX > 0.01d ? raw.UiScaleX : raw == null ? 1d : raw.UiScale;
            scaleY = raw != null && raw.UiScaleY > 0.01d ? raw.UiScaleY : raw == null ? 1d : raw.UiScale;
            if (scaleX <= 0.01d)
            {
                scaleX = 1d;
            }

            if (scaleY <= 0.01d)
            {
                scaleY = 1d;
            }

            return raw != null &&
                   raw.UiScaleAvailable &&
                   (Math.Abs(scaleX - 1d) > 0.01d ||
                    Math.Abs(scaleY - 1d) > 0.01d ||
                    Math.Abs(raw.UiTranslateX) > 0.01d ||
                    Math.Abs(raw.UiTranslateY) > 0.01d);
        }

        private static int SafeScreenWidth()
        {
            try
            {
                return TerrariaMainCompat.ScreenWidth;
            }
            catch
            {
                return 1280;
            }
        }

        private static int SafeScreenHeight()
        {
            try
            {
                return TerrariaMainCompat.ScreenHeight;
            }
            catch
            {
                return 720;
            }
        }
    }
}
