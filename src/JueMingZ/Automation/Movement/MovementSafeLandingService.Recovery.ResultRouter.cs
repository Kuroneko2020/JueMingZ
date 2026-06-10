using System;
using JueMingZ.Actions;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Movement
{
    public static partial class MovementSafeLandingService
    {
        private static void RouteSafeLandingActionQueueResult(InputActionResult result)
        {
            var applyHandled = false;
            var activationHandled = false;
            var restoreHandled = false;
            var mountActivationHandled = false;
            var mountCancelHandled = false;
            var gravityActivationHandled = false;
            var gravityRestoreHandled = false;
            lock (SyncRoot)
            {
                if (result.RequestId == _temporaryEquipmentApplyRequestId)
                {
                    _temporaryEquipmentApplyRequestId = Guid.Empty;
                    applyHandled = true;
                }

                if (result.RequestId == _temporaryEquipmentActivationRequestId)
                {
                    _temporaryEquipmentActivationRequestId = Guid.Empty;
                    _temporaryEquipmentActivationApplied = IsTemporaryEquipmentActivationApplied(result);
                    _temporaryEquipmentLastDecision = _temporaryEquipmentActivationApplied ? "activationApplied" : "activationCompleted";
                    _temporaryEquipmentLastSkipReason = result.Status.ToString() + ":" + (result.ResultCode ?? string.Empty);
                    activationHandled = true;
                }

                if (result.RequestId == _temporaryEquipmentRestoreRequestId)
                {
                    _temporaryEquipmentRestoreRequestId = Guid.Empty;
                    restoreHandled = true;
                }

                if (result.RequestId == _safeLandingMountActivationRequestId)
                {
                    _safeLandingMountActivationRequestId = Guid.Empty;
                    mountActivationHandled = true;
                }

                if (result.RequestId == _safeLandingMountCancelRequestId)
                {
                    _safeLandingMountCancelRequestId = Guid.Empty;
                    mountCancelHandled = true;
                }

                if (result.RequestId == _safeLandingGravityActivationRequestId)
                {
                    _safeLandingGravityActivationRequestId = Guid.Empty;
                    gravityActivationHandled = true;
                }

                if (result.RequestId == _safeLandingGravityRestoreRequestId)
                {
                    _safeLandingGravityRestoreRequestId = Guid.Empty;
                    gravityRestoreHandled = true;
                }
            }

            if (mountActivationHandled && IsSafeLandingActionApplied(result))
            {
                MarkSafeLandingMountCancelPending();
            }

            if (mountCancelHandled)
            {
                CompleteSafeLandingMountCancel(result);
            }

            if (gravityActivationHandled && IsSafeLandingActionApplied(result))
            {
                MarkSafeLandingGravityRestorePending();
            }

            if (gravityRestoreHandled)
            {
                CompleteSafeLandingGravityRestore(result);
            }

            MovementSafeLandingEquipmentActionResult actionResult;
            if (applyHandled && MovementSafeLandingEquipmentCompat.TryTakeApplyResult(result.RequestId, out actionResult))
            {
                TemporaryEquipmentApplyCompleted(actionResult);
                return;
            }

            if (activationHandled)
            {
                return;
            }

            if (restoreHandled && MovementSafeLandingEquipmentCompat.TryTakeRestoreResult(result.RequestId, out actionResult))
            {
                TemporaryEquipmentRestoreCompleted(actionResult);
            }
        }

        private static bool IsTemporaryEquipmentActivationApplied(InputActionResult result)
        {
            if (result == null)
            {
                return false;
            }

            return result.Status == InputActionStatus.Succeeded ||
                   result.Status == InputActionStatus.AttemptedButUnverified;
        }

        private static bool IsSafeLandingActionApplied(InputActionResult result)
        {
            if (result == null)
            {
                return false;
            }

            return result.Status == InputActionStatus.Succeeded ||
                   result.Status == InputActionStatus.AttemptedButUnverified;
        }
    }
}
