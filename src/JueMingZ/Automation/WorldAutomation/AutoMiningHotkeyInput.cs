using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.WorldAutomation
{
    // Legacy hotkey parser kept only for pre-unified regression tests; production auto-mining trigger
    // switched to UnifiedHotkeyRuntimeService in stage 07 and must not call this path again.
    internal static class AutoMiningHotkeyInput
    {
        private const int VkShift = 0x10;
        private const int VkControl = 0x11;
        private const int VkAlt = 0x12;
        private static readonly TimeSpan TriggerDebounce = TimeSpan.FromMilliseconds(220);
        private static readonly Dictionary<string, bool> WasDownByHotkey = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static DateTime _lastTriggerUtc = DateTime.MinValue;

        public static bool TryConsumePressed(string text, out string display)
        {
            var result = ConsumePressed(text);
            display = result.Display;
            return result.Accepted;
        }

        internal static AutoMiningHotkeyInputResult ConsumePressed(string text)
        {
            return ConsumePressedCore(
                text,
                IsPhysicalKeyDown,
                GetRuntimeBlockReason,
                IsCurrentProcessForeground,
                DateTime.UtcNow);
        }

        internal static AutoMiningHotkeyInputResult ConsumePressedForTesting(
            string text,
            IDictionary<int, bool> downKeys,
            bool inputBlocked,
            string blockReason,
            bool foreground,
            DateTime utcNow)
        {
            return ConsumePressedCore(
                text,
                keyCode =>
                {
                    bool down;
                    return downKeys != null && downKeys.TryGetValue(keyCode, out down) && down;
                },
                () => inputBlocked ? NormalizeBlockReason(blockReason, "inputBlocked") : string.Empty,
                () => foreground,
                utcNow);
        }

        internal static void ResetForTesting()
        {
            WasDownByHotkey.Clear();
            _lastTriggerUtc = DateTime.MinValue;
        }

        private static AutoMiningHotkeyInputResult ConsumePressedCore(
            string text,
            Func<int, bool> keyDown,
            Func<string> getBlockReason,
            Func<bool> isForeground,
            DateTime utcNow)
        {
            var result = new AutoMiningHotkeyInputResult();
            HotkeyChord chord;
            if (!TryParseHotkey(text, out chord))
            {
                SyncSingleState(string.Empty, false);
                result.Reason = string.IsNullOrWhiteSpace(text) ? "unbound" : "invalidHotkey";
                return result;
            }

            result.Display = chord.Display;
            result.Normalized = chord.Normalized;
            keyDown = keyDown ?? IsPhysicalKeyDown;
            var isDown = IsChordDown(chord, keyDown);
            result.Down = isDown;
            bool wasDown;
            WasDownByHotkey.TryGetValue(chord.Normalized, out wasDown);
            SyncSingleState(chord.Normalized, isDown);
            if (!isDown)
            {
                result.Reason = "notPressed";
                return result;
            }

            if (wasDown)
            {
                result.Reason = "held";
                return result;
            }

            result.PressedEdge = true;
            if (utcNow - _lastTriggerUtc < TriggerDebounce)
            {
                result.Reason = "debounce";
                return result;
            }

            if (isForeground != null && !isForeground())
            {
                result.Reason = "notForeground";
                result.DiagnosticResultCode = DiagnosticResultCode.BlockedByEnvironment;
                return result;
            }

            var blockReason = getBlockReason == null ? string.Empty : getBlockReason();
            if (!string.IsNullOrWhiteSpace(blockReason))
            {
                result.Reason = NormalizeBlockReason(blockReason, "inputBlocked");
                result.DiagnosticResultCode = IsUiBlockReason(result.Reason)
                    ? DiagnosticResultCode.BlockedByUi
                    : DiagnosticResultCode.BlockedByEnvironment;
                return result;
            }

            _lastTriggerUtc = utcNow;
            result.Accepted = true;
            result.Reason = "accepted";
            result.DiagnosticResultCode = DiagnosticResultCode.Succeeded;
            return result;
        }

        private static void SyncSingleState(string liveKey, bool isDown)
        {
            if (string.IsNullOrWhiteSpace(liveKey))
            {
                WasDownByHotkey.Clear();
                return;
            }

            var remove = new List<string>();
            foreach (var pair in WasDownByHotkey)
            {
                if (!string.Equals(pair.Key, liveKey, StringComparison.Ordinal))
                {
                    remove.Add(pair.Key);
                }
            }

            for (var index = 0; index < remove.Count; index++)
            {
                WasDownByHotkey.Remove(remove[index]);
            }

            WasDownByHotkey[liveKey] = isDown;
        }

        private static bool TryParseHotkey(string text, out HotkeyChord chord)
        {
            chord = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var parts = text.Split('+');
            var ctrl = false;
            var alt = false;
            var shift = false;
            var keyToken = string.Empty;
            for (var index = 0; index < parts.Length; index++)
            {
                var token = parts[index] == null ? string.Empty : parts[index].Trim();
                if (token.Length <= 0)
                {
                    continue;
                }

                if (string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl = true;
                    continue;
                }

                if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase))
                {
                    alt = true;
                    continue;
                }

                if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
                {
                    shift = true;
                    continue;
                }

                if (!string.IsNullOrEmpty(keyToken))
                {
                    return false;
                }

                keyToken = token;
            }

            int keyCode;
            if (!TryParseVirtualKey(keyToken, out keyCode))
            {
                return false;
            }

            var normalized = (ctrl ? "Ctrl+" : string.Empty) +
                             (alt ? "Alt+" : string.Empty) +
                             (shift ? "Shift+" : string.Empty) +
                             keyToken.ToUpperInvariant();
            chord = new HotkeyChord
            {
                Ctrl = ctrl,
                Alt = alt,
                Shift = shift,
                KeyCode = keyCode,
                Normalized = normalized,
                Display = normalized
            };
            return true;
        }

        private static bool TryParseVirtualKey(string token, out int keyCode)
        {
            keyCode = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var text = token.Trim();
            if (text.Length == 1)
            {
                var c = char.ToUpperInvariant(text[0]);
                if (c >= 'A' && c <= 'Z')
                {
                    keyCode = c;
                    return true;
                }

                if (c >= '0' && c <= '9')
                {
                    keyCode = c;
                    return true;
                }
            }

            var upper = text.ToUpperInvariant();
            if (upper.Length > 1 && upper[0] == 'F')
            {
                int fn;
                if (int.TryParse(upper.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out fn) &&
                    fn >= 1 &&
                    fn <= 24)
                {
                    keyCode = 0x6F + fn;
                    return true;
                }
            }

            switch (upper)
            {
                case "MOUSE1":
                    keyCode = 0x01;
                    return true;
                case "MOUSE2":
                    keyCode = 0x02;
                    return true;
                case "MOUSE3":
                    keyCode = 0x04;
                    return true;
                case "MOUSE4":
                    keyCode = 0x05;
                    return true;
                case "MOUSE5":
                    keyCode = 0x06;
                    return true;
                case "CAPS":
                case "CAPSLOCK":
                    keyCode = 0x14;
                    return true;
                case "LEFT":
                    keyCode = 0x25;
                    return true;
                case "UP":
                    keyCode = 0x26;
                    return true;
                case "RIGHT":
                    keyCode = 0x27;
                    return true;
                case "DOWN":
                    keyCode = 0x28;
                    return true;
                case "SPACE":
                    keyCode = 0x20;
                    return true;
                case "TAB":
                    keyCode = 0x09;
                    return true;
                case "ENTER":
                    keyCode = 0x0D;
                    return true;
                case "ESC":
                case "ESCAPE":
                    keyCode = 0x1B;
                    return true;
            }

            return false;
        }

        private static bool IsChordDown(HotkeyChord chord, Func<int, bool> keyDown)
        {
            return chord != null &&
                   keyDown != null &&
                   (!chord.Ctrl || keyDown(VkControl)) &&
                   (!chord.Alt || keyDown(VkAlt)) &&
                   (!chord.Shift || keyDown(VkShift)) &&
                   keyDown(chord.KeyCode);
        }

        private static string GetRuntimeBlockReason()
        {
            if (LegacyMainUiState.Visible)
            {
                return "legacyMainUiVisible";
            }

            if (LegacyUiInput.IsActiveInteraction())
            {
                return "legacyUiActive";
            }

            if (LegacyTextInput.IsAnyFocused)
            {
                return "textInputFocused";
            }

            bool focused;
            string reason;
            if (TerrariaInputCompat.TryReadTextInputFocus(out focused, out reason) && focused)
            {
                return NormalizeBlockReason(reason, "textInputFocused");
            }

            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return "gameInputUnavailable";
            }

            return string.Empty;
        }

        private static bool IsPhysicalKeyDown(int keyCode)
        {
            return (GetAsyncKeyState(keyCode) & 0x8000) != 0;
        }

        private static string NormalizeBlockReason(string reason, string fallback)
        {
            reason = reason == null ? string.Empty : reason.Trim();
            return reason.Length <= 0 ? fallback : reason;
        }

        private static bool IsUiBlockReason(string reason)
        {
            return string.Equals(reason, "legacyMainUiVisible", StringComparison.Ordinal) ||
                   string.Equals(reason, "legacyUiActive", StringComparison.Ordinal) ||
                   string.Equals(reason, "textInputFocused", StringComparison.Ordinal) ||
                   reason.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reason.IndexOf("chat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reason.IndexOf("inventory", StringComparison.OrdinalIgnoreCase) >= 0;
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

        private sealed class HotkeyChord
        {
            public bool Ctrl { get; set; }
            public bool Alt { get; set; }
            public bool Shift { get; set; }
            public int KeyCode { get; set; }
            public string Normalized { get; set; }
            public string Display { get; set; }
        }
    }
}
