namespace JueMingZ.Automation.Movement
{
    internal sealed class MovementSafeLandingHazardContext
    {
        public bool InWorld { get; set; }
        public bool PlayerControllable { get; set; }
        public bool TextInputFocused { get; set; }
        public string TextInputReason { get; set; }
        public bool Falling { get; set; }
        public bool FallStartKnown { get; set; }
        public float EstimatedFallTiles { get; set; }
        public bool Dangerous { get; set; }
        public bool AlreadySafe { get; set; }
        public string SafeReason { get; set; }
        public bool ImpactFound { get; set; }
        public float ImpactTicks { get; set; }
        public int ImpactDistancePixels { get; set; }
        public float FallingSpeed { get; set; }
        public float GravityDirection { get; set; }
        public bool ControlDown { get; set; }
        public float ImpactWorldX { get; set; }
        public float ImpactWorldY { get; set; }

        public MovementSafeLandingHazardContext()
        {
            InWorld = true;
            TextInputReason = string.Empty;
            SafeReason = string.Empty;
            ImpactTicks = -1f;
            ImpactDistancePixels = -1;
            GravityDirection = 1f;
        }

        public static MovementSafeLandingHazardContext FromAnalysis(MovementSafeLandingAnalysis analysis)
        {
            var context = new MovementSafeLandingHazardContext();
            if (analysis == null)
            {
                context.InWorld = false;
                return context;
            }

            context.PlayerControllable = analysis.PlayerControllable;
            context.TextInputFocused = analysis.TextInputFocused;
            context.TextInputReason = analysis.TextInputReason ?? string.Empty;
            context.Falling = analysis.FallingSpeed > 0.001f;
            context.FallStartKnown = analysis.FallStartKnown;
            context.EstimatedFallTiles = analysis.EstimatedFallTiles;
            context.Dangerous = analysis.Dangerous;
            context.AlreadySafe = analysis.AlreadySafe;
            context.SafeReason = analysis.SafeReason ?? string.Empty;
            context.ImpactFound = analysis.ImpactFound;
            context.ImpactTicks = analysis.ImpactTicks;
            context.ImpactDistancePixels = analysis.ImpactDistancePixels;
            context.FallingSpeed = analysis.FallingSpeed;
            context.GravityDirection = analysis.GravityDirection;
            context.ControlDown = analysis.ControlDown;
            context.ImpactWorldX = analysis.ImpactWorldX;
            context.ImpactWorldY = analysis.ImpactWorldY;
            return context;
        }
    }
}
