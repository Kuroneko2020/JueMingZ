using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Search
{
    internal static class ItemQueryService
    {
        public static ItemQueryResult BuildQuery(int itemType)
        {
            var result = new ItemQueryResult
            {
                ItemType = itemType,
                Status = "unknownItem"
            };

            ItemQueryReference item;
            if (itemType <= 0 || !ItemCatalogIndex.TryGet(itemType, out item))
            {
                return result;
            }

            result.Found = true;
            result.Status = "ok";
            result.Item = item;

            AddDistinctRange(result.AcquisitionSources, GetAcquisitionSources(itemType));
            AddRange(result.CraftingSources, ItemRecipeIndex.GetCraftingSources(itemType));
            AddRange(result.CraftingUses, ItemRecipeIndex.GetCraftingUses(itemType));
            result.Shimmer = ItemShimmerIndex.BuildSummary(itemType);
            return result;
        }

        public static IList<ItemQueryCandidate> ResolveCandidates(string query, int maxResults)
        {
            return ResolveCandidateQuery(query, maxResults).Candidates;
        }

        public static ItemQueryCandidateResult ResolveCandidateQuery(string query, int maxResults)
        {
            return ItemCatalogIndex.ResolveCandidateQuery(query, maxResults);
        }

        internal static void ResetForTesting()
        {
            ItemCatalogIndex.ResetForTesting();
            ItemRecipeIndex.ResetForTesting();
            ItemShimmerIndex.ResetForTesting();
            ItemNpcDropSourceIndex.ResetForTesting();
            ItemNpcShopSourceIndex.ResetForTesting();
            ItemContainerOpenSourceIndex.ResetForTesting();
            ItemFishingSourceIndex.ResetForTesting();
            ItemCuratedOtherSourceIndex.ResetForTesting();
            ItemWorldPlaceableSourceIndex.ResetForTesting();
            ItemAcquisitionTagIndex.ResetForTesting();
        }

        private static IEnumerable<ItemAcquisitionSourceSummary> GetAcquisitionSources(int itemType)
        {
            foreach (var source in ItemNpcDropSourceIndex.GetSources(itemType))
            {
                yield return source;
            }

            foreach (var source in ItemNpcShopSourceIndex.GetSources(itemType))
            {
                yield return source;
            }

            foreach (var source in ItemContainerOpenSourceIndex.GetSources(itemType))
            {
                yield return source;
            }

            foreach (var source in ItemFishingSourceIndex.GetSources(itemType))
            {
                yield return source;
            }

            foreach (var source in ItemCuratedOtherSourceIndex.GetSources(itemType))
            {
                yield return source;
            }

            foreach (var source in ItemWorldPlaceableSourceIndex.GetSources(itemType))
            {
                yield return source;
            }

            foreach (var source in ItemAcquisitionTagIndex.GetSources(itemType))
            {
                yield return source;
            }
        }

        private static void AddDistinctRange(
            IList<ItemAcquisitionSourceSummary> target,
            IEnumerable<ItemAcquisitionSourceSummary> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in source)
            {
                if (item == null)
                {
                    continue;
                }

                if (seen.Add(BuildAcquisitionSourceKey(item)))
                {
                    target.Add(item);
                }
            }
        }

        private static string BuildAcquisitionSourceKey(ItemAcquisitionSourceSummary source)
        {
            return string.Join(
                "\u001f",
                source.SourceType ?? string.Empty,
                source.SourceTag ?? string.Empty,
                source.Title ?? string.Empty,
                source.SourceName ?? string.Empty,
                source.QuantityText ?? string.Empty,
                source.ProbabilityText ?? string.Empty,
                source.ConditionText ?? string.Empty,
                source.ContextText ?? string.Empty,
                source.ItemType.ToString(System.Globalization.CultureInfo.InvariantCulture),
                source.NpcNetId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                source.RelatedItemType.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void AddRange<T>(IList<T> target, IEnumerable<T> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (var item in source)
            {
                target.Add(item);
            }
        }
    }
}
