using System;
using System.Linq;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Hooks;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintCreationMaskTogglesSingleTileAndMultiRegion()
        {
            BlueprintEntryState.ResetForTesting();
            var started = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, AppSettings.CreateDefault());
            if (!started.Succeeded || started.PlaceholderOnly)
            {
                throw new InvalidOperationException("Expected 06 start-create to be implemented.");
            }

            ClickTileForBlueprintCreation(10, 20);
            ClickTileForBlueprintCreation(12, 22);
            var snapshot = BlueprintCreationMaskState.GetSnapshot();
            if (snapshot.SelectedCount != 2 || !HasBlueprintCell(snapshot, 10, 20) || !HasBlueprintCell(snapshot, 12, 22))
            {
                throw new InvalidOperationException("Expected single-click selection to support disjoint mask cells.");
            }

            ClickTileForBlueprintCreation(10, 20);
            snapshot = BlueprintCreationMaskState.GetSnapshot();
            if (snapshot.SelectedCount != 1 || HasBlueprintCell(snapshot, 10, 20) || !HasBlueprintCell(snapshot, 12, 22))
            {
                throw new InvalidOperationException("Expected clicking an already selected tile to deselect it.");
            }
        }

        private static void BlueprintCreationMaskDragTogglesRectangle()
        {
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintCreationMaskState.BeginCreate();
            BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 4,
                TileY = 6,
                LeftDown = true,
                LeftPressed = true
            });
            BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 6,
                TileY = 7,
                LeftDown = true
            });
            BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 6,
                TileY = 7,
                LeftReleased = true
            });

            var snapshot = BlueprintCreationMaskState.GetSnapshot();
            if (snapshot.SelectedCount != 6 ||
                !snapshot.HasBounds ||
                snapshot.MinX != 4 ||
                snapshot.MinY != 6 ||
                snapshot.MaxX != 6 ||
                snapshot.MaxY != 7)
            {
                throw new InvalidOperationException("Expected drag selection to toggle the full inclusive rectangle.");
            }
        }

        private static void BlueprintCreationMaskSelectsAirAndTracksBounds()
        {
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintCreationMaskState.BeginCreate();

            Func<int, int, bool> readable = (x, y) => true;
            var hover = BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 5,
                TileY = 6,
                ContentKnown = true,
                HasSelectableContent = false,
                IsSelectableTile = readable
            });
            if (hover == null ||
                hover.Snapshot == null ||
                !hover.Snapshot.HoverTileHit ||
                hover.Snapshot.HoverTileX != 5 ||
                hover.Snapshot.HoverTileY != 6)
            {
                throw new InvalidOperationException("Expected creation mask hover to track readable air cells.");
            }

            var airClick = BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 5,
                TileY = 6,
                ContentKnown = true,
                HasSelectableContent = false,
                IsSelectableTile = readable,
                LeftDown = true,
                LeftPressed = true
            });
            BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 5,
                TileY = 6,
                ContentKnown = true,
                HasSelectableContent = false,
                IsSelectableTile = readable,
                LeftReleased = true
            });
            var snapshot = BlueprintCreationMaskState.GetSnapshot();
            if (airClick == null ||
                !airClick.ShouldConsumeLeftInput ||
                string.Equals(airClick.ResultCode, "tileUnavailable", StringComparison.Ordinal) ||
                snapshot.SelectedCount != 1 ||
                !snapshot.HasBounds ||
                snapshot.MinX != 5 ||
                snapshot.MaxX != 5 ||
                !HasBlueprintCell(snapshot, 5, 6))
            {
                throw new InvalidOperationException("Expected air cell selection to count as a real blueprint mask cell.");
            }

            BlueprintCreationMaskState.ClearSelection();
            BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 4,
                TileY = 6,
                IsSelectableTile = readable,
                LeftDown = true,
                LeftPressed = true
            });
            BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 6,
                TileY = 7,
                IsSelectableTile = readable,
                LeftDown = true
            });
            BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 6,
                TileY = 7,
                IsSelectableTile = readable,
                LeftReleased = true
            });
            snapshot = BlueprintCreationMaskState.GetSnapshot();
            if (snapshot.SelectedCount != 6 ||
                !snapshot.HasBounds ||
                snapshot.MinX != 4 ||
                snapshot.MinY != 6 ||
                snapshot.MaxX != 6 ||
                snapshot.MaxY != 7 ||
                !HasBlueprintCell(snapshot, 4, 6) ||
                !HasBlueprintCell(snapshot, 6, 7) ||
                !HasBlueprintCell(snapshot, 5, 6))
            {
                throw new InvalidOperationException("Expected drag selection to include air cells in the mask and bounds.");
            }

            var unavailable = BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = 99,
                TileY = 99,
                IsSelectableTile = (x, y) => false,
                LeftDown = true,
                LeftPressed = true
            });
            if (unavailable == null ||
                !unavailable.ShouldConsumeLeftInput ||
                !string.Equals(unavailable.ResultCode, "tileUnavailable", StringComparison.Ordinal) ||
                BlueprintCreationMaskState.GetSnapshot().SelectedCount != 6)
            {
                throw new InvalidOperationException("Expected unreadable world cells to stay fail-closed without clearing the existing mask.");
            }
        }

        private static void BlueprintCreationUiHitConsumesWithoutChangingMask()
        {
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintCreationMaskState.BeginCreate();
            ClickTileForBlueprintCreation(30, 40);
            var before = BlueprintCreationMaskState.GetSnapshot();
            var result = BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                UiOwned = true,
                LeftDown = true,
                LeftPressed = true,
                WorldTileHit = true,
                TileX = 99,
                TileY = 99
            });
            var after = BlueprintCreationMaskState.GetSnapshot();
            if (!result.ShouldConsumeLeftInput ||
                after.SelectedCount != before.SelectedCount ||
                !HasBlueprintCell(after, 30, 40) ||
                HasBlueprintCell(after, 99, 99))
            {
                throw new InvalidOperationException("Expected UI-owned clicks to be consumed without modifying the creation mask.");
            }
        }

        private static void BlueprintCreationPointerOwnershipNarrowingKeepsWorldHoverAndMask()
        {
            ResetUiInputFrameTestState();
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintUiClickDiagnostics.ResetForTesting();
            try
            {
                BlueprintCreationMaskState.BeginCreate();
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-creation-pointer-narrow-outside");
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
                var outsideOwnership = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawOutside);
                var outsidePointerBlocksCreation = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(outsideOwnership);
                if (!outsideOwnership.PointerOwned ||
                    outsideOwnership.LeftConsumed ||
                    outsideOwnership.BoundsHit ||
                    outsidePointerBlocksCreation)
                {
                    throw new InvalidOperationException("Expected out-of-bounds hover-only ownership not to block blueprint creation.");
                }

                BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                    "creation",
                    "prefix",
                    true,
                    rawOutside,
                    false,
                    false,
                    outsideOwnership.PointerOwned,
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawOutside),
                    false,
                    true,
                    2,
                    2,
                    false);
                var trace = BlueprintUiClickDiagnostics.GetSnapshot().WorldOverlayInputTrace;
                AssertContains(trace, "pointerUiOwned=true");
                AssertContains(trace, "uiOwned=false");
                AssertContains(trace, "pointerOwnerBoundsHit=false");
                AssertContains(trace, "pointerLeftConsumed=false");

                var hover = BlueprintCreationMaskState.HandlePointer(
                    BlueprintCreationOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        outsidePointerBlocksCreation,
                        false,
                        false,
                        false,
                        true,
                        2,
                        2,
                        true,
                        true,
                        (x, y) => true));
                if (hover == null ||
                    hover.Snapshot == null ||
                    !hover.Snapshot.HoverTileHit ||
                    hover.Snapshot.HoverTileX != 2 ||
                    hover.Snapshot.HoverTileY != 2)
                {
                    throw new InvalidOperationException("Expected out-of-bounds hover-only ownership to keep creation hover alive.");
                }

                var outsideLeftDown = UiPointerOwnershipService.ResolveWorldLeftDown(rawOutside);
                BlueprintCreationMaskState.HandlePointer(
                    BlueprintCreationOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        outsidePointerBlocksCreation,
                        outsideLeftDown,
                        outsideLeftDown,
                        false,
                        true,
                        2,
                        2,
                        true,
                        true,
                        (x, y) => true));
                BlueprintCreationMaskState.HandlePointer(
                    BlueprintCreationOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        outsidePointerBlocksCreation,
                        false,
                        false,
                        true,
                        true,
                        2,
                        2,
                        true,
                        true,
                        (x, y) => true));
                var afterWorldClick = BlueprintCreationMaskState.GetSnapshot();
                if (!HasBlueprintCell(afterWorldClick, 2, 2))
                {
                    throw new InvalidOperationException("Expected out-of-bounds hover-only ownership to allow creation mask clicks.");
                }

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-creation-pointer-narrow-inside");
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
                    OsLeftDown = true
                };
                var insidePointerBlocksCreation = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(
                    UiPointerOwnershipService.ResolveWorldPointerOwnership(rawInside));
                if (!insidePointerBlocksCreation)
                {
                    throw new InvalidOperationException("Expected current owner-bounds hits to keep creation UI-owned.");
                }

                var insideResult = BlueprintCreationMaskState.HandlePointer(
                    BlueprintCreationOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        insidePointerBlocksCreation,
                        UiPointerOwnershipService.ResolveWorldLeftDown(rawInside),
                        true,
                        false,
                        true,
                        3,
                        3,
                        true,
                        true,
                        (x, y) => true));
                if (insideResult == null ||
                    !insideResult.ShouldConsumeLeftInput ||
                    HasBlueprintCell(BlueprintCreationMaskState.GetSnapshot(), 3, 3))
                {
                    throw new InvalidOperationException("Expected owner-bounds hits to block creation mask changes.");
                }

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-creation-pointer-narrow-consumed");
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
                var consumedOwnership = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawConsumed);
                if (!consumedOwnership.PointerBlocksWorldLeft ||
                    consumedOwnership.PointerBlocksHoverOrDrag ||
                    BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(consumedOwnership) ||
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawConsumed))
                {
                    throw new InvalidOperationException("Expected left-consumed ownership to block OS-left revival without becoming creation UI-owned.");
                }

                var legacyResult = BlueprintCreationMaskState.HandlePointer(
                    BlueprintCreationOverlay.BuildPointerInputForTesting(
                        true,
                        true,
                        false,
                        false,
                        true,
                        true,
                        false,
                        true,
                        4,
                        4,
                        true,
                        true,
                        (x, y) => true));
                var vanillaResult = BlueprintCreationMaskState.HandlePointer(
                    BlueprintCreationOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        true,
                        false,
                        true,
                        true,
                        false,
                        true,
                        5,
                        5,
                        true,
                        true,
                        (x, y) => true));
                var finalSnapshot = BlueprintCreationMaskState.GetSnapshot();
                if (legacyResult == null ||
                    vanillaResult == null ||
                    !legacyResult.ShouldConsumeLeftInput ||
                    !vanillaResult.ShouldConsumeLeftInput ||
                    HasBlueprintCell(finalSnapshot, 4, 4) ||
                    HasBlueprintCell(finalSnapshot, 5, 5))
                {
                    throw new InvalidOperationException("Expected legacy and vanilla UI ownership to stay fail-closed for creation.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintUiClickDiagnostics.ResetForTesting();
            }
        }

        private static void BlueprintUiPointerOwnershipBlocksWorldOverlayClicks()
        {
            ResetUiInputFrameTestState();
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintPlacementPreviewState.ResetForTesting();
            BlueprintEraseRegionState.ResetForTesting();
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            try
            {
                BlueprintCreationMaskState.BeginCreate();
                ClickTileForBlueprintCreation(30, 40);
                var before = BlueprintCreationMaskState.GetSnapshot();

                RegisterHandheldOwnershipForWorldOverlayTest(
                    BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId),
                    BlueprintHandheldActionBarState.ButtonIdCreate,
                    true);
                var osRevivedLeft = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsLeftDown = true
                };
                var consumedLeftDown = UiPointerOwnershipService.ResolveWorldLeftDown(osRevivedLeft);
                var consumedOwnership = UiPointerOwnershipService.ResolveWorldPointerOwnership(osRevivedLeft);
                var consumedPointerUiOwned = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(consumedOwnership);
                var creationUiInput = BlueprintCreationOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    false,
                    consumedPointerUiOwned,
                    consumedLeftDown,
                    consumedLeftDown,
                    false,
                    true,
                    99,
                    99,
                    true,
                    true,
                    (x, y) => true);
                if (creationUiInput.LeftDown || creationUiInput.LeftPressed)
                {
                    throw new InvalidOperationException("Expected UI-consumed OS left to be gated before creation pointer input is built.");
                }

                var creationUiResult = BlueprintCreationMaskState.HandlePointer(creationUiInput);
                var after = BlueprintCreationMaskState.GetSnapshot();
                if (!consumedOwnership.PointerBlocksWorldLeft ||
                    consumedOwnership.PointerBlocksHoverOrDrag ||
                    creationUiInput.UiOwned ||
                    creationUiResult.ShouldConsumeLeftInput ||
                    after.SelectedCount != before.SelectedCount ||
                    !HasBlueprintCell(after, 30, 40) ||
                    HasBlueprintCell(after, 99, 99))
                {
                    throw new InvalidOperationException("Expected handheld button ownership to block creation mask changes from revived OS left without becoming hover UI-owned.");
                }

                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                var disabledFrame = BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true));
                var disabledSave = disabledFrame.Buttons[0];
                var disabledPress = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                    disabledFrame,
                    BlueprintHandheldPointer(disabledSave.Rect.CenterX, disabledSave.Rect.CenterY, true, 0, true));
                if (!disabledPress.ShouldConsumeLeftInput || disabledPress.Clicked)
                {
                    throw new InvalidOperationException("Expected disabled handheld save to consume ownership without submitting a command.");
                }

                RegisterHandheldOwnershipForWorldOverlayTest(disabledFrame, disabledPress, true);
                var disabledPointerUiOwned = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(
                    UiPointerOwnershipService.ResolveWorldPointerOwnership(osRevivedLeft));
                var disabledUiInput = BlueprintCreationOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    false,
                    disabledPointerUiOwned,
                    UiPointerOwnershipService.ResolveWorldLeftDown(osRevivedLeft),
                    false,
                    false,
                    true,
                    98,
                    98,
                    true,
                    true,
                    (x, y) => true);
                BlueprintCreationMaskState.HandlePointer(disabledUiInput);
                if (HasBlueprintCell(BlueprintCreationMaskState.GetSnapshot(), 98, 98))
                {
                    throw new InvalidOperationException("Expected disabled save ownership to block creation mask changes.");
                }

                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                var blankFrame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
                var blankPress = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                    blankFrame,
                    BlueprintHandheldPointer(blankFrame.Bounds.X + 1, blankFrame.Bounds.CenterY, true, 0, true));
                if (!blankPress.ShouldConsumeLeftInput || blankPress.Clicked)
                {
                    throw new InvalidOperationException("Expected handheld blank-bar click to consume ownership without submitting a command.");
                }

                RegisterHandheldOwnershipForWorldOverlayTest(blankFrame, blankPress, true);
                var blankPointerUiOwned = BlueprintCreationOverlay.ShouldBlockCreationForPointerOwnershipForTesting(
                    UiPointerOwnershipService.ResolveWorldPointerOwnership(osRevivedLeft));
                var blankUiInput = BlueprintCreationOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    false,
                    blankPointerUiOwned,
                    UiPointerOwnershipService.ResolveWorldLeftDown(osRevivedLeft),
                    false,
                    false,
                    true,
                    97,
                    97,
                    true,
                    true,
                    (x, y) => true);
                BlueprintCreationMaskState.HandlePointer(blankUiInput);
                if (HasBlueprintCell(BlueprintCreationMaskState.GetSnapshot(), 97, 97))
                {
                    throw new InvalidOperationException("Expected handheld blank-bar ownership to block creation mask changes.");
                }

                UiPointerOwnershipService.ResetForTesting();
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-ui-ownership.outside-world");
                var outsideLeftDown = UiPointerOwnershipService.ResolveWorldLeftDown(osRevivedLeft);
                var outsideWorldInput = BlueprintCreationOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    false,
                    false,
                    outsideLeftDown,
                    outsideLeftDown,
                    false,
                    true,
                    96,
                    96,
                    true,
                    true,
                    (x, y) => true);
                BlueprintCreationMaskState.HandlePointer(outsideWorldInput);
                BlueprintCreationMaskState.HandlePointer(
                    BlueprintCreationOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        false,
                        false,
                        false,
                        true,
                        true,
                        96,
                        96,
                        true,
                        true,
                        (x, y) => true));
                if (!HasBlueprintCell(BlueprintCreationMaskState.GetSnapshot(), 96, 96))
                {
                    throw new InvalidOperationException("Expected world clicks outside UI ownership to keep modifying the creation mask.");
                }

                var preview = BlueprintPlacementPreviewState.BeginPreview(CreateEvenBlueprintTemplate("UI ownership preview"), "test");
                if (!preview.Succeeded)
                {
                    throw new InvalidOperationException("Expected placement preview to begin for UI ownership test.");
                }

                var placementUi = BlueprintPlacementPreviewState.HandlePointer(
                    BlueprintPlacementPreviewOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        true,
                        true,
                        true,
                        false,
                        true,
                        44,
                        55));
                if (!placementUi.ShouldConsumeLeftInput ||
                    placementUi.PlacedInstance ||
                    !BlueprintPlacementPreviewState.GetSnapshot().Active)
                {
                    throw new InvalidOperationException("Expected UI-owned placement click to consume without confirming an instance.");
                }

                BlueprintEraseRegionState.SetDependenciesForTesting(
                    new BlueprintWorldInstanceStore(),
                    BlueprintPlacementWorldContext.Success("pair-ui-owned", "world-ui-owned"));
                var beginErase = BlueprintEraseRegionState.BeginErase(string.Empty);
                if (!beginErase.Succeeded)
                {
                    throw new InvalidOperationException("Expected erase mode to begin for UI ownership test.");
                }

                var eraseUi = BlueprintEraseRegionState.HandlePointer(
                    BlueprintEraseRegionOverlay.BuildPointerInputForTesting(
                        true,
                        false,
                        false,
                        true,
                        true,
                        true,
                        false,
                        true,
                        12,
                        13));
                if (!eraseUi.ShouldConsumeLeftInput || eraseUi.ErasedRegion)
                {
                    throw new InvalidOperationException("Expected UI-owned erase click to consume without erasing an instance region.");
                }

                if (BlueprintCreationOverlay.ShouldConsumeAfterPlayerInputForTesting(true, false, false, true, true) ||
                    BlueprintPlacementPreviewOverlay.ShouldConsumeAfterPlayerInputForTesting(true, false, false, true, true) ||
                    BlueprintEraseRegionOverlay.ShouldConsumeAfterPlayerInputForTesting(true, false, false, true, true))
                {
                    throw new InvalidOperationException("Expected pointer ownership to suppress after-PlayerInput world consume guards.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintPlacementPreviewState.ResetForTesting();
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();
            }
        }

        private static void BlueprintCreationClearFinishAndCancelContracts()
        {
            BlueprintEntryState.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, settings);
            ClickTileForBlueprintCreation(7, 8);

            var finish = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.FinishCreateSave, settings);
            if (!finish.Succeeded ||
                !finish.PlaceholderOnly ||
                !string.Equals(finish.Mode, BlueprintEntryModes.CreatedPendingSave, StringComparison.Ordinal) ||
                !BlueprintCreationMaskState.GetSnapshot().CompletedPendingCapture)
            {
                throw new InvalidOperationException("Expected finish-create to keep only a pending capture mask for 06.");
            }

            BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, settings);
            ClickTileForBlueprintCreation(9, 10);
            var cleared = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.ClearSelection, settings);
            if (!cleared.Succeeded ||
                BlueprintCreationMaskState.GetSnapshot().SelectedCount != 0 ||
                !string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected clear-selection to empty the mask and stay in creating mode.");
            }

            ClickTileForBlueprintCreation(13, 14);
            var exited = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.ExitCreate, settings);
            var exitedSnapshot = BlueprintCreationMaskState.GetSnapshot();
            if (!exited.Succeeded ||
                !string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Tool, StringComparison.Ordinal) ||
                exitedSnapshot.Active ||
                exitedSnapshot.CompletedPendingCapture ||
                exitedSnapshot.SelectedCount != 1 ||
                !HasBlueprintCell(exitedSnapshot, 13, 14))
            {
                throw new InvalidOperationException("Expected exit-create to leave creation mode while preserving the selected mask.");
            }

            var resumed = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, settings);
            var resumedSnapshot = BlueprintCreationMaskState.GetSnapshot();
            if (!resumed.Succeeded ||
                !string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal) ||
                !resumedSnapshot.Active ||
                resumedSnapshot.CompletedPendingCapture ||
                resumedSnapshot.SelectedCount != 1 ||
                !HasBlueprintCell(resumedSnapshot, 13, 14))
            {
                throw new InvalidOperationException("Expected start-create to resume the preserved mask after exit-create.");
            }

            var cancel = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.Cancel, settings);
            if (!cancel.Succeeded ||
                BlueprintCreationMaskState.GetSnapshot().SelectedCount != 0 ||
                !string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Tool, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected cancel to clear the creation mask and return to tool mode.");
            }
        }

        private static void BlueprintCreationOverlayRoutesAndPointerContract()
        {
            if (!BlueprintCreationOverlay.ShouldRegisterWorldOverlayForTesting())
            {
                throw new InvalidOperationException("Expected blueprint creation overlay registration contract to stay enabled.");
            }

            var activeRoutes = InterfaceLayerHookCallbacks.GetGameOverlayDispatcherRouteNamesForTesting(true);
            var fallbackRoutes = InterfaceLayerHookCallbacks.GetGameOverlayDispatcherRouteNamesForTesting(false);
            if (!activeRoutes.Contains("BlueprintCreationOverlay.DrawInterfaceLayer") ||
                !fallbackRoutes.Contains("BlueprintCreationOverlay.DrawInterfaceLayer"))
            {
                throw new InvalidOperationException("Expected blueprint creation overlay to be routed through both game overlay dispatchers.");
            }

            var pointer = BlueprintCreationOverlay.BuildPointerInputForTesting(
                true,
                true,
                false,
                true,
                true,
                false,
                true,
                1,
                2);
            if (!pointer.UiOwned || !pointer.LeftPressed || !pointer.WorldTileHit)
            {
                throw new InvalidOperationException("Expected blueprint pointer contract to preserve UI ownership and world hit metadata.");
            }

            var visual = BlueprintCreationOverlay.GetVisualContractForTesting();
            AssertContains(visual, "lower-saturation-lower-alpha-no-border");
            AssertContains(visual, "continuous-row-runs");
            AssertContains(visual, "world-hover");
            AssertContains(visual, "air-select");
            if (BlueprintCreationOverlay.GetSelectedMaskAlphaForTesting() > 30 ||
                BlueprintCreationOverlay.GetHoverMaskAlphaForTesting() > 24 ||
                BlueprintCreationOverlay.GetDragMaskAlphaForTesting() > 20)
            {
                throw new InvalidOperationException("Expected blueprint creation overlay mask alpha to use the 05 lower-alpha visual contract.");
            }

            if (BlueprintCreationOverlay.ShouldConsumeAfterPlayerInputForTesting(true, true, true) ||
                !BlueprintCreationOverlay.ShouldConsumeAfterPlayerInputForTesting(true, false, true))
            {
                throw new InvalidOperationException("Expected after-PlayerInput consumption to preserve Legacy UI ownership while guarding world input.");
            }
        }

        private static void BlueprintCreationLocalPromptEdgesAndVisualContract()
        {
            var sink = new FakeBlueprintCreationPromptSink();
            BlueprintCreationPromptService.SetSinkForTesting(sink);
            try
            {
                BlueprintEntryState.ResetForTesting();
                var settings = AppSettings.CreateDefault();

                var started = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, settings);
                if (!started.Succeeded ||
                    sink.CallCount != 1 ||
                    !string.Equals(sink.LastEventKind, "start", StringComparison.Ordinal) ||
                    !string.Equals(sink.LastText, "开始创建蓝图选区", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected entering blueprint creation to show the local start prompt once.");
                }

                BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.ClearSelection, settings);
                if (sink.CallCount != 1)
                {
                    throw new InvalidOperationException("Expected same-mode creation commands not to repeat the start prompt.");
                }

                var exited = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, settings);
                if (!exited.Succeeded ||
                    sink.CallCount != 2 ||
                    !string.Equals(sink.LastEventKind, "exit", StringComparison.Ordinal) ||
                    !string.Equals(sink.LastText, "退出创建蓝图", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected repeated create toggle to show the local exit prompt once.");
                }

                BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.ExitCreate, settings);
                if (sink.CallCount != 2)
                {
                    throw new InvalidOperationException("Expected exit-create outside creation mode not to spam local prompts.");
                }

                BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, settings);
                BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.ExitCreate, settings);
                if (sink.CallCount != 4 ||
                    !string.Equals(sink.LastEventKind, "exit", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected explicit exit-create from creation mode to show the exit prompt.");
                }

                var attempt = BlueprintCreationPromptService.GetLastAttemptForTesting();
                if (attempt == null ||
                    !attempt.Attempted ||
                    !attempt.Succeeded ||
                    attempt.DurationFrames != 90 ||
                    attempt.ColorB <= attempt.ColorG ||
                    attempt.ColorG <= attempt.ColorR)
                {
                    throw new InvalidOperationException("Expected blueprint local prompt attempt to carry 90-frame blue text metadata.");
                }

                AssertContains(BlueprintCreationPromptService.GetLocalPromptContractForTesting(), "no-chat");
                AssertContains(BlueprintCreationPromptService.GetLocalPromptContractForTesting(), "no-network");
                AssertContains(BlueprintCreationPromptService.GetLocalPromptContractForTesting(), "no-player-state");
                AssertContains(TerrariaBlueprintCreationPromptCompat.GetLocalPromptContractForTesting(), "PopupText.AdvancedPopupRequest");
                AssertContains(TerrariaBlueprintCreationPromptCompat.GetLocalPromptContractForTesting(), "no chat");
                if (TerrariaBlueprintCreationPromptCompat.GetHeadPromptOffsetYForTesting() >= 0f ||
                    TerrariaBlueprintCreationPromptCompat.GetPromptVelocityYForTesting() >= 0f)
                {
                    throw new InvalidOperationException("Expected blueprint local prompt to spawn above the player's head and float upward.");
                }
            }
            finally
            {
                BlueprintCreationPromptService.SetSinkForTesting(null);
                BlueprintEntryState.ResetForTesting();
            }
        }

        private static void ClickTileForBlueprintCreation(int tileX, int tileY)
        {
            BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = tileX,
                TileY = tileY,
                LeftDown = true,
                LeftPressed = true
            });
            BlueprintCreationMaskState.HandlePointer(new BlueprintCreationPointerInput
            {
                WorldTileHit = true,
                TileX = tileX,
                TileY = tileY,
                LeftReleased = true
            });
        }

        private static void RegisterHandheldOwnershipForWorldOverlayTest(
            BlueprintHandheldActionBarFrame frame,
            string buttonId,
            bool leftConsumed)
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var button = frame.Buttons[0];
            for (var index = 0; index < frame.Buttons.Count; index++)
            {
                if (string.Equals(frame.Buttons[index].Id, buttonId, StringComparison.Ordinal))
                {
                    button = frame.Buttons[index];
                    break;
                }
            }

            var interaction = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true));
            if (!interaction.ShouldConsumeLeftInput)
            {
                throw new InvalidOperationException("Expected handheld button to consume left ownership for test setup.");
            }

            RegisterHandheldOwnershipForWorldOverlayTest(frame, interaction, leftConsumed);
        }

        private static void RegisterHandheldOwnershipForWorldOverlayTest(
            BlueprintHandheldActionBarFrame frame,
            BlueprintHandheldActionBarInteraction interaction,
            bool leftConsumed)
        {
            UiInputFrameClock.BeginUpdateFrame("test.blueprint-ui-ownership");
            var hoveredId = interaction == null ? string.Empty : interaction.HoveredButtonId ?? string.Empty;
            var ownerId = string.IsNullOrWhiteSpace(hoveredId)
                ? "blueprint-handheld-action-bar:frame"
                : BlueprintHandheldActionBarState.BuildCommandElementId(hoveredId);
            UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                ownerId,
                "BlueprintHandheldActionBar",
                frame.Bounds,
                true,
                leftConsumed,
                interaction != null && interaction.ShouldConsumeScroll,
                "test");
        }

        private static bool HasBlueprintCell(BlueprintCreationMaskSnapshot snapshot, int tileX, int tileY)
        {
            return snapshot != null &&
                   snapshot.SelectedCells != null &&
                   snapshot.SelectedCells.Any(cell => cell != null && cell.X == tileX && cell.Y == tileY);
        }

        private sealed class FakeBlueprintCreationPromptSink : IBlueprintCreationPromptSink
        {
            public int CallCount { get; private set; }
            public string LastEventKind { get; private set; }
            public string LastText { get; private set; }

            public bool TryShowBlueprintCreationPrompt(BlueprintCreationPromptRequest request, out string failureReason)
            {
                failureReason = string.Empty;
                CallCount++;
                LastEventKind = request == null ? string.Empty : request.EventKind;
                LastText = request == null ? string.Empty : request.Text;
                return true;
            }
        }
    }
}
