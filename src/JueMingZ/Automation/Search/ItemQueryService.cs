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

            AddRange(result.CraftingSources, ItemRecipeIndex.GetCraftingSources(itemType));
            AddRange(result.CraftingUses, ItemRecipeIndex.GetCraftingUses(itemType));
            result.Shimmer = ItemShimmerIndex.BuildSummary(itemType);
            return result;
        }

        public static IList<ItemQueryCandidate> ResolveCandidates(string query, int maxResults)
        {
            return ItemCatalogIndex.ResolveCandidates(query, maxResults);
        }

        internal static void ResetForTesting()
        {
            ItemCatalogIndex.ResetForTesting();
            ItemRecipeIndex.ResetForTesting();
            ItemShimmerIndex.ResetForTesting();
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
