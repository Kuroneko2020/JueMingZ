using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static void UpdateBlueprintEntryHotkeyCapture()
        {
            if (!_blueprintEntryHotkeyCaptureActive || !IsCurrentProcessForeground())
            {
                return;
            }

            if (PressedCaptureKey(BlueprintEntryCaptureWasDown, 0x1B))
            {
                _blueprintEntryHotkeyMessage = "已取消录入";
                StopBlueprintEntryHotkeyCapture();
                return;
            }

            string primaryKey;
            if (!TryCaptureBlueprintEntryPrimaryKeyToken(out primaryKey))
            {
                return;
            }

            var parts = new List<string>(2);
            if (IsKeyDown(VkAlt))
            {
                parts.Add("Alt");
            }

            if (IsKeyDown(VkControl))
            {
                parts.Add("Ctrl");
            }

            if (IsKeyDown(VkShift))
            {
                parts.Add("Shift");
            }

            if (parts.Count > 1)
            {
                _blueprintEntryHotkeyMessage = "只支持一个修饰键 + 一个主键";
                StopBlueprintEntryHotkeyCapture();
                return;
            }

            parts.Add(primaryKey);
            FeatureToggleHotkeyChord chord;
            if (!FeatureToggleHotkeyChord.TryParseParts(parts, out chord))
            {
                _blueprintEntryHotkeyMessage = "不支持这个组合";
                StopBlueprintEntryHotkeyCapture();
                return;
            }

            string message;
            bool changed;
            if (!TrySaveBlueprintEntryHotkey(ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault(), ConfigService.AppSettings ?? AppSettings.CreateDefault(), chord.Normalized, out message, out changed))
            {
                _blueprintEntryHotkeyMessage = message;
                StopBlueprintEntryHotkeyCapture();
                return;
            }

            if (changed)
            {
                ConfigService.SaveAll();
            }

            _blueprintEntryHotkeyMessage = message;
            StopBlueprintEntryHotkeyCapture();
        }

        private static void SeedBlueprintEntryHotkeyCaptureState()
        {
            BlueprintEntryCaptureWasDown.Clear();
            BlueprintEntryCaptureWasDown[VkAlt] = IsKeyDown(VkAlt);
            BlueprintEntryCaptureWasDown[VkControl] = IsKeyDown(VkControl);
            BlueprintEntryCaptureWasDown[VkShift] = IsKeyDown(VkShift);
            BlueprintEntryCaptureWasDown[0x1B] = IsKeyDown(0x1B);
            for (var key = 0x41; key <= 0x5A; key++)
            {
                BlueprintEntryCaptureWasDown[key] = IsKeyDown(key);
            }

            for (var key = 0x30; key <= 0x39; key++)
            {
                BlueprintEntryCaptureWasDown[key] = IsKeyDown(key);
            }

            for (var key = 0x70; key <= 0x87; key++)
            {
                BlueprintEntryCaptureWasDown[key] = IsKeyDown(key);
            }
        }

        private static bool TryCaptureBlueprintEntryPrimaryKeyToken(out string token)
        {
            token = string.Empty;
            for (var key = 0x41; key <= 0x5A; key++)
            {
                if (PressedCaptureKey(BlueprintEntryCaptureWasDown, key))
                {
                    token = ((char)key).ToString();
                    return true;
                }
            }

            for (var key = 0x30; key <= 0x39; key++)
            {
                if (PressedCaptureKey(BlueprintEntryCaptureWasDown, key))
                {
                    token = ((char)key).ToString();
                    return true;
                }
            }

            for (var key = 0x70; key <= 0x87; key++)
            {
                if (PressedCaptureKey(BlueprintEntryCaptureWasDown, key))
                {
                    token = "F" + (key - 0x6F).ToString(CultureInfo.InvariantCulture);
                    return true;
                }
            }

            return false;
        }

        private static bool TrySaveBlueprintEntryHotkey(
            HotkeySettings hotkeySettings,
            AppSettings appSettings,
            string chordText,
            out string message,
            out bool changed)
        {
            changed = false;
            message = string.Empty;
            FeatureToggleHotkeyChord chord;
            if (!FeatureToggleHotkeyChord.TryParse(chordText, out chord))
            {
                message = "不支持这个组合";
                return false;
            }

            hotkeySettings = hotkeySettings ?? HotkeySettings.CreateDefault();
            if (hotkeySettings.HotkeysByFeatureId == null)
            {
                hotkeySettings.HotkeysByFeatureId = new Dictionary<string, string>();
            }

            FeatureToggleHotkeyConflict conflict;
            if (FeatureToggleHotkeyConflictRegistry.TryFindConflict(hotkeySettings, appSettings ?? AppSettings.CreateDefault(), FeatureIds.BlueprintMain, chord, out conflict))
            {
                message = BuildFeatureToggleHotkeyConflictMessage(conflict);
                return false;
            }

            var old = GetBlueprintEntryHotkeyDisplay(hotkeySettings);
            changed = !string.Equals(old, chord.Normalized, StringComparison.Ordinal);
            hotkeySettings.HotkeysByFeatureId[FeatureIds.BlueprintMain] = chord.Normalized;
            message = changed ? "已保存 " + chord.Display : "未变化";
            return true;
        }

        internal static bool TryApplyBlueprintEntryHotkeyForTesting(
            HotkeySettings hotkeySettings,
            AppSettings appSettings,
            string chordText,
            out string message,
            out bool changed)
        {
            return TrySaveBlueprintEntryHotkey(hotkeySettings, appSettings, chordText, out message, out changed);
        }
    }
}
