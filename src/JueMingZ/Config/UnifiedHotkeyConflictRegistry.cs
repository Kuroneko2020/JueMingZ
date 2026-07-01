using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JueMingZ.Input.Hotkeys;

namespace JueMingZ.Config
{
    public static class UnifiedHotkeyConflictRegistry
    {
        public static bool TryValidateBinding(
            UnifiedHotkeySettings settings,
            string bindingId,
            string chordText,
            out UnifiedHotkeyBindingUpdateResult failure)
        {
            failure = null;
            UnifiedHotkeyFeaturePolicy policy;
            string ownerDisplayName;
            if (!UnifiedHotkeyFeaturePolicyCatalog.TryDescribeBinding(bindingId, out policy, out ownerDisplayName))
            {
                failure = UnifiedHotkeyBindingUpdateResult.Failure(
                    "invalidBindingId",
                    bindingId,
                    "Unknown unified hotkey binding id.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(chordText))
            {
                return true;
            }

            var parse = HotkeyParser.Parse(chordText);
            if (!parse.Succeeded)
            {
                failure = UnifiedHotkeyBindingUpdateResult.Failure(parse.Reason, bindingId, parse.Token);
                return false;
            }

            string resultCode;
            string policyMessage;
            if (!policy.TryValidate(parse.Chord, out resultCode, out policyMessage))
            {
                failure = UnifiedHotkeyBindingUpdateResult.Failure(resultCode, bindingId, policyMessage);
                return false;
            }

            UnifiedHotkeyConflict conflict;
            if (TryFindConflict(settings, bindingId, parse.Chord, out conflict))
            {
                failure = UnifiedHotkeyBindingUpdateResult.Failure(
                    conflict.ResultCode,
                    bindingId,
                    conflict.BindingId);
                return false;
            }

            return true;
        }

        public static bool TryFindConflict(
            UnifiedHotkeySettings settings,
            string currentBindingId,
            string chordText,
            out UnifiedHotkeyConflict conflict)
        {
            conflict = null;
            var parse = HotkeyParser.Parse(chordText);
            return parse.Succeeded &&
                   TryFindConflict(settings, currentBindingId, parse.Chord, out conflict);
        }

        public static bool TryFindConflict(
            UnifiedHotkeySettings settings,
            string currentBindingId,
            HotkeyChord chord,
            out UnifiedHotkeyConflict conflict)
        {
            conflict = null;
            if (chord == null || string.IsNullOrWhiteSpace(chord.Normalized))
            {
                return false;
            }

            UnifiedHotkeyFeaturePolicy currentPolicy;
            string currentDisplayName;
            if (!UnifiedHotkeyFeaturePolicyCatalog.TryDescribeBinding(currentBindingId, out currentPolicy, out currentDisplayName))
            {
                return false;
            }

            var currentNormalizedId = NormalizeBindingId(currentBindingId);
            var registrations = BuildRegistrations(settings);
            for (var index = 0; index < registrations.Count; index++)
            {
                var registration = registrations[index];
                if (registration == null ||
                    !registration.Enabled ||
                    registration.Chord == null ||
                    string.Equals(registration.BindingId, currentNormalizedId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(registration.Chord.Normalized, chord.Normalized, StringComparison.Ordinal))
                {
                    continue;
                }

                if (currentPolicy.AllowSamePolicyChord &&
                    registration.Policy != null &&
                    string.Equals(registration.Policy.PolicyId, currentPolicy.PolicyId, StringComparison.Ordinal))
                {
                    continue;
                }

                conflict = new UnifiedHotkeyConflict(
                    registration.BindingId,
                    registration.OwnerDisplayName,
                    registration.PolicyId,
                    registration.Chord.Normalized);
                return true;
            }

            return false;
        }

        public static IReadOnlyList<UnifiedHotkeyBindingRegistration> BuildRegistrations(UnifiedHotkeySettings settings)
        {
            var result = new List<UnifiedHotkeyBindingRegistration>();
            var bindings = settings == null ? null : settings.BindingsById;
            if (bindings == null || bindings.Count <= 0)
            {
                return new ReadOnlyCollection<UnifiedHotkeyBindingRegistration>(result);
            }

            foreach (var pair in bindings)
            {
                var bindingId = NormalizeBindingId(pair.Key);
                if (bindingId.Length <= 0 || string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                UnifiedHotkeyFeaturePolicy policy;
                string ownerDisplayName;
                if (!UnifiedHotkeyFeaturePolicyCatalog.TryDescribeBinding(bindingId, out policy, out ownerDisplayName))
                {
                    continue;
                }

                var parse = HotkeyParser.Parse(pair.Value);
                if (!parse.Succeeded)
                {
                    continue;
                }

                // This index is only for JueMingZ internal bindings. Terraria's own keybinds are intentionally
                // not a hard-fail source; players can resolve those conflicts in vanilla settings if needed.
                result.Add(new UnifiedHotkeyBindingRegistration(
                    bindingId,
                    ownerDisplayName,
                    policy,
                    true,
                    parse.Chord));
            }

            return new ReadOnlyCollection<UnifiedHotkeyBindingRegistration>(result);
        }

        private static string NormalizeBindingId(string bindingId)
        {
            return string.IsNullOrWhiteSpace(bindingId) ? string.Empty : bindingId.Trim();
        }
    }
}
