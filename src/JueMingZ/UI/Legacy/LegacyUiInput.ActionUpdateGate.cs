using System;

namespace JueMingZ.UI.Legacy
{
    internal sealed class LegacyUiActionUpdateGateSnapshot
    {
        public bool WindowVisible { get; private set; }
        public int PendingCommandCount { get; private set; }
        public string ActiveMode { get; private set; }
        public bool HasActiveInteraction { get; private set; }
        public bool HasActiveSlider { get; private set; }
        public bool HasPendingSlider { get; private set; }
        public bool HasFocusedTextInput { get; private set; }
        public bool HasFocusedHexInput { get; private set; }
        public bool HasScrollNeed { get; private set; }
        public bool HasCaptureNeed { get; private set; }

        public bool NeedsActionUpdateThisFrame
        {
            get
            {
                // Wake Runtime dispatch only for pending commands or active UI ownership
                // so idle frames stay off the command path.
                return PendingCommandCount > 0 ||
                       HasActiveInteraction ||
                       HasScrollNeed ||
                       HasCaptureNeed;
            }
        }

        public bool HasWindowDragOrResizeInteraction
        {
            get
            {
                return string.Equals(ActiveMode, "drag", StringComparison.Ordinal) ||
                       string.Equals(ActiveMode, "resize", StringComparison.Ordinal);
            }
        }

        public LegacyUiActionUpdateGateSnapshot(
            bool windowVisible,
            int pendingCommandCount,
            string activeMode,
            bool hasActiveInteraction,
            bool hasActiveSlider,
            bool hasPendingSlider,
            bool hasFocusedTextInput,
            bool hasFocusedHexInput,
            bool hasScrollNeed,
            bool hasCaptureNeed)
        {
            WindowVisible = windowVisible;
            PendingCommandCount = pendingCommandCount;
            ActiveMode = activeMode ?? string.Empty;
            HasActiveInteraction = hasActiveInteraction;
            HasActiveSlider = hasActiveSlider;
            HasPendingSlider = hasPendingSlider;
            HasFocusedTextInput = hasFocusedTextInput;
            HasFocusedHexInput = hasFocusedHexInput;
            HasScrollNeed = hasScrollNeed;
            HasCaptureNeed = hasCaptureNeed;
        }
    }

    public static partial class LegacyUiInput
    {
        private static long _commandCoalescedCount;

        public static long CommandCoalescedCount
        {
            get { lock (SyncRoot) { return _commandCoalescedCount; } }
        }

        internal static LegacyUiActionUpdateGateSnapshot GetActionUpdateGateSnapshot()
        {
            int pendingCommandCount;
            string activeMode;
            bool hasActiveSlider;
            bool hasPendingSlider;
            bool hasScrollNeed;
            bool hasCaptureNeed;

            lock (SyncRoot)
            {
                pendingCommandCount = PendingCommands.Count;
                activeMode = _activeMode ?? string.Empty;
                hasActiveSlider =
                    string.Equals(_activeMode, "slider", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(_activeSliderId);
                hasPendingSlider = !string.IsNullOrWhiteSpace(_pendingSliderId);
                hasScrollNeed =
                    _pendingUiScrollDelta != 0 ||
                    _wheelConsumedThisFrame ||
                    _lastPlayerInputScrollDelta != 0 ||
                    _lastMainScrollDelta != 0;
                hasCaptureNeed =
                    _lastHoverInWindow ||
                    _lastPlayerInputCleared ||
                    _lastMainScrollSuppressed ||
                    _lastScrollHotbarHookSuppressed;
            }

            var hasFocusedTextInput = LegacyTextInput.IsAnyFocused;
            var hasFocusedHexInput = LegacyHexColorInput.IsAnyFocused;
            var hasActiveInteraction =
                !string.IsNullOrWhiteSpace(activeMode) ||
                hasFocusedTextInput ||
                hasFocusedHexInput;

            return new LegacyUiActionUpdateGateSnapshot(
                LegacyMainUiState.Visible,
                pendingCommandCount,
                activeMode,
                hasActiveInteraction,
                hasActiveSlider,
                hasPendingSlider,
                hasFocusedTextInput,
                hasFocusedHexInput,
                hasScrollNeed,
                hasCaptureNeed);
        }

        internal static void ResetActionUpdateGateStateForTesting()
        {
            lock (SyncRoot)
            {
                PendingCommands.Clear();
                _commandCoalescedCount = 0;
                _pendingUiScrollDelta = 0;
                _wheelConsumedThisFrame = false;
                _lastPlayerInputScrollDelta = 0;
                _lastMainScrollDelta = 0;
                _lastPlayerInputCleared = false;
                _lastMainScrollSuppressed = false;
                _lastScrollHotbarHookSuppressed = false;
                _lastHoverInWindow = false;
                _scrollSnapshotSkippedCount = 0;
                _scrollEventCoalescedCount = 0;
                _lastScrollActionEventUtc = DateTime.MinValue;
                _lastScrollActionEventSignature = string.Empty;
            }
        }
    }
}
