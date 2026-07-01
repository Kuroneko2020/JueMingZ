using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace JueMingZ.Input.Hotkeys
{
    public static class HotkeyTokenCatalog
    {
        private static readonly IReadOnlyList<HotkeyToken> Tokens;
        private static readonly Dictionary<string, HotkeyToken> TokensByName;

        static HotkeyTokenCatalog()
        {
            var tokens = new List<HotkeyToken>();
            var byName = new Dictionary<string, HotkeyToken>(StringComparer.OrdinalIgnoreCase);

            for (var ch = 'A'; ch <= 'Z'; ch++)
            {
                var text = ch.ToString();
                Add(tokens, byName, text, text, HotkeyTokenKind.Letter, HotkeyTokenRole.Primary, ch);
            }

            for (var digit = 0; digit <= 9; digit++)
            {
                var text = digit.ToString(CultureInfo.InvariantCulture);
                Add(tokens, byName, text, text, HotkeyTokenKind.MainDigit, HotkeyTokenRole.Primary, 0x30 + digit);
            }

            for (var functionKey = 1; functionKey <= 24; functionKey++)
            {
                var text = "F" + functionKey.ToString(CultureInfo.InvariantCulture);
                Add(tokens, byName, text, text, HotkeyTokenKind.FunctionKey, HotkeyTokenRole.Primary, 0x6F + functionKey);
            }

            Add(tokens, byName, "Left", "Left", HotkeyTokenKind.Direction, HotkeyTokenRole.Primary, 0x25);
            Add(tokens, byName, "Up", "Up", HotkeyTokenKind.Direction, HotkeyTokenRole.Primary, 0x26);
            Add(tokens, byName, "Right", "Right", HotkeyTokenKind.Direction, HotkeyTokenRole.Primary, 0x27);
            Add(tokens, byName, "Down", "Down", HotkeyTokenKind.Direction, HotkeyTokenRole.Primary, 0x28);

            Add(tokens, byName, "Insert", "Insert", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x2D);
            Add(tokens, byName, "Delete", "Delete", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x2E, "Del");
            Add(tokens, byName, "Home", "Home", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x24);
            Add(tokens, byName, "End", "End", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x23);
            Add(tokens, byName, "PageUp", "PageUp", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x21, "PgUp");
            Add(tokens, byName, "PageDown", "PageDown", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x22, "PgDown");
            Add(tokens, byName, "Enter", "Enter", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x0D, "Return", "NumEnter", "NumpadEnter");
            Add(tokens, byName, "Tab", "Tab", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x09);
            Add(tokens, byName, "Space", "Space", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x20, "Spacebar");
            Add(tokens, byName, "Backspace", "Backspace", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x08);
            Add(tokens, byName, "Esc", "Esc", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x1B, "Escape");

            for (var digit = 0; digit <= 9; digit++)
            {
                var text = "Num" + digit.ToString(CultureInfo.InvariantCulture);
                Add(tokens, byName, text, text, HotkeyTokenKind.NumpadDigit, HotkeyTokenRole.Primary, 0x60 + digit, "Numpad" + digit.ToString(CultureInfo.InvariantCulture));
            }

            Add(tokens, byName, "NumPlus", "Num+", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6B, "NumpadPlus", "NumAdd", "NumpadAdd");
            Add(tokens, byName, "NumMinus", "Num-", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6D, "NumpadMinus", "NumSubtract", "NumpadSubtract");
            Add(tokens, byName, "NumMultiply", "Num*", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6A, "NumpadMultiply", "NumStar", "NumpadStar");
            Add(tokens, byName, "NumDivide", "Num/", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6F, "NumpadDivide");
            Add(tokens, byName, "NumDecimal", "Num.", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6E, "NumpadDecimal", "NumDot", "NumpadDot");

            Add(tokens, byName, "MouseLeft", "MouseLeft", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x01, "LeftMouse", "Mouse1", "LButton");
            Add(tokens, byName, "MouseRight", "MouseRight", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x02, "RightMouse", "Mouse2", "RButton");
            Add(tokens, byName, "MouseMiddle", "MouseMiddle", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x04, "MiddleMouse", "Mouse3", "MButton");
            Add(tokens, byName, "MouseX1", "MouseX1", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x05, "Mouse4", "XButton1");
            Add(tokens, byName, "MouseX2", "MouseX2", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x06, "Mouse5", "XButton2");

            Add(tokens, byName, "LCtrl", "LCtrl", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA2, "LeftCtrl", "LControl", "LeftControl");
            Add(tokens, byName, "RCtrl", "RCtrl", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA3, "RightCtrl", "RControl", "RightControl");
            Add(tokens, byName, "LAlt", "LAlt", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA4, "LeftAlt");
            Add(tokens, byName, "RAlt", "RAlt", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA5, "RightAlt");
            Add(tokens, byName, "LShift", "LShift", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA0, "LeftShift");
            Add(tokens, byName, "RShift", "RShift", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA1, "RightShift");

            Tokens = new ReadOnlyCollection<HotkeyToken>(tokens);
            TokensByName = byName;
        }

        public static IReadOnlyList<HotkeyToken> AllTokens
        {
            get { return Tokens; }
        }

        public static bool TryGetToken(string text, out HotkeyToken token)
        {
            token = null;
            var name = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            return name.Length > 0 && TokensByName.TryGetValue(name, out token);
        }

        public static bool TryNormalizeToken(string text, out string canonical)
        {
            HotkeyToken token;
            if (TryGetToken(text, out token))
            {
                canonical = token.Canonical;
                return true;
            }

            canonical = string.Empty;
            return false;
        }

        public static int GetModifierSortIndex(HotkeyToken token)
        {
            if (token == null || !token.IsModifier)
            {
                return int.MaxValue;
            }

            switch (token.Canonical)
            {
                case "LCtrl":
                    return 0;
                case "RCtrl":
                    return 1;
                case "LAlt":
                    return 2;
                case "RAlt":
                    return 3;
                case "LShift":
                    return 4;
                case "RShift":
                    return 5;
                default:
                    return int.MaxValue - 1;
            }
        }

        private static void Add(
            List<HotkeyToken> tokens,
            Dictionary<string, HotkeyToken> byName,
            string canonical,
            string displayName,
            HotkeyTokenKind kind,
            HotkeyTokenRole role,
            int virtualKey,
            params string[] aliases)
        {
            var token = new HotkeyToken(canonical, displayName, kind, role, virtualKey);
            tokens.Add(token);
            byName[canonical] = token;

            for (var index = 0; aliases != null && index < aliases.Length; index++)
            {
                var alias = aliases[index];
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    byName[alias.Trim()] = token;
                }
            }
        }
    }
}
