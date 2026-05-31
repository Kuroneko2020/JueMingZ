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
            var toggle = new LegacyUiElement
            {
                Id = toggleId,
                Label = label,
                Kind = "button",
                Rect = new LegacyUiRect(row.X + 116, row.Y + 5, 72, LegacyUiMetrics.ButtonHeight),
                Selected = enabled
            };
            var slider = new LegacyUiElement
            {
                Id = sliderId,
                Label = sliderLabel,
                Kind = "slider",
                Rect = new LegacyUiRect(row.X + 208, row.Y + 4, row.Width - 218, LegacyUiMetrics.SliderHeight),
                IntValue = LegacyMainUiState.Clamp(threshold, LegacySlider.MinPercent, LegacySlider.MaxPercent),
                MinValue = LegacySlider.MinPercent,
                MaxValue = LegacySlider.MaxPercent
            };

            var toggleHovered = toggle.Rect.Contains(mouse.X, mouse.Y);
            LegacyUiTheme.DrawButton(spriteBatch, toggle.Rect, toggleHovered, toggleHovered && mouse.LeftDown, enabled, true);
            UiTextRenderer.DrawCenteredText(spriteBatch, enabled ? "ON" : "OFF", toggle.Rect.X + 3, toggle.Rect.Y, toggle.Rect.Width - 6, toggle.Rect.Height, 245, 238, 210, 255, 0.82f);

            var sliderElement = new LegacySliderControl
            {
                Id = slider.Id,
                Label = slider.Label,
                Kind = slider.Kind,
                Bounds = slider.Rect,
                IntValue = slider.IntValue,
                MinValue = slider.MinValue,
                MaxValue = slider.MaxValue,
                SliderLabel = sliderLabel,
                Suffix = "%"
            }.RegisterAndUpdate(LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault()));
            var sliderDragging = string.Equals(LegacyUiInput.ActiveSliderId, slider.Id, StringComparison.Ordinal);
            var sliderValue = LegacyUiInput.GetSliderDisplayValue(slider.Id, slider.IntValue);
            LegacySlider.Draw(spriteBatch, slider.Rect, sliderLabel, sliderValue, slider.Rect.Contains(mouse.X, mouse.Y), sliderDragging);
            elements.Add(toggle);
            return toggleHovered ? toggle : (slider.Rect.Contains(mouse.X, mouse.Y) ? sliderElement : null);
        }
    }
}
