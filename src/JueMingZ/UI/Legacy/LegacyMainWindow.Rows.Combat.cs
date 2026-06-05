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
            DrawCombatAimValueText(spriteBatch, valueText, playerCenterMode ? "屏幕范围" : "鼠标半径 " + sliderValue.ToString(CultureInfo.InvariantCulture), playerCenterMode, area.Viewport);
            if (sliderHovered)
            {
                hovered = sliderElement;
            }

            return hovered;
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

        private static void DrawCombatAimRadiusSlider(object spriteBatch, LegacyUiRect rect, int value, bool hovered, bool dragging, bool disabled, LegacyUiRect clip)
        {
            value = LegacyMainUiState.Clamp(value, CombatAimRadiusMin, CombatAimRadiusMax);
            var trackY = rect.Y + rect.Height / 2 - 3;
            var trackX = rect.X + 10;
            var trackWidth = Math.Max(1, rect.Width - 20);
            var fillWidth = disabled ? trackWidth : (int)Math.Round(trackWidth * ((value - CombatAimRadiusMin) / (double)(CombatAimRadiusMax - CombatAimRadiusMin)));
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
