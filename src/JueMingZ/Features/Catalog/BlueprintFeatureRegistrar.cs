using JueMingZ.Actions;

namespace JueMingZ.Features.Catalog
{
    public static class BlueprintFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            registry.Register(FeatureDefinitionBuilder
                .Create("blueprint.main", "蓝图", "占位符")
                .Domain(FeatureCodeDomain.Blueprint)
                .Category(FeatureUserCategory.Blueprint)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.World, GameStateKind.Tiles, GameStateKind.Inventory)
                .Multiplayer(FeatureMultiplayerSupport.Unknown)
                .VisibleInMainUi(true)
                .Implemented(false)
                .Notes("非常复杂的功能，目标类似 Minecraft masa 全家桶里的蓝图 mod。M3 只登记入口。")
                .Build());
        }
    }
}
