using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Search
{
    internal static partial class ItemFishingSourceIndex
    {
        private const string FishingContextText = "整理钓鱼线索，不预测当前水域";
        private const string AnglerRewardContextText = "渔夫奖励整理表，不调用真实发奖励入口";

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
            AddDefinitions(result, itemIdType, CreateFishingDropDefinitions());
            AddDefinitions(result, itemIdType, CreateAnglerRewardDefinitions());
            SortSources(result);
            return result;
        }

        private static void AddDefinitions(
            Dictionary<int, List<ItemAcquisitionSourceSummary>> result,
            Type itemIdType,
            SourceDefinition[] definitions)
        {
            if (result == null || definitions == null)
            {
                return;
            }

            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                if (definition == null)
                {
                    continue;
                }

                var itemType = ResolveItemType(itemIdType, definition.ItemIdFieldName, definition.FallbackItemType);
                if (itemType <= 0)
                {
                    continue;
                }

                AddSource(result, itemType, definition);
            }
        }

        private static SourceDefinition Fish(
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
                ItemAcquisitionSourceTags.Fishing,
                title,
                sourceName,
                quantityText,
                conditionText,
                FishingContextText);
        }

        private static SourceDefinition QuestFish(
            string itemIdFieldName,
            int fallbackItemType,
            string sourceName,
            string conditionText)
        {
            return Fish(
                itemIdFieldName,
                fallbackItemType,
                "渔夫任务鱼",
                sourceName,
                "当日任务候选",
                conditionText + "；仅在渔夫指定任务日有任务价值");
        }

        private static SourceDefinition FishingCrate(
            string itemIdFieldName,
            int fallbackItemType,
            string sourceName,
            string conditionText)
        {
            return Fish(
                itemIdFieldName,
                fallbackItemType,
                "钓鱼宝匣",
                sourceName,
                "宝匣候选",
                conditionText + "；宝匣内容不在本标签展开");
        }

        private static SourceDefinition AnglerReward(
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
                ItemAcquisitionSourceTags.AnglerReward,
                title,
                sourceName,
                quantityText,
                conditionText,
                AnglerRewardContextText);
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
