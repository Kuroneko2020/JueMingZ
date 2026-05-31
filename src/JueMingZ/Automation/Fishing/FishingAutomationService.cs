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
    public static class FishingAutomationService
    {
        private static readonly object SyncRoot = new object();
        private static readonly FishingRuntimeState State = new FishingRuntimeState();
        internal const int SonarBuffType = 122;
        private const int RecastBobberWaitTimeoutTicks = 120;
        private const int MaxRecastRetryCount = 3;
        private const int WaitingForBobberGoneTimeoutTicks = 90;
        private static long _lastFallbackScanTick;
        private static long _lastCompletedActionTick;

        public static FishingAutomationDiagnosticInfo GetDiagnostics()
        {
            return FishingAutomationDiagnostics.GetSnapshot();
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
                settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
                var settings = settingsSnapshot.SourceSettings ?? AppSettings.CreateDefault();
                var autoStoreMode = settingsSnapshot.FishingAutoStoreMode;
                var truffleWormBaitActive = IsCurrentBaitTruffleWorm(settings);
                var equipmentDisabledReason = truffleWormBaitActive ? "currentBaitTruffleWorm" : "disabled";
                var autoLoadoutEnabled = settingsSnapshot.FishingAutoLoadoutEnabled && !truffleWormBaitActive;
                var autoEquipmentEnabled = settingsSnapshot.FishingAutoEquipmentEnabled && !truffleWormBaitActive;
                var anyEnabled = settingsSnapshot.FishingAutoFishEnabled ||
                                 settingsSnapshot.FishingAutoLoadoutEnabled ||
                                 settingsSnapshot.FishingAutoEquipmentEnabled ||
                                 FishingAutoEquipmentService.HasPendingRestore ||
                                 settingsSnapshot.FishingAutoStoreEnabled;
                if (!anyEnabled)
                {
                    EndSession(queue, snapshot, tick, "allDisabled");
                    PublishDiagnostics();
                    return;
                }

                if (!CanRun(snapshot))
                {
                    FishingAutoEquipmentService.Tick(queue, snapshot, autoEquipmentEnabled, false, FishingLiquidKind.Unknown, tick, equipmentDisabledReason);
                    EndSession(queue, snapshot, tick, "notInWorldOrPlayerUnavailable");
                    Record("skipped", "notInWorldOrPlayerUnavailable");
                    PublishDiagnostics();
                    return;
                }

                List<FishingBobberObservation> scanned;
                var scanAvailable = TryRefreshFallbackScan(tick, out scanned);
                FishingBobberObservation observation;
                var hasObservation = TrySelectObservation(scanned, out observation);
                UpdateSessionFromObservation(queue, snapshot, observation, hasObservation, tick);
                var hasFishingEquipmentBobber = State.SessionActive &&
                                                hasObservation &&
                                                observation != null &&
                                                observation.InLiquid;
                var fishingLiquidKind = hasFishingEquipmentBobber ? observation.LiquidKind : FishingLiquidKind.Unknown;
                FishingAutoEquipmentService.Tick(queue, snapshot, autoEquipmentEnabled, hasFishingEquipmentBobber, fishingLiquidKind, tick, equipmentDisabledReason);

                if (State.SessionActive)
                {
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
                        queue.Enqueue(forcedPullRequest);
                        State.PullRequestId = forcedPullRequest.RequestId;
                        if (observation != null)
                        {
                            State.LastProcessedHookIdentity = observation.Identity;
                        }

                        State.FilterSkipWaitingForBobberGone = false;
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
                queue.Enqueue(request);
                if (ShouldRegisterCaughtItemForAutoStore(settings, candidate))
                {
                    FishingQuestFishStorageService.RegisterExpectedCaughtItem(request.RequestId, candidate.Id);
                }

                State.PullRequestId = request.RequestId;
                State.LastProcessedHookIdentity = observation.Identity;
                State.WaitingForBobberGone = true;
                State.WaitingForBobberGoneStartTick = tick;
                State.FilterSkipWaitingForBobberGone = false;
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
            queue.Enqueue(request);
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
            queue.Enqueue(request);
            State.LastProcessedHookIdentity = observation.Identity;
            State.WaitingForBobberGone = true;
            State.WaitingForBobberGoneStartTick = tick;
            State.FilterSkipWaitingForBobberGone = true;
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
            RecordFishingFilterRun(settings, candidate, decision, "filterSkipDeferred", reason);
            Record("filterSkipDeferred", reason ?? string.Empty);
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
                SourceFeatureId = FeatureIds.FishingAutoFish,
                Description = description,
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
                SourceFeatureId = FeatureIds.FishingFilter,
                Description = "Fishing filter skip",
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

        private static bool TryRefreshFallbackScan(long tick, out List<FishingBobberObservation> scanned)
        {
            scanned = null;
            var shouldScan = tick - _lastFallbackScanTick >= 5 ||
                             State.WaitingForBobberGone ||
                             State.RecastDelayTicks > 0 ||
                             State.RecastWaitingForBobber;
            if (!shouldScan)
            {
                return false;
            }

            _lastFallbackScanTick = tick;
            if (!TerrariaFishingCompat.TryScanLocalBobbers(out scanned))
            {
                scanned = null;
                return false;
            }

            FishingBobberObserver.RemoveMissing(scanned);
            return true;
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
