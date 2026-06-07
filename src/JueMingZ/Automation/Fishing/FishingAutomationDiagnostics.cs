using System;

namespace JueMingZ.Automation.Fishing
{
    // Fishing diagnostics mirror hook/session state and must not pull bobbers, swap rods, or refresh inventories.
    public static class FishingAutomationDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static bool _hookInstalled;
        private static long _hookLastObservationTick;
        private static FishingAutomationDiagnosticInfo _last = new FishingAutomationDiagnosticInfo();

        public static bool HookInstalled
        {
            get
            {
                lock (SyncRoot)
                {
                    return _hookInstalled;
                }
            }
        }

        public static void MarkHookInstalled()
        {
            lock (SyncRoot)
            {
                _hookInstalled = true;
            }
        }

        public static void MarkHookSkipped()
        {
            lock (SyncRoot)
            {
                _hookInstalled = false;
            }
        }

        public static void MarkHookObservation(long tick)
        {
            lock (SyncRoot)
            {
                _hookLastObservationTick = Math.Max(_hookLastObservationTick, tick);
            }
        }

        public static void Update(FishingAutomationDiagnosticInfo info)
        {
            if (info == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                info.FishingHookInstalled = _hookInstalled;
                info.FishingHookLastObservationTick = Math.Max(_hookLastObservationTick, FishingBobberObserver.LastObservationTick);
                _last = info;
            }
        }

        public static FishingAutomationDiagnosticInfo GetSnapshot()
        {
            lock (SyncRoot)
            {
                return new FishingAutomationDiagnosticInfo
                {
                    FishingSessionActive = _last.FishingSessionActive,
                    FishingLastDecision = _last.FishingLastDecision ?? string.Empty,
                    FishingLastSkipReason = _last.FishingLastSkipReason ?? string.Empty,
                    FishingCurrentBobberIdentity = _last.FishingCurrentBobberIdentity,
                    FishingLastProcessedHookIdentity = _last.FishingLastProcessedHookIdentity,
                    FishingWaitingForBobberGone = _last.FishingWaitingForBobberGone,
                    FishingRecastDelayTicks = _last.FishingRecastDelayTicks,
                    FishingRecastWaitingForBobber = _last.FishingRecastWaitingForBobber,
                    FishingRecastBobberWaitTicks = _last.FishingRecastBobberWaitTicks,
                    FishingRecastRetryCount = _last.FishingRecastRetryCount,
                    FishingFilterSkipInProgress = _last.FishingFilterSkipInProgress,
                    FishingFilterSkipRequestId = _last.FishingFilterSkipRequestId ?? string.Empty,
                    FishingFilterSkipWaitingForBobberGone = _last.FishingFilterSkipWaitingForBobberGone,
                    FishingFilterSkipTemporarySlot = _last.FishingFilterSkipTemporarySlot,
                    FishingFilterSkipLastResult = _last.FishingFilterSkipLastResult ?? string.Empty,
                    FishingFilterSkipRestoreFailureReason = _last.FishingFilterSkipRestoreFailureReason ?? string.Empty,
                    FishingCastWorldX = _last.FishingCastWorldX,
                    FishingCastWorldY = _last.FishingCastWorldY,
                    FishingOriginalLoadoutIndex = _last.FishingOriginalLoadoutIndex,
                    FishingTargetLoadoutIndex = _last.FishingTargetLoadoutIndex,
                    FishingAutoEquipmentApplied = _last.FishingAutoEquipmentApplied,
                    FishingAutoEquipmentPendingRestoreCount = _last.FishingAutoEquipmentPendingRestoreCount,
                    FishingAutoEquipmentLastDecision = _last.FishingAutoEquipmentLastDecision ?? string.Empty,
                    FishingAutoEquipmentLastSkipReason = _last.FishingAutoEquipmentLastSkipReason ?? string.Empty,
                    FishingAutoEquipmentAppliedMoveCount = _last.FishingAutoEquipmentAppliedMoveCount,
                    FishingAutoEquipmentStillHoldingOriginalRod = _last.FishingAutoEquipmentStillHoldingOriginalRod,
                    FishingAutoEquipmentManualInventoryInteractionDetected = _last.FishingAutoEquipmentManualInventoryInteractionDetected,
                    FishingQuestFishStoreCooldownTicks = _last.FishingQuestFishStoreCooldownTicks,
                    FishingQuestFishLastItemId = _last.FishingQuestFishLastItemId,
                    FishingQuestFishLastSlotCount = _last.FishingQuestFishLastSlotCount,
                    FishingAutoStoreLastMode = _last.FishingAutoStoreLastMode ?? string.Empty,
                    FishingAutoStoreLastInventorySignature = _last.FishingAutoStoreLastInventorySignature ?? string.Empty,
                    FishingAutoStoreLastPendingItemIds = _last.FishingAutoStoreLastPendingItemIds ?? string.Empty,
                    FishingAutoStoreLastDiagnosticMessage = _last.FishingAutoStoreLastDiagnosticMessage ?? string.Empty,
                    FishingHookInstalled = _hookInstalled,
                    FishingHookLastObservationTick = Math.Max(_hookLastObservationTick, FishingBobberObserver.LastObservationTick),
                    FishingFallbackScanExecutedCount = _last.FishingFallbackScanExecutedCount,
                    FishingFallbackScanSkippedHookFreshCount = _last.FishingFallbackScanSkippedHookFreshCount,
                    FishingFallbackScanForcedDisappearanceConfirmationCount = _last.FishingFallbackScanForcedDisappearanceConfirmationCount,
                    FishingAutomationDispatchReason = _last.FishingAutomationDispatchReason ?? string.Empty,
                    FishingAutomationDispatchCadenceTicks = _last.FishingAutomationDispatchCadenceTicks,
                    FishingAutomationIdleFastSkipCount = _last.FishingAutomationIdleFastSkipCount,
                    FishingAutomationIdleWatchdogTickCount = _last.FishingAutomationIdleWatchdogTickCount,
                    FishingObserverFreshActiveCount = _last.FishingObserverFreshActiveCount,
                    FishingObserverFreshInactiveSkipCount = _last.FishingObserverFreshInactiveSkipCount,
                    FishingFallbackScanIdleSkippedCount = _last.FishingFallbackScanIdleSkippedCount,
                    FishingFallbackScanHookStaleCount = _last.FishingFallbackScanHookStaleCount,
                    FishingTickSubpathLast = _last.FishingTickSubpathLast ?? string.Empty,
                    FishingResidualStateMask = _last.FishingResidualStateMask,
                    FishingFilterMode = _last.FishingFilterMode ?? string.Empty,
                    FishingFilterMatchMode = _last.FishingFilterMatchMode ?? string.Empty,
                    FishingFilterCatchKind = _last.FishingFilterCatchKind ?? string.Empty,
                    FishingFilterCatchId = _last.FishingFilterCatchId,
                    FishingFilterCatchName = _last.FishingFilterCatchName ?? string.Empty,
                    FishingFilterDecision = _last.FishingFilterDecision ?? string.Empty,
                    FishingFilterDecisionReason = _last.FishingFilterDecisionReason ?? string.Empty,
                    FishingFilterMatchedRule = _last.FishingFilterMatchedRule ?? string.Empty,
                    FishingFilterDryRun = _last.FishingFilterDryRun,
                    FishingFilterCutRodSkipEnabled = _last.FishingFilterCutRodSkipEnabled
                };
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _hookInstalled = false;
                _hookLastObservationTick = 0;
                _last = new FishingAutomationDiagnosticInfo();
            }
        }
    }
}
