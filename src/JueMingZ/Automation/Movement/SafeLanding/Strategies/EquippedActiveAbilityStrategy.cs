using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Automation.Movement
{
    // Equipped active abilities are recommendations until queued; this strategy must not fire mounts, hooks, or items.
    internal sealed class EquippedActiveAbilityStrategy : IMovementSafeLandingStrategy
    {
        public IEnumerable<MovementSafeLandingStrategyEvaluation> Evaluate(MovementSafeLandingStrategyContext context)
        {
            yield return EvaluateOne(
                context,
                MovementSafeLandingStrategyIds.EquippedDoubleJump,
                MovementSafeLandingOptionCatalog.DoubleJump,
                MovementSafeLandingActionTypes.Jump,
                InputActionChannel.Jump,
                HasAirJump,
                "airJumpUnavailable",
                "order=1,doubleJump");

            yield return EvaluateOne(
                context,
                MovementSafeLandingStrategyIds.EquippedRocketBoots,
                MovementSafeLandingOptionCatalog.RocketBoots,
                MovementSafeLandingActionTypes.Jump,
                InputActionChannel.Jump,
                HasRocketBoots,
                "rocketBootsUnavailable",
                "order=2,rocketBoots");

            yield return EvaluateOne(
                context,
                MovementSafeLandingStrategyIds.EquippedFlyingCarpet,
                MovementSafeLandingOptionCatalog.FlyingCarpet,
                MovementSafeLandingActionTypes.Jump,
                InputActionChannel.Jump,
                HasFlyingCarpet,
                "flyingCarpetUnavailable",
                "order=3,flyingCarpet");

            yield return EvaluateOne(
                context,
                MovementSafeLandingStrategyIds.EquippedGravityGlobe,
                MovementSafeLandingOptionCatalog.GravityGlobe,
                MovementSafeLandingActionTypes.GravityFlip,
                InputActionChannel.Jump | InputActionChannel.GravityFlip,
                HasGravityFlip,
                "gravityFlipUnavailable",
                "order=4,gravityGlobe");

            yield return EvaluateOne(
                context,
                MovementSafeLandingStrategyIds.ActiveFlyingMount,
                MovementSafeLandingOptionCatalog.FlyingMount,
                MovementSafeLandingActionTypes.Jump,
                InputActionChannel.Jump,
                HasActiveFlyingMount,
                "activeFlyingMountUnavailable",
                "order=5,activeFlyingMount");

            yield return EvaluateOne(
                context,
                MovementSafeLandingStrategyIds.EquippedFlyingMount,
                MovementSafeLandingOptionCatalog.FlyingMount,
                MovementSafeLandingActionTypes.QuickMount,
                InputActionChannel.Jump | InputActionChannel.QuickMount,
                HasEquippedFlyingMount,
                "equippedFlyingMountUnavailable",
                "order=6,equippedFlyingMount");

            yield return EvaluateOne(
                context,
                MovementSafeLandingStrategyIds.EquippedSafeMount,
                MovementSafeLandingOptionCatalog.DamageReductionMount,
                MovementSafeLandingActionTypes.QuickMount,
                InputActionChannel.Jump | InputActionChannel.QuickMount,
                HasEquippedSafeMount,
                "equippedSafeMountUnavailable",
                "order=7,equippedSafeMount");
        }

        private static MovementSafeLandingStrategyEvaluation EvaluateOne(
            MovementSafeLandingStrategyContext context,
            string strategyId,
            string optionId,
            string actionType,
            InputActionChannel channels,
            Func<MovementSafeLandingCapabilitySnapshot, bool> capabilityPredicate,
            string unavailableReason,
            string sortReason)
        {
            var settings = context == null ? null : context.Settings;
            var hazard = context == null ? null : context.Hazard;
            var capability = context == null ? null : context.Capability;
            var configEnabled = MovementSafeLandingOptionCatalog.GetEnabled(settings, optionId);
            var playerReady = hazard == null || hazard.PlayerControllable;
            var capabilityAvailable = capability != null && capabilityPredicate(capability);
            var candidate = configEnabled && playerReady && capabilityAvailable;
            var ready = false;
            var timingReason = string.Empty;
            if (candidate)
            {
                var analysis = context == null ? null : context.Analysis;
                if (analysis != null)
                {
                    var originalStrategy = analysis.SelectedStrategyId;
                    var originalAction = analysis.SelectedActionType;
                    analysis.SelectedStrategyId = strategyId;
                    analysis.SelectedActionType = actionType;
                    ready = MovementSafeLandingTiming.IsEquippedRescueWindowReady(analysis, out timingReason);
                    analysis.SelectedStrategyId = originalStrategy;
                    analysis.SelectedActionType = originalAction;
                }
                else
                {
                    timingReason = "analysisUnavailable";
                }
            }

            return new MovementSafeLandingStrategyEvaluation
            {
                StrategyId = strategyId,
                Priority = 1,
                ActionType = actionType,
                RequestKind = InputActionKind.Jump,
                RequiredChannels = channels,
                TimingWindow = timingReason,
                IsCandidate = candidate,
                IsReady = candidate && ready,
                BlocksLowerPriority = candidate,
                RequiresTemporaryEquipment = false,
                RequiresRestore = string.Equals(actionType, MovementSafeLandingActionTypes.QuickMount, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(actionType, MovementSafeLandingActionTypes.GravityFlip, StringComparison.OrdinalIgnoreCase),
                SkipReason = candidate
                    ? ready ? string.Empty : "waitingForRescueWindow:" + strategyId + ":" + timingReason
                    : !configEnabled
                        ? MovementSafeLandingSkipReasons.ConfigDisabled
                        : !playerReady
                            ? MovementSafeLandingSkipReasons.PlayerNotControllable
                            : unavailableReason ?? MovementSafeLandingSkipReasons.CapabilityUnavailable,
                Confidence = candidate ? "medium" : "none",
                Readiness = candidate ? ready ? "ready" : "timingNotReady" : "notCandidate",
                SortReason = sortReason
            };
        }

        private static bool HasAirJump(MovementSafeLandingCapabilitySnapshot snapshot)
        {
            return snapshot.HasAirJump;
        }

        private static bool HasRocketBoots(MovementSafeLandingCapabilitySnapshot snapshot)
        {
            return snapshot.HasRocketBootsAvailable;
        }

        private static bool HasFlyingCarpet(MovementSafeLandingCapabilitySnapshot snapshot)
        {
            return snapshot.HasFlyingCarpetAvailable;
        }

        private static bool HasActiveFlyingMount(MovementSafeLandingCapabilitySnapshot snapshot)
        {
            return snapshot.HasActiveFlyingMount;
        }

        private static bool HasEquippedFlyingMount(MovementSafeLandingCapabilitySnapshot snapshot)
        {
            return snapshot.HasEquippedFlyingMount;
        }

        private static bool HasEquippedSafeMount(MovementSafeLandingCapabilitySnapshot snapshot)
        {
            return snapshot.HasEquippedSafeMount;
        }

        private static bool HasGravityFlip(MovementSafeLandingCapabilitySnapshot snapshot)
        {
            return snapshot.HasGravityFlipOpportunity;
        }
    }
}
