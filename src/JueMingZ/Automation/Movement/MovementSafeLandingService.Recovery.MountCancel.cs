using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;

namespace JueMingZ.Automation.Movement
{
    public static partial class MovementSafeLandingService
    {
        private static bool TryHandlePendingSafeLandingMountCancel(
            InputActionQueue queue,
            long tick,
            InputActionQueueFastState queueSnapshot,
            object player,
            MovementInputFrameCache.MovementInputFrame inputFrame,
            AppSettings settings)
        {
            bool pending;
            Guid cancelRequestId;
            lock (SyncRoot)
            {
                pending = _safeLandingMountCancelPending;
                cancelRequestId = _safeLandingMountCancelRequestId;
            }

            if (!pending && cancelRequestId == Guid.Empty)
            {
                return false;
            }

            if (cancelRequestId != Guid.Empty)
            {
                RecordDecision(true, "waiting", "safeLandingMountCancelInFlight", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            JumpInputProfile profile;
            string profileError;
            if (!TryReadJumpInputProfile(inputFrame, player, out profile, out profileError) || profile == null)
            {
                ResetSafeLandingMountCancelReadiness();
                RecordDecision(true, "mountCancelPending", "jumpProfileUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), profileError);
                return true;
            }

            string clearReason;
            if (ShouldClearSafeLandingMountCancel(profile, out clearReason))
            {
                ClearSafeLandingMountCancel(clearReason);
                RecordDecision(true, "mountCancelCompleted", clearReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (!profile.PlayerControllable)
            {
                ResetSafeLandingMountCancelReadiness();
                RecordDecision(true, "mountCancelPending", "playerNotControllable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            bool safeToCancel;
            bool requiresStableWait;
            string cancelReason;
            if (!TryResolveSafeLandingMountCancelReadiness(player, profile, out safeToCancel, out requiresStableWait, out cancelReason))
            {
                ResetSafeLandingMountCancelReadiness();
                RecordDecision(true, "mountCancelPending", cancelReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (!safeToCancel)
            {
                ResetSafeLandingMountCancelReadiness();
                RecordDecision(true, "mountCancelPending", cancelReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            var readyReason = cancelReason;
            if (requiresStableWait)
            {
                long readySince;
                lock (SyncRoot)
                {
                    if (_safeLandingMountCancelReadySinceTick < 0 ||
                        !string.Equals(_safeLandingMountCancelReadyReason, cancelReason ?? string.Empty, StringComparison.Ordinal))
                    {
                        _safeLandingMountCancelReadySinceTick = tick;
                        _safeLandingMountCancelReadyReason = cancelReason ?? string.Empty;
                    }

                    readySince = _safeLandingMountCancelReadySinceTick;
                    readyReason = _safeLandingMountCancelReadyReason;
                }

                if (readySince >= 0 && tick - readySince < TemporaryEquipmentStableRestoreTicks)
                {
                    RecordDecision(true, "mountCancelPending", "waitingForStableMountCancel:" + readyReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return true;
                }
            }
            else
            {
                ResetSafeLandingMountCancelReadiness();
            }

            long lastCancelAttemptTick;
            lock (SyncRoot)
            {
                lastCancelAttemptTick = _lastSafeLandingMountCancelAttemptTick;
            }

            if (tick - lastCancelAttemptTick < SafeLandingMountCancelRetryTicks)
            {
                RecordDecision(true, "mountCancelPending", "safeLandingMountCancelCooldown", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (queue == null)
            {
                RecordDecision(true, "mountCancelPending", "queueUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            var request = MovementSafeLandingRequestBuilder.BuildRecoveryJumpRequest(
                StrategyMountCancel,
                MovementSafeLandingActionTypes.QuickMount,
                "1",
                "mountCancelAfterLanding",
                1f,
                "Movement safe landing cancels flying mount after landing");

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision(true, "mountCancelPending", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            lock (SyncRoot)
            {
                _safeLandingMountCancelRequestId = request.RequestId;
                _lastSafeLandingMountCancelAttemptTick = tick;
            }

            RecordDecision(true, "submittedMountCancel", readyReason, tick, queueSnapshot, null, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
            return true;
        }

        private static void TrackSafeLandingMountActivationRequest(Guid requestId, string actionType)
        {
            if (requestId == Guid.Empty ||
                !string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lock (SyncRoot)
            {
                _safeLandingMountActivationRequestId = requestId;
            }
        }

        private static void MarkSafeLandingMountCancelPending()
        {
            lock (SyncRoot)
            {
                _safeLandingMountCancelPending = true;
                _safeLandingMountCancelReadySinceTick = -1;
                _safeLandingMountCancelReadyReason = string.Empty;
            }
        }

        private static void CompleteSafeLandingMountCancel(InputActionResult result)
        {
            var actionApplied = IsSafeLandingActionApplied(result);
            var mountStillActive = false;
            if (actionApplied)
            {
                object player;
                JumpInputProfile profile;
                mountStillActive = TerrariaInputCompat.TryGetLocalPlayer(out player) &&
                                   TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) &&
                                   profile != null &&
                                   profile.MountActive;
            }

            lock (SyncRoot)
            {
                if (actionApplied)
                {
                    _safeLandingMountCancelPending = mountStillActive;
                }

                _safeLandingMountCancelReadySinceTick = -1;
                _safeLandingMountCancelReadyReason = string.Empty;
            }
        }

        internal static bool ShouldClearSafeLandingMountCancelForTesting(JumpInputProfile profile, out string reason)
        {
            return ShouldClearSafeLandingMountCancel(profile, out reason);
        }

        private static bool ShouldClearSafeLandingMountCancel(JumpInputProfile profile, out string reason)
        {
            reason = string.Empty;
            if (profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.MountActive)
            {
                reason = "mountAlreadyInactive";
                return true;
            }

            return false;
        }

        internal static bool TryEvaluateSafeLandingMountCancelImminentForTesting(
            JumpInputProfile profile,
            bool impactProbeAvailable,
            int impactDistancePixels,
            float impactTicks,
            out bool safeToCancel,
            out bool requiresStableWait,
            out string reason)
        {
            safeToCancel = false;
            requiresStableWait = true;
            var ok = TryEvaluateSafeLandingMountCancelImminent(profile, impactProbeAvailable, impactDistancePixels, impactTicks, out safeToCancel, out reason);
            requiresStableWait = !safeToCancel;
            return ok;
        }

        private static bool TryResolveSafeLandingMountCancelReadiness(
            object player,
            JumpInputProfile profile,
            out bool safeToCancel,
            out bool requiresStableWait,
            out string reason)
        {
            safeToCancel = false;
            requiresStableWait = true;
            reason = string.Empty;
            if (profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.MountActive)
            {
                safeToCancel = true;
                requiresStableWait = false;
                reason = "mountAlreadyInactive";
                return true;
            }

            if (!profile.PlayerControllable)
            {
                reason = "playerNotControllable";
                return true;
            }

            int impactDistancePixels;
            float impactTicks;
            string impactReason;
            var impactProbeAvailable = MovementSafeLandingCompat.TryResolveLandingImpactDistanceForProfile(
                player,
                profile,
                out impactDistancePixels,
                out impactTicks,
                out impactReason);

            bool imminentCancelReady;
            string imminentReason;
            if (TryEvaluateSafeLandingMountCancelImminent(
                    profile,
                    impactProbeAvailable,
                    impactDistancePixels,
                    impactTicks,
                    out imminentCancelReady,
                    out imminentReason) &&
                imminentCancelReady)
            {
                safeToCancel = true;
                requiresStableWait = false;
                reason = imminentReason;
                return true;
            }

            bool groundedSafeToCancel;
            string groundedReason;
            if (!MovementSafeLandingEquipmentCompat.TryIsSafeToRestoreTemporaryEquipment(player, out groundedSafeToCancel, out groundedReason))
            {
                reason = string.IsNullOrWhiteSpace(groundedReason) ? impactReason : groundedReason;
                return false;
            }

            safeToCancel = groundedSafeToCancel;
            requiresStableWait = groundedSafeToCancel;
            if (groundedSafeToCancel)
            {
                reason = groundedReason;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(imminentReason) &&
                string.Equals(groundedReason, "stillFalling", StringComparison.Ordinal))
            {
                reason = imminentReason;
            }
            else
            {
                reason = string.IsNullOrWhiteSpace(groundedReason) ? imminentReason : groundedReason;
            }

            return true;
        }

        private static bool TryEvaluateSafeLandingMountCancelImminent(
            JumpInputProfile profile,
            bool impactProbeAvailable,
            int impactDistancePixels,
            float impactTicks,
            out bool safeToCancel,
            out string reason)
        {
            safeToCancel = false;
            reason = string.Empty;
            if (profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.MountActive)
            {
                safeToCancel = true;
                reason = "mountAlreadyInactive";
                return true;
            }

            if (!profile.PlayerControllable)
            {
                reason = "playerNotControllable";
                return true;
            }

            var gravityDirection = Math.Abs(profile.GravityDirection) > 0.001f ? profile.GravityDirection : 1f;
            var fallingSpeed = profile.VelocityY * gravityDirection;
            if (fallingSpeed <= SafeLandingMountCancelImminentMinFallingSpeed)
            {
                reason = "mountCancelImminentWindowNotRequired";
                return true;
            }

            if (!impactProbeAvailable)
            {
                reason = "mountCancelImpactProbeUnavailable";
                return true;
            }

            if (impactDistancePixels < 0 || impactTicks < 0f || float.IsNaN(impactTicks) || float.IsInfinity(impactTicks))
            {
                reason = "waitingForImminentLandingCollision:noImpact";
                return true;
            }

            var distanceGate = ResolveSafeLandingMountCancelImminentImpactDistance(fallingSpeed);
            if (impactDistancePixels <= distanceGate || impactTicks <= SafeLandingMountCancelImminentImpactMaxTicks)
            {
                safeToCancel = true;
                reason = "imminentLandingCollision:distance=" +
                         impactDistancePixels.ToString(CultureInfo.InvariantCulture) +
                         ",ticks=" +
                         impactTicks.ToString("0.###", CultureInfo.InvariantCulture);
                return true;
            }

            reason = "waitingForImminentLandingCollision:distance=" +
                     impactDistancePixels.ToString(CultureInfo.InvariantCulture) +
                     ",ticks=" +
                     impactTicks.ToString("0.###", CultureInfo.InvariantCulture) +
                     ",distanceGate=" +
                     distanceGate.ToString(CultureInfo.InvariantCulture) +
                     ",ticksGate=" +
                     SafeLandingMountCancelImminentImpactMaxTicks.ToString("0.###", CultureInfo.InvariantCulture);
            return true;
        }

        private static int ResolveSafeLandingMountCancelImminentImpactDistance(float fallingSpeed)
        {
            var speed = Math.Max(0f, fallingSpeed);
            var speedScaled = (int)Math.Ceiling(speed * 4f);
            if (speedScaled < SafeLandingMountCancelImminentImpactMinPixels)
            {
                return SafeLandingMountCancelImminentImpactMinPixels;
            }

            return speedScaled > SafeLandingMountCancelImminentImpactMaxPixels
                ? SafeLandingMountCancelImminentImpactMaxPixels
                : speedScaled;
        }

        private static void ResetSafeLandingMountCancelReadiness()
        {
            lock (SyncRoot)
            {
                _safeLandingMountCancelReadySinceTick = -1;
                _safeLandingMountCancelReadyReason = string.Empty;
            }
        }

        private static void ClearSafeLandingMountCancel(string reason)
        {
            lock (SyncRoot)
            {
                _safeLandingMountCancelPending = false;
                _safeLandingMountCancelRequestId = Guid.Empty;
                _safeLandingMountCancelReadySinceTick = -1;
                _safeLandingMountCancelReadyReason = reason ?? string.Empty;
            }
        }
    }
}
