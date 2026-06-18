using System;
using JueMingZ.Automation.Information.Notes;
using JueMingZ.Diagnostics;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void UserNotesPinnedOverlayInitialPlacementAvoidsOverlapAndClamps()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-placement");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot first;
                UserNoteSnapshot second;
                RequireSuccess(cache.CreateDefaultNote(out first), "create first note");
                RequireSuccess(cache.CreateDefaultNote(out second), "create second note");
                UserNoteSnapshot updated;
                RequireSuccess(cache.UpdatePinnedState(
                    first.NoteId,
                    new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 420,
                        Y = 80,
                        Width = 280,
                        Height = 180,
                        OpacityPercent = 100
                    },
                    out updated),
                    "pin first note");

                var state = UserNotesPinnedOverlay.BuildInitialPinnedStateForTesting(cache.Snapshot, second.NoteId, new LegacyUiRect(320, 80, 88, 40), 960, 540);
                var secondRect = new LegacyUiRect((int)state.X, (int)state.Y, (int)state.Width, (int)state.Height);
                var firstRect = new LegacyUiRect(420, 80, 280, 180);
                if (RectsIntersect(firstRect, secondRect))
                {
                    throw new InvalidOperationException("Expected initial pinned note placement to avoid existing notes.");
                }

                if (secondRect.X < 12 || secondRect.Y < 12 || secondRect.Right > 948 || secondRect.Bottom > 528)
                {
                    throw new InvalidOperationException("Expected initial pinned note placement to stay inside the screen safe area.");
                }
            }
            finally
            {
                UserNotesPinnedOverlay.ResetForTesting();
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static void UserNotesPinnedOverlayFrameRestoresPinnedNotesAndHoverControls()
        {
            var snapshot = BuildPinnedNotesSnapshot(
                new UserNoteSnapshot
                {
                    NoteId = "note-a",
                    Body = "第一行\n第二行",
                    PinnedState = new UserNotePinnedState
                    {
                        Pinned = true,
                        X = -100,
                        Y = -60,
                        Width = 280,
                        Height = 180,
                        OpacityPercent = 0
                    }
                });

            var frame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 640, 360, 20, 20);
            if (frame.Items.Count != 1)
            {
                throw new InvalidOperationException("Expected one pinned overlay note.");
            }

            var item = frame.Items[0];
            if (item.Rect.X < 12 || item.Rect.Y < 12)
            {
                throw new InvalidOperationException("Expected offscreen pinned note to be clamped into the safe screen area.");
            }

            if (!item.Hovered || item.OpacityPercent != 0 || item.CloseRect.Width <= 0 || item.DragHandleRect.Width <= 0)
            {
                throw new InvalidOperationException("Expected hover frame to expose controls while preserving 0 opacity.");
            }
        }

        private static void UserNotesPinnedOverlayBodyStartsAtContentTopWhenToolbarHidden()
        {
            UserNotesPinnedOverlay.ResetForTesting();
            var snapshot = BuildPinnedNotesSnapshot(
                new UserNoteSnapshot
                {
                    NoteId = "note-a",
                    Body = BuildRepeatedText("overlay body", 12),
                    PinnedState = new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 100,
                        Y = 80,
                        Width = 280,
                        Height = 150,
                        OpacityPercent = 75
                    }
                });

            var frame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 800, 480, 0, 0);
            var item = frame.Items[0];
            if (item.Hovered)
            {
                throw new InvalidOperationException("Expected a non-hover frame when the mouse is outside the pinned note.");
            }

            if (item.BodyRect.Y != item.Rect.Y + UserNotesPinnedOverlayState.BodyPadding ||
                item.BodyRect.Height != item.Rect.Height - UserNotesPinnedOverlayState.BodyPadding * 2)
            {
                throw new InvalidOperationException("Expected hidden toolbar to reserve no body header space.");
            }

            if (item.ToolbarRect.Bottom > item.Rect.Y)
            {
                throw new InvalidOperationException("Expected the hover toolbar to live outside the body frame when screen space allows.");
            }

            var hoverFrame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 800, 480, item.Rect.X + 4, item.Rect.Y + 4);
            var hoverItem = hoverFrame.Items[0];
            if (!hoverItem.Hovered)
            {
                throw new InvalidOperationException("Expected hovering over the body frame to expose the toolbar.");
            }

            if (hoverItem.BodyRect.X != item.BodyRect.X ||
                hoverItem.BodyRect.Y != item.BodyRect.Y ||
                hoverItem.BodyRect.Width != item.BodyRect.Width ||
                hoverItem.BodyRect.Height != item.BodyRect.Height ||
                hoverItem.BodyMaxScroll != item.BodyMaxScroll)
            {
                throw new InvalidOperationException("Expected hover toolbar visibility to leave body layout and scroll range stable.");
            }
        }

        private static void UserNotesPinnedOverlayBodyWrapMatchesDrawScaleWithoutEllipsis()
        {
            UserNotesPinnedOverlay.ResetForTesting();
            var snapshot = BuildPinnedNotesSnapshot(
                new UserNoteSnapshot
                {
                    NoteId = "note-a",
                    Body = BuildRepeatedText("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 12),
                    PinnedState = new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 100,
                        Y = 80,
                        Width = 220,
                        Height = 140,
                        OpacityPercent = 100
                    }
                });

            var frame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 800, 480, 0, 0);
            var item = frame.Items[0];
            if (item.BodyLines == null || item.BodyLines.Length < 2)
            {
                throw new InvalidOperationException("Expected long pinned body text to wrap into multiple lines.");
            }

            for (var index = 0; index < item.BodyLines.Length; index++)
            {
                var line = item.BodyLines[index] ?? string.Empty;
                if (UiTextRenderer.EstimateTextWidth(line, UserNotesPinnedOverlayState.BodyTextScale) > item.BodyRect.Width)
                {
                    throw new InvalidOperationException("Expected pinned body wrap width to match the draw width.");
                }

                if (!string.Equals(UiTextRenderer.Ellipsize(line, item.BodyRect.Width, UserNotesPinnedOverlayState.BodyTextScale), line, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected pinned body wrapped lines to draw without per-line ellipsis.");
                }
            }
        }

        private static void UserNotesPinnedOverlayScaledMouseHitsVisualControls()
        {
            UserNotesPinnedOverlay.ResetForTesting();
            var note = new UserNoteSnapshot
            {
                NoteId = "note-a",
                Body = BuildRepeatedText("scaled overlay", 24),
                PinnedState = new UserNotePinnedState
                {
                    Pinned = true,
                    X = 100,
                    Y = 80,
                    Width = 280,
                    Height = 150,
                    OpacityPercent = 100
                }
            };
            var snapshot = BuildPinnedNotesSnapshot(note);
            var frame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 1280, 720, 0, 0);
            var item = frame.Items[0];
            var persistedCount = 0;
            UserNotePinnedState lastState = null;
            Func<string, UserNotePinnedState, UserNotesOperationResult> persist = (noteId, state) =>
            {
                persistedCount++;
                lastState = state == null ? null : state.Clone();
                return UserNotesOperationResult.Success("saved", "saved");
            };

            var closeX = item.CloseRect.X + item.CloseRect.Width / 2;
            var closeY = item.CloseRect.Y + item.CloseRect.Height / 2;
            var closeMouse = BuildScaledPinnedOverlayMouseAtUiPoint(closeX, closeY, true);
            if (closeMouse.X != closeX || closeMouse.Y != closeY || !closeMouse.ReadMode.Contains("InterfaceOverlay"))
            {
                throw new InvalidOperationException("Expected pinned overlay mouse reads to stay in visual interface coordinates under capped UI scale.");
            }

            var closeFrame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 1280, 720, closeMouse.X, closeMouse.Y);
            var close = UserNotesPinnedOverlay.HandleInputForTesting(
                closeFrame,
                closeMouse.X,
                closeMouse.Y,
                true,
                true,
                false,
                0,
                1280,
                720,
                persist);
            if (!close.Unpinned || lastState == null || lastState.Pinned)
            {
                throw new InvalidOperationException("Expected scaled close visual rect to unpin the note.");
            }

            var opacityX = item.DecreaseOpacityRect.X + item.DecreaseOpacityRect.Width / 2;
            var opacityY = item.DecreaseOpacityRect.Y + item.DecreaseOpacityRect.Height / 2;
            var opacityMouse = BuildScaledPinnedOverlayMouseAtUiPoint(opacityX, opacityY, true);
            var opacityFrame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 1280, 720, opacityMouse.X, opacityMouse.Y);
            var opacity = UserNotesPinnedOverlay.HandleInputForTesting(
                opacityFrame,
                opacityMouse.X,
                opacityMouse.Y,
                true,
                true,
                false,
                0,
                1280,
                720,
                persist);
            if (!opacity.OpacityChanged || lastState == null || lastState.OpacityPercent != 95)
            {
                throw new InvalidOperationException("Expected scaled opacity visual rect to save one 5 percent step.");
            }

            UserNotesPinnedOverlay.ResetForTesting();
            var dragX = item.DragHandleRect.X + item.DragHandleRect.Width / 2;
            var dragY = item.DragHandleRect.Y + item.DragHandleRect.Height / 2;
            var dragMouse = BuildScaledPinnedOverlayMouseAtUiPoint(dragX, dragY, true);
            var dragFrame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 1280, 720, dragMouse.X, dragMouse.Y);
            var dragStart = UserNotesPinnedOverlay.HandleInputForTesting(
                dragFrame,
                dragMouse.X,
                dragMouse.Y,
                true,
                true,
                false,
                0,
                1280,
                720,
                persist);
            if (!dragStart.DragStarted || !dragStart.CapturedMouse)
            {
                throw new InvalidOperationException("Expected scaled drag handle visual rect to start a captured drag.");
            }

            var movedMouse = BuildScaledPinnedOverlayMouseAtUiPoint(dragX + 44, dragY + 32, true);
            var draggingFrame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 1280, 720, movedMouse.X, movedMouse.Y);
            var dragging = UserNotesPinnedOverlay.HandleInputForTesting(
                draggingFrame,
                movedMouse.X,
                movedMouse.Y,
                true,
                false,
                false,
                0,
                1280,
                720,
                persist);
            if (!dragging.Dragging || dragging.PendingState == null || dragging.PendingState.X <= note.PinnedState.X)
            {
                throw new InvalidOperationException("Expected scaled drag movement to expose a transient moved pinned state.");
            }

            var released = UserNotesPinnedOverlay.HandleInputForTesting(
                draggingFrame,
                movedMouse.X,
                movedMouse.Y,
                false,
                false,
                true,
                0,
                1280,
                720,
                persist);
            if (!released.DragSaved || lastState == null || !lastState.Pinned || lastState.X <= note.PinnedState.X)
            {
                throw new InvalidOperationException("Expected scaled drag release to save the moved pinned state.");
            }
        }

        private static void UserNotesPinnedOverlayProcessesClickAfterPlayerInput()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-playerinput-click");
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousGameMenu = Terraria.Main.gameMenu;
            var previousMapFullscreen = Terraria.Main.mapFullscreen;
            try
            {
                ResetUiInputFrameTestState();
                DisableUserNotesDiagnosticsForTesting();
                UserNotesUiState.ResetForTesting();
                LegacyMainUiState.SetVisible(false);
                Terraria.Main.gameMenu = false;
                Terraria.Main.mapFullscreen = false;
                UserNotesPinnedOverlay.ResetForTesting();
                UserNotesPinnedOverlay.SetShouldUseOverlayOverrideForTesting(true);
                Terraria.Main.mouseLeft = false;
                Terraria.Main.mouseLeftRelease = false;
                if (LegacyMainUiState.Visible)
                {
                    throw new InvalidOperationException("Expected test setup to hide the F5 window before exercising pinned overlay input.");
                }

                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create pinned note");
                UserNoteSnapshot updated;
                RequireSuccess(cache.UpdatePinnedState(
                    note.NoteId,
                    new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 100,
                        Y = 80,
                        Width = 280,
                        Height = 150,
                        OpacityPercent = 100
                    },
                    out updated),
                    "seed pinned note");
                UserNotesUiState.SetCacheForTesting(cache, true);

                var frame = UserNotesPinnedOverlay.BuildFrameForTesting(cache.Snapshot, 1280, 800, 0, 0);
                var item = frame.Items[0];
                var closeX = item.CloseRect.X + item.CloseRect.Width / 2;
                var closeY = item.CloseRect.Y + item.CloseRect.Height / 2;

                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "test-prefix",
                    true,
                    true,
                    BuildPinnedOverlayRawMouseAtUiPoint(closeX, closeY, false));
                if (cache.Snapshot.Notes[0].PinnedState == null || !cache.Snapshot.Notes[0].PinnedState.Pinned)
                {
                    throw new InvalidOperationException("Expected prefix without a left-button edge to leave the pinned note visible.");
                }

                Terraria.Main.mouseLeft = true;
                Terraria.Main.mouseLeftRelease = true;
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "test-playerinput-postfix",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPoint(closeX, closeY, true));

                var interaction = UserNotesPinnedOverlayState.LastInteraction;
                var pinnedAfter = cache.Snapshot.Notes[0].PinnedState;
                if (!interaction.Unpinned || pinnedAfter == null || pinnedAfter.Pinned)
                {
                    throw new InvalidOperationException(
                        "Expected PlayerInput postfix left edge to unpin the hovered pinned note. Interaction=" +
                        interaction.ToVerificationJson() +
                        ", pinnedAfter=" + (pinnedAfter == null ? "null" : pinnedAfter.Pinned.ToString()) +
                        ", runtimeErrorSource=" + RuntimeDiagnostics.LastErrorSource +
                        ", runtimeError=" + RuntimeDiagnostics.LastError);
                }

                if (Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected pinned overlay toolbar click to consume the vanilla left-button pulse.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.mapFullscreen = previousMapFullscreen;
                Terraria.Main.mouseLeft = false;
                Terraria.Main.mouseLeftRelease = false;
                UserNotesUiState.ResetForTesting();
                UserNotesPinnedOverlay.ResetForTesting();
                ResetUserNotesTestingHooks();
                restoreRuntimeTypes();
                restore();
            }
        }

        private static void UserNotesPinnedOverlayScrollDragOpacityAndCloseUsePinnedState()
        {
            UserNotesPinnedOverlay.ResetForTesting();
            var note = new UserNoteSnapshot
            {
                NoteId = "note-a",
                Body = BuildRepeatedText("滚动正文", 80),
                PinnedState = new UserNotePinnedState
                {
                    Pinned = true,
                    X = 100,
                    Y = 80,
                    Width = 280,
                    Height = 150,
                    OpacityPercent = 100
                }
            };
            var snapshot = BuildPinnedNotesSnapshot(note);
            var frame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 800, 480, 120, 128);
            var item = frame.Items[0];
            var persistedCount = 0;
            UserNotePinnedState lastState = null;
            Func<string, UserNotePinnedState, UserNotesOperationResult> persist = (noteId, state) =>
            {
                persistedCount++;
                lastState = state == null ? null : state.Clone();
                return UserNotesOperationResult.Success("saved", "saved");
            };

            var scroll = UserNotesPinnedOverlay.HandleInputForTesting(
                frame,
                item.BodyRect.X + 4,
                item.BodyRect.Y + 4,
                false,
                false,
                false,
                -120,
                800,
                480,
                persist);
            if (!scroll.ScrollConsumed || scroll.BodyScrollAfter <= scroll.BodyScrollBefore || persistedCount != 0)
            {
                throw new InvalidOperationException("Expected pinned note body wheel to scroll locally without saving index.");
            }

            var dragStart = UserNotesPinnedOverlay.HandleInputForTesting(
                frame,
                item.DragHandleRect.X + 2,
                item.DragHandleRect.Y + 2,
                true,
                true,
                false,
                0,
                800,
                480,
                persist);
            if (!dragStart.DragStarted || !dragStart.CapturedMouse)
            {
                throw new InvalidOperationException("Expected drag handle press to start a captured drag.");
            }

            var draggingFrame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 800, 480, 220, 210);
            var dragging = UserNotesPinnedOverlay.HandleInputForTesting(
                draggingFrame,
                220,
                210,
                true,
                false,
                false,
                0,
                800,
                480,
                persist);
            if (!dragging.Dragging || dragging.PendingState == null || dragging.PendingState.X <= note.PinnedState.X)
            {
                throw new InvalidOperationException("Expected active drag to expose the moved transient pinned state.");
            }

            var released = UserNotesPinnedOverlay.HandleInputForTesting(
                draggingFrame,
                220,
                210,
                false,
                false,
                true,
                0,
                800,
                480,
                persist);
            if (!released.DragSaved || persistedCount != 1 || lastState == null || !lastState.Pinned)
            {
                throw new InvalidOperationException("Expected drag release to save exactly one pinned position.");
            }

            UserNotesPinnedOverlay.ResetForTesting();
            frame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 800, 480, item.DecreaseOpacityRect.X + 2, item.DecreaseOpacityRect.Y + 2);
            item = frame.Items[0];
            var opacity = UserNotesPinnedOverlay.HandleInputForTesting(
                frame,
                item.DecreaseOpacityRect.X + 2,
                item.DecreaseOpacityRect.Y + 2,
                true,
                true,
                false,
                0,
                800,
                480,
                persist);
            if (!opacity.OpacityChanged || lastState == null || lastState.OpacityPercent != 95)
            {
                throw new InvalidOperationException("Expected opacity arrow to save a 5 percent step.");
            }

            var close = UserNotesPinnedOverlay.HandleInputForTesting(
                frame,
                item.CloseRect.X + 2,
                item.CloseRect.Y + 2,
                true,
                true,
                false,
                0,
                800,
                480,
                persist);
            if (!close.Unpinned || lastState == null || lastState.Pinned)
            {
                throw new InvalidOperationException("Expected close button to unpin without deleting the note.");
            }
        }

        private static void UserNotesPinnedOverlayStoreReloadAndDeleteSync()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-store-sync");
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                var store = new UserNotesStore();
                var cache = new UserNotesCache(store);
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create note");
                RequireSuccess(cache.SaveNote(note.NoteId, "title", "body", out note), "seed note");
                UserNotesUiState.SetCacheForTesting(cache, true);

                UserNoteSnapshot updated;
                RequireSuccess(UserNotesUiState.UpdatePinnedState(
                    note.NoteId,
                    new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 90,
                        Y = 70,
                        Width = 260,
                        Height = 140,
                        OpacityPercent = 60
                    },
                    "Ui.Notes.Pin",
                    out updated),
                    "pin through UI state");

                var reloaded = new UserNotesCache(store);
                RequireSuccess(reloaded.Refresh(), "reload notes");
                var frame = UserNotesPinnedOverlay.BuildFrameForTesting(reloaded.Snapshot, 800, 480, 100, 80);
                if (frame.Items.Count != 1 || frame.Items[0].OpacityPercent != 60)
                {
                    throw new InvalidOperationException("Expected pinned overlay state to survive cache reload.");
                }

                RequireSuccess(cache.DeleteNote(note.NoteId), "delete pinned note");
                frame = UserNotesPinnedOverlay.BuildFrameForTesting(cache.Snapshot, 800, 480, 100, 80);
                if (frame.Items.Count != 0)
                {
                    throw new InvalidOperationException("Expected deleting a pinned note from F5/store to remove it from overlay snapshot.");
                }
            }
            finally
            {
                UserNotesUiState.ResetForTesting();
                UserNotesPinnedOverlay.ResetForTesting();
                ResetUserNotesTestingHooks();
                restore();
            }
        }

        private static UserNotesSnapshot BuildPinnedNotesSnapshot(params UserNoteSnapshot[] notes)
        {
            return new UserNotesSnapshot(notes, Environment.TickCount);
        }

        private static bool RectsIntersect(LegacyUiRect a, LegacyUiRect b)
        {
            return a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
        }

        private static DiagnosticMouseState BuildPinnedOverlayRawMouseAtUiPoint(int uiX, int uiY, bool leftDown)
        {
            return new DiagnosticMouseState
            {
                GameInputAvailable = true,
                TerrariaReadAvailable = true,
                TerrariaMouseX = uiX,
                TerrariaMouseY = uiY,
                TerrariaLeftDown = leftDown,
                OsReadAvailable = true,
                OsClientMouseX = uiX,
                OsClientMouseY = uiY,
                OsLeftDown = leftDown,
                ReadMode = "Terraria+OsClient"
            };
        }

        private static LegacyMouseSnapshot BuildScaledPinnedOverlayMouseAtUiPoint(int uiX, int uiY, bool leftDown)
        {
            const double scale = 1.5d;
            var screenX = (int)Math.Round(uiX * scale);
            var screenY = (int)Math.Round(uiY * scale);
            return UserNotesPinnedOverlay.ReadOverlayMouseForTesting(new DiagnosticMouseState
            {
                GameInputAvailable = true,
                TerrariaReadAvailable = true,
                TerrariaMouseX = screenX,
                TerrariaMouseY = screenY,
                TerrariaLeftDown = leftDown,
                OsReadAvailable = true,
                OsClientMouseX = screenX,
                OsClientMouseY = screenY,
                OsLeftDown = leftDown,
                UiScaleAvailable = true,
                UiScaleMatrixAvailable = true,
                UiScale = scale,
                UiScaleX = scale,
                UiScaleY = scale,
                ReadMode = "Terraria+OsClient",
                UiScaleSource = "test"
            });
        }
    }
}
