using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class NpcServicesFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.NpcAutoReforge, "快速重铸", "按住重铸键在设置的前缀处停下")
                .Domain(FeatureCodeDomain.NpcServices)
                .Category(FeatureUserCategory.Misc)
                .Actions(InputActionKind.Reforge, InputActionKind.NpcInteract, InputActionKind.InventorySlot)
                .GameState(GameStateKind.Reforge, GameStateKind.Npcs, GameStateKind.Inventory, GameStateKind.Player)
                .Config(FeatureConfigUiKind.ListConfigWindow)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("在哥布林那里按住重铸键，匹配到设置好的前缀就停下，同时覆盖游戏自带的“好前缀暂停”。")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(FeatureIds.NpcAutoTaxCollect, "自动收税", "靠近税收官且有可领取税款时自动领取")
                .Domain(FeatureCodeDomain.NpcServices)
                .Category(FeatureUserCategory.Misc)
                .Actions(InputActionKind.NpcInteract)
                .GameState(GameStateKind.Player)
                .Config(FeatureConfigUiKind.None)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("靠近税收官且玩家 taxMoney 可领取时，提交 NpcInteract 并调用原版 Main.NPCChatText_DoTaxCollector 聊天按钮处理；不直接加钱、不写背包或 NPC 状态。")
                .Build());
        }
    }
}
