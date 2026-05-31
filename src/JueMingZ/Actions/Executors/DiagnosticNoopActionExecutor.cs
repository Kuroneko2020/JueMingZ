using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class DiagnosticNoopActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.DiagnosticNoop; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            return InputActionExecutionStepResult.Complete(InputActionStatus.Succeeded, "空动作成功：动作队列可以接收并完成诊断请求。");
        }
    }
}
