using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class FishingFeatureRegistrar
    {
        private const string EquipmentStrategyGroup = "fishing.equipment_strategy";

        public static void Register(FeatureRegistry registry)
        {
            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.FishingAutoFish, "自动钓鱼", "上钩后自动收杆并重新抛竿；菜单打开时不收杆")
                .Actions(InputActionKind.ItemUse, InputActionKind.MouseTarget)
                .GameState(GameStateKind.Fishing, GameStateKind.Player, GameStateKind.Inventory)
                .Hotkey(true, true)
                .Notes("玩家手动抛竿后进入钓鱼 session；观察本地鱼漂上钩后，如果 F5 主菜单未打开，则通过 InputActionQueue 收杆，并在鱼漂消失后重抛。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.FishingAutoLoadout, "自动换装", "进入钓鱼状态会切换到渔力高的配装")
                .Actions(InputActionKind.InventorySlot)
                .GameState(GameStateKind.Fishing, GameStateKind.Inventory, GameStateKind.Player)
                .ExclusiveGroup(EquipmentStrategyGroup)
                .Notes("只切换 Terraria 原版三套 loadout；和自动配装互斥，不交换背包装备、饰品或装饰栏。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.FishingAutoEquipment, "自动配装", "进入钓鱼状态会切换身上的饰品/装备到渔力最佳状态")
                .Actions(InputActionKind.InventorySlot)
                .GameState(GameStateKind.Fishing, GameStateKind.Inventory, GameStateKind.Player)
                .ExclusiveGroup(EquipmentStrategyGroup)
                .Notes("进入钓鱼 session 后从背包 / 虚空袋 / 社交栏临时抽取钓鱼装备；只要玩家仍手持本次钓鱼开始时记录的原鱼竿就保持配装；离开原鱼竿后安全恢复；和自动换装互斥。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.FishingAutoStoreQuestFish, "自动存放鱼", "按所有 / 任务鱼 / 关闭决定钓鱼存箱")
                .Actions(InputActionKind.Chest, InputActionKind.InventorySlot)
                .GameState(GameStateKind.Fishing, GameStateKind.Inventory, GameStateKind.Chests)
                .Notes("所有模式在自动钓鱼本轮 Pull 成功后，只对真实上钩鱼获 ID 做一次选择性 quick stack；任务鱼模式只处理当前渔夫任务鱼，并且只在附近容器或银行已有同类任务鱼时选择性 quick stack。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.FishingFilter, "钓鱼过滤", "根据白名单 / 黑名单过滤鱼获")
                .Actions(InputActionKind.ItemUse, InputActionKind.SelectHotbarSlot)
                .GameState(GameStateKind.Fishing, GameStateKind.Inventory, GameStateKind.Buffs)
                .Config(FeatureConfigUiKind.ListConfigWindow)
                .Notes("支持精确匹配 / 关键词匹配，当前页决定运行匹配方式；信息页支持完整鱼获 / 过滤鱼获显示；自动钓鱼上钩时只有存在声呐 Buff 才真实运行过滤，缺少声呐时保留本次鱼获；名单预设按白名单 / 黑名单、精确 / 关键词独立保存，应用时覆盖当前 active list；精确匹配按 Kind + Id 生效，不受材质包改名影响，关键词匹配按当前显示名生效，会受材质包 / 语言影响。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.FishingQuickRename, "快捷改名", "钓鱼页手动改名并刷新渔夫今日完成状态")
                .Actions(InputActionKind.PlayerRename)
                .GameState(GameStateKind.Player, GameStateKind.World, GameStateKind.UiState)
                .Notes("单机受控动作：通过 PlayerFileData.Rename 或受控 Player.name fallback 修改本地玩家名，并按 Main.anglerWhoFinishedToday 重新刷新 Main.anglerQuestFinished，模拟改名后重新进世界；多人暂不声明服务器权威支持。"), true, FeatureMultiplayerSupport.SinglePlayerFallbackOnly);
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder, bool implemented)
        {
            Add(registry, builder, implemented, FeatureMultiplayerSupport.SupportedByOriginalAction);
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder, bool implemented, FeatureMultiplayerSupport multiplayerSupport)
        {
            registry.Register(builder
                .Domain(FeatureCodeDomain.Fishing)
                .Category(FeatureUserCategory.Fishing)
                .Multiplayer(multiplayerSupport)
                .VisibleInMainUi(true)
                .Implemented(implemented)
                .Build());
        }
    }
}
