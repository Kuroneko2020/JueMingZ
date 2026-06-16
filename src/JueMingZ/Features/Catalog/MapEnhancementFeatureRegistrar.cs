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
                .Notes("开启后，在大地图右键弹出图标，可标注资源、矿物、怪物或其它特殊地点；未来应有单独界面和定位列表。")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.MapPersistentDeathMarkers, "死亡点常驻", "在大地图常驻显示当前玩家-世界已记录死亡点。")
                .Domain(FeatureCodeDomain.MapEnhancement)
                .Category(FeatureUserCategory.MapEnhancement)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.Map, GameStateKind.World, GameStateKind.UiState)
                .Multiplayer(FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
                .Config(FeatureConfigUiKind.None)
                .DefaultEnabled(false)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("地图加强页第一行标准开关；只控制大地图自有死亡点 layer 显示，不控制 Player.KillMe 死亡事件采集。绘制读取当前玩家-世界 deaths.jsonl，不依赖也不修改原版最后死亡点状态。")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.MapDeathHistory, "死亡信息", "显示当前玩家-世界死亡次数，并分页查看全部死亡历史。")
                .Domain(FeatureCodeDomain.MapEnhancement)
                .Category(FeatureUserCategory.MapEnhancement)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.Map, GameStateKind.World, GameStateKind.UiState)
                .Multiplayer(FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
                .Config(FeatureConfigUiKind.None)
                .DefaultEnabled(true)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("地图加强页第二行固定信息功能；右侧读取当前玩家-世界 death-summary.json 显示死亡次数，详情 modal 从 deaths.jsonl 分页读取全部记录，不写游戏状态或输入。")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.MapWorldDayCount, "世界天数", "显示当前玩家-世界累计观察到的游戏天数。")
                .Domain(FeatureCodeDomain.MapEnhancement)
                .Category(FeatureUserCategory.MapEnhancement)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.Map, GameStateKind.World, GameStateKind.UiState)
                .Multiplayer(FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
                .Config(FeatureConfigUiKind.None)
                .DefaultEnabled(true)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("地图加强页第三行固定信息功能；采样当前玩家-世界 playtime.json，按观察到的 Main.dayTime/Main.time 世界时钟 delta 累计，显示 floor(totalGameTicks/86400)，不读取 PlayerFileData.GetPlayTime，不写游戏状态或输入。")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.MapRevealedAreaRatio, "揭示区域", "显示当前玩家-世界地图揭示区域占比。")
                .Domain(FeatureCodeDomain.MapEnhancement)
                .Category(FeatureUserCategory.MapEnhancement)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.Map, GameStateKind.World, GameStateKind.UiState)
                .Multiplayer(FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
                .Config(FeatureConfigUiKind.None)
                .DefaultEnabled(true)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("地图加强页第四行固定信息功能；runtime 按性能/快速模式用时间预算分片只读 Main.Map.IsRevealed(x,y)，缓存到当前 pairId 的 exploration-summary.json，不在 Draw 中扫描或写文件。")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.MapCustomMarkers, "地图标记", "为当前玩家-世界保存自定义地图标记。")
                .Domain(FeatureCodeDomain.MapEnhancement)
                .Category(FeatureUserCategory.MapEnhancement)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.Map, GameStateKind.World, GameStateKind.UiState)
                .Multiplayer(FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
                .Config(FeatureConfigUiKind.None)
                .DefaultEnabled(false)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("地图标记独立于 map.enhanced_map 占位入口；当前接入开关、player-worlds/<pairId>/map-markers.json 数据底座、右键建点、大地图绘制、F5 列表命名删除和只写大地图 UI 状态的跳转。导航/传送/智驾仍是 UI-only 占位。")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.MapFootprints, "足迹", "在大地图展示当前玩家-世界足迹。")
                .Domain(FeatureCodeDomain.MapEnhancement)
                .Category(FeatureUserCategory.MapEnhancement)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.Map, GameStateKind.World, GameStateKind.UiState)
                .Multiplayer(FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
                .Config(FeatureConfigUiKind.None)
                .DefaultEnabled(false)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("MapFootprintsDisplayEnabled controls fullscreen display only; recording remains always-on during normal world ticks. Current stage includes config, F5 switch, footprints.json, runtime recorder, render cache, green map lines, playback UI, and runtime snapshot diagnostics.")
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
