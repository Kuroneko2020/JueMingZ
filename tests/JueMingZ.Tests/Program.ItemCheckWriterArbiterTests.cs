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
        private static void ItemCheckWriterArbiterPrioritizesBridgeOverCombatWriters()
        {
            var pending = ItemUseBridge.PendingRequestId;
            if (pending != Guid.Empty)
            {
                ItemUseBridge.Cancel(pending, "test cleanup before bridge priority arbiter test");
            }

            var bridgeRequestId = Guid.NewGuid();
            string bridgeMessage;
            if (!ItemUseBridge.TryEnqueueUseSelectedItem(
                bridgeRequestId,
                "test.bridge_writer",
                0,
                1,
                1,
                "Test Item",
                TimeSpan.FromSeconds(30),
                0,
                InputActionKind.ItemUse,
                "Test.ItemCheckWriter.Bridge",
                string.Empty,
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty,
                out bridgeMessage))
            {
                throw new InvalidOperationException("Failed to seed ItemUseBridge pending request: " + bridgeMessage);
            }

            try
            {
                var decision = ItemCheckWriterArbiter.ResolveOwner(new ItemCheckWriterArbiterContext
                {
                    BridgePendingAtStart = true,
                    BridgePendingNow = true,
                    UseItemPulseActive = true,
                    AutoCaptureCritterActive = true,
                    AutoHarvestActive = true
                });

                if (decision.Owner != ItemCheckWriterKind.ItemUseBridge ||
                    decision.OwnerRequestId != bridgeRequestId ||
                    !string.Equals(decision.Reason, "bridgePendingAtStart", StringComparison.Ordinal) ||
                    decision.BlockedCandidatesSummary.IndexOf("CombatPerfectRevolver:blockedByItemUseBridge", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Expected ItemUseBridge to own ItemCheck writer while bridge is pending.");
                }

                if (ItemUseHookCallbacks.ShouldAttemptAutoClickerTakeoverForTesting(true, true, false, false, false, false) ||
                    ItemUseHookCallbacks.ShouldAttemptFlailComboTakeoverForTesting(true, true, false, false, false, false))
                {
                    throw new InvalidOperationException("Combat writers must yield while ItemUseBridge is pending.");
                }
            }
            finally
            {
                ItemUseBridge.Cancel(bridgeRequestId, "test cleanup after bridge priority arbiter test");
            }
        }

        private static void ItemCheckWriterArbiterSelectsSingleWorldAutomationWriter()
        {
            WorldAutomationFairnessCoordinator.ResetForTesting();
            var pending = ItemUseBridge.PendingRequestId;
            if (pending != Guid.Empty)
            {
                ItemUseBridge.Cancel(pending, "test cleanup before world automation arbiter test");
            }

            var both = ItemCheckWriterArbiter.ResolveOwner(new ItemCheckWriterArbiterContext
            {
                AutoMiningActive = true,
                AutoCaptureCritterActive = true,
                AutoHarvestActive = true
            });

            if (both.Owner != ItemCheckWriterKind.AutoCaptureCritterSustainedUse ||
                both.BlockedCandidatesSummary.IndexOf("AutoHarvestSustainedUse:notOwner", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected one world automation owner with the other writer blocked.");
            }

            var rotated = ItemCheckWriterArbiter.ResolveOwner(new ItemCheckWriterArbiterContext
            {
                AutoMiningActive = true,
                AutoCaptureCritterActive = true,
                AutoHarvestActive = true
            });

            if (rotated.Owner != ItemCheckWriterKind.AutoHarvestSustainedUse ||
                rotated.BlockedCandidatesSummary.IndexOf("AutoCaptureCritterSustainedUse:notOwner", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected world automation writer fairness to rotate to auto harvest.");
            }

            var harvestOnly = ItemCheckWriterArbiter.ResolveOwner(new ItemCheckWriterArbiterContext
            {
                AutoHarvestActive = true
            });

            if (harvestOnly.Owner != ItemCheckWriterKind.AutoHarvestSustainedUse)
            {
                throw new InvalidOperationException("Expected auto harvest to own ItemCheck writer when it is the only active sustained session.");
            }

            var miningOnly = ItemCheckWriterArbiter.ResolveOwner(new ItemCheckWriterArbiterContext
            {
                AutoMiningActive = true
            });

            if (miningOnly.Owner != ItemCheckWriterKind.AutoMiningSustainedUse ||
                miningOnly.BlockedCandidatesSummary.IndexOf("CombatItemCheckAutoClicker:blockedByAutoMining", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected auto mining to own ItemCheck writer when it is the only active sustained session.");
            }

            if (ItemUseHookCallbacks.ShouldAttemptAutoClickerTakeoverForTesting(false, false, false, true, true, true) ||
                ItemUseHookCallbacks.ShouldAttemptFlailComboTakeoverForTesting(false, false, false, true, true, true))
            {
                throw new InvalidOperationException("Combat writers must yield while sustained world automation owns ItemCheck.");
            }
        }

        private static void ItemCheckWriterArbiterOwnsPhasebladeQuickSwitchAfterAdjacentWriters()
        {
            PhasebladeQuickSwitchBridge.ResetForTesting();
            var requestId = Guid.NewGuid();
            string message;
            if (!PhasebladeQuickSwitchBridge.TryBegin(
                    requestId,
                    FeatureIds.CombatPhasebladeQuickSwitch,
                    ScenarioNames.CombatPhasebladeQuickSwitch,
                    12,
                    true,
                    TimeSpan.FromSeconds(30),
                    out message))
            {
                throw new InvalidOperationException("Failed to seed phaseblade bridge: " + message);
            }

            try
            {
                var phaseblade = ItemCheckWriterArbiter.ResolveOwner(new ItemCheckWriterArbiterContext
                {
                    PhasebladeQuickSwitchActive = true
                });
                if (phaseblade.Owner != ItemCheckWriterKind.CombatPhasebladeQuickSwitch ||
                    phaseblade.OwnerRequestId != requestId ||
                    phaseblade.BlockedCandidatesSummary.IndexOf("CombatAim:blockedByPhasebladeQuickSwitch", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Expected phaseblade quick switch to own ItemCheck writer before combat fallback writers.");
                }

                var pulseWins = ItemCheckWriterArbiter.ResolveOwner(new ItemCheckWriterArbiterContext
                {
                    UseItemPulseActive = true,
                    PhasebladeQuickSwitchActive = true
                });
                if (pulseWins.Owner != ItemCheckWriterKind.UseItemPulseBridge)
                {
                    throw new InvalidOperationException("Expected UseItemPulseBridge to outrank phaseblade quick switch.");
                }

                var miningWins = ItemCheckWriterArbiter.ResolveOwner(new ItemCheckWriterArbiterContext
                {
                    AutoMiningActive = true,
                    PhasebladeQuickSwitchActive = true
                });
                if (miningWins.Owner != ItemCheckWriterKind.AutoMiningSustainedUse)
                {
                    throw new InvalidOperationException("Expected auto mining sustained use to outrank phaseblade quick switch.");
                }
            }
            finally
            {
                PhasebladeQuickSwitchBridge.Cancel(requestId, "test cleanup after phaseblade arbiter test");
                PhasebladeQuickSwitchBridge.ResetForTesting();
            }
        }

        private static void CombatAimFlailReleaseYieldsToActiveItemCheckWriter()
        {
            AssertCombatAimWritersBlockedByActiveOwner(
                new ItemCheckWriterArbiterContext { BridgePendingAtStart = true },
                ItemCheckWriterKind.ItemUseBridge,
                "bridge pending at start");
            AssertCombatAimWritersBlockedByActiveOwner(
                new ItemCheckWriterArbiterContext { AutoHarvestActive = true },
                ItemCheckWriterKind.AutoHarvestSustainedUse,
                "auto harvest active");
            AssertCombatAimWritersBlockedByActiveOwner(
                new ItemCheckWriterArbiterContext { AutoMiningActive = true },
                ItemCheckWriterKind.AutoMiningSustainedUse,
                "auto mining active");
            AssertCombatAimWritersBlockedByActiveOwner(
                new ItemCheckWriterArbiterContext { UseItemPulseActive = true },
                ItemCheckWriterKind.UseItemPulseBridge,
                "UseItemPulseBridge active");
        }

        private static void AssertCombatAimWritersBlockedByActiveOwner(
            ItemCheckWriterArbiterContext context,
            ItemCheckWriterKind expectedOwner,
            string label)
        {
            ItemCheckWriterDecision decision;
            if (!ItemCheckWriterArbiter.IsBlockedByActiveOwner(ItemCheckWriterKind.CombatFlailRelease, context, out decision) ||
                decision == null ||
                decision.Owner != expectedOwner)
            {
                throw new InvalidOperationException("Expected flail release ItemCheck writer to yield to " + label + ".");
            }

            if (!ItemCheckWriterArbiter.IsBlockedByActiveOwner(ItemCheckWriterKind.CombatAim, context, out decision) ||
                decision == null ||
                decision.Owner != expectedOwner)
            {
                throw new InvalidOperationException("Expected combat aim ItemCheck writer to yield to " + label + ".");
            }
        }

        private static void WorldAutomationFairnessCoordinatorRotatesRuntimeWinners()
        {
            WorldAutomationFairnessCoordinator.ResetForTesting();
            if (WorldAutomationFairnessCoordinator.ShouldDeferRuntimeSubmission(
                    WorldAutomationFairnessKind.AutoCaptureCritter,
                    10,
                    false))
            {
                throw new InvalidOperationException("First capture candidate should receive the initial short world automation grant.");
            }

            if (!WorldAutomationFairnessCoordinator.ShouldDeferRuntimeSubmission(
                    WorldAutomationFairnessKind.AutoHarvest,
                    10,
                    false))
            {
                throw new InvalidOperationException("Runtime grant within one tick should make the non-winner defer.");
            }

            if (!WorldAutomationFairnessCoordinator.ShouldDeferRuntimeSubmission(
                    WorldAutomationFairnessKind.AutoCaptureCritter,
                    11,
                    false))
            {
                throw new InvalidOperationException("Capture should defer on the next conflict after it won the previous one.");
            }

            if (WorldAutomationFairnessCoordinator.ShouldDeferRuntimeSubmission(
                    WorldAutomationFairnessKind.AutoHarvest,
                    11,
                    false))
            {
                throw new InvalidOperationException("Harvest should receive the next fairness grant after capture won.");
            }

            var snapshot = WorldAutomationFairnessCoordinator.GetSnapshot();
            if (!string.Equals(snapshot.LastWinner, "AutoHarvest", StringComparison.Ordinal) ||
                snapshot.LastFairnessBucket.IndexOf("worldAutomationFairnessGranted", StringComparison.Ordinal) < 0 ||
                snapshot.FairnessDebt.IndexOf("autoCapture=", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected runtime fairness diagnostics to record harvest winner and debt.");
            }
        }


    }
}
