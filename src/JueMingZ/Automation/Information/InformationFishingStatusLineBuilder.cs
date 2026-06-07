using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Information
{
    internal static class InformationFishingStatusLineBuilder
    {
        internal static string BuildFilterStatusSignature(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return FishingFilterModes.Normalize(settings.FishingFilterMode) + "|" +
                   FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode) + "|" +
                   FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterCrateRule) + "|" +
                   FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterQuestFishRule) + "|" +
                   FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterEnemyRule) + "|" +
                   BuildFilterListsHash(settings);
        }

        internal static IList<FishingCatchCandidate> ResolveFishingCatchCandidates(
            InformationWorldContext context,
            float bobberX,
            float bobberY,
            int bobberIdentity,
            string filterSignature,
            out string message)
        {
            try
            {
                // Status panel display is read-only; action submission stays in
                // Fishing automation and filter services, outside Information.
                return InformationFishingCatchResolver.ResolveCatchCandidates(
                    context,
                    bobberX,
                    bobberY,
                    bobberIdentity,
                    filterSignature,
                    out message);
            }
            catch (Exception error)
            {
                Logger.Debug("InformationOverlay", "Fishing catch resolution failed: " + error);
                message = "鱼获解析失败";
                return new List<FishingCatchCandidate>();
            }
        }

        internal static void AddFishingCatchLines(
            ICollection<InformationStatusLine> lines,
            int order,
            bool hasBobber,
            IList<FishingCatchCandidate> candidates,
            string message,
            InformationColor color,
            double fontScale)
        {
            if (!hasBobber)
            {
                return;
            }

            var names = BuildFishingCatchNames(candidates);
            if (names.Count <= 0)
            {
                InformationStatusLineService.AddLine(lines, order, "完整鱼获: " + FirstNonEmpty(message, "暂无可解析鱼获"), color, fontScale);
                return;
            }

            AddFishingCatchNameLines(lines, order, "完整鱼获: ", names, color, fontScale);
        }

        internal static void AddFilteredFishingCatchLines(
            ICollection<InformationStatusLine> lines,
            int order,
            AppSettings settings,
            bool hasBobber,
            bool sonarBuffActive,
            IList<FishingCatchCandidate> candidates,
            string message,
            InformationColor color,
            double fontScale)
        {
            if (!hasBobber)
            {
                return;
            }

            if (IsFishingFilterDisabled(settings))
            {
                InformationStatusLineService.AddLine(lines, order, "过滤鱼获: 过滤未启用", color, fontScale);
                return;
            }

            if (!sonarBuffActive)
            {
                InformationStatusLineService.AddLine(lines, order, "过滤鱼获: 需要声呐药水", color, fontScale);
                return;
            }

            if (candidates == null || candidates.Count <= 0)
            {
                InformationStatusLineService.AddLine(lines, order, "过滤鱼获: " + FirstNonEmpty(message, "暂无可解析鱼获"), color, fontScale);
                return;
            }

            var names = new List<string>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                FishingFilterDecision decision;
                try
                {
                    decision = FishingFilterDecisionService.Decide(settings, candidate);
                }
                catch
                {
                    decision = null;
                }

                if (decision == null || !decision.ShouldKeep)
                {
                    continue;
                }

                var name = candidate == null ? string.Empty : candidate.DisplayName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            if (names.Count <= 0)
            {
                InformationStatusLineService.AddLine(lines, order, "过滤鱼获: 无匹配鱼获", color, fontScale);
                return;
            }

            AddFishingCatchNameLines(lines, order, "过滤鱼获: ", names, color, fontScale);
        }

        internal static bool IsFishingFilterDisabled(AppSettings settings)
        {
            return string.Equals(
                FishingFilterModes.Normalize(settings == null ? null : settings.FishingFilterMode),
                FishingFilterModes.Disabled,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFilterListsHash(AppSettings settings)
        {
            unchecked
            {
                uint hash = 2166136261u;
                AddExactListHash(ref hash, settings == null ? null : settings.FishingFilterAllowExactEntries);
                AddExactListHash(ref hash, settings == null ? null : settings.FishingFilterDenyExactEntries);
                AddKeywordListHash(ref hash, settings == null ? null : settings.FishingFilterAllowKeywords);
                AddKeywordListHash(ref hash, settings == null ? null : settings.FishingFilterDenyKeywords);
                return hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private static void AddExactListHash(ref uint hash, IList<FishingFilterExactEntry> entries)
        {
            AddHashValue(ref hash, "exact");
            if (entries == null)
            {
                AddHashValue(ref hash, "<null>");
                return;
            }

            AddHashValue(ref hash, entries.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                AddHashValue(ref hash, entry == null ? "<null>" : entry.Kind);
                AddHashValue(ref hash, entry == null ? string.Empty : entry.Id.ToString(CultureInfo.InvariantCulture));
                AddHashValue(ref hash, entry == null ? string.Empty : entry.DisplayNameSnapshot);
            }
        }

        private static void AddKeywordListHash(ref uint hash, IList<string> keywords)
        {
            AddHashValue(ref hash, "keyword");
            if (keywords == null)
            {
                AddHashValue(ref hash, "<null>");
                return;
            }

            AddHashValue(ref hash, keywords.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < keywords.Count; index++)
            {
                AddHashValue(ref hash, keywords[index]);
            }
        }

        private static void AddHashValue(ref uint hash, string value)
        {
            unchecked
            {
                value = value ?? string.Empty;
                for (var index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }

                hash ^= 31u;
                hash *= 16777619u;
            }
        }

        private static List<string> BuildFishingCatchNames(IList<FishingCatchCandidate> candidates)
        {
            var names = new List<string>();
            if (candidates == null)
            {
                return names;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                var name = candidates[index] == null ? string.Empty : candidates[index].DisplayName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static void AddFishingCatchNameLines(
            ICollection<InformationStatusLine> lines,
            int order,
            string prefix,
            IList<string> names,
            InformationColor color,
            double fontScale)
        {
            const int maxCharsPerLine = 38;
            var current = prefix;
            var lineIndex = 0;
            for (var index = 0; index < names.Count; index++)
            {
                var name = names[index];
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var separator = string.Equals(current, prefix, StringComparison.Ordinal) || string.Equals(current, "  ", StringComparison.Ordinal)
                    ? string.Empty
                    : "、";
                var candidate = current + separator + name;
                if (candidate.Length > maxCharsPerLine &&
                    !string.Equals(current, prefix, StringComparison.Ordinal) &&
                    !string.Equals(current, "  ", StringComparison.Ordinal))
                {
                    InformationStatusLineService.AddLine(lines, order + lineIndex, current, color, fontScale);
                    lineIndex++;
                    current = "  " + name;
                }
                else
                {
                    current = candidate;
                }
            }

            if (!string.IsNullOrWhiteSpace(current) &&
                !string.Equals(current, prefix, StringComparison.Ordinal) &&
                !string.Equals(current, "  ", StringComparison.Ordinal))
            {
                InformationStatusLineService.AddLine(lines, order + lineIndex, current, color, fontScale);
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index];
                }
            }

            return string.Empty;
        }
    }
}
