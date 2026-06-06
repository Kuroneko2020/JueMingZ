using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Common;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class UseSelectedItemActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.ItemUse; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForCombatInput(snapshot))
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.BlockedByUi, "UseSelectedItem 未执行：当前不在世界内，或聊天框、NPC 对话等界面正在阻挡输入。");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, "selected item getter unavailable: " + TerrariaInputCompat.LastInputCompatError);
            }

            var originalSelectedSlot = selectedSlot;
            var targetSlot = HasMetadata(execution, ActionMetadataKeys.TargetSlot)
                ? GetMetadataInt(execution, ActionMetadataKeys.TargetSlot, selectedSlot)
                : selectedSlot;
            // Slot 58 is Terraria's mouse item slot. This executor may profile it,
            // but the actual use still has to go through ItemUseBridge.
            if (!TerrariaInputCompat.IsSupportedItemUseSlot(targetSlot))
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, "UseSelectedItem 当前只支持快捷栏物品和鼠标浮动物品；selected=" + selectedSlot + ", target=" + targetSlot);
            }

            var slotSwitchAttempted = false;
            var slotSwitchSucceeded = false;
            var slotSwitchMethod = string.Empty;
            var slotSwitchBefore = selectedSlot;
            var slotSwitchAfter = selectedSlot;
            var selectedSlotAtUseStart = selectedSlot;
            if (targetSlot != 58 && selectedSlot != targetSlot)
            {
                slotSwitchAttempted = true;
                bool selectedImmediately;
                if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, targetSlot, out selectedImmediately))
                {
                    return InputActionExecutionStepResult.Complete(
                        InputActionStatus.Failed,
                        "UseSelectedItem failed to switch to target slot: " + TerrariaInputCompat.LastInputCompatError);
                }

                slotSwitchMethod = TerrariaInputCompat.LastSelectionMethod;
                if (!selectedImmediately)
                {
                    execution.State["PendingTargetSlotSelection"] = "true";
                    execution.State["TargetSlot"] = targetSlot.ToString(CultureInfo.InvariantCulture);
                    execution.State["OriginalSelectedSlot"] = originalSelectedSlot.ToString(CultureInfo.InvariantCulture);
                    execution.State["SlotSwitchAttempted"] = "true";
                    execution.State["SlotSwitchBefore"] = slotSwitchBefore.ToString(CultureInfo.InvariantCulture);
                    execution.State["SlotSwitchMethod"] = slotSwitchMethod ?? string.Empty;
                    execution.State["SlotSelectionWaitTicks"] = "0";
                    return InputActionExecutionStepResult.Running("UseSelectedItem requested target slot; waiting for Terraria selection update.");
                }

                if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlotAtUseStart))
                {
                    return InputActionExecutionStepResult.Complete(
                        InputActionStatus.Failed,
                        "UseSelectedItem cannot verify selected slot after switch: " + TerrariaInputCompat.LastInputCompatError);
                }

                slotSwitchAfter = selectedSlotAtUseStart;
                slotSwitchSucceeded = selectedSlotAtUseStart == targetSlot;
                if (!slotSwitchSucceeded)
                {
                    return InputActionExecutionStepResult.Complete(
                        InputActionStatus.Failed,
                        "UseSelectedItem failed to select target slot. selectedSlot=" + selectedSlotAtUseStart +
                        ", targetSlot=" + targetSlot + ", method=" + slotSwitchMethod + ".");
                }
            }
            else
            {
                slotSwitchSucceeded = targetSlot == 58 || selectedSlotAtUseStart == targetSlot;
            }

            return BeginBridgeAfterSelection(
                execution,
                player,
                targetSlot,
                originalSelectedSlot,
                slotSwitchAttempted,
                slotSwitchSucceeded,
                slotSwitchMethod,
                slotSwitchBefore,
                slotSwitchAfter,
                selectedSlotAtUseStart);
        }

        private InputActionExecutionStepResult BeginBridgeAfterSelection(
            InputActionExecution execution,
            object player,
            int targetSlot,
            int originalSelectedSlot,
            bool slotSwitchAttempted,
            bool slotSwitchSucceeded,
            string slotSwitchMethod,
            int slotSwitchBefore,
            int slotSwitchAfter,
            int selectedSlotAtUseStart)
        {
            ItemUseVerificationState before;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, targetSlot, out before))
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            if (before.ItemType <= 0 || before.ItemStack <= 0)
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, targetSlot == 58 ? "当前鼠标浮动物品为空，无法使用。" : "当前手持格为空，无法使用。");
            }

            var requireUseItemHeld = string.Equals(
                GetMetadataString(execution, "RequireUseItemHeld", string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var allowCombatAim = string.Equals(
                GetMetadataString(execution, "AllowCombatAim", string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var applyMainMouseLeftForItemCheck = string.Equals(
                GetMetadataString(execution, "ApplyMainMouseLeftForItemCheck", string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var allowEarlyItemCheck = string.Equals(
                GetMetadataString(execution, "AllowEarlyItemCheck", string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var restoreSelectedSlotAfterItemUse = string.Equals(
                GetMetadataString(execution, "RestoreSelectedSlotAfterItemUse", string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var restoreSelectedSlotDelayTicks = GetMetadataInt(execution, "RestoreSelectedSlotDelayTicks", 0);
            var requireSelectedSlotUnchanged = string.Equals(
                GetMetadataString(execution, ActionMetadataKeys.RequireSelectedSlotUnchanged, string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var earlyItemCheckWindowTicks = GetMetadataInt(execution, "EarlyItemCheckWindowTicks", 0);
            if (requireUseItemHeld)
            {
                bool useItemHeld;
                if (!TerrariaInputCompat.TryReadUseItemHeld(player, out useItemHeld))
                {
                    return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, "无法读取当前使用输入状态：" + TerrariaInputCompat.LastInputCompatError);
                }

                if (!useItemHeld)
                {
                    return InputActionExecutionStepResult.Complete(InputActionStatus.NotApplicable, "UseSelectedItem 未执行：玩家已松开使用键。");
                }
            }

            float mouseWorldX = 0f;
            float mouseWorldY = 0f;
            int mouseScreenX = 0;
            int mouseScreenY = 0;
            var hasMouseWorldTarget = TryGetMetadataFloatInvariant(execution, ActionMetadataKeys.WorldX, out mouseWorldX) &&
                                      TryGetMetadataFloatInvariant(execution, ActionMetadataKeys.WorldY, out mouseWorldY);
            var hasMouseScreenTarget = !hasMouseWorldTarget &&
                                       TryGetMetadataIntInvariant(execution, ActionMetadataKeys.ScreenX, out mouseScreenX) &&
                                       TryGetMetadataIntInvariant(execution, ActionMetadataKeys.ScreenY, out mouseScreenY);

            var skipSelectInItemCheck = targetSlot != 58 &&
                                        (requireSelectedSlotUnchanged || selectedSlotAtUseStart == targetSlot);
            var options = slotSwitchAttempted ||
                           skipSelectInItemCheck ||
                           requireUseItemHeld ||
                           allowCombatAim ||
                           applyMainMouseLeftForItemCheck ||
                          allowEarlyItemCheck ||
                          restoreSelectedSlotAfterItemUse ||
                          hasMouseWorldTarget ||
                          hasMouseScreenTarget
                ? new ItemUseBridgeOptions
                {
                    SelectedSlotAtUseStart = skipSelectInItemCheck ? selectedSlotAtUseStart : -1,
                    SlotSwitchAttempted = slotSwitchAttempted,
                    SlotSwitchSucceeded = slotSwitchSucceeded,
                    SlotSwitchMethod = slotSwitchMethod ?? string.Empty,
                    SlotSwitchBefore = slotSwitchBefore,
                    SlotSwitchAfter = slotSwitchAfter,
                    SkipSelectInItemCheck = skipSelectInItemCheck,
                    RequireUseItemHeld = requireUseItemHeld,
                    ApplyMainMouseLeftForItemCheck = applyMainMouseLeftForItemCheck,
                    AllowEarlyItemCheck = allowEarlyItemCheck,
                    EarlyItemCheckWindowTicks = earlyItemCheckWindowTicks,
                    RestoreSelectedSlotOverride = -1,
                    AllowCombatAim = allowCombatAim,
                    HasMouseWorldTarget = hasMouseWorldTarget,
                    MouseWorldX = hasMouseWorldTarget ? mouseWorldX : 0f,
                    MouseWorldY = hasMouseWorldTarget ? mouseWorldY : 0f,
                    HasMouseScreenTarget = hasMouseScreenTarget,
                    MouseScreenX = hasMouseScreenTarget ? mouseScreenX : 0,
                    MouseScreenY = hasMouseScreenTarget ? mouseScreenY : 0
                }
                : null;

            string message;
            // Bridge submission is the mutation boundary; selection/options are prepared
            // here, while Player.ItemCheck owns the real use input.
            if (!ItemUseBridge.TryEnqueueUseSelectedItem(
                execution.Request.RequestId,
                execution.Request.SourceFeatureId,
                targetSlot,
                before.ItemType,
                before.ItemStack,
                before.ItemName,
                execution.Request.Timeout,
                originalSelectedSlot,
                InputActionKind.ItemUse,
                GetMetadataString(execution, "Scenario", "CtrlAltL.UseSelectedItem"),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty),
                options,
                out message))
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, message);
            }

            execution.State["BridgeRequestId"] = execution.Request.RequestId.ToString();
            execution.State["TargetSlot"] = targetSlot.ToString();
            execution.State["OriginalSelectedSlot"] = originalSelectedSlot.ToString(CultureInfo.InvariantCulture);
            execution.State["RestoreSelectedSlotAfterItemUse"] = restoreSelectedSlotAfterItemUse ? "true" : "false";
            execution.State["RestoreSelectedSlotDelayTicks"] = restoreSelectedSlotDelayTicks.ToString(CultureInfo.InvariantCulture);
            execution.State["ExpectedItemType"] = before.ItemType.ToString();
            execution.State["ExpectedStack"] = before.ItemStack.ToString();
            return InputActionExecutionStepResult.Running("已请求通过 ItemCheck 使用当前手持物品。");
        }

        public override InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (string.Equals(GetExecutionStateString(execution, "PendingTargetSlotSelection", string.Empty), "true", StringComparison.OrdinalIgnoreCase))
            {
                return WaitForTargetSelectionAndBeginBridge(execution);
            }

            var result = ItemUseBridge.GetResult(execution.Request.RequestId);
            switch (result.Status)
            {
                case ItemUseBridgeStatus.WaitingForItemCheck:
                case ItemUseBridgeStatus.Consumed:
                    return InputActionExecutionStepResult.Running(result.Message);

                case ItemUseBridgeStatus.Succeeded:
                    if (TryWaitBeforeRestoringSelectedSlot(execution))
                    {
                        return InputActionExecutionStepResult.Running("Waiting briefly before restoring selected slot.");
                    }

                    TryRestoreSelectedSlotAfterTerminalResult(execution);
                    SetResultCode(execution, DiagnosticResultCode.Succeeded);
                    MarkActionEventRecorded(execution);
                    return InputActionExecutionStepResult.Complete(InputActionStatus.Succeeded, result.Message);

                case ItemUseBridgeStatus.AttemptedButUnverified:
                    if (TryWaitBeforeRestoringSelectedSlot(execution))
                    {
                        return InputActionExecutionStepResult.Running("Waiting briefly before restoring selected slot.");
                    }

                    TryRestoreSelectedSlotAfterTerminalResult(execution);
                    SetResultCode(execution, DiagnosticResultCode.AttemptedButUnverified);
                    MarkActionEventRecorded(execution);
                    return InputActionExecutionStepResult.Complete(InputActionStatus.AttemptedButUnverified, result.Message);

                case ItemUseBridgeStatus.Expired:
                    if (TryWaitBeforeRestoringSelectedSlot(execution))
                    {
                        return InputActionExecutionStepResult.Running("Waiting briefly before restoring selected slot.");
                    }

                    TryRestoreSelectedSlotAfterTerminalResult(execution);
                    SetResultCode(execution, DiagnosticResultCode.TimedOut);
                    MarkActionEventRecorded(execution);
                    return InputActionExecutionStepResult.Complete(InputActionStatus.TimedOut, result.Message);

                case ItemUseBridgeStatus.Cancelled:
                    if (TryWaitBeforeRestoringSelectedSlot(execution))
                    {
                        return InputActionExecutionStepResult.Running("Waiting briefly before restoring selected slot.");
                    }

                    TryRestoreSelectedSlotAfterTerminalResult(execution);
                    SetResultCode(execution, DiagnosticResultCode.Failed);
                    MarkActionEventRecorded(execution);
                    return InputActionExecutionStepResult.Complete(InputActionStatus.Cancelled, result.Message);

                case ItemUseBridgeStatus.Failed:
                    if (TryWaitBeforeRestoringSelectedSlot(execution))
                    {
                        return InputActionExecutionStepResult.Running("Waiting briefly before restoring selected slot.");
                    }

                    TryRestoreSelectedSlotAfterTerminalResult(execution);
                    SetResultCode(execution, DiagnosticResultCode.Failed);
                    MarkActionEventRecorded(execution);
                    return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, result.Message);

                default:
                    return InputActionExecutionStepResult.Running("Waiting for Player.ItemCheck to consume queued ItemUse.");
            }
        }

        private InputActionExecutionStepResult WaitForTargetSelectionAndBeginBridge(InputActionExecution execution)
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            var targetSlot = GetExecutionStateInt(execution, "TargetSlot", -1);
            var originalSelectedSlot = GetExecutionStateInt(execution, "OriginalSelectedSlot", -1);
            var slotSwitchBefore = GetExecutionStateInt(execution, "SlotSwitchBefore", originalSelectedSlot);
            var slotSwitchMethod = GetExecutionStateString(execution, "SlotSwitchMethod", string.Empty);
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                return InputActionExecutionStepResult.Complete(
                    InputActionStatus.Failed,
                    "UseSelectedItem cannot read selected slot while waiting for target: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (selectedSlot == targetSlot)
            {
                execution.State["PendingTargetSlotSelection"] = "false";
                execution.State["SlotSwitchAfter"] = selectedSlot.ToString(CultureInfo.InvariantCulture);
                execution.State["SlotSwitchSucceeded"] = "true";
                return BeginBridgeAfterSelection(
                    execution,
                    player,
                    targetSlot,
                    originalSelectedSlot,
                    true,
                    true,
                    slotSwitchMethod,
                    slotSwitchBefore,
                    selectedSlot,
                    selectedSlot);
            }

            var waitTicks = GetExecutionStateInt(execution, "SlotSelectionWaitTicks", 0) + 1;
            execution.State["SlotSelectionWaitTicks"] = waitTicks.ToString(CultureInfo.InvariantCulture);
            if (waitTicks > 45)
            {
                return InputActionExecutionStepResult.Complete(
                    InputActionStatus.Failed,
                    "UseSelectedItem failed to select target slot. selectedSlot=" + selectedSlot +
                    ", targetSlot=" + targetSlot + ", method=" + slotSwitchMethod + ".");
            }

            if (waitTicks % 4 == 0)
            {
                bool selectedImmediately;
                if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, targetSlot, out selectedImmediately))
                {
                    return InputActionExecutionStepResult.Complete(
                        InputActionStatus.Failed,
                        "UseSelectedItem failed to request target slot: " + TerrariaInputCompat.LastInputCompatError);
                }

                execution.State["SlotSwitchMethod"] = TerrariaInputCompat.LastSelectionMethod;
            }

            return InputActionExecutionStepResult.Running("UseSelectedItem waiting for target slot selection.");
        }

        public override InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
        {
            if (execution != null && execution.Request != null)
            {
                ItemUseBridge.Cancel(execution.Request.RequestId, reason ?? "UseSelectedItem cancelled.");
            }

            object player;
            if (TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                TerrariaInputCompat.TryReleaseUseItem(player);
                TryRestoreSelectedSlotAfterTerminalResult(execution, player);
            }

            return InputActionExecutionStepResult.Complete(InputActionStatus.Cancelled, reason ?? "UseSelectedItem cancelled.");
        }

        private void TryRestoreSelectedSlotAfterTerminalResult(InputActionExecution execution)
        {
            object player;
            if (TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                TryRestoreSelectedSlotAfterTerminalResult(execution, player);
            }
        }

        private void TryRestoreSelectedSlotAfterTerminalResult(InputActionExecution execution, object player)
        {
            if (execution == null ||
                !string.Equals(GetExecutionStateString(execution, "RestoreSelectedSlotAfterItemUse", string.Empty), "true", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var originalSlot = GetExecutionStateInt(execution, "OriginalSelectedSlot", -1);
            if (player != null && TerrariaInputCompat.IsSupportedItemUseSlot(originalSlot))
            {
                bool selectedImmediately;
                TerrariaInputCompat.TryRequestInventorySlotSelection(player, originalSlot, out selectedImmediately);
            }
        }

        private bool TryWaitBeforeRestoringSelectedSlot(InputActionExecution execution)
        {
            if (execution == null ||
                !string.Equals(GetExecutionStateString(execution, "RestoreSelectedSlotAfterItemUse", string.Empty), "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var delayTicks = GetExecutionStateInt(execution, "RestoreSelectedSlotDelayTicks", 0);
            if (delayTicks <= 0)
            {
                return false;
            }

            var startedAt = GetExecutionStateInt(execution, "RestoreSelectedSlotDelayStartUpdate", -1);
            if (startedAt < 0)
            {
                execution.State["RestoreSelectedSlotDelayStartUpdate"] = execution.UpdateCount.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            return execution.UpdateCount - startedAt < delayTicks;
        }

        private static string GetExecutionStateString(InputActionExecution execution, string key, string fallback)
        {
            if (execution == null || execution.State == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            string value;
            return execution.State.TryGetValue(key, out value) ? value ?? fallback : fallback;
        }

        private static int GetExecutionStateInt(InputActionExecution execution, string key, int fallback)
        {
            var raw = GetExecutionStateString(execution, key, string.Empty);
            int value;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static bool TryGetMetadataFloatInvariant(InputActionExecution execution, string key, out float value)
        {
            value = 0f;
            if (execution == null || execution.Request == null || execution.Request.Metadata == null)
            {
                return false;
            }

            string raw;
            return execution.Request.Metadata.TryGetValue(key, out raw) &&
                   !string.IsNullOrWhiteSpace(raw) &&
                   float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetMetadataIntInvariant(InputActionExecution execution, string key, out int value)
        {
            value = 0;
            if (execution == null || execution.Request == null || execution.Request.Metadata == null)
            {
                return false;
            }

            string raw;
            return execution.Request.Metadata.TryGetValue(key, out raw) &&
                   !string.IsNullOrWhiteSpace(raw) &&
                   int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
