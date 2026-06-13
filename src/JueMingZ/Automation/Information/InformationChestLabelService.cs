using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Config;
using JueMingZ.Records;

namespace JueMingZ.Automation.Information
{
    // Chest labels read names and cache draw models only; chest contents and open state remain untouched.
    internal static class InformationChestLabelService
    {
        internal const int MaxLabelsPerFrame = 240;
        internal const float LabelMaxDistance = 1600f;
        internal const string ModeAlways = "Always";
        internal const string ModeOpened = "Opened";
        internal const string ModeOff = "Off";

        private const float SortRefreshDistance = 64f;
        private const float SortRefreshDistanceSquared = SortRefreshDistance * SortRefreshDistance;
        private const int CacheMovementChunkPixels = 64;
        private const ulong OpenedCacheRefreshTicks = 60;
        private const ulong AlwaysScanSafeRefreshTicks = 300;
        private const int AlwaysPartialScanBudgetTiles = 2048;

        private static readonly object SyncRoot = new object();
        private static readonly ChestLabel[] EmptyLabels = new ChestLabel[0];
        private static readonly List<ChestLabel> LabelBuildBuffer = new List<ChestLabel>();
        private static readonly List<ChestLabel> CurrentLabelFilterBuffer = new List<ChestLabel>();
        private static readonly List<ChestScanCandidate> ScanCandidateBuffer = new List<ChestScanCandidate>();
        private static readonly List<ChestScanCandidate> AlwaysPartialScanCandidateBuffer = new List<ChestScanCandidate>();
        private static readonly HashSet<long> AlwaysPartialScanAdded = new HashSet<long>();

        private static ChestLabel[] _cachedAlwaysLabels = EmptyLabels;
        private static ChestLabel[] _cachedOpenedLabels = EmptyLabels;
        private static ChestLabel[] _cachedSortedLabels = EmptyLabels;
        private static ChestLabel[] _lastSortedLabelSource = EmptyLabels;
        private static ulong _lastAlwaysScanTick;
        private static uint _lastAlwaysSignatureHash;
        private static ChestLabelCacheSignature _lastAlwaysSignature;
        private static uint _lastAlwaysStableSourceSignatureHash;
        private static ulong _lastOpenedScanTick;
        private static uint _lastOpenedSignatureHash;
        private static ChestLabelCacheSignature _lastOpenedSignature;
        private static uint _lastSortSignatureHash;
        private static float _lastSortPlayerCenterX;
        private static float _lastSortPlayerCenterY;
        private static float _lastSortScreenCenterX;
        private static float _lastSortScreenCenterY;
        private static string _lastOpenedChestsHash = "0";
        private static string _lastOpenedChestsHashPlayerKey = string.Empty;
        private static string _lastOpenedChestsHashWorldKey = string.Empty;
        private static ulong _lastOpenedChestsHashTick;
        private static bool _openedChestsHashDirty = true;
        private static long _snapshotRefreshCount;
        private static long _sortRefreshCount;
        private static long _alwaysScanCacheHitCount;
        private static long _alwaysScanCacheMissCount;
        private static long _alwaysSafeRefreshCount;
        private static int _alwaysTilesVisitedLast;
        private static string _alwaysLastDirtyReason = string.Empty;
        private static string _alwaysTypedTileFastPathStatus = string.Empty;
        private static bool _alwaysPartialScanActive;
        private static bool _alwaysPartialScanReturnsEmptyUntilComplete;
        private static ChestLabelCacheSignature _alwaysPartialScanSignature;
        private static string _alwaysPartialScanDirtyReason = string.Empty;
        private static int _alwaysPartialScanMinX;
        private static int _alwaysPartialScanMaxX;
        private static int _alwaysPartialScanMinY;
        private static int _alwaysPartialScanMaxY;
        private static int _alwaysPartialScanNextX;
        private static int _alwaysPartialScanNextY;
        private static int _alwaysPartialScanTilesVisited;
        private static int _alwaysPartialScanTypedTileReads;
        private static int _alwaysPartialScanFallbackTileReads;
        private static int _alwaysPartialScanFailedTileReads;
        private static int _alwaysPartialScanFrameCount;
        private static int _alwaysPartialScanPendingCount;
        private static int _alwaysPartialScanBudgetForTesting;
        private static long _alwaysStableSnapshotId;

        internal static long SnapshotRefreshCount
        {
            get { lock (SyncRoot) { return _snapshotRefreshCount; } }
        }

        internal static long SortRefreshCount
        {
            get { lock (SyncRoot) { return _sortRefreshCount; } }
        }

        internal static long AlwaysScanCacheHitCount
        {
            get { lock (SyncRoot) { return _alwaysScanCacheHitCount; } }
        }

        internal static long AlwaysScanCacheMissCount
        {
            get { lock (SyncRoot) { return _alwaysScanCacheMissCount; } }
        }

        internal static string AlwaysLastDirtyReason
        {
            get { lock (SyncRoot) { return _alwaysLastDirtyReason; } }
        }

        internal static long AlwaysSafeRefreshCount
        {
            get { lock (SyncRoot) { return _alwaysSafeRefreshCount; } }
        }

        internal static int AlwaysTilesVisitedLast
        {
            get { lock (SyncRoot) { return _alwaysTilesVisitedLast; } }
        }

        internal static string AlwaysTypedTileFastPathStatus
        {
            get { lock (SyncRoot) { return _alwaysTypedTileFastPathStatus; } }
        }

        internal static long AlwaysNameCacheHitCount
        {
            get { lock (SyncRoot) { return InformationChestNameResolver.NameCacheHitCount; } }
        }

        internal static long AlwaysNameCacheMissCount
        {
            get { lock (SyncRoot) { return InformationChestNameResolver.NameCacheMissCount; } }
        }

        internal static int AlwaysPartialScanFrameCount
        {
            get { lock (SyncRoot) { return _alwaysPartialScanFrameCount; } }
        }

        internal static int AlwaysPartialScanPendingCount
        {
            get { lock (SyncRoot) { return _alwaysPartialScanPendingCount; } }
        }

        internal static long AlwaysStableSnapshotId
        {
            get { lock (SyncRoot) { return _alwaysStableSnapshotId; } }
        }

        internal static ChestLabel[] GetLabelsForDrawing(InformationWorldContext context, AppSettings settings, string mode)
        {
            lock (SyncRoot)
            {
                uint sourceSignatureHash;
                var labels = GetLabels(context, settings, mode, out sourceSignatureHash);
                if (!ShouldRefreshSortedLabels(context, labels, sourceSignatureHash))
                {
                    return _cachedSortedLabels;
                }

                var sorted = labels == null || labels.Length == 0
                    ? EmptyLabels
                    : labels.Length == 1
                        ? labels
                        : (ChestLabel[])labels.Clone();
                SortLabelsForDrawing(context, sorted);
                _cachedSortedLabels = sorted;
                _lastSortedLabelSource = labels ?? EmptyLabels;
                _lastSortSignatureHash = BuildSortSignatureHash(context, sourceSignatureHash);
                _lastSortPlayerCenterX = GetSortPlayerCenterX(context);
                _lastSortPlayerCenterY = GetSortPlayerCenterY(context);
                _lastSortScreenCenterX = GetSortScreenCenterX(context);
                _lastSortScreenCenterY = GetSortScreenCenterY(context);
                unchecked
                {
                    _sortRefreshCount++;
                }

                return _cachedSortedLabels;
            }
        }

        internal static string NormalizeMode(AppSettings settings)
        {
            if (settings == null)
            {
                return ModeOff;
            }

            var mode = settings.InformationChestNameLabelsMode;
            if (string.Equals(mode, ModeAlways, StringComparison.OrdinalIgnoreCase))
            {
                return ModeAlways;
            }

            if (string.Equals(mode, ModeOpened, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Known", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Open", StringComparison.OrdinalIgnoreCase))
            {
                return ModeOpened;
            }

            return settings.InformationChestNameLabelsEnabled ? ModeOpened : ModeOff;
        }

        internal static void Invalidate()
        {
            lock (SyncRoot)
            {
                ResetCacheStateLocked();
            }
        }

        internal static bool IsChestTileTypeForTesting(int tileType)
        {
            return InformationChestTileScanner.IsChestTileType(tileType);
        }

        internal static bool TryNormalizeOriginFromFrameForTesting(int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            return InformationChestTileScanner.TryNormalizeOriginFromFrame(InformationChestTileScanner.TileTypeContainers, tileX, tileY, frameX, frameY, out chestX, out chestY);
        }

        internal static bool TryNormalizeOriginFromFrameForTesting(int tileType, int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            return InformationChestTileScanner.TryNormalizeOriginFromFrame(tileType, tileX, tileY, frameX, frameY, out chestX, out chestY);
        }

        internal static int BuildTileStyleForTesting(int tileType, int frameX)
        {
            return InformationChestTileScanner.BuildTileStyle(tileType, frameX);
        }

        internal static string ResolveTileDisplayNameForTesting(int tileType, int tileStyle)
        {
            return InformationChestNameResolver.ResolveTileDisplayName(null, tileType, tileStyle);
        }

        internal static string BuildCacheSignatureForTesting(InformationWorldContext context, AppSettings settings, string mode)
        {
            return BuildCacheSignature(context, settings, mode);
        }

        internal static bool ShouldRefreshAlwaysCacheForTesting(
            ulong lastScanTick,
            InformationWorldContext previousContext,
            AppSettings previousSettings,
            string previousMode,
            ulong currentTick,
            InformationWorldContext currentContext,
            AppSettings currentSettings,
            string currentMode,
            out string dirtyReason)
        {
            var previousSignature = BuildCacheSignatureData(previousContext, previousSettings, previousMode);
            var currentSignature = BuildCacheSignatureData(currentContext, currentSettings, currentMode);
            return ShouldRefreshAlwaysLabelsCore(
                lastScanTick,
                previousSignature.Hash,
                currentTick,
                currentSignature.Hash,
                previousSignature,
                currentSignature,
                out dirtyReason);
        }

        internal static int GetLabelCountForTesting(InformationWorldContext context, AppSettings settings, string mode)
        {
            lock (SyncRoot)
            {
                uint signatureHash;
                return GetLabels(context, settings, mode, out signatureHash).Length;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                ResetCacheStateLocked();
                _alwaysScanCacheHitCount = 0;
                _alwaysScanCacheMissCount = 0;
                _alwaysSafeRefreshCount = 0;
                InformationChestNameResolver.ResetCounters();
                _alwaysTilesVisitedLast = 0;
                _alwaysLastDirtyReason = string.Empty;
                _alwaysTypedTileFastPathStatus = string.Empty;
                _alwaysPartialScanFrameCount = 0;
                _alwaysPartialScanPendingCount = 0;
                _alwaysStableSnapshotId = 0;
                _lastAlwaysStableSourceSignatureHash = 0;
                _snapshotRefreshCount = 0;
                _sortRefreshCount = 0;
            }
        }

        internal static void SetAlwaysPartialScanBudgetForTesting(int budgetTiles)
        {
            lock (SyncRoot)
            {
                _alwaysPartialScanBudgetForTesting = Math.Max(0, budgetTiles);
                ResetCacheStateLocked();
                _alwaysPartialScanFrameCount = 0;
                _alwaysPartialScanPendingCount = 0;
                _alwaysStableSnapshotId = 0;
                _lastAlwaysStableSourceSignatureHash = 0;
            }
        }

        internal static bool CanCacheLabelForTesting(InformationWorldContext context, float worldX, float worldY)
        {
            return InformationChestTileScanner.CanCacheLabel(context, worldX, worldY);
        }

        internal static int[] SortLabelIndicesForTesting(InformationWorldContext context, float[] worldXs, float[] worldYs)
        {
            var count = worldXs == null ? 0 : worldXs.Length;
            var labels = new ChestLabel[count];
            for (var index = 0; index < count; index++)
            {
                labels[index] = new ChestLabel
                {
                    TileX = index,
                    TileY = 0,
                    WorldX = worldXs[index],
                    WorldY = worldYs != null && index < worldYs.Length ? worldYs[index] : 0f,
                    Name = "宝箱"
                };
            }

            SortLabelsForDrawing(context, labels);
            var result = new int[labels.Length];
            for (var index = 0; index < labels.Length; index++)
            {
                result[index] = labels[index].TileX;
            }

            return result;
        }

        internal static bool ShouldRefreshSortForTesting(
            float previousPlayerCenterX,
            float previousPlayerCenterY,
            float previousScreenX,
            float previousScreenY,
            int previousScreenWidth,
            int previousScreenHeight,
            uint previousSourceSignatureHash,
            float currentPlayerCenterX,
            float currentPlayerCenterY,
            float currentScreenX,
            float currentScreenY,
            int currentScreenWidth,
            int currentScreenHeight,
            uint currentSourceSignatureHash)
        {
            return IsSortDirty(
                BuildSortSignatureHash(previousSourceSignatureHash, previousScreenWidth, previousScreenHeight),
                previousPlayerCenterX,
                previousPlayerCenterY,
                previousScreenX + previousScreenWidth * 0.5f,
                previousScreenY + previousScreenHeight * 0.5f,
                BuildSortSignatureHash(currentSourceSignatureHash, currentScreenWidth, currentScreenHeight),
                currentPlayerCenterX,
                currentPlayerCenterY,
                currentScreenX + currentScreenWidth * 0.5f,
                currentScreenY + currentScreenHeight * 0.5f);
        }

        private static ChestLabel[] GetLabels(InformationWorldContext context, AppSettings settings, string mode, out uint sourceSignatureHash)
        {
            var labels = string.Equals(mode, ModeAlways, StringComparison.OrdinalIgnoreCase)
                ? GetAlwaysLabels(context, settings, mode, out sourceSignatureHash)
                : GetOpenedLabels(context, settings, mode, out sourceSignatureHash);
            return FilterCurrentContainerLabels(context, labels);
        }

        private static ChestLabel[] GetAlwaysLabels(InformationWorldContext context, AppSettings settings, string mode, out uint sourceSignatureHash)
        {
            var signature = BuildCacheSignatureData(context, settings, mode);
            if (_alwaysPartialScanActive)
            {
                if (AreCacheSignaturesSame(_alwaysPartialScanSignature, signature))
                {
                    if (AdvanceAlwaysPartialScan(context))
                    {
                        PublishAlwaysPartialScan(context);
                        sourceSignatureHash = _lastAlwaysStableSourceSignatureHash;
                        return _cachedAlwaysLabels;
                    }

                    sourceSignatureHash = GetAlwaysPendingSourceSignatureHash();
                    return GetAlwaysPendingLabels();
                }

                ResetAlwaysPartialScanState();
            }

            string dirtyReason;
            if (!ShouldRefreshAlwaysLabels(
                    context,
                    signature,
                    out dirtyReason))
            {
                unchecked
                {
                    _alwaysScanCacheHitCount++;
                }

                sourceSignatureHash = _lastAlwaysStableSourceSignatureHash;
                return _cachedAlwaysLabels;
            }

            unchecked
            {
                _alwaysScanCacheMissCount++;
                if (string.Equals(dirtyReason, "safeRefresh", StringComparison.Ordinal))
                {
                    _alwaysSafeRefreshCount++;
                }
            }

            _alwaysLastDirtyReason = dirtyReason ?? string.Empty;
            if (!BeginAlwaysPartialScan(context, signature, dirtyReason))
            {
                PublishAlwaysStableSnapshot(
                    context,
                    signature,
                    EmptyLabels,
                    0,
                    0,
                    0,
                    0,
                    0);
                sourceSignatureHash = _lastAlwaysStableSourceSignatureHash;
                return _cachedAlwaysLabels;
            }

            if (AdvanceAlwaysPartialScan(context))
            {
                PublishAlwaysPartialScan(context);
                sourceSignatureHash = _lastAlwaysStableSourceSignatureHash;
                return _cachedAlwaysLabels;
            }

            sourceSignatureHash = GetAlwaysPendingSourceSignatureHash();
            return GetAlwaysPendingLabels();
        }

        private static ChestLabel[] GetOpenedLabels(InformationWorldContext context, AppSettings settings, string mode, out uint sourceSignatureHash)
        {
            var signature = BuildCacheSignatureData(context, settings, mode);
            sourceSignatureHash = signature.Hash;
            if (!ShouldRefreshOpenedLabels(context, signature))
            {
                return _cachedOpenedLabels;
            }

            LabelBuildBuffer.Clear();
            var chestNames = InformationChestNameResolver.BuildNameLookup(context == null ? null : context.MainType);
            var openedChests = PlayerWorldBehaviorStore.GetOpenedChests(InformationChestRecordService.BuildBehaviorContext(context));
            for (var index = 0; index < openedChests.Count; index++)
            {
                var opened = openedChests[index];
                if (opened == null || opened.X <= 0 || opened.Y <= 0)
                {
                    continue;
                }

                int openedTileType;
                int openedTileStyle;
                var hasOpenedTileInfo = InformationChestTileScanner.TryResolveTileInfoAt(context, opened.X, opened.Y, out openedTileType, out openedTileStyle);

                var worldX = InformationChestTileScanner.BuildLabelWorldX(
                    opened.X,
                    hasOpenedTileInfo ? openedTileType : InformationChestTileScanner.TileTypeContainers);
                var worldY = InformationChestTileScanner.BuildLabelWorldY(
                    opened.Y,
                    hasOpenedTileInfo ? openedTileType : InformationChestTileScanner.TileTypeContainers);
                if (!InformationChestTileScanner.CanCacheLabel(context, worldX, worldY))
                {
                    continue;
                }

                string name = string.Empty;
                if (hasOpenedTileInfo &&
                    (!chestNames.TryGetValue(InformationChestNameResolver.BuildPositionKey(opened.X, opened.Y), out name) ||
                     string.IsNullOrWhiteSpace(name)))
                {
                    name = InformationChestNameResolver.ResolveTileDisplayName(context == null ? null : context.MainType, openedTileType, openedTileStyle);
                }

                LabelBuildBuffer.Add(new ChestLabel
                {
                    TileX = opened.X,
                    TileY = opened.Y,
                    TileType = hasOpenedTileInfo ? openedTileType : -1,
                    TileStyle = hasOpenedTileInfo ? openedTileStyle : 0,
                    WorldX = worldX,
                    WorldY = worldY,
                    Name = hasOpenedTileInfo && string.IsNullOrWhiteSpace(name)
                        ? InformationChestNameResolver.DefaultLabelName(openedTileType)
                        : name
                });
            }

            _cachedOpenedLabels = LabelBuildBuffer.Count == 0 ? EmptyLabels : LabelBuildBuffer.ToArray();
            _lastOpenedScanTick = context == null ? 0 : context.GameUpdateCount;
            _lastOpenedSignatureHash = signature.Hash;
            _lastOpenedSignature = signature;
            unchecked
            {
                _snapshotRefreshCount++;
            }

            return _cachedOpenedLabels;
        }

        private static ChestLabel[] FilterCurrentContainerLabels(InformationWorldContext context, ChestLabel[] labels)
        {
            if (labels == null || labels.Length == 0)
            {
                return EmptyLabels;
            }

            CurrentLabelFilterBuffer.Clear();
            var changed = false;
            Dictionary<long, string> currentChestNames = null;
            for (var index = 0; index < labels.Length; index++)
            {
                var label = labels[index];
                ChestLabel currentLabel;
                if (!TryBuildCurrentContainerLabel(context, label, ref currentChestNames, out currentLabel))
                {
                    if (!changed)
                    {
                        CopyLabelsToFilterBuffer(labels, index);
                        changed = true;
                    }

                    continue;
                }

                if (ReferenceEquals(currentLabel, label))
                {
                    if (changed)
                    {
                        CurrentLabelFilterBuffer.Add(label);
                    }

                    continue;
                }

                if (!changed)
                {
                    CopyLabelsToFilterBuffer(labels, index);
                    changed = true;
                }

                CurrentLabelFilterBuffer.Add(currentLabel);
            }

            if (!changed)
            {
                return labels;
            }

            return CurrentLabelFilterBuffer.Count == 0 ? EmptyLabels : CurrentLabelFilterBuffer.ToArray();
        }

        private static void CopyLabelsToFilterBuffer(ChestLabel[] labels, int count)
        {
            for (var index = 0; index < count; index++)
            {
                CurrentLabelFilterBuffer.Add(labels[index]);
            }
        }

        private static bool TryBuildCurrentContainerLabel(
            InformationWorldContext context,
            ChestLabel label,
            ref Dictionary<long, string> currentChestNames,
            out ChestLabel currentLabel)
        {
            currentLabel = null;
            if (label == null)
            {
                return false;
            }

            int tileType;
            int tileStyle;
            // Cached labels are only draw candidates. The current tile must still
            // be a valid container origin before an old snapshot is allowed to draw.
            if (!InformationChestTileScanner.TryResolveTileInfoAt(context, label.TileX, label.TileY, out tileType, out tileStyle))
            {
                return false;
            }

            var worldX = InformationChestTileScanner.BuildLabelWorldX(label.TileX, tileType);
            var worldY = InformationChestTileScanner.BuildLabelWorldY(label.TileY, tileType);
            if (!InformationChestTileScanner.CanCacheLabel(context, worldX, worldY))
            {
                return false;
            }

            if (label.TileType == tileType &&
                label.TileStyle == tileStyle &&
                NearlyEqual(label.WorldX, worldX) &&
                NearlyEqual(label.WorldY, worldY) &&
                !string.IsNullOrWhiteSpace(label.Name))
            {
                currentLabel = label;
                return true;
            }

            currentLabel = new ChestLabel
            {
                TileX = label.TileX,
                TileY = label.TileY,
                TileType = tileType,
                TileStyle = tileStyle,
                WorldX = worldX,
                WorldY = worldY,
                Name = ResolveCurrentContainerName(context, label.TileX, label.TileY, tileType, tileStyle, ref currentChestNames)
            };
            return true;
        }

        private static string ResolveCurrentContainerName(
            InformationWorldContext context,
            int tileX,
            int tileY,
            int tileType,
            int tileStyle,
            ref Dictionary<long, string> currentChestNames)
        {
            if (currentChestNames == null)
            {
                currentChestNames = InformationChestNameResolver.BuildNameLookup(context == null ? null : context.MainType);
            }

            string name;
            if (currentChestNames.TryGetValue(InformationChestNameResolver.BuildPositionKey(tileX, tileY), out name) &&
                !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            name = InformationChestNameResolver.ResolveTileDisplayName(context == null ? null : context.MainType, tileType, tileStyle);
            return string.IsNullOrWhiteSpace(name) ? InformationChestNameResolver.DefaultLabelName(tileType) : name;
        }

        private static string GetOpenedChestsHashCached(InformationWorldContext context)
        {
            var behaviorContext = InformationChestRecordService.BuildBehaviorContext(context);
            if (!PlayerWorldBehaviorStore.IsUsable(behaviorContext))
            {
                return "0";
            }

            if (context == null || context.GameUpdateCount == 0)
            {
                return PlayerWorldBehaviorStore.BuildOpenedChestsHash(behaviorContext);
            }

            var sameIdentity =
                string.Equals(_lastOpenedChestsHashPlayerKey, behaviorContext.PlayerKey, StringComparison.Ordinal) &&
                string.Equals(_lastOpenedChestsHashWorldKey, behaviorContext.WorldKey, StringComparison.Ordinal);
            if (!_openedChestsHashDirty &&
                sameIdentity &&
                _lastOpenedChestsHashTick != 0 &&
                context.GameUpdateCount >= _lastOpenedChestsHashTick &&
                context.GameUpdateCount - _lastOpenedChestsHashTick < OpenedCacheRefreshTicks)
            {
                return _lastOpenedChestsHash;
            }

            _lastOpenedChestsHash = PlayerWorldBehaviorStore.BuildOpenedChestsHash(behaviorContext);
            _lastOpenedChestsHashPlayerKey = behaviorContext.PlayerKey ?? string.Empty;
            _lastOpenedChestsHashWorldKey = behaviorContext.WorldKey ?? string.Empty;
            _lastOpenedChestsHashTick = context.GameUpdateCount;
            _openedChestsHashDirty = false;
            return _lastOpenedChestsHash;
        }

        private static string BuildCacheSignature(InformationWorldContext context, AppSettings settings, string mode)
        {
            return BuildCacheSignatureHash(context, settings, mode).ToString("X8", CultureInfo.InvariantCulture);
        }

        private static uint BuildCacheSignatureHash(InformationWorldContext context, AppSettings settings, string mode)
        {
            return BuildCacheSignatureData(context, settings, mode).Hash;
        }

        private static ChestLabelCacheSignature BuildCacheSignatureData(InformationWorldContext context, AppSettings settings, string mode)
        {
            var normalizedMode = NormalizeCacheMode(mode);
            var worldKey = context == null ? string.Empty : context.WorldKey ?? string.Empty;
            var worldRecordKey = context == null ? string.Empty : context.WorldRecordKey ?? string.Empty;
            var playerRecordKey = context == null ? string.Empty : context.PlayerRecordKey ?? string.Empty;
            var screenChunkX = BuildScreenChunkX(context);
            var screenChunkY = BuildScreenChunkY(context);
            var screenWidth = context == null ? 0 : Math.Max(0, context.ScreenWidth);
            var screenHeight = context == null ? 0 : Math.Max(0, context.ScreenHeight);
            var playerChunkX = BuildPlayerChunkX(context);
            var playerChunkY = BuildPlayerChunkY(context);
            var styleSignature = BuildStyleSignature(settings);
            var openedChestsHash = string.Equals(normalizedMode, ModeOpened, StringComparison.Ordinal)
                ? GetOpenedChestsHashCached(context)
                : string.Empty;
            unchecked
            {
                var hash = 2166136261u;
                AddHashValue(ref hash, normalizedMode);
                AddHashValue(ref hash, worldKey);
                AddHashValue(ref hash, worldRecordKey);
                AddHashValue(ref hash, playerRecordKey);
                AddHashInt(ref hash, screenChunkX);
                AddHashInt(ref hash, screenChunkY);
                AddHashInt(ref hash, screenWidth);
                AddHashInt(ref hash, screenHeight);
                AddHashInt(ref hash, playerChunkX);
                AddHashInt(ref hash, playerChunkY);
                AddHashValue(ref hash, styleSignature);
                AddHashValue(ref hash, openedChestsHash);
                return new ChestLabelCacheSignature(
                    hash,
                    normalizedMode,
                    worldKey,
                    worldRecordKey,
                    playerRecordKey,
                    screenChunkX,
                    screenChunkY,
                    screenWidth,
                    screenHeight,
                    playerChunkX,
                    playerChunkY,
                    styleSignature,
                    openedChestsHash);
            }
        }

        private static bool ShouldRefreshAlwaysLabels(InformationWorldContext context, ChestLabelCacheSignature currentSignature, out string dirtyReason)
        {
            return ShouldRefreshAlwaysLabelsCore(
                _lastAlwaysScanTick,
                _lastAlwaysSignatureHash,
                context == null ? 0 : context.GameUpdateCount,
                currentSignature.Hash,
                _lastAlwaysSignature,
                currentSignature,
                out dirtyReason);
        }

        private static bool ShouldRefreshAlwaysLabelsCore(
            ulong lastScanTick,
            uint previousSignatureHash,
            ulong currentTick,
            uint currentSignatureHash,
            ChestLabelCacheSignature previousSignature,
            ChestLabelCacheSignature currentSignature,
            out string dirtyReason)
        {
            if (lastScanTick == 0)
            {
                dirtyReason = "initial";
                return true;
            }

            if (previousSignatureHash != currentSignatureHash)
            {
                dirtyReason = DescribeSignatureChange(previousSignature, currentSignature);
                return true;
            }

            if (currentTick == 0)
            {
                dirtyReason = "cacheHit";
                return false;
            }

            if (currentTick < lastScanTick)
            {
                dirtyReason = "tickReset";
                return true;
            }

            if (currentTick - lastScanTick >= AlwaysScanSafeRefreshTicks)
            {
                dirtyReason = "safeRefresh";
                return true;
            }

            dirtyReason = "cacheHit";
            return false;
        }

        private static bool ShouldRefreshOpenedLabels(InformationWorldContext context, ChestLabelCacheSignature currentSignature)
        {
            var currentTick = context == null ? 0 : context.GameUpdateCount;
            if (_lastOpenedScanTick == 0 || _lastOpenedSignatureHash != currentSignature.Hash)
            {
                return true;
            }

            if (currentTick == 0)
            {
                return false;
            }

            if (currentTick < _lastOpenedScanTick)
            {
                return true;
            }

            return currentTick - _lastOpenedScanTick >= OpenedCacheRefreshTicks;
        }

        private static string DescribeSignatureChange(ChestLabelCacheSignature previous, ChestLabelCacheSignature current)
        {
            if (!string.Equals(previous.Mode, current.Mode, StringComparison.Ordinal))
            {
                return "modeChanged";
            }

            if (!string.Equals(previous.WorldKey, current.WorldKey, StringComparison.Ordinal) ||
                !string.Equals(previous.WorldRecordKey, current.WorldRecordKey, StringComparison.Ordinal))
            {
                return "worldChanged";
            }

            if (previous.ScreenWidth != current.ScreenWidth ||
                previous.ScreenHeight != current.ScreenHeight)
            {
                return "screenSizeChanged";
            }

            if (previous.ScreenChunkX != current.ScreenChunkX ||
                previous.ScreenChunkY != current.ScreenChunkY)
            {
                return "screenChunkChanged";
            }

            if (previous.PlayerChunkX != current.PlayerChunkX ||
                previous.PlayerChunkY != current.PlayerChunkY)
            {
                return "playerChunkChanged";
            }

            if (!string.Equals(previous.PlayerRecordKey, current.PlayerRecordKey, StringComparison.Ordinal))
            {
                return "playerChanged";
            }

            if (!string.Equals(previous.StyleSignature, current.StyleSignature, StringComparison.Ordinal))
            {
                return "styleChanged";
            }

            if (!string.Equals(previous.OpenedChestsHash, current.OpenedChestsHash, StringComparison.Ordinal))
            {
                return "openedRecordsChanged";
            }

            return previous.Hash == current.Hash ? "cacheHit" : "signatureChanged";
        }

        private static string NormalizeCacheMode(string mode)
        {
            if (string.Equals(mode, ModeAlways, StringComparison.OrdinalIgnoreCase))
            {
                return ModeAlways;
            }

            if (string.Equals(mode, ModeOpened, StringComparison.OrdinalIgnoreCase))
            {
                return ModeOpened;
            }

            if (string.Equals(mode, ModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return ModeOff;
            }

            return mode ?? string.Empty;
        }

        private static int BuildScreenChunkX(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.ScreenX), CacheMovementChunkPixels);
        }

        private static int BuildScreenChunkY(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.ScreenY), CacheMovementChunkPixels);
        }

        private static int BuildPlayerChunkX(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.PlayerCenterX), CacheMovementChunkPixels);
        }

        private static int BuildPlayerChunkY(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.PlayerCenterY), CacheMovementChunkPixels);
        }

        private static string BuildStyleSignature(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.ChestNameFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.ChestNameFeatureId).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static bool BeginAlwaysPartialScan(InformationWorldContext context, ChestLabelCacheSignature signature, string dirtyReason)
        {
            ResetAlwaysPartialScanState();
            _alwaysPartialScanFrameCount = 0;
            _alwaysPartialScanPendingCount = 0;
            _alwaysTilesVisitedLast = 0;
            _alwaysTypedTileFastPathStatus = "none";
            if (context == null || context.MainType == null)
            {
                return false;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                return false;
            }

            int minX;
            int maxX;
            int minY;
            int maxY;
            if (!InformationChestTileScanner.TryGetScanBounds(context, tiles, out minX, out maxX, out minY, out maxY))
            {
                return false;
            }

            _alwaysPartialScanActive = true;
            _alwaysPartialScanReturnsEmptyUntilComplete = ShouldReturnEmptyAlwaysSnapshotWhilePending(dirtyReason);
            _alwaysPartialScanSignature = signature;
            _alwaysPartialScanDirtyReason = dirtyReason ?? string.Empty;
            _alwaysPartialScanMinX = minX;
            _alwaysPartialScanMaxX = maxX;
            _alwaysPartialScanMinY = minY;
            _alwaysPartialScanMaxY = maxY;
            _alwaysPartialScanNextX = minX;
            _alwaysPartialScanNextY = minY;
            _alwaysPartialScanPendingCount = CalculateAlwaysPartialScanPendingCount();
            return true;
        }

        private static bool AdvanceAlwaysPartialScan(InformationWorldContext context)
        {
            if (!_alwaysPartialScanActive)
            {
                _alwaysPartialScanPendingCount = 0;
                return true;
            }

            if (context == null || context.MainType == null)
            {
                _alwaysPartialScanPendingCount = 0;
                return true;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                _alwaysPartialScanPendingCount = 0;
                return true;
            }

            var budget = GetAlwaysPartialScanBudgetTiles();
            var allowTypedTileRead = InformationChestTileScanner.CanUseTypedTileRead(tiles);
            var scannedThisFrame = 0;
            while (_alwaysPartialScanNextX <= _alwaysPartialScanMaxX && scannedThisFrame < budget)
            {
                InformationChestTileScanner.CollectVisibleCandidate(
                    context,
                    tiles,
                    AlwaysPartialScanAdded,
                    AlwaysPartialScanCandidateBuffer,
                    _alwaysPartialScanNextX,
                    _alwaysPartialScanNextY,
                    allowTypedTileRead,
                    ref _alwaysPartialScanTilesVisited,
                    ref _alwaysPartialScanTypedTileReads,
                    ref _alwaysPartialScanFallbackTileReads,
                    ref _alwaysPartialScanFailedTileReads);
                scannedThisFrame++;
                AdvanceAlwaysPartialScanCursor();
            }

            unchecked
            {
                _alwaysPartialScanFrameCount++;
            }

            _alwaysPartialScanPendingCount = CalculateAlwaysPartialScanPendingCount();
            _alwaysTilesVisitedLast = _alwaysPartialScanTilesVisited;
            _alwaysTypedTileFastPathStatus = InformationChestTileScanner.BuildTypedTileFastPathStatus(
                _alwaysPartialScanTypedTileReads,
                _alwaysPartialScanFallbackTileReads,
                _alwaysPartialScanFailedTileReads);
            return _alwaysPartialScanPendingCount == 0;
        }

        private static void AdvanceAlwaysPartialScanCursor()
        {
            if (_alwaysPartialScanNextY < _alwaysPartialScanMaxY)
            {
                _alwaysPartialScanNextY++;
                return;
            }

            _alwaysPartialScanNextY = _alwaysPartialScanMinY;
            _alwaysPartialScanNextX++;
        }

        private static int CalculateAlwaysPartialScanPendingCount()
        {
            if (!_alwaysPartialScanActive || _alwaysPartialScanNextX > _alwaysPartialScanMaxX)
            {
                return 0;
            }

            var rows = _alwaysPartialScanMaxY - _alwaysPartialScanMinY + 1;
            if (rows <= 0)
            {
                return 0;
            }

            long pending = _alwaysPartialScanMaxY - _alwaysPartialScanNextY + 1;
            pending += (long)(_alwaysPartialScanMaxX - _alwaysPartialScanNextX) * rows;
            if (pending <= 0)
            {
                return 0;
            }

            return pending > int.MaxValue ? int.MaxValue : (int)pending;
        }

        private static int GetAlwaysPartialScanBudgetTiles()
        {
            return _alwaysPartialScanBudgetForTesting > 0
                ? _alwaysPartialScanBudgetForTesting
                : AlwaysPartialScanBudgetTiles;
        }

        private static ChestLabel[] GetAlwaysPendingLabels()
        {
            return _alwaysPartialScanReturnsEmptyUntilComplete ? EmptyLabels : _cachedAlwaysLabels;
        }

        private static uint GetAlwaysPendingSourceSignatureHash()
        {
            return _alwaysPartialScanReturnsEmptyUntilComplete ? 0u : _lastAlwaysStableSourceSignatureHash;
        }

        private static bool ShouldReturnEmptyAlwaysSnapshotWhilePending(string dirtyReason)
        {
            return string.Equals(dirtyReason, "initial", StringComparison.Ordinal) ||
                   string.Equals(dirtyReason, "worldChanged", StringComparison.Ordinal) ||
                   string.Equals(dirtyReason, "playerChanged", StringComparison.Ordinal) ||
                   string.Equals(dirtyReason, "tickReset", StringComparison.Ordinal);
        }

        private static void PublishAlwaysPartialScan(InformationWorldContext context)
        {
            LabelBuildBuffer.Clear();
            AddLabelsFromCandidates(context, LabelBuildBuffer, AlwaysPartialScanCandidateBuffer, null);
            var labels = LabelBuildBuffer.Count == 0 ? EmptyLabels : LabelBuildBuffer.ToArray();
            PublishAlwaysStableSnapshot(
                context,
                _alwaysPartialScanSignature,
                labels,
                _alwaysPartialScanTilesVisited,
                _alwaysPartialScanTypedTileReads,
                _alwaysPartialScanFallbackTileReads,
                _alwaysPartialScanFailedTileReads,
                _alwaysPartialScanFrameCount);
            ResetAlwaysPartialScanState();
        }

        private static void PublishAlwaysStableSnapshot(
            InformationWorldContext context,
            ChestLabelCacheSignature signature,
            ChestLabel[] labels,
            int tilesVisited,
            int typedTileReads,
            int fallbackTileReads,
            int failedTileReads,
            int partialScanFrameCount)
        {
            _cachedAlwaysLabels = labels == null || labels.Length == 0 ? EmptyLabels : labels;
            _lastAlwaysScanTick = context == null ? 0 : context.GameUpdateCount;
            _lastAlwaysSignatureHash = signature.Hash;
            _lastAlwaysSignature = signature;
            _alwaysTilesVisitedLast = Math.Max(0, tilesVisited);
            _alwaysTypedTileFastPathStatus = InformationChestTileScanner.BuildTypedTileFastPathStatus(
                typedTileReads,
                fallbackTileReads,
                failedTileReads);
            _alwaysPartialScanFrameCount = Math.Max(0, partialScanFrameCount);
            _alwaysPartialScanPendingCount = 0;
            unchecked
            {
                _alwaysStableSnapshotId++;
                if (_alwaysStableSnapshotId <= 0)
                {
                    _alwaysStableSnapshotId = 1;
                }

                _snapshotRefreshCount++;
            }

            _lastAlwaysStableSourceSignatureHash = BuildAlwaysStableSourceSignatureHash(signature.Hash, _alwaysStableSnapshotId);
        }

        private static uint BuildAlwaysStableSourceSignatureHash(uint sourceSignatureHash, long stableSnapshotId)
        {
            unchecked
            {
                var hash = sourceSignatureHash == 0 ? 2166136261u : sourceSignatureHash;
                AddHashInt(ref hash, (int)stableSnapshotId);
                AddHashInt(ref hash, (int)(stableSnapshotId >> 32));
                return hash == 0 ? 1u : hash;
            }
        }

        private static bool AreCacheSignaturesSame(ChestLabelCacheSignature left, ChestLabelCacheSignature right)
        {
            return left.Hash == right.Hash &&
                   string.Equals(left.Mode, right.Mode, StringComparison.Ordinal) &&
                   string.Equals(left.WorldKey, right.WorldKey, StringComparison.Ordinal) &&
                   string.Equals(left.WorldRecordKey, right.WorldRecordKey, StringComparison.Ordinal) &&
                   string.Equals(left.PlayerRecordKey, right.PlayerRecordKey, StringComparison.Ordinal) &&
                   left.ScreenChunkX == right.ScreenChunkX &&
                   left.ScreenChunkY == right.ScreenChunkY &&
                   left.ScreenWidth == right.ScreenWidth &&
                   left.ScreenHeight == right.ScreenHeight &&
                   left.PlayerChunkX == right.PlayerChunkX &&
                   left.PlayerChunkY == right.PlayerChunkY &&
                   string.Equals(left.StyleSignature, right.StyleSignature, StringComparison.Ordinal) &&
                   string.Equals(left.OpenedChestsHash, right.OpenedChestsHash, StringComparison.Ordinal);
        }

        private static void ResetAlwaysPartialScanState()
        {
            _alwaysPartialScanActive = false;
            _alwaysPartialScanReturnsEmptyUntilComplete = false;
            _alwaysPartialScanSignature = new ChestLabelCacheSignature();
            _alwaysPartialScanDirtyReason = string.Empty;
            _alwaysPartialScanMinX = 0;
            _alwaysPartialScanMaxX = -1;
            _alwaysPartialScanMinY = 0;
            _alwaysPartialScanMaxY = -1;
            _alwaysPartialScanNextX = 0;
            _alwaysPartialScanNextY = 0;
            _alwaysPartialScanTilesVisited = 0;
            _alwaysPartialScanTypedTileReads = 0;
            _alwaysPartialScanFallbackTileReads = 0;
            _alwaysPartialScanFailedTileReads = 0;
            _alwaysPartialScanPendingCount = 0;
            AlwaysPartialScanCandidateBuffer.Clear();
            AlwaysPartialScanAdded.Clear();
        }

        private static void AddVisibleTileLabels(InformationWorldContext context, IList<ChestLabel> labels, IDictionary<long, string> loadedChestNames, ISet<long> added)
        {
            _alwaysTilesVisitedLast = 0;
            _alwaysTypedTileFastPathStatus = string.Empty;
            if (context == null || context.MainType == null || labels == null)
            {
                return;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                return;
            }

            int minX;
            int maxX;
            int minY;
            int maxY;
            if (!InformationChestTileScanner.TryGetScanBounds(context, tiles, out minX, out maxX, out minY, out maxY))
            {
                return;
            }

            ScanCandidateBuffer.Clear();
            int tilesVisited;
            int typedTileReads;
            int fallbackTileReads;
            int failedTileReads;
            InformationChestTileScanner.CollectVisibleCandidates(
                context,
                tiles,
                added,
                ScanCandidateBuffer,
                minX,
                maxX,
                minY,
                maxY,
                out tilesVisited,
                out typedTileReads,
                out fallbackTileReads,
                out failedTileReads);

            _alwaysTilesVisitedLast = tilesVisited;
            _alwaysTypedTileFastPathStatus = InformationChestTileScanner.BuildTypedTileFastPathStatus(
                typedTileReads,
                fallbackTileReads,
                failedTileReads);

            AddLabelsFromCandidates(context, labels, ScanCandidateBuffer, loadedChestNames);

            ScanCandidateBuffer.Clear();
        }

        private static void AddLabelsFromCandidates(
            InformationWorldContext context,
            IList<ChestLabel> labels,
            IList<ChestScanCandidate> candidates,
            IDictionary<long, string> loadedChestNames)
        {
            if (labels == null || candidates == null || candidates.Count == 0)
            {
                return;
            }

            var languageSignature = InformationChestNameResolver.BuildLanguageSignature();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                string name;
                if (loadedChestNames == null || !loadedChestNames.TryGetValue(candidate.Key, out name))
                {
                    name = InformationChestNameResolver.ResolveNameWithCache(context, candidate, languageSignature);
                }

                labels.Add(new ChestLabel
                {
                    TileX = candidate.ChestX,
                    TileY = candidate.ChestY,
                    TileType = candidate.TileType,
                    TileStyle = candidate.TileStyle,
                    WorldX = candidate.WorldX,
                    WorldY = candidate.WorldY,
                    Name = string.IsNullOrWhiteSpace(name) ? InformationChestNameResolver.DefaultLabelName(candidate.TileType) : name
                });
            }
        }

        private static bool ShouldRefreshSortedLabels(InformationWorldContext context, ChestLabel[] labels, uint sourceSignatureHash)
        {
            if (!ReferenceEquals(_lastSortedLabelSource, labels ?? EmptyLabels))
            {
                return true;
            }

            return IsSortDirty(
                _lastSortSignatureHash,
                _lastSortPlayerCenterX,
                _lastSortPlayerCenterY,
                _lastSortScreenCenterX,
                _lastSortScreenCenterY,
                BuildSortSignatureHash(context, sourceSignatureHash),
                GetSortPlayerCenterX(context),
                GetSortPlayerCenterY(context),
                GetSortScreenCenterX(context),
                GetSortScreenCenterY(context));
        }

        private static void SortLabelsForDrawing(InformationWorldContext context, ChestLabel[] labels)
        {
            if (context == null || labels == null || labels.Length <= 1)
            {
                return;
            }

            Array.Sort(labels, (left, right) => CompareLabelsForDrawing(context, left, right));
        }

        private static int CompareLabelsForDrawing(InformationWorldContext context, ChestLabel left, ChestLabel right)
        {
            var leftInsideScreen = IsLabelInsideScreen(context, left);
            var rightInsideScreen = IsLabelInsideScreen(context, right);
            if (leftInsideScreen != rightInsideScreen)
            {
                return leftInsideScreen ? -1 : 1;
            }

            var distanceCompare = LabelScreenCenterDistanceSquared(context, left)
                .CompareTo(LabelScreenCenterDistanceSquared(context, right));
            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            var yCompare = left.TileY.CompareTo(right.TileY);
            return yCompare != 0 ? yCompare : left.TileX.CompareTo(right.TileX);
        }

        private static bool IsLabelInsideScreen(InformationWorldContext context, ChestLabel label)
        {
            if (context == null || label == null)
            {
                return false;
            }

            var screenX = label.WorldX - context.ScreenX;
            var screenY = label.WorldY - context.ScreenY;
            return screenX >= 0f &&
                   screenY >= 0f &&
                   screenX <= context.ScreenWidth &&
                   screenY <= context.ScreenHeight;
        }

        private static float LabelScreenCenterDistanceSquared(InformationWorldContext context, ChestLabel label)
        {
            if (context == null || label == null)
            {
                return float.MaxValue;
            }

            var centerX = GetSortScreenCenterX(context);
            var centerY = GetSortScreenCenterY(context);
            return DistanceSquared(label.WorldX, label.WorldY, centerX, centerY);
        }

        private static uint BuildSortSignatureHash(InformationWorldContext context, uint sourceSignatureHash)
        {
            return BuildSortSignatureHash(
                sourceSignatureHash,
                context == null ? 0 : context.ScreenWidth,
                context == null ? 0 : context.ScreenHeight);
        }

        private static uint BuildSortSignatureHash(uint sourceSignatureHash, int screenWidth, int screenHeight)
        {
            unchecked
            {
                var hash = 2166136261u;
                AddHashInt(ref hash, (int)sourceSignatureHash);
                AddHashInt(ref hash, screenWidth);
                AddHashInt(ref hash, screenHeight);
                return hash == 0 ? 1u : hash;
            }
        }

        private static bool IsSortDirty(
            uint previousSignatureHash,
            float previousPlayerCenterX,
            float previousPlayerCenterY,
            float previousScreenCenterX,
            float previousScreenCenterY,
            uint currentSignatureHash,
            float currentPlayerCenterX,
            float currentPlayerCenterY,
            float currentScreenCenterX,
            float currentScreenCenterY)
        {
            if (previousSignatureHash != currentSignatureHash)
            {
                return true;
            }

            if (DistanceSquared(previousPlayerCenterX, previousPlayerCenterY, currentPlayerCenterX, currentPlayerCenterY) >= SortRefreshDistanceSquared)
            {
                return true;
            }

            return DistanceSquared(previousScreenCenterX, previousScreenCenterY, currentScreenCenterX, currentScreenCenterY) >= SortRefreshDistanceSquared;
        }

        private static float GetSortPlayerCenterX(InformationWorldContext context)
        {
            return context == null ? 0f : context.PlayerCenterX;
        }

        private static float GetSortPlayerCenterY(InformationWorldContext context)
        {
            return context == null ? 0f : context.PlayerCenterY;
        }

        private static float GetSortScreenCenterX(InformationWorldContext context)
        {
            return context == null ? 0f : context.ScreenX + context.ScreenWidth * 0.5f;
        }

        private static float GetSortScreenCenterY(InformationWorldContext context)
        {
            return context == null ? 0f : context.ScreenY + context.ScreenHeight * 0.5f;
        }

        private static void ResetCacheStateLocked()
        {
            _cachedAlwaysLabels = EmptyLabels;
            _cachedOpenedLabels = EmptyLabels;
            _cachedSortedLabels = EmptyLabels;
            InformationChestNameResolver.ResetCache();
            ScanCandidateBuffer.Clear();
            ResetAlwaysPartialScanState();
            _lastSortedLabelSource = EmptyLabels;
            _lastAlwaysScanTick = 0;
            _lastAlwaysSignatureHash = 0;
            _lastAlwaysSignature = new ChestLabelCacheSignature();
            _lastAlwaysStableSourceSignatureHash = 0;
            _lastOpenedScanTick = 0;
            _lastOpenedSignatureHash = 0;
            _lastOpenedSignature = new ChestLabelCacheSignature();
            _lastSortSignatureHash = 0;
            _lastSortPlayerCenterX = 0f;
            _lastSortPlayerCenterY = 0f;
            _lastSortScreenCenterX = 0f;
            _lastSortScreenCenterY = 0f;
            _openedChestsHashDirty = true;
        }

        private static float DistanceSquared(float leftX, float leftY, float rightX, float rightY)
        {
            var dx = leftX - rightX;
            var dy = leftY - rightY;
            return dx * dx + dy * dy;
        }

        private static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) < 0.01f;
        }

        private static int FloorDiv(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            if (value >= 0)
            {
                return value / divisor;
            }

            return -(((-value) + divisor - 1) / divisor);
        }

        private static void AddHashValue(ref uint hash, string value)
        {
            unchecked
            {
                var text = value ?? string.Empty;
                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }

                hash ^= 31u;
                hash *= 16777619u;
            }
        }

        private static void AddHashInt(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 16777619u;
                hash ^= (uint)(value >> 16);
                hash *= 16777619u;
                hash ^= 31u;
                hash *= 16777619u;
            }
        }
    }
}
