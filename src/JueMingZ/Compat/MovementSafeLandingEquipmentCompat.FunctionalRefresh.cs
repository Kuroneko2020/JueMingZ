using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using JueMingZ.Automation.Movement;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
        private static bool _applyEquipFunctionalResolved;
        private static MethodInfo _applyEquipFunctionalMethod;
        private static bool _refreshDoubleJumpsResolved;
        private static MethodInfo _refreshDoubleJumpsMethod;

        private static void ApplyFunctionalRefreshForTemporaryEquipment(
            object player,
            MovementSafeLandingEquipmentPlan plan,
            object equippedItem,
            MovementSafeLandingEquipmentActionResult result)
        {
            if (result == null || plan == null)
            {
                return;
            }

            var messages = new List<string>();
            if (plan.TargetContainerKind == MovementSafeLandingEquipmentContainerKind.Accessory)
            {
                result.FunctionalRefreshAttempted = true;
                string functionalMessage;
                result.FunctionalRefreshSucceeded = TryInvokeApplyEquipFunctional(player, plan.TargetSlot, equippedItem, out functionalMessage);
                messages.Add(functionalMessage);
            }
            else
            {
                messages.Add("functionalRefreshSkipped:targetKind=" + ContainerKindName(plan.TargetContainerKind));
            }

            if (string.Equals(plan.EquipmentCategory, "double_jump", StringComparison.OrdinalIgnoreCase))
            {
                result.DoubleJumpRefreshAttempted = true;
                string doubleJumpMessage;
                result.DoubleJumpRefreshSucceeded = TryInvokeRefreshDoubleJumps(player, out doubleJumpMessage);
                messages.Add(doubleJumpMessage);
            }

            if (string.Equals(plan.EquipmentCategory, "rocket_boots", StringComparison.OrdinalIgnoreCase))
            {
                string rocketBootsMessage;
                var rocketBootsPrimed = TryPrimeTemporaryRocketBootsFlightTime(player, out rocketBootsMessage);
                messages.Add((rocketBootsPrimed ? "rocketBootsTimerPrimeSucceeded:" : "rocketBootsTimerPrimeSkipped:") + rocketBootsMessage);
            }

            if (string.Equals(plan.EquipmentCategory, "flying_carpet", StringComparison.OrdinalIgnoreCase))
            {
                string carpetMessage;
                var carpetPrimed = TryPrimeTemporaryFlyingCarpet(player, out carpetMessage);
                messages.Add((carpetPrimed ? "flyingCarpetPrimeSucceeded:" : "flyingCarpetPrimeSkipped:") + carpetMessage);
            }

            if (string.Equals(plan.EquipmentCategory, "gravity_globe", StringComparison.OrdinalIgnoreCase))
            {
                string gravityMessage;
                var gravityPrimed = TryPrimeTemporaryGravityGlobe(player, out gravityMessage);
                messages.Add((gravityPrimed ? "gravityGlobePrimeSucceeded:" : "gravityGlobePrimeSkipped:") + gravityMessage);
            }

            result.FunctionalRefreshMessage = string.Join(";", messages.ToArray());
        }

        private static bool TryPrimeTemporaryRocketBootsFlightTime(object player, out string message)
        {
            message = string.Empty;
            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                message = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.PlayerControllable || !profile.AerialJumpWindow)
            {
                message = "notInAerialActivationWindow";
                return false;
            }

            if (profile.RocketBoots <= 0)
            {
                message = "rocketBootsUnavailable";
                return false;
            }

            if (profile.CanUseBootFlyingAbilitiesKnown && !profile.CanUseBootFlyingAbilities)
            {
                message = "canUseBootFlyingAbilitiesFalse";
                return false;
            }

            if (profile.RocketDelay > 0)
            {
                message = "rocketDelayActive";
                return false;
            }

            if (!profile.CanRocket)
            {
                message = "canRocketFalse";
                return false;
            }

            if (profile.RocketTime > 0f)
            {
                message = "rocketTimeAlreadyAvailable:" + profile.RocketTime.ToString("0.###", CultureInfo.InvariantCulture);
                return true;
            }

            if (!TrySetMember(player, "rocketTime", TemporaryRocketBootsPrimeRocketTime))
            {
                message = "rocketTimeSetFailed";
                return false;
            }

            message = "controlledLocalRocketTimePrime:" + TemporaryRocketBootsPrimeRocketTime.ToString("0.###", CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryPrimeTemporaryFlyingCarpet(object player, out string message)
        {
            message = string.Empty;
            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                message = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.PlayerControllable || !profile.AerialJumpWindow)
            {
                message = "notInAerialActivationWindow";
                return false;
            }

            var ok = true;
            if (!profile.HasFlyingCarpet)
            {
                ok &= TrySetMember(player, "carpet", true);
            }

            if (!profile.FlyingCarpetCanStart)
            {
                ok &= TrySetMember(player, "canCarpet", true);
            }

            if (profile.FlyingCarpetTime <= 0)
            {
                ok &= TrySetMember(player, "carpetTime", TemporaryFlyingCarpetPrimeTime);
            }

            message = ok
                ? "controlledLocalFlyingCarpetPrime:" + TemporaryFlyingCarpetPrimeTime.ToString(CultureInfo.InvariantCulture)
                : "flyingCarpetPrimeSetFailed";
            return ok;
        }

        private static bool TryPrimeTemporaryGravityGlobe(object player, out string message)
        {
            message = string.Empty;
            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                message = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.PlayerControllable || !profile.AerialJumpWindow)
            {
                message = "notInAerialActivationWindow";
                return false;
            }

            if (profile.HasGravityGlobe)
            {
                message = "gravityGlobeAlreadyAvailable";
                return true;
            }

            if (!TrySetMember(player, "gravControl2", true))
            {
                message = "gravityGlobeSetFailed";
                return false;
            }

            message = "controlledLocalGravityGlobePrime";
            return true;
        }

        private static void VerifyPostApplyCapability(
            object player,
            MovementSafeLandingEquipmentPlan plan,
            MovementSafeLandingEquipmentActionResult result)
        {
            if (result == null || plan == null)
            {
                return;
            }

            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                result.PostApplyVerificationReason = "jumpProfileUnavailable";
                result.PostApplyVerificationSummary = "postApplyVerification=unavailable,reason=jumpProfileUnavailable";
                return;
            }

            var snapshot = MovementSafeLandingCapabilitySnapshot.FromJumpProfile(profile);
            result.PostApplyRocketBoots = snapshot.RocketBoots;
            result.PostApplyRocketTime = snapshot.RocketTime;
            result.PostApplyRocketDelay = snapshot.RocketDelay;
            result.PostApplyCanRocket = snapshot.CanRocket;
            result.PostApplyCanRocketKnown = snapshot.CanRocketKnown;
            result.PostApplyRocketRelease = snapshot.RocketRelease;
            result.PostApplyCanUseBootFlyingAbilities = snapshot.CanUseBootFlyingAbilities;
            result.PostApplyCanUseBootFlyingAbilitiesKnown = snapshot.CanUseBootFlyingAbilitiesKnown;
            result.PostApplyHasRocketBootsAvailable = snapshot.HasRocketBootsAvailable;
            result.PostApplyHasFlyingCarpet = snapshot.HasFlyingCarpet;
            result.PostApplyHasFlyingCarpetAvailable = snapshot.HasFlyingCarpetAvailable;
            result.PostApplyFlyingCarpetCanStart = profile.FlyingCarpetCanStart;
            result.PostApplyFlyingCarpetTime = snapshot.FlyingCarpetTime;
            result.PostApplyAirJumpFlagCount = snapshot.AirJumpFlagCount;
            result.PostApplyHasGravityGlobe = snapshot.HasGravityGlobe;
            result.PostApplyHasGravityFlipOpportunity = snapshot.HasGravityFlipOpportunity;
            result.PostApplyAerialJumpWindow = profile.AerialJumpWindow;
            result.PostApplyGravityDirection = snapshot.GravityDirection;
            result.PostApplyHasWingFlight = snapshot.HasWingFlight;
            result.PostApplyWingsLogic = snapshot.WingsLogic;
            result.PostApplyWingTime = snapshot.WingTime;
            result.PostApplyVerificationReason = ResolvePostApplyVerificationReason(plan, snapshot, profile);

            var builder = new StringBuilder();
            AppendPart(builder, "category=" + (plan.EquipmentCategory ?? string.Empty));
            AppendPart(builder, "reason=" + result.PostApplyVerificationReason);
            AppendPart(builder, "applyEquipFunctionalAttempted=" + Bool(result.FunctionalRefreshAttempted));
            AppendPart(builder, "applyEquipFunctionalSucceeded=" + Bool(result.FunctionalRefreshSucceeded));
            AppendPart(builder, "functionalMessage=" + (result.FunctionalRefreshMessage ?? string.Empty));
            AppendPart(builder, "doubleJumpRefreshAttempted=" + Bool(result.DoubleJumpRefreshAttempted));
            AppendPart(builder, "doubleJumpRefreshSucceeded=" + Bool(result.DoubleJumpRefreshSucceeded));
            AppendPart(builder, "airJumpFlagCount=" + snapshot.AirJumpFlagCount.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "rocketBoots=" + snapshot.RocketBoots.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "rocketTime=" + snapshot.RocketTime.ToString("0.###", CultureInfo.InvariantCulture));
            AppendPart(builder, "rocketDelay=" + snapshot.RocketDelay.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "canRocket=" + Bool(snapshot.CanRocket));
            AppendPart(builder, "canRocketKnown=" + Bool(snapshot.CanRocketKnown));
            AppendPart(builder, "rocketRelease=" + Bool(snapshot.RocketRelease));
            AppendPart(builder, "canUseBootFlyingAbilities=" + Bool(snapshot.CanUseBootFlyingAbilities));
            AppendPart(builder, "canUseBootFlyingAbilitiesKnown=" + Bool(snapshot.CanUseBootFlyingAbilitiesKnown));
            AppendPart(builder, "hasRocketBootsAvailable=" + Bool(snapshot.HasRocketBootsAvailable));
            AppendPart(builder, "hasFlyingCarpet=" + Bool(snapshot.HasFlyingCarpet));
            AppendPart(builder, "hasFlyingCarpetAvailable=" + Bool(snapshot.HasFlyingCarpetAvailable));
            AppendPart(builder, "flyingCarpetCanStart=" + Bool(profile.FlyingCarpetCanStart));
            AppendPart(builder, "flyingCarpetTime=" + snapshot.FlyingCarpetTime.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "hasGravityGlobe=" + Bool(snapshot.HasGravityGlobe));
            AppendPart(builder, "hasGravityFlipOpportunity=" + Bool(snapshot.HasGravityFlipOpportunity));
            AppendPart(builder, "aerialJumpWindow=" + Bool(profile.AerialJumpWindow));
            AppendPart(builder, "gravityDirection=" + snapshot.GravityDirection.ToString("0.###", CultureInfo.InvariantCulture));
            AppendPart(builder, "hasWingFlight=" + Bool(snapshot.HasWingFlight));
            AppendPart(builder, "wingsLogic=" + snapshot.WingsLogic.ToString(CultureInfo.InvariantCulture));
            AppendPart(builder, "wingTime=" + snapshot.WingTime.ToString("0.###", CultureInfo.InvariantCulture));
            result.PostApplyVerificationSummary = builder.ToString();
        }

        private static string ResolvePostApplyVerificationReason(
            MovementSafeLandingEquipmentPlan plan,
            MovementSafeLandingCapabilitySnapshot snapshot,
            JumpInputProfile profile)
        {
            // Verification reasons describe observed capability after apply,
            // not proof that a guessed equipment write succeeded.
            var category = plan == null ? string.Empty : plan.EquipmentCategory ?? string.Empty;
            if (string.Equals(category, "rocket_boots", StringComparison.OrdinalIgnoreCase))
            {
                if (snapshot.HasRocketBootsAvailable)
                {
                    return "rocketBootsAvailable";
                }

                if (snapshot.RocketBoots <= 0)
                {
                    return "rocketBootsUnavailableAfterApply";
                }

                if (snapshot.RocketTime <= 0f)
                {
                    return "rocketTimeUnavailableAfterApply";
                }

                if (snapshot.RocketDelay > 0)
                {
                    return "rocketDelayActive";
                }

                if (!snapshot.CanRocket)
                {
                    return "canRocketFalse";
                }

                if (snapshot.RocketRelease)
                {
                    return "rocketReleaseNotPrimed";
                }

                if (snapshot.CanUseBootFlyingAbilitiesKnown && !snapshot.CanUseBootFlyingAbilities)
                {
                    return "canUseBootFlyingAbilitiesFalse";
                }

                return "rocketBootsUnavailableAfterApply";
            }

            if (string.Equals(category, "gravity_globe", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasGravityFlipOpportunity
                    ? "gravityRestorePendingExpected"
                    : snapshot.HasGravityGlobe ? "gravityGlobePresentButNoFlipOpportunity" : "gravityGlobeUnavailableAfterApply";
            }

            if (string.Equals(category, "flying_carpet", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasFlyingCarpetAvailable
                    ? "flyingCarpetAvailableAfterApply"
                    : snapshot.HasFlyingCarpet ? "flyingCarpetPresentButNoStartOpportunity" : "flyingCarpetUnavailableAfterApply";
            }

            if (string.Equals(category, "double_jump", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasAirJump ? "doubleJumpAvailableAfterRefresh" : "doubleJumpUnavailableAfterRefresh";
            }

            if (string.Equals(category, "wings", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasWingFlight ? "wingFlightAvailableAfterApply" : "wingFlightUnavailableAfterApply";
            }

            if (string.Equals(category, "fairy_boots", StringComparison.OrdinalIgnoreCase))
            {
                return snapshot.HasWingFlight || (profile != null && profile.HasAirJump)
                    ? "fairyBootsCapabilityObserved"
                    : "fairyBootsCapabilityNotObserved";
            }

            if (string.Equals(category, "horseshoe", StringComparison.OrdinalIgnoreCase))
            {
                return "horseshoeApplyVerifiedByAlreadySafeProbe";
            }

            if (string.Equals(category, "umbrella", StringComparison.OrdinalIgnoreCase))
            {
                return "umbrellaHotbarSelectionVerified";
            }

            return "postApplyCapabilitySnapshotCaptured";
        }

        private static bool IsVerifiedTemporaryActivationCapability(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            return string.Equals(reason, "doubleJumpAvailableAfterRefresh", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "rocketBootsAvailable", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "flyingCarpetAvailableAfterApply", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "gravityRestorePendingExpected", StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendPart(StringBuilder builder, string value)
        {
            if (builder == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(",");
            }

            builder.Append(value);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static bool TryInvokeApplyEquipFunctional(object player, int slot, object item, out string message)
        {
            message = string.Empty;
            if (player == null || item == null)
            {
                message = "applyEquipFunctionalSkipped:playerOrItemUnavailable";
                return false;
            }

            var method = ResolveApplyEquipFunctionalMethod(player.GetType(), item.GetType());
            if (method == null)
            {
                message = "applyEquipFunctionalUnavailable";
                return false;
            }

            try
            {
                method.Invoke(player, new[] { (object)slot, item });
                message = "applyEquipFunctionalInvoked";
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.ApplyEquipFunctional", error);
                message = "applyEquipFunctionalFailed:" + error.GetType().Name;
                return false;
            }
        }

        private static MethodInfo ResolveApplyEquipFunctionalMethod(Type playerType, Type itemType)
        {
            if (_applyEquipFunctionalResolved)
            {
                return _applyEquipFunctionalMethod;
            }

            _applyEquipFunctionalResolved = true;
            if (playerType == null || itemType == null)
            {
                return null;
            }

            try
            {
                var methods = playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (method == null || !string.Equals(method.Name, "ApplyEquipFunctional", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != 2 ||
                        parameters[0].ParameterType != typeof(int) ||
                        !parameters[1].ParameterType.IsAssignableFrom(itemType))
                    {
                        continue;
                    }

                    _applyEquipFunctionalMethod = method;
                    return _applyEquipFunctionalMethod;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.ResolveApplyEquipFunctional", error);
            }

            return null;
        }

        private static bool TryInvokeRefreshDoubleJumps(object player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "refreshDoubleJumpsSkipped:playerUnavailable";
                return false;
            }

            var method = ResolveRefreshDoubleJumpsMethod(player.GetType());
            if (method == null)
            {
                message = "refreshDoubleJumpsUnavailable";
                return false;
            }

            try
            {
                method.Invoke(player, null);
                message = "refreshDoubleJumpsInvoked";
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.RefreshDoubleJumps", error);
                message = "refreshDoubleJumpsFailed:" + error.GetType().Name;
                return false;
            }
        }

        private static MethodInfo ResolveRefreshDoubleJumpsMethod(Type playerType)
        {
            if (_refreshDoubleJumpsResolved)
            {
                return _refreshDoubleJumpsMethod;
            }

            _refreshDoubleJumpsResolved = true;
            if (playerType == null)
            {
                return null;
            }

            try
            {
                _refreshDoubleJumpsMethod = playerType.GetMethod(
                    "RefreshDoubleJumps",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingEquipmentCompat.ResolveRefreshDoubleJumps", error);
            }

            return _refreshDoubleJumpsMethod;
        }

    }
}
