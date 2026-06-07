using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using JueMingZ.Automation.Movement;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    internal static class MovementSafeLandingEquipmentCompat
    {
        // Temporary equipment plans are reversible mutations; every apply record
        // must be restorable or the rescue path should skip.
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const int LegEquipmentSlot = 2;
        private const int FirstAccessorySlot = 3;
        private const int MaxEquipmentSlotExclusive = 10;
        private const int FirstSocialArmorSlot = 10;
        private const int FirstSocialAccessorySlot = 13;
        private const int MountMiscEquipSlot = 3;
        private const int TemporaryEquipmentPriority = 2;
        private const int TemporaryUmbrellaPriority = 3;
        private const int TemporaryRocketBootsPrimeRocketTime = 20;
        private const int TemporaryFlyingCarpetPrimeTime = 20;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Guid, MovementSafeLandingEquipmentPlan> ApplyPlans = new Dictionary<Guid, MovementSafeLandingEquipmentPlan>();
        private static readonly Dictionary<Guid, MovementSafeLandingEquipmentActionResult> ApplyResults = new Dictionary<Guid, MovementSafeLandingEquipmentActionResult>();
        private static readonly Dictionary<Guid, RestoreRequest> RestoreRequests = new Dictionary<Guid, RestoreRequest>();
        private static readonly Dictionary<Guid, MovementSafeLandingEquipmentActionResult> RestoreResults = new Dictionary<Guid, MovementSafeLandingEquipmentActionResult>();
        private static readonly Dictionary<string, int> ItemIdCache = new Dictionary<string, int>(StringComparer.Ordinal);
        private static bool _applyEquipFunctionalResolved;
        private static MethodInfo _applyEquipFunctionalMethod;
        private static bool _refreshDoubleJumpsResolved;
        private static MethodInfo _refreshDoubleJumpsMethod;

        private sealed class SourceCandidate
        {
            public MovementSafeLandingEquipmentContainerKind Kind;
            public int Slot;
            public int SourcePriority;
            public object Item;
            public int ItemType;
            public int MountType;
            public MovementSafeLandingEquipmentItemSignature Signature;
        }

        private sealed class RestoreRequest
        {
            public List<MovementSafeLandingEquipmentMoveRecord> Records;
            public string Reason;
        }

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

        public static void RegisterApplyPlan(Guid requestId, MovementSafeLandingEquipmentPlan plan)
        {
            if (requestId == Guid.Empty || plan == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                ApplyPlans[requestId] = ClonePlan(plan);
            }
        }

        public static void RegisterRestoreRequest(Guid requestId, IList<MovementSafeLandingEquipmentMoveRecord> records, string reason)
        {
            if (requestId == Guid.Empty || records == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                RestoreRequests[requestId] = new RestoreRequest
                {
                    Records = CopyRecords(records),
                    Reason = reason ?? string.Empty
                };
            }
        }

        // Apply consumes a registered plan exactly once and verifies source and
        // target signatures before writing any temporary equipment swap.
        public static bool TryApplyRegisteredPlan(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            MovementSafeLandingEquipmentPlan plan;
            lock (SyncRoot)
            {
                if (!ApplyPlans.TryGetValue(requestId, out plan))
                {
                    result = BuildResult("applySkipped", "applyPlanUnavailable", "Safe landing temporary equipment apply plan unavailable.");
                    ApplyResults[requestId] = result;
                    return false;
                }

                ApplyPlans.Remove(requestId);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                result = BuildResult("applySkipped", "playerUnavailable", "Local player unavailable for safe landing temporary equipment apply.");
                StoreApplyResult(requestId, result);
                return false;
            }

            if (TryIsMouseItemPresent())
            {
                result = BuildResult("applyBlocked", "blockedByMouseItem", "Safe landing temporary equipment apply blocked because Main.mouseItem is not empty.");
                result.BlockedByMouseItem = true;
                StoreApplyResult(requestId, result);
                return false;
            }

            result = ApplyPlan(player, plan);
            StoreApplyResult(requestId, result);
            return result.AppliedMoveCount > 0;
        }

        // Restore records are the recovery anchor; blocked restores keep the
        // records pending instead of claiming success.
        public static bool TryRestoreRegisteredRecords(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            RestoreRequest request;
            lock (SyncRoot)
            {
                if (!RestoreRequests.TryGetValue(requestId, out request))
                {
                    result = BuildResult("restoreSkipped", "restoreRequestUnavailable", "Safe landing temporary equipment restore request unavailable.");
                    RestoreResults[requestId] = result;
                    return false;
                }

                RestoreRequests.Remove(requestId);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                result = BuildResult("restoreSkipped", "playerUnavailable", "Local player unavailable for safe landing temporary equipment restore.");
                result.Records.AddRange(CopyRecords(request.Records));
                result.PendingRestoreCount = result.Records.Count;
                StoreRestoreResult(requestId, result);
                return false;
            }

            if (TryIsMouseItemPresent())
            {
                result = BuildResult("restoreBlocked", "blockedByMouseItem", "Safe landing temporary equipment restore blocked because Main.mouseItem is not empty.");
                result.BlockedByMouseItem = true;
                result.Records.AddRange(CopyRecords(request.Records));
                result.PendingRestoreCount = result.Records.Count;
                StoreRestoreResult(requestId, result);
                return false;
            }

            result = RestoreRecords(player, request);
            StoreRestoreResult(requestId, result);
            return result.PendingRestoreCount == 0;
        }

        public static bool TryPeekApplyResult(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            if (requestId == Guid.Empty)
            {
                return false;
            }

            lock (SyncRoot)
            {
                return ApplyResults.TryGetValue(requestId, out result);
            }
        }

        public static bool TryTakeApplyResult(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            if (requestId == Guid.Empty)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (!ApplyResults.TryGetValue(requestId, out result))
                {
                    return false;
                }

                ApplyResults.Remove(requestId);
                return true;
            }
        }

        public static bool TryTakeRestoreResult(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            if (requestId == Guid.Empty)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (!RestoreResults.TryGetValue(requestId, out result))
                {
                    return false;
                }

                RestoreResults.Remove(requestId);
                return true;
            }
        }

        public static void UpdateApplyPulseResult(Guid requestId, SafeLandingJumpPulseSnapshot pulse, bool notRequired)
        {
            if (requestId == Guid.Empty)
            {
                return;
            }

            lock (SyncRoot)
            {
                MovementSafeLandingEquipmentActionResult result;
                if (!ApplyResults.TryGetValue(requestId, out result) || result == null)
                {
                    return;
                }

                result.PulseNotRequired = notRequired;
                if (pulse != null)
                {
                    result.PulseQueued = true;
                    result.PulseCompleted = pulse.Completed;
                    result.PulseFailed = pulse.Failed;
                    result.PulseStatus = pulse.Status ?? string.Empty;
                    result.PulsePhase = pulse.Phase ?? string.Empty;
                    result.PulseApplySite = pulse.LastApplySite ?? string.Empty;
                    result.PulseMessage = pulse.LastMessage ?? string.Empty;
                }
            }
        }

        public static bool TryIsMouseItemPresent()
        {
            object mouseItem;
            if (!TryGetStaticMember(TerrariaRuntimeTypes.MainType, "mouseItem", out mouseItem) || mouseItem == null)
            {
                return false;
            }

            return !CreateSignature(mouseItem).IsAir;
        }

        public static bool TryIsSafeToRestoreTemporaryEquipment(object player, out bool safeToRestore, out string reason)
        {
            safeToRestore = false;
            reason = string.Empty;
            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            var fallingSpeed = profile.VelocityY * (Math.Abs(profile.GravityDirection) > 0.001f ? profile.GravityDirection : 1f);
            if (fallingSpeed > 1f)
            {
                reason = "stillFalling";
                return true;
            }

            string groundedReason;
            if (TryIsActuallyGroundedForRestore(player, profile, out groundedReason))
            {
                safeToRestore = true;
                reason = groundedReason;
                return true;
            }

            reason = fallingSpeed < -1f ? "risingAfterRescue" : "waitingForGrounded";
            return true;
        }

        public static string ContainerKindName(MovementSafeLandingEquipmentContainerKind kind)
        {
            switch (kind)
            {
                case MovementSafeLandingEquipmentContainerKind.Inventory:
                    return "Inventory";
                case MovementSafeLandingEquipmentContainerKind.SocialAccessory:
                    return "SocialAccessory";
                case MovementSafeLandingEquipmentContainerKind.Accessory:
                    return "Accessory";
                case MovementSafeLandingEquipmentContainerKind.MiscEquip:
                    return "MiscEquip";
                case MovementSafeLandingEquipmentContainerKind.SocialArmor:
                    return "SocialArmor";
                case MovementSafeLandingEquipmentContainerKind.Hotbar:
                    return "Hotbar";
                default:
                    return "Unknown";
            }
        }

        public static MovementSafeLandingEquipmentItemSignature CreateSignature(object item)
        {
            var signature = new MovementSafeLandingEquipmentItemSignature();
            if (item == null)
            {
                return signature;
            }

            int value;
            signature.Type = TryReadItemInt(item, "type", out value) ? value : 0;
            signature.Stack = TryReadItemInt(item, "stack", out value) ? value : 0;
            signature.Prefix = TryReadItemInt(item, "prefix", out value) ? value : 0;
            var rawName = GetMember(item, "Name") ?? GetMember(item, "HoverName") ?? GetMember(item, "name");
            signature.Name = rawName == null ? string.Empty : rawName.ToString();
            return signature;
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

        private static MovementSafeLandingEquipmentActionResult ApplyPlan(object player, MovementSafeLandingEquipmentPlan plan)
        {
            var result = BuildResult("applyAttempted", string.Empty, "Safe landing temporary equipment apply attempted.");
            result.Invoked = true;
            if (plan == null)
            {
                result.Decision = "applySkipped";
                result.SkipReason = "planUnavailable";
                result.Message = "No safe landing temporary equipment plan was available.";
                return result;
            }

            result.StrategyId = plan.StrategyId;
            result.EquipmentCategory = plan.EquipmentCategory;
            result.ActionType = plan.ActionType;

            object sourceItem;
            if (!TryGetContainerItem(player, plan.SourceContainerKind, plan.SourceSlot, out sourceItem) ||
                !SignatureMatches(sourceItem, plan.CandidateSignature))
            {
                result.Decision = "applySkipped";
                result.SkipReason = "sourceItemChanged";
                result.Message = "Safe landing temporary equipment source item changed before apply.";
                result.SkippedMoveCount++;
                return result;
            }

            if (ShouldSelectTargetOnApply(plan))
            {
                result.SelectedSlotApplyAttempted = true;
            }

            object targetItem;
            if (!TryGetContainerItem(player, plan.TargetContainerKind, plan.TargetSlot, out targetItem))
            {
                targetItem = CreateAirLike(sourceItem);
            }

            if (!SignatureMatches(targetItem, plan.TargetSignatureAtPlan))
            {
                result.Decision = "applySkipped";
                result.SkipReason = "targetItemChanged";
                result.Message = "Safe landing temporary equipment target slot changed before apply.";
                result.SkippedMoveCount++;
                return result;
            }

            var targetSignature = CreateSignature(targetItem);
            var replacementForSource = targetItem ?? CreateAirLike(sourceItem);
            if (!SetContainerItem(player, plan.TargetContainerKind, plan.TargetSlot, sourceItem) ||
                !SetContainerItem(player, plan.SourceContainerKind, plan.SourceSlot, replacementForSource))
            {
                result.Decision = "applySkipped";
                result.SkipReason = "swapWriteFailed";
                result.Message = "Safe landing temporary equipment swap write failed.";
                result.SkippedMoveCount++;
                return result;
            }

            object writtenTarget;
            if (!TryGetContainerItem(player, plan.TargetContainerKind, plan.TargetSlot, out writtenTarget) ||
                !SignatureMatches(writtenTarget, plan.CandidateSignature))
            {
                result.Decision = "applySkipped";
                result.SkipReason = "targetVerificationFailed";
                result.Message = "Safe landing temporary equipment target verification failed.";
                result.SkippedMoveCount++;
                return result;
            }

            if (ShouldSelectTargetOnApply(plan))
            {
                string selectedSlotMessage;
                if (!TrySelectTemporaryHeldTarget(player, plan.TargetSlot, out selectedSlotMessage))
                {
                    SetContainerItem(player, plan.SourceContainerKind, plan.SourceSlot, sourceItem);
                    SetContainerItem(player, plan.TargetContainerKind, plan.TargetSlot, targetItem);
                    result.SelectedSlotMessage = selectedSlotMessage;
                    result.Decision = "applySkipped";
                    result.SkipReason = "selectedSlotWriteFailed";
                    result.Message = "Safe landing temporary held item selection failed after swap; swap was rolled back. " + selectedSlotMessage;
                    result.SkippedMoveCount++;
                    return result;
                }

                result.SelectedSlotApplySucceeded = true;
                result.SelectedSlotMessage = selectedSlotMessage;
            }

            result.AppliedMoveCount = 1;
            result.Decision = "applySucceeded";
            result.Message = "Safe landing temporary equipment applied: " + plan.StrategyId + ".";
            ApplyFunctionalRefreshForTemporaryEquipment(player, plan, writtenTarget, result);
            VerifyPostApplyCapability(player, plan, result);
            result.Records.Add(new MovementSafeLandingEquipmentMoveRecord
            {
                StrategyId = plan.StrategyId,
                EquipmentCategory = plan.EquipmentCategory,
                ActionType = plan.ActionType,
                SelectedPriority = plan.SelectedPriority,
                SourceContainerKind = plan.SourceContainerKind,
                SourceSlot = plan.SourceSlot,
                TargetContainerKind = plan.TargetContainerKind,
                TargetSlot = plan.TargetSlot,
                CandidateItemType = plan.CandidateItemType,
                CandidateMountType = plan.CandidateMountType,
                RescueItemSignature = CloneSignature(plan.CandidateSignature),
                OriginalTargetWasAir = targetSignature.IsAir,
                OriginalTargetItemSignature = targetSignature,
                OriginalTargetHoldingContainerKind = plan.SourceContainerKind,
                OriginalTargetHoldingSlot = plan.SourceSlot,
                ImpactTicks = plan.ImpactTicks,
                ImpactDistancePixels = plan.ImpactDistancePixels,
                FallingSpeed = plan.FallingSpeed,
                PostApplyCapabilityObserved = IsVerifiedTemporaryActivationCapability(result.PostApplyVerificationReason),
                PostApplyVerificationReason = result.PostApplyVerificationReason ?? string.Empty,
                ApplyStatus = "applied",
                RestoreStatus = "pending"
            });
            result.PendingRestoreCount = result.Records.Count;
            result.PulseNotRequired = !plan.ApplyTriggersInput;
            return result;
        }

        private static void ApplyFunctionalRefreshForTemporaryEquipment(
            object player,
            MovementSafeLandingEquipmentPlan plan,
            object equippedItem,
            MovementSafeLandingEquipmentActionResult result)
        {
            if (result == null || plan == null)
            {
                return;
            }

            var messages = new List<string>();
            if (plan.TargetContainerKind == MovementSafeLandingEquipmentContainerKind.Accessory)
            {
                result.FunctionalRefreshAttempted = true;
                string functionalMessage;
                result.FunctionalRefreshSucceeded = TryInvokeApplyEquipFunctional(player, plan.TargetSlot, equippedItem, out functionalMessage);
                messages.Add(functionalMessage);
            }
            else
            {
                messages.Add("functionalRefreshSkipped:targetKind=" + ContainerKindName(plan.TargetContainerKind));
            }

            if (string.Equals(plan.EquipmentCategory, "double_jump", StringComparison.OrdinalIgnoreCase))
            {
                result.DoubleJumpRefreshAttempted = true;
                string doubleJumpMessage;
                result.DoubleJumpRefreshSucceeded = TryInvokeRefreshDoubleJumps(player, out doubleJumpMessage);
                messages.Add(doubleJumpMessage);
            }

            if (string.Equals(plan.EquipmentCategory, "rocket_boots", StringComparison.OrdinalIgnoreCase))
            {
                string rocketBootsMessage;
                var rocketBootsPrimed = TryPrimeTemporaryRocketBootsFlightTime(player, out rocketBootsMessage);
                messages.Add((rocketBootsPrimed ? "rocketBootsTimerPrimeSucceeded:" : "rocketBootsTimerPrimeSkipped:") + rocketBootsMessage);
            }

            if (string.Equals(plan.EquipmentCategory, "flying_carpet", StringComparison.OrdinalIgnoreCase))
            {
                string carpetMessage;
                var carpetPrimed = TryPrimeTemporaryFlyingCarpet(player, out carpetMessage);
                messages.Add((carpetPrimed ? "flyingCarpetPrimeSucceeded:" : "flyingCarpetPrimeSkipped:") + carpetMessage);
            }

            if (string.Equals(plan.EquipmentCategory, "gravity_globe", StringComparison.OrdinalIgnoreCase))
            {
                string gravityMessage;
                var gravityPrimed = TryPrimeTemporaryGravityGlobe(player, out gravityMessage);
                messages.Add((gravityPrimed ? "gravityGlobePrimeSucceeded:" : "gravityGlobePrimeSkipped:") + gravityMessage);
            }

            result.FunctionalRefreshMessage = string.Join(";", messages.ToArray());
        }

        private static bool TryPrimeTemporaryRocketBootsFlightTime(object player, out string message)
        {
            message = string.Empty;
            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                message = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.PlayerControllable || !profile.AerialJumpWindow)
            {
                message = "notInAerialActivationWindow";
                return false;
            }

            if (profile.RocketBoots <= 0)
            {
                message = "rocketBootsUnavailable";
                return false;
            }

            if (profile.CanUseBootFlyingAbilitiesKnown && !profile.CanUseBootFlyingAbilities)
            {
                message = "canUseBootFlyingAbilitiesFalse";
                return false;
            }

            if (profile.RocketDelay > 0)
            {
                message = "rocketDelayActive";
                return false;
            }

            if (!profile.CanRocket)
            {
                message = "canRocketFalse";
                return false;
            }

            if (profile.RocketTime > 0f)
            {
                message = "rocketTimeAlreadyAvailable:" + profile.RocketTime.ToString("0.###", CultureInfo.InvariantCulture);
                return true;
            }

            if (!TrySetMember(player, "rocketTime", TemporaryRocketBootsPrimeRocketTime))
            {
                message = "rocketTimeSetFailed";
                return false;
            }

            message = "controlledLocalRocketTimePrime:" + TemporaryRocketBootsPrimeRocketTime.ToString("0.###", CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryPrimeTemporaryFlyingCarpet(object player, out string message)
        {
            message = string.Empty;
            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                message = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.PlayerControllable || !profile.AerialJumpWindow)
            {
                message = "notInAerialActivationWindow";
                return false;
            }

            var ok = true;
            if (!profile.HasFlyingCarpet)
            {
                ok &= TrySetMember(player, "carpet", true);
            }

            if (!profile.FlyingCarpetCanStart)
            {
                ok &= TrySetMember(player, "canCarpet", true);
            }

            if (profile.FlyingCarpetTime <= 0)
            {
                ok &= TrySetMember(player, "carpetTime", TemporaryFlyingCarpetPrimeTime);
            }

            message = ok
                ? "controlledLocalFlyingCarpetPrime:" + TemporaryFlyingCarpetPrimeTime.ToString(CultureInfo.InvariantCulture)
                : "flyingCarpetPrimeSetFailed";
            return ok;
        }

        private static bool TryPrimeTemporaryGravityGlobe(object player, out string message)
        {
            message = string.Empty;
            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                message = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.PlayerControllable || !profile.AerialJumpWindow)
            {
                message = "notInAerialActivationWindow";
                return false;
            }

            if (profile.HasGravityGlobe)
            {
                message = "gravityGlobeAlreadyAvailable";
                return true;
            }

            if (!TrySetMember(player, "gravControl2", true))
            {
                message = "gravityGlobeSetFailed";
                return false;
            }

            message = "controlledLocalGravityGlobePrime";
            return true;
        }

        private static void VerifyPostApplyCapability(
            object player,
            MovementSafeLandingEquipmentPlan plan,
            MovementSafeLandingEquipmentActionResult result)
        {
            if (result == null || plan == null)
            {
                return;
            }

            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                result.PostApplyVerificationReason = "jumpProfileUnavailable";
                result.PostApplyVerificationSummary = "postApplyVerification=unavailable,reason=jumpProfileUnavailable";
                return;
            }

            var snapshot = MovementSafeLandingCapabilitySnapshot.FromJumpProfile(profile);
            result.PostApplyRocketBoots = snapshot.RocketBoots;
            result.PostApplyRocketTime = snapshot.RocketTime;
            result.PostApplyRocketDelay = snapshot.RocketDelay;
            result.PostApplyCanRocket = snapshot.CanRocket;
            result.PostApplyCanRocketKnown = snapshot.CanRocketKnown;
            result.PostApplyRocketRelease = snapshot.RocketRelease;
            result.PostApplyCanUseBootFlyingAbilities = snapshot.CanUseBootFlyingAbilities;
            result.PostApplyCanUseBootFlyingAbilitiesKnown = snapshot.CanUseBootFlyingAbilitiesKnown;
            result.PostApplyHasRocketBootsAvailable = snapshot.HasRocketBootsAvailable;
            result.PostApplyHasFlyingCarpet = snapshot.HasFlyingCarpet;
            result.PostApplyHasFlyingCarpetAvailable = snapshot.HasFlyingCarpetAvailable;
            result.PostApplyFlyingCarpetCanStart = profile.FlyingCarpetCanStart;
            result.PostApplyFlyingCarpetTime = snapshot.FlyingCarpetTime;
            result.PostApplyAirJumpFlagCount = snapshot.AirJumpFlagCount;
            result.PostApplyHasGravityGlobe = snapshot.HasGravityGlobe;
            result.PostApplyHasGravityFlipOpportunity = snapshot.HasGravityFlipOpportunity;
            result.PostApplyAerialJumpWindow = profile.AerialJumpWindow;
            result.PostApplyGravityDirection = snapshot.GravityDirection;
            result.PostApplyHasWingFlight = snapshot.HasWingFlight;
            result.PostApplyWingsLogic = snapshot.WingsLogic;
            result.PostApplyWingTime = snapshot.WingTime;
            result.PostApplyVerificationReason = ResolvePostApplyVerificationReason(plan, snapshot, profile);

            var builder = new StringBuilder();
            AppendPart(builder, "category=" + (plan.EquipmentCategory ?? string.Empty));
            AppendPart(builder, "reason=" + result.PostApplyVerificationReason);
            AppendPart(builder, "applyEquipFunctionalAttempted=" + Bool(result.FunctionalRefreshAttempted));
            AppendPart(builder, "applyEquipFunctionalSucceeded=" + Bool(result.FunctionalRefreshSucceeded));
            AppendPart(builder, "functionalMessage=" + (result.FunctionalRefreshMessage ?? string.Empty));
            AppendPart(builder, "doubleJumpRefreshAttempted=" + Bool(result.DoubleJumpRefreshAttempted));
            AppendPart(builder, "doubleJumpRefreshSucceeded=" + Bool(result.DoubleJumpRefreshSucceeded));
            AppendPart(builder, "airJumpFlagCount=" + snapshot.AirJumpFlagCount.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "rocketBoots=" + snapshot.RocketBoots.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "rocketTime=" + snapshot.RocketTime.ToString("0.###", CultureInfo.InvariantCulture));
            AppendPart(builder, "rocketDelay=" + snapshot.RocketDelay.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "canRocket=" + Bool(snapshot.CanRocket));
            AppendPart(builder, "canRocketKnown=" + Bool(snapshot.CanRocketKnown));
            AppendPart(builder, "rocketRelease=" + Bool(snapshot.RocketRelease));
            AppendPart(builder, "canUseBootFlyingAbilities=" + Bool(snapshot.CanUseBootFlyingAbilities));
            AppendPart(builder, "canUseBootFlyingAbilitiesKnown=" + Bool(snapshot.CanUseBootFlyingAbilitiesKnown));
            AppendPart(builder, "hasRocketBootsAvailable=" + Bool(snapshot.HasRocketBootsAvailable));
            AppendPart(builder, "hasFlyingCarpet=" + Bool(snapshot.HasFlyingCarpet));
            AppendPart(builder, "hasFlyingCarpetAvailable=" + Bool(snapshot.HasFlyingCarpetAvailable));
            AppendPart(builder, "flyingCarpetCanStart=" + Bool(profile.FlyingCarpetCanStart));
            AppendPart(builder, "flyingCarpetTime=" + snapshot.FlyingCarpetTime.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "hasGravityGlobe=" + Bool(snapshot.HasGravityGlobe));
            AppendPart(builder, "hasGravityFlipOpportunity=" + Bool(snapshot.HasGravityFlipOpportunity));
            AppendPart(builder, "aerialJumpWindow=" + Bool(profile.AerialJumpWindow));
            AppendPart(builder, "gravityDirection=" + snapshot.GravityDirection.ToString("0.###", CultureInfo.InvariantCulture));
            AppendPart(builder, "hasWingFlight=" + Bool(snapshot.HasWingFlight));
            AppendPart(builder, "wingsLogic=" + snapshot.WingsLogic.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "wingTime=" + snapshot.WingTime.ToString("0.###", CultureInfo.InvariantCulture));
            result.PostApplyVerificationSummary = builder.ToString();
        }

        private static string ResolvePostApplyVerificationReason(
            MovementSafeLandingEquipmentPlan plan,
            MovementSafeLandingCapabilitySnapshot snapshot,
            JumpInputProfile profile)
        {
            // Verification reasons describe observed capability after apply,
            // not proof that a guessed equipment write succeeded.
            var category = plan == null ? string.Empty : plan.EquipmentCategory ?? string.Empty;
            if (string.Equals(category, "rocket_boots", StringComparison.OrdinalIgnoreCase))
            {
                if (snapshot.HasRocketBootsAvailable)
                {
                    return "rocketBootsAvailable";
                }

                if (snapshot.RocketBoots <= 0)
                {
                    return "rocketBootsUnavailableAfterApply";
                }

                if (snapshot.RocketTime <= 0f)
                {
                    return "rocketTimeUnavailableAfterApply";
                }

                if (snapshot.RocketDelay > 0)
                {
                    return "rocketDelayActive";
                }

                if (!snapshot.CanRocket)
                {
                    return "canRocketFalse";
                }

                if (snapshot.RocketRelease)
                {
                    return "rocketReleaseNotPrimed";
                }

                if (snapshot.CanUseBootFlyingAbilitiesKnown && !snapshot.CanUseBootFlyingAbilities)
                {
                    return "canUseBootFlyingAbilitiesFalse";
                }

                return "rocketBootsUnavailableAfterApply";
            }

            if (string.Equals(category, "gravity_globe", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasGravityFlipOpportunity
                    ? "gravityRestorePendingExpected"
                    : snapshot.HasGravityGlobe ? "gravityGlobePresentButNoFlipOpportunity" : "gravityGlobeUnavailableAfterApply";
            }

            if (string.Equals(category, "flying_carpet", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasFlyingCarpetAvailable
                    ? "flyingCarpetAvailableAfterApply"
                    : snapshot.HasFlyingCarpet ? "flyingCarpetPresentButNoStartOpportunity" : "flyingCarpetUnavailableAfterApply";
            }

            if (string.Equals(category, "double_jump", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasAirJump ? "doubleJumpAvailableAfterRefresh" : "doubleJumpUnavailableAfterRefresh";
            }

            if (string.Equals(category, "wings", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasWingFlight ? "wingFlightAvailableAfterApply" : "wingFlightUnavailableAfterApply";
            }

            if (string.Equals(category, "fairy_boots", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasWingFlight || (profile != null && profile.HasAirJump)
                    ? "fairyBootsCapabilityObserved"
                    : "fairyBootsCapabilityNotObserved";
            }

            if (string.Equals(category, "horseshoe", StringComparison.OrdinalIgnoreCase))
            {
                return "horseshoeApplyVerifiedByAlreadySafeProbe";
            }

            if (string.Equals(category, "umbrella", StringComparison.OrdinalIgnoreCase))
            {
                return "umbrellaHotbarSelectionVerified";
            }

            return "postApplyCapabilitySnapshotCaptured";
        }

        private static bool IsVerifiedTemporaryActivationCapability(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            return string.Equals(reason, "doubleJumpAvailableAfterRefresh", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "rocketBootsAvailable", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "flyingCarpetAvailableAfterApply", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "gravityRestorePendingExpected", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsKnownItemTypeForDiagnostics(string category, int itemType)
        {
            if (itemType <= 0 || string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            if (string.Equals(category, "double_jump", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, DoubleJumpNames, DoubleJumpFallbackIds);
            }

            if (string.Equals(category, "rocket_boots", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, RocketBootNames, RocketBootFallbackIds);
            }

            if (string.Equals(category, "flying_carpet", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, FlyingCarpetNames, FlyingCarpetFallbackIds);
            }

            if (string.Equals(category, "fairy_boots", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, FairyLegNames, FairyLegFallbackIds);
            }

            if (string.Equals(category, "horseshoe", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, HorseshoeNames, HorseshoeFallbackIds);
            }

            if (string.Equals(category, "gravity_globe", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, GravityGlobeNames, GravityGlobeFallbackIds);
            }

            if (string.Equals(category, "umbrella", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, UmbrellaNames, UmbrellaFallbackIds);
            }

            return false;
        }

        private static void AppendPart(StringBuilder builder, string value)
        {
            if (builder == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(",");
            }

            builder.Append(value);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static bool TryInvokeApplyEquipFunctional(object player, int slot, object item, out string message)
        {
            message = string.Empty;
            if (player == null || item == null)
            {
                message = "applyEquipFunctionalSkipped:playerOrItemUnavailable";
                return false;
            }

            var method = ResolveApplyEquipFunctionalMethod(player.GetType(), item.GetType());
            if (method == null)
            {
                message = "applyEquipFunctionalUnavailable";
                return false;
            }

            try
            {
                method.Invoke(player, new[] { (object)slot, item });
                message = "applyEquipFunctionalInvoked";
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.ApplyEquipFunctional", error);
                message = "applyEquipFunctionalFailed:" + error.GetType().Name;
                return false;
            }
        }

        private static MethodInfo ResolveApplyEquipFunctionalMethod(Type playerType, Type itemType)
        {
            if (_applyEquipFunctionalResolved)
            {
                return _applyEquipFunctionalMethod;
            }

            _applyEquipFunctionalResolved = true;
            if (playerType == null || itemType == null)
            {
                return null;
            }

            try
            {
                var methods = playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (method == null || !string.Equals(method.Name, "ApplyEquipFunctional", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != 2 ||
                        parameters[0].ParameterType != typeof(int) ||
                        !parameters[1].ParameterType.IsAssignableFrom(itemType))
                    {
                        continue;
                    }

                    _applyEquipFunctionalMethod = method;
                    return _applyEquipFunctionalMethod;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.ResolveApplyEquipFunctional", error);
            }

            return null;
        }

        private static bool TryInvokeRefreshDoubleJumps(object player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "refreshDoubleJumpsSkipped:playerUnavailable";
                return false;
            }

            var method = ResolveRefreshDoubleJumpsMethod(player.GetType());
            if (method == null)
            {
                message = "refreshDoubleJumpsUnavailable";
                return false;
            }

            try
            {
                method.Invoke(player, null);
                message = "refreshDoubleJumpsInvoked";
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.RefreshDoubleJumps", error);
                message = "refreshDoubleJumpsFailed:" + error.GetType().Name;
                return false;
            }
        }

        private static MethodInfo ResolveRefreshDoubleJumpsMethod(Type playerType)
        {
            if (_refreshDoubleJumpsResolved)
            {
                return _refreshDoubleJumpsMethod;
            }

            _refreshDoubleJumpsResolved = true;
            if (playerType == null)
            {
                return null;
            }

            try
            {
                _refreshDoubleJumpsMethod = playerType.GetMethod(
                    "RefreshDoubleJumps",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.ResolveRefreshDoubleJumps", error);
            }

            return _refreshDoubleJumpsMethod;
        }

        // Restore only from recorded signatures; user-changed slots are left
        // alone and unresolved writes stay pending for the caller.
        private static MovementSafeLandingEquipmentActionResult RestoreRecords(object player, RestoreRequest request)
        {
            var result = BuildResult("restoreAttempted", string.Empty, "Safe landing temporary equipment restore attempted.");
            result.Invoked = true;
            if (request == null || request.Records == null || request.Records.Count == 0)
            {
                result.Decision = "restoreSkipped";
                result.SkipReason = "noRecords";
                result.Message = "No safe landing temporary equipment records to restore.";
                return result;
            }

            for (var index = 0; index < request.Records.Count; index++)
            {
                var record = request.Records[index];
                if (record == null)
                {
                    continue;
                }

                result.StrategyId = string.IsNullOrWhiteSpace(result.StrategyId) ? record.StrategyId : result.StrategyId;
                result.EquipmentCategory = string.IsNullOrWhiteSpace(result.EquipmentCategory) ? record.EquipmentCategory : result.EquipmentCategory;
                result.ActionType = string.IsNullOrWhiteSpace(result.ActionType) ? record.ActionType : result.ActionType;

                object targetItem;
                if (!TryGetContainerItem(player, record.TargetContainerKind, record.TargetSlot, out targetItem) ||
                    !SignatureMatches(targetItem, record.RescueItemSignature))
                {
                    result.UserChangedManagedSlotCount++;
                    record.RestoreStatus = "userChangedManagedSlot";
                    continue;
                }

                if (record.OriginalTargetWasAir)
                {
                    MovementSafeLandingEquipmentContainerKind destinationKind;
                    int destinationSlot;
                    if (!TryFindRestoreDestination(player, record, out destinationKind, out destinationSlot))
                    {
                        record.RestoreStatus = "pendingRestoreNoSpace";
                        result.PendingRestoreNoSpaceCount++;
                        result.Records.Add(CloneRecord(record));
                        continue;
                    }

                    var air = CreateAirLike(targetItem);
                if (SetContainerItem(player, destinationKind, destinationSlot, targetItem) &&
                    SetContainerItem(player, record.TargetContainerKind, record.TargetSlot, air))
                {
                    TryRestoreSelectedSlotAfterRecord(player, record, result);
                    result.RestoredMoveCount++;
                    record.RestoreStatus = "restoredToEmptyTarget";
                    continue;
                }

                    record.RestoreStatus = "pendingRestoreWriteFailed";
                    result.Records.Add(CloneRecord(record));
                    continue;
                }

                object originalItem;
                if (!TryGetContainerItem(player, record.OriginalTargetHoldingContainerKind, record.OriginalTargetHoldingSlot, out originalItem) ||
                    !SignatureMatches(originalItem, record.OriginalTargetItemSignature))
                {
                    result.OriginalMovedByUserCount++;
                    record.RestoreStatus = "originalMovedByUser";
                    continue;
                }

                if (SetContainerItem(player, record.TargetContainerKind, record.TargetSlot, originalItem) &&
                    SetContainerItem(player, record.OriginalTargetHoldingContainerKind, record.OriginalTargetHoldingSlot, targetItem))
                {
                    TryRestoreSelectedSlotAfterRecord(player, record, result);
                    result.RestoredMoveCount++;
                    record.RestoreStatus = "restoredSwap";
                    continue;
                }

                record.RestoreStatus = "pendingRestoreWriteFailed";
                result.Records.Add(CloneRecord(record));
            }

            result.PendingRestoreCount = result.Records.Count;
            result.Decision = result.PendingRestoreCount > 0 ? "restorePending" : "restoreCompleted";
            result.Message = "Safe landing temporary equipment restore completed. restoredMoveCount=" +
                             result.RestoredMoveCount.ToString(CultureInfo.InvariantCulture) +
                             ", pendingRestoreCount=" +
                             result.PendingRestoreCount.ToString(CultureInfo.InvariantCulture) + ".";
            if (result.UserChangedManagedSlotCount > 0)
            {
                result.SkipReason = AppendReason(result.SkipReason, "userChangedManagedSlot");
            }

            if (result.OriginalMovedByUserCount > 0)
            {
                result.SkipReason = AppendReason(result.SkipReason, "originalMovedByUser");
            }

            if (result.PendingRestoreNoSpaceCount > 0)
            {
                result.SkipReason = AppendReason(result.SkipReason, "pendingRestoreNoSpace");
            }

            return result;
        }

        private static bool TryFindRestoreDestination(
            object player,
            MovementSafeLandingEquipmentMoveRecord record,
            out MovementSafeLandingEquipmentContainerKind kind,
            out int slot)
        {
            kind = MovementSafeLandingEquipmentContainerKind.Unknown;
            slot = -1;
            if (record == null)
            {
                return false;
            }

            if (TryIsContainerSlotEmpty(player, record.SourceContainerKind, record.SourceSlot))
            {
                kind = record.SourceContainerKind;
                slot = record.SourceSlot;
                return true;
            }

            if (TryFindEmptyInventorySlot(player, out slot))
            {
                kind = MovementSafeLandingEquipmentContainerKind.Inventory;
                return true;
            }

            return false;
        }

        private static List<SourceCandidate> ScanSources(object player, IList armor)
        {
            var result = new List<SourceCandidate>();
            IList inventory;
            if (TryGetInventoryItems(player, out inventory) && inventory != null)
            {
                var count = Math.Min(50, GetCollectionCount(inventory));
                for (var index = 0; index < count; index++)
                {
                    AddSourceCandidate(result, MovementSafeLandingEquipmentContainerKind.Inventory, index, 2, GetIndexed(inventory, index));
                }
            }

            if (armor != null)
            {
                var count = GetCollectionCount(armor);
                var socialArmorEnd = Math.Min(FirstSocialAccessorySlot, count);
                for (var index = FirstSocialArmorSlot; index < socialArmorEnd; index++)
                {
                    AddSourceCandidate(result, MovementSafeLandingEquipmentContainerKind.SocialArmor, index, 1, GetIndexed(armor, index));
                }

                for (var index = FirstSocialAccessorySlot; index < count; index++)
                {
                    AddSourceCandidate(result, MovementSafeLandingEquipmentContainerKind.SocialAccessory, index, 1, GetIndexed(armor, index));
                }
            }

            result.Sort(CompareSourceCandidate);
            return result;
        }

        private static int CompareSourceCandidate(SourceCandidate left, SourceCandidate right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var priorityCompare = left.SourcePriority.CompareTo(right.SourcePriority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            var kindCompare = ((int)left.Kind).CompareTo((int)right.Kind);
            return kindCompare != 0 ? kindCompare : left.Slot.CompareTo(right.Slot);
        }

        private static void AddSourceCandidate(
            List<SourceCandidate> result,
            MovementSafeLandingEquipmentContainerKind kind,
            int slot,
            int priority,
            object item)
        {
            if (result == null || item == null)
            {
                return;
            }

            var signature = CreateSignature(item);
            if (signature.IsAir)
            {
                return;
            }

            int itemType;
            if (!TryReadItemType(item, out itemType) || itemType <= 0)
            {
                return;
            }

            int mountType;
            TryReadItemMountType(item, out mountType);
            result.Add(new SourceCandidate
            {
                Kind = kind,
                Slot = slot,
                SourcePriority = priority,
                Item = item,
                ItemType = itemType,
                MountType = mountType,
                Signature = signature
            });
        }

        private static bool CandidateMatchesCategory(object item, int itemType, string category)
        {
            if (item == null || itemType <= 0 || string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            if (string.Equals(category, "double_jump", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, DoubleJumpNames, DoubleJumpFallbackIds);
            }

            if (string.Equals(category, "rocket_boots", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, RocketBootNames, RocketBootFallbackIds);
            }

            if (string.Equals(category, "flying_carpet", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, FlyingCarpetNames, FlyingCarpetFallbackIds);
            }

            if (string.Equals(category, "fairy_boots", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, FairyLegNames, FairyLegFallbackIds) || ReadItemName(item).IndexOf("Djinn", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(category, "wings", StringComparison.Ordinal))
            {
                sbyte wingSlot;
                return TryReadItemSByte(item, "wingSlot", out wingSlot) && wingSlot > -1;
            }

            if (string.Equals(category, "horseshoe", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, HorseshoeNames, HorseshoeFallbackIds);
            }

            if (string.Equals(category, "flying_mount", StringComparison.Ordinal))
            {
                int mountType;
                bool canFly;
                return TryReadItemMountType(item, out mountType) &&
                       mountType >= 0 &&
                       TryResolveMountCanFly(mountType, out canFly) &&
                       canFly;
            }

            if (string.Equals(category, "safe_mount", StringComparison.Ordinal))
            {
                int mountType;
                bool canFly;
                bool noFallDamage;
                return TryReadItemMountType(item, out mountType) &&
                       mountType >= 0 &&
                       TryResolveMountCanFly(mountType, out canFly) &&
                       !canFly &&
                       TryResolveMountNoFallDamage(mountType, out noFallDamage) &&
                       noFallDamage;
            }

            if (string.Equals(category, "gravity_globe", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, GravityGlobeNames, GravityGlobeFallbackIds);
            }

            if (string.Equals(category, "umbrella", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, UmbrellaNames, UmbrellaFallbackIds);
            }

            return false;
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

        private static bool TryIsContainerSlotEmpty(object player, MovementSafeLandingEquipmentContainerKind kind, int slot)
        {
            object item;
            return TryGetContainerItem(player, kind, slot, out item) && CreateSignature(item).IsAir;
        }

        private static bool TryFindEmptyInventorySlot(object player, out int slot)
        {
            slot = -1;
            IList inventory;
            if (!TryGetInventoryItems(player, out inventory) || inventory == null)
            {
                return false;
            }

            var count = Math.Min(50, GetCollectionCount(inventory));
            for (var index = 0; index < count; index++)
            {
                if (CreateSignature(GetIndexed(inventory, index)).IsAir)
                {
                    slot = index;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetContainerItem(object player, MovementSafeLandingEquipmentContainerKind kind, int slot, out object item)
        {
            item = null;
            IList items;
            if (!TryGetContainerItems(player, kind, out items) || items == null || slot < 0 || slot >= GetCollectionCount(items))
            {
                return false;
            }

            item = GetIndexed(items, slot);
            return true;
        }

        private static bool SetContainerItem(object player, MovementSafeLandingEquipmentContainerKind kind, int slot, object value)
        {
            IList items;
            return TryGetContainerItems(player, kind, out items) &&
                   SetIndexed(items, slot, value ?? CreateAirLike(null));
        }

        private static bool TryGetContainerItems(object player, MovementSafeLandingEquipmentContainerKind kind, out IList items)
        {
            items = null;
            if (player == null)
            {
                return false;
            }

            if (kind == MovementSafeLandingEquipmentContainerKind.Inventory)
            {
                return TryGetInventoryItems(player, out items);
            }

            if (kind == MovementSafeLandingEquipmentContainerKind.Hotbar)
            {
                return TryGetInventoryItems(player, out items);
            }

            if (kind == MovementSafeLandingEquipmentContainerKind.Accessory ||
                kind == MovementSafeLandingEquipmentContainerKind.SocialArmor ||
                kind == MovementSafeLandingEquipmentContainerKind.SocialAccessory)
            {
                return TryGetArmorItems(player, out items);
            }

            if (kind == MovementSafeLandingEquipmentContainerKind.MiscEquip)
            {
                return TryGetMiscEquipItems(player, out items);
            }

            return false;
        }

        private static bool TryGetInventoryItems(object player, out IList items)
        {
            items = GetMember(player, "inventory") as IList;
            return items != null;
        }

        private static bool TryGetArmorItems(object player, out IList items)
        {
            items = GetMember(player, "armor") as IList;
            return items != null;
        }

        private static bool TryGetMiscEquipItems(object player, out IList items)
        {
            items = GetMember(player, "miscEquips") as IList;
            return items != null;
        }

        private static bool TryGetItemAt(IList items, int index, out object item)
        {
            item = null;
            if (items == null || index < 0 || index >= GetCollectionCount(items))
            {
                return false;
            }

            item = GetIndexed(items, index);
            return item != null;
        }

        private static bool SignatureMatches(object item, MovementSafeLandingEquipmentItemSignature expected)
        {
            return expected != null && expected.Matches(CreateSignature(item));
        }

        private static bool ShouldSelectTargetOnApply(MovementSafeLandingEquipmentPlan plan)
        {
            return plan != null &&
                   string.Equals(plan.EquipmentCategory, "umbrella", StringComparison.OrdinalIgnoreCase) &&
                   plan.TargetSlot >= 0 &&
                   plan.TargetSlot <= 9;
        }

        private static bool ShouldRestoreSelectedSlot(MovementSafeLandingEquipmentMoveRecord record)
        {
            return record != null &&
                   string.Equals(record.EquipmentCategory, "umbrella", StringComparison.OrdinalIgnoreCase) &&
                   record.TargetSlot >= 0 &&
                   record.TargetSlot <= 9;
        }

        private static bool TrySelectTemporaryHeldTarget(object player, int slot, out string message)
        {
            message = string.Empty;
            if (slot < 0 || slot > 9)
            {
                message = "selectedSlotSkipped:targetOutOfRange";
                return false;
            }

            if (TerrariaInputCompat.TrySelectInventorySlot(player, slot))
            {
                message = "selectedSlotSet:" + slot.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            message = "selectedSlotFailed:" + TerrariaInputCompat.LastInputCompatError;
            return false;
        }

        private static void TryRestoreSelectedSlotAfterRecord(
            object player,
            MovementSafeLandingEquipmentMoveRecord record,
            MovementSafeLandingEquipmentActionResult result)
        {
            if (result == null || !ShouldRestoreSelectedSlot(record))
            {
                return;
            }

            result.SelectedSlotRestoreAttempted = true;
            string message;
            if (TrySelectTemporaryHeldTarget(player, record.TargetSlot, out message))
            {
                result.SelectedSlotRestoreSucceeded = true;
            }

            result.SelectedSlotMessage = AppendReason(result.SelectedSlotMessage, message);
        }

        private static object CreateAirLike(object item)
        {
            Type itemType = item == null ? null : item.GetType();
            if (itemType == null)
            {
                itemType = FindType("Terraria.Item");
            }

            if (itemType == null)
            {
                return null;
            }

            try
            {
                var empty = Activator.CreateInstance(itemType);
                TryTurnToAir(empty);
                return empty;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.CreateAirLike", error);
                return null;
            }
        }

        private static bool TryTurnToAir(object item)
        {
            if (item == null)
            {
                return false;
            }

            var methods = item.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "TurnToAir", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                try
                {
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                    {
                        method.Invoke(item, new object[] { false });
                        return true;
                    }

                    if (parameters.Length == 0)
                    {
                        method.Invoke(item, new object[0]);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadItemType(object item, out int itemType)
        {
            itemType = 0;
            if (item == null)
            {
                return false;
            }

            if (!TryReadItemInt(item, "type", out itemType))
            {
                return false;
            }

            int stack;
            if (TryReadItemInt(item, "stack", out stack) && stack <= 0)
            {
                return false;
            }

            bool isAir;
            if (TryReadItemBool(item, "IsAir", out isAir) && isAir)
            {
                return false;
            }

            return itemType > 0;
        }

        private static bool TryReadItemMountType(object item, out int mountType)
        {
            mountType = -1;
            return item != null && TryReadItemIntByNames(item, out mountType, "mountType", "MountType", "mountId", "MountId") && mountType >= 0;
        }

        private static bool TryResolveMountCanFly(int mountType, out bool canFly)
        {
            canFly = false;
            if (mountType < 0)
            {
                return false;
            }

            try
            {
                var mountTypeType = FindType("Terraria.Mount");
                var mounts = mountTypeType == null ? null : GetStatic(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                if (data == null)
                {
                    return false;
                }

                bool boolValue;
                int intValue;
                float floatValue;
                if (TryReadIntByNames(data, out intValue, "flightTimeMax", "FlightTimeMax") && intValue > 0)
                {
                    canFly = true;
                    return true;
                }

                if (TryReadBoolByNames(data, out boolValue, "usesHover", "UsesHover", "canFly", "CanFly") && boolValue)
                {
                    canFly = true;
                    return true;
                }

                if (TryReadFloatByNames(data, out floatValue, "flySpeed", "FlySpeed") && floatValue > 0.1f)
                {
                    canFly = true;
                    return true;
                }

                canFly = false;
                return true;
            }
            catch (Exception error)
            {
                Logger.Debug("MovementSafeLandingEquipmentCompat", "Mount fly detection failed: " + error.Message);
                return false;
            }
        }

        private static bool TryResolveMountNoFallDamage(int mountType, out bool noFallDamage)
        {
            noFallDamage = false;
            if (mountType < 0)
            {
                return false;
            }

            try
            {
                var mountTypeType = FindType("Terraria.Mount");
                var mounts = mountTypeType == null ? null : GetStatic(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                if (data == null)
                {
                    return false;
                }

                float fallDamage;
                if (TryReadFloatByNames(data, out fallDamage, "fallDamage", "FallDamage"))
                {
                    noFallDamage = fallDamage <= 0f;
                    return true;
                }

                return false;
            }
            catch (Exception error)
            {
                Logger.Debug("MovementSafeLandingEquipmentCompat", "Mount no-fall detection failed: " + error.Message);
                return false;
            }
        }

        private static bool TryReadExpertOrMasterMode()
        {
            bool expert;
            bool master;
            var hasExpert = TryReadStaticBool(TerrariaRuntimeTypes.MainType, "expertMode", out expert);
            var hasMaster = TryReadStaticBool(TerrariaRuntimeTypes.MainType, "masterMode", out master);
            return (hasExpert && expert) || (hasMaster && master);
        }

        private static bool TryIsActuallyGroundedForRestore(object player, JumpInputProfile profile, out string reason)
        {
            reason = string.Empty;
            if (player == null || profile == null)
            {
                reason = "groundProbeUnavailable";
                return false;
            }

            if (profile.Sliding)
            {
                reason = "sliding";
                return true;
            }

            var gravityDirection = Math.Abs(profile.GravityDirection) > 0.001f ? profile.GravityDirection : 1f;
            var fallingSpeed = profile.VelocityY * gravityDirection;
            if (Math.Abs(fallingSpeed) > 0.05f)
            {
                reason = fallingSpeed > 0f ? "stillMovingDown" : "risingAfterRescue";
                return false;
            }

            float positionX;
            float positionY;
            if (!TryReadVectorMember(player, "position", out positionX, out positionY))
            {
                reason = "groundProbePositionUnavailable";
                return false;
            }

            var width = TryReadIntOrDefault(player, "width", 20);
            var height = TryReadIntOrDefault(player, "height", 42);
            var probeY = positionY + (gravityDirection >= 0f ? 2f : -2f);
            bool solid;
            if (MovementSafeLandingCompat.TryProbeLandingCollision(positionX, probeY, width, height, gravityDirection, Math.Max(1f, Math.Abs(profile.VelocityY)), out solid) && solid)
            {
                reason = "grounded";
                return true;
            }

            reason = "waitingForGrounded";
            return false;
        }

        private static bool IsKnownItem(int itemType, string[] names, int[] fallbackIds)
        {
            if (itemType <= 0)
            {
                return false;
            }

            if (fallbackIds != null)
            {
                for (var index = 0; index < fallbackIds.Length; index++)
                {
                    if (itemType == fallbackIds[index])
                    {
                        return true;
                    }
                }
            }

            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                var resolved = ResolveItemId(names[index], -1);
                if (resolved > 0 && itemType == resolved)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveItemId(string name, int fallback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            lock (ItemIdCache)
            {
                int cached;
                if (ItemIdCache.TryGetValue(name, out cached))
                {
                    return cached <= 0 ? fallback : cached;
                }

                var resolved = fallback;
                try
                {
                    var itemIdType = FindType("Terraria.ID.ItemID");
                    var field = itemIdType == null ? null : itemIdType.GetField(name, StaticFlags);
                    if (field != null)
                    {
                        resolved = Convert.ToInt32(field.GetValue(null), CultureInfo.InvariantCulture);
                    }
                }
                catch
                {
                    resolved = fallback;
                }

                ItemIdCache[name] = resolved;
                return resolved;
            }
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var type = instance.GetType();
            FieldInfo field;
            if (TerrariaMemberCache.TryGetField(type, name, false, out field) && field != null)
            {
                return field.GetValue(instance);
            }

            PropertyInfo property;
            return TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property != null && property.CanRead
                ? property.GetValue(instance, null)
                : null;
        }

        private static bool TrySetMember(object instance, string name, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field) && field != null)
                {
                    object converted;
                    if (!TryConvertMemberValue(value, field.FieldType, out converted))
                    {
                        return false;
                    }

                    field.SetValue(instance, converted);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property != null && property.CanWrite)
                {
                    object converted;
                    if (!TryConvertMemberValue(value, property.PropertyType, out converted))
                    {
                        return false;
                    }

                    property.SetValue(instance, converted, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.TrySetMember:" + name, error);
            }

            return false;
        }

        private static bool TryConvertMemberValue(object value, Type targetType, out object converted)
        {
            converted = value;
            if (targetType == null)
            {
                return false;
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (value == null)
            {
                return !targetType.IsValueType || nullableType != null;
            }

            var valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType))
            {
                converted = value;
                return true;
            }

            try
            {
                if (targetType.IsEnum)
                {
                    converted = Enum.ToObject(targetType, value);
                    return true;
                }

                converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.TryConvertMemberValue:" + targetType.FullName, error);
                converted = value;
                return false;
            }
        }

        private static object GetStatic(Type type, string name)
        {
            object value;
            return TryGetStaticMember(type, name, out value) ? value : null;
        }

        private static bool TryGetStaticMember(Type type, string name, out object value)
        {
            value = null;
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field) && field != null)
                {
                    value = field.GetValue(null);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property != null && property.CanRead)
                {
                    value = property.GetValue(null, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryReadItemInt(object item, string name, out int value)
        {
            value = 0;
            var raw = GetMember(item, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadItemSByte(object item, string name, out sbyte value)
        {
            value = 0;
            var raw = GetMember(item, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToSByte(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadItemBool(object item, string name, out bool value)
        {
            value = false;
            var raw = GetMember(item, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadItemIntByNames(object item, out int value, params string[] names)
        {
            value = 0;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryReadItemInt(item, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static int TryReadIntOrDefault(object instance, string name, int fallback)
        {
            int value;
            return TryReadInt(instance, name, out value) ? value : fallback;
        }

        private static bool TryReadInt(object instance, string name, out int value)
        {
            value = 0;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadIntByNames(object instance, out int value, params string[] names)
        {
            value = 0;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryReadInt(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadBoolByNames(object instance, out bool value, params string[] names)
        {
            value = false;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                var raw = GetMember(instance, names[index]);
                if (raw == null)
                {
                    continue;
                }

                try
                {
                    value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadFloatByNames(object instance, out float value, params string[] names)
        {
            value = 0f;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                var raw = GetMember(instance, names[index]);
                if (raw == null)
                {
                    continue;
                }

                try
                {
                    value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadStaticBool(Type type, string name, out bool value)
        {
            value = false;
            object raw;
            if (!TryGetStaticMember(type, name, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadVectorMember(object instance, string name, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            var vector = GetMember(instance, name);
            if (vector == null)
            {
                return false;
            }

            return TryReadFloatByNames(vector, out x, "X", "x") &&
                   TryReadFloatByNames(vector, out y, "Y", "y");
        }

        private static object GetIndexed(object source, int index)
        {
            if (source == null || index < 0)
            {
                return null;
            }

            var list = source as IList;
            if (list != null)
            {
                return index < list.Count ? list[index] : null;
            }

            var array = source as Array;
            return array != null && array.Rank == 1 && index < array.GetLength(0)
                ? array.GetValue(index)
                : null;
        }

        private static bool SetIndexed(object source, int index, object value)
        {
            if (source == null || index < 0 || value == null)
            {
                return false;
            }

            try
            {
                var list = source as IList;
                if (list != null)
                {
                    if (index >= list.Count)
                    {
                        return false;
                    }

                    list[index] = value;
                    return true;
                }

                var array = source as Array;
                if (array != null && array.Rank == 1 && index < array.GetLength(0))
                {
                    array.SetValue(value, index);
                    return true;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.SetIndexed", error);
            }

            return false;
        }

        private static int GetCollectionCount(object source)
        {
            var list = source as IList;
            if (list != null)
            {
                return list.Count;
            }

            var array = source as Array;
            return array == null || array.Rank != 1 ? 0 : array.GetLength(0);
        }

        private static MethodInfo FindInstanceMethod(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                return type.GetMethod(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    parameterTypes ?? Type.EmptyTypes,
                    null);
            }
            catch
            {
                return null;
            }
        }

        private static string ReadItemName(object item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var value = GetMember(item, "Name") ?? GetMember(item, "HoverName") ?? GetMember(item, "name");
            return value == null ? string.Empty : value.ToString();
        }

        private static MovementSafeLandingEquipmentActionResult BuildResult(string decision, string skipReason, string message)
        {
            return new MovementSafeLandingEquipmentActionResult
            {
                Decision = decision ?? string.Empty,
                SkipReason = skipReason ?? string.Empty,
                Message = message ?? string.Empty
            };
        }

        private static void StoreApplyResult(Guid requestId, MovementSafeLandingEquipmentActionResult result)
        {
            lock (SyncRoot)
            {
                ApplyResults[requestId] = result ?? new MovementSafeLandingEquipmentActionResult();
            }
        }

        private static void StoreRestoreResult(Guid requestId, MovementSafeLandingEquipmentActionResult result)
        {
            lock (SyncRoot)
            {
                RestoreResults[requestId] = result ?? new MovementSafeLandingEquipmentActionResult();
            }
        }

        private static string AppendReason(string current, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return current ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                return reason;
            }

            return current + "," + reason;
        }

        private static MovementSafeLandingEquipmentPlan ClonePlan(MovementSafeLandingEquipmentPlan plan)
        {
            if (plan == null)
            {
                return null;
            }

            return new MovementSafeLandingEquipmentPlan
            {
                StrategyId = plan.StrategyId,
                EquipmentCategory = plan.EquipmentCategory,
                ActionType = plan.ActionType,
                SelectedPriority = plan.SelectedPriority,
                SourceContainerKind = plan.SourceContainerKind,
                SourceSlot = plan.SourceSlot,
                TargetContainerKind = plan.TargetContainerKind,
                TargetSlot = plan.TargetSlot,
                CandidateItemType = plan.CandidateItemType,
                CandidateMountType = plan.CandidateMountType,
                CandidateSignature = CloneSignature(plan.CandidateSignature),
                TargetSignatureAtPlan = CloneSignature(plan.TargetSignatureAtPlan),
                ApplyTriggersInput = plan.ApplyTriggersInput,
                ApplyRocketRelease = plan.ApplyRocketRelease,
                SuppressDown = plan.SuppressDown,
                HoldTicks = plan.HoldTicks,
                ImpactTicks = plan.ImpactTicks,
                ImpactDistancePixels = plan.ImpactDistancePixels,
                FallingSpeed = plan.FallingSpeed,
                CapabilitySummary = plan.CapabilitySummary ?? string.Empty
            };
        }

        private static List<MovementSafeLandingEquipmentMoveRecord> CopyRecords(IList<MovementSafeLandingEquipmentMoveRecord> records)
        {
            var result = new List<MovementSafeLandingEquipmentMoveRecord>();
            if (records == null)
            {
                return result;
            }

            for (var index = 0; index < records.Count; index++)
            {
                result.Add(CloneRecord(records[index]));
            }

            return result;
        }

        private static MovementSafeLandingEquipmentMoveRecord CloneRecord(MovementSafeLandingEquipmentMoveRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new MovementSafeLandingEquipmentMoveRecord
            {
                StrategyId = record.StrategyId,
                EquipmentCategory = record.EquipmentCategory,
                ActionType = record.ActionType,
                SelectedPriority = record.SelectedPriority,
                SourceContainerKind = record.SourceContainerKind,
                SourceSlot = record.SourceSlot,
                TargetContainerKind = record.TargetContainerKind,
                TargetSlot = record.TargetSlot,
                CandidateItemType = record.CandidateItemType,
                CandidateMountType = record.CandidateMountType,
                RescueItemSignature = CloneSignature(record.RescueItemSignature),
                OriginalTargetWasAir = record.OriginalTargetWasAir,
                OriginalTargetItemSignature = CloneSignature(record.OriginalTargetItemSignature),
                OriginalTargetHoldingContainerKind = record.OriginalTargetHoldingContainerKind,
                OriginalTargetHoldingSlot = record.OriginalTargetHoldingSlot,
                ImpactTicks = record.ImpactTicks,
                ImpactDistancePixels = record.ImpactDistancePixels,
                FallingSpeed = record.FallingSpeed,
                PostApplyCapabilityObserved = record.PostApplyCapabilityObserved,
                PostApplyVerificationReason = record.PostApplyVerificationReason ?? string.Empty,
                ApplyStatus = record.ApplyStatus,
                RestoreStatus = record.RestoreStatus
            };
        }

        private static MovementSafeLandingEquipmentItemSignature CloneSignature(MovementSafeLandingEquipmentItemSignature signature)
        {
            if (signature == null)
            {
                return new MovementSafeLandingEquipmentItemSignature();
            }

            return new MovementSafeLandingEquipmentItemSignature
            {
                Type = signature.Type,
                Stack = signature.Stack,
                Prefix = signature.Prefix,
                Name = signature.Name ?? string.Empty
            };
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static readonly string[] DoubleJumpNames =
        {
            "CloudinaBottle",
            "CloudinaBalloon",
            "SandstorminaBottle",
            "SandstorminaBalloon",
            "BlizzardinaBottle",
            "BlizzardinaBalloon",
            "BundleofBalloons",
            "FartinaJar",
            "FartInABalloon",
            "TsunamiInABottle",
            "SharkronBalloon",
            "BalloonHorseshoeFart",
            "BalloonHorseshoeHoney",
            "BalloonHorseshoeSharkron",
            "PartyBundleOfBalloonsAccessory",
            "HorseshoeBundle"
        };

        private static readonly int[] DoubleJumpFallbackIds = { 53, 399, 857, 983, 987, 1163, 1164, 1724, 1863, 3201, 3241, 3250, 3251, 3252, 3730, 5331 };

        private static readonly string[] RocketBootNames =
        {
            "RocketBoots",
            "SpectreBoots",
            "LightningBoots",
            "FrostsparkBoots",
            "HellfireTreads",
            "TerrasparkBoots",
            "FairyBoots"
        };

        private static readonly int[] RocketBootFallbackIds = { 128, 405, 898, 1862, 5000, 3993 };

        private static readonly string[] FlyingCarpetNames = { "FlyingCarpet" };

        private static readonly int[] FlyingCarpetFallbackIds = { 934 };

        private static readonly string[] FairyLegNames = { "DjinnsCurse" };

        private static readonly int[] FairyLegFallbackIds = { 3770 };

        private static readonly string[] GravityGlobeNames = { "GravityGlobe" };

        private static readonly int[] GravityGlobeFallbackIds = { 1131 };

        private static readonly string[] UmbrellaNames = { "Umbrella", "TragicUmbrella" };

        private static readonly int[] UmbrellaFallbackIds = { 946, 4707 };

        private static readonly string[] HorseshoeNames =
        {
            "LuckyHorseshoe",
            "ObsidianHorseshoe",
            "BlueHorseshoeBalloon",
            "WhiteHorseshoeBalloon",
            "YellowHorseshoeBalloon",
            "BalloonHorseshoeFart",
            "BalloonHorseshoeHoney",
            "BalloonHorseshoeSharkron",
            "HorseshoeBundle"
        };

        private static readonly int[] HorseshoeFallbackIds = { 158, 396, 1250, 1251, 1252, 3250, 3251, 3252, 5331 };
    }
}
