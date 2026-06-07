using JueMingZ.Automation.Combat;

namespace JueMingZ.Hooks
{
    internal static class ProjectileKillHookCallbacks
    {
        // Projectile.Kill scope exists only to retire persistent cursor overrides;
        // it must not change projectile lifetime, damage, or ownership.
        private struct ProjectileKillHookState
        {
            public CombatAimPersistentCursorService.ActiveOverride ActiveOverride;
        }

        private static void Prefix(object __instance, ref ProjectileKillHookState __state)
        {
            __state = new ProjectileKillHookState();
            CombatAimPersistentCursorService.ActiveOverride active;
            if (CombatAimPersistentCursorService.TryBeginProjectileKill(__instance, out active))
            {
                __state.ActiveOverride = active;
            }
        }

        private static void Postfix(ref ProjectileKillHookState __state)
        {
            CombatAimPersistentCursorService.EndProjectileKill(__state.ActiveOverride);
        }
    }
}
