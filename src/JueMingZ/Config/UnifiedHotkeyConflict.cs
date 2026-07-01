namespace JueMingZ.Config
{
    public sealed class UnifiedHotkeyConflict
    {
        public UnifiedHotkeyConflict(
            string bindingId,
            string ownerDisplayName,
            string policyId,
            string chord)
        {
            BindingId = bindingId ?? string.Empty;
            OwnerDisplayName = ownerDisplayName ?? string.Empty;
            PolicyId = policyId ?? string.Empty;
            Chord = chord ?? string.Empty;
        }

        public string BindingId { get; private set; }
        public string OwnerDisplayName { get; private set; }
        public string PolicyId { get; private set; }
        public string Chord { get; private set; }

        public string ResultCode
        {
            get { return "conflictWith:" + OwnerDisplayName; }
        }
    }
}
