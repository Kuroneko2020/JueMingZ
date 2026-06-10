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
        private static void HandleMiscTravelMenu(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var result = TravelMenuService.SetEnabledFromUi(enabled, enabled);
            var status = result.Succeeded
                ? "Succeeded"
                : string.Equals(result.ResultCode, "multiplayerBlocked", StringComparison.OrdinalIgnoreCase)
                    ? "BlockedByEnvironment"
                    : string.Equals(result.ResultCode, TravelMenuService.SuspendedResultCode, StringComparison.OrdinalIgnoreCase)
                        ? "BlockedByConfig"
                        : "Failed";

            Record(
                command,
                "Ui.Toggle.MiscTravelMenu",
                "WorldAutomation",
                status,
                result.Message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(!TravelMenuService.IsSuspended) + ",\"featureId\":\"misc.travel_menu\",\"enabled\":" + BoolRaw(result.Enabled) + ",\"openedMenu\":" + BoolRaw(result.OpenedMenu) + ",\"restored\":" + BoolRaw(result.Restored) + ",\"resultCode\":\"" + EscapeJson(result.ResultCode) + "\",\"detail\":\"" + EscapeJson(result.Detail) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");

            if (result.Succeeded && result.Enabled && result.OpenedMenu)
            {
                LegacyMainUiState.SetVisible(false);
            }
        }

        private static void HandleMiscAutoMiningMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var mode = AutoMiningModes.Normalize(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var oldMode = AutoMiningModes.Normalize(settings.WorldAutomationAutoMiningMode);
            var changed = !string.Equals(oldMode, mode, StringComparison.Ordinal);
            settings.WorldAutomationAutoMiningMode = mode;
            if (string.Equals(mode, AutoMiningModes.Off, StringComparison.Ordinal))
            {
                AutoMiningService.ClearSelection("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? "Auto mining mode changed to " + mode + "."
                : "Auto mining mode already " + mode + ".";

            Record(
                command,
                "Ui.Toggle.MiscAutoMining",
                "WorldAutomation",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.WorldAutomationAutoMining) + "\",\"mode\":\"" + EscapeJson(mode) + "\",\"oldMode\":\"" + EscapeJson(oldMode) + "\",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoMiningCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            if (string.Equals(payload, "hotkey", StringComparison.OrdinalIgnoreCase))
            {
                if (!command.IsDoubleClick)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoMining",
                        "UI",
                        "NotApplicable",
                        "Double-click the auto mining hotkey field to capture a key.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"hotkey\",\"doubleClick\":" + BoolRaw(command.IsDoubleClick) + ",\"captureActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                LegacyMainWindow.StartAutoMiningHotkeyCapture();
                Record(
                    command,
                    "Ui.Configure.MiscAutoMining",
                    "UI",
                    "Succeeded",
                    "Auto mining hotkey capture is now active.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"hotkey\",\"doubleClick\":true,\"captureActive\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            Record(
                command,
                "Ui.Configure.MiscAutoMining",
                "UI",
                "Rejected",
                "Unknown auto mining command.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"action\":\"unknown\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoCaptureCritterMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var oldMode = AutoCaptureCritterModes.Normalize(settings.WorldAutomationAutoCaptureCritterMode, settings.MiscAutoCaptureCritterEnabled);
            var mode = AutoCaptureCritterModes.Normalize(payload, settings.MiscAutoCaptureCritterEnabled);
            var changed = !string.Equals(oldMode, mode, StringComparison.Ordinal);
            settings.WorldAutomationAutoCaptureCritterMode = mode;
            if (string.Equals(mode, AutoCaptureCritterModes.Off, StringComparison.Ordinal))
            {
                AutoCaptureCritterService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? "Auto capture mode changed to " + mode + "."
                : "Auto capture mode already " + mode + ".";

            Record(
                command,
                "Ui.Toggle.MiscAutoCaptureCritter",
                "WorldAutomation",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.WorldAutomationAutoCaptureCritter) + "\",\"mode\":\"" + EscapeJson(mode) + "\",\"oldMode\":\"" + EscapeJson(oldMode) + "\",\"enabled\":" + BoolRaw(AutoCaptureCritterModes.IsEnabled(mode)) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleMiscAutoCaptureCritterConfig(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            LegacyMainWindow.ToggleAutoCaptureCritterConfigPopup();
            Record(
                command,
                "Ui.Toggle.MiscAutoCaptureCritterConfig",
                "UI",
                "Succeeded",
                "自动捕捉配置已切换。",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleMiscAutoCaptureCritterOption(LegacyUiCommand command, string optionId)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildUiOptionStateJson();
            var option = AutoCaptureCritterCategoryCatalog.Find(optionId);
            if (option == null)
            {
                Record(
                    command,
                    "Ui.Toggle.MiscAutoCaptureCritterOption",
                    "UI",
                    "NotApplicable",
                    "Unknown auto capture critter category: " + optionId + ".",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"optionId\":\"" + EscapeJson(optionId) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var enabled = !AutoCaptureCritterCategoryCatalog.GetEnabled(settings, option.Id);
            AutoCaptureCritterCategoryCatalog.SetEnabled(settings, option.Id, enabled);
            settings.MiscAutoCaptureCritterCategoryDefaultsMigrated = true;
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.MiscAutoCaptureCritterOption",
                "WorldAutomation",
                "Succeeded",
                "自动捕捉分类：" + option.Label + (enabled ? "已开启。" : "已关闭。"),
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.WorldAutomationAutoCaptureCritter) + "\",\"optionId\":\"" + EscapeJson(option.Id) + "\",\"label\":\"" + EscapeJson(option.Label) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoHarvestMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.WorldAutomationAutoHarvestEnabled != enabled;
            settings.WorldAutomationAutoHarvestEnabled = enabled;
            if (!enabled)
            {
                AutoHarvestService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Auto harvest enabled." : "Auto harvest disabled.")
                : (enabled ? "Auto harvest already enabled." : "Auto harvest already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoHarvest",
                "WorldAutomation",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.WorldAutomationAutoHarvest) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }
    }
}
