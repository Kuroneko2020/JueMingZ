using System.Globalization;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Movement
{
    // Capability snapshots describe available rescue options; they do not execute or verify a rescue by themselves.
    internal sealed class MovementSafeLandingCapabilitySnapshot
    {
        public bool HasAirJump { get; set; }
        public int AirJumpFlagCount { get; set; }
        public bool HasRocketBootsAvailable { get; set; }
        public bool HasRocketJump { get; set; }
        public int RocketBoots { get; set; }
        public float RocketTime { get; set; }
        public int RocketDelay { get; set; }
        public bool CanRocket { get; set; }
        public bool CanRocketKnown { get; set; }
        public bool RocketRelease { get; set; }
        public bool CanUseBootFlyingAbilities { get; set; }
        public bool CanUseBootFlyingAbilitiesKnown { get; set; }
        public bool HasFlyingCarpet { get; set; }
        public bool HasFlyingCarpetAvailable { get; set; }
        public int FlyingCarpetTime { get; set; }
        public bool HasGravityGlobe { get; set; }
        public bool HasGravityFlipOpportunity { get; set; }
        public float GravityDirection { get; set; }
        public bool HasWingFlight { get; set; }
        public int WingsLogic { get; set; }
        public float WingTime { get; set; }
        public bool HasActiveFlyingMount { get; set; }
        public bool HasEquippedFlyingMount { get; set; }
        public bool HasEquippedSafeMount { get; set; }
        public bool HasEquippedGrapple { get; set; }
        public bool HasInventoryGrapple { get; set; }
        public bool HasTeleportRod { get; set; }
        public int TeleportRodInventorySlot { get; set; }
        public int TeleportRodItemType { get; set; }
        public bool HasCushionBlock { get; set; }
        public int CushionBlockInventorySlot { get; set; }
        public int CushionBlockHotbarSlot { get; set; }
        public int CushionBlockItemType { get; set; }
        public int CushionBlockCreateTile { get; set; }
        public string Summary { get; set; }

        public MovementSafeLandingCapabilitySnapshot()
        {
            Summary = string.Empty;
            GravityDirection = 1f;
            TeleportRodInventorySlot = -1;
            CushionBlockInventorySlot = -1;
            CushionBlockHotbarSlot = -1;
            CushionBlockCreateTile = -1;
        }

        public static MovementSafeLandingCapabilitySnapshot FromAnalysis(MovementSafeLandingAnalysis analysis)
        {
            var snapshot = new MovementSafeLandingCapabilitySnapshot();
            if (analysis == null)
            {
                return snapshot;
            }

            snapshot.HasAirJump = analysis.HasAirJump;
            snapshot.AirJumpFlagCount = analysis.AirJumpFlagCount;
            snapshot.HasRocketBootsAvailable = analysis.HasRocketBootsAvailable;
            snapshot.HasRocketJump = analysis.HasRocketJump;
            snapshot.RocketBoots = analysis.RocketBoots;
            snapshot.RocketTime = analysis.RocketTime;
            snapshot.RocketDelay = analysis.RocketDelay;
            snapshot.CanRocket = analysis.CanRocket;
            snapshot.CanRocketKnown = analysis.CanRocketKnown;
            snapshot.RocketRelease = analysis.RocketRelease;
            snapshot.CanUseBootFlyingAbilities = analysis.CanUseBootFlyingAbilities;
            snapshot.CanUseBootFlyingAbilitiesKnown = analysis.CanUseBootFlyingAbilitiesKnown;
            snapshot.HasFlyingCarpet = analysis.HasFlyingCarpet;
            snapshot.HasFlyingCarpetAvailable = analysis.HasFlyingCarpetAvailable;
            snapshot.FlyingCarpetTime = analysis.FlyingCarpetTime;
            snapshot.HasGravityGlobe = analysis.HasGravityGlobe;
            snapshot.HasGravityFlipOpportunity = analysis.HasGravityFlipOpportunity;
            snapshot.GravityDirection = analysis.GravityDirection;
            snapshot.HasWingFlight = analysis.HasWingFlight;
            snapshot.WingsLogic = analysis.WingsLogic;
            snapshot.WingTime = analysis.WingTime;
            snapshot.HasActiveFlyingMount = analysis.HasActiveFlyingMount;
            snapshot.HasEquippedFlyingMount = analysis.HasEquippedFlyingMount;
            snapshot.HasEquippedSafeMount = analysis.HasEquippedSafeMount;
            snapshot.HasEquippedGrapple = analysis.HasEquippedGrapple;
            snapshot.HasInventoryGrapple = analysis.HasInventoryGrapple;
            snapshot.HasTeleportRod = analysis.HasTeleportRod;
            snapshot.TeleportRodInventorySlot = analysis.TeleportRodInventorySlot;
            snapshot.TeleportRodItemType = analysis.TeleportRodItemType;
            snapshot.HasCushionBlock = analysis.HasCushionBlock;
            snapshot.CushionBlockInventorySlot = analysis.CushionBlockInventorySlot;
            snapshot.CushionBlockHotbarSlot = analysis.CushionBlockHotbarSlot;
            snapshot.CushionBlockItemType = analysis.CushionBlockItemType;
            snapshot.CushionBlockCreateTile = analysis.CushionBlockCreateTile;
            snapshot.Summary = string.IsNullOrWhiteSpace(analysis.ActiveCapabilitySummary)
                ? snapshot.BuildSummary()
                : analysis.ActiveCapabilitySummary;
            return snapshot;
        }

        public static MovementSafeLandingCapabilitySnapshot FromJumpProfile(JumpInputProfile profile)
        {
            var snapshot = new MovementSafeLandingCapabilitySnapshot();
            if (profile == null)
            {
                return snapshot;
            }

            snapshot.HasAirJump = profile.HasAirJump;
            snapshot.AirJumpFlagCount = profile.AirJumpFlagCount;
            var safeLandingRocketBootsAvailable = MovementSafeLandingCompat.HasSafeLandingRocketBootsActivationOpportunity(profile);
            snapshot.HasRocketBootsAvailable = profile.HasRocketBootsAvailable || safeLandingRocketBootsAvailable;
            snapshot.HasRocketJump = profile.HasRocketJump || safeLandingRocketBootsAvailable;
            snapshot.RocketBoots = profile.RocketBoots;
            snapshot.RocketTime = profile.RocketTime;
            snapshot.RocketDelay = profile.RocketDelay;
            snapshot.CanRocket = profile.CanRocket;
            snapshot.CanRocketKnown = profile.CanRocketKnown;
            snapshot.RocketRelease = profile.RocketRelease;
            snapshot.CanUseBootFlyingAbilities = profile.CanUseBootFlyingAbilities;
            snapshot.CanUseBootFlyingAbilitiesKnown = profile.CanUseBootFlyingAbilitiesKnown;
            snapshot.HasFlyingCarpet = profile.HasFlyingCarpet;
            snapshot.HasFlyingCarpetAvailable = profile.HasFlyingCarpetAvailable;
            snapshot.FlyingCarpetTime = profile.FlyingCarpetTime;
            snapshot.HasGravityGlobe = profile.HasGravityGlobe;
            snapshot.HasGravityFlipOpportunity = profile.HasGravityFlipOpportunity;
            snapshot.GravityDirection = profile.GravityDirection;
            snapshot.HasWingFlight = profile.HasWingFlight;
            snapshot.WingsLogic = profile.WingsLogic;
            snapshot.WingTime = profile.WingTime;
            snapshot.HasActiveFlyingMount = profile.HasMountOpportunity;
            snapshot.HasEquippedFlyingMount = profile.HasEquippedFlyingMountOpportunity;
            snapshot.HasEquippedSafeMount = profile.HasEquippedSafeMountOpportunity;
            snapshot.HasEquippedGrapple = profile.HasEquippedGrapple;
            snapshot.HasInventoryGrapple = profile.HasInventoryGrapple;
            snapshot.Summary = snapshot.BuildSummary();
            return snapshot;
        }

        public string BuildSummary()
        {
            return "airJump=" + Bool(HasAirJump) +
                   ",airJumpFlagCount=" + AirJumpFlagCount.ToString(CultureInfo.InvariantCulture) +
                   ",rocketBootsAvailable=" + Bool(HasRocketBootsAvailable) +
                   ",rocketBoots=" + RocketBoots.ToString(CultureInfo.InvariantCulture) +
                   ",rocketTime=" + RocketTime.ToString("0.###", CultureInfo.InvariantCulture) +
                   ",rocketDelay=" + RocketDelay.ToString(CultureInfo.InvariantCulture) +
                   ",canRocket=" + Bool(CanRocket) +
                   ",canRocketKnown=" + Bool(CanRocketKnown) +
                   ",rocketRel=" + Bool(RocketRelease) +
                   ",canUseBootFlyingAbilities=" + Bool(CanUseBootFlyingAbilities) +
                   ",canUseBootFlyingAbilitiesKnown=" + Bool(CanUseBootFlyingAbilitiesKnown) +
                   ",flyingCarpet=" + Bool(HasFlyingCarpetAvailable) +
                   ",gravityGlobe=" + Bool(HasGravityFlipOpportunity) +
                   ",gravityDirection=" + GravityDirection.ToString("0.###", CultureInfo.InvariantCulture) +
                   ",wing=" + Bool(HasWingFlight) +
                   ",activeMount=" + Bool(HasActiveFlyingMount) +
                   ",equippedFlyingMount=" + Bool(HasEquippedFlyingMount) +
                   ",equippedSafeMount=" + Bool(HasEquippedSafeMount) +
                   ",equippedGrapple=" + Bool(HasEquippedGrapple) +
                   ",inventoryGrapple=" + Bool(HasInventoryGrapple) +
                   ",teleportRod=" + Bool(HasTeleportRod) +
                   ",teleportRodSlot=" + TeleportRodInventorySlot.ToString(CultureInfo.InvariantCulture) +
                   ",teleportRodItemType=" + TeleportRodItemType.ToString(CultureInfo.InvariantCulture) +
                   ",cushionBlock=" + Bool(HasCushionBlock) +
                   ",cushionBlockInventorySlot=" + CushionBlockInventorySlot.ToString(CultureInfo.InvariantCulture) +
                   ",cushionBlockSlot=" + CushionBlockHotbarSlot.ToString(CultureInfo.InvariantCulture) +
                   ",cushionBlockItemType=" + CushionBlockItemType.ToString(CultureInfo.InvariantCulture) +
                   ",cushionBlockTile=" + CushionBlockCreateTile.ToString(CultureInfo.InvariantCulture);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
