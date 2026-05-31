using System;
using System.Globalization;
using System.Text;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.UI;

namespace JueMingZ.Actions.Executors
{
    public sealed class UseHotbarItemActionExecutor : InputActionExecutorBase
    {
        private const string StageWaitMouseRelease = "WaitMouseReleaseBeforeSwitch";
        private const string StageSwitchToTargetSlot = "SwitchToTargetSlot";
        private const string StageVerifyTargetSlot = "VerifyTargetSlotSelected";
        private const string StageStabilizeTargetSlot = "StabilizeTargetSlot";
        private const string StageEnqueueBridge = "EnqueueItemUseBridge";
        private const string StageWaitBridgeResult = "WaitBridgeResult";
        private const string StageRestoreOriginalSlot = "RestoreOriginalSlot";
        private const string StageVerifyOriginalSlotRestored = "VerifyOriginalSlotRestored";
        private const int MaxRestoreAttempts = 3;
        private const int MaxFinalUseIdleWaitTicks = 90;

        public override InputActionKind Kind { get { return InputActionKind.UseHotbarItem; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForWorldInput(snapshot))
            {
                return CompleteDetailed(
                    execution,
                    InputActionStatus.BlockedByUi,
                    DiagnosticResultCode.BlockedByUi,
                    "UseHotbarItem blocked: not in world or a blocking UI is open.");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, TerrariaInputCompat.LastInputCompatError);
            }

            int originalSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out originalSlot))
            {
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "selected item getter unavailable: " + TerrariaInputCompat.LastInputCompatError);
            }

            var targetSlot = GetMetadataInt(execution, "Slot", 0);
            SetState(execution, "OriginalSlot", originalSlot);
            SetState(execution, "TargetSlot", targetSlot);
            SetState(execution, "SlotSwitchBefore", originalSlot);
            SetState(execution, "SlotSwitchTarget", targetSlot);
            SetState(execution, "SlotSwitchAttempted", "false");
            SetState(execution, "SlotSwitchSucceeded", "false");
            SetState(execution, "WaitForMouseReleaseAttempted", IsButtonSource(execution) ? "true" : "false");
            SetState(execution, "WaitedForMouseRelease", "false");
            SetState(execution, "MouseReleaseWaitTicks", 0);
            SetState(execution, "UiClickSuppressionAttempted", DiagnosticInteractionDiagnostics.UiClickSuppressionAttempted ? "true" : "false");
            SetState(execution, "UiClickSuppressionMode", DiagnosticInteractionDiagnostics.UiClickSuppressionMode);
            SetState(execution, "UiClickSuppressionSucceeded", DiagnosticInteractionDiagnostics.UiClickSuppressionSucceeded ? "true" : "false");
            SetState(execution, "UiMouseCaptureAvailableAtClick", DiagnosticInteractionDiagnostics.UiMouseCaptureAvailableAtClick ? "true" : "false");
            SetState(execution, "HitTestModeAtClick", GetMetadataString(execution, "HitTestMode", string.Empty));
            SetState(execution, "ClickSourceAtClick", GetMetadataString(execution, "ClickSource", string.Empty));
            SetState(execution, "HitTestCandidateSummary", DiagnosticInteractionDiagnostics.HitTestCandidateSummary);

            if (!TerrariaInputCompat.IsSupportedItemUseSlot(targetSlot))
            {
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "UseHotbarItem target item-use slot out of range: " + targetSlot);
            }

            ItemUseVerificationState targetState;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, targetSlot, out targetState))
            {
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            var targetItemTypeOverride = GetMetadataInt(execution, "TargetItemTypeOverride", 0);
            var effectiveTargetItemType = targetItemTypeOverride > 0 ? targetItemTypeOverride : targetState.ItemType;
            var effectiveTargetItemName = targetItemTypeOverride > 0
                ? ItemSwapFamilyCompat.ResolveItemDisplayName(targetItemTypeOverride, targetState.ItemName)
                : targetState.ItemName;
            SetState(execution, "TargetItemType", effectiveTargetItemType);
            SetState(execution, "TargetItemName", effectiveTargetItemName);
            SetState(execution, "TargetItemTypeBeforeFormChange", targetState.ItemType);
            SetState(execution, "TargetItemTypeOverride", targetItemTypeOverride);
            SetState(execution, "TargetItemFormChangeRequested", targetItemTypeOverride > 0 && targetItemTypeOverride != targetState.ItemType ? "true" : "false");
            SetState(execution, "TargetItemFormChangeAttempted", "false");
            SetState(execution, "TargetItemFormChangeSucceeded", "false");
            SetState(execution, "TargetItemStack", targetState.ItemStack);

            if (!targetState.PlayerActive || targetState.PlayerDead || targetState.PlayerGhost)
            {
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Player is not available for UseHotbarItem.");
            }

            if (targetState.ItemType <= 0 || targetState.ItemStack <= 0)
            {
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.MissingRequiredItem, "Target hotbar slot is empty; click a non-empty test slot first.");
            }

            SetState(execution, "Stage", IsButtonSource(execution) ? StageWaitMouseRelease : StageSwitchToTargetSlot);
            SetState(execution, "SlotSwitchRetryTicks", 0);
            SetState(execution, "StabilizeTicks", 0);
            return InputActionExecutionStepResult.Running("UseHotbarItem validated target slot.");
        }

        public override InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var stage = GetState(execution, "Stage", StageSwitchToTargetSlot);
            if (stage == StageWaitMouseRelease)
            {
                return WaitForMouseRelease(execution);
            }

            if (stage == StageSwitchToTargetSlot)
            {
                return SwitchToTargetSlot(execution);
            }

            if (stage == StageVerifyTargetSlot)
            {
                return VerifyTargetSlot(execution);
            }

            if (stage == StageStabilizeTargetSlot)
            {
                var ticks = GetStateInt(execution, "StabilizeTicks", 0) + 1;
                SetState(execution, "StabilizeTicks", ticks);
                if (ticks < 1)
                {
                    return InputActionExecutionStepResult.Running("UseHotbarItem waiting one tick for selected slot to settle.");
                }

                SetState(execution, "Stage", StageEnqueueBridge);
                return InputActionExecutionStepResult.Running("UseHotbarItem selected slot settled.");
            }

            if (stage == StageEnqueueBridge)
            {
                return EnqueueBridge(execution);
            }

            if (stage == StageRestoreOriginalSlot)
            {
                return RestoreOriginalSlot(execution);
            }

            if (stage == StageVerifyOriginalSlotRestored)
            {
                return VerifyOriginalSlotRestored(execution);
            }

            return WaitBridgeResult(execution);
        }

        public override InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
        {
            if (execution != null && execution.Request != null)
            {
                ItemUseBridge.Cancel(execution.Request.RequestId, reason ?? "UseHotbarItem cancelled.");
            }

            var status = string.Equals(reason, "Action timed out.", StringComparison.OrdinalIgnoreCase)
                ? InputActionStatus.TimedOut
                : InputActionStatus.Cancelled;
            var code = status == InputActionStatus.TimedOut ? DiagnosticResultCode.TimedOut : DiagnosticResultCode.Failed;
            CaptureBridgeResult(execution, ItemUseBridge.GetResult(execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId));
            TryRestoreOriginalSlot(execution);
            return CompleteFinalFromState(execution, status, code, reason ?? "UseHotbarItem cancelled.");
        }

        private InputActionExecutionStepResult WaitForMouseRelease(InputActionExecution execution)
        {
            var mouse = DiagnosticMouseStateReader.Read();
            var leftDown = mouse != null && (mouse.OsLeftDown || mouse.TerrariaLeftDown);
            var ticks = GetStateInt(execution, "MouseReleaseWaitTicks", 0);

            if (!leftDown)
            {
                SetState(execution, "WaitedForMouseRelease", "true");
                SetState(execution, "Stage", StageSwitchToTargetSlot);
                return InputActionExecutionStepResult.Running("Button left mouse is released; switching to target slot.");
            }

            if (ticks < 30)
            {
                SetState(execution, "MouseReleaseWaitTicks", ticks + 1);
                return InputActionExecutionStepResult.Running("Waiting for button left mouse release before switching slot.");
            }

            SetState(execution, "WaitedForMouseRelease", "false");
            SetState(execution, "MouseReleaseWaitTicks", 30);
            SetState(execution, "LikelyReason", "Button left mouse release wait timed out; one accidental vanilla click may still be possible.");
            SetState(execution, "Stage", StageSwitchToTargetSlot);
            return InputActionExecutionStepResult.Running("Mouse release wait timed out; continuing with target slot use and logging risk.");
        }

        private InputActionExecutionStepResult SwitchToTargetSlot(InputActionExecution execution)
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, TerrariaInputCompat.LastInputCompatError);
            }

            var targetSlot = GetStateInt(execution, "TargetSlot", -1);
            var originalSlot = GetStateInt(execution, "OriginalSlot", -1);
            SetState(execution, "SlotSwitchAttempted", "true");
            SetState(execution, "SlotSwitchBefore", originalSlot);
            SetState(execution, "SlotSwitchTarget", targetSlot);

            if (!TerrariaInputCompat.TrySelectInventorySlot(player, targetSlot))
            {
                SetState(execution, "SlotSwitchSucceeded", "false");
                SetState(execution, "SlotSwitchMethod", TerrariaInputCompat.LastSelectionMethod);
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "UseHotbarItem failed to switch to target slot: " + TerrariaInputCompat.LastInputCompatError);
            }

            SetState(execution, "SlotSwitchMethod", TerrariaInputCompat.LastSelectionMethod);
            int selectedSlot;
            if (TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                SetState(execution, "SlotSwitchAfter", selectedSlot);
                SetState(execution, "SlotSwitchSucceeded", selectedSlot == targetSlot ? "true" : "false");
            }

            SetState(execution, "Stage", StageVerifyTargetSlot);
            return InputActionExecutionStepResult.Running("UseHotbarItem switched to target slot; verifying selection.");
        }

        private InputActionExecutionStepResult VerifyTargetSlot(InputActionExecution execution)
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, TerrariaInputCompat.LastInputCompatError);
            }

            var targetSlot = GetStateInt(execution, "TargetSlot", -1);
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "UseHotbarItem cannot read selected slot: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (selectedSlot == targetSlot)
            {
                SetState(execution, "SelectedSlotAtUseStart", selectedSlot);
                SetState(execution, "SlotSwitchAfter", selectedSlot);
                SetState(execution, "SlotSwitchSucceeded", "true");
                SetState(execution, "Stage", StageStabilizeTargetSlot);
                return InputActionExecutionStepResult.Running("UseHotbarItem confirmed target slot selected.");
            }

            var retryTicks = GetStateInt(execution, "SlotSwitchRetryTicks", 0);
            if (retryTicks < 3 && TerrariaInputCompat.TrySelectInventorySlot(player, targetSlot))
            {
                SetState(execution, "SlotSwitchRetryTicks", retryTicks + 1);
                SetState(execution, "SlotSwitchMethod", TerrariaInputCompat.LastSelectionMethod);
                SetState(execution, "SlotSwitchAfter", selectedSlot);
                return InputActionExecutionStepResult.Running("UseHotbarItem retrying target slot selection.");
            }

            SetState(execution, "SlotSwitchAfter", selectedSlot);
            SetState(execution, "SlotSwitchSucceeded", "false");
            SetState(execution, "LikelyReason", "selectedSlotAtUseStart did not match targetSlot.");
            TryRestoreOriginalSlot(execution);
            return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "UseHotbarItem failed to select target slot. selectedSlot=" + selectedSlot + ", targetSlot=" + targetSlot + ".");
        }

        private InputActionExecutionStepResult EnqueueBridge(InputActionExecution execution)
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, TerrariaInputCompat.LastInputCompatError);
            }

            var targetSlot = GetStateInt(execution, "TargetSlot", -1);
            var originalSlot = GetStateInt(execution, "OriginalSlot", -1);
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "UseHotbarItem cannot read selected slot before use: " + TerrariaInputCompat.LastInputCompatError);
            }

            SetState(execution, "SelectedSlotAtUseStart", selectedSlot);
            if (selectedSlot != targetSlot)
            {
                SetState(execution, "LikelyReason", "selectedSlotAtUseStart did not match targetSlot.");
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "UseHotbarItem selectedSlot was not targetSlot before ItemUseBridge. selectedSlot=" + selectedSlot + ", targetSlot=" + targetSlot + ".");
            }

            var expectedType = GetStateInt(execution, "TargetItemType", 0);
            string formChangeMessage;
            if (!TryApplyTargetItemTypeOverride(execution, player, targetSlot, expectedType, out formChangeMessage))
            {
                SetState(execution, "LikelyReason", formChangeMessage);
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "UseHotbarItem target item form change failed: " + formChangeMessage);
            }

            ItemUseVerificationState beforeUse;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, targetSlot, out beforeUse))
            {
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            if (beforeUse.ItemType != expectedType || beforeUse.ItemStack <= 0)
            {
                SetState(execution, "LikelyReason", "Target slot item changed or became empty before ItemUseBridge.");
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, "UseHotbarItem target item changed or became empty before use.");
            }

            var hasMouseWorldTarget = HasMetadata(execution, ActionMetadataKeys.WorldX) &&
                                      HasMetadata(execution, ActionMetadataKeys.WorldY);
            var hasMouseScreenTarget = !hasMouseWorldTarget &&
                                       HasMetadata(execution, ActionMetadataKeys.ScreenX) &&
                                       HasMetadata(execution, ActionMetadataKeys.ScreenY);
            var applyMainMouseLeftForItemCheck = string.Equals(
                GetMetadataString(execution, "ApplyMainMouseLeftForItemCheck", string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);

            var options = new ItemUseBridgeOptions
            {
                SelectedSlotAtUseStart = selectedSlot,
                SlotSwitchAttempted = true,
                SlotSwitchSucceeded = true,
                SlotSwitchMethod = GetState(execution, "SlotSwitchMethod", string.Empty),
                SlotSwitchBefore = originalSlot,
                SlotSwitchAfter = selectedSlot,
                WaitForMouseReleaseAttempted = IsButtonSource(execution),
                WaitedForMouseRelease = GetStateBool(execution, "WaitedForMouseRelease", false),
                MouseReleaseWaitTicks = GetStateInt(execution, "MouseReleaseWaitTicks", 0),
                SkipSelectInItemCheck = true,
                RestoreSelectedSlotOverride = originalSlot,
                ApplyMainMouseLeftForItemCheck = applyMainMouseLeftForItemCheck,
                HasMouseWorldTarget = hasMouseWorldTarget,
                MouseWorldX = hasMouseWorldTarget ? GetMetadataFloat(execution, ActionMetadataKeys.WorldX, 0f) : 0f,
                MouseWorldY = hasMouseWorldTarget ? GetMetadataFloat(execution, ActionMetadataKeys.WorldY, 0f) : 0f,
                HasMouseScreenTarget = hasMouseScreenTarget,
                MouseScreenX = hasMouseScreenTarget ? GetMetadataInt(execution, ActionMetadataKeys.ScreenX, 0) : 0,
                MouseScreenY = hasMouseScreenTarget ? GetMetadataInt(execution, ActionMetadataKeys.ScreenY, 0) : 0,
                UiClickSuppressionAttempted = GetStateBool(execution, "UiClickSuppressionAttempted", false),
                UiClickSuppressionMode = GetState(execution, "UiClickSuppressionMode", string.Empty),
                UiClickSuppressionSucceeded = GetStateBool(execution, "UiClickSuppressionSucceeded", false),
                UiMouseCaptureAvailableAtClick = GetStateBool(execution, "UiMouseCaptureAvailableAtClick", false),
                HitTestModeAtClick = GetState(execution, "HitTestModeAtClick", string.Empty),
                ClickSourceAtClick = GetState(execution, "ClickSourceAtClick", string.Empty)
            };

            string message;
            if (!ItemUseBridge.TryEnqueueUseSelectedItem(
                execution.Request.RequestId,
                execution.Request.SourceFeatureId,
                targetSlot,
                beforeUse.ItemType,
                beforeUse.ItemStack,
                beforeUse.ItemName,
                execution.Request.Timeout,
                originalSlot,
                InputActionKind.UseHotbarItem,
                GetMetadataString(execution, "Scenario", IsButtonSource(execution) ? "Button.UseHotbarItem" : "CtrlAltU.UseHotbarItem"),
                GetSourceHotkey(execution),
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty),
                options,
                out message))
            {
                TryRestoreOriginalSlot(execution);
                return CompleteDetailed(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, message);
            }

            SetState(execution, "BridgeRequestId", execution.Request.RequestId.ToString());
            SetState(execution, "Stage", StageWaitBridgeResult);
            return InputActionExecutionStepResult.Running("UseHotbarItem submitted ItemUseBridge for target slot " + (targetSlot + 1) + ": " + beforeUse.ItemName + " x" + beforeUse.ItemStack + ".");
        }

        private bool TryApplyTargetItemTypeOverride(InputActionExecution execution, object player, int targetSlot, int expectedType, out string message)
        {
            message = string.Empty;
            var overrideType = GetStateInt(execution, "TargetItemTypeOverride", 0);
            if (overrideType <= 0)
            {
                SetState(execution, "TargetItemFormChangeAttempted", "false");
                SetState(execution, "TargetItemFormChangeSucceeded", "true");
                return true;
            }

            ItemUseVerificationState currentState;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, targetSlot, out currentState))
            {
                message = TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            SetState(execution, "TargetItemTypeBeforeFormChange", currentState.ItemType);
            if (currentState.ItemType == expectedType)
            {
                SetState(execution, "TargetItemFormChangeAttempted", "false");
                SetState(execution, "TargetItemFormChangeSucceeded", "true");
                SetState(execution, "TargetItemFormChangeMessage", "Target slot already has requested form.");
                return true;
            }

            SetState(execution, "TargetItemFormChangeAttempted", "true");
            int beforeType;
            int afterType;
            string beforeName;
            string afterName;
            if (!ItemSwapFamilyCompat.TryChangeInventoryItemType(player, targetSlot, expectedType, out beforeType, out beforeName, out afterType, out afterName, out message))
            {
                SetState(execution, "TargetItemFormChangeSucceeded", "false");
                SetState(execution, "TargetItemFormChangeMessage", message);
                return false;
            }

            SetState(execution, "TargetItemFormChangeBeforeType", beforeType);
            SetState(execution, "TargetItemFormChangeBeforeName", beforeName ?? string.Empty);
            SetState(execution, "TargetItemFormChangeAfterType", afterType);
            SetState(execution, "TargetItemFormChangeAfterName", afterName ?? string.Empty);
            SetState(execution, "TargetItemFormChangeSucceeded", "true");
            SetState(execution, "TargetItemFormChangeMessage", message);
            return true;
        }

        private InputActionExecutionStepResult WaitBridgeResult(InputActionExecution execution)
        {
            var result = ItemUseBridge.GetResult(execution.Request.RequestId);
            switch (result.Status)
            {
                case ItemUseBridgeStatus.WaitingForItemCheck:
                case ItemUseBridgeStatus.Consumed:
                    return InputActionExecutionStepResult.Running(result.Message);

                case ItemUseBridgeStatus.Succeeded:
                    CaptureBridgeResult(execution, result);
                    return BeginFinalRestore(execution, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, result.Message);

                case ItemUseBridgeStatus.AttemptedButUnverified:
                    CaptureBridgeResult(execution, result);
                    return BeginFinalRestore(execution, InputActionStatus.AttemptedButUnverified, DiagnosticResultCode.AttemptedButUnverified, result.Message);

                case ItemUseBridgeStatus.Expired:
                    CaptureBridgeResult(execution, result);
                    return BeginFinalRestore(execution, InputActionStatus.TimedOut, DiagnosticResultCode.TimedOut, result.Message);

                case ItemUseBridgeStatus.Cancelled:
                    CaptureBridgeResult(execution, result);
                    return BeginFinalRestore(execution, InputActionStatus.Cancelled, DiagnosticResultCode.Failed, result.Message);

                case ItemUseBridgeStatus.Failed:
                    CaptureBridgeResult(execution, result);
                    return BeginFinalRestore(execution, InputActionStatus.Failed, DiagnosticResultCode.Failed, result.Message);

                default:
                    return InputActionExecutionStepResult.Running("Waiting for Player.ItemCheck to consume queued UseHotbarItem.");
            }
        }

        private void CaptureBridgeResult(InputActionExecution execution, ItemUseBridgeResult result)
        {
            if (result == null || result.Status == ItemUseBridgeStatus.None)
            {
                return;
            }

            SetState(execution, "BridgeStatus", result.Status.ToString());
            SetState(execution, "BridgeResultCode", result.ResultCode ?? string.Empty);
            SetState(execution, "BridgeMessage", result.Message ?? string.Empty);
            SetState(execution, "BridgeDurationMs", result.DurationMs.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "BridgeConsumedByItemCheck", result.ConsumedByItemCheck ? "true" : "false");
            SetState(execution, "BridgeSelectedSlotAtUseStart", result.SelectedSlotAtUseStart);
            SetState(execution, "BridgeSlotSwitchAttempted", result.SlotSwitchAttempted ? "true" : "false");
            SetState(execution, "BridgeSlotSwitchSucceeded", result.SlotSwitchSucceeded ? "true" : "false");

            if (result.AfterState != null)
            {
                SetState(execution, "BridgeSelectedSlotAfterItemCheck", result.AfterState.SelectedSlot);
                SetState(execution, "BridgeAfterItemType", result.AfterState.ItemType);
                SetState(execution, "BridgeAfterItemName", result.AfterState.ItemName ?? string.Empty);
                SetState(execution, "BridgeAfterItemStack", result.AfterState.ItemStack);
                SetState(execution, "BridgeAfterLife", result.AfterState.Life);
                SetState(execution, "BridgeAfterMana", result.AfterState.Mana);
                SetState(execution, "BridgeAfterItemAnimation", result.AfterState.ItemAnimation);
                SetState(execution, "BridgeAfterItemTime", result.AfterState.ItemTime);
                SetState(execution, "BridgeAfterReuseDelay", result.AfterState.ReuseDelay);
            }

            SetState(execution, "BridgeObservableChange", HasObservableSuccess(result.BeforeState, result.AfterState) ? "true" : "false");
            SetState(execution, "BridgeChangedFieldsJson", BuildChangedFieldsJson(result.BeforeState, result.AfterState));
        }

        private InputActionExecutionStepResult BeginFinalRestore(
            InputActionExecution execution,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message)
        {
            SetState(execution, "PendingFinalStatus", status.ToString());
            SetState(execution, "PendingFinalResultCode", code.ToString());
            SetState(execution, "PendingFinalMessage", message ?? string.Empty);
            SetState(execution, "RestoreAttempted", "false");
            SetState(execution, "RestoreAttemptCount", 0);
            SetState(execution, "RestoreSucceeded", "false");
            SetState(execution, "SelectedSlotRestored", "false");
            SetState(execution, "RestoreWaitTicks", 0);
            SetState(execution, "RestoreWaitedForUseIdle", "false");
            SetState(execution, "RestoreObservedAfterDelay", "false");
            SetState(execution, "Stage", StageRestoreOriginalSlot);
            return InputActionExecutionStepResult.Running("UseHotbarItem bridge finished; restoring original selected slot.");
        }

        private InputActionExecutionStepResult RestoreOriginalSlot(InputActionExecution execution)
        {
            var originalSlot = GetStateInt(execution, "OriginalSlot", -1);
            if (!TerrariaInputCompat.IsSupportedItemUseSlot(originalSlot))
            {
                SetState(execution, "RestoreAttempted", "false");
                SetState(execution, "RestoreAttemptCount", 0);
                SetState(execution, "RestoreSucceeded", "true");
                SetState(execution, "SelectedSlotRestored", "true");
                SetState(execution, "RestoreMethod", "not-required");
                CaptureFinalUseStateFromLocalPlayer(execution);
                return CompleteFinalFromState(execution);
            }

            SetState(execution, "RestoreAttempted", "true");
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                SetState(execution, "RestoreSucceeded", "false");
                SetState(execution, "SelectedSlotRestored", "false");
                SetState(execution, "RestoreMessage", TerrariaInputCompat.LastInputCompatError);
                CaptureFinalUseStateFromLocalPlayer(execution);
                return CompleteFinalFromState(execution);
            }

            int selectedSlot;
            if (TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                SetFinalSelectedSlot(execution, selectedSlot);
                if (selectedSlot == originalSlot)
                {
                    SetState(execution, "RestoreSucceeded", "true");
                    SetState(execution, "SelectedSlotRestored", "true");
                    SetState(execution, "RestoreMethod", "already-restored");
                    SetState(execution, "RestoreVerifyWaitTicks", 0);
                    SetState(execution, "Stage", StageVerifyOriginalSlotRestored);
                    return InputActionExecutionStepResult.Running("UseHotbarItem original slot already restored; waiting one tick before final verification.");
                }
            }

            var attemptCount = GetStateInt(execution, "RestoreAttemptCount", 0);
            if (attemptCount >= MaxRestoreAttempts)
            {
                SetState(execution, "RestoreSucceeded", "false");
                SetState(execution, "SelectedSlotRestored", "false");
                if (string.IsNullOrWhiteSpace(GetState(execution, "RestoreMessage", string.Empty)))
                {
                    SetState(execution, "RestoreMessage", "Restore original slot failed after retries.");
                }

                CaptureFinalUseStateFromLocalPlayer(execution);
                return CompleteFinalFromState(execution);
            }

            attemptCount++;
            SetState(execution, "RestoreAttemptCount", attemptCount);
            if (!TerrariaInputCompat.TrySelectInventorySlot(player, originalSlot))
            {
                SetState(execution, "RestoreSucceeded", "false");
                SetState(execution, "SelectedSlotRestored", "false");
                SetState(execution, "RestoreMethod", TerrariaInputCompat.LastSelectionMethod);
                SetState(execution, "RestoreMessage", "Restore original slot failed: " + TerrariaInputCompat.LastInputCompatError);
                SetState(execution, "Stage", attemptCount >= MaxRestoreAttempts ? StageVerifyOriginalSlotRestored : StageRestoreOriginalSlot);
                return InputActionExecutionStepResult.Running("UseHotbarItem restore attempt failed; verifying or retrying original slot.");
            }

            SetState(execution, "RestoreMethod", TerrariaInputCompat.LastSelectionMethod);
            SetState(execution, "RestoreVerifyWaitTicks", 0);
            SetState(execution, "Stage", StageVerifyOriginalSlotRestored);
            return InputActionExecutionStepResult.Running("UseHotbarItem restored original slot; waiting one tick to verify.");
        }

        private InputActionExecutionStepResult VerifyOriginalSlotRestored(InputActionExecution execution)
        {
            var waitTicks = GetStateInt(execution, "RestoreVerifyWaitTicks", 0) + 1;
            SetState(execution, "RestoreVerifyWaitTicks", waitTicks);
            SetState(execution, "RestoreWaitTicks", waitTicks);
            if (waitTicks > 1)
            {
                SetState(execution, "RestoreObservedAfterDelay", "true");
            }

            var originalSlot = GetStateInt(execution, "OriginalSlot", -1);
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                SetState(execution, "RestoreSucceeded", "false");
                SetState(execution, "SelectedSlotRestored", "false");
                SetState(execution, "RestoreMessage", TerrariaInputCompat.LastInputCompatError);
                CaptureFinalUseStateFromLocalPlayer(execution);
                return CompleteFinalFromState(execution);
            }

            int finalSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out finalSlot))
            {
                SetState(execution, "RestoreSucceeded", "false");
                SetState(execution, "SelectedSlotRestored", "false");
                SetState(execution, "RestoreMessage", "Cannot read final selected slot: " + TerrariaInputCompat.LastInputCompatError);
                CaptureFinalUseState(execution, player);
                return CompleteFinalFromState(execution);
            }

            SetFinalSelectedSlot(execution, finalSlot);
            var useIdle = CaptureFinalUseState(execution, player);
            if (waitTicks < 1)
            {
                return InputActionExecutionStepResult.Running("UseHotbarItem waiting one tick before final restore verification.");
            }

            if (finalSlot == originalSlot)
            {
                SetState(execution, "RestoreSucceeded", "true");
                SetState(execution, "SelectedSlotRestored", "true");
                if (!useIdle && waitTicks < MaxFinalUseIdleWaitTicks)
                {
                    SetState(execution, "RestoreWaitedForUseIdle", "true");
                    SetState(execution, "RestoreMessage", "Original slot restored; waiting for itemAnimation/itemTime to become idle before final action-event.");
                    return InputActionExecutionStepResult.Running("UseHotbarItem restored original slot; waiting for item use to become idle before final log.");
                }

                if (!useIdle)
                {
                    SetState(execution, "RestoreWaitedForUseIdle", "true");
                    SetState(execution, "RestoreMessage", "Original slot restored; itemAnimation/itemTime still non-zero at bounded wait limit, logging restored slot.");
                }
                else if (string.IsNullOrWhiteSpace(GetState(execution, "RestoreMessage", string.Empty)))
                {
                    SetState(execution, "RestoreMessage", "Original selected slot restored and verified after delay.");
                }

                return CompleteFinalFromState(execution);
            }

            if (GetStateInt(execution, "RestoreAttemptCount", 0) < MaxRestoreAttempts)
            {
                SetState(execution, "Stage", StageRestoreOriginalSlot);
                return InputActionExecutionStepResult.Running("UseHotbarItem original slot not restored yet; retrying.");
            }

            SetState(execution, "RestoreSucceeded", "false");
            SetState(execution, "SelectedSlotRestored", "false");
            SetState(execution, "RestoreMessage", "Restore original slot failed after retries; finalSelectedSlot=" + finalSlot + ".");
            return CompleteFinalFromState(execution);
        }

        private bool TryRestoreOriginalSlot(InputActionExecution execution)
        {
            var originalSlot = GetStateInt(execution, "OriginalSlot", -1);
            if (!TerrariaInputCompat.IsSupportedItemUseSlot(originalSlot))
            {
                SetState(execution, "RestoreAttempted", "false");
                SetState(execution, "RestoreAttemptCount", 0);
                SetState(execution, "RestoreSucceeded", "true");
                SetState(execution, "SelectedSlotRestored", "true");
                SetState(execution, "RestoreMethod", "not-required");
                return true;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                SetState(execution, "RestoreAttempted", "true");
                SetState(execution, "RestoreSucceeded", "false");
                SetState(execution, "SelectedSlotRestored", "false");
                SetState(execution, "RestoreMessage", TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            SetState(execution, "RestoreAttempted", "true");
            var restored = false;
            int restoredSlot;
            if (TerrariaInputCompat.TryGetSelectedItem(player, out restoredSlot))
            {
                SetFinalSelectedSlot(execution, restoredSlot);
                if (restoredSlot == originalSlot)
                {
                    SetState(execution, "RestoreSucceeded", "true");
                    SetState(execution, "SelectedSlotRestored", "true");
                    SetState(execution, "RestoreMethod", "already-restored");
                    return true;
                }
            }

            var attemptCount = GetStateInt(execution, "RestoreAttemptCount", 0);
            while (attemptCount < MaxRestoreAttempts)
            {
                attemptCount++;
                SetState(execution, "RestoreAttemptCount", attemptCount);
                if (!TerrariaInputCompat.TrySelectInventorySlot(player, originalSlot))
                {
                    SetState(execution, "RestoreMethod", TerrariaInputCompat.LastSelectionMethod);
                    SetState(execution, "RestoreMessage", "Restore original slot failed: " + TerrariaInputCompat.LastInputCompatError);
                    continue;
                }

                SetState(execution, "RestoreMethod", TerrariaInputCompat.LastSelectionMethod);
                if (TerrariaInputCompat.TryGetSelectedItem(player, out restoredSlot))
                {
                    SetFinalSelectedSlot(execution, restoredSlot);
                    restored = restoredSlot == originalSlot;
                    if (restored)
                    {
                        break;
                    }
                }
            }

            SetState(execution, "RestoreSucceeded", restored ? "true" : "false");
            SetState(execution, "SelectedSlotRestored", restored ? "true" : "false");
            if (!restored)
            {
                if (string.IsNullOrWhiteSpace(GetState(execution, "RestoreMessage", string.Empty)))
                {
                    SetState(execution, "RestoreMessage", "Restore original slot failed: " + TerrariaInputCompat.LastInputCompatError);
                }

                SetState(execution, "LikelyReason", "selectedSlotRestored=false; " + TerrariaInputCompat.LastInputCompatError);
            }

            return restored;
        }

        private void SetFinalSelectedSlot(InputActionExecution execution, int slot)
        {
            SetState(execution, "FinalSelectedSlot", slot);
            SetState(execution, "RestoredSelectedSlot", slot);
        }

        private bool CaptureFinalUseStateFromLocalPlayer(InputActionExecution execution)
        {
            object player;
            return TerrariaInputCompat.TryGetLocalPlayer(out player) && CaptureFinalUseState(execution, player);
        }

        private bool CaptureFinalUseState(InputActionExecution execution, object player)
        {
            if (player == null)
            {
                return false;
            }

            var targetSlot = GetStateInt(execution, "TargetSlot", -1);
            ItemUseVerificationState finalState;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, targetSlot, out finalState))
            {
                return false;
            }

            SetFinalSelectedSlot(execution, finalState.SelectedSlot);
            SetState(execution, "FinalItemAnimation", finalState.ItemAnimation);
            SetState(execution, "FinalItemTime", finalState.ItemTime);
            SetState(execution, "FinalReuseDelay", finalState.ReuseDelay);
            SetState(execution, "FinalTargetItemType", finalState.ItemType);
            SetState(execution, "FinalTargetItemStack", finalState.ItemStack);
            if (!string.IsNullOrWhiteSpace(finalState.ItemName))
            {
                SetState(execution, "FinalTargetItemName", finalState.ItemName);
            }

            return finalState.ItemAnimation <= 0 && finalState.ItemTime <= 0 && finalState.ReuseDelay <= 0;
        }

        private InputActionExecutionStepResult CompleteFinalFromState(InputActionExecution execution)
        {
            CaptureFinalUseStateFromLocalPlayer(execution);
            return CompleteFinalFromState(
                execution,
                ParseStatus(GetState(execution, "PendingFinalStatus", InputActionStatus.Failed.ToString())),
                ParseResultCode(GetState(execution, "PendingFinalResultCode", DiagnosticResultCode.Failed.ToString())),
                GetState(execution, "PendingFinalMessage", string.Empty));
        }

        private InputActionExecutionStepResult CompleteFinalFromState(
            InputActionExecution execution,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message)
        {
            var restoreSucceeded = GetStateBool(execution, "RestoreSucceeded", GetStateBool(execution, "SelectedSlotRestored", false));
            var observableChange = GetStateBool(execution, "BridgeObservableChange", false);
            var bridgeStatus = GetState(execution, "BridgeStatus", string.Empty);
            var bridgeSucceeded = string.Equals(bridgeStatus, ItemUseBridgeStatus.Succeeded.ToString(), StringComparison.OrdinalIgnoreCase);
            if (status == InputActionStatus.Succeeded && !restoreSucceeded)
            {
                status = InputActionStatus.AttemptedButUnverified;
                code = DiagnosticResultCode.AttemptedButUnverified;
                message = "\u6D4B\u8BD5\u683C\u7269\u54C1\u5DF2\u4F7F\u7528\uFF0C\u4F46\u6062\u590D\u539F\u624B\u6301\u683C\u5931\u8D25\uFF1B\u8BF7\u4E0A\u4F20\u65E5\u5FD7\u3002";
            }
            else if (!restoreSucceeded)
            {
                var restoreMessage = GetState(execution, "RestoreMessage", string.Empty);
                if (!string.IsNullOrWhiteSpace(restoreMessage) && message != null && message.IndexOf("\u6062\u590D", StringComparison.Ordinal) < 0)
                {
                    message = message + " Restore warning: " + restoreMessage;
                }
            }
            else if (bridgeSucceeded && observableChange && string.IsNullOrWhiteSpace(message))
            {
                message = "UseHotbarItem used target slot item and restored original selected slot.";
            }

            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, "Scenario", IsButtonSource(execution) ? "Button.UseHotbarItem" : "CtrlAltU.UseHotbarItem"),
                InputActionKind.UseHotbarItem.ToString(),
                GetSourceHotkey(execution),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                GetDurationMs(execution),
                BuildBeforeJson(execution),
                BuildAfterJson(execution),
                BuildVerificationJson(execution),
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));
            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private InputActionExecutionStepResult CompleteDetailed(InputActionExecution execution, InputActionStatus status, DiagnosticResultCode code, string message)
        {
            TryRestoreOriginalSlot(execution);
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, "Scenario", IsButtonSource(execution) ? "Button.UseHotbarItem" : "CtrlAltU.UseHotbarItem"),
                InputActionKind.UseHotbarItem.ToString(),
                GetSourceHotkey(execution),
                status.ToString(),
                code.ToString(),
                message,
                0,
                BuildBeforeJson(execution),
                BuildAfterJson(execution),
                BuildVerificationJson(execution),
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));
            return InputActionExecutionStepResult.Complete(status, message);
        }

        private string BuildBeforeJson(InputActionExecution execution)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "originalSelectedSlot", SlotRaw(GetStateInt(execution, "OriginalSlot", -1)), true);
            AppendRaw(builder, "originalSelectedSlotDisplay", SlotDisplayRaw(GetStateInt(execution, "OriginalSlot", -1)), true);
            AppendRaw(builder, "targetSlot", SlotRaw(GetStateInt(execution, "TargetSlot", -1)), true);
            AppendRaw(builder, "targetSlotDisplay", SlotDisplayRaw(GetStateInt(execution, "TargetSlot", -1)), true);
            AppendRaw(builder, "targetItemType", IntRaw(GetStateInt(execution, "TargetItemType", 0)), true);
            AppendRaw(builder, "targetItemTypeBeforeFormChange", IntRaw(GetStateInt(execution, "TargetItemTypeBeforeFormChange", 0)), true);
            AppendRaw(builder, "targetItemTypeOverride", IntRaw(GetStateInt(execution, "TargetItemTypeOverride", 0)), true);
            AppendString(builder, "targetItemName", GetState(execution, "TargetItemName", string.Empty), true);
            AppendRaw(builder, "targetItemStack", IntRaw(GetStateInt(execution, "TargetItemStack", 0)), true);
            AppendRaw(builder, "waitForMouseReleaseAttempted", BoolRaw(GetStateBool(execution, "WaitForMouseReleaseAttempted", false)), true);
            AppendRaw(builder, "waitedForMouseRelease", BoolRaw(GetStateBool(execution, "WaitedForMouseRelease", false)), true);
            AppendRaw(builder, "mouseReleaseWaitTicks", IntRaw(GetStateInt(execution, "MouseReleaseWaitTicks", 0)), true);
            AppendRaw(builder, "slotSwitchAttempted", BoolRaw(GetStateBool(execution, "SlotSwitchAttempted", false)), true);
            AppendRaw(builder, "slotSwitchBefore", SlotRaw(GetStateInt(execution, "SlotSwitchBefore", -1)), true);
            AppendRaw(builder, "slotSwitchTarget", SlotRaw(GetStateInt(execution, "TargetSlot", -1)), true);
            AppendRaw(builder, "slotSwitchAfter", SlotRaw(GetStateInt(execution, "SlotSwitchAfter", -1)), true);
            AppendRaw(builder, "slotSwitchSucceeded", BoolRaw(GetStateBool(execution, "SlotSwitchSucceeded", false)), true);
            AppendString(builder, "slotSwitchMethod", GetState(execution, "SlotSwitchMethod", string.Empty), true);
            AppendRaw(builder, "selectedSlotAtUseStart", SlotRaw(GetStateInt(execution, "SelectedSlotAtUseStart", -1)), true);
            AppendRaw(builder, "uiClickSuppressionAttempted", BoolRaw(GetStateBool(execution, "UiClickSuppressionAttempted", false)), true);
            AppendString(builder, "uiClickSuppressionMode", GetState(execution, "UiClickSuppressionMode", string.Empty), true);
            AppendRaw(builder, "uiClickSuppressionSucceeded", BoolRaw(GetStateBool(execution, "UiClickSuppressionSucceeded", false)), true);
            AppendRaw(builder, "uiMouseCaptureAvailableAtClick", BoolRaw(GetStateBool(execution, "UiMouseCaptureAvailableAtClick", false)), true);
            AppendString(builder, "hitTestModeAtClick", GetState(execution, "HitTestModeAtClick", string.Empty), true);
            AppendString(builder, "clickSourceAtClick", GetState(execution, "ClickSourceAtClick", string.Empty), true);
            AppendString(builder, "hitTestCandidateSummary", GetState(execution, "HitTestCandidateSummary", string.Empty), false);
            builder.Append("}");
            return builder.ToString();
        }

        private string BuildAfterJson(InputActionExecution execution)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "bridgeSelectedSlotAfterItemCheck", SlotRaw(GetStateInt(execution, "BridgeSelectedSlotAfterItemCheck", -1)), true);
            AppendRaw(builder, "finalSelectedSlot", SlotRaw(GetStateInt(execution, "FinalSelectedSlot", GetStateInt(execution, "RestoredSelectedSlot", -1))), true);
            AppendRaw(builder, "finalSelectedSlotDisplay", SlotDisplayRaw(GetStateInt(execution, "FinalSelectedSlot", GetStateInt(execution, "RestoredSelectedSlot", -1))), true);
            AppendRaw(builder, "restoredSelectedSlot", SlotRaw(GetStateInt(execution, "RestoredSelectedSlot", -1)), true);
            AppendRaw(builder, "restoredSelectedSlotDisplay", SlotDisplayRaw(GetStateInt(execution, "RestoredSelectedSlot", -1)), true);
            AppendRaw(builder, "selectedSlotRestored", BoolRaw(GetStateBool(execution, "SelectedSlotRestored", false)), true);
            AppendRaw(builder, "targetSlot", SlotRaw(GetStateInt(execution, "TargetSlot", -1)), true);
            AppendRaw(builder, "targetSlotDisplay", SlotDisplayRaw(GetStateInt(execution, "TargetSlot", -1)), true);
            AppendRaw(builder, "targetItemType", IntRaw(GetStateInt(execution, "BridgeAfterItemType", GetStateInt(execution, "TargetItemType", 0))), true);
            AppendRaw(builder, "targetItemFormChangeAttempted", BoolRaw(GetStateBool(execution, "TargetItemFormChangeAttempted", false)), true);
            AppendRaw(builder, "targetItemFormChangeSucceeded", BoolRaw(GetStateBool(execution, "TargetItemFormChangeSucceeded", false)), true);
            AppendRaw(builder, "targetItemFormChangeBeforeType", IntRaw(GetStateInt(execution, "TargetItemFormChangeBeforeType", GetStateInt(execution, "TargetItemTypeBeforeFormChange", 0))), true);
            AppendRaw(builder, "targetItemFormChangeAfterType", IntRaw(GetStateInt(execution, "TargetItemFormChangeAfterType", GetStateInt(execution, "TargetItemType", 0))), true);
            AppendString(builder, "targetItemName", GetState(execution, "TargetItemName", string.Empty), true);
            AppendRaw(builder, "targetItemStack", IntRaw(GetStateInt(execution, "BridgeAfterItemStack", GetStateInt(execution, "TargetItemStack", 0))), true);
            AppendRaw(builder, "itemAnimation", IntRaw(GetStateInt(execution, "BridgeAfterItemAnimation", 0)), true);
            AppendRaw(builder, "itemTime", IntRaw(GetStateInt(execution, "BridgeAfterItemTime", 0)), true);
            AppendRaw(builder, "reuseDelay", IntRaw(GetStateInt(execution, "BridgeAfterReuseDelay", 0)), true);
            AppendRaw(builder, "finalItemAnimation", IntRaw(GetStateInt(execution, "FinalItemAnimation", GetStateInt(execution, "BridgeAfterItemAnimation", 0))), true);
            AppendRaw(builder, "finalItemTime", IntRaw(GetStateInt(execution, "FinalItemTime", GetStateInt(execution, "BridgeAfterItemTime", 0))), true);
            AppendRaw(builder, "finalReuseDelay", IntRaw(GetStateInt(execution, "FinalReuseDelay", GetStateInt(execution, "BridgeAfterReuseDelay", 0))), true);
            AppendRaw(builder, "restoreAttempted", BoolRaw(GetStateBool(execution, "RestoreAttempted", false)), true);
            AppendRaw(builder, "restoreAttemptCount", IntRaw(GetStateInt(execution, "RestoreAttemptCount", 0)), true);
            AppendRaw(builder, "restoreSucceeded", BoolRaw(GetStateBool(execution, "RestoreSucceeded", GetStateBool(execution, "SelectedSlotRestored", false))), true);
            AppendRaw(builder, "restoreWaitTicks", IntRaw(GetStateInt(execution, "RestoreWaitTicks", GetStateInt(execution, "RestoreVerifyWaitTicks", 0))), true);
            AppendRaw(builder, "restoreWaitedForUseIdle", BoolRaw(GetStateBool(execution, "RestoreWaitedForUseIdle", false)), true);
            AppendRaw(builder, "restoreObservedAfterDelay", BoolRaw(GetStateBool(execution, "RestoreObservedAfterDelay", false)), true);
            AppendString(builder, "restoreMethod", GetState(execution, "RestoreMethod", string.Empty), true);
            AppendString(builder, "restoreMessage", GetState(execution, "RestoreMessage", string.Empty), false);
            builder.Append("}");
            return builder.ToString();
        }

        private string BuildVerificationJson(InputActionExecution execution)
        {
            var selectedSlotAtUseStart = GetStateInt(execution, "SelectedSlotAtUseStart", -1);
            var targetSlot = GetStateInt(execution, "TargetSlot", -1);
            var originalSlot = GetStateInt(execution, "OriginalSlot", -1);
            var finalSlot = GetStateInt(execution, "FinalSelectedSlot", GetStateInt(execution, "RestoredSelectedSlot", -1));
            return "{" +
                   "\"itemCheckConsumed\":" + BoolRaw(GetStateBool(execution, "BridgeConsumedByItemCheck", false)) + "," +
                   "\"selectedSlotAtUseStartMatchesTarget\":" + BoolRaw(selectedSlotAtUseStart == targetSlot && targetSlot >= 0) + "," +
                   "\"slotSwitchSucceeded\":" + BoolRaw(GetStateBool(execution, "SlotSwitchSucceeded", false)) + "," +
                   "\"targetItemFormChangeRequested\":" + BoolRaw(GetStateBool(execution, "TargetItemFormChangeRequested", false)) + "," +
                   "\"targetItemFormChangeSucceeded\":" + BoolRaw(GetStateBool(execution, "TargetItemFormChangeSucceeded", false)) + "," +
                   "\"observableChange\":" + BoolRaw(GetStateBool(execution, "BridgeObservableChange", false)) + "," +
                   "\"changedFields\":" + GetState(execution, "BridgeChangedFieldsJson", "[]") + "," +
                   "\"selectedSlotRestored\":" + BoolRaw(GetStateBool(execution, "SelectedSlotRestored", false)) + "," +
                   "\"finalSelectedSlotMatchesOriginal\":" + BoolRaw(originalSlot >= 0 && finalSlot == originalSlot) + "," +
                   "\"buttonClickSuppressedGameInput\":" + BoolRaw(GetStateBool(execution, "UiClickSuppressionSucceeded", false)) + "," +
                   "\"likelyReason\":\"" + EscapeJson(GetState(execution, "LikelyReason", GetState(execution, "RestoreMessage", string.Empty))) + "\"" +
                   "}";
        }

        private bool IsButtonSource(InputActionExecution execution)
        {
            return string.Equals(GetMetadataString(execution, "SourceKind", string.Empty), "Button", StringComparison.OrdinalIgnoreCase);
        }

        private string GetSourceHotkey(InputActionExecution execution)
        {
            return IsButtonSource(execution) ? string.Empty : GetMetadataString(execution, "SourceHotkey", string.Empty);
        }

        private static long GetDurationMs(InputActionExecution execution)
        {
            if (execution == null)
            {
                return 0;
            }

            return (long)(DateTime.UtcNow - execution.StartedUtc).TotalMilliseconds;
        }

        private static InputActionStatus ParseStatus(string value)
        {
            InputActionStatus status;
            return Enum.TryParse(value ?? string.Empty, out status) ? status : InputActionStatus.Failed;
        }

        private static DiagnosticResultCode ParseResultCode(string value)
        {
            DiagnosticResultCode code;
            return Enum.TryParse(value ?? string.Empty, out code) ? code : DiagnosticResultCode.Failed;
        }

        private static bool HasObservableSuccess(ItemUseVerificationState before, ItemUseVerificationState after)
        {
            if (before == null || after == null)
            {
                return false;
            }

            return (before.ItemAnimation <= 0 && after.ItemAnimation > 0) ||
                   (before.ItemTime <= 0 && after.ItemTime > 0) ||
                   after.ReuseDelay > before.ReuseDelay ||
                   after.ItemType != before.ItemType ||
                   after.ItemStack < before.ItemStack ||
                   after.Life > before.Life ||
                   after.Mana > before.Mana ||
                   after.ActiveBuffCount > before.ActiveBuffCount ||
                   after.BuffTimeTotal > before.BuffTimeTotal;
        }

        private static string BuildChangedFieldsJson(ItemUseVerificationState before, ItemUseVerificationState after)
        {
            if (before == null || after == null)
            {
                return "[]";
            }

            var builder = new StringBuilder();
            builder.Append("[");
            var first = true;
            AppendChanged(builder, ref first, "selectedSlot", before.SelectedSlot != after.SelectedSlot);
            AppendChanged(builder, ref first, "itemAnimation", before.ItemAnimation != after.ItemAnimation);
            AppendChanged(builder, ref first, "itemTime", before.ItemTime != after.ItemTime);
            AppendChanged(builder, ref first, "reuseDelay", before.ReuseDelay != after.ReuseDelay);
            AppendChanged(builder, ref first, "targetItemType", before.ItemType != after.ItemType);
            AppendChanged(builder, ref first, "targetItemStack", before.ItemStack != after.ItemStack);
            AppendChanged(builder, ref first, "life", before.Life != after.Life);
            AppendChanged(builder, ref first, "mana", before.Mana != after.Mana);
            AppendChanged(builder, ref first, "activeBuffCount", before.ActiveBuffCount != after.ActiveBuffCount);
            AppendChanged(builder, ref first, "buffTimeTotal", before.BuffTimeTotal != after.BuffTimeTotal);
            builder.Append("]");
            return builder.ToString();
        }

        private static void AppendChanged(StringBuilder builder, ref bool first, string name, bool changed)
        {
            if (!changed)
            {
                return;
            }

            if (!first)
            {
                builder.Append(",");
            }

            builder.Append("\"").Append(name).Append("\"");
            first = false;
        }

        private static void SetState(InputActionExecution execution, string key, int value)
        {
            SetState(execution, key, value.ToString(CultureInfo.InvariantCulture));
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
            if (execution == null || execution.State == null)
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
            return TerrariaInputCompat.IsSupportedItemUseSlot(slot) ? slot.ToString(CultureInfo.InvariantCulture) : "null";
        }

        private static string SlotDisplayRaw(int slot)
        {
            return TerrariaInputCompat.IsSupportedItemUseSlot(slot) ? (slot + 1).ToString(CultureInfo.InvariantCulture) : "null";
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
            builder.Append("\"").Append(EscapeJson(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
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
