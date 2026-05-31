using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private sealed class MiscInventoryIconPickerOptions
        {
            public string Title;
            public string CloseId;
            public string CloseLabel;
            public string CloseTooltip;
            public string ConfirmId;
            public string ConfirmLabel;
            public string ConfirmTooltip;
            public string EmptyText;
            public string ToggleIdPrefix;
            public string ToggleLabel;
            public string SelectIdPrefix;
            public string SelectLabel;
            public string TooltipPrefix;
            public int TargetIndex;
            public Func<QuickItemInventoryCandidate, bool> IsSelected;
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
            if (pickerOpen)
            {
                return CalculateAutoItemPickerPanelHeight(viewportWidth, pickerCandidateCount);
            }

            return LegacyUiMetrics.SectionHeaderHeight + CalculateAutoSellCardsBodyHeight(viewportWidth, itemCount);
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

        private static int CalculateAutoItemPickerPanelHeight(int viewportWidth, int candidateCount)
        {
            var width = Math.Max(1, viewportWidth - 16);
            var cellSize = ResolveAutoItemPickerCellSize(width);
            var rows = candidateCount <= 0 ? 1 : (candidateCount + AutoItemPickerColumnCount - 1) / AutoItemPickerColumnCount;
            var bodyHeight = rows * cellSize + Math.Max(0, rows - 1) * AutoItemPickerCellGap + 4;
            return 34 + bodyHeight + 8;
        }

        private static int ResolveAutoItemPickerCellSize(int bodyWidth)
        {
            var usableWidth = Math.Max(1, bodyWidth);
            var gapWidth = (AutoItemPickerColumnCount - 1) * AutoItemPickerCellGap;
            return Math.Max(AutoItemPickerCellMinSize, (usableWidth - gapWidth) / AutoItemPickerColumnCount);
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
            if (itemIds == null)
            {
                itemIds = new List<int>();
            }

            if (itemIds.Count <= 0 && !pickerOpen)
            {
                if (closePicker != null)
                {
                    closePicker();
                }

                consumedHeight = 0;
                return null;
            }

            if (pickerOpen && (pickerIndex < -1 || pickerIndex >= itemIds.Count) && closePicker != null)
            {
                closePicker();
                pickerOpen = false;
            }

            if (itemIds.Count <= 0 && !pickerOpen)
            {
                consumedHeight = 0;
                return null;
            }

            List<QuickItemInventoryCandidate> pickerCandidates = null;
            if (pickerOpen && buildPickerCandidates != null)
            {
                pickerCandidates = buildPickerCandidates();
            }

            var pickerCandidateCount = pickerCandidates == null ? 0 : pickerCandidates.Count;
            var clip = area.Viewport;
            var hovered = (LegacyUiElement)null;

            if (pickerOpen && drawPicker != null)
            {
                consumedHeight = CalculateAutoItemPickerPanelHeight(area.Viewport.Width, pickerCandidateCount);
                var pickerRect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, consumedHeight);
                return drawPicker(spriteBatch, area, mouse, elements, pickerRect, pickerCandidates, itemIds);
            }

            consumedHeight = CalculateAutoSellPanelHeight(area.Viewport.Width, itemIds.Count, false, 0);
            DrawSection(spriteBatch, area, contentY, sectionTitle);
            var cardsContentY = contentY + LegacyUiMetrics.SectionHeaderHeight;
            var cardsHeight = CalculateAutoSellCardsBodyHeight(area.Viewport.Width, itemIds.Count);
            var cardsRect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(cardsContentY), area.Viewport.Width, cardsHeight);

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

        private static LegacyUiElement DrawMiscInventoryIconPickerPanel(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect rect,
            List<QuickItemInventoryCandidate> candidates,
            MiscInventoryIconPickerOptions options)
        {
            if (rect.Height <= 0 || rect.Width <= 0 || options == null)
            {
                return null;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
            var hovered = (LegacyUiElement)null;
            var hasConfirm = !string.IsNullOrWhiteSpace(options.ConfirmId);
            var confirmRect = hasConfirm
                ? new LegacyUiRect(rect.Right - 58, rect.Y + 6, 50, 20)
                : new LegacyUiRect(rect.Right - 28, rect.Y + 6, 20, 20);
            var closeRect = hasConfirm
                ? new LegacyUiRect(confirmRect.X - 26, rect.Y + 6, 20, 20)
                : confirmRect;
            var titleRightPadding = hasConfirm ? 104 : 54;
            UiTextRenderer.DrawTextClipped(spriteBatch, options.Title, rect.X + 10, rect.Y + 8, rect.Width - titleRightPadding, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.70f);

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

            if (hasConfirm)
            {
                var confirmHit = confirmRect.Intersect(area.Viewport);
                var confirmHovered = confirmHit.Width > 0 && confirmHit.Height > 0 && confirmHit.Contains(mouse.X, mouse.Y);
                LegacyUiTheme.DrawButtonClipped(spriteBatch, confirmRect, confirmHovered, confirmHovered && mouse.LeftDown, false, true, area.Viewport);
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, options.ConfirmLabel, confirmRect.X + 4, confirmRect.Y, confirmRect.Width - 8, confirmRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 230, 236, 220, 255, 0.66f);
                var confirmElement = new LegacyUiElement
                {
                    Id = options.ConfirmId,
                    Label = options.ConfirmLabel,
                    Kind = "button",
                    Rect = confirmHit.Width > 0 && confirmHit.Height > 0 ? confirmHit : confirmRect,
                    TooltipLines = new[] { options.ConfirmTooltip }
                };
                elements.Add(confirmElement);
                if (confirmHovered)
                {
                    hovered = confirmElement;
                }
            }

            var body = new LegacyUiRect(rect.X + 8, rect.Y + 32, rect.Width - 16, Math.Max(0, rect.Height - 40));
            if (candidates == null || candidates.Count <= 0)
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, options.EmptyText, body.X + 4, body.Y + 8, body.Width - 8, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 206, 214, 228, 236, 0.64f);
                return hovered;
            }

            var cellSize = ResolveAutoItemPickerCellSize(body.Width);
            for (var index = 0; index < candidates.Count; index++)
            {
                var rowIndex = index / AutoItemPickerColumnCount;
                var columnIndex = index % AutoItemPickerColumnCount;
                var cell = new LegacyUiRect(
                    body.X + columnIndex * (cellSize + AutoItemPickerCellGap),
                    body.Y + rowIndex * (cellSize + AutoItemPickerCellGap),
                    cellSize,
                    cellSize);
                hovered = DrawMiscInventoryIconPickerCandidate(spriteBatch, mouse, elements, cell, area.Viewport, candidates[index], options) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawMiscInventoryIconPickerCandidate(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect cell,
            LegacyUiRect clip,
            QuickItemInventoryCandidate candidate,
            MiscInventoryIconPickerOptions options)
        {
            if (candidate == null || cell.Width <= 0 || cell.Height <= 0 || !cell.Intersects(clip) || options == null)
            {
                return null;
            }

            var selected = options.IsSelected != null && options.IsSelected(candidate);
            var hit = cell.Intersect(clip);
            var hovered = hit.Width > 0 && hit.Height > 0 && hit.Contains(mouse.X, mouse.Y);
            DrawMiscItemIcon(spriteBatch, cell, clip, candidate.ItemType, hovered, false, string.Empty, 0.46f);
            if (selected)
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, cell.Right - 15, cell.Y + 4, 10, 10, clip.X, clip.Y, clip.Width, clip.Height, 5, 126, 226, 156, 245);
            }

            var addMode = options.TargetIndex < 0 && !string.IsNullOrWhiteSpace(options.ToggleIdPrefix);
            var label = string.IsNullOrWhiteSpace(candidate.ItemName)
                ? "#" + candidate.ItemType.ToString(CultureInfo.InvariantCulture)
                : candidate.ItemName + " #" + candidate.ItemType.ToString(CultureInfo.InvariantCulture);
            var element = new LegacyUiElement
            {
                Id = addMode
                    ? options.ToggleIdPrefix + candidate.ItemType.ToString(CultureInfo.InvariantCulture)
                    : options.SelectIdPrefix + options.TargetIndex.ToString(CultureInfo.InvariantCulture) + ":" + candidate.ItemType.ToString(CultureInfo.InvariantCulture),
                Label = addMode ? options.ToggleLabel : options.SelectLabel,
                Kind = "button",
                Rect = hit.Width > 0 && hit.Height > 0 ? hit : cell,
                Selected = selected,
                TooltipLines = new[]
                {
                    options.TooltipPrefix + label + "（背包槽位 #" + (candidate.Slot + 1).ToString(CultureInfo.InvariantCulture) + "）"
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
