using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class NpcInteractActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.NpcInteract; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var interaction = GetMetadataString(execution, "Interaction", string.Empty);
            if (!string.Equals(interaction, "NurseHeal", StringComparison.OrdinalIgnoreCase))
            {
                return Finish(execution, startedUtc, InputActionStatus.NotImplemented, DiagnosticResultCode.NotImplemented, "NpcInteract only supports NurseHeal in this build.", null);
            }

            var npcIndex = GetMetadataInt(execution, "NpcIndex", -1);
            NurseHealResult result;
            var changed = NurseServiceCompat.TryOpenAndHeal(npcIndex, out result);
            if (changed)
            {
                return Finish(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, result.Message, result);
            }

            var code = result != null && result.HealInvoked
                ? DiagnosticResultCode.AttemptedButUnverified
                : DiagnosticResultCode.NotApplicable;
            var status = code == DiagnosticResultCode.AttemptedButUnverified
                ? InputActionStatus.AttemptedButUnverified
                : InputActionStatus.NotApplicable;
            return Finish(execution, startedUtc, status, code, result == null ? "Nurse heal did not run." : result.Message, result);
        }

        private InputActionExecutionStepResult Finish(InputActionExecution execution, DateTime startedUtc, InputActionStatus status, DiagnosticResultCode code, string message, NurseHealResult result)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, "Scenario", "AutoRecovery.AutoNurse"),
                InputActionKind.NpcInteract.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildBeforeJson(execution, result),
                BuildAfterJson(execution, result, code.ToString(), message),
                BuildVerificationJson(result),
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private static string BuildBeforeJson(InputActionExecution execution, NurseHealResult result)
        {
            return "{" +
                   "\"interaction\":\"NurseHeal\"," +
                   "\"npcIndex\":" + IntRaw(GetMetadataInt(execution, "NpcIndex", -1)) + "," +
                   "\"lifeBefore\":" + IntRaw(result == null ? 0 : result.LifeBefore) + "," +
                   "\"removableDebuffsBefore\":" + IntRaw(result == null ? 0 : result.RemovableDebuffsBefore) +
                   "}";
        }

        private static string BuildAfterJson(InputActionExecution execution, NurseHealResult result, string resultCode, string message)
        {
            return "{" +
                   "\"interaction\":\"NurseHeal\"," +
                   "\"npcIndex\":" + IntRaw(GetMetadataInt(execution, "NpcIndex", -1)) + "," +
                   "\"healCost\":" + IntRaw(result == null ? 0 : result.HealCost) + "," +
                   "\"lifeAfter\":" + IntRaw(result == null ? 0 : result.LifeAfter) + "," +
                   "\"removableDebuffsAfter\":" + IntRaw(result == null ? 0 : result.RemovableDebuffsAfter) + "," +
                   "\"chatOpened\":" + BoolRaw(result != null && result.ChatOpened) + "," +
                   "\"chatClosed\":" + BoolRaw(result != null && result.ChatClosed) + "," +
                   "\"healInvoked\":" + BoolRaw(result != null && result.HealInvoked) + "," +
                   "\"resultCode\":\"" + EscapeJson(resultCode) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildVerificationJson(NurseHealResult result)
        {
            var lifeChanged = result != null && result.LifeAfter > result.LifeBefore;
            var debuffsRemoved = result != null && result.RemovableDebuffsAfter < result.RemovableDebuffsBefore;
            return "{" +
                   "\"observableChange\":" + BoolRaw(lifeChanged || debuffsRemoved) + "," +
                   "\"changedFields\":" + ((lifeChanged || debuffsRemoved) ? "[\"life\",\"debuffs\"]" : "[]") + "," +
                   "\"nurseChatClosed\":" + BoolRaw(result != null && result.ChatClosed) + "," +
                   "\"nurseHealInvoked\":" + BoolRaw(result != null && result.HealInvoked) +
                   "}";
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
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
