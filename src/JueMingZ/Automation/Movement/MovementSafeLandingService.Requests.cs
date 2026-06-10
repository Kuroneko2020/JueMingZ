using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Movement
{
    public static partial class MovementSafeLandingService
    {
        private static int ResolveTeleportRodUseSlot(MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null)
            {
                return -1;
            }

            return analysis.TeleportRodInventorySlot;
        }

        private static bool IsImplementedPriority(int priority)
        {
            return priority == 1 || priority == 4 || priority == 5;
        }

        private static bool IsImplementedActivationPriority(int priority, MovementSafeLandingEquipmentMoveRecord activationRecord)
        {
            if (activationRecord != null)
            {
                return IsTemporaryEquipmentInputAction(activationRecord.ActionType);
            }

            return IsImplementedPriority(priority);
        }

        private static MovementSafeLandingEquipmentMoveRecord FindTemporaryEquipmentActivationRecord(IList<MovementSafeLandingEquipmentMoveRecord> records)
        {
            if (records == null)
            {
                return null;
            }

            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record == null)
                {
                    continue;
                }

                if (IsTemporaryEquipmentInputAction(record.ActionType))
                {
                    return record;
                }
            }

            return null;
        }

        private static bool IsTemporaryEquipmentInputAction(string actionType)
        {
            return string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase) ||
                   IsGravityFlipAction(actionType);
        }

        internal static bool IsTemporaryEquipmentActivationCapabilityAvailable(
            MovementSafeLandingEquipmentMoveRecord record,
            MovementSafeLandingAnalysis analysis,
            out string reason)
        {
            reason = string.Empty;
            if (record == null)
            {
                reason = "activationRecordUnavailable";
                return false;
            }

            if (analysis == null)
            {
                reason = "activationAnalysisUnavailable";
                return false;
            }

            if (!analysis.PlayerControllable)
            {
                reason = "playerNotControllable";
                return false;
            }

            var category = record.EquipmentCategory ?? string.Empty;
            var strategy = record.StrategyId ?? string.Empty;
            var actionType = record.ActionType ?? string.Empty;

            if (string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(category, "double_jump", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_double_jump", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(
                        analysis.HasAirJump || HasPostApplyCapabilityEvidence(record, "doubleJumpAvailableAfterRefresh"),
                        "airJumpUnavailable",
                        out reason);
                }

                if (string.Equals(category, "rocket_boots", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_rocket_boots", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(
                        analysis.HasRocketJump ||
                        analysis.HasRocketBootsAvailable ||
                        (analysis.AerialJumpWindow && HasPostApplyCapabilityEvidence(record, "rocketBootsAvailable")),
                        "rocketBootsUnavailable",
                        out reason);
                }

                if (string.Equals(category, "flying_carpet", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_flying_carpet", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(
                        analysis.AerialJumpWindow &&
                        (analysis.HasFlyingCarpetAvailable ||
                         analysis.HasFlyingCarpet ||
                         analysis.FlyingCarpetTime > 0 ||
                         HasPostApplyCapabilityEvidence(record, "flyingCarpetAvailableAfterApply")),
                        "flyingCarpetUnavailable",
                        out reason);
                }

                reason = "unknownTemporaryJumpCapability";
                return false;
            }

            if (string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(category, "flying_mount", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_flying_mount", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(analysis.HasEquippedFlyingMount, "flyingMountUnavailable", out reason);
                }

                if (string.Equals(category, "safe_mount", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_safe_mount", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(analysis.HasEquippedSafeMount, "safeMountUnavailable", out reason);
                }

                reason = "unknownTemporaryMountCapability";
                return false;
            }

            if (IsGravityFlipAction(actionType) ||
                string.Equals(category, "gravity_globe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_gravity_globe", StringComparison.OrdinalIgnoreCase))
            {
                return CapabilityAvailable(
                    analysis.HasGravityFlipOpportunity ||
                    (analysis.AerialJumpWindow &&
                     (analysis.HasGravityGlobe ||
                      HasPostApplyCapabilityEvidence(record, "gravityRestorePendingExpected"))),
                    "gravityFlipUnavailable",
                    out reason);
            }

            if (string.Equals(actionType, MovementSafeLandingActionTypes.TeleportRod, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category, MovementSafeLandingOptionCatalog.TeleportRod, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, MovementSafeLandingStrategyIds.InventoryTeleportRod, StringComparison.OrdinalIgnoreCase))
            {
                if (!analysis.HasTeleportRod)
                {
                    reason = "teleportRodUnavailable";
                    return false;
                }

                if (!TerrariaInputCompat.IsSupportedItemUseSlot(ResolveTeleportRodUseSlot(analysis)))
                {
                    reason = "teleportRodUseSlotUnavailable";
                    return false;
                }

                if (!analysis.HasTeleportTarget)
                {
                    reason = "teleportTargetUnavailable";
                    return false;
                }

                return true;
            }

            if (string.Equals(actionType, "equip_only", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            reason = "unknownTemporaryActivationAction";
            return false;
        }

        private static bool CapabilityAvailable(bool available, string unavailableReason, out string reason)
        {
            reason = available ? string.Empty : unavailableReason ?? "capabilityUnavailable";
            return available;
        }

        private static bool HasPostApplyCapabilityEvidence(MovementSafeLandingEquipmentMoveRecord record, string expectedReason)
        {
            return record != null &&
                   record.PostApplyCapabilityObserved &&
                   !string.IsNullOrWhiteSpace(expectedReason) &&
                   string.Equals(record.PostApplyVerificationReason, expectedReason, StringComparison.OrdinalIgnoreCase);
        }

        private static long GetNextAllowedRescueTick()
        {
            lock (SyncRoot)
            {
                return _nextAllowedRescueTick;
            }
        }

        private static void MarkCooldown(long tick)
        {
            lock (SyncRoot)
            {
                _nextAllowedRescueTick = tick + RescueCooldownTicks;
            }
        }

        private static void MarkCooldown(long tick, MovementSafeLandingStrategyEvaluation evaluation)
        {
            lock (SyncRoot)
            {
                _nextAllowedRescueTick = tick + ResolveRescueCooldownTicks(evaluation);
            }
        }

        private static int ResolveRescueCooldownTicks(MovementSafeLandingStrategyEvaluation evaluation)
        {
            if (evaluation == null)
            {
                return RescueCooldownTicks;
            }

            if (evaluation.Priority == 1 || evaluation.Priority == 4)
            {
                return ActiveRescueCooldownTicks;
            }

            if (evaluation.Priority == 5)
            {
                return TeleportRodRescueCooldownTicks;
            }

            return RescueCooldownTicks;
        }

        private static void ResetCooldown()
        {
            lock (SyncRoot)
            {
                _nextAllowedRescueTick = 0;
                ClearDescentRescueGuardLocked();
            }
        }

    }
}
