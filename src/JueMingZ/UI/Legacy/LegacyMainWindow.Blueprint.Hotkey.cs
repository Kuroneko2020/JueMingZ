using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Input.Hotkeys;

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
            var capture = HotkeyCaptureService.Update(BlueprintEntryHotkeyCaptureSession, IsKeyDown);
            if (capture == null || !capture.HasResult)
            {
                return;
            }

            string message;
            bool changed;
            TryApplyUnifiedHotkeyCaptureResult(UnifiedHotkeyBindingIds.ForBlueprintAction(targetId), capture, out message, out changed);
            _blueprintEntryHotkeyMessage = message;
            StopBlueprintEntryHotkeyCapture();
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

            if (string.Equals(targetId, FeatureIds.BlueprintLibraryAction, StringComparison.OrdinalIgnoreCase))
            {
                return FeatureIds.BlueprintLibraryAction;
            }

            if (string.Equals(targetId, FeatureIds.BlueprintMoveAction, StringComparison.OrdinalIgnoreCase))
            {
                return FeatureIds.BlueprintMoveAction;
            }

            if (string.Equals(targetId, FeatureIds.BlueprintRegionAction, StringComparison.OrdinalIgnoreCase))
            {
                return FeatureIds.BlueprintRegionAction;
            }

            if (string.Equals(targetId, FeatureIds.BlueprintMirrorAction, StringComparison.OrdinalIgnoreCase))
            {
                return FeatureIds.BlueprintMirrorAction;
            }

            return string.Empty;
        }

        private static bool IsBlueprintActionHotkeyTargetId(string targetId)
        {
            var normalized = NormalizeBlueprintHotkeyTargetId(targetId);
            return string.Equals(normalized, FeatureIds.BlueprintCreateAction, StringComparison.Ordinal) ||
                   string.Equals(normalized, FeatureIds.BlueprintSaveAction, StringComparison.Ordinal) ||
                   string.Equals(normalized, FeatureIds.BlueprintMoveAction, StringComparison.Ordinal) ||
                   string.Equals(normalized, FeatureIds.BlueprintRegionAction, StringComparison.Ordinal) ||
                   string.Equals(normalized, FeatureIds.BlueprintMirrorAction, StringComparison.Ordinal) ||
                   string.Equals(normalized, FeatureIds.BlueprintLibraryAction, StringComparison.Ordinal);
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
