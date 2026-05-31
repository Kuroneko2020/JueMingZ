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
            LegacyUiTheme.DrawRowClipped(spriteBatch, card, clip);
            var captureSelected = _quickItemHotkeyCaptureActive && _quickItemHotkeyCaptureBindingIndex == index;
            var hotkeyText = captureSelected
                ? "按键中..."
                : string.IsNullOrWhiteSpace(binding == null ? null : binding.Hotkey) ? "+" : binding.Hotkey.Trim();
            var hotkeyWidth = ResolveQuickItemHotkeyWidth(hotkeyText, captureSelected, card.Width);
            var buttonHeight = Math.Max(RowModeButtonHeight, card.Height - 6);
            var buttonY = card.Y + Math.Max(0, (card.Height - buttonHeight) / 2);
            var hotkeyRect = new LegacyUiRect(card.Right - hotkeyWidth - 4, buttonY, hotkeyWidth, buttonHeight);
            var itemButtonRect = new LegacyUiRect(card.X + 4, buttonY, Math.Max(1, hotkeyRect.X - card.X - 8), buttonHeight);
            var itemType = GetQuickItemBindingPrimaryItemType(binding);
            var itemHit = itemButtonRect.Intersect(clip);
            var itemHovered = itemHit.Width > 0 && itemHit.Height > 0 && itemHit.Contains(mouse.X, mouse.Y);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, itemButtonRect, itemHovered, itemHovered && mouse.LeftDown, false, true, clip);

            var iconRect = new LegacyUiRect(itemButtonRect.X + 4, itemButtonRect.Y + Math.Max(0, (itemButtonRect.Height - QuickItemIconCellSize) / 2), QuickItemIconCellSize, QuickItemIconCellSize);
            LegacyUiTheme.DrawCellClipped(spriteBatch, iconRect, itemHovered, false, false, clip);
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

            var itemElement = new LegacyUiElement
            {
                Id = "misc-quick-item-hotkeys:picker-open:" + index.ToString(CultureInfo.InvariantCulture),
                Label = "快捷物品:选择物品",
                Kind = "button",
                Rect = itemHit.Width > 0 && itemHit.Height > 0 ? itemHit : itemButtonRect,
                TooltipLines = new[] { itemType > 0 ? "点击修改该快捷物品。" : "点击从背包选择物品。" }
            };
            elements.Add(itemElement);
            var hovered = itemHovered ? itemElement : null;

            if (itemType > 0 && itemHovered)
            {
                var deleteRect = new LegacyUiRect(itemButtonRect.Right - 15, itemButtonRect.Y + 1, 14, 14);
                var deleteHit = deleteRect.Intersect(clip);
                var deleteHovered = deleteHit.Width > 0 && deleteHit.Height > 0 && deleteHit.Contains(mouse.X, mouse.Y);
                LegacyUiTheme.DrawButtonClipped(spriteBatch, deleteRect, deleteHovered, deleteHovered && mouse.LeftDown, false, true, clip);
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "x", deleteRect.X + 1, deleteRect.Y, deleteRect.Width - 2, deleteRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 238, 196, 180, 255, 0.64f);
                var deleteElement = new LegacyUiElement
                {
                    Id = "misc-quick-item-hotkeys:remove:" + index.ToString(CultureInfo.InvariantCulture),
                    Label = "删除快捷物品条目",
                    Kind = "button",
                    Rect = deleteHit.Width > 0 && deleteHit.Height > 0 ? deleteHit : deleteRect,
                    TooltipLines = new[] { "删除这一条快捷物品配置。" }
                };
                elements.Add(deleteElement);
                if (deleteHovered)
                {
                    hovered = deleteElement;
                }
            }

            var hotkeyHit = hotkeyRect.Intersect(clip);
            var hotkeyHovered = hotkeyHit.Width > 0 && hotkeyHit.Height > 0 && hotkeyHit.Contains(mouse.X, mouse.Y);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, hotkeyRect, hotkeyHovered, hotkeyHovered && mouse.LeftDown, captureSelected, true, clip);
            var hotkeyScale = hotkeyText.Length >= 12
                ? 0.52f
                : hotkeyText.Length >= 10
                    ? 0.56f
                    : hotkeyText.Length >= 8
                        ? 0.60f
                        : 0.68f;
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, hotkeyText, hotkeyRect.X + 4, hotkeyRect.Y, hotkeyRect.Width - 8, hotkeyRect.Height, clip.X, clip.Y, clip.Width, clip.Height, captureSelected ? LegacyUiTheme.SelectedTextR : 236, captureSelected ? LegacyUiTheme.SelectedTextG : 234, captureSelected ? LegacyUiTheme.SelectedTextB : 220, 255, hotkeyScale);
            if (captureSelected)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(hotkeyRect.X + 4, hotkeyRect.Y, hotkeyRect.Width - 8, hotkeyRect.Height), clip, hotkeyText, hotkeyScale);
            }

            if (!captureSelected &&
                hotkeyHovered &&
                !string.IsNullOrWhiteSpace(binding == null ? null : binding.Hotkey))
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, "编辑", hotkeyRect.X + 4, hotkeyRect.Y + 2, hotkeyRect.Width - 8, 14, clip.X, clip.Y, clip.Width, clip.Height, 238, 196, 156, 248, 0.52f);
            }

            var hotkeyElement = new LegacyUiElement
            {
                Id = "misc-quick-item-hotkeys:capture-start:" + index.ToString(CultureInfo.InvariantCulture),
                Label = "编辑快捷键",
                Kind = "button",
                Rect = hotkeyHit.Width > 0 && hotkeyHit.Height > 0 ? hotkeyHit : hotkeyRect,
                Selected = captureSelected,
                TooltipLines = new[] { "点击后按下快捷键组合（支持 Ctrl / Alt / Shift + 键）。" }
            };
            elements.Add(hotkeyElement);
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
            var title = bindingIndex >= 0
                ? "背包物品选择（第 " + (bindingIndex + 1).ToString(CultureInfo.InvariantCulture) + " 条）"
                : "背包物品选择";
            return DrawMiscInventoryPickerPanel(
                spriteBatch,
                area,
                mouse,
                elements,
                rect,
                candidates,
                bindingIndex,
                selectedItemType,
                new MiscInventoryPickerOptions
                {
                    Title = title,
                    CloseId = "misc-quick-item-hotkeys:picker-close",
                    CloseLabel = "关闭物品选择",
                    CloseTooltip = "关闭物品选择窗口。",
                    EmptyText = "背包里没有可用物品。",
                    SelectIdPrefix = "misc-quick-item-hotkeys:picker-select:",
                    SelectLabel = "选择快捷物品",
                    SelectTooltipPrefix = "选择："
                });
        }
    }
}
