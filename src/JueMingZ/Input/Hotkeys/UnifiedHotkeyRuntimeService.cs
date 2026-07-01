using System;
using System.Collections.Generic;
using JueMingZ.Config;

namespace JueMingZ.Input.Hotkeys
{
    public static class UnifiedHotkeyRuntimeService
    {
        private static readonly object SyncRoot = new object();
        private static readonly UnifiedHotkeyRuntimeBindingCache BindingCache = new UnifiedHotkeyRuntimeBindingCache();
        private static readonly Dictionary<string, bool> WasDownByBindingId =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        public static UnifiedHotkeyRuntimeBinding[] GetBindingsSnapshot()
        {
            return BindingCache.GetSnapshot(
                ConfigService.UnifiedHotkeySettings,
                ConfigService.UnifiedHotkeySettingsCacheSignature);
        }

        public static bool TryGetBinding(string bindingId, out UnifiedHotkeyRuntimeBinding binding)
        {
            return BindingCache.TryGetBinding(
                ConfigService.UnifiedHotkeySettings,
                ConfigService.UnifiedHotkeySettingsCacheSignature,
                bindingId,
                out binding);
        }

        public static UnifiedHotkeyRuntimeTriggerResult QueryBinding(string bindingId)
        {
            return QueryBinding(
                ConfigService.UnifiedHotkeySettings,
                ConfigService.UnifiedHotkeySettingsCacheSignature,
                bindingId,
                UnifiedHotkeyRuntimeGate.CreateCurrentContext(),
                UnifiedHotkeyRuntimeInputState.Physical);
        }

        public static UnifiedHotkeyRuntimeTriggerResult QueryBinding(
            UnifiedHotkeySettings settings,
            string cacheSignature,
            string bindingId,
            UnifiedHotkeyRuntimeGateContext gateContext,
            UnifiedHotkeyRuntimeInputState inputState)
        {
            UnifiedHotkeyRuntimeBinding binding;
            if (!BindingCache.TryGetBinding(settings, cacheSignature, bindingId, out binding))
            {
                SetWasDown(bindingId, false);
                return UnifiedHotkeyRuntimeTriggerResult.Missing(bindingId);
            }

            var down = IsChordDown(binding.Chord, inputState);
            var wasDown = GetWasDown(binding.BindingId);
            SetWasDown(binding.BindingId, down);
            if (!down || wasDown)
            {
                return UnifiedHotkeyRuntimeTriggerResult.Idle(binding.BindingId, down, wasDown, binding);
            }

            var gate = UnifiedHotkeyRuntimeGate.Evaluate(gateContext);
            if (gate.Blocked)
            {
                return UnifiedHotkeyRuntimeTriggerResult.Blocked(binding.BindingId, gate.Reason, down, wasDown, binding);
            }

            return UnifiedHotkeyRuntimeTriggerResult.Triggered(binding.BindingId, binding);
        }

        internal static int CacheRebuildCountForTesting
        {
            get { return BindingCache.RebuildCount; }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                WasDownByBindingId.Clear();
            }

            BindingCache.Clear();
        }

        private static bool IsChordDown(HotkeyChord chord, UnifiedHotkeyRuntimeInputState inputState)
        {
            if (chord == null || chord.PrimaryKey == null || inputState == null)
            {
                return false;
            }

            for (var index = 0; index < chord.Modifiers.Count; index++)
            {
                var modifier = chord.Modifiers[index];
                if (modifier == null || !inputState.IsDown(modifier.VirtualKey))
                {
                    return false;
                }
            }

            return inputState.IsDown(chord.PrimaryKey.VirtualKey);
        }

        private static bool GetWasDown(string bindingId)
        {
            lock (SyncRoot)
            {
                return !string.IsNullOrWhiteSpace(bindingId) &&
                       WasDownByBindingId.TryGetValue(bindingId, out var wasDown) &&
                       wasDown;
            }
        }

        private static void SetWasDown(string bindingId, bool down)
        {
            if (string.IsNullOrWhiteSpace(bindingId))
            {
                return;
            }

            lock (SyncRoot)
            {
                WasDownByBindingId[bindingId] = down;
            }
        }
    }
}
