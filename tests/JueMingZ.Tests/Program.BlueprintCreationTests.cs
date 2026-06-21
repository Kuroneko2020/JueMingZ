using System;
using System.Linq;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
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
