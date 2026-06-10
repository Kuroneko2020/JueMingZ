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

    }
}
