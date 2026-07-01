using JueMingZ.Input.Hotkeys;

namespace JueMingZ.Config
{
    public sealed class UnifiedHotkeyFeaturePolicy
    {
        public UnifiedHotkeyFeaturePolicy(
            string policyId,
            string displayName,
            int maxModifierCount,
            bool allowMousePrimary,
            bool allowSamePolicyChord,
            string runtimeGateSummary)
        {
            PolicyId = policyId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            MaxModifierCount = maxModifierCount < 0 ? 0 : maxModifierCount;
            AllowMousePrimary = allowMousePrimary;
            AllowSamePolicyChord = allowSamePolicyChord;
            RuntimeGateSummary = runtimeGateSummary ?? string.Empty;
        }

        public string PolicyId { get; private set; }
        public string DisplayName { get; private set; }
        public int MaxModifierCount { get; private set; }
        public bool AllowMousePrimary { get; private set; }
        public bool AllowSamePolicyChord { get; private set; }
        public string RuntimeGateSummary { get; private set; }

        public bool BlocksTerrariaOriginalConflicts
        {
            get { return false; }
        }

        public bool TryValidate(HotkeyChord chord, out string resultCode, out string message)
        {
            resultCode = string.Empty;
            message = string.Empty;
            if (chord == null || chord.PrimaryKey == null)
            {
                resultCode = "invalidToken";
                message = "missing hotkey chord";
                return false;
            }

            if (chord.Modifiers != null && chord.Modifiers.Count > MaxModifierCount)
            {
                resultCode = "invalidToken";
                message = "modifier limit exceeded for " + DisplayName;
                return false;
            }

            if (!AllowMousePrimary && chord.PrimaryKey.Kind == HotkeyTokenKind.MouseButton)
            {
                resultCode = "invalidToken";
                message = "mouse primary is not allowed for " + DisplayName;
                return false;
            }

            return true;
        }
    }
}
