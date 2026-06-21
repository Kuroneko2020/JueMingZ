using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI
{
    public static class BlueprintHandheldActionBarOverlay
    {
        private const string VisualContract = "ui-scale-bottom-action-bar+dynamic-buttons+legacy-ui-theme+vanilla-ui-skin+button-text-scale-0.78+create-enters-mask+exit-create-preserves-mask+save-captures-mask+disabled-save-tooltip+open-library-real+unimplemented-buttons-ui-only+mouse-consume+no-blueprint-refresh+no-input-action-queue";
        internal const float ButtonTextScale = 0.78f;
        private const float MinimumButtonTextScale = 0.52f;

        public static bool DrawInterfaceLayer()
        {
            try
            {
                var frame = BuildFrame(RuntimeSettingsSnapshotProvider.GetCurrent(), GameStateReader.LastSnapshot, ReadEnvironment(GameStateReader.LastSnapshot));
                if (frame == null || !frame.Visible)
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("BlueprintHandheldActionBarOverlay", true, out spriteBatch))
                {
                    return true;
                }

                DrawFrame(spriteBatch, frame);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("BlueprintHandheldActionBarOverlay", error);
                LogThrottle.ErrorThrottled(
                    "blueprint-handheld-action-bar-overlay-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintHandheldActionBarOverlay",
                    "Blueprint handheld action bar overlay draw failed; exception swallowed.", error);
            }

            return true;
        }

        public static void UpdatePrefixGuard()
        {
            UpdateInputGuard("BlueprintHandheldActionBarOverlay.UpdatePrefixGuard", true);
        }

        public static void UpdateAfterPlayerInputGuard()
        {
            UpdateInputGuard("BlueprintHandheldActionBarOverlay.UpdateAfterPlayerInputGuard", false);
        }

        internal static bool ShouldRegisterUiOverlayForTesting()
        {
            return true;
        }

        internal static bool ShouldRegisterInputGuardsForTesting()
        {
            return true;
        }

        internal static string GetVisualContractForTesting()
        {
            return VisualContract;
        }

        internal static float ResolveButtonTextScaleForTesting(string label, int availableWidth)
        {
            return ResolveButtonTextScale(label, availableWidth);
        }

        internal static BlueprintHandheldActionBarFrame BuildFrameForTesting(
            RuntimeSettingsSnapshot settings,
            GameStateSnapshot gameState,
            BlueprintHandheldActionBarEnvironment environment)
        {
            return BuildFrame(settings, gameState, environment);
        }

        internal static BlueprintHandheldActionBarInteraction HandlePointerForTesting(
            BlueprintHandheldActionBarFrame frame,
            BlueprintHandheldActionBarPointerInput input)
        {
            return BlueprintHandheldActionBarState.HandlePointer(frame, input);
        }

        private static BlueprintHandheldActionBarFrame BuildFrame(
            RuntimeSettingsSnapshot settings,
            GameStateSnapshot gameState,
            BlueprintHandheldActionBarEnvironment environment)
        {
            return BlueprintHandheldActionBarState.BuildFrame(settings, gameState, environment);
        }

        private static BlueprintHandheldActionBarEnvironment ReadEnvironment(GameStateSnapshot gameState)
        {
            var environment = new BlueprintHandheldActionBarEnvironment
            {
                WorldReady = gameState != null && gameState.IsInWorld && TerrariaMainCompat.IsWorldReady,
                GameInputAvailable = TerrariaMainCompat.AllowsInputProcessing,
                MapFullscreenOpen = TerrariaMainCompat.IsMapFullscreenOpen,
                LegacyMainUiVisible = LegacyMainUiState.Visible,
                ScreenWidth = TerrariaMainCompat.ScreenWidth,
                ScreenHeight = TerrariaMainCompat.ScreenHeight
            };

            bool blocked;
            string reason;
            if (GameMode.TryReadLegacyUiBlockedByVanillaMenuLateOnly(out blocked, out reason))
            {
                environment.VanillaMenuReadAvailable = true;
                environment.VanillaMenuBlocked = blocked;
                environment.VanillaMenuReason = reason ?? string.Empty;
            }
            else
            {
                environment.VanillaMenuReadAvailable = false;
                environment.VanillaMenuBlocked = true;
                environment.VanillaMenuReason = "unavailable";
            }

            PopulateDynamicBlueprintState(environment);
            return environment;
        }

        private static void PopulateDynamicBlueprintState(BlueprintHandheldActionBarEnvironment environment)
        {
            if (environment == null)
            {
                return;
            }

            var creation = BlueprintCreationMaskState.GetSnapshot();
            environment.BlueprintCreationActive = creation != null && creation.Active;
            environment.BlueprintCreationSelectedCount = creation == null ? 0 : creation.SelectedCount;
            environment.BlueprintCreationCompletedPendingCapture = creation != null && creation.CompletedPendingCapture;
            environment.BlueprintCreationHasPendingSelection =
                creation != null &&
                (creation.CompletedPendingCapture || creation.SelectedCount > 0);

            var placed = BlueprintPlacedInstanceUiState.GetCachedSummary();
            var projection = BlueprintProjectionService.GetDiagnostics();
            var placedCount = Math.Max(
                placed == null ? 0 : placed.InstanceCount,
                projection == null ? 0 : projection.InstanceCount);
            environment.BlueprintPlacedInstanceCount = placedCount;
            environment.BlueprintHasPlacedInstances = placedCount > 0;
        }

        private static void UpdateInputGuard(string source, bool allowCommand)
        {
            try
            {
                var gameState = GameStateReader.LastSnapshot;
                var frame = BuildFrame(RuntimeSettingsSnapshotProvider.GetCurrent(), gameState, ReadEnvironment(gameState));
                var raw = DiagnosticMouseStateReader.Read();
                var mouse = LegacyUiInput.ReadMouseForOverlay(raw, LegacyMainUiScale.Resolve(raw));
                var interaction = BlueprintHandheldActionBarState.HandlePointer(
                    frame,
                    new BlueprintHandheldActionBarPointerInput
                    {
                        MouseX = mouse.X,
                        MouseY = mouse.Y,
                        LeftDown = mouse.LeftDown,
                        ScrollDelta = mouse.ScrollDelta,
                        ReadAvailable = mouse.ReadAvailable,
                        AllowCommand = allowCommand
                    });

                var captured = false;
                if (interaction.ShouldCaptureMouse)
                {
                    captured = mouse.LeftDown
                        ? UiMouseCaptureService.CaptureForOperationWindowPreserveMouseButtons()
                        : UiMouseCaptureService.CaptureForOperationWindow();
                }

                if (interaction.ShouldConsumeLeftInput)
                {
                    UiMouseCaptureService.ConsumeMouseTriggerForOperationWindow("MouseLeft", out _);
                }

                if (interaction.ShouldConsumeScroll)
                {
                    UiMouseCaptureService.ConsumeScrollForOperationWindow();
                }

                if (interaction.Clicked)
                {
                    EnqueuePlaceholderCommand(frame, mouse, interaction, captured);
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError(source, error);
                LogThrottle.ErrorThrottled(
                    "blueprint-handheld-action-bar-input-guard-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintHandheldActionBarOverlay",
                    "Blueprint handheld action bar input guard failed; exception swallowed.", error);
            }
        }

        private static void EnqueuePlaceholderCommand(
            BlueprintHandheldActionBarFrame frame,
            LegacyMouseSnapshot mouse,
            BlueprintHandheldActionBarInteraction interaction,
            bool captured)
        {
            if (frame == null || interaction == null || string.IsNullOrWhiteSpace(interaction.HoveredButtonId))
            {
                return;
            }

            var element = new LegacyUiElement
            {
                Id = BlueprintHandheldActionBarState.BuildCommandElementId(interaction.HoveredButtonId),
                Label = interaction.ButtonLabel,
                Kind = "button",
                Rect = ResolveButtonRect(frame, interaction.HoveredButtonId),
                Enabled = IsButtonEnabled(frame, interaction.HoveredButtonId),
                IntValue = interaction.HeldItemType,
                TooltipLines = ResolveButtonTooltip(frame, interaction.HoveredButtonId)
            };
            LegacyUiInput.EnqueueClick(element, mouse, captured);
        }

        private static LegacyUiRect ResolveButtonRect(BlueprintHandheldActionBarFrame frame, string buttonId)
        {
            if (frame == null || frame.Buttons == null)
            {
                return new LegacyUiRect(0, 0, 0, 0);
            }

            for (var index = 0; index < frame.Buttons.Count; index++)
            {
                var button = frame.Buttons[index];
                if (button != null && string.Equals(button.Id, buttonId, StringComparison.Ordinal))
                {
                    return button.Rect;
                }
            }

            return frame.Bounds;
        }

        private static string[] ResolveButtonTooltip(BlueprintHandheldActionBarFrame frame, string buttonId)
        {
            var button = ResolveButtonFrame(frame, buttonId);
            if (button == null || string.IsNullOrWhiteSpace(button.Tooltip))
            {
                return null;
            }

            return new[] { button.Tooltip };
        }

        private static bool IsButtonEnabled(BlueprintHandheldActionBarFrame frame, string buttonId)
        {
            var button = ResolveButtonFrame(frame, buttonId);
            return button == null || button.Enabled;
        }

        private static BlueprintHandheldActionBarButtonFrame ResolveButtonFrame(BlueprintHandheldActionBarFrame frame, string buttonId)
        {
            if (frame == null || frame.Buttons == null)
            {
                return null;
            }

            for (var index = 0; index < frame.Buttons.Count; index++)
            {
                var button = frame.Buttons[index];
                if (button != null && string.Equals(button.Id, buttonId, StringComparison.Ordinal))
                {
                    return button;
                }
            }

            return null;
        }

        private static void DrawFrame(object spriteBatch, BlueprintHandheldActionBarFrame frame)
        {
            var bounds = frame.Bounds;
            DrawNotice(spriteBatch, frame, bounds);
            LegacyUiTheme.DrawPanel(spriteBatch, bounds);

            var buttons = frame.Buttons;
            for (var index = 0; index < buttons.Count; index++)
            {
                DrawButton(spriteBatch, buttons[index], bounds, frame);
            }
        }

        private static void DrawNotice(object spriteBatch, BlueprintHandheldActionBarFrame frame, LegacyUiRect bounds)
        {
            var notice = ResolveNotice(frame);
            if (frame == null || string.IsNullOrWhiteSpace(notice))
            {
                return;
            }

            var y = Math.Max(0, bounds.Y - 20);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                notice,
                bounds.X,
                y,
                bounds.Width,
                18,
                0,
                0,
                Math.Max(1, frame.ScreenWidth),
                Math.Max(1, frame.ScreenHeight),
                238,
                218,
                150,
                242,
                0.58f);
        }

        private static string ResolveNotice(BlueprintHandheldActionBarFrame frame)
        {
            var hovered = ResolveButtonFrame(frame, frame == null ? string.Empty : frame.HoveredButtonId);
            if (hovered != null && !string.IsNullOrWhiteSpace(hovered.Tooltip))
            {
                return hovered.Tooltip;
            }

            return frame == null ? string.Empty : frame.LastNotice;
        }

        private static void DrawButton(object spriteBatch, BlueprintHandheldActionBarButtonFrame button, LegacyUiRect clip, BlueprintHandheldActionBarFrame frame)
        {
            if (button == null)
            {
                return;
            }

            var rect = button.Rect;
            var enabled = button.Enabled;
            var pressed = enabled && frame != null && string.Equals(frame.PressedButtonId, button.Id, StringComparison.Ordinal);
            var hovered = frame != null && string.Equals(frame.HoveredButtonId, button.Id, StringComparison.Ordinal);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, pressed, false, enabled, clip);
            var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, false, enabled);
            var textScale = ResolveButtonTextScale(button.Label, Math.Max(1, rect.Width - 8));
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                button.Label,
                rect.X + 4,
                contentRect.Y,
                Math.Max(1, rect.Width - 8),
                contentRect.Height,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                enabled ? pressed ? LegacyUiTheme.SelectedTextR : 230 : 150,
                enabled ? pressed ? LegacyUiTheme.SelectedTextG : 232 : 156,
                enabled ? pressed ? LegacyUiTheme.SelectedTextB : 224 : 170,
                enabled ? 255 : 210,
                textScale);
        }

        private static float ResolveButtonTextScale(string label, int availableWidth)
        {
            var scale = ButtonTextScale;
            var safeWidth = Math.Max(1, availableWidth);
            while (scale > MinimumButtonTextScale && UiTextRenderer.EstimateTextWidth(label ?? string.Empty, scale) > safeWidth)
            {
                scale -= 0.02f;
            }

            return scale < MinimumButtonTextScale ? MinimumButtonTextScale : scale;
        }
    }
}
