using System;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Combat
{
    public static partial class CombatAimFlailControlService
    {
        internal static bool TryRememberExistingItemCheckReleaseTail(CombatAimItemCheckDecision decision, string takeoverScope)
        {
            if (!IsItemCheckReleaseDecision(decision))
            {
                return false;
            }

            return TryRememberReleaseTailFromDecision(decision, takeoverScope, "existingItemCheckRelease", false);
        }

        internal static bool TryRememberFlailComboPressAim(CombatAimItemCheckDecision decision)
        {
            if (!IsFlailTailSourceDecision(decision))
            {
                return false;
            }

            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);
            // Combo press aim is a short-lived fallback for a later virtual
            // release tail, not a second input takeover.
            ReleaseAimCache.RememberFlailComboPressAim(decision, tick);
            return true;
        }

        internal static bool TryRememberFlailComboPressReleaseTail(string takeoverScope)
        {
            CombatAimItemCheckDecision decision;
            if (!TryGetRecentFlailComboPressAim(out decision))
            {
                return false;
            }

            // Use the remembered press aim only when no fresh release decision
            // exists for the combo release frame.
            return TryRememberReleaseTailFromDecision(decision, takeoverScope, "flailComboPressAim", true);
        }

        private static bool TryRememberReleaseTailFromDecision(
            CombatAimItemCheckDecision decision,
            string takeoverScope,
            string blockedReason,
            bool markCachedReleaseAim)
        {
            if (!IsFlailTailSourceDecision(decision))
            {
                return false;
            }

            var diagnostics = GetDecisionDiagnostics(decision);
            if (diagnostics == null || !diagnostics.Eligible)
            {
                return false;
            }

            diagnostics.Active = true;
            diagnostics.State = FlailControlStates.ReleaseToTarget;
            diagnostics.AttackPulse = false;
            diagnostics.AttackRelease = true;
            diagnostics.AttackSuppressed = true;
            diagnostics.AttackRestored = false;
            diagnostics.InputMode = "controlledUseItemRelease";
            diagnostics.InputPhase = FlailControlStates.ReleaseToTarget;
            diagnostics.TakeoverScope = string.IsNullOrWhiteSpace(takeoverScope) ? "ItemCheck" : takeoverScope;
            diagnostics.BlockedReason = string.IsNullOrWhiteSpace(blockedReason) ? "releaseTail" : blockedReason;
            diagnostics.PhysicalUseItemHeld = false;
            diagnostics.PhysicalReleasePending = true;
            diagnostics.CachedReleaseAim = diagnostics.CachedReleaseAim ||
                                           markCachedReleaseAim ||
                                           string.Equals(decision.ReleaseHoldValidationReason, "cachedFlailReleaseAim", StringComparison.Ordinal);
            if (diagnostics.CachedReleaseAim && string.IsNullOrWhiteSpace(diagnostics.CachedReleaseAimReason))
            {
                diagnostics.CachedReleaseAimReason = markCachedReleaseAim ? "flailComboPressAim" : "usedForExistingRelease";
            }

            Publish(diagnostics);
            return CombatAimPersistentCursorService.RememberFlailReleaseTail(decision);
        }

        private static bool TryGetRecentFlailComboPressAim(out CombatAimItemCheckDecision decision)
        {
            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);
            return ReleaseAimCache.TryGetRecentFlailComboPressAim(tick, out decision);
        }

        private static bool IsFlailTailSourceDecision(CombatAimItemCheckDecision decision)
        {
            if (decision == null || decision.WeaponProfile == null || decision.BallisticSolution == null)
            {
                return false;
            }

            var isYoyo = CombatAimYoyoCompat.IsYoyoProjectileType(decision.WeaponProfile.Shoot);
            var eligibility = CombatAimFlailPolicy.Evaluate(decision.WeaponProfile, decision.BallisticSolution.ProjectileAiStyle, isYoyo);
            return eligibility.Eligible;
        }

        private static bool IsItemCheckReleaseDecision(CombatAimItemCheckDecision decision)
        {
            if (!IsFlailTailSourceDecision(decision))
            {
                return false;
            }

            return decision.ReleaseDetected ||
                   decision.ReleasedThisTick ||
                   decision.ReleaseHoldActive ||
                   decision.ReleaseHoldPending ||
                   string.Equals(decision.AimApplyMode, CombatAimApplyModes.ReleaseHold, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(decision.ReleaseHoldValidationReason, "cachedFlailReleaseAim", StringComparison.Ordinal);
        }
    }
}
