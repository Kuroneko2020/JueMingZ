using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawQuickItemBindingCard(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, QuickItemHotkeyBinding binding, int index, LegacyUiRect card)
        {
            // Quick-item cards display configuration choices and capture intent; real
            // item use stays in hotkey/runtime services.
            LegacyUiTheme.DrawRowClipped(spriteBatch, card, clip);
            var captureSelected = _quickItemHotkeyCaptureActive && _quickItemHotkeyCaptureBindingIndex == index;
            var unifiedHotkey = GetUnifiedHotkeyDisplay(UnifiedHotkeyBindingIds.ForQuickItemSlot(index));
            var hotkeyText = captureSelected
                ? "按键中..."
                : string.IsNullOrWhiteSpace(unifiedHotkey) ? "+" : unifiedHotkey;
            var hotkeyWidth = ResolveQuickItemHotkeyWidth(hotkeyText, captureSelected, card.Width);
            var buttonHeight = Math.Max(RowModeButtonHeight, card.Height - 6);
            var buttonY = card.Y + Math.Max(0, (card.Height - buttonHeight) / 2);
            var hotkeyRect = new LegacyUiRect(card.Right - hotkeyWidth - 4, buttonY, hotkeyWidth, buttonHeight);
            var itemButtonRect = new LegacyUiRect(card.X + 4, buttonY, Math.Max(1, hotkeyRect.X - card.X - 8), buttonHeight);
            var itemType = GetQuickItemBindingPrimaryItemType(binding);
            var itemHit = itemButtonRect.Intersect(clip);
            var itemId = "misc-quick-item-hotkeys:picker-open:" + index.ToString(CultureInfo.InvariantCulture);
            var itemElementRect = itemHit.Width > 0 && itemHit.Height > 0 ? itemHit : itemButtonRect;
            var itemAreaHovered = mouse != null && itemElementRect.Contains(mouse.X, mouse.Y);
            var itemHovered = IsFrameElementHovered(itemId, itemElementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, itemButtonRect, itemAreaHovered, itemAreaHovered && mouse.LeftDown, false, true, clip);

            var iconRect = new LegacyUiRect(itemButtonRect.X + 4, itemButtonRect.Y + Math.Max(0, (itemButtonRect.Height - QuickItemIconCellSize) / 2), QuickItemIconCellSize, QuickItemIconCellSize);
            LegacyUiTheme.DrawCellClipped(spriteBatch, iconRect, itemAreaHovered, false, false, clip);
            if (itemType > 0)
            {
                object texture;
                if (VanillaUiSkinCompat.TryGetItemTexture(itemType, out texture))
                {
                    UiPrimitiveRenderer.DrawTextureContainedClipped(spriteBatch, texture, iconRect.X + 2, iconRect.Y + 2, iconRect.Width - 4, iconRect.Height - 4, clip.X, clip.Y, clip.Width, clip.Height, 255, 255, 255, 255);
                }
                else
                {
                    UiTextRenderer.DrawCenteredTextClipped(spriteBatch, itemType.ToString(CultureInfo.InvariantCulture), iconRect.X + 1, iconRect.Y + 5, iconRect.Width - 2, 12, clip.X, clip.Y, clip.Width, clip.Height, 232, 236, 220, 255, 0.46f);
                }
            }
            else
            {
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "+", iconRect.X + 1, iconRect.Y + 2, iconRect.Width - 2, iconRect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, 232, 236, 220, 255, 0.84f);
            }

            var displayLabel = BuildQuickItemBindingDisplayLabel(binding, itemType);
            var labelRect = new LegacyUiRect(iconRect.Right + 4, itemButtonRect.Y, Math.Max(1, itemButtonRect.Width - (iconRect.Width + 12)), itemButtonRect.Height);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                displayLabel,
                labelRect.X,
                labelRect.Y,
                labelRect.Width,
                labelRect.Height,
                UiTextHorizontalAlignment.Left,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                226,
                230,
                216,
                242,
                0.56f);

            var itemElement = AddFrameElement(elements, itemId, "快捷物品:选择物品", "button", itemElementRect, tooltipLines: new[] { itemType > 0 ? "点击修改该快捷物品。" : "点击从背包选择物品。" });
            RecordFrameElementHover(itemElement, itemHovered);
            var hovered = itemHovered ? itemElement : null;

            if (itemAreaHovered)
            {
                var deleteRect = new LegacyUiRect(itemButtonRect.Right - 15, itemButtonRect.Y + 1, 14, 14);
                var deleteHit = deleteRect.Intersect(clip);
                var deleteId = "misc-quick-item-hotkeys:remove:" + index.ToString(CultureInfo.InvariantCulture);
                var deleteElementRect = deleteHit.Width > 0 && deleteHit.Height > 0 ? deleteHit : deleteRect;
                var deleteHovered = IsFrameElementHovered(deleteId, deleteElementRect, mouse);
                LegacyUiTheme.DrawButtonClipped(spriteBatch, deleteRect, deleteHovered, deleteHovered && mouse.LeftDown, false, true, clip);
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "x", deleteRect.X + 1, deleteRect.Y, deleteRect.Width - 2, deleteRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 238, 196, 180, 255, 0.64f);
                var deleteElement = AddFrameElement(elements, deleteId, "删除快捷物品条目", "button", deleteElementRect, tooltipLines: new[] { "删除这一条快捷物品配置。" });
                RecordFrameElementHover(deleteElement, deleteHovered);
                if (deleteHovered)
                {
                    hovered = deleteElement;
                }
            }

            var hotkeyHit = hotkeyRect.Intersect(clip);
            var hotkeyId = "misc-quick-item-hotkeys:capture-start:" + index.ToString(CultureInfo.InvariantCulture);
            var hotkeyElementRect = hotkeyHit.Width > 0 && hotkeyHit.Height > 0 ? hotkeyHit : hotkeyRect;
            var hotkeyHovered = IsFrameElementHovered(hotkeyId, hotkeyElementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, hotkeyRect, hotkeyHovered, hotkeyHovered && mouse.LeftDown, captureSelected, true, clip);
            var hotkeyContentRect = LegacyUiTheme.GetSelectedButtonContentRect(hotkeyRect, captureSelected, true);
            var hotkeyScale = hotkeyText.Length >= 12
                ? 0.52f
                : hotkeyText.Length >= 10
                    ? 0.56f
                    : hotkeyText.Length >= 8
                        ? 0.60f
                        : 0.68f;
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, hotkeyText, hotkeyRect.X + 4, hotkeyContentRect.Y, hotkeyRect.Width - 8, hotkeyContentRect.Height, clip.X, clip.Y, clip.Width, clip.Height, captureSelected ? LegacyUiTheme.SelectedTextR : 236, captureSelected ? LegacyUiTheme.SelectedTextG : 234, captureSelected ? LegacyUiTheme.SelectedTextB : 220, 255, hotkeyScale);
            if (captureSelected)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(hotkeyRect.X + 4, hotkeyContentRect.Y, hotkeyRect.Width - 8, hotkeyContentRect.Height), clip, hotkeyText, hotkeyScale);
            }

            if (!captureSelected &&
                hotkeyHovered &&
                !string.IsNullOrWhiteSpace(unifiedHotkey))
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, "编辑", hotkeyRect.X + 4, hotkeyRect.Y + 2, hotkeyRect.Width - 8, 14, clip.X, clip.Y, clip.Width, clip.Height, 238, 196, 156, 248, 0.52f);
            }

            var hotkeyElement = AddFrameElement(elements, hotkeyId, "编辑快捷键", "button", hotkeyElementRect, selected: captureSelected, tooltipLines: new[] { "点击后按下快捷键组合（支持 Ctrl / Alt / Shift + 键，Backspace 删除）。" });
            RecordFrameElementHover(hotkeyElement, hotkeyHovered);
            if (hotkeyHovered)
            {
                hovered = hotkeyElement;
            }

            return hovered;
        }

        private static LegacyUiElement DrawQuickItemInventoryPickerPanel(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, List<QuickItemInventoryCandidate> candidates, List<QuickItemHotkeyBinding> bindings)
        {
            var bindingIndex = _quickItemPickerBindingIndex;
            var selectedItemType = bindingIndex >= 0 && bindings != null && bindingIndex < bindings.Count
                ? GetQuickItemBindingPrimaryItemType(bindings[bindingIndex])
                : 0;
            return DrawMiscInventoryIconPickerPanel(
                spriteBatch,
                area,
                mouse,
                elements,
                rect,
                candidates,
                new MiscInventoryIconPickerOptions
                {
                    Title = "点击选择物品",
                    CloseId = "misc-quick-item-hotkeys:picker-close",
                    CloseLabel = "关闭物品选择",
                    CloseTooltip = "关闭物品选择窗口。",
                    EmptyText = "背包里没有可用物品。",
                    SelectIdPrefix = "misc-quick-item-hotkeys:picker-select:",
                    SelectLabel = "选择快捷物品",
                    TooltipPrefix = "选择：",
                    TargetIndex = bindingIndex,
                    IsSelected = candidate => selectedItemType > 0 && candidate.ItemType == selectedItemType
                });
        }
    }
}
