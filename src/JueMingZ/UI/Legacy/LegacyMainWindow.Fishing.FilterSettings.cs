using System;
using System.Collections.Generic;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawFishingFilterSettingsPane(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect pane, AppSettings settings)
        {
            var modePaneHeight = Math.Min(FishingFilterModePaneHeight, Math.Max(74, pane.Height / 3));
            var modePane = new LegacyUiRect(pane.X, pane.Y, pane.Width, modePaneHeight);
            var specialPane = new LegacyUiRect(pane.X, modePane.Bottom + FishingFilterSidePaneGap, pane.Width, Math.Max(1, pane.Bottom - modePane.Bottom - FishingFilterSidePaneGap));
            var hovered = (LegacyUiElement)null;
            hovered = DrawFishingFilterModePane(spriteBatch, area, mouse, elements, modePane, settings.FishingFilterMode) ?? hovered;
            hovered = DrawFishingFilterSpecialPane(spriteBatch, area, mouse, elements, specialPane, settings) ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawFishingFilterModePane(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect pane, string mode)
        {
            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, pane, area.Viewport);
            var titleRect = new LegacyUiRect(pane.X + 6, pane.Y + 5, pane.Width - 12, BuffPaneTitleHeight);
            LegacyUiTheme.DrawSectionHeaderClipped(spriteBatch, titleRect, area.Viewport);
            UiTextRenderer.DrawTextClipped(spriteBatch, "过滤模式", titleRect.X + 8, titleRect.Y + 8, titleRect.Width - 16, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 244, 238, 210, 255, 0.78f);

            var normalized = FishingFilterModes.Normalize(mode);
            var buttonY = titleRect.Bottom + Math.Max(6, (pane.Bottom - titleRect.Bottom - RowModeButtonHeight) / 2);
            buttonY = Math.Min(buttonY, pane.Bottom - RowModeButtonHeight - 7);
            var nextMode = NextFishingFilterMode(normalized);
            var rect = new LegacyUiRect(pane.X + 10, buttonY, Math.Max(1, pane.Width - 20), RowModeButtonHeight);
            return DrawFishingFilterButton(spriteBatch, area, mouse, elements, rect, "fishing-filter-mode:" + nextMode, FishingFilterModeToggleLabel(normalized), true, FishingFilterModeTooltip(normalized));
        }

        private static LegacyUiElement DrawFishingFilterSpecialPane(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect pane, AppSettings settings)
        {
            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, pane, area.Viewport);
            var titleRect = new LegacyUiRect(pane.X + 6, pane.Y + 5, pane.Width - 12, BuffPaneTitleHeight);
            LegacyUiTheme.DrawSectionHeaderClipped(spriteBatch, titleRect, area.Viewport);
            UiTextRenderer.DrawTextClipped(spriteBatch, "特殊过滤", titleRect.X + 8, titleRect.Y + 8, titleRect.Width - 16, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 244, 238, 210, 255, 0.78f);

            var hovered = (LegacyUiElement)null;
            var rowGap = 6;
            var rowY = titleRect.Bottom + 7;
            var availableHeight = Math.Max(1, pane.Bottom - rowY - 10);
            var rowHeight = Math.Max(36, Math.Min(44, (availableHeight - rowGap * 2) / 3));
            hovered = DrawFishingFilterSpecialRuleRow(spriteBatch, area, mouse, elements, pane, rowY, rowHeight, "匣子", "crate", settings.FishingFilterCrateRule) ?? hovered;
            hovered = DrawFishingFilterSpecialRuleRow(spriteBatch, area, mouse, elements, pane, rowY + rowHeight + rowGap, rowHeight, "怪物", "enemy", settings.FishingFilterEnemyRule) ?? hovered;
            hovered = DrawFishingFilterSpecialRuleRow(spriteBatch, area, mouse, elements, pane, rowY + (rowHeight + rowGap) * 2, rowHeight, "任务鱼", "quest-fish", settings.FishingFilterQuestFishRule) ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawFishingFilterSpecialRuleRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect block, int y, int height, string label, string ruleKey, string selectedRule)
        {
            var row = new LegacyUiRect(block.X + 7, y, block.Width - 14, Math.Max(34, height));
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            var labelWidth = Math.Min(50, Math.Max(42, UiTextRenderer.EstimateTextWidth("任务鱼", 0.70f) + 8));
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, label, row.X + 8, row.Y, labelWidth, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.70f);
            var groupX = row.X + 8 + labelWidth + 5;
            var groupWidth = Math.Max(1, row.Right - groupX - 7);
            var buttonHeight = Math.Max(20, Math.Min(RowModeButtonHeight, row.Height - 8));
            var buttonY = row.Y + Math.Max(0, (row.Height - buttonHeight) / 2);
            var normalized = FishingFilterSpecialRuleModes.Normalize(selectedRule);
            var nextRule = NextFishingFilterSpecialRule(normalized);
            var rect = new LegacyUiRect(groupX, buttonY, groupWidth, buttonHeight);
            return DrawFishingFilterButton(spriteBatch, area, mouse, elements, rect, "fishing-filter-rule:" + ruleKey + ":" + nextRule, FishingFilterSpecialRuleToggleLabel(normalized), true, FishingFilterSpecialRuleTooltip(label, normalized));
        }

        private static string NextFishingFilterMode(string mode)
        {
            var normalized = FishingFilterModes.Normalize(mode);
            if (string.Equals(normalized, FishingFilterModes.AllowList, StringComparison.OrdinalIgnoreCase))
            {
                return FishingFilterModes.DenyList;
            }

            if (string.Equals(normalized, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase))
            {
                return FishingFilterModes.Disabled;
            }

            return FishingFilterModes.AllowList;
        }

        private static string FishingFilterModeToggleLabel(string mode)
        {
            var normalized = FishingFilterModes.Normalize(mode);
            if (string.Equals(normalized, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return "关闭过滤";
            }

            return string.Equals(normalized, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase) ? "黑名单" : "白名单";
        }

        private static string FishingFilterModeTooltip(string mode)
        {
            var normalized = FishingFilterModes.Normalize(mode);
            if (string.Equals(normalized, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return "钓上所有物品";
            }

            if (string.Equals(normalized, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase))
            {
                return "跳过当前名单物品，需要声呐buff";
            }

            return "只钓上当前名单物品，需要声呐buff";
        }

        private static string NextFishingFilterSpecialRule(string rule)
        {
            var normalized = FishingFilterSpecialRuleModes.Normalize(rule);
            if (string.Equals(normalized, FishingFilterSpecialRuleModes.Follow, StringComparison.OrdinalIgnoreCase))
            {
                return FishingFilterSpecialRuleModes.Allow;
            }

            if (string.Equals(normalized, FishingFilterSpecialRuleModes.Allow, StringComparison.OrdinalIgnoreCase))
            {
                return FishingFilterSpecialRuleModes.Deny;
            }

            return FishingFilterSpecialRuleModes.Follow;
        }

        private static string FishingFilterSpecialRuleToggleLabel(string rule)
        {
            var normalized = FishingFilterSpecialRuleModes.Normalize(rule);
            if (string.Equals(normalized, FishingFilterSpecialRuleModes.Allow, StringComparison.OrdinalIgnoreCase))
            {
                return "要";
            }

            return string.Equals(normalized, FishingFilterSpecialRuleModes.Deny, StringComparison.OrdinalIgnoreCase) ? "不要" : "跟随";
        }

        private static string FishingFilterSpecialRuleTooltip(string label, string rule)
        {
            var normalized = FishingFilterSpecialRuleModes.Normalize(rule);
            if (string.Equals(normalized, FishingFilterSpecialRuleModes.Allow, StringComparison.OrdinalIgnoreCase))
            {
                return "默认钓上" + label + "，可被黑名单过滤";
            }

            if (string.Equals(normalized, FishingFilterSpecialRuleModes.Deny, StringComparison.OrdinalIgnoreCase))
            {
                return "默认不要" + label + "，可被白过滤";
            }

            return null;
        }
    }
}
