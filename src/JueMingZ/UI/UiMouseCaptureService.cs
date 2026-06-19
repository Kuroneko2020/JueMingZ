using JueMingZ.Compat;

namespace JueMingZ.UI
{
    public static class UiMouseCaptureService
    {
        private static readonly object SyncRoot = new object();
        private static UiInputFrameKey _lastCaptureFrameKey;
        private static UiInputFrameKey _lastSuppressFrameKey;
        private static bool _lastCaptureFrameKeyValid;
        private static bool _lastSuppressFrameKeyValid;
        private static bool _captureActive;
        private static bool _lastCaptureSucceeded;
        private static bool _lastSuppressSucceeded;

        public static bool CaptureForOperationWindow()
        {
            // Capture marks vanilla UI ownership for this frame; it is not permission
            // to execute a gameplay action.
            return CaptureForOperationWindowCore(false);
        }

        public static bool CaptureForOperationWindowPreserveMouseButtons()
        {
            // Dragging UI controls must keep the physical held state available to
            // the owner while still marking Terraria's UI capture flags.
            return CaptureForOperationWindowCore(true);
        }

        private static bool CaptureForOperationWindowCore(bool preserveMouseButtons)
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                InvalidateCache();
                return false;
            }

            var frameKey = UiInputFrameClock.CurrentFrameKey;
            lock (SyncRoot)
            {
                if (_captureActive &&
                    _lastCaptureSucceeded &&
                    frameKey.IsValid &&
                    _lastCaptureFrameKeyValid &&
                    _lastCaptureFrameKey.Equals(frameKey))
                {
                    return true;
                }
            }

            var captured = preserveMouseButtons
                ? TerrariaUiMouseCompat.TryMarkUiMouseCapturePreserveButtonsForUi()
                : TerrariaUiMouseCompat.TryMarkUiMouseCapture();
            var suppressed = TerrariaUiMouseCompat.TrySuppressMouseText();
            lock (SyncRoot)
            {
                _lastCaptureFrameKey = frameKey;
                _lastSuppressFrameKey = frameKey;
                _lastCaptureFrameKeyValid = frameKey.IsValid;
                _lastSuppressFrameKeyValid = frameKey.IsValid;
                _captureActive = captured;
                _lastCaptureSucceeded = captured;
                _lastSuppressSucceeded = suppressed;
            }
            return captured;
        }

        public static bool SuppressMouseTextForOperationWindow()
        {
            return SuppressPendingMouseTextForOperationWindow();
        }

        public static bool SuppressPendingMouseTextForOperationWindow()
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            var frameKey = UiInputFrameClock.CurrentFrameKey;
            lock (SyncRoot)
            {
                if (_captureActive &&
                    _lastSuppressSucceeded &&
                    frameKey.IsValid &&
                    _lastSuppressFrameKeyValid &&
                    _lastSuppressFrameKey.Equals(frameKey))
                {
                    return true;
                }
            }

            var suppressed = TerrariaUiMouseCompat.TrySuppressPendingMouseTextForUi();
            lock (SyncRoot)
            {
                _lastSuppressFrameKey = frameKey;
                _lastSuppressFrameKeyValid = frameKey.IsValid;
                _lastSuppressSucceeded = suppressed;
            }
            return suppressed;
        }

        public static bool ReleaseForOperationWindow()
        {
            var released = TerrariaUiMouseCompat.TryReleaseUiMouseCapture();
            lock (SyncRoot)
            {
                _captureActive = false;
                _lastCaptureSucceeded = false;
                _lastSuppressSucceeded = false;
                _lastCaptureFrameKey = UiInputFrameKey.None;
                _lastSuppressFrameKey = UiInputFrameKey.None;
                _lastCaptureFrameKeyValid = false;
                _lastSuppressFrameKeyValid = false;
            }
            return released;
        }

        public static bool ConsumeMouseTriggerForOperationWindow(string triggerToken, out string message)
        {
            // One-shot UI command cleanup only: this consumes the click that
            // activated a Legacy UI control and lets vanilla own later input.
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                InvalidateCache();
                message = "UI mouse trigger consume skipped because game input is unavailable.";
                return false;
            }

            var consumed = TerrariaUiMouseCompat.TryConsumeMouseTriggerInput(triggerToken, out message);
            lock (SyncRoot)
            {
                _captureActive = false;
                _lastCaptureSucceeded = false;
                _lastSuppressSucceeded = false;
                _lastCaptureFrameKey = UiInputFrameKey.None;
                _lastSuppressFrameKey = UiInputFrameKey.None;
                _lastCaptureFrameKeyValid = false;
                _lastSuppressFrameKeyValid = false;
            }

            return consumed;
        }

        public static bool ConsumeScrollForOperationWindow()
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                InvalidateCache();
                return false;
            }

            return TerrariaUiMouseCompat.TryConsumeUiScroll();
        }

        public static void InvalidateCache()
        {
            lock (SyncRoot)
            {
                _captureActive = false;
                _lastCaptureSucceeded = false;
                _lastSuppressSucceeded = false;
                _lastCaptureFrameKey = UiInputFrameKey.None;
                _lastSuppressFrameKey = UiInputFrameKey.None;
                _lastCaptureFrameKeyValid = false;
                _lastSuppressFrameKeyValid = false;
            }
        }
    }
}
