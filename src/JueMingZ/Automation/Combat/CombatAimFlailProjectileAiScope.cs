using System;

namespace JueMingZ.Automation.Combat
{
    public static partial class CombatAimFlailControlService
    {
        public static void MarkProjectileAiScopedTakeover(CombatAimItemCheckDecision decision, CombatAimProjectileCursorMatch match)
        {
            CombatAimFlailProjectileAiScope.Mark(decision, match, GetDecisionDiagnostics, Publish);
        }
    }

    internal static class CombatAimFlailProjectileAiScope
    {
        public static void Mark(
            CombatAimItemCheckDecision decision,
            CombatAimProjectileCursorMatch match,
            Func<CombatAimItemCheckDecision, CombatAimFlailDiagnostics> getDiagnostics,
            Action<CombatAimFlailDiagnostics> publish)
        {
            if (decision == null || getDiagnostics == null || publish == null)
            {
                return;
            }

            var diagnostics = getDiagnostics(decision);
            if (diagnostics == null || !diagnostics.Eligible)
            {
                return;
            }

            diagnostics.Active = true;
            diagnostics.State = string.IsNullOrWhiteSpace(diagnostics.State) || string.Equals(diagnostics.State, FlailControlStates.Idle, StringComparison.Ordinal)
                ? FlailControlStates.ReleaseToTarget
                : diagnostics.State;
            diagnostics.InputPhase = string.IsNullOrWhiteSpace(diagnostics.InputPhase) ? diagnostics.State : diagnostics.InputPhase;
            diagnostics.InputMode = string.IsNullOrWhiteSpace(diagnostics.InputMode) ? "projectileAiScopedCursor" : diagnostics.InputMode;
            diagnostics.TakeoverScope = "ProjectileAI";
            // ProjectileAI scope is diagnostic and cursor-only; flail projectile
            // fields remain vanilla-owned.
            diagnostics.AttackRelease = true;
            diagnostics.AttackSuppressed = true;
            diagnostics.PhysicalUseItemHeld = false;
            diagnostics.PhysicalReleasePending = true;
            if (match != null && match.Matches)
            {
                diagnostics.ProjectileWhoAmI = match.ProjectileWhoAmI;
                diagnostics.ProjectileType = match.ProjectileType;
                diagnostics.ProjectileAiStyle = match.ProjectileAiStyle;
            }

            publish(diagnostics);
        }
    }
}
