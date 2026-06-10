using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Ui;
using JueMingZ.Hooks;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void CombatAimTargetMotionProfileClassifiesStableLinear()
        {
            CombatAimTargetHistoryService.Clear();
            try
            {
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(TargetSnapshot(7, 100, 0f, 0f, 2f, 0f, 0, false, false)), 10);
                var current = TargetSnapshot(7, 100, 2f, 0f, 2f, 0f, 0, false, false);
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(current), 11);

                AssertMotionKind(current, CombatAimTargetMotionProfile.StableLinear);
                if (!current.SmoothedVelocityAvailable ||
                    current.MotionProfile == null ||
                    current.MotionProfile.VelocityConfidence < 0.9f ||
                    !current.MotionProfile.PreferSmoothedVelocity)
                {
                    throw new InvalidOperationException("Expected stable linear target to keep high smoothed velocity confidence.");
                }
            }
            finally
            {
                CombatAimTargetHistoryService.Clear();
            }
        }

        private static void CombatAimTargetMotionProfileResetsOnTeleport()
        {
            CombatAimTargetHistoryService.Clear();
            try
            {
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(TargetSnapshot(8, 101, 0f, 0f, 0f, 0f, 0, false, false)), 20);
                var current = TargetSnapshot(8, 101, 500f, 0f, 0f, 0f, 0, false, false);
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(current), 21);

                AssertMotionKind(current, CombatAimTargetMotionProfile.TeleportOrDashRecent);
                AssertStringEquals(current.MotionProfile.HistoryResetReason, "teleportDistance", "teleport reset reason");
                AssertNear(current.SmoothedVelocityX, 0d, "teleport smoothed velocity");
            }
            finally
            {
                CombatAimTargetHistoryService.Clear();
            }
        }

        private static void CombatAimTargetMotionProfileMarksAiStyleOneGrounded()
        {
            CombatAimTargetHistoryService.Clear();
            try
            {
                var current = TargetSnapshot(9, 102, 0f, 0f, 0.2f, 0f, 1, false, true);
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(current), 30);

                AssertMotionKind(current, CombatAimTargetMotionProfile.JumpingGrounded);
                if (current.MotionProfile.MotionConfidence >= 0.6f ||
                    current.MotionProfile.RecommendedMaxLeadTicks > 18.001f ||
                    !current.MotionProfile.PreferCurrentVelocity)
                {
                    throw new InvalidOperationException("Expected grounded aiStyle=1 target to use a conservative low-trust motion profile.");
                }
            }
            finally
            {
                CombatAimTargetHistoryService.Clear();
            }
        }

        private static void CombatAimTargetMotionProfileTickGapAvoidsHugeMeasuredVelocity()
        {
            CombatAimTargetHistoryService.Clear();
            try
            {
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(TargetSnapshot(10, 103, 0f, 0f, 1f, 0f, 0, false, false)), 40);
                var current = TargetSnapshot(10, 103, 400f, 0f, 1f, 0f, 0, false, false);
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(current), 75);

                AssertMotionKind(current, CombatAimTargetMotionProfile.TeleportOrDashRecent);
                AssertStringEquals(current.MotionProfile.HistoryResetReason, "staleTickGap", "tick gap reset reason");
                AssertNear(current.SmoothedVelocityX, 1d, "tick gap smoothed velocity");
            }
            finally
            {
                CombatAimTargetHistoryService.Clear();
            }
        }

        private static void CombatAimTargetMotionProfileClampsAcceleration()
        {
            CombatAimTargetHistoryService.Clear();
            try
            {
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(TargetSnapshot(11, 104, 0f, 0f, 0f, 0f, 0, false, false)), 80);
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(TargetSnapshot(11, 104, 2f, 0f, 2f, 0f, 0, false, false)), 81);
                var current = TargetSnapshot(11, 104, 14f, 0f, 12f, 0f, 0, false, false);
                CombatAimTargetHistoryService.UpdateFromRead(ReadResultWith(current), 82);

                if (current.MotionProfile == null ||
                    Math.Abs(current.MotionProfile.AccelerationX) > 3.001f ||
                    Math.Abs(current.MotionProfile.AccelerationY) > 3.001f)
                {
                    throw new InvalidOperationException("Expected target acceleration to be clamped to the bounded profile budget.");
                }
            }
            finally
            {
                CombatAimTargetHistoryService.Clear();
            }
        }

        private static void CombatAimBallisticSolverUsesPointSolverForBeams()
        {
            var solution = SolveCombatAimBallistic(
                new FakePlayer(),
                new FakeItem
                {
                    type = 10100,
                    stack = 1,
                    Name = "Beam Test Staff",
                    damage = 30,
                    shoot = 9004,
                    shootSpeed = 16f,
                    magic = true
                },
                null,
                BallisticTarget(260f, 21f, 3f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.95f, 1f, 45f, false));

            AssertStringEquals(solution.SolverKind, CombatAimBallisticSolverKinds.PointAim, "beam solver kind");
            AssertStringEquals(solution.LeadWindowKind, CombatAimLeadWindowKinds.PointShort, "beam lead window");
            AssertStringEquals(solution.LeadClampReason, CombatAimLeadClampReasons.FixedPointLead, "beam clamp reason");
            AssertStringEquals(solution.SpecialWeaponKind, "beamOrInstant", "beam special kind");
            AssertStringEquals(solution.SpecialWeaponLeadPolicy, "nearInstantShortLead", "beam lead policy");
            AssertNear(solution.LeadTicks, 2d, "beam fixed lead");
        }

        private static void CombatAimBallisticSolverUsesShortLeadForHoming()
        {
            var solution = SolveCombatAimBallistic(
                new FakePlayer(),
                new FakeItem
                {
                    type = 10105,
                    stack = 1,
                    Name = "Homing Test Staff",
                    damage = 30,
                    shoot = 9005,
                    shootSpeed = 9f,
                    magic = true
                },
                null,
                BallisticTarget(360f, 21f, 3f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.95f, 1f, 45f, false));

            AssertStringEquals(solution.SolverKind, CombatAimBallisticSolverKinds.PointAim, "homing solver kind");
            AssertStringEquals(solution.LeadWindowKind, CombatAimLeadWindowKinds.PointShort, "homing lead window");
            AssertStringEquals(solution.SpecialWeaponKind, "homingOrSelfCorrecting", "homing special kind");
            AssertStringEquals(solution.SpecialWeaponLeadPolicy, "homingShortLead", "homing lead policy");
            AssertNear(solution.LeadTicks, 5d, "homing fixed lead");
        }

        private static void CombatAimBallisticSolverKeepsHighSpeedLeadShort()
        {
            var solution = SolveCombatAimBallistic(
                new FakePlayer(),
                new FakeItem
                {
                    type = 10101,
                    stack = 1,
                    Name = "Fast Test Gun",
                    damage = 24,
                    shoot = Terraria.ID.ProjectileID.Bullet,
                    shootSpeed = 20f,
                    useAmmo = Terraria.ID.AmmoID.Bullet,
                    ranged = true
                },
                new FakeItem
                {
                    type = 97,
                    stack = 99,
                    Name = "Musket Ball",
                    ammo = Terraria.ID.AmmoID.Bullet,
                    shoot = Terraria.ID.ProjectileID.Bullet,
                    shootSpeed = 4f
                },
                BallisticTarget(1000f, 21f, 4f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.95f, 1f, 45f, false));

            AssertStringEquals(solution.SolverKind, CombatAimBallisticSolverKinds.LinearIntercept, "high speed solver kind");
            AssertStringEquals(solution.LeadWindowKind, CombatAimLeadWindowKinds.HighSpeedShort, "high speed lead window");
            if (solution.LeadTicks > 24.001f ||
                solution.LeadWindowMaxTicks > 24.001f ||
                !solution.LeadClamped)
            {
                throw new InvalidOperationException("Expected high speed projectile lead to stay inside the short window.");
            }
        }

        private static void CombatAimBallisticSolverAllowsTrustedSlowProjectileLead()
        {
            var solution = SolveCombatAimBallistic(
                new FakePlayer(),
                SlowMagicWeapon(),
                null,
                BallisticTarget(260f, 21f, 2f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.95f, 1f, 45f, false));

            AssertStringEquals(solution.SolverKind, CombatAimBallisticSolverKinds.SlowProjectile, "slow projectile solver kind");
            AssertStringEquals(solution.LeadWindowKind, CombatAimLeadWindowKinds.SlowLong, "slow projectile lead window");
            if (solution.LeadTicks <= 45.001f || solution.LeadWindowMaxTicks < 90f)
            {
                throw new InvalidOperationException("Expected trusted slow projectile to use a long lead window above the old 45 tick cap.");
            }
        }

        private static void CombatAimBallisticSolverClampsSlowProjectileLowTrustLead()
        {
            var solution = SolveCombatAimBallistic(
                new FakePlayer(),
                SlowMagicWeapon(),
                null,
                BallisticTarget(260f, 21f, 2f, 0f, CombatAimTargetMotionProfile.JumpingGrounded, 0.45f, 0.5f, 0.55f, 18f, true));

            AssertStringEquals(solution.SolverKind, CombatAimBallisticSolverKinds.SlowProjectile, "low trust slow solver kind");
            if (solution.LeadTicks > 18.001f ||
                solution.LeadWindowMaxTicks > 18.001f ||
                !string.Equals(solution.LeadClampReason, CombatAimLeadClampReasons.MotionRecommendedMaxLead, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected low-trust jumping target to clamp slow projectile prediction to the motion profile window.");
            }
        }

        private static void CombatAimBallisticSolverUsesGravityProfileInputs()
        {
            var player = new FakePlayer
            {
                magicQuiver = true
            };
            player.buffType[0] = Terraria.ID.BuffID.Archery;
            player.buffTime[0] = 60;

            var solution = SolveCombatAimBallistic(
                player,
                new FakeItem
                {
                    type = 10102,
                    stack = 1,
                    Name = "Gravity Test Bow",
                    damage = 12,
                    shoot = Terraria.ID.ProjectileID.WoodenArrowFriendly,
                    shootSpeed = 16f,
                    useAmmo = Terraria.ID.AmmoID.Arrow,
                    ranged = true
                },
                new FakeItem
                {
                    type = 40,
                    stack = 99,
                    Name = "Wooden Arrow",
                    ammo = Terraria.ID.AmmoID.Arrow,
                    shoot = Terraria.ID.ProjectileID.WoodenArrowFriendly,
                    shootSpeed = 0.5f
                },
                BallisticTarget(500f, 21f, 1.5f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.95f, 1f, 45f, false));

            AssertStringEquals(solution.SolverKind, CombatAimBallisticSolverKinds.GravityArc, "gravity solver kind");
            AssertStringEquals(solution.LeadWindowKind, CombatAimLeadWindowKinds.GravityArc, "gravity lead window");
            AssertNear(solution.EffectiveProjectileSpeed, 40d, "gravity solver effective speed");
            AssertNear(solution.GravityPerTick, 0.2d, "gravity solver effective gravity");
            AssertNear(solution.GravityDelayTicks, 10d, "gravity delay");
        }

        private static void CombatAimBallisticSolverClassifiesReturningOutbound()
        {
            var solution = SolveCombatAimBallistic(
                new FakePlayer(),
                new FakeItem
                {
                    type = 10103,
                    stack = 1,
                    Name = "Returning Test Weapon",
                    damage = 18,
                    shoot = 9003,
                    shootSpeed = 10f,
                    ranged = true
                },
                null,
                BallisticTarget(520f, 21f, 2.5f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.95f, 1f, 45f, false));

            AssertStringEquals(solution.SolverKind, CombatAimBallisticSolverKinds.ReturningProjectile, "returning solver kind");
            AssertStringEquals(solution.LeadWindowKind, CombatAimLeadWindowKinds.ReturningOutbound, "returning lead window");
            AssertStringEquals(solution.SpecialWeaponKind, "returning", "returning special kind");
            AssertStringEquals(solution.SpecialWeaponLeadPolicy, "outboundOnly", "returning lead policy");
            AssertStringEquals(solution.ReturningPhaseAssumption, "outboundOnly", "returning phase assumption");
            if (solution.LeadWindowMaxTicks > 30.001f)
            {
                throw new InvalidOperationException("Expected returning projectile to stay in the outbound lead window.");
            }
        }

        private static void CombatAimBallisticSolverClassifiesSpreadCoverage()
        {
            var solution = SolveCombatAimBallistic(
                new FakePlayer(),
                new FakeItem
                {
                    type = 3475,
                    stack = 1,
                    Name = "Vortex Beater",
                    damage = 50,
                    shoot = 615,
                    shootSpeed = 14f,
                    useAmmo = Terraria.ID.AmmoID.Bullet,
                    ranged = true
                },
                new FakeItem
                {
                    type = 97,
                    stack = 99,
                    Name = "Musket Ball",
                    ammo = Terraria.ID.AmmoID.Bullet,
                    shoot = Terraria.ID.ProjectileID.Bullet,
                    shootSpeed = 4f
                },
                BallisticTarget(500f, 21f, 2f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.95f, 1f, 45f, false));

            AssertStringEquals(solution.SolverKind, CombatAimBallisticSolverKinds.Spread, "spread solver kind");
            AssertStringEquals(solution.LeadWindowKind, CombatAimLeadWindowKinds.SpreadCoverage, "spread lead window");
            AssertStringEquals(solution.SpecialWeaponKind, "dualProjectileSpread", "spread special kind");
            AssertStringEquals(solution.SpecialWeaponLeadPolicy, "spreadCoverage", "spread lead policy");
            AssertNear(solution.SpecialSpreadDegrees, 8d, "spread coverage degrees");
            if (solution.LeadWindowMaxTicks > 18.001f)
            {
                throw new InvalidOperationException("Expected spread solver to use a coverage window, not the ordinary single-projectile window.");
            }
        }

        private static void CombatAimBallisticSolverFallsBackWithoutProjectile()
        {
            var solution = SolveCombatAimBallistic(
                new FakePlayer(),
                new FakeItem
                {
                    type = 10104,
                    stack = 1,
                    Name = "Fallback Test Sword",
                    damage = 12,
                    melee = true
                },
                null,
                BallisticTarget(240f, 21f, 2f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.95f, 1f, 45f, false));

            AssertStringEquals(solution.SolverKind, CombatAimBallisticSolverKinds.FallbackCenter, "fallback solver kind");
            AssertStringEquals(solution.LeadWindowKind, CombatAimLeadWindowKinds.Fallback, "fallback lead window");
            AssertStringEquals(solution.LeadClampReason, CombatAimLeadClampReasons.CenterFallback, "fallback clamp reason");
            if (!solution.ConservativeCenter || !solution.Solved)
            {
                throw new InvalidOperationException("Expected missing projectile semantics to fall back to the target center.");
            }
        }

        private static void CombatAimPredictedSamplerLeadsSmallMovingHitbox()
        {
            CombatAimLineOfSight.SetCanHitLineOverrideForTesting((fromX, fromY, toX, toY) => true);
            try
            {
                var target = BallisticTarget(300f, 21f, 4f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.92f, 0.95f, 1f, 45f, false);
                var selection = SelectCombatAimSampleForTesting(target, CombatAimModes.TargetPriorityClearLine, CombatAimTargetMotionProfile.StableLinear, 18f);

                AssertStringEquals(selection.SampleSpace, CombatAimPredictedHitboxSampler.SampleSpacePredicted, "moving hitbox sample space");
                if (selection.BallisticTarget == null ||
                    selection.BallisticSolution == null ||
                    selection.SelectedSampleWorldX <= target.CenterX + 8f ||
                    Math.Abs(selection.BallisticTarget.CenterX - selection.SelectedSampleWorldX) > 0.001f ||
                    Math.Abs(selection.BallisticSolution.AimWorldX - selection.SelectedSampleWorldX) > 0.001f)
                {
                    throw new InvalidOperationException("Expected predicted sampler to bind the final moving-hitbox sample to both BallisticTarget and ballistic aim.");
                }
            }
            finally
            {
                CombatAimLineOfSight.SetCanHitLineOverrideForTesting(null);
            }
        }

        private static void CombatAimPredictedSamplerChoosesVisibleLargeHitboxSample()
        {
            CombatAimLineOfSight.SetCanHitLineOverrideForTesting((fromX, fromY, toX, toY) => toX >= 398f);
            try
            {
                var target = BallisticTarget(360f, 21f, 0f, 0f, CombatAimTargetMotionProfile.LargeOrSegmented, 0.8f, 0.8f, 1f, 36f, false);
                target.HitboxX = 280f;
                target.HitboxY = -29f;
                target.HitboxWidth = 160f;
                target.HitboxHeight = 100f;
                target.Width = 160;
                target.Height = 100;
                target.LifeMax = 3000;

                var selection = SelectCombatAimSampleForTesting(target, CombatAimModes.TargetPriorityClearLine, CombatAimTargetMotionProfile.LargeOrSegmented, 18f);

                AssertStringEquals(selection.SelectedSamplePoint, "rightMid", "large visible sample");
                if (!selection.LineClear ||
                    selection.VisibleSampleCount <= 0 ||
                    selection.LineOfSightRejectedSampleCount <= 0)
                {
                    throw new InvalidOperationException("Expected large semi-blocked target to reject blocked samples and keep a visible hitbox sample.");
                }
            }
            finally
            {
                CombatAimLineOfSight.SetCanHitLineOverrideForTesting(null);
            }
        }

        private static void CombatAimPredictedSamplerKeepsLowConfidenceCurrent()
        {
            CombatAimLineOfSight.SetCanHitLineOverrideForTesting((fromX, fromY, toX, toY) => true);
            try
            {
                var target = BallisticTarget(300f, 21f, 5f, -2f, CombatAimTargetMotionProfile.JumpingAirborne, 0.35f, 0.35f, 0.75f, 18f, true);
                var selection = SelectCombatAimSampleForTesting(target, CombatAimModes.TargetPriorityClearLine, CombatAimTargetMotionProfile.JumpingAirborne, 18f);

                AssertStringEquals(selection.SampleSpace, CombatAimPredictedHitboxSampler.SampleSpaceCurrent, "low confidence sample space");
                AssertStringEquals(selection.SelectedSamplePoint, "center", "low confidence stable sample");
                if (selection.PredictedHitboxCenterX <= target.CenterX)
                {
                    throw new InvalidOperationException("Expected low-confidence diagnostics to still expose the predicted hitbox center.");
                }
            }
            finally
            {
                CombatAimLineOfSight.SetCanHitLineOverrideForTesting(null);
            }
        }

        private static void CombatAimPredictedSamplerKeepsCenterOverNearest()
        {
            var target = BallisticTarget(300f, 21f, 0f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.9f, 1f, 36f, false);
            var selection = SelectCombatAimSampleForTesting(target, CombatAimModes.TargetPriorityNearest, CombatAimTargetMotionProfile.StableLinear, 18f);

            AssertStringEquals(selection.SampleSpace, CombatAimPredictedHitboxSampler.SampleSpaceCurrent, "nearest sample space");
            AssertStringEquals(selection.SelectedSamplePoint, "center", "nearest sample center preference");
            if (!selection.CenterPreferred || !selection.NearestHitboxPointPenaltyApplied)
            {
                throw new InvalidOperationException("Expected nearestHitboxPoint to remain penalized unless visibility clearly justifies it.");
            }
        }

        private static void CombatAimDecisionCacheReusesAttackSelectionWithinTtl()
        {
            CombatAimDecisionCache.ResetForTesting();
            try
            {
                var selection = CachedCombatAimSelectionForTesting(7, 101, 120f, 140f);
                CombatAimDecisionCache.StoreSelection("cache-key", 100, selection, "Attack");

                CombatAimTargetSelection cached;
                if (!CombatAimDecisionCache.TryGetSelection("cache-key", 103, out cached))
                {
                    throw new InvalidOperationException("Expected cached combat aim decision to be reusable within the short TTL.");
                }

                if (!cached.SelectionCacheHit ||
                    cached.DecisionCacheAgeTicks != 3 ||
                    !string.Equals(cached.DecisionCacheSource, "Attack", StringComparison.Ordinal) ||
                    cached.Target == null ||
                    cached.Target.WhoAmI != 7 ||
                    !string.Equals(cached.SelectedSamplePoint, "center", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected cached decision metadata and target identity to survive cloning.");
                }

                cached.Target.CenterX = 999f;
                CombatAimTargetSelection cachedAgain;
                if (!CombatAimDecisionCache.TryGetSelection("cache-key", 103, out cachedAgain) ||
                    Math.Abs(cachedAgain.Target.CenterX - 120f) > 0.001f)
                {
                    throw new InvalidOperationException("Expected decision cache to return cloned selections, not shared mutable instances.");
                }
            }
            finally
            {
                CombatAimDecisionCache.ResetForTesting();
            }
        }

        private static void CombatAimDecisionCacheExpiresStaleSelection()
        {
            CombatAimDecisionCache.ResetForTesting();
            try
            {
                CombatAimDecisionCache.StoreSelection("cache-key", 100, CachedCombatAimSelectionForTesting(7, 101, 120f, 140f), "Attack");
                CombatAimTargetSelection cached;
                if (CombatAimDecisionCache.TryGetSelection("cache-key", 105, out cached))
                {
                    throw new InvalidOperationException("Expected combat aim decision cache to expire after the short TTL.");
                }
            }
            finally
            {
                CombatAimDecisionCache.ResetForTesting();
            }
        }

        private static void CombatAimCachedSelectionValidationRejectsStaleTarget()
        {
            var selection = CachedCombatAimSelectionForTesting(7, 101, 120f, 140f);
            string reason;
            if (CombatAimItemCheckService.ValidateCachedSelectionForTesting(
                    selection,
                    null,
                    false,
                    "targetNotFound",
                    CombatAimModes.TargetPriorityClearLine,
                    out reason) ||
                !string.Equals(reason, "targetStale:targetNotFound", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected missing live target to reject cached aim with a targetStale reason, got " + reason);
            }

            var moved = CachedCombatAimSelectionForTesting(7, 101, 160f, 140f).Target;
            if (CombatAimItemCheckService.ValidateCachedSelectionForTesting(
                    selection,
                    moved,
                    true,
                    string.Empty,
                    CombatAimModes.TargetPriorityClearLine,
                    out reason) ||
                !string.Equals(reason, "targetStale:hitboxMoved", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected moved live target to reject cached aim, got " + reason);
            }
        }

        private static void CombatAimSelectorExplainsMarkerAttackMismatch()
        {
            CombatAimLineOfSight.SetCanHitLineOverrideForTesting((fromX, fromY, toX, toY) => true);
            try
            {
                var attackTarget = BallisticTarget(120f, 21f, 0f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.9f, 1f, 36f, false);
                attackTarget.WhoAmI = 1;
                attackTarget.Type = 101;
                var markerTargetOutsideRange = BallisticTarget(1200f, 21f, 0f, 0f, CombatAimTargetMotionProfile.StableLinear, 0.9f, 0.9f, 1f, 36f, false);
                markerTargetOutsideRange.WhoAmI = 2;
                markerTargetOutsideRange.Type = 102;
                var readResult = ReadResultWith(attackTarget, markerTargetOutsideRange);
                readResult.CursorWorldX = 640f;
                readResult.CursorWorldY = 21f;

                var selection = CombatAimTargetSelector.Select(
                    readResult,
                    20,
                    false,
                    true,
                    new CombatAimTargetSelectionContext
                    {
                        AimRangeOrigin = CombatAimModes.RangeOriginPlayer,
                        AimTargetPriority = CombatAimModes.TargetPriorityClearLine,
                        CursorAimRadius = 20,
                        PlayerAimRadius = 20,
                        HasPlayerCenter = true,
                        PlayerCenterX = 0f,
                        PlayerCenterY = 21f,
                        SelectionPurpose = "Attack",
                        PreferredTargetWhoAmI = 2,
                        PreferredTargetType = 102
                    });

                if (selection == null ||
                    selection.Target == null ||
                    selection.Target.WhoAmI != 1 ||
                    !selection.MarkerAttackTargetMismatch ||
                    !selection.MarkerTargetChangedForAttack ||
                    !string.Equals(selection.MarkerAttackMismatchReason, "itemCheckAttackRequiresStricterPath", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected selector to explain marker/attack target mismatch with the stricter attack-path reason.");
                }
            }
            finally
            {
                CombatAimLineOfSight.SetCanHitLineOverrideForTesting(null);
            }
        }


    }
}
