using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawQuickReforgeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var enabled = settings != null && settings.NpcAutoReforgeEnabled;
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, "自动重铸", row.X + 10, row.Y, 110, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);

            var gap = 6;
            var buttonY = RowModeButtonY(row);
            var offWidth = ModeButtonWidth("关闭");
            var onWidth = ModeButtonWidth("开启");
            var addWidth = ModeButtonWidth("添加");
            var offRect = new LegacyUiRect(row.Right - offWidth - 10, buttonY, offWidth, RowModeButtonHeight);
            var onRect = new LegacyUiRect(offRect.X - gap - onWidth, buttonY, onWidth, RowModeButtonHeight);
            var addRect = new LegacyUiRect(onRect.X - gap - addWidth, buttonY, addWidth, RowModeButtonHeight);
            var inputX = row.X + 120;
            var inputWidth = Math.Max(80, addRect.X - inputX - gap);
            var inputRect = new LegacyUiRect(inputX, buttonY, inputWidth, RowModeButtonHeight);

            var inputFocused = LegacyTextInput.IsFocused(MiscQuickReforgeTextInputId);
            if (inputFocused)
            {
                LegacyTextInput.Update(MiscQuickReforgeTextInputId);
            }

            var draft = LegacyTextInput.GetDraft(MiscQuickReforgeTextInputId);
            var inputText = inputFocused
                ? LegacyTextInput.GetDisplayText(MiscQuickReforgeTextInputId, "双击输入完整词缀名")
                : (string.IsNullOrWhiteSpace(draft) ? "双击输入完整词缀名" : draft);
            var hovered = (LegacyUiElement)null;
            hovered = DrawQuickReforgeInputElement(spriteBatch, mouse, elements, area.Viewport, inputRect, inputFocused, inputText) ?? hovered;
            hovered = DrawQuickReforgeActionButton(spriteBatch, mouse, elements, area.Viewport, addRect, "misc-quick-reforge:add", "快速重铸:添加", "添加", false, "把输入框的词缀加入名单。") ?? hovered;
            hovered = DrawQuickReforgeActionButton(spriteBatch, mouse, elements, area.Viewport, onRect, "misc-quick-reforge-mode:On", "快速重铸:开启", "开启", enabled, "按住重铸键直到名单的词缀停下") ?? hovered;
            hovered = DrawQuickReforgeActionButton(spriteBatch, mouse, elements, area.Viewport, offRect, "misc-quick-reforge-mode:Off", "快速重铸:关闭", "关闭", !enabled, "关闭快速重铸功能。") ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawQuickReforgeInputElement(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, LegacyUiRect rect, bool selected, string text)
        {
            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered("misc-quick-reforge:input", elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, selected, true, clip);
            var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, selected, true);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                text ?? string.Empty,
                rect.X + 8,
                contentRect.Y + 3,
                rect.Width - 16,
                Math.Max(1, contentRect.Height - 6),
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                selected ? LegacyUiTheme.SelectedTextR : 230,
                selected ? LegacyUiTheme.SelectedTextG : 232,
                selected ? LegacyUiTheme.SelectedTextB : 224,
                255,
                0.72f);
            TryAttachLegacyTextInputImePanel(MiscQuickReforgeTextInputId, rect, clip);
            if (selected)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(rect.X + 8, contentRect.Y + 3, rect.Width - 16, Math.Max(1, contentRect.Height - 6)), clip, text ?? string.Empty, 0.72f);
            }

            var element = AddFrameElement(elements, "misc-quick-reforge:input", "快速重铸:词缀输入", "button", elementRect, selected: selected, tooltipLines: new[] { "双击输入完整词缀名。", "不支持只填单字模糊匹配。" });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawQuickReforgeActionButton(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, LegacyUiRect rect, string id, string label, string text, bool selected, string tooltip)
        {
            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, selected, true, clip);
            var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, selected, true);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                text,
                rect.X + 3,
                contentRect.Y,
                rect.Width - 6,
                contentRect.Height,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                selected ? LegacyUiTheme.SelectedTextR : 230,
                selected ? LegacyUiTheme.SelectedTextG : 232,
                selected ? LegacyUiTheme.SelectedTextB : 224,
                255,
                0.78f);
            if (selected)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(rect.X + 3, contentRect.Y, rect.Width - 6, contentRect.Height), clip, text, 0.78f);
            }

            var element = AddFrameElement(elements, id, label, "button", elementRect, selected: selected, tooltipLines: string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawQuickReforgeListPanel(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, out int consumedHeight)
        {
            var prefixes = GetQuickReforgePrefixes();
            if (prefixes.Count <= 0)
            {
                consumedHeight = 0;
                return null;
            }

            consumedHeight = CalculateQuickReforgePanelHeight(area.Viewport.Width, prefixes.Count);
            DrawSection(spriteBatch, area, contentY, "快速重铸名单");
            var bodyHeight = CalculateAutoSellCardsBodyHeight(area.Viewport.Width, prefixes.Count);
            var bodyRect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY + LegacyUiMetrics.SectionHeaderHeight), area.Viewport.Width, bodyHeight);
            var hovered = (LegacyUiElement)null;
            if (area.IsVisible(bodyRect))
            {
                LegacyUiTheme.DrawSubPanelClipped(spriteBatch, bodyRect, area.Viewport);
                int columns;
                int rows;
                int cardWidth;
                ComputeAutoSellCardLayout(area.Viewport.Width, prefixes.Count, out columns, out rows, out cardWidth);
                for (var index = 0; index < prefixes.Count; index++)
                {
                    var rowIndex = index / columns;
                    var columnIndex = index % columns;
                    var entryRect = new LegacyUiRect(
                        bodyRect.X + 8 + columnIndex * (cardWidth + QuickItemCardGap),
                        bodyRect.Y + 8 + rowIndex * (AutoSellGridCellHeight + QuickItemCardGap),
                        cardWidth,
                        AutoSellGridCellHeight);
                    if (!entryRect.Intersects(area.Viewport))
                    {
                        continue;
                    }

                    hovered = DrawQuickReforgePrefixEntry(spriteBatch, mouse, elements, area.Viewport, entryRect, prefixes[index], index) ?? hovered;
                }
            }

            return hovered;
        }

        private static LegacyUiElement DrawQuickReforgePrefixEntry(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, LegacyUiRect rect, string prefix, int index)
        {
            if (rect.Width <= 0 || rect.Height <= 0 || !rect.Intersects(clip))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, rect, clip);
            var entryRect = new LegacyUiRect(rect.X + 4, rect.Y + 3, Math.Max(1, rect.Width - 8), Math.Max(1, rect.Height - 6));
            var entryHit = entryRect.Intersect(clip);
            var entryId = "misc-quick-reforge:prefix:" + index.ToString(CultureInfo.InvariantCulture);
            var entryElementRect = entryHit.Width > 0 && entryHit.Height > 0 ? entryHit : entryRect;
            var entryAreaHovered = mouse != null && entryElementRect.Contains(mouse.X, mouse.Y);
            var entryHovered = IsFrameElementHovered(entryId, entryElementRect, mouse);
            var display = string.IsNullOrWhiteSpace(prefix) ? "?" : prefix.Trim();
            LegacyUiTheme.DrawButtonClipped(spriteBatch, entryRect, entryAreaHovered, entryAreaHovered && mouse.LeftDown, false, true, clip);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                display,
                entryRect.X + 3,
                entryRect.Y,
                Math.Max(1, entryRect.Width - 6),
                entryRect.Height,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                232,
                234,
                224,
                246,
                0.62f);

            var entryElement = AddFrameElement(elements, entryId, "快速重铸词缀", "blocker", entryElementRect, tooltipLines: new[] { display, "悬停后点击 x 可移除。" });
            RecordFrameElementHover(entryElement, entryHovered);
            var hovered = entryHovered ? entryElement : null;

            if (entryAreaHovered)
            {
                var removeRect = new LegacyUiRect(entryRect.Right - 15, entryRect.Y + 1, 14, 14);
                var removeHit = removeRect.Intersect(clip);
                var removeId = "misc-quick-reforge:remove:" + index.ToString(CultureInfo.InvariantCulture);
                var removeElementRect = removeHit.Width > 0 && removeHit.Height > 0 ? removeHit : removeRect;
                var removeHovered = IsFrameElementHovered(removeId, removeElementRect, mouse);
                LegacyUiTheme.DrawButtonClipped(spriteBatch, removeRect, removeHovered, removeHovered && mouse.LeftDown, false, true, clip);
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "x", removeRect.X + 1, removeRect.Y, removeRect.Width - 2, removeRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 238, 196, 180, 255, 0.64f);
                var removeElement = AddFrameElement(elements, removeId, "从快速重铸名单移除", "button", removeElementRect, tooltipLines: new[] { "从快速重铸名单移除这个词缀。" });
                RecordFrameElementHover(removeElement, removeHovered);
                if (removeHovered)
                {
                    hovered = removeElement;
                }
            }

            return hovered;
        }

        private static int CalculateQuickReforgePanelHeight(int viewportWidth, int prefixCount)
        {
            return LegacyUiMetrics.SectionHeaderHeight + CalculateAutoSellCardsBodyHeight(viewportWidth, prefixCount);
        }

        private static List<string> GetQuickReforgePrefixes()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (settings.NpcAutoReforgePrefixes == null)
            {
                settings.NpcAutoReforgePrefixes = new List<string>();
            }

            return settings.NpcAutoReforgePrefixes;
        }
    }
}
