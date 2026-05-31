namespace JueMingZ.Compat
{
    public sealed class TeleportRodCorrectionPlan
    {
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public float OriginalMouseWorldX { get; set; }
        public float OriginalMouseWorldY { get; set; }
        public int OriginalMouseScreenX { get; set; }
        public int OriginalMouseScreenY { get; set; }
        public float RawTopLeftX { get; set; }
        public float RawTopLeftY { get; set; }
        public float OriginalTopLeftX { get; set; }
        public float OriginalTopLeftY { get; set; }
        public bool OriginalSafe { get; set; }
        public string OriginalUnsafeReason { get; set; }
        public int SearchRadiusPixels { get; set; }
        public int SearchStepPixels { get; set; }
        public int CandidateCount { get; set; }
        public int ValidCandidateCount { get; set; }
        public float NearestCandidateDistance { get; set; }
        public float CorrectedTopLeftX { get; set; }
        public float CorrectedTopLeftY { get; set; }
        public float CorrectedMouseWorldX { get; set; }
        public float CorrectedMouseWorldY { get; set; }
        public int CorrectedMouseScreenX { get; set; }
        public int CorrectedMouseScreenY { get; set; }
        public bool HasCorrection { get; set; }
        public bool MouseCaptureSucceeded { get; set; }
        public bool MouseApplySucceeded { get; set; }
        public bool MouseRestoreSucceeded { get; set; }
        public string SkipReason { get; set; }
        public string Message { get; set; }
        public string CompatError { get; set; }

        public TeleportRodCorrectionPlan()
        {
            ItemName = string.Empty;
            OriginalUnsafeReason = string.Empty;
            SearchStepPixels = 8;
            NearestCandidateDistance = -1f;
            CorrectedMouseScreenX = -1;
            CorrectedMouseScreenY = -1;
            SkipReason = string.Empty;
            Message = string.Empty;
            CompatError = string.Empty;
        }
    }
}
