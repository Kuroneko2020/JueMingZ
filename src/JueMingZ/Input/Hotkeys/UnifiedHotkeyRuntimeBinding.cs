using JueMingZ.Config;

namespace JueMingZ.Input.Hotkeys
{
    public sealed class UnifiedHotkeyRuntimeBinding
    {
        public UnifiedHotkeyRuntimeBinding(
            string bindingId,
            string ownerDisplayName,
            UnifiedHotkeyFeaturePolicy policy,
            HotkeyChord chord)
        {
            BindingId = bindingId ?? string.Empty;
            OwnerDisplayName = ownerDisplayName ?? string.Empty;
            Policy = policy;
            Chord = chord;
        }

        public string BindingId { get; private set; }
        public string OwnerDisplayName { get; private set; }
        public UnifiedHotkeyFeaturePolicy Policy { get; private set; }
        public HotkeyChord Chord { get; private set; }

        public string PolicyId
        {
            get { return Policy == null ? string.Empty : Policy.PolicyId; }
        }

        public string Normalized
        {
            get { return Chord == null ? string.Empty : Chord.Normalized; }
        }

        public string Display
        {
            get { return Chord == null ? string.Empty : Chord.Display; }
        }
    }
}
