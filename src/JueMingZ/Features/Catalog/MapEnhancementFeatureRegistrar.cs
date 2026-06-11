using JueMingZ.Actions;
using JueMingZ.Common;

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

            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.MapQuickAnnouncement, "快捷宣告", "把鼠标指向对象转换为聊天宣告。")
                .Domain(FeatureCodeDomain.Information)
                .Category(FeatureUserCategory.MapEnhancement)
                .Actions(InputActionKind.RawInput)
                .GameState(GameStateKind.Player, GameStateKind.Inventory, GameStateKind.Npcs, GameStateKind.Tiles, GameStateKind.World, GameStateKind.UiState)
                .Multiplayer(FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
                .Config(FeatureConfigUiKind.InlineHotkey)
                .DefaultEnabled(false)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("地图加强页开关、三格快捷键配置、颜色/冷却配置、运行触发、目标解析、聊天发送、鼠标触发输入吃掉、最近一次运行诊断和性能/输入边界审计防线已接入。CodeDomain=Information 表示只读信息/沟通辅助责任，UserCategory=MapEnhancement 表示玩家 UI 固定归地图加强页。")
                .Build());
        }
    }
}
