using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class InventoryAndItemsFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.InventoryQuickItemHotkeys, "快捷物品", "给物品添加快捷使用键")
                .Actions(InputActionKind.UseHotbarItem)
                .GameState(GameStateKind.Inventory, GameStateKind.Player)
                .Config(FeatureConfigUiKind.ListConfigWindow)
                .Hotkey(true, true)
                .Notes("按设定快捷键后，直接使用对应物品一次。支持贝壳电话、魔法海螺、恶魔海螺等多形态物品兼容匹配。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.InventoryAutoStack, "自动堆叠", "拾取物品后尝试把同类物品堆叠到附近箱子")
                .Actions(InputActionKind.Chest, InputActionKind.InventorySlot)
                .GameState(GameStateKind.Inventory, GameStateKind.Chests, GameStateKind.Player)
                .Notes("开启后检测刚增加的背包 itemType，并通过选择性 QuickStack 只尝试堆叠该 itemType；不执行全背包 QuickStack。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.InventoryAutoSell, "自动出售", "靠近商店 NPC 时尝试出售名单里的物品")
                .Actions(InputActionKind.Shop, InputActionKind.InventorySlot)
                .GameState(GameStateKind.Inventory, GameStateKind.Shops, GameStateKind.Npcs)
                .Config(FeatureConfigUiKind.ListConfigWindow)
                .Notes("开启后在玩家靠近可开商店 NPC、背包存在自动出售名单物品且自动堆叠没有待处理动作时，提交 Shop 动作走原版商店出售路径。默认名单为旧鞋、钓鱼海草和锡罐。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.InventoryAutoDiscard, "自动丢弃", "背包存在名单物品时尝试放入原版垃圾桶")
                .Actions(InputActionKind.TrashSlot, InputActionKind.InventorySlot)
                .GameState(GameStateKind.Inventory, GameStateKind.Player)
                .Config(FeatureConfigUiKind.ListConfigWindow)
                .Notes("开启后在自动堆叠和自动出售之后检测背包名单物品，提交 TrashSlot 动作走原版 ItemSlot 垃圾桶路径；默认名单为空。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.InventoryQuickBagOpen, "快速开袋", "按住shift长按右键点击匣子快速打开")
                .Actions(InputActionKind.InventorySlot)
                .GameState(GameStateKind.Inventory, GameStateKind.Player)
                .Notes("按住 Shift 并长按右键时，对匣子类物品执行快速重复右键打开。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.InventoryAutoDepositCoins, "自动存钱", "靠近容器主动存放货币")
                .Actions(InputActionKind.Chest, InputActionKind.InventorySlot)
                .GameState(GameStateKind.Inventory, GameStateKind.Chests, GameStateKind.Player)
                .Notes("开启后在玩家靠近银行类容器时，只对背包中的铜/银/金/铂币提交 Chest 动作，并在执行层调用原版 ChestUI.MoveCoins 银行存钱路径；若个人银行目标为空且来源是存钱罐/飞猪/切斯特，则只补第一笔钱币栈作为 fallback；不执行全背包 QuickStack。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.InventoryAutoExtractinator, "自动提炼", "靠近提炼机尝试自动提炼")
                .Actions(InputActionKind.InventorySlot)
                .GameState(GameStateKind.Inventory, GameStateKind.Tiles, GameStateKind.Player)
                .Notes("靠近提炼机或叶绿提炼机时，对背包可提炼物品执行快速重复右键提炼。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.InventoryKeepFavorited, "保持收藏", "让收藏的物品保持状态")
                .Actions(InputActionKind.InventorySlot)
                .GameState(GameStateKind.Inventory, GameStateKind.Player)
                .Notes("收藏物品在右键换装、替换等路径中丢失收藏时，会自动恢复收藏状态，直到玩家手动取消。"), true);
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder)
        {
            Add(registry, builder, false);
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder, bool implemented)
        {
            registry.Register(builder
                .Domain(FeatureCodeDomain.InventoryAndItems)
                .Category(FeatureUserCategory.Items)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .VisibleInMainUi(implemented)
                .Implemented(implemented)
                .Build());
        }
    }
}
