namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimBallisticSolution
    {
        public bool Solved { get; set; }
        public string Mode { get; set; }
        public string FallbackReason { get; set; }
        public string SolverKind { get; set; }
        public string LeadWindowKind { get; set; }
        public string LeadClampReason { get; set; }
        public string PredictionConfidence { get; set; }
        public int ProjectileType { get; set; }
        public string ProjectileName { get; set; }
        public int WeaponShootProjectileType { get; set; }
        public string WeaponShootProjectileName { get; set; }
        public string ResolvedProjectileRole { get; set; }
        public int SecondaryProjectileType { get; set; }
        public string SecondaryProjectileName { get; set; }
        public int ProjectileAiStyle { get; set; }
        public int ProjectileExtraUpdates { get; set; }
        public bool ProjectileDefaultsAvailable { get; set; }
        public bool ProjectileNoGravity { get; set; }
        public bool ProjectileArrow { get; set; }
        public bool ProjectileTileCollide { get; set; }
        public int ProjectileWidth { get; set; }
        public int ProjectileHeight { get; set; }
        public bool ProjectileFriendly { get; set; }
        public bool ProjectileHostile { get; set; }
        public float BaseProjectileSpeed { get; set; }
        public int EffectiveUpdatesPerTick { get; set; }
        public float GravityPerTickCandidate { get; set; }
        public float ProjectileRadiusForHit { get; set; }
        public string ProjectileProfileFamily { get; set; }
        public string ProjectileProfileStatus { get; set; }
        public string ProjectileProfileDegradedReason { get; set; }
        public string ProjectileProfileSpeedSource { get; set; }
        public bool ProjectileProfileGunProj { get; set; }
        public bool ProjectileProfileAmmoSpeedApplied { get; set; }
        public bool ProjectileProfileMagicQuiverApplied { get; set; }
        public bool ProjectileProfileArcheryApplied { get; set; }
        public bool ProjectileProfileArcherySpeedCapped { get; set; }
        public bool ProjectileProfileMagicQuiverEffectiveUpdateApplied { get; set; }
        public bool ProjectileProfileSpecificLauncherAmmoProjectileMatch { get; set; }
        public string ProjectileProfileTransformRole { get; set; }
        public int AmmoType { get; set; }
        public int AmmoItemType { get; set; }
        public string AmmoItemName { get; set; }
        public int AmmoProjectileType { get; set; }
        public string AmmoProjectileName { get; set; }
        public int PrimaryProjectileType { get; set; }
        public string PrimaryProjectileName { get; set; }
        public string PrimaryProjectileRole { get; set; }
        public int AmmoSlot { get; set; }
        public float AmmoShootSpeed { get; set; }
        public bool AmmoAvailable { get; set; }
        public bool AmmoArrowLike { get; set; }
        public bool AmmoBulletLike { get; set; }
        public string SecondaryProjectileRole { get; set; }
        public string SpecialWeaponKind { get; set; }
        public string SpecialWeaponName { get; set; }
        public string SpecialWeaponRule { get; set; }
        public string SpecialWeaponSolverKind { get; set; }
        public string SpecialWeaponLeadWindowKind { get; set; }
        public string SpecialWeaponLeadPolicy { get; set; }
        public string SpecialWeaponDiagnosticsReason { get; set; }
        public int SpecialShotCount { get; set; }
        public float SpecialSpreadDegrees { get; set; }
        public float SpecialParallelSpacingPixels { get; set; }
        public float SpecialLeadTicks { get; set; }
        public bool SpecialCursorTarget { get; set; }
        public bool SpecialAimApplied { get; set; }
        public bool SpecialWeaponUsesWeaponShoot { get; set; }
        public bool SpecialWeaponUsesAmmoShoot { get; set; }
        public string ReturningPhaseAssumption { get; set; }
        public bool ConservativeCenter { get; set; }
        public bool AimAdjusted { get; set; }
        public float PlayerCenterX { get; set; }
        public float PlayerCenterY { get; set; }
        public float TargetVelocityX { get; set; }
        public float TargetVelocityY { get; set; }
        public string TargetMotionProfileKind { get; set; }
        public float TargetMotionConfidence { get; set; }
        public float TargetVelocityConfidence { get; set; }
        public float TargetAccelerationX { get; set; }
        public float TargetAccelerationY { get; set; }
        public float TargetAccelerationConfidence { get; set; }
        public float TargetRecommendedLeadScale { get; set; }
        public float TargetRecommendedMaxLeadTicks { get; set; }
        public bool TargetPreferCurrentVelocity { get; set; }
        public bool TargetPreferSmoothedVelocity { get; set; }
        public string TargetHistoryResetReason { get; set; }
        public int TargetNpcAiStyle { get; set; }
        public bool TargetNoGravity { get; set; }
        public bool TargetCollideX { get; set; }
        public bool TargetCollideY { get; set; }
        public float PredictedTargetX { get; set; }
        public float PredictedTargetY { get; set; }
        public float ProjectileSpeed { get; set; }
        public float EffectiveProjectileSpeed { get; set; }
        public float RawLeadTicks { get; set; }
        public float LeadWindowMaxTicks { get; set; }
        public float LeadScale { get; set; }
        public bool LeadClamped { get; set; }
        public float LeadTicks { get; set; }
        public float GravityPerTick { get; set; }
        public float GravityDelayTicks { get; set; }
        public float GravityCompensationPixels { get; set; }
        public float AimWorldX { get; set; }
        public float AimWorldY { get; set; }
        public string SampleSpace { get; set; }
        public string SelectedSamplePoint { get; set; }
        public float SelectedSampleWorldX { get; set; }
        public float SelectedSampleWorldY { get; set; }
        public float PredictedHitboxCenterX { get; set; }
        public float PredictedHitboxCenterY { get; set; }
        public int VisibleSampleCount { get; set; }
        public float ProjectileHitRadius { get; set; }

        public CombatAimBallisticSolution()
        {
            Mode = string.Empty;
            FallbackReason = string.Empty;
            SolverKind = CombatAimBallisticSolverKinds.FallbackCenter;
            LeadWindowKind = CombatAimLeadWindowKinds.Fallback;
            LeadClampReason = CombatAimLeadClampReasons.None;
            PredictionConfidence = CombatAimPredictionConfidenceKinds.Unknown;
            ProjectileName = string.Empty;
            WeaponShootProjectileName = string.Empty;
            ResolvedProjectileRole = string.Empty;
            SecondaryProjectileName = string.Empty;
            AmmoItemName = string.Empty;
            AmmoProjectileName = string.Empty;
            PrimaryProjectileName = string.Empty;
            PrimaryProjectileRole = string.Empty;
            SecondaryProjectileRole = string.Empty;
            ProjectileProfileFamily = string.Empty;
            ProjectileProfileStatus = string.Empty;
            ProjectileProfileDegradedReason = string.Empty;
            ProjectileProfileSpeedSource = string.Empty;
            ProjectileProfileTransformRole = string.Empty;
            SpecialWeaponKind = string.Empty;
            SpecialWeaponName = string.Empty;
            SpecialWeaponRule = string.Empty;
            SpecialWeaponSolverKind = string.Empty;
            SpecialWeaponLeadWindowKind = string.Empty;
            SpecialWeaponLeadPolicy = string.Empty;
            SpecialWeaponDiagnosticsReason = string.Empty;
            ReturningPhaseAssumption = string.Empty;
            TargetMotionProfileKind = CombatAimTargetMotionProfile.Unknown;
            TargetHistoryResetReason = string.Empty;
            AmmoSlot = -1;
            SpecialShotCount = 1;
            EffectiveUpdatesPerTick = 1;
            LeadScale = 1f;
            SampleSpace = string.Empty;
            SelectedSamplePoint = string.Empty;
        }

        public CombatAimBallisticSolution Clone()
        {
            return (CombatAimBallisticSolution)MemberwiseClone();
        }
    }
}
