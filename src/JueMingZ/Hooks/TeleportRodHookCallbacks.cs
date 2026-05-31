using System;
using JueMingZ.Automation.Movement;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class TeleportRodHookCallbacks
    {
        private struct TeleportRodHookState
        {
            public bool Applied;
            public MouseTargetInputState RestoreState;
            public TeleportRodCorrectionPlan Plan;
        }

        private static void Prefix(object __instance, object[] __args, ref TeleportRodHookState __state)
        {
            __state = new TeleportRodHookState();

            try
            {
                MouseTargetInputState restoreState;
                TeleportRodCorrectionPlan plan;
                if (MovementTeleportCorrectionService.TryApplyBeforeVanilla(__instance, __args, out restoreState, out plan))
                {
                    __state.Applied = true;
                    __state.RestoreState = restoreState;
                    __state.Plan = plan;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TeleportRodHookCallbacks.Prefix", error);
                LogThrottle.ErrorThrottled(
                    "teleport-rod-hook-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "TeleportRodHookCallbacks",
                    "Teleport rod prefix failed; exception swallowed.", error);
            }
        }

        private static void Postfix(object __instance, object[] __args, ref TeleportRodHookState __state)
        {
            try
            {
                if (__state.Applied)
                {
                    MovementTeleportCorrectionService.RestoreAfterVanilla(__state.RestoreState, __state.Plan);
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TeleportRodHookCallbacks.Postfix", error);
                LogThrottle.ErrorThrottled(
                    "teleport-rod-hook-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "TeleportRodHookCallbacks",
                    "Teleport rod postfix failed; exception swallowed.", error);
            }
        }
    }
}
