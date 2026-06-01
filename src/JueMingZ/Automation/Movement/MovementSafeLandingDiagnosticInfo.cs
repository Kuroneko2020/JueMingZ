using System;

namespace JueMingZ.Automation.Movement
{
    public sealed class MovementSafeLandingDiagnosticInfo
    {
        public bool Enabled { get; set; }
        public bool LastTriggered { get; set; }
        public DateTime? LastTriggerUtc { get; set; }
        public string LastDecision { get; set; }
        public string LastSkipReason { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public long LastTick { get; set; }
        public int PendingActionCount { get; set; }
        public string RunningActionKind { get; set; }
        public bool TextInputFocused { get; set; }
        public string TextInputReason { get; set; }
        public bool PlayerControllable { get; set; }
        public bool Dangerous { get; set; }
        public bool RescueWindow { get; set; }
        public bool AlreadySafe { get; set; }
        public string SafeReason { get; set; }
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
        public float FallingSpeed { get; set; }
        public float VelocityY { get; set; }
        public float GravityDirection { get; set; }
        public bool ImpactFound { get; set; }
        public int ImpactDistancePixels { get; set; }
        public float ImpactTicks { get; set; }
        public float EstimatedFallTiles { get; set; }
        public string ActiveCapabilitySummary { get; set; }
        public string SelectedStrategyId { get; set; }
        public int SelectedPriority { get; set; }
        public string SelectedActionType { get; set; }
        public bool HasFlyingCarpet { get; set; }
        public bool HasFlyingCarpetAvailable { get; set; }
        public int FlyingCarpetTime { get; set; }
        public bool HasGravityGlobe { get; set; }
        public bool HasGravityFlipOpportunity { get; set; }
        public bool HasEquippedFlyingMount { get; set; }
        public bool HasEquippedSafeMount { get; set; }
        public bool HasEquippedGrapple { get; set; }
        public bool HasInventoryGrapple { get; set; }
        public bool HasTeleportRod { get; set; }
        public int TeleportRodInventorySlot { get; set; }
        public int TeleportRodItemType { get; set; }
        public bool TeleportTargetKnown { get; set; }
        public int TeleportTargetTileX { get; set; }
        public int TeleportTargetTileY { get; set; }
        public float TeleportTargetWorldX { get; set; }
        public float TeleportTargetWorldY { get; set; }
        public bool HasCushionBlock { get; set; }
        public int CushionBlockInventorySlot { get; set; }
        public int CushionBlockHotbarSlot { get; set; }
        public int CushionBlockItemType { get; set; }
        public int CushionBlockCreateTile { get; set; }
        public bool BlockPlacementTargetKnown { get; set; }
        public int BlockPlacementTileX { get; set; }
        public int BlockPlacementTileY { get; set; }
        public float BlockPlacementWorldX { get; set; }
        public float BlockPlacementWorldY { get; set; }
        public bool GravityRestorePending { get; set; }
        public float GravityRestoreOriginalDirection { get; set; }
        public long GravityRestorePendingTicks { get; set; }
        public string GravityRestoreLastDecision { get; set; }
        public string GravityRestoreLastSkipReason { get; set; }
        public string ConfigSummary { get; set; }
        public string StageSummary { get; set; }
        public string StrategyCatalogVersion { get; set; }
        public string StrategyEvaluationSummary { get; set; }
        public string CandidateSummary { get; set; }
        public string SelectedPlanSummary { get; set; }
        public string RejectedStrategiesSummary { get; set; }
        public string PostApplyVerificationSummary { get; set; }
        public string RecoveryStateSummary { get; set; }
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
        public string LandingSurfaceSummary { get; set; }
        public float GrappleHookSpeed { get; set; }
        public string GrappleTargetSource { get; set; }
        public bool GrappleTargetFromLandingSurface { get; set; }
        public float GrappleTargetDistancePixels { get; set; }
        public float GrappleHookVerticalSpeed { get; set; }
        public float GrappleRelativeDownSpeed { get; set; }
        public float GrappleRequiredLeadTicks { get; set; }
        public int GrappleRequiredLeadPixels { get; set; }
        public float GrappleEstimatedTicksToTarget { get; set; }
        public bool GrappleTooEarly { get; set; }
        public bool GrappleTooLate { get; set; }
        public bool GrappleTooSlowForDownwardSurface { get; set; }
        public string GrappleTimingSummary { get; set; }
        public float EquippedGrappleShootSpeed { get; set; }
        public float InventoryGrappleShootSpeed { get; set; }
        public int EquippedGrappleProjectileType { get; set; }
        public int InventoryGrappleProjectileType { get; set; }
        public float MaxFallSpeed { get; set; }

        public long SubmittedCount { get; set; }
        public long SkippedCount { get; set; }
        public long FullAnalysisCount { get; set; }
        public long CheapPrecheckSkipCount { get; set; }
        public long LandingProbeCount { get; set; }
        public string LastCompatError { get; set; }
        public string CollisionFastPathStatus { get; set; }
        public bool PlayerUpdateHookInstalled { get; set; }
        public string PlayerUpdateHookMessage { get; set; }
        public bool QueuedJumpPulseActive { get; set; }
        public string QueuedJumpPulseStatus { get; set; }
        public string QueuedJumpPulseApplySite { get; set; }
        public bool TemporaryEquipmentApplied { get; set; }
        public int TemporaryEquipmentPendingRestoreCount { get; set; }
        public int TemporaryEquipmentPendingRestoreNoSpaceCount { get; set; }
        public string TemporaryEquipmentLastDecision { get; set; }
        public string TemporaryEquipmentLastSkipReason { get; set; }
        public string TemporaryEquipmentSelectedCategory { get; set; }
        public string TemporaryEquipmentSelectedSourceKind { get; set; }
        public int TemporaryEquipmentSelectedSourceSlot { get; set; }
        public string TemporaryEquipmentSelectedTargetKind { get; set; }
        public int TemporaryEquipmentSelectedTargetSlot { get; set; }
        public int TemporaryEquipmentSelectedItemType { get; set; }
        public int TemporaryEquipmentSelectedMountType { get; set; }

        public MovementSafeLandingDiagnosticInfo()
        {
            LastDecision = string.Empty;
            LastSkipReason = string.Empty;
            RunningActionKind = string.Empty;
            TextInputReason = string.Empty;
            SafeReason = string.Empty;
            ActiveCapabilitySummary = string.Empty;
            SelectedStrategyId = string.Empty;
            SelectedActionType = string.Empty;
            ConfigSummary = string.Empty;
            StageSummary = string.Empty;
            StrategyCatalogVersion = string.Empty;
            StrategyEvaluationSummary = string.Empty;
            CandidateSummary = string.Empty;
            SelectedPlanSummary = string.Empty;
            RejectedStrategiesSummary = string.Empty;
            PostApplyVerificationSummary = string.Empty;
            RecoveryStateSummary = string.Empty;
            LandingSurfaceKind = string.Empty;
            LandingSlopeDirection = string.Empty;
            LandingContactSample = string.Empty;
            LandingSurfaceSummary = string.Empty;
            GrappleTargetSource = string.Empty;
            GrappleTimingSummary = string.Empty;
            EquippedGrappleProjectileType = 0;
            InventoryGrappleProjectileType = 0;
            LastCompatError = string.Empty;
            CollisionFastPathStatus = string.Empty;
            PlayerUpdateHookMessage = string.Empty;
            QueuedJumpPulseStatus = string.Empty;
            QueuedJumpPulseApplySite = string.Empty;
            TemporaryEquipmentLastDecision = string.Empty;
            TemporaryEquipmentLastSkipReason = string.Empty;
            TemporaryEquipmentSelectedCategory = string.Empty;
            TemporaryEquipmentSelectedSourceKind = string.Empty;
            TemporaryEquipmentSelectedTargetKind = string.Empty;
            GravityRestoreLastDecision = string.Empty;
            GravityRestoreLastSkipReason = string.Empty;
            ImpactDistancePixels = -1;
            ImpactTicks = -1f;
            SelectedPriority = -1;
            TeleportRodInventorySlot = -1;
            TeleportTargetTileX = -1;
            TeleportTargetTileY = -1;
            CushionBlockInventorySlot = -1;
            CushionBlockHotbarSlot = -1;
            CushionBlockCreateTile = -1;
            BlockPlacementTileX = -1;
            BlockPlacementTileY = -1;
            TemporaryEquipmentSelectedSourceSlot = -1;
            TemporaryEquipmentSelectedTargetSlot = -1;
            TemporaryEquipmentSelectedMountType = -1;
        }

        public MovementSafeLandingDiagnosticInfo Clone()
        {
            return new MovementSafeLandingDiagnosticInfo
            {
                Enabled = Enabled,
                LastTriggered = LastTriggered,
                LastTriggerUtc = LastTriggerUtc,
                LastDecision = LastDecision,
                LastSkipReason = LastSkipReason,
                LastDecisionUtc = LastDecisionUtc,
                LastTick = LastTick,
                PendingActionCount = PendingActionCount,
                RunningActionKind = RunningActionKind,
                TextInputFocused = TextInputFocused,
                TextInputReason = TextInputReason,
                PlayerControllable = PlayerControllable,
                Dangerous = Dangerous,
                RescueWindow = RescueWindow,
                AlreadySafe = AlreadySafe,
                SafeReason = SafeReason,
                RawCreativeGodMode = RawCreativeGodMode,
                RawNoFallDmg = RawNoFallDmg,
                RawSlowFall = RawSlowFall,
                RawWet = RawWet,
                RawHoneyWet = RawHoneyWet,
                RawShimmering = RawShimmering,
                RawWebbed = RawWebbed,
                RawStoned = RawStoned,
                RawGrapCount = RawGrapCount,
                RawEquippedWingCount = RawEquippedWingCount,
                RawMountNoFallDamage = RawMountNoFallDamage,
                RawExtraFall = RawExtraFall,
                FallingSpeed = FallingSpeed,
                VelocityY = VelocityY,
                GravityDirection = GravityDirection,
                ImpactFound = ImpactFound,
                ImpactDistancePixels = ImpactDistancePixels,
                ImpactTicks = ImpactTicks,
                EstimatedFallTiles = EstimatedFallTiles,
                ActiveCapabilitySummary = ActiveCapabilitySummary,
                SelectedStrategyId = SelectedStrategyId,
                SelectedPriority = SelectedPriority,
                SelectedActionType = SelectedActionType,
                HasFlyingCarpet = HasFlyingCarpet,
                HasFlyingCarpetAvailable = HasFlyingCarpetAvailable,
                FlyingCarpetTime = FlyingCarpetTime,
                HasGravityGlobe = HasGravityGlobe,
                HasGravityFlipOpportunity = HasGravityFlipOpportunity,
                HasEquippedFlyingMount = HasEquippedFlyingMount,
                HasEquippedSafeMount = HasEquippedSafeMount,
                HasEquippedGrapple = HasEquippedGrapple,
                HasInventoryGrapple = HasInventoryGrapple,
                HasTeleportRod = HasTeleportRod,
                TeleportRodInventorySlot = TeleportRodInventorySlot,
                TeleportRodItemType = TeleportRodItemType,
                TeleportTargetKnown = TeleportTargetKnown,
                TeleportTargetTileX = TeleportTargetTileX,
                TeleportTargetTileY = TeleportTargetTileY,
                TeleportTargetWorldX = TeleportTargetWorldX,
                TeleportTargetWorldY = TeleportTargetWorldY,
                HasCushionBlock = HasCushionBlock,
                CushionBlockInventorySlot = CushionBlockInventorySlot,
                CushionBlockHotbarSlot = CushionBlockHotbarSlot,
                CushionBlockItemType = CushionBlockItemType,
                CushionBlockCreateTile = CushionBlockCreateTile,
                BlockPlacementTargetKnown = BlockPlacementTargetKnown,
                BlockPlacementTileX = BlockPlacementTileX,
                BlockPlacementTileY = BlockPlacementTileY,
                BlockPlacementWorldX = BlockPlacementWorldX,
                BlockPlacementWorldY = BlockPlacementWorldY,
                GravityRestorePending = GravityRestorePending,
                GravityRestoreOriginalDirection = GravityRestoreOriginalDirection,
                GravityRestorePendingTicks = GravityRestorePendingTicks,
                GravityRestoreLastDecision = GravityRestoreLastDecision,
                GravityRestoreLastSkipReason = GravityRestoreLastSkipReason,
                ConfigSummary = ConfigSummary,
                StageSummary = StageSummary,
                StrategyCatalogVersion = StrategyCatalogVersion,
                StrategyEvaluationSummary = StrategyEvaluationSummary,
                CandidateSummary = CandidateSummary,
                SelectedPlanSummary = SelectedPlanSummary,
                RejectedStrategiesSummary = RejectedStrategiesSummary,
                PostApplyVerificationSummary = PostApplyVerificationSummary,
                RecoveryStateSummary = RecoveryStateSummary,
                SubmittedCount = SubmittedCount,
                SkippedCount = SkippedCount,
                FullAnalysisCount = FullAnalysisCount,
                CheapPrecheckSkipCount = CheapPrecheckSkipCount,
                LandingProbeCount = LandingProbeCount,
                LastCompatError = LastCompatError,
                CollisionFastPathStatus = CollisionFastPathStatus,
                PlayerUpdateHookInstalled = PlayerUpdateHookInstalled,
                PlayerUpdateHookMessage = PlayerUpdateHookMessage,
                QueuedJumpPulseActive = QueuedJumpPulseActive,
                QueuedJumpPulseStatus = QueuedJumpPulseStatus,
                QueuedJumpPulseApplySite = QueuedJumpPulseApplySite,
                TemporaryEquipmentApplied = TemporaryEquipmentApplied,
                TemporaryEquipmentPendingRestoreCount = TemporaryEquipmentPendingRestoreCount,
                TemporaryEquipmentPendingRestoreNoSpaceCount = TemporaryEquipmentPendingRestoreNoSpaceCount,
                TemporaryEquipmentLastDecision = TemporaryEquipmentLastDecision,
                TemporaryEquipmentLastSkipReason = TemporaryEquipmentLastSkipReason,
                TemporaryEquipmentSelectedCategory = TemporaryEquipmentSelectedCategory,
                TemporaryEquipmentSelectedSourceKind = TemporaryEquipmentSelectedSourceKind,
                TemporaryEquipmentSelectedSourceSlot = TemporaryEquipmentSelectedSourceSlot,
                TemporaryEquipmentSelectedTargetKind = TemporaryEquipmentSelectedTargetKind,
                TemporaryEquipmentSelectedTargetSlot = TemporaryEquipmentSelectedTargetSlot,
                TemporaryEquipmentSelectedItemType = TemporaryEquipmentSelectedItemType,
                TemporaryEquipmentSelectedMountType = TemporaryEquipmentSelectedMountType,
                LandingSurfaceKnown = LandingSurfaceKnown,
                LandingContactWorldX = LandingContactWorldX,
                LandingContactWorldY = LandingContactWorldY,
                LandingContactTileX = LandingContactTileX,
                LandingContactTileY = LandingContactTileY,
                LandingSurfaceKind = LandingSurfaceKind,
                LandingSlopeType = LandingSlopeType,
                LandingSlopeDirection = LandingSlopeDirection,
                LandingContactSample = LandingContactSample,
                LandingMovingIntoSlope = LandingMovingIntoSlope,
                LandingMovingWithSlope = LandingMovingWithSlope,
                LandingSurfaceSummary = LandingSurfaceSummary,
                GrappleHookSpeed = GrappleHookSpeed,
                GrappleTargetSource = GrappleTargetSource,
                GrappleTargetFromLandingSurface = GrappleTargetFromLandingSurface,
                GrappleTargetDistancePixels = GrappleTargetDistancePixels,
                GrappleHookVerticalSpeed = GrappleHookVerticalSpeed,
                GrappleRelativeDownSpeed = GrappleRelativeDownSpeed,
                GrappleRequiredLeadTicks = GrappleRequiredLeadTicks,
                GrappleRequiredLeadPixels = GrappleRequiredLeadPixels,
                GrappleEstimatedTicksToTarget = GrappleEstimatedTicksToTarget,
                GrappleTooSlowForDownwardSurface = GrappleTooSlowForDownwardSurface,
                GrappleTimingSummary = GrappleTimingSummary,
                GrappleTooEarly = GrappleTooEarly,
                GrappleTooLate = GrappleTooLate,
                EquippedGrappleShootSpeed = EquippedGrappleShootSpeed,
                InventoryGrappleShootSpeed = InventoryGrappleShootSpeed,
                EquippedGrappleProjectileType = EquippedGrappleProjectileType,
                InventoryGrappleProjectileType = InventoryGrappleProjectileType,
                MaxFallSpeed = MaxFallSpeed
            };
        }
    }
}
