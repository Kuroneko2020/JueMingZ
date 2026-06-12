using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Search
{
    internal static class ItemAcquisitionTagIndex
    {
        private const string StaticTagContext = "本地标签，非完整百科";

        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static Dictionary<int, List<ItemAcquisitionSourceSummary>> _sourcesByItemType =
            new Dictionary<int, List<ItemAcquisitionSourceSummary>>();

        public static IList<ItemAcquisitionSourceSummary> GetSources(int itemType)
        {
            EnsureInitialized();

            lock (SyncRoot)
            {
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
                _initialized = false;
                _sourcesByItemType = new Dictionary<int, List<ItemAcquisitionSourceSummary>>();
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

                _sourcesByItemType = BuildIndex();
                _initialized = true;
            }
        }

        private static Dictionary<int, List<ItemAcquisitionSourceSummary>> BuildIndex()
        {
            var result = new Dictionary<int, List<ItemAcquisitionSourceSummary>>();
            var itemIdType = SearchReflection.FindType("Terraria.ID.ItemID");
            var definitions = CreateDefinitions();
            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                var itemType = ResolveItemType(itemIdType, definition.ItemIdFieldName, definition.FallbackItemType);
                if (itemType <= 0)
                {
                    continue;
                }

                AddSource(result, itemType, definition);
            }

            SortSources(result);
            return result;
        }

        private static TagDefinition[] CreateDefinitions()
        {
            return new[]
            {
                Ore("CopperOre", 12, "基础矿脉", "地下/洞穴层常见，可用镐挖掘；不同世界可能生成替代矿"),
                Ore("TinOre", 699, "基础矿脉", "地下/洞穴层常见，可用镐挖掘；不同世界可能生成替代矿"),
                Ore("IronOre", 11, "基础矿脉", "地下/洞穴层常见，可用镐挖掘；不同世界可能生成替代矿"),
                Ore("LeadOre", 700, "基础矿脉", "地下/洞穴层常见，可用镐挖掘；不同世界可能生成替代矿"),
                Ore("SilverOre", 14, "基础矿脉", "地下/洞穴层常见，可用镐挖掘；不同世界可能生成替代矿"),
                Ore("TungstenOre", 701, "基础矿脉", "地下/洞穴层常见，可用镐挖掘；不同世界可能生成替代矿"),
                Ore("GoldOre", 13, "基础矿脉", "地下/洞穴层常见，可用镐挖掘；不同世界可能生成替代矿"),
                Ore("PlatinumOre", 702, "基础矿脉", "地下/洞穴层常见，可用镐挖掘；不同世界可能生成替代矿"),
                Ore("DemoniteOre", 56, "邪恶矿脉", "腐化世界相关来源；Boss/事件也可能产出，本文只标常见采集线索"),
                Ore("CrimtaneOre", 880, "邪恶矿脉", "猩红世界相关来源；Boss/事件也可能产出，本文只标常见采集线索"),
                Ore("Meteorite", 116, "陨石矿脉", "陨石落地后可在地表附近挖掘，生成条件不在本标签表穷举"),
                Ore("Obsidian", 193, "黑曜石", "水和熔岩接触后形成，可用镐采集"),
                Ore("Hellstone", 174, "地狱矿脉", "地狱层常见，需要合适镐力采集"),
                Ore("CobaltOre", 364, "困难模式矿脉", "困难模式祭坛/世界转换后出现；同级矿物可能由替代矿生成"),
                Ore("PalladiumOre", 1104, "困难模式矿脉", "困难模式祭坛/世界转换后出现；同级矿物可能由替代矿生成"),
                Ore("MythrilOre", 365, "困难模式矿脉", "困难模式祭坛/世界转换后出现；同级矿物可能由替代矿生成"),
                Ore("OrichalcumOre", 1105, "困难模式矿脉", "困难模式祭坛/世界转换后出现；同级矿物可能由替代矿生成"),
                Ore("AdamantiteOre", 366, "困难模式矿脉", "困难模式祭坛/世界转换后出现；同级矿物可能由替代矿生成"),
                Ore("TitaniumOre", 1106, "困难模式矿脉", "困难模式祭坛/世界转换后出现；同级矿物可能由替代矿生成"),
                Ore("ChlorophyteOre", 947, "叶绿矿脉", "困难模式地下丛林会扩散生成，需要合适镐力采集"),
                Ore("FossilOre", 3347, "沙漠化石", "地下沙漠常见，可挖掘采集；提炼产物不在本阶段展示"),

                Gem("Amethyst", 181, "紫晶", "地下宝石脉或宝石洞常见，可用镐采集"),
                Gem("Topaz", 180, "黄玉", "地下宝石脉或宝石洞常见，可用镐采集"),
                Gem("Sapphire", 177, "蓝玉", "地下宝石脉或宝石洞常见，可用镐采集"),
                Gem("Emerald", 179, "翡翠", "地下宝石脉或宝石洞常见，可用镐采集"),
                Gem("Ruby", 178, "红玉", "地下宝石脉或宝石洞常见，可用镐采集"),
                Gem("Diamond", 182, "钻石", "地下宝石脉或宝石洞常见，可用镐采集"),
                Gem("Amber", 999, "琥珀", "地下沙漠/沙漠相关来源常见；不展示完整概率"),

                Herb("Daybloom", 313, "太阳花", "森林/草地白天开花，可自然采集"),
                Herb("Moonglow", 314, "月光草", "丛林夜晚开花，可自然采集"),
                Herb("Blinkroot", 315, "闪耀根", "地下土石环境随机开花，可自然采集"),
                Herb("Deathweed", 316, "死亡草", "腐化/猩红环境，血月或满月开花，可自然采集"),
                Herb("Waterleaf", 317, "幌菊", "沙漠雨天开花，可自然采集"),
                Herb("Fireblossom", 318, "火焰花", "地狱层黄昏开花，可自然采集"),
                Herb("Shiverthorn", 2358, "寒颤棘", "雪地环境自然生长，可自然采集"),

                Block("DirtBlock", 2, "泥土", "地表/地下常见环境块，可挖掘"),
                Block("StoneBlock", 3, "石块", "地下/洞穴层常见环境块，可挖掘"),
                Block("ClayBlock", 124, "黏土", "地表浅层和水边常见环境块，可挖掘"),
                Block("MudBlock", 176, "泥块", "丛林/地下丛林常见环境块，可挖掘"),
                Block("SandBlock", 169, "沙块", "沙漠/海滩常见环境块，可挖掘"),
                Block("SiltBlock", 424, "泥沙块", "地下常见重力块，可挖掘；提炼产物不在本阶段展示"),
                Block("SlushBlock", 1103, "雪泥块", "雪地地下常见重力块，可挖掘；提炼产物不在本阶段展示"),
                Block("SnowBlock", 593, "雪块", "雪地常见环境块，可挖掘"),
                Block("IceBlock", 664, "冰雪块", "雪地/地下雪地常见环境块，可挖掘")
            };
        }

        private static TagDefinition Ore(string itemIdFieldName, int fallbackItemType, string sourceName, string conditionText)
        {
            return new TagDefinition(itemIdFieldName, fallbackItemType, "常见挖掘", sourceName, "常见来源", conditionText);
        }

        private static TagDefinition Gem(string itemIdFieldName, int fallbackItemType, string sourceName, string conditionText)
        {
            return new TagDefinition(itemIdFieldName, fallbackItemType, "常见挖掘", sourceName, "常见来源", conditionText);
        }

        private static TagDefinition Herb(string itemIdFieldName, int fallbackItemType, string sourceName, string conditionText)
        {
            return new TagDefinition(itemIdFieldName, fallbackItemType, "常见采集", sourceName, "自然采集", conditionText);
        }

        private static TagDefinition Block(string itemIdFieldName, int fallbackItemType, string sourceName, string conditionText)
        {
            return new TagDefinition(itemIdFieldName, fallbackItemType, "常见挖掘", sourceName, "常见来源", conditionText);
        }

        private static int ResolveItemType(Type itemIdType, string fieldName, int fallback)
        {
            int itemType;
            return SearchReflection.TryConvertInt(SearchReflection.GetStaticMember(itemIdType, fieldName), out itemType) && itemType > 0
                ? itemType
                : fallback;
        }

        private static void AddSource(
            Dictionary<int, List<ItemAcquisitionSourceSummary>> result,
            int itemType,
            TagDefinition definition)
        {
            List<ItemAcquisitionSourceSummary> sources;
            if (!result.TryGetValue(itemType, out sources))
            {
                sources = new List<ItemAcquisitionSourceSummary>();
                result[itemType] = sources;
            }

            sources.Add(new ItemAcquisitionSourceSummary
            {
                SourceType = ItemAcquisitionSourceTypes.MiningGatheringTag,
                Title = definition.Title,
                SourceName = definition.SourceName,
                QuantityText = definition.QuantityText,
                ProbabilityText = string.Empty,
                ConditionText = definition.ConditionText,
                ContextText = StaticTagContext,
                ItemType = itemType,
                NpcNetId = -1,
                RelatedItemType = itemType
            });
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

            var title = StringComparer.CurrentCultureIgnoreCase.Compare(left.Title ?? string.Empty, right.Title ?? string.Empty);
            if (title != 0)
            {
                return title;
            }

            return StringComparer.CurrentCultureIgnoreCase.Compare(left.SourceName ?? string.Empty, right.SourceName ?? string.Empty);
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

        private sealed class TagDefinition
        {
            public readonly string ItemIdFieldName;
            public readonly int FallbackItemType;
            public readonly string Title;
            public readonly string SourceName;
            public readonly string QuantityText;
            public readonly string ConditionText;

            public TagDefinition(
                string itemIdFieldName,
                int fallbackItemType,
                string title,
                string sourceName,
                string quantityText,
                string conditionText)
            {
                ItemIdFieldName = itemIdFieldName;
                FallbackItemType = fallbackItemType;
                Title = title;
                SourceName = sourceName;
                QuantityText = quantityText;
                ConditionText = conditionText;
            }
        }
    }
}
