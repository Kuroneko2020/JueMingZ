using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class MouseTargetDryRunActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.MouseTargetDryRun; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            if (IsBlockedForWorldInput(snapshot))
            {
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.BlockedByUi,
                    DiagnosticResultCode.BlockedByUi,
                    "MouseTarget dry-run 未执行：当前不在世界内，或聊天框、箱子、NPC 对话等界面正在阻挡输入。",
                    null,
                    null,
                    false,
                    false,
                    false);
            }

            MouseTargetInputState before;
            if (!TerrariaInputCompat.TryCaptureMouseTargetState(out before))
            {
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.Failed,
                    DiagnosticResultCode.Failed,
                    TerrariaInputCompat.LastInputCompatError,
                    null,
                    null,
                    false,
                    false,
                    false);
            }

            if (!TerrariaInputCompat.TryApplyMouseTargetOverride(before))
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(before);
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.Failed,
                    DiagnosticResultCode.Failed,
                    TerrariaInputCompat.LastInputCompatError,
                    before,
                    null,
                    true,
                    false,
                    false);
            }

            var restored = TerrariaInputCompat.TryRestoreMouseTargetState(before);
            MouseTargetInputState after;
            TerrariaInputCompat.TryCaptureMouseTargetState(out after);

            var status = restored ? InputActionStatus.Succeeded : InputActionStatus.Failed;
            var resultCode = restored ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.Failed;
            var optionalFieldsAvailable = before.PlayerInputMouseCaptured && before.TileTargetCaptured;
            var message = restored
                ? "鼠标干跑成功：已捕获、临时覆盖并恢复鼠标状态；不会点击、不会改变世界。" +
                  (optionalFieldsAvailable ? string.Empty : " 核心鼠标坐标已恢复，部分可选字段不可用。")
                : "MouseTarget dry-run 恢复鼠标状态失败：" + TerrariaInputCompat.LastInputCompatError;
            return Finish(execution, startedUtc, status, resultCode, message, before, after, true, true, restored);
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode resultCode,
            string message,
            MouseTargetInputState before,
            MouseTargetInputState after,
            bool captured,
            bool overrideApplied,
            bool restored)
        {
            SetResultCode(execution, resultCode);
            MarkActionEventRecorded(execution);
            MouseTargetDiagnostics.Record(status.ToString(), resultCode.ToString(), message);
            var beforeJson = before == null
                ? "{}"
                : DiagnosticActionRecorder.BuildMouseStateJson(before.MouseX, before.MouseY, before.PlayerInputMouseCaptured, before.PlayerInputMouseX, before.PlayerInputMouseY, before.TileTargetCaptured, before.TileTargetX, before.TileTargetY);
            var afterJson = after == null
                ? "{}"
                : DiagnosticActionRecorder.BuildMouseStateJson(after.MouseX, after.MouseY, after.PlayerInputMouseCaptured, after.PlayerInputMouseX, after.PlayerInputMouseY, after.TileTargetCaptured, after.TileTargetX, after.TileTargetY);
            var verification = "{" +
                               "\"captured\":" + (captured ? "true" : "false") + "," +
                               "\"overrideApplied\":" + (overrideApplied ? "true" : "false") + "," +
                               "\"restored\":" + (restored ? "true" : "false") + "," +
                               "\"worldChanged\":false," +
                               "\"changedFields\":" + BuildChangedFieldsJson(before, after) +
                               "}";
            DiagnosticActionRecorder.RecordCustomEvent(
                execution.Request.RequestId,
                GetMetadataString(execution, "Scenario", "CtrlAltT.MouseTargetDryRun"),
                InputActionKind.MouseTargetDryRun.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                resultCode.ToString(),
                message,
                (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds,
                beforeJson,
                afterJson,
                verification,
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));
            return InputActionExecutionStepResult.Complete(status, message);
        }

        private static string BuildChangedFieldsJson(MouseTargetInputState before, MouseTargetInputState after)
        {
            if (before == null || after == null)
            {
                return "[]";
            }

            var builder = new System.Text.StringBuilder();
            builder.Append("[");
            var first = true;
            AppendChanged(builder, ref first, "mouseX", before.MouseX != after.MouseX);
            AppendChanged(builder, ref first, "mouseY", before.MouseY != after.MouseY);
            AppendChanged(builder, ref first, "playerInputMouseX", before.PlayerInputMouseCaptured && after.PlayerInputMouseCaptured && before.PlayerInputMouseX != after.PlayerInputMouseX);
            AppendChanged(builder, ref first, "playerInputMouseY", before.PlayerInputMouseCaptured && after.PlayerInputMouseCaptured && before.PlayerInputMouseY != after.PlayerInputMouseY);
            AppendChanged(builder, ref first, "tileTargetX", before.TileTargetCaptured && after.TileTargetCaptured && before.TileTargetX != after.TileTargetX);
            AppendChanged(builder, ref first, "tileTargetY", before.TileTargetCaptured && after.TileTargetCaptured && before.TileTargetY != after.TileTargetY);
            builder.Append("]");
            return builder.ToString();
        }

        private static void AppendChanged(System.Text.StringBuilder builder, ref bool first, string name, bool changed)
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
    }
}
