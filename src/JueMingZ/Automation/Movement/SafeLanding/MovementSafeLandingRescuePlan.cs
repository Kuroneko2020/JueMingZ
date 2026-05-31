using System;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Automation.Movement
{
    internal sealed class MovementSafeLandingRescuePlan
    {
        public int Priority { get; set; }
        public string StrategyId { get; set; }
        public string ActionType { get; set; }
        public InputActionKind RequestKind { get; set; }
        public InputActionPriority RequestPriority { get; set; }
        public TimeSpan Timeout { get; set; }
        public TimeSpan QueueTimeout { get; set; }
        public InputActionChannel RequiredChannels { get; set; }
        public string ExpectedVerification { get; set; }
        public string RestorePolicy { get; set; }
        public string ExpiryCondition { get; set; }
        public string MetadataSummary { get; set; }
        public bool IsNoAction { get; set; }
        public bool IsReady { get; set; }
        public bool RequiresTemporaryEquipment { get; set; }
        public bool RequiresRestore { get; set; }
        public MovementSafeLandingEquipmentPlan EquipmentPlan { get; set; }

        public MovementSafeLandingRescuePlan()
        {
            StrategyId = string.Empty;
            ActionType = string.Empty;
            RequestKind = InputActionKind.None;
            RequestPriority = InputActionPriority.Normal;
            Timeout = TimeSpan.Zero;
            QueueTimeout = TimeSpan.Zero;
            RequiredChannels = InputActionChannel.None;
            ExpectedVerification = string.Empty;
            RestorePolicy = string.Empty;
            ExpiryCondition = string.Empty;
            MetadataSummary = string.Empty;
            IsReady = true;
        }

        public string ToSummary()
        {
            return "priority=" + Priority.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                   ",strategy=" + (StrategyId ?? string.Empty) +
                   ",action=" + (ActionType ?? string.Empty) +
                   ",kind=" + RequestKind +
                   ",ready=" + (IsReady ? "true" : "false") +
                   ",restore=" + (RequiresRestore ? "true" : "false");
        }
    }
}
