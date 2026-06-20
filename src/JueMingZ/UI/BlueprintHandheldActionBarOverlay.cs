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
        private const string VisualContract = "ui-scale-bottom-action-bar+five-buttons+ui-only-placeholder-click+mouse-consume+no-blueprint-refresh+no-input-action-queue";

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

            return environment;
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
                Enabled = true,
                IntValue = interaction.HeldItemType
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

        private static void DrawFrame(object spriteBatch, BlueprintHandheldActionBarFrame frame)
        {
            var bounds = frame.Bounds;
            DrawNotice(spriteBatch, frame, bounds);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, 6, 58, 72, 94, 218);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4, 5, 24, 29, 42, 226);

            var buttons = frame.Buttons;
            for (var index = 0; index < buttons.Count; index++)
            {
                DrawButton(spriteBatch, buttons[index], bounds, frame);
            }
        }

        private static void DrawNotice(object spriteBatch, BlueprintHandheldActionBarFrame frame, LegacyUiRect bounds)
        {
            if (frame == null || string.IsNullOrWhiteSpace(frame.LastNotice))
            {
                return;
            }

            var y = Math.Max(0, bounds.Y - 20);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                frame.LastNotice,
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

        private static void DrawButton(object spriteBatch, BlueprintHandheldActionBarButtonFrame button, LegacyUiRect clip, BlueprintHandheldActionBarFrame frame)
        {
            if (button == null)
            {
                return;
            }

            var rect = button.Rect;
            var pressed = frame != null && string.Equals(frame.PressedButtonId, button.Id, StringComparison.Ordinal);
            var hovered = frame != null && string.Equals(frame.HoveredButtonId, button.Id, StringComparison.Ordinal);
            var borderAlpha = pressed ? 242 : hovered ? 232 : 214;
            var bodyAlpha = pressed ? 238 : hovered ? 232 : 222;
            var bodyR = pressed ? 48 : hovered ? 42 : 34;
            var bodyG = pressed ? 58 : hovered ? 52 : 42;
            var bodyB = pressed ? 78 : hovered ? 72 : 62;
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 4, 92, 110, 146, borderAlpha);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, 3, bodyR, bodyG, bodyB, bodyAlpha);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                button.Label,
                rect.X + 2,
                rect.Y,
                Math.Max(1, rect.Width - 4),
                rect.Height,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                242,
                238,
                220,
                248,
                0.58f);
        }
    }
}
