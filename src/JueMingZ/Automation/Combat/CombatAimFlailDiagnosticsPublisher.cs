using System;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Combat
{
    internal sealed class CombatAimFlailDiagnosticsPublisher
    {
        private readonly object _syncRoot = new object();
        private CombatAimFlailDiagnostics _lastDiagnostics = CombatAimFlailDiagnostics.Empty();
        private string _lastEventKey = string.Empty;
        private DateTime _lastEventUtc = DateTime.MinValue;

        public CombatAimFlailDiagnostics GetDecisionDiagnostics(CombatAimItemCheckDecision decision)
        {
            var last = GetLastDiagnostics();
            if (decision == null || decision.WeaponProfile == null)
            {
                return last;
            }

            var projectileAiStyle = decision.BallisticSolution == null ? 0 : decision.BallisticSolution.ProjectileAiStyle;
            var isYoyo = CombatAimYoyoCompat.IsYoyoProjectileType(decision.WeaponProfile.Shoot);
            var eligibility = CombatAimFlailPolicy.Evaluate(decision.WeaponProfile, projectileAiStyle, isYoyo);
            var diagnostics = CombatAimFlailDiagnostics.Empty();
            diagnostics.ItemType = decision.ItemType;
            diagnostics.ItemName = decision.ItemName ?? string.Empty;
            diagnostics.Eligible = eligibility.Eligible;
            diagnostics.Reason = eligibility.Reason;
            diagnostics.ProjectileAiStyle = projectileAiStyle;

            if (eligibility.Eligible &&
                last != null &&
                last.ItemType == decision.ItemType)
            {
                CopyRuntimeFields(last, diagnostics);
            }
            else
            {
                diagnostics.Active = false;
                diagnostics.State = eligibility.Eligible ? FlailControlStates.Idle : FlailControlStates.Disabled;
                diagnostics.BlockedReason = eligibility.Eligible ? string.Empty : eligibility.Reason;
            }

            return diagnostics;
        }

        public void PublishBlocked(CombatAimFlailDiagnostics diagnostics, string blockedReason)
        {
            if (diagnostics == null)
            {
                diagnostics = CombatAimFlailDiagnostics.Empty();
            }

            diagnostics.State = FlailControlStates.Disabled;
            diagnostics.BlockedReason = blockedReason ?? string.Empty;
            Publish(diagnostics);
        }

        public void Publish(CombatAimFlailDiagnostics diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (IsDuplicateInactiveDiagnostics(_lastDiagnostics, diagnostics))
                {
                    return;
                }

                _lastDiagnostics = diagnostics.Clone();
            }

            RecordEventIfUseful(diagnostics);
        }

        public CombatAimFlailDiagnostics GetLastDiagnostics()
        {
            lock (_syncRoot)
            {
                return _lastDiagnostics == null ? CombatAimFlailDiagnostics.Empty() : _lastDiagnostics.Clone();
            }
        }

        internal string BuildDiagnosticsJsonForTesting(CombatAimFlailDiagnostics diagnostics)
        {
            return BuildDiagnosticsJson(diagnostics ?? CombatAimFlailDiagnostics.Empty());
        }

        internal void ResetForTesting()
        {
            lock (_syncRoot)
            {
                _lastDiagnostics = CombatAimFlailDiagnostics.Empty();
                _lastEventKey = string.Empty;
                _lastEventUtc = DateTime.MinValue;
            }
        }

        private static void CopyRuntimeFields(CombatAimFlailDiagnostics source, CombatAimFlailDiagnostics destination)
        {
            destination.Active = source.Active;
            destination.State = source.State;
            destination.ProjectileWhoAmI = source.ProjectileWhoAmI;
            destination.ProjectileType = source.ProjectileType;
            destination.ProjectileAiStyle = source.ProjectileAiStyle;
            destination.ProjectileAi0 = source.ProjectileAi0;
            destination.ProjectileVelocityX = source.ProjectileVelocityX;
            destination.ProjectileVelocityY = source.ProjectileVelocityY;
            destination.ProjectileIdentity = source.ProjectileIdentity;
            destination.HitDetected = source.HitDetected;
            destination.CollisionDetected = source.CollisionDetected;
            destination.LocalNpcImmunityChanged = source.LocalNpcImmunityChanged;
            destination.TileCollisionDetected = source.TileCollisionDetected;
            destination.AttackPulse = source.AttackPulse;
            destination.AttackRelease = source.AttackRelease;
            destination.AttackSuppressed = source.AttackSuppressed;
            destination.AttackRestored = source.AttackRestored;
            destination.BlockedReason = source.BlockedReason;
            destination.InputMode = source.InputMode;
            destination.InputPhase = source.InputPhase;
            destination.TakeoverScope = source.TakeoverScope;
            destination.StuckRecovery = source.StuckRecovery;
            destination.ReleaseSuppressedPhysicalInput = source.ReleaseSuppressedPhysicalInput;
            destination.PhysicalUseItemHeld = source.PhysicalUseItemHeld;
            destination.PhysicalReleasePending = source.PhysicalReleasePending;
            destination.PulseReason = source.PulseReason;
            destination.CachedReleaseAim = source.CachedReleaseAim;
            destination.CachedReleaseAimAgeTicks = source.CachedReleaseAimAgeTicks;
            destination.CachedReleaseAimReason = source.CachedReleaseAimReason;
        }

        private static bool IsDuplicateInactiveDiagnostics(CombatAimFlailDiagnostics previous, CombatAimFlailDiagnostics current)
        {
            if (previous == null || current == null || current.Active)
            {
                return false;
            }

            return !previous.Active &&
                   previous.ItemType == current.ItemType &&
                   previous.Eligible == current.Eligible &&
                   previous.PhysicalUseItemHeld == current.PhysicalUseItemHeld &&
                   previous.PhysicalReleasePending == current.PhysicalReleasePending &&
                   string.Equals(previous.State ?? string.Empty, current.State ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(previous.Reason ?? string.Empty, current.Reason ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(previous.BlockedReason ?? string.Empty, current.BlockedReason ?? string.Empty, StringComparison.Ordinal);
        }

        private void RecordEventIfUseful(CombatAimFlailDiagnostics diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            var key = diagnostics.ItemType.ToString(CultureInfo.InvariantCulture) + "|" +
                      diagnostics.State + "|" +
                      diagnostics.BlockedReason + "|" +
                      diagnostics.AttackPulse + "|" +
                      diagnostics.AttackRelease + "|" +
                      diagnostics.AttackSuppressed + "|" +
                      diagnostics.TakeoverScope + "|" +
                      diagnostics.StuckRecovery + "|" +
                      diagnostics.HitDetected + "|" +
                      diagnostics.CollisionDetected;

            var now = DateTime.UtcNow;
            var important = diagnostics.AttackPulse || diagnostics.AttackRelease || diagnostics.HitDetected || diagnostics.CollisionDetected;
            if (!diagnostics.Active && !important)
            {
                return;
            }

            var interval = diagnostics.Active ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(5);
            if (!important &&
                string.Equals(key, _lastEventKey, StringComparison.Ordinal) &&
                now - _lastEventUtc < interval)
            {
                return;
            }

            _lastEventKey = key;
            _lastEventUtc = now;
            // Keep metadata JSON behind the event gate; diagnostics reads must not
            // pay string-building cost on inactive or duplicate frames.
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "CombatAim.FlailControl",
                "Aim",
                string.Empty,
                diagnostics.AttackPulse || diagnostics.AttackRelease ? "Applied" : "Observed",
                diagnostics.AttackPulse || diagnostics.AttackRelease ? DiagnosticResultCode.Succeeded.ToString() : DiagnosticResultCode.NotApplicable.ToString(),
                "Combat flail control state: " + diagnostics.State,
                0,
                "{}",
                "{}",
                BuildDiagnosticsJson(diagnostics),
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static string BuildDiagnosticsJson(CombatAimFlailDiagnostics diagnostics)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "flailControlEligible", BoolRaw(diagnostics.Eligible), true);
            AppendString(builder, "flailControlReason", diagnostics.Reason, true);
            AppendRaw(builder, "flailControlActive", BoolRaw(diagnostics.Active), true);
            AppendString(builder, "flailControlState", diagnostics.State, true);
            AppendString(builder, "flailInputMode", diagnostics.InputMode, true);
            AppendString(builder, "flailInputPhase", diagnostics.InputPhase, true);
            AppendString(builder, "flailTakeoverScope", diagnostics.TakeoverScope, true);
            AppendRaw(builder, "flailPhysicalUseItemHeld", BoolRaw(diagnostics.PhysicalUseItemHeld), true);
            AppendRaw(builder, "flailPhysicalReleasePending", BoolRaw(diagnostics.PhysicalReleasePending), true);
            AppendRaw(builder, "flailProjectileWhoAmI", IntRaw(diagnostics.ProjectileWhoAmI), true);
            AppendRaw(builder, "flailProjectileType", IntRaw(diagnostics.ProjectileType), true);
            AppendRaw(builder, "flailProjectileAiStyle", IntRaw(diagnostics.ProjectileAiStyle), true);
            AppendRaw(builder, "flailProjectileAi0", FloatRaw(diagnostics.ProjectileAi0), true);
            AppendRaw(builder, "flailProjectileVelocity", BuildPointJson(diagnostics.ProjectileVelocityX, diagnostics.ProjectileVelocityY), true);
            AppendRaw(builder, "flailProjectileIdentity", IntRaw(diagnostics.ProjectileIdentity), true);
            AppendRaw(builder, "flailHitDetected", BoolRaw(diagnostics.HitDetected), true);
            AppendRaw(builder, "flailCollisionDetected", BoolRaw(diagnostics.CollisionDetected), true);
            AppendRaw(builder, "flailLocalNpcImmunityChanged", BoolRaw(diagnostics.LocalNpcImmunityChanged), true);
            AppendRaw(builder, "flailTileCollisionDetected", BoolRaw(diagnostics.TileCollisionDetected), true);
            AppendRaw(builder, "flailAttackPulse", BoolRaw(diagnostics.AttackPulse), true);
            AppendRaw(builder, "flailAttackRelease", BoolRaw(diagnostics.AttackRelease), true);
            AppendRaw(builder, "flailAttackSuppressed", BoolRaw(diagnostics.AttackSuppressed), true);
            AppendRaw(builder, "flailAttackRestored", BoolRaw(diagnostics.AttackRestored), true);
            AppendString(builder, "flailPulseReason", diagnostics.PulseReason, true);
            AppendString(builder, "flailStuckRecovery", diagnostics.StuckRecovery, true);
            AppendRaw(builder, "flailCachedReleaseAim", BoolRaw(diagnostics.CachedReleaseAim), true);
            AppendRaw(builder, "flailCachedReleaseAimAgeTicks", IntRaw(diagnostics.CachedReleaseAimAgeTicks), true);
            AppendString(builder, "flailCachedReleaseAimReason", diagnostics.CachedReleaseAimReason, true);
            AppendRaw(builder, "flailReleaseSuppressedPhysicalInput", BoolRaw(diagnostics.ReleaseSuppressedPhysicalInput), true);
            AppendString(builder, "flailControlBlockedReason", diagnostics.BlockedReason, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildPointJson(float x, float y)
        {
            return "{" +
                   "\"x\":" + FloatRaw(x) + "," +
                   "\"y\":" + FloatRaw(y) +
                   "}";
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(name).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(name).Append("\":\"").Append(Escape(value)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FloatRaw(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
