using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Fishing
{
    internal enum FishingObserverFreshness
    {
        HookUnavailable,
        FreshActive,
        FreshInactive,
        Stale
    }

    public static class FishingAutomationService
    {
        private static readonly object SyncRoot = new object();
        private static readonly FishingRuntimeState State = new FishingRuntimeState();
        internal const int SonarBuffType = 122;
        internal const int IdleWatchdogCadenceTicks = 10;
        private const int ActiveDispatchCadenceTicks = 1;
        private const int RecastBobberWaitTimeoutTicks = 120;
        private const int MaxRecastRetryCount = 3;
        private const int WaitingForBobberGoneTimeoutTicks = 90;
        private const int FallbackScanIntervalTicks = 5;
        private const int HookFreshFallbackSkipTicks = 2;
        private const int ResidualSessionActive = 1 << 0;
        private const int ResidualWaitingForBobberGone = 1 << 1;
        private const int ResidualRecastDelay = 1 << 2;
        private const int ResidualRecastWaitingForBobber = 1 << 3;
        private const int ResidualPullRequest = 1 << 4;
        private const int ResidualRecastRequest = 1 << 5;
        private const int ResidualFilterSkipInProgress = 1 << 6;
        private const int ResidualFilterSkipRequest = 1 << 7;
        private const int ResidualFilterSkipWaitingForBobberGone = 1 << 8;
        private const int ResidualAutoEquipmentRestore = 1 << 9;
        private const int ResidualLoadoutRestore = 1 << 10;
        private const int ResidualQuestFishStorage = 1 << 11;
        private static long _lastFallbackScanTick;
        private static long _lastCompletedActionTick;
        private static long _fallbackScanExecutedCount;
        private static long _fallbackScanSkippedHookFreshCount;
        private static long _fallbackScanForcedDisappearanceConfirmationCount;
        private static long _fallbackScanIdleSkippedCount;
        private static long _fallbackScanHookStaleCount;
        private static long _idleFastSkipCount;
        private static long _idleWatchdogTickCount;
        private static long _observerFreshActiveCount;
        private static long _observerFreshInactiveSkipCount;
        private static string _lastDispatchReason = "disabled";
        private static int _lastDispatchCadenceTicks = ActiveDispatchCadenceTicks;
        private static string _lastTickSubpath = string.Empty;

        public static FishingAutomationDiagnosticInfo GetDiagnostics()
        {
            var snapshot = FishingAutomationDiagnostics.GetSnapshot();
            lock (SyncRoot)
            {
                snapshot.FishingAutomationDispatchReason = _lastDispatchReason;
                snapshot.FishingAutomationDispatchCadenceTicks = _lastDispatchCadenceTicks;
                snapshot.FishingAutomationIdleFastSkipCount = _idleFastSkipCount;
                snapshot.FishingAutomationIdleWatchdogTickCount = _idleWatchdogTickCount;
                snapshot.FishingObserverFreshActiveCount = _observerFreshActiveCount;
                snapshot.FishingObserverFreshInactiveSkipCount = _observerFreshInactiveSkipCount;
                snapshot.FishingFallbackScanIdleSkippedCount = _fallbackScanIdleSkippedCount;
                snapshot.FishingFallbackScanHookStaleCount = _fallbackScanHookStaleCount;
                snapshot.FishingTickSubpathLast = _lastTickSubpath;
                snapshot.FishingResidualStateMask = GetResidualStateMask();
            }

            return snapshot;
        }

        public static bool HasResidualState
        {
            get
            {
                return GetResidualStateMask() != 0;
            }
        }

        internal static int GetResidualStateMask()
        {
            var mask = HasLocalResidualState();
            if (FishingAutoEquipmentService.HasPendingRestore)
            {
                mask |= ResidualAutoEquipmentRestore;
            }

            if (FishingLoadoutService.HasResidualState)
            {
                mask |= ResidualLoadoutRestore;
            }

            if (FishingQuestFishStorageService.HasResidualState)
            {
                mask |= ResidualQuestFishStorage;
            }

            return mask;
        }

        internal static bool HasFreshActiveBobberForRuntime(long tick)
        {
            return GetObserverFreshness(tick) == FishingObserverFreshness.FreshActive;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                State.Reset();
                _lastFallbackScanTick = 0;
                _lastCompletedActionTick = 0;
                _fallbackScanExecutedCount = 0;
                _fallbackScanSkippedHookFreshCount = 0;
                _fallbackScanForcedDisappearanceConfirmationCount = 0;
                _fallbackScanIdleSkippedCount = 0;
                _fallbackScanHookStaleCount = 0;
                _idleFastSkipCount = 0;
                _idleWatchdogTickCount = 0;
                _observerFreshActiveCount = 0;
                _observerFreshInactiveSkipCount = 0;
                _lastDispatchReason = "disabled";
                _lastDispatchCadenceTicks = ActiveDispatchCadenceTicks;
                _lastTickSubpath = string.Empty;
            }

            FishingAutomationDiagnostics.ResetForTesting();
        }

        internal static string GetDispatchReasonForResidualMask(int residualMask)
        {
            return residualMask == 0
                ? "idle"
                : "residual:0x" + residualMask.ToString("X", CultureInfo.InvariantCulture);
        }

        internal static void RecordDispatchState(string reason, int cadenceTicks)
        {
            lock (SyncRoot)
            {
                _lastDispatchReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason;
                _lastDispatchCadenceTicks = cadenceTicks <= 0 ? ActiveDispatchCadenceTicks : cadenceTicks;
            }
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            Tick(queue, snapshot, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            try
            {
                if (IsIdleWatchdogDispatch())
                {
                    _idleWatchdogTickCount++;
                }

                SetTickSubpath("enter");
                settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
                var settings = settingsSnapshot.SourceSettings ?? AppSettings.CreateDefault();
                var autoStoreMode = settingsSnapshot.FishingAutoStoreMode;
                var residualMask = GetResidualStateMask();
                var anyEnabled = settingsSnapshot.FishingAutomationNeedsTick ||
                                 residualMask != 0;
                if (!anyEnabled)
                {
                    SetTickSubpath("disabledCleanup");
                    EndSession(queue, snapshot, tick, "allDisabled");
                    FishingQuestFishStorageService.Tick(queue, snapshot, State, autoStoreMode, tick);
                    PublishDiagnostics();
                    return;
                }

                if (!CanRun(snapshot))
                {
                    SetTickSubpath("blocked:notInWorldOrPlayerUnavailable");
                    FishingAutoEquipmentService.Tick(queue, snapshot, settingsSnapshot.FishingAutoEquipmentEnabled, false, FishingLiquidKind.Unknown, tick, "notInWorldOrPlayerUnavailable");
                    EndSession(queue, snapshot, tick, "notInWorldOrPlayerUnavailable");
                    Record("skipped", "notInWorldOrPlayerUnavailable");
                    PublishDiagnostics();
                    return;
                }

                var observerFreshness = GetObserverFreshness(tick);
                if (TryRunIdleFastPath(settingsSnapshot, observerFreshness, tick))
                {
                    PublishDiagnostics();
                    return;
                }

                var truffleWormBaitActive = IsCurrentBaitTruffleWorm(settings);
                var equipmentDisabledReason = truffleWormBaitActive ? "currentBaitTruffleWorm" : "disabled";
                var autoLoadoutEnabled = settingsSnapshot.FishingAutoLoadoutEnabled && !truffleWormBaitActive;
                var autoEquipmentEnabled = settingsSnapshot.FishingAutoEquipmentEnabled && !truffleWormBaitActive;

                List<FishingBobberObservation> scanned;
                var scanAvailable = TryRefreshFallbackScan(tick, observerFreshness, out scanned);
                FishingBobberObservation observation;
                var hasObservation = TrySelectObservation(scanned, out observation);
                if (hasObservation)
                {
                    SetTickSubpath("observation");
                }
                else if (scanAvailable)
                {
                    SetTickSubpath("fallbackScanNoObservation");
                }
                else if (string.Equals(_lastTickSubpath, "enter", StringComparison.Ordinal))
                {
                    SetTickSubpath("noObservation");
                }

                UpdateSessionFromObservation(queue, snapshot, observation, hasObservation, tick);
                var hasFishingEquipmentBobber = State.SessionActive &&
                                                hasObservation &&
                                                observation != null &&
                                                observation.InLiquid;
                var fishingLiquidKind = hasFishingEquipmentBobber ? observation.LiquidKind : FishingLiquidKind.Unknown;
                FishingAutoEquipmentService.Tick(queue, snapshot, autoEquipmentEnabled, hasFishingEquipmentBobber, fishingLiquidKind, tick, equipmentDisabledReason);

                if (State.SessionActive)
                {
                    SetTickSubpath("sessionActive");
                    var autoFishDispatchedBeforeLoadout = false;
                    if (settingsSnapshot.FishingAutoFishEnabled && ShouldDispatchAutoFishBeforeLoadout(hasObservation, observation))
                    {
                        TryDispatchAutoFish(queue, snapshot, settings, observation, hasObservation, scanned, scanAvailable, tick);
                        autoFishDispatchedBeforeLoadout = true;
                    }

                    FishingLoadoutService.Tick(queue, snapshot, autoLoadoutEnabled, true, tick, truffleWormBaitActive ? "currentBaitTruffleWorm" : null);
                    if (settingsSnapshot.FishingAutoFishEnabled && !autoFishDispatchedBeforeLoadout)
                    {
                        TryDispatchAutoFish(queue, snapshot, settings, observation, hasObservation, scanned, scanAvailable, tick);
                    }

                    FishingQuestFishStorageService.Tick(
                        queue,
                        snapshot,
                        State,
                        autoStoreMode,
                        tick);
                }
                else
                {
                    if (string.Equals(_lastTickSubpath, "observation", StringComparison.Ordinal))
                    {
                        SetTickSubpath("observationNoSession");
                    }

                    FishingLoadoutService.Tick(queue, snapshot, autoLoadoutEnabled, false, tick, truffleWormBaitActive ? "currentBaitTruffleWorm" : null);
                }

                PublishDiagnostics();
            }
            catch (Exception error)
            {
                Record("exception", "exception:" + error.GetType().Name);
                RuntimeDiagnostics.RecordError("FishingAutomationService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "fishing-automation-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "FishingAutomationService",
                    "Fishing automation tick failed; exception swallowed.", error);
                PublishDiagnostics();
            }
        }

        private static bool ShouldDispatchAutoFishBeforeLoadout(bool hasObservation, FishingBobberObservation observation)
        {
            if (State.WaitingForBobberGone || State.RecastDelayTicks > 0 || State.RecastWaitingForBobber)
            {
                return true;
            }

            return hasObservation &&
                   observation != null &&
                   observation.Ai1 < 0f &&
                   observation.Identity != State.LastProcessedHookIdentity;
        }

        private static bool TryRunIdleFastPath(
            RuntimeSettingsSnapshot settingsSnapshot,
            FishingObserverFreshness observerFreshness,
            long tick)
        {
            if (settingsSnapshot == null ||
                !settingsSnapshot.FishingAutomationNeedsTick ||
                GetResidualStateMask() != 0 ||
                observerFreshness != FishingObserverFreshness.FreshInactive)
            {
                return false;
            }

            FishingBobberObserver.MarkNoActiveObservation(GetCurrentFishingTick(tick));
            _idleFastSkipCount++;
            _observerFreshInactiveSkipCount++;
            _fallbackScanIdleSkippedCount++;
            SetTickSubpath("idleFastSkip:freshInactiveNoLocalBobber");
            Record("idleFastSkip", "freshInactiveNoLocalBobber");
            return true;
        }

        private static bool IsIdleWatchdogDispatch()
        {
            lock (SyncRoot)
            {
                return string.Equals(_lastDispatchReason, "idleWatchdog", StringComparison.Ordinal);
            }
        }

        private static void SetTickSubpath(string subpath)
        {
            _lastTickSubpath = subpath ?? string.Empty;
        }

        private static int HasLocalResidualState()
        {
            var mask = 0;
            if (State.SessionActive)
            {
                mask |= ResidualSessionActive;
            }

            if (State.WaitingForBobberGone)
            {
                mask |= ResidualWaitingForBobberGone;
            }

            if (State.RecastDelayTicks > 0)
            {
                mask |= ResidualRecastDelay;
            }

            if (State.RecastWaitingForBobber)
            {
                mask |= ResidualRecastWaitingForBobber;
            }

            if (State.PullRequestId != Guid.Empty)
            {
                mask |= ResidualPullRequest;
            }

            if (State.RecastRequestId != Guid.Empty)
            {
                mask |= ResidualRecastRequest;
            }

            if (State.FilterSkipInProgress)
            {
                mask |= ResidualFilterSkipInProgress;
            }

            if (State.FilterSkipRequestId != Guid.Empty)
            {
                mask |= ResidualFilterSkipRequest;
            }

            if (State.FilterSkipWaitingForBobberGone)
            {
                mask |= ResidualFilterSkipWaitingForBobberGone;
            }

            return mask;
        }

        private static bool IsCurrentBaitTruffleWorm(AppSettings settings)
        {
            if (settings == null || (!settings.FishingAutoLoadoutEnabled && !settings.FishingAutoEquipmentEnabled))
            {
                return false;
            }

            return TryIsCurrentBaitTruffleWorm();
        }

        private static bool TryIsCurrentBaitTruffleWorm()
        {
            bool isTruffleWorm;
            int baitItemType;
            return TerrariaFishingCompat.TryIsCurrentBaitTruffleWorm(out isTruffleWorm, out baitItemType) &&
                   isTruffleWorm;
        }

        public static void AfterActionQueueUpdate(InputActionQueueFastState queueSnapshot)
        {
            var result = queueSnapshot == null ? null : queueSnapshot.LastResult;
            if (result == null || string.IsNullOrWhiteSpace(result.Scenario) ||
                !result.Scenario.StartsWith("Fishing.", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lock (SyncRoot)
            {
                if (result.RequestId == State.PullRequestId)
                {
                    State.PullRequestId = Guid.Empty;
                }

                if (result.RequestId == State.RecastRequestId)
                {
                    State.RecastRequestId = Guid.Empty;
                }

                if (result.RequestId == State.FilterSkipRequestId)
                {
                    State.FilterSkipRequestId = Guid.Empty;
                    State.FilterSkipInProgress = false;
                    State.FilterSkipLastResult = result.Status.ToString();

                    string restoreFailureReason = result.Status.ToString();
                    if (TryConfirmSelectedSessionPole(out restoreFailureReason))
                    {
                        State.FilterSkipRestoreFailureReason = string.Empty;
                    }
                    else
                    {
                        State.FilterSkipRestoreFailureReason = string.IsNullOrWhiteSpace(restoreFailureReason)
                            ? result.Status.ToString()
                            : restoreFailureReason;
                    }
                }

                _lastCompletedActionTick = JueMingZRuntime.State == null ? 0 : JueMingZRuntime.State.UpdateCount;
            }

            FishingLoadoutService.OnActionCompleted(result);
            FishingAutoEquipmentService.OnActionCompleted(result);
            FishingQuestFishStorageService.OnActionCompleted(result, _lastCompletedActionTick);
        }

        private static void UpdateSessionFromObservation(
            InputActionQueue queue,
            GameStateSnapshot snapshot,
            FishingBobberObservation observation,
            bool hasObservation,
            long tick)
        {
            int poleSlot;
            int poleItemType;
            int fishingPole;
            if (!TerrariaFishingCompat.TryReadSelectedFishingPole(out poleSlot, out poleItemType, out fishingPole))
            {
                if (ShouldHoldSessionDuringControlledPoleTransition("selectedItemNotFishingPole"))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(State.FilterSkipRestoreFailureReason))
                {
                    EndSession(queue, snapshot, tick, "filterSkipRestoreFailed:" + State.FilterSkipRestoreFailureReason);
                    return;
                }

                EndSession(queue, snapshot, tick, "selectedItemNotFishingPole");
                return;
            }

            if (!hasObservation || observation == null)
            {
                if (State.RecastWaitingForBobber)
                {
                    Record("waitingRecastBobber", string.Empty);
                    return;
                }

                if (State.SessionActive &&
                    IsFilterSkipDeferred() &&
                    tick - State.LastBobberSeenTick > 5)
                {
                    State.LastProcessedHookIdentity = State.CurrentBobberIdentity;
                    State.FilterSkipWaitingForBobberGone = false;
                    State.FilterSkipNaturalWaitForBobberGone = false;
                    State.RecastDelayTicks = 2;
                    State.FishingFilterDecision = "filterSkipDeferredBobberGone";
                    State.FishingFilterDecisionReason = string.Empty;
                    State.FishingFilterDryRun = false;
                    Record("filterSkipDeferredBobberGone", string.Empty);
                    return;
                }

                if (State.SessionActive &&
                    !State.WaitingForBobberGone &&
                    State.RecastDelayTicks <= 0 &&
                    tick - State.LastBobberSeenTick > 120)
                {
                    if (AutoCaptureCritterService.HasFishingProtectionInFlight())
                    {
                        Record("sessionActive", "waitingForAutoCaptureProtection");
                        return;
                    }

                    if (FishingAutoEquipmentService.HasPendingRestore && IsStillHoldingSessionPole(poleSlot, poleItemType))
                    {
                        Record("sessionActiveWithoutBobber", "autoEquipmentStillHoldingSessionPole");
                        return;
                    }

                    EndSession(queue, snapshot, tick, "bobberLost");
                }

                return;
            }

            if (State.RecastWaitingForBobber)
            {
                State.RecastWaitingForBobber = false;
                State.RecastBobberWaitTicks = 0;
                State.RecastRetryCount = 0;
                State.LastProcessedHookIdentity = -1;
                Record("recastBobberObserved", string.Empty);
            }

            if (!State.SessionActive)
            {
                var truffleWormSession = TryIsCurrentBaitTruffleWorm();
                if (!ShouldStartSessionFromObservation(observation, truffleWormSession))
                {
                    Record("waitingBobberLiquid", observation.LiquidStateKnown ? "bobberNotInLiquid" : "bobberLiquidStateUnknown");
                    return;
                }

                StartSession(poleSlot, poleItemType, observation, tick, truffleWormSession);
                return;
            }

            if (State.SessionPoleSlot != poleSlot || State.SessionPoleItemType != poleItemType)
            {
                if (ShouldHoldSessionDuringControlledPoleTransition("poleChanged"))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(State.FilterSkipRestoreFailureReason))
                {
                    EndSession(queue, snapshot, tick, "filterSkipRestoreFailed:" + State.FilterSkipRestoreFailureReason);
                    return;
                }

                EndSession(queue, snapshot, tick, "poleChanged");
                return;
            }

            State.FilterSkipRestoreFailureReason = string.Empty;
            State.CurrentBobberIdentity = observation.Identity;
            State.LastBobberSeenTick = tick;
            State.LastBobberWorldX = observation.CenterX;
            State.LastBobberWorldY = observation.CenterY;
            Record("sessionActive", string.Empty);
        }

        private static bool ShouldHoldSessionDuringControlledPoleTransition(string reason)
        {
            if (!State.SessionActive)
            {
                return false;
            }

            if (State.FilterSkipInProgress)
            {
                Record("filterSkipInProgress", "waitingForRodRestore:" + (reason ?? string.Empty));
                return true;
            }

            if (AutoCaptureCritterService.HasFishingProtectionInFlight())
            {
                Record("sessionActive", "waitingForAutoCaptureProtection:" + (reason ?? string.Empty));
                return true;
            }

            return false;
        }

        private static void StartSession(int poleSlot, int poleItemType, FishingBobberObservation observation, long tick, bool truffleWormSession)
        {
            State.SessionActive = true;
            State.SessionStartedWithTruffleWorm = truffleWormSession;
            State.SessionPoleSlot = poleSlot;
            State.SessionPoleItemType = poleItemType;
            State.CurrentBobberIdentity = observation.Identity;
            State.LastProcessedHookIdentity = -1;
            State.LastBobberSeenTick = tick;
            State.LastBobberWorldX = observation.CenterX;
            State.LastBobberWorldY = observation.CenterY;
            State.WaitingForBobberGone = false;
            State.WaitingForBobberGoneStartTick = 0;
            State.RecastDelayTicks = 0;
            State.RecastWaitingForBobber = false;
            State.RecastBobberWaitTicks = 0;
            State.RecastRetryCount = 0;
            State.PullRequestId = Guid.Empty;
            State.RecastRequestId = Guid.Empty;
            State.FilterSkipInProgress = false;
            State.FilterSkipRequestId = Guid.Empty;
            State.FilterSkipWaitingForBobberGone = false;
            State.FilterSkipNaturalWaitForBobberGone = false;
            State.FilterSkipTemporarySlot = -1;
            State.FilterSkipLastResult = string.Empty;
            State.FilterSkipRestoreFailureReason = string.Empty;

            float mouseWorldX;
            float mouseWorldY;
            if (TerrariaFishingCompat.TryReadMouseWorld(out mouseWorldX, out mouseWorldY))
            {
                State.CastWorldX = mouseWorldX;
                State.CastWorldY = mouseWorldY;
            }
            else
            {
                State.CastWorldX = observation.CenterX;
                State.CastWorldY = observation.CenterY;
            }

            Record("sessionStarted", string.Empty);
            FishingStatusPromptService.ShowStart(tick, truffleWormSession);
        }

        internal static bool ShouldStartSessionFromObservation(FishingBobberObservation observation, bool truffleWormBait)
        {
            if (observation == null || !observation.Active || !observation.Bobber)
            {
                return false;
            }

            if (observation.LiquidStateKnown && observation.InLiquid)
            {
                return true;
            }

            return truffleWormBait &&
                   (!observation.LiquidStateKnown || observation.Ai1 < 0f);
        }

        private static void EndSession(InputActionQueue queue, GameStateSnapshot snapshot, long tick, string reason)
        {
            var wasActive = State.SessionActive;
            if (wasActive)
            {
                Record("sessionEnded", reason);
                FishingStatusPromptService.ShowStop(tick, ShouldShowFishronPromptOnEnd(reason));
            }

            State.Reset();
            FishingLoadoutService.TryRestore(queue, snapshot, tick, reason);
        }

        private static bool ShouldShowFishronPromptOnEnd(string reason)
        {
            if (!State.SessionStartedWithTruffleWorm)
            {
                return false;
            }

            return string.Equals(reason, "selectedItemNotFishingPole", StringComparison.Ordinal) ||
                   string.Equals(reason, "poleChanged", StringComparison.Ordinal) ||
                   string.Equals(reason, "bobberLost", StringComparison.Ordinal);
        }

        private static void TryDispatchAutoFish(
            InputActionQueue queue,
            GameStateSnapshot snapshot,
            AppSettings settings,
            FishingBobberObservation observation,
            bool hasObservation,
            IReadOnlyList<FishingBobberObservation> scanned,
            bool scanAvailable,
            long tick)
        {
            if (queue == null)
            {
                Record("skipped", "queueUnavailable");
                return;
            }

            if (State.WaitingForBobberGone)
            {
                if (IsBobberGone(State.LastProcessedHookIdentity, scanned, scanAvailable, tick))
                {
                    State.WaitingForBobberGone = false;
                    State.WaitingForBobberGoneStartTick = 0;
                    if (State.FilterSkipWaitingForBobberGone)
                    {
                        State.FilterSkipWaitingForBobberGone = false;
                        State.FilterSkipNaturalWaitForBobberGone = false;
                        if (!State.FilterSkipInProgress)
                        {
                            State.FilterSkipRequestId = Guid.Empty;
                            State.FilterSkipTemporarySlot = -1;
                        }

                        State.FishingFilterDecision = "filterSkipBobberGone";
                        State.FishingFilterDecisionReason = string.Empty;
                        State.FishingFilterDryRun = false;
                        Record("filterSkipBobberGone", string.Empty);
                    }

                    State.RecastDelayTicks = 2;
                    if (!string.Equals(State.LastDecision, "filterSkipBobberGone", StringComparison.Ordinal))
                    {
                        Record("waitingRecastDelay", string.Empty);
                    }
                }
                else if (State.WaitingForBobberGoneStartTick > 0 &&
                         tick - State.WaitingForBobberGoneStartTick >= WaitingForBobberGoneTimeoutTicks)
                {
                    if (State.FilterSkipWaitingForBobberGone)
                    {
                        if (!ShouldForcePullFilterSkipTimeout(State.FilterSkipWaitingForBobberGone, State.FilterSkipNaturalWaitForBobberGone))
                        {
                            return;
                        }

                        if (IsQueueBusy(queue))
                        {
                            Record("filterSkipTimeoutQueueBusy", "queueBusy");
                            return;
                        }

                        var forcedPullRequest = CreateItemUseRequest(
                            ScenarioNames.FishingAutoFishPull,
                            "Fishing auto pull (filter skip timeout)",
                            observation == null ? State.LastProcessedHookIdentity : observation.Identity,
                            false);
                        InputActionAdmissionResult forcedAdmission;
                        if (!queue.TryEnqueue(forcedPullRequest, out forcedAdmission))
                        {
                            Record("filterSkipTimeoutAdmissionDenied", forcedAdmission == null ? "unknown" : forcedAdmission.Reason);
                            return;
                        }

                        State.PullRequestId = forcedPullRequest.RequestId;
                        if (observation != null)
                        {
                            State.LastProcessedHookIdentity = observation.Identity;
                        }

                        State.FilterSkipWaitingForBobberGone = false;
                        State.FilterSkipNaturalWaitForBobberGone = false;
                        State.FilterSkipInProgress = false;
                        State.FilterSkipRequestId = Guid.Empty;
                        State.FilterSkipTemporarySlot = -1;
                        State.FilterSkipLastResult = string.Empty;
                        State.FilterSkipRestoreFailureReason = string.Empty;
                        State.WaitingForBobberGoneStartTick = tick;
                        State.FishingFilterDecision = "filterSkipTimeoutForcedPull";
                        State.FishingFilterDecisionReason = "waitingBobberGoneTimeout";
                        State.FishingFilterDryRun = false;
                        Record("filterSkipTimeoutForcedPull", "waitingBobberGoneTimeout");
                        return;
                    }

                    State.WaitingForBobberGone = false;
                    State.WaitingForBobberGoneStartTick = 0;
                    State.RecastDelayTicks = 1;
                    Record("waitingForBobberGoneTimeout", "forceRecast");
                }

                return;
            }

            if (State.RecastDelayTicks > 0)
            {
                State.RecastDelayTicks--;
                if (State.RecastDelayTicks > 0)
                {
                    Record("recastDelay", string.Empty);
                    return;
                }

                TryEnqueueRecast(queue);
                return;
            }

            if (TryHandleRecastBobberWait(queue, snapshot, observation, hasObservation, tick))
            {
                return;
            }

            if (!hasObservation || observation == null)
            {
                Record("skipped", "noLocalBobber");
                return;
            }

            if (observation.Ai1 >= 0f)
            {
                Record("observing", "bobberNotHooked");
                return;
            }

            if (observation.Identity == State.LastProcessedHookIdentity)
            {
                Record("skipped", "hookAlreadyProcessed");
                return;
            }

            if (LegacyMainUiState.Visible)
            {
                RecordFishingFilterNotRun(settings, "legacyMainUiVisible");
                Record("skipped", "legacyMainUiVisible");
                return;
            }

            FishingCatchCandidate candidate;
            FishingFilterDecision decision;
            var shouldKeep = EvaluateFishingFilter(settings, snapshot, observation, out candidate, out decision);

            // Auto fishing owns decisions only. Pulls, recasts, and filter skips
            // must remain ActionQueue requests instead of direct bobber/slot edits.
            if (shouldKeep)
            {
                RecordFishingFilterRun(settings, candidate, decision, "filterKeepPull", decision == null ? string.Empty : decision.Reason);
                if (IsQueueBusy(queue))
                {
                    Record("skipped", "queueBusy");
                    return;
                }

                var request = CreateItemUseRequest(
                    ScenarioNames.FishingAutoFishPull,
                    "Fishing auto pull",
                    observation.Identity,
                    false);
                InputActionAdmissionResult admission;
                if (!queue.TryEnqueue(request, out admission))
                {
                    Record("skipped", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason));
                    return;
                }

                if (ShouldRegisterCaughtItemForAutoStore(settings, candidate))
                {
                    FishingQuestFishStorageService.RegisterExpectedCaughtItem(request.RequestId, candidate.Id);
                }

                State.PullRequestId = request.RequestId;
                State.LastProcessedHookIdentity = observation.Identity;
                State.WaitingForBobberGone = true;
                State.WaitingForBobberGoneStartTick = tick;
                State.FilterSkipWaitingForBobberGone = false;
                State.FilterSkipNaturalWaitForBobberGone = false;
                Record("filterKeepPull", string.Empty);
                return;
            }

            TryDispatchFilterSkip(queue, snapshot, settings, observation, candidate, decision, tick);
        }

        private static void TryEnqueueRecast(InputActionQueue queue)
        {
            if (IsQueueBusy(queue))
            {
                State.RecastDelayTicks = 1;
                Record("skipped", "queueBusyBeforeRecast");
                return;
            }

            var request = CreateItemUseRequest(
                ScenarioNames.FishingAutoFishRecast,
                "Fishing auto recast",
                State.LastProcessedHookIdentity,
                true);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                State.RecastDelayTicks = 1;
                Record("skipped", "admissionDeniedBeforeRecast:" + (admission == null ? "unknown" : admission.Reason));
                return;
            }

            State.RecastRequestId = request.RequestId;
            State.CurrentBobberIdentity = -1;
            State.LastProcessedHookIdentity = -1;
            State.RecastWaitingForBobber = true;
            State.RecastBobberWaitTicks = 0;
            Record("submittedRecast", string.Empty);
        }

        private static bool TryHandleRecastBobberWait(
            InputActionQueue queue,
            GameStateSnapshot snapshot,
            FishingBobberObservation observation,
            bool hasObservation,
            long tick)
        {
            if (!State.RecastWaitingForBobber)
            {
                return false;
            }

            if (hasObservation && observation != null)
            {
                State.RecastWaitingForBobber = false;
                State.RecastBobberWaitTicks = 0;
                State.RecastRetryCount = 0;
                State.LastProcessedHookIdentity = -1;
                Record("recastBobberObserved", string.Empty);
                return false;
            }

            State.RecastBobberWaitTicks++;
            if (State.RecastBobberWaitTicks < RecastBobberWaitTimeoutTicks)
            {
                Record("waitingRecastBobber", string.Empty);
                return true;
            }

            if (State.RecastRetryCount < MaxRecastRetryCount)
            {
                State.RecastRetryCount++;
                State.RecastBobberWaitTicks = 0;
                State.RecastDelayTicks = 1;
                Record("retryRecastNoBobber", "retry:" + State.RecastRetryCount.ToString(CultureInfo.InvariantCulture));
                return true;
            }

            State.RecastWaitingForBobber = false;
            State.RecastBobberWaitTicks = 0;
            EndSession(queue, snapshot, tick, "recastBobberNotObserved");
            return true;
        }

        private static bool ShouldRegisterCaughtItemForAutoStore(AppSettings settings, FishingCatchCandidate candidate)
        {
            if (settings == null || candidate == null || candidate.Id <= 0 ||
                !string.Equals(candidate.Kind, FishingCatchKinds.Item, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var autoStoreMode = FishingAutoStoreModes.Normalize(settings.FishingAutoStoreMode, settings.FishingAutoStoreQuestFishEnabled);
            return string.Equals(autoStoreMode, FishingAutoStoreModes.All, StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDispatchFilterSkip(
            InputActionQueue queue,
            GameStateSnapshot snapshot,
            AppSettings settings,
            FishingBobberObservation observation,
            FishingCatchCandidate candidate,
            FishingFilterDecision decision,
            long tick)
        {
            if (IsQueueBusy(queue))
            {
                RecordFishingFilterRun(settings, candidate, decision, "filterSkipQueueBusy", "filterSkipQueueBusy");
                Record("filterSkipQueueBusy", "queueBusy");
                return;
            }

            // Filter skip protects the existing session pole: temporary slot changes
            // wait for bobber disappearance or restore instead of overwriting selectedItem.
            string unsafeReason;
            if (IsUnsafeForFilterSkip(snapshot, out unsafeReason))
            {
                if (IsTransientFilterSkipBlock(unsafeReason))
                {
                    DeferFilterSkip(settings, candidate, decision, unsafeReason);
                    return;
                }

                BeginFilterSkipNaturalWait(settings, observation, candidate, decision, unsafeReason, tick);
                return;
            }

            if (!settings.FishingFilterCutRodSkipEnabled)
            {
                BeginFilterSkipNaturalWait(settings, observation, candidate, decision, "cutRodSkipDisabled", tick);
                return;
            }

            int temporarySlot;
            string noSlotReason;
            if (!TryChooseFilterSkipHotbarSlot(snapshot, out temporarySlot, out noSlotReason))
            {
                BeginFilterSkipNaturalWait(settings, observation, candidate, decision, noSlotReason, tick);
                return;
            }

            var request = CreateFilterSkipRequest(temporarySlot, observation, candidate, decision);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                BeginFilterSkipNaturalWait(settings, observation, candidate, decision, "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick);
                return;
            }

            State.LastProcessedHookIdentity = observation.Identity;
            State.WaitingForBobberGone = true;
            State.WaitingForBobberGoneStartTick = tick;
            State.FilterSkipWaitingForBobberGone = true;
            State.FilterSkipNaturalWaitForBobberGone = false;
            State.FilterSkipInProgress = true;
            State.FilterSkipRequestId = request.RequestId;
            State.FilterSkipTemporarySlot = temporarySlot;
            State.FilterSkipLastResult = string.Empty;
            State.FilterSkipRestoreFailureReason = string.Empty;
            RecordFishingFilterRun(settings, candidate, decision, "submittedFilterSkip", decision == null ? string.Empty : decision.Reason);
            Record("submittedFilterSkip", string.Empty);
        }

        private static void BeginFilterSkipNaturalWait(
            AppSettings settings,
            FishingBobberObservation observation,
            FishingCatchCandidate candidate,
            FishingFilterDecision decision,
            string reason,
            long tick)
        {
            State.LastProcessedHookIdentity = observation == null ? -1 : observation.Identity;
            State.WaitingForBobberGone = true;
            State.WaitingForBobberGoneStartTick = tick;
            State.FilterSkipWaitingForBobberGone = true;
            State.FilterSkipNaturalWaitForBobberGone = true;
            State.FilterSkipInProgress = false;
            State.FilterSkipRequestId = Guid.Empty;
            State.FilterSkipTemporarySlot = -1;
            State.FilterSkipLastResult = string.Empty;
            State.FilterSkipRestoreFailureReason = string.Empty;
            RecordFishingFilterRun(settings, candidate, decision, "skipFallbackWaitNatural", reason);
            Record("skipFallbackWaitNatural", reason ?? string.Empty);
        }

        private static void DeferFilterSkip(
            AppSettings settings,
            FishingCatchCandidate candidate,
            FishingFilterDecision decision,
            string reason)
        {
            State.FilterSkipInProgress = false;
            State.FilterSkipRequestId = Guid.Empty;
            State.FilterSkipTemporarySlot = -1;
            State.FilterSkipLastResult = string.Empty;
            State.FilterSkipRestoreFailureReason = string.Empty;
            State.FilterSkipNaturalWaitForBobberGone = false;
            RecordFishingFilterRun(settings, candidate, decision, "filterSkipDeferred", reason);
            Record("filterSkipDeferred", reason ?? string.Empty);
        }

        private static bool ShouldForcePullFilterSkipTimeout(bool waitingForBobberGone, bool naturalWaitForBobberGone)
        {
            return waitingForBobberGone && !naturalWaitForBobberGone;
        }

        internal static bool ShouldForcePullFilterSkipTimeoutForTesting(
            bool waitingForBobberGone,
            bool naturalWaitForBobberGone,
            long waitStartTick,
            long tick)
        {
            return waitStartTick > 0 &&
                   tick - waitStartTick >= WaitingForBobberGoneTimeoutTicks &&
                   ShouldForcePullFilterSkipTimeout(waitingForBobberGone, naturalWaitForBobberGone);
        }

        private static bool IsFilterSkipDeferred()
        {
            return string.Equals(State.LastDecision, "filterSkipDeferred", StringComparison.Ordinal);
        }

        private static bool IsTransientFilterSkipBlock(string reason)
        {
            return string.Equals(reason, "uiBlockingWorldInput", StringComparison.Ordinal) ||
                   string.Equals(reason, "filterSkipUnsafeMouseItem", StringComparison.Ordinal);
        }

        private static bool IsUnsafeForFilterSkip(GameStateSnapshot snapshot, out string reason)
        {
            reason = string.Empty;
            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                reason = "filterSkipUnsafeMouseItem";
                return true;
            }

            if (snapshot == null || !snapshot.IsInWorld || (snapshot.Ui != null && snapshot.Ui.HasBlockingUi))
            {
                reason = "uiBlockingWorldInput";
                return true;
            }

            return false;
        }

        private static bool TryChooseFilterSkipHotbarSlot(GameStateSnapshot snapshot, out int slot, out string reason)
        {
            slot = -1;
            reason = string.Empty;
            if (State.SessionPoleSlot < 0 || State.SessionPoleSlot > 9)
            {
                reason = "filterSkipNoSafeSlot";
                return false;
            }

            for (var index = 0; index < 10; index++)
            {
                if (index == State.SessionPoleSlot)
                {
                    continue;
                }

                int itemType;
                int stack;
                int fishingPole;
                if (TerrariaFishingCompat.TryReadHotbarSlotInfo(index, out itemType, out stack, out fishingPole) &&
                    (itemType <= 0 || stack <= 0))
                {
                    slot = index;
                    return true;
                }

                if (snapshot != null &&
                    snapshot.Inventory != null &&
                    snapshot.Inventory.Items != null)
                {
                    for (var itemIndex = 0; itemIndex < snapshot.Inventory.Items.Count; itemIndex++)
                    {
                        var item = snapshot.Inventory.Items[itemIndex];
                        if (item != null && item.SlotIndex == index && (item.Type <= 0 || item.Stack <= 0))
                        {
                            slot = index;
                            return true;
                        }
                    }
                }
            }

            for (var index = 0; index < 10; index++)
            {
                if (index == State.SessionPoleSlot)
                {
                    continue;
                }

                int itemType;
                int stack;
                int fishingPole;
                if (!TerrariaFishingCompat.TryReadHotbarSlotInfo(index, out itemType, out stack, out fishingPole))
                {
                    continue;
                }

                if (itemType > 0 && stack > 0 && fishingPole <= 0)
                {
                    slot = index;
                    return true;
                }
            }

            reason = "filterSkipNoSafeSlot";
            return false;
        }

        private static InputActionRequest CreateItemUseRequest(string scenario, string description, int bobberIdentity, bool includeWorldTarget)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.ItemUse,
                Priority = InputActionPriority.Normal,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.FishingAutoFish,
                Description = description,
                QueueTimeout = TimeSpan.FromMilliseconds(500),
                AdmissionKey = FeatureIds.FishingAutoFish + "|" + scenario + "|" + bobberIdentity.ToString(CultureInfo.InvariantCulture),
                Timeout = TimeSpan.FromSeconds(3)
            };
            request.Metadata[ActionMetadataKeys.Scenario] = scenario;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.TargetSlot] = State.SessionPoleSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ApplyMainMouseLeftForItemCheck"] = "true";
            request.Metadata["FishingBobberIdentity"] = bobberIdentity.ToString(CultureInfo.InvariantCulture);
            request.Metadata["FishingPoleItemType"] = State.SessionPoleItemType.ToString(CultureInfo.InvariantCulture);
            if (includeWorldTarget)
            {
                request.Metadata[ActionMetadataKeys.WorldX] = State.CastWorldX.ToString(CultureInfo.InvariantCulture);
                request.Metadata[ActionMetadataKeys.WorldY] = State.CastWorldY.ToString(CultureInfo.InvariantCulture);
            }

            return request;
        }

        private static InputActionRequest CreateFilterSkipRequest(
            int temporarySlot,
            FishingBobberObservation observation,
            FishingCatchCandidate candidate,
            FishingFilterDecision decision)
        {
            return CreateFilterSkipRequest(
                temporarySlot,
                State.SessionPoleSlot,
                State.SessionPoleItemType,
                observation,
                candidate,
                decision);
        }

        internal static InputActionRequest BuildFilterSkipRequestForTesting(
            int temporarySlot,
            int bobberIdentity,
            int poleSlot,
            int poleItemType)
        {
            return CreateFilterSkipRequest(
                temporarySlot,
                poleSlot,
                poleItemType,
                new FishingBobberObservation { Identity = bobberIdentity },
                new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = 1,
                    DisplayName = "Test Catch",
                    DisplayNameSnapshot = "Test Catch"
                },
                new FishingFilterDecision
                {
                    ShouldKeep = false,
                    Action = "Skip",
                    Reason = "test",
                    MatchedRule = "test",
                    FilterMode = FishingFilterModes.AllowList,
                    MatchMode = FishingFilterMatchModes.Exact
                });
        }

        private static InputActionRequest CreateFilterSkipRequest(
            int temporarySlot,
            int poleSlot,
            int poleItemType,
            FishingBobberObservation observation,
            FishingCatchCandidate candidate,
            FishingFilterDecision decision)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.SelectHotbarSlot,
                Priority = InputActionPriority.Normal,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.FishingFilter,
                Description = "Fishing filter skip",
                QueueTimeout = TimeSpan.FromMilliseconds(500),
                AdmissionKey = FeatureIds.FishingFilter + "|skip|" + (observation == null ? "-1" : observation.Identity.ToString(CultureInfo.InvariantCulture)),
                Timeout = TimeSpan.FromSeconds(4)
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.FishingFilterSkip;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["Slot"] = temporarySlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["RestoreAfterTicks"] = "1";
            request.Metadata["PreferImmediateSelection"] = "true";
            request.Metadata["HoldUntilFishingBobberGone"] = "true";
            request.Metadata["MaxBobberGoneWaitTicks"] = WaitingForBobberGoneTimeoutTicks.ToString(CultureInfo.InvariantCulture);
            request.Metadata["FishingBobberIdentity"] = observation == null ? "-1" : observation.Identity.ToString(CultureInfo.InvariantCulture);
            request.Metadata["FishingPoleSlot"] = poleSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["FishingPoleItemType"] = poleItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["FishingFilterCatchKind"] = candidate == null ? FishingCatchKinds.Unknown : FirstNonEmpty(candidate.Kind, FishingCatchKinds.Unknown);
            request.Metadata["FishingFilterCatchId"] = candidate == null ? "0" : candidate.Id.ToString(CultureInfo.InvariantCulture);
            request.Metadata["FishingFilterCatchName"] = candidate == null ? string.Empty : FirstNonEmpty(candidate.DisplayName, candidate.DisplayNameSnapshot, "Unknown");
            request.Metadata["FishingFilterDecisionReason"] = decision == null ? string.Empty : decision.Reason ?? string.Empty;
            request.Metadata["FishingFilterMatchedRule"] = decision == null ? string.Empty : decision.MatchedRule ?? string.Empty;
            request.Metadata["FishingFilterMode"] = decision == null ? string.Empty : decision.FilterMode ?? string.Empty;
            request.Metadata["FishingFilterMatchMode"] = decision == null ? string.Empty : decision.MatchMode ?? string.Empty;
            return request;
        }

        private static bool TrySelectObservation(IReadOnlyList<FishingBobberObservation> scanned, out FishingBobberObservation observation)
        {
            observation = null;
            if (scanned != null && scanned.Count > 0)
            {
                observation = scanned[0];
                for (var index = 1; index < scanned.Count; index++)
                {
                    if (scanned[index] != null &&
                        (observation == null || scanned[index].GameUpdateCount >= observation.GameUpdateCount))
                    {
                        observation = scanned[index];
                    }
                }

                return observation != null;
            }

            return FishingBobberObserver.TryGetLatest(out observation);
        }

        private static bool TryRefreshFallbackScan(long tick, FishingObserverFreshness observerFreshness, out List<FishingBobberObservation> scanned)
        {
            scanned = null;
            var forceDisappearanceConfirmation = State.WaitingForBobberGone || State.RecastWaitingForBobber;
            var forceBobberTransitionScan = forceDisappearanceConfirmation || State.RecastDelayTicks > 0;
            var shouldScan = tick - _lastFallbackScanTick >= FallbackScanIntervalTicks ||
                             forceBobberTransitionScan;
            if (!shouldScan)
            {
                return false;
            }

            if (ShouldSkipFallbackScanBecauseHookFresh(forceBobberTransitionScan, observerFreshness, tick))
            {
                _lastFallbackScanTick = tick;
                return false;
            }

            _lastFallbackScanTick = tick;
            _fallbackScanExecutedCount++;
            if (observerFreshness == FishingObserverFreshness.Stale)
            {
                _fallbackScanHookStaleCount++;
            }

            if (forceDisappearanceConfirmation)
            {
                _fallbackScanForcedDisappearanceConfirmationCount++;
            }

            if (!TerrariaFishingCompat.TryScanLocalBobbers(out scanned))
            {
                scanned = null;
                return false;
            }

            FishingBobberObserver.RemoveMissing(scanned);
            if (scanned.Count == 0)
            {
                FishingBobberObserver.MarkNoActiveObservation(GetCurrentFishingTick(tick));
            }

            return true;
        }

        private static bool ShouldSkipFallbackScanBecauseHookFresh(
            bool forceBobberTransitionScan,
            FishingObserverFreshness observerFreshness,
            long tick)
        {
            if (forceBobberTransitionScan)
            {
                return false;
            }

            if (observerFreshness == FishingObserverFreshness.FreshActive)
            {
                _fallbackScanSkippedHookFreshCount++;
                _observerFreshActiveCount++;
                SetTickSubpath("fallbackScanSkipped:freshActive");
                return true;
            }

            if (observerFreshness == FishingObserverFreshness.FreshInactive)
            {
                FishingBobberObserver.MarkNoActiveObservation(GetCurrentFishingTick(tick));
                _fallbackScanIdleSkippedCount++;
                _observerFreshInactiveSkipCount++;
                SetTickSubpath("fallbackScanSkipped:freshInactiveNoLocalBobber");
                return true;
            }

            return false;
        }

        internal static bool ShouldSkipFallbackScanForTesting(
            bool hookInstalled,
            bool observerHasActiveObservation,
            long hookLastObservationTick,
            long currentGameUpdateCount,
            bool forceBobberTransitionScan)
        {
            return ShouldSkipFallbackScanForFreshHook(
                hookInstalled,
                observerHasActiveObservation,
                hookLastObservationTick,
                0,
                currentGameUpdateCount,
                forceBobberTransitionScan);
        }

        internal static bool ShouldSkipFallbackScanForTesting(
            bool hookInstalled,
            bool observerHasActiveObservation,
            long hookLastObservationTick,
            long hookLastNoActiveObservationTick,
            long currentGameUpdateCount,
            bool forceBobberTransitionScan)
        {
            return ShouldSkipFallbackScanForFreshHook(
                hookInstalled,
                observerHasActiveObservation,
                hookLastObservationTick,
                hookLastNoActiveObservationTick,
                currentGameUpdateCount,
                forceBobberTransitionScan);
        }

        private static bool ShouldSkipFallbackScanForFreshHook(
            bool hookInstalled,
            bool observerHasActiveObservation,
            long hookLastObservationTick,
            long hookLastNoActiveObservationTick,
            long currentGameUpdateCount,
            bool forceBobberTransitionScan)
        {
            if (forceBobberTransitionScan ||
                !hookInstalled ||
                currentGameUpdateCount <= 0)
            {
                return false;
            }

            if (observerHasActiveObservation && hookLastObservationTick > 0)
            {
                var activeAge = currentGameUpdateCount - hookLastObservationTick;
                if (activeAge >= 0 && activeAge <= HookFreshFallbackSkipTicks)
                {
                    return true;
                }
            }

            if (!observerHasActiveObservation &&
                hookLastNoActiveObservationTick > 0 &&
                hookLastObservationTick <= hookLastNoActiveObservationTick)
            {
                var inactiveAge = currentGameUpdateCount - hookLastNoActiveObservationTick;
                return inactiveAge >= 0 && inactiveAge <= IdleWatchdogCadenceTicks;
            }

            return false;
        }

        private static bool IsBobberGone(int identity, IReadOnlyList<FishingBobberObservation> scanned, bool scanAvailable, long tick)
        {
            if (identity < 0)
            {
                return true;
            }

            if (scanned != null)
            {
                for (var index = 0; index < scanned.Count; index++)
                {
                    if (scanned[index] != null && scanned[index].Identity == identity)
                    {
                        return false;
                    }
                }

                return true;
            }

            FishingBobberObservation observation;
            if (FishingBobberObserver.TryGetByIdentity(identity, out observation) && observation != null)
            {
                return tick - observation.GameUpdateCount > 5;
            }

            return scanAvailable;
        }

        private static bool CanRun(GameStateSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.IsInWorld &&
                   snapshot.Player != null &&
                   snapshot.Player.Exists &&
                   snapshot.Player.Active &&
                   !snapshot.Player.Dead &&
                   !snapshot.Player.Ghost;
        }

        internal static FishingObserverFreshness GetObserverFreshnessForTesting(
            bool hookInstalled,
            bool hasFreshActiveObservation,
            bool hasFreshNoActiveObservation)
        {
            return GetObserverFreshness(hookInstalled, hasFreshActiveObservation, hasFreshNoActiveObservation);
        }

        private static FishingObserverFreshness GetObserverFreshness(long tick)
        {
            if (!FishingAutomationDiagnostics.HookInstalled)
            {
                return FishingObserverFreshness.HookUnavailable;
            }

            var currentTick = GetCurrentFishingTick(tick);
            return GetObserverFreshness(
                true,
                FishingBobberObserver.HasFreshActiveObservation(currentTick, HookFreshFallbackSkipTicks),
                FishingBobberObserver.HasFreshNoActiveObservation(currentTick, IdleWatchdogCadenceTicks));
        }

        private static FishingObserverFreshness GetObserverFreshness(
            bool hookInstalled,
            bool hasFreshActiveObservation,
            bool hasFreshNoActiveObservation)
        {
            if (!hookInstalled)
            {
                return FishingObserverFreshness.HookUnavailable;
            }

            if (hasFreshActiveObservation)
            {
                return FishingObserverFreshness.FreshActive;
            }

            if (hasFreshNoActiveObservation)
            {
                return FishingObserverFreshness.FreshInactive;
            }

            return FishingObserverFreshness.Stale;
        }

        private static long GetCurrentFishingTick(long fallbackTick)
        {
            long currentGameUpdateCount;
            if (TerrariaFishingCompat.TryReadGameUpdateCount(out currentGameUpdateCount) && currentGameUpdateCount > 0)
            {
                return currentGameUpdateCount;
            }

            return fallbackTick;
        }

        internal static bool IsFishingFilterEnabled(AppSettings settings)
        {
            var effectiveSettings = settings ?? ConfigService.AppSettings ?? AppSettings.CreateDefault();
            return !string.Equals(
                FishingFilterModes.Normalize(effectiveSettings.FishingFilterMode),
                FishingFilterModes.Disabled,
                StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsFishingFilterActiveForCatch(AppSettings settings, GameStateSnapshot snapshot)
        {
            return IsFishingFilterEnabled(settings) && HasSonarBuff(snapshot);
        }

        internal static bool HasSonarBuff(GameStateSnapshot snapshot)
        {
            return snapshot != null && HasSonarBuff(snapshot.ActiveBuffs);
        }

        internal static bool HasSonarBuffOnPlayer(object player)
        {
            return player != null && PlayerBuffCompat.HasActiveBuff(player, SonarBuffType);
        }

        private static bool HasSonarBuff(IReadOnlyList<BuffSnapshot> activeBuffs)
        {
            if (activeBuffs == null)
            {
                return false;
            }

            for (var index = 0; index < activeBuffs.Count; index++)
            {
                var buff = activeBuffs[index];
                if (buff != null && buff.BuffType == SonarBuffType && buff.BuffTime > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStillHoldingSessionPole(int poleSlot, int poleItemType)
        {
            return State.SessionActive &&
                   State.SessionPoleSlot == poleSlot &&
                   State.SessionPoleItemType == poleItemType;
        }

        private static bool IsQueueBusy(InputActionQueue queue)
        {
            var snapshot = queue == null ? null : queue.GetFastState();
            return snapshot == null ||
                   snapshot.PendingCount > 0 || snapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static bool TryConfirmSelectedSessionPole(out string reason)
        {
            reason = string.Empty;
            if (!State.SessionActive)
            {
                reason = "sessionInactive";
                return false;
            }

            int poleSlot;
            int poleItemType;
            int fishingPole;
            if (!TerrariaFishingCompat.TryReadSelectedFishingPole(out poleSlot, out poleItemType, out fishingPole))
            {
                reason = "selectedItemNotFishingPole";
                return false;
            }

            if (State.SessionPoleSlot != poleSlot || State.SessionPoleItemType != poleItemType)
            {
                reason = "selectedPoleMismatch";
                return false;
            }

            return true;
        }

        private static bool EvaluateFishingFilter(
            AppSettings settings,
            GameStateSnapshot snapshot,
            FishingBobberObservation observation,
            out FishingCatchCandidate candidate,
            out FishingFilterDecision decision)
        {
            candidate = null;
            decision = null;
            try
            {
                var effectiveSettings = settings ?? ConfigService.AppSettings ?? AppSettings.CreateDefault();
                FishingHookedCatchResolver.TryResolve(observation, out candidate);
                if (IsFishingFilterEnabled(effectiveSettings) && !HasSonarBuff(snapshot))
                {
                    decision = CreateKeepDecision(effectiveSettings, "sonarBuffMissing");
                    return true;
                }

                decision = FishingFilterDecisionService.Decide(effectiveSettings, candidate);
                return decision == null || decision.ShouldKeep;
            }
            catch (Exception error)
            {
                candidate = new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Unknown,
                    Id = 0,
                    DisplayName = "Unknown",
                    DisplayNameSnapshot = "Unknown"
                };
                decision = null;
                RuntimeDiagnostics.RecordError("FishingAutomationService.EvaluateFishingFilter", error);
                return true;
            }
        }

        private static FishingFilterDecision CreateKeepDecision(AppSettings settings, string reason)
        {
            var effectiveSettings = settings ?? ConfigService.AppSettings ?? AppSettings.CreateDefault();
            return new FishingFilterDecision
            {
                ShouldKeep = true,
                Action = "Keep",
                Reason = reason ?? string.Empty,
                MatchedRule = string.Empty,
                MatchMode = FishingFilterMatchModes.Normalize(effectiveSettings.FishingFilterMatchMode),
                FilterMode = FishingFilterModes.Normalize(effectiveSettings.FishingFilterMode)
            };
        }

        private static void RecordFishingFilterRun(
            AppSettings settings,
            FishingCatchCandidate candidate,
            FishingFilterDecision decision,
            string stateDecision,
            string reason)
        {
            var effectiveSettings = settings ?? ConfigService.AppSettings ?? AppSettings.CreateDefault();
            State.FishingFilterMode = FirstNonEmpty(
                decision == null ? string.Empty : decision.FilterMode,
                FishingFilterModes.Normalize(effectiveSettings.FishingFilterMode));
            State.FishingFilterMatchMode = FirstNonEmpty(
                decision == null ? string.Empty : decision.MatchMode,
                FishingFilterMatchModes.Normalize(effectiveSettings.FishingFilterMatchMode));
            State.FishingFilterCatchKind = candidate == null ? FishingCatchKinds.Unknown : FirstNonEmpty(candidate.Kind, FishingCatchKinds.Unknown);
            State.FishingFilterCatchId = candidate == null ? 0 : candidate.Id;
            State.FishingFilterCatchName = candidate == null ? string.Empty : FirstNonEmpty(candidate.DisplayName, candidate.DisplayNameSnapshot, "Unknown");
            State.FishingFilterDecision = stateDecision ?? string.Empty;
            State.FishingFilterDecisionReason = reason ?? (decision == null ? string.Empty : decision.Reason ?? string.Empty);
            State.FishingFilterMatchedRule = decision == null ? string.Empty : decision.MatchedRule ?? string.Empty;
            State.FishingFilterDryRun = false;
        }

        private static void Record(string decision, string skipReason)
        {
            State.LastDecision = decision ?? string.Empty;
            State.LastSkipReason = skipReason ?? string.Empty;
        }

        private static void RecordFishingFilterNotRun(AppSettings settings, string reason)
        {
            State.FishingFilterMode = settings == null ? string.Empty : FishingFilterModes.Normalize(settings.FishingFilterMode);
            State.FishingFilterMatchMode = settings == null ? string.Empty : FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            State.FishingFilterCatchKind = FishingCatchKinds.Unknown;
            State.FishingFilterCatchId = 0;
            State.FishingFilterCatchName = string.Empty;
            State.FishingFilterDecision = "notRun";
            State.FishingFilterDecisionReason = reason ?? string.Empty;
            State.FishingFilterMatchedRule = string.Empty;
            State.FishingFilterDryRun = false;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index].Trim();
                }
            }

            return string.Empty;
        }

        private static void PublishDiagnostics()
        {
            FishingAutomationDiagnostics.Update(new FishingAutomationDiagnosticInfo
            {
                FishingSessionActive = State.SessionActive,
                FishingLastDecision = State.LastDecision,
                FishingLastSkipReason = State.LastSkipReason,
                FishingCurrentBobberIdentity = State.CurrentBobberIdentity,
                FishingLastProcessedHookIdentity = State.LastProcessedHookIdentity,
                FishingWaitingForBobberGone = State.WaitingForBobberGone,
                FishingRecastDelayTicks = State.RecastDelayTicks,
                FishingRecastWaitingForBobber = State.RecastWaitingForBobber,
                FishingRecastBobberWaitTicks = State.RecastBobberWaitTicks,
                FishingRecastRetryCount = State.RecastRetryCount,
                FishingFilterSkipInProgress = State.FilterSkipInProgress,
                FishingFilterSkipRequestId = State.FilterSkipRequestId == Guid.Empty ? string.Empty : State.FilterSkipRequestId.ToString(),
                FishingFilterSkipWaitingForBobberGone = State.FilterSkipWaitingForBobberGone,
                FishingFilterSkipTemporarySlot = State.FilterSkipTemporarySlot,
                FishingFilterSkipLastResult = State.FilterSkipLastResult,
                FishingFilterSkipRestoreFailureReason = State.FilterSkipRestoreFailureReason,
                FishingCastWorldX = State.CastWorldX,
                FishingCastWorldY = State.CastWorldY,
                FishingOriginalLoadoutIndex = FishingLoadoutService.OriginalLoadoutIndex,
                FishingTargetLoadoutIndex = FishingLoadoutService.TargetLoadoutIndex,
                FishingAutoEquipmentApplied = FishingAutoEquipmentService.Applied,
                FishingAutoEquipmentPendingRestoreCount = FishingAutoEquipmentService.PendingRestoreCount,
                FishingAutoEquipmentLastDecision = FishingAutoEquipmentService.LastDecision,
                FishingAutoEquipmentLastSkipReason = FishingAutoEquipmentService.LastSkipReason,
                FishingAutoEquipmentAppliedMoveCount = FishingAutoEquipmentService.AppliedMoveCount,
                FishingAutoEquipmentStillHoldingOriginalRod = FishingAutoEquipmentService.StillHoldingOriginalRod,
                FishingAutoEquipmentManualInventoryInteractionDetected = FishingAutoEquipmentService.ManualInventoryInteractionDetected,
                FishingQuestFishStoreCooldownTicks = FishingQuestFishStorageService.CooldownTicks,
                FishingQuestFishLastItemId = FishingQuestFishStorageService.LastItemId,
                FishingQuestFishLastSlotCount = FishingQuestFishStorageService.LastSlotCount,
                FishingAutoStoreLastMode = FishingQuestFishStorageService.LastMode,
                FishingAutoStoreLastInventorySignature = FishingQuestFishStorageService.LastInventorySignature,
                FishingAutoStoreLastPendingItemIds = FishingQuestFishStorageService.LastPendingItemIds,
                FishingAutoStoreLastDiagnosticMessage = FishingQuestFishStorageService.LastDiagnosticMessage,
                FishingFallbackScanExecutedCount = _fallbackScanExecutedCount,
                FishingFallbackScanSkippedHookFreshCount = _fallbackScanSkippedHookFreshCount,
                FishingFallbackScanForcedDisappearanceConfirmationCount = _fallbackScanForcedDisappearanceConfirmationCount,
                FishingAutomationDispatchReason = _lastDispatchReason,
                FishingAutomationDispatchCadenceTicks = _lastDispatchCadenceTicks,
                FishingAutomationIdleFastSkipCount = _idleFastSkipCount,
                FishingAutomationIdleWatchdogTickCount = _idleWatchdogTickCount,
                FishingObserverFreshActiveCount = _observerFreshActiveCount,
                FishingObserverFreshInactiveSkipCount = _observerFreshInactiveSkipCount,
                FishingFallbackScanIdleSkippedCount = _fallbackScanIdleSkippedCount,
                FishingFallbackScanHookStaleCount = _fallbackScanHookStaleCount,
                FishingTickSubpathLast = _lastTickSubpath,
                FishingResidualStateMask = GetResidualStateMask(),
                FishingFilterMode = State.FishingFilterMode,
                FishingFilterMatchMode = State.FishingFilterMatchMode,
                FishingFilterCatchKind = State.FishingFilterCatchKind,
                FishingFilterCatchId = State.FishingFilterCatchId,
                FishingFilterCatchName = State.FishingFilterCatchName,
                FishingFilterDecision = State.FishingFilterDecision,
                FishingFilterDecisionReason = State.FishingFilterDecisionReason,
                FishingFilterMatchedRule = State.FishingFilterMatchedRule,
                FishingFilterDryRun = State.FishingFilterDryRun,
                FishingFilterCutRodSkipEnabled = (ConfigService.AppSettings ?? AppSettings.CreateDefault()).FishingFilterCutRodSkipEnabled
            });
        }
    }
}
