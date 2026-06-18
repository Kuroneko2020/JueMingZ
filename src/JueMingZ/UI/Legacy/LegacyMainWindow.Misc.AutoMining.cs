using System;
using System.Collections.Generic;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawAutoMiningRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            UpdateAutoMiningHotkeyCapture();

            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var mode = AutoMiningModes.Normalize(settings == null ? null : settings.WorldAutomationAutoMiningMode);
            var hotkey = GetAutoMiningHotkeyDisplay();
            var inputText = _autoMiningHotkeyCaptureActive
                ? "按下采集按键..."
                : (string.IsNullOrWhiteSpace(hotkey) ? "双击采集按键" : hotkey);

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, "自动挖矿", row.X + 10, row.Y, 92, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);

            var gap = 6;
            var buttonY = RowModeButtonY(row);
            var offWidth = ModeButtonWidth("关闭");
            var autoWidth = ModeButtonWidth("自动");
            var hotkeyWidth = ModeButtonWidth("快捷键");
            const string targetId = "automation.auto_mining";
            var offRect = new LegacyUiRect(row.Right - offWidth - 10 - GetFeatureToggleHotkeyReserveWidth(targetId), buttonY, offWidth, RowModeButtonHeight);
            var autoRect = new LegacyUiRect(offRect.X - gap - autoWidth, buttonY, autoWidth, RowModeButtonHeight);
            var hotkeyRect = new LegacyUiRect(autoRect.X - gap - hotkeyWidth, buttonY, hotkeyWidth, RowModeButtonHeight);
            var inputX = row.X + 106;
            var inputWidth = Math.Max(86, hotkeyRect.X - inputX - gap);
            var inputRect = new LegacyUiRect(inputX, buttonY, inputWidth, RowModeButtonHeight);

            var hovered = (LegacyUiElement)null;
            hovered = DrawAutoMiningHotkeyInput(spriteBatch, mouse, elements, area.Viewport, inputRect, inputText) ?? hovered;
            hovered = DrawAutoMiningModeButton(spriteBatch, mouse, elements, area.Viewport, hotkeyRect, "Hotkey", "快捷键", string.Equals(mode, AutoMiningModes.Hotkey, StringComparison.Ordinal), "光标指到矿物上按快捷键选中挖矿区域") ?? hovered;
            hovered = DrawAutoMiningModeButton(spriteBatch, mouse, elements, area.Viewport, autoRect, "Auto", "自动", string.Equals(mode, AutoMiningModes.Auto, StringComparison.Ordinal), "挖下第一个矿物开始接管") ?? hovered;
            hovered = DrawAutoMiningModeButton(spriteBatch, mouse, elements, area.Viewport, offRect, "Off", "关闭", string.Equals(mode, AutoMiningModes.Off, StringComparison.Ordinal), "关闭自动挖矿。") ?? hovered;
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings);
            hovered = DrawFeatureToggleHotkeyButton(context, row, targetId) ?? hovered;
            return context.HoveredElement ?? hovered;
        }

        private static LegacyUiElement DrawAutoMiningHotkeyInput(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, LegacyUiRect rect, string text)
        {
            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered("misc-auto-mining:hotkey", elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, _autoMiningHotkeyCaptureActive, true, clip);
            var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, _autoMiningHotkeyCaptureActive, true);
            var scale = ResolveAutoMiningInputScale(text, rect.Width - 16);
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
                _autoMiningHotkeyCaptureActive ? 255 : 230,
                _autoMiningHotkeyCaptureActive ? 245 : 232,
                _autoMiningHotkeyCaptureActive ? 205 : 224,
                255,
                scale);
            if (_autoMiningHotkeyCaptureActive)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(
                    spriteBatch,
                    new LegacyUiRect(rect.X + 8, contentRect.Y + 3, rect.Width - 16, Math.Max(1, contentRect.Height - 6)),
                    clip,
                    text ?? string.Empty,
                    scale);
            }

            var element = AddFrameElement(elements, "misc-auto-mining:hotkey", "自动挖矿:采集按键", "button", elementRect, selected: _autoMiningHotkeyCaptureActive, tooltipLines: new[] { "双击录入采集按键。", "Esc 取消录入。" });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawAutoMiningModeButton(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, LegacyUiRect rect, string value, string text, bool selected, string tooltip)
        {
            var hit = rect.Intersect(clip);
            var elementId = "misc-auto-mining-mode:" + value;
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, selected, true, clip);
            var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, selected, true);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, text, rect.X + 3, contentRect.Y, rect.Width - 6, contentRect.Height, clip.X, clip.Y, clip.Width, clip.Height, selected ? LegacyUiTheme.SelectedTextR : 230, selected ? LegacyUiTheme.SelectedTextG : 232, selected ? LegacyUiTheme.SelectedTextB : 224, 255, 0.78f);
            if (selected)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(rect.X + 3, contentRect.Y, rect.Width - 6, contentRect.Height), clip, text, 0.78f);
            }

            var element = AddFrameElement(elements, elementId, "自动挖矿:" + text, "button", elementRect, selected: selected, tooltipLines: string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static string GetAutoMiningHotkeyDisplay()
        {
            var hotkeys = ConfigService.HotkeySettings == null ? null : ConfigService.HotkeySettings.HotkeysByFeatureId;
            if (hotkeys == null)
            {
                return string.Empty;
            }

            string value;
            return hotkeys.TryGetValue(FeatureIds.WorldAutomationAutoMining, out value) ? (value ?? string.Empty).Trim() : string.Empty;
        }

        private static float ResolveAutoMiningInputScale(string text, int availableWidth)
        {
            var scale = 0.72f;
            if (availableWidth <= 0 || string.IsNullOrWhiteSpace(text))
            {
                return scale;
            }

            var width = UiTextRenderer.EstimateTextWidth(text, scale);
            return width <= availableWidth ? scale : Math.Max(0.56f, scale * availableWidth / Math.Max(1, width));
        }
    }
}
