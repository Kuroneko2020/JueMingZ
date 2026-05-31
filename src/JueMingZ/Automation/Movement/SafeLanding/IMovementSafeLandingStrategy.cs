using System.Collections.Generic;

namespace JueMingZ.Automation.Movement
{
    internal interface IMovementSafeLandingStrategy
    {
        IEnumerable<MovementSafeLandingStrategyEvaluation> Evaluate(MovementSafeLandingStrategyContext context);
    }
}
