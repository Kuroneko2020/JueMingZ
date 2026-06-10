using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Movement
{
    public static partial class MovementSafeLandingService
    {
        public static void AfterActionQueueUpdate(InputActionQueueFastState queueSnapshot)
        {
            var result = queueSnapshot == null ? null : queueSnapshot.LastResult;
            if (result == null ||
                !string.Equals(result.Scenario, ScenarioNames.MovementSafeLanding, StringComparison.Ordinal))
            {
                return;
            }

            var applyHandled = false;
            var activationHandled = false;
            var restoreHandled = false;
            var mountActivationHandled = false;
            var mountCancelHandled = false;
            var gravityActivationHandled = false;
            var gravityRestoreHandled = false;
            lock (SyncRoot)
            {
                if (result.RequestId == _temporaryEquipmentApplyRequestId)
                {
                    _temporaryEquipmentApplyRequestId = Guid.Empty;
                    applyHandled = true;
                }

                if (result.RequestId == _temporaryEquipmentActivationRequestId)
                {
                    _temporaryEquipmentActivationRequestId = Guid.Empty;
                    _temporaryEquipmentActivationApplied = IsTemporaryEquipmentActivationApplied(result);
                    _temporaryEquipmentLastDecision = _temporaryEquipmentActivationApplied ? "activationApplied" : "activationCompleted";
                    _temporaryEquipmentLastSkipReason = result.Status.ToString() + ":" + (result.ResultCode ?? string.Empty);
                    activationHandled = true;
                }

                if (result.RequestId == _temporaryEquipmentRestoreRequestId)
                {
                    _temporaryEquipmentRestoreRequestId = Guid.Empty;
                    restoreHandled = true;
                }

                if (result.RequestId == _safeLandingMountActivationRequestId)
                {
                    _safeLandingMountActivationRequestId = Guid.Empty;
                    mountActivationHandled = true;
                }

                if (result.RequestId == _safeLandingMountCancelRequestId)
                {
                    _safeLandingMountCancelRequestId = Guid.Empty;
                    mountCancelHandled = true;
                }

                if (result.RequestId == _safeLandingGravityActivationRequestId)
                {
                    _safeLandingGravityActivationRequestId = Guid.Empty;
                    gravityActivationHandled = true;
                }

                if (result.RequestId == _safeLandingGravityRestoreRequestId)
                {
                    _safeLandingGravityRestoreRequestId = Guid.Empty;
                    gravityRestoreHandled = true;
                }
            }

            if (mountActivationHandled && IsSafeLandingActionApplied(result))
            {
                MarkSafeLandingMountCancelPending();
            }

            if (mountCancelHandled)
            {
                CompleteSafeLandingMountCancel(result);
            }

            if (gravityActivationHandled && IsSafeLandingActionApplied(result))
            {
                MarkSafeLandingGravityRestorePending();
            }

            if (gravityRestoreHandled)
            {
                CompleteSafeLandingGravityRestore(result);
            }

            MovementSafeLandingEquipmentActionResult actionResult;
            if (applyHandled && MovementSafeLandingEquipmentCompat.TryTakeApplyResult(result.RequestId, out actionResult))
            {
                TemporaryEquipmentApplyCompleted(actionResult);
                return;
            }

            if (activationHandled)
            {
                return;
            }

            if (restoreHandled && MovementSafeLandingEquipmentCompat.TryTakeRestoreResult(result.RequestId, out actionResult))
            {
                TemporaryEquipmentRestoreCompleted(actionResult);
            }
        }

        private static bool TryHandleDisabledResidualState(
            InputActionQueue queue,
            GameStateSnapshot snapshot,
            long tick,
            AppSettings settings)
        {
            if (!HasSafeLandingResidualState())
            {
                return false;
            }

            var configSummary = MovementSafeLandingOptionCatalog.BuildConfigSummary(settings);
            if (queue == null)
            {
                RecordDecision(false, "disabled", "disabledPendingCleanup:queueUnavailable", tick, null, null, false, configSummary, string.Empty);
                return true;
            }

            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                var reason = snapshot == null || !snapshot.IsInWorld ? "notInWorld" : "inMainMenu";
                RecordDecision(false, "disabled", "disabledPendingCleanup:" + reason, tick, null, null, false, configSummary, string.Empty);
                return true;
            }

            var queueSnapshot = queue.GetFastState();
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                RecordDecision(
                    false,
                    "disabled",
                    "disabledPendingCleanup:localPlayerUnavailable",
                    tick,
                    queueSnapshot,
                    null,
                    false,
                    configSummary,
                    TerrariaInputCompat.LastInputCompatError);
                return true;
            }

            if (HasTemporaryEquipmentRecordsOrInflight())
            {
                HandleTemporaryEquipmentRestore(queue, tick, queueSnapshot, player, null, settings);
                return true;
            }

            if (TryHandlePendingSafeLandingGravityRestore(queue, tick, queueSnapshot, player, null, settings))
            {
                return true;
            }

            if (TryHandlePendingSafeLandingMountCancel(queue, tick, queueSnapshot, player, null, settings))
            {
                return true;
            }

            RecordDecision(false, "disabled", "disabledPendingCleanup:idle", tick, queueSnapshot, null, false, configSummary, string.Empty);
            return true;
        }

        private static bool TryHandlePendingSafeLandingGravityRestore(
            InputActionQueue queue,
            long tick,
            InputActionQueueFastState queueSnapshot,
            object player,
            MovementInputFrameCache.MovementInputFrame inputFrame,
            AppSettings settings)
        {
            bool pending;
            Guid restoreRequestId;
            long activationTick;
            float originalDirection;
            lock (SyncRoot)
            {
                pending = _safeLandingGravityRestorePending;
                restoreRequestId = _safeLandingGravityRestoreRequestId;
                activationTick = _safeLandingGravityActivationTick;
                originalDirection = _safeLandingGravityOriginalDirection;
            }

            if (!pending && restoreRequestId == Guid.Empty)
            {
                return false;
            }

            if (restoreRequestId != Guid.Empty)
            {
                RecordDecision(true, "waiting", "safeLandingGravityRestoreInFlight", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (activationTick >= 0 && tick - activationTick > SafeLandingGravityRestoreGiveUpTicks)
            {
                GiveUpSafeLandingGravityRestore("gravityRestoreGaveUpManualRequired");
                RecordDecision(true, "gravityRestorePending", "gravityRestoreGaveUpManualRequired", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            JumpInputProfile profile;
            string profileError;
            if (!TryReadJumpInputProfile(inputFrame, player, out profile, out profileError) || profile == null)
            {
                ResetSafeLandingGravityRestoreReadiness();
                RecordGravityRestoreState("restoreWaiting", "jumpProfileUnavailable");
                RecordDecision(true, "gravityRestorePending", "jumpProfileUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), profileError);
                return true;
            }

            var currentDirection = profile.GravityDirection >= 0f ? 1f : -1f;
            var targetDirection = originalDirection >= 0f ? 1f : -1f;
            if (Math.Abs(currentDirection - targetDirection) < 0.01f)
            {
                ClearSafeLandingGravityRestore("alreadyOriginalGravity");
                RecordDecision(true, "gravityRestoreCompleted", "alreadyOriginalGravity", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (!profile.PlayerControllable)
            {
                ResetSafeLandingGravityRestoreReadiness();
                RecordGravityRestoreState("restoreWaiting", "playerNotControllable");
                RecordDecision(true, "gravityRestorePending", "playerNotControllable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (!profile.HasGravityGlobe)
            {
                ResetSafeLandingGravityRestoreReadiness();
                RecordGravityRestoreState("restoreWaiting", "gravityGlobeUnavailable");
                RecordDecision(true, "gravityRestorePending", "gravityGlobeUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            var readyReason = "immediateGravityRestore";

            long lastRestoreAttemptTick;
            lock (SyncRoot)
            {
                lastRestoreAttemptTick = _lastSafeLandingGravityRestoreAttemptTick;
            }

            if (tick - lastRestoreAttemptTick < SafeLandingGravityRestoreRetryTicks)
            {
                RecordGravityRestoreState("restoreWaiting", "safeLandingGravityRestoreCooldown");
                RecordDecision(true, "gravityRestorePending", "safeLandingGravityRestoreCooldown", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (queue == null)
            {
                RecordGravityRestoreState("restoreWaiting", "queueUnavailable");
                RecordDecision(true, "gravityRestorePending", "queueUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            var request = MovementSafeLandingRequestBuilder.BuildRecoveryJumpRequest(
                StrategyGravityRestore,
                MovementSafeLandingActionTypes.GravityFlip,
                "1",
                "gravityRestoreAfterLanding",
                targetDirection,
                "Movement safe landing restores gravity direction after Gravity Globe rescue");

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordGravityRestoreState("restoreWaiting", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason));
                RecordDecision(true, "gravityRestorePending", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            lock (SyncRoot)
            {
                _safeLandingGravityRestoreRequestId = request.RequestId;
                _lastSafeLandingGravityRestoreAttemptTick = tick;
                _safeLandingGravityLastDecision = "submittedRestore";
                _safeLandingGravityLastSkipReason = readyReason ?? string.Empty;
            }

            RecordDecision(true, "submittedGravityRestore", readyReason, tick, queueSnapshot, null, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
            return true;
        }

        private static bool TryHandlePendingSafeLandingMountCancel(
            InputActionQueue queue,
            long tick,
            InputActionQueueFastState queueSnapshot,
            object player,
            MovementInputFrameCache.MovementInputFrame inputFrame,
            AppSettings settings)
        {
            bool pending;
            Guid cancelRequestId;
            lock (SyncRoot)
            {
                pending = _safeLandingMountCancelPending;
                cancelRequestId = _safeLandingMountCancelRequestId;
            }

            if (!pending && cancelRequestId == Guid.Empty)
            {
                return false;
            }

            if (cancelRequestId != Guid.Empty)
            {
                RecordDecision(true, "waiting", "safeLandingMountCancelInFlight", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            JumpInputProfile profile;
            string profileError;
            if (!TryReadJumpInputProfile(inputFrame, player, out profile, out profileError) || profile == null)
            {
                ResetSafeLandingMountCancelReadiness();
                RecordDecision(true, "mountCancelPending", "jumpProfileUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), profileError);
                return true;
            }

            string clearReason;
            if (ShouldClearSafeLandingMountCancel(profile, out clearReason))
            {
                ClearSafeLandingMountCancel(clearReason);
                RecordDecision(true, "mountCancelCompleted", clearReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (!profile.PlayerControllable)
            {
                ResetSafeLandingMountCancelReadiness();
                RecordDecision(true, "mountCancelPending", "playerNotControllable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            bool safeToCancel;
            bool requiresStableWait;
            string cancelReason;
            if (!TryResolveSafeLandingMountCancelReadiness(player, profile, out safeToCancel, out requiresStableWait, out cancelReason))
            {
                ResetSafeLandingMountCancelReadiness();
                RecordDecision(true, "mountCancelPending", cancelReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (!safeToCancel)
            {
                ResetSafeLandingMountCancelReadiness();
                RecordDecision(true, "mountCancelPending", cancelReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            var readyReason = cancelReason;
            if (requiresStableWait)
            {
                long readySince;
                lock (SyncRoot)
                {
                    if (_safeLandingMountCancelReadySinceTick < 0 ||
                        !string.Equals(_safeLandingMountCancelReadyReason, cancelReason ?? string.Empty, StringComparison.Ordinal))
                    {
                        _safeLandingMountCancelReadySinceTick = tick;
                        _safeLandingMountCancelReadyReason = cancelReason ?? string.Empty;
                    }

                    readySince = _safeLandingMountCancelReadySinceTick;
                    readyReason = _safeLandingMountCancelReadyReason;
                }

                if (readySince >= 0 && tick - readySince < TemporaryEquipmentStableRestoreTicks)
                {
                    RecordDecision(true, "mountCancelPending", "waitingForStableMountCancel:" + readyReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return true;
                }
            }
            else
            {
                ResetSafeLandingMountCancelReadiness();
            }

            long lastCancelAttemptTick;
            lock (SyncRoot)
            {
                lastCancelAttemptTick = _lastSafeLandingMountCancelAttemptTick;
            }

            if (tick - lastCancelAttemptTick < SafeLandingMountCancelRetryTicks)
            {
                RecordDecision(true, "mountCancelPending", "safeLandingMountCancelCooldown", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (queue == null)
            {
                RecordDecision(true, "mountCancelPending", "queueUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            var request = MovementSafeLandingRequestBuilder.BuildRecoveryJumpRequest(
                StrategyMountCancel,
                MovementSafeLandingActionTypes.QuickMount,
                "1",
                "mountCancelAfterLanding",
                1f,
                "Movement safe landing cancels flying mount after landing");

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision(true, "mountCancelPending", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            lock (SyncRoot)
            {
                _safeLandingMountCancelRequestId = request.RequestId;
                _lastSafeLandingMountCancelAttemptTick = tick;
            }

            RecordDecision(true, "submittedMountCancel", readyReason, tick, queueSnapshot, null, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
            return true;
        }

        private static void HandleTemporaryEquipmentRestore(
            InputActionQueue queue,
            long tick,
            InputActionQueueFastState queueSnapshot,
            object player,
            MovementInputFrameCache.MovementInputFrame inputFrame,
            AppSettings settings)
        {
            // Apply, activate, and restore are one state machine. Do not submit a
            // new rescue while any leg still owns recorded equipment state.
            if (HasTemporaryEquipmentApplyInflight() || HasTemporaryEquipmentActivationInflight() || HasTemporaryEquipmentRestoreInflight())
            {
                var waitingReason = HasTemporaryEquipmentRestoreInflight()
                    ? "temporaryRestoreInFlight"
                    : HasTemporaryEquipmentActivationInflight()
                        ? "temporaryActivationInFlight"
                        : "temporaryApplyInFlight";
                RecordDecision(true, "waiting", waitingReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return;
            }

            if (TryHandleTemporaryEquipmentActivation(queue, tick, queueSnapshot, player, inputFrame, settings))
            {
                return;
            }

            var records = GetTemporaryEquipmentRecordsCopy();
            long activeSince;
            lock (SyncRoot)
            {
                activeSince = _temporaryEquipmentActiveSinceTick;
            }

            if (activeSince >= 0 && tick - activeSince < TemporaryEquipmentMinRestoreTicks)
            {
                RecordDecision(true, "temporaryEquipmentActive", "waitingForMinimumRestoreDelay", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return;
            }

            bool safeToRestore;
            string restoreReason;
            if (!MovementSafeLandingEquipmentCompat.TryIsSafeToRestoreTemporaryEquipment(player, out safeToRestore, out restoreReason))
            {
                ResetTemporaryEquipmentRestoreReadiness();
                RecordDecision(true, "temporaryEquipmentActive", restoreReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return;
            }

            if (!safeToRestore)
            {
                ResetTemporaryEquipmentRestoreReadiness();
                RecordDecision(true, "temporaryEquipmentActive", restoreReason, tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return;
            }

            long restoreReadySince;
            string restoreReadyReason;
            lock (SyncRoot)
            {
                if (_temporaryEquipmentRestoreReadySinceTick < 0 ||
                    !string.Equals(_temporaryEquipmentRestoreReadyReason, restoreReason ?? string.Empty, StringComparison.Ordinal))
                {
                    _temporaryEquipmentRestoreReadySinceTick = tick;
                    _temporaryEquipmentRestoreReadyReason = restoreReason ?? string.Empty;
                }

                restoreReadySince = _temporaryEquipmentRestoreReadySinceTick;
                restoreReadyReason = _temporaryEquipmentRestoreReadyReason;
            }

            var stableRestoreTicks = ResolveTemporaryEquipmentStableRestoreTicks(records);
            if (restoreReadySince >= 0 && tick - restoreReadySince < stableRestoreTicks)
            {
                RecordDecision(
                    true,
                    "temporaryEquipmentActive",
                    "waitingForStableRestore:" + restoreReadyReason,
                    tick,
                    queueSnapshot,
                    null,
                    false,
                    MovementSafeLandingOptionCatalog.BuildConfigSummary(settings),
                    string.Empty);
                return;
            }

            TryEnqueueTemporaryEquipmentRestore(queue, tick, restoreReason);
            RecordDecision(true, "submittedRestore", restoreReason, tick, queueSnapshot, null, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
        }

        private static int ResolveTemporaryEquipmentStableRestoreTicks(IList<MovementSafeLandingEquipmentMoveRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                return TemporaryEquipmentStableRestoreTicks;
            }

            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record == null ||
                    !string.Equals(record.EquipmentCategory, "umbrella", StringComparison.OrdinalIgnoreCase))
                {
                    return TemporaryEquipmentStableRestoreTicks;
                }
            }

            return TemporaryUmbrellaStableRestoreTicks;
        }

        private static bool TryHandleTemporaryEquipmentActivation(
            InputActionQueue queue,
            long tick,
            InputActionQueueFastState queueSnapshot,
            object player,
            MovementInputFrameCache.MovementInputFrame inputFrame,
            AppSettings settings)
        {
            var records = GetTemporaryEquipmentRecordsCopy();
            if (records.Count == 0)
            {
                return false;
            }
            var activationRecord = FindTemporaryEquipmentActivationRecord(records);

            long activeSince;
            long lastActivationAttempt;
            int activationAttemptCount;
            bool activationApplied;
            lock (SyncRoot)
            {
                activeSince = _temporaryEquipmentActiveSinceTick;
                lastActivationAttempt = _lastTemporaryEquipmentActivationAttemptTick;
                activationAttemptCount = _temporaryEquipmentActivationAttemptCount;
                activationApplied = _temporaryEquipmentActivationApplied;
            }

            if (activeSince >= 0 && tick - activeSince < TemporaryEquipmentActivationSettleTicks)
            {
                RecordDecision(true, "temporaryEquipmentActive", "waitingForEquipmentRefresh", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (activationApplied)
            {
                return false;
            }

            MovementSafeLandingAnalysis analysis;
            if (!MovementSafeLandingCompat.TryAnalyze(player, settings, inputFrame, out analysis) || analysis == null)
            {
                RecordDecision(true, "temporaryEquipmentActive", "activationAnalysisFailed", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), MovementSafeLandingCompat.LastError);
                return false;
            }

            if (!analysis.Dangerous || analysis.AlreadySafe)
            {
                return false;
            }

            var selectedStrategyId = analysis.SelectedStrategyId ?? string.Empty;
            var selectedActionType = analysis.SelectedActionType ?? string.Empty;
            var selectedPriority = analysis.SelectedPriority;
            var activationSource = string.Empty;
            var activationExtra = string.Empty;

            if (activationRecord != null)
            {
                string temporaryActivationWindowReason;
                var temporaryActivationWindow = MovementSafeLandingTiming.IsTemporaryActivationWindowReady(
                    activationRecord,
                    analysis,
                    out temporaryActivationWindowReason);
                if (!temporaryActivationWindow)
                {
                    RecordDecision(true, "temporaryEquipmentActive", "waitingForTemporaryActivationWindow:" + activationRecord.StrategyId + ":" + temporaryActivationWindowReason, tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return true;
                }

                selectedStrategyId = activationRecord.StrategyId ?? string.Empty;
                selectedActionType = activationRecord.ActionType ?? string.Empty;
                selectedPriority = activationRecord.SelectedPriority;
                activationSource = "temporaryEquipmentRecord";
                activationExtra = "temporaryEquipmentRecordActivation";
            }
            else if (string.IsNullOrWhiteSpace(selectedStrategyId))
            {
                RecordDecision(true, "temporaryEquipmentActive", FirstNonEmpty(analysis.SkipReason, "waitingForEquippedCapability"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), MovementSafeLandingCompat.LastError);
                return true;
            }

            if (activationRecord == null && !analysis.RescueWindow)
            {
                RecordDecision(true, "temporaryEquipmentActive", FirstNonEmpty(analysis.SkipReason, "waitingForActivationWindow:" + selectedStrategyId), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), MovementSafeLandingCompat.LastError);
                return true;
            }

            if (!IsImplementedActivationPriority(selectedPriority, activationRecord) ||
                string.IsNullOrWhiteSpace(selectedActionType))
            {
                RecordDecision(true, "temporaryEquipmentActive", FirstNonEmpty(analysis.SkipReason, "temporaryActivationStageNotImplemented"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (activationRecord != null)
            {
                string capabilityReason;
                if (!IsTemporaryEquipmentActivationCapabilityAvailable(activationRecord, analysis, out capabilityReason))
                {
                    var waitReason = "waitingForTemporaryActivationCapability:" +
                                     FirstNonEmpty(activationRecord.StrategyId, activationRecord.EquipmentCategory) +
                                     ":" + capabilityReason;
                    RecordTemporaryEquipmentState("waitingForActivationCapability", waitReason);
                    RecordDecision(true, "temporaryEquipmentActive", waitReason, tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return true;
                }
            }

            if (activationAttemptCount > 0 && tick - lastActivationAttempt < TemporaryEquipmentActivationRetryTicks)
            {
                RecordDecision(true, "temporaryEquipmentActive", "temporaryActivationRetryCooldown", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            if (queue == null)
            {
                RecordDecision(true, "temporaryEquipmentActive", "queueUnavailable", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return true;
            }

            analysis.SelectedStrategyId = selectedStrategyId;
            analysis.SelectedActionType = selectedActionType;
            analysis.SelectedPriority = selectedPriority;
            var request = MovementSafeLandingRequestBuilder.BuildTemporaryEquipmentActivationRequest(
                activationRecord,
                analysis,
                records.Count,
                activationSource);

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision(true, "temporaryEquipmentActive", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), activationExtra);
                return true;
            }

            TrackSafeLandingGravityActivationRequest(request.RequestId, selectedActionType, analysis.GravityDirection);
            lock (SyncRoot)
            {
                _temporaryEquipmentActivationRequestId = request.RequestId;
                if (string.Equals(selectedActionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
                {
                    _safeLandingMountActivationRequestId = request.RequestId;
                }

                _lastTemporaryEquipmentActivationAttemptTick = tick;
                _temporaryEquipmentActivationAttemptCount++;
                _temporaryEquipmentLastDecision = "submittedActivation";
                _temporaryEquipmentLastSkipReason = string.Empty;
            }

            MarkCooldown(tick);
            MarkDescentRescueSubmitted(tick, analysis, selectedPriority, selectedStrategyId, selectedActionType);
            RecordDecision(true, "submittedActivation", string.Empty, tick, queueSnapshot, analysis, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), FirstNonEmpty(activationExtra, "temporaryEquipmentActivation"));
            return true;
        }

        private static void TryEnqueueTemporaryEquipmentApply(
            InputActionQueue queue,
            MovementSafeLandingEquipmentPlan plan,
            MovementSafeLandingAnalysis analysis,
            long tick,
            InputActionQueueFastState queueSnapshot,
            AppSettings settings,
            string planMessage)
        {
            if (queue == null || plan == null)
            {
                RecordDecision(true, "skipped", "temporaryEquipmentPlanUnavailable", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), planMessage);
                return;
            }

            if (tick - _lastTemporaryEquipmentApplyAttemptTick < 10)
            {
                RecordDecision(true, "skipped", "temporaryEquipmentApplyCooldown", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                return;
            }

            var request = MovementSafeLandingRequestBuilder.BuildTemporaryEquipmentApplyRequest(plan, analysis);

            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision(true, "skipped", "temporaryEquipmentApplyAdmissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), planMessage);
                return;
            }

            MovementSafeLandingEquipmentCompat.RegisterApplyPlan(request.RequestId, plan);
            lock (SyncRoot)
            {
                _temporaryEquipmentApplyRequestId = request.RequestId;
                _lastTemporaryEquipmentApplyAttemptTick = tick;
                _temporaryEquipmentLastDecision = "submittedApply";
                _temporaryEquipmentLastSkipReason = string.Empty;
                _temporaryEquipmentSelectedCategory = plan.EquipmentCategory ?? string.Empty;
                _temporaryEquipmentSelectedSourceKind = MovementSafeLandingEquipmentCompat.ContainerKindName(plan.SourceContainerKind);
                _temporaryEquipmentSelectedSourceSlot = plan.SourceSlot;
                _temporaryEquipmentSelectedTargetKind = MovementSafeLandingEquipmentCompat.ContainerKindName(plan.TargetContainerKind);
                _temporaryEquipmentSelectedTargetSlot = plan.TargetSlot;
                _temporaryEquipmentSelectedItemType = plan.CandidateItemType;
                _temporaryEquipmentSelectedMountType = plan.CandidateMountType;
            }

            MarkDescentRescueSubmitted(tick, analysis, plan.SelectedPriority, plan.StrategyId, plan.ActionType);
            RecordDecision(true, "submitted", string.Empty, tick, queueSnapshot, analysis, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), planMessage);
        }

        private static void TryEnqueueTemporaryEquipmentRestore(InputActionQueue queue, long tick, string reason)
        {
            if (queue == null)
            {
                RecordTemporaryEquipmentState("restoreSkipped", "queueUnavailable");
                return;
            }

            if (tick - _lastTemporaryEquipmentRestoreAttemptTick < 30)
            {
                RecordTemporaryEquipmentState("restoreSkipped", "restoreCooldown");
                return;
            }

            var records = GetTemporaryEquipmentRecordsCopy();
            if (records.Count == 0)
            {
                ClearTemporaryEquipmentRecords();
                return;
            }

            var request = MovementSafeLandingRequestBuilder.BuildTemporaryEquipmentRestoreRequest(records, reason);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordTemporaryEquipmentState("restoreSkipped", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason));
                return;
            }

            MovementSafeLandingEquipmentCompat.RegisterRestoreRequest(request.RequestId, records, reason);
            lock (SyncRoot)
            {
                _temporaryEquipmentRestoreRequestId = request.RequestId;
                _lastTemporaryEquipmentRestoreAttemptTick = tick;
                _temporaryEquipmentLastDecision = "submittedRestore";
                _temporaryEquipmentLastSkipReason = reason ?? string.Empty;
            }
        }

        private static bool HasTemporaryEquipmentRecordsOrInflight()
        {
            lock (SyncRoot)
            {
                return TemporaryEquipmentRecords.Count > 0 ||
                       _temporaryEquipmentApplyRequestId != Guid.Empty ||
                       _temporaryEquipmentActivationRequestId != Guid.Empty ||
                       _temporaryEquipmentRestoreRequestId != Guid.Empty;
            }
        }

        private static bool HasSafeLandingResidualState()
        {
            lock (SyncRoot)
            {
                return TemporaryEquipmentRecords.Count > 0 ||
                       _temporaryEquipmentApplyRequestId != Guid.Empty ||
                       _temporaryEquipmentActivationRequestId != Guid.Empty ||
                       _temporaryEquipmentRestoreRequestId != Guid.Empty ||
                       _safeLandingMountCancelPending ||
                       _safeLandingMountCancelRequestId != Guid.Empty ||
                       _safeLandingGravityRestorePending ||
                       _safeLandingGravityRestoreRequestId != Guid.Empty;
            }
        }

        private static bool HasActiveSafeLandingJumpPulse()
        {
            SafeLandingJumpPulseSnapshot pulse;
            return MovementSafeLandingCompat.TryGetAnySafeLandingJumpPulseSnapshot(out pulse) &&
                   pulse != null &&
                   pulse.Active;
        }

        private static bool HasTemporaryEquipmentApplyInflight()
        {
            lock (SyncRoot)
            {
                return _temporaryEquipmentApplyRequestId != Guid.Empty;
            }
        }

        private static bool HasTemporaryEquipmentRestoreInflight()
        {
            lock (SyncRoot)
            {
                return _temporaryEquipmentRestoreRequestId != Guid.Empty;
            }
        }

        private static bool HasTemporaryEquipmentActivationInflight()
        {
            lock (SyncRoot)
            {
                return _temporaryEquipmentActivationRequestId != Guid.Empty;
            }
        }

        private static List<MovementSafeLandingEquipmentMoveRecord> GetTemporaryEquipmentRecordsCopy()
        {
            lock (SyncRoot)
            {
                return new List<MovementSafeLandingEquipmentMoveRecord>(TemporaryEquipmentRecords);
            }
        }

        private static void TemporaryEquipmentApplyCompleted(MovementSafeLandingEquipmentActionResult result)
        {
            lock (SyncRoot)
            {
                TemporaryEquipmentRecords.Clear();
                if (result != null && result.Records != null)
                {
                    TemporaryEquipmentRecords.AddRange(result.Records);
                }

                _temporaryEquipmentActiveSinceTick = TemporaryEquipmentRecords.Count == 0
                    ? -1
                    : JueMingZRuntime.State == null ? _lastTemporaryEquipmentApplyAttemptTick : JueMingZRuntime.State.UpdateCount;
                _temporaryEquipmentActivationRequestId = Guid.Empty;
                _lastTemporaryEquipmentActivationAttemptTick = 0;
                _temporaryEquipmentActivationAttemptCount = 0;
                _temporaryEquipmentActivationApplied = false;
                _temporaryEquipmentRestoreReadySinceTick = -1;
                _temporaryEquipmentRestoreReadyReason = string.Empty;
                _temporaryEquipmentRestoreNoSpaceCount = 0;
                _temporaryEquipmentLastDecision = result == null ? "applyCompleted" : result.Decision;
                _temporaryEquipmentLastSkipReason = result == null ? string.Empty : result.SkipReason;
                _temporaryEquipmentPostApplyVerificationSummary = result == null ? string.Empty : result.PostApplyVerificationSummary ?? string.Empty;
                if (result != null)
                {
                    _temporaryEquipmentSelectedCategory = result.EquipmentCategory ?? _temporaryEquipmentSelectedCategory;
                }
            }
        }

        private static void TemporaryEquipmentRestoreCompleted(MovementSafeLandingEquipmentActionResult result)
        {
            lock (SyncRoot)
            {
                // Restore completion may still return pending records; keep them as
                // debt so blocked or failed restores cannot masquerade as success.
                TemporaryEquipmentRecords.Clear();
                if (result != null && result.Records != null)
                {
                    TemporaryEquipmentRecords.AddRange(result.Records);
                }

                _temporaryEquipmentRestoreNoSpaceCount = result == null ? 0 : result.PendingRestoreNoSpaceCount;
                _temporaryEquipmentLastDecision = result == null ? "restoreCompleted" : result.Decision;
                _temporaryEquipmentLastSkipReason = result == null ? string.Empty : result.SkipReason;
                if (TemporaryEquipmentRecords.Count == 0)
                {
                    _temporaryEquipmentActiveSinceTick = -1;
                    _temporaryEquipmentActivationRequestId = Guid.Empty;
                    _lastTemporaryEquipmentActivationAttemptTick = 0;
                    _temporaryEquipmentActivationAttemptCount = 0;
                    _temporaryEquipmentActivationApplied = false;
                    _temporaryEquipmentRestoreReadySinceTick = -1;
                    _temporaryEquipmentRestoreReadyReason = string.Empty;
                    _temporaryEquipmentSelectedCategory = string.Empty;
                    _temporaryEquipmentSelectedSourceKind = string.Empty;
                    _temporaryEquipmentSelectedTargetKind = string.Empty;
                    _temporaryEquipmentSelectedSourceSlot = -1;
                    _temporaryEquipmentSelectedTargetSlot = -1;
                    _temporaryEquipmentSelectedItemType = 0;
                    _temporaryEquipmentSelectedMountType = -1;
                    _temporaryEquipmentRestoreNoSpaceCount = 0;
                    _temporaryEquipmentPostApplyVerificationSummary = string.Empty;
                }
            }
        }

        private static void ClearTemporaryEquipmentRecords()
        {
            lock (SyncRoot)
            {
                TemporaryEquipmentRecords.Clear();
                _temporaryEquipmentApplyRequestId = Guid.Empty;
                _temporaryEquipmentActivationRequestId = Guid.Empty;
                _temporaryEquipmentRestoreRequestId = Guid.Empty;
                _temporaryEquipmentActiveSinceTick = -1;
                _temporaryEquipmentRestoreReadySinceTick = -1;
                _temporaryEquipmentRestoreReadyReason = string.Empty;
                _lastTemporaryEquipmentActivationAttemptTick = 0;
                _temporaryEquipmentActivationAttemptCount = 0;
                _temporaryEquipmentActivationApplied = false;
                _temporaryEquipmentRestoreNoSpaceCount = 0;
                _temporaryEquipmentSelectedCategory = string.Empty;
                _temporaryEquipmentSelectedSourceKind = string.Empty;
                _temporaryEquipmentSelectedTargetKind = string.Empty;
                _temporaryEquipmentSelectedSourceSlot = -1;
                _temporaryEquipmentSelectedTargetSlot = -1;
                _temporaryEquipmentSelectedItemType = 0;
                _temporaryEquipmentSelectedMountType = -1;
                _temporaryEquipmentPostApplyVerificationSummary = string.Empty;
            }
        }

        private static void ResetTemporaryEquipmentRestoreReadiness()
        {
            lock (SyncRoot)
            {
                _temporaryEquipmentRestoreReadySinceTick = -1;
                _temporaryEquipmentRestoreReadyReason = string.Empty;
            }
        }

        private static void TrackSafeLandingMountActivationRequest(Guid requestId, string actionType)
        {
            if (requestId == Guid.Empty ||
                !string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lock (SyncRoot)
            {
                _safeLandingMountActivationRequestId = requestId;
            }
        }

        private static void TrackSafeLandingGravityActivationRequest(Guid requestId, string actionType, float originalGravityDirection)
        {
            if (requestId == Guid.Empty || !IsGravityFlipAction(actionType))
            {
                return;
            }

            lock (SyncRoot)
            {
                _safeLandingGravityActivationRequestId = requestId;
                _safeLandingGravityOriginalDirection = originalGravityDirection >= 0f ? 1f : -1f;
                _safeLandingGravityActivationTick = JueMingZRuntime.State == null ? -1 : JueMingZRuntime.State.UpdateCount;
                _safeLandingGravityLastDecision = "submittedActivation";
                _safeLandingGravityLastSkipReason = string.Empty;
            }
        }

        private static void MarkSafeLandingMountCancelPending()
        {
            lock (SyncRoot)
            {
                _safeLandingMountCancelPending = true;
                _safeLandingMountCancelReadySinceTick = -1;
                _safeLandingMountCancelReadyReason = string.Empty;
            }
        }

        private static void MarkSafeLandingGravityRestorePending()
        {
            lock (SyncRoot)
            {
                _safeLandingGravityRestorePending = true;
                if (_safeLandingGravityActivationTick < 0 && JueMingZRuntime.State != null)
                {
                    _safeLandingGravityActivationTick = JueMingZRuntime.State.UpdateCount;
                }

                _safeLandingGravityLastDecision = "restorePending";
                _safeLandingGravityLastSkipReason = string.Empty;
            }
        }

        private static void CompleteSafeLandingMountCancel(InputActionResult result)
        {
            var actionApplied = IsSafeLandingActionApplied(result);
            var mountStillActive = false;
            if (actionApplied)
            {
                object player;
                JumpInputProfile profile;
                mountStillActive = TerrariaInputCompat.TryGetLocalPlayer(out player) &&
                                   TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) &&
                                   profile != null &&
                                   profile.MountActive;
            }

            lock (SyncRoot)
            {
                if (actionApplied)
                {
                    _safeLandingMountCancelPending = mountStillActive;
                }

                _safeLandingMountCancelReadySinceTick = -1;
                _safeLandingMountCancelReadyReason = string.Empty;
            }
        }

        internal static bool ShouldClearSafeLandingMountCancelForTesting(JumpInputProfile profile, out string reason)
        {
            return ShouldClearSafeLandingMountCancel(profile, out reason);
        }

        private static bool ShouldClearSafeLandingMountCancel(JumpInputProfile profile, out string reason)
        {
            reason = string.Empty;
            if (profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.MountActive)
            {
                reason = "mountAlreadyInactive";
                return true;
            }

            return false;
        }

        internal static bool TryEvaluateSafeLandingMountCancelImminentForTesting(
            JumpInputProfile profile,
            bool impactProbeAvailable,
            int impactDistancePixels,
            float impactTicks,
            out bool safeToCancel,
            out bool requiresStableWait,
            out string reason)
        {
            safeToCancel = false;
            requiresStableWait = true;
            var ok = TryEvaluateSafeLandingMountCancelImminent(profile, impactProbeAvailable, impactDistancePixels, impactTicks, out safeToCancel, out reason);
            requiresStableWait = !safeToCancel;
            return ok;
        }

        private static bool TryResolveSafeLandingMountCancelReadiness(
            object player,
            JumpInputProfile profile,
            out bool safeToCancel,
            out bool requiresStableWait,
            out string reason)
        {
            safeToCancel = false;
            requiresStableWait = true;
            reason = string.Empty;
            if (profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.MountActive)
            {
                safeToCancel = true;
                requiresStableWait = false;
                reason = "mountAlreadyInactive";
                return true;
            }

            if (!profile.PlayerControllable)
            {
                reason = "playerNotControllable";
                return true;
            }

            int impactDistancePixels;
            float impactTicks;
            string impactReason;
            var impactProbeAvailable = MovementSafeLandingCompat.TryResolveLandingImpactDistanceForProfile(
                player,
                profile,
                out impactDistancePixels,
                out impactTicks,
                out impactReason);

            bool imminentCancelReady;
            string imminentReason;
            if (TryEvaluateSafeLandingMountCancelImminent(
                    profile,
                    impactProbeAvailable,
                    impactDistancePixels,
                    impactTicks,
                    out imminentCancelReady,
                    out imminentReason) &&
                imminentCancelReady)
            {
                safeToCancel = true;
                requiresStableWait = false;
                reason = imminentReason;
                return true;
            }

            bool groundedSafeToCancel;
            string groundedReason;
            if (!MovementSafeLandingEquipmentCompat.TryIsSafeToRestoreTemporaryEquipment(player, out groundedSafeToCancel, out groundedReason))
            {
                reason = string.IsNullOrWhiteSpace(groundedReason) ? impactReason : groundedReason;
                return false;
            }

            safeToCancel = groundedSafeToCancel;
            requiresStableWait = groundedSafeToCancel;
            if (groundedSafeToCancel)
            {
                reason = groundedReason;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(imminentReason) &&
                string.Equals(groundedReason, "stillFalling", StringComparison.Ordinal))
            {
                reason = imminentReason;
            }
            else
            {
                reason = string.IsNullOrWhiteSpace(groundedReason) ? imminentReason : groundedReason;
            }

            return true;
        }

        private static bool TryEvaluateSafeLandingMountCancelImminent(
            JumpInputProfile profile,
            bool impactProbeAvailable,
            int impactDistancePixels,
            float impactTicks,
            out bool safeToCancel,
            out string reason)
        {
            safeToCancel = false;
            reason = string.Empty;
            if (profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.MountActive)
            {
                safeToCancel = true;
                reason = "mountAlreadyInactive";
                return true;
            }

            if (!profile.PlayerControllable)
            {
                reason = "playerNotControllable";
                return true;
            }

            var gravityDirection = Math.Abs(profile.GravityDirection) > 0.001f ? profile.GravityDirection : 1f;
            var fallingSpeed = profile.VelocityY * gravityDirection;
            if (fallingSpeed <= SafeLandingMountCancelImminentMinFallingSpeed)
            {
                reason = "mountCancelImminentWindowNotRequired";
                return true;
            }

            if (!impactProbeAvailable)
            {
                reason = "mountCancelImpactProbeUnavailable";
                return true;
            }

            if (impactDistancePixels < 0 || impactTicks < 0f || float.IsNaN(impactTicks) || float.IsInfinity(impactTicks))
            {
                reason = "waitingForImminentLandingCollision:noImpact";
                return true;
            }

            var distanceGate = ResolveSafeLandingMountCancelImminentImpactDistance(fallingSpeed);
            if (impactDistancePixels <= distanceGate || impactTicks <= SafeLandingMountCancelImminentImpactMaxTicks)
            {
                safeToCancel = true;
                reason = "imminentLandingCollision:distance=" +
                         impactDistancePixels.ToString(CultureInfo.InvariantCulture) +
                         ",ticks=" +
                         impactTicks.ToString("0.###", CultureInfo.InvariantCulture);
                return true;
            }

            reason = "waitingForImminentLandingCollision:distance=" +
                     impactDistancePixels.ToString(CultureInfo.InvariantCulture) +
                     ",ticks=" +
                     impactTicks.ToString("0.###", CultureInfo.InvariantCulture) +
                     ",distanceGate=" +
                     distanceGate.ToString(CultureInfo.InvariantCulture) +
                     ",ticksGate=" +
                     SafeLandingMountCancelImminentImpactMaxTicks.ToString("0.###", CultureInfo.InvariantCulture);
            return true;
        }

        private static int ResolveSafeLandingMountCancelImminentImpactDistance(float fallingSpeed)
        {
            var speed = Math.Max(0f, fallingSpeed);
            var speedScaled = (int)Math.Ceiling(speed * 4f);
            if (speedScaled < SafeLandingMountCancelImminentImpactMinPixels)
            {
                return SafeLandingMountCancelImminentImpactMinPixels;
            }

            return speedScaled > SafeLandingMountCancelImminentImpactMaxPixels
                ? SafeLandingMountCancelImminentImpactMaxPixels
                : speedScaled;
        }

        private static void CompleteSafeLandingGravityRestore(InputActionResult result)
        {
            var actionApplied = IsSafeLandingActionApplied(result);
            var restoredToOriginal = false;
            var restoreReason = result == null ? string.Empty : result.ResultCode ?? string.Empty;
            if (actionApplied)
            {
                // A queued gravity flip is only accepted when the observed gravity
                // direction returns to the recorded original direction.
                float originalDirection;
                lock (SyncRoot)
                {
                    originalDirection = _safeLandingGravityOriginalDirection >= 0f ? 1f : -1f;
                }

                object player;
                JumpInputProfile profile;
                if (TerrariaInputCompat.TryGetLocalPlayer(out player) &&
                    TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) &&
                    profile != null)
                {
                    var currentDirection = profile.GravityDirection >= 0f ? 1f : -1f;
                    restoredToOriginal = Math.Abs(currentDirection - originalDirection) < 0.01f;
                    restoreReason = restoredToOriginal
                        ? restoreReason
                        : "gravityDirectionStillInverted:" + currentDirection.ToString("0.###", CultureInfo.InvariantCulture);
                }
                else
                {
                    restoreReason = "gravityRestoreProfileUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                }
            }

            lock (SyncRoot)
            {
                if (actionApplied && restoredToOriginal)
                {
                    _safeLandingGravityRestorePending = false;
                    _safeLandingGravityLastDecision = "restoreCompleted";
                    _safeLandingGravityLastSkipReason = restoreReason;
                }
                else if (actionApplied)
                {
                    _safeLandingGravityRestorePending = true;
                    _safeLandingGravityLastDecision = "restoreStillPending";
                    _safeLandingGravityLastSkipReason = restoreReason;
                }
                else
                {
                    _safeLandingGravityLastDecision = "restoreCompletedUnverified";
                    _safeLandingGravityLastSkipReason = result == null ? string.Empty : result.Status.ToString() + ":" + (result.ResultCode ?? string.Empty);
                }

            }
        }

        private static void ResetSafeLandingMountCancelReadiness()
        {
            lock (SyncRoot)
            {
                _safeLandingMountCancelReadySinceTick = -1;
                _safeLandingMountCancelReadyReason = string.Empty;
            }
        }

        private static void ClearSafeLandingMountCancel(string reason)
        {
            lock (SyncRoot)
            {
                _safeLandingMountCancelPending = false;
                _safeLandingMountCancelRequestId = Guid.Empty;
                _safeLandingMountCancelReadySinceTick = -1;
                _safeLandingMountCancelReadyReason = reason ?? string.Empty;
            }
        }

        private static void ResetSafeLandingGravityRestoreReadiness()
        {
        }

        private static void ClearSafeLandingGravityRestore(string reason)
        {
            lock (SyncRoot)
            {
                _safeLandingGravityRestorePending = false;
                _safeLandingGravityRestoreRequestId = Guid.Empty;
                _safeLandingGravityLastDecision = "restoreCleared";
                _safeLandingGravityLastSkipReason = reason ?? string.Empty;
            }
        }

        private static void GiveUpSafeLandingGravityRestore(string reason)
        {
            lock (SyncRoot)
            {
                _safeLandingGravityRestorePending = false;
                _safeLandingGravityRestoreRequestId = Guid.Empty;
                _safeLandingGravityLastDecision = "restoreGaveUp";
                _safeLandingGravityLastSkipReason = reason ?? string.Empty;
            }
        }

        private static void RecordGravityRestoreState(string decision, string skipReason)
        {
            lock (SyncRoot)
            {
                _safeLandingGravityLastDecision = decision ?? string.Empty;
                _safeLandingGravityLastSkipReason = skipReason ?? string.Empty;
            }
        }

        private static bool IsGravityFlipAction(string actionType)
        {
            return string.Equals(actionType, "gravity_flip", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionType, "gravityFlip", StringComparison.OrdinalIgnoreCase);
        }

        private static void RecordTemporaryEquipmentState(string decision, string skipReason)
        {
            lock (SyncRoot)
            {
                _temporaryEquipmentLastDecision = decision ?? string.Empty;
                _temporaryEquipmentLastSkipReason = skipReason ?? string.Empty;
            }
        }

        private static bool TryReadJumpInputProfile(
            MovementInputFrameCache.MovementInputFrame inputFrame,
            object player,
            out JumpInputProfile profile,
            out string failureReason)
        {
            profile = null;
            failureReason = string.Empty;
            if (inputFrame != null && inputFrame.TryGetJumpProfile(out profile, out failureReason))
            {
                return true;
            }

            if (TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) && profile != null)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = FirstNonEmpty(TerrariaInputCompat.LastInputCompatError, failureReason);
            return false;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        }

        private static bool IsTemporaryEquipmentActivationApplied(InputActionResult result)
        {
            if (result == null)
            {
                return false;
            }

            return result.Status == InputActionStatus.Succeeded ||
                   result.Status == InputActionStatus.AttemptedButUnverified;
        }

        private static bool IsSafeLandingActionApplied(InputActionResult result)
        {
            if (result == null)
            {
                return false;
            }

            return result.Status == InputActionStatus.Succeeded ||
                   result.Status == InputActionStatus.AttemptedButUnverified;
        }
    }
}
