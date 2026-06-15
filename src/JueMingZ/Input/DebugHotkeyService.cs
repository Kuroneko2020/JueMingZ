using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static class DebugHotkeyService
    {
        private const int VkF5 = 0x74;
        private static readonly object SyncRoot = new object();
        private static bool _wasDown;
        private static F5HotkeyDiagnosticSnapshot _lastF5Hotkey = new F5HotkeyDiagnosticSnapshot();

        public static void Update()
        {
            try
            {
                var physicalDown = (GetAsyncKeyState(VkF5) & 0x8000) != 0;
                var inputAvailable = TerrariaMainCompat.AllowsInputProcessing;
                var mapFullscreenOpen = TerrariaMainCompat.IsMapFullscreenOpen;
                var foreground = IsCurrentProcessForeground();
                var now = DateTime.UtcNow;
                var decision = EvaluateF5HotkeyForTesting(
                    physicalDown,
                    _wasDown,
                    inputAvailable,
                    mapFullscreenOpen,
                    foreground,
                    now,
                    DateTime.MinValue);
                _wasDown = decision.NextWasDown;

                if (decision.RecordDiagnostic)
                {
                    RecordF5HotkeyDecision(decision);
                }

                if (decision.ShouldToggle)
                {
                    // The F5 hotkey owns only the Legacy UI visibility edge; it
                    // must not rely on a second press after focus/input gates recover.
                    if (LegacyMainUiState.HideIfMainMenu("F5"))
                    {
                        RecordF5HotkeyDecision(decision.WithSkippedReason("mainMenu"));
                        Logger.Info("DebugHotkeyService", "Legacy main UI toggle ignored on Terraria main menu.");
                        return;
                    }

                    if (TerrariaMainCompat.IsMapFullscreenOpen)
                    {
                        RecordF5HotkeyDecision(decision.WithSkippedReason("mapFullscreen"));
                        Logger.Info("DebugHotkeyService", "Legacy main UI toggle ignored while fullscreen map is open.");
                        return;
                    }

                    var visible = LegacyMainUiState.ToggleVisible();
                    RecordF5HotkeyDecision(decision.WithAcceptedReason(visible ? "opened" : "closed"));
                    Logger.Info("DebugHotkeyService", "Legacy main UI toggled: visible=" + visible);
                }
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "debug-hotkey-update-error",
                    TimeSpan.FromSeconds(10),
                    "DebugHotkeyService",
                    "Debug hotkey update failed.", error);
            }
        }

        public static F5HotkeyDiagnosticSnapshot GetF5HotkeySnapshot()
        {
            lock (SyncRoot)
            {
                return _lastF5Hotkey.Clone();
            }
        }

        internal static F5HotkeyDecision EvaluateF5HotkeyForTesting(
            bool physicalDown,
            bool wasDown,
            bool inputAvailable,
            bool mapFullscreenOpen,
            bool foreground,
            DateTime nowUtc,
            DateTime lastToggleUtc)
        {
            // A physical release already arms the next F5 edge; a fixed wall-clock
            // debounce made deliberate rapid open/close presses feel lost.
            const int debounceRemainingMs = 0;
            if (!physicalDown)
            {
                return new F5HotkeyDecision(
                    false,
                    wasDown ? "released" : "idle",
                    physicalDown,
                    wasDown,
                    debounceRemainingMs,
                    nowUtc,
                    false,
                    wasDown);
            }

            if (wasDown)
            {
                return new F5HotkeyDecision(
                    false,
                    "held",
                    physicalDown,
                    wasDown,
                    debounceRemainingMs,
                    nowUtc,
                    true,
                    false);
            }

            if (mapFullscreenOpen)
            {
                return new F5HotkeyDecision(
                    false,
                    "mapFullscreen",
                    physicalDown,
                    wasDown,
                    debounceRemainingMs,
                    nowUtc,
                    true,
                    true);
            }

            if (!inputAvailable)
            {
                return new F5HotkeyDecision(
                    false,
                    "gameInputUnavailable",
                    physicalDown,
                    wasDown,
                    debounceRemainingMs,
                    nowUtc,
                    true,
                    true);
            }

            if (!foreground)
            {
                return new F5HotkeyDecision(
                    false,
                    "notForeground",
                    physicalDown,
                    wasDown,
                    debounceRemainingMs,
                    nowUtc,
                    true,
                    true);
            }

            return new F5HotkeyDecision(
                true,
                "pressed",
                physicalDown,
                wasDown,
                0,
                nowUtc,
                true,
                true);
        }

        private static void RecordF5HotkeyDecision(F5HotkeyDecision decision)
        {
            lock (SyncRoot)
            {
                _lastF5Hotkey = new F5HotkeyDiagnosticSnapshot
                {
                    Decision = decision.ShouldToggle ? "accepted" : "skipped",
                    Reason = decision.Reason ?? string.Empty,
                    Down = decision.Down,
                    WasDown = decision.WasDown,
                    DebounceRemainingMs = decision.DebounceRemainingMs,
                    Utc = decision.Utc
                };
            }
        }

        private static bool IsCurrentProcessForeground()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    return true;
                }

                int processId;
                GetWindowThreadProcessId(foregroundWindow, out processId);
                return processId == Process.GetCurrentProcess().Id;
            }
            catch
            {
                return true;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr windowHandle, out int processId);
    }

    public sealed class F5HotkeyDiagnosticSnapshot
    {
        public string Decision { get; set; }
        public string Reason { get; set; }
        public bool Down { get; set; }
        public bool WasDown { get; set; }
        public int DebounceRemainingMs { get; set; }
        public DateTime? Utc { get; set; }

        public F5HotkeyDiagnosticSnapshot()
        {
            Decision = string.Empty;
            Reason = string.Empty;
        }

        public F5HotkeyDiagnosticSnapshot Clone()
        {
            return new F5HotkeyDiagnosticSnapshot
            {
                Decision = Decision ?? string.Empty,
                Reason = Reason ?? string.Empty,
                Down = Down,
                WasDown = WasDown,
                DebounceRemainingMs = DebounceRemainingMs,
                Utc = Utc
            };
        }
    }

    internal sealed class F5HotkeyDecision
    {
        public bool ShouldToggle { get; private set; }
        public string Reason { get; private set; }
        public bool Down { get; private set; }
        public bool WasDown { get; private set; }
        public int DebounceRemainingMs { get; private set; }
        public DateTime Utc { get; private set; }
        public bool NextWasDown { get; private set; }
        public bool RecordDiagnostic { get; private set; }

        public F5HotkeyDecision(
            bool shouldToggle,
            string reason,
            bool down,
            bool wasDown,
            int debounceRemainingMs,
            DateTime utc,
            bool nextWasDown,
            bool recordDiagnostic)
        {
            ShouldToggle = shouldToggle;
            Reason = reason ?? string.Empty;
            Down = down;
            WasDown = wasDown;
            DebounceRemainingMs = debounceRemainingMs;
            Utc = utc;
            NextWasDown = nextWasDown;
            RecordDiagnostic = recordDiagnostic;
        }

        public F5HotkeyDecision WithSkippedReason(string reason)
        {
            return new F5HotkeyDecision(
                false,
                reason,
                Down,
                WasDown,
                DebounceRemainingMs,
                Utc,
                NextWasDown,
                true);
        }

        public F5HotkeyDecision WithAcceptedReason(string reason)
        {
            return new F5HotkeyDecision(
                true,
                reason,
                Down,
                WasDown,
                DebounceRemainingMs,
                Utc,
                NextWasDown,
                true);
        }
    }
}
