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
        // SafeLanding may analyze danger and enqueue vanilla rescue or restore
        // actions; it must not write position, velocity, fall state, life, buffs,
        // tiles, or network state directly.
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
        private const int CheapSkipDiagnosticCadenceTicks = 30;
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
        private static long _cheapSkipDiagnosticSuppressedCount;
        private static long _cheapSkipDiagnosticWrittenCount;
        private static long _recoverySummarySkippedCount;
        private static long _lastCheapSkipDiagnosticTick = -1;
        private static string _lastCheapSkipReason = string.Empty;
        private static long _lastCheapSkipBoundarySignature = long.MinValue;
        private static bool _lastFullDiagnosticWasException;
        private static string _stageSummaryCache;
        private static long _stageSummaryCacheHitCount;
        private static MovementSafeLandingDiagnosticInfo _diagnostics = new MovementSafeLandingDiagnosticInfo();

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

                // Temporary equipment records are restore debt, not a completed
                // rescue. Drain them before considering a new SafeLanding action.
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
                    RecordCheapPrecheckSkipDecision(
                        tick,
                        queueSnapshot,
                        analysis,
                        FirstNonEmpty(analysis.SkipReason, "notDangerous:cheap"),
                        settings);
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
                    InputActionAdmissionResult activeAdmission;
                    if (!queue.TryEnqueue(activeRequest, out activeAdmission))
                    {
                        RecordDecision(true, "skipped", "admissionDenied:" + (activeAdmission == null ? "unknown" : activeAdmission.Reason), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

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
                    InputActionAdmissionResult grappleAdmission;
                    if (!queue.TryEnqueue(grappleRequest, out grappleAdmission))
                    {
                        RecordDecision(true, "skipped", "admissionDenied:" + (grappleAdmission == null ? "unknown" : grappleAdmission.Reason), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

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
                    InputActionAdmissionResult teleportAdmission;
                    if (!queue.TryEnqueue(teleportRodRequest, out teleportAdmission))
                    {
                        RecordDecision(true, "skipped", "admissionDenied:" + (teleportAdmission == null ? "unknown" : teleportAdmission.Reason), tick, queueSnapshot, analysis, false, MovementSafeLandingOptionCatalog.BuildConfigSummary(settings), string.Empty);
                        return;
                    }

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
    }
}
