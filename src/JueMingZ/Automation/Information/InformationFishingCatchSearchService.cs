using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;

namespace JueMingZ.Automation.Information
{
    internal static class InformationFishingCatchSearchService
    {
        public static IList<FishingCatchCandidate> ResolveGlobalFishableItemCandidates(
            FishingSearchRequest request,
            out bool truncated,
            out string message)
        {
            // This feeds only the fishing filter search UI. It must not scan the
            // current bobber water, read FishingConditions, or touch catch cache.
            truncated = false;
            message = string.Empty;
            var candidates = new List<FishingCatchCandidate>();
            var searchText = string.IsNullOrWhiteSpace(request.Query) ? string.Empty : request.Query.Trim();
            if (searchText.Length <= 0)
            {
                message = "请输入名称或 ID 搜索全游戏可钓鱼获";
                return candidates;
            }

            if (request.Context == null || request.Context.MainType == null)
            {
                message = "环境不可用";
                return candidates;
            }

            IList rules;
            if (!InformationFishDropRuleEvaluator.TryGetFishDropRules(request.Context, out rules) || rules == null || rules.Count <= 0)
            {
                message = "鱼获规则不可用";
                return candidates;
            }

            var itemIds = new List<int>();
            var seen = new HashSet<int>();
            for (var index = 0; index < rules.Count; index++)
            {
                InformationFishDropRuleEvaluator.AddPossibleItemsUnbounded(rules[index], itemIds, seen);
            }

            var questFishIds = InformationFishingItemNameResolver.ReadAnglerQuestFishIds(request.Context);
            foreach (var questFishId in questFishIds)
            {
                InformationFishDropRuleEvaluator.AddItemIdUnbounded(questFishId, itemIds, seen);
            }

            if (itemIds.Count <= 0)
            {
                message = "暂无全局可钓鱼获索引";
                return candidates;
            }

            int searchItemId;
            var hasItemIdSearch = TryParseSearchItemId(searchText, out searchItemId);
            for (var index = 0; index < itemIds.Count; index++)
            {
                var itemId = itemIds[index];
                if (itemId <= 0)
                {
                    continue;
                }

                var displayName = InformationFishingItemNameResolver.ResolveItemName(itemId);
                var internalName = InformationFishingItemNameResolver.ResolveInternalItemName(itemId);
                if (!MatchesGlobalSearch(itemId, displayName, internalName, searchText, hasItemIdSearch, searchItemId))
                {
                    continue;
                }

                candidates.Add(InformationFishingItemNameResolver.CreateGlobalSearchCandidate(itemId, displayName, questFishIds));
            }

            InformationFishingEnemyCandidateResolver.AddMatchingFishableEnemyCandidates(candidates, searchText, hasItemIdSearch, searchItemId);
            candidates.Sort(InformationFishingItemNameResolver.CompareCandidates);
            if (request.MaxResults > 0 && candidates.Count > request.MaxResults)
            {
                truncated = true;
                candidates.RemoveRange(request.MaxResults, candidates.Count - request.MaxResults);
            }

            if (candidates.Count <= 0)
            {
                message = "无匹配鱼获";
            }
            else if (truncated)
            {
                message = "结果较多，请继续输入缩小范围";
            }

            return candidates;
        }

        internal static bool MatchesGlobalSearch(
            int itemId,
            string displayName,
            string internalName,
            string query,
            bool hasItemIdSearch,
            int searchItemId)
        {
            // Keep ID, display-name, and internal-name matching distinct; the UI
            // truncation message depends on this cheap predicate staying cheap.
            if (hasItemIdSearch && itemId == searchItemId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(displayName) &&
                displayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(internalName) &&
                   internalName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool TryParseSearchItemId(string query, out int itemId)
        {
            itemId = 0;
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            var text = query.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                text = text.Substring(1).Trim();
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId) &&
                   itemId > 0;
        }
    }
}
