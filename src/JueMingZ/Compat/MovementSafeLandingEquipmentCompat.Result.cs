using System;
using System.Collections.Generic;
using JueMingZ.Automation.Movement;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
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
    }
}
