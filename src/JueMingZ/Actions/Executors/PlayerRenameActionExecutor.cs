using System;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class PlayerRenameActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.PlayerRename; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var requestedName = GetMetadataString(execution, "RequestedName", string.Empty);
            var mode = GetMetadataString(execution, "Mode", string.Empty);
            var allowMultiplayer = string.Equals(GetMetadataString(execution, "AllowMultiplayer", "false"), "true", StringComparison.OrdinalIgnoreCase);

            if (snapshot == null || !snapshot.IsInWorld)
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Player rename requires an active world.", null, requestedName, mode);
            }

            if (snapshot.Ui != null && (snapshot.Ui.IsInMainMenu || snapshot.Ui.ChatOpen || snapshot.Ui.NpcChatOpen))
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Player rename blocked by menu, chat, or NPC chat.", null, requestedName, mode);
            }

            // Player rename is a narrow single-player Compat path for fishing quest
            // state refresh; do not bypass the multiplayer guard from UI/runtime code.
            if (snapshot.NetMode != 0 && !allowMultiplayer)
            {
                var blocked = new PlayerRenameResult
                {
                    NetMode = snapshot.NetMode,
                    RequestedName = requestedName,
                    Message = "Player rename is single-player only in this build."
                };
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, blocked.Message, blocked, requestedName, mode);
            }

            PlayerRenameResult result;
            if (!PlayerRenameCompat.TryRenameLocalPlayer(requestedName, allowMultiplayer, out result))
            {
                var code = result != null && result.NetMode != 0
                    ? DiagnosticResultCode.BlockedByEnvironment
                    : DiagnosticResultCode.Failed;
                return Finish(execution, startedUtc, InputActionStatus.Failed, code, result == null ? "Player rename failed." : result.Message, result, requestedName, mode);
            }

            var changed = result != null && result.PlayerNameChanged;
            var refreshed = result != null && result.AnglerFinishedRefreshed;
            var finalStatus = changed || refreshed ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable;
            var finalCode = changed || refreshed ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable;
            return Finish(execution, startedUtc, finalStatus, finalCode, result == null ? "Player rename completed." : result.Message, result, requestedName, mode);
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            PlayerRenameResult result,
            string requestedName,
            string mode)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.FishingQuickRename),
                InputActionKind.PlayerRename.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildBeforeJson(result, requestedName, mode),
                BuildAfterJson(result, code.ToString(), message),
                BuildVerificationJson(result),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private static string BuildBeforeJson(PlayerRenameResult result, string requestedName, string mode)
        {
            return "{" +
                   "\"mode\":\"" + EscapeJson(mode) + "\"," +
                   "\"requestedName\":\"" + EscapeJson(requestedName) + "\"," +
                   "\"previousName\":\"" + EscapeJson(result == null ? string.Empty : result.PreviousName) + "\"," +
                   "\"anglerFinishedBefore\":" + BoolRaw(result != null && result.AnglerFinishedBefore) + "," +
                   "\"netMode\":" + IntRaw(result == null ? 0 : result.NetMode) +
                   "}";
        }

        private static string BuildAfterJson(PlayerRenameResult result, string resultCode, string message)
        {
            return "{" +
                   "\"finalName\":\"" + EscapeJson(result == null ? string.Empty : result.FinalName) + "\"," +
                   "\"playerNameChanged\":" + BoolRaw(result != null && result.PlayerNameChanged) + "," +
                   "\"renameMethodInvoked\":" + BoolRaw(result != null && result.RenameMethodInvoked) + "," +
                   "\"anglerFinishedAfter\":" + BoolRaw(result != null && result.AnglerFinishedAfter) + "," +
                   "\"anglerFinishedRefreshed\":" + BoolRaw(result != null && result.AnglerFinishedRefreshed) + "," +
                   "\"nameAlreadyFinishedToday\":" + BoolRaw(result != null && result.NameAlreadyFinishedToday) + "," +
                   "\"resultCode\":\"" + EscapeJson(resultCode) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildVerificationJson(PlayerRenameResult result)
        {
            return "{" +
                   "\"observableChange\":" + BoolRaw(result != null && (result.PlayerNameChanged || result.AnglerFinishedRefreshed)) + "," +
                   "\"singlePlayerOnly\":" + BoolRaw(result == null || result.NetMode == 0) + "," +
                   "\"simulatedReenterState\":" + BoolRaw(result != null && result.AnglerFinishedRefreshed) +
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
