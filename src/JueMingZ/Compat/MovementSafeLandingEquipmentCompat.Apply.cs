using System;
using JueMingZ.Automation.Movement;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
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
    }
}
