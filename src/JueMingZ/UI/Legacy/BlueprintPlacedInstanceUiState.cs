using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Records;

namespace JueMingZ.UI.Legacy
{
    internal sealed class BlueprintPlacedInstanceUiSnapshot
    {
        public IReadOnlyList<BlueprintWorldInstanceRecord> Instances { get; set; }
        public bool LoadSucceeded { get; set; }
        public string LoadResultCode { get; set; }
        public string LoadMessage { get; set; }
        public string WorldPairKey { get; set; }
        public string WorldKey { get; set; }
        public string SelectedInstanceId { get; set; }
        public string RemoveConfirmInstanceId { get; set; }
        public string LastNotice { get; set; }
        public string LastResultCode { get; set; }
        public int PageIndex { get; set; }
        public int PageCount { get; set; }
        public int PageSize { get; set; }
        public int VisibleStartIndex { get; set; }
        public int VisibleCount { get; set; }
        public int Revision { get; set; }

        public BlueprintPlacedInstanceUiSnapshot()
        {
            Instances = new List<BlueprintWorldInstanceRecord>();
            LoadResultCode = string.Empty;
            LoadMessage = string.Empty;
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            SelectedInstanceId = string.Empty;
            RemoveConfirmInstanceId = string.Empty;
            LastNotice = string.Empty;
            LastResultCode = string.Empty;
            PageSize = BlueprintPlacedInstanceUiState.PageSize;
        }
    }

    internal sealed class BlueprintPlacedInstanceCommandResult
    {
        private BlueprintPlacedInstanceCommandResult()
        {
            Outcome = "NotApplicable";
            ResultCode = string.Empty;
            Message = string.Empty;
            InstanceId = string.Empty;
            InstanceName = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public bool Changed { get; private set; }
        public string Outcome { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string InstanceId { get; private set; }
        public string InstanceName { get; private set; }

        public static BlueprintPlacedInstanceCommandResult Create(
            bool succeeded,
            bool changed,
            string outcome,
            string resultCode,
            string message,
            string instanceId,
            string instanceName)
        {
            return new BlueprintPlacedInstanceCommandResult
            {
                Succeeded = succeeded,
                Changed = changed,
                Outcome = string.IsNullOrWhiteSpace(outcome) ? (succeeded ? "Succeeded" : "Failed") : outcome,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                InstanceId = instanceId ?? string.Empty,
                InstanceName = instanceName ?? string.Empty
            };
        }
    }

    internal static class BlueprintPlacedInstanceUiState
    {
        public const int PageSize = 5;

        private static readonly object SyncRoot = new object();
        private static BlueprintWorldInstanceStore _store;
        private static BlueprintWorldInstanceStore _testingStore;
        private static BlueprintPlacementWorldContext _testingWorldContext;
        private static string _storeRoot = string.Empty;
        private static bool _loaded;
        private static BlueprintWorldInstanceSnapshot _snapshot = new BlueprintWorldInstanceSnapshot(string.Empty, string.Empty, new List<BlueprintWorldInstanceRecord>(), string.Empty, 0);
        private static BlueprintStorageOperationResult _lastLoadResult = BlueprintStorageOperationResult.Success("missing", "missing", string.Empty);
        private static string _worldPairKey = string.Empty;
        private static string _worldKey = string.Empty;
        private static string _selectedInstanceId = string.Empty;
        private static string _removeConfirmInstanceId = string.Empty;
        private static string _lastNotice = "已放置实例待命。";
        private static string _lastResultCode = string.Empty;
        private static int _pageIndex;
        private static int _revision;

        public static BlueprintPlacedInstanceUiSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                return BuildSnapshotLocked();
            }
        }

        public static BlueprintPlacedInstanceCommandResult OpenManagement()
        {
            lock (SyncRoot)
            {
                RefreshLocked();
                if (_lastLoadResult == null || !_lastLoadResult.Succeeded)
                {
                    RecordNoticeLocked("loadFailed", "当前世界蓝图实例读取失败：" + (_lastLoadResult == null ? string.Empty : _lastLoadResult.Message), string.Empty, false);
                    return CreateResultLocked(false, false, "Failed", "loadFailed", _lastNotice, string.Empty, string.Empty);
                }

                RecordNoticeLocked(
                    "opened",
                    "当前世界已放置蓝图 " + GetInstanceCountLocked().ToString(CultureInfo.InvariantCulture) + " 个。",
                    _selectedInstanceId,
                    false);
                return CreateResultLocked(true, false, "Succeeded", _lastResultCode, _lastNotice, _selectedInstanceId, GetSelectedInstanceNameLocked());
            }
        }

        public static BlueprintPlacedInstanceCommandResult MovePage(int delta)
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                var old = _pageIndex;
                _pageIndex = ClampPageIndex(_pageIndex + delta, GetInstanceCountLocked());
                var changed = old != _pageIndex;
                RecordNoticeLocked(
                    changed ? "pageChanged" : "pageUnchanged",
                    changed ? "已放置实例页码已切换。" : "已放置实例页码没有变化。",
                    _selectedInstanceId,
                    changed);
                return CreateResultLocked(true, changed, changed ? "Succeeded" : "NotApplicable", _lastResultCode, _lastNotice, _selectedInstanceId, GetSelectedInstanceNameLocked());
            }
        }

        public static BlueprintPlacedInstanceCommandResult SelectInstance(string instanceId)
        {
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(instanceId);
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                BlueprintWorldInstanceRecord instance;
                if (!TryFindInstanceLocked(normalizedId, out instance))
                {
                    RecordNoticeLocked("missingInstance", "蓝图实例不存在。", normalizedId, false);
                    return CreateResultLocked(false, false, "Failed", _lastResultCode, _lastNotice, normalizedId, string.Empty);
                }

                var changed = !string.Equals(_selectedInstanceId, normalizedId, StringComparison.OrdinalIgnoreCase);
                _selectedInstanceId = normalizedId;
                _removeConfirmInstanceId = string.Empty;
                RecordNoticeLocked("selected", "已选中蓝图实例 " + instance.Name + "。", normalizedId, changed);
                return CreateResultLocked(true, changed, changed ? "Succeeded" : "NotApplicable", _lastResultCode, _lastNotice, normalizedId, instance.Name);
            }
        }

        public static BlueprintPlacedInstanceCommandResult ToggleHidden(string instanceId)
        {
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(instanceId);
            lock (SyncRoot)
            {
                RefreshLocked();
                var instances = CloneInstances(_snapshot == null ? null : _snapshot.Instances);
                var index = FindInstanceIndex(instances, normalizedId);
                if (index < 0)
                {
                    RecordNoticeLocked("missingInstance", "蓝图实例不存在。", normalizedId, false);
                    return CreateResultLocked(false, false, "Failed", _lastResultCode, _lastNotice, normalizedId, string.Empty);
                }

                var instance = instances[index];
                instance.Hidden = !instance.Hidden;
                instance.UpdatedUtc = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow);
                var save = SaveCurrentWorldLocked(instances);
                if (!save.Succeeded)
                {
                    RecordNoticeLocked(save.ResultCode, "蓝图实例显示状态保存失败：" + save.Message, normalizedId, false);
                    return CreateResultLocked(false, false, "Failed", _lastResultCode, _lastNotice, normalizedId, instance.Name);
                }

                _selectedInstanceId = normalizedId;
                _removeConfirmInstanceId = string.Empty;
                var resultCode = instance.Hidden ? "hidden" : "shown";
                RecordNoticeLocked(resultCode, instance.Hidden ? "蓝图实例已隐藏。" : "蓝图实例已显示。", normalizedId, true);
                return CreateResultLocked(true, true, "Succeeded", _lastResultCode, _lastNotice, normalizedId, instance.Name);
            }
        }

        public static BlueprintPlacedInstanceCommandResult RequestRemoveOrConfirm(string instanceId)
        {
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(instanceId);
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                BlueprintWorldInstanceRecord instance;
                if (!TryFindInstanceLocked(normalizedId, out instance))
                {
                    RecordNoticeLocked("missingInstance", "蓝图实例不存在。", normalizedId, false);
                    return CreateResultLocked(false, false, "Failed", _lastResultCode, _lastNotice, normalizedId, string.Empty);
                }

                if (!string.Equals(_removeConfirmInstanceId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    _removeConfirmInstanceId = normalizedId;
                    _selectedInstanceId = normalizedId;
                    RecordNoticeLocked("removeConfirmArmed", "再次点击移除确认删除实例数据；模板和世界不受影响。", normalizedId, true);
                    return CreateResultLocked(true, true, "Succeeded", _lastResultCode, _lastNotice, normalizedId, instance.Name);
                }

                var instances = CloneInstances(_snapshot == null ? null : _snapshot.Instances);
                var index = FindInstanceIndex(instances, normalizedId);
                if (index >= 0)
                {
                    instances.RemoveAt(index);
                }

                RenumberLayerOrders(instances);
                var save = SaveCurrentWorldLocked(instances);
                if (!save.Succeeded)
                {
                    RecordNoticeLocked(save.ResultCode, "蓝图实例移除失败：" + save.Message, normalizedId, false);
                    return CreateResultLocked(false, false, "Failed", _lastResultCode, _lastNotice, normalizedId, instance.Name);
                }

                if (string.Equals(_selectedInstanceId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedInstanceId = GetInstanceCountLocked() > 0 ? _snapshot.Instances[0].InstanceId : string.Empty;
                }

                _removeConfirmInstanceId = string.Empty;
                RecordNoticeLocked("removed", "蓝图实例已移除；模板和世界内容未修改。", _selectedInstanceId, true);
                return CreateResultLocked(true, true, "Succeeded", _lastResultCode, _lastNotice, normalizedId, instance.Name);
            }
        }

        public static BlueprintPlacedInstanceCommandResult MoveLayer(string instanceId, int delta)
        {
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(instanceId);
            lock (SyncRoot)
            {
                RefreshLocked();
                var instances = CloneInstances(_snapshot == null ? null : _snapshot.Instances);
                SortByLayer(instances);
                var index = FindInstanceIndex(instances, normalizedId);
                if (index < 0)
                {
                    RecordNoticeLocked("missingInstance", "蓝图实例不存在。", normalizedId, false);
                    return CreateResultLocked(false, false, "Failed", _lastResultCode, _lastNotice, normalizedId, string.Empty);
                }

                var target = delta > 0 ? index + 1 : index - 1;
                if (target < 0 || target >= instances.Count)
                {
                    var edgeCode = delta > 0 ? "alreadyTopLayer" : "alreadyBottomLayer";
                    RecordNoticeLocked(edgeCode, delta > 0 ? "该实例已经是最高层级。" : "该实例已经是最低层级。", normalizedId, false);
                    return CreateResultLocked(true, false, "NotApplicable", _lastResultCode, _lastNotice, normalizedId, instances[index].Name);
                }

                var temp = instances[index];
                instances[index] = instances[target];
                instances[target] = temp;
                var now = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow);
                instances[index].UpdatedUtc = now;
                instances[target].UpdatedUtc = now;
                RenumberLayerOrders(instances);
                var save = SaveCurrentWorldLocked(instances);
                if (!save.Succeeded)
                {
                    RecordNoticeLocked(save.ResultCode, "蓝图实例层级保存失败：" + save.Message, normalizedId, false);
                    return CreateResultLocked(false, false, "Failed", _lastResultCode, _lastNotice, normalizedId, temp.Name);
                }

                _selectedInstanceId = normalizedId;
                _removeConfirmInstanceId = string.Empty;
                var resultCode = delta > 0 ? "layerRaised" : "layerLowered";
                RecordNoticeLocked(resultCode, delta > 0 ? "蓝图实例层级已上移。" : "蓝图实例层级已下移。", normalizedId, true);
                return CreateResultLocked(true, true, "Succeeded", _lastResultCode, _lastNotice, normalizedId, temp.Name);
            }
        }

        public static void NotifyInstanceCreated(BlueprintWorldInstanceRecord instance)
        {
            lock (SyncRoot)
            {
                _loaded = false;
                RefreshLocked();
                if (instance != null && InstanceExistsLocked(instance.InstanceId))
                {
                    _selectedInstanceId = BlueprintTemplateLibraryStore.NormalizeId(instance.InstanceId);
                }

                _removeConfirmInstanceId = string.Empty;
                RecordNoticeLocked("instanceCreated", "新蓝图实例已加入当前世界列表。", _selectedInstanceId, true);
            }
        }

        public static void NotifyInstancesChanged(string preferredSelectedInstanceId)
        {
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(preferredSelectedInstanceId);
            lock (SyncRoot)
            {
                _loaded = false;
                RefreshLocked();
                if (!string.IsNullOrEmpty(normalizedId) && InstanceExistsLocked(normalizedId))
                {
                    _selectedInstanceId = normalizedId;
                }

                _removeConfirmInstanceId = string.Empty;
                RecordNoticeLocked("instancesChanged", "当前世界蓝图实例数据已刷新。", _selectedInstanceId, true);
            }
        }

        public static string BuildInstanceSummary(BlueprintWorldInstanceRecord instance)
        {
            if (instance == null)
            {
                return "无实例";
            }

            var state = instance.Hidden ? "隐藏" : "显示";
            return state +
                   " / 层级 " + instance.LayerOrder.ToString(CultureInfo.InvariantCulture) +
                   " / 原点 " + instance.OriginTileX.ToString(CultureInfo.InvariantCulture) +
                   "," + instance.OriginTileY.ToString(CultureInfo.InvariantCulture) +
                   " / " + BuildTemplateSize(instance.TemplateSnapshot);
        }

        public static string BuildUiStateJson()
        {
            lock (SyncRoot)
            {
                EnsureLoadedLocked();
                return "{" +
                       "\"instanceCount\":" + GetInstanceCountLocked().ToString(CultureInfo.InvariantCulture) + "," +
                       "\"loadSucceeded\":" + BoolRaw(_lastLoadResult != null && _lastLoadResult.Succeeded) + "," +
                       "\"loadResultCode\":\"" + EscapeJson(_lastLoadResult == null ? string.Empty : _lastLoadResult.ResultCode) + "\"," +
                       "\"worldPairKey\":\"" + EscapeJson(_worldPairKey) + "\"," +
                       "\"worldKey\":\"" + EscapeJson(_worldKey) + "\"," +
                       "\"pageIndex\":" + _pageIndex.ToString(CultureInfo.InvariantCulture) + "," +
                       "\"pageCount\":" + CalculatePageCount(GetInstanceCountLocked()).ToString(CultureInfo.InvariantCulture) + "," +
                       "\"selectedInstanceId\":\"" + EscapeJson(_selectedInstanceId) + "\"," +
                       "\"removeConfirmInstanceId\":\"" + EscapeJson(_removeConfirmInstanceId) + "\"," +
                       "\"lastResultCode\":\"" + EscapeJson(_lastResultCode) + "\"" +
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
                    hash = hash * 31 + _revision;
                    hash = hash * 31 + GetInstanceCountLocked();
                    hash = hash * 31 + _pageIndex;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_worldPairKey ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_worldKey ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_selectedInstanceId ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_removeConfirmInstanceId ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_lastNotice ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_lastResultCode ?? string.Empty);
                    return hash;
                }
            }
        }

        public static string BuildCommandId(string action, string instanceId)
        {
            action = string.IsNullOrWhiteSpace(action) ? string.Empty : action.Trim();
            var id = BlueprintTemplateLibraryStore.NormalizeId(instanceId);
            return "blueprint-placed:" + action + (string.IsNullOrEmpty(id) ? string.Empty : ":" + id);
        }

        public static int CalculatePageCount(int instanceCount)
        {
            instanceCount = Math.Max(0, instanceCount);
            return instanceCount <= 0 ? 0 : (instanceCount + PageSize - 1) / PageSize;
        }

        internal static void SetDependenciesForTesting(BlueprintWorldInstanceStore store, BlueprintPlacementWorldContext worldContext, bool reload)
        {
            lock (SyncRoot)
            {
                _testingStore = store;
                _testingWorldContext = worldContext;
                ResetLoadedLocked();
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
                _testingWorldContext = null;
                _storeRoot = string.Empty;
                ResetLoadedLocked();
                _revision = 0;
            }
        }

        internal static int GetPageSizeForTesting()
        {
            return PageSize;
        }

        private static BlueprintPlacedInstanceUiSnapshot BuildSnapshotLocked()
        {
            var count = GetInstanceCountLocked();
            _pageIndex = ClampPageIndex(_pageIndex, count);
            var pageCount = CalculatePageCount(count);
            var start = pageCount <= 0 ? 0 : Math.Min(count, _pageIndex * PageSize);
            var visible = Math.Min(PageSize, Math.Max(0, count - start));
            return new BlueprintPlacedInstanceUiSnapshot
            {
                Instances = _snapshot == null ? new List<BlueprintWorldInstanceRecord>() : _snapshot.Instances,
                LoadSucceeded = _lastLoadResult != null && _lastLoadResult.Succeeded,
                LoadResultCode = _lastLoadResult == null ? string.Empty : _lastLoadResult.ResultCode,
                LoadMessage = _lastLoadResult == null ? string.Empty : _lastLoadResult.Message,
                WorldPairKey = _worldPairKey,
                WorldKey = _worldKey,
                SelectedInstanceId = _selectedInstanceId,
                RemoveConfirmInstanceId = _removeConfirmInstanceId,
                LastNotice = _lastNotice,
                LastResultCode = _lastResultCode,
                PageIndex = _pageIndex,
                PageCount = pageCount,
                PageSize = PageSize,
                VisibleStartIndex = start,
                VisibleCount = visible,
                Revision = _revision
            };
        }

        private static BlueprintPlacedInstanceCommandResult CreateResultLocked(
            bool succeeded,
            bool changed,
            string outcome,
            string resultCode,
            string message,
            string instanceId,
            string instanceName)
        {
            return BlueprintPlacedInstanceCommandResult.Create(succeeded, changed, outcome, resultCode, message, instanceId, instanceName);
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
            var context = ResolveWorldContextLocked();
            if (context == null || !context.Succeeded)
            {
                _worldPairKey = string.Empty;
                _worldKey = string.Empty;
                _snapshot = new BlueprintWorldInstanceSnapshot(string.Empty, string.Empty, new List<BlueprintWorldInstanceRecord>(), string.Empty, _revision);
                _lastLoadResult = BlueprintStorageOperationResult.Failure(
                    "worldIdentityUnavailable",
                    context == null ? "world identity unavailable" : context.FailureReason,
                    string.Empty);
                _loaded = true;
                unchecked { _revision++; }
                _selectedInstanceId = string.Empty;
                _removeConfirmInstanceId = string.Empty;
                return;
            }

            _worldPairKey = context.WorldPairKey ?? string.Empty;
            _worldKey = context.WorldKey ?? string.Empty;
            BlueprintWorldInstanceSnapshot snapshot;
            _lastLoadResult = store.TryLoadWorld(_worldPairKey, out snapshot);
            _snapshot = snapshot ?? new BlueprintWorldInstanceSnapshot(_worldPairKey, _worldKey, new List<BlueprintWorldInstanceRecord>(), store.BuildWorldPath(_worldPairKey), _revision);
            if (string.IsNullOrWhiteSpace(_worldKey) && _snapshot != null)
            {
                _worldKey = _snapshot.WorldKey ?? string.Empty;
            }

            _loaded = true;
            unchecked { _revision++; }
            _pageIndex = ClampPageIndex(_pageIndex, GetInstanceCountLocked());
            if (!InstanceExistsLocked(_selectedInstanceId))
            {
                _selectedInstanceId = GetInstanceCountLocked() > 0 ? _snapshot.Instances[0].InstanceId : string.Empty;
            }

            if (!InstanceExistsLocked(_removeConfirmInstanceId))
            {
                _removeConfirmInstanceId = string.Empty;
            }
        }

        private static BlueprintStorageOperationResult SaveCurrentWorldLocked(IList<BlueprintWorldInstanceRecord> instances)
        {
            var store = ResolveStoreLocked();
            if (string.IsNullOrWhiteSpace(_worldPairKey))
            {
                return BlueprintStorageOperationResult.Failure("invalidWorldPair", "world pair key unavailable", string.Empty);
            }

            BlueprintWorldInstanceSnapshot saved;
            var result = store.SaveWorldInstances(_worldPairKey, _worldKey, instances, out saved);
            if (result.Succeeded)
            {
                _snapshot = saved ?? new BlueprintWorldInstanceSnapshot(_worldPairKey, _worldKey, new List<BlueprintWorldInstanceRecord>(), store.BuildWorldPath(_worldPairKey), _revision);
                _loaded = true;
                _pageIndex = ClampPageIndex(_pageIndex, GetInstanceCountLocked());
                unchecked { _revision++; }
            }

            return result;
        }

        private static BlueprintWorldInstanceStore ResolveStoreLocked()
        {
            if (_testingStore != null)
            {
                return _testingStore;
            }

            var root = BlueprintStoragePaths.GetDefaultRootDirectory();
            if (_store == null || !string.Equals(_storeRoot, root, StringComparison.OrdinalIgnoreCase))
            {
                _store = new BlueprintWorldInstanceStore(root);
                _storeRoot = root;
                _loaded = false;
            }

            return _store;
        }

        private static BlueprintPlacementWorldContext ResolveWorldContextLocked()
        {
            if (_testingWorldContext != null)
            {
                return _testingWorldContext;
            }

            PlayerWorldIdentityResolution resolution;
            if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out resolution) ||
                resolution == null ||
                !resolution.IsResolved ||
                string.IsNullOrWhiteSpace(resolution.PairId) ||
                string.IsNullOrWhiteSpace(resolution.WorldId))
            {
                return BlueprintPlacementWorldContext.Failure(resolution == null ? "identityUnavailable" : resolution.FailureReason);
            }

            return BlueprintPlacementWorldContext.Success(resolution.PairId, resolution.WorldId);
        }

        private static void ResetLoadedLocked()
        {
            _loaded = false;
            _snapshot = new BlueprintWorldInstanceSnapshot(string.Empty, string.Empty, new List<BlueprintWorldInstanceRecord>(), string.Empty, 0);
            _lastLoadResult = BlueprintStorageOperationResult.Success("missing", "missing", string.Empty);
            _worldPairKey = string.Empty;
            _worldKey = string.Empty;
            _selectedInstanceId = string.Empty;
            _removeConfirmInstanceId = string.Empty;
            _lastNotice = "已放置实例待命。";
            _lastResultCode = string.Empty;
            _pageIndex = 0;
        }

        private static void RecordNoticeLocked(string resultCode, string message, string instanceId, bool changed)
        {
            _lastResultCode = resultCode ?? string.Empty;
            _lastNotice = message ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(instanceId) && InstanceExistsLocked(instanceId))
            {
                _selectedInstanceId = BlueprintTemplateLibraryStore.NormalizeId(instanceId);
            }

            if (changed)
            {
                unchecked { _revision++; }
            }
        }

        private static bool TryFindInstanceLocked(string instanceId, out BlueprintWorldInstanceRecord instance)
        {
            instance = null;
            var index = FindInstanceIndex(_snapshot == null ? null : _snapshot.Instances, instanceId);
            if (index < 0)
            {
                return false;
            }

            instance = _snapshot.Instances[index];
            return true;
        }

        private static bool InstanceExistsLocked(string instanceId)
        {
            BlueprintWorldInstanceRecord ignored;
            return TryFindInstanceLocked(instanceId, out ignored);
        }

        private static string GetSelectedInstanceNameLocked()
        {
            BlueprintWorldInstanceRecord instance;
            return TryFindInstanceLocked(_selectedInstanceId, out instance) ? instance.Name : string.Empty;
        }

        private static int GetInstanceCountLocked()
        {
            return _snapshot == null || _snapshot.Instances == null ? 0 : _snapshot.Instances.Count;
        }

        private static int ClampPageIndex(int pageIndex, int instanceCount)
        {
            var pageCount = CalculatePageCount(instanceCount);
            if (pageCount <= 1)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(pageIndex, pageCount - 1));
        }

        private static int FindInstanceIndex(IReadOnlyList<BlueprintWorldInstanceRecord> instances, string instanceId)
        {
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(instanceId);
            if (string.IsNullOrEmpty(normalizedId) || instances == null)
            {
                return -1;
            }

            for (var index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance != null && string.Equals(instance.InstanceId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static List<BlueprintWorldInstanceRecord> CloneInstances(IReadOnlyList<BlueprintWorldInstanceRecord> source)
        {
            var clone = new List<BlueprintWorldInstanceRecord>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    clone.Add(source[index].Clone());
                }
            }

            return clone;
        }

        private static void SortByLayer(List<BlueprintWorldInstanceRecord> instances)
        {
            if (instances == null)
            {
                return;
            }

            instances.Sort((left, right) =>
            {
                var layer = left.LayerOrder.CompareTo(right.LayerOrder);
                return layer != 0 ? layer : string.Compare(left.InstanceId, right.InstanceId, StringComparison.Ordinal);
            });
        }

        private static void RenumberLayerOrders(IList<BlueprintWorldInstanceRecord> instances)
        {
            if (instances == null)
            {
                return;
            }

            for (var index = 0; index < instances.Count; index++)
            {
                if (instances[index] != null)
                {
                    instances[index].LayerOrder = index;
                }
            }
        }

        private static string BuildTemplateSize(BlueprintTemplateRecord template)
        {
            if (template == null)
            {
                return "0x0";
            }

            return Math.Max(0, template.Width).ToString(CultureInfo.InvariantCulture) +
                   "x" +
                   Math.Max(0, template.Height).ToString(CultureInfo.InvariantCulture);
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
