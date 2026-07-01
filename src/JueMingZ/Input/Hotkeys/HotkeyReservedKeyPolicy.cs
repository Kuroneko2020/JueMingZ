using System;

namespace JueMingZ.Input.Hotkeys
{
    public static class HotkeyReservedKeyPolicy
    {
        public static bool IsReservedPrimary(HotkeyToken token)
        {
            return token != null && IsReservedPrimary(token.Canonical);
        }

        public static bool IsReservedPrimary(string canonicalToken)
        {
            return string.Equals(canonicalToken, "Backspace", StringComparison.Ordinal) ||
                   string.Equals(canonicalToken, "Esc", StringComparison.Ordinal) ||
                   string.Equals(canonicalToken, "F5", StringComparison.Ordinal);
        }
    }
}
