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
        private static void DrawSection(object spriteBatch, LegacyScrollArea area, int contentY, string title)
        {
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.SectionHeaderHeight);
            if (!area.IsVisible(rect))
            {
                return;
            }

            new LegacySectionHeaderControl
            {
                Bounds = rect,
                Label = title
            }.Draw(LegacyUiContext.ForScrollArea(spriteBatch, null, area, null, ConfigService.AppSettings ?? AppSettings.CreateDefault()));
        }

        private static LegacyUiElement DrawBinaryModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string label, bool enabled, string elementPrefix, string tooltip, string styleFeatureId = null, string featureToggleTargetId = null)
        {
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                label,
                enabled ? "On" : "Off",
                new[] { "开启", "关闭" },
                new[] { "On", "Off" },
                elementPrefix,
                string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip, null },
                styleFeatureId,
                featureToggleTargetId);
        }

        private static LegacyUiElement DrawRightModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string label, string selectedMode, string[] labels, string[] values, string elementPrefix, string[] tooltips, string styleFeatureId = null, string featureToggleTargetId = null)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            var reserveWidth = GetFeatureToggleHotkeyReserveWidth(featureToggleTargetId);
            var hovered = (LegacyUiElement)null;
            if (!InformationStyleHelper.IsConfigurable(styleFeatureId))
            {
                hovered = new LegacyModeButtonGroup
                {
                    Bounds = row,
                    Label = label,
                    SelectedValue = selectedMode,
                    ButtonLabels = labels,
                    ButtonValues = values,
                    ElementPrefix = elementPrefix,
                    ButtonTooltips = tooltips,
                    ElementLabelPrefix = label,
                    RightReserveWidth = reserveWidth
                }.Draw(context);
                hovered = DrawFeatureToggleHotkeyButton(context, row, featureToggleTargetId) ?? hovered;
                return context.HoveredElement ?? hovered;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            var buttonY = RowModeButtonY(row);
            var totalWidth = 0;
            for (var index = 0; index < labels.Length; index++)
            {
                totalWidth += ModeButtonWidth(labels[index]);
                if (index > 0)
                {
                    totalWidth += 6;
                }
            }

            var x = row.Right - totalWidth - 10 - reserveWidth;
            var configRect = new LegacyUiRect(x, buttonY, 0, 0);
            if (InformationStyleHelper.IsConfigurable(styleFeatureId))
            {
                configRect = new LegacyUiRect(x - InformationStyleConfigButtonWidth - 6, buttonY, InformationStyleConfigButtonWidth, RowModeButtonHeight);
            }

            var labelRight = configRect.Width > 0 ? configRect.X : x;
            var labelWidth = Math.Max(60, labelRight - row.X - 20);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, label, row.X + 10, row.Y, labelWidth, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);
            if (configRect.Width > 0)
            {
                var hit = configRect.Intersect(area.Viewport);
                var elementId = "information-style-config:" + styleFeatureId;
                var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : configRect;
                var isHovered = IsFrameElementHovered(elementId, elementRect, mouse);
                var selected = string.Equals(_informationStylePopupFeatureId, styleFeatureId, StringComparison.Ordinal);
                LegacyUiTheme.DrawButtonClipped(spriteBatch, configRect, isHovered, isHovered && mouse.LeftDown, selected, true, area.Viewport);
                var color = InformationStyleHelper.GetColor(ConfigService.AppSettings ?? AppSettings.CreateDefault(), styleFeatureId);
                var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(configRect, selected, true);
                var swatchY = contentRect.Y + Math.Max(0, (contentRect.Height - 10) / 2);
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, configRect.X + 7, swatchY, 10, 10, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 3, color.R, color.G, color.B, 245);
                UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, configRect.X + 7, swatchY, 10, 10, 1, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 20, 24, 34, 190);
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "配置", configRect.X + 14, contentRect.Y, configRect.Width - 16, contentRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, selected ? LegacyUiTheme.SelectedTextR : 230, selected ? LegacyUiTheme.SelectedTextG : 232, selected ? LegacyUiTheme.SelectedTextB : 224, 255, 0.72f);
                if (selected)
                {
                    LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(configRect.X + 14, contentRect.Y, configRect.Width - 16, contentRect.Height), area.Viewport, "配置", 0.72f);
                }

                var element = AddFrameElement(elements, elementId, label + ":配置", "button", elementRect, selected: selected, tooltipLines: new[] { "调整颜色和字号" });
                RecordFrameElementHover(element, isHovered);
                if (selected)
                {
                    _informationStylePopupAnchor = configRect;
                    _informationStylePopupAnchorVisible = true;
                }

                if (isHovered)
                {
                    hovered = element;
                }
            }

            for (var index = 0; index < labels.Length; index++)
            {
                var width = ModeButtonWidth(labels[index]);
                var rect = new LegacyUiRect(x, buttonY, width, RowModeButtonHeight);
                var selected = string.Equals(selectedMode, values[index], StringComparison.OrdinalIgnoreCase);
                var hit = rect.Intersect(area.Viewport);
                var elementId = elementPrefix + values[index];
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

            hovered = DrawFeatureToggleHotkeyButton(context, row, featureToggleTargetId) ?? hovered;
            return context.HoveredElement ?? hovered;
        }

        private static int RowModeButtonY(LegacyUiRect row)
        {
            return row.Y + Math.Max(0, (row.Height - RowModeButtonHeight) / 2);
        }

        private static int RowLabelY(LegacyUiRect row)
        {
            return row.Y + Math.Max(0, (row.Height - RowLabelTextHeight) / 2);
        }

        private static int ModeButtonWidth(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return 64;
            }

            return Math.Max(64, Math.Min(180, UiTextRenderer.EstimateTextWidth(label, 0.78f) + 18));
        }

    }
}
