using System;
using JueMingZ.Automation.Movement;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class MovementSafeLandingCompat
    {
        // Analysis partial stays read-only: it gathers hazard/capability evidence
        // for the service and must not enqueue actions or write Terraria state.
        public static bool TryAnalyze(object player, AppSettings settings, out MovementSafeLandingAnalysis analysis)
        {
            return TryAnalyze(player, settings, null, out analysis);
        }

        internal static bool TryAnalyze(
            object player,
            AppSettings settings,
            MovementInputFrameCache.MovementInputFrame inputFrame,
            out MovementSafeLandingAnalysis analysis)
        {
            analysis = new MovementSafeLandingAnalysis();
            try
            {
                if (player == null)
                {
                    return Fail("Safe landing analysis failed: local player unavailable.");
                }

                JumpInputProfile jump;
                string jumpProfileError;
                if (!TryReadJumpInputProfile(player, inputFrame, out jump, out jumpProfileError))
                {
                    return Fail("Safe landing jump profile failed: " + jumpProfileError);
                }

                var basicMotion = inputFrame == null ? null : inputFrame.GetBasicMotion(out _);
                analysis.PlayerControllable = jump.PlayerControllable;
                analysis.VelocityY = jump.VelocityY;
                analysis.GravityDirection = Math.Abs(jump.GravityDirection) > 0.001f ? jump.GravityDirection : 1f;
                analysis.FallingSpeed = jump.VelocityY * analysis.GravityDirection;
                analysis.ControlDown = jump.ControlDown;
                analysis.HasAirJump = jump.HasAirJump;
                analysis.AirJumpFlagCount = jump.AirJumpFlagCount;
                var safeLandingRocketBootsAvailable = HasSafeLandingRocketBootsActivationOpportunity(jump);
                analysis.HasRocketJump = jump.HasRocketBootsAvailable || safeLandingRocketBootsAvailable;
                analysis.HasRocketBootsAvailable = jump.HasRocketBootsAvailable || safeLandingRocketBootsAvailable;
                analysis.RocketBoots = jump.RocketBoots;
                analysis.RocketTime = jump.RocketTime;
                analysis.RocketDelay = jump.RocketDelay;
                analysis.CanRocket = jump.CanRocket;
                analysis.CanRocketKnown = jump.CanRocketKnown;
                analysis.RocketRelease = jump.RocketRelease;
                analysis.CanUseBootFlyingAbilities = jump.CanUseBootFlyingAbilities;
                analysis.CanUseBootFlyingAbilitiesKnown = jump.CanUseBootFlyingAbilitiesKnown;
                analysis.AerialJumpWindow = jump.AerialJumpWindow;
                analysis.HasFlyingCarpet = jump.HasFlyingCarpet;
                analysis.HasFlyingCarpetAvailable = jump.HasFlyingCarpetAvailable;
                analysis.FlyingCarpetTime = jump.FlyingCarpetTime;
                analysis.HasGravityGlobe = jump.HasGravityGlobe;
                analysis.HasGravityFlipOpportunity = jump.HasGravityFlipOpportunity;
                analysis.HasWingFlight = jump.HasWingFlight;
                analysis.WingsLogic = jump.WingsLogic;
                analysis.WingTime = jump.WingTime;
                analysis.HasActiveFlyingMount = jump.HasMountOpportunity;
                analysis.HasEquippedFlyingMount = jump.HasEquippedFlyingMountOpportunity;
                analysis.HasEquippedSafeMount = jump.HasEquippedSafeMountOpportunity;
                analysis.HasGrapple = jump.HasAnyGrapple;
                analysis.HasEquippedGrapple = jump.HasEquippedGrapple;
                analysis.HasInventoryGrapple = jump.HasInventoryGrapple;
                analysis.EquippedFlyingMountItemType = jump.EquippedMountItemType;
                analysis.EquippedFlyingMountType = jump.EquippedMountType;
                analysis.EquippedSafeMountItemType = jump.EquippedMountItemType;
                analysis.EquippedSafeMountType = jump.EquippedMountType;
                analysis.EquippedGrappleItemType = jump.EquippedGrappleItemType;
                analysis.InventoryGrappleItemType = jump.InventoryGrappleItemType;
                analysis.EquippedGrappleShootSpeed = jump.EquippedGrappleShootSpeed;
                analysis.InventoryGrappleShootSpeed = jump.InventoryGrappleShootSpeed;
                analysis.EquippedGrappleProjectileType = jump.EquippedGrappleProjectileType;
                analysis.InventoryGrappleProjectileType = jump.InventoryGrappleProjectileType;
                PopulateTeleportRodCandidate(player, analysis);
                analysis.ActiveCapabilitySummary = MovementSafeLandingCapabilitySnapshot.FromAnalysis(analysis).BuildSummary();

                float positionX;
                float positionY;
                if (basicMotion != null && basicMotion.PositionAvailable)
                {
                    positionX = basicMotion.PositionX;
                    positionY = basicMotion.PositionY;
                }
                else if (!TryReadVectorMember(player, "position", out positionX, out positionY))
                {
                    return Fail("Safe landing analysis failed: cannot read player.position.");
                }

                float velocityX;
                float velocityY;
                if (basicMotion != null && basicMotion.VelocityAvailable)
                {
                    analysis.VelocityX = basicMotion.VelocityX;
                    analysis.VelocityY = basicMotion.VelocityY;
                    analysis.FallingSpeed = basicMotion.VelocityY * analysis.GravityDirection;
                }
                else if (TryReadVectorMember(player, "velocity", out velocityX, out velocityY))
                {
                    analysis.VelocityX = velocityX;
                    analysis.VelocityY = velocityY;
                    analysis.FallingSpeed = velocityY * analysis.GravityDirection;
                }

                analysis.PositionX = positionX;
                analysis.PositionY = positionY;
                analysis.Width = basicMotion != null && basicMotion.DimensionsAvailable
                    ? basicMotion.Width
                    : TryReadInt(player, "width", 20);
                analysis.Height = basicMotion != null && basicMotion.DimensionsAvailable
                    ? basicMotion.Height
                    : TryReadInt(player, "height", 42);
                analysis.FallStartKnown = TryReadInt(player, "fallStart", out var fallStart);
                analysis.FallStartTileY = fallStart;
                analysis.RawExtraFall = Math.Max(0, TryReadInt(player, "extraFall", 0));
                PopulateAlreadySafeRawState(player, analysis);
                if (analysis.FallStartKnown)
                {
                    var currentTileY = analysis.GravityDirection >= 0f
                        ? (analysis.PositionY + analysis.Height) / 16f
                        : analysis.PositionY / 16f;
                    analysis.EstimatedFallTiles = analysis.GravityDirection >= 0f
                        ? currentTileY - fallStart
                        : fallStart - currentTileY;
                }

                analysis.AlreadySafe = TryResolveAlreadySafe(player, jump, analysis, out var safeReason);
                analysis.SafeReason = safeReason;
                if (analysis.AlreadySafe)
                {
                    analysis.Dangerous = false;
                    analysis.RescueWindow = false;
                    analysis.SelectedPriority = 0;
                    analysis.SelectedStrategyId = "already_safe";
                    analysis.SkipReason = "alreadySafe:" + safeReason;
                    return ClearError();
                }

                if (!analysis.PlayerControllable)
                {
                    analysis.SkipReason = "playerNotControllable";
                    return ClearError();
                }

                if (analysis.FallingSpeed < MinimumDangerFallingSpeed)
                {
                    analysis.SkipReason = "notFallingFastEnough";
                    return ClearError();
                }

                var safeFallTiles = MinimumFallTiles + analysis.RawExtraFall;
                var fallDistanceCandidate = analysis.FallStartKnown
                    ? analysis.EstimatedFallTiles >= safeFallTiles
                    : analysis.FallingSpeed >= SevereDangerFallingSpeed;
                if (!fallDistanceCandidate && analysis.FallingSpeed < SevereDangerFallingSpeed)
                {
                    analysis.SkipReason = "fallDistanceBelowThreshold";
                    return ClearError();
                }

                MovementLandingSurfaceHit landingHit;
                analysis.ImpactFound = TryFindLandingSurface(analysis, out landingHit);
                var impactDistance = analysis.ImpactFound && landingHit != null && landingHit.Found
                    ? landingHit.ImpactDistancePixels
                    : -1;
                analysis.ImpactDistancePixels = impactDistance;
                if (analysis.ImpactFound && landingHit != null && landingHit.Found)
                {
                    analysis.ImpactWorldX = (landingHit.ProjectedPlayerLeftX + landingHit.ProjectedPlayerRightX) / 2f;
                    analysis.ImpactWorldY = landingHit.ProjectedPlayerBottomY;
                    ApplyLandingSurfaceHit(analysis, landingHit);
                }

                analysis.GrappleHookSpeed = ResolveGrappleHookSpeed(analysis);

                if (TryResolveProjectedSafeLanding(analysis, analysis.ImpactFound ? impactDistance : -1, out var projectedSafeReason))
                {
                    analysis.AlreadySafe = true;
                    analysis.SafeReason = projectedSafeReason;
                    analysis.Dangerous = false;
                    analysis.RescueWindow = false;
                    analysis.SelectedPriority = 0;
                    analysis.SelectedStrategyId = "already_safe";
                    analysis.SkipReason = "alreadySafe:" + projectedSafeReason;
                    return ClearError();
                }

                if (!analysis.ImpactFound)
                {
                    analysis.SkipReason = "noImpactWithinProbe";
                    return ClearError();
                }

                analysis.ImpactTicks = analysis.FallingSpeed <= 0.001f
                    ? -1f
                    : impactDistance / analysis.FallingSpeed;

                var fallDistanceEnough = fallDistanceCandidate;
                analysis.Dangerous = fallDistanceEnough &&
                                     analysis.ImpactTicks >= 0f &&
                                     analysis.ImpactTicks <= EarlyDangerWindowTicks;

                if (!analysis.Dangerous)
                {
                    analysis.SkipReason = !fallDistanceEnough ? "fallDistanceBelowThreshold" : "impactTooFar";
                    return ClearError();
                }

                ResolvePriorityOneStrategy(analysis, settings);

                if (string.IsNullOrWhiteSpace(analysis.SelectedStrategyId))
                {
                    analysis.SkipReason = "noImplementedSafeLandingStrategy";
                    return ClearError();
                }

                string rescueWindowReason;
                analysis.RescueWindow = MovementSafeLandingTiming.IsEquippedRescueWindowReady(analysis, out rescueWindowReason);
                if (!analysis.RescueWindow)
                {
                    analysis.SkipReason = "waitingForRescueWindow:" + analysis.SelectedStrategyId + ":" + rescueWindowReason;
                    return ClearError();
                }

                return ClearError();
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingCompat.TryAnalyze", error);
                return Fail("Safe landing analysis exception: " + error.GetType().Name + ": " + error.Message);
            }
        }

        internal static bool TryCheapDangerPrecheck(
            object player,
            out MovementSafeLandingAnalysis analysis,
            out bool shouldRunFullAnalysis)
        {
            return TryCheapDangerPrecheck(player, null, out analysis, out shouldRunFullAnalysis);
        }

        internal static bool TryCheapDangerPrecheck(
            object player,
            MovementInputFrameCache.MovementInputFrame inputFrame,
            out MovementSafeLandingAnalysis analysis,
            out bool shouldRunFullAnalysis)
        {
            analysis = new MovementSafeLandingAnalysis();
            shouldRunFullAnalysis = true;
            try
            {
                if (player == null)
                {
                    return Fail("Safe landing cheap precheck failed: local player unavailable.");
                }

                var basicMotion = inputFrame == null ? null : inputFrame.GetBasicMotion(out _);
                if (basicMotion != null)
                {
                    if (!basicMotion.PlayerStateAvailable)
                    {
                        analysis.SkipReason = "cheapPrecheckUnavailable:playerState";
                        return ClearError();
                    }

                    analysis.PlayerControllable = basicMotion.PlayerControllable;
                    if (!analysis.PlayerControllable)
                    {
                        analysis.SkipReason = "playerNotControllable:cheap";
                        shouldRunFullAnalysis = false;
                        return ClearError();
                    }

                    if (!basicMotion.GravityDirectionAvailable)
                    {
                        analysis.SkipReason = "cheapPrecheckUnavailable:gravDir";
                        return ClearError();
                    }

                    if (!basicMotion.VelocityAvailable)
                    {
                        analysis.SkipReason = "cheapPrecheckUnavailable:velocity";
                        return ClearError();
                    }

                    analysis.GravityDirection = basicMotion.GravityDirection;
                    analysis.VelocityX = basicMotion.VelocityX;
                    analysis.VelocityY = basicMotion.VelocityY;
                    analysis.FallingSpeed = basicMotion.VelocityY * analysis.GravityDirection;
                    if (analysis.FallingSpeed < MinimumDangerFallingSpeed)
                    {
                        analysis.SkipReason = "notFallingFastEnough:cheap";
                        shouldRunFullAnalysis = false;
                        return ClearError();
                    }

                    analysis.SkipReason = "cheapPrecheckPassed";
                    return ClearError();
                }

                bool playerActive;
                bool playerDead;
                bool playerGhost;
                bool playerCrowdControlled;
                if (!TryReadBool(player, "active", out playerActive) ||
                    !TryReadBool(player, "dead", out playerDead) ||
                    !TryReadBool(player, "ghost", out playerGhost) ||
                    !TryReadBool(player, "CCed", out playerCrowdControlled))
                {
                    analysis.SkipReason = "cheapPrecheckUnavailable:playerState";
                    return ClearError();
                }

                analysis.PlayerControllable = playerActive && !playerDead && !playerGhost && !playerCrowdControlled;
                if (!analysis.PlayerControllable)
                {
                    analysis.SkipReason = "playerNotControllable:cheap";
                    shouldRunFullAnalysis = false;
                    return ClearError();
                }

                float gravityDirection;
                if (!TryReadFloat(player, "gravDir", out gravityDirection))
                {
                    analysis.SkipReason = "cheapPrecheckUnavailable:gravDir";
                    return ClearError();
                }

                float velocityX;
                float velocityY;
                if (!TryReadVectorMember(player, "velocity", out velocityX, out velocityY))
                {
                    analysis.SkipReason = "cheapPrecheckUnavailable:velocity";
                    return ClearError();
                }

                analysis.GravityDirection = Math.Abs(gravityDirection) > 0.001f ? gravityDirection : 1f;
                analysis.VelocityX = velocityX;
                analysis.VelocityY = velocityY;
                analysis.FallingSpeed = velocityY * analysis.GravityDirection;
                if (analysis.FallingSpeed < MinimumDangerFallingSpeed)
                {
                    analysis.SkipReason = "notFallingFastEnough:cheap";
                    shouldRunFullAnalysis = false;
                    return ClearError();
                }

                analysis.SkipReason = "cheapPrecheckPassed";
                return ClearError();
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingCompat.TryCheapDangerPrecheck", error);
                analysis.SkipReason = "cheapPrecheckException:" + error.GetType().Name;
                return Fail("Safe landing cheap precheck exception: " + error.GetType().Name + ": " + error.Message);
            }
        }

        private static void ResolvePriorityOneStrategy(MovementSafeLandingAnalysis analysis, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            if (analysis.HasAirJump && MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.DoubleJump))
            {
                analysis.SelectedPriority = 1;
                analysis.SelectedStrategyId = "equipped_double_jump";
                analysis.SelectedActionType = "jump";
                return;
            }

            if (analysis.HasRocketJump && MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.RocketBoots))
            {
                analysis.SelectedPriority = 1;
                analysis.SelectedStrategyId = "equipped_rocket_boots";
                analysis.SelectedActionType = "jump";
                return;
            }

            if (analysis.HasFlyingCarpetAvailable && MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.FlyingCarpet))
            {
                analysis.SelectedPriority = 1;
                analysis.SelectedStrategyId = "equipped_flying_carpet";
                analysis.SelectedActionType = "jump";
                return;
            }

            if (analysis.HasGravityFlipOpportunity && MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.GravityGlobe))
            {
                analysis.SelectedPriority = 1;
                analysis.SelectedStrategyId = "equipped_gravity_globe";
                analysis.SelectedActionType = "gravity_flip";
                return;
            }

            if (analysis.HasActiveFlyingMount && MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.FlyingMount))
            {
                analysis.SelectedPriority = 1;
                analysis.SelectedStrategyId = "active_flying_mount";
                analysis.SelectedActionType = "jump";
                return;
            }

            if (analysis.HasEquippedFlyingMount && MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.FlyingMount))
            {
                analysis.SelectedPriority = 1;
                analysis.SelectedStrategyId = "equipped_flying_mount";
                analysis.SelectedActionType = "quick_mount";
                return;
            }

            if (analysis.HasEquippedSafeMount && MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.DamageReductionMount))
            {
                analysis.SelectedPriority = 1;
                analysis.SelectedStrategyId = "equipped_safe_mount";
                analysis.SelectedActionType = "quick_mount";
                return;
            }

            // Equipped wings are treated as priority 0 because Terraria already suppresses fall damage.
            // Wings in inventory/social slots are handled by the temporary equipment layer instead.
        }

        internal static bool HasSafeLandingRocketBootsActivationOpportunity(JumpInputProfile profile)
        {
            return profile != null &&
                   profile.PlayerControllable &&
                   profile.AerialJumpWindow &&
                   profile.CanUseBootFlyingAbilities &&
                   profile.RocketBoots > 0 &&
                   profile.RocketTime > 0f &&
                   profile.RocketDelay <= 0 &&
                   profile.CanRocket;
        }

        internal static bool HasSafeLandingRocketBootsStartOpportunity(JumpInputProfile profile)
        {
            return HasSafeLandingRocketBootsActivationOpportunity(profile);
        }

        public static bool TryResolveLandingImpactDistanceForProfile(
            object player,
            JumpInputProfile profile,
            out int impactDistancePixels,
            out float impactTicks,
            out string reason)
        {
            impactDistancePixels = -1;
            impactTicks = -1f;
            reason = string.Empty;
            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            if (profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            var gravityDirection = Math.Abs(profile.GravityDirection) > 0.001f ? profile.GravityDirection : 1f;
            var fallingSpeed = profile.VelocityY * gravityDirection;
            if (fallingSpeed <= 0.05f)
            {
                reason = "notFalling";
                return true;
            }

            float positionX;
            float positionY;
            if (!TryReadVectorMember(player, "position", out positionX, out positionY))
            {
                reason = "positionUnavailable";
                return false;
            }

            var probeAnalysis = new MovementSafeLandingAnalysis
            {
                PositionX = positionX,
                PositionY = positionY,
                Width = TryReadInt(player, "width", 20),
                Height = TryReadInt(player, "height", 42),
                GravityDirection = gravityDirection,
                FallingSpeed = Math.Max(MinimumDangerFallingSpeed, Math.Abs(fallingSpeed))
            };
            float velocityX;
            float velocityY;
            if (TryReadVectorMember(player, "velocity", out velocityX, out velocityY))
            {
                probeAnalysis.VelocityX = velocityX;
            }

            if (!TryFindImpactDistance(probeAnalysis, out impactDistancePixels))
            {
                reason = "noImpactWithinProbe";
                impactDistancePixels = -1;
                impactTicks = -1f;
                return true;
            }

            impactTicks = impactDistancePixels / Math.Max(0.001f, Math.Abs(fallingSpeed));
            reason = "impactDistanceResolved";
            return true;
        }

        internal static float ProjectImpactProbeXForTesting(float positionX, float velocityX, float fallingSpeed, int offsetPixels)
        {
            return ResolveProjectedImpactProbeX(positionX, velocityX, fallingSpeed, offsetPixels);
        }

        internal static bool TryFindLandingSurfaceForTesting(MovementSafeLandingAnalysis analysis, out MovementLandingSurfaceHit hit)
        {
            return TryFindLandingSurface(analysis, out hit);
        }
    }
}

