using System;
using System.Collections.Generic;
using JueMingZ.UI;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public static class LegacyTooltipHostControl
    {
        public static void Draw(object spriteBatch, LegacyUiRect window, LegacyMouseSnapshot mouse, LegacyUiTooltipModel model)
        {
            if (spriteBatch == null || mouse == null || model == null || model.Lines == null || model.Lines.Length <= 0)
            {
                return;
            }

            const float scale = 0.70f;
            var maxTextWidth = Math.Max(140, Math.Min(500, window.Width - 72));
            var wrapped = WrapTooltipLines(model.Lines, maxTextWidth, scale);
            if (wrapped.Count <= 0)
            {
                return;
            }

            var lineHeight = Math.Max(24, UiTextRenderer.EstimateTextHeight(scale) + 8);
            var maxLineWidth = 0;
            for (var index = 0; index < wrapped.Count; index++)
            {
                maxLineWidth = Math.Max(maxLineWidth, UiTextRenderer.EstimateTextWidth(wrapped[index], scale));
            }

            var width = ClampInt(maxLineWidth + 28, model.Centered ? 90 : 236, maxTextWidth + 28);
            var height = 20 + wrapped.Count * lineHeight;
            var x = mouse.X + 18;
            var y = mouse.Y + 18;
            if (x + width > window.Right - 8)
            {
                x = mouse.X - width - 18;
            }

            if (y + height > window.Bottom - 8)
            {
                y = mouse.Y - height - 18;
            }

            x = ClampInt(x, window.X + 8, Math.Max(window.X + 8, window.Right - width - 8));
            y = ClampInt(y, window.Y + 8, Math.Max(window.Y + 8, window.Bottom - height - 8));
            var rect = new LegacyUiRect(x, y, width, height);
            LegacyUiTheme.DrawTooltip(spriteBatch, rect);
            for (var index = 0; index < wrapped.Count; index++)
            {
                if (model.Centered)
                {
                    UiTextRenderer.DrawCenteredText(spriteBatch, wrapped[index], rect.X + 8, rect.Y + 10 + index * lineHeight, rect.Width - 16, lineHeight, 236, 236, 222, 255, scale);
                }
                else
                {
                    UiTextRenderer.DrawText(spriteBatch, wrapped[index], rect.X + 12, rect.Y + 10 + index * lineHeight, 236, 236, 222, 255, scale);
                }
            }
        }

        private static List<string> WrapTooltipLines(string[] lines, int maxTextWidth, float scale)
        {
            var result = new List<string>();
            for (var index = 0; index < lines.Length; index++)
            {
                var sourceLine = lines[index] ?? string.Empty;
                if (sourceLine.Length <= 0)
                {
                    continue;
                }

                var splitLines = sourceLine
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split(new[] { '\n' }, StringSplitOptions.None);
                for (var splitIndex = 0; splitIndex < splitLines.Length; splitIndex++)
                {
                    var line = splitLines[splitIndex] ?? string.Empty;
                    if (line.Length <= 0)
                    {
                        continue;
                    }

                    if (UiTextRenderer.EstimateTextWidth(line, scale) <= maxTextWidth)
                    {
                        result.Add(line);
                        continue;
                    }

                    var indent = line.IndexOf(": ", StringComparison.Ordinal) > 0 ? "  " : string.Empty;
                    var current = string.Empty;
                    for (var charIndex = 0; charIndex < line.Length; charIndex++)
                    {
                        var next = current + line[charIndex];
                        if (current.Length > 0 && UiTextRenderer.EstimateTextWidth(next, scale) > maxTextWidth)
                        {
                            result.Add(current);
                            current = indent + line[charIndex];
                        }
                        else
                        {
                            current = next;
                        }
                    }

                    if (current.Length > 0)
                    {
                        result.Add(current);
                    }
                }
            }

            return result;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
