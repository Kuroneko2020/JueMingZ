using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
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
        private static void SetInformationFeatureEnabled(LegacyUiCommand command, string payload)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationUiStateJson();
            var separator = string.IsNullOrWhiteSpace(payload) ? -1 : payload.LastIndexOf(':');
            if (separator <= 0 || separator >= payload.Length - 1)
            {
                Record(
                    command,
                    "Ui.Toggle.Information",
                    "UI",
                    "NotApplicable",
                    "Information toggle ignored because the feature id or mode was invalid.",
                    before,
                    BuildInformationUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var featureId = payload.Substring(0, separator);
            var enabled = IsOnMode(payload.Substring(separator + 1));
            bool changed;
            string label;
            if (string.Equals(featureId, "information.enemy_name_labels", StringComparison.Ordinal))
            {
                changed = settings.InformationEnemyNameLabelsEnabled != enabled;
                settings.InformationEnemyNameLabelsEnabled = enabled;
                label = "敌怪显名";
            }
            else if (string.Equals(featureId, "information.critter_name_labels", StringComparison.Ordinal))
            {
                changed = settings.InformationCritterNameLabelsEnabled != enabled;
                settings.InformationCritterNameLabelsEnabled = enabled;
                label = "动物显名";
            }
            else if (string.Equals(featureId, "information.highlight_life_crystal", StringComparison.Ordinal))
            {
                changed = settings.InformationHighlightLifeCrystalEnabled != enabled;
                settings.InformationHighlightLifeCrystalEnabled = enabled;
                label = "显示生命水晶";
            }
            else if (string.Equals(featureId, "information.highlight_mana_crystal", StringComparison.Ordinal))
            {
                changed = settings.InformationHighlightManaCrystalEnabled != enabled;
                settings.InformationHighlightManaCrystalEnabled = enabled;
                label = "显示魔力水晶";
            }
            else if (string.Equals(featureId, "information.highlight_digtoise", StringComparison.Ordinal))
            {
                changed = settings.InformationHighlightDigtoiseEnabled != enabled;
                settings.InformationHighlightDigtoiseEnabled = enabled;
                label = "显示碎岩龟";
            }
            else if (string.Equals(featureId, "information.highlight_life_fruit", StringComparison.Ordinal))
            {
                changed = settings.InformationHighlightLifeFruitEnabled != enabled;
                settings.InformationHighlightLifeFruitEnabled = enabled;
                label = "显示生命果";
            }
            else if (string.Equals(featureId, "information.highlight_dragon_egg", StringComparison.Ordinal))
            {
                changed = settings.InformationHighlightDragonEggEnabled != enabled;
                settings.InformationHighlightDragonEggEnabled = enabled;
                label = "显示龙蛋";
            }
            else if (string.Equals(featureId, "information.biome_display", StringComparison.Ordinal))
            {
                changed = settings.InformationBiomeDisplayEnabled != enabled;
                settings.InformationBiomeDisplayEnabled = enabled;
                label = "群系显示";
            }
            else if (string.Equals(featureId, "information.world_infection", StringComparison.Ordinal))
            {
                changed = settings.InformationWorldInfectionEnabled != enabled;
                settings.InformationWorldInfectionEnabled = enabled;
                label = "世界感染";
            }
            else if (string.Equals(featureId, "information.luck_value", StringComparison.Ordinal))
            {
                changed = settings.InformationLuckValueEnabled != enabled;
                settings.InformationLuckValueEnabled = enabled;
                label = "幸运值";
            }
            else if (string.Equals(featureId, "information.fishing_catches", StringComparison.Ordinal))
            {
                changed = settings.InformationFishingCatchesEnabled != enabled;
                settings.InformationFishingCatchesEnabled = enabled;
                label = "完整鱼获";
            }
            else if (string.Equals(featureId, "information.fishing_filtered_catches", StringComparison.Ordinal))
            {
                changed = settings.InformationFishingFilteredCatchesEnabled != enabled;
                settings.InformationFishingFilteredCatchesEnabled = enabled;
                label = "过滤鱼获";
            }
            else if (string.Equals(featureId, "information.angler_quest", StringComparison.Ordinal))
            {
                changed = settings.InformationAnglerQuestEnabled != enabled;
                settings.InformationAnglerQuestEnabled = enabled;
                label = "渔夫任务";
            }
            else
            {
                Record(
                    command,
                    "Ui.Toggle.Information",
                    "UI",
                    "NotApplicable",
                    "Unknown information feature id: " + featureId + ".",
                    before,
                    BuildInformationUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"featureId\":\"" + EscapeJson(featureId) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.InformationFeature",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                label + (enabled ? "已开启。" : "已关闭。"),
                before,
                BuildInformationUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(featureId) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetInformationNpcNameLabelsMode(LegacyUiCommand command, string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationUiStateJson();
            var normalized = NormalizeInformationNpcNameLabelsMode(mode);
            var changed = !string.Equals(settings.InformationNpcNameLabelsMode, normalized, StringComparison.OrdinalIgnoreCase);
            settings.InformationNpcNameLabelsMode = normalized;
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.InformationNpcNameLabels",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                string.Equals(normalized, "Name", StringComparison.OrdinalIgnoreCase)
                    ? "NPC显名已切换为显示名字。"
                    : (string.Equals(normalized, "Type", StringComparison.OrdinalIgnoreCase) ? "NPC显名已切换为显示类型。" : "NPC显名已关闭。"),
                before,
                BuildInformationUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.npc_name_labels\",\"mode\":\"" + EscapeJson(normalized) + "\",\"enabled\":" + BoolRaw(!string.Equals(normalized, "Off", StringComparison.OrdinalIgnoreCase)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetInformationChestNameLabelsMode(LegacyUiCommand command, string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationUiStateJson();
            var normalized = NormalizeInformationChestNameLabelsMode(mode);
            var changed = !string.Equals(settings.InformationChestNameLabelsMode, normalized, StringComparison.OrdinalIgnoreCase);
            settings.InformationChestNameLabelsMode = normalized;
            settings.InformationChestNameLabelsEnabled = !string.Equals(normalized, "Off", StringComparison.OrdinalIgnoreCase);
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.InformationChestNameLabels",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                string.Equals(normalized, "Always", StringComparison.OrdinalIgnoreCase)
                    ? "宝箱显名已切换为始终显示。"
                    : (string.Equals(normalized, "Opened", StringComparison.OrdinalIgnoreCase) ? "宝箱显名已切换为显示开过的宝箱。" : "宝箱显名已关闭。"),
                before,
                BuildInformationUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.chest_name_labels\",\"mode\":\"" + EscapeJson(normalized) + "\",\"enabled\":" + BoolRaw(!string.Equals(normalized, "Off", StringComparison.OrdinalIgnoreCase)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetInformationSignTextLabelsMode(LegacyUiCommand command, string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationUiStateJson();
            var normalized = NormalizeInformationSignTextLabelsMode(mode);
            var changed = !string.Equals(settings.InformationSignTextLabelsMode, normalized, StringComparison.OrdinalIgnoreCase);
            settings.InformationSignTextLabelsMode = normalized;
            settings.InformationSignTextMaxLines = InformationSignTextModes.ClampLines(settings.InformationSignTextMaxLines);
            settings.InformationSignTextMaxCharacters = InformationSignTextModes.ClampCharacters(settings.InformationSignTextMaxCharacters);
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.InformationSignTextLabels",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                SignTextModeMessage(normalized),
                before,
                BuildInformationUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.sign_text_labels\",\"mode\":\"" + EscapeJson(normalized) + "\",\"enabled\":" + BoolRaw(InformationSignTextModes.IsEnabled(normalized)) + ",\"maxLines\":" + IntRaw(settings.InformationSignTextMaxLines) + ",\"maxCharacters\":" + IntRaw(settings.InformationSignTextMaxCharacters) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void AdjustInformationSignTextLimit(LegacyUiCommand command, int direction)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationUiStateJson();
            var mode = NormalizeInformationSignTextLabelsMode(settings.InformationSignTextLabelsMode);
            if (string.Equals(mode, InformationSignTextModes.Lines, StringComparison.OrdinalIgnoreCase))
            {
                var oldValue = InformationSignTextModes.ClampLines(settings.InformationSignTextMaxLines);
                var newValue = InformationSignTextModes.AdjustLines(oldValue, direction);
                settings.InformationSignTextMaxLines = newValue;
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Adjust.InformationSignTextLimit",
                    "UI",
                    oldValue == newValue ? "NotApplicable" : "Succeeded",
                    "牌子显示行数上限已设为前 " + newValue.ToString(CultureInfo.InvariantCulture) + " 行。",
                    before,
                    BuildInformationUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.sign_text_labels\",\"mode\":\"" + EscapeJson(mode) + "\",\"maxLines\":" + IntRaw(newValue) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (string.Equals(mode, InformationSignTextModes.Characters, StringComparison.OrdinalIgnoreCase))
            {
                var oldValue = InformationSignTextModes.ClampCharacters(settings.InformationSignTextMaxCharacters);
                var newValue = InformationSignTextModes.AdjustCharacters(oldValue, direction);
                settings.InformationSignTextMaxCharacters = newValue;
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Adjust.InformationSignTextLimit",
                    "UI",
                    oldValue == newValue ? "NotApplicable" : "Succeeded",
                    "牌子显示字数上限已设为前 " + newValue.ToString(CultureInfo.InvariantCulture) + " 字。",
                    before,
                    BuildInformationUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.sign_text_labels\",\"mode\":\"" + EscapeJson(mode) + "\",\"maxCharacters\":" + IntRaw(newValue) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            Record(
                command,
                "Ui.Adjust.InformationSignTextLimit",
                "UI",
                "NotApplicable",
                "牌子显示当前模式没有数量上限。",
                before,
                BuildInformationUiStateJson(),
                "{\"submitted\":false,\"implemented\":false,\"featureId\":\"information.sign_text_labels\",\"mode\":\"" + EscapeJson(mode) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetInformationTombstoneTextLabelsMode(LegacyUiCommand command, string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationUiStateJson();
            var normalized = NormalizeInformationSignTextLabelsMode(mode);
            var changed = !string.Equals(settings.InformationTombstoneTextLabelsMode, normalized, StringComparison.OrdinalIgnoreCase);
            settings.InformationTombstoneTextLabelsMode = normalized;
            settings.InformationTombstoneTextMaxLines = InformationSignTextModes.ClampLines(settings.InformationTombstoneTextMaxLines);
            settings.InformationTombstoneTextMaxCharacters = InformationSignTextModes.ClampCharacters(settings.InformationTombstoneTextMaxCharacters);
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Toggle.InformationTombstoneTextLabels",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                TombstoneTextModeMessage(normalized),
                before,
                BuildInformationUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.tombstone_text_labels\",\"mode\":\"" + EscapeJson(normalized) + "\",\"enabled\":" + BoolRaw(InformationSignTextModes.IsEnabled(normalized)) + ",\"maxLines\":" + IntRaw(settings.InformationTombstoneTextMaxLines) + ",\"maxCharacters\":" + IntRaw(settings.InformationTombstoneTextMaxCharacters) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void AdjustInformationTombstoneTextLimit(LegacyUiCommand command, int direction)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationUiStateJson();
            var mode = NormalizeInformationSignTextLabelsMode(settings.InformationTombstoneTextLabelsMode);
            if (string.Equals(mode, InformationSignTextModes.Lines, StringComparison.OrdinalIgnoreCase))
            {
                var oldValue = InformationSignTextModes.ClampLines(settings.InformationTombstoneTextMaxLines);
                var newValue = InformationSignTextModes.AdjustLines(oldValue, direction);
                settings.InformationTombstoneTextMaxLines = newValue;
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Adjust.InformationTombstoneTextLimit",
                    "UI",
                    oldValue == newValue ? "NotApplicable" : "Succeeded",
                    "墓碑显示行数上限已设为前 " + newValue.ToString(CultureInfo.InvariantCulture) + " 行。",
                    before,
                    BuildInformationUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.tombstone_text_labels\",\"mode\":\"" + EscapeJson(mode) + "\",\"maxLines\":" + IntRaw(newValue) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (string.Equals(mode, InformationSignTextModes.Characters, StringComparison.OrdinalIgnoreCase))
            {
                var oldValue = InformationSignTextModes.ClampCharacters(settings.InformationTombstoneTextMaxCharacters);
                var newValue = InformationSignTextModes.AdjustCharacters(oldValue, direction);
                settings.InformationTombstoneTextMaxCharacters = newValue;
                ConfigService.SaveAll();
                Record(
                    command,
                    "Ui.Adjust.InformationTombstoneTextLimit",
                    "UI",
                    oldValue == newValue ? "NotApplicable" : "Succeeded",
                    "墓碑显示字数上限已设为前 " + newValue.ToString(CultureInfo.InvariantCulture) + " 字。",
                    before,
                    BuildInformationUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.tombstone_text_labels\",\"mode\":\"" + EscapeJson(mode) + "\",\"maxCharacters\":" + IntRaw(newValue) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            Record(
                command,
                "Ui.Adjust.InformationTombstoneTextLimit",
                "UI",
                "NotApplicable",
                "墓碑显示当前模式没有数量上限。",
                before,
                BuildInformationUiStateJson(),
                "{\"submitted\":false,\"implemented\":false,\"featureId\":\"information.tombstone_text_labels\",\"mode\":\"" + EscapeJson(mode) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void StartInformationPanelPosition(LegacyUiCommand command)
        {
            var before = BuildInformationUiStateJson();
            InformationStatusPanelService.RequestAdjustPosition();
            LegacyMainUiState.SetVisible(false);
            Record(
                command,
                "Ui.Action.InformationPanelPosition",
                "UI",
                "Succeeded",
                "信息窗位置调整已启动。",
                before,
                BuildInformationUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.info_panel_position\",\"adjustMode\":true,\"mainWindowHidden\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleInformationStyleConfig(LegacyUiCommand command, string featureId)
        {
            var before = BuildInformationStyleStateJson(featureId);
            if (!InformationStyleHelper.IsConfigurable(featureId))
            {
                Record(
                    command,
                    "Ui.InformationStyle.Toggle",
                    "UI",
                    "NotApplicable",
                    "未知的信息样式配置项。",
                    before,
                    BuildInformationStyleStateJson(featureId),
                    "{\"submitted\":false,\"implemented\":false,\"featureId\":\"" + EscapeJson(featureId) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            LegacyMainWindow.ToggleInformationStylePopup(featureId);
            Record(
                command,
                "Ui.InformationStyle.Toggle",
                "UI",
                "Succeeded",
                InformationStyleHelper.GetDisplayName(featureId) + "配置面板已切换。",
                before,
                BuildInformationStyleStateJson(featureId),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(featureId) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void FocusInformationStyleColorInput(LegacyUiCommand command, string featureId)
        {
            var before = BuildInformationStyleStateJson(featureId);
            if (InformationStyleHelper.IsConfigurable(featureId))
            {
                LegacyMainWindow.FocusInformationStyleColorInput(featureId);
            }

            Record(
                command,
                "Ui.InformationStyle.HtmlFocus",
                "UI",
                InformationStyleHelper.IsConfigurable(featureId) ? "Succeeded" : "NotApplicable",
                InformationStyleHelper.IsConfigurable(featureId) ? "HTML 颜色输入已聚焦。" : "未知的信息样式配置项。",
                before,
                BuildInformationStyleStateJson(featureId),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(InformationStyleHelper.IsConfigurable(featureId)) + ",\"featureId\":\"" + EscapeJson(featureId) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetInformationStyleHsl(LegacyUiCommand command, string featureId, string channel)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationStyleStateJson(featureId);
            if (!InformationStyleHelper.IsConfigurable(featureId))
            {
                LegacyUiInput.ClearPendingSlider(command.ElementId);
                return;
            }

            var color = InformationStyleHelper.GetColor(settings, featureId);
            int hue;
            int saturation;
            int lightness;
            InformationStyleHelper.ColorToHsl(color, out hue, out saturation, out lightness);
            if (string.Equals(channel, "h", StringComparison.Ordinal))
            {
                hue = LegacyMainUiState.Clamp(command.IntValue, 0, 360);
            }
            else if (string.Equals(channel, "s", StringComparison.Ordinal))
            {
                saturation = LegacyMainUiState.Clamp(command.IntValue, 0, 100);
            }
            else
            {
                lightness = LegacyMainUiState.Clamp(command.IntValue, 0, 100);
            }

            var nextColor = InformationStyleHelper.ColorFromHsl(hue, saturation, lightness);
            InformationStyleHelper.SetColorHex(settings, featureId, nextColor);
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.InformationStyle.ColorHsl",
                "UI",
                "Succeeded",
                InformationStyleHelper.GetDisplayName(featureId) + "颜色已更新。",
                before,
                BuildInformationStyleStateJson(featureId),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(featureId) + "\",\"channel\":\"" + EscapeJson(channel) + "\",\"hue\":" + IntRaw(hue) + ",\"saturation\":" + IntRaw(saturation) + ",\"lightness\":" + IntRaw(lightness) + ",\"color\":\"" + EscapeJson(nextColor) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "UI");
            LegacyUiInput.ClearPendingSlider(command.ElementId);
        }

        private static void ResetInformationStyle(LegacyUiCommand command, string featureId)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationStyleStateJson(featureId);
            if (!InformationStyleHelper.IsConfigurable(featureId))
            {
                Record(
                    command,
                    "Ui.InformationStyle.Reset",
                    "UI",
                    "NotApplicable",
                    "未知的信息样式配置项。",
                    before,
                    BuildInformationStyleStateJson(featureId),
                    "{\"submitted\":false,\"implemented\":false,\"featureId\":\"" + EscapeJson(featureId) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            InformationStyleHelper.ResetToDefault(settings, featureId);
            LegacyMainWindow.ClearInformationStyleColorInputFocus();
            LegacyUiInput.ClearPendingSlider("information-style-h:" + featureId);
            LegacyUiInput.ClearPendingSlider("information-style-s:" + featureId);
            LegacyUiInput.ClearPendingSlider("information-style-l:" + featureId);
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.InformationStyle.Reset",
                "UI",
                "Succeeded",
                InformationStyleHelper.GetDisplayName(featureId) + "样式已恢复默认。",
                before,
                BuildInformationStyleStateJson(featureId),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(featureId) + "\",\"color\":\"" + EscapeJson(InformationStyleHelper.GetColorHex(settings, featureId)) + "\",\"fontScale\":" + InformationStyleHelper.GetFontScale(settings, featureId).ToString("0.00", CultureInfo.InvariantCulture) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void AdjustInformationStyleFont(LegacyUiCommand command, string featureId, int direction)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildInformationStyleStateJson(featureId);
            if (!InformationStyleHelper.IsConfigurable(featureId))
            {
                return;
            }

            var nextScale = InformationStyleHelper.AdjustFontScale(settings, featureId, direction);
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.InformationStyle.FontScale",
                "UI",
                "Succeeded",
                InformationStyleHelper.GetDisplayName(featureId) + "字号已调整为 " + InformationStyleHelper.FormatFontScale(nextScale) + "。",
                before,
                BuildInformationStyleStateJson(featureId),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(featureId) + "\",\"fontScale\":" + nextScale.ToString("0.00", CultureInfo.InvariantCulture) + ",\"direction\":" + IntRaw(direction) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetThreshold(LegacyUiCommand command, bool heal)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildAutoRecoveryBeforeJson();
            var value = LegacyMainUiState.Clamp(command.IntValue, LegacySlider.MinPercent, LegacySlider.MaxPercent);
            if (heal)
            {
                settings.AutoHealThresholdPercent = value;
            }
            else
            {
                settings.AutoManaThresholdPercent = value;
            }

            ConfigService.SaveAll();
            Record(
                command,
                heal ? "Ui.Slider.AutoHealThreshold" : "Ui.Slider.AutoManaThreshold",
                "AutoRecovery",
                "Succeeded",
                (heal ? "AutoHeal" : "AutoMana") + " threshold set to " + value.ToString(CultureInfo.InvariantCulture) + "%.",
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"thresholdPercent\":" + value.ToString(CultureInfo.InvariantCulture) + ",\"mouseCaptured\":true}",
                "UI");
            LegacyUiInput.ClearPendingSlider(command.ElementId);
        }

        private static void SetAutoBuffCooldown(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildAutoRecoveryBeforeJson();
            var value = LegacyMainUiState.Clamp(command.IntValue, 300, 3600);
            settings.AutoBuffCooldownTicks = value;
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Slider.AutoBuffCooldown",
                "AutoRecovery",
                "Succeeded",
                "AutoBuff cooldown set to " + value.ToString(CultureInfo.InvariantCulture) + " ticks.",
                before,
                BuildAutoRecoveryAfterJson(),
                "{\"submitted\":false,\"cooldownTicks\":" + value.ToString(CultureInfo.InvariantCulture) + ",\"mouseCaptured\":true}",
                "UI");
            LegacyUiInput.ClearPendingSlider(command.ElementId);
        }
    }
}
