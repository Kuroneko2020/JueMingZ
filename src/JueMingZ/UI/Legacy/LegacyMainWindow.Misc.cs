using System;
using System.Collections.Generic;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawMiscPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var y = 0;

            hovered = DrawQuickItemHotkeysRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            int quickItemPanelHeight;
            hovered = DrawQuickItemHotkeysPanel(spriteBatch, area, mouse, elements, y + LegacyUiMetrics.RowHeight, out quickItemPanelHeight) ?? hovered;
            y += MiscExpandableRowHeight(quickItemPanelHeight);
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动堆叠", settings.InventoryAutoStackEnabled, "misc-auto-stack-mode:", "尝试堆叠刚捡起的物品") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawAutoSellRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            int autoSellPanelHeight;
            hovered = DrawAutoSellListPanel(spriteBatch, area, mouse, elements, y + LegacyUiMetrics.RowHeight, out autoSellPanelHeight) ?? hovered;
            y += MiscExpandableRowHeight(autoSellPanelHeight);
            hovered = DrawAutoDiscardRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            int autoDiscardPanelHeight;
            hovered = DrawAutoDiscardListPanel(spriteBatch, area, mouse, elements, y + LegacyUiMetrics.RowHeight, out autoDiscardPanelHeight) ?? hovered;
            y += MiscExpandableRowHeight(autoDiscardPanelHeight);
            hovered = DrawQuickReforgeRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            int quickReforgePanelHeight;
            hovered = DrawQuickReforgeListPanel(spriteBatch, area, mouse, elements, y + LegacyUiMetrics.RowHeight, out quickReforgePanelHeight) ?? hovered;
            y += MiscExpandableRowHeight(quickReforgePanelHeight);
            hovered = DrawAutoMiningRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                y,
                "自动捕捉",
                AutoCaptureCritterModes.Normalize(settings.WorldAutomationAutoCaptureCritterMode),
                new[] { "自动", "手持", "关闭" },
                new[] { AutoCaptureCritterModes.Auto, AutoCaptureCritterModes.Manual, AutoCaptureCritterModes.Off },
                "misc-auto-capture-critter-mode:",
                new[] { "身上带着虫网就行", "必须手持虫网", null }) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动收获", settings.WorldAutomationAutoHarvestEnabled, "misc-auto-harvest-mode:", "携带再生法杖自动收获/种植") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "持续开袋", settings.InventoryQuickBagOpenEnabled, "misc-quick-bag-open-mode:", "按住shift长按右键点击匣子快速打开") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动存钱", settings.InventoryAutoDepositCoinsEnabled, "misc-auto-deposit-coins-mode:", "靠近容器主动存放货币") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动提炼", settings.InventoryAutoExtractinatorEnabled, "misc-auto-extractinator-mode:", "靠近提炼机尝试自动提炼") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "保持收藏", settings.InventoryKeepFavoritedEnabled, "misc-keep-favorited-mode:", "让收藏的物品保持状态") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "旅行菜单", settings.WorldAutomationTravelMenuEnabled, "misc-travel-menu-mode:", "单机临时开启原版旅行菜单") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawWorldGenerationDetailsRow(spriteBatch, area, mouse, elements, y) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawDeveloperEasterEggRow(spriteBatch, area, mouse, elements, y) ?? hovered;
            return hovered;
        }

        private static int MiscExpandableRowHeight(int panelHeight)
        {
            return LegacyUiMetrics.RowHeight + Math.Max(0, panelHeight) + LegacyUiMetrics.SettingRowGap;
        }

        private static LegacyUiElement DrawWorldGenerationDetailsRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY)
        {
            var alternate = IsWorldGenerationDetailsHintAlternate();
            return DrawSingleMiscActionRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "生成世界明细",
                alternate ? "暂时关不掉" : "世界生成读条界面按F5开启",
                alternate ? "locked" : "hint",
                "misc-world-generation-details:",
                230,
                232,
                224);
        }

        private static LegacyUiElement DrawDeveloperEasterEggRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY)
        {
            var pending = _developerEasterEggConfirmPending;
            var actionLabel = pending ? "有坏档的风险慎用" : "开启";
            var actionValue = pending ? "confirm" : "open";

            return DrawSingleMiscActionRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "开发者菜单",
                actionLabel,
                actionValue,
                "misc-developer-easter-egg:",
                pending ? 238 : 230,
                pending ? 82 : 232,
                pending ? 72 : 224);
        }

        private static LegacyUiElement DrawSingleMiscActionRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string label, string buttonLabel, string buttonValue, string elementPrefix, int textR, int textG, int textB)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);

            var buttonWidth = Math.Min(Math.Max(118, UiTextRenderer.EstimateTextWidth(buttonLabel, 0.72f) + 20), Math.Max(118, row.Width - 150));
            var buttonX = row.Right - buttonWidth - 10;
            var labelWidth = Math.Max(60, buttonX - row.X - 20);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, label, row.X + 10, row.Y, labelWidth, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);

            var rect = new LegacyUiRect(buttonX, RowModeButtonY(row), buttonWidth, RowModeButtonHeight);
            var hit = rect.Intersect(area.Viewport);
            var isHovered = hit.Width > 0 && hit.Height > 0 && hit.Contains(mouse.X, mouse.Y);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, isHovered, isHovered && mouse.LeftDown, false, true, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, buttonLabel, rect.X + 4, rect.Y, rect.Width - 8, rect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, textR, textG, textB, 255, ResolveSingleMiscButtonScale(buttonLabel, rect.Width - 8));

            var element = new LegacyUiElement
            {
                Id = elementPrefix + buttonValue,
                Label = label + ":" + buttonLabel,
                Kind = "button",
                Rect = hit.Width > 0 && hit.Height > 0 ? hit : rect,
                Selected = false,
                TooltipLines = null
            };
            elements.Add(element);
            return isHovered ? element : null;
        }

        private static float ResolveSingleMiscButtonScale(string text, int availableWidth)
        {
            var scale = 0.72f;
            if (availableWidth <= 0 || string.IsNullOrWhiteSpace(text))
            {
                return scale;
            }

            var estimatedWidth = UiTextRenderer.EstimateTextWidth(text, scale);
            if (estimatedWidth <= availableWidth)
            {
                return scale;
            }

            return Math.Max(0.58f, scale * availableWidth / Math.Max(1, estimatedWidth));
        }
    }
}
