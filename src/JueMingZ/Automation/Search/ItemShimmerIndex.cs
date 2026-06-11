using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Search
{
    internal static class ItemShimmerIndex
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static Dictionary<int, int> _forwardBySource = new Dictionary<int, int>();
        private static Dictionary<int, List<int>> _reverseSourcesByTarget = new Dictionary<int, List<int>>();

        public static ItemQueryShimmerSummary BuildSummary(int itemType)
        {
            EnsureInitialized();

            int forward;
            List<int> reverseSources;
            lock (SyncRoot)
            {
                _forwardBySource.TryGetValue(itemType, out forward);
                if (!_reverseSourcesByTarget.TryGetValue(itemType, out reverseSources))
                {
                    reverseSources = new List<int>();
                }
                else
                {
                    reverseSources = new List<int>(reverseSources);
                }
            }

            var summary = new ItemQueryShimmerSummary();
            if (forward > 0)
            {
                summary.ForwardResult = ItemCatalogIndex.CreateReference(forward, 0);
            }

            for (var index = 0; index < reverseSources.Count; index++)
            {
                var reference = ItemCatalogIndex.CreateReference(reverseSources[index], 0);
                if (reference != null)
                {
                    summary.ReverseSources.Add(reference);
                }
            }

            return summary;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _initialized = false;
                _forwardBySource = new Dictionary<int, int>();
                _reverseSourcesByTarget = new Dictionary<int, List<int>>();
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

                var forward = new Dictionary<int, int>();
                var reverse = new Dictionary<int, List<int>>();
                try
                {
                    BuildIndexes(forward, reverse);
                }
                catch
                {
                    forward.Clear();
                    reverse.Clear();
                }

                _forwardBySource = forward;
                _reverseSourcesByTarget = reverse;
                _initialized = true;
            }
        }

        private static void BuildIndexes(Dictionary<int, int> forward, Dictionary<int, List<int>> reverse)
        {
            var itemSetsType = SearchReflection.FindType("Terraria.ID.ItemID+Sets");
            var shimmerTransforms = SearchReflection.GetStaticMember(itemSetsType, "ShimmerTransformToItem");
            var count = SearchReflection.GetCollectionCount(shimmerTransforms);
            for (var source = 1; source < count; source++)
            {
                var rawTarget = SearchReflection.GetIndexedValue(shimmerTransforms, source);
                int target;
                if (!SearchReflection.TryConvertInt(rawTarget, out target) || target <= 0 || target == source)
                {
                    continue;
                }

                // This index intentionally records only direct vanilla shimmer transforms.
                forward[source] = target;
                List<int> sources;
                if (!reverse.TryGetValue(target, out sources))
                {
                    sources = new List<int>();
                    reverse[target] = sources;
                }

                sources.Add(source);
            }

            foreach (var pair in reverse)
            {
                pair.Value.Sort();
            }
        }
    }
}
