using System.Collections.Generic;

namespace JueMingZ.Automation.Movement
{
    internal enum MovementSafeLandingEquipmentContainerKind
    {
        Unknown = 0,
        Inventory = 1,
        SocialAccessory = 2,
        Accessory = 3,
        MiscEquip = 4,
        SocialArmor = 5,
        Hotbar = 6
    }

    internal sealed class MovementSafeLandingEquipmentItemSignature
    {
        public int Type { get; set; }
        public int Stack { get; set; }
        public int Prefix { get; set; }
        public string Name { get; set; }

        public bool IsAir
        {
            get { return Type <= 0 || Stack <= 0; }
        }

        public MovementSafeLandingEquipmentItemSignature()
        {
            Name = string.Empty;
        }

        public bool Matches(MovementSafeLandingEquipmentItemSignature other)
        {
            if (other == null)
            {
                return false;
            }

            if (IsAir && other.IsAir)
            {
                return true;
            }

            return Type == other.Type &&
                   Stack == other.Stack &&
                   Prefix == other.Prefix;
        }
    }

    internal sealed class MovementSafeLandingEquipmentPlan
    {
        public string StrategyId { get; set; }
        public string EquipmentCategory { get; set; }
        public string ActionType { get; set; }
        public int SelectedPriority { get; set; }
        public MovementSafeLandingEquipmentContainerKind SourceContainerKind { get; set; }
        public int SourceSlot { get; set; }
        public MovementSafeLandingEquipmentContainerKind TargetContainerKind { get; set; }
        public int TargetSlot { get; set; }
        public int CandidateItemType { get; set; }
        public int CandidateMountType { get; set; }
        public MovementSafeLandingEquipmentItemSignature CandidateSignature { get; set; }
        public MovementSafeLandingEquipmentItemSignature TargetSignatureAtPlan { get; set; }
        public bool ApplyTriggersInput { get; set; }
        public bool ApplyRocketRelease { get; set; }
        public bool SuppressDown { get; set; }
        public int HoldTicks { get; set; }
        public float ImpactTicks { get; set; }
        public int ImpactDistancePixels { get; set; }
        public float FallingSpeed { get; set; }
        public string CapabilitySummary { get; set; }

        public MovementSafeLandingEquipmentPlan()
        {
            StrategyId = string.Empty;
            EquipmentCategory = string.Empty;
            ActionType = string.Empty;
            SelectedPriority = -1;
            SourceSlot = -1;
            TargetSlot = -1;
            CandidateMountType = -1;
            CandidateSignature = new MovementSafeLandingEquipmentItemSignature();
            TargetSignatureAtPlan = new MovementSafeLandingEquipmentItemSignature();
            CapabilitySummary = string.Empty;
        }
    }

    internal sealed class MovementSafeLandingEquipmentMoveRecord
    {
        public string StrategyId { get; set; }
        public string EquipmentCategory { get; set; }
        public string ActionType { get; set; }
        public int SelectedPriority { get; set; }
        public MovementSafeLandingEquipmentContainerKind SourceContainerKind { get; set; }
        public int SourceSlot { get; set; }
        public MovementSafeLandingEquipmentContainerKind TargetContainerKind { get; set; }
        public int TargetSlot { get; set; }
        public int CandidateItemType { get; set; }
        public int CandidateMountType { get; set; }
        public MovementSafeLandingEquipmentItemSignature RescueItemSignature { get; set; }
        public bool OriginalTargetWasAir { get; set; }
        public MovementSafeLandingEquipmentItemSignature OriginalTargetItemSignature { get; set; }
        public MovementSafeLandingEquipmentContainerKind OriginalTargetHoldingContainerKind { get; set; }
        public int OriginalTargetHoldingSlot { get; set; }
        public float ImpactTicks { get; set; }
        public int ImpactDistancePixels { get; set; }
        public float FallingSpeed { get; set; }
        public bool PostApplyCapabilityObserved { get; set; }
        public string PostApplyVerificationReason { get; set; }
        public string ApplyStatus { get; set; }
        public string RestoreStatus { get; set; }

        public MovementSafeLandingEquipmentMoveRecord()
        {
            StrategyId = string.Empty;
            EquipmentCategory = string.Empty;
            ActionType = string.Empty;
            SelectedPriority = -1;
            SourceSlot = -1;
            TargetSlot = -1;
            CandidateMountType = -1;
            ImpactTicks = -1f;
            ImpactDistancePixels = -1;
            RescueItemSignature = new MovementSafeLandingEquipmentItemSignature();
            OriginalTargetItemSignature = new MovementSafeLandingEquipmentItemSignature();
            OriginalTargetHoldingSlot = -1;
            PostApplyVerificationReason = string.Empty;
            ApplyStatus = string.Empty;
            RestoreStatus = string.Empty;
        }
    }

    internal sealed class MovementSafeLandingEquipmentActionResult
    {
        public bool Invoked { get; set; }
        public bool BlockedByMouseItem { get; set; }
        public bool PulseQueued { get; set; }
        public bool PulseCompleted { get; set; }
        public bool PulseFailed { get; set; }
        public bool PulseNotRequired { get; set; }
        public bool FunctionalRefreshAttempted { get; set; }
        public bool FunctionalRefreshSucceeded { get; set; }
        public bool DoubleJumpRefreshAttempted { get; set; }
        public bool DoubleJumpRefreshSucceeded { get; set; }
        public bool SelectedSlotApplyAttempted { get; set; }
        public bool SelectedSlotApplySucceeded { get; set; }
        public bool SelectedSlotRestoreAttempted { get; set; }
        public bool SelectedSlotRestoreSucceeded { get; set; }
        public int AppliedMoveCount { get; set; }
        public int RestoredMoveCount { get; set; }
        public int PendingRestoreCount { get; set; }
        public int SkippedMoveCount { get; set; }
        public int UserChangedManagedSlotCount { get; set; }
        public int OriginalMovedByUserCount { get; set; }
        public int PendingRestoreNoSpaceCount { get; set; }
        public string StrategyId { get; set; }
        public string EquipmentCategory { get; set; }
        public string ActionType { get; set; }
        public string Decision { get; set; }
        public string SkipReason { get; set; }
        public string Message { get; set; }
        public string PulseStatus { get; set; }
        public string PulsePhase { get; set; }
        public string PulseApplySite { get; set; }
        public string PulseMessage { get; set; }
        public string FunctionalRefreshMessage { get; set; }
        public string SelectedSlotMessage { get; set; }
        public string PostApplyVerificationSummary { get; set; }
        public string PostApplyVerificationReason { get; set; }
        public bool PostApplyHasRocketBootsAvailable { get; set; }
        public bool PostApplyHasFlyingCarpet { get; set; }
        public bool PostApplyHasFlyingCarpetAvailable { get; set; }
        public bool PostApplyFlyingCarpetCanStart { get; set; }
        public int PostApplyFlyingCarpetTime { get; set; }
        public int PostApplyRocketBoots { get; set; }
        public float PostApplyRocketTime { get; set; }
        public int PostApplyRocketDelay { get; set; }
        public bool PostApplyCanRocket { get; set; }
        public bool PostApplyCanRocketKnown { get; set; }
        public bool PostApplyRocketRelease { get; set; }
        public bool PostApplyCanUseBootFlyingAbilities { get; set; }
        public bool PostApplyCanUseBootFlyingAbilitiesKnown { get; set; }
        public int PostApplyAirJumpFlagCount { get; set; }
        public bool PostApplyHasGravityGlobe { get; set; }
        public bool PostApplyHasGravityFlipOpportunity { get; set; }
        public bool PostApplyAerialJumpWindow { get; set; }
        public float PostApplyGravityDirection { get; set; }
        public bool PostApplyHasWingFlight { get; set; }
        public int PostApplyWingsLogic { get; set; }
        public float PostApplyWingTime { get; set; }
        public List<MovementSafeLandingEquipmentMoveRecord> Records { get; private set; }

        public MovementSafeLandingEquipmentActionResult()
        {
            StrategyId = string.Empty;
            EquipmentCategory = string.Empty;
            ActionType = string.Empty;
            Decision = string.Empty;
            SkipReason = string.Empty;
            Message = string.Empty;
            PulseStatus = string.Empty;
            PulsePhase = string.Empty;
            PulseApplySite = string.Empty;
            PulseMessage = string.Empty;
            FunctionalRefreshMessage = string.Empty;
            SelectedSlotMessage = string.Empty;
            PostApplyVerificationSummary = string.Empty;
            PostApplyVerificationReason = string.Empty;
            Records = new List<MovementSafeLandingEquipmentMoveRecord>();
        }
    }
}
