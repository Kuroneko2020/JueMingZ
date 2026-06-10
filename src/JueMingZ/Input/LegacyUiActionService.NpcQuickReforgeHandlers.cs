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

        private static List<string> EnsureQuickReforgePrefixes()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (settings.NpcAutoReforgePrefixes == null)
            {
                settings.NpcAutoReforgePrefixes = new List<string>();
            }

            return settings.NpcAutoReforgePrefixes;
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
    }
}
