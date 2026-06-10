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

        private static bool IsCoinItemType(int itemType)
        {
            return itemType >= 71 && itemType <= 74;
        }

        private static bool TryParseNonNegativeInt(string raw, out int value)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
        }
    }
}
