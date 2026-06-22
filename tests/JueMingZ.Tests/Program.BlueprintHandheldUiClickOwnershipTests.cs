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
            BlueprintWorldOverlayOwnershipDiagnosticsIncludeSnapshotDetails();
            BlueprintCreationPointerOwnershipNarrowingKeepsWorldHoverAndMask();
            BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits();
            UiPointerOwnershipConsumedLeftSurvivesCaptureReset();
            BlueprintUiPointerOwnershipBlocksWorldOverlayClicks();
            BlueprintHandheldActionBarInputCapturesOnlyInsideBar();
            BlueprintHandheldActionBarAfterPlayerInputGuardSubmitsFreshClickEdge();
            BlueprintHandheldActionBarPostfixReplaysStalePrefixPress();
            BlueprintHandheldActionBarRealCommandsAndUnimplementedButtons();
            BlueprintHandheldActionBarDiagnosticsSnapshotJson();
        }

        private static void BlueprintWorldOverlayOwnershipDiagnosticsIncludeSnapshotDetails()
        {
            ResetUiInputFrameTestState();
            BlueprintUiClickDiagnostics.ResetForTesting();
            try
            {
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-world-overlay-owner-details");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-hover-owner",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(100, 200, 80, 48),
                    false,
                    false,
                    false,
                    "hover");

                var rawInside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 120,
                    TerrariaMouseY = 220,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 600,
                    OsClientMouseY = 700,
                    OsLeftDown = true,
                    ReadMode = "TestWorld"
                };
                var inside = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawInside);
                if (!inside.PointerOwned ||
                    inside.LeftOwned ||
                    inside.LeftConsumed ||
                    inside.ScrollOwned ||
                    !inside.HasBounds ||
                    !inside.BoundsHit ||
                    !inside.MouseAvailable ||
                    inside.MouseX != 120 ||
                    inside.MouseY != 220 ||
                    !string.Equals(inside.MouseSource, "Terraria", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected hover-only pointer ownership details to report same-domain owner bounds hit without left consumption.");
                }

                if (!UiPointerOwnershipService.IsPointerOwnerBoundsHitThisFrame(rawInside) ||
                    !UiPointerOwnershipService.ResolveWorldLeftDown(rawInside))
                {
                    throw new InvalidOperationException("Expected hover-only pointer ownership to expose bounds hit while leaving OS left available to world overlays.");
                }

                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "creation",
                    "prefix",
                    true,
                    rawInside,
                    false,
                    false,
                    UiPointerOwnershipService.IsPointerOwnedThisFrame(),
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawInside),
                    false,
                    true,
                    4211,
                    572);
                var trace = BlueprintUiClickDiagnostics.GetSnapshot().WorldOverlayInputTrace;
                AssertContains(trace, "pointerOwnerId=blueprint-hover-owner");
                AssertContains(trace, "pointerOwnerKind=BlueprintHandheldActionBar");
                AssertContains(trace, "pointerOwnerReason=hover");
                AssertContains(trace, "pointerOwnerHasBounds=true");
                AssertContains(trace, "pointerOwnerBounds=100,200,80,48");
                AssertContains(trace, "pointerOwnerMouse=120,220");
                AssertContains(trace, "pointerOwnerMouseSource=Terraria");
                AssertContains(trace, "pointerOwnerBoundsHit=true");
                AssertContains(trace, "pointerLeftOwned=false");
                AssertContains(trace, "pointerLeftConsumed=false");
                AssertContains(trace, "pointerScrollOwned=false");

                var rawOutside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 12,
                    TerrariaMouseY = 34,
                    OsReadAvailable = true,
                    OsClientMouseX = 120,
                    OsClientMouseY = 220,
                    OsLeftDown = true
                };
                var outside = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawOutside);
                if (!outside.PointerOwned ||
                    outside.BoundsHit ||
                    UiPointerOwnershipService.IsPointerOwnerBoundsHitThisFrame(rawOutside))
                {
                    throw new InvalidOperationException("Expected ownership query to distinguish pointer-owned frames from current mouse bounds hits.");
                }

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-world-overlay-owner-nobounds");
                UiPointerOwnershipService.EnsureOperationWindowPointerOwned("capture-window");
                var rawOsInside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 0,
                    OsClientMouseY = 0,
                    OsLeftDown = true
                };
                var noBounds = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawOsInside);
                if (!noBounds.PointerOwned ||
                    !noBounds.LeftOwned ||
                    noBounds.LeftConsumed ||
                    noBounds.HasBounds ||
                    noBounds.BoundsHit ||
                    !noBounds.MouseAvailable ||
                    !string.Equals(noBounds.MouseSource, "OsClient", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected operation-window ownership to report no bounds instead of fabricating a bounds hit.");
                }

                UiPointerOwnershipService.MarkOperationWindowLeftConsumed("left");
                var consumed = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawOsInside);
                if (!consumed.LeftConsumed || UiPointerOwnershipService.ResolveWorldLeftDown(rawOsInside))
                {
                    throw new InvalidOperationException("Expected left-consumed ownership to keep blocking OS-left revival for world overlays.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintUiClickDiagnostics.ResetForTesting();
            }
        }

        private static void BlueprintPlacementErasePointerOwnershipNarrowingKeepsWorldInput()
        {
            ResetUiInputFrameTestState();
            BlueprintUiClickDiagnostics.ResetForTesting();
            BlueprintPlacementPreviewState.ResetForTesting();
            BlueprintEraseRegionState.ResetForTesting();
            var restore = PushTemporaryConfigDirectory("blueprint-adjacent-overlay-ownership-narrow");
            try
            {
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-adjacent-overlay-owner-outside");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-hover-owner",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(500, 500, 80, 48),
                    false,
                    false,
                    false,
                    "hover");

                var rawOutside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 32,
                    TerrariaMouseY = 32,
                    OsReadAvailable = true,
                    OsClientMouseX = 32,
                    OsClientMouseY = 32,
                    OsLeftDown = true,
                    ReadMode = "TestWorld"
                };
                var outside = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawOutside);
                var creationBlocksOutside = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(outside);
                var placementBlocksOutside = BlueprintPlacementPreviewOverlay.ShouldBlockPlacementForPointerOwnershipForTesting(outside);
                var eraseBlocksOutside = BlueprintEraseRegionOverlay.ShouldBlockEraseForPointerOwnershipForTesting(outside);
                if (!outside.PointerOwned ||
                    outside.LeftConsumed ||
                    outside.BoundsHit ||
                    creationBlocksOutside ||
                    placementBlocksOutside ||
                    eraseBlocksOutside)
                {
                    throw new InvalidOperationException("Expected out-of-bounds hover-only pointer ownership not to block adjacent blueprint world overlays.");
                }

                var outsideLeftDown = UiPointerOwnershipService.ResolveWorldLeftDown(rawOutside);
                if (!outsideLeftDown)
                {
                    throw new InvalidOperationException("Expected hover-only pointer ownership to leave world left input available for adjacent overlays.");
                }

                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "placement",
                    "prefix",
                    true,
                    rawOutside,
                    false,
                    false,
                    outside.PointerOwned,
                    outsideLeftDown,
                    false,
                    true,
                    44,
                    55,
                    false);
                var placementTrace = BlueprintUiClickDiagnostics.GetSnapshot().WorldOverlayInputTrace;
                AssertContains(placementTrace, "overlay=placement");
                AssertContains(placementTrace, "pointerUiOwned=true");
                AssertContains(placementTrace, "uiOwned=false");
                AssertContains(placementTrace, "pointerOwnerBoundsHit=false");

                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "erase",
                    "prefix",
                    true,
                    rawOutside,
                    false,
                    false,
                    outside.PointerOwned,
                    outsideLeftDown,
                    false,
                    true,
                    10,
                    20,
                    false);
                var eraseTrace = BlueprintUiClickDiagnostics.GetSnapshot().WorldOverlayInputTrace;
                AssertContains(eraseTrace, "overlay=erase");
                AssertContains(eraseTrace, "pointerUiOwned=true");
                AssertContains(eraseTrace, "uiOwned=false");
                AssertContains(eraseTrace, "pointerOwnerBoundsHit=false");

                var template = CreateEvenBlueprintTemplate("相邻 overlay 放行");
                var placementStore = new BlueprintWorldInstanceStore();
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    new BlueprintTemplateLibraryStore(),
                    placementStore,
                    BlueprintPlacementWorldContext.Success("pair-placement-narrow", "world-placement-narrow"));
                var preview = BlueprintPlacementPreviewState.BeginPreview(template, "test");
                if (!preview.Succeeded)
                {
                    throw new InvalidOperationException("Expected placement preview to begin for adjacent ownership narrowing test.");
                }

                var placement = BlueprintPlacementPreviewState.HandlePointer(
                    BlueprintPlacementPreviewOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        placementBlocksOutside,
                        outsideLeftDown,
                        outsideLeftDown,
                        false,
                        true,
                        44,
                        55));
                if (!placement.PlacedInstance || placement.Instance == null)
                {
                    throw new InvalidOperationException("Expected out-of-bounds hover-only ownership to allow placement confirmation.");
                }

                BlueprintPlacementPreviewState.ResetForTesting();

                var eraseStore = new BlueprintWorldInstanceStore();
                BlueprintWorldInstanceRecord eraseInstance;
                RequireBlueprintSuccess(
                    eraseStore.CreateInstanceFromTemplate(
                        "pair-erase-narrow",
                        "world-erase-narrow",
                        CreateEraseMaterialTemplate(),
                        10,
                        20,
                        0,
                        out eraseInstance),
                    "create erase narrowing instance");
                BlueprintEraseRegionState.SetDependenciesForTesting(
                    eraseStore,
                    BlueprintPlacementWorldContext.Success("pair-erase-narrow", "world-erase-narrow"));
                var beginErase = BlueprintEraseRegionState.BeginErase(eraseInstance.InstanceId);
                if (!beginErase.Succeeded)
                {
                    throw new InvalidOperationException("Expected erase mode to begin for adjacent ownership narrowing test.");
                }

                var eraseStart = BlueprintEraseRegionState.HandlePointer(
                    BlueprintEraseRegionOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        eraseBlocksOutside,
                        outsideLeftDown,
                        outsideLeftDown,
                        false,
                        true,
                        10,
                        20));
                if (!eraseStart.ShouldConsumeLeftInput ||
                    eraseStart.ErasedRegion ||
                    !BlueprintEraseRegionState.GetSnapshot().Dragging)
                {
                    throw new InvalidOperationException("Expected out-of-bounds hover-only ownership to allow erase drag start without applying an erase region yet.");
                }

                BlueprintEraseRegionState.ResetForTesting();

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-adjacent-overlay-owner-inside");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-hover-owner",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(20, 20, 80, 48),
                    false,
                    false,
                    false,
                    "hover");
                var rawInside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 32,
                    TerrariaMouseY = 32,
                    OsReadAvailable = true,
                    OsClientMouseX = 32,
                    OsClientMouseY = 32,
                    OsLeftDown = true,
                    ReadMode = "TestWorld"
                };
                var inside = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawInside);
                var creationBlocksInside = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(inside);
                var placementBlocksInside = BlueprintPlacementPreviewOverlay.ShouldBlockPlacementForPointerOwnershipForTesting(inside);
                var eraseBlocksInside = BlueprintEraseRegionOverlay.ShouldBlockEraseForPointerOwnershipForTesting(inside);
                if (!inside.BoundsHit ||
                    !creationBlocksInside ||
                    !placementBlocksInside ||
                    !eraseBlocksInside)
                {
                    throw new InvalidOperationException("Expected current owner-bounds hits to block all blueprint world overlays consistently.");
                }

                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    new BlueprintTemplateLibraryStore(),
                    new BlueprintWorldInstanceStore(),
                    BlueprintPlacementWorldContext.Success("pair-placement-inside", "world-placement-inside"));
                BlueprintPlacementPreviewState.BeginPreview(template, "test");
                var placementBlocked = BlueprintPlacementPreviewState.HandlePointer(
                    BlueprintPlacementPreviewOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        placementBlocksInside,
                        UiPointerOwnershipService.ResolveWorldLeftDown(rawInside),
                        true,
                        false,
                        true,
                        45,
                        56));
                if (!placementBlocked.ShouldConsumeLeftInput ||
                    placementBlocked.PlacedInstance ||
                    !BlueprintPlacementPreviewState.GetSnapshot().Active)
                {
                    throw new InvalidOperationException("Expected owner-bounds hits to block placement confirmation without exiting preview.");
                }

                var eraseInsideStore = new BlueprintWorldInstanceStore();
                BlueprintWorldInstanceRecord eraseInsideInstance;
                RequireBlueprintSuccess(
                    eraseInsideStore.CreateInstanceFromTemplate(
                        "pair-erase-inside",
                        "world-erase-inside",
                        CreateEraseMaterialTemplate(),
                        10,
                        20,
                        0,
                        out eraseInsideInstance),
                    "create erase bounds-hit instance");
                BlueprintEraseRegionState.SetDependenciesForTesting(
                    eraseInsideStore,
                    BlueprintPlacementWorldContext.Success("pair-erase-inside", "world-erase-inside"));
                BlueprintEraseRegionState.BeginErase(eraseInsideInstance.InstanceId);
                var eraseBlocked = BlueprintEraseRegionState.HandlePointer(
                    BlueprintEraseRegionOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        eraseBlocksInside,
                        UiPointerOwnershipService.ResolveWorldLeftDown(rawInside),
                        true,
                        false,
                        true,
                        10,
                        20));
                if (!eraseBlocked.ShouldConsumeLeftInput ||
                    eraseBlocked.ErasedRegion ||
                    BlueprintEraseRegionState.GetSnapshot().Dragging)
                {
                    throw new InvalidOperationException("Expected owner-bounds hits to block erase drag start.");
                }

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-adjacent-overlay-owner-consumed");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-left-consumed",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(500, 500, 80, 48),
                    true,
                    true,
                    false,
                    "left");
                var rawConsumed = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 32,
                    TerrariaMouseY = 32,
                    OsReadAvailable = true,
                    OsClientMouseX = 32,
                    OsClientMouseY = 32,
                    OsLeftDown = true
                };
                var consumed = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawConsumed);
                if (!consumed.LeftConsumed ||
                    !BlueprintPlacementPreviewOverlay.ShouldBlockPlacementForPointerOwnershipForTesting(consumed) ||
                    !BlueprintEraseRegionOverlay.ShouldBlockEraseForPointerOwnershipForTesting(consumed) ||
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawConsumed))
                {
                    throw new InvalidOperationException("Expected left-consumed ownership to keep blocking OS-left revival for placement and erase.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintUiClickDiagnostics.ResetForTesting();
                BlueprintPlacementPreviewState.ResetForTesting();
                BlueprintEraseRegionState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintWorldOverlayPointerOwnershipContractsStayWired()
        {
            BlueprintHandheldUiClickOwnershipContractsStayWired();
            BlueprintPlacementErasePointerOwnershipNarrowingKeepsWorldInput();
            BlueprintHotbarDeadClickHotkeyPathStaysIndependentOfHandheldOwnership();
        }

        private static void BlueprintHotbarDeadClickRegressionContractsStayWired()
        {
            BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired();
        }

        private static void BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired()
        {
            BlueprintHandheldActionBarPhysicalBottomCenterRejectsUiScaleLogicalExtent();
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
