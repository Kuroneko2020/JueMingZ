using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class WorldAutomationFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            // CodeDomain remains WorldAutomation while UserCategory.Misc preserves the existing player-facing page.
            registry.Register(FeatureDefinitionBuilder.Create(FeatureIds.WorldAutomationAutoMining, "自动挖矿", "手持镐子对准矿物点击快捷键，或手动挖下第一个矿物后自动采集矿脉。")
                .Domain(FeatureCodeDomain.WorldAutomation)
                .Category(FeatureUserCategory.Misc)
                .Actions(InputActionKind.TileInteract, InputActionKind.MouseTarget, InputActionKind.ItemUse)
                .GameState(GameStateKind.Tiles, GameStateKind.Player, GameStateKind.Inventory)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .Config(FeatureConfigUiKind.InlineHotkey)
                .Hotkey(true, false)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("用户要求 UI 显示在“杂项”，不要出现“自动化”UI 分类。快捷键模式按矿物标记矿群；自动模式手动挖下第一个矿物后标记矿群；同类矿物三格内连成一组，离开矿物边缘 30 格取消标记。")
                .Build());

            registry.Register(FeatureDefinitionBuilder.Create(FeatureIds.WorldAutomationAutoCaptureCritter, "自动捕捉", "自动模式携带虫网即可临时切网捕捉；手持模式只在当前手持虫网时接管。")
                .Domain(FeatureCodeDomain.WorldAutomation)
                .Category(FeatureUserCategory.Misc)
                .Actions(InputActionKind.RawInput, InputActionKind.ItemUse, InputActionKind.MouseTarget)
                .GameState(GameStateKind.Npcs, GameStateKind.Inventory, GameStateKind.Player, GameStateKind.Fishing)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("显示在 F5“杂项”页，模式为“自动 / 手持 / 关闭”。自动模式扫描背包 0-49 槽虫网和附近 catchItem > 0 的小动物，通过 RawInput 短会话在 ItemCheck 内临时切虫网并持续挥网；手持模式只接受当前选中虫网。钓鱼时捕捉结束后恢复原鱼竿并按原抛竿坐标重抛。")
                .Build());

            registry.Register(FeatureDefinitionBuilder.Create(FeatureIds.WorldAutomationAutoHarvest, "自动收获", "携带再生法杖或再生之斧时自动收获花盆 / 种植盆上的可收获草药，并按原草药种类复种。")
                .Domain(FeatureCodeDomain.WorldAutomation)
                .Category(FeatureUserCategory.Misc)
                .Actions(InputActionKind.TileInteract, InputActionKind.MouseTarget, InputActionKind.ItemUse, InputActionKind.RawInput)
                .GameState(GameStateKind.Tiles, GameStateKind.Inventory, GameStateKind.Player)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("显示在 F5“杂项”页，标准开关；开启后扫描玩家附近花盆 / 种植盆上的原版可掉种子的草药，用再生法杖或再生之斧经 RawInput 短会话在 ItemCheck 内连续指向原作物。若原版没有立即复种，会在 10 秒内记录原 herb style 并只用对应种子尝试原版种植。")
                .Build());

            registry.Register(FeatureDefinitionBuilder.Create(FeatureIds.WorldAutomationTravelMenu, "旅行菜单", "在非旅行人物/世界中临时启用原版旅行菜单。")
                .Domain(FeatureCodeDomain.WorldAutomation)
                .Category(FeatureUserCategory.Misc)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.Player, GameStateKind.World)
                .Multiplayer(FeatureMultiplayerSupport.SinglePlayerFallbackOnly)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Lifecycle(FeatureLifecycleStatus.Implemented)
                .Notes("单机实验功能：F5 杂项页显示入口；开启后安装 save-guard / CreativeUI / scoped Journey hooks，在原版 CreativeUI Update/Draw 和 powers 消费点短作用域伪装 Journey 状态，并在保存、关闭或离开世界时还原。当前仍只允许单机，不声明多人支持。")
                .Build());
        }
    }
}
