namespace JueMingZ.Automation.Combat
{
    public static partial class CombatAimFlailControlService
    {
        internal static CombatAimFlailControlDecision DecideForTesting(
            bool releasePending,
            bool hasProjectile,
            bool inCooldown,
            float ai0,
            bool hitDetected,
            bool collisionDetected)
        {
            return DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(hasProjectile, hasProjectile ? 1 : -1, 1, 1, ai0, 1f, 0f),
                true,
                inCooldown,
                hitDetected,
                collisionDetected,
                false,
                releasePending,
                releasePending,
                releasePending);
        }

        internal static CombatAimFlailControlDecision DecideForTesting(
            bool releasePending,
            string releasePendingKind,
            bool hasProjectile,
            bool itemReady,
            bool inCooldown,
            float ai0,
            bool hitDetected,
            bool collisionDetected)
        {
            return DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(hasProjectile, hasProjectile ? 1 : -1, 1, 1, ai0, 1f, 0f),
                itemReady,
                inCooldown,
                hitDetected,
                collisionDetected,
                false,
                !releasePending,
                releasePending,
                releasePending);
        }

        internal static CombatAimFlailControlDecision DecideForTesting(
            CombatAimFlailProjectileFrame projectile,
            bool itemReady,
            bool inCooldown,
            bool hitDetected,
            bool collisionDetected,
            bool stuckRecovery,
            bool physicalHeld,
            bool physicalReleasePending,
            bool releaseInFlight)
        {
            var context = new CombatAimFlailDecisionContext
            {
                Projectile = projectile,
                ItemReady = itemReady,
                InCooldown = inCooldown,
                HitDetected = hitDetected,
                CollisionDetected = collisionDetected,
                StuckRecovery = stuckRecovery,
                PhysicalHeld = physicalHeld,
                PhysicalReleasePending = physicalReleasePending,
                ReleaseInFlight = releaseInFlight
            };
            return CombatAimFlailReleaseStateMachine.Decide(in context);
        }

        internal static string BuildFlailDiagnosticsJsonForTesting(CombatAimFlailDiagnostics diagnostics)
        {
            return DiagnosticsPublisher.BuildDiagnosticsJsonForTesting(diagnostics);
        }

        internal static void SetLastDiagnosticsForTesting(CombatAimFlailDiagnostics diagnostics)
        {
            Publish(diagnostics == null ? CombatAimFlailDiagnostics.Empty() : diagnostics);
        }

        internal static void ResetForTesting()
        {
            ResetControlState();
            ClearFlailComboPressAim();
            DiagnosticsPublisher.ResetForTesting();
        }
    }
}
