using System;
using System.Collections.Generic;
using JueMingZ.Common;

namespace JueMingZ.Config
{
    public static class FeatureToggleHotkeyTargetCatalog
    {
        private static readonly FeatureToggleHotkeyTarget[] Targets =
        {
            Binary("buff.auto_heal", "自动回血", "buff"),
            Binary("buff.auto_mana", "自动回蓝", "buff"),
            Binary("buff.nurse_auto_heal", "自动护士", "buff"),
            Binary("buff.auto_station_buff", "自动家具", "buff"),
            Binary("buff.auto_buff", "自动增益", "buff"),
            Binary(FeatureIds.InventoryQuickItemHotkeys, "快捷物品", "items"),
            Binary(FeatureIds.InventoryAutoStack, "自动堆叠", "items"),
            Binary(FeatureIds.InventoryAutoSell, "自动出售", "items"),
            Binary(FeatureIds.InventoryAutoDiscard, "自动丢弃", "items"),
            Multi(FeatureIds.WorldAutomationAutoCaptureCritter, "自动捕捉", "items", "Off", "Auto", "Manual"),
            Binary(FeatureIds.WorldAutomationAutoHarvest, "自动收获", "items"),
            Binary(FeatureIds.InventoryQuickBagOpen, "持续开袋", "items"),
            Binary(FeatureIds.InventoryAutoDepositCoins, "自动存钱", "items"),
            Binary(FeatureIds.InventoryAutoExtractinator, "自动提炼", "items"),
            Binary(FeatureIds.InventoryKeepFavorited, "保持收藏", "items"),
            Binary(FeatureIds.NpcAutoReforge, "快速重铸", "misc"),
            Multi(FeatureIds.WorldAutomationAutoMining, "自动挖矿", "misc", "Off", "Hotkey", "Auto"),
            Binary(FeatureIds.NpcAutoTaxCollect, "自动收税", "misc"),
            Binary(FeatureIds.WorldAutomationTravelMenu, "旅行菜单", "misc"),
            Binary(FeatureIds.MapPersistentDeathMarkers, "死亡点常驻", "map"),
            Binary(FeatureIds.MapFootprints, "足迹", "map"),
            Binary(FeatureIds.MapRareCreatureDirection, "稀有生物显示方向", "map"),
            Binary(FeatureIds.MapTravellingMerchantDirection, "旅商显示方向", "map"),
            Binary(FeatureIds.MapQuickAnnouncement, "快捷宣告", "map"),
            Binary(FeatureIds.MapCustomMarkers, "地图标记", "map"),
            Binary(FeatureIds.FishingAutoFish, "自动钓鱼", "fishing"),
            Binary(FeatureIds.FishingAutoLoadout, "自动换装", "fishing"),
            Binary(FeatureIds.FishingAutoEquipment, "自动配装", "fishing"),
            Multi(FeatureIds.FishingAutoStoreQuestFish, "自动存放鱼", "fishing", "Off", "QuestFish", "All"),
            Binary("fishing.cut_rod_skip", "切杆跳过", "fishing"),
            Binary("information.enemy_name_labels", "敌怪显名", "information"),
            Binary("information.critter_name_labels", "动物显名", "information"),
            Multi("information.npc_name_labels", "NPC显名", "information", "Off", "Name", "Type"),
            Multi("information.chest_name_labels", "宝箱显名", "information", "Off", "Always", "Opened"),
            Multi("information.sign_text_labels", "牌子显示", "information", "Off", "All", "Lines", "Characters"),
            Multi("information.tombstone_text_labels", "墓碑显示", "information", "Off", "All", "Lines", "Characters"),
            Binary("information.highlight_life_crystal", "显示生命水晶", "information"),
            Binary("information.highlight_mana_crystal", "显示魔力水晶", "information"),
            Binary(FeatureIds.InformationHighlightDigtoise, "显示碎岩龟", "information"),
            Binary("information.highlight_life_fruit", "显示生命果", "information"),
            Binary("information.highlight_dragon_egg", "显示龙蛋", "information"),
            Binary("information.biome_display", "群系显示", "information"),
            Binary("information.world_infection", "世界感染", "information"),
            Binary("information.luck_value", "幸运值", "information"),
            Binary("information.fishing_catches", "完整鱼获", "information"),
            Binary("information.fishing_filtered_catches", "过滤鱼获", "information"),
            Binary("information.angler_quest", "渔夫任务", "information"),
            Binary(FeatureIds.CombatAutoClicker, "自动连点", "combat"),
            Binary(FeatureIds.CombatFlailCombo, "链球连击", "combat"),
            Binary(FeatureIds.CombatPhasebladeQuickSwitch, "光剑快切", "combat"),
            Binary(FeatureIds.CombatPerfectRevolver, "完美左轮", "combat"),
            Binary(FeatureIds.CombatMagicStringClicker, "省力魔法绳", "combat"),
            Binary(FeatureIds.CombatAutoFacing, "自动转向", "combat"),
            Binary(FeatureIds.CombatEquipmentWarning, "装备提示", "combat"),
            Binary(FeatureIds.CombatAutoBossDamageReport, "自动汇报", "combat"),
            Binary(FeatureIds.CombatGoblinExecution, "哥布林必死", "combat"),
            Binary(FeatureIds.MovementSimulatedMultiJump, "模拟连跳", "movement"),
            Multi(FeatureIds.MovementContinuousDash, "连续冲刺", "movement", "Off", "HoldDirection", "DoubleTapAndHold"),
            Binary(FeatureIds.MovementTeleportCorrection, "传送修正", "movement"),
            Binary(FeatureIds.MovementSafeLanding, "智能防摔", "movement")
        };

        private static readonly Dictionary<string, FeatureToggleHotkeyTarget> ById = BuildIndex();

        public static IEnumerable<FeatureToggleHotkeyTarget> All
        {
            get { return Targets; }
        }

        public static bool TryGet(string targetId, out FeatureToggleHotkeyTarget target)
        {
            target = null;
            string normalized;
            return TryNormalizeTargetId(targetId, out normalized) && ById.TryGetValue(normalized, out target);
        }

        public static bool TryNormalizeTargetId(string targetId, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(targetId) ? string.Empty : targetId.Trim();
            if (normalized.Length <= 0)
            {
                return false;
            }

            FeatureToggleHotkeyTarget target;
            if (!ById.TryGetValue(normalized, out target))
            {
                return false;
            }

            normalized = target.TargetId;
            return true;
        }

        public static string GetDisplayName(string targetId)
        {
            FeatureToggleHotkeyTarget target;
            return TryGet(targetId, out target) ? target.DisplayName : (targetId ?? string.Empty);
        }

        public static bool TryNormalizeLastNonOffMode(string targetId, string mode, out string normalized)
        {
            normalized = string.Empty;
            FeatureToggleHotkeyTarget target;
            if (!TryGet(targetId, out target) || !target.IsMultiMode)
            {
                return false;
            }

            return target.TryNormalizeNonOffMode(mode, out normalized);
        }

        private static Dictionary<string, FeatureToggleHotkeyTarget> BuildIndex()
        {
            var map = new Dictionary<string, FeatureToggleHotkeyTarget>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Targets.Length; index++)
            {
                var target = Targets[index];
                if (target != null && !map.ContainsKey(target.TargetId))
                {
                    map.Add(target.TargetId, target);
                }
            }

            return map;
        }

        private static FeatureToggleHotkeyTarget Binary(string targetId, string displayName, string pageId)
        {
            return new FeatureToggleHotkeyTarget(targetId, displayName, pageId, string.Empty, new string[0]);
        }

        private static FeatureToggleHotkeyTarget Multi(string targetId, string displayName, string pageId, string offMode, params string[] nonOffModes)
        {
            return new FeatureToggleHotkeyTarget(targetId, displayName, pageId, offMode, nonOffModes);
        }
    }

    public sealed class FeatureToggleHotkeyTarget
    {
        internal FeatureToggleHotkeyTarget(string targetId, string displayName, string pageId, string offMode, string[] nonOffModes)
        {
            TargetId = targetId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PageId = pageId ?? string.Empty;
            OffMode = offMode ?? string.Empty;
            NonOffModes = nonOffModes ?? new string[0];
        }

        public string TargetId { get; private set; }
        public string DisplayName { get; private set; }
        public string PageId { get; private set; }
        public string OffMode { get; private set; }
        public string[] NonOffModes { get; private set; }
        public bool IsMultiMode { get { return NonOffModes.Length > 0; } }

        public bool TryNormalizeNonOffMode(string mode, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(mode))
            {
                return false;
            }

            var candidate = mode.Trim();
            for (var index = 0; index < NonOffModes.Length; index++)
            {
                var value = NonOffModes[index];
                if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = value;
                    return true;
                }
            }

            return false;
        }
    }
}
