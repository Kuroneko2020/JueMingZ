using System.Collections.Generic;

namespace JueMingZ.Input.Hotkeys
{
    public static class HotkeyDisplayFormatter
    {
        public static string FormatToken(HotkeyToken token)
        {
            return token == null ? string.Empty : token.DisplayName;
        }

        public static string FormatToken(string token)
        {
            HotkeyToken resolved;
            return HotkeyTokenCatalog.TryGetToken(token, out resolved)
                ? FormatToken(resolved)
                : string.Empty;
        }

        public static string FormatChord(HotkeyChord chord)
        {
            if (chord == null || chord.PrimaryKey == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var index = 0; chord.Modifiers != null && index < chord.Modifiers.Count; index++)
            {
                parts.Add(FormatToken(chord.Modifiers[index]));
            }

            parts.Add(FormatToken(chord.PrimaryKey));
            return string.Join(" + ", parts.ToArray());
        }
    }
}
