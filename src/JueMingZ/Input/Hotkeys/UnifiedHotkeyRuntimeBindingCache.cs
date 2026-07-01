using System;
using System.Collections.Generic;
using JueMingZ.Config;

namespace JueMingZ.Input.Hotkeys
{
    public sealed class UnifiedHotkeyRuntimeBindingCache
    {
        private static readonly UnifiedHotkeyRuntimeBinding[] EmptyBindings = new UnifiedHotkeyRuntimeBinding[0];

        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, UnifiedHotkeyRuntimeBinding> _bindingsById =
            new Dictionary<string, UnifiedHotkeyRuntimeBinding>(StringComparer.Ordinal);

        private string _cacheSignature = string.Empty;
        private UnifiedHotkeyRuntimeBinding[] _snapshot = EmptyBindings;
        private int _rebuildCount;

        public string CacheSignature
        {
            get { lock (_syncRoot) { return _cacheSignature; } }
        }

        public int RebuildCount
        {
            get { lock (_syncRoot) { return _rebuildCount; } }
        }

        public UnifiedHotkeyRuntimeBinding[] GetSnapshot(UnifiedHotkeySettings settings, string cacheSignature)
        {
            cacheSignature = cacheSignature ?? string.Empty;
            lock (_syncRoot)
            {
                if (string.Equals(_cacheSignature, cacheSignature, StringComparison.Ordinal))
                {
                    return _snapshot;
                }

                RebuildLocked(settings, cacheSignature);
                return _snapshot;
            }
        }

        public bool TryGetBinding(
            UnifiedHotkeySettings settings,
            string cacheSignature,
            string bindingId,
            out UnifiedHotkeyRuntimeBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(bindingId))
            {
                return false;
            }

            GetSnapshot(settings, cacheSignature);
            lock (_syncRoot)
            {
                return _bindingsById.TryGetValue(bindingId.Trim(), out binding) &&
                       binding != null &&
                       binding.Chord != null;
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _bindingsById.Clear();
                _cacheSignature = string.Empty;
                _snapshot = EmptyBindings;
                _rebuildCount = 0;
            }
        }

        private void RebuildLocked(UnifiedHotkeySettings settings, string cacheSignature)
        {
            _bindingsById.Clear();

            if (settings == null)
            {
                _cacheSignature = cacheSignature;
                _snapshot = EmptyBindings;
                _rebuildCount++;
                return;
            }

            var registrations = UnifiedHotkeyConflictRegistry.BuildRegistrations(settings);
            var bindings = new List<UnifiedHotkeyRuntimeBinding>(registrations.Count);
            for (var index = 0; index < registrations.Count; index++)
            {
                var registration = registrations[index];
                if (registration == null || !registration.Enabled || registration.Chord == null)
                {
                    continue;
                }

                var binding = new UnifiedHotkeyRuntimeBinding(
                    registration.BindingId,
                    registration.OwnerDisplayName,
                    registration.Policy,
                    registration.Chord);
                _bindingsById[binding.BindingId] = binding;
                bindings.Add(binding);
            }

            _cacheSignature = cacheSignature;
            _snapshot = bindings.Count <= 0 ? EmptyBindings : bindings.ToArray();
            _rebuildCount++;
        }
    }
}
