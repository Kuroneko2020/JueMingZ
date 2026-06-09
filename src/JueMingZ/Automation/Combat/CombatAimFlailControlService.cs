using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    public static partial class CombatAimFlailControlService
    {
        private const int PulseCooldownTicks = 6;
        private const int StuckRecoveryTicks = 8;
        private const int ReleaseAimWindowTicks = 3;
        private const int CachedReleaseAimMaxAgeTicks = 120;
        private const int FlailComboPressAimMaxAgeTicks = 20;
        private static readonly CombatAimFlailReleaseAimCache ReleaseAimCache = new CombatAimFlailReleaseAimCache();
        private static readonly CombatAimFlailProjectileTracker ProjectileTracker = new CombatAimFlailProjectileTracker();
        private static readonly CombatAimFlailCollisionDetector CollisionDetector = new CombatAimFlailCollisionDetector();
        private static readonly CombatAimFlailDiagnosticsPublisher DiagnosticsPublisher = new CombatAimFlailDiagnosticsPublisher();
        private static long _cooldownUntilTick;
        private static bool _physicalUseItemHeldLastTick;
        private static int _releaseAimTicksRemaining;
        private static bool _releaseInFlight;
        private static string _state = FlailControlStates.Idle;

        public static void Update()
        {
            var diagnostics = CombatAimFlailDiagnostics.Empty();
            try
            {
                string blockedReason;
                if (!TryReadFlailRuntimeReady(out blockedReason))
                {
                    PublishBlocked(diagnostics, blockedReason);
                    return;
                }

                var frame = new CombatAimFlailRuntimeFrame();
                if (!TryReadFlailSettings(ref frame, out blockedReason))
                {
                    PublishBlocked(diagnostics, blockedReason);
                    return;
                }

                if (!TryReadFlailLocalPlayer(ref frame, out blockedReason))
                {
                    PublishBlocked(diagnostics, blockedReason);
                    return;
                }

                if (!TryReadFlailPhysicalInput(ref frame, out blockedReason))
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Idle;
                    diagnostics.BlockedReason = blockedReason;
                    Publish(diagnostics);
                    return;
                }

                ReadFlailUiContext(ref frame);
                if (TryResolveFlailUiBlocked(ref frame, out blockedReason))
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Disabled;
                    diagnostics.BlockedReason = blockedReason;
                    Publish(diagnostics);
                    return;
                }

                frame.PhysicalReleasePending = UpdatePhysicalUseItemReleaseState(frame.PhysicalHeld);
                frame.ReleaseInFlight = _releaseInFlight;
                diagnostics.PhysicalUseItemHeld = frame.PhysicalHeld;
                diagnostics.PhysicalReleasePending = frame.PhysicalReleasePending;
                // Keep this idle gate before profile reads, ballistic solve, and
                // projectile scans; inactive frames must stay cheap.
                if (!frame.PhysicalHeld && !frame.PhysicalReleasePending && !frame.ReleaseInFlight)
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Idle;
                    diagnostics.BlockedReason = "noActiveFlailUse";
                    Publish(diagnostics);
                    return;
                }

                if (!TryReadFlailWeaponProfile(ref frame, out blockedReason))
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Disabled;
                    diagnostics.BlockedReason = blockedReason;
                    Publish(diagnostics);
                    return;
                }

                diagnostics.ItemType = frame.Profile.ItemType;
                diagnostics.ItemName = frame.Profile.Name ?? string.Empty;

                ReadFlailEligibility(ref frame);
                diagnostics.Eligible = frame.Eligibility.Eligible;
                diagnostics.Reason = frame.Eligibility.Reason;
                diagnostics.ProjectileAiStyle = frame.ProjectileAiStyle;

                if (!frame.Eligibility.Eligible)
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Disabled;
                    diagnostics.BlockedReason = frame.Eligibility.Reason;
                    Publish(diagnostics);
                    return;
                }

                diagnostics.Active = true;

                ReadFlailTick(ref frame);

                ReadFlailCurrentSelection(ref frame);
                if (frame.CurrentTargetAvailable)
                {
                    UpdateCachedReleaseAim(frame.Player, frame.Profile, frame.Prepared, frame.Selection, frame.Settings, frame.Tick);
                }

                FlailCachedReleaseAim cachedReleaseAim;
                frame.CachedReleaseAimAvailable = TryGetCachedReleaseAim(frame.Profile, frame.Tick, out cachedReleaseAim);
                diagnostics.CachedReleaseAim = frame.CachedReleaseAimAvailable;
                diagnostics.CachedReleaseAimAgeTicks = cachedReleaseAim == null ? -1 : ComputeCachedReleaseAimAge(cachedReleaseAim, frame.Tick);
                diagnostics.CachedReleaseAimReason = frame.CachedReleaseAimAvailable
                    ? "available"
                    : ResolveCachedReleaseAimUnavailableReason(frame.Profile, frame.Tick);

                if (!frame.PhysicalHeld && !frame.PhysicalReleasePending && !frame.ReleaseInFlight)
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Idle;
                    diagnostics.BlockedReason = "notUsingItem";
                    Publish(diagnostics);
                    return;
                }

                frame.InCooldown = frame.Tick > 0 && frame.Tick < _cooldownUntilTick;

                if (!frame.CurrentTargetAvailable && !frame.CachedReleaseAimAvailable)
                {
                    diagnostics.State = frame.PhysicalHeld ? FlailControlStates.SpinHold : FlailControlStates.Disabled;
                    diagnostics.BlockedReason = "targetUnavailable:noCachedReleaseAim";
                    AdvanceReleaseWindow(frame.PhysicalHeld, false, CombatAimFlailProjectileFrame.None());
                    Publish(diagnostics);
                    return;
                }

                ReadFlailProjectileFrame(ref frame);
                frame.ItemReady = CanUseSelectedItem(frame.Player);
                ApplyFlailProjectileDiagnostics(ref frame, diagnostics);

                var decisionContext = new CombatAimFlailDecisionContext
                {
                    Projectile = frame.Projectile,
                    ItemReady = frame.ItemReady,
                    InCooldown = frame.InCooldown,
                    HitDetected = frame.HitDetected,
                    CollisionDetected = frame.CollisionDetected,
                    StuckRecovery = frame.StuckRecovery,
                    PhysicalHeld = frame.PhysicalHeld,
                    PhysicalReleasePending = frame.PhysicalReleasePending,
                    ReleaseInFlight = frame.ReleaseInFlight
                };
                var decision = CombatAimFlailReleaseStateMachine.Decide(in decisionContext);
                ApplyFlailDecisionDiagnostics(diagnostics, decision);
                ApplyFlailRuntimeUpdateTakeover(ref frame, decision, diagnostics);

                AdvanceReleaseWindow(frame.PhysicalHeld, frame.HasProjectile, frame.Projectile);

                Publish(diagnostics);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("CombatAimFlailControlService.Update", error);
                diagnostics.State = FlailControlStates.Disabled;
                diagnostics.BlockedReason = "exception:" + error.Message;
                Publish(diagnostics);
                LogThrottle.ErrorThrottled(
                    "combat-aim-flail-control-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAimFlailControlService",
                    "Combat aim flail control failed; exception swallowed.", error);
            }
        }

        private static void ApplyFlailProjectileDiagnostics(
            ref CombatAimFlailRuntimeFrame frame,
            CombatAimFlailDiagnostics diagnostics)
        {
            if (frame.HasProjectile)
            {
                var projectile = frame.RawProjectile;
                diagnostics.ProjectileWhoAmI = projectile.WhoAmI;
                diagnostics.ProjectileType = projectile.Type;
                diagnostics.ProjectileAiStyle = projectile.AiStyle;
                diagnostics.ProjectileAi0 = projectile.Ai0;
                diagnostics.ProjectileIdentity = projectile.Identity;
                diagnostics.ProjectileVelocityX = projectile.VelocityX;
                diagnostics.ProjectileVelocityY = projectile.VelocityY;

                frame.HitDetected = ProjectileTracker.UpdateHitCache(projectile);
                frame.CollisionDetected = CollisionDetector.DetectTileCollision(projectile);
                diagnostics.HitDetected = frame.HitDetected;
                diagnostics.CollisionDetected = frame.CollisionDetected;
                diagnostics.LocalNpcImmunityChanged = frame.HitDetected;
                diagnostics.TileCollisionDetected = frame.CollisionDetected;
                frame.StuckTicks = ProjectileTracker.UpdateStuckTracking(projectile);
                frame.StuckRecovery = !frame.PhysicalHeld && frame.ReleaseInFlight && frame.StuckTicks >= StuckRecoveryTicks;
                diagnostics.StuckRecovery = frame.StuckRecovery
                    ? "stuck:ai0ZeroVelocity:ticks=" + frame.StuckTicks.ToString(CultureInfo.InvariantCulture)
                    : "none";
                return;
            }

            ResetProjectileTracking();
            frame.Projectile = CombatAimFlailProjectileFrame.None();
            diagnostics.StuckRecovery = "none";
        }

        private static void ApplyFlailDecisionDiagnostics(
            CombatAimFlailDiagnostics diagnostics,
            CombatAimFlailControlDecision decision)
        {
            diagnostics.State = decision.State;
            diagnostics.AttackPulse = decision.AttackPulse;
            diagnostics.AttackSuppressed = decision.AttackSuppressed;
            diagnostics.AttackRestored = decision.AttackRestored;
            diagnostics.AttackRelease = decision.AttackRelease;
            diagnostics.BlockedReason = decision.BlockedReason;
            diagnostics.InputMode = decision.InputMode;
            diagnostics.InputPhase = decision.State;
            diagnostics.PulseReason = decision.PulseReason;
        }

        private static void ApplyFlailRuntimeUpdateTakeover(
            ref CombatAimFlailRuntimeFrame frame,
            CombatAimFlailControlDecision decision,
            CombatAimFlailDiagnostics diagnostics)
        {
            if (decision.AttackPulse)
            {
                if (TerrariaInputCompat.TryApplyUseItemTakeover(frame.Player, true))
                {
                    _cooldownUntilTick = frame.Tick + PulseCooldownTicks;
                    _state = decision.State;
                    diagnostics.TakeoverScope = "RuntimeUpdate";
                }
                else
                {
                    diagnostics.AttackPulse = false;
                    diagnostics.BlockedReason = "setUseItemPulseFailed:" + TerrariaInputCompat.LastInputCompatError;
                }

                return;
            }

            if (decision.AttackSuppressed)
            {
                if (TerrariaInputCompat.TryApplyUseItemTakeover(frame.Player, false))
                {
                    _state = decision.State;
                    diagnostics.TakeoverScope = "RuntimeUpdate";
                    diagnostics.ReleaseSuppressedPhysicalInput = frame.PhysicalHeld;
                }
                else
                {
                    diagnostics.AttackSuppressed = false;
                    diagnostics.AttackRelease = false;
                    diagnostics.BlockedReason = "setUseItemSuppressFailed:" + TerrariaInputCompat.LastInputCompatError;
                }

                return;
            }

            _state = decision.State;
        }

        public static CombatAimFlailDiagnostics GetDecisionDiagnostics(CombatAimItemCheckDecision decision)
        {
            return DiagnosticsPublisher.GetDecisionDiagnostics(decision);
        }

        private static CombatAimTargetSelection CloneSelection(CombatAimTargetSelection source)
        {
            if (source == null)
            {
                return null;
            }

            return new CombatAimTargetSelection
            {
                Enabled = source.Enabled,
                RadiusTiles = source.RadiusTiles,
                TrackDummy = source.TrackDummy,
                MarkerEnabled = source.MarkerEnabled,
                AimRangeOrigin = source.AimRangeOrigin ?? string.Empty,
                AimTargetPriority = source.AimTargetPriority ?? string.Empty,
                CursorAimRadius = source.CursorAimRadius,
                PlayerAimRadius = source.PlayerAimRadius,
                ActiveRangeMode = source.ActiveRangeMode ?? string.Empty,
                PlayerScreenMarginTiles = source.PlayerScreenMarginTiles,
                PlayerScreenRadiusTiles = source.PlayerScreenRadiusTiles,
                CursorWorldX = source.CursorWorldX,
                CursorWorldY = source.CursorWorldY,
                RangeCenterWorldX = source.RangeCenterWorldX,
                RangeCenterWorldY = source.RangeCenterWorldY,
                MouseScreenX = source.MouseScreenX,
                MouseScreenY = source.MouseScreenY,
                ScreenPositionX = source.ScreenPositionX,
                ScreenPositionY = source.ScreenPositionY,
                ScreenWidth = source.ScreenWidth,
                ScreenHeight = source.ScreenHeight,
                CandidateCount = source.CandidateCount,
                CheapCandidateCount = source.CheapCandidateCount,
                ExpensiveCandidateCount = source.ExpensiveCandidateCount,
                EvaluatedCandidateCount = source.EvaluatedCandidateCount,
                InRangeCandidateCount = source.InRangeCandidateCount,
                LosCacheHit = source.LosCacheHit,
                SkipReason = source.SkipReason ?? string.Empty,
                ResultCode = source.ResultCode ?? string.Empty,
                Target = CloneTarget(source.Target),
                BallisticTarget = CloneTarget(source.BallisticTarget),
                BallisticSolution = CloneBallisticSolution(source.BallisticSolution),
                CenterDistanceTiles = source.CenterDistanceTiles,
                HitboxDistanceTiles = source.HitboxDistanceTiles,
                TargetDistanceFromRangeCenterTiles = source.TargetDistanceFromRangeCenterTiles,
                TargetScore = source.TargetScore,
                LineClear = source.LineClear,
                LineClearAvailable = source.LineClearAvailable,
                DistanceToPlayerCursorRay = source.DistanceToPlayerCursorRay,
                InForwardCone = source.InForwardCone,
                PreviousTargetBonus = source.PreviousTargetBonus,
                SelectedSamplePoint = source.SelectedSamplePoint ?? string.Empty,
                AttackSamplePoint = source.AttackSamplePoint ?? string.Empty,
                SelectionSamplePoint = source.SelectionSamplePoint ?? string.Empty,
                SampleSpace = source.SampleSpace ?? string.Empty,
                LineOfSightRejectedSampleCount = source.LineOfSightRejectedSampleCount,
                VisibleSampleCount = source.VisibleSampleCount,
                NearestHitboxPointPenaltyApplied = source.NearestHitboxPointPenaltyApplied,
                CenterPreferred = source.CenterPreferred,
                SelectedSampleWorldX = source.SelectedSampleWorldX,
                SelectedSampleWorldY = source.SelectedSampleWorldY,
                PredictedHitboxCenterX = source.PredictedHitboxCenterX,
                PredictedHitboxCenterY = source.PredictedHitboxCenterY,
                ProjectileHitRadius = source.ProjectileHitRadius,
                SelectedReason = source.SelectedReason ?? string.Empty,
                LockedTargetId = source.LockedTargetId,
                LockedTargetType = source.LockedTargetType,
                LockedTargetStillValid = source.LockedTargetStillValid,
                TargetLockAgeTicks = source.TargetLockAgeTicks,
                TargetHoldTicksRemaining = source.TargetHoldTicksRemaining,
                SelectionPurpose = source.SelectionPurpose ?? string.Empty,
                SelectionCacheHit = source.SelectionCacheHit,
                SelectionCacheKey = source.SelectionCacheKey ?? string.Empty,
                PreferredTargetWhoAmI = source.PreferredTargetWhoAmI,
                PreferredTargetType = source.PreferredTargetType,
                MarkerTargetWhoAmI = source.MarkerTargetWhoAmI,
                MarkerTargetType = source.MarkerTargetType,
                AttackTargetWhoAmI = source.AttackTargetWhoAmI,
                AttackTargetType = source.AttackTargetType,
                MarkerAttackTargetMismatch = source.MarkerAttackTargetMismatch,
                MarkerTargetChangedForAttack = source.MarkerTargetChangedForAttack
            };
        }

        private static CombatTargetSnapshot CloneTarget(CombatTargetSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            return new CombatTargetSnapshot
            {
                WhoAmI = source.WhoAmI,
                Type = source.Type,
                Name = source.Name ?? string.Empty,
                Active = source.Active,
                Friendly = source.Friendly,
                TownNpc = source.TownNpc,
                Hide = source.Hide,
                Chaseable = source.Chaseable,
                DontTakeDamage = source.DontTakeDamage,
                Immortal = source.Immortal,
                IsTargetDummy = source.IsTargetDummy,
                Life = source.Life,
                LifeMax = source.LifeMax,
                PositionX = source.PositionX,
                PositionY = source.PositionY,
                Width = source.Width,
                Height = source.Height,
                CenterX = source.CenterX,
                CenterY = source.CenterY,
                VelocityX = source.VelocityX,
                VelocityY = source.VelocityY,
                SmoothedVelocityAvailable = source.SmoothedVelocityAvailable,
                SmoothedVelocityX = source.SmoothedVelocityX,
                SmoothedVelocityY = source.SmoothedVelocityY,
                NpcAiStyle = source.NpcAiStyle,
                NoGravity = source.NoGravity,
                CollideX = source.CollideX,
                CollideY = source.CollideY,
                Direction = source.Direction,
                DirectionY = source.DirectionY,
                TargetPlayer = source.TargetPlayer,
                AiSummaryAvailable = source.AiSummaryAvailable,
                Ai0 = source.Ai0,
                Ai1 = source.Ai1,
                Ai2 = source.Ai2,
                Ai3 = source.Ai3,
                LastReadTick = source.LastReadTick,
                MotionProfile = source.MotionProfile == null ? null : source.MotionProfile.Clone(),
                HitboxX = source.HitboxX,
                HitboxY = source.HitboxY,
                HitboxWidth = source.HitboxWidth,
                HitboxHeight = source.HitboxHeight
            };
        }

        private static CombatAimBallisticSolution CloneBallisticSolution(CombatAimBallisticSolution source)
        {
            if (source == null)
            {
                return null;
            }

            return source.Clone();
        }

        private static bool TryReadSelectedWeaponProfile(object player, out CombatAimWeaponProfile profile)
        {
            profile = null;
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) || selectedSlot < 0)
            {
                return false;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null || selectedSlot >= inventory.Count)
            {
                return false;
            }

            var item = inventory[selectedSlot];
            if (item == null)
            {
                return false;
            }

            profile = CombatAimWeaponProfile.Read(player, item);
            return profile != null && !profile.IsEmpty;
        }

        private static bool CanUseSelectedItem(object player)
        {
            if (player == null)
            {
                return false;
            }

            int itemAnimation;
            int itemTime;
            int reuseDelay;
            bool delayUseItem;
            GameStateReflection.TryGetInt(player, "itemAnimation", out itemAnimation);
            GameStateReflection.TryGetInt(player, "itemTime", out itemTime);
            GameStateReflection.TryGetInt(player, "reuseDelay", out reuseDelay);
            GameStateReflection.TryGetBool(player, "delayUseItem", out delayUseItem);
            return itemAnimation <= 0 && itemTime <= 0 && reuseDelay <= 0 && !delayUseItem;
        }

        private static void AdvanceReleaseWindow(bool physicalHeld, bool hasProjectile, CombatAimFlailProjectileFrame projectile)
        {
            if (_releaseAimTicksRemaining > 0)
            {
                _releaseAimTicksRemaining--;
            }

            if (!physicalHeld && _releaseAimTicksRemaining <= 0)
            {
                if (!hasProjectile || projectile == null || CombatAimFlailReleaseStateMachine.IsReturnOrEndingState(projectile.Ai0))
                {
                    _releaseInFlight = false;
                }
            }
        }

        private static bool UpdatePhysicalUseItemReleaseState(bool physicalHeld)
        {
            if (physicalHeld)
            {
                _physicalUseItemHeldLastTick = true;
                _releaseAimTicksRemaining = 0;
                _releaseInFlight = false;
                return false;
            }

            if (_physicalUseItemHeldLastTick)
            {
                _physicalUseItemHeldLastTick = false;
                _releaseAimTicksRemaining = ReleaseAimWindowTicks;
                _releaseInFlight = true;
            }

            return _releaseAimTicksRemaining > 0;
        }

        private static void ResetControlState()
        {
            if (!_physicalUseItemHeldLastTick &&
                _releaseAimTicksRemaining == 0 &&
                !_releaseInFlight &&
                _cooldownUntilTick == 0 &&
                string.Equals(_state, FlailControlStates.Idle, StringComparison.Ordinal) &&
                !ReleaseAimCache.HasCachedReleaseAim &&
                !ProjectileTracker.HasTracking)
            {
                return;
            }

            _physicalUseItemHeldLastTick = false;
            _releaseAimTicksRemaining = 0;
            _releaseInFlight = false;
            _cooldownUntilTick = 0;
            _state = FlailControlStates.Idle;
            ClearCachedReleaseAim();
            ResetProjectileTracking();
        }

        private static void ResetProjectileTracking()
        {
            ProjectileTracker.Reset();
        }

        private static void PublishBlocked(CombatAimFlailDiagnostics diagnostics, string blockedReason)
        {
            ResetControlState();
            DiagnosticsPublisher.PublishBlocked(diagnostics, blockedReason);
        }

        private static void Publish(CombatAimFlailDiagnostics diagnostics)
        {
            DiagnosticsPublisher.Publish(diagnostics);
        }

        private static CombatAimFlailDiagnostics GetLastDiagnostics()
        {
            return DiagnosticsPublisher.GetLastDiagnostics();
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

    }
}
