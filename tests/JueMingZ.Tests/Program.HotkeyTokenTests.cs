using System;
using JueMingZ.Input.Hotkeys;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void HotkeyTokenCatalogCoversStandardKeyboardMouseTokens()
        {
            AssertTokenExists("A", HotkeyTokenKind.Letter, HotkeyTokenRole.Primary, 0x41);
            AssertTokenExists("Z", HotkeyTokenKind.Letter, HotkeyTokenRole.Primary, 0x5A);

            for (var digit = 0; digit <= 9; digit++)
            {
                AssertTokenExists(digit.ToString(), HotkeyTokenKind.MainDigit, HotkeyTokenRole.Primary, 0x30 + digit);
                AssertTokenExists("Num" + digit.ToString(), HotkeyTokenKind.NumpadDigit, HotkeyTokenRole.Primary, 0x60 + digit);
            }

            for (var functionKey = 1; functionKey <= 24; functionKey++)
            {
                AssertTokenExists("F" + functionKey.ToString(), HotkeyTokenKind.FunctionKey, HotkeyTokenRole.Primary, 0x6F + functionKey);
            }

            AssertTokenExists("Left", HotkeyTokenKind.Direction, HotkeyTokenRole.Primary, 0x25);
            AssertTokenExists("Up", HotkeyTokenKind.Direction, HotkeyTokenRole.Primary, 0x26);
            AssertTokenExists("Right", HotkeyTokenKind.Direction, HotkeyTokenRole.Primary, 0x27);
            AssertTokenExists("Down", HotkeyTokenKind.Direction, HotkeyTokenRole.Primary, 0x28);
            AssertTokenExists("Insert", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x2D);
            AssertTokenExists("Delete", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x2E);
            AssertTokenExists("Home", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x24);
            AssertTokenExists("End", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x23);
            AssertTokenExists("PageUp", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x21);
            AssertTokenExists("PageDown", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x22);
            AssertTokenExists("Enter", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x0D);
            AssertTokenExists("Tab", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x09);
            AssertTokenExists("Space", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x20);
            AssertTokenExists("Backspace", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x08);
            AssertTokenExists("Esc", HotkeyTokenKind.Editing, HotkeyTokenRole.Primary, 0x1B);

            AssertTokenExists("NumPlus", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6B);
            AssertTokenExists("NumMinus", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6D);
            AssertTokenExists("NumMultiply", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6A);
            AssertTokenExists("NumDivide", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6F);
            AssertTokenExists("NumDecimal", HotkeyTokenKind.NumpadOperator, HotkeyTokenRole.Primary, 0x6E);

            AssertTokenExists("MouseLeft", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x01);
            AssertTokenExists("MouseRight", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x02);
            AssertTokenExists("MouseMiddle", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x04);
            AssertTokenExists("MouseX1", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x05);
            AssertTokenExists("MouseX2", HotkeyTokenKind.MouseButton, HotkeyTokenRole.Primary, 0x06);

            AssertTokenExists("LCtrl", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA2);
            AssertTokenExists("RCtrl", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA3);
            AssertTokenExists("LAlt", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA4);
            AssertTokenExists("RAlt", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA5);
            AssertTokenExists("LShift", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA0);
            AssertTokenExists("RShift", HotkeyTokenKind.Modifier, HotkeyTokenRole.Modifier, 0xA1);
        }

        private static void HotkeyParserNormalizesTokensAndAliases()
        {
            AssertHotkeyParseSuccess("1", "1", "1");
            AssertHotkeyParseSuccess("Num1", "Num1", "Num1");
            AssertHotkeyParseSuccess("numpad1", "Num1", "Num1");
            AssertHotkeyParseSuccess("NumPlus", "NumPlus", "Num+");
            AssertHotkeyParseSuccess("NumMinus", "NumMinus", "Num-");
            AssertHotkeyParseSuccess("NumMultiply", "NumMultiply", "Num*");
            AssertHotkeyParseSuccess("NumDivide", "NumDivide", "Num/");
            AssertHotkeyParseSuccess("NumDecimal", "NumDecimal", "Num.");
            AssertHotkeyParseSuccess("NumEnter", "Enter", "Enter");
            AssertHotkeyParseSuccess("Return", "Enter", "Enter");
            AssertHotkeyParseSuccess("Mouse4", "MouseX1", "MouseX1");
            AssertHotkeyParseSuccess("Mouse5", "MouseX2", "MouseX2");
            AssertHotkeyParseSuccess("XButton1", "MouseX1", "MouseX1");
            AssertHotkeyParseSuccess("LCtrl+RAlt+K", "LCtrl+RAlt+K", "LCtrl + RAlt + K");
            AssertHotkeyParseSuccess("RAlt+LCtrl+K", "LCtrl+RAlt+K", "LCtrl + RAlt + K");
            AssertHotkeyParseSuccess("LAlt+LShift+MouseLeft", "LAlt+LShift+MouseLeft", "LAlt + LShift + MouseLeft");
        }

        private static void HotkeyParserReportsFailureReasons()
        {
            AssertHotkeyParseFailure("Backspace", HotkeyParseFailureReason.ReservedKey, "reservedKey");
            AssertHotkeyParseFailure("Escape", HotkeyParseFailureReason.ReservedKey, "reservedKey");
            AssertHotkeyParseFailure("F5", HotkeyParseFailureReason.ReservedKey, "reservedKey");
            AssertHotkeyParseFailure("LCtrl+F5", HotkeyParseFailureReason.ReservedKey, "reservedKey");
            AssertHotkeyParseFailure("RAlt+Esc", HotkeyParseFailureReason.ReservedKey, "reservedKey");
            AssertHotkeyParseFailure("WheelUp", HotkeyParseFailureReason.UnsupportedToken, "unsupportedToken");
            AssertHotkeyParseFailure("WheelDown", HotkeyParseFailureReason.UnsupportedToken, "unsupportedToken");
            AssertHotkeyParseFailure("LCtrl+WheelDown", HotkeyParseFailureReason.UnsupportedToken, "unsupportedToken");
            AssertHotkeyParseFailure("Ctrl+K", HotkeyParseFailureReason.UnsupportedToken, "unsupportedToken");
            AssertHotkeyParseFailure("LCtrl+LCtrl+K", HotkeyParseFailureReason.DuplicateModifier, "duplicateModifier");
            AssertHotkeyParseFailure("LCtrl+RAlt", HotkeyParseFailureReason.MissingPrimaryKey, "missingPrimaryKey");
            AssertHotkeyParseFailure("K+L", HotkeyParseFailureReason.TooManyPrimaryKeys, "tooManyPrimaryKeys");
            AssertHotkeyParseFailure("NotAKey", HotkeyParseFailureReason.InvalidToken, "invalidToken");
        }

        private static void HotkeyDisplayFormatterKeepsMainAndNumpadDistinct()
        {
            AssertStringEquals(HotkeyDisplayFormatter.FormatToken("1"), "1", "main digit display");
            AssertStringEquals(HotkeyDisplayFormatter.FormatToken("Num1"), "Num1", "numpad digit display");
            AssertStringEquals(HotkeyDisplayFormatter.FormatToken("NumPlus"), "Num+", "numpad plus display");
            AssertStringEquals(HotkeyDisplayFormatter.FormatToken("LCtrl"), "LCtrl", "left control display");
            AssertStringEquals(HotkeyDisplayFormatter.FormatToken("RCtrl"), "RCtrl", "right control display");
            AssertStringEquals(HotkeyDisplayFormatter.FormatToken("MouseMiddle"), "MouseMiddle", "mouse middle display");
        }

        private static void AssertTokenExists(string tokenText, HotkeyTokenKind expectedKind, HotkeyTokenRole expectedRole, int expectedVirtualKey)
        {
            HotkeyToken token;
            if (!HotkeyTokenCatalog.TryGetToken(tokenText, out token) || token == null)
            {
                throw new InvalidOperationException("Expected hotkey token " + tokenText + " to exist.");
            }

            if (token.Kind != expectedKind)
            {
                throw new InvalidOperationException("Expected hotkey token " + tokenText + " kind " + expectedKind + ", got " + token.Kind + ".");
            }

            if (token.Role != expectedRole)
            {
                throw new InvalidOperationException("Expected hotkey token " + tokenText + " role " + expectedRole + ", got " + token.Role + ".");
            }

            if (token.VirtualKey != expectedVirtualKey)
            {
                throw new InvalidOperationException("Expected hotkey token " + tokenText + " virtual key " + expectedVirtualKey + ", got " + token.VirtualKey + ".");
            }
        }

        private static void AssertHotkeyParseSuccess(string value, string expectedNormalized, string expectedDisplay)
        {
            var result = HotkeyParser.Parse(value);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Expected hotkey " + value + " to parse, got " + result.Reason + ".");
            }

            AssertStringEquals(result.Normalized, expectedNormalized, "hotkey normalized");
            AssertStringEquals(result.Display, expectedDisplay, "hotkey display");
        }

        private static void AssertHotkeyParseFailure(string value, HotkeyParseFailureReason expectedFailureReason, string expectedReason)
        {
            var result = HotkeyParser.Parse(value);
            if (result.Succeeded)
            {
                throw new InvalidOperationException("Expected hotkey " + value + " to fail, got " + result.Normalized + ".");
            }

            if (result.FailureReason != expectedFailureReason)
            {
                throw new InvalidOperationException("Expected hotkey " + value + " failure " + expectedFailureReason + ", got " + result.FailureReason + ".");
            }

            AssertStringEquals(result.Reason, expectedReason, "hotkey failure reason");
        }
    }
}
