using System;
using System.Collections.Generic;
using JueMingZ.Features;

namespace JueMingZ.Runtime
{
    internal sealed class RuntimeFeatureCatalogSnapshotStats
    {
        public int FeatureCatalogCount { get; set; }
        public int ImplementedFeatureCount { get; set; }
        public int VisibleFeatureCount { get; set; }
        public int HotkeyVisibleFeatureCount { get; set; }
        public Dictionary<string, int> UserCategoryCounts { get; set; }
        public Dictionary<string, int> CodeDomainCounts { get; set; }

        public RuntimeFeatureCatalogSnapshotStats()
        {
            UserCategoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            CodeDomainCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal static class RuntimeFeatureCatalogSnapshotCache
    {
        private static RuntimeFeatureCatalogSnapshotStats _current = new RuntimeFeatureCatalogSnapshotStats();

        public static RuntimeFeatureCatalogSnapshotStats Current
        {
            get { return _current; }
        }

        public static void Refresh(FeatureRegistry registry)
        {
            var stats = new RuntimeFeatureCatalogSnapshotStats();
            if (registry != null)
            {
                var definitions = registry.GetAll();
                for (var index = 0; index < definitions.Count; index++)
                {
                    var definition = definitions[index];
                    if (definition == null)
                    {
                        continue;
                    }

                    if (definition.IsInternalPlatform || !definition.CodeDomain.IsPublicDomain())
                    {
                        continue;
                    }

                    stats.FeatureCatalogCount++;
                    if (definition.IsImplemented)
                    {
                        stats.ImplementedFeatureCount++;
                    }

                    if (definition.VisibleInMainUi)
                    {
                        stats.VisibleFeatureCount++;
                    }

                    if (definition.HotkeyListVisible)
                    {
                        stats.HotkeyVisibleFeatureCount++;
                    }

                    Increment(stats.UserCategoryCounts, definition.UserCategory.ToString());
                    Increment(stats.CodeDomainCounts, definition.CodeDomain.ToCanonicalName());
                }
            }

            _current = stats;
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                key = "Unknown";
            }

            int value;
            counts.TryGetValue(key, out value);
            counts[key] = value + 1;
        }
    }
}
