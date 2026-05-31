using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Config;

namespace JueMingZ.Automation.Fishing.Filtering
{
    internal static class FishingFilterDecisionService
    {
        public static FishingFilterDecision Decide(AppSettings settings, FishingCatchCandidate candidate)
        {
            if (settings == null || candidate == null)
            {
                return Keep("unknownKeep", string.Empty, string.Empty, string.Empty);
            }

            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return Keep("filterDisabled", string.Empty, matchMode, filterMode);
            }

            var candidateKind = NormalizeCandidateKind(candidate.Kind);
            if (string.Equals(candidateKind, FishingCatchKinds.Unknown, StringComparison.OrdinalIgnoreCase) || candidate.Id <= 0)
            {
                return Keep("unknownCatchKeep", string.Empty, matchMode, filterMode);
            }

            var special = DecideSpecialRule(settings, candidate, matchMode, filterMode);
            if (special != null)
            {
                return special;
            }

            if (string.Equals(matchMode, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                return DecideKeyword(settings, candidate, filterMode, matchMode);
            }

            return DecideExact(settings, candidateKind, candidate.Id, filterMode, matchMode);
        }

        private static FishingFilterDecision DecideSpecialRule(
            AppSettings settings,
            FishingCatchCandidate candidate,
            string matchMode,
            string filterMode)
        {
            if (candidate.IsCrate)
            {
                var decision = DecideSpecialRule("crate", settings.FishingFilterCrateRule, matchMode, filterMode);
                if (decision != null)
                {
                    return decision;
                }
            }

            if (candidate.IsQuestFish)
            {
                var decision = DecideSpecialRule("questFish", settings.FishingFilterQuestFishRule, matchMode, filterMode);
                if (decision != null)
                {
                    return decision;
                }
            }

            if (candidate.IsEnemy)
            {
                var decision = DecideSpecialRule("enemy", settings.FishingFilterEnemyRule, matchMode, filterMode);
                if (decision != null)
                {
                    return decision;
                }
            }

            return null;
        }

        private static FishingFilterDecision DecideSpecialRule(string ruleName, string ruleValue, string matchMode, string filterMode)
        {
            var normalized = FishingFilterSpecialRuleModes.Normalize(ruleValue);
            var matchedRule = ruleName + ":" + normalized;
            if (string.Equals(normalized, FishingFilterSpecialRuleModes.Allow, StringComparison.OrdinalIgnoreCase))
            {
                return Keep(ruleName + "Allow", matchedRule, matchMode, filterMode);
            }

            if (string.Equals(normalized, FishingFilterSpecialRuleModes.Deny, StringComparison.OrdinalIgnoreCase))
            {
                return Skip(ruleName + "Deny", matchedRule, matchMode, filterMode);
            }

            return null;
        }

        private static FishingFilterDecision DecideExact(
            AppSettings settings,
            string candidateKind,
            int candidateId,
            string filterMode,
            string matchMode)
        {
            if (string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase))
            {
                string matchedRule;
                if (ContainsExact(settings.FishingFilterDenyExactEntries, candidateKind, candidateId, out matchedRule))
                {
                    return Skip("denyExactMatch", matchedRule, matchMode, filterMode);
                }

                return Keep("denyExactMiss", string.Empty, matchMode, filterMode);
            }

            string allowMatchedRule;
            if (ContainsExact(settings.FishingFilterAllowExactEntries, candidateKind, candidateId, out allowMatchedRule))
            {
                return Keep("allowExactMatch", allowMatchedRule, matchMode, filterMode);
            }

            return Skip("allowExactMiss", string.Empty, matchMode, filterMode);
        }

        private static FishingFilterDecision DecideKeyword(
            AppSettings settings,
            FishingCatchCandidate candidate,
            string filterMode,
            string matchMode)
        {
            var displayName = candidate.DisplayName ?? string.Empty;
            if (string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase))
            {
                string matchedRule;
                if (ContainsKeyword(settings.FishingFilterDenyKeywords, displayName, out matchedRule))
                {
                    return Skip("denyKeywordMatch", matchedRule, matchMode, filterMode);
                }

                return Keep("denyKeywordMiss", string.Empty, matchMode, filterMode);
            }

            string allowMatchedRule;
            if (ContainsKeyword(settings.FishingFilterAllowKeywords, displayName, out allowMatchedRule))
            {
                return Keep("allowKeywordMatch", allowMatchedRule, matchMode, filterMode);
            }

            return Skip("allowKeywordMiss", string.Empty, matchMode, filterMode);
        }

        private static bool ContainsExact(
            IList<FishingFilterExactEntry> entries,
            string candidateKind,
            int candidateId,
            out string matchedRule)
        {
            matchedRule = string.Empty;
            if (entries == null || string.IsNullOrEmpty(candidateKind) || candidateId <= 0)
            {
                return false;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || entry.Id != candidateId)
                {
                    continue;
                }

                var entryKind = NormalizeCandidateKind(entry.Kind);
                if (!string.Equals(entryKind, candidateKind, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matchedRule = entryKind + ":" + entry.Id.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            return false;
        }

        private static bool ContainsKeyword(IList<string> keywords, string displayName, out string matchedRule)
        {
            matchedRule = string.Empty;
            if (keywords == null || string.IsNullOrEmpty(displayName))
            {
                return false;
            }

            for (var index = 0; index < keywords.Count; index++)
            {
                var raw = keywords[index];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var keyword = raw.Trim();
                if (keyword.Length <= 0)
                {
                    continue;
                }

                if (displayName.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    continue;
                }

                matchedRule = keyword;
                return true;
            }

            return false;
        }

        private static string NormalizeCandidateKind(string kind)
        {
            if (string.Equals(kind, FishingCatchKinds.Item, StringComparison.OrdinalIgnoreCase))
            {
                return FishingCatchKinds.Item;
            }

            if (string.Equals(kind, FishingCatchKinds.NPC, StringComparison.OrdinalIgnoreCase))
            {
                return FishingCatchKinds.NPC;
            }

            return FishingCatchKinds.Unknown;
        }

        private static FishingFilterDecision Keep(string reason, string matchedRule, string matchMode, string filterMode)
        {
            return new FishingFilterDecision
            {
                ShouldKeep = true,
                Action = "Keep",
                Reason = reason ?? string.Empty,
                MatchedRule = matchedRule ?? string.Empty,
                MatchMode = matchMode ?? string.Empty,
                FilterMode = filterMode ?? string.Empty
            };
        }

        private static FishingFilterDecision Skip(string reason, string matchedRule, string matchMode, string filterMode)
        {
            return new FishingFilterDecision
            {
                ShouldKeep = false,
                Action = "Skip",
                Reason = reason ?? string.Empty,
                MatchedRule = matchedRule ?? string.Empty,
                MatchMode = matchMode ?? string.Empty,
                FilterMode = filterMode ?? string.Empty
            };
        }
    }
}
