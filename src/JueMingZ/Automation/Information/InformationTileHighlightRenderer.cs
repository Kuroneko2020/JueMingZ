using System;
using JueMingZ.UI;

namespace JueMingZ.Automation.Information
{
    internal static class InformationTileHighlightRenderer
    {
        private const int TileSize = 16;

        internal static int Draw(object spriteBatch, InformationWorldContext context, TileHighlight[] highlights)
        {
            if (context == null || highlights == null || highlights.Length <= 0)
            {
                return 0;
            }

            var drawn = 0;
            var pulse = 155 + (int)(Math.Abs(Math.Sin(context.GameUpdateCount / 12d)) * 80d);
            for (var index = 0; index < highlights.Length; index++)
            {
                var highlight = highlights[index];
                var x = (int)Math.Round(highlight.TileX * TileSize - context.ScreenX);
                var y = (int)Math.Round(highlight.TileY * TileSize - context.ScreenY);
                var color = highlight.Color;
                var borderAlpha = Math.Min(255, Math.Max(color.A, pulse));
                if (DrawFrame(spriteBatch, x, y, highlight.PixelWidth, highlight.PixelHeight, color, borderAlpha))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static bool DrawFrame(object spriteBatch, int x, int y, int width, int height, InformationColor color, int alpha)
        {
            var outerX = x - 3;
            var outerY = y - 3;
            var outerWidth = width + 6;
            var outerHeight = height + 6;
            var corner = Math.Max(8, Math.Min(18, Math.Min(outerWidth, outerHeight) / 2));
            var ok = UiPrimitiveRenderer.DrawRectBorder(spriteBatch, outerX, outerY, outerWidth, outerHeight, 1, color.R, color.G, color.B, alpha);
            ok |= UiPrimitiveRenderer.DrawRectBorder(spriteBatch, outerX - 2, outerY - 2, outerWidth + 4, outerHeight + 4, 1, 255, 255, 255, 120);
            ok |= DrawCorner(spriteBatch, outerX - 1, outerY - 1, corner, 1, 1, color, Math.Min(255, alpha + 20));
            ok |= DrawCorner(spriteBatch, outerX + outerWidth, outerY - 1, corner, -1, 1, color, Math.Min(255, alpha + 20));
            ok |= DrawCorner(spriteBatch, outerX - 1, outerY + outerHeight, corner, 1, -1, color, Math.Min(255, alpha + 20));
            ok |= DrawCorner(spriteBatch, outerX + outerWidth, outerY + outerHeight, corner, -1, -1, color, Math.Min(255, alpha + 20));
            return ok;
        }

        private static bool DrawCorner(object spriteBatch, int x, int y, int length, int horizontalDirection, int verticalDirection, InformationColor color, int alpha)
        {
            var thickness = 3;
            var horizontalX = horizontalDirection > 0 ? x : x - length;
            var verticalY = verticalDirection > 0 ? y : y - length;
            var ok = UiPrimitiveRenderer.DrawFilledRect(spriteBatch, horizontalX, y, length, thickness, color.R, color.G, color.B, alpha);
            ok |= UiPrimitiveRenderer.DrawFilledRect(spriteBatch, x, verticalY, thickness, length, color.R, color.G, color.B, alpha);
            return ok;
        }
    }
}
