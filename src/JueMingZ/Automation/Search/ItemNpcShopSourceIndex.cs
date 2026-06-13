using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Search
{
    internal static class ItemNpcShopSourceIndex
    {
        private const string CurrentShopContext = "当前打开的商店";
        private const string FullShopContext = "原版商店资料";
        private const long ContextRefreshTicks = 300L;
        private static readonly object SyncRoot = new object();
        private static readonly ShopDefinition[] ShopDefinitions = CreateShopDefinitions();
        private static readonly KnownShopSource[] KnownShopSources = CreateKnownShopSources();
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
                    _sourcesByItemType = BuildFullShopIndex(key);
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
                GameUpdateBucket = ReadStaticLong(mainType, "GameUpdateCount", 0L) / ContextRefreshTicks
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
            key.ContextSignature = BuildContextSignature(mainType, player);

            return key;
        }

        private static Dictionary<int, List<ItemAcquisitionSourceSummary>> BuildFullShopIndex(CacheKey key)
        {
            var result = new Dictionary<int, List<ItemAcquisitionSourceSummary>>();
            for (var index = 0; index < ShopDefinitions.Length; index++)
            {
                AddShopSources(result, ShopDefinitions[index], key);
            }

            AddKnownShopSourceHints(result);
            SortSources(result);
            return result;
        }

        private static void AddShopSources(Dictionary<int, List<ItemAcquisitionSourceSummary>> result, ShopDefinition definition, CacheKey key)
        {
            object shopChest;
            var contextText = BuildFullContextText(definition);
            if (!TryCreatePopulatedShopChest(definition.ShopIndex, out shopChest))
            {
                if (!TryGetExistingShopChest(definition.ShopIndex, out shopChest))
                {
                    return;
                }

                contextText = key.ShopIndex == definition.ShopIndex
                    ? BuildCurrentContextText(definition, key)
                    : BuildFullContextText(definition);
            }

            var items = SearchReflection.GetMember(shopChest, "item") as IEnumerable;
            if (items == null)
            {
                return;
            }

            var sourceName = ResolveShopSourceName(definition);
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
                    ConditionText = IsKnownConditionalShopItem(definition.ShopIndex, itemType)
                        ? "当前可购买 / 条件库存可能变化"
                        : "常驻商品 / 当前可购买",
                    ContextText = contextText,
                    ItemType = itemType,
                    NpcNetId = definition.NpcType,
                    RelatedItemType = itemType
                });
            }
        }

        private static void AddKnownShopSourceHints(Dictionary<int, List<ItemAcquisitionSourceSummary>> result)
        {
            for (var index = 0; index < KnownShopSources.Length; index++)
            {
                var hint = KnownShopSources[index];
                if (hint.ItemType <= 0)
                {
                    continue;
                }

                ShopDefinition definition;
                if (!TryFindShopDefinition(hint.ShopIndex, out definition))
                {
                    continue;
                }

                AddSource(result, new ItemAcquisitionSourceSummary
                {
                    SourceType = ItemAcquisitionSourceTypes.NpcShop,
                    Title = "NPC出售",
                    SourceName = ResolveShopSourceName(definition),
                    QuantityText = "可购买",
                    ProbabilityText = string.Empty,
                    ConditionText = hint.ConditionText,
                    ContextText = BuildFullContextText(definition),
                    ItemType = hint.ItemType,
                    NpcNetId = definition.NpcType,
                    RelatedItemType = hint.ItemType
                });
            }
        }

        private static bool TryCreatePopulatedShopChest(int shopIndex, out object shopChest)
        {
            shopChest = null;
            var chestType = SearchReflection.FindType("Terraria.Chest");
            if (chestType == null)
            {
                return false;
            }

            try
            {
                shopChest = Activator.CreateInstance(chestType);
            }
            catch
            {
                shopChest = null;
            }

            object ignored;
            // This is the only production path that may call SetupShop: it runs
            // against an isolated temporary Chest during query construction, never
            // against Main.instance.shop and never from the UI draw/hit-test path.
            if (shopChest == null || !SearchReflection.TryInvokeInstance(shopChest, "SetupShop", new object[] { shopIndex }, out ignored))
            {
                shopChest = null;
                return false;
            }

            return true;
        }

        private static bool TryGetExistingShopChest(int shopIndex, out object shopChest)
        {
            shopChest = null;
            if (!TryGetShopChest(shopIndex, out shopChest))
            {
                return false;
            }

            return HasReadableShopItems(shopChest);
        }

        private static bool TryGetShopChest(int shopIndex, out object shopChest)
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

        private static bool HasReadableShopItems(object shopChest)
        {
            var items = SearchReflection.GetMember(shopChest, "item") as IEnumerable;
            if (items == null)
            {
                return false;
            }

            foreach (var rawItem in items)
            {
                int itemType;
                if (TryReadShopItem(rawItem, out itemType))
                {
                    return true;
                }
            }

            return false;
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
                    string.Equals(source.SourceName, candidate.SourceName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveShopSourceName(ShopDefinition definition)
        {
            var npcName = ResolveNpcNameByType(definition.NpcType);
            if (string.IsNullOrWhiteSpace(npcName))
            {
                npcName = "未知 NPC 商店";
            }

            if (!string.IsNullOrWhiteSpace(definition.EntryName) &&
                !string.Equals(definition.EntryName, "出售", StringComparison.Ordinal))
            {
                return npcName + "（" + definition.EntryName + "）";
            }

            return npcName;
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

            return string.Empty;
        }

        private static string BuildFullContextText(ShopDefinition definition)
        {
            var text = FullShopContext;
            if (!string.IsNullOrWhiteSpace(definition.EntryName))
            {
                text += " / 店铺：" + definition.EntryName;
            }

            return text;
        }

        private static string BuildCurrentContextText(ShopDefinition definition, CacheKey key)
        {
            var text = CurrentShopContext + " / " + FullShopContext;
            if (!string.IsNullOrWhiteSpace(definition.EntryName))
            {
                text += " / 店铺：" + definition.EntryName;
            }

            if (key.TalkNpc >= 0)
            {
                text += " / 当前对话 NPC";
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

        private static bool ReadStaticBool(Type type, string name)
        {
            bool value;
            return SearchReflection.TryConvertBool(SearchReflection.GetStaticMember(type, name), out value) && value;
        }

        private static bool ReadMemberBool(object instance, string name)
        {
            bool value;
            return SearchReflection.TryConvertBool(SearchReflection.GetMember(instance, name), out value) && value;
        }

        private static int BuildContextSignature(Type mainType, object player)
        {
            unchecked
            {
                var hash = 17;
                AddBool(ref hash, ReadStaticBool(mainType, "hardMode"));
                AddBool(ref hash, ReadStaticBool(mainType, "dayTime"));
                AddBool(ref hash, ReadStaticBool(mainType, "bloodMoon"));
                AddBool(ref hash, ReadStaticBool(mainType, "eclipse"));
                AddBool(ref hash, ReadStaticBool(mainType, "pumpkinMoon"));
                AddBool(ref hash, ReadStaticBool(mainType, "snowMoon"));
                AddBool(ref hash, ReadStaticBool(mainType, "raining"));
                AddBool(ref hash, ReadStaticBool(mainType, "xMas"));
                AddBool(ref hash, ReadStaticBool(mainType, "halloween"));
                AddBool(ref hash, ReadStaticBool(mainType, "notTheBeesWorld"));
                AddBool(ref hash, ReadStaticBool(mainType, "remixWorld"));
                AddBool(ref hash, ReadStaticBool(mainType, "tenthAnniversaryWorld"));
                hash = (hash * 397) ^ ReadStaticInt(mainType, "moonPhase", 0);
                if (player != null)
                {
                    AddBool(ref hash, ReadMemberBool(player, "ZoneSnow"));
                    AddBool(ref hash, ReadMemberBool(player, "ZoneJungle"));
                    AddBool(ref hash, ReadMemberBool(player, "ZoneDesert"));
                    AddBool(ref hash, ReadMemberBool(player, "ZoneHallow"));
                    AddBool(ref hash, ReadMemberBool(player, "ZoneCrimson"));
                    AddBool(ref hash, ReadMemberBool(player, "ZoneCorrupt"));
                    AddBool(ref hash, ReadMemberBool(player, "ZoneBeach"));
                    AddBool(ref hash, ReadMemberBool(player, "ZoneGlowshroom"));
                    AddBool(ref hash, ReadMemberBool(player, "ZoneGraveyard"));
                }

                return hash;
            }
        }

        private static void AddBool(ref int hash, bool value)
        {
            unchecked
            {
                hash = (hash * 397) ^ (value ? 1 : 0);
            }
        }

        private static bool TryFindShopDefinition(int shopIndex, out ShopDefinition definition)
        {
            for (var index = 0; index < ShopDefinitions.Length; index++)
            {
                if (ShopDefinitions[index].ShopIndex == shopIndex)
                {
                    definition = ShopDefinitions[index];
                    return true;
                }
            }

            definition = default(ShopDefinition);
            return false;
        }

        private static bool IsKnownConditionalShopItem(int shopIndex, int itemType)
        {
            for (var index = 0; index < KnownShopSources.Length; index++)
            {
                var hint = KnownShopSources[index];
                if (hint.ShopIndex == shopIndex && hint.ItemType == itemType && hint.IsConditionalInventory)
                {
                    return true;
                }
            }

            return false;
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
                SourceTag = source.SourceTag,
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

        private static ShopDefinition[] CreateShopDefinitions()
        {
            return new[]
            {
                new ShopDefinition(1, 17, "出售"),
                new ShopDefinition(2, 19, "出售"),
                new ShopDefinition(3, 20, "出售"),
                new ShopDefinition(4, 38, "出售"),
                new ShopDefinition(5, 54, "出售"),
                new ShopDefinition(6, 107, "出售"),
                new ShopDefinition(7, 108, "出售"),
                new ShopDefinition(8, 124, "出售"),
                new ShopDefinition(9, 142, "出售"),
                new ShopDefinition(10, 160, "出售"),
                new ShopDefinition(11, 178, "出售"),
                new ShopDefinition(12, 207, "出售"),
                new ShopDefinition(13, 208, "出售"),
                new ShopDefinition(14, 209, "出售"),
                new ShopDefinition(15, 227, "出售"),
                new ShopDefinition(16, 228, "出售"),
                new ShopDefinition(17, 229, "出售"),
                new ShopDefinition(18, 353, "出售"),
                new ShopDefinition(19, 368, "随机库存"),
                new ShopDefinition(20, 453, "出售"),
                new ShopDefinition(21, 550, "出售"),
                new ShopDefinition(22, 588, "出售"),
                new ShopDefinition(23, 633, "出售"),
                new ShopDefinition(24, 663, "出售"),
                new ShopDefinition(25, 227, "装饰")
            };
        }

        private static KnownShopSource[] CreateKnownShopSources()
        {
            return new[]
            {
                new KnownShopSource(1, 188, "可能出售 / 困难模式后出售", true),
                new KnownShopSource(1, 189, "可能出售 / 困难模式后出售", true),
                new KnownShopSource(1, 488, "可能出售 / 困难模式后出售", true),
                new KnownShopSource(1, 967, "可能出售 / 玩家位于雪原时出售", true),
                new KnownShopSource(1, 33, "可能出售 / 玩家位于丛林时出售", true),
                new KnownShopSource(1, 279, "可能出售 / 血月期间出售", true),
                new KnownShopSource(1, 282, "可能出售 / 夜晚出售", true),
                new KnownShopSource(1, 346, "可能出售 / 击败骷髅王后出售", true),
                new KnownShopSource(1, 931, "可能出售 / 玩家背包持有标尺相关物品时出售", true),
                new KnownShopSource(1, 1348, "可能出售 / 困难模式后出售", true),
                new KnownShopSource(1, 3198, "可能出售 / 困难模式后出售", true),
                new KnownShopSource(1, 4063, "可能出售 / Boss 进度或困难模式条件后出售", true),
                new KnownShopSource(2, 98, "常驻商品", false),
                new KnownShopSource(2, 324, "可能出售 / 夜晚出售", true),
                // Travel-shop and Skeleton Merchant random/context stock are static encyclopedia clues only.
                // They must not read or refresh the live random inventory, because that would mutate shop state.
                new KnownShopSource(19, 2260, "可能出售 / 游商随机出售 / 不代表本次到货", true),
                new KnownShopSource(20, 857, "可能出售 / 沙漠环境出售", true)
            };
        }

        private struct CacheKey : IEquatable<CacheKey>
        {
            public int ShopIndex;
            public int PlayerIndex;
            public int TalkNpc;
            public int NpcType;
            public long GameUpdateBucket;
            public int ContextSignature;

            public bool Equals(CacheKey other)
            {
                return ShopIndex == other.ShopIndex &&
                       PlayerIndex == other.PlayerIndex &&
                       TalkNpc == other.TalkNpc &&
                       NpcType == other.NpcType &&
                       GameUpdateBucket == other.GameUpdateBucket &&
                       ContextSignature == other.ContextSignature;
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
                    hash = (hash * 397) ^ GameUpdateBucket.GetHashCode();
                    hash = (hash * 397) ^ ContextSignature;
                    return hash;
                }
            }
        }

        private struct ShopDefinition
        {
            public readonly int ShopIndex;
            public readonly int NpcType;
            public readonly string EntryName;

            public ShopDefinition(int shopIndex, int npcType, string entryName)
            {
                ShopIndex = shopIndex;
                NpcType = npcType;
                EntryName = entryName ?? string.Empty;
            }
        }

        private struct KnownShopSource
        {
            public readonly int ShopIndex;
            public readonly int ItemType;
            public readonly string ConditionText;
            public readonly bool IsConditionalInventory;

            public KnownShopSource(int shopIndex, int itemType, string conditionText, bool isConditionalInventory)
            {
                ShopIndex = shopIndex;
                ItemType = itemType;
                ConditionText = conditionText ?? string.Empty;
                IsConditionalInventory = isConditionalInventory;
            }
        }
    }
}
