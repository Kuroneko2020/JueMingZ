namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimBallisticContext
    {
        public bool Prepared { get; set; }
        public string FallbackReason { get; set; }
        public CombatAimWeaponProfile Weapon { get; set; }
        public CombatAimProjectileProfile ProjectileProfile { get; set; }
        public bool HasPlayerCenter { get; set; }
        public float PlayerCenterX { get; set; }
        public float PlayerCenterY { get; set; }
        public int ProjectileType { get; set; }
        public string ProjectileName { get; set; }
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
        public float ProjectileSpeed { get; set; }
        public float EffectiveProjectileSpeed { get; set; }
        public int EffectiveUpdatesPerTick { get; set; }
        public float GravityPerTickCandidate { get; set; }
        public float ProjectileRadiusForHit { get; set; }
        public string ProfileFamilyHint { get; set; }
        public string ProfileCompleteness { get; set; }
        public string ProfileFallbackReason { get; set; }
        public string ProfileSpeedSource { get; set; }
        public bool ProfileGunProj { get; set; }
        public bool ProfileAmmoSpeedApplied { get; set; }
        public bool ProfileMagicQuiverApplied { get; set; }
        public bool ProfileArcheryApplied { get; set; }
        public bool ProfileArcherySpeedCapped { get; set; }
        public bool ProfileMagicQuiverEffectiveUpdateApplied { get; set; }
        public bool ProfileSpecificLauncherAmmoProjectileMatch { get; set; }
        public string ProfileProjectileTransformRole { get; set; }
        public bool AmmoAvailable { get; set; }
        public int AmmoType { get; set; }
        public int AmmoItemType { get; set; }
        public string AmmoItemName { get; set; }
        public int AmmoProjectileType { get; set; }
        public int AmmoSlot { get; set; }
        public float AmmoShootSpeed { get; set; }
        public bool AmmoArrowLike { get; set; }
        public bool AmmoBulletLike { get; set; }

        public CombatAimBallisticContext()
        {
            FallbackReason = string.Empty;
            ProjectileName = string.Empty;
            AmmoItemName = string.Empty;
            AmmoSlot = -1;
            EffectiveUpdatesPerTick = 1;
            ProfileFamilyHint = string.Empty;
            ProfileCompleteness = string.Empty;
            ProfileFallbackReason = string.Empty;
            ProfileSpeedSource = string.Empty;
            ProfileProjectileTransformRole = string.Empty;
        }
    }
}
