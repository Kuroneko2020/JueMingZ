using System.Collections.Generic;

namespace JueMingZ.Automation.Movement
{
    internal sealed class MovementSafeLandingStrategySelection
    {
        public List<MovementSafeLandingStrategyEvaluation> Evaluations { get; private set; }
        public MovementSafeLandingStrategyEvaluation SelectedEvaluation { get; set; }
        public MovementSafeLandingRescuePlan SelectedPlan { get; set; }
        public string CandidateSummary { get; set; }
        public string RejectedStrategiesSummary { get; set; }
        public string EvaluationSummary { get; set; }
        public string SelectedPlanSummary { get; set; }

        public MovementSafeLandingStrategySelection()
        {
            Evaluations = new List<MovementSafeLandingStrategyEvaluation>();
            CandidateSummary = string.Empty;
            RejectedStrategiesSummary = string.Empty;
            EvaluationSummary = string.Empty;
            SelectedPlanSummary = string.Empty;
        }
    }
}
