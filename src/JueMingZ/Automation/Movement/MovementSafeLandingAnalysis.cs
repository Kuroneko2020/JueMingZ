namespace JueMingZ.Automation.Movement
{
    public sealed class MovementSafeLandingAnalysis
    {
        public bool PlayerControllable { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public float GravityDirection { get; set; }
        public float FallingSpeed { get; set; }
        public int FallStartTileY { get; set; }
        public bool FallStartKnown { get; set; }
        public float EstimatedFallTiles { get; set; }
        public int ImpactDistancePixels { get; set; }
        public float ImpactTicks { get; set; }
        public bool ImpactFound { get; set; }
        public float ImpactWorldX { get; set; }
        public float ImpactWorldY { get; set; }
        public bool Dangerous { get; set; }
        public bool RescueWindow { get; set; }
        public bool AlreadySafe { get; set; }
        public string SafeReason { get; set; }
        public string SkipReason { get; set; }
        public bool RawCreativeGodMode { get; set; }
        public bool RawNoFallDmg { get; set; }
        public bool RawSlowFall { get; set; }
        public bool RawWet { get; set; }
        public bool RawHoneyWet { get; set; }
        public bool RawShimmering { get; set; }
        public bool RawWebbed { get; set; }
        public bool RawStoned { get; set; }
        public int RawGrapCount { get; set; }
        public int RawEquippedWingCount { get; set; }
        public bool RawMountNoFallDamage { get; set; }
        public int RawExtraFall { get; set; }
        public string ActiveCapabilitySummary { get; set; }
        public bool HasAirJump { get; set; }
        public int AirJumpFlagCount { get; set; }
        public bool HasRocketJump { get; set; }
        public bool HasRocketBootsAvailable { get; set; }
        public int RocketBoots { get; set; }
        public float RocketTime { get; set; }
        public int RocketDelay { get; set; }
        public bool CanRocket { get; set; }
        public bool CanRocketKnown { get; set; }
        public bool RocketRelease { get; set; }
        public bool CanUseBootFlyingAbilities { get; set; }
        public bool CanUseBootFlyingAbilitiesKnown { get; set; }
        public bool AerialJumpWindow { get; set; }
        public bool HasFlyingCarpet { get; set; }
        public bool HasFlyingCarpetAvailable { get; set; }
        public int FlyingCarpetTime { get; set; }
        public bool HasGravityGlobe { get; set; }
        public bool HasGravityFlipOpportunity { get; set; }
        public bool HasWingFlight { get; set; }
        public int WingsLogic { get; set; }
        public float WingTime { get; set; }
        public bool HasActiveFlyingMount { get; set; }
        public bool HasEquippedFlyingMount { get; set; }
        public bool HasEquippedSafeMount { get; set; }
        public bool HasGrapple { get; set; }
        public bool HasEquippedGrapple { get; set; }
        public bool HasInventoryGrapple { get; set; }
        public bool HasTeleportRod { get; set; }
        public int TeleportRodInventorySlot { get; set; }
        public int TeleportRodItemType { get; set; }
        public string TeleportRodItemName { get; set; }
        public bool HasCushionBlock { get; set; }
        public int CushionBlockInventorySlot { get; set; }
        public int CushionBlockHotbarSlot { get; set; }
        public int CushionBlockItemType { get; set; }
        public int CushionBlockStack { get; set; }
        public int CushionBlockCreateTile { get; set; }
        public int CushionBlockPlaceStyle { get; set; }
        public string CushionBlockItemName { get; set; }
        public int EquippedFlyingMountItemType { get; set; }
        public int EquippedFlyingMountType { get; set; }
        public int EquippedSafeMountItemType { get; set; }
        public int EquippedSafeMountType { get; set; }
        public int EquippedGrappleItemType { get; set; }
        public int InventoryGrappleItemType { get; set; }
        public float EquippedGrappleShootSpeed { get; set; }
        public float InventoryGrappleShootSpeed { get; set; }
        public float MaxFallSpeed { get; set; }
        public float GrappleHookSpeed { get; set; }

        // Landing surface contact fields (grapple-surface-aim)
        public bool LandingSurfaceKnown { get; set; }
        public float LandingContactWorldX { get; set; }
        public float LandingContactWorldY { get; set; }
        public int LandingContactTileX { get; set; }
        public int LandingContactTileY { get; set; }
        public string LandingSurfaceKind { get; set; }
        public int LandingSlopeType { get; set; }
        public string LandingSlopeDirection { get; set; }
        public string LandingContactSample { get; set; }
        public bool LandingMovingIntoSlope { get; set; }
        public bool LandingMovingWithSlope { get; set; }
        public float LandingProjectedPlayerLeftX { get; set; }
        public float LandingProjectedPlayerRightX { get; set; }
        public float LandingProjectedPlayerBottomY { get; set; }
        public string LandingSurfaceSummary { get; set; }

        // Grapple diagnostic fields (grapple-surface-aim)
        public string GrappleTargetSource { get; set; }
        public float GrappleTargetDistancePixels { get; set; }
        public float GrappleHookVerticalSpeed { get; set; }
        public float GrappleRelativeDownSpeed { get; set; }
        public float GrappleRequiredLeadTicks { get; set; }
        public int GrappleRequiredLeadPixels { get; set; }
        public float GrappleEstimatedTicksToTarget { get; set; }
        public bool GrappleTooEarly { get; set; }
        public bool GrappleTooLate { get; set; }
        public bool GrappleTooSlowForDownwardSurface { get; set; }
        public bool GrappleTargetFromLandingSurface { get; set; }
        public string GrappleTimingSummary { get; set; }
        public int EquippedGrappleProjectileType { get; set; }
        public int InventoryGrappleProjectileType { get; set; }
        public bool HasGrappleTarget { get; set; }
        public float GrappleTargetWorldX { get; set; }
        public float GrappleTargetWorldY { get; set; }
        public bool HasBlockPlacementTarget { get; set; }
        public float BlockPlacementWorldX { get; set; }
        public float BlockPlacementWorldY { get; set; }
        public int BlockPlacementTileX { get; set; }
        public int BlockPlacementTileY { get; set; }
        public bool HasTeleportTarget { get; set; }
        public float TeleportTargetWorldX { get; set; }
        public float TeleportTargetWorldY { get; set; }
        public int TeleportTargetTileX { get; set; }
        public int TeleportTargetTileY { get; set; }
        public bool ControlDown { get; set; }
        public bool TextInputFocused { get; set; }
        public string TextInputReason { get; set; }
        public string SelectedStrategyId { get; set; }
        public int SelectedPriority { get; set; }
        public string SelectedActionType { get; set; }
        public string StrategyCatalogVersion { get; set; }
        public string StrategyEvaluationSummary { get; set; }
        public string CandidateSummary { get; set; }
        public string SelectedPlanSummary { get; set; }
        public string RejectedStrategiesSummary { get; set; }
        public string PostApplyVerificationSummary { get; set; }
        public string RecoveryStateSummary { get; set; }

        public MovementSafeLandingAnalysis()
        {
            GravityDirection = 1f;
            ImpactDistancePixels = -1;
            ImpactTicks = -1f;
            ImpactWorldX = 0f;
            ImpactWorldY = 0f;
            SafeReason = string.Empty;
            SkipReason = string.Empty;
            ActiveCapabilitySummary = string.Empty;
            TextInputReason = string.Empty;
            SelectedStrategyId = string.Empty;
            SelectedPriority = -1;
            SelectedActionType = string.Empty;
            StrategyCatalogVersion = string.Empty;
            StrategyEvaluationSummary = string.Empty;
            CandidateSummary = string.Empty;
            SelectedPlanSummary = string.Empty;
            RejectedStrategiesSummary = string.Empty;
            PostApplyVerificationSummary = string.Empty;
            RecoveryStateSummary = string.Empty;
            EquippedFlyingMountItemType = 0;
            EquippedFlyingMountType = -1;
            EquippedSafeMountItemType = 0;
            EquippedSafeMountType = -1;
            EquippedGrappleItemType = 0;
            InventoryGrappleItemType = 0;
            LandingContactTileX = -1;
            LandingContactTileY = -1;
            LandingSurfaceKind = string.Empty;
            LandingSlopeDirection = string.Empty;
            LandingContactSample = string.Empty;
            LandingSurfaceSummary = string.Empty;
            GrappleTargetSource = string.Empty;
            GrappleTargetDistancePixels = -1f;
            GrappleRequiredLeadTicks = -1f;
            GrappleRequiredLeadPixels = -1;
            GrappleEstimatedTicksToTarget = -1f;
            GrappleTimingSummary = string.Empty;
            EquippedGrappleProjectileType = 0;
            InventoryGrappleProjectileType = 0;
            TeleportRodInventorySlot = -1;
            TeleportRodItemName = string.Empty;
            CushionBlockInventorySlot = -1;
            CushionBlockHotbarSlot = -1;
            CushionBlockItemName = string.Empty;
            CushionBlockCreateTile = -1;
            CushionBlockPlaceStyle = -1;
            BlockPlacementTileX = -1;
            BlockPlacementTileY = -1;
            TeleportTargetTileX = -1;
            TeleportTargetTileY = -1;
        }
    }
}
