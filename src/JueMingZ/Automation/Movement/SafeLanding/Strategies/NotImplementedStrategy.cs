using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Automation.Movement
{
    internal sealed class NotImplementedStrategy : IMovementSafeLandingStrategy
    {
        public IEnumerable<MovementSafeLandingStrategyEvaluation> Evaluate(MovementSafeLandingStrategyContext context)
        {
            yield return Placeholder(6, MovementSafeLandingStrategyIds.PlaceholderFatalMitigation, "fatal_only_mitigation");
        }

        private static MovementSafeLandingStrategyEvaluation Placeholder(int priority, string strategyId, string label)
        {
            return new MovementSafeLandingStrategyEvaluation
            {
                StrategyId = strategyId,
                Priority = priority,
                ActionType = MovementSafeLandingActionTypes.NotImplemented,
                RequestKind = InputActionKind.None,
                RequiredChannels = InputActionChannel.None,
                IsCandidate = false,
                IsReady = false,
                BlocksLowerPriority = false,
                RequiresTemporaryEquipment = false,
                RequiresRestore = false,
                SkipReason = MovementSafeLandingSkipReasons.NotImplemented,
                Confidence = "none",
                Readiness = "notImplemented",
                SortReason = label + ":placeholderOnly"
            };
        }
    }
}
