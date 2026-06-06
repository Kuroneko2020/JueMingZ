using JueMingZ.Compat;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class MouseTargetActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.MouseTarget; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (IsBlockedForWorldInput(snapshot))
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.BlockedByUi, "MouseTarget blocked outside active world.");
            }

            int screenX;
            int screenY;
            if (HasMetadata(execution, "ScreenX") && HasMetadata(execution, "ScreenY"))
            {
                screenX = GetMetadataInt(execution, "ScreenX", 0);
                screenY = GetMetadataInt(execution, "ScreenY", 0);
            }
            else if (HasMetadata(execution, "WorldX") && HasMetadata(execution, "WorldY"))
            {
                var worldX = GetMetadataFloat(execution, "WorldX", 0f);
                var worldY = GetMetadataFloat(execution, "WorldY", 0f);
                // MouseTarget may position Terraria's cursor for an owning action,
                // but it must not perform the click or item/tile interaction itself.
                if (!TerrariaInputCompat.TrySetMouseWorldPosition(worldX, worldY))
                {
                    return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
                }

                execution.State["DurationTicks"] = GetMetadataInt(execution, "DurationTicks", 1).ToString();
                return InputActionExecutionStepResult.Running("MouseTarget world position applied.");
            }
            else
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, "MouseTarget requires ScreenX/ScreenY or WorldX/WorldY metadata.");
            }

            // Screen-position requests follow the same boundary: set only the cursor
            // target, leaving use/click semantics to the queued executor that owns them.
            if (!TerrariaInputCompat.TrySetMouseScreenPosition(screenX, screenY))
            {
                return InputActionExecutionStepResult.Complete(InputActionStatus.Failed, TerrariaInputCompat.LastInputCompatError);
            }

            execution.State["DurationTicks"] = GetMetadataInt(execution, "DurationTicks", 1).ToString();
            return InputActionExecutionStepResult.Running("MouseTarget positioned.");
        }

        public override InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var durationTicks = GetMetadataInt(execution, "DurationTicks", 1);
            return execution.UpdateCount >= durationTicks
                ? InputActionExecutionStepResult.Complete(InputActionStatus.Succeeded, "MouseTarget succeeded.")
                : InputActionExecutionStepResult.Running("MouseTarget running.");
        }
    }
}
