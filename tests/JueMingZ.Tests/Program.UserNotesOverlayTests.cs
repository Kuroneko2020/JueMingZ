using System;
using JueMingZ.Automation.Information.Notes;
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
    }
}
