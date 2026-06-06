using System;
using System.Globalization;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Movement;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class InventorySlotActionExecutor : InputActionExecutorBase
    {
        private const string SafeLandingModeKey = "SafeLandingRescueMode";
        private const string SafeLandingTemporaryEquipmentApply = "TemporaryEquipmentApply";
        private const string SafeLandingTemporaryEquipmentRestore = "TemporaryEquipmentRestore";
        private const string FishingLoadoutVerifyMode = "FishingLoadoutVerify";
        private const int FishingLoadoutVerificationWaitTicks = 12;
        private const int FishingLoadoutVerificationRetryIntervalTicks = 4;

        public override InputActionKind Kind { get { return InputActionKind.InventorySlot; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var scenario = GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty);
            if (string.Equals(scenario, ScenarioNames.MovementSafeLanding, StringComparison.Ordinal) &&
                IsSafeLandingTemporaryEquipmentMode(GetMetadataString(execution, SafeLandingModeKey, string.Empty)))
            {
                return StartMovementSafeLandingTemporaryEquipment(execution, snapshot, startedUtc);
            }

            if (string.Equals(scenario, ScenarioNames.FishingAutoEquipmentApply, StringComparison.Ordinal) ||
                string.Equals(scenario, ScenarioNames.FishingAutoEquipmentRestore, StringComparison.Ordinal))
            {
                return StartAutoEquipment(execution, snapshot, startedUtc, scenario);
            }

            if (string.Equals(scenario, ScenarioNames.InventoryQuickBagOpen, StringComparison.Ordinal))
            {
                return StartQuickBagOpen(execution, snapshot, startedUtc);
            }

            if (string.Equals(scenario, ScenarioNames.InventoryAutoExtractinator, StringComparison.Ordinal))
            {
                return StartAutoExtractinator(execution, snapshot, startedUtc);
            }

            if (string.Equals(scenario, ScenarioNames.InventoryKeepFavorited, StringComparison.Ordinal))
            {
                return StartKeepFavorited(execution, snapshot, startedUtc);
            }

            // InventorySlot is a controlled ActionQueue executor, not a generic
            // inventory mutation API. Slot changes must stay inside scenario Compat.
            if (!string.Equals(scenario, ScenarioNames.FishingAutoLoadoutSwitch, StringComparison.Ordinal) &&
                !string.Equals(scenario, ScenarioNames.FishingAutoLoadoutRestore, StringComparison.Ordinal))
            {
                return Finish(execution, startedUtc, InputActionStatus.NotImplemented, DiagnosticResultCode.NotImplemented, "InventorySlot only supports fishing loadout, fishing auto equipment, quick bag open, auto extractinator, keep favorited scenarios.", -1, -1, -1, string.Empty, false);
            }

            if (IsBlockedForCombatInput(snapshot))
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Fishing loadout switch blocked by world/UI state.", -1, -1, -1, string.Empty, false);
            }

            if (snapshot == null || snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Fishing loadout switch blocked because local player is unavailable or inactive.", -1, -1, -1, string.Empty, false);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Local player unavailable for fishing loadout switch: " + TerrariaInputCompat.LastInputCompatError, -1, -1, -1, string.Empty, false);
            }

            var target = GetMetadataInt(execution, "TargetLoadoutIndex", -1);
            var original = GetMetadataInt(execution, "OriginalLoadoutIndex", -1);
            var reason = GetMetadataString(execution, "Reason", string.Empty);

            int count;
            if (!FishingLoadoutCompat.TryGetLoadoutCount(player, out count) || count <= 0)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Fishing loadout count unavailable.", target, original, -1, reason, false);
            }

            if (target < 0 || target >= count)
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Target loadout index out of range: " + target + ", count=" + count + ".", target, original, -1, reason, false);
            }

            int before;
            if (!FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out before))
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Current loadout index unavailable before switch.", target, original, -1, reason, false);
            }

            if (before == target)
            {
                return Finish(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, "Already on target fishing loadout " + target + ".", target, original, before, reason, false);
            }

            string message;
            var invoked = FishingLoadoutCompat.TrySwitchLoadout(player, target, out message);
            int after;
            var verified = FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out after) && after == target;
            if (verified)
            {
                return Finish(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, "Fishing loadout switched " + before + " -> " + target + ". " + message, target, original, after, reason, invoked);
            }

            if (invoked)
            {
                return BeginFishingLoadoutVerification(execution, target, original, before, after, reason, message);
            }

            return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, message, target, original, after, reason, false);
        }

        public override InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (string.Equals(GetState(execution, SafeLandingModeKey, string.Empty), SafeLandingTemporaryEquipmentApply, StringComparison.Ordinal))
            {
                return UpdateMovementSafeLandingTemporaryEquipmentApply(execution, snapshot);
            }

            if (string.Equals(GetState(execution, "InventorySlotMode", string.Empty), FishingLoadoutVerifyMode, StringComparison.Ordinal))
            {
                return UpdateFishingLoadoutVerification(execution, snapshot);
            }

            return base.Update(execution, snapshot);
        }

        public override InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
        {
            if (string.Equals(GetState(execution, SafeLandingModeKey, string.Empty), SafeLandingTemporaryEquipmentApply, StringComparison.Ordinal))
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

            return base.Cancel(execution, reason);
        }

        private InputActionExecutionStepResult BeginFishingLoadoutVerification(
            InputActionExecution execution,
            int target,
            int original,
            int before,
            int current,
            string reason,
            string message)
        {
            SetState(execution, "InventorySlotMode", FishingLoadoutVerifyMode);
            SetState(execution, "LoadoutTargetIndex", target.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "LoadoutOriginalIndex", original.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "LoadoutBeforeIndex", before.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "LoadoutCurrentIndex", current.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "LoadoutReason", reason ?? string.Empty);
            SetState(execution, "LoadoutInitialMessage", message ?? string.Empty);
            SetState(execution, "LoadoutVerificationTicks", "0");
            SetState(execution, "LoadoutLastRetryTick", "0");
            SetState(execution, "LoadoutRetryCount", "0");
            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("Fishing loadout switch invoked; waiting briefly for Terraria to verify the target loadout.");
        }

        private InputActionExecutionStepResult UpdateFishingLoadoutVerification(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var target = GetStateInt(execution, "LoadoutTargetIndex", GetMetadataInt(execution, "TargetLoadoutIndex", -1));
            var original = GetStateInt(execution, "LoadoutOriginalIndex", GetMetadataInt(execution, "OriginalLoadoutIndex", -1));
            var reason = GetState(execution, "LoadoutReason", GetMetadataString(execution, "Reason", string.Empty));

            if (IsBlockedForCombatInput(snapshot))
            {
                return Finish(execution, execution == null ? DateTime.UtcNow : execution.StartedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Fishing loadout verification blocked by world/UI state.", target, original, GetStateInt(execution, "LoadoutCurrentIndex", -1), reason, true);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return Finish(execution, execution == null ? DateTime.UtcNow : execution.StartedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Local player unavailable while verifying fishing loadout: " + TerrariaInputCompat.LastInputCompatError, target, original, GetStateInt(execution, "LoadoutCurrentIndex", -1), reason, true);
            }

            int current;
            if (!FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out current))
            {
                return Finish(execution, execution == null ? DateTime.UtcNow : execution.StartedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Current loadout index unavailable while verifying switch.", target, original, GetStateInt(execution, "LoadoutCurrentIndex", -1), reason, true);
            }

            SetState(execution, "LoadoutCurrentIndex", current.ToString(CultureInfo.InvariantCulture));
            if (current == target)
            {
                return Finish(execution, execution == null ? DateTime.UtcNow : execution.StartedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, "Fishing loadout switch verified after short wait.", target, original, current, reason, true);
            }

            var waitTicks = GetStateInt(execution, "LoadoutVerificationTicks", 0) + 1;
            SetState(execution, "LoadoutVerificationTicks", waitTicks.ToString(CultureInfo.InvariantCulture));

            var lastRetryTick = GetStateInt(execution, "LoadoutLastRetryTick", 0);
            if (waitTicks - lastRetryTick >= FishingLoadoutVerificationRetryIntervalTicks &&
                waitTicks < FishingLoadoutVerificationWaitTicks)
            {
                string retryMessage;
                var retryInvoked = FishingLoadoutCompat.TrySwitchLoadout(player, target, out retryMessage);
                SetState(execution, "LoadoutLastRetryTick", waitTicks.ToString(CultureInfo.InvariantCulture));
                SetState(execution, "LoadoutRetryCount", (GetStateInt(execution, "LoadoutRetryCount", 0) + 1).ToString(CultureInfo.InvariantCulture));
                SetState(execution, "LoadoutRetryMessage", retryMessage ?? string.Empty);

                int afterRetry;
                if (retryInvoked &&
                    FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out afterRetry) &&
                    afterRetry == target)
                {
                    SetState(execution, "LoadoutCurrentIndex", afterRetry.ToString(CultureInfo.InvariantCulture));
                    return Finish(execution, execution == null ? DateTime.UtcNow : execution.StartedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, "Fishing loadout switch verified after retry. " + retryMessage, target, original, afterRetry, reason, true);
                }
            }

            if (waitTicks >= FishingLoadoutVerificationWaitTicks)
            {
                var message = "Fishing loadout switch invoked but target was not verified after short wait. " +
                              GetState(execution, "LoadoutInitialMessage", string.Empty);
                return Finish(execution, execution == null ? DateTime.UtcNow : execution.StartedUtc, InputActionStatus.AttemptedButUnverified, DiagnosticResultCode.AttemptedButUnverified, message, target, original, current, reason, true);
            }

            return InputActionExecutionStepResult.Running("Fishing loadout verification waiting for target loadout.");
        }

        private InputActionExecutionStepResult StartMovementSafeLandingTemporaryEquipment(InputActionExecution execution, GameStateSnapshot snapshot, DateTime startedUtc)
        {
            var mode = GetMetadataString(execution, SafeLandingModeKey, string.Empty);
            SetState(execution, SafeLandingModeKey, mode);
            if (IsBlockedForCombatInput(snapshot))
            {
                return FinishMovementSafeLandingEquipment(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Safe landing temporary equipment blocked by world/UI state.", null);
            }

            if (snapshot == null || snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return FinishMovementSafeLandingEquipment(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Safe landing temporary equipment blocked because local player is unavailable, dead, or ghost.", null);
            }

            MovementSafeLandingEquipmentActionResult result;
            if (string.Equals(mode, SafeLandingTemporaryEquipmentRestore, StringComparison.Ordinal))
            {
                var restored = MovementSafeLandingEquipmentCompat.TryRestoreRegisteredRecords(execution.Request.RequestId, out result);
                var restoreStatus = MapMovementSafeLandingEquipmentStatus(mode, restored, result);
                var restoreCode = MapDiagnosticCode(restoreStatus, result);
                var restoreMessage = result == null || string.IsNullOrWhiteSpace(result.Message)
                    ? "Safe landing temporary equipment restore completed."
                    : result.Message;
                return FinishMovementSafeLandingEquipment(execution, startedUtc, restoreStatus, restoreCode, restoreMessage, result);
            }

            var applied = MovementSafeLandingEquipmentCompat.TryApplyRegisteredPlan(execution.Request.RequestId, out result);
            if (!applied || result == null || result.AppliedMoveCount <= 0)
            {
                var applyStatus = MapMovementSafeLandingEquipmentStatus(mode, applied, result);
                var applyCode = MapDiagnosticCode(applyStatus, result);
                var applyMessage = result == null || string.IsNullOrWhiteSpace(result.Message)
                    ? "Safe landing temporary equipment apply completed."
                    : result.Message;
                return FinishMovementSafeLandingEquipment(execution, startedUtc, applyStatus, applyCode, applyMessage, result);
            }

            if (result.PulseNotRequired || !ShouldSafeLandingTemporaryEquipmentPulse(execution, result))
            {
                result.PulseNotRequired = true;
                return FinishMovementSafeLandingEquipment(
                    execution,
                    startedUtc,
                    InputActionStatus.AttemptedButUnverified,
                    DiagnosticResultCode.AttemptedButUnverified,
                    "Safe landing temporary equipment was applied; rescue effect still needs in-game verification.",
                    result);
            }

            string queueMessage;
            var queued = MovementSafeLandingCompat.QueueSafeLandingJumpPulse(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                result.StrategyId,
                result.ActionType,
                GetMetadataBoolStatic(execution, "SafeLandingApplyRocketRelease", false),
                GetMetadataBoolStatic(execution, "SafeLandingSuppressDown", false),
                GetMetadataIntStatic(execution, "SafeLandingHoldTicks", 0),
                out queueMessage);
            result.PulseQueued = queued;
            result.PulseStatus = queued ? "queued" : "failed";
            result.PulseMessage = queueMessage;
            SetState(execution, "SafeLandingStrategy", result.StrategyId);
            SetState(execution, "SafeLandingActionType", result.ActionType);
            SetState(execution, "SafeLandingPulseStatus", result.PulseStatus);
            SetState(execution, "SafeLandingPulseMessage", queueMessage);
            if (!queued)
            {
                return FinishMovementSafeLandingEquipment(
                    execution,
                    startedUtc,
                    InputActionStatus.Failed,
                    DiagnosticResultCode.Failed,
                    string.IsNullOrWhiteSpace(queueMessage) ? "Safe landing temporary equipment pulse queue failed." : queueMessage,
                    result);
            }

            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("Safe landing temporary equipment applied; waiting for Player.Update input pulse.");
        }

        private InputActionExecutionStepResult UpdateMovementSafeLandingTemporaryEquipmentApply(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                return CompleteMovementSafeLandingEquipmentNow(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Safe landing temporary equipment pulse stopped: not in a playable world.");
            }

            SafeLandingJumpPulseSnapshot pulse;
            if (!MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(
                    execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                    out pulse) ||
                pulse == null)
            {
                return CompleteMovementSafeLandingEquipmentNow(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Safe landing temporary equipment pulse was lost before Player.Update consumed it.");
            }

            MovementSafeLandingEquipmentCompat.UpdateApplyPulseResult(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                pulse,
                false);
            SetState(execution, "SafeLandingPulseStatus", pulse.Status);
            SetState(execution, "SafeLandingPulsePhase", pulse.Phase);
            SetState(execution, "SafeLandingPulseApplySite", pulse.LastApplySite);
            SetState(execution, "SafeLandingPulseMessage", pulse.LastMessage);

            if (pulse.Failed)
            {
                return CompleteMovementSafeLandingEquipmentNow(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, pulse.LastMessage);
            }

            if (!pulse.Completed)
            {
                return InputActionExecutionStepResult.Running("Safe landing temporary equipment pulse waiting for Player.Update consumption: " + pulse.Phase + ".");
            }

            return CompleteMovementSafeLandingEquipmentNow(
                execution,
                InputActionStatus.AttemptedButUnverified,
                DiagnosticResultCode.AttemptedButUnverified,
                "Safe landing temporary equipment input was applied before Player.Update; rescue effect still needs in-game verification.");
        }

        private InputActionExecutionStepResult StartAutoEquipment(InputActionExecution execution, GameStateSnapshot snapshot, DateTime startedUtc, string scenario)
        {
            if (IsBlockedForCombatInput(snapshot))
            {
                return FinishAutoEquipment(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Fishing auto equipment blocked by world/UI state.", null);
            }

            if (snapshot == null || snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return FinishAutoEquipment(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Fishing auto equipment blocked because local player is unavailable, dead, or ghost.", null);
            }

            FishingAutoEquipmentActionResult result;
            var ok = string.Equals(scenario, ScenarioNames.FishingAutoEquipmentApply, StringComparison.Ordinal)
                ? FishingAutoEquipmentCompat.TryApplyRegisteredPlan(execution.Request.RequestId, out result)
                : FishingAutoEquipmentCompat.TryRestoreRegisteredRecords(execution.Request.RequestId, out result);

            var status = MapAutoEquipmentStatus(scenario, ok, result);
            var code = MapDiagnosticCode(status, result);
            var message = result == null || string.IsNullOrWhiteSpace(result.Message)
                ? "Fishing auto equipment action completed."
                : result.Message;
            return FinishAutoEquipment(execution, startedUtc, status, code, message, result);
        }

        private InputActionExecutionStepResult StartQuickBagOpen(InputActionExecution execution, GameStateSnapshot snapshot, DateTime startedUtc)
        {
            var slot = GetMetadataInt(execution, ActionMetadataKeys.TargetSlot, -1);
            var itemType = GetMetadataInt(execution, "QuickBagOpenItemType", 0);
            var repeatCount = Math.Max(1, Math.Min(GetMetadataInt(execution, "QuickBagOpenRepeatCount", 8), 30));
            if (IsBlockedForCombatInput(snapshot))
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.BlockedByUi, "Quick bag open blocked by world/UI state.", "QuickBagOpen", slot, itemType, 0, string.Empty);
            }

            if (snapshot == null || snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.Failed, "Quick bag open blocked because local player is unavailable, dead, or ghost.", "QuickBagOpen", slot, itemType, 0, string.Empty);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.Failed, "Local player unavailable for quick bag open: " + TerrariaInputCompat.LastInputCompatError, "QuickBagOpen", slot, itemType, 0, string.Empty);
            }

            int openedCount;
            string message;
            if (!QuickBagOpenCompat.TryRapidOpenSlot(player, slot, repeatCount, out openedCount, out message))
            {
                var status = string.Equals(message, "no bag stack consumed", StringComparison.OrdinalIgnoreCase)
                    ? InputActionStatus.NotApplicable
                    : InputActionStatus.Failed;
                return FinishInventorySlotMutation(execution, startedUtc, status, string.IsNullOrWhiteSpace(message) ? "Quick bag open failed." : message, "QuickBagOpen", slot, itemType, openedCount, string.Empty);
            }

            return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.Succeeded, "Quick bag open consumed " + openedCount + " bag stack(s).", "QuickBagOpen", slot, itemType, openedCount, string.Empty);
        }

        private InputActionExecutionStepResult StartAutoExtractinator(InputActionExecution execution, GameStateSnapshot snapshot, DateTime startedUtc)
        {
            var slot = GetMetadataInt(execution, ActionMetadataKeys.TargetSlot, -1);
            var itemType = GetMetadataInt(execution, "AutoExtractinatorItemType", 0);
            var repeatCount = Math.Max(1, Math.Min(GetMetadataInt(execution, "AutoExtractinatorRepeatCount", 8), 30));
            var tileX = GetMetadataInt(execution, "AutoExtractinatorTileX", -1);
            var tileY = GetMetadataInt(execution, "AutoExtractinatorTileY", -1);
            var tileType = GetMetadataInt(execution, "AutoExtractinatorTileType", 0);
            if (IsBlockedForCombatInput(snapshot))
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.BlockedByUi, "Auto extractinator blocked by world/UI state.", "AutoExtractinator", slot, itemType, 0, BuildExtractinatorExtra(tileX, tileY, tileType));
            }

            if (snapshot == null || snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.Failed, "Auto extractinator blocked because local player is unavailable, dead, or ghost.", "AutoExtractinator", slot, itemType, 0, BuildExtractinatorExtra(tileX, tileY, tileType));
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.Failed, "Local player unavailable for auto extractinator: " + TerrariaInputCompat.LastInputCompatError, "AutoExtractinator", slot, itemType, 0, BuildExtractinatorExtra(tileX, tileY, tileType));
            }

            int consumedCount;
            string message;
            if (!AutoExtractinatorCompat.TryRapidExtractSlot(player, slot, itemType, repeatCount, out consumedCount, out message))
            {
                var status = string.Equals(message, "no extractable stack consumed", StringComparison.OrdinalIgnoreCase)
                    ? InputActionStatus.NotApplicable
                    : InputActionStatus.Failed;
                return FinishInventorySlotMutation(execution, startedUtc, status, string.IsNullOrWhiteSpace(message) ? "Auto extractinator failed." : message, "AutoExtractinator", slot, itemType, consumedCount, BuildExtractinatorExtra(tileX, tileY, tileType));
            }

            return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.Succeeded, "Auto extractinator consumed " + consumedCount + " stack(s).", "AutoExtractinator", slot, itemType, consumedCount, BuildExtractinatorExtra(tileX, tileY, tileType));
        }

        private InputActionExecutionStepResult StartKeepFavorited(InputActionExecution execution, GameStateSnapshot snapshot, DateTime startedUtc)
        {
            var slot = GetMetadataInt(execution, ActionMetadataKeys.TargetSlot, -1);
            var container = GetMetadataString(execution, "KeepFavoritedContainer", GetMetadataString(execution, "SourceContainer", "Inventory"));
            var itemType = GetMetadataInt(execution, "KeepFavoritedItemType", 0);
            var signature = GetMetadataString(execution, "KeepFavoritedSignature", string.Empty);
            if (IsBlockedForCombatInput(snapshot))
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.BlockedByUi, "Keep favorited blocked by world/UI state.", "KeepFavorited", slot, itemType, 0, signature);
            }

            if (snapshot == null || snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.Failed, "Keep favorited blocked because local player is unavailable, dead, or ghost.", "KeepFavorited", slot, itemType, 0, signature);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.Failed, "Local player unavailable for keep favorited: " + TerrariaInputCompat.LastInputCompatError, "KeepFavorited", slot, itemType, 0, signature);
            }

            bool restored;
            string message;
            if (!KeepFavoritedCompat.TryRestoreFavoritedInContainer(player, container, slot, itemType, signature, out restored, out message))
            {
                var status = string.Equals(message, "slot item changed before restore", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(message, "slot item signature changed before restore", StringComparison.OrdinalIgnoreCase)
                    ? InputActionStatus.NotApplicable
                    : InputActionStatus.Failed;
                return FinishInventorySlotMutation(execution, startedUtc, status, string.IsNullOrWhiteSpace(message) ? "Keep favorited failed." : message, "KeepFavorited", slot, itemType, 0, signature);
            }

            if (restored)
            {
                return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.Succeeded, "Keep favorited restored the item favorite flag.", "KeepFavorited", slot, itemType, 1, signature);
            }

            return FinishInventorySlotMutation(execution, startedUtc, InputActionStatus.NotApplicable, string.IsNullOrWhiteSpace(message) ? "Item favorite flag already valid." : message, "KeepFavorited", slot, itemType, 0, signature);
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            int target,
            int original,
            int current,
            string reason,
            bool switchInvoked)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty),
                InputActionKind.InventorySlot.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildStateJson(target, original, -1, reason),
                BuildStateJson(target, original, current, reason),
                "{" +
                "\"targetLoadoutIndex\":" + IntRaw(target) + "," +
                "\"originalLoadoutIndex\":" + IntRaw(original) + "," +
                "\"currentLoadoutIndex\":" + IntRaw(current) + "," +
                "\"switchInvoked\":" + BoolRaw(switchInvoked) + "," +
                "\"verificationWaitTicks\":" + IntRaw(GetStateInt(execution, "LoadoutVerificationTicks", 0)) + "," +
                "\"retryCount\":" + IntRaw(GetStateInt(execution, "LoadoutRetryCount", 0)) +
                "}",
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private InputActionExecutionStepResult FinishAutoEquipment(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            FishingAutoEquipmentActionResult result)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty),
                InputActionKind.InventorySlot.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildAutoEquipmentStateJson(execution, result, false),
                BuildAutoEquipmentStateJson(execution, result, true),
                BuildAutoEquipmentVerificationJson(result),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private InputActionExecutionStepResult FinishInventorySlotMutation(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            string message,
            string action,
            int slot,
            int itemType,
            int affectedCount,
            string extra)
        {
            var code = MapDiagnosticCode(status);
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty),
                InputActionKind.InventorySlot.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildInventoryMutationStateJson(action, slot, itemType, affectedCount, false, extra, message),
                BuildInventoryMutationStateJson(action, slot, itemType, affectedCount, true, extra, message),
                BuildInventoryMutationVerificationJson(action, affectedCount, extra, status),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private InputActionExecutionStepResult CompleteMovementSafeLandingEquipmentNow(
            InputActionExecution execution,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message)
        {
            MovementSafeLandingEquipmentActionResult result;
            MovementSafeLandingEquipmentCompat.TryPeekApplyResult(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                out result);
            return FinishMovementSafeLandingEquipment(execution, execution == null ? DateTime.UtcNow : execution.StartedUtc, status, code, message, result);
        }

        private InputActionExecutionStepResult FinishMovementSafeLandingEquipment(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            MovementSafeLandingEquipmentActionResult result)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty),
                InputActionKind.InventorySlot.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildMovementSafeLandingEquipmentStateJson(execution, result, false),
                BuildMovementSafeLandingEquipmentStateJson(execution, result, true),
                BuildMovementSafeLandingEquipmentVerificationJson(result),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private static InputActionStatus MapAutoEquipmentStatus(string scenario, bool ok, FishingAutoEquipmentActionResult result)
        {
            if (result != null && result.BlockedByMouseItem)
            {
                return InputActionStatus.BlockedByUi;
            }

            if (result != null && result.LoadoutChangedDuringAutoEquipment)
            {
                return InputActionStatus.NotApplicable;
            }

            if (string.Equals(scenario, ScenarioNames.FishingAutoEquipmentApply, StringComparison.Ordinal))
            {
                if (result != null && result.AppliedMoveCount > 0)
                {
                    return InputActionStatus.Succeeded;
                }

                return InputActionStatus.NotApplicable;
            }

            if (result != null && result.PendingRestoreNoSpaceCount > 0)
            {
                return InputActionStatus.AttemptedButUnverified;
            }

            if (result != null && result.PendingRestoreCount > 0)
            {
                return InputActionStatus.AttemptedButUnverified;
            }

            return ok ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable;
        }

        private static DiagnosticResultCode MapDiagnosticCode(InputActionStatus status, FishingAutoEquipmentActionResult result)
        {
            if (result != null && result.BlockedByMouseItem)
            {
                return DiagnosticResultCode.BlockedByUi;
            }

            switch (status)
            {
                case InputActionStatus.Succeeded:
                    return DiagnosticResultCode.Succeeded;
                case InputActionStatus.AttemptedButUnverified:
                    return DiagnosticResultCode.AttemptedButUnverified;
                case InputActionStatus.NotApplicable:
                    return DiagnosticResultCode.NotApplicable;
                case InputActionStatus.BlockedByUi:
                    return DiagnosticResultCode.BlockedByUi;
                default:
                    return DiagnosticResultCode.Failed;
            }
        }

        private static InputActionStatus MapMovementSafeLandingEquipmentStatus(string mode, bool ok, MovementSafeLandingEquipmentActionResult result)
        {
            if (result != null && result.BlockedByMouseItem)
            {
                return InputActionStatus.BlockedByUi;
            }

            if (string.Equals(mode, SafeLandingTemporaryEquipmentApply, StringComparison.Ordinal))
            {
                return result != null && result.AppliedMoveCount > 0
                    ? InputActionStatus.AttemptedButUnverified
                    : InputActionStatus.NotApplicable;
            }

            if (result != null && result.PendingRestoreNoSpaceCount > 0)
            {
                return InputActionStatus.AttemptedButUnverified;
            }

            if (result != null && result.PendingRestoreCount > 0)
            {
                return InputActionStatus.AttemptedButUnverified;
            }

            if (result != null &&
                result.SelectedSlotRestoreAttempted &&
                !result.SelectedSlotRestoreSucceeded)
            {
                return InputActionStatus.AttemptedButUnverified;
            }

            return ok ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable;
        }

        private static DiagnosticResultCode MapDiagnosticCode(InputActionStatus status, MovementSafeLandingEquipmentActionResult result)
        {
            if (result != null && result.BlockedByMouseItem)
            {
                return DiagnosticResultCode.BlockedByUi;
            }

            switch (status)
            {
                case InputActionStatus.Succeeded:
                    return DiagnosticResultCode.Succeeded;
                case InputActionStatus.AttemptedButUnverified:
                    return DiagnosticResultCode.AttemptedButUnverified;
                case InputActionStatus.NotApplicable:
                    return DiagnosticResultCode.NotApplicable;
                case InputActionStatus.BlockedByUi:
                    return DiagnosticResultCode.BlockedByUi;
                default:
                    return DiagnosticResultCode.Failed;
            }
        }

        private static DiagnosticResultCode MapDiagnosticCode(InputActionStatus status)
        {
            switch (status)
            {
                case InputActionStatus.Succeeded:
                    return DiagnosticResultCode.Succeeded;
                case InputActionStatus.AttemptedButUnverified:
                    return DiagnosticResultCode.AttemptedButUnverified;
                case InputActionStatus.NotApplicable:
                    return DiagnosticResultCode.NotApplicable;
                case InputActionStatus.BlockedByUi:
                    return DiagnosticResultCode.BlockedByUi;
                default:
                    return DiagnosticResultCode.Failed;
            }
        }

        private static string BuildStateJson(int target, int original, int current, string reason)
        {
            return "{" +
                   "\"targetLoadoutIndex\":" + IntRaw(target) + "," +
                   "\"originalLoadoutIndex\":" + IntRaw(original) + "," +
                   "\"currentLoadoutIndex\":" + IntRaw(current) + "," +
                   "\"reason\":\"" + EscapeJson(reason) + "\"" +
                   "}";
        }

        private static string BuildAutoEquipmentStateJson(InputActionExecution execution, FishingAutoEquipmentActionResult result, bool after)
        {
            return "{" +
                   "\"originalSelectedItemIndex\":" + IntRaw(GetMetadataIntStatic(execution, "OriginalSelectedItemIndex", -1)) + "," +
                   "\"originalLoadoutIndex\":" + IntRaw(GetMetadataIntStatic(execution, "OriginalLoadoutIndex", -1)) + "," +
                   "\"plannedMoveCount\":" + IntRaw(GetMetadataIntStatic(execution, "PlannedMoveCount", 0)) + "," +
                   "\"pendingRestoreCountFromRequest\":" + IntRaw(GetMetadataIntStatic(execution, "PendingRestoreCount", 0)) + "," +
                   "\"reason\":\"" + EscapeJson(GetMetadataStringStatic(execution, "Reason", string.Empty)) + "\"," +
                   "\"after\":" + BoolRaw(after) + "," +
                   "\"appliedMoveCount\":" + IntRaw(result == null ? 0 : result.AppliedMoveCount) + "," +
                   "\"restoredMoveCount\":" + IntRaw(result == null ? 0 : result.RestoredMoveCount) + "," +
                   "\"pendingRestoreCount\":" + IntRaw(result == null ? 0 : result.PendingRestoreCount) + "," +
                   "\"skipReason\":\"" + EscapeJson(result == null ? string.Empty : result.SkipReason) + "\"" +
                   "}";
        }

        private static string BuildInventoryMutationStateJson(string action, int slot, int itemType, int affectedCount, bool after, string extra, string message)
        {
            return "{" +
                   "\"action\":\"" + EscapeJson(action) + "\"," +
                   "\"targetSlot\":" + IntRaw(slot) + "," +
                   "\"itemType\":" + IntRaw(itemType) + "," +
                   "\"affectedCount\":" + IntRaw(affectedCount) + "," +
                   "\"after\":" + BoolRaw(after) + "," +
                   "\"extra\":\"" + EscapeJson(extra) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildInventoryMutationVerificationJson(string action, int affectedCount, string extra, InputActionStatus status)
        {
            return "{" +
                   "\"action\":\"" + EscapeJson(action) + "\"," +
                   "\"affectedCount\":" + IntRaw(affectedCount) + "," +
                   "\"extra\":\"" + EscapeJson(extra) + "\"," +
                   "\"result\":\"" + EscapeJson(status.ToString()) + "\"," +
                   "\"controlledInventoryMutation\":true" +
                   "}";
        }

        private static string BuildAutoEquipmentVerificationJson(FishingAutoEquipmentActionResult result)
        {
            return "{" +
                   "\"autoEquipmentInvoked\":" + BoolRaw(result != null && result.Invoked) + "," +
                   "\"appliedMoveCount\":" + IntRaw(result == null ? 0 : result.AppliedMoveCount) + "," +
                   "\"restoredMoveCount\":" + IntRaw(result == null ? 0 : result.RestoredMoveCount) + "," +
                   "\"pendingRestoreCount\":" + IntRaw(result == null ? 0 : result.PendingRestoreCount) + "," +
                   "\"skippedMoveCount\":" + IntRaw(result == null ? 0 : result.SkippedMoveCount) + "," +
                   "\"blockedByMouseItem\":" + BoolRaw(result != null && result.BlockedByMouseItem) + "," +
                   "\"stillHoldingOriginalRod\":" + BoolRaw(result != null && result.StillHoldingOriginalRod) + "," +
                   "\"leftOriginalRod\":" + BoolRaw(result != null && result.LeftOriginalRod) + "," +
                   "\"userChangedManagedSlot\":" + BoolRaw(result != null && result.UserChangedManagedSlotCount > 0) + "," +
                   "\"userChangedManagedSlotCount\":" + IntRaw(result == null ? 0 : result.UserChangedManagedSlotCount) + "," +
                   "\"originalMovedByUser\":" + BoolRaw(result != null && result.OriginalMovedByUserCount > 0) + "," +
                   "\"originalMovedByUserCount\":" + IntRaw(result == null ? 0 : result.OriginalMovedByUserCount) + "," +
                   "\"originalRelocatedByUser\":" + BoolRaw(result != null && result.OriginalRelocatedByUserCount > 0) + "," +
                   "\"originalRelocatedByUserCount\":" + IntRaw(result == null ? 0 : result.OriginalRelocatedByUserCount) + "," +
                   "\"pendingRestoreNoSpace\":" + BoolRaw(result != null && result.PendingRestoreNoSpaceCount > 0) + "," +
                   "\"pendingRestoreNoSpaceCount\":" + IntRaw(result == null ? 0 : result.PendingRestoreNoSpaceCount) + "," +
                   "\"loadoutChangedDuringAutoEquipment\":" + BoolRaw(result != null && result.LoadoutChangedDuringAutoEquipment) + "," +
                   "\"decision\":\"" + EscapeJson(result == null ? string.Empty : result.Decision) + "\"," +
                   "\"skipReason\":\"" + EscapeJson(result == null ? string.Empty : result.SkipReason) + "\"" +
                   "}";
        }

        private static string BuildMovementSafeLandingEquipmentStateJson(InputActionExecution execution, MovementSafeLandingEquipmentActionResult result, bool after)
        {
            return "{" +
                   "\"rescueMode\":\"" + EscapeJson(GetMetadataStringStatic(execution, SafeLandingModeKey, string.Empty)) + "\"," +
                   "\"strategy\":\"" + EscapeJson(result == null ? GetMetadataStringStatic(execution, "SafeLandingStrategy", string.Empty) : result.StrategyId) + "\"," +
                   "\"equipmentCategory\":\"" + EscapeJson(result == null ? GetMetadataStringStatic(execution, "SafeLandingEquipmentCategory", string.Empty) : result.EquipmentCategory) + "\"," +
                   "\"actionType\":\"" + EscapeJson(result == null ? GetMetadataStringStatic(execution, "SafeLandingActionType", string.Empty) : result.ActionType) + "\"," +
                   "\"priority\":" + IntRaw(GetMetadataIntStatic(execution, "SafeLandingPriority", -1)) + "," +
                   "\"sourceKind\":\"" + EscapeJson(GetMetadataStringStatic(execution, "SafeLandingEquipmentSourceKind", string.Empty)) + "\"," +
                   "\"sourceSlot\":" + IntRaw(GetMetadataIntStatic(execution, "SafeLandingEquipmentSourceSlot", -1)) + "," +
                   "\"targetKind\":\"" + EscapeJson(GetMetadataStringStatic(execution, "SafeLandingEquipmentTargetKind", string.Empty)) + "\"," +
                   "\"targetSlot\":" + IntRaw(GetMetadataIntStatic(execution, "SafeLandingEquipmentTargetSlot", -1)) + "," +
                   "\"candidateItemType\":" + IntRaw(GetMetadataIntStatic(execution, "SafeLandingEquipmentItemType", 0)) + "," +
                   "\"candidateItemName\":\"" + EscapeJson(GetMetadataStringStatic(execution, "SafeLandingEquipmentItemName", string.Empty)) + "\"," +
                   "\"candidateMountType\":" + IntRaw(GetMetadataIntStatic(execution, "SafeLandingEquipmentMountType", -1)) + "," +
                   "\"impactTicks\":\"" + EscapeJson(GetMetadataStringStatic(execution, "SafeLandingImpactTicks", string.Empty)) + "\"," +
                   "\"impactDistancePixels\":\"" + EscapeJson(GetMetadataStringStatic(execution, "SafeLandingImpactDistancePixels", string.Empty)) + "\"," +
                   "\"after\":" + BoolRaw(after) + "," +
                   "\"appliedMoveCount\":" + IntRaw(result == null ? 0 : result.AppliedMoveCount) + "," +
                   "\"restoredMoveCount\":" + IntRaw(result == null ? 0 : result.RestoredMoveCount) + "," +
                   "\"pendingRestoreCount\":" + IntRaw(result == null ? 0 : result.PendingRestoreCount) + "," +
                   "\"skipReason\":\"" + EscapeJson(result == null ? string.Empty : result.SkipReason) + "\"" +
                   "}";
        }

        private static string BuildMovementSafeLandingEquipmentVerificationJson(MovementSafeLandingEquipmentActionResult result)
        {
            return "{" +
                   "\"temporaryEquipmentInvoked\":" + BoolRaw(result != null && result.Invoked) + "," +
                   "\"equipmentSwapApplied\":" + BoolRaw(result != null && result.AppliedMoveCount > 0) + "," +
                   "\"appliedMoveCount\":" + IntRaw(result == null ? 0 : result.AppliedMoveCount) + "," +
                   "\"restoredMoveCount\":" + IntRaw(result == null ? 0 : result.RestoredMoveCount) + "," +
                   "\"pendingRestoreCount\":" + IntRaw(result == null ? 0 : result.PendingRestoreCount) + "," +
                   "\"pendingRestoreNoSpace\":" + BoolRaw(result != null && result.PendingRestoreNoSpaceCount > 0) + "," +
                   "\"pendingRestoreNoSpaceCount\":" + IntRaw(result == null ? 0 : result.PendingRestoreNoSpaceCount) + "," +
                   "\"userChangedManagedSlot\":" + BoolRaw(result != null && result.UserChangedManagedSlotCount > 0) + "," +
                   "\"userChangedManagedSlotCount\":" + IntRaw(result == null ? 0 : result.UserChangedManagedSlotCount) + "," +
                   "\"originalMovedByUser\":" + BoolRaw(result != null && result.OriginalMovedByUserCount > 0) + "," +
                   "\"originalMovedByUserCount\":" + IntRaw(result == null ? 0 : result.OriginalMovedByUserCount) + "," +
                   "\"pulseQueued\":" + BoolRaw(result != null && result.PulseQueued) + "," +
                   "\"pulseCompleted\":" + BoolRaw(result != null && result.PulseCompleted) + "," +
                   "\"pulseFailed\":" + BoolRaw(result != null && result.PulseFailed) + "," +
                   "\"pulseNotRequired\":" + BoolRaw(result != null && result.PulseNotRequired) + "," +
                   "\"pulseStatus\":\"" + EscapeJson(result == null ? string.Empty : result.PulseStatus) + "\"," +
                   "\"pulsePhase\":\"" + EscapeJson(result == null ? string.Empty : result.PulsePhase) + "\"," +
                   "\"pulseApplySite\":\"" + EscapeJson(result == null ? string.Empty : result.PulseApplySite) + "\"," +
                   "\"pulseMessage\":\"" + EscapeJson(result == null ? string.Empty : result.PulseMessage) + "\"," +
                   "\"functionalRefreshAttempted\":" + BoolRaw(result != null && result.FunctionalRefreshAttempted) + "," +
                   "\"functionalRefreshSucceeded\":" + BoolRaw(result != null && result.FunctionalRefreshSucceeded) + "," +
                   "\"doubleJumpRefreshAttempted\":" + BoolRaw(result != null && result.DoubleJumpRefreshAttempted) + "," +
                   "\"doubleJumpRefreshSucceeded\":" + BoolRaw(result != null && result.DoubleJumpRefreshSucceeded) + "," +
                   "\"functionalRefreshMessage\":\"" + EscapeJson(result == null ? string.Empty : result.FunctionalRefreshMessage) + "\"," +
                   "\"postApplyVerificationSummary\":\"" + EscapeJson(result == null ? string.Empty : result.PostApplyVerificationSummary) + "\"," +
                   "\"postApplyVerificationReason\":\"" + EscapeJson(result == null ? string.Empty : result.PostApplyVerificationReason) + "\"," +
                   "\"postApplyRocketBoots\":" + IntRaw(result == null ? 0 : result.PostApplyRocketBoots) + "," +
                   "\"postApplyRocketTime\":\"" + EscapeJson(result == null ? string.Empty : result.PostApplyRocketTime.ToString("0.###", CultureInfo.InvariantCulture)) + "\"," +
                   "\"postApplyRocketDelay\":" + IntRaw(result == null ? 0 : result.PostApplyRocketDelay) + "," +
                   "\"postApplyCanRocket\":" + BoolRaw(result != null && result.PostApplyCanRocket) + "," +
                   "\"postApplyCanRocketKnown\":" + BoolRaw(result != null && result.PostApplyCanRocketKnown) + "," +
                   "\"postApplyRocketRelease\":" + BoolRaw(result != null && result.PostApplyRocketRelease) + "," +
                   "\"postApplyCanUseBootFlyingAbilities\":" + BoolRaw(result != null && result.PostApplyCanUseBootFlyingAbilities) + "," +
                   "\"postApplyCanUseBootFlyingAbilitiesKnown\":" + BoolRaw(result != null && result.PostApplyCanUseBootFlyingAbilitiesKnown) + "," +
                   "\"postApplyHasRocketBootsAvailable\":" + BoolRaw(result != null && result.PostApplyHasRocketBootsAvailable) + "," +
                   "\"postApplyHasFlyingCarpet\":" + BoolRaw(result != null && result.PostApplyHasFlyingCarpet) + "," +
                   "\"postApplyHasFlyingCarpetAvailable\":" + BoolRaw(result != null && result.PostApplyHasFlyingCarpetAvailable) + "," +
                   "\"postApplyFlyingCarpetCanStart\":" + BoolRaw(result != null && result.PostApplyFlyingCarpetCanStart) + "," +
                   "\"postApplyFlyingCarpetTime\":" + IntRaw(result == null ? 0 : result.PostApplyFlyingCarpetTime) + "," +
                   "\"postApplyAirJumpFlagCount\":" + IntRaw(result == null ? 0 : result.PostApplyAirJumpFlagCount) + "," +
                   "\"postApplyHasGravityGlobe\":" + BoolRaw(result != null && result.PostApplyHasGravityGlobe) + "," +
                   "\"postApplyHasGravityFlipOpportunity\":" + BoolRaw(result != null && result.PostApplyHasGravityFlipOpportunity) + "," +
                   "\"postApplyAerialJumpWindow\":" + BoolRaw(result != null && result.PostApplyAerialJumpWindow) + "," +
                   "\"postApplyGravityDirection\":\"" + EscapeJson(result == null ? string.Empty : result.PostApplyGravityDirection.ToString("0.###", CultureInfo.InvariantCulture)) + "\"," +
                   "\"postApplyHasWingFlight\":" + BoolRaw(result != null && result.PostApplyHasWingFlight) + "," +
                   "\"postApplyWingsLogic\":" + IntRaw(result == null ? 0 : result.PostApplyWingsLogic) + "," +
                   "\"postApplyWingTime\":\"" + EscapeJson(result == null ? string.Empty : result.PostApplyWingTime.ToString("0.###", CultureInfo.InvariantCulture)) + "\"," +
                   "\"selectedSlotApplyAttempted\":" + BoolRaw(result != null && result.SelectedSlotApplyAttempted) + "," +
                   "\"selectedSlotApplySucceeded\":" + BoolRaw(result != null && result.SelectedSlotApplySucceeded) + "," +
                   "\"selectedSlotRestoreAttempted\":" + BoolRaw(result != null && result.SelectedSlotRestoreAttempted) + "," +
                   "\"selectedSlotRestoreSucceeded\":" + BoolRaw(result != null && result.SelectedSlotRestoreSucceeded) + "," +
                   "\"selectedSlotMessage\":\"" + EscapeJson(result == null ? string.Empty : result.SelectedSlotMessage) + "\"," +
                   "\"controlledInventoryMutation\":true," +
                   "\"directVelocityMutation\":false," +
                   "\"directPositionMutation\":false," +
                   "\"directNoFallDamageMutation\":false," +
                   "\"decision\":\"" + EscapeJson(result == null ? string.Empty : result.Decision) + "\"," +
                   "\"skipReason\":\"" + EscapeJson(result == null ? string.Empty : result.SkipReason) + "\"" +
                   "}";
        }

        private static bool IsSafeLandingTemporaryEquipmentMode(string mode)
        {
            return string.Equals(mode, SafeLandingTemporaryEquipmentApply, StringComparison.Ordinal) ||
                   string.Equals(mode, SafeLandingTemporaryEquipmentRestore, StringComparison.Ordinal);
        }

        private static bool ShouldSafeLandingTemporaryEquipmentPulse(InputActionExecution execution, MovementSafeLandingEquipmentActionResult result)
        {
            // The swap itself must settle through Terraria's normal equipment refresh before an active input is useful.
            // MovementSafeLandingService submits the follow-up Jump/QuickMount action while keeping the restore record pending.
            return false;
        }

        private static string BuildExtractinatorExtra(int tileX, int tileY, int tileType)
        {
            return "tile=" + tileX.ToString(CultureInfo.InvariantCulture) +
                   "," + tileY.ToString(CultureInfo.InvariantCulture) +
                   ",type=" + tileType.ToString(CultureInfo.InvariantCulture);
        }

        private static void SetState(InputActionExecution execution, string key, string value)
        {
            if (execution == null || execution.State == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            execution.State[key] = value ?? string.Empty;
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
            int parsed;
            return int.TryParse(GetState(execution, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static int GetMetadataIntStatic(InputActionExecution execution, string key, int fallback)
        {
            if (execution == null || execution.Request == null || execution.Request.Metadata == null)
            {
                return fallback;
            }

            string value;
            int parsed;
            return execution.Request.Metadata.TryGetValue(key, out value) &&
                   int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static bool GetMetadataBoolStatic(InputActionExecution execution, string key, bool fallback)
        {
            if (execution == null || execution.Request == null || execution.Request.Metadata == null)
            {
                return fallback;
            }

            string value;
            bool parsed;
            return execution.Request.Metadata.TryGetValue(key, out value) &&
                   bool.TryParse(value, out parsed)
                ? parsed
                : fallback;
        }

        private static string GetMetadataStringStatic(InputActionExecution execution, string key, string fallback)
        {
            if (execution == null || execution.Request == null || execution.Request.Metadata == null)
            {
                return fallback;
            }

            string value;
            return execution.Request.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : fallback;
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
