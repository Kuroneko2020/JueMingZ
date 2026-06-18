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
        private static LegacyUiElement DrawMovementPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var y = 0;
            _movementSafeLandingConfigAnchorVisible = false;

            hovered = DrawMovementToggleRow(spriteBatch, area, mouse, elements, y, "模拟连跳", "movement.simulated_multi_jump", settings.MovementSimulatedMultiJumpEnabled, "模拟蛙腿的连跳效果。") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawContinuousDashModeRow(spriteBatch, area, mouse, elements, y, settings.MovementContinuousDashEnabled, settings.MovementContinuousDashMode) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawMovementToggleRow(spriteBatch, area, mouse, elements, y, "传送修正", "movement.teleport_correction", settings.MovementTeleportCorrectionEnabled, "点击进方块时，自动修正到附近可传送空位。") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawMovementSafeLandingRow(spriteBatch, area, mouse, elements, y, settings.MovementSafeLandingEnabled) ?? hovered;
            RegisterMovementSafeLandingConfigPopupOverlay(area, settings);

            return hovered;
        }

        private static LegacyUiElement DrawMovementSafeLandingRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, bool enabled)
        {
            const string targetId = "movement.fall_protection";
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var labels = new[] { "配置", "开启", "关闭" };
            var values = new[] { "Config", "On", "Off" };
            var totalWidth = 0;
            for (var index = 0; index < labels.Length; index++)
            {
                totalWidth += ModeButtonWidth(labels[index]);
                if (index > 0)
                {
                    totalWidth += 6;
                }
            }

            var x = row.Right - totalWidth - 10 - GetFeatureToggleHotkeyReserveWidth(targetId);
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            LegacySettingRowControl.DrawBackgroundAndLabel(context, row, "智能防摔", x);

            var hovered = (LegacyUiElement)null;
            var buttonY = RowModeButtonY(row);
            for (var index = 0; index < labels.Length; index++)
            {
                var width = ModeButtonWidth(labels[index]);
                var rect = new LegacyUiRect(x, buttonY, width, RowModeButtonHeight);
                var selected = index == 0 ? _movementSafeLandingConfigOpen : enabled == string.Equals(values[index], "On", StringComparison.Ordinal);
                var element = new LegacyButtonControl
                {
                    Id = "movement-safe-landing-mode:" + values[index],
                    Label = labels[index],
                    Text = labels[index],
                    ElementLabel = "智能防摔:" + labels[index],
                    Kind = "button",
                    Bounds = rect,
                    Selected = selected,
                    TextScale = 0.78f,
                    TooltipLines = index == 0
                        ? null
                        : index == 1
                            ? new[] { "利用合理手段避免摔落伤害" }
                            : new[] { "关闭智能防摔" }
                }.Draw(context);
                if (index == 0 && _movementSafeLandingConfigOpen)
                {
                    _movementSafeLandingConfigAnchor = rect;
                    _movementSafeLandingConfigAnchorVisible = true;
                }

                if (element != null && context.IsElementHovered(element.Id, rect))
                {
                    hovered = element;
                }

                x += width + 6;
            }

            hovered = DrawFeatureToggleHotkeyButton(context, row, targetId) ?? hovered;
            return context.HoveredElement ?? hovered;
        }

        private static bool RegisterMovementSafeLandingConfigPopupOverlay(LegacyScrollArea area, AppSettings settings)
        {
            if (!_movementSafeLandingConfigOpen || !_movementSafeLandingConfigAnchorVisible || area == null)
            {
                return false;
            }

            var options = MovementSafeLandingOptionCatalog.Options;
            int columns;
            int optionWidth;
            int columnGap;
            int rowGap;
            var popup = CalculateMovementSafeLandingPopupRect(
                area.Viewport,
                _movementSafeLandingConfigAnchor,
                options == null ? 0 : options.Length,
                out columns,
                out optionWidth,
                out columnGap,
                out rowGap);
            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = "movement-safe-landing-config-popup",
                OwnerPageId = "movement",
                Bounds = popup,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 20,
                CacheSignature = BuildMovementSafeLandingPopupCacheSignature(settings, options),
                State = area,
                Draw = DrawMovementSafeLandingConfigPopupOverlay
            });
        }

        private static void DrawMovementSafeLandingConfigPopupOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var area = request == null ? null : request.State as LegacyScrollArea;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || area == null || elements == null)
            {
                return;
            }

            DrawMovementSafeLandingConfigPopup(context.SpriteBatch, area, context.Mouse, elements);
        }

        private static LegacyUiElement DrawMovementSafeLandingConfigPopup(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            if (!_movementSafeLandingConfigOpen || !_movementSafeLandingConfigAnchorVisible || area == null)
            {
                return null;
            }

            var options = MovementSafeLandingOptionCatalog.Options;
            int columns;
            int optionWidth;
            int columnGap;
            int rowGap;
            var popup = CalculateMovementSafeLandingPopupRect(
                area.Viewport,
                _movementSafeLandingConfigAnchor,
                options == null ? 0 : options.Length,
                out columns,
                out optionWidth,
                out columnGap,
                out rowGap);
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            new LegacyPopupPanelControl
            {
                Id = "movement-safe-landing-config-popup",
                Label = "智能防摔配置",
                Kind = "blocker",
                Bounds = popup
            }.Draw(context);

            UiTextRenderer.DrawText(spriteBatch, "智能防摔配置", popup.X + 16, popup.Y + 11, 238, 238, 226, 255, 0.82f);
            var hovered = (LegacyUiElement)null;
            var close = new LegacyUiRect(popup.Right - 54, popup.Y + 8, 40, 20);
            hovered = DrawMovementSafeLandingSmallButton(spriteBatch, mouse, elements, close, "movement-safe-landing-mode:Config", "关闭", "关闭配置") ?? hovered;

            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var startX = popup.X + MovementSafeLandingPopupHorizontalPadding;
            var startY = popup.Y + MovementSafeLandingPopupContentStartY;
            for (var index = 0; index < options.Length; index++)
            {
                var column = index % columns;
                var row = index / columns;
                var rect = new LegacyUiRect(
                    startX + column * (optionWidth + columnGap),
                    startY + row * (MovementSafeLandingOptionHeight + rowGap),
                    optionWidth,
                    MovementSafeLandingOptionHeight);
                hovered = DrawMovementSafeLandingOption(spriteBatch, mouse, elements, rect, options[index], MovementSafeLandingOptionCatalog.GetEnabled(settings, options[index].Id)) ?? hovered;
            }

            return hovered;
        }

        private static int BuildMovementSafeLandingPopupCacheSignature(AppSettings settings, MovementSafeLandingOptionDefinition[] options)
        {
            unchecked
            {
                settings = settings ?? AppSettings.CreateDefault();
                var hash = 17;
                hash = hash * 31 + settings.ConfigVersion;
                hash = hash * 31 + (options == null ? 0 : options.Length);
                if (options != null)
                {
                    for (var index = 0; index < options.Length; index++)
                    {
                        var option = options[index];
                        hash = hash * 31 + StringComparer.Ordinal.GetHashCode(option == null ? string.Empty : option.Id ?? string.Empty);
                        hash = hash * 31 + (option != null && MovementSafeLandingOptionCatalog.GetEnabled(settings, option.Id) ? 1 : 0);
                    }
                }

                return hash;
            }
        }

        private static LegacyUiElement DrawMovementSafeLandingSmallButton(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, string tooltip)
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

        private static LegacyUiElement DrawMovementSafeLandingOption(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, MovementSafeLandingOptionDefinition option, bool enabled)
        {
            var context = new LegacyUiContext(spriteBatch, mouse, LegacyMainUiState.WindowRect, LegacyMainUiState.SelectedPageId, ConfigService.AppSettings ?? AppSettings.CreateDefault(), elements);
            var element = new LegacyCheckboxButtonControl
            {
                Id = "movement-safe-landing-option:" + option.Id,
                Label = option.Label,
                Kind = "button",
                Bounds = rect,
                Selected = enabled,
                TextScale = 0.70f,
                TooltipLines = IsMovementSafeLandingGrappleOption(option) && !string.IsNullOrWhiteSpace(option.Tooltip)
                    ? new[] { option.Tooltip }
                    : null
            }.Draw(context);
            if (element != null)
            {
                element.Label = "智能防摔:" + option.Label;
            }

            return element != null && context.IsElementHovered(element.Id, rect) ? element : null;
        }

        private static bool IsMovementSafeLandingGrappleOption(MovementSafeLandingOptionDefinition option)
        {
            return option != null &&
                   string.Equals(option.Id, MovementSafeLandingOptionCatalog.Grapple, StringComparison.Ordinal);
        }

        private static LegacyUiRect CalculateMovementSafeLandingPopupRect(
            LegacyUiRect viewport,
            LegacyUiRect anchor,
            int optionCount,
            out int columns,
            out int optionWidth,
            out int columnGap,
            out int rowGap)
        {
            optionCount = Math.Max(1, optionCount);
            columnGap = MovementSafeLandingPopupColumnGap;
            rowGap = MovementSafeLandingPopupRowGap;
            columns = optionCount <= 8 ? 2 : 3;
            if (viewport.Width < 420)
            {
                columns = Math.Min(columns, 2);
            }

            columns = Math.Max(1, Math.Min(columns, optionCount));
            var desiredWidth = MovementSafeLandingPopupHorizontalPadding * 2 +
                               columns * MovementSafeLandingOptionMinWidth +
                               (columns - 1) * columnGap;
            var maxWidth = Math.Min(MovementSafeLandingPopupMaxWidth, Math.Max(MovementSafeLandingPopupMinWidth, viewport.Width - 12));
            var width = ClampInt(desiredWidth, MovementSafeLandingPopupMinWidth, maxWidth);
            optionWidth = Math.Max(
                MovementSafeLandingOptionMinWidth,
                (width - MovementSafeLandingPopupHorizontalPadding * 2 - (columns - 1) * columnGap) / columns);

            var rows = (optionCount + columns - 1) / columns;
            var desiredHeight = MovementSafeLandingPopupContentStartY +
                                rows * MovementSafeLandingOptionHeight +
                                Math.Max(0, rows - 1) * rowGap +
                                MovementSafeLandingPopupBottomPadding;
            var maxHeight = Math.Min(MovementSafeLandingPopupMaxHeight, Math.Max(MovementSafeLandingPopupMinHeight, viewport.Height - 12));
            var height = ClampInt(desiredHeight, MovementSafeLandingPopupMinHeight, maxHeight);
            var x = ClampInt(anchor.X - width + anchor.Width, viewport.X + 6, Math.Max(viewport.X + 6, viewport.Right - width - 6));
            var y = anchor.Bottom + 8;
            if (y + height > viewport.Bottom - 6)
            {
                y = anchor.Y - height - 8;
            }

            y = ClampInt(y, viewport.Y + 6, Math.Max(viewport.Y + 6, viewport.Bottom - height - 6));
            return new LegacyUiRect(x, y, width, height);
        }

        internal static bool RegisterMovementSafeLandingConfigPopupOverlayForTesting(LegacyScrollArea area, LegacyUiRect anchor)
        {
            _movementSafeLandingConfigOpen = true;
            _movementSafeLandingConfigAnchor = anchor;
            _movementSafeLandingConfigAnchorVisible = true;
            return RegisterMovementSafeLandingConfigPopupOverlay(area, AppSettings.CreateDefault());
        }

        internal static void ResetMovementSafeLandingConfigPopupForTesting()
        {
            _movementSafeLandingConfigOpen = false;
            _movementSafeLandingConfigAnchor = new LegacyUiRect();
            _movementSafeLandingConfigAnchorVisible = false;
        }

        private static LegacyUiElement DrawMovementToggleRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string label, string featureId, bool enabled, string tooltip)
        {
            return DrawBinaryModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                label,
                enabled,
                "movement-toggle:" + featureId + ":",
                tooltip,
                featureToggleTargetId: featureId);
        }

        private static LegacyUiElement DrawContinuousDashModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, bool enabled, string mode)
        {
            var selected = enabled ? MovementContinuousDashModes.Normalize(mode) : MovementContinuousDashModes.Off;
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "连续冲刺",
                selected,
                new[] { "按住", "双击", "关闭" },
                new[] { MovementContinuousDashModes.HoldDirection, MovementContinuousDashModes.DoubleTapAndHold, MovementContinuousDashModes.Off },
                "movement-continuous-dash-mode:",
                new[] { "按住方向键冲刺", "双击并按住冲刺", "关闭连续冲刺" },
                featureToggleTargetId: "movement.continuous_dash");
        }
    }
}
