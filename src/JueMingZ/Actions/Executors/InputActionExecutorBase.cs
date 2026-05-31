using System;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public abstract class InputActionExecutorBase : IInputActionExecutor
    {
        public abstract InputActionKind Kind { get; }

        public abstract InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot);

        public virtual InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            return InputActionExecutionStepResult.Complete(InputActionStatus.NotImplemented, "Action executor update not implemented: " + Kind);
        }

        public virtual InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
        {
            return InputActionExecutionStepResult.Complete(InputActionStatus.Cancelled, reason ?? "Action cancelled.");
        }

        protected static bool IsBlockedForWorldInput(GameStateSnapshot snapshot)
        {
            return snapshot == null || !snapshot.IsInWorld || (snapshot.Ui != null && snapshot.Ui.HasBlockingUi);
        }

        protected static bool IsBlockedForRecoveryInventoryUse(GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                return true;
            }

            if (snapshot.Ui == null)
            {
                return false;
            }

            return snapshot.Ui.IsInMainMenu ||
                   snapshot.Ui.ChatOpen ||
                   snapshot.Ui.NpcChatOpen;
        }

        protected static bool IsBlockedForCombatInput(GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                return true;
            }

            if (snapshot.Ui == null)
            {
                return false;
            }

            return snapshot.Ui.IsInMainMenu ||
                   snapshot.Ui.ChatOpen ||
                   snapshot.Ui.NpcChatOpen;
        }

        protected static int GetMetadataInt(InputActionExecution execution, string key, int fallback)
        {
            if (execution == null || execution.Request == null || execution.Request.Metadata == null)
            {
                return fallback;
            }

            string value;
            if (!execution.Request.Metadata.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }

        protected static float GetMetadataFloat(InputActionExecution execution, string key, float fallback)
        {
            if (execution == null || execution.Request == null || execution.Request.Metadata == null)
            {
                return fallback;
            }

            string value;
            if (!execution.Request.Metadata.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            float parsed;
            return float.TryParse(value, out parsed) ? parsed : fallback;
        }

        protected static bool HasMetadata(InputActionExecution execution, string key)
        {
            return execution != null &&
                   execution.Request != null &&
                   execution.Request.Metadata != null &&
                   execution.Request.Metadata.ContainsKey(key);
        }

        protected static void SetResultCode(InputActionExecution execution, DiagnosticResultCode code)
        {
            if (execution == null || execution.State == null)
            {
                return;
            }

            execution.State["ResultCode"] = code.ToString();
        }

        protected static void MarkActionEventRecorded(InputActionExecution execution)
        {
            if (execution == null || execution.State == null)
            {
                return;
            }

            execution.State["ActionEventRecorded"] = "true";
        }

        protected static string GetMetadataString(InputActionExecution execution, string key, string fallback)
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

        protected static InputActionExecutionStepResult CompleteWithCode(
            InputActionExecution execution,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message)
        {
            SetResultCode(execution, code);
            return InputActionExecutionStepResult.Complete(status, message);
        }
    }
}
