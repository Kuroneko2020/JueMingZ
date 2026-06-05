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
        private static LegacyUiElement DrawHealModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var mode = settings.AutoHealMode;
            var selected = AutoRecoverySettings.NormalizeHealMode(mode, false);
            return DrawAutoRecoveryModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "heal",
                "自动回血",
                selected,
                new[] { "智能", "快速", "关闭" },
                new[] { "Smart", "Quick", "Off" },
                "auto-heal-mode:",
                new[] { "根据掉血量智能的选择回血物品，可能会推迟回血", "检测到掉血立即使用回血量最高的物品", string.Empty },
                AutoRecoveryItemFilter.CountBlockedHealItems(settings));
        }

        private static LegacyUiElement DrawManaModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var mode = settings.AutoManaMode;
            var selected = AutoRecoverySettings.NormalizeManaMode(mode, false);
            return DrawAutoRecoveryModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "mana",
                "自动回蓝",
                selected,
                new[] { "开启", "关闭" },
                new[] { "ManaFlower", "Off" },
                "auto-mana-mode:",
                null,
                AutoRecoveryItemFilter.CountBlockedManaItems(settings));
        }

        private static LegacyUiElement DrawAutoRecoveryModeRow(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            string configKind,
            string label,
            string selectedMode,
            string[] labels,
            string[] values,
            string elementPrefix,
            string[] tooltips,
            int blockedCount)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            var hovered = (LegacyUiElement)null;
            var buttonY = RowModeButtonY(row);
            var configWidth = ModeButtonWidth("配置");
            var totalWidth = configWidth;
            if (labels != null)
            {
                for (var index = 0; index < labels.Length; index++)
                {
                    totalWidth += 6 + ModeButtonWidth(labels[index]);
                }
            }

            var x = row.Right - totalWidth - 10;
            var labelWidth = Math.Max(60, x - row.X - 20);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, label, row.X + 10, row.Y, labelWidth, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);

            var configRect = new LegacyUiRect(x, buttonY, configWidth, RowModeButtonHeight);
            var configElementId = "auto-recovery-item-config:" + configKind;
            var configHit = configRect.Intersect(area.Viewport);
            var configElementRect = configHit.Width > 0 && configHit.Height > 0 ? configHit : configRect;
            var configHovered = IsFrameElementHovered(configElementId, configElementRect, mouse);
            var configSelected = string.Equals(_autoRecoveryItemConfigKind, configKind, StringComparison.Ordinal);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, configRect, configHovered, configHovered && mouse.LeftDown, configSelected, true, area.Viewport);
            var configContentRect = LegacyUiTheme.GetSelectedButtonContentRect(configRect, configSelected, true);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "配置", configRect.X + 3, configContentRect.Y, configRect.Width - 6, configContentRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, configSelected ? LegacyUiTheme.SelectedTextR : 230, configSelected ? LegacyUiTheme.SelectedTextG : 232, configSelected ? LegacyUiTheme.SelectedTextB : 224, 255, 0.78f);
            if (configSelected)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(configRect.X + 3, configContentRect.Y, configRect.Width - 6, configContentRect.Height), area.Viewport, "配置", 0.78f);
                _autoRecoveryItemConfigAnchor = configRect;
                _autoRecoveryItemConfigAnchorVisible = true;
            }

            var configTooltip = blockedCount > 0
                ? new[] { blockedCount.ToString(CultureInfo.InvariantCulture) + " 个恢复物品已取消勾选" }
                : null;
            var configElement = AddFrameElement(elements, configElementId, label + ":配置", "button", configElementRect, selected: configSelected, tooltipLines: configTooltip);
            RecordFrameElementHover(configElement, configHovered);
            if (configHovered)
            {
                hovered = configElement;
            }

            x += configWidth + 6;
            for (var index = 0; labels != null && index < labels.Length; index++)
            {
                var width = ModeButtonWidth(labels[index]);
                var rect = new LegacyUiRect(x, buttonY, width, RowModeButtonHeight);
                var selected = values != null && index < values.Length && string.Equals(selectedMode, values[index], StringComparison.OrdinalIgnoreCase);
                var hit = rect.Intersect(area.Viewport);
                var elementId = elementPrefix + (values != null && index < values.Length ? values[index] : string.Empty);
                var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
                var isHovered = IsFrameElementHovered(elementId, elementRect, mouse);
                LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, isHovered, isHovered && mouse.LeftDown, selected, true, area.Viewport);
                var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, selected, true);
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, labels[index], rect.X + 3, contentRect.Y, rect.Width - 6, contentRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, selected ? LegacyUiTheme.SelectedTextR : 230, selected ? LegacyUiTheme.SelectedTextG : 232, selected ? LegacyUiTheme.SelectedTextB : 224, 255, 0.78f);
                if (selected)
                {
                    LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(rect.X + 3, contentRect.Y, rect.Width - 6, contentRect.Height), area.Viewport, labels[index], 0.78f);
                }

                var element = AddFrameElement(
                    elements,
                    elementId,
                    label + ":" + labels[index],
                    "button",
                    elementRect,
                    selected: selected,
                    tooltipLines: tooltips != null && index < tooltips.Length && !string.IsNullOrWhiteSpace(tooltips[index])
                        ? new[] { tooltips[index] }
                        : null);
                RecordFrameElementHover(element, isHovered);
                if (isHovered)
                {
                    hovered = element;
                }

                x += width + 6;
            }

            return hovered;
        }

        private static LegacyUiElement DrawAutoRecoveryItemConfigPopup(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var kind = NormalizeAutoRecoveryItemConfigKind(_autoRecoveryItemConfigKind);
            if (string.IsNullOrEmpty(kind) || !_autoRecoveryItemConfigAnchorVisible)
            {
                return null;
            }

            var heal = string.Equals(kind, "heal", StringComparison.Ordinal);
            var definitions = heal
                ? RecoveryPotionDefinitionCatalog.GetHealDefinitions()
                : RecoveryPotionDefinitionCatalog.GetManaDefinitions();
            int columns;
            int optionWidth;
            int columnGap;
            int rowGap;
            var popup = CalculateAutoRecoveryItemPopupRect(
                area.Viewport,
                _autoRecoveryItemConfigAnchor,
                definitions == null ? 0 : definitions.Length,
                out columns,
                out optionWidth,
                out columnGap,
                out rowGap);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings);
            new LegacyPopupPanelControl
            {
                Id = "auto-recovery-item-config-popup",
                Label = heal ? "自动回血配置" : "自动回蓝配置",
                Kind = "blocker",
                Bounds = popup
            }.Draw(context);

            UiTextRenderer.DrawText(spriteBatch, heal ? "自动回血配置" : "自动回蓝配置", popup.X + 16, popup.Y + 11, 238, 238, 226, 255, 0.82f);
            var hovered = (LegacyUiElement)null;
            var close = new LegacyUiRect(popup.Right - 54, popup.Y + 8, 40, 20);
            hovered = DrawAutoRecoverySmallButton(spriteBatch, mouse, elements, close, "auto-recovery-item-config:" + kind, "关闭", "关闭配置") ?? hovered;

            if (definitions == null || definitions.Length <= 0)
            {
                UiTextRenderer.DrawCenteredText(spriteBatch, "未读取到可配置物品", popup.X + 16, popup.Y + AutoRecoveryItemPopupContentStartY, popup.Width - 32, 28, 205, 218, 238, 235, 0.76f);
                return hovered;
            }

            var startX = popup.X + AutoRecoveryItemPopupHorizontalPadding;
            var startY = popup.Y + AutoRecoveryItemPopupContentStartY;
            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                if (definition == null)
                {
                    continue;
                }

                var column = index % columns;
                var row = index / columns;
                var rect = new LegacyUiRect(
                    startX + column * (optionWidth + columnGap),
                    startY + row * (AutoRecoveryItemOptionHeight + rowGap),
                    optionWidth,
                    AutoRecoveryItemOptionHeight);
                if (rect.Bottom > popup.Bottom - AutoRecoveryItemPopupBottomPadding)
                {
                    continue;
                }

                var enabled = heal
                    ? AutoRecoveryItemFilter.IsHealItemEnabled(settings, definition.ItemType)
                    : AutoRecoveryItemFilter.IsManaItemEnabled(settings, definition.ItemType);
                hovered = DrawAutoRecoveryItemOption(spriteBatch, mouse, elements, rect, kind, definition, enabled) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawAutoRecoverySmallButton(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, string tooltip)
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

        private static LegacyUiElement DrawAutoRecoveryItemOption(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string kind, RecoveryPotionDefinition definition, bool enabled)
        {
            var context = new LegacyUiContext(spriteBatch, mouse, LegacyMainUiState.WindowRect, LegacyMainUiState.SelectedPageId, ConfigService.AppSettings ?? AppSettings.CreateDefault(), elements);
            var itemName = string.IsNullOrWhiteSpace(definition.ItemName)
                ? "#" + definition.ItemType.ToString(CultureInfo.InvariantCulture)
                : definition.ItemName;
            var displayName = UiTextRenderer.Ellipsize(itemName, Math.Max(1, rect.Width - 42), 0.70f);
            var element = new LegacyCheckboxButtonControl
            {
                Id = "auto-recovery-item-option:" + kind + ":" + definition.ItemType.ToString(CultureInfo.InvariantCulture),
                Label = displayName,
                Kind = "button",
                Bounds = rect,
                Selected = enabled,
                TextScale = 0.70f,
                TooltipLines = BuildAutoRecoveryItemTooltip(definition)
            }.Draw(context);
            if (element != null)
            {
                element.Label = (string.Equals(kind, "heal", StringComparison.Ordinal) ? "自动回血:" : "自动回蓝:") + itemName;
            }

            return element != null && context.IsElementHovered(element.Id, rect) ? element : null;
        }

        private static string[] BuildAutoRecoveryItemTooltip(RecoveryPotionDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            return new[]
            {
                "物品: " + (string.IsNullOrWhiteSpace(definition.ItemName) ? "#" + definition.ItemType.ToString(CultureInfo.InvariantCulture) : definition.ItemName),
                "生命恢复: " + definition.HealLife.ToString(CultureInfo.InvariantCulture),
                "魔力恢复: " + definition.HealMana.ToString(CultureInfo.InvariantCulture),
                "item ID: " + definition.ItemType.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static LegacyUiRect CalculateAutoRecoveryItemPopupRect(
            LegacyUiRect viewport,
            LegacyUiRect anchor,
            int optionCount,
            out int columns,
            out int optionWidth,
            out int columnGap,
            out int rowGap)
        {
            optionCount = Math.Max(1, optionCount);
            columnGap = AutoRecoveryItemPopupColumnGap;
            rowGap = AutoRecoveryItemPopupRowGap;
            columns = optionCount <= 8 ? 2 : 3;
            if (viewport.Width < 460)
            {
                columns = Math.Min(columns, 2);
            }

            columns = Math.Max(1, Math.Min(columns, optionCount));
            var desiredWidth = AutoRecoveryItemPopupHorizontalPadding * 2 +
                               columns * AutoRecoveryItemOptionMinWidth +
                               (columns - 1) * columnGap;
            var maxWidth = Math.Min(AutoRecoveryItemPopupMaxWidth, Math.Max(AutoRecoveryItemPopupMinWidth, viewport.Width - 12));
            var width = ClampInt(desiredWidth, AutoRecoveryItemPopupMinWidth, maxWidth);
            optionWidth = Math.Max(
                AutoRecoveryItemOptionMinWidth,
                (width - AutoRecoveryItemPopupHorizontalPadding * 2 - (columns - 1) * columnGap) / columns);

            var rows = (optionCount + columns - 1) / columns;
            var desiredHeight = AutoRecoveryItemPopupContentStartY +
                                rows * AutoRecoveryItemOptionHeight +
                                Math.Max(0, rows - 1) * rowGap +
                                AutoRecoveryItemPopupBottomPadding;
            var maxHeight = Math.Min(AutoRecoveryItemPopupMaxHeight, Math.Max(AutoRecoveryItemPopupMinHeight, viewport.Height - 12));
            var height = ClampInt(desiredHeight, AutoRecoveryItemPopupMinHeight, maxHeight);
            var x = ClampInt(anchor.X - width + anchor.Width, viewport.X + 6, Math.Max(viewport.X + 6, viewport.Right - width - 6));
            var y = anchor.Bottom + 8;
            if (y + height > viewport.Bottom - 6)
            {
                y = anchor.Y - height - 8;
            }

            y = ClampInt(y, viewport.Y + 6, Math.Max(viewport.Y + 6, viewport.Bottom - height - 6));
            return new LegacyUiRect(x, y, width, height);
        }

        private static LegacyUiElement DrawRecoveryRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string label, string toggleId, bool enabled, string sliderId, int threshold, string sliderLabel)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyFeatureRow.DrawLabel(spriteBatch, row, label, enabled ? "ON" : "OFF");
            var toggleRect = new LegacyUiRect(row.X + 116, row.Y + 5, 72, LegacyUiMetrics.ButtonHeight);
            var sliderRect = new LegacyUiRect(row.X + 208, row.Y + 4, row.Width - 218, LegacyUiMetrics.SliderHeight);
            var sliderValue = LegacyMainUiState.Clamp(threshold, LegacySlider.MinPercent, LegacySlider.MaxPercent);

            var toggleHovered = IsFrameElementHovered(toggleId, toggleRect, mouse);
            LegacyUiTheme.DrawButton(spriteBatch, toggleRect, toggleHovered, toggleHovered && mouse.LeftDown, enabled, true);
            var toggleContentRect = LegacyUiTheme.GetSelectedButtonContentRect(toggleRect, enabled, true);
            UiTextRenderer.DrawCenteredText(spriteBatch, enabled ? "ON" : "OFF", toggleRect.X + 3, toggleContentRect.Y, toggleRect.Width - 6, toggleContentRect.Height, 245, 238, 210, 255, 0.82f);

            var sliderElement = new LegacySliderControl
            {
                Id = sliderId,
                Label = sliderLabel,
                Kind = "slider",
                Bounds = sliderRect,
                IntValue = sliderValue,
                MinValue = LegacySlider.MinPercent,
                MaxValue = LegacySlider.MaxPercent,
                SliderLabel = sliderLabel,
                Suffix = "%"
            }.RegisterAndUpdate(LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault()));
            var sliderDragging = string.Equals(LegacyUiInput.ActiveSliderId, sliderId, StringComparison.Ordinal);
            var sliderDisplayValue = LegacyUiInput.GetSliderDisplayValue(sliderId, sliderValue);
            var sliderHovered = IsFrameElementHovered(sliderId, sliderRect, mouse);
            LegacySlider.Draw(spriteBatch, sliderRect, sliderLabel, sliderDisplayValue, sliderHovered, sliderDragging);
            var toggle = AddFrameElement(elements, toggleId, label, "button", toggleRect, selected: enabled);
            RecordFrameElementHover(toggle, toggleHovered);
            return toggleHovered ? toggle : (sliderHovered ? sliderElement : null);
        }
    }
}
