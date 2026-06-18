using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Information.Notes;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI
{
    internal static class UserNotesPinnedOverlayState
    {
        internal const int DefaultWidth = 280;
        internal const int DefaultHeight = 180;
        internal const int MinWidth = 220;
        internal const int MinHeight = 120;
        internal const int MaxWidth = 360;
        internal const int MaxHeight = 300;
        internal const int HeaderHeight = 24;
        internal const int BodyPadding = 8;
        internal const int LineHeight = 18;
        internal const int ScreenPadding = 12;
        internal const int AvoidanceStep = 28;
        internal const int OpacityStep = 5;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, int> BodyScrollOffsets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static UserNotesPinnedOverlayDragState _drag;
        private static UserNotesPinnedOverlayInteraction _lastInteraction = UserNotesPinnedOverlayInteraction.None;

        public static UserNotesPinnedOverlayFrame BuildFrame(UserNotesSnapshot snapshot, int screenWidth, int screenHeight)
        {
            return BuildFrame(snapshot, screenWidth, screenHeight, -1, -1);
        }

        public static UserNotesPinnedOverlayFrame BuildFrame(UserNotesSnapshot snapshot, int screenWidth, int screenHeight, int mouseX, int mouseY)
        {
            var notes = snapshot == null ? null : snapshot.Notes;
            var items = new List<UserNotesPinnedOverlayItem>();
            var safeScreenWidth = Math.Max(1, screenWidth);
            var safeScreenHeight = Math.Max(1, screenHeight);
            if (notes == null || notes.Count <= 0)
            {
                return new UserNotesPinnedOverlayFrame(items, string.Empty, false);
            }

            var occupied = new List<LegacyUiRect>();
            var drag = GetDragState();
            var draggingNoteId = drag == null ? string.Empty : drag.NoteId ?? string.Empty;
            for (var index = 0; index < notes.Count; index++)
            {
                var note = notes[index];
                if (note == null || note.PinnedState == null || !note.PinnedState.Pinned)
                {
                    continue;
                }

                var state = note.PinnedState;
                var width = Clamp((int)Math.Round(state.Width <= 0f ? DefaultWidth : state.Width), MinWidth, Math.Min(MaxWidth, Math.Max(MinWidth, safeScreenWidth - ScreenPadding * 2)));
                var height = Clamp((int)Math.Round(state.Height <= 0f ? DefaultHeight : state.Height), MinHeight, Math.Min(MaxHeight, Math.Max(MinHeight, safeScreenHeight - ScreenPadding * 2)));
                var desired = new LegacyUiRect(
                    (int)Math.Round(state.X),
                    (int)Math.Round(state.Y),
                    width,
                    height);
                if (drag != null && string.Equals(drag.NoteId, note.NoteId, StringComparison.OrdinalIgnoreCase) && mouseX >= 0 && mouseY >= 0)
                {
                    desired = new LegacyUiRect(mouseX - drag.OffsetX, mouseY - drag.OffsetY, width, height);
                }
                var rect = ConstrainAndAvoid(desired, safeScreenWidth, safeScreenHeight, occupied, string.Equals(draggingNoteId, note.NoteId, StringComparison.OrdinalIgnoreCase));
                occupied.Add(rect);

                var bodyRect = new LegacyUiRect(
                    rect.X + BodyPadding,
                    rect.Y + HeaderHeight + BodyPadding,
                    Math.Max(1, rect.Width - BodyPadding * 2),
                    Math.Max(1, rect.Height - HeaderHeight - BodyPadding * 2));
                var lines = BuildBodyLines(note.Body, Math.Max(1, bodyRect.Width - BodyPadding));
                var contentHeight = Math.Max(bodyRect.Height, Math.Max(1, lines.Length) * LineHeight + BodyPadding);
                var maxScroll = Math.Max(0, contentHeight - bodyRect.Height);
                var offset = Clamp(GetScrollOffset(note.NoteId), 0, maxScroll);
                SetScrollOffset(note.NoteId, offset);
                var hovered = rect.Contains(mouseX, mouseY);
                items.Add(new UserNotesPinnedOverlayItem
                {
                    NoteId = note.NoteId ?? string.Empty,
                    Body = note.Body ?? string.Empty,
                    Rect = rect,
                    HeaderRect = new LegacyUiRect(rect.X, rect.Y, rect.Width, HeaderHeight),
                    DragHandleRect = new LegacyUiRect(rect.X + 8, rect.Y + 7, 38, 10),
                    DecreaseOpacityRect = new LegacyUiRect(rect.Right - 70, rect.Y + 4, 18, 16),
                    IncreaseOpacityRect = new LegacyUiRect(rect.Right - 47, rect.Y + 4, 18, 16),
                    CloseRect = new LegacyUiRect(rect.Right - 24, rect.Y + 4, 18, 16),
                    BodyRect = bodyRect,
                    BodyLines = lines,
                    BodyContentHeight = contentHeight,
                    BodyScrollOffset = offset,
                    BodyMaxScroll = maxScroll,
                    OpacityPercent = ClampOpacity(state.OpacityPercent),
                    Hovered = hovered
                });
            }

            return new UserNotesPinnedOverlayFrame(items, draggingNoteId, !string.IsNullOrWhiteSpace(draggingNoteId));
        }

        public static UserNotePinnedState BuildInitialPinnedState(UserNotesSnapshot snapshot, string noteId, LegacyUiRect anchor, int screenWidth, int screenHeight)
        {
            var safeScreenWidth = Math.Max(1, screenWidth);
            var safeScreenHeight = Math.Max(1, screenHeight);
            var width = Clamp(DefaultWidth, MinWidth, Math.Min(MaxWidth, Math.Max(MinWidth, safeScreenWidth - ScreenPadding * 2)));
            var height = Clamp(DefaultHeight, MinHeight, Math.Min(MaxHeight, Math.Max(MinHeight, safeScreenHeight - ScreenPadding * 2)));
            var x = anchor.Width > 0 ? anchor.Right + 12 : ScreenPadding;
            var y = anchor.Height > 0 ? anchor.Y : ScreenPadding;
            if (x + width + ScreenPadding > safeScreenWidth)
            {
                x = anchor.Width > 0 ? anchor.X - width - 12 : ScreenPadding;
            }

            var occupied = BuildOccupiedRects(snapshot, noteId, safeScreenWidth, safeScreenHeight);
            var rect = ConstrainAndAvoid(new LegacyUiRect(x, y, width, height), safeScreenWidth, safeScreenHeight, occupied, false);
            return new UserNotePinnedState
            {
                Pinned = true,
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                OpacityPercent = 100
            };
        }

        public static UserNotesPinnedOverlayInteraction HandleInput(
            UserNotesPinnedOverlayFrame frame,
            int mouseX,
            int mouseY,
            bool leftDown,
            bool leftPressed,
            bool leftReleased,
            int rawScrollDelta,
            int screenWidth,
            int screenHeight,
            Func<string, UserNotePinnedState, UserNotesOperationResult> persist)
        {
            frame = frame ?? new UserNotesPinnedOverlayFrame(new List<UserNotesPinnedOverlayItem>(), string.Empty, false);
            persist = persist ?? ((noteId, state) => UserNotesOperationResult.Failure("persistUnavailable", "persist unavailable"));
            var interaction = UserNotesPinnedOverlayInteraction.None;
            var hit = HitTest(frame, mouseX, mouseY);
            interaction.HitNoteId = hit == null ? string.Empty : hit.NoteId;
            interaction.MouseInside = hit != null;

            UserNotesPinnedOverlayDragState drag;
            lock (SyncRoot)
            {
                drag = _drag == null ? null : _drag.Clone();
            }

            if (drag != null)
            {
                interaction.MouseInside = true;
                interaction.HitNoteId = drag.NoteId;
                interaction.CapturedMouse = true;
                if (leftDown && !leftReleased)
                {
                    var dragged = FindItem(frame, drag.NoteId);
                    var width = dragged == null ? DefaultWidth : dragged.Rect.Width;
                    var height = dragged == null ? DefaultHeight : dragged.Rect.Height;
                    var rect = ClampToScreen(new LegacyUiRect(mouseX - drag.OffsetX, mouseY - drag.OffsetY, width, height), screenWidth, screenHeight);
                    interaction.Dragging = true;
                    interaction.PendingState = new UserNotePinnedState
                    {
                        Pinned = true,
                        X = rect.X,
                        Y = rect.Y,
                        Width = rect.Width,
                        Height = rect.Height,
                        OpacityPercent = dragged == null ? 100 : dragged.OpacityPercent
                    };
                }
                else
                {
                    ClearDrag();
                    var dragged = FindItem(frame, drag.NoteId);
                    if (dragged != null)
                    {
                        var rect = ClampToScreen(new LegacyUiRect(mouseX - drag.OffsetX, mouseY - drag.OffsetY, dragged.Rect.Width, dragged.Rect.Height), screenWidth, screenHeight);
                        var state = new UserNotePinnedState
                        {
                            Pinned = true,
                            X = rect.X,
                            Y = rect.Y,
                            Width = rect.Width,
                            Height = rect.Height,
                            OpacityPercent = dragged.OpacityPercent
                        };
                        interaction.PersistResult = persist(drag.NoteId, state);
                        interaction.DragSaved = interaction.PersistResult != null && interaction.PersistResult.Succeeded;
                        interaction.PendingState = state;
                    }
                }

                SetLastInteraction(interaction);
                return interaction;
            }

            if (hit == null)
            {
                SetLastInteraction(interaction);
                return interaction;
            }

            interaction.CapturedMouse = leftDown || leftPressed || rawScrollDelta != 0;
            if (rawScrollDelta != 0 && hit.BodyRect.Contains(mouseX, mouseY))
            {
                var before = hit.BodyScrollOffset;
                var next = Clamp(before + WheelDeltaToScrollOffset(rawScrollDelta), 0, hit.BodyMaxScroll);
                if (next != before)
                {
                    SetScrollOffset(hit.NoteId, next);
                    interaction.ScrollConsumed = true;
                    interaction.BodyScrollBefore = before;
                    interaction.BodyScrollAfter = next;
                }
                else if (hit.BodyMaxScroll > 0)
                {
                    interaction.ScrollConsumed = true;
                    interaction.BodyScrollBefore = before;
                    interaction.BodyScrollAfter = before;
                }
            }

            if (leftPressed && hit.CloseRect.Contains(mouseX, mouseY))
            {
                var state = BuildStateFromItem(hit);
                state.Pinned = false;
                interaction.PersistResult = persist(hit.NoteId, state);
                interaction.Unpinned = interaction.PersistResult != null && interaction.PersistResult.Succeeded;
                interaction.CapturedMouse = true;
                SetLastInteraction(interaction);
                return interaction;
            }

            if (leftPressed && (hit.DecreaseOpacityRect.Contains(mouseX, mouseY) || hit.IncreaseOpacityRect.Contains(mouseX, mouseY)))
            {
                var state = BuildStateFromItem(hit);
                var delta = hit.IncreaseOpacityRect.Contains(mouseX, mouseY) ? OpacityStep : -OpacityStep;
                state.OpacityPercent = ClampOpacity(hit.OpacityPercent + delta);
                interaction.PersistResult = persist(hit.NoteId, state);
                interaction.OpacityChanged = interaction.PersistResult != null && interaction.PersistResult.Succeeded;
                interaction.CapturedMouse = true;
                SetLastInteraction(interaction);
                return interaction;
            }

            if (leftPressed && hit.DragHandleRect.Contains(mouseX, mouseY))
            {
                lock (SyncRoot)
                {
                    _drag = new UserNotesPinnedOverlayDragState
                    {
                        NoteId = hit.NoteId,
                        OffsetX = mouseX - hit.Rect.X,
                        OffsetY = mouseY - hit.Rect.Y
                    };
                }

                interaction.DragStarted = true;
                interaction.Dragging = true;
                interaction.CapturedMouse = true;
                SetLastInteraction(interaction);
                return interaction;
            }

            SetLastInteraction(interaction);
            return interaction;
        }

        public static bool ShouldCaptureMouse(UserNotesPinnedOverlayFrame frame, int mouseX, int mouseY)
        {
            frame = frame ?? new UserNotesPinnedOverlayFrame(new List<UserNotesPinnedOverlayItem>(), string.Empty, false);
            return frame.Dragging || HitTest(frame, mouseX, mouseY) != null;
        }

        public static bool ShouldSuppressHotbarScroll(UserNotesPinnedOverlayFrame frame, int mouseX, int mouseY)
        {
            var hit = HitTest(frame, mouseX, mouseY);
            return hit != null && hit.BodyRect.Contains(mouseX, mouseY);
        }

        public static UserNotesPinnedOverlayInteraction LastInteraction
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastInteraction.Clone();
                }
            }
        }

        public static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                BodyScrollOffsets.Clear();
                _drag = null;
                _lastInteraction = UserNotesPinnedOverlayInteraction.None;
            }
        }

        private static List<LegacyUiRect> BuildOccupiedRects(UserNotesSnapshot snapshot, string excludeNoteId, int screenWidth, int screenHeight)
        {
            var result = new List<LegacyUiRect>();
            var notes = snapshot == null ? null : snapshot.Notes;
            if (notes == null)
            {
                return result;
            }

            for (var index = 0; index < notes.Count; index++)
            {
                var note = notes[index];
                if (note == null || note.PinnedState == null || !note.PinnedState.Pinned ||
                    string.Equals(note.NoteId, excludeNoteId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var state = note.PinnedState;
                result.Add(ClampToScreen(new LegacyUiRect(
                    (int)Math.Round(state.X),
                    (int)Math.Round(state.Y),
                    Clamp((int)Math.Round(state.Width <= 0f ? DefaultWidth : state.Width), MinWidth, MaxWidth),
                    Clamp((int)Math.Round(state.Height <= 0f ? DefaultHeight : state.Height), MinHeight, MaxHeight)),
                    screenWidth,
                    screenHeight));
            }

            return result;
        }

        private static LegacyUiRect ConstrainAndAvoid(LegacyUiRect desired, int screenWidth, int screenHeight, List<LegacyUiRect> occupied, bool keepCurrent)
        {
            var rect = ClampToScreen(desired, screenWidth, screenHeight);
            if (keepCurrent || occupied == null || occupied.Count <= 0)
            {
                return rect;
            }

            for (var attempt = 0; attempt < 32; attempt++)
            {
                if (!IntersectsAny(rect, occupied))
                {
                    return rect;
                }

                rect = ClampToScreen(new LegacyUiRect(rect.X + AvoidanceStep, rect.Y + AvoidanceStep, rect.Width, rect.Height), screenWidth, screenHeight);
                if (rect.Right >= screenWidth - ScreenPadding && rect.Bottom >= screenHeight - ScreenPadding)
                {
                    rect = ClampToScreen(new LegacyUiRect(ScreenPadding, ScreenPadding + attempt * AvoidanceStep, rect.Width, rect.Height), screenWidth, screenHeight);
                }
            }

            return rect;
        }

        private static LegacyUiRect ClampToScreen(LegacyUiRect rect, int screenWidth, int screenHeight)
        {
            var safeScreenWidth = Math.Max(1, screenWidth);
            var safeScreenHeight = Math.Max(1, screenHeight);
            var width = Clamp(rect.Width <= 0 ? DefaultWidth : rect.Width, MinWidth, Math.Max(MinWidth, safeScreenWidth - ScreenPadding * 2));
            var height = Clamp(rect.Height <= 0 ? DefaultHeight : rect.Height, MinHeight, Math.Max(MinHeight, safeScreenHeight - ScreenPadding * 2));
            var x = Clamp(rect.X, ScreenPadding, Math.Max(ScreenPadding, safeScreenWidth - width - ScreenPadding));
            var y = Clamp(rect.Y, ScreenPadding, Math.Max(ScreenPadding, safeScreenHeight - height - ScreenPadding));
            return new LegacyUiRect(x, y, width, height);
        }

        private static bool IntersectsAny(LegacyUiRect rect, List<LegacyUiRect> occupied)
        {
            for (var index = 0; index < occupied.Count; index++)
            {
                if (Intersects(rect, occupied[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Intersects(LegacyUiRect a, LegacyUiRect b)
        {
            return a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
        }

        private static UserNotesPinnedOverlayItem HitTest(UserNotesPinnedOverlayFrame frame, int mouseX, int mouseY)
        {
            if (frame == null || frame.Items == null)
            {
                return null;
            }

            for (var index = frame.Items.Count - 1; index >= 0; index--)
            {
                var item = frame.Items[index];
                if (item != null && item.Rect.Contains(mouseX, mouseY))
                {
                    return item;
                }
            }

            return null;
        }

        private static UserNotesPinnedOverlayItem FindItem(UserNotesPinnedOverlayFrame frame, string noteId)
        {
            if (frame == null || frame.Items == null || string.IsNullOrWhiteSpace(noteId))
            {
                return null;
            }

            for (var index = 0; index < frame.Items.Count; index++)
            {
                var item = frame.Items[index];
                if (item != null && string.Equals(item.NoteId, noteId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static string[] BuildBodyLines(string body, int width)
        {
            var text = string.IsNullOrWhiteSpace(body) ? string.Empty : body.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = UserNotesUiState.BuildBodyLinesForDrawing(text, Math.Max(1, width));
            return lines.Length <= 0 ? new[] { string.Empty } : lines;
        }

        private static int GetScrollOffset(string noteId)
        {
            var id = UserNotesStore.NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(id))
            {
                return 0;
            }

            lock (SyncRoot)
            {
                int value;
                return BodyScrollOffsets.TryGetValue(id, out value) ? value : 0;
            }
        }

        private static void SetScrollOffset(string noteId, int value)
        {
            var id = UserNotesStore.NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            lock (SyncRoot)
            {
                BodyScrollOffsets[id] = Math.Max(0, value);
            }
        }

        private static UserNotePinnedState BuildStateFromItem(UserNotesPinnedOverlayItem item)
        {
            return new UserNotePinnedState
            {
                Pinned = true,
                X = item.Rect.X,
                Y = item.Rect.Y,
                Width = item.Rect.Width,
                Height = item.Rect.Height,
                OpacityPercent = item.OpacityPercent
            };
        }

        private static int WheelDeltaToScrollOffset(int rawScrollDelta)
        {
            var scrollDelta = -rawScrollDelta / 3;
            if (scrollDelta == 0)
            {
                scrollDelta = rawScrollDelta > 0 ? -40 : 40;
            }

            return scrollDelta;
        }

        private static UserNotesPinnedOverlayDragState GetDragState()
        {
            lock (SyncRoot)
            {
                return _drag == null ? null : _drag.Clone();
            }
        }

        private static void ClearDrag()
        {
            lock (SyncRoot)
            {
                _drag = null;
            }
        }

        private static void SetLastInteraction(UserNotesPinnedOverlayInteraction interaction)
        {
            lock (SyncRoot)
            {
                _lastInteraction = interaction.Clone();
            }
        }

        private static int ClampOpacity(int value)
        {
            if (value < 0)
            {
                return 100;
            }

            if (value > 100)
            {
                return 100;
            }

            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                max = min;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }

    internal sealed class UserNotesPinnedOverlayFrame
    {
        public UserNotesPinnedOverlayFrame(List<UserNotesPinnedOverlayItem> items, string draggingNoteId, bool dragging)
        {
            Items = items ?? new List<UserNotesPinnedOverlayItem>();
            DraggingNoteId = draggingNoteId ?? string.Empty;
            Dragging = dragging;
        }

        public List<UserNotesPinnedOverlayItem> Items { get; private set; }
        public string DraggingNoteId { get; private set; }
        public bool Dragging { get; private set; }
    }

    internal sealed class UserNotesPinnedOverlayItem
    {
        public string NoteId { get; set; }
        public string Body { get; set; }
        public LegacyUiRect Rect { get; set; }
        public LegacyUiRect HeaderRect { get; set; }
        public LegacyUiRect DragHandleRect { get; set; }
        public LegacyUiRect DecreaseOpacityRect { get; set; }
        public LegacyUiRect IncreaseOpacityRect { get; set; }
        public LegacyUiRect CloseRect { get; set; }
        public LegacyUiRect BodyRect { get; set; }
        public string[] BodyLines { get; set; }
        public int BodyContentHeight { get; set; }
        public int BodyScrollOffset { get; set; }
        public int BodyMaxScroll { get; set; }
        public int OpacityPercent { get; set; }
        public bool Hovered { get; set; }
    }

    internal sealed class UserNotesPinnedOverlayInteraction
    {
        public static readonly UserNotesPinnedOverlayInteraction None = new UserNotesPinnedOverlayInteraction();

        public string HitNoteId { get; set; }
        public bool MouseInside { get; set; }
        public bool CapturedMouse { get; set; }
        public bool ScrollConsumed { get; set; }
        public int BodyScrollBefore { get; set; }
        public int BodyScrollAfter { get; set; }
        public bool DragStarted { get; set; }
        public bool Dragging { get; set; }
        public bool DragSaved { get; set; }
        public bool OpacityChanged { get; set; }
        public bool Unpinned { get; set; }
        public UserNotePinnedState PendingState { get; set; }
        public UserNotesOperationResult PersistResult { get; set; }

        public UserNotesPinnedOverlayInteraction Clone()
        {
            return new UserNotesPinnedOverlayInteraction
            {
                HitNoteId = HitNoteId ?? string.Empty,
                MouseInside = MouseInside,
                CapturedMouse = CapturedMouse,
                ScrollConsumed = ScrollConsumed,
                BodyScrollBefore = BodyScrollBefore,
                BodyScrollAfter = BodyScrollAfter,
                DragStarted = DragStarted,
                Dragging = Dragging,
                DragSaved = DragSaved,
                OpacityChanged = OpacityChanged,
                Unpinned = Unpinned,
                PendingState = PendingState == null ? null : PendingState.Clone(),
                PersistResult = PersistResult
            };
        }

        public string ToVerificationJson()
        {
            return "{" +
                   "\"hitNoteId\":\"" + EscapeJson(HitNoteId) + "\"," +
                   "\"mouseInside\":" + BoolRaw(MouseInside) + "," +
                   "\"capturedMouse\":" + BoolRaw(CapturedMouse) + "," +
                   "\"scrollConsumed\":" + BoolRaw(ScrollConsumed) + "," +
                   "\"bodyScrollBefore\":" + BodyScrollBefore.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"bodyScrollAfter\":" + BodyScrollAfter.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"dragStarted\":" + BoolRaw(DragStarted) + "," +
                   "\"dragging\":" + BoolRaw(Dragging) + "," +
                   "\"dragSaved\":" + BoolRaw(DragSaved) + "," +
                   "\"opacityChanged\":" + BoolRaw(OpacityChanged) + "," +
                   "\"unpinned\":" + BoolRaw(Unpinned) + "," +
                   "\"persistResultCode\":\"" + EscapeJson(PersistResult == null ? string.Empty : PersistResult.ResultCode) + "\"" +
                   "}";
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }

    internal sealed class UserNotesPinnedOverlayDragState
    {
        public string NoteId { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }

        public UserNotesPinnedOverlayDragState Clone()
        {
            return new UserNotesPinnedOverlayDragState
            {
                NoteId = NoteId ?? string.Empty,
                OffsetX = OffsetX,
                OffsetY = OffsetY
            };
        }
    }

}
