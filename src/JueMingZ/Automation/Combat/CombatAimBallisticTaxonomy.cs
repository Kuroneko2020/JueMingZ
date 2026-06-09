namespace JueMingZ.Automation.Combat
{
    public static class CombatAimBallisticSolverKinds
    {
        public const string PointAim = "PointAimSolver";
        public const string LinearIntercept = "LinearInterceptSolver";
        public const string SlowProjectile = "SlowProjectileSolver";
        public const string GravityArc = "GravityArcSolver";
        public const string ReturningProjectile = "ReturningProjectileSolver";
        public const string Spread = "SpreadSolver";
        public const string FallbackCenter = "FallbackCenterSolver";
    }

    public static class CombatAimLeadWindowKinds
    {
        public const string PointShort = "PointShort";
        public const string HighSpeedShort = "HighSpeedShort";
        public const string Medium = "Medium";
        public const string SlowLong = "SlowLong";
        public const string GravityArc = "GravityArc";
        public const string ReturningOutbound = "ReturningOutbound";
        public const string SpreadCoverage = "SpreadCoverage";
        public const string Fallback = "Fallback";
    }

    public static class CombatAimLeadClampReasons
    {
        public const string None = "none";
        public const string FixedPointLead = "fixedPointLead";
        public const string ProjectileFamilyWindow = "projectileFamilyWindow";
        public const string MotionLeadScale = "motionLeadScale";
        public const string MotionRecommendedMaxLead = "motionRecommendedMaxLead";
        public const string PredictionConfidence = "predictionConfidence";
        public const string ProjectileProfileDegraded = "projectileProfileDegraded";
        public const string GravityCompensationCap = "gravityCompensationCap";
        public const string CenterFallback = "centerFallback";
    }

    public static class CombatAimPredictionConfidenceKinds
    {
        public const string Unknown = "Unknown";
        public const string VeryLow = "VeryLow";
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";
    }
}
