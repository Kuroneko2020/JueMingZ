using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class CombatFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatAutoAim, "辅助瞄准", "辅助瞄准按范围中心与目标策略选择敌怪；实际开火优先中心/安全命中点。")
                .Actions(InputActionKind.Aim, InputActionKind.MouseTarget)
                .GameState(GameStateKind.CombatTargets, GameStateKind.Player)
                .Hotkey(true, true)
                .Notes("Stage 4 special weapon rules implemented for coin gun, rain-from-sky, parallel multi-shot, spread multi-shot, guided cursor, homing/beam, and heavy-gravity projectiles. 鼠标中心提供 0~50 瞄准半径滑条；玩家中心使用屏幕范围。实际攻击点优先中心/安全点，nearestHitboxPoint 仅作低优先级 fallback。智能弹道适配和哨兵免疫是内部默认策略，不登记为独立 Feature。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatAutoClicker, "自动连点", "ItemCheck scoped takeover 策略已接入，等待诊断与复测收口")
                .Actions()
                .GameState(GameStateKind.Player, GameStateKind.Inventory)
                .Notes("上一套自动连点 source / policy / input source 路线已按用户实测失败清理。当前接入 ItemCheck scoped takeover 核心、四象限策略、原版自动复用让路、鱼竿 / 左轮硬排除，并让路 ItemUseBridge、UseItemPulseBridge、自动捕捉 / 自动收获等相邻 scoped use；诊断和用户复测口径等待后续计划。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatFlailCombo, "链球连击", "长按右键触发连击")
                .Actions(InputActionKind.ItemUse, InputActionKind.RawInput)
                .GameState(GameStateKind.Player, GameStateKind.Inventory, GameStateKind.CombatTargets)
                .Priority(18)
                .Notes("默认关闭。手持 aiStyle 15 且非悠悠球的链球/连枷时，长按右键在 Player.ItemCheck scoped takeover 内制造左键按下/松开节奏；右键 UI、交互 Tile/NPC 和物品自身右键语义全部原版优先并 fail-closed。只读取 projectile 状态，不写 projectile/NPC/Tile/玩家状态；按下 tick 可继续让 combat.auto_aim 应用目标。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatPhasebladeQuickSwitch, "光剑快切", "按住右键快切快捷栏的光剑")
                .Actions(InputActionKind.ItemUse, InputActionKind.SelectHotbarSlot, InputActionKind.RawInput)
                .GameState(GameStateKind.Player, GameStateKind.Inventory, GameStateKind.CombatTargets)
                .Priority(17)
                .Notes("默认关闭。当前已具备固定 18 个 Phaseblade / Phasesaber 物品资格、专用 RawInput bridge 和 Player.ItemCheck scoped takeover；右键安全门禁和运行入口由后续治理阶段接入。只扫描快捷栏 0-9，不搜索背包，不能复用通用自动连点资格。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatPerfectRevolver, "完美左轮", "最大程度发挥左轮威力")
                .Actions(InputActionKind.ItemUse, InputActionKind.RawInput)
                .GameState(GameStateKind.Player, GameStateKind.Inventory)
                .Priority(20)
                .Notes("手持左轮时，长按左键会在 Player.ItemCheck prefix 接管本次攻击输入：冷却中松开以累积原版 revolverCritChanceBonus，2 tick fire window 或空闲时按下；按下 tick 仍允许 combat.auto_aim。完美左轮优先级高于系统自动连点和 combat.auto_clicker。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatMagicStringClicker, "省力魔法绳", "长按实现装备魔法绳连点的效果")
                .Actions(InputActionKind.ItemUse, InputActionKind.RawInput)
                .GameState(GameStateKind.Player, GameStateKind.Inventory)
                .Hotkey(true, true)
                .Priority(15)
                .Notes("装备魔法绳效果并手持悠悠球时，长按使用键会通过队列授权的 RawInput 脉冲模拟高速点击；按下脉冲继续允许 combat.auto_aim 应用。省力魔法绳优先级高于 combat.auto_clicker，低于 combat.perfect_revolver。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatAutoFacing, "自动转向", "什么叫毁灭刃不能转头？")
                .Actions(InputActionKind.Aim, InputActionKind.RawInput)
                .GameState(GameStateKind.Player, GameStateKind.CombatTargets)
                .Hotkey(true, true)
                .Notes("玩家按住使用合格战斗物品时，A/D 手动输入优先作为即时朝向；没有 A/D 输入时才朝向目标或鼠标方向。方向写入集中在 TerrariaInputCompat，Runtime 阶段通过 RawInput 请求执行，Player.ItemCheck 前会再次按当前 A/D 校正。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatEquipmentWarning, "装备提示", "Boss 或非血月事件期间提示当前装备非战斗饰品")
                .Actions()
                .GameState(GameStateKind.Player, GameStateKind.World, GameStateKind.CombatTargets)
                .Notes("只读扫描当前功能装备 / 饰品槽和 miscEquips；检测到 Boss 或血月以外的事件时，如果穿着建筑、钓鱼、信息、经济、召唤娃娃或非战斗头身腿等装备，在玩家头顶显示 1 秒提示，不提交动作也不修改装备。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatAutoBossDamageReport, "自动汇报", "boss战结束后自动汇报")
                .Actions()
                .GameState(GameStateKind.World, GameStateKind.CombatTargets)
                .Notes("默认关闭。运行时只读 Terraria NPCDamageTracker.RecentAttempts()；开启后的首帧只建立基线，之后检测到新的已结束 Boss 伤害记录时，通过原版聊天命令路径发送 /bossdamage。不会模拟键盘输入，不写玩家、NPC、Tile 或网络状态。"), true);

            Add(registry, FeatureDefinitionBuilder.Create(FeatureIds.CombatGoblinExecution, "哥布林必死", "允许玩家武器命中哥布林工匠")
                .Actions()
                .GameState(GameStateKind.Player)
                .Notes("默认关闭；开启后只在 Terraria 原版玩家近战或弹幕命中 NPC 的路径里对白名单 NPC type 107 放行，不改变臭虫剑、向导/服装商娃娃、NPC friendly/townNPC 状态、玩家状态、伤害写入或网络包。BoundGoblin type 105 和其它城镇 NPC 不受影响。"), true);
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder)
        {
            Add(registry, builder, false);
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder, bool implemented)
        {
            // Registrar metadata only describes capabilities; runtime behavior still belongs to services and actions.
            registry.Register(builder
                .Domain(FeatureCodeDomain.Combat)
                .Category(FeatureUserCategory.Combat)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .VisibleInMainUi(true)
                .Implemented(implemented)
                .Build());
        }
    }
}
