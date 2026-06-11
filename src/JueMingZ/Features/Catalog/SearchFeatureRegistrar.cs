using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class SearchFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.SearchMain, "搜索查询", "输入物品名或 #ID 查看只读物品资料")
                .Domain(FeatureCodeDomain.Search)
                .Category(FeatureUserCategory.Search)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.World, GameStateKind.Inventory, GameStateKind.Npcs, GameStateKind.Tiles)
                .Multiplayer(FeatureMultiplayerSupport.Unknown)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("第一版只读物品查询 UI 已接入 F5 搜索页，覆盖基础信息、合成来源 / 用途和直接微光关系；掉落、商店、decraft 和背包入口仍待后续阶段。")
                .Build());
        }
    }
}
