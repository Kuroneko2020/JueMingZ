using System;
using System.Globalization;
using System.Text;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class JumpActionExecutor : InputActionExecutorBase
    {
        private const string ModeSafeLandingTakeover = "SafeLandingTakeover";
        private const string ModeSimulatedMultiJumpPulse = "SimulatedMultiJumpPulse";
        private const string SafeLandingStagePress = "Press";
        private const string SafeLandingStageHold = "Hold";
        private const string SafeLandingStageRelease = "Release";
        private const int SafeLandingDefaultPlayerUpdateHoldTicks = 4;
        private const int SafeLandingDoubleJumpHoldTicks = 2;
        private const int SafeLandingGrappleHoldTicks = 1;
        private const int SafeLandingGrappleMaxHoldTicks = 3;
        private const int SafeLandingRocketBootsHoldTicks = 4;
        private const int SafeLandingFlyingCarpetHoldTicks = 4;
        private const int SafeLandingFlyingMountHoldTicks = 1;
        private const int SafeLandingGravityFlipHoldTicks = 1;

        public override InputActionKind Kind
        {
            get { return InputActionKind.Jump; }
        }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (string.Equals(GetMetadataString(execution, "JumpMode", string.Empty), ModeSafeLandingTakeover, StringComparison.OrdinalIgnoreCase))
            {
                return StartSafeLandingTakeover(execution, snapshot);
            }

            var startedUtc = DateTime.UtcNow;
            var scenario = GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.MovementSimulatedMultiJump);
            var triggerReason = GetMetadataString(execution, "TriggerReason", string.Empty);
            JumpInputProfile before = null;
            JumpInputProfile after = null;

            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Simulated jump blocked: not in a playable world.", scenario, triggerReason, before, after, false, false, "worldBlocked");
            }

            bool textFocused;
            string textReason;
            TerrariaInputCompat.TryReadTextInputFocus(out textFocused, out textReason);
            if (textFocused)
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Simulated jump blocked by text input focus: " + textReason + ".", scenario, triggerReason, before, after, false, false, "textInput:" + textReason);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Simulated jump failed: local player unavailable.", scenario, triggerReason, before, after, false, false, "localPlayerUnavailable");
            }

            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out before))
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Simulated jump failed: " + TerrariaInputCompat.LastInputCompatError, scenario, triggerReason, before, after, false, false, "profileUnavailable");
            }

            SetState(execution, "SimulatedJumpBeforeJson", BuildJumpStateJson(before));
            SetState(execution, "SimulatedJumpTriggerReason", triggerReason);

            if (!before.PlayerControllable)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Simulated jump skipped: player is not controllable.", scenario, triggerReason, before, after, false, false, "playerNotControllable");
            }

            if (!before.ControlJump)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Simulated jump skipped: jump key is not held.", scenario, triggerReason, before, after, false, false, "jumpNotHeld");
            }

            if (before.ControlDown)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Simulated jump skipped: preserving down+jump platform passthrough.", scenario, triggerReason, before, after, false, false, "downJumpPassthrough");
            }

            if (!before.HasAvailableJumpOpportunity)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Simulated jump skipped: no vanilla jump opportunity is available.", scenario, triggerReason, before, after, false, false, "noAvailableJumpOpportunity");
            }

            var applyRocketRelease = before.HasRocketJump && !before.HasAirJump && !before.HasWingFlight;
            SetState(execution, "JumpMode", ModeSimulatedMultiJumpPulse);
            SetState(execution, "SimulatedJumpApplyRocketRelease", applyRocketRelease ? "true" : "false");
            string queueMessage;
            var queued = MovementSimulatedJumpPulseCompat.QueueSimulatedJumpPulse(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                triggerReason,
                applyRocketRelease,
                out queueMessage);
            TerrariaInputCompat.TryReadJumpInputProfile(player, out after);

            if (!queued)
            {
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.Failed,
                    DiagnosticResultCode.Failed,
                    queueMessage,
                    scenario,
                    triggerReason,
                    before,
                    after,
                    false,
                    applyRocketRelease,
                    "queuePulseFailed");
            }

            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("Simulated jump release/press pulse queued for Player.Update.");
        }

        public override InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (string.Equals(GetState(execution, "JumpMode", string.Empty), ModeSafeLandingTakeover, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateSafeLandingTakeover(execution, snapshot);
            }

            if (string.Equals(GetState(execution, "JumpMode", string.Empty), ModeSimulatedMultiJumpPulse, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateSimulatedMultiJumpPulse(execution, snapshot);
            }

            return InputActionExecutionStepResult.Complete(InputActionStatus.NotImplemented, "Jump action update is not implemented for this mode.");
        }

        public override InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
        {
            if (string.Equals(GetState(execution, "JumpMode", string.Empty), ModeSafeLandingTakeover, StringComparison.OrdinalIgnoreCase))
            {
                MovementSafeLandingCompat.CancelSafeLandingJumpPulse(
                    execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                    reason);
                object player;
                if (TerrariaInputCompat.TryGetLocalPlayer(out player) && player != null)
                {
                    string releaseMessage;
                    TerrariaInputCompat.TryReleaseSafeLandingControlInputs(player, out releaseMessage);
                }
            }

            if (string.Equals(GetState(execution, "JumpMode", string.Empty), ModeSimulatedMultiJumpPulse, StringComparison.OrdinalIgnoreCase))
            {
                MovementSimulatedJumpPulseCompat.CancelSimulatedJumpPulse(
                    execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                    reason);
            }

            return base.Cancel(execution, reason);
        }

        private static InputActionExecutionStepResult UpdateSimulatedMultiJumpPulse(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                MovementSimulatedJumpPulseCompat.CancelSimulatedJumpPulse(
                    execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                    "worldBlocked");
                return CompleteSimulatedJumpNow(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Simulated jump stopped: not in a playable world.", "worldBlocked");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                MovementSimulatedJumpPulseCompat.CancelSimulatedJumpPulse(
                    execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                    "localPlayerUnavailable");
                return CompleteSimulatedJumpNow(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Simulated jump stopped: local player unavailable.", "localPlayerUnavailable");
            }

            SimulatedJumpPulseSnapshot pulse;
            if (!MovementSimulatedJumpPulseCompat.TryGetSimulatedJumpPulseSnapshot(
                    execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                    out pulse) ||
                pulse == null)
            {
                return CompleteSimulatedJumpNow(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Simulated jump pulse was lost before Player.Update consumed it.", "pulseMissing");
            }

            CopySimulatedPulseSnapshotToState(execution, pulse);
            if (pulse.Failed)
            {
                return CompleteSimulatedJumpNow(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, pulse.LastMessage, "pulseFailed:" + pulse.LastApplySite);
            }

            if (!pulse.Completed)
            {
                return InputActionExecutionStepResult.Running("Simulated jump pulse waiting for Player.Update consumption: " + pulse.Phase + ".");
            }

            JumpInputProfile after = null;
            TerrariaInputCompat.TryReadJumpInputProfile(player, out after);

            return FinishWithPulse(
                execution,
                execution == null ? DateTime.UtcNow : execution.StartedUtc,
                InputActionStatus.Succeeded,
                DiagnosticResultCode.Succeeded,
                "Simulated jump release/press pulse applied before Player.Update.",
                GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.MovementSimulatedMultiJump),
                GetState(execution, "SimulatedJumpTriggerReason", pulse.TriggerReason),
                null,
                after,
                pulse.ReleaseApplied,
                pulse.PressApplied,
                pulse.ApplyRocketRelease,
                pulse.Status,
                pulse.Phase,
                pulse.LastApplySite,
                "playerUpdatePulseApplied:" + pulse.LastApplySite);
        }

        private static InputActionExecutionStepResult CompleteSimulatedJumpNow(
            InputActionExecution execution,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            string verificationReason)
        {
            JumpInputProfile after = null;
            object player;
            if (TerrariaInputCompat.TryGetLocalPlayer(out player) && player != null)
            {
                TerrariaInputCompat.TryReadJumpInputProfile(player, out after);
            }

            return FinishWithPulse(
                execution,
                execution == null ? DateTime.UtcNow : execution.StartedUtc,
                status,
                code,
                message,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.MovementSimulatedMultiJump),
                GetState(execution, "SimulatedJumpTriggerReason", GetMetadataString(execution, "TriggerReason", string.Empty)),
                null,
                after,
                GetStateBool(execution, "SimulatedJumpReleaseApplied", false),
                GetStateBool(execution, "SimulatedJumpPressApplied", false),
                GetStateBool(execution, "SimulatedJumpApplyRocketRelease", false),
                GetState(execution, "SimulatedJumpPulseStatus", string.Empty),
                GetState(execution, "SimulatedJumpPulsePhase", string.Empty),
                GetState(execution, "SimulatedJumpPulseApplySite", string.Empty),
                verificationReason);
        }

        private static InputActionExecutionStepResult StartSafeLandingTakeover(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var scenario = GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.MovementSafeLanding);
            var strategy = GetMetadataString(execution, "SafeLandingStrategy", string.Empty);
            var actionType = GetMetadataString(execution, "SafeLandingActionType", "jump");
            JumpInputProfile before = null;
            JumpInputProfile after = null;

            SetState(execution, "JumpMode", ModeSafeLandingTakeover);
            SetState(execution, "SafeLandingStrategy", strategy);
            SetState(execution, "SafeLandingActionType", actionType);
            SetState(execution, "SafeLandingPriority", GetMetadataString(execution, "SafeLandingPriority", string.Empty));
            SetState(execution, "SafeLandingImpactTicks", GetMetadataString(execution, "SafeLandingImpactTicks", string.Empty));
            SetState(execution, "SafeLandingImpactDistancePixels", GetMetadataString(execution, "SafeLandingImpactDistancePixels", string.Empty));
            SetState(execution, "SafeLandingImpactWorldX", GetMetadataString(execution, "SafeLandingImpactWorldX", string.Empty));
            SetState(execution, "SafeLandingImpactWorldY", GetMetadataString(execution, "SafeLandingImpactWorldY", string.Empty));
            SetState(execution, "SafeLandingFallingSpeed", GetMetadataString(execution, "SafeLandingFallingSpeed", string.Empty));
            SetState(execution, "SafeLandingCapabilitySummary", GetMetadataString(execution, "SafeLandingCapabilitySummary", string.Empty));
            SetState(execution, "SafeLandingGrappleTargetWorldX", GetMetadataString(execution, "SafeLandingGrappleTargetWorldX", string.Empty));
            SetState(execution, "SafeLandingGrappleTargetWorldY", GetMetadataString(execution, "SafeLandingGrappleTargetWorldY", string.Empty));
            SetState(execution, "SafeLandingSuppressDown", GetMetadataString(execution, "SafeLandingSuppressDown", "false"));
            SetState(execution, "SafeLandingGravityOriginalDirection", GetMetadataString(execution, "SafeLandingGravityOriginalDirection", string.Empty));
            SetState(execution, "SafeLandingStage", SafeLandingStagePress);
            SetState(execution, "SafeLandingHoldTicks", 0);
            var impactTicks = GetMetadataFloat(execution, "SafeLandingImpactTicks", 0f);
            var holdTargetTicks = ResolveSafeLandingHoldTicks(strategy, actionType, impactTicks);
            SetState(execution, "SafeLandingHoldTargetTicks", holdTargetTicks);
            SetState(execution, "SafeLandingReleaseApplied", "false");
            SetState(execution, "SafeLandingPressApplied", "false");
            SetState(execution, "SafeLandingFinalReleaseApplied", "false");

            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                return FinishSafeLanding(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Safe landing jump blocked: not in a playable world.", before, after, "worldBlocked");
            }

            bool textFocused;
            string textReason;
            TerrariaInputCompat.TryReadTextInputFocus(out textFocused, out textReason);
            if (textFocused)
            {
                return FinishSafeLanding(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Safe landing jump blocked by text input focus: " + textReason + ".", before, after, "textInput:" + textReason);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return FinishSafeLanding(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Safe landing jump failed: local player unavailable.", before, after, "localPlayerUnavailable");
            }

            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out before))
            {
                return FinishSafeLanding(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Safe landing jump failed: " + TerrariaInputCompat.LastInputCompatError, before, after, "profileUnavailable");
            }

            if (!before.PlayerControllable)
            {
                return FinishSafeLanding(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Safe landing jump skipped: player is not controllable.", before, after, "playerNotControllable");
            }

            if (SafeLandingRequiresAerialJumpWindow(actionType, strategy) && !before.AerialJumpWindow)
            {
                return FinishSafeLanding(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Safe landing action skipped: no aerial jump window is available.", before, after, "noAerialJumpWindow");
            }

            if (!SafeLandingStrategyStillAvailable(actionType, strategy, before))
            {
                return FinishSafeLanding(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Safe landing jump skipped: selected active ability is no longer available.", before, after, "abilityUnavailable");
            }

            var applyRocketRelease = string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase) &&
                                     (string.Equals(strategy, "equipped_rocket_boots", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(strategy, "temporary_rocket_boots", StringComparison.OrdinalIgnoreCase));
            var startWithPress = ShouldStartSafeLandingWithPress(actionType, strategy, before);
            var immediateMountCancel = ShouldImmediateCancelSafeLandingMount(strategy, actionType);
            var grappleTargetX = 0f;
            var grappleTargetY = 0f;
            var hasGrappleTarget = string.Equals(actionType, "grapple", StringComparison.OrdinalIgnoreCase) &&
                                   TryGetMetadataFloat(execution, "SafeLandingGrappleTargetWorldX", out grappleTargetX) &&
                                   TryGetMetadataFloat(execution, "SafeLandingGrappleTargetWorldY", out grappleTargetY);
            SetState(execution, "SafeLandingApplyRocketRelease", applyRocketRelease ? "true" : "false");
            SetState(execution, "SafeLandingStartWithPress", startWithPress ? "true" : "false");
            SetState(execution, "SafeLandingImmediateMountCancel", immediateMountCancel ? "true" : "false");
            SetState(execution, "SafeLandingGrappleTargetKnown", hasGrappleTarget ? "true" : "false");
            string queueMessage;
            var queued = MovementSafeLandingCompat.QueueSafeLandingJumpPulse(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                strategy,
                actionType,
                applyRocketRelease,
                GetStateBool(execution, "SafeLandingSuppressDown", false),
                holdTargetTicks,
                startWithPress,
                immediateMountCancel,
                hasGrappleTarget,
                hasGrappleTarget ? grappleTargetX : 0f,
                hasGrappleTarget ? grappleTargetY : 0f,
                out queueMessage);
            TerrariaInputCompat.TryReadJumpInputProfile(player, out after);
            SetState(execution, "SafeLandingPulseStatus", queued ? "queued" : "failed");
            SetState(execution, "SafeLandingPulseMessage", queueMessage);
            if (!queued)
            {
                return FinishSafeLanding(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, queueMessage, before, after, "queuePulseFailed");
            }

            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("Safe landing " + actionType + " pulse queued for Player.Update.");
        }

        private static InputActionExecutionStepResult UpdateSafeLandingTakeover(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                return CompleteSafeLandingNow(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Safe landing jump stopped: not in a playable world.", "worldBlocked");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return CompleteSafeLandingNow(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Safe landing jump stopped: local player unavailable.", "localPlayerUnavailable");
            }

            SafeLandingJumpPulseSnapshot pulse;
            if (!MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(
                    execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                    out pulse) ||
                pulse == null)
            {
                return CompleteSafeLandingNow(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Safe landing jump pulse was lost before Player.Update consumed it.", "pulseMissing");
            }

            CopyPulseSnapshotToState(execution, pulse);
            if (pulse.Failed)
            {
                return CompleteSafeLandingNow(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, pulse.LastMessage, "pulseFailed:" + pulse.LastApplySite);
            }

            if (!pulse.Completed)
            {
                return InputActionExecutionStepResult.Running("Safe landing jump pulse waiting for Player.Update consumption: " + pulse.Phase + ".");
            }

            return CompleteSafeLandingNow(
                execution,
                InputActionStatus.AttemptedButUnverified,
                DiagnosticResultCode.AttemptedButUnverified,
                "Safe landing input was applied before Player.Update; rescue effect still needs in-game verification.",
                "playerUpdatePulseApplied:" + pulse.LastApplySite);
        }

        private static int ResolveSafeLandingHoldTicks(string strategy, string actionType, float impactTicks)
        {
            if (string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(actionType, "grapple", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveSafeLandingGrappleHoldTicks(impactTicks);
            }

            if (IsGravityFlipAction(actionType))
            {
                return SafeLandingGravityFlipHoldTicks;
            }

            if (string.Equals(strategy, "equipped_double_jump", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_double_jump", StringComparison.OrdinalIgnoreCase))
            {
                return SafeLandingDoubleJumpHoldTicks;
            }

            if (string.Equals(strategy, "equipped_rocket_boots", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_rocket_boots", StringComparison.OrdinalIgnoreCase))
            {
                return SafeLandingRocketBootsHoldTicks;
            }

            if (string.Equals(strategy, "active_flying_mount", StringComparison.OrdinalIgnoreCase))
            {
                return SafeLandingFlyingMountHoldTicks;
            }

            if (string.Equals(strategy, "equipped_flying_carpet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_flying_carpet", StringComparison.OrdinalIgnoreCase))
            {
                return SafeLandingFlyingCarpetHoldTicks;
            }

            return SafeLandingDefaultPlayerUpdateHoldTicks;
        }

        private static int ResolveSafeLandingGrappleHoldTicks(float impactTicks)
        {
            if (float.IsNaN(impactTicks) || float.IsInfinity(impactTicks) || impactTicks <= 0f)
            {
                return SafeLandingGrappleHoldTicks;
            }

            var impactBasedHold = (int)Math.Ceiling(impactTicks) + 1;
            return Math.Max(SafeLandingGrappleHoldTicks, Math.Min(SafeLandingGrappleMaxHoldTicks, impactBasedHold));
        }

        private static bool ShouldStartSafeLandingWithPress(string actionType, string strategy, JumpInputProfile profile)
        {
            if (string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(actionType, "grapple", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase) &&
                   profile != null &&
                   profile.PlayerControllable &&
                   !profile.ControlJump &&
                   profile.ReleaseJump;
        }

        private static bool ShouldImmediateCancelSafeLandingMount(string strategy, string actionType)
        {
            if (!string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(strategy, "active_flying_mount", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "equipped_flying_mount", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_flying_mount", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SafeLandingRequiresAerialJumpWindow(string actionType, string strategy)
        {
            return string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "equipped_double_jump", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "equipped_rocket_boots", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "active_flying_mount", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SafeLandingStrategyStillAvailable(string actionType, string strategy, JumpInputProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            if (IsTemporaryEquipmentActivationStrategy(strategy))
            {
                return TemporaryEquipmentStrategyStillAvailable(actionType, strategy, profile);
            }

            if (string.Equals(strategy, "safe_landing_mount_cancel", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                return profile.PlayerControllable && profile.MountActive;
            }

            if (string.Equals(strategy, "safe_landing_gravity_restore", StringComparison.OrdinalIgnoreCase) &&
                IsGravityFlipAction(actionType))
            {
                return profile.PlayerControllable && profile.HasGravityGlobe;
            }

            if (string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                return profile.HasEquippedFlyingMountOpportunity || profile.HasEquippedSafeMountOpportunity;
            }

            if (string.Equals(actionType, "grapple", StringComparison.OrdinalIgnoreCase))
            {
                return profile.PlayerControllable && profile.HasAnyGrapple;
            }

            if (IsGravityFlipAction(actionType))
            {
                return profile.HasGravityFlipOpportunity;
            }

            if (string.Equals(strategy, "equipped_double_jump", StringComparison.OrdinalIgnoreCase))
            {
                return profile.HasAirJump;
            }

            if (string.Equals(strategy, "equipped_rocket_boots", StringComparison.OrdinalIgnoreCase))
            {
                return MovementSafeLandingCompat.HasSafeLandingRocketBootsActivationOpportunity(profile);
            }

            if (string.Equals(strategy, "active_flying_mount", StringComparison.OrdinalIgnoreCase))
            {
                return profile.HasMountOpportunity;
            }

            if (string.Equals(strategy, "equipped_flying_carpet", StringComparison.OrdinalIgnoreCase))
            {
                return profile.HasFlyingCarpetAvailable;
            }

            return false;
        }

        private static bool TemporaryEquipmentStrategyStillAvailable(string actionType, string strategy, JumpInputProfile profile)
        {
            if (profile == null || !profile.PlayerControllable)
            {
                return false;
            }

            if (string.Equals(strategy, "temporary_double_jump", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase) &&
                       profile.AerialJumpWindow &&
                       (profile.HasAirJump || profile.AirJumpFlagCount > 0);
            }

            if (string.Equals(strategy, "temporary_rocket_boots", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase) &&
                       profile.AerialJumpWindow &&
                       (MovementSafeLandingCompat.HasSafeLandingRocketBootsActivationOpportunity(profile) ||
                        profile.HasRocketBootsAvailable ||
                        profile.RocketBoots > 0);
            }

            if (string.Equals(strategy, "temporary_flying_carpet", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase) &&
                       profile.AerialJumpWindow &&
                       (profile.HasFlyingCarpetAvailable ||
                        profile.HasFlyingCarpet ||
                        profile.FlyingCarpetCanStart ||
                        profile.FlyingCarpetTime > 0);
            }

            if (string.Equals(strategy, "temporary_flying_mount", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase) &&
                       profile.HasEquippedFlyingMountOpportunity;
            }

            if (string.Equals(strategy, "temporary_safe_mount", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase) &&
                       profile.HasEquippedSafeMountOpportunity;
            }

            if (string.Equals(strategy, "temporary_gravity_globe", StringComparison.OrdinalIgnoreCase))
            {
                return IsGravityFlipAction(actionType) &&
                       profile.AerialJumpWindow &&
                       (profile.HasGravityFlipOpportunity || profile.HasGravityGlobe);
            }

            return false;
        }

        private static bool IsGravityFlipAction(string actionType)
        {
            return string.Equals(actionType, "gravity_flip", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionType, "gravityFlip", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTemporaryEquipmentActivationStrategy(string strategy)
        {
            return string.Equals(strategy, "temporary_double_jump", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_rocket_boots", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_flying_carpet", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_flying_mount", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_safe_mount", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_gravity_globe", StringComparison.OrdinalIgnoreCase);
        }

        private static InputActionExecutionStepResult CompleteSafeLandingNow(
            InputActionExecution execution,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            string verificationReason)
        {
            JumpInputProfile after = null;
            object player;
            if (TerrariaInputCompat.TryGetLocalPlayer(out player) && player != null)
            {
                TerrariaInputCompat.TryReadJumpInputProfile(player, out after);
            }

            return FinishSafeLanding(execution, execution == null ? DateTime.UtcNow : execution.StartedUtc, status, code, message, null, after, verificationReason);
        }

        private static InputActionExecutionStepResult FinishSafeLanding(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            JumpInputProfile before,
            JumpInputProfile after,
            string verificationReason)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);

            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.MovementSafeLanding),
                InputActionKind.Jump.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds,
                BuildSafeLandingBeforeJson(execution, before),
                BuildJumpStateJson(after),
                BuildSafeLandingVerificationJson(execution, verificationReason),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private static string BuildSafeLandingBeforeJson(InputActionExecution execution, JumpInputProfile before)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "strategy", GetState(execution, "SafeLandingStrategy", GetMetadataString(execution, "SafeLandingStrategy", string.Empty)), true);
            AppendString(builder, "actionType", GetState(execution, "SafeLandingActionType", GetMetadataString(execution, "SafeLandingActionType", string.Empty)), true);
            AppendString(builder, "priority", GetState(execution, "SafeLandingPriority", GetMetadataString(execution, "SafeLandingPriority", string.Empty)), true);
            AppendString(builder, "impactTicks", GetState(execution, "SafeLandingImpactTicks", GetMetadataString(execution, "SafeLandingImpactTicks", string.Empty)), true);
            AppendString(builder, "impactDistancePixels", GetState(execution, "SafeLandingImpactDistancePixels", GetMetadataString(execution, "SafeLandingImpactDistancePixels", string.Empty)), true);
            AppendString(builder, "impactWorldX", GetState(execution, "SafeLandingImpactWorldX", GetMetadataString(execution, "SafeLandingImpactWorldX", string.Empty)), true);
            AppendString(builder, "impactWorldY", GetState(execution, "SafeLandingImpactWorldY", GetMetadataString(execution, "SafeLandingImpactWorldY", string.Empty)), true);
            AppendString(builder, "fallingSpeed", GetState(execution, "SafeLandingFallingSpeed", GetMetadataString(execution, "SafeLandingFallingSpeed", string.Empty)), true);
            AppendString(builder, "velocityX", GetState(execution, "SafeLandingVelocityX", GetMetadataString(execution, "SafeLandingVelocityX", string.Empty)), true);
            AppendString(builder, "capabilitySummary", GetState(execution, "SafeLandingCapabilitySummary", GetMetadataString(execution, "SafeLandingCapabilitySummary", string.Empty)), true);
            AppendString(builder, "grappleTargetWorldX", GetState(execution, "SafeLandingGrappleTargetWorldX", GetMetadataString(execution, "SafeLandingGrappleTargetWorldX", string.Empty)), true);
            AppendString(builder, "grappleTargetWorldY", GetState(execution, "SafeLandingGrappleTargetWorldY", GetMetadataString(execution, "SafeLandingGrappleTargetWorldY", string.Empty)), true);
            AppendString(builder, "grappleTargetSource", GetState(execution, "SafeLandingGrappleTargetSource", GetMetadataString(execution, "SafeLandingGrappleTargetSource", string.Empty)), true);
            AppendString(builder, "grappleTargetFromLandingSurface", GetState(execution, "SafeLandingGrappleTargetFromLandingSurface", GetMetadataString(execution, "SafeLandingGrappleTargetFromLandingSurface", string.Empty)), true);
            AppendString(builder, "landingSurfaceKnown", GetState(execution, "SafeLandingLandingSurfaceKnown", GetMetadataString(execution, "SafeLandingLandingSurfaceKnown", string.Empty)), true);
            AppendString(builder, "landingSurfaceKind", GetState(execution, "SafeLandingLandingSurfaceKind", GetMetadataString(execution, "SafeLandingLandingSurfaceKind", string.Empty)), true);
            AppendString(builder, "landingSlopeDirection", GetState(execution, "SafeLandingLandingSlopeDirection", GetMetadataString(execution, "SafeLandingLandingSlopeDirection", string.Empty)), true);
            AppendString(builder, "landingContactSample", GetState(execution, "SafeLandingLandingContactSample", GetMetadataString(execution, "SafeLandingLandingContactSample", string.Empty)), true);
            AppendString(builder, "landingMovingIntoSlope", GetState(execution, "SafeLandingLandingMovingIntoSlope", GetMetadataString(execution, "SafeLandingLandingMovingIntoSlope", string.Empty)), true);
            AppendString(builder, "landingMovingWithSlope", GetState(execution, "SafeLandingLandingMovingWithSlope", GetMetadataString(execution, "SafeLandingLandingMovingWithSlope", string.Empty)), true);
            AppendString(builder, "landingProjectedPlayerLeftX", GetState(execution, "SafeLandingLandingProjectedPlayerLeftX", GetMetadataString(execution, "SafeLandingLandingProjectedPlayerLeftX", string.Empty)), true);
            AppendString(builder, "landingProjectedPlayerRightX", GetState(execution, "SafeLandingLandingProjectedPlayerRightX", GetMetadataString(execution, "SafeLandingLandingProjectedPlayerRightX", string.Empty)), true);
            AppendString(builder, "gravityOriginalDirection", GetState(execution, "SafeLandingGravityOriginalDirection", GetMetadataString(execution, "SafeLandingGravityOriginalDirection", string.Empty)), true);
            AppendRaw(builder, "jumpProfile", BuildJumpStateJson(before), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildSafeLandingVerificationJson(InputActionExecution execution, string verificationReason)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "jumpMode", ModeSafeLandingTakeover, true);
            AppendString(builder, "strategy", GetState(execution, "SafeLandingStrategy", string.Empty), true);
            AppendString(builder, "actionType", GetState(execution, "SafeLandingActionType", string.Empty), true);
            AppendRaw(builder, "releaseApplied", BoolRaw(GetStateBool(execution, "SafeLandingReleaseApplied", false)), true);
            AppendRaw(builder, "pressApplied", BoolRaw(GetStateBool(execution, "SafeLandingPressApplied", false)), true);
            AppendRaw(builder, "holdTicks", IntRaw(GetStateInt(execution, "SafeLandingHoldTicks", 0)), true);
            AppendRaw(builder, "holdTargetTicks", IntRaw(GetStateInt(execution, "SafeLandingHoldTargetTicks", 0)), true);
            AppendRaw(builder, "finalReleaseApplied", BoolRaw(GetStateBool(execution, "SafeLandingFinalReleaseApplied", false)), true);
            AppendRaw(builder, "rocketReleaseRequested", BoolRaw(GetStateBool(execution, "SafeLandingApplyRocketRelease", false)), true);
            AppendRaw(builder, "startWithPress", BoolRaw(GetStateBool(execution, "SafeLandingStartWithPress", false)), true);
            AppendRaw(builder, "immediateMountCancel", BoolRaw(GetStateBool(execution, "SafeLandingImmediateMountCancel", false)), true);
            AppendRaw(builder, "mountCancelPressApplied", BoolRaw(GetStateBool(execution, "SafeLandingMountCancelPressApplied", false)), true);
            AppendRaw(builder, "mountCancelFinalReleaseApplied", BoolRaw(GetStateBool(execution, "SafeLandingMountCancelFinalReleaseApplied", false)), true);
            AppendRaw(builder, "grappleTargetKnown", BoolRaw(GetStateBool(execution, "SafeLandingGrappleTargetKnown", false)), true);
            AppendString(builder, "grappleTargetWorldX", GetState(execution, "SafeLandingGrappleTargetWorldX", string.Empty), true);
            AppendString(builder, "grappleTargetWorldY", GetState(execution, "SafeLandingGrappleTargetWorldY", string.Empty), true);
            AppendRaw(builder, "grappleMouseTargetCaptured", BoolRaw(GetStateBool(execution, "SafeLandingGrappleMouseTargetCaptured", false)), true);
            AppendRaw(builder, "grappleMouseTargetRestoreAttempted", BoolRaw(GetStateBool(execution, "SafeLandingGrappleMouseTargetRestoreAttempted", false)), true);
            AppendRaw(builder, "grappleMouseTargetRestoreSucceeded", BoolRaw(GetStateBool(execution, "SafeLandingGrappleMouseTargetRestoreSucceeded", false)), true);
            AppendString(builder, "grappleMouseTargetRestoreMessage", GetState(execution, "SafeLandingGrappleMouseTargetRestoreMessage", string.Empty), true);
            AppendRaw(builder, "suppressedDownInput", BoolRaw(GetStateBool(execution, "SafeLandingSuppressDown", false)), true);
            AppendString(builder, "pulseStatus", GetState(execution, "SafeLandingPulseStatus", string.Empty), true);
            AppendString(builder, "pulsePhase", GetState(execution, "SafeLandingPulsePhase", string.Empty), true);
            AppendString(builder, "pulseApplySite", GetState(execution, "SafeLandingPulseApplySite", string.Empty), true);
            AppendString(builder, "pulseMessage", GetState(execution, "SafeLandingPulseMessage", string.Empty), true);
            AppendRaw(builder, "directVelocityMutation", "false", true);
            AppendRaw(builder, "directPositionMutation", "false", true);
            AppendRaw(builder, "directNoFallDamageMutation", "false", true);
            AppendString(builder, "releaseMessage", GetState(execution, "SafeLandingReleaseMessage", string.Empty), true);
            AppendString(builder, "pressMessage", GetState(execution, "SafeLandingPressMessage", string.Empty), true);
            AppendString(builder, "finalReleaseMessage", GetState(execution, "SafeLandingFinalReleaseMessage", string.Empty), true);
            AppendString(builder, "verificationReason", verificationReason, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static void CopyPulseSnapshotToState(InputActionExecution execution, SafeLandingJumpPulseSnapshot pulse)
        {
            if (pulse == null)
            {
                return;
            }

            SetState(execution, "SafeLandingPulseActionType", pulse.ActionType);
            SetState(execution, "SafeLandingPulseStatus", pulse.Status);
            SetState(execution, "SafeLandingPulsePhase", pulse.Phase);
            SetState(execution, "SafeLandingPulseApplySite", pulse.LastApplySite);
            SetState(execution, "SafeLandingPulseMessage", pulse.LastMessage);
            SetState(execution, "SafeLandingReleaseApplied", pulse.ReleaseApplied ? "true" : "false");
            SetState(execution, "SafeLandingPressApplied", pulse.PressApplied ? "true" : "false");
            SetState(execution, "SafeLandingFinalReleaseApplied", pulse.FinalReleaseApplied ? "true" : "false");
            SetState(execution, "SafeLandingImmediateMountCancel", pulse.ImmediateCancelAfterPress ? "true" : "false");
            SetState(execution, "SafeLandingMountCancelPressApplied", pulse.CancelPressApplied ? "true" : "false");
            SetState(execution, "SafeLandingMountCancelFinalReleaseApplied", pulse.CancelFinalReleaseApplied ? "true" : "false");
            SetState(execution, "SafeLandingGrappleTargetKnown", pulse.TargetWorldKnown ? "true" : "false");
            if (pulse.TargetWorldKnown)
            {
                SetState(execution, "SafeLandingGrappleTargetWorldX", pulse.TargetWorldX.ToString("0.###", CultureInfo.InvariantCulture));
                SetState(execution, "SafeLandingGrappleTargetWorldY", pulse.TargetWorldY.ToString("0.###", CultureInfo.InvariantCulture));
            }
            SetState(execution, "SafeLandingGrappleMouseTargetCaptured", pulse.MouseTargetCaptured ? "true" : "false");
            SetState(execution, "SafeLandingGrappleMouseTargetRestoreAttempted", pulse.MouseTargetRestoreAttempted ? "true" : "false");
            SetState(execution, "SafeLandingGrappleMouseTargetRestoreSucceeded", pulse.MouseTargetRestoreSucceeded ? "true" : "false");
            SetState(execution, "SafeLandingGrappleMouseTargetRestoreMessage", pulse.MouseTargetRestoreMessage ?? string.Empty);
            SetState(execution, "SafeLandingHoldTicks", pulse.HoldTicks);
            SetState(execution, "SafeLandingHoldTargetTicks", pulse.HoldTargetTicks);
            SetState(execution, "SafeLandingReleaseMessage", pulse.LastMessage);
            SetState(execution, "SafeLandingPressMessage", pulse.LastMessage);
            SetState(execution, "SafeLandingFinalReleaseMessage", pulse.LastMessage);
        }

        private static void CopySimulatedPulseSnapshotToState(InputActionExecution execution, SimulatedJumpPulseSnapshot pulse)
        {
            if (pulse == null)
            {
                return;
            }

            SetState(execution, "SimulatedJumpPulseStatus", pulse.Status);
            SetState(execution, "SimulatedJumpPulsePhase", pulse.Phase);
            SetState(execution, "SimulatedJumpPulseApplySite", pulse.LastApplySite);
            SetState(execution, "SimulatedJumpPulseMessage", pulse.LastMessage);
            SetState(execution, "SimulatedJumpReleaseApplied", pulse.ReleaseApplied ? "true" : "false");
            SetState(execution, "SimulatedJumpPressApplied", pulse.PressApplied ? "true" : "false");
            SetState(execution, "SimulatedJumpApplyRocketRelease", pulse.ApplyRocketRelease ? "true" : "false");
        }

        private static InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            string scenario,
            string triggerReason,
            JumpInputProfile before,
            JumpInputProfile after,
            bool jumpReleasePrimed,
            bool rocketReleaseApplied,
            string verificationReason)
        {
            return FinishWithPulse(
                execution,
                startedUtc,
                status,
                code,
                message,
                scenario,
                triggerReason,
                before,
                after,
                jumpReleasePrimed,
                false,
                rocketReleaseApplied,
                string.Empty,
                string.Empty,
                string.Empty,
                verificationReason);
        }

        private static InputActionExecutionStepResult FinishWithPulse(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            string scenario,
            string triggerReason,
            JumpInputProfile before,
            JumpInputProfile after,
            bool jumpReleasePrimed,
            bool jumpPressApplied,
            bool rocketReleaseApplied,
            string pulseStatus,
            string pulsePhase,
            string pulseApplySite,
            string verificationReason)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var beforeJson = GetState(execution, "SimulatedJumpBeforeJson", string.Empty);
            if (string.IsNullOrWhiteSpace(beforeJson))
            {
                beforeJson = BuildJumpStateJson(before);
            }

            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                scenario,
                InputActionKind.Jump.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message,
                (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds,
                beforeJson,
                BuildJumpStateJson(after),
                BuildVerificationJson(triggerReason, jumpReleasePrimed, jumpPressApplied, rocketReleaseApplied, pulseStatus, pulsePhase, pulseApplySite, verificationReason),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message);
        }

        private static void SetState(InputActionExecution execution, string key, string value)
        {
            if (execution == null || execution.State == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            execution.State[key] = value ?? string.Empty;
        }

        private static void SetState(InputActionExecution execution, string key, int value)
        {
            SetState(execution, key, value.ToString(CultureInfo.InvariantCulture));
        }

        private static string GetState(InputActionExecution execution, string key, string fallback)
        {
            if (execution == null || execution.State == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            string value;
            return execution.State.TryGetValue(key, out value) ? value ?? fallback : fallback;
        }

        private static int GetStateInt(InputActionExecution execution, string key, int fallback)
        {
            int value;
            return int.TryParse(GetState(execution, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static bool GetStateBool(InputActionExecution execution, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(GetState(execution, key, string.Empty), out value) ? value : fallback;
        }

        private static bool TryGetMetadataFloat(InputActionExecution execution, string key, out float value)
        {
            value = 0f;
            return float.TryParse(GetMetadataString(execution, key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string BuildJumpStateJson(JumpInputProfile profile)
        {
            if (profile == null)
            {
                return "{}";
            }

            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "playerControllable", BoolRaw(profile.PlayerControllable), true);
            AppendRaw(builder, "controlJump", BoolRaw(profile.ControlJump), true);
            AppendRaw(builder, "releaseJump", BoolRaw(profile.ReleaseJump), true);
            AppendRaw(builder, "controlDown", BoolRaw(profile.ControlDown), true);
            AppendRaw(builder, "groundedOrSliding", BoolRaw(profile.GroundedOrSliding), true);
            AppendRaw(builder, "aerialJumpWindow", BoolRaw(profile.AerialJumpWindow), true);
            AppendRaw(builder, "availableJumpOpportunity", BoolRaw(profile.HasAvailableJumpOpportunity), true);
            AppendRaw(builder, "airJumpFlagCount", IntRaw(profile.AirJumpFlagCount), true);
            AppendRaw(builder, "hasAirJump", BoolRaw(profile.HasAirJump), true);
            AppendRaw(builder, "hasRocketJump", BoolRaw(profile.HasRocketJump), true);
            AppendRaw(builder, "hasRocketBootsAvailable", BoolRaw(profile.HasRocketBootsAvailable), true);
            AppendRaw(builder, "canUseBootFlyingAbilities", BoolRaw(profile.CanUseBootFlyingAbilities), true);
            AppendRaw(builder, "canUseBootFlyingAbilitiesKnown", BoolRaw(profile.CanUseBootFlyingAbilitiesKnown), true);
            AppendRaw(builder, "rocketBoots", IntRaw(profile.RocketBoots), true);
            AppendRaw(builder, "rocketDelay", IntRaw(profile.RocketDelay), true);
            AppendRaw(builder, "canRocket", BoolRaw(profile.CanRocket), true);
            AppendRaw(builder, "canRocketKnown", BoolRaw(profile.CanRocketKnown), true);
            AppendRaw(builder, "rocketRelease", BoolRaw(profile.RocketRelease), true);
            AppendRaw(builder, "rocketTime", FloatRaw(profile.RocketTime), true);
            AppendRaw(builder, "hasFlyingCarpet", BoolRaw(profile.HasFlyingCarpet), true);
            AppendRaw(builder, "hasFlyingCarpetAvailable", BoolRaw(profile.HasFlyingCarpetAvailable), true);
            AppendRaw(builder, "flyingCarpetCanStart", BoolRaw(profile.FlyingCarpetCanStart), true);
            AppendRaw(builder, "flyingCarpetTime", IntRaw(profile.FlyingCarpetTime), true);
            AppendRaw(builder, "hasGravityGlobe", BoolRaw(profile.HasGravityGlobe), true);
            AppendRaw(builder, "hasGravityFlipOpportunity", BoolRaw(profile.HasGravityFlipOpportunity), true);
            AppendRaw(builder, "hasWingFlight", BoolRaw(profile.HasWingFlight), true);
            AppendRaw(builder, "wingTime", FloatRaw(profile.WingTime), true);
            AppendRaw(builder, "mountActive", BoolRaw(profile.MountActive), true);
            AppendRaw(builder, "mountCanFlyKnown", BoolRaw(profile.MountCanFlyKnown), true);
            AppendRaw(builder, "mountCanFly", BoolRaw(profile.MountCanFly), true);
            AppendRaw(builder, "mountNoFallDamageKnown", BoolRaw(profile.MountNoFallDamageKnown), true);
            AppendRaw(builder, "mountNoFallDamage", BoolRaw(profile.MountNoFallDamage), true);
            AppendRaw(builder, "hasEquippedFlyingMount", BoolRaw(profile.HasEquippedFlyingMountOpportunity), true);
            AppendRaw(builder, "hasEquippedSafeMount", BoolRaw(profile.HasEquippedSafeMountOpportunity), true);
            AppendRaw(builder, "equippedMountItemType", IntRaw(profile.EquippedMountItemType), true);
            AppendRaw(builder, "equippedMountType", IntRaw(profile.EquippedMountType), true);
            AppendRaw(builder, "equippedMountNoFallDamageKnown", BoolRaw(profile.EquippedMountNoFallDamageKnown), true);
            AppendRaw(builder, "equippedMountNoFallDamage", BoolRaw(profile.EquippedMountNoFallDamage), true);
            AppendRaw(builder, "hasEquippedGrapple", BoolRaw(profile.HasEquippedGrapple), true);
            AppendRaw(builder, "hasInventoryGrapple", BoolRaw(profile.HasInventoryGrapple), true);
            AppendRaw(builder, "equippedGrappleItemType", IntRaw(profile.EquippedGrappleItemType), true);
            AppendRaw(builder, "inventoryGrappleItemType", IntRaw(profile.InventoryGrappleItemType), true);
            AppendString(builder, "capabilitySummary", profile.CapabilitySummary, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildVerificationJson(
            string triggerReason,
            bool jumpReleasePrimed,
            bool jumpPressApplied,
            bool rocketReleaseApplied,
            string pulseStatus,
            string pulsePhase,
            string pulseApplySite,
            string verificationReason)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "triggerReason", triggerReason, true);
            AppendRaw(builder, "jumpReleasePrimed", BoolRaw(jumpReleasePrimed), true);
            AppendRaw(builder, "jumpPressApplied", BoolRaw(jumpPressApplied), true);
            AppendRaw(builder, "rocketReleaseApplied", BoolRaw(rocketReleaseApplied), true);
            AppendString(builder, "pulseStatus", pulseStatus, true);
            AppendString(builder, "pulsePhase", pulsePhase, true);
            AppendString(builder, "pulseApplySite", pulseApplySite, true);
            AppendRaw(builder, "directVelocityMutation", "false", true);
            AppendRaw(builder, "directAbilityMutation", "false", true);
            AppendRaw(builder, "preservesDownJump", "true", true);
            AppendString(builder, "verificationReason", verificationReason, false);
            builder.Append("}");
            return builder.ToString();
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

        private static string FloatRaw(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
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
