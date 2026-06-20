using System;
using System.Collections.Generic;
using JueMingZ.Common;

namespace JueMingZ.Config
{
    public static class FeatureToggleHotkeyConflictRegistry
    {
        private static readonly ReservedHotkey[] ReservedHotkeys =
        {
            new ReservedHotkey("F5", "F5 主窗口热键", FeatureToggleHotkeyConflictType.LegacyMainWindow),
            new ReservedHotkey("Ctrl+Alt+D", "诊断保留热键 Ctrl+Alt+D", FeatureToggleHotkeyConflictType.DiagnosticReserved),
            new ReservedHotkey("Ctrl+Alt+J", "诊断保留热键 Ctrl+Alt+J", FeatureToggleHotkeyConflictType.DiagnosticReserved),
            new ReservedHotkey("Ctrl+Alt+K", "诊断保留热键 Ctrl+Alt+K", FeatureToggleHotkeyConflictType.DiagnosticReserved),
            new ReservedHotkey("Ctrl+Alt+L", "诊断保留热键 Ctrl+Alt+L", FeatureToggleHotkeyConflictType.DiagnosticReserved),
            new ReservedHotkey("Ctrl+Alt+U", "诊断保留热键 Ctrl+Alt+U", FeatureToggleHotkeyConflictType.DiagnosticReserved),
            new ReservedHotkey("Ctrl+Alt+T", "诊断保留热键 Ctrl+Alt+T", FeatureToggleHotkeyConflictType.DiagnosticReserved)
        };

        public static bool TryFindConflict(
            HotkeySettings hotkeySettings,
            AppSettings appSettings,
            string currentTargetId,
            string chordText,
            out FeatureToggleHotkeyConflict conflict)
        {
            conflict = null;
            FeatureToggleHotkeyChord chord;
            if (!FeatureToggleHotkeyChord.TryParse(chordText, out chord))
            {
                return false;
            }

            return TryFindConflict(hotkeySettings, appSettings, currentTargetId, chord, out conflict);
        }

        public static bool TryFindConflict(
            HotkeySettings hotkeySettings,
            AppSettings appSettings,
            string currentTargetId,
            FeatureToggleHotkeyChord chord,
            out FeatureToggleHotkeyConflict conflict)
        {
            conflict = null;
            if (chord == null)
            {
                return false;
            }

            var normalizedTargetId = NormalizeTargetIdForComparison(currentTargetId);
            if (TryFindToggleHotkeyConflict(hotkeySettings, normalizedTargetId, chord, out conflict))
            {
                return true;
            }

            if (TryFindQuickItemConflict(hotkeySettings, chord, out conflict))
            {
                return true;
            }

            if (TryFindAutoMiningTriggerConflict(hotkeySettings, chord, out conflict))
            {
                return true;
            }

            if (TryFindBlueprintEntryConflict(hotkeySettings, currentTargetId, chord, out conflict))
            {
                return true;
            }

            if (TryFindQuickAnnouncementConflict(appSettings, chord, out conflict))
            {
                return true;
            }

            return TryFindReservedConflict(chord, out conflict);
        }

        private static bool TryFindToggleHotkeyConflict(
            HotkeySettings hotkeySettings,
            string currentTargetId,
            FeatureToggleHotkeyChord chord,
            out FeatureToggleHotkeyConflict conflict)
        {
            conflict = null;
            var bindings = hotkeySettings == null ? null : hotkeySettings.ToggleHotkeysByTargetId;
            if (bindings == null || bindings.Count <= 0)
            {
                return false;
            }

            foreach (var pair in bindings)
            {
                var ownerTargetId = NormalizeTargetIdForComparison(pair.Key);
                if (ownerTargetId.Length <= 0 ||
                    string.Equals(ownerTargetId, currentTargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                string normalized;
                if (FeatureToggleHotkeyChord.TryNormalize(pair.Value, out normalized) &&
                    string.Equals(normalized, chord.Normalized, StringComparison.Ordinal))
                {
                    conflict = new FeatureToggleHotkeyConflict(
                        FeatureToggleHotkeyConflictType.FeatureToggle,
                        ownerTargetId,
                        FeatureToggleHotkeyTargetCatalog.GetDisplayName(ownerTargetId),
                        chord.Normalized);
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindQuickItemConflict(
            HotkeySettings hotkeySettings,
            FeatureToggleHotkeyChord chord,
            out FeatureToggleHotkeyConflict conflict)
        {
            conflict = null;
            var bindings = hotkeySettings == null ? null : hotkeySettings.QuickItemHotkeyBindings;
            if (bindings == null || bindings.Count <= 0)
            {
                return false;
            }

            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding == null || !binding.Enabled)
                {
                    continue;
                }

                string normalized;
                if (FeatureToggleHotkeyChord.TryNormalize(binding.Hotkey, out normalized) &&
                    string.Equals(normalized, chord.Normalized, StringComparison.Ordinal))
                {
                    conflict = new FeatureToggleHotkeyConflict(
                        FeatureToggleHotkeyConflictType.QuickItem,
                        string.Empty,
                        BuildQuickItemOwnerName(binding),
                        chord.Normalized);
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindAutoMiningTriggerConflict(
            HotkeySettings hotkeySettings,
            FeatureToggleHotkeyChord chord,
            out FeatureToggleHotkeyConflict conflict)
        {
            conflict = null;
            var hotkeys = hotkeySettings == null ? null : hotkeySettings.HotkeysByFeatureId;
            if (hotkeys == null || hotkeys.Count <= 0)
            {
                return false;
            }

            string miningTriggerHotkey;
            if (!hotkeys.TryGetValue(FeatureIds.WorldAutomationAutoMining, out miningTriggerHotkey))
            {
                return false;
            }

            // This legacy field remains the auto-mining collection trigger, not
            // the new feature-toggle hotkey storage.
            string normalized;
            if (FeatureToggleHotkeyChord.TryNormalize(miningTriggerHotkey, out normalized) &&
                string.Equals(normalized, chord.Normalized, StringComparison.Ordinal))
            {
                conflict = new FeatureToggleHotkeyConflict(
                    FeatureToggleHotkeyConflictType.AutoMiningTrigger,
                    FeatureIds.WorldAutomationAutoMining,
                    "自动挖矿 的采集按键",
                    chord.Normalized);
                return true;
            }

            return false;
        }

        private static bool TryFindBlueprintEntryConflict(
            HotkeySettings hotkeySettings,
            string currentTargetId,
            FeatureToggleHotkeyChord chord,
            out FeatureToggleHotkeyConflict conflict)
        {
            conflict = null;
            if (string.Equals(currentTargetId, FeatureIds.BlueprintMain, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var hotkeys = hotkeySettings == null ? null : hotkeySettings.HotkeysByFeatureId;
            if (hotkeys == null || hotkeys.Count <= 0)
            {
                return false;
            }

            string blueprintHotkey;
            if (!hotkeys.TryGetValue(FeatureIds.BlueprintMain, out blueprintHotkey))
            {
                return false;
            }

            string normalized;
            if (FeatureToggleHotkeyChord.TryNormalize(blueprintHotkey, out normalized) &&
                string.Equals(normalized, chord.Normalized, StringComparison.Ordinal))
            {
                conflict = new FeatureToggleHotkeyConflict(
                    FeatureToggleHotkeyConflictType.BlueprintEntry,
                    FeatureIds.BlueprintMain,
                    "蓝图入口快捷键",
                    chord.Normalized);
                return true;
            }

            return false;
        }

        private static bool TryFindQuickAnnouncementConflict(
            AppSettings appSettings,
            FeatureToggleHotkeyChord chord,
            out FeatureToggleHotkeyConflict conflict)
        {
            conflict = null;
            if (appSettings == null)
            {
                return false;
            }

            var parts = new List<string>();
            AddNonEmpty(parts, appSettings.MapQuickAnnouncementHotkeySlot1);
            AddNonEmpty(parts, appSettings.MapQuickAnnouncementHotkeySlot2);
            AddNonEmpty(parts, appSettings.MapQuickAnnouncementTriggerKey);

            FeatureToggleHotkeyChord quickAnnouncementChord;
            if (FeatureToggleHotkeyChord.TryParseParts(parts, out quickAnnouncementChord) &&
                string.Equals(quickAnnouncementChord.Normalized, chord.Normalized, StringComparison.Ordinal))
            {
                conflict = new FeatureToggleHotkeyConflict(
                    FeatureToggleHotkeyConflictType.QuickAnnouncement,
                    FeatureIds.MapQuickAnnouncement,
                    "快捷宣告",
                    chord.Normalized);
                return true;
            }

            return false;
        }

        private static bool TryFindReservedConflict(FeatureToggleHotkeyChord chord, out FeatureToggleHotkeyConflict conflict)
        {
            conflict = null;
            for (var index = 0; index < ReservedHotkeys.Length; index++)
            {
                var reserved = ReservedHotkeys[index];
                string normalized;
                if (FeatureToggleHotkeyChord.TryNormalize(reserved.Chord, out normalized) &&
                    string.Equals(normalized, chord.Normalized, StringComparison.Ordinal))
                {
                    conflict = new FeatureToggleHotkeyConflict(
                        reserved.ConflictType,
                        string.Empty,
                        reserved.DisplayName,
                        chord.Normalized);
                    return true;
                }
            }

            return false;
        }

        private static void AddNonEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value.Trim());
            }
        }

        private static string NormalizeTargetIdForComparison(string targetId)
        {
            string normalized;
            return FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalized)
                ? normalized
                : string.Empty;
        }

        private static string BuildQuickItemOwnerName(QuickItemHotkeyBinding binding)
        {
            if (binding != null && !string.IsNullOrWhiteSpace(binding.DisplayName))
            {
                return "快捷物品 " + binding.DisplayName.Trim();
            }

            return "快捷物品绑定";
        }

        private sealed class ReservedHotkey
        {
            public ReservedHotkey(string chord, string displayName, FeatureToggleHotkeyConflictType conflictType)
            {
                Chord = chord ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                ConflictType = conflictType;
            }

            public string Chord { get; private set; }
            public string DisplayName { get; private set; }
            public FeatureToggleHotkeyConflictType ConflictType { get; private set; }
        }
    }

    public sealed class FeatureToggleHotkeyConflict
    {
        internal FeatureToggleHotkeyConflict(
            FeatureToggleHotkeyConflictType conflictType,
            string ownerTargetId,
            string ownerDisplayName,
            string chord)
        {
            ConflictType = conflictType;
            OwnerTargetId = ownerTargetId ?? string.Empty;
            OwnerDisplayName = ownerDisplayName ?? string.Empty;
            Chord = chord ?? string.Empty;
        }

        public FeatureToggleHotkeyConflictType ConflictType { get; private set; }
        public string OwnerTargetId { get; private set; }
        public string OwnerDisplayName { get; private set; }
        public string Chord { get; private set; }
    }

    public enum FeatureToggleHotkeyConflictType
    {
        FeatureToggle,
        QuickItem,
        AutoMiningTrigger,
        BlueprintEntry,
        QuickAnnouncement,
        LegacyMainWindow,
        DiagnosticReserved
    }
}
