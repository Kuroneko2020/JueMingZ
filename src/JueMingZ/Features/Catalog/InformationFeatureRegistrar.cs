using JueMingZ.Actions;

using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class InformationFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            Add(registry, FeatureDefinitionBuilder.Create("information.enemy_name_labels", "敌怪显名", "敌怪头顶显示名字")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Npcs, GameStateKind.CombatTargets)
                .Notes("仅针对敌怪，需要和 NPC 和小动物分开，敌怪名字默认显示为红色。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.critter_name_labels", "动物显名", "小动物头顶显示名字")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Npcs)
                .Notes("仅针对小动物，需要和 NPC 和敌怪分开，动物名字默认显示为蓝色，金小动物额外显示为金色。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.npc_name_labels", "NPC显名", "NPC头顶显示名字/类型")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Npcs)
                .Notes("仅针对 NPC，需要和小动物和敌怪分开，名字默认显示为绿色，并提供显示名字或者类型的选项。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.chest_name_labels", "宝箱显名", "显示宝箱名称")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Chests, GameStateKind.Tiles)
                .Notes("提供始终 / 开过 / 关闭三态；始终需要金属探测器或上位信息饰品，开过不需要。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.sign_text_labels", "牌子显示", "直接显示木牌内容")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Tiles)
                .Notes("只读 Main.sign[] 和 Sign.text；提供全部 / 前几行 / 前几字 / 关闭，按原版悬停 460px、最多 10 行显示规则换行裁剪。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.tombstone_text_labels", "墓碑显示", "直接显示墓碑内容")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Tiles)
                .Notes("只读 Main.sign[] 和 Sign.text；仅显示 TileID.Tombstones 的墓碑文本，配置和牌子显示一致，默认红色。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.highlight_life_crystal", "显示生命水晶", "需要身上有金属探测器，高亮显示生命水晶")
                .GameState(GameStateKind.Inventory, GameStateKind.Tiles)
                .Notes("不显示名字，而是高亮，需要玩家背包里有金属探测器。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.highlight_mana_crystal", "显示魔力水晶", "需要身上有金属探测器，高亮显示魔力水晶")
                .GameState(GameStateKind.Inventory, GameStateKind.Tiles)
                .Notes("不显示名字，而是高亮，需要玩家背包里有金属探测器。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.highlight_digtoise", "显示碎岩龟", "需要身上有金属探测器，高亮显示碎岩龟")
                .GameState(GameStateKind.Inventory, GameStateKind.Tiles)
                .Notes("不显示名字，而是高亮，需要玩家背包里有金属探测器。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.highlight_life_fruit", "显示生命果", "需要身上有金属探测器，高亮显示生命果")
                .GameState(GameStateKind.Inventory, GameStateKind.Tiles)
                .Notes("不显示名字，而是高亮，需要玩家背包里有金属探测器。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.highlight_dragon_egg", "显示龙蛋", "需要身上有金属探测器，高亮显示龙蛋")
                .GameState(GameStateKind.Inventory, GameStateKind.Tiles)
                .Notes("不显示名字，而是高亮，需要玩家背包里有金属探测器。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.info_panel_position", "调整信息窗位置", "点击开始拖动信息窗口")
                .Actions(InputActionKind.RawInput)
                .GameState(GameStateKind.UiState)
                .Hotkey(true, true, "信息窗")
                .Notes("点击开始后，决明主窗口隐藏，此时可以拖动信息窗口，松开鼠标取消拖动，并再次打开决明窗口。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.biome_display", "群系显示", "显示玩家当前所在群系")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Player, GameStateKind.Biome));

            Add(registry, FeatureDefinitionBuilder.Create("information.world_infection", "世界感染", "显示感染占比，需要树妖")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.World, GameStateKind.Npcs)
                .Notes("不需要树妖活着，而是在这个世界已存在树妖。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.luck_value", "幸运值", "显示幸运值和来源明细，需要巫师")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Player, GameStateKind.Npcs)
                .Notes("不需要巫师活着，而是在这个世界已存在巫师；按当前 Terraria Player.RecalculateLuck 只读拆分瓢虫、火把、药水、风筝、银河珍珠、灯笼夜、花园侏儒、臭味、装备、钱币和破镜坏运贡献。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.fishing_catches", "完整鱼获", "显示当前鱼漂位置可能钓上的全部鱼获物品名")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Fishing, GameStateKind.Player, GameStateKind.World)
                .Notes("必须等鱼漂进到水里才显示；只显示鱼、匣子等物品名，不显示鱼力、液体或群系 fallback。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.fishing_filtered_catches", "过滤鱼获", "显示按当前钓鱼过滤模式筛选后的当前可能鱼获")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Fishing, GameStateKind.Player, GameStateKind.World)
                .Notes("复用当前鱼漂位置的完整鱼获候选，并按 fishing.filter 的模式、匹配方式和特殊规则筛选；只用于信息显示，不提交自动钓鱼动作。"));

            Add(registry, FeatureDefinitionBuilder.Create("information.angler_quest", "渔夫任务", "显示当天渔夫任务，需要渔夫")
                .Config(FeatureConfigUiKind.StyleConfigWindow)
                .GameState(GameStateKind.Npcs, GameStateKind.World)
                .Notes("不需要渔夫活着，而是在这个世界已存在渔夫。"));

            registry.Register(FeatureDefinitionBuilder.Create(FeatureIds.InformationUserNotes, "笔记", "全局用户笔记与悬挂便签")
                .Domain(FeatureCodeDomain.Information)
                .Category(FeatureUserCategory.MoreInformation)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.UiState)
                .Multiplayer(FeatureMultiplayerSupport.Unknown)
                .VisibleInMainUi(false)
                .Implemented(false)
                .Notes("本阶段只登记稳定 feature id 与存储底座；F5 笔记页、正文编辑器和悬挂 overlay 后续阶段实现后再打开 visible / implemented。")
                .Build());
        }

        private static void Add(FeatureRegistry registry, FeatureDefinitionBuilder builder)
        {
            registry.Register(builder
                .Domain(FeatureCodeDomain.Information)
                .Category(FeatureUserCategory.MoreInformation)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Build());
        }
    }
}
