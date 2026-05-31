using JueMingZ.Actions;

namespace JueMingZ.Features.Catalog
{
    public static class MapEnhancementFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            registry.Register(FeatureDefinitionBuilder
                .Create("map.enhanced_map", "大地图加强", "占位符")
                .Domain(FeatureCodeDomain.MapEnhancement)
                .Category(FeatureUserCategory.MapEnhancement)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.Map, GameStateKind.World, GameStateKind.UiState)
                .Multiplayer(FeatureMultiplayerSupport.Unknown)
                .VisibleInMainUi(true)
                .Implemented(false)
                .Notes("开启后，在大地图右键弹出图标，可标注资源、矿物、怪物或其他特殊地点；未来应有单独界面和定位列表。")
                .Build());
        }
    }
}
