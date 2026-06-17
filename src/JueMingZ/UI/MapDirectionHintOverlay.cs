using System;
using JueMingZ.Automation.MapEnhancement;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class MapDirectionHintOverlay
    {
        private const float TravellingMerchantTextScale = 0.86f;
        private const float RareCreatureArrowScale = 1.12f;
        private const float RareCreatureLabelScale = 0.78f;
        private const int ScreenPadding = 8;
        private const int LineGap = 2;
        private const int RareCreatureArrowBoxSize = 28;
        private const int RareCreatureLabelGap = 14;

        public static bool DrawInterfaceLayer()
        {
            try
            {
                var snapshot = MapDirectionHintTargetService.GetSnapshot();
                var travellingTarget = snapshot == null ? null : snapshot.TravellingMerchantTarget;
                var rareTarget = snapshot == null ? null : snapshot.RareCreatureTarget;
                var hasTravellingTarget = travellingTarget != null && travellingTarget.Enabled && travellingTarget.Active;
                var hasRareTarget = rareTarget != null && rareTarget.Enabled && rareTarget.Active;
                if (!hasTravellingTarget && !hasRareTarget)
                {
                    MapDirectionHintDiagnostics.RecordTravellingMerchantProjection(
                        MapTravellingMerchantDirectionProjection.Empty(travellingTarget == null ? "targetUnavailable" : travellingTarget.Status),
                        travellingTarget == null ? "targetUnavailable" : travellingTarget.Status);
                    MapDirectionHintDiagnostics.RecordRareCreatureProjection(
                        MapRareCreatureDirectionProjection.Empty(rareTarget == null ? "targetUnavailable" : rareTarget.Status),
                        rareTarget == null ? "targetUnavailable" : rareTarget.Status);
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("MapDirectionHintOverlay.TravellingMerchant", true, out spriteBatch))
                {
                    return true;
                }

                DrawTravellingMerchant(spriteBatch, travellingTarget);
                DrawRareCreature(spriteBatch, rareTarget);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("MapDirectionHintOverlay.TravellingMerchant", error);
                LogThrottle.ErrorThrottled(
                    "map-direction-hint-overlay-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "MapDirectionHintOverlay",
                    "Map direction hint overlay draw failed; exception swallowed.", error);
            }

            return true;
        }

        private static void DrawTravellingMerchant(object spriteBatch, MapTravellingMerchantDirectionTarget target)
        {
            if (target == null || !target.Enabled || !target.Active)
            {
                MapDirectionHintDiagnostics.RecordTravellingMerchantProjection(
                    MapTravellingMerchantDirectionProjection.Empty(target == null ? "targetUnavailable" : target.Status),
                    target == null ? "targetUnavailable" : target.Status);
                return;
            }

            MapDirectionHintScreenContext screen;
            if (!TryReadScreenContext(out screen))
            {
                MapDirectionHintDiagnostics.RecordTravellingMerchantProjection(
                    MapTravellingMerchantDirectionProjection.Empty("screenUnavailable"),
                    "screenUnavailable");
                return;
            }

            MapTravellingMerchantDirectionProjection projection;
            if (!MapTravellingMerchantDirectionTargetResolver.TryBuildProjectionForTesting(target, screen, out projection))
            {
                MapDirectionHintDiagnostics.RecordTravellingMerchantProjection(projection, projection.Status);
                return;
            }

            if (projection.OnScreen || !projection.ShouldDraw)
            {
                MapDirectionHintDiagnostics.RecordTravellingMerchantProjection(projection, projection.Status);
                return;
            }

            DrawTravellingMerchantLabel(spriteBatch, projection, screen.ScreenWidth, screen.ScreenHeight);
            MapDirectionHintDiagnostics.RecordTravellingMerchantProjection(projection, "drawn");
        }

        private static void DrawRareCreature(object spriteBatch, MapRareCreatureDirectionTarget target)
        {
            if (target == null || !target.Enabled || !target.Active)
            {
                MapDirectionHintDiagnostics.RecordRareCreatureProjection(
                    MapRareCreatureDirectionProjection.Empty(target == null ? "targetUnavailable" : target.Status),
                    target == null ? "targetUnavailable" : target.Status);
                return;
            }

            MapDirectionHintScreenContext screen;
            if (!TryReadScreenContext(out screen))
            {
                MapDirectionHintDiagnostics.RecordRareCreatureProjection(
                    MapRareCreatureDirectionProjection.Empty("screenUnavailable"),
                    "screenUnavailable");
                return;
            }

            MapRareCreatureDirectionProjection projection;
            if (!MapRareCreatureDirectionTargetResolver.TryBuildProjectionForTesting(target, screen, out projection) ||
                !projection.ShouldDraw)
            {
                MapDirectionHintDiagnostics.RecordRareCreatureProjection(projection, projection.Status);
                return;
            }

            DrawRareCreatureArrow(spriteBatch, projection);
            if (projection.ShouldDrawLabel)
            {
                DrawRareCreatureLabel(spriteBatch, projection, screen.ScreenWidth, screen.ScreenHeight);
                MapDirectionHintDiagnostics.RecordRareCreatureProjection(projection, "drawnWithLabel");
                return;
            }

            MapDirectionHintDiagnostics.RecordRareCreatureProjection(projection, "drawnArrowOnly");
        }

        internal static MapTravellingMerchantLabelLayout BuildTravellingMerchantLabelLayoutForTesting(
            MapTravellingMerchantDirectionProjection projection,
            int screenWidth,
            int screenHeight)
        {
            projection = projection ?? MapTravellingMerchantDirectionProjection.Empty("targetUnavailable");
            var line1 = string.IsNullOrWhiteSpace(projection.LabelLine1) ? "旅商" : projection.LabelLine1;
            var line2 = projection.LabelLine2 ?? string.Empty;
            var line3 = string.IsNullOrWhiteSpace(projection.LabelLine3) ? "环境未知" : projection.LabelLine3;
            var lineHeight = Math.Max(1, UiTextRenderer.EstimateTextHeight(TravellingMerchantTextScale));
            var width = Math.Max(
                UiTextRenderer.EstimateTextWidth(line1, TravellingMerchantTextScale),
                Math.Max(
                    UiTextRenderer.EstimateTextWidth(line2, TravellingMerchantTextScale),
                    UiTextRenderer.EstimateTextWidth(line3, TravellingMerchantTextScale)));
            var height = lineHeight * 3 + LineGap * 2;
            var safeScreenWidth = Math.Max(1, screenWidth);
            var safeScreenHeight = Math.Max(1, screenHeight);
            var x = Clamp((int)Math.Round(projection.EdgeX - width * 0.5f), ScreenPadding, Math.Max(ScreenPadding, safeScreenWidth - width - ScreenPadding));
            var y = Clamp((int)Math.Round(projection.EdgeY - height * 0.5f), ScreenPadding, Math.Max(ScreenPadding, safeScreenHeight - height - ScreenPadding));

            return new MapTravellingMerchantLabelLayout
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                LineHeight = lineHeight,
                LineGap = LineGap,
                Line1 = line1,
                Line2 = line2,
                Line3 = line3
            };
        }

        internal static MapRareCreatureLabelLayout BuildRareCreatureLabelLayoutForTesting(
            MapRareCreatureDirectionProjection projection,
            int screenWidth,
            int screenHeight)
        {
            projection = projection ?? MapRareCreatureDirectionProjection.Empty("targetUnavailable");
            var line1 = string.IsNullOrWhiteSpace(projection.LabelLine1) ? "稀有生物" : projection.LabelLine1;
            var line2 = projection.LabelLine2 ?? string.Empty;
            var lineHeight = Math.Max(1, UiTextRenderer.EstimateTextHeight(RareCreatureLabelScale));
            var width = Math.Max(
                UiTextRenderer.EstimateTextWidth(line1, RareCreatureLabelScale),
                UiTextRenderer.EstimateTextWidth(line2, RareCreatureLabelScale));
            var height = lineHeight * 2 + LineGap;
            var safeScreenWidth = Math.Max(1, screenWidth);
            var safeScreenHeight = Math.Max(1, screenHeight);
            var x = projection.DirectionX >= 0f
                ? (int)Math.Round(projection.ArrowX + RareCreatureLabelGap)
                : (int)Math.Round(projection.ArrowX - width - RareCreatureLabelGap);
            var y = (int)Math.Round(projection.ArrowY - height * 0.5f);

            x = Clamp(x, ScreenPadding, Math.Max(ScreenPadding, safeScreenWidth - width - ScreenPadding));
            y = Clamp(y, ScreenPadding, Math.Max(ScreenPadding, safeScreenHeight - height - ScreenPadding));

            return new MapRareCreatureLabelLayout
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                LineHeight = lineHeight,
                LineGap = LineGap,
                Line1 = line1,
                Line2 = line2
            };
        }

        private static void DrawTravellingMerchantLabel(
            object spriteBatch,
            MapTravellingMerchantDirectionProjection projection,
            int screenWidth,
            int screenHeight)
        {
            var layout = BuildTravellingMerchantLabelLayoutForTesting(projection, screenWidth, screenHeight);
            DrawCenteredGoldLine(spriteBatch, layout.Line1, layout.X, layout.Y, layout.Width, layout.LineHeight);
            DrawCenteredGoldLine(spriteBatch, layout.Line2, layout.X, layout.Y + layout.LineHeight + layout.LineGap, layout.Width, layout.LineHeight);
            DrawCenteredGoldLine(spriteBatch, layout.Line3, layout.X, layout.Y + (layout.LineHeight + layout.LineGap) * 2, layout.Width, layout.LineHeight);
        }

        private static void DrawRareCreatureArrow(object spriteBatch, MapRareCreatureDirectionProjection projection)
        {
            var glyph = string.IsNullOrWhiteSpace(projection.ArrowGlyph) ? "→" : projection.ArrowGlyph;
            var x = (int)Math.Round(projection.ArrowX - RareCreatureArrowBoxSize * 0.5f);
            var y = (int)Math.Round(projection.ArrowY - RareCreatureArrowBoxSize * 0.5f);
            UiTextRenderer.DrawAlignedText(spriteBatch, glyph, x + 2, y + 2, RareCreatureArrowBoxSize, RareCreatureArrowBoxSize, UiTextHorizontalAlignment.Center, 12, 14, 20, 210, RareCreatureArrowScale);
            UiTextRenderer.DrawAlignedText(spriteBatch, glyph, x, y, RareCreatureArrowBoxSize, RareCreatureArrowBoxSize, UiTextHorizontalAlignment.Center, 255, 224, 96, 245, RareCreatureArrowScale);
        }

        private static void DrawRareCreatureLabel(
            object spriteBatch,
            MapRareCreatureDirectionProjection projection,
            int screenWidth,
            int screenHeight)
        {
            var layout = BuildRareCreatureLabelLayoutForTesting(projection, screenWidth, screenHeight);
            DrawCenteredGoldLine(spriteBatch, layout.Line1, layout.X, layout.Y, layout.Width, layout.LineHeight, RareCreatureLabelScale);
            DrawCenteredGoldLine(spriteBatch, layout.Line2, layout.X, layout.Y + layout.LineHeight + layout.LineGap, layout.Width, layout.LineHeight, RareCreatureLabelScale);
        }

        private static void DrawCenteredGoldLine(object spriteBatch, string text, int x, int y, int width, int height)
        {
            DrawCenteredGoldLine(spriteBatch, text, x, y, width, height, TravellingMerchantTextScale);
        }

        private static void DrawCenteredGoldLine(object spriteBatch, string text, int x, int y, int width, int height, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            UiTextRenderer.DrawAlignedText(spriteBatch, text, x + 2, y + 2, width, height, UiTextHorizontalAlignment.Center, 12, 14, 20, 210, scale);
            UiTextRenderer.DrawAlignedText(spriteBatch, text, x, y, width, height, UiTextHorizontalAlignment.Center, 255, 224, 96, 240, scale);
        }

        private static bool TryReadScreenContext(out MapDirectionHintScreenContext screen)
        {
            screen = null;
            try
            {
                Terraria.Player player;
                if (!TerrariaMainCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    return false;
                }

                var playerCenter = TerrariaPlayerReadCompat.Center(player);
                var screenPosition = TerrariaMainCompat.ScreenPosition;
                screen = new MapDirectionHintScreenContext
                {
                    ScreenX = screenPosition.X,
                    ScreenY = screenPosition.Y,
                    ScreenWidth = TerrariaMainCompat.ScreenWidth,
                    ScreenHeight = TerrariaMainCompat.ScreenHeight,
                    PlayerCenterX = playerCenter.X,
                    PlayerCenterY = playerCenter.Y
                };

                return screen.IsValid;
            }
            catch
            {
                return false;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                max = min;
            }

            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }

    internal struct MapTravellingMerchantLabelLayout
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int LineHeight { get; set; }
        public int LineGap { get; set; }
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public string Line3 { get; set; }
    }

    internal struct MapRareCreatureLabelLayout
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int LineHeight { get; set; }
        public int LineGap { get; set; }
        public string Line1 { get; set; }
        public string Line2 { get; set; }
    }
}
