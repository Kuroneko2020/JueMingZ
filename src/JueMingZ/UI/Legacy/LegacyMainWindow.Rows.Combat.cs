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
        private static LegacyUiElement DrawCombatAimAssistRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, CombatAimRowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            var hovered = (LegacyUiElement)null;
            var title = "辅助瞄准";
            var titleWidth = Math.Max(76, UiTextRenderer.EstimateTextWidth(title, 0.86f) + 8);
            var titleRect = new LegacyUiRect(row.X + 10, row.Y + 7, titleWidth, 22);
            UiTextRenderer.DrawTextClipped(spriteBatch, title, titleRect.X, titleRect.Y + 2, titleRect.Width, titleRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);
            var titleHit = titleRect.Intersect(area.Viewport);
            var titleElementRect = titleHit.Width > 0 && titleHit.Height > 0 ? titleHit : titleRect;
            var titleHovered = IsFrameElementHovered("combat-aim-assist-title", titleElementRect, mouse);
            if (titleHovered)
            {
                hovered = AddFrameElement(elements, "combat-aim-assist-title", title, "label", titleElementRect, enabled: false, tooltipLines: new[] { "辅助瞄准会按当前范围中心与目标策略选择敌怪；鼠标中心使用滑条距离，玩家中心使用屏幕范围。" });
                RecordFrameElementHover(hovered, true);
            }

            const int toggleWidth = 76;
            const int toggleGap = 6;
            var toggleX = row.Right - toggleWidth * 4 - toggleGap * 3 - 10;
            var toggleY = row.Y + 5;
            hovered = DrawCombatSmallToggle(spriteBatch, area, mouse, elements, new LegacyUiRect(toggleX, toggleY, toggleWidth, CombatSmallToggleHeight), "combat-aim-priority-toggle", CombatAimModes.TargetPriorityLabel(settings.AimTargetPriority), true) ?? hovered;
            hovered = DrawCombatSmallToggle(spriteBatch, area, mouse, elements, new LegacyUiRect(toggleX + (toggleWidth + toggleGap), toggleY, toggleWidth, CombatSmallToggleHeight), "combat-aim-origin-toggle", CombatAimModes.RangeOriginLabel(settings.AimRangeOrigin), true) ?? hovered;
            hovered = DrawCombatSmallToggle(spriteBatch, area, mouse, elements, new LegacyUiRect(toggleX + (toggleWidth + toggleGap) * 2, toggleY, toggleWidth, CombatSmallToggleHeight), "combat-aim-track-dummy-toggle", "追踪人偶", settings.CombatAimTrackDummyEnabled) ?? hovered;
            hovered = DrawCombatSmallToggle(spriteBatch, area, mouse, elements, new LegacyUiRect(toggleX + (toggleWidth + toggleGap) * 3, toggleY, toggleWidth, CombatSmallToggleHeight), "combat-aim-marker-toggle", "瞄准标记", settings.CombatAimMarkerEnabled) ?? hovered;

            var playerCenterMode = string.Equals(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase);
            var activeRadius = settings.CursorAimRadius;

            var valueText = new LegacyUiRect(
                row.Right - CombatAimValueTextWidth - 28,
                row.Y + 43,
                CombatAimValueTextWidth,
                CombatAimValueTextHeight);
            var sliderRight = valueText.X - 12;
            var slider = new LegacySliderControl
            {
                Id = "combat-aim-radius",
                Label = "辅助瞄准半径",
                Kind = playerCenterMode ? "label" : "slider",
                Bounds = new LegacyUiRect(row.X + 32, row.Y + 42, Math.Max(160, sliderRight - (row.X + 32)), 30),
                IntValue = LegacyMainUiState.Clamp(activeRadius, CombatAimRadiusMin, CombatAimRadiusMax),
                MinValue = CombatAimRadiusMin,
                MaxValue = CombatAimRadiusMax,
                Enabled = !playerCenterMode,
                TooltipLines = playerCenterMode
                    ? new[] { "玩家中心模式使用当前屏幕范围加少量边缘余量，不需要手动设置距离。" }
                    : null
            };
            var sliderElement = slider.RegisterAndUpdate(LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings));

            var sliderDragging = !playerCenterMode && string.Equals(LegacyUiInput.ActiveSliderId, slider.Id, StringComparison.Ordinal);
            var sliderValue = LegacyUiInput.GetSliderDisplayValue(slider.Id, slider.IntValue);
            var sliderHovered = IsFrameElementHovered(slider.Id, slider.Bounds, mouse);
            DrawCombatAimRadiusSlider(spriteBatch, slider.Bounds, sliderValue, sliderHovered, sliderDragging, playerCenterMode, area.Viewport);
            DrawCombatAimValueText(spriteBatch, valueText, BuildCombatAimRadiusStatusText(sliderValue, playerCenterMode), playerCenterMode, area.Viewport);
            if (sliderHovered)
            {
                hovered = sliderElement;
            }

            return hovered;
        }

        private static LegacyUiElement DrawCombatPhasebladeQuickSwitchRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            const string targetId = "combat.phaseblade_quick_switch";
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            settings = settings ?? AppSettings.CreateDefault();
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings);
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);

            const int rowPadding = 10;
            const int buttonGap = 6;
            var title = "光剑快切";
            var titleWidth = Math.Max(78, UiTextRenderer.EstimateTextWidth(title, 0.86f) + 8);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                title,
                row.X + rowPadding,
                row.Y,
                titleWidth,
                row.Height,
                UiTextHorizontalAlignment.Left,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                238,
                238,
                226,
                255,
                0.86f);

            var buttonLabels = new[] { "开启", "关闭" };
            var buttonValues = new[] { "On", "Off" };
            var selectedMode = settings.CombatPhasebladeQuickSwitchEnabled ? "On" : "Off";
            var buttonGroupWidth = ModeButtonWidth(buttonLabels[0]) + buttonGap + ModeButtonWidth(buttonLabels[1]);
            var buttonX = row.Right - buttonGroupWidth - rowPadding - GetFeatureToggleHotkeyReserveWidth(targetId);
            var inlineLeft = row.X + rowPadding + titleWidth + 12;
            var inlineRight = buttonX - 12;
            var inlineWidth = Math.Max(1, inlineRight - inlineLeft);
            var intervalLabelWidth = inlineWidth >= 170 ? 38 : 0;
            var intervalValueWidth = inlineWidth >= 148 ? 58 : 48;
            var value = CombatPhasebladeQuickSwitchSettings.NormalizeIntervalTicks(settings.CombatPhasebladeQuickSwitchIntervalTicks);
            var valueRect = new LegacyUiRect(inlineRight - intervalValueWidth, RowModeButtonY(row), intervalValueWidth, RowModeButtonHeight);
            var sliderX = inlineLeft + intervalLabelWidth + (intervalLabelWidth > 0 ? 6 : 0);
            var sliderRight = valueRect.X - 8;
            var sliderRect = new LegacyUiRect(sliderX, row.Y + 3, Math.Max(1, sliderRight - sliderX), LegacyUiMetrics.SliderHeight);

            if (intervalLabelWidth > 0)
            {
                UiTextRenderer.DrawAlignedTextClipped(
                    spriteBatch,
                    "间隔",
                    inlineLeft,
                    row.Y,
                    intervalLabelWidth,
                    row.Height,
                    UiTextHorizontalAlignment.Left,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    206,
                    218,
                    238,
                    230,
                    0.72f);
            }

            var slider = new LegacySliderControl
            {
                Id = CombatPhasebladeQuickSwitchIntervalSliderId,
                Label = "光剑快切间隔",
                Bounds = sliderRect,
                IntValue = value,
                MinValue = CombatPhasebladeQuickSwitchSettings.MinIntervalTicks,
                MaxValue = CombatPhasebladeQuickSwitchSettings.MaxIntervalTicks
            };
            var sliderElement = slider.RegisterAndUpdate(context);
            var sliderDragging = string.Equals(LegacyUiInput.ActiveSliderId, slider.Id, StringComparison.Ordinal);
            var sliderValue = LegacyUiInput.GetSliderDisplayValue(slider.Id, slider.IntValue);
            var sliderHovered = IsFrameElementHovered(slider.Id, slider.Bounds, mouse);
            DrawCombatInlineSlider(
                spriteBatch,
                slider.Bounds,
                sliderValue,
                CombatPhasebladeQuickSwitchSettings.MinIntervalTicks,
                CombatPhasebladeQuickSwitchSettings.MaxIntervalTicks,
                sliderHovered,
                sliderDragging,
                false,
                area.Viewport);
            DrawCombatAimValueText(spriteBatch, valueRect, sliderValue.ToString(CultureInfo.InvariantCulture) + " tick", false, area.Viewport);

            var hovered = sliderHovered ? sliderElement : null;
            var buttonY = RowModeButtonY(row);
            for (var index = 0; index < buttonLabels.Length; index++)
            {
                var width = ModeButtonWidth(buttonLabels[index]);
                var rect = new LegacyUiRect(buttonX, buttonY, width, RowModeButtonHeight);
                var selected = string.Equals(selectedMode, buttonValues[index], StringComparison.OrdinalIgnoreCase);
                var element = new LegacyButtonControl
                {
                    Id = "combat-phaseblade-quick-switch-mode:" + buttonValues[index],
                    Label = buttonLabels[index],
                    Text = buttonLabels[index],
                    ElementLabel = "光剑快切:" + buttonLabels[index],
                    Kind = "button",
                    Bounds = rect,
                    Selected = selected,
                    TextScale = 0.78f,
                    TooltipLines = index == 0 ? new[] { "按住右键快切快捷栏的光剑" } : null
                }.Draw(context);

                if (element != null && context.IsElementHovered(element.Id, rect))
                {
                    hovered = element;
                }

                buttonX += width + buttonGap;
            }

            hovered = DrawFeatureToggleHotkeyButton(context, row, targetId) ?? hovered;
            return context.HoveredElement ?? hovered;
        }

        private static LegacyUiElement DrawCombatSmallToggle(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, bool selected)
        {
            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, selected, true, area.Viewport);
            var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, selected, true);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, label, rect.X + 3, contentRect.Y, rect.Width - 6, contentRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, selected ? LegacyUiTheme.SelectedTextR : 230, selected ? LegacyUiTheme.SelectedTextG : 232, selected ? LegacyUiTheme.SelectedTextB : 224, 255, 0.62f);
            if (selected)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(rect.X + 3, contentRect.Y, rect.Width - 6, contentRect.Height), area.Viewport, label, 0.62f);
            }

            var element = AddFrameElement(elements, id, "辅助瞄准:" + label, "button", elementRect, selected: selected);
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static void DrawCombatAimValueText(object spriteBatch, LegacyUiRect rect, string text, bool disabled, LegacyUiRect clip)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, text, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, disabled ? 222 : 238, disabled ? 224 : 238, disabled ? 228 : 214, disabled ? 230 : 255, 0.72f);
        }

        private static string BuildCombatAimRadiusStatusText(int radius, bool playerCenterMode)
        {
            radius = LegacyMainUiState.Clamp(radius, CombatAimRadiusMin, CombatAimRadiusMax);
            if (radius <= 0)
            {
                return "已关闭自瞄";
            }

            return playerCenterMode
                ? "屏幕范围"
                : "鼠标半径 " + radius.ToString(CultureInfo.InvariantCulture);
        }

        internal static string BuildCombatAimRadiusStatusTextForTesting(int radius, bool playerCenterMode)
        {
            return BuildCombatAimRadiusStatusText(radius, playerCenterMode);
        }

        private static void DrawCombatAimRadiusSlider(object spriteBatch, LegacyUiRect rect, int value, bool hovered, bool dragging, bool disabled, LegacyUiRect clip)
        {
            DrawCombatInlineSlider(spriteBatch, rect, value, CombatAimRadiusMin, CombatAimRadiusMax, hovered, dragging, disabled, clip);
        }

        private static void DrawCombatInlineSlider(object spriteBatch, LegacyUiRect rect, int value, int minValue, int maxValue, bool hovered, bool dragging, bool disabled, LegacyUiRect clip)
        {
            value = LegacyMainUiState.Clamp(value, minValue, maxValue);
            var trackY = rect.Y + rect.Height / 2 - 3;
            var trackX = rect.X + 10;
            var trackWidth = Math.Max(1, rect.Width - 20);
            var valueRange = Math.Max(1, maxValue - minValue);
            var fillWidth = disabled ? trackWidth : (int)Math.Round(trackWidth * ((value - minValue) / (double)valueRange));
            object texture;
            if (VanillaUiSkinCompat.TryGetColorBarTexture(out texture))
            {
                var alpha = disabled ? 90 : 210;
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiledClipped(spriteBatch, texture, trackX, trackY, trackWidth, 7, clip.X, clip.Y, clip.Width, clip.Height, 180, 180, 180, alpha);
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiledClipped(spriteBatch, texture, trackX, trackY, Math.Max(7, fillWidth), 7, clip.X, clip.Y, clip.Width, clip.Height, disabled ? 150 : 255, disabled ? 150 : 255, disabled ? 150 : 255, disabled ? 120 : 246);
            }
            else
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, trackX, trackY, trackWidth, 7, clip.X, clip.Y, clip.Width, clip.Height, 4, 15, 21, 44, disabled ? 130 : 220);
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, trackX, trackY, Math.Max(7, fillWidth), 7, clip.X, clip.Y, clip.Width, clip.Height, 4, disabled ? 92 : 95, disabled ? 96 : 132, disabled ? 104 : 188, disabled ? 120 : 230);
                UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, trackX, trackY, trackWidth, 7, 1, clip.X, clip.Y, clip.Width, clip.Height, disabled ? 90 : 115, disabled ? 94 : 143, disabled ? 104 : 198, disabled ? 150 : 220);
            }

            if (!disabled)
            {
                DrawCombatAimNeedleMarker(spriteBatch, trackX + fillWidth, trackY + 3, dragging, hovered, clip);
            }
        }

        private static void DrawCombatAimNeedleMarker(object spriteBatch, int centerX, int centerY, bool dragging, bool hovered, LegacyUiRect clip)
        {
            var fillR = dragging ? 255 : hovered ? 248 : 236;
            var fillG = dragging ? 246 : hovered ? 238 : 230;
            var fillB = dragging ? 188 : hovered ? 176 : 160;
            var topY = centerY - 12;
            var x = centerX - 3;

            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, topY, 7, 25, clip.X, clip.Y, clip.Width, clip.Height, 20, 18, 30, 235);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 1, topY + 1, 5, 23, clip.X, clip.Y, clip.Width, clip.Height, fillR, fillG, fillB, 255);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 2, topY + 2, 1, 21, clip.X, clip.Y, clip.Width, clip.Height, 255, 252, 208, 210);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 5, topY + 2, 1, 21, clip.X, clip.Y, clip.Width, clip.Height, 124, 92, 58, 120);
        }
    }
}
