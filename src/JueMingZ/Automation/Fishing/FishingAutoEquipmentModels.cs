using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Fishing
{
    internal enum FishingEquipmentContainerKind
    {
        Unknown = 0,
        Inventory = 1,
        VoidBag = 2,
        Social = 3
    }

    internal enum FishingLiquidKind
    {
        Unknown = 0,
        Water = 1,
        Honey = 2,
        Lava = 3,
        Shimmer = 4
    }

    internal sealed class FishingAutoEquipmentItemSignature
    {
        public int Type { get; set; }
        public int Stack { get; set; }
        public int Prefix { get; set; }
        public string Name { get; set; }

        public bool IsAir
        {
            get { return Type <= 0 || Stack <= 0; }
        }

        public FishingAutoEquipmentItemSignature()
        {
            Name = string.Empty;
        }

        public bool Matches(FishingAutoEquipmentItemSignature other)
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

    internal sealed class FishingAutoEquipmentSessionInfo
    {
        public int OriginalSelectedItemIndex { get; set; }
        public FishingAutoEquipmentItemSignature OriginalRodSignature { get; set; }
        public int OriginalLoadoutIndex { get; set; }

        public FishingAutoEquipmentSessionInfo()
        {
            OriginalSelectedItemIndex = -1;
            OriginalLoadoutIndex = -1;
            OriginalRodSignature = new FishingAutoEquipmentItemSignature();
        }
    }

    internal sealed class FishingAutoEquipmentCandidate
    {
        public FishingEquipmentContainerKind SourceContainerKind { get; set; }
        public int SourceSlot { get; set; }
        public int SourcePriority { get; set; }
        public FishingAutoEquipmentItemSignature Signature { get; set; }
        public int Score { get; set; }
        public string EffectGroup { get; set; }
        public string Reason { get; set; }

        public FishingAutoEquipmentCandidate()
        {
            SourceSlot = -1;
            SourcePriority = 99;
            Signature = new FishingAutoEquipmentItemSignature();
            EffectGroup = string.Empty;
            Reason = string.Empty;
        }
    }

    internal sealed class FishingAutoEquipmentMovePlan
    {
        public int MoveId { get; set; }
        public int TargetEquipmentSlot { get; set; }
        public FishingEquipmentContainerKind SourceContainerKind { get; set; }
        public int SourceSlot { get; set; }
        public FishingAutoEquipmentItemSignature CandidateSignature { get; set; }
        public FishingAutoEquipmentItemSignature TargetSignatureAtPlan { get; set; }
        public int CandidateScore { get; set; }
        public int ExistingScore { get; set; }
        public string EffectGroup { get; set; }
        public string Reason { get; set; }

        public FishingAutoEquipmentMovePlan()
        {
            TargetEquipmentSlot = -1;
            SourceSlot = -1;
            CandidateSignature = new FishingAutoEquipmentItemSignature();
            TargetSignatureAtPlan = new FishingAutoEquipmentItemSignature();
            EffectGroup = string.Empty;
            Reason = string.Empty;
        }
    }

    internal sealed class FishingAutoEquipmentMoveRecord
    {
        public int MoveId { get; set; }
        public int TargetEquipmentSlot { get; set; }
        public FishingEquipmentContainerKind SourceContainerKind { get; set; }
        public int SourceSlot { get; set; }
        public FishingAutoEquipmentItemSignature FishingItemSignature { get; set; }
        public bool OriginalTargetWasAir { get; set; }
        public FishingAutoEquipmentItemSignature OriginalTargetItemSignature { get; set; }
        public FishingEquipmentContainerKind OriginalTargetHoldingContainerKind { get; set; }
        public int OriginalTargetHoldingSlot { get; set; }
        public string ApplyStatus { get; set; }
        public string RestoreStatus { get; set; }

        public FishingAutoEquipmentMoveRecord()
        {
            TargetEquipmentSlot = -1;
            SourceSlot = -1;
            OriginalTargetHoldingSlot = -1;
            FishingItemSignature = new FishingAutoEquipmentItemSignature();
            OriginalTargetItemSignature = new FishingAutoEquipmentItemSignature();
            ApplyStatus = string.Empty;
            RestoreStatus = string.Empty;
        }
    }

    internal sealed class FishingAutoEquipmentPlan
    {
        public FishingAutoEquipmentSessionInfo Session { get; set; }
        public List<FishingAutoEquipmentMovePlan> Moves { get; private set; }
        public string SkipReason { get; set; }
        public int CandidateCount { get; set; }

        public FishingAutoEquipmentPlan()
        {
            Session = new FishingAutoEquipmentSessionInfo();
            Moves = new List<FishingAutoEquipmentMovePlan>();
            SkipReason = string.Empty;
        }
    }

    internal sealed class FishingAutoEquipmentActionResult
    {
        public bool Invoked { get; set; }
        public bool BlockedByMouseItem { get; set; }
        public bool StillHoldingOriginalRod { get; set; }
        public bool LeftOriginalRod { get; set; }
        public bool LoadoutChangedDuringAutoEquipment { get; set; }
        public int AppliedMoveCount { get; set; }
        public int RestoredMoveCount { get; set; }
        public int PendingRestoreCount { get; set; }
        public int SkippedMoveCount { get; set; }
        public int UserChangedManagedSlotCount { get; set; }
        public int OriginalMovedByUserCount { get; set; }
        public int PendingRestoreNoSpaceCount { get; set; }
        public string Decision { get; set; }
        public string SkipReason { get; set; }
        public string Message { get; set; }
        public List<FishingAutoEquipmentMoveRecord> Records { get; private set; }

        public FishingAutoEquipmentActionResult()
        {
            Decision = string.Empty;
            SkipReason = string.Empty;
            Message = string.Empty;
            Records = new List<FishingAutoEquipmentMoveRecord>();
        }
    }
}
