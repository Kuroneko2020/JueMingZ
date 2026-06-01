using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static int GetFishingFilterActionButtonGap(int totalWidth)
        {
            return Math.Min(10, Math.Max(6, totalWidth / 38));
        }

        private static int[] BuildFishingFilterActionButtonWidths(int totalWidth, int buttonGap, string[] labels)
        {
            var count = labels == null ? 0 : labels.Length;
            var widths = new int[count];
            if (count <= 0)
            {
                return widths;
            }

            var minimums = new int[count];
            var maximums = new int[count];
            var availableWidth = Math.Max(1, totalWidth - buttonGap * Math.Max(0, count - 1));
            for (var index = 0; index < count; index++)
            {
                var label = labels[index] ?? string.Empty;
                var plus = string.Equals(label, "+", StringComparison.Ordinal);
                var searchInput = label.IndexOf("#", StringComparison.Ordinal) >= 0 || label.IndexOf("ID", StringComparison.OrdinalIgnoreCase) >= 0;
                var shortLabel = label.Length <= 2;
                var current = string.Equals(label, "添加当前", StringComparison.Ordinal);
                var minimum = searchInput ? 72 : plus ? 30 : shortLabel ? 40 : current ? 58 : 54;
                var maximum = searchInput ? 116 : plus ? 40 : shortLabel ? 54 : current ? 80 : 74;
                var preferred = UiTextRenderer.EstimateTextWidth(label, 0.66f) + 18;
                minimums[index] = minimum;
                maximums[index] = Math.Max(minimum, maximum);
                widths[index] = Math.Min(maximums[index], Math.Max(minimum, preferred));
            }

            var widthSum = Sum(widths);
            while (widthSum > availableWidth)
            {
                var changed = false;
                for (var index = widths.Length - 1; index >= 0 && widthSum > availableWidth; index--)
                {
                    if (widths[index] <= minimums[index])
                    {
                        continue;
                    }

                    widths[index]--;
                    widthSum--;
                    changed = true;
                }

                if (!changed)
                {
                    break;
                }
            }

            while (widthSum < availableWidth)
            {
                var changed = false;
                for (var index = 0; index < widths.Length && widthSum < availableWidth; index++)
                {
                    if (widths[index] >= maximums[index])
                    {
                        continue;
                    }

                    widths[index]++;
                    widthSum++;
                    changed = true;
                }

                if (!changed)
                {
                    break;
                }
            }

            return widths;
        }

        private static int Sum(int[] values)
        {
            var total = 0;
            if (values == null)
            {
                return total;
            }

            for (var index = 0; index < values.Length; index++)
            {
                total += values[index];
            }

            return total;
        }

        private static float FitFishingFilterActionButtonScale(string label, int width, float preferredScale)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return preferredScale;
            }

            var scale = preferredScale;
            var maxTextWidth = Math.Max(1, width - 8);
            while (scale > 0.48f && UiTextRenderer.EstimateTextWidth(label, scale) > maxTextWidth)
            {
                scale -= 0.04f;
            }

            return scale < 0.48f ? 0.48f : scale;
        }

        private static int GetFishingFilterCompactButtonWidth(int totalWidth, int buttonGap, int compactButtonCount, int leadingTargetWidth)
        {
            var maxByLeading = Math.Max(1, (totalWidth - buttonGap * compactButtonCount - Math.Max(1, leadingTargetWidth)) / Math.Max(1, compactButtonCount));
            return Math.Max(1, Math.Min(78, maxByLeading));
        }

        private static LegacyUiElement DrawFishingFilterButton(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, bool selected, string tooltip)
        {
            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, selected, true, area.Viewport);
            var scale = rect.Width < 36
                ? 0.52f
                : rect.Width < 44
                    ? 0.58f
                    : label != null && label.Length >= 4 ? 0.68f : 0.74f;
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, label, rect.X + 3, rect.Y, rect.Width - 6, rect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, selected ? LegacyUiTheme.SelectedTextR : 230, selected ? LegacyUiTheme.SelectedTextG : 232, selected ? LegacyUiTheme.SelectedTextB : 224, 255, scale);
            if (selected)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(rect.X + 3, rect.Y, rect.Width - 6, rect.Height), area.Viewport, label, scale);
            }

            var element = AddFrameElement(elements, id, label, "button", elementRect, selected: selected, tooltipLines: string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawFishingFilterActionButton(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, bool selected, bool enabled, string tooltip)
        {
            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = enabled && IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, selected, enabled, area.Viewport);
            var scale = FitFishingFilterActionButtonScale(label, rect.Width, label != null && label.Length >= 5 ? 0.60f : label != null && label.Length >= 4 ? 0.66f : 0.72f);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, label, rect.X + 3, rect.Y, rect.Width - 6, rect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, enabled ? 230 : 150, enabled ? 232 : 154, enabled ? 224 : 160, enabled ? 255 : 180, scale);
            if (selected && enabled)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(rect.X + 3, rect.Y, rect.Width - 6, rect.Height), area.Viewport, label, scale);
            }

            var element = AddFrameElement(elements, id, label, "button", elementRect, enabled: enabled, selected: selected, tooltipLines: string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawFishingFilterUiOnlyButton(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, string tooltip)
        {
            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, false, true, area.Viewport);
            var scale = label != null && label.Length >= 5 ? 0.60f : label != null && label.Length >= 4 && rect.Width < 64 ? 0.56f : 0.74f;
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, label, rect.X + 3, rect.Y, rect.Width - 6, rect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 230, 232, 224, 255, scale);
            var element = AddFrameElement(elements, id, label, "button", elementRect, enabled: false, tooltipLines: string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }
    }
}
