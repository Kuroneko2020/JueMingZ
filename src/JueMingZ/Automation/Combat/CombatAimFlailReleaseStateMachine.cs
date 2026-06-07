using System;

namespace JueMingZ.Automation.Combat
{
    internal static class CombatAimFlailReleaseStateMachine
    {
        private const float StationaryVelocityEpsilon = 0.001f;

        public static CombatAimFlailControlDecision Decide(in CombatAimFlailDecisionContext context)
        {
            var projectile = context.Projectile;
            if (context.PhysicalHeld)
            {
                // Left-click flails must keep vanilla spin-hold while the
                // physical button is held; only release-pending states steer.
                if (!HasActiveProjectile(projectile))
                {
                    return context.ItemReady
                        ? CombatAimFlailControlDecision.None(FlailControlStates.SpinHold, "spinHoldNoProjectile")
                        : CombatAimFlailControlDecision.None(FlailControlStates.ReadyToLaunch, "itemUseCooldown");
                }

                if (IsReturnOrEndingState(projectile.Ai0))
                {
                    return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileActive, "spinHoldReturnState");
                }

                if (IsStationaryLaunchProjectile(projectile))
                {
                    return CombatAimFlailControlDecision.None(FlailControlStates.SpinHold, "spinHold");
                }

                return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileFlying, "physicalHoldProjectileMoving");
            }

            if (context.PhysicalReleasePending)
            {
                return CombatAimFlailControlDecision.Release(FlailControlStates.ReleaseToTarget, "physicalRelease", false);
            }

            if (context.InCooldown && !context.ReleaseInFlight)
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.Cooldown, "pulseCooldown");
            }

            if (!HasActiveProjectile(projectile))
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.Idle, "notUsingItem");
            }

            if (IsReturnOrEndingState(projectile.Ai0))
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileActive, "flailReturnState");
            }

            if (context.StuckRecovery)
            {
                return CombatAimFlailControlDecision.Release(FlailControlStates.StuckRecoveryRelease, "stuckRecoveryRelease:ai0ZeroVelocity", false);
            }

            if (context.HitDetected || context.CollisionDetected)
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileActive, context.HitDetected ? "hitDetected" : "collisionDetected");
            }

            if (!IsStationaryLaunchProjectile(projectile))
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileFlying, "projectileFlying");
            }

            return CombatAimFlailControlDecision.None(FlailControlStates.WaitHitOrCollision, context.ReleaseInFlight ? "waitReturnAfterRelease" : "waitSpinRelease");
        }

        public static bool IsReturnOrEndingState(float ai0)
        {
            return Math.Abs(ai0 - 4f) < 0.001f ||
                   Math.Abs(ai0 - 5f) < 0.001f ||
                   Math.Abs(ai0 - 6f) < 0.001f;
        }

        public static bool IsStationaryLaunchProjectile(CombatAimFlailProjectileFrame projectile)
        {
            return projectile != null &&
                   Math.Abs(projectile.Ai0) < 0.001f &&
                   Math.Abs(projectile.VelocityX) < StationaryVelocityEpsilon &&
                   Math.Abs(projectile.VelocityY) < StationaryVelocityEpsilon;
        }

        private static bool HasActiveProjectile(CombatAimFlailProjectileFrame projectile)
        {
            return projectile != null && projectile.Active && projectile.WhoAmI >= 0 && projectile.Type > 0;
        }
    }
}
