using System;
using System.Collections.Generic;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Features
{
    public sealed class FeatureManager
    {
        private readonly object _syncRoot = new object();
        private readonly FeatureRegistry _registry;
        private readonly List<FeatureDefinition> _definitions;
        private readonly Dictionary<string, FeatureState> _states =
            new Dictionary<string, FeatureState>(StringComparer.OrdinalIgnoreCase);
        private long _updateCount;
        private DateTime _nextHeartbeatUtc = DateTime.MinValue;

        public FeatureManager(FeatureRegistry registry)
            : this(registry, ConfigService.FeatureSettings)
        {
        }

        public FeatureManager(FeatureRegistry registry, FeatureSettings settings)
        {
            _registry = registry ?? FeatureRegistry.CreateDefault();
            _definitions = new List<FeatureDefinition>(_registry.GetAll());
            InitializeStates(settings ?? FeatureSettings.CreateDefault());
        }

        public void Register(FeatureDefinition definition)
        {
            lock (_syncRoot)
            {
                _registry.Register(definition);
                var replaced = false;
                for (var index = 0; index < _definitions.Count; index++)
                {
                    if (string.Equals(_definitions[index].Id, definition.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        _definitions[index] = definition;
                        replaced = true;
                        break;
                    }
                }

                if (!replaced)
                {
                    _definitions.Add(definition);
                }

                _states[definition.Id] = definition.DefaultEnabled ? FeatureState.Enabled : FeatureState.Disabled;
            }
        }

        public void Update(FeatureRuntimeContext context)
        {
            var nowUtc = context == null || context.UtcNow == default(DateTime)
                ? DateTime.UtcNow
                : context.UtcNow;
            FeatureManagerDiagnosticInfo heartbeatInfo = null;

            lock (_syncRoot)
            {
                _updateCount++;

                var settings = ConfigService.FeatureSettings ?? FeatureSettings.CreateDefault();
                for (var index = 0; index < _definitions.Count; index++)
                {
                    var definition = _definitions[index];
                    bool configured;
                    if (settings.EnabledByFeatureId != null && settings.EnabledByFeatureId.TryGetValue(definition.Id, out configured))
                    {
                        _states[definition.Id] = configured ? FeatureState.Enabled : FeatureState.Disabled;
                        continue;
                    }

                    FeatureState state;
                    if (!_states.TryGetValue(definition.Id, out state))
                    {
                        _states[definition.Id] = definition.DefaultEnabled ? FeatureState.Enabled : FeatureState.Disabled;
                    }
                }

                if (nowUtc >= _nextHeartbeatUtc)
                {
                    _nextHeartbeatUtc = nowUtc.AddSeconds(10);
                    heartbeatInfo = BuildDiagnosticInfoLocked();
                }
            }

            if (heartbeatInfo != null)
            {
                Logger.Info(
                    "FeatureManager",
                    "FeatureManager heartbeat: totalFeatures=" + heartbeatInfo.TotalFeatures +
                    ", enabled=" + heartbeatInfo.EnabledFeatures +
                    ", updateCount=" + heartbeatInfo.UpdateCount);
            }
        }

        public FeatureManagerDiagnosticInfo GetDiagnosticInfo()
        {
            lock (_syncRoot)
            {
                return BuildDiagnosticInfoLocked();
            }
        }

        private FeatureManagerDiagnosticInfo BuildDiagnosticInfoLocked()
        {
            var total = 0;
            var enabled = 0;
            for (var index = 0; index < _definitions.Count; index++)
            {
                var definition = _definitions[index];
                if (definition == null ||
                    definition.IsInternalPlatform ||
                    !definition.CodeDomain.IsPublicDomain())
                {
                    continue;
                }

                total++;

                FeatureState state;
                if (_states.TryGetValue(definition.Id, out state) &&
                    state == FeatureState.Enabled)
                {
                    enabled++;
                }
            }

            return new FeatureManagerDiagnosticInfo
            {
                TotalFeatures = total,
                EnabledFeatures = enabled,
                UpdateCount = _updateCount
            };
        }

        private void InitializeStates(FeatureSettings settings)
        {
            for (var index = 0; index < _definitions.Count; index++)
            {
                var definition = _definitions[index];
                bool configured;
                var enabled = settings.EnabledByFeatureId != null &&
                              settings.EnabledByFeatureId.TryGetValue(definition.Id, out configured)
                    ? configured
                    : definition.DefaultEnabled;
                _states[definition.Id] = enabled ? FeatureState.Enabled : FeatureState.Disabled;
            }
        }
    }

    public sealed class FeatureManagerDiagnosticInfo
    {
        public static readonly FeatureManagerDiagnosticInfo Empty = new FeatureManagerDiagnosticInfo();

        public int TotalFeatures { get; set; }
        public int EnabledFeatures { get; set; }
        public long UpdateCount { get; set; }
    }
}
