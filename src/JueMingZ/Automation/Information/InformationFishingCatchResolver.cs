using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using JueMingZ.Automation.Fishing.Filtering;

namespace JueMingZ.Automation.Information
{
    internal static class InformationFishingCatchResolver
    {
        private const int TileSize = 16;
        private const int MinimumWaterTilesForFishingRules = 75;
        private const int MaxCatchItems = 96;
        private const int CatchCacheLimit = 16;
        private static FieldInfo _fishRulesField;
        private static bool _crateSetsInitialized;
        private static object _isFishingCrateSet;
        private static object _isFishingCrateHardmodeSet;
        private static readonly Dictionary<Type, MethodInfo> MeetsConditionsMethods = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<int, string> ItemInternalNameCache = new Dictionary<int, string>();
        private static readonly object CatchCacheSyncRoot = new object();
        private static readonly Dictionary<FishingCatchQueryKey, FishingCatchCacheEntry> CatchCache =
            new Dictionary<FishingCatchQueryKey, FishingCatchCacheEntry>();
        private static readonly Queue<FishingCatchQueryKey> CatchCacheOrder = new Queue<FishingCatchQueryKey>();
        private static readonly string[] QueryPlayerZoneFields =
        {
            "ZoneCorrupt",
            "ZoneCrimson",
            "ZoneJungle",
            "ZoneSnow",
            "ZoneDesert",
            "ZoneUndergroundDesert",
            "ZoneBeach",
            "ZoneDungeon",
            "ZoneHallow",
            "ZoneMeteor",
            "ZoneGlowshroom",
            "ZoneUnderworldHeight",
            "ZoneOverworldHeight",
            "ZoneSkyHeight",
            "ZoneDirtLayerHeight",
            "ZoneRockLayerHeight",
            "ZoneRain"
        };
        private static readonly string[] QueryWorldBoolFields =
        {
            "hardMode",
            "expertMode",
            "masterMode",
            "dayTime",
            "bloodMoon",
            "eclipse",
            "pumpkinMoon",
            "snowMoon",
            "raining",
            "remixWorld",
            "notTheBeesWorld",
            "drunkWorld",
            "getGoodWorld",
            "tenthAnniversaryWorld",
            "dontStarveWorld",
            "zenithWorld",
            "xMas",
            "halloween",
            "slimeRain"
        };

        public static IList<string> ResolveCatchNames(InformationWorldContext context, float bobberWorldX, float bobberWorldY, out string message)
        {
            var names = new List<string>();
            var candidates = ResolveCatchCandidates(context, bobberWorldX, bobberWorldY, out message);
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

        public static IList<FishingCatchCandidate> ResolveCatchCandidates(InformationWorldContext context, float bobberWorldX, float bobberWorldY, out string message)
        {
            return ResolveCatchCandidates(context, bobberWorldX, bobberWorldY, string.Empty, out message);
        }

        public static IList<FishingCatchCandidate> ResolveCatchCandidates(InformationWorldContext context, float bobberWorldX, float bobberWorldY, string filterSignature, out string message)
        {
            message = string.Empty;
            var candidates = new List<FishingCatchCandidate>();
            if (context == null || context.LocalPlayer == null || context.MainType == null)
            {
                message = "环境不可用";
                return candidates;
            }

            IList rules;
            if (!TryGetFishDropRules(context, out rules) || rules == null || rules.Count <= 0)
            {
                message = "鱼获规则不可用";
                return candidates;
            }

            var tileX = (int)Math.Floor(bobberWorldX / TileSize);
            var tileY = (int)Math.Floor(bobberWorldY / TileSize);
            var water = ScanFishingWater(context, tileX, tileY);
            if (water.TotalTiles < MinimumWaterTilesForFishingRules)
            {
                message = "水体不足";
                return candidates;
            }

            var fishingConditions = EnsureFishingConditionsForDisplay(context, InvokeInstance(context.LocalPlayer, "GetFishingConditions", null));
            var finalFishingLevel = ReadInt(fishingConditions, "FinalFishingLevel", 0);
            if (finalFishingLevel <= 0)
            {
                message = "暂无可解析鱼获";
                return candidates;
            }

            var baitItemType = ReadInt(fishingConditions, "BaitItemType", 0);
            var baitPower = ReadInt(fishingConditions, "BaitPower", 0);
            var poleItemType = ReadInt(fishingConditions, "PoleItemType", 0);
            var polePower = ReadInt(fishingConditions, "PolePower", 0);
            if (baitItemType == 2673)
            {
                message = "松露虫不列入普通鱼获";
                return candidates;
            }

            var fishingLevel = ApplyFishingWaterPenalty(context, bobberWorldY, water, finalFishingLevel, out var waterNeeded, out var junkPossible);
            var questFish = ReadQuestFishItem(context);
            var canFishInLava = CanFishInLava(context, fishingConditions);
            var queryKey = BuildCatchQueryKey(
                context,
                tileX,
                tileY,
                ResolveLiquidKind(water),
                water.TotalTiles,
                water.Chums,
                waterNeeded,
                junkPossible,
                finalFishingLevel,
                fishingLevel,
                polePower,
                poleItemType,
                baitPower,
                baitItemType,
                canFishInLava,
                questFish,
                filterSignature);
            FishingCatchCacheEntry cached;
            if (TryGetCatchCache(queryKey, out cached))
            {
                message = cached.Message;
                return cached.Candidates;
            }

            var heightLevels = BuildHeightLevels(context, tileY);
            var catchIds = new List<int>();
            var seen = new HashSet<int>();

            for (var heightIndex = 0; heightIndex < heightLevels.Count && catchIds.Count < MaxCatchItems; heightIndex++)
            {
                var heightLevel = heightLevels[heightIndex];
                var corruptionRolls = BuildCorruptionRolls(context, heightLevel);
                var honeyRolls = BuildHoneyRolls(context, water.InHoney);
                var snowRolls = BuildBooleanRolls(HasZone(context.LocalPlayer, "ZoneSnow"));
                var desertRolls = BuildBooleanRolls(HasZone(context.LocalPlayer, "ZoneDesert") || HasZone(context.LocalPlayer, "ZoneUndergroundDesert"));
                var infectedDesertRolls = BuildBooleanRolls((HasZone(context.LocalPlayer, "ZoneDesert") || HasZone(context.LocalPlayer, "ZoneUndergroundDesert")) &&
                                                            (HasZone(context.LocalPlayer, "ZoneCorrupt") || HasZone(context.LocalPlayer, "ZoneCrimson")));
                var crateRolls = new[] { false, true };
                var junkRolls = junkPossible ? new[] { false, true } : new[] { false };

                for (var corruptionIndex = 0; corruptionIndex < corruptionRolls.Count && catchIds.Count < MaxCatchItems; corruptionIndex++)
                {
                    var corruption = corruptionRolls[corruptionIndex];
                    for (var honeyIndex = 0; honeyIndex < honeyRolls.Length && catchIds.Count < MaxCatchItems; honeyIndex++)
                    {
                        for (var snowIndex = 0; snowIndex < snowRolls.Length && catchIds.Count < MaxCatchItems; snowIndex++)
                        {
                            for (var desertIndex = 0; desertIndex < desertRolls.Length && catchIds.Count < MaxCatchItems; desertIndex++)
                            {
                                for (var infectedDesertIndex = 0; infectedDesertIndex < infectedDesertRolls.Length && catchIds.Count < MaxCatchItems; infectedDesertIndex++)
                                {
                                    for (var crateIndex = 0; crateIndex < crateRolls.Length && catchIds.Count < MaxCatchItems; crateIndex++)
                                    {
                                        for (var junkIndex = 0; junkIndex < junkRolls.Length && catchIds.Count < MaxCatchItems; junkIndex++)
                                        {
                                            var fishingContext = CreateFishingContext(
                                                context,
                                                fishingConditions,
                                                tileX,
                                                tileY,
                                                water.InLava,
                                                honeyRolls[honeyIndex],
                                                water.TotalTiles,
                                                waterNeeded,
                                                water.Chums,
                                                fishingLevel,
                                                canFishInLava,
                                                questFish,
                                                heightLevel,
                                                corruption.Corrupt,
                                                corruption.Crimson,
                                                HasZone(context.LocalPlayer, "ZoneJungle"),
                                                snowRolls[snowIndex],
                                                desertRolls[desertIndex],
                                                infectedDesertRolls[infectedDesertIndex],
                                                false,
                                                crateRolls[crateIndex],
                                                junkRolls[junkIndex]);

                                            if (fishingContext == null)
                                            {
                                                continue;
                                            }

                                            AddMatchingRuleItems(rules, fishingContext, catchIds, seen);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            for (var index = 0; index < catchIds.Count; index++)
            {
                var itemId = catchIds[index];
                var name = ResolveItemName(itemId);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    candidates.Add(new FishingCatchCandidate
                    {
                        Kind = FishingCatchKinds.Item,
                        Id = itemId,
                        DisplayName = name,
                        DisplayNameSnapshot = name,
                        IsCrate = IsFishingCrateItem(itemId),
                        IsQuestFish = itemId == questFish,
                        IsEnemy = false
                    });
                }
            }

            candidates.Sort(CompareCandidates);

            if (candidates.Count <= 0)
            {
                message = "暂无可解析鱼获";
            }

            StoreCatchCache(queryKey, candidates, message);
            return candidates;
        }

        public static IList<FishingCatchCandidate> ResolveGlobalFishableItemCandidates(
            InformationWorldContext context,
            string query,
            int maxResults,
            out bool truncated,
            out string message)
        {
            truncated = false;
            message = string.Empty;
            var candidates = new List<FishingCatchCandidate>();
            var searchText = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            if (searchText.Length <= 0)
            {
                message = "请输入名称或 ID 搜索全游戏可钓物品";
                return candidates;
            }

            if (context == null || context.MainType == null)
            {
                message = "环境不可用";
                return candidates;
            }

            IList rules;
            if (!TryGetFishDropRules(context, out rules) || rules == null || rules.Count <= 0)
            {
                message = "鱼获规则不可用";
                return candidates;
            }

            var itemIds = new List<int>();
            var seen = new HashSet<int>();
            for (var index = 0; index < rules.Count; index++)
            {
                AddPossibleItemsUnbounded(rules[index], itemIds, seen);
            }

            var questFishIds = ReadAnglerQuestFishIds(context);
            foreach (var questFishId in questFishIds)
            {
                AddItemIdUnbounded(questFishId, itemIds, seen);
            }

            if (itemIds.Count <= 0)
            {
                message = "暂无全局可钓物品索引";
                return candidates;
            }

            int searchItemId;
            var hasItemIdSearch = TryParseSearchItemId(searchText, out searchItemId);
            for (var index = 0; index < itemIds.Count; index++)
            {
                var itemId = itemIds[index];
                if (itemId <= 0)
                {
                    continue;
                }

                var displayName = ResolveItemName(itemId);
                var internalName = ResolveInternalItemName(itemId);
                if (!MatchesGlobalSearch(itemId, displayName, internalName, searchText, hasItemIdSearch, searchItemId))
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(displayName)
                    ? "#" + itemId.ToString(CultureInfo.InvariantCulture)
                    : displayName.Trim();
                candidates.Add(new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = itemId,
                    DisplayName = name,
                    DisplayNameSnapshot = name,
                    IsCrate = IsFishingCrateItem(itemId),
                    IsQuestFish = questFishIds.Contains(itemId),
                    IsEnemy = false
                });
            }

            candidates.Sort(CompareCandidates);
            if (maxResults > 0 && candidates.Count > maxResults)
            {
                truncated = true;
                candidates.RemoveRange(maxResults, candidates.Count - maxResults);
            }

            if (candidates.Count <= 0)
            {
                message = "无匹配物品";
            }
            else if (truncated)
            {
                message = "结果较多，请继续输入缩小范围";
            }

            return candidates;
        }

        private static int CompareCandidates(FishingCatchCandidate left, FishingCatchCandidate right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var displayName = StringComparer.Ordinal.Compare(left.DisplayName ?? string.Empty, right.DisplayName ?? string.Empty);
            if (displayName != 0)
            {
                return displayName;
            }

            var id = left.Id.CompareTo(right.Id);
            if (id != 0)
            {
                return id;
            }

            return StringComparer.Ordinal.Compare(left.Kind ?? string.Empty, right.Kind ?? string.Empty);
        }

        internal static void ResetCatchCacheForTesting()
        {
            lock (CatchCacheSyncRoot)
            {
                CatchCache.Clear();
                CatchCacheOrder.Clear();
            }
        }

        internal static string BuildCatchQuerySignatureForTesting(
            InformationWorldContext context,
            int tileX,
            int tileY,
            string liquidKind,
            int waterTiles,
            int finalFishingLevel,
            int poleItemType,
            int baitItemType,
            int questFish,
            string filterSignature)
        {
            return BuildCatchQueryKey(
                context,
                tileX,
                tileY,
                liquidKind,
                waterTiles,
                0,
                300,
                false,
                finalFishingLevel,
                finalFishingLevel,
                0,
                poleItemType,
                0,
                baitItemType,
                false,
                questFish,
                filterSignature).Signature;
        }

        private static bool TryGetCatchCache(FishingCatchQueryKey key, out FishingCatchCacheEntry entry)
        {
            lock (CatchCacheSyncRoot)
            {
                return CatchCache.TryGetValue(key, out entry) && entry != null;
            }
        }

        private static void StoreCatchCache(FishingCatchQueryKey key, IList<FishingCatchCandidate> candidates, string message)
        {
            lock (CatchCacheSyncRoot)
            {
                if (!CatchCache.ContainsKey(key))
                {
                    CatchCacheOrder.Enqueue(key);
                }

                CatchCache[key] = new FishingCatchCacheEntry
                {
                    Candidates = candidates ?? new List<FishingCatchCandidate>(),
                    Message = message ?? string.Empty
                };

                while (CatchCacheOrder.Count > CatchCacheLimit)
                {
                    var oldest = CatchCacheOrder.Dequeue();
                    CatchCache.Remove(oldest);
                }
            }
        }

        private static FishingCatchQueryKey BuildCatchQueryKey(
            InformationWorldContext context,
            int tileX,
            int tileY,
            string liquidKind,
            int waterTiles,
            int chums,
            int waterNeeded,
            bool junkPossible,
            int finalFishingLevel,
            int fishingLevel,
            int polePower,
            int poleItemType,
            int baitPower,
            int baitItemType,
            bool canFishInLava,
            int questFish,
            string filterSignature)
        {
            var builder = new StringBuilder(512);
            AppendKeyPart(builder, "world", context == null ? string.Empty : context.WorldKey);
            AppendKeyPart(builder, "tile", tileX.ToString(CultureInfo.InvariantCulture) + "," + tileY.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "liquid", liquidKind);
            AppendKeyPart(builder, "water", waterTiles.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "chums", chums.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "waterNeeded", waterNeeded.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "junk", junkPossible ? "1" : "0");
            AppendKeyPart(builder, "final", finalFishingLevel.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "level", fishingLevel.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "pole", polePower.ToString(CultureInfo.InvariantCulture) + ":" + poleItemType.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "bait", baitPower.ToString(CultureInfo.InvariantCulture) + ":" + baitItemType.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "lava", canFishInLava ? "1" : "0");
            AppendKeyPart(builder, "quest", questFish.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "filter", filterSignature);
            AppendKeyPart(builder, "player", BuildPlayerEnvironmentSignature(context));
            AppendKeyPart(builder, "worldState", BuildWorldStateSignature(context));
            AppendKeyPart(builder, "language", BuildLanguageSignature());
            return new FishingCatchQueryKey(builder.ToString());
        }

        private static string ResolveLiquidKind(FishingWaterScan water)
        {
            if (water != null && water.InLava)
            {
                return "lava";
            }

            if (water != null && water.InHoney)
            {
                return "honey";
            }

            return "water";
        }

        private static string BuildPlayerEnvironmentSignature(InformationWorldContext context)
        {
            var player = context == null ? null : context.LocalPlayer;
            var builder = new StringBuilder(192);
            double luck;
            AppendKeyPart(builder, "luck", TryReadNumber(player, "luck", out luck) ? luck.ToString("0.###", CultureInfo.InvariantCulture) : "unknown");
            AppendKeyPart(builder, "fishSkill", ReadInt(player, "fishingSkill", 0).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "accLavaFishing", ReadBool(player, "accLavaFishing", false) ? "1" : "0");
            AppendKeyPart(builder, "heightTile", context == null ? "0" : ((int)Math.Floor(context.PlayerCenterY / TileSize)).ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < QueryPlayerZoneFields.Length; index++)
            {
                AppendKeyPart(builder, QueryPlayerZoneFields[index], HasZone(player, QueryPlayerZoneFields[index]) ? "1" : "0");
            }

            return builder.ToString();
        }

        private static string BuildWorldStateSignature(InformationWorldContext context)
        {
            var mainType = context == null ? null : context.MainType;
            var builder = new StringBuilder(256);
            for (var index = 0; index < QueryWorldBoolFields.Length; index++)
            {
                AppendKeyPart(builder, QueryWorldBoolFields[index], ReadStaticBool(mainType, QueryWorldBoolFields[index], false) ? "1" : "0");
            }

            AppendKeyPart(builder, "moonPhase", ReadStaticInt(mainType, "moonPhase", -1).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "maxTilesX", ReadStaticInt(mainType, "maxTilesX", 0).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "maxTilesY", ReadStaticInt(mainType, "maxTilesY", 0).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "worldSurface", ReadStaticDouble(mainType, "worldSurface", 0d).ToString("0.###", CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "rockLayer", ReadStaticDouble(mainType, "rockLayer", 0d).ToString("0.###", CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "timeBucket", ((int)(ReadStaticDouble(mainType, "time", 0d) / 3600d)).ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string BuildLanguageSignature()
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

        private static void AppendKeyPart(StringBuilder builder, string name, string value)
        {
            if (builder == null)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            builder.Append(name ?? string.Empty);
            builder.Append('=');
            builder.Append(value ?? string.Empty);
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
                    return values[index].Trim();
                }
            }

            return string.Empty;
        }

        private static bool TryGetFishDropRules(InformationWorldContext context, out IList rules)
        {
            rules = null;
            var fishDropDb = InformationReflection.GetStaticMember(context.MainType, "FishDropsDB");
            if (fishDropDb == null)
            {
                return false;
            }

            try
            {
                if (_fishRulesField != null)
                {
                    rules = _fishRulesField.GetValue(fishDropDb) as IList;
                    if (rules != null)
                    {
                        return true;
                    }
                }

                var fields = fishDropDb.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (var index = 0; index < fields.Length; index++)
                {
                    var value = fields[index].GetValue(fishDropDb) as IList;
                    if (!LooksLikeFishRuleList(value))
                    {
                        continue;
                    }

                    _fishRulesField = fields[index];
                    rules = value;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static object EnsureFishingConditionsForDisplay(InformationWorldContext context, object fishingConditions)
        {
            var conditions = fishingConditions ?? CreatePlayerFishingConditions();
            if (conditions == null)
            {
                return fishingConditions;
            }

            var polePower = ReadInt(conditions, "PolePower", 0);
            var poleItemType = ReadInt(conditions, "PoleItemType", 0);
            var baitPower = ReadInt(conditions, "BaitPower", 0);
            var baitItemType = ReadInt(conditions, "BaitItemType", 0);

            FillDisplayPoleAndBait(context, ref polePower, ref poleItemType, ref baitPower, ref baitItemType);

            var finalFishingLevel = ReadInt(conditions, "FinalFishingLevel", 0);
            if (finalFishingLevel <= 0)
            {
                var baseFishingLevel = polePower + baitPower + ReadInt(context.LocalPlayer, "fishingSkill", 0);
                if (baseFishingLevel <= 0 && poleItemType > 0)
                {
                    baseFishingLevel = Math.Max(1, polePower);
                }

                if (baseFishingLevel > 0)
                {
                    var multiplier = ReadFloat(conditions, "LevelMultipliers", 0f);
                    if (multiplier <= 0f)
                    {
                        multiplier = ReadFishingPowerMultiplier(context);
                    }

                    if (multiplier <= 0f)
                    {
                        multiplier = 1f;
                    }

                    finalFishingLevel = Math.Max(1, (int)(baseFishingLevel * multiplier));
                }
            }

            SetMember(conditions, "PolePower", polePower);
            SetMember(conditions, "PoleItemType", poleItemType);
            SetMember(conditions, "BaitPower", baitPower);
            SetMember(conditions, "BaitItemType", baitItemType);
            if (ReadFloat(conditions, "LevelMultipliers", 0f) <= 0f)
            {
                SetMember(conditions, "LevelMultipliers", Math.Max(1f, ReadFishingPowerMultiplier(context)));
            }

            SetMember(conditions, "FinalFishingLevel", finalFishingLevel);
            return conditions;
        }

        private static object CreatePlayerFishingConditions()
        {
            var conditionsType = InformationReflection.FindType("Terraria.DataStructures.PlayerFishingConditions");
            if (conditionsType == null)
            {
                return null;
            }

            try
            {
                return Activator.CreateInstance(conditionsType);
            }
            catch
            {
                return null;
            }
        }

        private static void FillDisplayPoleAndBait(InformationWorldContext context, ref int polePower, ref int poleItemType, ref int baitPower, ref int baitItemType)
        {
            if (context == null || context.LocalPlayer == null)
            {
                return;
            }

            var inventory = InformationReflection.GetMember(context.LocalPlayer, "inventory");
            var selectedItemIndex = ReadInt(context.LocalPlayer, "selectedItem", -1);
            var selectedItem = InformationReflection.GetIndexedValue(inventory, selectedItemIndex);
            ConsiderFishingPole(selectedItem, ref polePower, ref poleItemType);

            var mouseItem = context.MainType == null ? null : InformationReflection.GetStaticMember(context.MainType, "mouseItem");
            ConsiderFishingPole(mouseItem, ref polePower, ref poleItemType);
            ConsiderBait(mouseItem, ref baitPower, ref baitItemType);

            var count = GetCollectionCount(inventory);
            for (var slot = 0; slot < count && slot < 58; slot++)
            {
                ConsiderFishingPole(InformationReflection.GetIndexedValue(inventory, slot), ref polePower, ref poleItemType);
            }

            if (baitPower <= 0 && baitItemType <= 0)
            {
                for (var slot = 54; slot < count && slot < 58; slot++)
                {
                    if (ConsiderBait(InformationReflection.GetIndexedValue(inventory, slot), ref baitPower, ref baitItemType))
                    {
                        return;
                    }
                }

                for (var slot = 0; slot < count && slot < 50; slot++)
                {
                    if (ConsiderBait(InformationReflection.GetIndexedValue(inventory, slot), ref baitPower, ref baitItemType))
                    {
                        return;
                    }
                }
            }
        }

        private static void ConsiderFishingPole(object item, ref int polePower, ref int poleItemType)
        {
            if (item == null)
            {
                return;
            }

            var power = ReadInt(item, "fishingPole", 0);
            if (power <= polePower)
            {
                return;
            }

            polePower = power;
            poleItemType = ReadInt(item, "type", 0);
        }

        private static bool ConsiderBait(object item, ref int baitPower, ref int baitItemType)
        {
            if (item == null || ReadInt(item, "stack", 0) <= 0)
            {
                return false;
            }

            var power = ReadInt(item, "bait", 0);
            if (power <= 0)
            {
                return false;
            }

            baitPower = power;
            baitItemType = ReadInt(item, "type", 0);
            return true;
        }

        private static float ReadFishingPowerMultiplier(InformationWorldContext context)
        {
            var playerType = context == null || context.LocalPlayer == null ? null : context.LocalPlayer.GetType();
            object raw;
            if (InformationReflection.TryInvokeStatic(playerType, "Fishing_GetPowerMultiplier", null, out raw))
            {
                return ToFloat(raw, 1f);
            }

            return 1f;
        }

        private static bool LooksLikeFishRuleList(IList value)
        {
            if (value == null)
            {
                return false;
            }

            for (var index = 0; index < value.Count && index < 8; index++)
            {
                var item = value[index];
                if (item == null)
                {
                    continue;
                }

                return item.GetType().FullName.IndexOf("FishDropRule", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       InformationReflection.GetMember(item, "PossibleItems") != null;
            }

            return value.GetType().FullName.IndexOf("FishDropRule", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static FishingWaterScan ScanFishingWater(InformationWorldContext context, int tileX, int tileY)
        {
            var result = new FishingWaterScan();
            result.Chums = 0;
            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                return result;
            }

            var minX = tileX;
            var maxX = tileX;
            while (minX > 10 && IsLiquidOpenTile(context, tiles, minX, tileY))
            {
                minX--;
            }

            while (maxX < ReadStaticInt(context.MainType, "maxTilesX", tileX + 10) - 10 &&
                   IsLiquidOpenTile(context, tiles, maxX, tileY))
            {
                maxX++;
            }

            var maxY = ReadStaticInt(context.MainType, "maxTilesY", tileY + 10);
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = tileY; y < maxY - 10 && IsLiquidOpenTile(context, tiles, x, y); y++)
                {
                    var tile = InformationTileAccess.GetTileAt(tiles, x, y);
                    var liquidType = ReadLiquidType(tile);
                    result.TotalTiles++;
                    if (liquidType == 1)
                    {
                        result.InLava = true;
                    }
                    else if (liquidType == 2)
                    {
                        result.InHoney = true;
                    }
                }
            }

            if (result.InHoney)
            {
                result.TotalTiles = (int)(result.TotalTiles * 1.5d);
            }

            return result;
        }

        private static bool IsLiquidOpenTile(InformationWorldContext context, object tiles, int x, int y)
        {
            var tile = InformationTileAccess.GetTileAt(tiles, x, y);
            return tile != null && ReadLiquidAmount(tile) > 0 && !IsSolidTile(context, x, y);
        }

        private static bool IsSolidTile(InformationWorldContext context, int x, int y)
        {
            object raw;
            var worldGenType = InformationReflection.FindType("Terraria.WorldGen");
            if (InformationReflection.TryInvokeStatic(worldGenType, "SolidTile", new object[] { x, y, false }, out raw))
            {
                bool value;
                if (TryConvertBool(raw, out value))
                {
                    return value;
                }
            }

            return false;
        }

        private static int ApplyFishingWaterPenalty(InformationWorldContext context, float bobberWorldY, FishingWaterScan water, int finalFishingLevel, out int waterNeeded, out bool junkPossible)
        {
            waterNeeded = 300;
            var maxTilesX = ReadStaticInt(context.MainType, "maxTilesX", 4200);
            var worldSurface = ReadStaticDouble(context.MainType, "worldSurface", 1200d);
            var worldScale = maxTilesX / 4200f;
            worldScale *= worldScale;
            var multiplier = ((bobberWorldY / TileSize) - (60f + 10f * worldScale)) / Math.Max(1f, (float)(worldSurface / 6d));
            if (multiplier < 0.25f)
            {
                multiplier = 0.25f;
            }

            if (multiplier > 1f)
            {
                multiplier = 1f;
            }

            waterNeeded = (int)(waterNeeded * multiplier);
            var fishingLevel = finalFishingLevel;
            if (water.Chums > 0)
            {
                fishingLevel += 11;
            }

            if (water.Chums > 1)
            {
                fishingLevel += 6;
            }

            if (water.Chums > 2)
            {
                fishingLevel += 3;
            }

            if (water.TotalTiles < waterNeeded)
            {
                var ratio = water.TotalTiles / Math.Max(1f, waterNeeded);
                if (ratio < 1f)
                {
                    fishingLevel = (int)(fishingLevel * ratio);
                }
            }

            var luckLevel = fishingLevel;
            double luck;
            if (TryReadNumber(context.LocalPlayer, "luck", out luck) && luck < 0d)
            {
                luckLevel = Math.Min(luckLevel, (int)(fishingLevel * 0.6f));
            }

            junkPossible = water.TotalTiles < waterNeeded && luckLevel < 49;
            return fishingLevel;
        }

        private static List<int> BuildHeightLevels(InformationWorldContext context, int tileY)
        {
            var result = new List<int>();
            var worldSurface = ReadStaticDouble(context.MainType, "worldSurface", 1200d);
            var rockLayer = ReadStaticDouble(context.MainType, "rockLayer", worldSurface + 200d);
            var maxTilesY = ReadStaticInt(context.MainType, "maxTilesY", 1800);
            var remix = ReadStaticBool(context.MainType, "remixWorld", false);
            int level;
            if (remix)
            {
                level = tileY < worldSurface * 0.5d
                    ? 0
                    : (tileY < worldSurface ? 1 : (tileY < rockLayer ? 3 : (tileY >= maxTilesY - 300 ? 4 : 2)));
                result.Add(level);
                if (level == 2)
                {
                    result.Add(1);
                }
            }
            else
            {
                level = tileY < worldSurface * 0.5d
                    ? 0
                    : (tileY < worldSurface ? 1 : (tileY < rockLayer ? 2 : (tileY >= maxTilesY - 300 ? 4 : 3)));
                result.Add(level);
            }

            return result;
        }

        private static IList<CorruptionRoll> BuildCorruptionRolls(InformationWorldContext context, int heightLevel)
        {
            var result = new List<CorruptionRoll>();
            if (ReadStaticBool(context.MainType, "remixWorld", false) && heightLevel == 0)
            {
                result.Add(new CorruptionRoll(false, false));
                return result;
            }

            var corrupt = HasZone(context.LocalPlayer, "ZoneCorrupt");
            var crimson = HasZone(context.LocalPlayer, "ZoneCrimson");
            if (corrupt && crimson)
            {
                result.Add(new CorruptionRoll(true, false));
                result.Add(new CorruptionRoll(false, true));
            }
            else
            {
                result.Add(new CorruptionRoll(corrupt, crimson));
            }

            return result;
        }

        private static bool[] BuildHoneyRolls(InformationWorldContext context, bool inHoney)
        {
            if (inHoney && ReadStaticBool(context.MainType, "notTheBeesWorld", false))
            {
                return new[] { true, false };
            }

            return new[] { inHoney };
        }

        private static bool[] BuildBooleanRolls(bool enabled)
        {
            return enabled ? new[] { false, true } : new[] { false };
        }

        private static object CreateFishingContext(
            InformationWorldContext context,
            object fishingConditions,
            int tileX,
            int tileY,
            bool inLava,
            bool inHoney,
            int waterTilesCount,
            int waterNeeded,
            int chums,
            int fishingLevel,
            bool canFishInLava,
            int questFish,
            int heightLevel,
            bool rolledCorruption,
            bool rolledCrimson,
            bool rolledJungle,
            bool rolledSnow,
            bool rolledDesert,
            bool rolledInfectedDesert,
            bool rolledRemixOcean,
            bool crate,
            bool junk)
        {
            var attemptType = InformationReflection.FindType("Terraria.DataStructures.FishingAttempt");
            var contextType = InformationReflection.FindType("Terraria.GameContent.FishDropRules.FishingContext");
            if (attemptType == null || contextType == null)
            {
                return null;
            }

            try
            {
                var attempt = Activator.CreateInstance(attemptType);
                SetMember(attempt, "playerFishingConditions", fishingConditions);
                SetMember(attempt, "X", tileX);
                SetMember(attempt, "Y", tileY);
                SetMember(attempt, "bobberType", 0);
                SetMember(attempt, "common", false);
                SetMember(attempt, "uncommon", false);
                SetMember(attempt, "rare", false);
                SetMember(attempt, "veryrare", false);
                SetMember(attempt, "legendary", false);
                SetMember(attempt, "crate", crate);
                SetMember(attempt, "junk", junk);
                SetMember(attempt, "inLava", inLava);
                SetMember(attempt, "inHoney", inHoney);
                SetMember(attempt, "waterTilesCount", waterTilesCount);
                SetMember(attempt, "waterNeededToFish", waterNeeded);
                SetMember(attempt, "waterQuality", 0f);
                SetMember(attempt, "chumsInWater", chums);
                SetMember(attempt, "fishingLevel", fishingLevel);
                SetMember(attempt, "CanFishInLava", canFishInLava);
                SetMember(attempt, "atmo", 0f);
                SetMember(attempt, "questFish", questFish);
                SetMember(attempt, "heightLevel", heightLevel);
                SetMember(attempt, "rolledItemDrop", 0);
                SetMember(attempt, "rolledEnemySpawn", 0);

                var fishContext = Activator.CreateInstance(contextType);
                SetMember(fishContext, "Player", context.LocalPlayer);
                SetMember(fishContext, "Fisher", attempt);
                SetMember(fishContext, "RolledCorruption", rolledCorruption);
                SetMember(fishContext, "RolledCrimson", rolledCrimson);
                SetMember(fishContext, "RolledJungle", rolledJungle);
                SetMember(fishContext, "RolledSnow", rolledSnow);
                SetMember(fishContext, "RolledDesert", rolledDesert);
                SetMember(fishContext, "RolledInfectedDesert", rolledInfectedDesert);
                SetMember(fishContext, "RolledRemixOcean", rolledRemixOcean);
                return fishContext;
            }
            catch
            {
                return null;
            }
        }

        private static void AddMatchingRuleItems(IList rules, object fishingContext, IList<int> itemIds, ISet<int> seen)
        {
            for (var index = 0; index < rules.Count && itemIds.Count < MaxCatchItems; index++)
            {
                var rule = rules[index];
                if (rule == null || !RuleMeetsConditions(rule, fishingContext))
                {
                    continue;
                }

                AddPossibleItems(rule, itemIds, seen);
                bool stopper;
                if (TryConvertBool(InformationReflection.GetMember(rule, "IsStopper"), out stopper) && stopper)
                {
                    break;
                }
            }
        }

        private static bool RuleMeetsConditions(object rule, object fishingContext)
        {
            try
            {
                MethodInfo method;
                var type = rule.GetType();
                if (!MeetsConditionsMethods.TryGetValue(type, out method))
                {
                    method = FindMeetsConditionsMethod(type);
                    MeetsConditionsMethods[type] = method;
                }

                if (method == null)
                {
                    return false;
                }

                var raw = method.Invoke(rule, new[] { fishingContext, (object)true });
                bool value;
                return TryConvertBool(raw, out value) && value;
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo FindMeetsConditionsMethod(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "MeetsConditions", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[1].ParameterType == typeof(bool))
                {
                    return method;
                }
            }

            return null;
        }

        private static void AddPossibleItems(object rule, IList<int> itemIds, ISet<int> seen)
        {
            var possible = InformationReflection.GetMember(rule, "PossibleItems");
            var array = possible as Array;
            if (array != null)
            {
                for (var index = 0; index < array.Length && itemIds.Count < MaxCatchItems; index++)
                {
                    AddItemId(array.GetValue(index), itemIds, seen);
                }

                return;
            }

            var list = possible as IList;
            if (list == null)
            {
                return;
            }

            for (var index = 0; index < list.Count && itemIds.Count < MaxCatchItems; index++)
            {
                AddItemId(list[index], itemIds, seen);
            }
        }

        private static void AddItemId(object raw, IList<int> itemIds, ISet<int> seen)
        {
            var itemId = ToInt(raw, 0);
            if (itemId > 0 && seen.Add(itemId))
            {
                itemIds.Add(itemId);
            }
        }

        private static void AddPossibleItemsUnbounded(object rule, IList<int> itemIds, ISet<int> seen)
        {
            if (rule == null)
            {
                return;
            }

            var possible = InformationReflection.GetMember(rule, "PossibleItems");
            var array = possible as Array;
            if (array != null)
            {
                for (var index = 0; index < array.Length; index++)
                {
                    AddItemIdUnbounded(array.GetValue(index), itemIds, seen);
                }

                return;
            }

            var list = possible as IList;
            if (list == null)
            {
                return;
            }

            for (var index = 0; index < list.Count; index++)
            {
                AddItemIdUnbounded(list[index], itemIds, seen);
            }
        }

        private static void AddItemIdUnbounded(object raw, IList<int> itemIds, ISet<int> seen)
        {
            var itemId = ToInt(raw, 0);
            if (itemId > 0 && seen.Add(itemId))
            {
                itemIds.Add(itemId);
            }
        }

        private static bool CanFishInLava(InformationWorldContext context, object fishingConditions)
        {
            if (ReadBool(context.LocalPlayer, "accLavaFishing", false))
            {
                return true;
            }

            var poleItemType = ReadInt(fishingConditions, "PoleItemType", 0);
            var baitItemType = ReadInt(fishingConditions, "BaitItemType", 0);
            var itemSetsType = InformationReflection.FindType("Terraria.ID.ItemID+Sets");
            return ReadBoolArrayValue(InformationReflection.GetStaticMember(itemSetsType, "CanFishInLava"), poleItemType) ||
                   ReadBoolArrayValue(InformationReflection.GetStaticMember(itemSetsType, "IsLavaBait"), baitItemType);
        }

        private static bool IsFishingCrateItem(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            EnsureFishingCrateSets();
            return ReadBoolArrayValue(_isFishingCrateSet, itemId) ||
                   ReadBoolArrayValue(_isFishingCrateHardmodeSet, itemId);
        }

        private static void EnsureFishingCrateSets()
        {
            if (_crateSetsInitialized)
            {
                return;
            }

            _crateSetsInitialized = true;
            var itemSetsType = InformationReflection.FindType("Terraria.ID.ItemID+Sets");
            _isFishingCrateSet = InformationReflection.GetStaticMember(itemSetsType, "IsFishingCrate");
            _isFishingCrateHardmodeSet = InformationReflection.GetStaticMember(itemSetsType, "IsFishingCrateHardmode");
        }

        private static int ReadQuestFishItem(InformationWorldContext context)
        {
            if (ReadAnglerQuestFinished(context))
            {
                return -1;
            }

            int questIndex;
            InformationReflection.TryReadStaticInt(context.MainType, "anglerQuest", out questIndex);
            var itemIds = InformationReflection.GetStaticMember(context.MainType, "anglerQuestItemNetIDs");
            var itemId = ToInt(InformationReflection.GetIndexedValue(itemIds, questIndex), -1);
            if (itemId <= 0)
            {
                return -1;
            }

            var hasItem = InvokeInstance(context.LocalPlayer, "HasItem", new object[] { itemId });
            bool alreadyHasItem;
            if (TryConvertBool(hasItem, out alreadyHasItem) && alreadyHasItem)
            {
                return -1;
            }

            return itemId;
        }

        private static HashSet<int> ReadAnglerQuestFishIds(InformationWorldContext context)
        {
            var result = new HashSet<int>();
            if (context == null || context.MainType == null)
            {
                return result;
            }

            var itemIds = InformationReflection.GetStaticMember(context.MainType, "anglerQuestItemNetIDs");
            var count = GetCollectionCount(itemIds);
            for (var index = 0; index < count; index++)
            {
                var itemId = ToInt(InformationReflection.GetIndexedValue(itemIds, index), 0);
                if (itemId > 0)
                {
                    result.Add(itemId);
                }
            }

            return result;
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
            var indexed = InformationReflection.GetIndexedValue(raw, myPlayer);
            return TryConvertBool(indexed, out direct) && direct;
        }

        private static string ResolveItemName(int itemId)
        {
            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetItemNameValue", new object[] { itemId }, out raw) && raw != null)
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return itemId.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveInternalItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            string cached;
            lock (ItemInternalNameCache)
            {
                if (ItemInternalNameCache.TryGetValue(itemId, out cached))
                {
                    return cached;
                }
            }

            var value = ResolveInternalItemNameUncached(itemId);
            lock (ItemInternalNameCache)
            {
                ItemInternalNameCache[itemId] = value ?? string.Empty;
            }

            return value ?? string.Empty;
        }

        private static string ResolveInternalItemNameUncached(int itemId)
        {
            var itemIdType = InformationReflection.FindType("Terraria.ID.ItemID");
            if (itemIdType == null)
            {
                return string.Empty;
            }

            var search = InformationReflection.GetStaticMember(itemIdType, "Search");
            var raw = InvokeInstance(search, "GetName", new object[] { itemId });
            var name = raw == null ? string.Empty : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            try
            {
                var fields = itemIdType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (var index = 0; index < fields.Length; index++)
                {
                    var field = fields[index];
                    if (field.FieldType != typeof(int))
                    {
                        continue;
                    }

                    if (ToInt(field.GetValue(null), 0) == itemId)
                    {
                        return field.Name;
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static bool MatchesGlobalSearch(
            int itemId,
            string displayName,
            string internalName,
            string query,
            bool hasItemIdSearch,
            int searchItemId)
        {
            if (hasItemIdSearch && itemId == searchItemId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(displayName) &&
                displayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(internalName) &&
                   internalName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryParseSearchItemId(string query, out int itemId)
        {
            itemId = 0;
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            var text = query.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                text = text.Substring(1).Trim();
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId) &&
                   itemId > 0;
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
            return array == null || array.Rank != 1 ? 0 : array.GetLength(0);
        }

        private static bool HasZone(object player, string member)
        {
            bool value;
            return InformationReflection.TryReadBool(player, member, out value) && value;
        }

        private static int ReadLiquidAmount(object tile)
        {
            return InformationTileAccess.ReadLiquidAmount(tile);
        }

        private static int ReadLiquidType(object tile)
        {
            var type = InformationTileAccess.ReadLiquidType(tile);
            if (type != 0)
            {
                return type;
            }

            if (ReadBool(tile, "lava", false))
            {
                return 1;
            }

            return ReadBool(tile, "honey", false) ? 2 : 0;
        }

        private static object InvokeInstance(object instance, string methodName, object[] args)
        {
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            try
            {
                var methods = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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

                    return method.Invoke(instance, args);
                }
            }
            catch
            {
            }

            return null;
        }

        private static void SetMember(object instance, string name, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            try
            {
                var type = instance.GetType();
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(instance, ConvertForMember(field.FieldType, value));
                    return;
                }

                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, ConvertForMember(property.PropertyType, value), null);
                }
            }
            catch
            {
            }
        }

        private static object ConvertForMember(Type targetType, object value)
        {
            if (targetType == null || value == null)
            {
                return value;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (targetType.IsEnum)
            {
                try
                {
                    return Enum.ToObject(targetType, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                }
                catch
                {
                    return value;
                }
            }

            try
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }

        private static int ReadStaticInt(Type type, string name, int fallback)
        {
            return ToInt(InformationReflection.GetStaticMember(type, name), fallback);
        }

        private static double ReadStaticDouble(Type type, string name, double fallback)
        {
            return ToDouble(InformationReflection.GetStaticMember(type, name), fallback);
        }

        private static bool ReadStaticBool(Type type, string name, bool fallback)
        {
            bool value;
            return TryConvertBool(InformationReflection.GetStaticMember(type, name), out value) ? value : fallback;
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            return ToInt(InformationReflection.GetMember(instance, name), fallback);
        }

        private static float ReadFloat(object instance, string name, float fallback)
        {
            return ToFloat(InformationReflection.GetMember(instance, name), fallback);
        }

        private static bool ReadBool(object instance, string name, bool fallback)
        {
            bool value;
            return TryConvertBool(InformationReflection.GetMember(instance, name), out value) ? value : fallback;
        }

        private static bool TryReadNumber(object instance, string name, out double value)
        {
            value = ToDouble(InformationReflection.GetMember(instance, name), double.NaN);
            return !double.IsNaN(value);
        }

        private static bool ReadBoolArrayValue(object source, int index)
        {
            if (index < 0)
            {
                return false;
            }

            bool value;
            return TryConvertBool(InformationReflection.GetIndexedValue(source, index), out value) && value;
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

        private static double ToDouble(object raw, double fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static float ToFloat(object raw, float fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
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

        internal struct FishingCatchQueryKey : IEquatable<FishingCatchQueryKey>
        {
            public string Signature { get; private set; }

            public FishingCatchQueryKey(string signature)
            {
                Signature = signature ?? string.Empty;
            }

            public bool Equals(FishingCatchQueryKey other)
            {
                return string.Equals(Signature, other.Signature, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is FishingCatchQueryKey && Equals((FishingCatchQueryKey)obj);
            }

            public override int GetHashCode()
            {
                return StringComparer.Ordinal.GetHashCode(Signature ?? string.Empty);
            }

            public override string ToString()
            {
                return Signature ?? string.Empty;
            }
        }

        private sealed class FishingCatchCacheEntry
        {
            public IList<FishingCatchCandidate> Candidates { get; set; }
            public string Message { get; set; }

            public FishingCatchCacheEntry()
            {
                Candidates = new List<FishingCatchCandidate>();
                Message = string.Empty;
            }
        }

        private sealed class FishingWaterScan
        {
            public int TotalTiles;
            public bool InLava;
            public bool InHoney;
            public int Chums;
        }

        private sealed class CorruptionRoll
        {
            public bool Corrupt { get; private set; }
            public bool Crimson { get; private set; }

            public CorruptionRoll(bool corrupt, bool crimson)
            {
                Corrupt = corrupt;
                Crimson = crimson;
            }
        }
    }
}
