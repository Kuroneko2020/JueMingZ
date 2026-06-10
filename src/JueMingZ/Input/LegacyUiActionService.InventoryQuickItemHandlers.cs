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
    }
}
