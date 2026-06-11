using System;
using System.Collections.Generic;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const string MapQuickAnnouncementKeyboardSlotTooltip = "双击进行改键，不支持鼠标按键";
        private const string MapQuickAnnouncementTriggerSlotTooltip = "双击进行改键，支持鼠标按键";
        private const string MapQuickAnnouncementOnTooltip = "按下快捷键对鼠标位置内容进行广播";
        private const string MapQuickAnnouncementOffTooltip = "";
        internal const string MapEnhancementFuturePlaceholderText = "更多功能正在开发中";

        private static LegacyUiElement DrawMapEnhancementPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            UpdateMapQuickAnnouncementHotkeyCapture(settings);

            hovered = DrawMapQuickAnnouncementRow(spriteBatch, area, mouse, elements, 0, settings) ?? hovered;
            DrawMapEnhancementFuturePlaceholder(spriteBatch, area, LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap);
            return hovered;
        }

        private static void DrawMapEnhancementFuturePlaceholder(object spriteBatch, LegacyScrollArea area, int contentY)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                MapEnhancementFuturePlaceholderText,
                row.X + 10,
                row.Y,
                Math.Max(1, row.Width - 20),
                row.Height,
                UiTextHorizontalAlignment.Left,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                205,
                218,
                238,
                235,
                0.82f);
        }

        private static LegacyUiElement DrawMapQuickAnnouncementRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            settings = settings ?? AppSettings.CreateDefault();
            var hotkey = MapQuickAnnouncementSettings.NormalizeHotkey(
                settings.MapQuickAnnouncementHotkeySlot1,
                settings.MapQuickAnnouncementHotkeySlot2,
                settings.MapQuickAnnouncementTriggerKey);
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings);
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                "快捷宣告",
                row.X + 10,
                row.Y,
                92,
                row.Height,
                UiTextHorizontalAlignment.Left,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                238,
                238,
                226,
                255,
                0.86f);

            const int gap = 6;
            const int switchWidth = 54;
            var buttonY = RowModeButtonY(row);
            var available = Math.Max(1, row.Width - 120);
            var slotWidth = Math.Max(58, Math.Min(72, (available - switchWidth * 2 - gap * 4) / 3));
            var totalWidth = slotWidth * 3 + switchWidth * 2 + gap * 4;
            var x = row.Right - totalWidth - 10;
            var hovered = (LegacyUiElement)null;

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                new LegacyUiRect(x, buttonY, slotWidth, RowModeButtonHeight),
                "map-quick-announcement-key:1",
                "快捷宣告:前置键1",
                BuildMapQuickAnnouncementKeyText(hotkey.Slot1, MapQuickAnnouncementSettings.HotkeySlot1Id),
                IsMapQuickAnnouncementHotkeyCaptureSlot(MapQuickAnnouncementSettings.HotkeySlot1Id),
                MapQuickAnnouncementKeyboardSlotTooltip) ?? hovered;
            x += slotWidth + gap;

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                new LegacyUiRect(x, buttonY, slotWidth, RowModeButtonHeight),
                "map-quick-announcement-key:2",
                "快捷宣告:前置键2",
                BuildMapQuickAnnouncementKeyText(hotkey.Slot2, MapQuickAnnouncementSettings.HotkeySlot2Id),
                IsMapQuickAnnouncementHotkeyCaptureSlot(MapQuickAnnouncementSettings.HotkeySlot2Id),
                MapQuickAnnouncementKeyboardSlotTooltip) ?? hovered;
            x += slotWidth + gap;

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                new LegacyUiRect(x, buttonY, slotWidth, RowModeButtonHeight),
                "map-quick-announcement-key:trigger",
                "快捷宣告:触发键",
                BuildMapQuickAnnouncementKeyText(hotkey.TriggerKey, MapQuickAnnouncementSettings.HotkeyTriggerId),
                IsMapQuickAnnouncementHotkeyCaptureSlot(MapQuickAnnouncementSettings.HotkeyTriggerId),
                MapQuickAnnouncementTriggerSlotTooltip) ?? hovered;
            x += slotWidth + gap;

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                new LegacyUiRect(x, buttonY, switchWidth, RowModeButtonHeight),
                "map-quick-announcement-mode:On",
                "快捷宣告:开启",
                "开启",
                settings.MapQuickAnnouncementEnabled,
                MapQuickAnnouncementOnTooltip) ?? hovered;
            x += switchWidth + gap;

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                new LegacyUiRect(x, buttonY, switchWidth, RowModeButtonHeight),
                "map-quick-announcement-mode:Off",
                "快捷宣告:关闭",
                "关闭",
                !settings.MapQuickAnnouncementEnabled,
                MapQuickAnnouncementOffTooltip) ?? hovered;

            return hovered;
        }

        private static LegacyUiElement DrawMapQuickAnnouncementKeyButton(
            LegacyUiContext context,
            LegacyUiRect clip,
            LegacyUiRect rect,
            string id,
            string label,
            string text,
            bool selected,
            string tooltip)
        {
            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var button = new LegacyButtonControl
            {
                Id = id,
                Label = label,
                Text = text,
                ElementLabel = label,
                Kind = "button",
                Bounds = elementRect,
                Selected = selected,
                TextScale = ResolveMapQuickAnnouncementKeyTextScale(text, elementRect.Width - 8),
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            };
            var element = button.Draw(context);
            return element != null && context.IsElementHovered(id, elementRect) ? element : null;
        }

        private static string BuildMapQuickAnnouncementKeyText(string token, string slotId = "")
        {
            if (IsMapQuickAnnouncementHotkeyCaptureSlot(slotId))
            {
                return "录入";
            }

            var display = MapQuickAnnouncementSettings.DisplayKey(token);
            return string.IsNullOrWhiteSpace(display) ? "空" : display;
        }

        private static bool IsMapQuickAnnouncementHotkeyCaptureSlot(string slotId)
        {
            var normalized = MapQuickAnnouncementSettings.NormalizeHotkeySlotId(slotId);
            return normalized.Length > 0 &&
                   string.Equals(_mapQuickAnnouncementHotkeyCaptureSlot, normalized, StringComparison.Ordinal);
        }

        private static float ResolveMapQuickAnnouncementKeyTextScale(string text, int availableWidth)
        {
            var scale = 0.72f;
            if (availableWidth <= 0 || string.IsNullOrWhiteSpace(text))
            {
                return scale;
            }

            var width = UiTextRenderer.EstimateTextWidth(text, scale);
            return width <= availableWidth ? scale : Math.Max(0.56f, scale * availableWidth / Math.Max(1, width));
        }

        internal static string BuildMapQuickAnnouncementHotkeyDisplayForTesting(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var hotkey = MapQuickAnnouncementSettings.NormalizeHotkey(
                settings.MapQuickAnnouncementHotkeySlot1,
                settings.MapQuickAnnouncementHotkeySlot2,
                settings.MapQuickAnnouncementTriggerKey);
            return BuildMapQuickAnnouncementKeyText(hotkey.Slot1) + "|" +
                   BuildMapQuickAnnouncementKeyText(hotkey.Slot2) + "|" +
                   BuildMapQuickAnnouncementKeyText(hotkey.TriggerKey);
        }

        internal static string[] GetMapQuickAnnouncementButtonTooltipsForTesting()
        {
            return new[]
            {
                MapQuickAnnouncementKeyboardSlotTooltip,
                MapQuickAnnouncementKeyboardSlotTooltip,
                MapQuickAnnouncementTriggerSlotTooltip,
                MapQuickAnnouncementOnTooltip,
                MapQuickAnnouncementOffTooltip
            };
        }
    }
}
