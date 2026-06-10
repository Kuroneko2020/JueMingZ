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
        private static void FlailDiagnosticsPublisherKeepsMetadataFieldNames()
        {
            var diagnostics = new CombatAimFlailDiagnostics
            {
                ItemType = 1058,
                ItemName = "The Meatball",
                Eligible = true,
                Reason = "eligible:flailAiStyle15",
                Active = true,
                State = FlailControlStates.ReleaseToTarget,
                ProjectileWhoAmI = 9,
                ProjectileType = 1058,
                ProjectileAiStyle = 15,
                ProjectileAi0 = 1f,
                ProjectileVelocityX = 3.5f,
                ProjectileVelocityY = -2.25f,
                ProjectileIdentity = 42,
                HitDetected = true,
                CollisionDetected = true,
                LocalNpcImmunityChanged = true,
                TileCollisionDetected = true,
                AttackPulse = false,
                AttackRelease = true,
                AttackSuppressed = true,
                AttackRestored = false,
                BlockedReason = "physicalRelease",
                InputMode = "controlledUseItemRelease",
                InputPhase = FlailControlStates.ReleaseToTarget,
                TakeoverScope = "ItemCheck",
                StuckRecovery = "none",
                ReleaseSuppressedPhysicalInput = true,
                PhysicalUseItemHeld = false,
                PhysicalReleasePending = true,
                PulseReason = string.Empty,
                CachedReleaseAim = true,
                CachedReleaseAimAgeTicks = 3,
                CachedReleaseAimReason = "available"
            };

            var json = CombatAimFlailControlService.BuildFlailDiagnosticsJsonForTesting(diagnostics);

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
        }

        private static void FlailDiagnosticsPublisherSuppressesDuplicateInactiveSnapshots()
        {
            CombatAimFlailControlService.ResetForTesting();
            CombatAimFlailControlService.SetLastDiagnosticsForTesting(new CombatAimFlailDiagnostics
            {
                ItemType = 0,
                ItemName = "first-inactive",
                Eligible = false,
                Reason = "notEvaluated",
                Active = false,
                State = FlailControlStates.Idle,
                BlockedReason = "noActiveFlailUse",
                InputMode = "observe",
                TakeoverScope = "none",
                StuckRecovery = "none"
            });

            CombatAimFlailControlService.SetLastDiagnosticsForTesting(new CombatAimFlailDiagnostics
            {
                ItemType = 0,
                ItemName = "duplicate-should-not-replace",
                Eligible = false,
                Reason = "notEvaluated",
                Active = false,
                State = FlailControlStates.Idle,
                BlockedReason = "noActiveFlailUse",
                InputMode = "observe",
                TakeoverScope = "none",
                StuckRecovery = "none"
            });

            var last = CombatAimFlailControlService.GetDecisionDiagnostics(null);
            if (last == null ||
                !string.Equals(last.ItemName, "first-inactive", StringComparison.Ordinal) ||
                !string.Equals(last.BlockedReason, "noActiveFlailUse", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected duplicate inactive flail diagnostics to keep the previous snapshot.");
            }

            CombatAimFlailControlService.SetLastDiagnosticsForTesting(new CombatAimFlailDiagnostics
            {
                ItemType = 0,
                ItemName = "changed-reason",
                Eligible = false,
                Reason = "notEvaluated",
                Active = false,
                State = FlailControlStates.Disabled,
                BlockedReason = "gameMenu",
                InputMode = "observe",
                TakeoverScope = "none",
                StuckRecovery = "none"
            });

            last = CombatAimFlailControlService.GetDecisionDiagnostics(null);
            if (last == null ||
                !string.Equals(last.ItemName, "changed-reason", StringComparison.Ordinal) ||
                !string.Equals(last.BlockedReason, "gameMenu", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected changed inactive flail diagnostics to replace the last snapshot.");
            }
        }

        private static void CombatAimWeaponFamilyResolverClassifiesRequestedFamilies()
        {
            AssertWeaponFamily(
                "Pigron flail",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 5526,
                    stack = 1,
                    Name = "Flairon",
                    damage = 66,
                    shoot = 1058,
                    shootSpeed = 12f,
                    melee = true,
                    channel = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 1058, ProjectileAiStyle = 15 },
                CombatAimWeaponFamilies.FlailAiStyle15);

            AssertWeaponFamily(
                "Xenopopper",
                BuildCombatAimWeaponProfile(new FakeItem
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
                }),
                new CombatAimBallisticSolution { ProjectileType = 14, ProjectileAiStyle = 1, AmmoProjectileType = 14 },
                CombatAimWeaponFamilies.SpecialCursorSpawnBurst);

            AssertWeaponFamily(
                "VortexBeater",
                BuildCombatAimWeaponProfile(new FakeItem
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
                }),
                new CombatAimBallisticSolution { ProjectileType = 14, ProjectileAiStyle = 1, AmmoProjectileType = 14 },
                CombatAimWeaponFamilies.SpecialDualProjectile);

            AssertWeaponFamily(
                "Onyx Blaster",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 3788,
                    stack = 1,
                    Name = "Onyx Blaster",
                    damage = 28,
                    shoot = 661,
                    shootSpeed = 14f,
                    useAmmo = 97,
                    ranged = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 14, ProjectileAiStyle = 1, AmmoProjectileType = 14 },
                CombatAimWeaponFamilies.SpecialSpreadOrMultiShot);

            AssertWeaponFamily(
                "Spear",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 280,
                    stack = 1,
                    Name = "Spear",
                    damage = 8,
                    shoot = 49,
                    shootSpeed = 5f,
                    melee = true,
                    noMelee = true,
                    noUseGraphic = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 49, ProjectileAiStyle = 19 },
                CombatAimWeaponFamilies.SpearAiStyle19);

            AssertWeaponFamily(
                "Wooden Boomerang",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 55,
                    stack = 1,
                    Name = "Wooden Boomerang",
                    damage = 8,
                    shoot = 3,
                    shootSpeed = 10f,
                    melee = true,
                    noUseGraphic = true,
                    useStyle = 1
                }),
                new CombatAimBallisticSolution { ProjectileType = 3, ProjectileAiStyle = 3 },
                CombatAimWeaponFamilies.ReturningBoomerangAiStyle3);

            AssertWeaponFamily(
                "Ordinary Gun",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 100,
                    stack = 1,
                    Name = "Ordinary Gun",
                    damage = 20,
                    shoot = 14,
                    shootSpeed = 10f,
                    useAmmo = 97,
                    ranged = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 14, ProjectileAiStyle = 1, AmmoProjectileType = 14 },
                CombatAimWeaponFamilies.DirectProjectile);

            AssertWeaponFamily(
                "Ordinary Bow",
                BuildCombatAimWeaponProfile(new FakeItem
                {
                    type = 99,
                    stack = 1,
                    Name = "Ordinary Bow",
                    damage = 17,
                    shoot = 1,
                    shootSpeed = 8.5f,
                    useAmmo = 1,
                    ranged = true,
                    useStyle = 5
                }),
                new CombatAimBallisticSolution { ProjectileType = 1, ProjectileAiStyle = 1, AmmoProjectileType = 1 },
                CombatAimWeaponFamilies.DirectProjectile);
        }

        private static void CombatAimWeaponFamilyDiagnosticsEmitsMetadataFields()
        {
            var decision = BuildCombatAimDiagnosticDecision();
            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"DirectProjectile\"");
            AssertContains(json, "\"weaponFamilyReason\":\"projectileSemantics:shoot=1;useAmmo=1\"");

            var player = new FakePlayer();
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
            decision.ItemType = item.type;
            decision.ItemStack = item.stack;
            decision.ItemName = item.Name;
            decision.Damage = item.damage;
            decision.Shoot = item.shoot;
            decision.UseAmmo = item.useAmmo;
            decision.WeaponProfile = CombatAimWeaponProfile.Read(player, item);
            decision.BallisticSolution = new CombatAimBallisticSolution
            {
                Solved = true,
                Mode = "specialCursorSpawnBurst",
                ProjectileType = 14,
                ProjectileName = "Bullet",
                ProjectileAiStyle = 1,
                AmmoProjectileType = 14,
                WeaponShootProjectileType = 444,
                SpecialWeaponKind = "cursorSpawnBurst",
                SpecialWeaponName = "Xenopopper",
                SpecialWeaponRule = "cursorSpawnBubbleBullet",
                SpecialWeaponUsesWeaponShoot = true,
                SpecialWeaponUsesAmmoShoot = true
            };

            json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialCursorSpawnBurst\"");
            AssertContains(json, "\"weaponFamilyReason\":\"specialWeaponRuleKind=cursorSpawnBurst\"");
            AssertContains(json, "\"projectileAiStyle\":1");
            AssertContains(json, "\"specialWeaponRuleKind\":\"cursorSpawnBurst\"");
        }

        private static void CombatAimSkipReasonsNormalizeToStableStrings()
        {
            AssertCombatAimSkipReason("radiusOff", "disabled");
            AssertCombatAimSkipReason("itemUseBridgePending", "bridgeBusy");
            AssertCombatAimSkipReason("playerMouseInterface", "mouseCaptured");
            AssertCombatAimSkipReason("placementItem", "placementItem");
            AssertCombatAimSkipReason("toolOrFishingItem", "toolOrFishingRod");
            AssertCombatAimSkipReason("sentryPlacementWeapon", "sentryOrSummonPlacement");
            AssertCombatAimSkipReason("summonPlacementWeapon", "sentryOrSummonPlacement");
            AssertCombatAimSkipReason("notProjectileAmmoOrMelee", "noProjectile");
            AssertCombatAimSkipReason("targetUnavailable:NoTarget:noCandidates", "noTarget");
            AssertCombatAimSkipReason("targetUnavailable:NoTarget:blockedByLineOfSight", "lineOfSightBlocked");
            AssertCombatAimSkipReason("notEligible:notChannelProjectile", "persistentCursorNotEligible");
            AssertCombatAimSkipReason("releaseHoldTargetInvalid:targetDummyDisabled", "noTarget");
        }

        private static void PersistentCursorPolicyRejectsOrdinaryProjectileWeapons()
        {
            var profile = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 100,
                stack = 1,
                Name = "Ordinary Gun",
                damage = 20,
                shoot = 14,
                shootSpeed = 10f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            });

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(profile, false, false, string.Empty);
            AssertPersistentCursorEligibility(eligibility, false, "notEligible:notChannelProjectile", "none");
        }

        private static void PersistentCursorPolicyAllowsChannelProjectileScopedOnly()
        {
            var profile = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 101,
                stack = 1,
                Name = "Channel Projectile",
                damage = 40,
                shoot = 633,
                shootSpeed = 8f,
                magic = true,
                channel = true,
                useStyle = 5
            });

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(profile, false, false, string.Empty);
            AssertPersistentCursorEligibility(eligibility, true, "eligible:projectileAiScoped", "channelProjectileWeapon");
            if (!eligibility.VisibleCursorHijackRisk ||
                !eligibility.VisibleCursorHijackRiskMitigated ||
                !string.Equals(eligibility.CursorOwnershipMode, "projectileAiScoped", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected channel projectile weapons to mitigate visible cursor risk through Projectile.AI scoped ownership.");
            }

            if (CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.AI099, eligibility) ||
                CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.MainUpdateFallback, eligibility))
            {
                throw new InvalidOperationException("Expected channel projectile weapons to block Main.Update fallback.");
            }

            if (!CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.ProjectileAI, eligibility) ||
                CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.AI099, eligibility))
            {
                throw new InvalidOperationException("Expected channel projectile weapons to allow only generic Projectile.AI scoped override.");
            }
        }

        private static void PersistentCursorPolicyAllowsSpecialProjectileScopedOnly()
        {
            var profile = BuildCombatAimWeaponProfile(new FakeItem
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
            });

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(profile, false, false, string.Empty);
            AssertPersistentCursorEligibility(eligibility, true, "eligible:specialProjectileAiScoped", "specialProjectileWeapon");
            if (!eligibility.VisibleCursorHijackRisk ||
                !eligibility.VisibleCursorHijackRiskMitigated ||
                !eligibility.AllowsAnimationScopedWithoutHeld ||
                !string.Equals(eligibility.CursorOwnershipMode, "projectileAiScoped", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected special projectile weapons to use scoped projectile ownership and allow animation-scoped followup.");
            }

            if (CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.AI099, eligibility) ||
                CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.MainUpdateFallback, eligibility))
            {
                throw new InvalidOperationException("Expected special projectile weapons to block Main.Update fallback.");
            }

            if (!CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.ProjectileAI, eligibility) ||
                CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.AI099, eligibility))
            {
                throw new InvalidOperationException("Expected special projectile weapons to allow only generic Projectile.AI scoped override.");
            }

            var vortex = BuildCombatAimWeaponProfile(new FakeItem
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
            });
            var vortexEligibility = CombatAimPersistentCursorPolicy.Evaluate(vortex, false, false, string.Empty);
            AssertPersistentCursorEligibility(vortexEligibility, true, "eligible:specialProjectileAiScoped", "specialProjectileWeapon");

            var ordinaryShotgun = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 534,
                stack = 1,
                Name = "Shotgun",
                damage = 24,
                shoot = 14,
                shootSpeed = 9f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            });
            var ordinaryEligibility = CombatAimPersistentCursorPolicy.Evaluate(ordinaryShotgun, false, false, string.Empty);
            AssertPersistentCursorEligibility(ordinaryEligibility, false, "notEligible:notChannelProjectile", "none");

            var boomstick = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 964,
                stack = 1,
                Name = "Boomstick",
                damage = 14,
                shoot = 14,
                shootSpeed = 5.35f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            });
            var boomstickEligibility = CombatAimPersistentCursorPolicy.Evaluate(boomstick, false, false, string.Empty);
            AssertPersistentCursorEligibility(boomstickEligibility, false, "notEligible:notChannelProjectile", "none");

            CombatAimSpecialWeaponRule boomstickRule;
            if (!CombatAimSpecialWeaponRuleResolver.TryResolve(boomstick, out boomstickRule) ||
                !string.Equals(boomstickRule.Kind, "spreadMultiShot", StringComparison.Ordinal) ||
                boomstickRule.AllowsProjectileAiScoped)
            {
                throw new InvalidOperationException("Expected Boomstick to resolve only as an unscoped spread rule.");
            }
        }

        private static void PersistentCursorPolicyRejectsPlacementSummonsAndSentries()
        {
            var summon = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 102,
                stack = 1,
                Name = "Summon Placement",
                damage = 20,
                shoot = 1,
                buffType = 1,
                summon = true,
                channel = true
            });
            AssertPersistentCursorEligibility(
                CombatAimPersistentCursorPolicy.Evaluate(summon, false, false, string.Empty),
                false,
                "notEligible:placementOrSummon",
                "none");

            var sentry = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 103,
                stack = 1,
                Name = "Sentry Placement",
                damage = 20,
                shoot = 1,
                sentry = true,
                channel = true
            });
            AssertPersistentCursorEligibility(
                CombatAimPersistentCursorPolicy.Evaluate(sentry, false, false, string.Empty),
                false,
                "notEligible:placementOrSummon",
                "none");
        }

        private static void PersistentCursorPolicyPreservesYoyoEligibility()
        {
            var profile = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 104,
                stack = 1,
                Name = "Yoyo",
                damage = 30,
                shoot = 99,
                melee = true,
                channel = true
            });

            var eligibility = CombatAimPersistentCursorPolicy.Evaluate(profile, true, true, "activeYoyoProjectile");
            AssertPersistentCursorEligibility(eligibility, true, "yoyo", "yoyo");
            if (CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.AI099, eligibility))
            {
                throw new InvalidOperationException("Expected yoyo to keep projectile hook ownership when AI099 is installed.");
            }

            if (!CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.AI099, eligibility) ||
                !CombatAimPersistentCursorPolicy.AllowsProjectileAiScoped(PersistentCursorHooks.ProjectileAI, eligibility))
            {
                throw new InvalidOperationException("Expected yoyo to allow projectile-scoped hooks.");
            }

            if (!CombatAimPersistentCursorPolicy.AllowsMainUpdateFallback(PersistentCursorHooks.MainUpdateFallback, eligibility))
            {
                throw new InvalidOperationException("Expected yoyo fallback to remain available when projectile hook is unavailable.");
            }
        }

        private static void ProjectileCursorMatchAcceptsOnlyLocalChannelProjectile()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = 101,
                stack = 1,
                Name = "Channel Projectile",
                damage = 40,
                shoot = 633,
                shootSpeed = 8f,
                magic = true,
                channel = true,
                useStyle = 5
            };
            var profile = CombatAimWeaponProfile.Read(player, item);
            var solution = new CombatAimBallisticSolution { ProjectileType = 633 };

            var match = CombatAimProjectileCursorCompat.MatchChannelProjectile(
                new FakeProjectile { whoAmI = 10, type = 633, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (!match.Matches || !string.Equals(match.Reason, "matched:channelProjectileWeapon", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected local owned channel projectile to match, got " + match.Reason);
            }

            AssertProjectileCursorRejected(
                new FakeProjectile { whoAmI = 11, type = 633, owner = 7, active = true, friendly = true, hostile = true },
                player,
                profile,
                solution,
                "notEligible:hostileProjectile");
            AssertProjectileCursorRejected(
                new FakeProjectile { whoAmI = 12, type = 633, owner = 8, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:notLocalOwnedProjectile");
            AssertProjectileCursorRejected(
                new FakeProjectile { whoAmI = 13, type = 999, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:projectileMismatch");

            var ordinaryGun = CombatAimWeaponProfile.Read(
                player,
                new FakeItem
                {
                    type = 102,
                    stack = 1,
                    Name = "Ordinary Gun",
                    damage = 20,
                    shoot = 14,
                    shootSpeed = 10f,
                    useAmmo = 97,
                    ranged = true,
                    useStyle = 5
                });
            AssertProjectileCursorRejected(
                new FakeProjectile { whoAmI = 14, type = 14, owner = 7, active = true, friendly = true },
                player,
                ordinaryGun,
                new CombatAimBallisticSolution { ProjectileType = 14 },
                "notEligible:notChannelProjectileWeapon");
        }

        private static void ProjectileCursorMatchAcceptsOnlyLocalSpecialWeaponProjectile()
        {
            var player = new FakePlayer { whoAmI = 7 };
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
            var profile = CombatAimWeaponProfile.Read(player, item);
            var solution = new CombatAimBallisticSolution
            {
                ProjectileType = 14,
                AmmoProjectileType = 14,
                WeaponShootProjectileType = 444,
                SecondaryProjectileType = 444,
                SpecialWeaponUsesWeaponShoot = true,
                SpecialWeaponUsesAmmoShoot = true
            };

            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 20, type = 444, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (!match.Matches || !string.Equals(match.Reason, "matched:specialWeaponProjectile", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected local owned special weapon projectile to match, got " + match.Reason);
            }

            AssertSpecialProjectileCursorRejected(
                new FakeProjectile { whoAmI = 21, type = 444, owner = 8, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:notLocalOwnedProjectile");
            AssertSpecialProjectileCursorRejected(
                new FakeProjectile { whoAmI = 22, type = 14, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:projectileMismatch");

            var vortex = CombatAimWeaponProfile.Read(
                player,
                new FakeItem
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
                });
            var vortexSolution = new CombatAimBallisticSolution
            {
                ProjectileType = 14,
                AmmoProjectileType = 14,
                WeaponShootProjectileType = 615,
                SecondaryProjectileType = 615,
                SpecialWeaponUsesWeaponShoot = true,
                SpecialWeaponUsesAmmoShoot = true
            };
            var vortexController = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 25, type = 615, owner = 7, active = true, friendly = false },
                player,
                vortex,
                vortexSolution);
            if (!vortexController.Matches)
            {
                throw new InvalidOperationException("Expected VortexBeater non-friendly controller projectile to match special scoped rule, got " + vortexController.Reason);
            }

            var vortexRocket = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 24, type = 616, owner = 7, active = true, friendly = true },
                player,
                vortex,
                vortexSolution);
            if (!vortexRocket.Matches)
            {
                throw new InvalidOperationException("Expected VortexBeater assist rocket projectile to match special scoped rule, got " + vortexRocket.Reason);
            }

            var ordinaryGun = CombatAimWeaponProfile.Read(
                player,
                new FakeItem
                {
                    type = 100,
                    stack = 1,
                    Name = "Ordinary Gun",
                    damage = 20,
                    shoot = 14,
                    shootSpeed = 10f,
                    useAmmo = 97,
                    ranged = true,
                    useStyle = 5
                });
            AssertSpecialProjectileCursorRejected(
                new FakeProjectile { whoAmI = 23, type = 14, owner = 7, active = true, friendly = true },
                player,
                ordinaryGun,
                new CombatAimBallisticSolution { ProjectileType = 14 },
                "notEligible:notSpecialProjectileWeapon");
        }

        private static void ProjectileCursorMatchAcceptsOnlyLocalFlailReleaseProjectile()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = 5526,
                stack = 1,
                Name = "Flairon",
                damage = 66,
                shoot = 1058,
                shootSpeed = 12f,
                melee = true,
                channel = true,
                useStyle = 5
            };
            var profile = CombatAimWeaponProfile.Read(player, item);
            var solution = new CombatAimBallisticSolution
            {
                ProjectileType = 1058,
                ProjectileAiStyle = 15
            };

            var match = CombatAimProjectileCursorCompat.MatchFlailProjectile(
                new FakeProjectile { whoAmI = 31, type = 1058, aiStyle = 15, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (!match.Matches || !string.Equals(match.Reason, "matched:flailAiStyle15Release", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected local owned aiStyle 15 flail projectile to match, got " + match.Reason);
            }

            AssertFlailProjectileCursorRejected(
                new FakeProjectile { whoAmI = 32, type = 1058, aiStyle = 15, owner = 8, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:notLocalOwnedProjectile");
            AssertFlailProjectileCursorRejected(
                new FakeProjectile { whoAmI = 33, type = 1058, aiStyle = 99, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution,
                "notEligible:notFlailAiStyle15");
            AssertFlailProjectileCursorRejected(
                new FakeProjectile { whoAmI = 34, type = 633, aiStyle = 75, owner = 7, active = true, friendly = true },
                player,
                CombatAimWeaponProfile.Read(
                    player,
                    new FakeItem
                    {
                        type = 4956,
                        stack = 1,
                        Name = "Channel Beam",
                        damage = 120,
                        shoot = 633,
                        shootSpeed = 1f,
                        magic = true,
                        channel = true,
                        useStyle = 5
                    }),
                new CombatAimBallisticSolution { ProjectileType = 633, ProjectileAiStyle = 75 },
                "notEligible:notFlailAiStyle15");
        }

        private static void AssertProjectileCursorRejected(
            FakeProjectile projectile,
            FakePlayer player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            string expectedReason)
        {
            var match = CombatAimProjectileCursorCompat.MatchChannelProjectile(projectile, player, profile, solution);
            if (match.Matches || !string.Equals(match.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected projectile cursor reject " + expectedReason + ", got " + match.Reason);
            }
        }

        private static void AssertSpecialProjectileCursorRejected(
            FakeProjectile projectile,
            FakePlayer player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            string expectedReason)
        {
            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(projectile, player, profile, solution);
            if (match.Matches || !string.Equals(match.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected special projectile cursor reject " + expectedReason + ", got " + match.Reason);
            }
        }

        private static void AssertFlailProjectileCursorRejected(
            FakeProjectile projectile,
            FakePlayer player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            string expectedReason)
        {
            var match = CombatAimProjectileCursorCompat.MatchFlailProjectile(projectile, player, profile, solution);
            if (match.Matches || !string.Equals(match.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected flail projectile cursor reject " + expectedReason + ", got " + match.Reason);
            }
        }

        private static void CombatAimScopedCursorDiagnosticsKeepsOwnershipFields()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = 101,
                stack = 1,
                Name = "Channel Projectile",
                damage = 40,
                shoot = 633,
                shootSpeed = 8f,
                magic = true,
                channel = true,
                useStyle = 5
            };

            var decision = BuildCombatAimDiagnosticDecision();
            decision.AimApplyMode = CombatAimApplyModes.PersistentCursor;
            decision.PersistentCursorActive = true;
            decision.PersistentHook = PersistentCursorHooks.ProjectileAI;
            decision.PersistentCursorReason = "channelProjectileWeapon";
            decision.ItemType = item.type;
            decision.ItemStack = item.stack;
            decision.ItemName = item.Name;
            decision.Damage = item.damage;
            decision.Shoot = item.shoot;
            decision.UseAmmo = item.useAmmo;
            decision.WeaponProfile = CombatAimWeaponProfile.Read(player, item);
            decision.BallisticSolution.ProjectileType = 633;
            decision.AimWorldX = 320f;
            decision.AimWorldY = 360f;
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(
                decision,
                CombatAimProjectileCursorMatch.Result(true, "matched:channelProjectileWeapon", 10, 633, 7, 75),
                true,
                true,
                true);

            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"persistentHook\":\"ProjectileAI\"");
            AssertContains(json, "\"persistentCursorEligibility\":true");
            AssertContains(json, "\"persistentCursorEligibilityReason\":\"eligible:projectileAiScoped\"");
            AssertContains(json, "\"persistentCursorMainUpdateFallbackAllowed\":false");
            AssertContains(json, "\"persistentCursorProjectileAiScopedAllowed\":true");
            AssertContains(json, "\"persistentCursorScopedOverride\":true");
            AssertContains(json, "\"persistentCursorTargetSet\":true");
            AssertContains(json, "\"projectileCursorMatch\":true");
            AssertContains(json, "\"projectileCursorMatchReason\":\"matched:channelProjectileWeapon\"");
            AssertContains(json, "\"projectileCursorProjectileType\":633");
            AssertContains(json, "\"projectileCursorOwner\":7");
            AssertContains(json, "\"visibleCursorHijackRiskMitigated\":true");
            AssertContains(json, "\"cursorOwnershipMode\":\"projectileAiScoped\"");
        }

        private static void FlailPolicyOnlyAcceptsNonYoyoChannelAiStyle15()
        {
            var flail = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 5526,
                stack = 1,
                Name = "Flairon",
                damage = 66,
                shoot = 1058,
                shootSpeed = 12f,
                melee = true,
                channel = true,
                useStyle = 5
            });
            var eligible = CombatAimFlailPolicy.Evaluate(flail, 15, false);
            if (!eligible.Eligible || !string.Equals(eligible.Reason, "eligible:flailAiStyle15", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected channel aiStyle 15 flail to be eligible, got " + eligible.Reason);
            }

            AssertFlailRejected(flail, 15, true, "notFlail:yoyo");
            AssertFlailRejected(flail, 75, false, "notFlail:notFlailAiStyle15");

            var channelProjectile = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 4956,
                stack = 1,
                Name = "Channel Beam",
                damage = 120,
                shoot = 633,
                shootSpeed = 1f,
                magic = true,
                channel = true,
                useStyle = 5
            });
            AssertFlailRejected(channelProjectile, 75, false, "notFlail:notFlailAiStyle15");

            var gun = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 1553,
                stack = 1,
                Name = "SDMG",
                damage = 85,
                shoot = 10,
                shootSpeed = 13f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            });
            AssertFlailRejected(gun, 1, false, "notFlail:notChannel");

            var sentry = BuildCombatAimWeaponProfile(new FakeItem
            {
                type = 3826,
                stack = 1,
                Name = "Ballista Staff",
                damage = 27,
                shoot = 679,
                sentry = true
            });
            AssertFlailRejected(sentry, 15, false, "notFlail:placementOrSummon");
        }

        private static void AssertFlailRejected(CombatAimWeaponProfile profile, int projectileAiStyle, bool isYoyo, string expectedReason)
        {
            var eligibility = CombatAimFlailPolicy.Evaluate(profile, projectileAiStyle, isYoyo);
            if (eligibility.Eligible || !string.Equals(eligibility.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected flail rejection " + expectedReason + ", got " + eligibility.Reason);
            }
        }


    }
}
