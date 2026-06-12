using System;
using System.Collections.Generic;
using JueMingZ.Automation.Search;

namespace JueMingZ.Automation.Search.ChestLocator
{
    internal static class ChestItemLocatorQueryResolver
    {
        public const int DefaultCandidateLimit = 24;
        public const int MaxCandidateLimit = 24;

        public static ChestItemLocatorQueryResult Resolve(string queryText)
        {
            return Resolve(queryText, DefaultCandidateLimit);
        }

        public static ChestItemLocatorQueryResult Resolve(string queryText, int maxCandidates)
        {
            var limit = NormalizeCandidateLimit(maxCandidates);
            var query = new ChestItemLocatorQuery(queryText, limit);
            if (query.NormalizedText.Length <= 0)
            {
                return ChestItemLocatorQueryResult.Failed(query, ChestItemLocatorQueryResult.StatusEmptyInput);
            }

            int parsedItemType;
            var isItemIdQuery = ItemCatalogIndex.TryParseItemType(query.NormalizedText, out parsedItemType);
            var rawCandidates = ItemQueryService.ResolveCandidates(query.NormalizedText, limit + 1);
            if (rawCandidates == null || rawCandidates.Count <= 0)
            {
                return ChestItemLocatorQueryResult.Failed(
                    query,
                    isItemIdQuery
                        ? ChestItemLocatorQueryResult.StatusUnknownItemId
                        : ChestItemLocatorQueryResult.StatusNoMatch);
            }

            var truncated = rawCandidates.Count > limit;
            var candidates = CopyCandidates(rawCandidates, limit);
            return truncated
                ? ChestItemLocatorQueryResult.TooManyCandidates(query, candidates)
                : ChestItemLocatorQueryResult.Success(query, candidates);
        }

        internal static int NormalizeCandidateLimit(int maxCandidates)
        {
            if (maxCandidates <= 0)
            {
                return DefaultCandidateLimit;
            }

            return Math.Min(maxCandidates, MaxCandidateLimit);
        }

        internal static void ResetForTesting()
        {
            ItemQueryService.ResetForTesting();
        }

        private static IList<ChestItemLocatorCandidate> CopyCandidates(
            IList<ItemQueryCandidate> rawCandidates,
            int limit)
        {
            var result = new List<ChestItemLocatorCandidate>();
            if (rawCandidates == null || limit <= 0)
            {
                return result;
            }

            var count = Math.Min(rawCandidates.Count, limit);
            for (var index = 0; index < count; index++)
            {
                var raw = rawCandidates[index];
                if (raw == null || raw.ItemType <= 0)
                {
                    continue;
                }

                result.Add(new ChestItemLocatorCandidate(raw.ItemType, raw.DisplayName, raw.InternalName));
            }

            return result;
        }
    }
}
