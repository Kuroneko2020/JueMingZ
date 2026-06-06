using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAimFlailControlService
    {
        private const int ImmunityCacheLength = 256;
        private const int PulseCooldownTicks = 6;
        private const int StuckRecoveryTicks = 8;
        private const int ReleaseAimWindowTicks = 3;
        private const int CachedReleaseAimMaxAgeTicks = 120;
        private const int FlailComboPressAimMaxAgeTicks = 20;
        private const float StationaryVelocityEpsilon = 0.001f;
        private static readonly object SyncRoot = new object();
        private static readonly int[] LastLocalNpcImmunity = new int[ImmunityCacheLength];
        private static CombatAimFlailDiagnostics _lastDiagnostics = CombatAimFlailDiagnostics.Empty();
        private static FlailCachedReleaseAim _cachedReleaseAim;
        private static CombatAimItemCheckDecision _flailComboPressAimDecision;
        private static long _flailComboPressAimTick;
        private static long _cooldownUntilTick;
        private static bool _physicalUseItemHeldLastTick;
        private static int _releaseAimTicksRemaining;
        private static bool _releaseInFlight;
        private static int _trackedProjectileWhoAmI = -1;
        private static int _trackedProjectileIdentity = -1;
        private static int _trackedProjectileType;
        private static int _trackedProjectileStuckTicks;
        private static string _state = FlailControlStates.Idle;
        private static string _lastEventKey = string.Empty;
        private static DateTime _lastEventUtc = DateTime.MinValue;
        private static MethodInfo _tileCollisionMethod;
        private static bool _tileCollisionResolved;

        public static void Update()
        {
            var diagnostics = CombatAimFlailDiagnostics.Empty();
            try
            {
                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    PublishBlocked(diagnostics, "runtimeTypesUnavailable");
                    return;
                }

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (settings.CursorAimRadius <= 0)
                {
                    PublishBlocked(diagnostics, "autoAimDisabled");
                    return;
                }

                object player;
                if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    PublishBlocked(diagnostics, "playerUnavailable");
                    return;
                }

                bool physicalHeld;
                if (!TerrariaInputCompat.TryReadPhysicalUseItemHeld(player, out physicalHeld))
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Idle;
                    diagnostics.BlockedReason = "physicalUseItemUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                    Publish(diagnostics);
                    return;
                }

                var ui = TerrariaInputCompat.ReadUiInputContext(player);
                if (ui.MainTypeUnavailable || ui.GameMenu || ui.ChatOpen || ui.NpcChatOpen ||
                    ui.ChestOpen || ui.MouseCapturedByUi)
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Disabled;
                    diagnostics.BlockedReason = ui.MainTypeUnavailable
                        ? "mainTypeUnavailable"
                        : ui.GameMenu
                            ? "gameMenu"
                            : ui.ChatOpen
                                ? "chatOpen"
                                : ui.NpcChatOpen
                                    ? "npcChatOpen"
                                    : ui.ChestOpen
                                        ? "chestOpen"
                                        : "uiBlocked:" + (ui.MouseCaptureReason ?? string.Empty);
                    Publish(diagnostics);
                    return;
                }

                var physicalReleasePending = UpdatePhysicalUseItemReleaseState(physicalHeld);
                diagnostics.PhysicalUseItemHeld = physicalHeld;
                diagnostics.PhysicalReleasePending = physicalReleasePending;
                if (!physicalHeld && !physicalReleasePending && !_releaseInFlight)
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Idle;
                    diagnostics.BlockedReason = "noActiveFlailUse";
                    Publish(diagnostics);
                    return;
                }

                CombatAimWeaponProfile profile;
                if (!TryReadSelectedWeaponProfile(player, out profile))
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Disabled;
                    diagnostics.BlockedReason = "selectedItemUnavailable";
                    Publish(diagnostics);
                    return;
                }

                diagnostics.ItemType = profile.ItemType;
                diagnostics.ItemName = profile.Name ?? string.Empty;

                var prepared = CombatAimBallisticSolver.Prepare(player, profile);
                var projectileAiStyle = prepared == null ? 0 : prepared.ProjectileAiStyle;
                var isYoyo = CombatAimYoyoCompat.IsYoyoProjectileType(profile.Shoot);
                var eligibility = CombatAimFlailPolicy.Evaluate(profile, projectileAiStyle, isYoyo);
                diagnostics.Eligible = eligibility.Eligible;
                diagnostics.Reason = eligibility.Reason;
                diagnostics.ProjectileAiStyle = projectileAiStyle;

                if (!eligibility.Eligible)
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Disabled;
                    diagnostics.BlockedReason = eligibility.Reason;
                    Publish(diagnostics);
                    return;
                }

                diagnostics.Active = true;

                long tick;
                TerrariaInputCompat.TryReadGameUpdateCount(out tick);

                var selection = CombatAutoAimService.CurrentSelection;
                var currentTargetAvailable = selection != null && selection.Enabled && selection.Target != null;
                if (currentTargetAvailable)
                {
                    UpdateCachedReleaseAim(player, profile, prepared, selection, settings, tick);
                }

                FlailCachedReleaseAim cachedReleaseAim;
                var cachedReleaseAimAvailable = TryGetCachedReleaseAim(profile, tick, out cachedReleaseAim);
                diagnostics.CachedReleaseAim = cachedReleaseAimAvailable;
                diagnostics.CachedReleaseAimAgeTicks = cachedReleaseAim == null ? -1 : ComputeCachedReleaseAimAge(cachedReleaseAim, tick);
                diagnostics.CachedReleaseAimReason = cachedReleaseAimAvailable
                    ? "available"
                    : ResolveCachedReleaseAimUnavailableReason(profile, tick);

                if (!physicalHeld && !physicalReleasePending && !_releaseInFlight)
                {
                    ResetControlState();
                    diagnostics.State = FlailControlStates.Idle;
                    diagnostics.BlockedReason = "notUsingItem";
                    Publish(diagnostics);
                    return;
                }

                var inCooldown = tick > 0 && tick < _cooldownUntilTick;

                if (!currentTargetAvailable && !cachedReleaseAimAvailable)
                {
                    diagnostics.State = physicalHeld ? FlailControlStates.SpinHold : FlailControlStates.Disabled;
                    diagnostics.BlockedReason = "targetUnavailable:noCachedReleaseAim";
                    AdvanceReleaseWindow(physicalHeld, false, CombatAimFlailProjectileFrame.None());
                    Publish(diagnostics);
                    return;
                }

                FlailProjectileSnapshot projectile;
                var hasProjectile = TryFindActiveFlailProjectile(player, profile.Shoot, out projectile);
                var itemReady = CanUseSelectedItem(player);
                bool hitDetected = false;
                bool collisionDetected = false;
                var stuckTicks = 0;
                var stuckRecovery = false;
                if (hasProjectile)
                {
                    diagnostics.ProjectileWhoAmI = projectile.WhoAmI;
                    diagnostics.ProjectileType = projectile.Type;
                    diagnostics.ProjectileAiStyle = projectile.AiStyle;
                    diagnostics.ProjectileAi0 = projectile.Ai0;
                    diagnostics.ProjectileIdentity = projectile.Identity;
                    diagnostics.ProjectileVelocityX = projectile.VelocityX;
                    diagnostics.ProjectileVelocityY = projectile.VelocityY;

                    hitDetected = UpdateHitCache(projectile);
                    collisionDetected = DetectTileCollision(projectile);
                    diagnostics.HitDetected = hitDetected;
                    diagnostics.CollisionDetected = collisionDetected;
                    diagnostics.LocalNpcImmunityChanged = hitDetected;
                    diagnostics.TileCollisionDetected = collisionDetected;
                    stuckTicks = UpdateStuckTracking(projectile);
                    stuckRecovery = !physicalHeld && _releaseInFlight && stuckTicks >= StuckRecoveryTicks;
                    diagnostics.StuckRecovery = stuckRecovery ? "stuck:ai0ZeroVelocity:ticks=" + stuckTicks.ToString(CultureInfo.InvariantCulture) : "none";
                }
                else
                {
                    ResetProjectileTracking();
                    diagnostics.StuckRecovery = "none";
                }

                var projectileFrame = hasProjectile ? CombatAimFlailProjectileFrame.FromSnapshot(projectile) : CombatAimFlailProjectileFrame.None();
                var decision = Decide(projectileFrame, itemReady, inCooldown, hitDetected, collisionDetected, stuckRecovery, physicalHeld, physicalReleasePending, _releaseInFlight);
                diagnostics.State = decision.State;
                diagnostics.AttackPulse = decision.AttackPulse;
                diagnostics.AttackSuppressed = decision.AttackSuppressed;
                diagnostics.AttackRestored = decision.AttackRestored;
                diagnostics.AttackRelease = decision.AttackRelease;
                diagnostics.BlockedReason = decision.BlockedReason;
                diagnostics.InputMode = decision.InputMode;
                diagnostics.InputPhase = decision.State;
                diagnostics.PulseReason = decision.PulseReason;

                if (decision.AttackPulse)
                {
                    if (TerrariaInputCompat.TryApplyUseItemTakeover(player, true))
                    {
                        _cooldownUntilTick = tick + PulseCooldownTicks;
                        _state = decision.State;
                        diagnostics.TakeoverScope = "RuntimeUpdate";
                    }
                    else
                    {
                        diagnostics.AttackPulse = false;
                        diagnostics.BlockedReason = "setUseItemPulseFailed:" + TerrariaInputCompat.LastInputCompatError;
                    }
                }
                else if (decision.AttackSuppressed)
                {
                    if (TerrariaInputCompat.TryApplyUseItemTakeover(player, false))
                    {
                        _state = decision.State;
                        diagnostics.TakeoverScope = "RuntimeUpdate";
                        diagnostics.ReleaseSuppressedPhysicalInput = physicalHeld;
                    }
                    else
                    {
                        diagnostics.AttackSuppressed = false;
                        diagnostics.AttackRelease = false;
                        diagnostics.BlockedReason = "setUseItemSuppressFailed:" + TerrariaInputCompat.LastInputCompatError;
                    }
                }
                else
                {
                    _state = decision.State;
                }

                AdvanceReleaseWindow(physicalHeld, hasProjectile, projectileFrame);

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

        public static CombatAimFlailDiagnostics GetDecisionDiagnostics(CombatAimItemCheckDecision decision)
        {
            var last = GetLastDiagnostics();
            if (decision == null || decision.WeaponProfile == null)
            {
                return last;
            }

            var projectileAiStyle = decision.BallisticSolution == null ? 0 : decision.BallisticSolution.ProjectileAiStyle;
            var isYoyo = CombatAimYoyoCompat.IsYoyoProjectileType(decision.WeaponProfile.Shoot);
            var eligibility = CombatAimFlailPolicy.Evaluate(decision.WeaponProfile, projectileAiStyle, isYoyo);
            var diagnostics = CombatAimFlailDiagnostics.Empty();
            diagnostics.ItemType = decision.ItemType;
            diagnostics.ItemName = decision.ItemName ?? string.Empty;
            diagnostics.Eligible = eligibility.Eligible;
            diagnostics.Reason = eligibility.Reason;
            diagnostics.ProjectileAiStyle = projectileAiStyle;

            if (eligibility.Eligible &&
                last != null &&
                last.ItemType == decision.ItemType)
            {
                diagnostics.Active = last.Active;
                diagnostics.State = last.State;
                diagnostics.ProjectileWhoAmI = last.ProjectileWhoAmI;
                diagnostics.ProjectileType = last.ProjectileType;
                diagnostics.ProjectileAiStyle = last.ProjectileAiStyle;
                diagnostics.ProjectileAi0 = last.ProjectileAi0;
                diagnostics.ProjectileVelocityX = last.ProjectileVelocityX;
                diagnostics.ProjectileVelocityY = last.ProjectileVelocityY;
                diagnostics.ProjectileIdentity = last.ProjectileIdentity;
                diagnostics.HitDetected = last.HitDetected;
                diagnostics.CollisionDetected = last.CollisionDetected;
                diagnostics.LocalNpcImmunityChanged = last.LocalNpcImmunityChanged;
                diagnostics.TileCollisionDetected = last.TileCollisionDetected;
                diagnostics.AttackPulse = last.AttackPulse;
                diagnostics.AttackRelease = last.AttackRelease;
                diagnostics.AttackSuppressed = last.AttackSuppressed;
                diagnostics.AttackRestored = last.AttackRestored;
                diagnostics.BlockedReason = last.BlockedReason;
                diagnostics.InputMode = last.InputMode;
                diagnostics.InputPhase = last.InputPhase;
                diagnostics.TakeoverScope = last.TakeoverScope;
                diagnostics.StuckRecovery = last.StuckRecovery;
                diagnostics.ReleaseSuppressedPhysicalInput = last.ReleaseSuppressedPhysicalInput;
                diagnostics.PhysicalUseItemHeld = last.PhysicalUseItemHeld;
                diagnostics.PhysicalReleasePending = last.PhysicalReleasePending;
                diagnostics.PulseReason = last.PulseReason;
                diagnostics.CachedReleaseAim = last.CachedReleaseAim;
                diagnostics.CachedReleaseAimAgeTicks = last.CachedReleaseAimAgeTicks;
                diagnostics.CachedReleaseAimReason = last.CachedReleaseAimReason;
            }
            else
            {
                diagnostics.Active = false;
                diagnostics.State = eligibility.Eligible ? FlailControlStates.Idle : FlailControlStates.Disabled;
                diagnostics.BlockedReason = eligibility.Eligible ? string.Empty : eligibility.Reason;
            }

            return diagnostics;
        }

        public static bool TryBeginItemCheckTakeover(
            object player,
            CombatAimItemCheckDecision decision,
            out TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            takeover = null;
            if (player == null || decision == null)
            {
                return false;
            }

            var diagnostics = GetDecisionDiagnostics(decision);
            if (diagnostics == null ||
                !diagnostics.Eligible ||
                !diagnostics.Active)
            {
                return false;
            }

            var releaseDecision = IsItemCheckReleaseDecision(decision);
            if (!diagnostics.AttackPulse &&
                !diagnostics.AttackSuppressed &&
                !releaseDecision)
            {
                return false;
            }

            if (releaseDecision && !diagnostics.AttackPulse)
            {
                diagnostics.State = FlailControlStates.ReleaseToTarget;
                diagnostics.AttackRelease = true;
                diagnostics.AttackSuppressed = true;
                diagnostics.AttackRestored = false;
                diagnostics.InputMode = "controlledUseItemRelease";
                diagnostics.InputPhase = FlailControlStates.ReleaseToTarget;
                diagnostics.BlockedReason = string.IsNullOrWhiteSpace(diagnostics.BlockedReason) ||
                                            string.Equals(diagnostics.BlockedReason, "spinHold", StringComparison.Ordinal) ||
                                            string.Equals(diagnostics.BlockedReason, "spinHoldNoProjectile", StringComparison.Ordinal)
                    ? "releaseHoldItemCheck"
                    : diagnostics.BlockedReason;
                diagnostics.PhysicalUseItemHeld = false;
                diagnostics.PhysicalReleasePending = true;
                diagnostics.CachedReleaseAim = diagnostics.CachedReleaseAim || string.Equals(decision.ReleaseHoldValidationReason, "cachedFlailReleaseAim", StringComparison.Ordinal);
                if (diagnostics.CachedReleaseAim && string.IsNullOrWhiteSpace(diagnostics.CachedReleaseAimReason))
                {
                    diagnostics.CachedReleaseAimReason = "usedForPhysicalRelease";
                }
            }

            var pressed = diagnostics.AttackPulse;
            if (!TerrariaInputCompat.TryBeginScopedUseItemTakeover(player, pressed, "ItemCheck", out takeover))
            {
                diagnostics.BlockedReason = "itemCheckTakeoverFailed:" + TerrariaInputCompat.LastInputCompatError;
                Publish(diagnostics);
                return false;
            }

            diagnostics.TakeoverScope = "ItemCheck";
            diagnostics.InputPhase = diagnostics.State;
            diagnostics.ReleaseSuppressedPhysicalInput = !pressed && takeover.SuppressedPhysicalInput;
            Publish(diagnostics);
            if (!pressed)
            {
                CombatAimPersistentCursorService.RememberFlailReleaseTail(decision);
            }

            return true;
        }

        internal static bool TryRememberExistingItemCheckReleaseTail(CombatAimItemCheckDecision decision, string takeoverScope)
        {
            if (!IsItemCheckReleaseDecision(decision))
            {
                return false;
            }

            return TryRememberReleaseTailFromDecision(decision, takeoverScope, "existingItemCheckRelease", false);
        }

        internal static bool TryRememberFlailComboPressAim(CombatAimItemCheckDecision decision)
        {
            if (!IsFlailTailSourceDecision(decision))
            {
                return false;
            }

            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);
            lock (SyncRoot)
            {
                _flailComboPressAimDecision = decision;
                _flailComboPressAimTick = tick;
            }

            return true;
        }

        internal static bool TryRememberFlailComboPressReleaseTail(string takeoverScope)
        {
            CombatAimItemCheckDecision decision;
            if (!TryGetRecentFlailComboPressAim(out decision))
            {
                return false;
            }

            return TryRememberReleaseTailFromDecision(decision, takeoverScope, "flailComboPressAim", true);
        }

        private static bool TryRememberReleaseTailFromDecision(
            CombatAimItemCheckDecision decision,
            string takeoverScope,
            string blockedReason,
            bool markCachedReleaseAim)
        {
            if (!IsFlailTailSourceDecision(decision))
            {
                return false;
            }

            var diagnostics = GetDecisionDiagnostics(decision);
            if (diagnostics == null || !diagnostics.Eligible)
            {
                return false;
            }

            diagnostics.Active = true;
            diagnostics.State = FlailControlStates.ReleaseToTarget;
            diagnostics.AttackPulse = false;
            diagnostics.AttackRelease = true;
            diagnostics.AttackSuppressed = true;
            diagnostics.AttackRestored = false;
            diagnostics.InputMode = "controlledUseItemRelease";
            diagnostics.InputPhase = FlailControlStates.ReleaseToTarget;
            diagnostics.TakeoverScope = string.IsNullOrWhiteSpace(takeoverScope) ? "ItemCheck" : takeoverScope;
            diagnostics.BlockedReason = string.IsNullOrWhiteSpace(blockedReason) ? "releaseTail" : blockedReason;
            diagnostics.PhysicalUseItemHeld = false;
            diagnostics.PhysicalReleasePending = true;
            diagnostics.CachedReleaseAim = diagnostics.CachedReleaseAim ||
                                           markCachedReleaseAim ||
                                           string.Equals(decision.ReleaseHoldValidationReason, "cachedFlailReleaseAim", StringComparison.Ordinal);
            if (diagnostics.CachedReleaseAim && string.IsNullOrWhiteSpace(diagnostics.CachedReleaseAimReason))
            {
                diagnostics.CachedReleaseAimReason = markCachedReleaseAim ? "flailComboPressAim" : "usedForExistingRelease";
            }

            Publish(diagnostics);
            return CombatAimPersistentCursorService.RememberFlailReleaseTail(decision);
        }

        private static bool TryGetRecentFlailComboPressAim(out CombatAimItemCheckDecision decision)
        {
            decision = null;
            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);

            lock (SyncRoot)
            {
                if (_flailComboPressAimDecision == null)
                {
                    return false;
                }

                if (tick > 0 &&
                    _flailComboPressAimTick > 0 &&
                    tick - _flailComboPressAimTick > FlailComboPressAimMaxAgeTicks)
                {
                    _flailComboPressAimDecision = null;
                    _flailComboPressAimTick = 0;
                    return false;
                }

                decision = _flailComboPressAimDecision;
                return true;
            }
        }

        private static bool IsFlailTailSourceDecision(CombatAimItemCheckDecision decision)
        {
            if (decision == null || decision.WeaponProfile == null || decision.BallisticSolution == null)
            {
                return false;
            }

            var isYoyo = CombatAimYoyoCompat.IsYoyoProjectileType(decision.WeaponProfile.Shoot);
            var eligibility = CombatAimFlailPolicy.Evaluate(decision.WeaponProfile, decision.BallisticSolution.ProjectileAiStyle, isYoyo);
            return eligibility.Eligible;
        }

        private static bool IsItemCheckReleaseDecision(CombatAimItemCheckDecision decision)
        {
            if (!IsFlailTailSourceDecision(decision))
            {
                return false;
            }

            return decision.ReleaseDetected ||
                   decision.ReleasedThisTick ||
                   decision.ReleaseHoldActive ||
                   decision.ReleaseHoldPending ||
                   string.Equals(decision.AimApplyMode, CombatAimApplyModes.ReleaseHold, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(decision.ReleaseHoldValidationReason, "cachedFlailReleaseAim", StringComparison.Ordinal);
        }

        public static void MarkProjectileAiScopedTakeover(CombatAimItemCheckDecision decision, CombatAimProjectileCursorMatch match)
        {
            if (decision == null)
            {
                return;
            }

            var diagnostics = GetDecisionDiagnostics(decision);
            if (diagnostics == null || !diagnostics.Eligible)
            {
                return;
            }

            diagnostics.Active = true;
            diagnostics.State = string.IsNullOrWhiteSpace(diagnostics.State) || string.Equals(diagnostics.State, FlailControlStates.Idle, StringComparison.Ordinal)
                ? FlailControlStates.ReleaseToTarget
                : diagnostics.State;
            diagnostics.InputPhase = string.IsNullOrWhiteSpace(diagnostics.InputPhase) ? diagnostics.State : diagnostics.InputPhase;
            diagnostics.InputMode = string.IsNullOrWhiteSpace(diagnostics.InputMode) ? "projectileAiScopedCursor" : diagnostics.InputMode;
            diagnostics.TakeoverScope = "ProjectileAI";
            diagnostics.AttackRelease = true;
            diagnostics.AttackSuppressed = true;
            diagnostics.PhysicalUseItemHeld = false;
            diagnostics.PhysicalReleasePending = true;
            if (match != null && match.Matches)
            {
                diagnostics.ProjectileWhoAmI = match.ProjectileWhoAmI;
                diagnostics.ProjectileType = match.ProjectileType;
                diagnostics.ProjectileAiStyle = match.ProjectileAiStyle;
            }

            Publish(diagnostics);
        }

        internal static bool TryCreateCachedReleaseDecision(object player, out CombatAimItemCheckDecision decision)
        {
            decision = null;
            if (player == null)
            {
                return false;
            }

            CombatAimWeaponProfile profile;
            if (!TryReadSelectedWeaponProfile(player, out profile))
            {
                return false;
            }

            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);

            FlailCachedReleaseAim cached;
            var cachedAvailable = TryGetCachedReleaseAim(profile, tick, out cached);

            var prepared = CombatAimBallisticSolver.Prepare(player, profile);
            var projectileAiStyle = prepared == null ? 0 : prepared.ProjectileAiStyle;
            if (projectileAiStyle <= 0 && cachedAvailable && cached != null && cached.BallisticSolution != null)
            {
                projectileAiStyle = cached.BallisticSolution.ProjectileAiStyle;
            }

            var isYoyo = CombatAimYoyoCompat.IsYoyoProjectileType(profile.Shoot);
            var eligibility = CombatAimFlailPolicy.Evaluate(profile, projectileAiStyle, isYoyo);
            if (!eligibility.Eligible)
            {
                return false;
            }

            bool physicalHeld;
            if (!TerrariaInputCompat.TryReadPhysicalUseItemHeld(player, out physicalHeld))
            {
                return false;
            }

            var physicalReleasePending = UpdatePhysicalUseItemReleaseState(physicalHeld);
            if (physicalHeld || !physicalReleasePending || !cachedAvailable || cached == null)
            {
                return false;
            }

            var ui = TerrariaInputCompat.ReadUiInputContext(player);
            if (!ui.MainTypeUnavailable &&
                (ui.GameMenu || ui.ChatOpen || ui.NpcChatOpen || ui.ChestOpen || ui.MouseCapturedByUi))
            {
                return false;
            }

            CombatAimUseInputSnapshot input;
            if (!TerrariaInputCompat.TryReadCombatAimUseInputSnapshot(player, out input))
            {
                input = new CombatAimUseInputSnapshot();
            }

            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            decision = BuildCachedReleaseDecision(player, profile, prepared, cached, settings, input, tick);
            if (decision == null)
            {
                return false;
            }

            decision.UiContext = ui;

            var last = GetLastDiagnostics();
            var diagnostics = last != null && last.ItemType == profile.ItemType
                ? last
                : CombatAimFlailDiagnostics.Empty();
            diagnostics.ItemType = profile.ItemType;
            diagnostics.ItemName = profile.Name ?? string.Empty;
            diagnostics.Eligible = true;
            diagnostics.Reason = eligibility.Reason;
            diagnostics.Active = true;
            diagnostics.State = FlailControlStates.ReleaseToTarget;
            diagnostics.AttackPulse = false;
            diagnostics.AttackRelease = true;
            diagnostics.AttackSuppressed = true;
            diagnostics.AttackRestored = false;
            diagnostics.InputMode = "controlledUseItemRelease";
            diagnostics.InputPhase = FlailControlStates.ReleaseToTarget;
            diagnostics.TakeoverScope = "cachedReleaseAim";
            diagnostics.BlockedReason = "cachedReleaseAim";
            diagnostics.PhysicalUseItemHeld = false;
            diagnostics.PhysicalReleasePending = true;
            diagnostics.CachedReleaseAim = true;
            diagnostics.CachedReleaseAimAgeTicks = ComputeCachedReleaseAimAge(cached, tick);
            diagnostics.CachedReleaseAimReason = "usedForPhysicalRelease";
            Publish(diagnostics);
            return true;
        }

        internal static CombatAimFlailControlDecision DecideForTesting(
            bool releasePending,
            bool hasProjectile,
            bool inCooldown,
            float ai0,
            bool hitDetected,
            bool collisionDetected)
        {
            return Decide(
                CombatAimFlailProjectileFrame.ForTesting(hasProjectile, hasProjectile ? 1 : -1, 1, 1, ai0, 1f, 0f),
                true,
                inCooldown,
                hitDetected,
                collisionDetected,
                false,
                releasePending,
                releasePending,
                releasePending);
        }

        internal static CombatAimFlailControlDecision DecideForTesting(
            bool releasePending,
            string releasePendingKind,
            bool hasProjectile,
            bool itemReady,
            bool inCooldown,
            float ai0,
            bool hitDetected,
            bool collisionDetected)
        {
            return Decide(
                CombatAimFlailProjectileFrame.ForTesting(hasProjectile, hasProjectile ? 1 : -1, 1, 1, ai0, 1f, 0f),
                itemReady,
                inCooldown,
                hitDetected,
                collisionDetected,
                false,
                !releasePending,
                releasePending,
                releasePending);
        }

        internal static CombatAimFlailControlDecision DecideForTesting(
            CombatAimFlailProjectileFrame projectile,
            bool itemReady,
            bool inCooldown,
            bool hitDetected,
            bool collisionDetected,
            bool stuckRecovery,
            bool physicalHeld,
            bool physicalReleasePending,
            bool releaseInFlight)
        {
            return Decide(projectile, itemReady, inCooldown, hitDetected, collisionDetected, stuckRecovery, physicalHeld, physicalReleasePending, releaseInFlight);
        }

        private static CombatAimFlailControlDecision Decide(
            CombatAimFlailProjectileFrame projectile,
            bool itemReady,
            bool inCooldown,
            bool hitDetected,
            bool collisionDetected,
            bool stuckRecovery,
            bool physicalHeld,
            bool physicalReleasePending,
            bool releaseInFlight)
        {
            if (physicalHeld)
            {
                if (projectile == null || !projectile.Active || projectile.WhoAmI < 0 || projectile.Type <= 0)
                {
                    return itemReady
                        ? CombatAimFlailControlDecision.None(FlailControlStates.SpinHold, "spinHoldNoProjectile")
                        : CombatAimFlailControlDecision.None(FlailControlStates.ReadyToLaunch, "itemUseCooldown");
                }

                if (IsReturnOrEndingState(projectile.Ai0))
                {
                    return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileActive, "spinHoldReturnState");
                }

                if (IsStationaryLaunchProjectile(projectile))
                {
                    return CombatAimFlailControlDecision.None(FlailControlStates.SpinHold, "spinHold");
                }

                return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileFlying, "physicalHoldProjectileMoving");
            }

            if (physicalReleasePending)
            {
                return CombatAimFlailControlDecision.Release(FlailControlStates.ReleaseToTarget, "physicalRelease", false);
            }

            if (inCooldown && !releaseInFlight)
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.Cooldown, "pulseCooldown");
            }

            if (projectile == null || !projectile.Active || projectile.WhoAmI < 0 || projectile.Type <= 0)
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.Idle, "notUsingItem");
            }

            if (IsReturnOrEndingState(projectile.Ai0))
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileActive, "flailReturnState");
            }

            if (stuckRecovery)
            {
                return CombatAimFlailControlDecision.Release(FlailControlStates.StuckRecoveryRelease, "stuckRecoveryRelease:ai0ZeroVelocity", false);
            }

            if (hitDetected || collisionDetected)
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileActive, hitDetected ? "hitDetected" : "collisionDetected");
            }

            if (!IsStationaryLaunchProjectile(projectile))
            {
                return CombatAimFlailControlDecision.None(FlailControlStates.ProjectileFlying, "projectileFlying");
            }

            return CombatAimFlailControlDecision.None(FlailControlStates.WaitHitOrCollision, releaseInFlight ? "waitReturnAfterRelease" : "waitSpinRelease");
        }

        private static bool IsReturnOrEndingState(float ai0)
        {
            return Math.Abs(ai0 - 4f) < 0.001f ||
                   Math.Abs(ai0 - 5f) < 0.001f ||
                   Math.Abs(ai0 - 6f) < 0.001f;
        }

        private static bool IsStationaryLaunchProjectile(CombatAimFlailProjectileFrame projectile)
        {
            return projectile != null &&
                   Math.Abs(projectile.Ai0) < 0.001f &&
                   Math.Abs(projectile.VelocityX) < StationaryVelocityEpsilon &&
                   Math.Abs(projectile.VelocityY) < StationaryVelocityEpsilon;
        }

        private static void UpdateCachedReleaseAim(
            object player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticContext prepared,
            CombatAimTargetSelection selection,
            AppSettings settings,
            long tick)
        {
            if (profile == null || profile.IsEmpty || selection == null || selection.Target == null)
            {
                return;
            }

            var ballistic = selection.BallisticSolution ?? CombatAimBallisticSolver.Solve(prepared, selection.BallisticTarget ?? selection.Target);
            var aimWorldX = ballistic == null ? selection.SelectedSampleWorldX : ballistic.AimWorldX;
            var aimWorldY = ballistic == null ? selection.SelectedSampleWorldY : ballistic.AimWorldY;

            int selectedSlot;
            TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot);
            lock (SyncRoot)
            {
                _cachedReleaseAim = new FlailCachedReleaseAim
                {
                    ItemType = profile.ItemType,
                    ItemName = profile.Name ?? string.Empty,
                    Shoot = profile.Shoot,
                    SelectedSlot = selectedSlot,
                    RecordedGameUpdateCount = tick,
                    AimWorldX = aimWorldX,
                    AimWorldY = aimWorldY,
                    TargetWhoAmI = selection.Target.WhoAmI,
                    TargetType = selection.Target.Type,
                    TargetName = selection.Target.Name ?? string.Empty,
                    WeaponProfile = profile,
                    Selection = CloneSelection(selection),
                    BallisticSolution = CloneBallisticSolution(ballistic),
                    AimRangeOrigin = CombatAimModes.NormalizeRangeOrigin(settings == null ? string.Empty : settings.AimRangeOrigin),
                    AimTargetPriority = CombatAimModes.NormalizeTargetPriority(settings == null ? string.Empty : settings.AimTargetPriority),
                    CursorAimRadius = Clamp(settings == null ? 0 : settings.CursorAimRadius, 0, 50),
                    PlayerAimRadius = Clamp(settings == null ? 0 : settings.PlayerAimRadius, 0, 50),
                    TrackDummy = settings != null && settings.CombatAimTrackDummyEnabled,
                    MarkerEnabled = settings != null && settings.CombatAimMarkerEnabled
                };
            }
        }

        private static bool TryGetCachedReleaseAim(
            CombatAimWeaponProfile profile,
            long tick,
            out FlailCachedReleaseAim cached)
        {
            lock (SyncRoot)
            {
                cached = _cachedReleaseAim == null ? null : _cachedReleaseAim.Clone();
            }

            if (profile == null || cached == null)
            {
                return false;
            }

            if (cached.ItemType != profile.ItemType || cached.Shoot != profile.Shoot)
            {
                return false;
            }

            var age = ComputeCachedReleaseAimAge(cached, tick);
            return age >= 0 && age <= CachedReleaseAimMaxAgeTicks;
        }

        private static int ComputeCachedReleaseAimAge(FlailCachedReleaseAim cached, long tick)
        {
            if (cached == null)
            {
                return -1;
            }

            if (tick <= 0 || cached.RecordedGameUpdateCount <= 0)
            {
                return 0;
            }

            var age = tick - cached.RecordedGameUpdateCount;
            if (age < 0)
            {
                return -1;
            }

            return age > int.MaxValue ? int.MaxValue : (int)age;
        }

        private static string ResolveCachedReleaseAimUnavailableReason(CombatAimWeaponProfile profile, long tick)
        {
            FlailCachedReleaseAim cached;
            lock (SyncRoot)
            {
                cached = _cachedReleaseAim == null ? null : _cachedReleaseAim.Clone();
            }

            if (cached == null)
            {
                return "missing";
            }

            if (profile == null)
            {
                return "missingProfile";
            }

            if (cached.ItemType != profile.ItemType || cached.Shoot != profile.Shoot)
            {
                return "itemChanged";
            }

            var age = ComputeCachedReleaseAimAge(cached, tick);
            if (age < 0)
            {
                return "futureTick";
            }

            if (age > CachedReleaseAimMaxAgeTicks)
            {
                return "expired:age=" + age.ToString(CultureInfo.InvariantCulture);
            }

            return "unknown";
        }

        private static CombatAimItemCheckDecision BuildCachedReleaseDecision(
            object player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticContext prepared,
            FlailCachedReleaseAim cached,
            AppSettings settings,
            CombatAimUseInputSnapshot input,
            long tick)
        {
            if (profile == null || cached == null)
            {
                return null;
            }

            var selection = CloneSelection(cached.Selection);
            if (selection == null || selection.Target == null)
            {
                selection = new CombatAimTargetSelection
                {
                    Enabled = true,
                    RadiusTiles = Clamp(settings == null ? 0 : settings.CursorAimRadius, 0, 50),
                    TrackDummy = settings != null && settings.CombatAimTrackDummyEnabled,
                    MarkerEnabled = settings != null && settings.CombatAimMarkerEnabled,
                    AimRangeOrigin = cached.AimRangeOrigin ?? string.Empty,
                    AimTargetPriority = cached.AimTargetPriority ?? string.Empty,
                    CursorAimRadius = cached.CursorAimRadius,
                    PlayerAimRadius = cached.PlayerAimRadius,
                    ResultCode = "CachedFlailReleaseTarget",
                    Target = new CombatTargetSnapshot
                    {
                        WhoAmI = cached.TargetWhoAmI,
                        Type = cached.TargetType,
                        Name = cached.TargetName ?? string.Empty,
                        Active = true,
                        CenterX = cached.AimWorldX,
                        CenterY = cached.AimWorldY
                    },
                    SelectedSampleWorldX = cached.AimWorldX,
                    SelectedSampleWorldY = cached.AimWorldY,
                    SelectedSamplePoint = "cachedReleaseAim",
                    AttackSamplePoint = "cachedReleaseAim",
                    SelectionSamplePoint = "cachedReleaseAim",
                    SelectionPurpose = "FlailRelease"
                };
            }

            var ballistic = CloneBallisticSolution(cached.BallisticSolution);
            if (ballistic == null)
            {
                ballistic = CombatAimBallisticSolver.Solve(prepared, selection.BallisticTarget ?? selection.Target);
            }

            if (ballistic != null)
            {
                ballistic.AimWorldX = cached.AimWorldX;
                ballistic.AimWorldY = cached.AimWorldY;
            }

            int aimScreenX;
            int aimScreenY;
            if (!TerrariaInputCompat.TryWorldToScreen(cached.AimWorldX, cached.AimWorldY, out aimScreenX, out aimScreenY))
            {
                aimScreenX = (int)Math.Round(cached.AimWorldX);
                aimScreenY = (int)Math.Round(cached.AimWorldY);
            }

            int selectedSlot;
            TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot);

            input = input ?? new CombatAimUseInputSnapshot();
            var decision = new CombatAimItemCheckDecision
            {
                Enabled = true,
                RadiusTiles = Clamp(settings == null ? 0 : settings.CursorAimRadius, 0, 50),
                AimRangeOrigin = cached.AimRangeOrigin ?? string.Empty,
                AimTargetPriority = cached.AimTargetPriority ?? string.Empty,
                ActiveRangeMode = selection.ActiveRangeMode ?? string.Empty,
                CursorAimRadius = cached.CursorAimRadius,
                PlayerAimRadius = cached.PlayerAimRadius,
                PlayerScreenMarginTiles = selection.PlayerScreenMarginTiles,
                PlayerScreenRadiusTiles = selection.PlayerScreenRadiusTiles,
                RangeCenterWorldX = selection.RangeCenterWorldX,
                RangeCenterWorldY = selection.RangeCenterWorldY,
                TrackDummy = cached.TrackDummy,
                MarkerEnabled = cached.MarkerEnabled,
                BridgePending = ItemUseBridge.PendingRequestId != Guid.Empty,
                UseItemHeld = false,
                UseItemReleased = input.UseItemReleased,
                WasUseItemHeldLastTick = true,
                ReleasedThisTick = true,
                ReleaseDetected = true,
                ItemAnimation = input.ItemAnimation,
                ItemTime = input.ItemTime,
                GameUpdateCount = tick > 0 ? tick : input.GameUpdateCount,
                AimApplyMode = CombatAimApplyModes.InstantItemCheck,
                ReleaseHoldValidationReason = "cachedFlailReleaseAim",
                AttackQualified = true,
                ResultCode = DiagnosticResultCode.Succeeded.ToString(),
                SelectedSlot = selectedSlot,
                ItemType = profile.ItemType,
                ItemStack = profile.Stack,
                ItemName = profile.Name ?? string.Empty,
                Damage = profile.Damage,
                Shoot = profile.Shoot,
                UseAmmo = profile.UseAmmo,
                Melee = profile.Melee,
                CreateTile = profile.CreateTile,
                CreateWall = profile.CreateWall,
                Pick = profile.Pick,
                Axe = profile.Axe,
                Hammer = profile.Hammer,
                FishingPole = profile.FishingPole,
                WeaponProfile = profile,
                AimWorldX = cached.AimWorldX,
                AimWorldY = cached.AimWorldY,
                AimScreenX = aimScreenX,
                AimScreenY = aimScreenY,
                BallisticSolution = ballistic,
                Selection = selection,
                UiContext = TerrariaInputCompat.ReadUiInputContext(player)
            };

            decision.Selection.SelectionCacheHit = true;
            decision.Selection.SelectionCacheKey = "flailCachedReleaseAim";
            decision.Selection.ResultCode = string.IsNullOrWhiteSpace(decision.Selection.ResultCode)
                ? "CachedFlailReleaseTarget"
                : decision.Selection.ResultCode;
            decision.Selection.SelectionPurpose = "FlailRelease";
            decision.Selection.AttackTargetWhoAmI = decision.Selection.Target == null ? cached.TargetWhoAmI : decision.Selection.Target.WhoAmI;
            decision.Selection.AttackTargetType = decision.Selection.Target == null ? cached.TargetType : decision.Selection.Target.Type;
            CombatAimTargetLockService.MarkAttackQualified(decision.Target);
            return decision;
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
                LineOfSightRejectedSampleCount = source.LineOfSightRejectedSampleCount,
                NearestHitboxPointPenaltyApplied = source.NearestHitboxPointPenaltyApplied,
                CenterPreferred = source.CenterPreferred,
                SelectedSampleWorldX = source.SelectedSampleWorldX,
                SelectedSampleWorldY = source.SelectedSampleWorldY,
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

            return new CombatAimBallisticSolution
            {
                Solved = source.Solved,
                Mode = source.Mode ?? string.Empty,
                FallbackReason = source.FallbackReason ?? string.Empty,
                ProjectileType = source.ProjectileType,
                ProjectileName = source.ProjectileName ?? string.Empty,
                WeaponShootProjectileType = source.WeaponShootProjectileType,
                WeaponShootProjectileName = source.WeaponShootProjectileName ?? string.Empty,
                ResolvedProjectileRole = source.ResolvedProjectileRole ?? string.Empty,
                SecondaryProjectileType = source.SecondaryProjectileType,
                SecondaryProjectileName = source.SecondaryProjectileName ?? string.Empty,
                ProjectileAiStyle = source.ProjectileAiStyle,
                ProjectileExtraUpdates = source.ProjectileExtraUpdates,
                ProjectileDefaultsAvailable = source.ProjectileDefaultsAvailable,
                ProjectileNoGravity = source.ProjectileNoGravity,
                ProjectileArrow = source.ProjectileArrow,
                ProjectileTileCollide = source.ProjectileTileCollide,
                ProjectileWidth = source.ProjectileWidth,
                ProjectileHeight = source.ProjectileHeight,
                ProjectileFriendly = source.ProjectileFriendly,
                ProjectileHostile = source.ProjectileHostile,
                AmmoType = source.AmmoType,
                AmmoItemType = source.AmmoItemType,
                AmmoItemName = source.AmmoItemName ?? string.Empty,
                AmmoProjectileType = source.AmmoProjectileType,
                AmmoProjectileName = source.AmmoProjectileName ?? string.Empty,
                PrimaryProjectileType = source.PrimaryProjectileType,
                PrimaryProjectileName = source.PrimaryProjectileName ?? string.Empty,
                PrimaryProjectileRole = source.PrimaryProjectileRole ?? string.Empty,
                AmmoSlot = source.AmmoSlot,
                AmmoShootSpeed = source.AmmoShootSpeed,
                AmmoAvailable = source.AmmoAvailable,
                AmmoArrowLike = source.AmmoArrowLike,
                AmmoBulletLike = source.AmmoBulletLike,
                SecondaryProjectileRole = source.SecondaryProjectileRole ?? string.Empty,
                SpecialWeaponKind = source.SpecialWeaponKind ?? string.Empty,
                SpecialWeaponName = source.SpecialWeaponName ?? string.Empty,
                SpecialWeaponRule = source.SpecialWeaponRule ?? string.Empty,
                SpecialShotCount = source.SpecialShotCount,
                SpecialSpreadDegrees = source.SpecialSpreadDegrees,
                SpecialParallelSpacingPixels = source.SpecialParallelSpacingPixels,
                SpecialLeadTicks = source.SpecialLeadTicks,
                SpecialCursorTarget = source.SpecialCursorTarget,
                SpecialAimApplied = source.SpecialAimApplied,
                SpecialWeaponUsesWeaponShoot = source.SpecialWeaponUsesWeaponShoot,
                SpecialWeaponUsesAmmoShoot = source.SpecialWeaponUsesAmmoShoot,
                ConservativeCenter = source.ConservativeCenter,
                AimAdjusted = source.AimAdjusted,
                PlayerCenterX = source.PlayerCenterX,
                PlayerCenterY = source.PlayerCenterY,
                TargetVelocityX = source.TargetVelocityX,
                TargetVelocityY = source.TargetVelocityY,
                PredictedTargetX = source.PredictedTargetX,
                PredictedTargetY = source.PredictedTargetY,
                ProjectileSpeed = source.ProjectileSpeed,
                EffectiveProjectileSpeed = source.EffectiveProjectileSpeed,
                LeadTicks = source.LeadTicks,
                GravityPerTick = source.GravityPerTick,
                GravityCompensationPixels = source.GravityCompensationPixels,
                AimWorldX = source.AimWorldX,
                AimWorldY = source.AimWorldY
            };
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

        private static bool TryFindActiveFlailProjectile(object player, int expectedProjectileType, out FlailProjectileSnapshot snapshot)
        {
            snapshot = null;
            var projectiles = ReadMainProjectiles();
            if (projectiles == null)
            {
                return false;
            }

            var localOwner = ReadLocalPlayerId(player);
            FlailProjectileSnapshot first = null;
            for (var index = 0; index < projectiles.Count; index++)
            {
                var projectile = projectiles[index];
                if (projectile == null)
                {
                    continue;
                }

                FlailProjectileSnapshot current;
                if (!TryReadFlailProjectile(projectile, out current))
                {
                    continue;
                }

                if (!current.Active || current.Owner != localOwner || current.AiStyle != 15 || !current.Friendly || current.Hostile)
                {
                    continue;
                }

                if (first == null)
                {
                    first = current;
                }

                if (expectedProjectileType > 0 && current.Type == expectedProjectileType)
                {
                    snapshot = current;
                    TrackProjectile(current);
                    return true;
                }
            }

            if (first == null)
            {
                return false;
            }

            snapshot = first;
            TrackProjectile(first);
            return true;
        }

        private static bool TryReadFlailProjectile(object projectile, out FlailProjectileSnapshot snapshot)
        {
            snapshot = null;
            if (projectile == null)
            {
                return false;
            }

            var current = new FlailProjectileSnapshot();
            current.Raw = projectile;
            GameStateReflection.TryGetInt(projectile, "whoAmI", out current.WhoAmI);
            GameStateReflection.TryGetInt(projectile, "type", out current.Type);
            GameStateReflection.TryGetInt(projectile, "aiStyle", out current.AiStyle);
            GameStateReflection.TryGetInt(projectile, "owner", out current.Owner);
            GameStateReflection.TryGetInt(projectile, "identity", out current.Identity);
            GameStateReflection.TryGetBool(projectile, "active", out current.Active);
            GameStateReflection.TryGetBool(projectile, "friendly", out current.Friendly);
            GameStateReflection.TryGetBool(projectile, "hostile", out current.Hostile);
            GameStateReflection.TryGetInt(projectile, "width", out current.Width);
            GameStateReflection.TryGetInt(projectile, "height", out current.Height);
            current.Ai0 = ReadAi0(projectile);
            current.Position = GameStateReflection.GetMember(projectile, "position");
            current.Velocity = GameStateReflection.GetMember(projectile, "velocity");
            GameStateReflection.TryReadVector2(current.Velocity, out current.VelocityX, out current.VelocityY);
            current.LocalNpcImmunity = GameStateReflection.AsList(GameStateReflection.GetMember(projectile, "localNPCImmunity"));
            snapshot = current;
            return current.Type > 0;
        }

        private static bool UpdateHitCache(FlailProjectileSnapshot projectile)
        {
            if (projectile == null || projectile.LocalNpcImmunity == null)
            {
                return false;
            }

            if (projectile.WhoAmI != _trackedProjectileWhoAmI ||
                projectile.Identity != _trackedProjectileIdentity ||
                projectile.Type != _trackedProjectileType)
            {
                ResetProjectileTracking();
                TrackProjectile(projectile);
            }

            var detected = false;
            var count = Math.Min(projectile.LocalNpcImmunity.Count, LastLocalNpcImmunity.Length);
            for (var index = 0; index < count; index++)
            {
                var raw = projectile.LocalNpcImmunity[index];
                var value = raw == null ? 0 : Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                if (value > LastLocalNpcImmunity[index])
                {
                    detected = true;
                }

                LastLocalNpcImmunity[index] = value;
            }

            for (var index = count; index < LastLocalNpcImmunity.Length; index++)
            {
                LastLocalNpcImmunity[index] = 0;
            }

            return detected;
        }

        private static bool DetectTileCollision(FlailProjectileSnapshot projectile)
        {
            if (projectile == null || Math.Abs(projectile.Ai0 - 1f) > 0.001f)
            {
                return false;
            }

            float velocityX;
            float velocityY;
            if (!GameStateReflection.TryReadVector2(projectile.Velocity, out velocityX, out velocityY) ||
                Math.Abs(velocityX) < 0.001f && Math.Abs(velocityY) < 0.001f)
            {
                return false;
            }

            var method = ResolveTileCollisionMethod();
            if (method == null)
            {
                return false;
            }

            try
            {
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                for (var index = 0; index < parameters.Length; index++)
                {
                    if (index == 0)
                    {
                        args[index] = projectile.Position;
                    }
                    else if (index == 1)
                    {
                        args[index] = projectile.Velocity;
                    }
                    else if (index == 2)
                    {
                        args[index] = projectile.Width;
                    }
                    else if (index == 3)
                    {
                        args[index] = projectile.Height;
                    }
                    else if (parameters[index].ParameterType == typeof(bool))
                    {
                        args[index] = false;
                    }
                    else if (parameters[index].ParameterType == typeof(int))
                    {
                        args[index] = 1;
                    }
                    else
                    {
                        args[index] = parameters[index].ParameterType.IsValueType
                            ? Activator.CreateInstance(parameters[index].ParameterType)
                            : null;
                    }
                }

                var result = method.Invoke(null, args);
                float resultX;
                float resultY;
                if (!GameStateReflection.TryReadVector2(result, out resultX, out resultY))
                {
                    return false;
                }

                return Math.Abs(resultX - velocityX) > 0.001f ||
                       Math.Abs(resultY - velocityY) > 0.001f;
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo ResolveTileCollisionMethod()
        {
            if (_tileCollisionResolved)
            {
                return _tileCollisionMethod;
            }

            _tileCollisionResolved = true;
            var type = FindType("Terraria.Collision");
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "TileCollision", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length < 4)
                {
                    continue;
                }

                if (string.Equals(parameters[0].ParameterType.Name, "Vector2", StringComparison.Ordinal) &&
                    string.Equals(parameters[1].ParameterType.Name, "Vector2", StringComparison.Ordinal) &&
                    parameters[2].ParameterType == typeof(int) &&
                    parameters[3].ParameterType == typeof(int))
                {
                    _tileCollisionMethod = method;
                    return _tileCollisionMethod;
                }
            }

            return null;
        }

        private static IList ReadMainProjectiles()
        {
            try
            {
                var mainType = GameMode.FindTerrariaMainType();
                return mainType == null ? null : GameStateReflection.AsList(GameStateReflection.GetStaticMember(mainType, "projectile"));
            }
            catch
            {
                return null;
            }
        }

        private static int ReadLocalPlayerId(object player)
        {
            try
            {
                var mainType = GameMode.FindTerrariaMainType();
                var raw = mainType == null ? null : GameStateReflection.GetStaticMember(mainType, "myPlayer");
                if (raw != null)
                {
                    return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            int whoAmI;
            return player != null && GameStateReflection.TryGetInt(player, "whoAmI", out whoAmI) ? whoAmI : -1;
        }

        private static float ReadAi0(object projectile)
        {
            var ai = GameStateReflection.AsList(GameStateReflection.GetMember(projectile, "ai"));
            if (ai == null || ai.Count <= 0 || ai[0] == null)
            {
                return 0f;
            }

            return Convert.ToSingle(ai[0], CultureInfo.InvariantCulture);
        }

        private static void TrackProjectile(FlailProjectileSnapshot projectile)
        {
            if (projectile == null)
            {
                return;
            }

            if (_trackedProjectileWhoAmI == projectile.WhoAmI &&
                _trackedProjectileIdentity == projectile.Identity &&
                _trackedProjectileType == projectile.Type)
            {
                return;
            }

            ResetProjectileTracking();
            _trackedProjectileWhoAmI = projectile.WhoAmI;
            _trackedProjectileIdentity = projectile.Identity;
            _trackedProjectileType = projectile.Type;
        }

        private static int UpdateStuckTracking(FlailProjectileSnapshot projectile)
        {
            if (projectile == null ||
                projectile.WhoAmI != _trackedProjectileWhoAmI ||
                projectile.Identity != _trackedProjectileIdentity ||
                projectile.Type != _trackedProjectileType ||
                Math.Abs(projectile.Ai0) >= 0.001f ||
                Math.Abs(projectile.VelocityX) >= StationaryVelocityEpsilon ||
                Math.Abs(projectile.VelocityY) >= StationaryVelocityEpsilon)
            {
                _trackedProjectileStuckTicks = 0;
                return _trackedProjectileStuckTicks;
            }

            _trackedProjectileStuckTicks++;
            return _trackedProjectileStuckTicks;
        }

        private static void AdvanceReleaseWindow(bool physicalHeld, bool hasProjectile, CombatAimFlailProjectileFrame projectile)
        {
            if (_releaseAimTicksRemaining > 0)
            {
                _releaseAimTicksRemaining--;
            }

            if (!physicalHeld && _releaseAimTicksRemaining <= 0)
            {
                if (!hasProjectile || projectile == null || IsReturnOrEndingState(projectile.Ai0))
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
                _cachedReleaseAim == null &&
                _trackedProjectileWhoAmI < 0 &&
                _trackedProjectileIdentity < 0 &&
                _trackedProjectileType == 0 &&
                _trackedProjectileStuckTicks == 0)
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

        private static void ClearCachedReleaseAim()
        {
            lock (SyncRoot)
            {
                _cachedReleaseAim = null;
            }
        }

        private static void ClearFlailComboPressAim()
        {
            lock (SyncRoot)
            {
                _flailComboPressAimDecision = null;
                _flailComboPressAimTick = 0;
            }
        }

        private static void ResetProjectileTracking()
        {
            var hadTracking =
                _trackedProjectileWhoAmI >= 0 ||
                _trackedProjectileIdentity >= 0 ||
                _trackedProjectileType != 0 ||
                _trackedProjectileStuckTicks != 0;
            _trackedProjectileWhoAmI = -1;
            _trackedProjectileIdentity = -1;
            _trackedProjectileType = 0;
            _trackedProjectileStuckTicks = 0;
            if (hadTracking)
            {
                Array.Clear(LastLocalNpcImmunity, 0, LastLocalNpcImmunity.Length);
            }
        }

        private static void PublishBlocked(CombatAimFlailDiagnostics diagnostics, string blockedReason)
        {
            ResetControlState();
            diagnostics.State = FlailControlStates.Disabled;
            diagnostics.BlockedReason = blockedReason ?? string.Empty;
            Publish(diagnostics);
        }

        private static void Publish(CombatAimFlailDiagnostics diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (IsDuplicateInactiveDiagnostics(_lastDiagnostics, diagnostics))
                {
                    return;
                }

                _lastDiagnostics = diagnostics.Clone();
            }

            RecordEventIfUseful(diagnostics);
        }

        private static bool IsDuplicateInactiveDiagnostics(CombatAimFlailDiagnostics previous, CombatAimFlailDiagnostics current)
        {
            if (previous == null || current == null || current.Active)
            {
                return false;
            }

            return !previous.Active &&
                   previous.ItemType == current.ItemType &&
                   previous.Eligible == current.Eligible &&
                   previous.PhysicalUseItemHeld == current.PhysicalUseItemHeld &&
                   previous.PhysicalReleasePending == current.PhysicalReleasePending &&
                   string.Equals(previous.State ?? string.Empty, current.State ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(previous.Reason ?? string.Empty, current.Reason ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(previous.BlockedReason ?? string.Empty, current.BlockedReason ?? string.Empty, StringComparison.Ordinal);
        }

        private static CombatAimFlailDiagnostics GetLastDiagnostics()
        {
            lock (SyncRoot)
            {
                return _lastDiagnostics == null ? CombatAimFlailDiagnostics.Empty() : _lastDiagnostics.Clone();
            }
        }

        private static void RecordEventIfUseful(CombatAimFlailDiagnostics diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            var key = diagnostics.ItemType.ToString(CultureInfo.InvariantCulture) + "|" +
                      diagnostics.State + "|" +
                      diagnostics.BlockedReason + "|" +
                      diagnostics.AttackPulse + "|" +
                      diagnostics.AttackRelease + "|" +
                      diagnostics.AttackSuppressed + "|" +
                      diagnostics.TakeoverScope + "|" +
                      diagnostics.StuckRecovery + "|" +
                      diagnostics.HitDetected + "|" +
                      diagnostics.CollisionDetected;

            var now = DateTime.UtcNow;
            var important = diagnostics.AttackPulse || diagnostics.AttackRelease || diagnostics.HitDetected || diagnostics.CollisionDetected;
            if (!diagnostics.Active && !important)
            {
                return;
            }

            var interval = diagnostics.Active ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(5);
            if (!important &&
                string.Equals(key, _lastEventKey, StringComparison.Ordinal) &&
                now - _lastEventUtc < interval)
            {
                return;
            }

            _lastEventKey = key;
            _lastEventUtc = now;
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "CombatAim.FlailControl",
                "Aim",
                string.Empty,
                diagnostics.AttackPulse || diagnostics.AttackRelease ? "Applied" : "Observed",
                diagnostics.AttackPulse || diagnostics.AttackRelease ? DiagnosticResultCode.Succeeded.ToString() : DiagnosticResultCode.NotApplicable.ToString(),
                "Combat flail control state: " + diagnostics.State,
                0,
                "{}",
                "{}",
                BuildDiagnosticsJson(diagnostics),
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static string BuildDiagnosticsJson(CombatAimFlailDiagnostics diagnostics)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "flailControlEligible", BoolRaw(diagnostics.Eligible), true);
            AppendString(builder, "flailControlReason", diagnostics.Reason, true);
            AppendRaw(builder, "flailControlActive", BoolRaw(diagnostics.Active), true);
            AppendString(builder, "flailControlState", diagnostics.State, true);
            AppendString(builder, "flailInputMode", diagnostics.InputMode, true);
            AppendString(builder, "flailInputPhase", diagnostics.InputPhase, true);
            AppendString(builder, "flailTakeoverScope", diagnostics.TakeoverScope, true);
            AppendRaw(builder, "flailPhysicalUseItemHeld", BoolRaw(diagnostics.PhysicalUseItemHeld), true);
            AppendRaw(builder, "flailPhysicalReleasePending", BoolRaw(diagnostics.PhysicalReleasePending), true);
            AppendRaw(builder, "flailProjectileWhoAmI", IntRaw(diagnostics.ProjectileWhoAmI), true);
            AppendRaw(builder, "flailProjectileType", IntRaw(diagnostics.ProjectileType), true);
            AppendRaw(builder, "flailProjectileAiStyle", IntRaw(diagnostics.ProjectileAiStyle), true);
            AppendRaw(builder, "flailProjectileAi0", FloatRaw(diagnostics.ProjectileAi0), true);
            AppendRaw(builder, "flailProjectileVelocity", BuildPointJson(diagnostics.ProjectileVelocityX, diagnostics.ProjectileVelocityY), true);
            AppendRaw(builder, "flailProjectileIdentity", IntRaw(diagnostics.ProjectileIdentity), true);
            AppendRaw(builder, "flailHitDetected", BoolRaw(diagnostics.HitDetected), true);
            AppendRaw(builder, "flailCollisionDetected", BoolRaw(diagnostics.CollisionDetected), true);
            AppendRaw(builder, "flailLocalNpcImmunityChanged", BoolRaw(diagnostics.LocalNpcImmunityChanged), true);
            AppendRaw(builder, "flailTileCollisionDetected", BoolRaw(diagnostics.TileCollisionDetected), true);
            AppendRaw(builder, "flailAttackPulse", BoolRaw(diagnostics.AttackPulse), true);
            AppendRaw(builder, "flailAttackRelease", BoolRaw(diagnostics.AttackRelease), true);
            AppendRaw(builder, "flailAttackSuppressed", BoolRaw(diagnostics.AttackSuppressed), true);
            AppendRaw(builder, "flailAttackRestored", BoolRaw(diagnostics.AttackRestored), true);
            AppendString(builder, "flailPulseReason", diagnostics.PulseReason, true);
            AppendString(builder, "flailStuckRecovery", diagnostics.StuckRecovery, true);
            AppendRaw(builder, "flailCachedReleaseAim", BoolRaw(diagnostics.CachedReleaseAim), true);
            AppendRaw(builder, "flailCachedReleaseAimAgeTicks", IntRaw(diagnostics.CachedReleaseAimAgeTicks), true);
            AppendString(builder, "flailCachedReleaseAimReason", diagnostics.CachedReleaseAimReason, true);
            AppendRaw(builder, "flailReleaseSuppressedPhysicalInput", BoolRaw(diagnostics.ReleaseSuppressedPhysicalInput), true);
            AppendString(builder, "flailControlBlockedReason", diagnostics.BlockedReason, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildPointJson(float x, float y)
        {
            return "{" +
                   "\"x\":" + FloatRaw(x) + "," +
                   "\"y\":" + FloatRaw(y) +
                   "}";
        }

        private static Type FindType(string fullName)
        {
            return TerrariaTypeCache.Find(fullName);
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(name).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(name).Append("\":\"").Append(Escape(value)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FloatRaw(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        internal sealed class FlailProjectileSnapshot
        {
            public object Raw;
            public int WhoAmI = -1;
            public int Type;
            public int AiStyle;
            public int Owner = -1;
            public int Identity = -1;
            public bool Active;
            public bool Friendly;
            public bool Hostile;
            public int Width;
            public int Height;
            public float Ai0;
            public float VelocityX;
            public float VelocityY;
            public object Position;
            public object Velocity;
            public IList LocalNpcImmunity;
        }

        private sealed class FlailCachedReleaseAim
        {
            public int ItemType;
            public string ItemName = string.Empty;
            public int Shoot;
            public int SelectedSlot = -1;
            public long RecordedGameUpdateCount;
            public float AimWorldX;
            public float AimWorldY;
            public int TargetWhoAmI = -1;
            public int TargetType;
            public string TargetName = string.Empty;
            public CombatAimWeaponProfile WeaponProfile;
            public CombatAimTargetSelection Selection;
            public CombatAimBallisticSolution BallisticSolution;
            public string AimRangeOrigin = string.Empty;
            public string AimTargetPriority = string.Empty;
            public int CursorAimRadius;
            public int PlayerAimRadius;
            public bool TrackDummy;
            public bool MarkerEnabled;

            public FlailCachedReleaseAim Clone()
            {
                return new FlailCachedReleaseAim
                {
                    ItemType = ItemType,
                    ItemName = ItemName ?? string.Empty,
                    Shoot = Shoot,
                    SelectedSlot = SelectedSlot,
                    RecordedGameUpdateCount = RecordedGameUpdateCount,
                    AimWorldX = AimWorldX,
                    AimWorldY = AimWorldY,
                    TargetWhoAmI = TargetWhoAmI,
                    TargetType = TargetType,
                    TargetName = TargetName ?? string.Empty,
                    WeaponProfile = WeaponProfile,
                    Selection = CombatAimFlailControlService.CloneSelection(Selection),
                    BallisticSolution = CombatAimFlailControlService.CloneBallisticSolution(BallisticSolution),
                    AimRangeOrigin = AimRangeOrigin ?? string.Empty,
                    AimTargetPriority = AimTargetPriority ?? string.Empty,
                    CursorAimRadius = CursorAimRadius,
                    PlayerAimRadius = PlayerAimRadius,
                    TrackDummy = TrackDummy,
                    MarkerEnabled = MarkerEnabled
                };
            }
        }

        internal static void SetLastDiagnosticsForTesting(CombatAimFlailDiagnostics diagnostics)
        {
            Publish(diagnostics == null ? CombatAimFlailDiagnostics.Empty() : diagnostics);
        }

        internal static void SetCachedReleaseAimForTesting(CombatAimItemCheckDecision decision, long recordedTick)
        {
            if (decision == null || decision.WeaponProfile == null)
            {
                ClearCachedReleaseAim();
                return;
            }

            lock (SyncRoot)
            {
                _cachedReleaseAim = new FlailCachedReleaseAim
                {
                    ItemType = decision.ItemType,
                    ItemName = decision.ItemName ?? string.Empty,
                    Shoot = decision.WeaponProfile.Shoot,
                    SelectedSlot = decision.SelectedSlot,
                    RecordedGameUpdateCount = recordedTick,
                    AimWorldX = decision.AimWorldX,
                    AimWorldY = decision.AimWorldY,
                    TargetWhoAmI = decision.Target == null ? -1 : decision.Target.WhoAmI,
                    TargetType = decision.Target == null ? 0 : decision.Target.Type,
                    TargetName = decision.Target == null ? string.Empty : decision.Target.Name ?? string.Empty,
                    WeaponProfile = decision.WeaponProfile,
                    Selection = CloneSelection(decision.Selection),
                    BallisticSolution = CloneBallisticSolution(decision.BallisticSolution),
                    AimRangeOrigin = decision.AimRangeOrigin ?? string.Empty,
                    AimTargetPriority = decision.AimTargetPriority ?? string.Empty,
                    CursorAimRadius = decision.CursorAimRadius,
                    PlayerAimRadius = decision.PlayerAimRadius,
                    TrackDummy = decision.TrackDummy,
                    MarkerEnabled = decision.MarkerEnabled
                };
            }
        }

        internal static void ResetForTesting()
        {
            ResetControlState();
            ClearFlailComboPressAim();
            Publish(CombatAimFlailDiagnostics.Empty());
        }
    }

    internal sealed class CombatAimFlailProjectileFrame
    {
        public int WhoAmI = -1;
        public int Type;
        public int Identity = -1;
        public bool Active;
        public float Ai0;
        public float VelocityX;
        public float VelocityY;

        public static CombatAimFlailProjectileFrame None()
        {
            return new CombatAimFlailProjectileFrame();
        }

        public static CombatAimFlailProjectileFrame ForTesting(
            bool active,
            int whoAmI,
            int type,
            int identity,
            float ai0,
            float velocityX,
            float velocityY)
        {
            return new CombatAimFlailProjectileFrame
            {
                Active = active,
                WhoAmI = whoAmI,
                Type = type,
                Identity = identity,
                Ai0 = ai0,
                VelocityX = velocityX,
                VelocityY = velocityY
            };
        }

        internal static CombatAimFlailProjectileFrame FromSnapshot(CombatAimFlailControlService.FlailProjectileSnapshot snapshot)
        {
            return snapshot == null
                ? None()
                : ForTesting(snapshot.Active, snapshot.WhoAmI, snapshot.Type, snapshot.Identity, snapshot.Ai0, snapshot.VelocityX, snapshot.VelocityY);
        }
    }

    public static class FlailControlStates
    {
        public const string Idle = "Idle";
        public const string ReadyToLaunch = "ReadyToLaunch";
        public const string SpinHold = "SpinHold";
        public const string LaunchPulse = "LaunchPulse";
        public const string ProjectileActive = "ProjectileActive";
        public const string ProjectileFlying = "ProjectileFlying";
        public const string ReleaseAfterLaunch = "ReleaseAfterLaunch";
        public const string ReleaseToTarget = "ReleaseToTarget";
        public const string WaitHitOrCollision = "WaitHitOrCollision";
        public const string ReattackPulse = "ReattackPulse";
        public const string StuckRecoveryRelease = "StuckRecoveryRelease";
        public const string ReleaseAfterPulse = "ReleaseAfterPulse";
        public const string Cooldown = "Cooldown";
        public const string Disabled = "Disabled";
    }

    public static class FlailReleaseKinds
    {
        public const string Launch = "launch";
        public const string Reattack = "reattack";
    }

    public sealed class CombatAimFlailControlDecision
    {
        public string State { get; private set; }
        public bool AttackPulse { get; private set; }
        public bool AttackRelease { get; private set; }
        public bool AttackSuppressed { get; private set; }
        public bool AttackRestored { get; private set; }
        public string BlockedReason { get; private set; }
        public string InputMode { get; private set; }
        public string PulseReason { get; private set; }
        public string ReleaseKind { get; private set; }

        private CombatAimFlailControlDecision(
            string state,
            bool attackPulse,
            bool attackRelease,
            bool attackSuppressed,
            bool attackRestored,
            string blockedReason,
            string inputMode,
            string pulseReason,
            string releaseKind)
        {
            State = state ?? string.Empty;
            AttackPulse = attackPulse;
            AttackRelease = attackRelease;
            AttackSuppressed = attackSuppressed;
            AttackRestored = attackRestored;
            BlockedReason = blockedReason ?? string.Empty;
            InputMode = inputMode ?? string.Empty;
            PulseReason = pulseReason ?? string.Empty;
            ReleaseKind = releaseKind ?? string.Empty;
        }

        public static CombatAimFlailControlDecision None(string state, string reason)
        {
            return new CombatAimFlailControlDecision(state, false, false, false, false, reason, "observe", string.Empty, string.Empty);
        }

        public static CombatAimFlailControlDecision Release(string state, string reason, bool restored)
        {
            return new CombatAimFlailControlDecision(state, false, true, true, restored, reason, "controlledUseItemRelease", string.Empty, string.Empty);
        }

        public static CombatAimFlailControlDecision Pulse(string state, string reason, string releaseKind)
        {
            return new CombatAimFlailControlDecision(state, true, false, false, false, reason, "controlledUseItemPulse", reason, releaseKind);
        }
    }

    public sealed class CombatAimFlailDiagnostics
    {
        public int ItemType;
        public string ItemName;
        public bool Eligible;
        public string Reason;
        public bool Active;
        public string State;
        public int ProjectileWhoAmI;
        public int ProjectileType;
        public int ProjectileAiStyle;
        public float ProjectileAi0;
        public float ProjectileVelocityX;
        public float ProjectileVelocityY;
        public int ProjectileIdentity;
        public bool HitDetected;
        public bool CollisionDetected;
        public bool LocalNpcImmunityChanged;
        public bool TileCollisionDetected;
        public bool AttackPulse;
        public bool AttackRelease;
        public bool AttackSuppressed;
        public bool AttackRestored;
        public string BlockedReason;
        public string InputMode;
        public string InputPhase;
        public string TakeoverScope;
        public string StuckRecovery;
        public bool ReleaseSuppressedPhysicalInput;
        public bool PhysicalUseItemHeld;
        public bool PhysicalReleasePending;
        public string PulseReason;
        public bool CachedReleaseAim;
        public int CachedReleaseAimAgeTicks;
        public string CachedReleaseAimReason;

        public static CombatAimFlailDiagnostics Empty()
        {
            return new CombatAimFlailDiagnostics
            {
                ItemType = 0,
                ItemName = string.Empty,
                Reason = "notEvaluated",
                State = FlailControlStates.Idle,
                ProjectileWhoAmI = -1,
                ProjectileIdentity = -1,
                BlockedReason = string.Empty,
                InputMode = "observe",
                InputPhase = string.Empty,
                TakeoverScope = "none",
                StuckRecovery = "none",
                PhysicalUseItemHeld = false,
                PhysicalReleasePending = false,
                PulseReason = string.Empty,
                CachedReleaseAim = false,
                CachedReleaseAimAgeTicks = -1,
                CachedReleaseAimReason = string.Empty
            };
        }

        public CombatAimFlailDiagnostics Clone()
        {
            return new CombatAimFlailDiagnostics
            {
                ItemType = ItemType,
                ItemName = ItemName ?? string.Empty,
                Eligible = Eligible,
                Reason = Reason ?? string.Empty,
                Active = Active,
                State = State ?? string.Empty,
                ProjectileWhoAmI = ProjectileWhoAmI,
                ProjectileType = ProjectileType,
                ProjectileAiStyle = ProjectileAiStyle,
                ProjectileAi0 = ProjectileAi0,
                ProjectileVelocityX = ProjectileVelocityX,
                ProjectileVelocityY = ProjectileVelocityY,
                ProjectileIdentity = ProjectileIdentity,
                HitDetected = HitDetected,
                CollisionDetected = CollisionDetected,
                LocalNpcImmunityChanged = LocalNpcImmunityChanged,
                TileCollisionDetected = TileCollisionDetected,
                AttackPulse = AttackPulse,
                AttackRelease = AttackRelease,
                AttackSuppressed = AttackSuppressed,
                AttackRestored = AttackRestored,
                BlockedReason = BlockedReason ?? string.Empty,
                InputMode = InputMode ?? string.Empty,
                InputPhase = InputPhase ?? string.Empty,
                TakeoverScope = TakeoverScope ?? string.Empty,
                StuckRecovery = StuckRecovery ?? string.Empty,
                ReleaseSuppressedPhysicalInput = ReleaseSuppressedPhysicalInput,
                PhysicalUseItemHeld = PhysicalUseItemHeld,
                PhysicalReleasePending = PhysicalReleasePending,
                PulseReason = PulseReason ?? string.Empty,
                CachedReleaseAim = CachedReleaseAim,
                CachedReleaseAimAgeTicks = CachedReleaseAimAgeTicks,
                CachedReleaseAimReason = CachedReleaseAimReason ?? string.Empty
            };
        }
    }
}
