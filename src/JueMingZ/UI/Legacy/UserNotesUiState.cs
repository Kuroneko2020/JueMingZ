using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Information.Notes;
using JueMingZ.Compat;
using JueMingZ.UI;

namespace JueMingZ.UI.Legacy
{
    internal static class UserNotesUiState
    {
        public const string AddButtonId = "notes:add";
        public const string PinButtonPrefix = "notes:pin:";
        public const string DeleteButtonPrefix = "notes:delete:";
        public const string TitleElementPrefix = "notes:title:";
        public const string BodyElementPrefix = "notes:body:";
        public const string EditOutsideElementId = "notes:edit-outside";
        public const string EmptyBodyPromptText = "双击进入编辑";

        private const int AddButtonHeight = 34;
        private const int CardGap = 8;
        private const int CardPadding = 8;
        private const int CardHeaderHeight = 28;
        private const int HeaderBodyGap = 5;
        private const int BodyTextInset = 10;
        private const int BodyLineHeight = 21;
        private const int BodyMinTextHeight = BodyLineHeight * 2;
        private const int BodyMinHeight = BodyMinTextHeight + BodyTextInset * 2;
        private const int SingleColumnThreshold = 360;
        private const int TitleMaxLength = 80;
        private const string EditModeTitle = "title";
        private const string EditModeBody = "body";
        private const float TitleTextScale = 0.76f;
        private const float BodyTextScale = 0.66f;
        private const float PinnedOverlayBodyWrapTextScale = UserNotesPinnedOverlayState.BodyTextScale;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, int> BodyScrollOffsets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, UserNotesBodyScrollState> BodyScrollStates = new Dictionary<string, UserNotesBodyScrollState>(StringComparer.OrdinalIgnoreCase);

        private static UserNotesCache _cache;
        private static bool _loaded;
        private static string _deleteConfirmNoteId = string.Empty;
        private static UserNotesOperationResult _lastOperation = UserNotesOperationResult.Success("idle", "idle");
        private static UserNotesPageLayout _layoutCache;
        private static int _layoutCacheSignature;
        private static bool _layoutCacheValid;
        private static UserNotesEditTransaction _editor;

        public static bool HasActiveEditor
        {
            get { lock (SyncRoot) { return _editor != null; } }
        }

        public static UserNotesOperationResult EnsureLoaded()
        {
            lock (SyncRoot)
            {
                if (_loaded)
                {
                    return _lastOperation;
                }
            }

            var result = GetCache().Refresh();
            lock (SyncRoot)
            {
                _loaded = true;
                _lastOperation = result;
                _layoutCacheValid = false;
            }

            return result;
        }

        public static UserNotesSnapshot Snapshot
        {
            get
            {
                EnsureLoaded();
                return GetCache().Snapshot;
            }
        }

        public static UserNotesOperationResult CreateDefaultNote(out UserNoteSnapshot note)
        {
            EnsureLoaded();
            var result = GetCache().CreateDefaultNote(out note);
            lock (SyncRoot)
            {
                _lastOperation = result;
                if (result.Succeeded)
                {
                    _deleteConfirmNoteId = string.Empty;
                }

                _layoutCacheValid = false;
            }

            if (!result.Succeeded)
            {
                UserNotesDiagnostics.RecordOperation("Ui.Notes.Add", result, string.Empty);
            }

            return result;
        }

        public static UserNotesOperationResult PinNote(string noteId, out UserNoteSnapshot note)
        {
            EnsureLoaded();
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                note = null;
                var invalid = UserNotesOperationResult.Failure("invalidNoteId", "invalid note id");
                UserNotesDiagnostics.RecordOperation("Ui.Notes.Pin", invalid, noteId);
                SetLastOperation(invalid);
                return invalid;
            }

            var current = FindNote(Snapshot, normalizedId);
            var pinnedState = current == null || current.PinnedState == null
                ? new UserNotePinnedState()
                : current.PinnedState.Clone();
            pinnedState.Pinned = true;
            if (current == null || current.PinnedState == null || !current.PinnedState.Pinned || pinnedState.Width <= 0f || pinnedState.Height <= 0f)
            {
                pinnedState = UserNotesPinnedOverlayState.BuildInitialPinnedState(
                    Snapshot,
                    normalizedId,
                    LegacyMainUiState.WindowRect,
                    SafeScreenWidth(),
                    SafeScreenHeight());
            }

            if (pinnedState.Width <= 0f)
            {
                pinnedState.Width = UserNotesPinnedOverlayState.DefaultWidth;
            }

            if (pinnedState.Height <= 0f)
            {
                pinnedState.Height = UserNotesPinnedOverlayState.DefaultHeight;
            }

            if (pinnedState.OpacityPercent < 0)
            {
                pinnedState.OpacityPercent = 0;
            }
            else if (pinnedState.OpacityPercent > 100)
            {
                pinnedState.OpacityPercent = 100;
            }

            var result = UpdatePinnedStateCore(normalizedId, pinnedState, "Ui.Notes.Pin", out note);
            return result;
        }

        public static UserNotesOperationResult UpdatePinnedState(string noteId, UserNotePinnedState pinnedState, string scenario, out UserNoteSnapshot note)
        {
            EnsureLoaded();
            return UpdatePinnedStateCore(noteId, pinnedState, scenario, out note);
        }

        private static UserNotesOperationResult UpdatePinnedStateCore(string noteId, UserNotePinnedState pinnedState, string scenario, out UserNoteSnapshot note)
        {
            note = null;
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                var invalid = UserNotesOperationResult.Failure("invalidNoteId", "invalid note id");
                UserNotesDiagnostics.RecordOperation(string.IsNullOrWhiteSpace(scenario) ? "Ui.Notes.Pin" : scenario, invalid, noteId);
                SetLastOperation(invalid);
                return invalid;
            }

            pinnedState = pinnedState ?? new UserNotePinnedState();
            var result = GetCache().UpdatePinnedState(normalizedId, pinnedState, out note);
            lock (SyncRoot)
            {
                _lastOperation = result;
                if (result.Succeeded && !pinnedState.Pinned)
                {
                    BodyScrollOffsets.Remove(normalizedId);
                    BodyScrollStates.Remove(normalizedId);
                }

                _layoutCacheValid = false;
            }

            if (!string.IsNullOrWhiteSpace(scenario) &&
                !string.Equals(scenario, "Ui.Notes.Pin", StringComparison.Ordinal) &&
                !string.Equals(scenario, "Ui.Notes.Unpin", StringComparison.Ordinal))
            {
                UserNotesDiagnostics.RecordOperation(scenario, result, normalizedId);
            }
            else if (!result.Succeeded)
            {
                UserNotesDiagnostics.RecordOperation(string.IsNullOrWhiteSpace(scenario) ? "Ui.Notes.Pin" : scenario, result, normalizedId);
            }

            return result;
        }

        public static UserNotesOperationResult RequestDeleteOrConfirm(string noteId)
        {
            EnsureLoaded();
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                var invalid = UserNotesOperationResult.Failure("invalidNoteId", "invalid note id");
                UserNotesDiagnostics.RecordOperation("Ui.Notes.Delete", invalid, noteId);
                SetLastOperation(invalid);
                return invalid;
            }

            lock (SyncRoot)
            {
                if (!string.Equals(_deleteConfirmNoteId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    _deleteConfirmNoteId = normalizedId;
                    _lastOperation = UserNotesOperationResult.Success("confirmPending", "delete confirmation pending");
                    _layoutCacheValid = false;
                    return _lastOperation;
                }
            }

            var result = GetCache().DeleteNote(normalizedId);
            lock (SyncRoot)
            {
                _lastOperation = result;
                if (result.Succeeded)
                {
                    _deleteConfirmNoteId = string.Empty;
                    BodyScrollOffsets.Remove(normalizedId);
                    BodyScrollStates.Remove(normalizedId);
                    if (_editor != null && string.Equals(_editor.NoteId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    {
                        _editor = null;
                        LegacyMultilineTextInput.ClearFocus();
                    }
                }

                _layoutCacheValid = false;
            }

            if (!result.Succeeded)
            {
                UserNotesDiagnostics.RecordOperation("Ui.Notes.Delete", result, normalizedId);
            }

            return result;
        }

        public static bool IsDeleteConfirming(string noteId)
        {
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            lock (SyncRoot)
            {
                return !string.IsNullOrEmpty(normalizedId) &&
                       string.Equals(_deleteConfirmNoteId, normalizedId, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsEditingTitle(string noteId)
        {
            return IsEditing(noteId, EditModeTitle);
        }

        public static bool IsEditingBody(string noteId)
        {
            return IsEditing(noteId, EditModeBody);
        }

        public static UserNotesOperationResult HandleTitleEditCommand(string noteId, bool doubleClick, int mouseX, int mouseY, LegacyUiRect rect)
        {
            return HandleEditCommand(noteId, EditModeTitle, doubleClick, mouseX, mouseY, rect);
        }

        public static UserNotesOperationResult HandleBodyEditCommand(string noteId, bool doubleClick, int mouseX, int mouseY, LegacyUiRect rect)
        {
            return HandleEditCommand(noteId, EditModeBody, doubleClick, mouseX, mouseY, rect);
        }

        public static UserNotesOperationResult SaveActiveEditor(string reason)
        {
            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor == null)
            {
                return UserNotesOperationResult.Success("noActiveEditor", "no active editor");
            }

            var snapshot = LegacyMultilineTextInput.GetSnapshot(editor.InputId);
            var draft = snapshot == null ? string.Empty : snapshot.Draft ?? string.Empty;
            var current = FindNote(Snapshot, editor.NoteId);
            if (current == null)
            {
                var missing = UserNotesOperationResult.Failure("missingNote", "note not found");
                SetLastOperation(missing);
                return missing;
            }

            var title = string.Equals(editor.Mode, EditModeTitle, StringComparison.Ordinal)
                ? draft
                : current.Title;
            var body = string.Equals(editor.Mode, EditModeBody, StringComparison.Ordinal)
                ? draft
                : current.Body;
            var dirty = !string.Equals(title ?? string.Empty, editor.OriginalTitle ?? string.Empty, StringComparison.Ordinal) ||
                        !string.Equals(body ?? string.Empty, editor.OriginalBody ?? string.Empty, StringComparison.Ordinal);
            if (!dirty)
            {
                ClearActiveEditor();
                var unchanged = UserNotesOperationResult.Success("unchanged", "unchanged");
                SetLastOperation(unchanged);
                return unchanged;
            }

            UserNoteSnapshot saved;
            var result = GetCache().SaveNote(editor.NoteId, title, body, out saved);
            lock (SyncRoot)
            {
                _lastOperation = result;
                _layoutCacheValid = false;
                if (result.Succeeded)
                {
                    _editor = null;
                    _deleteConfirmNoteId = string.Empty;
                }
                else if (_editor != null && string.Equals(_editor.InputId, editor.InputId, StringComparison.Ordinal))
                {
                    _editor.LastFailure = result.Message ?? string.Empty;
                }
            }

            if (result.Succeeded)
            {
                LegacyMultilineTextInput.ClearFocus();
            }
            else
            {
                UserNotesDiagnostics.RecordOperation("Ui.Notes.Save", result, editor.NoteId);
            }

            return result;
        }

        public static UserNotesOperationResult CancelActiveEditor()
        {
            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor;
                _editor = null;
                _lastOperation = UserNotesOperationResult.Success("cancelled", "edit cancelled");
                _layoutCacheValid = false;
            }

            if (editor != null)
            {
                LegacyMultilineTextInput.ClearFocus();
            }

            return UserNotesOperationResult.Success("cancelled", "edit cancelled");
        }

        public static void UpdateActiveEditorForDraw()
        {
            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor == null)
            {
                return;
            }

            var isBody = string.Equals(editor.Mode, EditModeBody, StringComparison.Ordinal);
            var update = LegacyMultilineTextInput.Update(editor.InputId, isBody, isBody ? 0 : TitleMaxLength);
            if (update == null)
            {
                return;
            }

            if (update.Changed)
            {
                MarkEditorChanged(editor.InputId);
            }

            if (update.CancelRequested)
            {
                CancelActiveEditor();
                return;
            }

            if (update.SubmitRequested)
            {
                SaveActiveEditor("submit");
            }
        }

        public static string GetTitleDisplayText(UserNoteSnapshot note)
        {
            if (note == null)
            {
                return UserNotesStore.DefaultTitle;
            }

            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor != null &&
                string.Equals(editor.Mode, EditModeTitle, StringComparison.Ordinal) &&
                string.Equals(editor.NoteId, note.NoteId, StringComparison.OrdinalIgnoreCase))
            {
                var snapshot = LegacyMultilineTextInput.GetSnapshot(editor.InputId);
                return snapshot == null ? string.Empty : snapshot.DisplayText ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(note.Title) ? UserNotesStore.DefaultTitle : note.Title;
        }

        public static string GetBodyDisplayText(UserNoteSnapshot note)
        {
            if (note == null)
            {
                return string.Empty;
            }

            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor != null &&
                string.Equals(editor.Mode, EditModeBody, StringComparison.Ordinal) &&
                string.Equals(editor.NoteId, note.NoteId, StringComparison.OrdinalIgnoreCase))
            {
                var snapshot = LegacyMultilineTextInput.GetSnapshot(editor.InputId);
                return snapshot == null ? string.Empty : snapshot.DisplayText ?? string.Empty;
            }

            return NormalizeBodyPreview(note.Body);
        }

        public static bool TryAttachActiveEditorImePanel(string noteId, string mode, LegacyUiRect anchor)
        {
            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor == null ||
                !string.Equals(editor.Mode, NormalizeEditMode(mode), StringComparison.Ordinal) ||
                !string.Equals(editor.NoteId, UserNotesStore.NormalizeNoteId(noteId), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return LegacyMultilineTextInput.TryAttachImeCompositionPanel(editor.InputId, anchor);
        }

        public static int ResolveBodyEditorImeLineY(string noteId, LegacyUiRect textViewport)
        {
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor == null ||
                !string.Equals(editor.Mode, EditModeBody, StringComparison.Ordinal) ||
                !string.Equals(editor.NoteId, normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return textViewport.Y;
            }

            var snapshot = LegacyMultilineTextInput.GetSnapshot(editor.InputId);
            var lines = BuildWrappedBodyLineModels(snapshot == null ? string.Empty : snapshot.Draft, Math.Max(1, textViewport.Width));
            var cursor = snapshot == null ? 0 : snapshot.CursorIndex;
            var lineIndex = FindLineIndexForCursor(lines, cursor);
            var y = textViewport.Y + lineIndex * BodyLineHeight - GetBodyScrollOffset(normalizedId);
            return Clamp(y, textViewport.Y, Math.Max(textViewport.Y, textViewport.Bottom - BodyLineHeight));
        }

        public static UserNotesPageLayout BuildLayout(int viewportWidth, int viewportHeight)
        {
            EnsureLoaded();
            var snapshot = GetCache().Snapshot;
            var signature = BuildLayoutSignature(snapshot, viewportWidth, viewportHeight);
            lock (SyncRoot)
            {
                if (_layoutCacheValid &&
                    _layoutCache != null &&
                    _layoutCacheSignature == signature)
                {
                    return _layoutCache;
                }
            }

            var layout = BuildLayoutUncached(snapshot, viewportWidth, viewportHeight);
            lock (SyncRoot)
            {
                _layoutCache = layout;
                _layoutCacheSignature = signature;
                _layoutCacheValid = true;
            }

            return layout;
        }

        public static int BuildStateSignature()
        {
            EnsureLoaded();
            var snapshot = GetCache().Snapshot;
            unchecked
            {
                var hash = 17;
                AddHash(ref hash, snapshot.Revision);
                AddHash(ref hash, snapshot.Notes == null ? 0 : snapshot.Notes.Count);
                if (snapshot.Notes != null)
                {
                    for (var index = 0; index < snapshot.Notes.Count; index++)
                    {
                        var note = snapshot.Notes[index];
                        AddHash(ref hash, note == null ? string.Empty : note.NoteId);
                        AddHash(ref hash, note == null ? string.Empty : note.Title);
                        AddHash(ref hash, note == null ? 0 : note.BodyLength);
                        AddHash(ref hash, note != null && note.PinnedState != null && note.PinnedState.Pinned);
                    }
                }

                lock (SyncRoot)
                {
                    AddHash(ref hash, _deleteConfirmNoteId);
                    AddHash(ref hash, _lastOperation == null ? string.Empty : _lastOperation.ResultCode);
                    if (_editor != null)
                    {
                        var editorSnapshot = LegacyMultilineTextInput.GetSnapshot(_editor.InputId);
                        AddHash(ref hash, _editor.NoteId);
                        AddHash(ref hash, _editor.Mode);
                        AddHash(ref hash, editorSnapshot == null ? string.Empty : editorSnapshot.Draft);
                        AddHash(ref hash, editorSnapshot == null ? 0 : editorSnapshot.CursorIndex);
                        AddHash(ref hash, editorSnapshot == null ? string.Empty : editorSnapshot.CompositionPreview);
                        AddHash(ref hash, _editor.LastFailure);
                    }

                    foreach (var pair in BodyScrollOffsets)
                    {
                        AddHash(ref hash, pair.Key);
                        AddHash(ref hash, pair.Value);
                    }
                }

                return hash;
            }
        }

        public static void BeginBodyViewportFrame()
        {
            lock (SyncRoot)
            {
                BodyScrollStates.Clear();
            }
        }

        public static void SetBodyViewport(string noteId, LegacyUiRect viewport, int contentHeight)
        {
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(normalizedId) || viewport.Width <= 0 || viewport.Height <= 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                int offset;
                BodyScrollOffsets.TryGetValue(normalizedId, out offset);
                var maxScroll = Math.Max(0, contentHeight - Math.Max(0, viewport.Height));
                offset = Clamp(offset, 0, maxScroll);
                BodyScrollOffsets[normalizedId] = offset;
                BodyScrollStates[normalizedId] = new UserNotesBodyScrollState
                {
                    NoteId = normalizedId,
                    Viewport = viewport,
                    MaxScroll = maxScroll
                };
            }
        }

        public static int GetBodyScrollOffset(string noteId)
        {
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            lock (SyncRoot)
            {
                int value;
                return !string.IsNullOrEmpty(normalizedId) && BodyScrollOffsets.TryGetValue(normalizedId, out value)
                    ? value
                    : 0;
            }
        }

        public static bool TryConsumeNestedScroll(LegacyMouseSnapshot mouse, int rawScrollDelta)
        {
            if (mouse == null || rawScrollDelta == 0)
            {
                return false;
            }

            lock (SyncRoot)
            {
                foreach (var pair in BodyScrollStates)
                {
                    var state = pair.Value;
                    if (state == null ||
                        state.Viewport.Width <= 0 ||
                        state.Viewport.Height <= 0 ||
                        state.MaxScroll <= 0 ||
                        !state.Viewport.Contains(mouse.X, mouse.Y))
                    {
                        continue;
                    }

                    int current;
                    BodyScrollOffsets.TryGetValue(state.NoteId, out current);
                    var next = Clamp(current + ConvertWheelDelta(rawScrollDelta), 0, state.MaxScroll);
                    if (next == current)
                    {
                        return false;
                    }

                    BodyScrollOffsets[state.NoteId] = next;
                    _layoutCacheValid = false;
                    return true;
                }
            }

            return false;
        }

        public static bool EnsureActiveBodyEditorCaretVisible(string noteId)
        {
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return false;
            }

            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor == null ||
                !string.Equals(editor.Mode, EditModeBody, StringComparison.Ordinal) ||
                !string.Equals(editor.NoteId, normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var snapshot = LegacyMultilineTextInput.GetSnapshot(editor.InputId);
            if (snapshot == null)
            {
                return false;
            }

            lock (SyncRoot)
            {
                UserNotesBodyScrollState state;
                if (!BodyScrollStates.TryGetValue(normalizedId, out state) ||
                    state == null ||
                    state.Viewport.Width <= 0 ||
                    state.Viewport.Height <= 0 ||
                    state.MaxScroll <= 0)
                {
                    return false;
                }

                var lines = BuildWrappedBodyLineModels(snapshot.Draft ?? string.Empty, Math.Max(1, state.Viewport.Width));
                var lineIndex = FindLineIndexForCursor(lines, snapshot.CursorIndex);
                var caretTop = lineIndex * BodyLineHeight;
                var caretBottom = caretTop + BodyLineHeight;
                int current;
                BodyScrollOffsets.TryGetValue(normalizedId, out current);
                current = Clamp(current, 0, state.MaxScroll);

                var next = current;
                if (caretTop < current)
                {
                    next = caretTop;
                }
                else if (caretBottom > current + state.Viewport.Height)
                {
                    next = caretBottom - state.Viewport.Height;
                }

                next = Clamp(next, 0, state.MaxScroll);
                if (next == current)
                {
                    return false;
                }

                BodyScrollOffsets[normalizedId] = next;
                _layoutCacheValid = false;
                return true;
            }
        }

        public static string BuildUiStateJson()
        {
            var snapshot = Snapshot;
            string confirm;
            UserNotesOperationResult last;
            lock (SyncRoot)
            {
                confirm = _deleteConfirmNoteId;
                last = _lastOperation;
            }

            return "{" +
                   "\"featureId\":\"information.user_notes\"," +
                   "\"noteCount\":" + (snapshot.Notes == null ? 0 : snapshot.Notes.Count).ToString(CultureInfo.InvariantCulture) + "," +
                   "\"revision\":" + snapshot.Revision.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"deleteConfirmNoteId\":\"" + EscapeJson(confirm) + "\"," +
                   "\"editingNoteId\":\"" + EscapeJson(GetActiveEditorNoteId()) + "\"," +
                   "\"editingMode\":\"" + EscapeJson(GetActiveEditorMode()) + "\"," +
                   "\"lastResultCode\":\"" + EscapeJson(last == null ? string.Empty : last.ResultCode) + "\"" +
                   "}";
        }

        internal static string[] BuildWrappedBodyLinesForTesting(string body, int width)
        {
            return BuildWrappedBodyLines(NormalizeBodyPreview(body), width).ToArray();
        }

        internal static string[] BuildCardBodyLinesForDrawing(string body, int textWidth)
        {
            return BuildWrappedBodyLines(body ?? string.Empty, Math.Max(1, textWidth)).ToArray();
        }

        internal static string[] BuildBodyLinesForDrawing(string body, int width)
        {
            return BuildWrappedBodyLines(body ?? string.Empty, Math.Max(1, width), PinnedOverlayBodyWrapTextScale).ToArray();
        }

        internal static LegacyUiRect ResolveBodyTextViewport(LegacyUiRect bodyPanel)
        {
            return new LegacyUiRect(
                bodyPanel.X + BodyTextInset,
                bodyPanel.Y + BodyTextInset,
                Math.Max(1, bodyPanel.Width - BodyTextInset * 2),
                Math.Max(1, bodyPanel.Height - BodyTextInset * 2));
        }

        internal static int BodyTextInsetForTesting
        {
            get { return BodyTextInset; }
        }

        internal static int BodyLineHeightForLayout
        {
            get { return BodyLineHeight; }
        }

        internal static float BodyTextScaleForLayout
        {
            get { return BodyTextScale; }
        }

        internal static int BodyLineHeightForTesting
        {
            get { return BodyLineHeight; }
        }

        internal static float BodyTextScaleForTesting
        {
            get { return BodyTextScale; }
        }

        internal static int CalculateBodyTextContentHeightForTesting(int lineCount)
        {
            return CalculateBodyTextContentHeight(lineCount);
        }

        internal static void SetBodyViewportForTesting(string noteId, LegacyUiRect viewport, int contentHeight)
        {
            SetBodyViewport(noteId, viewport, contentHeight);
        }

        internal static bool EnsureActiveBodyEditorCaretVisibleForTesting(string noteId)
        {
            return EnsureActiveBodyEditorCaretVisible(noteId);
        }

        internal static string BuildEditorInputIdForTesting(string noteId, string mode)
        {
            return BuildEditorInputId(UserNotesStore.NormalizeNoteId(noteId), NormalizeEditMode(mode));
        }

        internal static UserNotesOperationResult ApplyActiveEditorControlForTesting(TextInputControlState controlState)
        {
            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor == null)
            {
                return UserNotesOperationResult.Success("noActiveEditor", "no active editor");
            }

            var isBody = string.Equals(editor.Mode, EditModeBody, StringComparison.Ordinal);
            var update = LegacyMultilineTextInput.ApplyControlForTesting(editor.InputId, isBody, isBody ? 0 : TitleMaxLength, controlState);
            if (update.Changed)
            {
                MarkEditorChanged(editor.InputId);
            }

            if (update.CancelRequested)
            {
                return CancelActiveEditor();
            }

            if (update.SubmitRequested)
            {
                return SaveActiveEditor("submit");
            }

            return UserNotesOperationResult.Success("updated", "updated");
        }

        internal static void SetCacheForTesting(UserNotesCache cache, bool loaded)
        {
            lock (SyncRoot)
            {
                _cache = cache;
                _loaded = loaded;
                _deleteConfirmNoteId = string.Empty;
                _editor = null;
                _lastOperation = UserNotesOperationResult.Success("testing", "testing");
                BodyScrollOffsets.Clear();
                BodyScrollStates.Clear();
                _layoutCache = null;
                _layoutCacheValid = false;
                _layoutCacheSignature = 0;
            }

            LegacyMultilineTextInput.ClearFocus();
        }

        internal static void ResetForTesting()
        {
            SetCacheForTesting(null, false);
        }

        private static UserNotesCache GetCache()
        {
            lock (SyncRoot)
            {
                if (_cache == null)
                {
                    _cache = new UserNotesCache();
                }

                return _cache;
            }
        }

        private static void SetLastOperation(UserNotesOperationResult result)
        {
            lock (SyncRoot)
            {
                _lastOperation = result;
                _layoutCacheValid = false;
            }
        }

        private static UserNoteSnapshot FindNote(UserNotesSnapshot snapshot, string noteId)
        {
            if (snapshot == null || snapshot.Notes == null || string.IsNullOrEmpty(noteId))
            {
                return null;
            }

            for (var index = 0; index < snapshot.Notes.Count; index++)
            {
                var note = snapshot.Notes[index];
                if (note != null && string.Equals(note.NoteId, noteId, StringComparison.OrdinalIgnoreCase))
                {
                    return note;
                }
            }

            return null;
        }

        private static UserNotesPageLayout BuildLayoutUncached(UserNotesSnapshot snapshot, int viewportWidth, int viewportHeight)
        {
            var safeWidth = Math.Max(1, viewportWidth);
            var safeHeight = Math.Max(1, viewportHeight);
            var columns = safeWidth < SingleColumnThreshold ? 1 : 2;
            var columnGap = columns == 1 ? 0 : CardGap;
            var columnWidth = columns == 1 ? safeWidth : Math.Max(1, (safeWidth - columnGap) / 2);
            var columnHeights = new int[columns];
            var startY = AddButtonHeight + CardGap;
            for (var index = 0; index < columns; index++)
            {
                columnHeights[index] = startY;
            }

            var cards = new List<UserNoteCardLayout>();
            var notes = snapshot == null || snapshot.Notes == null
                ? new List<UserNoteSnapshot>()
                : snapshot.Notes;
            for (var index = 0; index < notes.Count; index++)
            {
                var note = notes[index];
                if (note == null)
                {
                    continue;
                }

                var column = FindShortestColumn(columnHeights);
                var bodyText = GetBodyMeasurementText(note);
                var bodyPanelWidth = Math.Max(1, columnWidth - CardPadding * 2);
                var bodyTextWidth = Math.Max(1, bodyPanelWidth - BodyTextInset * 2);
                var bodyLines = BuildWrappedBodyLineModels(bodyText, bodyTextWidth);
                var bodyContentHeight = CalculateBodyTextContentHeight(bodyLines.Count);
                var maxCardHeight = Math.Max(CardHeaderHeight + HeaderBodyGap + BodyMinHeight + CardPadding * 2, safeHeight / 2);
                var maxBodyHeight = Math.Max(BodyMinHeight, maxCardHeight - CardPadding * 2 - CardHeaderHeight - HeaderBodyGap);
                var bodyViewportHeight = Math.Min(CalculateBodyPanelHeight(bodyContentHeight), maxBodyHeight);
                var cardHeight = CardPadding * 2 + CardHeaderHeight + HeaderBodyGap + bodyViewportHeight;
                if (cardHeight > maxCardHeight)
                {
                    cardHeight = maxCardHeight;
                    bodyViewportHeight = Math.Max(BodyMinHeight, cardHeight - CardPadding * 2 - CardHeaderHeight - HeaderBodyGap);
                }

                cards.Add(new UserNoteCardLayout
                {
                    NoteId = note.NoteId ?? string.Empty,
                    ColumnIndex = column,
                    X = column * (columnWidth + columnGap),
                    Y = columnHeights[column],
                    Width = columnWidth,
                    Height = cardHeight,
                    BodyY = CardPadding + CardHeaderHeight + HeaderBodyGap,
                    BodyHeight = bodyViewportHeight,
                    BodyContentHeight = bodyContentHeight,
                    BodyLines = ToTextArray(bodyLines)
                });
                columnHeights[column] += cardHeight + CardGap;
            }

            var contentHeight = AddButtonHeight + CardGap + 24;
            for (var index = 0; index < columnHeights.Length; index++)
            {
                contentHeight = Math.Max(contentHeight, columnHeights[index] - CardGap + 24);
            }

            return new UserNotesPageLayout
            {
                AddButtonHeight = AddButtonHeight,
                ColumnCount = columns,
                ColumnWidth = columnWidth,
                ContentHeight = contentHeight,
                Cards = cards
            };
        }

        private static int CalculateBodyTextContentHeight(int lineCount)
        {
            return Math.Max(BodyMinTextHeight, Math.Max(1, lineCount) * BodyLineHeight);
        }

        private static int CalculateBodyPanelHeight(int textContentHeight)
        {
            return Math.Max(BodyMinHeight, Math.Max(0, textContentHeight) + BodyTextInset * 2);
        }

        private static int FindShortestColumn(int[] columnHeights)
        {
            var column = 0;
            var height = columnHeights == null || columnHeights.Length <= 0 ? 0 : columnHeights[0];
            if (columnHeights == null)
            {
                return 0;
            }

            for (var index = 1; index < columnHeights.Length; index++)
            {
                if (columnHeights[index] < height)
                {
                    height = columnHeights[index];
                    column = index;
                }
            }

            return column;
        }

        private static int BuildLayoutSignature(UserNotesSnapshot snapshot, int viewportWidth, int viewportHeight)
        {
            unchecked
            {
                var hash = 17;
                AddHash(ref hash, viewportWidth);
                AddHash(ref hash, viewportHeight);
                AddHash(ref hash, UiTextRenderer.FontSignatureForLayoutCache);
                AddHash(ref hash, UiTextRenderer.CacheGenerationForLayoutCache);
                AddHash(ref hash, snapshot == null ? 0 : snapshot.Revision);
                AddHash(ref hash, snapshot == null || snapshot.Notes == null ? 0 : snapshot.Notes.Count);
                if (snapshot != null && snapshot.Notes != null)
                {
                    for (var index = 0; index < snapshot.Notes.Count; index++)
                    {
                        var note = snapshot.Notes[index];
                        AddHash(ref hash, note == null ? string.Empty : note.NoteId);
                        AddHash(ref hash, note == null ? string.Empty : note.Title);
                        AddHash(ref hash, note == null ? string.Empty : note.Body);
                    }
                }

                lock (SyncRoot)
                {
                    if (_editor != null)
                    {
                        var editorSnapshot = LegacyMultilineTextInput.GetSnapshot(_editor.InputId);
                        AddHash(ref hash, _editor.NoteId);
                        AddHash(ref hash, _editor.Mode);
                        AddHash(ref hash, editorSnapshot == null ? string.Empty : editorSnapshot.Draft);
                        AddHash(ref hash, editorSnapshot == null ? 0 : editorSnapshot.CursorIndex);
                        AddHash(ref hash, editorSnapshot == null ? string.Empty : editorSnapshot.CompositionPreview);
                    }
                }

                return hash;
            }
        }

        private static string NormalizeBodyPreview(string body)
        {
            return string.IsNullOrWhiteSpace(body) ? EmptyBodyPromptText : body.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static List<string> BuildWrappedBodyLines(string text, int width)
        {
            return ToTextList(BuildWrappedBodyLineModels(text, width, BodyTextScale));
        }

        private static List<string> BuildWrappedBodyLines(string text, int width, float scale)
        {
            return ToTextList(BuildWrappedBodyLineModels(text, width, scale));
        }

        private static List<UserNotesTextLine> BuildWrappedBodyLineModels(string text, int width)
        {
            return BuildWrappedBodyLineModels(text, width, BodyTextScale);
        }

        private static List<UserNotesTextLine> BuildWrappedBodyLineModels(string text, int width, float scale)
        {
            var lines = new List<UserNotesTextLine>();
            var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            if (normalized.Length <= 0)
            {
                lines.Add(new UserNotesTextLine { Text = string.Empty, StartIndex = 0, Length = 0 });
                return lines;
            }

            var paragraphStart = 0;
            while (paragraphStart <= normalized.Length)
            {
                var newline = normalized.IndexOf('\n', paragraphStart);
                var paragraphEnd = newline >= 0 ? newline : normalized.Length;
                AddWrappedParagraph(lines, normalized, paragraphStart, paragraphEnd, width, scale);
                if (newline < 0)
                {
                    break;
                }

                paragraphStart = newline + 1;
                if (paragraphStart == normalized.Length)
                {
                    lines.Add(new UserNotesTextLine { Text = string.Empty, StartIndex = paragraphStart, Length = 0 });
                    break;
                }
            }

            return lines;
        }

        private static void AddWrappedParagraph(List<UserNotesTextLine> lines, string text, int start, int end, int width, float scale)
        {
            if (end <= start)
            {
                lines.Add(new UserNotesTextLine { Text = string.Empty, StartIndex = start, Length = 0 });
                return;
            }

            var currentStart = start;
            var currentLength = 0;
            for (var index = start; index < end; index++)
            {
                var candidateLength = currentLength + 1;
                var candidate = text.Substring(currentStart, candidateLength);
                if (currentLength <= 0 || UiTextRenderer.EstimateTextWidth(candidate, scale) <= width)
                {
                    currentLength = candidateLength;
                    continue;
                }

                lines.Add(new UserNotesTextLine
                {
                    Text = text.Substring(currentStart, currentLength),
                    StartIndex = currentStart,
                    Length = currentLength
                });
                currentStart = index;
                currentLength = 1;
            }

            if (currentLength > 0)
            {
                lines.Add(new UserNotesTextLine
                {
                    Text = text.Substring(currentStart, currentLength),
                    StartIndex = currentStart,
                    Length = currentLength
                });
            }
        }

        private static UserNotesOperationResult HandleEditCommand(string noteId, string mode, bool doubleClick, int mouseX, int mouseY, LegacyUiRect rect)
        {
            EnsureLoaded();
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            mode = NormalizeEditMode(mode);
            if (string.IsNullOrEmpty(normalizedId) || string.IsNullOrEmpty(mode))
            {
                var invalid = UserNotesOperationResult.Failure("invalidEditor", "invalid note editor");
                SetLastOperation(invalid);
                return invalid;
            }

            if (IsEditing(normalizedId, mode))
            {
                SetActiveEditorCursorFromPoint(normalizedId, mode, mouseX, mouseY, rect);
                return UserNotesOperationResult.Success("cursorMoved", "cursor moved");
            }

            if (!doubleClick)
            {
                return UserNotesOperationResult.Success("singleClickIgnored", "single click ignored");
            }

            var save = SaveActiveEditor("switchEditor");
            if (!save.Succeeded)
            {
                return save;
            }

            var note = FindNote(Snapshot, normalizedId);
            if (note == null)
            {
                var missing = UserNotesOperationResult.Failure("missingNote", "note not found");
                SetLastOperation(missing);
                return missing;
            }

            var text = string.Equals(mode, EditModeTitle, StringComparison.Ordinal)
                ? (note.Title ?? string.Empty)
                : (note.Body ?? string.Empty);
            var inputId = BuildEditorInputId(normalizedId, mode);
            lock (SyncRoot)
            {
                _editor = new UserNotesEditTransaction
                {
                    NoteId = normalizedId,
                    Mode = mode,
                    InputId = inputId,
                    OriginalTitle = note.Title ?? string.Empty,
                    OriginalBody = note.Body ?? string.Empty
                };
                _deleteConfirmNoteId = string.Empty;
                _lastOperation = UserNotesOperationResult.Success("editing", "editing");
                _layoutCacheValid = false;
            }

            LegacyMultilineTextInput.Focus(inputId, text, text == null ? 0 : text.Length);
            SetActiveEditorCursorFromPoint(normalizedId, mode, mouseX, mouseY, rect);
            return UserNotesOperationResult.Success("editing", "editing");
        }

        private static void MarkEditorChanged(string inputId)
        {
            lock (SyncRoot)
            {
                if (_editor != null && string.Equals(_editor.InputId, inputId, StringComparison.Ordinal))
                {
                    _editor.Dirty = true;
                    _layoutCacheValid = false;
                }
            }
        }

        private static bool IsEditing(string noteId, string mode)
        {
            var normalizedId = UserNotesStore.NormalizeNoteId(noteId);
            mode = NormalizeEditMode(mode);
            lock (SyncRoot)
            {
                return _editor != null &&
                       string.Equals(_editor.NoteId, normalizedId, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(_editor.Mode, mode, StringComparison.Ordinal);
            }
        }

        private static string GetBodyMeasurementText(UserNoteSnapshot note)
        {
            if (note == null)
            {
                return EmptyBodyPromptText;
            }

            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor != null &&
                string.Equals(editor.Mode, EditModeBody, StringComparison.Ordinal) &&
                string.Equals(editor.NoteId, note.NoteId, StringComparison.OrdinalIgnoreCase))
            {
                var snapshot = LegacyMultilineTextInput.GetSnapshot(editor.InputId);
                if (snapshot == null)
                {
                    return string.Empty;
                }

                return (snapshot.Draft ?? string.Empty).Insert(
                    Math.Max(0, Math.Min(snapshot.CursorIndex, (snapshot.Draft ?? string.Empty).Length)),
                    snapshot.CompositionPreview ?? string.Empty);
            }

            return NormalizeBodyPreview(note.Body);
        }

        private static string GetActiveEditorNoteId()
        {
            lock (SyncRoot)
            {
                return _editor == null ? string.Empty : _editor.NoteId ?? string.Empty;
            }
        }

        private static string GetActiveEditorMode()
        {
            lock (SyncRoot)
            {
                return _editor == null ? string.Empty : _editor.Mode ?? string.Empty;
            }
        }

        private static void ClearActiveEditor()
        {
            lock (SyncRoot)
            {
                _editor = null;
                _layoutCacheValid = false;
            }

            LegacyMultilineTextInput.ClearFocus();
        }

        private static void SetActiveEditorCursorFromPoint(string noteId, string mode, int mouseX, int mouseY, LegacyUiRect rect)
        {
            UserNotesEditTransaction editor;
            lock (SyncRoot)
            {
                editor = _editor == null ? null : _editor.Clone();
            }

            if (editor == null ||
                !string.Equals(editor.NoteId, noteId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(editor.Mode, mode, StringComparison.Ordinal))
            {
                return;
            }

            var snapshot = LegacyMultilineTextInput.GetSnapshot(editor.InputId);
            var draft = snapshot == null ? string.Empty : snapshot.Draft ?? string.Empty;
            int cursor;
            if (string.Equals(mode, EditModeTitle, StringComparison.Ordinal))
            {
                cursor = CalculateCursorIndexOnLine(draft, mouseX - rect.X - 2, TitleTextScale);
            }
            else
            {
                cursor = CalculateBodyCursorIndexFromPoint(noteId, draft, rect, mouseX, mouseY);
            }

            LegacyMultilineTextInput.SetCursorIndex(editor.InputId, cursor);
            lock (SyncRoot)
            {
                _layoutCacheValid = false;
            }
        }

        private static int CalculateBodyCursorIndexFromPoint(string noteId, string draft, LegacyUiRect rect, int mouseX, int mouseY)
        {
            var lines = BuildWrappedBodyLineModels(draft ?? string.Empty, Math.Max(1, rect.Width));
            var offset = GetBodyScrollOffset(noteId);
            var lineIndex = Clamp((mouseY - rect.Y + offset) / BodyLineHeight, 0, Math.Max(0, lines.Count - 1));
            var line = lines[lineIndex];
            var x = mouseX - rect.X;
            return line.StartIndex + CalculateCursorIndexOnLine(line.Text, x, BodyTextScale);
        }

        private static int CalculateCursorIndexOnLine(string text, int x, float scale)
        {
            text = text ?? string.Empty;
            if (x <= 0 || text.Length <= 0)
            {
                return 0;
            }

            for (var index = 1; index <= text.Length; index++)
            {
                var width = UiTextRenderer.EstimateTextWidth(text.Substring(0, index), scale);
                if (width > x)
                {
                    return index - 1;
                }
            }

            return text.Length;
        }

        private static int FindLineIndexForCursor(List<UserNotesTextLine> lines, int cursor)
        {
            if (lines == null || lines.Count <= 0)
            {
                return 0;
            }

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (cursor >= line.StartIndex && cursor <= line.StartIndex + Math.Max(0, line.Length))
                {
                    return index;
                }
            }

            return lines.Count - 1;
        }

        private static string NormalizeEditMode(string mode)
        {
            if (string.Equals(mode, EditModeTitle, StringComparison.Ordinal))
            {
                return EditModeTitle;
            }

            return string.Equals(mode, EditModeBody, StringComparison.Ordinal) ? EditModeBody : string.Empty;
        }

        private static string BuildEditorInputId(string noteId, string mode)
        {
            return "notes:editor:" + mode + ":" + noteId;
        }

        private static int SafeScreenWidth()
        {
            try
            {
                return TerrariaMainCompat.ScreenWidth;
            }
            catch
            {
                return 1280;
            }
        }

        private static int SafeScreenHeight()
        {
            try
            {
                return TerrariaMainCompat.ScreenHeight;
            }
            catch
            {
                return 720;
            }
        }

        private static string[] ToTextArray(List<UserNotesTextLine> lines)
        {
            var list = ToTextList(lines);
            return list.ToArray();
        }

        private static List<string> ToTextList(List<UserNotesTextLine> lines)
        {
            var result = new List<string>();
            if (lines == null)
            {
                return result;
            }

            for (var index = 0; index < lines.Count; index++)
            {
                result.Add(lines[index] == null ? string.Empty : lines[index].Text ?? string.Empty);
            }

            return result;
        }

        private static int ConvertWheelDelta(int rawScrollDelta)
        {
            var scrollDelta = -rawScrollDelta / 3;
            if (scrollDelta == 0)
            {
                scrollDelta = rawScrollDelta > 0 ? -40 : 40;
            }

            return scrollDelta;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static void AddHash(ref int hash, int value)
        {
            hash = hash * 31 + value;
        }

        private static void AddHash(ref int hash, bool value)
        {
            hash = hash * 31 + (value ? 1 : 0);
        }

        private static void AddHash(ref int hash, string value)
        {
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
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

    internal sealed class UserNotesPageLayout
    {
        public int AddButtonHeight { get; set; }
        public int ColumnCount { get; set; }
        public int ColumnWidth { get; set; }
        public int ContentHeight { get; set; }
        public List<UserNoteCardLayout> Cards { get; set; }
    }

    internal sealed class UserNoteCardLayout
    {
        public string NoteId { get; set; }
        public int ColumnIndex { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int BodyY { get; set; }
        public int BodyHeight { get; set; }
        public int BodyContentHeight { get; set; }
        public string[] BodyLines { get; set; }
    }

    internal sealed class UserNotesBodyScrollState
    {
        public string NoteId { get; set; }
        public LegacyUiRect Viewport { get; set; }
        public int MaxScroll { get; set; }
    }

    internal sealed class UserNotesEditTransaction
    {
        public string NoteId { get; set; }
        public string Mode { get; set; }
        public string InputId { get; set; }
        public string OriginalTitle { get; set; }
        public string OriginalBody { get; set; }
        public bool Dirty { get; set; }
        public string LastFailure { get; set; }

        public UserNotesEditTransaction Clone()
        {
            return new UserNotesEditTransaction
            {
                NoteId = NoteId ?? string.Empty,
                Mode = Mode ?? string.Empty,
                InputId = InputId ?? string.Empty,
                OriginalTitle = OriginalTitle ?? string.Empty,
                OriginalBody = OriginalBody ?? string.Empty,
                Dirty = Dirty,
                LastFailure = LastFailure ?? string.Empty
            };
        }
    }

    internal sealed class UserNotesTextLine
    {
        public string Text { get; set; }
        public int StartIndex { get; set; }
        public int Length { get; set; }
    }
}
