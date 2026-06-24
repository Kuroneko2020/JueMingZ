using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using JueMingZ.Records;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintProjectionLayerStatuses
    {
        public const string Fulfilled = "fulfilled";
        public const string Completed = "completed";
        public const string Missing = "missing";
        public const string Conflict = "conflict";
        public const string Covered = "covered";
        public const string Unavailable = "unavailable";
    }

    internal sealed class BlueprintProjectionCellSnapshot
    {
        public BlueprintProjectionCellSnapshot()
        {
            InstanceId = string.Empty;
            InstanceName = string.Empty;
            LayerKind = string.Empty;
            CoverageGroup = string.Empty;
            Status = string.Empty;
            MaterialDisplayName = string.Empty;
        }

        public string InstanceId { get; set; }
        public string InstanceName { get; set; }
        public int LayerOrder { get; set; }
        public int WorldTileX { get; set; }
        public int WorldTileY { get; set; }
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
        public string LayerKind { get; set; }
        public string CoverageGroup { get; set; }
        public int ContentId { get; set; }
        public int Style { get; set; }
        public int FrameX { get; set; }
        public int FrameY { get; set; }
        public int PaintId { get; set; }
        public int CoatingFlags { get; set; }
        public int Slope { get; set; }
        public bool HalfBrick { get; set; }
        public bool Inactive { get; set; }
        public int MaterialItemId { get; set; }
        public int MaterialStack { get; set; }
        public string MaterialDisplayName { get; set; }
        public string Status { get; set; }
    }

    internal sealed class BlueprintProjectionSnapshot
    {
        public BlueprintProjectionSnapshot()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            Signature = string.Empty;
            ProjectedLayers = new List<BlueprintProjectionCellSnapshot>();
            AllProjectedLayers = new List<BlueprintProjectionCellSnapshot>();
            LastResolvedUtc = DateTime.UtcNow;
        }

        public bool LoadSucceeded { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public string WorldPairKey { get; set; }
        public string WorldKey { get; set; }
        public string Signature { get; set; }
        public int Revision { get; set; }
        public int InstanceCount { get; set; }
        public int VisibleInstanceCount { get; set; }
        public int HiddenInstanceCount { get; set; }
        public int EffectiveLayerCount { get; set; }
        public int FulfilledLayerCount { get; set; }
        public int CompletedLayerCount { get; set; }
        public int MissingLayerCount { get; set; }
        public int ConflictLayerCount { get; set; }
        public int CoveredLayerCount { get; set; }
        public int ErasedLayerCount { get; set; }
        public int UnavailableLayerCount { get; set; }
        public int CacheHitCount { get; set; }
        public int CacheMissCount { get; set; }
        public double LastResolveElapsedMs { get; set; }
        public DateTime? LastResolvedUtc { get; set; }
        public IReadOnlyList<BlueprintProjectionCellSnapshot> ProjectedLayers { get; set; }
        public IReadOnlyList<BlueprintProjectionCellSnapshot> AllProjectedLayers { get; set; }

        public BlueprintProjectionSnapshot Clone()
        {
            return Clone(true);
        }

        public BlueprintProjectionSnapshot CloneSummary()
        {
            return Clone(false);
        }

        private BlueprintProjectionSnapshot Clone(bool includeLayers)
        {
            var clone = new BlueprintProjectionSnapshot
            {
                LoadSucceeded = LoadSucceeded,
                ResultCode = ResultCode ?? string.Empty,
                Message = Message ?? string.Empty,
                WorldPairKey = WorldPairKey ?? string.Empty,
                WorldKey = WorldKey ?? string.Empty,
                Signature = Signature ?? string.Empty,
                Revision = Revision,
                InstanceCount = InstanceCount,
                VisibleInstanceCount = VisibleInstanceCount,
                HiddenInstanceCount = HiddenInstanceCount,
                EffectiveLayerCount = EffectiveLayerCount,
                FulfilledLayerCount = FulfilledLayerCount,
                CompletedLayerCount = CompletedLayerCount,
                MissingLayerCount = MissingLayerCount,
                ConflictLayerCount = ConflictLayerCount,
                CoveredLayerCount = CoveredLayerCount,
                ErasedLayerCount = ErasedLayerCount,
                UnavailableLayerCount = UnavailableLayerCount,
                CacheHitCount = CacheHitCount,
                CacheMissCount = CacheMissCount,
                LastResolveElapsedMs = LastResolveElapsedMs,
                LastResolvedUtc = LastResolvedUtc,
                ProjectedLayers = includeLayers ? CloneProjectedLayers(ProjectedLayers) : new List<BlueprintProjectionCellSnapshot>(),
                AllProjectedLayers = includeLayers ? CloneProjectedLayers(AllProjectedLayers) : new List<BlueprintProjectionCellSnapshot>()
            };
            return clone;
        }

        private static IReadOnlyList<BlueprintProjectionCellSnapshot> CloneProjectedLayers(IReadOnlyList<BlueprintProjectionCellSnapshot> source)
        {
            var clone = new List<BlueprintProjectionCellSnapshot>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var layer = source[index];
                if (layer == null)
                {
                    continue;
                }

                clone.Add(new BlueprintProjectionCellSnapshot
                {
                    InstanceId = layer.InstanceId ?? string.Empty,
                    InstanceName = layer.InstanceName ?? string.Empty,
                    LayerOrder = layer.LayerOrder,
                    WorldTileX = layer.WorldTileX,
                    WorldTileY = layer.WorldTileY,
                    RelativeX = layer.RelativeX,
                    RelativeY = layer.RelativeY,
                    LayerKind = layer.LayerKind ?? string.Empty,
                    CoverageGroup = layer.CoverageGroup ?? string.Empty,
                    ContentId = layer.ContentId,
                    Style = layer.Style,
                    FrameX = layer.FrameX,
                    FrameY = layer.FrameY,
                    PaintId = layer.PaintId,
                    CoatingFlags = layer.CoatingFlags,
                    Slope = layer.Slope,
                    HalfBrick = layer.HalfBrick,
                    Inactive = layer.Inactive,
                    MaterialItemId = layer.MaterialItemId,
                    MaterialStack = layer.MaterialStack,
                    MaterialDisplayName = layer.MaterialDisplayName ?? string.Empty,
                    Status = layer.Status ?? string.Empty
                });
            }

            return clone;
        }
    }

    internal static class BlueprintProjectionService
    {
        private const int CacheCadenceMs = 250;
        private const int MaxProjectedLayersForOverlay = 2048;
        private static readonly object SyncRoot = new object();
        private static BlueprintWorldInstanceStore _testingStore;
        private static BlueprintPlacementWorldContext _testingWorldContext;
        private static IBlueprintWorldTileReader _testingReader;
        private static BlueprintProjectionSnapshot _lastSnapshot;
        private static string _lastSignature = string.Empty;
        private static DateTime _lastResolveUtc = DateTime.MinValue;
        private static int _cacheHitCount;
        private static int _cacheMissCount;

        public static BlueprintProjectionSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return ResolveSnapshotLocked(false).Clone();
            }
        }

        public static BlueprintProjectionSnapshot GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return CloneLastSnapshotSummaryLocked();
            }
        }

        internal static BlueprintProjectionSnapshot ForceRefreshForAutoPlacement()
        {
            lock (SyncRoot)
            {
                return ResolveSnapshotLocked(true).Clone();
            }
        }

        internal static BlueprintProjectionSnapshot RefreshAfterWorldInstancesChanged()
        {
            lock (SyncRoot)
            {
                // Instance writes are the explicit mutation boundary allowed to
                // refresh projection data; draw and hit-test paths stay cache-only.
                return ResolveSnapshotLocked(true).CloneSummary();
            }
        }

        internal static BlueprintProjectionSnapshot GetCachedSnapshotForDraw()
        {
            lock (SyncRoot)
            {
                // Draw paths must not refresh or clone full projection layers. The
                // cached snapshot is read-only by overlay contract.
                return _lastSnapshot;
            }
        }

        public static string BuildUiStateJson()
        {
            var snapshot = GetDiagnostics();
            return "{" +
                   "\"loadSucceeded\":" + BoolRaw(snapshot.LoadSucceeded) + "," +
                   "\"resultCode\":\"" + EscapeJson(snapshot.ResultCode) + "\"," +
                   "\"worldPairKey\":\"" + EscapeJson(snapshot.WorldPairKey) + "\"," +
                   "\"worldKey\":\"" + EscapeJson(snapshot.WorldKey) + "\"," +
                   "\"instanceCount\":" + snapshot.InstanceCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"visibleInstanceCount\":" + snapshot.VisibleInstanceCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"hiddenInstanceCount\":" + snapshot.HiddenInstanceCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"effectiveLayerCount\":" + snapshot.EffectiveLayerCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"fulfilledLayerCount\":" + snapshot.FulfilledLayerCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"completedLayerCount\":" + snapshot.CompletedLayerCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"missingLayerCount\":" + snapshot.MissingLayerCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"conflictLayerCount\":" + snapshot.ConflictLayerCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"coveredLayerCount\":" + snapshot.CoveredLayerCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"erasedLayerCount\":" + snapshot.ErasedLayerCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"unavailableLayerCount\":" + snapshot.UnavailableLayerCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"cacheHitCount\":" + snapshot.CacheHitCount.ToString(CultureInfo.InvariantCulture) + "," +
                   "\"cacheMissCount\":" + snapshot.CacheMissCount.ToString(CultureInfo.InvariantCulture) +
                   "}";
        }

        public static int BuildStateSignature()
        {
            var snapshot = GetDiagnostics();
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + snapshot.Revision;
                hash = hash * 31 + snapshot.InstanceCount;
                hash = hash * 31 + snapshot.VisibleInstanceCount;
                hash = hash * 31 + snapshot.HiddenInstanceCount;
                hash = hash * 31 + snapshot.EffectiveLayerCount;
                hash = hash * 31 + snapshot.FulfilledLayerCount;
                hash = hash * 31 + snapshot.CompletedLayerCount;
                hash = hash * 31 + snapshot.MissingLayerCount;
                hash = hash * 31 + snapshot.ConflictLayerCount;
                hash = hash * 31 + snapshot.CoveredLayerCount;
                hash = hash * 31 + snapshot.ErasedLayerCount;
                hash = hash * 31 + snapshot.UnavailableLayerCount;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.ResultCode ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.Signature ?? string.Empty);
                return hash;
            }
        }

        internal static void SetDependenciesForTesting(
            BlueprintWorldInstanceStore store,
            BlueprintPlacementWorldContext worldContext,
            IBlueprintWorldTileReader reader,
            bool reload)
        {
            lock (SyncRoot)
            {
                _testingStore = store;
                _testingWorldContext = worldContext;
                _testingReader = reader;
                if (reload)
                {
                    ResetCacheLocked();
                }
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _testingStore = null;
                _testingWorldContext = null;
                _testingReader = null;
                ResetCacheLocked();
            }
        }

        internal static IReadOnlyList<BlueprintProjectionCellSnapshot> GetProjectedLayersForTesting()
        {
            return GetSnapshot().ProjectedLayers;
        }

        private static BlueprintProjectionSnapshot ResolveSnapshotLocked(bool forceRefresh)
        {
            var context = ResolveWorldContextLocked();
            if (context == null || !context.Succeeded)
            {
                return SaveFailureLocked(
                    "worldUnavailable",
                    "当前玩家/世界 identity 不可用：" + (context == null ? "identityUnavailable" : context.FailureReason),
                    string.Empty,
                    string.Empty);
            }

            var store = ResolveStoreLocked();
            BlueprintWorldInstanceSnapshot worldSnapshot;
            var load = store.TryLoadWorld(context.WorldPairKey, out worldSnapshot);
            if (!load.Succeeded)
            {
                return SaveFailureLocked("instanceLoadFailed", "蓝图实例读取失败：" + load.Message, context.WorldPairKey, context.WorldKey);
            }

            var instances = worldSnapshot == null || worldSnapshot.Instances == null
                ? new List<BlueprintWorldInstanceRecord>()
                : new List<BlueprintWorldInstanceRecord>(worldSnapshot.Instances);
            var reader = ResolveReaderLocked();
            var signature = BuildProjectionSignature(context, worldSnapshot, instances, reader);
            var now = DateTime.UtcNow;
            if (!forceRefresh &&
                _lastSnapshot != null &&
                string.Equals(_lastSignature, signature, StringComparison.Ordinal) &&
                (now - _lastResolveUtc).TotalMilliseconds < CacheCadenceMs)
            {
                _cacheHitCount++;
                _lastSnapshot.CacheHitCount = _cacheHitCount;
                return _lastSnapshot;
            }

            _cacheMissCount++;
            var watch = Stopwatch.StartNew();
            var result = BuildProjectionSnapshot(context, worldSnapshot, instances, reader, signature);
            PersistCompletedProgressIfNeeded(store, context, instances, result.AllProjectedLayers);
            watch.Stop();
            result.LastResolveElapsedMs = watch.Elapsed.TotalMilliseconds;
            result.LastResolvedUtc = now;
            result.CacheHitCount = _cacheHitCount;
            result.CacheMissCount = _cacheMissCount;
            _lastSignature = signature;
            _lastResolveUtc = now;
            _lastSnapshot = result;
            BlueprintDiagnostics.RecordProjectionResolve(result);
            return _lastSnapshot;
        }

        private static BlueprintProjectionSnapshot BuildProjectionSnapshot(
            BlueprintPlacementWorldContext context,
            BlueprintWorldInstanceSnapshot worldSnapshot,
            IList<BlueprintWorldInstanceRecord> instances,
            IBlueprintWorldTileReader reader,
            string signature)
        {
            var snapshot = new BlueprintProjectionSnapshot
            {
                LoadSucceeded = true,
                ResultCode = "resolved",
                Message = "蓝图投影已解析。",
                WorldPairKey = context.WorldPairKey ?? string.Empty,
                WorldKey = context.WorldKey ?? string.Empty,
                Signature = signature ?? string.Empty,
                Revision = worldSnapshot == null ? 0 : worldSnapshot.Revision,
                InstanceCount = instances == null ? 0 : instances.Count
            };

            if (reader == null || !reader.IsWorldReady)
            {
                snapshot.LoadSucceeded = false;
                snapshot.ResultCode = "worldTileReaderUnavailable";
                snapshot.Message = "当前世界 tile 读取不可用，无法判定蓝图完成度。";
                return snapshot;
            }

            var effective = new Dictionary<string, BlueprintProjectionCandidate>(StringComparer.Ordinal);
            var order = new List<string>();
            for (var index = 0; instances != null && index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance == null)
                {
                    continue;
                }

                if (instance.Hidden)
                {
                    snapshot.HiddenInstanceCount++;
                    continue;
                }

                snapshot.VisibleInstanceCount++;
                var template = instance.TemplateSnapshot;
                var cells = template == null ? null : template.Cells;
                for (var cellIndex = 0; cells != null && cellIndex < cells.Count; cellIndex++)
                {
                    var cell = cells[cellIndex];
                    if (cell == null || cell.Layers == null || cell.Layers.Count <= 0)
                    {
                        continue;
                    }

                    for (var layerIndex = 0; layerIndex < cell.Layers.Count; layerIndex++)
                    {
                        var layer = cell.Layers[layerIndex];
                        if (layer == null || string.IsNullOrWhiteSpace(layer.LayerKind))
                        {
                            continue;
                        }

                        if (BlueprintEraseRegionState.IsCellErased(instance.EraseMask, cell.X, cell.Y))
                        {
                            snapshot.ErasedLayerCount++;
                            continue;
                        }

                        var candidate = BlueprintProjectionCandidate.Create(instance, cell, layer);
                        var key = candidate.BuildCoverageKey();
                        if (effective.ContainsKey(key))
                        {
                            // Overlap is resolved only in this transient
                            // projection map: the later / higher layer wins for
                            // display and automation, while both instance
                            // snapshots remain complete on disk.
                            snapshot.CoveredLayerCount++;
                            effective[key] = candidate;
                            continue;
                        }

                        effective[key] = candidate;
                        order.Add(key);
                    }
                }
            }

            var projected = new List<BlueprintProjectionCellSnapshot>();
            var allProjected = new List<BlueprintProjectionCellSnapshot>();
            var replacementSettings = BlueprintReplacementRuleService.GetSettingsFromCurrentConfig();
            var completedKeysByInstance = BuildCompletedKeySets(instances);
            for (var index = 0; index < order.Count; index++)
            {
                var key = order[index];
                BlueprintProjectionCandidate candidate;
                if (!effective.TryGetValue(key, out candidate) || candidate == null)
                {
                    continue;
                }

                var projectedLayer = candidate.ToSnapshot();
                projectedLayer.Status = IsCandidateCompleted(candidate, completedKeysByInstance)
                    ? BlueprintProjectionLayerStatuses.Completed
                    : ClassifyLayer(candidate, reader, replacementSettings);
                CountStatus(snapshot, projectedLayer.Status);
                allProjected.Add(projectedLayer);
                if (projected.Count < MaxProjectedLayersForOverlay)
                {
                    projected.Add(projectedLayer);
                }
            }

            snapshot.EffectiveLayerCount = order.Count;
            snapshot.ProjectedLayers = projected;
            snapshot.AllProjectedLayers = allProjected;
            if (snapshot.InstanceCount <= 0)
            {
                snapshot.ResultCode = "empty";
                snapshot.Message = "当前世界没有已放置蓝图实例。";
            }
            else if (snapshot.VisibleInstanceCount <= 0)
            {
                snapshot.ResultCode = "allHidden";
                snapshot.Message = "当前世界蓝图实例均已隐藏，投影跳过。";
            }

            return snapshot;
        }

        private static void PersistCompletedProgressIfNeeded(
            BlueprintWorldInstanceStore store,
            BlueprintPlacementWorldContext context,
            IList<BlueprintWorldInstanceRecord> instances,
            IReadOnlyList<BlueprintProjectionCellSnapshot> layers)
        {
            if (store == null || context == null || !context.Succeeded || instances == null || layers == null || layers.Count <= 0)
            {
                return;
            }

            var byInstance = new Dictionary<string, BlueprintWorldInstanceRecord>(StringComparer.Ordinal);
            for (var index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance != null && !string.IsNullOrWhiteSpace(instance.InstanceId) && !byInstance.ContainsKey(instance.InstanceId))
                {
                    byInstance.Add(instance.InstanceId, instance);
                }
            }

            var changed = false;
            var now = BlueprintStorageConstants.FormatUtc(DateTime.UtcNow);
            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null ||
                    !string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal))
                {
                    continue;
                }

                BlueprintWorldInstanceRecord instance;
                if (!byInstance.TryGetValue(layer.InstanceId ?? string.Empty, out instance) || instance == null)
                {
                    continue;
                }

                if (AddCompletedLayer(instance, layer))
                {
                    instance.UpdatedUtc = now;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            BlueprintWorldInstanceSnapshot ignored;
            store.SaveWorldInstances(context.WorldPairKey, context.WorldKey, instances, out ignored);
        }

        private static Dictionary<string, HashSet<string>> BuildCompletedKeySets(IList<BlueprintWorldInstanceRecord> instances)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            for (var index = 0; instances != null && index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance == null || string.IsNullOrWhiteSpace(instance.InstanceId))
                {
                    continue;
                }

                var set = new HashSet<string>(StringComparer.Ordinal);
                var completed = instance.CompletedLayers;
                for (var layerIndex = 0; completed != null && layerIndex < completed.Count; layerIndex++)
                {
                    var layer = completed[layerIndex];
                    if (layer == null)
                    {
                        continue;
                    }

                    var key = BuildCompletionKey(layer.X, layer.Y, layer.LayerKind, layer.CoverageGroup, layer.ContentId, layer.Style);
                    if (key.Length > 0)
                    {
                        set.Add(key);
                    }
                }

                result[instance.InstanceId] = set;
            }

            return result;
        }

        private static bool IsCandidateCompleted(
            BlueprintProjectionCandidate candidate,
            IDictionary<string, HashSet<string>> completedKeysByInstance)
        {
            if (candidate == null || candidate.Instance == null || completedKeysByInstance == null)
            {
                return false;
            }

            HashSet<string> keys;
            if (!completedKeysByInstance.TryGetValue(candidate.Instance.InstanceId ?? string.Empty, out keys) ||
                keys == null ||
                keys.Count <= 0)
            {
                return false;
            }

            return keys.Contains(BuildCompletionKey(
                candidate.Cell == null ? 0 : candidate.Cell.X,
                candidate.Cell == null ? 0 : candidate.Cell.Y,
                candidate.Layer == null ? string.Empty : candidate.Layer.LayerKind,
                candidate.CoverageGroup,
                candidate.Layer == null ? 0 : candidate.Layer.ContentId,
                candidate.Layer == null ? 0 : candidate.Layer.Style));
        }

        private static bool AddCompletedLayer(BlueprintWorldInstanceRecord instance, BlueprintProjectionCellSnapshot layer)
        {
            if (instance == null || layer == null)
            {
                return false;
            }

            if (instance.CompletedLayers == null)
            {
                instance.CompletedLayers = new List<BlueprintCompletedLayerRecord>();
            }

            var key = BuildCompletionKey(layer.RelativeX, layer.RelativeY, layer.LayerKind, layer.CoverageGroup, layer.ContentId, layer.Style);
            if (key.Length <= 0)
            {
                return false;
            }

            for (var index = 0; index < instance.CompletedLayers.Count; index++)
            {
                var completed = instance.CompletedLayers[index];
                if (completed != null &&
                    string.Equals(
                        BuildCompletionKey(completed.X, completed.Y, completed.LayerKind, completed.CoverageGroup, completed.ContentId, completed.Style),
                        key,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            // Completion progress is an instance-local promise: once a target
            // layer has matched the world, later mining must not resurrect its
            // ghost or material demand. The blueprint library template remains
            // untouched.
            instance.CompletedLayers.Add(new BlueprintCompletedLayerRecord
            {
                X = layer.RelativeX,
                Y = layer.RelativeY,
                LayerKind = NormalizeLayerKind(layer.LayerKind),
                CoverageGroup = NormalizeLayerKind(layer.CoverageGroup),
                ContentId = layer.ContentId,
                Style = layer.Style
            });
            return true;
        }

        private static string BuildCompletionKey(
            int x,
            int y,
            string layerKind,
            string coverageGroup,
            int contentId,
            int style)
        {
            var normalizedKind = NormalizeLayerKind(layerKind);
            var normalizedGroup = NormalizeLayerKind(coverageGroup);
            if (normalizedGroup.Length <= 0)
            {
                normalizedGroup = ResolveCoverageGroup(normalizedKind);
            }

            if (normalizedKind.Length <= 0 || normalizedGroup.Length <= 0)
            {
                return string.Empty;
            }

            return x.ToString(CultureInfo.InvariantCulture) + ":" +
                   y.ToString(CultureInfo.InvariantCulture) + ":" +
                   normalizedKind + ":" +
                   normalizedGroup + ":" +
                   contentId.ToString(CultureInfo.InvariantCulture) + ":" +
                   style.ToString(CultureInfo.InvariantCulture);
        }

        private static string ClassifyLayer(
            BlueprintProjectionCandidate candidate,
            IBlueprintWorldTileReader reader,
            BlueprintReplacementSettings replacementSettings)
        {
            BlueprintWorldTileSnapshot world;
            if (!reader.TryReadTile(candidate.WorldTileX, candidate.WorldTileY, out world) || world == null)
            {
                return BlueprintProjectionLayerStatuses.Unavailable;
            }

            var kind = NormalizeLayerKind(candidate.Layer.LayerKind);
            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyWall(candidate.Layer, world);
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyWire(candidate.Layer, world);
            }

            if (string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyActuator(candidate.Layer, world);
            }

            return ClassifyTile(candidate.Layer, world, replacementSettings);
        }

        private static string ClassifyTile(
            BlueprintCellLayerRecord layer,
            BlueprintWorldTileSnapshot world,
            BlueprintReplacementSettings replacementSettings)
        {
            if (!world.Active)
            {
                return BlueprintProjectionLayerStatuses.Missing;
            }

            if (world.TileType != layer.ContentId)
            {
                string category;
                if (BlueprintReplacementRuleService.IsWorldReplacementFulfilled(layer, world, replacementSettings, out category))
                {
                    return BlueprintProjectionLayerStatuses.Fulfilled;
                }

                return BlueprintProjectionLayerStatuses.Conflict;
            }

            if (layer.Style > 0 && world.ObjectStyle != layer.Style)
            {
                string category;
                if (BlueprintReplacementRuleService.IsWorldReplacementFulfilled(layer, world, replacementSettings, out category))
                {
                    return BlueprintProjectionLayerStatuses.Fulfilled;
                }

                return BlueprintProjectionLayerStatuses.Conflict;
            }

            if (world.TilePaintId != layer.PaintId ||
                BuildTileCoatingFlags(world) != layer.CoatingFlags ||
                world.Slope != layer.Slope ||
                world.HalfBrick != layer.HalfBrick ||
                world.Inactive != layer.Inactive)
            {
                return BlueprintProjectionLayerStatuses.Conflict;
            }

            return BlueprintProjectionLayerStatuses.Fulfilled;
        }

        private static string ClassifyWall(BlueprintCellLayerRecord layer, BlueprintWorldTileSnapshot world)
        {
            if (world.WallType <= 0)
            {
                return BlueprintProjectionLayerStatuses.Missing;
            }

            if (world.WallType != layer.ContentId ||
                world.WallPaintId != layer.PaintId ||
                BuildWallCoatingFlags(world) != layer.CoatingFlags)
            {
                return BlueprintProjectionLayerStatuses.Conflict;
            }

            return BlueprintProjectionLayerStatuses.Fulfilled;
        }

        private static string ClassifyWire(BlueprintCellLayerRecord layer, BlueprintWorldTileSnapshot world)
        {
            var actual = BuildWireFlags(world);
            if (actual == 0)
            {
                return BlueprintProjectionLayerStatuses.Missing;
            }

            return actual == layer.ContentId
                ? BlueprintProjectionLayerStatuses.Fulfilled
                : BlueprintProjectionLayerStatuses.Conflict;
        }

        private static string ClassifyActuator(BlueprintCellLayerRecord layer, BlueprintWorldTileSnapshot world)
        {
            if (!world.HasActuator)
            {
                return BlueprintProjectionLayerStatuses.Missing;
            }

            return layer.ContentId != 0
                ? BlueprintProjectionLayerStatuses.Fulfilled
                : BlueprintProjectionLayerStatuses.Conflict;
        }

        private static void CountStatus(BlueprintProjectionSnapshot snapshot, string status)
        {
            if (string.Equals(status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal))
            {
                snapshot.FulfilledLayerCount++;
            }
            else if (string.Equals(status, BlueprintProjectionLayerStatuses.Completed, StringComparison.Ordinal))
            {
                snapshot.CompletedLayerCount++;
            }
            else if (string.Equals(status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal))
            {
                snapshot.MissingLayerCount++;
            }
            else if (string.Equals(status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
            {
                snapshot.ConflictLayerCount++;
            }
            else
            {
                snapshot.UnavailableLayerCount++;
            }
        }

        private static int BuildTileCoatingFlags(BlueprintWorldTileSnapshot world)
        {
            var flags = 0;
            if (world.TileFullbright)
            {
                flags |= BlueprintCaptureCoatingFlags.Fullbright;
            }

            if (world.TileInvisible)
            {
                flags |= BlueprintCaptureCoatingFlags.Invisible;
            }

            return flags;
        }

        private static int BuildWallCoatingFlags(BlueprintWorldTileSnapshot world)
        {
            var flags = 0;
            if (world.WallFullbright)
            {
                flags |= BlueprintCaptureCoatingFlags.Fullbright;
            }

            if (world.WallInvisible)
            {
                flags |= BlueprintCaptureCoatingFlags.Invisible;
            }

            return flags;
        }

        private static int BuildWireFlags(BlueprintWorldTileSnapshot world)
        {
            var flags = 0;
            if (world.HasRedWire) flags |= BlueprintCaptureWireFlags.Red;
            if (world.HasBlueWire) flags |= BlueprintCaptureWireFlags.Blue;
            if (world.HasGreenWire) flags |= BlueprintCaptureWireFlags.Green;
            if (world.HasYellowWire) flags |= BlueprintCaptureWireFlags.Yellow;
            return flags;
        }

        private static string BuildProjectionSignature(
            BlueprintPlacementWorldContext context,
            BlueprintWorldInstanceSnapshot worldSnapshot,
            IList<BlueprintWorldInstanceRecord> instances,
            IBlueprintWorldTileReader reader)
        {
            var builder = new StringBuilder();
            builder.Append(context == null ? string.Empty : context.WorldPairKey ?? string.Empty).Append('|');
            builder.Append(context == null ? string.Empty : context.WorldKey ?? string.Empty).Append('|');
            builder.Append(worldSnapshot == null ? 0 : worldSnapshot.Revision).Append('|');
            builder.Append(reader != null && reader.IsWorldReady ? "world-ready" : "world-unready").Append('|');
            builder.Append(BlueprintReplacementRuleService.GetSettingsFromCurrentConfig().BuildSignature()).Append('|');
            for (var index = 0; instances != null && index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance == null)
                {
                    continue;
                }

                builder.Append(instance.InstanceId ?? string.Empty).Append(':');
                builder.Append(instance.LayerOrder.ToString(CultureInfo.InvariantCulture)).Append(':');
                builder.Append(instance.Hidden ? '1' : '0').Append(':');
                builder.Append(instance.OriginTileX.ToString(CultureInfo.InvariantCulture)).Append(',');
                builder.Append(instance.OriginTileY.ToString(CultureInfo.InvariantCulture)).Append(':');
                builder.Append(instance.UpdatedUtc ?? string.Empty).Append(':');
                AppendEraseMaskSignature(builder, instance.EraseMask);
                builder.Append(':');
                AppendCompletedLayerSignature(builder, instance.CompletedLayers);
                builder.Append(':');
                var template = instance.TemplateSnapshot;
                builder.Append(template == null ? string.Empty : template.TemplateId ?? string.Empty).Append(':');
                builder.Append(template == null ? string.Empty : template.UpdatedUtc ?? string.Empty).Append(':');
                builder.Append(template == null || template.Cells == null ? 0 : template.Cells.Count).Append(';');
            }

            return builder.ToString();
        }

        private static BlueprintProjectionSnapshot SaveFailureLocked(string resultCode, string message, string worldPairKey, string worldKey)
        {
            _cacheMissCount++;
            var snapshot = new BlueprintProjectionSnapshot
            {
                LoadSucceeded = false,
                ResultCode = resultCode ?? string.Empty,
                Message = message ?? string.Empty,
                WorldPairKey = worldPairKey ?? string.Empty,
                WorldKey = worldKey ?? string.Empty,
                CacheHitCount = _cacheHitCount,
                CacheMissCount = _cacheMissCount,
                LastResolvedUtc = DateTime.UtcNow
            };
            _lastSnapshot = snapshot;
            _lastSignature = string.Empty;
            _lastResolveUtc = DateTime.UtcNow;
            BlueprintDiagnostics.RecordProjectionResolve(snapshot);
            return _lastSnapshot;
        }

        private static void AppendEraseMaskSignature(StringBuilder builder, IList<BlueprintEraseMaskCellRecord> mask)
        {
            if (mask == null || mask.Count <= 0)
            {
                builder.Append("erase=0");
                return;
            }

            var cells = new List<BlueprintEraseMaskCellRecord>();
            for (var index = 0; index < mask.Count; index++)
            {
                if (mask[index] != null)
                {
                    cells.Add(mask[index]);
                }
            }

            cells.Sort((left, right) =>
            {
                var y = left.Y.CompareTo(right.Y);
                return y != 0 ? y : left.X.CompareTo(right.X);
            });
            builder.Append("erase=").Append(cells.Count.ToString(CultureInfo.InvariantCulture)).Append('[');
            for (var index = 0; index < cells.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(cells[index].X.ToString(CultureInfo.InvariantCulture)).Append(':');
                builder.Append(cells[index].Y.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(']');
        }

        private static void AppendCompletedLayerSignature(StringBuilder builder, IList<BlueprintCompletedLayerRecord> completedLayers)
        {
            if (completedLayers == null || completedLayers.Count <= 0)
            {
                builder.Append("completed=0");
                return;
            }

            var keys = new List<string>();
            for (var index = 0; index < completedLayers.Count; index++)
            {
                var layer = completedLayers[index];
                if (layer == null)
                {
                    continue;
                }

                var key = BuildCompletionKey(layer.X, layer.Y, layer.LayerKind, layer.CoverageGroup, layer.ContentId, layer.Style);
                if (key.Length > 0)
                {
                    keys.Add(key);
                }
            }

            keys.Sort(StringComparer.Ordinal);
            builder.Append("completed=").Append(keys.Count.ToString(CultureInfo.InvariantCulture)).Append('[');
            for (var index = 0; index < keys.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(keys[index]);
            }

            builder.Append(']');
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

        private static BlueprintWorldInstanceStore ResolveStoreLocked()
        {
            return _testingStore ?? new BlueprintWorldInstanceStore();
        }

        private static IBlueprintWorldTileReader ResolveReaderLocked()
        {
            return _testingReader ?? BlueprintCaptureService.CreateWorldTileReader();
        }

        private static void ResetCacheLocked()
        {
            _lastSnapshot = null;
            _lastSignature = string.Empty;
            _lastResolveUtc = DateTime.MinValue;
            _cacheHitCount = 0;
            _cacheMissCount = 0;
        }

        private static BlueprintProjectionSnapshot CloneLastSnapshotSummaryLocked()
        {
            return (_lastSnapshot ?? CreateNotResolvedSnapshot()).CloneSummary();
        }

        private static BlueprintProjectionSnapshot CreateNotResolvedSnapshot()
        {
            return new BlueprintProjectionSnapshot
            {
                LoadSucceeded = false,
                ResultCode = "notResolved",
                Message = "蓝图投影尚未刷新。",
                LastResolvedUtc = null
            };
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string NormalizeLayerKind(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string ResolveCoverageGroup(string layerKind)
        {
            var kind = NormalizeLayerKind(layerKind);
            if (string.Equals(kind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintLayerKinds.Tile;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintLayerKinds.Wall;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintLayerKinds.Wire;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintLayerKinds.Actuator;
            }

            return kind;
        }

        private sealed class BlueprintProjectionCandidate
        {
            public BlueprintWorldInstanceRecord Instance { get; private set; }
            public BlueprintCellRecord Cell { get; private set; }
            public BlueprintCellLayerRecord Layer { get; private set; }
            public int WorldTileX { get; private set; }
            public int WorldTileY { get; private set; }
            public string CoverageGroup { get; private set; }

            public static BlueprintProjectionCandidate Create(
                BlueprintWorldInstanceRecord instance,
                BlueprintCellRecord cell,
                BlueprintCellLayerRecord layer)
            {
                return new BlueprintProjectionCandidate
                {
                    Instance = instance,
                    Cell = cell,
                    Layer = layer,
                    WorldTileX = instance.OriginTileX + cell.X,
                    WorldTileY = instance.OriginTileY + cell.Y,
                    CoverageGroup = ResolveCoverageGroup(layer.LayerKind)
                };
            }

            public string BuildCoverageKey()
            {
                return WorldTileX.ToString(CultureInfo.InvariantCulture) + ":" +
                       WorldTileY.ToString(CultureInfo.InvariantCulture) + ":" +
                       (CoverageGroup ?? string.Empty);
            }

            public BlueprintProjectionCellSnapshot ToSnapshot()
            {
                return new BlueprintProjectionCellSnapshot
                {
                    InstanceId = Instance == null ? string.Empty : Instance.InstanceId ?? string.Empty,
                    InstanceName = Instance == null ? string.Empty : Instance.Name ?? string.Empty,
                    LayerOrder = Instance == null ? 0 : Instance.LayerOrder,
                    WorldTileX = WorldTileX,
                    WorldTileY = WorldTileY,
                    RelativeX = Cell == null ? 0 : Cell.X,
                    RelativeY = Cell == null ? 0 : Cell.Y,
                    LayerKind = Layer == null ? string.Empty : Layer.LayerKind ?? string.Empty,
                    CoverageGroup = CoverageGroup ?? string.Empty,
                    ContentId = Layer == null ? 0 : Layer.ContentId,
                    Style = Layer == null ? 0 : Layer.Style,
                    FrameX = Layer == null ? 0 : Layer.FrameX,
                    FrameY = Layer == null ? 0 : Layer.FrameY,
                    PaintId = Layer == null ? 0 : Layer.PaintId,
                    CoatingFlags = Layer == null ? 0 : Layer.CoatingFlags,
                    Slope = Layer == null ? 0 : Layer.Slope,
                    HalfBrick = Layer != null && Layer.HalfBrick,
                    Inactive = Layer != null && Layer.Inactive,
                    MaterialItemId = Layer == null ? 0 : Layer.MaterialItemId,
                    MaterialStack = Layer == null ? 0 : Layer.MaterialStack,
                    MaterialDisplayName = ResolveMaterialDisplayName(Instance, Layer == null ? 0 : Layer.MaterialItemId)
                };
            }

            private static string ResolveMaterialDisplayName(BlueprintWorldInstanceRecord instance, int itemId)
            {
                if (itemId <= 0)
                {
                    return string.Empty;
                }

                var template = instance == null ? null : instance.TemplateSnapshot;
                var materials = template == null ? null : template.Materials;
                for (var index = 0; materials != null && index < materials.Count; index++)
                {
                    var material = materials[index];
                    if (material == null || material.ItemId != itemId)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(material.DisplayNameSnapshot))
                    {
                        return material.DisplayNameSnapshot.Trim();
                    }
                }

                return "#" + itemId.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
