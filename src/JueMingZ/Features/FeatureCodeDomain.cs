namespace JueMingZ.Features
{
    public enum FeatureCodeDomain
    {
        Diagnostics,
        Information,
        Fishing,
        Combat,
        Movement,
        BuffAndRecovery,
        InventoryAndItems,
        WorldAutomation,
        NpcServices,
        MapEnhancement,
        Blueprint,
        Search,

        // Obsolete compatibility aliases only. Do not use for new registrations,
        // JSON export, UI, docs, or diagnostics. Use ToCanonicalName() for output.
        [System.Obsolete("Use BuffAndRecovery. This alias is compatibility-only.")]
        PotionAndBuff = BuffAndRecovery,
        [System.Obsolete("Use MapEnhancement. This alias is compatibility-only.")]
        Map = MapEnhancement,
        [System.Obsolete("Use WorldAutomation. This alias is compatibility-only.")]
        Automation = WorldAutomation,
        [System.Obsolete("Use NpcServices. This alias is compatibility-only.")]
        Npc = NpcServices
    }
}
