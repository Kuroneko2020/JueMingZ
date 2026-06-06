using System;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class SelectHotbarSlotActionExecutor : InputActionExecutorBase
    {
        private const string StageWaitTarget = "WaitTarget";
        private const string StageHoldTarget = "HoldTarget";
        private const string StageWaitRestore = "WaitRestore";
        private const int MaxSelectionWaitTicks = 45;
        private const int DefaultFishingBobberGoneWaitTicks = 90;

        public override InputActionKind Kind { get { return InputActionKind.SelectHotbarSlot; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForWorldInput(snapshot))
            {
                return Complete(execution, InputActionStatus.BlockedByUi, "SelectHotbarSlot 未执行：当前不在世界内，或聊天框、箱子、NPC 对话等界面正在阻挡输入。");
            }

            var slot = GetMetadataInt(execution, "Slot", 0);
            if (slot < 0 || slot > 9)
            {
                return Complete(execution, InputActionStatus.Failed, "Hotbar slot out of range: " + slot);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return Complete(execution, InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            int previous;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out previous))
            {
                return Complete(
                    execution,
                    InputActionStatus.Failed,
                    "selected item getter unavailable: " + TerrariaInputCompat.LastInputCompatError);
            }

            execution.State["PreviousSlot"] = previous.ToString();
            execution.State["TargetSlot"] = slot.ToString();
            execution.State["RestoreAfterTicks"] = GetMetadataInt(execution, "RestoreAfterTicks", 30).ToString();
            execution.State["KeepSelected"] = ShouldKeepSelected(execution) ? "true" : "false";

            return RequestTargetSelection(execution, player, slot);
        }

        public override InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var stage = GetStateString(execution, "Stage", StageWaitTarget);
            if (string.Equals(stage, StageWaitTarget, StringComparison.Ordinal))
            {
                return WaitForTargetSelection(execution);
            }

            if (string.Equals(stage, StageHoldTarget, StringComparison.Ordinal))
            {
                return HoldTargetBeforeRestore(execution);
            }

            if (string.Equals(stage, StageWaitRestore, StringComparison.Ordinal))
            {
                return WaitForRestoreSelection(execution);
            }

            return WaitForTargetSelection(execution);
        }

        private InputActionExecutionStepResult RequestTargetSelection(InputActionExecution execution, object player, int slot)
        {
            // Hotbar selection is asynchronous Terraria input state. Request first,
            // then verify the reported selected slot before use or restore.
            if (ShouldPreferImmediateSelection(execution) &&
                TerrariaInputCompat.TryForceInventorySlotSelection(player, slot))
            {
                execution.State["SelectionMethod"] = TerrariaInputCompat.LastSelectionMethod;
                execution.State["Stage"] = StageHoldTarget;
                execution.State["TargetSelectionWaitTicks"] = "0";
                return CompleteTargetSelection(execution, slot);
            }

            bool selectedImmediately;
            if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, slot, out selectedImmediately))
            {
                return Complete(execution, InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            execution.State["SelectionMethod"] = TerrariaInputCompat.LastSelectionMethod;
            execution.State["Stage"] = selectedImmediately ? StageHoldTarget : StageWaitTarget;
            execution.State["TargetSelectionWaitTicks"] = "0";
            if (selectedImmediately)
            {
                return CompleteTargetSelection(execution, slot);
            }

            return InputActionExecutionStepResult.Running("SelectHotbarSlot requested target slot; waiting for Terraria selection update.");
        }

        private InputActionExecutionStepResult WaitForTargetSelection(InputActionExecution execution)
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return Complete(execution, InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            var targetSlot = GetStateInt(execution, "TargetSlot", GetMetadataInt(execution, "Slot", 0));
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                return Complete(execution, InputActionStatus.Failed, "SelectHotbarSlot cannot read selected slot: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (selectedSlot == targetSlot)
            {
                return CompleteTargetSelection(execution, targetSlot);
            }

            var waitTicks = GetStateInt(execution, "TargetSelectionWaitTicks", 0) + 1;
            execution.State["TargetSelectionWaitTicks"] = waitTicks.ToString();
            if (waitTicks > MaxSelectionWaitTicks)
            {
                return Complete(execution, InputActionStatus.Failed, "SelectHotbarSlot target slot was not selected. selectedSlot=" + selectedSlot + ", targetSlot=" + targetSlot + ".");
            }

            if (waitTicks % 4 == 0)
            {
                bool selectedImmediately;
                if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, targetSlot, out selectedImmediately))
                {
                    return Complete(execution, InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
                }

                execution.State["SelectionMethod"] = TerrariaInputCompat.LastSelectionMethod;
            }

            return InputActionExecutionStepResult.Running("SelectHotbarSlot waiting for target selection.");
        }

        private InputActionExecutionStepResult CompleteTargetSelection(InputActionExecution execution, int targetSlot)
        {
            execution.State["TargetSelectedAtUpdate"] = execution.UpdateCount.ToString();
            if (ShouldKeepSelected(execution))
            {
                return Complete(execution, InputActionStatus.Succeeded, "已切换到测试快捷栏第 " + (targetSlot + 1) + " 格。");
            }

            execution.State["Stage"] = StageHoldTarget;
            return InputActionExecutionStepResult.Running("已临时切换到测试快捷栏第 " + (targetSlot + 1) + " 格。");
        }

        private InputActionExecutionStepResult HoldTargetBeforeRestore(InputActionExecution execution)
        {
            var restoreAfterTicks = GetMetadataInt(execution, "RestoreAfterTicks", 30);
            var targetSelectedAt = GetStateInt(execution, "TargetSelectedAtUpdate", execution.UpdateCount);
            var heldTicks = execution.UpdateCount - targetSelectedAt;
            if (ShouldHoldUntilFishingBobberGone(execution))
            {
                if (!ShouldRestoreAfterFishingBobberWait(execution, heldTicks))
                {
                    return InputActionExecutionStepResult.Running("SelectHotbarSlot holding target slot until fishing bobber disappears.");
                }
            }
            else if (heldTicks < restoreAfterTicks)
            {
                return InputActionExecutionStepResult.Running("SelectHotbarSlot waiting to restore.");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return Complete(execution, InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            var previous = GetStateInt(execution, "PreviousSlot", 0);
            if (previous < 0 || previous > 9)
            {
                var targetSlot = GetStateInt(execution, "TargetSlot", GetMetadataInt(execution, "Slot", 0));
                return Complete(execution, GetRestoreCompletionStatus(execution), "已临时切换到测试快捷栏第 " + (targetSlot + 1) + " 格。");
            }

            var preferImmediateSelection = ShouldPreferImmediateSelection(execution);
            bool selectedImmediately;
            if (preferImmediateSelection &&
                TerrariaInputCompat.TryForceInventorySlotSelection(player, previous))
            {
                selectedImmediately = true;
            }
            else if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, previous, out selectedImmediately))
            {
                return Complete(
                    execution,
                    InputActionStatus.Failed,
                    "SelectHotbarSlot selected target but failed to request previous slot restore: " + TerrariaInputCompat.LastInputCompatError);
            }

            execution.State["RestoreMethod"] = TerrariaInputCompat.LastSelectionMethod;
            execution.State["RestoreSelectionWaitTicks"] = "0";
            if (selectedImmediately)
            {
                var targetSlot = GetStateInt(execution, "TargetSlot", GetMetadataInt(execution, "Slot", 0));
                return Complete(execution, GetRestoreCompletionStatus(execution), "已临时切换到测试快捷栏第 " + (targetSlot + 1) + " 格，并恢复原手持格。");
            }

            execution.State["Stage"] = StageWaitRestore;
            return InputActionExecutionStepResult.Running("SelectHotbarSlot requested previous slot restore.");
        }

        private InputActionExecutionStepResult WaitForRestoreSelection(InputActionExecution execution)
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return Complete(execution, InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            var previous = GetStateInt(execution, "PreviousSlot", 0);
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                return Complete(execution, InputActionStatus.Failed, "SelectHotbarSlot cannot read selected slot after restore: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (selectedSlot == previous)
            {
                var targetSlot = GetStateInt(execution, "TargetSlot", GetMetadataInt(execution, "Slot", 0));
                return Complete(execution, GetRestoreCompletionStatus(execution), "已临时切换到测试快捷栏第 " + (targetSlot + 1) + " 格，并恢复原手持格。");
            }

            var waitTicks = GetStateInt(execution, "RestoreSelectionWaitTicks", 0) + 1;
            execution.State["RestoreSelectionWaitTicks"] = waitTicks.ToString();
            if (waitTicks > MaxSelectionWaitTicks)
            {
                return Complete(execution, InputActionStatus.Failed, "SelectHotbarSlot previous slot was not restored. selectedSlot=" + selectedSlot + ", previousSlot=" + previous + ".");
            }

            if (waitTicks % 4 == 0)
            {
                bool selectedImmediately;
                if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, previous, out selectedImmediately))
                {
                    return Complete(execution, InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
                }

                execution.State["RestoreMethod"] = TerrariaInputCompat.LastSelectionMethod;
            }

            return InputActionExecutionStepResult.Running("SelectHotbarSlot waiting for previous slot restore.");
        }

        public override InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
        {
            object player;
            if (!ShouldKeepSelected(execution) && TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                int previous;
                if (int.TryParse(execution.State.ContainsKey("PreviousSlot") ? execution.State["PreviousSlot"] : "0", out previous) &&
                    previous >= 0 && previous <= 9)
                {
                    bool selectedImmediately;
                    if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, previous, out selectedImmediately))
                    {
                        Logger.Warn("SelectHotbarSlotActionExecutor", "Cancel restore failed: " + TerrariaInputCompat.LastInputCompatError);
                    }
                }
            }

            return Complete(execution, InputActionStatus.Cancelled, reason ?? "SelectHotbarSlot cancelled.");
        }
        private InputActionExecutionStepResult Complete(InputActionExecution execution, InputActionStatus status, string message)
        {
            if (IsFishingFilterSkip(execution))
            {
                RecordFishingFilterSkipEvent(execution, status, message);
            }

            return InputActionExecutionStepResult.Complete(status, message);
        }

        private static bool IsFishingFilterSkip(InputActionExecution execution)
        {
            return string.Equals(
                GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty),
                ScenarioNames.FishingFilterSkip,
                StringComparison.Ordinal);
        }

        private static void RecordFishingFilterSkipEvent(InputActionExecution execution, InputActionStatus status, string message)
        {
            MarkActionEventRecorded(execution);
            var startedUtc = execution == null ? DateTime.UtcNow : execution.StartedUtc;
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            var resultCode = InputActionResult.MapResultCode(status);
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                ScenarioNames.FishingFilterSkip,
                InputActionKind.SelectHotbarSlot.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                resultCode,
                message ?? string.Empty,
                duration,
                BuildFishingFilterSkipBeforeJson(execution),
                BuildFishingFilterSkipAfterJson(execution, status, message),
                BuildFishingFilterSkipVerificationJson(execution),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));
        }

        private static string BuildFishingFilterSkipBeforeJson(InputActionExecution execution)
        {
            return "{" +
                   "\"targetSlot\":" + GetMetadataInt(execution, "Slot", -1) + "," +
                   "\"previousSlot\":" + EscapeJsonNumber(GetStateString(execution, "PreviousSlot", "-1")) + "," +
                   "\"restoreAfterTicks\":" + GetMetadataInt(execution, "RestoreAfterTicks", 0) + "," +
                   "\"catchKind\":\"" + EscapeJson(GetMetadataString(execution, "FishingFilterCatchKind", string.Empty)) + "\"," +
                   "\"catchId\":" + GetMetadataInt(execution, "FishingFilterCatchId", 0) + "," +
                   "\"catchName\":\"" + EscapeJson(GetMetadataString(execution, "FishingFilterCatchName", string.Empty)) + "\"" +
                   "}";
        }

        private static string BuildFishingFilterSkipAfterJson(InputActionExecution execution, InputActionStatus status, string message)
        {
            return "{" +
                   "\"targetSlot\":" + GetMetadataInt(execution, "Slot", -1) + "," +
                   "\"restoredSlot\":" + EscapeJsonNumber(GetStateString(execution, "PreviousSlot", "-1")) + "," +
                   "\"result\":\"" + EscapeJson(status.ToString()) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildFishingFilterSkipVerificationJson(InputActionExecution execution)
        {
            return "{" +
                   "\"cutRodSkip\":true," +
                   "\"catchKind\":\"" + EscapeJson(GetMetadataString(execution, "FishingFilterCatchKind", string.Empty)) + "\"," +
                   "\"catchId\":" + GetMetadataInt(execution, "FishingFilterCatchId", 0) + "," +
                   "\"catchName\":\"" + EscapeJson(GetMetadataString(execution, "FishingFilterCatchName", string.Empty)) + "\"," +
                   "\"decisionReason\":\"" + EscapeJson(GetMetadataString(execution, "FishingFilterDecisionReason", string.Empty)) + "\"," +
                   "\"matchedRule\":\"" + EscapeJson(GetMetadataString(execution, "FishingFilterMatchedRule", string.Empty)) + "\"," +
                   "\"filterMode\":\"" + EscapeJson(GetMetadataString(execution, "FishingFilterMode", string.Empty)) + "\"," +
                   "\"matchMode\":\"" + EscapeJson(GetMetadataString(execution, "FishingFilterMatchMode", string.Empty)) + "\"," +
                   "\"holdUntilFishingBobberGone\":" + BoolRaw(ShouldHoldUntilFishingBobberGone(execution)) + "," +
                   "\"fishingBobberIdentity\":" + GetMetadataInt(execution, "FishingBobberIdentity", -1) + "," +
                   "\"fishingBobberGoneBeforeRestore\":" + BoolRaw(IsStateTrue(execution, "FishingBobberGoneBeforeRestore")) + "," +
                   "\"fishingBobberGoneWaitTimedOut\":" + BoolRaw(IsStateTrue(execution, "FishingBobberGoneWaitTimedOut")) +
                   "}";
        }

        private static string GetStateString(InputActionExecution execution, string key, string fallback)
        {
            if (execution == null || execution.State == null)
            {
                return fallback;
            }

            string value;
            return execution.State.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }

        private static int GetStateInt(InputActionExecution execution, string key, int fallback)
        {
            int parsed;
            return int.TryParse(GetStateString(execution, key, string.Empty), out parsed) ? parsed : fallback;
        }

        private static string EscapeJsonNumber(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed.ToString() : "-1";
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private static bool ShouldKeepSelected(InputActionExecution execution)
        {
            return string.Equals(
                GetMetadataString(execution, "KeepSelected", string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldPreferImmediateSelection(InputActionExecution execution)
        {
            return string.Equals(
                GetMetadataString(execution, "PreferImmediateSelection", string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldHoldUntilFishingBobberGone(InputActionExecution execution)
        {
            return string.Equals(
                GetMetadataString(execution, "HoldUntilFishingBobberGone", string.Empty),
                "true",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldRestoreAfterFishingBobberWait(InputActionExecution execution, int heldTicks)
        {
            if (execution == null || execution.State == null)
            {
                return true;
            }

            var maxWaitTicks = Math.Max(1, GetMetadataInt(execution, "MaxBobberGoneWaitTicks", DefaultFishingBobberGoneWaitTicks));
            bool gone;
            if (TerrariaFishingCompat.TryIsLocalBobberGone(GetMetadataInt(execution, "FishingBobberIdentity", -1), out gone))
            {
                execution.State["FishingBobberGoneBeforeRestore"] = gone ? "true" : "false";
                if (gone)
                {
                    return true;
                }
            }

            if (heldTicks >= maxWaitTicks)
            {
                execution.State["FishingBobberGoneWaitTimedOut"] = "true";
                execution.State["FishingBobberGoneBeforeRestore"] = "false";
                return true;
            }

            execution.State["FishingBobberGoneWaitTicks"] = heldTicks.ToString();
            return false;
        }

        private static InputActionStatus GetRestoreCompletionStatus(InputActionExecution execution)
        {
            return IsStateTrue(execution, "FishingBobberGoneWaitTimedOut")
                ? InputActionStatus.AttemptedButUnverified
                : InputActionStatus.Succeeded;
        }

        private static bool IsStateTrue(InputActionExecution execution, string key)
        {
            return string.Equals(GetStateString(execution, key, string.Empty), "true", StringComparison.OrdinalIgnoreCase);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

    }
}
