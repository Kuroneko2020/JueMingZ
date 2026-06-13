using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Search
{
    internal static partial class ItemCuratedOtherSourceIndex
    {
        private const string ChestContextText = "整理宝箱线索，不扫描当前世界";
        private const string WorldContextText = "整理世界生成线索，不扫描当前世界";
        private const string ExtractinatorContextText = "整理提炼机候选，不调用真实提炼，不展示随机概率";

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

        private static SourceDefinition[] CreateDefinitions()
        {
            var definitions = new List<SourceDefinition>();
            AddDefinitions(definitions, CreateChestDefinitions());
            AddDefinitions(definitions, CreateWorldGenerationDefinitions());
            AddDefinitions(definitions, CreateExtractinatorDefinitions());
            return definitions.ToArray();
        }

        private static void AddDefinitions(List<SourceDefinition> target, SourceDefinition[] definitions)
        {
            if (target == null || definitions == null)
            {
                return;
            }

            for (var index = 0; index < definitions.Length; index++)
            {
                if (definitions[index] != null)
                {
                    target.Add(definitions[index]);
                }
            }
        }

        private static SourceDefinition Chest(
            string itemIdFieldName,
            int fallbackItemType,
            string title,
            string sourceName,
            string quantityText,
            string conditionText)
        {
            return new SourceDefinition(
                itemIdFieldName,
                fallbackItemType,
                ItemAcquisitionSourceTags.Chest,
                title,
                sourceName,
                quantityText,
                conditionText,
                ChestContextText);
        }

        private static SourceDefinition World(
            string itemIdFieldName,
            int fallbackItemType,
            string title,
            string sourceName,
            string quantityText,
            string conditionText)
        {
            return new SourceDefinition(
                itemIdFieldName,
                fallbackItemType,
                ItemAcquisitionSourceTags.WorldGeneration,
                title,
                sourceName,
                quantityText,
                conditionText,
                WorldContextText);
        }

        private static SourceDefinition Extractinator(
            string itemIdFieldName,
            int fallbackItemType,
            string title,
            string sourceName,
            string quantityText,
            string conditionText)
        {
            return new SourceDefinition(
                itemIdFieldName,
                fallbackItemType,
                ItemAcquisitionSourceTags.Extractinator,
                title,
                sourceName,
                quantityText,
                conditionText,
                ExtractinatorContextText);
        }

        private static SourceDefinition ExtractinatorInput(
            string itemIdFieldName,
            int fallbackItemType,
            string sourceName,
            string conditionText)
        {
            return new SourceDefinition(
                itemIdFieldName,
                fallbackItemType,
                ItemAcquisitionSourceTags.Extractinator,
                "提炼输入材料",
                sourceName,
                "输入物候选",
                conditionText,
                ExtractinatorContextText);
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
            SourceDefinition definition)
        {
            List<ItemAcquisitionSourceSummary> sources;
            if (!result.TryGetValue(itemType, out sources))
            {
                sources = new List<ItemAcquisitionSourceSummary>();
                result[itemType] = sources;
            }

            sources.Add(new ItemAcquisitionSourceSummary
            {
                SourceType = ItemAcquisitionSourceTypes.Other,
                SourceTag = definition.SourceTag,
                Title = definition.Title,
                SourceName = definition.SourceName,
                QuantityText = definition.QuantityText,
                ProbabilityText = string.Empty,
                ConditionText = definition.ConditionText,
                ContextText = definition.ContextText,
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

            var tag = StringComparer.CurrentCultureIgnoreCase.Compare(left.SourceTag ?? string.Empty, right.SourceTag ?? string.Empty);
            if (tag != 0)
            {
                return tag;
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

        private sealed class SourceDefinition
        {
            public readonly string ItemIdFieldName;
            public readonly int FallbackItemType;
            public readonly string SourceTag;
            public readonly string Title;
            public readonly string SourceName;
            public readonly string QuantityText;
            public readonly string ConditionText;
            public readonly string ContextText;

            public SourceDefinition(
                string itemIdFieldName,
                int fallbackItemType,
                string sourceTag,
                string title,
                string sourceName,
                string quantityText,
                string conditionText,
                string contextText)
            {
                ItemIdFieldName = itemIdFieldName;
                FallbackItemType = fallbackItemType;
                SourceTag = sourceTag;
                Title = title;
                SourceName = sourceName;
                QuantityText = quantityText;
                ConditionText = conditionText;
                ContextText = contextText;
            }
        }
    }
}
