using System;

namespace JueMingZ.Input.Hotkeys
{
    public static class HotkeyUnsupportedTokenPolicy
    {
        public static bool IsUnsupportedToken(string text)
        {
            var token = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            if (token.Length <= 0)
            {
                return false;
            }

            if (string.Equals(token, "WheelUp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "WheelDown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "MouseWheelUp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "MouseWheelDown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "ScrollUp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "ScrollDown", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // New bindings must store the side-specific modifier tokens. AltGr
            // is represented by whatever left/right modifier tokens the input
            // layer actually reports, not by a generic synthetic token.
            return string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "AltGr", StringComparison.OrdinalIgnoreCase);
        }
    }
}
