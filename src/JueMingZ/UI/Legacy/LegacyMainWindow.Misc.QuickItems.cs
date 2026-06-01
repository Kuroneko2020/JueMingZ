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
        private static LegacyUiElement DrawQuickItemHotkeysRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            var enabled = settings != null && settings.InventoryQuickItemHotkeysEnabled;
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "快捷物品",
                enabled ? "On" : "Off",
                new[] { "添加", "开启", "关闭" },
                new[] { "add-empty", "On", "Off" },
                "misc-quick-item-hotkeys-row:",
                new[]
                {
                    "新增一条快捷物品配置。",
                    "启用快捷物品功能。",
                    "关闭快捷物品功能。"
                });
        }

        private static LegacyUiElement DrawQuickItemHotkeysPanel(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, out int consumedHeight)
        {
            var bindings = GetQuickItemBindings();
            if (bindings.Count <= 0)
            {
                CloseQuickItemPicker();
                StopQuickItemHotkeyCapture();
                consumedHeight = 0;
                return null;
            }

            if (_quickItemPickerOpen &&
                (_quickItemPickerBindingIndex < 0 || _quickItemPickerBindingIndex >= bindings.Count))
            {
                CloseQuickItemPicker();
            }

            if (_quickItemHotkeyCaptureActive &&
                (_quickItemHotkeyCaptureBindingIndex < 0 || _quickItemHotkeyCaptureBindingIndex >= bindings.Count))
            {
                StopQuickItemHotkeyCapture();
            }

            UpdateQuickItemHotkeyCapture(bindings);

            List<QuickItemInventoryCandidate> pickerCandidates = null;
            if (_quickItemPickerOpen)
            {
                pickerCandidates = GetQuickItemPickerCandidates();
            }

            var pickerCandidateCount = pickerCandidates == null ? 0 : pickerCandidates.Count;
            consumedHeight = CalculateQuickItemPanelHeight(area.Viewport.Width, bindings.Count, _quickItemHotkeyCaptureActive, _quickItemPickerOpen, pickerCandidateCount);
            DrawSection(spriteBatch, area, contentY, "快捷物品列表");
            var cardsContentY = contentY + LegacyUiMetrics.SectionHeaderHeight;
            var cardsHeight = CalculateQuickItemCardsBodyHeight(area.Viewport.Width, bindings.Count);
            var cardsRect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(cardsContentY), area.Viewport.Width, cardsHeight);
            var clip = area.Viewport;
            var hovered = (LegacyUiElement)null;

            if (area.IsVisible(cardsRect))
            {
                LegacyUiTheme.DrawSubPanelClipped(spriteBatch, cardsRect, clip);
                if (bindings.Count <= 0)
                {
                    UiTextRenderer.DrawTextClipped(
                        spriteBatch,
                        "暂无快捷物品。点击上方“添加”创建一条空位配置。",
                        cardsRect.X + 10,
                        cardsRect.Y + 13,
                        cardsRect.Width - 20,
                        20,
                        clip.X,
                        clip.Y,
                        clip.Width,
                        clip.Height,
                        206,
                        214,
                        228,
                        240,
                        0.66f);
                }
                else
                {
                    int columns;
                    int rows;
                    int cardWidth;
                    ComputeQuickItemCardLayout(area.Viewport.Width, bindings.Count, out columns, out rows, out cardWidth);
                    for (var index = 0; index < bindings.Count; index++)
                    {
                        var rowIndex = index / columns;
                        var columnIndex = index % columns;
                        var card = new LegacyUiRect(
                            cardsRect.X + 8 + columnIndex * (cardWidth + QuickItemCardGap),
                            cardsRect.Y + 8 + rowIndex * (QuickItemCardHeight + QuickItemCardGap),
                            cardWidth,
                            QuickItemCardHeight);
                        if (!card.Intersects(clip))
                        {
                            continue;
                        }

                        hovered = DrawQuickItemBindingCard(spriteBatch, mouse, elements, clip, bindings[index], index, card) ?? hovered;
                    }
                }
            }

            var nextContentY = cardsContentY + cardsHeight;
            if (_quickItemHotkeyCaptureActive)
            {
                var captureRect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(nextContentY), area.Viewport.Width, QuickItemCaptureHintHeight);
                if (area.IsVisible(captureRect))
                {
                    LegacyUiTheme.DrawRowClipped(spriteBatch, captureRect, clip);
                    var status = _quickItemHotkeyCaptureBindingIndex < 0
                        ? "请按下快捷键组合（支持 Ctrl / Alt / Shift + 键）"
                        : "正在录入第 " + (_quickItemHotkeyCaptureBindingIndex + 1).ToString(CultureInfo.InvariantCulture) + " 条快捷键（Esc 取消）";
                    UiTextRenderer.DrawTextClipped(spriteBatch, status, captureRect.X + 10, captureRect.Y + 5, captureRect.Width - 90, captureRect.Height - 4, clip.X, clip.Y, clip.Width, clip.Height, 236, 230, 206, 246, 0.66f);

                    var cancelRect = new LegacyUiRect(captureRect.Right - 72, captureRect.Y + 3, 64, 22);
                    var cancelHit = cancelRect.Intersect(clip);
                    var cancelElementRect = cancelHit.Width > 0 && cancelHit.Height > 0 ? cancelHit : cancelRect;
                    var cancelHovered = IsFrameElementHovered("misc-quick-item-hotkeys:capture-stop", cancelElementRect, mouse);
                    LegacyUiTheme.DrawButtonClipped(spriteBatch, cancelRect, cancelHovered, cancelHovered && mouse.LeftDown, false, true, clip);
                    UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "取消", cancelRect.X + 2, cancelRect.Y, cancelRect.Width - 4, cancelRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 230, 232, 224, 255, 0.70f);
                    var cancelElement = AddFrameElement(elements, "misc-quick-item-hotkeys:capture-stop", "取消快捷键录入", "button", cancelElementRect, tooltipLines: new[] { "取消当前快捷键录入。" });
                    RecordFrameElementHover(cancelElement, cancelHovered);
                    if (cancelHovered)
                    {
                        hovered = cancelElement;
                    }
                }

                nextContentY += QuickItemCaptureHintHeight + 6;
            }

            if (_quickItemPickerOpen)
            {
                var pickerHeight = CalculateAutoItemPickerPanelHeight(area.Viewport.Width, pickerCandidateCount);
                var pickerRect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(nextContentY), area.Viewport.Width, pickerHeight);
                hovered = DrawQuickItemInventoryPickerPanel(spriteBatch, area, mouse, elements, pickerRect, pickerCandidates, bindings) ?? hovered;
            }

            return hovered;
        }

    }
}
