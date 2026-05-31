using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void ToggleAutoHeal(LegacyUiCommand command)
        {
            var before = BuildAutoRecoveryBeforeJson();
            var enabled = AutoRecoveryService.ToggleAutoHeal();
            Record(
                command,
                "Ui.Toggle.AutoHeal",
                "AutoRecovery",
                "Succeeded",
                "AutoHeal " + (enabled ? "enabled." : "disabled."),
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleAboutCopyFeedbackGroup(LegacyUiCommand command)
        {
            string detail;
            var copied = ClipboardCompat.TrySetText(LegacyMainWindow.AboutFeedbackGroupNumber, out detail);
            Record(
                command,
                "Ui.About.CopyFeedbackGroup",
                "UI",
                copied ? "Succeeded" : "Failed",
                copied ? "Feedback QQ group copied to clipboard." : "Feedback QQ group copy failed: " + detail,
                "{}",
                "{}",
                "{\"submitted\":false,\"copied\":" + BoolRaw(copied) + ",\"group\":\"" + EscapeJson(LegacyMainWindow.AboutFeedbackGroupNumber) + "\",\"detail\":\"" + EscapeJson(detail) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscDeveloperEasterEgg(LegacyUiCommand command, string payload)
        {
            var mode = string.IsNullOrWhiteSpace(payload) ? "open" : payload.Trim();
            var before = BuildUiOptionStateJson();

            if (string.Equals(mode, "debugOn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "debugOff", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "blocked", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.SetDeveloperEasterEggConfirmPending(false);
                Record(
                    command,
                    "Ui.Toggle.MiscDeveloperEasterEgg",
                    "Diagnostics",
                    "NotApplicable",
                    "Developer menu startup switch has been removed; use the open button instead.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"step\":\"removedSwitch\",\"payload\":\"" + EscapeJson(mode) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var pending = LegacyMainWindow.IsDeveloperEasterEggConfirmPending();
            if (!pending && string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.SetDeveloperEasterEggConfirmPending(true);
                Record(
                    command,
                    "Ui.Toggle.MiscDeveloperEasterEgg",
                    "UI",
                    "Succeeded",
                    "Developer menu armed. Click again to run vanilla /hh.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"step\":\"armed\",\"pendingConfirmation\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (pending && (string.Equals(mode, "confirm", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase)))
            {
                var opened = DebugCommandsCompat.TryOpenDebugCommandsHelp(out var detail);
                LegacyMainWindow.SetDeveloperEasterEggConfirmPending(false);
                Record(
                    command,
                    "Ui.Toggle.MiscDeveloperEasterEgg",
                    "Diagnostics",
                    opened ? "Succeeded" : "Failed",
                    opened ? "Vanilla /hh debug command list requested." : "Vanilla /hh open failed: " + detail,
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"step\":\"confirm\",\"opened\":" + BoolRaw(opened) + ",\"pendingConfirmation\":false,\"detail\":\"" + EscapeJson(detail) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            Record(
                command,
                "Ui.Toggle.MiscDeveloperEasterEgg",
                "UI",
                "Rejected",
                "Unknown developer menu payload.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"step\":\"unknown\",\"payload\":\"" + EscapeJson(mode) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscDebugCommandsMode(LegacyUiCommand command, string payload)
        {
            var mode = string.IsNullOrWhiteSpace(payload) ? string.Empty : payload.Trim();
            var before = BuildUiOptionStateJson();
            LegacyMainWindow.SetDeveloperEasterEggConfirmPending(false);
            Record(
                command,
                "Ui.Toggle.MiscDebugCommandsSwitch",
                "Diagnostics",
                "NotApplicable",
                "Developer menu startup switch has been removed; no configuration was changed.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"enabled\":true,\"changed\":false,\"resultCode\":\"removedSwitch\",\"payload\":\"" + EscapeJson(mode) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

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

        private static void HandleMiscWorldGenerationDetails(LegacyUiCommand command, string payload)
        {
            var mode = string.IsNullOrWhiteSpace(payload) ? string.Empty : payload.Trim();
            var before = BuildUiOptionStateJson();
            if (string.Equals(mode, "hint", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "status", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "locked", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.SetWorldGenerationDetailsHintAlternate(true);
            }

            Record(
                command,
                "Ui.Toggle.MiscWorldGenerationDetails",
                "Diagnostics",
                "NotApplicable",
                "WorldGen Debug Viewer is always enabled; this row is informational only.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.DiagnosticsWorldGenDebugViewer) + "\",\"enabled\":true,\"changed\":false,\"uiOnly\":true,\"payload\":\"" + EscapeJson(mode) + "\",\"sessionConfiguredEnabled\":" + BoolRaw(WorldGenDebugCompat.WorldGenSessionConfiguredEnabled) + ",\"sharedDebugFieldEnabled\":" + BoolRaw(WorldGenDebugCompat.Enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscQuickItemHotkeysMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryQuickItemHotkeysEnabled != enabled;
            settings.InventoryQuickItemHotkeysEnabled = enabled;
            ConfigService.SaveAll();
            var bindingCount = ConfigService.HotkeySettings == null || ConfigService.HotkeySettings.QuickItemHotkeyBindings == null
                ? 0
                : ConfigService.HotkeySettings.QuickItemHotkeyBindings.Count;
            var message = changed
                ? (enabled
                    ? "Quick item hotkeys enabled."
                    : "Quick item hotkeys disabled.")
                : (enabled
                    ? "Quick item hotkeys already enabled."
                    : "Quick item hotkeys already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscQuickItemHotkeys",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"inventory.quick_item_hotkeys\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"bindingCount\":" + IntRaw(bindingCount) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoStackMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryAutoStackEnabled != enabled;
            settings.InventoryAutoStackEnabled = enabled;
            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Auto stack enabled." : "Auto stack disabled.")
                : (enabled ? "Auto stack already enabled." : "Auto stack already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoStack",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryAutoStack) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscQuickBagOpenMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryQuickBagOpenEnabled != enabled;
            settings.InventoryQuickBagOpenEnabled = enabled;
            if (!enabled)
            {
                QuickBagOpenService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Quick bag open enabled." : "Quick bag open disabled.")
                : (enabled ? "Quick bag open already enabled." : "Quick bag open already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscQuickBagOpen",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryQuickBagOpen) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoDepositCoinsMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryAutoDepositCoinsEnabled != enabled;
            settings.InventoryAutoDepositCoinsEnabled = enabled;
            if (!enabled)
            {
                AutoDepositCoinsService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Auto deposit coins enabled." : "Auto deposit coins disabled.")
                : (enabled ? "Auto deposit coins already enabled." : "Auto deposit coins already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoDepositCoins",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryAutoDepositCoins) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoExtractinatorMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryAutoExtractinatorEnabled != enabled;
            settings.InventoryAutoExtractinatorEnabled = enabled;
            if (!enabled)
            {
                AutoExtractinatorService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Auto extractinator enabled." : "Auto extractinator disabled.")
                : (enabled ? "Auto extractinator already enabled." : "Auto extractinator already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoExtractinator",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryAutoExtractinator) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscKeepFavoritedMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryKeepFavoritedEnabled != enabled;
            settings.InventoryKeepFavoritedEnabled = enabled;
            if (!enabled)
            {
                KeepFavoritedService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Keep favorited enabled." : "Keep favorited disabled.")
                : (enabled ? "Keep favorited already enabled." : "Keep favorited already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscKeepFavorited",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryKeepFavorited) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
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

        private static void HandleMiscAutoSellMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryAutoSellEnabled != enabled;
            settings.InventoryAutoSellEnabled = enabled;
            ConfigService.SaveAll();
            var itemCount = CountValidAutoSellItems(settings.InventoryAutoSellItemIds);
            var message = changed
                ? (enabled ? "Auto sell enabled." : "Auto sell disabled.")
                : (enabled ? "Auto sell already enabled." : "Auto sell already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoSell",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryAutoSell) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"autoSellItemCount\":" + IntRaw(itemCount) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoSellRow(LegacyUiCommand command, string payload)
        {
            if (string.Equals(payload, "On", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload, "Off", StringComparison.OrdinalIgnoreCase))
            {
                HandleMiscAutoSellMode(command, payload);
                return;
            }

            if (!string.Equals(payload, "add-empty", StringComparison.OrdinalIgnoreCase))
            {
                var beforeUnknown = BuildUiOptionStateJson();
                Record(
                    command,
                    "Ui.Configure.MiscAutoSell",
                    "UI",
                    "Rejected",
                    "Unknown auto sell row payload.",
                    beforeUnknown,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"resultCode\":\"unknownPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var before = BuildUiOptionStateJson();
            var itemIds = EnsureAutoSellItemIds();
            LegacyMainWindow.OpenAutoSellAddPicker();
            Record(
                command,
                "Ui.Configure.MiscAutoSell",
                "UI",
                "Succeeded",
                "Auto sell multi item picker opened.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"action\":\"picker-open-add\",\"autoSellEntryCount\":" + IntRaw(itemIds.Count) + ",\"autoSellItemCount\":" + IntRaw(CountValidAutoSellItems(itemIds)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoSellCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var itemIds = EnsureAutoSellItemIds();
            if (string.Equals(payload, "picker-confirm", StringComparison.OrdinalIgnoreCase))
            {
                var selectedItemTypes = LegacyMainWindow.GetAutoSellPickerPendingItemTypes();
                var appendedCount = AppendAutoItemTypes(itemIds, selectedItemTypes);
                LegacyMainWindow.CloseAutoSellPicker();
                if (appendedCount > 0)
                {
                    ConfigService.SaveAll();
                }

                Record(
                    command,
                    "Ui.Configure.MiscAutoSell",
                    "Inventory",
                    appendedCount > 0 ? "Succeeded" : "NotApplicable",
                    appendedCount > 0 ? "Auto sell items added." : "Auto sell item add skipped: no selected items.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-confirm\",\"addedCount\":" + IntRaw(appendedCount) + ",\"selectedCount\":" + IntRaw(selectedItemTypes == null ? 0 : selectedItemTypes.Count) + ",\"autoSellEntryCount\":" + IntRaw(itemIds.Count) + ",\"autoSellItemCount\":" + IntRaw(CountValidAutoSellItems(itemIds)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (string.Equals(payload, "picker-close", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.CloseAutoSellPicker();
                Record(
                    command,
                    "Ui.Configure.MiscAutoSell",
                    "UI",
                    "Succeeded",
                    "Auto sell item picker closed.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-close\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string pickerTogglePrefix = "picker-toggle:";
            if (payload.StartsWith(pickerTogglePrefix, StringComparison.OrdinalIgnoreCase))
            {
                int itemType;
                if (!int.TryParse(payload.Substring(pickerTogglePrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out itemType) ||
                    itemType <= 0 ||
                    IsCoinItemType(itemType) ||
                    ContainsAutoSellItem(itemIds, itemType, -1))
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoSell",
                        "Inventory",
                        "Rejected",
                        "Auto sell item toggle failed: invalid or duplicate item type.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-toggle\",\"resultCode\":\"invalidSelection\",\"payload\":\"" + EscapeJson(payload) + "\",\"autoSellEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                var selected = LegacyMainWindow.ToggleAutoSellPickerPendingItemType(itemType);
                var pendingCount = LegacyMainWindow.GetAutoSellPickerPendingItemTypes().Count;
                Record(
                    command,
                    "Ui.Configure.MiscAutoSell",
                    "UI",
                    "Succeeded",
                    selected ? "Auto sell item marked for add." : "Auto sell item unmarked for add.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-toggle\",\"itemType\":" + IntRaw(itemType) + ",\"selected\":" + BoolRaw(selected) + ",\"pendingCount\":" + IntRaw(pendingCount) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string pickerOpenPrefix = "picker-open:";
            if (payload.StartsWith(pickerOpenPrefix, StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (!TryParseNonNegativeInt(payload.Substring(pickerOpenPrefix.Length), out index) ||
                    index >= itemIds.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoSell",
                        "UI",
                        "Rejected",
                        "Auto sell picker open failed: invalid index.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-open\",\"resultCode\":\"invalidIndex\",\"payload\":\"" + EscapeJson(payload) + "\",\"autoSellEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                LegacyMainWindow.OpenAutoSellPicker(index);
                Record(
                    command,
                    "Ui.Configure.MiscAutoSell",
                    "UI",
                    "Succeeded",
                    "Auto sell item picker opened.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-open\",\"index\":" + IntRaw(index) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string removePrefix = "remove:";
            if (payload.StartsWith(removePrefix, StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (!TryParseNonNegativeInt(payload.Substring(removePrefix.Length), out index) ||
                    index >= itemIds.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoSell",
                        "Inventory",
                        "Rejected",
                        "Auto sell item remove failed: invalid index.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"remove\",\"resultCode\":\"invalidIndex\",\"payload\":\"" + EscapeJson(payload) + "\",\"autoSellEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                var removedItemType = itemIds[index];
                itemIds.RemoveAt(index);
                LegacyMainWindow.CloseAutoSellPicker();
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Configure.MiscAutoSell",
                    "Inventory",
                    "Succeeded",
                    "Auto sell item entry removed.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"remove\",\"index\":" + IntRaw(index) + ",\"itemType\":" + IntRaw(removedItemType) + ",\"autoSellEntryCount\":" + IntRaw(itemIds.Count) + ",\"autoSellItemCount\":" + IntRaw(CountValidAutoSellItems(itemIds)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string pickerSelectPrefix = "picker-select:";
            if (payload.StartsWith(pickerSelectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var remain = payload.Substring(pickerSelectPrefix.Length);
                var separator = remain.IndexOf(':');
                if (separator <= 0 || separator >= remain.Length - 1)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoSell",
                        "Inventory",
                        "Rejected",
                        "Auto sell item selection failed: invalid payload.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-select\",\"resultCode\":\"invalidPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                int index;
                int itemType;
                if (!TryParseNonNegativeInt(remain.Substring(0, separator), out index) ||
                    !int.TryParse(remain.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out itemType) ||
                    itemType <= 0 ||
                    IsCoinItemType(itemType) ||
                    index >= itemIds.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoSell",
                        "Inventory",
                        "Rejected",
                        "Auto sell item selection failed: invalid index or item type.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-select\",\"resultCode\":\"invalidSelection\",\"payload\":\"" + EscapeJson(payload) + "\",\"autoSellEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                if (ContainsAutoSellItem(itemIds, itemType, index))
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoSell",
                        "Inventory",
                        "Rejected",
                        "Auto sell item selection failed: duplicate item type.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-select\",\"resultCode\":\"duplicateItemType\",\"index\":" + IntRaw(index) + ",\"itemType\":" + IntRaw(itemType) + ",\"autoSellEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                itemIds[index] = itemType;
                LegacyMainWindow.CloseAutoSellPicker();
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Configure.MiscAutoSell",
                    "Inventory",
                    "Succeeded",
                    "Auto sell item selected.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-select\",\"index\":" + IntRaw(index) + ",\"itemType\":" + IntRaw(itemType) + ",\"autoSellEntryCount\":" + IntRaw(itemIds.Count) + ",\"autoSellItemCount\":" + IntRaw(CountValidAutoSellItems(itemIds)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            Record(
                command,
                "Ui.Configure.MiscAutoSell",
                "UI",
                "Rejected",
                "Unknown auto sell command payload.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"resultCode\":\"unknownPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoDiscardMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryAutoDiscardEnabled != enabled;
            settings.InventoryAutoDiscardEnabled = enabled;
            ConfigService.SaveAll();
            var itemCount = CountValidAutoDiscardItems(settings.InventoryAutoDiscardItemIds);
            var message = changed
                ? (enabled ? "Auto discard enabled." : "Auto discard disabled.")
                : (enabled ? "Auto discard already enabled." : "Auto discard already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoDiscard",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryAutoDiscard) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"autoDiscardItemCount\":" + IntRaw(itemCount) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoDiscardRow(LegacyUiCommand command, string payload)
        {
            if (string.Equals(payload, "On", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload, "Off", StringComparison.OrdinalIgnoreCase))
            {
                HandleMiscAutoDiscardMode(command, payload);
                return;
            }

            if (!string.Equals(payload, "add-empty", StringComparison.OrdinalIgnoreCase))
            {
                var beforeUnknown = BuildUiOptionStateJson();
                Record(
                    command,
                    "Ui.Configure.MiscAutoDiscard",
                    "UI",
                    "Rejected",
                    "Unknown auto discard row payload.",
                    beforeUnknown,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"resultCode\":\"unknownPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var before = BuildUiOptionStateJson();
            var itemIds = EnsureAutoDiscardItemIds();
            LegacyMainWindow.OpenAutoDiscardAddPicker();
            Record(
                command,
                "Ui.Configure.MiscAutoDiscard",
                "UI",
                "Succeeded",
                "Auto discard multi item picker opened.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"action\":\"picker-open-add\",\"autoDiscardEntryCount\":" + IntRaw(itemIds.Count) + ",\"autoDiscardItemCount\":" + IntRaw(CountValidAutoDiscardItems(itemIds)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoDiscardCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var itemIds = EnsureAutoDiscardItemIds();
            if (string.Equals(payload, "picker-confirm", StringComparison.OrdinalIgnoreCase))
            {
                var selectedItemTypes = LegacyMainWindow.GetAutoDiscardPickerPendingItemTypes();
                var appendedCount = AppendAutoItemTypes(itemIds, selectedItemTypes);
                LegacyMainWindow.CloseAutoDiscardPicker();
                if (appendedCount > 0)
                {
                    ConfigService.SaveAll();
                }

                Record(
                    command,
                    "Ui.Configure.MiscAutoDiscard",
                    "Inventory",
                    appendedCount > 0 ? "Succeeded" : "NotApplicable",
                    appendedCount > 0 ? "Auto discard items added." : "Auto discard item add skipped: no selected items.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-confirm\",\"addedCount\":" + IntRaw(appendedCount) + ",\"selectedCount\":" + IntRaw(selectedItemTypes == null ? 0 : selectedItemTypes.Count) + ",\"autoDiscardEntryCount\":" + IntRaw(itemIds.Count) + ",\"autoDiscardItemCount\":" + IntRaw(CountValidAutoDiscardItems(itemIds)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (string.Equals(payload, "picker-close", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.CloseAutoDiscardPicker();
                Record(
                    command,
                    "Ui.Configure.MiscAutoDiscard",
                    "UI",
                    "Succeeded",
                    "Auto discard item picker closed.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-close\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string pickerTogglePrefix = "picker-toggle:";
            if (payload.StartsWith(pickerTogglePrefix, StringComparison.OrdinalIgnoreCase))
            {
                int itemType;
                if (!int.TryParse(payload.Substring(pickerTogglePrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out itemType) ||
                    itemType <= 0 ||
                    IsCoinItemType(itemType) ||
                    ContainsAutoDiscardItem(itemIds, itemType, -1))
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoDiscard",
                        "Inventory",
                        "Rejected",
                        "Auto discard item toggle failed: invalid or duplicate item type.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-toggle\",\"resultCode\":\"invalidSelection\",\"payload\":\"" + EscapeJson(payload) + "\",\"autoDiscardEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                var selected = LegacyMainWindow.ToggleAutoDiscardPickerPendingItemType(itemType);
                var pendingCount = LegacyMainWindow.GetAutoDiscardPickerPendingItemTypes().Count;
                Record(
                    command,
                    "Ui.Configure.MiscAutoDiscard",
                    "UI",
                    "Succeeded",
                    selected ? "Auto discard item marked for add." : "Auto discard item unmarked for add.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-toggle\",\"itemType\":" + IntRaw(itemType) + ",\"selected\":" + BoolRaw(selected) + ",\"pendingCount\":" + IntRaw(pendingCount) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string pickerOpenPrefix = "picker-open:";
            if (payload.StartsWith(pickerOpenPrefix, StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (!TryParseNonNegativeInt(payload.Substring(pickerOpenPrefix.Length), out index) ||
                    index >= itemIds.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoDiscard",
                        "UI",
                        "Rejected",
                        "Auto discard picker open failed: invalid index.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-open\",\"resultCode\":\"invalidIndex\",\"payload\":\"" + EscapeJson(payload) + "\",\"autoDiscardEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                LegacyMainWindow.OpenAutoDiscardPicker(index);
                Record(
                    command,
                    "Ui.Configure.MiscAutoDiscard",
                    "UI",
                    "Succeeded",
                    "Auto discard item picker opened.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-open\",\"index\":" + IntRaw(index) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string removePrefix = "remove:";
            if (payload.StartsWith(removePrefix, StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (!TryParseNonNegativeInt(payload.Substring(removePrefix.Length), out index) ||
                    index >= itemIds.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoDiscard",
                        "Inventory",
                        "Rejected",
                        "Auto discard item remove failed: invalid index.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"remove\",\"resultCode\":\"invalidIndex\",\"payload\":\"" + EscapeJson(payload) + "\",\"autoDiscardEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                var removedItemType = itemIds[index];
                itemIds.RemoveAt(index);
                LegacyMainWindow.CloseAutoDiscardPicker();
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Configure.MiscAutoDiscard",
                    "Inventory",
                    "Succeeded",
                    "Auto discard item entry removed.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"remove\",\"index\":" + IntRaw(index) + ",\"itemType\":" + IntRaw(removedItemType) + ",\"autoDiscardEntryCount\":" + IntRaw(itemIds.Count) + ",\"autoDiscardItemCount\":" + IntRaw(CountValidAutoDiscardItems(itemIds)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string pickerSelectPrefix = "picker-select:";
            if (payload.StartsWith(pickerSelectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var remain = payload.Substring(pickerSelectPrefix.Length);
                var separator = remain.IndexOf(':');
                if (separator <= 0 || separator >= remain.Length - 1)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoDiscard",
                        "Inventory",
                        "Rejected",
                        "Auto discard item selection failed: invalid payload.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-select\",\"resultCode\":\"invalidPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                int index;
                int itemType;
                if (!TryParseNonNegativeInt(remain.Substring(0, separator), out index) ||
                    !int.TryParse(remain.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out itemType) ||
                    itemType <= 0 ||
                    IsCoinItemType(itemType) ||
                    index >= itemIds.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoDiscard",
                        "Inventory",
                        "Rejected",
                        "Auto discard item selection failed: invalid index or item type.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-select\",\"resultCode\":\"invalidSelection\",\"payload\":\"" + EscapeJson(payload) + "\",\"autoDiscardEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                if (ContainsAutoDiscardItem(itemIds, itemType, index))
                {
                    Record(
                        command,
                        "Ui.Configure.MiscAutoDiscard",
                        "Inventory",
                        "Rejected",
                        "Auto discard item selection failed: duplicate item type.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-select\",\"resultCode\":\"duplicateItemType\",\"index\":" + IntRaw(index) + ",\"itemType\":" + IntRaw(itemType) + ",\"autoDiscardEntryCount\":" + IntRaw(itemIds.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                itemIds[index] = itemType;
                LegacyMainWindow.CloseAutoDiscardPicker();
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Configure.MiscAutoDiscard",
                    "Inventory",
                    "Succeeded",
                    "Auto discard item selected.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-select\",\"index\":" + IntRaw(index) + ",\"itemType\":" + IntRaw(itemType) + ",\"autoDiscardEntryCount\":" + IntRaw(itemIds.Count) + ",\"autoDiscardItemCount\":" + IntRaw(CountValidAutoDiscardItems(itemIds)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            Record(
                command,
                "Ui.Configure.MiscAutoDiscard",
                "UI",
                "Rejected",
                "Unknown auto discard command payload.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"resultCode\":\"unknownPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscQuickReforgeMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.NpcAutoReforgeEnabled != enabled;
            settings.NpcAutoReforgeEnabled = enabled;
            ConfigService.SaveAll();
            var prefixCount = CountValidQuickReforgePrefixes(settings.NpcAutoReforgePrefixes);
            var message = changed
                ? (enabled ? "Quick reforge enabled." : "Quick reforge disabled.")
                : (enabled ? "Quick reforge already enabled." : "Quick reforge already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscQuickReforge",
                "NpcServices",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.NpcAutoReforge) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"quickReforgePrefixCount\":" + IntRaw(prefixCount) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscQuickReforgeCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var prefixes = EnsureQuickReforgePrefixes();
            if (string.Equals(payload, "input", StringComparison.OrdinalIgnoreCase))
            {
                if (!command.IsDoubleClick)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscQuickReforge",
                        "UI",
                        "NotApplicable",
                        "Double-click the affix input field to edit.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"input\",\"doubleClick\":" + BoolRaw(command.IsDoubleClick) + ",\"inputActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                LegacyTextInput.Focus("misc-quick-reforge:prefix", string.Empty);
                Record(
                    command,
                    "Ui.Configure.MiscQuickReforge",
                    "UI",
                    "Succeeded",
                    "Quick reforge input is now active.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"input\",\"doubleClick\":true,\"inputActive\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (string.Equals(payload, "add", StringComparison.OrdinalIgnoreCase))
            {
                var draft = LegacyTextInput.GetDraft("misc-quick-reforge:prefix");
                var normalized = NormalizeQuickReforgePrefix(draft);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    Record(
                        command,
                        "Ui.Configure.MiscQuickReforge",
                        "UI",
                        "Rejected",
                        "Quick reforge add failed: affix text is empty.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"add\",\"resultCode\":\"emptyAffix\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                if (ContainsQuickReforgePrefix(prefixes, normalized, -1))
                {
                    Record(
                        command,
                        "Ui.Configure.MiscQuickReforge",
                        "UI",
                        "Rejected",
                        "Quick reforge add failed: duplicate affix.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"add\",\"resultCode\":\"duplicateAffix\",\"prefix\":\"" + EscapeJson(normalized) + "\",\"quickReforgePrefixCount\":" + IntRaw(prefixes.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                prefixes.Add(normalized);
                LegacyTextInput.ClearFocus();
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Configure.MiscQuickReforge",
                    "NpcServices",
                    "Succeeded",
                    "Quick reforge prefix added.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"add\",\"prefix\":\"" + EscapeJson(normalized) + "\",\"quickReforgePrefixCount\":" + IntRaw(prefixes.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string removePrefix = "remove:";
            if (payload.StartsWith(removePrefix, StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (!TryParseNonNegativeInt(payload.Substring(removePrefix.Length), out index) ||
                    index >= prefixes.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscQuickReforge",
                        "UI",
                        "Rejected",
                        "Quick reforge remove failed: invalid index.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"remove\",\"resultCode\":\"invalidIndex\",\"payload\":\"" + EscapeJson(payload) + "\",\"quickReforgePrefixCount\":" + IntRaw(prefixes.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                var removed = prefixes[index] ?? string.Empty;
                prefixes.RemoveAt(index);
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Configure.MiscQuickReforge",
                    "NpcServices",
                    "Succeeded",
                    "Quick reforge prefix removed.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"remove\",\"index\":" + IntRaw(index) + ",\"prefix\":\"" + EscapeJson(removed) + "\",\"quickReforgePrefixCount\":" + IntRaw(prefixes.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            Record(
                command,
                "Ui.Configure.MiscQuickReforge",
                "UI",
                "Rejected",
                "Unknown quick reforge command payload.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"resultCode\":\"unknownPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscQuickItemHotkeysRow(LegacyUiCommand command, string payload)
        {
            if (string.Equals(payload, "On", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload, "Off", StringComparison.OrdinalIgnoreCase))
            {
                HandleMiscQuickItemHotkeysMode(command, payload);
                return;
            }

            if (!string.Equals(payload, "add-empty", StringComparison.OrdinalIgnoreCase))
            {
                var beforeUnknown = BuildUiOptionStateJson();
                Record(
                    command,
                    "Ui.Configure.MiscQuickItemHotkeys",
                    "UI",
                    "Rejected",
                    "Unknown quick item row payload.",
                    beforeUnknown,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"resultCode\":\"unknownPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var before = BuildUiOptionStateJson();
            var bindings = EnsureQuickItemHotkeyBindings();
            bindings.Add(new QuickItemHotkeyBinding
            {
                Hotkey = string.Empty,
                ItemTypes = new List<int>(),
                DisplayName = string.Empty,
                Enabled = true
            });
            ConfigService.SaveAll();
            var createdIndex = bindings.Count - 1;
            Record(
                command,
                "Ui.Configure.MiscQuickItemHotkeys",
                "Inventory",
                "Succeeded",
                "Added empty quick item hotkey entry.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"action\":\"add-empty\",\"createdIndex\":" + IntRaw(createdIndex) + ",\"bindingCount\":" + IntRaw(bindings.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscQuickItemHotkeysCommand(LegacyUiCommand command, string payload, GameStateSnapshot snapshot)
        {
            var before = BuildUiOptionStateJson();
            var bindings = EnsureQuickItemHotkeyBindings();
            if (string.Equals(payload, "picker-close", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.CloseQuickItemPicker();
                Record(
                    command,
                    "Ui.Configure.MiscQuickItemHotkeys",
                    "UI",
                    "Succeeded",
                    "Quick item picker closed.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-close\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (string.Equals(payload, "capture-stop", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.StopQuickItemHotkeyCapture();
                Record(
                    command,
                    "Ui.Configure.MiscQuickItemHotkeys",
                    "UI",
                    "Succeeded",
                    "Quick item hotkey capture cancelled.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"capture-stop\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string pickerOpenPrefix = "picker-open:";
            if (payload.StartsWith(pickerOpenPrefix, StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (!TryParseNonNegativeInt(payload.Substring(pickerOpenPrefix.Length), out index) ||
                    index >= bindings.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscQuickItemHotkeys",
                        "UI",
                        "Rejected",
                        "Quick item picker open failed: invalid index.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-open\",\"resultCode\":\"invalidIndex\",\"payload\":\"" + EscapeJson(payload) + "\",\"bindingCount\":" + IntRaw(bindings.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                LegacyMainWindow.OpenQuickItemPicker(index);
                Record(
                    command,
                    "Ui.Configure.MiscQuickItemHotkeys",
                    "UI",
                    "Succeeded",
                    "Quick item picker opened.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-open\",\"index\":" + IntRaw(index) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string captureStartPrefix = "capture-start:";
            if (payload.StartsWith(captureStartPrefix, StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (!TryParseNonNegativeInt(payload.Substring(captureStartPrefix.Length), out index) ||
                    index >= bindings.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscQuickItemHotkeys",
                        "UI",
                        "Rejected",
                        "Quick item capture start failed: invalid index.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"capture-start\",\"resultCode\":\"invalidIndex\",\"payload\":\"" + EscapeJson(payload) + "\",\"bindingCount\":" + IntRaw(bindings.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                LegacyMainWindow.StartQuickItemHotkeyCapture(index);
                Record(
                    command,
                    "Ui.Configure.MiscQuickItemHotkeys",
                    "UI",
                    "Succeeded",
                    "Quick item hotkey capture started.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"capture-start\",\"index\":" + IntRaw(index) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string removePrefix = "remove:";
            if (payload.StartsWith(removePrefix, StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (!TryParseNonNegativeInt(payload.Substring(removePrefix.Length), out index) ||
                    index >= bindings.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscQuickItemHotkeys",
                        "Inventory",
                        "Rejected",
                        "Quick item remove failed: invalid index.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"remove\",\"resultCode\":\"invalidIndex\",\"payload\":\"" + EscapeJson(payload) + "\",\"bindingCount\":" + IntRaw(bindings.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                bindings.RemoveAt(index);
                LegacyMainWindow.CloseQuickItemPicker();
                LegacyMainWindow.StopQuickItemHotkeyCapture();
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Configure.MiscQuickItemHotkeys",
                    "Inventory",
                    "Succeeded",
                    "Quick item entry removed.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"remove\",\"index\":" + IntRaw(index) + ",\"bindingCount\":" + IntRaw(bindings.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            const string pickerSelectPrefix = "picker-select:";
            if (payload.StartsWith(pickerSelectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var remain = payload.Substring(pickerSelectPrefix.Length);
                var separator = remain.IndexOf(':');
                if (separator <= 0 || separator >= remain.Length - 1)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscQuickItemHotkeys",
                        "Inventory",
                        "Rejected",
                        "Quick item selection failed: invalid payload.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-select\",\"resultCode\":\"invalidPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                int index;
                int itemType;
                if (!TryParseNonNegativeInt(remain.Substring(0, separator), out index) ||
                    !int.TryParse(remain.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out itemType) ||
                    itemType <= 0 ||
                    index >= bindings.Count)
                {
                    Record(
                        command,
                        "Ui.Configure.MiscQuickItemHotkeys",
                        "Inventory",
                        "Rejected",
                        "Quick item selection failed: invalid index or item type.",
                        before,
                        BuildUiOptionStateJson(),
                        "{\"submitted\":false,\"action\":\"picker-select\",\"resultCode\":\"invalidSelection\",\"payload\":\"" + EscapeJson(payload) + "\",\"bindingCount\":" + IntRaw(bindings.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                var binding = bindings[index] ?? new QuickItemHotkeyBinding();
                binding.ItemTypes = new List<int> { itemType };
                binding.DisplayName = ResolveQuickItemDisplayName(snapshot, itemType);
                binding.Enabled = true;
                bindings[index] = binding;
                LegacyMainWindow.CloseQuickItemPicker();
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Configure.MiscQuickItemHotkeys",
                    "Inventory",
                    "Succeeded",
                    "Quick item selected.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"action\":\"picker-select\",\"index\":" + IntRaw(index) + ",\"itemType\":" + IntRaw(itemType) + ",\"bindingCount\":" + IntRaw(bindings.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            Record(
                command,
                "Ui.Configure.MiscQuickItemHotkeys",
                "UI",
                "Rejected",
                "Unknown quick item command payload.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"resultCode\":\"unknownPayload\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static List<QuickItemHotkeyBinding> EnsureQuickItemHotkeyBindings()
        {
            var hotkeySettings = ConfigService.HotkeySettings;
            if (hotkeySettings == null)
            {
                return new List<QuickItemHotkeyBinding>();
            }

            if (hotkeySettings.QuickItemHotkeyBindings == null)
            {
                hotkeySettings.QuickItemHotkeyBindings = new List<QuickItemHotkeyBinding>();
            }

            return hotkeySettings.QuickItemHotkeyBindings;
        }

        private static List<int> EnsureAutoSellItemIds()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (settings.InventoryAutoSellItemIds == null)
            {
                settings.InventoryAutoSellItemIds = new List<int>(AutoSellCompat.DefaultAutoSellItemIds);
            }

            return settings.InventoryAutoSellItemIds;
        }

        private static List<int> EnsureAutoDiscardItemIds()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (settings.InventoryAutoDiscardItemIds == null)
            {
                settings.InventoryAutoDiscardItemIds = new List<int>();
            }

            return settings.InventoryAutoDiscardItemIds;
        }

        private static List<string> EnsureQuickReforgePrefixes()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (settings.NpcAutoReforgePrefixes == null)
            {
                settings.NpcAutoReforgePrefixes = new List<string>();
            }

            return settings.NpcAutoReforgePrefixes;
        }

        private static int AppendAutoItemTypes(List<int> itemIds, IList<int> selectedItemTypes)
        {
            if (itemIds == null || selectedItemTypes == null || selectedItemTypes.Count <= 0)
            {
                return 0;
            }

            var appendedCount = 0;
            for (var index = 0; index < selectedItemTypes.Count; index++)
            {
                var itemType = selectedItemTypes[index];
                if (itemType <= 0 ||
                    IsCoinItemType(itemType) ||
                    ContainsAutoSellItem(itemIds, itemType, -1))
                {
                    continue;
                }

                itemIds.Add(itemType);
                appendedCount++;
            }

            return appendedCount;
        }

        private static int CountValidAutoSellItems(IList<int> itemIds)
        {
            if (itemIds == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < itemIds.Count; index++)
            {
                if (itemIds[index] > 0 && !IsCoinItemType(itemIds[index]))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountValidAutoDiscardItems(IList<int> itemIds)
        {
            return CountValidAutoSellItems(itemIds);
        }

        private static int CountValidQuickReforgePrefixes(IList<string> prefixes)
        {
            if (prefixes == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < prefixes.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(prefixes[index]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsAutoSellItem(IList<int> itemIds, int itemType, int exceptIndex)
        {
            if (itemIds == null || itemType <= 0)
            {
                return false;
            }

            for (var index = 0; index < itemIds.Count; index++)
            {
                if (index != exceptIndex && itemIds[index] == itemType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAutoDiscardItem(IList<int> itemIds, int itemType, int exceptIndex)
        {
            return ContainsAutoSellItem(itemIds, itemType, exceptIndex);
        }

        private static bool ContainsQuickReforgePrefix(IList<string> prefixes, string value, int exceptIndex)
        {
            if (prefixes == null || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (var index = 0; index < prefixes.Count; index++)
            {
                if (index == exceptIndex || string.IsNullOrWhiteSpace(prefixes[index]))
                {
                    continue;
                }

                if (string.Equals(prefixes[index].Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeQuickReforgePrefix(string raw)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? string.Empty
                : raw.Trim();
        }

        private static bool IsCoinItemType(int itemType)
        {
            return itemType >= 71 && itemType <= 74;
        }

        private static bool TryParseNonNegativeInt(string raw, out int value)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        private static string ResolveQuickItemDisplayName(GameStateSnapshot snapshot, int itemType)
        {
            if (itemType <= 0)
            {
                return string.Empty;
            }

            var inventory = snapshot == null || snapshot.Inventory == null
                ? null
                : snapshot.Inventory.Items;
            if (inventory == null)
            {
                return ItemSwapFamilyCompat.ResolveItemDisplayName(itemType, itemType.ToString(CultureInfo.InvariantCulture));
            }

            for (var slot = 0; slot < inventory.Count; slot++)
            {
                var item = inventory[slot];
                if (item == null)
                {
                    continue;
                }

                if (item.Type == itemType && item.Stack > 0 && !string.IsNullOrWhiteSpace(item.Name))
                {
                    return item.Name.Trim();
                }
            }

            return ItemSwapFamilyCompat.ResolveItemDisplayName(itemType, itemType.ToString(CultureInfo.InvariantCulture));
        }

        private static void ToggleAutoMana(LegacyUiCommand command)
        {
            var before = BuildAutoRecoveryBeforeJson();
            var enabled = AutoRecoveryService.ToggleAutoMana();
            Record(
                command,
                "Ui.Toggle.AutoMana",
                "AutoRecovery",
                "Succeeded",
                "AutoMana " + (enabled ? "enabled." : "disabled."),
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetAutoHealMode(LegacyUiCommand command, string mode)
        {
            var before = BuildAutoRecoveryBeforeJson();
            var normalized = AutoRecoveryService.SetAutoHealMode(mode);
            Record(
                command,
                "Ui.Toggle.AutoHealMode",
                "AutoRecovery",
                "Succeeded",
                string.Equals(normalized, AutoRecoverySettings.HealModeSmart, StringComparison.OrdinalIgnoreCase)
                    ? "AutoHeal smart mode selected; suitable recovery potions will be chosen by missing life."
                    : "AutoHeal mode set to " + normalized + ".",
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"autoHealMode\":\"" + EscapeJson(normalized) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetAutoManaMode(LegacyUiCommand command, string mode)
        {
            var before = BuildAutoRecoveryBeforeJson();
            var normalized = AutoRecoveryService.SetAutoManaMode(mode);
            Record(
                command,
                "Ui.Toggle.AutoManaMode",
                "AutoRecovery",
                "Succeeded",
                string.Equals(normalized, AutoRecoverySettings.ManaModeManaFlower, StringComparison.OrdinalIgnoreCase)
                    ? "AutoMana selected; an available mana potion will be used when the selected mana weapon cannot be used with current mana and mana can be restored."
                    : "AutoMana disabled.",
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"autoManaMode\":\"" + EscapeJson(normalized) + "\",\"executionMode\":\"" + (string.Equals(normalized, AutoRecoverySettings.ManaModeManaFlower, StringComparison.OrdinalIgnoreCase) ? "OriginalRecoveryItemUse" : string.Empty) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetAutoBuffEnabled(LegacyUiCommand command, bool enabled)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildAutoRecoveryBeforeJson();
            var changed = settings.AutoBuffEnabled != enabled;
            if (changed)
            {
                AutoRecoveryService.ToggleAutoBuff();
            }

            Record(
                command,
                "Ui.Toggle.AutoBuff",
                "AutoRecovery",
                changed ? "Succeeded" : "NotApplicable",
                enabled
                    ? "AutoBuff enabled; missing whitelisted buffs may be used automatically."
                    : "AutoBuff disabled.",
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"immediateReconcileTriggered\":" + BoolRaw(changed && enabled) + ",\"triggerReason\":\"" + (changed && enabled ? "AutoBuffEnabled" : string.Empty) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleAutoBuff(LegacyUiCommand command)
        {
            var before = BuildAutoRecoveryBeforeJson();
            var enabled = AutoRecoveryService.ToggleAutoBuff();
            if (enabled)
            {
                AutoRecoveryService.RequestImmediateAutoBuffReconcile("AutoBuffEnabled");
            }

            Record(
                command,
                "Ui.Toggle.AutoBuff",
                "AutoRecovery",
                "Succeeded",
                enabled
                    ? "AutoBuff enabled; missing whitelisted buffs may be used automatically."
                    : "AutoBuff disabled.",
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"immediateReconcileTriggered\":" + BoolRaw(enabled) + ",\"triggerReason\":\"" + (enabled ? "AutoBuffEnabled" : string.Empty) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetAutoNurseEnabled(LegacyUiCommand command, bool enabled)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildAutoRecoveryBeforeJson();
            var changed = settings.AutoNurseEnabled != enabled;
            AutoRecoveryService.SetAutoNurseEnabled(enabled);
            Record(
                command,
                "Ui.Toggle.AutoNurse",
                "AutoRecovery",
                changed ? "Succeeded" : "NotApplicable",
                enabled
                    ? "Auto nurse enabled; reachable nurse healing may be attempted automatically."
                    : "Auto nurse disabled.",
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"implemented\":true,\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetAutoStationBuffEnabled(LegacyUiCommand command, bool enabled)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildAutoRecoveryBeforeJson();
            var changed = settings.AutoStationBuffEnabled != enabled;
            AutoRecoveryService.SetAutoStationBuffEnabled(enabled);
            Record(
                command,
                "Ui.Toggle.AutoStationBuff",
                "AutoRecovery",
                changed ? "Succeeded" : "NotApplicable",
                enabled
                    ? "Auto station buff enabled; reachable buff furniture may be interacted with automatically."
                    : "Auto station buff disabled.",
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"implemented\":true,\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleAutoBuffFollowOption(LegacyUiCommand command, bool followAdd)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildUiOptionStateJson();
            bool enabled;
            if (followAdd)
            {
                settings.AutoBuffFollowAddEnabled = !settings.AutoBuffFollowAddEnabled;
                enabled = settings.AutoBuffFollowAddEnabled;
            }
            else
            {
                settings.AutoBuffFollowRemoveEnabled = !settings.AutoBuffFollowRemoveEnabled;
                enabled = settings.AutoBuffFollowRemoveEnabled;
            }

            ConfigService.SaveAll();
            Record(
                command,
                followAdd ? "Ui.Toggle.AutoBuffFollowAdd" : "Ui.Toggle.AutoBuffFollowRemove",
                "UI",
                "Succeeded",
                (followAdd ? "AutoBuff follow-add" : "AutoBuff follow-remove") + " " + (enabled ? "enabled." : "disabled."),
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

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
                    ? "自动连点已开启：长按使用物品时，会通过 ItemCheck 补一次可用的非原生连点物品。"
                    : "自动连点已关闭。";
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
            else
            {
                changed = settings.CombatEquipmentWarningEnabled != enabled;
                settings.CombatEquipmentWarningEnabled = enabled;
                scenario = "Ui.Toggle.CombatEquipmentWarning";
                implemented = true;
                message = enabled
                    ? "装备提示已开启：Boss 或非血月事件期间，穿着非战斗装备时会在玩家头顶提示。"
                    : "装备提示已关闭。";
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

        private static void SetFishingFeatureEnabled(LegacyUiCommand command, string payload)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildFishingUiStateJson();
            var separator = string.IsNullOrWhiteSpace(payload) ? -1 : payload.LastIndexOf(':');
            if (separator <= 0 || separator >= payload.Length - 1)
            {
                Record(
                    command,
                    "Ui.Toggle.FishingFeature",
                    "UI",
                    "NotApplicable",
                    "Fishing toggle ignored because the feature id or mode was invalid.",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var featureId = payload.Substring(0, separator);
            var enabled = IsOnMode(payload.Substring(separator + 1));
            bool changed;
            bool disabledExclusive = false;
            bool implemented = false;
            string label;
            string scenario;
            if (string.Equals(featureId, "fishing.auto_fish", StringComparison.Ordinal))
            {
                changed = settings.FishingAutoFishEnabled != enabled;
                settings.FishingAutoFishEnabled = enabled;
                label = "自动钓鱼";
                scenario = "Ui.Toggle.FishingAutoFish";
                implemented = true;
            }
            else if (string.Equals(featureId, "fishing.auto_loadout", StringComparison.Ordinal))
            {
                changed = settings.FishingAutoLoadoutEnabled != enabled;
                settings.FishingAutoLoadoutEnabled = enabled;
                if (enabled && settings.FishingAutoEquipmentEnabled)
                {
                    settings.FishingAutoEquipmentEnabled = false;
                    disabledExclusive = true;
                    changed = true;
                }

                label = "自动换装";
                scenario = "Ui.Toggle.FishingAutoLoadout";
                implemented = true;
            }
            else if (string.Equals(featureId, "fishing.auto_equipment", StringComparison.Ordinal))
            {
                changed = settings.FishingAutoEquipmentEnabled != enabled;
                settings.FishingAutoEquipmentEnabled = enabled;
                if (enabled && settings.FishingAutoLoadoutEnabled)
                {
                    settings.FishingAutoLoadoutEnabled = false;
                    disabledExclusive = true;
                    changed = true;
                }

                label = "自动配装";
                scenario = "Ui.Toggle.FishingAutoEquipment";
                implemented = true;
            }
            else if (string.Equals(featureId, "fishing.auto_store_quest_fish", StringComparison.Ordinal))
            {
                changed = settings.FishingAutoStoreQuestFishEnabled != enabled;
                settings.FishingAutoStoreQuestFishEnabled = enabled;
                settings.FishingAutoStoreMode = enabled ? FishingAutoStoreModes.QuestFish : FishingAutoStoreModes.Off;
                label = "自动存放鱼";
                scenario = "Ui.Toggle.FishingAutoStoreQuestFish";
                implemented = true;
            }
            else
            {
                Record(
                    command,
                    "Ui.Toggle.FishingFeature",
                    "UI",
                    "NotApplicable",
                    "Unknown fishing feature id: " + featureId + ".",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"featureId\":\"" + EscapeJson(featureId) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            ConfigService.SaveAll();
            var uiOnly = !implemented;
            var message = enabled
                ? implemented
                    ? label + "已开启。"
                    : label + "已开启：当前仅保存 UI 状态，实际自动配装将在后续阶段实现。"
                : label + "已关闭。";
            Record(
                command,
                scenario,
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(implemented) + ",\"uiOnly\":" + BoolRaw(uiOnly) + ",\"featureId\":\"" + EscapeJson(featureId) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"exclusiveGroup\":\"" + (disabledExclusive ? "fishing.equipment_strategy" : string.Empty) + "\",\"disabledExclusivePeer\":" + BoolRaw(disabledExclusive) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

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
