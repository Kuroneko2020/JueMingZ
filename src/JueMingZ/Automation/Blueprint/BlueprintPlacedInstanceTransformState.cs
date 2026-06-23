using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Diagnostics;
using JueMingZ.Records;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintPlacedInstanceTransformModes
    {
        public const string Move = "move";
        public const string Mirror = "mirror";
    }

    internal static class BlueprintPlacedInstanceTransformPhases
    {
        public const string SelectTarget = "selectTarget";
        public const string PlaceTarget = "placeTarget";
    }

    internal sealed class BlueprintPlacedInstanceTransformSnapshot
    {
        public BlueprintPlacedInstanceTransformSnapshot()
        {
            Mode = string.Empty;
            Phase = string.Empty;
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            TargetInstanceId = string.Empty;
            TargetInstanceName = string.Empty;
            LastNotice = string.Empty;
            LastInputOwner = string.Empty;
            LastResultCode = string.Empty;
        }

        public bool Active { get; set; }
        public string Mode { get; set; }
        public string Phase { get; set; }
        public string WorldPairKey { get; set; }
        public string WorldKey { get; set; }
        public string TargetInstanceId { get; set; }
        public string TargetInstanceName { get; set; }
        public int TargetLayerOrder { get; set; }
        public int GrabOffsetX { get; set; }
        public int GrabOffsetY { get; set; }
        public int LastOriginTileX { get; set; }
        public int LastOriginTileY { get; set; }
        public string LastNotice { get; set; }
        public string LastInputOwner { get; set; }
        public string LastResultCode { get; set; }
    }

    internal sealed class BlueprintPlacedInstanceTransformCommandResult
    {
        private BlueprintPlacedInstanceTransformCommandResult()
        {
            Mode = string.Empty;
            Phase = string.Empty;
            ResultCode = string.Empty;
            Message = string.Empty;
            TargetInstanceId = string.Empty;
            TargetInstanceName = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public bool Changed { get; private set; }
        public string Mode { get; private set; }
        public string Phase { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string TargetInstanceId { get; private set; }
        public string TargetInstanceName { get; private set; }

        public static BlueprintPlacedInstanceTransformCommandResult Create(
            bool succeeded,
            bool changed,
            string mode,
            string phase,
            string resultCode,
            string message,
            string targetInstanceId,
            string targetInstanceName)
        {
            return new BlueprintPlacedInstanceTransformCommandResult
            {
                Succeeded = succeeded,
                Changed = changed,
                Mode = mode ?? string.Empty,
                Phase = phase ?? string.Empty,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                TargetInstanceId = targetInstanceId ?? string.Empty,
                TargetInstanceName = targetInstanceName ?? string.Empty
            };
        }
    }

    internal sealed class BlueprintPlacedInstanceTransformPointerInput
    {
        public bool UiOwned { get; set; }
        public bool WorldTileHit { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public bool LeftDown { get; set; }
        public bool LeftPressed { get; set; }
        public bool LeftReleased { get; set; }
    }

    internal sealed class BlueprintPlacedInstanceTransformInteractionResult
    {
        public bool Succeeded { get; set; }
        public bool Changed { get; set; }
        public bool ShouldConsumeLeftInput { get; set; }
        public bool InputActive { get; set; }
        public bool Completed { get; set; }
        public string Mode { get; set; }
        public string Phase { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public string TargetInstanceId { get; set; }
        public string TargetInstanceName { get; set; }
    }

    internal static class BlueprintPlacedInstanceTransformState
    {
        private static readonly object SyncRoot = new object();

        private static BlueprintWorldInstanceStore _store;
        private static BlueprintWorldInstanceStore _testingStore;
        private static BlueprintPlacementWorldContext _testingWorldContext;
        private static string _storeRoot = string.Empty;

        private static bool _active;
        private static string _mode = string.Empty;
        private static string _phase = string.Empty;
        private static string _worldPairKey = string.Empty;
        private static string _worldKey = string.Empty;
        private static string _targetInstanceId = string.Empty;
        private static string _targetInstanceName = string.Empty;
        private static int _targetLayerOrder;
        private static int _grabOffsetX;
        private static int _grabOffsetY;
        private static int _lastOriginTileX;
        private static int _lastOriginTileY;
        private static string _lastNotice = "蓝图移动 / 镜像待命。";
        private static string _lastInputOwner = string.Empty;
        private static string _lastResultCode = "idle";

        public static BlueprintPlacedInstanceTransformSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return BuildSnapshotLocked();
            }
        }

        public static BlueprintPlacedInstanceTransformCommandResult BeginMove()
        {
            return Begin(BlueprintPlacedInstanceTransformModes.Move);
        }

        public static BlueprintPlacedInstanceTransformCommandResult BeginMirror()
        {
            return Begin(BlueprintPlacedInstanceTransformModes.Mirror);
        }

        public static BlueprintPlacedInstanceTransformCommandResult Cancel()
        {
            lock (SyncRoot)
            {
                var changed = _active;
                _active = false;
                _mode = string.Empty;
                _phase = string.Empty;
                _targetInstanceId = string.Empty;
                _targetInstanceName = string.Empty;
                _targetLayerOrder = 0;
                _grabOffsetX = 0;
                _grabOffsetY = 0;
                _lastInputOwner = "ui";
                _lastResultCode = "transformCancelled";
                _lastNotice = "已取消蓝图移动 / 镜像。";
                return BlueprintPlacedInstanceTransformCommandResult.Create(
                    true,
                    changed,
                    string.Empty,
                    string.Empty,
                    _lastResultCode,
                    _lastNotice,
                    string.Empty,
                    string.Empty);
            }
        }

        public static BlueprintPlacedInstanceTransformInteractionResult HandlePointer(BlueprintPlacedInstanceTransformPointerInput input)
        {
            input = input ?? new BlueprintPlacedInstanceTransformPointerInput();
            lock (SyncRoot)
            {
                if (!_active)
                {
                    return BuildInteractionResultLocked(true, false, false, false, false, _lastResultCode, _lastNotice);
                }

                if (input.UiOwned)
                {
                    _lastInputOwner = "ui";
                    _lastResultCode = "uiOwned";
                    _lastNotice = "鼠标位于 UI 上，蓝图移动 / 镜像不会命中世界实例。";
                    return BuildInteractionResultLocked(true, false, input.LeftDown || input.LeftPressed || input.LeftReleased, true, false, _lastResultCode, _lastNotice);
                }

                if (!input.WorldTileHit)
                {
                    _lastInputOwner = "world";
                    _lastResultCode = "worldMiss";
                    _lastNotice = "鼠标未命中世界格，蓝图移动 / 镜像等待有效世界位置。";
                    return BuildInteractionResultLocked(true, false, input.LeftDown || input.LeftPressed, true, false, _lastResultCode, _lastNotice);
                }

                if (!input.LeftPressed)
                {
                    _lastInputOwner = "world";
                    _lastResultCode = "hover";
                    return BuildInteractionResultLocked(true, false, false, true, false, _lastResultCode, _lastNotice);
                }

                if (string.Equals(_mode, BlueprintPlacedInstanceTransformModes.Move, StringComparison.Ordinal) &&
                    string.Equals(_phase, BlueprintPlacedInstanceTransformPhases.PlaceTarget, StringComparison.Ordinal))
                {
                    return ApplyMoveLocked(input.TileX, input.TileY);
                }

                return SelectOrMirrorTargetLocked(input.TileX, input.TileY);
            }
        }

        public static string BuildUiStateJson()
        {
            var snapshot = GetSnapshot();
            return "{" +
                   "\"active\":" + BoolRaw(snapshot.Active) + "," +
                   "\"mode\":\"" + EscapeJson(snapshot.Mode) + "\"," +
                   "\"phase\":\"" + EscapeJson(snapshot.Phase) + "\"," +
                   "\"worldPairKey\":\"" + EscapeJson(snapshot.WorldPairKey) + "\"," +
                   "\"worldKey\":\"" + EscapeJson(snapshot.WorldKey) + "\"," +
                   "\"targetInstanceId\":\"" + EscapeJson(snapshot.TargetInstanceId) + "\"," +
                   "\"targetInstanceName\":\"" + EscapeJson(snapshot.TargetInstanceName) + "\"," +
                   "\"targetLayerOrder\":" + IntRaw(snapshot.TargetLayerOrder) + "," +
                   "\"grabOffsetX\":" + IntRaw(snapshot.GrabOffsetX) + "," +
                   "\"grabOffsetY\":" + IntRaw(snapshot.GrabOffsetY) + "," +
                   "\"lastOriginTileX\":" + IntRaw(snapshot.LastOriginTileX) + "," +
                   "\"lastOriginTileY\":" + IntRaw(snapshot.LastOriginTileY) + "," +
                   "\"lastResultCode\":\"" + EscapeJson(snapshot.LastResultCode) + "\"" +
                   "}";
        }

        public static int BuildStateSignature()
        {
            var snapshot = GetSnapshot();
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (snapshot.Active ? 1 : 0);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.Mode ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.Phase ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.WorldPairKey ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.TargetInstanceId ?? string.Empty);
                hash = hash * 31 + snapshot.TargetLayerOrder;
                hash = hash * 31 + snapshot.GrabOffsetX;
                hash = hash * 31 + snapshot.GrabOffsetY;
                hash = hash * 31 + snapshot.LastOriginTileX;
                hash = hash * 31 + snapshot.LastOriginTileY;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastResultCode ?? string.Empty);
                return hash;
            }
        }

        internal static void SetDependenciesForTesting(BlueprintWorldInstanceStore store, BlueprintPlacementWorldContext worldContext)
        {
            lock (SyncRoot)
            {
                _testingStore = store;
                _testingWorldContext = worldContext;
                _store = null;
                _storeRoot = string.Empty;
                ResetStateLocked();
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
                ResetStateLocked();
            }
        }

        private static BlueprintPlacedInstanceTransformCommandResult Begin(string mode)
        {
            mode = NormalizeMode(mode);
            lock (SyncRoot)
            {
                var context = ResolveWorldContextLocked();
                if (context == null || !context.Succeeded)
                {
                    ResetStateLocked();
                    _lastResultCode = "worldIdentityUnavailable";
                    _lastNotice = "当前玩家 / 世界身份不可用，无法移动或镜像已放置蓝图。";
                    return BlueprintPlacedInstanceTransformCommandResult.Create(false, false, mode, string.Empty, _lastResultCode, _lastNotice, string.Empty, string.Empty);
                }

                BlueprintWorldInstanceSnapshot snapshot;
                var load = ResolveStoreLocked().TryLoadWorld(context.WorldPairKey, out snapshot);
                if (!load.Succeeded)
                {
                    ResetStateLocked();
                    _lastResultCode = load.ResultCode;
                    _lastNotice = "当前世界蓝图实例读取失败：" + load.Message;
                    return BlueprintPlacedInstanceTransformCommandResult.Create(false, false, mode, string.Empty, _lastResultCode, _lastNotice, string.Empty, string.Empty);
                }

                if (!HasVisibleInstances(snapshot))
                {
                    ResetStateLocked();
                    _worldPairKey = context.WorldPairKey ?? string.Empty;
                    _worldKey = context.WorldKey ?? string.Empty;
                    _lastResultCode = "noVisibleInstances";
                    _lastNotice = "当前世界没有可移动或镜像的可见蓝图实例。";
                    return BlueprintPlacedInstanceTransformCommandResult.Create(false, false, mode, string.Empty, _lastResultCode, _lastNotice, string.Empty, string.Empty);
                }

                _active = true;
                _mode = mode;
                _phase = BlueprintPlacedInstanceTransformPhases.SelectTarget;
                _worldPairKey = context.WorldPairKey ?? string.Empty;
                _worldKey = context.WorldKey ?? string.Empty;
                _targetInstanceId = string.Empty;
                _targetInstanceName = string.Empty;
                _targetLayerOrder = 0;
                _grabOffsetX = 0;
                _grabOffsetY = 0;
                _lastInputOwner = "ui";
                _lastResultCode = string.Equals(mode, BlueprintPlacedInstanceTransformModes.Move, StringComparison.Ordinal)
                    ? "moveTargetSelectStarted"
                    : "mirrorTargetSelectStarted";
                _lastNotice = string.Equals(mode, BlueprintPlacedInstanceTransformModes.Move, StringComparison.Ordinal)
                    ? "请点击一个已放置蓝图作为移动目标。"
                    : "请点击一个已放置蓝图进行镜像；不安全 frame / 方向会自动拒绝。";
                return BlueprintPlacedInstanceTransformCommandResult.Create(true, true, _mode, _phase, _lastResultCode, _lastNotice, string.Empty, string.Empty);
            }
        }

        private static BlueprintPlacedInstanceTransformInteractionResult SelectOrMirrorTargetLocked(int tileX, int tileY)
        {
            BlueprintWorldInstanceSnapshot snapshot;
            var load = ResolveStoreLocked().TryLoadWorld(_worldPairKey, out snapshot);
            if (!load.Succeeded)
            {
                _lastInputOwner = "world";
                _lastResultCode = load.ResultCode;
                _lastNotice = "当前世界蓝图实例读取失败：" + load.Message;
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice);
            }

            BlueprintWorldInstanceRecord target;
            int relativeX;
            int relativeY;
            if (!TryFindTopmostInstanceAtWorldTile(snapshot == null ? null : snapshot.Instances, tileX, tileY, out target, out relativeX, out relativeY))
            {
                _lastInputOwner = "world";
                _lastResultCode = "noInstanceAtPointer";
                _lastNotice = "该位置没有可见已放置蓝图；请点击蓝图虚影覆盖的格子。";
                return BuildInteractionResultLocked(true, false, true, true, false, _lastResultCode, _lastNotice);
            }

            if (string.Equals(_mode, BlueprintPlacedInstanceTransformModes.Move, StringComparison.Ordinal))
            {
                _phase = BlueprintPlacedInstanceTransformPhases.PlaceTarget;
                _targetInstanceId = target.InstanceId ?? string.Empty;
                _targetInstanceName = target.Name ?? string.Empty;
                _targetLayerOrder = target.LayerOrder;
                _grabOffsetX = relativeX;
                _grabOffsetY = relativeY;
                _lastOriginTileX = target.OriginTileX;
                _lastOriginTileY = target.OriginTileY;
                _lastInputOwner = "world";
                _lastResultCode = "moveTargetSelected";
                _lastNotice = "已选择蓝图实例 " + GetInstanceName(target) + "；请点击新位置完成移动。";
                return BuildInteractionResultLocked(true, true, true, true, false, _lastResultCode, _lastNotice);
            }

            return ApplyMirrorLocked(snapshot, target);
        }

        private static BlueprintPlacedInstanceTransformInteractionResult ApplyMoveLocked(int tileX, int tileY)
        {
            BlueprintWorldInstanceSnapshot snapshot;
            var load = ResolveStoreLocked().TryLoadWorld(_worldPairKey, out snapshot);
            if (!load.Succeeded)
            {
                _lastInputOwner = "world";
                _lastResultCode = load.ResultCode;
                _lastNotice = "当前世界蓝图实例读取失败：" + load.Message;
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice);
            }

            var instances = CloneInstances(snapshot == null ? null : snapshot.Instances);
            var index = FindInstanceIndex(instances, _targetInstanceId);
            if (index < 0 || instances[index].Hidden)
            {
                ResetActiveTargetLocked();
                _lastInputOwner = "world";
                _lastResultCode = "targetUnavailable";
                _lastNotice = "移动目标已不存在或已隐藏，请重新选择。";
                return BuildInteractionResultLocked(false, true, true, _active, false, _lastResultCode, _lastNotice);
            }

            var target = instances[index];
            var newOriginX = tileX - _grabOffsetX;
            var newOriginY = tileY - _grabOffsetY;
            var changed = target.OriginTileX != newOriginX || target.OriginTileY != newOriginY;
            target.OriginTileX = newOriginX;
            target.OriginTileY = newOriginY;
            target.UpdatedUtc = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow);

            BlueprintWorldInstanceSnapshot saved;
            var save = ResolveStoreLocked().SaveWorldInstances(_worldPairKey, _worldKey, instances, out saved);
            if (!save.Succeeded)
            {
                _lastInputOwner = "world";
                _lastResultCode = save.ResultCode;
                _lastNotice = "蓝图实例移动保存失败：" + save.Message;
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice);
            }

            _lastInputOwner = "world";
            _lastResultCode = changed ? "moveApplied" : "moveUnchanged";
            _lastNotice = changed
                ? "蓝图实例 " + GetInstanceName(target) + " 已移动；模板、世界实物、擦除区域和层级未改。"
                : "蓝图实例位置未变化；模板和世界实物未改。";
            _lastOriginTileX = newOriginX;
            _lastOriginTileY = newOriginY;
            var targetId = target.InstanceId ?? string.Empty;
            var targetName = target.Name ?? string.Empty;
            _active = false;
            _mode = string.Empty;
            _phase = string.Empty;
            _targetInstanceId = targetId;
            _targetInstanceName = targetName;
            RefreshAfterInstanceMutation();
            return BuildInteractionResultLocked(true, changed, true, false, true, _lastResultCode, _lastNotice);
        }

        private static BlueprintPlacedInstanceTransformInteractionResult ApplyMirrorLocked(
            BlueprintWorldInstanceSnapshot snapshot,
            BlueprintWorldInstanceRecord target)
        {
            if (target == null)
            {
                _lastResultCode = "targetUnavailable";
                _lastNotice = "镜像目标不可用。";
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice);
            }

            if (HasAutoPlacementProgress(target))
            {
                _targetInstanceId = target.InstanceId ?? string.Empty;
                _targetInstanceName = target.Name ?? string.Empty;
                _targetLayerOrder = target.LayerOrder;
                _lastInputOwner = "world";
                _lastResultCode = "autoPlacementProgressActive";
                _lastNotice = "该蓝图实例已有自动摆放进度标记，禁止镜像实例副本。";
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice);
            }

            var mirror = BlueprintMirrorService.TryMirrorHorizontal(target.TemplateSnapshot);
            if (mirror == null || !mirror.Succeeded || mirror.Template == null)
            {
                _targetInstanceId = target.InstanceId ?? string.Empty;
                _targetInstanceName = target.Name ?? string.Empty;
                _targetLayerOrder = target.LayerOrder;
                _lastInputOwner = "world";
                _lastResultCode = mirror == null ? "mirrorUnknown" : mirror.ResultCode;
                _lastNotice = mirror == null ? "蓝图实例镜像结果未知。" : mirror.Message;
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice);
            }

            var instances = CloneInstances(snapshot == null ? null : snapshot.Instances);
            var index = FindInstanceIndex(instances, target.InstanceId);
            if (index < 0)
            {
                _lastResultCode = "targetUnavailable";
                _lastNotice = "镜像目标已不存在。";
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice);
            }

            var edited = instances[index];
            edited.TemplateSnapshot = mirror.Template.Clone();
            edited.UpdatedUtc = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow);
            BlueprintWorldInstanceSnapshot saved;
            var save = ResolveStoreLocked().SaveWorldInstances(_worldPairKey, _worldKey, instances, out saved);
            if (!save.Succeeded)
            {
                _lastInputOwner = "world";
                _lastResultCode = save.ResultCode;
                _lastNotice = "蓝图实例镜像保存失败：" + save.Message;
                return BuildInteractionResultLocked(false, false, true, true, false, _lastResultCode, _lastNotice);
            }

            _lastInputOwner = "world";
            _lastResultCode = mirror.ResultCode;
            _lastNotice = "蓝图实例 " + GetInstanceName(edited) + " 已镜像；只修改实例副本，模板库和世界实物未改。";
            _targetInstanceId = edited.InstanceId ?? string.Empty;
            _targetInstanceName = edited.Name ?? string.Empty;
            _targetLayerOrder = edited.LayerOrder;
            _lastOriginTileX = edited.OriginTileX;
            _lastOriginTileY = edited.OriginTileY;
            _active = false;
            _mode = string.Empty;
            _phase = string.Empty;
            RefreshAfterInstanceMutation();
            return BuildInteractionResultLocked(true, true, true, false, true, _lastResultCode, _lastNotice);
        }

        private static bool HasAutoPlacementProgress(BlueprintWorldInstanceRecord instance)
        {
            if (instance == null)
            {
                return false;
            }

            // Stage 07 introduces the marker before real auto-placement progress exists.
            // Only empty / idle is mirrorable; any future non-idle progress must fail closed.
            var state = (instance.AutoPlacementProgressState ?? string.Empty).Trim();
            return state.Length > 0 && !string.Equals(state, "idle", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryFindTopmostInstanceAtWorldTile(
            IReadOnlyList<BlueprintWorldInstanceRecord> instances,
            int tileX,
            int tileY,
            out BlueprintWorldInstanceRecord target,
            out int relativeX,
            out int relativeY)
        {
            target = null;
            relativeX = 0;
            relativeY = 0;
            for (var index = 0; instances != null && index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance == null || instance.Hidden)
                {
                    continue;
                }

                var relX = tileX - instance.OriginTileX;
                var relY = tileY - instance.OriginTileY;
                if (!InstanceHasVisibleCellAt(instance, relX, relY))
                {
                    continue;
                }

                if (target == null ||
                    instance.LayerOrder > target.LayerOrder ||
                    (instance.LayerOrder == target.LayerOrder &&
                     string.Compare(instance.InstanceId, target.InstanceId, StringComparison.Ordinal) > 0))
                {
                    target = instance.Clone();
                    relativeX = relX;
                    relativeY = relY;
                }
            }

            return target != null;
        }

        private static bool InstanceHasVisibleCellAt(BlueprintWorldInstanceRecord instance, int relativeX, int relativeY)
        {
            if (IsCellErased(instance == null ? null : instance.EraseMask, relativeX, relativeY))
            {
                return false;
            }

            var template = instance == null ? null : instance.TemplateSnapshot;
            for (var index = 0; template != null && template.Cells != null && index < template.Cells.Count; index++)
            {
                var cell = template.Cells[index];
                if (cell != null &&
                    cell.X == relativeX &&
                    cell.Y == relativeY &&
                    HasContentLayer(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasContentLayer(BlueprintCellRecord cell)
        {
            for (var index = 0; cell != null && cell.Layers != null && index < cell.Layers.Count; index++)
            {
                var layer = cell.Layers[index];
                if (layer != null && !string.IsNullOrWhiteSpace(layer.LayerKind))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCellErased(IReadOnlyList<BlueprintEraseMaskCellRecord> mask, int x, int y)
        {
            for (var index = 0; mask != null && index < mask.Count; index++)
            {
                var cell = mask[index];
                if (cell != null && cell.X == x && cell.Y == y)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasVisibleInstances(BlueprintWorldInstanceSnapshot snapshot)
        {
            for (var index = 0; snapshot != null && snapshot.Instances != null && index < snapshot.Instances.Count; index++)
            {
                var instance = snapshot.Instances[index];
                if (instance != null && !instance.Hidden)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<BlueprintWorldInstanceRecord> CloneInstances(IReadOnlyList<BlueprintWorldInstanceRecord> source)
        {
            var clone = new List<BlueprintWorldInstanceRecord>();
            for (var index = 0; source != null && index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    clone.Add(source[index].Clone());
                }
            }

            return clone;
        }

        private static int FindInstanceIndex(IList<BlueprintWorldInstanceRecord> instances, string instanceId)
        {
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(instanceId);
            for (var index = 0; instances != null && index < instances.Count; index++)
            {
                if (instances[index] != null &&
                    string.Equals(instances[index].InstanceId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void RefreshAfterInstanceMutation()
        {
            try
            {
                BlueprintProjectionService.RefreshAfterWorldInstancesChanged();
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "blueprint-transform-projection-refresh-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintPlacedInstanceTransformState",
                    "Blueprint transform projection refresh failed; the instance store mutation remains saved. " + error.Message);
            }

            try
            {
                BlueprintMaterialService.ForceRefreshForPlacedInstanceList();
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "blueprint-transform-material-refresh-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintPlacedInstanceTransformState",
                    "Blueprint transform material refresh failed; the instance store mutation remains saved. " + error.Message);
            }
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

        private static BlueprintPlacedInstanceTransformSnapshot BuildSnapshotLocked()
        {
            return new BlueprintPlacedInstanceTransformSnapshot
            {
                Active = _active,
                Mode = _mode,
                Phase = _phase,
                WorldPairKey = _worldPairKey,
                WorldKey = _worldKey,
                TargetInstanceId = _targetInstanceId,
                TargetInstanceName = _targetInstanceName,
                TargetLayerOrder = _targetLayerOrder,
                GrabOffsetX = _grabOffsetX,
                GrabOffsetY = _grabOffsetY,
                LastOriginTileX = _lastOriginTileX,
                LastOriginTileY = _lastOriginTileY,
                LastNotice = _lastNotice,
                LastInputOwner = _lastInputOwner,
                LastResultCode = _lastResultCode
            };
        }

        private static BlueprintPlacedInstanceTransformInteractionResult BuildInteractionResultLocked(
            bool succeeded,
            bool changed,
            bool consumeLeft,
            bool inputActive,
            bool completed,
            string resultCode,
            string message)
        {
            return new BlueprintPlacedInstanceTransformInteractionResult
            {
                Succeeded = succeeded,
                Changed = changed,
                ShouldConsumeLeftInput = consumeLeft,
                InputActive = inputActive,
                Completed = completed,
                Mode = _mode,
                Phase = _phase,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                TargetInstanceId = _targetInstanceId,
                TargetInstanceName = _targetInstanceName
            };
        }

        private static void ResetActiveTargetLocked()
        {
            _active = true;
            _phase = BlueprintPlacedInstanceTransformPhases.SelectTarget;
            _targetInstanceId = string.Empty;
            _targetInstanceName = string.Empty;
            _targetLayerOrder = 0;
            _grabOffsetX = 0;
            _grabOffsetY = 0;
        }

        private static void ResetStateLocked()
        {
            _active = false;
            _mode = string.Empty;
            _phase = string.Empty;
            _worldPairKey = string.Empty;
            _worldKey = string.Empty;
            _targetInstanceId = string.Empty;
            _targetInstanceName = string.Empty;
            _targetLayerOrder = 0;
            _grabOffsetX = 0;
            _grabOffsetY = 0;
            _lastOriginTileX = 0;
            _lastOriginTileY = 0;
            _lastNotice = "蓝图移动 / 镜像待命。";
            _lastInputOwner = string.Empty;
            _lastResultCode = "idle";
        }

        private static string NormalizeMode(string mode)
        {
            return string.Equals(mode, BlueprintPlacedInstanceTransformModes.Mirror, StringComparison.OrdinalIgnoreCase)
                ? BlueprintPlacedInstanceTransformModes.Mirror
                : BlueprintPlacedInstanceTransformModes.Move;
        }

        private static string GetInstanceName(BlueprintWorldInstanceRecord instance)
        {
            return instance == null || string.IsNullOrWhiteSpace(instance.Name)
                ? BlueprintStorageConstants.DefaultTemplateName
                : instance.Name.Trim();
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
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
