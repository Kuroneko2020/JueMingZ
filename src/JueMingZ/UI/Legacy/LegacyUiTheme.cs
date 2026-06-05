using System;
using JueMingZ.Compat;

namespace JueMingZ.UI.Legacy
{
    public static class LegacyUiTheme
    {
        public const int RadiusLarge = 10;
        public const int Radius = 8;
        public const int RadiusSmall = 6;
        public const int SelectedTextR = 255;
        public const int SelectedTextG = 226;
        public const int SelectedTextB = 150;

        public static void DrawPanel(object spriteBatch, LegacyUiRect rect)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, RadiusLarge, palette.BorderR, palette.BorderG, palette.BorderB, palette.BorderA);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4, RadiusLarge - 2, palette.PanelR, palette.PanelG, palette.PanelB, palette.PanelA);
            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, rect.X + 5, rect.Y + 5, rect.Width - 10, rect.Height - 10, palette.OverlayR, palette.OverlayG, palette.OverlayB, palette.OverlayA);
        }

        public static void DrawTitleBar(object spriteBatch, LegacyUiRect rect)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height + RadiusLarge, RadiusLarge, palette.HeaderR, palette.HeaderG, palette.HeaderB, palette.HeaderA);
            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, rect.X, rect.Bottom - 2, rect.Width, 2, palette.BorderR, palette.BorderG, palette.BorderB, palette.BorderA);
        }

        public static void DrawContentPanel(object spriteBatch, LegacyUiRect rect)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, Radius, palette.BorderR, palette.BorderG, palette.BorderB, palette.BorderA);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, Radius - 1, palette.ContentR, palette.ContentG, palette.ContentB, palette.ContentA);
        }

        public static void DrawSubPanelClipped(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall, palette.BorderR, palette.BorderG, palette.BorderB, 146);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall - 1, palette.RowR, palette.RowG, palette.RowB, palette.RowA);
        }

        public static void DrawButton(object spriteBatch, LegacyUiRect rect, bool hovered, bool pressed, bool selected, bool enabled)
        {
            DrawButtonClipped(spriteBatch, rect, hovered, pressed, selected, enabled, rect);
        }

        public static int GetSelectedButtonContentOffset(bool selected, bool enabled)
        {
            return selected && enabled ? 1 : 0;
        }

        public static LegacyUiRect GetSelectedButtonContentRect(LegacyUiRect rect, bool selected, bool enabled)
        {
            var offset = GetSelectedButtonContentOffset(selected, enabled);
            return new LegacyUiRect(rect.X, rect.Y + offset, rect.Width, Math.Max(1, rect.Height - offset));
        }

        public static void DrawButtonClipped(object spriteBatch, LegacyUiRect rect, bool hovered, bool pressed, bool selected, bool enabled, LegacyUiRect clip)
        {
            object texture;
            if (VanillaUiSkinCompat.TryGetButtonTexture(false, false, out texture))
            {
                var alpha = enabled ? (hovered ? 232 : 218) : 128;
                var tintR = hovered ? 246 : 255;
                var tintG = hovered ? 250 : 255;
                var tintB = 255;
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiledClipped(spriteBatch, texture, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, tintR, tintG, tintB, alpha);

                if (!enabled)
                {
                    UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall - 1, 0, 0, 0, 90);
                }

                if (selected && enabled)
                {
                    DrawSelectedButtonDepthClipped(spriteBatch, rect, clip);
                }

                if (pressed)
                {
                    UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall - 1, 0, 0, 0, 55);
                }

                return;
            }

            if (!enabled)
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall, 74, 82, 112, 170);
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall - 1, 30, 34, 48, 150);
                return;
            }

            var r = 48;
            var g = 64;
            var b = 104;
            var a = 210;
            if (hovered)
            {
                r += 10;
                g += 12;
                b += 12;
                a = 226;
            }

            if (pressed)
            {
                r -= 10;
                g -= 10;
                b -= 10;
            }

            var border = 1;
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall, 96, 124, 172, hovered ? 204 : 184);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + border, rect.Y + border, rect.Width - border * 2, rect.Height - border * 2, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall - 1, r, g, b, a);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall - 3, 12, 17, 34, 48);
            if (selected)
            {
                DrawSelectedButtonDepthClipped(spriteBatch, rect, clip);
            }
        }

        public static void DrawCell(object spriteBatch, LegacyUiRect rect, bool hovered, bool selected)
        {
            DrawCellClipped(spriteBatch, rect, hovered, selected, false, rect);
        }

        public static void DrawCellClipped(object spriteBatch, LegacyUiRect rect, bool hovered, bool selected, bool missing, LegacyUiRect clip)
        {
            object texture;
            var variant = 1;
            if (VanillaUiSkinCompat.TryGetInventoryBackTexture(variant, out texture))
            {
                var tintR = hovered ? 246 : 255;
                var tintG = hovered ? 250 : 255;
                var tintB = hovered ? 255 : 255;
                var alpha = hovered ? 226 : 238;
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiledClipped(spriteBatch, texture, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, tintR, tintG, tintB, alpha);
            }
            else
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall, 45, 58, 102, 218);
            }

            if (selected)
            {
                DrawSelectedTintClipped(spriteBatch, rect, clip, 24);
            }
        }

        public static void DrawSectionHeader(object spriteBatch, LegacyUiRect rect)
        {
            DrawSectionHeaderClipped(spriteBatch, rect, rect);
        }

        public static void DrawSectionHeaderClipped(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall, palette.BorderR, palette.BorderG, palette.BorderB, palette.BorderA);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall - 1, palette.HeaderR, palette.HeaderG, palette.HeaderB, palette.HeaderA);
        }

        public static void DrawRow(object spriteBatch, LegacyUiRect rect)
        {
            DrawRowClipped(spriteBatch, rect, rect);
        }

        public static void DrawRowClipped(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall, palette.BorderR, palette.BorderG, palette.BorderB, 138);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, RadiusSmall - 1, palette.RowR, palette.RowG, palette.RowB, palette.RowA);
        }

        public static void DrawScrollbar(object spriteBatch, LegacyUiRect track, LegacyUiRect thumb)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            var trackWidth = track.Width <= 8 ? track.Width : 8;
            var trackX = track.X + (track.Width - trackWidth) / 2;
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, trackX, track.Y, trackWidth, track.Height, trackWidth / 2, palette.BorderR, palette.BorderG, palette.BorderB, 150);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, trackX + 1, track.Y + 1, trackWidth - 2, track.Height - 2, Math.Max(1, (trackWidth - 2) / 2), palette.PanelR, palette.PanelG, palette.PanelB, 134);

            var thumbWidth = thumb.Width <= 8 ? thumb.Width : 8;
            var thumbX = thumb.X + (thumb.Width - thumbWidth) / 2;
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, thumbX, thumb.Y, thumbWidth, thumb.Height, thumbWidth / 2, palette.BorderR, palette.BorderG, palette.BorderB, 232);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, thumbX + 1, thumb.Y + 1, thumbWidth - 2, thumb.Height - 2, Math.Max(1, (thumbWidth - 2) / 2), palette.HeaderR, palette.HeaderG, palette.HeaderB, 226);
        }

        public static void DrawTooltip(object spriteBatch, LegacyUiRect rect)
        {
            object texture;
            if (VanillaUiSkinCompat.TryGetTextBackTexture(out texture) ||
                VanillaUiSkinCompat.TryGetSettingsPanel2Texture(out texture) ||
                VanillaUiSkinCompat.TryGetInventoryBackTexture(out texture))
            {
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiled(spriteBatch, texture, rect.X, rect.Y, rect.Width, rect.Height, 255, 255, 255, 238);
                UiPrimitiveRenderer.DrawFilledRect(spriteBatch, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4, 0, 0, 0, 88);
                return;
            }

            DrawFallbackPanel(spriteBatch, rect);
        }

        public static void DrawResizeGrip(object spriteBatch, LegacyUiRect rect)
        {
            if (!LegacyUiMetrics.AllowResize || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            var x = rect.Right - 4;
            var y = rect.Bottom - 4;
            for (var line = 0; line < 3; line++)
            {
                var length = 7 + line * 5;
                var offset = line * 5;
                DrawGripLine(spriteBatch, x - length, y - offset, length);
            }
        }

        private static void DrawGripLine(object spriteBatch, int x, int y, int length)
        {
            for (var step = 0; step < length; step++)
            {
                UiPrimitiveRenderer.DrawFilledRect(spriteBatch, x + step, y - step, 2, 2, 202, 216, 240, 185);
            }
        }

        private static void DrawHighlightClipped(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, int alpha)
        {
            if (rect.Width <= 8 || rect.Height <= 8)
            {
                return;
            }

            UiPrimitiveRenderer.DrawRoundedRectClipped(
                spriteBatch,
                rect.X + 1,
                rect.Y + 1,
                rect.Width - 2,
                rect.Height - 2,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                Math.Max(1, RadiusSmall - 1),
                220,
                232,
                238,
                alpha);
        }

        private static void DrawSelectedTintClipped(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, int alpha)
        {
            if (rect.Width <= 8 || rect.Height <= 8)
            {
                return;
            }

            UiPrimitiveRenderer.DrawRoundedRectClipped(
                spriteBatch,
                rect.X + 1,
                rect.Y + 1,
                rect.Width - 2,
                rect.Height - 2,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                Math.Max(1, RadiusSmall - 1),
                218,
                232,
                214,
                alpha);
        }

        public static void DrawSelectedTextMarkers(object spriteBatch, LegacyUiRect textRect, string text, float scale)
        {
            // Selected buttons use a pressed face and warm content color; no extra markers.
        }

        public static void DrawSelectedTextMarkersClipped(object spriteBatch, LegacyUiRect textRect, LegacyUiRect clip, string text, float scale)
        {
            // Selected buttons use a pressed face and warm content color; no extra markers.
        }

        public static void DrawSelectedContentMarkersClipped(object spriteBatch, LegacyUiRect contentRect, LegacyUiRect clip)
        {
            // Selected buttons use a pressed face and warm content color; no extra markers.
        }

        private static void DrawSelectedButtonDepthClipped(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            if (rect.Width <= 8 || rect.Height <= 8)
            {
                return;
            }

            UiPrimitiveRenderer.DrawRoundedRectClipped(
                spriteBatch,
                rect.X + 1,
                rect.Y + 1,
                rect.Width - 2,
                rect.Height - 2,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                Math.Max(1, RadiusSmall - 1),
                0,
                0,
                0,
                88);
        }

        private static void DrawFallbackPanel(object spriteBatch, LegacyUiRect rect)
        {
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, RadiusLarge, 89, 116, 171, 238);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4, RadiusLarge - 2, 33, 43, 79, 224);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X + 5, rect.Y + 5, rect.Width - 10, rect.Height - 10, Radius, 12, 18, 38, 70);
        }
    }
}
