using System;

namespace JueMingZ.Compat
{
    public sealed class JumpInputProfile
    {
        public bool PlayerActive { get; set; }
        public bool PlayerDead { get; set; }
        public bool PlayerGhost { get; set; }
        public bool PlayerCrowdControlled { get; set; }
        public bool ControlJump { get; set; }
        public bool ReleaseJump { get; set; }
        public bool ControlDown { get; set; }
        public bool Sliding { get; set; }
        public int JumpTicksRemaining { get; set; }
        public float VelocityY { get; set; }
        public float GravityDirection { get; set; }
        public bool GroundedOrSliding { get; set; }
        public bool AerialJumpWindow { get; set; }
        public bool HasAirJump { get; set; }
        public int AirJumpFlagCount { get; set; }
        public bool CanUseBootFlyingAbilities { get; set; }
        public bool CanUseBootFlyingAbilitiesKnown { get; set; }
        public int RocketBoots { get; set; }
        public float RocketTime { get; set; }
        public int RocketDelay { get; set; }
        public bool CanRocket { get; set; }
        public bool CanRocketKnown { get; set; }
        public bool RocketRelease { get; set; }
        public bool HasRocketJump { get; set; }
        public bool HasRocketBootsAvailable { get; set; }
        public bool HasFlyingCarpet { get; set; }
        public bool HasFlyingCarpetAvailable { get; set; }
        public int FlyingCarpetTime { get; set; }
        public bool FlyingCarpetCanStart { get; set; }
        public bool HasGravityGlobe { get; set; }
        public bool HasGravityFlipOpportunity { get; set; }
        public int WingsLogic { get; set; }
        public float WingTime { get; set; }
        public bool HasWingFlight { get; set; }
        public bool MountActive { get; set; }
        public int MountType { get; set; }
        public bool MountCanFly { get; set; }
        public bool MountCanFlyKnown { get; set; }
        public bool MountNoFallDamage { get; set; }
        public bool MountNoFallDamageKnown { get; set; }
        public bool HasMountOpportunity { get; set; }
        public int EquippedMountItemType { get; set; }
        public int EquippedMountType { get; set; }
        public bool EquippedMountCanFly { get; set; }
        public bool EquippedMountCanFlyKnown { get; set; }
        public bool EquippedMountNoFallDamage { get; set; }
        public bool EquippedMountNoFallDamageKnown { get; set; }
        public bool HasEquippedFlyingMountOpportunity { get; set; }
        public bool HasEquippedSafeMountOpportunity { get; set; }
        public bool HasEquippedGrapple { get; set; }
        public bool HasInventoryGrapple { get; set; }
        public bool HasAnyGrapple { get; set; }
        public int EquippedGrappleItemType { get; set; }
        public int InventoryGrappleItemType { get; set; }

        public float EquippedGrappleShootSpeed { get; set; }
        public float InventoryGrappleShootSpeed { get; set; }
        public int EquippedGrappleProjectileType { get; set; }
        public int InventoryGrappleProjectileType { get; set; }

        public bool PlayerControllable
        {
            get { return PlayerActive && !PlayerDead && !PlayerGhost && !PlayerCrowdControlled; }
        }

        public bool HasAvailableJumpOpportunity
        {
            get
            {
                return PlayerControllable &&
                       (GroundedOrSliding ||
                        (AerialJumpWindow && (HasAirJump || HasRocketJump || HasFlyingCarpetAvailable || HasWingFlight || HasMountOpportunity)));
            }
        }

        public string CapabilitySummary
        {
            get
            {
                return "grounded=" + BoolText(GroundedOrSliding) +
                       ",airJump=" + BoolText(HasAirJump) +
                       ",rocket=" + BoolText(HasRocketJump) +
                       ",rocketBootsReady=" + BoolText(HasRocketBootsAvailable) +
                       ",flyingCarpet=" + BoolText(HasFlyingCarpetAvailable) +
                       ",gravityGlobe=" + BoolText(HasGravityFlipOpportunity) +
                       ",wing=" + BoolText(HasWingFlight) +
                       ",activeMount=" + BoolText(HasMountOpportunity) +
                       ",equippedFlyingMount=" + BoolText(HasEquippedFlyingMountOpportunity) +
                       ",equippedSafeMount=" + BoolText(HasEquippedSafeMountOpportunity) +
                       ",grapple=" + BoolText(HasAnyGrapple);
            }
        }

        public JumpInputProfile Clone()
        {
            return (JumpInputProfile)MemberwiseClone();
        }

        private static string BoolText(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
