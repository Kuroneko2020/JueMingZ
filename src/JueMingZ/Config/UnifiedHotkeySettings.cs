using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using JueMingZ.Input.Hotkeys;

namespace JueMingZ.Config
{
    [DataContract]
    public sealed class UnifiedHotkeySettings
    {
        public const int CurrentConfigVersion = 1;
        private const string DefaultMapQuickAnnouncementTrigger = "LAlt+LShift+MouseLeft";

        [DataMember(Order = 1)]
        public int ConfigVersion { get; set; } = CurrentConfigVersion;

        [DataMember(Order = 2)]
        public Dictionary<string, string> BindingsById { get; set; } = CreateDefaultBindings();

        public static UnifiedHotkeySettings CreateDefault()
        {
            return new UnifiedHotkeySettings
            {
                ConfigVersion = CurrentConfigVersion,
                BindingsById = CreateDefaultBindings()
            };
        }

        public static Dictionary<string, string> CreateDefaultBindings()
        {
            // User-facing wording may say Alt + Shift + MouseLeft, but the new config never writes generic modifiers.
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger, DefaultMapQuickAnnouncementTrigger }
            };
        }

        public void Normalize()
        {
            if (ConfigVersion <= 0)
            {
                ConfigVersion = CurrentConfigVersion;
            }

            BindingsById = NormalizeBindings(BindingsById);
        }

        public string GetBinding(string bindingId)
        {
            var normalizedId = NormalizeBindingId(bindingId);
            if (normalizedId.Length <= 0 || BindingsById == null)
            {
                return string.Empty;
            }

            string value;
            return BindingsById.TryGetValue(normalizedId, out value) ? value ?? string.Empty : string.Empty;
        }

        public bool TrySetBinding(
            string bindingId,
            string chordText,
            out UnifiedHotkeyBindingUpdateResult result)
        {
            var normalizedId = NormalizeBindingId(bindingId);
            if (normalizedId.Length <= 0)
            {
                result = UnifiedHotkeyBindingUpdateResult.Failure("invalidBindingId", string.Empty, "Binding id is empty.");
                return false;
            }

            if (BindingsById == null)
            {
                BindingsById = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            if (string.IsNullOrWhiteSpace(chordText))
            {
                var changed = BindingsById.Remove(normalizedId);
                result = UnifiedHotkeyBindingUpdateResult.Cleared(normalizedId, changed);
                return true;
            }

            var parse = HotkeyParser.Parse(chordText);
            if (!parse.Succeeded)
            {
                result = UnifiedHotkeyBindingUpdateResult.Failure(parse.Reason, normalizedId, parse.Token);
                return false;
            }

            string previous;
            var normalized = parse.Normalized;
            var changedBinding = !BindingsById.TryGetValue(normalizedId, out previous) ||
                                 !string.Equals(previous, normalized, StringComparison.Ordinal);
            BindingsById[normalizedId] = normalized;
            result = UnifiedHotkeyBindingUpdateResult.Updated(normalizedId, normalized, parse.Display, changedBinding);
            return true;
        }

        public string CreateCacheSignature()
        {
            unchecked
            {
                var hash = 2166136261u;
                AddHashInt(ref hash, ConfigVersion);

                var bindings = NormalizeBindings(BindingsById);
                var keys = new List<string>(bindings.Keys);
                keys.Sort(StringComparer.Ordinal);
                AddHashInt(ref hash, keys.Count);
                for (var index = 0; index < keys.Count; index++)
                {
                    var key = keys[index];
                    AddHashString(ref hash, key);
                    AddHashString(ref hash, bindings[key]);
                }

                return hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private static Dictionary<string, string> NormalizeBindings(Dictionary<string, string> source)
        {
            var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
            if (source == null || source.Count <= 0)
            {
                return normalized;
            }

            foreach (var pair in source)
            {
                var bindingId = NormalizeBindingId(pair.Key);
                if (bindingId.Length <= 0 || string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                var parse = HotkeyParser.Parse(pair.Value);
                if (!parse.Succeeded)
                {
                    continue;
                }

                normalized[bindingId] = parse.Normalized;
            }

            return normalized;
        }

        private static string NormalizeBindingId(string bindingId)
        {
            return string.IsNullOrWhiteSpace(bindingId) ? string.Empty : bindingId.Trim();
        }

        private static void AddHashString(ref uint hash, string value)
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

        private static void AddHashInt(ref uint hash, int value)
        {
            AddHashString(ref hash, value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
