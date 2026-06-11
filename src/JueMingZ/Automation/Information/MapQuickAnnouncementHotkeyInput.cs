using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Config;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementHotkeyTokens
    {
        private static readonly int[] CaptureVirtualKeys =
        {
            0x10,
            0x11,
            0x12,
            0x41,
            0x42,
            0x43,
            0x44,
            0x45,
            0x46,
            0x47,
            0x48,
            0x49,
            0x4A,
            0x4B,
            0x4C,
            0x4D,
            0x4E,
            0x4F,
            0x50,
            0x51,
            0x52,
            0x53,
            0x54,
            0x55,
            0x56,
            0x57,
            0x58,
            0x59,
            0x5A,
            0x30,
            0x31,
            0x32,
            0x33,
            0x34,
            0x35,
            0x36,
            0x37,
            0x38,
            0x39,
            0x70,
            0x71,
            0x72,
            0x73,
            0x74,
            0x75,
            0x76,
            0x77,
            0x78,
            0x79,
            0x7A,
            0x7B,
            0x7C,
            0x7D,
            0x7E,
            0x7F,
            0x80,
            0x81,
            0x82,
            0x83,
            0x84,
            0x85,
            0x86,
            0x87,
            0x14,
            0x20,
            0x09,
            0x0D,
            0x1B,
            0x25,
            0x26,
            0x27,
            0x28,
            0x24,
            0x23,
            0x2D,
            0x2E,
            0x21,
            0x22,
            0x01,
            0x02,
            0x04,
            0x05,
            0x06
        };

        internal static void SeedCaptureState(Dictionary<int, bool> state, Func<int, bool> isKeyDown)
        {
            if (state == null)
            {
                return;
            }

            state.Clear();
            for (var index = 0; index < CaptureVirtualKeys.Length; index++)
            {
                var key = CaptureVirtualKeys[index];
                state[key] = isKeyDown != null && isKeyDown(key);
            }
        }

        internal static bool TryCapturePressedToken(
            Dictionary<int, bool> state,
            Func<int, bool> isKeyDown,
            out string token)
        {
            token = string.Empty;
            for (var index = 0; index < CaptureVirtualKeys.Length; index++)
            {
                var key = CaptureVirtualKeys[index];
                var down = isKeyDown != null && isKeyDown(key);
                bool wasDown;
                if (state == null)
                {
                    wasDown = false;
                }
                else
                {
                    state.TryGetValue(key, out wasDown);
                    state[key] = down;
                }

                if (!down || wasDown || !TryGetTokenFromVirtualKey(key, out token))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        internal static bool TryGetVirtualKey(string token, out int virtualKey)
        {
            virtualKey = 0;
            token = MapQuickAnnouncementSettings.NormalizeTriggerKey(token);
            if (token.Length == 1)
            {
                var ch = token[0];
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
                {
                    virtualKey = ch;
                    return true;
                }
            }

            if (token.Length > 1 && token[0] == 'F')
            {
                int functionKey;
                if (int.TryParse(token.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out functionKey) &&
                    functionKey >= 1 &&
                    functionKey <= 24)
                {
                    virtualKey = 0x6F + functionKey;
                    return true;
                }
            }

            switch (token)
            {
                case "MouseLeft":
                    virtualKey = 0x01;
                    return true;
                case "MouseRight":
                    virtualKey = 0x02;
                    return true;
                case "MouseMiddle":
                    virtualKey = 0x04;
                    return true;
                case "Mouse4":
                    virtualKey = 0x05;
                    return true;
                case "Mouse5":
                    virtualKey = 0x06;
                    return true;
                case "Shift":
                    virtualKey = 0x10;
                    return true;
                case "Ctrl":
                    virtualKey = 0x11;
                    return true;
                case "Alt":
                    virtualKey = 0x12;
                    return true;
                case "Caps":
                    virtualKey = 0x14;
                    return true;
                case "Space":
                    virtualKey = 0x20;
                    return true;
                case "Tab":
                    virtualKey = 0x09;
                    return true;
                case "Enter":
                    virtualKey = 0x0D;
                    return true;
                case "Escape":
                    virtualKey = 0x1B;
                    return true;
                case "Left":
                    virtualKey = 0x25;
                    return true;
                case "Up":
                    virtualKey = 0x26;
                    return true;
                case "Right":
                    virtualKey = 0x27;
                    return true;
                case "Down":
                    virtualKey = 0x28;
                    return true;
                case "Home":
                    virtualKey = 0x24;
                    return true;
                case "End":
                    virtualKey = 0x23;
                    return true;
                case "Insert":
                    virtualKey = 0x2D;
                    return true;
                case "Delete":
                    virtualKey = 0x2E;
                    return true;
                case "PageUp":
                    virtualKey = 0x21;
                    return true;
                case "PageDown":
                    virtualKey = 0x22;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetTokenFromVirtualKey(int virtualKey, out string token)
        {
            token = string.Empty;
            if (virtualKey >= 0x41 && virtualKey <= 0x5A)
            {
                token = ((char)virtualKey).ToString();
                return true;
            }

            if (virtualKey >= 0x30 && virtualKey <= 0x39)
            {
                token = ((char)virtualKey).ToString();
                return true;
            }

            if (virtualKey >= 0x70 && virtualKey <= 0x87)
            {
                token = "F" + (virtualKey - 0x6F).ToString(CultureInfo.InvariantCulture);
                return true;
            }

            switch (virtualKey)
            {
                case 0x01:
                    token = "MouseLeft";
                    return true;
                case 0x02:
                    token = "MouseRight";
                    return true;
                case 0x04:
                    token = "MouseMiddle";
                    return true;
                case 0x05:
                    token = "Mouse4";
                    return true;
                case 0x06:
                    token = "Mouse5";
                    return true;
                case 0x10:
                    token = "Shift";
                    return true;
                case 0x11:
                    token = "Ctrl";
                    return true;
                case 0x12:
                    token = "Alt";
                    return true;
                case 0x14:
                    token = "Caps";
                    return true;
                case 0x20:
                    token = "Space";
                    return true;
                case 0x09:
                    token = "Tab";
                    return true;
                case 0x0D:
                    token = "Enter";
                    return true;
                case 0x1B:
                    token = "Escape";
                    return true;
                case 0x25:
                    token = "Left";
                    return true;
                case 0x26:
                    token = "Up";
                    return true;
                case 0x27:
                    token = "Right";
                    return true;
                case 0x28:
                    token = "Down";
                    return true;
                case 0x24:
                    token = "Home";
                    return true;
                case 0x23:
                    token = "End";
                    return true;
                case 0x2D:
                    token = "Insert";
                    return true;
                case 0x2E:
                    token = "Delete";
                    return true;
                case 0x21:
                    token = "PageUp";
                    return true;
                case 0x22:
                    token = "PageDown";
                    return true;
                default:
                    return false;
            }
        }
    }

    public sealed class MapQuickAnnouncementHotkeyStateMachine
    {
        private string _lastSignature = string.Empty;
        private bool _lastTriggerDown;
        private bool _latchedUntilAnyRelease;

        public MapQuickAnnouncementHotkeyState Update(MapQuickAnnouncementHotkey hotkey, Func<string, bool> isTokenDown)
        {
            hotkey = hotkey == null
                ? MapQuickAnnouncementSettings.NormalizeHotkey(string.Empty, string.Empty, string.Empty)
                : MapQuickAnnouncementSettings.NormalizeHotkey(hotkey.Slot1, hotkey.Slot2, hotkey.TriggerKey);

            var signature = BuildSignature(hotkey);
            if (!string.Equals(signature, _lastSignature, StringComparison.Ordinal))
            {
                _lastSignature = signature;
                _lastTriggerDown = false;
                _latchedUntilAnyRelease = false;
            }

            var valid = !string.IsNullOrWhiteSpace(hotkey.TriggerKey);
            var slot1Down = IsOptionalTokenDown(hotkey.Slot1, isTokenDown);
            var slot2Down = IsOptionalTokenDown(hotkey.Slot2, isTokenDown);
            var triggerDown = IsRequiredTokenDown(hotkey.TriggerKey, isTokenDown);
            var combinationHeld = valid && slot1Down && slot2Down && triggerDown;
            var triggerEdge = triggerDown && !_lastTriggerDown;
            var triggered = combinationHeld && triggerEdge && !_latchedUntilAnyRelease;

            if (!combinationHeld)
            {
                _latchedUntilAnyRelease = false;
            }
            else if (triggered)
            {
                _latchedUntilAnyRelease = true;
            }

            _lastTriggerDown = triggerDown;
            return new MapQuickAnnouncementHotkeyState
            {
                IsValid = valid,
                Slot1Down = slot1Down,
                Slot2Down = slot2Down,
                TriggerDown = triggerDown,
                CombinationHeld = combinationHeld,
                TriggerPressedEdge = triggerEdge,
                Triggered = triggered,
                LatchedUntilRelease = _latchedUntilAnyRelease,
                Signature = signature
            };
        }

        public void Reset()
        {
            _lastSignature = string.Empty;
            _lastTriggerDown = false;
            _latchedUntilAnyRelease = false;
        }

        private static bool IsOptionalTokenDown(string token, Func<string, bool> isTokenDown)
        {
            return string.IsNullOrWhiteSpace(token) || IsRequiredTokenDown(token, isTokenDown);
        }

        private static bool IsRequiredTokenDown(string token, Func<string, bool> isTokenDown)
        {
            return !string.IsNullOrWhiteSpace(token) && isTokenDown != null && isTokenDown(token);
        }

        private static string BuildSignature(MapQuickAnnouncementHotkey hotkey)
        {
            return (hotkey == null ? string.Empty : hotkey.Slot1 ?? string.Empty) + "|" +
                   (hotkey == null ? string.Empty : hotkey.Slot2 ?? string.Empty) + "|" +
                   (hotkey == null ? string.Empty : hotkey.TriggerKey ?? string.Empty);
        }
    }

    public sealed class MapQuickAnnouncementHotkeyState
    {
        public bool IsValid { get; set; }
        public bool Slot1Down { get; set; }
        public bool Slot2Down { get; set; }
        public bool TriggerDown { get; set; }
        public bool CombinationHeld { get; set; }
        public bool TriggerPressedEdge { get; set; }
        public bool Triggered { get; set; }
        public bool LatchedUntilRelease { get; set; }
        public string Signature { get; set; }
    }
}
