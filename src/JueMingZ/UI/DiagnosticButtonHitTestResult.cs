namespace JueMingZ.UI
{
    public sealed class DiagnosticButtonHitTestResult
    {
        public DiagnosticTestButton Button { get; set; }
        public string HitTestMode { get; set; }
        public int HitTestX { get; set; }
        public int HitTestY { get; set; }
        public bool HitTestConflict { get; set; }
        public string CandidateSummary { get; set; }
        public int VisualRectX { get; set; }
        public int VisualRectY { get; set; }
        public int VisualRectWidth { get; set; }
        public int VisualRectHeight { get; set; }
        public int HitRectX { get; set; }
        public int HitRectY { get; set; }
        public int HitRectWidth { get; set; }
        public int HitRectHeight { get; set; }

        public bool HasButton
        {
            get { return Button != null; }
        }

        public DiagnosticButtonHitTestResult()
        {
            HitTestMode = "none";
            HitTestX = -1;
            HitTestY = -1;
            CandidateSummary = string.Empty;
            VisualRectX = -1;
            VisualRectY = -1;
            HitRectX = -1;
            HitRectY = -1;
        }
    }
}
