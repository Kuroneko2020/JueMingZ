using System;
using JueMingZ.Automation.Information.Notes;
using JueMingZ.Compat;
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

                var state = UserNotesPinnedOverlay.BuildInitialPinnedStateForTesting(cache.Snapshot, second.NoteId, new LegacyUiRect(320, 80, 88, 40), 1280, 720);
                var secondRect = new LegacyUiRect((int)state.X, (int)state.Y, (int)state.Width, (int)state.Height);
                var firstRect = new LegacyUiRect(420, 80, 280, 180);
                if (RectsIntersect(firstRect, secondRect))
                {
                    throw new InvalidOperationException("Expected initial pinned note placement to avoid existing notes.");
                }

                if (secondRect.X < 12 || secondRect.Y < 12 || secondRect.Right > 1268 || secondRect.Bottom > 708)
                {
                    throw new InvalidOperationException("Expected initial pinned note placement to stay inside the screen safe area.");
                }

                if (state.OpacityPercent != 0)
                {
                    throw new InvalidOperationException("Expected initial pinned note opacity to default to 0 percent opacity so UI transparency starts at 100 percent.");
                }

                var visibleBodyRows = (state.Height - UserNotesPinnedOverlayState.BodyPadding * 2) / UserNotesPinnedOverlayState.LineHeight;
                if (visibleBodyRows < UserNotesPinnedOverlayState.DefaultVisibleBodyLines ||
                    UserNotesPinnedOverlayState.DefaultVisibleBodyLines < 8)
                {
                    throw new InvalidOperationException("Expected default pinned note height to fit at least eight body text lines.");
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

        private static void UserNotesPinnedOverlayInputTraceDescribesControlAndCapture()
        {
            UserNotesPinnedOverlay.ResetForTesting();
            var snapshot = BuildPinnedNotesSnapshot(
                new UserNoteSnapshot
                {
                    NoteId = "note-a",
                    Body = "trace",
                    PinnedState = new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 100,
                        Y = 80,
                        Width = 280,
                        Height = 150,
                        OpacityPercent = 100
                    }
                });

            var frame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 1280, 800, 0, 0);
            var item = frame.Items[0];
            var closeX = item.CloseRect.X + item.CloseRect.Width / 2;
            var closeY = item.CloseRect.Y + item.CloseRect.Height / 2;
            var hit = UserNotesPinnedOverlayState.BuildHitDiagnostics(frame, closeX, closeY);
            if (!hit.MouseInside || !hit.CloseHit || !string.Equals(hit.ControlId, "close", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected input trace hit diagnostics to identify the close control.");
            }

            var json = UserNotesPinnedOverlayInputDiagnostics.BuildVerificationJsonForTesting(
                "test-playerinput-postfix",
                true,
                false,
                new LegacyMouseSnapshot
                {
                    X = closeX,
                    Y = closeY,
                    LeftDown = true,
                    LeftPressed = true,
                    ReadAvailable = true,
                    ReadMode = "TerrariaOnly/InterfaceOverlay"
                },
                hit,
                new UserNotesPinnedOverlayInteraction
                {
                    HitNoteId = "note-a",
                    CapturedMouse = true,
                    Unpinned = true,
                    PersistResult = UserNotesOperationResult.Success("saved", "saved")
                },
                new UserNotesPinnedOverlaySuppressionDiagnostics
                {
                    MouseCaptureRequested = true,
                    MouseCaptureSucceeded = true,
                    ButtonConsumeRequested = true,
                    ButtonConsumeSucceeded = true,
                    ButtonConsumeMessage = "Mouse trigger input consumed for MouseLeft."
                },
                0,
                false);

            AssertContains(json, "\"source\":\"test-playerinput-postfix\"");
            AssertContains(json, "\"controlId\":\"close\"");
            AssertContains(json, "\"mouseX\":" + closeX);
            AssertContains(json, "\"leftPressed\":true");
            AssertContains(json, "\"legacyWindowOwnsMouse\":false");
            AssertContains(json, "\"mouseCaptureRequested\":true");
            AssertContains(json, "\"buttonConsumeSucceeded\":true");
            AssertContains(json, "\"persistResultCode\":\"saved\"");
        }

        private static void UserNotesPinnedOverlayInputTraceRequiresPinnedScope()
        {
            UserNotesPinnedOverlay.ResetForTesting();
            UserNotesPinnedOverlayInputDiagnostics.ResetForTesting();

            var emptyFrame = UserNotesPinnedOverlay.BuildFrameForTesting(BuildPinnedNotesSnapshot(), 1280, 800, 320, 120);
            if (UserNotesPinnedOverlay.ShouldProcessInputFrameForTesting(emptyFrame))
            {
                throw new InvalidOperationException("Expected an empty pinned overlay frame to skip input diagnostics.");
            }

            var f5Mouse = new LegacyMouseSnapshot
            {
                X = 320,
                Y = 120,
                LeftDown = true,
                LeftPressed = true,
                ReadAvailable = true,
                ReadMode = "Test/LegacyWindow",
                WindowHit = true
            };
            var noPinnedHit = new UserNotesPinnedOverlayHitDiagnostics
            {
                MouseX = 320,
                MouseY = 120,
                ControlId = "none"
            };
            if (UserNotesPinnedOverlayInputDiagnostics.ShouldRecordForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    f5Mouse,
                    noPinnedHit,
                    new UserNotesPinnedOverlayInteraction(),
                    new UserNotesPinnedOverlaySuppressionDiagnostics(),
                    -120,
                    true,
                    "legacyWindowOwnsMouse"))
            {
                throw new InvalidOperationException("F5 window ownership without a pinned-note hit must not record Ui.Notes.InputTrace.");
            }

            var pinnedSnapshot = BuildPinnedNotesSnapshot(
                new UserNoteSnapshot
                {
                    NoteId = "note-a",
                    Body = "trace",
                    PinnedState = new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 100,
                        Y = 80,
                        Width = 280,
                        Height = 150,
                        OpacityPercent = 100
                    }
                });
            var pinnedFrame = UserNotesPinnedOverlay.BuildFrameForTesting(pinnedSnapshot, 1280, 800, 104, 84);
            if (!UserNotesPinnedOverlay.ShouldProcessInputFrameForTesting(pinnedFrame))
            {
                throw new InvalidOperationException("Expected a pinned overlay frame to keep input diagnostics enabled.");
            }

            var pinnedHit = UserNotesPinnedOverlayState.BuildHitDiagnostics(pinnedFrame, 104, 84);
            if (!pinnedHit.MouseInside ||
                !UserNotesPinnedOverlayInputDiagnostics.ShouldRecordForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    new LegacyMouseSnapshot
                    {
                        X = 104,
                        Y = 84,
                        ReadAvailable = true,
                        ReadMode = "Test/PinnedOverlay"
                    },
                    pinnedHit,
                    new UserNotesPinnedOverlayInteraction { MouseInside = true, HitNoteId = "note-a" },
                    new UserNotesPinnedOverlaySuppressionDiagnostics(),
                    0,
                    false,
                    "hover"))
            {
                throw new InvalidOperationException("Expected real pinned-note hover to remain eligible for Ui.Notes.InputTrace.");
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

        private static void UserNotesPinnedOverlayToolbarHandleIsCenteredAndSeparatedFromButtons()
        {
            UserNotesPinnedOverlay.ResetForTesting();
            var snapshot = BuildPinnedNotesSnapshot(
                new UserNoteSnapshot
                {
                    NoteId = "note-a",
                    Body = BuildRepeatedText("toolbar layout", 20),
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

            var frame = UserNotesPinnedOverlay.BuildFrameForTesting(snapshot, 800, 480, 120, 120);
            var item = frame.Items[0];
            if (UserNotesPinnedOverlayState.BodyTextScale < 1.20f ||
                UserNotesPinnedOverlayState.LineHeight < 36 ||
                UserNotesPinnedOverlayState.DefaultVisibleBodyLines < 8 ||
                UserNotesPinnedOverlayState.DefaultHeight < UserNotesPinnedOverlayState.BodyPadding * 2 + UserNotesPinnedOverlayState.LineHeight * 8 ||
                UserNotesPinnedOverlayState.ToolbarHeight < 28)
            {
                throw new InvalidOperationException("Expected pinned overlay body text, default height, and toolbar metrics to be enlarged together.");
            }

            if (item.DragHandleRect.Width < 84)
            {
                throw new InvalidOperationException("Expected the pinned overlay drag handle to be visibly longer than the old short handle.");
            }

            var toolbarCenter = item.ToolbarRect.X + item.ToolbarRect.Width / 2;
            var handleCenter = item.DragHandleRect.X + item.DragHandleRect.Width / 2;
            if (Math.Abs(handleCenter - toolbarCenter) > 8)
            {
                throw new InvalidOperationException("Expected the pinned overlay drag handle to stay near the toolbar center.");
            }

            if (RectsIntersect(item.DragHandleRect, item.DecreaseOpacityRect) ||
                RectsIntersect(item.DragHandleRect, item.IncreaseOpacityRect) ||
                RectsIntersect(item.DragHandleRect, item.CloseRect) ||
                RectsIntersect(item.DecreaseOpacityRect, item.IncreaseOpacityRect) ||
                RectsIntersect(item.IncreaseOpacityRect, item.CloseRect))
            {
                throw new InvalidOperationException("Expected the centered drag handle, opacity buttons, and close button to keep non-overlapping hit rects.");
            }

            var dragHit = UserNotesPinnedOverlayState.BuildHitDiagnostics(
                frame,
                item.DragHandleRect.X + item.DragHandleRect.Width / 2,
                item.DragHandleRect.Y + item.DragHandleRect.Height / 2);
            if (!dragHit.DragHandleHit || !string.Equals(dragHit.ControlId, "drag", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected centered drag handle hit diagnostics to resolve drag.");
            }

            var opacityHit = UserNotesPinnedOverlayState.BuildHitDiagnostics(
                frame,
                item.IncreaseOpacityRect.X + item.IncreaseOpacityRect.Width / 2,
                item.IncreaseOpacityRect.Y + item.IncreaseOpacityRect.Height / 2);
            if (!opacityHit.IncreaseOpacityHit || !string.Equals(opacityHit.ControlId, "opacity-increase", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected enlarged opacity button hit diagnostics to resolve opacity-increase.");
            }

            var closeHit = UserNotesPinnedOverlayState.BuildHitDiagnostics(
                frame,
                item.CloseRect.X + item.CloseRect.Width / 2,
                item.CloseRect.Y + item.CloseRect.Height / 2);
            if (!closeHit.CloseHit || !string.Equals(closeHit.ControlId, "close", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected enlarged close button hit diagnostics to resolve close.");
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
            if (closeMouse.X != closeX ||
                closeMouse.Y != closeY ||
                closeMouse.ReadMode.IndexOf("OsClientScreen", StringComparison.Ordinal) < 0 ||
                !closeMouse.ReadMode.Contains("InterfaceOverlay"))
            {
                throw new InvalidOperationException("Expected pinned overlay mouse reads to stay in visual screen coordinates under capped UI scale.");
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

        private static void UserNotesPinnedOverlayRightEdgeUsesScreenMouseAndClamps()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-right-edge-interface");
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousGameMenu = Terraria.Main.gameMenu;
            var previousMapFullscreen = Terraria.Main.mapFullscreen;
            var previousScreenWidth = Terraria.Main.screenWidth;
            var previousScreenHeight = Terraria.Main.screenHeight;
            try
            {
                ResetUiInputFrameTestState();
                DisableUserNotesDiagnosticsForTesting();
                UserNotesUiState.ResetForTesting();
                LegacyMainUiState.SetVisible(false);
                Terraria.Main.gameMenu = false;
                Terraria.Main.mapFullscreen = false;
                Terraria.Main.screenWidth = 2560;
                Terraria.Main.screenHeight = 1440;
                TerrariaMainCompat.SetScreenSizeOverrideForTesting(2560, 1440);
                Terraria.Main.mouseLeft = false;
                Terraria.Main.mouseLeftRelease = false;
                UserNotesPinnedOverlay.ResetForTesting();
                UserNotesPinnedOverlay.SetShouldUseOverlayOverrideForTesting(true);

                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create pinned note");
                UserNoteSnapshot updated;
                RequireSuccess(cache.UpdatePinnedState(
                    note.NoteId,
                    new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 2500,
                        Y = 420,
                        Width = 280,
                        Height = 180,
                        OpacityPercent = 100
                    },
                    out updated),
                    "seed right-edge pinned note");
                UserNotesUiState.SetCacheForTesting(cache, true);

                const double scale = 1.2d;
                var overlayWidth = Terraria.Main.screenWidth;
                var overlayHeight = Terraria.Main.screenHeight;
                var frame = UserNotesPinnedOverlay.BuildFrameForTesting(cache.Snapshot, overlayWidth, overlayHeight, 0, 0);
                var item = frame.Items[0];
                var expectedClampedX = overlayWidth - item.Rect.Width - UserNotesPinnedOverlayState.ScreenPadding;
                if (item.Rect.X != expectedClampedX)
                {
                    throw new InvalidOperationException("Expected right-edge pinned note frame to clamp in interface coordinates; x=" + item.Rect.X + ", expected=" + expectedClampedX + ".");
                }

                var opacityX = item.DecreaseOpacityRect.X + item.DecreaseOpacityRect.Width / 2;
                var opacityY = item.DecreaseOpacityRect.Y + item.DecreaseOpacityRect.Height / 2;
                var raw = BuildScaledPinnedOverlayRawMouseWithTerrariaAndOs(opacityX + 92, opacityY + 180, opacityX, opacityY, true, scale);
                var mouse = UserNotesPinnedOverlay.ReadOverlayMouseForTesting(raw);
                if (mouse.X != opacityX ||
                    mouse.Y != opacityY ||
                    mouse.ReadMode.IndexOf("OsClientScreen", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Expected pinned overlay to prefer OS client screen coordinates at the right edge; mouse=" + mouse.X + "," + mouse.Y + " mode=" + mouse.ReadMode + ".");
                }

                var overlayScreenWidth = UserNotesPinnedOverlay.OverlayScreenWidthForTesting(raw);
                var overlayScreenHeight = UserNotesPinnedOverlay.OverlayScreenHeightForTesting(raw);
                var inputFrame = UserNotesPinnedOverlay.BuildOverlayFrameForTesting(raw);
                var inputItem = inputFrame.Items[0];
                if (overlayScreenWidth != overlayWidth ||
                    overlayScreenHeight != overlayHeight ||
                    !string.Equals(UserNotesPinnedOverlay.OverlayCoordinateModeForTesting(raw), "ScreenUnscaled", StringComparison.Ordinal) ||
                    inputItem.Rect.X != expectedClampedX ||
                    !inputItem.DecreaseOpacityRect.Contains(mouse.X, mouse.Y))
                {
                    throw new InvalidOperationException(
                        "Expected production overlay frame to use scaled interface extents and hit the visual opacity button; overlay=" +
                        overlayScreenWidth +
                        "x" +
                        overlayScreenHeight +
                        ", expectedOverlay=" +
                        overlayWidth +
                        "x" +
                        overlayHeight +
                        ", frameX=" +
                        inputItem.Rect.X +
                        ", expectedX=" +
                        expectedClampedX +
                        ", mouse=" +
                        mouse.X +
                        "," +
                        mouse.Y +
                        ", opacityRect=" +
                        inputItem.DecreaseOpacityRect.X +
                        "," +
                        inputItem.DecreaseOpacityRect.Y +
                        "," +
                        inputItem.DecreaseOpacityRect.Width +
                        "x" +
                        inputItem.DecreaseOpacityRect.Height);
                }

                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdatePrefixGuard",
                    true,
                    true,
                    raw);

                var prefixInteraction = UserNotesPinnedOverlayState.LastInteraction;
                if (prefixInteraction.OpacityChanged || prefixInteraction.Unpinned || prefixInteraction.DragStarted)
                {
                    throw new InvalidOperationException(
                        "Expected right-edge stale prefix coordinates to arm but not execute a toolbar command. Interaction=" +
                        prefixInteraction.ToVerificationJson());
                }

                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildScaledPinnedOverlayRawMouseWithTerrariaAndOs(opacityX, opacityY, opacityX, opacityY, true, scale));

                var postfixInteraction = UserNotesPinnedOverlayState.LastInteraction;
                var pinned = cache.Snapshot.Notes[0].PinnedState;
                if (!postfixInteraction.OpacityChanged ||
                    pinned.OpacityPercent != 95 ||
                    (int)Math.Round(pinned.X) != expectedClampedX)
                {
                    throw new InvalidOperationException(
                        "Expected right-edge visual opacity button to hit and persist the clamped screen position. Interaction=" +
                        postfixInteraction.ToVerificationJson() +
                        ", opacity=" + pinned.OpacityPercent +
                        ", x=" + pinned.X +
                        ", expectedX=" + expectedClampedX);
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetScreenSizeOverrideForTesting(null, null);
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.mapFullscreen = previousMapFullscreen;
                Terraria.Main.screenWidth = previousScreenWidth;
                Terraria.Main.screenHeight = previousScreenHeight;
                Terraria.Main.mouseLeft = false;
                Terraria.Main.mouseLeftRelease = false;
                UserNotesUiState.ResetForTesting();
                UserNotesPinnedOverlay.ResetForTesting();
                ResetUserNotesTestingHooks();
                restoreRuntimeTypes();
                restore();
            }
        }

        private static void UserNotesPinnedOverlayScreenCoordinatesMatchFrozenRightSideSample()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-frozen-right-side-sample");
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousGameMenu = Terraria.Main.gameMenu;
            var previousMapFullscreen = Terraria.Main.mapFullscreen;
            var previousScreenWidth = Terraria.Main.screenWidth;
            var previousScreenHeight = Terraria.Main.screenHeight;
            try
            {
                DisableUserNotesDiagnosticsForTesting();
                UserNotesUiState.ResetForTesting();
                LegacyMainUiState.SetVisible(false);
                Terraria.Main.gameMenu = false;
                Terraria.Main.mapFullscreen = false;
                Terraria.Main.screenWidth = 2560;
                Terraria.Main.screenHeight = 1440;
                TerrariaMainCompat.SetScreenSizeOverrideForTesting(2560, 1440);
                UserNotesPinnedOverlay.ResetForTesting();
                UserNotesPinnedOverlay.SetShouldUseOverlayOverrideForTesting(true);

                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create pinned note");
                UserNoteSnapshot updated;
                RequireSuccess(cache.UpdatePinnedState(
                    note.NoteId,
                    new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 1567,
                        Y = 111,
                        Width = 280,
                        Height = 180,
                        OpacityPercent = 100
                    },
                    out updated),
                    "seed frozen right-side pinned note");
                UserNotesUiState.SetCacheForTesting(cache, true);

                const double scale = 1.2d;
                var raw = BuildScaledPinnedOverlayRawMouseWithTerrariaAndOs(1337, 252, 1604, 250, false, scale);
                var mouse = UserNotesPinnedOverlay.ReadOverlayMouseForTesting(raw);
                var frame = UserNotesPinnedOverlay.BuildOverlayFrameForTesting(raw);
                var hit = UserNotesPinnedOverlayState.BuildHitDiagnostics(frame, mouse.X, mouse.Y);
                if (mouse.X != 1604 ||
                    mouse.Y != 250 ||
                    frame.ScreenWidth != 2560 ||
                    !string.Equals(frame.CoordinateMode, "ScreenUnscaled", StringComparison.Ordinal) ||
                    frame.Items.Count != 1 ||
                    frame.Items[0].Rect.X != 1567 ||
                    !hit.MouseInside)
                {
                    throw new InvalidOperationException(
                        "Expected frozen right-side sample to keep OS client coordinates in the visual screen rect; mouse=" +
                        mouse.X +
                        "," +
                        mouse.Y +
                        ", mode=" +
                        mouse.ReadMode +
                        ", screen=" +
                        frame.ScreenWidth +
                        "x" +
                        frame.ScreenHeight +
                        ", coordinateMode=" +
                        frame.CoordinateMode +
                        ", itemX=" +
                        (frame.Items.Count <= 0 ? -1 : frame.Items[0].Rect.X) +
                        ", hit=" +
                        hit.MouseInside);
                }
            }
            finally
            {
                TerrariaMainCompat.SetScreenSizeOverrideForTesting(null, null);
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.mapFullscreen = previousMapFullscreen;
                Terraria.Main.screenWidth = previousScreenWidth;
                Terraria.Main.screenHeight = previousScreenHeight;
                UserNotesUiState.ResetForTesting();
                UserNotesPinnedOverlay.ResetForTesting();
                ResetUserNotesTestingHooks();
                restoreRuntimeTypes();
                restore();
            }
        }

        private static void UserNotesPinnedOverlayInitialPlacementUsesScreenExtentUnderUiScale()
        {
            UserNotesPinnedOverlay.ResetForTesting();
            const double scale = 1.2d;
            var raw = BuildScaledPinnedOverlayRawMouseWithTerrariaAndOs(0, 0, 0, 0, false, scale);
            var context = UserNotesPinnedOverlayCoordinates.ResolveScreenContext(raw, 2560, 1440);
            var snapshot = BuildPinnedNotesSnapshot();
            var state = UserNotesPinnedOverlay.BuildInitialPinnedStateForTesting(
                snapshot,
                "note-a",
                new LegacyUiRect(1900, 80, 80, 40),
                context.ScreenWidth,
                context.ScreenHeight);

            if (context.ScreenWidth != 2560 ||
                context.ScreenHeight != 1440 ||
                !string.Equals(context.CoordinateMode, "ScreenUnscaled", StringComparison.Ordinal) ||
                (int)Math.Round(state.X) != 1992)
            {
                throw new InvalidOperationException(
                    "Expected initial pinned placement to use the unscaled screen extent under UI scale; screen=" +
                    context.ScreenWidth +
                    "x" +
                    context.ScreenHeight +
                    ", mode=" +
                    context.CoordinateMode +
                    ", x=" +
                    state.X);
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

        private static void UserNotesPinnedOverlayTransfersPrefixPressToPlayerInputToolbarHit()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-toolbar-press-transfer");
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
                var bodyX = item.BodyRect.X + 10;
                var bodyY = item.BodyRect.Y + 10;
                var opacityX = item.DecreaseOpacityRect.X + item.DecreaseOpacityRect.Width / 2;
                var opacityY = item.DecreaseOpacityRect.Y + item.DecreaseOpacityRect.Height / 2;

                Terraria.Main.mouseLeft = true;
                Terraria.Main.mouseLeftRelease = true;
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdatePrefixGuard",
                    true,
                    true,
                    BuildPinnedOverlayRawMouseWithTerrariaAndOs(bodyX, bodyY, opacityX, opacityY, true));
                var prefixInteraction = UserNotesPinnedOverlayState.LastInteraction;
                if (prefixInteraction.OpacityChanged || prefixInteraction.Unpinned || prefixInteraction.DragStarted)
                {
                    throw new InvalidOperationException("Expected stale prefix coordinates to arm but not execute a toolbar command.");
                }

                var opacityBefore = cache.Snapshot.Notes[0].PinnedState.OpacityPercent;
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPoint(opacityX, opacityY, true));

                var interaction = UserNotesPinnedOverlayState.LastInteraction;
                var opacityAfter = cache.Snapshot.Notes[0].PinnedState.OpacityPercent;
                if (!interaction.OpacityChanged || opacityBefore != 100 || opacityAfter != 95)
                {
                    throw new InvalidOperationException(
                        "Expected PlayerInput toolbar hit to receive the prefix press edge once. Interaction=" +
                        interaction.ToVerificationJson() +
                        ", opacityBefore=" + opacityBefore +
                        ", opacityAfter=" + opacityAfter);
                }

                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPoint(opacityX, opacityY, true));
                if (cache.Snapshot.Notes[0].PinnedState.OpacityPercent != 95)
                {
                    throw new InvalidOperationException("Expected transferred toolbar press to be single-use while the physical button remains held.");
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

        private static void UserNotesPinnedOverlayTransfersPrefixPressWhenTerrariaCoordinatesMissNote()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-toolbar-press-miss-transfer");
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
                var opacityX = item.DecreaseOpacityRect.X + item.DecreaseOpacityRect.Width / 2;
                var opacityY = item.DecreaseOpacityRect.Y + item.DecreaseOpacityRect.Height / 2;
                var staleTerrariaX = item.Rect.X + item.Rect.Width / 2;
                var staleTerrariaY = item.Rect.Bottom + 24;
                var staleHit = UserNotesPinnedOverlayState.BuildHitDiagnostics(frame, staleTerrariaX, staleTerrariaY);
                if (!string.Equals(staleHit.ControlId, "none", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stale Terraria coordinates to miss the pinned note, got " + staleHit.ControlId + ".");
                }

                Terraria.Main.mouseLeft = true;
                Terraria.Main.mouseLeftRelease = true;
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdatePrefixGuard",
                    true,
                    true,
                    BuildPinnedOverlayRawMouseWithTerrariaAndOs(staleTerrariaX, staleTerrariaY, opacityX, opacityY, true));
                var prefixInteraction = UserNotesPinnedOverlayState.LastInteraction;
                if (prefixInteraction.OpacityChanged || prefixInteraction.Unpinned || prefixInteraction.DragStarted)
                {
                    throw new InvalidOperationException("Expected stale prefix coordinates outside the note to arm but not execute a toolbar command.");
                }

                var opacityBefore = cache.Snapshot.Notes[0].PinnedState.OpacityPercent;
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPoint(opacityX, opacityY, true));

                var interaction = UserNotesPinnedOverlayState.LastInteraction;
                var opacityAfter = cache.Snapshot.Notes[0].PinnedState.OpacityPercent;
                if (!interaction.OpacityChanged || opacityBefore != 100 || opacityAfter != 95)
                {
                    throw new InvalidOperationException(
                        "Expected PlayerInput toolbar hit to recover the prefix press edge even when Terraria prefix coordinates missed the note. Interaction=" +
                        interaction.ToVerificationJson() +
                        ", opacityBefore=" + opacityBefore +
                        ", opacityAfter=" + opacityAfter);
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

        private static void UserNotesPinnedOverlayTransfersPrefixPressToPlayerInputDragAndKeepsHeldLeft()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-drag-press-transfer");
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
                var bodyX = item.BodyRect.X + 10;
                var bodyY = item.BodyRect.Y + 10;
                var dragX = item.DragHandleRect.X + item.DragHandleRect.Width / 2;
                var dragY = item.DragHandleRect.Y + item.DragHandleRect.Height / 2;
                var prefixFrame = UserNotesPinnedOverlay.BuildFrameForTesting(cache.Snapshot, 1280, 800, bodyX, bodyY);
                var osHit = UserNotesPinnedOverlayState.BuildHitDiagnostics(prefixFrame, dragX, dragY);
                if (!string.Equals(osHit.ControlId, "drag", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected raw OS point to identify the pending drag control, got " + osHit.ControlId + ".");
                }

                Terraria.Main.mouseLeft = true;
                Terraria.Main.mouseLeftRelease = true;
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdatePrefixGuard",
                    true,
                    true,
                    BuildPinnedOverlayRawMouseWithTerrariaAndOs(bodyX, bodyY, dragX, dragY, true));
                var prefixInteraction = UserNotesPinnedOverlayState.LastInteraction;
                if (prefixInteraction.DragStarted || prefixInteraction.OpacityChanged || prefixInteraction.Unpinned)
                {
                    throw new InvalidOperationException("Expected stale prefix coordinates to arm drag transfer without executing immediately.");
                }

                if (!Terraria.Main.mouseLeft)
                {
                    throw new InvalidOperationException("Pending drag transfer must preserve the held left button for the postfix drag state.");
                }

                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPoint(dragX, dragY, true));
                var dragStart = UserNotesPinnedOverlayState.LastInteraction;
                if (!dragStart.DragStarted || !dragStart.Dragging || !Terraria.Main.mouseLeft)
                {
                    throw new InvalidOperationException("Expected PlayerInput toolbar hit to start drag while preserving held left.");
                }

                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPoint(dragX + 60, dragY + 36, true));
                var dragging = UserNotesPinnedOverlayState.LastInteraction;
                if (!dragging.Dragging || dragging.PendingState == null || dragging.PendingState.X <= 100)
                {
                    throw new InvalidOperationException("Expected transferred drag to keep moving while left remains held.");
                }

                Terraria.Main.mouseLeft = false;
                Terraria.Main.mouseLeftRelease = false;
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPoint(dragX + 60, dragY + 36, false));
                var released = UserNotesPinnedOverlayState.LastInteraction;
                if (!released.DragSaved || cache.Snapshot.Notes[0].PinnedState.X <= 100)
                {
                    throw new InvalidOperationException("Expected transferred drag release to save the moved pinned state.");
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

        private static void UserNotesPinnedOverlayOpacityDefaultsAndClampsWithoutWrap()
        {
            UserNotesPinnedOverlay.ResetForTesting();
            if (new UserNotePinnedState().OpacityPercent != 0)
            {
                throw new InvalidOperationException("Expected new pinned note state to default to 0 percent stored opacity.");
            }

            if (UserNotesPinnedOverlay.ScaleAlphaForTesting(200, 50) != 100 ||
                UserNotesPinnedOverlay.ScaleAlphaForTesting(168, 0) != 0 ||
                UserNotesPinnedOverlay.ScaleAlphaForTesting(168, 100) != 168 ||
                UserNotesPinnedOverlay.PremultiplyForAlphaBlendForTesting(18, 0) != 0 ||
                UserNotesPinnedOverlay.PremultiplyForAlphaBlendForTesting(18, 109) != 8 ||
                UserNotesPinnedOverlay.PremultiplyForAlphaBlendForTesting(96, 84) != 32 ||
                UserNotesPinnedOverlay.ForegroundAlphaForTesting(246) != 246 ||
                UserNotesPinnedOverlay.ForegroundAlphaForTesting(255) != 255)
            {
                throw new InvalidOperationException("Expected pinned overlay opacity surfaces to premultiply RGB for AlphaBlend while foreground text remains opaque.");
            }

            var negativeSnapshot = BuildPinnedNotesSnapshot(
                new UserNoteSnapshot
                {
                    NoteId = "note-negative",
                    Body = "negative",
                    PinnedState = new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 100,
                        Y = 80,
                        Width = 280,
                        Height = 150,
                        OpacityPercent = -5
                    }
                });
            var negativeFrame = UserNotesPinnedOverlay.BuildFrameForTesting(negativeSnapshot, 800, 480, 0, 0);
            if (negativeFrame.Items.Count != 1 || negativeFrame.Items[0].OpacityPercent != 0)
            {
                throw new InvalidOperationException("Expected negative stored opacity to clamp to 0 instead of wrapping to 100.");
            }

            var highSnapshot = BuildPinnedNotesSnapshot(
                new UserNoteSnapshot
                {
                    NoteId = "note-high",
                    Body = "high",
                    PinnedState = new UserNotePinnedState
                    {
                        Pinned = true,
                        X = 100,
                        Y = 80,
                        Width = 280,
                        Height = 150,
                        OpacityPercent = 120
                    }
                });
            var highFrame = UserNotesPinnedOverlay.BuildFrameForTesting(highSnapshot, 800, 480, 0, 0);
            if (highFrame.Items.Count != 1 || highFrame.Items[0].OpacityPercent != 100)
            {
                throw new InvalidOperationException("Expected opacity above 100 to clamp to 100.");
            }

            var lowFrame = UserNotesPinnedOverlay.BuildFrameForTesting(negativeSnapshot, 800, 480, 0, 0);
            var lowItem = lowFrame.Items[0];
            var persistedCount = 0;
            var decreaseAtMinimum = UserNotesPinnedOverlay.HandleInputForTesting(
                lowFrame,
                lowItem.DecreaseOpacityRect.X + 2,
                lowItem.DecreaseOpacityRect.Y + 2,
                true,
                true,
                false,
                0,
                800,
                480,
                (noteId, state) =>
                {
                    persistedCount++;
                    return UserNotesOperationResult.Success("saved", "saved");
                });
            if (decreaseAtMinimum.OpacityChanged || !decreaseAtMinimum.CapturedMouse || persistedCount != 0)
            {
                throw new InvalidOperationException("Expected opacity decrease at 0 percent to capture without wrapping or saving.");
            }

            var highItem = highFrame.Items[0];
            var increaseAtMaximum = UserNotesPinnedOverlay.HandleInputForTesting(
                highFrame,
                highItem.IncreaseOpacityRect.X + 2,
                highItem.IncreaseOpacityRect.Y + 2,
                true,
                true,
                false,
                0,
                800,
                480,
                (noteId, state) =>
                {
                    persistedCount++;
                    return UserNotesOperationResult.Success("saved", "saved");
                });
            if (increaseAtMaximum.OpacityChanged || !increaseAtMaximum.CapturedMouse || persistedCount != 0)
            {
                throw new InvalidOperationException("Expected opacity increase at 100 percent to capture without wrapping or saving.");
            }
        }

        private static void UserNotesPinnedOverlayDragCaptureBlocksNonLeftMouseAndKeepsHeldLeft()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-drag-non-left-block");
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousGameMenu = Terraria.Main.gameMenu;
            var previousMapFullscreen = Terraria.Main.mapFullscreen;
            try
            {
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
                DisableUserNotesDiagnosticsForTesting();
                UserNotesUiState.ResetForTesting();
                LegacyMainUiState.SetVisible(false);
                Terraria.Main.gameMenu = false;
                Terraria.Main.mapFullscreen = false;
                UserNotesPinnedOverlay.ResetForTesting();
                UserNotesPinnedOverlay.SetShouldUseOverlayOverrideForTesting(true);
                var player = new Terraria.Player();
                Terraria.Main.LocalPlayer = player;
                Terraria.Main.player[0] = player;
                Terraria.Main.myPlayer = 0;

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
                var dragX = item.DragHandleRect.X + item.DragHandleRect.Width / 2;
                var dragY = item.DragHandleRect.Y + item.DragHandleRect.Height / 2;

                SeedPinnedOverlayMouseButtonsForPointThroughTest();
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPoint(dragX, dragY, true));

                var dragStart = UserNotesPinnedOverlayState.LastInteraction;
                if (!dragStart.DragStarted || !dragStart.Dragging)
                {
                    throw new InvalidOperationException("Expected drag handle press to start a captured drag.");
                }

                AssertPinnedOverlayDragInputIsolationPreservedLeftAndBlockedNonLeft("drag start");

                SeedPinnedOverlayMouseButtonsForPointThroughTest();
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPoint(dragX + 40, dragY + 22, true));

                var dragging = UserNotesPinnedOverlayState.LastInteraction;
                if (!dragging.Dragging || dragging.PendingState == null || dragging.PendingState.X <= 100)
                {
                    throw new InvalidOperationException("Expected active drag to keep moving while preserving held left.");
                }

                AssertPinnedOverlayDragInputIsolationPreservedLeftAndBlockedNonLeft("drag move");
            }
            finally
            {
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.mapFullscreen = previousMapFullscreen;
                Terraria.Main.LocalPlayer = null;
                Terraria.Main.player[0] = null;
                UserNotesUiState.ResetForTesting();
                UserNotesPinnedOverlay.ResetForTesting();
                ResetUserNotesTestingHooks();
                restoreRuntimeTypes();
                restore();
            }
        }

        private static void UserNotesPinnedOverlayPostPlayerInputWheelScrollsBody()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-post-playerinput-wheel");
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

                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create pinned note");
                RequireSuccess(cache.SaveNote(note.NoteId, "title", BuildRepeatedText("post wheel", 90), out note), "seed long body");
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
                if (item.BodyMaxScroll <= 0)
                {
                    throw new InvalidOperationException("Expected long pinned body to have a positive scroll range.");
                }

                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPointWithScroll(item.BodyRect.X + 4, item.BodyRect.Y + 4, -120));

                var interaction = UserNotesPinnedOverlayState.LastInteraction;
                var scrolledFrame = UserNotesPinnedOverlay.BuildFrameForTesting(cache.Snapshot, 1280, 800, item.BodyRect.X + 4, item.BodyRect.Y + 4);
                if (!interaction.ScrollConsumed || scrolledFrame.Items[0].BodyScrollOffset <= 0)
                {
                    throw new InvalidOperationException(
                        "Expected PlayerInput postfix wheel over pinned body to scroll the body before suppressing hotbar. Interaction=" +
                        interaction.ToVerificationJson() +
                        ", offset=" + scrolledFrame.Items[0].BodyScrollOffset);
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.mapFullscreen = previousMapFullscreen;
                UserNotesUiState.ResetForTesting();
                UserNotesPinnedOverlay.ResetForTesting();
                ResetUserNotesTestingHooks();
                restoreRuntimeTypes();
                restore();
            }
        }

        private static void UserNotesPinnedOverlayVisualSurfaceWheelBlocksHotbarWithoutFakeWheel()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-surface-wheel-block");
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousGameMenu = Terraria.Main.gameMenu;
            var previousMapFullscreen = Terraria.Main.mapFullscreen;
            try
            {
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
                DisableUserNotesDiagnosticsForTesting();
                UserNotesUiState.ResetForTesting();
                LegacyMainUiState.SetVisible(false);
                Terraria.Main.gameMenu = false;
                Terraria.Main.mapFullscreen = false;
                UserNotesPinnedOverlay.ResetForTesting();
                UserNotesPinnedOverlay.SetShouldUseOverlayOverrideForTesting(true);
                var player = new Terraria.Player();
                Terraria.Main.LocalPlayer = player;
                Terraria.Main.player[0] = player;
                Terraria.Main.myPlayer = 0;

                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create pinned note");
                RequireSuccess(cache.SaveNote(note.NoteId, "title", "short body", out note), "seed short body");
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
                var toolbarX = item.DecreaseOpacityRect.X + item.DecreaseOpacityRect.Width / 2;
                var toolbarY = item.DecreaseOpacityRect.Y + item.DecreaseOpacityRect.Height / 2;
                if (item.BodyRect.Contains(toolbarX, toolbarY))
                {
                    throw new InvalidOperationException("Expected toolbar wheel sample to be outside the body rect.");
                }

                Terraria.Main.mouseX = toolbarX;
                Terraria.Main.mouseY = toolbarY;
                Terraria.Main.mouseLeft = false;
                Terraria.Main.mouseLeftRelease = false;
                Terraria.Main.mouseScrollWheel = 240;
                Terraria.Main.oldMouseScrollWheel = 0;
                UiInputFrameClock.BeginInputFrame("user-notes-overlay-toolbar-wheel-test");

                var suppressed = UserNotesPinnedOverlay.ShouldSuppressHotbarScrollFromHook();
                var interaction = UserNotesPinnedOverlayState.LastInteraction;
                if (!suppressed ||
                    !TerrariaUiMouseCompat.LastScrollHotbarHookSuppressed ||
                    Terraria.Main.mouseScrollWheel != Terraria.Main.oldMouseScrollWheel ||
                    !Terraria.Main.mouseInterface ||
                    !Terraria.Main.blockMouse ||
                    !player.mouseInterface)
                {
                    throw new InvalidOperationException("Expected wheel over the pinned toolbar surface to be captured and suppressed before reaching the hotbar.");
                }

                if (interaction.ScrollConsumed || interaction.BodyScrollAfter != interaction.BodyScrollBefore)
                {
                    throw new InvalidOperationException("Expected toolbar wheel isolation to avoid recording a fake body wheel success.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.mapFullscreen = previousMapFullscreen;
                Terraria.Main.LocalPlayer = null;
                Terraria.Main.player[0] = null;
                UserNotesUiState.ResetForTesting();
                UserNotesPinnedOverlay.ResetForTesting();
                ResetUserNotesTestingHooks();
                restoreRuntimeTypes();
                restore();
            }
        }

        private static void UserNotesPinnedOverlayRepeatedToolbarClicksKeepEdgesAndWheel()
        {
            var restore = PushTemporaryConfigDirectory("user-notes-overlay-repeated-toolbar");
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

                var cache = new UserNotesCache(new UserNotesStore());
                UserNoteSnapshot note;
                RequireSuccess(cache.CreateDefaultNote(out note), "create pinned note");
                RequireSuccess(cache.SaveNote(note.NoteId, "title", BuildRepeatedText("repeat toolbar wheel", 80), out note), "seed long note");
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
                var opacityX = item.DecreaseOpacityRect.X + item.DecreaseOpacityRect.Width / 2;
                var opacityY = item.DecreaseOpacityRect.Y + item.DecreaseOpacityRect.Height / 2;

                PressAndReleasePinnedOverlayPoint(opacityX, opacityY);
                var opacityAfterFirstClick = cache.Snapshot.Notes[0].PinnedState.OpacityPercent;
                if (opacityAfterFirstClick != 95)
                {
                    throw new InvalidOperationException("Expected first toolbar opacity click to remain effective; opacity=" + opacityAfterFirstClick + ".");
                }

                frame = UserNotesPinnedOverlay.BuildFrameForTesting(cache.Snapshot, 1280, 800, opacityX, opacityY);
                item = frame.Items[0];
                opacityX = item.DecreaseOpacityRect.X + item.DecreaseOpacityRect.Width / 2;
                opacityY = item.DecreaseOpacityRect.Y + item.DecreaseOpacityRect.Height / 2;
                PressAndReleasePinnedOverlayPoint(opacityX, opacityY);
                var opacityAfterSecondClick = cache.Snapshot.Notes[0].PinnedState.OpacityPercent;
                if (opacityAfterSecondClick != 90)
                {
                    throw new InvalidOperationException("Expected repeated toolbar opacity click to get a fresh left edge; opacity=" + opacityAfterSecondClick + ".");
                }

                frame = UserNotesPinnedOverlay.BuildFrameForTesting(cache.Snapshot, 1280, 800, opacityX, opacityY);
                item = frame.Items[0];
                UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                    "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                    true,
                    false,
                    BuildPinnedOverlayRawMouseAtUiPointWithScroll(item.BodyRect.X + 4, item.BodyRect.Y + 4, -120));

                var interaction = UserNotesPinnedOverlayState.LastInteraction;
                var scrolledFrame = UserNotesPinnedOverlay.BuildFrameForTesting(cache.Snapshot, 1280, 800, item.BodyRect.X + 4, item.BodyRect.Y + 4);
                if (!interaction.ScrollConsumed || scrolledFrame.Items[0].BodyScrollOffset <= 0)
                {
                    throw new InvalidOperationException(
                        "Expected pinned body wheel to remain effective after repeated toolbar operations. Interaction=" +
                        interaction.ToVerificationJson() +
                        ", offset=" + scrolledFrame.Items[0].BodyScrollOffset);
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

        private static DiagnosticMouseState BuildPinnedOverlayRawMouseAtUiPointWithScroll(int uiX, int uiY, int scrollDelta)
        {
            var state = BuildPinnedOverlayRawMouseAtUiPoint(uiX, uiY, false);
            state.TerrariaScrollWheelAvailable = true;
            state.ScrollDelta = scrollDelta;
            return state;
        }

        private static void PressAndReleasePinnedOverlayPoint(int uiX, int uiY)
        {
            Terraria.Main.mouseLeft = true;
            Terraria.Main.mouseLeftRelease = true;
            UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                "UserNotesPinnedOverlay.UpdatePrefixGuard",
                true,
                true,
                BuildPinnedOverlayRawMouseAtUiPoint(uiX, uiY, true));
            UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                true,
                false,
                BuildPinnedOverlayRawMouseAtUiPoint(uiX, uiY, true));

            Terraria.Main.mouseLeft = false;
            Terraria.Main.mouseLeftRelease = false;
            UserNotesPinnedOverlay.UpdateInputGuardForTesting(
                "UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard",
                true,
                false,
                BuildPinnedOverlayRawMouseAtUiPoint(uiX, uiY, false));
        }

        private static void SeedPinnedOverlayMouseButtonsForPointThroughTest()
        {
            Terraria.Main.mouseLeft = true;
            Terraria.Main.mouseLeftRelease = true;
            Terraria.Main.mouseRight = true;
            Terraria.Main.mouseRightRelease = true;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = true;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseMiddle = true;
            Terraria.GameInput.PlayerInput.Triggers.Current.Mouse4 = true;
            Terraria.GameInput.PlayerInput.Triggers.Current.Mouse5 = true;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = true;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseRight = true;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseMiddle = true;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.Mouse4 = true;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.Mouse5 = true;
        }

        private static void AssertPinnedOverlayDragInputIsolationPreservedLeftAndBlockedNonLeft(string phase)
        {
            if (!Terraria.Main.mouseLeft ||
                !Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft ||
                !Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft)
            {
                throw new InvalidOperationException("Expected pinned overlay " + phase + " capture to preserve held left for dragging.");
            }

            if (Terraria.Main.mouseRight ||
                Terraria.Main.mouseRightRelease ||
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight ||
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseMiddle ||
                Terraria.GameInput.PlayerInput.Triggers.Current.Mouse4 ||
                Terraria.GameInput.PlayerInput.Triggers.Current.Mouse5 ||
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseRight ||
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseMiddle ||
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.Mouse4 ||
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.Mouse5)
            {
                throw new InvalidOperationException("Expected pinned overlay " + phase + " capture to clear right, middle, and side mouse inputs inside the visual surface.");
            }
        }

        private static DiagnosticMouseState BuildPinnedOverlayRawMouseWithTerrariaAndOs(int terrariaX, int terrariaY, int osX, int osY, bool leftDown)
        {
            return new DiagnosticMouseState
            {
                GameInputAvailable = true,
                TerrariaReadAvailable = true,
                TerrariaMouseX = terrariaX,
                TerrariaMouseY = terrariaY,
                TerrariaLeftDown = leftDown,
                OsReadAvailable = true,
                OsClientMouseX = osX,
                OsClientMouseY = osY,
                OsLeftDown = leftDown,
                ReadMode = "Terraria+OsClient"
            };
        }

        private static LegacyMouseSnapshot BuildScaledPinnedOverlayMouseAtUiPoint(int uiX, int uiY, bool leftDown)
        {
            const double scale = 1.5d;
            var staleTerrariaX = (int)Math.Round(uiX * scale);
            var staleTerrariaY = (int)Math.Round(uiY * scale);
            return UserNotesPinnedOverlay.ReadOverlayMouseForTesting(new DiagnosticMouseState
            {
                GameInputAvailable = true,
                TerrariaReadAvailable = true,
                TerrariaMouseX = staleTerrariaX,
                TerrariaMouseY = staleTerrariaY,
                TerrariaLeftDown = leftDown,
                OsReadAvailable = true,
                OsClientMouseX = uiX,
                OsClientMouseY = uiY,
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

        private static DiagnosticMouseState BuildScaledPinnedOverlayRawMouseWithTerrariaAndOs(int terrariaX, int terrariaY, int uiX, int uiY, bool leftDown, double scale)
        {
            var raw = BuildPinnedOverlayRawMouseWithTerrariaAndOs(
                terrariaX,
                terrariaY,
                uiX,
                uiY,
                leftDown);
            raw.UiScaleAvailable = true;
            raw.UiScaleMatrixAvailable = true;
            raw.UiScale = scale;
            raw.UiScaleX = scale;
            raw.UiScaleY = scale;
            raw.UiScaleSource = "test";
            return raw;
        }
    }
}
