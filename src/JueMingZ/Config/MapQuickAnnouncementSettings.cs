using System;
using System.Globalization;

namespace JueMingZ.Config
{
    public static class MapQuickAnnouncementSettings
    {
        public const string DefaultHotkeySlot1 = "Alt";
        public const string DefaultHotkeySlot2 = "Shift";
        public const string DefaultTriggerKey = "MouseLeft";
        public const string DefaultAnnouncementColorHex = "#FFD966";
        public const int DefaultCooldownMilliseconds = 500;
        public const int DefaultAirCooldownMilliseconds = 2000;
        public const int MinCooldownMilliseconds = 100;
        public const int MaxCooldownMilliseconds = 60000;
        public const string HotkeySlot1Id = "1";
        public const string HotkeySlot2Id = "2";
        public const string HotkeyTriggerId = "trigger";

        public static MapQuickAnnouncementHotkey NormalizeHotkey(string slot1, string slot2, string triggerKey)
        {
            var first = NormalizeKeyboardKey(slot1);
            var second = NormalizeKeyboardKey(slot2);
            var trigger = NormalizeTriggerKey(triggerKey);

            if (!string.IsNullOrWhiteSpace(first) &&
                string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
            {
                second = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(trigger) &&
                (string.Equals(trigger, first, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(trigger, second, StringComparison.OrdinalIgnoreCase)))
            {
                trigger = string.Empty;
            }

            return new MapQuickAnnouncementHotkey(first, second, trigger);
        }

        public static MapQuickAnnouncementHotkey CreateDefaultHotkey()
        {
            return new MapQuickAnnouncementHotkey(DefaultHotkeySlot1, DefaultHotkeySlot2, DefaultTriggerKey);
        }

        public static bool TryApplyCapturedHotkeyToken(
            string slot1,
            string slot2,
            string triggerKey,
            string targetSlot,
            string capturedToken,
            out MapQuickAnnouncementHotkey hotkey,
            out string resultCode)
        {
            hotkey = NormalizeHotkey(slot1, slot2, triggerKey);
            resultCode = string.Empty;
            var slotId = NormalizeHotkeySlotId(targetSlot);
            if (slotId.Length <= 0)
            {
                resultCode = "invalidSlot";
                return false;
            }

            var normalizedToken = string.Equals(slotId, HotkeyTriggerId, StringComparison.Ordinal)
                ? NormalizeTriggerKey(capturedToken)
                : NormalizeKeyboardKey(capturedToken);
            if (normalizedToken.Length <= 0)
            {
                resultCode = string.Equals(slotId, HotkeyTriggerId, StringComparison.Ordinal)
                    ? "invalidTriggerKey"
                    : "invalidKeyboardKey";
                return false;
            }

            if (IsDuplicateForSlot(hotkey, slotId, normalizedToken))
            {
                resultCode = "duplicateKey";
                return false;
            }

            if (string.Equals(slotId, HotkeySlot1Id, StringComparison.Ordinal))
            {
                hotkey = new MapQuickAnnouncementHotkey(normalizedToken, hotkey.Slot2, hotkey.TriggerKey);
            }
            else if (string.Equals(slotId, HotkeySlot2Id, StringComparison.Ordinal))
            {
                hotkey = new MapQuickAnnouncementHotkey(hotkey.Slot1, normalizedToken, hotkey.TriggerKey);
            }
            else
            {
                hotkey = new MapQuickAnnouncementHotkey(hotkey.Slot1, hotkey.Slot2, normalizedToken);
            }

            resultCode = "captured";
            return true;
        }

        public static bool TryClearHotkeySlot(
            string slot1,
            string slot2,
            string triggerKey,
            string targetSlot,
            out MapQuickAnnouncementHotkey hotkey,
            out string resultCode)
        {
            hotkey = NormalizeHotkey(slot1, slot2, triggerKey);
            resultCode = string.Empty;
            var slotId = NormalizeHotkeySlotId(targetSlot);
            if (slotId.Length <= 0)
            {
                resultCode = "invalidSlot";
                return false;
            }

            var oldValue = string.Equals(slotId, HotkeySlot1Id, StringComparison.Ordinal)
                ? hotkey.Slot1
                : (string.Equals(slotId, HotkeySlot2Id, StringComparison.Ordinal) ? hotkey.Slot2 : hotkey.TriggerKey);
            if (string.Equals(slotId, HotkeySlot1Id, StringComparison.Ordinal))
            {
                hotkey = new MapQuickAnnouncementHotkey(string.Empty, hotkey.Slot2, hotkey.TriggerKey);
            }
            else if (string.Equals(slotId, HotkeySlot2Id, StringComparison.Ordinal))
            {
                hotkey = new MapQuickAnnouncementHotkey(hotkey.Slot1, string.Empty, hotkey.TriggerKey);
            }
            else
            {
                hotkey = new MapQuickAnnouncementHotkey(hotkey.Slot1, hotkey.Slot2, string.Empty);
            }

            resultCode = string.IsNullOrWhiteSpace(oldValue) ? "alreadyEmpty" : "cleared";
            return true;
        }

        public static string NormalizeHotkeySlotId(string value)
        {
            var slot = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (string.Equals(slot, HotkeySlot1Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slot, "slot1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slot, "first", StringComparison.OrdinalIgnoreCase))
            {
                return HotkeySlot1Id;
            }

            if (string.Equals(slot, HotkeySlot2Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slot, "slot2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slot, "second", StringComparison.OrdinalIgnoreCase))
            {
                return HotkeySlot2Id;
            }

            if (string.Equals(slot, HotkeyTriggerId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slot, "3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slot, "slot3", StringComparison.OrdinalIgnoreCase))
            {
                return HotkeyTriggerId;
            }

            return string.Empty;
        }

        public static string NormalizeKeyboardKey(string value)
        {
            var token = NormalizeToken(value);
            if (IsMouseToken(token))
            {
                return string.Empty;
            }

            return IsKnownKeyboardToken(token) ? token : string.Empty;
        }

        public static string NormalizeTriggerKey(string value)
        {
            var token = NormalizeToken(value);
            if (IsMouseToken(token) || IsKnownKeyboardToken(token))
            {
                return token;
            }

            return string.Empty;
        }

        public static string NormalizeColorHex(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (value.Length == 6)
            {
                value = "#" + value;
            }

            if (value.Length != 7 || value[0] != '#')
            {
                return DefaultAnnouncementColorHex;
            }

            for (var index = 1; index < value.Length; index++)
            {
                if (!Uri.IsHexDigit(value[index]))
                {
                    return DefaultAnnouncementColorHex;
                }
            }

            return "#" + value.Substring(1).ToUpperInvariant();
        }

        public static int NormalizeCooldownMilliseconds(int value, int fallback)
        {
            if (value <= 0)
            {
                return fallback;
            }

            if (value < MinCooldownMilliseconds)
            {
                return MinCooldownMilliseconds;
            }

            return value > MaxCooldownMilliseconds ? MaxCooldownMilliseconds : value;
        }

        public static string DisplayKey(string token)
        {
            token = NormalizeToken(token);
            switch (token)
            {
                case "MouseLeft":
                    return "左键";
                case "MouseRight":
                    return "右键";
                case "MouseMiddle":
                    return "中键";
                case "Mouse4":
                    return "侧键1";
                case "Mouse5":
                    return "侧键2";
                case "Space":
                    return "Space";
                case "Caps":
                    return "Caps";
                case "Enter":
                    return "Enter";
                case "Escape":
                    return "Esc";
                case "PageUp":
                    return "PageUp";
                case "PageDown":
                    return "PageDown";
                default:
                    return token;
            }
        }

        private static string NormalizeToken(string value)
        {
            var token = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (token.Length <= 0)
            {
                return string.Empty;
            }

            if (string.Equals(token, "MouseLeft", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "LeftMouse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Mouse1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "LButton", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "左键", StringComparison.OrdinalIgnoreCase))
            {
                return "MouseLeft";
            }

            if (string.Equals(token, "MouseRight", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "RightMouse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Mouse2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "RButton", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "右键", StringComparison.OrdinalIgnoreCase))
            {
                return "MouseRight";
            }

            if (string.Equals(token, "MouseMiddle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "MiddleMouse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Mouse3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "MButton", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "中键", StringComparison.OrdinalIgnoreCase))
            {
                return "MouseMiddle";
            }

            if (string.Equals(token, "XButton1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Mouse4", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "MOUSE4", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "侧键1", StringComparison.OrdinalIgnoreCase))
            {
                return "Mouse4";
            }

            if (string.Equals(token, "XButton2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Mouse5", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "MOUSE5", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "侧键2", StringComparison.OrdinalIgnoreCase))
            {
                return "Mouse5";
            }

            if (token.Length == 1)
            {
                var ch = char.ToUpperInvariant(token[0]);
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
                {
                    return ch.ToString();
                }
            }

            if (token.Length >= 2 &&
                (token[0] == 'F' || token[0] == 'f') &&
                int.TryParse(token.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var functionKey) &&
                functionKey >= 1 &&
                functionKey <= 24)
            {
                return "F" + functionKey.ToString(CultureInfo.InvariantCulture);
            }

            if (string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase))
            {
                return "Ctrl";
            }

            if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase))
            {
                return "Alt";
            }

            if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
            {
                return "Shift";
            }

            if (string.Equals(token, "Space", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "SPACE", StringComparison.OrdinalIgnoreCase))
            {
                return "Space";
            }

            if (string.Equals(token, "Caps", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "CapsLock", StringComparison.OrdinalIgnoreCase))
            {
                return "Caps";
            }

            if (string.Equals(token, "Tab", StringComparison.OrdinalIgnoreCase))
            {
                return "Tab";
            }

            if (string.Equals(token, "Enter", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Return", StringComparison.OrdinalIgnoreCase))
            {
                return "Enter";
            }

            if (string.Equals(token, "Esc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Escape", StringComparison.OrdinalIgnoreCase))
            {
                return "Escape";
            }

            if (string.Equals(token, "Left", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Up", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Right", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Down", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Home", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "End", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Insert", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Delete", StringComparison.OrdinalIgnoreCase))
            {
                return char.ToUpperInvariant(token[0]) + token.Substring(1).ToLowerInvariant();
            }

            if (string.Equals(token, "PageUp", StringComparison.OrdinalIgnoreCase))
            {
                return "PageUp";
            }

            if (string.Equals(token, "PageDown", StringComparison.OrdinalIgnoreCase))
            {
                return "PageDown";
            }

            return string.Empty;
        }

        public static bool IsMouseToken(string token)
        {
            return IsMouseTokenCore(NormalizeToken(token));
        }

        public static bool IsKeyboardToken(string token)
        {
            token = NormalizeToken(token);
            return IsKnownKeyboardToken(token);
        }

        private static bool IsDuplicateForSlot(MapQuickAnnouncementHotkey hotkey, string slotId, string token)
        {
            if (hotkey == null || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!string.Equals(slotId, HotkeySlot1Id, StringComparison.Ordinal) &&
                string.Equals(token, hotkey.Slot1, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(slotId, HotkeySlot2Id, StringComparison.Ordinal) &&
                string.Equals(token, hotkey.Slot2, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.Equals(slotId, HotkeyTriggerId, StringComparison.Ordinal) &&
                   string.Equals(token, hotkey.TriggerKey, StringComparison.Ordinal);
        }

        private static bool IsMouseTokenCore(string token)
        {
            return string.Equals(token, "MouseLeft", StringComparison.Ordinal) ||
                   string.Equals(token, "MouseRight", StringComparison.Ordinal) ||
                   string.Equals(token, "MouseMiddle", StringComparison.Ordinal) ||
                   string.Equals(token, "Mouse4", StringComparison.Ordinal) ||
                   string.Equals(token, "Mouse5", StringComparison.Ordinal);
        }

        private static bool IsKnownKeyboardToken(string token)
        {
            return !string.IsNullOrWhiteSpace(token) && !IsMouseTokenCore(token);
        }
    }

    public sealed class MapQuickAnnouncementHotkey
    {
        public MapQuickAnnouncementHotkey(string slot1, string slot2, string triggerKey)
        {
            Slot1 = slot1 ?? string.Empty;
            Slot2 = slot2 ?? string.Empty;
            TriggerKey = triggerKey ?? string.Empty;
        }

        public string Slot1 { get; private set; }
        public string Slot2 { get; private set; }
        public string TriggerKey { get; private set; }
    }
}
