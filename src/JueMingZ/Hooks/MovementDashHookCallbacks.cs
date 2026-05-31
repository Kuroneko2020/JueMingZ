using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class MovementDashHookCallbacks
    {
        private struct DashMovementHookState
        {
            public DashPulseApplyResult Pulse;
        }

        private static void Prefix(object __instance, ref DashMovementHookState __state)
        {
            __state = new DashMovementHookState();
            try
            {
                if (__instance == null || !TerrariaInputCompat.TryIsLocalPlayer(__instance))
                {
                    return;
                }

                DashPulseApplyResult pulse;
                if (TerrariaDashCompat.TryApplyQueuedContinuousDashPulseBeforeDashMovement(__instance, out pulse) && pulse != null && pulse.Applied)
                {
                    __state.Pulse = pulse;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementDashHookCallbacks.Prefix", error);
                LogThrottle.ErrorThrottled(
                    "movement-dash-hook-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "MovementDashHookCallbacks",
                    "DashMovement prefix failed; exception swallowed.", error);
            }
        }

        private static void Postfix(object __instance, ref DashMovementHookState __state)
        {
            try
            {
                if (__state.Pulse != null && __state.Pulse.Applied)
                {
                    TerrariaDashCompat.ResetDashPulseAfterDashMovement(__instance, __state.Pulse);
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementDashHookCallbacks.Postfix", error);
                LogThrottle.ErrorThrottled(
                    "movement-dash-hook-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "MovementDashHookCallbacks",
                    "DashMovement postfix failed; exception swallowed.", error);
            }
        }
    }
}
