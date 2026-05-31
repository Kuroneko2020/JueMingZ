using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public interface IInputActionExecutor
    {
        InputActionKind Kind { get; }
        InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot);
        InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot);
        InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason);
    }
}
