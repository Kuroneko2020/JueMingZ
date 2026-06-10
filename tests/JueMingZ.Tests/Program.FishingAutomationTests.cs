using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
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
using JueMingZ.GameState.Buffs;
using JueMingZ.Records;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FishingSessionWaitsForBobberLiquid()
        {
            var thrownBobber = new FishingBobberObservation
            {
                Active = true,
                Bobber = true,
                LiquidStateKnown = true,
                InLiquid = false,
                Ai1 = 0f
            };
            if (FishingAutomationService.ShouldStartSessionFromObservation(thrownBobber, false))
            {
                throw new InvalidOperationException("Expected ordinary thrown bobber outside liquid not to start fishing session.");
            }

            thrownBobber.InLiquid = true;
            if (!FishingAutomationService.ShouldStartSessionFromObservation(thrownBobber, false))
            {
                throw new InvalidOperationException("Expected bobber in liquid to start fishing session.");
            }

            var truffleWormBobber = new FishingBobberObservation
            {
                Active = true,
                Bobber = true,
                LiquidStateKnown = true,
                InLiquid = false,
                Ai1 = -1f
            };
            if (!FishingAutomationService.ShouldStartSessionFromObservation(truffleWormBobber, true))
            {
                throw new InvalidOperationException("Expected Truffle Worm special bobber state to start even when liquid read says false.");
            }

            truffleWormBobber.Bobber = false;
            if (FishingAutomationService.ShouldStartSessionFromObservation(truffleWormBobber, true))
            {
                throw new InvalidOperationException("Expected non-bobber projectile not to start fishing session.");
            }
        }

        private static void FishingFilterSpecialRulesRespectOppositeListOverrides()
        {
            var pearlwoodCrate = FishingFilterCandidate(701, "Pearlwood Crate", true, false, false);
            var ironCrate = FishingFilterCandidate(702, "Iron Crate", true, false, false);
            var settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.DenyList;
            settings.FishingFilterMatchMode = FishingFilterMatchModes.Exact;
            settings.FishingFilterCrateRule = FishingFilterSpecialRuleModes.Allow;
            settings.FishingFilterDenyExactEntries.Add(new FishingFilterExactEntry { Kind = FishingCatchKinds.Item, Id = 701 });
            AssertFishingFilterDecision(false, FishingFilterDecisionService.Decide(settings, pearlwoodCrate), "allow crate must still skip a specifically denied crate");
            AssertFishingFilterDecision(true, FishingFilterDecisionService.Decide(settings, ironCrate), "allow crate must keep crates missing from the blacklist");

            settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.AllowList;
            settings.FishingFilterMatchMode = FishingFilterMatchModes.Exact;
            settings.FishingFilterCrateRule = FishingFilterSpecialRuleModes.Deny;
            settings.FishingFilterAllowExactEntries.Add(new FishingFilterExactEntry { Kind = FishingCatchKinds.Item, Id = 701 });
            AssertFishingFilterDecision(true, FishingFilterDecisionService.Decide(settings, pearlwoodCrate), "deny crate must still keep a specifically allowed crate");
            AssertFishingFilterDecision(false, FishingFilterDecisionService.Decide(settings, ironCrate), "deny crate must skip crates missing from the whitelist");

            var goblinShark = FishingFilterCandidate(620, "Goblin Shark", false, false, true);
            var zombieMerman = FishingFilterCandidate(586, "Zombie Merman", false, false, true);
            settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.DenyList;
            settings.FishingFilterMatchMode = FishingFilterMatchModes.Keyword;
            settings.FishingFilterEnemyRule = FishingFilterSpecialRuleModes.Allow;
            settings.FishingFilterDenyKeywords.Add("Shark");
            AssertFishingFilterDecision(false, FishingFilterDecisionService.Decide(settings, goblinShark), "allow enemy must still obey a blacklist keyword match");
            AssertFishingFilterDecision(true, FishingFilterDecisionService.Decide(settings, zombieMerman), "allow enemy must keep enemy catches missing from blacklist keywords");
        }

        private static void FishingSessionDamageExitComparesLifeDrop()
        {
            if (!FishingAutomationService.ShouldEndSessionForPlayerDamageForTesting(400, 399))
            {
                throw new InvalidOperationException("Expected fishing session to end when player life drops.");
            }

            if (FishingAutomationService.ShouldEndSessionForPlayerDamageForTesting(400, 400) ||
                FishingAutomationService.ShouldEndSessionForPlayerDamageForTesting(400, 420) ||
                FishingAutomationService.ShouldEndSessionForPlayerDamageForTesting(0, 399))
            {
                throw new InvalidOperationException("Expected fishing damage exit to ignore stable life, healing, and unseeded baselines.");
            }
        }

        private static FishingCatchCandidate FishingFilterCandidate(int id, string name, bool crate, bool questFish, bool enemy)
        {
            return new FishingCatchCandidate
            {
                Kind = enemy ? FishingCatchKinds.NPC : FishingCatchKinds.Item,
                Id = id,
                DisplayName = name,
                DisplayNameSnapshot = name,
                IsCrate = crate,
                IsQuestFish = questFish,
                IsEnemy = enemy
            };
        }

        private static void AssertFishingFilterDecision(bool expectedKeep, FishingFilterDecision decision, string label)
        {
            if (decision == null || decision.ShouldKeep != expectedKeep)
            {
                throw new InvalidOperationException("Unexpected fishing filter decision: " + label + ".");
            }
        }

        private static void FishingAutoEquipmentWaterSkipsLavaHookAndCoveredParts()
        {
            var player = CreateFishingEquipmentPlayer();
            player.armor[3] = Accessory(TestLavaproofTackleBag, "Lavaproof Tackle Bag");
            player.inventory[10] = Accessory(TestHighTestFishingLine, "High Test Fishing Line");
            player.inventory[11] = Accessory(TestTackleBox, "Tackle Box");
            player.inventory[12] = Accessory(TestLavaFishingHook, "Lavaproof Fishing Hook");
            player.inventory[13] = Accessory(TestFishingBobber, "Fishing Bobber");

            var plan = BuildFishingAutoEquipmentPlan(player, FishingLiquidKind.Water);
            AssertPlanContains(plan, TestFishingBobber, "water bobber");
            AssertPlanDoesNotContain(plan, TestHighTestFishingLine, "covered high test line");
            AssertPlanDoesNotContain(plan, TestTackleBox, "covered tackle box");
            AssertPlanDoesNotContain(plan, TestLavaFishingHook, "non-lava lava hook");
        }

        private static void FishingAutoEquipmentLavaPrefersLavaproofBagOverHook()
        {
            var player = CreateFishingEquipmentPlayer();
            player.inventory[10] = Accessory(TestLavaproofTackleBag, "Lavaproof Tackle Bag");
            player.inventory[11] = Accessory(TestLavaFishingHook, "Lavaproof Fishing Hook");

            var plan = BuildFishingAutoEquipmentPlan(player, FishingLiquidKind.Lava);
            AssertPlanContains(plan, TestLavaproofTackleBag, "lavaproof tackle bag");
            AssertPlanDoesNotContain(plan, TestLavaFishingHook, "lava hook covered by lavaproof bag");
        }

        private static void FishingAutoEquipmentKeepsStackableTackleBags()
        {
            var player = CreateFishingEquipmentPlayer();
            player.inventory[10] = Accessory(TestAnglerTackleBag, "Angler Tackle Bag");
            player.inventory[11] = Accessory(TestLavaproofTackleBag, "Lavaproof Tackle Bag");
            player.inventory[12] = Accessory(TestHighTestFishingLine, "High Test Fishing Line");
            player.inventory[13] = Accessory(TestTackleBox, "Tackle Box");
            player.inventory[14] = Accessory(TestLavaFishingHook, "Lavaproof Fishing Hook");

            var plan = BuildFishingAutoEquipmentPlan(player, FishingLiquidKind.Lava);
            AssertPlanContains(plan, TestAnglerTackleBag, "stackable angler tackle bag");
            AssertPlanContains(plan, TestLavaproofTackleBag, "stackable lavaproof tackle bag");
            AssertPlanDoesNotContain(plan, TestHighTestFishingLine, "covered high test line");
            AssertPlanDoesNotContain(plan, TestTackleBox, "covered tackle box");
            AssertPlanDoesNotContain(plan, TestLavaFishingHook, "lava hook covered by lavaproof bag");
        }

        private static void FishingAutoEquipmentLavaUsesHookWithoutLavaproofBag()
        {
            var player = CreateFishingEquipmentPlayer();
            player.inventory[10] = Accessory(TestAnglerTackleBag, "Angler Tackle Bag");
            player.inventory[11] = Accessory(TestLavaFishingHook, "Lavaproof Fishing Hook");
            player.inventory[12] = Accessory(TestHighTestFishingLine, "High Test Fishing Line");
            player.inventory[13] = Accessory(TestTackleBox, "Tackle Box");

            var plan = BuildFishingAutoEquipmentPlan(player, FishingLiquidKind.Lava);
            AssertPlanContains(plan, TestAnglerTackleBag, "angler tackle bag");
            AssertPlanContains(plan, TestLavaFishingHook, "lava hook without lavaproof bag");
            AssertPlanDoesNotContain(plan, TestHighTestFishingLine, "high test line covered by angler bag");
            AssertPlanDoesNotContain(plan, TestTackleBox, "tackle box covered by angler bag");
        }

        private static void FishingFilterRequiresSonarBuff()
        {
            var settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.DenyList;

            var noBuffSnapshot = new GameStateSnapshot
            {
                ActiveBuffs = new List<BuffSnapshot>()
            };
            if (FishingAutomationService.IsFishingFilterActiveForCatch(settings, noBuffSnapshot))
            {
                throw new InvalidOperationException("Fishing filter must stay inactive when Sonar buff is missing.");
            }

            var sonarSnapshot = new GameStateSnapshot
            {
                ActiveBuffs = new List<BuffSnapshot>
                {
                    new BuffSnapshot { BuffType = FishingAutomationService.SonarBuffType, BuffTime = 60 }
                }
            };
            if (!FishingAutomationService.IsFishingFilterActiveForCatch(settings, sonarSnapshot))
            {
                throw new InvalidOperationException("Fishing filter should become active when Sonar buff is present.");
            }

            settings.FishingFilterMode = FishingFilterModes.Disabled;
            if (FishingAutomationService.IsFishingFilterActiveForCatch(settings, sonarSnapshot))
            {
                throw new InvalidOperationException("Disabled fishing filter must stay inactive even with Sonar buff.");
            }
        }

        private static void FishingAutoEquipmentDeduplicatesLuckAndBobbers()
        {
            var player = CreateFishingEquipmentPlayer();
            player.inventory[10] = Accessory(TestLuckyCoin, "Lucky Coin");
            player.inventory[11] = Accessory(TestCoinRing, "Coin Ring");
            player.inventory[12] = Accessory(TestGreedyRing, "Greedy Ring");
            player.inventory[13] = Accessory(TestLuckyHorseshoe, "Lucky Horseshoe");
            player.inventory[14] = Accessory(TestBlueHorseshoeBalloon, "Blue Horseshoe Balloon");
            player.inventory[15] = Accessory(TestHorseshoeBundle, "Bundle of Horseshoe Balloons");
            player.inventory[16] = Accessory(TestFishingBobber, "Fishing Bobber");
            player.inventory[17] = Accessory(TestFishingBobberRainbow, "Rainbow Fishing Bobber");

            var plan = BuildFishingAutoEquipmentPlan(player, FishingLiquidKind.Water);
            AssertPlanContains(plan, TestGreedyRing, "best lucky coin line");
            AssertPlanContains(plan, TestHorseshoeBundle, "best horseshoe line");
            AssertPlanContains(plan, TestFishingBobberRainbow, "best bobber");
            AssertPlanDoesNotContain(plan, TestLuckyCoin, "lower lucky coin");
            AssertPlanDoesNotContain(plan, TestCoinRing, "middle lucky coin");
            AssertPlanDoesNotContain(plan, TestLuckyHorseshoe, "lower horseshoe");
            AssertPlanDoesNotContain(plan, TestBlueHorseshoeBalloon, "middle horseshoe");
            AssertPlanDoesNotContain(plan, TestFishingBobber, "lower bobber");
        }

        private static void FishingAutoEquipmentCapacityKeepsHighestScores()
        {
            var player = CreateFishingEquipmentPlayer();
            player.MaxUsableSlot = 4;
            player.inventory[10] = Accessory(TestLavaproofTackleBag, "Lavaproof Tackle Bag");
            player.inventory[11] = Accessory(TestAnglerEarring, "Angler Earring");
            player.inventory[12] = Accessory(TestFishingBobberRainbow, "Rainbow Fishing Bobber");
            player.inventory[13] = Accessory(TestGreedyRing, "Greedy Ring");

            var plan = BuildFishingAutoEquipmentPlan(player, FishingLiquidKind.Water);
            AssertPlanContains(plan, TestLavaproofTackleBag, "highest tackle bag");
            AssertPlanContains(plan, TestAnglerEarring, "second highest earring");
            AssertPlanDoesNotContain(plan, TestFishingBobberRainbow, "bobber beyond capacity");
            AssertPlanDoesNotContain(plan, TestGreedyRing, "luck beyond capacity");
        }

        private static void FishingAutoEquipmentReplacesCoveredLowerAccessoryFirst()
        {
            var player = CreateFishingEquipmentPlayer();
            player.armor[3] = Accessory(TestLavaproofTackleBag, "Lavaproof Tackle Bag");
            player.armor[4] = Accessory(TestHighTestFishingLine, "High Test Fishing Line");
            player.inventory[10] = Accessory(TestAnglerEarring, "Angler Earring");

            var plan = BuildFishingAutoEquipmentPlan(player, FishingLiquidKind.Water);
            AssertPlanContains(plan, TestAnglerEarring, "angler earring");
            if (plan.Moves.Count != 1 || plan.Moves[0].TargetEquipmentSlot != 4)
            {
                throw new InvalidOperationException("Expected covered High Test Fishing Line slot to be replaced before empty slots.");
            }
        }

        private static void FishingAutoEquipmentManualInventoryInteractionStopsKeepingRodAppliedWithoutBobber()
        {
            if (!FishingAutoEquipmentService.ShouldRestoreWithoutBobberForTesting(true, true))
            {
                throw new InvalidOperationException("Expected manual inventory interaction to restore after bobber is gone even while the original rod is still selected.");
            }

            if (FishingAutoEquipmentService.ShouldRestoreWithoutBobberForTesting(true, false))
            {
                throw new InvalidOperationException("Expected ordinary bobber scan gaps to keep auto equipment applied while the original rod is still selected.");
            }

            if (!FishingAutoEquipmentService.ShouldRestoreWithoutBobberForTesting(false, false))
            {
                throw new InvalidOperationException("Expected leaving the original rod to restore auto equipment.");
            }
        }

        private static void FishingAutoEquipmentRestoreKeepsPendingWhenOriginalMovedByUser()
        {
            var player = CreateFishingEquipmentPlayer();
            var fishingItem = Accessory(TestAnglerEarring, "Angler Earring");
            var originalItem = Accessory(TestLuckyCoin, "Lucky Coin");
            player.armor[3] = fishingItem;
            player.inventory[10] = null;

            var result = InvokeFishingAutoEquipmentRestoreRecords(
                player,
                new[]
                {
                    FishingAutoEquipmentRecord(3, FishingEquipmentContainerKind.Inventory, 10, fishingItem, originalItem)
                });

            if (result == null || result.PendingRestoreCount != 1 || result.OriginalMovedByUserCount != 1 || result.Records.Count != 1)
            {
                throw new InvalidOperationException("Expected restore to remain pending when the original accessory is temporarily unavailable.");
            }
        }

        private static void FishingAutoEquipmentRestoreCompletesWhenOriginalAlreadyBack()
        {
            var player = CreateFishingEquipmentPlayer();
            var fishingItem = Accessory(TestAnglerEarring, "Angler Earring");
            var originalItem = Accessory(TestLuckyCoin, "Lucky Coin");
            player.armor[3] = originalItem;
            player.inventory[10] = fishingItem;

            var result = InvokeFishingAutoEquipmentRestoreRecords(
                player,
                new[]
                {
                    FishingAutoEquipmentRecord(3, FishingEquipmentContainerKind.Inventory, 10, fishingItem, originalItem)
                });

            if (result == null || result.PendingRestoreCount != 0 || result.RestoredMoveCount != 1 || result.UserChangedManagedSlotCount != 0)
            {
                throw new InvalidOperationException("Expected restore to complete when the original accessory is already back in the managed slot.");
            }
        }


    }
}
