using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Movement
{
    public static partial class MovementSafeLandingService
    {
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

        private static void RecordTemporaryEquipmentState(string decision, string skipReason)
        {
            lock (SyncRoot)
            {
                _temporaryEquipmentLastDecision = decision ?? string.Empty;
                _temporaryEquipmentLastSkipReason = skipReason ?? string.Empty;
            }
        }
    }
}
