using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void TemporaryDoubleJumpRequiresAirJump()
        {
            var record = Record("temporary_double_jump", "double_jump", "jump");
            var analysis = Analysis();
            AssertUnavailable(record, analysis, "airJumpUnavailable");
            analysis.HasAirJump = true;
            AssertAvailable(record, analysis);
        }

        private static void TemporaryRocketBootsRequireCapability()
        {
            var record = Record("temporary_rocket_boots", "rocket_boots", "jump");
            var analysis = Analysis();
            AssertUnavailable(record, analysis, "rocketBootsUnavailable");
            analysis.HasRocketJump = true;
            AssertAvailable(record, analysis);
        }

        private static void TemporaryFlyingCarpetRequiresCapability()
        {
            var record = Record("temporary_flying_carpet", "flying_carpet", "jump");
            var analysis = Analysis();
            analysis.AerialJumpWindow = true;
            AssertUnavailable(record, analysis, "flyingCarpetUnavailable");
            analysis.HasFlyingCarpetAvailable = true;
            AssertAvailable(record, analysis);
        }

        private static void TemporaryFlyingCarpetAcceptsPostApplyCapabilityEvidence()
        {
            var record = Record("temporary_flying_carpet", "flying_carpet", "jump");
            record.PostApplyCapabilityObserved = true;
            record.PostApplyVerificationReason = "flyingCarpetAvailableAfterApply";

            var analysis = Analysis();
            analysis.AerialJumpWindow = true;
            AssertAvailable(record, analysis);
        }

        private static void SafeLandingTreatsWingFlightStateAsOriginalSafeWithoutEquippedWing()
        {
            var player = new FakePlayer
            {
                wingsLogic = 1,
                wingTime = 120f
            };
            var profile = new JumpInputProfile
            {
                WingsLogic = 1,
                WingTime = 120f,
                HasWingFlight = true
            };

            string reason;
            if (!InvokeSafeLandingAlreadySafe(player, profile, out reason) ||
                !string.Equals(reason, "wingFlightState", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected wing flight state without equipped wing to be treated as original safe state, got " + reason + ".");
            }

            JumpInputProfile readProfile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out readProfile) ||
                readProfile == null ||
                !readProfile.HasWingFlight)
            {
                throw new InvalidOperationException("Expected jump profile to preserve vanilla wing flight state after wing removal.");
            }
        }

        private static void SafeLandingTreatsCurrentEquippedWingAsAlreadySafe()
        {
            var player = new FakePlayer();
            player.armor[3] = new FakeItem
            {
                type = 999,
                stack = 1,
                Name = "Test Wing",
                wingSlot = 1
            };

            string reason;
            if (!InvokeSafeLandingAlreadySafe(player, new JumpInputProfile(), out reason) ||
                !string.Equals(reason, "wingsEquipped", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected equipped wing to be already safe, got " + reason + ".");
            }
        }

        private static void SafeLandingActiveGrappleIsNotAlreadySafeWhileFallingFast()
        {
            var player = new FakePlayer
            {
                grapCount = 1
            };
            var analysis = Analysis();
            analysis.RawGrapCount = 1;
            analysis.FallingSpeed = 10f;

            string reason;
            if (InvokeSafeLandingAlreadySafe(player, new JumpInputProfile(), analysis, out reason))
            {
                throw new InvalidOperationException("Expected a fast-falling active grapple projectile not to suppress rescue, got " + reason + ".");
            }

            analysis.FallingSpeed = 1f;
            if (!InvokeSafeLandingAlreadySafe(player, new JumpInputProfile(), analysis, out reason) ||
                !string.Equals(reason, "grappled", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected slow active grapple state to remain already safe, got " + reason + ".");
            }
        }

        private static void SafeLandingIgnoresWingInLockedAccessorySlot()
        {
            var player = new FakePlayer
            {
                MaxUsableSlot = 7
            };
            player.armor[8] = new FakeItem
            {
                type = 1001,
                stack = 1,
                Name = "Locked Slot Wing",
                wingSlot = 1
            };

            string reason;
            if (InvokeSafeLandingAlreadySafe(player, new JumpInputProfile(), out reason))
            {
                throw new InvalidOperationException("Expected locked accessory slot wing to be ignored, got " + reason + ".");
            }
        }

        private static void SafeLandingCheapPrecheckSkipsSlowFall()
        {
            var player = new FakePlayer
            {
                gravDir = 1f,
                velocity = new FakeVector2 { X = 0f, Y = 2f }
            };

            MovementSafeLandingAnalysis analysis;
            bool shouldRunFullAnalysis;
            if (!MovementSafeLandingCompat.TryCheapDangerPrecheck(player, out analysis, out shouldRunFullAnalysis))
            {
                throw new InvalidOperationException("Expected safe landing cheap precheck to read a fake player.");
            }

            if (shouldRunFullAnalysis)
            {
                throw new InvalidOperationException("Expected slow falling player to skip full safe landing analysis.");
            }

            AssertStringEquals(analysis.SkipReason, "notFallingFastEnough:cheap", "cheap precheck skip reason");
            AssertNear(analysis.FallingSpeed, 2f, "cheap precheck falling speed");
            if (!analysis.PlayerControllable)
            {
                throw new InvalidOperationException("Expected fake player to be controllable during cheap precheck.");
            }
        }

        private static void SafeLandingCheapPrecheckOpensFullAnalysisWhenFast()
        {
            var player = new FakePlayer
            {
                gravDir = -1f,
                velocity = new FakeVector2 { X = 1f, Y = -7f }
            };

            MovementSafeLandingAnalysis analysis;
            bool shouldRunFullAnalysis;
            if (!MovementSafeLandingCompat.TryCheapDangerPrecheck(player, out analysis, out shouldRunFullAnalysis))
            {
                throw new InvalidOperationException("Expected safe landing cheap precheck to read reverse-gravity falling state.");
            }

            if (!shouldRunFullAnalysis)
            {
                throw new InvalidOperationException("Expected fast reverse-gravity fall to continue into full safe landing analysis.");
            }

            AssertStringEquals(analysis.SkipReason, "cheapPrecheckPassed", "cheap precheck pass reason");
            AssertNear(analysis.GravityDirection, -1f, "cheap precheck gravity direction");
            AssertNear(analysis.FallingSpeed, 7f, "cheap precheck reverse-gravity falling speed");
        }

        private static void SafeLandingCheapPrecheckFailsOpenWhenVelocityUnavailable()
        {
            var player = new FakeSafeLandingCheapPrecheckPlayerWithoutVelocity();

            MovementSafeLandingAnalysis analysis;
            bool shouldRunFullAnalysis;
            if (!MovementSafeLandingCompat.TryCheapDangerPrecheck(player, out analysis, out shouldRunFullAnalysis))
            {
                throw new InvalidOperationException("Expected missing velocity to fail open without treating precheck as an error.");
            }

            if (!shouldRunFullAnalysis)
            {
                throw new InvalidOperationException("Expected missing velocity to continue into full safe landing analysis.");
            }

            AssertStringEquals(analysis.SkipReason, "cheapPrecheckUnavailable:velocity", "cheap precheck fail-open reason");
        }

        private static void SafeLandingCheapPrecheckFailsOpenWhenPlayerStateUnavailable()
        {
            var player = new FakeSafeLandingCheapPrecheckPlayerWithoutState();

            MovementSafeLandingAnalysis analysis;
            bool shouldRunFullAnalysis;
            if (!MovementSafeLandingCompat.TryCheapDangerPrecheck(player, out analysis, out shouldRunFullAnalysis))
            {
                throw new InvalidOperationException("Expected missing player state to fail open without treating precheck as an error.");
            }

            if (!shouldRunFullAnalysis)
            {
                throw new InvalidOperationException("Expected missing player state to continue into full safe landing analysis.");
            }

            AssertStringEquals(analysis.SkipReason, "cheapPrecheckUnavailable:playerState", "cheap precheck state fail-open reason");
        }

        private sealed class FakeSafeLandingCheapPrecheckPlayerWithoutVelocity
        {
            public bool active = true;
            public bool dead;
            public bool ghost;
            public bool CCed;
            public float gravDir = 1f;
        }

        private sealed class FakeSafeLandingCheapPrecheckPlayerWithoutState
        {
            public float gravDir = 1f;
            public FakeVector2 velocity = new FakeVector2 { X = 0f, Y = 2f };
        }

        private static bool InvokeSafeLandingAlreadySafe(FakePlayer player, JumpInputProfile profile, out string reason)
        {
            var method = typeof(MovementSafeLandingCompat).GetMethod(
                "TryResolveAlreadySafe",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(object), typeof(JumpInputProfile), typeof(string).MakeByRefType() },
                null);
            if (method == null)
            {
                throw new InvalidOperationException("TryResolveAlreadySafe reflection hook missing.");
            }

            var args = new object[] { player, profile, null };
            var result = (bool)method.Invoke(null, args);
            reason = args[2] as string ?? string.Empty;
            return result;
        }

        private static bool InvokeSafeLandingAlreadySafe(FakePlayer player, JumpInputProfile profile, MovementSafeLandingAnalysis analysis, out string reason)
        {
            var method = typeof(MovementSafeLandingCompat).GetMethod(
                "TryResolveAlreadySafe",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(object), typeof(JumpInputProfile), typeof(MovementSafeLandingAnalysis), typeof(string).MakeByRefType() },
                null);
            if (method == null)
            {
                throw new InvalidOperationException("TryResolveAlreadySafe analysis reflection hook missing.");
            }

            var args = new object[] { player, profile, analysis, null };
            var result = (bool)method.Invoke(null, args);
            reason = args[3] as string ?? string.Empty;
            return result;
        }

        private static void SafeLandingProjectsHorizontalImpactProbe()
        {
            var projected = MovementSafeLandingCompat.ProjectImpactProbeXForTesting(100f, 5f, 10f, 80);
            AssertNear(projected, 140f, "projected impact X");

            var capped = MovementSafeLandingCompat.ProjectImpactProbeXForTesting(100f, 5f, 10f, 400);
            AssertNear(capped, 190f, "capped projected impact X");

            var stationary = MovementSafeLandingCompat.ProjectImpactProbeXForTesting(100f, 0f, 10f, 80);
            AssertNear(stationary, 100f, "stationary projected impact X");

            var upwardUnknown = MovementSafeLandingCompat.ProjectImpactProbeXForTesting(100f, -4f, 0f, 80);
            AssertNear(upwardUnknown, 100f, "zero speed projected impact X");
        }

        private static void SafeLandingManualProbeDetectsSlopedPlatform()
        {
            var previousTiles = Terraria.Main.tile;
            var previousSolid = Terraria.Main.tileSolid;
            var previousSolidTop = Terraria.Main.tileSolidTop;
            try
            {
                MovementSafeLandingCompat.SetMainTypeForTesting(typeof(Terraria.Main));
                MovementSafeLandingCompat.ResetCollisionFastPathCachesForTesting();
                Terraria.Main.tile = new object[20, 20];
                Terraria.Main.tileSolid = new bool[1000];
                Terraria.Main.tileSolidTop = new bool[1000];
                Terraria.Main.tileSolid[19] = true;
                Terraria.Main.tileSolidTop[19] = true;
                Terraria.Main.tile[5, 10] = new FakeTile
                {
                    type = 19,
                    Active = true,
                    Slope = 1
                };

                bool solid;
                if (!MovementSafeLandingCompat.TryProbeLandingCollision(80f, 126f, 20, 42, 1f, 10f, out solid) || !solid)
                {
                    throw new InvalidOperationException("Expected manual landing probe to detect a sloped platform surface.");
                }
            }
            finally
            {
                Terraria.Main.tile = previousTiles;
                Terraria.Main.tileSolid = previousSolid;
                Terraria.Main.tileSolidTop = previousSolidTop;
                MovementSafeLandingCompat.SetMainTypeForTesting(null);
                MovementSafeLandingCompat.ResetCollisionFastPathCachesForTesting();
            }
        }

        private static void SafeLandingCollisionFastPathRecordsManualFallback()
        {
            WithSafeLandingTileMap(() =>
            {
                Terraria.Main.tile[5, 10] = new FakeTile
                {
                    type = 1,
                    Active = true,
                    Slope = 1
                };

                bool solid;
                if (!MovementSafeLandingCompat.TryProbeLandingCollision(80f, 126f, 20, 42, 1f, 10f, out solid) || !solid)
                {
                    throw new InvalidOperationException("Expected safe landing collision probe to detect the manual slope fallback.");
                }

                AssertStringEquals(MovementSafeLandingCompat.CollisionFastPathStatus, "manual_surface", "collision fast path status");
            });
        }

        private static void SafeLandingLandingSurfaceReportsSlopeContact()
        {
            WithSafeLandingTileMap(() =>
            {
                Terraria.Main.tile[5, 10] = new FakeTile
                {
                    type = 1,
                    Active = true,
                    Slope = 1
                };

                var analysis = AnalysisForLandingSurfaceProbe();
                MovementLandingSurfaceHit hit;
                if (!MovementSafeLandingCompat.TryFindLandingSurfaceForTesting(analysis, out hit) || hit == null || !hit.Found)
                {
                    throw new InvalidOperationException("Expected landing surface solver to find a slope contact.");
                }

                AssertStringEquals(hit.SurfaceKind, "slope", "surface kind");
                AssertStringEquals(hit.SlopeDirection, "left_high_right_low", "slope direction");
                AssertStringEquals(hit.ContactTileX.ToString(CultureInfo.InvariantCulture), "5", "contact tile x");
                AssertStringEquals(hit.ContactTileY.ToString(CultureInfo.InvariantCulture), "10", "contact tile y");
                AssertContains(hit.Summary, "slope left_high_right_low contact=");
                AssertContains(hit.Summary, "movingIntoSlope=false");
                AssertStringEquals(hit.Summary, hit.BuildSummary(), "landing surface summary");
                if (hit.ProjectedPlayerRightX - hit.ProjectedPlayerLeftX < 19f)
                {
                    throw new InvalidOperationException("Expected projected player left/right to describe the full rectangle.");
                }
            });
        }

        private static void SafeLandingLandingSurfaceReportsMovingIntoSlope()
        {
            WithSafeLandingTileMap(() =>
            {
                Terraria.Main.tile[5, 10] = new FakeTile
                {
                    type = 1,
                    Active = true,
                    Slope = 1
                };

                var analysis = AnalysisForLandingSurfaceProbe();
                analysis.VelocityX = -4f;
                MovementLandingSurfaceHit hit;
                if (!MovementSafeLandingCompat.TryFindLandingSurfaceForTesting(analysis, out hit) || hit == null || !hit.Found)
                {
                    throw new InvalidOperationException("Expected landing surface solver to find an into-slope contact.");
                }

                if (!hit.MovingIntoSlope || hit.MovingWithSlope)
                {
                    throw new InvalidOperationException("Expected left-high/right-low slope with leftward velocity to be moving into slope.");
                }

                AssertStringEquals(hit.ContactSample, "leading_foot", "contact sample");
            });
        }

        private static void SafeLandingLandingSurfaceHandlesThreeTileFootCoverage()
        {
            WithSafeLandingTileMap(() =>
            {
                Terraria.Main.tile[6, 10] = new FakeTile
                {
                    type = 1,
                    Active = true
                };

                var analysis = AnalysisForLandingSurfaceProbe();
                analysis.PositionX = 70f;
                analysis.Width = 40;
                MovementLandingSurfaceHit hit;
                if (!MovementSafeLandingCompat.TryFindLandingSurfaceForTesting(analysis, out hit) || hit == null || !hit.Found)
                {
                    throw new InvalidOperationException("Expected landing surface solver to scan tile segment samples across a wide player rectangle.");
                }

                AssertStringEquals(hit.ContactTileX.ToString(CultureInfo.InvariantCulture), "6", "contact tile x");
                AssertStringEquals(hit.ContactSample, "tile_segment", "contact sample");
            });
        }

        private static void SafeLandingLandingSurfaceProjectsHorizontalMotion()
        {
            WithSafeLandingTileMap(() =>
            {
                Terraria.Main.tile[7, 10] = new FakeTile
                {
                    type = 1,
                    Active = true
                };

                var analysis = AnalysisForLandingSurfaceProbe();
                analysis.VelocityX = 5f;
                MovementLandingSurfaceHit hit;
                if (!MovementSafeLandingCompat.TryFindLandingSurfaceForTesting(analysis, out hit) || hit == null || !hit.Found)
                {
                    throw new InvalidOperationException("Expected landing surface solver to project horizontal motion.");
                }

                if (hit.ProjectedPlayerLeftX <= analysis.PositionX + 4f)
                {
                    throw new InvalidOperationException("Expected projected landing left X to move with horizontal velocity, got " + hit.ProjectedPlayerLeftX.ToString(CultureInfo.InvariantCulture));
                }
            });
        }

        private static void SafeLandingLandingSurfacePrefersProjectedMotionOverCurrentColumn()
        {
            WithSafeLandingTileMap(() =>
            {
                Terraria.Main.tile[5, 10] = new FakeTile
                {
                    type = 1,
                    Active = true
                };
                Terraria.Main.tile[7, 10] = new FakeTile
                {
                    type = 1,
                    Active = true
                };

                var analysis = AnalysisForLandingSurfaceProbe();
                analysis.VelocityX = 5f;
                MovementLandingSurfaceHit hit;
                if (!MovementSafeLandingCompat.TryFindLandingSurfaceForTesting(analysis, out hit) || hit == null || !hit.Found)
                {
                    throw new InvalidOperationException("Expected landing surface solver to find a projected horizontal-motion contact.");
                }

                AssertStringEquals(hit.ContactTileX.ToString(CultureInfo.InvariantCulture), "7", "projected motion contact tile x");
                if (hit.ProjectedPlayerLeftX <= analysis.PositionX + 8f)
                {
                    throw new InvalidOperationException("Expected projected contact to win over current vertical column, got left X " + hit.ProjectedPlayerLeftX.ToString(CultureInfo.InvariantCulture));
                }
            });
        }

        private static void SafeLandingJumpInputProfileReadsGrappleShootSpeed()
        {
            var player = new FakePlayer();
            player.miscEquips[4] = new FakeItem
            {
                type = 84,
                stack = 1,
                Name = "Grappling Hook",
                shoot = 13,
                shootSpeed = 17.5f
            };
            player.inventory[0] = new FakeItem
            {
                type = 185,
                stack = 1,
                Name = "Inventory Hook",
                shoot = 13,
                shootSpeed = 9.5f
            };

            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile))
            {
                throw new InvalidOperationException("Expected jump input profile to read fake player: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (!profile.HasEquippedGrapple || !profile.HasInventoryGrapple)
            {
                throw new InvalidOperationException("Expected equipped and inventory grapples to be detected.");
            }

            AssertNear(profile.EquippedGrappleShootSpeed, 17.5f, "equipped grapple shoot speed");
            AssertNear(profile.InventoryGrappleShootSpeed, 9.5f, "inventory grapple shoot speed");
            AssertStringEquals(profile.EquippedGrappleProjectileType.ToString(CultureInfo.InvariantCulture), "13", "equipped grapple projectile type");
            AssertStringEquals(profile.InventoryGrappleProjectileType.ToString(CultureInfo.InvariantCulture), "13", "inventory grapple projectile type");
        }

        private static MovementSafeLandingAnalysis AnalysisForLandingSurfaceProbe()
        {
            return new MovementSafeLandingAnalysis
            {
                PlayerControllable = true,
                PositionX = 80f,
                PositionY = 80f,
                Width = 20,
                Height = 42,
                GravityDirection = 1f,
                FallingSpeed = 10f,
                VelocityX = 0f
            };
        }

        private static MovementSafeLandingAnalysis AnalysisForMotionAwareGrappleSurface()
        {
            var analysis = AnalysisNearGround();
            analysis.HasInventoryGrapple = true;
            analysis.InventoryGrappleItemType = 2585;
            analysis.GrappleHookSpeed = 13f;
            analysis.ImpactFound = true;
            analysis.ImpactTicks = 12f;
            analysis.ImpactDistancePixels = 120;
            analysis.PositionX = 90f;
            analysis.PositionY = 80f;
            analysis.Width = 20;
            analysis.Height = 42;
            analysis.FallingSpeed = 10f;
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 180f;
            analysis.LandingSurfaceKnown = true;
            analysis.LandingContactWorldX = 100f;
            analysis.LandingContactWorldY = 160f;
            analysis.LandingContactTileX = 6;
            analysis.LandingContactTileY = 10;
            analysis.LandingSurfaceKind = "full_block";
            analysis.LandingSlopeType = 0;
            analysis.LandingSlopeDirection = "none";
            analysis.LandingContactSample = "center_foot";
            analysis.LandingProjectedPlayerLeftX = 90f;
            analysis.LandingProjectedPlayerRightX = 110f;
            analysis.LandingProjectedPlayerBottomY = 180f;
            return analysis;
        }

        private static void WithSafeLandingTileMap(Action action)
        {
            var previousTiles = Terraria.Main.tile;
            var previousSolid = Terraria.Main.tileSolid;
            var previousSolidTop = Terraria.Main.tileSolidTop;
            try
            {
                MovementSafeLandingCompat.SetMainTypeForTesting(typeof(Terraria.Main));
                MovementSafeLandingCompat.ResetCollisionFastPathCachesForTesting();
                Terraria.Main.tile = new object[30, 30];
                Terraria.Main.tileSolid = new bool[1000];
                Terraria.Main.tileSolidTop = new bool[1000];
                Terraria.Main.tileSolid[1] = true;
                Terraria.Main.tileSolid[19] = true;
                Terraria.Main.tileSolidTop[19] = true;
                action();
            }
            finally
            {
                Terraria.Main.tile = previousTiles;
                Terraria.Main.tileSolid = previousSolid;
                Terraria.Main.tileSolidTop = previousSolidTop;
                MovementSafeLandingCompat.SetMainTypeForTesting(null);
                MovementSafeLandingCompat.ResetCollisionFastPathCachesForTesting();
            }
        }

        private static void TravelMenuDiagnosticsCloneKeepsScopedHookFields()
        {
            var info = new TravelMenuDiagnosticInfo
            {
                ScopedPowerHookInstalled = true,
                ScopedPowerHookMessage = "installed",
                ScopedApplyCount = 7,
                ScopedRestoreCount = 6,
                ScopedCleanupCount = 1
            };

            var clone = info.Clone();
            if (!clone.ScopedPowerHookInstalled ||
                !string.Equals(clone.ScopedPowerHookMessage, "installed", StringComparison.Ordinal) ||
                clone.ScopedApplyCount != 7 ||
                clone.ScopedRestoreCount != 6 ||
                clone.ScopedCleanupCount != 1)
            {
                throw new InvalidOperationException("Travel menu scoped diagnostics were not preserved by Clone().");
            }
        }

        private static void TravelMenuItemCheckGuardSuppressesWorldUseAndRestoresClick()
        {
            ResetFakeMainMouse(true, true);
            var player = new FakePlayer
            {
                controlUseItem = true,
                releaseUseItem = false,
                channel = true
            };

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            string message;
            if (!TravelMenuCompat.TryBeginScopedCreativeUiWorldItemUseGuard(player, out takeover, out message))
            {
                throw new InvalidOperationException("Expected travel menu ItemCheck guard to apply: " + message);
            }

            if (player.controlUseItem ||
                !player.releaseUseItem ||
                !player.channel ||
                Terraria.Main.mouseLeft ||
                !Terraria.Main.mouseLeftRelease ||
                takeover == null ||
                !takeover.Applied ||
                takeover.Pressed ||
                !string.Equals(takeover.ScopeName, "TravelMenu.CreativeUI.ItemCheck", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected travel menu ItemCheck guard to suppress only scoped world use input.");
            }

            if (!TravelMenuCompat.TryRestoreScopedCreativeUiWorldItemUseGuard(takeover, out message))
            {
                throw new InvalidOperationException("Expected travel menu ItemCheck guard to restore: " + message);
            }

            if (!player.controlUseItem ||
                player.releaseUseItem ||
                !player.channel ||
                !Terraria.Main.mouseLeft ||
                !Terraria.Main.mouseLeftRelease)
            {
                throw new InvalidOperationException("Expected travel menu ItemCheck guard restore to preserve the original click for CreativeUI.Update.");
            }
        }

        private static void TravelMenuCreativeUiWorldInputGuardDoesNotOverrideMouseState()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousCreativeMenu = Terraria.Main.CreativeMenu;
            var previousPlayerInventory = Terraria.Main.playerInventory;
            try
            {
                Terraria.Main.CreativeMenu = new Terraria.CreativeMenuStub { Enabled = true };
                Terraria.Main.playerInventory = true;
                ResetFakeMainMouse(true, true);
                Terraria.Main.mouseRight = true;
                Terraria.Main.mouseRightRelease = true;
                var player = new FakePlayer
                {
                    controlUseItem = true,
                    releaseUseItem = false
                };

                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Source = TravelMenuScopedJourneySource.JueMingZTravelMenuScope,
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryApplyCreativeUiWorldInputGuard(state, out message))
                {
                    throw new InvalidOperationException("Expected travel menu CreativeUI world input guard to apply: " + message);
                }

                if (player.controlUseItem ||
                    !player.releaseUseItem ||
                    !Terraria.Main.mouseLeft ||
                    !Terraria.Main.mouseLeftRelease ||
                    !Terraria.Main.mouseRight ||
                    !Terraria.Main.mouseRightRelease)
                {
                    throw new InvalidOperationException("Expected travel menu CreativeUI world input guard to preserve Main mouse button state while suppressing use-item controls.");
                }
            }
            finally
            {
                Terraria.Main.CreativeMenu = previousCreativeMenu;
                Terraria.Main.playerInventory = previousPlayerInventory;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiWorldInputGuardSkipsNativeJourneyScope()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousCreativeMenu = Terraria.Main.CreativeMenu;
            var previousPlayerInventory = Terraria.Main.playerInventory;
            try
            {
                Terraria.Main.CreativeMenu = new Terraria.CreativeMenuStub { Enabled = true };
                Terraria.Main.playerInventory = true;
                var player = new FakePlayer
                {
                    controlUseItem = true,
                    releaseUseItem = false
                };

                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Source = TravelMenuScopedJourneySource.NativeJourneyCreativeUiScope,
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (TravelMenuCompat.TryApplyCreativeUiWorldInputGuard(state, out message))
                {
                    throw new InvalidOperationException("Expected native Journey CreativeUI world input guard to skip.");
                }

                if (!player.controlUseItem || player.releaseUseItem)
                {
                    throw new InvalidOperationException("Expected native Journey CreativeUI scope to leave use-item flags untouched.");
                }
            }
            finally
            {
                Terraria.Main.CreativeMenu = previousCreativeMenu;
                Terraria.Main.playerInventory = previousPlayerInventory;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiWorldInputGuardRequiresInventory()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousCreativeMenu = Terraria.Main.CreativeMenu;
            var previousPlayerInventory = Terraria.Main.playerInventory;
            try
            {
                Terraria.Main.CreativeMenu = new Terraria.CreativeMenuStub { Enabled = true };
                Terraria.Main.playerInventory = false;
                var player = new FakePlayer
                {
                    controlUseItem = true,
                    releaseUseItem = false
                };

                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Source = TravelMenuScopedJourneySource.JueMingZTravelMenuScope,
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (TravelMenuCompat.TryApplyCreativeUiWorldInputGuard(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI world input guard to skip while inventory is closed.");
                }

                if (!player.controlUseItem || player.releaseUseItem)
                {
                    throw new InvalidOperationException("Expected closed-inventory CreativeUI scope to leave use-item flags untouched.");
                }
            }
            finally
            {
                Terraria.Main.CreativeMenu = previousCreativeMenu;
                Terraria.Main.playerInventory = previousPlayerInventory;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiDrawRestorePreservesVanillaMouseInterface()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(_ => false);
                SetFakeMouseOverTravelMenuToggle();
                ResetFakeMainMouse(true, true);
                var player = new FakePlayer { mouseInterface = false };
                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw input bypass to apply: " + message);
                }

                player.mouseInterface = true;

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw input bypass to restore: " + message);
                }

                if (!player.mouseInterface)
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw restore to preserve vanilla mouseInterface=true set by the hovered toggle.");
                }
            }
            finally
            {
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(null);
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiDrawReleasePulseIgnoresOtherInventoryButtons()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(_ => false);
                SetFakeMouseAwayFromTravelMenuToggle();
                ResetFakeMainMouse(true, true);
                var player = new FakePlayer();
                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw input bypass to apply away from toggle: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw to preserve mouseLeftRelease while the cursor is away from the travel menu toggle.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw input bypass to restore away from toggle: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI Draw restore to leave non-travel-menu inventory button clicks usable.");
                }
            }
            finally
            {
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(null);
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiInputBypassClearsUsingItemGate()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(_ => false);
                ResetFakeMainMouse(false, false);
                Terraria.Main.mouseRightRelease = false;
                Terraria.Main.mouseInterface = true;
                Terraria.Main.blockMouse = true;
                var player = new FakePlayer
                {
                    mouseInterface = true,
                    itemAnimation = 12,
                    reuseDelay = 3,
                    channel = true,
                    pendingItemReuse = true
                };

                var state = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI input bypass to apply: " + message);
                }

                if (player.mouseInterface ||
                    player.itemAnimation != 0 ||
                    player.reuseDelay != 0 ||
                    player.channel ||
                    player.pendingItemReuse ||
                    Terraria.Main.mouseLeftRelease ||
                    Terraria.Main.mouseRightRelease ||
                    Terraria.Main.mouseInterface ||
                    Terraria.Main.blockMouse)
                {
                    throw new InvalidOperationException("Expected CreativeUI input bypass to clear PlayerInput.IgnoreMouseInterface sources without inventing a click when mouse is up.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(state, out message))
                {
                    throw new InvalidOperationException("Expected CreativeUI input bypass to restore: " + message);
                }

                if (!player.mouseInterface ||
                    player.itemAnimation != 12 ||
                    player.reuseDelay != 3 ||
                    !player.channel ||
                    !player.pendingItemReuse ||
                    Terraria.Main.mouseLeftRelease ||
                    Terraria.Main.mouseRightRelease ||
                    !Terraria.Main.mouseInterface ||
                    !Terraria.Main.blockMouse)
                {
                    throw new InvalidOperationException("Expected CreativeUI input bypass restore to preserve original using-item state.");
                }
            }
            finally
            {
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(null);
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuCreativeUiReleasePulseIsOncePerMouseHold()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                SetFakeMouseOverTravelMenuToggle();
                ResetFakeMainMouse(true, false);
                Terraria.Main.mouseRight = false;
                Terraria.Main.mouseRightRelease = false;
                var player = new FakePlayer();

                var firstState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };

                string message;
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(firstState, out message))
                {
                    throw new InvalidOperationException("Expected first CreativeUI release pulse to apply: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected first held-frame CreativeUI scope to expose one release pulse.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(firstState, out message))
                {
                    throw new InvalidOperationException("Expected first CreativeUI release pulse to restore: " + message);
                }

                if (Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected release pulse to be consumed after first scope while mouse remains held.");
                }

                var heldState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };

                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(heldState, out message))
                {
                    throw new InvalidOperationException("Expected held CreativeUI scope to apply: " + message);
                }

                if (Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected held mouse to stay release-suppressed until physical mouse up.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(heldState, out message))
                {
                    throw new InvalidOperationException("Expected held CreativeUI scope to restore: " + message);
                }

                ResetFakeMainMouse(false, true);
                var resetState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(resetState, out message))
                {
                    throw new InvalidOperationException("Expected mouse-up CreativeUI scope to apply: " + message);
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(resetState, out message))
                {
                    throw new InvalidOperationException("Expected mouse-up CreativeUI scope to restore: " + message);
                }

                ResetFakeMainMouse(true, false);
                var secondClickState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(secondClickState, out message))
                {
                    throw new InvalidOperationException("Expected second click CreativeUI scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected a new physical press after mouse-up to expose another release pulse.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(secondClickState, out message))
                {
                    throw new InvalidOperationException("Expected second click CreativeUI scope to restore: " + message);
                }

                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                SetFakeMouseOverTravelMenuToggle();
                ResetFakeMainMouse(false, false);
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(virtualKey => virtualKey == 0x01);
                var physicalHeldState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(physicalHeldState, out message))
                {
                    throw new InvalidOperationException("Expected physical-held CreativeUI scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected physical mouse fallback to expose one release pulse when Main.mouseLeft was consumed.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(physicalHeldState, out message))
                {
                    throw new InvalidOperationException("Expected physical-held CreativeUI scope to restore: " + message);
                }

                var physicalStillHeldState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(physicalStillHeldState, out message))
                {
                    throw new InvalidOperationException("Expected still-physical-held CreativeUI scope to apply: " + message);
                }

                if (Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected physical mouse fallback to keep release suppressed while Main.mouseLeft stays consumed but the physical button is still held.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(physicalStillHeldState, out message))
                {
                    throw new InvalidOperationException("Expected still-physical-held CreativeUI scope to restore: " + message);
                }

                ResetFakeMainMouse(false, false);
                var updatePhysicalHeldState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(updatePhysicalHeldState, out message))
                {
                    throw new InvalidOperationException("Expected physical-held CreativeUI.Update scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Update to synthesize held mouse down without inventing a release pulse.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(updatePhysicalHeldState, out message))
                {
                    throw new InvalidOperationException("Expected physical-held CreativeUI.Update scope to restore: " + message);
                }

                if (Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Update physical mouse synthesis to restore original Main mouse state.");
                }

                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(_ => false);
                SetFakeMouseOverTravelMenuToggle();
                ResetFakeMainMouse(false, false);
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
                var triggerHeldDrawState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Draw",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(triggerHeldDrawState, out message))
                {
                    throw new InvalidOperationException("Expected PlayerInput-triggered CreativeUI.Draw scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Draw to synthesize click state from PlayerInput.Triggers.Current when Main.mouseLeft was consumed.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(triggerHeldDrawState, out message))
                {
                    throw new InvalidOperationException("Expected PlayerInput-triggered CreativeUI.Draw scope to restore: " + message);
                }

                if (Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease || !Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Draw PlayerInput-trigger synthesis to restore original trigger and Main mouse state.");
                }

                ResetFakeMainMouse(false, false);
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = false;
                Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = true;
                var justPressedUpdateState = new TravelMenuScopedJourneyState
                {
                    Scope = "CreativeUI.Update",
                    Player = player,
                    LocalPlayer = player
                };
                if (!TravelMenuCompat.TryBeginScopedCreativeUiInputBypass(justPressedUpdateState, out message))
                {
                    throw new InvalidOperationException("Expected JustPressed CreativeUI.Update scope to apply: " + message);
                }

                if (!Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease || !Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft)
                {
                    throw new InvalidOperationException("Expected CreativeUI.Update to preserve a consumed short click from PlayerInput.Triggers.JustPressed without inventing release.");
                }

                if (!TravelMenuCompat.TryRestoreScopedCreativeUiInputBypass(justPressedUpdateState, out message))
                {
                    throw new InvalidOperationException("Expected JustPressed CreativeUI.Update scope to restore: " + message);
                }

                if (Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease || Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft)
                {
                    throw new InvalidOperationException("Expected JustPressed CreativeUI.Update synthesis to restore original trigger and Main mouse state.");
                }
            }
            finally
            {
                ResetFakeMainMouse(false, true);
                TravelMenuCompat.SetCreativeUiMouseButtonDownFallbackOverrideForTests(null);
                TravelMenuCompat.ResetCreativeUiReleasePulseState();
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuGodmodePowerReadUsesCreativeFlagFallback()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousMyPlayer = Terraria.Main.myPlayer;
            try
            {
                var player = new FakePlayer
                {
                    whoAmI = 7,
                    creativeGodMode = true
                };

                Terraria.Main.myPlayer = player.whoAmI;
                Terraria.Main.LocalPlayer = player;

                bool enabled;
                string message;
                if (!TravelMenuCompat.TryReadGodmodePowerEnabled(out enabled, out message))
                {
                    throw new InvalidOperationException("Expected godmode power read to fall back to LocalPlayer.creativeGodMode: " + message);
                }

                if (!enabled)
                {
                    throw new InvalidOperationException("Expected godmode power read to report enabled when LocalPlayer.creativeGodMode=true.");
                }
            }
            finally
            {
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.myPlayer = previousMyPlayer;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuGodmodePowerReadDisabledWhenCreativeFlagOff()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousMyPlayer = Terraria.Main.myPlayer;
            try
            {
                var player = new FakePlayer
                {
                    whoAmI = 11,
                    creativeGodMode = false
                };

                Terraria.Main.myPlayer = player.whoAmI;
                Terraria.Main.LocalPlayer = player;

                bool enabled;
                string message;
                if (!TravelMenuCompat.TryReadGodmodePowerEnabled(out enabled, out message))
                {
                    throw new InvalidOperationException("Expected godmode power read fallback to succeed when LocalPlayer.creativeGodMode=false: " + message);
                }

                if (enabled)
                {
                    throw new InvalidOperationException("Expected godmode power read to report disabled when LocalPlayer.creativeGodMode=false.");
                }
            }
            finally
            {
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.myPlayer = previousMyPlayer;
                restoreRuntimeTypes();
            }
        }

        private static void TravelMenuStateStoreRecoversActiveMarkerFromBackup()
        {
            var restoreConfigDirectory = PushTemporaryConfigDirectory("travel-menu-state-store");
            try
            {
                ResetTravelMenuStateStoreCache();
                var context = new TravelMenuContext
                {
                    PlayerPath = @"C:\Players\TestPlayer.plr",
                    WorldPath = @"C:\Worlds\TestWorld.wld",
                    PlayerName = "TestPlayer",
                    WorldName = "TestWorld",
                    PlayerDifficulty = 0,
                    WorldGameMode = 0,
                    MainGameMode = 0
                };

                TravelMenuStateStore.UpsertActiveMarker(context, "enabled");

                TravelMenuRestoreMarker marker;
                if (!TravelMenuStateStore.TryFindActiveMarker(context, out marker) || marker == null)
                {
                    throw new InvalidOperationException("Expected active travel menu restore marker after enable.");
                }

                var statePath = TravelMenuStateStore.StatePath;
                var backupPath = statePath + ".bak";
                if (!File.Exists(statePath) || !File.Exists(backupPath))
                {
                    throw new InvalidOperationException("Expected travel menu state primary and backup files to exist.");
                }

                File.WriteAllText(statePath, "{corrupt");
                ResetTravelMenuStateStoreCache();

                if (!TravelMenuStateStore.TryFindActiveMarker(context, out marker) || marker == null)
                {
                    throw new InvalidOperationException("Expected active travel menu restore marker to recover from backup when primary is corrupt.");
                }

                if (marker.OriginalPlayerDifficulty != context.PlayerDifficulty ||
                    marker.OriginalWorldGameMode != context.WorldGameMode ||
                    marker.OriginalMainGameMode != context.MainGameMode)
                {
                    throw new InvalidOperationException("Expected recovered travel menu marker to preserve original player and world modes.");
                }

                TravelMenuStateStore.MarkRestored(context, "restored");
                ResetTravelMenuStateStoreCache();
                if (TravelMenuStateStore.TryFindActiveMarker(context, out marker))
                {
                    throw new InvalidOperationException("Expected restored travel menu marker to become inactive.");
                }
            }
            finally
            {
                ResetTravelMenuStateStoreCache();
                restoreConfigDirectory();
            }
        }

        private static void SetFakeMouseOverTravelMenuToggle()
        {
            Terraria.Main.mouseX = 40;
            Terraria.Main.mouseY = 280;
            Terraria.Main.screenHeight = 800;
            Terraria.Main.inventoryScale = 1f;
        }

        private static void SetFakeMouseAwayFromTravelMenuToggle()
        {
            Terraria.Main.mouseX = 500;
            Terraria.Main.mouseY = 500;
            Terraria.Main.screenHeight = 800;
            Terraria.Main.inventoryScale = 1f;
        }

        private static Action PushFakeTerrariaMainType()
        {
            var gameModeMainField = typeof(GameMode).GetField("_cachedMainType", BindingFlags.Static | BindingFlags.NonPublic);
            var runtimeMainField = typeof(TerrariaRuntimeTypes).GetField("_mainType", BindingFlags.Static | BindingFlags.NonPublic);
            if (gameModeMainField == null || runtimeMainField == null)
            {
                throw new InvalidOperationException("Terraria runtime type cache field missing.");
            }

            var previousGameModeMain = gameModeMainField.GetValue(null);
            var previousRuntimeMain = runtimeMainField.GetValue(null);
            gameModeMainField.SetValue(null, typeof(Terraria.Main));
            runtimeMainField.SetValue(null, null);
            return () =>
            {
                runtimeMainField.SetValue(null, previousRuntimeMain);
                gameModeMainField.SetValue(null, previousGameModeMain);
            };
        }

        private static Action PushTemporaryConfigDirectory(string prefix)
        {
            var configDirectoryField = typeof(ConfigService).GetField("<ConfigDirectory>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var appSettingsPathField = typeof(ConfigService).GetField("<AppSettingsPath>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var featureSettingsPathField = typeof(ConfigService).GetField("<FeatureSettingsPath>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var hotkeySettingsPathField = typeof(ConfigService).GetField("<HotkeySettingsPath>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            if (configDirectoryField == null || appSettingsPathField == null || featureSettingsPathField == null || hotkeySettingsPathField == null)
            {
                throw new InvalidOperationException("ConfigService path backing fields missing.");
            }

            var previousConfigDirectory = (string)configDirectoryField.GetValue(null);
            var previousAppSettingsPath = (string)appSettingsPathField.GetValue(null);
            var previousFeatureSettingsPath = (string)featureSettingsPathField.GetValue(null);
            var previousHotkeySettingsPath = (string)hotkeySettingsPathField.GetValue(null);
            var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".test-config", prefix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            configDirectoryField.SetValue(null, root);
            appSettingsPathField.SetValue(null, Path.Combine(root, "appsettings.json"));
            featureSettingsPathField.SetValue(null, Path.Combine(root, "features.json"));
            hotkeySettingsPathField.SetValue(null, Path.Combine(root, "hotkeys.json"));

            return () =>
            {
                configDirectoryField.SetValue(null, previousConfigDirectory);
                appSettingsPathField.SetValue(null, previousAppSettingsPath);
                featureSettingsPathField.SetValue(null, previousFeatureSettingsPath);
                hotkeySettingsPathField.SetValue(null, previousHotkeySettingsPath);
                TryDeleteTemporaryConfigDirectory(root);
            };
        }

        private static void ResetTravelMenuStateStoreCache()
        {
            var stateField = typeof(TravelMenuStateStore).GetField("_state", BindingFlags.Static | BindingFlags.NonPublic);
            if (stateField == null)
            {
                throw new InvalidOperationException("TravelMenuStateStore state cache field missing.");
            }

            stateField.SetValue(null, null);
        }

        private static void TryDeleteTemporaryConfigDirectory(string path)
        {
            try
            {
                var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".test-config");
                var fullPath = Path.GetFullPath(path);
                var fullBasePath = Path.GetFullPath(basePath);
                if (fullPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                }
            }
            catch
            {
            }
        }

        private static void TemporaryMountsRequireCapability()
        {
            var flying = Record("temporary_flying_mount", "flying_mount", "quick_mount");
            var safe = Record("temporary_safe_mount", "safe_mount", "quick_mount");
            var analysis = Analysis();
            AssertUnavailable(flying, analysis, "flyingMountUnavailable");
            analysis.HasEquippedFlyingMount = true;
            AssertAvailable(flying, analysis);

            analysis = Analysis();
            AssertUnavailable(safe, analysis, "safeMountUnavailable");
            analysis.HasEquippedSafeMount = true;
            AssertAvailable(safe, analysis);
        }

        private static void TemporaryGravityGlobeRequiresCapability()
        {
            var record = Record("temporary_gravity_globe", "gravity_globe", "gravity_flip");
            var analysis = Analysis();
            analysis.AerialJumpWindow = true;
            AssertUnavailable(record, analysis, "gravityFlipUnavailable");
            analysis.HasGravityFlipOpportunity = true;
            AssertAvailable(record, analysis);
        }

        private static void TemporaryGravityGlobeAcceptsPostApplyCapabilityEvidence()
        {
            var record = Record("temporary_gravity_globe", "gravity_globe", "gravity_flip");
            record.PostApplyCapabilityObserved = true;
            record.PostApplyVerificationReason = "gravityRestorePendingExpected";

            var analysis = Analysis();
            analysis.AerialJumpWindow = true;
            AssertAvailable(record, analysis);
        }

        private static void EquippedDoubleJumpWaitsForNearGroundDistance()
        {
            var analysis = Analysis();
            analysis.SelectedStrategyId = "equipped_double_jump";
            analysis.SelectedActionType = "jump";
            analysis.ImpactTicks = 3f;
            analysis.FallingSpeed = 10f;
            analysis.ImpactDistancePixels = 120;

            string reason;
            if (MovementSafeLandingTiming.IsEquippedRescueWindowReady(analysis, out reason))
            {
                throw new InvalidOperationException("Expected equipped double jump to wait while impact distance is still high.");
            }

            AssertContains(reason, "impactDistanceTooFar");
            analysis.ImpactTicks = 3f;
            analysis.ImpactDistancePixels = 32;
            if (!MovementSafeLandingTiming.IsEquippedRescueWindowReady(analysis, out reason))
            {
                throw new InvalidOperationException("Expected equipped double jump near ground to be ready, got " + reason);
            }
        }

        private static void TemporaryDoubleJumpApplyKeepsPreactivationWindow()
        {
            var source = new FakeItem { type = 857, stack = 1, prefix = 0, Name = "Cloud in a Bottle" };
            var target = new FakeItem { type = 0, stack = 0, prefix = 0, Name = string.Empty };
            var plan = BuildPlan(
                "temporary_double_jump",
                "double_jump",
                "jump",
                MovementSafeLandingEquipmentContainerKind.SocialAccessory,
                13,
                MovementSafeLandingEquipmentContainerKind.Accessory,
                3,
                source,
                target,
                true);
            var analysis = Analysis();
            analysis.ImpactTicks = 10f;
            analysis.FallingSpeed = 10f;
            analysis.ImpactDistancePixels = 100;

            string reason;
            if (!MovementSafeLandingTiming.IsTemporaryApplyWindowReady(plan, analysis, out reason))
            {
                throw new InvalidOperationException("Expected temporary double jump apply preactivation window to be ready, got " + reason);
            }

            analysis.ImpactDistancePixels = 140;
            if (MovementSafeLandingTiming.IsTemporaryApplyWindowReady(plan, analysis, out reason))
            {
                throw new InvalidOperationException("Expected temporary double jump apply to wait when still far from ground.");
            }

            AssertContains(reason, "impactDistanceTooFar");
        }

        private static void TemporaryDoubleJumpActivationWaitsForNearGroundDistance()
        {
            var record = Record("temporary_double_jump", "double_jump", "jump");
            var analysis = Analysis();
            analysis.ImpactTicks = 3f;
            analysis.FallingSpeed = 10f;
            analysis.ImpactDistancePixels = 120;

            string reason;
            if (MovementSafeLandingTiming.IsTemporaryActivationWindowReady(record, analysis, out reason))
            {
                throw new InvalidOperationException("Expected temporary double jump activation to wait while impact distance is still high.");
            }

            AssertContains(reason, "impactDistanceTooFar");
            analysis.ImpactTicks = 3f;
            analysis.ImpactDistancePixels = 32;
            if (!MovementSafeLandingTiming.IsTemporaryActivationWindowReady(record, analysis, out reason))
            {
                throw new InvalidOperationException("Expected temporary double jump near ground activation to be ready, got " + reason);
            }
        }

        private static void FlyingCarpetActivationWaitsForLowerNearGroundDistance()
        {
            var equipped = Analysis();
            equipped.SelectedStrategyId = "equipped_flying_carpet";
            equipped.SelectedActionType = "jump";
            equipped.ImpactTicks = 2.5f;
            equipped.FallingSpeed = 10f;
            equipped.ImpactDistancePixels = 48;

            string reason;
            if (MovementSafeLandingTiming.IsEquippedRescueWindowReady(equipped, out reason))
            {
                throw new InvalidOperationException("Expected equipped flying carpet to wait at three-tile distance.");
            }

            AssertContains(reason, "impactDistanceTooFar");
            equipped.ImpactDistancePixels = 32;
            if (!MovementSafeLandingTiming.IsEquippedRescueWindowReady(equipped, out reason))
            {
                throw new InvalidOperationException("Expected equipped flying carpet to trigger around two tiles, got " + reason);
            }

            var temporary = Record("temporary_flying_carpet", "flying_carpet", "jump");
            equipped.ImpactDistancePixels = 48;
            if (MovementSafeLandingTiming.IsTemporaryActivationWindowReady(temporary, equipped, out reason))
            {
                throw new InvalidOperationException("Expected temporary flying carpet activation to wait at three-tile distance.");
            }

            AssertContains(reason, "impactDistanceTooFar");
        }

        private static void GravityGlobeActivationStartsBeforeLanding()
        {
            var analysis = Analysis();
            analysis.SelectedStrategyId = "equipped_gravity_globe";
            analysis.SelectedActionType = "gravity_flip";
            analysis.ImpactTicks = 3.5f;
            analysis.FallingSpeed = 10f;
            analysis.ImpactDistancePixels = 40;

            string reason;
            if (!MovementSafeLandingTiming.IsEquippedRescueWindowReady(analysis, out reason))
            {
                throw new InvalidOperationException("Expected gravity globe to activate before the last landing moment, got " + reason);
            }

            analysis.ImpactDistancePixels = 80;
            if (MovementSafeLandingTiming.IsEquippedRescueWindowReady(analysis, out reason))
            {
                throw new InvalidOperationException("Expected gravity globe to wait while the reset point is still too far from the landing surface.");
            }

            AssertContains(reason, "impactDistanceTooFar");
        }

        private static void SafeLandingJumpPulseCanStartFromPressWhenReleasePrimed()
        {
            var requestId = Guid.NewGuid();
            string message;
            if (!MovementSafeLandingCompat.QueueSafeLandingJumpPulse(
                    requestId,
                    "equipped_double_jump",
                    "jump",
                    false,
                    false,
                    2,
                    true,
                    out message))
            {
                throw new InvalidOperationException("Expected direct press pulse to queue: " + message);
            }

            SafeLandingJumpPulseSnapshot snapshot;
            if (!MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(requestId, out snapshot) || snapshot == null)
            {
                throw new InvalidOperationException("Expected direct press pulse snapshot.");
            }

            try
            {
                if (!string.Equals(snapshot.Phase, "PressHold", StringComparison.Ordinal) || snapshot.ReleaseApplied)
                {
                    throw new InvalidOperationException("Expected direct press pulse to start at PressHold without release.");
                }
            }
            finally
            {
                MovementSafeLandingCompat.CancelSafeLandingJumpPulse(requestId, "test cleanup");
            }
        }

        private static void SafeLandingQuickMountPulseStartsFromPressWithImmediateCancel()
        {
            var requestId = Guid.NewGuid();
            string message;
            if (!MovementSafeLandingCompat.QueueSafeLandingJumpPulse(
                    requestId,
                    "active_flying_mount",
                    "quick_mount",
                    false,
                    true,
                    1,
                    true,
                    true,
                    out message))
            {
                throw new InvalidOperationException("Expected quick mount pulse to queue: " + message);
            }

            SafeLandingJumpPulseSnapshot snapshot;
            if (!MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(requestId, out snapshot) || snapshot == null)
            {
                throw new InvalidOperationException("Expected quick mount pulse snapshot.");
            }

            try
            {
                if (!string.Equals(snapshot.Phase, "PressHold", StringComparison.Ordinal) ||
                    snapshot.ReleaseApplied ||
                    snapshot.HoldTargetTicks != 1 ||
                    !snapshot.ImmediateCancelAfterPress)
                {
                    throw new InvalidOperationException("Expected quick mount pulse to start from PressHold with one-tick immediate cancel.");
                }

                var player = new FakePlayer();
                if (!MovementSafeLandingCompat.ApplyQueuedSafeLandingJumpPulseBeforePlayerUpdate(player))
                {
                    throw new InvalidOperationException("Expected quick mount activation press to apply.");
                }

                MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(requestId, out snapshot);
                if (snapshot == null ||
                    !snapshot.PressApplied ||
                    !string.Equals(snapshot.Phase, "FinalRelease", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected quick mount activation press to move to FinalRelease.");
                }

                if (!MovementSafeLandingCompat.ApplyQueuedSafeLandingJumpPulseBeforePlayerUpdate(player))
                {
                    throw new InvalidOperationException("Expected quick mount activation release to apply.");
                }

                MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(requestId, out snapshot);
                if (snapshot == null ||
                    !snapshot.FinalReleaseApplied ||
                    !string.Equals(snapshot.Phase, "CancelPressHold", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected quick mount activation release to continue into immediate cancel press.");
                }

                if (!MovementSafeLandingCompat.ApplyQueuedSafeLandingJumpPulseBeforePlayerUpdate(player))
                {
                    throw new InvalidOperationException("Expected quick mount cancel press to apply.");
                }

                MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(requestId, out snapshot);
                if (snapshot == null ||
                    !snapshot.CancelPressApplied ||
                    !string.Equals(snapshot.Phase, "CancelFinalRelease", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected quick mount cancel press to move to cancel final release.");
                }

                if (!MovementSafeLandingCompat.ApplyQueuedSafeLandingJumpPulseBeforePlayerUpdate(player))
                {
                    throw new InvalidOperationException("Expected quick mount cancel release to apply.");
                }

                MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(requestId, out snapshot);
                if (snapshot == null ||
                    !snapshot.Completed ||
                    !snapshot.CancelFinalReleaseApplied ||
                    snapshot.Active)
                {
                    throw new InvalidOperationException("Expected quick mount immediate cancel pulse to complete.");
                }
            }
            finally
            {
                MovementSafeLandingCompat.CancelSafeLandingJumpPulse(requestId, "test cleanup");
            }
        }

        private static void TemporaryDoubleJumpApplyRefreshesFunctionalEffect()
        {
            var player = new FakePlayer();
            var source = new FakeItem { type = 857, stack = 1, prefix = 0, Name = "Cloud in a Bottle" };
            var target = new FakeItem { type = 0, stack = 0, prefix = 0, Name = string.Empty };
            player.armor[13] = source;
            player.armor[3] = target;

            var result = InvokeApplyPlan(player, BuildPlan(
                "temporary_double_jump",
                "double_jump",
                "jump",
                MovementSafeLandingEquipmentContainerKind.SocialAccessory,
                13,
                MovementSafeLandingEquipmentContainerKind.Accessory,
                3,
                source,
                target,
                true));

            if (result == null || result.AppliedMoveCount != 1)
            {
                throw new InvalidOperationException("Expected temporary double jump to apply.");
            }

            if (!result.FunctionalRefreshAttempted || !result.FunctionalRefreshSucceeded)
            {
                throw new InvalidOperationException("Expected ApplyEquipFunctional refresh to succeed.");
            }

            if (!result.DoubleJumpRefreshAttempted || !result.DoubleJumpRefreshSucceeded)
            {
                throw new InvalidOperationException("Expected RefreshDoubleJumps to succeed.");
            }

            if (player.AppliedSlot != 3 || !object.ReferenceEquals(player.AppliedItem, source) || player.RefreshDoubleJumpsCount != 1)
            {
                throw new InvalidOperationException("Expected fake player refresh methods to observe the swapped double jump item.");
            }

            AssertContains(result.FunctionalRefreshMessage, "applyEquipFunctionalInvoked");
            AssertContains(result.FunctionalRefreshMessage, "refreshDoubleJumpsInvoked");
        }

        private static void TemporaryPassiveAccessoryApplyRefreshesFunctionalEffect()
        {
            var player = new FakePlayer();
            var source = new FakeItem { type = 158, stack = 1, prefix = 0, Name = "Lucky Horseshoe" };
            var target = new FakeItem { type = 0, stack = 0, prefix = 0, Name = string.Empty };
            player.armor[13] = source;
            player.armor[3] = target;

            var result = InvokeApplyPlan(player, BuildPlan(
                "temporary_horseshoe",
                "horseshoe",
                "equip_only",
                MovementSafeLandingEquipmentContainerKind.SocialAccessory,
                13,
                MovementSafeLandingEquipmentContainerKind.Accessory,
                3,
                source,
                target,
                false));

            if (result == null || result.AppliedMoveCount != 1)
            {
                throw new InvalidOperationException("Expected temporary passive accessory to apply.");
            }

            if (!result.FunctionalRefreshAttempted || !result.FunctionalRefreshSucceeded)
            {
                throw new InvalidOperationException("Expected passive accessory ApplyEquipFunctional refresh to succeed.");
            }

            if (result.DoubleJumpRefreshAttempted || result.DoubleJumpRefreshSucceeded)
            {
                throw new InvalidOperationException("Expected non-double-jump accessory not to refresh double jumps.");
            }

            if (!result.PulseNotRequired || player.AppliedSlot != 3 || !object.ReferenceEquals(player.AppliedItem, source))
            {
                throw new InvalidOperationException("Expected equip-only accessory to refresh without input pulse.");
            }
        }

        private static void TemporaryPassiveAccessoryApplyWaitsForTighterNearGroundWindow()
        {
            var plan = PlanForCategory("temporary_horseshoe", "horseshoe", "equip_only", 2);
            var analysis = Analysis();
            analysis.ImpactTicks = 8f;
            analysis.FallingSpeed = 10f;
            analysis.ImpactDistancePixels = 96;

            string reason;
            if (MovementSafeLandingTiming.IsTemporaryApplyWindowReady(plan, analysis, out reason))
            {
                throw new InvalidOperationException("Expected passive temporary accessory to wait while still far from ground.");
            }

            AssertContains(reason, "impactTicksTooFar");
            analysis.ImpactTicks = 4f;
            analysis.ImpactDistancePixels = 48;
            if (!MovementSafeLandingTiming.IsTemporaryApplyWindowReady(plan, analysis, out reason))
            {
                throw new InvalidOperationException("Expected passive temporary accessory near ground apply to be ready, got " + reason);
            }
        }

        private static void TemporaryUmbrellaPlanTargetsSelectedHotbarSlot()
        {
            var player = new FakePlayer();
            player.selectedItem = 0;
            player.inventory[0] = new FakeItem { type = 1, stack = 1, prefix = 0, Name = "Copper Shortsword" };
            player.inventory[20] = new FakeItem { type = 946, stack = 1, prefix = 0, Name = "Umbrella" };

            MovementSafeLandingEquipmentPlan plan;
            string message;
            if (!MovementSafeLandingEquipmentCompat.TryBuildTemporaryEquipmentPlan(player, AppSettings.CreateDefault(), Analysis(), out plan, out message))
            {
                throw new InvalidOperationException("Expected umbrella plan, got " + message);
            }

            if (plan == null ||
                !string.Equals(plan.StrategyId, "temporary_umbrella", StringComparison.Ordinal) ||
                !string.Equals(plan.EquipmentCategory, "umbrella", StringComparison.Ordinal) ||
                plan.SelectedPriority != 3 ||
                plan.SourceSlot != 20 ||
                plan.TargetContainerKind != MovementSafeLandingEquipmentContainerKind.Hotbar ||
                plan.TargetSlot != 0 ||
                plan.ApplyTriggersInput)
            {
                throw new InvalidOperationException("Unexpected umbrella plan shape.");
            }
        }

        private static void TemporaryUmbrellaDoesNotTriggerWhileAlreadyHeld()
        {
            var player = new FakePlayer();
            player.selectedItem = 0;
            player.inventory[0] = new FakeItem { type = 946, stack = 1, prefix = 0, Name = "Umbrella" };
            player.inventory[20] = new FakeItem { type = 4707, stack = 1, prefix = 0, Name = "Tragic Umbrella" };

            MovementSafeLandingEquipmentPlan plan;
            string message;
            if (MovementSafeLandingEquipmentCompat.TryBuildTemporaryEquipmentPlan(player, AppSettings.CreateDefault(), Analysis(), out plan, out message))
            {
                throw new InvalidOperationException("Expected no umbrella plan while selected item is already an umbrella.");
            }
        }

        private static void TemporaryUmbrellaApplyWaitsForNearGroundDistance()
        {
            var source = new FakeItem { type = 946, stack = 1, prefix = 0, Name = "Umbrella" };
            var target = new FakeItem { type = 1, stack = 1, prefix = 0, Name = "Copper Shortsword" };
            var plan = BuildPlan(
                "temporary_umbrella",
                "umbrella",
                "equip_only",
                MovementSafeLandingEquipmentContainerKind.Inventory,
                20,
                MovementSafeLandingEquipmentContainerKind.Hotbar,
                0,
                source,
                target,
                false);
            plan.SelectedPriority = 3;

            var analysis = Analysis();
            analysis.ImpactTicks = 3f;
            analysis.FallingSpeed = 10f;
            analysis.ImpactDistancePixels = 64;

            string reason;
            if (MovementSafeLandingTiming.IsTemporaryApplyWindowReady(plan, analysis, out reason))
            {
                throw new InvalidOperationException("Expected temporary umbrella apply to wait when still far from ground.");
            }

            AssertContains(reason, "impactDistanceTooFar");
            analysis.ImpactDistancePixels = 32;
            if (!MovementSafeLandingTiming.IsTemporaryApplyWindowReady(plan, analysis, out reason))
            {
                throw new InvalidOperationException("Expected temporary umbrella near ground apply to be ready, got " + reason);
            }
        }

        private static void TemporaryUmbrellaApplySwapsIntoSelectedHotbarSlot()
        {
            var player = new FakePlayer();
            player.selectedItem = 0;
            var umbrella = new FakeItem { type = 946, stack = 1, prefix = 0, Name = "Umbrella" };
            var originalHeld = new FakeItem { type = 1, stack = 1, prefix = 0, Name = "Copper Shortsword" };
            player.inventory[20] = umbrella;
            player.inventory[0] = originalHeld;

            var result = InvokeApplyPlan(player, BuildPlan(
                "temporary_umbrella",
                "umbrella",
                "equip_only",
                MovementSafeLandingEquipmentContainerKind.Inventory,
                20,
                MovementSafeLandingEquipmentContainerKind.Hotbar,
                0,
                umbrella,
                originalHeld,
                false));

            if (result == null || result.AppliedMoveCount != 1)
            {
                throw new InvalidOperationException("Expected temporary umbrella to apply.");
            }

            if (!object.ReferenceEquals(player.inventory[0], umbrella) ||
                !object.ReferenceEquals(player.inventory[20], originalHeld) ||
                player.selectedItem != 0)
            {
                throw new InvalidOperationException("Expected umbrella to be swapped into the selected hotbar slot.");
            }

            if (!result.SelectedSlotApplyAttempted || !result.SelectedSlotApplySucceeded || !result.PulseNotRequired)
            {
                throw new InvalidOperationException("Expected umbrella apply to select the target slot without input pulse.");
            }

            if (result.FunctionalRefreshAttempted || result.DoubleJumpRefreshAttempted)
            {
                throw new InvalidOperationException("Expected umbrella hand-held swap not to run equipment refresh.");
            }
        }

        private static void SafeLandingStrategyCatalogPreservesPriorityOrder()
        {
            var settings = AppSettings.CreateDefault();
            var analysis = AnalysisNearGround();
            analysis.AlreadySafe = true;
            analysis.SafeReason = "wings";
            analysis.HasAirJump = true;
            var context = MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis);
            context.TemporaryEquipmentPlan = PlanForCategory("temporary_horseshoe", "horseshoe", "equip_only", 2);
            var selection = MovementSafeLandingStrategyCatalog.Evaluate(context);
            AssertSelected(selection, 0, "already_safe", "none");

            analysis = AnalysisNearGround();
            analysis.HasAirJump = true;
            selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));
            AssertSelected(selection, 1, "equipped_double_jump", "jump");

            analysis = AnalysisNearGround();
            context = MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis);
            context.TemporaryEquipmentPlan = PlanForCategory("temporary_horseshoe", "horseshoe", "equip_only", 2);
            selection = MovementSafeLandingStrategyCatalog.Evaluate(context);
            AssertSelected(selection, 2, "temporary_horseshoe", "equip_only");

            analysis = AnalysisNearGround();
            context = MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis);
            context.TemporaryEquipmentPlan = PlanForCategory("temporary_umbrella", "umbrella", "equip_only", 3);
            selection = MovementSafeLandingStrategyCatalog.Evaluate(context);
            AssertSelected(selection, 3, "temporary_umbrella", "equip_only");
        }

        private static void PriorityOneEquippedDoubleJumpGeneratesJumpPlan()
        {
            var analysis = AnalysisNearGround();
            analysis.HasAirJump = true;
            AssertPriorityOnePlan(analysis, "equipped_double_jump", "jump");
        }

        private static void PriorityOneEquippedRocketBootsGeneratesJumpPlan()
        {
            var analysis = AnalysisNearGround();
            analysis.HasRocketBootsAvailable = true;
            AssertPriorityOnePlan(analysis, "equipped_rocket_boots", "jump");
        }

        private static void PriorityOneEquippedFlyingCarpetGeneratesJumpPlan()
        {
            var analysis = AnalysisNearGround();
            analysis.HasFlyingCarpetAvailable = true;
            AssertPriorityOnePlan(analysis, "equipped_flying_carpet", "jump");
        }

        private static void PriorityOneEquippedFlyingMountGeneratesQuickMountPlan()
        {
            var analysis = AnalysisNearGround();
            analysis.HasEquippedFlyingMount = true;
            AssertPriorityOnePlan(analysis, "equipped_flying_mount", "quick_mount");
        }

        private static void PriorityOneEquippedGravityGlobeGeneratesGravityFlipPlan()
        {
            var settings = AppSettings.CreateDefault();
            settings.MovementSafeLandingGravityGlobeEnabled = true;
            var analysis = AnalysisNearGround();
            analysis.HasGravityGlobe = true;
            analysis.HasGravityFlipOpportunity = true;
            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));
            AssertSelected(selection, 1, "equipped_gravity_globe", "gravity_flip");
            if (selection.SelectedPlan == null || !selection.SelectedPlan.RequiresRestore)
            {
                throw new InvalidOperationException("Expected gravity globe plan to require restore.");
            }
        }

        private static void PriorityOneGravityGlobeUsesDedicatedOption()
        {
            var settings = AppSettings.CreateDefault();
            settings.MovementSafeLandingGravityGlobeEnabled = true;
            settings.MovementSafeLandingGravityPotionEnabled = false;
            var analysis = AnalysisNearGround();
            analysis.HasGravityGlobe = true;
            analysis.HasGravityFlipOpportunity = true;
            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));
            AssertSelected(selection, 1, "equipped_gravity_globe", "gravity_flip");

            settings.MovementSafeLandingGravityGlobeEnabled = false;
            settings.MovementSafeLandingGravityPotionEnabled = true;
            selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));
            AssertSelected(selection, 1, "equipped_gravity_globe", "gravity_flip");
        }

        private static void PriorityOneGravityGlobeOutranksMounts()
        {
            var settings = AppSettings.CreateDefault();
            settings.MovementSafeLandingGravityGlobeEnabled = true;
            settings.MovementSafeLandingFlyingMountEnabled = true;
            settings.MovementSafeLandingDamageReductionMountEnabled = true;

            var analysis = AnalysisNearGround();
            analysis.HasGravityGlobe = true;
            analysis.HasGravityFlipOpportunity = true;
            analysis.HasActiveFlyingMount = true;
            analysis.HasEquippedFlyingMount = true;
            analysis.HasEquippedSafeMount = true;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));
            AssertSelected(selection, 1, "equipped_gravity_globe", "gravity_flip");
            AssertContains(selection.CandidateSummary, "equipped_flying_mount");
            AssertContains(selection.CandidateSummary, "equipped_safe_mount");
        }

        private static void SafeLandingMountCancelClearsWhenAlreadyUnmounted()
        {
            string reason;
            if (!MovementSafeLandingService.ShouldClearSafeLandingMountCancelForTesting(new JumpInputProfile(), out reason) ||
                !string.Equals(reason, "mountAlreadyInactive", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected mount cancel pending state to clear once the mount is already inactive.");
            }

            var mountedProfile = new JumpInputProfile
            {
                PlayerActive = true,
                MountActive = true
            };
            if (MovementSafeLandingService.ShouldClearSafeLandingMountCancelForTesting(mountedProfile, out reason))
            {
                throw new InvalidOperationException("Expected mount cancel pending state to remain while the mount is still active.");
            }

            if (MovementSafeLandingService.ShouldClearSafeLandingMountCancelForTesting(null, out reason) ||
                !string.Equals(reason, "jumpProfileUnavailable", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unavailable jump profile not to be treated as a completed mount cancel.");
            }
        }

        private static void SafeLandingMountCancelImminentCollisionBypassesStableWait()
        {
            var profile = new JumpInputProfile
            {
                PlayerActive = true,
                MountActive = true,
                GravityDirection = 1f,
                VelocityY = 12f
            };

            bool safeToCancel;
            bool requiresStableWait;
            string reason;
            if (!MovementSafeLandingService.TryEvaluateSafeLandingMountCancelImminentForTesting(
                    profile,
                    true,
                    36,
                    3f,
                    out safeToCancel,
                    out requiresStableWait,
                    out reason))
            {
                throw new InvalidOperationException("Expected mount cancel imminent evaluator to return successfully.");
            }

            if (!safeToCancel || requiresStableWait)
            {
                throw new InvalidOperationException("Expected imminent landing collision to cancel mount without stable wait. safeToCancel=" +
                                                    safeToCancel.ToString() +
                                                    ",requiresStableWait=" +
                                                    requiresStableWait.ToString() +
                                                    ",reason=" +
                                                    (reason ?? string.Empty));
            }

            AssertContains(reason, "imminentLandingCollision");
        }

        private static void SafeLandingMountCancelWaitsWhenCollisionIsDistant()
        {
            var profile = new JumpInputProfile
            {
                PlayerActive = true,
                MountActive = true,
                GravityDirection = 1f,
                VelocityY = 12f
            };

            bool safeToCancel;
            bool requiresStableWait;
            string reason;
            if (!MovementSafeLandingService.TryEvaluateSafeLandingMountCancelImminentForTesting(
                    profile,
                    true,
                    240,
                    20f,
                    out safeToCancel,
                    out requiresStableWait,
                    out reason))
            {
                throw new InvalidOperationException("Expected mount cancel imminent evaluator to return successfully.");
            }

            if (safeToCancel || !requiresStableWait)
            {
                throw new InvalidOperationException("Expected distant collision to keep mount cancel pending.");
            }

            AssertContains(reason, "waitingForImminentLandingCollision");
        }

        private static void PriorityTwoTemporaryHorseshoeGeneratesInventoryApplyPlan()
        {
            AssertTemporaryApplyRequest(PlanForCategory("temporary_horseshoe", "horseshoe", "equip_only", 2), "horseshoe");
        }

        private static void PriorityTwoTemporaryWingsGeneratesInventoryApplyPlan()
        {
            AssertTemporaryApplyRequest(PlanForCategory("temporary_wings", "wings", "equip_only", 2), "wings");
        }

        private static void PriorityTwoTemporaryFairyBootsTargetsLegEquipmentSlot()
        {
            var player = new FakePlayer();
            player.armor[10] = new FakeItem { type = 3770, stack = 1, prefix = 0, Name = "Djinn's Curse" };
            player.armor[2] = new FakeItem { type = 0, stack = 0, prefix = 0, Name = string.Empty };

            MovementSafeLandingEquipmentPlan plan;
            string message;
            if (!MovementSafeLandingEquipmentCompat.TryBuildTemporaryEquipmentPlan(player, AppSettings.CreateDefault(), AnalysisNearGround(), out plan, out message))
            {
                throw new InvalidOperationException("Expected fairy boots plan, got " + message);
            }

            if (plan == null ||
                !string.Equals(plan.EquipmentCategory, "fairy_boots", StringComparison.Ordinal) ||
                plan.TargetContainerKind != MovementSafeLandingEquipmentContainerKind.Accessory ||
                plan.TargetSlot != 2)
            {
                throw new InvalidOperationException("Expected fairy boots to target leg equipment slot 2.");
            }
        }

        private static void PriorityTwoTemporaryDoubleJumpRecordsRefreshExpectation()
        {
            var player = new FakePlayer();
            player.controlJump = false;
            player.canJumpAgainCloud = true;
            var source = new FakeItem { type = 857, stack = 1, prefix = 0, Name = "Cloud in a Bottle" };
            var target = new FakeItem { type = 0, stack = 0, prefix = 0, Name = string.Empty };
            player.armor[13] = source;
            player.armor[3] = target;
            var result = InvokeApplyPlan(player, BuildPlan(
                "temporary_double_jump",
                "double_jump",
                "jump",
                MovementSafeLandingEquipmentContainerKind.SocialAccessory,
                13,
                MovementSafeLandingEquipmentContainerKind.Accessory,
                3,
                source,
                target,
                true));

            if (result == null || !result.DoubleJumpRefreshAttempted || !result.DoubleJumpRefreshSucceeded)
            {
                throw new InvalidOperationException("Expected temporary double jump apply to record RefreshDoubleJumps.");
            }

            AssertContains(result.PostApplyVerificationSummary, "airJumpFlagCount=");
        }

        private static void PriorityTwoTemporaryRocketBootsDetectsTerrasparkFallbackId()
        {
            if (!MovementSafeLandingEquipmentCompat.IsKnownItemTypeForDiagnostics("rocket_boots", 5000))
            {
                throw new InvalidOperationException("Expected Terraspark Boots fallback id 5000 to be recognized as rocket boots.");
            }
        }

        private static void PriorityTwoTemporaryRocketBootsRecordsPostApplyVerification()
        {
            var player = new FakePlayer
            {
                controlJump = false,
                rocketBoots = 1,
                rocketTime = 0,
                rocketDelay = 0,
                canRocket = true,
                rocketRelease = true,
                bootFlyingAbilities = true,
                velocity = new FakeVector2 { X = 0f, Y = 9f }
            };
            var source = new FakeItem { type = 5000, stack = 1, prefix = 0, Name = "Terraspark Boots" };
            var target = new FakeItem { type = 0, stack = 0, prefix = 0, Name = string.Empty };
            player.armor[13] = source;
            player.armor[3] = target;

            var result = InvokeApplyPlan(player, BuildPlan(
                "temporary_rocket_boots",
                "rocket_boots",
                "jump",
                MovementSafeLandingEquipmentContainerKind.SocialAccessory,
                13,
                MovementSafeLandingEquipmentContainerKind.Accessory,
                3,
                source,
                target,
                true));

            if (result == null || !result.PostApplyHasRocketBootsAvailable || result.PostApplyRocketTime <= 0f)
            {
                throw new InvalidOperationException("Expected rocket boots post-apply capability and rocketTime to be available.");
            }

            AssertContains(result.PostApplyVerificationSummary, "rocketBoots=");
            AssertContains(result.PostApplyVerificationSummary, "rocketTime=");
            AssertContains(result.PostApplyVerificationSummary, "canRocket=");
            AssertContains(result.PostApplyVerificationSummary, "rocketRelease=");
            AssertContains(result.PostApplyVerificationSummary, "rocketBootsAvailable");
            AssertContains(result.FunctionalRefreshMessage, "controlledLocalRocketTimePrime");
        }

        private static void TemporaryRocketBootsPulseKeepsActiveHoldTicks()
        {
            var requestId = Guid.NewGuid();
            string message;
            if (!MovementSafeLandingCompat.QueueSafeLandingJumpPulse(
                    requestId,
                    "temporary_rocket_boots",
                    "jump",
                    true,
                    false,
                    16,
                    out message))
            {
                throw new InvalidOperationException("Expected temporary rocket boots pulse to queue: " + message);
            }

            SafeLandingJumpPulseSnapshot snapshot;
            if (!MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(requestId, out snapshot) || snapshot == null)
            {
                throw new InvalidOperationException("Expected temporary rocket boots pulse snapshot.");
            }

            try
            {
                if (snapshot.HoldTargetTicks != 16)
                {
                    throw new InvalidOperationException("Expected temporary rocket boots hold target 16, got " + snapshot.HoldTargetTicks);
                }
            }
            finally
            {
                MovementSafeLandingCompat.CancelSafeLandingJumpPulse(requestId, "test cleanup");
            }
        }

        private static void GravityFlipPulseStartsFromPress()
        {
            var requestId = Guid.NewGuid();
            string message;
            if (!MovementSafeLandingCompat.QueueSafeLandingJumpPulse(
                requestId,
                "safe_landing_gravity_restore",
                "gravity_flip",
                false,
                false,
                1,
                out message))
            {
                throw new InvalidOperationException("Queue gravity restore pulse failed: " + message);
            }

            try
            {
                SafeLandingJumpPulseSnapshot snapshot;
                if (!MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(requestId, out snapshot) || snapshot == null)
                {
                    throw new InvalidOperationException("Expected queued gravity restore pulse snapshot.");
                }

                if (!string.Equals(snapshot.Phase, "PressHold", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected gravity flip pulse to start from PressHold, got " + snapshot.Phase);
                }

                if (snapshot.ReleaseApplied)
                {
                    throw new InvalidOperationException("Expected gravity flip pulse to skip the synthetic release stage.");
                }

                if (snapshot.HoldTargetTicks != 1)
                {
                    throw new InvalidOperationException("Expected gravity flip hold target 1, got " + snapshot.HoldTargetTicks);
                }
            }
            finally
            {
                MovementSafeLandingCompat.CancelSafeLandingJumpPulse(requestId, "test cleanup");
            }
        }

        private static void SafeLandingGrapplePulseStartsFromPressWithTarget()
        {
            var requestId = Guid.NewGuid();
            string message;
            if (!MovementSafeLandingCompat.QueueSafeLandingJumpPulse(
                    requestId,
                    "inventory_grapple",
                    "grapple",
                    false,
                    false,
                    1,
                    true,
                    false,
                    true,
                    123.5f,
                    456.25f,
                    out message))
            {
                throw new InvalidOperationException("Queue grapple pulse failed: " + message);
            }

            try
            {
                SafeLandingJumpPulseSnapshot snapshot;
                if (!MovementSafeLandingCompat.TryGetSafeLandingJumpPulseSnapshot(requestId, out snapshot) || snapshot == null)
                {
                    throw new InvalidOperationException("Expected queued grapple pulse snapshot.");
                }

                if (!string.Equals(snapshot.Phase, "PressHold", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected grapple pulse to start from PressHold, got " + snapshot.Phase);
                }

                if (!snapshot.TargetWorldKnown ||
                    Math.Abs(snapshot.TargetWorldX - 123.5f) > 0.001f ||
                    Math.Abs(snapshot.TargetWorldY - 456.25f) > 0.001f)
                {
                    throw new InvalidOperationException("Expected grapple pulse to retain target world coordinates.");
                }

                if (snapshot.HoldTargetTicks != 1)
                {
                    throw new InvalidOperationException("Expected grapple hold target 1, got " + snapshot.HoldTargetTicks);
                }
            }
            finally
            {
                MovementSafeLandingCompat.CancelSafeLandingJumpPulse(requestId, "test cleanup");
            }
        }

        private static void SimulatedJumpPulsePressesAfterReleaseWithoutFinalRelease()
        {
            var requestId = Guid.NewGuid();
            string message;
            if (!MovementSimulatedJumpPulseCompat.QueueSimulatedJumpPulse(requestId, "wings", false, out message))
            {
                throw new InvalidOperationException("Queue simulated jump pulse failed: " + message);
            }

            var player = new FakePlayer
            {
                controlJump = true,
                releaseJump = false,
                wingsLogic = 1,
                wingTime = 180f
            };

            try
            {
                if (!MovementSimulatedJumpPulseCompat.ApplyQueuedSimulatedJumpPulseBeforePlayerUpdate(player))
                {
                    throw new InvalidOperationException("Expected simulated jump release phase to apply.");
                }

                if (player.controlJump || !player.releaseJump)
                {
                    throw new InvalidOperationException("Expected release phase to clear controlJump and prime releaseJump.");
                }

                SimulatedJumpPulseSnapshot snapshot;
                if (!MovementSimulatedJumpPulseCompat.TryGetSimulatedJumpPulseSnapshot(requestId, out snapshot) || snapshot == null)
                {
                    throw new InvalidOperationException("Expected simulated jump pulse snapshot after release.");
                }

                if (!snapshot.ReleaseApplied || snapshot.PressApplied || snapshot.Completed || !string.Equals(snapshot.Phase, "Press", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected release phase to advance to Press without completing.");
                }

                if (!MovementSimulatedJumpPulseCompat.ApplyQueuedSimulatedJumpPulseBeforePlayerUpdate(player))
                {
                    throw new InvalidOperationException("Expected simulated jump press phase to apply.");
                }

                if (!player.controlJump || !player.releaseJump)
                {
                    throw new InvalidOperationException("Expected press phase to restore controlJump while preserving releaseJump edge.");
                }

                if (!MovementSimulatedJumpPulseCompat.TryGetSimulatedJumpPulseSnapshot(requestId, out snapshot) || snapshot == null)
                {
                    throw new InvalidOperationException("Expected simulated jump pulse snapshot after press.");
                }

                if (!snapshot.Completed || snapshot.Failed || !snapshot.PressApplied || !string.Equals(snapshot.Phase, "Completed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected press phase to complete the pulse without a final release.");
                }

                if (MovementSimulatedJumpPulseCompat.ApplyQueuedSimulatedJumpPulseBeforePlayerUpdate(player))
                {
                    throw new InvalidOperationException("Expected completed simulated jump pulse to stay terminal.");
                }

                if (!player.controlJump)
                {
                    throw new InvalidOperationException("Completed simulated jump pulse should not apply a final release over held flight.");
                }
            }
            finally
            {
                MovementSimulatedJumpPulseCompat.CancelSimulatedJumpPulse(requestId, "test cleanup");
            }
        }

        private static void PriorityTwoTemporaryGravityGlobeRecordsRestoreExpectation()
        {
            var player = new FakePlayer
            {
                controlJump = false,
                gravControl2 = true,
                gravDir = 1f,
                velocity = new FakeVector2 { X = 0f, Y = 8f }
            };
            var source = new FakeItem { type = 1131, stack = 1, prefix = 0, Name = "Gravity Globe" };
            var target = new FakeItem { type = 0, stack = 0, prefix = 0, Name = string.Empty };
            player.armor[13] = source;
            player.armor[3] = target;

            var result = InvokeApplyPlan(player, BuildPlan(
                "temporary_gravity_globe",
                "gravity_globe",
                "gravity_flip",
                MovementSafeLandingEquipmentContainerKind.SocialAccessory,
                13,
                MovementSafeLandingEquipmentContainerKind.Accessory,
                3,
                source,
                target,
                true));

            if (result == null || !result.PostApplyHasGravityGlobe)
            {
                throw new InvalidOperationException("Expected gravity globe post-apply snapshot.");
            }

            AssertContains(result.PostApplyVerificationSummary, "gravityRestorePendingExpected");
            AssertContains(result.PostApplyVerificationSummary, "hasGravityFlipOpportunity=");
            AssertContains(result.PostApplyVerificationSummary, "gravityDirection=");
        }

        private static void GravityGlobeDefaultMigrationEnablesOption()
        {
            var settings = AppSettings.CreateDefault();
            settings.MovementSafeLandingGravityGlobeEnabled = false;
            settings.MovementSafeLandingGravityPotionEnabled = false;
            settings.MovementSafeLandingGravityGlobeDefaultMigrated = false;
            InvokeMigrateAppSettings(settings);

            if (!MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.GravityGlobe))
            {
                throw new InvalidOperationException("Expected gravity globe migration to enable the dedicated option.");
            }

            settings.MovementSafeLandingGravityGlobeEnabled = false;
            settings.MovementSafeLandingGravityPotionEnabled = false;
            settings.MovementSafeLandingGravityGlobeDefaultMigrated = true;
            InvokeMigrateAppSettings(settings);
            if (MovementSafeLandingOptionCatalog.GetEnabled(settings, MovementSafeLandingOptionCatalog.GravityGlobe))
            {
                throw new InvalidOperationException("Expected explicit post-migration gravity globe disable to be preserved.");
            }
        }

        private static void PriorityThreeUmbrellaRequestTargetsHotbarWithoutActivationPulse()
        {
            var plan = PlanForCategory("temporary_umbrella", "umbrella", "equip_only", 3);
            plan.SourceContainerKind = MovementSafeLandingEquipmentContainerKind.Inventory;
            plan.SourceSlot = 20;
            plan.TargetContainerKind = MovementSafeLandingEquipmentContainerKind.Hotbar;
            plan.TargetSlot = 0;
            plan.ApplyTriggersInput = false;
            var request = MovementSafeLandingRequestBuilder.BuildTemporaryEquipmentApplyRequest(plan, AnalysisNearGround());
            AssertMetadata(request, "SafeLandingEquipmentTargetKind", "Hotbar");
            AssertMetadata(request, "SafeLandingEquipmentTargetSlot", "0");
            AssertMetadata(request, "SafeLandingActionType", "equip_only");
            AssertMetadata(request, "SafeLandingRescueMode", "TemporaryEquipmentApply");
            if (request.Metadata.ContainsKey("SafeLandingTemporaryEquipmentActivation"))
            {
                throw new InvalidOperationException("Umbrella apply request should not create an activation pulse marker.");
            }
        }

        private static void PriorityFourInventoryGrappleGeneratesGrapplePlan()
        {
            var analysis = AnalysisNearGround();
            analysis.HasInventoryGrapple = true;
            analysis.InventoryGrappleItemType = 84;
            analysis.ImpactTicks = 20f;
            analysis.ImpactDistancePixels = 220;
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 280f;
            analysis.PositionX = 80f;
            analysis.PositionY = 80f;
            analysis.Width = 20;
            analysis.Height = 42;
            analysis.VelocityX = 2f;
            analysis.GrappleHookSpeed = 13f;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 4, "inventory_grapple", "grapple");
            if (selection.SelectedPlan == null ||
                selection.SelectedPlan.RequestKind != InputActionKind.Jump ||
                (selection.SelectedPlan.RequiredChannels & InputActionChannel.Grapple) == 0 ||
                (selection.SelectedPlan.RequiredChannels & InputActionChannel.MouseTarget) == 0)
            {
                throw new InvalidOperationException("Expected priority 4 grapple plan to use Jump with Grapple and MouseTarget channels.");
            }

            var request = MovementSafeLandingRequestBuilder.BuildJumpRequest(selection.SelectedPlan, analysis, "test", InputActionPriority.High);
            AssertMetadata(request, "SafeLandingStrategy", "inventory_grapple");
            AssertMetadata(request, "SafeLandingActionType", "grapple");
            AssertMetadata(request, "SafeLandingPriority", "4");
            AssertMetadata(request, "SafeLandingGrappleTargetWorldX", "100");
            AssertMetadata(request, "SafeLandingGrappleTargetSource", "fallback_impact");
        }

        private static void PriorityFourGrappleWaitsBehindPriorityOne()
        {
            var analysis = AnalysisNearGround();
            analysis.HasAirJump = true;
            analysis.HasInventoryGrapple = true;
            analysis.InventoryGrappleItemType = 84;
            analysis.ImpactTicks = 3f;
            analysis.ImpactDistancePixels = 32;
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 200f;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 1, "equipped_double_jump", "jump");
            AssertContains(selection.CandidateSummary, "inventory_grapple");
        }

        private static void PriorityFourGrappleTargetsLandingSurfaceSlopeContact()
        {
            var analysis = AnalysisNearGround();
            analysis.HasInventoryGrapple = true;
            analysis.InventoryGrappleItemType = 2585;
            analysis.GrappleHookSpeed = 13f;
            analysis.ImpactFound = true;
            analysis.ImpactTicks = 16f;
            analysis.ImpactDistancePixels = 160;
            analysis.PositionX = 80f;
            analysis.PositionY = 80f;
            analysis.Width = 20;
            analysis.Height = 42;
            analysis.FallingSpeed = 10f;
            analysis.VelocityX = -4f;
            analysis.ImpactWorldX = 90f;
            analysis.ImpactWorldY = 240f;
            analysis.LandingSurfaceKnown = true;
            analysis.LandingContactWorldX = 82f;
            analysis.LandingContactWorldY = 160f;
            analysis.LandingContactTileX = 5;
            analysis.LandingContactTileY = 10;
            analysis.LandingSurfaceKind = "slope";
            analysis.LandingSlopeType = 1;
            analysis.LandingSlopeDirection = "left_high_right_low";
            analysis.LandingContactSample = "left_foot";
            analysis.LandingMovingIntoSlope = true;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 4, "inventory_grapple", "grapple");
            if (selection.SelectedEvaluation == null)
                throw new InvalidOperationException("Expected grapple evaluation.");
            if (selection.SelectedPlan == null)
                throw new InvalidOperationException("Expected grapple plan.");

            var request = MovementSafeLandingRequestBuilder.BuildJumpRequest(selection.SelectedPlan, analysis, "test", InputActionPriority.High);
            AssertMetadata(request, "SafeLandingPriority", "4");
            AssertMetadata(request, "SafeLandingGrappleTargetSource", "landing_surface_slope_into_motion");
            AssertMetadata(request, "SafeLandingGrappleTargetFromLandingSurface", "true");

            var targetX = analysis.GrappleTargetWorldX;
            var targetY = analysis.GrappleTargetWorldY;
            if (System.Math.Abs(targetX - analysis.LandingContactWorldX) > 12f)
                throw new InvalidOperationException("Expected grapple target X near landing contact, got " + targetX + " vs " + analysis.LandingContactWorldX);
            if (System.Math.Abs(targetY - analysis.LandingContactWorldY - 4f) > 8f)
                throw new InvalidOperationException("Expected grapple target Y near landing contact+4, got " + targetY + " vs " + (analysis.LandingContactWorldY + 4f));
        }

        private static void PriorityFourGrappleFlatSurfaceFollowsRightMotion()
        {
            var analysis = AnalysisForMotionAwareGrappleSurface();
            analysis.VelocityX = 4f;
            analysis.LandingContactWorldX = 91f;
            analysis.LandingContactSample = "left_foot";

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 4, "inventory_grapple", "grapple");

            if (analysis.GrappleTargetWorldX <= analysis.ImpactWorldX)
            {
                throw new InvalidOperationException("Expected rightward motion to aim grapple to the right of impact center, got target " + analysis.GrappleTargetWorldX + " impact " + analysis.ImpactWorldX);
            }
            if (analysis.GrappleTargetWorldX < analysis.LandingProjectedPlayerRightX + 7f)
            {
                throw new InvalidOperationException("Expected rightward motion grapple target to lead past the projected right foot, got target " + analysis.GrappleTargetWorldX + " projected right " + analysis.LandingProjectedPlayerRightX);
            }

            var request = MovementSafeLandingRequestBuilder.BuildJumpRequest(selection.SelectedPlan, analysis, "test", InputActionPriority.High);
            AssertMetadata(request, "SafeLandingGrappleTargetSource", "landing_surface_full_block_motion");
            AssertMetadata(request, "SafeLandingVelocityX", "4");
            AssertMetadata(request, "SafeLandingLandingProjectedPlayerLeftX", "90");
            AssertMetadata(request, "SafeLandingLandingProjectedPlayerRightX", "110");
        }

        private static void PriorityFourGrappleFlatSurfaceFollowsLeftMotion()
        {
            var analysis = AnalysisForMotionAwareGrappleSurface();
            analysis.VelocityX = -4f;
            analysis.LandingContactWorldX = 109f;
            analysis.LandingContactSample = "right_foot";

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 4, "inventory_grapple", "grapple");

            if (analysis.GrappleTargetWorldX >= analysis.ImpactWorldX)
            {
                throw new InvalidOperationException("Expected leftward motion to aim grapple to the left of impact center, got target " + analysis.GrappleTargetWorldX + " impact " + analysis.ImpactWorldX);
            }
            if (analysis.GrappleTargetWorldX > analysis.LandingProjectedPlayerLeftX - 7f)
            {
                throw new InvalidOperationException("Expected leftward motion grapple target to lead past the projected left foot, got target " + analysis.GrappleTargetWorldX + " projected left " + analysis.LandingProjectedPlayerLeftX);
            }
        }

        private static void PriorityFourGrappleWithSlopeUsesGentleMotionBias()
        {
            var analysis = AnalysisForMotionAwareGrappleSurface();
            analysis.VelocityX = 4f;
            analysis.LandingContactWorldX = 91f;
            analysis.LandingContactWorldY = 171f;
            analysis.LandingSurfaceKind = "slope";
            analysis.LandingSlopeType = 1;
            analysis.LandingSlopeDirection = "left_high_right_low";
            analysis.LandingMovingWithSlope = true;
            analysis.LandingContactSample = "left_foot";

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 4, "inventory_grapple", "grapple");

            if (analysis.GrappleTargetWorldX <= analysis.ImpactWorldX + 8f ||
                analysis.GrappleTargetWorldX > analysis.LandingProjectedPlayerRightX + 1f)
            {
                throw new InvalidOperationException("Expected with-slope motion to aim near the moving edge, got " + analysis.GrappleTargetWorldX);
            }

            var request = MovementSafeLandingRequestBuilder.BuildJumpRequest(selection.SelectedPlan, analysis, "test", InputActionPriority.High);
            AssertMetadata(request, "SafeLandingGrappleTargetSource", "landing_surface_slope_with_motion");
            AssertMetadata(request, "SafeLandingLandingMovingWithSlope", "true");
        }

        private static void PriorityFourGrappleUnknownSurfaceUsesMotionLeadAndContactY()
        {
            var analysis = AnalysisForMotionAwareGrappleSurface();
            analysis.VelocityX = 4f;
            analysis.LandingSurfaceKind = "unknown";
            analysis.LandingContactWorldY = 181f;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 4, "inventory_grapple", "grapple");

            if (analysis.GrappleTargetWorldX < analysis.LandingProjectedPlayerRightX + 7f)
            {
                throw new InvalidOperationException("Expected unknown landing surface to keep the horizontal motion lead, got target " + analysis.GrappleTargetWorldX);
            }
            AssertNear(analysis.GrappleTargetWorldY, 185f, "unknown surface target y");
        }

        private static void PriorityFiveTeleportRodWaitsBehindValidGrapple()
        {
            var analysis = AnalysisNearGround();
            analysis.HasInventoryGrapple = true;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 2;
            analysis.TeleportRodItemType = 5335;
            analysis.TeleportRodItemName = "Rod of Harmony";
            analysis.ImpactFound = true;
            analysis.ImpactTicks = 16f;
            analysis.ImpactDistancePixels = 144;
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 200f;
            analysis.GrappleHookSpeed = 13f;
            analysis.FallingSpeed = 9f;
            analysis.PositionX = 80f;
            analysis.PositionY = 80f;
            analysis.Width = 20;
            analysis.Height = 42;
            analysis.GrappleTooLate = false;
            analysis.GrappleTooSlowForDownwardSurface = false;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 4, "inventory_grapple", "grapple");
        }

        private static void PriorityFiveTeleportRodCanFallbackWhenGrappleTooLate()
        {
            var analysis = AnalysisNearGround();
            analysis.HasInventoryGrapple = true;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 2;
            analysis.TeleportRodItemType = 5335;
            analysis.TeleportRodItemName = "Rod of Harmony";
            analysis.ImpactFound = true;
            analysis.ImpactTicks = 2f;
            analysis.ImpactDistancePixels = 18;
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 200f;
            analysis.GrappleHookSpeed = 10f;
            analysis.FallingSpeed = 10f;
            analysis.PositionX = 80f;
            analysis.PositionY = 80f;
            analysis.Width = 20;
            analysis.Height = 42;
            analysis.LandingSurfaceKnown = true;
            analysis.LandingContactWorldX = 84f;
            analysis.LandingContactWorldY = 160f;
            analysis.LandingContactTileX = 5;
            analysis.LandingContactTileY = 10;
            analysis.LandingSurfaceKind = "full_block";

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));

            // Verify grapple is a candidate but NOT selected (too late, blocksLowerPriority=false)
            var grappleFound = false;
            for (var i = 0; i < selection.Evaluations.Count; i++)
            {
                var eval = selection.Evaluations[i];
                if (eval != null && eval.Priority == 4 && string.Equals(eval.StrategyId, "inventory_grapple", StringComparison.Ordinal))
                {
                    grappleFound = true;
                    if (!eval.IsCandidate)
                        throw new InvalidOperationException("Expected grapple to be a candidate.");
                    if (eval.IsReady)
                        throw new InvalidOperationException("Expected grapple to NOT be ready (too late).");
                    if (eval.BlocksLowerPriority)
                        throw new InvalidOperationException("Expected grapple to NOT block lower priority when too late.");
                    break;
                }
            }
            if (!grappleFound)
                throw new InvalidOperationException("Expected grapple evaluation to exist.");

            AssertSelected(selection, 5, "inventory_teleport_rod", "teleport_rod");
        }

        private static void PriorityFiveTeleportRodDoesNotRunWhenGrappleTooEarly()
        {
            var analysis = AnalysisNearGround();
            analysis.HasInventoryGrapple = true;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 2;
            analysis.TeleportRodItemType = 5335;
            analysis.TeleportRodItemName = "Rod of Harmony";
            analysis.ImpactFound = true;
            analysis.ImpactTicks = 20f;
            analysis.ImpactDistancePixels = 200;
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 280f;
            analysis.GrappleHookSpeed = 13f;
            analysis.FallingSpeed = 9f;
            analysis.PositionX = 80f;
            analysis.PositionY = 80f;
            analysis.Width = 20;
            analysis.Height = 42;
            analysis.GrappleTooLate = false;
            analysis.GrappleTooSlowForDownwardSurface = false;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));

            var grappleEval = (MovementSafeLandingStrategyEvaluation)null;
            for (var i = 0; i < selection.Evaluations.Count; i++)
            {
                var eval = selection.Evaluations[i];
                if (eval != null && eval.Priority == 4 && string.Equals(eval.StrategyId, "inventory_grapple", StringComparison.Ordinal))
                {
                    grappleEval = eval;
                    break;
                }
            }
            if (grappleEval == null)
                throw new InvalidOperationException("Expected grapple evaluation to exist.");
            if (!grappleEval.IsCandidate)
                throw new InvalidOperationException("Expected grapple to be a candidate.");
            if (grappleEval.IsReady)
                throw new InvalidOperationException("Expected grapple to NOT be ready (too early).");
            if (!grappleEval.BlocksLowerPriority)
                throw new InvalidOperationException("Expected grapple to block lower priority when too early.");
            if (string.IsNullOrEmpty(grappleEval.SkipReason) || !grappleEval.SkipReason.Contains("grappleTooEarly"))
                throw new InvalidOperationException("Expected skip reason to contain grappleTooEarly, got: " + (grappleEval.SkipReason ?? ""));

            AssertSelected(selection, 4, "inventory_grapple", "grapple");
        }

        private static void PriorityOneStillBlocksLowerPriorityWhenTimingNotReady()
        {
            var analysis = AnalysisNearGround();
            analysis.HasAirJump = true;
            analysis.HasInventoryGrapple = true;
            analysis.InventoryGrappleItemType = 84;
            analysis.ImpactTicks = 6f;
            analysis.ImpactDistancePixels = 48;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));

            // P1 should be selected even if timing not ready
            var p1Found = false;
            for (var i = 0; i < selection.Evaluations.Count; i++)
            {
                var eval = selection.Evaluations[i];
                if (eval != null && eval.Priority == 1 && eval.IsCandidate)
                {
                    p1Found = true;
                    if (!eval.BlocksLowerPriority)
                        throw new InvalidOperationException("Expected P1 to block lower priority.");
                    break;
                }
            }
            if (!p1Found)
                throw new InvalidOperationException("Expected P1 evaluation to exist.");

            AssertSelected(selection, 1, "equipped_double_jump", "jump");
        }

        private static void PriorityFiveTeleportRodGeneratesUsePlan()
        {
            var analysis = AnalysisNearGround();
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 200f;
            analysis.ImpactTicks = 20f;
            analysis.ImpactDistancePixels = 220;
            analysis.GrappleHookSpeed = 13f;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 2;
            analysis.TeleportRodItemType = 5335;
            analysis.TeleportRodItemName = "Rod of Harmony";

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 5, "inventory_teleport_rod", "teleport_rod");
            if (selection.SelectedPlan == null ||
                selection.SelectedPlan.RequestKind != InputActionKind.UseHotbarItem ||
                (selection.SelectedPlan.RequiredChannels & InputActionChannel.MouseTarget) == 0)
            {
                throw new InvalidOperationException("Expected priority 5 teleport rod plan to use UseHotbarItem with MouseTarget.");
            }

            var request = MovementSafeLandingRequestBuilder.BuildTeleportRodRequest(selection.SelectedPlan, analysis);
            if (request.Kind != InputActionKind.UseHotbarItem)
            {
                throw new InvalidOperationException("Expected priority 5 request kind UseHotbarItem.");
            }

            AssertMetadata(request, "SafeLandingStrategy", "inventory_teleport_rod");
            AssertMetadata(request, "SafeLandingActionType", "teleport_rod");
            AssertMetadata(request, "SafeLandingPriority", "5");
            AssertMetadata(request, "SafeLandingTeleportRodItemType", "5335");
            AssertMetadata(request, "SafeLandingTeleportRodItemName", "Rod of Harmony");
            AssertMetadata(request, "SafeLandingTeleportRodInventorySlot", "2");
            AssertMetadata(request, "SafeLandingTeleportTargetTileX", "6");
            AssertMetadata(request, "SafeLandingTeleportTargetTileY", "11");
            AssertMetadata(request, "SafeLandingTeleportTargetWorldX", "104");
            AssertMetadata(request, "SafeLandingTeleportTargetWorldY", "184");
            AssertMetadata(request, "Slot", "2");
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "2");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "104");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "184");
            AssertMetadata(request, "ApplyMainMouseLeftForItemCheck", "true");
            AssertMetadata(request, "SafeLandingRescueMode", "TeleportRod");

            var channelProfile = InputActionChannelResolver.Resolve(request);
            AssertHas(channelProfile.RequiredChannels, InputActionChannel.UseItem, "teleport rod required");
            AssertHas(channelProfile.RequiredChannels, InputActionChannel.HotbarSelection, "teleport rod required");
            AssertHas(channelProfile.RequiredChannels, InputActionChannel.BridgeItemUse, "teleport rod required");
            AssertHas(channelProfile.RequiredChannels, InputActionChannel.MouseTarget, "teleport rod required");
        }

        private static void PriorityFiveTeleportRodUsesDirectInventorySlot()
        {
            var analysis = AnalysisNearGround();
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 200f;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 44;
            analysis.TeleportRodItemType = 1326;
            analysis.TeleportRodItemName = "Rod of Discord";

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 5, "inventory_teleport_rod", "teleport_rod");

            var request = MovementSafeLandingRequestBuilder.BuildTeleportRodRequest(selection.SelectedPlan, analysis);
            AssertMetadata(request, "SafeLandingTeleportRodInventorySlot", "44");
            AssertMetadata(request, "Slot", "44");
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "44");

            if (!TerrariaInputCompat.IsSupportedItemUseSlot(44))
            {
                throw new InvalidOperationException("Expected inventory slot 44 to be a supported vanilla item-use slot.");
            }
        }

        private static void PriorityFiveTeleportRodWaitsBehindGrapple()
        {
            var analysis = AnalysisNearGround();
            analysis.HasInventoryGrapple = true;
            analysis.InventoryGrappleItemType = 84;
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 280f;
            analysis.ImpactTicks = 20f;
            analysis.ImpactDistancePixels = 220;
            analysis.GrappleHookSpeed = 13f;
            analysis.PositionX = 80f;
            analysis.PositionY = 80f;
            analysis.Width = 20;
            analysis.Height = 42;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 2;
            analysis.TeleportRodItemType = 5335;
            analysis.TeleportRodItemName = "Rod of Harmony";

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 4, "inventory_grapple", "grapple");
            AssertContains(selection.CandidateSummary, "inventory_teleport_rod");
            AssertDoesNotContain(selection.RejectedStrategiesSummary, "placeholder_teleport_rod:notImplemented");
        }

        private static void PriorityLowerStrategyWaitsBehindHigherTimingWindow()
        {
            var analysis = AnalysisNearGround();
            analysis.HasAirJump = true;
            analysis.HasInventoryGrapple = true;
            analysis.InventoryGrappleItemType = 84;
            analysis.ImpactTicks = 3f;
            analysis.ImpactDistancePixels = 120;
            analysis.ImpactWorldX = 100f;
            analysis.ImpactWorldY = 200f;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            AssertSelected(selection, 1, "equipped_double_jump", "jump");
            if (selection.SelectedEvaluation == null || selection.SelectedEvaluation.IsReady)
            {
                throw new InvalidOperationException("Expected higher priority candidate to block lower priority until its timing window is ready.");
            }

            AssertContains(selection.CandidateSummary, "equipped_double_jump");
            AssertContains(selection.CandidateSummary, "inventory_grapple");
        }

        private static void SafeLandingSuppressesRepeatedRescueUntilDangerClears()
        {
            MovementSafeLandingService.ResetDescentRescueGuardForTesting();
            try
            {
                var analysis = AnalysisNearGround();
                analysis.HasInventoryGrapple = true;
                MovementSafeLandingService.MarkDescentRescueSubmittedForTesting(
                    10,
                    analysis,
                    4,
                    "inventory_grapple",
                    "grapple");

                string reason;
                if (!MovementSafeLandingService.TrySuppressRepeatedDescentRescueForTesting(analysis, 12, out reason) ||
                    !string.Equals(reason, "sameDescentRescueAlreadySubmitted:4:inventory_grapple:grapple", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected same descent rescue guard to suppress repeated rescue, got " + reason + ".");
                }

                var safeAnalysis = AnalysisNearGround();
                safeAnalysis.Dangerous = false;
                if (MovementSafeLandingService.TrySuppressRepeatedDescentRescueForTesting(safeAnalysis, 13, out reason))
                {
                    throw new InvalidOperationException("Expected non-dangerous analysis to clear same descent rescue guard.");
                }

                if (MovementSafeLandingService.TrySuppressRepeatedDescentRescueForTesting(analysis, 14, out reason))
                {
                    throw new InvalidOperationException("Expected guard to remain clear for the next descent after danger clears.");
                }
            }
            finally
            {
                MovementSafeLandingService.ResetDescentRescueGuardForTesting();
            }
        }

        private static void SafeLandingClearsRepeatedRescueWhenLandingChanges()
        {
            MovementSafeLandingService.ResetDescentRescueGuardForTesting();
            try
            {
                var original = AnalysisNearGround();
                original.PositionX = 90f;
                original.ImpactWorldX = 100f;
                original.ImpactWorldY = 200f;
                MovementSafeLandingService.MarkDescentRescueSubmittedForTesting(
                    10,
                    original,
                    5,
                    "inventory_teleport_rod",
                    "teleport_rod");

                var changed = AnalysisNearGround();
                changed.PositionX = 420f;
                changed.ImpactWorldX = 430f;
                changed.ImpactWorldY = 200f;

                string reason;
                if (MovementSafeLandingService.TrySuppressRepeatedDescentRescueForTesting(changed, 12, out reason))
                {
                    throw new InvalidOperationException("Expected changed landing target to clear same descent rescue guard, got " + reason + ".");
                }
            }
            finally
            {
                MovementSafeLandingService.ResetDescentRescueGuardForTesting();
            }
        }

        private static void SafeLandingTeleportRodRequestStaleWhenLandingChanges()
        {
            var metadata = SafeLandingTeleportRodMetadata(100f, 200f, 104f, 184f, 90f, 120f);
            var current = AnalysisNearGround();
            current.PositionX = 420f;
            current.ImpactWorldX = 430f;
            current.ImpactWorldY = 200f;
            current.HasTeleportTarget = true;
            current.TeleportTargetWorldX = 424f;
            current.TeleportTargetWorldY = 184f;

            string reason;
            if (!MovementSafeLandingService.IsSafeLandingTeleportRodRequestStaleForTesting(current, metadata, out reason) ||
                reason.IndexOf("landingChanged", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected stale teleport rod request when landing changed, got " + reason + ".");
            }
        }

        private static void SafeLandingTeleportRodRequestKeepsSameLanding()
        {
            var metadata = SafeLandingTeleportRodMetadata(100f, 200f, 104f, 184f, 90f, 120f);
            var current = AnalysisNearGround();
            current.PositionX = 96f;
            current.ImpactWorldX = 108f;
            current.ImpactWorldY = 204f;
            current.HasTeleportTarget = true;
            current.TeleportTargetWorldX = 104f;
            current.TeleportTargetWorldY = 184f;

            string reason;
            if (MovementSafeLandingService.IsSafeLandingTeleportRodRequestStaleForTesting(current, metadata, out reason))
            {
                throw new InvalidOperationException("Expected same landing teleport rod request to remain valid, got " + reason + ".");
            }
        }

        private static void SafeLandingRequestBuilderPreservesOldMetadataKeys()
        {
            var analysis = AnalysisNearGround();
            analysis.HasAirJump = true;
            var selection = MovementSafeLandingStrategyCatalog.Evaluate(MovementSafeLandingStrategyContext.FromAnalysis(AppSettings.CreateDefault(), analysis));
            var jumpRequest = MovementSafeLandingRequestBuilder.BuildJumpRequest(selection.SelectedPlan, analysis, "test", InputActionPriority.High);
            AssertPreservedSafeLandingMetadata(jumpRequest);

            var applyRequest = MovementSafeLandingRequestBuilder.BuildTemporaryEquipmentApplyRequest(
                PlanForCategory("temporary_horseshoe", "horseshoe", "equip_only", 2),
                AnalysisNearGround());
            AssertPreservedSafeLandingMetadata(applyRequest);

            var rodAnalysis = AnalysisNearGround();
            rodAnalysis.HasTeleportRod = true;
            rodAnalysis.TeleportRodInventorySlot = 1;
            rodAnalysis.TeleportRodItemType = 5335;
            rodAnalysis.TeleportRodItemName = "Rod of Harmony";
            rodAnalysis.TeleportTargetWorldX = 104f;
            rodAnalysis.TeleportTargetWorldY = 184f;
            rodAnalysis.TeleportTargetTileX = 6;
            rodAnalysis.TeleportTargetTileY = 11;
            rodAnalysis.HasTeleportTarget = true;
            var rodPlan = new MovementSafeLandingRescuePlan
            {
                Priority = 5,
                StrategyId = "inventory_teleport_rod",
                ActionType = "teleport_rod",
                RequestKind = InputActionKind.UseHotbarItem,
                RequestPriority = InputActionPriority.High
            };
            var rodRequest = MovementSafeLandingRequestBuilder.BuildTeleportRodRequest(rodPlan, rodAnalysis);
            AssertPreservedSafeLandingMetadata(rodRequest);
        }

        private static Dictionary<string, string> SafeLandingTeleportRodMetadata(
            float impactWorldX,
            float impactWorldY,
            float teleportWorldX,
            float teleportWorldY,
            float positionX,
            float positionY)
        {
            return new Dictionary<string, string>
            {
                { ActionMetadataKeys.Scenario, ScenarioNames.MovementSafeLanding },
                { "SafeLandingActionType", "teleport_rod" },
                { "SafeLandingRescueMode", "TeleportRod" },
                { "SafeLandingImpactWorldX", impactWorldX.ToString("0.###", CultureInfo.InvariantCulture) },
                { "SafeLandingImpactWorldY", impactWorldY.ToString("0.###", CultureInfo.InvariantCulture) },
                { "SafeLandingTeleportTargetWorldX", teleportWorldX.ToString("0.###", CultureInfo.InvariantCulture) },
                { "SafeLandingTeleportTargetWorldY", teleportWorldY.ToString("0.###", CultureInfo.InvariantCulture) },
                { "SafeLandingPlayerPositionX", positionX.ToString("0.###", CultureInfo.InvariantCulture) },
                { "SafeLandingPlayerPositionY", positionY.ToString("0.###", CultureInfo.InvariantCulture) }
            };
        }

        private static void SafeLandingRecoveryKeepsItemWhenRestoreTargetChanged()
        {
            var player = new FakePlayer();
            var rescue = new FakeItem { type = 158, stack = 1, prefix = 0, Name = "Lucky Horseshoe" };
            var userItem = new FakeItem { type = 946, stack = 1, prefix = 0, Name = "Umbrella" };
            player.armor[3] = userItem;
            player.armor[13] = new FakeItem { type = 0, stack = 0, prefix = 0, Name = string.Empty };

            var record = new MovementSafeLandingEquipmentMoveRecord
            {
                StrategyId = "temporary_horseshoe",
                EquipmentCategory = "horseshoe",
                ActionType = "equip_only",
                SelectedPriority = 2,
                SourceContainerKind = MovementSafeLandingEquipmentContainerKind.SocialAccessory,
                SourceSlot = 13,
                TargetContainerKind = MovementSafeLandingEquipmentContainerKind.Accessory,
                TargetSlot = 3,
                CandidateItemType = rescue.type,
                RescueItemSignature = MovementSafeLandingEquipmentCompat.CreateSignature(rescue),
                OriginalTargetWasAir = true,
                OriginalTargetItemSignature = MovementSafeLandingEquipmentCompat.CreateSignature(new FakeItem()),
                OriginalTargetHoldingContainerKind = MovementSafeLandingEquipmentContainerKind.SocialAccessory,
                OriginalTargetHoldingSlot = 13
            };

            var result = InvokeRestoreRecords(player, new[] { record });
            if (result == null || result.UserChangedManagedSlotCount != 1 || result.RestoredMoveCount != 0)
            {
                throw new InvalidOperationException("Expected restore to defer when managed target slot changed.");
            }

            if (!object.ReferenceEquals(player.armor[3], userItem) || !MovementSafeLandingEquipmentCompat.CreateSignature(player.armor[13]).IsAir)
            {
                throw new InvalidOperationException("Expected restore deferral not to delete or move user items.");
            }
        }

        private static void SafeLandingResolveGrappleHookSpeedPrefersEquippedShootSpeedOverFallbackTable()
        {
            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.EquippedGrappleItemType = 1273; // Skeletron Hand fallback table is 8f
            analysis.EquippedGrappleShootSpeed = 15f; // deliberately differs from fallback table
            analysis.GrappleHookSpeed = 0f;

            // Invoke ResolveGrappleHookSpeed through reflection (private method)
            var compatType = typeof(JueMingZ.Compat.MovementSafeLandingCompat);
            var method = compatType.GetMethod("ResolveGrappleHookSpeed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("ResolveGrappleHookSpeed method not found.");
            }
            var speed = (float)method.Invoke(null, new object[] { analysis });
            if (speed < 14.9f || speed > 15.1f)
            {
                throw new InvalidOperationException("Expected shootSpeed 15 from equipped item, got " + speed);
            }
        }

        private static void SafeLandingResolveGrappleHookSpeedFallsBackToTableOnlyWhenShootSpeedMissing()
        {
            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.EquippedGrappleItemType = 1273; // Skeletron Hand
            analysis.EquippedGrappleShootSpeed = 0f;  // shootSpeed not read from item
            analysis.GrappleHookSpeed = 0f;

            var compatType = typeof(JueMingZ.Compat.MovementSafeLandingCompat);
            var method = compatType.GetMethod("ResolveGrappleHookSpeed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("ResolveGrappleHookSpeed method not found.");
            }
            var speed = (float)method.Invoke(null, new object[] { analysis });
            if (speed < 7.9f || speed > 8.1f)
            {
                throw new InvalidOperationException("Expected fallback speed 8 for Skeletron Hand when shootSpeed missing, got " + speed);
            }
        }

        private static void SafeLandingResolveGrappleHookSpeedUsesEquippedFallbackBeforeInventoryShootSpeed()
        {
            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.EquippedGrappleItemType = 1273; // Skeletron Hand fallback table is 8f
            analysis.EquippedGrappleShootSpeed = 0f; // equipped item speed was not readable
            analysis.HasInventoryGrapple = true;
            analysis.InventoryGrappleItemType = 3572; // Lunar Hook
            analysis.InventoryGrappleShootSpeed = 18f;
            analysis.GrappleHookSpeed = 0f;

            var compatType = typeof(JueMingZ.Compat.MovementSafeLandingCompat);
            var method = compatType.GetMethod("ResolveGrappleHookSpeed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("ResolveGrappleHookSpeed method not found.");
            }
            var speed = (float)method.Invoke(null, new object[] { analysis });
            if (speed < 7.9f || speed > 8.1f)
            {
                throw new InvalidOperationException("Expected equipped fallback speed 8 before inventory shootSpeed, got " + speed);
            }
        }

        private static void SafeLandingResolveGrappleHookSpeedUsesInventoryShootSpeedWhenNoEquippedGrapple()
        {
            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = false;
            analysis.EquippedGrappleItemType = 1273;
            analysis.EquippedGrappleShootSpeed = 0f;
            analysis.HasInventoryGrapple = true;
            analysis.InventoryGrappleItemType = 1273; // fallback table is 8f
            analysis.InventoryGrappleShootSpeed = 18f;
            analysis.GrappleHookSpeed = 0f;

            var compatType = typeof(JueMingZ.Compat.MovementSafeLandingCompat);
            var method = compatType.GetMethod("ResolveGrappleHookSpeed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("ResolveGrappleHookSpeed method not found.");
            }
            var speed = (float)method.Invoke(null, new object[] { analysis });
            if (speed < 17.9f || speed > 18.1f)
            {
                throw new InvalidOperationException("Expected inventory shootSpeed 18 when no equipped grapple exists, got " + speed);
            }
        }

        private static void SafeLandingResolveGrappleHookSpeedFallsBackToDefaultOnlyWhenUnknown()
        {
            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.EquippedGrappleItemType = 99999; // unknown item
            analysis.EquippedGrappleShootSpeed = 0f;
            analysis.GrappleHookSpeed = 0f;

            var compatType = typeof(JueMingZ.Compat.MovementSafeLandingCompat);
            var method = compatType.GetMethod("ResolveGrappleHookSpeed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("ResolveGrappleHookSpeed method not found.");
            }
            var speed = (float)method.Invoke(null, new object[] { analysis });
            if (speed < 12.9f || speed > 13.1f)
            {
                throw new InvalidOperationException("Expected default 13 for unknown hook, got " + speed);
            }
        }

        private static void SafeLandingGrappleTooEarlyBlocksLowerPriorityButNotReady()
        {
            // Grapple is candidate, but impact is far away (tooEarly).
            // Should: IsReady=false, BlocksLowerPriority=true.
            var settings = AppSettings.CreateDefault();
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.Grapple, true);
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.TeleportRod, true);

            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.EquippedGrappleShootSpeed = 13f;
            analysis.GrappleHookSpeed = 13f;
            analysis.ImpactFound = true;
            analysis.ImpactDistancePixels = 300;
            analysis.ImpactTicks = 10f;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 1;
            analysis.TeleportRodItemType = 5335;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(
                MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));

            if (selection.SelectedEvaluation == null)
            {
                throw new InvalidOperationException("Expected a selected evaluation.");
            }
            if (selection.SelectedEvaluation.Priority != 4)
            {
                throw new InvalidOperationException("Expected Priority 4 (grapple) to win over P5 when tooEarly, got " + selection.SelectedEvaluation.Priority);
            }
            if (selection.SelectedEvaluation.IsReady)
            {
                throw new InvalidOperationException("Expected grapple to be NOT ready when tooEarly.");
            }
            if (!selection.SelectedEvaluation.BlocksLowerPriority)
            {
                throw new InvalidOperationException("Expected grapple to BLOCK lower priority when tooEarly.");
            }
        }

        private static void SafeLandingGrappleTooLateAllowsTeleportRodFallback()
        {
            // Grapple is candidate but tooLate. Teleport rod should be selected.
            var settings = AppSettings.CreateDefault();
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.Grapple, true);
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.TeleportRod, true);

            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.EquippedGrappleShootSpeed = 13f;
            analysis.GrappleHookSpeed = 13f;
            analysis.ImpactFound = true;
            analysis.ImpactDistancePixels = 100;
            analysis.ImpactTicks = 2f;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 1;
            analysis.TeleportRodItemType = 5335;
            analysis.TeleportRodItemName = "Rod of Harmony";
            analysis.HasTeleportTarget = true;
            analysis.TeleportTargetWorldX = 104f;
            analysis.TeleportTargetWorldY = 184f;
            analysis.TeleportTargetTileX = 6;
            analysis.TeleportTargetTileY = 11;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(
                MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));

            if (selection.SelectedEvaluation == null)
            {
                throw new InvalidOperationException("Expected a selected evaluation.");
            }
            if (selection.SelectedEvaluation.Priority != 5)
            {
                throw new InvalidOperationException("Expected Priority 5 (teleport rod) when grapple tooLate, got " + selection.SelectedEvaluation.Priority);
            }
        }

        private static void SafeLandingGrappleTooSlowAllowsPriority5Fallback()
        {
            // Grapple vertical hook speed is too slow relative to fall speed.
            var settings = AppSettings.CreateDefault();
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.Grapple, true);
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.TeleportRod, true);

            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.EquippedGrappleShootSpeed = 8f;
            analysis.GrappleHookSpeed = 8f;  // slow Skeletron Hand
            analysis.FallingSpeed = 20f;  // very fast fall
            analysis.ImpactFound = true;
            analysis.ImpactDistancePixels = 200;
            analysis.ImpactTicks = 15f;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 1;
            analysis.TeleportRodItemType = 5335;
            analysis.TeleportRodItemName = "Rod of Harmony";
            analysis.HasTeleportTarget = true;
            analysis.TeleportTargetWorldX = 104f;
            analysis.TeleportTargetWorldY = 184f;
            analysis.TeleportTargetTileX = 6;
            analysis.TeleportTargetTileY = 11;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(
                MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));

            // tooSlow should allow P5 to fall through
            if (selection.SelectedEvaluation == null)
            {
                throw new InvalidOperationException("Expected a selected evaluation.");
            }
            if (selection.SelectedEvaluation.Priority != 5)
            {
                throw new InvalidOperationException("Expected Priority 5 when grapple tooSlow, got " + selection.SelectedEvaluation.Priority);
            }
        }

        private static void SafeLandingGrappleOnlyTooLateStillSubmitsWhenTeleportRodDisabled()
        {
            var settings = AppSettings.CreateDefault();
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.Grapple, true);
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.TeleportRod, false);

            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.EquippedGrappleShootSpeed = 13f;
            analysis.GrappleHookSpeed = 13f;
            analysis.ImpactFound = true;
            analysis.ImpactDistancePixels = 100;
            analysis.ImpactTicks = 2f;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 1;
            analysis.TeleportRodItemType = 5335;
            analysis.TeleportRodItemName = "Rod of Harmony";

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(
                MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));

            AssertSelected(selection, 4, "equipped_grapple", "grapple");
            if (!selection.SelectedEvaluation.IsReady)
            {
                throw new InvalidOperationException("Expected grapple-only tooLate case to submit as last resort when P5 is disabled.");
            }
            if (!string.Equals(selection.SelectedEvaluation.Readiness, "lastResort", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected grapple-only tooLate readiness lastResort, got " + selection.SelectedEvaluation.Readiness);
            }
            if (!analysis.GrappleTooLate)
            {
                throw new InvalidOperationException("Expected tooLate diagnostic flag to remain set for last-resort grapple.");
            }
        }

        private static void SafeLandingGrappleOnlyTooSlowStillSubmitsWhenTeleportRodUnavailable()
        {
            var settings = AppSettings.CreateDefault();
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.Grapple, true);
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.TeleportRod, true);

            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.EquippedGrappleShootSpeed = 8f;
            analysis.GrappleHookSpeed = 8f;
            analysis.FallingSpeed = 20f;
            analysis.ImpactFound = true;
            analysis.ImpactDistancePixels = 200;
            analysis.ImpactTicks = 15f;
            analysis.HasTeleportRod = false;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(
                MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));

            AssertSelected(selection, 4, "equipped_grapple", "grapple");
            if (!selection.SelectedEvaluation.IsReady)
            {
                throw new InvalidOperationException("Expected grapple-only tooSlow case to submit as last resort when P5 is unavailable.");
            }
            if (!string.Equals(selection.SelectedEvaluation.Readiness, "lastResort", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected grapple-only tooSlow readiness lastResort, got " + selection.SelectedEvaluation.Readiness);
            }
            if (!analysis.GrappleTooSlowForDownwardSurface)
            {
                throw new InvalidOperationException("Expected tooSlow diagnostic flag to remain set for last-resort grapple.");
            }
        }

        private static void SafeLandingP1toP3BlockLowerPriorityNotDestroyed()
        {
            // P1 (EquippedActiveAbility) when not ready should still block lower priorities.
            var settings = AppSettings.CreateDefault();
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.DoubleJump, true);
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.Grapple, true);

            var analysis = AnalysisNearGround();
            analysis.AerialJumpWindow = true;
            analysis.HasAirJump = true;
            analysis.AirJumpFlagCount = 1;
            analysis.ImpactFound = true;
            analysis.ImpactDistancePixels = 200;
            analysis.ImpactTicks = 20f;
            analysis.HasEquippedGrapple = true;
            analysis.GrappleHookSpeed = 13f;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(
                MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));

            if (selection.SelectedEvaluation == null)
            {
                throw new InvalidOperationException("Expected a selected evaluation.");
            }
            if (selection.SelectedEvaluation.Priority >= 4)
            {
                throw new InvalidOperationException("Expected P1-P3 to block P4-P5, got Priority " + selection.SelectedEvaluation.Priority);
            }
        }

        private static void SafeLandingGrappleSpeedUnavailableAllowsPriority5()
        {
            // hookSpeed <= 0 should allow P5 fallback, not block it.
            var settings = AppSettings.CreateDefault();
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.Grapple, true);
            MovementSafeLandingOptionCatalog.SetEnabled(settings, MovementSafeLandingOptionCatalog.TeleportRod, true);

            var analysis = AnalysisNearGround();
            analysis.HasEquippedGrapple = true;
            analysis.GrappleHookSpeed = 0f;  // speed unavailable
            analysis.ImpactFound = true;
            analysis.ImpactDistancePixels = 200;
            analysis.ImpactTicks = 15f;
            analysis.HasTeleportRod = true;
            analysis.TeleportRodInventorySlot = 1;
            analysis.TeleportRodItemType = 5335;
            analysis.TeleportRodItemName = "Rod of Harmony";
            analysis.HasTeleportTarget = true;
            analysis.TeleportTargetWorldX = 104f;
            analysis.TeleportTargetWorldY = 184f;
            analysis.TeleportTargetTileX = 6;
            analysis.TeleportTargetTileY = 11;

            var selection = MovementSafeLandingStrategyCatalog.Evaluate(
                MovementSafeLandingStrategyContext.FromAnalysis(settings, analysis));

            if (selection.SelectedEvaluation == null)
            {
                throw new InvalidOperationException("Expected a selected evaluation.");
            }
            if (selection.SelectedEvaluation.Priority != 5)
            {
                throw new InvalidOperationException("Expected Priority 5 when hookSpeed unavailable, got " + selection.SelectedEvaluation.Priority);
            }
        }
    }
}
