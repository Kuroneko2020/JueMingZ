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
        private static void SpecialProjectileRulesDistinguishWeaponAndAmmoProjectiles()
        {
            var xenopopper = ResolveSpreadRuleForTesting(2797);
            AssertStringEquals(xenopopper.Kind, "cursorSpawnBurst", "Xenopopper special kind");
            AssertStringEquals(xenopopper.Name, "Xenopopper", "Xenopopper special name");
            AssertStringEquals(xenopopper.Rule, "cursorSpawnBubbleBullet", "Xenopopper special rule");
            if (!xenopopper.UsesCursorTarget || !xenopopper.UsesWeaponShoot || !xenopopper.UsesAmmoShoot)
            {
                throw new InvalidOperationException("Expected Xenopopper to retain cursor, weapon, and ammo projectile roles.");
            }

            var vortex = ResolveSpreadRuleForTesting(3475);
            AssertStringEquals(vortex.Kind, "dualProjectileSpread", "Vortex special kind");
            AssertStringEquals(vortex.Name, "VortexBeater", "Vortex special name");
            if (!vortex.UsesWeaponShoot || !vortex.UsesAmmoShoot || !vortex.AllowsProjectileAiScoped)
            {
                throw new InvalidOperationException("Expected Vortex Beater assist projectiles to stay scoped and role-aware.");
            }

            var onyx = ResolveSpreadRuleForTesting(3788);
            AssertStringEquals(onyx.Kind, "spreadMultiShot", "Onyx special kind");
            AssertStringEquals(onyx.Name, "OnyxBlaster", "Onyx special name");
            if (!onyx.UsesWeaponShoot || !onyx.UsesAmmoShoot || onyx.AllowsProjectileAiScoped)
            {
                throw new InvalidOperationException("Expected Onyx Blaster to use spread role metadata without scoped cursor ownership.");
            }

            var player = new FakePlayer();
            var item = new FakeItem
            {
                type = 2797,
                stack = 1,
                Name = "Xenopopper",
                damage = 45,
                shoot = 444,
                shootSpeed = 10f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            player.inventory[0] = item;
            var profile = CombatAimWeaponProfile.Read(player, item);
            player.inventory[54] = new FakeItem
            {
                type = 97,
                stack = 99,
                Name = "Bullet",
                ammo = 97,
                shoot = 14,
                shootSpeed = 4f
            };
            var projectileProfile = CombatAimProjectileProfileResolver.Resolve(player, profile);
            var solution = new CombatAimBallisticSolution
            {
                ProjectileType = 14,
                ProjectileName = "Bullet",
                AmmoProjectileType = 14,
                AmmoItemType = 97,
                AmmoItemName = "Bullet"
            };

            InvokePrivateStatic("ApplyProjectileRoleMetadata", solution, profile, projectileProfile);
            if (solution.WeaponShootProjectileType != 444 ||
                !string.Equals(solution.ResolvedProjectileRole, "ammoProjectile", StringComparison.Ordinal) ||
                solution.PrimaryProjectileType != 14 ||
                !string.Equals(solution.PrimaryProjectileRole, "ammoPrimary", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected ammo projectile to remain primary while weapon.shoot is retained.");
            }

            InvokePrivateStatic("ApplySpecialMetadata", solution, xenopopper);
            if (!solution.SpecialWeaponUsesWeaponShoot ||
                !solution.SpecialWeaponUsesAmmoShoot ||
                solution.SecondaryProjectileType != 444 ||
                !string.Equals(solution.SecondaryProjectileRole, "weaponAssist", StringComparison.Ordinal) ||
                !solution.SpecialCursorTarget)
            {
                throw new InvalidOperationException("Expected Xenopopper special metadata to retain weapon and ammo projectile roles.");
            }

            CombatAimSpecialWeaponRule resolvedRule;
            if (!CombatAimSpecialWeaponRuleResolver.TryResolve(profile, out resolvedRule) ||
                !string.Equals(resolvedRule.Kind, "cursorSpawnBurst", StringComparison.Ordinal) ||
                !resolvedRule.UsesWeaponProjectile ||
                !resolvedRule.UsesAmmoProjectile)
            {
                throw new InvalidOperationException("Expected special weapon rule resolver to classify Xenopopper by rule kind.");
            }
        }

        private static void SpecialDualProjectileRejectsVortexAmmoBulletScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildVortexTailDecision(player, 100f, 120f);

            string role;
            if (CombatAimSpecialWeaponRuleResolver.TryResolveScopedProjectileRole(
                    14,
                    decision.WeaponProfile,
                    decision.BallisticSolution,
                    out role) ||
                !string.Equals(role, "ammoPrimary", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Vortex ammo bullet to resolve as ammoPrimary and stay out of special scoped cursor.");
            }

            var ammoBullet = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 30, type = 14, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (ammoBullet.Matches || !string.Equals(ammoBullet.Reason, "notEligible:projectileMismatch", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Vortex ammo bullet to be rejected, got " + ammoBullet.Reason);
            }
        }

        private static void SpecialDualProjectileMatchesVortexControllerAndRocketScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildVortexTailDecision(player, 180f, 220f);

            var controller = AssertSpecialWeaponAssistMatch(
                "Vortex weapon controller",
                new FakeProjectile { whoAmI = 31, type = 615, owner = 7, active = true, friendly = false },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);

            AssertSpecialWeaponAssistMatch(
                "Vortex rocket assist",
                new FakeProjectile { whoAmI = 32, type = 616, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);

            decision.PersistentHook = PersistentCursorHooks.ProjectileAI;
            decision.PersistentCursorActive = true;
            decision.PersistentCursorReason = "specialProjectileWeapon";
            decision.SpecialProjectileTailActive = true;
            decision.SpecialProjectileTailRecomputedAim = true;
            decision.SpecialProjectileTailExpiredReason = "none";
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(decision, controller, true, true, true);

            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialDualProjectile\"");
            AssertContains(json, "\"specialWeaponRuleKind\":\"dualProjectileSpread\"");
            AssertContains(json, "\"primaryProjectileRole\":\"ammoPrimary\"");
            AssertContains(json, "\"secondaryProjectileRole\":\"weaponAssist\"");
            AssertContains(json, "\"projectileCursorProjectileType\":615");
            AssertContains(json, "\"projectileCursorMatchReason\":\"matched:specialWeaponProjectile\"");
            AssertContains(json, "\"specialProjectileTailRecomputedAim\":true");
        }

        private static void OnyxBlasterStaysItemCheckSpreadPathWithoutSpecialScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
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
            };
            player.inventory[0] = item;
            var profile = CombatAimWeaponProfile.Read(player, item);
            var solution = new CombatAimBallisticSolution
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
                WeaponShootProjectileType = 661,
                WeaponShootProjectileName = "Black Bolt",
                SecondaryProjectileType = 661,
                SecondaryProjectileName = "Black Bolt",
                SecondaryProjectileRole = "weaponAssist",
                SpecialWeaponKind = "spreadMultiShot",
                SpecialWeaponName = "OnyxBlaster",
                SpecialWeaponRule = "spreadBulletWithDarkBolt",
                SpecialWeaponUsesWeaponShoot = true,
                SpecialWeaponUsesAmmoShoot = true
            };

            if (CombatAimPersistentCursorPolicy.IsSpecialProjectileScopedWeapon(profile))
            {
                throw new InvalidOperationException("Expected Onyx Blaster to stay out of specialProjectileWeapon policy.");
            }

            CombatAimSpecialWeaponRule scopedRule;
            if (!CombatAimSpecialWeaponRuleResolver.TryResolve(profile, out scopedRule) ||
                !string.Equals(scopedRule.Kind, "spreadMultiShot", StringComparison.Ordinal) ||
                scopedRule.AllowsProjectileAiScoped)
            {
                throw new InvalidOperationException("Expected Onyx Blaster to resolve as unscoped spread rule.");
            }

            string role;
            if (CombatAimSpecialWeaponRuleResolver.TryResolveScopedProjectileRole(
                    661,
                    profile,
                    solution,
                    out role))
            {
                throw new InvalidOperationException("Expected Onyx dark bolt to stay in ItemCheck spread path, got scoped role " + role);
            }

            var darkBolt = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 33, type = 661, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (darkBolt.Matches || !string.Equals(darkBolt.Reason, "notEligible:notSpecialProjectileWeapon", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Onyx dark bolt to be rejected by special scoped cursor, got " + darkBolt.Reason);
            }

            var ammoBullet = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 34, type = 14, owner = 7, active = true, friendly = true },
                player,
                profile,
                solution);
            if (ammoBullet.Matches)
            {
                throw new InvalidOperationException("Expected Onyx Blaster ordinary ammo bullet to stay out of special scoped cursor.");
            }

            var decision = new CombatAimItemCheckDecision
            {
                Enabled = true,
                AimApplyMode = CombatAimApplyModes.PersistentCursor,
                PersistentHook = PersistentCursorHooks.ProjectileAI,
                PersistentCursorActive = true,
                PersistentCursorReason = "specialProjectileWeapon",
                ItemType = item.type,
                ItemName = item.Name,
                ItemStack = item.stack,
                Damage = item.damage,
                Shoot = item.shoot,
                UseAmmo = item.useAmmo,
                WeaponProfile = profile,
                AimWorldX = 240f,
                AimWorldY = 260f,
                BallisticSolution = solution
            };
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(decision, darkBolt, false, false, true);
            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"specialWeaponRuleKind\":\"spreadMultiShot\"");
            AssertContains(json, "\"primaryProjectileRole\":\"ammoPrimary\"");
            AssertContains(json, "\"secondaryProjectileRole\":\"weaponAssist\"");
            AssertContains(json, "\"projectileCursorMatchReason\":\"notEligible:notSpecialProjectileWeapon\"");
            if (json.IndexOf("\"weaponFamily\":\"SpecialDualProjectile\"", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("Expected Onyx Blaster not to report SpecialDualProjectile family.");
            }
        }

        private static void OrdinaryShotgunFamilyStaysOutOfSpecialProjectileScopedCursor()
        {
            AssertOrdinaryShotgunStaysOutOfSpecialProjectileScopedCursor("Shotgun", 534, 14);
            AssertOrdinaryShotgunStaysOutOfSpecialProjectileScopedCursor("Boomstick", 964, 14);
            AssertOrdinaryShotgunStaysOutOfSpecialProjectileScopedCursor("Quad-Barrel Shotgun", 4703, 14);
        }

        private static void SpecialProjectileTailKeepsScopedAimAfterUseWindow()
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
            var decision = new CombatAimItemCheckDecision
            {
                Enabled = true,
                AimApplyMode = CombatAimApplyModes.InstantItemCheck,
                ItemType = 2797,
                ItemName = "Xenopopper",
                ItemStack = 1,
                Damage = 45,
                Shoot = 444,
                UseAmmo = 97,
                WeaponProfile = profile,
                AimWorldX = 1234f,
                AimWorldY = 5678f,
                BallisticSolution = new CombatAimBallisticSolution
                {
                    ProjectileType = 14,
                    AmmoProjectileType = 14,
                    WeaponShootProjectileType = 444,
                    SecondaryProjectileType = 444,
                    SpecialWeaponUsesWeaponShoot = true,
                    SpecialWeaponUsesAmmoShoot = true
                }
            };

            if (!CombatAimPersistentCursorService.RememberSpecialProjectileTail(decision))
            {
                throw new InvalidOperationException("Expected special projectile tail to be remembered.");
            }

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryGetSpecialProjectileTailDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryGetSpecialProjectileTailDecision reflection failed.");
            }

            var args = new object[] { 1L, null, null };
            var ok = (bool)method.Invoke(null, args);
            var tail = args[2] as CombatAimItemCheckDecision;
            if (!ok || tail == null)
            {
                throw new InvalidOperationException("Expected special projectile tail decision after use window.");
            }

            if (tail.UseItemHeld ||
                !tail.UseItemReleased ||
                !string.Equals(tail.AimApplyMode, CombatAimApplyModes.PersistentCursor, StringComparison.Ordinal) ||
                !string.Equals(tail.PersistentCursorReason, "specialProjectileWeapon", StringComparison.Ordinal) ||
                Math.Abs(tail.AimWorldX - 1234f) > 0.001f ||
                Math.Abs(tail.AimWorldY - 5678f) > 0.001f ||
                tail.WeaponProfile == null ||
                tail.WeaponProfile.ItemType != 2797)
            {
                throw new InvalidOperationException("Special projectile tail did not preserve scoped aim metadata.");
            }
        }

        private static void SpecialProjectileTailMatchesXenopopperBubbleScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildXenopopperTailDecision(player, 1234f, 5678f);
            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 20, type = 444, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);

            if (!match.Matches || !string.Equals(match.Reason, "matched:specialWeaponProjectile", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Xenopopper bubble type 444 to enter special projectile scoped cursor, got " + match.Reason);
            }

            decision.PersistentHook = PersistentCursorHooks.ProjectileAI;
            decision.PersistentCursorActive = true;
            decision.PersistentCursorReason = "specialProjectileWeapon";
            decision.SpecialProjectileTailActive = true;
            decision.SpecialProjectileTailRecomputedAim = true;
            decision.SpecialProjectileTailExpiredReason = "none";
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(decision, match, true, true, true);

            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialCursorSpawnBurst\"");
            AssertContains(json, "\"specialWeaponRuleKind\":\"cursorSpawnBurst\"");
            AssertContains(json, "\"projectileCursorProjectileType\":444");
            AssertContains(json, "\"projectileCursorMatchReason\":\"matched:specialWeaponProjectile\"");
            AssertContains(json, "\"specialProjectileTailActive\":true");
            AssertContains(json, "\"specialProjectileTailRecomputedAim\":true");
            AssertContains(json, "\"specialProjectileTailExpiredReason\":\"none\"");
        }

        private static void SpecialProjectileTailUsesXenopopperBubbleProjectileKillScopedCursor()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildXenopopperTailDecision(player, 2234f, 6678f);
            var hookMethod = typeof(CombatAimPersistentCursorService).GetMethod("IsSpecialProjectileTailScopedHook", BindingFlags.Static | BindingFlags.NonPublic);
            if (hookMethod == null)
            {
                throw new InvalidOperationException("IsSpecialProjectileTailScopedHook reflection failed.");
            }

            if (!(bool)hookMethod.Invoke(null, new object[] { PersistentCursorHooks.ProjectileAI }) ||
                !(bool)hookMethod.Invoke(null, new object[] { PersistentCursorHooks.ProjectileKill }) ||
                (bool)hookMethod.Invoke(null, new object[] { PersistentCursorHooks.MainUpdateFallback }))
            {
                throw new InvalidOperationException("Expected special projectile tail to be scoped only to Projectile.AI and Projectile.Kill.");
            }

            var guardMethod = typeof(CombatAimPersistentCursorService).GetMethod("ShouldAttemptSpecialProjectileTailOverride", BindingFlags.Static | BindingFlags.NonPublic);
            if (guardMethod == null)
            {
                throw new InvalidOperationException("ShouldAttemptSpecialProjectileTailOverride reflection failed.");
            }

            var projectile = new FakeProjectile { whoAmI = 22, type = 444, owner = 7, active = true, friendly = true };
            if (!(bool)guardMethod.Invoke(null, new object[] { projectile, PersistentCursorHooks.ProjectileAI }) ||
                !(bool)guardMethod.Invoke(null, new object[] { projectile, PersistentCursorHooks.ProjectileKill }) ||
                (bool)guardMethod.Invoke(null, new object[] { projectile, PersistentCursorHooks.MainUpdateFallback }) ||
                (bool)guardMethod.Invoke(null, new object[] { null, PersistentCursorHooks.ProjectileKill }))
            {
                throw new InvalidOperationException("Expected special projectile tail begin guard to include Projectile.Kill and reject fallback/null scopes.");
            }

            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                projectile,
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (!match.Matches)
            {
                throw new InvalidOperationException("Expected Xenopopper bubble type 444 to match Projectile.Kill tail scope, got " + match.Reason);
            }

            decision.PersistentHook = PersistentCursorHooks.ProjectileKill;
            decision.PersistentCursorActive = true;
            decision.PersistentCursorReason = "specialProjectileWeapon";
            decision.SpecialProjectileTailActive = true;
            decision.SpecialProjectileTailRecomputedAim = true;
            decision.SpecialProjectileTailExpiredReason = "none";
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(decision, match, true, true, true);

            var json = BuildCombatAimDecisionJson(decision, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialCursorSpawnBurst\"");
            AssertContains(json, "\"persistentHook\":\"ProjectileKill\"");
            AssertContains(json, "\"persistentCursorMainUpdateFallbackAllowed\":false");
            AssertContains(json, "\"projectileCursorProjectileType\":444");
            AssertContains(json, "\"projectileCursorMatchReason\":\"matched:specialWeaponProjectile\"");
            AssertContains(json, "\"specialProjectileTailActive\":true");
            AssertContains(json, "\"specialProjectileTailRecomputedAim\":true");
        }

        private static void SpecialProjectileTailActiveBubbleRefreshesFixedTailWindow()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildXenopopperTailDecision(player, 1234f, 5678f);
            var rememberMethod = typeof(CombatAimPersistentCursorService).GetMethod(
                "RememberSpecialProjectileTail",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(CombatAimItemCheckDecision), typeof(long) },
                null);
            var getMethod = typeof(CombatAimPersistentCursorService).GetMethod("TryGetSpecialProjectileTailDecision", BindingFlags.Static | BindingFlags.NonPublic);
            var refreshMethod = typeof(CombatAimPersistentCursorService).GetMethod("RefreshSpecialProjectileTailLease", BindingFlags.Static | BindingFlags.NonPublic);
            if (rememberMethod == null || getMethod == null || refreshMethod == null)
            {
                throw new InvalidOperationException("Special projectile tail lease reflection failed.");
            }

            rememberMethod.Invoke(null, new object[] { decision, 100L });

            var beforeRefreshArgs = new object[] { 219L, null, null };
            var beforeRefreshOk = (bool)getMethod.Invoke(null, beforeRefreshArgs);
            if (!beforeRefreshOk)
            {
                throw new InvalidOperationException("Expected Xenopopper tail to still exist before the original fixed tail window ends.");
            }

            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 24, type = 444, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (!match.Matches)
            {
                throw new InvalidOperationException("Expected local active Xenopopper bubble to refresh the tail lease, got " + match.Reason);
            }

            refreshMethod.Invoke(null, new object[] { match, 219L });

            var refreshedArgs = new object[] { 339L, null, null };
            var refreshedOk = (bool)getMethod.Invoke(null, refreshedArgs);
            if (!refreshedOk)
            {
                throw new InvalidOperationException("Expected active Xenopopper bubble to keep special projectile tail alive beyond the original fixed window.");
            }

            var expiredArgs = new object[] { 340L, null, null };
            var expiredOk = (bool)getMethod.Invoke(null, expiredArgs);
            if (expiredOk)
            {
                throw new InvalidOperationException("Expected refreshed Xenopopper tail to expire after the active-projectile lease window.");
            }
        }

        private static void SpecialProjectileTailUsesRecomputedAimAfterTargetMoves()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var stale = BuildXenopopperTailDecision(player, 100f, 120f);
            var recomputed = BuildXenopopperTailDecision(player, 220f, 260f);
            recomputed.Selection.Target.CenterX = 220f;
            recomputed.Selection.Target.CenterY = 260f;

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryChooseSpecialProjectileTailAimDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryChooseSpecialProjectileTailAimDecision reflection failed.");
            }

            var args = new object[] { stale, recomputed, true, false, "targetMovedWithoutRecompute", null, false, null };
            var ok = (bool)method.Invoke(null, args);
            var selected = args[5] as CombatAimItemCheckDecision;
            var recomputedAim = args[6] is bool && (bool)args[6];
            var expiredReason = args[7] as string;
            if (!ok ||
                !ReferenceEquals(selected, recomputed) ||
                !recomputedAim ||
                !string.Equals(expiredReason, "none", StringComparison.Ordinal) ||
                Math.Abs(selected.AimWorldX - stale.AimWorldX) < 0.001f ||
                Math.Abs(selected.AimWorldY - stale.AimWorldY) < 0.001f)
            {
                throw new InvalidOperationException("Expected special projectile tail to prefer recomputed aim after target movement.");
            }

            args = new object[] { stale, null, false, false, "targetMovedWithoutRecompute", null, false, null };
            ok = (bool)method.Invoke(null, args);
            expiredReason = args[7] as string;
            if (ok || !string.Equals(expiredReason, "targetMovedWithoutRecompute", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected moved target without recompute to expire instead of reusing stale aim.");
            }
        }

        private static void SpecialDualProjectileTailRecomputesAimForMovingAssistTarget()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var stale = BuildVortexTailDecision(player, 100f, 120f);
            var recomputed = BuildVortexTailDecision(player, 260f, 300f);
            recomputed.Selection.Target.CenterX = 260f;
            recomputed.Selection.Target.CenterY = 300f;

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryChooseSpecialProjectileTailAimDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryChooseSpecialProjectileTailAimDecision reflection failed.");
            }

            var controller = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 35, type = 615, owner = 7, active = true, friendly = false },
                player,
                recomputed.WeaponProfile,
                recomputed.BallisticSolution);
            if (!controller.Matches)
            {
                throw new InvalidOperationException("Expected Vortex controller projectile to match before recompute assertion, got " + controller.Reason);
            }

            var args = new object[] { stale, recomputed, true, false, "targetMovedWithoutRecompute", null, false, null };
            var ok = (bool)method.Invoke(null, args);
            var selected = args[5] as CombatAimItemCheckDecision;
            var recomputedAim = args[6] is bool && (bool)args[6];
            var expiredReason = args[7] as string;
            if (!ok ||
                !ReferenceEquals(selected, recomputed) ||
                !recomputedAim ||
                !string.Equals(expiredReason, "none", StringComparison.Ordinal) ||
                Math.Abs(selected.AimWorldX - stale.AimWorldX) < 0.001f ||
                Math.Abs(selected.AimWorldY - stale.AimWorldY) < 0.001f)
            {
                throw new InvalidOperationException("Expected Vortex controller scoped tail to prefer recomputed aim after target movement.");
            }

            selected.PersistentHook = PersistentCursorHooks.ProjectileAI;
            selected.PersistentCursorActive = true;
            selected.PersistentCursorReason = "specialProjectileWeapon";
            selected.SpecialProjectileTailActive = true;
            selected.SpecialProjectileTailRecomputedAim = recomputedAim;
            selected.SpecialProjectileTailExpiredReason = "none";
            CombatAimProjectileCursorCompat.AttachDecisionMetadata(selected, controller, true, true, true);

            var json = BuildCombatAimDecisionJson(selected, true, true);
            AssertContains(json, "\"weaponFamily\":\"SpecialDualProjectile\"");
            AssertContains(json, "\"specialWeaponRuleKind\":\"dualProjectileSpread\"");
            AssertContains(json, "\"projectileCursorProjectileType\":615");
            AssertContains(json, "\"specialProjectileTailRecomputedAim\":true");
        }

        private static void SpecialProjectileTailExpiresInactiveBubbleAndIgnoresAmmoBullet()
        {
            var player = new FakePlayer { whoAmI = 7 };
            var decision = BuildXenopopperTailDecision(player, 1234f, 5678f);
            var inactiveBubble = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 20, type = 444, owner = 7, active = false, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);

            var method = typeof(CombatAimPersistentCursorService).GetMethod("ShouldExpireSpecialProjectileTailForMatchFailure", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("ShouldExpireSpecialProjectileTailForMatchFailure reflection failed.");
            }

            var args = new object[] { decision, inactiveBubble, null };
            var shouldExpire = (bool)method.Invoke(null, args);
            var expiredReason = args[2] as string;
            if (!shouldExpire || !string.Equals(expiredReason, "projectileInactive", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected inactive Xenopopper bubble to expire the special projectile tail.");
            }

            var ammoBullet = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                new FakeProjectile { whoAmI = 21, type = 14, owner = 7, active = true, friendly = true },
                player,
                decision.WeaponProfile,
                decision.BallisticSolution);
            if (ammoBullet.Matches || !string.Equals(ammoBullet.Reason, "notEligible:projectileMismatch", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected ordinary ammo bullet to stay out of Xenopopper bubble tail, got " + ammoBullet.Reason);
            }

            args = new object[] { decision, ammoBullet, null };
            shouldExpire = (bool)method.Invoke(null, args);
            if (shouldExpire)
            {
                throw new InvalidOperationException("Expected ordinary ammo bullet mismatch not to expire Xenopopper bubble tail.");
            }
        }

        private static void CombatAimItemCheckLogThrottleKeepsIndependentKeys()
        {
            CombatAimItemCheckService.ResetLogThrottleForTesting();
            var now = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
            if (!CombatAimItemCheckService.ShouldRecordLogForTesting("InstantItemCheck:noTarget:5526", now))
            {
                throw new InvalidOperationException("Expected first instant no-target log key to record.");
            }

            if (!CombatAimItemCheckService.ShouldRecordLogForTesting("PersistentCursor:noTarget:5526", now.AddMilliseconds(20)))
            {
                throw new InvalidOperationException("Expected first persistent no-target log key to record independently.");
            }

            if (CombatAimItemCheckService.ShouldRecordLogForTesting("InstantItemCheck:noTarget:5526", now.AddSeconds(1)))
            {
                throw new InvalidOperationException("Expected instant no-target log key to stay throttled even after an alternating key.");
            }

            if (CombatAimItemCheckService.ShouldRecordLogForTesting("PersistentCursor:noTarget:5526", now.AddSeconds(1)))
            {
                throw new InvalidOperationException("Expected persistent no-target log key to stay throttled even after an alternating key.");
            }

            if (!CombatAimItemCheckService.ShouldRecordLogForTesting("InstantItemCheck:noTarget:5526", now.AddSeconds(6)))
            {
                throw new InvalidOperationException("Expected itemcheck log key to record after the throttle interval.");
            }
        }

        private static CombatAimProjectileCursorMatch AssertSpecialWeaponAssistMatch(
            string label,
            FakeProjectile projectile,
            FakePlayer player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            string role;
            if (!CombatAimSpecialWeaponRuleResolver.TryResolveScopedProjectileRole(
                    projectile.type,
                    profile,
                    solution,
                    out role) ||
                !string.Equals(role, "weaponAssist", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + label + " to resolve as weaponAssist scoped projectile, got " + role);
            }

            var match = CombatAimProjectileCursorCompat.MatchSpecialWeaponProjectile(
                projectile,
                player,
                profile,
                solution);
            if (!match.Matches || !string.Equals(match.Reason, "matched:specialWeaponProjectile", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + label + " to match special scoped cursor, got " + match.Reason);
            }

            return match;
        }

        private static void AssertOrdinaryShotgunStaysOutOfSpecialProjectileScopedCursor(
            string name,
            int itemType,
            int shoot)
        {
            var player = new FakePlayer { whoAmI = 7 };
            var item = new FakeItem
            {
                type = itemType,
                stack = 1,
                Name = name,
                damage = 24,
                shoot = shoot,
                shootSpeed = 9f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            var profile = CombatAimWeaponProfile.Read(player, item);

            if (CombatAimPersistentCursorPolicy.IsSpecialProjectileScopedWeapon(profile))
            {
                throw new InvalidOperationException("Expected " + name + " to stay out of specialProjectileWeapon policy.");
            }

            CombatAimSpecialWeaponRule rule;
            if (!CombatAimSpecialWeaponRuleResolver.TryResolve(profile, out rule) ||
                !string.Equals(rule.Kind, "spreadMultiShot", StringComparison.Ordinal) ||
                rule.AllowsProjectileAiScoped)
            {
                throw new InvalidOperationException("Expected " + name + " to resolve as unscoped spread rule.");
            }

            string role;
            if (CombatAimSpecialWeaponRuleResolver.TryResolveScopedProjectileRole(
                    14,
                    profile,
                    new CombatAimBallisticSolution
                    {
                        ProjectileType = 14,
                        AmmoProjectileType = 14,
                        PrimaryProjectileType = 14,
                        PrimaryProjectileRole = "ammoPrimary"
                    },
                    out role))
            {
                throw new InvalidOperationException("Expected " + name + " ammo projectile not to resolve as scoped special projectile.");
            }
        }

        private static CombatAimSpecialWeaponRule ResolveSpreadRuleForTesting(int itemType)
        {
            var player = new FakePlayer();
            var item = new FakeItem
            {
                type = itemType,
                stack = 1,
                Name = "Special Spread Test",
                damage = 24,
                shoot = itemType == 2797 ? 444 : itemType == 3475 ? 615 : itemType == 3788 ? 661 : 14,
                shootSpeed = 10f,
                useAmmo = 97,
                ranged = true,
                useStyle = 5
            };
            player.inventory[0] = item;
            var profile = CombatAimWeaponProfile.Read(player, item);

            CombatAimSpecialWeaponRule rule;
            if (!CombatAimSpecialWeaponRuleResolver.TryResolve(profile, out rule))
            {
                throw new InvalidOperationException("Expected special spread rule for item " + itemType + ".");
            }

            return rule;
        }

        private static void InvokePrivateStatic(string name, params object[] args)
        {
            var method = typeof(CombatAimBallisticSolver).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException(name + " not found.");
            }

            method.Invoke(null, args);
        }

        private static void AssertPrivateStringField(object instance, string fieldName, string expected)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var actual = field == null ? null : field.GetValue(instance) as string;
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + fieldName + "=" + expected + ", got " + actual);
            }
        }

        private static void AssertPrivateBoolField(object instance, string fieldName, bool expected)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var actual = field != null && field.GetValue(instance) is bool && (bool)field.GetValue(instance);
            if (actual != expected)
            {
                throw new InvalidOperationException("Expected " + fieldName + "=" + expected + ", got " + actual);
            }
        }


    }
}
