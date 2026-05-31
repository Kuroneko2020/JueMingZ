using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Automation.Movement
{
    internal sealed class TemporaryUmbrellaStrategy : IMovementSafeLandingStrategy
    {
        public IEnumerable<MovementSafeLandingStrategyEvaluation> Evaluate(MovementSafeLandingStrategyContext context)
        {
            var plan = context == null ? null : context.TemporaryEquipmentPlan;
            var isUmbrella = plan != null && plan.SelectedPriority == 3;
            string timingReason = string.Empty;
            var ready = false;
            if (isUmbrella)
            {
                ready = MovementSafeLandingTiming.IsTemporaryApplyWindowReady(
                    plan,
                    context == null ? null : context.Analysis,
                    out timingReason);
            }

            yield return new MovementSafeLandingStrategyEvaluation
            {
                StrategyId = isUmbrella ? plan.StrategyId : MovementSafeLandingStrategyIds.TemporaryUmbrella,
                Priority = 3,
                ActionType = MovementSafeLandingActionTypes.EquipOnly,
                RequestKind = InputActionKind.InventorySlot,
                RequiredChannels = InputActionChannel.InventorySlot | InputActionChannel.HotbarSelection,
                TimingWindow = timingReason,
                IsCandidate = isUmbrella,
                IsReady = isUmbrella && ready,
                BlocksLowerPriority = isUmbrella,
                RequiresTemporaryEquipment = true,
                RequiresRestore = isUmbrella,
                SkipReason = isUmbrella
                    ? ready ? string.Empty : "waitingForRescueWindow:" + plan.StrategyId + ":" + timingReason
                    : MovementSafeLandingSkipReasons.PlanUnavailable,
                Confidence = isUmbrella ? "medium" : "none",
                Readiness = isUmbrella ? ready ? "ready" : "timingNotReady" : "noUmbrellaPlan",
                SortReason = "priority3-after-priority1-and-priority2",
                EquipmentPlan = plan
            };
        }
    }
}
