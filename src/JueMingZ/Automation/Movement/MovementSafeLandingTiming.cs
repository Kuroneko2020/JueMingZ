using System;
using System.Globalization;

namespace JueMingZ.Automation.Movement
{
    internal static class MovementSafeLandingTiming
    {
        private const float DoubleJumpActivationWindowTicks = 3.5f;
        private const float RocketBootsActivationWindowTicks = 4.5f;
        private const float FlyingCarpetActivationWindowTicks = 3f;
        private const float FlyingMountActivationWindowTicks = 6f;
        private const float GravityGlobeActivationWindowTicks = 4.5f;
        private const float GrappleActivationWindowTicks = 12f;
        private const float TeleportRodActivationWindowTicks = 6f;
        private const float UmbrellaApplyWindowTicks = 3.5f;
        private const float DefaultActivationWindowTicks = 5f;
        private const float TemporaryActiveApplyLeadTicks = 7f;
        private const float TemporaryPassiveApplyWindowTicks = 5f;
        private const int DoubleJumpActivationMinPixels = 24;
        private const int DoubleJumpActivationMaxPixels = 40;
        private const int DoubleJumpTemporaryApplyMaxPixels = 112;
        private const int RocketBootsActivationMaxPixels = 56;
        private const int RocketBootsTemporaryApplyMaxPixels = 176;
        private const int FlyingCarpetActivationMaxPixels = 32;
        private const int FlyingCarpetTemporaryApplyMaxPixels = 144;
        private const int FlyingMountActivationMaxPixels = 96;
        private const int FlyingMountTemporaryApplyMaxPixels = 224;
        private const int GravityGlobeActivationMaxPixels = 56;
        private const int GravityGlobeTemporaryApplyMaxPixels = 160;
        private const int GrappleActivationMaxPixels = 144;
        private const int TeleportRodActivationMaxPixels = 96;
        private const int TemporaryPassiveApplyMaxPixels = 72;
        private const int UmbrellaTemporaryApplyMaxPixels = 32;

        internal static bool IsEquippedRescueWindowReady(MovementSafeLandingAnalysis analysis, out string reason)
        {
            if (analysis == null)
            {
                reason = "analysisUnavailable";
                return false;
            }

            return IsWindowReady(
                analysis.SelectedStrategyId,
                string.Empty,
                analysis.SelectedActionType,
                false,
                TimingStage.Activation,
                analysis.ImpactTicks,
                analysis.ImpactDistancePixels,
                analysis.FallingSpeed,
                out reason,
                analysis.GrappleHookSpeed);
        }


        internal static bool IsTemporaryApplyWindowReady(
            MovementSafeLandingEquipmentPlan plan,
            MovementSafeLandingAnalysis analysis,
            out string reason)
        {
            if (plan == null)
            {
                reason = "temporaryPlanUnavailable";
                return false;
            }

            var impactTicks = analysis == null ? plan.ImpactTicks : analysis.ImpactTicks;
            var impactDistancePixels = analysis == null ? plan.ImpactDistancePixels : analysis.ImpactDistancePixels;
            var fallingSpeed = analysis == null ? plan.FallingSpeed : analysis.FallingSpeed;
            return IsWindowReady(
                plan.StrategyId,
                plan.EquipmentCategory,
                plan.ActionType,
                plan.ApplyTriggersInput,
                TimingStage.TemporaryApply,
                impactTicks,
                impactDistancePixels,
                fallingSpeed,
                out reason);
        }

        internal static bool IsTemporaryActivationWindowReady(
            MovementSafeLandingEquipmentMoveRecord record,
            MovementSafeLandingAnalysis analysis,
            out string reason)
        {
            if (record == null)
            {
                reason = "activationRecordUnavailable";
                return false;
            }

            if (analysis == null)
            {
                reason = "analysisUnavailable";
                return false;
            }

            return IsWindowReady(
                record.StrategyId,
                record.EquipmentCategory,
                record.ActionType,
                IsTemporaryInputAction(record.ActionType),
                TimingStage.Activation,
                analysis.ImpactTicks,
                analysis.ImpactDistancePixels,
                analysis.FallingSpeed,
                out reason);
        }

        private static bool IsWindowReady(
            string strategy,
            string category,
            string actionType,
            bool applyTriggersInput,
            TimingStage stage,
            float impactTicks,
            int impactDistancePixels,
            float fallingSpeed,
            out string reason,
            float grappleHookSpeed = 0f)
        {
            reason = string.Empty;
            if (impactTicks < 0f || float.IsNaN(impactTicks) || float.IsInfinity(impactTicks))
            {
                reason = "impactTicksUnavailable";
                return false;
            }

            var window = ResolveWindow(strategy, category, actionType, applyTriggersInput, stage, fallingSpeed, grappleHookSpeed);
            if (impactTicks > window.MaxTicks)
            {
                reason = "impactTicksTooFar:" + Format(impactTicks) + ">" + Format(window.MaxTicks);
                return false;
            }

            if (window.MaxDistancePixels > 0)
            {
                if (impactDistancePixels < 0)
                {
                    reason = "impactDistanceUnavailable";
                    return false;
                }

                if (impactDistancePixels > window.MaxDistancePixels)
                {
                    reason = "impactDistanceTooFar:" +
                             impactDistancePixels.ToString(CultureInfo.InvariantCulture) +
                             ">" +
                             window.MaxDistancePixels.ToString(CultureInfo.InvariantCulture);
                    return false;
                }
            }

            return true;
        }

        private static TimingWindow ResolveWindow(
            string strategy,
            string category,
            string actionType,
            bool applyTriggersInput,
            TimingStage stage,
            float fallingSpeed,
            float grappleHookSpeed)
        {
            strategy = strategy ?? string.Empty;
            category = category ?? string.Empty;
            actionType = actionType ?? string.Empty;

            if (IsDoubleJump(strategy, category))
            {
                var activeDistance = ResolveDoubleJumpActivationDistancePixels(fallingSpeed);
                if (stage == TimingStage.TemporaryApply)
                {
                    return new TimingWindow(
                        DoubleJumpActivationWindowTicks + TemporaryActiveApplyLeadTicks,
                        Math.Min(DoubleJumpTemporaryApplyMaxPixels, AddLeadDistance(activeDistance, fallingSpeed, TemporaryActiveApplyLeadTicks)));
                }

                return new TimingWindow(DoubleJumpActivationWindowTicks, activeDistance);
            }

            if (IsRocketBoots(strategy, category))
            {
                if (stage == TimingStage.TemporaryApply)
                {
                    return new TimingWindow(
                        RocketBootsActivationWindowTicks + 2f,
                        RocketBootsTemporaryApplyMaxPixels);
                }

                return new TimingWindow(RocketBootsActivationWindowTicks, RocketBootsActivationMaxPixels);
            }

            if (IsFlyingCarpet(strategy, category))
            {
                if (stage == TimingStage.TemporaryApply)
                {
                    return new TimingWindow(
                        FlyingCarpetActivationWindowTicks + 3f,
                        FlyingCarpetTemporaryApplyMaxPixels);
                }

                return new TimingWindow(FlyingCarpetActivationWindowTicks, FlyingCarpetActivationMaxPixels);
            }

            if (IsGravityGlobe(strategy, category, actionType))
            {
                if (stage == TimingStage.TemporaryApply)
                {
                    return new TimingWindow(
                        GravityGlobeActivationWindowTicks + TemporaryActiveApplyLeadTicks,
                        GravityGlobeTemporaryApplyMaxPixels);
                }

                return new TimingWindow(GravityGlobeActivationWindowTicks, GravityGlobeActivationMaxPixels);
            }

            if (IsUmbrella(strategy, category))
            {
                return new TimingWindow(UmbrellaApplyWindowTicks, UmbrellaTemporaryApplyMaxPixels);
            }

            if (IsGrapple(strategy, category, actionType))
            {
                var adjMaxPx = (int)GrappleActivationMaxPixels;
                if (grappleHookSpeed > 0f && grappleHookSpeed < 100f)
                {
                    var maxFall = Math.Max(1f, fallingSpeed);
                    var ratio = maxFall / Math.Max(0.5f, grappleHookSpeed);
                    if (ratio > 1f)
                    {
                        adjMaxPx = (int)(GrappleActivationMaxPixels * ratio);
                        if (adjMaxPx > 480) adjMaxPx = 480;
                    }
                }
                return new TimingWindow(GrappleActivationWindowTicks, adjMaxPx);
            }

            if (IsTeleportRod(strategy, category, actionType))
            {
                return new TimingWindow(TeleportRodActivationWindowTicks, TeleportRodActivationMaxPixels);
            }

            if (IsQuickMountAction(actionType) || string.Equals(strategy, "active_flying_mount", StringComparison.OrdinalIgnoreCase))
            {
                if (stage == TimingStage.TemporaryApply)
                {
                    return new TimingWindow(
                        FlyingMountActivationWindowTicks + 2f,
                        FlyingMountTemporaryApplyMaxPixels);
                }

                return new TimingWindow(FlyingMountActivationWindowTicks, FlyingMountActivationMaxPixels);
            }

            if (stage == TimingStage.TemporaryApply && !applyTriggersInput)
            {
                return new TimingWindow(TemporaryPassiveApplyWindowTicks, TemporaryPassiveApplyMaxPixels);
            }

            return new TimingWindow(DefaultActivationWindowTicks, 0);
        }

        private static int ResolveDoubleJumpActivationDistancePixels(float fallingSpeed)
        {
            var speed = Math.Max(0f, fallingSpeed);
            var helperProbePixels = (int)Math.Ceiling(Math.Max(speed * 8f / 60f * 16f + 6f, 16f));
            var inputLeadPixels = (int)Math.Ceiling(speed * 3.5f);
            return ClampInt(helperProbePixels + inputLeadPixels, DoubleJumpActivationMinPixels, DoubleJumpActivationMaxPixels);
        }

        private static int AddLeadDistance(int baseDistance, float fallingSpeed, float leadTicks)
        {
            return baseDistance + (int)Math.Ceiling(Math.Max(0f, fallingSpeed) * leadTicks);
        }

        private static bool IsTemporaryInputAction(string actionType)
        {
            return string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase) ||
                   IsQuickMountAction(actionType) ||
                   IsGravityFlipAction(actionType);
        }

        private static bool IsDoubleJump(string strategy, string category)
        {
            return string.Equals(category, "double_jump", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "equipped_double_jump", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_double_jump", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRocketBoots(string strategy, string category)
        {
            return string.Equals(category, "rocket_boots", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "equipped_rocket_boots", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_rocket_boots", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFlyingCarpet(string strategy, string category)
        {
            return string.Equals(category, "flying_carpet", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "equipped_flying_carpet", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_flying_carpet", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGravityGlobe(string strategy, string category, string actionType)
        {
            return IsGravityFlipAction(actionType) ||
                   string.Equals(category, "gravity_globe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "equipped_gravity_globe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_gravity_globe", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUmbrella(string strategy, string category)
        {
            return string.Equals(category, "umbrella", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "temporary_umbrella", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGrapple(string strategy, string category, string actionType)
        {
            return string.Equals(actionType, "grapple", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(category, "grapple", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "equipped_grapple", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, "inventory_grapple", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTeleportRod(string strategy, string category, string actionType)
        {
            return string.Equals(actionType, MovementSafeLandingActionTypes.TeleportRod, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(category, MovementSafeLandingOptionCatalog.TeleportRod, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(strategy, MovementSafeLandingStrategyIds.InventoryTeleportRod, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQuickMountAction(string actionType)
        {
            return string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionType, "quickMount", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGravityFlipAction(string actionType)
        {
            return string.Equals(actionType, "gravity_flip", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionType, "gravityFlip", StringComparison.OrdinalIgnoreCase);
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private enum TimingStage
        {
            Activation,
            TemporaryApply
        }

        private struct TimingWindow
        {
            public readonly float MaxTicks;
            public readonly int MaxDistancePixels;

            public TimingWindow(float maxTicks, int maxDistancePixels)
            {
                MaxTicks = maxTicks;
                MaxDistancePixels = maxDistancePixels;
            }
        }
    }
}
