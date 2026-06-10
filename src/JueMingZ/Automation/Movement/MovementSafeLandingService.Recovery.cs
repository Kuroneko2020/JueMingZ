using System;
using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Automation.Movement
{
    public static partial class MovementSafeLandingService
    {
        public static void AfterActionQueueUpdate(InputActionQueueFastState queueSnapshot)
        {
            var result = queueSnapshot == null ? null : queueSnapshot.LastResult;
            if (result == null ||
                !string.Equals(result.Scenario, ScenarioNames.MovementSafeLanding, StringComparison.Ordinal))
            {
                return;
            }

            RouteSafeLandingActionQueueResult(result);
        }
    }
}