namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimTargetMotionProfile
    {
        public const string Unknown = "Unknown";
        public const string StableLinear = "StableLinear";
        public const string JumpingGrounded = "JumpingGrounded";
        public const string JumpingAirborne = "JumpingAirborne";
        public const string FlyingAccelerating = "FlyingAccelerating";
        public const string HoveringOscillating = "HoveringOscillating";
        public const string TeleportOrDashRecent = "TeleportOrDashRecent";
        public const string LargeOrSegmented = "LargeOrSegmented";

        public string MotionProfileKind { get; set; }
        public float MotionConfidence { get; set; }
        public float VelocityConfidence { get; set; }
        public float AccelerationX { get; set; }
        public float AccelerationY { get; set; }
        public float AccelerationConfidence { get; set; }
        public float RecommendedLeadScale { get; set; }
        public float RecommendedMaxLeadTicks { get; set; }
        public bool PreferCurrentVelocity { get; set; }
        public bool PreferSmoothedVelocity { get; set; }
        public string HistoryResetReason { get; set; }

        public CombatAimTargetMotionProfile()
        {
            MotionProfileKind = Unknown;
            RecommendedLeadScale = 1f;
            HistoryResetReason = string.Empty;
        }

        public CombatAimTargetMotionProfile Clone()
        {
            return new CombatAimTargetMotionProfile
            {
                MotionProfileKind = MotionProfileKind ?? Unknown,
                MotionConfidence = MotionConfidence,
                VelocityConfidence = VelocityConfidence,
                AccelerationX = AccelerationX,
                AccelerationY = AccelerationY,
                AccelerationConfidence = AccelerationConfidence,
                RecommendedLeadScale = RecommendedLeadScale,
                RecommendedMaxLeadTicks = RecommendedMaxLeadTicks,
                PreferCurrentVelocity = PreferCurrentVelocity,
                PreferSmoothedVelocity = PreferSmoothedVelocity,
                HistoryResetReason = HistoryResetReason ?? string.Empty
            };
        }
    }
}
