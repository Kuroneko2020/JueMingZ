using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAimItemCheckService
    {
        private static readonly object LogSync = new object();
        private static readonly TimeSpan LogInterval = TimeSpan.FromSeconds(5);
        private const int MaxLogThrottleKeys = 128;
        private const long SelectionCacheTtlTicks = 0;
        private static readonly Dictionary<string, DateTime> LastLogUtcByKey = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private static readonly object SelectionCacheSync = new object();
        private static string _cachedSelectionKey = string.Empty;
        private static long _cachedSelectionTick = long.MinValue;
        private static CombatAimTargetSelection _cachedSelection;

        public static bool TryCreateAimDecision(object player, out CombatAimItemCheckDecision decision)
        {
            return TryCreateAimDecision(player, CombatAimApplyModes.InstantItemCheck, out decision);
        }

        public static bool TryCreateAimDecision(object player, string requestedApplyMode, out CombatAimItemCheckDecision decision)
        {
            return TryCreateAimDecision(player, requestedApplyMode, false, out decision);
        }

        public static bool TryCreateAimDecision(object player, bool allowItemUseBridgePending, out CombatAimItemCheckDecision decision)
        {
            return TryCreateAimDecision(player, CombatAimApplyModes.InstantItemCheck, allowItemUseBridgePending, out decision);
        }

        public static bool TryCreateAimDecision(object player, string requestedApplyMode, bool allowItemUseBridgePending, out CombatAimItemCheckDecision decision)
        {
            return TryCreateAimDecisionCore(player, requestedApplyMode, allowItemUseBridgePending, false, out decision);
        }

        internal static bool TryCreatePersistentCursorTailAimDecision(object player, out CombatAimItemCheckDecision decision)
        {
            return TryCreateAimDecisionCore(player, CombatAimApplyModes.PersistentCursor, false, true, out decision);
        }

        private static bool TryCreateAimDecisionCore(
            object player,
            string requestedApplyMode,
            bool allowItemUseBridgePending,
            bool allowPersistentCursorWithoutHeld,
            out CombatAimItemCheckDecision decision)
        {
            decision = new CombatAimItemCheckDecision();

            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                decision.AimApplyMode = string.IsNullOrWhiteSpace(requestedApplyMode) ? CombatAimApplyModes.InstantItemCheck : requestedApplyMode;
                decision.AimRangeOrigin = CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin);
                decision.AimTargetPriority = CombatAimModes.NormalizeTargetPriority(settings.AimTargetPriority);
                decision.CursorAimRadius = Clamp(settings.CursorAimRadius, 0, 50);
                decision.PlayerAimRadius = Clamp(settings.PlayerAimRadius, 0, 50);
                decision.RadiusTiles = decision.CursorAimRadius;
                decision.ActiveRangeMode = string.Equals(decision.AimRangeOrigin, CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase)
                    ? CombatAimRangeResolver.RangeModePlayerScreen
                    : CombatAimRangeResolver.RangeModeCursorSlider;
                decision.Enabled = decision.RadiusTiles > 0;
                decision.TrackDummy = settings.CombatAimTrackDummyEnabled;
                decision.MarkerEnabled = settings.CombatAimMarkerEnabled;
                decision.BridgePending = ItemUseBridge.PendingRequestId != Guid.Empty;

                if (!decision.Enabled)
                {
                    return Skip(decision, "radiusOff", DiagnosticResultCode.NotApplicable, false);
                }

                if (decision.BridgePending && !allowItemUseBridgePending)
                {
                    return Skip(decision, "itemUseBridgePending", DiagnosticResultCode.BlockedByEnvironment, true);
                }

                if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
                {
                    return Skip(decision, "notLocalPlayer", DiagnosticResultCode.BlockedByEnvironment, false);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    return Skip(decision, "runtimeTypesUnavailable:" + TerrariaRuntimeTypes.LastError, DiagnosticResultCode.BlockedByEnvironment, false);
                }

                decision.UiContext = TerrariaInputCompat.ReadUiInputContext(player);
                if (decision.UiContext.MainTypeUnavailable)
                {
                    return Skip(decision, "mainTypeUnavailable", DiagnosticResultCode.BlockedByEnvironment, true);
                }

                if (decision.UiContext.GameMenu)
                {
                    return Skip(decision, "gameMenu", DiagnosticResultCode.BlockedByEnvironment, true);
                }

                string playerReason;
                if (IsPlayerUnavailable(player, out playerReason))
                {
                    return Skip(decision, playerReason, DiagnosticResultCode.BlockedByEnvironment, true);
                }

                if (decision.UiContext.MouseCapturedByUi)
                {
                    return Skip(decision, decision.UiContext.MouseCaptureReason, DiagnosticResultCode.BlockedByUi, true);
                }

                CombatAimUseInputSnapshot inputState;
                if (!TerrariaInputCompat.TryReadCombatAimUseInputSnapshot(player, out inputState))
                {
                    return Skip(decision, "useItemStateUnavailable", DiagnosticResultCode.BlockedByEnvironment, true);
                }

                decision.UseItemHeld = inputState.UseItemHeld;
                decision.UseItemReleased = inputState.UseItemReleased;
                decision.ItemAnimation = inputState.ItemAnimation;
                decision.ItemTime = inputState.ItemTime;
                decision.GameUpdateCount = inputState.GameUpdateCount;
                CombatAimReleaseHoldService.DecorateDecision(decision, inputState);

                string itemReason;
                if (!TryReadSelectedItem(player, decision, out itemReason))
                {
                    return Skip(decision, itemReason, DiagnosticResultCode.MissingRequiredItem, true);
                }

                if (!IsEligibleCombatItem(decision, out itemReason))
                {
                    return Skip(decision, itemReason, DiagnosticResultCode.NotApplicable, true);
                }

                if (string.Equals(decision.AimApplyMode, CombatAimApplyModes.PersistentCursor, StringComparison.OrdinalIgnoreCase))
                {
                    decision.PersistentHook = CombatAimPersistentCursorService.PersistentHook;
                    if (!settings.PersistentCursorAimEnabled)
                    {
                        return Skip(decision, "persistentCursorDisabled", DiagnosticResultCode.NotApplicable, false);
                    }

                    var eligibility = CombatAimPersistentCursorPolicy.Evaluate(player, decision.WeaponProfile);
                    decision.YoyoDetected = eligibility.YoyoDetected;
                    decision.PersistentCursorReason = eligibility.Reason;
                    if (!eligibility.Eligible)
                    {
                        return Skip(decision, eligibility.Reason, DiagnosticResultCode.NotApplicable, eligibility.YoyoDetected || eligibility.VisibleCursorHijackRisk);
                    }

                    if (!decision.UseItemHeld)
                    {
                        if (allowPersistentCursorWithoutHeld)
                        {
                            decision.ContinuousUseWeaponAllowed = true;
                        }
                        else if (eligibility.AllowsAnimationScopedWithoutHeld && HasActiveUseAnimation(inputState))
                        {
                            decision.ContinuousUseWeaponAllowed = true;
                            decision.UseItemHeld = true;
                        }
                        else
                        {
                            return Skip(decision, "notUsingItem", DiagnosticResultCode.NotApplicable, false);
                        }
                    }
                }
                else if (!decision.UseItemHeld)
                {
                    if (CanUseContinuousAnimationFallback(decision.WeaponProfile, inputState))
                    {
                        decision.ContinuousUseWeaponAllowed = true;
                        decision.UseItemHeld = true;
                    }
                    else if (!TryApplyReleaseHoldDecision(player, decision, settings, inputState, out itemReason))
                    {
                        var releaseSkipReason = string.IsNullOrWhiteSpace(itemReason) ? "notUsingItem" : itemReason;
                        var logReleaseSkip = decision.ReleaseDetected ||
                                             decision.ReleaseHoldPending ||
                                             decision.WasUseItemHeldLastTick;
                        return Skip(decision, releaseSkipReason, DiagnosticResultCode.NotApplicable, logReleaseSkip);
                    }
                    else
                    {
                        int releaseAimScreenX;
                        int releaseAimScreenY;
                        if (!TerrariaInputCompat.TryWorldToScreen(decision.AimWorldX, decision.AimWorldY, out releaseAimScreenX, out releaseAimScreenY))
                        {
                            return Skip(decision, "worldToScreenFailed:" + TerrariaInputCompat.LastInputCompatError, DiagnosticResultCode.BlockedByEnvironment, true);
                        }

                        decision.AimScreenX = releaseAimScreenX;
                        decision.AimScreenY = releaseAimScreenY;
                        decision.ResultCode = DiagnosticResultCode.Succeeded.ToString();
                        decision.SkipReason = string.Empty;
                        decision.AttackQualified = true;
                        CombatAimTargetLockService.MarkAttackQualified(decision.Target);
                        return true;
                    }
                }


                var readResult = CombatAimTargetReader.Read(decision.TrackDummy);
                CombatAimTargetHistoryService.Enrich(readResult == null ? null : readResult.Candidates);
                object localPlayer = player;
                float playerCenterX;
                float playerCenterY;
                var hasPlayerCenter = CombatAimPlayerContext.TryReadPlayerCenter(player, out playerCenterX, out playerCenterY);
                var range = CombatAimRangeResolver.Resolve(settings, readResult, hasPlayerCenter, playerCenterX, playerCenterY);
                decision.ActiveRangeMode = range.RangeMode;
                decision.RadiusTiles = range.RadiusTiles;
                decision.PlayerScreenMarginTiles = range.PlayerScreenMarginTiles;
                decision.PlayerScreenRadiusTiles = range.PlayerScreenRadiusTiles;
                if (!range.Enabled)
                {
                    return Skip(decision, string.IsNullOrWhiteSpace(range.DisabledReason) ? "radiusOff" : range.DisabledReason, DiagnosticResultCode.NotApplicable, false);
                }

                var markerSelection = CombatAutoAimService.CurrentSelection;
                var ballisticContext = CombatAimBallisticSolver.Prepare(player, decision.WeaponProfile);
                var selectionCacheKey = BuildSelectionCacheKey(decision, range, inputState);
                CombatAimTargetSelection selection;
                if (!TryGetCachedSelection(selectionCacheKey, inputState.GameUpdateCount, out selection))
                {
                    selection = CombatAimTargetSelector.Select(
                        readResult,
                        range.RadiusTiles,
                        decision.TrackDummy,
                        decision.MarkerEnabled,
                        new CombatAimTargetSelectionContext
                        {
                            AimRangeOrigin = decision.AimRangeOrigin,
                            AimTargetPriority = decision.AimTargetPriority,
                            CursorAimRadius = decision.CursorAimRadius,
                            PlayerAimRadius = decision.PlayerAimRadius,
                            HasPlayerCenter = hasPlayerCenter,
                            PlayerCenterX = playerCenterX,
                            PlayerCenterY = playerCenterY,
                            Player = localPlayer,
                            WeaponProfile = decision.WeaponProfile,
                            IncludeBallisticScoring = true,
                            HasResolvedRange = true,
                            Range = range,
                            SelectionPurpose = string.Equals(decision.AimApplyMode, CombatAimApplyModes.PersistentCursor, StringComparison.OrdinalIgnoreCase) ? "PersistentCursor" : "Attack",
                            PreferredTargetWhoAmI = markerSelection == null || markerSelection.Target == null ? -1 : markerSelection.Target.WhoAmI,
                            PreferredTargetType = markerSelection == null || markerSelection.Target == null ? 0 : markerSelection.Target.Type,
                            BallisticContext = ballisticContext,
                            SelectionCacheKey = selectionCacheKey
                        });
                    StoreCachedSelection(selectionCacheKey, inputState.GameUpdateCount, selection);
                }
                decision.Selection = selection;
                decision.RangeCenterWorldX = selection == null ? 0f : selection.RangeCenterWorldX;
                decision.RangeCenterWorldY = selection == null ? 0f : selection.RangeCenterWorldY;
                if (selection == null || selection.Target == null)
                {
                    var reason = selection == null
                        ? "selectionUnavailable"
                        : "targetUnavailable:" + (selection.ResultCode ?? string.Empty) + ":" + (selection.SkipReason ?? string.Empty);
                    return Skip(decision, reason, DiagnosticResultCode.NotApplicable, true);
                }

                var ballistic = selection.BallisticSolution ?? CombatAimBallisticSolver.Solve(ballisticContext, selection.BallisticTarget ?? selection.Target);
                decision.BallisticSolution = ballistic;
                decision.AimWorldX = ballistic == null ? selection.SelectedSampleWorldX : ballistic.AimWorldX;
                decision.AimWorldY = ballistic == null ? selection.SelectedSampleWorldY : ballistic.AimWorldY;

                int aimScreenX;
                int aimScreenY;
                if (!TerrariaInputCompat.TryWorldToScreen(decision.AimWorldX, decision.AimWorldY, out aimScreenX, out aimScreenY))
                {
                    return Skip(decision, "worldToScreenFailed:" + TerrariaInputCompat.LastInputCompatError, DiagnosticResultCode.BlockedByEnvironment, true);
                }

                decision.AimScreenX = aimScreenX;
                decision.AimScreenY = aimScreenY;
                decision.ResultCode = DiagnosticResultCode.Succeeded.ToString();
                decision.SkipReason = string.Empty;
                decision.AttackQualified = true;
                if (string.Equals(decision.AimApplyMode, CombatAimApplyModes.InstantItemCheck, StringComparison.OrdinalIgnoreCase) &&
                    IsLikelyReleaseHoldWeapon(decision.WeaponProfile))
                {
                    CombatAimReleaseHoldService.Record(decision, settings.ReleaseHoldTicks);
                }

                CombatAimTargetLockService.MarkAttackQualified(decision.Target);
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("CombatAimItemCheckService.TryCreateAimDecision", error);
                decision.SkipReason = "decisionFailed:" + error.Message;
                decision.ResultCode = DiagnosticResultCode.Failed.ToString();
                LogThrottle.ErrorThrottled(
                    "combat-aim-itemcheck-decision-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAimItemCheckService",
                    "Combat aim ItemCheck decision failed; exception swallowed.", error);
                RecordItemCheckAim(decision, "Skipped", DiagnosticResultCode.Failed, "Combat ItemCheck aim decision failed: " + error.Message, false, false);
                return false;
            }
        }

        public static void RecordItemCheckAim(
            CombatAimItemCheckDecision decision,
            string result,
            DiagnosticResultCode resultCode,
            string message,
            bool mouseOverrideApplied,
            bool restored)
        {
            if (decision == null)
            {
                return;
            }

            var key = BuildLogThrottleKey(decision, result, resultCode, mouseOverrideApplied, restored);

            lock (LogSync)
            {
                var now = DateTime.UtcNow;
                if (!ShouldRecordLogLocked(key, now))
                {
                    return;
                }
            }

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "CombatAim.ItemCheckAim",
                "Aim",
                string.Empty,
                string.IsNullOrWhiteSpace(result) ? "Observed" : result,
                resultCode.ToString(),
                message ?? string.Empty,
                0,
                "{}",
                "{}",
                BuildDecisionJson(decision, mouseOverrideApplied, restored),
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        internal static void ResetLogThrottleForTesting()
        {
            lock (LogSync)
            {
                LastLogUtcByKey.Clear();
            }
        }

        internal static bool ShouldRecordLogForTesting(string key, DateTime now)
        {
            lock (LogSync)
            {
                return ShouldRecordLogLocked(key, now);
            }
        }

        private static string BuildLogThrottleKey(
            CombatAimItemCheckDecision decision,
            string result,
            DiagnosticResultCode resultCode,
            bool mouseOverrideApplied,
            bool restored)
        {
            return (result ?? string.Empty) + "|" +
                   resultCode + "|" +
                   (decision == null ? string.Empty : decision.AimApplyMode ?? string.Empty) + "|" +
                   (decision == null ? string.Empty : decision.SkipReason ?? string.Empty) + "|" +
                   (decision == null ? 0 : decision.RadiusTiles).ToString(CultureInfo.InvariantCulture) + "|" +
                   (decision == null ? 0 : decision.ItemType).ToString(CultureInfo.InvariantCulture) + "|" +
                   (decision == null || decision.Target == null ? -1 : decision.Target.WhoAmI).ToString(CultureInfo.InvariantCulture) + "|" +
                   mouseOverrideApplied + "|" +
                   restored;
        }

        private static bool ShouldRecordLogLocked(string key, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                key = "unknown";
            }

            DateTime last;
            if (LastLogUtcByKey.TryGetValue(key, out last) && now - last < LogInterval)
            {
                return false;
            }

            LastLogUtcByKey[key] = now;
            PruneLogThrottleKeysLocked(now);
            return true;
        }

        private static void PruneLogThrottleKeysLocked(DateTime now)
        {
            if (LastLogUtcByKey.Count <= MaxLogThrottleKeys)
            {
                return;
            }

            var expired = new List<string>();
            foreach (var pair in LastLogUtcByKey)
            {
                if (now - pair.Value >= TimeSpan.FromSeconds(30))
                {
                    expired.Add(pair.Key);
                }
            }

            for (var index = 0; index < expired.Count; index++)
            {
                LastLogUtcByKey.Remove(expired[index]);
            }

            if (LastLogUtcByKey.Count <= MaxLogThrottleKeys)
            {
                return;
            }

            var removeCount = LastLogUtcByKey.Count - MaxLogThrottleKeys;
            var keys = new List<string>(LastLogUtcByKey.Keys);
            for (var index = 0; index < keys.Count && removeCount > 0; index++, removeCount--)
            {
                LastLogUtcByKey.Remove(keys[index]);
            }
        }

        private static bool Skip(
            CombatAimItemCheckDecision decision,
            string reason,
            DiagnosticResultCode resultCode,
            bool logWhenSkipped)
        {
            var rawReason = reason ?? string.Empty;
            decision.SkipReason = NormalizeSkipReason(decision, rawReason);
            decision.ResultCode = resultCode.ToString();
            decision.AttackQualified = false;
            decision.AttackDisqualifiedReason = rawReason;
            if (decision.Selection != null)
            {
                decision.Selection.LockedTargetId = CombatAimTargetLockService.LockedTargetId;
                decision.Selection.LockedTargetType = CombatAimTargetLockService.LockedTargetType;
                decision.Selection.LockedTargetStillValid = decision.Target != null && CombatAimTargetLockService.IsLockedTarget(decision.Target.WhoAmI, decision.Target.Type);
                decision.Selection.TargetLockAgeTicks = CombatAimTargetLockService.LockAgeTicks;
                decision.Selection.TargetHoldTicksRemaining = CombatAimTargetLockService.HoldTicksRemaining;
            }

            CombatAimTargetLockService.MarkAttackDisqualified();
            if (logWhenSkipped)
            {
                RecordItemCheckAim(
                    decision,
                    "Skipped",
                    resultCode,
                    "Combat ItemCheck aim skipped: " + decision.SkipReason,
                    false,
                    false);
            }

            return false;
        }

        private static bool IsPlayerUnavailable(object player, out string reason)
        {
            reason = string.Empty;
            bool active;
            bool dead;
            bool ghost;
            TryGetBool(player, "active", out active);
            TryGetBool(player, "dead", out dead);
            TryGetBool(player, "ghost", out ghost);

            if (!active)
            {
                reason = "playerInactive";
                return true;
            }

            if (dead)
            {
                reason = "playerDead";
                return true;
            }

            if (ghost)
            {
                reason = "playerGhost";
                return true;
            }

            return false;
        }

        private static bool TryReadSelectedItem(object player, CombatAimItemCheckDecision decision, out string reason)
        {
            reason = string.Empty;
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                reason = "selectedSlotUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            decision.SelectedSlot = selectedSlot;
            if (selectedSlot < 0)
            {
                reason = "selectedSlotInvalid";
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
                reason = "selectedItemUnavailable";
                return false;
            }

            var profile = CombatAimWeaponProfile.Read(player, item);
            decision.WeaponProfile = profile;
            decision.ItemType = profile.ItemType;
            decision.ItemStack = profile.Stack;
            decision.ItemName = profile.Name;
            decision.Damage = profile.Damage;
            decision.Shoot = profile.Shoot;
            decision.UseAmmo = profile.UseAmmo;
            decision.Melee = profile.Melee;
            decision.CreateTile = profile.CreateTile;
            decision.CreateWall = profile.CreateWall;
            decision.Pick = profile.Pick;
            decision.Axe = profile.Axe;
            decision.Hammer = profile.Hammer;
            decision.FishingPole = profile.FishingPole;
            return true;
        }

        private static bool IsEligibleCombatItem(CombatAimItemCheckDecision decision, out string reason)
        {
            reason = string.Empty;
            var profile = decision.WeaponProfile;
            if (profile == null || profile.IsEmpty)
            {
                reason = "selectedItemEmpty";
                return false;
            }

            if (profile.IsCoinGun && !profile.CoinAmmoAvailable)
            {
                reason = "coinGunNoCoinAmmo";
                return false;
            }

            if (!profile.IsCoinGun && profile.Damage <= 0)
            {
                reason = "damageNotPositive";
                return false;
            }

            if (profile.IsPlacementItem)
            {
                reason = "placementItem";
                return false;
            }

            if (profile.IsToolOrFishingItem)
            {
                reason = "toolOrFishingItem";
                return false;
            }

            if (profile.IsAmmoItem)
            {
                reason = "ammoItem";
                return false;
            }

            if (profile.IsSentryPlacementWeapon)
            {
                reason = "sentryPlacementWeapon";
                return false;
            }

            if (profile.IsSummonPlacementWeapon)
            {
                reason = "summonPlacementWeapon";
                return false;
            }

            if (!profile.HasWeaponUseSemantics && !profile.IsCoinGun)
            {
                reason = "notProjectileAmmoOrMelee";
                return false;
            }

            return true;
        }

        private static bool TryApplyReleaseHoldDecision(
            object player,
            CombatAimItemCheckDecision decision,
            AppSettings settings,
            CombatAimUseInputSnapshot inputState,
            out string reason)
        {
            reason = string.Empty;
            if (settings == null || settings.ReleaseHoldTicks <= 0)
            {
                reason = "notUsingItem";
                return false;
            }

            if (!IsLikelyReleaseHoldWeapon(decision.WeaponProfile))
            {
                reason = "notReleaseHoldWeapon";
                return false;
            }

            var readResult = CombatAimTargetReader.Read(decision.TrackDummy);
            CombatAimTargetHistoryService.Enrich(readResult == null ? null : readResult.Candidates);
            float playerCenterX;
            float playerCenterY;
            var hasPlayerCenter = CombatAimPlayerContext.TryReadPlayerCenter(player, out playerCenterX, out playerCenterY);
            var range = CombatAimRangeResolver.Resolve(settings, readResult, hasPlayerCenter, playerCenterX, playerCenterY);
            decision.ActiveRangeMode = range.RangeMode;
            decision.RadiusTiles = range.RadiusTiles;
            decision.PlayerScreenMarginTiles = range.PlayerScreenMarginTiles;
            decision.PlayerScreenRadiusTiles = range.PlayerScreenRadiusTiles;
            decision.RangeCenterWorldX = range.RangeCenterWorldX;
            decision.RangeCenterWorldY = range.RangeCenterWorldY;

            if (!CombatAimReleaseHoldService.TryApply(player, decision, readResult, inputState, range, settings, out reason))
            {
                return false;
            }

            decision.Selection.RangeCenterWorldX = decision.RangeCenterWorldX;
            decision.Selection.RangeCenterWorldY = decision.RangeCenterWorldY;
            decision.Selection.AimRangeOrigin = decision.AimRangeOrigin;
            decision.Selection.AimTargetPriority = decision.AimTargetPriority;
            return true;
        }

        private static bool IsLikelyReleaseHoldWeapon(CombatAimWeaponProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            if (profile.IsPlacementItem || profile.IsToolOrFishingItem || profile.IsAmmoItem || profile.IsSentryPlacementWeapon || profile.IsSummonPlacementWeapon)
            {
                return false;
            }

            if (profile.ShootsOnUseRelease)
            {
                return true;
            }

            if (profile.Channel)
            {
                return true;
            }

            if (profile.ReuseDelay > 0 && (profile.Ranged || profile.Magic || profile.Shoot > 0))
            {
                return true;
            }

            if (profile.UseAnimation > profile.UseTime + 6 && (profile.Ranged || profile.Magic || profile.Shoot > 0))
            {
                return true;
            }

            return profile.NoUseGraphic && profile.Shoot > 0 && (profile.Ranged || profile.Magic || profile.Thrown);
        }

        private static bool CanUseContinuousAnimationFallback(CombatAimWeaponProfile profile, CombatAimUseInputSnapshot inputState)
        {
            if (profile == null || inputState == null)
            {
                return false;
            }

            if (IsLikelyReleaseHoldWeapon(profile) || profile.Channel || profile.ShootsOnUseRelease)
            {
                return false;
            }

            if (inputState.ItemAnimation <= 0 && inputState.ItemTime <= 0)
            {
                return false;
            }

            if (profile.IsPlacementItem || profile.IsToolOrFishingItem || profile.IsAmmoItem || profile.IsSentryPlacementWeapon || profile.IsSummonPlacementWeapon)
            {
                return false;
            }

            return profile.IsCoinGun || profile.Ranged || profile.Magic || profile.Thrown || profile.Shoot > 0 || profile.UseAmmo > 0;
        }

        private static bool HasActiveUseAnimation(CombatAimUseInputSnapshot inputState)
        {
            return inputState != null && (inputState.ItemAnimation > 0 || inputState.ItemTime > 0);
        }

        private static bool TryGetCachedSelection(string key, long tick, out CombatAimTargetSelection selection)
        {
            selection = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            lock (SelectionCacheSync)
            {
                if (_cachedSelection == null || !string.Equals(_cachedSelectionKey, key, StringComparison.Ordinal))
                {
                    return false;
                }

                var age = tick - _cachedSelectionTick;
                if (age < 0 || age > SelectionCacheTtlTicks)
                {
                    return false;
                }

                selection = _cachedSelection;
                selection.SelectionCacheHit = true;
                selection.SelectionCacheKey = key;
                return true;
            }
        }

        private static void StoreCachedSelection(string key, long tick, CombatAimTargetSelection selection)
        {
            if (string.IsNullOrWhiteSpace(key) || selection == null || !selection.HasTarget)
            {
                return;
            }

            lock (SelectionCacheSync)
            {
                _cachedSelectionKey = key;
                _cachedSelectionTick = tick;
                _cachedSelection = selection;
            }
        }

        private static string BuildSelectionCacheKey(CombatAimItemCheckDecision decision, CombatAimRangeResolveResult range, CombatAimUseInputSnapshot input)
        {
            return (decision == null ? string.Empty : decision.AimApplyMode ?? string.Empty) + ":" +
                   (decision == null ? 0 : decision.ItemType).ToString(CultureInfo.InvariantCulture) + ":" +
                   (decision == null ? -1 : decision.SelectedSlot).ToString(CultureInfo.InvariantCulture) + ":" +
                   (decision == null ? string.Empty : decision.AimRangeOrigin ?? string.Empty) + ":" +
                   (decision == null ? string.Empty : decision.AimTargetPriority ?? string.Empty) + ":" +
                   (range == null ? string.Empty : range.RangeMode ?? string.Empty) + ":" +
                   (range == null ? 0 : range.RadiusTiles).ToString(CultureInfo.InvariantCulture) + ":" +
                   Quantize(range == null ? 0f : range.RangeCenterWorldX, 16f).ToString(CultureInfo.InvariantCulture) + "," +
                   Quantize(range == null ? 0f : range.RangeCenterWorldY, 16f).ToString(CultureInfo.InvariantCulture) + ":" +
                   (decision == null ? 0 : decision.CursorAimRadius).ToString(CultureInfo.InvariantCulture) + ":" +
                   (decision == null ? 0 : decision.PlayerAimRadius).ToString(CultureInfo.InvariantCulture) + ":" +
                   (decision != null && decision.MarkerEnabled ? "marker" : "nomarker");
        }

        private static int Quantize(float value, float step)
        {
            return step <= 0f ? (int)Math.Round(value) : (int)Math.Round(value / step);
        }

        private static string BuildDecisionJson(
            CombatAimItemCheckDecision decision,
            bool mouseOverrideApplied,
            bool restored)
        {
            var persistentEligibility = CombatAimPersistentCursorPolicy.Evaluate(
                decision == null ? null : decision.WeaponProfile,
                decision != null && string.Equals(decision.PersistentCursorReason, "yoyo", StringComparison.Ordinal),
                decision != null && decision.YoyoDetected,
                decision == null ? string.Empty : decision.PersistentCursorReason);
            var projectileCursorMetadata = CombatAimProjectileCursorCompat.GetDecisionMetadata(decision);
            var hookForPolicy = decision == null || string.IsNullOrWhiteSpace(decision.PersistentHook)
                ? CombatAimPersistentCursorService.PersistentHook
                : decision.PersistentHook;
            var flailDiagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(decision);
            var weaponFamily = CombatAimWeaponFamilyResolver.Resolve(
                decision == null ? null : decision.WeaponProfile,
                decision == null ? null : decision.BallisticSolution,
                decision != null && decision.YoyoDetected,
                decision == null ? string.Empty : decision.PersistentCursorReason);
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "enabled", BoolRaw(decision.Enabled), true);
            AppendRaw(builder, "radiusTiles", IntRaw(decision.RadiusTiles), true);
            AppendRaw(builder, "aimRangeRadius", IntRaw(decision.RadiusTiles), true);
            AppendString(builder, "aimRangeOrigin", decision.AimRangeOrigin, true);
            AppendString(builder, "activeRangeMode", decision.ActiveRangeMode, true);
            AppendString(builder, "aimTargetPriority", decision.AimTargetPriority, true);
            AppendString(builder, "targetPriority", decision.AimTargetPriority, true);
            AppendRaw(builder, "cursorAimRadius", IntRaw(decision.CursorAimRadius), true);
            AppendRaw(builder, "playerAimRadius", IntRaw(decision.PlayerAimRadius), true);
            AppendRaw(builder, "playerScreenRadiusTiles", IntRaw(decision.PlayerScreenRadiusTiles), true);
            AppendRaw(builder, "playerScreenMarginTiles", IntRaw(decision.PlayerScreenMarginTiles), true);
            AppendRaw(builder, "rangeCenterWorld", BuildPointJson(decision.RangeCenterWorldX, decision.RangeCenterWorldY), true);
            AppendRaw(builder, "trackDummy", BoolRaw(decision.TrackDummy), true);
            AppendRaw(builder, "markerEnabled", BoolRaw(decision.MarkerEnabled), true);
            AppendRaw(builder, "bridgePending", BoolRaw(decision.BridgePending), true);
            AppendString(builder, "resultCode", decision.ResultCode, true);
            AppendRaw(builder, "useItemHeld", BoolRaw(decision.UseItemHeld), true);
            AppendRaw(builder, "useItemReleased", BoolRaw(decision.UseItemReleased), true);
            AppendRaw(builder, "wasUseItemHeldLastTick", BoolRaw(decision.WasUseItemHeldLastTick), true);
            AppendRaw(builder, "releasedThisTick", BoolRaw(decision.ReleasedThisTick), true);
            AppendRaw(builder, "releaseDetected", BoolRaw(decision.ReleaseDetected), true);
            AppendRaw(builder, "itemAnimation", IntRaw(decision.ItemAnimation), true);
            AppendRaw(builder, "itemTime", IntRaw(decision.ItemTime), true);
            AppendString(builder, "aimApplyMode", decision.AimApplyMode, true);
            AppendString(builder, "applyPolicy", decision.AimApplyMode, true);
            AppendString(builder, "aimPurpose", ResolveAimPurpose(decision), true);
            AppendString(builder, "weaponFamily", weaponFamily.Family, true);
            AppendString(builder, "weaponFamilyReason", weaponFamily.Reason, true);
            AppendString(builder, "releaseHoldState", decision.ReleaseHoldState, true);
            AppendRaw(builder, "releaseHoldArmed", BoolRaw(decision.ReleaseHoldArmed), true);
            AppendRaw(builder, "releaseHoldPending", BoolRaw(decision.ReleaseHoldPending), true);
            AppendRaw(builder, "releaseHoldConsumed", BoolRaw(decision.ReleaseHoldConsumed), true);
            AppendRaw(builder, "releaseHoldActive", BoolRaw(decision.ReleaseHoldActive), true);
            AppendRaw(builder, "releaseHoldTicksRemaining", IntRaw(decision.ReleaseHoldTicksRemaining), true);
            AppendRaw(builder, "releaseHoldApplyCount", IntRaw(decision.ReleaseHoldApplyCount), true);
            AppendString(builder, "releaseHoldValidationMode", decision.ReleaseHoldValidationMode, true);
            AppendString(builder, "releaseHoldValidationReason", decision.ReleaseHoldValidationReason, true);
            AppendRaw(builder, "releaseHoldRecomputedAimUsed", BoolRaw(decision.ReleaseHoldRecomputedAimUsed), true);
            AppendRaw(builder, "releaseHoldRecordedAimUsed", BoolRaw(decision.ReleaseHoldRecordedAimUsed), true);
            AppendRaw(builder, "persistentCursorActive", BoolRaw(decision.PersistentCursorActive), true);
            AppendString(builder, "persistentHook", decision.PersistentHook, true);
            AppendString(builder, "persistentCursorReason", decision.PersistentCursorReason, true);
            AppendRaw(builder, "persistentCursorEligibility", BoolRaw(persistentEligibility != null && persistentEligibility.Eligible), true);
            AppendString(builder, "persistentCursorEligibilityReason", persistentEligibility == null ? string.Empty : persistentEligibility.Reason, true);
            AppendString(builder, "persistentCursorClass", persistentEligibility == null ? string.Empty : persistentEligibility.Class, true);
            AppendRaw(builder, "persistentCursorMainUpdateFallbackAllowed", BoolRaw(CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(hookForPolicy, persistentEligibility)), true);
            AppendRaw(builder, "persistentCursorProjectileAiScopedAllowed", BoolRaw(projectileCursorMetadata.ProjectileAiScopedAllowed || CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(hookForPolicy, persistentEligibility)), true);
            AppendRaw(builder, "persistentCursorScopedOverride", BoolRaw(projectileCursorMetadata.ScopedOverride), true);
            AppendRaw(builder, "persistentCursorFrameCached", BoolRaw(decision.PersistentCursorFrameCached), true);
            AppendRaw(builder, "visibleCursorHijackRisk", BoolRaw(persistentEligibility != null && persistentEligibility.VisibleCursorHijackRisk), true);
            AppendRaw(builder, "visibleCursorHijackRiskMitigated", BoolRaw(projectileCursorMetadata.VisibleCursorHijackRiskMitigated || (persistentEligibility != null && persistentEligibility.VisibleCursorHijackRiskMitigated)), true);
            AppendRaw(builder, "projectileCursorMatch", BoolRaw(projectileCursorMetadata.ProjectileMatch), true);
            AppendString(builder, "projectileCursorMatchReason", projectileCursorMetadata.ProjectileMatchReason, true);
            AppendRaw(builder, "projectileCursorProjectileType", IntRaw(projectileCursorMetadata.ProjectileType), true);
            AppendRaw(builder, "projectileCursorOwner", IntRaw(projectileCursorMetadata.ProjectileOwner), true);
            AppendRaw(builder, "specialProjectileTailActive", BoolRaw(decision.SpecialProjectileTailActive), true);
            AppendRaw(builder, "specialProjectileTailRecomputedAim", BoolRaw(decision.SpecialProjectileTailRecomputedAim), true);
            AppendString(builder, "specialProjectileTailExpiredReason", decision.SpecialProjectileTailExpiredReason, true);
            AppendRaw(builder, "userCursorWorldAvailable", BoolRaw(decision.Selection != null || decision.RangeCenterWorldX != 0f || decision.RangeCenterWorldY != 0f), true);
            AppendRaw(builder, "userCursorWorld", BuildPointJson(decision.RangeCenterWorldX, decision.RangeCenterWorldY), true);
            AppendRaw(builder, "simulatedAimWorld", BuildPointJson(decision.AimWorldX, decision.AimWorldY), true);
            AppendString(builder, "cursorOwnershipMode", persistentEligibility == null ? string.Empty : persistentEligibility.CursorOwnershipMode, true);
            AppendRaw(builder, "releaseHoldTargetDummyAllowed", BoolRaw(IsReleaseHoldTargetDummyAllowed(decision)), true);
            AppendRaw(builder, "flailControlEligible", BoolRaw(flailDiagnostics != null && flailDiagnostics.Eligible), true);
            AppendString(builder, "flailControlReason", flailDiagnostics == null ? "notEvaluated" : flailDiagnostics.Reason, true);
            AppendRaw(builder, "flailControlActive", BoolRaw(flailDiagnostics != null && flailDiagnostics.Active), true);
            AppendString(builder, "flailControlState", flailDiagnostics == null ? FlailControlStates.Idle : flailDiagnostics.State, true);
            AppendString(builder, "flailInputMode", flailDiagnostics == null ? "observe" : flailDiagnostics.InputMode, true);
            AppendString(builder, "flailInputPhase", flailDiagnostics == null ? string.Empty : flailDiagnostics.InputPhase, true);
            AppendString(builder, "flailTakeoverScope", flailDiagnostics == null ? "none" : flailDiagnostics.TakeoverScope, true);
            AppendRaw(builder, "flailPhysicalUseItemHeld", BoolRaw(flailDiagnostics != null && flailDiagnostics.PhysicalUseItemHeld), true);
            AppendRaw(builder, "flailPhysicalReleasePending", BoolRaw(flailDiagnostics != null && flailDiagnostics.PhysicalReleasePending), true);
            AppendRaw(builder, "flailProjectileWhoAmI", flailDiagnostics == null ? "-1" : IntRaw(flailDiagnostics.ProjectileWhoAmI), true);
            AppendRaw(builder, "flailProjectileType", flailDiagnostics == null ? "0" : IntRaw(flailDiagnostics.ProjectileType), true);
            AppendRaw(builder, "flailProjectileAiStyle", flailDiagnostics == null ? "0" : IntRaw(flailDiagnostics.ProjectileAiStyle), true);
            AppendRaw(builder, "flailProjectileAi0", flailDiagnostics == null ? "0" : FloatRaw(flailDiagnostics.ProjectileAi0), true);
            AppendRaw(builder, "flailProjectileVelocity", flailDiagnostics == null ? BuildPointJson(0f, 0f) : BuildPointJson(flailDiagnostics.ProjectileVelocityX, flailDiagnostics.ProjectileVelocityY), true);
            AppendRaw(builder, "flailProjectileIdentity", flailDiagnostics == null ? "-1" : IntRaw(flailDiagnostics.ProjectileIdentity), true);
            AppendRaw(builder, "flailHitDetected", BoolRaw(flailDiagnostics != null && flailDiagnostics.HitDetected), true);
            AppendRaw(builder, "flailCollisionDetected", BoolRaw(flailDiagnostics != null && flailDiagnostics.CollisionDetected), true);
            AppendRaw(builder, "flailLocalNpcImmunityChanged", BoolRaw(flailDiagnostics != null && flailDiagnostics.LocalNpcImmunityChanged), true);
            AppendRaw(builder, "flailTileCollisionDetected", BoolRaw(flailDiagnostics != null && flailDiagnostics.TileCollisionDetected), true);
            AppendRaw(builder, "flailAttackPulse", BoolRaw(flailDiagnostics != null && flailDiagnostics.AttackPulse), true);
            AppendRaw(builder, "flailAttackRelease", BoolRaw(flailDiagnostics != null && flailDiagnostics.AttackRelease), true);
            AppendRaw(builder, "flailAttackSuppressed", BoolRaw(flailDiagnostics != null && flailDiagnostics.AttackSuppressed), true);
            AppendRaw(builder, "flailAttackRestored", BoolRaw(flailDiagnostics != null && flailDiagnostics.AttackRestored), true);
            AppendString(builder, "flailPulseReason", flailDiagnostics == null ? string.Empty : flailDiagnostics.PulseReason, true);
            AppendString(builder, "flailStuckRecovery", flailDiagnostics == null ? "none" : flailDiagnostics.StuckRecovery, true);
            AppendRaw(builder, "flailCachedReleaseAim", BoolRaw(flailDiagnostics != null && flailDiagnostics.CachedReleaseAim), true);
            AppendRaw(builder, "flailCachedReleaseAimAgeTicks", flailDiagnostics == null ? "-1" : IntRaw(flailDiagnostics.CachedReleaseAimAgeTicks), true);
            AppendString(builder, "flailCachedReleaseAimReason", flailDiagnostics == null ? string.Empty : flailDiagnostics.CachedReleaseAimReason, true);
            AppendRaw(builder, "flailReleaseSuppressedPhysicalInput", BoolRaw(flailDiagnostics != null && flailDiagnostics.ReleaseSuppressedPhysicalInput), true);
            AppendString(builder, "flailControlBlockedReason", flailDiagnostics == null ? string.Empty : flailDiagnostics.BlockedReason, true);
            AppendRaw(builder, "continuousUseWeaponAllowed", BoolRaw(decision.ContinuousUseWeaponAllowed), true);
            AppendRaw(builder, "yoyoProjectileWhoAmI", decision.YoyoProjectileWhoAmI >= 0 ? IntRaw(decision.YoyoProjectileWhoAmI) : "null", true);
            AppendRaw(builder, "yoyoProjectileType", IntRaw(decision.YoyoProjectileType), true);
            AppendRaw(builder, "yoyoProjectileAiStyle", IntRaw(decision.YoyoProjectileAiStyle), true);
            AppendRaw(builder, "yoyoDetected", BoolRaw(decision.YoyoDetected), true);
            AppendRaw(builder, "attackQualified", BoolRaw(decision.AttackQualified), true);
            AppendString(builder, "attackDisqualifiedReason", decision.AttackDisqualifiedReason, true);
            AppendRaw(builder, "gameMenu", BoolRaw(decision.UiContext != null && decision.UiContext.GameMenu), true);
            AppendRaw(builder, "chatOpen", BoolRaw(decision.UiContext != null && decision.UiContext.ChatOpen), true);
            AppendRaw(builder, "npcChatOpen", BoolRaw(decision.UiContext != null && decision.UiContext.NpcChatOpen), true);
            AppendRaw(builder, "playerInventoryOpen", BoolRaw(decision.UiContext != null && decision.UiContext.PlayerInventoryOpen), true);
            AppendRaw(builder, "chestOpen", BoolRaw(decision.UiContext != null && decision.UiContext.ChestOpen), true);
            AppendRaw(builder, "playerMouseInterface", BoolRaw(decision.UiContext != null && decision.UiContext.PlayerMouseInterface), true);
            AppendRaw(builder, "mainMouseInterface", BoolRaw(decision.UiContext != null && decision.UiContext.MainMouseInterface), true);
            AppendRaw(builder, "mainBlockMouse", BoolRaw(decision.UiContext != null && decision.UiContext.MainBlockMouse), true);
            AppendString(builder, "uiCaptureReason", decision.UiContext == null ? string.Empty : decision.UiContext.MouseCaptureReason, true);
            AppendString(builder, "skipReason", decision.SkipReason, true);
            AppendString(builder, "skipDetail", decision.AttackDisqualifiedReason, true);
            AppendRaw(builder, "selectedSlot", decision.SelectedSlot >= 0 ? IntRaw(decision.SelectedSlot) : "null", true);
            AppendRaw(builder, "selectedSlotDisplay", decision.SelectedSlot >= 0 ? IntRaw(decision.SelectedSlot + 1) : "null", true);
            AppendRaw(builder, "itemType", IntRaw(decision.ItemType), true);
            AppendRaw(builder, "itemStack", IntRaw(decision.ItemStack), true);
            AppendString(builder, "itemName", UnknownIfEmpty(decision.ItemName), true);
            AppendRaw(builder, "damage", IntRaw(decision.Damage), true);
            AppendRaw(builder, "shoot", IntRaw(decision.Shoot), true);
            AppendRaw(builder, "useAmmo", IntRaw(decision.UseAmmo), true);
            AppendRaw(builder, "melee", BoolRaw(decision.Melee), true);
            AppendRaw(builder, "createTile", IntRaw(decision.CreateTile), true);
            AppendRaw(builder, "createWall", IntRaw(decision.CreateWall), true);
            AppendRaw(builder, "pick", IntRaw(decision.Pick), true);
            AppendRaw(builder, "axe", IntRaw(decision.Axe), true);
            AppendRaw(builder, "hammer", IntRaw(decision.Hammer), true);
            AppendRaw(builder, "fishingPole", IntRaw(decision.FishingPole), true);
            AppendWeaponProfileJson(builder, decision.WeaponProfile);
            AppendRaw(builder, "targetWhoAmI", decision.Target == null ? "null" : IntRaw(decision.Target.WhoAmI), true);
            AppendRaw(builder, "targetType", decision.Target == null ? "null" : IntRaw(decision.Target.Type), true);
            AppendString(builder, "targetName", decision.Target == null ? "unknown" : UnknownIfEmpty(decision.Target.Name), true);
            AppendRaw(builder, "targetCenter", decision.Target == null ? "null" : BuildPointJson(decision.Target.CenterX, decision.Target.CenterY), true);
            AppendRaw(builder, "selectedSampleWorld", decision.Selection == null ? "null" : BuildPointJson(decision.Selection.SelectedSampleWorldX, decision.Selection.SelectedSampleWorldY), true);
            AppendString(builder, "selectedSamplePoint", decision.Selection == null ? string.Empty : decision.Selection.SelectedSamplePoint, true);
            AppendString(builder, "attackSamplePoint", decision.Selection == null ? string.Empty : decision.Selection.AttackSamplePoint, true);
            AppendString(builder, "selectionSamplePoint", decision.Selection == null ? string.Empty : decision.Selection.SelectionSamplePoint, true);
            AppendRaw(builder, "lineOfSightRejectedSampleCount", IntRaw(decision.Selection == null ? 0 : decision.Selection.LineOfSightRejectedSampleCount), true);
            AppendRaw(builder, "nearestHitboxPointPenaltyApplied", BoolRaw(decision.Selection != null && decision.Selection.NearestHitboxPointPenaltyApplied), true);
            AppendRaw(builder, "centerPreferred", BoolRaw(decision.Selection != null && decision.Selection.CenterPreferred), true);
            AppendRaw(builder, "targetScore", FloatRaw(decision.Selection == null ? 0f : decision.Selection.TargetScore), true);
            AppendRaw(builder, "lineClear", BoolRaw(decision.Selection != null && decision.Selection.LineClear), true);
            AppendRaw(builder, "lineClearAvailable", BoolRaw(decision.Selection != null && decision.Selection.LineClearAvailable), true);
            AppendString(builder, "lineOfSightResult", ResolveLineOfSightResult(decision.Selection), true);
            AppendRaw(builder, "losCacheHit", BoolRaw(decision.Selection != null && decision.Selection.LosCacheHit), true);
            AppendRaw(builder, "distanceToPlayerCursorRay", FloatRaw(decision.Selection == null ? 0f : decision.Selection.DistanceToPlayerCursorRay), true);
            AppendRaw(builder, "inForwardCone", BoolRaw(decision.Selection != null && decision.Selection.InForwardCone), true);
            AppendRaw(builder, "previousTargetBonus", FloatRaw(decision.Selection == null ? 0f : decision.Selection.PreviousTargetBonus), true);
            AppendString(builder, "selectedReason", decision.Selection == null ? string.Empty : decision.Selection.SelectedReason, true);
            AppendBallisticJson(builder, decision.BallisticSolution);
            AppendRaw(builder, "aimWorld", BuildPointJson(decision.AimWorldX, decision.AimWorldY), true);
            AppendRaw(builder, "aimScreen", BuildIntPointJson(decision.AimScreenX, decision.AimScreenY), true);
            AppendRaw(builder, "centerDistanceTiles", FloatRaw(decision.Selection == null ? 0f : decision.Selection.CenterDistanceTiles), true);
            AppendRaw(builder, "distanceToTarget", FloatRaw(decision.Selection == null ? 0f : decision.Selection.CenterDistanceTiles), true);
            AppendRaw(builder, "hitboxDistanceTiles", FloatRaw(decision.Selection == null ? 0f : decision.Selection.HitboxDistanceTiles), true);
            AppendRaw(builder, "targetDistanceFromRangeCenter", FloatRaw(decision.Selection == null ? 0f : decision.Selection.TargetDistanceFromRangeCenterTiles), true);
            AppendRaw(builder, "lockedTargetId", decision.Selection == null || decision.Selection.LockedTargetId < 0 ? "null" : IntRaw(decision.Selection.LockedTargetId), true);
            AppendRaw(builder, "lockedTargetType", decision.Selection == null || decision.Selection.LockedTargetType <= 0 ? "null" : IntRaw(decision.Selection.LockedTargetType), true);
            AppendRaw(builder, "lockedTargetStillValid", BoolRaw(decision.Selection != null && decision.Selection.LockedTargetStillValid), true);
            AppendRaw(builder, "targetLockAgeTicks", IntRaw(decision.Selection == null ? 0 : decision.Selection.TargetLockAgeTicks), true);
            AppendRaw(builder, "targetHoldTicksRemaining", IntRaw(decision.Selection == null ? 0 : decision.Selection.TargetHoldTicksRemaining), true);
            AppendString(builder, "selectionPurpose", decision.Selection == null ? string.Empty : decision.Selection.SelectionPurpose, true);
            AppendRaw(builder, "selectionCacheHit", BoolRaw(decision.Selection != null && decision.Selection.SelectionCacheHit), true);
            AppendString(builder, "selectionCacheKey", decision.Selection == null ? string.Empty : decision.Selection.SelectionCacheKey, true);
            AppendRaw(builder, "markerTargetWhoAmI", decision.Selection == null || decision.Selection.MarkerTargetWhoAmI < 0 ? "null" : IntRaw(decision.Selection.MarkerTargetWhoAmI), true);
            AppendRaw(builder, "attackTargetWhoAmI", decision.Selection == null || decision.Selection.AttackTargetWhoAmI < 0 ? "null" : IntRaw(decision.Selection.AttackTargetWhoAmI), true);
            AppendRaw(builder, "markerAttackTargetMismatch", BoolRaw(decision.Selection != null && decision.Selection.MarkerAttackTargetMismatch), true);
            AppendRaw(builder, "markerTargetChangedForAttack", BoolRaw(decision.Selection != null && decision.Selection.MarkerTargetChangedForAttack), true);
            AppendRaw(builder, "candidateCount", IntRaw(decision.Selection == null ? 0 : decision.Selection.CandidateCount), true);
            AppendRaw(builder, "cheapCandidateCount", IntRaw(decision.Selection == null ? 0 : decision.Selection.CheapCandidateCount), true);
            AppendRaw(builder, "expensiveCandidateCount", IntRaw(decision.Selection == null ? 0 : decision.Selection.ExpensiveCandidateCount), true);
            AppendRaw(builder, "evaluatedCandidateCount", IntRaw(decision.Selection == null ? 0 : decision.Selection.EvaluatedCandidateCount), true);
            AppendRaw(builder, "inRangeCandidateCount", IntRaw(decision.Selection == null ? 0 : decision.Selection.InRangeCandidateCount), true);
            AppendRaw(builder, "itemCheckAimEntered", BoolRaw(true), true);
            AppendRaw(builder, "mouseStateCaptured", BoolRaw(mouseOverrideApplied || restored), true);
            AppendRaw(builder, "mouseOverrideApplied", BoolRaw(mouseOverrideApplied), true);
            AppendRaw(builder, "persistentCursorTargetSet", BoolRaw(decision.PersistentCursorActive && mouseOverrideApplied), true);
            AppendRaw(builder, "restored", BoolRaw(restored), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static void AppendWeaponProfileJson(StringBuilder builder, CombatAimWeaponProfile profile)
        {
            AppendString(builder, "weaponClassification", profile == null ? "unknown" : UnknownIfEmpty(profile.Classification), true);
            AppendRaw(builder, "shootSpeed", FloatRaw(profile == null ? 0f : profile.ShootSpeed), true);
            AppendRaw(builder, "itemAmmo", IntRaw(profile == null ? 0 : profile.Ammo), true);
            AppendRaw(builder, "useStyle", IntRaw(profile == null ? 0 : profile.UseStyle), true);
            AppendRaw(builder, "useTime", IntRaw(profile == null ? 0 : profile.UseTime), true);
            AppendRaw(builder, "useAnimation", IntRaw(profile == null ? 0 : profile.UseAnimation), true);
            AppendRaw(builder, "reuseDelay", IntRaw(profile == null ? 0 : profile.ReuseDelay), true);
            AppendRaw(builder, "mana", IntRaw(profile == null ? 0 : profile.Mana), true);
            AppendRaw(builder, "buffType", IntRaw(profile == null ? 0 : profile.BuffType), true);
            AppendRaw(builder, "ranged", BoolRaw(profile != null && profile.Ranged), true);
            AppendRaw(builder, "magic", BoolRaw(profile != null && profile.Magic), true);
            AppendRaw(builder, "summon", BoolRaw(profile != null && profile.Summon), true);
            AppendRaw(builder, "thrown", BoolRaw(profile != null && profile.Thrown), true);
            AppendRaw(builder, "sentry", BoolRaw(profile != null && profile.Sentry), true);
            AppendRaw(builder, "consumable", BoolRaw(profile != null && profile.Consumable), true);
            AppendRaw(builder, "channel", BoolRaw(profile != null && profile.Channel), true);
            AppendRaw(builder, "noMelee", BoolRaw(profile != null && profile.NoMelee), true);
            AppendRaw(builder, "noUseGraphic", BoolRaw(profile != null && profile.NoUseGraphic), true);
            AppendRaw(builder, "shootsOnUseRelease", BoolRaw(profile != null && profile.ShootsOnUseRelease), true);
            AppendString(builder, "damageTypeName", profile == null ? "unknown" : UnknownIfEmpty(profile.DamageTypeName), true);
            AppendString(builder, "damageType", profile == null ? "unknown" : UnknownIfEmpty(profile.DamageTypeName), true);
            AppendRaw(builder, "damageTypeSummonLike", BoolRaw(profile != null && profile.DamageTypeSummonLike), true);
            AppendRaw(builder, "ammoItem", BoolRaw(profile != null && profile.IsAmmoItem), true);
            AppendRaw(builder, "coinGun", BoolRaw(profile != null && profile.IsCoinGun), true);
            AppendRaw(builder, "coinGunType", IntRaw(profile == null ? 0 : profile.CoinGunType), true);
            AppendRaw(builder, "coinAmmoType", IntRaw(profile == null ? 0 : profile.CoinAmmoType), true);
            AppendRaw(builder, "coinAmmoAvailable", BoolRaw(profile != null && profile.CoinAmmoAvailable), true);
            AppendRaw(builder, "coinAmmoSlot", profile != null && profile.CoinAmmoSlot >= 0 ? IntRaw(profile.CoinAmmoSlot) : "null", true);
            AppendRaw(builder, "coinAmmoItemType", IntRaw(profile == null ? 0 : profile.CoinAmmoItemType), true);
            AppendRaw(builder, "coinAmmoStack", IntRaw(profile == null ? 0 : profile.CoinAmmoStack), true);
        }

        private static void AppendBallisticJson(StringBuilder builder, CombatAimBallisticSolution solution)
        {
            AppendRaw(builder, "ballisticSolved", BoolRaw(solution != null && solution.Solved), true);
            AppendString(builder, "ballisticMode", solution == null ? "unknown" : UnknownIfEmpty(solution.Mode), true);
            AppendString(builder, "ballisticFallbackReason", solution == null ? "unknown" : UnknownIfEmpty(solution.FallbackReason), true);
            AppendRaw(builder, "ballisticProjectileType", IntRaw(solution == null ? 0 : solution.ProjectileType), true);
            AppendRaw(builder, "resolvedProjectileType", IntRaw(solution == null ? 0 : solution.ProjectileType), true);
            AppendString(builder, "resolvedProjectileName", solution == null ? "unknown" : UnknownIfEmpty(solution.ProjectileName), true);
            AppendRaw(builder, "weaponShootProjectileType", IntRaw(solution == null ? 0 : solution.WeaponShootProjectileType), true);
            AppendString(builder, "weaponShootProjectileName", solution == null ? "unknown" : UnknownIfEmpty(solution.WeaponShootProjectileName), true);
            AppendRaw(builder, "ammoProjectileType", IntRaw(solution == null ? 0 : solution.AmmoProjectileType), true);
            AppendString(builder, "ammoProjectileName", solution == null ? "unknown" : UnknownIfEmpty(solution.AmmoProjectileName), true);
            AppendString(builder, "resolvedProjectileRole", solution == null ? string.Empty : solution.ResolvedProjectileRole, true);
            AppendRaw(builder, "primaryProjectileType", IntRaw(solution == null ? 0 : solution.PrimaryProjectileType), true);
            AppendString(builder, "primaryProjectileName", solution == null ? "unknown" : UnknownIfEmpty(solution.PrimaryProjectileName), true);
            AppendString(builder, "primaryProjectileRole", solution == null ? string.Empty : solution.PrimaryProjectileRole, true);
            AppendRaw(builder, "secondaryProjectileType", IntRaw(solution == null ? 0 : solution.SecondaryProjectileType), true);
            AppendString(builder, "secondaryProjectileName", solution == null ? "unknown" : UnknownIfEmpty(solution.SecondaryProjectileName), true);
            AppendString(builder, "secondaryProjectileRole", solution == null ? string.Empty : solution.SecondaryProjectileRole, true);
            AppendRaw(builder, "ballisticProjectileAiStyle", IntRaw(solution == null ? 0 : solution.ProjectileAiStyle), true);
            AppendRaw(builder, "projectileAiStyle", IntRaw(solution == null ? 0 : solution.ProjectileAiStyle), true);
            AppendRaw(builder, "ballisticProjectileExtraUpdates", IntRaw(solution == null ? 0 : solution.ProjectileExtraUpdates), true);
            AppendRaw(builder, "projectileExtraUpdates", IntRaw(solution == null ? 0 : solution.ProjectileExtraUpdates), true);
            AppendRaw(builder, "ballisticProjectileDefaultsAvailable", BoolRaw(solution != null && solution.ProjectileDefaultsAvailable), true);
            AppendString(builder, "projectileProfileStatus", solution != null && solution.ProjectileDefaultsAvailable ? "resolved" : "unknown", true);
            AppendRaw(builder, "ballisticProjectileNoGravity", BoolRaw(solution != null && solution.ProjectileNoGravity), true);
            AppendRaw(builder, "ballisticProjectileArrow", BoolRaw(solution != null && solution.ProjectileArrow), true);
            AppendRaw(builder, "projectileTileCollide", BoolRaw(solution != null && solution.ProjectileTileCollide), true);
            AppendRaw(builder, "projectileWidth", IntRaw(solution == null ? 0 : solution.ProjectileWidth), true);
            AppendRaw(builder, "projectileHeight", IntRaw(solution == null ? 0 : solution.ProjectileHeight), true);
            AppendRaw(builder, "projectileFriendly", BoolRaw(solution != null && solution.ProjectileFriendly), true);
            AppendRaw(builder, "projectileHostile", BoolRaw(solution != null && solution.ProjectileHostile), true);
            AppendRaw(builder, "ballisticAmmoAvailable", BoolRaw(solution != null && solution.AmmoAvailable), true);
            AppendRaw(builder, "ballisticAmmoType", IntRaw(solution == null ? 0 : solution.AmmoType), true);
            AppendRaw(builder, "ballisticAmmoItemType", IntRaw(solution == null ? 0 : solution.AmmoItemType), true);
            AppendRaw(builder, "ammoItemType", IntRaw(solution == null ? 0 : solution.AmmoItemType), true);
            AppendString(builder, "ammoItemName", solution == null ? "unknown" : UnknownIfEmpty(solution.AmmoItemName), true);
            AppendRaw(builder, "ballisticAmmoProjectileType", IntRaw(solution == null ? 0 : solution.AmmoProjectileType), true);
            AppendRaw(builder, "ammoShoot", IntRaw(solution == null ? 0 : solution.AmmoProjectileType), true);
            AppendRaw(builder, "ballisticAmmoSlot", solution != null && solution.AmmoSlot >= 0 ? IntRaw(solution.AmmoSlot) : "null", true);
            AppendRaw(builder, "ballisticAmmoShootSpeed", FloatRaw(solution == null ? 0f : solution.AmmoShootSpeed), true);
            AppendRaw(builder, "ammoShootSpeed", FloatRaw(solution == null ? 0f : solution.AmmoShootSpeed), true);
            AppendRaw(builder, "ballisticAmmoArrowLike", BoolRaw(solution != null && solution.AmmoArrowLike), true);
            AppendRaw(builder, "ballisticAmmoBulletLike", BoolRaw(solution != null && solution.AmmoBulletLike), true);
            AppendString(builder, "ballisticSpecialWeaponKind", solution == null ? string.Empty : solution.SpecialWeaponKind, true);
            AppendString(builder, "ballisticSpecialWeaponName", solution == null ? string.Empty : solution.SpecialWeaponName, true);
            AppendString(builder, "ballisticSpecialWeaponRule", solution == null ? string.Empty : solution.SpecialWeaponRule, true);
            AppendString(builder, "specialWeaponRuleKind", solution == null ? string.Empty : solution.SpecialWeaponKind, true);
            AppendString(builder, "specialWeaponRuleName", solution == null ? string.Empty : solution.SpecialWeaponName, true);
            AppendRaw(builder, "specialWeaponRuleApplied", BoolRaw(solution != null && !string.IsNullOrWhiteSpace(solution.SpecialWeaponKind)), true);
            AppendString(builder, "specialWeaponAimMode", solution == null ? string.Empty : solution.Mode, true);
            AppendRaw(builder, "specialWeaponAimPoint", solution == null ? "null" : BuildPointJson(solution.AimWorldX, solution.AimWorldY), true);
            AppendRaw(builder, "ballisticSpecialShotCount", IntRaw(solution == null ? 0 : solution.SpecialShotCount), true);
            AppendRaw(builder, "ballisticSpecialSpreadDegrees", FloatRaw(solution == null ? 0f : solution.SpecialSpreadDegrees), true);
            AppendRaw(builder, "ballisticSpecialParallelSpacingPixels", FloatRaw(solution == null ? 0f : solution.SpecialParallelSpacingPixels), true);
            AppendRaw(builder, "ballisticSpecialLeadTicks", FloatRaw(solution == null ? 0f : solution.SpecialLeadTicks), true);
            AppendRaw(builder, "ballisticSpecialCursorTarget", BoolRaw(solution != null && solution.SpecialCursorTarget), true);
            AppendRaw(builder, "specialWeaponUsesCursorTarget", BoolRaw(solution != null && solution.SpecialCursorTarget), true);
            AppendRaw(builder, "ballisticSpecialAimApplied", BoolRaw(solution != null && solution.SpecialAimApplied), true);
            AppendRaw(builder, "specialWeaponUsesWeaponShoot", BoolRaw(solution != null && solution.SpecialWeaponUsesWeaponShoot), true);
            AppendRaw(builder, "specialWeaponUsesAmmoShoot", BoolRaw(solution != null && solution.SpecialWeaponUsesAmmoShoot), true);
            AppendRaw(builder, "specialWeaponUsesWeaponProjectile", BoolRaw(solution != null && solution.SpecialWeaponUsesWeaponShoot), true);
            AppendRaw(builder, "specialWeaponUsesAmmoProjectile", BoolRaw(solution != null && solution.SpecialWeaponUsesAmmoShoot), true);
            AppendRaw(builder, "ballisticConservativeCenter", BoolRaw(solution != null && solution.ConservativeCenter), true);
            AppendRaw(builder, "ballisticAimAdjusted", BoolRaw(solution != null && solution.AimAdjusted), true);
            AppendRaw(builder, "ballisticPlayerCenter", solution == null ? "null" : BuildPointJson(solution.PlayerCenterX, solution.PlayerCenterY), true);
            AppendRaw(builder, "ballisticTargetVelocity", solution == null ? "null" : BuildPointJson(solution.TargetVelocityX, solution.TargetVelocityY), true);
            AppendRaw(builder, "ballisticPredictedTargetCenter", solution == null ? "null" : BuildPointJson(solution.PredictedTargetX, solution.PredictedTargetY), true);
            AppendRaw(builder, "ballisticProjectileSpeed", FloatRaw(solution == null ? 0f : solution.ProjectileSpeed), true);
            AppendRaw(builder, "resolvedProjectileSpeed", FloatRaw(solution == null ? 0f : solution.ProjectileSpeed), true);
            AppendRaw(builder, "ballisticEffectiveProjectileSpeed", FloatRaw(solution == null ? 0f : solution.EffectiveProjectileSpeed), true);
            AppendRaw(builder, "ballisticLeadTicks", FloatRaw(solution == null ? 0f : solution.LeadTicks), true);
            AppendRaw(builder, "ballisticGravityPerTick", FloatRaw(solution == null ? 0f : solution.GravityPerTick), true);
            AppendRaw(builder, "ballisticGravityCompensationPixels", FloatRaw(solution == null ? 0f : solution.GravityCompensationPixels), true);
        }

        private static string ResolveAimPurpose(CombatAimItemCheckDecision decision)
        {
            if (decision == null)
            {
                return "other";
            }

            if (string.Equals(decision.AimApplyMode, CombatAimApplyModes.PersistentCursor, StringComparison.OrdinalIgnoreCase))
            {
                return "persistentCursor";
            }

            if (string.Equals(decision.AimApplyMode, CombatAimApplyModes.InstantItemCheck, StringComparison.OrdinalIgnoreCase))
            {
                return "itemCheck";
            }

            return "other";
        }

        private static string ResolveLineOfSightResult(CombatAimTargetSelection selection)
        {
            if (selection == null || !selection.LineClearAvailable)
            {
                return "unknown";
            }

            return selection.LineClear ? "clear" : "blocked";
        }

        private static string NormalizeSkipReason(CombatAimItemCheckDecision decision, string reason)
        {
            var value = reason ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            {
                return "none";
            }

            if (string.Equals(value, "radiusOff", StringComparison.OrdinalIgnoreCase))
            {
                return "disabled";
            }

            if (string.Equals(value, "gameMenu", StringComparison.OrdinalIgnoreCase))
            {
                return "notInWorld";
            }

            if (string.Equals(value, "itemUseBridgePending", StringComparison.OrdinalIgnoreCase))
            {
                return "bridgeBusy";
            }

            if (string.Equals(value, "playerMouseInterface", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "mainMouseInterface", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "mainBlockMouse", StringComparison.OrdinalIgnoreCase))
            {
                return "mouseCaptured";
            }

            if (string.Equals(value, "notLocalPlayer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "playerInactive", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "playerDead", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "playerGhost", StringComparison.OrdinalIgnoreCase))
            {
                return "noPlayer";
            }

            if (string.Equals(value, "selectedSlotInvalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "selectedItemUnavailable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "selectedItemEmpty", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "inventoryUnavailable", StringComparison.OrdinalIgnoreCase) ||
                StartsWithOrdinalIgnoreCase(value, "selectedSlotUnavailable"))
            {
                return "noWeapon";
            }

            if (string.Equals(value, "placementItem", StringComparison.OrdinalIgnoreCase))
            {
                return "placementItem";
            }

            if (string.Equals(value, "toolOrFishingItem", StringComparison.OrdinalIgnoreCase))
            {
                return "toolOrFishingRod";
            }

            if (string.Equals(value, "sentryPlacementWeapon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "summonPlacementWeapon", StringComparison.OrdinalIgnoreCase))
            {
                return "sentryOrSummonPlacement";
            }

            if (string.Equals(value, "ammoItem", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "damageNotPositive", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "coinGunNoCoinAmmo", StringComparison.OrdinalIgnoreCase))
            {
                return "unsupportedWeapon";
            }

            if (string.Equals(value, "notProjectileAmmoOrMelee", StringComparison.OrdinalIgnoreCase))
            {
                return "noProjectile";
            }

            if (ContainsOrdinalIgnoreCase(value, "blockedByLineOfSight"))
            {
                return "lineOfSightBlocked";
            }

            if (StartsWithOrdinalIgnoreCase(value, "targetUnavailable") ||
                string.Equals(value, "selectionUnavailable", StringComparison.OrdinalIgnoreCase) ||
                ContainsOrdinalIgnoreCase(value, "noCandidates") ||
                ContainsOrdinalIgnoreCase(value, "outsideRadius") ||
                ContainsOrdinalIgnoreCase(value, "noScoredSample"))
            {
                return "noTarget";
            }

            if (string.Equals(value, "persistentCursorDisabled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "notYoyoWeapon", StringComparison.OrdinalIgnoreCase) ||
                StartsWithOrdinalIgnoreCase(value, "notEligible:") ||
                (decision != null &&
                 string.Equals(decision.AimApplyMode, CombatAimApplyModes.PersistentCursor, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(value, "notUsingItem", StringComparison.OrdinalIgnoreCase)))
            {
                return "persistentCursorNotEligible";
            }

            if (StartsWithOrdinalIgnoreCase(value, "releaseHoldTargetInvalid"))
            {
                return "noTarget";
            }

            if (string.Equals(value, "notUsingItem", StringComparison.OrdinalIgnoreCase))
            {
                return "unsupportedWeapon";
            }

            return "unknown";
        }

        private static bool IsReleaseHoldTargetDummyAllowed(CombatAimItemCheckDecision decision)
        {
            return decision != null &&
                   decision.TrackDummy &&
                   decision.Target != null &&
                   decision.Target.Active &&
                   decision.Target.IsTargetDummy;
        }

        private static string UnknownIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static bool StartsWithOrdinalIgnoreCase(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(prefix) &&
                   value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsOrdinalIgnoreCase(string value, string pattern)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(pattern) &&
                   value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryGetBool(object source, string name, out bool value)
        {
            return GameStateReflection.TryGetBool(source, name, out value);
        }

        private static bool TryGetInt(object source, string name, out int value)
        {
            return GameStateReflection.TryGetInt(source, name, out value);
        }

        private static int ReadInt(object source, string name, int fallback)
        {
            int value;
            return TryGetInt(source, name, out value) ? value : fallback;
        }

        private static bool ReadBool(object source, string name, bool fallback)
        {
            bool value;
            return TryGetBool(source, name, out value) ? value : fallback;
        }

        private static string ReadItemName(object item)
        {
            try
            {
                var name = GameStateReflection.GetMember(item, "Name") ??
                           GameStateReflection.GetMember(item, "name");
                return name == null ? string.Empty : Convert.ToString(name, CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static string BuildPointJson(float x, float y)
        {
            return "{" +
                   "\"x\":" + FloatRaw(x) + "," +
                   "\"y\":" + FloatRaw(y) +
                   "}";
        }

        private static string BuildIntPointJson(int x, int y)
        {
            return "{" +
                   "\"x\":" + IntRaw(x) + "," +
                   "\"y\":" + IntRaw(y) +
                   "}";
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
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":\"").Append(Escape(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
