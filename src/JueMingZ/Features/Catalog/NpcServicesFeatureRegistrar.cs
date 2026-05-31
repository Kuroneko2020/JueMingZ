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
        }
    }
}
