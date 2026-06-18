using System;
using JueMingZ.Compat;
using Microsoft.Xna.Framework.Input;

namespace JueMingZ.UI.Legacy
{
    internal static class LegacyMultilineTextInput
    {
        private static readonly object SyncRoot = new object();
        private static string _activeId = string.Empty;
        private static string _draft = string.Empty;
        private static int _cursorIndex;
        private static string _compositionPreview = string.Empty;
        private static string _diagnosticMessage = string.Empty;
        private static KeyboardState _lastKeyboardState;
        private static bool _lastKeyboardStateValid;

        public static bool IsAnyFocused
        {
            get { lock (SyncRoot) { return !string.IsNullOrWhiteSpace(_activeId); } }
        }

        public static string DiagnosticMessage
        {
            get { lock (SyncRoot) { return _diagnosticMessage ?? string.Empty; } }
        }

        public static void Focus(string id, string currentText, int cursorIndex)
        {
            lock (SyncRoot)
            {
                _activeId = id ?? string.Empty;
                _draft = NormalizeText(currentText, true, 0);
                _cursorIndex = Clamp(cursorIndex, 0, _draft.Length);
                _compositionPreview = string.Empty;
                _diagnosticMessage = string.Empty;
                _lastKeyboardStateValid = TryReadKeyboardState(out _lastKeyboardState);
            }
        }

        public static void ClearFocus()
        {
            lock (SyncRoot)
            {
                _activeId = string.Empty;
                _draft = string.Empty;
                _cursorIndex = 0;
                _compositionPreview = string.Empty;
                _diagnosticMessage = string.Empty;
                _lastKeyboardStateValid = false;
            }

            TerrariaTextInputCompat.EndTextInput();
            TerrariaTextInputCompat.BeginImePanelFrame();
        }

        public static bool IsFocused(string id)
        {
            lock (SyncRoot)
            {
                return IsFocusedLocked(id);
            }
        }

        public static LegacyTextEditorSnapshot GetSnapshot(string id)
        {
            lock (SyncRoot)
            {
                if (!IsFocusedLocked(id))
                {
                    return new LegacyTextEditorSnapshot();
                }

                return BuildSnapshotLocked();
            }
        }

        public static LegacyTextEditorUpdateResult Update(string id, bool allowNewLine, int maxLength)
        {
            lock (SyncRoot)
            {
                if (!IsFocusedLocked(id))
                {
                    return LegacyTextEditorUpdateResult.None;
                }

                var result = new LegacyTextEditorUpdateResult();
                string next;
                string message;
                TextInputControlState controlState;
                if (!TerrariaTextInputCompat.TryGetInputText(_draft, allowNewLine, out next, out controlState, out message))
                {
                    _diagnosticMessage = "原生文本输入不可用，无法安全输入中文。";
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        _diagnosticMessage += " " + message;
                    }

                    return result;
                }

                next = NormalizeText(next, allowNewLine, maxLength);
                result.Changed = ApplyNativeDeltaLocked(next, allowNewLine, maxLength);

                if (controlState.EscapePressed)
                {
                    result.CancelRequested = true;
                }

                if (controlState.EnterPressed)
                {
                    if (allowNewLine)
                    {
                        InsertTextLocked("\n", allowNewLine, maxLength);
                        result.Changed = true;
                    }
                    else
                    {
                        result.SubmitRequested = true;
                    }
                }

                var keyboardChanged = ApplyKeyboardEdgesLocked(allowNewLine, maxLength);
                result.Changed = result.Changed || keyboardChanged;
                RefreshCompositionPreviewLocked(allowNewLine, maxLength);
                return result;
            }
        }

        public static void SetCursorIndex(string id, int cursorIndex)
        {
            lock (SyncRoot)
            {
                if (IsFocusedLocked(id))
                {
                    _cursorIndex = Clamp(cursorIndex, 0, _draft == null ? 0 : _draft.Length);
                }
            }
        }

        public static bool TryAttachImeCompositionPanel(string id, LegacyUiRect anchor)
        {
            if (anchor.Width <= 0 || anchor.Height <= 0)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (!IsFocusedLocked(id))
                {
                    return false;
                }
            }

            return LegacyTextInput.TryAttachFocusedImeCompositionPanel(anchor, SetDiagnosticMessage);
        }

        internal static LegacyTextEditorUpdateResult ApplyControlForTesting(
            string id,
            bool allowNewLine,
            int maxLength,
            TextInputControlState controlState)
        {
            lock (SyncRoot)
            {
                if (!IsFocusedLocked(id))
                {
                    return LegacyTextEditorUpdateResult.None;
                }

                var result = new LegacyTextEditorUpdateResult();
                if (controlState.EscapePressed)
                {
                    result.CancelRequested = true;
                }

                if (controlState.EnterPressed)
                {
                    if (allowNewLine)
                    {
                        InsertTextLocked("\n", allowNewLine, maxLength);
                        result.Changed = true;
                    }
                    else
                    {
                        result.SubmitRequested = true;
                    }
                }

                return result;
            }
        }

        internal static void InsertTextForTesting(string id, string text, bool allowNewLine, int maxLength)
        {
            lock (SyncRoot)
            {
                if (IsFocusedLocked(id))
                {
                    InsertTextLocked(text, allowNewLine, maxLength);
                }
            }
        }

        private static LegacyTextEditorSnapshot BuildSnapshotLocked()
        {
            var draft = _draft ?? string.Empty;
            var composition = _compositionPreview ?? string.Empty;
            var cursor = Clamp(_cursorIndex, 0, draft.Length);
            var display = draft.Insert(cursor, composition + (ShowCaret() ? "|" : string.Empty));
            return new LegacyTextEditorSnapshot
            {
                Draft = draft,
                CursorIndex = cursor,
                CompositionPreview = composition,
                DisplayText = display,
                DiagnosticMessage = _diagnosticMessage ?? string.Empty
            };
        }

        private static bool ApplyNativeDeltaLocked(string nativeNext, bool allowNewLine, int maxLength)
        {
            var draft = _draft ?? string.Empty;
            nativeNext = NormalizeText(nativeNext, allowNewLine, maxLength);
            if (string.Equals(nativeNext, draft, StringComparison.Ordinal))
            {
                return false;
            }

            if (nativeNext.StartsWith(draft, StringComparison.Ordinal))
            {
                InsertTextLocked(nativeNext.Substring(draft.Length), allowNewLine, maxLength);
                return true;
            }

            if (draft.Length > 0 &&
                string.Equals(nativeNext, draft.Substring(0, draft.Length - 1), StringComparison.Ordinal))
            {
                DeleteBeforeCursorLocked();
                return true;
            }

            _draft = nativeNext;
            _cursorIndex = Clamp(_cursorIndex, 0, _draft.Length);
            return true;
        }

        private static bool ApplyKeyboardEdgesLocked(bool allowNewLine, int maxLength)
        {
            KeyboardState state;
            if (!TryReadKeyboardState(out state))
            {
                return false;
            }

            var changed = false;
            if (WasPressed(state, Keys.Left))
            {
                _cursorIndex = Clamp(_cursorIndex - 1, 0, (_draft ?? string.Empty).Length);
            }
            else if (WasPressed(state, Keys.Right))
            {
                _cursorIndex = Clamp(_cursorIndex + 1, 0, (_draft ?? string.Empty).Length);
            }
            else if (WasPressed(state, Keys.Home))
            {
                _cursorIndex = FindLineStart(_draft, _cursorIndex);
            }
            else if (WasPressed(state, Keys.End))
            {
                _cursorIndex = FindLineEnd(_draft, _cursorIndex);
            }
            else if (WasPressed(state, Keys.Up))
            {
                _cursorIndex = MoveCursorVertical(_draft, _cursorIndex, -1);
            }
            else if (WasPressed(state, Keys.Down))
            {
                _cursorIndex = MoveCursorVertical(_draft, _cursorIndex, 1);
            }

            if (WasPressed(state, Keys.Delete))
            {
                changed = DeleteAtCursorLocked();
            }

            _lastKeyboardState = state;
            _lastKeyboardStateValid = true;
            return changed;
        }

        private static bool WasPressed(KeyboardState state, Keys key)
        {
            return state.IsKeyDown(key) &&
                   (!_lastKeyboardStateValid || !_lastKeyboardState.IsKeyDown(key));
        }

        private static bool TryReadKeyboardState(out KeyboardState state)
        {
            try
            {
                state = Keyboard.GetState();
                return true;
            }
            catch
            {
                state = new KeyboardState();
                return false;
            }
        }

        private static void InsertTextLocked(string text, bool allowNewLine, int maxLength)
        {
            text = NormalizeText(text, allowNewLine, maxLength <= 0 ? 0 : maxLength);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var draft = _draft ?? string.Empty;
            var cursor = Clamp(_cursorIndex, 0, draft.Length);
            var next = draft.Substring(0, cursor) + text + draft.Substring(cursor);
            _draft = NormalizeText(next, allowNewLine, maxLength);
            _cursorIndex = Clamp(cursor + text.Length, 0, _draft.Length);
        }

        private static void DeleteBeforeCursorLocked()
        {
            var draft = _draft ?? string.Empty;
            if (_cursorIndex <= 0 || draft.Length <= 0)
            {
                return;
            }

            var cursor = Clamp(_cursorIndex, 0, draft.Length);
            _draft = draft.Substring(0, cursor - 1) + draft.Substring(cursor);
            _cursorIndex = cursor - 1;
        }

        private static bool DeleteAtCursorLocked()
        {
            var draft = _draft ?? string.Empty;
            var cursor = Clamp(_cursorIndex, 0, draft.Length);
            if (cursor >= draft.Length)
            {
                return false;
            }

            _draft = draft.Substring(0, cursor) + draft.Substring(cursor + 1);
            _cursorIndex = cursor;
            return true;
        }

        private static int MoveCursorVertical(string text, int cursorIndex, int direction)
        {
            text = text ?? string.Empty;
            cursorIndex = Clamp(cursorIndex, 0, text.Length);
            var currentStart = FindLineStart(text, cursorIndex);
            var currentColumn = cursorIndex - currentStart;
            if (direction < 0)
            {
                if (currentStart <= 0)
                {
                    return cursorIndex;
                }

                var previousEnd = currentStart - 1;
                var previousStart = FindLineStart(text, previousEnd);
                return previousStart + Math.Min(currentColumn, Math.Max(0, previousEnd - previousStart));
            }

            var currentEnd = FindLineEnd(text, cursorIndex);
            if (currentEnd >= text.Length)
            {
                return cursorIndex;
            }

            var nextStart = currentEnd + 1;
            var nextEnd = FindLineEnd(text, nextStart);
            return nextStart + Math.Min(currentColumn, Math.Max(0, nextEnd - nextStart));
        }

        private static int FindLineStart(string text, int cursorIndex)
        {
            text = text ?? string.Empty;
            cursorIndex = Clamp(cursorIndex, 0, text.Length);
            for (var index = cursorIndex - 1; index >= 0; index--)
            {
                if (text[index] == '\n')
                {
                    return index + 1;
                }
            }

            return 0;
        }

        private static int FindLineEnd(string text, int cursorIndex)
        {
            text = text ?? string.Empty;
            cursorIndex = Clamp(cursorIndex, 0, text.Length);
            for (var index = cursorIndex; index < text.Length; index++)
            {
                if (text[index] == '\n')
                {
                    return index;
                }
            }

            return text.Length;
        }

        private static void RefreshCompositionPreviewLocked(bool allowNewLine, int maxLength)
        {
            string composition;
            string message;
            if (TerrariaTextInputCompat.TryGetImeCompositionString(out composition, out message))
            {
                _compositionPreview = NormalizeText(composition, allowNewLine, maxLength);
                _diagnosticMessage = string.Empty;
                return;
            }

            _compositionPreview = string.Empty;
            _diagnosticMessage = "原生 IME 组合预览不可用，已降级为提交后显示。";
            if (!string.IsNullOrWhiteSpace(message))
            {
                _diagnosticMessage += " " + message;
            }
        }

        private static void SetDiagnosticMessage(string message)
        {
            lock (SyncRoot)
            {
                _diagnosticMessage = message ?? string.Empty;
            }
        }

        private static bool IsFocusedLocked(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && string.Equals(_activeId, id, StringComparison.Ordinal);
        }

        private static string NormalizeText(string value, bool allowNewLine, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var text = value.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ');
            if (!allowNewLine)
            {
                text = text.Replace("\n", string.Empty);
            }

            return maxLength > 0 && text.Length > maxLength ? text.Substring(0, maxLength) : text;
        }

        private static bool ShowCaret()
        {
            return (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond / 500) % 2 == 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }

    internal sealed class LegacyTextEditorUpdateResult
    {
        public static readonly LegacyTextEditorUpdateResult None = new LegacyTextEditorUpdateResult();

        public bool Changed { get; set; }
        public bool SubmitRequested { get; set; }
        public bool CancelRequested { get; set; }
    }

    internal sealed class LegacyTextEditorSnapshot
    {
        public string Draft { get; set; }
        public int CursorIndex { get; set; }
        public string CompositionPreview { get; set; }
        public string DisplayText { get; set; }
        public string DiagnosticMessage { get; set; }

        public LegacyTextEditorSnapshot()
        {
            Draft = string.Empty;
            CompositionPreview = string.Empty;
            DisplayText = string.Empty;
            DiagnosticMessage = string.Empty;
        }
    }
}
