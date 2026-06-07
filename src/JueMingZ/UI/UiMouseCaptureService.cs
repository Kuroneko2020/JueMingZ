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

            var captured = TerrariaUiMouseCompat.TryMarkUiMouseCapture();
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

            var suppressed = TerrariaUiMouseCompat.TrySuppressMouseText();
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
