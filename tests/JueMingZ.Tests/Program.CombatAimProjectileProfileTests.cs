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
        private static void CombatGoblinExecutionAllowsOnlyTinkererWhenEnabled()
        {
            if (CombatGoblinExecutionCompat.ShouldAllowGoblinExecutionForTesting(false, true, CombatGoblinExecutionCompat.GoblinTinkererNpcType))
            {
                throw new InvalidOperationException("Goblin execution must stay disabled until both transpilers are ready.");
            }

            if (CombatGoblinExecutionCompat.ShouldAllowGoblinExecutionForTesting(true, false, CombatGoblinExecutionCompat.GoblinTinkererNpcType))
            {
                throw new InvalidOperationException("Goblin execution must stay disabled when config is off.");
            }

            if (!CombatGoblinExecutionCompat.ShouldAllowGoblinExecutionForTesting(true, true, CombatGoblinExecutionCompat.GoblinTinkererNpcType))
            {
                throw new InvalidOperationException("Goblin execution should allow NPC type 107 when hook and config are enabled.");
            }

            if (CombatGoblinExecutionCompat.ShouldAllowGoblinExecutionForTesting(true, true, CombatGoblinExecutionCompat.BoundGoblinNpcType) ||
                CombatGoblinExecutionCompat.ShouldAllowGoblinExecutionForTesting(true, true, 22) ||
                CombatGoblinExecutionCompat.ShouldAllowGoblinExecutionForTesting(true, true, 54) ||
                CombatGoblinExecutionCompat.ShouldAllowGoblinExecutionForTesting(true, true, 108))
            {
                throw new InvalidOperationException("Goblin execution must not allow BoundGoblin, guide, clothier, or other town NPC types.");
            }
        }

        private static void CombatAimDiagnosticsMetadataKeepsStableFieldNames()
        {
            // These JSON field names are consumed by runtime snapshots, action
            // events, and user-return diagnostics; rename only with doc updates.
            var decision = BuildCombatAimDiagnosticDecision();
            var json = BuildCombatAimDecisionJson(decision, true, true);

            AssertContains(json, "\"itemType\"");
            AssertContains(json, "\"itemName\"");
            AssertContains(json, "\"damageType\"");
            AssertContains(json, "\"ammoItemType\"");
            AssertContains(json, "\"ammoItemName\"");
            AssertContains(json, "\"ammoShoot\"");
            AssertContains(json, "\"resolvedProjectileType\"");
            AssertContains(json, "\"resolvedProjectileName\"");
            AssertContains(json, "\"projectileTileCollide\"");
            AssertContains(json, "\"projectileWidth\"");
            AssertContains(json, "\"projectileFriendly\"");
            AssertContains(json, "\"aimPurpose\"");
            AssertContains(json, "\"applyPolicy\"");
            AssertContains(json, "\"lineOfSightResult\"");
            AssertContains(json, "\"markerAttackTargetMismatch\"");
            AssertContains(json, "\"markerAttackMismatchReason\"");
            AssertContains(json, "\"markerAttackTargetMismatchReason\"");
            AssertContains(json, "\"decisionCacheSource\"");
            AssertContains(json, "\"decisionCacheAgeTicks\"");
            AssertContains(json, "\"decisionCacheRevalidationReason\"");
            AssertContains(json, "\"aimDecisionCacheHit\"");
            AssertContains(json, "\"liveTargetRevalidation\"");
            AssertContains(json, "\"itemCheckAimEntered\"");
            AssertContains(json, "\"mouseStateCaptured\"");
            AssertContains(json, "\"weaponFamily\"");
            AssertContains(json, "\"weaponFamilyReason\"");
            AssertContains(json, "\"persistentCursorTargetSet\"");
            AssertContains(json, "\"persistentCursorEligibility\"");
            AssertContains(json, "\"persistentCursorEligibilityReason\"");
            AssertContains(json, "\"persistentCursorClass\"");
            AssertContains(json, "\"persistentCursorMainUpdateFallbackAllowed\"");
            AssertContains(json, "\"persistentCursorProjectileAiScopedAllowed\"");
            AssertContains(json, "\"persistentCursorScopedOverride\"");
            AssertContains(json, "\"projectileCursorMatch\"");
            AssertContains(json, "\"projectileCursorMatchReason\"");
            AssertContains(json, "\"projectileCursorProjectileType\"");
            AssertContains(json, "\"projectileCursorOwner\"");
            AssertContains(json, "\"visibleCursorHijackRisk\"");
            AssertContains(json, "\"visibleCursorHijackRiskMitigated\"");
            AssertContains(json, "\"userCursorWorldAvailable\"");
            AssertContains(json, "\"userCursorWorld\"");
            AssertContains(json, "\"simulatedAimWorld\"");
            AssertContains(json, "\"cursorOwnershipMode\"");
            AssertContains(json, "\"releaseHoldTargetDummyAllowed\"");
            AssertContains(json, "\"flailControlEligible\"");
            AssertContains(json, "\"flailControlReason\"");
            AssertContains(json, "\"flailControlActive\"");
            AssertContains(json, "\"flailControlState\"");
            AssertContains(json, "\"flailInputMode\"");
            AssertContains(json, "\"flailInputPhase\"");
            AssertContains(json, "\"flailTakeoverScope\"");
            AssertContains(json, "\"flailPhysicalUseItemHeld\"");
            AssertContains(json, "\"flailPhysicalReleasePending\"");
            AssertContains(json, "\"flailProjectileWhoAmI\"");
            AssertContains(json, "\"flailProjectileType\"");
            AssertContains(json, "\"flailProjectileAiStyle\"");
            AssertContains(json, "\"flailProjectileAi0\"");
            AssertContains(json, "\"flailProjectileVelocity\"");
            AssertContains(json, "\"flailProjectileIdentity\"");
            AssertContains(json, "\"flailHitDetected\"");
            AssertContains(json, "\"flailCollisionDetected\"");
            AssertContains(json, "\"flailLocalNpcImmunityChanged\"");
            AssertContains(json, "\"flailTileCollisionDetected\"");
            AssertContains(json, "\"flailAttackPulse\"");
            AssertContains(json, "\"flailAttackRelease\"");
            AssertContains(json, "\"flailAttackSuppressed\"");
            AssertContains(json, "\"flailAttackRestored\"");
            AssertContains(json, "\"flailPulseReason\"");
            AssertContains(json, "\"flailStuckRecovery\"");
            AssertContains(json, "\"flailCachedReleaseAim\"");
            AssertContains(json, "\"flailCachedReleaseAimAgeTicks\"");
            AssertContains(json, "\"flailCachedReleaseAimReason\"");
            AssertContains(json, "\"flailReleaseSuppressedPhysicalInput\"");
            AssertContains(json, "\"flailControlBlockedReason\"");
            AssertContains(json, "\"weaponShootProjectileType\"");
            AssertContains(json, "\"weaponShootProjectileName\"");
            AssertContains(json, "\"ammoProjectileType\"");
            AssertContains(json, "\"ammoProjectileName\"");
            AssertContains(json, "\"primaryProjectileType\"");
            AssertContains(json, "\"primaryProjectileName\"");
            AssertContains(json, "\"primaryProjectileRole\"");
            AssertContains(json, "\"resolvedProjectileRole\"");
            AssertContains(json, "\"secondaryProjectileType\"");
            AssertContains(json, "\"secondaryProjectileName\"");
            AssertContains(json, "\"secondaryProjectileRole\"");
            AssertContains(json, "\"specialWeaponRuleKind\"");
            AssertContains(json, "\"specialWeaponRuleName\"");
            AssertContains(json, "\"specialWeaponRuleApplied\"");
            AssertContains(json, "\"specialWeaponSolverKind\"");
            AssertContains(json, "\"specialWeaponLeadWindowKind\"");
            AssertContains(json, "\"specialWeaponLeadPolicy\"");
            AssertContains(json, "\"specialWeaponDiagnosticsReason\"");
            AssertContains(json, "\"specialWeaponAimMode\"");
            AssertContains(json, "\"specialWeaponAimPoint\"");
            AssertContains(json, "\"returningPhaseAssumption\"");
            AssertContains(json, "\"specialWeaponUsesCursorTarget\"");
            AssertContains(json, "\"specialWeaponUsesWeaponProjectile\"");
            AssertContains(json, "\"specialWeaponUsesAmmoProjectile\"");
            AssertContains(json, "\"specialWeaponUsesWeaponShoot\"");
            AssertContains(json, "\"specialWeaponUsesAmmoShoot\"");
            AssertContains(json, "\"projectileProfileFamily\"");
            AssertContains(json, "\"projectileProfileKind\"");
            AssertContains(json, "\"projectileProfileStatus\"");
            AssertContains(json, "\"projectileProfileDegradedReason\"");
            AssertContains(json, "\"profileFallbackReason\"");
            AssertContains(json, "\"projectileProfileSpeedSource\"");
            AssertContains(json, "\"effectiveProjectileSpeedSource\"");
            AssertContains(json, "\"projectileProfileMagicQuiverApplied\"");
            AssertContains(json, "\"magicQuiverApplied\"");
            AssertContains(json, "\"projectileProfileArcheryApplied\"");
            AssertContains(json, "\"archeryApplied\"");
            AssertContains(json, "\"projectileEffectiveUpdatesPerTick\"");
            AssertContains(json, "\"effectiveUpdatesPerTick\"");
            AssertContains(json, "\"projectileRadiusForHit\"");
            AssertContains(json, "\"ballisticSolverKind\"");
            AssertContains(json, "\"solverKind\"");
            AssertContains(json, "\"ballisticLeadWindowKind\"");
            AssertContains(json, "\"leadWindowKind\"");
            AssertContains(json, "\"ballisticLeadClampReason\"");
            AssertContains(json, "\"leadClampReason\"");
            AssertContains(json, "\"ballisticPredictionConfidence\"");
            AssertContains(json, "\"predictionConfidence\"");
            AssertContains(json, "\"sampleSpace\"");
            AssertContains(json, "\"predictedHitboxCenter\"");
            AssertContains(json, "\"visibleSampleCount\"");
            AssertContains(json, "\"projectileHitRadius\"");
            AssertContains(json, "\"ballisticSampleSpace\"");
            AssertContains(json, "\"ballisticSelectedSamplePoint\"");
            AssertContains(json, "\"ballisticRawLeadTicks\"");
            AssertContains(json, "\"ballisticLeadWindowMaxTicks\"");
            AssertContains(json, "\"ballisticLeadScale\"");
            AssertContains(json, "\"ballisticGravityDelayTicks\"");
            AssertContains(json, "\"ballisticGravityCompensationPixels\"");
            AssertContains(json, "\"gravityCompensationPixels\"");
            AssertContains(json, "\"effectiveProjectileSpeed\"");
            AssertContains(json, "\"targetMotionProfileKind\"");
            AssertContains(json, "\"targetMotionKind\"");
            AssertContains(json, "\"targetVelocityConfidence\"");
            AssertContains(json, "\"targetAcceleration\"");
            AssertContains(json, "\"targetRecommendedMaxLeadTicks\"");
            AssertContains(json, "\"targetHistoryResetReason\"");
            AssertContains(json, "\"aimDecisionCacheHit\":true");
            AssertContains(json, "\"liveTargetRevalidation\":\"lineOfSightChanged\"");
            AssertContains(json, "\"markerAttackTargetMismatchReason\":\"lineOfSightChanged\"");
            AssertContains(json, "\"projectileProfileKind\":\"GravityArc\"");
            AssertContains(json, "\"effectiveProjectileSpeed\":13.5");
            AssertContains(json, "\"gravityCompensationPixels\":3.25");
        }

        private static void CombatAimProjectileProfileResolvesBowAndArrow()
        {
            var player = new FakePlayer();
            var profile = BuildCombatAimProjectileProfile(
                player,
                new FakeItem
                {
                    type = 10000,
                    stack = 1,
                    Name = "Profile Test Bow",
                    damage = 9,
                    shoot = Terraria.ID.ProjectileID.WoodenArrowFriendly,
                    shootSpeed = 6f,
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
                    shootSpeed = 3f,
                    damage = 5,
                    knockBack = 1.25f
                });

            AssertStringEquals(profile.ProfileCompleteness, "complete", "profile status");
            AssertStringEquals(profile.ProfileFamilyHint, "GravityArc", "profile family");
            AssertStringEquals(profile.ResolvedProjectileRole, "ammoProjectile", "projectile role");
            AssertNear(profile.BaseProjectileSpeed, 9d, "bow arrow base speed");
            AssertNear(profile.EffectiveProjectileSpeed, 9d, "bow arrow effective speed");
            if (!profile.AmmoAvailable ||
                !profile.AmmoArrowLike ||
                profile.ProjectileType != Terraria.ID.ProjectileID.WoodenArrowFriendly ||
                profile.ProjectileRadiusForHit < 5f)
            {
                throw new InvalidOperationException("Expected basic bow + arrow profile to resolve ammo, arrow role, projectile type, and hit radius.");
            }
        }

        private static void CombatAimProjectileProfileAppliesQuiverAndArcherySpeed()
        {
            var player = new FakePlayer
            {
                magicQuiver = true
            };
            player.buffType[0] = Terraria.ID.BuffID.Archery;
            player.buffTime[0] = 60;

            var profile = BuildCombatAimProjectileProfile(
                player,
                new FakeItem
                {
                    type = 10001,
                    stack = 1,
                    Name = "Profile Test Bow",
                    damage = 9,
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
                });

            AssertNear(profile.BaseProjectileSpeed, 20d, "quiver archery capped speed");
            AssertNear(profile.EffectiveProjectileSpeed, 40d, "quiver archery effective speed");
            if (!profile.MagicQuiverApplied ||
                !profile.ArcheryApplied ||
                !profile.ArcherySpeedCapped ||
                !profile.MagicQuiverEffectiveUpdateApplied ||
                profile.EffectiveUpdatesPerTick != 2)
            {
                throw new InvalidOperationException("Expected quiver + archery profile to apply speed cap and friendly-arrow effective update floor.");
            }
        }

        private static void CombatAimProjectileProfileKeepsGunProjWeaponSpeed()
        {
            const int gunProjWeaponType = 5002;
            var previous = Terraria.ID.ItemID.Sets.gunProj[gunProjWeaponType];
            Terraria.ID.ItemID.Sets.gunProj[gunProjWeaponType] = true;
            try
            {
                var profile = BuildCombatAimProjectileProfile(
                    new FakePlayer(),
                    new FakeItem
                    {
                        type = gunProjWeaponType,
                        stack = 1,
                        Name = "Profile Test Gun",
                        damage = 12,
                        shoot = Terraria.ID.ProjectileID.Bullet,
                        shootSpeed = 10f,
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
                    });

                AssertNear(profile.BaseProjectileSpeed, 10d, "gunProj base speed");
                AssertNear(profile.EffectiveProjectileSpeed, 10d, "gunProj effective speed");
                AssertStringEquals(profile.ProfileSpeedSource, "weaponGunProjOnly", "gunProj speed source");
                if (!profile.GunProj || profile.AmmoSpeedApplied)
                {
                    throw new InvalidOperationException("Expected gunProj profile to use weapon shootSpeed without ammo speed.");
                }
            }
            finally
            {
                Terraria.ID.ItemID.Sets.gunProj[gunProjWeaponType] = previous;
            }
        }

        private static void CombatAimProjectileProfileResolvesSpecificLauncherMapping()
        {
            const int launcherType = 10003;
            const int ammoItemType = 10004;
            Dictionary<int, int> previous;
            Terraria.ID.AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.TryGetValue(launcherType, out previous);
            Terraria.ID.AmmoID.Sets.SpecificLauncherAmmoProjectileMatches[launcherType] = new Dictionary<int, int>
            {
                { ammoItemType, 9000 }
            };

            try
            {
                var profile = BuildCombatAimProjectileProfile(
                    new FakePlayer(),
                    new FakeItem
                    {
                        type = launcherType,
                        stack = 1,
                        Name = "Profile Test Launcher",
                        damage = 20,
                        shoot = 100,
                        shootSpeed = 8f,
                        useAmmo = ammoItemType,
                        ranged = true
                    },
                    new FakeItem
                    {
                        type = ammoItemType,
                        stack = 99,
                        Name = "Mapped Ammo",
                        ammo = ammoItemType,
                        shoot = 101,
                        shootSpeed = 2f
                    });

                if (!profile.SpecificLauncherAmmoProjectileMatch ||
                    profile.ProjectileType != 9000 ||
                    !string.Equals(profile.ProjectileName, "Specific Launcher Projectile", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected specific launcher mapping to override ammo projectile.");
                }
            }
            finally
            {
                if (previous == null)
                {
                    Terraria.ID.AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.Remove(launcherType);
                }
                else
                {
                    Terraria.ID.AmmoID.Sets.SpecificLauncherAmmoProjectileMatches[launcherType] = previous;
                }
            }
        }

        private static void CombatAimProjectileProfileCarriesEffectiveExtraUpdates()
        {
            var profile = BuildCombatAimProjectileProfile(
                new FakePlayer(),
                new FakeItem
                {
                    type = 10005,
                    stack = 1,
                    Name = "Profile Test Fast Bow",
                    damage = 9,
                    shoot = Terraria.ID.ProjectileID.WoodenArrowFriendly,
                    shootSpeed = 7f,
                    useAmmo = Terraria.ID.AmmoID.Arrow,
                    ranged = true
                },
                new FakeItem
                {
                    type = 10006,
                    stack = 99,
                    Name = "Fast Test Arrow",
                    ammo = Terraria.ID.AmmoID.Arrow,
                    shoot = 9001,
                    shootSpeed = 3f
                });

            if (profile.ProjectileExtraUpdates != 2 || profile.EffectiveUpdatesPerTick != 3)
            {
                throw new InvalidOperationException("Expected projectile defaults extraUpdates=2 to become 3 effective updates per tick.");
            }

            AssertNear(profile.EffectiveProjectileSpeed, 30d, "extra update effective speed");
        }


    }
}
