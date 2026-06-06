using JueMingZ.Actions;

namespace JueMingZ.Automation.Movement
{
    internal static class MovementSafeLandingExecutionMonitor
    {
        public static string DescribeResult(InputActionResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            // This is diagnostic text only; callers must interpret status and
            // verification separately before clearing rescue or restore state.
            return "requestId=" + result.RequestId +
                   ",kind=" + result.Kind +
                   ",status=" + result.Status +
                   ",resultCode=" + (result.ResultCode ?? string.Empty) +
                   ",message=" + (result.Message ?? string.Empty);
        }
    }
}
