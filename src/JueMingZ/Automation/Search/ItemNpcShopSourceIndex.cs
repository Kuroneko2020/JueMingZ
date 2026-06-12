using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Search
{
    internal static class ItemNpcShopSourceIndex
    {
        private const string CurrentShopContext = "当前已打开 NPC 商店快照";
        private static readonly object SyncRoot = new object();
        private static CacheKey _cacheKey;
        private static Dictionary<int, List<ItemAcquisitionSourceSummary>> _sourcesByItemType =
            new Dictionary<int, List<ItemAcquisitionSourceSummary>>();

        public static IList<ItemAcquisitionSourceSummary> GetSources(int itemType)
        {
            var key = CaptureCacheKey();
            lock (SyncRoot)
            {
                if (!key.Equals(_cacheKey))
                {
                    _sourcesByItemType = BuildCurrentShopIndex(key);
                    _cacheKey = key;
                }

                List<ItemAcquisitionSourceSummary> sources;
                if (!_sourcesByItemType.TryGetValue(itemType, out sources) || sources.Count <= 0)
                {
                    return new List<ItemAcquisitionSourceSummary>();
                }

                var result = new List<ItemAcquisitionSourceSummary>(sources.Count);
                for (var index = 0; index < sources.Count; index++)
                {
                    result.Add(Clone(sources[index]));
                }

                return result;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _cacheKey = default(CacheKey);
                _sourcesByItemType = new Dictionary<int, List<ItemAcquisitionSourceSummary>>();
            }
        }

        private static CacheKey CaptureCacheKey()
        {
            var mainType = SearchReflection.FindType("Terraria.Main");
            var key = new CacheKey
            {
                ShopIndex = ReadStaticInt(mainType, "npcShop", 0),
                PlayerIndex = ReadStaticInt(mainType, "myPlayer", 0),
                GameUpdateCount = ReadStaticLong(mainType, "GameUpdateCount", 0L)
            };

            var player = ResolveLocalPlayer(mainType, key.PlayerIndex);
            if (player != null)
            {
                key.TalkNpc = ReadMemberInt(player, "talkNPC", -1);
            }
            else
            {
                key.TalkNpc = -1;
            }

            key.NpcType = ResolveNpcType(mainType, key.TalkNpc);

            return key;
        }

        private static Dictionary<int, List<ItemAcquisitionSourceSummary>> BuildCurrentShopIndex(CacheKey key)
        {
            var result = new Dictionary<int, List<ItemAcquisitionSourceSummary>>();
            if (key.ShopIndex <= 0)
            {
                return result;
            }

            // Search query must not open or rebuild shops; this index only copies
            // the already-open vanilla shop chest into immutable source rows.
            object shopChest;
            if (!TryGetCurrentShopChest(key.ShopIndex, out shopChest))
            {
                return result;
            }

            var items = SearchReflection.GetMember(shopChest, "item") as IEnumerable;
            if (items == null)
            {
                return result;
            }

            var sourceName = ResolveShopSourceName(key);
            var contextText = BuildContextText(key);
            foreach (var rawItem in items)
            {
                int itemType;
                if (!TryReadShopItem(rawItem, out itemType) || itemType <= 0)
                {
                    continue;
                }

                AddSource(result, new ItemAcquisitionSourceSummary
                {
                    SourceType = ItemAcquisitionSourceTypes.NpcShop,
                    Title = "NPC出售",
                    SourceName = sourceName,
                    QuantityText = "可购买",
                    ProbabilityText = string.Empty,
                    ConditionText = "当前上下文可售卖",
                    ContextText = contextText,
                    ItemType = itemType,
                    NpcNetId = key.NpcType > 0 ? key.NpcType : key.TalkNpc,
                    RelatedItemType = itemType
                });
            }

            SortSources(result);
            return result;
        }

        private static bool TryGetCurrentShopChest(int shopIndex, out object shopChest)
        {
            shopChest = null;
            var mainType = SearchReflection.FindType("Terraria.Main");
            var instance = SearchReflection.GetStaticMember(mainType, "instance");
            var shops = SearchReflection.GetMember(instance, "shop");
            if (shops == null)
            {
                return false;
            }

            var count = SearchReflection.GetCollectionCount(shops);
            if (shopIndex <= 0 || shopIndex >= count)
            {
                return false;
            }

            shopChest = SearchReflection.GetIndexedValue(shops, shopIndex);
            return shopChest != null;
        }

        private static bool TryReadShopItem(object item, out int itemType)
        {
            itemType = 0;
            if (item == null)
            {
                return false;
            }

            if (!SearchReflection.TryReadInt(item, "type", out itemType) || itemType <= 0)
            {
                return false;
            }

            int stack;
            if (SearchReflection.TryReadInt(item, "stack", out stack) && stack <= 0)
            {
                return false;
            }

            return true;
        }

        private static void AddSource(Dictionary<int, List<ItemAcquisitionSourceSummary>> result, ItemAcquisitionSourceSummary source)
        {
            List<ItemAcquisitionSourceSummary> sources;
            if (!result.TryGetValue(source.ItemType, out sources))
            {
                sources = new List<ItemAcquisitionSourceSummary>();
                result[source.ItemType] = sources;
            }

            if (!ContainsEquivalent(sources, source))
            {
                sources.Add(source);
            }
        }

        private static bool ContainsEquivalent(IList<ItemAcquisitionSourceSummary> sources, ItemAcquisitionSourceSummary candidate)
        {
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (source.ItemType == candidate.ItemType &&
                    source.NpcNetId == candidate.NpcNetId &&
                    string.Equals(source.SourceName, candidate.SourceName, StringComparison.Ordinal) &&
                    string.Equals(source.ContextText, candidate.ContextText, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveShopSourceName(CacheKey key)
        {
            if (key.NpcType > 0)
            {
                var npcName = ResolveNpcNameByType(key.NpcType);
                if (!string.IsNullOrWhiteSpace(npcName))
                {
                    return npcName;
                }
            }

            return "Shop #" + key.ShopIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static int ResolveNpcType(Type mainType, int npcIndex)
        {
            var npcs = SearchReflection.GetStaticMember(mainType, "npc");
            var npc = SearchReflection.GetIndexedValue(npcs, npcIndex);
            int npcType;
            if (npc != null && SearchReflection.TryReadInt(npc, "type", out npcType) && npcType != 0)
            {
                return npcType;
            }

            return 0;
        }

        private static string ResolveNpcNameByType(int npcType)
        {
            object raw;
            var langType = SearchReflection.FindType("Terraria.Lang");
            if (SearchReflection.TryInvokeStatic(langType, "GetNPCNameValue", new object[] { npcType }, out raw) && raw != null)
            {
                var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            var npcIdType = SearchReflection.FindType("Terraria.ID.NPCID");
            var search = SearchReflection.GetStaticMember(npcIdType, "Search");
            if (SearchReflection.TryInvokeInstance(search, "GetName", new object[] { npcType }, out raw) && raw != null)
            {
                var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return "NPC #" + npcType.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildContextText(CacheKey key)
        {
            var text = CurrentShopContext + " / Shop #" + key.ShopIndex.ToString(CultureInfo.InvariantCulture);
            if (key.TalkNpc >= 0)
            {
                text += " / talkNPC #" + key.TalkNpc.ToString(CultureInfo.InvariantCulture);
            }

            return text;
        }

        private static object ResolveLocalPlayer(Type mainType, int playerIndex)
        {
            var localPlayer = SearchReflection.GetStaticMember(mainType, "LocalPlayer");
            if (localPlayer != null)
            {
                return localPlayer;
            }

            var players = SearchReflection.GetStaticMember(mainType, "player");
            return SearchReflection.GetIndexedValue(players, playerIndex);
        }

        private static int ReadStaticInt(Type type, string name, int fallback)
        {
            int value;
            return SearchReflection.TryConvertInt(SearchReflection.GetStaticMember(type, name), out value) ? value : fallback;
        }

        private static long ReadStaticLong(Type type, string name, long fallback)
        {
            var raw = SearchReflection.GetStaticMember(type, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static int ReadMemberInt(object instance, string name, int fallback)
        {
            int value;
            return SearchReflection.TryConvertInt(SearchReflection.GetMember(instance, name), out value) ? value : fallback;
        }

        private static void SortSources(Dictionary<int, List<ItemAcquisitionSourceSummary>> sourcesByItemType)
        {
            foreach (var pair in sourcesByItemType)
            {
                pair.Value.Sort(CompareSources);
            }
        }

        private static int CompareSources(ItemAcquisitionSourceSummary left, ItemAcquisitionSourceSummary right)
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

            var name = StringComparer.CurrentCultureIgnoreCase.Compare(left.SourceName ?? string.Empty, right.SourceName ?? string.Empty);
            return name != 0 ? name : left.ItemType.CompareTo(right.ItemType);
        }

        private static ItemAcquisitionSourceSummary Clone(ItemAcquisitionSourceSummary source)
        {
            return new ItemAcquisitionSourceSummary
            {
                SourceType = source.SourceType,
                Title = source.Title,
                SourceName = source.SourceName,
                QuantityText = source.QuantityText,
                ProbabilityText = source.ProbabilityText,
                ConditionText = source.ConditionText,
                ContextText = source.ContextText,
                ItemType = source.ItemType,
                NpcNetId = source.NpcNetId,
                RelatedItemType = source.RelatedItemType
            };
        }

        private struct CacheKey : IEquatable<CacheKey>
        {
            public int ShopIndex;
            public int PlayerIndex;
            public int TalkNpc;
            public int NpcType;
            public long GameUpdateCount;

            public bool Equals(CacheKey other)
            {
                return ShopIndex == other.ShopIndex &&
                       PlayerIndex == other.PlayerIndex &&
                       TalkNpc == other.TalkNpc &&
                       NpcType == other.NpcType &&
                       GameUpdateCount == other.GameUpdateCount;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey && Equals((CacheKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = ShopIndex;
                    hash = (hash * 397) ^ PlayerIndex;
                    hash = (hash * 397) ^ TalkNpc;
                    hash = (hash * 397) ^ NpcType;
                    hash = (hash * 397) ^ GameUpdateCount.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
