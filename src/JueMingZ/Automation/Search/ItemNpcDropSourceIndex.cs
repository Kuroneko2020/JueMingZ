using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Automation.Search
{
    internal static class ItemNpcDropSourceIndex
    {
        private const int GlobalDropNpcNetId = 0;
        private const string GlobalDropSourceName = "任意 NPC";

        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static Dictionary<int, List<ItemAcquisitionSourceSummary>> _sourcesByItemType =
            new Dictionary<int, List<ItemAcquisitionSourceSummary>>();

        public static IList<ItemAcquisitionSourceSummary> GetSources(int itemType)
        {
            EnsureInitialized();

            List<ItemAcquisitionSourceSummary> sources;
            lock (SyncRoot)
            {
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
            var itemDropsDb = SearchReflection.GetStaticMember(SearchReflection.FindType("Terraria.Main"), "ItemDropsDB");
            if (itemDropsDb == null)
            {
                return result;
            }

            // Global drops are queried once and shown as a single broad source,
            // rather than duplicated onto every NPC row by includeGlobalDrops.
            AddNpcDropRules(result, itemDropsDb, GlobalDropNpcNetId, true);

            var range = ResolveNpcNetIdRange();
            for (var npcNetId = range.Start; npcNetId < range.EndExclusive; npcNetId++)
            {
                if (npcNetId == GlobalDropNpcNetId)
                {
                    continue;
                }

                AddNpcDropRules(result, itemDropsDb, npcNetId, false);
            }

            SortSources(result);
            return result;
        }

        private static void AddNpcDropRules(
            Dictionary<int, List<ItemAcquisitionSourceSummary>> result,
            object itemDropsDb,
            int npcNetId,
            bool includeGlobalDrops)
        {
            object rawRules;
            if (!SearchReflection.TryInvokeInstance(itemDropsDb, "GetRulesForNPCID", new object[] { npcNetId, includeGlobalDrops }, out rawRules))
            {
                return;
            }

            var rules = rawRules as IEnumerable;
            if (rules == null)
            {
                return;
            }

            var sourceName = npcNetId == GlobalDropNpcNetId ? GlobalDropSourceName : ResolveNpcName(npcNetId);
            foreach (var rule in rules)
            {
                ReportRuleDrops(result, rule, npcNetId, sourceName);
            }
        }

        private static void ReportRuleDrops(
            Dictionary<int, List<ItemAcquisitionSourceSummary>> result,
            object rule,
            int npcNetId,
            string sourceName)
        {
            if (rule == null)
            {
                return;
            }

            Type dropRateInfoType;
            Type chainFeedType;
            if (!TryResolveReportDropratesTypes(rule, out dropRateInfoType, out chainFeedType))
            {
                return;
            }

            object drops = null;
            object chainFeed = null;
            try
            {
                var listType = typeof(List<>).MakeGenericType(dropRateInfoType);
                drops = Activator.CreateInstance(listType);
                chainFeed = Activator.CreateInstance(chainFeedType, new object[] { 1f });
            }
            catch
            {
                return;
            }

            object ignored;
            if (!SearchReflection.TryInvokeInstance(rule, "ReportDroprates", new[] { drops, chainFeed }, out ignored))
            {
                return;
            }

            var enumerable = drops as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            foreach (var rawDrop in enumerable)
            {
                AddDrop(result, rawDrop, npcNetId, sourceName);
            }
        }

        private static bool TryResolveReportDropratesTypes(object rule, out Type dropRateInfoType, out Type chainFeedType)
        {
            dropRateInfoType = null;
            chainFeedType = null;
            if (rule == null)
            {
                return false;
            }

            try
            {
                var methods = rule.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, "ReportDroprates", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != 2)
                    {
                        continue;
                    }

                    var first = parameters[0].ParameterType;
                    if (!first.IsGenericType || first.GetGenericArguments().Length != 1)
                    {
                        continue;
                    }

                    dropRateInfoType = first.GetGenericArguments()[0];
                    chainFeedType = parameters[1].ParameterType;
                    return dropRateInfoType != null && chainFeedType != null;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void AddDrop(
            Dictionary<int, List<ItemAcquisitionSourceSummary>> result,
            object rawDrop,
            int npcNetId,
            string sourceName)
        {
            int itemType;
            if (!SearchReflection.TryReadInt(rawDrop, "itemId", out itemType) || itemType <= 0)
            {
                return;
            }

            int stackMin;
            int stackMax;
            SearchReflection.TryReadInt(rawDrop, "stackMin", out stackMin);
            SearchReflection.TryReadInt(rawDrop, "stackMax", out stackMax);

            float dropRate;
            TryReadFloat(rawDrop, "dropRate", out dropRate);

            var source = new ItemAcquisitionSourceSummary
            {
                SourceType = ItemAcquisitionSourceTypes.NpcDrop,
                Title = "NPC掉落",
                SourceName = sourceName,
                QuantityText = FormatQuantity(stackMin, stackMax),
                ProbabilityText = FormatProbability(dropRate),
                ConditionText = BuildConditionText(SearchReflection.GetMember(rawDrop, "conditions")),
                ContextText = npcNetId == GlobalDropNpcNetId ? "全局掉落规则" : string.Empty,
                ItemType = itemType,
                NpcNetId = npcNetId,
                RelatedItemType = itemType
            };

            List<ItemAcquisitionSourceSummary> sources;
            if (!result.TryGetValue(itemType, out sources))
            {
                sources = new List<ItemAcquisitionSourceSummary>();
                result[itemType] = sources;
            }

            if (!ContainsEquivalent(sources, source))
            {
                sources.Add(source);
            }
        }

        private static string ResolveNpcName(int npcNetId)
        {
            object raw;
            var langType = SearchReflection.FindType("Terraria.Lang");
            if (SearchReflection.TryInvokeStatic(langType, "GetNPCNameValue", new object[] { npcNetId }, out raw) && raw != null)
            {
                var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            var npcIdType = SearchReflection.FindType("Terraria.ID.NPCID");
            var search = SearchReflection.GetStaticMember(npcIdType, "Search");
            if (SearchReflection.TryInvokeInstance(search, "GetName", new object[] { npcNetId }, out raw) && raw != null)
            {
                var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return "NPC #" + npcNetId.ToString(CultureInfo.InvariantCulture);
        }

        private static NpcNetIdRange ResolveNpcNetIdRange()
        {
            var npcIdType = SearchReflection.FindType("Terraria.ID.NPCID");

            var start = -65;
            int negativeCount;
            if (SearchReflection.TryConvertInt(SearchReflection.GetStaticMember(npcIdType, "NegativeIDCount"), out negativeCount) && negativeCount < 0)
            {
                start = negativeCount + 1;
            }

            var end = 700;
            int count;
            if (SearchReflection.TryConvertInt(SearchReflection.GetStaticMember(npcIdType, "Count"), out count) && count > 0)
            {
                end = count;
            }

            return new NpcNetIdRange(start, end);
        }

        private static bool TryReadFloat(object instance, string name, out float value)
        {
            value = 0f;
            var raw = SearchReflection.GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatQuantity(int stackMin, int stackMax)
        {
            if (stackMin <= 0 && stackMax <= 0)
            {
                return string.Empty;
            }

            if (stackMax <= 0 || stackMax == stackMin)
            {
                return Math.Max(1, stackMin).ToString(CultureInfo.InvariantCulture) + "个";
            }

            return Math.Max(1, stackMin).ToString(CultureInfo.InvariantCulture) +
                   "-" +
                   stackMax.ToString(CultureInfo.InvariantCulture) +
                   "个";
        }

        private static string FormatProbability(float dropRate)
        {
            if (dropRate <= 0f)
            {
                return string.Empty;
            }

            var percent = dropRate * 100f;
            var text = percent >= 10f
                ? percent.ToString("0.##", CultureInfo.InvariantCulture)
                : percent.ToString("0.###", CultureInfo.InvariantCulture);
            return text + "%";
        }

        private static string BuildConditionText(object rawConditions)
        {
            var conditions = rawConditions as IEnumerable;
            if (conditions == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var condition in conditions)
            {
                var text = ResolveConditionText(condition);
                if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
                {
                    parts.Add(text);
                }
            }

            return string.Join("；", parts.ToArray());
        }

        private static string ResolveConditionText(object condition)
        {
            if (condition == null)
            {
                return string.Empty;
            }

            object raw;
            if (SearchReflection.TryInvokeInstance(condition, "CanShowItemDropInUI", new object[0], out raw))
            {
                bool canShow;
                if (SearchReflection.TryConvertBool(raw, out canShow) && !canShow)
                {
                    return "特殊条件";
                }
            }

            if (SearchReflection.TryInvokeInstance(condition, "GetConditionDescription", new object[0], out raw) && raw != null)
            {
                var description = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    return description.Trim();
                }
            }

            var typeName = condition.GetType().Name;
            return string.IsNullOrWhiteSpace(typeName) ? "特殊条件" : typeName;
        }

        private static bool ContainsEquivalent(IList<ItemAcquisitionSourceSummary> sources, ItemAcquisitionSourceSummary candidate)
        {
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (source.ItemType == candidate.ItemType &&
                    source.NpcNetId == candidate.NpcNetId &&
                    string.Equals(source.QuantityText, candidate.QuantityText, StringComparison.Ordinal) &&
                    string.Equals(source.ProbabilityText, candidate.ProbabilityText, StringComparison.Ordinal) &&
                    string.Equals(source.ConditionText, candidate.ConditionText, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

            var leftGlobal = left.NpcNetId == GlobalDropNpcNetId;
            var rightGlobal = right.NpcNetId == GlobalDropNpcNetId;
            if (leftGlobal != rightGlobal)
            {
                return leftGlobal ? 1 : -1;
            }

            var name = StringComparer.CurrentCultureIgnoreCase.Compare(left.SourceName ?? string.Empty, right.SourceName ?? string.Empty);
            return name != 0 ? name : left.NpcNetId.CompareTo(right.NpcNetId);
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

        private struct NpcNetIdRange
        {
            public readonly int Start;
            public readonly int EndExclusive;

            public NpcNetIdRange(int start, int endExclusive)
            {
                Start = start;
                EndExclusive = endExclusive;
            }
        }
    }
}
