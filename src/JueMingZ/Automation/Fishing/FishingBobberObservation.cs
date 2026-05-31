namespace JueMingZ.Automation.Fishing
{
    internal sealed class FishingBobberObservation
    {
        public long GameUpdateCount { get; set; }
        public int Identity { get; set; }
        public int WhoAmI { get; set; }
        public int Type { get; set; }
        public int Owner { get; set; }
        public bool Active { get; set; }
        public bool Bobber { get; set; }
        public bool InLiquid { get; set; }
        public bool LiquidStateKnown { get; set; }
        public FishingLiquidKind LiquidKind { get; set; }
        public float Ai1 { get; set; }
        public float LocalAi1 { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
    }
}
