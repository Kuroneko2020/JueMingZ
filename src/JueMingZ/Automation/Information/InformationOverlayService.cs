using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private const int TileSize = 16;
        private const float ChestLabelMaxDistance = 1600f;
        private const int ChestTileScanMarginTiles = 6;
        private const float ChestCacheCullPadding = ChestTileScanMarginTiles * TileSize;
        private const ulong NpcScanIntervalTicks = 12;
        private const ulong TileScanIntervalTicks = 60;
        private const ulong ChestScanIntervalTicks = 60;
        private const ulong SignScanIntervalTicks = 60;
        private const ulong StatusRefreshTicks = 30;
        private const string ChestLabelsModeAlways = "Always";
        private const string ChestLabelsModeOpened = "Opened";
        private const string ChestLabelsModeOff = "Off";
        private static readonly object SyncRoot = new object();
        private static readonly InformationWorldLabelRenderer LabelRenderer = new InformationWorldLabelRenderer();
        private static readonly List<NpcLabel> CachedNpcLabels = new List<NpcLabel>();
        private static readonly List<TileHighlight> CachedTileHighlights = new List<TileHighlight>();
        private static readonly List<ChestLabel> CachedChestLabels = new List<ChestLabel>();
        private static readonly List<SignTextLabel> CachedSignTextLabels = new List<SignTextLabel>();
        private static readonly List<SignTextLabel> CachedTombstoneTextLabels = new List<SignTextLabel>();
        private static readonly List<InformationStatusLine> CachedStatusLines = new List<InformationStatusLine>();
        private static readonly HashSet<int> GoldCritterNpcTypes = new HashSet<int>
        {
            442, 443, 444, 445, 446, 447, 448, 539, 592, 593, 601, 605, 613, 627
        };
        private static readonly InformationOverlayDiagnostics Diagnostics = new InformationOverlayDiagnostics();
        private static readonly Dictionary<int, bool> ChestTileTypeCache = new Dictionary<int, bool>();
        private static ulong _lastNpcScanTick;
        private static uint _lastNpcLabelSignatureHash;
        private static ulong _lastTileScanTick;
        private static ulong _lastChestScanTick;
        private static uint _lastChestLabelSignatureHash;
        private static string _lastOpenedChestsHash = "0";
        private static string _lastOpenedChestsHashPlayerKey = string.Empty;
        private static string _lastOpenedChestsHashWorldKey = string.Empty;
        private static ulong _lastOpenedChestsHashTick;
        private static bool _openedChestsHashDirty = true;
        private static ulong _lastSignScanTick;
        private static ulong _lastTombstoneScanTick;
        private static ulong _lastStatusRefreshTick;
        private static string _lastStatusStyleSignature = string.Empty;
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
                    LastSkipReason = Diagnostics.LastSkipReason
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
                if (!TryBuildContext(out context, out skip))
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
                if (!TryBuildContext(out context, out skip))
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
            for (var index = 0; index < labels.Count && drawn < MaxNpcLabelsPerFrame; index++)
            {
                var label = labels[index];
                if (LabelRenderer.DrawWorldLabel(spriteBatch, context, label.WorldX, label.WorldY, label.Text, label.Color, label.MaxDistance, false, -1f, label.FontScale))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static List<NpcLabel> GetNpcLabels(InformationWorldContext context, AppSettings settings)
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
                    RefreshCachedNpcLabelPositions(context, CachedNpcLabels);
                    return CachedNpcLabels;
                }

                CachedNpcLabels.Clear();
                ScanNpcLabels(context, settings, CachedNpcLabels);
                _lastNpcScanTick = context.GameUpdateCount;
                _lastNpcLabelSignatureHash = signatureHash;
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

        private static void RefreshCachedNpcLabelPositions(InformationWorldContext context, IList<NpcLabel> labels)
        {
            if (context == null || labels == null || labels.Count == 0)
            {
                return;
            }

            NPC[] typedNpcs;
            object reflectedNpcs;
            int count;
            if (!TryGetNpcCollection(context, out typedNpcs, out reflectedNpcs, out count))
            {
                labels.Clear();
                return;
            }

            for (var labelIndex = labels.Count - 1; labelIndex >= 0; labelIndex--)
            {
                var label = labels[labelIndex];
                if (label == null || label.Index < 0 || label.Index >= count)
                {
                    labels.RemoveAt(labelIndex);
                    continue;
                }

                var npc = typedNpcs != null ? (object)typedNpcs[label.Index] : InformationReflection.GetIndexedValue(reflectedNpcs, label.Index);
                NpcLabelSnapshot snapshot;
                if (!TryReadNpcLabelSnapshot(npc, label.Index, out snapshot) ||
                    snapshot.Type != label.Type ||
                    (label.WhoAmI >= 0 && snapshot.WhoAmI >= 0 && snapshot.WhoAmI != label.WhoAmI))
                {
                    labels.RemoveAt(labelIndex);
                    continue;
                }

                label.WorldX = snapshot.WorldX;
                label.WorldY = snapshot.WorldY;
            }
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

            var labels = GetChestLabels(context, settings, mode);
            SortChestLabelsForDrawing(context, labels);
            var drawn = 0;
            var color = InformationColorHelper.ChestName(settings);
            var fontScale = InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.ChestNameFeatureId);
            for (var index = 0; index < labels.Count && drawn < MaxChestLabelsPerFrame; index++)
            {
                var label = labels[index];
                if (LabelRenderer.DrawWorldLabel(spriteBatch, context, label.WorldX, label.WorldY, label.Name, color, ChestLabelMaxDistance, false, 0f, (float)fontScale))
                {
                    drawn++;
                }
            }

            return drawn;
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

        private static void SortChestLabelsForDrawing(InformationWorldContext context, List<ChestLabel> labels)
        {
            if (context == null || labels == null || labels.Count <= 1)
            {
                return;
            }

            labels.Sort((left, right) => CompareChestLabelsForDrawing(context, left, right));
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
                IList<string> lines;
                if (!TryBuildSignTextDisplayLines(label.Text, mode, settings.InformationSignTextMaxLines, settings.InformationSignTextMaxCharacters, fontScale, out lines))
                {
                    continue;
                }

                if (DrawSignTextBlock(spriteBatch, context, label, lines, color, fontScale))
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
                IList<string> lines;
                if (!TryBuildSignTextDisplayLines(label.Text, mode, settings.InformationTombstoneTextMaxLines, settings.InformationTombstoneTextMaxCharacters, fontScale, out lines))
                {
                    continue;
                }

                if (DrawSignTextBlock(spriteBatch, context, label, lines, color, fontScale))
                {
                    drawn++;
                }
            }

            return drawn;
        }

        private static bool DrawSignTextBlock(object spriteBatch, InformationWorldContext context, SignTextLabel label, IList<string> lines, InformationColor color, float scale)
        {
            if (spriteBatch == null ||
                context == null ||
                label == null ||
                lines == null ||
                lines.Count <= 0 ||
                !LabelRenderer.CanDraw(context, label.WorldLeft, label.WorldTop, 1600f, false))
            {
                return false;
            }

            var lineHeight = Math.Max(16, UiTextRenderer.EstimateTextHeight(scale) + 5);
            var anyVisibleText = false;
            for (var index = 0; index < lines.Count; index++)
            {
                anyVisibleText |= UiTextRenderer.EstimateTextWidth(lines[index], scale) > 0;
            }

            if (!anyVisibleText)
            {
                return false;
            }

            var signCenterX = ((label.WorldLeft + label.WorldRight) * 0.5f) - context.ScreenX;
            var signTop = label.WorldTop - context.ScreenY;
            var ok = false;
            for (var index = 0; index < lines.Count; index++)
            {
                var lineWidth = UiTextRenderer.EstimateTextWidth(lines[index], scale);
                var drawX = CalculateSignTextLineX(signCenterX, lineWidth, context.ScreenWidth);
                ok |= UiTextRenderer.DrawText(spriteBatch, lines[index], drawX, signTop + index * lineHeight, color.R, color.G, color.B, color.A, scale);
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

        internal static bool IsChestTileTypeForTesting(int tileType)
        {
            return IsChestTileType(null, tileType);
        }

        internal static bool TryNormalizeChestOriginFromFrameForTesting(int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            return TryNormalizeChestOriginFromFrame(tileX, tileY, frameX, frameY, out chestX, out chestY);
        }

        internal static string BuildChestLabelCacheSignatureForTesting(InformationWorldContext context, AppSettings settings, string mode)
        {
            return BuildChestLabelCacheSignature(context, settings, mode);
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
            var labels = new List<ChestLabel>();
            var count = worldXs == null ? 0 : worldXs.Length;
            for (var index = 0; index < count; index++)
            {
                labels.Add(new ChestLabel
                {
                    TileX = index,
                    TileY = 0,
                    WorldX = worldXs[index],
                    WorldY = worldYs != null && index < worldYs.Length ? worldYs[index] : 0f,
                    Name = "宝箱"
                });
            }

            SortChestLabelsForDrawing(context, labels);
            var result = new int[labels.Count];
            for (var index = 0; index < labels.Count; index++)
            {
                result[index] = labels[index].TileX;
            }

            return result;
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
            for (var index = 0; index < highlights.Count; index++)
            {
                var highlight = highlights[index];
                var x = (int)Math.Round(highlight.TileX * TileSize - context.ScreenX);
                var y = (int)Math.Round(highlight.TileY * TileSize - context.ScreenY);
                var width = Math.Max(TileSize, highlight.Width * TileSize);
                var height = Math.Max(TileSize, highlight.Height * TileSize);
                var color = highlight.Color;
                var pulse = 155 + (int)(Math.Abs(Math.Sin(context.GameUpdateCount / 12d)) * 80d);
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
                var styleSignature = BuildStatusStyleSignature(settings);
                if (context.GameUpdateCount != 0 &&
                    _lastStatusRefreshTick != 0 &&
                    string.Equals(_lastStatusStyleSignature, styleSignature, StringComparison.Ordinal) &&
                    context.GameUpdateCount >= _lastStatusRefreshTick &&
                    context.GameUpdateCount - _lastStatusRefreshTick < StatusRefreshTicks)
                {
                    return CachedStatusLines;
                }

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
                    hasFishingBobber = TryFindLocalBobber(context, out bobberX, out bobberY);
                    if (hasFishingBobber &&
                        (settings.InformationFishingCatchesEnabled || !IsFishingFilterDisabled(settings)))
                    {
                        fishingCandidates = ResolveFishingCatchCandidates(context, bobberX, bobberY, out fishingMessage);
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
                _lastStatusStyleSignature = styleSignature;
                return CachedStatusLines;
            }
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
                context.GameUpdateCount - _lastOpenedChestsHashTick < ChestScanIntervalTicks)
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

        private static List<ChestLabel> GetChestLabels(InformationWorldContext context, AppSettings settings, string mode)
        {
            lock (SyncRoot)
            {
                var signatureHash = BuildChestLabelCacheSignatureHash(context, settings, mode);
                if (context.GameUpdateCount != 0 &&
                    _lastChestScanTick != 0 &&
                    _lastChestLabelSignatureHash == signatureHash &&
                    context.GameUpdateCount >= _lastChestScanTick &&
                    context.GameUpdateCount - _lastChestScanTick < ChestScanIntervalTicks)
                {
                    return CachedChestLabels;
                }

                CachedChestLabels.Clear();
                if (string.Equals(mode, ChestLabelsModeAlways, StringComparison.OrdinalIgnoreCase))
                {
                    AddAllChestLabels(context, CachedChestLabels);
                }
                else
                {
                    var chestNames = BuildChestNameLookup(context.MainType);
                    var openedChests = PlayerWorldBehaviorStore.GetOpenedChests(BuildBehaviorContext(context));
                    for (var index = 0; index < openedChests.Count; index++)
                    {
                        var opened = openedChests[index];
                        if (opened == null || opened.X <= 0 || opened.Y <= 0)
                        {
                            continue;
                        }

                        string name;
                        if (!chestNames.TryGetValue(BuildChestPositionKey(opened.X, opened.Y), out name))
                        {
                            name = "宝箱";
                        }

                        var worldX = opened.X * TileSize + TileSize;
                        var worldY = opened.Y * TileSize + TileSize;
                        if (!CanCacheChestLabel(context, worldX, worldY))
                        {
                            continue;
                        }

                        CachedChestLabels.Add(new ChestLabel
                        {
                            TileX = opened.X,
                            TileY = opened.Y,
                            WorldX = worldX,
                            WorldY = worldY,
                            Name = string.IsNullOrWhiteSpace(name) ? "宝箱" : name
                        });
                    }
                }

                _lastChestScanTick = context.GameUpdateCount;
                _lastChestLabelSignatureHash = signatureHash;
                return CachedChestLabels;
            }
        }

        private static string BuildChestLabelCacheSignature(InformationWorldContext context, AppSettings settings, string mode)
        {
            return BuildChestLabelCacheSignatureHash(context, settings, mode).ToString("X8", CultureInfo.InvariantCulture);
        }

        private static uint BuildChestLabelCacheSignatureHash(InformationWorldContext context, AppSettings settings, string mode)
        {
            var screenBucketX = context == null ? 0 : (int)Math.Floor(context.ScreenX / 128f);
            var screenBucketY = context == null ? 0 : (int)Math.Floor(context.ScreenY / 128f);
            var screenWidthBucket = context == null ? 0 : Math.Max(0, context.ScreenWidth / 128);
            var screenHeightBucket = context == null ? 0 : Math.Max(0, context.ScreenHeight / 128);
            unchecked
            {
                var hash = 2166136261u;
                AddHashValue(ref hash, mode);
                AddHashValue(ref hash, context == null ? string.Empty : context.WorldKey);
                AddHashInt(ref hash, screenBucketX);
                AddHashInt(ref hash, screenBucketY);
                AddHashInt(ref hash, screenWidthBucket);
                AddHashInt(ref hash, screenHeightBucket);
                AddHashValue(ref hash, context == null ? string.Empty : context.PlayerRecordKey);
                AddHashValue(ref hash, context == null ? string.Empty : context.WorldRecordKey);
                if (string.Equals(mode, ChestLabelsModeOpened, StringComparison.OrdinalIgnoreCase))
                {
                    AddHashValue(ref hash, GetOpenedChestsHashCached(context));
                }

                return hash;
            }
        }

        private static void AddAllChestLabels(InformationWorldContext context, IList<ChestLabel> labels)
        {
            var added = new HashSet<long>();
            AddVisibleChestTileLabels(context, labels, null, added);
        }

        private static void AddVisibleChestTileLabels(InformationWorldContext context, IList<ChestLabel> labels, IDictionary<long, string> loadedChestNames, ISet<long> added)
        {
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

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    bool active;
                    int tileType;
                    int frameX;
                    int frameY;
                    if (!TryReadTileActiveTypeAndFrame(tiles, x, y, out active, out tileType, out frameX, out frameY) || !active)
                    {
                        continue;
                    }

                    if (!IsChestTileType(context.MainType, tileType))
                    {
                        continue;
                    }

                    int chestX;
                    int chestY;
                    if (!TryNormalizeChestOriginFromFrame(x, y, frameX, frameY, out chestX, out chestY))
                    {
                        continue;
                    }

                    var key = BuildChestPositionKey(chestX, chestY);
                    if (added != null && added.Contains(key))
                    {
                        continue;
                    }

                    var worldX = chestX * TileSize + TileSize;
                    var worldY = chestY * TileSize + TileSize;
                    if (!CanCacheChestLabel(context, worldX, worldY))
                    {
                        continue;
                    }

                    string name;
                    if (loadedChestNames == null || !loadedChestNames.TryGetValue(key, out name))
                    {
                        if (!TryResolveLoadedChestNameAt(context.MainType, chestX, chestY, out name))
                        {
                            name = ResolveChestTileDisplayName(context.MainType, tileType);
                        }
                    }

                    if (added != null)
                    {
                        added.Add(key);
                    }

                    labels.Add(new ChestLabel
                    {
                        TileX = chestX,
                        TileY = chestY,
                        WorldX = worldX,
                        WorldY = worldY,
                        Name = string.IsNullOrWhiteSpace(name) ? "宝箱" : name
                    });
                }
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
            active = false;
            tileType = -1;
            frameX = 0;
            frameY = 0;

            try
            {
                Tile typedTile;
                if (TerrariaTileReadCompat.TryGetTile(x, y, out typedTile))
                {
                    active = TerrariaTileReadCompat.IsActive(typedTile);
                    tileType = TerrariaTileReadCompat.Type(typedTile);
                    frameX = TerrariaTileReadCompat.FrameX(typedTile);
                    frameY = TerrariaTileReadCompat.FrameY(typedTile);
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
            frameX = ReadTileFrameX(tile);
            frameY = ReadTileFrameY(tile);
            return true;
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
            try
            {
                var typedChestIndex = Chest.FindChest(x, y);
                Chest typedChest;
                if (TerrariaMainCompat.TryGetChest(typedChestIndex, out typedChest))
                {
                    name = typedChest.name ?? string.Empty;
                    return !string.IsNullOrWhiteSpace(name);
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

            name = FirstNonEmpty(
                InformationReflection.TryReadString(chest, "name"),
                InformationReflection.TryReadString(chest, "Name"));
            return !string.IsNullOrWhiteSpace(name);
        }

        private static bool TryNormalizeChestOriginFromFrame(int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            chestX = tileX - PositiveModulo(frameX / 18, 2);
            chestY = tileY - PositiveModulo(frameY / 18, 2);
            return chestX >= 0 && chestY >= 0;
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
            if (tileType < 0)
            {
                return false;
            }

            bool cached;
            if (ChestTileTypeCache.TryGetValue(tileType, out cached))
            {
                return cached;
            }

            bool value;
            if (TryReadBoolSet(InformationReflection.GetStaticMember(mainType, "tileContainer"), tileType, out value) && value)
            {
                ChestTileTypeCache[tileType] = true;
                return true;
            }

            var setsType = InformationReflection.FindType("Terraria.ID.TileID+Sets");
            if (TryReadBoolSet(InformationReflection.GetStaticMember(setsType, "BasicChest"), tileType, out value) && value)
            {
                ChestTileTypeCache[tileType] = true;
                return true;
            }

            if (TryReadBoolSet(InformationReflection.GetStaticMember(setsType, "BasicChestFake"), tileType, out value) && value)
            {
                ChestTileTypeCache[tileType] = true;
                return true;
            }

            var fallback = tileType == 21 || tileType == 467;
            ChestTileTypeCache[tileType] = fallback;
            return fallback;
        }

        private static bool TryReadBoolSet(object source, int index, out bool value)
        {
            value = false;
            if (source == null || index < 0)
            {
                return false;
            }

            return TryConvertBool(InformationReflection.GetIndexedValue(source, index), out value);
        }

        private static string ResolveChestTileDisplayName(Type mainType, int tileType)
        {
            object lookup;
            var mapHelperType = InformationReflection.FindType("Terraria.Map.MapHelper") ??
                                InformationReflection.FindType("Terraria.MapHelper");
            if (InformationReflection.TryInvokeStatic(mapHelperType, "TileToLookup", new object[] { tileType, 0 }, out lookup) && lookup != null)
            {
                object rawName;
                var langType = InformationReflection.FindType("Terraria.Lang") ??
                               InformationReflection.FindType("Terraria.Localization.Lang");
                if (InformationReflection.TryInvokeStatic(langType, "GetMapObjectName", new[] { lookup }, out rawName) && rawName != null)
                {
                    var name = Convert.ToString(rawName, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }

            return "宝箱";
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

                        result[BuildChestPositionKey(chest.x, chest.y)] = string.IsNullOrWhiteSpace(chest.name) ? "宝箱" : chest.name;
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
                result[BuildChestPositionKey(chestX, chestY)] = string.IsNullOrWhiteSpace(name) ? "宝箱" : name;
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
                    Text = text
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
                if (context.GameUpdateCount != 0 &&
                    _lastTileScanTick != 0 &&
                    context.GameUpdateCount >= _lastTileScanTick &&
                    context.GameUpdateCount - _lastTileScanTick < TileScanIntervalTicks)
                {
                    return CachedTileHighlights;
                }

                CachedTileHighlights.Clear();
                ScanTileHighlights(context, settings, CachedTileHighlights);
                _lastTileScanTick = context.GameUpdateCount;
                return CachedTileHighlights;
            }
        }

        private static void ScanTileHighlights(InformationWorldContext context, AppSettings settings, IList<TileHighlight> results)
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

            var minX = Math.Max(0, (int)Math.Floor(context.ScreenX / TileSize) - 4);
            var minY = Math.Max(0, (int)Math.Floor(context.ScreenY / TileSize) - 4);
            var maxX = Math.Max(minX, (int)Math.Ceiling((context.ScreenX + context.ScreenWidth) / TileSize) + 4);
            var maxY = Math.Max(minY, (int)Math.Ceiling((context.ScreenY + context.ScreenHeight) / TileSize) + 4);
            var visited = new HashSet<long>();
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
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, InformationColorHelper.LifeCrystal(settings), visited, results);
                    }
                    else if (settings.InformationHighlightManaCrystalEnabled && tileType == manaCrystalType)
                    {
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, InformationColorHelper.ManaCrystal(settings), visited, results);
                    }
                    else if (settings.InformationHighlightDigtoiseEnabled && tileType == digtoiseType)
                    {
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, InformationColorHelper.Digtoise(settings), visited, results);
                    }
                    else if (settings.InformationHighlightLifeFruitEnabled && tileType == lifeFruitType)
                    {
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, InformationColorHelper.LifeFruit(settings), visited, results);
                    }
                    else if (settings.InformationHighlightDragonEggEnabled && dragonEggType >= 0 && tileType == dragonEggType)
                    {
                        AddTileHighlightGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, InformationColorHelper.DragonEgg(settings), visited, results);
                    }
                }
            }
        }

        private static void AddTileHighlightGroup(object tiles, int startX, int startY, int minX, int minY, int maxX, int maxY, int tileType, InformationColor color, ISet<long> visited, IList<TileHighlight> results)
        {
            var startKey = BuildTileVisitKey(tileType, startX, startY);
            if (visited.Contains(startKey))
            {
                return;
            }

            var stack = new List<TilePoint>();
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
                CachedChestLabels.Clear();
                _lastChestScanTick = 0;
                _lastChestLabelSignatureHash = 0;
                _openedChestsHashDirty = true;
            }
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
            x = 0f;
            y = 0f;
            int myPlayer;
            InformationReflection.TryReadStaticInt(context.MainType, "myPlayer", out myPlayer);
            var projectiles = InformationReflection.GetStaticMember(context.MainType, "projectile");
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
                    return true;
                }

                if (InformationReflection.TryReadVectorMember(projectile, "position", out x, out y))
                {
                    return true;
                }
            }

            return false;
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

        private static IList<FishingCatchCandidate> ResolveFishingCatchCandidates(InformationWorldContext context, float bobberX, float bobberY, out string message)
        {
            try
            {
                return InformationFishingCatchResolver.ResolveCatchCandidates(context, bobberX, bobberY, out message);
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
        }

        private sealed class NpcLabel
        {
            public int Index;
            public int WhoAmI;
            public int Type;
            public float WorldX;
            public float WorldY;
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

        private sealed class TileHighlight
        {
            public int TileX { get; private set; }
            public int TileY { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public InformationColor Color { get; private set; }

            public TileHighlight(int tileX, int tileY, int width, int height, InformationColor color)
            {
                TileX = tileX;
                TileY = tileY;
                Width = Math.Max(1, width);
                Height = Math.Max(1, height);
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
