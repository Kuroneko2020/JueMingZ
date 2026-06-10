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
        public static MovementSafeLandingDiagnosticInfo GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return _diagnostics.Clone();
            }
        }
        internal static void ResetDiagnosticsForTesting()
        {
            lock (DiagnosticsSyncRoot)
            {
                lock (SyncRoot)
                {
                    _fullAnalysisCount = 0;
                    _cheapPrecheckSkipCount = 0;
                    _cheapSkipDiagnosticSuppressedCount = 0;
                    _cheapSkipDiagnosticWrittenCount = 0;
                    _recoverySummarySkippedCount = 0;
                    _lastCheapSkipDiagnosticTick = -1;
                    _lastCheapSkipReason = string.Empty;
                    _lastCheapSkipBoundarySignature = long.MinValue;
                    _lastFullDiagnosticWasException = false;
                    _stageSummaryCache = null;
                    _stageSummaryCacheHitCount = 0;
                }

                _diagnostics = new MovementSafeLandingDiagnosticInfo();
            }

            MovementSafeLandingOptionCatalog.ResetConfigSummaryCacheForTesting();
        }

        internal static void RecordCheapPrecheckSkipForTesting(long tick, string reason, bool playerControllable)
        {
            IncrementCheapPrecheckSkipCount();
            RecordCheapPrecheckSkipDecision(
                tick,
                InputActionQueueFastState.Empty,
                new MovementSafeLandingAnalysis
                {
                    PlayerControllable = playerControllable,
                    SkipReason = reason ?? string.Empty,
                    FallingSpeed = playerControllable ? 0.25f : 0f,
                    VelocityY = playerControllable ? 0.25f : 0f,
                    GravityDirection = 1f
                },
                reason,
                AppSettings.CreateDefault());
        }

        internal static void RecordFullDecisionForTesting(string decision, string reason, long tick)
        {
            RecordDecision(
                true,
                decision,
                reason,
                tick,
                InputActionQueueFastState.Empty,
                new MovementSafeLandingAnalysis
                {
                    PlayerControllable = true,
                    Dangerous = string.Equals(decision, "submitted", StringComparison.Ordinal),
                    RescueWindow = string.Equals(decision, "submitted", StringComparison.Ordinal),
                    SkipReason = reason ?? string.Empty
                },
                string.Equals(decision, "submitted", StringComparison.Ordinal),
                MovementSafeLandingOptionCatalog.BuildConfigSummary(AppSettings.CreateDefault()),
                string.Empty);
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

        private static void RecordCheapPrecheckSkipDecision(
            long tick,
            InputActionQueueFastState queueSnapshot,
            MovementSafeLandingAnalysis analysis,
            string reason,
            AppSettings settings)
        {
            reason = reason ?? string.Empty;
            var boundarySignature = BuildCheapSkipBoundarySignature(queueSnapshot, analysis);
            if (ShouldRecordCheapSkipDiagnostics(reason, boundarySignature, tick))
            {
                MarkCheapSkipDiagnosticWritten(reason, boundarySignature, tick);
                RecordDecision(
                    true,
                    "skipped",
                    reason,
                    tick,
                    queueSnapshot,
                    analysis,
                    false,
                    MovementSafeLandingOptionCatalog.BuildConfigSummary(settings),
                    string.Empty);
                return;
            }

            MarkCheapSkipDiagnosticSuppressed();
            RecordLightweightCheapSkipDecision(tick, queueSnapshot, analysis, reason);
        }

        private static bool ShouldRecordCheapSkipDiagnostics(string reason, long boundarySignature, long tick)
        {
            lock (SyncRoot)
            {
                if (_lastFullDiagnosticWasException)
                {
                    return true;
                }

                if (!string.Equals(_lastCheapSkipReason, reason, StringComparison.Ordinal))
                {
                    return true;
                }

                if (_lastCheapSkipBoundarySignature != boundarySignature)
                {
                    return true;
                }

                if (_lastCheapSkipDiagnosticTick < 0 || tick < _lastCheapSkipDiagnosticTick)
                {
                    return true;
                }

                return tick - _lastCheapSkipDiagnosticTick >= CheapSkipDiagnosticCadenceTicks;
            }
        }

        private static long BuildCheapSkipBoundarySignature(
            InputActionQueueFastState queueSnapshot,
            MovementSafeLandingAnalysis analysis)
        {
            unchecked
            {
                long signature = 17;
                signature = signature * 31 + (queueSnapshot == null ? 0 : queueSnapshot.PendingCount);
                signature = signature * 31 + (queueSnapshot != null && queueSnapshot.HasRunningAction ? 1 : 0);
                signature = signature * 31 + (queueSnapshot == null ? 0 : (int)queueSnapshot.RunningActionKindValue);
                signature = signature * 31 + (ItemUseBridge.PendingRequestId == Guid.Empty ? 0 : 1);
                if (analysis != null)
                {
                    signature = signature * 31 + (analysis.TextInputFocused ? 1 : 0);
                    signature = signature * 31 + (analysis.PlayerControllable ? 1 : 0);
                    signature = signature * 31 + (analysis.Dangerous ? 1 : 0);
                    signature = signature * 31 + (analysis.RescueWindow ? 1 : 0);
                    signature = signature * 31 + (analysis.AlreadySafe ? 1 : 0);
                    signature = signature * 31 + (analysis.RawNoFallDmg ? 1 : 0);
                    signature = signature * 31 + (analysis.RawSlowFall ? 1 : 0);
                    signature = signature * 31 + (analysis.RawWet ? 1 : 0);
                    signature = signature * 31 + (analysis.RawHoneyWet ? 1 : 0);
                    signature = signature * 31 + (analysis.RawShimmering ? 1 : 0);
                    signature = signature * 31 + (analysis.RawWebbed ? 1 : 0);
                    signature = signature * 31 + (analysis.RawStoned ? 1 : 0);
                }

                lock (SyncRoot)
                {
                    signature = signature * 31 + (_safeLandingGravityRestorePending ? 1 : 0);
                    signature = signature * 31 + (_safeLandingMountCancelPending ? 1 : 0);
                    signature = signature * 31 + (TemporaryEquipmentRecords.Count > 0 ? 1 : 0);
                    signature = signature * 31 + (_temporaryEquipmentApplyRequestId == Guid.Empty ? 0 : 1);
                    signature = signature * 31 + (_temporaryEquipmentActivationRequestId == Guid.Empty ? 0 : 1);
                    signature = signature * 31 + (_temporaryEquipmentRestoreRequestId == Guid.Empty ? 0 : 1);
                }

                return signature;
            }
        }

        private static void MarkCheapSkipDiagnosticWritten(string reason, long boundarySignature, long tick)
        {
            lock (SyncRoot)
            {
                _cheapSkipDiagnosticWrittenCount++;
                _lastCheapSkipReason = reason ?? string.Empty;
                _lastCheapSkipBoundarySignature = boundarySignature;
                _lastCheapSkipDiagnosticTick = tick;
                _lastFullDiagnosticWasException = false;
            }
        }

        private static void MarkCheapSkipDiagnosticSuppressed()
        {
            lock (SyncRoot)
            {
                _cheapSkipDiagnosticSuppressedCount++;
                _recoverySummarySkippedCount++;
            }
        }

        private static void RecordLightweightCheapSkipDecision(
            long tick,
            InputActionQueueFastState queueSnapshot,
            MovementSafeLandingAnalysis analysis,
            string reason)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics ?? new MovementSafeLandingDiagnosticInfo();
                current.Enabled = true;
                current.LastTriggered = false;
                current.LastDecision = "skipped";
                current.LastSkipReason = reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastTick = tick;
                current.PendingActionCount = queueSnapshot == null ? 0 : queueSnapshot.PendingCount;
                current.RunningActionKind = queueSnapshot == null ? string.Empty : queueSnapshot.RunningActionKind ?? string.Empty;
                current.LastCompatError = string.Empty;
                current.CollisionFastPathStatus = MovementSafeLandingCompat.CollisionFastPathStatus;
                current.LandingProbeCount = MovementSafeLandingCompat.LandingProbeCount;
                current.PlayerUpdateHookInstalled = MovementSafeLandingCompat.PlayerUpdateHookInstalled;
                current.PlayerUpdateHookMessage = MovementSafeLandingCompat.PlayerUpdateHookMessage;
                if (analysis != null)
                {
                    current.TextInputFocused = analysis.TextInputFocused;
                    current.TextInputReason = analysis.TextInputReason ?? string.Empty;
                    current.PlayerControllable = analysis.PlayerControllable;
                    current.Dangerous = analysis.Dangerous;
                    current.RescueWindow = analysis.RescueWindow;
                    current.AlreadySafe = analysis.AlreadySafe;
                    current.SafeReason = analysis.SafeReason ?? string.Empty;
                    current.FallingSpeed = analysis.FallingSpeed;
                    current.VelocityY = analysis.VelocityY;
                    current.GravityDirection = analysis.GravityDirection;
                }

                current.SkippedCount++;
                ApplySafeLandingOptimizationCounters(current);
                _diagnostics = current;
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
                current.StageSummary = GetStageSummary();
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

                ApplySafeLandingOptimizationCounters(current);
                lock (SyncRoot)
                {
                    _lastFullDiagnosticWasException = string.Equals(decision, "exception", StringComparison.Ordinal);
                }

                _diagnostics = current;
            }
        }

        private static string GetStageSummary()
        {
            if (_stageSummaryCache != null)
            {
                lock (SyncRoot)
                {
                    _stageSummaryCacheHitCount++;
                }

                return _stageSummaryCache;
            }

            _stageSummaryCache =
                "priority0-safe(no-action),singleActiveRescuePerDescent=true,strictPriorityWait=true,priority1-equipped-active(order=double_jump>rocket_boots>flying_carpet>gravity_globe>flying_mount>non_flying_safe_mount,nearGroundDistanceGate=true,no-equipped-wings-input,no-grapple,gravityRestoreImmediate=true,gravityRestoreRetryTicks=" +
                SafeLandingGravityRestoreRetryTicks.ToString(CultureInfo.InvariantCulture) +
                ",mountCancelPreImpact=true,mountCancelRetryTicks=" +
                SafeLandingMountCancelRetryTicks.ToString(CultureInfo.InvariantCulture) +
                "),priority2-temporary-equipment(order=horseshoe>wings>fairy_boots>double_jump>rocket_boots>flying_carpet>gravity_globe>flying_mount>non_flying_safe_mount,activationCapabilityGate=true,temporaryActiveNearGroundDistanceGate=true,restoreStableTicks=" +
                TemporaryEquipmentStableRestoreTicks.ToString(CultureInfo.InvariantCulture) +
                "),priority3-temporary-held-item(order=umbrella,nearGroundDistanceGate=true,restoreStableTicks=" +
                TemporaryUmbrellaStableRestoreTicks.ToString(CultureInfo.InvariantCulture) +
                "),priority4-grapple(order=equipped_grapple>inventory_grapple,vanillaQuickGrappleInventoryScan=true,landingSurfaceTarget=true,relativeHookTiming=true,failOpenWhenTooLate=true),priority5-teleport-rod(order=inventory_teleport_rod,vanillaUseHotbarItem=true,nearGroundDistanceGate=true,noDirectPositionMutation=true)";
            return _stageSummaryCache;
        }

        private static void ApplySafeLandingOptimizationCounters(MovementSafeLandingDiagnosticInfo current)
        {
            if (current == null)
            {
                return;
            }

            long configHitCount;
            long configMissCount;
            MovementSafeLandingOptionCatalog.GetConfigSummaryCacheStats(out configHitCount, out configMissCount);
            current.ConfigSummaryCacheHitCount = configHitCount;
            current.ConfigSummaryCacheMissCount = configMissCount;
            lock (SyncRoot)
            {
                current.StageSummaryCacheHitCount = _stageSummaryCacheHitCount;
                current.CheapSkipDiagnosticSuppressedCount = _cheapSkipDiagnosticSuppressedCount;
                current.CheapSkipDiagnosticWrittenCount = _cheapSkipDiagnosticWrittenCount;
                current.CheapSkipLastReason = _lastCheapSkipReason ?? string.Empty;
                current.CheapSkipDiagnosticCadenceTicks = CheapSkipDiagnosticCadenceTicks;
                current.RecoverySummarySkippedCount = _recoverySummarySkippedCount;
                current.FullAnalysisCount = _fullAnalysisCount;
                current.CheapPrecheckSkipCount = _cheapPrecheckSkipCount;
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
    }
}
