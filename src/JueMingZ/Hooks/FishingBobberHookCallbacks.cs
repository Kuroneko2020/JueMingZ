using System;
using JueMingZ.Automation.Fishing;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class FishingBobberHookCallbacks
    {
        // Bobber AI postfix is read-only telemetry. Automation may consume the
        // observation later, but this hook must not reel, switch rods, or enqueue.
        private static void Postfix(object __instance)
        {
            try
            {
                FishingBobberObservation observation;
                if (!TerrariaFishingCompat.TryReadBobberObservation(__instance, out observation) || observation == null)
                {
                    return;
                }

                FishingBobberObserver.Observe(observation);
                FishingAutomationDiagnostics.MarkHookObservation(observation.GameUpdateCount);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("FishingBobberHookCallbacks.Postfix", error);
                LogThrottle.ErrorThrottled(
                    "fishing-bobber-hook-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "FishingBobberHookCallbacks",
                    "Fishing bobber hook postfix failed; exception swallowed.", error);
            }
        }
    }
}
