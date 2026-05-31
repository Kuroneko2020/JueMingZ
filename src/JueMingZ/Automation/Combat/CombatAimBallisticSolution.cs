namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimBallisticSolution
    {
        public bool Solved { get; set; }
        public string Mode { get; set; }
        public string FallbackReason { get; set; }
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
        public int SpecialShotCount { get; set; }
        public float SpecialSpreadDegrees { get; set; }
        public float SpecialParallelSpacingPixels { get; set; }
        public float SpecialLeadTicks { get; set; }
        public bool SpecialCursorTarget { get; set; }
        public bool SpecialAimApplied { get; set; }
        public bool SpecialWeaponUsesWeaponShoot { get; set; }
        public bool SpecialWeaponUsesAmmoShoot { get; set; }
        public bool ConservativeCenter { get; set; }
        public bool AimAdjusted { get; set; }
        public float PlayerCenterX { get; set; }
        public float PlayerCenterY { get; set; }
        public float TargetVelocityX { get; set; }
        public float TargetVelocityY { get; set; }
        public float PredictedTargetX { get; set; }
        public float PredictedTargetY { get; set; }
        public float ProjectileSpeed { get; set; }
        public float EffectiveProjectileSpeed { get; set; }
        public float LeadTicks { get; set; }
        public float GravityPerTick { get; set; }
        public float GravityCompensationPixels { get; set; }
        public float AimWorldX { get; set; }
        public float AimWorldY { get; set; }

        public CombatAimBallisticSolution()
        {
            Mode = string.Empty;
            FallbackReason = string.Empty;
            ProjectileName = string.Empty;
            WeaponShootProjectileName = string.Empty;
            ResolvedProjectileRole = string.Empty;
            SecondaryProjectileName = string.Empty;
            AmmoItemName = string.Empty;
            AmmoProjectileName = string.Empty;
            PrimaryProjectileName = string.Empty;
            PrimaryProjectileRole = string.Empty;
            SecondaryProjectileRole = string.Empty;
            SpecialWeaponKind = string.Empty;
            SpecialWeaponName = string.Empty;
            SpecialWeaponRule = string.Empty;
            AmmoSlot = -1;
            SpecialShotCount = 1;
        }
    }
}
