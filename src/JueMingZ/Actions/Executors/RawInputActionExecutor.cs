using System;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class RawInputActionExecutor : InputActionExecutorBase
    {
        private const string ModeMagicStringClicker = "MagicStringClicker";
        private const string ModeAutoFacing = "AutoFacing";
        private const string ModeAutoMiningSustainedUse = "AutoMiningSustainedUse";
        private const string ModeAutoHarvestSustainedUse = "AutoHarvestSustainedUse";
        private const string ModeAutoCaptureCritterSustainedUse = "AutoCaptureCritterSustainedUse";
        private const string ModePhasebladeQuickSwitch = "PhasebladeQuickSwitch";
        private const string AutoCapturePendingBugNetSelectionState = "AutoCapturePendingBugNetSelection";
        private const string AutoCaptureTargetSlotState = "AutoCaptureTargetSlot";
        private const string AutoCaptureOriginalSelectedSlotState = "AutoCaptureOriginalSelectedSlot";
        private const string AutoCaptureOriginalDirectionState = "AutoCaptureOriginalDirection";
        private const string AutoCaptureOriginalDirectionCapturedState = "AutoCaptureOriginalDirectionCaptured";
        private const string AutoCaptureRestoreOriginalState = "AutoCaptureRestoreOriginalState";
        private const string AutoCaptureSlotSelectionWaitTicksState = "AutoCaptureSlotSelectionWaitTicks";
        private const string AutoCaptureSlotSwitchMethodState = "AutoCaptureSlotSwitchMethod";

        public override InputActionKind Kind { get { return InputActionKind.RawInput; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var mode = GetMetadataString(execution, "RawInputMode", string.Empty);
            // RawInput modes are allowlisted here; unknown modes stay NotImplemented and
            // the resolver keeps them globally exclusive.
            if (string.Equals(mode, ModeAutoFacing, StringComparison.OrdinalIgnoreCase))
            {
                return StartAutoFacing(execution, snapshot);
            }

            if (string.Equals(mode, ModeAutoHarvestSustainedUse, StringComparison.OrdinalIgnoreCase))
            {
                return StartAutoHarvestSustainedUse(execution, snapshot);
            }

            if (string.Equals(mode, ModeAutoMiningSustainedUse, StringComparison.OrdinalIgnoreCase))
            {
                return StartAutoMiningSustainedUse(execution, snapshot);
            }

            if (string.Equals(mode, ModeAutoCaptureCritterSustainedUse, StringComparison.OrdinalIgnoreCase))
            {
                return StartAutoCaptureCritterSustainedUse(execution, snapshot);
            }

            if (string.Equals(mode, ModePhasebladeQuickSwitch, StringComparison.OrdinalIgnoreCase))
            {
                return StartPhasebladeQuickSwitch(execution, snapshot);
            }

            if (!string.Equals(mode, ModeMagicStringClicker, StringComparison.OrdinalIgnoreCase))
            {
                return CompleteWithCode(execution, InputActionStatus.NotImplemented, DiagnosticResultCode.NotImplemented, "RawInput mode is not implemented: " + mode);
            }

            if (IsBlockedForCombatInput(snapshot))
            {
                return CompleteWithCode(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Magic string clicker did not start: world input is blocked.");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Magic string clicker did not start: local player unavailable.");
            }

            bool useItemHeld;
            if (!TerrariaInputCompat.TryReadUseItemHeld(player, out useItemHeld) || !useItemHeld)
            {
                return CompleteWithCode(execution, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Magic string clicker did not start: use item is not held.");
            }

            var selectedSlot = GetMetadataInt(execution, "MagicStringSelectedSlot", -1);
            var itemType = GetMetadataInt(execution, "MagicStringItemType", 0);
            var itemName = GetMetadataString(execution, "MagicStringItemName", string.Empty);
            var intervalTicks = GetMetadataInt(execution, "MagicStringPulseIntervalTicks", 2);
            var allowCombatAim = string.Equals(GetMetadataString(execution, "AllowCombatAim", string.Empty), "true", StringComparison.OrdinalIgnoreCase);
            var requireUseItemHeld = string.Equals(GetMetadataString(execution, "RequireUseItemHeld", string.Empty), "true", StringComparison.OrdinalIgnoreCase);

            string message;
            // Magic string uses UseItemPulseBridge so pulse ownership is visible to
            // channels and ItemCheck writer arbitration.
            if (!UseItemPulseBridge.TryBegin(
                execution.Request.RequestId,
                execution.Request.SourceFeatureId,
                GetMetadataString(execution, "Scenario", "Combat.MagicStringClicker"),
                selectedSlot,
                itemType,
                itemName,
                intervalTicks,
                requireUseItemHeld,
                allowCombatAim,
                execution.Request.Timeout,
                out message))
            {
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, message);
            }

            execution.State["MagicStringSelectedSlot"] = selectedSlot.ToString(CultureInfo.InvariantCulture);
            execution.State["MagicStringItemType"] = itemType.ToString(CultureInfo.InvariantCulture);
            execution.State["MagicStringPulseIntervalTicks"] = intervalTicks.ToString(CultureInfo.InvariantCulture);
            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("Magic string clicker pulse takeover started.");
        }

        private static InputActionExecutionStepResult StartAutoFacing(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForCombatInput(snapshot))
            {
                return CompleteWithCode(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Auto facing did not run: world input is blocked.");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto facing did not run: local player unavailable.");
            }

            var requestedDirection = GetMetadataInt(execution, "AutoFacingDirection", 0);
            if (requestedDirection == 0)
            {
                return CompleteWithCode(execution, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Auto facing did not run: requested direction was unavailable.");
            }

            var directionSource = GetMetadataString(execution, "AutoFacingDirectionSource", "targetOrCursor");
            var allowFieldFallback = string.Equals(directionSource, "manualMovementInput", StringComparison.OrdinalIgnoreCase);
            int beforeDirection;
            int afterDirection;
            string method;
            var applied = TerrariaInputCompat.TryChangePlayerDirection(player, requestedDirection, allowFieldFallback, out beforeDirection, out afterDirection, out method);
            var itemCheckOverrideArmed = false;
            if (applied)
            {
                TerrariaInputCompat.BeginAutoFacingDirectionOverride(
                    execution.Request.RequestId,
                    requestedDirection,
                    GetMetadataInt(execution, "AutoFacingSelectedSlot", -1),
                    GetMetadataInt(execution, "AutoFacingItemType", 0),
                    TimeSpan.FromMilliseconds(750));
                itemCheckOverrideArmed = true;
            }

            var normalizedDirection = requestedDirection >= 0 ? 1 : -1;
            var verified = applied && afterDirection == normalizedDirection;
            var changed = beforeDirection != 0 && beforeDirection != afterDirection;
            var status = verified
                ? InputActionStatus.Succeeded
                : applied ? InputActionStatus.AttemptedButUnverified : InputActionStatus.Failed;
            var code = verified
                ? DiagnosticResultCode.Succeeded
                : applied ? DiagnosticResultCode.AttemptedButUnverified : DiagnosticResultCode.Failed;
            var message = verified
                ? changed
                    ? "Auto facing changed player direction via " + method + "."
                    : "Auto facing verified player direction via " + method + "."
                : "Auto facing attempted to change direction, but verification failed: " + TerrariaInputCompat.LastInputCompatError;

            RecordAutoFacingEvent(execution, status, code, message, requestedDirection, beforeDirection, afterDirection, method, verified, changed, itemCheckOverrideArmed, directionSource);
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            return InputActionExecutionStepResult.Complete(status, message);
        }

        private static InputActionExecutionStepResult StartAutoHarvestSustainedUse(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForCombatInput(snapshot))
            {
                return CompleteWithCode(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Auto harvest sustained use did not start: world input is blocked.");
            }

            string message;
            // World automation starts bridge ownership only; ItemCheck applies and
            // restores the scoped input override.
            if (!AutoHarvestSustainedUseBridge.TryBegin(
                execution.Request.RequestId,
                execution.Request.SourceFeatureId,
                GetMetadataString(execution, "Scenario", "WorldAutomation.AutoHarvest"),
                execution.Request.Timeout,
                out message))
            {
                return CompleteWithCode(execution, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, message);
            }

            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("Auto harvest sustained use started.");
        }

        private static InputActionExecutionStepResult StartAutoMiningSustainedUse(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForCombatInput(snapshot))
            {
                return CompleteWithCode(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Auto mining sustained use did not start: world input is blocked.");
            }

            string message;
            // Mining bridge ownership starts here; ItemCheck remains the only place
            // that applies and restores the held-pickaxe input override.
            if (!AutoMiningSustainedUseBridge.TryBegin(
                execution.Request.RequestId,
                execution.Request.SourceFeatureId,
                GetMetadataString(execution, "Scenario", "WorldAutomation.AutoMining"),
                execution.Request.Timeout,
                out message))
            {
                return CompleteWithCode(execution, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, message);
            }

            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("Auto mining sustained use started.");
        }

        private static InputActionExecutionStepResult StartPhasebladeQuickSwitch(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForCombatInput(snapshot))
            {
                return CompleteWithCode(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Phaseblade quick switch did not start: world input is blocked.");
            }

            var intervalTicks = GetMetadataInt(execution, "PhasebladeQuickSwitchIntervalTicks", CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks);
            var allowCombatAim = string.Equals(GetMetadataString(execution, "AllowCombatAim", "true"), "true", StringComparison.OrdinalIgnoreCase);
            string message;
            // The phaseblade bridge owns only lifecycle and scoped ItemCheck
            // press/release; later right-click gates may decide when to enqueue it.
            if (!PhasebladeQuickSwitchBridge.TryBegin(
                execution.Request.RequestId,
                execution.Request.SourceFeatureId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.CombatPhasebladeQuickSwitch),
                intervalTicks,
                allowCombatAim,
                execution.Request.Timeout,
                out message))
            {
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, message);
            }

            execution.State["PhasebladeQuickSwitchIntervalTicks"] = CombatPhasebladeQuickSwitchSettings.NormalizeIntervalTicks(intervalTicks).ToString(CultureInfo.InvariantCulture);
            execution.State["PhasebladeQuickSwitchAllowCombatAim"] = allowCombatAim ? "true" : "false";
            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("Phaseblade quick switch bridge started.");
        }

        private static InputActionExecutionStepResult StartAutoCaptureCritterSustainedUse(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForCombatInput(snapshot))
            {
                return CompleteWithCode(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Auto capture critter sustained use did not start: world input is blocked.");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use did not start: local player unavailable.");
            }

            var targetSlot = GetMetadataInt(execution, ActionMetadataKeys.TargetSlot, -1);
            if (!TerrariaInputCompat.IsSupportedItemUseSlot(targetSlot))
            {
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Auto capture critter sustained use did not start: bug net slot was invalid.");
            }

            int originalSelectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out originalSelectedSlot))
            {
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use did not start: selected slot unavailable: " + TerrariaInputCompat.LastInputCompatError);
            }

            int originalDirection;
            var originalDirectionCaptured = TerrariaInputCompat.TryReadPlayerDirection(player, out originalDirection);
            var mode = GetMetadataString(execution, "AutoCaptureCritterMode", string.Empty);
            var restoreOriginalState = string.Equals(mode, "Auto", StringComparison.OrdinalIgnoreCase);
            execution.State[AutoCaptureTargetSlotState] = targetSlot.ToString(CultureInfo.InvariantCulture);
            execution.State[AutoCaptureOriginalSelectedSlotState] = originalSelectedSlot.ToString(CultureInfo.InvariantCulture);
            execution.State[AutoCaptureOriginalDirectionState] = originalDirection.ToString(CultureInfo.InvariantCulture);
            execution.State[AutoCaptureOriginalDirectionCapturedState] = originalDirectionCaptured ? "true" : "false";
            execution.State[AutoCaptureRestoreOriginalState] = restoreOriginalState ? "true" : "false";

            if (targetSlot != 58 && originalSelectedSlot != targetSlot)
            {
                bool selectedImmediately;
                if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, targetSlot, out selectedImmediately))
                {
                    return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use failed to request bug net slot: " + TerrariaInputCompat.LastInputCompatError);
                }

                execution.State[AutoCaptureSlotSwitchMethodState] = TerrariaInputCompat.LastSelectionMethod ?? string.Empty;
                if (!selectedImmediately)
                {
                    execution.State[AutoCapturePendingBugNetSelectionState] = "true";
                    execution.State[AutoCaptureSlotSelectionWaitTicksState] = "0";
                    SetResultCode(execution, DiagnosticResultCode.Queued);
                    return InputActionExecutionStepResult.Running("Auto capture critter requested bug net slot; waiting for Terraria selection update.");
                }

                int selectedAfterRequest;
                if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedAfterRequest))
                {
                    TryRestoreAutoCaptureOriginalSelection(execution, player);
                    return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use cannot verify bug net slot: " + TerrariaInputCompat.LastInputCompatError);
                }

                if (selectedAfterRequest != targetSlot)
                {
                    TryRestoreAutoCaptureOriginalSelection(execution, player);
                    return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Auto capture critter sustained use failed to select bug net slot. selectedSlot=" + selectedAfterRequest.ToString(CultureInfo.InvariantCulture) + ", targetSlot=" + targetSlot.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }

            return BeginAutoCaptureCritterBridgeAfterSelection(
                execution,
                player,
                targetSlot,
                originalSelectedSlot,
                originalDirectionCaptured,
                originalDirection);
        }

        private static InputActionExecutionStepResult BeginAutoCaptureCritterBridgeAfterSelection(
            InputActionExecution execution,
            object player,
            int targetSlot,
            int originalSelectedSlot,
            bool originalDirectionCaptured,
            int originalDirection)
        {
            if (player == null)
            {
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use did not start: local player unavailable.");
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                TryRestoreAutoCaptureOriginalSelection(execution, player);
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use cannot read selected slot before bridge start: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (targetSlot != 58 && selectedSlot != targetSlot)
            {
                TryRestoreAutoCaptureOriginalSelection(execution, player);
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Auto capture critter sustained use did not start: bug net slot was not selected. selectedSlot=" + selectedSlot.ToString(CultureInfo.InvariantCulture) + ", targetSlot=" + targetSlot.ToString(CultureInfo.InvariantCulture) + ".");
            }

            string message;
            // Capture mode may preselect a bug net, but the sustained bridge still owns
            // the scoped ItemCheck input and restore contract.
            if (!AutoCaptureCritterSustainedUseBridge.TryBegin(
                execution.Request.RequestId,
                execution.Request.SourceFeatureId,
                GetMetadataString(execution, "Scenario", "WorldAutomation.AutoCaptureCritter"),
                execution.Request.Timeout,
                originalSelectedSlot,
                originalDirectionCaptured,
                originalDirection,
                out message))
            {
                TryRestoreAutoCaptureOriginalSelection(execution, player);
                return CompleteWithCode(execution, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, message);
            }

            execution.State[AutoCapturePendingBugNetSelectionState] = "false";
            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("Auto capture critter sustained use started.");
        }

        public override InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var mode = GetMetadataString(execution, "RawInputMode", string.Empty);
            if (string.Equals(mode, ModeAutoHarvestSustainedUse, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateAutoHarvestSustainedUse(execution, snapshot);
            }

            if (string.Equals(mode, ModeAutoMiningSustainedUse, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateAutoMiningSustainedUse(execution, snapshot);
            }

            if (string.Equals(mode, ModeAutoCaptureCritterSustainedUse, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateAutoCaptureCritterSustainedUse(execution, snapshot);
            }

            if (string.Equals(mode, ModePhasebladeQuickSwitch, StringComparison.OrdinalIgnoreCase))
            {
                return UpdatePhasebladeQuickSwitch(execution, snapshot);
            }

            var blocked = IsBlockedForCombatInput(snapshot);
            var pulse = UseItemPulseBridge.Update(
                execution.Request.RequestId,
                blocked,
                blocked ? "Magic string clicker stopped: world input is blocked." : string.Empty);

            if (pulse != null && pulse.Status == InputActionStatus.Running)
            {
                return InputActionExecutionStepResult.Running(pulse.Message);
            }

            if (pulse == null)
            {
                SetResultCode(execution, DiagnosticResultCode.Failed);
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, "Magic string clicker pulse state was lost.");
            }

            RecordCompletionEvent(execution, pulse);
            SetResultCode(execution, pulse.ResultCode);
            MarkActionEventRecorded(execution);
            return InputActionExecutionStepResult.Complete(pulse.Status, pulse.Message);
        }

        private static InputActionExecutionStepResult UpdateAutoHarvestSustainedUse(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var blocked = IsBlockedForCombatInput(snapshot);
            var use = AutoHarvestSustainedUseBridge.Update(
                execution.Request.RequestId,
                blocked,
                blocked ? "Auto harvest sustained use stopped: world input is blocked." : string.Empty);

            if (use != null && use.Status == InputActionStatus.Running)
            {
                return InputActionExecutionStepResult.Running(use.Message);
            }

            if (use == null)
            {
                SetResultCode(execution, DiagnosticResultCode.Failed);
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, "Auto harvest sustained use state was lost.");
            }

            RecordAutoHarvestSustainedCompletionEvent(execution, use);
            SetResultCode(execution, use.ResultCode);
            MarkActionEventRecorded(execution);
            return InputActionExecutionStepResult.Complete(use.Status, use.Message);
        }

        private static InputActionExecutionStepResult UpdateAutoMiningSustainedUse(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var blocked = IsBlockedForCombatInput(snapshot);
            var use = AutoMiningSustainedUseBridge.Update(
                execution.Request.RequestId,
                blocked,
                blocked ? "Auto mining sustained use stopped: world input is blocked." : string.Empty);

            if (use != null && use.Status == InputActionStatus.Running)
            {
                return InputActionExecutionStepResult.Running(use.Message);
            }

            if (use == null)
            {
                SetResultCode(execution, DiagnosticResultCode.Failed);
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, "Auto mining sustained use state was lost.");
            }

            RecordAutoMiningSustainedCompletionEvent(execution, use);
            SetResultCode(execution, use.ResultCode);
            MarkActionEventRecorded(execution);
            return InputActionExecutionStepResult.Complete(use.Status, use.Message);
        }

        private static InputActionExecutionStepResult UpdatePhasebladeQuickSwitch(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var blocked = IsBlockedForCombatInput(snapshot);
            var use = PhasebladeQuickSwitchBridge.Update(
                execution.Request.RequestId,
                blocked,
                blocked ? "Phaseblade quick switch stopped: world input is blocked." : string.Empty);

            if (use != null && use.Status == InputActionStatus.Running)
            {
                return InputActionExecutionStepResult.Running(use.Message);
            }

            if (use == null)
            {
                SetResultCode(execution, DiagnosticResultCode.Failed);
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, "Phaseblade quick switch state was lost.");
            }

            RecordPhasebladeQuickSwitchCompletionEvent(execution, use);
            SetResultCode(execution, use.ResultCode);
            MarkActionEventRecorded(execution);
            return InputActionExecutionStepResult.Complete(use.Status, use.Message);
        }

        private static InputActionExecutionStepResult UpdateAutoCaptureCritterSustainedUse(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (string.Equals(GetExecutionStateString(execution, AutoCapturePendingBugNetSelectionState, string.Empty), "true", StringComparison.OrdinalIgnoreCase))
            {
                return WaitForAutoCaptureBugNetSelectionAndBeginBridge(execution, snapshot);
            }

            var blocked = IsBlockedForCombatInput(snapshot);
            var use = AutoCaptureCritterSustainedUseBridge.Update(
                execution.Request.RequestId,
                blocked,
                blocked ? "Auto capture critter sustained use stopped: world input is blocked." : string.Empty);

            if (use != null && use.Status == InputActionStatus.Running)
            {
                return InputActionExecutionStepResult.Running(use.Message);
            }

            if (use == null)
            {
                SetResultCode(execution, DiagnosticResultCode.Failed);
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, "Auto capture critter sustained use state was lost.");
            }

            RecordAutoCaptureCritterSustainedCompletionEvent(execution, use);
            SetResultCode(execution, use.ResultCode);
            MarkActionEventRecorded(execution);
            return InputActionExecutionStepResult.Complete(use.Status, use.Message);
        }

        private static InputActionExecutionStepResult WaitForAutoCaptureBugNetSelectionAndBeginBridge(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForCombatInput(snapshot))
            {
                TryRestoreAutoCaptureOriginalSelection(execution);
                return CompleteWithCode(execution, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Auto capture critter sustained use stopped while waiting for bug net slot: world input is blocked.");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                TryRestoreAutoCaptureOriginalSelection(execution);
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use stopped while waiting for bug net slot: local player unavailable.");
            }

            var targetSlot = GetExecutionStateInt(execution, AutoCaptureTargetSlotState, GetMetadataInt(execution, ActionMetadataKeys.TargetSlot, -1));
            var originalSelectedSlot = GetExecutionStateInt(execution, AutoCaptureOriginalSelectedSlotState, -1);
            var originalDirection = GetExecutionStateInt(execution, AutoCaptureOriginalDirectionState, 0);
            var originalDirectionCaptured = GetExecutionStateBool(execution, AutoCaptureOriginalDirectionCapturedState, false);

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                TryRestoreAutoCaptureOriginalSelection(execution, player);
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use cannot read selected slot while waiting: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (selectedSlot == targetSlot)
            {
                execution.State[AutoCapturePendingBugNetSelectionState] = "false";
                return BeginAutoCaptureCritterBridgeAfterSelection(
                    execution,
                    player,
                    targetSlot,
                    originalSelectedSlot,
                    originalDirectionCaptured,
                    originalDirection);
            }

            var waitTicks = GetExecutionStateInt(execution, AutoCaptureSlotSelectionWaitTicksState, 0) + 1;
            execution.State[AutoCaptureSlotSelectionWaitTicksState] = waitTicks.ToString(CultureInfo.InvariantCulture);
            if (waitTicks > 45)
            {
                TryRestoreAutoCaptureOriginalSelection(execution, player);
                return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Auto capture critter sustained use failed to select bug net slot. selectedSlot=" + selectedSlot.ToString(CultureInfo.InvariantCulture) + ", targetSlot=" + targetSlot.ToString(CultureInfo.InvariantCulture) + ", method=" + GetExecutionStateString(execution, AutoCaptureSlotSwitchMethodState, string.Empty) + ".");
            }

            if (waitTicks % 4 == 0)
            {
                bool selectedImmediately;
                if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, targetSlot, out selectedImmediately))
                {
                    TryRestoreAutoCaptureOriginalSelection(execution, player);
                    return CompleteWithCode(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use failed to request bug net slot while waiting: " + TerrariaInputCompat.LastInputCompatError);
                }

                execution.State[AutoCaptureSlotSwitchMethodState] = TerrariaInputCompat.LastSelectionMethod ?? string.Empty;
            }

            return InputActionExecutionStepResult.Running("Auto capture critter waiting for bug net slot selection.");
        }

        public override InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
        {
            var defaultReason = "Magic string clicker cancelled.";
            if (execution != null && execution.Request != null)
            {
                var mode = GetMetadataString(execution, "RawInputMode", string.Empty);
                if (string.Equals(mode, ModeAutoHarvestSustainedUse, StringComparison.OrdinalIgnoreCase))
                {
                    defaultReason = "Auto harvest sustained use cancelled.";
                    AutoHarvestSustainedUseBridge.Cancel(execution.Request.RequestId, reason ?? defaultReason);
                }
                else if (string.Equals(mode, ModeAutoMiningSustainedUse, StringComparison.OrdinalIgnoreCase))
                {
                    defaultReason = "Auto mining sustained use cancelled.";
                    AutoMiningSustainedUseBridge.Cancel(execution.Request.RequestId, reason ?? defaultReason);
                }
                else if (string.Equals(mode, ModeAutoCaptureCritterSustainedUse, StringComparison.OrdinalIgnoreCase))
                {
                    defaultReason = "Auto capture critter sustained use cancelled.";
                    AutoCaptureCritterSustainedUseBridge.Cancel(execution.Request.RequestId, reason ?? defaultReason);
                    // Cancel must release bridge ownership and restore any preselected
                    // bug net slot before the queue records the terminal cancellation.
                    TryRestoreAutoCaptureOriginalSelection(execution);
                }
                else if (string.Equals(mode, ModePhasebladeQuickSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    defaultReason = "Phaseblade quick switch cancelled.";
                    PhasebladeQuickSwitchBridge.Cancel(execution.Request.RequestId, reason ?? defaultReason);
                }
                else
                {
                    UseItemPulseBridge.Cancel(execution.Request.RequestId, reason ?? defaultReason);
                }
            }

            SetResultCode(execution, DiagnosticResultCode.Failed);
            return InputActionExecutionStepResult.Complete(InputActionStatus.Cancelled, reason ?? defaultReason);
        }

        private static void TryRestoreAutoCaptureOriginalSelection(InputActionExecution execution)
        {
            object player;
            if (TerrariaInputCompat.TryGetLocalPlayer(out player) && player != null)
            {
                TryRestoreAutoCaptureOriginalSelection(execution, player);
            }
        }

        private static void TryRestoreAutoCaptureOriginalSelection(InputActionExecution execution, object player)
        {
            if (execution == null || player == null)
            {
                return;
            }

            if (!GetExecutionStateBool(execution, AutoCaptureRestoreOriginalState, false))
            {
                return;
            }

            var originalSelectedSlot = GetExecutionStateInt(execution, AutoCaptureOriginalSelectedSlotState, -1);
            if (TerrariaInputCompat.IsSupportedItemUseSlot(originalSelectedSlot))
            {
                TerrariaInputCompat.TrySelectInventorySlot(player, originalSelectedSlot);
            }
        }

        private static string GetExecutionStateString(InputActionExecution execution, string key, string fallback)
        {
            if (execution == null || execution.State == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            string value;
            return execution.State.TryGetValue(key, out value) && value != null ? value : fallback;
        }

        private static int GetExecutionStateInt(InputActionExecution execution, string key, int fallback)
        {
            var value = GetExecutionStateString(execution, key, string.Empty);
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static bool GetExecutionStateBool(InputActionExecution execution, string key, bool fallback)
        {
            var value = GetExecutionStateString(execution, key, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void RecordCompletionEvent(InputActionExecution execution, UseItemPulseBridgeSnapshot pulse)
        {
            if (execution == null || execution.Request == null || pulse == null)
            {
                return;
            }

            var beforeJson = "{" +
                             "\"selectedSlot\":" + SlotRaw(pulse.SelectedSlot) + "," +
                             "\"selectedSlotDisplay\":" + SlotDisplayRaw(pulse.SelectedSlot) + "," +
                             "\"itemType\":" + IntRaw(pulse.ExpectedItemType) + "," +
                             "\"itemName\":\"" + EscapeJson(pulse.ItemName) + "\"," +
                             "\"pulseIntervalTicks\":" + IntRaw(pulse.IntervalTicks) +
                             "}";
            var afterJson = "{" +
                            "\"itemCheckCount\":" + IntRaw(pulse.ItemCheckCount) + "," +
                            "\"pressPulseCount\":" + IntRaw(pulse.PressPulseCount) + "," +
                            "\"releasePulseCount\":" + IntRaw(pulse.ReleasePulseCount) + "," +
                            "\"lastAppliedPress\":" + BoolRaw(pulse.LastAppliedPress) + "," +
                            "\"lastAppliedTick\":" + (pulse.LastAppliedTick == long.MinValue ? "null" : pulse.LastAppliedTick.ToString(CultureInfo.InvariantCulture)) +
                            "}";
            var verificationJson = "{" +
                                   "\"rawInputMode\":\"MagicStringClicker\"," +
                                   "\"requireUseItemHeld\":" + BoolRaw(pulse.RequireUseItemHeld) + "," +
                                   "\"allowCombatAim\":" + BoolRaw(pulse.AllowCombatAim) + "," +
                                   "\"magicStringPulseApplied\":" + BoolRaw(pulse.ItemCheckCount > 0) + "," +
                                   "\"pressPulseCount\":" + IntRaw(pulse.PressPulseCount) + "," +
                                   "\"releasePulseCount\":" + IntRaw(pulse.ReleasePulseCount) + "," +
                                   "\"pulseIntervalTicks\":" + IntRaw(pulse.IntervalTicks) +
                                   "}";

            DiagnosticActionRecorder.RecordCustomEvent(
                execution.Request.RequestId,
                GetMetadata(execution, "Scenario", "Combat.MagicStringClicker"),
                InputActionKind.RawInput.ToString(),
                GetMetadata(execution, "SourceHotkey", string.Empty),
                pulse.Status.ToString(),
                pulse.ResultCode.ToString(),
                pulse.Message,
                pulse.DurationMs,
                beforeJson,
                afterJson,
                verificationJson,
                GetMetadata(execution, "SourceKind", string.Empty),
                GetMetadata(execution, "SourceUi", string.Empty),
                GetMetadata(execution, "ButtonId", string.Empty),
                GetMetadata(execution, "ButtonLabel", string.Empty));
        }

        private static void RecordAutoHarvestSustainedCompletionEvent(InputActionExecution execution, AutoHarvestSustainedUseBridgeSnapshot use)
        {
            if (execution == null || execution.Request == null || use == null)
            {
                return;
            }

            var beforeJson = "{" +
                             "\"selectedSlot\":" + SlotRaw(use.ToolSlot) + "," +
                             "\"selectedSlotDisplay\":" + SlotDisplayRaw(use.ToolSlot) + "," +
                             "\"itemType\":" + IntRaw(use.ToolItemType) + "," +
                             "\"itemName\":\"" + EscapeJson(use.ToolItemName) + "\"," +
                             "\"targetTile\":" + BuildTileJson(use.TileX, use.TileY) + "," +
                             "\"seedItemType\":" + IntRaw(use.SeedItemType) +
                             "}";
            var afterJson = "{" +
                            "\"itemCheckApplyCount\":" + IntRaw(use.ApplyCount) + "," +
                            "\"targetRefreshCount\":" + IntRaw(use.TargetRefreshCount) + "," +
                            "\"lastAppliedTick\":" + (use.LastAppliedTick == long.MinValue ? "null" : use.LastAppliedTick.ToString(CultureInfo.InvariantCulture)) +
                            "}";
            var verificationJson = "{" +
                                   "\"rawInputMode\":\"AutoHarvestSustainedUse\"," +
                                   "\"autoHarvestSustainedUseApplied\":" + BoolRaw(use.ApplyCount > 0) + "," +
                                   "\"targetWorld\":" + BuildPointJson(use.WorldX, use.WorldY) +
                                   "}";

            DiagnosticActionRecorder.RecordCustomEvent(
                execution.Request.RequestId,
                GetMetadata(execution, "Scenario", "WorldAutomation.AutoHarvest"),
                InputActionKind.RawInput.ToString(),
                GetMetadata(execution, "SourceHotkey", string.Empty),
                use.Status.ToString(),
                use.ResultCode.ToString(),
                use.Message,
                use.DurationMs,
                beforeJson,
                afterJson,
                verificationJson,
                GetMetadata(execution, "SourceKind", string.Empty),
                GetMetadata(execution, "SourceUi", string.Empty),
                GetMetadata(execution, "ButtonId", string.Empty),
                GetMetadata(execution, "ButtonLabel", string.Empty));
        }

        private static void RecordAutoMiningSustainedCompletionEvent(InputActionExecution execution, AutoMiningSustainedUseBridgeSnapshot use)
        {
            if (execution == null || execution.Request == null || use == null)
            {
                return;
            }

            var beforeJson = "{" +
                             "\"selectedSlot\":" + SlotRaw(use.PickSlot) + "," +
                             "\"selectedSlotDisplay\":" + SlotDisplayRaw(use.PickSlot) + "," +
                             "\"itemType\":" + IntRaw(use.PickItemType) + "," +
                             "\"itemName\":\"" + EscapeJson(use.PickItemName) + "\"," +
                             "\"targetTile\":" + BuildTileJson(use.TileX, use.TileY) + "," +
                             "\"tileType\":" + IntRaw(use.TileType) +
                             "}";
            var afterJson = "{" +
                            "\"itemCheckApplyCount\":" + IntRaw(use.ApplyCount) + "," +
                            "\"targetRefreshCount\":" + IntRaw(use.TargetRefreshCount) + "," +
                            "\"lastAppliedTick\":" + (use.LastAppliedTick == long.MinValue ? "null" : use.LastAppliedTick.ToString(CultureInfo.InvariantCulture)) +
                            "}";
            var verificationJson = "{" +
                                   "\"rawInputMode\":\"AutoMiningSustainedUse\"," +
                                   "\"autoMiningSustainedUseApplied\":" + BoolRaw(use.ApplyCount > 0) + "," +
                                   "\"sourceMode\":\"" + EscapeJson(use.SourceMode) + "\"," +
                                   "\"targetWorld\":" + BuildPointJson(use.WorldX, use.WorldY) +
                                   "}";

            DiagnosticActionRecorder.RecordCustomEvent(
                execution.Request.RequestId,
                GetMetadata(execution, "Scenario", "WorldAutomation.AutoMining"),
                InputActionKind.RawInput.ToString(),
                GetMetadata(execution, "SourceHotkey", string.Empty),
                use.Status.ToString(),
                use.ResultCode.ToString(),
                use.Message,
                use.DurationMs,
                beforeJson,
                afterJson,
                verificationJson,
                GetMetadata(execution, "SourceKind", string.Empty),
                GetMetadata(execution, "SourceUi", string.Empty),
                GetMetadata(execution, "ButtonId", string.Empty),
                GetMetadata(execution, "ButtonLabel", string.Empty));
        }

        private static void RecordPhasebladeQuickSwitchCompletionEvent(InputActionExecution execution, PhasebladeQuickSwitchBridgeSnapshot use)
        {
            if (execution == null || execution.Request == null || use == null)
            {
                return;
            }

            var beforeJson = "{" +
                             "\"selectedSlot\":" + SlotRaw(use.LastSelectedSlot) + "," +
                             "\"selectedSlotDisplay\":" + SlotDisplayRaw(use.LastSelectedSlot) + "," +
                             "\"itemType\":" + IntRaw(use.LastItemType) + "," +
                             "\"intervalTicks\":" + IntRaw(use.IntervalTicks) + "," +
                             "\"eligibleSlotCount\":" + IntRaw(GetMetadataInt(execution, "PhasebladeQuickSwitchEligibleSlotCount", 0)) + "," +
                             "\"nextSlot\":" + SlotRaw(GetMetadataInt(execution, "PhasebladeQuickSwitchNextSlot", -1)) +
                             "}";
            var afterJson = "{" +
                            "\"itemCheckApplyCount\":" + IntRaw(use.ApplyCount) + "," +
                            "\"pressCount\":" + IntRaw(use.PressCount) + "," +
                            "\"releaseCount\":" + IntRaw(use.ReleaseCount) + "," +
                            "\"switchRequestCount\":" + IntRaw(use.SwitchRequestCount) + "," +
                            "\"restoreSuccessCount\":" + IntRaw(use.RestoreSuccessCount) + "," +
                            "\"restoreOk\":" + BoolRaw(use.LastRestoreSucceeded) + "," +
                            "\"lastTargetSlot\":" + SlotRaw(use.LastTargetSlot) + "," +
                            "\"lastTargetSlotDisplay\":" + SlotDisplayRaw(use.LastTargetSlot) + "," +
                            "\"lastAppliedTick\":" + (use.LastAppliedTick == long.MinValue ? "null" : use.LastAppliedTick.ToString(CultureInfo.InvariantCulture)) +
                            "}";
            var verificationJson = "{" +
                                   "\"rawInputMode\":\"PhasebladeQuickSwitch\"," +
                                   "\"phasebladeQuickSwitchApplied\":" + BoolRaw(use.ApplyCount > 0) + "," +
                                   "\"allowCombatAim\":" + BoolRaw(use.AllowCombatAim) + "," +
                                   "\"lastDecisionState\":\"" + EscapeJson(use.LastDecisionState) + "\"," +
                                   "\"lastDecisionReason\":\"" + EscapeJson(use.LastDecisionReason) + "\"," +
                                   "\"lastSwitchMethod\":\"" + EscapeJson(use.LastSwitchMethod) + "\"" +
                                   "}";

            DiagnosticActionRecorder.RecordCustomEvent(
                execution.Request.RequestId,
                GetMetadata(execution, ActionMetadataKeys.Scenario, ScenarioNames.CombatPhasebladeQuickSwitch),
                InputActionKind.RawInput.ToString(),
                GetMetadata(execution, "SourceHotkey", string.Empty),
                use.Status.ToString(),
                use.ResultCode.ToString(),
                use.Message,
                use.DurationMs,
                beforeJson,
                afterJson,
                verificationJson,
                GetMetadata(execution, "SourceKind", string.Empty),
                GetMetadata(execution, "SourceUi", string.Empty),
                GetMetadata(execution, "ButtonId", string.Empty),
                GetMetadata(execution, "ButtonLabel", string.Empty));
        }

        private static void RecordAutoCaptureCritterSustainedCompletionEvent(InputActionExecution execution, AutoCaptureCritterSustainedUseBridgeSnapshot use)
        {
            if (execution == null || execution.Request == null || use == null)
            {
                return;
            }

            var beforeJson = "{" +
                             "\"selectedSlot\":" + SlotRaw(use.BugNetSlot) + "," +
                             "\"selectedSlotDisplay\":" + SlotDisplayRaw(use.BugNetSlot) + "," +
                             "\"itemType\":" + IntRaw(use.BugNetItemType) + "," +
                             "\"itemName\":\"" + EscapeJson(use.BugNetItemName) + "\"," +
                             "\"targetNpcIndex\":" + NullableNonNegativeRaw(use.NpcIndex) + "," +
                             "\"targetNpcType\":" + NullablePositiveRaw(use.NpcType) + "," +
                             "\"catchItem\":" + NullablePositiveRaw(use.CatchItem) +
                             "}";
            var afterJson = "{" +
                            "\"itemCheckApplyCount\":" + IntRaw(use.ApplyCount) + "," +
                            "\"targetRefreshCount\":" + IntRaw(use.TargetRefreshCount) + "," +
                            "\"lastAppliedTick\":" + (use.LastAppliedTick == long.MinValue ? "null" : use.LastAppliedTick.ToString(CultureInfo.InvariantCulture)) + "," +
                            "\"restoreOriginalStateOnComplete\":" + BoolRaw(use.RestoreOriginalStateOnComplete) + "," +
                            "\"originalSelectedSlot\":" + SlotRaw(use.OriginalSelectedSlot) + "," +
                            "\"originalSelectedSlotDisplay\":" + SlotDisplayRaw(use.OriginalSelectedSlot) +
                            "}";
            var verificationJson = "{" +
                                   "\"rawInputMode\":\"AutoCaptureCritterSustainedUse\"," +
                                   "\"autoCaptureCritterMode\":\"" + EscapeJson(GetMetadata(execution, "AutoCaptureCritterMode", string.Empty)) + "\"," +
                                   "\"autoCaptureSustainedUseApplied\":" + BoolRaw(use.ApplyCount > 0) + "," +
                                   "\"targetWorld\":" + BuildPointJson(use.WorldX, use.WorldY) + "," +
                                   "\"temporaryDirection\":" + NullableDirectionRaw(use.Direction) + "," +
                                   "\"originalDirection\":" + NullableDirectionRaw(use.OriginalDirection) + "," +
                                   "\"originalDirectionCaptured\":" + BoolRaw(use.OriginalDirectionCaptured) +
                                   "}";

            DiagnosticActionRecorder.RecordCustomEvent(
                execution.Request.RequestId,
                GetMetadata(execution, "Scenario", "WorldAutomation.AutoCaptureCritter"),
                InputActionKind.RawInput.ToString(),
                GetMetadata(execution, "SourceHotkey", string.Empty),
                use.Status.ToString(),
                use.ResultCode.ToString(),
                use.Message,
                use.DurationMs,
                beforeJson,
                afterJson,
                verificationJson,
                GetMetadata(execution, "SourceKind", string.Empty),
                GetMetadata(execution, "SourceUi", string.Empty),
                GetMetadata(execution, "ButtonId", string.Empty),
                GetMetadata(execution, "ButtonLabel", string.Empty));
        }

        private static void RecordAutoFacingEvent(
            InputActionExecution execution,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            int requestedDirection,
            int beforeDirection,
            int afterDirection,
            string method,
            bool verified,
            bool changed,
            bool itemCheckOverrideArmed,
            string directionSource)
        {
            if (execution == null || execution.Request == null)
            {
                return;
            }

            var beforeJson = "{" +
                             "\"requestedDirection\":" + IntRaw(requestedDirection >= 0 ? 1 : -1) + "," +
                             "\"directionBefore\":" + NullableDirectionRaw(beforeDirection) + "," +
                             "\"selectedSlot\":" + SlotRaw(GetMetadataInt(execution, "AutoFacingSelectedSlot", -1)) + "," +
                             "\"selectedSlotDisplay\":" + SlotDisplayRaw(GetMetadataInt(execution, "AutoFacingSelectedSlot", -1)) + "," +
                             "\"itemType\":" + IntRaw(GetMetadataInt(execution, "AutoFacingItemType", 0)) + "," +
                             "\"itemName\":\"" + EscapeJson(GetMetadataString(execution, "AutoFacingItemName", string.Empty)) + "\"" +
                             "}";
            var afterJson = "{" +
                            "\"directionAfter\":" + NullableDirectionRaw(afterDirection) + "," +
                            "\"directionChanged\":" + BoolRaw(changed) + "," +
                            "\"applyMethod\":\"" + EscapeJson(method) + "\"" +
                            "}";
            var verificationJson = "{" +
                                   "\"rawInputMode\":\"AutoFacing\"," +
                                   "\"autoFacingApplied\":" + BoolRaw(verified) + "," +
                                   "\"directionVerified\":" + BoolRaw(verified) + "," +
                                   "\"itemCheckDirectionOverrideArmed\":" + BoolRaw(itemCheckOverrideArmed) + "," +
                                   "\"directionSource\":\"" + EscapeJson(directionSource ?? string.Empty) + "\"," +
                                   "\"targetWhoAmI\":" + NullableNonNegativeRaw(GetMetadataInt(execution, "AutoFacingTargetWhoAmI", -1)) + "," +
                                   "\"targetType\":" + NullablePositiveRaw(GetMetadataInt(execution, "AutoFacingTargetType", 0)) + "," +
                                   "\"targetName\":\"" + EscapeJson(GetMetadataString(execution, "AutoFacingTargetName", string.Empty)) + "\"," +
                                   "\"targetCenter\":" + BuildPointJson(GetMetadataFloat(execution, "AutoFacingTargetCenterX", 0f), GetMetadataFloat(execution, "AutoFacingTargetCenterY", 0f)) + "," +
                                   "\"playerCenter\":" + BuildPointJson(GetMetadataFloat(execution, "AutoFacingPlayerCenterX", 0f), GetMetadataFloat(execution, "AutoFacingPlayerCenterY", 0f)) + "," +
                                   "\"selectionSource\":\"" + EscapeJson(GetMetadataString(execution, "AutoFacingSelectionSource", string.Empty)) + "\"," +
                                   "\"selectionRadiusTiles\":" + IntRaw(GetMetadataInt(execution, "AutoFacingSelectionRadiusTiles", 0)) +
                                   "}";

            DiagnosticActionRecorder.RecordCustomEvent(
                execution.Request.RequestId,
                GetMetadata(execution, "Scenario", "Combat.AutoFacing"),
                InputActionKind.RawInput.ToString(),
                GetMetadata(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                0,
                beforeJson,
                afterJson,
                verificationJson,
                GetMetadata(execution, "SourceKind", string.Empty),
                GetMetadata(execution, "SourceUi", string.Empty),
                GetMetadata(execution, "ButtonId", string.Empty),
                GetMetadata(execution, "ButtonLabel", string.Empty));
        }

        private static string GetMetadata(InputActionExecution execution, string key, string fallback)
        {
            if (execution == null || execution.Request == null || execution.Request.Metadata == null)
            {
                return fallback;
            }

            string value;
            return execution.Request.Metadata.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string SlotRaw(int slot)
        {
            return slot >= 0 && slot <= 9 ? slot.ToString(CultureInfo.InvariantCulture) : "null";
        }

        private static string SlotDisplayRaw(int slot)
        {
            return slot >= 0 && slot <= 9 ? (slot + 1).ToString(CultureInfo.InvariantCulture) : "null";
        }

        private static string NullableDirectionRaw(int direction)
        {
            return direction == 0 ? "null" : IntRaw(direction >= 0 ? 1 : -1);
        }

        private static string NullableNonNegativeRaw(int value)
        {
            return value >= 0 ? IntRaw(value) : "null";
        }

        private static string NullablePositiveRaw(int value)
        {
            return value > 0 ? IntRaw(value) : "null";
        }

        private static string BuildPointJson(float x, float y)
        {
            return "{" +
                   "\"x\":" + FloatRaw(x) + "," +
                   "\"y\":" + FloatRaw(y) +
                   "}";
        }

        private static string BuildTileJson(int x, int y)
        {
            return "{" +
                   "\"x\":" + IntRaw(x) + "," +
                   "\"y\":" + IntRaw(y) +
                   "}";
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
