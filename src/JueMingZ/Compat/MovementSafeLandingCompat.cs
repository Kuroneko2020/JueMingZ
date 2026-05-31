using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JueMingZ.Automation.Movement;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class MovementSafeLandingCompat
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const float MinimumDangerFallingSpeed = 6.25f;
        private const float SevereDangerFallingSpeed = 10.5f;
        private const float EarlyDangerWindowTicks = 18f;
        private const float ImpactHorizontalLeadMaxTicks = EarlyDangerWindowTicks;
        private const float ImpactHorizontalSampleEpsilon = 0.5f;
        private const float MinimumFallTiles = 22f;
        private const int GravityRestoreProbePixels = 96;
        private const int PlatformTileType = 19;
        private const int CobwebTileFallbackType = 51;
        private const int CloudTileType = 189;
        private const int RainCloudTileType = 196;
        private const int PinkSlimeBlockTileType = 371;
        private const int SillyBalloonPinkTileType = 446;
        private const int SillyBalloonPurpleTileType = 447;
        private const int SillyBalloonGreenTileType = 448;
        private const int SnowCloudTileType = 460;
        private const int PoopBlockTileType = 666;
        private const int CobwebReplicaTileType = 697;
        private const int LavaCloudTileType = 717;
        private const int StarCloudTileType = 718;
        private const int RainbowCloudTileType = 719;
        private const int CloudPlatformStyle = 49;
        private const int PlatformFrameHeight = 18;
        private const int LavaLiquidType = 1;
        private const float ImpactCollisionProbeVelocity = 4f;
        private static bool _collisionMethodResolved;
        private static MethodInfo _solidCollisionMethod;
        private static bool _solidCollisionTopSurfaceMethodResolved;
        private static MethodInfo _solidCollisionTopSurfaceMethod;
        private static bool _tileCollisionMethodResolved;
        private static MethodInfo _tileCollisionMethod;
        private static bool _slotUsabilityMethodResolved;
        private static MethodInfo _slotUsabilityMethod;
        private static string _lastError = string.Empty;
        private static readonly object PulseSyncRoot = new object();
        private static SafeLandingJumpPulseState _queuedJumpPulse;
        private static bool _playerUpdateHookInstalled;
        private static string _playerUpdateHookMessage = string.Empty;
        // GrappleHookSpeedTable: itemType -> shootSpeed (px/tick) from Terraria 1.4.5.6
        // Fallback lookup: itemType -> shootSpeed (px/tick).
        // Only used when actual item.shootSpeed cannot be read from the live Item instance.
        // Values verified against Terraria 1.4.5.6 vanilla ItemID constants and vanilla shootSpeed.
        private static readonly Dictionary<int, float> GrappleHookSpeedTable = new Dictionary<int, float>
        {
            {84, 11.5f},     // Grappling Hook (vanilla ItemID 84)
            {185, 13f},      // Ivy Whip
            {437, 14f},      // Dual Hook
            {939, 10f},      // Web Slinger
            {1236, 10f},     // Amethyst Hook
            {1237, 10.5f},   // Topaz Hook
            {1238, 11f},     // Sapphire Hook
            {1239, 11.5f},   // Emerald Hook
            {1240, 12f},     // Ruby Hook
            {1241, 12.5f},   // Diamond Hook
            {1273, 8f},      // Skeletron Hand
            {2360, 13f},     // Fish Hook
            {2585, 13f},     // Slime Hook
            {2800, 14f},     // Anti-Gravity Hook
            {3572, 18f},     // Lunar Hook
            {3623, 16f},     // Static Hook
            {4759, 11.5f},   // Squirrel Hook
            // Hook of Dissonance / Queen Slime Hook (4980) uses a unique teleport mechanic;
            // its shootSpeed is not a standard hook speed and is excluded from this table.
        };

        public static string LastError
        {
            get { return _lastError; }
        }

        public static bool PlayerUpdateHookInstalled
        {
            get { return _playerUpdateHookInstalled; }
        }

        public static string PlayerUpdateHookMessage
        {
            get { return _playerUpdateHookMessage ?? string.Empty; }
        }

        public static void MarkPlayerUpdateHookResult(bool installed, string message)
        {
            _playerUpdateHookInstalled = installed;
            _playerUpdateHookMessage = message ?? string.Empty;
        }

        public static bool TryAnalyze(object player, AppSettings settings, out MovementSafeLandingAnalysis analysis)
        {
            analysis = new MovementSafeLandingAnalysis();
            try
            {
                if (player == null)
                {
                    return Fail("Safe landing analysis failed: local player unavailable.");
                }

                JumpInputProfile jump;
                if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out jump))
                {
                    return Fail("Safe landing jump profile failed: " + TerrariaInputCompat.LastInputCompatError);
                }

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
                if (!TryReadVectorMember(player, "position", out positionX, out positionY))
                {
                    return Fail("Safe landing analysis failed: cannot read player.position.");
                }

                float velocityX;
                float velocityY;
                if (TryReadVectorMember(player, "velocity", out velocityX, out velocityY))
                {
                    analysis.VelocityX = velocityX;
                    analysis.VelocityY = velocityY;
                    analysis.FallingSpeed = velocityY * analysis.GravityDirection;
                }

                analysis.PositionX = positionX;
                analysis.PositionY = positionY;
                analysis.Width = TryReadInt(player, "width", 20);
                analysis.Height = TryReadInt(player, "height", 42);
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

        public static bool QueueSafeLandingJumpPulse(Guid requestId, string strategy, string actionType, bool applyRocketRelease, bool suppressDown, int holdTicks, out string message)
        {
            return QueueSafeLandingJumpPulse(requestId, strategy, actionType, applyRocketRelease, suppressDown, holdTicks, false, out message);
        }

        public static bool QueueSafeLandingJumpPulse(Guid requestId, string strategy, string actionType, bool applyRocketRelease, bool suppressDown, int holdTicks, bool startWithPress, out string message)
        {
            return QueueSafeLandingJumpPulse(requestId, strategy, actionType, applyRocketRelease, suppressDown, holdTicks, startWithPress, false, out message);
        }

        public static bool QueueSafeLandingJumpPulse(Guid requestId, string strategy, string actionType, bool applyRocketRelease, bool suppressDown, int holdTicks, bool startWithPress, bool immediateCancelAfterPress, out string message)
        {
            return QueueSafeLandingJumpPulse(requestId, strategy, actionType, applyRocketRelease, suppressDown, holdTicks, startWithPress, immediateCancelAfterPress, false, 0f, 0f, out message);
        }

        public static bool QueueSafeLandingJumpPulse(Guid requestId, string strategy, string actionType, bool applyRocketRelease, bool suppressDown, int holdTicks, bool startWithPress, bool immediateCancelAfterPress, bool hasTargetWorld, float targetWorldX, float targetWorldY, out string message)
        {
            message = string.Empty;
            if (requestId == Guid.Empty)
            {
                message = "Safe landing jump pulse rejected: request id is empty.";
                return false;
            }

            lock (PulseSyncRoot)
            {
                if (_queuedJumpPulse != null && !_queuedJumpPulse.IsTerminal)
                {
                    message = "Safe landing jump pulse rejected: another pulse is active.";
                    return false;
                }

                _queuedJumpPulse = new SafeLandingJumpPulseState
                {
                    RequestId = requestId,
                    Strategy = strategy ?? string.Empty,
                    ActionType = string.IsNullOrWhiteSpace(actionType) ? "jump" : actionType,
                    ApplyRocketRelease = applyRocketRelease,
                    SuppressDown = suppressDown,
                    HoldTargetTicks = ResolveHoldTargetTicks(strategy, actionType, holdTicks),
                    ImmediateCancelAfterPress = immediateCancelAfterPress && IsQuickMountAction(actionType),
                    Phase = IsGravityFlipAction(actionType) ||
                        (startWithPress && string.Equals(actionType, "jump", StringComparison.OrdinalIgnoreCase)) ||
                        (startWithPress && IsQuickMountAction(actionType)) ||
                        (startWithPress && IsGrappleAction(actionType))
                        ? SafeLandingJumpPulsePhase.PressHold
                        : SafeLandingJumpPulsePhase.Release,
                    Status = "queued",
                    LastMessage = immediateCancelAfterPress
                        ? "Queued for Player.Update prefix press with immediate mount cancel."
                        : startWithPress ? "Queued for Player.Update prefix press." : "Queued for Player.Update prefix.",
                    StartedUtc = DateTime.UtcNow
                };
                if (hasTargetWorld && IsGrappleAction(actionType))
                {
                    _queuedJumpPulse.TargetWorldKnown = true;
                    _queuedJumpPulse.TargetWorldX = targetWorldX;
                    _queuedJumpPulse.TargetWorldY = targetWorldY;
                }

                message = _queuedJumpPulse.LastMessage;
                return true;
            }
        }

        private static int ResolveHoldTargetTicks(string strategy, string actionType, int requestedTicks)
        {
            if (IsQuickMountAction(actionType))
            {
                return ClampInt(requestedTicks <= 0 ? 2 : requestedTicks, 1, 4);
            }

            if (IsGrappleAction(actionType))
            {
                return ClampInt(requestedTicks <= 0 ? 1 : requestedTicks, 1, 3);
            }

            if (IsGravityFlipAction(actionType))
            {
                return ClampInt(requestedTicks <= 0 ? 4 : requestedTicks, 1, 6);
            }

            if (string.Equals(strategy, "equipped_double_jump", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_double_jump", StringComparison.OrdinalIgnoreCase))
            {
                return ClampInt(requestedTicks <= 0 ? 2 : requestedTicks, 1, 4);
            }

            if (string.Equals(strategy, "equipped_rocket_boots", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_rocket_boots", StringComparison.OrdinalIgnoreCase))
            {
                return ClampInt(requestedTicks <= 0 ? 16 : requestedTicks, 4, 20);
            }

            if (string.Equals(strategy, "active_flying_mount", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_flying_mount", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_safe_mount", StringComparison.OrdinalIgnoreCase))
            {
                return ClampInt(requestedTicks <= 0 ? 6 : requestedTicks, 2, 8);
            }

            if (string.Equals(strategy, "equipped_flying_carpet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strategy, "temporary_flying_carpet", StringComparison.OrdinalIgnoreCase))
            {
                return ClampInt(requestedTicks <= 0 ? 12 : requestedTicks, 4, 20);
            }

            return ClampInt(requestedTicks <= 0 ? 4 : requestedTicks, 1, 8);
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static float ClampFloat(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static bool TryGetSafeLandingJumpPulseSnapshot(Guid requestId, out SafeLandingJumpPulseSnapshot snapshot)
        {
            lock (PulseSyncRoot)
            {
                snapshot = _queuedJumpPulse == null ? null : _queuedJumpPulse.ToSnapshot();
            }

            return snapshot != null && (requestId == Guid.Empty || snapshot.RequestId == requestId);
        }

        public static bool TryGetAnySafeLandingJumpPulseSnapshot(out SafeLandingJumpPulseSnapshot snapshot)
        {
            lock (PulseSyncRoot)
            {
                snapshot = _queuedJumpPulse == null ? null : _queuedJumpPulse.ToSnapshot();
            }

            return snapshot != null;
        }

        public static void CancelSafeLandingJumpPulse(Guid requestId, string reason)
        {
            lock (PulseSyncRoot)
            {
                if (_queuedJumpPulse == null ||
                    (requestId != Guid.Empty && _queuedJumpPulse.RequestId != requestId) ||
                    _queuedJumpPulse.IsTerminal)
                {
                    return;
                }

                _queuedJumpPulse.Failed = true;
                _queuedJumpPulse.Status = "cancelled";
                _queuedJumpPulse.LastMessage = TryRestoreQueuedGrappleMouseTarget(_queuedJumpPulse, "Safe landing jump pulse cancelled: " + (reason ?? string.Empty));
                _queuedJumpPulse.CompletedUtc = DateTime.UtcNow;
            }
        }

        public static bool ApplyQueuedSafeLandingJumpPulseBeforePlayerUpdate(object player)
        {
            SafeLandingJumpPulseState pulse;
            lock (PulseSyncRoot)
            {
                pulse = _queuedJumpPulse;
                if (pulse == null || pulse.IsTerminal)
                {
                    return false;
                }
            }

            var applied = false;
            string message;
            try
            {
                if (player == null)
                {
                    FinishQueuedPulse(pulse.RequestId, false, "Player.Update prefix received null player.", "Player.Update:failed");
                    return false;
                }

                if (pulse.SuppressDown)
                {
                    TerrariaInputCompat.TrySetControlDown(player, false);
                }

                if (IsQuickMountAction(pulse.ActionType))
                {
                    return ApplyQueuedSafeLandingQuickMountPulse(player, pulse);
                }

                if (IsGravityFlipAction(pulse.ActionType))
                {
                    return ApplyQueuedSafeLandingGravityFlipPulse(player, pulse);
                }

                if (IsGrappleAction(pulse.ActionType))
                {
                    return ApplyQueuedSafeLandingGrapplePulse(player, pulse);
                }

                if (pulse.Phase == SafeLandingJumpPulsePhase.Release)
                {
                    applied = TerrariaInputCompat.TryPrimeJumpReleaseForNextTick(player, pulse.ApplyRocketRelease, out message);
                    UpdateQueuedPulse(pulse.RequestId, applied, message, "Player.Update:release", nextPhase: applied ? SafeLandingJumpPulsePhase.PressHold : SafeLandingJumpPulsePhase.Failed);
                    return applied;
                }

                if (pulse.Phase == SafeLandingJumpPulsePhase.PressHold)
                {
                    if (!pulse.PressApplied)
                    {
                        applied = TerrariaInputCompat.TryPressPrimedJumpForNextTick(player, pulse.ApplyRocketRelease, out message);
                    }
                    else
                    {
                        applied = TerrariaInputCompat.TryHoldJumpInput(player, out message);
                    }

                    UpdateQueuedPulse(pulse.RequestId, applied, message, "Player.Update:pressHold");
                    return applied;
                }

                if (pulse.Phase == SafeLandingJumpPulsePhase.FinalRelease)
                {
                    applied = TerrariaInputCompat.TryReleaseJumpInput(player, out message);
                    FinishQueuedPulse(pulse.RequestId, applied, message, applied ? "Player.Update:finalRelease" : "Player.Update:failed");
                    return applied;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSafeLandingCompat.ApplyQueuedSafeLandingJumpPulseBeforePlayerUpdate", error);
                FinishQueuedPulse(pulse.RequestId, false, "Safe landing jump pulse failed: " + error.GetType().Name + ": " + error.Message, "Player.Update:exception");
            }

            return false;
        }

        private static bool ApplyQueuedSafeLandingQuickMountPulse(object player, SafeLandingJumpPulseState pulse)
        {
            var applied = false;
            string message;
            if (pulse.Phase == SafeLandingJumpPulsePhase.Release)
            {
                applied = TerrariaInputCompat.TryPrimeQuickMountReleaseForNextTick(player, out message);
                UpdateQueuedPulse(pulse.RequestId, applied, message, "Player.Update:quickMountRelease", nextPhase: applied ? SafeLandingJumpPulsePhase.PressHold : SafeLandingJumpPulsePhase.Failed);
                return applied;
            }

            if (pulse.Phase == SafeLandingJumpPulsePhase.PressHold)
            {
                applied = !pulse.PressApplied
                    ? TerrariaInputCompat.TryPressPrimedQuickMountForNextTick(player, out message)
                    : TerrariaInputCompat.TryHoldQuickMountInput(player, out message);
                UpdateQueuedQuickMountPressHold(pulse.RequestId, applied, message, "Player.Update:quickMountPressHold");
                return applied;
            }

            if (pulse.Phase == SafeLandingJumpPulsePhase.FinalRelease)
            {
                applied = TerrariaInputCompat.TryReleaseQuickMountInput(player, out message);
                if (applied && pulse.ImmediateCancelAfterPress && !pulse.CancelPressApplied)
                {
                    ContinueQueuedQuickMountImmediateCancel(pulse.RequestId, message, "Player.Update:quickMountFinalRelease");
                }
                else
                {
                    FinishQueuedPulse(pulse.RequestId, applied, message, applied ? "Player.Update:quickMountFinalRelease" : "Player.Update:failed");
                }
                return applied;
            }

            if (pulse.Phase == SafeLandingJumpPulsePhase.CancelPressHold)
            {
                applied = TerrariaInputCompat.TryPressPrimedQuickMountForNextTick(player, out message);
                UpdateQueuedQuickMountCancelPress(pulse.RequestId, applied, message, "Player.Update:quickMountCancelPress");
                return applied;
            }

            if (pulse.Phase == SafeLandingJumpPulsePhase.CancelFinalRelease)
            {
                applied = TerrariaInputCompat.TryReleaseQuickMountInput(player, out message);
                FinishQueuedQuickMountImmediateCancel(pulse.RequestId, applied, message, applied ? "Player.Update:quickMountCancelFinalRelease" : "Player.Update:failed");
                return applied;
            }

            return false;
        }

        private static bool ApplyQueuedSafeLandingGravityFlipPulse(object player, SafeLandingJumpPulseState pulse)
        {
            var applied = false;
            string message;
            if (pulse.Phase == SafeLandingJumpPulsePhase.Release)
            {
                applied = TerrariaInputCompat.TryPrimeUpReleaseForNextTick(player, out message);
                UpdateQueuedPulse(pulse.RequestId, applied, message, "Player.Update:gravityFlipRelease", nextPhase: applied ? SafeLandingJumpPulsePhase.PressHold : SafeLandingJumpPulsePhase.Failed);
                return applied;
            }

            if (pulse.Phase == SafeLandingJumpPulsePhase.PressHold)
            {
                applied = !pulse.PressApplied
                    ? TerrariaInputCompat.TryPressPrimedUpForNextTick(player, out message)
                    : TerrariaInputCompat.TryHoldUpInput(player, out message);
                UpdateQueuedPulse(pulse.RequestId, applied, message, "Player.Update:gravityFlipPressHold");
                return applied;
            }

            if (pulse.Phase == SafeLandingJumpPulsePhase.FinalRelease)
            {
                applied = TerrariaInputCompat.TryReleaseUpInput(player, out message);
                FinishQueuedPulse(pulse.RequestId, applied, message, applied ? "Player.Update:gravityFlipFinalRelease" : "Player.Update:failed");
                return applied;
            }

            return false;
        }

        private static bool ApplyQueuedSafeLandingGrapplePulse(object player, SafeLandingJumpPulseState pulse)
        {
            var applied = false;
            string message;
            if (pulse.Phase == SafeLandingJumpPulsePhase.Release)
            {
                applied = TerrariaInputCompat.TryPrimeGrappleReleaseForNextTick(player, out message);
                UpdateQueuedPulse(pulse.RequestId, applied, message, "Player.Update:grappleRelease", nextPhase: applied ? SafeLandingJumpPulsePhase.PressHold : SafeLandingJumpPulsePhase.Failed);
                return applied;
            }

            if (pulse.Phase == SafeLandingJumpPulsePhase.PressHold)
            {
                if (!TryApplySafeLandingGrappleAim(player, pulse, out message))
                {
                    UpdateQueuedPulse(pulse.RequestId, false, message, "Player.Update:grappleAimFailed");
                    return false;
                }

                applied = !pulse.PressApplied
                    ? TerrariaInputCompat.TryPressPrimedGrappleForNextTick(player, out message)
                    : TerrariaInputCompat.TryHoldGrappleInput(player, out message);
                UpdateQueuedPulse(pulse.RequestId, applied, message, "Player.Update:grapplePressHold");
                return applied;
            }

            if (pulse.Phase == SafeLandingJumpPulsePhase.FinalRelease)
            {
                applied = TerrariaInputCompat.TryReleaseGrappleInput(player, out message);
                FinishQueuedPulse(pulse.RequestId, applied, message, applied ? "Player.Update:grappleFinalRelease" : "Player.Update:failed");
                return applied;
            }

            return false;
        }

        private static bool TryApplySafeLandingGrappleAim(object player, SafeLandingJumpPulseState pulse, out string message)
        {
            message = string.Empty;
            if (player == null || pulse == null)
            {
                message = "Safe landing grapple aim failed: player or pulse unavailable.";
                return false;
            }

            float targetX;
            float targetY;
            if (!pulse.TargetWorldKnown)
            {
                if (!TryResolveCurrentGrappleTarget(player, out targetX, out targetY))
                {
                    message = "Safe landing grapple aim failed: target unavailable.";
                    return false;
                }

                pulse.TargetWorldKnown = true;
                pulse.TargetWorldX = targetX;
                pulse.TargetWorldY = targetY;
            }

            if (!pulse.MouseTargetCaptured)
            {
                MouseTargetInputState restoreState;
                if (TerrariaInputCompat.TryCaptureMouseTargetState(player, out restoreState))
                {
                    pulse.MouseTargetRestoreState = restoreState;
                    pulse.MouseTargetCaptured = true;
                }
            }

            if (!TerrariaInputCompat.TrySetMouseWorldPosition(pulse.TargetWorldX, pulse.TargetWorldY))
            {
                message = "Safe landing grapple aim failed: " + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            message = "Safe landing grapple aim applied.";
            return true;
        }

        private static string TryRestoreQueuedGrappleMouseTarget(SafeLandingJumpPulseState pulse, string message)
        {
            if (pulse == null || !IsGrappleAction(pulse.ActionType) || !pulse.MouseTargetCaptured || pulse.MouseTargetRestoreAttempted)
            {
                return message ?? string.Empty;
            }

            pulse.MouseTargetRestoreAttempted = true;
            pulse.MouseTargetRestoreSucceeded = TerrariaInputCompat.TryRestoreMouseTargetState(pulse.MouseTargetRestoreState);
            pulse.MouseTargetRestoreMessage = pulse.MouseTargetRestoreSucceeded
                ? "Grapple mouse target restored."
                : "Grapple mouse target restore failed: " + TerrariaInputCompat.LastInputCompatError;
            return (message ?? string.Empty) + " " + pulse.MouseTargetRestoreMessage;
        }

        private static void UpdateQueuedPulse(Guid requestId, bool applied, string message, string applySite)
        {
            UpdateQueuedPulse(requestId, applied, message, applySite, null);
        }

        private static void UpdateQueuedPulse(Guid requestId, bool applied, string message, string applySite, SafeLandingJumpPulsePhase? nextPhase)
        {
            lock (PulseSyncRoot)
            {
                if (_queuedJumpPulse == null || _queuedJumpPulse.RequestId != requestId || _queuedJumpPulse.IsTerminal)
                {
                    return;
                }

                _queuedJumpPulse.LastApplySite = applySite ?? string.Empty;
                _queuedJumpPulse.LastMessage = message ?? string.Empty;
                _queuedJumpPulse.LastAppliedUtc = DateTime.UtcNow;
                _queuedJumpPulse.Status = applied ? "applying" : "failed";

                if (!applied)
                {
                    _queuedJumpPulse.LastMessage = TryRestoreQueuedGrappleMouseTarget(_queuedJumpPulse, _queuedJumpPulse.LastMessage);
                    _queuedJumpPulse.Failed = true;
                    _queuedJumpPulse.CompletedUtc = DateTime.UtcNow;
                    _queuedJumpPulse.Phase = SafeLandingJumpPulsePhase.Failed;
                    return;
                }

                if (_queuedJumpPulse.Phase == SafeLandingJumpPulsePhase.Release)
                {
                    _queuedJumpPulse.ReleaseApplied = true;
                }
                else if (_queuedJumpPulse.Phase == SafeLandingJumpPulsePhase.PressHold)
                {
                    if (!_queuedJumpPulse.PressApplied)
                    {
                        _queuedJumpPulse.PressApplied = true;
                    }
                    else
                    {
                        _queuedJumpPulse.HoldTicks++;
                    }
                }

                if (nextPhase.HasValue)
                {
                    _queuedJumpPulse.Phase = nextPhase.Value;
                }
                else if (_queuedJumpPulse.Phase == SafeLandingJumpPulsePhase.PressHold &&
                         _queuedJumpPulse.PressApplied &&
                         _queuedJumpPulse.HoldTicks >= _queuedJumpPulse.HoldTargetTicks)
                {
                    _queuedJumpPulse.Phase = SafeLandingJumpPulsePhase.FinalRelease;
                }
            }
        }

        private static void UpdateQueuedQuickMountPressHold(Guid requestId, bool applied, string message, string applySite)
        {
            lock (PulseSyncRoot)
            {
                if (_queuedJumpPulse == null || _queuedJumpPulse.RequestId != requestId || _queuedJumpPulse.IsTerminal)
                {
                    return;
                }

                _queuedJumpPulse.LastApplySite = applySite ?? string.Empty;
                _queuedJumpPulse.LastMessage = message ?? string.Empty;
                _queuedJumpPulse.LastAppliedUtc = DateTime.UtcNow;
                _queuedJumpPulse.Status = applied ? "applying" : "failed";

                if (!applied)
                {
                    _queuedJumpPulse.Failed = true;
                    _queuedJumpPulse.CompletedUtc = DateTime.UtcNow;
                    _queuedJumpPulse.Phase = SafeLandingJumpPulsePhase.Failed;
                    return;
                }

                if (!_queuedJumpPulse.PressApplied)
                {
                    _queuedJumpPulse.PressApplied = true;
                }

                _queuedJumpPulse.HoldTicks++;
                if (_queuedJumpPulse.HoldTicks >= Math.Max(1, _queuedJumpPulse.HoldTargetTicks))
                {
                    _queuedJumpPulse.Phase = SafeLandingJumpPulsePhase.FinalRelease;
                }
            }
        }

        private static void ContinueQueuedQuickMountImmediateCancel(Guid requestId, string message, string applySite)
        {
            lock (PulseSyncRoot)
            {
                if (_queuedJumpPulse == null || _queuedJumpPulse.RequestId != requestId || _queuedJumpPulse.IsTerminal)
                {
                    return;
                }

                _queuedJumpPulse.FinalReleaseApplied = true;
                _queuedJumpPulse.LastApplySite = applySite ?? string.Empty;
                _queuedJumpPulse.LastMessage = (message ?? string.Empty) + " Immediate mount cancel pending.";
                _queuedJumpPulse.LastAppliedUtc = DateTime.UtcNow;
                _queuedJumpPulse.Status = "applying";
                _queuedJumpPulse.Phase = SafeLandingJumpPulsePhase.CancelPressHold;
            }
        }

        private static void UpdateQueuedQuickMountCancelPress(Guid requestId, bool applied, string message, string applySite)
        {
            lock (PulseSyncRoot)
            {
                if (_queuedJumpPulse == null || _queuedJumpPulse.RequestId != requestId || _queuedJumpPulse.IsTerminal)
                {
                    return;
                }

                _queuedJumpPulse.LastApplySite = applySite ?? string.Empty;
                _queuedJumpPulse.LastMessage = message ?? string.Empty;
                _queuedJumpPulse.LastAppliedUtc = DateTime.UtcNow;
                _queuedJumpPulse.Status = applied ? "applying" : "failed";

                if (!applied)
                {
                    _queuedJumpPulse.Failed = true;
                    _queuedJumpPulse.CompletedUtc = DateTime.UtcNow;
                    _queuedJumpPulse.Phase = SafeLandingJumpPulsePhase.Failed;
                    return;
                }

                _queuedJumpPulse.CancelPressApplied = true;
                _queuedJumpPulse.Phase = SafeLandingJumpPulsePhase.CancelFinalRelease;
            }
        }

        private static void FinishQueuedQuickMountImmediateCancel(Guid requestId, bool succeeded, string message, string applySite)
        {
            lock (PulseSyncRoot)
            {
                if (_queuedJumpPulse == null || _queuedJumpPulse.RequestId != requestId || _queuedJumpPulse.IsTerminal)
                {
                    return;
                }

                _queuedJumpPulse.CancelFinalReleaseApplied = succeeded;
                _queuedJumpPulse.Completed = succeeded;
                _queuedJumpPulse.Failed = !succeeded;
                _queuedJumpPulse.Status = succeeded ? "completed" : "failed";
                _queuedJumpPulse.LastMessage = message ?? string.Empty;
                _queuedJumpPulse.LastApplySite = applySite ?? string.Empty;
                _queuedJumpPulse.LastAppliedUtc = DateTime.UtcNow;
                _queuedJumpPulse.CompletedUtc = DateTime.UtcNow;
                _queuedJumpPulse.Phase = succeeded ? SafeLandingJumpPulsePhase.Completed : SafeLandingJumpPulsePhase.Failed;
            }
        }

        private static void FinishQueuedPulse(Guid requestId, bool succeeded, string message, string applySite)
        {
            lock (PulseSyncRoot)
            {
                if (_queuedJumpPulse == null || _queuedJumpPulse.RequestId != requestId || _queuedJumpPulse.IsTerminal)
                {
                    return;
                }

                _queuedJumpPulse.FinalReleaseApplied = succeeded;
                _queuedJumpPulse.Completed = succeeded;
                _queuedJumpPulse.Failed = !succeeded;
                _queuedJumpPulse.Status = succeeded ? "completed" : "failed";
                _queuedJumpPulse.LastMessage = TryRestoreQueuedGrappleMouseTarget(_queuedJumpPulse, message);
                _queuedJumpPulse.LastApplySite = applySite ?? string.Empty;
                _queuedJumpPulse.LastAppliedUtc = DateTime.UtcNow;
                _queuedJumpPulse.CompletedUtc = DateTime.UtcNow;
                _queuedJumpPulse.Phase = succeeded ? SafeLandingJumpPulsePhase.Completed : SafeLandingJumpPulsePhase.Failed;
            }
        }

        private static bool TryResolveAlreadySafe(object player, JumpInputProfile jump, out string reason)
        {
            return TryResolveAlreadySafe(player, jump, null, out reason);
        }

        private static bool TryResolveAlreadySafe(object player, JumpInputProfile jump, MovementSafeLandingAnalysis analysis, out string reason)
        {
            if (analysis != null)
            {
                var activeGrapCount = analysis.FallingSpeed >= MinimumDangerFallingSpeed
                    ? 0
                    : analysis.RawGrapCount;
                return TryResolveAlreadySafeCore(
                    analysis.RawCreativeGodMode,
                    analysis.RawNoFallDmg,
                    analysis.RawSlowFall,
                    analysis.RawWet,
                    analysis.RawHoneyWet,
                    analysis.RawShimmering,
                    analysis.RawWebbed,
                    analysis.RawStoned,
                    activeGrapCount,
                    analysis.RawEquippedWingCount,
                    analysis.HasWingFlight,
                    analysis.RawMountNoFallDamage,
                    out reason);
            }

            var creativeGodMode = TryReadBool(player, "creativeGodMode", false);
            var noFallDmg = TryReadBool(player, "noFallDmg", false);
            var slowFall = TryReadBool(player, "slowFall", false);
            var wet = TryReadBool(player, "wet", false);
            var honeyWet = TryReadBool(player, "honeyWet", false);
            var shimmering = TryReadBool(player, "shimmering", false);
            var webbed = TryReadBool(player, "webbed", false);
            var stoned = TryReadBool(player, "stoned", false);
            var grapCount = TryReadInt(player, "grapCount", 0);
            var equippedWingCount = TryCountEquippedFallDamageWings(player);
            var wingFlightState = jump != null && jump.HasWingFlight;
            var mountNoFallDamage = TryReadMountNoFallDamage(player);

            return TryResolveAlreadySafeCore(
                creativeGodMode,
                noFallDmg,
                slowFall,
                wet,
                honeyWet,
                shimmering,
                webbed,
                stoned,
                grapCount,
                equippedWingCount,
                wingFlightState,
                mountNoFallDamage,
                out reason);
        }

        private static void PopulateAlreadySafeRawState(object player, MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null)
            {
                return;
            }

            analysis.RawCreativeGodMode = TryReadBool(player, "creativeGodMode", false);
            analysis.RawNoFallDmg = TryReadBool(player, "noFallDmg", false);
            analysis.RawSlowFall = TryReadBool(player, "slowFall", false);
            analysis.RawWet = TryReadBool(player, "wet", false);
            analysis.RawHoneyWet = TryReadBool(player, "honeyWet", false);
            analysis.RawShimmering = TryReadBool(player, "shimmering", false);
            analysis.RawWebbed = TryReadBool(player, "webbed", false);
            analysis.RawStoned = TryReadBool(player, "stoned", false);
            analysis.RawGrapCount = TryReadInt(player, "grapCount", 0);
            analysis.RawEquippedWingCount = TryCountEquippedFallDamageWings(player);
            analysis.RawMountNoFallDamage = TryReadMountNoFallDamage(player);
        }

        private static bool TryResolveAlreadySafeCore(
            bool creativeGodMode,
            bool noFallDmg,
            bool slowFall,
            bool wet,
            bool honeyWet,
            bool shimmering,
            bool webbed,
            bool stoned,
            int grapCount,
            int equippedWingCount,
            bool wingFlightState,
            bool mountNoFallDamage,
            out string reason)
        {
            reason = string.Empty;
            if (creativeGodMode)
            {
                reason = "creativeGodMode";
                return true;
            }

            if (!stoned && noFallDmg)
            {
                reason = "noFallDmg";
                return true;
            }

            if (slowFall)
            {
                reason = "slowFall";
                return true;
            }

            if (wet || honeyWet || shimmering)
            {
                reason = "liquid";
                return true;
            }

            if (webbed)
            {
                reason = "webbed";
                return true;
            }

            if (!stoned && grapCount > 0)
            {
                reason = "grappled";
                return true;
            }

            if (!stoned && equippedWingCount > 0)
            {
                reason = "wingsEquipped";
                return true;
            }

            if (!stoned && wingFlightState)
            {
                reason = "wingFlightState";
                return true;
            }

            if (!stoned && mountNoFallDamage)
            {
                reason = "safeMount";
                return true;
            }

            return false;
        }

        private static int TryCountEquippedFallDamageWings(object player)
        {
            var armor = GetMember(player, "armor") as IList;
            if (armor == null || armor.Count <= 3)
            {
                return 0;
            }

            var count = 0;
            var end = Math.Min(10, armor.Count);
            for (var slot = 3; slot < end; slot++)
            {
                if (!TryIsAccessorySlotUsable(player, slot))
                {
                    continue;
                }

                var item = armor[slot];
                if (item == null)
                {
                    continue;
                }

                int stack;
                int wingSlot;
                if (TryReadInt(item, "stack", out stack) &&
                    stack > 0 &&
                    TryReadInt(item, "wingSlot", out wingSlot) &&
                    wingSlot > -1)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryIsAccessorySlotUsable(object player, int slot)
        {
            if (player == null || slot < 3 || slot > 9)
            {
                return false;
            }

            try
            {
                var slotUsability = ResolveSlotUsabilityMethod(player.GetType());
                if (slotUsability != null)
                {
                    return Convert.ToBoolean(slotUsability.Invoke(player, new object[] { slot }));
                }
            }
            catch
            {
                return false;
            }

            return slot <= 7;
        }

        private static MethodInfo ResolveSlotUsabilityMethod(Type playerType)
        {
            if (_slotUsabilityMethodResolved)
            {
                return _slotUsabilityMethod;
            }

            _slotUsabilityMethodResolved = true;
            if (playerType == null)
            {
                _slotUsabilityMethod = null;
                return null;
            }

            _slotUsabilityMethod = playerType.GetMethod(
                "IsItemSlotUnlockedAndUsable",
                InstanceFlags,
                null,
                new[] { typeof(int) },
                null);
            return _slotUsabilityMethod;
        }

        private static bool TryResolveProjectedSafeLanding(MovementSafeLandingAnalysis analysis, int impactDistancePixels, out string reason)
        {
            reason = string.Empty;
            if (analysis == null || analysis.FallingSpeed < MinimumDangerFallingSpeed)
            {
                return false;
            }

            Array tiles;
            if (!TryGetMainTileArray(out tiles))
            {
                return false;
            }

            var maxDistance = impactDistancePixels >= 0
                ? Math.Max(16, Math.Min(impactDistancePixels, 768))
                : Math.Min(768, Math.Max(128, (int)(analysis.FallingSpeed * 24f) + 96));
            var gravityDirection = analysis.GravityDirection >= 0f ? 1 : -1;
            var scanOriginY = analysis.GravityDirection >= 0f
                ? analysis.PositionY + analysis.Height
                : analysis.PositionY;
            for (var offset = 0; offset <= maxDistance; offset += 16)
            {
                var tileY = (int)Math.Floor((scanOriginY + offset * gravityDirection) / 16f);
                if (ScanProjectedSafeLandingTiles(tiles, analysis, tileY, analysis.PositionX, out reason))
                {
                    return true;
                }

                var projectedX = ResolveProjectedImpactProbeX(analysis.PositionX, analysis.VelocityX, analysis.FallingSpeed, offset);
                if (!NearlyEqual(projectedX, analysis.PositionX) &&
                    ScanProjectedSafeLandingTiles(tiles, analysis, tileY, projectedX, out reason))
                {
                    return true;
                }

                var middleX = (analysis.PositionX + projectedX) / 2f;
                if (!NearlyEqual(middleX, analysis.PositionX) &&
                    !NearlyEqual(middleX, projectedX) &&
                    ScanProjectedSafeLandingTiles(tiles, analysis, tileY, middleX, out reason))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ScanProjectedSafeLandingTiles(Array tiles, MovementSafeLandingAnalysis analysis, int tileY, float positionX, out string reason)
        {
            reason = string.Empty;
            if (tiles == null || analysis == null)
            {
                return false;
            }

            var leftTile = (int)Math.Floor((positionX + 2f) / 16f);
            var rightTile = (int)Math.Floor((positionX + Math.Max(2, analysis.Width - 2)) / 16f);
            if (rightTile < leftTile)
            {
                var temp = leftTile;
                leftTile = rightTile;
                rightTile = temp;
            }

            for (var tileX = leftTile; tileX <= rightTile; tileX++)
            {
                var tile = GetTile(tiles, tileX, tileY);
                if (IsProjectedSafeTile(tile, out reason))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindImpactDistance(MovementSafeLandingAnalysis analysis, out int distance)
        {
            float impactPositionX;
            return TryFindImpact(analysis, out distance, out impactPositionX);
        }

        private static bool TryFindImpact(MovementSafeLandingAnalysis analysis, out int distance, out float impactPositionX)
        {
            distance = -1;
            impactPositionX = analysis == null ? 0f : analysis.PositionX;
            if (analysis == null)
            {
                return false;
            }

            MovementLandingSurfaceHit hit;
            if (!TryFindLandingSurface(analysis, out hit) || hit == null || !hit.Found)
            {
                return false;
            }

            distance = hit.ImpactDistancePixels;
            impactPositionX = hit.ProjectedPlayerLeftX;
            return true;
        }

        private static bool TryFindLandingSurface(MovementSafeLandingAnalysis analysis, out MovementLandingSurfaceHit hit)
        {
            hit = new MovementLandingSurfaceHit();
            if (analysis == null)
            {
                return false;
            }

            var probe = Math.Min(768, Math.Max(128, (int)(analysis.FallingSpeed * 24f) + 96));
            const int coarseStep = 16;
            var previous = 0;
            for (var offset = 8; offset <= probe; offset += coarseStep)
            {
                MovementLandingSurfaceHit coarseHit;
                if (TryProbeProjectedLandingSurface(analysis, offset, out coarseHit) && coarseHit != null && coarseHit.Found)
                {
                    var low = Math.Max(0, previous - 4);
                    var high = offset;
                    var bestHit = coarseHit;
                    while (high - low > 4)
                    {
                        var middle = low + (high - low) / 2;
                        MovementLandingSurfaceHit middleHit;
                        if (TryProbeProjectedLandingSurface(analysis, middle, out middleHit) && middleHit != null && middleHit.Found)
                        {
                            high = middle;
                            bestHit = middleHit;
                        }
                        else
                        {
                            low = middle + 1;
                        }
                    }

                    MovementLandingSurfaceHit finalHit;
                    hit = TryProbeProjectedLandingSurface(analysis, high, out finalHit) && finalHit != null && finalHit.Found
                        ? finalHit
                        : bestHit;
                    return true;
                }

                previous = offset;
            }

            return false;
        }

        private static bool TryProbeProjectedLandingSurface(MovementSafeLandingAnalysis analysis, int offset, out MovementLandingSurfaceHit hit)
        {
            hit = new MovementLandingSurfaceHit();
            if (analysis == null)
            {
                return false;
            }

            var direction = analysis.GravityDirection >= 0f ? 1f : -1f;
            var y = analysis.PositionY + offset * direction;
            var projectedX = ResolveProjectedImpactProbeX(analysis.PositionX, analysis.VelocityX, analysis.FallingSpeed, offset);
            MovementLandingSurfaceHit bestHit = null;
            MovementLandingSurfaceHit candidate;

            if (TryProbeLandingSurfaceAtOffset(analysis, projectedX, y, offset, out candidate) && candidate != null && candidate.Found)
            {
                bestHit = candidate;
            }

            var middleX = (analysis.PositionX + projectedX) / 2f;
            if (!NearlyEqual(middleX, projectedX) &&
                !NearlyEqual(middleX, analysis.PositionX) &&
                TryProbeLandingSurfaceAtOffset(analysis, middleX, y, offset, out candidate) &&
                candidate != null &&
                candidate.Found &&
                IsBetterProbeHit(candidate, bestHit, analysis))
            {
                bestHit = candidate;
            }

            if (!NearlyEqual(analysis.PositionX, projectedX) &&
                TryProbeLandingSurfaceAtOffset(analysis, analysis.PositionX, y, offset, out candidate) &&
                candidate != null &&
                candidate.Found &&
                IsBetterProbeHit(candidate, bestHit, analysis))
            {
                bestHit = candidate;
            }

            if (bestHit == null)
            {
                return false;
            }

            hit = bestHit;
            return true;
        }

        private static bool TryProbeLandingSurfaceAtOffset(
            MovementSafeLandingAnalysis analysis,
            float x,
            float y,
            int offset,
            out MovementLandingSurfaceHit hit)
        {
            hit = TryManualLandingSurfaceImpact(
                x,
                y,
                analysis.Width,
                analysis.Height,
                analysis.GravityDirection,
                analysis.FallingSpeed,
                analysis.VelocityX);
            if (hit != null && hit.Found)
            {
                ApplyImpactDistanceToHit(analysis, hit, x, y, offset);
                return true;
            }

            bool solid;
            if (TryProbeLandingCollision(x, y, analysis.Width, analysis.Height, analysis.GravityDirection, analysis.FallingSpeed, out solid) && solid)
            {
                hit = CreateLegacyLandingSurfaceHit(analysis, x, y, offset);
                return true;
            }

            hit = new MovementLandingSurfaceHit();
            return false;
        }

        private static MovementLandingSurfaceHit CreateLegacyLandingSurfaceHit(MovementSafeLandingAnalysis analysis, float x, float y, int offset)
        {
            var bottomY = analysis.GravityDirection >= 0f ? y + analysis.Height : y;
            var centerX = x + analysis.Width / 2f;
            var hit = new MovementLandingSurfaceHit
            {
                Found = true,
                ContactWorldX = centerX,
                ContactWorldY = bottomY,
                ContactTileX = (int)Math.Floor(centerX / 16f),
                ContactTileY = (int)Math.Floor(bottomY / 16f),
                SurfaceKind = "unknown",
                SlopeDirection = "unknown",
                ContactSample = "center_foot",
                Summary = "legacy_collision contact=center_foot"
            };
            ApplyImpactDistanceToHit(analysis, hit, x, y, offset);
            return hit;
        }

        private static void ApplyImpactDistanceToHit(MovementSafeLandingAnalysis analysis, MovementLandingSurfaceHit hit, float x, float y, int offset)
        {
            if (analysis == null || hit == null)
            {
                return;
            }

            hit.ImpactDistancePixels = Math.Max(0, offset);
            hit.ImpactTicks = analysis.FallingSpeed > 0.001f
                ? hit.ImpactDistancePixels / analysis.FallingSpeed
                : -1f;
            hit.ProjectedPlayerLeftX = x;
            hit.ProjectedPlayerRightX = x + analysis.Width;
            hit.ProjectedPlayerBottomY = analysis.GravityDirection >= 0f
                ? y + analysis.Height
                : y;
        }

        private static bool IsBetterProbeHit(MovementLandingSurfaceHit candidate, MovementLandingSurfaceHit current, MovementSafeLandingAnalysis analysis)
        {
            if (candidate == null || !candidate.Found)
            {
                return false;
            }

            if (current == null || !current.Found)
            {
                return true;
            }

            if (candidate.MovingIntoSlope != current.MovingIntoSlope)
            {
                return candidate.MovingIntoSlope;
            }

            if (Math.Abs(candidate.ContactWorldY - current.ContactWorldY) > 0.25f)
            {
                return analysis == null || analysis.GravityDirection >= 0f
                    ? candidate.ContactWorldY < current.ContactWorldY
                    : candidate.ContactWorldY > current.ContactWorldY;
            }

            var candidateLeading = string.Equals(candidate.ContactSample, "leading_foot", StringComparison.Ordinal);
            var currentLeading = string.Equals(current.ContactSample, "leading_foot", StringComparison.Ordinal);
            if (candidateLeading != currentLeading)
            {
                return candidateLeading;
            }

            var velocityX = analysis == null ? 0f : analysis.VelocityX;
            if (Math.Abs(velocityX) > 0.01f)
            {
                var candidateProjectedCenter = (candidate.ProjectedPlayerLeftX + candidate.ProjectedPlayerRightX) / 2f;
                var currentProjectedCenter = (current.ProjectedPlayerLeftX + current.ProjectedPlayerRightX) / 2f;
                if (Math.Abs(candidateProjectedCenter - currentProjectedCenter) > 0.25f)
                {
                    return velocityX > 0f
                        ? candidateProjectedCenter > currentProjectedCenter
                        : candidateProjectedCenter < currentProjectedCenter;
                }
            }

            var candidateCenterX = (candidate.ProjectedPlayerLeftX + candidate.ProjectedPlayerRightX) / 2f;
            var currentCenterX = (current.ProjectedPlayerLeftX + current.ProjectedPlayerRightX) / 2f;
            if (candidate.ProjectedPlayerRightX <= candidate.ProjectedPlayerLeftX)
            {
                candidateCenterX = analysis == null ? candidate.ContactWorldX : analysis.PositionX + analysis.Width / 2f;
            }

            if (current.ProjectedPlayerRightX <= current.ProjectedPlayerLeftX)
            {
                currentCenterX = analysis == null ? current.ContactWorldX : analysis.PositionX + analysis.Width / 2f;
            }

            var candidateCenterDistance = Math.Abs(candidate.ContactWorldX - candidateCenterX);
            var currentCenterDistance = Math.Abs(current.ContactWorldX - currentCenterX);
            if (Math.Abs(candidateCenterDistance - currentCenterDistance) > 0.25f)
            {
                return candidateCenterDistance < currentCenterDistance;
            }

            if (velocityX < -0.01f && candidate.ContactTileX != current.ContactTileX)
            {
                return candidate.ContactTileX < current.ContactTileX;
            }

            if (velocityX > 0.01f && candidate.ContactTileX != current.ContactTileX)
            {
                return candidate.ContactTileX > current.ContactTileX;
            }

            return false;
        }

        private static void ApplyLandingSurfaceHit(MovementSafeLandingAnalysis analysis, MovementLandingSurfaceHit hit)
        {
            if (analysis == null)
            {
                return;
            }

            if (hit == null || !hit.Found)
            {
                analysis.LandingSurfaceKnown = false;
                return;
            }

            analysis.LandingSurfaceKnown = true;
            analysis.LandingContactWorldX = hit.ContactWorldX;
            analysis.LandingContactWorldY = hit.ContactWorldY;
            analysis.LandingContactTileX = hit.ContactTileX;
            analysis.LandingContactTileY = hit.ContactTileY;
            analysis.LandingSurfaceKind = hit.SurfaceKind ?? string.Empty;
            analysis.LandingSlopeType = hit.SlopeType;
            analysis.LandingSlopeDirection = hit.SlopeDirection ?? string.Empty;
            analysis.LandingContactSample = hit.ContactSample ?? string.Empty;
            analysis.LandingMovingIntoSlope = hit.MovingIntoSlope;
            analysis.LandingMovingWithSlope = hit.MovingWithSlope;
            analysis.LandingProjectedPlayerLeftX = hit.ProjectedPlayerLeftX;
            analysis.LandingProjectedPlayerRightX = hit.ProjectedPlayerRightX;
            analysis.LandingProjectedPlayerBottomY = hit.ProjectedPlayerBottomY;
            analysis.LandingSurfaceSummary = hit.Summary ?? string.Empty;
        }

        private static bool TryProbeProjectedLandingCollision(MovementSafeLandingAnalysis analysis, int offset, out bool solid, out float hitX)
        {
            solid = false;
            hitX = analysis == null ? 0f : analysis.PositionX;
            if (analysis == null)
            {
                return false;
            }

            var direction = analysis.GravityDirection >= 0f ? 1f : -1f;
            var y = analysis.PositionY + offset * direction;
            var projectedX = ResolveProjectedImpactProbeX(analysis.PositionX, analysis.VelocityX, analysis.FallingSpeed, offset);
            var resolved = false;

            if (TryProbeLandingCollision(projectedX, y, analysis.Width, analysis.Height, analysis.GravityDirection, analysis.FallingSpeed, out var projectedSolid))
            {
                resolved = true;
                if (projectedSolid)
                {
                    solid = true;
                    hitX = projectedX;
                    return true;
                }
            }

            var middleX = (analysis.PositionX + projectedX) / 2f;
            if (!NearlyEqual(middleX, projectedX) &&
                !NearlyEqual(middleX, analysis.PositionX) &&
                TryProbeLandingCollision(middleX, y, analysis.Width, analysis.Height, analysis.GravityDirection, analysis.FallingSpeed, out var middleSolid))
            {
                resolved = true;
                if (middleSolid)
                {
                    solid = true;
                    hitX = middleX;
                    return true;
                }
            }

            if (!NearlyEqual(analysis.PositionX, projectedX) &&
                TryProbeLandingCollision(analysis.PositionX, y, analysis.Width, analysis.Height, analysis.GravityDirection, analysis.FallingSpeed, out var currentSolid))
            {
                resolved = true;
                if (currentSolid)
                {
                    solid = true;
                    hitX = analysis.PositionX;
                    return true;
                }
            }

            return resolved;
        }

        private static float ResolveProjectedImpactProbeX(float positionX, float velocityX, float fallingSpeed, int offsetPixels)
        {
            if (offsetPixels <= 0 || Math.Abs(velocityX) < 0.01f || Math.Abs(fallingSpeed) < 0.001f)
            {
                return positionX;
            }

            var leadTicks = offsetPixels / Math.Abs(fallingSpeed);
            if (float.IsNaN(leadTicks) || float.IsInfinity(leadTicks) || leadTicks <= 0f)
            {
                return positionX;
            }

            if (leadTicks > ImpactHorizontalLeadMaxTicks)
            {
                leadTicks = ImpactHorizontalLeadMaxTicks;
            }

            return positionX + velocityX * leadTicks;
        }

        private static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= ImpactHorizontalSampleEpsilon;
        }

        private static bool TryGetMainTileArray(out Array tiles)
        {
            tiles = GetStaticMember(ResolveMainType(), "tile") as Array;
            return tiles != null && tiles.Rank >= 2;
        }

        private static Type ResolveMainType()
        {
            return TerrariaRuntimeTypes.MainType ?? FindType("Terraria.Main");
        }

        private static object GetTile(Array tiles, int x, int y)
        {
            if (tiles == null || tiles.Rank < 2 || x < 0 || y < 0 || x >= tiles.GetLength(0) || y >= tiles.GetLength(1))
            {
                return null;
            }

            try
            {
                return tiles.GetValue(x, y);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsProjectedSafeTile(object tile, out string reason)
        {
            reason = string.Empty;
            if (tile == null)
            {
                return false;
            }

            if (ReadTileLiquidAmount(tile) > 0)
            {
                var liquidType = ReadTileLiquidType(tile);
                reason = liquidType == 2
                    ? "projectedHoney"
                    : liquidType == 3
                        ? "projectedShimmer"
                        : liquidType == LavaLiquidType
                            ? "projectedLavaNoFallDamage"
                            : "projectedWater";
                return true;
            }

            if (!IsTileActive(tile))
            {
                return false;
            }

            var tileType = ReadTileType(tile);
            if (IsFallDamageSafeLandingTile(tile, tileType, out reason))
            {
                return true;
            }

            return false;
        }

        private static bool IsFallDamageSafeLandingTile(object tile, int tileType, out string reason)
        {
            reason = string.Empty;
            switch (tileType)
            {
                case CobwebTileFallbackType:
                case CobwebReplicaTileType:
                    reason = "projectedCobweb";
                    return true;
                case CloudTileType:
                case RainCloudTileType:
                case SnowCloudTileType:
                case LavaCloudTileType:
                case StarCloudTileType:
                case RainbowCloudTileType:
                    reason = "projectedCloudBlock";
                    return true;
                case PinkSlimeBlockTileType:
                    reason = "projectedPinkSlimeBlock";
                    return true;
                case SillyBalloonPinkTileType:
                case SillyBalloonPurpleTileType:
                case SillyBalloonGreenTileType:
                    reason = "projectedSillyBalloonBlock";
                    return true;
                case PoopBlockTileType:
                    reason = "projectedPoopBlock";
                    return true;
                case PlatformTileType:
                    if (ReadPlatformStyle(tile) == CloudPlatformStyle)
                    {
                        reason = "projectedCloudPlatform";
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        private static void PopulateTeleportRodCandidate(object player, MovementSafeLandingAnalysis analysis)
        {
            if (player == null || analysis == null)
            {
                return;
            }

            var inventory = GetMember(player, "inventory") as IList;
            if (inventory == null || inventory.Count == 0)
            {
                return;
            }

            var selectedSlot = -1;
            TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot);
            var bestPriority = int.MaxValue;
            var found = false;
            var bestCandidate = default(TeleportRodCandidate);
            if (TryReadTeleportRod(inventory, selectedSlot, out var selectedCandidate))
            {
                bestCandidate = selectedCandidate;
                bestPriority = selectedCandidate.Priority;
                found = true;
            }

            var inventoryMax = Math.Min(59, inventory.Count);
            for (var slot = 0; slot < inventoryMax; slot++)
            {
                if (slot == selectedSlot ||
                    !TerrariaInputCompat.IsSupportedItemUseSlot(slot) ||
                    !TryReadTeleportRod(inventory, slot, out var candidate))
                {
                    continue;
                }

                if (!found ||
                    candidate.Priority < bestPriority ||
                    (candidate.Priority == bestPriority && candidate.Slot < bestCandidate.Slot))
                {
                    bestCandidate = candidate;
                    bestPriority = candidate.Priority;
                    found = true;
                }
            }

            if (found)
            {
                ApplyTeleportRodCandidate(analysis, bestCandidate);
            }
        }

        private static bool TryReadTeleportRod(IList inventory, int slot, out TeleportRodCandidate candidate)
        {
            candidate = default(TeleportRodCandidate);
            if (inventory == null || slot < 0 || slot >= inventory.Count)
            {
                return false;
            }

            if (!TerrariaInputCompat.IsSupportedItemUseSlot(slot))
            {
                return false;
            }

            var item = inventory[slot];
            if (item == null)
            {
                return false;
            }

            int itemType;
            int stack;
            if (!TryReadInt(item, "type", out itemType) ||
                !TryReadInt(item, "stack", out stack) ||
                itemType <= 0 ||
                stack <= 0 ||
                !TerrariaTeleportRodCompat.IsTeleportRodItem(itemType))
            {
                return false;
            }

            var name = Convert.ToString(GetMember(item, "Name") ?? GetMember(item, "name")) ?? string.Empty;
            candidate = new TeleportRodCandidate
            {
                Slot = slot,
                ItemType = itemType,
                Stack = stack,
                Name = name,
                Priority = TerrariaTeleportRodCompat.GetTeleportRodPriority(itemType, name)
            };
            return true;
        }

        private static bool IsPlaceableFallDamageSafeTile(int createTile, int placeStyle)
        {
            switch (createTile)
            {
                case CobwebTileFallbackType:
                case CobwebReplicaTileType:
                case CloudTileType:
                case RainCloudTileType:
                case SnowCloudTileType:
                case LavaCloudTileType:
                case StarCloudTileType:
                case RainbowCloudTileType:
                case PinkSlimeBlockTileType:
                case SillyBalloonPinkTileType:
                case SillyBalloonPurpleTileType:
                case SillyBalloonGreenTileType:
                case PoopBlockTileType:
                    return true;
                case PlatformTileType:
                    return placeStyle == CloudPlatformStyle;
                default:
                    return false;
            }
        }

        private static void ApplyTeleportRodCandidate(MovementSafeLandingAnalysis analysis, TeleportRodCandidate candidate)
        {
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = candidate.Slot;
            analysis.TeleportRodItemType = candidate.ItemType;
            analysis.TeleportRodItemName = candidate.Name ?? string.Empty;
        }

        private struct TeleportRodCandidate
        {
            public int Slot;
            public int ItemType;
            public int Stack;
            public string Name;
            public int Priority;
        }

        private static int ReadTileLiquidAmount(object tile)
        {
            int liquid;
            if (TryReadInt(tile, "liquid", out liquid) || TryReadInt(tile, "LiquidAmount", out liquid))
            {
                return liquid;
            }

            return 0;
        }

        private static int ReadTileLiquidType(object tile)
        {
            int liquidType;
            if (TryReadInt(tile, "LiquidType", out liquidType) ||
                TryReadInt(tile, "liquidType", out liquidType) ||
                TryInvokeInt(tile, "liquidType", out liquidType))
            {
                return liquidType;
            }

            bool liquidFlag;
            if (TryInvokeBool(tile, "lava", out liquidFlag) && liquidFlag)
            {
                return 1;
            }

            if (TryInvokeBool(tile, "honey", out liquidFlag) && liquidFlag)
            {
                return 2;
            }

            return TryInvokeBool(tile, "shimmer", out liquidFlag) && liquidFlag ? 3 : 0;
        }

        private static bool IsTileActive(object tile)
        {
            bool value;
            if (TryInvokeBool(tile, "active", out value))
            {
                return value;
            }

            return TryReadBool(tile, "active", false);
        }

        private static bool IsTileInactive(object tile)
        {
            bool value;
            if (TryInvokeBool(tile, "inActive", out value) ||
                TryInvokeBool(tile, "inactive", out value))
            {
                return value;
            }

            return TryReadBool(tile, "inActive", false) || TryReadBool(tile, "inactive", false);
        }

        private static int ReadTileType(object tile)
        {
            int type;
            return TryReadInt(tile, "type", out type) ? type : 0;
        }

        private static int ReadTileSlope(object tile)
        {
            int slope;
            if (TryInvokeInt(tile, "slope", out slope) ||
                TryReadInt(tile, "slope", out slope) ||
                TryReadInt(tile, "Slope", out slope))
            {
                return slope;
            }

            return 0;
        }

        private static bool ReadTileHalfBrick(object tile)
        {
            bool halfBrick;
            if (TryInvokeBool(tile, "halfBrick", out halfBrick) ||
                TryReadBool(tile, "halfBrick", out halfBrick) ||
                TryReadBool(tile, "HalfBrick", out halfBrick))
            {
                return halfBrick;
            }

            return false;
        }

        private static int ReadPlatformStyle(object tile)
        {
            var frameY = ReadTileFrameY(tile);
            if (frameY < 0)
            {
                return 0;
            }

            return frameY / PlatformFrameHeight;
        }

        private static int ReadTileFrameY(object tile)
        {
            int frameY;
            if (TryReadInt(tile, "frameY", out frameY) || TryReadInt(tile, "FrameY", out frameY))
            {
                return frameY;
            }

            return 0;
        }

        private static bool TryResolveCurrentGrappleTarget(object player, out float targetX, out float targetY)
        {
            targetX = 0f;
            targetY = 0f;
            if (player == null)
            {
                return false;
            }

            float positionX;
            float positionY;
            if (!TryReadVectorMember(player, "position", out positionX, out positionY))
            {
                return false;
            }

            var width = TryReadInt(player, "width", 20);
            var height = TryReadInt(player, "height", 42);
            var gravityDirection = TryReadFloat(player, "gravDir", 1f);
            if (Math.Abs(gravityDirection) < 0.001f)
            {
                gravityDirection = 1f;
            }

            float velocityX;
            float velocityY;
            if (!TryReadVectorMember(player, "velocity", out velocityX, out velocityY))
            {
                velocityX = 0f;
                velocityY = 0f;
            }

            var fallingSpeed = Math.Max(velocityY * gravityDirection, MinimumDangerFallingSpeed);
            var probeAnalysis = new MovementSafeLandingAnalysis
            {
                PositionX = positionX,
                PositionY = positionY,
                Width = width,
                Height = height,
                GravityDirection = gravityDirection,
                FallingSpeed = fallingSpeed
            };

            int impactDistance;
            if (!TryFindImpactDistance(probeAnalysis, out impactDistance))
            {
                impactDistance = Math.Max(96, (int)(fallingSpeed * 12f));
            }

            targetX = positionX + width / 2f + velocityX * 6f;
            targetY = gravityDirection >= 0f
                ? positionY + height + impactDistance + 12f
                : positionY - impactDistance - 12f;
            return true;
        }

        private static bool IsQuickMountAction(string actionType)
        {
            return string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionType, "quickMount", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGrappleAction(string actionType)
        {
            return string.Equals(actionType, "grapple", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGravityFlipAction(string actionType)
        {
            return string.Equals(actionType, "gravity_flip", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actionType, "gravityFlip", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryIsGravityRestoreSafe(object player, float originalGravityDirection, out bool safeToRestore, out string reason)
        {
            safeToRestore = false;
            reason = string.Empty;
            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            var originalDirection = originalGravityDirection >= 0f ? 1f : -1f;
            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            if (!profile.PlayerControllable)
            {
                reason = "playerNotControllable";
                return true;
            }

            var currentDirection = profile.GravityDirection >= 0f ? 1f : -1f;
            if (Math.Abs(currentDirection - originalDirection) < 0.01f)
            {
                reason = "alreadyOriginalGravity";
                return true;
            }

            if (!profile.HasGravityGlobe)
            {
                reason = "gravityGlobeUnavailable";
                return true;
            }

            var originalDirectionSpeed = profile.VelocityY * originalDirection;
            if (originalDirectionSpeed > 1f)
            {
                reason = "restoreWouldContinueFalling";
                return true;
            }

            float positionX;
            float positionY;
            if (!TryReadVectorMember(player, "position", out positionX, out positionY))
            {
                reason = "positionUnavailable";
                return false;
            }

            var restoreProbe = new MovementSafeLandingAnalysis
            {
                PositionX = positionX,
                PositionY = positionY,
                Width = TryReadInt(player, "width", 20),
                Height = TryReadInt(player, "height", 42),
                GravityDirection = originalDirection,
                FallingSpeed = Math.Max(MinimumDangerFallingSpeed, Math.Abs(originalDirectionSpeed) + 1f)
            };

            int impactDistance;
            if (!TryFindImpactDistance(restoreProbe, out impactDistance))
            {
                reason = "originalGravityNoNearbySurface";
                return true;
            }

            if (impactDistance > GravityRestoreProbePixels)
            {
                reason = "originalGravitySurfaceTooFar:" + impactDistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            safeToRestore = true;
            reason = "originalGravitySurfaceNear:" + impactDistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        public static bool TryProbeLandingCollision(float x, float y, int width, int height, float gravityDirection, float fallingSpeed, out bool solid)
        {
            solid = false;
            var solidResolved = TrySolidOrTopSurfaceCollision(x, y, width, height, out var solidHit);
            if (solidResolved && solidHit)
            {
                solid = true;
                return true;
            }

            var direction = gravityDirection >= 0f ? 1f : -1f;
            var slopeProbeY = y + 4f * direction;
            if (TrySolidOrTopSurfaceCollision(x, slopeProbeY, width, height, out var slopeSolidHit) && slopeSolidHit)
            {
                solid = true;
                return true;
            }

            var slopeProbeDeepY = y + 8f * direction;
            if (TrySolidOrTopSurfaceCollision(x, slopeProbeDeepY, width, height, out var slopeDeepSolidHit) && slopeDeepSolidHit)
            {
                solid = true;
                return true;
            }

            var tileCollisionResolved = TryTileCollisionImpact(x, y, width, height, gravityDirection, fallingSpeed, out var tileCollisionHit);
            if (tileCollisionResolved && tileCollisionHit)
            {
                solid = true;
                return true;
            }

            if (TryManualLandingSurfaceImpact(x, y, width, height, gravityDirection, fallingSpeed, out var manualSurfaceHit))
            {
                solid = manualSurfaceHit;
                return true;
            }

            if (solidResolved || tileCollisionResolved)
            {
                solid = false;
                return true;
            }

            return false;
        }

        // Kept for backward compat; delegates to the surface-hit overload.
        private static bool TryManualLandingSurfaceImpact(float x, float y, int width, int height, float gravityDirection, float fallingSpeed, out bool solid)
        {
            var hit = TryManualLandingSurfaceImpact(x, y, width, height, gravityDirection, fallingSpeed);
            solid = hit != null && hit.Found;
            return true;
        }

        private static MovementLandingSurfaceHit TryManualLandingSurfaceImpact(float x, float y, int width, int height, float gravityDirection, float fallingSpeed, float velocityX = 0f)
        {
            if (gravityDirection < 0f || fallingSpeed <= 0.05f)
            {
                return new MovementLandingSurfaceHit();
            }

            Array tiles;
            if (!TryGetMainTileArray(out tiles))
            {
                return new MovementLandingSurfaceHit();
            }

            var mainType = ResolveMainType();
            var solidTiles = GetStaticMember(mainType, "tileSolid") as Array;
            var solidTopTiles = GetStaticMember(mainType, "tileSolidTop") as Array;
            var setsType = FindType("Terraria.ID.TileID+Sets");
            var platformTiles = GetStaticMember(setsType, "Platforms") as Array;
            var bottomY = y + height;
            var topY = y;
            var tolerance = Math.Max(8f, Math.Min(24f, Math.Abs(fallingSpeed) + 6f));
            var leftTile = (int)Math.Floor((x + 1f) / 16f);
            var rightTile = (int)Math.Floor((x + Math.Max(1, width - 1)) / 16f);
            var topTile = (int)Math.Floor((bottomY - tolerance) / 16f) - 1;
            var bottomTile = (int)Math.Floor((bottomY + tolerance) / 16f) + 1;
            var left = x + 1f;
            var right = x + Math.Max(1, width) - 1f;
            var center = (left + right) / 2f;
            var samples = BuildLandingSurfaceSamples(x, width, velocityX);
            MovementLandingSurfaceHit bestHit = null;

            for (var tileY = topTile; tileY <= bottomTile; tileY++)
            {
                for (var tileX = leftTile; tileX <= rightTile; tileX++)
                {
                    var tile = GetTile(tiles, tileX, tileY);
                    if (!IsLandingSurfaceTile(tile, solidTiles, solidTopTiles, platformTiles))
                    {
                        continue;
                    }

                    for (var sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
                    {
                        var sample = samples[sampleIndex];
                        var sampleX = sample.X;
                        var tileLeft = tileX * 16f;
                        if (sampleX < tileLeft - 0.25f || sampleX > tileLeft + 16.25f)
                        {
                            continue;
                        }

                        float surfaceY;
                        if (!TryResolveTileSurfaceY(tileX, tileY, tile, sampleX, out surfaceY))
                        {
                            continue;
                        }

                        if (!(bottomY >= surfaceY - 1f && bottomY <= surfaceY + tolerance && topY < surfaceY + 16f))
                        {
                            continue;
                        }

                        var slope = ReadTileSlope(tile);
                        var surfaceKind = ResolveLandingSurfaceKind(tile, solidTopTiles, platformTiles);
                        var slopeDirection = ResolveSlopeDirection(slope);
                        var movingIntoSlope = IsMovingIntoSlope(slopeDirection, velocityX);
                        var movingWithSlope = IsMovingWithSlope(slopeDirection, velocityX);
                        var candidate = new MovementLandingSurfaceHit
                        {
                            Found = true,
                            ImpactDistancePixels = 0,
                            ImpactTicks = fallingSpeed > 0.001f ? 0f : -1f,
                            ProjectedPlayerLeftX = x,
                            ProjectedPlayerRightX = x + width,
                            ProjectedPlayerBottomY = bottomY,
                            ContactWorldX = sampleX,
                            ContactWorldY = surfaceY,
                            ContactTileX = tileX,
                            ContactTileY = tileY,
                            SurfaceKind = surfaceKind,
                            SlopeType = slope,
                            SlopeDirection = slopeDirection,
                            ContactSample = sample.Label,
                            MovingIntoSlope = movingIntoSlope,
                            MovingWithSlope = movingWithSlope,
                            Summary = surfaceKind +
                                      (slopeDirection.Length > 0 ? " " + slopeDirection : "") +
                                      " contact=" + sample.Label +
                                      " movingIntoSlope=" + BoolText(movingIntoSlope) +
                                      " movingWithSlope=" + BoolText(movingWithSlope) +
                                      " world=(" + sampleX.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) +
                                      "," + surfaceY.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ")"
                        };

                        if (IsBetterManualSurfaceHit(candidate, bestHit, velocityX, center, gravityDirection))
                        {
                            bestHit = candidate;
                        }
                    }
                }
            }

            return bestHit ?? new MovementLandingSurfaceHit();
        }

        private struct LandingSurfaceSample
        {
            public LandingSurfaceSample(float x, string label, int priority)
            {
                X = x;
                Label = label ?? string.Empty;
                Priority = priority;
            }

            public float X;
            public string Label;
            public int Priority;
        }

        private static LandingSurfaceSample[] BuildLandingSurfaceSamples(float x, int width, float velocityX)
        {
            var samples = new List<LandingSurfaceSample>();
            var left = x + 1f;
            var right = x + Math.Max(1, width) - 1f;
            var center = (left + right) / 2f;

            AddLandingSurfaceSample(samples, left, "left_foot", 2);
            AddLandingSurfaceSample(samples, center, "center_foot", 2);
            AddLandingSurfaceSample(samples, right, "right_foot", 2);

            if (velocityX < -0.01f)
            {
                AddLandingSurfaceSample(samples, left, "leading_foot", 4);
            }
            else if (velocityX > 0.01f)
            {
                AddLandingSurfaceSample(samples, right, "leading_foot", 4);
            }

            var footLeftTile = (int)Math.Floor((x + 0.5f) / 16f);
            var footRightTile = (int)Math.Floor((x + Math.Max(1, width) - 0.5f) / 16f);
            for (var tileCol = footLeftTile; tileCol <= footRightTile; tileCol++)
            {
                AddLandingSurfaceSample(samples, ClampFloat(tileCol * 16f + 8f, left, right), "tile_segment", 1);
            }

            return samples.ToArray();
        }

        private static void AddLandingSurfaceSample(List<LandingSurfaceSample> samples, float x, string label, int priority)
        {
            for (var index = 0; index < samples.Count; index++)
            {
                var existing = samples[index];
                if (Math.Abs(existing.X - x) < 0.25f)
                {
                    if (priority > existing.Priority)
                    {
                        samples[index] = new LandingSurfaceSample(existing.X, label, priority);
                    }

                    return;
                }
            }

            samples.Add(new LandingSurfaceSample(x, label, priority));
        }

        private static bool IsBetterManualSurfaceHit(
            MovementLandingSurfaceHit candidate,
            MovementLandingSurfaceHit current,
            float velocityX,
            float playerCenterX,
            float gravityDirection)
        {
            if (candidate == null || !candidate.Found)
            {
                return false;
            }

            if (current == null || !current.Found)
            {
                return true;
            }

            if (candidate.MovingIntoSlope != current.MovingIntoSlope)
            {
                return candidate.MovingIntoSlope;
            }

            if (Math.Abs(candidate.ContactWorldY - current.ContactWorldY) > 0.25f)
            {
                return gravityDirection >= 0f
                    ? candidate.ContactWorldY < current.ContactWorldY
                    : candidate.ContactWorldY > current.ContactWorldY;
            }

            var candidateLeading = string.Equals(candidate.ContactSample, "leading_foot", StringComparison.Ordinal);
            var currentLeading = string.Equals(current.ContactSample, "leading_foot", StringComparison.Ordinal);
            if (candidateLeading != currentLeading)
            {
                return candidateLeading;
            }

            var candidateCenterDistance = Math.Abs(candidate.ContactWorldX - playerCenterX);
            var currentCenterDistance = Math.Abs(current.ContactWorldX - playerCenterX);
            if (Math.Abs(candidateCenterDistance - currentCenterDistance) > 0.25f)
            {
                return candidateCenterDistance < currentCenterDistance;
            }

            if (velocityX < -0.01f && candidate.ContactTileX != current.ContactTileX)
            {
                return candidate.ContactTileX < current.ContactTileX;
            }

            if (velocityX > 0.01f && candidate.ContactTileX != current.ContactTileX)
            {
                return candidate.ContactTileX > current.ContactTileX;
            }

            return false;
        }

        private static string ResolveLandingSurfaceKind(object tile, Array solidTopTiles, Array platformTiles)
        {
            if (tile == null)
            {
                return "unknown";
            }

            if (ReadTileHalfBrick(tile))
            {
                return "half_brick";
            }

            var slope = ReadTileSlope(tile);
            if (slope >= 1 && slope <= 4)
            {
                return "slope";
            }

            var tileType = ReadTileType(tile);
            if (tileType == PlatformTileType || ReadStaticBoolArray(platformTiles, tileType))
            {
                return "platform";
            }

            if (ReadStaticBoolArray(solidTopTiles, tileType))
            {
                return "solid_top";
            }

            return "full_block";
        }

        private static string ResolveSlopeDirection(int slope)
        {
            if (slope == 1 || slope == 3)
            {
                return "left_high_right_low";
            }

            if (slope == 2 || slope == 4)
            {
                return "left_low_right_high";
            }

            return "none";
        }

        private static bool IsMovingIntoSlope(string slopeDirection, float velocityX)
        {
            if (string.Equals(slopeDirection, "left_high_right_low", StringComparison.Ordinal))
            {
                return velocityX < -0.01f;
            }

            if (string.Equals(slopeDirection, "left_low_right_high", StringComparison.Ordinal))
            {
                return velocityX > 0.01f;
            }

            return false;
        }

        private static bool IsMovingWithSlope(string slopeDirection, float velocityX)
        {
            if (string.Equals(slopeDirection, "left_high_right_low", StringComparison.Ordinal))
            {
                return velocityX > 0.01f;
            }

            if (string.Equals(slopeDirection, "left_low_right_high", StringComparison.Ordinal))
            {
                return velocityX < -0.01f;
            }

            return false;
        }

        private static string BoolText(bool value)
        {
            return value ? "true" : "false";
        }

        // Extracted helper: resolve tile surface Y for a given sample X.
        private static bool TryResolveTileSurfaceY(int tileX, int tileY, object tile, float sampleX, out float surfaceY)
        {
            surfaceY = 0f;
            if (tile == null || !IsTileActive(tile) || IsTileInactive(tile))
            {
                return false;
            }

            var tileTop = tileY * 16f;
            var slope = ReadTileSlope(tile);

            if (ReadTileHalfBrick(tile))
            {
                surfaceY = tileTop + 8f;
                return true;
            }

            if (slope == 0)
            {
                surfaceY = tileTop;
                return true;
            }

            if (slope == 1 || slope == 3)
            {
                surfaceY = tileTop + ClampFloat(sampleX - tileX * 16f, 0f, 16f);
                return true;
            }

            if (slope == 2 || slope == 4)
            {
                surfaceY = tileTop + 16f - ClampFloat(sampleX - tileX * 16f, 0f, 16f);
                return true;
            }

            return false;
        }

        private static float ResolveGrappleHookSpeed(MovementSafeLandingAnalysis analysis)
        {
            if (analysis == null)
            {
                return 0f;
            }

            if (analysis.HasEquippedGrapple)
            {
                if (analysis.EquippedGrappleShootSpeed > 0f)
                {
                    return analysis.EquippedGrappleShootSpeed;
                }

                if (analysis.EquippedGrappleItemType > 0 &&
                    GrappleHookSpeedTable.TryGetValue(analysis.EquippedGrappleItemType, out var equippedSpeed))
                {
                    return equippedSpeed;
                }

                return 13f;
            }

            if (analysis.HasInventoryGrapple)
            {
                if (analysis.InventoryGrappleShootSpeed > 0f)
                {
                    return analysis.InventoryGrappleShootSpeed;
                }

                if (analysis.InventoryGrappleItemType > 0 &&
                    GrappleHookSpeedTable.TryGetValue(analysis.InventoryGrappleItemType, out var inventorySpeed))
                {
                    return inventorySpeed;
                }

                return 13f;
            }

            return 13f;
        }

        private static bool IsFallingAcrossTileSurface(float sampleX, float topY, float bottomY, int tileX, int tileY, object tile, float tolerance)
        {
            var tileLeft = tileX * 16f;
            var tileTop = tileY * 16f;
            var slope = ReadTileSlope(tile);
            var surfaceY = tileTop;
            if (ReadTileHalfBrick(tile))
            {
                surfaceY = tileTop + 8f;
            }
            else if (slope == 1)
            {
                surfaceY = tileTop + ClampFloat(sampleX - tileLeft, 0f, 16f);
            }
            else if (slope == 2)
            {
                surfaceY = tileTop + 16f - ClampFloat(sampleX - tileLeft, 0f, 16f);
            }
            else if (slope > 2)
            {
                return false;
            }

            return bottomY >= surfaceY - 1f &&
                   bottomY <= surfaceY + tolerance &&
                   topY < surfaceY + 16f;
        }

        private static bool IsLandingSurfaceTile(object tile, Array solidTiles, Array solidTopTiles, Array platformTiles)
        {
            if (tile == null || !IsTileActive(tile) || IsTileInactive(tile))
            {
                return false;
            }

            var tileType = ReadTileType(tile);
            return ReadStaticBoolArray(solidTiles, tileType) ||
                   ReadStaticBoolArray(solidTopTiles, tileType) ||
                   tileType == PlatformTileType ||
                   ReadStaticBoolArray(platformTiles, tileType);
        }

        private static bool ReadStaticBoolArray(Array values, int index)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
                return false;
            }

            try
            {
                var raw = values.GetValue(index);
                return raw is bool && (bool)raw;
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySolidOrTopSurfaceCollision(float x, float y, int width, int height, out bool solid)
        {
            solid = false;
            if (!EnsureSolidCollisionTopSurfaceMethod())
            {
                return TrySolidCollision(x, y, width, height, out solid);
            }

            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (vectorType == null)
            {
                return Fail("Microsoft.Xna.Framework.Vector2 type unavailable.");
            }

            try
            {
                var vector = Activator.CreateInstance(vectorType, new object[] { x, y });
                var result = _solidCollisionTopSurfaceMethod.Invoke(null, new object[] { vector, width, height, true });
                if (result is bool)
                {
                    solid = (bool)result;
                    return true;
                }

                return Fail("Collision.SolidCollision(Vector2,int,int,bool) returned a non-bool result.");
            }
            catch (Exception error)
            {
                return Fail("Collision.SolidCollision(Vector2,int,int,bool) failed: " + error.Message);
            }
        }

        private static bool TryTileCollisionImpact(float x, float y, int width, int height, float gravityDirection, float fallingSpeed, out bool collided)
        {
            collided = false;
            if (!EnsureTileCollisionMethod())
            {
                return false;
            }

            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (vectorType == null)
            {
                return Fail("Microsoft.Xna.Framework.Vector2 type unavailable.");
            }

            var direction = gravityDirection >= 0f ? 1 : -1;
            var requestedSpeed = Math.Max(
                ImpactCollisionProbeVelocity,
                Math.Min(24f, Math.Abs(fallingSpeed) + 2f));
            var requestedVelocityY = requestedSpeed * direction;

            try
            {
                var position = Activator.CreateInstance(vectorType, new object[] { x, y });
                var velocity = Activator.CreateInstance(vectorType, new object[] { 0f, requestedVelocityY });
                var result = _tileCollisionMethod.Invoke(null, new object[]
                {
                    position,
                    velocity,
                    width,
                    height,
                    false,
                    false,
                    direction,
                    false,
                    false,
                    false
                });
                if (result == null || !TryReadVector2(result, out var returnedX, out var returnedY))
                {
                    return Fail("Collision.TileCollision returned an unreadable Vector2 result.");
                }

                collided = returnedY * direction < requestedVelocityY * direction - 0.01f || Math.Abs(returnedX) > 0.01f;
                return true;
            }
            catch (Exception error)
            {
                return Fail("Collision.TileCollision failed: " + error.Message);
            }
        }

        private static bool TrySolidCollision(float x, float y, int width, int height, out bool solid)
        {
            solid = false;
            if (!EnsureSolidCollisionMethod())
            {
                return false;
            }

            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (vectorType == null)
            {
                return Fail("Microsoft.Xna.Framework.Vector2 type unavailable.");
            }

            try
            {
                var vector = Activator.CreateInstance(vectorType, new object[] { x, y });
                var result = _solidCollisionMethod.Invoke(null, new object[] { vector, width, height });
                if (result is bool)
                {
                    solid = (bool)result;
                    return true;
                }

                return Fail("Collision.SolidCollision returned a non-bool result.");
            }
            catch (Exception error)
            {
                return Fail("Collision.SolidCollision failed: " + error.Message);
            }
        }

        private static bool EnsureSolidCollisionMethod()
        {
            if (_collisionMethodResolved)
            {
                return _solidCollisionMethod != null;
            }

            _collisionMethodResolved = true;
            var collisionType = FindType("Terraria.Collision");
            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (collisionType == null || vectorType == null)
            {
                _lastError = "Terraria.Collision or Vector2 type unavailable.";
                return false;
            }

            _solidCollisionMethod = collisionType.GetMethod(
                "SolidCollision",
                StaticFlags,
                null,
                new[] { vectorType, typeof(int), typeof(int) },
                null);
            if (_solidCollisionMethod == null)
            {
                _lastError = "Terraria.Collision.SolidCollision(Vector2,int,int) not found.";
                return false;
            }

            return true;
        }

        private static bool EnsureTileCollisionMethod()
        {
            if (_tileCollisionMethodResolved)
            {
                return _tileCollisionMethod != null;
            }

            _tileCollisionMethodResolved = true;
            var collisionType = FindType("Terraria.Collision");
            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (collisionType == null || vectorType == null)
            {
                _lastError = "Terraria.Collision or Vector2 type unavailable.";
                return false;
            }

            _tileCollisionMethod = collisionType.GetMethod(
                "TileCollision",
                StaticFlags,
                null,
                new[]
                {
                    vectorType,
                    vectorType,
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(bool),
                    typeof(int),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)
                },
                null);
            return _tileCollisionMethod != null;
        }

        private static bool EnsureSolidCollisionTopSurfaceMethod()
        {
            if (_solidCollisionTopSurfaceMethodResolved)
            {
                return _solidCollisionTopSurfaceMethod != null;
            }

            _solidCollisionTopSurfaceMethodResolved = true;
            var collisionType = FindType("Terraria.Collision");
            var vectorType = TerrariaRuntimeTypes.Vector2Type;
            if (collisionType == null || vectorType == null)
            {
                _lastError = "Terraria.Collision or Vector2 type unavailable.";
                return false;
            }

            _solidCollisionTopSurfaceMethod = collisionType.GetMethod(
                "SolidCollision",
                StaticFlags,
                null,
                new[] { vectorType, typeof(int), typeof(int), typeof(bool) },
                null);
            return _solidCollisionTopSurfaceMethod != null;
        }

        private static bool TryReadMountNoFallDamage(object player)
        {
            try
            {
                var mount = GetMember(player, "mount");
                if (mount == null)
                {
                    return false;
                }

                var active = TryReadBool(mount, "Active", false) || TryReadBool(mount, "_active", false);
                if (!active)
                {
                    return false;
                }

                var mountType = TryReadInt(mount, "Type", TryReadInt(mount, "_type", -1));
                if (mountType < 0)
                {
                    return false;
                }

                var mountTypeType = FindType("Terraria.Mount");
                if (mountTypeType == null)
                {
                    return false;
                }

                var mounts = GetStaticMember(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                return data != null && TryReadFloat(data, "fallDamage", 1f) <= 0f;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadVectorMember(object instance, string name, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            var vector = GetMember(instance, name);
            if (vector == null)
            {
                return false;
            }

            return TryReadVector2(vector, out x, out y);
        }

        private static bool TryReadVector2(object vector, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (vector == null)
            {
                return false;
            }

            return TryReadFloat(vector, "X", out x) && TryReadFloat(vector, "Y", out y);
        }

        private static bool TryReadBool(object instance, string name, bool fallback)
        {
            bool value;
            return TryReadBool(instance, name, out value) ? value : fallback;
        }

        private static bool TryReadBool(object instance, string name, out bool value)
        {
            value = false;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int TryReadInt(object instance, string name, int fallback)
        {
            int value;
            return TryReadInt(instance, name, out value) ? value : fallback;
        }

        private static bool TryReadInt(object instance, string name, out int value)
        {
            value = 0;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static float TryReadFloat(object instance, string name, float fallback)
        {
            float value;
            return TryReadFloat(instance, name, out value) ? value : fallback;
        }

        private static bool TryReadFloat(object instance, string name, out float value)
        {
            value = 0f;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToSingle(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(instance.GetType(), name, false, out field) && field != null)
                {
                    return field.GetValue(instance);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(instance.GetType(), name, false, out property) && property != null && property.CanRead)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field) && field != null)
                {
                    return field.GetValue(null);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property != null && property.CanRead)
                {
                    return property.GetValue(null, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool TryInvokeInt(object instance, string methodName, out int value)
        {
            value = 0;
            object raw;
            if (!TryInvokeNoArg(instance, methodName, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInvokeBool(object instance, string methodName, out bool value)
        {
            value = false;
            object raw;
            if (!TryInvokeNoArg(instance, methodName, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInvokeNoArg(object instance, string methodName, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var method = instance.GetType().GetMethod(
                    methodName,
                    InstanceFlags,
                    null,
                    Type.EmptyTypes,
                    null);
                if (method == null)
                {
                    return false;
                }

                value = method.Invoke(instance, new object[0]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool ClearError()
        {
            _lastError = string.Empty;
            return true;
        }

        private static bool Fail(string message)
        {
            _lastError = message ?? string.Empty;
            return false;
        }

        private enum SafeLandingJumpPulsePhase
        {
            Release,
            PressHold,
            FinalRelease,
            CancelPressHold,
            CancelFinalRelease,
            Completed,
            Failed
        }

        private sealed class SafeLandingJumpPulseState
        {
            public Guid RequestId { get; set; }
            public string Strategy { get; set; }
            public string ActionType { get; set; }
            public bool ApplyRocketRelease { get; set; }
            public bool SuppressDown { get; set; }
            public int HoldTargetTicks { get; set; }
            public int HoldTicks { get; set; }
            public bool ReleaseApplied { get; set; }
            public bool PressApplied { get; set; }
            public bool FinalReleaseApplied { get; set; }
            public bool ImmediateCancelAfterPress { get; set; }
            public bool CancelPressApplied { get; set; }
            public bool CancelFinalReleaseApplied { get; set; }
            public bool Completed { get; set; }
            public bool Failed { get; set; }
            public string Status { get; set; }
            public string LastMessage { get; set; }
            public string LastApplySite { get; set; }
            public bool TargetWorldKnown { get; set; }
            public float TargetWorldX { get; set; }
            public float TargetWorldY { get; set; }
            public bool MouseTargetCaptured { get; set; }
            public bool MouseTargetRestoreAttempted { get; set; }
            public bool MouseTargetRestoreSucceeded { get; set; }
            public string MouseTargetRestoreMessage { get; set; }
            public MouseTargetInputState MouseTargetRestoreState { get; set; }
            public DateTime StartedUtc { get; set; }
            public DateTime? LastAppliedUtc { get; set; }
            public DateTime? CompletedUtc { get; set; }
            public SafeLandingJumpPulsePhase Phase { get; set; }

            public bool IsTerminal
            {
                get { return Completed || Failed || Phase == SafeLandingJumpPulsePhase.Completed || Phase == SafeLandingJumpPulsePhase.Failed; }
            }

            public SafeLandingJumpPulseSnapshot ToSnapshot()
            {
                return new SafeLandingJumpPulseSnapshot
                {
                    RequestId = RequestId,
                    Strategy = Strategy ?? string.Empty,
                    ActionType = ActionType ?? string.Empty,
                    ApplyRocketRelease = ApplyRocketRelease,
                    SuppressDown = SuppressDown,
                    HoldTargetTicks = HoldTargetTicks,
                    HoldTicks = HoldTicks,
                    ReleaseApplied = ReleaseApplied,
                    PressApplied = PressApplied,
                    FinalReleaseApplied = FinalReleaseApplied,
                    ImmediateCancelAfterPress = ImmediateCancelAfterPress,
                    CancelPressApplied = CancelPressApplied,
                    CancelFinalReleaseApplied = CancelFinalReleaseApplied,
                    Completed = Completed,
                    Failed = Failed,
                    Active = !IsTerminal,
                    Status = Status ?? string.Empty,
                    Phase = Phase.ToString(),
                    LastMessage = LastMessage ?? string.Empty,
                    LastApplySite = LastApplySite ?? string.Empty,
                    TargetWorldKnown = TargetWorldKnown,
                    TargetWorldX = TargetWorldX,
                    TargetWorldY = TargetWorldY,
                    MouseTargetCaptured = MouseTargetCaptured,
                    MouseTargetRestoreAttempted = MouseTargetRestoreAttempted,
                    MouseTargetRestoreSucceeded = MouseTargetRestoreSucceeded,
                    MouseTargetRestoreMessage = MouseTargetRestoreMessage ?? string.Empty,
                    StartedUtc = StartedUtc,
                    LastAppliedUtc = LastAppliedUtc,
                    CompletedUtc = CompletedUtc
                };
            }
        }
    }

    public sealed class SafeLandingJumpPulseSnapshot
    {
        public Guid RequestId { get; set; }
        public string Strategy { get; set; }
        public string ActionType { get; set; }
        public bool ApplyRocketRelease { get; set; }
        public bool SuppressDown { get; set; }
        public int HoldTargetTicks { get; set; }
        public int HoldTicks { get; set; }
        public bool ReleaseApplied { get; set; }
        public bool PressApplied { get; set; }
        public bool FinalReleaseApplied { get; set; }
        public bool ImmediateCancelAfterPress { get; set; }
        public bool CancelPressApplied { get; set; }
        public bool CancelFinalReleaseApplied { get; set; }
        public bool Completed { get; set; }
        public bool Failed { get; set; }
        public bool Active { get; set; }
        public string Status { get; set; }
        public string Phase { get; set; }
        public string LastMessage { get; set; }
        public string LastApplySite { get; set; }
        public bool TargetWorldKnown { get; set; }
        public float TargetWorldX { get; set; }
        public float TargetWorldY { get; set; }
        public bool MouseTargetCaptured { get; set; }
        public bool MouseTargetRestoreAttempted { get; set; }
        public bool MouseTargetRestoreSucceeded { get; set; }
        public string MouseTargetRestoreMessage { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? LastAppliedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
    }
}
