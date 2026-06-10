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
    }
}
