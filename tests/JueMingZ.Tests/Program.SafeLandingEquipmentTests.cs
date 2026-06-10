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
            // Regression guard: missing cheap-precheck fields fail open to full
            // analysis, not to "safe" or a hard failure.
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

        private static void SafeLandingRecoveryKeepsItemWhenRestoreTargetChanged()
        {
            // Restore deferral protects user-moved items; never weaken it into
            // a successful restore just because the rescue item is still known.
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

    }
}
