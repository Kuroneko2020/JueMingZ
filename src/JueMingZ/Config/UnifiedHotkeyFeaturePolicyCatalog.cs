using System;
using JueMingZ.Common;

namespace JueMingZ.Config
{
    public static class UnifiedHotkeyFeaturePolicyCatalog
    {
        public const string FeatureTogglePolicyId = "feature-toggle";
        public const string QuickItemPolicyId = "quick-item";
        public const string AutoMiningTriggerPolicyId = "auto-mining-trigger";
        public const string QuickAnnouncementPolicyId = "quick-announcement";
        public const string BlueprintActionPolicyId = "blueprint-action";

        private static readonly UnifiedHotkeyFeaturePolicy FeatureTogglePolicy =
            new UnifiedHotkeyFeaturePolicy(
                FeatureTogglePolicyId,
                "功能主开关快捷键",
                1,
                false,
                false,
                "后续运行时必须走统一文本 / 菜单 gate，只切换功能主开关。");

        private static readonly UnifiedHotkeyFeaturePolicy QuickItemPolicy =
            new UnifiedHotkeyFeaturePolicy(
                QuickItemPolicyId,
                "快捷物品快捷键",
                6,
                true,
                false,
                "后续运行时必须进入 InputActionQueue，不直接改写手持槽或原版用物品输入。");

        private static readonly UnifiedHotkeyFeaturePolicy AutoMiningTriggerPolicy =
            new UnifiedHotkeyFeaturePolicy(
                AutoMiningTriggerPolicyId,
                "自动挖矿采集键",
                6,
                true,
                false,
                "采集触发键只用于 Hotkey 模式入口，和功能主开关键保持分离。");

        private static readonly UnifiedHotkeyFeaturePolicy QuickAnnouncementPolicy =
            new UnifiedHotkeyFeaturePolicy(
                QuickAnnouncementPolicyId,
                "快捷宣告",
                2,
                true,
                false,
                "三格策略的 canonical chord；鼠标触发消费仍属于快捷宣告运行服务和 Compat 边界。");

        private static readonly UnifiedHotkeyFeaturePolicy BlueprintActionPolicy =
            new UnifiedHotkeyFeaturePolicy(
                BlueprintActionPolicyId,
                "蓝图动作快捷键",
                1,
                false,
                false,
                "保留 F5 主窗口关系；后续运行时必须走统一文本 / 菜单 gate，不直接写 Tile / Wall / 背包。");

        public static bool TryDescribeBinding(
            string bindingId,
            out UnifiedHotkeyFeaturePolicy policy,
            out string ownerDisplayName)
        {
            policy = null;
            ownerDisplayName = string.Empty;
            if (string.IsNullOrWhiteSpace(bindingId))
            {
                return false;
            }

            var normalized = bindingId.Trim();
            if (string.Equals(normalized, UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger, StringComparison.Ordinal))
            {
                policy = QuickAnnouncementPolicy;
                ownerDisplayName = "快捷宣告";
                return true;
            }

            if (string.Equals(normalized, UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger, StringComparison.Ordinal))
            {
                policy = AutoMiningTriggerPolicy;
                ownerDisplayName = "自动挖矿 的采集按键";
                return true;
            }

            string targetId;
            if (UnifiedHotkeyBindingIds.TryGetFeatureToggleTargetId(normalized, out targetId))
            {
                string featureTargetId;
                if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out featureTargetId))
                {
                    return false;
                }

                policy = FeatureTogglePolicy;
                ownerDisplayName = FeatureToggleHotkeyTargetCatalog.GetDisplayName(featureTargetId);
                return true;
            }

            int quickItemSlot;
            if (UnifiedHotkeyBindingIds.TryGetQuickItemSlotNumber(normalized, out quickItemSlot))
            {
                policy = QuickItemPolicy;
                ownerDisplayName = "快捷物品 " + quickItemSlot.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            string blueprintTargetId;
            if (UnifiedHotkeyBindingIds.TryGetBlueprintActionTargetId(normalized, out blueprintTargetId) &&
                TryGetBlueprintActionDisplayName(blueprintTargetId, out ownerDisplayName))
            {
                policy = BlueprintActionPolicy;
                return true;
            }

            return false;
        }

        private static bool TryGetBlueprintActionDisplayName(string targetId, out string displayName)
        {
            displayName = string.Empty;
            if (string.Equals(targetId, FeatureIds.BlueprintCreateAction, StringComparison.Ordinal))
            {
                displayName = "蓝图创建快捷键";
                return true;
            }

            if (string.Equals(targetId, FeatureIds.BlueprintSaveAction, StringComparison.Ordinal))
            {
                displayName = "蓝图保存快捷键";
                return true;
            }

            if (string.Equals(targetId, FeatureIds.BlueprintMoveAction, StringComparison.Ordinal))
            {
                displayName = "蓝图移动快捷键";
                return true;
            }

            if (string.Equals(targetId, FeatureIds.BlueprintRegionAction, StringComparison.Ordinal))
            {
                displayName = "蓝图区域修改快捷键";
                return true;
            }

            if (string.Equals(targetId, FeatureIds.BlueprintMirrorAction, StringComparison.Ordinal))
            {
                displayName = "蓝图镜像快捷键";
                return true;
            }

            if (string.Equals(targetId, FeatureIds.BlueprintLibraryAction, StringComparison.Ordinal))
            {
                displayName = "蓝图库打开快捷键";
                return true;
            }

            return false;
        }
    }
}
