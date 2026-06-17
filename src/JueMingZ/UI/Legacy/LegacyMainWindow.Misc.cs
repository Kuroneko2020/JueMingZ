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

            hovered = DrawQuickReforgeRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            int quickReforgePanelHeight;
            hovered = DrawQuickReforgeListPanel(spriteBatch, area, mouse, elements, y + LegacyUiMetrics.RowHeight, out quickReforgePanelHeight) ?? hovered;
            y += MiscExpandableRowHeight(quickReforgePanelHeight);
            hovered = DrawAutoMiningRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
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

        private static bool RegisterAutoCaptureCritterConfigPopupOverlay(LegacyScrollArea area, AppSettings settings)
        {
            if (!_autoCaptureCritterConfigOpen || !_autoCaptureCritterConfigAnchorVisible || area == null)
            {
                return false;
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
            settings = settings ?? AppSettings.CreateDefault();
            // The config popup is modal overlay content. Keeping it registered
            // here prevents later items rows from drawing or hit-testing above it.
            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = "misc-auto-capture-critter-config-popup",
                OwnerPageId = "home",
                Bounds = popup,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 20,
                CacheSignature = BuildAutoCaptureCritterPopupCacheSignature(settings, options == null ? 0 : options.Length),
                Draw = DrawAutoCaptureCritterConfigPopupOverlay
            });
        }

        private static void DrawAutoCaptureCritterConfigPopupOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            if (context == null || request == null)
            {
                return;
            }

            var popup = request.Bounds;
            var options = AutoCaptureCritterCategoryCatalog.Options;
            int columns;
            int optionWidth;
            int columnGap;
            int rowGap;
            CalculateAutoCaptureCritterOptionGrid(
                popup,
                options == null ? 0 : options.Length,
                out columns,
                out optionWidth,
                out columnGap,
                out rowGap);
            new LegacyPopupPanelControl
            {
                Id = "misc-auto-capture-critter-config-popup",
                Label = "自动捕捉配置",
                Kind = "blocker",
                Bounds = popup
            }.Draw(context);

            UiTextRenderer.DrawText(context.SpriteBatch, "自动捕捉配置", popup.X + 16, popup.Y + 11, 238, 238, 226, 255, 0.82f);
            var close = new LegacyUiRect(popup.Right - 54, popup.Y + 8, 40, 20);
            DrawAutoCaptureCritterSmallButton(context, close, "misc-auto-capture-critter-config:toggle", "关闭", "关闭配置");

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
                DrawAutoCaptureCritterOption(context, rect, option, AutoCaptureCritterCategoryCatalog.GetEnabled(context.Settings, option.Id));
            }
        }

        private static LegacyUiElement DrawAutoCaptureCritterSmallButton(LegacyUiContext context, LegacyUiRect rect, string id, string label, string tooltip)
        {
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

        private static LegacyUiElement DrawAutoCaptureCritterOption(LegacyUiContext context, LegacyUiRect rect, AutoCaptureCritterCategoryDefinition option, bool enabled)
        {
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

        private static void CalculateAutoCaptureCritterOptionGrid(
            LegacyUiRect popup,
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
            if (popup.Width < 420)
            {
                columns = Math.Min(columns, 2);
            }

            columns = Math.Max(1, Math.Min(columns, optionCount));
            optionWidth = Math.Max(
                AutoCaptureCritterOptionMinWidth,
                (popup.Width - AutoCaptureCritterPopupHorizontalPadding * 2 - (columns - 1) * columnGap) / columns);
        }

        private static int BuildAutoCaptureCritterPopupCacheSignature(AppSettings settings, int optionCount)
        {
            unchecked
            {
                settings = settings ?? AppSettings.CreateDefault();
                var hash = 17;
                hash = hash * 31 + optionCount;
                hash = hash * 31 + settings.ConfigVersion;
                hash = hash * 31 + AutoCaptureCritterCategoryCatalog.CountDisabled(settings);
                return hash;
            }
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
            columns = optionCount <= 8 ? 2 : 3;
            if (viewport.Width < 420)
            {
                columns = Math.Min(columns, 2);
            }

            columns = Math.Max(1, Math.Min(columns, optionCount));
            columnGap = AutoCaptureCritterPopupColumnGap;
            rowGap = AutoCaptureCritterPopupRowGap;
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

        internal static bool RegisterAutoCaptureCritterConfigPopupOverlayForTesting(LegacyScrollArea area, LegacyUiRect anchor)
        {
            _autoCaptureCritterConfigOpen = true;
            _autoCaptureCritterConfigAnchor = anchor;
            _autoCaptureCritterConfigAnchorVisible = true;
            return RegisterAutoCaptureCritterConfigPopupOverlay(area, AppSettings.CreateDefault());
        }

        internal static void ResetAutoCaptureCritterConfigPopupForTesting()
        {
            _autoCaptureCritterConfigOpen = false;
            _autoCaptureCritterConfigAnchor = new LegacyUiRect();
            _autoCaptureCritterConfigAnchorVisible = false;
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
                pending ? 72 : 224,
                pending ? 0 : ModeButtonWidth(actionLabel));
        }

        private static LegacyUiElement DrawSingleMiscActionRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string label, string buttonLabel, string buttonValue, string elementPrefix, int textR, int textG, int textB, int buttonWidthOverride = 0)
        {
            // Misc rows register hit-test elements only; clicks are translated to
            // LegacyUiCommand after the frame.
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);

            var buttonWidth = CalculateSingleMiscActionButtonWidth(buttonLabel, row.Width, buttonWidthOverride);
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

        private static int CalculateSingleMiscActionButtonWidth(string buttonLabel, int rowWidth, int buttonWidthOverride)
        {
            var maxWidth = Math.Max(1, rowWidth - 150);
            if (buttonWidthOverride > 0)
            {
                return Math.Min(buttonWidthOverride, maxWidth);
            }

            return Math.Min(Math.Max(118, UiTextRenderer.EstimateTextWidth(buttonLabel, 0.72f) + 20), Math.Max(118, maxWidth));
        }

        internal static int[] GetDeveloperEasterEggButtonWidthsForTesting(int rowWidth)
        {
            return new[]
            {
                CalculateSingleMiscActionButtonWidth("开启", rowWidth, ModeButtonWidth("开启")),
                CalculateSingleMiscActionButtonWidth("有坏档的风险慎用", rowWidth, 0),
                RowModeButtonHeight
            };
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
