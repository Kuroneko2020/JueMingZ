using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal static class InformationNpcLabelService
    {
        internal const int MaxLabelsPerFrame = 120;
        private const ulong ScanIntervalTicks = 12;
        private const float EnemyHealthFontScaleDelta = 0.13f;
        private static readonly object SyncRoot = new object();
        private static readonly NpcLabel[] EmptyLabels = new NpcLabel[0];
        private static readonly List<NpcLabel> LabelBuildBuffer = new List<NpcLabel>();
        private static readonly HashSet<int> EnemyGroupBuildBuffer = new HashSet<int>();
        private static readonly HashSet<int> GoldCritterNpcTypes = new HashSet<int>
        {
            442, 443, 444, 445, 446, 447, 448, 539, 592, 593, 601, 605, 613, 627
        };

        private static NpcLabel[] _cachedLabels = EmptyLabels;
        private static ulong _lastScanTick;
        private static uint _lastSignatureHash;
        private static long _npcLabelSnapshotRefreshCount;
        private static long _worldLabelSnapshotRefreshCount;
        private static bool _targetDummyNpcTypeResolved;
        private static int _targetDummyNpcType = 488;
        private static bool _skeletonMerchantNpcTypeResolved;
        private static int _skeletonMerchantNpcType = 453;
        private static bool _critterSetResolved;
        private static object _critterSet;

        internal static long NpcLabelSnapshotRefreshCount
        {
            get
            {
                lock (SyncRoot)
                {
                    return _npcLabelSnapshotRefreshCount;
                }
            }
        }

        internal static long WorldLabelSnapshotRefreshCount
        {
            get
            {
                lock (SyncRoot)
                {
                    return _worldLabelSnapshotRefreshCount;
                }
            }
        }

        internal static NpcLabel[] GetLabels(InformationWorldContext context, AppSettings settings)
        {
            lock (SyncRoot)
            {
                var signatureHash = BuildSignatureHash(settings);
                if (context != null &&
                    context.GameUpdateCount != 0 &&
                    _lastScanTick != 0 &&
                    _lastSignatureHash == signatureHash &&
                    context.GameUpdateCount >= _lastScanTick &&
                    context.GameUpdateCount - _lastScanTick < ScanIntervalTicks)
                {
                    if (RefreshCachedPositions(context, _cachedLabels))
                    {
                        return _cachedLabels;
                    }
                }

                LabelBuildBuffer.Clear();
                ScanLabels(context, settings, LabelBuildBuffer);
                _cachedLabels = LabelBuildBuffer.Count == 0 ? EmptyLabels : LabelBuildBuffer.ToArray();
                _lastScanTick = context == null ? 0 : context.GameUpdateCount;
                _lastSignatureHash = signatureHash;
                unchecked
                {
                    _npcLabelSnapshotRefreshCount++;
                    _worldLabelSnapshotRefreshCount++;
                }

                return _cachedLabels;
            }
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
            return CanReuseSnapshot(
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

        internal static bool CanReuseNpcLabelHealthValuesForTesting(int labelLife, int labelLifeMax, int currentLife, int currentLifeMax)
        {
            return CanReuseHealthValues(
                new NpcLabel
                {
                    HealthText = BuildEnemyHealthText(labelLife, labelLifeMax),
                    HealthLife = labelLife,
                    HealthLifeMax = labelLifeMax
                },
                currentLife,
                currentLifeMax);
        }

        internal static string BuildEnemyHealthTextForTesting(int life, int lifeMax)
        {
            return BuildEnemyHealthText(life, lifeMax);
        }

        internal static float ResolveEnemyHealthFontScaleForTesting(float nameFontScale)
        {
            return ResolveEnemyHealthFontScale(nameFontScale);
        }

        internal static bool IsNpcNameLabelCandidateForTesting(int npcType, bool townNpc)
        {
            return IsNpcNameLabelCandidate(new NpcLabelSnapshot { Type = npcType, TownNpc = townNpc });
        }

        internal static bool IsEnemyNameLabelCandidateForTesting(int npcType, bool friendly, bool critter, int life, int lifeMax)
        {
            return IsEnemyNameLabelCandidate(
                new NpcLabelSnapshot
                {
                    Type = npcType,
                    Friendly = friendly,
                    Critter = critter,
                    Life = life,
                    LifeMax = lifeMax
                });
        }

        private static uint BuildSignatureHash(AppSettings settings)
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

        private static void ScanLabels(InformationWorldContext context, AppSettings settings, IList<NpcLabel> labels)
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

            settings = settings ?? AppSettings.CreateDefault();
            var npcMode = NormalizeNpcMode(settings.InformationNpcNameLabelsMode);
            EnemyGroupBuildBuffer.Clear();
            var segmentInfos = settings.InformationEnemyNameLabelsEnabled
                ? typedNpcs != null
                    ? InformationNpcSegmentService.BuildNpcSegmentInfos(typedNpcs, count)
                    : InformationNpcSegmentService.BuildNpcSegmentInfos(reflectedNpcs, count)
                : null;
            // NPC, critter, and enemy buckets are mutually exclusive. Skeleton
            // merchant stays with NPC labels instead of being swallowed by enemy labels.
            for (var index = 0; index < count && labels.Count < MaxLabelsPerFrame; index++)
            {
                var npc = typedNpcs != null ? (object)typedNpcs[index] : InformationReflection.GetIndexedValue(reflectedNpcs, index);
                NpcLabelSnapshot snapshot;
                if (!TryReadSnapshot(npc, index, out snapshot))
                {
                    continue;
                }

                if (snapshot.Hidden)
                {
                    continue;
                }

                if (IsNpcNameLabelCandidate(snapshot) && !string.Equals(npcMode, "Off", StringComparison.OrdinalIgnoreCase))
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
                        Text = InformationNpcNameCompat.ResolveDisplayName(npc, snapshot.Type, snapshot.WhoAmI, npcMode, context == null ? 0 : context.GameUpdateCount),
                        Color = InformationColorHelper.NpcName(settings),
                        MaxDistance = 1800f,
                        FontScale = (float)InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.NpcNameFeatureId)
                    });
                    continue;
                }

                if (snapshot.Critter && settings.InformationCritterNameLabelsEnabled)
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

                if (settings.InformationEnemyNameLabelsEnabled &&
                    IsEnemyNameLabelCandidate(snapshot))
                {
                    var knownSegmentRole = InformationNpcSegmentService.GetKnownSegmentRole(snapshot.Type);
                    if (knownSegmentRole == NpcSegmentRole.Body)
                    {
                        continue;
                    }

                    NpcSegmentInfo segmentInfo = null;
                    var hasSegmentInfo = segmentInfos != null && segmentInfos.TryGetValue(index, out segmentInfo);
                    if (knownSegmentRole == NpcSegmentRole.Unknown &&
                        hasSegmentInfo &&
                        !InformationNpcSegmentService.ShouldDrawEnemySegmentLabel(segmentInfo.GroupSize, segmentInfo.NeighborCount))
                    {
                        continue;
                    }

                    var groupKey = hasSegmentInfo ? segmentInfo.GroupKey : InformationNpcSegmentService.ResolveSegmentGroupKey(index, snapshot.WhoAmI, -1);
                    if (!EnemyGroupBuildBuffer.Add(groupKey))
                    {
                        continue;
                    }

                    var healthSourceIndex = ResolveEnemyHealthSourceIndex(index, snapshot, segmentInfo, count);
                    var healthLife = snapshot.Life;
                    var healthLifeMax = snapshot.LifeMax;
                    int resolvedHealthLife;
                    int resolvedHealthLifeMax;
                    if (TryReadNpcHealthByIndex(typedNpcs, reflectedNpcs, count, healthSourceIndex, out resolvedHealthLife, out resolvedHealthLifeMax))
                    {
                        healthLife = resolvedHealthLife;
                        healthLifeMax = resolvedHealthLifeMax;
                    }

                    var fontScale = (float)InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.EnemyNameFeatureId);
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
                        HealthText = BuildEnemyHealthText(healthLife, healthLifeMax),
                        HealthSourceIndex = healthSourceIndex,
                        HealthLife = healthLife,
                        HealthLifeMax = healthLifeMax,
                        Color = InformationColorHelper.EnemyName(settings),
                        MaxDistance = 1400f,
                        FontScale = fontScale,
                        HealthFontScale = ResolveEnemyHealthFontScale(fontScale)
                    });
                }
            }
        }

        private static bool RefreshCachedPositions(InformationWorldContext context, NpcLabel[] labels)
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
                if (!TryReadSnapshot(npc, label.Index, out snapshot) ||
                    !CanReuseSnapshot(label, snapshot) ||
                    !CanReuseHealth(typedNpcs, reflectedNpcs, count, label, snapshot))
                {
                    return false;
                }

                label.WorldX = snapshot.WorldX;
                label.WorldY = snapshot.WorldY;
            }

            return true;
        }

        private static bool CanReuseSnapshot(NpcLabel label, NpcLabelSnapshot snapshot)
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
                   GetLifeEligibilityKey(label.Life, label.LifeMax) == GetLifeEligibilityKey(snapshot.Life, snapshot.LifeMax);
        }

        private static bool CanReuseHealth(NPC[] typedNpcs, object reflectedNpcs, int count, NpcLabel label, NpcLabelSnapshot snapshot)
        {
            if (label == null || string.IsNullOrWhiteSpace(label.HealthText))
            {
                return true;
            }

            var healthSourceIndex = label.HealthSourceIndex >= 0 ? label.HealthSourceIndex : label.Index;
            int life;
            int lifeMax;
            if (!TryReadNpcHealthByIndex(typedNpcs, reflectedNpcs, count, healthSourceIndex, out life, out lifeMax))
            {
                life = snapshot.Life;
                lifeMax = snapshot.LifeMax;
            }

            return CanReuseHealthValues(label, life, lifeMax);
        }

        private static bool CanReuseHealthValues(NpcLabel label, int life, int lifeMax)
        {
            return label != null &&
                   string.Equals(label.HealthText, BuildEnemyHealthText(life, lifeMax), StringComparison.Ordinal) &&
                   label.HealthLife == life &&
                   label.HealthLifeMax == lifeMax;
        }

        private static bool IsNpcNameLabelCandidate(NpcLabelSnapshot snapshot)
        {
            return snapshot.TownNpc || IsSkeletonMerchant(snapshot.Type);
        }

        private static bool IsEnemyNameLabelCandidate(NpcLabelSnapshot snapshot)
        {
            return !IsNpcNameLabelCandidate(snapshot) &&
                   !snapshot.Friendly &&
                   !snapshot.Critter &&
                   snapshot.Life > 0 &&
                   snapshot.LifeMax > 5 &&
                   !IsTargetDummy(snapshot.Type);
        }

        private static int GetLifeEligibilityKey(int life, int lifeMax)
        {
            if (life <= 0)
            {
                return 0;
            }

            return lifeMax > 5 ? 2 : 1;
        }

        private static int ResolveEnemyHealthSourceIndex(int index, NpcLabelSnapshot snapshot, NpcSegmentInfo segmentInfo, int count)
        {
            if (segmentInfo != null && segmentInfo.GroupKey >= 0 && segmentInfo.GroupKey < count)
            {
                return segmentInfo.GroupKey;
            }

            if (snapshot.WhoAmI >= 0 && snapshot.WhoAmI < count)
            {
                return snapshot.WhoAmI;
            }

            return index;
        }

        private static bool TryReadNpcHealthByIndex(NPC[] typedNpcs, object reflectedNpcs, int count, int index, out int life, out int lifeMax)
        {
            // Segment health reads are label text only; failed reads keep the
            // snapshot's own life instead of inventing a group total.
            life = 0;
            lifeMax = 0;
            if (index < 0 || index >= count)
            {
                return false;
            }

            if (typedNpcs != null)
            {
                if (index >= typedNpcs.Length)
                {
                    return false;
                }

                var npc = typedNpcs[index];
                if (!TerrariaNpcReadCompat.IsActive(npc))
                {
                    return false;
                }

                life = TerrariaNpcReadCompat.Life(npc);
                lifeMax = TerrariaNpcReadCompat.LifeMax(npc);
                return true;
            }

            var reflectedNpc = InformationReflection.GetIndexedValue(reflectedNpcs, index);
            if (reflectedNpc == null || !IsNpcActive(reflectedNpc))
            {
                return false;
            }

            var hasLife = InformationReflection.TryReadInt(reflectedNpc, "life", out life);
            var hasLifeMax = InformationReflection.TryReadInt(reflectedNpc, "lifeMax", out lifeMax);
            return hasLife && hasLifeMax;
        }

        private static string BuildEnemyHealthText(int life, int lifeMax)
        {
            if (lifeMax <= 0)
            {
                return string.Empty;
            }

            return Math.Max(0, life).ToString(CultureInfo.InvariantCulture) +
                   "/" +
                   Math.Max(0, lifeMax).ToString(CultureInfo.InvariantCulture);
        }

        private static float ResolveEnemyHealthFontScale(float nameFontScale)
        {
            if (float.IsNaN(nameFontScale) || float.IsInfinity(nameFontScale) || nameFontScale <= 0.05f)
            {
                nameFontScale = 0.70f;
            }

            var value = Math.Round(nameFontScale - EnemyHealthFontScaleDelta, 2);
            if (value < InformationStyleHelper.MinFontScale)
            {
                value = InformationStyleHelper.MinFontScale;
            }

            if (value > InformationStyleHelper.MaxFontScale)
            {
                value = InformationStyleHelper.MaxFontScale;
            }

            return (float)value;
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

        private static bool TryReadSnapshot(object npc, int fallbackIndex, out NpcLabelSnapshot snapshot)
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
                if (!TryReadLabelAnchor(typedNpc, out worldX, out worldY))
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
            if (!TryReadLabelAnchor(npc, out fallbackWorldX, out fallbackWorldY))
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

        private static bool IsSkeletonMerchant(int npcType)
        {
            return npcType == ReadSkeletonMerchantNpcType();
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

        private static int ReadSkeletonMerchantNpcType()
        {
            if (_skeletonMerchantNpcTypeResolved)
            {
                return _skeletonMerchantNpcType;
            }

            _skeletonMerchantNpcType = ReadNpcId("SkeletonMerchant", 453);
            _skeletonMerchantNpcTypeResolved = true;
            return _skeletonMerchantNpcType;
        }

        private static int ReadNpcId(string name, int fallback)
        {
            var npcIdType = InformationReflection.FindType("Terraria.ID.NPCID");
            int value;
            return TryReadStaticInt(npcIdType, name, out value) ? value : fallback;
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

        private static bool TryReadLabelAnchor(NPC npc, out float worldX, out float worldY)
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

        private static bool TryReadLabelAnchor(object npc, out float worldX, out float worldY)
        {
            var typedNpc = npc as NPC;
            if (typedNpc != null)
            {
                return TryReadLabelAnchor(typedNpc, out worldX, out worldY);
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

        private static int GetCollectionCount(object source)
        {
            if (source == null)
            {
                return 0;
            }

            var array = source as Array;
            if (array != null)
            {
                return array.Length;
            }

            var collection = source as System.Collections.ICollection;
            if (collection != null)
            {
                return collection.Count;
            }

            int length;
            if (InformationReflection.TryReadInt(source, "Length", out length) ||
                InformationReflection.TryReadInt(source, "Count", out length))
            {
                return Math.Max(0, length);
            }

            return 0;
        }

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
            if (raw == null)
            {
                return false;
            }

            if (raw is bool)
            {
                value = (bool)raw;
                return true;
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

        private static string NormalizeNpcMode(string mode)
        {
            if (string.Equals(mode, "Name", StringComparison.OrdinalIgnoreCase))
            {
                return "Name";
            }

            return string.Equals(mode, "Type", StringComparison.OrdinalIgnoreCase) ? "Type" : "Off";
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
    }
}
