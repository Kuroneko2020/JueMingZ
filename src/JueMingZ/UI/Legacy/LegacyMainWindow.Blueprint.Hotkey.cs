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

            var targetId = NormalizeBlueprintHotkeyTargetId(_blueprintHotkeyCaptureTargetId);
            if (PressedCaptureKey(BlueprintEntryCaptureWasDown, VkEscape))
            {
                _blueprintEntryHotkeyMessage = "已取消录入";
                StopBlueprintEntryHotkeyCapture();
                return;
            }

            if (PressedCaptureKey(BlueprintEntryCaptureWasDown, VkBackspace))
            {
                var hotkeySettings = ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault();
                bool clearChanged;
                ClearBlueprintHotkeyBinding(hotkeySettings, targetId, out clearChanged);
                if (clearChanged)
                {
                    ConfigService.SaveAll();
                }

                _blueprintEntryHotkeyMessage = clearChanged ? "已清除绑定" : "当前未绑定";
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
            if (!TrySaveBlueprintHotkey(ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault(), ConfigService.AppSettings ?? AppSettings.CreateDefault(), targetId, chord.Normalized, out message, out changed))
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
            BlueprintEntryCaptureWasDown[VkBackspace] = IsKeyDown(VkBackspace);
            BlueprintEntryCaptureWasDown[VkEscape] = IsKeyDown(VkEscape);
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
            message = "蓝图页直接打开快捷键已停用";
            return false;
        }

        private static bool TrySaveBlueprintActionHotkey(
            HotkeySettings hotkeySettings,
            AppSettings appSettings,
            string targetId,
            string chordText,
            out string message,
            out bool changed)
        {
            return TrySaveBlueprintHotkey(
                hotkeySettings,
                appSettings,
                NormalizeBlueprintHotkeyTargetId(targetId),
                chordText,
                out message,
                out changed);
        }

        private static bool TrySaveBlueprintHotkey(
            HotkeySettings hotkeySettings,
            AppSettings appSettings,
            string targetId,
            string chordText,
            out string message,
            out bool changed)
        {
            changed = false;
            message = string.Empty;
            targetId = NormalizeBlueprintHotkeyTargetId(targetId);
            if (targetId.Length <= 0)
            {
                message = "不支持这个蓝图快捷键目标";
                return false;
            }

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
            if (FeatureToggleHotkeyConflictRegistry.TryFindConflict(hotkeySettings, appSettings ?? AppSettings.CreateDefault(), targetId, chord, out conflict))
            {
                message = BuildFeatureToggleHotkeyConflictMessage(conflict);
                return false;
            }

            var old = GetBlueprintHotkeyDisplay(hotkeySettings, targetId);
            changed = !string.Equals(old, chord.Normalized, StringComparison.Ordinal);
            hotkeySettings.HotkeysByFeatureId[targetId] = chord.Normalized;
            message = changed ? "已保存 " + chord.Display : "未变化";
            return true;
        }

        private static bool ClearBlueprintHotkeyBinding(HotkeySettings hotkeySettings, string targetId, out bool changed)
        {
            changed = false;
            targetId = NormalizeBlueprintHotkeyTargetId(targetId);
            if (targetId.Length <= 0)
            {
                return false;
            }

            hotkeySettings = hotkeySettings ?? HotkeySettings.CreateDefault();
            if (hotkeySettings.HotkeysByFeatureId == null)
            {
                hotkeySettings.HotkeysByFeatureId = new Dictionary<string, string>();
            }

            changed = hotkeySettings.HotkeysByFeatureId.Remove(targetId);
            return true;
        }

        private static string NormalizeBlueprintHotkeyTargetId(string targetId)
        {
            if (string.Equals(targetId, FeatureIds.BlueprintCreateAction, StringComparison.OrdinalIgnoreCase))
            {
                return FeatureIds.BlueprintCreateAction;
            }

            if (string.Equals(targetId, FeatureIds.BlueprintSaveAction, StringComparison.OrdinalIgnoreCase))
            {
                return FeatureIds.BlueprintSaveAction;
            }

            return string.Empty;
        }

        private static bool IsBlueprintActionHotkeyTargetId(string targetId)
        {
            var normalized = NormalizeBlueprintHotkeyTargetId(targetId);
            return string.Equals(normalized, FeatureIds.BlueprintCreateAction, StringComparison.Ordinal) ||
                   string.Equals(normalized, FeatureIds.BlueprintSaveAction, StringComparison.Ordinal);
        }

        private static string GetBlueprintHotkeyDisplay(HotkeySettings settings, string targetId)
        {
            var hotkeys = settings == null ? null : settings.HotkeysByFeatureId;
            if (hotkeys == null)
            {
                return string.Empty;
            }

            string value;
            string normalized;
            return hotkeys.TryGetValue(NormalizeBlueprintHotkeyTargetId(targetId), out value) &&
                   FeatureToggleHotkeyChord.TryNormalize(value, out normalized)
                ? normalized
                : string.Empty;
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

        internal static bool TryApplyBlueprintActionHotkeyForTesting(
            HotkeySettings hotkeySettings,
            AppSettings appSettings,
            string targetId,
            string chordText,
            out string message,
            out bool changed)
        {
            return TrySaveBlueprintActionHotkey(hotkeySettings, appSettings, targetId, chordText, out message, out changed);
        }

        internal static bool TryClearBlueprintActionHotkeyForTesting(HotkeySettings hotkeySettings, string targetId, out bool changed)
        {
            return ClearBlueprintHotkeyBinding(hotkeySettings, targetId, out changed);
        }
    }
}
