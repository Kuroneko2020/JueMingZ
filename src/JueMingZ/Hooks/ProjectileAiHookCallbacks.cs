using System;
using JueMingZ.Automation.Combat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class ProjectileAiHookCallbacks
    {
        // ProjectileAI hooks may lend cursor context to combat services for one
        // projectile frame only; projectile data and network state stay untouched.
        private struct ProjectileAiHookState
        {
            public CombatAimPersistentCursorService.ActiveOverride ActiveOverride;
        }

        private static void Prefix(object __instance, ref ProjectileAiHookState __state)
        {
            __state = new ProjectileAiHookState();
            try
            {
                // ProjectileAI may only receive a scoped cursor override here.
                // The service must not edit projectile state or network fields.
                CombatAimPersistentCursorService.ActiveOverride active;
                if (CombatAimPersistentCursorService.TryBeginProjectileAi(__instance, out active))
                {
                    __state.ActiveOverride = active;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("ProjectileAiHookCallbacks.Prefix", error);
                LogThrottle.ErrorThrottled(
                    "projectile-ai-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "ProjectileAiHookCallbacks",
                    "Projectile AI prefix failed; exception swallowed.", error);
            }
        }

        private static void Postfix(ref ProjectileAiHookState __state)
        {
            try
            {
                // Restore the cursor in the paired postfix for this hook frame;
                // scoped cursor state must not leak into later ProjectileAI calls.
                CombatAimPersistentCursorService.EndProjectileAi(__state.ActiveOverride);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("ProjectileAiHookCallbacks.Postfix", error);
                LogThrottle.ErrorThrottled(
                    "projectile-ai-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "ProjectileAiHookCallbacks",
                    "Projectile AI postfix failed; exception swallowed.", error);
            }
        }
    }
}
