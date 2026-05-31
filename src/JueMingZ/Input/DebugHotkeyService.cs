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
        private static readonly TimeSpan ToggleDebounce = TimeSpan.FromMilliseconds(300);
        private static bool _wasDown;
        private static DateTime _lastToggleUtc = DateTime.MinValue;

        public static void Update()
        {
            try
            {
                var isDown = TerrariaMainCompat.AllowsInputProcessing && (GetAsyncKeyState(VkF5) & 0x8000) != 0;
                var now = DateTime.UtcNow;
                if (isDown && !_wasDown && now - _lastToggleUtc >= ToggleDebounce && IsCurrentProcessForeground())
                {
                    _lastToggleUtc = now;
                    if (LegacyMainUiState.HideIfMainMenu("F5"))
                    {
                        Logger.Info("DebugHotkeyService", "Legacy main UI toggle ignored on Terraria main menu.");
                        _wasDown = isDown;
                        return;
                    }

                    var visible = LegacyMainUiState.ToggleVisible();
                    Logger.Info("DebugHotkeyService", "Legacy main UI toggled: visible=" + visible);
                }

                _wasDown = isDown;
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
}
