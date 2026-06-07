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
        private static void SetFishingAutoStoreMode(LegacyUiCommand command, string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildFishingUiStateJson();
            var normalized = FishingAutoStoreModes.Normalize(mode, false);
            var changed = !string.Equals(FishingAutoStoreModes.Normalize(settings.FishingAutoStoreMode, settings.FishingAutoStoreQuestFishEnabled), normalized, StringComparison.OrdinalIgnoreCase);
            settings.FishingAutoStoreMode = normalized;
            settings.FishingAutoStoreQuestFishEnabled = FishingAutoStoreModes.IsEnabled(normalized);
            ConfigService.SaveAll();

            var label = string.Equals(normalized, FishingAutoStoreModes.All, StringComparison.OrdinalIgnoreCase)
                ? "所有"
                : string.Equals(normalized, FishingAutoStoreModes.QuestFish, StringComparison.OrdinalIgnoreCase)
                    ? "任务鱼"
                    : "关闭";
            Record(
                command,
                "Ui.Toggle.FishingAutoStoreQuestFish",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                "自动存放鱼已切换为：" + label + "。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"fishing.auto_store_quest_fish\",\"mode\":\"" + EscapeJson(normalized) + "\",\"enabled\":" + BoolRaw(FishingAutoStoreModes.IsEnabled(normalized)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleFishingQuickRename(LegacyUiCommand command, InputActionQueue queue)
        {
            var before = BuildFishingUiStateJson();
            if (string.Equals(command.ElementId, "fishing-quick-rename:input", StringComparison.Ordinal))
            {
                if (!command.IsDoubleClick)
                {
                    Record(
                        command,
                        "Ui.FishingQuickRename.Input",
                        "UI",
                        "NotApplicable",
                        "Double-click the name field to edit.",
                        before,
                        BuildFishingUiStateJson(),
                        "{\"submitted\":false,\"doubleClick\":" + BoolRaw(command.IsDoubleClick) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                string currentName;
                string message;
                if (!PlayerRenameCompat.TryReadCurrentPlayerName(out currentName, out message))
                {
                    LegacyTextInput.ClearFocus();
                    Record(
                        command,
                        "Ui.FishingQuickRename.Input",
                        "UI",
                        "Failed",
                        message,
                        before,
                        BuildFishingUiStateJson(),
                        "{\"submitted\":false,\"doubleClick\":true,\"inputActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                LegacyTextInput.Focus("fishing-quick-rename:name", currentName);
                Record(
                    command,
                    "Ui.FishingQuickRename.Input",
                    "UI",
                    "Succeeded",
                    "快捷改名输入框已进入编辑状态。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"doubleClick\":true,\"inputActive\":true,\"currentName\":\"" + EscapeJson(currentName) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (!string.Equals(command.ElementId, "fishing-quick-rename:apply", StringComparison.Ordinal))
            {
                return;
            }

            var inputFocused = LegacyTextInput.IsFocused("fishing-quick-rename:name");
            string requestedName;
            string mode;
            if (inputFocused)
            {
                requestedName = LegacyTextInput.GetDraft("fishing-quick-rename:name");
                mode = "Manual";
            }
            else
            {
                string currentName;
                string readMessage;
                if (!PlayerRenameCompat.TryReadCurrentPlayerName(out currentName, out readMessage))
                {
                    Record(
                        command,
                        "Ui.FishingQuickRename.Submit",
                        "PlayerRename",
                        "Failed",
                        readMessage,
                        before,
                        BuildFishingUiStateJson(),
                        "{\"submitted\":false,\"mode\":\"QuickIncrement\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                        "Button");
                    return;
                }

                requestedName = PlayerRenameCompat.BuildIncrementedName(currentName);
                mode = "QuickIncrement";
            }

            var normalized = PlayerRenameCompat.NormalizePlayerName(requestedName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                Record(
                    command,
                    "Ui.FishingQuickRename.Submit",
                    "PlayerRename",
                    "NotApplicable",
                    "名字不能为空。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"mode\":\"" + EscapeJson(mode) + "\",\"requestedName\":\"" + EscapeJson(requestedName) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var request = new InputActionRequest
            {
                Kind = InputActionKind.PlayerRename,
                Priority = InputActionPriority.Normal,
                SourceFeatureId = FeatureIds.FishingQuickRename,
                Description = "Fishing quick rename",
                QueueTimeout = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.FishingQuickRename
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.FishingQuickRename;
            request.Metadata[ActionMetadataKeys.SourceKind] = "UI";
            request.Metadata["SourceUi"] = "LegacyMainWindow";
            request.Metadata["ButtonId"] = command.ElementId;
            request.Metadata["ButtonLabel"] = command.Label;
            request.Metadata["RequestedName"] = normalized;
            request.Metadata["Mode"] = mode;
            request.Metadata["AllowMultiplayer"] = "false";

            InputActionAdmissionResult admission = null;
            var accepted = queue != null && queue.TryEnqueue(request, out admission);
            if (accepted)
            {
                LegacyTextInput.ClearFocus();
            }

            Record(
                command,
                "Ui.FishingQuickRename.Submit",
                InputActionKind.PlayerRename.ToString(),
                accepted ? "Queued" : "Failed",
                accepted ? "快捷改名请求已提交。" : "快捷改名请求未提交：" + (admission == null ? "动作队列不可用" : admission.Reason),
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":" + BoolRaw(accepted) + ",\"mode\":\"" + EscapeJson(mode) + "\",\"requestedName\":\"" + EscapeJson(normalized) + "\",\"admission\":\"" + EscapeJson(admission == null ? "unavailable" : admission.Summary) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetFishingCutRodSkipEnabled(LegacyUiCommand command, bool enabled)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildFishingUiStateJson();
            var changed = settings.FishingFilterCutRodSkipEnabled != enabled;
            settings.FishingFilterCutRodSkipEnabled = enabled;
            settings.FishingFilterCutRodSkipDefaultMigrated = true;
            ConfigService.SaveAll();

            Record(
                command,
                "Ui.Toggle.FishingCutRodSkip",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                enabled ? "切杆跳过已开启。" : "切杆跳过已关闭。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"optionId\":\"fishing.cut_rod_skip\",\"enabled\":" + BoolRaw(enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetFishingFilterMode(LegacyUiCommand command, string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildFishingUiStateJson();
            var normalized = FishingFilterModes.Normalize(mode);
            var changed = !string.Equals(FishingFilterModes.Normalize(settings.FishingFilterMode), normalized, StringComparison.OrdinalIgnoreCase);
            settings.FishingFilterMode = normalized;
            FishingFilterUiState.EnsureModeSignature(settings);
            ConfigService.SaveAll();

            Record(
                command,
                "Ui.FishingFilter.Mode",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                "钓鱼过滤名单已切换为：" + FishingFilterModes.DisplayName(normalized) + "。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":false,\"featureId\":\"fishing.filter\",\"mode\":\"" + EscapeJson(normalized) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        // Follow 完全交给名单模式；Allow 默认保留该类；Deny 默认跳过该类。
        private static void SetFishingFilterSpecialRule(LegacyUiCommand command, string payload)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildFishingUiStateJson();
            var separator = string.IsNullOrWhiteSpace(payload) ? -1 : payload.LastIndexOf(':');
            if (separator <= 0 || separator >= payload.Length - 1)
            {
                Record(
                    command,
                    "Ui.FishingFilter.SpecialRule",
                    "UI",
                    "NotApplicable",
                    "Fishing filter special rule ignored because the payload was invalid.",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"uiOnly\":false,\"featureId\":\"fishing.filter\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var key = payload.Substring(0, separator);
            var normalized = FishingFilterSpecialRuleModes.Normalize(payload.Substring(separator + 1));
            var previous = GetFishingFilterSpecialRule(settings, key);
            if (previous == null)
            {
                Record(
                    command,
                    "Ui.FishingFilter.SpecialRule",
                    "UI",
                    "NotApplicable",
                    "Unknown fishing filter special rule: " + key + ".",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"uiOnly\":false,\"featureId\":\"fishing.filter\",\"ruleKey\":\"" + EscapeJson(key) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var changed = !string.Equals(FishingFilterSpecialRuleModes.Normalize(previous), normalized, StringComparison.OrdinalIgnoreCase);
            SetFishingFilterSpecialRuleValue(settings, key, normalized);
            ConfigService.SaveAll();

            Record(
                command,
                "Ui.FishingFilter.SpecialRule",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                GetFishingFilterSpecialRuleDisplayName(key) + "规则已切换为：" + GetFishingFilterSpecialRuleValueDisplayName(normalized) + "。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":false,\"featureId\":\"fishing.filter\",\"ruleKey\":\"" + EscapeJson(key) + "\",\"rule\":\"" + EscapeJson(normalized) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetFishingFilterMatchMode(LegacyUiCommand command, string matchMode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var before = BuildFishingUiStateJson();
            var normalized = FishingFilterMatchModes.Normalize(matchMode);
            var changed = !string.Equals(FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode), normalized, StringComparison.OrdinalIgnoreCase);
            settings.FishingFilterMatchMode = normalized;
            FishingFilterUiState.EnsureModeSignature(settings);
            ConfigService.SaveAll();

            Record(
                command,
                "Ui.FishingFilter.MatchMode",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                "钓鱼过滤名单页已切换为：" + FishingFilterMatchModes.EditorTitle(normalized) + "。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":false,\"featureId\":\"fishing.filter\",\"matchMode\":\"" + EscapeJson(normalized) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleFishingFilterExactPicker(LegacyUiCommand command, string payload)
        {
            if (string.Equals(payload, "open-current", StringComparison.Ordinal))
            {
                OpenFishingFilterExactPicker(command);
                return;
            }

            if (string.Equals(payload, "open-global", StringComparison.Ordinal))
            {
                StartFishingFilterGlobalSearch(command);
                return;
            }

            if (string.Equals(payload, "add-selected", StringComparison.Ordinal))
            {
                AddFishingFilterExactPickerSelection(command);
                return;
            }

            if (string.Equals(payload, "close", StringComparison.Ordinal))
            {
                CloseFishingFilterExactPicker(command);
                return;
            }

            if (payload != null && payload.StartsWith("toggle:", StringComparison.Ordinal))
            {
                ToggleFishingFilterExactPickerCandidate(command, payload.Substring("toggle:".Length));
                return;
            }

            var before = BuildFishingUiStateJson();
            Record(
                command,
                "Ui.FishingFilter.ExactPicker.Unknown",
                "UI",
                "NotApplicable",
                "Unknown fishing filter exact picker action.",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void StartFishingFilterGlobalSearch(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                FishingFilterUiState.ClosePicker(settings, "过滤未启用");
                Record(
                    command,
                    "Ui.FishingFilter.ExactPicker.GlobalSearch.Start",
                    "UI",
                    "NotApplicable",
                    "过滤未启用，请选择白名单或黑名单后编辑名单。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"globalSearchInputActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (!string.Equals(matchMode, FishingFilterMatchModes.Exact, StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                FishingFilterUiState.ClosePicker(settings, "当前不是精确匹配页");
                Record(
                    command,
                    "Ui.FishingFilter.ExactPicker.GlobalSearch.Start",
                    "UI",
                    "NotApplicable",
                    "当前不是精确匹配页。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"globalSearchInputActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            LegacyTextInput.Focus(FishingFilterUiState.GlobalSearchInputId, string.Empty);
            FishingFilterUiState.OpenGlobalSearchPicker(
                settings,
                new List<FishingCatchCandidate>(),
                string.Empty,
                "请输入名称或 ID 搜索全游戏可钓鱼获");
            Record(
                command,
                "Ui.FishingFilter.ExactPicker.GlobalSearch.Start",
                "UI",
                "Succeeded",
                "已开始全局搜索可钓鱼获。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"globalSearchInputActive\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void OpenFishingFilterExactPicker(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                FishingFilterUiState.ClosePicker(settings, "过滤未启用");
                Record(
                    command,
                    "Ui.FishingFilter.ExactPicker.OpenCurrent",
                    "UI",
                    "NotApplicable",
                    "过滤未启用，请选择白名单或黑名单后编辑名单。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"pickerOpen\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (!string.Equals(matchMode, FishingFilterMatchModes.Exact, StringComparison.OrdinalIgnoreCase))
            {
                FishingFilterUiState.ClosePicker(settings, "当前不是精确匹配页");
                Record(
                    command,
                    "Ui.FishingFilter.ExactPicker.OpenCurrent",
                    "UI",
                    "NotApplicable",
                    "当前不是精确匹配页。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"pickerOpen\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (FishingFilterUiState.PickerOpen &&
                string.Equals(FishingFilterUiState.PickerSource, FishingFilterUiState.PickerSourceCurrent, StringComparison.Ordinal))
            {
                CloseFishingFilterExactPicker(command);
                return;
            }

            string reason;
            LegacyTextInput.ClearFocus();
            var candidates = ResolveCurrentFishingFilterCandidates(out reason);
            FishingFilterUiState.OpenPicker(settings, candidates, reason);
            var count = candidates == null ? 0 : candidates.Count;
            Record(
                command,
                "Ui.FishingFilter.ExactPicker.OpenCurrent",
                "UI",
                count > 0 ? "Succeeded" : "NotApplicable",
                count > 0
                    ? "已获取当前水域鱼获候选：" + count.ToString(CultureInfo.InvariantCulture) + " 项。"
                    : "未获取鱼获列表：" + FirstNonEmpty(reason, "暂无可解析鱼获"),
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"pickerOpen\":true,\"candidateCount\":" + IntRaw(count) + ",\"reason\":\"" + EscapeJson(reason) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void CloseFishingFilterExactPicker(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            LegacyTextInput.ClearFocus();
            FishingFilterUiState.ClosePicker(settings, string.Empty);
            Record(
                command,
                "Ui.FishingFilter.ExactPicker.Close",
                "UI",
                "Succeeded",
                "Fishing filter candidate window closed.",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"pickerOpen\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleFishingFilterExactPickerCandidate(LegacyUiCommand command, string key)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            string kind;
            int id;
            var parsed = TryParseFishingFilterExactKey(key, out kind, out id);
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            if (!parsed ||
                string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase) ||
                !FishingFilterUiState.PickerOpen)
            {
                Record(
                    command,
                    "Ui.FishingFilter.ExactPicker.Toggle",
                    "UI",
                    "NotApplicable",
                    "候选选择已忽略。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"key\":\"" + EscapeJson(key) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var wasSelected = FishingFilterUiState.IsSelected(kind, id);
            var selected = FishingFilterUiState.ToggleSelection(kind, id);
            var changed = wasSelected || selected;
            Record(
                command,
                "Ui.FishingFilter.ExactPicker.Toggle",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                changed
                    ? (selected ? "已勾选候选鱼获。" : "已取消勾选候选鱼获。")
                    : "未找到可切换的候选鱼获。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"kind\":\"" + EscapeJson(kind) + "\",\"id\":" + IntRaw(id) + ",\"selected\":" + BoolRaw(selected) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void AddFishingFilterExactPickerSelection(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                FishingFilterUiState.ClosePicker(settings, string.Empty);
                Record(
                    command,
                    "Ui.FishingFilter.ExactPicker.AddSelected",
                    "UI",
                    "NotApplicable",
                    "过滤未启用，请选择白名单或黑名单后编辑名单。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"addedCount\":0,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var selected = FishingFilterUiState.GetSelectedCandidates();
            var activeList = ResolveActiveExactListForWrite(settings, filterMode);
            if (activeList == null || selected.Count <= 0)
            {
                LegacyTextInput.ClearFocus();
                FishingFilterUiState.ClosePicker(settings, string.Empty);
                Record(
                    command,
                    "Ui.FishingFilter.ExactPicker.AddSelected",
                    "UI",
                    "NotApplicable",
                    "没有勾选候选鱼获。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"selectedCount\":" + IntRaw(selected.Count) + ",\"addedCount\":0,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var added = 0;
            for (var index = 0; index < selected.Count; index++)
            {
                var candidate = selected[index];
                if (candidate == null || string.IsNullOrWhiteSpace(FishingFilterUiState.NormalizeKind(candidate.Kind)) || candidate.Id <= 0)
                {
                    continue;
                }

                if (ContainsExact(activeList, candidate.Kind, candidate.Id))
                {
                    continue;
                }

                activeList.Add(new FishingFilterExactEntry
                {
                    Kind = FishingFilterUiState.NormalizeKind(candidate.Kind),
                    Id = candidate.Id,
                    DisplayNameSnapshot = FirstNonEmpty(candidate.DisplayName, candidate.DisplayNameSnapshot)
                });
                added++;
            }

            if (added > 0)
            {
                ConfigService.SaveAll();
            }

            FishingFilterUiState.ClearSelection();
            LegacyTextInput.ClearFocus();
            FishingFilterUiState.ClosePicker(settings, string.Empty);
            Record(
                command,
                "Ui.FishingFilter.ExactPicker.AddSelected",
                "UI",
                added > 0 ? "Succeeded" : "NotApplicable",
                added > 0
                    ? "已加入当前" + FishingFilterModes.DisplayName(filterMode) + "精确名单：" + added.ToString(CultureInfo.InvariantCulture) + " 项。"
                    : "勾选项已在当前名单中，无需重复添加。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"selectedCount\":" + IntRaw(selected.Count) + ",\"addedCount\":" + IntRaw(added) + ",\"activeExactCount\":" + IntRaw(activeList.Count) + ",\"saved\":" + BoolRaw(added > 0) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ClearFishingFilterActiveList(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            var removed = 0;
            var resultCode = "NotApplicable";
            var message = "过滤未启用，请选择白名单或黑名单后清空名单。";

            if (!string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(matchMode, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase))
                {
                    var activeKeywords = ResolveActiveKeywordListForWrite(settings, filterMode);
                    removed = activeKeywords == null ? 0 : activeKeywords.Count;
                    if (activeKeywords != null)
                    {
                        activeKeywords.Clear();
                    }
                }
                else
                {
                    var activeExact = ResolveActiveExactListForWrite(settings, filterMode);
                    removed = activeExact == null ? 0 : activeExact.Count;
                    if (activeExact != null)
                    {
                        activeExact.Clear();
                    }
                }

                if (removed > 0)
                {
                    ConfigService.SaveAll();
                    resultCode = "Succeeded";
                    message = "已清空当前" + FishingFilterModes.DisplayName(filterMode) + FishingFilterMatchModes.EditorTitle(matchMode) + "名单。";
                }
                else
                {
                    message = "当前名单已是空的。";
                }
            }

            LegacyTextInput.ClearFocus();
            FishingFilterUiState.ClosePicker(settings, string.Empty);
            FishingFilterUiState.ClosePresetList(settings);
            Record(
                command,
                "Ui.FishingFilter.List.Clear",
                "UI",
                resultCode,
                message,
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"removedCount\":" + IntRaw(removed) + ",\"saved\":" + BoolRaw(removed > 0) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void DeleteFishingFilterExactEntry(LegacyUiCommand command, string key)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            string kind;
            int id;
            var parsed = TryParseFishingFilterExactKey(key, out kind, out id);
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var activeList = ResolveActiveExactListForWrite(settings, filterMode);
            if (!parsed || activeList == null)
            {
                Record(
                    command,
                    "Ui.FishingFilter.ExactEntry.Delete",
                    "UI",
                    "NotApplicable",
                    "没有可删除的当前精确名单条目。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"key\":\"" + EscapeJson(key) + "\",\"removedCount\":0,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var removed = 0;
            for (var index = activeList.Count - 1; index >= 0; index--)
            {
                var entry = activeList[index];
                if (entry != null &&
                    string.Equals(FishingFilterUiState.NormalizeKind(entry.Kind), kind, StringComparison.OrdinalIgnoreCase) &&
                    entry.Id == id)
                {
                    activeList.RemoveAt(index);
                    removed++;
                }
            }

            if (removed > 0)
            {
                ConfigService.SaveAll();
            }

            Record(
                command,
                "Ui.FishingFilter.ExactEntry.Delete",
                "UI",
                removed > 0 ? "Succeeded" : "NotApplicable",
                removed > 0 ? "已删除当前精确名单条目。" : "当前精确名单中没有该条目。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"kind\":\"" + EscapeJson(kind) + "\",\"id\":" + IntRaw(id) + ",\"removedCount\":" + IntRaw(removed) + ",\"saved\":" + BoolRaw(removed > 0) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleFishingFilterKeyword(LegacyUiCommand command, string payload)
        {
            if (string.Equals(payload, "add-start", StringComparison.Ordinal))
            {
                StartFishingFilterKeywordInput(command);
                return;
            }

            if (string.Equals(payload, "confirm", StringComparison.Ordinal))
            {
                ConfirmFishingFilterKeywordInput(command);
                return;
            }

            if (string.Equals(payload, "cancel", StringComparison.Ordinal))
            {
                CancelFishingFilterKeywordInput(command);
                return;
            }

            if (payload != null && payload.StartsWith("delete:", StringComparison.Ordinal))
            {
                DeleteFishingFilterKeyword(command, payload.Substring("delete:".Length));
                return;
            }

            var before = BuildFishingUiStateJson();
            Record(
                command,
                "Ui.FishingFilter.Keyword.Unknown",
                "UI",
                "NotApplicable",
                "Unknown fishing filter keyword action.",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void StartFishingFilterKeywordInput(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                Record(
                    command,
                    "Ui.FishingFilter.Keyword.StartInput",
                    "UI",
                    "NotApplicable",
                    "过滤未启用，请选择白名单或黑名单后编辑关键词。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"keywordInputActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (!string.Equals(matchMode, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                Record(
                    command,
                    "Ui.FishingFilter.Keyword.StartInput",
                    "UI",
                    "NotApplicable",
                    "当前不是关键词页。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"keywordInputActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            LegacyTextInput.Focus(FishingFilterUiState.KeywordInputId, string.Empty);
            Record(
                command,
                "Ui.FishingFilter.Keyword.StartInput",
                "UI",
                "Succeeded",
                "已开始输入关键词。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"keywordInputActive\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ConfirmFishingFilterKeywordInput(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            var draft = LegacyTextInput.GetDraft(FishingFilterUiState.KeywordInputId);
            var keyword = string.IsNullOrWhiteSpace(draft) ? string.Empty : draft.Trim();
            var activeList = ResolveActiveKeywordListForWrite(settings, filterMode);
            var added = false;
            var resultCode = "NotApplicable";
            var message = "空关键词已忽略。";

            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                message = "过滤未启用，请选择白名单或黑名单后编辑关键词。";
            }
            else if (!string.Equals(matchMode, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                message = "当前不是关键词页。";
            }
            else if (activeList == null)
            {
                message = "没有可写入的当前关键词名单。";
            }
            else if (keyword.Length > 0)
            {
                if (ContainsKeyword(activeList, keyword))
                {
                    message = "当前关键词已存在，无需重复添加。";
                }
                else
                {
                    activeList.Add(keyword);
                    ConfigService.SaveAll();
                    added = true;
                    resultCode = "Succeeded";
                    message = "已加入当前" + FishingFilterModes.DisplayName(filterMode) + "关键词：" + keyword + "。";
                }
            }

            LegacyTextInput.ClearFocus();
            Record(
                command,
                "Ui.FishingFilter.Keyword.Confirm",
                "UI",
                resultCode,
                message,
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"keyword\":\"" + EscapeJson(keyword) + "\",\"added\":" + BoolRaw(added) + ",\"activeKeywordCount\":" + IntRaw(activeList == null ? 0 : activeList.Count) + ",\"saved\":" + BoolRaw(added) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void CancelFishingFilterKeywordInput(LegacyUiCommand command)
        {
            var before = BuildFishingUiStateJson();
            LegacyTextInput.ClearFocus();
            Record(
                command,
                "Ui.FishingFilter.Keyword.Cancel",
                "UI",
                "Succeeded",
                "已取消关键词输入。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"keywordInputActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void DeleteFishingFilterKeyword(LegacyUiCommand command, string indexText)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            int index;
            var parsed = int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
            var activeList = ResolveActiveKeywordListForWrite(settings, filterMode);
            var removed = string.Empty;
            if (parsed && activeList != null && index >= 0 && index < activeList.Count)
            {
                removed = activeList[index] ?? string.Empty;
                activeList.RemoveAt(index);
                ConfigService.SaveAll();
            }

            Record(
                command,
                "Ui.FishingFilter.Keyword.Delete",
                "UI",
                string.IsNullOrEmpty(removed) ? "NotApplicable" : "Succeeded",
                string.IsNullOrEmpty(removed) ? "没有可删除的当前关键词条目。" : "已删除关键词：" + removed + "。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"index\":" + IntRaw(parsed ? index : -1) + ",\"keyword\":\"" + EscapeJson(removed) + "\",\"removedCount\":" + IntRaw(string.IsNullOrEmpty(removed) ? 0 : 1) + ",\"saved\":" + BoolRaw(!string.IsNullOrEmpty(removed)) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleFishingFilterPreset(LegacyUiCommand command, string payload)
        {
            if (string.Equals(payload, "save-start", StringComparison.Ordinal))
            {
                SaveFishingFilterPresetDirect(command);
                return;
            }

            if (string.Equals(payload, "save-confirm", StringComparison.Ordinal))
            {
                ConfirmFishingFilterPresetSave(command);
                return;
            }

            if (string.Equals(payload, "save-cancel", StringComparison.Ordinal))
            {
                CancelFishingFilterPresetSave(command);
                return;
            }

            if (string.Equals(payload, "list-toggle", StringComparison.Ordinal))
            {
                ToggleFishingFilterPresetList(command);
                return;
            }

            if (payload != null && payload.StartsWith("apply:", StringComparison.Ordinal))
            {
                ApplyFishingFilterPreset(command, payload.Substring("apply:".Length));
                return;
            }

            if (payload != null && payload.StartsWith("delete:", StringComparison.Ordinal))
            {
                DeleteFishingFilterPreset(command, payload.Substring("delete:".Length));
                return;
            }

            var before = BuildFishingUiStateJson();
            Record(
                command,
                "Ui.FishingFilter.Preset.Unknown",
                "UI",
                "NotApplicable",
                "Unknown fishing filter preset action.",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SaveFishingFilterPresetDirect(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            var resultCode = "NotApplicable";
            var message = "过滤未启用，请选择白名单或黑名单后保存预设。";
            var saved = false;
            var replaced = false;
            var itemCount = 0;
            var name = string.Empty;

            LegacyTextInput.ClearFocus();
            if (!string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                if (settings.FishingFilterPresets == null)
                {
                    settings.FishingFilterPresets = new List<FishingFilterPreset>();
                }

                name = BuildFishingFilterPresetAutoName(settings, filterMode, matchMode);
                var preset = BuildFishingFilterPresetFromCurrentList(settings, filterMode, matchMode, name);
                itemCount = string.Equals(matchMode, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase)
                    ? (preset.Keywords == null ? 0 : preset.Keywords.Count)
                    : (preset.ExactEntries == null ? 0 : preset.ExactEntries.Count);
                replaced = RemoveFishingFilterPresetByName(settings.FishingFilterPresets, filterMode, matchMode, name) > 0;
                settings.FishingFilterPresets.Add(preset);
                ConfigService.SaveAll();
                saved = true;
                resultCode = "Succeeded";
                message = replaced ? "已更新当前名单预设。" : "已保存当前名单预设。";
                FishingFilterUiState.ShowPresetSaveNotice(settings, "已保存");
            }

            FishingFilterUiState.ClosePicker(settings, string.Empty);
            Record(
                command,
                "Ui.FishingFilter.Preset.SaveDirect",
                "UI",
                resultCode,
                message,
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"presetName\":\"" + EscapeJson(name) + "\",\"saved\":" + BoolRaw(saved) + ",\"replaced\":" + BoolRaw(replaced) + ",\"itemCount\":" + IntRaw(itemCount) + ",\"presetCount\":" + IntRaw(settings.FishingFilterPresets == null ? 0 : settings.FishingFilterPresets.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void StartFishingFilterPresetNameInput(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                FishingFilterUiState.ClosePresetList(settings);
                Record(
                    command,
                    "Ui.FishingFilter.Preset.SaveStart",
                    "UI",
                    "NotApplicable",
                    "过滤未启用，请选择白名单或黑名单后保存预设。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"presetNameInputActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var defaultName = BuildDefaultFishingFilterPresetName(settings, filterMode, matchMode);
            FishingFilterUiState.ClosePresetList(settings);
            FishingFilterUiState.ClosePicker(settings, string.Empty);
            LegacyTextInput.Focus(FishingFilterUiState.PresetNameInputId, defaultName);
            Record(
                command,
                "Ui.FishingFilter.Preset.SaveStart",
                "UI",
                "Succeeded",
                "请输入钓鱼过滤预设名称。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"defaultName\":\"" + EscapeJson(defaultName) + "\",\"presetNameInputActive\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ConfirmFishingFilterPresetSave(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            var draft = LegacyTextInput.GetDraft(FishingFilterUiState.PresetNameInputId);
            var name = string.IsNullOrWhiteSpace(draft) ? string.Empty : draft.Trim();
            var resultCode = "NotApplicable";
            var message = "预设名称不能为空。";
            var saved = false;
            var replaced = false;

            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                message = "过滤未启用，请选择白名单或黑名单后保存预设。";
            }
            else if (name.Length > 0)
            {
                if (settings.FishingFilterPresets == null)
                {
                    settings.FishingFilterPresets = new List<FishingFilterPreset>();
                }

                replaced = RemoveFishingFilterPresetByName(settings.FishingFilterPresets, filterMode, matchMode, name) > 0;
                settings.FishingFilterPresets.Add(BuildFishingFilterPresetFromCurrentList(settings, filterMode, matchMode, name));
                ConfigService.SaveAll();
                saved = true;
                resultCode = "Succeeded";
                message = replaced ? "已覆盖同名预设：" + name + "。" : "已保存预设：" + name + "。";
                FishingFilterUiState.ShowPresetSaveNotice(settings, "已保存");
            }

            LegacyTextInput.ClearFocus();
            Record(
                command,
                "Ui.FishingFilter.Preset.SaveConfirm",
                "UI",
                resultCode,
                message,
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"presetName\":\"" + EscapeJson(name) + "\",\"saved\":" + BoolRaw(saved) + ",\"replaced\":" + BoolRaw(replaced) + ",\"presetCount\":" + IntRaw(settings.FishingFilterPresets == null ? 0 : settings.FishingFilterPresets.Count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void CancelFishingFilterPresetSave(LegacyUiCommand command)
        {
            var before = BuildFishingUiStateJson();
            LegacyTextInput.ClearFocus();
            Record(
                command,
                "Ui.FishingFilter.Preset.SaveCancel",
                "UI",
                "Succeeded",
                "已取消保存预设。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"presetNameInputActive\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ToggleFishingFilterPresetList(LegacyUiCommand command)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                FishingFilterUiState.ClosePresetList(settings);
                Record(
                    command,
                    "Ui.FishingFilter.Preset.ListToggle",
                    "UI",
                    "NotApplicable",
                    "过滤未启用，请选择白名单或黑名单后查看预设。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"presetListOpen\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            FishingFilterUiState.TogglePresetList(settings);
            var count = CountVisibleFishingFilterPresets(settings, filterMode, matchMode);
            Record(
                command,
                "Ui.FishingFilter.Preset.ListToggle",
                "UI",
                "Succeeded",
                FishingFilterUiState.PresetListOpen ? "已展开当前模式预设列表。" : "已收起当前模式预设列表。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"presetListOpen\":" + BoolRaw(FishingFilterUiState.PresetListOpen) + ",\"visiblePresetCount\":" + IntRaw(count) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void ApplyFishingFilterPreset(LegacyUiCommand command, string key)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            FishingFilterPreset preset;
            int settingsIndex;
            bool builtIn;
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase) ||
                !TryResolveFishingFilterPreset(settings, filterMode, matchMode, key, out preset, out settingsIndex, out builtIn))
            {
                Record(
                    command,
                    "Ui.FishingFilter.Preset.Apply",
                    "UI",
                    "NotApplicable",
                    "没有可应用的当前模式预设。",
                    before,
                    BuildFishingUiStateJson(),
                    "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"key\":\"" + EscapeJson(key) + "\",\"applied\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var appliedCount = 0;
            if (string.Equals(matchMode, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                var activeKeywords = ResolveActiveKeywordListForWrite(settings, filterMode);
                if (activeKeywords != null)
                {
                    activeKeywords.Clear();
                    var keywords = CloneFishingFilterKeywords(preset.Keywords);
                    for (var index = 0; index < keywords.Count; index++)
                    {
                        activeKeywords.Add(keywords[index]);
                    }

                    appliedCount = activeKeywords.Count;
                }
            }
            else
            {
                var activeExact = ResolveActiveExactListForWrite(settings, filterMode);
                if (activeExact != null)
                {
                    activeExact.Clear();
                    var entries = CloneFishingFilterExactEntries(preset.ExactEntries);
                    for (var index = 0; index < entries.Count; index++)
                    {
                        activeExact.Add(entries[index]);
                    }

                    appliedCount = activeExact.Count;
                }
            }

            ConfigService.SaveAll();
            FishingFilterUiState.ClearSelection();
            FishingFilterUiState.ClosePresetList(settings);
            Record(
                command,
                "Ui.FishingFilter.Preset.Apply",
                "UI",
                "Succeeded",
                "已应用预设并覆盖当前名单：" + preset.Name + "。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"key\":\"" + EscapeJson(key) + "\",\"presetName\":\"" + EscapeJson(preset.Name) + "\",\"builtIn\":" + BoolRaw(builtIn) + ",\"settingsIndex\":" + IntRaw(settingsIndex) + ",\"appliedCount\":" + IntRaw(appliedCount) + ",\"saved\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void DeleteFishingFilterPreset(LegacyUiCommand command, string key)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            FishingFilterUiState.EnsureModeSignature(settings);
            var before = BuildFishingUiStateJson();
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            FishingFilterPreset preset;
            int settingsIndex;
            bool builtIn;
            var removed = false;
            var presetName = string.Empty;
            if (TryResolveFishingFilterPreset(settings, filterMode, matchMode, key, out preset, out settingsIndex, out builtIn) &&
                !builtIn &&
                settings.FishingFilterPresets != null &&
                settingsIndex >= 0 &&
                settingsIndex < settings.FishingFilterPresets.Count)
            {
                presetName = preset == null ? string.Empty : preset.Name ?? string.Empty;
                settings.FishingFilterPresets.RemoveAt(settingsIndex);
                ConfigService.SaveAll();
                removed = true;
            }

            Record(
                command,
                "Ui.FishingFilter.Preset.Delete",
                "UI",
                removed ? "Succeeded" : "NotApplicable",
                removed ? "已删除预设：" + presetName + "。" : "没有可删除的当前模式预设。",
                before,
                BuildFishingUiStateJson(),
                "{\"submitted\":false,\"featureId\":\"fishing.filter\",\"filterMode\":\"" + EscapeJson(filterMode) + "\",\"matchMode\":\"" + EscapeJson(matchMode) + "\",\"key\":\"" + EscapeJson(key) + "\",\"presetName\":\"" + EscapeJson(presetName) + "\",\"removed\":" + BoolRaw(removed) + ",\"saved\":" + BoolRaw(removed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static List<FishingCatchCandidate> ResolveCurrentFishingFilterCandidates(out string reason)
        {
            reason = string.Empty;
            var result = new List<FishingCatchCandidate>();

            List<FishingBobberObservation> scanned;
            if (!TerrariaFishingCompat.TryScanLocalBobbers(out scanned))
            {
                reason = "环境不可用";
                return result;
            }

            if (scanned == null || scanned.Count <= 0)
            {
                reason = "未找到本地鱼漂";
                return result;
            }

            InformationWorldContext context;
            string skipReason;
            if (!InformationWorldContextProvider.TryBuild(out context, out skipReason))
            {
                reason = NormalizeFishingPickerContextReason(skipReason);
                return result;
            }

            var bobber = scanned[0];
            if (!IsBobberInLiquid(context, bobber.CenterX, bobber.CenterY))
            {
                reason = "鱼漂不在水里";
                return result;
            }

            try
            {
                string message;
                // "Add current" reads current-water candidates only; it must
                // not pull rods, swap hotbar slots, or reuse auto-fish actions.
                var candidates = InformationFishingCatchResolver.ResolveCatchCandidates(context, bobber.CenterX, bobber.CenterY, out message);
                if (candidates != null)
                {
                    for (var index = 0; index < candidates.Count; index++)
                    {
                        var candidate = candidates[index];
                        if (candidate == null)
                        {
                            continue;
                        }

                        var kind = FishingFilterUiState.NormalizeKind(candidate.Kind);
                        if (string.IsNullOrWhiteSpace(kind) || candidate.Id <= 0)
                        {
                            continue;
                        }

                        result.Add(new FishingCatchCandidate
                        {
                            Kind = kind,
                            Id = candidate.Id,
                            DisplayName = candidate.DisplayName,
                            DisplayNameSnapshot = candidate.DisplayNameSnapshot,
                            IsCrate = candidate.IsCrate,
                            IsQuestFish = candidate.IsQuestFish,
                            IsEnemy = candidate.IsEnemy
                        });
                    }
                }

                InformationFishingEnemyCandidateResolver.AddFishableEnemyCandidates(result);
                if (result.Count <= 0)
                {
                    reason = FirstNonEmpty(message, "暂无可解析鱼获");
                }
            }
            catch (Exception error)
            {
                Logger.Debug("LegacyUiActionService", "Fishing filter picker candidate resolution failed: " + error);
                reason = "鱼获解析失败";
            }

            return result;
        }

        private static bool IsBobberInLiquid(InformationWorldContext context, float worldX, float worldY)
        {
            if (context == null || context.MainType == null)
            {
                return false;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                return false;
            }

            var tileX = (int)Math.Floor(worldX / 16f);
            var tileY = (int)Math.Floor(worldY / 16f);
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = 0; dy <= 1; dy++)
                {
                    var tile = InformationTileAccess.GetTileAt(tiles, tileX + dx, tileY + dy);
                    if (ReadLiquidAmount(tile) > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int ReadLiquidAmount(object tile)
        {
            return InformationTileAccess.ReadLiquidAmount(tile);
        }

        private static string NormalizeFishingPickerContextReason(string skipReason)
        {
            if (string.Equals(skipReason, "mainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return "当前不在世界内";
            }

            if (string.Equals(skipReason, "localPlayerUnavailable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skipReason, "localPlayerInactive", StringComparison.OrdinalIgnoreCase))
            {
                return "本地玩家不可用";
            }

            if (string.Equals(skipReason, "terrariaRuntimeTypesUnavailable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skipReason, "mainTypeUnavailable", StringComparison.OrdinalIgnoreCase))
            {
                return "环境不可用";
            }

            return string.IsNullOrWhiteSpace(skipReason) ? "环境不可用" : skipReason;
        }

        private static List<FishingFilterExactEntry> ResolveActiveExactListForWrite(AppSettings settings, string filterMode)
        {
            if (settings == null ||
                string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase))
            {
                if (settings.FishingFilterDenyExactEntries == null)
                {
                    settings.FishingFilterDenyExactEntries = new List<FishingFilterExactEntry>();
                }

                return settings.FishingFilterDenyExactEntries;
            }

            if (settings.FishingFilterAllowExactEntries == null)
            {
                settings.FishingFilterAllowExactEntries = new List<FishingFilterExactEntry>();
            }

            return settings.FishingFilterAllowExactEntries;
        }

        private static List<string> ResolveActiveKeywordListForWrite(AppSettings settings, string filterMode)
        {
            if (settings == null ||
                string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase))
            {
                if (settings.FishingFilterDenyKeywords == null)
                {
                    settings.FishingFilterDenyKeywords = new List<string>();
                }

                return settings.FishingFilterDenyKeywords;
            }

            if (settings.FishingFilterAllowKeywords == null)
            {
                settings.FishingFilterAllowKeywords = new List<string>();
            }

            return settings.FishingFilterAllowKeywords;
        }

        private static FishingFilterPreset BuildFishingFilterPresetFromCurrentList(AppSettings settings, string filterMode, string matchMode, string name)
        {
            var preset = new FishingFilterPreset
            {
                Name = name ?? string.Empty,
                FilterModeScope = FishingFilterModes.Normalize(filterMode),
                MatchModeScope = FishingFilterMatchModes.Normalize(matchMode),
                ExactEntries = new List<FishingFilterExactEntry>(),
                Keywords = new List<string>(),
                UpdatedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };

            if (string.Equals(preset.MatchModeScope, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                preset.Keywords = CloneFishingFilterKeywords(
                    string.Equals(preset.FilterModeScope, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                        ? settings.FishingFilterDenyKeywords
                        : settings.FishingFilterAllowKeywords);
            }
            else
            {
                preset.ExactEntries = CloneFishingFilterExactEntries(
                    string.Equals(preset.FilterModeScope, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                        ? settings.FishingFilterDenyExactEntries
                        : settings.FishingFilterAllowExactEntries);
            }

            return preset;
        }

        private static List<FishingFilterExactEntry> CloneFishingFilterExactEntries(IList<FishingFilterExactEntry> entries)
        {
            var result = new List<FishingFilterExactEntry>();
            if (entries == null)
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    continue;
                }

                var kind = FishingFilterUiState.NormalizeKind(entry.Kind);
                if (string.IsNullOrWhiteSpace(kind) || entry.Id <= 0)
                {
                    continue;
                }

                var key = FishingFilterUiState.BuildKey(kind, entry.Id);
                if (!seen.Add(key))
                {
                    continue;
                }

                result.Add(new FishingFilterExactEntry
                {
                    Kind = kind,
                    Id = entry.Id,
                    DisplayNameSnapshot = string.IsNullOrWhiteSpace(entry.DisplayNameSnapshot)
                        ? string.Empty
                        : entry.DisplayNameSnapshot.Trim()
                });
            }

            return result;
        }

        private static List<string> CloneFishingFilterKeywords(IList<string> keywords)
        {
            var result = new List<string>();
            if (keywords == null)
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < keywords.Count; index++)
            {
                var keyword = keywords[index];
                var text = string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim();
                if (text.Length <= 0 || !seen.Add(text))
                {
                    continue;
                }

                result.Add(text);
            }

            return result;
        }

        private static int RemoveFishingFilterPresetByName(List<FishingFilterPreset> presets, string filterMode, string matchMode, string name)
        {
            if (presets == null || string.IsNullOrWhiteSpace(name))
            {
                return 0;
            }

            var removed = 0;
            var normalizedFilter = FishingFilterModes.Normalize(filterMode);
            var normalizedMatch = FishingFilterMatchModes.Normalize(matchMode);
            for (var index = presets.Count - 1; index >= 0; index--)
            {
                var preset = presets[index];
                if (preset != null &&
                    string.Equals(FishingFilterModes.Normalize(preset.FilterModeScope), normalizedFilter, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(FishingFilterMatchModes.Normalize(preset.MatchModeScope), normalizedMatch, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(preset.Name == null ? string.Empty : preset.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    presets.RemoveAt(index);
                    removed++;
                }
            }

            return removed;
        }

        private static string BuildDefaultFishingFilterPresetName(AppSettings settings, string filterMode, string matchMode)
        {
            var prefix = string.Equals(FishingFilterMatchModes.Normalize(matchMode), FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase)
                ? "关键词预设 "
                : "精确预设 ";
            for (var index = 1; index < 1000; index++)
            {
                var name = prefix + index.ToString(CultureInfo.InvariantCulture);
                if (!FishingFilterPresetNameExists(settings, filterMode, matchMode, name))
                {
                    return name;
                }
            }

            return prefix + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
        }

        private static string BuildFishingFilterPresetAutoName(AppSettings settings, string filterMode, string matchMode)
        {
            var signature = BuildFishingFilterPresetContentSignature(settings, filterMode, matchMode);
            return "auto-" +
                   FishingFilterModes.Normalize(filterMode) + "-" +
                   FishingFilterMatchModes.Normalize(matchMode) + "-" +
                   BuildStableHexHash(signature);
        }

        private static string BuildFishingFilterPresetContentSignature(AppSettings settings, string filterMode, string matchMode)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var normalizedFilter = FishingFilterModes.Normalize(filterMode);
            var normalizedMatch = FishingFilterMatchModes.Normalize(matchMode);
            if (string.Equals(normalizedMatch, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                var keywords = CloneFishingFilterKeywords(
                    string.Equals(normalizedFilter, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                        ? settings.FishingFilterDenyKeywords
                        : settings.FishingFilterAllowKeywords);
                return "K:" + string.Join("|", keywords.ToArray());
            }

            var entries = CloneFishingFilterExactEntries(
                string.Equals(normalizedFilter, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                    ? settings.FishingFilterDenyExactEntries
                    : settings.FishingFilterAllowExactEntries);
            var parts = new List<string>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null)
                {
                    parts.Add(FishingFilterUiState.NormalizeKind(entry.Kind) + ":" + entry.Id.ToString(CultureInfo.InvariantCulture));
                }
            }

            return "E:" + string.Join("|", parts.ToArray());
        }

        private static string BuildStableHexHash(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                var value = text ?? string.Empty;
                for (var index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 1099511628211UL;
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        private static bool FishingFilterPresetNameExists(AppSettings settings, string filterMode, string matchMode, string name)
        {
            var presets = settings == null ? null : settings.FishingFilterPresets;
            if (presets == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var normalizedFilter = FishingFilterModes.Normalize(filterMode);
            var normalizedMatch = FishingFilterMatchModes.Normalize(matchMode);
            for (var index = 0; index < presets.Count; index++)
            {
                var preset = presets[index];
                if (preset != null &&
                    string.Equals(FishingFilterModes.Normalize(preset.FilterModeScope), normalizedFilter, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(FishingFilterMatchModes.Normalize(preset.MatchModeScope), normalizedMatch, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(preset.Name == null ? string.Empty : preset.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountVisibleFishingFilterPresets(AppSettings settings, string filterMode, string matchMode)
        {
            var count = 0;
            var normalizedFilter = FishingFilterModes.Normalize(filterMode);
            var normalizedMatch = FishingFilterMatchModes.Normalize(matchMode);
            if (FishingFilterDefaultPresets.IsLowFishingPowerJunkScope(normalizedFilter, normalizedMatch))
            {
                FishingFilterPreset defaultPreset;
                if (FishingFilterDefaultPresets.TryGetLowFishingPowerJunkPreset(out defaultPreset))
                {
                    count++;
                }
            }

            var presets = settings == null ? null : settings.FishingFilterPresets;
            if (presets == null)
            {
                return count;
            }

            for (var index = 0; index < presets.Count; index++)
            {
                var preset = presets[index];
                if (preset != null &&
                    !string.IsNullOrWhiteSpace(preset.Name) &&
                    string.Equals(FishingFilterModes.Normalize(preset.FilterModeScope), normalizedFilter, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(FishingFilterMatchModes.Normalize(preset.MatchModeScope), normalizedMatch, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryResolveFishingFilterPreset(AppSettings settings, string filterMode, string matchMode, string key, out FishingFilterPreset preset, out int settingsIndex, out bool builtIn)
        {
            preset = null;
            settingsIndex = -1;
            builtIn = false;
            var normalizedFilter = FishingFilterModes.Normalize(filterMode);
            var normalizedMatch = FishingFilterMatchModes.Normalize(matchMode);
            if (string.Equals(key, "default-low-junk", StringComparison.OrdinalIgnoreCase) &&
                FishingFilterDefaultPresets.IsLowFishingPowerJunkScope(normalizedFilter, normalizedMatch))
            {
                builtIn = true;
                return FishingFilterDefaultPresets.TryGetLowFishingPowerJunkPreset(out preset);
            }

            const string prefix = "saved-";
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int parsedIndex;
            if (!int.TryParse(key.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedIndex))
            {
                return false;
            }

            var presets = settings == null ? null : settings.FishingFilterPresets;
            if (presets == null || parsedIndex < 0 || parsedIndex >= presets.Count)
            {
                return false;
            }

            var candidate = presets[parsedIndex];
            if (candidate == null ||
                string.IsNullOrWhiteSpace(candidate.Name) ||
                !string.Equals(FishingFilterModes.Normalize(candidate.FilterModeScope), normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(FishingFilterMatchModes.Normalize(candidate.MatchModeScope), normalizedMatch, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            preset = candidate;
            settingsIndex = parsedIndex;
            return true;
        }

        private static bool ContainsExact(IList<FishingFilterExactEntry> entries, string kind, int id)
        {
            if (entries == null || id <= 0)
            {
                return false;
            }

            var normalizedKind = FishingFilterUiState.NormalizeKind(kind);
            if (string.IsNullOrWhiteSpace(normalizedKind))
            {
                return false;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null &&
                    entry.Id == id &&
                    string.Equals(FishingFilterUiState.NormalizeKind(entry.Kind), normalizedKind, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsKeyword(IList<string> keywords, string keyword)
        {
            if (keywords == null || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            var normalized = keyword.Trim();
            for (var index = 0; index < keywords.Count; index++)
            {
                if (string.Equals(keywords[index] == null ? string.Empty : keywords[index].Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseFishingFilterExactKey(string raw, out string kind, out int id)
        {
            kind = string.Empty;
            id = 0;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var separator = raw.LastIndexOf(':');
            if (separator <= 0 || separator >= raw.Length - 1)
            {
                return false;
            }

            kind = FishingFilterUiState.NormalizeKind(raw.Substring(0, separator));
            return !string.IsNullOrWhiteSpace(kind) &&
                   int.TryParse(raw.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out id) &&
                   id > 0;
        }

        private static string GetFishingFilterSpecialRule(AppSettings settings, string key)
        {
            if (settings == null)
            {
                return null;
            }

            if (string.Equals(key, "crate", StringComparison.Ordinal))
            {
                return settings.FishingFilterCrateRule;
            }

            if (string.Equals(key, "quest-fish", StringComparison.Ordinal))
            {
                return settings.FishingFilterQuestFishRule;
            }

            return string.Equals(key, "enemy", StringComparison.Ordinal) ? settings.FishingFilterEnemyRule : null;
        }

        private static void SetFishingFilterSpecialRuleValue(AppSettings settings, string key, string value)
        {
            if (string.Equals(key, "crate", StringComparison.Ordinal))
            {
                settings.FishingFilterCrateRule = value;
            }
            else if (string.Equals(key, "quest-fish", StringComparison.Ordinal))
            {
                settings.FishingFilterQuestFishRule = value;
            }
            else if (string.Equals(key, "enemy", StringComparison.Ordinal))
            {
                settings.FishingFilterEnemyRule = value;
            }
        }

        private static string GetFishingFilterSpecialRuleDisplayName(string key)
        {
            if (string.Equals(key, "crate", StringComparison.Ordinal))
            {
                return "匣子";
            }

            if (string.Equals(key, "quest-fish", StringComparison.Ordinal))
            {
                return "任务鱼";
            }

            return string.Equals(key, "enemy", StringComparison.Ordinal) ? "怪物" : key;
        }

        private static string GetFishingFilterSpecialRuleValueDisplayName(string value)
        {
            var normalized = FishingFilterSpecialRuleModes.Normalize(value);
            if (string.Equals(normalized, FishingFilterSpecialRuleModes.Allow, StringComparison.OrdinalIgnoreCase))
            {
                return "要";
            }

            return string.Equals(normalized, FishingFilterSpecialRuleModes.Deny, StringComparison.OrdinalIgnoreCase) ? "不要" : "跟随";
        }
    }
}
