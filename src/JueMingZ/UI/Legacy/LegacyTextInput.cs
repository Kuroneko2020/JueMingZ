using System;
using JueMingZ.Compat;

namespace JueMingZ.UI.Legacy
{
    internal static class LegacyTextInput
    {
        private const int MaxTextLength = 48;
        private const float ImePanelAnchorVerticalOffset = 32f;
        private static readonly object SyncRoot = new object();
        private static string _activeId = string.Empty;
        private static string _draft = string.Empty;
        private static string _compositionPreview = string.Empty;
        private static string _diagnosticMessage = string.Empty;

        public static bool IsAnyFocused
        {
            get { lock (SyncRoot) { return !string.IsNullOrWhiteSpace(_activeId); } }
        }

        public static int DraftLength
        {
            get { lock (SyncRoot) { return _draft == null ? 0 : _draft.Length; } }
        }

        public static string DiagnosticMessage
        {
            get { lock (SyncRoot) { return _diagnosticMessage ?? string.Empty; } }
        }

        public static void Focus(string id, string currentText)
        {
            lock (SyncRoot)
            {
                _activeId = id ?? string.Empty;
                _draft = NormalizeText(currentText);
                _compositionPreview = string.Empty;
                _diagnosticMessage = string.Empty;
            }
        }

        public static void ClearFocus()
        {
            lock (SyncRoot)
            {
                _activeId = string.Empty;
                _draft = string.Empty;
                _compositionPreview = string.Empty;
                _diagnosticMessage = string.Empty;
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

        public static string GetDraft(string id)
        {
            lock (SyncRoot)
            {
                return IsFocusedLocked(id) ? _draft ?? string.Empty : string.Empty;
            }
        }

        public static string GetDisplayText(string id, string placeholder)
        {
            lock (SyncRoot)
            {
                if (!IsFocusedLocked(id))
                {
                    return placeholder ?? string.Empty;
                }

                var text = _draft ?? string.Empty;
                var composition = _compositionPreview ?? string.Empty;
                return text + composition + (ShowCaret() ? "|" : string.Empty);
            }
        }

        public static bool Update(string id)
        {
            lock (SyncRoot)
            {
                if (!IsFocusedLocked(id))
                {
                    return false;
                }

                string next;
                string message;
                if (!TerrariaTextInputCompat.TryGetInputText(_draft, out next, out message))
                {
                    _diagnosticMessage = "原生文本输入不可用，无法安全输入中文。";
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        _diagnosticMessage += " " + message;
                    }

                    return false;
                }

                next = NormalizeText(next);
                var changed = !string.Equals(_draft, next, StringComparison.Ordinal);
                _draft = next;
                RefreshCompositionPreviewLocked();
                return changed;
            }
        }

        public static void BeginImeCompositionPanelFrame()
        {
            TerrariaTextInputCompat.BeginImePanelFrame();
        }

        public static bool TryDetachImeCompositionPanel()
        {
            TerrariaTextInputCompat.BeginImePanelFrame();
            return true;
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

            string message;
            // Terraria draws the native IME candidate strip 32 px above the anchor.
            // Anchoring at the input bottom would overlap the text box itself.
            var attached = TerrariaTextInputCompat.TrySetImePanelAnchor(
                anchor.X,
                anchor.Bottom + ImePanelAnchorVerticalOffset,
                0f,
                out message);
            if (!attached)
            {
                SetDiagnosticMessage("原生 IME 候选面板不可用。 " + message);
            }

            return attached;
        }

        public static bool TryDrawImeCompositionPanelTail(bool spriteBatchBegun)
        {
            string message;
            var drawn = TerrariaTextInputCompat.TryDrawImePanelAfterSpriteBatchEnded(spriteBatchBegun, out message);
            if (!drawn)
            {
                SetDiagnosticMessage("原生 IME 候选面板不可用。 " + message);
            }

            return drawn;
        }

        internal static string GetCompositionPreviewForTesting(string id)
        {
            lock (SyncRoot)
            {
                return IsFocusedLocked(id) ? _compositionPreview ?? string.Empty : string.Empty;
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

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var text = value.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("\t", " ");
            return text.Length > MaxTextLength ? text.Substring(0, MaxTextLength) : text;
        }

        private static void RefreshCompositionPreviewLocked()
        {
            string composition;
            string message;
            if (TerrariaTextInputCompat.TryGetImeCompositionString(out composition, out message))
            {
                _compositionPreview = NormalizeText(composition);
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

        private static bool ShowCaret()
        {
            return (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond / 500) % 2 == 0;
        }
    }
}
