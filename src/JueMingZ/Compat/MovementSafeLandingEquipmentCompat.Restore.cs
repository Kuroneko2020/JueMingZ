using System;
using System.Collections;
using System.Globalization;
using JueMingZ.Automation.Movement;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
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

        private static bool SetContainerItem(object player, MovementSafeLandingEquipmentContainerKind kind, int slot, object value)
        {
            IList items;
            return TryGetContainerItems(player, kind, out items) &&
                   SetIndexed(items, slot, value ?? CreateAirLike(null));
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
    }
}
