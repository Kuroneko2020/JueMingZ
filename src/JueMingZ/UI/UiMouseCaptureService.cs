using JueMingZ.Compat;

namespace JueMingZ.UI
{
    public static class UiMouseCaptureService
    {
        private static readonly object SyncRoot = new object();
        private static readonly System.TimeSpan CaptureThrottleWindow = System.TimeSpan.FromMilliseconds(8);
        private static readonly System.TimeSpan SuppressThrottleWindow = System.TimeSpan.FromMilliseconds(8);
        private static System.DateTime _lastCaptureUtc = System.DateTime.MinValue;
        private static System.DateTime _lastSuppressUtc = System.DateTime.MinValue;
        private static bool _captureActive;
        private static bool _lastCaptureSucceeded;

        public static bool CaptureForOperationWindow()
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                InvalidateCache();
                return false;
            }

            var now = System.DateTime.UtcNow;
            lock (SyncRoot)
            {
                if (_captureActive &&
                    _lastCaptureSucceeded &&
                    now - _lastCaptureUtc <= CaptureThrottleWindow)
                {
                    return true;
                }
            }

            var captured = TerrariaUiMouseCompat.TryMarkUiMouseCapture();
            TerrariaUiMouseCompat.TrySuppressMouseText();
            lock (SyncRoot)
            {
                _lastCaptureUtc = now;
                _lastSuppressUtc = now;
                _captureActive = captured;
                _lastCaptureSucceeded = captured;
            }
            return captured;
        }

        public static bool SuppressMouseTextForOperationWindow()
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            var now = System.DateTime.UtcNow;
            lock (SyncRoot)
            {
                if (_captureActive &&
                    now - _lastSuppressUtc <= SuppressThrottleWindow)
                {
                    return true;
                }
            }

            var suppressed = TerrariaUiMouseCompat.TrySuppressMouseText();
            lock (SyncRoot)
            {
                _lastSuppressUtc = now;
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
                _lastCaptureUtc = System.DateTime.MinValue;
                _lastSuppressUtc = System.DateTime.MinValue;
            }
            return released;
        }

        public static void InvalidateCache()
        {
            lock (SyncRoot)
            {
                _captureActive = false;
                _lastCaptureSucceeded = false;
                _lastCaptureUtc = System.DateTime.MinValue;
                _lastSuppressUtc = System.DateTime.MinValue;
            }
        }
    }
}
