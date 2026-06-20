using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class BlueprintFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.BlueprintMain, "蓝图", "入口配置占位")
                .Domain(FeatureCodeDomain.Blueprint)
                .Category(FeatureUserCategory.Blueprint)
                .Actions(InputActionKind.BlueprintAutoPlace)
                .GameState(GameStateKind.World, GameStateKind.Tiles, GameStateKind.Inventory)
                .Multiplayer(FeatureMultiplayerSupport.Unknown)
                .Config(FeatureConfigUiKind.Placeholder)
                .VisibleInMainUi(true)
                .Implemented(false)
                .Notes("04-12 已提供 F5 入口、蓝图库、创建/采集/实例/投影/材料/擦除与自动摆放 ActionQueue 契约；实际 Tile/Wall 自动摆放、替换和镜像仍未实现。")
                .Build());
        }
    }
}
