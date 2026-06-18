using System.Collections.Generic;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawItemsPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var y = 0;
            _autoCaptureCritterConfigAnchorVisible = false;

            hovered = DrawQuickItemHotkeysRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            int quickItemPanelHeight;
            hovered = DrawQuickItemHotkeysPanel(spriteBatch, area, mouse, elements, y + LegacyUiMetrics.RowHeight, out quickItemPanelHeight) ?? hovered;
            y += MiscExpandableRowHeight(quickItemPanelHeight);
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动堆叠", settings.InventoryAutoStackEnabled, "misc-auto-stack-mode:", "尝试堆叠刚捡起的物品", featureToggleTargetId: "inventory.auto_stack") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawAutoSellRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            int autoSellPanelHeight;
            hovered = DrawAutoSellListPanel(spriteBatch, area, mouse, elements, y + LegacyUiMetrics.RowHeight, out autoSellPanelHeight) ?? hovered;
            y += MiscExpandableRowHeight(autoSellPanelHeight);
            hovered = DrawAutoDiscardRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            int autoDiscardPanelHeight;
            hovered = DrawAutoDiscardListPanel(spriteBatch, area, mouse, elements, y + LegacyUiMetrics.RowHeight, out autoDiscardPanelHeight) ?? hovered;
            y += MiscExpandableRowHeight(autoDiscardPanelHeight);
            hovered = DrawAutoCaptureCritterRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            RegisterAutoCaptureCritterConfigPopupOverlay(area, settings);
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动收获", settings.WorldAutomationAutoHarvestEnabled, "misc-auto-harvest-mode:", "携带再生法杖自动收获/种植", featureToggleTargetId: "automation.auto_harvest") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "持续开袋", settings.InventoryQuickBagOpenEnabled, "misc-quick-bag-open-mode:", "按住shift长按右键点击匣子快速打开", featureToggleTargetId: "inventory.continuous_bag_open") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动存钱", settings.InventoryAutoDepositCoinsEnabled, "misc-auto-deposit-coins-mode:", "靠近容器主动存放货币", featureToggleTargetId: "inventory.auto_deposit_coins") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动提炼", settings.InventoryAutoExtractinatorEnabled, "misc-auto-extractinator-mode:", "靠近提炼机尝试自动提炼", featureToggleTargetId: "inventory.auto_extractinator") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "保持收藏", settings.InventoryKeepFavoritedEnabled, "misc-keep-favorited-mode:", "让收藏的物品保持状态", featureToggleTargetId: "inventory.keep_favorited") ?? hovered;
            return hovered;
        }
    }
}
