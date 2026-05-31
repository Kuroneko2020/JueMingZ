using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private sealed class MiscInventoryPickerOptions
        {
            public string Title;
            public string CloseId;
            public string CloseLabel;
            public string CloseTooltip;
            public string EmptyText;
            public string SelectIdPrefix;
            public string SelectLabel;
            public string SelectTooltipPrefix;
            public string DuplicateTooltip;
            public Func<QuickItemInventoryCandidate, bool> IsDuplicate;
        }

        private delegate LegacyUiElement MiscItemCardRenderer(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect clip,
            int itemType,
            int index,
            LegacyUiRect card);

        private delegate LegacyUiElement MiscItemPickerRenderer(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect rect,
            List<QuickItemInventoryCandidate> candidates,
            List<int> itemIds);

        private static int CalculateAutoSellPanelHeight(int viewportWidth, int itemCount, bool pickerOpen, int pickerCandidateCount)
        {
            var height = LegacyUiMetrics.SectionHeaderHeight + CalculateAutoSellCardsBodyHeight(viewportWidth, itemCount);
            if (pickerOpen)
            {
                height += CalculateQuickItemPickerPanelHeight(viewportWidth, pickerCandidateCount) + 8;
            }

            return height;
        }

        private static int CalculateAutoSellCardsBodyHeight(int viewportWidth, int itemCount)
        {
            if (itemCount <= 0)
            {
                return 44;
            }

            int columns;
            int rows;
            int cardWidth;
            ComputeAutoSellCardLayout(viewportWidth, itemCount, out columns, out rows, out cardWidth);
            return rows * AutoSellGridCellHeight + Math.Max(0, rows - 1) * QuickItemCardGap + 16;
        }

        private static int CalculateQuickItemPickerPanelHeight(int viewportWidth, int candidateCount)
        {
            var width = Math.Max(1, viewportWidth - 16);
            var columnWidth = Math.Max(100, (width - QuickItemPickerColumnGap) / QuickItemPickerColumnCount);
            var columns = columnWidth > 0 ? QuickItemPickerColumnCount : 1;
            var rows = candidateCount <= 0 ? 1 : (candidateCount + columns - 1) / columns;
            var bodyHeight = rows * (QuickItemPickerRowHeight + 3) + 4;
            return 40 + bodyHeight + 8;
        }

        private static void ComputeAutoSellCardLayout(int viewportWidth, int itemCount, out int columns, out int rows, out int cardWidth)
        {
            var innerWidth = Math.Max(120, viewportWidth - 16);
            columns = Math.Max(1, Math.Min(AutoSellGridColumnCount, (innerWidth + QuickItemCardGap) / (AutoSellGridCellMinWidth + QuickItemCardGap)));
            cardWidth = Math.Max(AutoSellGridCellMinWidth, (innerWidth - (columns - 1) * QuickItemCardGap) / columns);
            rows = itemCount <= 0 ? 0 : (itemCount + columns - 1) / columns;
        }

        private static LegacyUiElement DrawMiscItemTypeListPanel(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            out int consumedHeight,
            List<int> itemIds,
            bool pickerOpen,
            int pickerIndex,
            Action closePicker,
            Func<List<QuickItemInventoryCandidate>> buildPickerCandidates,
            string sectionTitle,
            MiscItemCardRenderer drawCard,
            MiscItemPickerRenderer drawPicker)
        {
            if (itemIds == null || itemIds.Count <= 0)
            {
                if (closePicker != null)
                {
                    closePicker();
                }

                consumedHeight = 0;
                return null;
            }

            if (pickerOpen && (pickerIndex < 0 || pickerIndex >= itemIds.Count) && closePicker != null)
            {
                closePicker();
                pickerOpen = false;
            }

            List<QuickItemInventoryCandidate> pickerCandidates = null;
            if (pickerOpen && buildPickerCandidates != null)
            {
                pickerCandidates = buildPickerCandidates();
            }

            var pickerCandidateCount = pickerCandidates == null ? 0 : pickerCandidates.Count;
            consumedHeight = CalculateAutoSellPanelHeight(area.Viewport.Width, itemIds.Count, pickerOpen, pickerCandidateCount);
            DrawSection(spriteBatch, area, contentY, sectionTitle);
            var cardsContentY = contentY + LegacyUiMetrics.SectionHeaderHeight;
            var cardsHeight = CalculateAutoSellCardsBodyHeight(area.Viewport.Width, itemIds.Count);
            var cardsRect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(cardsContentY), area.Viewport.Width, cardsHeight);
            var clip = area.Viewport;
            var hovered = (LegacyUiElement)null;

            if (area.IsVisible(cardsRect))
            {
                LegacyUiTheme.DrawSubPanelClipped(spriteBatch, cardsRect, clip);
                int columns;
                int rows;
                int cardWidth;
                ComputeAutoSellCardLayout(area.Viewport.Width, itemIds.Count, out columns, out rows, out cardWidth);
                for (var index = 0; index < itemIds.Count; index++)
                {
                    var rowIndex = index / columns;
                    var columnIndex = index % columns;
                    var card = new LegacyUiRect(
                        cardsRect.X + 8 + columnIndex * (cardWidth + QuickItemCardGap),
                        cardsRect.Y + 8 + rowIndex * (AutoSellGridCellHeight + QuickItemCardGap),
                        cardWidth,
                        AutoSellGridCellHeight);
                    hovered = drawCard(spriteBatch, mouse, elements, clip, itemIds[index], index, card) ?? hovered;
                }
            }

            if (pickerOpen && drawPicker != null)
            {
                var pickerHeight = CalculateQuickItemPickerPanelHeight(area.Viewport.Width, pickerCandidateCount);
                var pickerRect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(cardsContentY + cardsHeight), area.Viewport.Width, pickerHeight);
                hovered = drawPicker(spriteBatch, area, mouse, elements, pickerRect, pickerCandidates, itemIds) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawMiscItemTypeCard(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect clip,
            int itemType,
            int index,
            LegacyUiRect card,
            bool selected,
            string openIdPrefix,
            string openLabel,
            string removeIdPrefix,
            string removeLabel,
            string itemLabel,
            string selectedTooltip,
            string emptyTooltip,
            string removeFilledTooltip,
            string removeEmptyTooltip)
        {
            LegacyUiTheme.DrawRowClipped(spriteBatch, card, clip);
            var itemButtonRect = new LegacyUiRect(card.X + 4, card.Y + 3, Math.Max(1, card.Width - 8), Math.Max(1, card.Height - 6));
            var itemHit = itemButtonRect.Intersect(clip);
            var itemHovered = itemHit.Width > 0 && itemHit.Height > 0 && itemHit.Contains(mouse.X, mouse.Y);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, itemButtonRect, itemHovered, itemHovered && mouse.LeftDown, selected, true, clip);

            var iconRect = new LegacyUiRect(
                itemButtonRect.X + Math.Max(0, (itemButtonRect.Width - AutoSellGridIconCellSize) / 2),
                itemButtonRect.Y + Math.Max(0, (itemButtonRect.Height - AutoSellGridIconCellSize) / 2),
                AutoSellGridIconCellSize,
                AutoSellGridIconCellSize);
            DrawMiscItemIcon(spriteBatch, iconRect, clip, itemType, itemHovered, false, "+", 0.84f);

            var itemElement = new LegacyUiElement
            {
                Id = openIdPrefix + index.ToString(CultureInfo.InvariantCulture),
                Label = openLabel,
                Kind = "button",
                Rect = itemHit.Width > 0 && itemHit.Height > 0 ? itemHit : itemButtonRect,
                TooltipLines = itemType > 0
                    ? new[] { itemLabel + " #" + itemType.ToString(CultureInfo.InvariantCulture), selectedTooltip }
                    : new[] { "未选择", emptyTooltip }
            };
            elements.Add(itemElement);
            var hovered = itemHovered ? itemElement : null;

            if (itemHovered)
            {
                var deleteRect = new LegacyUiRect(itemButtonRect.Right - 15, itemButtonRect.Y + 1, 14, 14);
                var deleteHit = deleteRect.Intersect(clip);
                var deleteHovered = deleteHit.Width > 0 && deleteHit.Height > 0 && deleteHit.Contains(mouse.X, mouse.Y);
                LegacyUiTheme.DrawButtonClipped(spriteBatch, deleteRect, deleteHovered, deleteHovered && mouse.LeftDown, false, true, clip);
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "x", deleteRect.X + 1, deleteRect.Y, deleteRect.Width - 2, deleteRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 238, 196, 180, 255, 0.64f);
                var deleteElement = new LegacyUiElement
                {
                    Id = removeIdPrefix + index.ToString(CultureInfo.InvariantCulture),
                    Label = removeLabel,
                    Kind = "button",
                    Rect = deleteHit.Width > 0 && deleteHit.Height > 0 ? deleteHit : deleteRect,
                    TooltipLines = new[] { itemType > 0 ? removeFilledTooltip : removeEmptyTooltip }
                };
                elements.Add(deleteElement);
                if (deleteHovered)
                {
                    hovered = deleteElement;
                }
            }

            return hovered;
        }

        private static LegacyUiElement DrawMiscInventoryPickerPanel(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect rect,
            List<QuickItemInventoryCandidate> candidates,
            int targetIndex,
            int selectedItemType,
            MiscInventoryPickerOptions options)
        {
            if (rect.Height <= 0 || rect.Width <= 0 || options == null)
            {
                return null;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
            var hovered = (LegacyUiElement)null;
            UiTextRenderer.DrawTextClipped(spriteBatch, options.Title, rect.X + 10, rect.Y + 8, rect.Width - 54, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.70f);

            var closeRect = new LegacyUiRect(rect.Right - 28, rect.Y + 6, 20, 20);
            var closeHit = closeRect.Intersect(area.Viewport);
            var closeHovered = closeHit.Width > 0 && closeHit.Height > 0 && closeHit.Contains(mouse.X, mouse.Y);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, closeRect, closeHovered, closeHovered && mouse.LeftDown, false, true, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "x", closeRect.X + 2, closeRect.Y + 1, closeRect.Width - 4, closeRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 196, 180, 255, 0.72f);
            var closeElement = new LegacyUiElement
            {
                Id = options.CloseId,
                Label = options.CloseLabel,
                Kind = "button",
                Rect = closeHit.Width > 0 && closeHit.Height > 0 ? closeHit : closeRect,
                TooltipLines = new[] { options.CloseTooltip }
            };
            elements.Add(closeElement);
            if (closeHovered)
            {
                hovered = closeElement;
            }

            var body = new LegacyUiRect(rect.X + 8, rect.Y + 32, rect.Width - 16, Math.Max(0, rect.Height - 40));
            if (candidates == null || candidates.Count <= 0)
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, options.EmptyText, body.X + 4, body.Y + 8, body.Width - 8, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 206, 214, 228, 236, 0.64f);
                return hovered;
            }

            var columnWidth = Math.Max(100, (body.Width - QuickItemPickerColumnGap) / QuickItemPickerColumnCount);
            for (var index = 0; index < candidates.Count; index++)
            {
                var rowIndex = index / QuickItemPickerColumnCount;
                var columnIndex = index % QuickItemPickerColumnCount;
                var row = new LegacyUiRect(
                    body.X + columnIndex * (columnWidth + QuickItemPickerColumnGap),
                    body.Y + rowIndex * (QuickItemPickerRowHeight + 3),
                    columnWidth,
                    QuickItemPickerRowHeight);
                hovered = DrawMiscInventoryPickerCandidate(spriteBatch, mouse, elements, row, area.Viewport, candidates[index], selectedItemType, targetIndex, options) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawMiscInventoryPickerCandidate(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect row,
            LegacyUiRect clip,
            QuickItemInventoryCandidate candidate,
            int selectedItemType,
            int targetIndex,
            MiscInventoryPickerOptions options)
        {
            if (candidate == null || row.Width <= 0 || row.Height <= 0 || !row.Intersects(clip) || options == null)
            {
                return null;
            }

            var duplicate = options.IsDuplicate != null && options.IsDuplicate(candidate);
            var selected = candidate.ItemType > 0 && candidate.ItemType == selectedItemType;
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, clip);
            if (selected)
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, row.X + 1, row.Y + 1, row.Width - 2, row.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, 6, 88, 114, 86, 84);
            }
            else if (duplicate)
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, row.X + 1, row.Y + 1, row.Width - 2, row.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, 6, 78, 64, 48, 76);
            }

            var iconRect = new LegacyUiRect(row.X + 5, row.Y + 4, 22, 22);
            DrawMiscItemIcon(spriteBatch, iconRect, clip, candidate.ItemType, false, selected, string.Empty, 0.46f);

            var label = string.IsNullOrWhiteSpace(candidate.ItemName)
                ? "#" + candidate.ItemType.ToString(CultureInfo.InvariantCulture)
                : candidate.ItemName + " #" + candidate.ItemType.ToString(CultureInfo.InvariantCulture);
            UiTextRenderer.DrawTextClipped(spriteBatch, label, row.X + 31, row.Y + 7, row.Width - 36, 16, clip.X, clip.Y, clip.Width, clip.Height, duplicate ? 206 : 232, duplicate ? 194 : 234, duplicate ? 174 : 224, 246, 0.58f);

            var hit = row.Intersect(clip);
            var hovered = hit.Width > 0 && hit.Height > 0 && hit.Contains(mouse.X, mouse.Y);
            var element = new LegacyUiElement
            {
                Id = options.SelectIdPrefix + targetIndex.ToString(CultureInfo.InvariantCulture) + ":" + candidate.ItemType.ToString(CultureInfo.InvariantCulture),
                Label = options.SelectLabel,
                Kind = "button",
                Rect = hit.Width > 0 && hit.Height > 0 ? hit : row,
                Selected = selected,
                TooltipLines = new[]
                {
                    duplicate && !string.IsNullOrWhiteSpace(options.DuplicateTooltip)
                        ? options.DuplicateTooltip
                        : options.SelectTooltipPrefix + label + "（背包槽位 #" + (candidate.Slot + 1).ToString(CultureInfo.InvariantCulture) + "）"
                }
            };
            elements.Add(element);
            return hovered ? element : null;
        }

        private static void DrawMiscItemIcon(
            object spriteBatch,
            LegacyUiRect iconRect,
            LegacyUiRect clip,
            int itemType,
            bool hovered,
            bool selected,
            string emptyText,
            float emptyScale)
        {
            LegacyUiTheme.DrawCellClipped(spriteBatch, iconRect, hovered, selected, false, clip);
            if (itemType > 0)
            {
                object texture;
                if (VanillaUiSkinCompat.TryGetItemTexture(itemType, out texture))
                {
                    UiPrimitiveRenderer.DrawTextureContainedClipped(spriteBatch, texture, iconRect.X + 2, iconRect.Y + 2, iconRect.Width - 4, iconRect.Height - 4, clip.X, clip.Y, clip.Width, clip.Height, 255, 255, 255, 255);
                }
                else
                {
                    var fallbackY = iconRect.Height <= 22 ? iconRect.Y + 6 : iconRect.Y + 5;
                    UiTextRenderer.DrawCenteredTextClipped(spriteBatch, itemType.ToString(CultureInfo.InvariantCulture), iconRect.X + 1, fallbackY, iconRect.Width - 2, 12, clip.X, clip.Y, clip.Width, clip.Height, 232, 236, 220, 255, 0.46f);
                }

                return;
            }

            if (!string.IsNullOrEmpty(emptyText))
            {
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, emptyText, iconRect.X + 1, iconRect.Y + 2, iconRect.Width - 2, iconRect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, 232, 236, 220, 255, emptyScale);
            }
        }
    }
}
