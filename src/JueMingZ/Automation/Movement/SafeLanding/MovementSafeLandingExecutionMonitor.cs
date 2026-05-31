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

            return "requestId=" + result.RequestId +
                   ",kind=" + result.Kind +
                   ",status=" + result.Status +
                   ",resultCode=" + (result.ResultCode ?? string.Empty) +
                   ",message=" + (result.Message ?? string.Empty);
        }
    }
}
