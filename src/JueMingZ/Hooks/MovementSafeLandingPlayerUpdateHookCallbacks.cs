using System;
using JueMingZ.Config;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class MovementSafeLandingPlayerUpdateHookCallbacks
    {
        // Player.Update prefix can apply queued jump pulses only. It must not edit
        // velocity, position, fallStart, noFallDmg, or ability state directly.
        private static void Prefix(object __instance)
        {
            try
            {
                if (__instance == null || !TerrariaInputCompat.TryIsLocalPlayer(__instance))
                {
                    return;
                }

                var settings = ConfigService.AppSettings;
                var safeLandingEnabled = settings != null && settings.MovementSafeLandingEnabled;
                var simulatedJumpEnabled = settings != null && settings.MovementSimulatedMultiJumpEnabled;
                if (!safeLandingEnabled)
                {
                    MovementSafeLandingCompat.CancelSafeLandingJumpPulse(Guid.Empty, "movement.fall_protection disabled before Player.Update");
                }

                if (!simulatedJumpEnabled)
                {
                    MovementSimulatedJumpPulseCompat.CancelSimulatedJumpPulse(Guid.Empty, "movement.simulated_multi_jump disabled before Player.Update");
                }

                if (safeLandingEnabled)
                {
                    MovementSafeLandingCompat.ApplyQueuedSafeLandingJumpPulseBeforePlayerUpdate(__instance);
                }

                if (simulatedJumpEnabled)
                {
                    MovementSimulatedJumpPulseCompat.ApplyQueuedSimulatedJumpPulseBeforePlayerUpdate(__instance);
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingPlayerUpdateHookCallbacks.Prefix", error);
                LogThrottle.ErrorThrottled(
                    "movement-safe-landing-player-update-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "MovementSafeLandingPlayerUpdateHookCallbacks",
                    "Safe landing Player.Update prefix failed; exception swallowed.", error);
            }
        }
    }
}
