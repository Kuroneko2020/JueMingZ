using System;
using System.Globalization;
using System.Text;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Actions.Executors
{
    public sealed class DashActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind
        {
            get { return InputActionKind.Dash; }
        }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var scenario = GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.MovementContinuousDash);
            var mode = MovementContinuousDashModes.Normalize(GetMetadataString(execution, "ContinuousDashMode", MovementContinuousDashModes.HoldDirection));
            var requestedDirection = NormalizeDirection(GetMetadataInt(execution, "DashDirection", 0));
            DashInputProfile before = null;
            DashInputProfile after = null;

            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Continuous dash blocked: not in a playable world.", scenario, mode, requestedDirection, before, after, false, false, "worldBlocked");
            }

            if (LegacyTextInput.IsAnyFocused)
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Continuous dash blocked by Legacy UI text input focus.", scenario, mode, requestedDirection, before, after, false, false, "textInput:legacyUi");
            }

            bool textFocused;
            string textReason;
            TerrariaInputCompat.TryReadTextInputFocus(out textFocused, out textReason);
            if (textFocused)
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Continuous dash blocked by text input focus: " + textReason + ".", scenario, mode, requestedDirection, before, after, false, false, "textInput:" + textReason);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Continuous dash failed: local player unavailable.", scenario, mode, requestedDirection, before, after, false, false, "localPlayerUnavailable");
            }

            if (!TerrariaDashCompat.TryReadDashInputProfile(player, out before))
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Continuous dash failed: " + TerrariaDashCompat.LastDashCompatError, scenario, mode, requestedDirection, before, after, false, false, "profileUnavailable");
            }

            if (requestedDirection == 0 || before.HeldDirection != requestedDirection)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Continuous dash skipped: requested direction is no longer held.", scenario, mode, requestedDirection, before, after, false, false, "directionChanged");
            }

            if (!before.PlayerControllable)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Continuous dash skipped: player is not controllable.", scenario, mode, requestedDirection, before, after, false, false, "playerNotControllable");
            }

            if (!before.HasDashAbility)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.MissingRequiredItem, "Continuous dash skipped: no vanilla dash ability is available.", scenario, mode, requestedDirection, before, after, false, false, "noDashAbility");
            }

            if (!before.DashCooldownReady)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.BlockedByCooldown, "Continuous dash skipped: vanilla dash cooldown is not ready.", scenario, mode, requestedDirection, before, after, false, false, "dashCooldown");
            }

            long gameTick;
            TerrariaInputCompat.TryReadGameUpdateCount(out gameTick);
            if (TerrariaDashCompat.DashMovementHookInstalled)
            {
                string message;
                var queued = TerrariaDashCompat.TryRequestContinuousDashPulse(execution.Request.RequestId, requestedDirection, mode, gameTick, out message);
                TerrariaDashCompat.TryReadDashInputProfile(player, out after);
                return Finish(
                    execution,
                    startedUtc,
                    queued ? InputActionStatus.Succeeded : InputActionStatus.Failed,
                    queued ? DiagnosticResultCode.Queued : DiagnosticResultCode.Failed,
                    queued ? "Continuous dash pulse queued for DashMovement hook." : message,
                    scenario,
                    mode,
                    requestedDirection,
                    before,
                    after,
                    queued,
                    false,
                    queued ? "dashPulseQueued" : "queuePulseFailed");
            }

            DashPulseApplyResult fallback;
            var applied = TerrariaDashCompat.TryApplyImmediateContinuousDashPulse(player, execution.Request.RequestId, requestedDirection, mode, out fallback);
            after = fallback == null ? null : fallback.AfterProfile;
            return Finish(
                execution,
                startedUtc,
                applied ? InputActionStatus.AttemptedButUnverified : InputActionStatus.Failed,
                applied ? DiagnosticResultCode.AttemptedButUnverified : DiagnosticResultCode.Failed,
                applied
                    ? "Continuous dash pulse applied through Runtime tick fallback; timing must be verified in game."
                    : fallback == null ? "Continuous dash fallback failed." : fallback.Message,
                scenario,
                mode,
                requestedDirection,
                before,
                after,
                applied,
                true,
                applied ? "runtimeFallbackPulseApplied" : "runtimeFallbackPulseFailed");
        }

        private static InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            string scenario,
            string mode,
            int requestedDirection,
            DashInputProfile before,
            DashInputProfile after,
            bool pulseArmedOrApplied,
            bool runtimeFallback,
            string verificationReason)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);

            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                scenario,
                InputActionKind.Dash.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message,
                (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds,
                BuildDashStateJson(before),
                BuildDashStateJson(after),
                BuildVerificationJson(mode, requestedDirection, pulseArmedOrApplied, runtimeFallback, verificationReason),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message);
        }

        private static string BuildDashStateJson(DashInputProfile profile)
        {
            if (profile == null)
            {
                return "{}";
            }

            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "playerControllable", BoolRaw(profile.PlayerControllable), true);
            AppendRaw(builder, "controlLeft", BoolRaw(profile.ControlLeft), true);
            AppendRaw(builder, "controlRight", BoolRaw(profile.ControlRight), true);
            AppendRaw(builder, "heldDirection", IntRaw(profile.HeldDirection), true);
            AppendRaw(builder, "controlDash", BoolRaw(profile.ControlDash), true);
            AppendRaw(builder, "releaseDash", BoolRaw(profile.ReleaseDash), true);
            AppendRaw(builder, "dashDelay", IntRaw(profile.DashDelay), true);
            AppendRaw(builder, "dashCooldownReady", BoolRaw(profile.DashCooldownReady), true);
            AppendRaw(builder, "dashType", IntRaw(profile.DashType), true);
            AppendRaw(builder, "fallbackDashType", IntRaw(profile.FallbackDashType), true);
            AppendRaw(builder, "hasDashAbility", BoolRaw(profile.HasDashAbility), true);
            AppendString(builder, "dashAbilitySource", profile.DashAbilitySource, true);
            AppendRaw(builder, "mountActive", BoolRaw(profile.MountActive), true);
            AppendRaw(builder, "mountType", IntRaw(profile.MountType), true);
            AppendRaw(builder, "mountCanDashKnown", BoolRaw(profile.MountCanDashKnown), true);
            AppendRaw(builder, "mountCanDash", BoolRaw(profile.MountCanDash), true);
            AppendString(builder, "capabilitySummary", profile.CapabilitySummary, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildVerificationJson(string mode, int requestedDirection, bool pulseArmedOrApplied, bool runtimeFallback, string verificationReason)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "mode", MovementContinuousDashModes.Normalize(mode), true);
            AppendRaw(builder, "requestedDirection", IntRaw(requestedDirection), true);
            AppendRaw(builder, "pulseArmedOrApplied", BoolRaw(pulseArmedOrApplied), true);
            AppendRaw(builder, "dashMovementHookInstalled", BoolRaw(TerrariaDashCompat.DashMovementHookInstalled), true);
            AppendRaw(builder, "runtimeFallback", BoolRaw(runtimeFallback), true);
            AppendRaw(builder, "directVelocityMutation", "false", true);
            AppendRaw(builder, "directPositionMutation", "false", true);
            AppendString(builder, "verificationReason", verificationReason, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static int NormalizeDirection(int direction)
        {
            if (direction > 0)
            {
                return 1;
            }

            return direction < 0 ? -1 : 0;
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":\"").Append(EscapeJson(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":").Append(value ?? string.Empty);
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

        private static string EscapeJson(string value)
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
