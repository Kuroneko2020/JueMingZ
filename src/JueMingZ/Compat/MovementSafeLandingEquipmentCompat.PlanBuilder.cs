using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Movement;
using JueMingZ.Config;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
        // Build plans from verifiable inventory/equipment snapshots only;
        // unreadable containers must skip rescue rather than guess slots.
        public static bool TryBuildTemporaryEquipmentPlan(
            object player,
            AppSettings settings,
            MovementSafeLandingAnalysis analysis,
            out MovementSafeLandingEquipmentPlan plan,
            out string message)
        {
            plan = null;
            message = string.Empty;
            settings = settings ?? AppSettings.CreateDefault();
            if (player == null)
            {
                message = "playerUnavailable";
                return false;
            }

            if (analysis == null || !analysis.Dangerous)
            {
                message = "analysisNotDangerous";
                return false;
            }

            IList armor;
            if (!TryGetArmorItems(player, out armor) || armor == null)
            {
                message = "armorUnavailable";
                return false;
            }

            IList miscEquips;
            TryGetMiscEquipItems(player, out miscEquips);

            var sources = ScanSources(player, armor);
            if (sources.Count == 0)
            {
                message = "noTemporaryEquipmentCandidates";
                return false;
            }

            bool expertOrMaster = TryReadExpertOrMasterMode();
            if (TryBuildCategoryPlan(player, armor, miscEquips, sources, settings, analysis, expertOrMaster, "temporary_horseshoe", "horseshoe", "equip_only", TemporaryEquipmentPriority, false, out plan))
            {
                message = "temporaryHorseshoePlanReady";
                return true;
            }

            if (TryBuildCategoryPlan(player, armor, miscEquips, sources, settings, analysis, expertOrMaster, "temporary_wings", "wings", "equip_only", TemporaryEquipmentPriority, false, out plan))
            {
                message = "temporaryWingsPlanReady";
                return true;
            }

            if (TryBuildCategoryPlan(player, armor, miscEquips, sources, settings, analysis, expertOrMaster, "temporary_fairy_boots", "fairy_boots", "equip_only", TemporaryEquipmentPriority, false, out plan))
            {
                message = "temporaryFairyLegsPlanReady";
                return true;
            }

            if (TryBuildCategoryPlan(player, armor, miscEquips, sources, settings, analysis, expertOrMaster, "temporary_double_jump", "double_jump", "jump", TemporaryEquipmentPriority, true, out plan))
            {
                message = "temporaryDoubleJumpPlanReady";
                return true;
            }

            if (TryBuildCategoryPlan(player, armor, miscEquips, sources, settings, analysis, expertOrMaster, "temporary_rocket_boots", "rocket_boots", "jump", TemporaryEquipmentPriority, true, out plan))
            {
                message = "temporaryRocketBootsPlanReady";
                return true;
            }

            if (TryBuildCategoryPlan(player, armor, miscEquips, sources, settings, analysis, expertOrMaster, "temporary_flying_carpet", "flying_carpet", "jump", TemporaryEquipmentPriority, true, out plan))
            {
                message = "temporaryFlyingCarpetPlanReady";
                return true;
            }

            if (TryBuildCategoryPlan(player, armor, miscEquips, sources, settings, analysis, expertOrMaster, "temporary_gravity_globe", "gravity_globe", "gravity_flip", TemporaryEquipmentPriority, true, out plan))
            {
                message = "temporaryGravityGlobePlanReady";
                return true;
            }

            if (TryBuildCategoryPlan(player, armor, miscEquips, sources, settings, analysis, expertOrMaster, "temporary_flying_mount", "flying_mount", "quick_mount", TemporaryEquipmentPriority, true, out plan))
            {
                message = "temporaryFlyingMountPlanReady";
                return true;
            }

            if (TryBuildCategoryPlan(player, armor, miscEquips, sources, settings, analysis, expertOrMaster, "temporary_safe_mount", "safe_mount", "quick_mount", TemporaryEquipmentPriority, true, out plan))
            {
                message = "temporarySafeMountPlanReady";
                return true;
            }

            if (TryBuildUmbrellaPlan(player, sources, settings, analysis, out plan))
            {
                message = "temporaryUmbrellaPlanReady";
                return true;
            }

            message = "noUsableTemporaryEquipmentCandidate";
            return false;
        }

        [Obsolete("Priority 5 cushion block placement was abandoned; do not call. Use the teleport rod strategy instead.", false)]
        public static bool TryBuildCushionBlockHotbarPlan(
            object player,
            MovementSafeLandingAnalysis analysis,
            out MovementSafeLandingEquipmentPlan plan,
            out string message)
        {
            plan = null;
            message = string.Empty;
            if (player == null)
            {
                message = "playerUnavailable";
                return false;
            }

            if (analysis == null || !analysis.HasCushionBlock)
            {
                message = "cushionBlockUnavailable";
                return false;
            }

            var sourceSlot = analysis.CushionBlockInventorySlot;
            if (sourceSlot < 0)
            {
                message = "cushionBlockInventorySlotUnavailable";
                return false;
            }

            if (sourceSlot <= 9)
            {
                message = "cushionBlockAlreadyInHotbar";
                return false;
            }

            object sourceItem;
            if (!TryGetContainerItem(player, MovementSafeLandingEquipmentContainerKind.Inventory, sourceSlot, out sourceItem))
            {
                message = "cushionBlockSourceUnavailable";
                return false;
            }

            var sourceSignature = CreateSignature(sourceItem);
            if (sourceSignature.IsAir)
            {
                message = "cushionBlockSourceUnavailable";
                return false;
            }

            int sourceItemType;
            if (!TryReadItemType(sourceItem, out sourceItemType) || sourceItemType <= 0)
            {
                message = "cushionBlockSourceInvalid";
                return false;
            }

            if (analysis.CushionBlockItemType > 0 && sourceItemType != analysis.CushionBlockItemType)
            {
                message = "cushionBlockSourceChanged";
                return false;
            }

            int targetSlot;
            object targetItem;
            string targetReason;
            if (!TryResolveCushionBlockHotbarTarget(player, sourceSlot, out targetSlot, out targetItem, out targetReason))
            {
                message = targetReason;
                return false;
            }

            plan = new MovementSafeLandingEquipmentPlan
            {
                StrategyId = MovementSafeLandingStrategyIds.HotbarCushionBlock,
                EquipmentCategory = "cushion_block",
                ActionType = MovementSafeLandingActionTypes.BlockPlace,
                SelectedPriority = 5,
                SourceContainerKind = MovementSafeLandingEquipmentContainerKind.Inventory,
                SourceSlot = sourceSlot,
                TargetContainerKind = MovementSafeLandingEquipmentContainerKind.Hotbar,
                TargetSlot = targetSlot,
                CandidateItemType = sourceItemType,
                CandidateMountType = -1,
                CandidateSignature = sourceSignature,
                TargetSignatureAtPlan = CreateSignature(targetItem),
                ApplyTriggersInput = false,
                ApplyRocketRelease = false,
                SuppressDown = analysis.ControlDown,
                HoldTicks = 0,
                ImpactTicks = analysis.ImpactTicks,
                ImpactDistancePixels = analysis.ImpactDistancePixels,
                FallingSpeed = analysis.FallingSpeed,
                CapabilitySummary = analysis.ActiveCapabilitySummary ?? string.Empty
            };
            message = "cushionBlockHotbarSwapPlanReady:" + targetReason;
            return true;
        }

        private static bool TryBuildCategoryPlan(
            object player,
            IList armor,
            IList miscEquips,
            IList<SourceCandidate> sources,
            AppSettings settings,
            MovementSafeLandingAnalysis analysis,
            bool expertOrMasterMode,
            string strategyId,
            string category,
            string actionType,
            int selectedPriority,
            bool applyTriggersInput,
            out MovementSafeLandingEquipmentPlan plan)
        {
            plan = null;
            if (!IsCategoryEnabled(settings, category))
            {
                return false;
            }

            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (source == null || !CandidateMatchesCategory(source.Item, source.ItemType, category))
                {
                    continue;
                }

                if (!IsCandidateAllowedInCurrentMode(source.Item, expertOrMasterMode))
                {
                    continue;
                }

                MovementSafeLandingEquipmentContainerKind targetKind;
                int targetSlot;
                object targetItem;
                if (string.Equals(category, "flying_mount", StringComparison.Ordinal) ||
                    string.Equals(category, "safe_mount", StringComparison.Ordinal))
                {
                    if (!TryGetItemAt(miscEquips, MountMiscEquipSlot, out targetItem))
                    {
                        targetItem = CreateAirLike(source.Item);
                    }

                    targetKind = MovementSafeLandingEquipmentContainerKind.MiscEquip;
                    targetSlot = MountMiscEquipSlot;
                }
                else if (string.Equals(category, "fairy_boots", StringComparison.Ordinal))
                {
                    if (!TryFindFixedEquipmentTargetSlot(player, armor, LegEquipmentSlot, out targetItem))
                    {
                        continue;
                    }

                    targetKind = MovementSafeLandingEquipmentContainerKind.Accessory;
                    targetSlot = LegEquipmentSlot;
                }
                else
                {
                    if (!TryFindAccessoryTargetSlot(player, armor, source.Item, out targetSlot, out targetItem))
                    {
                        continue;
                    }

                    targetKind = MovementSafeLandingEquipmentContainerKind.Accessory;
                }

                plan = new MovementSafeLandingEquipmentPlan
                {
                    StrategyId = strategyId,
                    EquipmentCategory = category,
                    ActionType = actionType,
                    SelectedPriority = selectedPriority,
                    SourceContainerKind = source.Kind,
                    SourceSlot = source.Slot,
                    TargetContainerKind = targetKind,
                    TargetSlot = targetSlot,
                    CandidateItemType = source.ItemType,
                    CandidateMountType = source.MountType,
                    CandidateSignature = source.Signature,
                    TargetSignatureAtPlan = CreateSignature(targetItem),
                    ApplyTriggersInput = applyTriggersInput,
                    ApplyRocketRelease = string.Equals(category, "rocket_boots", StringComparison.Ordinal),
                    SuppressDown = analysis.ControlDown,
                    HoldTicks = ResolveHoldTicks(category, actionType),
                    ImpactTicks = analysis.ImpactTicks,
                    ImpactDistancePixels = analysis.ImpactDistancePixels,
                    FallingSpeed = analysis.FallingSpeed,
                    CapabilitySummary = analysis.ActiveCapabilitySummary ?? string.Empty
                };
                return true;
            }

            return false;
        }

        private static bool TryBuildUmbrellaPlan(
            object player,
            IList<SourceCandidate> sources,
            AppSettings settings,
            MovementSafeLandingAnalysis analysis,
            out MovementSafeLandingEquipmentPlan plan)
        {
            plan = null;
            if (!IsCategoryEnabled(settings, "umbrella") || player == null || sources == null)
            {
                return false;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) || selectedSlot < 0 || selectedSlot > 9)
            {
                return false;
            }

            object selectedItem;
            if (!TryGetContainerItem(player, MovementSafeLandingEquipmentContainerKind.Inventory, selectedSlot, out selectedItem))
            {
                selectedItem = null;
            }

            int selectedItemType;
            if (TryReadItemType(selectedItem, out selectedItemType) &&
                CandidateMatchesCategory(selectedItem, selectedItemType, "umbrella"))
            {
                return false;
            }

            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (source == null ||
                    source.Kind != MovementSafeLandingEquipmentContainerKind.Inventory ||
                    source.Slot == selectedSlot ||
                    !CandidateMatchesCategory(source.Item, source.ItemType, "umbrella"))
                {
                    continue;
                }

                plan = new MovementSafeLandingEquipmentPlan
                {
                    StrategyId = "temporary_umbrella",
                    EquipmentCategory = "umbrella",
                    ActionType = "equip_only",
                    SelectedPriority = TemporaryUmbrellaPriority,
                    SourceContainerKind = source.Kind,
                    SourceSlot = source.Slot,
                    TargetContainerKind = MovementSafeLandingEquipmentContainerKind.Hotbar,
                    TargetSlot = selectedSlot,
                    CandidateItemType = source.ItemType,
                    CandidateMountType = source.MountType,
                    CandidateSignature = source.Signature,
                    TargetSignatureAtPlan = CreateSignature(selectedItem),
                    ApplyTriggersInput = false,
                    ApplyRocketRelease = false,
                    SuppressDown = analysis != null && analysis.ControlDown,
                    HoldTicks = 0,
                    ImpactTicks = analysis == null ? -1f : analysis.ImpactTicks,
                    ImpactDistancePixels = analysis == null ? -1 : analysis.ImpactDistancePixels,
                    FallingSpeed = analysis == null ? 0f : analysis.FallingSpeed,
                    CapabilitySummary = analysis == null ? string.Empty : analysis.ActiveCapabilitySummary ?? string.Empty
                };
                return true;
            }

            return false;
        }

        private static bool TryResolveCushionBlockHotbarTarget(
            object player,
            int sourceSlot,
            out int targetSlot,
            out object targetItem,
            out string reason)
        {
            targetSlot = -1;
            targetItem = null;
            reason = "cushionBlockHotbarTargetUnavailable";
            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            for (var slot = 0; slot < 10; slot++)
            {
                if (slot == sourceSlot)
                {
                    continue;
                }

                object item;
                if (!TryGetContainerItem(player, MovementSafeLandingEquipmentContainerKind.Hotbar, slot, out item))
                {
                    continue;
                }

                if (CreateSignature(item).IsAir)
                {
                    targetSlot = slot;
                    targetItem = item;
                    reason = "emptyHotbarSlot:" + slot.ToString(CultureInfo.InvariantCulture);
                    return true;
                }
            }

            int selectedSlot;
            if (TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) &&
                selectedSlot >= 0 &&
                selectedSlot <= 9 &&
                selectedSlot != sourceSlot &&
                TryGetContainerItem(player, MovementSafeLandingEquipmentContainerKind.Hotbar, selectedSlot, out targetItem))
            {
                targetSlot = selectedSlot;
                reason = "selectedHotbarSlot:" + selectedSlot.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            for (var slot = 0; slot < 10; slot++)
            {
                if (slot == sourceSlot)
                {
                    continue;
                }

                if (TryGetContainerItem(player, MovementSafeLandingEquipmentContainerKind.Hotbar, slot, out targetItem))
                {
                    targetSlot = slot;
                    reason = "fallbackHotbarSlot:" + slot.ToString(CultureInfo.InvariantCulture);
                    return true;
                }
            }

            return false;
        }

        private static bool IsCategoryEnabled(AppSettings settings, string category)
        {
            if (string.Equals(category, "double_jump", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.DoubleJump);
            }

            if (string.Equals(category, "rocket_boots", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.RocketBoots);
            }

            if (string.Equals(category, "flying_carpet", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.FlyingCarpet);
            }

            if (string.Equals(category, "fairy_boots", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.FairyBoots);
            }

            if (string.Equals(category, "wings", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.Wings);
            }

            if (string.Equals(category, "horseshoe", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.Horseshoe);
            }

            if (string.Equals(category, "flying_mount", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.FlyingMount);
            }

            if (string.Equals(category, "safe_mount", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.DamageReductionMount);
            }

            if (string.Equals(category, "gravity_globe", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.GravityGlobe);
            }

            if (string.Equals(category, "umbrella", StringComparison.Ordinal))
            {
                return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.Umbrella);
            }

            return false;
        }

        private static int ResolveHoldTicks(string category, string actionType)
        {
            if (string.Equals(actionType, "quick_mount", StringComparison.Ordinal))
            {
                return 3;
            }

            if (string.Equals(category, "double_jump", StringComparison.Ordinal))
            {
                return 2;
            }

            if (string.Equals(category, "rocket_boots", StringComparison.Ordinal))
            {
                return 16;
            }

            if (string.Equals(category, "flying_carpet", StringComparison.Ordinal))
            {
                return 12;
            }

            if (string.Equals(actionType, "gravity_flip", StringComparison.Ordinal))
            {
                return 4;
            }

            return 0;
        }

        private static bool IsCandidateAllowedInCurrentMode(object item, bool expertOrMasterMode)
        {
            bool expertOnly;
            return !TryReadItemBool(item, "expertOnly", out expertOnly) || !expertOnly || expertOrMasterMode;
        }

        private static bool TryFindFixedEquipmentTargetSlot(object player, IList armor, int fixedSlot, out object targetItem)
        {
            targetItem = null;
            if (player == null || armor == null || fixedSlot < 0 || fixedSlot >= Math.Min(MaxEquipmentSlotExclusive, GetCollectionCount(armor)))
            {
                return false;
            }

            if (!TryIsTargetEquipmentSlotUsable(player, fixedSlot))
            {
                return false;
            }

            targetItem = GetIndexed(armor, fixedSlot);
            return true;
        }

        private static bool TryFindAccessoryTargetSlot(object player, IList armor, object candidateItem, out int slot, out object targetItem)
        {
            slot = -1;
            targetItem = null;
            if (player == null || armor == null || candidateItem == null)
            {
                return false;
            }

            var count = Math.Min(MaxEquipmentSlotExclusive, GetCollectionCount(armor));
            for (var index = FirstAccessorySlot; index < count; index++)
            {
                if (!TryIsTargetAccessorySlotUsable(player, index))
                {
                    continue;
                }

                var item = GetIndexed(armor, index);
                if (CreateSignature(item).IsAir)
                {
                    slot = index;
                    targetItem = item;
                    return true;
                }
            }

            for (var index = FirstAccessorySlot; index < count; index++)
            {
                if (!TryIsTargetAccessorySlotUsable(player, index))
                {
                    continue;
                }

                slot = index;
                targetItem = GetIndexed(armor, index);
                return true;
            }

            return false;
        }

        private static bool TryIsTargetEquipmentSlotUsable(object player, int slot)
        {
            if (player == null || slot < 0 || slot >= MaxEquipmentSlotExclusive)
            {
                return false;
            }

            if (slot <= 2)
            {
                return true;
            }

            try
            {
                var method = FindInstanceMethod(player.GetType(), "IsItemSlotUnlockedAndUsable", typeof(int));
                if (method != null)
                {
                    return Convert.ToBoolean(method.Invoke(player, new object[] { slot }), CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                return false;
            }

            return slot <= 2 || slot <= 7;
        }

        private static bool TryIsTargetAccessorySlotUsable(object player, int slot)
        {
            if (player == null || slot < FirstAccessorySlot || slot >= MaxEquipmentSlotExclusive)
            {
                return false;
            }

            return TryIsTargetEquipmentSlotUsable(player, slot);
        }
    }
}
