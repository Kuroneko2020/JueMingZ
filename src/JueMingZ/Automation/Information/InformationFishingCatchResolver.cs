using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.Fishing.Filtering;

namespace JueMingZ.Automation.Information
{
    internal static partial class InformationFishingCatchResolver
    {
        private const int TileSize = 16;
        private const int MinimumWaterTilesForFishingRules = 75;

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
            return ResolveCatchCandidates(context, bobberWorldX, bobberWorldY, -1, string.Empty, out message);
        }

        public static IList<FishingCatchCandidate> ResolveCatchCandidates(InformationWorldContext context, float bobberWorldX, float bobberWorldY, string filterSignature, out string message)
        {
            return ResolveCatchCandidates(context, bobberWorldX, bobberWorldY, -1, filterSignature, out message);
        }

        public static IList<FishingCatchCandidate> ResolveCatchCandidates(InformationWorldContext context, float bobberWorldX, float bobberWorldY, int bobberIdentity, string filterSignature, out string message)
        {
            message = string.Empty;
            var candidates = new List<FishingCatchCandidate>();
            // This resolver is display-only. Status panels and filter pickers may
            // read candidates, but must not submit fishing actions or move items.
            if (context == null || context.LocalPlayer == null || context.MainType == null)
            {
                message = "环境不可用";
                return candidates;
            }

            IList rules;
            if (!InformationFishDropRuleEvaluator.TryGetFishDropRules(context, out rules) || rules == null || rules.Count <= 0)
            {
                message = "鱼获规则不可用";
                return candidates;
            }

            var tileX = (int)Math.Floor(bobberWorldX / TileSize);
            var tileY = (int)Math.Floor(bobberWorldY / TileSize);
            var earlyEnvironment = InformationFishingEnvironmentReader.ReadEarlyEnvironmentSnapshot(context);
            var earlyKey = BuildEarlyCatchCacheKey(
                context,
                tileX,
                tileY,
                bobberIdentity,
                earlyEnvironment.PolePower,
                earlyEnvironment.PoleItemType,
                earlyEnvironment.BaitPower,
                earlyEnvironment.BaitItemType,
                earlyEnvironment.QuestFish);
            FishingCatchCacheEntry earlyCached;
            // Early cache hits must return before water scans, FishingConditions
            // reads, rule enumeration, and display string building.
            if (InformationFishingCatchCache.TryGetEarlyCatchCache(earlyKey, out earlyCached))
            {
                message = earlyCached.Message;
                return earlyCached.Candidates;
            }

            var water = InformationFishingWaterScanner.ScanFishingWater(context, tileX, tileY);
            if (water.TotalTiles < MinimumWaterTilesForFishingRules)
            {
                // Keep water shortage as its own player-visible reason; it is
                // not the same state as unavailable rules or empty candidates.
                message = "水体不足";
                InformationFishingCatchCache.StoreEarlyCatchCache(earlyKey, candidates, message);
                return candidates;
            }

            var fishingConditions = InformationFishingEnvironmentReader.ReadFishingConditionsForDisplay(context);
            var environment = InformationFishingEnvironmentReader.ReadResolvedEnvironmentSnapshot(context, fishingConditions, earlyEnvironment.QuestFish);
            if (environment.FinalFishingLevel <= 0)
            {
                message = "暂无可解析鱼获";
                InformationFishingCatchCache.StoreEarlyCatchCache(earlyKey, candidates, message);
                return candidates;
            }

            if (environment.BaitItemType == 2673)
            {
                message = "松露虫不列入普通鱼获";
                InformationFishingCatchCache.StoreEarlyCatchCache(earlyKey, candidates, message);
                return candidates;
            }

            var waterPenalty = InformationFishingWaterScanner.ApplyFishingWaterPenalty(context, bobberWorldY, water, environment.FinalFishingLevel);
            var queryKey = BuildCatchQueryKey(
                context,
                tileX,
                tileY,
                InformationFishingWaterScanner.ResolveLiquidKind(water),
                water.TotalTiles,
                water.Chums,
                waterPenalty.WaterNeeded,
                waterPenalty.JunkPossible,
                environment.FinalFishingLevel,
                waterPenalty.FishingLevel,
                environment.PolePower,
                environment.PoleItemType,
                environment.BaitPower,
                environment.BaitItemType,
                environment.CanFishInLava,
                environment.QuestFish,
                filterSignature);
            FishingCatchCacheEntry cached;
            if (InformationFishingCatchCache.TryGetCatchCacheAndStoreEarly(queryKey, earlyKey, out cached))
            {
                message = cached.Message;
                return cached.Candidates;
            }

            var catchIds = new List<int>();
            var seen = new HashSet<int>();
            var jungle = HasZone(context.LocalPlayer, "ZoneJungle");
            var snow = HasZone(context.LocalPlayer, "ZoneSnow");
            var desert = HasZone(context.LocalPlayer, "ZoneDesert") || HasZone(context.LocalPlayer, "ZoneUndergroundDesert");
            var infectedDesert = desert && (HasZone(context.LocalPlayer, "ZoneCorrupt") || HasZone(context.LocalPlayer, "ZoneCrimson"));

            InformationFishingConditionRolls.ForEachRoll(
                context,
                tileY,
                water.InHoney,
                jungle,
                snow,
                desert,
                infectedDesert,
                waterPenalty.JunkPossible,
                delegate(FishingConditionRoll roll)
                {
                    if (catchIds.Count >= InformationFishDropRuleEvaluator.MaxCatchItems)
                    {
                        return false;
                    }

                    var fishingContext = InformationFishingContextFactory.CreateContext(new FishingAttemptSpec
                    {
                        Context = context,
                        FishingConditions = fishingConditions,
                        TileX = tileX,
                        TileY = tileY,
                        InLava = water.InLava,
                        InHoney = roll.InHoney,
                        WaterTilesCount = water.TotalTiles,
                        WaterNeeded = waterPenalty.WaterNeeded,
                        Chums = water.Chums,
                        FishingLevel = waterPenalty.FishingLevel,
                        CanFishInLava = environment.CanFishInLava,
                        QuestFish = environment.QuestFish,
                        Roll = roll
                    });

                    if (fishingContext != null)
                    {
                        InformationFishDropRuleEvaluator.AddMatchingRuleItems(rules, fishingContext, catchIds, seen);
                    }

                    return catchIds.Count < InformationFishDropRuleEvaluator.MaxCatchItems;
                });

            for (var index = 0; index < catchIds.Count; index++)
            {
                var itemId = catchIds[index];
                var candidate = InformationFishingItemNameResolver.CreateCurrentWaterCandidate(itemId, environment.QuestFish);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }

            candidates.Sort(InformationFishingItemNameResolver.CompareCandidates);

            if (candidates.Count <= 0)
            {
                message = "暂无可解析鱼获";
            }

            InformationFishingCatchCache.StoreCatchCache(queryKey, candidates, message);
            InformationFishingCatchCache.StoreEarlyCatchCache(earlyKey, candidates, message);
            return candidates;
        }

        public static IList<FishingCatchCandidate> ResolveGlobalFishableItemCandidates(
            InformationWorldContext context,
            string query,
            int maxResults,
            out bool truncated,
            out string message)
        {
            return InformationFishingCatchSearchService.ResolveGlobalFishableItemCandidates(
                new FishingSearchRequest
                {
                    Context = context,
                    Query = query,
                    MaxResults = maxResults
                },
                out truncated,
                out message);
        }

        private static FishingCatchEarlyCacheKey BuildEarlyCatchCacheKey(
            InformationWorldContext context,
            int tileX,
            int tileY,
            int bobberIdentity,
            int polePower,
            int poleItemType,
            int baitPower,
            int baitItemType,
            int questFish)
        {
            // The early key is the guard before heavy reads; changing fields here
            // changes when scans and FishingConditions calls are skipped.
            return InformationFishingCatchSignatureBuilder.BuildEarlyCatchCacheKey(new FishingCatchEarlyQuerySpec
            {
                Context = context,
                TileX = tileX,
                TileY = tileY,
                BobberIdentity = bobberIdentity,
                PolePower = polePower,
                PoleItemType = poleItemType,
                BaitPower = baitPower,
                BaitItemType = baitItemType,
                QuestFish = questFish
            });
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
            return InformationFishingCatchSignatureBuilder.BuildCatchQueryKey(new FishingCatchQuerySpec
            {
                Context = context,
                TileX = tileX,
                TileY = tileY,
                LiquidKind = liquidKind,
                WaterTiles = waterTiles,
                Chums = chums,
                WaterNeeded = waterNeeded,
                JunkPossible = junkPossible,
                FinalFishingLevel = finalFishingLevel,
                FishingLevel = fishingLevel,
                PolePower = polePower,
                PoleItemType = poleItemType,
                BaitPower = baitPower,
                BaitItemType = baitItemType,
                CanFishInLava = canFishInLava,
                QuestFish = questFish,
                FilterSignature = filterSignature
            });
        }

        internal static int GetCollectionCount(object source)
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

        internal static bool HasZone(object player, string member)
        {
            bool value;
            return InformationReflection.TryReadBool(player, member, out value) && value;
        }

        internal static object InvokeInstance(object instance, string methodName, object[] args)
        {
            // Optional vanilla calls enrich display only; unavailable reflection
            // returns null and must not trigger fishing actions.
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

        internal static void SetMember(object instance, string name, object value)
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

        internal static int ReadStaticInt(Type type, string name, int fallback)
        {
            return ToInt(InformationReflection.GetStaticMember(type, name), fallback);
        }

        internal static double ReadStaticDouble(Type type, string name, double fallback)
        {
            return ToDouble(InformationReflection.GetStaticMember(type, name), fallback);
        }

        internal static bool ReadStaticBool(Type type, string name, bool fallback)
        {
            bool value;
            return TryConvertBool(InformationReflection.GetStaticMember(type, name), out value) ? value : fallback;
        }

        internal static int ReadInt(object instance, string name, int fallback)
        {
            return ToInt(InformationReflection.GetMember(instance, name), fallback);
        }

        internal static float ReadFloat(object instance, string name, float fallback)
        {
            return ToFloat(InformationReflection.GetMember(instance, name), fallback);
        }

        internal static bool ReadBool(object instance, string name, bool fallback)
        {
            bool value;
            return TryConvertBool(InformationReflection.GetMember(instance, name), out value) ? value : fallback;
        }

        internal static bool TryReadNumber(object instance, string name, out double value)
        {
            value = ToDouble(InformationReflection.GetMember(instance, name), double.NaN);
            return !double.IsNaN(value);
        }

        internal static bool ReadBoolArrayValue(object source, int index)
        {
            if (index < 0)
            {
                return false;
            }

            bool value;
            return TryConvertBool(InformationReflection.GetIndexedValue(source, index), out value) && value;
        }

        internal static int ToInt(object raw, int fallback)
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

        internal static double ToDouble(object raw, double fallback)
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

        internal static float ToFloat(object raw, float fallback)
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

        internal static bool TryConvertBool(object raw, out bool value)
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

    }
}
