using System;
using System.Collections.Generic;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Controls;
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
            _autoCaptureCritterConfigAnchorVisible = false;

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
            hovered = DrawAutoCaptureCritterRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            hovered = DrawAutoCaptureCritterConfigPopup(spriteBatch, area, mouse, elements) ?? hovered;
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
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动收税", settings.NpcAutoTaxCollectEnabled, "misc-auto-tax-collect-mode:", "靠近税收官自动收钱") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "旅行菜单", settings.WorldAutomationTravelMenuEnabled, "misc-travel-menu-mode:", "单机临时开启原版旅行菜单") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawWorldGenerationDetailsRow(spriteBatch, area, mouse, elements, y) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawDeveloperEasterEggRow(spriteBatch, area, mouse, elements, y) ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawAutoCaptureCritterRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var selectedMode = AutoCaptureCritterModes.Normalize(settings.WorldAutomationAutoCaptureCritterMode, settings.MiscAutoCaptureCritterEnabled);
            var labels = new[] { "配置", "自动", "手持", "关闭" };
            var values = new[] { "Config", AutoCaptureCritterModes.Auto, AutoCaptureCritterModes.Manual, AutoCaptureCritterModes.Off };
            var totalWidth = 0;
            for (var index = 0; index < labels.Length; index++)
            {
                totalWidth += ModeButtonWidth(labels[index]);
                if (index > 0)
                {
                    totalWidth += 6;
                }
            }

            var x = row.Right - totalWidth - 10;
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings);
            LegacySettingRowControl.DrawBackgroundAndLabel(context, row, "自动捕捉", x);

            var hovered = (LegacyUiElement)null;
            var buttonY = RowModeButtonY(row);
            for (var index = 0; index < labels.Length; index++)
            {
                var width = ModeButtonWidth(labels[index]);
                var rect = new LegacyUiRect(x, buttonY, width, RowModeButtonHeight);
                var configButton = index == 0;
                var selected = configButton
                    ? _autoCaptureCritterConfigOpen
                    : string.Equals(selectedMode, values[index], StringComparison.Ordinal);
                var element = new LegacyButtonControl
                {
                    Id = configButton
                        ? "misc-auto-capture-critter-config:toggle"
                        : "misc-auto-capture-critter-mode:" + values[index],
                    Label = labels[index],
                    Text = labels[index],
                    ElementLabel = "自动捕捉:" + labels[index],
                    Kind = "button",
                    Bounds = rect,
                    Selected = selected,
                    TextScale = 0.78f,
                    TooltipLines = BuildAutoCaptureCritterRowTooltip(index, settings)
                }.Draw(context);

                if (configButton && _autoCaptureCritterConfigOpen)
                {
                    _autoCaptureCritterConfigAnchor = rect;
                    _autoCaptureCritterConfigAnchorVisible = true;
                }

                if (element != null && context.IsElementHovered(element.Id, rect))
                {
                    hovered = element;
                }

                x += width + 6;
            }

            return hovered;
        }

        private static string[] BuildAutoCaptureCritterRowTooltip(int index, AppSettings settings)
        {
            if (index == 0)
            {
                var disabled = AutoCaptureCritterCategoryCatalog.CountDisabled(settings);
                return disabled > 0
                    ? new[] { disabled.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 个捕捉分类已关闭" }
                    : null;
            }

            if (index == 1)
            {
                return new[] { "身上带着虫网就行" };
            }

            if (index == 2)
            {
                return new[] { "必须手持虫网" };
            }

            return null;
        }

        private static LegacyUiElement DrawAutoCaptureCritterConfigPopup(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            if (!_autoCaptureCritterConfigOpen || !_autoCaptureCritterConfigAnchorVisible)
            {
                return null;
            }

            var options = AutoCaptureCritterCategoryCatalog.Options;
            int columns;
            int optionWidth;
            int columnGap;
            int rowGap;
            var popup = CalculateAutoCaptureCritterPopupRect(
                area.Viewport,
                _autoCaptureCritterConfigAnchor,
                options == null ? 0 : options.Length,
                out columns,
                out optionWidth,
                out columnGap,
                out rowGap);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings);
            new LegacyPopupPanelControl
            {
                Id = "misc-auto-capture-critter-config-popup",
                Label = "自动捕捉配置",
                Kind = "blocker",
                Bounds = popup
            }.Draw(context);

            UiTextRenderer.DrawText(spriteBatch, "自动捕捉配置", popup.X + 16, popup.Y + 11, 238, 238, 226, 255, 0.82f);
            var hovered = (LegacyUiElement)null;
            var close = new LegacyUiRect(popup.Right - 54, popup.Y + 8, 40, 20);
            hovered = DrawAutoCaptureCritterSmallButton(spriteBatch, mouse, elements, close, "misc-auto-capture-critter-config:toggle", "关闭", "关闭配置") ?? hovered;

            var startX = popup.X + AutoCaptureCritterPopupHorizontalPadding;
            var startY = popup.Y + AutoCaptureCritterPopupContentStartY;
            for (var index = 0; options != null && index < options.Length; index++)
            {
                var option = options[index];
                if (option == null)
                {
                    continue;
                }

                var column = index % columns;
                var row = index / columns;
                var rect = new LegacyUiRect(
                    startX + column * (optionWidth + columnGap),
                    startY + row * (AutoCaptureCritterOptionHeight + rowGap),
                    optionWidth,
                    AutoCaptureCritterOptionHeight);
                hovered = DrawAutoCaptureCritterOption(spriteBatch, mouse, elements, rect, option, AutoCaptureCritterCategoryCatalog.GetEnabled(settings, option.Id)) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawAutoCaptureCritterSmallButton(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, string tooltip)
        {
            var context = new LegacyUiContext(spriteBatch, mouse, LegacyMainUiState.WindowRect, LegacyMainUiState.SelectedPageId, ConfigService.AppSettings ?? AppSettings.CreateDefault(), elements);
            var element = new LegacySmallButtonControl
            {
                Id = id,
                Label = label,
                Kind = "button",
                Bounds = rect,
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            }.Draw(context);
            return element != null && context.IsElementHovered(element.Id, rect) ? element : null;
        }

        private static LegacyUiElement DrawAutoCaptureCritterOption(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, AutoCaptureCritterCategoryDefinition option, bool enabled)
        {
            var context = new LegacyUiContext(spriteBatch, mouse, LegacyMainUiState.WindowRect, LegacyMainUiState.SelectedPageId, ConfigService.AppSettings ?? AppSettings.CreateDefault(), elements);
            var element = new LegacyCheckboxButtonControl
            {
                Id = "misc-auto-capture-critter-option:" + option.Id,
                Label = option.Label,
                Kind = "button",
                Bounds = rect,
                Selected = enabled,
                TextScale = 0.70f
            }.Draw(context);
            if (element != null)
            {
                element.Label = "自动捕捉:" + option.Label;
            }

            return element != null && context.IsElementHovered(element.Id, rect) ? element : null;
        }

        private static LegacyUiRect CalculateAutoCaptureCritterPopupRect(
            LegacyUiRect viewport,
            LegacyUiRect anchor,
            int optionCount,
            out int columns,
            out int optionWidth,
            out int columnGap,
            out int rowGap)
        {
            optionCount = Math.Max(1, optionCount);
            columnGap = AutoCaptureCritterPopupColumnGap;
            rowGap = AutoCaptureCritterPopupRowGap;
            columns = optionCount <= 8 ? 2 : 3;
            if (viewport.Width < 420)
            {
                columns = Math.Min(columns, 2);
            }

            columns = Math.Max(1, Math.Min(columns, optionCount));
            var desiredWidth = AutoCaptureCritterPopupHorizontalPadding * 2 +
                               columns * AutoCaptureCritterOptionMinWidth +
                               (columns - 1) * columnGap;
            var maxWidth = Math.Min(AutoCaptureCritterPopupMaxWidth, Math.Max(AutoCaptureCritterPopupMinWidth, viewport.Width - 12));
            var width = ClampInt(desiredWidth, AutoCaptureCritterPopupMinWidth, maxWidth);
            optionWidth = Math.Max(
                AutoCaptureCritterOptionMinWidth,
                (width - AutoCaptureCritterPopupHorizontalPadding * 2 - (columns - 1) * columnGap) / columns);

            var rows = (optionCount + columns - 1) / columns;
            var desiredHeight = AutoCaptureCritterPopupContentStartY +
                                rows * AutoCaptureCritterOptionHeight +
                                Math.Max(0, rows - 1) * rowGap +
                                AutoCaptureCritterPopupBottomPadding;
            var maxHeight = Math.Min(AutoCaptureCritterPopupMaxHeight, Math.Max(AutoCaptureCritterPopupMinHeight, viewport.Height - 12));
            var height = ClampInt(desiredHeight, AutoCaptureCritterPopupMinHeight, maxHeight);
            var x = ClampInt(anchor.X - width + anchor.Width, viewport.X + 6, Math.Max(viewport.X + 6, viewport.Right - width - 6));
            var y = anchor.Bottom + 8;
            if (y + height > viewport.Bottom - 6)
            {
                y = anchor.Y - height - 8;
            }

            y = ClampInt(y, viewport.Y + 6, Math.Max(viewport.Y + 6, viewport.Bottom - height - 6));
            return new LegacyUiRect(x, y, width, height);
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
            // Misc rows register hit-test elements only; clicks are translated to
            // LegacyUiCommand after the frame.
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
            var elementId = elementPrefix + buttonValue;
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var isHovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, isHovered, isHovered && mouse.LeftDown, false, true, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, buttonLabel, rect.X + 4, rect.Y, rect.Width - 8, rect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, textR, textG, textB, 255, ResolveSingleMiscButtonScale(buttonLabel, rect.Width - 8));

            var element = AddFrameElement(elements, elementId, label + ":" + buttonLabel, "button", elementRect);
            RecordFrameElementHover(element, isHovered);
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
