using System;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Config
{
    public sealed class FeatureToggleHotkeyChord
    {
        private FeatureToggleHotkeyChord(string modifier, string key)
        {
            Modifier = modifier ?? string.Empty;
            Key = key ?? string.Empty;
            Normalized = Modifier.Length <= 0 ? Key : Modifier + "+" + Key;
            Display = Normalized;
        }

        public string Modifier { get; private set; }
        public string Key { get; private set; }
        public string Normalized { get; private set; }
        public string Display { get; private set; }

        public static bool TryNormalize(string text, out string normalized)
        {
            FeatureToggleHotkeyChord chord;
            if (TryParse(text, out chord))
            {
                normalized = chord.Normalized;
                return true;
            }

            normalized = string.Empty;
            return false;
        }

        public static bool TryParse(string text, out FeatureToggleHotkeyChord chord)
        {
            chord = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return TryParseParts(text.Split('+'), out chord);
        }

        public static bool TryParseParts(IEnumerable<string> parts, out FeatureToggleHotkeyChord chord)
        {
            chord = null;
            if (parts == null)
            {
                return false;
            }

            var modifier = string.Empty;
            var key = string.Empty;
            var sawAny = false;
            foreach (var part in parts)
            {
                if (part == null)
                {
                    return false;
                }

                var token = part.Trim();
                if (token.Length <= 0)
                {
                    return false;
                }

                sawAny = true;
                var normalizedModifier = NormalizeModifier(token);
                if (normalizedModifier.Length > 0)
                {
                    if (modifier.Length > 0)
                    {
                        return false;
                    }

                    modifier = normalizedModifier;
                    continue;
                }

                var normalizedKey = NormalizeMainKey(token);
                if (normalizedKey.Length <= 0 || key.Length > 0)
                {
                    return false;
                }

                key = normalizedKey;
            }

            if (!sawAny || key.Length <= 0)
            {
                return false;
            }

            chord = new FeatureToggleHotkeyChord(modifier, key);
            return true;
        }

        public static bool IsModifierToken(string token)
        {
            return NormalizeModifier(token).Length > 0;
        }

        private static string NormalizeModifier(string token)
        {
            if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase))
            {
                return "Alt";
            }

            if (string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase))
            {
                return "Ctrl";
            }

            if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
            {
                return "Shift";
            }

            return string.Empty;
        }

        private static string NormalizeMainKey(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || IsMouseToken(token))
            {
                return string.Empty;
            }

            var text = token.Trim();
            if (string.Equals(text, "Esc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Escape", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Backspace", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (text.Length == 1)
            {
                var ch = char.ToUpperInvariant(text[0]);
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
                {
                    return ch.ToString();
                }
            }

            if (text.Length >= 2 &&
                (text[0] == 'F' || text[0] == 'f') &&
                int.TryParse(text.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var functionKey) &&
                functionKey >= 1 &&
                functionKey <= 24)
            {
                return "F" + functionKey.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private static bool IsMouseToken(string token)
        {
            return string.Equals(token, "MouseLeft", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "LeftMouse", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Mouse1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "LButton", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "MouseRight", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "RightMouse", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Mouse2", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "RButton", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "MouseMiddle", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "MiddleMouse", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Mouse3", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "MButton", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Mouse4", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "XButton1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Mouse5", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "XButton2", StringComparison.OrdinalIgnoreCase);
        }
    }
}
