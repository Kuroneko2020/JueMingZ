using System;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAimPersistentCursorService
    {
        private const long SpecialProjectileTailTicks = 120;
        private const long FlailReleaseTailTicks = 24;
        private static readonly object SyncRoot = new object();
        private static ActiveOverride _mainUpdateActive;
        private static string _persistentHook = PersistentCursorHooks.MainUpdateFallback;
        private static FrameCacheEntry _frameCache;
        private static bool _projectileAiScopedActive;
        private static SpecialProjectileTailEntry _specialProjectileTail;
        private static FlailReleaseTailEntry _flailReleaseTail;

        public static string PersistentHook
        {
            get { lock (SyncRoot) { return _persistentHook; } }
        }

        public static void MarkHookInstalled(string hook)
        {
            lock (SyncRoot)
            {
                _persistentHook = string.IsNullOrWhiteSpace(hook) ? PersistentCursorHooks.None : hook;
            }
        }

        public static void MarkHookFailed(string reason)
        {
            lock (SyncRoot)
            {
                _persistentHook = PersistentCursorHooks.MainUpdateFallback;
            }

            LogThrottle.WarnThrottled(
                "combat-aim-persistent-hook-failed",
                TimeSpan.FromSeconds(10),
                "CombatAimPersistentCursorService",
                "Persistent cursor projectile hook failed; using Main.Update fallback: " + (reason ?? string.Empty));
        }

        public static void BeginFrame()
        {
            bool allowYoyoMainUpdateFallback;
            if (!ShouldUseMainUpdateFallback(out allowYoyoMainUpdateFallback))
            {
                return;
            }

            ActiveOverride active;
            if (TryBeginOverride(null, PersistentCursorHooks.MainUpdateFallback, allowYoyoMainUpdateFallback, out active))
            {
                lock (SyncRoot)
                {
                    if (_mainUpdateActive != null)
                    {
                        EndFrameLocked();
                    }

                    _mainUpdateActive = active;
                }
            }
        }

        public static void EndFrame()
        {
            lock (SyncRoot)
            {
                EndFrameLocked();
            }
        }

        public static bool TryBeginProjectileAi(object projectile, out ActiveOverride active)
        {
            active = null;
            // ProjectileAI scopes may override cursor for the active projectile
            // only; projectile state remains vanilla-owned.
            var hook = PersistentHook;
            if (!string.Equals(hook, PersistentCursorHooks.ProjectileAI, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(hook, PersistentCursorHooks.AI099, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryBeginOverride(projectile, hook, true, out active);
        }

        public static bool TryBeginProjectileKill(object projectile, out ActiveOverride active)
        {
            active = null;
            return TryBeginOverride(projectile, PersistentCursorHooks.ProjectileKill, false, out active);
        }

        public static void EndProjectileAi(ActiveOverride active)
        {
            EndOverride(active);
        }

        public static void EndProjectileKill(ActiveOverride active)
        {
            EndOverride(active);
        }

        public static bool RememberSpecialProjectileTail(CombatAimItemCheckDecision decision)
        {
            if (!IsSpecialProjectileTailCandidate(decision))
            {
                return false;
            }

            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);
            RememberSpecialProjectileTail(decision, tick);
            return true;
        }

        public static bool RememberFlailReleaseTail(CombatAimItemCheckDecision decision)
        {
            if (!IsFlailReleaseTailCandidate(decision))
            {
                return false;
            }

            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);
            RememberFlailReleaseTail(decision, tick);
            return true;
        }

        private static bool TryBeginOverride(object projectile, string hook, bool allowYoyoMainUpdateFallback, out ActiveOverride active)
        {
            active = null;
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.PersistentCursorAimEnabled || Clamp(settings.CursorAimRadius, 0, 50) <= 0)
                {
                    return false;
                }

                int projectileWhoAmI = -1;
                int projectileType = 0;
                int projectileAiStyle = 0;
                string projectileReason = string.Empty;
                var yoyoProjectileMatch = false;

                object player;
                if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    return false;
                }

                CombatAimUseInputSnapshot input;
                var inputAvailable = TerrariaInputCompat.TryReadCombatAimUseInputSnapshot(player, out input);
                var hasCurrentUseWindow = inputAvailable &&
                                          (input.UseItemHeld || input.ItemAnimation > 0 || input.ItemTime > 0);
                if (!hasCurrentUseWindow && projectile == null)
                {
                    return false;
                }

                var ui = TerrariaInputCompat.ReadUiInputContext(player);
                if (ui.MainTypeUnavailable || ui.GameMenu || ui.MouseCapturedByUi)
                {
                    return false;
                }

                if (projectile != null && TryBeginFlailReleaseTailOverride(projectile, player, hook, ui, out active))
                {
                    return true;
                }

                if (projectile != null && TryBeginSpecialProjectileTailOverride(projectile, player, hook, ui, out active))
                {
                    return true;
                }

                if (string.Equals(hook, PersistentCursorHooks.ProjectileKill, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!hasCurrentUseWindow)
                {
                    return false;
                }

                CombatAimItemCheckDecision decision;
                bool cacheHit;
                if (!TryGetFrameDecision(player, settings, input, projectileWhoAmI, projectileType, projectileAiStyle, hook, out decision, out cacheHit))
                {
                    return false;
                }

                var eligibility = CombatAimPersistentCursorPolicy.Evaluate(player, decision.WeaponProfile);
                if (!eligibility.Eligible)
                {
                    return false;
                }

                if (projectile != null && string.Equals(eligibility.Class, "yoyo", StringComparison.Ordinal))
                {
                    yoyoProjectileMatch = CombatAimYoyoCompat.IsLocalOwnedYoyoProjectile(
                        projectile,
                        out projectileWhoAmI,
                        out projectileType,
                        out projectileAiStyle,
                        out projectileReason);
                }

                CombatAimProjectileCursorMatch projectileMatch = CombatAimProjectileCursorMatch.NotEvaluated();
                if (projectile == null)
                {
                    if ((!allowYoyoMainUpdateFallback && string.Equals(eligibility.Class, "yoyo", StringComparison.Ordinal)) ||
                        !CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(hook, eligibility))
                    {
                        return false;
                    }
                }
                else if (!TryMatchProjectileAiScoped(
                    projectile,
                    player,
                    hook,
                    eligibility,
                    yoyoProjectileMatch,
                    projectileReason,
                    projectileWhoAmI,
                    projectileType,
                    projectileAiStyle,
                    decision,
                    out projectileMatch))
                {
                    return false;
                }

                decision.PersistentHook = hook;
                decision.PersistentCursorFrameCached = cacheHit;
                decision.YoyoProjectileWhoAmI = projectileWhoAmI;
                decision.YoyoProjectileType = projectileType;
                decision.YoyoProjectileAiStyle = projectileAiStyle;
                decision.YoyoDetected = eligibility.YoyoDetected;
                decision.PersistentCursorActive = true;
                decision.PersistentCursorReason = ResolvePersistentCursorReason(eligibility);
                RememberSpecialProjectileTail(decision);
                CombatAimProjectileCursorCompat.AttachDecisionMetadata(
                    decision,
                    projectile == null ? CombatAimProjectileCursorMatch.NotEvaluated() : projectileMatch,
                    projectile != null,
                    CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(hook, eligibility),
                    eligibility.VisibleCursorHijackRiskMitigated);

                MouseTargetInputState restoreState;
                if (!TerrariaInputCompat.TryCaptureMouseTargetState(player, out restoreState))
                {
                    CombatAimItemCheckService.RecordItemCheckAim(
                        decision,
                        "Failed",
                        DiagnosticResultCode.Failed,
                        "Combat persistent cursor aim failed to capture mouse state: " + TerrariaInputCompat.LastInputCompatError,
                        false,
                        false);
                    return false;
                }

                if (!TerrariaInputCompat.TrySetMouseWorldPosition(decision.AimWorldX, decision.AimWorldY))
                {
                    TerrariaInputCompat.TryRestoreMouseTargetState(restoreState);
                    CombatAimItemCheckService.RecordItemCheckAim(
                        decision,
                        "Failed",
                        DiagnosticResultCode.Failed,
                        "Combat persistent cursor aim failed to apply mouse target: " + TerrariaInputCompat.LastInputCompatError,
                        false,
                        true);
                    return false;
                }

                active = new ActiveOverride
                {
                    Decision = decision,
                    RestoreState = restoreState,
                    ProjectileAiScoped = projectile != null,
                    ScopeHook = hook
                };
                if (active.ProjectileAiScoped)
                {
                    lock (SyncRoot)
                    {
                        _projectileAiScopedActive = true;
                    }
                }

                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("CombatAimPersistentCursorService.TryBeginOverride", error);
                LogThrottle.ErrorThrottled(
                    "combat-aim-persistent-override-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAimPersistentCursorService",
                    "Persistent cursor override failed; exception swallowed.", error);
                return false;
            }
        }

        private static bool TryBeginFlailReleaseTailOverride(
            object projectile,
            object player,
            string hook,
            TerrariaUiInputContext ui,
            out ActiveOverride active)
        {
            active = null;
            if (projectile == null ||
                !IsSpecialProjectileTailScopedHook(hook))
            {
                return false;
            }

            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);

            CombatAimItemCheckDecision decision;
            if (!TryGetFlailReleaseTailDecision(tick, ui, out decision))
            {
                return false;
            }

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(
                decision.WeaponProfile,
                false,
                false,
                decision.PersistentCursorReason);
            if (eligibility == null ||
                !eligibility.Eligible ||
                !string.Equals(eligibility.Class, "flailAiStyle15", StringComparison.Ordinal))
            {
                return false;
            }

            lock (SyncRoot)
            {
                // Only one ProjectileAI cursor scope may be active at a time.
                // Failure to prove ownership keeps the path closed.
                if (_projectileAiScopedActive)
                {
                    return false;
                }
            }

            // Flail release tail must match the local owner's active flail
            // projectile before any cursor override is allowed.
            var projectileMatch = CombatAimProjectileCursorCompat.MatchFlailProjectile(
                projectile,
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (!projectileMatch.Matches)
            {
                return false;
            }

            decision.PersistentHook = hook;
            decision.PersistentCursorFrameCached = true;
            decision.PersistentCursorActive = true;
            decision.PersistentCursorReason = "flailAiStyle15Release";
            decision.AimApplyMode = CombatAimApplyModes.PersistentCursor;
            decision.GameUpdateCount = tick;
            CombatAimFlailControlService.MarkProjectileAiScopedTakeover(decision, projectileMatch);
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(
                decision,
                projectileMatch,
                true,
                true,
                true);

            MouseTargetInputState restoreState;
            if (!TerrariaInputCompat.TryCaptureMouseTargetState(player, out restoreState))
            {
                CombatAimItemCheckService.RecordItemCheckAim(
                    decision,
                    "Failed",
                    DiagnosticResultCode.Failed,
                    "Combat flail Projectile.AI release aim failed to capture mouse state: " + TerrariaInputCompat.LastInputCompatError,
                    false,
                    false);
                return false;
            }

            if (!TerrariaInputCompat.TrySetMouseWorldPosition(decision.AimWorldX, decision.AimWorldY))
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(restoreState);
                CombatAimItemCheckService.RecordItemCheckAim(
                    decision,
                    "Failed",
                    DiagnosticResultCode.Failed,
                    "Combat flail Projectile.AI release aim failed to apply mouse target: " + TerrariaInputCompat.LastInputCompatError,
                    false,
                    true);
                return false;
            }

            active = new ActiveOverride
            {
                Decision = decision,
                RestoreState = restoreState,
                ProjectileAiScoped = true,
                ScopeHook = hook
            };

            lock (SyncRoot)
            {
                _projectileAiScopedActive = true;
            }

            return true;
        }

        private static bool TryBeginSpecialProjectileTailOverride(
            object projectile,
            object player,
            string hook,
            TerrariaUiInputContext ui,
            out ActiveOverride active)
        {
            active = null;
            if (!ShouldAttemptSpecialProjectileTailOverride(projectile, hook))
            {
                return false;
            }

            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);

            CombatAimItemCheckDecision decision;
            if (!TryGetSpecialProjectileTailDecision(tick, ui, out decision))
            {
                return false;
            }

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(player, decision.WeaponProfile);
            if (eligibility == null ||
                !eligibility.Eligible ||
                !string.Equals(eligibility.Class, "specialProjectileWeapon", StringComparison.Ordinal))
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (_projectileAiScopedActive)
                {
                    return false;
                }
            }

            var projectileMatch = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                projectile,
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (!projectileMatch.Matches)
            {
                string matchExpiredReason;
                if (ShouldExpireSpecialProjectileTailForMatchFailure(decision, projectileMatch, out matchExpiredReason))
                {
                    ExpireAndRecordSpecialProjectileTail(decision, projectileMatch, matchExpiredReason, hook);
                }

                return false;
            }

            RefreshSpecialProjectileTailLease(projectileMatch, tick);

            string expiredReason;
            if (!IsSpecialProjectileTailTargetStillValid(decision, out expiredReason))
            {
                ExpireAndRecordSpecialProjectileTail(decision, projectileMatch, expiredReason, hook);
                return false;
            }

            bool recomputedAim;
            CombatAimItemCheckDecision selectedDecision;
            if (!TryResolveSpecialProjectileTailAimDecision(
                projectile,
                player,
                decision,
                projectileMatch,
                out selectedDecision,
                out projectileMatch,
                out recomputedAim,
                out expiredReason))
            {
                ExpireAndRecordSpecialProjectileTail(decision, projectileMatch, expiredReason, hook);
                return false;
            }

            decision = selectedDecision;
            decision.PersistentHook = hook;
            decision.PersistentCursorFrameCached = !recomputedAim;
            decision.PersistentCursorActive = true;
            decision.PersistentCursorReason = "specialProjectileWeapon";
            decision.AimApplyMode = CombatAimApplyModes.PersistentCursor;
            decision.GameUpdateCount = tick;
            decision.SpecialProjectileTailActive = true;
            decision.SpecialProjectileTailRecomputedAim = recomputedAim;
            decision.SpecialProjectileTailExpiredReason = "none";
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(
                decision,
                projectileMatch,
                true,
                true,
                true);

            MouseTargetInputState restoreState;
            if (!TerrariaInputCompat.TryCaptureMouseTargetState(player, out restoreState))
            {
                CombatAimItemCheckService.RecordItemCheckAim(
                    decision,
                    "Failed",
                    DiagnosticResultCode.Failed,
                    "Combat special projectile tail aim failed to capture mouse state: " + TerrariaInputCompat.LastInputCompatError,
                    false,
                    false);
                return false;
            }

            if (!TerrariaInputCompat.TrySetMouseWorldPosition(decision.AimWorldX, decision.AimWorldY))
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(restoreState);
                CombatAimItemCheckService.RecordItemCheckAim(
                    decision,
                    "Failed",
                    DiagnosticResultCode.Failed,
                    "Combat special projectile tail aim failed to apply mouse target: " + TerrariaInputCompat.LastInputCompatError,
                    false,
                    true);
                return false;
            }

            active = new ActiveOverride
            {
                Decision = decision,
                RestoreState = restoreState,
                ProjectileAiScoped = true,
                ScopeHook = hook
            };

            lock (SyncRoot)
            {
                _projectileAiScopedActive = true;
            }

            return true;
        }

        private static void RememberSpecialProjectileTail(CombatAimItemCheckDecision decision, long tick)
        {
            var copy = CloneSpecialProjectileTailDecision(decision);
            if (copy == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _specialProjectileTail = new SpecialProjectileTailEntry
                {
                    Decision = copy,
                    ExpiresAtTick = tick > 0 ? tick + SpecialProjectileTailTicks : 0,
                    ExpiresAtUtc = DateTime.UtcNow.AddSeconds(3)
                };
            }
        }

        private static void RememberFlailReleaseTail(CombatAimItemCheckDecision decision, long tick)
        {
            var copy = CloneFlailReleaseTailDecision(decision);
            if (copy == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _flailReleaseTail = new FlailReleaseTailEntry
                {
                    Decision = copy,
                    ExpiresAtTick = tick > 0 ? tick + FlailReleaseTailTicks : 0,
                    ExpiresAtUtc = DateTime.UtcNow.AddMilliseconds(600)
                };
            }
        }

        private static bool TryGetSpecialProjectileTailDecision(long tick, TerrariaUiInputContext ui, out CombatAimItemCheckDecision decision)
        {
            decision = null;
            lock (SyncRoot)
            {
                if (_specialProjectileTail == null || _specialProjectileTail.Decision == null)
                {
                    return false;
                }

                if (IsSpecialProjectileTailExpired(_specialProjectileTail, tick))
                {
                    _specialProjectileTail = null;
                    return false;
                }

                decision = CloneSpecialProjectileTailDecision(_specialProjectileTail.Decision);
            }

            if (decision == null)
            {
                return false;
            }

            decision.UiContext = ui ?? decision.UiContext;
            decision.UseItemHeld = false;
            decision.UseItemReleased = true;
            decision.ItemAnimation = 0;
            decision.ItemTime = 0;
            return true;
        }

        private static bool TryResolveSpecialProjectileTailAimDecision(
            object projectile,
            object player,
            CombatAimItemCheckDecision tailDecision,
            CombatAimProjectileCursorMatch tailMatch,
            out CombatAimItemCheckDecision selectedDecision,
            out CombatAimProjectileCursorMatch selectedMatch,
            out bool recomputedAim,
            out string expiredReason)
        {
            selectedDecision = null;
            selectedMatch = tailMatch;
            recomputedAim = false;
            expiredReason = string.Empty;

            CombatAimItemCheckDecision recomputedDecision;
            var recomputeAvailable = CombatAimItemCheckService.TryCreatePersistentCursorTailAimDecision(player, out recomputedDecision);
            if (recomputeAvailable)
            {
                if (!IsSameSpecialProjectileTailWeapon(tailDecision, recomputedDecision == null ? null : recomputedDecision.WeaponProfile, out expiredReason))
                {
                    expiredReason = "ruleMismatch:" + expiredReason;
                    return false;
                }

                var recomputedMatch = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                    projectile,
                    player,
                    recomputedDecision.WeaponProfile,
                    recomputedDecision.BallisticSolution);
                if (!recomputedMatch.Matches)
                {
                    expiredReason = "ruleMismatch:" + recomputedMatch.Reason;
                    return false;
                }

                if (!TryChooseSpecialProjectileTailAimDecision(
                    tailDecision,
                    recomputedDecision,
                    true,
                    false,
                    string.Empty,
                    out selectedDecision,
                    out recomputedAim,
                    out expiredReason))
                {
                    return false;
                }

                selectedMatch = recomputedMatch;
                return true;
            }

            if (recomputedDecision != null &&
                recomputedDecision.WeaponProfile != null &&
                !IsSameSpecialProjectileTailWeapon(tailDecision, recomputedDecision.WeaponProfile, out expiredReason))
            {
                expiredReason = "ruleMismatch:" + expiredReason;
                return false;
            }

            string fallbackUnsafeReason;
            var fallbackSafe = IsSpecialProjectileTailFallbackSafe(player, tailDecision, out fallbackUnsafeReason);
            return TryChooseSpecialProjectileTailAimDecision(
                tailDecision,
                recomputedDecision,
                false,
                fallbackSafe,
                fallbackUnsafeReason,
                out selectedDecision,
                out recomputedAim,
                out expiredReason);
        }

        private static bool TryChooseSpecialProjectileTailAimDecision(
            CombatAimItemCheckDecision tailDecision,
            CombatAimItemCheckDecision recomputedDecision,
            bool recomputeAvailable,
            bool fallbackSafe,
            string fallbackUnsafeReason,
            out CombatAimItemCheckDecision selectedDecision,
            out bool recomputedAim,
            out string expiredReason)
        {
            selectedDecision = null;
            recomputedAim = false;
            expiredReason = string.Empty;

            if (recomputeAvailable && recomputedDecision != null)
            {
                selectedDecision = recomputedDecision;
                recomputedAim = true;
                expiredReason = "none";
                return true;
            }

            if (fallbackSafe && tailDecision != null)
            {
                selectedDecision = tailDecision;
                recomputedAim = false;
                expiredReason = "none";
                return true;
            }

            expiredReason = string.IsNullOrWhiteSpace(fallbackUnsafeReason)
                ? "recomputeUnavailable"
                : fallbackUnsafeReason;
            return false;
        }

        private static bool IsSpecialProjectileTailTargetStillValid(CombatAimItemCheckDecision decision, out string expiredReason)
        {
            expiredReason = string.Empty;
            var target = decision == null ? null : decision.Target;
            if (target == null)
            {
                expiredReason = "targetInvalid:missingTailTarget";
                return false;
            }

            CombatTargetSnapshot refreshed;
            string skipReason;
            if (!CombatAimTargetReader.TryReadTargetByIdentity(
                target.WhoAmI,
                target.Type,
                decision.TrackDummy,
                out refreshed,
                out skipReason))
            {
                expiredReason = "targetInvalid:" + (string.IsNullOrWhiteSpace(skipReason) ? "unavailable" : skipReason);
                return false;
            }

            return true;
        }

        private static bool IsSpecialProjectileTailFallbackSafe(
            object player,
            CombatAimItemCheckDecision decision,
            out string unsafeReason)
        {
            unsafeReason = string.Empty;
            if (!IsCurrentSelectedWeaponSameAsTail(player, decision, out unsafeReason))
            {
                unsafeReason = "ruleMismatch:" + unsafeReason;
                return false;
            }

            var target = decision == null ? null : decision.Target;
            if (target == null)
            {
                unsafeReason = "targetInvalid:missingTailTarget";
                return false;
            }

            CombatTargetSnapshot refreshed;
            string skipReason;
            if (!CombatAimTargetReader.TryReadTargetByIdentity(
                target.WhoAmI,
                target.Type,
                decision.TrackDummy,
                out refreshed,
                out skipReason))
            {
                unsafeReason = "targetInvalid:" + (string.IsNullOrWhiteSpace(skipReason) ? "unavailable" : skipReason);
                return false;
            }

            if (Math.Abs(refreshed.CenterX - target.CenterX) > 1f ||
                Math.Abs(refreshed.CenterY - target.CenterY) > 1f)
            {
                unsafeReason = "targetMovedWithoutRecompute";
                return false;
            }

            return true;
        }

        private static bool IsCurrentSelectedWeaponSameAsTail(
            object player,
            CombatAimItemCheckDecision tailDecision,
            out string reason)
        {
            reason = string.Empty;
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) || selectedSlot < 0)
            {
                reason = "selectedItemUnavailable";
                return false;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null || selectedSlot >= inventory.Count)
            {
                reason = "inventoryUnavailable";
                return false;
            }

            var item = inventory[selectedSlot];
            if (item == null)
            {
                reason = "selectedItemMissing";
                return false;
            }

            return IsSameSpecialProjectileTailWeapon(tailDecision, CombatAimWeaponProfile.Read(player, item), out reason);
        }

        private static bool IsSameSpecialProjectileTailWeapon(
            CombatAimItemCheckDecision tailDecision,
            CombatAimWeaponProfile currentProfile,
            out string reason)
        {
            reason = string.Empty;
            var tailProfile = tailDecision == null ? null : tailDecision.WeaponProfile;
            if (tailProfile == null || currentProfile == null || currentProfile.IsEmpty)
            {
                reason = "weaponProfileUnavailable";
                return false;
            }

            if (tailProfile.ItemType != currentProfile.ItemType)
            {
                reason = "itemTypeChanged";
                return false;
            }

            CombatAimSpecialWeaponRule tailRule;
            CombatAimSpecialWeaponRule currentRule;
            if (!CombatAimSpecialWeaponRuleResolver.TryResolve(tailProfile, out tailRule) ||
                !CombatAimSpecialWeaponRuleResolver.TryResolve(currentProfile, out currentRule))
            {
                reason = "specialRuleUnavailable";
                return false;
            }

            if (!tailRule.AllowsProjectileAiScoped || !currentRule.AllowsProjectileAiScoped)
            {
                reason = "projectileAiScopedNotAllowed";
                return false;
            }

            if (!string.Equals(tailRule.Kind, currentRule.Kind, StringComparison.Ordinal) ||
                !string.Equals(tailRule.Name, currentRule.Name, StringComparison.Ordinal))
            {
                reason = "specialRuleChanged";
                return false;
            }

            return true;
        }

        private static bool ShouldExpireSpecialProjectileTailForMatchFailure(
            CombatAimItemCheckDecision decision,
            CombatAimProjectileCursorMatch match,
            out string expiredReason)
        {
            expiredReason = string.Empty;
            if (decision == null || match == null)
            {
                return false;
            }

            if (!IsExpectedSpecialProjectileTailType(match.ProjectileType, decision))
            {
                return false;
            }

            if (string.Equals(match.Reason, "notEligible:projectileInactive", StringComparison.Ordinal))
            {
                expiredReason = "projectileInactive";
                return true;
            }

            if (string.Equals(match.Reason, "notEligible:notLocalOwnedProjectile", StringComparison.Ordinal))
            {
                expiredReason = "ownerMismatch";
                return true;
            }

            if (string.Equals(match.Reason, "notEligible:notFriendlyProjectile", StringComparison.Ordinal) ||
                string.Equals(match.Reason, "notEligible:hostileProjectile", StringComparison.Ordinal))
            {
                expiredReason = "projectileInvalid:" + match.Reason;
                return true;
            }

            if (!match.Matches)
            {
                expiredReason = "projectileMatchFailed:" + match.Reason;
                return true;
            }

            return false;
        }

        private static bool IsSpecialProjectileTailScopedHook(string hook)
        {
            return string.Equals(hook, PersistentCursorHooks.ProjectileAI, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(hook, PersistentCursorHooks.ProjectileKill, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExpectedSpecialProjectileTailType(int projectileType, CombatAimItemCheckDecision decision)
        {
            return projectileType > 0 &&
                   decision != null &&
                   CombatAimSpecialWeaponRuleResolver.MatchesScopedProjectile(
                       projectileType,
                       decision.WeaponProfile,
                       decision.BallisticSolution);
        }

        private static void ExpireAndRecordSpecialProjectileTail(
            CombatAimItemCheckDecision decision,
            CombatAimProjectileCursorMatch match,
            string expiredReason,
            string hook)
        {
            ExpireSpecialProjectileTail(expiredReason);
            if (decision == null)
            {
                return;
            }

            decision.SpecialProjectileTailActive = false;
            decision.SpecialProjectileTailRecomputedAim = false;
            decision.SpecialProjectileTailExpiredReason = string.IsNullOrWhiteSpace(expiredReason) ? "expired" : expiredReason;
            decision.PersistentHook = string.IsNullOrWhiteSpace(hook) ? PersistentCursorHooks.ProjectileAI : hook;
            decision.PersistentCursorReason = "specialProjectileWeapon";
            decision.AimApplyMode = CombatAimApplyModes.PersistentCursor;
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(
                decision,
                match ?? CombatAimProjectileCursorMatch.NotEvaluated(),
                false,
                true,
                true);
            CombatAimItemCheckService.RecordItemCheckAim(
                decision,
                "Skipped",
                DiagnosticResultCode.NotApplicable,
                "Combat special projectile tail expired: " + decision.SpecialProjectileTailExpiredReason,
                false,
                false);
        }

        private static void ExpireSpecialProjectileTail(string expiredReason)
        {
            lock (SyncRoot)
            {
                if (_specialProjectileTail != null && _specialProjectileTail.Decision != null)
                {
                    _specialProjectileTail.Decision.SpecialProjectileTailExpiredReason = string.IsNullOrWhiteSpace(expiredReason)
                        ? "expired"
                        : expiredReason;
                }

                _specialProjectileTail = null;
            }
        }

        private static bool TryGetFlailReleaseTailDecision(long tick, TerrariaUiInputContext ui, out CombatAimItemCheckDecision decision)
        {
            decision = null;
            lock (SyncRoot)
            {
                if (_flailReleaseTail == null || _flailReleaseTail.Decision == null)
                {
                    return false;
                }

                if (IsFlailReleaseTailExpired(_flailReleaseTail, tick))
                {
                    _flailReleaseTail = null;
                    return false;
                }

                decision = CloneFlailReleaseTailDecision(_flailReleaseTail.Decision);
            }

            if (decision == null)
            {
                return false;
            }

            decision.UiContext = ui ?? decision.UiContext;
            decision.UseItemHeld = false;
            decision.UseItemReleased = true;
            decision.WasUseItemHeldLastTick = true;
            decision.ReleasedThisTick = true;
            decision.ReleaseDetected = true;
            decision.ItemAnimation = 0;
            decision.ItemTime = 0;
            return true;
        }

        private static bool ShouldAttemptSpecialProjectileTailOverride(object projectile, string hook)
        {
            return projectile != null && IsSpecialProjectileTailScopedHook(hook);
        }

        private static void RefreshSpecialProjectileTailLease(CombatAimProjectileCursorMatch match, long tick)
        {
            if (match == null || !match.Matches)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_specialProjectileTail == null)
                {
                    return;
                }

                if (tick > 0)
                {
                    _specialProjectileTail.ExpiresAtTick = tick + SpecialProjectileTailTicks;
                }

                _specialProjectileTail.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(3);
                _specialProjectileTail.BoundToActiveProjectile = true;
                _specialProjectileTail.BoundProjectileWhoAmI = match.ProjectileWhoAmI;
                _specialProjectileTail.BoundProjectileType = match.ProjectileType;
                _specialProjectileTail.BoundProjectileOwner = match.ProjectileOwner;
                _specialProjectileTail.BoundAtTick = tick;
            }
        }

        private static bool IsSpecialProjectileTailExpired(SpecialProjectileTailEntry entry, long tick)
        {
            if (entry == null)
            {
                return true;
            }

            if (tick > 0 && entry.ExpiresAtTick > 0)
            {
                return tick > entry.ExpiresAtTick;
            }

            return DateTime.UtcNow > entry.ExpiresAtUtc;
        }

        private static bool IsFlailReleaseTailExpired(FlailReleaseTailEntry entry, long tick)
        {
            if (entry == null)
            {
                return true;
            }

            if (tick > 0 && entry.ExpiresAtTick > 0)
            {
                return tick > entry.ExpiresAtTick;
            }

            return DateTime.UtcNow > entry.ExpiresAtUtc;
        }

        private static bool IsSpecialProjectileTailCandidate(CombatAimItemCheckDecision decision)
        {
            if (decision == null || decision.WeaponProfile == null || decision.BallisticSolution == null)
            {
                return false;
            }

            CombatAimSpecialWeaponRule rule;
            return CombatAimSpecialWeaponRuleResolver.TryResolve(decision.WeaponProfile, out rule) &&
                   rule.AllowsProjectileAiScoped;
        }

        private static bool IsFlailReleaseTailCandidate(CombatAimItemCheckDecision decision)
        {
            if (decision == null || decision.WeaponProfile == null || decision.BallisticSolution == null)
            {
                return false;
            }

            var isYoyo = CombatAimYoyoCompat.IsYoyoProjectileType(decision.WeaponProfile.Shoot);
            var eligibility = CombatAimFlailPolicy.Evaluate(decision.WeaponProfile, decision.BallisticSolution.ProjectileAiStyle, isYoyo);
            if (!eligibility.Eligible)
            {
                return false;
            }

            var diagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(decision);
            return diagnostics != null &&
                   diagnostics.Active &&
                   diagnostics.AttackRelease &&
                   (string.Equals(diagnostics.State, FlailControlStates.ReleaseToTarget, StringComparison.Ordinal) ||
                    string.Equals(diagnostics.State, FlailControlStates.StuckRecoveryRelease, StringComparison.Ordinal));
        }

        private static CombatAimItemCheckDecision CloneSpecialProjectileTailDecision(CombatAimItemCheckDecision source)
        {
            if (source == null)
            {
                return null;
            }

            return new CombatAimItemCheckDecision
            {
                Enabled = source.Enabled,
                RadiusTiles = source.RadiusTiles,
                AimRangeOrigin = source.AimRangeOrigin,
                AimTargetPriority = source.AimTargetPriority,
                ActiveRangeMode = source.ActiveRangeMode,
                CursorAimRadius = source.CursorAimRadius,
                PlayerAimRadius = source.PlayerAimRadius,
                PlayerScreenMarginTiles = source.PlayerScreenMarginTiles,
                PlayerScreenRadiusTiles = source.PlayerScreenRadiusTiles,
                HasCursorWorld = source.HasCursorWorld,
                CursorWorldX = source.CursorWorldX,
                CursorWorldY = source.CursorWorldY,
                RangeCenterWorldX = source.RangeCenterWorldX,
                RangeCenterWorldY = source.RangeCenterWorldY,
                TrackDummy = source.TrackDummy,
                MarkerEnabled = source.MarkerEnabled,
                BridgePending = source.BridgePending,
                UseItemHeld = source.UseItemHeld,
                UseItemReleased = source.UseItemReleased,
                WasUseItemHeldLastTick = source.WasUseItemHeldLastTick,
                ReleasedThisTick = source.ReleasedThisTick,
                ReleaseDetected = source.ReleaseDetected,
                ItemAnimation = source.ItemAnimation,
                ItemTime = source.ItemTime,
                GameUpdateCount = source.GameUpdateCount,
                AimApplyMode = CombatAimApplyModes.PersistentCursor,
                ReleaseHoldState = source.ReleaseHoldState,
                ReleaseHoldArmed = source.ReleaseHoldArmed,
                ReleaseHoldPending = source.ReleaseHoldPending,
                ReleaseHoldConsumed = source.ReleaseHoldConsumed,
                ReleaseHoldActive = source.ReleaseHoldActive,
                ReleaseHoldTicksRemaining = source.ReleaseHoldTicksRemaining,
                ReleaseHoldApplyCount = source.ReleaseHoldApplyCount,
                ReleaseHoldValidationMode = source.ReleaseHoldValidationMode,
                ReleaseHoldValidationReason = source.ReleaseHoldValidationReason,
                ReleaseHoldRecomputedAimUsed = source.ReleaseHoldRecomputedAimUsed,
                ReleaseHoldRecordedAimUsed = source.ReleaseHoldRecordedAimUsed,
                PersistentCursorActive = source.PersistentCursorActive,
                PersistentCursorReason = "specialProjectileWeapon",
                PersistentHook = PersistentCursorHooks.ProjectileAI,
                PersistentCursorFrameCached = true,
                SpecialProjectileTailActive = source.SpecialProjectileTailActive,
                SpecialProjectileTailRecomputedAim = source.SpecialProjectileTailRecomputedAim,
                SpecialProjectileTailExpiredReason = source.SpecialProjectileTailExpiredReason,
                ContinuousUseWeaponAllowed = source.ContinuousUseWeaponAllowed,
                YoyoProjectileWhoAmI = source.YoyoProjectileWhoAmI,
                YoyoProjectileType = source.YoyoProjectileType,
                YoyoProjectileAiStyle = source.YoyoProjectileAiStyle,
                YoyoDetected = source.YoyoDetected,
                AttackQualified = source.AttackQualified,
                AttackDisqualifiedReason = source.AttackDisqualifiedReason,
                SkipReason = source.SkipReason,
                ResultCode = source.ResultCode,
                SelectedSlot = source.SelectedSlot,
                ItemType = source.ItemType,
                ItemStack = source.ItemStack,
                ItemName = source.ItemName,
                Damage = source.Damage,
                Shoot = source.Shoot,
                UseAmmo = source.UseAmmo,
                Melee = source.Melee,
                CreateTile = source.CreateTile,
                CreateWall = source.CreateWall,
                Pick = source.Pick,
                Axe = source.Axe,
                Hammer = source.Hammer,
                FishingPole = source.FishingPole,
                WeaponProfile = source.WeaponProfile,
                AimWorldX = source.AimWorldX,
                AimWorldY = source.AimWorldY,
                AimScreenX = source.AimScreenX,
                AimScreenY = source.AimScreenY,
                BallisticSolution = source.BallisticSolution,
                Selection = source.Selection,
                UiContext = source.UiContext
            };
        }

        private static CombatAimItemCheckDecision CloneFlailReleaseTailDecision(CombatAimItemCheckDecision source)
        {
            var copy = CloneSpecialProjectileTailDecision(source);
            if (copy == null)
            {
                return null;
            }

            copy.UseItemHeld = false;
            copy.UseItemReleased = true;
            copy.WasUseItemHeldLastTick = true;
            copy.ReleasedThisTick = true;
            copy.ReleaseDetected = true;
            copy.ItemAnimation = 0;
            copy.ItemTime = 0;
            copy.AimApplyMode = CombatAimApplyModes.PersistentCursor;
            copy.PersistentCursorActive = true;
            copy.PersistentCursorReason = "flailAiStyle15Release";
            copy.PersistentHook = PersistentCursorHooks.ProjectileAI;
            copy.PersistentCursorFrameCached = true;
            copy.ReleaseHoldValidationReason = "flailProjectileAiScoped";
            return copy;
        }

        private static bool TryMatchProjectileAiScoped(
            object projectile,
            object player,
            string hook,
            CombatAimPersistentCursorEligibility eligibility,
            bool yoyoProjectileMatch,
            string yoyoProjectileReason,
            int yoyoProjectileWhoAmI,
            int yoyoProjectileType,
            int yoyoProjectileAiStyle,
            CombatAimItemCheckDecision decision,
            out CombatAimProjectileCursorMatch projectileMatch)
        {
            projectileMatch = CombatAimProjectileCursorMatch.NotEvaluated();
            if (!CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(hook, eligibility))
            {
                return false;
            }

            lock (SyncRoot)
            {
                // A scoped cursor tail is exclusive for the current hook frame.
                if (_projectileAiScopedActive)
                {
                    return false;
                }
            }

            if (string.Equals(eligibility.Class, "yoyo", StringComparison.Ordinal))
            {
                if (!yoyoProjectileMatch)
                {
                    return false;
                }

                projectileMatch = CombatAimProjectileCursorMatch.Result(
                    true,
                    string.IsNullOrWhiteSpace(yoyoProjectileReason) ? "matched:yoyo" : yoyoProjectileReason,
                    yoyoProjectileWhoAmI,
                    yoyoProjectileType,
                    -1,
                    yoyoProjectileAiStyle);
                return true;
            }

            if (string.Equals(eligibility.Class, "specialProjectileWeapon", StringComparison.Ordinal))
            {
                if (!string.Equals(hook, PersistentCursorHooks.ProjectileAI, StringComparison.OrdinalIgnoreCase))
                {
                    projectileMatch = CombatAimProjectileCursorMatch.Result(false, "notEligible:requiresProjectileAIHook", -1, 0, -1, 0);
                    return false;
                }

                projectileMatch = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                    projectile,
                    player,
                    decision == null ? null : decision.WeaponProfile,
                    decision == null ? null : decision.BallisticSolution);
                return projectileMatch.Matches;
            }

            if (!string.Equals(eligibility.Class, "channelProjectileWeapon", StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(hook, PersistentCursorHooks.ProjectileAI, StringComparison.OrdinalIgnoreCase))
            {
                projectileMatch = CombatAimProjectileCursorMatch.Result(false, "notEligible:requiresProjectileAIHook", -1, 0, -1, 0);
                return false;
            }

            projectileMatch = CombatAimProjectileCursorCompat.MatchChannelProjectile(
                projectile,
                player,
                decision == null ? null : decision.WeaponProfile,
                decision == null ? null : decision.BallisticSolution);
            return projectileMatch.Matches;
        }

        private static string ResolvePersistentCursorReason(CombatAimPersistentCursorEligibility eligibility)
        {
            if (eligibility == null)
            {
                return string.Empty;
            }

            if (string.Equals(eligibility.Class, "channelProjectileWeapon", StringComparison.Ordinal))
            {
                return "channelProjectileWeapon";
            }

            if (string.Equals(eligibility.Class, "specialProjectileWeapon", StringComparison.Ordinal))
            {
                return "specialProjectileWeapon";
            }

            return eligibility.Reason;
        }

        private static bool TryGetFrameDecision(
            object player,
            AppSettings settings,
            CombatAimUseInputSnapshot input,
            int projectileWhoAmI,
            int projectileType,
            int projectileAiStyle,
            string hook,
            out CombatAimItemCheckDecision decision,
            out bool cacheHit)
        {
            decision = null;
            cacheHit = false;
            var key = BuildFrameCacheKey(settings, input);
            lock (SyncRoot)
            {
                if (_frameCache != null &&
                    _frameCache.GameUpdateCount == input.GameUpdateCount &&
                    string.Equals(_frameCache.Key, key, StringComparison.Ordinal) &&
                    _frameCache.Decision != null)
                {
                    decision = _frameCache.Decision;
                    cacheHit = true;
                    return true;
                }
            }

            if (!CombatAimItemCheckService.TryCreateAimDecision(player, CombatAimApplyModes.PersistentCursor, out decision))
            {
                return false;
            }

            decision.PersistentHook = hook;
            decision.YoyoProjectileWhoAmI = projectileWhoAmI;
            decision.YoyoProjectileType = projectileType;
            decision.YoyoProjectileAiStyle = projectileAiStyle;
            lock (SyncRoot)
            {
                _frameCache = new FrameCacheEntry
                {
                    GameUpdateCount = input.GameUpdateCount,
                    Key = key,
                    Decision = decision
                };
            }

            return true;
        }

        private static string BuildFrameCacheKey(AppSettings settings, CombatAimUseInputSnapshot input)
        {
            return (input == null ? 0 : input.GameUpdateCount).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                   (input == null ? 0 : input.ItemType).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                   (input == null ? -1 : input.SelectedSlot).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                   CombatAimModes.NormalizeRangeOrigin(settings == null ? string.Empty : settings.AimRangeOrigin) + ":" +
                   CombatAimModes.NormalizeTargetPriority(settings == null ? string.Empty : settings.AimTargetPriority);
        }

        private static bool ShouldUseMainUpdateFallback()
        {
            bool allowYoyoMainUpdateFallback;
            return ShouldUseMainUpdateFallback(out allowYoyoMainUpdateFallback);
        }

        private static bool ShouldUseMainUpdateFallback(out bool allowYoyoMainUpdateFallback)
        {
            allowYoyoMainUpdateFallback = false;
            var hook = PersistentHook;
            var hookIsFallback =
                string.Equals(hook, PersistentCursorHooks.MainUpdateFallback, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hook, PersistentCursorHooks.None, StringComparison.OrdinalIgnoreCase);
            allowYoyoMainUpdateFallback = hookIsFallback;

            CombatAimPersistentCursorEligibility eligibility;
            if (!TryReadFrameEligibility(out eligibility))
            {
                return false;
            }

            if (hookIsFallback)
            {
                return CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(hook, eligibility);
            }

            return eligibility.AllowsMainUpdateFallbackWithProjectileHook;
        }

        private static bool TryReadFrameEligibility(out CombatAimPersistentCursorEligibility eligibility)
        {
            eligibility = null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (!settings.PersistentCursorAimEnabled || Clamp(settings.CursorAimRadius, 0, 50) <= 0)
            {
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return false;
            }

            CombatAimUseInputSnapshot input;
            if (!TerrariaInputCompat.TryReadCombatAimUseInputSnapshot(player, out input) || input == null || !input.UseItemHeld)
            {
                return false;
            }

            var ui = TerrariaInputCompat.ReadUiInputContext(player);
            if (ui.MainTypeUnavailable || ui.GameMenu || ui.MouseCapturedByUi)
            {
                return false;
            }

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

            var profile = CombatAimWeaponProfile.Read(player, item);
            eligibility = CombatAimPersistentCursorPolicy.Evaluate(player, profile);
            return eligibility != null && eligibility.Eligible;
        }

        private static void EndFrameLocked()
        {
            if (_mainUpdateActive == null)
            {
                return;
            }

            var active = _mainUpdateActive;
            _mainUpdateActive = null;
            EndOverride(active);
        }

        private static void EndOverride(ActiveOverride active)
        {
            if (active == null)
            {
                return;
            }

            if (active.ProjectileAiScoped)
            {
                lock (SyncRoot)
                {
                    _projectileAiScopedActive = false;
                }
            }

            // Restore mouse target before recording success; scoped cursor
            // application is not complete until cleanup is verified.
            var restored = TerrariaInputCompat.TryRestoreMouseTargetState(active.RestoreState);
            CombatAimItemCheckService.RecordItemCheckAim(
                active.Decision,
                restored ? "Applied" : "AttemptedButUnverified",
                restored ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.AttemptedButUnverified,
                BuildRestoreMessage(active, restored),
                true,
                restored);
        }

        private static string BuildRestoreMessage(ActiveOverride active, bool restored)
        {
            if (!restored)
            {
                return "Combat persistent cursor aim applied, but mouse state restore was not fully verified: " + TerrariaInputCompat.LastInputCompatError;
            }

            if (active != null && active.ProjectileAiScoped)
            {
                return string.Equals(active.ScopeHook, PersistentCursorHooks.ProjectileKill, StringComparison.OrdinalIgnoreCase)
                    ? "Combat persistent cursor aim targeted a Projectile.Kill scoped cursor point and restored mouse state."
                    : "Combat persistent cursor aim targeted a Projectile.AI scoped cursor point and restored mouse state.";
            }

            return "Combat persistent cursor aim targeted a yoyo cursor point and restored mouse state.";
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public sealed class ActiveOverride
        {
            public CombatAimItemCheckDecision Decision;
            public MouseTargetInputState RestoreState;
            public bool ProjectileAiScoped;
            public string ScopeHook;
        }

        private sealed class FrameCacheEntry
        {
            public long GameUpdateCount;
            public string Key;
            public CombatAimItemCheckDecision Decision;
        }

        private sealed class SpecialProjectileTailEntry
        {
            public CombatAimItemCheckDecision Decision;
            public long ExpiresAtTick;
            public DateTime ExpiresAtUtc;
            public bool BoundToActiveProjectile;
            public int BoundProjectileWhoAmI;
            public int BoundProjectileType;
            public int BoundProjectileOwner;
            public long BoundAtTick;
        }

        private sealed class FlailReleaseTailEntry
        {
            public CombatAimItemCheckDecision Decision;
            public long ExpiresAtTick;
            public DateTime ExpiresAtUtc;
        }
    }

    public static class PersistentCursorHooks
    {
        public const string ProjectileAI = "ProjectileAI";
        public const string ProjectileKill = "ProjectileKill";
        public const string AI099 = "AI099";
        public const string MainUpdateFallback = "MainUpdateFallback";
        public const string None = "None";
    }
}
