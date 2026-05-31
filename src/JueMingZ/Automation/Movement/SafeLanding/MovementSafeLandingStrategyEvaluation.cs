using JueMingZ.Actions;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Automation.Movement
{
    internal sealed class MovementSafeLandingStrategyEvaluation
    {
        public string StrategyId { get; set; }
        public int Priority { get; set; }
        public string ActionType { get; set; }
        public InputActionKind RequestKind { get; set; }
        public InputActionChannel RequiredChannels { get; set; }
        public string TimingWindow { get; set; }
        public bool IsCandidate { get; set; }
        public bool IsReady { get; set; }
        public bool BlocksLowerPriority { get; set; }
        public bool RequiresTemporaryEquipment { get; set; }
        public bool RequiresRestore { get; set; }
        public string SkipReason { get; set; }
        public string Confidence { get; set; }
        public string Readiness { get; set; }
        public string SortReason { get; set; }
        public MovementSafeLandingEquipmentPlan EquipmentPlan { get; set; }

        public MovementSafeLandingStrategyEvaluation()
        {
            StrategyId = string.Empty;
            Priority = -1;
            ActionType = string.Empty;
            RequestKind = InputActionKind.None;
            RequiredChannels = InputActionChannel.None;
            TimingWindow = string.Empty;
            SkipReason = string.Empty;
            Confidence = string.Empty;
            Readiness = string.Empty;
            SortReason = string.Empty;
        }

        public string ToSummary()
        {
            return Priority.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                   ":" + (StrategyId ?? string.Empty) +
                   ":candidate=" + Bool(IsCandidate) +
                   ":ready=" + Bool(IsReady) +
                   ":action=" + (ActionType ?? string.Empty) +
                   ":skip=" + (SkipReason ?? string.Empty);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
