using System;
using JueMingZ.Compat;

namespace JueMingZ.UI.Legacy
{
    internal sealed class LegacyMainUiScaleSnapshot
    {
        public double UiScaleX { get; private set; }
        public double UiScaleY { get; private set; }
        public double UiTranslateX { get; private set; }
        public double UiTranslateY { get; private set; }
        public double EffectiveScaleX { get; private set; }
        public double EffectiveScaleY { get; private set; }
        public double DrawScaleX { get; private set; }
        public double DrawScaleY { get; private set; }
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }
        public bool Capped { get; private set; }

        private LegacyMainUiScaleSnapshot()
        {
        }

        public float DrawScaleXFloat
        {
            get { return (float)DrawScaleX; }
        }

        public float DrawScaleYFloat
        {
            get { return (float)DrawScaleY; }
        }

        public int ToBaseLogicalX(int uiLogicalX)
        {
            return ScaleCoordinate(uiLogicalX, DrawScaleX);
        }

        public int ToBaseLogicalY(int uiLogicalY)
        {
            return ScaleCoordinate(uiLogicalY, DrawScaleY);
        }

        public int ScreenToBaseLogicalX(int screenX)
        {
            return ScreenToBaseLogical(screenX, UiScaleX, UiTranslateX, DrawScaleX);
        }

        public int ScreenToBaseLogicalY(int screenY)
        {
            return ScreenToBaseLogical(screenY, UiScaleY, UiTranslateY, DrawScaleY);
        }

        public bool ContainsScreenPoint(LegacyUiRect logicalRect, int screenX, int screenY)
        {
            if (screenX < 0 || screenY < 0)
            {
                return false;
            }

            var left = logicalRect.X * EffectiveScaleX + UiTranslateX;
            var top = logicalRect.Y * EffectiveScaleY + UiTranslateY;
            var right = logicalRect.Right * EffectiveScaleX + UiTranslateX;
            var bottom = logicalRect.Bottom * EffectiveScaleY + UiTranslateY;
            return screenX >= Math.Min(left, right) &&
                   screenY >= Math.Min(top, bottom) &&
                   screenX < Math.Max(left, right) &&
                   screenY < Math.Max(top, bottom);
        }

        private static int ScaleCoordinate(int value, double drawScale)
        {
            if (drawScale <= 0.01d)
            {
                return value;
            }

            return (int)Math.Round(value / drawScale);
        }

        private static int ScreenToBaseLogical(int screenValue, double uiScale, double translate, double drawScale)
        {
            if (screenValue < 0)
            {
                return screenValue;
            }

            if (uiScale <= 0.01d)
            {
                uiScale = 1d;
            }

            if (drawScale <= 0.01d)
            {
                drawScale = 1d;
            }

            return (int)Math.Round(((screenValue - translate) / uiScale) / drawScale);
        }

        internal static LegacyMainUiScaleSnapshot Create(
            double uiScaleX,
            double uiScaleY,
            double uiTranslateX,
            double uiTranslateY,
            int screenWidth,
            int screenHeight)
        {
            uiScaleX = NormalizeScale(uiScaleX);
            uiScaleY = NormalizeScale(uiScaleY);

            var currentScale = Math.Min(uiScaleX, uiScaleY);
            var maxEffectiveScale = ResolveMaxEffectiveScale(screenWidth, screenHeight);
            var targetScale = Math.Min(currentScale, maxEffectiveScale);
            targetScale = Math.Max(LegacyUiMetrics.MinimumEffectiveScale, targetScale);

            var drawScaleX = Math.Min(1d, targetScale / uiScaleX);
            var drawScaleY = Math.Min(1d, targetScale / uiScaleY);
            return new LegacyMainUiScaleSnapshot
            {
                UiScaleX = uiScaleX,
                UiScaleY = uiScaleY,
                UiTranslateX = uiTranslateX,
                UiTranslateY = uiTranslateY,
                EffectiveScaleX = uiScaleX * drawScaleX,
                EffectiveScaleY = uiScaleY * drawScaleY,
                DrawScaleX = drawScaleX,
                DrawScaleY = drawScaleY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                Capped = drawScaleX < 0.999d || drawScaleY < 0.999d
            };
        }

        private static double ResolveMaxEffectiveScale(int screenWidth, int screenHeight)
        {
            var maxScale = 1d;
            if (screenWidth > 0)
            {
                maxScale = Math.Min(maxScale, ResolveScreenFitScale(screenWidth, LegacyUiMetrics.MaxVisualWidth));
            }

            if (screenHeight > 0)
            {
                maxScale = Math.Min(maxScale, ResolveScreenFitScale(screenHeight, LegacyUiMetrics.MaxVisualHeight));
            }

            return Math.Max(LegacyUiMetrics.MinimumEffectiveScale, maxScale);
        }

        private static double ResolveScreenFitScale(int screenSize, int maxVisualSize)
        {
            if (screenSize <= 0 || maxVisualSize <= 0)
            {
                return 1d;
            }

            var safeSize = Math.Max(1, screenSize - LegacyUiMetrics.VisualScreenMargin);
            return safeSize / (double)maxVisualSize;
        }

        private static double NormalizeScale(double scale)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0.01d)
            {
                return 1d;
            }

            return scale;
        }
    }

    internal static class LegacyMainUiScale
    {
        public static LegacyMainUiScaleSnapshot Resolve(DiagnosticMouseState raw)
        {
            var screenWidth = 1280;
            var screenHeight = 720;
            try
            {
                screenWidth = TerrariaMainCompat.ScreenWidth;
                screenHeight = TerrariaMainCompat.ScreenHeight;
            }
            catch
            {
            }

            return ResolveForScreen(raw, screenWidth, screenHeight);
        }

        internal static LegacyMainUiScaleSnapshot ResolveForTesting(
            double uiScale,
            int screenWidth,
            int screenHeight)
        {
            return LegacyMainUiScaleSnapshot.Create(uiScale, uiScale, 0d, 0d, screenWidth, screenHeight);
        }

        internal static LegacyMainUiScaleSnapshot ResolveForScreen(DiagnosticMouseState raw, int screenWidth, int screenHeight)
        {
            var uiScale = raw != null && raw.UiScale > 0.01d ? raw.UiScale : 1d;
            var uiScaleX = raw != null && raw.UiScaleX > 0.01d ? raw.UiScaleX : uiScale;
            var uiScaleY = raw != null && raw.UiScaleY > 0.01d ? raw.UiScaleY : uiScale;
            var translateX = raw == null ? 0d : raw.UiTranslateX;
            var translateY = raw == null ? 0d : raw.UiTranslateY;
            return LegacyMainUiScaleSnapshot.Create(uiScaleX, uiScaleY, translateX, translateY, screenWidth, screenHeight);
        }
    }
}
