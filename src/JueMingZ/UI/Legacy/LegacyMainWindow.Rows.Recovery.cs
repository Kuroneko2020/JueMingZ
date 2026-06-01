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
        private static LegacyUiElement DrawHealModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string mode)
        {
            var selected = AutoRecoverySettings.NormalizeHealMode(mode, false);
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "自动回血",
                selected,
                new[] { "智能", "快速", "关闭" },
                new[] { "Smart", "Quick", "Off" },
                "auto-heal-mode:",
                new[] { "根据掉血量智能的选择回血物品，可能会推迟回血", "检测到掉血立即使用回血量最高的物品", string.Empty });
        }

        private static LegacyUiElement DrawManaModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string mode)
        {
            var selected = AutoRecoverySettings.NormalizeManaMode(mode, false);
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "自动回蓝",
                selected,
                new[] { "开启", "关闭" },
                new[] { "ManaFlower", "Off" },
                "auto-mana-mode:",
                null);
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
            UiTextRenderer.DrawCenteredText(spriteBatch, enabled ? "ON" : "OFF", toggleRect.X + 3, toggleRect.Y, toggleRect.Width - 6, toggleRect.Height, 245, 238, 210, 255, 0.82f);

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
