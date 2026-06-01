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
    public static class MovementSafeLandingService
    {
        private const int RescueCooldownTicks = 45;
        private const int ActiveRescueCooldownTicks = 4;
        private const int TeleportRodRescueCooldownTicks = 12;
        private const int TemporaryEquipmentActivationSettleTicks = 3;
        private const int TemporaryEquipmentActivationRetryTicks = 15;
        private const int TemporaryEquipmentMinRestoreTicks = 6;
        private const int TemporaryEquipmentStableRestoreTicks = 18;
        private const int TemporaryUmbrellaStableRestoreTicks = 3;
        private const int SafeLandingMountCancelRetryTicks = 6;
        private const float SafeLandingMountCancelImminentImpactMaxTicks = 4f;
        private const float SafeLandingMountCancelImminentMinFallingSpeed = 1.25f;
        private const int SafeLandingMountCancelImminentImpactMinPixels = 48;
        private const int SafeLandingMountCancelImminentImpactMaxPixels = 128;
        private const int SafeLandingGravityRestoreRetryTicks = 3;
        private const int SafeLandingGravityRestoreGiveUpTicks = 600;
        private const int DescentRescueGuardMaxTicks = 180;
        private const float DescentRescueGuardLandingChangePixels = 128f;
        private const float DescentRescueGuardHorizontalChangePixels = 192f;
        private const string StrategyMountCancel = "safe_landing_mount_cancel";
        private const string StrategyGravityRestore = "safe_landing_gravity_restore";
        private static readonly object SyncRoot = new object();
        private static readonly object DiagnosticsSyncRoot = new object();
        private static long _nextAllowedRescueTick;
        private static Guid _temporaryEquipmentApplyRequestId = Guid.Empty;
        private static Guid _temporaryEquipmentActivationRequestId = Guid.Empty;
        private static Guid _temporaryEquipmentRestoreRequestId = Guid.Empty;
        private static long _lastTemporaryEquipmentApplyAttemptTick;
        private static long _lastTemporaryEquipmentActivationAttemptTick;
        private static long _lastTemporaryEquipmentRestoreAttemptTick;
        private static long _temporaryEquipmentActiveSinceTick = -1;
        private static long _temporaryEquipmentRestoreReadySinceTick = -1;
        private static string _temporaryEquipmentRestoreReadyReason = string.Empty;
        private static Guid _safeLandingMountActivationRequestId = Guid.Empty;
        private static Guid _safeLandingMountCancelRequestId = Guid.Empty;
        private static bool _safeLandingMountCancelPending;
        private static long _safeLandingMountCancelReadySinceTick = -1;
        private static string _safeLandingMountCancelReadyReason = string.Empty;
        private static long _lastSafeLandingMountCancelAttemptTick;
        private static Guid _safeLandingGravityActivationRequestId = Guid.Empty;
        private static Guid _safeLandingGravityRestoreRequestId = Guid.Empty;
        private static bool _safeLandingGravityRestorePending;
        private static float _safeLandingGravityOriginalDirection = 1f;
        private static long _safeLandingGravityActivationTick = -1;
        private static long _lastSafeLandingGravityRestoreAttemptTick;
        private static string _safeLandingGravityLastDecision = string.Empty;
        private static string _safeLandingGravityLastSkipReason = string.Empty;
        private static int _temporaryEquipmentActivationAttemptCount;
        private static bool _temporaryEquipmentActivationApplied;
        private static int _temporaryEquipmentRestoreNoSpaceCount;
        private static readonly List<MovementSafeLandingEquipmentMoveRecord> TemporaryEquipmentRecords = new List<MovementSafeLandingEquipmentMoveRecord>();
        private static string _temporaryEquipmentLastDecision = string.Empty;
        private static string _temporaryEquipmentLastSkipReason = string.Empty;
        private static string _temporaryEquipmentPostApplyVerificationSummary = string.Empty;
        private static string _temporaryEquipmentSelectedCategory = string.Empty;
        private static string _temporaryEquipmentSelectedSourceKind = string.Empty;
        private static string _temporaryEquipmentSelectedTargetKind = string.Empty;
        private static int _temporaryEquipmentSelectedSourceSlot = -1;
        private static int _temporaryEquipmentSelectedTargetSlot = -1;
        private static int _temporaryEquipmentSelectedItemType;
        private static int _temporaryEquipmentSelectedMountType = -1;
        private static bool _descentRescueGuardActive;
        private static long _descentRescueGuardStartedTick = -1;
        private static string _descentRescueGuardStrategyId = string.Empty;
        private static string _descentRescueGuardActionType = string.Empty;
        private static int _descentRescueGuardPriority = -1;
        private static bool _descentRescueGuardLandingKnown;
        private static float _descentRescueGuardImpactWorldX;
        private static float _descentRescueGuardImpactWorldY;
        private static float _descentRescueGuardPositionX;
        private static long _fullAnalysisCount;
        private static long _cheapPrecheckSkipCount;
        private static MovementSafeLandingDiagnosticInfo _diagnostics = new MovementSafeLandingDiagnosticInfo();

        public static MovementSafeLandingDiagnosticInfo GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        internal static bool RequiresRuntimeTickWhenDisabled()
        {
            return HasSafeLandingResidualState();
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            Tick(queue, snapshot, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var settings = settingsSnapshot.SourceSettings ?? AppSettings.CreateDefault();
            var enabled = settingsSnapshot.MovementSafeLandingEnabled;
            try
            {
                if (!enabled)
                {
                    MovementSafeLandingCompat.CancelSafeLandingJumpPulse(Guid.Empty, "movement.fall_protection disabled");
                    if (!TryHandleDisabledResidualState(queue, snapshot, tick, settings))
                    {
                        RecordDecision(false, "disabled", "disabled", tick, null, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    }

                    ResetCooldown();
                    return;
                }

                if (queue == null)
                {
                    RecordDecision(true, "skipped", "queueUnavailable", tick, null, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return;
                }

                if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
                {
                    MovementSafeLandingCompat.InvalidateCollisionFastPathCaches("notInWorld");
                    ResetCooldown();
                    RecordDecision(true, "skipped", "notInWorld", tick, null, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return;
                }

                if (LegacyTextInput.IsAnyFocused)
                {
                    var textAnalysis = new MovementSafeLandingAnalysis
                    {
                        TextInputFocused = true,
                        TextInputReason = "legacyUi",
                        SkipReason = "textInput:legacyUi"
                    };
                    RecordDecision(true, "skipped", "textInput:legacyUi", tick, null, textAnalysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return;
                }

                bool textFocused;
                string textReason;
                TerrariaInputCompat.TryReadTextInputFocus(out textFocused, out textReason);
                if (textFocused)
                {
                    var textAnalysis = new MovementSafeLandingAnalysis
                    {
                        TextInputFocused = true,
                        TextInputReason = textReason,
                        SkipReason = "textInput:" + textReason
                    };
                    RecordDecision(true, "skipped", textAnalysis.SkipReason, tick, null, textAnalysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return;
                }

                var queueSnapshot = queue.GetFastState();
                if (!string.IsNullOrWhiteSpace(queueSnapshot.RunningActionKind) ||
                    ItemUseBridge.PendingRequestId != Guid.Empty)
                {
                    RecordDecision(true, "skipped", "queueBusy", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return;
                }

                var inputFrame = MovementInputFrameCache.GetOrCreate(runtimeState, settingsSnapshot);
                object player;
                if (inputFrame == null || !inputFrame.TryGetPlayer(out player))
                {
                    if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                    {
                        RecordDecision(true, "skipped", "localPlayerUnavailable", tick, queueSnapshot, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), TerrariaInputCompat.LastInputCompatError);
                        return;
                    }
                }

                if (TryHandlePendingSafeLandingGravityRestore(queue, tick, queueSnapshot, player, inputFrame, settings))
                {
                    return;
                }

                if (HasTemporaryEquipmentRecordsOrInflight())
                {
                    HandleTemporaryEquipmentRestore(queue, tick, queueSnapshot, player, inputFrame, settings);
                    return;
                }

                if (TryHandlePendingSafeLandingMountCancel(queue, tick, queueSnapshot, player, inputFrame, settings))
                {
                    return;
                }

                MovementSafeLandingAnalysis analysis;
                bool shouldRunFullAnalysis;
                if (!HasActiveSafeLandingJumpPulse() &&
                    MovementSafeLandingCompat.TryCheapDangerPrecheck(player, inputFrame, out analysis, out shouldRunFullAnalysis) &&
                    !shouldRunFullAnalysis)
                {
                    IncrementCheapPrecheckSkipCount();
                    ClearDescentRescueGuard("notDangerous");
                    RecordDecision(
                        true,
                        "skipped",
                        FirstNonEmpty(analysis.SkipReason, "notDangerous:cheap"),
                        tick,
                        queueSnapshot,
                        analysis,
                        false,
                        MovementSafeLandingOptionCatalog.BuildConfigSummary(settings),
                        string.Empty);
                    return;
                }

                IncrementFullAnalysisCount();
                if (!MovementSafeLandingCompat.TryAnalyze(player, settings, inputFrame, out analysis) || analysis == null)
                {
                    RecordDecision(true, "skipped", "analysisFailed", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), MovementSafeLandingCompat.LastError);
                    return;
                }

                var strategyContext = MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis);
                var selection = MovementSafeLandingStrategyCatalog.Evaluate(strategyContext);
                if (!analysis.Dangerous || analysis.AlreadySafe)
                {
                    ClearDescentRescueGuard(analysis.AlreadySafe ? "alreadySafe" : "notDangerous");
                    RecordDecision(true, "skipped", FirstNonEmpty(analysis.SkipReason, analysis.AlreadySafe ? "alreadySafe:" + (analysis.SafeReason ?? string.Empty) : "notDangerous"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), MovementSafeLandingCompat.LastError);
                    return;
                }

                string repeatRescueReason;
                if (TrySuppressRepeatedDescentRescue(analysis, tick, out repeatRescueReason))
                {
                    RecordDecision(true, "skipped", repeatRescueReason, tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return;
                }

                if (selection != null &&
                    selection.SelectedEvaluation != null &&
                    selection.SelectedEvaluation.Priority == 1)
                {
                    if (!selection.SelectedEvaluation.IsReady)
                    {
                        RecordDecision(true, "skipped", FirstNonEmpty(selection.SelectedEvaluation.SkipReason, "waitingForRescueWindow"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), MovementSafeLandingCompat.LastError);
                        return;
                    }

                    if (tick < GetNextAllowedRescueTick())
                    {
                        RecordDecision(true, "skipped", "cooldown", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

                    if (!IsImplementedPriority(analysis.SelectedPriority) || string.IsNullOrWhiteSpace(analysis.SelectedStrategyId) || string.IsNullOrWhiteSpace(analysis.SelectedActionType))
                    {
                        RecordDecision(true, "skipped", FirstNonEmpty(analysis.SkipReason, "stageNotImplemented"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

                    var activeRequest = MovementSafeLandingRequestBuilder.BuildJumpRequest(
                        selection.SelectedPlan,
                        analysis,
                        "Movement safe landing uses a vanilla control input",
                        InputActionPriority.High);
                    queue.Enqueue(activeRequest);
                    TrackSafeLandingMountActivationRequest(activeRequest.RequestId, analysis.SelectedActionType);
                    TrackSafeLandingGravityActivationRequest(activeRequest.RequestId, analysis.SelectedActionType, analysis.GravityDirection);
                    MarkCooldown(tick, selection.SelectedEvaluation);
                    MarkDescentRescueSubmitted(tick, analysis, selection.SelectedEvaluation);
                    RecordDecision(true, "submitted", string.Empty, tick, queueSnapshot, analysis, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return;
                }

                MovementSafeLandingEquipmentPlan temporaryPlan;
                string temporaryMessage;
                if (MovementSafeLandingEquipmentCompat.TryBuildTemporaryEquipmentPlan(player, settings, analysis, out temporaryPlan, out temporaryMessage) &&
                    temporaryPlan != null)
                {
                    strategyContext.TemporaryEquipmentPlan = temporaryPlan;
                    strategyContext.TemporaryEquipmentPlanMessage = temporaryMessage ?? string.Empty;
                    selection = MovementSafeLandingStrategyCatalog.Evaluate(strategyContext);
                    if (selection == null || selection.SelectedEvaluation == null || selection.SelectedPlan == null)
                    {
                        RecordDecision(true, "skipped", FirstNonEmpty(temporaryMessage, "noImplementedSafeLandingStrategy"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

                    if (selection.SelectedEvaluation.Priority >= 2 && selection.SelectedEvaluation.Priority <= 3)
                    {
                        if (!selection.SelectedEvaluation.IsReady)
                        {
                            RecordDecision(true, "skipped", FirstNonEmpty(selection.SelectedEvaluation.SkipReason, "waitingForRescueWindow"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), temporaryMessage);
                            return;
                        }

                        if (tick < GetNextAllowedRescueTick())
                        {
                            RecordDecision(true, "skipped", "cooldown", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                            return;
                        }

                        TryEnqueueTemporaryEquipmentApply(queue, selection.SelectedPlan.EquipmentPlan ?? temporaryPlan, analysis, tick, queueSnapshot, settings, temporaryMessage);
                        return;
                    }
                }

                if (selection != null &&
                    selection.SelectedEvaluation != null &&
                    selection.SelectedEvaluation.Priority == 4)
                {
                    if (!selection.SelectedEvaluation.IsReady)
                    {
                        RecordDecision(true, "skipped", FirstNonEmpty(selection.SelectedEvaluation.SkipReason, "waitingForRescueWindow"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), MovementSafeLandingCompat.LastError);
                        return;
                    }

                    if (tick < GetNextAllowedRescueTick())
                    {
                        RecordDecision(true, "skipped", "cooldown", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

                    if (!IsImplementedPriority(analysis.SelectedPriority) || string.IsNullOrWhiteSpace(analysis.SelectedStrategyId) || string.IsNullOrWhiteSpace(analysis.SelectedActionType))
                    {
                        RecordDecision(true, "skipped", FirstNonEmpty(analysis.SkipReason, "stageNotImplemented"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

                    var grappleRequest = MovementSafeLandingRequestBuilder.BuildJumpRequest(
                        selection.SelectedPlan,
                        analysis,
                        "Movement safe landing uses vanilla quick grapple input",
                        InputActionPriority.High);
                    queue.Enqueue(grappleRequest);
                    MarkCooldown(tick, selection.SelectedEvaluation);
                    MarkDescentRescueSubmitted(tick, analysis, selection.SelectedEvaluation);
                    RecordDecision(true, "submitted", string.Empty, tick, queueSnapshot, analysis, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return;
                }

                if (selection != null &&
                    selection.SelectedEvaluation != null &&
                    selection.SelectedEvaluation.Priority == 5)
                {
                    if (!selection.SelectedEvaluation.IsReady)
                    {
                        RecordDecision(true, "skipped", FirstNonEmpty(selection.SelectedEvaluation.SkipReason, "waitingForRescueWindow"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), MovementSafeLandingCompat.LastError);
                        return;
                    }

                    if (tick < GetNextAllowedRescueTick())
                    {
                        RecordDecision(true, "skipped", "cooldown", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

                    if (!IsImplementedPriority(analysis.SelectedPriority) ||
                        string.IsNullOrWhiteSpace(analysis.SelectedStrategyId) ||
                        string.IsNullOrWhiteSpace(analysis.SelectedActionType) ||
                        !analysis.HasTeleportTarget)
                    {
                        RecordDecision(true, "skipped", FirstNonEmpty(analysis.SkipReason, "stageNotImplemented"), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

                    if (!TerrariaInputCompat.IsSupportedItemUseSlot(ResolveTeleportRodUseSlot(analysis)))
                    {
                        RecordDecision(true, "skipped", "teleportRodUseSlotUnavailable", tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

                    var teleportRodRequest = MovementSafeLandingRequestBuilder.BuildTeleportRodRequest(
                        selection.SelectedPlan,
                        analysis);
                    queue.Enqueue(teleportRodRequest);
                    MarkCooldown(tick, selection.SelectedEvaluation);
                    MarkDescentRescueSubmitted(tick, analysis, selection.SelectedEvaluation);
                    RecordDecision(true, "submitted", string.Empty, tick, queueSnapshot, analysis, true, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                    return;
                }

                RecordDecision(true, "skipped", string.IsNullOrWhiteSpace(temporaryMessage) ? "noImplementedSafeLandingStrategy" : temporaryMessage, tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
            }
            catch (Exception error)
            {
                RecordDecision(enabled, "exception", "exception:" + error.GetType().Name, tick, null, null, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), error.Message);
                RuntimeDiagnostics.RecordError("MovementSafeLandingService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "movement-safe-landing-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "MovementSafeLandingService",
                    "Movement safe landing tick failed; exception swallowed.", error);
            }
        }

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

            queue.Enqueue(request);
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

            queue.Enqueue(request);
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

            queue.Enqueue(request);
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

            MovementSafeLandingEquipmentCompat.RegisterApplyPlan(request.RequestId, plan);
            queue.Enqueue(request);
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
            MovementSafeLandingEquipmentCompat.RegisterRestoreRequest(request.RequestId, records, reason);
            queue.Enqueue(request);
            lock (SyncRoot)
            {
                _temporaryEquipmentRestoreRequestId = request.RequestId;
                _lastTemporaryEquipmentRestoreAttemptTick = tick;
                _temporaryEquipmentLastDecision = "submittedRestore";
                _temporaryEquipmentLastSkipReason = reason ?? string.Empty;
            }
        }

        private static int ResolveTeleportRodUseSlot(MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null)
            {
                return -1;
            }

            return analysis.TeleportRodInventorySlot;
        }

        private static bool IsImplementedPriority(int priority)
        {
            return priority == 1 || priority == 4 || priority == 5;
        }

        private static bool IsImplementedActivationPriority(int priority, MovementSafeLandingEquipmentMoveRecord activationRecord)
        {
            if (activationRecord != null)
            {
                return IsTemporaryEquipmentInputAction(activationRecord.ActionType);
            }

            return IsImplementedPriority(priority);
        }

        private static MovementSafeLandingEquipmentMoveRecord FindTemporaryEquipmentActivationRecord(IList<MovementSafeLandingEquipmentMoveRecord> records)
        {
            if (records == null)
            {
                return null;
            }

            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record == null)
                {
                    continue;
                }

                if (IsTemporaryEquipmentInputAction(record.ActionType))
                {
                    return record;
                }
            }

            return null;
        }

        private static bool IsTemporaryEquipmentInputAction(string actionType)
        {
            return string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase) ||
                   IsGravityFlipAction(actionType);
        }

        internal static bool IsTemporaryEquipmentActivationCapabilityAvailable(
            MovementSafeLandingEquipmentMoveRecord record,
            MovementSafeLandingAnalysis analysis,
            out string reason)
        {
            reason = string.Empty;
            if (record == null)
            {
                reason = "activationRecordUnavailable";
                return false;
            }

            if (analysis == null)
            {
                reason = "activationAnalysisUnavailable";
                return false;
            }

            if (!analysis.PlayerControllable)
            {
                reason = "playerNotControllable";
                return false;
            }

            var category = record.EquipmentCategory ?? string.Empty;
            var strategy = record.StrategyId ?? string.Empty;
            var actionType = record.ActionType ?? string.Empty;

            if (string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(category, "double_jump", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_double_jump", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(
                        analysis.HasAirJump || HasPostApplyCapabilityEvidence(record, "doubleJumpAvailableAfterRefresh"),
                        "airJumpUnavailable",
                        out reason);
                }

                if (string.Equals(category, "rocket_boots", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_rocket_boots", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(
                        analysis.HasRocketJump ||
                        analysis.HasRocketBootsAvailable ||
                        (analysis.AerialJumpWindow && HasPostApplyCapabilityEvidence(record, "rocketBootsAvailable")),
                        "rocketBootsUnavailable",
                        out reason);
                }

                if (string.Equals(category, "flying_carpet", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_flying_carpet", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(
                        analysis.AerialJumpWindow &&
                        (analysis.HasFlyingCarpetAvailable ||
                         analysis.HasFlyingCarpet ||
                         analysis.FlyingCarpetTime > 0 ||
                         HasPostApplyCapabilityEvidence(record, "flyingCarpetAvailableAfterApply")),
                        "flyingCarpetUnavailable",
                        out reason);
                }

                reason = "unknownTemporaryJumpCapability";
                return false;
            }

            if (string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(category, "flying_mount", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_flying_mount", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(analysis.HasEquippedFlyingMount, "flyingMountUnavailable", out reason);
                }

                if (string.Equals(category, "safe_mount", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(strategy, "temporary_safe_mount", StringComparison.OrdinalIgnoreCase))
                {
                    return CapabilityAvailable(analysis.HasEquippedSafeMount, "safeMountUnavailable", out reason);
                }

                reason = "unknownTemporaryMountCapability";
                return false;
            }

            if (IsGravityFlipAction(actionType) ||
                string.Equals(category, "gravity_globe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_gravity_globe", StringComparison.OrdinalIgnoreCase))
            {
                return CapabilityAvailable(
                    analysis.HasGravityFlipOpportunity ||
                    (analysis.AerialJumpWindow &&
                     (analysis.HasGravityGlobe ||
                      HasPostApplyCapabilityEvidence(record, "gravityRestorePendingExpected"))),
                    "gravityFlipUnavailable",
                    out reason);
            }

            if (string.Equals(actionType, MovementSafeLandingActionTypes.TeleportRod, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category, MovementSafeLandingOptionCatalog.TeleportRod, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, MovementSafeLandingStrategyIds.InventoryTeleportRod, StringComparison.OrdinalIgnoreCase))
            {
                if (!analysis.HasTeleportRod)
                {
                    reason = "teleportRodUnavailable";
                    return false;
                }

                if (!TerrariaInputCompat.IsSupportedItemUseSlot(ResolveTeleportRodUseSlot(analysis)))
                {
                    reason = "teleportRodUseSlotUnavailable";
                    return false;
                }

                if (!analysis.HasTeleportTarget)
                {
                    reason = "teleportTargetUnavailable";
                    return false;
                }

                return true;
            }

            if (string.Equals(actionType, "equip_only", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            reason = "unknownTemporaryActivationAction";
            return false;
        }

        private static bool CapabilityAvailable(bool available, string unavailableReason, out string reason)
        {
            reason = available ? string.Empty : unavailableReason ?? "capabilityUnavailable";
            return available;
        }

        private static bool HasPostApplyCapabilityEvidence(MovementSafeLandingEquipmentMoveRecord record, string expectedReason)
        {
            return record != null &&
                   record.PostApplyCapabilityObserved &&
                   !string.IsNullOrWhiteSpace(expectedReason) &&
                   string.Equals(record.PostApplyVerificationReason, expectedReason, StringComparison.OrdinalIgnoreCase);
        }

        private static long GetNextAllowedRescueTick()
        {
            lock (SyncRoot)
            {
                return _nextAllowedRescueTick;
            }
        }

        private static void MarkCooldown(long tick)
        {
            lock (SyncRoot)
            {
                _nextAllowedRescueTick = tick + RescueCooldownTicks;
            }
        }

        private static void MarkCooldown(long tick, MovementSafeLandingStrategyEvaluation evaluation)
        {
            lock (SyncRoot)
            {
                _nextAllowedRescueTick = tick + ResolveRescueCooldownTicks(evaluation);
            }
        }

        private static int ResolveRescueCooldownTicks(MovementSafeLandingStrategyEvaluation evaluation)
        {
            if (evaluation == null)
            {
                return RescueCooldownTicks;
            }

            if (evaluation.Priority == 1 || evaluation.Priority == 4)
            {
                return ActiveRescueCooldownTicks;
            }

            if (evaluation.Priority == 5)
            {
                return TeleportRodRescueCooldownTicks;
            }

            return RescueCooldownTicks;
        }

        private static void ResetCooldown()
        {
            lock (SyncRoot)
            {
                _nextAllowedRescueTick = 0;
                ClearDescentRescueGuardLocked();
            }
        }

        internal static void ResetDescentRescueGuardForTesting()
        {
            lock (SyncRoot)
            {
                ClearDescentRescueGuardLocked();
            }
        }

        internal static void MarkDescentRescueSubmittedForTesting(
            long tick,
            MovementSafeLandingAnalysis analysis,
            int priority,
            string strategyId,
            string actionType)
        {
            MarkDescentRescueSubmitted(tick, analysis, priority, strategyId, actionType);
        }

        internal static bool TrySuppressRepeatedDescentRescueForTesting(
            MovementSafeLandingAnalysis analysis,
            long tick,
            out string reason)
        {
            return TrySuppressRepeatedDescentRescue(analysis, tick, out reason);
        }

        internal static bool IsSafeLandingTeleportRodRequestStaleForTesting(
            MovementSafeLandingAnalysis analysis,
            IDictionary<string, string> metadata,
            out string reason)
        {
            return IsSafeLandingTeleportRodRequestStale(analysis, metadata, out reason);
        }

        internal static bool IsSafeLandingTeleportRodRequestStale(
            object player,
            IDictionary<string, string> metadata,
            out string reason)
        {
            reason = string.Empty;
            if (!IsSafeLandingTeleportRodRequestMetadata(metadata))
            {
                return false;
            }

            if (player == null)
            {
                reason = "safeLandingTeleportStale:playerUnavailable";
                return true;
            }

            var settingsSnapshot = RuntimeSettingsSnapshotProvider.GetCurrent();
            var settings = settingsSnapshot == null || settingsSnapshot.SourceSettings == null
                ? AppSettings.CreateDefault()
                : settingsSnapshot.SourceSettings;
            MovementSafeLandingAnalysis analysis;
            if (!MovementSafeLandingCompat.TryAnalyze(player, settings, out analysis) || analysis == null)
            {
                reason = "safeLandingTeleportStale:analysisFailed:" + (MovementSafeLandingCompat.LastError ?? string.Empty);
                return true;
            }

            return IsSafeLandingTeleportRodRequestStale(analysis, metadata, out reason);
        }

        private static void MarkDescentRescueSubmitted(long tick, MovementSafeLandingAnalysis analysis, MovementSafeLandingStrategyEvaluation evaluation)
        {
            if (evaluation == null)
            {
                return;
            }

            MarkDescentRescueSubmitted(tick, analysis, evaluation.Priority, evaluation.StrategyId, evaluation.ActionType);
        }

        private static void MarkDescentRescueSubmitted(
            long tick,
            MovementSafeLandingAnalysis analysis,
            int priority,
            string strategyId,
            string actionType)
        {
            if (priority < 1 || priority > 5)
            {
                return;
            }

            lock (SyncRoot)
            {
                _descentRescueGuardActive = true;
                _descentRescueGuardStartedTick = tick;
                _descentRescueGuardStrategyId = strategyId ?? string.Empty;
                _descentRescueGuardActionType = actionType ?? string.Empty;
                _descentRescueGuardPriority = priority;
                _descentRescueGuardLandingKnown = analysis != null && analysis.ImpactFound;
                _descentRescueGuardImpactWorldX = analysis == null ? 0f : analysis.ImpactWorldX;
                _descentRescueGuardImpactWorldY = analysis == null ? 0f : analysis.ImpactWorldY;
                _descentRescueGuardPositionX = analysis == null ? 0f : analysis.PositionX;
            }
        }

        private static bool IsSafeLandingTeleportRodRequestStale(
            MovementSafeLandingAnalysis analysis,
            IDictionary<string, string> metadata,
            out string reason)
        {
            reason = string.Empty;
            if (!IsSafeLandingTeleportRodRequestMetadata(metadata))
            {
                return false;
            }

            if (analysis == null)
            {
                reason = "safeLandingTeleportStale:analysisUnavailable";
                return true;
            }

            if (analysis.AlreadySafe)
            {
                reason = "safeLandingTeleportStale:alreadySafe:" + (analysis.SafeReason ?? string.Empty);
                return true;
            }

            if (!analysis.Dangerous)
            {
                reason = "safeLandingTeleportStale:notDangerous:" + (analysis.SkipReason ?? string.Empty);
                return true;
            }

            if (!analysis.ImpactFound)
            {
                reason = "safeLandingTeleportStale:impactUnavailable";
                return true;
            }

            if (TryMetadataDeltaExceeds(
                    metadata,
                    "SafeLandingImpactWorldX",
                    "SafeLandingImpactWorldY",
                    analysis.ImpactWorldX,
                    analysis.ImpactWorldY,
                    DescentRescueGuardLandingChangePixels,
                    "landingChanged",
                    out reason))
            {
                return true;
            }

            if (analysis.HasTeleportTarget &&
                TryMetadataDeltaExceeds(
                    metadata,
                    "SafeLandingTeleportTargetWorldX",
                    "SafeLandingTeleportTargetWorldY",
                    analysis.TeleportTargetWorldX,
                    analysis.TeleportTargetWorldY,
                    DescentRescueGuardLandingChangePixels,
                    "teleportTargetChanged",
                    out reason))
            {
                return true;
            }

            float originalPositionX;
            if (TryGetMetadataFloat(metadata, "SafeLandingPlayerPositionX", out originalPositionX) &&
                Math.Abs(analysis.PositionX - originalPositionX) > DescentRescueGuardHorizontalChangePixels)
            {
                reason = "safeLandingTeleportStale:playerMovedHorizontally:" +
                         FormatDelta(Math.Abs(analysis.PositionX - originalPositionX));
                return true;
            }

            return false;
        }

        private static bool TrySuppressRepeatedDescentRescue(MovementSafeLandingAnalysis analysis, long tick, out string reason)
        {
            reason = string.Empty;
            lock (SyncRoot)
            {
                if (!_descentRescueGuardActive)
                {
                    return false;
                }

                string clearReason;
                if (ShouldClearDescentRescueGuardLocked(analysis, tick, out clearReason))
                {
                    ClearDescentRescueGuardLocked();
                    return false;
                }

                reason = "sameDescentRescueAlreadySubmitted:" +
                         _descentRescueGuardPriority.ToString(CultureInfo.InvariantCulture) +
                         ":" +
                         (_descentRescueGuardStrategyId ?? string.Empty) +
                         ":" +
                         (_descentRescueGuardActionType ?? string.Empty);
                return true;
            }
        }

        private static void ClearDescentRescueGuard(string reason)
        {
            lock (SyncRoot)
            {
                ClearDescentRescueGuardLocked();
            }
        }

        private static bool ShouldClearDescentRescueGuardLocked(MovementSafeLandingAnalysis analysis, long tick, out string reason)
        {
            reason = string.Empty;
            if (!_descentRescueGuardActive)
            {
                reason = "inactive";
                return true;
            }

            if (analysis == null)
            {
                reason = "analysisUnavailable";
                return true;
            }

            if (!analysis.Dangerous)
            {
                reason = "notDangerous";
                return true;
            }

            if (analysis.AlreadySafe)
            {
                reason = "alreadySafe:" + (analysis.SafeReason ?? string.Empty);
                return true;
            }

            if (TryResolveDescentLandingChangedLocked(analysis, out reason))
            {
                return true;
            }

            if (_descentRescueGuardStartedTick >= 0 &&
                tick - _descentRescueGuardStartedTick > DescentRescueGuardMaxTicks)
            {
                reason = "guardTimeout";
                return true;
            }

            return false;
        }

        private static bool TryResolveDescentLandingChangedLocked(MovementSafeLandingAnalysis analysis, out string reason)
        {
            reason = string.Empty;
            if (!_descentRescueGuardLandingKnown || analysis == null)
            {
                return false;
            }

            if (!analysis.ImpactFound)
            {
                reason = "landingChanged:impactUnavailable";
                return true;
            }

            var impactDeltaX = Math.Abs(analysis.ImpactWorldX - _descentRescueGuardImpactWorldX);
            var impactDeltaY = Math.Abs(analysis.ImpactWorldY - _descentRescueGuardImpactWorldY);
            if (impactDeltaX > DescentRescueGuardLandingChangePixels ||
                impactDeltaY > DescentRescueGuardLandingChangePixels)
            {
                reason = "landingChanged:impactDelta:" +
                         FormatDelta(impactDeltaX) +
                         "," +
                         FormatDelta(impactDeltaY);
                return true;
            }

            var positionDeltaX = Math.Abs(analysis.PositionX - _descentRescueGuardPositionX);
            if (positionDeltaX > DescentRescueGuardHorizontalChangePixels &&
                impactDeltaX > 32f)
            {
                reason = "landingChanged:playerMovedHorizontally:" + FormatDelta(positionDeltaX);
                return true;
            }

            return false;
        }

        private static void ClearDescentRescueGuardLocked()
        {
            _descentRescueGuardActive = false;
            _descentRescueGuardStartedTick = -1;
            _descentRescueGuardStrategyId = string.Empty;
            _descentRescueGuardActionType = string.Empty;
            _descentRescueGuardPriority = -1;
            _descentRescueGuardLandingKnown = false;
            _descentRescueGuardImpactWorldX = 0f;
            _descentRescueGuardImpactWorldY = 0f;
            _descentRescueGuardPositionX = 0f;
        }

        private static bool IsSafeLandingTeleportRodRequestMetadata(IDictionary<string, string> metadata)
        {
            if (metadata == null)
            {
                return false;
            }

            return string.Equals(GetMetadata(metadata, ActionMetadataKeys.Scenario), ScenarioNames.MovementSafeLanding, StringComparison.Ordinal) &&
                   (string.Equals(GetMetadata(metadata, "SafeLandingRescueMode"), "TeleportRod", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(GetMetadata(metadata, "SafeLandingActionType"), MovementSafeLandingActionTypes.TeleportRod, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryMetadataDeltaExceeds(
            IDictionary<string, string> metadata,
            string xKey,
            string yKey,
            float currentX,
            float currentY,
            float threshold,
            string reasonKind,
            out string reason)
        {
            reason = string.Empty;
            float originalX;
            float originalY;
            if (!TryGetMetadataFloat(metadata, xKey, out originalX) ||
                !TryGetMetadataFloat(metadata, yKey, out originalY))
            {
                return false;
            }

            var deltaX = Math.Abs(currentX - originalX);
            var deltaY = Math.Abs(currentY - originalY);
            if (deltaX <= threshold && deltaY <= threshold)
            {
                return false;
            }

            reason = "safeLandingTeleportStale:" +
                     reasonKind +
                     ":" +
                     FormatDelta(deltaX) +
                     "," +
                     FormatDelta(deltaY);
            return true;
        }

        private static bool TryGetMetadataFloat(IDictionary<string, string> metadata, string key, out float value)
        {
            value = 0f;
            if (metadata == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string raw;
            if (!metadata.TryGetValue(key, out raw) || string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string GetMetadata(IDictionary<string, string> metadata, string key)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string value;
            return metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string FormatDelta(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
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

        private static void IncrementFullAnalysisCount()
        {
            lock (SyncRoot)
            {
                _fullAnalysisCount++;
            }
        }

        private static void IncrementCheapPrecheckSkipCount()
        {
            lock (SyncRoot)
            {
                _cheapPrecheckSkipCount++;
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

        private static void RecordDecision(
            bool enabled,
            string decision,
            string reason,
            long tick,
            InputActionQueueFastState queueSnapshot,
            MovementSafeLandingAnalysis analysis,
            bool submitted,
            string configSummary,
            string compatError)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new MovementSafeLandingDiagnosticInfo() : _diagnostics.Clone();
                current.Enabled = enabled;
                current.LastTriggered = submitted;
                current.LastDecision = decision ?? string.Empty;
                current.LastSkipReason = submitted ? string.Empty : reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastTick = tick;
                current.PendingActionCount = queueSnapshot == null ? 0 : queueSnapshot.PendingCount;
                current.RunningActionKind = queueSnapshot == null ? string.Empty : queueSnapshot.RunningActionKind ?? string.Empty;
                current.ConfigSummary = configSummary ?? string.Empty;
                current.StageSummary = "priority0-safe(no-action),singleActiveRescuePerDescent=true,strictPriorityWait=true,priority1-equipped-active(order=double_jump>rocket_boots>flying_carpet>gravity_globe>flying_mount>non_flying_safe_mount,nearGroundDistanceGate=true,no-equipped-wings-input,no-grapple,gravityRestoreImmediate=true,gravityRestoreRetryTicks=" + SafeLandingGravityRestoreRetryTicks.ToString(CultureInfo.InvariantCulture) + ",mountCancelPreImpact=true,mountCancelRetryTicks=" + SafeLandingMountCancelRetryTicks.ToString(CultureInfo.InvariantCulture) + "),priority2-temporary-equipment(order=horseshoe>wings>fairy_boots>double_jump>rocket_boots>flying_carpet>gravity_globe>flying_mount>non_flying_safe_mount,activationCapabilityGate=true,temporaryActiveNearGroundDistanceGate=true,restoreStableTicks=" + TemporaryEquipmentStableRestoreTicks.ToString(CultureInfo.InvariantCulture) + "),priority3-temporary-held-item(order=umbrella,nearGroundDistanceGate=true,restoreStableTicks=" + TemporaryUmbrellaStableRestoreTicks.ToString(CultureInfo.InvariantCulture) + "),priority4-grapple(order=equipped_grapple>inventory_grapple,vanillaQuickGrappleInventoryScan=true,landingSurfaceTarget=true,relativeHookTiming=true,failOpenWhenTooLate=true),priority5-teleport-rod(order=inventory_teleport_rod,vanillaUseHotbarItem=true,nearGroundDistanceGate=true,noDirectPositionMutation=true)";
                current.StrategyCatalogVersion = MovementSafeLandingStrategyCatalog.Version;
                current.RecoveryStateSummary = BuildRecoveryStateSummary(tick);
                current.LastCompatError = compatError ?? string.Empty;
                current.CollisionFastPathStatus = MovementSafeLandingCompat.CollisionFastPathStatus;
                current.LandingProbeCount = MovementSafeLandingCompat.LandingProbeCount;
                current.PlayerUpdateHookInstalled = MovementSafeLandingCompat.PlayerUpdateHookInstalled;
                current.PlayerUpdateHookMessage = MovementSafeLandingCompat.PlayerUpdateHookMessage;
                SafeLandingJumpPulseSnapshot pulse;
                if (MovementSafeLandingCompat.TryGetAnySafeLandingJumpPulseSnapshot(out pulse) && pulse != null)
                {
                    current.QueuedJumpPulseActive = pulse.Active;
                    current.QueuedJumpPulseStatus = pulse.Status ?? string.Empty;
                    current.QueuedJumpPulseApplySite = pulse.LastApplySite ?? string.Empty;
                }
                else
                {
                    current.QueuedJumpPulseActive = false;
                    current.QueuedJumpPulseStatus = string.Empty;
                    current.QueuedJumpPulseApplySite = string.Empty;
                }

                lock (SyncRoot)
                {
                    current.TemporaryEquipmentApplied = TemporaryEquipmentRecords.Count > 0;
                    current.TemporaryEquipmentPendingRestoreCount = TemporaryEquipmentRecords.Count;
                    current.TemporaryEquipmentPendingRestoreNoSpaceCount = _temporaryEquipmentRestoreNoSpaceCount;
                    current.TemporaryEquipmentLastDecision = _temporaryEquipmentLastDecision ?? string.Empty;
                    current.TemporaryEquipmentLastSkipReason = _temporaryEquipmentLastSkipReason ?? string.Empty;
                    current.TemporaryEquipmentSelectedCategory = _temporaryEquipmentSelectedCategory ?? string.Empty;
                    current.TemporaryEquipmentSelectedSourceKind = _temporaryEquipmentSelectedSourceKind ?? string.Empty;
                    current.TemporaryEquipmentSelectedSourceSlot = _temporaryEquipmentSelectedSourceSlot;
                    current.TemporaryEquipmentSelectedTargetKind = _temporaryEquipmentSelectedTargetKind ?? string.Empty;
                    current.TemporaryEquipmentSelectedTargetSlot = _temporaryEquipmentSelectedTargetSlot;
                    current.TemporaryEquipmentSelectedItemType = _temporaryEquipmentSelectedItemType;
                    current.TemporaryEquipmentSelectedMountType = _temporaryEquipmentSelectedMountType;
                    current.FullAnalysisCount = _fullAnalysisCount;
                    current.CheapPrecheckSkipCount = _cheapPrecheckSkipCount;
                    current.GravityRestorePending = _safeLandingGravityRestorePending;
                    current.GravityRestoreOriginalDirection = _safeLandingGravityOriginalDirection;
                    current.GravityRestorePendingTicks = _safeLandingGravityRestorePending && _safeLandingGravityActivationTick >= 0
                        ? Math.Max(0, tick - _safeLandingGravityActivationTick)
                        : 0;
                    current.GravityRestoreLastDecision = _safeLandingGravityLastDecision ?? string.Empty;
                    current.GravityRestoreLastSkipReason = _safeLandingGravityLastSkipReason ?? string.Empty;
                }

                if (analysis != null)
                {
                    current.TextInputFocused = analysis.TextInputFocused;
                    current.TextInputReason = analysis.TextInputReason ?? string.Empty;
                    current.PlayerControllable = analysis.PlayerControllable;
                    current.Dangerous = analysis.Dangerous;
                    current.RescueWindow = analysis.RescueWindow;
                    current.AlreadySafe = analysis.AlreadySafe;
                    current.SafeReason = analysis.SafeReason ?? string.Empty;
                    current.RawCreativeGodMode = analysis.RawCreativeGodMode;
                    current.RawNoFallDmg = analysis.RawNoFallDmg;
                    current.RawSlowFall = analysis.RawSlowFall;
                    current.RawWet = analysis.RawWet;
                    current.RawHoneyWet = analysis.RawHoneyWet;
                    current.RawShimmering = analysis.RawShimmering;
                    current.RawWebbed = analysis.RawWebbed;
                    current.RawStoned = analysis.RawStoned;
                    current.RawGrapCount = analysis.RawGrapCount;
                    current.RawEquippedWingCount = analysis.RawEquippedWingCount;
                    current.RawMountNoFallDamage = analysis.RawMountNoFallDamage;
                    current.RawExtraFall = analysis.RawExtraFall;
                    current.FallingSpeed = analysis.FallingSpeed;
                    current.VelocityY = analysis.VelocityY;
                    current.GravityDirection = analysis.GravityDirection;
                    current.ImpactFound = analysis.ImpactFound;
                    current.ImpactDistancePixels = analysis.ImpactDistancePixels;
                    current.ImpactTicks = analysis.ImpactTicks;
                    current.EstimatedFallTiles = analysis.EstimatedFallTiles;
                    current.ActiveCapabilitySummary = analysis.ActiveCapabilitySummary ?? string.Empty;
                    current.SelectedStrategyId = analysis.SelectedStrategyId ?? string.Empty;
                    current.SelectedPriority = analysis.SelectedPriority;
                    current.SelectedActionType = analysis.SelectedActionType ?? string.Empty;
                    current.HasFlyingCarpet = analysis.HasFlyingCarpet;
                    current.HasFlyingCarpetAvailable = analysis.HasFlyingCarpetAvailable;
                    current.FlyingCarpetTime = analysis.FlyingCarpetTime;
                    current.HasGravityGlobe = analysis.HasGravityGlobe;
                    current.HasGravityFlipOpportunity = analysis.HasGravityFlipOpportunity;
                    current.HasEquippedFlyingMount = analysis.HasEquippedFlyingMount;
                    current.HasEquippedSafeMount = analysis.HasEquippedSafeMount;
                    current.HasEquippedGrapple = analysis.HasEquippedGrapple;
                    current.HasInventoryGrapple = analysis.HasInventoryGrapple;
                    current.HasTeleportRod = analysis.HasTeleportRod;
                    current.TeleportRodInventorySlot = analysis.TeleportRodInventorySlot;
                    current.TeleportRodItemType = analysis.TeleportRodItemType;
                    current.TeleportTargetKnown = analysis.HasTeleportTarget;
                    current.TeleportTargetTileX = analysis.TeleportTargetTileX;
                    current.TeleportTargetTileY = analysis.TeleportTargetTileY;
                    current.TeleportTargetWorldX = analysis.TeleportTargetWorldX;
                    current.TeleportTargetWorldY = analysis.TeleportTargetWorldY;
                    current.HasCushionBlock = analysis.HasCushionBlock;
                    current.CushionBlockInventorySlot = analysis.CushionBlockInventorySlot;
                    current.CushionBlockHotbarSlot = analysis.CushionBlockHotbarSlot;
                    current.CushionBlockItemType = analysis.CushionBlockItemType;
                    current.CushionBlockCreateTile = analysis.CushionBlockCreateTile;
                    current.BlockPlacementTargetKnown = analysis.HasBlockPlacementTarget;
                    current.BlockPlacementTileX = analysis.BlockPlacementTileX;
                    current.BlockPlacementTileY = analysis.BlockPlacementTileY;
                    current.BlockPlacementWorldX = analysis.BlockPlacementWorldX;
                    current.BlockPlacementWorldY = analysis.BlockPlacementWorldY;
                    current.StrategyEvaluationSummary = analysis.StrategyEvaluationSummary ?? string.Empty;
                    current.CandidateSummary = analysis.CandidateSummary ?? string.Empty;
                    current.SelectedPlanSummary = analysis.SelectedPlanSummary ?? string.Empty;
                    current.RejectedStrategiesSummary = analysis.RejectedStrategiesSummary ?? string.Empty;
                    current.PostApplyVerificationSummary = FirstNonEmpty(analysis.PostApplyVerificationSummary, _temporaryEquipmentPostApplyVerificationSummary);
                    current.LandingSurfaceKnown = analysis.LandingSurfaceKnown;
                    current.LandingContactWorldX = analysis.LandingContactWorldX;
                    current.LandingContactWorldY = analysis.LandingContactWorldY;
                    current.LandingContactTileX = analysis.LandingContactTileX;
                    current.LandingContactTileY = analysis.LandingContactTileY;
                    current.LandingSurfaceKind = analysis.LandingSurfaceKind ?? string.Empty;
                    current.LandingSlopeType = analysis.LandingSlopeType;
                    current.LandingSlopeDirection = analysis.LandingSlopeDirection ?? string.Empty;
                    current.LandingContactSample = analysis.LandingContactSample ?? string.Empty;
                    current.LandingMovingIntoSlope = analysis.LandingMovingIntoSlope;
                    current.LandingMovingWithSlope = analysis.LandingMovingWithSlope;
                    current.LandingSurfaceSummary = analysis.LandingSurfaceSummary ?? string.Empty;
                    current.GrappleHookSpeed = analysis.GrappleHookSpeed;
                    current.GrappleTargetSource = analysis.GrappleTargetSource ?? string.Empty;
                    current.GrappleTargetFromLandingSurface = analysis.GrappleTargetFromLandingSurface;
                    current.GrappleTargetDistancePixels = analysis.GrappleTargetDistancePixels;
                    current.GrappleHookVerticalSpeed = analysis.GrappleHookVerticalSpeed;
                    current.GrappleRelativeDownSpeed = analysis.GrappleRelativeDownSpeed;
                    current.GrappleRequiredLeadTicks = analysis.GrappleRequiredLeadTicks;
                    current.GrappleRequiredLeadPixels = analysis.GrappleRequiredLeadPixels;
                    current.GrappleEstimatedTicksToTarget = analysis.GrappleEstimatedTicksToTarget;
                    current.GrappleTooEarly = analysis.GrappleTooEarly;
                    current.GrappleTooLate = analysis.GrappleTooLate;
                    current.GrappleTooSlowForDownwardSurface = analysis.GrappleTooSlowForDownwardSurface;
                    current.GrappleTimingSummary = analysis.GrappleTimingSummary ?? string.Empty;
                    current.EquippedGrappleShootSpeed = analysis.EquippedGrappleShootSpeed;
                    current.InventoryGrappleShootSpeed = analysis.InventoryGrappleShootSpeed;
                    current.EquippedGrappleProjectileType = analysis.EquippedGrappleProjectileType;
                    current.InventoryGrappleProjectileType = analysis.InventoryGrappleProjectileType;
                }
                else
                {
                    current.TextInputFocused = false;
                    current.TextInputReason = string.Empty;
                    current.PlayerControllable = false;
                    current.Dangerous = false;
                    current.RescueWindow = false;
                    current.AlreadySafe = false;
                    current.SafeReason = string.Empty;
                    current.RawCreativeGodMode = false;
                    current.RawNoFallDmg = false;
                    current.RawSlowFall = false;
                    current.RawWet = false;
                    current.RawHoneyWet = false;
                    current.RawShimmering = false;
                    current.RawWebbed = false;
                    current.RawStoned = false;
                    current.RawGrapCount = 0;
                    current.RawEquippedWingCount = 0;
                    current.RawMountNoFallDamage = false;
                    current.RawExtraFall = 0;
                    current.FallingSpeed = 0f;
                    current.VelocityY = 0f;
                    current.GravityDirection = 0f;
                    current.ImpactFound = false;
                    current.ImpactDistancePixels = -1;
                    current.ImpactTicks = -1f;
                    current.EstimatedFallTiles = 0f;
                    current.ActiveCapabilitySummary = string.Empty;
                    current.SelectedStrategyId = string.Empty;
                    current.SelectedPriority = -1;
                    current.SelectedActionType = string.Empty;
                    current.HasFlyingCarpet = false;
                    current.HasFlyingCarpetAvailable = false;
                    current.FlyingCarpetTime = 0;
                    current.HasGravityGlobe = false;
                    current.HasGravityFlipOpportunity = false;
                    current.HasEquippedFlyingMount = false;
                    current.HasEquippedSafeMount = false;
                    current.HasEquippedGrapple = false;
                    current.HasInventoryGrapple = false;
                    current.HasTeleportRod = false;
                    current.TeleportRodInventorySlot = -1;
                    current.TeleportRodItemType = 0;
                    current.TeleportTargetKnown = false;
                    current.TeleportTargetTileX = -1;
                    current.TeleportTargetTileY = -1;
                    current.TeleportTargetWorldX = 0f;
                    current.TeleportTargetWorldY = 0f;
                    current.HasCushionBlock = false;
                    current.CushionBlockInventorySlot = -1;
                    current.CushionBlockHotbarSlot = -1;
                    current.CushionBlockItemType = 0;
                    current.CushionBlockCreateTile = -1;
                    current.BlockPlacementTargetKnown = false;
                    current.BlockPlacementTileX = -1;
                    current.BlockPlacementTileY = -1;
                    current.BlockPlacementWorldX = 0f;
                    current.BlockPlacementWorldY = 0f;
                    current.StrategyEvaluationSummary = string.Empty;
                    current.CandidateSummary = string.Empty;
                    current.SelectedPlanSummary = string.Empty;
                    current.RejectedStrategiesSummary = string.Empty;
                    current.PostApplyVerificationSummary = _temporaryEquipmentPostApplyVerificationSummary ?? string.Empty;
                }

                if (submitted)
                {
                    current.SubmittedCount++;
                    current.LastTriggerUtc = DateTime.UtcNow;
                }
                else
                {
                    current.SkippedCount++;
                }

                _diagnostics = current;
            }
        }

        private static string BuildRecoveryStateSummary(long tick)
        {
            lock (SyncRoot)
            {
                var state = new MovementSafeLandingRecoveryState
                {
                    GravityRestorePending = _safeLandingGravityRestorePending,
                    GravityOriginalDirection = _safeLandingGravityOriginalDirection,
                    MountCancelPending = _safeLandingMountCancelPending,
                    DescentRescueGuardActive = _descentRescueGuardActive,
                    DescentRescueGuardTicks = _descentRescueGuardActive && _descentRescueGuardStartedTick >= 0
                        ? Math.Max(0, tick - _descentRescueGuardStartedTick)
                        : 0,
                    DescentRescueGuardStrategy = _descentRescueGuardStrategyId ?? string.Empty,
                    TemporaryEquipmentLastDecision = _temporaryEquipmentLastDecision ?? string.Empty,
                    TemporaryEquipmentLastSkipReason = _temporaryEquipmentLastSkipReason ?? string.Empty,
                    GravityLastDecision = _safeLandingGravityLastDecision ?? string.Empty,
                    GravityLastSkipReason = _safeLandingGravityLastSkipReason ?? string.Empty
                };
                state.TemporaryEquipmentRecords.AddRange(TemporaryEquipmentRecords);
                var pendingTicks = _safeLandingGravityRestorePending && _safeLandingGravityActivationTick >= 0
                    ? Math.Max(0, tick - _safeLandingGravityActivationTick)
                    : 0;
                return state.BuildSummary() + ",gravityPendingTicks=" + pendingTicks.ToString(CultureInfo.InvariantCulture);
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
