using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Input;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private const int FeatureToggleVkAlt = 0x12;
        private const int FeatureToggleVkK = 0x4B;
        private const int FeatureToggleVkM = 0x4D;

        private static void FeatureToggleHotkeySettingsDefaultEmpty()
        {
            var settings = HotkeySettings.CreateDefault();
            if (settings.ConfigVersion != 4)
            {
                throw new InvalidOperationException("Expected hotkey settings config version 4.");
            }

            if (settings.ToggleHotkeysByTargetId == null || settings.ToggleHotkeysByTargetId.Count != 0)
            {
                throw new InvalidOperationException("Expected feature toggle hotkeys to default to an empty dictionary.");
            }

            if (settings.LastNonOffModeByTargetId == null || settings.LastNonOffModeByTargetId.Count != 0)
            {
                throw new InvalidOperationException("Expected feature toggle last modes to default to an empty dictionary.");
            }
        }

        private static void FeatureToggleHotkeySettingsMigrationPreservesLegacyHotkeys()
        {
            var restore = PushTemporaryConfigDirectory("feature-toggle-hotkey-migration");
            try
            {
                WriteConfigJson(ConfigService.AppSettingsPath, AppSettings.CreateDefault());
                WriteConfigJson(ConfigService.FeatureSettingsPath, FeatureSettings.CreateDefault());
                var oldSettings = HotkeySettings.CreateDefault();
                oldSettings.ConfigVersion = 3;
                oldSettings.HotkeysByFeatureId[FeatureIds.WorldAutomationAutoMining] = "Ctrl+G";
                oldSettings.QuickItemHotkeyBindings.Add(new QuickItemHotkeyBinding
                {
                    Hotkey = "Alt+K",
                    ItemTypes = new List<int> { 4263 },
                    DisplayName = "回家",
                    Enabled = true
                });
                oldSettings.ToggleHotkeysByTargetId = null;
                oldSettings.LastNonOffModeByTargetId = null;
                WriteConfigJson(ConfigService.HotkeySettingsPath, oldSettings);

                ConfigService.Initialize();

                var settings = ConfigService.HotkeySettings;
                if (settings.ConfigVersion != 4)
                {
                    throw new InvalidOperationException("Expected hotkey config migration to version 4.");
                }

                if (settings.ToggleHotkeysByTargetId == null || settings.ToggleHotkeysByTargetId.Count != 0)
                {
                    throw new InvalidOperationException("Expected null toggle hotkey map to migrate to an empty dictionary.");
                }

                if (settings.LastNonOffModeByTargetId == null || settings.LastNonOffModeByTargetId.Count != 0)
                {
                    throw new InvalidOperationException("Expected null last-mode map to migrate to an empty dictionary.");
                }

                AssertStringEquals(
                    settings.HotkeysByFeatureId[FeatureIds.WorldAutomationAutoMining],
                    "Ctrl+G",
                    "auto mining collection hotkey preserved");
                if (settings.QuickItemHotkeyBindings == null ||
                    settings.QuickItemHotkeyBindings.Count != 1 ||
                    !string.Equals(settings.QuickItemHotkeyBindings[0].Hotkey, "Alt+K", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected quick item hotkey bindings to be preserved by feature-toggle migration.");
                }
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void FeatureToggleHotkeySettingsMigrationNormalizesNewFields()
        {
            var restore = PushTemporaryConfigDirectory("feature-toggle-hotkey-normalize");
            try
            {
                WriteConfigJson(ConfigService.AppSettingsPath, AppSettings.CreateDefault());
                WriteConfigJson(ConfigService.FeatureSettingsPath, FeatureSettings.CreateDefault());
                var settingsToNormalize = HotkeySettings.CreateDefault();
                settingsToNormalize.ToggleHotkeysByTargetId = new Dictionary<string, string>
                {
                    { "BUFF.AUTO_HEAL", "alt+k" },
                    { FeatureIds.CombatAutoClicker, "Ctrl+Alt+K" },
                    { "unknown.target", "F9" },
                    { FeatureIds.InventoryAutoStack, string.Empty }
                };
                settingsToNormalize.LastNonOffModeByTargetId = new Dictionary<string, string>
                {
                    { FeatureIds.WorldAutomationAutoMining, "hotkey" },
                    { FeatureIds.MovementContinuousDash, "DoubleTapAndHold" },
                    { FeatureIds.InventoryAutoStack, "Auto" },
                    { "buff.auto_mana", "Off" }
                };
                WriteConfigJson(ConfigService.HotkeySettingsPath, settingsToNormalize);

                ConfigService.Initialize();

                var settings = ConfigService.HotkeySettings;
                AssertStringEquals(settings.ToggleHotkeysByTargetId["buff.auto_heal"], "Alt+K", "normalized toggle hotkey chord");
                if (settings.ToggleHotkeysByTargetId.Count != 1)
                {
                    throw new InvalidOperationException("Expected invalid or unknown toggle hotkeys to be dropped.");
                }

                AssertStringEquals(settings.LastNonOffModeByTargetId[FeatureIds.WorldAutomationAutoMining], "Hotkey", "auto mining last mode");
                AssertStringEquals(settings.LastNonOffModeByTargetId[FeatureIds.MovementContinuousDash], "DoubleTapAndHold", "continuous dash last mode");
                if (settings.LastNonOffModeByTargetId.Count != 2)
                {
                    throw new InvalidOperationException("Expected invalid or off last modes to be dropped.");
                }
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void FeatureToggleHotkeyChordNormalizesAllowedCombinations()
        {
            AssertNormalizedFeatureToggleChord("F9", "F9");
            AssertNormalizedFeatureToggleChord("k", "K");
            AssertNormalizedFeatureToggleChord("0", "0");
            AssertNormalizedFeatureToggleChord("alt+k", "Alt+K");
            AssertNormalizedFeatureToggleChord("Ctrl+f8", "Ctrl+F8");
            AssertNormalizedFeatureToggleChord("shift+r", "Shift+R");
        }

        private static void FeatureToggleHotkeyChordRejectsUnsupportedCombinations()
        {
            AssertInvalidFeatureToggleChord("Alt");
            AssertInvalidFeatureToggleChord("Ctrl");
            AssertInvalidFeatureToggleChord("Shift");
            AssertInvalidFeatureToggleChord("Esc");
            AssertInvalidFeatureToggleChord("Escape");
            AssertInvalidFeatureToggleChord("Ctrl+Alt+K");
            AssertInvalidFeatureToggleChord("Alt+Shift+R");
            AssertInvalidFeatureToggleChord("A+B");
            AssertInvalidFeatureToggleChord("MouseLeft");
        }

        private static void FeatureToggleHotkeyTargetCatalogNormalizesLastModesFailClosed()
        {
            string mode;
            if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeLastNonOffMode(FeatureIds.WorldAutomationAutoMining, "hotkey", out mode) ||
                !string.Equals(mode, "Hotkey", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected auto mining last mode to normalize to Hotkey.");
            }

            if (FeatureToggleHotkeyTargetCatalog.TryNormalizeLastNonOffMode(FeatureIds.WorldAutomationAutoMining, "Off", out mode))
            {
                throw new InvalidOperationException("Expected off mode to be rejected from LastNonOffModeByTargetId.");
            }

            if (FeatureToggleHotkeyTargetCatalog.TryNormalizeLastNonOffMode(FeatureIds.InventoryAutoStack, "Auto", out mode))
            {
                throw new InvalidOperationException("Expected binary targets to reject last-mode records.");
            }
        }

        private static void FeatureToggleHotkeyConflictRegistryReportsSources()
        {
            AssertFeatureToggleConflict(
                BuildHotkeySettingsWithToggle(FeatureIds.InventoryAutoStack, "Alt+K"),
                AppSettings.CreateDefault(),
                "buff.auto_heal",
                "alt+k",
                FeatureToggleHotkeyConflictType.FeatureToggle,
                "自动堆叠");

            var quickItemSettings = HotkeySettings.CreateDefault();
            quickItemSettings.QuickItemHotkeyBindings.Add(new QuickItemHotkeyBinding
            {
                Hotkey = "Shift+R",
                DisplayName = "回家",
                ItemTypes = new List<int> { 4263 },
                Enabled = true
            });
            AssertFeatureToggleConflict(
                quickItemSettings,
                AppSettings.CreateDefault(),
                "buff.auto_heal",
                "shift+r",
                FeatureToggleHotkeyConflictType.QuickItem,
                "快捷物品 回家");

            var autoMiningSettings = HotkeySettings.CreateDefault();
            autoMiningSettings.HotkeysByFeatureId[FeatureIds.WorldAutomationAutoMining] = "Ctrl+F8";
            AssertFeatureToggleConflict(
                autoMiningSettings,
                AppSettings.CreateDefault(),
                FeatureIds.WorldAutomationAutoMining,
                "Ctrl+F8",
                FeatureToggleHotkeyConflictType.AutoMiningTrigger,
                "自动挖矿 的采集按键");

            var quickAnnouncementSettings = AppSettings.CreateDefault();
            quickAnnouncementSettings.MapQuickAnnouncementHotkeySlot1 = "Alt";
            quickAnnouncementSettings.MapQuickAnnouncementHotkeySlot2 = string.Empty;
            quickAnnouncementSettings.MapQuickAnnouncementTriggerKey = "K";
            AssertFeatureToggleConflict(
                HotkeySettings.CreateDefault(),
                quickAnnouncementSettings,
                "buff.auto_heal",
                "Alt+K",
                FeatureToggleHotkeyConflictType.QuickAnnouncement,
                "快捷宣告");

            AssertFeatureToggleConflict(
                HotkeySettings.CreateDefault(),
                AppSettings.CreateDefault(),
                "buff.auto_heal",
                "F5",
                FeatureToggleHotkeyConflictType.LegacyMainWindow,
                "F5 主窗口热键");
        }

        private static void FeatureToggleHotkeyConflictRegistrySkipsSelfTarget()
        {
            var settings = BuildHotkeySettingsWithToggle("buff.auto_heal", "Alt+K");
            FeatureToggleHotkeyConflict conflict;
            if (FeatureToggleHotkeyConflictRegistry.TryFindConflict(
                    settings,
                    AppSettings.CreateDefault(),
                    "buff.auto_heal",
                    "alt+k",
                    out conflict))
            {
                throw new InvalidOperationException("Expected same target to be exempt from its current feature-toggle hotkey.");
            }
        }

        private static void FeatureToggleHotkeyUiReserveAndIconContract()
        {
            AssertStringEquals(LegacyMainWindow.GetFeatureToggleHotkeyIconIdForTesting(), "keyboard", "feature toggle hotkey icon");
            if (LegacyMainWindow.GetFeatureToggleHotkeyReserveWidthForTesting() != 30)
            {
                throw new InvalidOperationException("Expected feature toggle hotkey rows to reserve 30px.");
            }

            var row = new LegacyUiRect(10, 20, 500, 32);
            var button = LegacyMainWindow.CalculateFeatureToggleHotkeyButtonRectForTesting(row, "buff.auto_heal");
            if (button.Width != 24 || button.Height != 24)
            {
                throw new InvalidOperationException("Expected feature toggle hotkey icon button to be 24x24.");
            }

            if (button.Right != row.Right - 10)
            {
                throw new InvalidOperationException("Expected feature toggle hotkey icon to sit at the row end.");
            }

            if (LegacyMainWindow.GetFeatureToggleModeGroupRightForTesting(row, "buff.auto_heal") != row.Right - 40)
            {
                throw new InvalidOperationException("Expected feature toggle mode group to be shifted left by the 30px reserve.");
            }

            if (LegacyMainWindow.GetFeatureToggleModeGroupRightForTesting(row, "search.main") != row.Right - 10)
            {
                throw new InvalidOperationException("Expected excluded targets to keep the original mode group right edge.");
            }

            var excluded = LegacyMainWindow.CalculateFeatureToggleHotkeyButtonRectForTesting(row, "search.main");
            if (excluded.Width != 0 || excluded.Height != 0)
            {
                throw new InvalidOperationException("Expected excluded targets to skip the feature toggle hotkey icon.");
            }

            AssertStringEquals(
                LegacyMainWindow.GetFeatureToggleHotkeyOpenElementIdForTesting("automation.auto_mining"),
                "feature-toggle-hotkey-open:automation.auto_mining",
                "feature toggle hotkey open element id");

            var tooltip = LegacyMainWindow.GetFeatureToggleHotkeyIconTooltipForTesting("buff.auto_heal");
            if (tooltip.Length != 1)
            {
                throw new InvalidOperationException("Expected feature toggle hotkey icon tooltip to use one short line.");
            }

            AssertStringEquals(tooltip[0], "双击打开", "feature toggle hotkey icon tooltip");
        }

        private static void FeatureToggleHotkeyModalCopyAndCaptureMutualExclusion()
        {
            LegacyMainWindow.CloseFeatureToggleHotkeyModal();
            LegacyMainWindow.StopQuickItemHotkeyCapture();
            LegacyMainWindow.StopAutoMiningHotkeyCapture();
            LegacyMainWindow.StopMapQuickAnnouncementHotkeyCapture();

            var copy = LegacyMainWindow.GetFeatureToggleHotkeyModalCopyForTesting();
            AssertStringEquals(copy[0], "只切换功能开启/关闭，不执行功能动作。", "feature toggle modal intro");
            AssertStringEquals(copy[1], "单击开始录入按钮", "feature toggle modal idle text");
            AssertStringEquals(copy[2], "请按下按键，按esc退出", "feature toggle modal capture text");

            var tooltipCopy = LegacyMainWindow.GetFeatureToggleHotkeyModalTooltipCopyForTesting();
            AssertStringEquals(tooltipCopy[0], null, "feature toggle close button tooltip");
            AssertStringEquals(tooltipCopy[1], "支持Ctrl，Alt，Shift + ", "feature toggle capture button tooltip");
            AssertStringEquals(tooltipCopy[2], null, "feature toggle clear button tooltip");

            LegacyMainWindow.StartQuickItemHotkeyCapture(0);
            LegacyMainWindow.OpenFeatureToggleHotkeyModalForTesting("buff.auto_heal", new LegacyUiRect(400, 100, 24, 24));
            if (LegacyMainWindow.IsQuickItemHotkeyCaptureActiveForTesting())
            {
                throw new InvalidOperationException("Expected opening the feature toggle hotkey modal to stop quick item capture.");
            }

            LegacyMainWindow.StartFeatureToggleHotkeyCaptureForTesting();
            if (!LegacyMainWindow.IsFeatureToggleHotkeyCaptureActiveForTesting())
            {
                throw new InvalidOperationException("Expected feature toggle hotkey capture to start from an open modal.");
            }

            LegacyMainWindow.StartAutoMiningHotkeyCapture();
            if (LegacyMainWindow.IsFeatureToggleHotkeyCaptureActiveForTesting() ||
                !string.IsNullOrWhiteSpace(LegacyMainWindow.GetFeatureToggleHotkeyModalTargetForTesting()))
            {
                throw new InvalidOperationException("Expected auto mining capture to close the feature toggle hotkey modal.");
            }

            LegacyMainWindow.OpenFeatureToggleHotkeyModalForTesting("buff.auto_heal", new LegacyUiRect(400, 100, 24, 24));
            LegacyMainWindow.StartFeatureToggleHotkeyCaptureForTesting();
            LegacyMainWindow.StartMapQuickAnnouncementHotkeyCapture(MapQuickAnnouncementSettings.HotkeySlot1Id);
            if (LegacyMainWindow.IsFeatureToggleHotkeyCaptureActiveForTesting() ||
                !string.IsNullOrWhiteSpace(LegacyMainWindow.GetFeatureToggleHotkeyModalTargetForTesting()))
            {
                throw new InvalidOperationException("Expected map quick announcement capture to close the feature toggle hotkey modal.");
            }

            LegacyMainWindow.StopQuickItemHotkeyCapture();
            LegacyMainWindow.StopAutoMiningHotkeyCapture();
            LegacyMainWindow.StopMapQuickAnnouncementHotkeyCapture();
            LegacyMainWindow.CloseFeatureToggleHotkeyModal();
        }

        private static void FeatureToggleHotkeyModalSaveHonorsConflictsAndSelf()
        {
            var settings = BuildHotkeySettingsWithToggle("buff.auto_heal", "Alt+K");
            string message;
            bool changed;
            if (!LegacyMainWindow.TryApplyFeatureToggleHotkeyCapturedChordForTesting(settings, AppSettings.CreateDefault(), "buff.auto_heal", "alt+k", out message, out changed) ||
                changed)
            {
                throw new InvalidOperationException("Expected saving the same target chord to be a self-conflict-exempt no-op.");
            }

            if (!string.Equals(settings.ToggleHotkeysByTargetId["buff.auto_heal"], "Alt+K", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected self save to preserve the normalized feature toggle hotkey.");
            }

            if (LegacyMainWindow.TryApplyFeatureToggleHotkeyCapturedChordForTesting(settings, AppSettings.CreateDefault(), "buff.auto_mana", "Alt+K", out message, out changed) ||
                changed)
            {
                throw new InvalidOperationException("Expected cross-target feature toggle conflict to reject save.");
            }

            AssertContains(message, "已被 自动回血 使用");

            var quickItemSettings = HotkeySettings.CreateDefault();
            quickItemSettings.QuickItemHotkeyBindings.Add(new QuickItemHotkeyBinding
            {
                Hotkey = "Shift+R",
                DisplayName = "回家",
                ItemTypes = new List<int> { 4263 },
                Enabled = true
            });
            if (LegacyMainWindow.TryApplyFeatureToggleHotkeyCapturedChordForTesting(quickItemSettings, AppSettings.CreateDefault(), "buff.auto_mana", "Shift+R", out message, out changed))
            {
                throw new InvalidOperationException("Expected quick item conflict to reject feature toggle hotkey save.");
            }

            AssertContains(message, "与 快捷物品 回家冲突");
        }

        private static void FeatureToggleHotkeyEligibleAndExcludedTargets()
        {
            AssertFeatureToggleTargetEligible("inventory.quick_item_hotkeys");
            AssertFeatureToggleTargetEligible("automation.auto_mining");
            AssertFeatureToggleTargetEligible("map.quick_announcement");
            AssertFeatureToggleTargetEligible("fishing.cut_rod_skip");
            AssertFeatureToggleTargetEligible("movement.fall_protection");

            AssertFeatureToggleTargetExcluded(FeatureIds.SearchMain);
            AssertFeatureToggleTargetExcluded(FeatureIds.CombatAutoAim);
            AssertFeatureToggleTargetExcluded(FeatureIds.MapDeathHistory);
            AssertFeatureToggleTargetExcluded(FeatureIds.MapWorldDayCount);
            AssertFeatureToggleTargetExcluded(FeatureIds.MapRevealedAreaRatio);
            AssertFeatureToggleTargetExcluded(FeatureIds.FishingQuickRename);
            AssertFeatureToggleTargetExcluded(FeatureIds.FishingFilter);
        }

        private static void FeatureToggleHotkeyRuntimeDebouncesSingleAndModifierChords()
        {
            FeatureToggleHotkeyService.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            settings.InventoryAutoStackEnabled = false;
            var hotkeys = BuildHotkeySettingsWithToggle(FeatureIds.InventoryAutoStack, "K");

            var result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyApplied(result, FeatureIds.InventoryAutoStack, "On", "single chord first press");
            if (!settings.InventoryAutoStackEnabled)
            {
                throw new InvalidOperationException("Expected inventory auto stack to be enabled on first feature-toggle press.");
            }

            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyNoTrigger(result, "held single chord");
            if (!settings.InventoryAutoStackEnabled)
            {
                throw new InvalidOperationException("Expected held feature-toggle chord to debounce without toggling.");
            }

            FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(), true, string.Empty);
            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyApplied(result, FeatureIds.InventoryAutoStack, "Off", "single chord second press");
            if (settings.InventoryAutoStackEnabled)
            {
                throw new InvalidOperationException("Expected inventory auto stack to be disabled after release and repress.");
            }

            FeatureToggleHotkeyService.ResetForTesting();
            settings.InventoryAutoSellEnabled = false;
            hotkeys = BuildHotkeySettingsWithToggle(FeatureIds.InventoryAutoSell, "Alt+K");
            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyNoTrigger(result, "modifier chord without modifier");
            if (settings.InventoryAutoSellEnabled)
            {
                throw new InvalidOperationException("Expected missing modifier to keep inventory auto sell disabled.");
            }

            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkAlt, FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyApplied(result, FeatureIds.InventoryAutoSell, "On", "modifier chord first press");
            if (!settings.InventoryAutoSellEnabled)
            {
                throw new InvalidOperationException("Expected modifier chord to enable inventory auto sell.");
            }
        }

        private static void FeatureToggleHotkeyRuntimeGateRearmsAfterRelease()
        {
            FeatureToggleHotkeyService.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            settings.InventoryAutoStackEnabled = false;
            var hotkeys = BuildHotkeySettingsWithToggle(FeatureIds.InventoryAutoStack, "K");

            var result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), false, "textInputFocused");
            AssertFeatureToggleHotkeyBlocked(result, FeatureIds.InventoryAutoStack, "textInputFocused", "text input gate");
            if (settings.InventoryAutoStackEnabled)
            {
                throw new InvalidOperationException("Expected gated feature-toggle press not to mutate app settings.");
            }

            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyNoTrigger(result, "same held key after gate opens");
            if (settings.InventoryAutoStackEnabled)
            {
                throw new InvalidOperationException("Expected gate reopen to require physical key release before toggling.");
            }

            FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(), true, string.Empty);
            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyApplied(result, FeatureIds.InventoryAutoStack, "On", "rearmed after release");
            if (!settings.InventoryAutoStackEnabled)
            {
                throw new InvalidOperationException("Expected feature-toggle hotkey to work after release and repress.");
            }
        }

        private static void FeatureToggleHotkeyRuntimeTogglesMultiModeLastOnly()
        {
            FeatureToggleHotkeyService.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            settings.MovementContinuousDashEnabled = false;
            settings.MovementContinuousDashMode = MovementContinuousDashModes.HoldDirection;
            var hotkeys = BuildHotkeySettingsWithToggle(FeatureIds.MovementContinuousDash, "K");

            var result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyBlocked(result, FeatureIds.MovementContinuousDash, "noLastNonOffMode", "multi-mode missing last mode");
            if (settings.MovementContinuousDashEnabled)
            {
                throw new InvalidOperationException("Expected multi-mode feature toggle not to guess a default mode.");
            }

            FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(), true, string.Empty);
            hotkeys.LastNonOffModeByTargetId[FeatureIds.MovementContinuousDash] = MovementContinuousDashModes.DoubleTapAndHold;
            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyApplied(result, FeatureIds.MovementContinuousDash, MovementContinuousDashModes.DoubleTapAndHold, "multi-mode restore");
            if (!settings.MovementContinuousDashEnabled ||
                !string.Equals(settings.MovementContinuousDashMode, MovementContinuousDashModes.DoubleTapAndHold, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected continuous dash to restore the recorded last non-off mode.");
            }

            FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(), true, string.Empty);
            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyApplied(result, FeatureIds.MovementContinuousDash, MovementContinuousDashModes.Off, "multi-mode turn off");
            if (settings.MovementContinuousDashEnabled ||
                !string.Equals(hotkeys.LastNonOffModeByTargetId[FeatureIds.MovementContinuousDash], MovementContinuousDashModes.DoubleTapAndHold, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected continuous dash to turn off and preserve its last non-off mode.");
            }
        }

        private static void FeatureToggleHotkeyRuntimeBlocksAutoMiningHotkeyWithoutTrigger()
        {
            FeatureToggleHotkeyService.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            settings.WorldAutomationAutoMiningMode = AutoMiningModes.Off;
            var hotkeys = BuildHotkeySettingsWithToggle(FeatureIds.WorldAutomationAutoMining, "Alt+M");
            hotkeys.LastNonOffModeByTargetId[FeatureIds.WorldAutomationAutoMining] = AutoMiningModes.Hotkey;
            hotkeys.HotkeysByFeatureId.Remove(FeatureIds.WorldAutomationAutoMining);

            var result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkAlt, FeatureToggleVkM), true, string.Empty);
            AssertFeatureToggleHotkeyBlocked(result, FeatureIds.WorldAutomationAutoMining, "missingMiningTriggerHotkey", "auto mining hotkey restore without trigger");
            AssertStringEquals(settings.WorldAutomationAutoMiningMode, AutoMiningModes.Off, "auto mining mode after missing trigger restore");

            FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(), true, string.Empty);
            hotkeys.HotkeysByFeatureId[FeatureIds.WorldAutomationAutoMining] = "K";
            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkAlt, FeatureToggleVkM), true, string.Empty);
            AssertFeatureToggleHotkeyApplied(result, FeatureIds.WorldAutomationAutoMining, AutoMiningModes.Hotkey, "auto mining hotkey restore with trigger");
            AssertStringEquals(settings.WorldAutomationAutoMiningMode, AutoMiningModes.Hotkey, "auto mining mode after valid trigger restore");

            FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(), true, string.Empty);
            hotkeys.HotkeysByFeatureId.Remove(FeatureIds.WorldAutomationAutoMining);
            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkAlt, FeatureToggleVkM), true, string.Empty);
            AssertFeatureToggleHotkeyBlocked(result, FeatureIds.WorldAutomationAutoMining, "missingMiningTriggerHotkey", "auto mining hotkey disable without trigger");
            AssertStringEquals(settings.WorldAutomationAutoMiningMode, AutoMiningModes.Hotkey, "auto mining mode after missing trigger disable");
        }

        private static void FeatureToggleHotkeyRuntimeDoesNotMutateActionHotkeyPayloads()
        {
            FeatureToggleHotkeyService.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            settings.InventoryQuickItemHotkeysEnabled = false;
            settings.MapQuickAnnouncementEnabled = false;
            settings.MapQuickAnnouncementHotkeySlot1 = "Alt";
            settings.MapQuickAnnouncementHotkeySlot2 = "Shift";
            settings.MapQuickAnnouncementTriggerKey = "R";

            var hotkeys = BuildHotkeySettingsWithToggle(FeatureIds.InventoryQuickItemHotkeys, "K");
            hotkeys.ToggleHotkeysByTargetId[FeatureIds.MapQuickAnnouncement] = "Alt+M";
            hotkeys.QuickItemHotkeyBindings.Add(new QuickItemHotkeyBinding
            {
                Hotkey = "F9",
                DisplayName = "回家",
                ItemTypes = new List<int> { 4263 },
                Enabled = true
            });
            var queue = new InputActionQueue();

            var result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkK), true, string.Empty);
            AssertFeatureToggleHotkeyApplied(result, FeatureIds.InventoryQuickItemHotkeys, "On", "quick item master toggle");
            if (!settings.InventoryQuickItemHotkeysEnabled ||
                hotkeys.QuickItemHotkeyBindings.Count != 1 ||
                !string.Equals(hotkeys.QuickItemHotkeyBindings[0].Hotkey, "F9", StringComparison.Ordinal) ||
                queue.GetFastState().PendingCount != 0)
            {
                throw new InvalidOperationException("Expected quick item feature toggle to leave action bindings and queue untouched.");
            }

            FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(), true, string.Empty);
            result = FeatureToggleHotkeyService.TickForTesting(settings, hotkeys, FeatureToggleDownKeys(FeatureToggleVkAlt, FeatureToggleVkM), true, string.Empty);
            AssertFeatureToggleHotkeyApplied(result, FeatureIds.MapQuickAnnouncement, "On", "map quick announcement master toggle");
            if (!settings.MapQuickAnnouncementEnabled ||
                !string.Equals(settings.MapQuickAnnouncementHotkeySlot1, "Alt", StringComparison.Ordinal) ||
                !string.Equals(settings.MapQuickAnnouncementHotkeySlot2, "Shift", StringComparison.Ordinal) ||
                !string.Equals(settings.MapQuickAnnouncementTriggerKey, "R", StringComparison.Ordinal) ||
                queue.GetFastState().PendingCount != 0)
            {
                throw new InvalidOperationException("Expected quick announcement feature toggle to leave trigger slots and queue untouched.");
            }
        }

        private static HotkeySettings BuildHotkeySettingsWithToggle(string targetId, string chord)
        {
            var settings = HotkeySettings.CreateDefault();
            settings.ToggleHotkeysByTargetId[targetId] = chord;
            return settings;
        }

        private static Dictionary<int, bool> FeatureToggleDownKeys(params int[] keys)
        {
            var result = new Dictionary<int, bool>();
            for (var index = 0; keys != null && index < keys.Length; index++)
            {
                if (keys[index] > 0)
                {
                    result[keys[index]] = true;
                }
            }

            return result;
        }

        private static void AssertFeatureToggleHotkeyApplied(
            FeatureToggleHotkeyDispatchResult result,
            string targetId,
            string expectedMode,
            string context)
        {
            if (result == null ||
                !result.Triggered ||
                !result.Applied ||
                !string.Equals(result.TargetId, targetId, StringComparison.Ordinal) ||
                !string.Equals(result.NewMode, expectedMode, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected applied feature-toggle result for " +
                    context +
                    ", got " +
                    FormatFeatureToggleHotkeyResult(result) +
                    ".");
            }
        }

        private static void AssertFeatureToggleHotkeyBlocked(
            FeatureToggleHotkeyDispatchResult result,
            string targetId,
            string expectedReason,
            string context)
        {
            if (result == null ||
                !result.Triggered ||
                result.Applied ||
                !string.Equals(result.TargetId, targetId, StringComparison.Ordinal) ||
                !string.Equals(result.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected blocked feature-toggle result for " +
                    context +
                    ", got " +
                    FormatFeatureToggleHotkeyResult(result) +
                    ".");
            }
        }

        private static void AssertFeatureToggleHotkeyNoTrigger(FeatureToggleHotkeyDispatchResult result, string context)
        {
            if (result != null && result.Triggered)
            {
                throw new InvalidOperationException(
                    "Expected no feature-toggle trigger for " +
                    context +
                    ", got " +
                    FormatFeatureToggleHotkeyResult(result) +
                    ".");
            }
        }

        private static string FormatFeatureToggleHotkeyResult(FeatureToggleHotkeyDispatchResult result)
        {
            if (result == null)
            {
                return "<null>";
            }

            return "triggered=" +
                   result.Triggered +
                   ", applied=" +
                   result.Applied +
                   ", target=" +
                   (result.TargetId ?? string.Empty) +
                   ", mode=" +
                   (result.NewMode ?? string.Empty) +
                   ", reason=" +
                   (result.Reason ?? string.Empty);
        }

        private static void AssertNormalizedFeatureToggleChord(string value, string expected)
        {
            string normalized;
            if (!FeatureToggleHotkeyChord.TryNormalize(value, out normalized) ||
                !string.Equals(normalized, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + value + " to normalize to " + expected + ", got " + (normalized ?? "<null>") + ".");
            }
        }

        private static void AssertInvalidFeatureToggleChord(string value)
        {
            string normalized;
            if (FeatureToggleHotkeyChord.TryNormalize(value, out normalized))
            {
                throw new InvalidOperationException("Expected " + value + " to be rejected for feature toggle hotkeys.");
            }
        }

        private static void AssertFeatureToggleConflict(
            HotkeySettings hotkeySettings,
            AppSettings appSettings,
            string targetId,
            string chord,
            FeatureToggleHotkeyConflictType expectedType,
            string expectedOwner)
        {
            FeatureToggleHotkeyConflict conflict;
            if (!FeatureToggleHotkeyConflictRegistry.TryFindConflict(hotkeySettings, appSettings, targetId, chord, out conflict) ||
                conflict == null)
            {
                throw new InvalidOperationException("Expected feature toggle hotkey conflict for " + chord + ".");
            }

            if (conflict.ConflictType != expectedType)
            {
                throw new InvalidOperationException("Expected conflict type " + expectedType + ", got " + conflict.ConflictType + ".");
            }

            AssertStringEquals(conflict.OwnerDisplayName, expectedOwner, "feature toggle conflict owner");
        }

        private static void AssertFeatureToggleTargetEligible(string targetId)
        {
            FeatureToggleHotkeyTarget target;
            if (!FeatureToggleHotkeyTargetCatalog.TryGet(targetId, out target))
            {
                throw new InvalidOperationException("Expected " + targetId + " to be eligible for feature toggle hotkeys.");
            }
        }

        private static void AssertFeatureToggleTargetExcluded(string targetId)
        {
            FeatureToggleHotkeyTarget target;
            if (FeatureToggleHotkeyTargetCatalog.TryGet(targetId, out target))
            {
                throw new InvalidOperationException("Expected " + targetId + " to be excluded from feature toggle hotkeys.");
            }
        }
    }
}
