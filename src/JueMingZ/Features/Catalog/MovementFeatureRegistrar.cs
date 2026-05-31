using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class MovementFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            Add(
                registry,
                FeatureDefinitionBuilder.Create(
                        FeatureIds.MovementSimulatedMultiJump,
                        "模拟连跳",
                        "模拟蛙腿的连跳效果。")
                    .Actions(InputActionKind.Jump, InputActionKind.Movement)
                    .GameState(GameStateKind.Player)
                    .Notes("输入辅助功能：只模拟跳跃键释放/重新按下，不增加跳跃次数，不直接修改速度、高度、摔落伤害或玩家能力。"),
                true);

            Add(
                registry,
                FeatureDefinitionBuilder.Create(FeatureIds.MovementContinuousDash, "连续冲刺", "原版冲刺冷却就绪后补一帧 dash 输入脉冲。")
                    .Actions(InputActionKind.Dash, InputActionKind.Movement, InputActionKind.RawInput)
                    .GameState(GameStateKind.Player, GameStateKind.Inventory)
                    .Hotkey(true, true)
                    .Notes("输入辅助功能：支持按住方向键冲刺和双击并按住冲刺两种互斥模式，只补 controlDash/releaseDash 脉冲，不直接修改速度或位置。"),
                true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.MovementTeleportCorrection, "传送修正", "点击进方块时，自动修正到附近可传送空位。")
                .Actions(InputActionKind.TeleportCorrection, InputActionKind.MouseTarget, InputActionKind.ItemUse)
                .GameState(GameStateKind.Player, GameStateKind.Tiles)
                .Notes("传送杖鼠标目标修正：支持原版混沌传送杖和和谐传送杖；只在原版传送杖方法执行前临时修正鼠标目标，让原版完成传送、冷却、混乱状态和多人同步，不直接移动玩家。"),
                true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.MovementSafeLanding, "智能防摔", "利用合理手段避免摔落伤害。")
                .Actions(InputActionKind.Jump, InputActionKind.Movement, InputActionKind.InventorySlot, InputActionKind.UseHotbarItem, InputActionKind.MouseTarget)
                .GameState(GameStateKind.Player, GameStateKind.Inventory, GameStateKind.Tiles)
                .Config(FeatureConfigUiKind.StrategyConfigWindow)
                .Notes("SafeLanding 重构：当前实现优先级 0 已安全静默、优先级 1 已穿戴输入、优先级 2 临时装备、优先级 3 临时雨伞、优先级 4 原版 QuickGrapple 抓钩输入，以及优先级 5 背包 / 热栏传送杖原版点击传送；缓冲方块方案已放弃，不得作为当前策略恢复。"),
                true);
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder)
        {
            Add(registry, builder, false);
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder, bool implemented)
        {
            registry.Register(builder
                .Domain(FeatureCodeDomain.Movement)
                .Category(FeatureUserCategory.Movement)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .VisibleInMainUi(true)
                .Implemented(implemented)
                .Build());
        }
    }
}
