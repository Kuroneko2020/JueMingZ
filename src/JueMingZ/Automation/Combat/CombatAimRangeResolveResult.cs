namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimRangeResolveResult
    {
        public bool Enabled { get; set; }
        public string RangeMode { get; set; }
        public string AimRangeOrigin { get; set; }
        public int RadiusTiles { get; set; }
        public float RadiusPixels { get; set; }
        public float RangeCenterWorldX { get; set; }
        public float RangeCenterWorldY { get; set; }
        public int CursorAimRadius { get; set; }
        public int PlayerAimRadius { get; set; }
        public int PlayerScreenMarginTiles { get; set; }
        public int PlayerScreenRadiusTiles { get; set; }
        public string DisabledReason { get; set; }

        public CombatAimRangeResolveResult()
        {
            RangeMode = string.Empty;
            AimRangeOrigin = string.Empty;
            DisabledReason = string.Empty;
        }
    }
}
