using JueMingZ.Input.Hotkeys;

namespace JueMingZ.Config
{
    public sealed class UnifiedHotkeyBindingRegistration
    {
        public UnifiedHotkeyBindingRegistration(
            string bindingId,
            string ownerDisplayName,
            UnifiedHotkeyFeaturePolicy policy,
            bool enabled,
            HotkeyChord chord)
        {
            BindingId = bindingId ?? string.Empty;
            OwnerDisplayName = ownerDisplayName ?? string.Empty;
            Policy = policy;
            Enabled = enabled;
            Chord = chord;
        }

        public string BindingId { get; private set; }
        public string OwnerDisplayName { get; private set; }
        public UnifiedHotkeyFeaturePolicy Policy { get; private set; }
        public bool Enabled { get; private set; }
        public HotkeyChord Chord { get; private set; }

        public string PolicyId
        {
            get { return Policy == null ? string.Empty : Policy.PolicyId; }
        }
    }
}
