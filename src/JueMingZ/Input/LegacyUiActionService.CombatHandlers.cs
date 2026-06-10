using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void SetCombatAimRadius(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildCombatUiStateJson();
            var value = LegacyMainUiState.Clamp(command.IntValue, 0, 50);
            if (string.Equals(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase))
            {
                Record(
                    command,
                    "Ui.Slider.CombatAimRadius",
                    "UI",
                    "Ignored",
                    "Combat aim radius slider ignored because player-center mode uses screen range.",
                    before,
                    BuildCombatUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"radius\":" + value.ToString(CultureInfo.InvariantCulture) + ",\"aimRangeOrigin\":\"" + EscapeJson(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin)) + "\",\"activeRangeMode\":\"PlayerScreen\",\"sliderDisabled\":true,\"sliderIgnoredReason\":\"playerCenterFixedRange\",\"mouseCaptured\":true}",
                    "UI");
                LegacyUiInput.ClearPendingSlider(command.ElementId);
                return;
            }

            settings.CursorAimRadius = value;
            settings.CombatAimAssistRadius = value;
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Slider.CombatAimRadius",
                "UI",
                "Succeeded",
                "Combat aim assist radius set to " + value.ToString(CultureInfo.InvariantCulture) + ".",
                before,
                BuildCombatUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"radius\":" + value.ToString(CultureInfo.InvariantCulture) + ",\"aimRangeOrigin\":\"" + EscapeJson(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin)) + "\",\"mouseCaptured\":true}",
                "UI");
            LegacyUiInput.ClearPendingSlider(command.ElementId);
        }

        private static void ToggleCombatAimPriority(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildCombatUiStateJson();
            settings.AimTargetPriority = CombatAimModes.ToggleTargetPriority(settings.AimTargetPriority);
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.CombatAimTargetPriority",
                "UI",
                "Succeeded",
                "Combat aim target priority set to " + CombatAimModes.TargetPriorityLabel(settings.AimTargetPriority) + ".",
                before,
                BuildCombatUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"aimTargetPriority\":\"" + EscapeJson(CombatAimModes.NormalizeTargetPriority(settings.AimTargetPriority)) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleCombatAimOrigin(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildCombatUiStateJson();
            settings.AimRangeOrigin = CombatAimModes.ToggleRangeOrigin(settings.AimRangeOrigin);
            settings.CombatAimAssistRadius = LegacyMainUiState.Clamp(settings.CursorAimRadius, 0, 50);
            if (string.Equals(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase))
            {
                LegacyUiInput.CancelActiveSlider("combat-aim-radius");
            }

            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.CombatAimRangeOrigin",
                "UI",
                "Succeeded",
                "Combat aim range origin set to " + CombatAimModes.RangeOriginLabel(settings.AimRangeOrigin) + ".",
                before,
                BuildCombatUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"aimRangeOrigin\":\"" + EscapeJson(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin)) + "\",\"activeRangeMode\":\"" + (string.Equals(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase) ? "PlayerScreen" : "CursorSlider") + "\",\"activeRadius\":" + IntRaw(settings.CombatAimAssistRadius) + ",\"sliderDisabled\":" + BoolRaw(string.Equals(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleCombatAimOption(LegacyUiCommand command, bool trackDummy)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildCombatUiStateJson();
            bool enabled;
            string scenario;
            string label;
            if (trackDummy)
            {
                settings.CombatAimTrackDummyEnabled = !settings.CombatAimTrackDummyEnabled;
                enabled = settings.CombatAimTrackDummyEnabled;
                scenario = "Ui.Toggle.CombatAimTrackDummy";
                label = "追踪人偶";
            }
            else
            {
                settings.CombatAimMarkerEnabled = !settings.CombatAimMarkerEnabled;
                enabled = settings.CombatAimMarkerEnabled;
                scenario = "Ui.Toggle.CombatAimMarker";
                label = "瞄准标记";
            }

            ConfigService.SaveAll();
            Record(
                command,
                scenario,
                "UI",
                "Succeeded",
                "Combat aim option " + label + " " + (enabled ? "enabled." : "disabled."),
                before,
                BuildCombatUiStateJson(),
                "{\"submitted\":false,\"implemented\":false,\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetCombatFeatureEnabled(LegacyUiCommand command, string option, bool enabled)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildCombatUiStateJson();
            bool changed;
            string scenario;
            bool implemented;
            string message;
            if (string.Equals(option, "autoClicker", StringComparison.Ordinal))
            {
                changed = settings.CombatAutoClickerEnabled != enabled;
                settings.CombatAutoClickerEnabled = enabled;
                scenario = "Ui.Toggle.CombatAutoClicker";
                implemented = true;
                message = enabled
                    ? "自动连点已开启：ItemCheck 策略会按原版自动复用状态补足合格物品，并排除鱼竿和左轮。"
                    : "自动连点已关闭；ItemCheck 核心不会接管物品使用。";
            }
            else if (string.Equals(option, "flailCombo", StringComparison.Ordinal))
            {
                changed = settings.CombatFlailComboEnabled != enabled;
                settings.CombatFlailComboEnabled = enabled;
                scenario = "Ui.Toggle.CombatFlailCombo";
                implemented = true;
                message = enabled
                    ? "链球连击已开启：手持链球长按右键时，会在原版右键无意义的安全场景中用 ItemCheck 节奏触发连击。"
                    : "链球连击已关闭。";
            }
            else if (string.Equals(option, "phasebladeQuickSwitch", StringComparison.Ordinal))
            {
                changed = settings.CombatPhasebladeQuickSwitchEnabled != enabled;
                settings.CombatPhasebladeQuickSwitchEnabled = enabled;
                scenario = "Ui.Toggle.CombatPhasebladeQuickSwitch";
                implemented = true;
                message = enabled
                    ? "光剑快切已开启：本阶段只保存配置，实际右键快切运行逻辑由后续治理阶段接入。"
                    : "光剑快切已关闭。";
            }
            else if (string.Equals(option, "perfectRevolver", StringComparison.Ordinal))
            {
                changed = settings.CombatPerfectRevolverEnabled != enabled;
                settings.CombatPerfectRevolverEnabled = enabled;
                scenario = "Ui.Toggle.CombatPerfectRevolver";
                implemented = true;
                message = enabled
                    ? "完美左轮已开启：手持左轮长按使用键时，会在 ItemCheck 前接管攻击输入并按原版暴击窗口控节奏，兼容辅助瞄准。"
                    : "完美左轮已关闭。";
            }
            else if (string.Equals(option, "magicStringClicker", StringComparison.Ordinal))
            {
                changed = settings.CombatMagicStringClickerEnabled != enabled;
                settings.CombatMagicStringClickerEnabled = enabled;
                scenario = "Ui.Toggle.CombatMagicStringClicker";
                implemented = true;
                message = enabled
                    ? "省力魔法绳已开启：装备魔法绳效果并手持悠悠球长按时，会以 2 tick 节奏脉冲使用输入，并兼容辅助瞄准。"
                    : "省力魔法绳已关闭。";
            }
            else if (string.Equals(option, "autoFacing", StringComparison.Ordinal))
            {
                changed = settings.CombatAutoFacingEnabled != enabled;
                settings.CombatAutoFacingEnabled = enabled;
                scenario = "Ui.Toggle.CombatAutoFacing";
                implemented = true;
                message = enabled
                    ? "自动转向已开启：使用合格战斗物品时，会通过 Player.ChangeDir 朝向当前目标或鼠标方向。"
                    : "自动转向已关闭。";
            }
            else if (string.Equals(option, "equipmentWarning", StringComparison.Ordinal))
            {
                changed = settings.CombatEquipmentWarningEnabled != enabled;
                settings.CombatEquipmentWarningEnabled = enabled;
                scenario = "Ui.Toggle.CombatEquipmentWarning";
                implemented = true;
                message = enabled
                    ? "装备提示已开启：Boss 或非血月事件期间，穿着非战斗装备时会在玩家头顶提示。"
                    : "装备提示已关闭。";
            }
            else if (string.Equals(option, "goblinExecution", StringComparison.Ordinal))
            {
                changed = settings.CombatGoblinExecutionEnabled != enabled;
                settings.CombatGoblinExecutionEnabled = enabled;
                scenario = "Ui.Toggle.CombatGoblinExecution";
                implemented = true;
                message = enabled
                    ? "哥布林必死已开启：玩家武器命中哥布林工匠时会放行原版伤害路径，其它城镇 NPC 不受影响。"
                    : "哥布林必死已关闭。";
            }
            else
            {
                Record(
                    command,
                    "Ui.Toggle.CombatUnknown",
                    "UI",
                    "NotApplicable",
                    "Combat toggle ignored because the option was unknown.",
                    before,
                    BuildCombatUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            ConfigService.SaveAll();
            Record(
                command,
                scenario,
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildCombatUiStateJson(),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(implemented) + ",\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetCombatPhasebladeQuickSwitchInterval(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildCombatUiStateJson();
            var value = CombatPhasebladeQuickSwitchSettings.NormalizeIntervalTicks(command.IntValue);
            var changed = settings.CombatPhasebladeQuickSwitchIntervalTicks != value;
            settings.CombatPhasebladeQuickSwitchIntervalTicks = value;
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Slider.CombatPhasebladeQuickSwitchInterval",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                "光剑快切间隔设置为 " + value.ToString(CultureInfo.InvariantCulture) + " tick。",
                before,
                BuildCombatUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"intervalTicks\":" + IntRaw(value) + ",\"minIntervalTicks\":" + IntRaw(CombatPhasebladeQuickSwitchSettings.MinIntervalTicks) + ",\"maxIntervalTicks\":" + IntRaw(CombatPhasebladeQuickSwitchSettings.MaxIntervalTicks) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "UI");
            LegacyUiInput.ClearPendingSlider(command.ElementId);
        }
    }
}
