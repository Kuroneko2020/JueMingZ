using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Automation.Movement
{
    internal sealed class TeleportRodStrategy : IMovementSafeLandingStrategy
    {
        public IEnumerable<MovementSafeLandingStrategyEvaluation> Evaluate(MovementSafeLandingStrategyContext context)
        {
            var settings = context == null ? null : context.Settings;
            var analysis = context == null ? null : context.Analysis;
            var hazard = context == null ? null : context.Hazard;
            var capability = context == null ? null : context.Capability;
            var configEnabled = MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.TeleportRod);
            var playerReady = hazard == null || hazard.PlayerControllable;
            var hasTeleportRod = capability != null && capability.HasTeleportRod;
            var hasImpactTarget = analysis != null && analysis.ImpactFound;
            var candidate = configEnabled && playerReady && hasTeleportRod && hasImpactTarget;

            string timingReason = string.Empty;
            var ready = false;
            if (candidate && analysis != null)
            {
                UpdateTeleportTarget(analysis);
                var originalStrategy = analysis.SelectedStrategyId;
                var originalAction = analysis.SelectedActionType;
                analysis.SelectedStrategyId = MovementSafeLandingStrategyIds.InventoryTeleportRod;
                analysis.SelectedActionType = MovementSafeLandingActionTypes.TeleportRod;
                ready = MovementSafeLandingTiming.IsEquippedRescueWindowReady(analysis, out timingReason);
                analysis.SelectedStrategyId = originalStrategy;
                analysis.SelectedActionType = originalAction;
            }
            else if (candidate)
            {
                timingReason = "analysisUnavailable";
            }

            yield return new MovementSafeLandingStrategyEvaluation
            {
                StrategyId = MovementSafeLandingStrategyIds.InventoryTeleportRod,
                Priority = 5,
                ActionType = MovementSafeLandingActionTypes.TeleportRod,
                RequestKind = InputActionKind.UseHotbarItem,
                RequiredChannels = InputActionChannel.UseItem |
                                   InputActionChannel.HotbarSelection |
                                   InputActionChannel.BridgeItemUse |
                                   InputActionChannel.MouseTarget,
                TimingWindow = timingReason,
                IsCandidate = candidate,
                IsReady = candidate && ready,
                BlocksLowerPriority = candidate,
                RequiresTemporaryEquipment = false,
                RequiresRestore = false,
                SkipReason = candidate
                    ? ready ? string.Empty : "waitingForRescueWindow:" + MovementSafeLandingStrategyIds.InventoryTeleportRod + ":" + timingReason
                    : !configEnabled
                        ? MovementSafeLandingSkipReasons.ConfigDisabled
                        : !playerReady
                            ? MovementSafeLandingSkipReasons.PlayerNotControllable
                            : !hasTeleportRod
                                ? "teleportRodUnavailable"
                                : "teleportTargetUnavailable",
                Confidence = candidate ? "medium" : "none",
                Readiness = candidate ? ready ? "ready" : "timingNotReady" : "notCandidate",
                SortReason = "priority5-after-priority0-1-2-3-4,vanillaTeleportRodItemUse"
            };
        }

        private static void UpdateTeleportTarget(MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null || !analysis.ImpactFound)
            {
                return;
            }

            var tileX = (int)Math.Floor(analysis.ImpactWorldX / 16f);
            var tileY = analysis.GravityDirection >= 0f
                ? (int)Math.Floor(analysis.ImpactWorldY / 16f) - 1
                : (int)Math.Floor(analysis.ImpactWorldY / 16f) + 1;

            analysis.HasTeleportTarget = true;
            analysis.TeleportTargetTileX = tileX;
            analysis.TeleportTargetTileY = tileY;
            analysis.TeleportTargetWorldX = tileX * 16f + 8f;
            analysis.TeleportTargetWorldY = tileY * 16f + 8f;
        }
    }
}
