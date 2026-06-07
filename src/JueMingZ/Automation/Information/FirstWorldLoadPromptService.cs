using System;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Records;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Information
{
    // First-world prompts are UI state only; progress reads must not trigger scans, world edits, or automation actions.
    internal static class FirstWorldLoadPromptService
    {
        private const string PromptText = "按f5打开配置窗口";
        private static readonly TimeSpan PromptReadableDuration = TimeSpan.FromSeconds(3d);
        private static readonly TimeSpan PromptFadeDuration = TimeSpan.FromSeconds(0.5d);
        private static readonly object SyncRoot = new object();
        private static string _text = string.Empty;
        private static DateTime _shownUtc = DateTime.MinValue;
        private static DateTime _expiresUtc = DateTime.MinValue;
        private static string _lastCheckedPlayer = string.Empty;

        public static void Tick(GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            try
            {
                InformationWorldContext context;
                if (!InformationWorldContextProvider.TryBuild(out context, out _))
                {
                    ResetState();
                    return;
                }

                if (string.IsNullOrWhiteSpace(context.PlayerRecordKey))
                {
                    ResetState();
                    return;
                }

                var playerKey = context.PlayerRecordKey ?? string.Empty;
                if (string.Equals(_lastCheckedPlayer, playerKey, StringComparison.Ordinal))
                {
                    return;
                }

                _lastCheckedPlayer = playerKey;

                bool firstVisit;
                if (!PlayerWorldBehaviorStore.TryMarkPlayerFirstLoad(BuildBehaviorContext(context), out firstVisit, out _))
                {
                    return;
                }

                if (!firstVisit)
                {
                    return;
                }

                Show(playerKey: playerKey, tick: runtimeState == null ? 0 : runtimeState.UpdateCount);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("FirstWorldLoadPromptService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "first-world-load-prompt-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "FirstWorldLoadPromptService",
                    "First world load prompt tick failed; exception swallowed.",
                    error);
            }
        }

        public static bool TryGetPrompt(out string text, out double progress, out double alpha)
        {
            lock (SyncRoot)
            {
                text = _text;
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
                var readableDuration = Math.Max(1d, PromptReadableDuration.TotalMilliseconds);
                progress = Math.Max(0d, Math.Min(1d, elapsed / readableDuration));
                alpha = CalculatePromptAlpha(elapsed);
                return true;
            }
        }

        private static void Show(string playerKey, long tick)
        {
            lock (SyncRoot)
            {
                _text = PromptText;
                _shownUtc = DateTime.UtcNow;
                _expiresUtc = _shownUtc + PromptReadableDuration + PromptFadeDuration;
            }

            Logger.Info(
                "FirstWorldLoadPromptService",
                "First world load prompt shown for player " +
                playerKey +
                ", tick=" +
                tick.ToString());
        }

        private static double CalculatePromptAlpha(double elapsedMs)
        {
            if (elapsedMs <= PromptReadableDuration.TotalMilliseconds)
            {
                return 1d;
            }

            var fadeMs = Math.Max(1d, PromptFadeDuration.TotalMilliseconds);
            var fadeProgress = (elapsedMs - PromptReadableDuration.TotalMilliseconds) / fadeMs;
            return Math.Max(0d, Math.Min(1d, 1d - fadeProgress));
        }

        private static void ResetState()
        {
            lock (SyncRoot)
            {
                _lastCheckedPlayer = string.Empty;
            }
        }

        private static PlayerWorldBehaviorContext BuildBehaviorContext(InformationWorldContext context)
        {
            return context == null
                ? new PlayerWorldBehaviorContext()
                : new PlayerWorldBehaviorContext
                {
                    PlayerKey = context.PlayerRecordKey ?? string.Empty,
                    WorldKey = context.WorldRecordKey ?? string.Empty,
                    PlayerName = context.PlayerName ?? string.Empty,
                    WorldName = context.WorldName ?? string.Empty
                };
        }
    }
}
