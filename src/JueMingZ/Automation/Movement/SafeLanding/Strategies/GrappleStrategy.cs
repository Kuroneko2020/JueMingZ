using System;
using System.Collections.Generic;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Config;

namespace JueMingZ.Automation.Movement
{
    internal sealed class GrappleStrategy : IMovementSafeLandingStrategy
    {
        private const float GrappleInputDelayTicks = 2f;
        private const float GrappleLatchConfirmTicks = 1f;
        private const float GrappleSafetyTicks = 1f;
        private const float HorizontalMotionThreshold = 0.01f;
        private const float DirectionalAimLeadMinPixels = 8f;
        private const float DirectionalAimLeadMaxPixels = 24f;
        private const float DirectionalAimLeadSpeedScale = 2.5f;
        private const float SlopeWithMotionAimOffsetPixels = 10f;
        private const int GrappleOneTileLeadPixels = 16;
        private const int GrappleActivationHardCapPixels = 480;

        public IEnumerable<MovementSafeLandingStrategyEvaluation> Evaluate(MovementSafeLandingStrategyContext context)
        {
            var settings = context == null ? null : context.Settings;
            var analysis = context == null ? null : context.Analysis;
            var hazard = context == null ? null : context.Hazard;
            var capability = context == null ? null : context.Capability;
            var configEnabled = MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.Grapple);
            var playerReady = hazard == null || hazard.PlayerControllable;
            var hasEquippedGrapple = capability != null && capability.HasEquippedGrapple;
            var hasInventoryGrapple = capability != null && capability.HasInventoryGrapple;
            var hasGrapple = hasEquippedGrapple || hasInventoryGrapple;
            var hasImpactTarget = analysis != null && analysis.ImpactFound;
            var candidate = configEnabled && playerReady && hasGrapple && hasImpactTarget;
            var strategyId = hasEquippedGrapple
                ? MovementSafeLandingStrategyIds.EquippedGrapple
                : MovementSafeLandingStrategyIds.InventoryGrapple;

            string timingReason = string.Empty;
            var ready = false;
            var tooLate = false;
            var tooSlow = false;
            var lastResortWithoutTeleportFallback = false;
            if (candidate && analysis != null)
            {
                UpdateGrappleTarget(analysis);
                var grappleTimingReason = string.Empty;
                ready = IsGrappleRescueWindowReady(analysis, out grappleTimingReason);
                timingReason = grappleTimingReason;
                tooLate = analysis.GrappleTooLate;
                tooSlow = analysis.GrappleTooSlowForDownwardSurface;
                lastResortWithoutTeleportFallback =
                    !ready &&
                    (tooLate || tooSlow) &&
                    !HasPriorityFiveFallbackCandidate(settings, hazard, capability, analysis);
                if (lastResortWithoutTeleportFallback)
                {
                    ready = true;
                    timingReason = "grappleLastResortNoPriorityFiveFallback:" + timingReason;
                    analysis.GrappleTimingSummary = AppendTimingMarker(analysis.GrappleTimingSummary, "lastResortNoPriorityFiveFallback");
                }
            }
            else if (candidate)
            {
                timingReason = "analysisUnavailable";
            }

            var blocksLower = candidate && ((!tooLate && !tooSlow) || lastResortWithoutTeleportFallback);

            yield return new MovementSafeLandingStrategyEvaluation
            {
                StrategyId = strategyId,
                Priority = 4,
                ActionType = MovementSafeLandingActionTypes.Grapple,
                RequestKind = InputActionKind.Jump,
                RequiredChannels = InputActionChannel.Jump | InputActionChannel.Grapple | InputActionChannel.MouseTarget,
                TimingWindow = timingReason,
                IsCandidate = candidate,
                IsReady = candidate && ready,
                BlocksLowerPriority = blocksLower,
                RequiresTemporaryEquipment = false,
                RequiresRestore = false,
                SkipReason = candidate
                    ? ready ? string.Empty : timingReason
                    : !configEnabled
                        ? MovementSafeLandingSkipReasons.ConfigDisabled
                        : !playerReady
                            ? MovementSafeLandingSkipReasons.PlayerNotControllable
                            : !hasGrapple
                                ? "grappleUnavailable"
                                : "grappleTargetUnavailable",
                Confidence = candidate ? (lastResortWithoutTeleportFallback ? "low" : (ready ? "medium" : (tooLate || tooSlow ? "low" : "waiting"))) : "none",
                Readiness = candidate ? lastResortWithoutTeleportFallback ? "lastResort" : ready ? "ready" : (tooLate ? "tooLate" : (tooSlow ? "tooSlow" : "tooEarly")) : "notCandidate",
                SortReason = "priority4-after-priority0-1-2-3,vanillaQuickGrapple,equippedOrInventory,landingSurfaceTarget,relativeHookTiming,failOpenWhenTooLate"
            };
        }

        private static bool HasPriorityFiveFallbackCandidate(
            AppSettings settings,
            MovementSafeLandingHazardContext hazard,
            MovementSafeLandingCapabilitySnapshot capability,
            MovementSafeLandingAnalysis analysis)
        {
            return MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.TeleportRod) &&
                   (hazard == null || hazard.PlayerControllable) &&
                   capability != null &&
                   capability.HasTeleportRod &&
                   analysis != null &&
                   analysis.ImpactFound;
        }

        private static string AppendTimingMarker(string summary, string marker)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return marker;
            }

            if (summary.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return summary;
            }

            return summary + "," + marker;
        }

        private static void UpdateGrappleTarget(MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null || !analysis.ImpactFound)
            {
                if (analysis != null)
                    analysis.HasGrappleTarget = false;
                return;
            }

            float targetX;
            float targetY;
            string source;

            if (!TryResolveGrappleTargetFromLandingSurface(analysis, out targetX, out targetY, out source))
            {
                targetX = analysis.ImpactWorldX;
                var direction = analysis.GravityDirection >= 0f ? 1 : -1;
                var targetTileY = (int)System.Math.Floor((analysis.ImpactWorldY + direction * 1f) / 16f);
                targetY = targetTileY * 16f + 4f;
                source = "fallback_impact";
                analysis.GrappleTargetFromLandingSurface = false;
            }

            analysis.HasGrappleTarget = true;
            analysis.GrappleTargetWorldX = targetX;
            analysis.GrappleTargetWorldY = targetY;
            analysis.GrappleTargetSource = source;
        }

        private static bool TryResolveGrappleTargetFromLandingSurface(
            MovementSafeLandingAnalysis analysis,
            out float targetX,
            out float targetY,
            out string source)
        {
            targetX = analysis.ImpactWorldX;
            targetY = analysis.ImpactWorldY;
            source = "fallback_impact";

            if (!analysis.LandingSurfaceKnown)
            {
                return false;
            }

            var direction = analysis.GravityDirection >= 0f ? 1 : -1;
            var tileLeft = analysis.LandingContactTileX * 16f;
            var tileTop = analysis.LandingContactTileY * 16f;

            var hasHorizontalMotion = HasHorizontalMotion(analysis);
            if (analysis.LandingSurfaceKind == "slope")
            {
                targetX = hasHorizontalMotion
                    ? ResolveSlopeMotionTargetX(analysis)
                    : ClampFloat(analysis.LandingContactWorldX, tileLeft + 2f, tileLeft + 14f);
                targetY = ResolveSlopeSurfaceY(analysis, targetX) + direction * 4f;
                source = hasHorizontalMotion
                    ? analysis.LandingMovingWithSlope
                        ? "landing_surface_slope_with_motion"
                        : analysis.LandingMovingIntoSlope
                            ? "landing_surface_slope_into_motion"
                            : "landing_surface_slope_motion"
                    : "landing_surface_slope";
                analysis.GrappleTargetFromLandingSurface = true;
                return true;
            }

            if (analysis.LandingSurfaceKind == "half_brick")
            {
                targetX = hasHorizontalMotion
                    ? ResolveDirectionalFootTargetX(analysis)
                    : ClampFloat(analysis.LandingContactWorldX, tileLeft + 3f, tileLeft + 13f);
                targetY = analysis.LandingContactWorldY + direction * 4f;
                source = hasHorizontalMotion ? "landing_surface_half_brick_motion" : "landing_surface_half_brick";
                analysis.GrappleTargetFromLandingSurface = true;
                return true;
            }

            targetX = hasHorizontalMotion
                ? ResolveDirectionalFootTargetX(analysis)
                : ClampFloat(analysis.LandingContactWorldX, tileLeft + 3f, tileLeft + 13f);
            targetY = ResolveGenericSurfaceTargetY(analysis, tileTop) + direction * 4f;
            source = "landing_surface_" + (analysis.LandingSurfaceKind ?? "unknown") + (hasHorizontalMotion ? "_motion" : string.Empty);
            analysis.GrappleTargetFromLandingSurface = true;
            return true;
        }

        private static bool HasHorizontalMotion(MovementSafeLandingAnalysis analysis)
        {
            return analysis != null && System.Math.Abs(analysis.VelocityX) > HorizontalMotionThreshold;
        }

        private static float ResolveDirectionalFootTargetX(MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null)
            {
                return 0f;
            }

            var direction = analysis.VelocityX > 0f ? 1f : -1f;
            float left;
            float right;
            if (TryGetProjectedPlayerBounds(analysis, out left, out right))
            {
                var lead = ResolveDirectionalAimLeadPixels(analysis);
                return direction > 0f
                    ? right + lead
                    : left - lead;
            }

            var fallbackOffset = System.Math.Max(8f, analysis.Width * 0.5f) + ResolveDirectionalAimLeadPixels(analysis);
            return analysis.ImpactWorldX + direction * fallbackOffset;
        }

        private static float ResolveSlopeMotionTargetX(MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null)
            {
                return 0f;
            }

            if (analysis.LandingMovingWithSlope)
            {
                return ResolveMotionBiasedCenterTargetX(analysis, SlopeWithMotionAimOffsetPixels);
            }

            return ResolveDirectionalFootTargetX(analysis);
        }

        private static float ResolveMotionBiasedCenterTargetX(MovementSafeLandingAnalysis analysis, float offsetPixels)
        {
            var direction = analysis.VelocityX > 0f ? 1f : -1f;
            float left;
            float right;
            if (TryGetProjectedPlayerBounds(analysis, out left, out right))
            {
                var center = (left + right) / 2f;
                var maxOffset = System.Math.Max(4f, (right - left) * 0.55f);
                var offset = ClampFloat(offsetPixels, 2f, maxOffset);
                return center + direction * offset;
            }

            return analysis.ImpactWorldX + direction * offsetPixels;
        }

        private static float ResolveDirectionalAimLeadPixels(MovementSafeLandingAnalysis analysis)
        {
            var speed = analysis == null ? 0f : System.Math.Abs(analysis.VelocityX);
            return ClampFloat(
                speed * DirectionalAimLeadSpeedScale,
                DirectionalAimLeadMinPixels,
                DirectionalAimLeadMaxPixels);
        }

        private static bool TryGetProjectedPlayerBounds(MovementSafeLandingAnalysis analysis, out float left, out float right)
        {
            left = 0f;
            right = 0f;
            if (analysis == null)
            {
                return false;
            }

            if (analysis.LandingProjectedPlayerRightX > analysis.LandingProjectedPlayerLeftX + 1f)
            {
                left = analysis.LandingProjectedPlayerLeftX;
                right = analysis.LandingProjectedPlayerRightX;
                return true;
            }

            if (analysis.Width > 1)
            {
                left = analysis.ImpactWorldX - analysis.Width / 2f;
                right = analysis.ImpactWorldX + analysis.Width / 2f;
                return true;
            }

            return false;
        }

        private static float ResolveGenericSurfaceTargetY(MovementSafeLandingAnalysis analysis, float tileTop)
        {
            if (analysis == null)
            {
                return tileTop;
            }

            return analysis.LandingContactWorldY > 0f
                ? analysis.LandingContactWorldY
                : tileTop;
        }

        private static float ResolveSlopeSurfaceY(MovementSafeLandingAnalysis analysis, float targetX)
        {
            if (analysis == null)
            {
                return 0f;
            }

            var tileLeft = analysis.LandingContactTileX * 16f;
            var tileTop = analysis.LandingContactTileY * 16f;
            var localX = ClampFloat(targetX - tileLeft, 0f, 16f);
            if (analysis.LandingSlopeType == 1 || analysis.LandingSlopeType == 3)
            {
                return tileTop + localX;
            }

            if (analysis.LandingSlopeType == 2 || analysis.LandingSlopeType == 4)
            {
                return tileTop + 16f - localX;
            }

            return analysis.LandingContactWorldY;
        }

        internal static bool IsGrappleRescueWindowReady(MovementSafeLandingAnalysis analysis, out string reason)
        {
            reason = string.Empty;
            if (analysis == null)
            {
                reason = "grappleAnalysisUnavailable";
                return false;
            }

            if (!analysis.ImpactFound || analysis.ImpactTicks < 0f)
            {
                reason = "impactTicksUnavailable";
                return false;
            }

            analysis.GrappleTooEarly = false;
            analysis.GrappleTooLate = false;
            analysis.GrappleTooSlowForDownwardSurface = false;

            if (!analysis.HasGrappleTarget)
            {
                reason = "grappleTargetUnavailable";
                return false;
            }

            var hookSpeed = analysis.GrappleHookSpeed;
            if (hookSpeed <= 0f)
            {
                analysis.GrappleTooSlowForDownwardSurface = true;
                analysis.GrappleTimingSummary = "grappleHookSpeedUnavailable";
                reason = "grappleHookSpeedUnavailable";
                return false;
            }

            var fallSpeed = System.Math.Max(analysis.FallingSpeed, 6.25f);
            var playerCenterX = analysis.PositionX + analysis.Width / 2f;
            var playerCenterY = analysis.PositionY + analysis.Height / 2f;
            var gTargetX = analysis.GrappleTargetWorldX;
            var gTargetY = analysis.GrappleTargetWorldY;

            var dx = gTargetX - playerCenterX;
            var dy = gTargetY - playerCenterY;
            var distance = (float)System.Math.Sqrt(dx * dx + dy * dy);

            analysis.GrappleTargetDistancePixels = distance;

            var gravitySign = analysis.GravityDirection >= 0f ? 1f : -1f;
            var downComponent = (dy * gravitySign) / System.Math.Max(1f, distance);
            var hookVerticalSpeed = hookSpeed * System.Math.Max(0f, downComponent);
            analysis.GrappleHookVerticalSpeed = hookVerticalSpeed;

            if (downComponent <= 0f || hookVerticalSpeed <= 0.25f)
            {
                analysis.GrappleTooSlowForDownwardSurface = true;
                analysis.GrappleTooEarly = false;
                analysis.GrappleTooLate = false;
                analysis.GrappleRelativeDownSpeed = 0f;
                reason = "grappleTooSlowForDownwardSurface:downComponent=" + downComponent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                return false;
            }

            var relativeDownSpeed = hookVerticalSpeed - fallSpeed;
            analysis.GrappleRelativeDownSpeed = relativeDownSpeed;

            if (relativeDownSpeed <= 0.25f)
            {
                analysis.GrappleTooSlowForDownwardSurface = true;
                analysis.GrappleTooEarly = false;
                analysis.GrappleTooLate = false;
                reason = "grappleTooSlowForDownwardSurface:relative=" + relativeDownSpeed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                return false;
            }

            var centerToBottom = analysis.Height * 0.5f;
            var ticksToReachPlayerBottom = centerToBottom / relativeDownSpeed;
            var requiredLeadTicks = GrappleInputDelayTicks + ticksToReachPlayerBottom + GrappleSafetyTicks;
            var requiredLeadPixels = GrappleOneTileLeadPixels + (int)System.Math.Ceiling(fallSpeed * requiredLeadTicks);

            analysis.GrappleRequiredLeadTicks = requiredLeadTicks;
            analysis.GrappleRequiredLeadPixels = requiredLeadPixels;

            var ticksToTarget = distance / hookSpeed;
            analysis.GrappleEstimatedTicksToTarget = ticksToTarget;
            var requiredTicksToLatch = GrappleInputDelayTicks + ticksToTarget + GrappleLatchConfirmTicks + GrappleSafetyTicks;

            var sb = new StringBuilder();
            sb.Append("hookSpeed=");
            sb.Append(hookSpeed.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",hookVert=");
            sb.Append(hookVerticalSpeed.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",relDown=");
            sb.Append(relativeDownSpeed.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",leadTicks=");
            sb.Append(requiredLeadTicks.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",leadPx=");
            sb.Append(requiredLeadPixels);
            sb.Append(",targetDist=");
            sb.Append(distance.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",targetTicks=");
            sb.Append(ticksToTarget.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",impactTicks=");
            sb.Append(analysis.ImpactTicks.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",impactDist=");
            sb.Append(analysis.ImpactDistancePixels);

            if (analysis.ImpactTicks < requiredTicksToLatch)
            {
                analysis.GrappleTooEarly = false;
                analysis.GrappleTooLate = true;
                analysis.GrappleTooSlowForDownwardSurface = false;
                sb.Append(",grappleTooLate:impactTicks<requiredLatchTicks:");
                sb.Append(analysis.ImpactTicks.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                sb.Append("<");
                sb.Append(requiredTicksToLatch.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                analysis.GrappleTimingSummary = sb.ToString();
                reason = "grappleTooLate:impactTicks<requiredLatchTicks:" +
                    analysis.ImpactTicks.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) +
                    "<" + requiredTicksToLatch.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                return false;
            }

            if (analysis.ImpactDistancePixels > requiredLeadPixels)
            {
                analysis.GrappleTooEarly = true;
                analysis.GrappleTooLate = false;
                analysis.GrappleTooSlowForDownwardSurface = false;
                sb.Append(",grappleTooEarly:impactDistanceTooFar:");
                sb.Append(analysis.ImpactDistancePixels);
                sb.Append(">");
                sb.Append(requiredLeadPixels);
                analysis.GrappleTimingSummary = sb.ToString();
                reason = "grappleTooEarly:impactDistanceTooFar:" +
                    analysis.ImpactDistancePixels + ">" + requiredLeadPixels;
                return false;
            }

            if (analysis.ImpactTicks > 20f)
            {
                analysis.GrappleTooEarly = true;
                analysis.GrappleTooLate = false;
                analysis.GrappleTooSlowForDownwardSurface = false;
                sb.Append(",grappleTooEarly:impactTicksTooFar:");
                sb.Append(analysis.ImpactTicks.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(">20");
                analysis.GrappleTimingSummary = sb.ToString();
                reason = "grappleTooEarly:impactTicksTooFar:" +
                    analysis.ImpactTicks.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ">20";
                return false;
            }

            sb.Append(",grappleReady");
            analysis.GrappleTooEarly = false;
            analysis.GrappleTooLate = false;
            analysis.GrappleTooSlowForDownwardSurface = false;
            analysis.GrappleTimingSummary = sb.ToString();
            reason = "grappleReady:lead=" + requiredLeadPixels + ",targetTicks=" +
                ticksToTarget.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        private static float ClampFloat(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }
    }
}
