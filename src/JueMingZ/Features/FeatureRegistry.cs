using System;
using System.Collections.Generic;
using System.Linq;
using JueMingZ.Features.Catalog;

namespace JueMingZ.Features
{
    public sealed class FeatureRegistry
    {
        private readonly Dictionary<string, FeatureDefinition> _definitions =
            new Dictionary<string, FeatureDefinition>(StringComparer.OrdinalIgnoreCase);

        public int Count
        {
            get { return _definitions.Count; }
        }

        public void Register(FeatureDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new ArgumentException("FeatureDefinition.Id 不能为空。", "definition");
            }

            _definitions[definition.Id] = definition;
        }

        public bool TryGet(string featureId, out FeatureDefinition definition)
        {
            return _definitions.TryGetValue(featureId ?? string.Empty, out definition);
        }

        public IReadOnlyList<FeatureDefinition> GetAll()
        {
            return _definitions.Values.OrderBy(definition => definition.Id, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static FeatureRegistry CreateDefault()
        {
            var registry = new FeatureRegistry();
            FeatureCatalogRegistrar.RegisterAll(registry);
            return registry;
        }
    }
}
