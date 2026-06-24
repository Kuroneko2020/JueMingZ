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
                .Notes("blueprint.main 仍保持 planned / IsImplemented=false，用作蓝图页总入口与长期缺口容器；现有子链路已提供 F5 / 手持 / 动作快捷键入口、蓝图库、创建 / 采集 / 实例 / 投影 / 材料 / 擦除、移动 / 区域修改 / 镜像，以及阶段 15 自动摆放 ActionQueue 契约、ItemUseBridge 投影复验和同类替换候选。wire、open door、paint / coating / slope / half brick / inactive、复杂 frame 与多人验收仍 fail-closed / 待后续治理。")
                .Build());
        }
    }
}
