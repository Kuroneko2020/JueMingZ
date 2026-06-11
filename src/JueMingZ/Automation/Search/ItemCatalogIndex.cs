using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Automation.Search
{
    internal static class ItemCatalogIndex
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static Dictionary<int, ItemCatalogEntry> _itemsByType = new Dictionary<int, ItemCatalogEntry>();
        private static Dictionary<int, string> _itemIdFieldNames = new Dictionary<int, string>();

        public static bool TryGet(int itemType, out ItemQueryReference reference)
        {
            reference = null;
            EnsureInitialized();

            ItemCatalogEntry entry;
            lock (SyncRoot)
            {
                if (!_itemsByType.TryGetValue(itemType, out entry))
                {
                    return false;
                }
            }

            reference = CreateReference(entry, 0);
            return true;
        }

        public static bool Contains(int itemType)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                return _itemsByType.ContainsKey(itemType);
            }
        }

        public static ItemQueryReference CreateReference(int itemType, int stack)
        {
            EnsureInitialized();

            ItemCatalogEntry entry;
            lock (SyncRoot)
            {
                if (!_itemsByType.TryGetValue(itemType, out entry))
                {
                    return null;
                }
            }

            return CreateReference(entry, stack);
        }

        public static IList<ItemQueryCandidate> ResolveCandidates(string query, int maxResults)
        {
            EnsureInitialized();

            var result = new List<ItemQueryCandidate>();
            var text = NormalizeQuery(query);
            if (text.Length <= 0)
            {
                return result;
            }

            int itemType;
            if (TryParseItemType(text, out itemType))
            {
                ItemQueryReference reference;
                if (TryGet(itemType, out reference))
                {
                    result.Add(ToCandidate(reference));
                }

                return result;
            }

            List<ItemCatalogEntry> entries;
            lock (SyncRoot)
            {
                entries = new List<ItemCatalogEntry>(_itemsByType.Values);
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var displayName = ResolveDisplayName(entry.ItemType);
                if (!MatchesText(displayName, entry.InternalName, text))
                {
                    continue;
                }

                result.Add(ToCandidate(CreateReference(entry, 0, displayName)));
            }

            result.Sort(CompareCandidates);
            if (maxResults > 0 && result.Count > maxResults)
            {
                result.RemoveRange(maxResults, result.Count - maxResults);
            }

            return result;
        }

        internal static bool TryParseItemType(string query, out int itemType)
        {
            itemType = 0;
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            var text = query.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                text = text.Substring(1).Trim();
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemType) &&
                   itemType > 0;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _initialized = false;
                _itemsByType = new Dictionary<int, ItemCatalogEntry>();
                _itemIdFieldNames = new Dictionary<int, string>();
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                var itemIdNames = BuildItemIdFieldNames();
                var entries = new Dictionary<int, ItemCatalogEntry>();
                try
                {
                    BuildFromContentSamples(entries, itemIdNames);
                    BuildFallbackFromItemIdFields(entries, itemIdNames);
                }
                catch
                {
                    entries.Clear();
                }

                _itemIdFieldNames = itemIdNames;
                _itemsByType = entries;
                _initialized = true;
            }
        }

        private static void BuildFromContentSamples(Dictionary<int, ItemCatalogEntry> entries, IDictionary<int, string> itemIdNames)
        {
            var contentSamplesType = SearchReflection.FindType("Terraria.ID.ContentSamples");
            var itemsByType = SearchReflection.GetStaticMember(contentSamplesType, "ItemsByType");
            var enumerable = itemsByType as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            foreach (var rawEntry in enumerable)
            {
                object key;
                object sample;
                if (!SearchReflection.TryReadDictionaryEntry(rawEntry, out key, out sample) || sample == null)
                {
                    continue;
                }

                int itemType;
                if (!SearchReflection.TryConvertInt(key, out itemType))
                {
                    SearchReflection.TryReadInt(sample, "type", out itemType);
                }

                if (itemType <= 0)
                {
                    continue;
                }

                var entry = CreateEntry(itemType, sample, itemIdNames);
                entries[itemType] = entry;
            }
        }

        private static void BuildFallbackFromItemIdFields(Dictionary<int, ItemCatalogEntry> entries, IDictionary<int, string> itemIdNames)
        {
            foreach (var pair in itemIdNames)
            {
                if (pair.Key <= 0 || entries.ContainsKey(pair.Key))
                {
                    continue;
                }

                var internalName = ResolveInternalName(pair.Key, itemIdNames);
                if (string.IsNullOrWhiteSpace(internalName))
                {
                    continue;
                }

                entries[pair.Key] = new ItemCatalogEntry
                {
                    ItemType = pair.Key,
                    InternalName = internalName,
                    CreateTile = -1,
                    CreateWall = -1
                };
            }
        }

        private static ItemCatalogEntry CreateEntry(int itemType, object sample, IDictionary<int, string> itemIdNames)
        {
            int maxStack;
            int rare;
            int value;
            int createTile;
            int createWall;
            int consumableRaw;
            SearchReflection.TryReadInt(sample, "maxStack", out maxStack);
            SearchReflection.TryReadInt(sample, "rare", out rare);
            SearchReflection.TryReadInt(sample, "value", out value);
            if (!SearchReflection.TryReadInt(sample, "createTile", out createTile))
            {
                createTile = -1;
            }

            if (!SearchReflection.TryReadInt(sample, "createWall", out createWall))
            {
                createWall = -1;
            }

            var consumable = false;
            object rawConsumable = SearchReflection.GetMember(sample, "consumable");
            if (!SearchReflection.TryConvertBool(rawConsumable, out consumable) &&
                SearchReflection.TryReadInt(sample, "consumable", out consumableRaw))
            {
                consumable = consumableRaw != 0;
            }

            return new ItemCatalogEntry
            {
                ItemType = itemType,
                InternalName = ResolveInternalName(itemType, itemIdNames),
                MaxStack = maxStack,
                Rare = rare,
                Value = value,
                IsMaterial = ReadMaterialFlag(itemType),
                IsConsumable = consumable,
                CreateTile = createTile,
                CreateWall = createWall
            };
        }

        private static ItemQueryReference CreateReference(ItemCatalogEntry entry, int stack)
        {
            return CreateReference(entry, stack, ResolveDisplayName(entry.ItemType));
        }

        private static ItemQueryReference CreateReference(ItemCatalogEntry entry, int stack, string displayName)
        {
            return new ItemQueryReference
            {
                ItemType = entry.ItemType,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? "#" + entry.ItemType.ToString(CultureInfo.InvariantCulture)
                    : displayName.Trim(),
                InternalName = entry.InternalName ?? string.Empty,
                Stack = stack,
                MaxStack = entry.MaxStack,
                Rare = entry.Rare,
                Value = entry.Value,
                IsMaterial = entry.IsMaterial,
                IsConsumable = entry.IsConsumable,
                CreateTile = entry.CreateTile,
                CreateWall = entry.CreateWall
            };
        }

        private static string ResolveDisplayName(int itemType)
        {
            object raw;
            var langType = SearchReflection.FindType("Terraria.Lang") ??
                           SearchReflection.FindType("Terraria.Localization.Lang");
            if (SearchReflection.TryInvokeStatic(langType, "GetItemNameValue", new object[] { itemType }, out raw) && raw != null)
            {
                var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                return value == null ? string.Empty : value.Trim();
            }

            return string.Empty;
        }

        private static string ResolveInternalName(int itemType, IDictionary<int, string> itemIdNames)
        {
            var itemIdType = SearchReflection.FindType("Terraria.ID.ItemID");
            var search = SearchReflection.GetStaticMember(itemIdType, "Search");
            object raw;
            if (SearchReflection.TryInvokeInstance(search, "GetName", new object[] { itemType }, out raw) && raw != null)
            {
                var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            string fieldName;
            return itemIdNames != null && itemIdNames.TryGetValue(itemType, out fieldName)
                ? fieldName
                : string.Empty;
        }

        private static bool ReadMaterialFlag(int itemType)
        {
            var setsType = SearchReflection.FindType("Terraria.ID.ItemID+Sets");
            var raw = SearchReflection.GetStaticMember(setsType, "IsAMaterial");
            return ReadBoolArrayValue(raw, itemType);
        }

        private static bool ReadBoolArrayValue(object source, int index)
        {
            if (source == null || index < 0)
            {
                return false;
            }

            try
            {
                var array = source as Array;
                if (array != null && array.Rank == 1 && index < array.GetLength(0))
                {
                    bool value;
                    return SearchReflection.TryConvertBool(array.GetValue(index), out value) && value;
                }

                var list = source as IList;
                if (list != null && index < list.Count)
                {
                    bool value;
                    return SearchReflection.TryConvertBool(list[index], out value) && value;
                }
            }
            catch
            {
            }

            return false;
        }

        private static Dictionary<int, string> BuildItemIdFieldNames()
        {
            var result = new Dictionary<int, string>();
            var itemIdType = SearchReflection.FindType("Terraria.ID.ItemID");
            if (itemIdType == null)
            {
                return result;
            }

            try
            {
                var fields = itemIdType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (var index = 0; index < fields.Length; index++)
                {
                    var field = fields[index];
                    if (field.FieldType != typeof(int) && field.FieldType != typeof(short))
                    {
                        continue;
                    }

                    int itemType;
                    if (!SearchReflection.TryConvertInt(field.GetValue(null), out itemType) || itemType <= 0)
                    {
                        continue;
                    }

                    if (!result.ContainsKey(itemType))
                    {
                        result.Add(itemType, field.Name);
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        private static string NormalizeQuery(string query)
        {
            return string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        }

        private static bool MatchesText(string displayName, string internalName, string query)
        {
            if (!string.IsNullOrWhiteSpace(displayName) &&
                displayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(internalName) &&
                   internalName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ItemQueryCandidate ToCandidate(ItemQueryReference reference)
        {
            return new ItemQueryCandidate
            {
                ItemType = reference.ItemType,
                DisplayName = reference.DisplayName,
                InternalName = reference.InternalName
            };
        }

        private static int CompareCandidates(ItemQueryCandidate left, ItemQueryCandidate right)
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

            var displayName = StringComparer.CurrentCultureIgnoreCase.Compare(left.DisplayName ?? string.Empty, right.DisplayName ?? string.Empty);
            if (displayName != 0)
            {
                return displayName;
            }

            return left.ItemType.CompareTo(right.ItemType);
        }

        private sealed class ItemCatalogEntry
        {
            public int ItemType;
            public string InternalName;
            public int MaxStack;
            public int Rare;
            public int Value;
            public bool IsMaterial;
            public bool IsConsumable;
            public int CreateTile;
            public int CreateWall;

            public ItemCatalogEntry()
            {
                InternalName = string.Empty;
                CreateTile = -1;
                CreateWall = -1;
            }
        }
    }
}
