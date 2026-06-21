using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static void UpdateQuickItemHotkeyCapture(List<QuickItemHotkeyBinding> bindings)
        {
            // Hotkey capture writes configuration only while the game window is focused;
            // it must not synthesize the captured key as gameplay input.
            if (!_quickItemHotkeyCaptureActive ||
                _quickItemHotkeyCaptureBindingIndex < 0 ||
                bindings == null ||
                _quickItemHotkeyCaptureBindingIndex >= bindings.Count ||
                !IsCurrentProcessForeground())
            {
                return;
            }

            var ctrl = IsKeyDown(VkControl);
            var alt = IsKeyDown(VkAlt);
            var shift = IsKeyDown(VkShift);
            if (!ctrl && !alt && !shift && PressedQuickItemCaptureKey(VkEscape))
            {
                StopQuickItemHotkeyCapture();
                return;
            }

            if (PressedQuickItemCaptureKey(VkBackspace))
            {
                bool clearChanged;
                if (ClearQuickItemHotkeyBinding(bindings, _quickItemHotkeyCaptureBindingIndex, out clearChanged) && clearChanged)
                {
                    ConfigService.SaveAll();
                }

                StopQuickItemHotkeyCapture();
                return;
            }

            string token;
            if (!TryCaptureQuickItemPrimaryKeyToken(out token))
            {
                return;
            }

            var normalized = (ctrl ? "Ctrl+" : string.Empty) +
                             (alt ? "Alt+" : string.Empty) +
                             (shift ? "Shift+" : string.Empty) +
                             token;
            var binding = bindings[_quickItemHotkeyCaptureBindingIndex];
            if (binding != null)
            {
                binding.Hotkey = normalized;
                binding.Enabled = true;
                ConfigService.SaveAll();
            }

            StopQuickItemHotkeyCapture();
        }

        private static void UpdateAutoMiningHotkeyCapture()
        {
            if (!_autoMiningHotkeyCaptureActive || !IsCurrentProcessForeground())
            {
                return;
            }

            var ctrl = IsKeyDown(VkControl);
            var alt = IsKeyDown(VkAlt);
            var shift = IsKeyDown(VkShift);
            if (!ctrl && !alt && !shift && PressedCaptureKey(AutoMiningCaptureWasDown, VkEscape))
            {
                StopAutoMiningHotkeyCapture();
                return;
            }

            if (PressedCaptureKey(AutoMiningCaptureWasDown, VkBackspace))
            {
                bool clearChanged;
                if (ClearAutoMiningHotkeyBinding(ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault(), out clearChanged) && clearChanged)
                {
                    ConfigService.SaveAll();
                }

                StopAutoMiningHotkeyCapture();
                return;
            }

            string token;
            if (!TryCaptureHotkeyPrimaryKeyToken(AutoMiningCaptureWasDown, out token))
            {
                return;
            }

            var normalized = (ctrl ? "Ctrl+" : string.Empty) +
                             (alt ? "Alt+" : string.Empty) +
                             (shift ? "Shift+" : string.Empty) +
                             token;
            var hotkeySettings = ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault();
            if (hotkeySettings.HotkeysByFeatureId == null)
            {
                hotkeySettings.HotkeysByFeatureId = new Dictionary<string, string>();
            }

            hotkeySettings.HotkeysByFeatureId[FeatureIds.WorldAutomationAutoMining] = normalized;
            ConfigService.SaveAll();
            StopAutoMiningHotkeyCapture();
        }

        private static bool TryCaptureQuickItemPrimaryKeyToken(out string token)
        {
            return TryCaptureHotkeyPrimaryKeyToken(QuickItemCaptureWasDown, out token);
        }

        private static bool TryCaptureHotkeyPrimaryKeyToken(Dictionary<int, bool> state, out string token)
        {
            token = string.Empty;
            for (var key = 0x41; key <= 0x5A; key++)
            {
                if (PressedCaptureKey(state, key))
                {
                    token = ((char)key).ToString();
                    return true;
                }
            }

            for (var key = 0x30; key <= 0x39; key++)
            {
                if (PressedCaptureKey(state, key))
                {
                    token = ((char)key).ToString();
                    return true;
                }
            }

            for (var key = 0x70; key <= 0x87; key++)
            {
                if (PressedCaptureKey(state, key))
                {
                    token = "F" + (key - 0x6F).ToString(CultureInfo.InvariantCulture);
                    return true;
                }
            }

            for (var index = 0; index < QuickItemCaptureAdditionalKeys.Length; index++)
            {
                var keyCode = QuickItemCaptureAdditionalKeys[index];
                if (!PressedCaptureKey(state, keyCode))
                {
                    continue;
                }

                if (TryGetQuickItemCaptureToken(keyCode, out token))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PressedQuickItemCaptureKey(int keyCode)
        {
            return PressedCaptureKey(QuickItemCaptureWasDown, keyCode);
        }

        private static bool PressedCaptureKey(Dictionary<int, bool> state, int keyCode)
        {
            var isDown = IsKeyDown(keyCode);
            bool wasDown;
            if (state == null)
            {
                return isDown;
            }

            state.TryGetValue(keyCode, out wasDown);
            state[keyCode] = isDown;
            return isDown && !wasDown;
        }

        private static bool IsKeyDown(int keyCode)
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            return (GetAsyncKeyState(keyCode) & 0x8000) != 0;
        }

        private static bool TryGetQuickItemCaptureToken(int keyCode, out string token)
        {
            token = string.Empty;
            switch (keyCode)
            {
                case 0x14:
                    token = "CAPS";
                    return true;
                case 0x20:
                    token = "SPACE";
                    return true;
                case 0x09:
                    token = "TAB";
                    return true;
                case 0x0D:
                    token = "ENTER";
                    return true;
                case 0x1B:
                    token = "ESC";
                    return true;
                case 0x25:
                    token = "LEFT";
                    return true;
                case 0x26:
                    token = "UP";
                    return true;
                case 0x27:
                    token = "RIGHT";
                    return true;
                case 0x28:
                    token = "DOWN";
                    return true;
                case 0x05:
                    token = "MOUSE4";
                    return true;
                case 0x06:
                    token = "MOUSE5";
                    return true;
                default:
                    return false;
            }
        }

        private static bool ClearQuickItemHotkeyBinding(List<QuickItemHotkeyBinding> bindings, int index, out bool changed)
        {
            changed = false;
            if (bindings == null || index < 0 || index >= bindings.Count)
            {
                return false;
            }

            var binding = bindings[index];
            if (binding == null)
            {
                return false;
            }

            changed = !string.IsNullOrWhiteSpace(binding.Hotkey);
            binding.Hotkey = string.Empty;
            return true;
        }

        private static bool ClearAutoMiningHotkeyBinding(HotkeySettings hotkeySettings, out bool changed)
        {
            changed = false;
            hotkeySettings = hotkeySettings ?? HotkeySettings.CreateDefault();
            if (hotkeySettings.HotkeysByFeatureId == null)
            {
                hotkeySettings.HotkeysByFeatureId = new Dictionary<string, string>();
            }

            changed = hotkeySettings.HotkeysByFeatureId.Remove(FeatureIds.WorldAutomationAutoMining);
            return true;
        }

        internal static bool TryClearQuickItemHotkeyBindingForTesting(List<QuickItemHotkeyBinding> bindings, int index, out bool changed)
        {
            return ClearQuickItemHotkeyBinding(bindings, index, out changed);
        }

        internal static bool TryClearAutoMiningHotkeyBindingForTesting(HotkeySettings hotkeySettings, out bool changed)
        {
            return ClearAutoMiningHotkeyBinding(hotkeySettings, out changed);
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
    }
}
