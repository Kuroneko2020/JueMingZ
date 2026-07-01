using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Input.Hotkeys;
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

            var capture = HotkeyCaptureService.Update(QuickItemHotkeyCaptureSession, IsKeyDown);
            if (capture == null || !capture.HasResult)
            {
                return;
            }

            string message;
            bool changed;
            TryApplyUnifiedHotkeyCaptureResult(
                UnifiedHotkeyBindingIds.ForQuickItemSlot(_quickItemHotkeyCaptureBindingIndex),
                capture,
                out message,
                out changed);
            StopQuickItemHotkeyCapture();
        }

        private static void UpdateAutoMiningHotkeyCapture()
        {
            if (!_autoMiningHotkeyCaptureActive || !IsCurrentProcessForeground())
            {
                return;
            }

            var capture = HotkeyCaptureService.Update(AutoMiningHotkeyCaptureSession, IsKeyDown);
            if (capture == null || !capture.HasResult)
            {
                return;
            }

            string message;
            bool changed;
            TryApplyUnifiedHotkeyCaptureResult(
                UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger,
                capture,
                out message,
                out changed);
            StopAutoMiningHotkeyCapture();
        }

        private static bool IsKeyDown(int keyCode)
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            return (GetAsyncKeyState(keyCode) & 0x8000) != 0;
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
