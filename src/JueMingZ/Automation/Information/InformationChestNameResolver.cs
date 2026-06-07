using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal static class InformationChestNameResolver
    {
        private const int NameCacheLimit = 512;
        private static readonly Dictionary<ChestNameCacheKey, string> NameCache = new Dictionary<ChestNameCacheKey, string>();
        private static readonly Queue<ChestNameCacheKey> NameCacheOrder = new Queue<ChestNameCacheKey>();
        private static long _nameCacheHitCount;
        private static long _nameCacheMissCount;

        internal static long NameCacheHitCount
        {
            get { return _nameCacheHitCount; }
        }

        internal static long NameCacheMissCount
        {
            get { return _nameCacheMissCount; }
        }

        internal static void ResetCache()
        {
            NameCache.Clear();
            NameCacheOrder.Clear();
        }

        internal static void ResetCounters()
        {
            _nameCacheHitCount = 0;
            _nameCacheMissCount = 0;
        }

        internal static string ResolveNameWithCache(InformationWorldContext context, ChestScanCandidate candidate, string languageSignature)
        {
            string recordSignature;
            string loadedName;
            if (!TryReadRecordIdentityAt(context == null ? null : context.MainType, candidate.ChestX, candidate.ChestY, out recordSignature, out loadedName))
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
            if (NameCache.TryGetValue(key, out cached))
            {
                unchecked
                {
                    _nameCacheHitCount++;
                }

                return cached;
            }

            unchecked
            {
                _nameCacheMissCount++;
            }

            var name = !string.IsNullOrWhiteSpace(loadedName)
                ? loadedName
                : ResolveTileDisplayName(context == null ? null : context.MainType, candidate.TileType, candidate.TileStyle);
            name = string.IsNullOrWhiteSpace(name) ? DefaultLabelName(candidate.TileType) : name;
            StoreNameCache(key, name, candidate.TileType);
            return name;
        }

        internal static Dictionary<long, string> BuildNameLookup(Type mainType)
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

                        result[BuildPositionKey(chest.x, chest.y)] = chest.name ?? string.Empty;
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
                result[BuildPositionKey(chestX, chestY)] = name ?? string.Empty;
            }

            return result;
        }

        internal static bool TryReadRecordIdentityAt(Type mainType, int x, int y, out string recordSignature, out string loadedName)
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

        internal static string ResolveTileDisplayName(Type mainType, int tileType, int tileStyle)
        {
            // Container style indexes address Lang chest/dresser arrays, not
            // MapHelper lookup options. Keep default chest names on this path.
            if (InformationChestTileScanner.IsDresserTileType(tileType))
            {
                return ResolveDresserTileDisplayName(tileStyle);
            }

            if (tileType == InformationChestTileScanner.TileTypeContainers ||
                tileType == InformationChestTileScanner.TileTypeFakeContainers)
            {
                return ResolvePrimaryChestTileDisplayName(tileStyle);
            }

            if (tileType == InformationChestTileScanner.TileTypeContainers2 ||
                tileType == InformationChestTileScanner.TileTypeFakeContainers2)
            {
                return ResolveSecondaryChestTileDisplayName(tileType, tileStyle);
            }

            return DefaultLabelName(tileType);
        }

        internal static string DefaultLabelName(int tileType)
        {
            return InformationChestTileScanner.IsDresserTileType(tileType) ? "梳妆台" : "宝箱";
        }

        internal static string BuildLanguageSignature()
        {
            return CultureInfo.CurrentCulture.Name + "/" +
                   CultureInfo.CurrentUICulture.Name + "/" +
                   ReadTerrariaLanguageSignature();
        }

        internal static long BuildPositionKey(int x, int y)
        {
            unchecked
            {
                return ((long)(x & 0x7fffffff) << 32) | (uint)y;
            }
        }

        private static void StoreNameCache(ChestNameCacheKey key, string name, int tileType)
        {
            if (!NameCache.ContainsKey(key))
            {
                NameCacheOrder.Enqueue(key);
            }

            NameCache[key] = string.IsNullOrWhiteSpace(name) ? DefaultLabelName(tileType) : name;
            while (NameCacheOrder.Count > NameCacheLimit)
            {
                var oldest = NameCacheOrder.Dequeue();
                NameCache.Remove(oldest);
            }
        }

        private static string ResolvePrimaryChestTileDisplayName(int tileStyle)
        {
            var name = ResolveChestLocalizedTextValue("chestType", tileStyle, true);
            return string.IsNullOrWhiteSpace(name) ? DefaultLabelName(InformationChestTileScanner.TileTypeContainers) : name;
        }

        private static string ResolveSecondaryChestTileDisplayName(int tileType, int tileStyle)
        {
            if (tileType == InformationChestTileScanner.TileTypeContainers2 && tileStyle == 4)
            {
                var goldChestName = ResolveItemName(3988);
                if (!string.IsNullOrWhiteSpace(goldChestName) &&
                    !string.Equals(goldChestName, "3988", StringComparison.Ordinal))
                {
                    return goldChestName;
                }
            }

            var name = ResolveChestLocalizedTextValue("chestType2", tileStyle, false);
            return string.IsNullOrWhiteSpace(name) ? DefaultLabelName(tileType) : name;
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

            return DefaultLabelName(InformationChestTileScanner.TileTypeDressers);
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
    }
}
