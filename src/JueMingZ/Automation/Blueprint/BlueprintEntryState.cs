using System;
using System.Globalization;
using JueMingZ.Config;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintEntryModes
    {
        public const string Tool = "Tool";
        public const string Creating = "Creating";
        public const string CreatedPendingSave = "CreatedPendingSave";
        public const string PlacementPreview = "PlacementPreview";
        public const string PlacedManagement = "PlacedManagement";
        public const string EraseRegion = "EraseRegion";

        public static string Normalize(string mode)
        {
            if (string.Equals(mode, Creating, StringComparison.OrdinalIgnoreCase))
            {
                return Creating;
            }

            if (string.Equals(mode, CreatedPendingSave, StringComparison.OrdinalIgnoreCase))
            {
                return CreatedPendingSave;
            }

            if (string.Equals(mode, PlacementPreview, StringComparison.OrdinalIgnoreCase))
            {
                return PlacementPreview;
            }

            if (string.Equals(mode, PlacedManagement, StringComparison.OrdinalIgnoreCase))
            {
                return PlacedManagement;
            }

            if (string.Equals(mode, EraseRegion, StringComparison.OrdinalIgnoreCase))
            {
                return EraseRegion;
            }

            return Tool;
        }

        public static string GetDisplayName(string mode)
        {
            switch (Normalize(mode))
            {
                case Creating:
                    return "创建入口";
                case CreatedPendingSave:
                    return "待保存";
                case PlacementPreview:
                    return "投影预览";
                case PlacedManagement:
                    return "已放置管理";
                case EraseRegion:
                    return "擦除区域";
                default:
                    return "工具入口";
            }
        }
    }

    internal static class BlueprintEntryCommands
    {
        public const string OpenEntryHotkey = "open-entry-hotkey";
        public const string StartCreate = "start-create";
        public const string OpenLibrary = "open-library";
        public const string OpenPlacedInstances = "open-placed";
        public const string OpenMaterials = "open-materials";
        public const string StartErase = "start-erase";
        public const string ClearSelection = "clear-selection";
        public const string FinishCreateSave = "finish-create-save";
        public const string FinishCreateUse = "finish-create-use";
        public const string MirrorPreviewHorizontal = "mirror-preview-horizontal";
        public const string Cancel = "cancel";
    }

    internal sealed class BlueprintEntrySnapshot
    {
        public string Mode { get; set; }
        public string ModeDisplayName { get; set; }
        public string LastNotice { get; set; }
        public string LastSource { get; set; }
        public string SelectedTemplateId { get; set; }
        public string SelectedTemplateName { get; set; }
        public int ToolItemId { get; set; }
        public bool HandheldEntryEnabled { get; set; }
        public bool AutoPlacementEnabled { get; set; }
        public bool ReplacementEnabled { get; set; }
    }

    internal sealed class BlueprintEntryCommandResult
    {
        private BlueprintEntryCommandResult()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
            Mode = BlueprintEntryModes.Tool;
            ModeDisplayName = BlueprintEntryModes.GetDisplayName(BlueprintEntryModes.Tool);
        }

        public bool Succeeded { get; private set; }
        public bool Changed { get; private set; }
        public bool PlaceholderOnly { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string Mode { get; private set; }
        public string ModeDisplayName { get; private set; }

        public static BlueprintEntryCommandResult Create(
            bool succeeded,
            bool changed,
            bool placeholderOnly,
            string resultCode,
            string message,
            string mode)
        {
            var normalizedMode = BlueprintEntryModes.Normalize(mode);
            return new BlueprintEntryCommandResult
            {
                Succeeded = succeeded,
                Changed = changed,
                PlaceholderOnly = placeholderOnly,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                Mode = normalizedMode,
                ModeDisplayName = BlueprintEntryModes.GetDisplayName(normalizedMode)
            };
        }
    }

    internal static class BlueprintEntryState
    {
        private static readonly object SyncRoot = new object();
        private static string _mode = BlueprintEntryModes.Tool;
        private static string _lastNotice = "入口待命。";
        private static string _lastSource = string.Empty;
        private static string _selectedTemplateId = string.Empty;
        private static string _selectedTemplateName = string.Empty;

        public static BlueprintEntrySnapshot GetSnapshot(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            lock (SyncRoot)
            {
                return new BlueprintEntrySnapshot
                {
                    Mode = _mode,
                    ModeDisplayName = BlueprintEntryModes.GetDisplayName(_mode),
                    LastNotice = _lastNotice,
                    LastSource = _lastSource,
                    SelectedTemplateId = _selectedTemplateId,
                    SelectedTemplateName = _selectedTemplateName,
                    ToolItemId = BlueprintSettings.NormalizeToolItemId(settings.BlueprintToolItemId),
                    HandheldEntryEnabled = settings.BlueprintHandheldEntryEnabled,
                    AutoPlacementEnabled = settings.BlueprintAutoPlacementEnabled,
                    ReplacementEnabled = settings.BlueprintReplacementEnabled
                };
            }
        }

        public static BlueprintEntryCommandResult ApplyCommand(string command, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var action = string.IsNullOrWhiteSpace(command) ? string.Empty : command.Trim();
            switch (action)
            {
                case BlueprintEntryCommands.OpenEntryHotkey:
                    return RecordNotice(
                        "hotkey",
                        "opened",
                        "蓝图入口已打开。",
                        _mode,
                        false,
                        false,
                        true);
                case BlueprintEntryCommands.StartCreate:
                    BlueprintPlacementPreviewState.Cancel();
                    BlueprintEraseRegionState.Cancel();
                    BlueprintCreationMaskState.BeginCreate();
                    return SetMode(
                        BlueprintEntryModes.Creating,
                        "ui",
                        "entryStateChanged",
                        "创建入口已进入 mask 选择状态。",
                        false);
                case BlueprintEntryCommands.OpenLibrary:
                    return RecordNotice(
                        "ui",
                        "libraryOpened",
                        "蓝图库已打开。",
                        _mode,
                        false,
                        false,
                        true);
                case BlueprintEntryCommands.OpenPlacedInstances:
                    BlueprintCreationMaskState.Cancel();
                    BlueprintPlacementPreviewState.Cancel();
                    BlueprintEraseRegionState.Cancel();
                    return SetMode(
                        BlueprintEntryModes.PlacedManagement,
                        "ui",
                        "placedManagementOpened",
                        "已放置蓝图管理已打开。",
                        false);
                case BlueprintEntryCommands.OpenMaterials:
                    return RecordNotice(
                        "ui",
                        "materialsOpened",
                        "材料统计浮窗已打开。",
                        _mode,
                        true,
                        false,
                        true);
                case BlueprintEntryCommands.StartErase:
                    BlueprintCreationMaskState.Cancel();
                    BlueprintPlacementPreviewState.Cancel();
                    return MarkEraseStarted(BlueprintEraseRegionState.BeginErase(string.Empty));
                case BlueprintEntryCommands.ClearSelection:
                    return ApplyCreationSelectionResult(BlueprintCreationMaskState.ClearSelection(), BlueprintEntryModes.Creating, false);
                case BlueprintEntryCommands.FinishCreateSave:
                    return ApplyCreationSelectionResult(BlueprintCreationMaskState.FinishCreate(false), BlueprintEntryModes.CreatedPendingSave, true);
                case BlueprintEntryCommands.FinishCreateUse:
                    return ApplyCreationSelectionResult(BlueprintCreationMaskState.FinishCreate(true), BlueprintEntryModes.CreatedPendingSave, true);
                case BlueprintEntryCommands.MirrorPreviewHorizontal:
                    return ApplyPlacementMirrorResult(BlueprintPlacementPreviewState.MirrorHorizontal());
                case BlueprintEntryCommands.Cancel:
                    BlueprintCreationMaskState.Cancel();
                    BlueprintPlacementPreviewState.Cancel();
                    BlueprintEraseRegionState.Cancel();
                    return SetMode(
                        BlueprintEntryModes.Tool,
                        "ui",
                        "entryStateChanged",
                        "蓝图入口已回到工具待命。",
                        true);
                default:
                    return RecordNotice(
                        "ui",
                        "unknownCommand",
                        "未知蓝图入口命令。",
                        _mode,
                        false,
                        true,
                        false);
            }
        }

        public static int BuildStateSignature(AppSettings settings)
        {
            var snapshot = GetSnapshot(settings);
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.Mode ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastNotice ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.SelectedTemplateId ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.SelectedTemplateName ?? string.Empty);
                hash = hash * 31 + snapshot.ToolItemId;
                hash = hash * 31 + (snapshot.HandheldEntryEnabled ? 1 : 0);
                hash = hash * 31 + (snapshot.AutoPlacementEnabled ? 1 : 0);
                hash = hash * 31 + (snapshot.ReplacementEnabled ? 1 : 0);
                hash = hash * 31 + BlueprintPlacementPreviewState.BuildStateSignature();
                hash = hash * 31 + BlueprintEraseRegionState.BuildStateSignature();
                return hash;
            }
        }

        public static BlueprintEntryCommandResult SelectTemplateForPlacement(BlueprintTemplateRecord template)
        {
            var preview = BlueprintPlacementPreviewState.BeginPreview(template, "library");
            if (!preview.Succeeded)
            {
                return RecordNotice(
                    "library",
                    preview.ResultCode,
                    preview.Message,
                    _mode,
                    false,
                    false,
                    false);
            }

            BlueprintCreationMaskState.Cancel();
            lock (SyncRoot)
            {
                var changed = !string.Equals(_mode, BlueprintEntryModes.PlacementPreview, StringComparison.Ordinal) ||
                              !string.Equals(_selectedTemplateId, preview.TemplateId, StringComparison.Ordinal);
                _mode = BlueprintEntryModes.PlacementPreview;
                _selectedTemplateId = preview.TemplateId;
                _selectedTemplateName = preview.TemplateName;
                _lastSource = "library";
                _lastNotice = preview.Message;
                return BlueprintEntryCommandResult.Create(
                    true,
                    changed,
                    false,
                    preview.ResultCode,
                    _lastNotice,
                    _mode);
            }
        }

        public static BlueprintEntryCommandResult MarkCaptureSaved(BlueprintCaptureResult capture)
        {
            capture = capture ?? BlueprintCaptureResult.Failure("unknown", "蓝图采集结果未知。", null, null, 0, 0, 0, 0, 0, false);
            var saved = capture.SavedTemplate ?? new BlueprintTemplateRecord();
            var name = string.IsNullOrWhiteSpace(saved.Name) ? BlueprintStorageConstants.DefaultTemplateName : saved.Name.Trim();
            BlueprintCreationMaskState.MarkCaptureSaved(name, capture.UseAfterSave);
            var preview = capture.UseAfterSave
                ? BlueprintPlacementPreviewState.BeginPreview(saved, "capture")
                : BlueprintPlacementPreviewState.Cancel();
            lock (SyncRoot)
            {
                _mode = capture.UseAfterSave && preview.Succeeded
                    ? BlueprintEntryModes.PlacementPreview
                    : BlueprintEntryModes.Tool;
                _selectedTemplateId = saved.TemplateId ?? string.Empty;
                _selectedTemplateName = name;
                _lastSource = "capture";
                _lastNotice = "已保存蓝图模板 " + name + "，单元 " +
                              capture.CapturedCellCount.ToString(CultureInfo.InvariantCulture) + "。";
                if (capture.UseAfterSave && preview.Succeeded)
                {
                    _lastNotice += " " + preview.Message;
                }
                else if (capture.UseAfterSave)
                {
                    _lastNotice += " 但摆放预览启动失败：" + preview.Message;
                }

                return BlueprintEntryCommandResult.Create(
                    !capture.UseAfterSave || preview.Succeeded,
                    true,
                    false,
                    capture.UseAfterSave && preview.Succeeded ? "templateSavedPreviewStarted" : "templateSaved",
                    _lastNotice,
                    _mode);
            }
        }

        public static BlueprintEntryCommandResult RecordCaptureFailure(BlueprintCaptureResult capture)
        {
            capture = capture ?? BlueprintCaptureResult.Failure("unknown", "蓝图采集失败。", null, null, 0, 0, 0, 0, 0, false);
            lock (SyncRoot)
            {
                _mode = BlueprintEntryModes.CreatedPendingSave;
                _lastSource = "capture";
                _lastNotice = capture.Message ?? string.Empty;
                return BlueprintEntryCommandResult.Create(
                    false,
                    false,
                    false,
                    capture.ResultCode,
                    _lastNotice,
                    _mode);
            }
        }

        public static BlueprintEntryCommandResult MarkPlacementConfirmed(BlueprintPlacementInteractionResult placement)
        {
            placement = placement ?? new BlueprintPlacementInteractionResult
            {
                Succeeded = false,
                Changed = false,
                ResultCode = "unknown",
                Message = "蓝图摆放结果未知。"
            };
            lock (SyncRoot)
            {
                if (placement.Succeeded && placement.PlacedInstance)
                {
                    _mode = BlueprintEntryModes.Tool;
                    if (placement.Instance != null)
                    {
                        _selectedTemplateId = placement.Instance.TemplateIdSnapshot ?? _selectedTemplateId;
                        _selectedTemplateName = placement.Instance.Name ?? _selectedTemplateName;
                    }
                }

                _lastSource = "placement";
                _lastNotice = placement.Message ?? string.Empty;
                return BlueprintEntryCommandResult.Create(
                    placement.Succeeded,
                    placement.Changed,
                    false,
                    placement.ResultCode,
                    _lastNotice,
                    _mode);
            }
        }

        public static BlueprintEntryCommandResult MarkEraseStarted(BlueprintEraseCommandResult erase)
        {
            erase = erase ?? BlueprintEraseCommandResult.Create(false, false, "unknown", "蓝图擦除模式状态未知。", string.Empty, string.Empty);
            lock (SyncRoot)
            {
                if (erase.Succeeded)
                {
                    _mode = BlueprintEntryModes.EraseRegion;
                    _selectedTemplateId = string.Empty;
                    _selectedTemplateName = erase.TargetInstanceName ?? string.Empty;
                }

                _lastSource = "erase";
                _lastNotice = erase.Message ?? string.Empty;
                return BlueprintEntryCommandResult.Create(
                    erase.Succeeded,
                    erase.Changed,
                    false,
                    erase.ResultCode,
                    _lastNotice,
                    _mode);
            }
        }

        public static BlueprintEntryCommandResult MarkEraseApplied(BlueprintEraseInteractionResult erase)
        {
            erase = erase ?? new BlueprintEraseInteractionResult
            {
                Succeeded = false,
                Changed = false,
                ResultCode = "unknown",
                Message = "蓝图擦除结果未知。"
            };
            lock (SyncRoot)
            {
                _mode = BlueprintEntryModes.EraseRegion;
                _selectedTemplateId = string.Empty;
                _selectedTemplateName = erase.TargetInstanceName ?? string.Empty;
                _lastSource = "erase";
                _lastNotice = erase.Message ?? string.Empty;
                return BlueprintEntryCommandResult.Create(
                    erase.Succeeded,
                    erase.Changed,
                    false,
                    erase.ResultCode,
                    _lastNotice,
                    _mode);
            }
        }

        public static string BuildSettingsSummary(AppSettings settings)
        {
            var snapshot = GetSnapshot(settings);
            return "手持快捷入口 " + (snapshot.HandheldEntryEnabled ? "开" : "关") +
                   " / 自动放置 " + (snapshot.AutoPlacementEnabled ? "开" : "关") +
                   " / 同类替换 " + (snapshot.ReplacementEnabled ? "开" : "关") +
                   " / 分类 " + CountEnabledReplacementCategories(settings).ToString(CultureInfo.InvariantCulture);
        }

        private static int CountEnabledReplacementCategories(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var count = 0;
            if (settings.BlueprintReplacementTorchesEnabled) count++;
            if (settings.BlueprintReplacementPlatformsEnabled) count++;
            if (settings.BlueprintReplacementWorkBenchesEnabled) count++;
            if (settings.BlueprintReplacementChairsEnabled) count++;
            if (settings.BlueprintReplacementDoorsEnabled) count++;
            if (settings.BlueprintReplacementTablesEnabled) count++;
            if (settings.BlueprintReplacementChestsEnabled) count++;
            if (settings.BlueprintReplacementSignsEnabled) count++;
            return count;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _mode = BlueprintEntryModes.Tool;
                _lastNotice = "入口待命。";
                _lastSource = string.Empty;
                _selectedTemplateId = string.Empty;
                _selectedTemplateName = string.Empty;
            }

            BlueprintCreationMaskState.ResetForTesting();
            BlueprintPlacementPreviewState.ResetForTesting();
            BlueprintEraseRegionState.ResetForTesting();
        }

        private static BlueprintEntryCommandResult ApplyPlacementMirrorResult(BlueprintPlacementCommandResult result)
        {
            result = result ?? BlueprintPlacementCommandResult.Create(false, false, "mirrorUnknown", "蓝图镜像结果未知。", string.Empty, string.Empty);
            lock (SyncRoot)
            {
                var mode = _mode;
                var changed = result.Changed;
                if (result.Succeeded)
                {
                    changed = changed || !string.Equals(_mode, BlueprintEntryModes.PlacementPreview, StringComparison.Ordinal);
                    _mode = BlueprintEntryModes.PlacementPreview;
                    _selectedTemplateId = result.TemplateId;
                    _selectedTemplateName = result.TemplateName;
                    mode = _mode;
                }

                _lastSource = "ui";
                _lastNotice = result.Message ?? string.Empty;
                return BlueprintEntryCommandResult.Create(
                    result.Succeeded,
                    changed,
                    false,
                    result.ResultCode,
                    _lastNotice,
                    mode);
            }
        }

        private static BlueprintEntryCommandResult ApplyCreationSelectionResult(
            BlueprintCreationInteractionResult result,
            string successMode,
            bool placeholderOnly)
        {
            result = result ?? new BlueprintCreationInteractionResult
            {
                Succeeded = false,
                Changed = false,
                ResultCode = "unknown",
                Message = "创建 mask 状态未知。"
            };
            lock (SyncRoot)
            {
                var mode = result.Succeeded ? successMode : _mode;
                var normalized = BlueprintEntryModes.Normalize(mode);
                var changed = result.Changed || !string.Equals(_mode, normalized, StringComparison.Ordinal);
                _mode = normalized;
                if (!string.Equals(normalized, BlueprintEntryModes.PlacementPreview, StringComparison.Ordinal))
                {
                    _selectedTemplateId = string.Empty;
                    _selectedTemplateName = string.Empty;
                }

                _lastSource = "ui";
                _lastNotice = result.Message ?? string.Empty;
                return BlueprintEntryCommandResult.Create(
                    result.Succeeded,
                    changed,
                    placeholderOnly,
                    result.ResultCode,
                    result.Message,
                    _mode);
            }
        }

        private static BlueprintEntryCommandResult SetMode(
            string mode,
            string source,
            string resultCode,
            string message,
            bool placeholderOnly)
        {
            var normalized = BlueprintEntryModes.Normalize(mode);
            lock (SyncRoot)
            {
                var changed = !string.Equals(_mode, normalized, StringComparison.Ordinal);
                _mode = normalized;
                if (!string.Equals(normalized, BlueprintEntryModes.PlacementPreview, StringComparison.Ordinal))
                {
                    _selectedTemplateId = string.Empty;
                    _selectedTemplateName = string.Empty;
                }

                _lastSource = source ?? string.Empty;
                _lastNotice = message ?? string.Empty;
                return BlueprintEntryCommandResult.Create(true, changed, placeholderOnly, resultCode, message, _mode);
            }
        }

        private static BlueprintEntryCommandResult RecordNotice(
            string source,
            string resultCode,
            string message,
            string mode,
            bool changed,
            bool placeholderOnly,
            bool succeeded)
        {
            lock (SyncRoot)
            {
                _lastSource = source ?? string.Empty;
                _lastNotice = message ?? string.Empty;
                return BlueprintEntryCommandResult.Create(succeeded, changed, placeholderOnly, resultCode, message, mode);
            }
        }
    }
}
