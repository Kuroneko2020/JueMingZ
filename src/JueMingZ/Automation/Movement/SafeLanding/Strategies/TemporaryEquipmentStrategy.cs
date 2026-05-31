using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Automation.Movement
{
    internal sealed class TemporaryEquipmentStrategy : IMovementSafeLandingStrategy
    {
        public IEnumerable<MovementSafeLandingStrategyEvaluation> Evaluate(MovementSafeLandingStrategyContext context)
        {
            var plan = context == null ? null : context.TemporaryEquipmentPlan;
            var isPriorityTwo = plan != null && plan.SelectedPriority == 2;
            string timingReason = string.Empty;
            var ready = false;
            if (isPriorityTwo)
            {
                ready = MovementSafeLandingTiming.IsTemporaryApplyWindowReady(
                    plan,
                    context == null ? null : context.Analysis,
                    out timingReason);
            }

            yield return new MovementSafeLandingStrategyEvaluation
            {
                StrategyId = isPriorityTwo ? plan.StrategyId : "temporary_equipment",
                Priority = 2,
                ActionType = isPriorityTwo ? plan.ActionType : MovementSafeLandingActionTypes.InventorySlot,
                RequestKind = InputActionKind.InventorySlot,
                RequiredChannels = ResolveChannels(plan),
                TimingWindow = timingReason,
                IsCandidate = isPriorityTwo,
                IsReady = isPriorityTwo && ready,
                BlocksLowerPriority = isPriorityTwo,
                RequiresTemporaryEquipment = true,
                RequiresRestore = isPriorityTwo,
                SkipReason = isPriorityTwo
                    ? ready ? string.Empty : "waitingForRescueWindow:" + plan.StrategyId + ":" + timingReason
                    : MovementSafeLandingSkipReasons.PlanUnavailable,
                Confidence = isPriorityTwo ? "medium" : "none",
                Readiness = isPriorityTwo ? ready ? "ready" : "timingNotReady" : "noPriority2Plan",
                SortReason = "priority2-order=horseshoe>wings>fairy_boots>double_jump>rocket_boots>flying_carpet>gravity_globe>flying_mount>safe_mount",
                EquipmentPlan = plan
            };
        }

        private static InputActionChannel ResolveChannels(MovementSafeLandingEquipmentPlan plan)
        {
            var channels = InputActionChannel.InventorySlot;
            if (plan != null &&
                plan.TargetContainerKind == MovementSafeLandingEquipmentContainerKind.Hotbar)
            {
                channels |= InputActionChannel.HotbarSelection;
            }

            return channels;
        }
    }
}
