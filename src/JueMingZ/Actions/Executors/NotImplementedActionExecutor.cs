using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class NotImplementedActionExecutor : InputActionExecutorBase
    {
        // Unimplemented action kinds fail closed as terminal results; they must
        // not be interpreted as generic RawInput or best-effort executor slots.
        private readonly InputActionKind _kind;

        public NotImplementedActionExecutor(InputActionKind kind)
        {
            _kind = kind;
        }

        public override InputActionKind Kind { get { return _kind; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            return InputActionExecutionStepResult.Complete(
                InputActionStatus.NotImplemented,
                "Action not implemented in current stage: " + _kind);
        }
    }
}
