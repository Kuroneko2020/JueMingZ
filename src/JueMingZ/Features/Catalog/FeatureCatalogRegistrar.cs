namespace JueMingZ.Features.Catalog
{
    public static class FeatureCatalogRegistrar
    {
        public static void RegisterAll(FeatureRegistry registry)
        {
            DiagnosticsFeatureRegistrar.Register(registry);
            InformationFeatureRegistrar.Register(registry);
            FishingFeatureRegistrar.Register(registry);
            CombatFeatureRegistrar.Register(registry);
            MovementFeatureRegistrar.Register(registry);
            BuffAndRecoveryFeatureRegistrar.Register(registry);
            InventoryAndItemsFeatureRegistrar.Register(registry);
            WorldAutomationFeatureRegistrar.Register(registry);
            NpcServicesFeatureRegistrar.Register(registry);
            MapEnhancementFeatureRegistrar.Register(registry);
            BlueprintFeatureRegistrar.Register(registry);
            SearchFeatureRegistrar.Register(registry);
        }
    }
}
