using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Records;

namespace JueMingZ.Automation.Blueprint
{
    internal sealed class BlueprintEraseRegionSnapshot
    {
        public BlueprintEraseRegionSnapshot()
        {
            TargetInstanceId = string.Empty;
            TargetInstanceName = string.Empty;
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            LastNotice = string.Empty;
            LastInputOwner = string.Empty;
            LastResultCode = string.Empty;
        }

        public bool Active { get; set; }
        public bool Dragging { get; set; }
        public bool HasFixedTarget { get; set; }
        public string TargetInstanceId { get; set; }
        public string TargetInstanceName { get; set; }
        public int TargetLayerOrder { get; set; }
        public string WorldPairKey { get; set; }
        public string WorldKey { get; set; }
        public int DragStartX { get; set; }
        public int DragStartY { get; set; }
        public int DragCurrentX { get; set; }
        public int DragCurrentY { get; set; }
        public bool HasHoverTile { get; set; }
        public int HoverTileX { get; set; }
        public int HoverTileY { get; set; }
        public int LastErasedCellCount { get; set; }
        public int TotalEraseCellCount { get; set; }
        public string LastNotice { get; set; }
        public string LastInputOwner { get; set; }
        public string LastResultCode { get; set; }
    }

    internal sealed class BlueprintEraseCommandResult
    {
        private BlueprintEraseCommandResult()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
            TargetInstanceId = string.Empty;
            TargetInstanceName = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public bool Changed { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public string TargetInstanceId { get; private set; }
        public string TargetInstanceName { get; private set; }

        public static BlueprintEraseCommandResult Create(
            bool succeeded,
            bool changed,
            string resultCode,
            string message,
            string targetInstanceId,
            string targetInstanceName)
        {
            return new BlueprintEraseCommandResult
            {
                Succeeded = succeeded,
                Changed = changed,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                TargetInstanceId = targetInstanceId ?? string.Empty,
                TargetInstanceName = targetInstanceName ?? string.Empty
            };
        }
    }

    internal sealed class BlueprintErasePointerInput
    {
        public bool UiOwned { get; set; }
        public bool WorldTileHit { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public bool LeftDown { get; set; }
        public bool LeftPressed { get; set; }
        public bool LeftReleased { get; set; }
    }

    internal sealed class BlueprintEraseInteractionResult
    {
        public bool Succeeded { get; set; }
        public bool Changed { get; set; }
        public bool ShouldConsumeLeftInput { get; set; }
        public bool InputActive { get; set; }
        public bool ErasedRegion { get; set; }
        public int ErasedCellCount { get; set; }
        public int TotalEraseCellCount { get; set; }
        public string TargetInstanceId { get; set; }
        public string TargetInstanceName { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
    }

    internal static class BlueprintEraseRegionState
    {
        // Erase mode mutates only the instance-local mask. Projection, material
        // statistics and future placement stages must consume that mask instead
        // of writing or deleting any Terraria world state.
        public const int MaxEraseCells = 16384;

        private static readonly object SyncRoot = new object();
        private static readonly object TestingSyncRoot = new object();
        private static BlueprintWorldInstanceStore _testingStore;
        private static BlueprintPlacementWorldContext _testingWorldContext;

        private static bool _active;
        private static bool _dragging;
        private static bool _hasFixedTarget;
        private static string _targetInstanceId = string.Empty;
        private static string _targetInstanceName = string.Empty;
        private static int _targetLayerOrder;
        private static string _worldPairKey = string.Empty;
        private static string _worldKey = string.Empty;
        private static int _dragStartX;
        private static int _dragStartY;
        private static int _dragCurrentX;
        private static int _dragCurrentY;
        private static bool _hasHoverTile;
        private static int _hoverTileX;
        private static int _hoverTileY;
        private static int _lastErasedCellCount;
        private static int _totalEraseCellCount;
        private static string _lastNotice = "擦除区域待命。";
        private static string _lastInputOwner = string.Empty;
        private static string _lastResultCode = "idle";

        public static BlueprintEraseRegionSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return BuildSnapshotLocked();
            }
        }

        public static BlueprintEraseRegionSnapshot GetDiagnostics()
        {
            return GetSnapshot();
        }

        public static BlueprintEraseCommandResult BeginErase(string preferredInstanceId)
        {
            var normalizedId = BlueprintTemplateLibraryStore.NormalizeId(preferredInstanceId);
            lock (SyncRoot)
            {
                var context = ResolveWorldContext();
                if (context == null || !context.Succeeded)
                {
                    ResetActiveLocked();
                    _lastResultCode = "worldIdentityUnavailable";
                    _lastNotice = "当前玩家-世界身份不可用，不能进入蓝图实例擦除模式：" +
                                  (context == null ? "unknown" : context.FailureReason);
                    return BlueprintEraseCommandResult.Create(false, false, _lastResultCode, _lastNotice, string.Empty, string.Empty);
                }

                var store = ResolveStore();
                BlueprintWorldInstanceSnapshot world;
                var load = store.TryLoadWorld(context.WorldPairKey, out world);
                if (!load.Succeeded)
                {
                    ResetActiveLocked();
                    _lastResultCode = load.ResultCode;
                    _lastNotice = "蓝图实例读取失败，不能进入擦除模式：" + load.Message;
                    return BlueprintEraseCommandResult.Create(false, false, _lastResultCode, _lastNotice, string.Empty, string.Empty);
                }

                BlueprintWorldInstanceRecord target = null;
                if (!string.IsNullOrEmpty(normalizedId))
                {
                    target = FindVisibleInstance(world == null ? null : world.Instances, normalizedId);
                    if (target == null)
                    {
                        ResetActiveLocked();
                        _lastResultCode = "missingTargetInstance";
                        _lastNotice = "当前选中的蓝图实例不存在或已隐藏，不能擦除。";
                        return BlueprintEraseCommandResult.Create(false, false, _lastResultCode, _lastNotice, normalizedId, string.Empty);
                    }
                }
                else if (!HasVisibleInstances(world == null ? null : world.Instances))
                {
                    ResetActiveLocked();
                    _lastResultCode = "noVisibleInstances";
                    _lastNotice = "当前世界没有可修改的可见蓝图实例。";
                    return BlueprintEraseCommandResult.Create(false, false, _lastResultCode, _lastNotice, string.Empty, string.Empty);
                }

                var changed = !_active ||
                              _dragging ||
                              !string.Equals(_targetInstanceId, normalizedId, StringComparison.OrdinalIgnoreCase);
                _active = true;
                _dragging = false;
                _worldPairKey = context.WorldPairKey ?? string.Empty;
                _worldKey = context.WorldKey ?? string.Empty;
                ApplyTargetLocked(target, target != null);
                _lastErasedCellCount = 0;
                _lastInputOwner = "ui";
                _lastResultCode = target == null ? "eraseStartedHoverTarget" : "eraseStartedSelectedTarget";
                _lastNotice = target == null
                    ? "正在修改已放置蓝图区域。按住左键拖选修剪，点击取消修改结束。"
                    : "正在修改已放置蓝图区域。目标实例 " + _targetInstanceName + "，点击取消修改结束。";
                return BlueprintEraseCommandResult.Create(true, changed, _lastResultCode, _lastNotice, _targetInstanceId, _targetInstanceName);
            }
        }

        public static BlueprintEraseCommandResult Cancel()
        {
            lock (SyncRoot)
            {
                var changed = _active || _dragging;
                ResetActiveLocked();
                _lastInputOwner = "ui";
                _lastResultCode = "eraseCancelled";
                _lastNotice = "已取消已放置蓝图区域修改。";
                return BlueprintEraseCommandResult.Create(true, changed, _lastResultCode, _lastNotice, string.Empty, string.Empty);
            }
        }

        public static BlueprintEraseInteractionResult HandlePointer(BlueprintErasePointerInput input)
        {
            input = input ?? new BlueprintErasePointerInput();
            lock (SyncRoot)
            {
                if (!_active)
                {
                    return BuildInteractionResultLocked(true, false, false, false, false, 0, "inactive", _lastNotice);
                }

                if (input.UiOwned)
                {
                    var changed = _dragging || _hasHoverTile;
                    _dragging = false;
                    ClearHoverLocked();
                    _lastInputOwner = "ui";
                    _lastResultCode = "uiOwned";
                    _lastNotice = "鼠标命中 UI；擦除区域未变化。";
                    return BuildInteractionResultLocked(true, changed, input.LeftDown || input.LeftPressed || input.LeftReleased, true, false, 0, _lastResultCode, _lastNotice);
                }

                if (input.LeftPressed)
                {
                    if (!input.WorldTileHit)
                    {
                        var changed = _dragging || _hasHoverTile;
                        _dragging = false;
                        ClearHoverLocked();
                        _lastInputOwner = "world-outside";
                        _lastResultCode = "worldMiss";
                        _lastNotice = "鼠标未命中有效世界格；擦除区域未变化。";
                        return BuildInteractionResultLocked(true, changed, true, true, false, 0, _lastResultCode, _lastNotice);
                    }

                    UpdateHoverLocked(input.TileX, input.TileY);
                    TargetResolution target;
                    if (!ResolveTargetForPointerLocked(input.TileX, input.TileY, out target))
                    {
                        _dragging = false;
                        _lastInputOwner = "world";
                        _lastResultCode = target == null ? "targetUnavailable" : target.ResultCode;
                        _lastNotice = target == null ? "没有可擦除的蓝图实例。" : target.Message;
                        return BuildInteractionResultLocked(false, false, true, true, false, 0, _lastResultCode, _lastNotice);
                    }

                    ApplyTargetLocked(target.Instance, target.FixedTarget);
                    _dragging = true;
                    _dragStartX = input.TileX;
                    _dragStartY = input.TileY;
                    _dragCurrentX = input.TileX;
                    _dragCurrentY = input.TileY;
                    _lastInputOwner = "world";
                    _lastResultCode = "dragStarted";
                    _lastNotice = "开始擦除蓝图实例 " + _targetInstanceName + " 的区域。";
                    return BuildInteractionResultLocked(true, true, true, true, false, 0, _lastResultCode, _lastNotice);
                }

                if (_dragging && input.LeftDown)
                {
                    if (input.WorldTileHit &&
                        (_dragCurrentX != input.TileX || _dragCurrentY != input.TileY))
                    {
                        _dragCurrentX = input.TileX;
                        _dragCurrentY = input.TileY;
                        UpdateHoverLocked(input.TileX, input.TileY);
                        _lastInputOwner = "world";
                        _lastResultCode = "dragUpdated";
                        _lastNotice = "正在拖选蓝图擦除区域。";
                        return BuildInteractionResultLocked(true, true, true, true, false, 0, _lastResultCode, _lastNotice);
                    }

                    return BuildInteractionResultLocked(true, false, true, true, false, 0, "dragHeld", _lastNotice);
                }

                if (_dragging && input.LeftReleased)
                {
                    if (!input.WorldTileHit)
                    {
                        _dragging = false;
                        _lastInputOwner = "world-outside";
                        _lastResultCode = "dragCancelled";
                        _lastNotice = "拖选释放点无效；擦除区域未变化。";
                        return BuildInteractionResultLocked(true, true, true, true, false, 0, _lastResultCode, _lastNotice);
                    }

                    _dragCurrentX = input.TileX;
                    _dragCurrentY = input.TileY;
                    UpdateHoverLocked(input.TileX, input.TileY);
                    return ApplyEraseRectangleLocked();
                }

                if (input.LeftDown || input.LeftReleased)
                {
                    _lastInputOwner = input.WorldTileHit ? "world" : "world-outside";
                    _lastResultCode = "heldIgnored";
                    _lastNotice = "等待新的左键按下后再擦除蓝图实例区域。";
                    return BuildInteractionResultLocked(true, false, true, true, false, 0, _lastResultCode, _lastNotice);
                }

                if (input.WorldTileHit)
                {
                    var hoverChanged = UpdateHoverLocked(input.TileX, input.TileY);
                    if (hoverChanged)
                    {
                        _lastInputOwner = "world-hover";
                        _lastResultCode = "hoverUpdated";
                        _lastNotice = "正在修改已放置蓝图区域。按住左键拖选修剪，点击取消修改结束。";
                    }

                    return BuildInteractionResultLocked(true, hoverChanged, false, true, false, 0, _lastResultCode, _lastNotice);
                }

                var clearedHover = _hasHoverTile;
                ClearHoverLocked();
                return BuildInteractionResultLocked(true, clearedHover, false, true, false, 0, "idle", _lastNotice);
            }
        }

        public static string BuildUiStateJson()
        {
            var snapshot = GetSnapshot();
            return "{" +
                   "\"active\":" + BoolRaw(snapshot.Active) + "," +
                   "\"dragging\":" + BoolRaw(snapshot.Dragging) + "," +
                   "\"hasFixedTarget\":" + BoolRaw(snapshot.HasFixedTarget) + "," +
                   "\"targetInstanceId\":\"" + EscapeJson(snapshot.TargetInstanceId) + "\"," +
                   "\"targetInstanceName\":\"" + EscapeJson(snapshot.TargetInstanceName) + "\"," +
                   "\"worldPairKey\":\"" + EscapeJson(snapshot.WorldPairKey) + "\"," +
                   "\"worldKey\":\"" + EscapeJson(snapshot.WorldKey) + "\"," +
                   "\"hasHoverTile\":" + BoolRaw(snapshot.HasHoverTile) + "," +
                   "\"hoverTileX\":" + IntRaw(snapshot.HoverTileX) + "," +
                   "\"hoverTileY\":" + IntRaw(snapshot.HoverTileY) + "," +
                   "\"lastErasedCellCount\":" + IntRaw(snapshot.LastErasedCellCount) + "," +
                   "\"totalEraseCellCount\":" + IntRaw(snapshot.TotalEraseCellCount) + "," +
                   "\"lastResultCode\":\"" + EscapeJson(snapshot.LastResultCode) + "\"," +
                   "\"lastInputOwner\":\"" + EscapeJson(snapshot.LastInputOwner) + "\"" +
                   "}";
        }

        public static int BuildStateSignature()
        {
            var snapshot = GetSnapshot();
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (snapshot.Active ? 1 : 0);
                hash = hash * 31 + (snapshot.Dragging ? 1 : 0);
                hash = hash * 31 + (snapshot.HasFixedTarget ? 1 : 0);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.TargetInstanceId ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.TargetInstanceName ?? string.Empty);
                hash = hash * 31 + snapshot.TargetLayerOrder;
                hash = hash * 31 + snapshot.DragStartX;
                hash = hash * 31 + snapshot.DragStartY;
                hash = hash * 31 + snapshot.DragCurrentX;
                hash = hash * 31 + snapshot.DragCurrentY;
                hash = hash * 31 + (snapshot.HasHoverTile ? 1 : 0);
                hash = hash * 31 + snapshot.HoverTileX;
                hash = hash * 31 + snapshot.HoverTileY;
                hash = hash * 31 + snapshot.LastErasedCellCount;
                hash = hash * 31 + snapshot.TotalEraseCellCount;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.LastResultCode ?? string.Empty);
                return hash;
            }
        }

        internal static void SetDependenciesForTesting(BlueprintWorldInstanceStore store, BlueprintPlacementWorldContext worldContext)
        {
            lock (TestingSyncRoot)
            {
                _testingStore = store;
                _testingWorldContext = worldContext;
            }
        }

        internal static void ResetForTesting()
        {
            lock (TestingSyncRoot)
            {
                _testingStore = null;
                _testingWorldContext = null;
            }

            lock (SyncRoot)
            {
                ResetAllLocked();
            }
        }

        private static BlueprintEraseInteractionResult ApplyEraseRectangleLocked()
        {
            var targetId = BlueprintTemplateLibraryStore.NormalizeId(_targetInstanceId);
            if (string.IsNullOrEmpty(targetId))
            {
                _dragging = false;
                _lastInputOwner = "world";
                _lastResultCode = "targetUnavailable";
                _lastNotice = "没有可擦除的蓝图实例目标。";
                return BuildInteractionResultLocked(false, true, true, true, false, 0, _lastResultCode, _lastNotice);
            }

            var minX = Math.Min(_dragStartX, _dragCurrentX);
            var maxX = Math.Max(_dragStartX, _dragCurrentX);
            var minY = Math.Min(_dragStartY, _dragCurrentY);
            var maxY = Math.Max(_dragStartY, _dragCurrentY);
            var width = maxX - minX + 1;
            var height = maxY - minY + 1;
            if ((long)width * height > MaxEraseCells)
            {
                _dragging = false;
                _lastInputOwner = "world";
                _lastResultCode = "eraseRegionTooLarge";
                _lastNotice = "擦除区域超过 " + MaxEraseCells.ToString(CultureInfo.InvariantCulture) + " 格，已拒绝。";
                return BuildInteractionResultLocked(false, true, true, true, false, 0, _lastResultCode, _lastNotice);
            }

            var context = ResolveWorldContext();
            if (context == null || !context.Succeeded)
            {
                _dragging = false;
                _lastResultCode = "worldIdentityUnavailable";
                _lastNotice = "当前玩家-世界身份不可用，擦除保存失败。";
                return BuildInteractionResultLocked(false, true, true, true, false, 0, _lastResultCode, _lastNotice);
            }

            var store = ResolveStore();
            BlueprintWorldInstanceSnapshot world;
            var load = store.TryLoadWorld(context.WorldPairKey, out world);
            if (!load.Succeeded)
            {
                _dragging = false;
                _lastResultCode = load.ResultCode;
                _lastNotice = "蓝图实例读取失败，擦除保存失败：" + load.Message;
                return BuildInteractionResultLocked(false, true, true, true, false, 0, _lastResultCode, _lastNotice);
            }

            var instances = CloneInstances(world == null ? null : world.Instances);
            var index = FindInstanceIndex(instances, targetId);
            if (index < 0 || instances[index] == null || instances[index].Hidden)
            {
                _dragging = false;
                _lastResultCode = "missingTargetInstance";
                _lastNotice = "蓝图擦除目标不存在或已隐藏。";
                return BuildInteractionResultLocked(false, true, true, true, false, 0, _lastResultCode, _lastNotice);
            }

            var target = instances[index];
            var added = AddEraseMaskCells(target, minX, maxX, minY, maxY);
            _dragging = false;
            _lastInputOwner = "world";
            _lastErasedCellCount = added;
            _totalEraseCellCount = target.EraseMask == null ? 0 : target.EraseMask.Count;
            if (added <= 0)
            {
                ApplyTargetLocked(target, true);
                _lastResultCode = "eraseUnchanged";
                _lastNotice = "擦除区域未命中该实例的未擦除蓝图内容。";
                return BuildInteractionResultLocked(true, true, true, true, false, 0, _lastResultCode, _lastNotice);
            }

            target.UpdatedUtc = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow);
            BlueprintWorldInstanceSnapshot saved;
            var save = store.SaveWorldInstances(context.WorldPairKey, context.WorldKey, instances, out saved);
            if (!save.Succeeded)
            {
                _lastResultCode = save.ResultCode;
                _lastNotice = "蓝图实例擦除 mask 保存失败：" + save.Message;
                return BuildInteractionResultLocked(false, true, true, true, false, 0, _lastResultCode, _lastNotice);
            }

            var savedTarget = FindVisibleInstance(saved == null ? null : saved.Instances, targetId) ?? target;
            ApplyTargetLocked(savedTarget, true);
            _lastResultCode = "erased";
            _lastNotice = "已擦除实例 " + _targetInstanceName + " 的 " + added.ToString(CultureInfo.InvariantCulture) +
                          " 个蓝图单元；可继续拖选修改，模板和世界内容未修改。";
            return BuildInteractionResultLocked(true, true, true, true, true, added, _lastResultCode, _lastNotice);
        }

        private static int AddEraseMaskCells(BlueprintWorldInstanceRecord target, int minWorldX, int maxWorldX, int minWorldY, int maxWorldY)
        {
            if (target == null)
            {
                return 0;
            }

            if (target.EraseMask == null)
            {
                target.EraseMask = new List<BlueprintEraseMaskCellRecord>();
            }

            var existing = BuildEraseMaskSet(target.EraseMask);
            var added = 0;
            var cells = target.TemplateSnapshot == null ? null : target.TemplateSnapshot.Cells;
            for (var index = 0; cells != null && index < cells.Count; index++)
            {
                var cell = cells[index];
                if (!HasContentLayer(cell))
                {
                    continue;
                }

                var worldX = target.OriginTileX + cell.X;
                var worldY = target.OriginTileY + cell.Y;
                if (worldX < minWorldX || worldX > maxWorldX || worldY < minWorldY || worldY > maxWorldY)
                {
                    continue;
                }

                var key = BuildKey(cell.X, cell.Y);
                if (!existing.Add(key))
                {
                    continue;
                }

                target.EraseMask.Add(new BlueprintEraseMaskCellRecord { X = cell.X, Y = cell.Y });
                added++;
            }

            return added;
        }

        private static bool ResolveTargetForPointerLocked(int tileX, int tileY, out TargetResolution resolution)
        {
            resolution = null;
            var context = ResolveWorldContext();
            if (context == null || !context.Succeeded)
            {
                resolution = TargetResolution.Failure("worldIdentityUnavailable", "当前玩家-世界身份不可用，不能选择擦除目标。");
                return false;
            }

            var store = ResolveStore();
            BlueprintWorldInstanceSnapshot world;
            var load = store.TryLoadWorld(context.WorldPairKey, out world);
            if (!load.Succeeded)
            {
                resolution = TargetResolution.Failure(load.ResultCode, "蓝图实例读取失败：" + load.Message);
                return false;
            }

            _worldPairKey = context.WorldPairKey ?? string.Empty;
            _worldKey = context.WorldKey ?? string.Empty;
            var instances = world == null ? null : world.Instances;
            if (!string.IsNullOrEmpty(_targetInstanceId))
            {
                var fixedTarget = FindVisibleInstance(instances, _targetInstanceId);
                if (fixedTarget == null)
                {
                    resolution = TargetResolution.Failure("missingTargetInstance", "当前擦除目标不存在或已隐藏。");
                    return false;
                }

                resolution = TargetResolution.Success(fixedTarget, true);
                return true;
            }

            var target = FindTopmostInstanceAtWorldTile(instances, tileX, tileY);
            if (target == null)
            {
                resolution = TargetResolution.Failure("noInstanceAtTile", "鼠标位置没有可擦除的可见蓝图实例内容。");
                return false;
            }

            resolution = TargetResolution.Success(target, false);
            return true;
        }

        private static BlueprintWorldInstanceStore ResolveStore()
        {
            lock (TestingSyncRoot)
            {
                if (_testingStore != null)
                {
                    return _testingStore;
                }
            }

            return new BlueprintWorldInstanceStore();
        }

        private static BlueprintPlacementWorldContext ResolveWorldContext()
        {
            lock (TestingSyncRoot)
            {
                if (_testingWorldContext != null)
                {
                    return _testingWorldContext;
                }
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

        private static BlueprintWorldInstanceRecord FindTopmostInstanceAtWorldTile(
            IReadOnlyList<BlueprintWorldInstanceRecord> instances,
            int tileX,
            int tileY)
        {
            BlueprintWorldInstanceRecord best = null;
            for (var index = 0; instances != null && index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance == null || instance.Hidden || !InstanceHasVisibleCellAtWorldTile(instance, tileX, tileY))
                {
                    continue;
                }

                if (best == null ||
                    instance.LayerOrder > best.LayerOrder ||
                    (instance.LayerOrder == best.LayerOrder &&
                     string.Compare(instance.InstanceId, best.InstanceId, StringComparison.Ordinal) > 0))
                {
                    best = instance;
                }
            }

            return best == null ? null : best.Clone();
        }

        private static bool InstanceHasVisibleCellAtWorldTile(BlueprintWorldInstanceRecord instance, int tileX, int tileY)
        {
            if (instance == null)
            {
                return false;
            }

            var relativeX = tileX - instance.OriginTileX;
            var relativeY = tileY - instance.OriginTileY;
            var eraseMask = BuildEraseMaskSet(instance.EraseMask);
            if (eraseMask.Contains(BuildKey(relativeX, relativeY)))
            {
                return false;
            }

            var cells = instance.TemplateSnapshot == null ? null : instance.TemplateSnapshot.Cells;
            for (var index = 0; cells != null && index < cells.Count; index++)
            {
                var cell = cells[index];
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

        private static bool HasVisibleInstances(IReadOnlyList<BlueprintWorldInstanceRecord> instances)
        {
            for (var index = 0; instances != null && index < instances.Count; index++)
            {
                if (instances[index] != null && !instances[index].Hidden)
                {
                    return true;
                }
            }

            return false;
        }

        private static BlueprintWorldInstanceRecord FindVisibleInstance(
            IReadOnlyList<BlueprintWorldInstanceRecord> instances,
            string instanceId)
        {
            var index = FindInstanceIndex(instances, instanceId);
            if (index < 0 || instances[index] == null || instances[index].Hidden)
            {
                return null;
            }

            return instances[index].Clone();
        }

        private static int FindInstanceIndex(IReadOnlyList<BlueprintWorldInstanceRecord> instances, string instanceId)
        {
            var id = BlueprintTemplateLibraryStore.NormalizeId(instanceId);
            if (string.IsNullOrEmpty(id) || instances == null)
            {
                return -1;
            }

            for (var index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance != null && string.Equals(instance.InstanceId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
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

        private static bool HasContentLayer(BlueprintCellRecord cell)
        {
            if (cell == null || cell.Layers == null)
            {
                return false;
            }

            for (var index = 0; index < cell.Layers.Count; index++)
            {
                var layer = cell.Layers[index];
                if (layer != null && !string.IsNullOrWhiteSpace(layer.LayerKind))
                {
                    return true;
                }
            }

            return false;
        }

        internal static HashSet<string> BuildEraseMaskSet(IList<BlueprintEraseMaskCellRecord> mask)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; mask != null && index < mask.Count; index++)
            {
                var cell = mask[index];
                if (cell != null)
                {
                    set.Add(BuildKey(cell.X, cell.Y));
                }
            }

            return set;
        }

        internal static bool IsCellErased(IList<BlueprintEraseMaskCellRecord> mask, int relativeX, int relativeY)
        {
            if (mask == null || mask.Count <= 0)
            {
                return false;
            }

            var key = BuildKey(relativeX, relativeY);
            for (var index = 0; index < mask.Count; index++)
            {
                var cell = mask[index];
                if (cell != null && string.Equals(BuildKey(cell.X, cell.Y), key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyTargetLocked(BlueprintWorldInstanceRecord target, bool fixedTarget)
        {
            if (target == null)
            {
                _hasFixedTarget = false;
                _targetInstanceId = string.Empty;
                _targetInstanceName = string.Empty;
                _targetLayerOrder = 0;
                _totalEraseCellCount = 0;
                return;
            }

            _hasFixedTarget = fixedTarget;
            _targetInstanceId = target.InstanceId ?? string.Empty;
            _targetInstanceName = string.IsNullOrWhiteSpace(target.Name) ? BlueprintStorageConstants.DefaultTemplateName : target.Name.Trim();
            _targetLayerOrder = target.LayerOrder;
            _totalEraseCellCount = target.EraseMask == null ? 0 : target.EraseMask.Count;
        }

        private static BlueprintEraseRegionSnapshot BuildSnapshotLocked()
        {
            return new BlueprintEraseRegionSnapshot
            {
                Active = _active,
                Dragging = _dragging,
                HasFixedTarget = _hasFixedTarget,
                TargetInstanceId = _targetInstanceId,
                TargetInstanceName = _targetInstanceName,
                TargetLayerOrder = _targetLayerOrder,
                WorldPairKey = _worldPairKey,
                WorldKey = _worldKey,
                DragStartX = _dragStartX,
                DragStartY = _dragStartY,
                DragCurrentX = _dragCurrentX,
                DragCurrentY = _dragCurrentY,
                HasHoverTile = _hasHoverTile,
                HoverTileX = _hoverTileX,
                HoverTileY = _hoverTileY,
                LastErasedCellCount = _lastErasedCellCount,
                TotalEraseCellCount = _totalEraseCellCount,
                LastNotice = _lastNotice,
                LastInputOwner = _lastInputOwner,
                LastResultCode = _lastResultCode
            };
        }

        private static BlueprintEraseInteractionResult BuildInteractionResultLocked(
            bool succeeded,
            bool changed,
            bool shouldConsumeLeftInput,
            bool inputActive,
            bool erasedRegion,
            int erasedCellCount,
            string resultCode,
            string message)
        {
            return new BlueprintEraseInteractionResult
            {
                Succeeded = succeeded,
                Changed = changed,
                ShouldConsumeLeftInput = shouldConsumeLeftInput,
                InputActive = inputActive,
                ErasedRegion = erasedRegion,
                ErasedCellCount = erasedCellCount,
                TotalEraseCellCount = _totalEraseCellCount,
                TargetInstanceId = _targetInstanceId,
                TargetInstanceName = _targetInstanceName,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty
            };
        }

        private static void ResetActiveLocked()
        {
            _active = false;
            _dragging = false;
            _hasFixedTarget = false;
            _targetInstanceId = string.Empty;
            _targetInstanceName = string.Empty;
            _targetLayerOrder = 0;
            _dragStartX = 0;
            _dragStartY = 0;
            _dragCurrentX = 0;
            _dragCurrentY = 0;
            ClearHoverLocked();
            _lastErasedCellCount = 0;
            _totalEraseCellCount = 0;
        }

        private static bool UpdateHoverLocked(int tileX, int tileY)
        {
            var changed = !_hasHoverTile || _hoverTileX != tileX || _hoverTileY != tileY;
            _hasHoverTile = true;
            _hoverTileX = tileX;
            _hoverTileY = tileY;
            return changed;
        }

        private static void ClearHoverLocked()
        {
            _hasHoverTile = false;
            _hoverTileX = 0;
            _hoverTileY = 0;
        }

        private static void ResetAllLocked()
        {
            ResetActiveLocked();
            _worldPairKey = string.Empty;
            _worldKey = string.Empty;
            _lastNotice = "擦除区域待命。";
            _lastInputOwner = string.Empty;
            _lastResultCode = "idle";
        }

        private static string BuildKey(int x, int y)
        {
            return x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture);
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

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class TargetResolution
        {
            private TargetResolution()
            {
                ResultCode = string.Empty;
                Message = string.Empty;
            }

            public BlueprintWorldInstanceRecord Instance { get; private set; }
            public bool FixedTarget { get; private set; }
            public string ResultCode { get; private set; }
            public string Message { get; private set; }

            public static TargetResolution Success(BlueprintWorldInstanceRecord instance, bool fixedTarget)
            {
                return new TargetResolution
                {
                    Instance = instance == null ? null : instance.Clone(),
                    FixedTarget = fixedTarget,
                    ResultCode = "targetResolved",
                    Message = "擦除目标已选择。"
                };
            }

            public static TargetResolution Failure(string resultCode, string message)
            {
                return new TargetResolution
                {
                    Instance = null,
                    FixedTarget = false,
                    ResultCode = resultCode ?? string.Empty,
                    Message = message ?? string.Empty
                };
            }
        }
    }
}
