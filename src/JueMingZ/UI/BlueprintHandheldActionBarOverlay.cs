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
        private const string VisualContract = "physical-screen-bottom-action-bar+dynamic-buttons+legacy-ui-theme+vanilla-ui-skin+button-text-scale-0.78+create-enters-mask+exit-create-preserves-mask+save-captures-mask+clear-selection+disabled-save-tooltip+open-library-real+unimplemented-buttons-ui-only+mouse-consume+no-blueprint-refresh+no-library-refresh+no-input-action-queue";
        internal const float ButtonTextScale = 0.78f;
        private const float MinimumButtonTextScale = 0.52f;

        public static bool DrawInterfaceLayer()
        {
            try
            {
                var raw = DiagnosticMouseStateReader.ReadForBlueprintHandheldActionBarOverlay();
                var frame = BuildFrame(RuntimeSettingsSnapshotProvider.GetCurrent(), GameStateReader.LastSnapshot, ReadEnvironment(GameStateReader.LastSnapshot, raw));
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
            UpdateInputGuard("BlueprintHandheldActionBarOverlay.UpdatePrefixGuard", true, false);
        }

        public static void UpdateAfterPlayerInputGuard()
        {
            UpdateInputGuard("BlueprintHandheldActionBarOverlay.UpdateAfterPlayerInputGuard", true, true);
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

        internal static BlueprintHandheldActionBarFrame BuildFrameForTesting(
            RuntimeSettingsSnapshot settings,
            GameStateSnapshot gameState,
            BlueprintHandheldActionBarEnvironment environment,
            DiagnosticMouseState raw)
        {
            return BuildFrame(settings, gameState, ResolveFrameEnvironment(environment, raw));
        }

        internal static BlueprintHandheldActionBarInteraction HandlePointerForTesting(
            BlueprintHandheldActionBarFrame frame,
            BlueprintHandheldActionBarPointerInput input)
        {
            return BlueprintHandheldActionBarState.HandlePointer(frame, input);
        }

        internal static UiPointerOwnershipSnapshot RegisterPointerOwnershipForTesting(
            BlueprintHandheldActionBarFrame frame,
            BlueprintHandheldActionBarInteraction interaction,
            bool mouseLeftDown)
        {
            RegisterPointerOwnership(
                frame,
                new LegacyMouseSnapshot { LeftDown = mouseLeftDown },
                interaction);
            return UiPointerOwnershipService.GetSnapshotForTesting();
        }

        private static BlueprintHandheldActionBarFrame BuildFrame(
            RuntimeSettingsSnapshot settings,
            GameStateSnapshot gameState,
            BlueprintHandheldActionBarEnvironment environment)
        {
            return BlueprintHandheldActionBarState.BuildFrame(settings, gameState, environment);
        }

        private static BlueprintHandheldActionBarEnvironment ReadEnvironment(GameStateSnapshot gameState, DiagnosticMouseState raw)
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
            return ResolveFrameEnvironment(environment, raw);
        }

        private static BlueprintHandheldActionBarEnvironment ResolveFrameEnvironment(
            BlueprintHandheldActionBarEnvironment environment,
            DiagnosticMouseState raw)
        {
            if (environment == null)
            {
                return null;
            }

            var resolved = new BlueprintHandheldActionBarEnvironment
            {
                WorldReady = environment.WorldReady,
                GameInputAvailable = environment.GameInputAvailable,
                VanillaMenuReadAvailable = environment.VanillaMenuReadAvailable,
                VanillaMenuBlocked = environment.VanillaMenuBlocked,
                VanillaMenuReason = environment.VanillaMenuReason,
                MapFullscreenOpen = environment.MapFullscreenOpen,
                LegacyMainUiVisible = environment.LegacyMainUiVisible,
                BlueprintCreationActive = environment.BlueprintCreationActive,
                BlueprintCreationHasPendingSelection = environment.BlueprintCreationHasPendingSelection,
                BlueprintCreationCompletedPendingCapture = environment.BlueprintCreationCompletedPendingCapture,
                BlueprintCreationSelectedCount = environment.BlueprintCreationSelectedCount,
                BlueprintHasPlacedInstances = environment.BlueprintHasPlacedInstances,
                BlueprintPlacedInstanceCount = environment.BlueprintPlacedInstanceCount,
                ScreenWidth = environment.ScreenWidth,
                ScreenHeight = environment.ScreenHeight
            };

            // This overlay draws directly into the active interface SpriteBatch
            // without pairing the F5 UiDrawTransform. Its visual layout target is
            // therefore the draw/client screen extent, not ScreenWidth / UIScale.
            return resolved;
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

        private static void UpdateInputGuard(string source, bool allowCommand, bool afterPlayerInput)
        {
            try
            {
                var gameState = GameStateReader.LastSnapshot;
                var raw = DiagnosticMouseStateReader.ReadForBlueprintHandheldActionBarOverlay();
                var frame = BuildFrame(RuntimeSettingsSnapshotProvider.GetCurrent(), gameState, ReadEnvironment(gameState, raw));
                var mouse = LegacyUiInput.ReadMouseForBlueprintHandheldOverlay(raw, LegacyMainUiScale.Resolve(raw));
                var pointerInput = new BlueprintHandheldActionBarPointerInput
                {
                    MouseX = mouse.X,
                    MouseY = mouse.Y,
                    LeftDown = mouse.LeftDown,
                    ScrollDelta = mouse.ScrollDelta,
                    MouseReadMode = mouse.ReadMode,
                    ReadAvailable = mouse.ReadAvailable,
                    AllowCommand = allowCommand,
                    AfterPlayerInput = afterPlayerInput
                };
                var interaction = BlueprintHandheldActionBarState.HandlePointer(frame, pointerInput);
                BlueprintUiClickDiagnostics.RecordHandheldInput(source, frame, pointerInput, interaction);

                RegisterPointerOwnership(frame, mouse, interaction);

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

        private static void RegisterPointerOwnership(
            BlueprintHandheldActionBarFrame frame,
            LegacyMouseSnapshot mouse,
            BlueprintHandheldActionBarInteraction interaction)
        {
            if (frame == null ||
                interaction == null ||
                (!interaction.ShouldCaptureMouse && !interaction.ShouldConsumeLeftInput && !interaction.ShouldConsumeScroll))
            {
                return;
            }

            var hoveredId = interaction.HoveredButtonId ?? string.Empty;
            var ownerId = BlueprintHandheldActionBarState.BuildPointerOwnerId(hoveredId);
            var reason = interaction.ShouldConsumeLeftInput
                ? BlueprintHandheldActionBarState.PointerOwnershipReasonLeft
                : interaction.ShouldConsumeScroll
                    ? BlueprintHandheldActionBarState.PointerOwnershipReasonScroll
                    : BlueprintHandheldActionBarState.PointerOwnershipReasonHover;
            UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                ownerId,
                "BlueprintHandheldActionBar",
                frame.Bounds,
                interaction.ShouldConsumeLeftInput || (mouse != null && mouse.LeftDown),
                interaction.ShouldConsumeLeftInput,
                interaction.ShouldConsumeScroll,
                reason);
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
