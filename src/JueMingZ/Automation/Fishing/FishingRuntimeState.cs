using System;

namespace JueMingZ.Automation.Fishing
{
    internal sealed class FishingRuntimeState
    {
        public bool SessionActive { get; set; }
        public bool SessionStartedWithTruffleWorm { get; set; }
        public int SessionPoleSlot { get; set; }
        public int SessionPoleItemType { get; set; }
        public int CurrentBobberIdentity { get; set; }
        public int LastProcessedHookIdentity { get; set; }
        public int LastObservedPlayerLife { get; set; }
        public long LastBobberSeenTick { get; set; }
        public float CastWorldX { get; set; }
        public float CastWorldY { get; set; }
        public float LastBobberWorldX { get; set; }
        public float LastBobberWorldY { get; set; }
        public bool WaitingForBobberGone { get; set; }
        public long WaitingForBobberGoneStartTick { get; set; }
        public int RecastDelayTicks { get; set; }
        public bool RecastWaitingForBobber { get; set; }
        public int RecastBobberWaitTicks { get; set; }
        public int RecastRetryCount { get; set; }
        public Guid PullRequestId { get; set; }
        public Guid RecastRequestId { get; set; }
        public bool FilterSkipInProgress { get; set; }
        public Guid FilterSkipRequestId { get; set; }
        public bool FilterSkipWaitingForBobberGone { get; set; }
        public bool FilterSkipNaturalWaitForBobberGone { get; set; }
        public int FilterSkipTemporarySlot { get; set; }
        public string FilterSkipLastResult { get; set; }
        public string FilterSkipRestoreFailureReason { get; set; }
        public string LastDecision { get; set; }
        public string LastSkipReason { get; set; }
        public string FishingFilterMode { get; set; }
        public string FishingFilterMatchMode { get; set; }
        public string FishingFilterCatchKind { get; set; }
        public int FishingFilterCatchId { get; set; }
        public string FishingFilterCatchName { get; set; }
        public string FishingFilterDecision { get; set; }
        public string FishingFilterDecisionReason { get; set; }
        public string FishingFilterMatchedRule { get; set; }
        public bool FishingFilterDryRun { get; set; }

        public FishingRuntimeState()
        {
            Reset();
        }

        public void Reset()
        {
            SessionActive = false;
            SessionStartedWithTruffleWorm = false;
            SessionPoleSlot = -1;
            SessionPoleItemType = 0;
            CurrentBobberIdentity = -1;
            LastProcessedHookIdentity = -1;
            LastObservedPlayerLife = 0;
            LastBobberSeenTick = 0;
            CastWorldX = 0f;
            CastWorldY = 0f;
            LastBobberWorldX = 0f;
            LastBobberWorldY = 0f;
            WaitingForBobberGone = false;
            WaitingForBobberGoneStartTick = 0;
            RecastDelayTicks = 0;
            RecastWaitingForBobber = false;
            RecastBobberWaitTicks = 0;
            RecastRetryCount = 0;
            PullRequestId = Guid.Empty;
            RecastRequestId = Guid.Empty;
            FilterSkipInProgress = false;
            FilterSkipRequestId = Guid.Empty;
            FilterSkipWaitingForBobberGone = false;
            FilterSkipNaturalWaitForBobberGone = false;
            FilterSkipTemporarySlot = -1;
            FilterSkipLastResult = string.Empty;
            FilterSkipRestoreFailureReason = string.Empty;
            LastDecision = string.Empty;
            LastSkipReason = string.Empty;
            FishingFilterMode = string.Empty;
            FishingFilterMatchMode = string.Empty;
            FishingFilterCatchKind = string.Empty;
            FishingFilterCatchId = 0;
            FishingFilterCatchName = string.Empty;
            FishingFilterDecision = string.Empty;
            FishingFilterDecisionReason = string.Empty;
            FishingFilterMatchedRule = string.Empty;
            FishingFilterDryRun = false;
        }
    }
}
