using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Automation.Movement
{
    internal sealed class AlreadySafeStrategy : IMovementSafeLandingStrategy
    {
        public IEnumerable<MovementSafeLandingStrategyEvaluation> Evaluate(MovementSafeLandingStrategyContext context)
        {
            var hazard = context == null ? null : context.Hazard;
            var safe = hazard != null && hazard.AlreadySafe;
            yield return new MovementSafeLandingStrategyEvaluation
            {
                StrategyId = MovementSafeLandingStrategyIds.AlreadySafe,
                Priority = 0,
                ActionType = MovementSafeLandingActionTypes.None,
                RequestKind = InputActionKind.None,
                RequiredChannels = InputActionChannel.None,
                IsCandidate = safe,
                IsReady = safe,
                BlocksLowerPriority = safe,
                RequiresTemporaryEquipment = false,
                RequiresRestore = false,
                SkipReason = safe ? string.Empty : "notAlreadySafe",
                Confidence = safe ? "high" : "none",
                Readiness = safe ? hazard.SafeReason ?? string.Empty : "notAlreadySafe",
                SortReason = "priority0-already-safe"
            };
        }
    }
}
