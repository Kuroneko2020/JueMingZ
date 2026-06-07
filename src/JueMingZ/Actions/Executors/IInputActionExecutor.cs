using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public interface IInputActionExecutor
    {
        // Start/Update/Cancel form the mutation cleanup contract for queue-owned
        // work; callers must not bypass Cancel when an action is timed out.
        InputActionKind Kind { get; }
        InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot);
        InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot);
        InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason);
    }
}
