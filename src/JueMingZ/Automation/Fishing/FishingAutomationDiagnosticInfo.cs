namespace JueMingZ.Automation.Fishing
{
    public sealed class FishingAutomationDiagnosticInfo
    {
        public bool FishingSessionActive { get; set; }
        public string FishingLastDecision { get; set; }
        public string FishingLastSkipReason { get; set; }
        public int FishingCurrentBobberIdentity { get; set; }
        public int FishingLastProcessedHookIdentity { get; set; }
        public bool FishingWaitingForBobberGone { get; set; }
        public int FishingRecastDelayTicks { get; set; }
        public bool FishingRecastWaitingForBobber { get; set; }
        public int FishingRecastBobberWaitTicks { get; set; }
        public int FishingRecastRetryCount { get; set; }
        public bool FishingFilterSkipInProgress { get; set; }
        public string FishingFilterSkipRequestId { get; set; }
        public bool FishingFilterSkipWaitingForBobberGone { get; set; }
        public int FishingFilterSkipTemporarySlot { get; set; }
        public string FishingFilterSkipLastResult { get; set; }
        public string FishingFilterSkipRestoreFailureReason { get; set; }
        public float FishingCastWorldX { get; set; }
        public float FishingCastWorldY { get; set; }
        public int FishingOriginalLoadoutIndex { get; set; }
        public int FishingTargetLoadoutIndex { get; set; }
        public bool FishingAutoEquipmentApplied { get; set; }
        public int FishingAutoEquipmentPendingRestoreCount { get; set; }
        public string FishingAutoEquipmentLastDecision { get; set; }
        public string FishingAutoEquipmentLastSkipReason { get; set; }
        public int FishingAutoEquipmentAppliedMoveCount { get; set; }
        public bool FishingAutoEquipmentStillHoldingOriginalRod { get; set; }
        public bool FishingAutoEquipmentManualInventoryInteractionDetected { get; set; }
        public int FishingQuestFishStoreCooldownTicks { get; set; }
        public int FishingQuestFishLastItemId { get; set; }
        public int FishingQuestFishLastSlotCount { get; set; }
        public string FishingAutoStoreLastMode { get; set; }
        public string FishingAutoStoreLastInventorySignature { get; set; }
        public string FishingAutoStoreLastPendingItemIds { get; set; }
        public string FishingAutoStoreLastDiagnosticMessage { get; set; }
        public bool FishingHookInstalled { get; set; }
        public long FishingHookLastObservationTick { get; set; }
        public long FishingFallbackScanExecutedCount { get; set; }
        public long FishingFallbackScanSkippedHookFreshCount { get; set; }
        public long FishingFallbackScanForcedDisappearanceConfirmationCount { get; set; }
        public string FishingAutomationDispatchReason { get; set; }
        public int FishingAutomationDispatchCadenceTicks { get; set; }
        public long FishingAutomationIdleFastSkipCount { get; set; }
        public long FishingAutomationIdleWatchdogTickCount { get; set; }
        public long FishingObserverFreshActiveCount { get; set; }
        public long FishingObserverFreshInactiveSkipCount { get; set; }
        public long FishingFallbackScanIdleSkippedCount { get; set; }
        public long FishingFallbackScanHookStaleCount { get; set; }
        public string FishingTickSubpathLast { get; set; }
        public int FishingResidualStateMask { get; set; }
        public string FishingFilterMode { get; set; }
        public string FishingFilterMatchMode { get; set; }
        public string FishingFilterCatchKind { get; set; }
        public int FishingFilterCatchId { get; set; }
        public string FishingFilterCatchName { get; set; }
        public string FishingFilterDecision { get; set; }
        public string FishingFilterDecisionReason { get; set; }
        public string FishingFilterMatchedRule { get; set; }
        public bool FishingFilterDryRun { get; set; }
        public bool FishingFilterCutRodSkipEnabled { get; set; }

        public FishingAutomationDiagnosticInfo()
        {
            FishingLastDecision = string.Empty;
            FishingLastSkipReason = string.Empty;
            FishingAutoEquipmentLastDecision = string.Empty;
            FishingAutoEquipmentLastSkipReason = string.Empty;
            FishingFilterMode = string.Empty;
            FishingFilterMatchMode = string.Empty;
            FishingAutoStoreLastMode = string.Empty;
            FishingAutoStoreLastInventorySignature = string.Empty;
            FishingAutoStoreLastPendingItemIds = string.Empty;
            FishingAutoStoreLastDiagnosticMessage = string.Empty;
            FishingAutomationDispatchReason = string.Empty;
            FishingTickSubpathLast = string.Empty;
            FishingFilterCatchKind = string.Empty;
            FishingFilterCatchName = string.Empty;
            FishingFilterDecision = string.Empty;
            FishingFilterDecisionReason = string.Empty;
            FishingFilterMatchedRule = string.Empty;
            FishingFilterSkipRequestId = string.Empty;
            FishingFilterSkipLastResult = string.Empty;
            FishingFilterSkipRestoreFailureReason = string.Empty;
            FishingCurrentBobberIdentity = -1;
            FishingLastProcessedHookIdentity = -1;
            FishingFilterSkipTemporarySlot = -1;
            FishingOriginalLoadoutIndex = -1;
            FishingTargetLoadoutIndex = -1;
        }
    }
}
