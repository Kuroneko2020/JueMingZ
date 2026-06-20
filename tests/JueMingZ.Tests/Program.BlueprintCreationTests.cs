using System;
using System.Linq;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Hooks;
using JueMingZ.UI;

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
                throw new InvalidOperationException("Expected 05 start-create to be implemented.");
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

            BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, settings);
            ClickTileForBlueprintCreation(11, 12);
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

            if (BlueprintCreationOverlay.ShouldConsumeAfterPlayerInputForTesting(true, true, true) ||
                !BlueprintCreationOverlay.ShouldConsumeAfterPlayerInputForTesting(true, false, true))
            {
                throw new InvalidOperationException("Expected after-PlayerInput consumption to preserve Legacy UI ownership while guarding world input.");
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

        private static bool HasBlueprintCell(BlueprintCreationMaskSnapshot snapshot, int tileX, int tileY)
        {
            return snapshot != null &&
                   snapshot.SelectedCells != null &&
                   snapshot.SelectedCells.Any(cell => cell != null && cell.X == tileX && cell.Y == tileY);
        }
    }
}
