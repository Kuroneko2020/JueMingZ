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
        private static void SetMovementFeatureEnabled(LegacyUiCommand command, string payload)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildMovementUiStateJson();
            var separator = string.IsNullOrWhiteSpace(payload) ? -1 : payload.LastIndexOf(':');
            if (separator <= 0 || separator >= payload.Length - 1)
            {
                Record(
                    command,
                    "Ui.Toggle.MovementFeature",
                    "UI",
                    "NotApplicable",
                    "Movement toggle ignored because the feature id or mode was invalid.",
                    before,
                    BuildMovementUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var featureId = payload.Substring(0, separator);
            var enabled = IsOnMode(payload.Substring(separator + 1));
            bool changed;
            string label;
            string scenario;
            bool implemented;
            if (string.Equals(featureId, "movement.simulated_multi_jump", StringComparison.Ordinal))
            {
                changed = settings.MovementSimulatedMultiJumpEnabled != enabled;
                settings.MovementSimulatedMultiJumpEnabled = enabled;
                if (!enabled)
                {
                    MovementSimulatedJumpPulseCompat.CancelSimulatedJumpPulse(Guid.Empty, "movement.simulated_multi_jump disabled from UI");
                }
                label = "模拟连跳";
                scenario = "Ui.Toggle.MovementSimulatedMultiJump";
                implemented = true;
            }
            else if (string.Equals(featureId, "movement.continuous_dash", StringComparison.Ordinal))
            {
                changed = settings.MovementContinuousDashEnabled != enabled;
                settings.MovementContinuousDashEnabled = enabled;
                label = "连续冲刺";
                scenario = "Ui.Toggle.MovementContinuousDash";
                implemented = true;
            }
            else if (string.Equals(featureId, "movement.teleport_correction", StringComparison.Ordinal))
            {
                changed = settings.MovementTeleportCorrectionEnabled != enabled;
                settings.MovementTeleportCorrectionEnabled = enabled;
                label = "传送修正";
                scenario = "Ui.Toggle.MovementTeleportCorrection";
                implemented = true;
            }
            else if (string.Equals(featureId, "movement.fall_protection", StringComparison.Ordinal))
            {
                changed = settings.MovementSafeLandingEnabled != enabled;
                settings.MovementSafeLandingEnabled = enabled;
                if (!enabled)
                {
                    MovementSafeLandingCompat.CancelSafeLandingJumpPulse(Guid.Empty, "movement.fall_protection disabled from UI");
                }
                label = "智能防摔";
                scenario = "Ui.Toggle.MovementSafeLanding";
                implemented = true;
            }
            else
            {
                Record(
                    command,
                    "Ui.Toggle.MovementFeature",
                    "UI",
                    "NotApplicable",
                    "Unknown movement feature id: " + featureId + ".",
                    before,
                    BuildMovementUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"featureId\":\"" + EscapeJson(featureId) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            ConfigService.SaveAll();
            var uiOnly = !implemented;
            Record(
                command,
                scenario,
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                enabled
                    ? implemented
                        ? label + "已开启。"
                        : label + "已开启（占位）：当前只保存 UI 状态，不提交移动动作。"
                    : label + "已关闭。",
                before,
                BuildMovementUiStateJson(),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(implemented) + ",\"uiOnly\":" + BoolRaw(uiOnly) + ",\"featureId\":\"" + EscapeJson(featureId) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetMovementSafeLandingMode(LegacyUiCommand command, string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildMovementUiStateJson();
            if (string.Equals(mode, "Config", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.ToggleMovementSafeLandingConfigPopup();
                Record(
                    command,
                    "Ui.Toggle.MovementSafeLandingConfig",
                    "UI",
                    "Succeeded",
                    "智能防摔配置已切换。",
                    before,
                    BuildMovementUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var enabled = IsOnMode(mode);
            var changed = settings.MovementSafeLandingEnabled != enabled;
            settings.MovementSafeLandingEnabled = enabled;
            if (!enabled)
            {
                MovementSafeLandingCompat.CancelSafeLandingJumpPulse(Guid.Empty, "movement.fall_protection disabled from UI");
            }

            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.MovementSafeLanding",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                enabled ? "智能防摔已开启。" : "智能防摔已关闭。",
                before,
                BuildMovementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":false,\"featureId\":\"movement.fall_protection\",\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleMovementSafeLandingOption(LegacyUiCommand command, string optionId)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildMovementUiStateJson();
            var option = MovementSafeLandingOptionCatalog.Find(optionId);
            if (option == null)
            {
                Record(
                    command,
                    "Ui.Toggle.MovementSafeLandingOption",
                    "UI",
                    "NotApplicable",
                    "Unknown smart fall protection option: " + optionId + ".",
                    before,
                    BuildMovementUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"optionId\":\"" + EscapeJson(optionId) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var enabled = !MovementSafeLandingOptionCatalog.GetEnabled(settings, optionId);
            MovementSafeLandingOptionCatalog.SetEnabled(settings, optionId, enabled);
            settings.MovementSafeLandingOptionsDefaultMigrated = true;
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.MovementSafeLandingOption",
                "UI",
                "Succeeded",
                "智能防摔策略分类：" + option.Label + (enabled ? "已开启。" : "已关闭。"),
                before,
                BuildMovementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":false,\"optionId\":\"" + EscapeJson(optionId) + "\",\"label\":\"" + EscapeJson(option.Label) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetMovementContinuousDashMode(LegacyUiCommand command, string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildMovementUiStateJson();
            var off = string.Equals(mode, MovementContinuousDashModes.Off, StringComparison.OrdinalIgnoreCase);
            var normalized = off
                ? MovementContinuousDashModes.Normalize(settings.MovementContinuousDashMode)
                : MovementContinuousDashModes.Normalize(mode);
            var oldMode = MovementContinuousDashModes.Normalize(settings.MovementContinuousDashMode);
            var oldEnabled = settings.MovementContinuousDashEnabled;
            var newEnabled = !off;
            var changed = oldEnabled != newEnabled || (!off && !string.Equals(oldMode, normalized, StringComparison.Ordinal));
            settings.MovementContinuousDashEnabled = newEnabled;
            if (!off)
            {
                settings.MovementContinuousDashMode = normalized;
            }

            ConfigService.SaveAll();

            var modeLabel = off ? "关闭" : MovementContinuousDashModes.DisplayName(normalized);
            Record(
                command,
                "Ui.Toggle.MovementContinuousDashMode",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                off ? "连续冲刺已关闭。" : "连续冲刺模式：" + modeLabel + "。",
                before,
                BuildMovementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":false,\"enabled\":" + BoolRaw(newEnabled) + ",\"mode\":\"" + EscapeJson(normalized) + "\",\"modeLabel\":\"" + EscapeJson(modeLabel) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }
    }
}
