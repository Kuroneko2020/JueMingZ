using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Movement
{
    public static partial class MovementSafeLandingService
    {
        private static bool TryHandlePendingSafeLandingGravityRestore(
            InputActionQueue queue,
            long tick,
            InputActionQueueFastState queueSnapshot,
            object player,
            MovementInputFrameCache.MovementInputFrame inputFrame,
            AppSettings settings)
        {
            bool pending;
            Guid restoreRequestId;
            long activationTick;
            float originalDirection;
            lock (SyncRoot)
            {
                pending = _safeLandingGravityRestorePending;
                restoreRequestId = _safeLandingGravityRestoreRequestId;
                activationTick = _safeLandingGravityActivationTick;
                originalDirection = _safeLandingGravityOriginalDirection;
            }

            if (!pending && restoreRequestId == Guid.Empty)
            {
                return false;
            }

            if (restoreRequestId != Guid.Empty)
            {
                RecordDecision(true, "waiting", "safeLandingGravityRestoreInFlight", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (activationTick >= 0 && tick - activationTick > SafeLandingGravityRestoreGiveUpTicks)
            {
                GiveUpSafeLandingGravityRestore("gravityRestoreGaveUpManualRequired");
                RecordDecision(true, "gravityRestorePending", "gravityRestoreGaveUpManualRequired", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            JumpInputProfile profile;
            string profileError;
            if (!TryReadJumpInputProfile(inputFrame, player, out profile, out profileError) || profile == null)
            {
                ResetSafeLandingGravityRestoreReadiness();
                RecordGravityRestoreState("restoreWaiting", "jumpProfileUnavailable");
                RecordDecision(true, "gravityRestorePending", "jumpProfileUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), profileError);
                return true;
            }

            var currentDirection = profile.GravityDirection >= 0f ? 1f : -1f;
            var targetDirection = originalDirection >= 0f ? 1f : -1f;
            if (Math.Abs(currentDirection - targetDirection) < 0.01f)
            {
                ClearSafeLandingGravityRestore("alreadyOriginalGravity");
                RecordDecision(true, "gravityRestoreCompleted", "alreadyOriginalGravity", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (!profile.PlayerControllable)
            {
                ResetSafeLandingGravityRestoreReadiness();
                RecordGravityRestoreState("restoreWaiting", "playerNotControllable");
                RecordDecision(true, "gravityRestorePending", "playerNotControllable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (!profile.HasGravityGlobe)
            {
                ResetSafeLandingGravityRestoreReadiness();
                RecordGravityRestoreState("restoreWaiting", "gravityGlobeUnavailable");
                RecordDecision(true, "gravityRestorePending", "gravityGlobeUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            var readyReason = "immediateGravityRestore";

            long lastRestoreAttemptTick;
            lock (SyncRoot)
            {
                lastRestoreAttemptTick = _lastSafeLandingGravityRestoreAttemptTick;
            }

            if (tick - lastRestoreAttemptTick < SafeLandingGravityRestoreRetryTicks)
            {
                RecordGravityRestoreState("restoreWaiting", "safeLandingGravityRestoreCooldown");
                RecordDecision(true, "gravityRestorePending", "safeLandingGravityRestoreCooldown", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (queue == null)
            {
                RecordGravityRestoreState("restoreWaiting", "queueUnavailable");
                RecordDecision(true, "gravityRestorePending", "queueUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            var request = MovementSafeLandingRequestBuilder.BuildRecoveryJumpRequest(
                StrategyGravityRestore,
                MovementSafeLandingActionTypes.GravityFlip,
                "1",
                "gravityRestoreAfterLanding",
                targetDirection,
                "Movement safe landing restores gravity direction after Gravity Globe rescue");

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordGravityRestoreState("restoreWaiting", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason));
                RecordDecision(true, "gravityRestorePending", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            lock (SyncRoot)
            {
                _safeLandingGravityRestoreRequestId = request.RequestId;
                _lastSafeLandingGravityRestoreAttemptTick = tick;
                _safeLandingGravityLastDecision = "submittedRestore";
                _safeLandingGravityLastSkipReason = readyReason ?? string.Empty;
            }

            RecordDecision(true, "submittedGravityRestore", readyReason, tick, queueSnapshot, null, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
            return true;
        }

        private static void TrackSafeLandingGravityActivationRequest(Guid requestId, string actionType, float originalGravityDirection)
        {
            if (requestId == Guid.Empty || !IsGravityFlipAction(actionType))
            {
                return;
            }

            lock (SyncRoot)
            {
                _safeLandingGravityActivationRequestId = requestId;
                _safeLandingGravityOriginalDirection = originalGravityDirection >= 0f ? 1f : -1f;
                _safeLandingGravityActivationTick = JueMingZRuntime.State == null ? -1 : JueMingZRuntime.State.UpdateCount;
                _safeLandingGravityLastDecision = "submittedActivation";
                _safeLandingGravityLastSkipReason = string.Empty;
            }
        }

        private static void MarkSafeLandingGravityRestorePending()
        {
            lock (SyncRoot)
            {
                _safeLandingGravityRestorePending = true;
                if (_safeLandingGravityActivationTick < 0 && JueMingZRuntime.State != null)
                {
                    _safeLandingGravityActivationTick = JueMingZRuntime.State.UpdateCount;
                }

                _safeLandingGravityLastDecision = "restorePending";
                _safeLandingGravityLastSkipReason = string.Empty;
            }
        }

        private static void CompleteSafeLandingGravityRestore(InputActionResult result)
        {
            var actionApplied = IsSafeLandingActionApplied(result);
            var restoredToOriginal = false;
            var restoreReason = result == null ? string.Empty : result.ResultCode ?? string.Empty;
            if (actionApplied)
            {
                // A queued gravity flip is only accepted when the observed gravity
                // direction returns to the recorded original direction.
                float originalDirection;
                lock (SyncRoot)
                {
                    originalDirection = _safeLandingGravityOriginalDirection >= 0f ? 1f : -1f;
                }

                object player;
                JumpInputProfile profile;
                if (TerrariaInputCompat.TryGetLocalPlayer(out player) &&
                    TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) &&
                    profile != null)
                {
                    var currentDirection = profile.GravityDirection >= 0f ? 1f : -1f;
                    restoredToOriginal = Math.Abs(currentDirection - originalDirection) < 0.01f;
                    restoreReason = restoredToOriginal
                        ? restoreReason
                        : "gravityDirectionStillInverted:" + currentDirection.ToString("0.###", CultureInfo.InvariantCulture);
                }
                else
                {
                    restoreReason = "gravityRestoreProfileUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                }
            }

            lock (SyncRoot)
            {
                if (actionApplied && restoredToOriginal)
                {
                    _safeLandingGravityRestorePending = false;
                    _safeLandingGravityLastDecision = "restoreCompleted";
                    _safeLandingGravityLastSkipReason = restoreReason;
                }
                else if (actionApplied)
                {
                    _safeLandingGravityRestorePending = true;
                    _safeLandingGravityLastDecision = "restoreStillPending";
                    _safeLandingGravityLastSkipReason = restoreReason;
                }
                else
                {
                    _safeLandingGravityLastDecision = "restoreCompletedUnverified";
                    _safeLandingGravityLastSkipReason = result == null ? string.Empty : result.Status.ToString() + ":" + (result.ResultCode ?? string.Empty);
                }

            }
        }

        private static void ResetSafeLandingGravityRestoreReadiness()
        {
        }

        private static void ClearSafeLandingGravityRestore(string reason)
        {
            lock (SyncRoot)
            {
                _safeLandingGravityRestorePending = false;
                _safeLandingGravityRestoreRequestId = Guid.Empty;
                _safeLandingGravityLastDecision = "restoreCleared";
                _safeLandingGravityLastSkipReason = reason ?? string.Empty;
            }
        }

        private static void GiveUpSafeLandingGravityRestore(string reason)
        {
            lock (SyncRoot)
            {
                _safeLandingGravityRestorePending = false;
                _safeLandingGravityRestoreRequestId = Guid.Empty;
                _safeLandingGravityLastDecision = "restoreGaveUp";
                _safeLandingGravityLastSkipReason = reason ?? string.Empty;
            }
        }

        private static void RecordGravityRestoreState(string decision, string skipReason)
        {
            lock (SyncRoot)
            {
                _safeLandingGravityLastDecision = decision ?? string.Empty;
                _safeLandingGravityLastSkipReason = skipReason ?? string.Empty;
            }
        }

        private static bool IsGravityFlipAction(string actionType)
        {
            return string.Equals(actionType, "gravity_flip", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionType, "gravityFlip", StringComparison.OrdinalIgnoreCase);
        }
    }
}
