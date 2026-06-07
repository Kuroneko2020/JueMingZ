using JueMingZ.Config;

namespace JueMingZ.Automation.Movement
{
    // Strategy context is immutable read evidence; selected plans must still be turned into ActionQueue requests.
    internal sealed class MovementSafeLandingStrategyContext
    {
        public AppSettings Settings { get; set; }
        public MovementSafeLandingAnalysis Analysis { get; set; }
        public MovementSafeLandingHazardContext Hazard { get; set; }
        public MovementSafeLandingCapabilitySnapshot Capability { get; set; }
        public MovementSafeLandingEquipmentPlan TemporaryEquipmentPlan { get; set; }
        public string TemporaryEquipmentPlanMessage { get; set; }

        public MovementSafeLandingStrategyContext()
        {
            TemporaryEquipmentPlanMessage = string.Empty;
        }

        public static MovementSafeLandingStrategyContext FromAnalysis(AppSettings settings, MovementSafeLandingAnalysis analysis)
        {
            return new MovementSafeLandingStrategyContext
            {
                Settings = settings ?? AppSettings.CreateDefault(),
                Analysis = analysis,
                Hazard = MovementSafeLandingHazardContext.FromAnalysis(analysis),
                Capability = MovementSafeLandingCapabilitySnapshot.FromAnalysis(analysis)
            };
        }
    }
}
