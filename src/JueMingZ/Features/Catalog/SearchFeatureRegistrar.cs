using JueMingZ.Actions;

namespace JueMingZ.Features.Catalog
{
    public static class SearchFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            registry.Register(FeatureDefinitionBuilder
                .Create("search.main", "搜索查询", "占位符")
                .Domain(FeatureCodeDomain.Search)
                .Category(FeatureUserCategory.Search)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.World, GameStateKind.Inventory, GameStateKind.Npcs, GameStateKind.Tiles)
                .Multiplayer(FeatureMultiplayerSupport.Unknown)
                .VisibleInMainUi(true)
                .Implemented(false)
                .Notes("用户认为有很大可开发性，但还没想好具体功能，因此先单独准备界面。M3 只登记入口。")
                .Build());
        }
    }
}
