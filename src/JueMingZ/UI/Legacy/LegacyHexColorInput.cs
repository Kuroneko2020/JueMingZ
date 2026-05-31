using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;

namespace JueMingZ.UI.Legacy
{
    internal static class LegacyHexColorInput
    {
        private const int VkBack = 0x08;
        private const int VkReturn = 0x0D;
        private const int VkEscape = 0x1B;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, bool> WasDown = new Dictionary<int, bool>();
        private static string _activeId = string.Empty;
        private static string _draft = "#";
        private static bool _replaceOnNextHex;

        public static void Focus(string id, string currentColor)
        {
            lock (SyncRoot)
            {
                _activeId = id ?? string.Empty;
                _draft = InformationStyleHelper.NormalizeHtmlColor(currentColor, "#FFFFFF");
                _replaceOnNextHex = true;
                WasDown.Clear();
            }
        }

        public static void ClearFocus()
        {
            lock (SyncRoot)
            {
                _activeId = string.Empty;
                _draft = "#";
                _replaceOnNextHex = false;
                WasDown.Clear();
            }
        }

        public static bool IsFocused(string id)
        {
            lock (SyncRoot)
            {
                return !string.IsNullOrWhiteSpace(id) && string.Equals(_activeId, id, StringComparison.Ordinal);
            }
        }

        public static string GetDisplayText(string id, string currentColor)
        {
            lock (SyncRoot)
            {
                return IsFocusedLocked(id) ? _draft : InformationStyleHelper.NormalizeHtmlColor(currentColor, "#FFFFFF");
            }
        }

        public static bool Update(string id, string currentColor, out string colorHex)
        {
            colorHex = string.Empty;
            lock (SyncRoot)
            {
                if (!IsFocusedLocked(id))
                {
                    return false;
                }

                if (!IsCurrentProcessForeground())
                {
                    return false;
                }

                var changed = false;
                if (PressedLocked(VkEscape))
                {
                    ClearFocusLocked();
                    return false;
                }

                if (PressedLocked(VkBack))
                {
                    if (_replaceOnNextHex)
                    {
                        _draft = "#";
                        _replaceOnNextHex = false;
                        changed = true;
                    }
                    else if (_draft.Length > 1)
                    {
                        _draft = _draft.Substring(0, _draft.Length - 1);
                        changed = true;
                    }
                }

                for (var key = 0x30; key <= 0x39; key++)
                {
                    if (PressedLocked(key))
                    {
                        changed = AppendHexCharLocked((char)('0' + key - 0x30)) || changed;
                    }
                }

                for (var key = 0x60; key <= 0x69; key++)
                {
                    if (PressedLocked(key))
                    {
                        changed = AppendHexCharLocked((char)('0' + key - 0x60)) || changed;
                    }
                }

                for (var key = 0x41; key <= 0x46; key++)
                {
                    if (PressedLocked(key))
                    {
                        changed = AppendHexCharLocked((char)key) || changed;
                    }
                }

                if (PressedLocked(VkReturn))
                {
                    changed = true;
                }

                if (!changed || _draft.Length != 7)
                {
                    return false;
                }

                colorHex = InformationStyleHelper.NormalizeHtmlColor(_draft, InformationStyleHelper.NormalizeHtmlColor(currentColor, "#FFFFFF"));
                return string.Equals(colorHex, _draft, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsFocusedLocked(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && string.Equals(_activeId, id, StringComparison.Ordinal);
        }

        private static bool AppendHexCharLocked(char value)
        {
            if (_replaceOnNextHex)
            {
                _draft = "#";
                _replaceOnNextHex = false;
            }

            if (_draft.Length >= 7)
            {
                return false;
            }

            _draft += char.ToUpperInvariant(value);
            return true;
        }

        private static void ClearFocusLocked()
        {
            _activeId = string.Empty;
            _draft = "#";
            _replaceOnNextHex = false;
            WasDown.Clear();
        }

        private static bool PressedLocked(int key)
        {
            var isDown = TerrariaMainCompat.AllowsInputProcessing && (GetAsyncKeyState(key) & 0x8000) != 0;
            bool wasDown;
            WasDown.TryGetValue(key, out wasDown);
            WasDown[key] = isDown;
            return isDown && !wasDown;
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
