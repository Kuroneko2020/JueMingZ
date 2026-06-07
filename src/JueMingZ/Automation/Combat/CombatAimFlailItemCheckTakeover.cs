using System;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Combat
{
    public static partial class CombatAimFlailControlService
    {
        public static bool TryBeginItemCheckTakeover(
            object player,
            CombatAimItemCheckDecision decision,
            out TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            return CombatAimFlailItemCheckTakeover.TryBegin(
                player,
                decision,
                GetDecisionDiagnostics,
                IsItemCheckReleaseDecision,
                Publish,
                out takeover);
        }
    }

    internal static class CombatAimFlailItemCheckTakeover
    {
        public static bool TryBegin(
            object player,
            CombatAimItemCheckDecision decision,
            Func<CombatAimItemCheckDecision, CombatAimFlailDiagnostics> getDiagnostics,
            Func<CombatAimItemCheckDecision, bool> isReleaseDecision,
            Action<CombatAimFlailDiagnostics> publish,
            out TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            takeover = null;
            if (player == null || decision == null || getDiagnostics == null || isReleaseDecision == null || publish == null)
            {
                return false;
            }

            var diagnostics = getDiagnostics(decision);
            if (diagnostics == null ||
                !diagnostics.Eligible ||
                !diagnostics.Active)
            {
                return false;
            }

            var releaseDecision = isReleaseDecision(decision);
            if (!diagnostics.AttackPulse &&
                !diagnostics.AttackSuppressed &&
                !releaseDecision)
            {
                return false;
            }

            if (releaseDecision && !diagnostics.AttackPulse)
            {
                PromoteReleaseDiagnostics(diagnostics, decision);
            }

            var pressed = diagnostics.AttackPulse;
            // ItemCheck takeover emits one scoped press/release and relies on
            // the hook postfix to restore captured input.
            if (!TerrariaInputCompat.TryBeginScopedUseItemTakeover(player, pressed, "ItemCheck", out takeover))
            {
                diagnostics.BlockedReason = "itemCheckTakeoverFailed:" + TerrariaInputCompat.LastInputCompatError;
                publish(diagnostics);
                return false;
            }

            diagnostics.TakeoverScope = "ItemCheck";
            diagnostics.InputPhase = diagnostics.State;
            diagnostics.ReleaseSuppressedPhysicalInput = !pressed && takeover.SuppressedPhysicalInput;
            publish(diagnostics);
            if (!pressed)
            {
                // Release tails pass cursor aim to ProjectileAI only; they do
                // not mutate projectile ai, velocity, position, or network state.
                CombatAimPersistentCursorService.RememberFlailReleaseTail(decision);
            }

            return true;
        }

        private static void PromoteReleaseDiagnostics(
            CombatAimFlailDiagnostics diagnostics,
            CombatAimItemCheckDecision decision)
        {
            diagnostics.State = FlailControlStates.ReleaseToTarget;
            diagnostics.AttackRelease = true;
            diagnostics.AttackSuppressed = true;
            diagnostics.AttackRestored = false;
            diagnostics.InputMode = "controlledUseItemRelease";
            diagnostics.InputPhase = FlailControlStates.ReleaseToTarget;
            diagnostics.BlockedReason = string.IsNullOrWhiteSpace(diagnostics.BlockedReason) ||
                                        string.Equals(diagnostics.BlockedReason, "spinHold", StringComparison.Ordinal) ||
                                        string.Equals(diagnostics.BlockedReason, "spinHoldNoProjectile", StringComparison.Ordinal)
                ? "releaseHoldItemCheck"
                : diagnostics.BlockedReason;
            diagnostics.PhysicalUseItemHeld = false;
            diagnostics.PhysicalReleasePending = true;
            diagnostics.CachedReleaseAim = diagnostics.CachedReleaseAim ||
                                           string.Equals(decision.ReleaseHoldValidationReason, "cachedFlailReleaseAim", StringComparison.Ordinal);
            if (diagnostics.CachedReleaseAim && string.IsNullOrWhiteSpace(diagnostics.CachedReleaseAimReason))
            {
                diagnostics.CachedReleaseAimReason = "usedForPhysicalRelease";
            }
        }
    }
}
