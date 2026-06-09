using System;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Combat
{
    // Cached release decisions reuse a bounded release aim; do not run fresh target selection on this path.
    public static partial class CombatAimFlailControlService
    {
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
            PublishCachedReleaseDiagnostics(profile, eligibility, cached, tick);
            return true;
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
                selection = CreateFallbackCachedSelection(cached, settings);
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

            MarkCachedReleaseSelection(decision, cached);
            CombatAimTargetLockService.MarkAttackQualified(decision.Target);
            return decision;
        }

        private static CombatAimTargetSelection CreateFallbackCachedSelection(FlailCachedReleaseAim cached, AppSettings settings)
        {
            return new CombatAimTargetSelection
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
                SampleSpace = "cachedReleaseAim",
                SelectionPurpose = "FlailRelease"
            };
        }

        private static void MarkCachedReleaseSelection(CombatAimItemCheckDecision decision, FlailCachedReleaseAim cached)
        {
            decision.Selection.SelectionCacheHit = true;
            decision.Selection.SelectionCacheKey = "flailCachedReleaseAim";
            decision.Selection.ResultCode = string.IsNullOrWhiteSpace(decision.Selection.ResultCode)
                ? "CachedFlailReleaseTarget"
                : decision.Selection.ResultCode;
            decision.Selection.SelectionPurpose = "FlailRelease";
            decision.Selection.AttackTargetWhoAmI = decision.Selection.Target == null ? cached.TargetWhoAmI : decision.Selection.Target.WhoAmI;
            decision.Selection.AttackTargetType = decision.Selection.Target == null ? cached.TargetType : decision.Selection.Target.Type;
        }

        private static void PublishCachedReleaseDiagnostics(
            CombatAimWeaponProfile profile,
            CombatAimFlailEligibility eligibility,
            FlailCachedReleaseAim cached,
            long tick)
        {
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
        }
    }
}
