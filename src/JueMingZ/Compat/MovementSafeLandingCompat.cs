using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.Movement;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace JueMingZ.Compat
{
    public static partial class MovementSafeLandingCompat
    {
        // Safe-landing reads and queued pulses must never repair fall state
        // directly; rescue remains input/equipment based.
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
        private static SolidCollisionDelegate _solidCollisionDelegate;
        private static bool _solidCollisionTopSurfaceMethodResolved;
        private static MethodInfo _solidCollisionTopSurfaceMethod;
        private static SolidCollisionTopSurfaceDelegate _solidCollisionTopSurfaceDelegate;
        private static bool _tileCollisionMethodResolved;
        private static MethodInfo _tileCollisionMethod;
        private static TileCollisionDelegate _tileCollisionDelegate;
        private static bool _typedSolidCollisionDisabled;
        private static bool _typedSolidTopSurfaceCollisionDisabled;
        private static bool _typedTileCollisionDisabled;
        private static int _lastCollisionFastPath;
        private static readonly object CollisionCacheSyncRoot = new object();
        private static TileStaticTableCache _tileStaticTableCache;
        private static Type _mainTypeOverrideForTesting;
        private static bool _slotUsabilityMethodResolved;
        private static MethodInfo _slotUsabilityMethod;
        private static string _lastError = string.Empty;
        private static readonly object PulseSyncRoot = new object();
        private static SafeLandingJumpPulseState _queuedJumpPulse;
        private static bool _playerUpdateHookInstalled;
        private static string _playerUpdateHookMessage = string.Empty;
        private static long _landingProbeCount;
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

        private delegate bool SolidCollisionDelegate(XnaVector2 position, int width, int height);

        private delegate bool SolidCollisionTopSurfaceDelegate(XnaVector2 position, int width, int height, bool acceptTopSurfaces);

        private delegate XnaVector2 TileCollisionDelegate(
            XnaVector2 position,
            XnaVector2 velocity,
            int width,
            int height,
            bool fallThrough,
            bool fall2,
            int gravDir,
            bool damage,
            bool noLiquid,
            bool waterWalk);

        private const int CollisionPathNone = 0;
        private const int CollisionPathTypedSolidTop = 1;
        private const int CollisionPathTypedSolid = 2;
        private const int CollisionPathTypedTile = 3;
        private const int CollisionPathDelegateSolidTop = 4;
        private const int CollisionPathDelegateSolid = 5;
        private const int CollisionPathDelegateTile = 6;
        private const int CollisionPathReflectionSolidTop = 7;
        private const int CollisionPathReflectionSolid = 8;
        private const int CollisionPathReflectionTile = 9;
        private const int CollisionPathManualSurface = 10;
        private const int CollisionPathUnavailable = 11;

        public static string LastError
        {
            get { return _lastError; }
        }

        public static string CollisionFastPathStatus
        {
            get { return CollisionFastPathToString(_lastCollisionFastPath); }
        }

        public static long LandingProbeCount
        {
            get { return Interlocked.Read(ref _landingProbeCount); }
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

        internal static void InvalidateCollisionFastPathCaches(string reason)
        {
            lock (CollisionCacheSyncRoot)
            {
                _tileStaticTableCache = null;
                _collisionMethodResolved = false;
                _solidCollisionMethod = null;
                _solidCollisionDelegate = null;
                _solidCollisionTopSurfaceMethodResolved = false;
                _solidCollisionTopSurfaceMethod = null;
                _solidCollisionTopSurfaceDelegate = null;
                _tileCollisionMethodResolved = false;
                _tileCollisionMethod = null;
                _tileCollisionDelegate = null;
                _typedSolidCollisionDisabled = false;
                _typedSolidTopSurfaceCollisionDisabled = false;
                _typedTileCollisionDisabled = false;
                _lastCollisionFastPath = CollisionPathNone;
                Interlocked.Exchange(ref _landingProbeCount, 0);
            }
        }

        internal static void ResetCollisionFastPathCachesForTesting()
        {
            InvalidateCollisionFastPathCaches("test");
        }

        internal static void SetMainTypeForTesting(Type mainType)
        {
            lock (CollisionCacheSyncRoot)
            {
                _mainTypeOverrideForTesting = mainType;
                _tileStaticTableCache = null;
                _lastCollisionFastPath = CollisionPathNone;
            }
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

