using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void HandleBlueprintEntryHotkeyCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildBlueprintUiStateJson();
            Record(
                command,
                "Ui.Blueprint.EntryHotkey",
                "UI",
                "NotApplicable",
                "Blueprint direct entry hotkey capture is disabled.",
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"payload\":\"" + EscapeJson(payload) + "\",\"resultCode\":\"directEntryHotkeyDisabled\",\"captureActive\":false,\"mouseCaptured\":" + BoolRaw(command != null && command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleBlueprintActionHotkeyCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildBlueprintUiStateJson();
            var targetId = NormalizeBlueprintActionHotkeyTargetId(payload);
            if (targetId.Length <= 0)
            {
                Record(
                    command,
                    "Ui.Blueprint.ActionHotkey",
                    "UI",
                    "Rejected",
                    "Unknown blueprint action hotkey target.",
                    before,
                    BuildBlueprintUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command != null && command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (command == null || !command.IsDoubleClick)
            {
                Record(
                    command,
                    "Ui.Blueprint.ActionHotkey",
                    "UI",
                    "NotApplicable",
                    "Double-click the blueprint action hotkey field to capture a key.",
                    before,
                    BuildBlueprintUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"doubleClick\":false,\"captureActive\":false,\"hotkeyFeatureId\":\"" + EscapeJson(targetId) + "\",\"actionLabel\":\"" + EscapeJson(BuildBlueprintActionHotkeyLabel(targetId)) + "\",\"mouseCaptured\":" + BoolRaw(command != null && command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            LegacyMainWindow.StartBlueprintActionHotkeyCapture(targetId);
            Record(
                command,
                "Ui.Blueprint.ActionHotkey",
                "UI",
                "Succeeded",
                "Blueprint action hotkey capture is now active.",
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"doubleClick\":true,\"captureActive\":true,\"hotkeyFeatureId\":\"" + EscapeJson(targetId) + "\",\"actionLabel\":\"" + EscapeJson(BuildBlueprintActionHotkeyLabel(targetId)) + "\",\"mouseCaptured\":" + BoolRaw(command != null && command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleBlueprintActionEntryCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildBlueprintUiStateJson();
            var normalizedAction = NormalizeBlueprintActionEntryPayload(payload);
            if (normalizedAction.Length <= 0)
            {
                Record(
                    command,
                    "Ui.Blueprint.CreateSaveEntry",
                    "UI",
                    "Rejected",
                    "Unknown blueprint create/save entry command.",
                    before,
                    BuildBlueprintUiStateJson(),
                    "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"payload\":\"" + EscapeJson(payload) + "\",\"resultCode\":\"invalidAction\",\"mouseCaptured\":" + BoolRaw(command != null && command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var targetId = string.Equals(normalizedAction, BlueprintEntryCommands.StartCreate, StringComparison.Ordinal)
                ? FeatureIds.BlueprintCreateAction
                : FeatureIds.BlueprintSaveAction;
            var result = BlueprintEntryState.ApplyCommand(normalizedAction, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            BlueprintCaptureResult capture = null;
            if (result.Succeeded &&
                string.Equals(normalizedAction, BlueprintEntryCommands.FinishCreateSave, StringComparison.Ordinal))
            {
                capture = BlueprintCaptureService.CapturePendingMaskAndSave(false);
                if (capture.Succeeded)
                {
                    BlueprintLibraryUiState.NotifyTemplateCreated(capture.SavedTemplate);
                    result = BlueprintEntryState.MarkCaptureSaved(capture);
                }
                else
                {
                    result = BlueprintEntryState.RecordCaptureFailure(capture);
                }
            }

            var outcome = capture != null
                ? capture.Succeeded ? "Succeeded" : "Failed"
                : result.Succeeded ? (result.Changed ? "Succeeded" : "NotApplicable") : "NotApplicable";
            var resultCode = capture == null ? result.ResultCode : capture.ResultCode;
            var templateId = capture == null || capture.SavedTemplate == null ? string.Empty : capture.SavedTemplate.TemplateId;
            var templateName = capture == null || capture.SavedTemplate == null ? string.Empty : capture.SavedTemplate.Name;
            if (string.Equals(normalizedAction, BlueprintEntryCommands.StartCreate, StringComparison.Ordinal) &&
                result.Succeeded &&
                string.Equals(result.Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal))
            {
                LegacyMainUiState.SetVisible(false);
            }

            Record(
                command,
                "Ui.Blueprint.CreateSaveEntry",
                "UI",
                outcome,
                result.Message,
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(!result.PlaceholderOnly) + ",\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"hotkeyFeatureId\":\"" + EscapeJson(targetId) + "\",\"action\":\"" + EscapeJson(normalizedAction) + "\",\"actionLabel\":\"" + EscapeJson(BuildBlueprintActionHotkeyLabel(targetId)) + "\",\"resultCode\":\"" + EscapeJson(resultCode) + "\",\"mode\":\"" + EscapeJson(result.Mode) + "\",\"changed\":" + BoolRaw(result.Changed || (capture != null && capture.Succeeded)) + ",\"placeholderOnly\":" + BoolRaw(result.PlaceholderOnly) + ",\"captureAttempted\":" + BoolRaw(capture != null) + ",\"capturedCells\":" + IntRaw(capture == null ? 0 : capture.CapturedCellCount) + ",\"capturedLayers\":" + IntRaw(capture == null ? 0 : capture.CapturedLayerCount) + ",\"skippedAirCells\":" + IntRaw(capture == null ? 0 : capture.SkippedAirCellCount) + ",\"unavailableCells\":" + IntRaw(capture == null ? 0 : capture.UnavailableCellCount) + ",\"templateId\":\"" + EscapeJson(templateId) + "\",\"templateName\":\"" + EscapeJson(templateName) + "\",\"mouseCaptured\":" + BoolRaw(command != null && command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleBlueprintToolItemCommand(LegacyUiCommand command, string payload)
        {
            const int smallStep = 1;
            const int largeStep = 10;
            var before = BuildBlueprintUiStateJson();
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var oldValue = BlueprintSettings.NormalizeToolItemId(settings.BlueprintToolItemId);
            var next = oldValue;
            if (string.Equals(payload, "decrease-large", StringComparison.OrdinalIgnoreCase))
            {
                next = BlueprintSettings.AdjustToolItemId(oldValue, -largeStep);
            }
            else if (string.Equals(payload, "decrease", StringComparison.OrdinalIgnoreCase))
            {
                next = BlueprintSettings.AdjustToolItemId(oldValue, -smallStep);
            }
            else if (string.Equals(payload, "increase", StringComparison.OrdinalIgnoreCase))
            {
                next = BlueprintSettings.AdjustToolItemId(oldValue, smallStep);
            }
            else if (string.Equals(payload, "increase-large", StringComparison.OrdinalIgnoreCase))
            {
                next = BlueprintSettings.AdjustToolItemId(oldValue, largeStep);
            }
            else if (string.Equals(payload, "reset", StringComparison.OrdinalIgnoreCase))
            {
                next = BlueprintSettings.DefaultToolItemId;
            }
            else
            {
                Record(
                    command,
                    "Ui.Blueprint.ToolItem",
                    "UI",
                    "Rejected",
                    "Unknown blueprint tool item command.",
                    before,
                    BuildBlueprintUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"payload\":\"" + EscapeJson(payload) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            settings.BlueprintToolItemId = next;
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Blueprint.ToolItem",
                "UI",
                oldValue == next ? "NotApplicable" : "Succeeded",
                oldValue == next ? "Blueprint tool item unchanged." : "Blueprint tool item changed.",
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"oldItemId\":" + IntRaw(oldValue) + ",\"itemId\":" + IntRaw(next) + ",\"changed\":" + BoolRaw(oldValue != next) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleBlueprintAutoPlacementMode(LegacyUiCommand command, string payload)
        {
            SetBlueprintBooleanSetting(
                command,
                payload,
                "Ui.Blueprint.AutoPlacement",
                "autoPlacementEnabled",
                FeatureIds.BlueprintMain,
                settings => settings.BlueprintAutoPlacementEnabled,
                (settings, enabled) => settings.BlueprintAutoPlacementEnabled = enabled);
        }

        private static void HandleBlueprintHandheldEntryMode(LegacyUiCommand command, string payload)
        {
            SetBlueprintBooleanSetting(
                command,
                payload,
                "Ui.Blueprint.HandheldEntry",
                "handheldEntryEnabled",
                FeatureIds.BlueprintMain,
                settings => settings.BlueprintHandheldEntryEnabled,
                (settings, enabled) => settings.BlueprintHandheldEntryEnabled = enabled);
        }

        private static void HandleBlueprintHandheldActionBarCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildBlueprintUiStateJson();
            payload = (payload ?? string.Empty).Trim();
            if (string.Equals(payload, BlueprintHandheldActionBarState.ButtonIdCreate, StringComparison.OrdinalIgnoreCase))
            {
                var entry = BlueprintEntryState.ApplyCommand(
                    BlueprintEntryCommands.StartCreate,
                    ConfigService.AppSettings ?? AppSettings.CreateDefault());
                var commandResult = BlueprintHandheldActionBarState.RecordCommandResultClick(
                    payload,
                    command == null ? 0 : command.IntValue,
                    command != null && command.MouseCaptured,
                    entry.ResultCode,
                    entry.Message);
                Record(
                    command,
                    "Ui.Blueprint.HandheldActionBar",
                    "UI",
                    entry.Succeeded ? (entry.Changed ? "Succeeded" : "NotApplicable") : "Failed",
                    entry.Message,
                    before,
                    BuildBlueprintUiStateJson(),
                    "{\"submitted\":false,\"implemented\":" + BoolRaw(!entry.PlaceholderOnly) + ",\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"action\":\"" + EscapeJson(commandResult.ButtonId) + "\",\"buttonLabel\":\"" + EscapeJson(commandResult.ButtonLabel) + "\",\"resultCode\":\"" + EscapeJson(entry.ResultCode) + "\",\"mode\":\"" + EscapeJson(entry.Mode) + "\",\"changed\":" + BoolRaw(entry.Changed) + ",\"placeholderOnly\":" + BoolRaw(entry.PlaceholderOnly) + ",\"heldItemType\":" + IntRaw(commandResult.HeldItemType) + ",\"visibleReason\":\"" + EscapeJson(BlueprintHandheldActionBarState.HiddenReasonNone) + "\",\"blockedReason\":\"\",\"mouseCaptured\":" + BoolRaw(commandResult.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (string.Equals(payload, BlueprintHandheldActionBarState.ButtonIdSave, StringComparison.OrdinalIgnoreCase))
            {
                var entry = BlueprintEntryState.ApplyCommand(
                    BlueprintEntryCommands.FinishCreateSave,
                    ConfigService.AppSettings ?? AppSettings.CreateDefault());
                BlueprintCaptureResult capture = null;
                if (entry.Succeeded)
                {
                    capture = BlueprintCaptureService.CapturePendingMaskAndSave(false);
                    if (capture.Succeeded)
                    {
                        BlueprintLibraryUiState.NotifyTemplateCreated(capture.SavedTemplate);
                        entry = BlueprintEntryState.MarkCaptureSaved(capture);
                    }
                    else
                    {
                        entry = BlueprintEntryState.RecordCaptureFailure(capture);
                    }
                }

                var resultCode = capture == null ? entry.ResultCode : capture.ResultCode;
                var templateId = capture == null || capture.SavedTemplate == null ? string.Empty : capture.SavedTemplate.TemplateId;
                var templateName = capture == null || capture.SavedTemplate == null ? string.Empty : capture.SavedTemplate.Name;
                var commandResult = BlueprintHandheldActionBarState.RecordCommandResultClick(
                    payload,
                    command == null ? 0 : command.IntValue,
                    command != null && command.MouseCaptured,
                    resultCode,
                    entry.Message);
                Record(
                    command,
                    "Ui.Blueprint.HandheldActionBar",
                    "UI",
                    capture != null ? (capture.Succeeded ? "Succeeded" : "Failed") : entry.Succeeded ? (entry.Changed ? "Succeeded" : "NotApplicable") : "Failed",
                    entry.Message,
                    before,
                    BuildBlueprintUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"action\":\"" + EscapeJson(commandResult.ButtonId) + "\",\"entryAction\":\"" + EscapeJson(BlueprintEntryCommands.FinishCreateSave) + "\",\"buttonLabel\":\"" + EscapeJson(commandResult.ButtonLabel) + "\",\"resultCode\":\"" + EscapeJson(resultCode) + "\",\"mode\":\"" + EscapeJson(entry.Mode) + "\",\"changed\":" + BoolRaw(entry.Changed || (capture != null && capture.Succeeded)) + ",\"placeholderOnly\":" + BoolRaw(entry.PlaceholderOnly) + ",\"captureAttempted\":" + BoolRaw(capture != null) + ",\"capturedCells\":" + IntRaw(capture == null ? 0 : capture.CapturedCellCount) + ",\"capturedLayers\":" + IntRaw(capture == null ? 0 : capture.CapturedLayerCount) + ",\"skippedAirCells\":" + IntRaw(capture == null ? 0 : capture.SkippedAirCellCount) + ",\"unavailableCells\":" + IntRaw(capture == null ? 0 : capture.UnavailableCellCount) + ",\"templateId\":\"" + EscapeJson(templateId) + "\",\"templateName\":\"" + EscapeJson(templateName) + "\",\"heldItemType\":" + IntRaw(commandResult.HeldItemType) + ",\"visibleReason\":\"" + EscapeJson(BlueprintHandheldActionBarState.HiddenReasonNone) + "\",\"blockedReason\":\"\",\"mouseCaptured\":" + BoolRaw(commandResult.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (string.Equals(payload, BlueprintHandheldActionBarState.ButtonIdExitCreate, StringComparison.OrdinalIgnoreCase))
            {
                var entry = BlueprintEntryState.ApplyCommand(
                    BlueprintEntryCommands.ExitCreate,
                    ConfigService.AppSettings ?? AppSettings.CreateDefault());
                var commandResult = BlueprintHandheldActionBarState.RecordCommandResultClick(
                    payload,
                    command == null ? 0 : command.IntValue,
                    command != null && command.MouseCaptured,
                    entry.ResultCode,
                    entry.Message);
                Record(
                    command,
                    "Ui.Blueprint.HandheldActionBar",
                    "UI",
                    entry.Succeeded ? (entry.Changed ? "Succeeded" : "NotApplicable") : "Failed",
                    entry.Message,
                    before,
                    BuildBlueprintUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"action\":\"" + EscapeJson(commandResult.ButtonId) + "\",\"entryAction\":\"" + EscapeJson(BlueprintEntryCommands.ExitCreate) + "\",\"buttonLabel\":\"" + EscapeJson(commandResult.ButtonLabel) + "\",\"resultCode\":\"" + EscapeJson(entry.ResultCode) + "\",\"mode\":\"" + EscapeJson(entry.Mode) + "\",\"changed\":" + BoolRaw(entry.Changed) + ",\"placeholderOnly\":" + BoolRaw(entry.PlaceholderOnly) + ",\"heldItemType\":" + IntRaw(commandResult.HeldItemType) + ",\"visibleReason\":\"" + EscapeJson(BlueprintHandheldActionBarState.HiddenReasonNone) + "\",\"blockedReason\":\"\",\"mouseCaptured\":" + BoolRaw(commandResult.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (string.Equals(payload, BlueprintHandheldActionBarState.ButtonIdOpenLibrary, StringComparison.OrdinalIgnoreCase))
            {
                var library = BlueprintLibraryUiState.OpenLibrary();
                var entry = BlueprintEntryState.ApplyCommand(
                    BlueprintEntryCommands.OpenLibrary,
                    ConfigService.AppSettings ?? AppSettings.CreateDefault());
                var resultCode = library == null || string.IsNullOrWhiteSpace(library.ResultCode)
                    ? entry.ResultCode
                    : library.ResultCode;
                var commandResult = BlueprintHandheldActionBarState.RecordCommandResultClick(
                    payload,
                    command == null ? 0 : command.IntValue,
                    command != null && command.MouseCaptured,
                    entry.ResultCode,
                    library != null && !string.IsNullOrWhiteSpace(library.Message) ? library.Message : entry.Message);
                Record(
                    command,
                    "Ui.Blueprint.HandheldActionBar",
                    "UI",
                    library != null && !library.Succeeded ? "Failed" : entry.Succeeded ? (entry.Changed || (library != null && library.Changed) ? "Succeeded" : "NotApplicable") : "Failed",
                    library != null && !library.Succeeded ? library.Message : entry.Message,
                    before,
                    BuildBlueprintUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"action\":\"" + EscapeJson(commandResult.ButtonId) + "\",\"entryAction\":\"" + EscapeJson(BlueprintEntryCommands.OpenLibrary) + "\",\"buttonLabel\":\"" + EscapeJson(commandResult.ButtonLabel) + "\",\"resultCode\":\"" + EscapeJson(resultCode) + "\",\"entryResultCode\":\"" + EscapeJson(entry.ResultCode) + "\",\"mode\":\"" + EscapeJson(entry.Mode) + "\",\"changed\":" + BoolRaw(entry.Changed || (library != null && library.Changed)) + ",\"placeholderOnly\":" + BoolRaw(entry.PlaceholderOnly) + ",\"heldItemType\":" + IntRaw(commandResult.HeldItemType) + ",\"visibleReason\":\"" + EscapeJson(BlueprintHandheldActionBarState.HiddenReasonNone) + "\",\"blockedReason\":\"\",\"mouseCaptured\":" + BoolRaw(commandResult.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var result = BlueprintHandheldActionBarState.RecordPlaceholderClick(
                payload,
                command == null ? 0 : command.IntValue,
                command != null && command.MouseCaptured);
            Record(
                command,
                "Ui.Blueprint.HandheldActionBar",
                "UI",
                "NotApplicable",
                result.Message,
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"action\":\"" + EscapeJson(result.ButtonId) + "\",\"buttonLabel\":\"" + EscapeJson(result.ButtonLabel) + "\",\"resultCode\":\"" + EscapeJson(result.ResultCode) + "\",\"heldItemType\":" + IntRaw(result.HeldItemType) + ",\"visibleReason\":\"" + EscapeJson(BlueprintHandheldActionBarState.HiddenReasonNone) + "\",\"blockedReason\":\"\",\"mouseCaptured\":" + BoolRaw(result.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleBlueprintReplacementMode(LegacyUiCommand command, string payload)
        {
            if (string.Equals(payload, "Config", StringComparison.OrdinalIgnoreCase))
            {
                var before = BuildBlueprintUiStateJson();
                LegacyMainWindow.ToggleBlueprintReplacementConfigPopup();
                Record(
                    command,
                    "Ui.Blueprint.ReplacementConfig",
                    "UI",
                    "Succeeded",
                    "Blueprint same-kind replacement config toggled.",
                    before,
                    BuildBlueprintUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            SetBlueprintBooleanSetting(
                command,
                payload,
                "Ui.Blueprint.Replacement",
                "replacementEnabled",
                FeatureIds.BlueprintMain,
                settings => settings.BlueprintReplacementEnabled,
                (settings, enabled) => settings.BlueprintReplacementEnabled = enabled);
        }

        private static void HandleBlueprintReplacementCategoryMode(LegacyUiCommand command, string payload)
        {
            string category;
            string mode;
            SplitBlueprintReplacementCategoryPayload(payload, out category, out mode);
            if (string.Equals(category, BlueprintReplacementCategories.Torch, StringComparison.Ordinal))
            {
                SetBlueprintReplacementCategorySetting(command, category, mode, "火把", "replacementTorchEnabled", settings => settings.BlueprintReplacementTorchesEnabled, (settings, enabled) => settings.BlueprintReplacementTorchesEnabled = enabled);
                return;
            }

            if (string.Equals(category, BlueprintReplacementCategories.Platform, StringComparison.Ordinal))
            {
                SetBlueprintReplacementCategorySetting(command, category, mode, "平台", "replacementPlatformEnabled", settings => settings.BlueprintReplacementPlatformsEnabled, (settings, enabled) => settings.BlueprintReplacementPlatformsEnabled = enabled);
                return;
            }

            if (string.Equals(category, BlueprintReplacementCategories.WorkBench, StringComparison.Ordinal))
            {
                SetBlueprintReplacementCategorySetting(command, category, mode, "工作台", "replacementWorkBenchEnabled", settings => settings.BlueprintReplacementWorkBenchesEnabled, (settings, enabled) => settings.BlueprintReplacementWorkBenchesEnabled = enabled);
                return;
            }

            if (string.Equals(category, BlueprintReplacementCategories.Chair, StringComparison.Ordinal))
            {
                SetBlueprintReplacementCategorySetting(command, category, mode, "椅子", "replacementChairEnabled", settings => settings.BlueprintReplacementChairsEnabled, (settings, enabled) => settings.BlueprintReplacementChairsEnabled = enabled);
                return;
            }

            if (string.Equals(category, BlueprintReplacementCategories.Door, StringComparison.Ordinal))
            {
                SetBlueprintReplacementCategorySetting(command, category, mode, "门", "replacementDoorEnabled", settings => settings.BlueprintReplacementDoorsEnabled, (settings, enabled) => settings.BlueprintReplacementDoorsEnabled = enabled);
                return;
            }

            if (string.Equals(category, BlueprintReplacementCategories.Table, StringComparison.Ordinal))
            {
                SetBlueprintReplacementCategorySetting(command, category, mode, "桌子", "replacementTableEnabled", settings => settings.BlueprintReplacementTablesEnabled, (settings, enabled) => settings.BlueprintReplacementTablesEnabled = enabled);
                return;
            }

            if (string.Equals(category, BlueprintReplacementCategories.Chest, StringComparison.Ordinal))
            {
                SetBlueprintReplacementCategorySetting(command, category, mode, "箱子", "replacementChestEnabled", settings => settings.BlueprintReplacementChestsEnabled, (settings, enabled) => settings.BlueprintReplacementChestsEnabled = enabled);
                return;
            }

            if (string.Equals(category, BlueprintReplacementCategories.Sign, StringComparison.Ordinal))
            {
                SetBlueprintReplacementCategorySetting(command, category, mode, "牌子", "replacementSignEnabled", settings => settings.BlueprintReplacementSignsEnabled, (settings, enabled) => settings.BlueprintReplacementSignsEnabled = enabled);
                return;
            }

            var before = BuildBlueprintUiStateJson();
            Record(
                command,
                "Ui.Blueprint.ReplacementCategory",
                "UI",
                "Rejected",
                "Unknown blueprint replacement category.",
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"category\":\"" + EscapeJson(category) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void SetBlueprintReplacementCategorySetting(
            LegacyUiCommand command,
            string category,
            string mode,
            string categoryLabel,
            string fieldName,
            Func<AppSettings, bool> getValue,
            Action<AppSettings, bool> setValue)
        {
            var before = BuildBlueprintUiStateJson();
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var old = getValue(settings);
            var toggle = string.IsNullOrWhiteSpace(mode);
            var enabled = toggle ? !old : IsOnMode(mode);
            setValue(settings, enabled);
            ConfigService.SaveAll();
            Record(
                command,
                "Ui.Blueprint.ReplacementCategory",
                "UI",
                old == enabled ? "NotApplicable" : "Succeeded",
                old == enabled
                    ? "Blueprint same-kind replacement category unchanged."
                    : "Blueprint same-kind replacement category " + categoryLabel + (enabled ? " enabled." : " disabled."),
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"category\":\"" + EscapeJson(category) + "\",\"categoryLabel\":\"" + EscapeJson(categoryLabel) + "\",\"" + EscapeJson(fieldName) + "\":" + BoolRaw(enabled) + ",\"toggle\":" + BoolRaw(toggle) + ",\"changed\":" + BoolRaw(old != enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleBlueprintEntryCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildBlueprintUiStateJson();
            BlueprintLibraryCommandResult library = null;
            BlueprintPlacedInstanceCommandResult placed = null;
            if (string.Equals(payload, BlueprintEntryCommands.OpenLibrary, StringComparison.OrdinalIgnoreCase))
            {
                library = BlueprintLibraryUiState.OpenLibrary();
            }
            else if (string.Equals(payload, BlueprintEntryCommands.OpenPlacedInstances, StringComparison.OrdinalIgnoreCase))
            {
                placed = BlueprintPlacedInstanceUiState.OpenManagement();
            }
            else if (string.Equals(payload, BlueprintEntryCommands.OpenMaterials, StringComparison.OrdinalIgnoreCase))
            {
                BlueprintMaterialWindowState.Show();
                BlueprintMaterialService.ForceRefreshForMaterialWindow();
            }

            BlueprintEraseCommandResult eraseStart = null;
            BlueprintEntryCommandResult result;
            if (string.Equals(payload, BlueprintEntryCommands.StartErase, StringComparison.OrdinalIgnoreCase))
            {
                var placedSnapshot = BlueprintPlacedInstanceUiState.GetSnapshot();
                eraseStart = BlueprintEraseRegionState.BeginErase(placedSnapshot == null ? string.Empty : placedSnapshot.SelectedInstanceId);
                result = BlueprintEntryState.MarkEraseStarted(eraseStart);
            }
            else
            {
                result = BlueprintEntryState.ApplyCommand(payload, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            }

            BlueprintCaptureResult capture = null;
            if (result.Succeeded &&
                (string.Equals(payload, BlueprintEntryCommands.FinishCreateSave, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(payload, BlueprintEntryCommands.FinishCreateUse, StringComparison.OrdinalIgnoreCase)))
            {
                capture = BlueprintCaptureService.CapturePendingMaskAndSave(
                    string.Equals(payload, BlueprintEntryCommands.FinishCreateUse, StringComparison.OrdinalIgnoreCase));
                if (capture.Succeeded)
                {
                    BlueprintLibraryUiState.NotifyTemplateCreated(capture.SavedTemplate);
                    result = BlueprintEntryState.MarkCaptureSaved(capture);
                }
                else
                {
                    result = BlueprintEntryState.RecordCaptureFailure(capture);
                }
            }

            var outcome = placed != null && !placed.Succeeded
                ? "Failed"
                : library != null && !library.Succeeded
                ? "Failed"
                : eraseStart != null && !eraseStart.Succeeded
                ? "Failed"
                : capture != null
                    ? (capture.Succeeded ? "Succeeded" : "Failed")
                    : result.Succeeded ? (result.Changed ? "Succeeded" : "NotApplicable") : "NotApplicable";
            var resultCode = capture == null
                ? (placed != null ? placed.ResultCode : library == null ? result.ResultCode : library.ResultCode)
                : capture.ResultCode;
            var templateId = capture == null || capture.SavedTemplate == null ? string.Empty : capture.SavedTemplate.TemplateId;
            var templateName = capture == null || capture.SavedTemplate == null ? string.Empty : capture.SavedTemplate.Name;
            Record(
                command,
                "Ui.Blueprint.Entry",
                "UI",
                outcome,
                placed != null && !placed.Succeeded ? placed.Message : library != null && !library.Succeeded ? library.Message : result.Message,
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(!result.PlaceholderOnly) + ",\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"action\":\"" + EscapeJson(payload) + "\",\"resultCode\":\"" + EscapeJson(resultCode) + "\",\"mode\":\"" + EscapeJson(result.Mode) + "\",\"changed\":" + BoolRaw(result.Changed || (library != null && library.Changed) || (placed != null && placed.Changed) || (eraseStart != null && eraseStart.Changed) || (capture != null && capture.Succeeded)) + ",\"placeholderOnly\":" + BoolRaw(result.PlaceholderOnly) + ",\"captureAttempted\":" + BoolRaw(capture != null) + ",\"capturedCells\":" + IntRaw(capture == null ? 0 : capture.CapturedCellCount) + ",\"capturedLayers\":" + IntRaw(capture == null ? 0 : capture.CapturedLayerCount) + ",\"skippedAirCells\":" + IntRaw(capture == null ? 0 : capture.SkippedAirCellCount) + ",\"unavailableCells\":" + IntRaw(capture == null ? 0 : capture.UnavailableCellCount) + ",\"templateId\":\"" + EscapeJson(templateId) + "\",\"templateName\":\"" + EscapeJson(templateName) + "\",\"instanceId\":\"" + EscapeJson(eraseStart == null ? placed == null ? string.Empty : placed.InstanceId : eraseStart.TargetInstanceId) + "\",\"instanceName\":\"" + EscapeJson(eraseStart == null ? placed == null ? string.Empty : placed.InstanceName : eraseStart.TargetInstanceName) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleBlueprintLibraryCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildBlueprintUiStateJson();
            string action;
            string templateId;
            SplitBlueprintLibraryPayload(payload, out action, out templateId);
            var result = HandleBlueprintLibraryAction(command, action, templateId);
            Record(
                command,
                "Ui.Blueprint.Library." + BuildBlueprintLibraryActionLabel(action),
                "UI",
                result == null ? "Failed" : result.Outcome,
                result == null ? "Blueprint library command failed." : result.Message,
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(result != null && !result.PlaceholderOnly) + ",\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"action\":\"" + EscapeJson(action) + "\",\"templateId\":\"" + EscapeJson(templateId) + "\",\"templateName\":\"" + EscapeJson(result == null ? string.Empty : result.TemplateName) + "\",\"resultCode\":\"" + EscapeJson(result == null ? string.Empty : result.ResultCode) + "\",\"changed\":" + BoolRaw(result != null && result.Changed) + ",\"placeholderOnly\":" + BoolRaw(result != null && result.PlaceholderOnly) + ",\"exportPath\":\"" + EscapeJson(result == null ? string.Empty : result.ExportPath) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static BlueprintLibraryCommandResult HandleBlueprintLibraryAction(LegacyUiCommand command, string action, string templateId)
        {
            action = (action ?? string.Empty).Trim();
            templateId = (templateId ?? string.Empty).Trim();
            if (string.Equals(action, "page-prev", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintLibraryUiState.MovePage(-1);
            }

            if (string.Equals(action, "page-next", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintLibraryUiState.MovePage(1);
            }

            if (string.Equals(action, "name", StringComparison.OrdinalIgnoreCase))
            {
                var inputId = BlueprintLibraryUiState.BuildNameInputId(templateId);
                if (LegacyTextInput.IsFocused(inputId))
                {
                    var renamed = BlueprintLibraryUiState.RenameTemplate(templateId, LegacyTextInput.GetDraft(inputId));
                    if (renamed.Succeeded)
                    {
                        LegacyTextInput.ClearFocus();
                    }

                    return renamed;
                }

                if (command != null && command.IsDoubleClick)
                {
                    var focused = BlueprintLibraryUiState.FocusRename(templateId);
                    if (focused.Succeeded)
                    {
                        LegacyTextInput.Focus(inputId, focused.TemplateName);
                    }

                    return focused;
                }

                return BlueprintLibraryCommandResult.Create(true, false, false, "NotApplicable", "needsDoubleClick", "Double-click the blueprint template name to edit.", templateId, string.Empty, string.Empty);
            }

            if (string.Equals(action, BlueprintLibraryUiState.ConfirmNameAction, StringComparison.OrdinalIgnoreCase))
            {
                var inputId = BlueprintLibraryUiState.BuildNameInputId(templateId);
                if (!LegacyTextInput.IsFocused(inputId))
                {
                    return BlueprintLibraryCommandResult.Create(true, false, false, "NotApplicable", "nameInputNotFocused", "Blueprint template name confirm skipped because the name field is not being edited.", templateId, string.Empty, string.Empty);
                }

                var confirmed = BlueprintLibraryUiState.RenameTemplate(templateId, LegacyTextInput.GetDraft(inputId));
                if (confirmed.Succeeded)
                {
                    LegacyTextInput.ClearFocus();
                }

                return confirmed;
            }

            if (string.Equals(action, "delete", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintLibraryUiState.RequestDeleteOrConfirm(templateId);
            }

            if (string.Equals(action, "use", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintLibraryUiState.UseTemplate(templateId);
            }

            if (string.Equals(action, "export", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintLibraryUiState.ExportTemplate(templateId);
            }

            return BlueprintLibraryCommandResult.Create(false, false, false, "Rejected", "invalidAction", "Unknown blueprint library command.", templateId, string.Empty, string.Empty);
        }

        private static void HandleBlueprintPlacedInstanceCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildBlueprintUiStateJson();
            string action;
            string instanceId;
            SplitBlueprintLibraryPayload(payload, out action, out instanceId);
            var result = HandleBlueprintPlacedInstanceAction(action, instanceId);
            if (result != null && result.Succeeded && !string.Equals(action, "page-prev", StringComparison.OrdinalIgnoreCase) && !string.Equals(action, "page-next", StringComparison.OrdinalIgnoreCase))
            {
                BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.OpenPlacedInstances, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            }

            Record(
                command,
                "Ui.Blueprint.Placed." + BuildBlueprintLibraryActionLabel(action),
                "UI",
                result == null ? "Failed" : result.Outcome,
                result == null ? "Blueprint placed instance command failed." : result.Message,
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(result != null) + ",\"uiOnly\":true,\"featureId\":\"" + EscapeJson(FeatureIds.BlueprintMain) + "\",\"action\":\"" + EscapeJson(action) + "\",\"instanceId\":\"" + EscapeJson(instanceId) + "\",\"instanceName\":\"" + EscapeJson(result == null ? string.Empty : result.InstanceName) + "\",\"resultCode\":\"" + EscapeJson(result == null ? string.Empty : result.ResultCode) + "\",\"changed\":" + BoolRaw(result != null && result.Changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static BlueprintPlacedInstanceCommandResult HandleBlueprintPlacedInstanceAction(string action, string instanceId)
        {
            action = (action ?? string.Empty).Trim();
            instanceId = (instanceId ?? string.Empty).Trim();
            if (string.Equals(action, "page-prev", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintPlacedInstanceUiState.MovePage(-1);
            }

            if (string.Equals(action, "page-next", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintPlacedInstanceUiState.MovePage(1);
            }

            if (string.Equals(action, "select", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintPlacedInstanceUiState.SelectInstance(instanceId);
            }

            if (string.Equals(action, "toggle-hidden", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintPlacedInstanceUiState.ToggleHidden(instanceId);
            }

            if (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintPlacedInstanceUiState.RequestRemoveOrConfirm(instanceId);
            }

            if (string.Equals(action, "layer-up", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintPlacedInstanceUiState.MoveLayer(instanceId, 1);
            }

            if (string.Equals(action, "layer-down", StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                return BlueprintPlacedInstanceUiState.MoveLayer(instanceId, -1);
            }

            return BlueprintPlacedInstanceCommandResult.Create(false, false, "Rejected", "invalidAction", "Unknown blueprint placed instance command.", instanceId, string.Empty);
        }

        private static void SetBlueprintBooleanSetting(
            LegacyUiCommand command,
            string payload,
            string scenario,
            string fieldName,
            string featureId,
            Func<AppSettings, bool> getValue,
            Action<AppSettings, bool> setValue)
        {
            var before = BuildBlueprintUiStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var old = getValue(settings);
            setValue(settings, enabled);
            ConfigService.SaveAll();
            Record(
                command,
                scenario,
                "UI",
                old == enabled ? "NotApplicable" : "Succeeded",
                old == enabled ? "Blueprint setting unchanged." : "Blueprint setting changed.",
                before,
                BuildBlueprintUiStateJson(),
                "{\"submitted\":false,\"implemented\":false,\"uiOnly\":true,\"featureId\":\"" + EscapeJson(featureId) + "\",\"" + EscapeJson(fieldName) + "\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(old != enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static string BuildBlueprintUiStateJson()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var entry = BlueprintEntryState.GetSnapshot(settings);
            // Action events keep a lightweight UI state; full library, placed
            // instance, projection, and material scans belong to explicit commands.
            return "{" +
                   "\"toolItemId\":" + IntRaw(BlueprintSettings.NormalizeToolItemId(settings.BlueprintToolItemId)) + "," +
                   "\"handheldEntryEnabled\":" + BoolRaw(settings.BlueprintHandheldEntryEnabled) + "," +
                   "\"autoPlacementEnabled\":" + BoolRaw(settings.BlueprintAutoPlacementEnabled) + "," +
                   "\"replacementEnabled\":" + BoolRaw(settings.BlueprintReplacementEnabled) + "," +
                   "\"replacementTorchesEnabled\":" + BoolRaw(settings.BlueprintReplacementTorchesEnabled) + "," +
                   "\"replacementPlatformsEnabled\":" + BoolRaw(settings.BlueprintReplacementPlatformsEnabled) + "," +
                   "\"replacementWorkBenchesEnabled\":" + BoolRaw(settings.BlueprintReplacementWorkBenchesEnabled) + "," +
                   "\"replacementChairsEnabled\":" + BoolRaw(settings.BlueprintReplacementChairsEnabled) + "," +
                   "\"replacementDoorsEnabled\":" + BoolRaw(settings.BlueprintReplacementDoorsEnabled) + "," +
                   "\"replacementTablesEnabled\":" + BoolRaw(settings.BlueprintReplacementTablesEnabled) + "," +
                   "\"replacementChestsEnabled\":" + BoolRaw(settings.BlueprintReplacementChestsEnabled) + "," +
                   "\"replacementSignsEnabled\":" + BoolRaw(settings.BlueprintReplacementSignsEnabled) + "," +
                   "\"entryMode\":\"" + EscapeJson(entry.Mode) + "\"," +
                   "\"selectedTemplateId\":\"" + EscapeJson(entry.SelectedTemplateId) + "\"," +
                   "\"creationMask\":" + BlueprintCreationMaskState.BuildUiStateJson() + "," +
                   "\"placementPreview\":" + BlueprintPlacementPreviewState.BuildUiStateJson() + "," +
                   "\"eraseRegion\":" + BlueprintEraseRegionState.BuildUiStateJson() + "," +
                   "\"mirror\":" + BlueprintMirrorService.BuildUiStateJson() + "," +
                   "\"projection\":" + BlueprintProjectionService.BuildUiStateJson() + "," +
                   "\"materials\":" + BlueprintMaterialService.BuildUiStateJson() + "," +
                   "\"autoPlacement\":" + BlueprintAutoPlacementService.BuildUiStateJson() + "," +
                   "\"stateScope\":\"lightweight\"" +
                   "}";
        }

        private static void SplitBlueprintReplacementCategoryPayload(string payload, out string category, out string mode)
        {
            payload = payload ?? string.Empty;
            var split = payload.IndexOf(':');
            if (split < 0)
            {
                category = payload;
                mode = string.Empty;
                return;
            }

            category = payload.Substring(0, split);
            mode = payload.Substring(split + 1);
        }

        private static void SplitBlueprintLibraryPayload(string payload, out string action, out string templateId)
        {
            payload = payload ?? string.Empty;
            var split = payload.IndexOf(':');
            if (split < 0)
            {
                action = payload;
                templateId = string.Empty;
                return;
            }

            action = payload.Substring(0, split);
            templateId = payload.Substring(split + 1);
        }

        private static string BuildBlueprintLibraryActionLabel(string action)
        {
            return string.IsNullOrWhiteSpace(action) ? "Invalid" : action.Trim();
        }

        private static string NormalizeBlueprintActionHotkeyTargetId(string payload)
        {
            if (string.Equals(payload, FeatureIds.BlueprintCreateAction, StringComparison.OrdinalIgnoreCase))
            {
                return FeatureIds.BlueprintCreateAction;
            }

            if (string.Equals(payload, FeatureIds.BlueprintSaveAction, StringComparison.OrdinalIgnoreCase))
            {
                return FeatureIds.BlueprintSaveAction;
            }

            return string.Empty;
        }

        private static string NormalizeBlueprintActionEntryPayload(string payload)
        {
            if (string.Equals(payload, BlueprintEntryCommands.StartCreate, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintEntryCommands.StartCreate;
            }

            if (string.Equals(payload, BlueprintEntryCommands.FinishCreateSave, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintEntryCommands.FinishCreateSave;
            }

            return string.Empty;
        }

        private static string BuildBlueprintActionHotkeyLabel(string targetId)
        {
            if (string.Equals(targetId, FeatureIds.BlueprintCreateAction, StringComparison.Ordinal))
            {
                return "创建蓝图";
            }

            if (string.Equals(targetId, FeatureIds.BlueprintSaveAction, StringComparison.Ordinal))
            {
                return "保存蓝图";
            }

            return "蓝图动作";
        }

        internal static void HandleBlueprintLibraryCommandForTesting(LegacyUiCommand command)
        {
            if (command == null)
            {
                return;
            }

            if (!command.ElementId.StartsWith("blueprint-library:", StringComparison.Ordinal))
            {
                HandleBlueprintLibraryCommand(command, string.Empty);
                return;
            }

            HandleBlueprintLibraryCommand(command, command.ElementId.Substring("blueprint-library:".Length));
        }

        internal static void HandleBlueprintEntryCommandForTesting(LegacyUiCommand command)
        {
            if (command == null)
            {
                return;
            }

            if (!command.ElementId.StartsWith("blueprint-entry:", StringComparison.Ordinal))
            {
                HandleBlueprintEntryCommand(command, string.Empty);
                return;
            }

            HandleBlueprintEntryCommand(command, command.ElementId.Substring("blueprint-entry:".Length));
        }

        internal static void HandleBlueprintActionEntryCommandForTesting(LegacyUiCommand command)
        {
            if (command == null)
            {
                return;
            }

            if (!command.ElementId.StartsWith("blueprint-action-entry:", StringComparison.Ordinal))
            {
                HandleBlueprintActionEntryCommand(command, string.Empty);
                return;
            }

            HandleBlueprintActionEntryCommand(command, command.ElementId.Substring("blueprint-action-entry:".Length));
        }

        internal static void HandleBlueprintHandheldActionBarCommandForTesting(LegacyUiCommand command)
        {
            if (command == null)
            {
                return;
            }

            if (!command.ElementId.StartsWith(BlueprintHandheldActionBarState.CommandElementPrefix, StringComparison.Ordinal))
            {
                HandleBlueprintHandheldActionBarCommand(command, string.Empty);
                return;
            }

            HandleBlueprintHandheldActionBarCommand(command, command.ElementId.Substring(BlueprintHandheldActionBarState.CommandElementPrefix.Length));
        }

        internal static void HandleBlueprintPlacedInstanceCommandForTesting(LegacyUiCommand command)
        {
            if (command == null)
            {
                return;
            }

            if (!command.ElementId.StartsWith("blueprint-placed:", StringComparison.Ordinal))
            {
                HandleBlueprintPlacedInstanceCommand(command, string.Empty);
                return;
            }

            HandleBlueprintPlacedInstanceCommand(command, command.ElementId.Substring("blueprint-placed:".Length));
        }

        internal static void HandleBlueprintReplacementCategoryCommandForTesting(LegacyUiCommand command)
        {
            if (command == null)
            {
                return;
            }

            if (!command.ElementId.StartsWith("blueprint-replacement-category:", StringComparison.Ordinal))
            {
                HandleBlueprintReplacementCategoryMode(command, string.Empty);
                return;
            }

            HandleBlueprintReplacementCategoryMode(command, command.ElementId.Substring("blueprint-replacement-category:".Length));
        }
    }
}
