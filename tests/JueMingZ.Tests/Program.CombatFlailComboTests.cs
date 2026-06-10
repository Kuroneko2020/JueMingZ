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
        private static void CombatFlailComboCoreLaunchesReleasesAndRecalls()
        {
            var profile = CreateFlailComboProfile();
            var none = CombatFlailRuntimeFrame.None();

            AssertFlailComboDecision(
                CombatFlailComboService.CreateDecision(profile, none, true, true, false, FlailComboStates.Idle),
                true,
                true,
                FlailComboStates.LaunchPress,
                "launchReady");

            AssertFlailComboDecision(
                CombatFlailComboService.CreateDecision(profile, none, true, true, false, FlailComboStates.LaunchPress),
                true,
                false,
                FlailComboStates.LaunchRelease,
                "launchRelease");

            AssertFlailComboDecision(
                CombatFlailComboService.CreateDecision(profile, none, true, true, true, FlailComboStates.LaunchRelease),
                false,
                false,
                FlailComboStates.Cooldown,
                "cooldown");

            var flying = CombatFlailRuntimeFrame.ForTesting(true, 10, 1058, 20, 1f, 4f, 0f, false, false, 0);
            AssertFlailComboDecision(
                CombatFlailComboService.CreateDecision(profile, flying, true, true, false, FlailComboStates.InFlight),
                false,
                false,
                FlailComboStates.InFlight,
                "inFlight");

            var hit = CombatFlailRuntimeFrame.ForTesting(true, 10, 1058, 20, 1f, 4f, 0f, true, false, 0);
            AssertFlailComboDecision(
                CombatFlailComboService.CreateDecision(profile, hit, true, true, false, FlailComboStates.InFlight),
                true,
                true,
                FlailComboStates.RecallPress,
                "hitDetected");

            AssertFlailComboDecision(
                CombatFlailComboService.CreateDecision(profile, hit, true, true, false, FlailComboStates.RecallPress),
                true,
                false,
                FlailComboStates.RecallRelease,
                "recallRelease");
        }

        private static void CombatFlailComboBlocksVanillaRightClickSemantics()
        {
            // Regression guard: right-click flail takeover must yield to vanilla
            // item right-click semantics instead of stealing those interactions.
            var profile = CreateFlailComboProfile();
            profile.VanillaRightClickBlocked = true;
            profile.VanillaRightClickReason = "itemHasRightFire";

            AssertFlailComboDecision(
                CombatFlailComboService.CreateDecision(profile, CombatFlailRuntimeFrame.None(), true, true, false, FlailComboStates.Idle),
                false,
                false,
                FlailComboStates.Disabled,
                "itemHasRightFire");
        }

        private static void CombatFlailComboItemSetGuardFailsClosed()
        {
            try
            {
                CombatFlailComboService.ResetForTesting();
                Terraria.ID.ItemID.Sets.HasRightFire[5526] = true;
                string reason;
                if (!CombatFlailRuntime.HasVanillaRightClickSemantics(5526, new FakePlayer(), out reason) ||
                    !string.Equals(reason, "itemHasRightFire", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected HasRightFire item set to block flail combo right-click takeover.");
                }

                Terraria.ID.ItemID.Sets.HasRightFire[5526] = false;
                Terraria.ID.ItemID.Sets.ItemsThatAllowRepeatedRightClick[5526] = true;
                CombatFlailRuntime.ResetItemSetCacheForTesting();
                if (!CombatFlailRuntime.HasVanillaRightClickSemantics(5526, new FakePlayer(), out reason) ||
                    !string.Equals(reason, "itemAllowsRepeatedRightClick", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected repeated right-click item set to block flail combo right-click takeover.");
                }
            }
            finally
            {
                Terraria.ID.ItemID.Sets.HasRightFire[5526] = false;
                Terraria.ID.ItemID.Sets.ItemsThatAllowRepeatedRightClick[5526] = false;
                CombatFlailRuntime.ResetItemSetCacheForTesting();
            }
        }

        private static void CombatFlailComboScopedTakeoverSuppressesAndRestoresRightClick()
        {
            // Right-click suppression is scoped takeover state; restore must put
            // both left and right mouse inputs back exactly.
            var player = new Terraria.Player
            {
                active = true,
                controlUseItem = false,
                releaseUseItem = false
            };

            ResetFakeMainMouse(false, false);
            Terraria.Main.mouseRight = true;
            Terraria.Main.mouseRightRelease = true;

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (!TerrariaInputCompat.TryBeginScopedUseItemClickTakeoverSuppressingRightClick(player, true, "CombatFlailComboItemCheck", out takeover))
            {
                throw new InvalidOperationException("Expected flail combo scoped takeover to apply: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (!player.controlUseItem || !player.releaseUseItem ||
                !Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease ||
                Terraria.Main.mouseRight || Terraria.Main.mouseRightRelease ||
                takeover == null || !takeover.Pressed ||
                !takeover.MainMouseRightCaptured || !takeover.MainMouseRightReleaseCaptured)
            {
                throw new InvalidOperationException("Expected flail combo takeover to press left and suppress right within scope.");
            }

            if (!TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover))
            {
                throw new InvalidOperationException("Expected flail combo scoped takeover restore to succeed: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (player.controlUseItem || player.releaseUseItem ||
                Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease ||
                !Terraria.Main.mouseRight || !Terraria.Main.mouseRightRelease)
            {
                throw new InvalidOperationException("Expected flail combo scoped takeover to restore left and right mouse state.");
            }
        }

        private static void CombatFlailComboWorldRightClickGuardAllowsRawRightClickIntent()
        {
            // Raw right-click intent and fake smart-cursor hints are not confirmed
            // world interactions; only genuine targets may block takeover.
            var player = new FakePlayer();
            var restoreMainType = PushFakeTerrariaMainType();
            string reason;

            try
            {
                Terraria.Main.SmartCursorWanted_Mouse = true;
                Terraria.Main.SmartCursorShowing = true;
                Terraria.Main.SmartInteractShowingFake = true;
                if (TerrariaInputCompat.IsWorldRightClickInteractionActive(player, out reason))
                {
                    throw new InvalidOperationException("Expected smart cursor/fake interact hints to be allowed, got " + reason + ".");
                }

                Terraria.Main.SmartInteractShowingGenuine = true;
                if (!TerrariaInputCompat.IsWorldRightClickInteractionActive(player, out reason) ||
                    !string.Equals(reason, "smartInteractGenuine", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected genuine smart interact to block world right click takeover.");
                }

                Terraria.Main.SmartInteractShowingGenuine = false;
                Terraria.Main.SmartInteractNPC = 12;
                if (!TerrariaInputCompat.IsWorldRightClickInteractionActive(player, out reason) ||
                    !string.Equals(reason, "smartInteractTarget", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected smart interact target to block world right click takeover.");
                }

                Terraria.Main.SmartInteractNPC = -1;
                player.controlUseTile = true;
                player.tileInteractAttempted = true;
                if (TerrariaInputCompat.IsWorldRightClickInteractionActive(player, out reason))
                {
                    throw new InvalidOperationException("Expected raw right click intent to be allowed, got " + reason + ".");
                }

                player.tileInteractionHappened = true;
                if (!TerrariaInputCompat.IsWorldRightClickInteractionActive(player, out reason) ||
                    !string.Equals(reason, "tileInteractionHappened", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected completed tile interaction to block world right click takeover.");
                }
            }
            finally
            {
                Terraria.Main.SmartInteractShowingGenuine = false;
                Terraria.Main.SmartInteractShowingFake = false;
                Terraria.Main.SmartCursorShowing = false;
                Terraria.Main.SmartCursorWanted_Mouse = false;
                Terraria.Main.SmartCursorWanted_GamePad = false;
                Terraria.Main.SmartInteractNPC = -1;
                Terraria.Main.SmartInteractProj = -1;
                player.controlUseTile = false;
                player.tileInteractionHappened = false;
                player.tileInteractAttempted = false;
                restoreMainType();
            }
        }

        private static void CombatFlailComboAllowsPlainInventoryOpen()
        {
            // Regression guard: opening inventory alone is not UI capture; actual
            // mouse ownership still blocks flail combo as before.
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousInventoryOpen = Terraria.Main.playerInventory;
            var previousMainMouseInterface = Terraria.Main.mouseInterface;
            var previousBlockMouse = Terraria.Main.blockMouse;
            var previousGameMenu = Terraria.Main.gameMenu;
            var previousChatMode = Terraria.Main.chatMode;
            var previousDrawingPlayerChat = Terraria.Main.drawingPlayerChat;
            var previousNpcChatText = Terraria.Main.npcChatText;
            try
            {
                Terraria.Main.playerInventory = true;
                Terraria.Main.mouseInterface = false;
                Terraria.Main.blockMouse = false;
                Terraria.Main.gameMenu = false;
                Terraria.Main.chatMode = false;
                Terraria.Main.drawingPlayerChat = false;
                Terraria.Main.npcChatText = string.Empty;

                var player = new FakePlayer
                {
                    active = true,
                    mouseInterface = false
                };

                string reason;
                if (CombatFlailComboService.IsUiBlockedForTesting(player, out reason))
                {
                    throw new InvalidOperationException("Plain open inventory should not block flail combo, got " + reason + ".");
                }

                Terraria.Main.mouseInterface = true;
                if (!CombatFlailComboService.IsUiBlockedForTesting(player, out reason) ||
                    !string.Equals(reason, "uiBlocked:mainMouseInterface", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Mouse-captured inventory UI should still block flail combo, got " + reason + ".");
                }
            }
            finally
            {
                Terraria.Main.playerInventory = previousInventoryOpen;
                Terraria.Main.mouseInterface = previousMainMouseInterface;
                Terraria.Main.blockMouse = previousBlockMouse;
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.chatMode = previousChatMode;
                Terraria.Main.drawingPlayerChat = previousDrawingPlayerChat;
                Terraria.Main.npcChatText = previousNpcChatText;
                restoreRuntimeTypes();
            }
        }

        private static void CombatFlailComboYieldsToAdjacentScopedUse()
        {
            if (!ItemUseHookCallbacks.ShouldAttemptFlailComboTakeoverForTesting(false, false, false, false, false, false))
            {
                throw new InvalidOperationException("Expected flail combo to run when no adjacent scoped use is active.");
            }

            AssertFlailComboYield(true, false, false, false, false, false, "bridge pending at start");
            AssertFlailComboYield(false, true, false, false, false, false, "bridge pending now");
            AssertFlailComboYield(false, false, true, false, false, false, "UseItemPulseBridge");
            AssertFlailComboYield(false, false, false, true, false, false, "auto mining");
            AssertFlailComboYield(false, false, false, false, true, false, "auto harvest");
            AssertFlailComboYield(false, false, false, false, false, true, "auto capture");
        }

        private static void CombatFlailComboDiagnosticsRecordScopedDecision()
        {
            var profile = CreateFlailComboProfile();
            var frame = CombatFlailRuntimeFrame.ForTesting(true, 10, 1058, 20, 1f, 4f, 0f, true, false, 0);
            var decision = CombatFlailComboService.CreateDecision(profile, frame, true, true, false, FlailComboStates.InFlight);
            CombatFlailComboService.RecordDecisionForTesting(decision, profile, frame);

            var diagnostics = CombatFlailComboService.GetDiagnostics();
            if (!string.Equals(diagnostics.LastDecision, "scopedPress", StringComparison.Ordinal) ||
                !string.Equals(diagnostics.LastReason, "hitDetected", StringComparison.Ordinal) ||
                diagnostics.ItemType != 5526 ||
                diagnostics.ProjectileType != 1058 ||
                !diagnostics.HitDetected ||
                !diagnostics.ScopedPress ||
                diagnostics.ScopedRelease ||
                diagnostics.RestoreOk)
            {
                throw new InvalidOperationException("Expected flail combo diagnostics to record scoped press decision.");
            }

            CombatFlailComboService.RecordRestoreStatus(true);
            diagnostics = CombatFlailComboService.GetDiagnostics();
            if (!diagnostics.RestoreOk)
            {
                throw new InvalidOperationException("Expected flail combo diagnostics to record restore status.");
            }

            CombatItemCheckAutoClickService.RecordExternalSkip("flailComboTakeover");
            var autoClicker = CombatItemCheckAutoClickService.GetDiagnostics();
            if (!string.Equals(autoClicker.LastDecision, "noOp", StringComparison.Ordinal) ||
                !string.Equals(autoClicker.LastReason, "flailComboTakeover", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected auto clicker diagnostics to record flail combo external skip.");
            }
        }

        private static void CombatFlailComboReleaseRemembersFlailAimTail()
        {
            CombatAimFlailControlService.ResetForTesting();
            Terraria.Main.GameUpdateCount = 900;
            ResetFakeMainMouse(false, true);
            var player = new FakePlayer
            {
                whoAmI = 7,
                controlUseItem = false,
                releaseUseItem = true,
                channel = false
            };
            var decision = BuildFlailItemCheckDecision(player);
            decision.UseItemHeld = false;
            decision.UseItemReleased = true;
            decision.WasUseItemHeldLastTick = true;
            decision.ReleasedThisTick = true;
            decision.ReleaseDetected = true;
            decision.ReleaseHoldPending = true;
            decision.ReleaseHoldActive = true;
            decision.ReleaseHoldState = ReleaseHoldStates.ReleasedPending;
            decision.ReleaseHoldValidationReason = "targetDummyAllowed:strictRecomputed";

            if (!CombatAimFlailControlService.TryRememberExistingItemCheckReleaseTail(decision, "FlailComboItemCheck"))
            {
                throw new InvalidOperationException("Expected flail combo release to remember ProjectileAI aim tail without a second input takeover.");
            }

            if (player.controlUseItem || !player.releaseUseItem || player.channel ||
                Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease)
            {
                throw new InvalidOperationException("Expected flail combo release tail memory to leave input state unchanged.");
            }

            var diagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(decision);
            if (diagnostics == null ||
                !diagnostics.Active ||
                !diagnostics.AttackRelease ||
                !diagnostics.AttackSuppressed ||
                !diagnostics.PhysicalReleasePending ||
                !string.Equals(diagnostics.State, FlailControlStates.ReleaseToTarget, StringComparison.Ordinal) ||
                !string.Equals(diagnostics.TakeoverScope, "FlailComboItemCheck", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected flail combo release tail diagnostics to mark existing release scope.");
            }

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryGetFlailReleaseTailDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryGetFlailReleaseTailDecision reflection failed.");
            }

            var args = new object[] { 900L, null, null };
            var tailAvailable = (bool)method.Invoke(null, args);
            var tail = args[2] as CombatAimItemCheckDecision;
            if (!tailAvailable || tail == null ||
                !string.Equals(tail.PersistentCursorReason, "flailAiStyle15Release", StringComparison.Ordinal) ||
                Math.Abs(tail.AimWorldX - decision.AimWorldX) > 0.001f ||
                Math.Abs(tail.AimWorldY - decision.AimWorldY) > 0.001f)
            {
                throw new InvalidOperationException("Expected flail combo release to arm flail Projectile.AI scoped aim.");
            }
        }

        private static void CombatFlailComboPressAimFeedsReleaseTail()
        {
            CombatAimFlailControlService.ResetForTesting();
            Terraria.Main.GameUpdateCount = 1200;
            ResetFakeMainMouse(false, true);
            var player = new FakePlayer
            {
                whoAmI = 7,
                controlUseItem = false,
                releaseUseItem = true,
                channel = false
            };

            var pressDecision = BuildFlailItemCheckDecision(player);
            pressDecision.UseItemHeld = true;
            pressDecision.UseItemReleased = true;
            pressDecision.WasUseItemHeldLastTick = false;
            pressDecision.ReleasedThisTick = false;
            pressDecision.ReleaseDetected = false;
            pressDecision.ReleaseHoldPending = true;
            pressDecision.ReleaseHoldActive = false;
            pressDecision.ReleaseHoldState = ReleaseHoldStates.ArmedWhileHeld;

            if (!CombatAimFlailControlService.TryRememberFlailComboPressAim(pressDecision))
            {
                throw new InvalidOperationException("Expected flail combo press aim to be cached for the following virtual release.");
            }

            Terraria.Main.GameUpdateCount = 1201;
            if (!CombatAimFlailControlService.TryRememberFlailComboPressReleaseTail("FlailComboItemCheck"))
            {
                throw new InvalidOperationException("Expected flail combo virtual release to arm tail from the press aim cache.");
            }

            if (player.controlUseItem || !player.releaseUseItem || player.channel ||
                Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease)
            {
                throw new InvalidOperationException("Expected flail combo press-cache release tail memory to leave input state unchanged.");
            }

            var diagnostics = CombatAimFlailControlService.GetDecisionDiagnostics(pressDecision);
            if (diagnostics == null ||
                !diagnostics.Active ||
                !diagnostics.AttackRelease ||
                !diagnostics.AttackSuppressed ||
                !diagnostics.CachedReleaseAim ||
                !diagnostics.PhysicalReleasePending ||
                !string.Equals(diagnostics.State, FlailControlStates.ReleaseToTarget, StringComparison.Ordinal) ||
                !string.Equals(diagnostics.TakeoverScope, "FlailComboItemCheck", StringComparison.Ordinal) ||
                !string.Equals(diagnostics.BlockedReason, "flailComboPressAim", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected flail combo press-cache diagnostics to mark a virtual release tail.");
            }

            var method = typeof(CombatAimPersistentCursorService).GetMethod("TryGetFlailReleaseTailDecision", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("TryGetFlailReleaseTailDecision reflection failed.");
            }

            var args = new object[] { 1201L, null, null };
            var tailAvailable = (bool)method.Invoke(null, args);
            var tail = args[2] as CombatAimItemCheckDecision;
            if (!tailAvailable || tail == null ||
                !string.Equals(tail.PersistentCursorReason, "flailAiStyle15Release", StringComparison.Ordinal) ||
                !tail.ReleaseDetected ||
                !tail.ReleasedThisTick ||
                !tail.WasUseItemHeldLastTick ||
                Math.Abs(tail.AimWorldX - pressDecision.AimWorldX) > 0.001f ||
                Math.Abs(tail.AimWorldY - pressDecision.AimWorldY) > 0.001f)
            {
                throw new InvalidOperationException("Expected flail combo press aim cache to feed the Projectile.AI release tail.");
            }
        }

        private static void FlailComboPressAimExpiresAfterTailWindow()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                CombatAimFlailControlService.ResetForTesting();
                Terraria.Main.GameUpdateCount = 2200;
                ResetFakeMainMouse(false, true);
                var player = new FakePlayer
                {
                    whoAmI = 7,
                    controlUseItem = false,
                    releaseUseItem = true,
                    channel = false
                };

                var pressDecision = BuildFlailItemCheckDecision(player);
                pressDecision.UseItemHeld = true;
                pressDecision.UseItemReleased = true;
                pressDecision.WasUseItemHeldLastTick = false;
                pressDecision.ReleasedThisTick = false;
                pressDecision.ReleaseDetected = false;
                pressDecision.ReleaseHoldPending = true;
                pressDecision.ReleaseHoldActive = false;
                pressDecision.ReleaseHoldState = ReleaseHoldStates.ArmedWhileHeld;

                if (!CombatAimFlailControlService.TryRememberFlailComboPressAim(pressDecision))
                {
                    throw new InvalidOperationException("Expected flail combo press aim to be cached for tail age testing.");
                }

                Terraria.Main.GameUpdateCount = 2220;
                if (!CombatAimFlailControlService.TryRememberFlailComboPressReleaseTail("FlailComboItemCheck"))
                {
                    throw new InvalidOperationException("Expected flail combo press aim to remain available at 20 ticks.");
                }

                if (player.controlUseItem || !player.releaseUseItem || player.channel ||
                    Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected 20 tick combo press tail memory to leave input state unchanged.");
                }

                Terraria.Main.GameUpdateCount = 2221;
                if (CombatAimFlailControlService.TryRememberFlailComboPressReleaseTail("FlailComboItemCheck"))
                {
                    throw new InvalidOperationException("Expected flail combo press aim to expire after 20 ticks.");
                }
            }
            finally
            {
                restoreRuntimeTypes();
            }
        }

        private static void AssertFlailComboYield(
            bool bridgePendingAtStart,
            bool bridgePendingNow,
            bool pulseApplied,
            bool autoMiningApplied,
            bool autoHarvestApplied,
            bool autoCaptureApplied,
            string label)
        {
            if (ItemUseHookCallbacks.ShouldAttemptFlailComboTakeoverForTesting(
                bridgePendingAtStart,
                bridgePendingNow,
                pulseApplied,
                autoMiningApplied,
                autoHarvestApplied,
                autoCaptureApplied))
            {
                throw new InvalidOperationException("Expected flail combo to yield to " + label + ".");
            }
        }

        private static CombatFlailComboProfile CreateFlailComboProfile()
        {
            return new CombatFlailComboProfile
            {
                Available = true,
                Eligible = true,
                Reason = "eligible:flailAiStyle15",
                ItemType = 5526,
                ItemName = "Flairon",
                Shoot = 1058,
                ProjectileAiStyle = 15,
                ItemReady = true
            };
        }

        private static void AssertFlailComboDecision(
            CombatFlailComboDecision decision,
            bool expectedApply,
            bool expectedPress,
            string expectedState,
            string expectedReason)
        {
            if (decision == null ||
                decision.ApplyTakeover != expectedApply ||
                decision.PressAttack != expectedPress ||
                !string.Equals(decision.State, expectedState, StringComparison.Ordinal) ||
                !string.Equals(decision.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected flail combo decision " + expectedApply + "/" + expectedPress + "/" + expectedState + "/" + expectedReason +
                    ", got " + (decision == null ? "<null>" : decision.ApplyTakeover + "/" + decision.PressAttack + "/" + decision.State + "/" + decision.Reason) + ".");
            }
        }


    }
}
