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
        private static void FlailUpdateDisabledStopsBeforeLocalPlayerRead()
        {
            CombatAimFlailControlService.ResetForTesting();
            var restore = PushFlailUpdateTestState(0, null);
            try
            {
                CombatAimFlailControlService.Update();
                AssertFlailLastDiagnostics(FlailControlStates.Disabled, "autoAimDisabled");
            }
            finally
            {
                restore();
            }
        }

        private static void FlailUpdateUiBlockedStopsBeforeWeaponProfile()
        {
            CombatAimFlailControlService.ResetForTesting();
            var player = new Terraria.Player
            {
                whoAmI = 0,
                selectedItem = 0,
                active = true,
                controlUseItem = true,
                releaseUseItem = false,
                channel = true
            };
            var restore = PushFlailUpdateTestState(30, player);
            try
            {
                Terraria.Main.gameMenu = true;
                CombatAimFlailControlService.Update();
                AssertFlailLastDiagnostics(FlailControlStates.Disabled, "gameMenu");
            }
            finally
            {
                restore();
            }
        }

        private static void FlailUpdateIdleStopsBeforeWeaponProfile()
        {
            CombatAimFlailControlService.ResetForTesting();
            var player = new Terraria.Player
            {
                whoAmI = 0,
                selectedItem = 0,
                active = true,
                controlUseItem = false,
                releaseUseItem = true,
                channel = false
            };
            var restore = PushFlailUpdateTestState(30, player);
            try
            {
                CombatAimFlailControlService.Update();
                AssertFlailLastDiagnostics(FlailControlStates.Idle, "noActiveFlailUse");
            }
            finally
            {
                restore();
            }
        }

        private static void FlailControlPreservesHoldSpinAndReleasesOnPhysicalRelease()
        {
            var noProjectileHeld = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.None(),
                true,
                false,
                false,
                false,
                false,
                true,
                false,
                false);
            AssertFlailDecision(noProjectileHeld, FlailControlStates.SpinHold, false, false, false, "spinHoldNoProjectile");

            var spinHeld = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 0f, 0f, 0f),
                true,
                false,
                false,
                false,
                false,
                true,
                false,
                false);
            AssertFlailDecision(spinHeld, FlailControlStates.SpinHold, false, false, false, "spinHold");

            var release = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 0f, 0f, 0f),
                true,
                false,
                false,
                false,
                false,
                false,
                true,
                true);
            AssertFlailDecision(release, FlailControlStates.ReleaseToTarget, false, true, true, "physicalRelease");

            var hit = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 1f, 6f, 0f),
                true,
                false,
                true,
                false,
                false,
                false,
                false,
                true);
            AssertFlailDecision(hit, FlailControlStates.ProjectileActive, false, false, false, "hitDetected");

            var flying = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 1f, 6f, 0f),
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                true);
            AssertFlailDecision(flying, FlailControlStates.ProjectileFlying, false, false, false, "projectileFlying");
        }

        private static void FlailReleaseStateMachineKeepsStableReasons()
        {
            var noProjectile = CombatAimFlailProjectileFrame.None();
            var stationary = CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 0f, 0f, 0f);
            var moving = CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 1f, 6f, 0f);
            var returning = CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 4f, 0f, 0f);

            AssertFlailDecision(
                DecideFlailStateMachine(noProjectile, true, false, false, false, false, true, false, false),
                FlailControlStates.SpinHold,
                false,
                false,
                false,
                "spinHoldNoProjectile");
            AssertFlailDecision(
                DecideFlailStateMachine(noProjectile, false, false, false, false, false, true, false, false),
                FlailControlStates.ReadyToLaunch,
                false,
                false,
                false,
                "itemUseCooldown");
            AssertFlailDecision(
                DecideFlailStateMachine(returning, true, false, false, false, false, true, false, false),
                FlailControlStates.ProjectileActive,
                false,
                false,
                false,
                "spinHoldReturnState");
            AssertFlailDecision(
                DecideFlailStateMachine(stationary, true, false, false, false, false, true, false, false),
                FlailControlStates.SpinHold,
                false,
                false,
                false,
                "spinHold");
            AssertFlailDecision(
                DecideFlailStateMachine(moving, true, false, false, false, false, true, false, false),
                FlailControlStates.ProjectileFlying,
                false,
                false,
                false,
                "physicalHoldProjectileMoving");
            AssertFlailDecision(
                DecideFlailStateMachine(noProjectile, true, true, false, false, false, false, true, true),
                FlailControlStates.ReleaseToTarget,
                false,
                true,
                true,
                "physicalRelease");
            AssertFlailDecision(
                DecideFlailStateMachine(noProjectile, true, true, false, false, false, false, false, false),
                FlailControlStates.Cooldown,
                false,
                false,
                false,
                "pulseCooldown");
            AssertFlailDecision(
                DecideFlailStateMachine(noProjectile, true, false, false, false, false, false, false, false),
                FlailControlStates.Idle,
                false,
                false,
                false,
                "notUsingItem");
            AssertFlailDecision(
                DecideFlailStateMachine(returning, true, false, false, false, false, false, false, true),
                FlailControlStates.ProjectileActive,
                false,
                false,
                false,
                "flailReturnState");
            AssertFlailDecision(
                DecideFlailStateMachine(stationary, true, false, false, false, true, false, false, true),
                FlailControlStates.StuckRecoveryRelease,
                false,
                true,
                true,
                "stuckRecoveryRelease:ai0ZeroVelocity");
            AssertFlailDecision(
                DecideFlailStateMachine(moving, true, false, true, false, false, false, false, true),
                FlailControlStates.ProjectileActive,
                false,
                false,
                false,
                "hitDetected");
            AssertFlailDecision(
                DecideFlailStateMachine(moving, true, false, false, true, false, false, false, true),
                FlailControlStates.ProjectileActive,
                false,
                false,
                false,
                "collisionDetected");
            AssertFlailDecision(
                DecideFlailStateMachine(moving, true, false, false, false, false, false, false, true),
                FlailControlStates.ProjectileFlying,
                false,
                false,
                false,
                "projectileFlying");
            AssertFlailDecision(
                DecideFlailStateMachine(stationary, true, false, false, false, false, false, false, true),
                FlailControlStates.WaitHitOrCollision,
                false,
                false,
                false,
                "waitReturnAfterRelease");
            AssertFlailDecision(
                DecideFlailStateMachine(stationary, true, false, false, false, false, false, false, false),
                FlailControlStates.WaitHitOrCollision,
                false,
                false,
                false,
                "waitSpinRelease");
        }

        private static CombatAimFlailControlDecision DecideFlailStateMachine(
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
            var context = new CombatAimFlailDecisionContext
            {
                Projectile = projectile,
                ItemReady = itemReady,
                InCooldown = inCooldown,
                HitDetected = hitDetected,
                CollisionDetected = collisionDetected,
                StuckRecovery = stuckRecovery,
                PhysicalHeld = physicalHeld,
                PhysicalReleasePending = physicalReleasePending,
                ReleaseInFlight = releaseInFlight
            };
            return CombatAimFlailReleaseStateMachine.Decide(in context);
        }

        private static void FlailItemCheckTakeoverSkipsHoldSpin()
        {
            ResetFakeMainMouse(true, false);
            var player = new FakePlayer
            {
                controlUseItem = true,
                releaseUseItem = false,
                channel = true
            };
            var decision = BuildFlailItemCheckDecision(player);
            CombatAimFlailControlService.SetLastDiagnosticsForTesting(new CombatAimFlailDiagnostics
            {
                ItemType = decision.ItemType,
                ItemName = decision.ItemName,
                Eligible = true,
                Reason = "eligible:flailAiStyle15",
                Active = true,
                State = FlailControlStates.SpinHold,
                InputMode = "observe",
                InputPhase = FlailControlStates.SpinHold,
                TakeoverScope = "none",
                StuckRecovery = "none",
                PhysicalUseItemHeld = true
            });

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (CombatAimFlailControlService.TryBeginItemCheckTakeover(player, decision, out takeover))
            {
                TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
                throw new InvalidOperationException("Expected flail hold spin to avoid ItemCheck use-item takeover.");
            }

            if (!player.controlUseItem || player.releaseUseItem || !player.channel ||
                !Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease)
            {
                throw new InvalidOperationException("Expected flail hold spin to preserve physical held inputs.");
            }
        }

        private static void FlailReleaseHoldItemCheckTakeoverArmsProjectileTailBeforeRuntimeUpdate()
        {
            CombatAimFlailControlService.ResetForTesting();
            Terraria.Main.GameUpdateCount = 700;
            ResetFakeMainMouse(false, true);
            var player = new FakePlayer
            {
                whoAmI = 7,
                controlUseItem = false,
                releaseUseItem = true,
                channel = false
            };
            var decision = BuildFlailItemCheckDecision(player);
            decision.AimApplyMode = CombatAimApplyModes.ReleaseHold;
            decision.UseItemHeld = false;
            decision.UseItemReleased = false;
            decision.WasUseItemHeldLastTick = true;
            decision.ReleasedThisTick = true;
            decision.ReleaseDetected = true;
            decision.ReleaseHoldPending = true;
            decision.ReleaseHoldActive = true;
            decision.ReleaseHoldState = ReleaseHoldStates.ReleasedPending;
            decision.ReleaseHoldValidationReason = "targetDummyAllowed:strictRecomputed";

            CombatAimFlailControlService.SetLastDiagnosticsForTesting(new CombatAimFlailDiagnostics
            {
                ItemType = decision.ItemType,
                ItemName = decision.ItemName,
                Eligible = true,
                Reason = "eligible:flailAiStyle15",
                Active = true,
                State = FlailControlStates.SpinHold,
                InputMode = "observe",
                InputPhase = FlailControlStates.SpinHold,
                TakeoverScope = "none",
                StuckRecovery = "none",
                PhysicalUseItemHeld = true,
                PhysicalReleasePending = false,
                CachedReleaseAim = true,
                CachedReleaseAimAgeTicks = 0,
                CachedReleaseAimReason = "available"
            });

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (!CombatAimFlailControlService.TryBeginItemCheckTakeover(player, decision, out takeover))
            {
                throw new InvalidOperationException("Expected ReleaseHold flail decision to enter ItemCheck release takeover before RuntimeUpdate observes the release.");
            }

            if (player.controlUseItem || !player.releaseUseItem || player.channel ||
                Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease ||
                takeover == null || !takeover.Applied || takeover.Pressed)
            {
                throw new InvalidOperationException("Expected ReleaseHold ItemCheck takeover to apply release state without pressing use item.");
            }

            var diagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(decision);
            if (diagnostics == null ||
                !diagnostics.AttackRelease ||
                !diagnostics.AttackSuppressed ||
                !diagnostics.PhysicalReleasePending ||
                !string.Equals(diagnostics.State, FlailControlStates.ReleaseToTarget, StringComparison.Ordinal) ||
                !string.Equals(diagnostics.TakeoverScope, "ItemCheck", StringComparison.Ordinal) ||
                !string.Equals(diagnostics.BlockedReason, "releaseHoldItemCheck", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected ReleaseHold ItemCheck takeover diagnostics to promote stale SpinHold into ReleaseToTarget.");
            }

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryGetFlailReleaseTailDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryGetFlailReleaseTailDecision reflection failed.");
            }

            var args = new object[] { 700L, null, null };
            var tailAvailable = (bool)method.Invoke(null, args);
            var tail = args[2] as CombatAimItemCheckDecision;
            if (!tailAvailable || tail == null ||
                !string.Equals(tail.PersistentCursorReason, "flailAiStyle15Release", StringComparison.Ordinal) ||
                Math.Abs(tail.AimWorldX - decision.AimWorldX) > 0.001f ||
                Math.Abs(tail.AimWorldY - decision.AimWorldY) > 0.001f)
            {
                throw new InvalidOperationException("Expected ReleaseHold ItemCheck takeover to arm flail Projectile.AI release tail immediately.");
            }

            TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
        }

        private static void FlailItemCheckTakeoverAppliesPhysicalReleaseScope()
        {
            ResetFakeMainMouse(false, true);
            var player = new FakePlayer
            {
                controlUseItem = false,
                releaseUseItem = true,
                channel = false
            };
            var decision = BuildFlailItemCheckDecision(player);
            CombatAimFlailControlService.SetLastDiagnosticsForTesting(new CombatAimFlailDiagnostics
            {
                ItemType = decision.ItemType,
                ItemName = decision.ItemName,
                Eligible = true,
                Reason = "eligible:flailAiStyle15",
                Active = true,
                State = FlailControlStates.ReleaseToTarget,
                AttackRelease = true,
                AttackSuppressed = true,
                InputMode = "controlledUseItemRelease",
                InputPhase = FlailControlStates.ReleaseToTarget,
                TakeoverScope = "none",
                StuckRecovery = "none",
                PhysicalUseItemHeld = false,
                PhysicalReleasePending = true
            });

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (!CombatAimFlailControlService.TryBeginItemCheckTakeover(player, decision, out takeover))
            {
                throw new InvalidOperationException("Expected flail physical release takeover to apply in ItemCheck scope: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (player.controlUseItem || !player.releaseUseItem || player.channel ||
                Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease ||
                takeover == null || !takeover.Applied || takeover.Pressed ||
                !string.Equals(takeover.ScopeName, "ItemCheck", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected physical release takeover to apply release state without pressing use item.");
            }

            TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
            if (player.controlUseItem || !player.releaseUseItem || player.channel ||
                Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease)
            {
                throw new InvalidOperationException("Expected physical release takeover restore to recover original release inputs.");
            }
        }

        private static void FlailStuckProjectileRetriesReleaseAfterPhysicalRelease()
        {
            var stuck = CombatAimFlailControlService.DecideForTesting(
                CombatAimFlailProjectileFrame.ForTesting(true, 10, 1058, 42, 0f, 0f, 0f),
                true,
                false,
                false,
                false,
                true,
                false,
                false,
                true);
            AssertFlailDecision(stuck, FlailControlStates.StuckRecoveryRelease, false, true, true, "stuckRecoveryRelease:ai0ZeroVelocity");
        }

        private static void FlailProjectileTrackerAcceptsOnlyLocalActiveFriendlyFlail()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var oldProjectiles = Terraria.Main.projectile;
            var oldMyPlayer = Terraria.Main.myPlayer;
            try
            {
                Terraria.Main.myPlayer = 7;
                Terraria.Main.projectile = new object[]
                {
                    BuildFakeFlailProjectile(1, 1058, 11, 9, true, true, false),
                    BuildFakeFlailProjectile(2, 1058, 12, 7, false, true, false),
                    BuildFakeFlailProjectile(3, 1058, 13, 7, true, false, false),
                    BuildFakeFlailProjectile(4, 1058, 14, 7, true, true, true),
                    new FakeProjectile { whoAmI = 5, type = 1058, identity = 15, owner = 7, active = true, friendly = true, aiStyle = 14 },
                    BuildFakeFlailProjectile(6, 1058, 16, 7, true, true, false)
                };

                var tracker = new CombatAimFlailProjectileTracker();
                CombatAimFlailControlService.FlailProjectileSnapshot snapshot;
                if (!tracker.TryFindActiveFlailProjectile(new FakePlayer { whoAmI = 7 }, 1058, out snapshot) ||
                    snapshot == null ||
                    snapshot.WhoAmI != 6 ||
                    snapshot.Owner != 7 ||
                    snapshot.Type != 1058 ||
                    snapshot.AiStyle != 15 ||
                    !snapshot.Active ||
                    !snapshot.Friendly ||
                    snapshot.Hostile)
                {
                    throw new InvalidOperationException("Expected tracker to accept only the local active friendly non-hostile aiStyle 15 flail projectile.");
                }
            }
            finally
            {
                Terraria.Main.projectile = oldProjectiles;
                Terraria.Main.myPlayer = oldMyPlayer;
                restoreRuntimeTypes();
            }
        }

        private static void FlailProjectileTrackerKeepsNonExpectedFallback()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var oldProjectiles = Terraria.Main.projectile;
            var oldMyPlayer = Terraria.Main.myPlayer;
            try
            {
                Terraria.Main.myPlayer = 7;
                Terraria.Main.projectile = new object[]
                {
                    BuildFakeFlailProjectile(10, 1057, 20, 7, true, true, false),
                    BuildFakeFlailProjectile(11, 1059, 21, 7, true, true, false)
                };

                var tracker = new CombatAimFlailProjectileTracker();
                CombatAimFlailControlService.FlailProjectileSnapshot snapshot;
                if (!tracker.TryFindActiveFlailProjectile(new FakePlayer { whoAmI = 7 }, 1058, out snapshot) ||
                    snapshot == null ||
                    snapshot.WhoAmI != 10 ||
                    snapshot.Type != 1057)
                {
                    throw new InvalidOperationException("Expected tracker to keep the first eligible flail fallback when expected projectile type is absent.");
                }
            }
            finally
            {
                Terraria.Main.projectile = oldProjectiles;
                Terraria.Main.myPlayer = oldMyPlayer;
                restoreRuntimeTypes();
            }
        }

        private static void FlailHitCacheResetsOnProjectileIdentityChange()
        {
            var tracker = new CombatAimFlailProjectileTracker();
            var cacheField = typeof(CombatAimFlailProjectileTracker).GetField("_lastLocalNpcImmunity", BindingFlags.Instance | BindingFlags.NonPublic);
            var cache = cacheField == null ? null : cacheField.GetValue(tracker) as int[];
            if (cache == null || cache.Length != 256)
            {
                throw new InvalidOperationException("Expected flail local NPC immunity cache length to remain 256.");
            }

            var snapshot = BuildFlailSnapshot(20, 1058, 30, 0f, 0f, 0f, new object[] { 0, 0, 0 });
            if (tracker.UpdateHitCache(snapshot))
            {
                throw new InvalidOperationException("Expected initial zero immunity baseline to report no hit.");
            }

            snapshot.LocalNpcImmunity = new object[] { 0, 2, 5 };
            if (!tracker.UpdateHitCache(snapshot))
            {
                throw new InvalidOperationException("Expected increased local NPC immunity to report a hit.");
            }

            if (tracker.UpdateHitCache(snapshot))
            {
                throw new InvalidOperationException("Expected unchanged local NPC immunity to report no new hit.");
            }

            snapshot.LocalNpcImmunity = new object[] { 0 };
            if (tracker.UpdateHitCache(snapshot))
            {
                throw new InvalidOperationException("Expected shorter local NPC immunity cache update to clear old slots without reporting a hit.");
            }

            snapshot.LocalNpcImmunity = new object[] { 0, 0, 5 };
            if (!tracker.UpdateHitCache(snapshot))
            {
                throw new InvalidOperationException("Expected cleared old cache slots to detect a later immunity increase.");
            }

            snapshot.Identity = 31;
            snapshot.LocalNpcImmunity = new object[] { 0, 0, 0 };
            if (tracker.UpdateHitCache(snapshot))
            {
                throw new InvalidOperationException("Expected projectile identity change to reset hit cache before new zero baseline.");
            }

            snapshot.LocalNpcImmunity = new object[] { 0, 1, 0 };
            if (!tracker.UpdateHitCache(snapshot))
            {
                throw new InvalidOperationException("Expected new projectile immunity increase to be detected after identity reset.");
            }
        }

        private static void FlailStuckTrackingReachesRecoveryTick()
        {
            var tracker = new CombatAimFlailProjectileTracker();
            var snapshot = BuildFlailSnapshot(30, 1058, 40, 0f, 0f, 0f, new object[] { 0 });
            tracker.UpdateHitCache(snapshot);

            var ticks = 0;
            for (var index = 0; index < 8; index++)
            {
                ticks = tracker.UpdateStuckTracking(snapshot);
            }

            if (ticks != 8)
            {
                throw new InvalidOperationException("Expected stationary ai0=0 flail projectile to reach 8 stuck ticks.");
            }

            snapshot.VelocityX = 0.01f;
            snapshot.Velocity = new Vector2 { X = 0.01f, Y = 0f };
            if (tracker.UpdateStuckTracking(snapshot) != 0)
            {
                throw new InvalidOperationException("Expected moving flail projectile to reset stuck ticks.");
            }
        }

        private static void FlailTileCollisionDetectorFailsClosedAndCachesMethodInfo()
        {
            var snapshot = BuildFlailSnapshot(40, 1058, 50, 1f, 4f, 0f, new object[] { 0 });
            snapshot.Position = new Vector2 { X = 10f, Y = 12f };
            snapshot.Velocity = new Vector2 { X = 4f, Y = 0f };
            snapshot.Width = 16;
            snapshot.Height = 18;

            var missingResolveCount = 0;
            var missingDetector = new CombatAimFlailCollisionDetector(delegate
            {
                missingResolveCount++;
                return typeof(FakeMissingTileCollisionType);
            });
            if (missingDetector.DetectTileCollision(snapshot) ||
                missingDetector.DetectTileCollision(snapshot) ||
                missingResolveCount != 1)
            {
                throw new InvalidOperationException("Expected missing TileCollision method to fail closed and resolve only once.");
            }

            var validResolveCount = 0;
            FakeTileCollisionType.CallCount = 0;
            var validDetector = new CombatAimFlailCollisionDetector(delegate
            {
                validResolveCount++;
                return typeof(FakeTileCollisionType);
            });
            if (!validDetector.DetectTileCollision(snapshot) ||
                !validDetector.DetectTileCollision(snapshot) ||
                validResolveCount != 1 ||
                FakeTileCollisionType.CallCount != 2)
            {
                throw new InvalidOperationException("Expected TileCollision detector to cache MethodInfo while invoking the cached method per check.");
            }

            var earlyResolveCount = 0;
            var earlyDetector = new CombatAimFlailCollisionDetector(delegate
            {
                earlyResolveCount++;
                return typeof(FakeTileCollisionType);
            });
            snapshot.Ai0 = 0f;
            if (earlyDetector.DetectTileCollision(snapshot) || earlyResolveCount != 0)
            {
                throw new InvalidOperationException("Expected non-release ai0 state to skip TileCollision resolution.");
            }
        }


    }
}
