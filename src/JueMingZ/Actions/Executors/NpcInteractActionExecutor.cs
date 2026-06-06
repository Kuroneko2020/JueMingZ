using System;
using System.Globalization;
using JueMingZ.Common;
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
            if (string.Equals(interaction, "NurseHeal", StringComparison.OrdinalIgnoreCase))
            {
                return RunNurseHeal(execution, startedUtc);
            }

            if (string.Equals(interaction, "TaxCollect", StringComparison.OrdinalIgnoreCase))
            {
                return RunTaxCollect(execution, startedUtc);
            }

            return FinishUnsupported(execution, startedUtc, interaction);
        }

        private InputActionExecutionStepResult RunNurseHeal(InputActionExecution execution, DateTime startedUtc)
        {
            var npcIndex = GetMetadataInt(execution, "NpcIndex", -1);
            NurseHealResult result;
            // NPC actions must use the vanilla dialog/service compat flow; this executor
            // must not edit NPC service results or player stats directly.
            var changed = NurseServiceCompat.TryOpenAndHeal(npcIndex, out result);
            if (changed)
            {
                return FinishNurse(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, result.Message, result);
            }

            var code = result != null && result.HealInvoked
                ? DiagnosticResultCode.AttemptedButUnverified
                : DiagnosticResultCode.NotApplicable;
            var status = code == DiagnosticResultCode.AttemptedButUnverified
                ? InputActionStatus.AttemptedButUnverified
                : InputActionStatus.NotApplicable;
            return FinishNurse(execution, startedUtc, status, code, result == null ? "Nurse heal did not run." : result.Message, result);
        }

        private InputActionExecutionStepResult RunTaxCollect(InputActionExecution execution, DateTime startedUtc)
        {
            var npcIndex = GetMetadataInt(execution, "NpcIndex", -1);
            TaxCollectResult result;
            // Tax collection follows the same NPC service boundary; collected value is
            // verified from the vanilla service result, not by editing NPC state.
            var changed = TaxCollectorServiceCompat.TryOpenAndCollect(npcIndex, out result);
            if (changed)
            {
                return FinishTaxCollect(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, result.Message, result);
            }

            var code = result != null && result.CollectInvoked
                ? DiagnosticResultCode.AttemptedButUnverified
                : DiagnosticResultCode.NotApplicable;
            var status = code == DiagnosticResultCode.AttemptedButUnverified
                ? InputActionStatus.AttemptedButUnverified
                : InputActionStatus.NotApplicable;
            return FinishTaxCollect(execution, startedUtc, status, code, result == null ? "Tax collect did not run." : result.Message, result);
        }

        private InputActionExecutionStepResult FinishUnsupported(InputActionExecution execution, DateTime startedUtc, string interaction)
        {
            SetResultCode(execution, DiagnosticResultCode.NotImplemented);
            MarkActionEventRecorded(execution);
            var message = "NpcInteract does not support interaction '" + (interaction ?? string.Empty) + "'.";
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, "Scenario", string.Empty),
                InputActionKind.NpcInteract.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                InputActionStatus.NotImplemented.ToString(),
                DiagnosticResultCode.NotImplemented.ToString(),
                message,
                duration,
                "{\"interaction\":\"" + EscapeJson(interaction) + "\"}",
                "{\"interaction\":\"" + EscapeJson(interaction) + "\",\"resultCode\":\"NotImplemented\"}",
                "{\"observableChange\":false}",
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(InputActionStatus.NotImplemented, message);
        }

        private InputActionExecutionStepResult FinishNurse(InputActionExecution execution, DateTime startedUtc, InputActionStatus status, DiagnosticResultCode code, string message, NurseHealResult result)
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

        private InputActionExecutionStepResult FinishTaxCollect(InputActionExecution execution, DateTime startedUtc, InputActionStatus status, DiagnosticResultCode code, string message, TaxCollectResult result)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, "Scenario", ScenarioNames.NpcAutoTaxCollect),
                InputActionKind.NpcInteract.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildTaxBeforeJson(execution, result),
                BuildTaxAfterJson(execution, result, code.ToString(), message),
                BuildTaxVerificationJson(result),
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

        private static string BuildTaxBeforeJson(InputActionExecution execution, TaxCollectResult result)
        {
            return "{" +
                   "\"interaction\":\"TaxCollect\"," +
                   "\"npcIndex\":" + IntRaw(GetMetadataInt(execution, "NpcIndex", -1)) + "," +
                   "\"npcType\":" + IntRaw(GetMetadataInt(execution, "NpcType", TaxCollectorServiceCompat.TaxCollectorNpcType)) + "," +
                   "\"npcWhoAmI\":" + IntRaw(GetMetadataInt(execution, "NpcWhoAmI", result == null ? -1 : result.WhoAmI)) + "," +
                   "\"taxMoneyBefore\":" + IntRaw(result == null ? GetMetadataInt(execution, "TaxMoneyBefore", 0) : result.TaxMoneyBefore) +
                   "}";
        }

        private static string BuildTaxAfterJson(InputActionExecution execution, TaxCollectResult result, string resultCode, string message)
        {
            return "{" +
                   "\"interaction\":\"TaxCollect\"," +
                   "\"npcIndex\":" + IntRaw(GetMetadataInt(execution, "NpcIndex", -1)) + "," +
                   "\"npcWhoAmI\":" + IntRaw(result == null ? GetMetadataInt(execution, "NpcWhoAmI", -1) : result.WhoAmI) + "," +
                   "\"npcName\":\"" + EscapeJson(result == null ? GetMetadataString(execution, "NpcName", string.Empty) : result.NpcName) + "\"," +
                   "\"taxMoneyAfter\":" + IntRaw(result == null ? 0 : result.TaxMoneyAfter) + "," +
                   "\"chatOpened\":" + BoolRaw(result != null && result.ChatOpened) + "," +
                   "\"chatClosed\":" + BoolRaw(result != null && result.ChatClosed) + "," +
                   "\"collectInvoked\":" + BoolRaw(result != null && result.CollectInvoked) + "," +
                   "\"shoppingSettingsApplied\":" + BoolRaw(result != null && result.ShoppingSettingsApplied) + "," +
                   "\"resultCode\":\"" + EscapeJson(resultCode) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildTaxVerificationJson(TaxCollectResult result)
        {
            var taxCleared = result != null && result.TaxMoneyBefore > 0 && result.TaxMoneyAfter < result.TaxMoneyBefore;
            return "{" +
                   "\"observableChange\":" + BoolRaw(taxCleared) + "," +
                   "\"changedFields\":" + (taxCleared ? "[\"taxMoney\"]" : "[]") + "," +
                   "\"taxCollectorChatClosed\":" + BoolRaw(result != null && result.ChatClosed) + "," +
                   "\"taxCollectorCollectInvoked\":" + BoolRaw(result != null && result.CollectInvoked) + "," +
                   "\"taxMoneyBefore\":" + IntRaw(result == null ? 0 : result.TaxMoneyBefore) + "," +
                   "\"taxMoneyAfter\":" + IntRaw(result == null ? 0 : result.TaxMoneyAfter) +
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
