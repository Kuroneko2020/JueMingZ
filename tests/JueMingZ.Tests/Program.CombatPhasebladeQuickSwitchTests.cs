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
        private static void CombatPhasebladeQuickSwitchRecognizesFixedItemList()
        {
            var expected = new[] { 198, 199, 200, 201, 202, 203, 3764, 3765, 3766, 3767, 3768, 3769, 4258, 4259, 5535, 5536, 5670, 5671 };
            var actual = new int[32];
            var count = CombatPhasebladeQuickSwitchService.CopyEligibleItemTypesForTesting(actual);
            if (count != expected.Length || CombatPhasebladeQuickSwitchService.EligibleItemTypeCount != expected.Length)
            {
                throw new InvalidOperationException("Expected phaseblade quick switch to expose exactly 18 audited item IDs.");
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (actual[i] != expected[i] || !CombatPhasebladeQuickSwitchService.IsEligibleItemType(expected[i]))
                {
                    throw new InvalidOperationException("Expected phaseblade item type " + expected[i] + " to be eligible.");
                }
            }

            if (!CombatPhasebladeQuickSwitchService.IsEligibleHotbarItem(198, 1) ||
                CombatPhasebladeQuickSwitchService.IsEligibleHotbarItem(198, 0))
            {
                throw new InvalidOperationException("Expected phaseblade hotbar item eligibility to require a positive stack.");
            }

            try
            {
                Terraria.ID.ItemID.Sets.ShootsOnUseRelease[671] = true;
                Terraria.ID.ItemID.Sets.ShootsOnUseRelease[3772] = true;
                Terraria.ID.ItemID.Sets.ShootsOnUseRelease[3352] = true;

                if (CombatPhasebladeQuickSwitchService.IsEligibleItemType(671) ||
                    CombatPhasebladeQuickSwitchService.IsEligibleItemType(3772) ||
                    CombatPhasebladeQuickSwitchService.IsEligibleItemType(3352) ||
                    CombatPhasebladeQuickSwitchService.IsEligibleItemType(1) ||
                    CombatPhasebladeQuickSwitchService.IsEligibleItemType(0))
                {
                    throw new InvalidOperationException("Expected Keybrand, Antlion Claw, Stylist scissors, and ordinary items to stay out of phaseblade quick switch.");
                }
            }
            finally
            {
                Terraria.ID.ItemID.Sets.ShootsOnUseRelease[671] = false;
                Terraria.ID.ItemID.Sets.ShootsOnUseRelease[3772] = false;
                Terraria.ID.ItemID.Sets.ShootsOnUseRelease[3352] = false;
            }
        }

        private static void CombatPhasebladeQuickSwitchScansHotbarOnly()
        {
            var hotbarAndBackpack = new[] { 198, 0, 5535, 1, 2, 3, 4, 5, 6, 5671, 199, 200 };
            var slots = new int[CombatPhasebladeQuickSwitchService.HotbarSlotCount];
            var count = CombatPhasebladeQuickSwitchService.FindEligibleHotbarSlots(hotbarAndBackpack, slots);

            if (count != 3 || slots[0] != 0 || slots[1] != 2 || slots[2] != 9)
            {
                throw new InvalidOperationException("Expected phaseblade quick switch to scan only hotbar slots 0-9.");
            }

            if (!CombatPhasebladeQuickSwitchService.IsSelectedSlotEligible(2, slots, count) ||
                CombatPhasebladeQuickSwitchService.IsSelectedSlotEligible(10, slots, count))
            {
                throw new InvalidOperationException("Expected selected slot eligibility to stay inside hotbar bounds.");
            }

            if (CombatPhasebladeQuickSwitchService.FindNextEligibleSlot(0, slots, count) != 2 ||
                CombatPhasebladeQuickSwitchService.FindNextEligibleSlot(2, slots, count) != 9 ||
                CombatPhasebladeQuickSwitchService.FindNextEligibleSlot(9, slots, count) != 0)
            {
                throw new InvalidOperationException("Expected phaseblade next-slot search to wrap in hotbar order.");
            }

            var oneSlot = new[] { 4 };
            if (CombatPhasebladeQuickSwitchService.FindNextEligibleSlot(4, oneSlot, 1) != -1)
            {
                throw new InvalidOperationException("Expected one eligible phaseblade to be insufficient for quick switching.");
            }
        }

        private static void CombatPhasebladeQuickSwitchStateMachineCyclesActions()
        {
            var slots = new[] { 0, 3, 9 };
            var state = CombatPhasebladeQuickSwitchState.Idle();
            var frame = CreatePhasebladeQuickSwitchFrame(0, slots, slots.Length, true, 100, 5);

            var press = CombatPhasebladeQuickSwitchService.Decide(state, frame);
            AssertPhasebladeQuickSwitchDecision(press, true, false, false, PhasebladeQuickSwitchStates.PressCurrent, "pressCurrent", -1);

            state = press.NextState;
            frame.Tick = 101;
            var release = CombatPhasebladeQuickSwitchService.Decide(state, frame);
            AssertPhasebladeQuickSwitchDecision(release, false, true, false, PhasebladeQuickSwitchStates.ReleaseCurrent, "releaseCurrent", -1);

            state = release.NextState;
            frame.Tick = 102;
            var switchNext = CombatPhasebladeQuickSwitchService.Decide(state, frame);
            AssertPhasebladeQuickSwitchDecision(switchNext, false, false, true, PhasebladeQuickSwitchStates.SwitchNext, "switchNext", 3);

            state = switchNext.NextState;
            frame.Tick = 103;
            var pending = CombatPhasebladeQuickSwitchService.Decide(state, frame);
            AssertPhasebladeQuickSwitchDecision(pending, false, false, false, PhasebladeQuickSwitchStates.SwitchNext, "switchPending", 3);

            state = pending.NextState;
            frame.SelectedSlot = 3;
            frame.Tick = 104;
            var wait = CombatPhasebladeQuickSwitchService.Decide(state, frame);
            AssertPhasebladeQuickSwitchDecision(wait, false, false, false, PhasebladeQuickSwitchStates.WaitInterval, "switchConfirmed", -1);
            if (wait.WaitUntilTick != 109)
            {
                throw new InvalidOperationException("Expected wait interval to start after the target slot is selected.");
            }

            state = wait.NextState;
            frame.Tick = 108;
            var waiting = CombatPhasebladeQuickSwitchService.Decide(state, frame);
            AssertPhasebladeQuickSwitchDecision(waiting, false, false, false, PhasebladeQuickSwitchStates.WaitInterval, "intervalWait", -1);

            state = waiting.NextState;
            frame.Tick = 109;
            var nextPress = CombatPhasebladeQuickSwitchService.Decide(state, frame);
            AssertPhasebladeQuickSwitchDecision(nextPress, true, false, false, PhasebladeQuickSwitchStates.PressCurrent, "pressCurrent", -1);
        }

        private static void CombatPhasebladeQuickSwitchStateMachineResetsAndClamps()
        {
            var slots = new[] { 1, 5 };
            var frame = CreatePhasebladeQuickSwitchFrame(1, slots, slots.Length, true, 10, 1);
            AssertPhasebladeQuickSwitchReset(frame, delegate { frame.Enabled = false; }, "disabled");
            AssertPhasebladeQuickSwitchReset(frame, delegate { frame.RightHeld = false; }, "rightNotHeld");
            AssertPhasebladeQuickSwitchReset(frame, delegate { frame.SafeContext = false; }, "unsafeContext");
            AssertPhasebladeQuickSwitchReset(frame, delegate { frame.SelectedSlot = 10; }, "invalidSelectedSlot");
            AssertPhasebladeQuickSwitchReset(frame, delegate { frame.EligibleSlotCount = 1; }, "notEnoughPhaseblades");
            AssertPhasebladeQuickSwitchReset(frame, delegate { frame.SelectedSlot = 2; }, "currentNotPhaseblade");

            frame = CreatePhasebladeQuickSwitchFrame(1, slots, slots.Length, false, 20, 1);
            var notReady = CombatPhasebladeQuickSwitchService.Decide(CombatPhasebladeQuickSwitchState.Idle(), frame);
            AssertPhasebladeQuickSwitchDecision(notReady, false, false, false, PhasebladeQuickSwitchStates.Idle, "itemNotReady", -1);

            frame = CreatePhasebladeQuickSwitchFrame(5, slots, slots.Length, true, 30, 1);
            var waitLow = CombatPhasebladeQuickSwitchService.Decide(CombatPhasebladeQuickSwitchState.SwitchPending(5), frame);
            if (waitLow.WaitUntilTick != 31)
            {
                throw new InvalidOperationException("Expected low phaseblade interval to allow the 1 tick lower bound.");
            }

            frame.IntervalTicks = 999;
            frame.Tick = 40;
            var waitHigh = CombatPhasebladeQuickSwitchService.Decide(CombatPhasebladeQuickSwitchState.SwitchPending(5), frame);
            if (waitHigh.WaitUntilTick != 70)
            {
                throw new InvalidOperationException("Expected high phaseblade interval to clamp to 30 ticks.");
            }
        }

        private static void CombatPhasebladeQuickSwitchRawInputExecutorLifecycle()
        {
            PhasebladeQuickSwitchBridge.ResetForTesting();
            try
            {
                var request = new InputActionRequest
                {
                    Kind = InputActionKind.RawInput,
                    SourceFeatureId = FeatureIds.CombatPhasebladeQuickSwitch,
                    Timeout = TimeSpan.FromSeconds(30)
                };
                request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.CombatPhasebladeQuickSwitch;
                request.Metadata[ActionMetadataKeys.RawInputMode] = "PhasebladeQuickSwitch";
                request.Metadata["PhasebladeQuickSwitchIntervalTicks"] = "99";
                request.Metadata["AllowCombatAim"] = "true";

                var execution = new InputActionExecution { Request = request };
                var snapshot = new GameStateSnapshot { IsInWorld = true };
                var executor = new RawInputActionExecutor();
                var started = executor.Start(execution, snapshot);
                if (started.Status != InputActionStatus.Running ||
                    !PhasebladeQuickSwitchBridge.HasActiveUse ||
                    !string.Equals(execution.State["PhasebladeQuickSwitchIntervalTicks"], CombatPhasebladeQuickSwitchSettings.MaxIntervalTicks.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected phaseblade raw input executor to start a clamped bridge session.");
                }

                var cancelled = executor.Cancel(execution, "phaseblade lifecycle test cancel");
                if (cancelled.Status != InputActionStatus.Cancelled ||
                    PhasebladeQuickSwitchBridge.HasActiveUse)
                {
                    throw new InvalidOperationException("Expected phaseblade raw input cancel to release bridge ownership.");
                }
            }
            finally
            {
                PhasebladeQuickSwitchBridge.ResetForTesting();
            }
        }

        private static void CombatPhasebladeQuickSwitchBridgeAppliesScopedInputAndLeavesLastSlot()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var restoreLocalPlayer = CaptureFakeLocalPlayerState();
            var previousGameUpdateCount = Terraria.Main.GameUpdateCount;
            try
            {
                PhasebladeQuickSwitchBridge.ResetForTesting();
                var player = new Terraria.Player
                {
                    whoAmI = 0,
                    selectedItem = 0,
                    active = true,
                    releaseUseItem = false
                };
                player.inventory[0] = new FakeItem { type = 198, stack = 1, Name = "Blue Phaseblade" };
                player.inventory[3] = new FakeItem { type = 199, stack = 1, Name = "Red Phaseblade" };
                ResetFakeLocalPlayer(player);
                ResetFakeMainMouse(false, false);
                Terraria.Main.mouseRight = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = true;

                var requestId = Guid.NewGuid();
                string message;
                if (!PhasebladeQuickSwitchBridge.TryBegin(
                        requestId,
                        FeatureIds.CombatPhasebladeQuickSwitch,
                        ScenarioNames.CombatPhasebladeQuickSwitch,
                        5,
                        true,
                        TimeSpan.FromSeconds(30),
                        out message))
                {
                    throw new InvalidOperationException("Expected phaseblade bridge to begin: " + message);
                }

                Terraria.Main.GameUpdateCount = 100;
                PhasebladeQuickSwitchApplyResult apply;
                if (!PhasebladeQuickSwitchBridge.TryApplyItemCheckUse(player, out apply) || apply == null || !apply.Pressed)
                {
                    throw new InvalidOperationException("Expected phaseblade bridge to apply press scope.");
                }

                if (!player.controlUseItem || !player.releaseUseItem || !Terraria.Main.mouseLeft || Terraria.Main.mouseRight)
                {
                    throw new InvalidOperationException("Expected phaseblade press scope to synthesize left use and suppress right click.");
                }

                var pressRestored = TerrariaInputCompat.TryRestoreUseItemInputState(player, apply.RestoreState) &&
                    TerrariaInputCompat.TryApplyPhasebladeQuickSwitchPostItemCheckState(player, true);
                if (!pressRestored)
                {
                    throw new InvalidOperationException("Expected phaseblade press restore to succeed: " + TerrariaInputCompat.LastInputCompatError);
                }

                PhasebladeQuickSwitchBridge.RecordRestoreStatus(requestId, pressRestored);
                if (!player.controlUseItem ||
                    player.releaseUseItem ||
                    !Terraria.Main.mouseLeft ||
                    Terraria.Main.mouseLeftRelease ||
                    Terraria.Main.mouseRight ||
                    player.selectedItem != 0)
                {
                    throw new InvalidOperationException("Expected phaseblade press restore to leave synthetic left held for projectile AI.");
                }

                Terraria.Main.mouseRight = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = true;
                Terraria.Main.GameUpdateCount = 101;
                if (!PhasebladeQuickSwitchBridge.TryApplyItemCheckUse(player, out apply) || apply == null || !apply.Released)
                {
                    throw new InvalidOperationException("Expected phaseblade bridge to apply release scope.");
                }

                if (player.controlUseItem || !player.releaseUseItem || Terraria.Main.mouseLeft || Terraria.Main.mouseRight)
                {
                    throw new InvalidOperationException("Expected phaseblade release scope to release use item and suppress right click.");
                }

                var releaseRestored = TerrariaInputCompat.TryRestoreUseItemInputState(player, apply.RestoreState) &&
                    TerrariaInputCompat.TryApplyPhasebladeQuickSwitchPostItemCheckState(player, false);
                if (!releaseRestored)
                {
                    throw new InvalidOperationException("Expected phaseblade release restore to succeed: " + TerrariaInputCompat.LastInputCompatError);
                }

                PhasebladeQuickSwitchBridge.RecordRestoreStatus(requestId, releaseRestored);
                if (player.controlUseItem ||
                    !player.releaseUseItem ||
                    Terraria.Main.mouseLeft ||
                    !Terraria.Main.mouseLeftRelease ||
                    Terraria.Main.mouseRight ||
                    player.selectedItem != 0)
                {
                    throw new InvalidOperationException("Expected phaseblade release restore to leave synthetic left released before switching.");
                }

                Terraria.Main.mouseRight = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = true;
                Terraria.Main.GameUpdateCount = 102;
                if (PhasebladeQuickSwitchBridge.TryApplyItemCheckUse(player, out apply))
                {
                    throw new InvalidOperationException("Expected switch-next state to queue a slot change without another use scope.");
                }

                var switching = PhasebladeQuickSwitchBridge.Update(requestId, false, string.Empty);
                if (switching == null ||
                    switching.PendingSwitchTargetSlot != 3 ||
                    switching.SwitchRequestCount <= 0 ||
                    player.selectedItem != 3)
                {
                    throw new InvalidOperationException("Expected phaseblade bridge to request the next eligible hotbar slot.");
                }

                Terraria.Main.GameUpdateCount = 103;
                if (PhasebladeQuickSwitchBridge.TryApplyItemCheckUse(player, out apply))
                {
                    throw new InvalidOperationException("Expected selected-slot confirmation to enter wait interval without scoped input.");
                }

                Terraria.Main.mouseRight = false;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = false;
                var stopped = PhasebladeQuickSwitchBridge.Update(requestId, false, string.Empty);
                if (stopped == null ||
                    stopped.Status != InputActionStatus.Succeeded ||
                    stopped.RestoreSuccessCount != 2 ||
                    player.selectedItem != 3 ||
                    PhasebladeQuickSwitchBridge.HasActiveUse)
                {
                    throw new InvalidOperationException("Expected phaseblade stop to keep the last phaseblade slot selected and finish cleanly.");
                }

                if (!PhasebladeQuickSwitchBridge.TryBegin(
                        requestId,
                        FeatureIds.CombatPhasebladeQuickSwitch,
                        ScenarioNames.CombatPhasebladeQuickSwitch,
                        5,
                        true,
                        TimeSpan.FromSeconds(30),
                        out message))
                {
                    throw new InvalidOperationException("Expected phaseblade bridge to begin again for cancel cleanup: " + message);
                }

                Terraria.Main.mouseRight = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = true;
                Terraria.Main.GameUpdateCount = 104;
                if (!PhasebladeQuickSwitchBridge.TryApplyItemCheckUse(player, out apply) || apply == null || !apply.Pressed)
                {
                    throw new InvalidOperationException("Expected phaseblade bridge to apply press before cancel cleanup.");
                }

                pressRestored = TerrariaInputCompat.TryRestoreUseItemInputState(player, apply.RestoreState) &&
                    TerrariaInputCompat.TryApplyPhasebladeQuickSwitchPostItemCheckState(player, true);
                PhasebladeQuickSwitchBridge.RecordRestoreStatus(requestId, pressRestored);
                PhasebladeQuickSwitchBridge.Cancel(requestId, "phaseblade cancel cleanup test");
                if (player.controlUseItem ||
                    !player.releaseUseItem ||
                    Terraria.Main.mouseLeft ||
                    !Terraria.Main.mouseLeftRelease ||
                    PhasebladeQuickSwitchBridge.HasActiveUse)
                {
                    throw new InvalidOperationException("Expected phaseblade cancel to clear synthetic held-left input.");
                }
            }
            finally
            {
                Terraria.Main.GameUpdateCount = previousGameUpdateCount;
                PhasebladeQuickSwitchBridge.ResetForTesting();
                ResetFakeMainMouse(false, true);
                restoreLocalPlayer();
                restoreRuntimeTypes();
            }
        }

        private static void CombatPhasebladeQuickSwitchRuntimeGuardSubmitsRightHeldRequest()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var restoreLocalPlayer = CaptureFakeLocalPlayerState();
            var previousGameUpdateCount = Terraria.Main.GameUpdateCount;
            try
            {
                var player = CreatePhasebladeRuntimePlayer();
                ResetFakeLocalPlayer(player);
                ResetFakeCombatUiUnblocked();
                ResetFakeMainMouse(false, true);
                Terraria.Main.mouseRight = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = true;
                Terraria.Main.GameUpdateCount = 300;
                CombatFlailRuntime.ResetItemSetCacheForTesting();

                var settings = CreatePhasebladeRuntimeSettings(true);
                PhasebladeQuickSwitchRuntimeProfile profile;
                string reason;
                if (!CombatPhasebladeQuickSwitchRuntimeService.TryReadProfileForTesting(settings, out profile, out reason))
                {
                    throw new InvalidOperationException("Expected phaseblade quick switch runtime guard to accept safe right hold, got " + reason + ".");
                }

                if (profile.SelectedSlot != 0 ||
                    profile.ItemType != 198 ||
                    profile.EligibleSlotCount != 2 ||
                    profile.NextSlot != 3)
                {
                    throw new InvalidOperationException("Expected phaseblade runtime profile to capture selected item and next hotbar slot.");
                }

                var request = CombatPhasebladeQuickSwitchRuntimeService.BuildRequestForTesting(profile);
                if (request.Kind != InputActionKind.RawInput ||
                    request.SourceFeatureId != FeatureIds.CombatPhasebladeQuickSwitch ||
                    request.DuplicatePolicy != InputActionDuplicatePolicy.CoalescePending ||
                    !string.Equals(request.Metadata[ActionMetadataKeys.RawInputMode], "PhasebladeQuickSwitch", StringComparison.Ordinal) ||
                    !string.Equals(request.Metadata["AllowCombatAim"], "true", StringComparison.Ordinal) ||
                    !string.Equals(request.Metadata["PhasebladeQuickSwitchIntervalTicks"], "12", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected phaseblade runtime request to use RawInput bridge metadata and allow combat aim.");
                }

                var queue = new InputActionQueue();
                CombatPhasebladeQuickSwitchRuntimeService.Tick(
                    queue,
                    new GameStateSnapshot { IsInWorld = true, Ui = new UiStateSnapshot() },
                    new RuntimeState(),
                    settings);

                var pending = queue.GetPendingRequestsForTesting();
                if (pending.Count != 1 ||
                    pending[0].Kind != InputActionKind.RawInput ||
                    !string.Equals(pending[0].Metadata[ActionMetadataKeys.RawInputMode], "PhasebladeQuickSwitch", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected phaseblade runtime tick to submit one RawInput request.");
                }
            }
            finally
            {
                Terraria.Main.GameUpdateCount = previousGameUpdateCount;
                ResetFakeMainMouse(false, true);
                ResetFakeCombatUiUnblocked();
                CombatFlailRuntime.ResetItemSetCacheForTesting();
                restoreLocalPlayer();
                restoreRuntimeTypes();
            }
        }

        private static void CombatPhasebladeQuickSwitchRuntimeGuardBlocksUnsafeContext()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var restoreLocalPlayer = CaptureFakeLocalPlayerState();
            try
            {
                var player = CreatePhasebladeRuntimePlayer();
                ResetFakeLocalPlayer(player);
                ResetFakeCombatUiUnblocked();
                ResetFakeMainMouse(false, true);
                Terraria.Main.mouseRight = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = true;
                var settings = CreatePhasebladeRuntimeSettings(true);
                PhasebladeQuickSwitchRuntimeProfile profile;
                string reason;

                Terraria.Main.mouseLeft = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
                AssertPhasebladeRuntimeBlocked(settings, "physicalLeftHeld", out profile, out reason);

                Terraria.Main.mouseLeft = false;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = false;
                Terraria.Main.mouseInterface = true;
                AssertPhasebladeRuntimeBlocked(settings, "uiBlocked:mainMouseInterface", out profile, out reason);

                Terraria.Main.mouseInterface = false;
                Terraria.ID.ItemID.Sets.HasRightFire[198] = true;
                CombatFlailRuntime.ResetItemSetCacheForTesting();
                AssertPhasebladeRuntimeBlocked(settings, "itemHasRightFire", out profile, out reason);

                Terraria.ID.ItemID.Sets.HasRightFire[198] = false;
                CombatFlailRuntime.ResetItemSetCacheForTesting();
                player.inventory[3] = new FakeItem { type = 1, stack = 1, Name = "Copper Shortsword" };
                AssertPhasebladeRuntimeBlocked(settings, "notEnoughPhaseblades", out profile, out reason);

                player.inventory[3] = new FakeItem { type = 199, stack = 1, Name = "Red Phaseblade" };
                Terraria.Main.SmartInteractShowingGenuine = true;
                AssertPhasebladeRuntimeBlocked(settings, "smartInteractGenuine", out profile, out reason);
            }
            finally
            {
                Terraria.ID.ItemID.Sets.HasRightFire[198] = false;
                Terraria.Main.SmartInteractShowingGenuine = false;
                Terraria.Main.SmartInteractNPC = -1;
                Terraria.Main.SmartInteractProj = -1;
                ResetFakeMainMouse(false, true);
                ResetFakeCombatUiUnblocked();
                CombatFlailRuntime.ResetItemSetCacheForTesting();
                restoreLocalPlayer();
                restoreRuntimeTypes();
            }
        }

        private static void CombatPhasebladeQuickSwitchDiagnosticsRecordProfileSummary()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var restoreLocalPlayer = CaptureFakeLocalPlayerState();
            var previousGameUpdateCount = Terraria.Main.GameUpdateCount;
            try
            {
                var player = CreatePhasebladeRuntimePlayer();
                ResetFakeLocalPlayer(player);
                ResetFakeCombatUiUnblocked();
                ResetFakeMainMouse(false, true);
                Terraria.Main.mouseRight = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = true;
                Terraria.Main.GameUpdateCount = 420;
                CombatFlailRuntime.ResetItemSetCacheForTesting();
                CombatPhasebladeQuickSwitchRuntimeService.ResetDiagnosticsForTesting();
                PhasebladeQuickSwitchBridge.ResetForTesting();

                var settings = CreatePhasebladeRuntimeSettings(true);
                CombatPhasebladeQuickSwitchRuntimeService.Tick(
                    new InputActionQueue(),
                    new GameStateSnapshot { IsInWorld = true, Ui = new UiStateSnapshot() },
                    new RuntimeState { UpdateCount = 420 },
                    settings);

                var submitted = CombatPhasebladeQuickSwitchRuntimeService.GetDiagnostics();
                if (!submitted.Enabled ||
                    !submitted.RightHeld ||
                    !submitted.Eligible ||
                    !string.Equals(submitted.LastDecision, "submitted", StringComparison.Ordinal) ||
                    submitted.CurrentSlot != 0 ||
                    submitted.NextSlot != 3 ||
                    submitted.EligibleSlotCount != 2 ||
                    submitted.IntervalTicks != CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks ||
                    submitted.ItemType != 198 ||
                    submitted.SubmittedCount != 1 ||
                    submitted.SkippedCount != 0)
                {
                    throw new InvalidOperationException("Expected phaseblade diagnostics to capture submitted profile summary.");
                }

                Terraria.Main.mouseLeft = true;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
                CombatPhasebladeQuickSwitchRuntimeService.Tick(
                    new InputActionQueue(),
                    new GameStateSnapshot { IsInWorld = true, Ui = new UiStateSnapshot() },
                    new RuntimeState { UpdateCount = 421 },
                    settings);

                var skipped = CombatPhasebladeQuickSwitchRuntimeService.GetDiagnostics();
                if (!skipped.Enabled ||
                    !skipped.RightHeld ||
                    skipped.Eligible ||
                    !string.Equals(skipped.LastDecision, "skipped", StringComparison.Ordinal) ||
                    !string.Equals(skipped.LastReason, "physicalLeftHeld", StringComparison.Ordinal) ||
                    skipped.CurrentSlot != -1 ||
                    skipped.SkippedCount != 1)
                {
                    throw new InvalidOperationException("Expected phaseblade diagnostics to capture physical-left-held skip summary.");
                }
            }
            finally
            {
                Terraria.Main.GameUpdateCount = previousGameUpdateCount;
                ResetFakeMainMouse(false, true);
                ResetFakeCombatUiUnblocked();
                CombatFlailRuntime.ResetItemSetCacheForTesting();
                CombatPhasebladeQuickSwitchRuntimeService.ResetDiagnosticsForTesting();
                PhasebladeQuickSwitchBridge.ResetForTesting();
                restoreLocalPlayer();
                restoreRuntimeTypes();
            }
        }

        private static void AssertPhasebladeRuntimeBlocked(
            RuntimeSettingsSnapshot settings,
            string expectedReason,
            out PhasebladeQuickSwitchRuntimeProfile profile,
            out string reason)
        {
            if (CombatPhasebladeQuickSwitchRuntimeService.TryReadProfileForTesting(settings, out profile, out reason) ||
                !string.Equals(reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected phaseblade runtime guard to block with " + expectedReason + ", got " + reason + ".");
            }
        }

        private static Terraria.Player CreatePhasebladeRuntimePlayer()
        {
            var player = new Terraria.Player
            {
                whoAmI = 0,
                selectedItem = 0,
                active = true,
                releaseUseItem = true
            };
            player.inventory[0] = new FakeItem { type = 198, stack = 1, Name = "Blue Phaseblade" };
            player.inventory[3] = new FakeItem { type = 199, stack = 1, Name = "Red Phaseblade" };
            return player;
        }

        private static RuntimeSettingsSnapshot CreatePhasebladeRuntimeSettings(bool enabled)
        {
            var settings = AppSettings.CreateDefault();
            settings.CombatPhasebladeQuickSwitchEnabled = enabled;
            settings.CombatPhasebladeQuickSwitchIntervalTicks = CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks;
            return RuntimeSettingsSnapshot.FromSettings(settings);
        }

        private static CombatPhasebladeQuickSwitchFrame CreatePhasebladeQuickSwitchFrame(
            int selectedSlot,
            int[] eligibleSlots,
            int eligibleSlotCount,
            bool itemReady,
            long tick,
            int intervalTicks)
        {
            return new CombatPhasebladeQuickSwitchFrame
            {
                Enabled = true,
                RightHeld = true,
                SafeContext = true,
                SelectedSlot = selectedSlot,
                EligibleSlots = eligibleSlots,
                EligibleSlotCount = eligibleSlotCount,
                ItemReady = itemReady,
                Tick = tick,
                IntervalTicks = intervalTicks
            };
        }

        private static CombatPhasebladeQuickSwitchFrame ClonePhasebladeQuickSwitchFrame(CombatPhasebladeQuickSwitchFrame source)
        {
            return new CombatPhasebladeQuickSwitchFrame
            {
                Enabled = source.Enabled,
                RightHeld = source.RightHeld,
                SafeContext = source.SafeContext,
                SelectedSlot = source.SelectedSlot,
                EligibleSlots = source.EligibleSlots == null ? null : (int[])source.EligibleSlots.Clone(),
                EligibleSlotCount = source.EligibleSlotCount,
                ItemReady = source.ItemReady,
                Tick = source.Tick,
                IntervalTicks = source.IntervalTicks
            };
        }

        private static void AssertPhasebladeQuickSwitchReset(
            CombatPhasebladeQuickSwitchFrame frame,
            Action mutate,
            string expectedReason)
        {
            var copy = ClonePhasebladeQuickSwitchFrame(frame);
            mutate();
            var mutated = ClonePhasebladeQuickSwitchFrame(frame);
            frame.Enabled = copy.Enabled;
            frame.RightHeld = copy.RightHeld;
            frame.SafeContext = copy.SafeContext;
            frame.SelectedSlot = copy.SelectedSlot;
            frame.EligibleSlots = copy.EligibleSlots;
            frame.EligibleSlotCount = copy.EligibleSlotCount;
            frame.ItemReady = copy.ItemReady;
            frame.Tick = copy.Tick;
            frame.IntervalTicks = copy.IntervalTicks;

            var decision = CombatPhasebladeQuickSwitchService.Decide(
                CombatPhasebladeQuickSwitchState.ForState(PhasebladeQuickSwitchStates.PressCurrent),
                mutated);

            if (decision == null ||
                !decision.ResetState ||
                decision.PressCurrent ||
                decision.ReleaseCurrent ||
                decision.SwitchNext ||
                !string.Equals(decision.State, PhasebladeQuickSwitchStates.Idle, StringComparison.Ordinal) ||
                !string.Equals(decision.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected phaseblade quick switch reset " + expectedReason +
                    ", got " + (decision == null ? "<null>" : decision.State + "/" + decision.Reason) + ".");
            }
        }

        private static void AssertPhasebladeQuickSwitchDecision(
            CombatPhasebladeQuickSwitchDecision decision,
            bool expectedPress,
            bool expectedRelease,
            bool expectedSwitch,
            string expectedState,
            string expectedReason,
            int expectedTargetSlot)
        {
            if (decision == null ||
                decision.PressCurrent != expectedPress ||
                decision.ReleaseCurrent != expectedRelease ||
                decision.SwitchNext != expectedSwitch ||
                decision.TargetSlot != expectedTargetSlot ||
                !string.Equals(decision.State, expectedState, StringComparison.Ordinal) ||
                !string.Equals(decision.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected phaseblade quick switch decision " +
                    expectedPress + "/" + expectedRelease + "/" + expectedSwitch + "/" + expectedState + "/" + expectedReason + "/" + expectedTargetSlot +
                    ", got " + (decision == null
                        ? "<null>"
                        : decision.PressCurrent + "/" + decision.ReleaseCurrent + "/" + decision.SwitchNext + "/" + decision.State + "/" + decision.Reason + "/" + decision.TargetSlot) + ".");
            }
        }


    }
}
