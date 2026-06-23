using System;
using System.Collections.Generic;
using JueMingZ.Actions;
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
            BlueprintPointerOwnershipSemanticsSplitBlocksWorldLeftNotHover();
            BlueprintWorldOverlayOwnershipDiagnosticsIncludeSnapshotDetails();
            BlueprintHandheldOwnerBoundsUseOsClientCoordinateDomain();
            BlueprintCreationWorldOverlayPhaseTraceSlotsKeepPrefixAndAfter();
            BlueprintCreationClearReasonTraceRecordsStateAndCoordinates();
            BlueprintCreationPointerOwnershipNarrowingKeepsWorldHoverAndMask();
            BlueprintCreationPhysicalLeftEdgesIgnoreConsumedWorldLeft();
            BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits();
            UiPointerOwnershipConsumedLeftSurvivesCaptureReset();
            BlueprintUiPointerOwnershipBlocksWorldOverlayClicks();
            BlueprintHandheldActionBarInputCapturesOnlyInsideBar();
            BlueprintHandheldActionBarAfterPlayerInputGuardSubmitsFreshClickEdge();
            BlueprintHandheldActionBarPostfixReplaysStalePrefixPress();
            BlueprintHandheldActionBarRealCommandsAndDeferredPlacedCommands();
            BlueprintHandheldActionBarDiagnosticsSnapshotJson();
        }

        private static void BlueprintCreationDiagnosticContractsStayWired()
        {
            BlueprintCreationWorldOverlayPhaseTraceSlotsKeepPrefixAndAfter();
            BlueprintCreationClearReasonTraceRecordsStateAndCoordinates();
            BlueprintCreationPhysicalLeftEdgesIgnoreConsumedWorldLeft();
            BlueprintCreationActionMetadataCarriesClearTrace();
            BlueprintWorldOverlayPointerOwnershipContractsStayWired();
            BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired();
        }

        private static void BlueprintCreationFlickerFixContractsStayWired()
        {
            BlueprintPointerOwnershipSemanticsSplitBlocksWorldLeftNotHover();
            BlueprintCreationPhysicalLeftEdgesIgnoreConsumedWorldLeft();
            BlueprintHandheldOwnerBoundsUseOsClientCoordinateDomain();
            BlueprintWorldOverlayOwnershipDiagnosticsIncludeSnapshotDetails();
            BlueprintWorldOverlayPointerOwnershipContractsStayWired();
            BlueprintCreationDiagnosticContractsStayWired();
        }

        private static void BlueprintPointerOwnershipSemanticsSplitBlocksWorldLeftNotHover()
        {
            ResetUiInputFrameTestState();
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintUiClickDiagnostics.ResetForTesting();
            try
            {
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-pointer-semantics-consumed-left");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-left-consumed",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(500, 500, 80, 48),
                    true,
                    true,
                    false,
                    "left");

                var rawConsumedOutside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 32,
                    TerrariaMouseY = 32,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 32,
                    OsClientMouseY = 32,
                    OsLeftDown = true,
                    ReadMode = "TestPointerSemanticsConsumed"
                };
                var consumedOutside = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawConsumedOutside);
                var consumedCreationBlock = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(consumedOutside);
                if (!consumedOutside.PointerOwned ||
                    !consumedOutside.PointerBlocksWorldLeft ||
                    consumedOutside.PointerBlocksHoverOrDrag ||
                    consumedOutside.BoundsHit ||
                    consumedCreationBlock ||
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawConsumedOutside))
                {
                    throw new InvalidOperationException("Expected left-consumed ownership to block world-left revival without becoming a creation hover/drag UI hit.");
                }

                BlueprintCreationMaskState.BeginCreate();
                var beforeHover = BlueprintCreationMaskState.GetSnapshot();
                var hoverInput = BlueprintCreationOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    false,
                    consumedCreationBlock,
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawConsumedOutside),
                    false,
                    false,
                    true,
                    7,
                    8,
                    true,
                    true,
                    (x, y) => true);
                var hoverResult = BlueprintCreationMaskState.HandlePointer(hoverInput);
                BlueprintUiClickDiagnostics.RecordCreationStateTransition(
                    "prefix",
                    rawConsumedOutside,
                    false,
                    false,
                    consumedOutside.PointerOwned,
                    consumedCreationBlock,
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawConsumedOutside),
                    true,
                    7,
                    8,
                    hoverInput,
                    beforeHover,
                    hoverResult);
                var afterHover = BlueprintCreationMaskState.GetSnapshot();
                if (hoverInput.UiOwned ||
                    hoverInput.LeftDown ||
                    hoverInput.LeftPressed ||
                    !afterHover.HoverTileHit ||
                    afterHover.HoverTileX != 7 ||
                    afterHover.HoverTileY != 8 ||
                    HasBlueprintCell(afterHover, 7, 8))
                {
                    throw new InvalidOperationException("Expected consumed-left creation input to keep hover alive without creating a mask cell.");
                }

                var consumedTrace = BlueprintUiClickDiagnostics.GetSnapshot().CreationLastClearReasonTrace;
                AssertContains(consumedTrace, "pointerUiOwned=true");
                AssertContains(consumedTrace, "pointerBlocksCreation=false");
                AssertContains(consumedTrace, "pointerBlocksWorldLeft=true");
                AssertContains(consumedTrace, "pointerBlocksHoverOrDrag=false");
                AssertContains(consumedTrace, "pointerOwnerBoundsHit=false");
                AssertContains(consumedTrace, "pointerLeftConsumed=true");

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-pointer-semantics-bounds-hit");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-bounds-hit",
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
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 32,
                    OsClientMouseY = 32,
                    OsLeftDown = true
                };
                var inside = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawInside);
                if (!inside.PointerOwned ||
                    inside.PointerBlocksWorldLeft ||
                    !inside.PointerBlocksHoverOrDrag ||
                    !BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(inside) ||
                    !UiPointerOwnershipService.ResolveWorldLeftDown(rawInside))
                {
                    throw new InvalidOperationException("Expected owner-bounds hit to block creation hover/drag without blocking world-left revival by itself.");
                }

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-pointer-semantics-coarse-only");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-coarse-only",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(500, 500, 80, 48),
                    false,
                    false,
                    false,
                    "hover");
                var rawCoarseOnly = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 32,
                    TerrariaMouseY = 32,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 32,
                    OsClientMouseY = 32,
                    OsLeftDown = true
                };
                var coarseOnly = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawCoarseOnly);
                if (!coarseOnly.PointerOwned ||
                    coarseOnly.PointerBlocksWorldLeft ||
                    coarseOnly.PointerBlocksHoverOrDrag ||
                    BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(coarseOnly) ||
                    !UiPointerOwnershipService.ResolveWorldLeftDown(rawCoarseOnly))
                {
                    throw new InvalidOperationException("Expected coarse pointer ownership alone not to block world-left, hover, or drag.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintUiClickDiagnostics.ResetForTesting();
            }
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
                    TerrariaMouseX = 600,
                    TerrariaMouseY = 700,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 120,
                    OsClientMouseY = 220,
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
                    !string.Equals(inside.MouseSource, "OsClient", StringComparison.Ordinal))
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
                AssertContains(trace, "pointerOwnerMouseSource=OsClient");
                AssertContains(trace, "pointerOwnerBoundsHit=true");
                AssertContains(trace, "pointerBlocksWorldLeft=false");
                AssertContains(trace, "pointerBlocksHoverOrDrag=true");
                AssertContains(trace, "pointerLeftOwned=false");
                AssertContains(trace, "pointerLeftConsumed=false");
                AssertContains(trace, "pointerScrollOwned=false");

                var rawOutside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 120,
                    TerrariaMouseY = 220,
                    OsReadAvailable = true,
                    OsClientMouseX = 12,
                    OsClientMouseY = 34,
                    OsLeftDown = true
                };
                var outside = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawOutside);
                if (!outside.PointerOwned ||
                    outside.BoundsHit ||
                    !string.Equals(outside.MouseSource, "OsClient", StringComparison.Ordinal) ||
                    UiPointerOwnershipService.IsPointerOwnerBoundsHitThisFrame(rawOutside))
                {
                    throw new InvalidOperationException("Expected ownership query to prefer OS client coordinates so Terraria raw from another domain cannot fabricate a bounds hit.");
                }

                var rawFallback = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 120,
                    TerrariaMouseY = 220,
                    OsReadAvailable = false,
                    OsLeftDown = true
                };
                var fallback = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawFallback);
                if (!fallback.BoundsHit ||
                    !string.Equals(fallback.MouseSource, "Terraria", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected ownership query to keep Terraria raw as a fallback when OS client coordinates are absent.");
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

        private static void BlueprintHandheldOwnerBoundsUseOsClientCoordinateDomain()
        {
            ResetUiInputFrameTestState();
            try
            {
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-handheld-owner-os-client-domain");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-handheld-action-bar:create",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(630, 640, 120, 48),
                    false,
                    false,
                    false,
                    "hover");

                var osInsideTerrariaOutside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 32,
                    TerrariaMouseY = 48,
                    OsReadAvailable = true,
                    OsClientMouseX = 640,
                    OsClientMouseY = 650,
                    ReadMode = "TestHandheldOwnerOsInside"
                };
                var osHit = UiPointerOwnershipService.ResolveWorldPointerOwnership(osInsideTerrariaOutside);
                if (!osHit.BoundsHit ||
                    !osHit.PointerBlocksHoverOrDrag ||
                    !string.Equals(osHit.MouseSource, "OsClient", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected handheld owner bounds hit to use OS client coordinates when Terraria raw is in a different domain.");
                }

                var osOutsideTerrariaInside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 640,
                    TerrariaMouseY = 650,
                    OsReadAvailable = true,
                    OsClientMouseX = 32,
                    OsClientMouseY = 48,
                    ReadMode = "TestHandheldOwnerTerrariaInside"
                };
                var terrariaMismatch = UiPointerOwnershipService.ResolveWorldPointerOwnership(osOutsideTerrariaInside);
                if (terrariaMismatch.BoundsHit ||
                    terrariaMismatch.PointerBlocksHoverOrDrag ||
                    !string.Equals(terrariaMismatch.MouseSource, "OsClient", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Terraria raw must not make handheld owner bounds hit when OS client coordinates are available outside the physical frame.");
                }

                var osMissingTerrariaInside = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 640,
                    TerrariaMouseY = 650,
                    OsReadAvailable = false,
                    ReadMode = "TestHandheldOwnerTerrariaFallback"
                };
                var fallback = UiPointerOwnershipService.ResolveWorldPointerOwnership(osMissingTerrariaInside);
                if (!fallback.BoundsHit ||
                    !fallback.PointerBlocksHoverOrDrag ||
                    !string.Equals(fallback.MouseSource, "Terraria", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected handheld owner bounds to keep Terraria raw fallback when OS client coordinates are unavailable.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
            }
        }

        private static void BlueprintCreationWorldOverlayPhaseTraceSlotsKeepPrefixAndAfter()
        {
            ResetUiInputFrameTestState();
            BlueprintUiClickDiagnostics.ResetForTesting();
            try
            {
                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "creation",
                    "prefix",
                    true,
                    new DiagnosticMouseState
                    {
                        ReadMode = "TestCreationPrefix",
                        GameInputAvailable = true,
                        TerrariaLeftDown = true,
                        OsLeftDown = true
                    },
                    false,
                    false,
                    false,
                    true,
                    false,
                    true,
                    12,
                    34,
                    false);

                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "creation",
                    "after-player-input",
                    true,
                    new DiagnosticMouseState
                    {
                        ReadMode = "TestCreationAfter",
                        GameInputAvailable = true,
                        TerrariaLeftDown = true,
                        OsLeftDown = true
                    },
                    false,
                    false,
                    true,
                    false,
                    true,
                    false,
                    0,
                    0,
                    true);

                var trace = BlueprintUiClickDiagnostics.GetSnapshot();
                AssertContains(trace.WorldOverlayInputTrace, "phase=after-player-input");
                AssertContains(trace.WorldOverlayInputTrace, "readMode=TestCreationAfter");
                AssertContains(trace.CreationPrefixWorldOverlayInputTrace, "overlay=creation");
                AssertContains(trace.CreationPrefixWorldOverlayInputTrace, "phase=prefix");
                AssertContains(trace.CreationPrefixWorldOverlayInputTrace, "readMode=TestCreationPrefix");
                AssertContains(trace.CreationPrefixWorldOverlayInputTrace, "worldTileHit=true");
                AssertContains(trace.CreationPrefixWorldOverlayInputTrace, "tile=12,34");
                AssertContains(trace.CreationAfterPlayerInputWorldOverlayInputTrace, "overlay=creation");
                AssertContains(trace.CreationAfterPlayerInputWorldOverlayInputTrace, "phase=after-player-input");
                AssertContains(trace.CreationAfterPlayerInputWorldOverlayInputTrace, "readMode=TestCreationAfter");
                AssertContains(trace.CreationAfterPlayerInputWorldOverlayInputTrace, "consumeAfter=true");
                if (trace.CreationPrefixWorldOverlayInputTrace.Contains("TestCreationAfter"))
                {
                    throw new InvalidOperationException("Creation prefix world overlay trace must not be overwritten by after-player-input.");
                }

                var snapshot = new DiagnosticSnapshot
                {
                    BlueprintWorldOverlayLastInputTrace = trace.WorldOverlayInputTrace,
                    BlueprintCreationPrefixWorldOverlayInputTrace = trace.CreationPrefixWorldOverlayInputTrace,
                    BlueprintCreationAfterPlayerInputWorldOverlayInputTrace = trace.CreationAfterPlayerInputWorldOverlayInputTrace
                };
                var json = InvokeDiagnosticSnapshotJson(snapshot);
                AssertContains(json, "\"BlueprintWorldOverlayLastInputTrace\": \"overlay=creation;phase=after-player-input;");
                AssertContains(json, "\"BlueprintCreationPrefixWorldOverlayInputTrace\": \"overlay=creation;phase=prefix;");
                AssertContains(json, "\"BlueprintCreationAfterPlayerInputWorldOverlayInputTrace\": \"overlay=creation;phase=after-player-input;");

                BlueprintUiClickDiagnostics.ResetForTesting();
                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "creation",
                    "after-player-input",
                    true,
                    new DiagnosticMouseState
                    {
                        ReadMode = "OnlyAfter",
                        GameInputAvailable = true,
                        TerrariaLeftDown = true,
                        OsLeftDown = false
                    },
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    0,
                    0,
                    false);
                var onlyAfter = BlueprintUiClickDiagnostics.GetSnapshot();
                AssertStringEquals(onlyAfter.CreationPrefixWorldOverlayInputTrace, string.Empty, "creation prefix slot empty when only after-player-input records");
                AssertContains(onlyAfter.CreationAfterPlayerInputWorldOverlayInputTrace, "readMode=OnlyAfter");
                AssertContains(onlyAfter.WorldOverlayInputTrace, "readMode=OnlyAfter");
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintUiClickDiagnostics.ResetForTesting();
            }
        }

        private static void BlueprintCreationClearReasonTraceRecordsStateAndCoordinates()
        {
            ResetUiInputFrameTestState();
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintUiClickDiagnostics.ResetForTesting();
            try
            {
                BlueprintCreationMaskState.BeginCreate();
                ClickTileForBlueprintCreation(5, 5);
                BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
                {
                    WorldTileHit = true,
                    TileX = 6,
                    TileY = 6,
                    ContentKnown = true,
                    HasSelectableContent = true,
                    IsSelectableTile = (x, y) => true
                });

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-creation-clear-ui-owned");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-clear-owner",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(100, 200, 80, 48),
                    false,
                    false,
                    false,
                    "hover");
                var rawUi = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 600,
                    TerrariaMouseY = 700,
                    TerrariaLeftDown = true,
                    OsReadAvailable = true,
                    OsClientMouseX = 120,
                    OsClientMouseY = 220,
                    OsLeftDown = true,
                    ReadMode = "TestCreationClearUi"
                };
                var ownership = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawUi);
                var pointerBlocksCreation = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(ownership);
                var beforeUi = BlueprintCreationMaskState.GetSnapshot();
                var uiInput = BlueprintCreationOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    false,
                    pointerBlocksCreation,
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawUi),
                    true,
                    false,
                    true,
                    9,
                    9,
                    true,
                    true,
                    (x, y) => true);
                var uiResult = BlueprintCreationMaskState.HandlePointer(uiInput);
                BlueprintUiClickDiagnostics.RecordCreationStateTransition(
                    "prefix",
                    rawUi,
                    false,
                    false,
                    ownership.PointerOwned,
                    pointerBlocksCreation,
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawUi),
                    true,
                    9,
                    9,
                    uiInput,
                    beforeUi,
                    uiResult);
                var uiTrace = BlueprintUiClickDiagnostics.GetSnapshot().CreationLastClearReasonTrace;
                AssertContains(uiTrace, "reason=uiOwned");
                AssertContains(uiTrace, "beforeHover=6,6");
                AssertContains(uiTrace, "afterHover=none");
                AssertContains(uiTrace, "beforeSelected=1");
                AssertContains(uiTrace, "afterSelected=1");
                AssertContains(uiTrace, "legacyUiOwned=false");
                AssertContains(uiTrace, "pointerUiOwned=true");
                AssertContains(uiTrace, "pointerBlocksCreation=true");
                AssertContains(uiTrace, "pointerBlocksWorldLeft=false");
                AssertContains(uiTrace, "pointerBlocksHoverOrDrag=true");
                AssertContains(uiTrace, "uiOwned=true");
                AssertContains(uiTrace, "pointerOwnerBoundsHit=true");
                AssertContains(uiTrace, "pointerLeftConsumed=false");
                AssertContains(uiTrace, "terrariaMouse=600,700");
                AssertContains(uiTrace, "osMouse=120,220");
                AssertContains(uiTrace, "pointerOwnerMouseSource=OsClient");
                AssertContains(uiTrace, "worldMouseSource=Terraria");

                BlueprintCreationMaskState.ResetForTesting();
                BlueprintCreationMaskState.BeginCreate();
                var rawMiss = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 12,
                    OsClientMouseY = 34,
                    OsLeftDown = true,
                    ReadMode = "TestCreationWorldMiss"
                };
                var beforeMiss = BlueprintCreationMaskState.GetSnapshot();
                var missInput = BlueprintCreationOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    false,
                    false,
                    true,
                    true,
                    false,
                    false,
                    0,
                    0);
                var missResult = BlueprintCreationMaskState.HandlePointer(missInput);
                BlueprintUiClickDiagnostics.RecordCreationStateTransition(
                    "prefix",
                    rawMiss,
                    false,
                    false,
                    false,
                    false,
                    true,
                    false,
                    0,
                    0,
                    missInput,
                    beforeMiss,
                    missResult);
                var missTrace = BlueprintUiClickDiagnostics.GetSnapshot().CreationLastClearReasonTrace;
                AssertContains(missTrace, "reason=worldMiss");
                AssertContains(missTrace, "worldTileHit=false");
                AssertContains(missTrace, "worldMouseSource=OsClient");
                AssertContains(missTrace, "osMouse=12,34");

                BlueprintCreationMaskState.ResetForTesting();
                BlueprintCreationMaskState.BeginCreate();
                BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
                {
                    WorldTileHit = true,
                    TileX = 2,
                    TileY = 2,
                    LeftDown = true,
                    LeftPressed = true
                });
                var beforeRelease = BlueprintCreationMaskState.GetSnapshot();
                var rawRelease = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    OsReadAvailable = true,
                    OsClientMouseX = 48,
                    OsClientMouseY = 64,
                    OsLeftDown = false,
                    ReadMode = "TestCreationRelease"
                };
                var releaseInput = BlueprintCreationOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    false,
                    false,
                    false,
                    false,
                    true,
                    true,
                    3,
                    3,
                    true,
                    true,
                    (x, y) => true);
                var releaseResult = BlueprintCreationMaskState.HandlePointer(releaseInput);
                BlueprintUiClickDiagnostics.RecordCreationStateTransition(
                    "prefix",
                    rawRelease,
                    false,
                    false,
                    false,
                    false,
                    false,
                    true,
                    3,
                    3,
                    releaseInput,
                    beforeRelease,
                    releaseResult);
                var releaseTrace = BlueprintUiClickDiagnostics.GetSnapshot().CreationLastClearReasonTrace;
                AssertContains(releaseTrace, "reason=selectionToggled");
                AssertContains(releaseTrace, "leftReleased=true");
                AssertContains(releaseTrace, "resolvedLeft=false");
                AssertContains(releaseTrace, "beforeDragging=true");
                AssertContains(releaseTrace, "afterDragging=false");

                var snapshot = new DiagnosticSnapshot
                {
                    BlueprintCreationLastClearReasonTrace = releaseTrace
                };
                var json = InvokeDiagnosticSnapshotJson(snapshot);
                AssertContains(json, "\"BlueprintCreationLastClearReasonTrace\": \"phase=prefix;reason=selectionToggled;");
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintUiClickDiagnostics.ResetForTesting();
            }
        }

        private static void BlueprintCreationPhysicalLeftEdgesIgnoreConsumedWorldLeft()
        {
            ResetUiInputFrameTestState();
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintUiClickDiagnostics.ResetForTesting();
            try
            {
                BlueprintCreationMaskState.BeginCreate();
                var startInput = BlueprintCreationOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    false,
                    true,
                    true,
                    false,
                    false,
                    true,
                    4,
                    4,
                    true,
                    true,
                    (x, y) => true);
                if (!startInput.WorldLeftDown ||
                    !startInput.PhysicalLeftDown ||
                    !startInput.LeftPressed ||
                    startInput.LeftReleased)
                {
                    throw new InvalidOperationException("Expected an initial physical/world left edge to start blueprint creation drag.");
                }

                BlueprintCreationMaskState.HandlePointer(startInput);
                var dragging = BlueprintCreationMaskState.GetSnapshot();
                if (!dragging.Dragging)
                {
                    throw new InvalidOperationException("Expected blueprint creation drag to start before the consumed-left frame.");
                }

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-creation-physical-edge-consumed-held");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-left-consumed",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(500, 500, 80, 48),
                    true,
                    true,
                    false,
                    "left");
                var rawConsumedHeld = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 80,
                    TerrariaMouseY = 80,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 80,
                    OsClientMouseY = 80,
                    OsLeftDown = true,
                    ReadMode = "TestCreationPhysicalConsumedHeld"
                };
                var consumedOwnership = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawConsumedHeld);
                var consumedPointerBlock = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(consumedOwnership);
                var consumedWorldLeft = UiPointerOwnershipService.ResolveWorldLeftDown(rawConsumedHeld);
                var consumedPhysicalLeft = BlueprintCreationOverlay.ResolvePhysicalLeftDownForTesting(rawConsumedHeld);
                if (!consumedOwnership.PointerBlocksWorldLeft ||
                    consumedOwnership.PointerBlocksHoverOrDrag ||
                    consumedPointerBlock ||
                    consumedWorldLeft ||
                    !consumedPhysicalLeft)
                {
                    throw new InvalidOperationException("Expected consumed-left outside owner bounds to block only world-left while physical left remains held.");
                }

                var beforeConsumed = BlueprintCreationMaskState.GetSnapshot();
                var consumedInput = BlueprintCreationOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    consumedPointerBlock,
                    consumedWorldLeft,
                    consumedPhysicalLeft,
                    true,
                    false,
                    true,
                    5,
                    5,
                    true,
                    true,
                    (x, y) => true);
                if (consumedInput.LeftDown ||
                    consumedInput.LeftPressed ||
                    consumedInput.LeftReleased ||
                    !consumedInput.PhysicalLeftDown ||
                    consumedInput.WorldLeftDown ||
                    consumedInput.UiOwned)
                {
                    throw new InvalidOperationException("Consumed world-left must not create a press or fake release while the physical button is still held.");
                }

                var consumedResult = BlueprintCreationMaskState.HandlePointer(consumedInput);
                BlueprintUiClickDiagnostics.RecordCreationStateTransition(
                    "prefix",
                    rawConsumedHeld,
                    false,
                    false,
                    consumedOwnership.PointerOwned,
                    consumedPointerBlock,
                    consumedWorldLeft,
                    true,
                    5,
                    5,
                    consumedInput,
                    beforeConsumed,
                    consumedResult);
                var afterConsumed = BlueprintCreationMaskState.GetSnapshot();
                if (!afterConsumed.Dragging ||
                    !afterConsumed.HoverTileHit ||
                    afterConsumed.HoverTileX != 5 ||
                    afterConsumed.HoverTileY != 5 ||
                    afterConsumed.SelectedCount != 0)
                {
                    throw new InvalidOperationException("Consumed-held creation frame should keep drag and hover alive without changing mask cells.");
                }

                var consumedTrace = BlueprintUiClickDiagnostics.GetSnapshot().CreationLastClearReasonTrace;
                AssertContains(consumedTrace, "resolvedLeft=false");
                AssertContains(consumedTrace, "physicalLeft=true");
                AssertContains(consumedTrace, "leftReleased=false");
                AssertContains(consumedTrace, "pointerBlocksWorldLeft=true");
                AssertContains(consumedTrace, "pointerBlocksHoverOrDrag=false");

                BlueprintCreationMaskState.ResetForTesting();
                BlueprintCreationMaskState.BeginCreate();
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-creation-physical-edge-consumed-press");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-left-consumed-press",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(500, 500, 80, 48),
                    true,
                    true,
                    false,
                    "left");
                var consumedPressOwnership = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawConsumedHeld);
                var consumedPressInput = BlueprintCreationOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(consumedPressOwnership),
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawConsumedHeld),
                    BlueprintCreationOverlay.ResolvePhysicalLeftDownForTesting(rawConsumedHeld),
                    false,
                    false,
                    true,
                    6,
                    6,
                    true,
                    true,
                    (x, y) => true);
                if (consumedPressInput.LeftPressed)
                {
                    throw new InvalidOperationException("Consumed-left must not become a fresh creation press.");
                }

                BlueprintCreationMaskState.HandlePointer(consumedPressInput);
                if (HasBlueprintCell(BlueprintCreationMaskState.GetSnapshot(), 6, 6))
                {
                    throw new InvalidOperationException("Consumed-left fresh physical press must not create a mask cell.");
                }

                BlueprintCreationMaskState.ResetForTesting();
                BlueprintCreationMaskState.BeginCreate();
                BlueprintCreationMaskState.HandlePointer(startInput);
                var beforeRelease = BlueprintCreationMaskState.GetSnapshot();
                var rawRelease = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 96,
                    TerrariaMouseY = 96,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 96,
                    OsClientMouseY = 96,
                    OsLeftDown = false,
                    ReadMode = "TestCreationPhysicalRelease"
                };
                var releaseInput = BlueprintCreationOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    false,
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawRelease),
                    BlueprintCreationOverlay.ResolvePhysicalLeftDownForTesting(rawRelease),
                    true,
                    false,
                    true,
                    5,
                    5,
                    true,
                    true,
                    (x, y) => true);
                if (!releaseInput.LeftReleased ||
                    releaseInput.PhysicalLeftDown)
                {
                    throw new InvalidOperationException("Expected only a real physical release to finish blueprint creation drag.");
                }

                var releaseResult = BlueprintCreationMaskState.HandlePointer(releaseInput);
                BlueprintUiClickDiagnostics.RecordCreationStateTransition(
                    "prefix",
                    rawRelease,
                    false,
                    false,
                    false,
                    false,
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawRelease),
                    true,
                    5,
                    5,
                    releaseInput,
                    beforeRelease,
                    releaseResult);
                var afterRelease = BlueprintCreationMaskState.GetSnapshot();
                if (afterRelease.Dragging ||
                    !HasBlueprintCell(afterRelease, 4, 4) ||
                    !HasBlueprintCell(afterRelease, 5, 5))
                {
                    throw new InvalidOperationException("Expected real physical release to complete the drag rectangle.");
                }

                var releaseTrace = BlueprintUiClickDiagnostics.GetSnapshot().CreationLastClearReasonTrace;
                AssertContains(releaseTrace, "reason=selectionToggled");
                AssertContains(releaseTrace, "resolvedLeft=false");
                AssertContains(releaseTrace, "physicalLeft=false");
                AssertContains(releaseTrace, "leftReleased=true");
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintUiClickDiagnostics.ResetForTesting();
            }
        }

        private static void BlueprintCreationActionMetadataCarriesClearTrace()
        {
            ResetUiInputFrameTestState();
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintUiClickDiagnostics.ResetForTesting();
            try
            {
                BlueprintCreationMaskState.BeginCreate();
                var raw = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 44,
                    OsClientMouseY = 55,
                    OsLeftDown = true,
                    ReadMode = "TestCreationActionMetadata"
                };
                var before = BlueprintCreationMaskState.GetSnapshot();
                var input = BlueprintCreationOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    false,
                    false,
                    true,
                    true,
                    false,
                    false,
                    0,
                    0);
                var result = BlueprintCreationMaskState.HandlePointer(input);
                BlueprintUiClickDiagnostics.RecordCreationStateTransition(
                    "prefix",
                    raw,
                    false,
                    false,
                    false,
                    false,
                    true,
                    false,
                    0,
                    0,
                    input,
                    before,
                    result);

                var clearTrace = BlueprintUiClickDiagnostics.GetSnapshot().CreationLastClearReasonTrace;
                AssertContains(clearTrace, "phase=prefix");
                AssertContains(clearTrace, "reason=worldMiss");
                AssertContains(clearTrace, "worldMouseSource=OsClient");

                var creationMetadata = LegacyUiActionService.BuildBlueprintCreationActionMetadataForTesting();
                AssertContains(creationMetadata, "\"creationClearTrace\":\"phase=prefix;reason=worldMiss;");
                AssertContains(creationMetadata, "worldMouseSource=OsClient");

                var handheldMetadata = LegacyUiActionService.BuildBlueprintHandheldActionMetadataForTesting(
                    BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdCreate, "创建蓝图"),
                    BlueprintHandheldActionBarState.ButtonIdCreate,
                    true);
                AssertContains(handheldMetadata, "\"ownerId\":\"" + BlueprintHandheldActionBarState.BuildCommandElementId(BlueprintHandheldActionBarState.ButtonIdCreate) + "\"");
                AssertContains(handheldMetadata, "\"creationClearTrace\":\"phase=prefix;reason=worldMiss;");
                AssertContains(handheldMetadata, "worldMouseSource=OsClient");

                var hotkeyResult = BlueprintEntryHotkeyDispatchResult.FromApply(
                    FeatureIds.BlueprintCreateAction,
                    BlueprintEntryCommands.StartCreate,
                    "G",
                    BlueprintEntryCommandResult.Create(
                        true,
                        true,
                        false,
                        "entryStateChanged",
                        "Blueprint creation toggled for diagnostic metadata test.",
                        BlueprintEntryModes.Creating),
                    null);
                var hotkeyMetadata = BlueprintEntryHotkeyService.BuildBlueprintActionHotkeyMetadataForTesting(hotkeyResult);
                AssertContains(hotkeyMetadata, "\"targetId\":\"" + FeatureIds.BlueprintCreateAction + "\"");
                AssertContains(hotkeyMetadata, "\"action\":\"" + BlueprintEntryCommands.StartCreate + "\"");
                AssertContains(hotkeyMetadata, "\"creationClearTrace\":\"phase=prefix;reason=worldMiss;");
                AssertContains(hotkeyMetadata, "worldMouseSource=OsClient");
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintCreationMaskState.ResetForTesting();
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
                    !consumed.PointerBlocksWorldLeft ||
                    consumed.PointerBlocksHoverOrDrag ||
                    BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(consumed) ||
                    !BlueprintPlacementPreviewOverlay.ShouldBlockPlacementForPointerOwnershipForTesting(consumed) ||
                    !BlueprintEraseRegionOverlay.ShouldBlockEraseForPointerOwnershipForTesting(consumed) ||
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawConsumed))
                {
                    throw new InvalidOperationException("Expected left-consumed ownership to block world-left revival while only placement and erase keep the strict click-blocking gate.");
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
