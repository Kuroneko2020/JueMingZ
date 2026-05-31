namespace JueMingZ.Automation.Fishing.Filtering
{
    internal sealed class FishingFilterDecision
    {
        public bool ShouldKeep { get; set; }

        public string Action { get; set; }

        public string Reason { get; set; }

        public string MatchedRule { get; set; }

        public string MatchMode { get; set; }

        public string FilterMode { get; set; }

        public FishingFilterDecision()
        {
            Action = "Keep";
            Reason = string.Empty;
            MatchedRule = string.Empty;
            MatchMode = string.Empty;
            FilterMode = string.Empty;
        }
    }
}
