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
                // Suppress duplicate rescues within one descent, but clear the
                // guard as soon as danger ends so the next fall can act.
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
            // Pending teleport-rod rescues must revalidate the landing snapshot;
            // replaying stale coordinates can move the player to the wrong place.
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
            // SafeLanding metadata keys are action-event diagnostics contracts;
            // passing this test is local coverage, not real-world rescue proof.
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
