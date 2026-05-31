using System;
using System.Globalization;
using JueMingZ.Compat;

namespace JueMingZ.UI.Legacy
{
    public static class LegacySlider
    {
        public const int MinPercent = 10;
        public const int MaxPercent = 90;

        public static int ValueFromMouse(LegacyUiRect rect, int mouseX)
        {
            return ValueFromMouse(rect, mouseX, MinPercent, MaxPercent);
        }

        public static int ValueFromMouse(LegacyUiRect rect, int mouseX, int minValue, int maxValue)
        {
            var trackX = rect.X + 10;
            var trackWidth = Math.Max(1, rect.Width - 20);
            var relative = Clamp(mouseX - trackX, 0, trackWidth);
            return minValue + (int)Math.Round((maxValue - minValue) * (relative / (double)trackWidth));
        }

        public static void Draw(object spriteBatch, LegacyUiRect rect, string label, int value, bool hovered, bool dragging)
        {
            DrawValue(spriteBatch, rect, label, value, MinPercent, MaxPercent, "%", hovered, dragging);
        }

        public static void DrawValue(object spriteBatch, LegacyUiRect rect, string label, int value, int minValue, int maxValue, string suffix, bool hovered, bool dragging)
        {
            value = Clamp(value, minValue, maxValue);
            var trackY = rect.Y + rect.Height / 2 - 3;
            var trackX = rect.X + 10;
            var trackWidth = Math.Max(1, rect.Width - 20);
            var fillWidth = (int)Math.Round(trackWidth * ((value - minValue) / (double)(maxValue - minValue)));
            var knobX = trackX + fillWidth - 7;
            object texture;
            if (VanillaUiSkinCompat.TryGetColorBarTexture(out texture))
            {
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiled(spriteBatch, texture, trackX, trackY, trackWidth, 7, 255, 255, 255, 210);
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiled(spriteBatch, texture, trackX, trackY, Math.Max(7, fillWidth), 7, 255, 255, 255, 246);
            }
            else
            {
                UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, trackX, trackY, trackWidth, 7, 4, 15, 21, 44, 220);
                UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, trackX, trackY, Math.Max(7, fillWidth), 7, 4, 95, 132, 188, 230);
                UiPrimitiveRenderer.DrawRectBorder(spriteBatch, trackX, trackY, trackWidth, 7, 1, 115, 143, 198, 220);
            }

            if (VanillaUiSkinCompat.TryGetColorSliderTexture(out texture) ||
                VanillaUiSkinCompat.TryGetColorBlipTexture(out texture))
            {
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiled(spriteBatch, texture, knobX, rect.Y + 5, 14, rect.Height - 10, 255, 255, 255, dragging ? 255 : 230);
            }
            else
            {
                UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, knobX, rect.Y + 5, 14, rect.Height - 10, 7, 65, 54, 32, 230);
                UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, knobX + 1, rect.Y + 6, 12, rect.Height - 12, 6, dragging ? 232 : 196, dragging ? 224 : 208, 160, 238);
            }

            UiTextRenderer.DrawAlignedText(
                spriteBatch,
                label + " " + value.ToString(CultureInfo.InvariantCulture) + (suffix ?? string.Empty),
                rect.X + 6,
                rect.Y,
                Math.Max(1, rect.Width - 12),
                rect.Height,
                UiTextHorizontalAlignment.Left,
                hovered ? 255 : 235,
                hovered ? 245 : 235,
                210,
                255,
                0.9f);
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
