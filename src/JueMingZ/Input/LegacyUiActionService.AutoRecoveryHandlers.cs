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

        private static string ResolveRecoveryItemDisplayName(string kind, int itemType)
        {
            if (itemType <= 0)
            {
                return string.Empty;
            }

            var definitions = string.Equals(kind, "heal", StringComparison.Ordinal)
                ? RecoveryPotionDefinitionCatalog.GetHealDefinitions()
                : RecoveryPotionDefinitionCatalog.GetManaDefinitions();
            if (definitions != null)
            {
                for (var index = 0; index < definitions.Length; index++)
                {
                    var definition = definitions[index];
                    if (definition != null && definition.ItemType == itemType && !string.IsNullOrWhiteSpace(definition.ItemName))
                    {
                        return definition.ItemName.Trim();
                    }
                }
            }

            return ItemSwapFamilyCompat.ResolveItemDisplayName(itemType, itemType.ToString(CultureInfo.InvariantCulture));
        }

        private static string NormalizeAutoRecoveryItemKind(string kind)
        {
            if (string.Equals(kind, "heal", StringComparison.OrdinalIgnoreCase))
            {
                return "heal";
            }

            return string.Equals(kind, "mana", StringComparison.OrdinalIgnoreCase) ? "mana" : string.Empty;
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

        private static void ToggleAutoRecoveryItemConfig(LegacyUiCommand command, string kind)
        {
            kind = NormalizeAutoRecoveryItemKind(kind);
            var before = BuildAutoRecoveryBeforeJson();
            if (string.IsNullOrEmpty(kind))
            {
                Record(
                    command,
                    "Ui.Toggle.AutoRecoveryItemConfig",
                    "AutoRecovery",
                    "NotApplicable",
                    "未知的自动恢复物品配置面板。",
                    before,
                    BuildAutoRecoveryAfterJson(),
                    "{\"submitted\":false,\"implemented\":false,\"kind\":\"" + EscapeJson(kind) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            LegacyMainWindow.ToggleAutoRecoveryItemConfigPopup(kind);
            Record(
                command,
                "Ui.Toggle.AutoRecoveryItemConfig",
                "AutoRecovery",
                "Succeeded",
                (string.Equals(kind, "heal", StringComparison.Ordinal) ? "自动回血" : "自动回蓝") + "配置已切换。",
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"implemented\":true,\"kind\":\"" + EscapeJson(kind) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleAutoRecoveryItemOption(LegacyUiCommand command, string payload)
        {
            var before = BuildAutoRecoveryBeforeJson();
            var separator = string.IsNullOrWhiteSpace(payload) ? -1 : payload.IndexOf(':');
            int itemType;
            if (separator <= 0 ||
                separator >= payload.Length - 1 ||
                !int.TryParse(payload.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out itemType) ||
                itemType <= 0)
            {
                Record(
                    command,
                    "Ui.Toggle.AutoRecoveryItemOption",
                    "AutoRecovery",
                    "NotApplicable",
                    "自动恢复物品配置项无效。",
                    before,
                    BuildAutoRecoveryAfterJson(),
                    "{\"submitted\":false,\"implemented\":false,\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var kind = NormalizeAutoRecoveryItemKind(payload.Substring(0, separator));
            if (string.IsNullOrEmpty(kind))
            {
                Record(
                    command,
                    "Ui.Toggle.AutoRecoveryItemOption",
                    "AutoRecovery",
                    "NotApplicable",
                    "未知的自动恢复物品类型。",
                    before,
                    BuildAutoRecoveryAfterJson(),
                    "{\"submitted\":false,\"implemented\":false,\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            bool enabled;
            var changed = string.Equals(kind, "heal", StringComparison.Ordinal)
                ? AutoRecoveryItemFilter.ToggleHealItem(settings, itemType, out enabled)
                : AutoRecoveryItemFilter.ToggleManaItem(settings, itemType, out enabled);
            if (changed)
            {
                ConfigService.SaveAll();
            }

            var blockedCount = string.Equals(kind, "heal", StringComparison.Ordinal)
                ? AutoRecoveryItemFilter.CountBlockedHealItems(settings)
                : AutoRecoveryItemFilter.CountBlockedManaItems(settings);
            var itemName = ResolveRecoveryItemDisplayName(kind, itemType);
            Record(
                command,
                "Ui.Toggle.AutoRecoveryItemOption",
                "AutoRecovery",
                changed ? "Succeeded" : "NotApplicable",
                itemName + (enabled ? "已允许自动使用。" : "已禁止自动使用。"),
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"implemented\":true,\"kind\":\"" + EscapeJson(kind) + "\",\"itemType\":" + IntRaw(itemType) + ",\"itemName\":\"" + EscapeJson(itemName) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"blockedCount\":" + IntRaw(blockedCount) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
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
    }
}
