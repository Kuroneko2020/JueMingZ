namespace JueMingZ.Automation.Combat
{
    // Target selection is aim evidence, not action admission; use-item paths must revalidate the current frame.
    public sealed class CombatAimTargetSelection
    {
        public bool Enabled { get; set; }
        public int RadiusTiles { get; set; }
        public bool TrackDummy { get; set; }
        public bool MarkerEnabled { get; set; }
        public string AimRangeOrigin { get; set; } = string.Empty;
        public string AimTargetPriority { get; set; } = string.Empty;
        public int CursorAimRadius { get; set; }
        public int PlayerAimRadius { get; set; }
        public string ActiveRangeMode { get; set; } = string.Empty;
        public int PlayerScreenMarginTiles { get; set; }
        public int PlayerScreenRadiusTiles { get; set; }
        public bool HasCursorWorld { get; set; }
        public float CursorWorldX { get; set; }
        public float CursorWorldY { get; set; }
        public float RangeCenterWorldX { get; set; }
        public float RangeCenterWorldY { get; set; }
        public int MouseScreenX { get; set; }
        public int MouseScreenY { get; set; }
        public float ScreenPositionX { get; set; }
        public float ScreenPositionY { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public int CandidateCount { get; set; }
        public int CheapCandidateCount { get; set; }
        public int ExpensiveCandidateCount { get; set; }
        public int EvaluatedCandidateCount { get; set; }
        public int InRangeCandidateCount { get; set; }
        public bool LosCacheHit { get; set; }
        public string SkipReason { get; set; } = string.Empty;
        public string ResultCode { get; set; } = "Disabled";
        public CombatTargetSnapshot Target { get; set; }
        public CombatTargetSnapshot BallisticTarget { get; set; }
        public CombatAimBallisticSolution BallisticSolution { get; set; }
        public float CenterDistanceTiles { get; set; }
        public float HitboxDistanceTiles { get; set; }
        public float TargetDistanceFromRangeCenterTiles { get; set; }
        public float TargetScore { get; set; }
        public bool LineClear { get; set; }
        public bool LineClearAvailable { get; set; }
        public float DistanceToPlayerCursorRay { get; set; }
        public bool InForwardCone { get; set; }
        public float PreviousTargetBonus { get; set; }
        public string SelectedSamplePoint { get; set; } = string.Empty;
        public string AttackSamplePoint { get; set; } = string.Empty;
        public string SelectionSamplePoint { get; set; } = string.Empty;
        public string SampleSpace { get; set; } = string.Empty;
        public int LineOfSightRejectedSampleCount { get; set; }
        public int VisibleSampleCount { get; set; }
        public bool NearestHitboxPointPenaltyApplied { get; set; }
        public bool CenterPreferred { get; set; }
        public float SelectedSampleWorldX { get; set; }
        public float SelectedSampleWorldY { get; set; }
        public float PredictedHitboxCenterX { get; set; }
        public float PredictedHitboxCenterY { get; set; }
        public float ProjectileHitRadius { get; set; }
        public string SelectedReason { get; set; } = string.Empty;
        public int LockedTargetId { get; set; } = -1;
        public int LockedTargetType { get; set; }
        public bool LockedTargetStillValid { get; set; }
        public int TargetLockAgeTicks { get; set; }
        public int TargetHoldTicksRemaining { get; set; }
        public string SelectionPurpose { get; set; } = string.Empty;
        public bool SelectionCacheHit { get; set; }
        public string SelectionCacheKey { get; set; } = string.Empty;
        public int PreferredTargetWhoAmI { get; set; } = -1;
        public int PreferredTargetType { get; set; }
        public int MarkerTargetWhoAmI { get; set; } = -1;
        public int MarkerTargetType { get; set; }
        public int AttackTargetWhoAmI { get; set; } = -1;
        public int AttackTargetType { get; set; }
        public bool MarkerAttackTargetMismatch { get; set; }
        public bool MarkerTargetChangedForAttack { get; set; }
        public string MarkerAttackMismatchReason { get; set; } = string.Empty;
        public string DecisionCacheSource { get; set; } = string.Empty;
        public long DecisionCacheAgeTicks { get; set; }
        public string DecisionCacheRevalidationReason { get; set; } = string.Empty;

        public bool HasTarget
        {
            get { return Target != null; }
        }
    }
}
