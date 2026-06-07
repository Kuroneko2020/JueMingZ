using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.Fishing.Filtering;

namespace JueMingZ.Automation.Information
{
    // Item-name fallbacks are display text only; missing names must not create items or mark rules invalid.
    internal static class InformationFishingItemNameResolver
    {
        private static bool _crateSetsInitialized;
        private static object _isFishingCrateSet;
        private static object _isFishingCrateHardmodeSet;
        private static readonly Dictionary<int, string> ItemInternalNameCache = new Dictionary<int, string>();

        public static string ResolveItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetItemNameValue", new object[] { itemId }, out raw) && raw != null)
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return itemId.ToString(CultureInfo.InvariantCulture);
        }

        public static string ResolveInternalItemName(int itemId)
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

        public static bool IsFishingCrateItem(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            EnsureFishingCrateSets();
            return InformationFishingCatchResolver.ReadBoolArrayValue(_isFishingCrateSet, itemId) ||
                   InformationFishingCatchResolver.ReadBoolArrayValue(_isFishingCrateHardmodeSet, itemId);
        }

        public static HashSet<int> ReadAnglerQuestFishIds(InformationWorldContext context)
        {
            var result = new HashSet<int>();
            if (context == null || context.MainType == null)
            {
                return result;
            }

            var itemIds = InformationReflection.GetStaticMember(context.MainType, "anglerQuestItemNetIDs");
            var count = InformationFishingCatchResolver.GetCollectionCount(itemIds);
            for (var index = 0; index < count; index++)
            {
                var itemId = InformationFishingCatchResolver.ToInt(InformationReflection.GetIndexedValue(itemIds, index), 0);
                if (itemId > 0)
                {
                    result.Add(itemId);
                }
            }

            return result;
        }

        public static FishingCatchCandidate CreateCurrentWaterCandidate(int itemId, int questFish)
        {
            var name = ResolveItemName(itemId);
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return new FishingCatchCandidate
            {
                Kind = FishingCatchKinds.Item,
                Id = itemId,
                DisplayName = name,
                DisplayNameSnapshot = name,
                IsCrate = IsFishingCrateItem(itemId),
                IsQuestFish = itemId == questFish,
                IsEnemy = false
            };
        }

        public static FishingCatchCandidate CreateGlobalSearchCandidate(int itemId, string displayName, ISet<int> questFishIds)
        {
            var name = string.IsNullOrWhiteSpace(displayName)
                ? "#" + itemId.ToString(CultureInfo.InvariantCulture)
                : displayName.Trim();
            return new FishingCatchCandidate
            {
                Kind = FishingCatchKinds.Item,
                Id = itemId,
                DisplayName = name,
                DisplayNameSnapshot = name,
                IsCrate = IsFishingCrateItem(itemId),
                IsQuestFish = questFishIds != null && questFishIds.Contains(itemId),
                IsEnemy = false
            };
        }

        public static int CompareCandidates(FishingCatchCandidate left, FishingCatchCandidate right)
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

        internal static int InternalNameCacheCountForTesting
        {
            get { return ItemInternalNameCache.Count; }
        }

        internal static void ResetForTesting()
        {
            _crateSetsInitialized = false;
            _isFishingCrateSet = null;
            _isFishingCrateHardmodeSet = null;
            lock (ItemInternalNameCache)
            {
                ItemInternalNameCache.Clear();
            }
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

        private static string ResolveInternalItemNameUncached(int itemId)
        {
            var itemIdType = InformationReflection.FindType("Terraria.ID.ItemID");
            if (itemIdType == null)
            {
                return string.Empty;
            }

            var search = InformationReflection.GetStaticMember(itemIdType, "Search");
            var raw = InformationFishingCatchResolver.InvokeInstance(search, "GetName", new object[] { itemId });
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

                    if (InformationFishingCatchResolver.ToInt(field.GetValue(null), 0) == itemId)
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
    }
}
