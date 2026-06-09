using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Movement;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawFishingPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var y = 0;

            hovered = DrawFishingToggleRow(spriteBatch, area, mouse, elements, y, "自动钓鱼", "fishing.auto_fish", settings.FishingAutoFishEnabled, "上钩后自动收杆并重新抛竿；菜单打开时不收杆") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawFishingToggleRow(spriteBatch, area, mouse, elements, y, "自动换装", "fishing.auto_loadout", settings.FishingAutoLoadoutEnabled, "进入钓鱼状态会切换到渔力高的配装") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawFishingToggleRow(spriteBatch, area, mouse, elements, y, "自动配装", "fishing.auto_equipment", settings.FishingAutoEquipmentEnabled, "进入钓鱼状态会切换身上的饰品/装备到渔力最佳状态") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawFishingStoreModeRow(spriteBatch, area, mouse, elements, y, settings.FishingAutoStoreMode) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawFishingQuickRenameRow(spriteBatch, area, mouse, elements, y) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "切杆跳过", settings.FishingFilterCutRodSkipEnabled, "fishing-cut-rod-skip-mode:", "切杆跳过不要的鱼获") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SectionGap;

            hovered = DrawFishingFilterLayout(spriteBatch, area, mouse, elements, y, settings) ?? hovered;

            return hovered;
        }

        private static LegacyUiElement DrawFishingToggleRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string label, string featureId, bool enabled, string enabledTooltip)
        {
            return DrawBinaryModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                label,
                enabled,
                "fishing-toggle:" + featureId + ":",
                enabledTooltip);
        }

        private static LegacyUiElement DrawFishingStoreModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string mode)
        {
            var selected = FishingAutoStoreModes.Normalize(mode, false);
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "自动存放鱼",
                selected,
                new[] { "所有", "任务鱼", "关闭" },
                new[] { FishingAutoStoreModes.All, FishingAutoStoreModes.QuestFish, FishingAutoStoreModes.Off },
                "fishing-store-mode:",
                new[] { "每次钓上来东西后，点一次堆叠到附近箱子", "只在钓上当前渔夫任务鱼时存放", null });
        }

        private static LegacyUiElement DrawFishingQuickRenameRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, "快捷改名", row.X + 10, row.Y, 110, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);

            var buttonWidth = 96;
            var gap = 6;
            var buttonY = RowModeButtonY(row);
            var buttonRect = new LegacyUiRect(row.Right - buttonWidth - 10, buttonY, buttonWidth, RowModeButtonHeight);
            var inputX = row.X + 120;
            var inputWidth = Math.Max(80, buttonRect.X - inputX - gap);
            var inputRect = new LegacyUiRect(inputX, buttonY, inputWidth, RowModeButtonHeight);

            string currentName;
            string message;
            var hasName = PlayerRenameCompat.TryReadCurrentPlayerName(out currentName, out message);
            var inputFocused = LegacyTextInput.IsFocused(FishingQuickRenameTextInputId);
            if (inputFocused)
            {
                LegacyTextInput.Update(FishingQuickRenameTextInputId);
            }

            var inputText = inputFocused
                ? LegacyTextInput.GetDisplayText(FishingQuickRenameTextInputId, currentName)
                : hasName ? currentName : "未进入世界";
            var inputHit = inputRect.Intersect(area.Viewport);
            var inputElementRect = inputHit.Width > 0 && inputHit.Height > 0 ? inputHit : inputRect;
            var inputHovered = IsFrameElementHovered("fishing-quick-rename:input", inputElementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, inputRect, inputHovered, inputHovered && mouse.LeftDown, inputFocused, hasName || inputFocused, area.Viewport);
            var inputContentRect = LegacyUiTheme.GetSelectedButtonContentRect(inputRect, inputFocused, hasName || inputFocused);
            UiTextRenderer.DrawTextClipped(spriteBatch, inputText, inputRect.X + 8, inputContentRect.Y + 3, inputRect.Width - 16, Math.Max(1, inputContentRect.Height - 6), area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, inputFocused ? 255 : 230, inputFocused ? 245 : 232, inputFocused ? 205 : 224, 255, 0.72f);

            var inputElement = AddFrameElement(elements, "fishing-quick-rename:input", "快捷改名:名字", "button", inputElementRect, enabled: hasName || inputFocused, selected: inputFocused, tooltipLines: new[] { hasName || inputFocused ? "双击编辑" : FirstNonEmpty(message, "未进入世界") });
            RecordFrameElementHover(inputElement, inputHovered);

            var buttonLabel = inputFocused ? "确定" : "快捷改名";
            var buttonHit = buttonRect.Intersect(area.Viewport);
            var buttonElementRect = buttonHit.Width > 0 && buttonHit.Height > 0 ? buttonHit : buttonRect;
            var buttonHovered = IsFrameElementHovered("fishing-quick-rename:apply", buttonElementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, buttonRect, buttonHovered, buttonHovered && mouse.LeftDown, false, hasName || inputFocused, area.Viewport);
            var buttonScale = FitFishingFilterActionButtonScale(buttonLabel, buttonRect.Width, buttonLabel.Length >= 4 ? 0.66f : 0.74f);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, buttonLabel, buttonRect.X + 3, buttonRect.Y, buttonRect.Width - 6, buttonRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 230, 232, 224, 255, buttonScale);
            var buttonElement = AddFrameElement(elements, "fishing-quick-rename:apply", "快捷改名:" + buttonLabel, "button", buttonElementRect, enabled: hasName || inputFocused, tooltipLines: new[] { inputFocused ? "应用当前输入的名字" : "+1" });
            RecordFrameElementHover(buttonElement, buttonHovered);

            if (buttonHovered)
            {
                return buttonElement;
            }

            return inputHovered ? inputElement : null;
        }

        private static LegacyUiElement DrawFishingFilterModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string mode)
        {
            var selected = FishingFilterModes.Normalize(mode);
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "过滤模式",
                selected,
                new[] { "白名单", "黑名单", "关闭" },
                new[] { FishingFilterModes.AllowList, FishingFilterModes.DenyList, FishingFilterModes.Disabled },
                "fishing-filter-mode:",
                new[] { "白名单模式", "黑名单模式", "关闭钓鱼过滤；自动钓鱼会保留全部鱼获" });
        }

        private static LegacyUiElement DrawFishingFilterLayout(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            var paneGap = LegacyUiMetrics.GridCellGap * 2;
            new LegacyEmbeddedPanelControl
            {
                Id = "fishing-filter-legacy-adapter",
                Label = "钓鱼过滤",
                Bounds = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, CalculateFishingFilterLayoutHeight(area.Viewport.Width)),
                AdapterKind = "FishingFilter"
            }.Draw(LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings));
            if (UseStackedFishingFilterLayout(area.Viewport.Width))
            {
                var stackedPaneY = area.ToScreenY(contentY);
                var stackedLeftPane = new LegacyUiRect(area.Viewport.X, stackedPaneY, area.Viewport.Width, FishingFilterPanelHeight);
                var stackedRightPane = new LegacyUiRect(area.Viewport.X, stackedLeftPane.Bottom + paneGap, area.Viewport.Width, FishingFilterPanelHeight);
                var stackedHovered = (LegacyUiElement)null;
                stackedHovered = DrawFishingFilterListPane(spriteBatch, area, mouse, elements, stackedLeftPane, settings) ?? stackedHovered;
                stackedHovered = DrawFishingFilterSettingsPane(spriteBatch, area, mouse, elements, stackedRightPane, settings) ?? stackedHovered;
                return stackedHovered;
            }

            var rightWidth = Math.Min(FishingFilterSettingsMaxWidth, Math.Max(FishingFilterSettingsMinWidth, area.Viewport.Width / 3));
            var leftWidth = Math.Max(280, area.Viewport.Width - paneGap - rightWidth);
            if (leftWidth + paneGap + rightWidth > area.Viewport.Width)
            {
                rightWidth = Math.Max(FishingFilterSettingsMinWidth, area.Viewport.Width - paneGap - leftWidth);
            }

            var totalWidth = leftWidth + paneGap + rightWidth;
            var x = area.Viewport.X + Math.Max(0, (area.Viewport.Width - totalWidth) / 2);
            var paneY = area.ToScreenY(contentY);
            var leftPane = new LegacyUiRect(x, paneY, leftWidth, FishingFilterPanelHeight);
            var rightPane = new LegacyUiRect(leftPane.Right + paneGap, paneY, rightWidth, FishingFilterPanelHeight);
            var hovered = (LegacyUiElement)null;

            hovered = DrawFishingFilterListPane(spriteBatch, area, mouse, elements, leftPane, settings) ?? hovered;
            hovered = DrawFishingFilterSettingsPane(spriteBatch, area, mouse, elements, rightPane, settings) ?? hovered;
            return hovered;
        }

        private static bool UseStackedFishingFilterLayout(int viewportWidth)
        {
            return viewportWidth < 500;
        }

        private static int CalculateFishingFilterLayoutHeight(int viewportWidth)
        {
            return UseStackedFishingFilterLayout(viewportWidth)
                ? FishingFilterPanelHeight * 2 + LegacyUiMetrics.GridCellGap * 2
                : FishingFilterPanelHeight;
        }

        private static LegacyUiElement DrawFishingFilterListPane(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect pane, AppSettings settings)
        {
            FishingFilterUiState.EnsureModeSignature(settings);
            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, pane, area.Viewport);
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var title = FishingFilterModes.DisplayName(filterMode);
            var titleRect = new LegacyUiRect(pane.X + 6, pane.Y + 5, pane.Width - 12, BuffPaneTitleHeight);
            LegacyUiTheme.DrawSectionHeaderClipped(spriteBatch, titleRect, area.Viewport);
            UiTextRenderer.DrawTextClipped(spriteBatch, title, titleRect.X + 8, titleRect.Y + 8, titleRect.Width - 16, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 244, 238, 210, 255, 0.78f);

            var hovered = (LegacyUiElement)null;
            var toolbarX = pane.X + 12;
            var toolbarY = titleRect.Bottom + 7;
            var toolbarWidth = pane.Width - 24;
            var buttonGap = LegacyUiMetrics.GridCellGap;
            var buttonWidth = Math.Max(64, (toolbarWidth - buttonGap) / 2);
            var keywordButtonWidth = Math.Max(64, toolbarWidth - buttonWidth - buttonGap);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            hovered = DrawFishingFilterButton(spriteBatch, area, mouse, elements, new LegacyUiRect(toolbarX, toolbarY, buttonWidth, RowModeButtonHeight), "fishing-filter-match-mode:" + FishingFilterMatchModes.Exact, "精确匹配", string.Equals(matchMode, FishingFilterMatchModes.Exact, StringComparison.OrdinalIgnoreCase), null) ?? hovered;
            hovered = DrawFishingFilterButton(spriteBatch, area, mouse, elements, new LegacyUiRect(toolbarX + buttonWidth + buttonGap, toolbarY, keywordButtonWidth, RowModeButtonHeight), "fishing-filter-match-mode:" + FishingFilterMatchModes.Keyword, "关键词", string.Equals(matchMode, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase), null) ?? hovered;

            var listRect = new LegacyUiRect(pane.X + 10, toolbarY + RowModeButtonHeight + 8, pane.Width - 20, pane.Bottom - toolbarY - RowModeButtonHeight - 18);
            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, listRect, area.Viewport);
            hovered = DrawFishingFilterListContent(spriteBatch, area, mouse, elements, listRect, settings, filterMode, matchMode) ?? hovered;

            var hit = titleRect.Intersect(area.Viewport);
            var titleElementRect = hit.Width > 0 && hit.Height > 0 ? hit : titleRect;
            AddFrameElement(elements, "fishing-filter-list-title", title, "label", titleElementRect, enabled: false, tooltipLines: new[] { title + "：编辑当前模式下的钓鱼过滤名单；自动钓鱼会按当前页匹配方式运行过滤。" });
            return hovered;
        }

        private static LegacyUiElement DrawFishingFilterListContent(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, AppSettings settings, string filterMode, string matchMode)
        {
            var normalized = FishingFilterMatchModes.Normalize(matchMode);
            if (string.Equals(normalized, FishingFilterMatchModes.Exact, StringComparison.OrdinalIgnoreCase))
            {
                return DrawFishingFilterExactListContent(spriteBatch, area, mouse, elements, rect, settings, filterMode);
            }

            FishingFilterUiState.ClearPickerViewport();
            return DrawFishingFilterKeywordListContent(spriteBatch, area, mouse, elements, rect, settings, filterMode);
        }

        private static LegacyUiElement DrawFishingFilterExactListContent(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, AppSettings settings, string filterMode)
        {
            LegacyUiRect currentPickerAnchor;
            LegacyUiRect globalSearchAnchor;
            LegacyUiRect presetListAnchor;
            var hovered = DrawFishingFilterExactActions(spriteBatch, area, mouse, elements, rect, settings, filterMode, out currentPickerAnchor, out globalSearchAnchor, out presetListAnchor);
            var contentTop = rect.Y + 8 + FishingFilterActionButtonHeight + 8;
            var contentBottom = rect.Bottom - 8;
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                FishingFilterUiState.ClearPickerViewport();
                FishingFilterUiState.ClearPresetViewport();
                FishingFilterUiState.ClearEntryViewport();
                UiTextRenderer.DrawTextClipped(spriteBatch, "过滤未启用，请选择白名单或黑名单后编辑名单。", rect.X + 10, contentTop + 8, rect.Width - 20, 44, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 226, 218, 194, 240, 0.70f);
                return hovered;
            }

            var entriesRect = new LegacyUiRect(rect.X + 10, contentTop, rect.Width - 20, Math.Max(0, contentBottom - contentTop));
            hovered = DrawFishingFilterExactEntries(spriteBatch, area, mouse, elements, entriesRect, settings, filterMode) ?? hovered;

            if (FishingFilterUiState.PresetListOpen)
            {
                var presetHeight = BuildFishingFilterFloatingHeight(area, presetListAnchor, FishingFilterPresetMaxHeight, 78);
                if (presetHeight > 0)
                {
                    var presetRect = BuildFishingFilterFloatingRect(area, rect, presetListAnchor, presetHeight);
                    RegisterFishingFilterPresetListOverlay(area, presetRect, presetListAnchor, settings, filterMode, FishingFilterMatchModes.Exact);
                }
            }
            else
            {
                FishingFilterUiState.ClearPresetViewport();
            }

            if (FishingFilterUiState.PickerOpen)
            {
                var pickerAnchor = string.Equals(FishingFilterUiState.PickerSource, FishingFilterUiState.PickerSourceGlobal, StringComparison.Ordinal)
                    ? globalSearchAnchor
                    : currentPickerAnchor;
                var pickerHeight = BuildFishingFilterFloatingHeight(area, pickerAnchor, FishingFilterPickerMaxHeight, 78);
                if (pickerHeight > 0)
                {
                    var pickerRect = BuildFishingFilterFloatingRect(area, rect, pickerAnchor, pickerHeight);
                    RegisterFishingFilterExactPickerOverlay(area, pickerRect, pickerAnchor);
                }
            }
            else
            {
                FishingFilterUiState.ClearPickerViewport();
            }

            return hovered;
        }

    }
}
