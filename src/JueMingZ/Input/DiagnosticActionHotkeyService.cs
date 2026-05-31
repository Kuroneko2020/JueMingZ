using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static class DiagnosticActionHotkeyService
    {
        private const int VkControl = 0x11;
        private const int VkAlt = 0x12;
        private const int VkJ = 0x4A;
        private const int VkK = 0x4B;
        private const int VkL = 0x4C;
        private const int VkD = 0x44;
        private const int VkU = 0x55;
        private const int VkT = 0x54;
        private const int VkLeft = 0x25;
        private const int VkRight = 0x27;
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);
        private static readonly Dictionary<int, bool> WasDown = new Dictionary<int, bool>();
        private static readonly List<string> RecentFeedback = new List<string>();
        private static DateTime _lastHotkeyUtc = DateTime.MinValue;
        private static bool _wasDiagnosticsOverlayVisible;

        public static string LastDiagnosticHotkey { get; private set; } = string.Empty;
        public static DateTime? LastDiagnosticHotkeyUtc { get; private set; }
        public static string LastDiagnosticHotkeyMessage { get; private set; } = string.Empty;

        public static void Update(InputActionQueue queue, GameStateSnapshot snapshot)
        {
            if (queue == null)
            {
                return;
            }

            try
            {
                if (!DiagnosticsOverlay.Visible)
                {
                    _wasDiagnosticsOverlayVisible = false;
                    WasDown.Clear();
                    return;
                }

                if (!_wasDiagnosticsOverlayVisible)
                {
                    _wasDiagnosticsOverlayVisible = true;
                    RefreshAllComboStates();
                    return;
                }

                if (LegacyTextInput.IsAnyFocused)
                {
                    RefreshAllComboStates();
                    return;
                }

                if (PressedCombo(VkD))
                {
                    Mark("Ctrl+Alt+D", "备用热键：切换诊断输入。");
                    DiagnosticActionDispatcher.ToggleDiagnosticInput(DiagnosticActionSource.ForHotkey("Ctrl+Alt+D"));
                    return;
                }

                if (PressedCombo(VkLeft))
                {
                    Mark("Ctrl+Alt+←", "备用热键：上一测试格。");
                    DiagnosticActionDispatcher.ChangeDiagnosticTestSlot(-1, snapshot, DiagnosticActionSource.ForHotkey("Ctrl+Alt+←"));
                    return;
                }

                if (PressedCombo(VkRight))
                {
                    Mark("Ctrl+Alt+→", "备用热键：下一测试格。");
                    DiagnosticActionDispatcher.ChangeDiagnosticTestSlot(1, snapshot, DiagnosticActionSource.ForHotkey("Ctrl+Alt+→"));
                    return;
                }

                if (PressedCombo(VkJ))
                {
                    Mark("Ctrl+Alt+J", "备用热键：空动作。");
                    DiagnosticActionDispatcher.EnqueueDiagnosticNoop(queue, DiagnosticActionSource.ForHotkey("Ctrl+Alt+J"));
                }

                if (PressedCombo(VkK))
                {
                    Mark("Ctrl+Alt+K", "备用热键：切到测试格并恢复。");
                    DiagnosticActionDispatcher.EnqueueSelectHotbarSlot(queue, snapshot, DiagnosticActionSource.ForHotkey("Ctrl+Alt+K"));
                }

                if (PressedCombo(VkL))
                {
                    Mark("Ctrl+Alt+L", "备用热键：使用手上物品。");
                    DiagnosticActionDispatcher.EnqueueUseSelectedItem(queue, snapshot, DiagnosticActionSource.ForHotkey("Ctrl+Alt+L"));
                }

                if (PressedCombo(VkU))
                {
                    Mark("Ctrl+Alt+U", "备用热键：使用测试格物品。");
                    DiagnosticActionDispatcher.EnqueueUseHotbarItem(queue, snapshot, DiagnosticActionSource.ForHotkey("Ctrl+Alt+U"));
                }

                if (PressedCombo(VkT))
                {
                    Mark("Ctrl+Alt+T", "备用热键：鼠标干跑。");
                    DiagnosticActionDispatcher.EnqueueMouseTargetDryRun(queue, snapshot, DiagnosticActionSource.ForHotkey("Ctrl+Alt+T"));
                }
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "diagnostic-action-hotkey-error",
                    TimeSpan.FromSeconds(10),
                    "DiagnosticActionHotkeyService",
                    "Diagnostic action hotkey update failed.", error);
            }
        }

        public static string GetRecentFeedbackLine(int newestIndex)
        {
            lock (RecentFeedback)
            {
                var index = RecentFeedback.Count - 1 - newestIndex;
                return index >= 0 && index < RecentFeedback.Count
                    ? RecentFeedback[index]
                    : string.Empty;
            }
        }

        private static bool PressedCombo(int key)
        {
            var isDown = IsKeyDown(VkControl) && IsKeyDown(VkAlt) && IsKeyDown(key);
            bool wasDown;
            WasDown.TryGetValue(key, out wasDown);
            WasDown[key] = isDown;

            if (!isDown || wasDown || DateTime.UtcNow - _lastHotkeyUtc < Debounce)
            {
                return false;
            }

            if (!IsCurrentProcessForeground())
            {
                return false;
            }

            _lastHotkeyUtc = DateTime.UtcNow;
            return true;
        }

        private static void RefreshComboState(int key)
        {
            WasDown[key] = IsKeyDown(VkControl) && IsKeyDown(VkAlt) && IsKeyDown(key);
        }

        private static void RefreshAllComboStates()
        {
            RefreshComboState(VkJ);
            RefreshComboState(VkK);
            RefreshComboState(VkL);
            RefreshComboState(VkD);
            RefreshComboState(VkU);
            RefreshComboState(VkT);
            RefreshComboState(VkLeft);
            RefreshComboState(VkRight);
        }

        private static bool IsKeyDown(int key)
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            return (GetAsyncKeyState(key) & 0x8000) != 0;
        }

        private static void Mark(string hotkey, string message)
        {
            LastDiagnosticHotkey = hotkey ?? string.Empty;
            LastDiagnosticHotkeyUtc = DateTime.UtcNow;
            LastDiagnosticHotkeyMessage = message ?? string.Empty;
            DiagnosticInteractionDiagnostics.RecordHotkey();
            var line = "[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " +
                       LastDiagnosticHotkey + ": " + LastDiagnosticHotkeyMessage;
            lock (RecentFeedback)
            {
                RecentFeedback.Add(line);
                while (RecentFeedback.Count > 8)
                {
                    RecentFeedback.RemoveAt(0);
                }
            }

            Logger.Info("DiagnosticActionHotkeyService", LastDiagnosticHotkey + ": " + LastDiagnosticHotkeyMessage);
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
