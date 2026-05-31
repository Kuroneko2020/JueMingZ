using System;
using JueMingZ.Automation.Fishing;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class FishingBobberHookCallbacks
    {
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
