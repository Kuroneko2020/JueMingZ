using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Blueprint;

namespace JueMingZ.UI.Legacy
{
    internal sealed class BlueprintLibraryUiSnapshot
    {
        public IReadOnlyList<BlueprintTemplateRecord> Templates { get; set; }
        public bool LoadSucceeded { get; set; }
        public string LoadResultCode { get; set; }
        public string LoadMessage { get; set; }
        public string SelectedTemplateId { get; set; }
        public string DeleteConfirmTemplateId { get; set; }
        public string LastNotice { get; set; }
        public string LastResultCode { get; set; }
        public string LastExportPath { get; set; }
        public string LastImportPath { get; set; }
        public bool IsOpen { get; set; }
        public int PageIndex { get; set; }
        public int PageCount { get; set; }
        public int PageSize { get; set; }
        public int VisibleStartIndex { get; set; }
        public int VisibleCount { get; set; }
        public int Revision { get; set; }

        public BlueprintLibraryUiSnapshot()
        {
            Templates = new List<BlueprintTemplateRecord>();
            LoadResultCode = string.Empty;
            LoadMessage = string.Empty;
            SelectedTemplateId = string.Empty;
            DeleteConfirmTemplateId = string.Empty;
            LastNotice = string.Empty;
            LastResultCode = string.Empty;
            LastExportPath = string.Empty;
            LastImportPath = string.Empty;
            PageSize = BlueprintLibraryUiState.PageSize;
        }
    }

    internal sealed class BlueprintLibraryCommandResult
    {
        private BlueprintLibraryCommandResult()
        {
            Outcome = "NotApplicable";
            ResultCode = string.Empty;
            Message = string.Empty;
            TemplateId = string.Empty;
            TemplateName = string.Empty;
            ExportPath = string.Empty;
            ImportPath = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public bool Changed { get; private set; }
        public bool PlaceholderOnly { get; private set; }
        public string Outcome { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string TemplateId { get; private set; }
        public string TemplateName { get; private set; }
        public string ExportPath { get; private set; }
        public string ImportPath { get; private set; }

        public static BlueprintLibraryCommandResult Create(
            bool succeeded,
            bool changed,
            bool placeholderOnly,
            string outcome,
            string resultCode,
            string message,
            string templateId,
            string templateName,
            string exportPath)
        {
            return Create(succeeded, changed, placeholderOnly, outcome, resultCode, message, templateId, templateName, exportPath, string.Empty);
        }

        public static BlueprintLibraryCommandResult Create(
            bool succeeded,
            bool changed,
            bool placeholderOnly,
            string outcome,
            string resultCode,
            string message,
            string templateId,
            string templateName,
            string exportPath,
            string importPath)
        {
            return new BlueprintLibraryCommandResult
            {
                Succeeded = succeeded,
                Changed = changed,
                PlaceholderOnly = placeholderOnly,
                Outcome = string.IsNullOrWhiteSpace(outcome) ? (succeeded ? "Succeeded" : "Failed") : outcome,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                TemplateId = templateId ?? string.Empty,
                TemplateName = templateName ?? string.Empty,
                ExportPath = exportPath ?? string.Empty,
                ImportPath = importPath ?? string.Empty
            };
        }
    }

    internal static class BlueprintLibraryUiState
    {
        public const int PageSize = 6;
        public const string NameElementPrefix = "blueprint-library:name:";
        public const string ConfirmNameAction = "confirm-name";

        private static readonly object SyncRoot = new object();
        private static BlueprintTemplateLibraryStore _store;
        private static BlueprintTemplateLibraryStore _testingStore;
        private static string _storeRoot = string.Empty;
        private static bool _loaded;
        private static BlueprintTemplateLibrarySnapshot _snapshot = new BlueprintTemplateLibrarySnapshot(new List<BlueprintTemplateRecord>(), string.Empty, 0);
        private static BlueprintStorageOperationResult _lastLoadResult = BlueprintStorageOperationResult.Success("missing", "missing", string.Empty);
        private static string _selectedTemplateId = string.Empty;
        private static string _deleteConfirmTemplateId = string.Empty;
        private static string _lastNotice = "蓝图库待命。";
        private static string _lastResultCode = string.Empty;
        private static string _lastExportPath = string.Empty;
        private static string _lastImportPath = string.Empty;
        private static bool _isOpen;
        private static int _pageIndex;
        private static int _revision;

        public static bool IsOpen
        {
            get
            {
                lock (SyncRoot)
                {
                    return _isOpen;
                }
            }
        }

        public static BlueprintLibraryUiSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                return BuildSnapshotLocked();
            }
        }

        public static BlueprintLibraryCommandResult OpenLibrary()
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                if (_lastLoadResult == null || !_lastLoadResult.Succeeded)
                {
                    RecordNoticeLocked("loadFailed", "蓝图库读取失败：" + (_lastLoadResult == null ? string.Empty : _lastLoadResult.Message), string.Empty, false);
                    return CreateResultLocked(false, false, false, "Failed", "loadFailed", _lastNotice, string.Empty, string.Empty, string.Empty);
                }

                var count = _snapshot == null || _snapshot.Templates == null ? 0 : _snapshot.Templates.Count;
                var changed = !_isOpen;
                _isOpen = true;
                RecordNoticeLocked("opened", "蓝图库已打开，模板 " + count.ToString(CultureInfo.InvariantCulture) + " 个。", string.Empty, false);
                return CreateResultLocked(true, changed, false, changed ? "Succeeded" : "NotApplicable", "opened", _lastNotice, string.Empty, string.Empty, string.Empty);
            }
        }

        public static BlueprintLibraryCommandResult CloseLibrary()
        {
            lock (SyncRoot)
            {
                var changed = _isOpen;
                _isOpen = false;
                RecordNoticeLocked(changed ? "closed" : "closeNoop", changed ? "已返回蓝图主菜单。" : "蓝图库未打开。", string.Empty, changed);
                return CreateResultLocked(true, changed, false, changed ? "Succeeded" : "NotApplicable", _lastResultCode, _lastNotice, string.Empty, string.Empty, string.Empty);
            }
        }

        public static BlueprintLibraryCommandResult MovePage(int delta)
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                var old = _pageIndex;
                _pageIndex = ClampPageIndex(_pageIndex + delta, GetTemplateCountLocked());
                var changed = old != _pageIndex;
                RecordNoticeLocked(
                    changed ? "pageChanged" : "pageUnchanged",
                    changed ? "蓝图库页码已切换。" : "蓝图库页码没有变化。",
                    string.Empty,
                    changed);
                return CreateResultLocked(true, changed, false, changed ? "Succeeded" : "NotApplicable", _lastResultCode, _lastNotice, string.Empty, string.Empty, string.Empty);
            }
        }

        public static BlueprintLibraryCommandResult FocusRename(string templateId)
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                BlueprintTemplateRecord template;
                if (!TryFindTemplateLocked(templateId, out template))
                {
                    RecordNoticeLocked("missingTemplate", "蓝图模板不存在。", templateId, false);
                    return CreateResultLocked(false, false, false, "Failed", "missingTemplate", _lastNotice, templateId, string.Empty, string.Empty);
                }

                _selectedTemplateId = template.TemplateId;
                _deleteConfirmTemplateId = string.Empty;
                RecordNoticeLocked("renameFocused", "正在编辑模板名称。", template.TemplateId, true);
                return CreateResultLocked(true, true, false, "Succeeded", "renameFocused", _lastNotice, template.TemplateId, template.Name, string.Empty);
            }
        }

        public static BlueprintLibraryCommandResult RenameTemplate(string templateId, string requestedName)
        {
            BlueprintTemplateLibraryStore store;
            lock (SyncRoot)
            {
                store = ResolveStoreLocked();
            }

            BlueprintTemplateRecord renamed;
            var write = store.RenameTemplate(templateId, requestedName, out renamed);
            lock (SyncRoot)
            {
                if (write.Succeeded)
                {
                    _loaded = false;
                    RefreshLocked();
                    if (renamed != null)
                    {
                        _selectedTemplateId = renamed.TemplateId;
                    }

                    _deleteConfirmTemplateId = string.Empty;
                }

                var changed = write.Succeeded && !string.Equals(write.ResultCode, "unchanged", StringComparison.Ordinal);
                var outcome = write.Succeeded ? (changed ? "Succeeded" : "NotApplicable") : "Failed";
                var name = renamed == null ? string.Empty : renamed.Name;
                RecordNoticeLocked(
                    write.ResultCode,
                    write.Succeeded
                        ? (changed ? "模板已重命名为 " + name + "。" : "模板名称没有变化。")
                        : "模板重命名失败：" + write.Message,
                    renamed == null ? templateId : renamed.TemplateId,
                    changed);
                return CreateResultLocked(write.Succeeded, changed, false, outcome, write.ResultCode, _lastNotice, renamed == null ? templateId : renamed.TemplateId, name, string.Empty);
            }
        }

        public static BlueprintLibraryCommandResult RequestDeleteOrConfirm(string templateId)
        {
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(templateId);
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                BlueprintTemplateRecord template;
                if (!TryFindTemplateLocked(normalizedId, out template))
                {
                    RecordNoticeLocked("missingTemplate", "蓝图模板不存在。", normalizedId, false);
                    return CreateResultLocked(false, false, false, "Failed", "missingTemplate", _lastNotice, normalizedId, string.Empty, string.Empty);
                }

                if (!string.Equals(_deleteConfirmTemplateId, normalizedId, StringComparison.Ordinal))
                {
                    _deleteConfirmTemplateId = normalizedId;
                    _selectedTemplateId = normalizedId;
                    RecordNoticeLocked("deleteConfirmArmed", "再次点击删除确认移除模板。", normalizedId, true);
                    return CreateResultLocked(true, true, false, "Succeeded", "deleteConfirmArmed", _lastNotice, normalizedId, template.Name, string.Empty);
                }
            }

            BlueprintTemplateLibraryStore store;
            lock (SyncRoot)
            {
                store = ResolveStoreLocked();
            }

            var write = store.DeleteTemplate(normalizedId);
            lock (SyncRoot)
            {
                if (write.Succeeded)
                {
                    _deleteConfirmTemplateId = string.Empty;
                    if (string.Equals(_selectedTemplateId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedTemplateId = string.Empty;
                    }

                    _loaded = false;
                    RefreshLocked();
                }

                RecordNoticeLocked(
                    write.ResultCode,
                    write.Succeeded ? "模板已删除；已放置实例不受影响。" : "模板删除失败：" + write.Message,
                    normalizedId,
                    write.Succeeded);
                return CreateResultLocked(write.Succeeded, write.Succeeded, false, write.Succeeded ? "Succeeded" : "Failed", write.ResultCode, _lastNotice, normalizedId, string.Empty, string.Empty);
            }
        }

        public static BlueprintLibraryCommandResult ExportTemplate(string templateId)
        {
            BlueprintTemplateLibraryStore store;
            lock (SyncRoot)
            {
                store = ResolveStoreLocked();
            }

            string savedPath;
            var write = store.ExportTemplate(templateId, string.Empty, out savedPath);
            lock (SyncRoot)
            {
                if (write.Succeeded)
                {
                    _selectedTemplateId = BlueprintTemplateLibraryStore.NormalizeId(templateId);
                    _lastExportPath = savedPath ?? string.Empty;
                }
                else
                {
                    _lastExportPath = string.Empty;
                }

                RecordNoticeLocked(
                    write.ResultCode,
                    write.Succeeded ? "模板已导出。" : "模板导出失败：" + write.Message,
                    templateId,
                    write.Succeeded);
                return CreateResultLocked(write.Succeeded, write.Succeeded, false, write.Succeeded ? "Succeeded" : "Failed", write.ResultCode, _lastNotice, templateId, string.Empty, savedPath);
            }
        }

        public static BlueprintLibraryCommandResult ImportTemplate()
        {
            BlueprintTemplateLibraryStore store;
            lock (SyncRoot)
            {
                store = ResolveStoreLocked();
            }

            BlueprintTemplateRecord imported;
            var write = store.ImportTemplate(string.Empty, out imported);
            lock (SyncRoot)
            {
                _lastImportPath = write == null ? string.Empty : write.Path ?? string.Empty;
                if (write.Succeeded)
                {
                    _loaded = false;
                    RefreshLocked();
                    if (imported != null)
                    {
                        _selectedTemplateId = imported.TemplateId;
                    }

                    _deleteConfirmTemplateId = string.Empty;
                }

                var importedId = imported == null ? string.Empty : imported.TemplateId;
                var importedName = imported == null ? string.Empty : imported.Name;
                RecordNoticeLocked(
                    write.ResultCode,
                    write.Succeeded ? "模板已导入：" + importedName + "。" : "模板导入失败：" + write.Message,
                    importedId,
                    write.Succeeded);
                return CreateResultLocked(write.Succeeded, write.Succeeded, false, write.Succeeded ? "Succeeded" : "Failed", write.ResultCode, _lastNotice, importedId, importedName, string.Empty, _lastImportPath);
            }
        }

        public static BlueprintLibraryCommandResult UseTemplate(string templateId)
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                BlueprintTemplateRecord template;
                if (!TryFindTemplateLocked(templateId, out template))
                {
                    RecordNoticeLocked("missingTemplate", "蓝图模板不存在。", templateId, false);
                    return CreateResultLocked(false, false, false, "Failed", "missingTemplate", _lastNotice, templateId, string.Empty, string.Empty);
                }

                _selectedTemplateId = template.TemplateId;
                _deleteConfirmTemplateId = string.Empty;
                var entry = BlueprintEntryState.SelectTemplateForPlacement(template);
                RecordNoticeLocked("templateSelected", entry.Message, template.TemplateId, true);
                return CreateResultLocked(entry.Succeeded, entry.Changed, false, entry.Succeeded ? "Succeeded" : "Failed", entry.ResultCode, _lastNotice, template.TemplateId, template.Name, string.Empty);
            }
        }

        public static void NotifyTemplateCreated(BlueprintTemplateRecord template)
        {
            lock (SyncRoot)
            {
                _loaded = false;
                RefreshLocked();
                if (template != null && TemplateExistsLocked(template.TemplateId))
                {
                    _selectedTemplateId = template.TemplateId;
                }

                _deleteConfirmTemplateId = string.Empty;
                RecordNoticeLocked(
                    "templateCreated",
                    "新蓝图模板已加入蓝图库。",
                    template == null ? string.Empty : template.TemplateId,
                    true);
            }
        }

        public static BlueprintTemplateRecord GetSelectedOrFirstTemplate(BlueprintLibraryUiSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Templates == null || snapshot.Templates.Count <= 0)
            {
                return null;
            }

            var selectedId = BlueprintTemplateLibraryStore.NormalizeId(snapshot.SelectedTemplateId);
            for (var index = 0; index < snapshot.Templates.Count; index++)
            {
                var template = snapshot.Templates[index];
                if (template != null &&
                    string.Equals(template.TemplateId, selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    return template;
                }
            }

            return snapshot.Templates[0];
        }

        public static string BuildTemplateSummary(BlueprintTemplateRecord template)
        {
            if (template == null)
            {
                return "无模板";
            }

            return template.Width.ToString(CultureInfo.InvariantCulture) + "x" +
                   template.Height.ToString(CultureInfo.InvariantCulture) +
                   " / 单元 " + Count(template.Cells).ToString(CultureInfo.InvariantCulture) +
                   " / 材料 " + Count(template.Materials).ToString(CultureInfo.InvariantCulture);
        }

        public static string BuildUiStateJson()
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                return "{" +
                       "\"templateCount\":" + GetTemplateCountLocked().ToString(CultureInfo.InvariantCulture) + "," +
                       "\"loadSucceeded\":" + BoolRaw(_lastLoadResult != null && _lastLoadResult.Succeeded) + "," +
                       "\"loadResultCode\":\"" + EscapeJson(_lastLoadResult == null ? string.Empty : _lastLoadResult.ResultCode) + "\"," +
                       "\"pageIndex\":" + _pageIndex.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"pageCount\":" + CalculatePageCount(GetTemplateCountLocked()).ToString(CultureInfo.InvariantCulture) + "," +
                       "\"selectedTemplateId\":\"" + EscapeJson(_selectedTemplateId) + "\"," +
                       "\"deleteConfirmTemplateId\":\"" + EscapeJson(_deleteConfirmTemplateId) + "\"," +
                       "\"lastResultCode\":\"" + EscapeJson(_lastResultCode) + "\"," +
                       "\"isOpen\":" + BoolRaw(_isOpen) + "," +
                       "\"lastExportPath\":\"" + EscapeJson(_lastExportPath) + "\"," +
                       "\"lastImportPath\":\"" + EscapeJson(_lastImportPath) + "\"" +
                       "}";
            }
        }

        public static int BuildStateSignature()
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + (_isOpen ? 1 : 0);
                    hash = hash * 31 + _revision;
                    hash = hash * 31 + GetTemplateCountLocked();
                    hash = hash * 31 + _pageIndex;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_selectedTemplateId ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_deleteConfirmTemplateId ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_lastNotice ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_lastResultCode ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_lastExportPath ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_lastImportPath ?? string.Empty);
                    hash = hash * 31 + (LegacyTextInput.IsAnyFocused ? 1 : 0);
                    return hash;
                }
            }
        }

        public static string BuildNameInputId(string templateId)
        {
            return "blueprint-library:name-input:" + BlueprintTemplateLibraryStore.NormalizeId(templateId);
        }

        public static string BuildCommandId(string action, string templateId)
        {
            action = string.IsNullOrWhiteSpace(action) ? string.Empty : action.Trim();
            var id = BlueprintTemplateLibraryStore.NormalizeId(templateId);
            return "blueprint-library:" + action + (string.IsNullOrEmpty(id) ? string.Empty : ":" + id);
        }

        public static int CalculatePageCount(int templateCount)
        {
            templateCount = Math.Max(0, templateCount);
            return templateCount <= 0 ? 0 : (templateCount + PageSize - 1) / PageSize;
        }

        internal static void SetStoreForTesting(BlueprintTemplateLibraryStore store, bool reload)
        {
            lock (SyncRoot)
            {
                _testingStore = store;
                _loaded = false;
                _snapshot = new BlueprintTemplateLibrarySnapshot(new List<BlueprintTemplateRecord>(), string.Empty, 0);
                _pageIndex = 0;
                _selectedTemplateId = string.Empty;
                _deleteConfirmTemplateId = string.Empty;
                _lastNotice = "蓝图库待命。";
                _lastResultCode = string.Empty;
                _lastExportPath = string.Empty;
                _lastImportPath = string.Empty;
                _isOpen = false;
                if (reload)
                {
                    EnsureLoadedLocked();
                }
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _store = null;
                _testingStore = null;
                _storeRoot = string.Empty;
                _loaded = false;
                _snapshot = new BlueprintTemplateLibrarySnapshot(new List<BlueprintTemplateRecord>(), string.Empty, 0);
                _lastLoadResult = BlueprintStorageOperationResult.Success("missing", "missing", string.Empty);
                _selectedTemplateId = string.Empty;
                _deleteConfirmTemplateId = string.Empty;
                _lastNotice = "蓝图库待命。";
                _lastResultCode = string.Empty;
                _lastExportPath = string.Empty;
                _lastImportPath = string.Empty;
                _isOpen = false;
                _pageIndex = 0;
                _revision = 0;
            }
        }

        internal static int GetPageSizeForTesting()
        {
            return PageSize;
        }

        private static BlueprintLibraryUiSnapshot BuildSnapshotLocked()
        {
            var count = GetTemplateCountLocked();
            _pageIndex = ClampPageIndex(_pageIndex, count);
            var pageCount = CalculatePageCount(count);
            var start = pageCount <= 0 ? 0 : Math.Min(count, _pageIndex * PageSize);
            var visible = Math.Min(PageSize, Math.Max(0, count - start));
            return new BlueprintLibraryUiSnapshot
            {
                Templates = _snapshot == null ? new List<BlueprintTemplateRecord>() : _snapshot.Templates,
                LoadSucceeded = _lastLoadResult != null && _lastLoadResult.Succeeded,
                LoadResultCode = _lastLoadResult == null ? string.Empty : _lastLoadResult.ResultCode,
                LoadMessage = _lastLoadResult == null ? string.Empty : _lastLoadResult.Message,
                SelectedTemplateId = _selectedTemplateId,
                DeleteConfirmTemplateId = _deleteConfirmTemplateId,
                LastNotice = _lastNotice,
                LastResultCode = _lastResultCode,
                LastExportPath = _lastExportPath,
                LastImportPath = _lastImportPath,
                IsOpen = _isOpen,
                PageIndex = _pageIndex,
                PageCount = pageCount,
                PageSize = PageSize,
                VisibleStartIndex = start,
                VisibleCount = visible,
                Revision = _revision
            };
        }

        private static BlueprintLibraryCommandResult CreateResultLocked(
            bool succeeded,
            bool changed,
            bool placeholderOnly,
            string outcome,
            string resultCode,
            string message,
            string templateId,
            string templateName,
            string exportPath)
        {
            return BlueprintLibraryCommandResult.Create(succeeded, changed, placeholderOnly, outcome, resultCode, message, templateId, templateName, exportPath);
        }

        private static BlueprintLibraryCommandResult CreateResultLocked(
            bool succeeded,
            bool changed,
            bool placeholderOnly,
            string outcome,
            string resultCode,
            string message,
            string templateId,
            string templateName,
            string exportPath,
            string importPath)
        {
            return BlueprintLibraryCommandResult.Create(succeeded, changed, placeholderOnly, outcome, resultCode, message, templateId, templateName, exportPath, importPath);
        }

        private static void EnsureLoadedLocked()
        {
            ResolveStoreLocked();
            if (_loaded)
            {
                return;
            }

            RefreshLocked();
        }

        private static void RefreshLocked()
        {
            var store = ResolveStoreLocked();
            BlueprintTemplateLibrarySnapshot snapshot;
            var load = store.TryLoad(out snapshot);
            _lastLoadResult = load;
            _snapshot = snapshot ?? new BlueprintTemplateLibrarySnapshot(new List<BlueprintTemplateRecord>(), store.LibraryPath, _revision);
            _loaded = true;
            unchecked
            {
                _revision++;
            }

            _pageIndex = ClampPageIndex(_pageIndex, GetTemplateCountLocked());
            if (!TemplateExistsLocked(_selectedTemplateId))
            {
                _selectedTemplateId = GetTemplateCountLocked() > 0 ? _snapshot.Templates[0].TemplateId : string.Empty;
            }

            if (!TemplateExistsLocked(_deleteConfirmTemplateId))
            {
                _deleteConfirmTemplateId = string.Empty;
            }
        }

        private static BlueprintTemplateLibraryStore ResolveStoreLocked()
        {
            if (_testingStore != null)
            {
                return _testingStore;
            }

            var root = BlueprintStoragePaths.GetDefaultRootDirectory();
            if (_store == null || !string.Equals(_storeRoot, root, StringComparison.OrdinalIgnoreCase))
            {
                _store = new BlueprintTemplateLibraryStore(root);
                _storeRoot = root;
                _loaded = false;
            }

            return _store;
        }

        private static void RecordNoticeLocked(string resultCode, string message, string templateId, bool changed)
        {
            _lastResultCode = resultCode ?? string.Empty;
            _lastNotice = message ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(templateId) && TemplateExistsLocked(templateId))
            {
                _selectedTemplateId = BlueprintTemplateLibraryStore.NormalizeId(templateId);
            }

            if (changed)
            {
                unchecked
                {
                    _revision++;
                }
            }
        }

        private static bool TryFindTemplateLocked(string templateId, out BlueprintTemplateRecord template)
        {
            template = null;
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(templateId);
            if (string.IsNullOrEmpty(normalizedId) || _snapshot == null || _snapshot.Templates == null)
            {
                return false;
            }

            for (var index = 0; index < _snapshot.Templates.Count; index++)
            {
                var candidate = _snapshot.Templates[index];
                if (candidate != null &&
                    string.Equals(candidate.TemplateId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    template = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TemplateExistsLocked(string templateId)
        {
            BlueprintTemplateRecord ignored;
            return TryFindTemplateLocked(templateId, out ignored);
        }

        private static int GetTemplateCountLocked()
        {
            return _snapshot == null || _snapshot.Templates == null ? 0 : _snapshot.Templates.Count;
        }

        private static int ClampPageIndex(int pageIndex, int templateCount)
        {
            var pageCount = CalculatePageCount(templateCount);
            if (pageCount <= 1)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(pageIndex, pageCount - 1));
        }

        private static int Count<T>(ICollection<T> items)
        {
            return items == null ? 0 : items.Count;
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
