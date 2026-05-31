using System;

namespace JueMingZ.Automation.Fishing
{
    internal static class FishingStatusPromptService
    {
        private static readonly object SyncRoot = new object();
        private static readonly TimeSpan Duration = TimeSpan.FromSeconds(1.6d);
        private static string _text = string.Empty;
        private static bool _startPrompt;
        private static DateTime _shownUtc = DateTime.MinValue;
        private static DateTime _expiresUtc = DateTime.MinValue;

        public static void ShowStart(long tick, bool truffleWormBait)
        {
            Show(truffleWormBait ? "开始鲨猪" : "开始钓鱼", true);
        }

        public static void ShowStop(long tick, bool fishronHooked)
        {
            Show(fishronHooked ? "鲨猪啦！" : "停止钓鱼", false);
        }

        public static bool TryGetPrompt(out string text, out bool startPrompt, out double progress, out double alpha)
        {
            lock (SyncRoot)
            {
                text = _text;
                startPrompt = _startPrompt;
                progress = 0d;
                alpha = 0d;
                if (string.IsNullOrWhiteSpace(_text))
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                if (now >= _expiresUtc)
                {
                    _text = string.Empty;
                    return false;
                }

                var elapsed = (now - _shownUtc).TotalMilliseconds;
                var duration = Math.Max(1d, Duration.TotalMilliseconds);
                progress = Math.Max(0d, Math.Min(1d, elapsed / duration));
                var remaining = Math.Max(0d, Math.Min(1d, (_expiresUtc - now).TotalMilliseconds / duration));
                alpha = Math.Min(1d, remaining * 2.2d);
                return true;
            }
        }

        private static void Show(string text, bool startPrompt)
        {
            lock (SyncRoot)
            {
                _text = text ?? string.Empty;
                _startPrompt = startPrompt;
                _shownUtc = DateTime.UtcNow;
                _expiresUtc = _shownUtc + Duration;
            }
        }
    }
}
