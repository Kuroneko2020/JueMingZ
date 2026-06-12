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
                .Notes("第一版只读物品查询 UI 已接入 F5 搜索页，覆盖基础信息、获取来源空区块、合成来源 / 用途和直接微光关系；获取来源真实数据由后续阶段逐类填充。")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.SearchChestItemLocator, "箱内物品定位", "输入物品名或 #ID 后定位附近含有该物品的箱子")
                .Domain(FeatureCodeDomain.Search)
                .Category(FeatureUserCategory.Search)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.World, GameStateKind.Inventory, GameStateKind.Chests, GameStateKind.Tiles)
                .Multiplayer(FeatureMultiplayerSupport.Unknown)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("F5 搜索页已接入箱内物品定位输入、候选、提交、清空、附近已同步箱子只读快照、世界高亮和诊断摘要；多人客户端提交时会按当前玩家 section 受控请求原版同步，但仍待多人实机验证。")
                .Build());
        }
    }
}
