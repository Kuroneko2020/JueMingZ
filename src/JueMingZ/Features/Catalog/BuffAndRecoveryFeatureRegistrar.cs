using JueMingZ.Actions;

namespace JueMingZ.Features.Catalog
{
    public static class BuffAndRecoveryFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            Add(registry, FeatureDefinitionBuilder.Create("buff.nurse_auto_heal", "护士自动治疗", "靠近护士自动治疗")
                .Actions(InputActionKind.NpcInteract)
                .GameState(GameStateKind.Player, GameStateKind.Npcs, GameStateKind.Buffs)
                .Implemented(true)
                .Notes("靠近护士且需要回血或清除可移除 Debuff 时，提交 NpcInteract 并尝试走原版护士聊天治疗处理。当前按单机/本地辅助可用范围表达，严格多人服务器权威同步尚未完整验收；多人回滚或不同步应作为后续兼容问题处理。SecondaryDomain: NpcServices。"));

            Add(registry, FeatureDefinitionBuilder.Create("buff.auto_buff", "自动增益", "基础白名单策略；开启不会立即 QuickBuff")
                .Actions(InputActionKind.BuffPotionDirectUse)
                .GameState(GameStateKind.Inventory, GameStateKind.Buffs, GameStateKind.Player)
                .Hotkey(true, true)
                .Implemented(true)
                .Notes("默认关闭，F5 开启后仅启用 Buff 药水白名单策略，不会立即 QuickBuff。手动 QuickBuff 一次仍是独立诊断按钮，等价原版 B；受控本地增益药执行路径只允许在动作/兼容层治理。当前按单机/本地辅助可用范围表达，不声明严格多人服务器权威同步已完整验证。"));

            Add(registry, FeatureDefinitionBuilder.Create("buff.auto_station_buff", "自动家具增益", "靠近可交互家具时自动使用")
                .Actions(InputActionKind.TileInteract)
                .GameState(GameStateKind.Tiles, GameStateKind.Inventory, GameStateKind.Buffs, GameStateKind.Player)
                .Config(FeatureConfigUiKind.StrategyConfigWindow)
                .Implemented(true)
                .Notes("靠近水晶球、弹药箱、施法桌、磨刀站、战争桌、蛋糕块、药水站等互动家具且缺少对应 Buff 时，提交 TileInteract；背包家具暂不实现。当前按单机/本地辅助可用范围表达，严格多人同步异常按后续兼容问题处理。"));

            Add(registry, FeatureDefinitionBuilder.Create("buff.auto_heal", "自动回血", "掉血后按智能或快速策略使用背包回血药")
                .Actions(InputActionKind.UseInventoryItem)
                .GameState(GameStateKind.Player, GameStateKind.Inventory, GameStateKind.Buffs)
                .Config(FeatureConfigUiKind.ListConfigWindow)
                .Implemented(true)
                .Notes("默认关闭，F5 面板开启。Quick 选择回血量最高的可用药；Smart 选择不明显浪费且适配超上限药和诡药的可用药；配置只按 item type 禁用候选物品，不改变原优先级。动作提交 UseInventoryItem 并调用 Terraria 原版恢复物品内部流程。当前按单机/本地辅助可用范围表达，不声明严格多人服务器权威同步已完整验证。"));

            Add(registry, FeatureDefinitionBuilder.Create("buff.auto_mana", "自动回蓝", "当前手持耗蓝武器下一次蓝量不足时自动使用背包回蓝药")
                .Actions(InputActionKind.UseInventoryItem)
                .GameState(GameStateKind.Player, GameStateKind.Inventory, GameStateKind.Buffs)
                .Config(FeatureConfigUiKind.ListConfigWindow)
                .Implemented(true)
                .Notes("默认关闭，F5 面板开启；当前手持耗蓝武器下一次实际使用会因魔力不足失败时，选择可用回蓝药并提交 UseInventoryItem；配置只按 item type 禁用候选物品，不改变原优先级。当前按单机/本地辅助可用范围表达，不声明严格多人服务器权威同步已完整验证。"));
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder)
        {
            registry.Register(builder
                .Domain(FeatureCodeDomain.BuffAndRecovery)
                .Category(FeatureUserCategory.Buff)
                .Multiplayer(FeatureMultiplayerSupport.LocalAssistPendingMultiplayerVerification)
                .VisibleInMainUi(true)
                .Build());
        }
    }
}
