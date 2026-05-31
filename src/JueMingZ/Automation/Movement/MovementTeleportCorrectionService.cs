using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Movement
{
    public static class MovementTeleportCorrectionService
    {
        private static readonly object DiagnosticsSyncRoot = new object();
        private static readonly object EventThrottleSyncRoot = new object();
        private static readonly Dictionary<string, DateTime> LastEventByReason = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private static MovementTeleportCorrectionDiagnosticInfo _diagnostics = new MovementTeleportCorrectionDiagnosticInfo();

        public static MovementTeleportCorrectionDiagnosticInfo GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new MovementTeleportCorrectionDiagnosticInfo() : _diagnostics.Clone();
                current.Enabled = ConfigService.AppSettings != null && ConfigService.AppSettings.MovementTeleportCorrectionEnabled;
                current.HookInstalled = HookDiagnostics.TeleportRodHookInstalled;
                current.HookMethod = HookDiagnostics.TeleportRodHookMethod;
                current.HookMessage = HookDiagnostics.TeleportRodHookMessage;
                return current;
            }
        }

        public static void RecordHookMissing(string message)
        {
            var plan = new TeleportRodCorrectionPlan
            {
                SkipReason = "hookMissing",
                Message = message ?? "Teleport rod hook missing.",
                CompatError = message ?? string.Empty
            };
            RecordAttempt(false, "skipped", "hookMissing", plan, false, true);
        }

        public static bool TryApplyBeforeVanilla(object player, object[] args, out MouseTargetInputState restoreState, out TeleportRodCorrectionPlan plan)
        {
            restoreState = null;
            plan = new TeleportRodCorrectionPlan();

            try
            {
                object item;
                if (!TerrariaTeleportRodCompat.TryFindItemArgument(args, out item))
                {
                    plan.SkipReason = "itemUnavailable";
                    plan.CompatError = TerrariaTeleportRodCompat.LastError;
                    RecordAttempt(false, "skipped", "itemUnavailable", plan, false, false);
                    return false;
                }

                int itemType;
                string itemName;
                TerrariaTeleportRodCompat.TryReadItemInfo(item, out itemType, out itemName);
                plan.ItemType = itemType;
                plan.ItemName = itemName;

                if (!TerrariaTeleportRodCompat.IsTeleportRodItem(itemType))
                {
                    plan.SkipReason = "notTeleportRod";
                    RecordAttempt(false, "skipped", "notTeleportRod", plan, false, false);
                    return false;
                }

                var enabled = ConfigService.AppSettings != null && ConfigService.AppSettings.MovementTeleportCorrectionEnabled;
                if (!enabled)
                {
                    plan.SkipReason = "disabled";
                    RecordAttempt(false, "skipped", "disabled", plan, false, false);
                    return false;
                }

                if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
                {
                    plan.SkipReason = "nonLocalPlayer";
                    RecordAttempt(true, "skipped", "nonLocalPlayer", plan, false, true);
                    return false;
                }

                var uiContext = TerrariaInputCompat.ReadUiInputContext(player);
                if (uiContext.MainTypeUnavailable || uiContext.GameMenu)
                {
                    plan.SkipReason = uiContext.MainTypeUnavailable ? "worldUnavailable" : "gameMenu";
                    RecordAttempt(true, "skipped", plan.SkipReason, plan, false, true);
                    return false;
                }

                bool playerActive;
                bool playerDead;
                bool playerGhost;
                if (PlayerBuffCompat.TryReadPlayerAvailability(player, out playerActive, out playerDead, out playerGhost) &&
                    (!playerActive || playerDead || playerGhost))
                {
                    plan.SkipReason = !playerActive ? "playerInactive" : playerDead ? "playerDead" : "playerGhost";
                    RecordAttempt(true, "skipped", plan.SkipReason, plan, false, true);
                    return false;
                }

                if (LegacyTextInput.IsAnyFocused)
                {
                    plan.SkipReason = "textInputFocus:legacyUi";
                    RecordAttempt(true, "skipped", plan.SkipReason, plan, false, false);
                    return false;
                }

                bool textInputFocused;
                string textInputReason;
                TerrariaInputCompat.TryReadTextInputFocus(out textInputFocused, out textInputReason);
                if (textInputFocused)
                {
                    plan.SkipReason = "textInputFocus:" + textInputReason;
                    RecordAttempt(true, "skipped", plan.SkipReason, plan, false, false);
                    return false;
                }

                if (!TerrariaTeleportRodCompat.TryBuildCorrectionPlan(player, item, out plan) || plan == null)
                {
                    if (plan == null)
                    {
                        plan = new TeleportRodCorrectionPlan { SkipReason = "planUnavailable" };
                    }

                    RecordAttempt(true, "skipped", NormalizeReason(plan.SkipReason, "noCandidate"), plan, false, true);
                    return false;
                }

                if (plan.OriginalSafe)
                {
                    RecordAttempt(true, "skipped", "originalSafe", plan, false, false);
                    return false;
                }

                if (!plan.HasCorrection)
                {
                    RecordAttempt(true, "skipped", NormalizeReason(plan.SkipReason, "noCandidate"), plan, false, true);
                    return false;
                }

                if (!TerrariaTeleportRodCompat.TryApplyCorrectedMouseTarget(player, plan, out restoreState))
                {
                    var reason = plan.MouseCaptureSucceeded ? "applyMouseFailed" : "mouseCaptureFailed";
                    plan.SkipReason = reason;
                    RecordAttempt(true, "failed", reason, plan, false, true);
                    return false;
                }

                RecordAttempt(true, "appliedBeforeVanilla", string.Empty, plan, true, false);
                return true;
            }
            catch (Exception error)
            {
                plan.SkipReason = "exception:" + error.GetType().Name;
                plan.CompatError = error.Message;
                RecordAttempt(true, "failed", plan.SkipReason, plan, false, true);
                RuntimeDiagnostics.RecordError("MovementTeleportCorrectionService.TryApplyBeforeVanilla", error);
                LogThrottle.ErrorThrottled(
                    "movement-teleport-correction-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "MovementTeleportCorrectionService",
                    "Teleport correction prefix failed; exception swallowed.", error);
                return false;
            }
        }

        public static void RestoreAfterVanilla(MouseTargetInputState restoreState, TeleportRodCorrectionPlan plan)
        {
            if (plan == null || !plan.HasCorrection)
            {
                return;
            }

            try
            {
                var restored = TerrariaTeleportRodCompat.TryRestoreMouseTarget(restoreState, plan);
                RecordAttempt(
                    true,
                    restored ? "applied" : "attemptedButUnverified",
                    restored ? string.Empty : "restoreFailed",
                    plan,
                    true,
                    true);
            }
            catch (Exception error)
            {
                plan.MouseRestoreSucceeded = false;
                plan.CompatError = error.Message;
                RecordAttempt(true, "failed", "restoreFailed", plan, true, true);
                RuntimeDiagnostics.RecordError("MovementTeleportCorrectionService.RestoreAfterVanilla", error);
                LogThrottle.ErrorThrottled(
                    "movement-teleport-correction-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "MovementTeleportCorrectionService",
                    "Teleport correction mouse restore failed; exception swallowed.", error);
            }
        }

        private static void RecordAttempt(
            bool enabled,
            string decision,
            string reason,
            TeleportRodCorrectionPlan plan,
            bool applied,
            bool forceEvent)
        {
            if (plan == null)
            {
                plan = new TeleportRodCorrectionPlan();
            }

            var normalizedDecision = string.IsNullOrWhiteSpace(decision) ? "skipped" : decision;
            var normalizedReason = reason ?? string.Empty;

            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new MovementTeleportCorrectionDiagnosticInfo() : _diagnostics.Clone();
                current.Enabled = enabled;
                current.HookInstalled = HookDiagnostics.TeleportRodHookInstalled;
                current.HookMethod = HookDiagnostics.TeleportRodHookMethod;
                current.HookMessage = HookDiagnostics.TeleportRodHookMessage;
                current.LastDecision = normalizedDecision;
                current.LastSkipReason = normalizedReason;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.ItemType = plan.ItemType;
                current.ItemName = plan.ItemName ?? string.Empty;
                current.OriginalMouseWorldX = plan.OriginalMouseWorldX;
                current.OriginalMouseWorldY = plan.OriginalMouseWorldY;
                current.OriginalMouseScreenX = plan.OriginalMouseScreenX;
                current.OriginalMouseScreenY = plan.OriginalMouseScreenY;
                current.OriginalTopLeftX = plan.OriginalTopLeftX;
                current.OriginalTopLeftY = plan.OriginalTopLeftY;
                current.OriginalSafe = plan.OriginalSafe;
                current.SearchRadiusPixels = plan.SearchRadiusPixels;
                current.SearchStepPixels = plan.SearchStepPixels;
                current.CandidateCount = plan.CandidateCount;
                current.ValidCandidateCount = plan.ValidCandidateCount;
                current.NearestCandidateDistance = plan.NearestCandidateDistance;
                current.CorrectedTopLeftX = plan.CorrectedTopLeftX;
                current.CorrectedTopLeftY = plan.CorrectedTopLeftY;
                current.CorrectedMouseWorldX = plan.CorrectedMouseWorldX;
                current.CorrectedMouseWorldY = plan.CorrectedMouseWorldY;
                current.CorrectedMouseScreenX = plan.CorrectedMouseScreenX;
                current.CorrectedMouseScreenY = plan.CorrectedMouseScreenY;
                current.MouseCaptureSucceeded = plan.MouseCaptureSucceeded;
                current.MouseApplySucceeded = plan.MouseApplySucceeded;
                current.MouseRestoreSucceeded = plan.MouseRestoreSucceeded;
                current.VanillaContinued = true;
                current.LastCompatError = FirstNonEmpty(plan.CompatError, TerrariaTeleportRodCompat.LastError, TerrariaInputCompat.LastInputCompatError);
                if (applied && string.Equals(normalizedDecision, "applied", StringComparison.OrdinalIgnoreCase))
                {
                    current.AppliedCount++;
                }
                else if (!applied || !string.Equals(normalizedDecision, "appliedBeforeVanilla", StringComparison.OrdinalIgnoreCase))
                {
                    current.SkippedCount++;
                }

                _diagnostics = current;
            }

            if (forceEvent || ShouldRecordEvent(normalizedDecision, normalizedReason, applied))
            {
                RecordEvent(enabled, normalizedDecision, normalizedReason, plan);
            }
        }

        private static bool ShouldRecordEvent(string decision, string reason, bool applied)
        {
            if (applied ||
                string.Equals(decision, "failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(decision, "attemptedButUnverified", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var key = string.IsNullOrWhiteSpace(reason) ? decision : reason;
            var interval = string.Equals(reason, "originalSafe", StringComparison.OrdinalIgnoreCase)
                ? TimeSpan.FromSeconds(2)
                : TimeSpan.FromSeconds(5);

            lock (EventThrottleSyncRoot)
            {
                DateTime last;
                var now = DateTime.UtcNow;
                if (LastEventByReason.TryGetValue(key, out last) && now - last < interval)
                {
                    return false;
                }

                LastEventByReason[key] = now;
                return true;
            }
        }

        private static void RecordEvent(bool enabled, string decision, string reason, TeleportRodCorrectionPlan plan)
        {
            var resultCode = MapResultCode(decision, reason);
            var message = BuildMessage(decision, reason, plan);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Movement.TeleportCorrection",
                InputActionKind.TeleportCorrection.ToString(),
                string.Empty,
                decision,
                resultCode,
                message,
                0,
                BuildBeforeJson(enabled, plan),
                BuildAfterJson(plan),
                BuildVerificationJson(enabled, decision, reason, plan),
                "Hook",
                "Player.ItemCheck_UseTeleportRod",
                string.Empty,
                string.Empty);
        }

        private static string BuildMessage(string decision, string reason, TeleportRodCorrectionPlan plan)
        {
            if (string.Equals(decision, "applied", StringComparison.OrdinalIgnoreCase))
            {
                return "Teleport correction adjusted the rod mouse target and restored mouse state after vanilla execution.";
            }

            if (string.Equals(decision, "appliedBeforeVanilla", StringComparison.OrdinalIgnoreCase))
            {
                return "Teleport correction adjusted the rod mouse target; vanilla method will continue.";
            }

            if (string.Equals(reason, "originalSafe", StringComparison.OrdinalIgnoreCase))
            {
                return "Teleport correction skipped because the original rod target is already safe.";
            }

            if (string.Equals(reason, "noCandidate", StringComparison.OrdinalIgnoreCase))
            {
                return "Teleport correction found no reliable safe candidate; vanilla method will continue unchanged.";
            }

            if (!string.IsNullOrWhiteSpace(plan == null ? string.Empty : plan.CompatError))
            {
                return "Teleport correction skipped: " + reason + " / " + plan.CompatError;
            }

            return "Teleport correction skipped: " + (string.IsNullOrWhiteSpace(reason) ? decision : reason) + ".";
        }

        private static string MapResultCode(string decision, string reason)
        {
            if (string.Equals(decision, "applied", StringComparison.OrdinalIgnoreCase))
            {
                return DiagnosticResultCode.Succeeded.ToString();
            }

            if (string.Equals(decision, "appliedBeforeVanilla", StringComparison.OrdinalIgnoreCase))
            {
                return DiagnosticResultCode.AttemptedButUnverified.ToString();
            }

            if (string.Equals(decision, "attemptedButUnverified", StringComparison.OrdinalIgnoreCase))
            {
                return DiagnosticResultCode.AttemptedButUnverified.ToString();
            }

            if (string.Equals(decision, "failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reason, "restoreFailed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reason, "applyMouseFailed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reason, "mouseCaptureFailed", StringComparison.OrdinalIgnoreCase))
            {
                return DiagnosticResultCode.Failed.ToString();
            }

            return DiagnosticResultCode.NotApplicable.ToString();
        }

        private static string BuildBeforeJson(bool enabled, TeleportRodCorrectionPlan plan)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "enabled", BoolRaw(enabled), true);
            AppendRaw(builder, "itemType", IntRaw(plan.ItemType), true);
            AppendString(builder, "itemName", plan.ItemName, true);
            AppendRaw(builder, "originalMouseWorld", BuildPointJson(plan.OriginalMouseWorldX, plan.OriginalMouseWorldY), true);
            AppendRaw(builder, "originalMouseScreen", BuildIntPointJson(plan.OriginalMouseScreenX, plan.OriginalMouseScreenY), true);
            AppendRaw(builder, "rawTopLeft", BuildPointJson(plan.RawTopLeftX, plan.RawTopLeftY), true);
            AppendRaw(builder, "originalTopLeft", BuildPointJson(plan.OriginalTopLeftX, plan.OriginalTopLeftY), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildAfterJson(TeleportRodCorrectionPlan plan)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "hasCorrection", BoolRaw(plan.HasCorrection), true);
            AppendRaw(builder, "correctedTopLeft", BuildPointJson(plan.CorrectedTopLeftX, plan.CorrectedTopLeftY), true);
            AppendRaw(builder, "correctedMouseWorld", BuildPointJson(plan.CorrectedMouseWorldX, plan.CorrectedMouseWorldY), true);
            AppendRaw(builder, "correctedMouseScreen", BuildIntPointJson(plan.CorrectedMouseScreenX, plan.CorrectedMouseScreenY), true);
            AppendRaw(builder, "mouseCaptureSucceeded", BoolRaw(plan.MouseCaptureSucceeded), true);
            AppendRaw(builder, "mouseApplySucceeded", BoolRaw(plan.MouseApplySucceeded), true);
            AppendRaw(builder, "mouseRestoreSucceeded", BoolRaw(plan.MouseRestoreSucceeded), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildVerificationJson(bool enabled, string decision, string reason, TeleportRodCorrectionPlan plan)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "hookInstalled", BoolRaw(HookDiagnostics.TeleportRodHookInstalled), true);
            AppendString(builder, "hookMethod", HookDiagnostics.TeleportRodHookMethod, true);
            AppendRaw(builder, "enabled", BoolRaw(enabled), true);
            AppendRaw(builder, "vanillaContinued", "true", true);
            AppendString(builder, "decision", decision, true);
            AppendString(builder, "skipReason", reason, true);
            AppendRaw(builder, "itemType", IntRaw(plan.ItemType), true);
            AppendString(builder, "itemName", plan.ItemName, true);
            AppendRaw(builder, "originalSafe", BoolRaw(plan.OriginalSafe), true);
            AppendString(builder, "originalUnsafeReason", plan.OriginalUnsafeReason, true);
            AppendRaw(builder, "searchRadiusPixels", IntRaw(plan.SearchRadiusPixels), true);
            AppendRaw(builder, "searchRadiusTiles", IntRaw(plan.SearchRadiusPixels / 16), true);
            AppendRaw(builder, "searchStepPixels", IntRaw(plan.SearchStepPixels), true);
            AppendRaw(builder, "candidateCount", IntRaw(plan.CandidateCount), true);
            AppendRaw(builder, "validCandidateCount", IntRaw(plan.ValidCandidateCount), true);
            AppendRaw(builder, "nearestCandidateDistance", FloatRaw(plan.NearestCandidateDistance), true);
            AppendRaw(builder, "mouseCaptureSucceeded", BoolRaw(plan.MouseCaptureSucceeded), true);
            AppendRaw(builder, "mouseApplySucceeded", BoolRaw(plan.MouseApplySucceeded), true);
            AppendRaw(builder, "mouseRestoreSucceeded", BoolRaw(plan.MouseRestoreSucceeded), true);
            AppendString(builder, "compatError", FirstNonEmpty(plan.CompatError, TerrariaTeleportRodCompat.LastError, TerrariaInputCompat.LastInputCompatError), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string NormalizeReason(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index];
                }
            }

            return string.Empty;
        }

        private static string BuildPointJson(float x, float y)
        {
            return "{\"x\":" + FloatRaw(x) + ",\"y\":" + FloatRaw(y) + "}";
        }

        private static string BuildIntPointJson(int x, int y)
        {
            return "{\"x\":" + IntRaw(x) + ",\"y\":" + IntRaw(y) + "}";
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
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":\"").Append(Escape(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
