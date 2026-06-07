using System;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class BuffRemovalHookCallbacks
    {
        // This prefix observes a vanilla buff removal request before Terraria
        // mutates buffs; follow logic must not add or remove buffs from the hook.
        private static void Prefix(object __instance, object[] __args, System.Reflection.MethodBase __originalMethod)
        {
            try
            {
                if (__instance == null || __args == null || __args.Length <= 0)
                {
                    return;
                }

                int buffIndex;
                try
                {
                    buffIndex = Convert.ToInt32(__args[0]);
                }
                catch
                {
                    return;
                }

                AutoBuffFollowService.NotifyBuffRemovedByPlayerUi(
                    __instance,
                    buffIndex,
                    __originalMethod == null ? string.Empty : __originalMethod.Name);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("BuffRemovalHookCallbacks.Prefix", error);
                LogThrottle.ErrorThrottled(
                    "buff-removal-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "BuffRemovalHookCallbacks",
                    "Buff removal prefix failed; exception swallowed.", error);
            }
        }
    }
}
