using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Search
{
    internal static partial class ItemContainerOpenSourceIndex
    {
        private const string BossBagConditionText = "专家或大师模式";
        private const string BossBagContextText = "Boss 宝藏袋整理表，不展示随机概率";
        private const string ContainerOpenContextText = "开包整理表，不展示随机概率";
        private const string FishingCrateOpenContextText = "宝匣开包整理表，不表示宝匣可钓到的地点，不展示随机概率";

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
            var containers = CreateDefinitions();
            for (var containerIndex = 0; containerIndex < containers.Length; containerIndex++)
            {
                var container = containers[containerIndex];
                var containerItemType = ResolveItemType(itemIdType, container.ContainerFieldName, container.ContainerFallbackItemType);
                if (containerItemType <= 0)
                {
                    continue;
                }

                var sourceName = ResolveItemName(containerItemType, container.FallbackSourceName, container.ContainerFieldName);
                for (var outputIndex = 0; outputIndex < container.Outputs.Length; outputIndex++)
                {
                    var output = container.Outputs[outputIndex];
                    var outputItemType = ResolveItemType(itemIdType, output.OutputFieldName, output.OutputFallbackItemType);
                    if (outputItemType <= 0)
                    {
                        continue;
                    }

                    AddSource(result, outputItemType, container, output, containerItemType, sourceName);
                }
            }

            SortSources(result);
            return result;
        }

        private static ContainerDefinition[] CreateDefinitions()
        {
            var definitions = new List<ContainerDefinition>();
            AddDefinitions(definitions, CreateBossBagDefinitions());
            AddDefinitions(definitions, CreateFishingCrateDefinitions());
            AddDefinitions(definitions, CreateGenericOpenContainerDefinitions());
            return definitions.ToArray();
        }

        private static void AddDefinitions(List<ContainerDefinition> target, ContainerDefinition[] definitions)
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

        private static ContainerDefinition BossBag(string containerFieldName, int fallbackItemType, string fallbackSourceName, params OutputDefinition[] outputs)
        {
            return new ContainerDefinition(
                containerFieldName,
                fallbackItemType,
                fallbackSourceName,
                ItemAcquisitionSourceTags.TreasureBag,
                BossBagConditionText,
                BossBagContextText,
                outputs);
        }

        private static ContainerDefinition OpenContainer(string containerFieldName, int fallbackItemType, string fallbackSourceName, params OutputDefinition[] outputs)
        {
            return new ContainerDefinition(
                containerFieldName,
                fallbackItemType,
                fallbackSourceName,
                ItemAcquisitionSourceTags.ContainerOpen,
                string.Empty,
                ContainerOpenContextText,
                outputs);
        }

        private static ContainerDefinition FishingCrate(string containerFieldName, int fallbackItemType, string fallbackSourceName, params OutputDefinition[] outputs)
        {
            return new ContainerDefinition(
                containerFieldName,
                fallbackItemType,
                fallbackSourceName,
                ItemAcquisitionSourceTags.ContainerOpen,
                "宝匣开包内容",
                FishingCrateOpenContextText,
                outputs);
        }

        private static OutputDefinition Output(string outputFieldName, int fallbackItemType, string quantityText)
        {
            return new OutputDefinition(outputFieldName, fallbackItemType, quantityText);
        }

        private static int ResolveItemType(Type itemIdType, string fieldName, int fallback)
        {
            int itemType;
            return SearchReflection.TryConvertInt(SearchReflection.GetStaticMember(itemIdType, fieldName), out itemType) && itemType > 0
                ? itemType
                : fallback;
        }

        private static string ResolveItemName(int itemType, string fallbackName, string fallbackInternalName)
        {
            ItemQueryReference item;
            if (ItemCatalogIndex.TryGet(itemType, out item) && item != null)
            {
                if (!string.IsNullOrWhiteSpace(item.DisplayName))
                {
                    return item.DisplayName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(item.InternalName))
                {
                    return item.InternalName.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackName))
            {
                return fallbackName.Trim();
            }

            return string.IsNullOrWhiteSpace(fallbackInternalName) ? "开包物 #" + itemType.ToString(System.Globalization.CultureInfo.InvariantCulture) : fallbackInternalName.Trim();
        }

        private static void AddSource(
            Dictionary<int, List<ItemAcquisitionSourceSummary>> result,
            int outputItemType,
            ContainerDefinition container,
            OutputDefinition output,
            int containerItemType,
            string sourceName)
        {
            List<ItemAcquisitionSourceSummary> sources;
            if (!result.TryGetValue(outputItemType, out sources))
            {
                sources = new List<ItemAcquisitionSourceSummary>();
                result[outputItemType] = sources;
            }

            sources.Add(new ItemAcquisitionSourceSummary
            {
                SourceType = ItemAcquisitionSourceTypes.Other,
                SourceTag = container.SourceTag,
                Title = "可开出",
                SourceName = sourceName,
                QuantityText = output.QuantityText,
                ProbabilityText = string.Empty,
                ConditionText = container.ConditionText,
                ContextText = container.ContextText,
                ItemType = outputItemType,
                NpcNetId = -1,
                RelatedItemType = containerItemType
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

        private sealed class ContainerDefinition
        {
            public readonly string ContainerFieldName;
            public readonly int ContainerFallbackItemType;
            public readonly string FallbackSourceName;
            public readonly string SourceTag;
            public readonly string ConditionText;
            public readonly string ContextText;
            public readonly OutputDefinition[] Outputs;

            public ContainerDefinition(
                string containerFieldName,
                int containerFallbackItemType,
                string fallbackSourceName,
                string sourceTag,
                string conditionText,
                string contextText,
                OutputDefinition[] outputs)
            {
                ContainerFieldName = containerFieldName;
                ContainerFallbackItemType = containerFallbackItemType;
                FallbackSourceName = fallbackSourceName;
                SourceTag = sourceTag;
                ConditionText = conditionText;
                ContextText = contextText;
                Outputs = outputs ?? new OutputDefinition[0];
            }
        }

        private sealed class OutputDefinition
        {
            public readonly string OutputFieldName;
            public readonly int OutputFallbackItemType;
            public readonly string QuantityText;

            public OutputDefinition(string outputFieldName, int outputFallbackItemType, string quantityText)
            {
                OutputFieldName = outputFieldName;
                OutputFallbackItemType = outputFallbackItemType;
                QuantityText = quantityText;
            }
        }
    }
}
