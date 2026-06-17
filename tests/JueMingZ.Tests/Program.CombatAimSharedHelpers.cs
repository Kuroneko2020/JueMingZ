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
        private static CombatAimTargetSelection CachedCombatAimSelectionForTesting(int whoAmI, int type, float centerX, float centerY)
        {
            var target = new CombatTargetSnapshot
            {
                WhoAmI = whoAmI,
                Type = type,
                Name = "Cached Target",
                Active = true,
                Life = 100,
                LifeMax = 100,
                CenterX = centerX,
                CenterY = centerY,
                HitboxX = centerX - 20f,
                HitboxY = centerY - 20f,
                HitboxWidth = 40f,
                HitboxHeight = 40f,
                Width = 40,
                Height = 40,
                MotionProfile = new CombatAimTargetMotionProfile
                {
                    MotionProfileKind = CombatAimTargetMotionProfile.StableLinear,
                    MotionConfidence = 0.9f
                }
            };

            return new CombatAimTargetSelection
            {
                Enabled = true,
                RadiusTiles = 20,
                TrackDummy = false,
                MarkerEnabled = true,
                AimRangeOrigin = CombatAimModes.RangeOriginPlayer,
                AimTargetPriority = CombatAimModes.TargetPriorityClearLine,
                ActiveRangeMode = "playerScreen",
                Target = target,
                BallisticTarget = target.CloneForAimSample(centerX, centerY),
                BallisticSolution = new CombatAimBallisticSolution
                {
                    Solved = true,
                    AimWorldX = centerX,
                    AimWorldY = centerY,
                    PlayerCenterX = 0f,
                    PlayerCenterY = centerY,
                    SolverKind = CombatAimBallisticSolverKinds.LinearIntercept,
                    PredictionConfidence = CombatAimPredictionConfidenceKinds.High,
                    SelectedSamplePoint = "center",
                    SelectedSampleWorldX = centerX,
                    SelectedSampleWorldY = centerY
                },
                SelectedSamplePoint = "center",
                AttackSamplePoint = "center",
                SelectionSamplePoint = "center",
                SampleSpace = CombatAimPredictedHitboxSampler.SampleSpaceCurrent,
                SelectedSampleWorldX = centerX,
                SelectedSampleWorldY = centerY,
                PredictedHitboxCenterX = centerX,
                PredictedHitboxCenterY = centerY,
                VisibleSampleCount = 1,
                ProjectileHitRadius = 5f,
                LineClear = true,
                LineClearAvailable = true,
                SelectionPurpose = "Attack",
                MarkerTargetWhoAmI = whoAmI,
                MarkerTargetType = type,
                AttackTargetWhoAmI = whoAmI,
                AttackTargetType = type,
                ResultCode = "TargetSelected",
                SkipReason = "none"
            };
        }

        private static CombatAimItemCheckDecision BuildCombatAimDiagnosticDecision()
        {
            var item = new FakeItem
            {
                type = 99,
                stack = 1,
                Name = "Diagnostic Bow",
                damage = 17,
                shoot = 1,
                shootSpeed = 8.5f,
                useAmmo = 1,
                useStyle = 5,
                useTime = 20,
                useAnimation = 20,
                ranged = true
            };
            var player = new FakePlayer();
            player.inventory[0] = item;

            var selection = new CombatAimTargetSelection
            {
                HasCursorWorld = true,
                CursorWorldX = 700f,
                CursorWorldY = 610f,
                RangeCenterWorldX = 800f,
                RangeCenterWorldY = 600f,
                Target = new CombatTargetSnapshot
                {
                    WhoAmI = 3,
                    Type = 4,
                    Name = "Diagnostic Target",
                    CenterX = 120f,
                    CenterY = 140f,
                    HitboxX = 100f,
                    HitboxY = 120f,
                    HitboxWidth = 40f,
                    HitboxHeight = 40f,
                    MotionProfile = new CombatAimTargetMotionProfile
                    {
                        MotionProfileKind = CombatAimTargetMotionProfile.StableLinear,
                        MotionConfidence = 0.9f,
                        VelocityConfidence = 0.8f,
                        AccelerationX = 0.1f,
                        AccelerationY = -0.05f,
                        AccelerationConfidence = 0.7f,
                        RecommendedLeadScale = 0.75f,
                        RecommendedMaxLeadTicks = 24f,
                        PreferSmoothedVelocity = true,
                        HistoryResetReason = "none"
                    }
                },
                SelectedSamplePoint = "center",
                AttackSamplePoint = "center",
                SelectionSamplePoint = "center",
                SampleSpace = CombatAimPredictedHitboxSampler.SampleSpacePredicted,
                SelectedSampleWorldX = 120f,
                SelectedSampleWorldY = 140f,
                PredictedHitboxCenterX = 124f,
                PredictedHitboxCenterY = 140f,
                VisibleSampleCount = 4,
                ProjectileHitRadius = 6f,
                LineClear = true,
                LineClearAvailable = true,
                MarkerTargetWhoAmI = 2,
                AttackTargetWhoAmI = 3,
                MarkerAttackTargetMismatch = true,
                MarkerAttackMismatchReason = "lineOfSightChanged",
                SelectionCacheHit = true,
                SelectionCacheKey = "diagnostic-cache-key",
                DecisionCacheSource = "attackSelectionCache",
                DecisionCacheAgeTicks = 2,
                DecisionCacheRevalidationReason = "lineOfSightChanged",
                SelectionPurpose = "Attack"
            };

            return new CombatAimItemCheckDecision
            {
                Enabled = true,
                RadiusTiles = 18,
                AimRangeOrigin = "mouse",
                AimTargetPriority = "line-of-sight",
                ActiveRangeMode = "cursorSlider",
                TrackDummy = true,
                MarkerEnabled = true,
                ResultCode = "Succeeded",
                AimApplyMode = CombatAimApplyModes.PersistentCursor,
                PersistentCursorActive = true,
                PersistentHook = "ProjectileAI",
                PersistentCursorReason = "diagnostic",
                SelectedSlot = 0,
                ItemType = item.type,
                ItemStack = item.stack,
                ItemName = item.Name,
                Damage = item.damage,
                Shoot = item.shoot,
                UseAmmo = item.useAmmo,
                WeaponProfile = CombatAimWeaponProfile.Read(player, item),
                HasCursorWorld = true,
                CursorWorldX = selection.CursorWorldX,
                CursorWorldY = selection.CursorWorldY,
                RangeCenterWorldX = selection.RangeCenterWorldX,
                RangeCenterWorldY = selection.RangeCenterWorldY,
                Selection = selection,
                BallisticSolution = new CombatAimBallisticSolution
                {
                    Solved = true,
                    Mode = "linearBasic",
                    ProjectileType = 12,
                    ProjectileName = "Diagnostic Projectile",
                    ProjectileAiStyle = 1,
                    ProjectileExtraUpdates = 1,
                    ProjectileDefaultsAvailable = true,
                    ProjectileTileCollide = true,
                    ProjectileWidth = 10,
                    ProjectileHeight = 12,
                    ProjectileFriendly = true,
                    BaseProjectileSpeed = 9.5f,
                    EffectiveProjectileSpeed = 13.5f,
                    EffectiveUpdatesPerTick = 2,
                    ProjectileProfileFamily = "GravityArc",
                    ProjectileProfileStatus = "complete",
                    ProjectileProfileSpeedSource = "ammo",
                    ProjectileProfileMagicQuiverApplied = true,
                    ProjectileProfileArcheryApplied = true,
                    AmmoAvailable = true,
                    AmmoItemType = 40,
                    AmmoItemName = "Diagnostic Arrow",
                    AmmoProjectileType = 12,
                    AmmoShootSpeed = 2f,
                    ProjectileSpeed = 10.5f,
                    SolverKind = CombatAimBallisticSolverKinds.GravityArc,
                    LeadWindowKind = CombatAimLeadWindowKinds.GravityArc,
                    LeadClampReason = CombatAimLeadClampReasons.None,
                    PredictionConfidence = CombatAimPredictionConfidenceKinds.High,
                    GravityCompensationPixels = 3.25f,
                    SampleSpace = CombatAimPredictedHitboxSampler.SampleSpacePredicted,
                    SelectedSamplePoint = "center",
                    SelectedSampleWorldX = 120f,
                    SelectedSampleWorldY = 140f,
                    PredictedHitboxCenterX = 124f,
                    PredictedHitboxCenterY = 140f,
                    VisibleSampleCount = 4,
                    ProjectileHitRadius = 6f
                }
            };
        }

        private static CombatAimItemCheckDecision BuildXenopopperTailDecision(FakePlayer player, float aimWorldX, float aimWorldY)
        {
            var item = new FakeItem
            {
                type = 2797,
                stack = 1,
                Name = "Xenopopper",
                damage = 45,
                shoot = 444,
                shootSpeed = 24f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            player.inventory[0] = item;

            return new CombatAimItemCheckDecision
            {
                Enabled = true,
                RadiusTiles = 18,
                AimRangeOrigin = "mouse",
                AimTargetPriority = "line-of-sight",
                ActiveRangeMode = "cursorSlider",
                TrackDummy = true,
                MarkerEnabled = true,
                ResultCode = "Succeeded",
                AimApplyMode = CombatAimApplyModes.PersistentCursor,
                PersistentCursorActive = true,
                PersistentHook = PersistentCursorHooks.ProjectileAI,
                PersistentCursorReason = "specialProjectileWeapon",
                SelectedSlot = 0,
                ItemType = item.type,
                ItemStack = item.stack,
                ItemName = item.Name,
                Damage = item.damage,
                Shoot = item.shoot,
                UseAmmo = item.useAmmo,
                WeaponProfile = CombatAimWeaponProfile.Read(player, item),
                AimWorldX = aimWorldX,
                AimWorldY = aimWorldY,
                Selection = new CombatAimTargetSelection
                {
                    Target = new CombatTargetSnapshot
                    {
                        WhoAmI = 42,
                        Type = 245,
                        Name = "Xenopopper Target",
                        Active = true,
                        Life = 100,
                        LifeMax = 100,
                        CenterX = aimWorldX,
                        CenterY = aimWorldY,
                        HitboxX = aimWorldX - 20f,
                        HitboxY = aimWorldY - 20f,
                        HitboxWidth = 40f,
                        HitboxHeight = 40f
                    },
                    SelectedSampleWorldX = aimWorldX,
                    SelectedSampleWorldY = aimWorldY,
                    SelectedSamplePoint = "center",
                    AttackSamplePoint = "center",
                    SelectionSamplePoint = "center",
                    AttackTargetWhoAmI = 42,
                    AttackTargetType = 245,
                    LineClear = true,
                    LineClearAvailable = true,
                    SelectionPurpose = "PersistentCursor"
                },
                BallisticSolution = new CombatAimBallisticSolution
                {
                    Solved = true,
                    Mode = "specialCursorSpawnBurst",
                    ProjectileType = 14,
                    ProjectileName = "Bullet",
                    AmmoProjectileType = 14,
                    AmmoProjectileName = "Bullet",
                    PrimaryProjectileType = 14,
                    PrimaryProjectileName = "Bullet",
                    PrimaryProjectileRole = "ammoPrimary",
                    AmmoItemType = 97,
                    AmmoItemName = "Bullet",
                    WeaponShootProjectileType = 444,
                    WeaponShootProjectileName = "Xenopopper Bubble",
                    SecondaryProjectileType = 444,
                    SecondaryProjectileName = "Xenopopper Bubble",
                    SecondaryProjectileRole = "weaponAssist",
                    SpecialWeaponKind = "cursorSpawnBurst",
                    SpecialWeaponName = "Xenopopper",
                    SpecialWeaponRule = "cursorSpawnBubbleBullet",
                    SpecialCursorTarget = true,
                    SpecialAimApplied = true,
                    SpecialWeaponUsesWeaponShoot = true,
                    SpecialWeaponUsesAmmoShoot = true,
                    ProjectileSpeed = 24f,
                    AimWorldX = aimWorldX,
                    AimWorldY = aimWorldY
                }
            };
        }

        private static CombatAimItemCheckDecision BuildVortexTailDecision(FakePlayer player, float aimWorldX, float aimWorldY)
        {
            var item = new FakeItem
            {
                type = 3475,
                stack = 1,
                Name = "VortexBeater",
                damage = 50,
                shoot = 615,
                shootSpeed = 14f,
                useAmmo = 97,
                ranged = true,
                channel = true,
                useStyle = 5
            };
            player.inventory[0] = item;

            return new CombatAimItemCheckDecision
            {
                Enabled = true,
                RadiusTiles = 18,
                AimRangeOrigin = "mouse",
                AimTargetPriority = "line-of-sight",
                ActiveRangeMode = "cursorSlider",
                TrackDummy = true,
                MarkerEnabled = true,
                ResultCode = "Succeeded",
                AimApplyMode = CombatAimApplyModes.PersistentCursor,
                PersistentCursorActive = true,
                PersistentHook = PersistentCursorHooks.ProjectileAI,
                PersistentCursorReason = "specialProjectileWeapon",
                SelectedSlot = 0,
                ItemType = item.type,
                ItemStack = item.stack,
                ItemName = item.Name,
                Damage = item.damage,
                Shoot = item.shoot,
                UseAmmo = item.useAmmo,
                WeaponProfile = CombatAimWeaponProfile.Read(player, item),
                AimWorldX = aimWorldX,
                AimWorldY = aimWorldY,
                Selection = new CombatAimTargetSelection
                {
                    Target = new CombatTargetSnapshot
                    {
                        WhoAmI = 43,
                        Type = 245,
                        Name = "Vortex Target",
                        Active = true,
                        Life = 100,
                        LifeMax = 100,
                        CenterX = aimWorldX,
                        CenterY = aimWorldY,
                        HitboxX = aimWorldX - 20f,
                        HitboxY = aimWorldY - 20f,
                        HitboxWidth = 40f,
                        HitboxHeight = 40f
                    },
                    SelectedSampleWorldX = aimWorldX,
                    SelectedSampleWorldY = aimWorldY,
                    SelectedSamplePoint = "center",
                    AttackSamplePoint = "center",
                    SelectionSamplePoint = "center",
                    AttackTargetWhoAmI = 43,
                    AttackTargetType = 245,
                    LineClear = true,
                    LineClearAvailable = true,
                    SelectionPurpose = "PersistentCursor"
                },
                BallisticSolution = new CombatAimBallisticSolution
                {
                    Solved = true,
                    Mode = "specialSpreadMultiShot",
                    ProjectileType = 14,
                    ProjectileName = "Bullet",
                    ResolvedProjectileRole = "ammoProjectile",
                    AmmoProjectileType = 14,
                    AmmoProjectileName = "Bullet",
                    PrimaryProjectileType = 14,
                    PrimaryProjectileName = "Bullet",
                    PrimaryProjectileRole = "ammoPrimary",
                    AmmoItemType = 97,
                    AmmoItemName = "Bullet",
                    WeaponShootProjectileType = 615,
                    WeaponShootProjectileName = "Vortex Beater",
                    SecondaryProjectileType = 615,
                    SecondaryProjectileName = "Vortex Beater",
                    SecondaryProjectileRole = "weaponAssist",
                    SpecialWeaponKind = "dualProjectileSpread",
                    SpecialWeaponName = "VortexBeater",
                    SpecialWeaponRule = "bulletSpreadWithRocketAssist",
                    SpecialAimApplied = true,
                    SpecialWeaponUsesWeaponShoot = true,
                    SpecialWeaponUsesAmmoShoot = true,
                    ProjectileSpeed = 14f,
                    AimWorldX = aimWorldX,
                    AimWorldY = aimWorldY
                }
            };
        }

        private static string BuildCombatAimDecisionJson(CombatAimItemCheckDecision decision, bool mouseOverrideApplied, bool restored)
        {
            var method = typeof(CombatAimItemCheckService).GetMethod("BuildDecisionJson", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("BuildDecisionJson not found.");
            }

            return (string)method.Invoke(null, new object[] { decision, mouseOverrideApplied, restored });
        }

        private static void AssertCombatAimSkipReason(string raw, string expected)
        {
            var method = typeof(CombatAimItemCheckService).GetMethod("NormalizeSkipReason", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("NormalizeSkipReason not found.");
            }

            var actual = (string)method.Invoke(null, new object[] { new CombatAimItemCheckDecision(), raw });
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + raw + " -> " + expected + ", got " + actual);
            }
        }

        private static CombatAimWeaponProfile BuildCombatAimWeaponProfile(FakeItem item)
        {
            var player = new FakePlayer();
            player.inventory[0] = item;
            return CombatAimWeaponProfile.Read(player, item);
        }

        private static CombatAimProjectileProfile BuildCombatAimProjectileProfile(FakePlayer player, FakeItem weapon, FakeItem ammo)
        {
            player = player ?? new FakePlayer();
            player.inventory[0] = weapon;
            if (ammo != null)
            {
                player.inventory[54] = ammo;
            }

            var weaponProfile = CombatAimWeaponProfile.Read(player, weapon);
            return CombatAimProjectileProfileResolver.Resolve(player, weaponProfile);
        }

        private static FakeItem SlowMagicWeapon()
        {
            return new FakeItem
            {
                type = 10105,
                stack = 1,
                Name = "Slow Test Magic Weapon",
                damage = 22,
                shoot = 9002,
                shootSpeed = 4f,
                magic = true
            };
        }

        private static CombatAimBallisticSolution SolveCombatAimBallistic(
            FakePlayer player,
            FakeItem weapon,
            FakeItem ammo,
            CombatTargetSnapshot target)
        {
            player = player ?? new FakePlayer();
            player.inventory[0] = weapon;
            if (ammo != null)
            {
                player.inventory[54] = ammo;
            }

            var weaponProfile = CombatAimWeaponProfile.Read(player, weapon);
            return CombatAimBallisticSolver.Solve(player, weaponProfile, target);
        }

        private static CombatAimTargetSelection SelectCombatAimSampleForTesting(
            CombatTargetSnapshot target,
            string priority,
            string motionKind,
            float projectileSpeed)
        {
            var player = new FakePlayer();
            var weapon = new FakeItem
            {
                type = 10120,
                stack = 1,
                Name = "Sample Test Rifle",
                damage = 20,
                shoot = Terraria.ID.ProjectileID.Bullet,
                shootSpeed = projectileSpeed,
                useAmmo = Terraria.ID.AmmoID.Bullet,
                ranged = true,
                useStyle = 5
            };
            player.inventory[0] = weapon;
            var weaponProfile = CombatAimWeaponProfile.Read(player, weapon);
            if (target != null && target.MotionProfile != null)
            {
                target.MotionProfile.MotionProfileKind = motionKind ?? target.MotionProfile.MotionProfileKind;
            }

            var readResult = ReadResultWith(target);
            readResult.CursorWorldX = 640f;
            readResult.CursorWorldY = 21f;

            return CombatAimTargetSelector.Select(
                readResult,
                80,
                false,
                true,
                new CombatAimTargetSelectionContext
                {
                    AimRangeOrigin = CombatAimModes.RangeOriginPlayer,
                    AimTargetPriority = priority,
                    CursorAimRadius = 80,
                    PlayerAimRadius = 80,
                    HasPlayerCenter = true,
                    PlayerCenterX = 0f,
                    PlayerCenterY = 21f,
                    Player = player,
                    WeaponProfile = weaponProfile,
                    IncludeBallisticScoring = true,
                    SelectionPurpose = "Attack",
                    BallisticContext = new CombatAimBallisticContext
                    {
                        Prepared = true,
                        Weapon = weaponProfile,
                        HasPlayerCenter = true,
                        PlayerCenterX = 0f,
                        PlayerCenterY = 21f,
                        ProjectileType = Terraria.ID.ProjectileID.Bullet,
                        ProjectileName = "Bullet",
                        ProjectileAiStyle = 1,
                        ProjectileDefaultsAvailable = true,
                        ProjectileNoGravity = true,
                        ProjectileTileCollide = true,
                        ProjectileWidth = 10,
                        ProjectileHeight = 10,
                        ProjectileFriendly = true,
                        BaseProjectileSpeed = projectileSpeed,
                        ProjectileSpeed = projectileSpeed,
                        EffectiveProjectileSpeed = projectileSpeed,
                        EffectiveUpdatesPerTick = 1,
                        ProjectileRadiusForHit = 5f,
                        ProfileFamilyHint = "HighSpeedLinear",
                        ProfileCompleteness = "complete",
                        ProfileSpeedSource = "testing",
                        AmmoAvailable = true,
                        AmmoType = Terraria.ID.AmmoID.Bullet,
                        AmmoItemType = 97,
                        AmmoProjectileType = Terraria.ID.ProjectileID.Bullet,
                        AmmoShootSpeed = 0f,
                        AmmoBulletLike = true
                    }
                });
        }

        private static CombatTargetSnapshot BallisticTarget(
            float centerX,
            float centerY,
            float velocityX,
            float velocityY,
            string motionKind,
            float motionConfidence,
            float velocityConfidence,
            float leadScale,
            float maxLeadTicks,
            bool preferCurrentVelocity)
        {
            var target = TargetSnapshot(31, 131, centerX, centerY, velocityX, velocityY, 0, false, false);
            target.SmoothedVelocityAvailable = !preferCurrentVelocity;
            target.SmoothedVelocityX = velocityX;
            target.SmoothedVelocityY = velocityY;
            target.MotionProfile = new CombatAimTargetMotionProfile
            {
                MotionProfileKind = motionKind ?? CombatAimTargetMotionProfile.Unknown,
                MotionConfidence = motionConfidence,
                VelocityConfidence = velocityConfidence,
                RecommendedLeadScale = leadScale,
                RecommendedMaxLeadTicks = maxLeadTicks,
                PreferCurrentVelocity = preferCurrentVelocity,
                PreferSmoothedVelocity = !preferCurrentVelocity
            };
            return target;
        }

        private static CombatAimReadResult ReadResultWith(params CombatTargetSnapshot[] targets)
        {
            var result = new CombatAimReadResult
            {
                CanSearch = true
            };

            if (targets != null)
            {
                for (var index = 0; index < targets.Length; index++)
                {
                    result.Candidates.Add(targets[index]);
                }
            }

            return result;
        }

        private static CombatTargetSnapshot TargetSnapshot(
            int whoAmI,
            int type,
            float centerX,
            float centerY,
            float velocityX,
            float velocityY,
            int aiStyle,
            bool noGravity,
            bool collideY)
        {
            return new CombatTargetSnapshot
            {
                WhoAmI = whoAmI,
                Type = type,
                Name = "Motion Target",
                Active = true,
                Life = 100,
                LifeMax = 100,
                Chaseable = true,
                CenterX = centerX,
                CenterY = centerY,
                PositionX = centerX - 20f,
                PositionY = centerY - 20f,
                Width = 40,
                Height = 40,
                HitboxX = centerX - 20f,
                HitboxY = centerY - 20f,
                HitboxWidth = 40f,
                HitboxHeight = 40f,
                VelocityX = velocityX,
                VelocityY = velocityY,
                NpcAiStyle = aiStyle,
                NoGravity = noGravity,
                CollideY = collideY,
                Direction = velocityX < 0f ? -1 : 1,
                DirectionY = velocityY < 0f ? -1 : velocityY > 0f ? 1 : 0,
                TargetPlayer = 0,
                AiSummaryAvailable = true
            };
        }

        private static void AssertMotionKind(CombatTargetSnapshot target, string expectedKind)
        {
            if (target == null || target.MotionProfile == null)
            {
                throw new InvalidOperationException("Expected motion profile " + expectedKind + ", got null.");
            }

            AssertStringEquals(target.MotionProfile.MotionProfileKind, expectedKind, "motion profile kind");
        }

        private static void AssertWeaponFamily(
            string label,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            string expectedFamily)
        {
            var result = CombatAimWeaponFamilyResolver.Resolve(profile, solution);
            if (result == null ||
                !string.Equals(result.Family, expectedFamily, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(result.Reason))
            {
                throw new InvalidOperationException(
                    "Expected " + label + " family " + expectedFamily +
                    ", got " + (result == null ? "<null>" : result.Family + " / " + result.Reason));
            }
        }

        private static void AssertPersistentCursorEligibility(
            CombatAimPersistentCursorEligibility eligibility,
            bool expectedEligible,
            string expectedReason,
            string expectedClass)
        {
            if (eligibility == null)
            {
                throw new InvalidOperationException("Expected persistent cursor eligibility result.");
            }

            if (eligibility.Eligible != expectedEligible ||
                !string.Equals(eligibility.Reason, expectedReason, StringComparison.Ordinal) ||
                !string.Equals(eligibility.Class, expectedClass, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected eligibility " + expectedEligible + " / " + expectedReason + " / " + expectedClass +
                    ", got " + eligibility.Eligible + " / " + eligibility.Reason + " / " + eligibility.Class);
            }
        }

    }
}
