using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Records;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using Terraria;

namespace JueMingZ.Automation.Information
{
    public static class InformationOverlayService
    {
        private const int MaxNpcLabelsPerFrame = 120;
        private const int MaxChestLabelsPerFrame = 240;
        private const int MaxSignTextLabelsPerFrame = 40;
        private const int MaxTombstoneTextLabelsPerFrame = 40;
        private const int SignTextLayoutCacheLimit = 512;
        private const int TileSize = 16;
        private const int TileFrameSize = 18;
        private const int ChestTileTypeContainers = 21;
        private const int ChestTileTypeDressers = 88;
        private const int ChestTileTypeFakeContainers = 441;
        private const int ChestTileTypeContainers2 = 467;
        private const int ChestTileTypeFakeContainers2 = 468;
        private const int ChestFrameColumns = 2;
        private const int ChestFrameRows = 2;
        private const int ChestStyleFrameWidth = ChestFrameColumns * TileFrameSize;
        private const int DresserFrameColumns = 3;
        private const int DresserStyleFrameWidth = DresserFrameColumns * TileFrameSize;
        private const int TileHighlightScanMarginTiles = 4;
        private const int TileHighlightPlayerChunkTiles = 16;
        private const int TileHighlightLifeCrystalMask = 1 << 0;
        private const int TileHighlightManaCrystalMask = 1 << 1;
        private const int TileHighlightDigtoiseMask = 1 << 2;
        private const int TileHighlightLifeFruitMask = 1 << 3;
        private const int TileHighlightDragonEggMask = 1 << 4;
        private const float ChestLabelMaxDistance = 1600f;
        private const int ChestTileScanMarginTiles = 6;
        private const float ChestCacheCullPadding = ChestTileScanMarginTiles * TileSize;
        private const float ChestLabelSortRefreshDistance = 64f;
        private const float ChestLabelSortRefreshDistanceSquared = ChestLabelSortRefreshDistance * ChestLabelSortRefreshDistance;
        private const ulong NpcScanIntervalTicks = 12;
        private const ulong TileScanIntervalTicks = 60;
        private const int ChestLabelCacheMovementChunkPixels = 64;
        private const ulong ChestOpenedCacheRefreshTicks = 60;
        private const ulong ChestAlwaysScanSafeRefreshTicks = 300;
        private const int ChestAlwaysNameCacheLimit = 512;
        private const int ChestAlwaysPartialScanBudgetTiles = 2048;
        private const ulong SignScanIntervalTicks = 60;
        private const ulong StatusRefreshTicks = 30;
        private const ulong FishingBobberObserverFreshTicks = 2;
        private const string ChestLabelsModeAlways = "Always";
        private const string ChestLabelsModeOpened = "Opened";
        private const string ChestLabelsModeOff = "Off";
        private static readonly object SyncRoot = new object();
        private static readonly InformationWorldLabelRenderer LabelRenderer = new InformationWorldLabelRenderer();
        private static readonly NpcLabel[] EmptyNpcLabels = new NpcLabel[0];
        private static readonly ChestLabel[] EmptyChestLabels = new ChestLabel[0];
        private static readonly List<NpcLabel> NpcLabelBuildBuffer = new List<NpcLabel>();
        private static readonly List<ChestLabel> ChestLabelBuildBuffer = new List<ChestLabel>();
        private static readonly List<ChestScanCandidate> ChestScanCandidateBuffer = new List<ChestScanCandidate>();
        private static readonly List<ChestScanCandidate> ChestAlwaysPartialScanCandidateBuffer = new List<ChestScanCandidate>();
        private static readonly HashSet<long> ChestAlwaysPartialScanAdded = new HashSet<long>();
        private static readonly Dictionary<ChestNameCacheKey, string> ChestAlwaysNameCache = new Dictionary<ChestNameCacheKey, string>();
        private static readonly Queue<ChestNameCacheKey> ChestAlwaysNameCacheOrder = new Queue<ChestNameCacheKey>();
        private static NpcLabel[] CachedNpcLabels = EmptyNpcLabels;
        private static readonly List<TileHighlight> CachedTileHighlights = new List<TileHighlight>();
        private static readonly HashSet<long> TileHighlightVisited = new HashSet<long>();
        private static readonly List<TilePoint> TileHighlightStack = new List<TilePoint>(64);
        private static ChestLabel[] CachedAlwaysChestLabels = EmptyChestLabels;
        private static ChestLabel[] CachedOpenedChestLabels = EmptyChestLabels;
        private static ChestLabel[] CachedSortedChestLabels = EmptyChestLabels;
        private static ChestLabel[] _lastSortedChestLabelSource = EmptyChestLabels;
        private static readonly List<SignTextLabel> CachedSignTextLabels = new List<SignTextLabel>();
        private static readonly List<SignTextLabel> CachedTombstoneTextLabels = new List<SignTextLabel>();
        private static readonly Dictionary<SignTextLayoutKey, SignTextLayout> SignTextLayoutCache = new Dictionary<SignTextLayoutKey, SignTextLayout>();
        private static readonly List<InformationStatusLine> CachedStatusLines = new List<InformationStatusLine>();
        private static readonly HashSet<int> GoldCritterNpcTypes = new HashSet<int>
        {
            442, 443, 444, 445, 446, 447, 448, 539, 592, 593, 601, 605, 613, 627
        };
        private static readonly InformationOverlayDiagnostics Diagnostics = new InformationOverlayDiagnostics();
        private static ulong _lastNpcScanTick;
        private static uint _lastNpcLabelSignatureHash;
        private static ulong _lastTileScanTick;
        private static uint _lastTileHighlightSignatureHash;
        private static ulong _lastChestAlwaysScanTick;
        private static uint _lastChestAlwaysSignatureHash;
        private static ChestLabelCacheSignature _lastChestAlwaysSignature;
        private static uint _lastChestAlwaysStableSourceSignatureHash;
        private static ulong _lastChestOpenedScanTick;
        private static uint _lastChestOpenedSignatureHash;
        private static ChestLabelCacheSignature _lastChestOpenedSignature;
        private static uint _lastChestSortSignatureHash;
        private static float _lastChestSortPlayerCenterX;
        private static float _lastChestSortPlayerCenterY;
        private static float _lastChestSortScreenCenterX;
        private static float _lastChestSortScreenCenterY;
        private static string _lastOpenedChestsHash = "0";
        private static string _lastOpenedChestsHashPlayerKey = string.Empty;
        private static string _lastOpenedChestsHashWorldKey = string.Empty;
        private static ulong _lastOpenedChestsHashTick;
        private static bool _openedChestsHashDirty = true;
        private static ulong _lastSignScanTick;
        private static ulong _lastTombstoneScanTick;
        private static int _signTextLayoutCacheRebuildCount;
        private static string _signTextLayoutFontSignature = string.Empty;
        private static int _signTextLayoutCacheGeneration;
        private static ulong _lastStatusRefreshTick;
        private static string _lastStatusStyleSignature = string.Empty;
        private static long _signTextLayoutCacheHitCount;
        private static long _signTextLayoutCacheMissCount;
        private static long _statusLineCacheHitCount;
        private static long _statusLineCacheMissCount;
        private static long _worldLabelSnapshotRefreshCount;
        private static long _npcLabelSnapshotRefreshCount;
        private static long _chestLabelSnapshotRefreshCount;
        private static long _chestLabelSortRefreshCount;
        private static long _chestAlwaysScanCacheHitCount;
        private static long _chestAlwaysScanCacheMissCount;
        private static long _chestAlwaysSafeRefreshCount;
        private static long _chestAlwaysNameCacheHitCount;
        private static long _chestAlwaysNameCacheMissCount;
        private static int _chestAlwaysTilesVisitedLast;
        private static string _chestAlwaysLastDirtyReason = string.Empty;
        private static string _chestAlwaysTypedTileFastPathStatus = string.Empty;
        private static bool _chestAlwaysPartialScanActive;
        private static bool _chestAlwaysPartialScanReturnsEmptyUntilComplete;
        private static ChestLabelCacheSignature _chestAlwaysPartialScanSignature;
        private static string _chestAlwaysPartialScanDirtyReason = string.Empty;
        private static int _chestAlwaysPartialScanMinX;
        private static int _chestAlwaysPartialScanMaxX;
        private static int _chestAlwaysPartialScanMinY;
        private static int _chestAlwaysPartialScanMaxY;
        private static int _chestAlwaysPartialScanNextX;
        private static int _chestAlwaysPartialScanNextY;
        private static int _chestAlwaysPartialScanTilesVisited;
        private static int _chestAlwaysPartialScanTypedTileReads;
        private static int _chestAlwaysPartialScanFallbackTileReads;
        private static int _chestAlwaysPartialScanFailedTileReads;
        private static int _chestAlwaysPartialScanFrameCount;
        private static int _chestAlwaysPartialScanPendingCount;
        private static int _chestAlwaysPartialScanBudgetForTesting;
        private static long _chestAlwaysStableSnapshotId;
        private static long _informationFishingBobberObserverFreshInactiveSkipCount;
        private static long _informationFishingProjectileFallbackScanCount;
        private static bool _dragonEggMissingLogged;
        private static bool _tileIdsResolved;
        private static int _lifeCrystalTileType = 12;
        private static int _manaCrystalTileType = 639;
        private static int _digtoiseTileType = 751;
        private static int _lifeFruitTileType = 236;
        private static int _dragonEggTileType = -1;
        private static int _tombstoneTileType = 85;
        private static bool _targetDummyNpcTypeResolved;
        private static int _targetDummyNpcType = 488;
        private static bool _critterSetResolved;
        private static object _critterSet;

        public static InformationOverlayDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new InformationOverlayDiagnostics
                {
                    EnabledSummary = Diagnostics.EnabledSummary,
                    NpcLabelsDrawn = Diagnostics.NpcLabelsDrawn,
                    ChestLabelsDrawn = Diagnostics.ChestLabelsDrawn,
                    SignTextLabelsDrawn = Diagnostics.SignTextLabelsDrawn,
                    TombstoneTextLabelsDrawn = Diagnostics.TombstoneTextLabelsDrawn,
                    TileHighlightsDrawn = Diagnostics.TileHighlightsDrawn,
                    StatusLinesDrawn = Diagnostics.StatusLinesDrawn,
                    LastDrawElapsedMs = Diagnostics.LastDrawElapsedMs,
                    LastSkipReason = Diagnostics.LastSkipReason,
                    SignTextLayoutCacheHitCount = _signTextLayoutCacheHitCount,
                    SignTextLayoutCacheMissCount = _signTextLayoutCacheMissCount,
                    WorldLabelSnapshotRefreshCount = _worldLabelSnapshotRefreshCount,
                    NpcLabelSnapshotRefreshCount = _npcLabelSnapshotRefreshCount,
                    ChestLabelSnapshotRefreshCount = _chestLabelSnapshotRefreshCount,
                    ChestLabelSortRefreshCount = _chestLabelSortRefreshCount,
                    ChestAlwaysScanCacheHitCount = _chestAlwaysScanCacheHitCount,
                    ChestAlwaysScanCacheMissCount = _chestAlwaysScanCacheMissCount,
                    ChestAlwaysLastDirtyReason = _chestAlwaysLastDirtyReason,
                    ChestAlwaysSafeRefreshCount = _chestAlwaysSafeRefreshCount,
                    ChestAlwaysTilesVisitedLast = _chestAlwaysTilesVisitedLast,
                    ChestAlwaysTypedTileFastPathStatus = _chestAlwaysTypedTileFastPathStatus,
                    ChestAlwaysNameCacheHitCount = _chestAlwaysNameCacheHitCount,
                    ChestAlwaysNameCacheMissCount = _chestAlwaysNameCacheMissCount,
                    ChestAlwaysPartialScanFrameCount = _chestAlwaysPartialScanFrameCount,
                    ChestAlwaysPartialScanPendingCount = _chestAlwaysPartialScanPendingCount,
                    ChestAlwaysStableSnapshotId = _chestAlwaysStableSnapshotId,
                    WorldContextCacheHitCount = InformationWorldContextProvider.CacheHitCount,
                    WorldContextCacheMissCount = InformationWorldContextProvider.CacheMissCount,
                    WorldContextProfile = InformationWorldContextProvider.LastProfile,
                    WorldContextFileDataRefreshCount = InformationWorldContextProvider.FileDataRefreshCount,
                    StatusLineCacheHitCount = _statusLineCacheHitCount,
                    StatusLineCacheMissCount = _statusLineCacheMissCount,
                    FishingCatchEarlyCacheHitCount = InformationFishingCatchResolver.EarlyCacheHitCount,
                    FishingCatchEarlyCacheMissCount = InformationFishingCatchResolver.EarlyCacheMissCount,
                    FishingWaterScanCount = InformationFishingCatchResolver.WaterScanCount,
                    FishingConditionsReadCount = InformationFishingCatchResolver.ConditionsReadCount,
                    FishingBobberObserverFreshInactiveSkipCount = Interlocked.Read(ref _informationFishingBobberObserverFreshInactiveSkipCount),
                    FishingProjectileFallbackScanCount = Interlocked.Read(ref _informationFishingProjectileFallbackScanCount)
                };
            }
        }

        public static double GetLastDrawElapsedMs()
        {
            lock (SyncRoot)
            {
                return Diagnostics.LastDrawElapsedMs;
            }
        }

        public static bool ShouldDrawWorldOverlay()
        {
            return HasWorldOverlayEnabled(ConfigService.AppSettings ?? AppSettings.CreateDefault());
        }

        public static bool ShouldDrawStatusPanel()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            return HasStatusPanelEnabled(settings) || InformationStatusPanelService.IsAdjusting;
        }

        public static void DrawWorldOverlay(object spriteBatch)
        {
            var stopwatch = Stopwatch.StartNew();
            var npcLabels = 0;
            var chestLabels = 0;
            var signTextLabels = 0;
            var tombstoneTextLabels = 0;
            var tileHighlights = 0;
            var skip = string.Empty;

            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!HasWorldOverlayEnabled(settings))
                {
                    skip = "worldOverlayDisabled";
                    return;
                }

                InformationWorldContext context;
                if (!TryBuildContext(BuildWorldOverlayContextProfile(settings), out context, out skip))
                {
                    return;
                }

                ImportLegacyKnownChests(context, settings);
                RecordOpenChest(context, settings);
                npcLabels = DrawNpcLabels(spriteBatch, context, settings);
                chestLabels = DrawChestLabels(spriteBatch, context, settings);
                signTextLabels = DrawSignTextLabels(spriteBatch, context, settings);
                tombstoneTextLabels = DrawTombstoneTextLabels(spriteBatch, context, settings);
                tileHighlights = DrawTileHighlights(spriteBatch, context, settings);
            }
            catch (Exception error)
            {
                skip = "worldOverlayException";
                LogThrottle.ErrorThrottled(
                    "information-world-overlay-service-error",
                    TimeSpan.FromSeconds(10),
                    "InformationOverlayService",
                    "Information world overlay failed; exception swallowed.", error);
            }
            finally
            {
                stopwatch.Stop();
                UpdateDiagnostics(npcLabels, chestLabels, signTextLabels, tombstoneTextLabels, tileHighlights, null, stopwatch.Elapsed.TotalMilliseconds, skip);
            }
        }

        public static void DrawStatusPanel(object spriteBatch)
        {
            var stopwatch = Stopwatch.StartNew();
            var statusLines = 0;
            var skip = string.Empty;

            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!HasStatusPanelEnabled(settings) && !InformationStatusPanelService.IsAdjusting)
                {
                    skip = "statusPanelDisabled";
                    return;
                }

                InformationWorldContext context;
                if (!TryBuildContext(BuildStatusContextProfile(settings), out context, out skip))
                {
                    return;
                }

                var lines = GetStatusLines(context, settings);
                statusLines = InformationStatusPanelService.DrawPanel(spriteBatch, context, lines);
            }
            catch (Exception error)
            {
                skip = "statusPanelException";
                LogThrottle.ErrorThrottled(
                    "information-status-panel-service-error",
                    TimeSpan.FromSeconds(10),
                    "InformationOverlayService",
                    "Information status panel failed; exception swallowed.", error);
            }
            finally
            {
                stopwatch.Stop();
                UpdateDiagnostics(null, null, null, null, null, statusLines, stopwatch.Elapsed.TotalMilliseconds, skip);
            }
        }

        private static bool TryBuildContext(out InformationWorldContext context, out string skipReason)
        {
            return InformationWorldContextProvider.TryBuild(out context, out skipReason);
        }

        private static bool TryBuildContext(InformationWorldContextProfile profile, out InformationWorldContext context, out string skipReason)
        {
            return InformationWorldContextProvider.TryBuild(profile, out context, out skipReason);
        }

        private static int DrawNpcLabels(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            if (!settings.InformationEnemyNameLabelsEnabled &&
                !settings.InformationCritterNameLabelsEnabled &&
                string.Equals(NormalizeNpcMode(settings.InformationNpcNameLabelsMode), "Off", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var labels = GetNpcLabels(context, settings);
            var drawn = 0;
            for (var index = 0; index < labels.Length && drawn < MaxNpcLabelsPerFrame; index++)
            {
                var label = labels[index];
                if (LabelRenderer.DrawWorldLabel(spriteBatch, context, label.WorldX, label.WorldY, label.Text, label.Color, label.MaxDistance, false, -1f, label.FontScale))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static NpcLabel[] GetNpcLabels(InformationWorldContext context, AppSettings settings)
        {
            lock (SyncRoot)
            {
                var signatureHash = BuildNpcLabelSignatureHash(settings);
                if (context.GameUpdateCount != 0 &&
                    _lastNpcScanTick != 0 &&
                    _lastNpcLabelSignatureHash == signatureHash &&
                    context.GameUpdateCount >= _lastNpcScanTick &&
                    context.GameUpdateCount - _lastNpcScanTick < NpcScanIntervalTicks)
                {
                    if (RefreshCachedNpcLabelPositions(context, CachedNpcLabels))
                    {
                        return CachedNpcLabels;
                    }
                }

                NpcLabelBuildBuffer.Clear();
                ScanNpcLabels(context, settings, NpcLabelBuildBuffer);
                CachedNpcLabels = NpcLabelBuildBuffer.Count == 0 ? EmptyNpcLabels : NpcLabelBuildBuffer.ToArray();
                _lastNpcScanTick = context.GameUpdateCount;
                _lastNpcLabelSignatureHash = signatureHash;
                unchecked
                {
                    _npcLabelSnapshotRefreshCount++;
                    _worldLabelSnapshotRefreshCount++;
                }

                return CachedNpcLabels;
            }
        }

        private static uint BuildNpcLabelSignatureHash(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            unchecked
            {
                var hash = 2166136261u;
                AddHashBool(ref hash, settings.InformationEnemyNameLabelsEnabled);
                AddHashBool(ref hash, settings.InformationCritterNameLabelsEnabled);
                AddHashValue(ref hash, NormalizeNpcMode(settings.InformationNpcNameLabelsMode));
                AddHashValue(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.EnemyNameFeatureId));
                AddHashScaledDouble(ref hash, InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.EnemyNameFeatureId));
                AddHashValue(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.CritterNameFeatureId));
                AddHashScaledDouble(ref hash, InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.CritterNameFeatureId));
                AddHashValue(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.NpcNameFeatureId));
                AddHashScaledDouble(ref hash, InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.NpcNameFeatureId));
                return hash;
            }
        }

        private static void ScanNpcLabels(InformationWorldContext context, AppSettings settings, IList<NpcLabel> labels)
        {
            NPC[] typedNpcs;
            object reflectedNpcs;
            int count;
            if (!TryGetNpcCollection(context, out typedNpcs, out reflectedNpcs, out count))
            {
                LogThrottle.WarnThrottled(
                    "information-main-npc-unavailable",
                    TimeSpan.FromSeconds(30),
                    "InformationOverlayService",
                    "Main.npc is unavailable; NPC labels skipped.");
                return;
            }

            var npcMode = NormalizeNpcMode(settings.InformationNpcNameLabelsMode);
            var segmentInfos = settings.InformationEnemyNameLabelsEnabled
                ? typedNpcs != null
                    ? BuildNpcSegmentInfos(typedNpcs, count)
                    : BuildNpcSegmentInfos(reflectedNpcs, count)
                : null;
            for (var index = 0; index < count && labels.Count < MaxNpcLabelsPerFrame; index++)
            {
                var npc = typedNpcs != null ? (object)typedNpcs[index] : InformationReflection.GetIndexedValue(reflectedNpcs, index);
                NpcLabelSnapshot snapshot;
                if (!TryReadNpcLabelSnapshot(npc, index, out snapshot))
                {
                    continue;
                }

                if (snapshot.Hidden)
                {
                    continue;
                }

                if (snapshot.TownNpc && !string.Equals(npcMode, "Off", StringComparison.OrdinalIgnoreCase))
                {
                    labels.Add(new NpcLabel
                    {
                        Index = index,
                        WhoAmI = snapshot.WhoAmI,
                        Type = snapshot.Type,
                        WorldX = snapshot.WorldX,
                        WorldY = snapshot.WorldY,
                        Life = snapshot.Life,
                        LifeMax = snapshot.LifeMax,
                        TownNpc = snapshot.TownNpc,
                        Friendly = snapshot.Friendly,
                        Hidden = snapshot.Hidden,
                        Critter = snapshot.Critter,
                        Text = InformationNpcNameCompat.ResolveDisplayName(npc, snapshot.Type, snapshot.WhoAmI, npcMode, context.GameUpdateCount),
                        Color = InformationColorHelper.NpcName(settings),
                        MaxDistance = 1800f,
                        FontScale = (float)InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.NpcNameFeatureId)
                    });
                    continue;
                }
                else if (snapshot.Critter && settings.InformationCritterNameLabelsEnabled)
                {
                    labels.Add(new NpcLabel
                    {
                        Index = index,
                        WhoAmI = snapshot.WhoAmI,
                        Type = snapshot.Type,
                        WorldX = snapshot.WorldX,
                        WorldY = snapshot.WorldY,
                        Life = snapshot.Life,
                        LifeMax = snapshot.LifeMax,
                        TownNpc = snapshot.TownNpc,
                        Friendly = snapshot.Friendly,
                        Hidden = snapshot.Hidden,
                        Critter = snapshot.Critter,
                        Text = InformationNpcNameCompat.ResolveNpcTypeName(npc, snapshot.Type),
                        Color = IsGoldCritter(snapshot.Type)
                            ? InformationColorHelper.GoldCritterName()
                            : InformationColorHelper.CritterName(settings),
                        MaxDistance = 1200f,
                        FontScale = (float)InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.CritterNameFeatureId)
                    });
                    continue;
                }
                else if (settings.InformationEnemyNameLabelsEnabled &&
                         !snapshot.TownNpc &&
                         !snapshot.Friendly &&
                         !snapshot.Critter &&
                         snapshot.Life > 0 &&
                         snapshot.LifeMax > 5 &&
                         !IsTargetDummy(snapshot.Type))
                {
                    var knownSegmentRole = GetKnownSegmentRole(snapshot.Type);
                    if (knownSegmentRole == NpcSegmentRole.Body)
                    {
                        continue;
                    }

                    NpcSegmentInfo segmentInfo = null;
                    var hasSegmentInfo = segmentInfos != null && segmentInfos.TryGetValue(index, out segmentInfo);
                    if (knownSegmentRole == NpcSegmentRole.Unknown &&
                        hasSegmentInfo &&
                        !ShouldDrawEnemySegmentLabel(segmentInfo.GroupSize, segmentInfo.NeighborCount))
                    {
                        continue;
                    }

                    labels.Add(new NpcLabel
                    {
                        Index = index,
                        WhoAmI = snapshot.WhoAmI,
                        Type = snapshot.Type,
                        WorldX = snapshot.WorldX,
                        WorldY = snapshot.WorldY,
                        Life = snapshot.Life,
                        LifeMax = snapshot.LifeMax,
                        TownNpc = snapshot.TownNpc,
                        Friendly = snapshot.Friendly,
                        Hidden = snapshot.Hidden,
                        Critter = snapshot.Critter,
                        Text = InformationNpcNameCompat.ResolveNpcTypeName(npc, snapshot.Type),
                        Color = InformationColorHelper.EnemyName(settings),
                        MaxDistance = 1400f,
                        FontScale = (float)InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.EnemyNameFeatureId)
                    });
                    continue;
                }
                else
                {
                    continue;
                }
            }
        }

        private static bool RefreshCachedNpcLabelPositions(InformationWorldContext context, NpcLabel[] labels)
        {
            if (context == null || labels == null || labels.Length == 0)
            {
                return true;
            }

            NPC[] typedNpcs;
            object reflectedNpcs;
            int count;
            if (!TryGetNpcCollection(context, out typedNpcs, out reflectedNpcs, out count))
            {
                return false;
            }

            for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                var label = labels[labelIndex];
                if (label == null || label.Index < 0 || label.Index >= count)
                {
                    return false;
                }

                var npc = typedNpcs != null ? (object)typedNpcs[label.Index] : InformationReflection.GetIndexedValue(reflectedNpcs, label.Index);
                NpcLabelSnapshot snapshot;
                if (!TryReadNpcLabelSnapshot(npc, label.Index, out snapshot) ||
                    !CanReuseNpcLabelSnapshot(label, snapshot))
                {
                    return false;
                }

                label.WorldX = snapshot.WorldX;
                label.WorldY = snapshot.WorldY;
            }

            return true;
        }

        private static bool CanReuseNpcLabelSnapshot(NpcLabel label, NpcLabelSnapshot snapshot)
        {
            if (label == null ||
                snapshot.Type != label.Type ||
                (label.WhoAmI >= 0 && snapshot.WhoAmI >= 0 && snapshot.WhoAmI != label.WhoAmI))
            {
                return false;
            }

            return label.TownNpc == snapshot.TownNpc &&
                   label.Friendly == snapshot.Friendly &&
                   label.Hidden == snapshot.Hidden &&
                   label.Critter == snapshot.Critter &&
                   GetNpcLifeEligibilityKey(label.Life, label.LifeMax) == GetNpcLifeEligibilityKey(snapshot.Life, snapshot.LifeMax);
        }

        private static int GetNpcLifeEligibilityKey(int life, int lifeMax)
        {
            if (life <= 0)
            {
                return 0;
            }

            return lifeMax > 5 ? 2 : 1;
        }

        private static bool TryGetNpcCollection(InformationWorldContext context, out NPC[] typedNpcs, out object reflectedNpcs, out int count)
        {
            typedNpcs = null;
            reflectedNpcs = null;
            count = 0;

            try
            {
                typedNpcs = TerrariaMainCompat.Npcs;
                if (typedNpcs != null && typedNpcs.Length > 0)
                {
                    count = typedNpcs.Length;
                    return true;
                }
            }
            catch
            {
                typedNpcs = null;
            }

            reflectedNpcs = InformationReflection.GetStaticMember(context == null ? null : context.MainType, "npc");
            count = GetCollectionCount(reflectedNpcs);
            return count > 0;
        }

        private static bool TryReadNpcLabelSnapshot(object npc, int fallbackIndex, out NpcLabelSnapshot snapshot)
        {
            snapshot = new NpcLabelSnapshot();
            if (npc == null)
            {
                return false;
            }

            var typedNpc = npc as NPC;
            if (typedNpc != null)
            {
                if (!TerrariaNpcReadCompat.IsActive(typedNpc))
                {
                    return false;
                }

                float worldX;
                float worldY;
                if (!TryReadNpcLabelAnchor(typedNpc, out worldX, out worldY))
                {
                    return false;
                }

                snapshot.Type = TerrariaNpcReadCompat.Type(typedNpc);
                snapshot.WhoAmI = TerrariaNpcReadCompat.WhoAmI(typedNpc);
                if (snapshot.WhoAmI < 0)
                {
                    snapshot.WhoAmI = fallbackIndex;
                }

                snapshot.Life = TerrariaNpcReadCompat.Life(typedNpc);
                snapshot.LifeMax = TerrariaNpcReadCompat.LifeMax(typedNpc);
                snapshot.TownNpc = TerrariaNpcReadCompat.IsTownNpc(typedNpc);
                snapshot.Friendly = TerrariaNpcReadCompat.IsFriendly(typedNpc);
                snapshot.Hidden = TerrariaNpcReadCompat.IsHidden(typedNpc);
                snapshot.Critter = IsCritter(typedNpc, snapshot.Type);
                snapshot.WorldX = worldX;
                snapshot.WorldY = worldY;
                return true;
            }

            if (!IsNpcActive(npc))
            {
                return false;
            }

            float fallbackWorldX;
            float fallbackWorldY;
            if (!TryReadNpcLabelAnchor(npc, out fallbackWorldX, out fallbackWorldY))
            {
                return false;
            }

            InformationReflection.TryReadInt(npc, "type", out snapshot.Type);
            if (!InformationReflection.TryReadInt(npc, "whoAmI", out snapshot.WhoAmI))
            {
                snapshot.WhoAmI = fallbackIndex;
            }

            InformationReflection.TryReadInt(npc, "life", out snapshot.Life);
            InformationReflection.TryReadInt(npc, "lifeMax", out snapshot.LifeMax);
            InformationReflection.TryReadBool(npc, "townNPC", out snapshot.TownNpc);
            InformationReflection.TryReadBool(npc, "friendly", out snapshot.Friendly);
            InformationReflection.TryReadBool(npc, "hide", out snapshot.Hidden);
            snapshot.Critter = IsCritter(npc, snapshot.Type);
            snapshot.WorldX = fallbackWorldX;
            snapshot.WorldY = fallbackWorldY;
            return true;
        }

        private static int DrawChestLabels(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            var mode = NormalizeChestLabelsMode(settings);
            if (string.Equals(mode, ChestLabelsModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(mode, ChestLabelsModeAlways, StringComparison.OrdinalIgnoreCase) &&
                !HasMetalDetector(context.LocalPlayer))
            {
                return 0;
            }

            var labels = GetChestLabelsForDrawing(context, settings, mode);
            var drawn = 0;
            var color = InformationColorHelper.ChestName(settings);
            var fontScale = InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.ChestNameFeatureId);
            for (var index = 0; index < labels.Length && drawn < MaxChestLabelsPerFrame; index++)
            {
                var label = labels[index];
                if (LabelRenderer.DrawWorldLabel(spriteBatch, context, label.WorldX, label.WorldY, label.Name, color, ChestLabelMaxDistance, false, 0f, (float)fontScale))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static ChestLabel[] GetChestLabelsForDrawing(InformationWorldContext context, AppSettings settings, string mode)
        {
            lock (SyncRoot)
            {
                uint sourceSignatureHash;
                var labels = GetChestLabels(context, settings, mode, out sourceSignatureHash);
                if (!ShouldRefreshSortedChestLabels(context, labels, sourceSignatureHash))
                {
                    return CachedSortedChestLabels;
                }

                var sorted = labels == null || labels.Length == 0
                    ? EmptyChestLabels
                    : labels.Length == 1
                        ? labels
                        : (ChestLabel[])labels.Clone();
                SortChestLabelsForDrawing(context, sorted);
                CachedSortedChestLabels = sorted;
                _lastSortedChestLabelSource = labels ?? EmptyChestLabels;
                _lastChestSortSignatureHash = BuildChestLabelSortSignatureHash(context, sourceSignatureHash);
                _lastChestSortPlayerCenterX = GetChestSortPlayerCenterX(context);
                _lastChestSortPlayerCenterY = GetChestSortPlayerCenterY(context);
                _lastChestSortScreenCenterX = GetChestSortScreenCenterX(context);
                _lastChestSortScreenCenterY = GetChestSortScreenCenterY(context);
                unchecked
                {
                    _chestLabelSortRefreshCount++;
                }

                return CachedSortedChestLabels;
            }
        }

        private static bool CanCacheChestLabel(InformationWorldContext context, float worldX, float worldY)
        {
            if (context == null || context.LocalPlayer == null)
            {
                return false;
            }

            var screenX = worldX - context.ScreenX;
            var screenY = worldY - context.ScreenY;
            if (screenX < -ChestCacheCullPadding ||
                screenY < -ChestCacheCullPadding ||
                screenX > context.ScreenWidth + ChestCacheCullPadding ||
                screenY > context.ScreenHeight + ChestCacheCullPadding)
            {
                return false;
            }

            var dx = context.PlayerCenterX - worldX;
            var dy = context.PlayerCenterY - worldY;
            var maxDistance = ChestLabelMaxDistance + ChestCacheCullPadding;
            return dx * dx + dy * dy <= maxDistance * maxDistance;
        }

        private static bool ShouldRefreshSortedChestLabels(InformationWorldContext context, ChestLabel[] labels, uint sourceSignatureHash)
        {
            if (!ReferenceEquals(_lastSortedChestLabelSource, labels ?? EmptyChestLabels))
            {
                return true;
            }

            return IsChestLabelSortDirty(
                _lastChestSortSignatureHash,
                _lastChestSortPlayerCenterX,
                _lastChestSortPlayerCenterY,
                _lastChestSortScreenCenterX,
                _lastChestSortScreenCenterY,
                BuildChestLabelSortSignatureHash(context, sourceSignatureHash),
                GetChestSortPlayerCenterX(context),
                GetChestSortPlayerCenterY(context),
                GetChestSortScreenCenterX(context),
                GetChestSortScreenCenterY(context));
        }

        private static void SortChestLabelsForDrawing(InformationWorldContext context, ChestLabel[] labels)
        {
            if (context == null || labels == null || labels.Length <= 1)
            {
                return;
            }

            Array.Sort(labels, (left, right) => CompareChestLabelsForDrawing(context, left, right));
        }

        private static int CompareChestLabelsForDrawing(InformationWorldContext context, ChestLabel left, ChestLabel right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var leftInsideScreen = IsChestLabelInsideScreen(context, left);
            var rightInsideScreen = IsChestLabelInsideScreen(context, right);
            if (leftInsideScreen != rightInsideScreen)
            {
                return leftInsideScreen ? -1 : 1;
            }

            var distanceCompare = ChestLabelScreenCenterDistanceSquared(context, left)
                .CompareTo(ChestLabelScreenCenterDistanceSquared(context, right));
            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            var yCompare = left.TileY.CompareTo(right.TileY);
            return yCompare != 0 ? yCompare : left.TileX.CompareTo(right.TileX);
        }

        private static bool IsChestLabelInsideScreen(InformationWorldContext context, ChestLabel label)
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

        private static float ChestLabelScreenCenterDistanceSquared(InformationWorldContext context, ChestLabel label)
        {
            if (context == null || label == null)
            {
                return float.MaxValue;
            }

            var centerX = context.ScreenX + context.ScreenWidth * 0.5f;
            var centerY = context.ScreenY + context.ScreenHeight * 0.5f;
            var dx = label.WorldX - centerX;
            var dy = label.WorldY - centerY;
            return dx * dx + dy * dy;
        }

        private static uint BuildChestLabelSortSignatureHash(InformationWorldContext context, uint sourceSignatureHash)
        {
            return BuildChestLabelSortSignatureHash(
                sourceSignatureHash,
                context == null ? 0 : context.ScreenWidth,
                context == null ? 0 : context.ScreenHeight);
        }

        private static uint BuildChestLabelSortSignatureHash(uint sourceSignatureHash, int screenWidth, int screenHeight)
        {
            unchecked
            {
                var hash = 2166136261u;
                AddHashInt(ref hash, (int)sourceSignatureHash);
                AddHashInt(ref hash, screenWidth);
                AddHashInt(ref hash, screenHeight);
                return hash;
            }
        }

        private static bool IsChestLabelSortDirty(
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

            if (DistanceSquared(previousPlayerCenterX, previousPlayerCenterY, currentPlayerCenterX, currentPlayerCenterY) >= ChestLabelSortRefreshDistanceSquared)
            {
                return true;
            }

            return DistanceSquared(previousScreenCenterX, previousScreenCenterY, currentScreenCenterX, currentScreenCenterY) >= ChestLabelSortRefreshDistanceSquared;
        }

        private static float DistanceSquared(float leftX, float leftY, float rightX, float rightY)
        {
            var dx = leftX - rightX;
            var dy = leftY - rightY;
            return dx * dx + dy * dy;
        }

        private static float GetChestSortPlayerCenterX(InformationWorldContext context)
        {
            return context == null ? 0f : context.PlayerCenterX;
        }

        private static float GetChestSortPlayerCenterY(InformationWorldContext context)
        {
            return context == null ? 0f : context.PlayerCenterY;
        }

        private static float GetChestSortScreenCenterX(InformationWorldContext context)
        {
            return context == null ? 0f : context.ScreenX + context.ScreenWidth * 0.5f;
        }

        private static float GetChestSortScreenCenterY(InformationWorldContext context)
        {
            return context == null ? 0f : context.ScreenY + context.ScreenHeight * 0.5f;
        }

        private static int DrawSignTextLabels(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            var mode = NormalizeSignTextMode(settings);
            if (string.Equals(mode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var labels = GetSignTextLabels(context);
            var drawn = 0;
            var color = InformationColorHelper.SignText(settings);
            var fontScale = (float)InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.SignTextFeatureId);
            for (var index = 0; index < labels.Count && drawn < MaxSignTextLabelsPerFrame; index++)
            {
                var label = labels[index];
                var layout = GetOrBuildSignTextLayout(
                    label.Text,
                    label.TextHash,
                    mode,
                    settings.InformationSignTextMaxLines,
                    settings.InformationSignTextMaxCharacters,
                    fontScale);
                if (layout == null)
                {
                    continue;
                }

                if (DrawSignTextBlock(spriteBatch, context, label, layout, color))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static int DrawTombstoneTextLabels(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            var mode = NormalizeTombstoneTextMode(settings);
            if (string.Equals(mode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var labels = GetTombstoneTextLabels(context);
            var drawn = 0;
            var color = InformationColorHelper.TombstoneText(settings);
            var fontScale = (float)InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.TombstoneTextFeatureId);
            for (var index = 0; index < labels.Count && drawn < MaxTombstoneTextLabelsPerFrame; index++)
            {
                var label = labels[index];
                var layout = GetOrBuildSignTextLayout(
                    label.Text,
                    label.TextHash,
                    mode,
                    settings.InformationTombstoneTextMaxLines,
                    settings.InformationTombstoneTextMaxCharacters,
                    fontScale);
                if (layout == null)
                {
                    continue;
                }

                if (DrawSignTextBlock(spriteBatch, context, label, layout, color))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static bool DrawSignTextBlock(object spriteBatch, InformationWorldContext context, SignTextLabel label, SignTextLayout layout, InformationColor color)
        {
            if (spriteBatch == null ||
                context == null ||
                label == null ||
                layout == null ||
                layout.DisplayLines.Length <= 0 ||
                !layout.HasVisibleText ||
                !LabelRenderer.CanDraw(context, label.WorldLeft, label.WorldTop, 1600f, false))
            {
                return false;
            }

            var signCenterX = ((label.WorldLeft + label.WorldRight) * 0.5f) - context.ScreenX;
            var signTop = label.WorldTop - context.ScreenY;
            var ok = false;
            for (var index = 0; index < layout.DisplayLines.Length; index++)
            {
                var lineWidth = index < layout.LineWidths.Length ? layout.LineWidths[index] : 0;
                var drawX = CalculateSignTextLineX(signCenterX, lineWidth, context.ScreenWidth);
                ok |= UiTextRenderer.DrawText(spriteBatch, layout.DisplayLines[index], drawX, signTop + index * layout.LineHeight, color.R, color.G, color.B, color.A, layout.Scale);
            }

            return ok;
        }

        internal static float CalculateSignTextLineXForTesting(float signCenterX, int lineWidth, int screenWidth)
        {
            return CalculateSignTextLineX(signCenterX, lineWidth, screenWidth);
        }

        internal static bool IsTombstoneTileTypeForTesting(int tileType)
        {
            return IsTombstoneTileType(tileType);
        }

        internal static bool IsManaCrystalTileTypeForTesting(int tileType)
        {
            EnsureTileIdsResolved();
            return tileType == _manaCrystalTileType;
        }

        internal static string BuildTileHighlightCacheSignatureForTesting(InformationWorldContext context, AppSettings settings)
        {
            return BuildTileHighlightScanSignature(context, settings).Hash.ToString("X8", CultureInfo.InvariantCulture);
        }

        internal static bool ShouldRefreshTileHighlightCacheForTesting(ulong lastScanTick, uint previousSignatureHash, ulong currentTick, uint currentSignatureHash)
        {
            return ShouldRefreshTileHighlightsCore(lastScanTick, previousSignatureHash, currentTick, currentSignatureHash);
        }

        internal static bool IsChestTileTypeForTesting(int tileType)
        {
            return IsChestTileType(null, tileType);
        }

        internal static bool TryNormalizeChestOriginFromFrameForTesting(int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            return TryNormalizeChestOriginFromFrame(ChestTileTypeContainers, tileX, tileY, frameX, frameY, out chestX, out chestY);
        }

        internal static bool TryNormalizeChestOriginFromFrameForTesting(int tileType, int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            return TryNormalizeChestOriginFromFrame(tileType, tileX, tileY, frameX, frameY, out chestX, out chestY);
        }

        internal static int BuildChestTileStyleForTesting(int tileType, int frameX)
        {
            return BuildChestTileStyle(tileType, frameX);
        }

        internal static string ResolveChestTileDisplayNameForTesting(int tileType, int tileStyle)
        {
            return ResolveChestTileDisplayName(null, tileType, tileStyle);
        }

        internal static string BuildChestLabelCacheSignatureForTesting(InformationWorldContext context, AppSettings settings, string mode)
        {
            return BuildChestLabelCacheSignature(context, settings, mode);
        }

        internal static bool ShouldRefreshChestAlwaysCacheForTesting(
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
            var previousSignature = BuildChestLabelCacheSignatureData(previousContext, previousSettings, previousMode);
            var currentSignature = BuildChestLabelCacheSignatureData(currentContext, currentSettings, currentMode);
            return ShouldRefreshChestAlwaysLabelsCore(
                lastScanTick,
                previousSignature.Hash,
                currentTick,
                currentSignature.Hash,
                previousSignature,
                currentSignature,
                out dirtyReason);
        }

        internal static int GetChestLabelCountForTesting(InformationWorldContext context, AppSettings settings, string mode)
        {
            lock (SyncRoot)
            {
                uint signatureHash;
                return GetChestLabels(context, settings, mode, out signatureHash).Length;
            }
        }

        internal static void ResetChestLabelCacheForTesting()
        {
            lock (SyncRoot)
            {
                ResetChestLabelCacheStateLocked();
                _chestAlwaysScanCacheHitCount = 0;
                _chestAlwaysScanCacheMissCount = 0;
                _chestAlwaysSafeRefreshCount = 0;
                _chestAlwaysNameCacheHitCount = 0;
                _chestAlwaysNameCacheMissCount = 0;
                _chestAlwaysTilesVisitedLast = 0;
                _chestAlwaysLastDirtyReason = string.Empty;
                _chestAlwaysTypedTileFastPathStatus = string.Empty;
                _chestAlwaysPartialScanFrameCount = 0;
                _chestAlwaysPartialScanPendingCount = 0;
                _chestAlwaysStableSnapshotId = 0;
                _lastChestAlwaysStableSourceSignatureHash = 0;
                _chestLabelSnapshotRefreshCount = 0;
                _chestLabelSortRefreshCount = 0;
            }
        }

        internal static void SetChestAlwaysPartialScanBudgetForTesting(int budgetTiles)
        {
            lock (SyncRoot)
            {
                _chestAlwaysPartialScanBudgetForTesting = Math.Max(0, budgetTiles);
                ResetChestLabelCacheStateLocked();
                _chestAlwaysPartialScanFrameCount = 0;
                _chestAlwaysPartialScanPendingCount = 0;
                _chestAlwaysStableSnapshotId = 0;
                _lastChestAlwaysStableSourceSignatureHash = 0;
            }
        }

        internal static string BuildStatusLineCacheSignatureForTesting(InformationWorldContext context, AppSettings settings)
        {
            return BuildStatusLineCacheSignature(context, settings);
        }

        internal static InformationWorldContextProfile BuildStatusContextProfileForTesting(AppSettings settings)
        {
            return BuildStatusContextProfile(settings);
        }

        internal static InformationWorldContextProfile BuildWorldOverlayContextProfileForTesting(AppSettings settings)
        {
            return BuildWorldOverlayContextProfile(settings);
        }

        internal static bool CanReuseStatusLinesForTesting(ulong lastRefreshTick, string lastSignature, InformationWorldContext context, AppSettings settings)
        {
            return CanReuseStatusLines(lastRefreshTick, lastSignature, context, BuildStatusLineCacheSignature(context, settings));
        }

        internal static int MaxChestLabelsPerFrameForTesting()
        {
            return MaxChestLabelsPerFrame;
        }

        internal static bool CanCacheChestLabelForTesting(InformationWorldContext context, float worldX, float worldY)
        {
            return CanCacheChestLabel(context, worldX, worldY);
        }

        internal static int[] SortChestLabelIndicesForTesting(InformationWorldContext context, float[] worldXs, float[] worldYs)
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

            SortChestLabelsForDrawing(context, labels);
            var result = new int[labels.Length];
            for (var index = 0; index < labels.Length; index++)
            {
                result[index] = labels[index].TileX;
            }

            return result;
        }

        internal static bool ShouldRefreshChestLabelSortForTesting(
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
            return IsChestLabelSortDirty(
                BuildChestLabelSortSignatureHash(previousSourceSignatureHash, previousScreenWidth, previousScreenHeight),
                previousPlayerCenterX,
                previousPlayerCenterY,
                previousScreenX + previousScreenWidth * 0.5f,
                previousScreenY + previousScreenHeight * 0.5f,
                BuildChestLabelSortSignatureHash(currentSourceSignatureHash, currentScreenWidth, currentScreenHeight),
                currentPlayerCenterX,
                currentPlayerCenterY,
                currentScreenX + currentScreenWidth * 0.5f,
                currentScreenY + currentScreenHeight * 0.5f);
        }

        internal static bool CanReuseNpcLabelSnapshotForTesting(
            int labelWhoAmI,
            int labelType,
            int labelLife,
            int labelLifeMax,
            bool labelTownNpc,
            bool labelFriendly,
            bool labelHidden,
            bool labelCritter,
            int snapshotWhoAmI,
            int snapshotType,
            int snapshotLife,
            int snapshotLifeMax,
            bool snapshotTownNpc,
            bool snapshotFriendly,
            bool snapshotHidden,
            bool snapshotCritter)
        {
            return CanReuseNpcLabelSnapshot(
                new NpcLabel
                {
                    WhoAmI = labelWhoAmI,
                    Type = labelType,
                    Life = labelLife,
                    LifeMax = labelLifeMax,
                    TownNpc = labelTownNpc,
                    Friendly = labelFriendly,
                    Hidden = labelHidden,
                    Critter = labelCritter
                },
                new NpcLabelSnapshot
                {
                    WhoAmI = snapshotWhoAmI,
                    Type = snapshotType,
                    Life = snapshotLife,
                    LifeMax = snapshotLifeMax,
                    TownNpc = snapshotTownNpc,
                    Friendly = snapshotFriendly,
                    Hidden = snapshotHidden,
                    Critter = snapshotCritter
                });
        }

        internal static bool TryParseChestKeyForTesting(string key, string currentWorldKey, out int x, out int y)
        {
            return TryParseChestKey(key, currentWorldKey, out x, out y);
        }

        internal static int ImportLegacyKnownChestsForTesting(InformationWorldContext context, AppSettings settings)
        {
            return ImportLegacyKnownChestsCore(context, settings, false);
        }

        private static float CalculateSignTextLineX(float signCenterX, int lineWidth, int screenWidth)
        {
            var width = Math.Max(0, lineWidth);
            var drawX = signCenterX - width / 2f;
            var maxX = Math.Max(4f, screenWidth - width - 4f);
            if (drawX < 4f)
            {
                return 4f;
            }

            return drawX > maxX ? maxX : drawX;
        }

        private static int DrawTileHighlights(object spriteBatch, InformationWorldContext context, AppSettings settings)
        {
            if (!settings.InformationHighlightLifeCrystalEnabled &&
                !settings.InformationHighlightManaCrystalEnabled &&
                !settings.InformationHighlightDigtoiseEnabled &&
                !settings.InformationHighlightLifeFruitEnabled &&
                !settings.InformationHighlightDragonEggEnabled)
            {
                return 0;
            }

            if (!HasMetalDetector(context.LocalPlayer))
            {
                return 0;
            }

            var highlights = GetTileHighlights(context, settings);
            var drawn = 0;
            var pulse = 155 + (int)(Math.Abs(Math.Sin(context.GameUpdateCount / 12d)) * 80d);
            for (var index = 0; index < highlights.Count; index++)
            {
                var highlight = highlights[index];
                var x = (int)Math.Round(highlight.TileX * TileSize - context.ScreenX);
                var y = (int)Math.Round(highlight.TileY * TileSize - context.ScreenY);
                var width = highlight.PixelWidth;
                var height = highlight.PixelHeight;
                var color = highlight.Color;
                var borderAlpha = Math.Min(255, Math.Max(color.A, pulse));
                var ok = DrawTileHighlightFrame(spriteBatch, x, y, width, height, color, borderAlpha);
                if (ok)
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static IList<InformationStatusLine> GetStatusLines(InformationWorldContext context, AppSettings settings)
        {
            lock (SyncRoot)
            {
                var cacheSignature = BuildStatusLineCacheSignature(context, settings);
                if (CanReuseStatusLines(_lastStatusRefreshTick, _lastStatusStyleSignature, context, cacheSignature))
                {
                    _statusLineCacheHitCount++;
                    return CachedStatusLines;
                }

                _statusLineCacheMissCount++;
                CachedStatusLines.Clear();
                if (settings.InformationBiomeDisplayEnabled)
                {
                    AddLine(CachedStatusLines, 10, BuildBiomeLine(context.LocalPlayer), InformationColorHelper.BiomeText(settings), InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.BiomeDisplayFeatureId));
                }

                if (settings.InformationWorldInfectionEnabled)
                {
                    AddLine(CachedStatusLines, 20, BuildWorldInfectionLine(context), InformationColorHelper.WorldInfectionText(settings), InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.WorldInfectionFeatureId));
                }

                if (settings.InformationLuckValueEnabled)
                {
                    AddLuckLines(CachedStatusLines, 30, context, InformationColorHelper.LuckText(settings), InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.LuckValueFeatureId));
                }

                var hasFishingBobber = false;
                var fishingFilterSonarActive = false;
                var fishingMessage = string.Empty;
                IList<FishingCatchCandidate> fishingCandidates = null;
                if (settings.InformationFishingCatchesEnabled || settings.InformationFishingFilteredCatchesEnabled)
                {
                    fishingFilterSonarActive = FishingAutomationService.HasSonarBuffOnPlayer(context.LocalPlayer);
                    float bobberX;
                    float bobberY;
                    int bobberIdentity;
                    hasFishingBobber = TryFindLocalBobber(context, out bobberX, out bobberY, out bobberIdentity);
                    if (hasFishingBobber &&
                        (settings.InformationFishingCatchesEnabled || !IsFishingFilterDisabled(settings)))
                    {
                        fishingCandidates = ResolveFishingCatchCandidates(context, bobberX, bobberY, bobberIdentity, BuildFishingFilterStatusSignature(settings), out fishingMessage);
                    }
                }

                if (settings.InformationFishingCatchesEnabled)
                {
                    AddFishingCatchLines(CachedStatusLines, 40, hasFishingBobber, fishingCandidates, fishingMessage, InformationColorHelper.FishingCatchesText(settings), InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingCatchesFeatureId));
                }

                if (settings.InformationFishingFilteredCatchesEnabled)
                {
                    AddFilteredFishingCatchLines(CachedStatusLines, 45, settings, hasFishingBobber, fishingFilterSonarActive, fishingCandidates, fishingMessage, InformationColorHelper.FishingFilteredCatchesText(settings), InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingFilteredCatchesFeatureId));
                }

                if (settings.InformationAnglerQuestEnabled)
                {
                    AddLine(CachedStatusLines, 50, BuildAnglerQuestLine(context), InformationColorHelper.AnglerQuestText(settings), InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.AnglerQuestFeatureId));
                }

                _lastStatusRefreshTick = context.GameUpdateCount;
                _lastStatusStyleSignature = cacheSignature;
                return CachedStatusLines;
            }
        }

        private static bool CanReuseStatusLines(ulong lastRefreshTick, string lastSignature, InformationWorldContext context, string currentSignature)
        {
            return context != null &&
                   context.GameUpdateCount != 0 &&
                   lastRefreshTick != 0 &&
                   string.Equals(lastSignature, currentSignature, StringComparison.Ordinal) &&
                   context.GameUpdateCount >= lastRefreshTick &&
                   context.GameUpdateCount - lastRefreshTick < StatusRefreshTicks;
        }

        private static string BuildStatusLineCacheSignature(InformationWorldContext context, AppSettings settings)
        {
            return BuildStatusStyleSignature(settings) + "|ctx:" +
                   (context == null ? string.Empty : context.WorldKey ?? string.Empty) + "|" +
                   BuildLocalPlayerIdentity(context == null ? null : context.LocalPlayer) + "|" +
                   CultureInfo.CurrentUICulture.Name + "|" +
                   CultureInfo.CurrentCulture.Name;
        }

        private static string BuildStatusStyleSignature(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return (settings.InformationBiomeDisplayEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.BiomeDisplayFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.BiomeDisplayFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   (settings.InformationWorldInfectionEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.WorldInfectionFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.WorldInfectionFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   (settings.InformationLuckValueEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.LuckValueFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.LuckValueFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   (settings.InformationFishingCatchesEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.FishingCatchesFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingCatchesFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   (settings.InformationFishingFilteredCatchesEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.FishingFilteredCatchesFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingFilteredCatchesFeatureId).ToString("0.00", CultureInfo.InvariantCulture) + "|" +
                   BuildFishingFilterStatusSignature(settings) + "|" +
                   (settings.InformationAnglerQuestEnabled ? "1" : "0") + "|" +
                   InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.AnglerQuestFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.AnglerQuestFeatureId).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string BuildLocalPlayerIdentity(object player)
        {
            return player == null
                ? string.Empty
                : RuntimeHelpers.GetHashCode(player).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildFishingFilterStatusSignature(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return FishingFilterModes.Normalize(settings.FishingFilterMode) + "|" +
                   FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode) + "|" +
                   FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterCrateRule) + "|" +
                   FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterQuestFishRule) + "|" +
                   FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterEnemyRule) + "|" +
                   BuildFishingFilterListsHash(settings);
        }

        private static string BuildFishingFilterListsHash(AppSettings settings)
        {
            unchecked
            {
                uint hash = 2166136261u;
                AddExactListHash(ref hash, settings == null ? null : settings.FishingFilterAllowExactEntries);
                AddExactListHash(ref hash, settings == null ? null : settings.FishingFilterDenyExactEntries);
                AddKeywordListHash(ref hash, settings == null ? null : settings.FishingFilterAllowKeywords);
                AddKeywordListHash(ref hash, settings == null ? null : settings.FishingFilterDenyKeywords);
                return hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private static void AddExactListHash(ref uint hash, IList<FishingFilterExactEntry> entries)
        {
            AddHashValue(ref hash, "exact");
            if (entries == null)
            {
                AddHashValue(ref hash, "<null>");
                return;
            }

            AddHashValue(ref hash, entries.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                AddHashValue(ref hash, entry == null ? "<null>" : entry.Kind);
                AddHashValue(ref hash, entry == null ? string.Empty : entry.Id.ToString(CultureInfo.InvariantCulture));
                AddHashValue(ref hash, entry == null ? string.Empty : entry.DisplayNameSnapshot);
            }
        }

        private static void AddKeywordListHash(ref uint hash, IList<string> keywords)
        {
            AddHashValue(ref hash, "keyword");
            if (keywords == null)
            {
                AddHashValue(ref hash, "<null>");
                return;
            }

            AddHashValue(ref hash, keywords.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < keywords.Count; index++)
            {
                AddHashValue(ref hash, keywords[index]);
            }
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

        private static void AddHashBool(ref uint hash, bool value)
        {
            AddHashInt(ref hash, value ? 1 : 0);
        }

        private static void AddHashScaledDouble(ref uint hash, double value)
        {
            AddHashInt(ref hash, (int)Math.Round(value * 1000d));
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

        private static string GetOpenedChestsHashCached(InformationWorldContext context)
        {
            var behaviorContext = BuildBehaviorContext(context);
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
                context.GameUpdateCount - _lastOpenedChestsHashTick < ChestOpenedCacheRefreshTicks)
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

        private static ChestLabel[] GetChestLabels(InformationWorldContext context, AppSettings settings, string mode, out uint sourceSignatureHash)
        {
            return string.Equals(mode, ChestLabelsModeAlways, StringComparison.OrdinalIgnoreCase)
                ? GetAlwaysChestLabels(context, settings, mode, out sourceSignatureHash)
                : GetOpenedChestLabels(context, settings, mode, out sourceSignatureHash);
        }

        private static ChestLabel[] GetAlwaysChestLabels(InformationWorldContext context, AppSettings settings, string mode, out uint sourceSignatureHash)
        {
            var signature = BuildChestLabelCacheSignatureData(context, settings, mode);
            if (_chestAlwaysPartialScanActive)
            {
                if (AreChestLabelCacheSignaturesSame(_chestAlwaysPartialScanSignature, signature))
                {
                    if (AdvanceChestAlwaysPartialScan(context))
                    {
                        PublishChestAlwaysPartialScan(context);
                        sourceSignatureHash = _lastChestAlwaysStableSourceSignatureHash;
                        return CachedAlwaysChestLabels;
                    }

                    sourceSignatureHash = GetChestAlwaysPendingSourceSignatureHash();
                    return GetChestAlwaysPendingLabels();
                }

                ResetChestAlwaysPartialScanState();
            }

            string dirtyReason;
            if (!ShouldRefreshChestAlwaysLabels(
                    context,
                    signature,
                    out dirtyReason))
            {
                unchecked
                {
                    _chestAlwaysScanCacheHitCount++;
                }

                sourceSignatureHash = _lastChestAlwaysStableSourceSignatureHash;
                return CachedAlwaysChestLabels;
            }

            unchecked
            {
                _chestAlwaysScanCacheMissCount++;
                if (string.Equals(dirtyReason, "safeRefresh", StringComparison.Ordinal))
                {
                    _chestAlwaysSafeRefreshCount++;
                }
            }

            _chestAlwaysLastDirtyReason = dirtyReason ?? string.Empty;
            if (!BeginChestAlwaysPartialScan(context, signature, dirtyReason))
            {
                PublishChestAlwaysStableSnapshot(
                    context,
                    signature,
                    EmptyChestLabels,
                    0,
                    0,
                    0,
                    0,
                    0);
                sourceSignatureHash = _lastChestAlwaysStableSourceSignatureHash;
                return CachedAlwaysChestLabels;
            }

            if (AdvanceChestAlwaysPartialScan(context))
            {
                PublishChestAlwaysPartialScan(context);
                sourceSignatureHash = _lastChestAlwaysStableSourceSignatureHash;
                return CachedAlwaysChestLabels;
            }

            sourceSignatureHash = GetChestAlwaysPendingSourceSignatureHash();
            return GetChestAlwaysPendingLabels();
        }

        private static ChestLabel[] GetOpenedChestLabels(InformationWorldContext context, AppSettings settings, string mode, out uint sourceSignatureHash)
        {
            var signature = BuildChestLabelCacheSignatureData(context, settings, mode);
            sourceSignatureHash = signature.Hash;
            if (!ShouldRefreshOpenedChestLabels(context, signature))
            {
                return CachedOpenedChestLabels;
            }

            ChestLabelBuildBuffer.Clear();
            var chestNames = BuildChestNameLookup(context == null ? null : context.MainType);
            var openedChests = PlayerWorldBehaviorStore.GetOpenedChests(BuildBehaviorContext(context));
            for (var index = 0; index < openedChests.Count; index++)
            {
                var opened = openedChests[index];
                if (opened == null || opened.X <= 0 || opened.Y <= 0)
                {
                    continue;
                }

                int openedTileType;
                int openedTileStyle;
                var hasOpenedTileInfo = TryResolveChestTileInfoAt(context, opened.X, opened.Y, out openedTileType, out openedTileStyle);

                string name;
                if (!chestNames.TryGetValue(BuildChestPositionKey(opened.X, opened.Y), out name) ||
                    string.IsNullOrWhiteSpace(name))
                {
                    name = hasOpenedTileInfo
                        ? ResolveChestTileDisplayName(context == null ? null : context.MainType, openedTileType, openedTileStyle)
                        : DefaultChestLabelName(ChestTileTypeContainers);
                }

                var worldX = BuildChestLabelWorldX(opened.X, openedTileType);
                var worldY = BuildChestLabelWorldY(opened.Y, openedTileType);
                if (!CanCacheChestLabel(context, worldX, worldY))
                {
                    continue;
                }

                ChestLabelBuildBuffer.Add(new ChestLabel
                {
                    TileX = opened.X,
                    TileY = opened.Y,
                    WorldX = worldX,
                    WorldY = worldY,
                    Name = string.IsNullOrWhiteSpace(name) ? DefaultChestLabelName(openedTileType) : name
                });
            }

            CachedOpenedChestLabels = ChestLabelBuildBuffer.Count == 0 ? EmptyChestLabels : ChestLabelBuildBuffer.ToArray();
            _lastChestOpenedScanTick = context == null ? 0 : context.GameUpdateCount;
            _lastChestOpenedSignatureHash = signature.Hash;
            _lastChestOpenedSignature = signature;
            unchecked
            {
                _chestLabelSnapshotRefreshCount++;
                _worldLabelSnapshotRefreshCount++;
            }

            return CachedOpenedChestLabels;
        }

        private static string BuildChestLabelCacheSignature(InformationWorldContext context, AppSettings settings, string mode)
        {
            return BuildChestLabelCacheSignatureHash(context, settings, mode).ToString("X8", CultureInfo.InvariantCulture);
        }

        private static uint BuildChestLabelCacheSignatureHash(InformationWorldContext context, AppSettings settings, string mode)
        {
            return BuildChestLabelCacheSignatureData(context, settings, mode).Hash;
        }

        private static ChestLabelCacheSignature BuildChestLabelCacheSignatureData(InformationWorldContext context, AppSettings settings, string mode)
        {
            var normalizedMode = NormalizeChestLabelCacheMode(mode);
            var worldKey = context == null ? string.Empty : context.WorldKey ?? string.Empty;
            var worldRecordKey = context == null ? string.Empty : context.WorldRecordKey ?? string.Empty;
            var playerRecordKey = context == null ? string.Empty : context.PlayerRecordKey ?? string.Empty;
            var screenChunkX = BuildChestLabelScreenChunkX(context);
            var screenChunkY = BuildChestLabelScreenChunkY(context);
            var screenWidth = context == null ? 0 : Math.Max(0, context.ScreenWidth);
            var screenHeight = context == null ? 0 : Math.Max(0, context.ScreenHeight);
            var playerChunkX = BuildChestLabelPlayerChunkX(context);
            var playerChunkY = BuildChestLabelPlayerChunkY(context);
            var styleSignature = BuildChestLabelStyleSignature(settings);
            var openedChestsHash = string.Equals(normalizedMode, ChestLabelsModeOpened, StringComparison.Ordinal)
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

        private static bool ShouldRefreshChestAlwaysLabels(InformationWorldContext context, ChestLabelCacheSignature currentSignature, out string dirtyReason)
        {
            return ShouldRefreshChestAlwaysLabelsCore(
                _lastChestAlwaysScanTick,
                _lastChestAlwaysSignatureHash,
                context == null ? 0 : context.GameUpdateCount,
                currentSignature.Hash,
                _lastChestAlwaysSignature,
                currentSignature,
                out dirtyReason);
        }

        private static bool ShouldRefreshChestAlwaysLabelsCore(
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
                dirtyReason = DescribeChestLabelSignatureChange(previousSignature, currentSignature);
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

            if (currentTick - lastScanTick >= ChestAlwaysScanSafeRefreshTicks)
            {
                dirtyReason = "safeRefresh";
                return true;
            }

            dirtyReason = "cacheHit";
            return false;
        }

        private static bool ShouldRefreshOpenedChestLabels(InformationWorldContext context, ChestLabelCacheSignature currentSignature)
        {
            var currentTick = context == null ? 0 : context.GameUpdateCount;
            if (_lastChestOpenedScanTick == 0 || _lastChestOpenedSignatureHash != currentSignature.Hash)
            {
                return true;
            }

            if (currentTick == 0)
            {
                return false;
            }

            if (currentTick < _lastChestOpenedScanTick)
            {
                return true;
            }

            return currentTick - _lastChestOpenedScanTick >= ChestOpenedCacheRefreshTicks;
        }

        private static string DescribeChestLabelSignatureChange(ChestLabelCacheSignature previous, ChestLabelCacheSignature current)
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

        private static string NormalizeChestLabelCacheMode(string mode)
        {
            if (string.Equals(mode, ChestLabelsModeAlways, StringComparison.OrdinalIgnoreCase))
            {
                return ChestLabelsModeAlways;
            }

            if (string.Equals(mode, ChestLabelsModeOpened, StringComparison.OrdinalIgnoreCase))
            {
                return ChestLabelsModeOpened;
            }

            if (string.Equals(mode, ChestLabelsModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return ChestLabelsModeOff;
            }

            return mode ?? string.Empty;
        }

        private static int BuildChestLabelScreenChunkX(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.ScreenX), ChestLabelCacheMovementChunkPixels);
        }

        private static int BuildChestLabelScreenChunkY(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.ScreenY), ChestLabelCacheMovementChunkPixels);
        }

        private static int BuildChestLabelPlayerChunkX(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.PlayerCenterX), ChestLabelCacheMovementChunkPixels);
        }

        private static int BuildChestLabelPlayerChunkY(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.PlayerCenterY), ChestLabelCacheMovementChunkPixels);
        }

        private static string BuildChestLabelStyleSignature(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.ChestNameFeatureId) + "|" +
                   InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.ChestNameFeatureId).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static bool BeginChestAlwaysPartialScan(InformationWorldContext context, ChestLabelCacheSignature signature, string dirtyReason)
        {
            ResetChestAlwaysPartialScanState();
            _chestAlwaysPartialScanFrameCount = 0;
            _chestAlwaysPartialScanPendingCount = 0;
            _chestAlwaysTilesVisitedLast = 0;
            _chestAlwaysTypedTileFastPathStatus = "none";
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
            if (!TryGetChestTileScanBounds(context, tiles, out minX, out maxX, out minY, out maxY))
            {
                return false;
            }

            _chestAlwaysPartialScanActive = true;
            _chestAlwaysPartialScanReturnsEmptyUntilComplete = ShouldReturnEmptyChestAlwaysSnapshotWhilePending(dirtyReason);
            _chestAlwaysPartialScanSignature = signature;
            _chestAlwaysPartialScanDirtyReason = dirtyReason ?? string.Empty;
            _chestAlwaysPartialScanMinX = minX;
            _chestAlwaysPartialScanMaxX = maxX;
            _chestAlwaysPartialScanMinY = minY;
            _chestAlwaysPartialScanMaxY = maxY;
            _chestAlwaysPartialScanNextX = minX;
            _chestAlwaysPartialScanNextY = minY;
            _chestAlwaysPartialScanPendingCount = CalculateChestAlwaysPartialScanPendingCount();
            return true;
        }

        private static bool AdvanceChestAlwaysPartialScan(InformationWorldContext context)
        {
            if (!_chestAlwaysPartialScanActive)
            {
                _chestAlwaysPartialScanPendingCount = 0;
                return true;
            }

            if (context == null || context.MainType == null)
            {
                _chestAlwaysPartialScanPendingCount = 0;
                return true;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                _chestAlwaysPartialScanPendingCount = 0;
                return true;
            }

            var budget = GetChestAlwaysPartialScanBudgetTiles();
            var allowTypedTileRead = CanUseTypedTileRead(tiles);
            var scannedThisFrame = 0;
            while (_chestAlwaysPartialScanNextX <= _chestAlwaysPartialScanMaxX && scannedThisFrame < budget)
            {
                CollectVisibleChestTileCandidate(
                    context,
                    tiles,
                    ChestAlwaysPartialScanAdded,
                    ChestAlwaysPartialScanCandidateBuffer,
                    _chestAlwaysPartialScanNextX,
                    _chestAlwaysPartialScanNextY,
                    allowTypedTileRead,
                    ref _chestAlwaysPartialScanTilesVisited,
                    ref _chestAlwaysPartialScanTypedTileReads,
                    ref _chestAlwaysPartialScanFallbackTileReads,
                    ref _chestAlwaysPartialScanFailedTileReads);
                scannedThisFrame++;
                AdvanceChestAlwaysPartialScanCursor();
            }

            unchecked
            {
                _chestAlwaysPartialScanFrameCount++;
            }

            _chestAlwaysPartialScanPendingCount = CalculateChestAlwaysPartialScanPendingCount();
            _chestAlwaysTilesVisitedLast = _chestAlwaysPartialScanTilesVisited;
            _chestAlwaysTypedTileFastPathStatus = BuildChestAlwaysTypedTileFastPathStatus(
                _chestAlwaysPartialScanTypedTileReads,
                _chestAlwaysPartialScanFallbackTileReads,
                _chestAlwaysPartialScanFailedTileReads);
            return _chestAlwaysPartialScanPendingCount == 0;
        }

        private static void AdvanceChestAlwaysPartialScanCursor()
        {
            if (_chestAlwaysPartialScanNextY < _chestAlwaysPartialScanMaxY)
            {
                _chestAlwaysPartialScanNextY++;
                return;
            }

            _chestAlwaysPartialScanNextY = _chestAlwaysPartialScanMinY;
            _chestAlwaysPartialScanNextX++;
        }

        private static int CalculateChestAlwaysPartialScanPendingCount()
        {
            if (!_chestAlwaysPartialScanActive || _chestAlwaysPartialScanNextX > _chestAlwaysPartialScanMaxX)
            {
                return 0;
            }

            var rows = _chestAlwaysPartialScanMaxY - _chestAlwaysPartialScanMinY + 1;
            if (rows <= 0)
            {
                return 0;
            }

            long pending = _chestAlwaysPartialScanMaxY - _chestAlwaysPartialScanNextY + 1;
            pending += (long)(_chestAlwaysPartialScanMaxX - _chestAlwaysPartialScanNextX) * rows;
            if (pending <= 0)
            {
                return 0;
            }

            return pending > int.MaxValue ? int.MaxValue : (int)pending;
        }

        private static int GetChestAlwaysPartialScanBudgetTiles()
        {
            return _chestAlwaysPartialScanBudgetForTesting > 0
                ? _chestAlwaysPartialScanBudgetForTesting
                : ChestAlwaysPartialScanBudgetTiles;
        }

        private static ChestLabel[] GetChestAlwaysPendingLabels()
        {
            return _chestAlwaysPartialScanReturnsEmptyUntilComplete ? EmptyChestLabels : CachedAlwaysChestLabels;
        }

        private static uint GetChestAlwaysPendingSourceSignatureHash()
        {
            return _chestAlwaysPartialScanReturnsEmptyUntilComplete ? 0u : _lastChestAlwaysStableSourceSignatureHash;
        }

        private static bool ShouldReturnEmptyChestAlwaysSnapshotWhilePending(string dirtyReason)
        {
            return string.Equals(dirtyReason, "initial", StringComparison.Ordinal) ||
                   string.Equals(dirtyReason, "worldChanged", StringComparison.Ordinal) ||
                   string.Equals(dirtyReason, "playerChanged", StringComparison.Ordinal) ||
                   string.Equals(dirtyReason, "tickReset", StringComparison.Ordinal);
        }

        private static void PublishChestAlwaysPartialScan(InformationWorldContext context)
        {
            ChestLabelBuildBuffer.Clear();
            AddChestLabelsFromCandidates(context, ChestLabelBuildBuffer, ChestAlwaysPartialScanCandidateBuffer, null);
            var labels = ChestLabelBuildBuffer.Count == 0 ? EmptyChestLabels : ChestLabelBuildBuffer.ToArray();
            PublishChestAlwaysStableSnapshot(
                context,
                _chestAlwaysPartialScanSignature,
                labels,
                _chestAlwaysPartialScanTilesVisited,
                _chestAlwaysPartialScanTypedTileReads,
                _chestAlwaysPartialScanFallbackTileReads,
                _chestAlwaysPartialScanFailedTileReads,
                _chestAlwaysPartialScanFrameCount);
            ResetChestAlwaysPartialScanState();
        }

        private static void PublishChestAlwaysStableSnapshot(
            InformationWorldContext context,
            ChestLabelCacheSignature signature,
            ChestLabel[] labels,
            int tilesVisited,
            int typedTileReads,
            int fallbackTileReads,
            int failedTileReads,
            int partialScanFrameCount)
        {
            CachedAlwaysChestLabels = labels == null || labels.Length == 0 ? EmptyChestLabels : labels;
            _lastChestAlwaysScanTick = context == null ? 0 : context.GameUpdateCount;
            _lastChestAlwaysSignatureHash = signature.Hash;
            _lastChestAlwaysSignature = signature;
            _chestAlwaysTilesVisitedLast = Math.Max(0, tilesVisited);
            _chestAlwaysTypedTileFastPathStatus = BuildChestAlwaysTypedTileFastPathStatus(
                typedTileReads,
                fallbackTileReads,
                failedTileReads);
            _chestAlwaysPartialScanFrameCount = Math.Max(0, partialScanFrameCount);
            _chestAlwaysPartialScanPendingCount = 0;
            unchecked
            {
                _chestAlwaysStableSnapshotId++;
                if (_chestAlwaysStableSnapshotId <= 0)
                {
                    _chestAlwaysStableSnapshotId = 1;
                }

                _chestLabelSnapshotRefreshCount++;
                _worldLabelSnapshotRefreshCount++;
            }

            _lastChestAlwaysStableSourceSignatureHash = BuildChestAlwaysStableSourceSignatureHash(signature.Hash, _chestAlwaysStableSnapshotId);
        }

        private static uint BuildChestAlwaysStableSourceSignatureHash(uint sourceSignatureHash, long stableSnapshotId)
        {
            unchecked
            {
                var hash = sourceSignatureHash == 0 ? 2166136261u : sourceSignatureHash;
                AddHashInt(ref hash, (int)stableSnapshotId);
                AddHashInt(ref hash, (int)(stableSnapshotId >> 32));
                return hash == 0 ? 1u : hash;
            }
        }

        private static bool AreChestLabelCacheSignaturesSame(ChestLabelCacheSignature left, ChestLabelCacheSignature right)
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

        private static void ResetChestAlwaysPartialScanState()
        {
            _chestAlwaysPartialScanActive = false;
            _chestAlwaysPartialScanReturnsEmptyUntilComplete = false;
            _chestAlwaysPartialScanSignature = new ChestLabelCacheSignature();
            _chestAlwaysPartialScanDirtyReason = string.Empty;
            _chestAlwaysPartialScanMinX = 0;
            _chestAlwaysPartialScanMaxX = -1;
            _chestAlwaysPartialScanMinY = 0;
            _chestAlwaysPartialScanMaxY = -1;
            _chestAlwaysPartialScanNextX = 0;
            _chestAlwaysPartialScanNextY = 0;
            _chestAlwaysPartialScanTilesVisited = 0;
            _chestAlwaysPartialScanTypedTileReads = 0;
            _chestAlwaysPartialScanFallbackTileReads = 0;
            _chestAlwaysPartialScanFailedTileReads = 0;
            _chestAlwaysPartialScanPendingCount = 0;
            ChestAlwaysPartialScanCandidateBuffer.Clear();
            ChestAlwaysPartialScanAdded.Clear();
        }

        private static void AddAllChestLabels(InformationWorldContext context, IList<ChestLabel> labels)
        {
            var added = new HashSet<long>();
            AddVisibleChestTileLabels(context, labels, null, added);
        }

        private static void AddVisibleChestTileLabels(InformationWorldContext context, IList<ChestLabel> labels, IDictionary<long, string> loadedChestNames, ISet<long> added)
        {
            _chestAlwaysTilesVisitedLast = 0;
            _chestAlwaysTypedTileFastPathStatus = string.Empty;
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
            if (!TryGetChestTileScanBounds(context, tiles, out minX, out maxX, out minY, out maxY))
            {
                return;
            }

            ChestScanCandidateBuffer.Clear();
            int tilesVisited;
            int typedTileReads;
            int fallbackTileReads;
            int failedTileReads;
            CollectVisibleChestTileCandidates(
                context,
                tiles,
                added,
                ChestScanCandidateBuffer,
                minX,
                maxX,
                minY,
                maxY,
                out tilesVisited,
                out typedTileReads,
                out fallbackTileReads,
                out failedTileReads);

            _chestAlwaysTilesVisitedLast = tilesVisited;
            _chestAlwaysTypedTileFastPathStatus = BuildChestAlwaysTypedTileFastPathStatus(
                typedTileReads,
                fallbackTileReads,
                failedTileReads);

            AddChestLabelsFromCandidates(context, labels, ChestScanCandidateBuffer, loadedChestNames);

            ChestScanCandidateBuffer.Clear();
        }

        private static void AddChestLabelsFromCandidates(
            InformationWorldContext context,
            IList<ChestLabel> labels,
            IList<ChestScanCandidate> candidates,
            IDictionary<long, string> loadedChestNames)
        {
            if (labels == null || candidates == null || candidates.Count == 0)
            {
                return;
            }

            var languageSignature = BuildChestNameLanguageSignature();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                string name;
                if (loadedChestNames == null || !loadedChestNames.TryGetValue(candidate.Key, out name))
                {
                    name = ResolveChestNameWithCache(context, candidate, languageSignature);
                }

                labels.Add(new ChestLabel
                {
                    TileX = candidate.ChestX,
                    TileY = candidate.ChestY,
                    WorldX = candidate.WorldX,
                    WorldY = candidate.WorldY,
                    Name = string.IsNullOrWhiteSpace(name) ? DefaultChestLabelName(candidate.TileType) : name
                });
            }
        }

        private static void CollectVisibleChestTileCandidates(
            InformationWorldContext context,
            object tiles,
            ISet<long> added,
            IList<ChestScanCandidate> candidates,
            int minX,
            int maxX,
            int minY,
            int maxY,
            out int tilesVisited,
            out int typedTileReads,
            out int fallbackTileReads,
            out int failedTileReads)
        {
            tilesVisited = 0;
            typedTileReads = 0;
            fallbackTileReads = 0;
            failedTileReads = 0;
            var allowTypedTileRead = CanUseTypedTileRead(tiles);
            if (context == null || context.MainType == null || tiles == null || candidates == null)
            {
                return;
            }

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    CollectVisibleChestTileCandidate(
                        context,
                        tiles,
                        added,
                        candidates,
                        x,
                        y,
                        allowTypedTileRead,
                        ref tilesVisited,
                        ref typedTileReads,
                        ref fallbackTileReads,
                        ref failedTileReads);
                }
            }
        }

        private static void CollectVisibleChestTileCandidate(
            InformationWorldContext context,
            object tiles,
            ISet<long> added,
            IList<ChestScanCandidate> candidates,
            int x,
            int y,
            bool allowTypedTileRead,
            ref int tilesVisited,
            ref int typedTileReads,
            ref int fallbackTileReads,
            ref int failedTileReads)
        {
            tilesVisited++;
            bool active;
            int tileType;
            int frameX;
            int frameY;
            bool usedTypedTileRead;
            if (!TryReadTileActiveTypeAndFrame(
                    tiles,
                    x,
                    y,
                    out active,
                    out tileType,
                    out frameX,
                    out frameY,
                    allowTypedTileRead,
                    out usedTypedTileRead))
            {
                failedTileReads++;
                return;
            }

            if (usedTypedTileRead)
            {
                typedTileReads++;
            }
            else
            {
                fallbackTileReads++;
            }

            if (!active)
            {
                return;
            }

            if (!IsChestTileType(context.MainType, tileType))
            {
                return;
            }

            int chestX;
            int chestY;
            if (!TryNormalizeChestOriginFromFrame(tileType, x, y, frameX, frameY, out chestX, out chestY))
            {
                return;
            }

            var key = BuildChestPositionKey(chestX, chestY);
            if (added != null && added.Contains(key))
            {
                return;
            }

            var worldX = BuildChestLabelWorldX(chestX, tileType);
            var worldY = BuildChestLabelWorldY(chestY, tileType);
            if (!CanCacheChestLabel(context, worldX, worldY))
            {
                return;
            }

            if (added != null)
            {
                added.Add(key);
            }

            candidates.Add(new ChestScanCandidate
            {
                ChestX = chestX,
                ChestY = chestY,
                Key = key,
                TileType = tileType,
                TileStyle = BuildChestTileStyle(tileType, frameX),
                WorldX = worldX,
                WorldY = worldY
            });
        }

        private static string ResolveChestNameWithCache(InformationWorldContext context, ChestScanCandidate candidate, string languageSignature)
        {
            string recordSignature;
            string loadedName;
            if (!TryReadChestRecordIdentityAt(context == null ? null : context.MainType, candidate.ChestX, candidate.ChestY, out recordSignature, out loadedName))
            {
                recordSignature = "missing";
                loadedName = string.Empty;
            }

            var key = new ChestNameCacheKey(
                context == null ? string.Empty : context.WorldKey,
                context == null ? string.Empty : context.WorldRecordKey,
                candidate.ChestX,
                candidate.ChestY,
                candidate.TileType,
                candidate.TileStyle,
                languageSignature,
                recordSignature);

            string cached;
            if (ChestAlwaysNameCache.TryGetValue(key, out cached))
            {
                unchecked
                {
                    _chestAlwaysNameCacheHitCount++;
                }

                return cached;
            }

            unchecked
            {
                _chestAlwaysNameCacheMissCount++;
            }

            var name = !string.IsNullOrWhiteSpace(loadedName)
                ? loadedName
                : ResolveChestTileDisplayName(context == null ? null : context.MainType, candidate.TileType, candidate.TileStyle);
            name = string.IsNullOrWhiteSpace(name) ? DefaultChestLabelName(candidate.TileType) : name;
            StoreChestNameCache(key, name, candidate.TileType);
            return name;
        }

        private static void StoreChestNameCache(ChestNameCacheKey key, string name, int tileType)
        {
            if (!ChestAlwaysNameCache.ContainsKey(key))
            {
                ChestAlwaysNameCacheOrder.Enqueue(key);
            }

            ChestAlwaysNameCache[key] = string.IsNullOrWhiteSpace(name) ? DefaultChestLabelName(tileType) : name;
            while (ChestAlwaysNameCacheOrder.Count > ChestAlwaysNameCacheLimit)
            {
                var oldest = ChestAlwaysNameCacheOrder.Dequeue();
                ChestAlwaysNameCache.Remove(oldest);
            }
        }

        private static string BuildChestAlwaysTypedTileFastPathStatus(int typedTileReads, int fallbackTileReads, int failedTileReads)
        {
            if (typedTileReads <= 0 && fallbackTileReads <= 0 && failedTileReads <= 0)
            {
                return "none";
            }

            return "typed=" + typedTileReads.ToString(CultureInfo.InvariantCulture) +
                   ";fallback=" + fallbackTileReads.ToString(CultureInfo.InvariantCulture) +
                   ";failed=" + failedTileReads.ToString(CultureInfo.InvariantCulture);
        }

        private static int BuildChestTileStyle(int tileType, int frameX)
        {
            if (tileType < 0 || frameX < 0)
            {
                return 0;
            }

            return Math.Max(0, frameX / GetChestTileStyleFrameWidth(tileType));
        }

        private static float BuildChestLabelWorldX(int chestX, int tileType)
        {
            return (chestX * TileSize) + (GetChestTileFrameColumns(tileType) * TileSize * 0.5f);
        }

        private static float BuildChestLabelWorldY(int chestY, int tileType)
        {
            return (chestY * TileSize) + (GetChestTileFrameRows(tileType) * TileSize * 0.5f);
        }

        private static string BuildChestNameLanguageSignature()
        {
            return CultureInfo.CurrentCulture.Name + "/" +
                   CultureInfo.CurrentUICulture.Name + "/" +
                   ReadTerrariaLanguageSignature();
        }

        private static string ReadTerrariaLanguageSignature()
        {
            try
            {
                var managerType = InformationReflection.FindType("Terraria.Localization.LanguageManager");
                var manager = InformationReflection.GetStaticMember(managerType, "Instance");
                var activeCulture = InformationReflection.GetMember(manager, "ActiveCulture");
                var cultureName = FirstNonEmpty(
                    InformationReflection.TryReadString(activeCulture, "Name"),
                    InformationReflection.TryReadString(activeCulture, "CultureInfoName"),
                    InformationReflection.TryReadString(activeCulture, "LegacyId"));
                if (!string.IsNullOrWhiteSpace(cultureName))
                {
                    return cultureName.Trim();
                }

                return activeCulture == null ? string.Empty : Convert.ToString(activeCulture, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryGetChestTileScanBounds(InformationWorldContext context, object tiles, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = 0;
            maxX = -1;
            minY = 0;
            maxY = -1;

            int worldMaxX;
            int worldMaxY;
            if (!TryReadTileWorldBounds(context == null ? null : context.MainType, tiles, out worldMaxX, out worldMaxY) ||
                worldMaxX <= 0 ||
                worldMaxY <= 0)
            {
                return false;
            }

            minX = Math.Max(0, (int)Math.Floor(((context == null ? 0f : context.ScreenX) - ChestCacheCullPadding) / TileSize) - 2);
            maxX = Math.Min(worldMaxX - 1, (int)Math.Ceiling(((context == null ? 0f : context.ScreenX) + (context == null ? 0 : context.ScreenWidth) + ChestCacheCullPadding) / TileSize) + 2);
            minY = Math.Max(0, (int)Math.Floor(((context == null ? 0f : context.ScreenY) - ChestCacheCullPadding) / TileSize) - 2);
            maxY = Math.Min(worldMaxY - 1, (int)Math.Ceiling(((context == null ? 0f : context.ScreenY) + (context == null ? 0 : context.ScreenHeight) + ChestCacheCullPadding) / TileSize) + 2);
            return maxX >= minX && maxY >= minY;
        }

        private static bool TryReadTileWorldBounds(Type mainType, object tiles, out int maxX, out int maxY)
        {
            maxX = 0;
            maxY = 0;
            try
            {
                maxX = TerrariaMainCompat.MaxTilesX;
                maxY = TerrariaMainCompat.MaxTilesY;
                if (maxX > 0 && maxY > 0)
                {
                    return true;
                }
            }
            catch
            {
            }

            if (InformationReflection.TryReadStaticInt(mainType, "maxTilesX", out maxX) &&
                InformationReflection.TryReadStaticInt(mainType, "maxTilesY", out maxY) &&
                maxX > 0 &&
                maxY > 0)
            {
                return true;
            }

            var array = tiles as Array;
            if (array != null)
            {
                if (array.Rank == 2)
                {
                    maxX = array.GetLength(0);
                    maxY = array.GetLength(1);
                    return maxX > 0 && maxY > 0;
                }

                if (array.Rank == 1 && array.GetLength(0) > 0)
                {
                    maxX = array.GetLength(0);
                    maxY = GetCollectionCount(array.GetValue(0));
                    return maxY > 0;
                }
            }

            var list = tiles as IList;
            if (list != null && list.Count > 0)
            {
                maxX = list.Count;
                maxY = GetCollectionCount(list[0]);
                return maxY > 0;
            }

            return false;
        }

        private static bool TryReadTileActiveType(object tiles, int x, int y, out bool active, out int tileType)
        {
            active = false;
            tileType = -1;

            try
            {
                Tile typedTile;
                if (TerrariaTileReadCompat.TryGetTile(x, y, out typedTile))
                {
                    active = TerrariaTileReadCompat.IsActive(typedTile);
                    tileType = TerrariaTileReadCompat.Type(typedTile);
                    return true;
                }
            }
            catch
            {
            }

            var tile = InformationTileAccess.GetTileAt(tiles, x, y);
            if (tile == null)
            {
                return false;
            }

            active = IsTileActive(tile);
            tileType = ReadTileType(tile);
            return true;
        }

        private static bool TryReadTileActiveTypeAndFrame(object tiles, int x, int y, out bool active, out int tileType, out int frameX, out int frameY)
        {
            bool usedTypedTileRead;
            return TryReadTileActiveTypeAndFrame(
                tiles,
                x,
                y,
                out active,
                out tileType,
                out frameX,
                out frameY,
                true,
                out usedTypedTileRead);
        }

        private static bool TryReadTileActiveTypeAndFrame(object tiles, int x, int y, out bool active, out int tileType, out int frameX, out int frameY, bool allowTypedTileRead, out bool usedTypedTileRead)
        {
            active = false;
            tileType = -1;
            frameX = 0;
            frameY = 0;
            usedTypedTileRead = false;

            if (allowTypedTileRead)
            {
                try
                {
                    Tile typedTile;
                    if (TerrariaTileReadCompat.TryGetTile(x, y, out typedTile))
                    {
                        active = TerrariaTileReadCompat.IsActive(typedTile);
                        tileType = TerrariaTileReadCompat.Type(typedTile);
                        frameX = TerrariaTileReadCompat.FrameX(typedTile);
                        frameY = TerrariaTileReadCompat.FrameY(typedTile);
                        usedTypedTileRead = true;
                        return true;
                    }
                }
                catch
                {
                }
            }

            var tile = InformationTileAccess.GetTileAt(tiles, x, y);
            if (tile == null)
            {
                return false;
            }

            active = IsTileActive(tile);
            tileType = ReadTileType(tile);
            frameX = ReadTileFrameX(tile);
            frameY = ReadTileFrameY(tile);
            return true;
        }

        private static bool CanUseTypedTileRead(object tiles)
        {
            try
            {
                return tiles != null && ReferenceEquals(tiles, TerrariaMainCompat.Tiles);
            }
            catch
            {
                return false;
            }
        }

        private static int ReadTileFrameX(object tile)
        {
            return InformationTileAccess.ReadFrameX(tile);
        }

        private static int ReadTileFrameY(object tile)
        {
            return InformationTileAccess.ReadFrameY(tile);
        }

        private static bool TryResolveLoadedChestNameAt(Type mainType, int x, int y, out string name)
        {
            name = string.Empty;
            string recordSignature;
            return TryReadChestRecordIdentityAt(mainType, x, y, out recordSignature, out name) &&
                   !string.IsNullOrWhiteSpace(name);
        }

        private static bool TryReadChestRecordIdentityAt(Type mainType, int x, int y, out string recordSignature, out string loadedName)
        {
            recordSignature = string.Empty;
            loadedName = string.Empty;
            try
            {
                var typedChestIndex = Chest.FindChest(x, y);
                Chest typedChest;
                if (TerrariaMainCompat.TryGetChest(typedChestIndex, out typedChest))
                {
                    loadedName = typedChest.name ?? string.Empty;
                    recordSignature = "typed:" +
                                      typedChestIndex.ToString(CultureInfo.InvariantCulture) +
                                      ":" +
                                      RuntimeHelpers.GetHashCode(typedChest).ToString(CultureInfo.InvariantCulture) +
                                      ":" +
                                      (loadedName ?? string.Empty);
                    return true;
                }
            }
            catch
            {
            }

            var chestType = InformationReflection.FindType("Terraria.Chest");
            object rawIndex;
            if (!InformationReflection.TryInvokeStatic(chestType, "FindChest", new object[] { x, y }, out rawIndex) || rawIndex == null)
            {
                return false;
            }

            int chestIndex;
            try
            {
                chestIndex = Convert.ToInt32(rawIndex, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }

            if (chestIndex < 0)
            {
                return false;
            }

            var chests = InformationReflection.GetStaticMember(mainType, "chest");
            var chest = InformationReflection.GetIndexedValue(chests, chestIndex);
            if (chest == null)
            {
                return false;
            }

            loadedName = FirstNonEmpty(
                InformationReflection.TryReadString(chest, "name"),
                InformationReflection.TryReadString(chest, "Name"));
            recordSignature = "ref:" +
                              chestIndex.ToString(CultureInfo.InvariantCulture) +
                              ":" +
                              RuntimeHelpers.GetHashCode(chest).ToString(CultureInfo.InvariantCulture) +
                              ":" +
                              (loadedName ?? string.Empty);
            return true;
        }

        private static bool TryNormalizeChestOriginFromFrame(int tileType, int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            chestX = tileX - PositiveModulo(frameX / TileFrameSize, GetChestTileFrameColumns(tileType));
            chestY = tileY - PositiveModulo(frameY / TileFrameSize, GetChestTileFrameRows(tileType));
            return chestX >= 0 && chestY >= 0;
        }

        private static int GetChestTileFrameColumns(int tileType)
        {
            return IsDresserTileType(tileType) ? DresserFrameColumns : ChestFrameColumns;
        }

        private static int GetChestTileFrameRows(int tileType)
        {
            return ChestFrameRows;
        }

        private static int GetChestTileStyleFrameWidth(int tileType)
        {
            return IsDresserTileType(tileType) ? DresserStyleFrameWidth : ChestStyleFrameWidth;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static bool IsChestTileType(Type mainType, int tileType)
        {
            return tileType == ChestTileTypeContainers ||
                   tileType == ChestTileTypeContainers2 ||
                   tileType == ChestTileTypeDressers ||
                   tileType == ChestTileTypeFakeContainers ||
                   tileType == ChestTileTypeFakeContainers2;
        }

        private static bool IsDresserTileType(int tileType)
        {
            return tileType == ChestTileTypeDressers;
        }

        private static string ResolveChestTileDisplayName(Type mainType, int tileType, int tileStyle)
        {
            if (IsDresserTileType(tileType))
            {
                return ResolveDresserTileDisplayName(tileStyle);
            }

            if (tileType == ChestTileTypeContainers || tileType == ChestTileTypeFakeContainers)
            {
                return ResolvePrimaryChestTileDisplayName(tileStyle);
            }

            if (tileType == ChestTileTypeContainers2 || tileType == ChestTileTypeFakeContainers2)
            {
                return ResolveSecondaryChestTileDisplayName(tileType, tileStyle);
            }

            return DefaultChestLabelName(tileType);
        }

        private static string ResolvePrimaryChestTileDisplayName(int tileStyle)
        {
            var name = ResolveChestLocalizedTextValue("chestType", tileStyle, true);
            return string.IsNullOrWhiteSpace(name) ? DefaultChestLabelName(ChestTileTypeContainers) : name;
        }

        private static string ResolveSecondaryChestTileDisplayName(int tileType, int tileStyle)
        {
            if (tileType == ChestTileTypeContainers2 && tileStyle == 4)
            {
                var goldChestName = ResolveItemName(3988);
                if (!string.IsNullOrWhiteSpace(goldChestName) &&
                    !string.Equals(goldChestName, "3988", StringComparison.Ordinal))
                {
                    return goldChestName;
                }
            }

            var name = ResolveChestLocalizedTextValue("chestType2", tileStyle, false);
            return string.IsNullOrWhiteSpace(name) ? DefaultChestLabelName(tileType) : name;
        }

        private static string ResolveChestLocalizedTextValue(string memberName, int tileStyle, bool primary)
        {
            if (tileStyle < 0)
            {
                return string.Empty;
            }

            try
            {
                var chestTypes = primary ? Lang.chestType : Lang.chestType2;
                if (chestTypes != null && tileStyle < chestTypes.Length && chestTypes[tileStyle] != null)
                {
                    var name = chestTypes[tileStyle].Value;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
            catch
            {
            }

            try
            {
                var langType = InformationReflection.FindType("Terraria.Lang") ??
                               InformationReflection.FindType("Terraria.Localization.Lang");
                var chestTypes = InformationReflection.GetStaticMember(langType, memberName);
                var rawName = InformationReflection.GetIndexedValue(chestTypes, tileStyle);
                var name = FirstNonEmpty(
                    InformationReflection.TryReadString(rawName, "Value"),
                    Convert.ToString(rawName, CultureInfo.InvariantCulture));
                return string.IsNullOrWhiteSpace(name) ? string.Empty : name;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ResolveDresserTileDisplayName(int tileStyle)
        {
            if (tileStyle >= 0)
            {
                try
                {
                    var dresserTypes = Lang.dresserType;
                    if (dresserTypes != null && tileStyle < dresserTypes.Length && dresserTypes[tileStyle] != null)
                    {
                        var name = dresserTypes[tileStyle].Value;
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            return name;
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    var langType = InformationReflection.FindType("Terraria.Lang") ??
                                   InformationReflection.FindType("Terraria.Localization.Lang");
                    var dresserTypes = InformationReflection.GetStaticMember(langType, "dresserType");
                    var rawName = InformationReflection.GetIndexedValue(dresserTypes, tileStyle);
                    var name = FirstNonEmpty(
                        InformationReflection.TryReadString(rawName, "Value"),
                        Convert.ToString(rawName, CultureInfo.InvariantCulture));
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
                catch
                {
                }
            }

            return DefaultChestLabelName(ChestTileTypeDressers);
        }

        private static string DefaultChestLabelName(int tileType)
        {
            return IsDresserTileType(tileType) ? "梳妆台" : "宝箱";
        }

        private static bool TryResolveChestTileInfoAt(InformationWorldContext context, int tileX, int tileY, out int tileType, out int tileStyle)
        {
            tileType = ChestTileTypeContainers;
            tileStyle = 0;
            if (context == null || context.MainType == null || tileX < 0 || tileY < 0)
            {
                return false;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                return false;
            }

            bool active;
            int frameX;
            int frameY;
            if (!TryReadTileActiveTypeAndFrame(
                    tiles,
                    tileX,
                    tileY,
                    out active,
                    out tileType,
                    out frameX,
                    out frameY) ||
                !active ||
                frameX < 0 ||
                frameY < 0 ||
                !IsChestTileType(context.MainType, tileType))
            {
                tileType = ChestTileTypeContainers;
                tileStyle = 0;
                return false;
            }

            tileStyle = BuildChestTileStyle(tileType, frameX);
            return true;
        }

        private static Dictionary<long, string> BuildChestNameLookup(Type mainType)
        {
            var result = new Dictionary<long, string>();
            try
            {
                var typedChests = TerrariaMainCompat.Chests;
                if (typedChests != null)
                {
                    for (var index = 0; index < typedChests.Length; index++)
                    {
                        var chest = typedChests[index];
                        if (chest == null || chest.x <= 0 || chest.y <= 0)
                        {
                            continue;
                        }

                        result[BuildChestPositionKey(chest.x, chest.y)] = chest.name ?? string.Empty;
                    }

                    return result;
                }
            }
            catch
            {
                result.Clear();
            }

            var chests = InformationReflection.GetStaticMember(mainType, "chest");
            var count = GetCollectionCount(chests);
            for (var index = 0; index < count; index++)
            {
                var chest = InformationReflection.GetIndexedValue(chests, index);
                if (chest == null)
                {
                    continue;
                }

                int chestX;
                int chestY;
                if (!InformationReflection.TryReadInt(chest, "x", out chestX) ||
                    !InformationReflection.TryReadInt(chest, "y", out chestY) ||
                    chestX <= 0 ||
                    chestY <= 0)
                {
                    continue;
                }

                var name = FirstNonEmpty(
                    InformationReflection.TryReadString(chest, "name"),
                    InformationReflection.TryReadString(chest, "Name"));
                result[BuildChestPositionKey(chestX, chestY)] = name ?? string.Empty;
            }

            return result;
        }

        private static List<SignTextLabel> GetSignTextLabels(InformationWorldContext context)
        {
            lock (SyncRoot)
            {
                if (context.GameUpdateCount != 0 &&
                    _lastSignScanTick != 0 &&
                    context.GameUpdateCount >= _lastSignScanTick &&
                    context.GameUpdateCount - _lastSignScanTick < SignScanIntervalTicks)
                {
                    return CachedSignTextLabels;
                }

                CachedSignTextLabels.Clear();
                AddAllSignTextLabels(context, CachedSignTextLabels, false);
                _lastSignScanTick = context.GameUpdateCount;
                return CachedSignTextLabels;
            }
        }

        private static List<SignTextLabel> GetTombstoneTextLabels(InformationWorldContext context)
        {
            lock (SyncRoot)
            {
                if (context.GameUpdateCount != 0 &&
                    _lastTombstoneScanTick != 0 &&
                    context.GameUpdateCount >= _lastTombstoneScanTick &&
                    context.GameUpdateCount - _lastTombstoneScanTick < SignScanIntervalTicks)
                {
                    return CachedTombstoneTextLabels;
                }

                CachedTombstoneTextLabels.Clear();
                AddAllSignTextLabels(context, CachedTombstoneTextLabels, true);
                _lastTombstoneScanTick = context.GameUpdateCount;
                return CachedTombstoneTextLabels;
            }
        }

        private static void AddAllSignTextLabels(InformationWorldContext context, IList<SignTextLabel> labels, bool tombstoneLabels)
        {
            if (context == null || context.MainType == null || labels == null)
            {
                return;
            }

            var signs = InformationReflection.GetStaticMember(context.MainType, "sign");
            var count = GetCollectionCount(signs);
            for (var index = 0; index < count; index++)
            {
                var sign = InformationReflection.GetIndexedValue(signs, index);
                if (sign == null)
                {
                    continue;
                }

                int signX;
                int signY;
                if (!InformationReflection.TryReadInt(sign, "x", out signX) ||
                    !InformationReflection.TryReadInt(sign, "y", out signY) ||
                    signX <= 0 ||
                    signY <= 0)
                {
                    continue;
                }

                var text = InformationReflection.TryReadString(sign, "text");
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var worldLeft = signX * TileSize;
                var worldTop = signY * TileSize;
                var worldRight = worldLeft + TileSize * 2;
                if (!LabelRenderer.CanDraw(context, worldLeft, worldTop, 1600f, false))
                {
                    continue;
                }

                if (!IsValidSignTile(context, signX, signY, tombstoneLabels))
                {
                    continue;
                }

                labels.Add(new SignTextLabel
                {
                    TileX = signX,
                    TileY = signY,
                    WorldLeft = worldLeft,
                    WorldTop = worldTop,
                    WorldRight = worldRight,
                    Text = text,
                    TextHash = HashText(text)
                });
            }
        }

        private static bool IsValidSignTile(InformationWorldContext context, int tileX, int tileY, bool tombstoneLabels)
        {
            if (context == null || context.MainType == null)
            {
                return false;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            bool active;
            int tileType;
            if (!TryReadTileActiveType(tiles, tileX, tileY, out active, out tileType) || !active)
            {
                return false;
            }

            if (tileType < 0)
            {
                return false;
            }

            if (!IsTileSignType(context.MainType, tileType))
            {
                return false;
            }

            var isTombstone = IsTombstoneTileType(tileType);
            return tombstoneLabels ? isTombstone : !isTombstone;
        }

        private static bool IsTileSignType(Type mainType, int tileType)
        {
            var tileSign = InformationReflection.GetStaticMember(mainType, "tileSign");
            var raw = InformationReflection.GetIndexedValue(tileSign, tileType);
            try
            {
                return raw != null && Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTombstoneTileType(int tileType)
        {
            EnsureTileIdsResolved();
            return tileType == _tombstoneTileType;
        }

        private static long BuildChestPositionKey(int x, int y)
        {
            unchecked
            {
                return ((long)(x & 0x7fffffff) << 32) | (uint)y;
            }
        }

        private static List<TileHighlight> GetTileHighlights(InformationWorldContext context, AppSettings settings)
        {
            lock (SyncRoot)
            {
                var signature = BuildTileHighlightScanSignature(context, settings);
                if (!ShouldRefreshTileHighlights(context, signature.Hash))
                {
                    return CachedTileHighlights;
                }

                CachedTileHighlights.Clear();
                ScanTileHighlights(context, settings, signature.Bounds, BuildTileHighlightColors(settings), CachedTileHighlights);
                _lastTileScanTick = context == null ? 0 : context.GameUpdateCount;
                _lastTileHighlightSignatureHash = signature.Hash;
                return CachedTileHighlights;
            }
        }

        private static bool ShouldRefreshTileHighlights(InformationWorldContext context, uint currentSignatureHash)
        {
            return ShouldRefreshTileHighlightsCore(
                _lastTileScanTick,
                _lastTileHighlightSignatureHash,
                context == null ? 0 : context.GameUpdateCount,
                currentSignatureHash);
        }

        private static bool ShouldRefreshTileHighlightsCore(ulong lastScanTick, uint previousSignatureHash, ulong currentTick, uint currentSignatureHash)
        {
            if (lastScanTick == 0 || previousSignatureHash != currentSignatureHash)
            {
                return true;
            }

            if (currentTick == 0)
            {
                return false;
            }

            if (currentTick < lastScanTick)
            {
                return true;
            }

            return currentTick - lastScanTick >= TileScanIntervalTicks;
        }

        private static TileHighlightScanSignature BuildTileHighlightScanSignature(InformationWorldContext context, AppSettings settings)
        {
            var bounds = BuildTileHighlightScanBounds(context);
            var enabledMask = BuildTileHighlightEnabledMask(settings);
            unchecked
            {
                var hash = 2166136261u;
                AddHashValue(ref hash, context == null ? string.Empty : context.WorldKey);
                AddHashValue(ref hash, context == null ? string.Empty : context.WorldRecordKey);
                AddHashInt(ref hash, bounds.MinX);
                AddHashInt(ref hash, bounds.MinY);
                AddHashInt(ref hash, bounds.MaxX);
                AddHashInt(ref hash, bounds.MaxY);
                AddHashInt(ref hash, BuildTileHighlightPlayerChunkX(context));
                AddHashInt(ref hash, BuildTileHighlightPlayerChunkY(context));
                AddHashInt(ref hash, enabledMask);
                if ((enabledMask & TileHighlightLifeCrystalMask) != 0)
                {
                    AddHashValue(ref hash, BuildTileHighlightColorSignature(settings == null ? null : settings.InformationLifeCrystalHighlightColor));
                }

                if ((enabledMask & TileHighlightManaCrystalMask) != 0)
                {
                    AddHashValue(ref hash, BuildTileHighlightColorSignature(settings == null ? null : settings.InformationManaCrystalHighlightColor));
                }

                if ((enabledMask & TileHighlightLifeFruitMask) != 0)
                {
                    AddHashValue(ref hash, BuildTileHighlightColorSignature(settings == null ? null : settings.InformationLifeFruitHighlightColor));
                }

                if ((enabledMask & TileHighlightDragonEggMask) != 0)
                {
                    AddHashValue(ref hash, BuildTileHighlightColorSignature(settings == null ? null : settings.InformationDragonEggHighlightColor));
                }

                return new TileHighlightScanSignature(hash, bounds);
            }
        }

        private static TileHighlightScanBounds BuildTileHighlightScanBounds(InformationWorldContext context)
        {
            var screenX = context == null ? 0f : context.ScreenX;
            var screenY = context == null ? 0f : context.ScreenY;
            var screenWidth = context == null ? 0 : context.ScreenWidth;
            var screenHeight = context == null ? 0 : context.ScreenHeight;
            var minX = Math.Max(0, (int)Math.Floor(screenX / TileSize) - TileHighlightScanMarginTiles);
            var minY = Math.Max(0, (int)Math.Floor(screenY / TileSize) - TileHighlightScanMarginTiles);
            var maxX = Math.Max(minX, (int)Math.Ceiling((screenX + screenWidth) / TileSize) + TileHighlightScanMarginTiles);
            var maxY = Math.Max(minY, (int)Math.Ceiling((screenY + screenHeight) / TileSize) + TileHighlightScanMarginTiles);
            return new TileHighlightScanBounds(minX, minY, maxX, maxY);
        }

        private static int BuildTileHighlightEnabledMask(AppSettings settings)
        {
            var mask = 0;
            if (settings == null)
            {
                return mask;
            }

            if (settings.InformationHighlightLifeCrystalEnabled) mask |= TileHighlightLifeCrystalMask;
            if (settings.InformationHighlightManaCrystalEnabled) mask |= TileHighlightManaCrystalMask;
            if (settings.InformationHighlightDigtoiseEnabled) mask |= TileHighlightDigtoiseMask;
            if (settings.InformationHighlightLifeFruitEnabled) mask |= TileHighlightLifeFruitMask;
            if (settings.InformationHighlightDragonEggEnabled) mask |= TileHighlightDragonEggMask;
            return mask;
        }

        private static int BuildTileHighlightPlayerChunkX(InformationWorldContext context)
        {
            var tileX = context == null ? 0 : (int)Math.Floor(context.PlayerCenterX / TileSize);
            return FloorDiv(tileX, TileHighlightPlayerChunkTiles);
        }

        private static int BuildTileHighlightPlayerChunkY(InformationWorldContext context)
        {
            var tileY = context == null ? 0 : (int)Math.Floor(context.PlayerCenterY / TileSize);
            return FloorDiv(tileY, TileHighlightPlayerChunkTiles);
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

        private static string BuildTileHighlightColorSignature(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static TileHighlightColors BuildTileHighlightColors(AppSettings settings)
        {
            return new TileHighlightColors(
                InformationColorHelper.LifeCrystal(settings),
                InformationColorHelper.ManaCrystal(settings),
                InformationColorHelper.Digtoise(settings),
                InformationColorHelper.LifeFruit(settings),
                InformationColorHelper.DragonEgg(settings));
        }

        private static void ScanTileHighlights(InformationWorldContext context, AppSettings settings, TileHighlightScanBounds bounds, TileHighlightColors colors, IList<TileHighlight> results)
        {
            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                LogThrottle.WarnThrottled(
                    "information-main-tile-unavailable",
                    TimeSpan.FromSeconds(30),
                    "InformationOverlayService",
                    "Main.tile is unavailable; tile highlights skipped.");
                return;
            }

            EnsureTileIdsResolved();
            var lifeCrystalType = _lifeCrystalTileType;
            var manaCrystalType = _manaCrystalTileType;
            var digtoiseType = _digtoiseTileType;
            var lifeFruitType = _lifeFruitTileType;
            var dragonEggType = _dragonEggTileType;
            if (dragonEggType < 0 && settings.InformationHighlightDragonEggEnabled && !_dragonEggMissingLogged)
            {
                _dragonEggMissingLogged = true;
                LogThrottle.WarnThrottled(
                    "information-dragon-egg-tileid-unavailable",
                    TimeSpan.FromMinutes(1),
                    "InformationOverlayService",
                    "TileID.DragonEgg is unavailable; dragon egg highlight skipped.");
            }

            var minX = bounds.MinX;
            var minY = bounds.MinY;
            var maxX = bounds.MaxX;
            var maxY = bounds.MaxY;
            TileHighlightVisited.Clear();
            TileHighlightStack.Clear();
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    bool active;
                    int tileType;
                    if (!TryReadTileActiveType(tiles, x, y, out active, out tileType) || !active)
                    {
                        continue;
                    }

                    if (settings.InformationHighlightLifeCrystalEnabled && tileType == lifeCrystalType)
                    {
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.LifeCrystal, TileHighlightVisited, TileHighlightStack, results);
                    }
                    else if (settings.InformationHighlightManaCrystalEnabled && tileType == manaCrystalType)
                    {
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.ManaCrystal, TileHighlightVisited, TileHighlightStack, results);
                    }
                    else if (settings.InformationHighlightDigtoiseEnabled && tileType == digtoiseType)
                    {
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.Digtoise, TileHighlightVisited, TileHighlightStack, results);
                    }
                    else if (settings.InformationHighlightLifeFruitEnabled && tileType == lifeFruitType)
                    {
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.LifeFruit, TileHighlightVisited, TileHighlightStack, results);
                    }
                    else if (settings.InformationHighlightDragonEggEnabled && dragonEggType >= 0 && tileType == dragonEggType)
                    {
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.DragonEgg, TileHighlightVisited, TileHighlightStack, results);
                    }
                }
            }

            TileHighlightStack.Clear();
            TileHighlightVisited.Clear();
        }

        private static void AddTileHighlightGroup(object tiles, int startX, int startY, int minX, int minY, int maxX, int maxY, int tileType, InformationColor color, ISet<long> visited, IList<TilePoint> stack, IList<TileHighlight> results)
        {
            var startKey = BuildTileVisitKey(tileType, startX, startY);
            if (visited.Contains(startKey))
            {
                return;
            }

            stack.Clear();
            stack.Add(new TilePoint(startX, startY));
            var groupMinX = startX;
            var groupMaxX = startX;
            var groupMinY = startY;
            var groupMaxY = startY;
            var matched = 0;

            while (stack.Count > 0 && matched < 64)
            {
                var last = stack.Count - 1;
                var point = stack[last];
                stack.RemoveAt(last);

                if (point.X < minX || point.X > maxX || point.Y < minY || point.Y > maxY)
                {
                    continue;
                }

                var key = BuildTileVisitKey(tileType, point.X, point.Y);
                if (visited.Contains(key))
                {
                    continue;
                }

                bool active;
                int currentTileType;
                if (!TryReadTileActiveType(tiles, point.X, point.Y, out active, out currentTileType) ||
                    !active ||
                    currentTileType != tileType)
                {
                    continue;
                }

                visited.Add(key);
                matched++;
                groupMinX = Math.Min(groupMinX, point.X);
                groupMaxX = Math.Max(groupMaxX, point.X);
                groupMinY = Math.Min(groupMinY, point.Y);
                groupMaxY = Math.Max(groupMaxY, point.Y);

                stack.Add(new TilePoint(point.X - 1, point.Y));
                stack.Add(new TilePoint(point.X + 1, point.Y));
                stack.Add(new TilePoint(point.X, point.Y - 1));
                stack.Add(new TilePoint(point.X, point.Y + 1));
            }

            if (matched <= 0)
            {
                return;
            }

            results.Add(new TileHighlight(
                groupMinX,
                groupMinY,
                groupMaxX - groupMinX + 1,
                groupMaxY - groupMinY + 1,
                color));
        }

        private static bool DrawTileHighlightFrame(object spriteBatch, int x, int y, int width, int height, InformationColor color, int alpha)
        {
            var outerX = x - 3;
            var outerY = y - 3;
            var outerWidth = width + 6;
            var outerHeight = height + 6;
            var corner = Math.Max(8, Math.Min(18, Math.Min(outerWidth, outerHeight) / 2));
            var ok = UiPrimitiveRenderer.DrawRectBorder(spriteBatch, outerX, outerY, outerWidth, outerHeight, 1, color.R, color.G, color.B, alpha);
            ok |= UiPrimitiveRenderer.DrawRectBorder(spriteBatch, outerX - 2, outerY - 2, outerWidth + 4, outerHeight + 4, 1, 255, 255, 255, 120);
            ok |= DrawCorner(spriteBatch, outerX - 1, outerY - 1, corner, 1, 1, color, Math.Min(255, alpha + 20));
            ok |= DrawCorner(spriteBatch, outerX + outerWidth, outerY - 1, corner, -1, 1, color, Math.Min(255, alpha + 20));
            ok |= DrawCorner(spriteBatch, outerX - 1, outerY + outerHeight, corner, 1, -1, color, Math.Min(255, alpha + 20));
            ok |= DrawCorner(spriteBatch, outerX + outerWidth, outerY + outerHeight, corner, -1, -1, color, Math.Min(255, alpha + 20));
            return ok;
        }

        private static bool DrawCorner(object spriteBatch, int x, int y, int length, int horizontalDirection, int verticalDirection, InformationColor color, int alpha)
        {
            var thickness = 3;
            var horizontalX = horizontalDirection > 0 ? x : x - length;
            var verticalY = verticalDirection > 0 ? y : y - length;
            var ok = UiPrimitiveRenderer.DrawFilledRect(spriteBatch, horizontalX, y, length, thickness, color.R, color.G, color.B, alpha);
            ok |= UiPrimitiveRenderer.DrawFilledRect(spriteBatch, x, verticalY, thickness, length, color.R, color.G, color.B, alpha);
            return ok;
        }

        private static long BuildTileVisitKey(int tileType, int x, int y)
        {
            unchecked
            {
                return ((long)(tileType & 0xffff) << 48) |
                       ((long)(x & 0x00ffffff) << 24) |
                       (uint)(y & 0x00ffffff);
            }
        }

        private static void RecordOpenChest(InformationWorldContext context, AppSettings settings)
        {
            int chestIndex;
            var typedPlayer = context == null ? null : context.LocalPlayer as Player;
            if (typedPlayer != null)
            {
                chestIndex = TerrariaPlayerReadCompat.ChestIndex(typedPlayer);
            }
            else if (!InformationReflection.TryReadInt(context == null ? null : context.LocalPlayer, "chest", out chestIndex))
            {
                return;
            }

            if (chestIndex < 0)
            {
                return;
            }

            int x;
            int y;
            Chest typedChest;
            if (TerrariaMainCompat.TryGetChest(chestIndex, out typedChest))
            {
                x = typedChest.x;
                y = typedChest.y;
            }
            else
            {
                var chests = InformationReflection.GetStaticMember(context.MainType, "chest");
                var chest = InformationReflection.GetIndexedValue(chests, chestIndex);
                if (chest == null ||
                    !InformationReflection.TryReadInt(chest, "x", out x) ||
                    !InformationReflection.TryReadInt(chest, "y", out y))
                {
                    return;
                }
            }

            if (x <= 0 || y <= 0)
            {
                return;
            }

            bool added;
            string message;
            if (!PlayerWorldBehaviorStore.TryRecordOpenedChest(
                    BuildBehaviorContext(context),
                    x,
                    y,
                    "Information.OpenChest",
                    out added,
                    out message))
            {
                LogThrottle.WarnThrottled(
                    "information-opened-chest-record-failed",
                    TimeSpan.FromSeconds(30),
                    "InformationOverlayService",
                    "Opened chest record skipped: " + message);
                return;
            }

            if (added)
            {
                InvalidateChestLabelCache();
            }
        }

        private static int ImportLegacyKnownChests(InformationWorldContext context, AppSettings settings)
        {
            return ImportLegacyKnownChestsCore(context, settings, true);
        }

        private static int ImportLegacyKnownChestsCore(InformationWorldContext context, AppSettings settings, bool saveConfig)
        {
            if (context == null ||
                settings == null ||
                settings.InformationKnownChestKeys == null ||
                settings.InformationKnownChestKeys.Count == 0)
            {
                return 0;
            }

            var behaviorContext = BuildBehaviorContext(context);
            if (!PlayerWorldBehaviorStore.IsUsable(behaviorContext))
            {
                return 0;
            }

            var imported = new List<PlayerWorldOpenedChestRecord>();
            var remaining = new List<string>();
            for (var index = 0; index < settings.InformationKnownChestKeys.Count; index++)
            {
                var legacyKey = settings.InformationKnownChestKeys[index];
                int x;
                int y;
                if (TryParseChestKey(legacyKey, context.WorldKey, out x, out y))
                {
                    imported.Add(new PlayerWorldOpenedChestRecord
                    {
                        X = x,
                        Y = y,
                        Source = "LegacyInformationKnownChestKeys"
                    });
                    continue;
                }

                remaining.Add(legacyKey);
            }

            if (imported.Count == 0)
            {
                return 0;
            }

            var added = PlayerWorldBehaviorStore.ImportOpenedChests(
                behaviorContext,
                imported,
                "LegacyInformationKnownChestKeys");

            settings.InformationKnownChestKeys = remaining;
            if (saveConfig)
            {
                ConfigService.SaveAll();
            }

            if (added > 0)
            {
                InvalidateChestLabelCache();
            }

            return added;
        }

        private static PlayerWorldBehaviorContext BuildBehaviorContext(InformationWorldContext context)
        {
            if (context == null)
            {
                return new PlayerWorldBehaviorContext();
            }

            return new PlayerWorldBehaviorContext
            {
                PlayerKey = context.PlayerRecordKey ?? string.Empty,
                WorldKey = context.WorldRecordKey ?? string.Empty,
                PlayerName = context.PlayerName ?? string.Empty,
                WorldName = context.WorldName ?? string.Empty
            };
        }

        private static void InvalidateChestLabelCache()
        {
            lock (SyncRoot)
            {
                ResetChestLabelCacheStateLocked();
            }
        }

        private static void ResetChestLabelCacheStateLocked()
        {
            CachedAlwaysChestLabels = EmptyChestLabels;
            CachedOpenedChestLabels = EmptyChestLabels;
            CachedSortedChestLabels = EmptyChestLabels;
            ChestAlwaysNameCache.Clear();
            ChestAlwaysNameCacheOrder.Clear();
            ChestScanCandidateBuffer.Clear();
            ResetChestAlwaysPartialScanState();
            _lastSortedChestLabelSource = EmptyChestLabels;
            _lastChestAlwaysScanTick = 0;
            _lastChestAlwaysSignatureHash = 0;
            _lastChestAlwaysSignature = new ChestLabelCacheSignature();
            _lastChestAlwaysStableSourceSignatureHash = 0;
            _lastChestOpenedScanTick = 0;
            _lastChestOpenedSignatureHash = 0;
            _lastChestOpenedSignature = new ChestLabelCacheSignature();
            _lastChestSortSignatureHash = 0;
            _lastChestSortPlayerCenterX = 0f;
            _lastChestSortPlayerCenterY = 0f;
            _lastChestSortScreenCenterX = 0f;
            _lastChestSortScreenCenterY = 0f;
            _openedChestsHashDirty = true;
        }

        private static string BuildBiomeLine(object player)
        {
            var zones = new List<string>();
            AddZone(zones, player, "ZoneDesert", "沙漠");
            AddZone(zones, player, "ZoneUndergroundDesert", "地下沙漠");
            AddZone(zones, player, "ZoneSnow", "雪原");
            AddZone(zones, player, "ZoneJungle", "丛林");
            AddZone(zones, player, "ZoneDungeon", "地牢");
            AddZone(zones, player, "ZoneBeach", "海洋");
            AddZone(zones, player, "ZoneCorrupt", "腐化");
            AddZone(zones, player, "ZoneCrimson", "猩红");
            AddZone(zones, player, "ZoneHallow", "神圣");
            AddZone(zones, player, "ZoneHoly", "神圣");
            AddZone(zones, player, "ZoneGlowshroom", "发光蘑菇");
            AddZone(zones, player, "ZoneMeteor", "陨石");
            AddZone(zones, player, "ZoneGranite", "花岗岩");
            AddZone(zones, player, "ZoneMarble", "大理石");
            AddZone(zones, player, "ZoneHive", "蜂巢");
            AddZone(zones, player, "ZoneLihzhardTemple", "神庙");
            AddZone(zones, player, "ZoneGraveyard", "墓地");

            if (HasZone(player, "ZoneSkyHeight"))
            {
                AddUnique(zones, "天空");
            }
            else if (HasZone(player, "ZoneUnderworldHeight"))
            {
                AddUnique(zones, "地狱");
            }
            else if (HasZone(player, "ZoneRockLayerHeight"))
            {
                AddUnique(zones, "洞穴");
            }
            else if (HasZone(player, "ZoneDirtLayerHeight") || HasZone(player, "ShoppingZone_BelowSurface"))
            {
                AddUnique(zones, "地下");
            }
            else if (HasZone(player, "ZoneOverworldHeight") && zones.Count <= 0)
            {
                AddUnique(zones, "森林");
            }

            return "群系: " + (zones.Count <= 0 ? "N/A" : string.Join(" / ", zones.ToArray()));
        }

        private static string BuildWorldInfectionLine(InformationWorldContext context)
        {
            if (!HasSavedNpc(context, "savedDryad", "Dryad", 20))
            {
                return string.Empty;
            }

            var worldGen = InformationReflection.FindType("Terraria.WorldGen");
            double good;
            double evil;
            double blood;
            var hasGood = TryReadStaticNumber(worldGen, "tGood", out good);
            var hasEvil = TryReadStaticNumber(worldGen, "tEvil", out evil);
            var hasBlood = TryReadStaticNumber(worldGen, "tBlood", out blood);
            if (!hasGood && !hasEvil && !hasBlood)
            {
                return string.Empty;
            }

            return "感染信息 神圣:" + FormatPercentLike(good) +
                   " 腐化:" + FormatPercentLike(evil) +
                   " 猩红:" + FormatPercentLike(blood);
        }

        private static void AddLuckLines(ICollection<InformationStatusLine> lines, int order, InformationWorldContext context, InformationColor color, double fontScale)
        {
            if (!HasSavedNpc(context, "savedWizard", "Wizard", 108))
            {
                return;
            }

            IList<string> luckLines;
            if (!InformationLuckBreakdownBuilder.TryBuildDisplayLines(context, out luckLines))
            {
                return;
            }

            for (var index = 0; index < luckLines.Count; index++)
            {
                AddLine(lines, order + index, luckLines[index], color, fontScale);
            }
        }

        private static string BuildAnglerQuestLine(InformationWorldContext context)
        {
            if (!HasSavedNpc(context, "savedAngler", "Angler", 369))
            {
                return string.Empty;
            }

            int questIndex;
            InformationReflection.TryReadStaticInt(context.MainType, "anglerQuest", out questIndex);
            var itemIds = InformationReflection.GetStaticMember(context.MainType, "anglerQuestItemNetIDs");
            var rawItem = InformationReflection.GetIndexedValue(itemIds, questIndex);
            var itemId = ToInt(rawItem, questIndex);
            var itemName = ResolveItemName(itemId);
            int finishedCount;
            InformationReflection.TryReadInt(context.LocalPlayer, "anglerQuestsFinished", out finishedCount);
            var line = "渔夫任务: " + (string.IsNullOrWhiteSpace(itemName) ? itemId.ToString(CultureInfo.InvariantCulture) : itemName) +
                       " / 完成:" + finishedCount.ToString(CultureInfo.InvariantCulture);
            var location = ResolveAnglerQuestLocation(context);
            if (!string.IsNullOrWhiteSpace(location))
            {
                line += " / 位置:" + location;
            }

            if (ReadAnglerQuestFinished(context))
            {
                line += " / 今日已交";
            }

            return line;
        }

        private static string ResolveAnglerQuestLocation(InformationWorldContext context)
        {
            var byItemText = ResolveAnglerQuestLocationFromItemText(context);
            if (!string.IsNullOrWhiteSpace(byItemText))
            {
                return byItemText;
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (!InformationReflection.TryInvokeStatic(langType, "AnglerQuestChat", new object[] { false }, out raw) || raw == null)
            {
                return string.Empty;
            }

            return ExtractAnglerQuestLocation(Convert.ToString(raw, CultureInfo.InvariantCulture));
        }

        private static string ResolveAnglerQuestLocationFromItemText(InformationWorldContext context)
        {
            int questIndex;
            InformationReflection.TryReadStaticInt(context.MainType, "anglerQuest", out questIndex);
            var itemIds = InformationReflection.GetStaticMember(context.MainType, "anglerQuestItemNetIDs");
            var rawItem = InformationReflection.GetIndexedValue(itemIds, questIndex);
            var itemId = ToInt(rawItem, 0);
            if (itemId <= 0)
            {
                return string.Empty;
            }

            var internalName = ResolveItemInternalName(itemId);
            if (string.IsNullOrWhiteSpace(internalName))
            {
                return string.Empty;
            }

            var questText = ReadLocalizedText("AnglerQuestText." + internalName);
            if (string.IsNullOrWhiteSpace(questText) ||
                string.Equals(questText, "AnglerQuestText." + internalName, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return ExtractAnglerQuestLocation(questText);
        }

        private static string ExtractAnglerQuestLocation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var location = ExtractAfterMarker(text, "抓捕位置：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "抓捕位置:", ")");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "抓捕地点：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "捕获地点：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "钓鱼地点：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "位置：", "）");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractAfterMarker(text, "Caught in ", ")");
            if (!string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            location = ExtractLastParenthesizedLocation(text);
            return location ?? string.Empty;
        }

        private static string ExtractAfterMarker(string text, string marker, string endMarker)
        {
            var start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return string.Empty;
            }

            start += marker.Length;
            var end = text.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
            {
                end = text.IndexOf('\n', start);
            }

            if (end < 0)
            {
                end = text.Length;
            }

            return text.Substring(start, Math.Max(0, end - start)).Trim();
        }

        private static string ExtractLastParenthesizedLocation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var start = Math.Max(text.LastIndexOf('（'), text.LastIndexOf('('));
            if (start < 0 || start >= text.Length - 1)
            {
                return string.Empty;
            }

            var end = text.IndexOfAny(new[] { '）', ')' }, start + 1);
            if (end < 0)
            {
                end = text.Length;
            }

            var value = text.Substring(start + 1, Math.Max(0, end - start - 1)).Trim();
            value = StripLocationPrefix(value);
            return value.Trim('（', '(', '）', ')', ' ', '\t', '。', '.', '，', ',', '、', '；', ';');
        }

        private static string StripLocationPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var prefixes = new[]
            {
                "抓捕位置：", "抓捕位置:", "抓捕地点：", "抓捕地点:",
                "捕获地点：", "捕获地点:", "钓鱼地点：", "钓鱼地点:",
                "位置：", "位置:", "Caught in ", "caught in "
            };
            for (var index = 0; index < prefixes.Length; index++)
            {
                if (value.StartsWith(prefixes[index], StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring(prefixes[index].Length).Trim();
                }
            }

            return value.Trim();
        }

        private static bool TryFindLocalBobber(InformationWorldContext context, out float x, out float y)
        {
            int identity;
            return TryFindLocalBobber(context, out x, out y, out identity);
        }

        private static bool TryFindLocalBobber(InformationWorldContext context, out float x, out float y, out int identity)
        {
            x = 0f;
            y = 0f;
            identity = -1;
            int myPlayer;
            InformationReflection.TryReadStaticInt(context.MainType, "myPlayer", out myPlayer);
            FishingBobberObservation observation;
            if (FishingBobberObserver.TryGetLatest(out observation) &&
                TryUseObservedLocalBobber(observation, myPlayer, context.GameUpdateCount, out x, out y, out identity))
            {
                return true;
            }

            if (ShouldSkipProjectileFallbackForFreshInactiveObserver(context.GameUpdateCount))
            {
                Interlocked.Increment(ref _informationFishingBobberObserverFreshInactiveSkipCount);
                return false;
            }

            Interlocked.Increment(ref _informationFishingProjectileFallbackScanCount);
            var projectiles = InformationReflection.GetStaticMember(context.MainType, "projectile");
            if (projectiles == null)
            {
                return false;
            }

            var count = GetCollectionCount(projectiles);
            for (var index = 0; index < count; index++)
            {
                var projectile = InformationReflection.GetIndexedValue(projectiles, index);
                if (projectile == null)
                {
                    continue;
                }

                bool active;
                bool bobber;
                int owner;
                InformationReflection.TryReadBool(projectile, "active", out active);
                InformationReflection.TryReadBool(projectile, "bobber", out bobber);
                InformationReflection.TryReadInt(projectile, "owner", out owner);
                if (!active || !bobber || owner != myPlayer)
                {
                    continue;
                }

                if (InformationReflection.TryReadVectorMember(projectile, "Center", out x, out y))
                {
                    InformationReflection.TryReadInt(projectile, "identity", out identity);
                    return true;
                }

                if (InformationReflection.TryReadVectorMember(projectile, "position", out x, out y))
                {
                    InformationReflection.TryReadInt(projectile, "identity", out identity);
                    return true;
                }
            }

            MarkNoActiveFishingBobberObservation(context.GameUpdateCount);
            return false;
        }

        internal static bool TryUseObservedLocalBobberForTesting(FishingBobberObservation observation, int myPlayer, ulong currentGameUpdateCount, out float x, out float y)
        {
            int identity;
            return TryUseObservedLocalBobber(observation, myPlayer, currentGameUpdateCount, out x, out y, out identity);
        }

        internal static bool TryFindLocalBobberForTesting(InformationWorldContext context, out float x, out float y)
        {
            int identity;
            return TryFindLocalBobber(context, out x, out y, out identity);
        }

        internal static void ResetFishingBobberLookupDiagnosticsForTesting()
        {
            Interlocked.Exchange(ref _informationFishingBobberObserverFreshInactiveSkipCount, 0);
            Interlocked.Exchange(ref _informationFishingProjectileFallbackScanCount, 0);
        }

        private static bool TryUseObservedLocalBobber(FishingBobberObservation observation, int myPlayer, ulong currentGameUpdateCount, out float x, out float y, out int identity)
        {
            x = 0f;
            y = 0f;
            identity = -1;
            if (observation == null ||
                !observation.Active ||
                !observation.Bobber ||
                observation.Owner != myPlayer ||
                !observation.LiquidStateKnown ||
                !observation.InLiquid ||
                !IsFreshObservedBobber(observation, currentGameUpdateCount) ||
                !IsFinite(observation.CenterX) ||
                !IsFinite(observation.CenterY))
            {
                return false;
            }

            x = observation.CenterX;
            y = observation.CenterY;
            identity = observation.Identity;
            return true;
        }

        private static bool ShouldSkipProjectileFallbackForFreshInactiveObserver(ulong currentGameUpdateCount)
        {
            if (currentGameUpdateCount == 0 || currentGameUpdateCount > long.MaxValue)
            {
                return false;
            }

            return FishingBobberObserver.HasFreshNoActiveObservation(
                (long)currentGameUpdateCount,
                (int)FishingBobberObserverFreshTicks);
        }

        private static void MarkNoActiveFishingBobberObservation(ulong currentGameUpdateCount)
        {
            if (currentGameUpdateCount == 0 || currentGameUpdateCount > long.MaxValue)
            {
                return;
            }

            FishingBobberObserver.MarkNoActiveObservation((long)currentGameUpdateCount);
        }

        private static bool IsFreshObservedBobber(FishingBobberObservation observation, ulong currentGameUpdateCount)
        {
            if (observation == null || observation.GameUpdateCount < 0 || currentGameUpdateCount > long.MaxValue)
            {
                return false;
            }

            var age = (long)currentGameUpdateCount - observation.GameUpdateCount;
            return age >= 0 && age <= (long)FishingBobberObserverFreshTicks;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string ResolveChestName(Type mainType, int x, int y)
        {
            var chests = InformationReflection.GetStaticMember(mainType, "chest");
            var count = GetCollectionCount(chests);
            for (var index = 0; index < count; index++)
            {
                var chest = InformationReflection.GetIndexedValue(chests, index);
                if (chest == null)
                {
                    continue;
                }

                int chestX;
                int chestY;
                if (InformationReflection.TryReadInt(chest, "x", out chestX) &&
                    InformationReflection.TryReadInt(chest, "y", out chestY) &&
                    chestX == x &&
                    chestY == y)
                {
                    var name = FirstNonEmpty(
                        InformationReflection.TryReadString(chest, "name"),
                        InformationReflection.TryReadString(chest, "Name"));
                    return string.IsNullOrWhiteSpace(name) ? "宝箱" : name;
                }
            }

            return "宝箱";
        }

        private static bool HasWorldOverlayEnabled(AppSettings settings)
        {
            return settings != null &&
                   (settings.InformationEnemyNameLabelsEnabled ||
                    settings.InformationCritterNameLabelsEnabled ||
                    !string.Equals(NormalizeNpcMode(settings.InformationNpcNameLabelsMode), "Off", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(NormalizeChestLabelsMode(settings), ChestLabelsModeOff, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(NormalizeSignTextMode(settings), InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(NormalizeTombstoneTextMode(settings), InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase) ||
                    settings.InformationHighlightLifeCrystalEnabled ||
                    settings.InformationHighlightManaCrystalEnabled ||
                    settings.InformationHighlightDigtoiseEnabled ||
                    settings.InformationHighlightLifeFruitEnabled ||
                    settings.InformationHighlightDragonEggEnabled);
        }

        private static bool HasStatusPanelEnabled(AppSettings settings)
        {
            return settings != null &&
                   (settings.InformationBiomeDisplayEnabled ||
                    settings.InformationWorldInfectionEnabled ||
                    settings.InformationLuckValueEnabled ||
                    settings.InformationFishingCatchesEnabled ||
                    settings.InformationFishingFilteredCatchesEnabled ||
                    settings.InformationAnglerQuestEnabled);
        }

        private static InformationWorldContextProfile BuildStatusContextProfile(AppSettings settings)
        {
            return InformationWorldContextProfile.Status;
        }

        private static InformationWorldContextProfile BuildWorldOverlayContextProfile(AppSettings settings)
        {
            return InformationWorldContextProfile.FullRecord;
        }

        private static void UpdateDiagnostics(int? npcLabels, int? chestLabels, int? signTextLabels, int? tombstoneTextLabels, int? tileHighlights, int? statusLines, double elapsedMs, string skipReason)
        {
            lock (SyncRoot)
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                Diagnostics.EnabledSummary = BuildEnabledSummary(settings);
                if (npcLabels.HasValue)
                {
                    Diagnostics.NpcLabelsDrawn = npcLabels.Value;
                }

                if (chestLabels.HasValue)
                {
                    Diagnostics.ChestLabelsDrawn = chestLabels.Value;
                }

                if (signTextLabels.HasValue)
                {
                    Diagnostics.SignTextLabelsDrawn = signTextLabels.Value;
                }

                if (tombstoneTextLabels.HasValue)
                {
                    Diagnostics.TombstoneTextLabelsDrawn = tombstoneTextLabels.Value;
                }

                if (tileHighlights.HasValue)
                {
                    Diagnostics.TileHighlightsDrawn = tileHighlights.Value;
                }

                if (statusLines.HasValue)
                {
                    Diagnostics.StatusLinesDrawn = statusLines.Value;
                }

                Diagnostics.LastDrawElapsedMs = elapsedMs;
                Diagnostics.LastSkipReason = skipReason ?? string.Empty;
            }
        }

        private static string BuildEnabledSummary(AppSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (settings.InformationEnemyNameLabelsEnabled) parts.Add("enemy");
            if (settings.InformationCritterNameLabelsEnabled) parts.Add("critter");
            if (!string.Equals(NormalizeNpcMode(settings.InformationNpcNameLabelsMode), "Off", StringComparison.OrdinalIgnoreCase)) parts.Add("npc:" + NormalizeNpcMode(settings.InformationNpcNameLabelsMode));
            var chestMode = NormalizeChestLabelsMode(settings);
            if (!string.Equals(chestMode, ChestLabelsModeOff, StringComparison.OrdinalIgnoreCase)) parts.Add("chest:" + chestMode);
            var signTextMode = NormalizeSignTextMode(settings);
            if (!string.Equals(signTextMode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase)) parts.Add("signText:" + signTextMode);
            var tombstoneTextMode = NormalizeTombstoneTextMode(settings);
            if (!string.Equals(tombstoneTextMode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase)) parts.Add("tombstoneText:" + tombstoneTextMode);
            if (settings.InformationHighlightLifeCrystalEnabled) parts.Add("lifeCrystal");
            if (settings.InformationHighlightManaCrystalEnabled) parts.Add("manaCrystal");
            if (settings.InformationHighlightDigtoiseEnabled) parts.Add("digtoise");
            if (settings.InformationHighlightLifeFruitEnabled) parts.Add("lifeFruit");
            if (settings.InformationHighlightDragonEggEnabled) parts.Add("dragonEgg");
            if (settings.InformationBiomeDisplayEnabled) parts.Add("biome");
            if (settings.InformationWorldInfectionEnabled) parts.Add("infection");
            if (settings.InformationLuckValueEnabled) parts.Add("luck");
            if (settings.InformationFishingCatchesEnabled) parts.Add("fishing");
            if (settings.InformationFishingFilteredCatchesEnabled) parts.Add("filteredFishing");
            if (settings.InformationAnglerQuestEnabled) parts.Add("angler");
            return string.Join(",", parts.ToArray());
        }

        private static object GetLocalPlayer(Type mainType)
        {
            var local = InformationReflection.GetStaticMember(mainType, "LocalPlayer");
            if (local != null)
            {
                return local;
            }

            var players = InformationReflection.GetStaticMember(mainType, "player");
            int index;
            InformationReflection.TryReadStaticInt(mainType, "myPlayer", out index);
            if (index < 0)
            {
                index = 0;
            }

            return InformationReflection.GetIndexedValue(players, index);
        }

        private static bool IsNpcActive(object npc)
        {
            var typedNpc = npc as NPC;
            if (typedNpc != null)
            {
                return TerrariaNpcReadCompat.IsActive(typedNpc);
            }

            bool active;
            return InformationReflection.TryReadBool(npc, "active", out active) && active;
        }

        private static bool TryReadNpcLabelAnchor(NPC npc, out float worldX, out float worldY)
        {
            if (npc == null)
            {
                worldX = 0f;
                worldY = 0f;
                return false;
            }

            var hitbox = TerrariaNpcReadCompat.Hitbox(npc);
            if (hitbox.Width > 0 && hitbox.Height > 0)
            {
                worldX = hitbox.X + hitbox.Width * 0.5f;
                worldY = hitbox.Y;
                return true;
            }

            var position = TerrariaNpcReadCompat.Position(npc);
            var width = TerrariaNpcReadCompat.Width(npc);
            worldX = position.X + Math.Max(0, width) * 0.5f;
            worldY = position.Y;
            return true;
        }

        private static bool TryReadNpcLabelAnchor(object npc, out float worldX, out float worldY)
        {
            var typedNpc = npc as NPC;
            if (typedNpc != null)
            {
                return TryReadNpcLabelAnchor(typedNpc, out worldX, out worldY);
            }

            int x;
            int y;
            int width;
            int height;
            if (InformationReflection.TryReadRectangle(InformationReflection.GetMember(npc, "Hitbox"), out x, out y, out width, out height) &&
                width > 0 &&
                height > 0)
            {
                worldX = x + width * 0.5f;
                worldY = y;
                return true;
            }

            float positionX;
            float positionY;
            if (InformationReflection.TryReadVectorMember(npc, "position", out positionX, out positionY))
            {
                int npcWidth;
                InformationReflection.TryReadInt(npc, "width", out npcWidth);
                worldX = positionX + Math.Max(0, npcWidth) * 0.5f;
                worldY = positionY;
                return true;
            }

            if (InformationReflection.TryReadVectorMember(npc, "Top", out worldX, out worldY))
            {
                return true;
            }

            if (InformationReflection.TryReadVectorMember(npc, "Center", out worldX, out worldY))
            {
                int npcHeight;
                if (InformationReflection.TryReadInt(npc, "height", out npcHeight))
                {
                    worldY -= Math.Max(0, npcHeight) * 0.5f;
                }

                return true;
            }

            worldX = 0f;
            worldY = 0f;
            return false;
        }

        internal static bool ShouldDrawEnemySegmentLabel(int groupSize, int neighborCount)
        {
            return groupSize < 3 || neighborCount < 2;
        }

        internal static bool ShouldDrawEnemyNpcTypeLabelForTesting(int npcType)
        {
            return GetKnownSegmentRole(npcType) != NpcSegmentRole.Body;
        }

        private static NpcSegmentRole GetKnownSegmentRole(int npcType)
        {
            switch (npcType)
            {
                case 7:   // DevourerHead
                case 10:  // GiantWormHead
                case 13:  // EaterofWorldsHead
                case 39:  // BoneSerpentHead
                case 87:  // WyvernHead
                case 95:  // DiggerHead
                case 98:  // SeekerHead
                case 117: // LeechHead
                case 134: // TheDestroyer
                case 402: // StardustWormHead
                case 412: // SolarCrawltipedeHead
                case 454: // CultistDragonHead
                case 510: // DuneSplicerHead
                case 513: // TombCrawlerHead
                case 621: // BloodEelHead
                    return NpcSegmentRole.Head;

                case 9:   // DevourerTail
                case 12:  // GiantWormTail
                case 15:  // EaterofWorldsTail
                case 41:  // BoneSerpentTail
                case 92:  // WyvernTail
                case 97:  // DiggerTail
                case 100: // SeekerTail
                case 119: // LeechTail
                case 136: // TheDestroyerTail
                case 404: // StardustWormTail
                case 414: // SolarCrawltipedeTail
                case 459: // CultistDragonTail
                case 512: // DuneSplicerTail
                case 515: // TombCrawlerTail
                case 623: // BloodEelTail
                    return NpcSegmentRole.Tail;

                case 8:   // DevourerBody
                case 11:  // GiantWormBody
                case 14:  // EaterofWorldsBody
                case 40:  // BoneSerpentBody
                case 88:  // WyvernLegs
                case 89:  // WyvernBody
                case 90:  // WyvernBody2
                case 91:  // WyvernBody3
                case 96:  // DiggerBody
                case 99:  // SeekerBody
                case 118: // LeechBody
                case 135: // TheDestroyerBody
                case 403: // StardustWormBody
                case 413: // SolarCrawltipedeBody
                case 455: // CultistDragonBody1
                case 456: // CultistDragonBody2
                case 457: // CultistDragonBody3
                case 458: // CultistDragonBody4
                case 511: // DuneSplicerBody
                case 514: // TombCrawlerBody
                case 622: // BloodEelBody
                    return NpcSegmentRole.Body;

                default:
                    return NpcSegmentRole.Unknown;
            }
        }

        private static Dictionary<int, NpcSegmentInfo> BuildNpcSegmentInfos(NPC[] npcs, int count)
        {
            var result = new Dictionary<int, NpcSegmentInfo>();
            for (var index = 0; npcs != null && index < count && index < npcs.Length; index++)
            {
                var npc = npcs[index];
                if (!TerrariaNpcReadCompat.IsActive(npc))
                {
                    continue;
                }

                var whoAmI = TerrariaNpcReadCompat.WhoAmI(npc);
                if (whoAmI < 0)
                {
                    whoAmI = index;
                }

                var realLife = TerrariaNpcReadCompat.RealLife(npc);
                var info = new NpcSegmentInfo
                {
                    Index = index,
                    WhoAmI = whoAmI,
                    RealLife = realLife,
                    GroupKey = ResolveSegmentGroupKey(index, whoAmI, realLife),
                    References = ReadNpcReferences(npc, count)
                };
                result[index] = info;
            }

            CompleteNpcSegmentInfoCounts(result);
            return result;
        }

        private static Dictionary<int, NpcSegmentInfo> BuildNpcSegmentInfos(object npcs, int count)
        {
            var result = new Dictionary<int, NpcSegmentInfo>();
            for (var index = 0; index < count; index++)
            {
                var npc = InformationReflection.GetIndexedValue(npcs, index);
                if (npc == null || !IsNpcActive(npc))
                {
                    continue;
                }

                int whoAmI;
                if (!InformationReflection.TryReadInt(npc, "whoAmI", out whoAmI))
                {
                    whoAmI = index;
                }

                int realLife;
                if (!InformationReflection.TryReadInt(npc, "realLife", out realLife))
                {
                    realLife = -1;
                }

                var info = new NpcSegmentInfo
                {
                    Index = index,
                    WhoAmI = whoAmI,
                    RealLife = realLife,
                    GroupKey = ResolveSegmentGroupKey(index, whoAmI, realLife),
                    References = ReadNpcReferences(npc, count)
                };
                result[index] = info;
            }

            CompleteNpcSegmentInfoCounts(result);
            return result;
        }

        private static void CompleteNpcSegmentInfoCounts(Dictionary<int, NpcSegmentInfo> result)
        {
            foreach (var pair in result)
            {
                var info = pair.Value;
                info.GroupSize = CountSegmentGroupMembers(result, info.GroupKey);
                info.NeighborCount = CountSegmentNeighbors(result, info);
            }
        }

        private static int ResolveSegmentGroupKey(int index, int whoAmI, int realLife)
        {
            if (realLife >= 0)
            {
                return realLife;
            }

            return whoAmI >= 0 ? whoAmI : index;
        }

        private static int[] ReadNpcReferences(object npc, int npcCount)
        {
            var ai = InformationReflection.GetMember(npc, "ai");
            var result = new int[4];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = ReadNpcReference(ai, index, npcCount);
            }

            return result;
        }

        private static int[] ReadNpcReferences(NPC npc, int npcCount)
        {
            var ai = TerrariaNpcReadCompat.Ai(npc);
            var result = new int[4];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = ReadNpcReference(ai, index, npcCount);
            }

            return result;
        }

        private static int ReadNpcReference(float[] ai, int index, int npcCount)
        {
            if (ai == null || index < 0 || index >= ai.Length)
            {
                return -1;
            }

            var value = ai[index];
            var rounded = (int)Math.Round(value);
            if (Math.Abs(value - rounded) > 0.001f ||
                rounded < 0 ||
                rounded >= npcCount)
            {
                return -1;
            }

            return rounded;
        }

        private static int ReadNpcReference(object ai, int index, int npcCount)
        {
            var raw = InformationReflection.GetIndexedValue(ai, index);
            if (raw == null)
            {
                return -1;
            }

            try
            {
                var value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                var rounded = (int)Math.Round(value);
                if (Math.Abs(value - rounded) > 0.001f ||
                    rounded < 0 ||
                    rounded >= npcCount)
                {
                    return -1;
                }

                return rounded;
            }
            catch
            {
                return -1;
            }
        }

        private static int CountSegmentGroupMembers(Dictionary<int, NpcSegmentInfo> infos, int groupKey)
        {
            var count = 0;
            foreach (var pair in infos)
            {
                if (pair.Value != null && pair.Value.GroupKey == groupKey)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSegmentNeighbors(Dictionary<int, NpcSegmentInfo> infos, NpcSegmentInfo current)
        {
            if (infos == null || current == null)
            {
                return 0;
            }

            var neighbors = new List<int>();
            AddForwardSegmentNeighbors(infos, current, neighbors);
            foreach (var pair in infos)
            {
                var other = pair.Value;
                if (other == null || other.Index == current.Index || other.GroupKey != current.GroupKey)
                {
                    continue;
                }

                if (ReferencesSegment(other, current))
                {
                    AddUniqueNeighbor(neighbors, other.Index);
                }
            }

            return neighbors.Count;
        }

        private static void AddForwardSegmentNeighbors(Dictionary<int, NpcSegmentInfo> infos, NpcSegmentInfo current, IList<int> neighbors)
        {
            for (var index = 0; current.References != null && index < current.References.Length; index++)
            {
                var reference = current.References[index];
                NpcSegmentInfo target;
                if (TryGetSegmentInfoByReference(infos, reference, out target) &&
                    target.Index != current.Index &&
                    target.GroupKey == current.GroupKey)
                {
                    AddUniqueNeighbor(neighbors, target.Index);
                }
            }
        }

        private static bool ReferencesSegment(NpcSegmentInfo source, NpcSegmentInfo target)
        {
            if (source == null || target == null || source.References == null)
            {
                return false;
            }

            for (var index = 0; index < source.References.Length; index++)
            {
                var reference = source.References[index];
                if (reference == target.Index || (target.WhoAmI >= 0 && reference == target.WhoAmI))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSegmentInfoByReference(Dictionary<int, NpcSegmentInfo> infos, int reference, out NpcSegmentInfo info)
        {
            info = null;
            if (reference < 0 || infos == null)
            {
                return false;
            }

            if (infos.TryGetValue(reference, out info))
            {
                return true;
            }

            foreach (var pair in infos)
            {
                if (pair.Value != null && pair.Value.WhoAmI == reference)
                {
                    info = pair.Value;
                    return true;
                }
            }

            info = null;
            return false;
        }

        private static void AddUniqueNeighbor(IList<int> neighbors, int index)
        {
            for (var existing = 0; existing < neighbors.Count; existing++)
            {
                if (neighbors[existing] == index)
                {
                    return;
                }
            }

            neighbors.Add(index);
        }

        private static object ReadCritterSet()
        {
            if (_critterSetResolved)
            {
                return _critterSet;
            }

            _critterSet = InformationReflection.GetStaticMember(
                InformationReflection.FindType("Terraria.ID.NPCID+Sets"),
                "CountsAsCritter");
            _critterSetResolved = true;
            return _critterSet;
        }

        private static bool IsCritter(object npc, int type)
        {
            var typedNpc = npc as NPC;
            if (typedNpc != null)
            {
                return IsCritter(typedNpc, type);
            }

            bool countsAsCritter;
            if (InformationReflection.TryReadBool(npc, "CountsAsACritter", out countsAsCritter) && countsAsCritter)
            {
                return true;
            }

            int catchItem;
            if (InformationReflection.TryReadInt(npc, "catchItem", out catchItem) && catchItem > 0)
            {
                return true;
            }

            object raw = InformationReflection.GetIndexedValue(ReadCritterSet(), type);
            bool value;
            return TryConvertBool(raw, out value) && value;
        }

        private static bool IsCritter(NPC npc, int type)
        {
            if (TerrariaNpcReadCompat.IsCritter(npc))
            {
                return true;
            }

            object raw = InformationReflection.GetIndexedValue(ReadCritterSet(), type);
            bool value;
            return TryConvertBool(raw, out value) && value;
        }

        private static bool IsGoldCritter(int type)
        {
            return GoldCritterNpcTypes.Contains(type);
        }

        private static bool IsTargetDummy(int npcType)
        {
            return npcType == ReadTargetDummyNpcType();
        }

        private static bool HasMetalDetector(object player)
        {
            var typedPlayer = player as Player;
            if (typedPlayer != null && TerrariaPlayerReadCompat.HasMetalDetector(typedPlayer))
            {
                return true;
            }

            bool value;
            if (InformationReflection.TryReadBool(player, "accOreFinder", out value) && value)
            {
                return true;
            }

            return InformationReflection.TryReadBool(player, "accOreFinderGold", out value) && value;
        }

        private static bool IsTileActive(object tile)
        {
            return InformationTileAccess.IsActive(tile);
        }

        private static int ReadTileType(object tile)
        {
            return InformationTileAccess.ReadType(tile);
        }

        private static void EnsureTileIdsResolved()
        {
            if (_tileIdsResolved)
            {
                return;
            }

            _lifeCrystalTileType = ReadTileId("LifeCrystal", 12);
            _manaCrystalTileType = ReadTileId("ManaCrystal", 639);
            _digtoiseTileType = ReadTileId("PalworldDigtoiseSleeping", 751);
            _lifeFruitTileType = ReadTileId("LifeFruit", 236);
            _dragonEggTileType = ReadTileId("DragonEgg", -1);
            _tombstoneTileType = ReadTileId("Tombstones", 85);
            _tileIdsResolved = true;
        }

        private static int ReadTileId(string name, int fallback)
        {
            var tileIdType = InformationReflection.FindType("Terraria.ID.TileID");
            int value;
            return TryReadStaticInt(tileIdType, name, out value) ? value : fallback;
        }

        private static int ReadNpcId(string name, int fallback)
        {
            var npcIdType = InformationReflection.FindType("Terraria.ID.NPCID");
            int value;
            return TryReadStaticInt(npcIdType, name, out value) ? value : fallback;
        }

        private static int ReadTargetDummyNpcType()
        {
            if (_targetDummyNpcTypeResolved)
            {
                return _targetDummyNpcType;
            }

            _targetDummyNpcType = ReadNpcId("TargetDummy", 488);
            _targetDummyNpcTypeResolved = true;
            return _targetDummyNpcType;
        }

        private static bool TryReadStaticInt(Type type, string name, out int value)
        {
            value = 0;
            var raw = InformationReflection.GetStaticMember(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AddZone(ICollection<string> zones, object player, string member, string label)
        {
            bool value;
            if (InformationReflection.TryReadBool(player, member, out value) && value && !Contains(zones, label))
            {
                zones.Add(label);
            }
        }

        private static bool HasZone(object player, string member)
        {
            bool value;
            return InformationReflection.TryReadBool(player, member, out value) && value;
        }

        private static void AddUnique(ICollection<string> zones, string label)
        {
            if (!Contains(zones, label))
            {
                zones.Add(label);
            }
        }

        private static bool Contains(IEnumerable<string> values, string needle)
        {
            foreach (var value in values)
            {
                if (string.Equals(value, needle, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSavedNpc(InformationWorldContext context, string savedField, string npcIdName, int fallbackNpcId)
        {
            var npcType = InformationReflection.FindType("Terraria.NPC");
            bool saved;
            if (InformationReflection.TryReadStaticBool(npcType, savedField, out saved) && saved)
            {
                return true;
            }

            return AnyActiveNpcOfType(context.MainType, ReadNpcId(npcIdName, fallbackNpcId));
        }

        private static bool AnyActiveNpcOfType(Type mainType, int npcType)
        {
            try
            {
                var typedNpcs = TerrariaMainCompat.Npcs;
                for (var index = 0; typedNpcs != null && index < typedNpcs.Length; index++)
                {
                    var npc = typedNpcs[index];
                    if (TerrariaNpcReadCompat.IsActive(npc) && TerrariaNpcReadCompat.Type(npc) == npcType)
                    {
                        return true;
                    }
                }

                if (typedNpcs != null)
                {
                    return false;
                }
            }
            catch
            {
            }

            var npcs = InformationReflection.GetStaticMember(mainType, "npc");
            var count = GetCollectionCount(npcs);
            for (var index = 0; index < count; index++)
            {
                var npc = InformationReflection.GetIndexedValue(npcs, index);
                int type;
                if (npc != null && IsNpcActive(npc) && InformationReflection.TryReadInt(npc, "type", out type) && type == npcType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadStaticNumber(Type type, string name, out double value)
        {
            value = 0d;
            var raw = InformationReflection.GetStaticMember(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadNumber(object instance, string name, out double value)
        {
            value = 0d;
            var raw = InformationReflection.GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatPercentLike(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        private static bool ReadAnglerQuestFinished(InformationWorldContext context)
        {
            var raw = InformationReflection.GetStaticMember(context.MainType, "anglerQuestFinished");
            bool direct;
            if (TryConvertBool(raw, out direct))
            {
                return direct;
            }

            int myPlayer;
            InformationReflection.TryReadStaticInt(context.MainType, "myPlayer", out myPlayer);
            object indexed = InformationReflection.GetIndexedValue(raw, myPlayer);
            return TryConvertBool(indexed, out direct) && direct;
        }

        private static string ResolveItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetItemNameValue", new object[] { itemId }, out raw))
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return itemId.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveItemInternalName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            try
            {
                var itemIdType = InformationReflection.FindType("Terraria.ID.ItemID");
                var search = InformationReflection.GetStaticMember(itemIdType, "Search");
                object raw;
                if (TryInvokeInstance(search, "GetName", new object[] { itemId }, out raw) && raw != null)
                {
                    return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string ReadLocalizedText(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            try
            {
                var languageType = InformationReflection.FindType("Terraria.Localization.Language");
                object raw;
                if (InformationReflection.TryInvokeStatic(languageType, "GetTextValue", new object[] { key }, out raw) && raw != null)
                {
                    return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static bool TryInvokeInstance(object instance, string methodName, object[] args, out object result)
        {
            result = null;
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var methods = instance.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != (args == null ? 0 : args.Length))
                    {
                        continue;
                    }

                    result = method.Invoke(instance, args);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static IList<FishingCatchCandidate> ResolveFishingCatchCandidates(InformationWorldContext context, float bobberX, float bobberY, int bobberIdentity, string filterSignature, out string message)
        {
            try
            {
                return InformationFishingCatchResolver.ResolveCatchCandidates(context, bobberX, bobberY, bobberIdentity, filterSignature, out message);
            }
            catch (Exception error)
            {
                Logger.Debug("InformationOverlay", "Fishing catch resolution failed: " + error);
                message = "鱼获解析失败";
                return new List<FishingCatchCandidate>();
            }
        }

        private static void AddFishingCatchLines(
            ICollection<InformationStatusLine> lines,
            int order,
            bool hasBobber,
            IList<FishingCatchCandidate> candidates,
            string message,
            InformationColor color,
            double fontScale)
        {
            if (!hasBobber)
            {
                return;
            }

            var names = BuildFishingCatchNames(candidates);
            if (names.Count <= 0)
            {
                AddLine(lines, order, "完整鱼获: " + FirstNonEmpty(message, "暂无可解析鱼获"), color, fontScale);
                return;
            }

            AddFishingCatchNameLines(lines, order, "完整鱼获: ", names, color, fontScale);
        }

        private static void AddFilteredFishingCatchLines(
            ICollection<InformationStatusLine> lines,
            int order,
            AppSettings settings,
            bool hasBobber,
            bool sonarBuffActive,
            IList<FishingCatchCandidate> candidates,
            string message,
            InformationColor color,
            double fontScale)
        {
            if (!hasBobber)
            {
                return;
            }

            if (IsFishingFilterDisabled(settings))
            {
                AddLine(lines, order, "过滤鱼获: 过滤未启用", color, fontScale);
                return;
            }

            if (!sonarBuffActive)
            {
                AddLine(lines, order, "过滤鱼获: 需要声呐药水", color, fontScale);
                return;
            }

            if (candidates == null || candidates.Count <= 0)
            {
                AddLine(lines, order, "过滤鱼获: " + FirstNonEmpty(message, "暂无可解析鱼获"), color, fontScale);
                return;
            }

            var names = new List<string>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                FishingFilterDecision decision;
                try
                {
                    decision = FishingFilterDecisionService.Decide(settings, candidate);
                }
                catch
                {
                    decision = null;
                }

                if (decision == null || !decision.ShouldKeep)
                {
                    continue;
                }

                var name = candidate == null ? string.Empty : candidate.DisplayName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            if (names.Count <= 0)
            {
                AddLine(lines, order, "过滤鱼获: 无匹配鱼获", color, fontScale);
                return;
            }

            AddFishingCatchNameLines(lines, order, "过滤鱼获: ", names, color, fontScale);
        }

        private static bool IsFishingFilterDisabled(AppSettings settings)
        {
            return string.Equals(
                FishingFilterModes.Normalize(settings == null ? null : settings.FishingFilterMode),
                FishingFilterModes.Disabled,
                StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> BuildFishingCatchNames(IList<FishingCatchCandidate> candidates)
        {
            var names = new List<string>();
            if (candidates == null)
            {
                return names;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                var name = candidates[index] == null ? string.Empty : candidates[index].DisplayName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static void AddFishingCatchNameLines(
            ICollection<InformationStatusLine> lines,
            int order,
            string prefix,
            IList<string> names,
            InformationColor color,
            double fontScale)
        {
            const int maxCharsPerLine = 38;
            var current = prefix;
            var lineIndex = 0;
            for (var index = 0; index < names.Count; index++)
            {
                var name = names[index];
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var separator = string.Equals(current, prefix, StringComparison.Ordinal) || string.Equals(current, "  ", StringComparison.Ordinal)
                    ? string.Empty
                    : "、";
                var candidate = current + separator + name;
                if (candidate.Length > maxCharsPerLine && !string.Equals(current, prefix, StringComparison.Ordinal) && !string.Equals(current, "  ", StringComparison.Ordinal))
                {
                    AddLine(lines, order + lineIndex, current, color, fontScale);
                    lineIndex++;
                    current = "  " + name;
                }
                else
                {
                    current = candidate;
                }
            }

            if (!string.IsNullOrWhiteSpace(current) &&
                !string.Equals(current, prefix, StringComparison.Ordinal) &&
                !string.Equals(current, "  ", StringComparison.Ordinal))
            {
                AddLine(lines, order + lineIndex, current, color, fontScale);
            }
        }

        private static void AddLine(ICollection<InformationStatusLine> lines, int order, string text, InformationColor color, double fontScale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            lines.Add(new InformationStatusLine
            {
                Order = order,
                Text = text,
                Color = color,
                FontScale = InformationStyleHelper.NormalizeFontScale(fontScale, 0.72d)
            });
        }

        internal static string[] BuildSignTextDisplayLinesForTesting(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            IList<string> lines;
            return TryBuildSignTextDisplayLines(text, NormalizeSignTextMode(mode), maxLines, maxCharacters, scale, out lines)
                ? ToArray(lines)
                : new string[0];
        }

        internal static void ResetSignTextLayoutCacheForTesting()
        {
            lock (SyncRoot)
            {
                SignTextLayoutCache.Clear();
                _signTextLayoutCacheRebuildCount = 0;
                _signTextLayoutCacheHitCount = 0;
                _signTextLayoutCacheMissCount = 0;
                _signTextLayoutFontSignature = UiTextRenderer.FontSignatureForLayoutCache;
                _signTextLayoutCacheGeneration = UiTextRenderer.CacheGenerationForLayoutCache;
            }
        }

        internal static InformationSignTextLayoutSnapshot BuildSignTextLayoutSnapshotForTesting(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            var layout = GetOrBuildSignTextLayout(text, HashText(text), mode, maxLines, maxCharacters, scale);
            int rebuildCount;
            lock (SyncRoot)
            {
                rebuildCount = _signTextLayoutCacheRebuildCount;
            }

            if (layout == null)
            {
                return new InformationSignTextLayoutSnapshot(0, string.Empty, 0, 0, 0, rebuildCount);
            }

            return new InformationSignTextLayoutSnapshot(
                layout.DisplayLines.Length,
                layout.DisplayLines.Length <= 0 ? string.Empty : layout.DisplayLines[0],
                layout.LineWidths.Length <= 0 ? 0 : layout.LineWidths[0],
                layout.LineHeight,
                layout.TotalHeight,
                rebuildCount);
        }

        private static SignTextLayout GetOrBuildSignTextLayout(string text, int textHash, string mode, int maxLines, int maxCharacters, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var normalizedMode = NormalizeSignTextMode(mode);
            if (string.Equals(normalizedMode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fontSignature = UiTextRenderer.FontSignatureForLayoutCache;
            var cacheGeneration = UiTextRenderer.CacheGenerationForLayoutCache;
            var key = new SignTextLayoutKey(
                text,
                textHash,
                normalizedMode,
                InformationSignTextModes.ClampLines(maxLines),
                InformationSignTextModes.ClampCharacters(maxCharacters),
                ScaleKey(scale),
                fontSignature,
                cacheGeneration);

            SignTextLayout cached;
            lock (SyncRoot)
            {
                ClearSignTextLayoutCacheIfFontChangedLocked(fontSignature, cacheGeneration);
                if (SignTextLayoutCache.TryGetValue(key, out cached))
                {
                    unchecked
                    {
                        _signTextLayoutCacheHitCount++;
                    }

                    return cached;
                }
            }

            var layout = BuildSignTextLayout(text, normalizedMode, maxLines, maxCharacters, scale);
            if (layout == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                ClearSignTextLayoutCacheIfFontChangedLocked(fontSignature, cacheGeneration);
                if (SignTextLayoutCache.TryGetValue(key, out cached))
                {
                    unchecked
                    {
                        _signTextLayoutCacheHitCount++;
                    }

                    return cached;
                }

                if (SignTextLayoutCache.Count >= SignTextLayoutCacheLimit)
                {
                    SignTextLayoutCache.Clear();
                }

                SignTextLayoutCache[key] = layout;
                unchecked
                {
                    _signTextLayoutCacheRebuildCount++;
                    _signTextLayoutCacheMissCount++;
                }
            }

            return layout;
        }

        private static void ClearSignTextLayoutCacheIfFontChangedLocked(string fontSignature, int cacheGeneration)
        {
            if (_signTextLayoutCacheGeneration == cacheGeneration &&
                string.Equals(_signTextLayoutFontSignature, fontSignature ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            SignTextLayoutCache.Clear();
            _signTextLayoutFontSignature = fontSignature ?? string.Empty;
            _signTextLayoutCacheGeneration = cacheGeneration;
        }

        private static SignTextLayout BuildSignTextLayout(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            IList<string> lines;
            if (!TryBuildSignTextDisplayLines(text, mode, maxLines, maxCharacters, scale, out lines))
            {
                return null;
            }

            var displayLines = ToArray(lines);
            var lineWidths = new int[displayLines.Length];
            var hasVisibleText = false;
            for (var index = 0; index < displayLines.Length; index++)
            {
                var width = UiTextRenderer.EstimateTextWidth(displayLines[index], scale);
                lineWidths[index] = width;
                hasVisibleText |= width > 0;
            }

            var lineHeight = Math.Max(16, UiTextRenderer.EstimateTextHeight(scale) + 5);
            return new SignTextLayout(displayLines, lineWidths, lineHeight, lineHeight * displayLines.Length, scale, hasVisibleText);
        }

        private static bool TryBuildSignTextDisplayLines(string text, string mode, int maxLines, int maxCharacters, float scale, out IList<string> lines)
        {
            lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalizedMode = NormalizeSignTextMode(mode);
            if (string.Equals(normalizedMode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var displayText = NormalizeLineBreaks(text);
            var characterLimited = false;
            if (string.Equals(normalizedMode, InformationSignTextModes.Characters, StringComparison.OrdinalIgnoreCase))
            {
                var limit = InformationSignTextModes.ClampCharacters(maxCharacters);
                if (displayText.Length > limit)
                {
                    displayText = displayText.Substring(0, limit).TrimEnd();
                    characterLimited = true;
                }
            }

            var lineLimit = InformationSignTextModes.VanillaDisplayMaxLines;
            if (string.Equals(normalizedMode, InformationSignTextModes.Lines, StringComparison.OrdinalIgnoreCase))
            {
                lineLimit = InformationSignTextModes.ClampLines(maxLines);
            }

            var truncatedByLines = WrapSignText(displayText, lineLimit, scale, lines);
            if (lines.Count <= 0)
            {
                return false;
            }

            if (characterLimited || truncatedByLines)
            {
                lines[lines.Count - 1] = AppendEllipsisToFit(lines[lines.Count - 1], scale);
            }

            return true;
        }

        private static bool WrapSignText(string text, int maxLines, float scale, IList<string> lines)
        {
            var source = text ?? string.Empty;
            var paragraphs = source.Split('\n');
            var truncated = false;
            var width = Math.Max(80, (int)Math.Round(InformationSignTextModes.VanillaDisplayWidthPixels * Math.Max(0.1f, scale)));
            for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
            {
                if (lines.Count >= maxLines)
                {
                    truncated = HasRemainingParagraphText(paragraphs, paragraphIndex);
                    break;
                }

                var paragraph = paragraphs[paragraphIndex] ?? string.Empty;
                if (paragraph.Length <= 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }

                var offset = 0;
                while (offset < paragraph.Length)
                {
                    if (lines.Count >= maxLines)
                    {
                        truncated = true;
                        break;
                    }

                    var take = FindWrappedTakeCount(paragraph, offset, width, scale);
                    var line = paragraph.Substring(offset, take).TrimEnd();
                    if (line.Length > 0 || paragraph.Length == 0)
                    {
                        lines.Add(line);
                    }

                    offset += take;
                    while (offset < paragraph.Length && char.IsWhiteSpace(paragraph[offset]))
                    {
                        offset++;
                    }
                }
            }

            return truncated;
        }

        private static int FindWrappedTakeCount(string text, int offset, int maxWidth, float scale)
        {
            var best = 1;
            var lastBreak = -1;
            for (var index = offset; index < text.Length; index++)
            {
                var current = text[index];
                if (char.IsWhiteSpace(current))
                {
                    lastBreak = index;
                }

                var length = index - offset + 1;
                if (UiTextRenderer.EstimateTextWidth(text.Substring(offset, length), scale) <= maxWidth)
                {
                    best = length;
                    continue;
                }

                if (lastBreak >= offset)
                {
                    return Math.Max(1, lastBreak - offset);
                }

                return best;
            }

            return Math.Max(1, text.Length - offset);
        }

        private static bool HasRemainingParagraphText(string[] paragraphs, int startIndex)
        {
            if (paragraphs == null)
            {
                return false;
            }

            for (var index = startIndex; index < paragraphs.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(paragraphs[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string AppendEllipsisToFit(string value, float scale)
        {
            var text = value ?? string.Empty;
            var maxWidth = Math.Max(80, (int)Math.Round(InformationSignTextModes.VanillaDisplayWidthPixels * Math.Max(0.1f, scale)));
            const string suffix = "...";
            while (text.Length > 0 && UiTextRenderer.EstimateTextWidth(text + suffix, scale) > maxWidth)
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
            }

            return text.Length <= 0 ? suffix : text + suffix;
        }

        private static string NormalizeLineBreaks(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        private static string NormalizeSignTextMode(AppSettings settings)
        {
            return InformationSignTextModes.Normalize(settings == null ? null : settings.InformationSignTextLabelsMode);
        }

        private static string NormalizeTombstoneTextMode(AppSettings settings)
        {
            return InformationSignTextModes.Normalize(settings == null ? null : settings.InformationTombstoneTextLabelsMode);
        }

        private static string NormalizeSignTextMode(string mode)
        {
            return InformationSignTextModes.Normalize(mode);
        }

        private static string[] ToArray(IList<string> values)
        {
            if (values == null || values.Count <= 0)
            {
                return new string[0];
            }

            var result = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                result[index] = values[index] ?? string.Empty;
            }

            return result;
        }

        private static int HashText(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;
                if (text == null)
                {
                    return hash;
                }

                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619;
                }

                return hash;
            }
        }

        private static int AddHash(int hash, int value)
        {
            unchecked
            {
                return (hash * 16777619) ^ value;
            }
        }

        private static int ScaleKey(float scale)
        {
            return (int)Math.Round(scale * 10000f);
        }

        private static int GetCollectionCount(object source)
        {
            if (source == null)
            {
                return 0;
            }

            var list = source as IList;
            if (list != null)
            {
                return list.Count;
            }

            var array = source as Array;
            return array != null && array.Rank == 1 ? array.GetLength(0) : 0;
        }

        private static int ToInt(object raw, int fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index];
                }
            }

            return string.Empty;
        }

        private static string NormalizeNpcMode(string mode)
        {
            if (string.Equals(mode, "Name", StringComparison.OrdinalIgnoreCase))
            {
                return "Name";
            }

            return string.Equals(mode, "Type", StringComparison.OrdinalIgnoreCase) ? "Type" : "Off";
        }

        private static string NormalizeChestLabelsMode(AppSettings settings)
        {
            if (settings == null)
            {
                return ChestLabelsModeOff;
            }

            var mode = settings.InformationChestNameLabelsMode;
            if (string.Equals(mode, ChestLabelsModeAlways, StringComparison.OrdinalIgnoreCase))
            {
                return ChestLabelsModeAlways;
            }

            if (string.Equals(mode, ChestLabelsModeOpened, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Known", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Open", StringComparison.OrdinalIgnoreCase))
            {
                return ChestLabelsModeOpened;
            }

            return settings.InformationChestNameLabelsEnabled ? ChestLabelsModeOpened : ChestLabelsModeOff;
        }

        private static bool TryParseChestKey(string key, string currentWorldKey, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var lastSeparator = key.LastIndexOf('|');
            var secondSeparator = lastSeparator <= 0 ? -1 : key.LastIndexOf('|', lastSeparator - 1);
            if (secondSeparator <= 0 || lastSeparator <= secondSeparator + 1 || lastSeparator >= key.Length - 1)
            {
                return false;
            }

            var worldKey = key.Substring(0, secondSeparator);
            if (!WorldKeysMatch(worldKey, currentWorldKey))
            {
                return false;
            }

            return int.TryParse(key.Substring(secondSeparator + 1, lastSeparator - secondSeparator - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                   int.TryParse(key.Substring(lastSeparator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        }

        private static bool WorldKeysMatch(string storedWorldKey, string currentWorldKey)
        {
            if (string.Equals(storedWorldKey ?? string.Empty, currentWorldKey ?? string.Empty, StringComparison.Ordinal))
            {
                return true;
            }

            var storedId = ExtractWorldId(storedWorldKey);
            var currentId = ExtractWorldId(currentWorldKey);
            return !string.IsNullOrWhiteSpace(storedId) &&
                   !string.IsNullOrWhiteSpace(currentId) &&
                   string.Equals(storedId, currentId, StringComparison.Ordinal);
        }

        private static string ExtractWorldId(string worldKey)
        {
            if (string.IsNullOrWhiteSpace(worldKey))
            {
                return string.Empty;
            }

            var marker = worldKey.LastIndexOf('#');
            if (marker < 0 || marker >= worldKey.Length - 1)
            {
                return string.Empty;
            }

            return worldKey.Substring(marker + 1).Trim();
        }

        private struct ChestScanCandidate
        {
            public int ChestX;
            public int ChestY;
            public long Key;
            public int TileType;
            public int TileStyle;
            public float WorldX;
            public float WorldY;
        }

        private struct ChestNameCacheKey : IEquatable<ChestNameCacheKey>
        {
            private readonly string _worldKey;
            private readonly string _worldRecordKey;
            private readonly int _chestX;
            private readonly int _chestY;
            private readonly int _tileType;
            private readonly int _tileStyle;
            private readonly string _languageSignature;
            private readonly string _recordSignature;

            public ChestNameCacheKey(
                string worldKey,
                string worldRecordKey,
                int chestX,
                int chestY,
                int tileType,
                int tileStyle,
                string languageSignature,
                string recordSignature)
            {
                _worldKey = worldKey ?? string.Empty;
                _worldRecordKey = worldRecordKey ?? string.Empty;
                _chestX = chestX;
                _chestY = chestY;
                _tileType = tileType;
                _tileStyle = tileStyle;
                _languageSignature = languageSignature ?? string.Empty;
                _recordSignature = recordSignature ?? string.Empty;
            }

            public bool Equals(ChestNameCacheKey other)
            {
                return _chestX == other._chestX &&
                       _chestY == other._chestY &&
                       _tileType == other._tileType &&
                       _tileStyle == other._tileStyle &&
                       string.Equals(_worldKey, other._worldKey, StringComparison.Ordinal) &&
                       string.Equals(_worldRecordKey, other._worldRecordKey, StringComparison.Ordinal) &&
                       string.Equals(_languageSignature, other._languageSignature, StringComparison.Ordinal) &&
                       string.Equals(_recordSignature, other._recordSignature, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ChestNameCacheKey && Equals((ChestNameCacheKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_worldKey ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_worldRecordKey ?? string.Empty);
                    hash = hash * 31 + _chestX;
                    hash = hash * 31 + _chestY;
                    hash = hash * 31 + _tileType;
                    hash = hash * 31 + _tileStyle;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_languageSignature ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_recordSignature ?? string.Empty);
                    return hash;
                }
            }
        }

        private struct ChestLabelCacheSignature
        {
            public uint Hash;
            public string Mode;
            public string WorldKey;
            public string WorldRecordKey;
            public string PlayerRecordKey;
            public int ScreenChunkX;
            public int ScreenChunkY;
            public int ScreenWidth;
            public int ScreenHeight;
            public int PlayerChunkX;
            public int PlayerChunkY;
            public string StyleSignature;
            public string OpenedChestsHash;

            public ChestLabelCacheSignature(
                uint hash,
                string mode,
                string worldKey,
                string worldRecordKey,
                string playerRecordKey,
                int screenChunkX,
                int screenChunkY,
                int screenWidth,
                int screenHeight,
                int playerChunkX,
                int playerChunkY,
                string styleSignature,
                string openedChestsHash)
            {
                Hash = hash;
                Mode = mode ?? string.Empty;
                WorldKey = worldKey ?? string.Empty;
                WorldRecordKey = worldRecordKey ?? string.Empty;
                PlayerRecordKey = playerRecordKey ?? string.Empty;
                ScreenChunkX = screenChunkX;
                ScreenChunkY = screenChunkY;
                ScreenWidth = screenWidth;
                ScreenHeight = screenHeight;
                PlayerChunkX = playerChunkX;
                PlayerChunkY = playerChunkY;
                StyleSignature = styleSignature ?? string.Empty;
                OpenedChestsHash = openedChestsHash ?? string.Empty;
            }
        }

        private sealed class ChestLabel
        {
            public int TileX;
            public int TileY;
            public float WorldX;
            public float WorldY;
            public string Name;
        }

        private sealed class SignTextLabel
        {
            public int TileX;
            public int TileY;
            public float WorldLeft;
            public float WorldTop;
            public float WorldRight;
            public string Text;
            public int TextHash;
        }

        private sealed class SignTextLayout
        {
            public SignTextLayout(string[] displayLines, int[] lineWidths, int lineHeight, int totalHeight, float scale, bool hasVisibleText)
            {
                DisplayLines = displayLines ?? new string[0];
                LineWidths = lineWidths ?? new int[0];
                LineHeight = lineHeight;
                TotalHeight = totalHeight;
                Scale = scale;
                HasVisibleText = hasVisibleText;
            }

            public string[] DisplayLines { get; private set; }

            public int[] LineWidths { get; private set; }

            public int LineHeight { get; private set; }

            public int TotalHeight { get; private set; }

            public float Scale { get; private set; }

            public bool HasVisibleText { get; private set; }
        }

        private struct SignTextLayoutKey : IEquatable<SignTextLayoutKey>
        {
            private readonly string _text;
            private readonly int _textHash;
            private readonly int _textLength;
            private readonly string _mode;
            private readonly int _maxLines;
            private readonly int _maxCharacters;
            private readonly int _scaleKey;
            private readonly string _fontSignature;
            private readonly int _cacheGeneration;

            public SignTextLayoutKey(
                string text,
                int textHash,
                string mode,
                int maxLines,
                int maxCharacters,
                int scaleKey,
                string fontSignature,
                int cacheGeneration)
            {
                _text = text ?? string.Empty;
                _textHash = textHash;
                _textLength = _text.Length;
                _mode = mode ?? string.Empty;
                _maxLines = maxLines;
                _maxCharacters = maxCharacters;
                _scaleKey = scaleKey;
                _fontSignature = fontSignature ?? string.Empty;
                _cacheGeneration = cacheGeneration;
            }

            public bool Equals(SignTextLayoutKey other)
            {
                return _textHash == other._textHash &&
                       _textLength == other._textLength &&
                       _maxLines == other._maxLines &&
                       _maxCharacters == other._maxCharacters &&
                       _scaleKey == other._scaleKey &&
                       _cacheGeneration == other._cacheGeneration &&
                       string.Equals(_text, other._text, StringComparison.Ordinal) &&
                       string.Equals(_mode, other._mode, StringComparison.Ordinal) &&
                       string.Equals(_fontSignature, other._fontSignature, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is SignTextLayoutKey && Equals((SignTextLayoutKey)obj);
            }

            public override int GetHashCode()
            {
                var hash = AddHash(17, _textHash);
                hash = AddHash(hash, _textLength);
                hash = AddHash(hash, HashText(_mode));
                hash = AddHash(hash, _maxLines);
                hash = AddHash(hash, _maxCharacters);
                hash = AddHash(hash, _scaleKey);
                hash = AddHash(hash, HashText(_fontSignature));
                hash = AddHash(hash, _cacheGeneration);
                return hash;
            }
        }

        internal sealed class InformationSignTextLayoutSnapshot
        {
            public InformationSignTextLayoutSnapshot(
                int lineCount,
                string firstLineText,
                int firstLineWidth,
                int lineHeight,
                int totalHeight,
                int rebuildCount)
            {
                LineCount = lineCount;
                FirstLineText = firstLineText ?? string.Empty;
                FirstLineWidth = firstLineWidth;
                LineHeight = lineHeight;
                TotalHeight = totalHeight;
                RebuildCount = rebuildCount;
            }

            public int LineCount { get; private set; }

            public string FirstLineText { get; private set; }

            public int FirstLineWidth { get; private set; }

            public int LineHeight { get; private set; }

            public int TotalHeight { get; private set; }

            public int RebuildCount { get; private set; }
        }

        private sealed class NpcLabel
        {
            public int Index;
            public int WhoAmI;
            public int Type;
            public float WorldX;
            public float WorldY;
            public int Life;
            public int LifeMax;
            public bool TownNpc;
            public bool Friendly;
            public bool Hidden;
            public bool Critter;
            public string Text;
            public InformationColor Color;
            public float MaxDistance;
            public float FontScale;
        }

        private struct NpcLabelSnapshot
        {
            public int Type;
            public int WhoAmI;
            public int Life;
            public int LifeMax;
            public bool TownNpc;
            public bool Friendly;
            public bool Hidden;
            public bool Critter;
            public float WorldX;
            public float WorldY;
        }

        private sealed class NpcSegmentInfo
        {
            public int Index;
            public int WhoAmI;
            public int RealLife;
            public int GroupKey;
            public int GroupSize;
            public int NeighborCount;
            public int[] References;
        }

        private enum NpcSegmentRole
        {
            Unknown,
            Head,
            Body,
            Tail
        }

        private struct TileHighlightScanSignature
        {
            public uint Hash { get; private set; }
            public TileHighlightScanBounds Bounds { get; private set; }

            public TileHighlightScanSignature(uint hash, TileHighlightScanBounds bounds)
            {
                Hash = hash;
                Bounds = bounds;
            }
        }

        private struct TileHighlightScanBounds
        {
            public int MinX { get; private set; }
            public int MinY { get; private set; }
            public int MaxX { get; private set; }
            public int MaxY { get; private set; }

            public TileHighlightScanBounds(int minX, int minY, int maxX, int maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }
        }

        private struct TileHighlightColors
        {
            public InformationColor LifeCrystal { get; private set; }
            public InformationColor ManaCrystal { get; private set; }
            public InformationColor Digtoise { get; private set; }
            public InformationColor LifeFruit { get; private set; }
            public InformationColor DragonEgg { get; private set; }

            public TileHighlightColors(InformationColor lifeCrystal, InformationColor manaCrystal, InformationColor digtoise, InformationColor lifeFruit, InformationColor dragonEgg)
            {
                LifeCrystal = lifeCrystal;
                ManaCrystal = manaCrystal;
                Digtoise = digtoise;
                LifeFruit = lifeFruit;
                DragonEgg = dragonEgg;
            }
        }

        private struct TileHighlight
        {
            public int TileX { get; private set; }
            public int TileY { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public int PixelWidth { get; private set; }
            public int PixelHeight { get; private set; }
            public InformationColor Color { get; private set; }

            public TileHighlight(int tileX, int tileY, int width, int height, InformationColor color)
            {
                TileX = tileX;
                TileY = tileY;
                Width = Math.Max(1, width);
                Height = Math.Max(1, height);
                PixelWidth = Width * TileSize;
                PixelHeight = Height * TileSize;
                Color = color;
            }
        }

        private struct TilePoint
        {
            public int X;
            public int Y;

            public TilePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

    }
}
