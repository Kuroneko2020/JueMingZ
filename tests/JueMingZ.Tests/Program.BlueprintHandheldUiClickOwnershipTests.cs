using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintHandheldUiClickOwnershipContractsStayWired()
        {
            BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits();
            UiPointerOwnershipConsumedLeftSurvivesCaptureReset();
            BlueprintUiPointerOwnershipBlocksWorldOverlayClicks();
            BlueprintHandheldActionBarInputCapturesOnlyInsideBar();
            BlueprintHandheldActionBarAfterPlayerInputGuardSubmitsFreshClickEdge();
            BlueprintHandheldActionBarPostfixReplaysStalePrefixPress();
            BlueprintHandheldActionBarRealCommandsAndUnimplementedButtons();
            BlueprintHandheldActionBarDiagnosticsSnapshotJson();
        }

        private static void BlueprintHotbarDeadClickRegressionContractsStayWired()
        {
            BlueprintHandheldActionBarUiScaleFrameAndMouseHitSameBottomBar();
            BlueprintHandheldUiClickOwnershipContractsStayWired();
            BlueprintHotbarDeadClickHotkeyPathStaysIndependentOfHandheldOwnership();
        }

        private static void BlueprintHotbarDeadClickHotkeyPathStaysIndependentOfHandheldOwnership()
        {
            BlueprintEntryHotkeyService.ResetForTesting();
            BlueprintEntryState.ResetForTesting();
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            UiPointerOwnershipService.ResetForTesting();
            try
            {
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-hotbar-dead-click-hotkey");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-handheld-action-bar:create",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(1, 2, 3, 4),
                    true,
                    true,
                    false,
                    "left");

                var settings = AppSettings.CreateDefault();
                var hotkeys = HotkeySettings.CreateDefault();
                hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintCreateAction] = "G";

                var result = BlueprintEntryHotkeyService.TickForTesting(
                    settings,
                    hotkeys,
                    new Dictionary<int, bool> { ['G'] = true },
                    true,
                    string.Empty,
                    false);
                if (!result.Triggered ||
                    !result.Applied ||
                    !string.Equals(result.Chord, "G", StringComparison.Ordinal) ||
                    !string.Equals(result.Action, BlueprintEntryCommands.StartCreate, StringComparison.Ordinal) ||
                    !string.Equals(result.ResultCode, "entryStateChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected G / Hotkey.BlueprintAction to keep entering blueprint creation without relying on handheld action-bar ownership.");
                }

                AssertStringEquals(
                    ScenarioNames.BlueprintActionHotkey,
                    "Hotkey.BlueprintAction",
                    "blueprint hotbar dead-click adjacent hotkey scenario");
                AssertStringEquals(
                    BlueprintEntryState.GetSnapshot(settings).Mode,
                    BlueprintEntryModes.Creating,
                    "blueprint action hotkey keeps creating-mode path independent of handheld UI");

                var held = BlueprintEntryHotkeyService.TickForTesting(
                    settings,
                    hotkeys,
                    new Dictionary<int, bool> { ['G'] = true },
                    true,
                    string.Empty,
                    false);
                if (held.Triggered)
                {
                    throw new InvalidOperationException("Expected held G / Hotkey.BlueprintAction to debounce independently from handheld action-bar click handling.");
                }
            }
            finally
            {
                BlueprintEntryHotkeyService.ResetForTesting();
                BlueprintEntryState.ResetForTesting();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                UiPointerOwnershipService.ResetForTesting();
                ResetUiInputFrameTestState();
            }
        }

        private static void BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits()
        {
            ResetUiInputFrameTestState();
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            UiPointerOwnershipService.ResetForTesting();
            try
            {
                var rawOsLeft = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsLeftDown = true
                };

                var enabledFrame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
                var enabledButton = enabledFrame.Buttons[0];
                AssertHandheldOwnershipConsumesWorldLeft(
                    "enabled button",
                    enabledFrame,
                    BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                        enabledFrame,
                        BlueprintHandheldPointer(enabledButton.Rect.CenterX, enabledButton.Rect.CenterY, true, 0, true)),
                    rawOsLeft);

                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                var disabledFrame = BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true));
                var disabledSave = disabledFrame.Buttons[0];
                var disabledInteraction = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                    disabledFrame,
                    BlueprintHandheldPointer(disabledSave.Rect.CenterX, disabledSave.Rect.CenterY, true, 0, true));
                if (disabledInteraction.Clicked)
                {
                    throw new InvalidOperationException("Expected disabled save ownership registration test to avoid command click.");
                }

                AssertHandheldOwnershipConsumesWorldLeft("disabled save", disabledFrame, disabledInteraction, rawOsLeft);

                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                var blankFrame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
                var blankInteraction = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                    blankFrame,
                    BlueprintHandheldPointer(blankFrame.Bounds.X + 1, blankFrame.Bounds.CenterY, true, 0, true));
                if (blankInteraction.Clicked)
                {
                    throw new InvalidOperationException("Expected blank handheld panel ownership registration test to avoid command click.");
                }

                AssertHandheldOwnershipConsumesWorldLeft("blank panel", blankFrame, blankInteraction, rawOsLeft);

                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                UiPointerOwnershipService.ResetForTesting();
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-handheld-owner-outside");
                var outsideFrame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
                var outsideInteraction = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                    outsideFrame,
                    BlueprintHandheldPointer(0, 0, true, 0, true));
                var outsideSnapshot = BlueprintHandheldActionBarOverlay.RegisterPointerOwnershipForTesting(
                    outsideFrame,
                    outsideInteraction,
                    true);
                if (outsideSnapshot.PointerOwned || !UiPointerOwnershipService.ResolveWorldLeftDown(rawOsLeft))
                {
                    throw new InvalidOperationException("Expected outside handheld clicks to leave world overlay left input available.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                UiPointerOwnershipService.ResetForTesting();
            }
        }

        private static void AssertHandheldOwnershipConsumesWorldLeft(
            string scenario,
            BlueprintHandheldActionBarFrame frame,
            BlueprintHandheldActionBarInteraction interaction,
            DiagnosticMouseState rawOsLeft)
        {
            if (interaction == null ||
                !interaction.ShouldCaptureMouse ||
                !interaction.ShouldConsumeLeftInput)
            {
                throw new InvalidOperationException("Expected " + scenario + " to capture and consume handheld left input.");
            }

            UiPointerOwnershipService.ResetForTesting();
            UiInputFrameClock.BeginUpdateFrame("test.blueprint-handheld-owner-" + scenario.Replace(" ", "-"));
            var snapshot = BlueprintHandheldActionBarOverlay.RegisterPointerOwnershipForTesting(frame, interaction, true);
            if (!snapshot.PointerOwned ||
                !snapshot.LeftOwned ||
                !snapshot.LeftConsumed ||
                !string.Equals(snapshot.OwnerKind, "BlueprintHandheldActionBar", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + scenario + " ownership registration to mark left consumed before compat trigger cleanup.");
            }

            if (UiPointerOwnershipService.ResolveWorldLeftDown(rawOsLeft))
            {
                throw new InvalidOperationException("Expected " + scenario + " ownership registration to block OS-left revival for world overlays.");
            }
        }
    }
}
