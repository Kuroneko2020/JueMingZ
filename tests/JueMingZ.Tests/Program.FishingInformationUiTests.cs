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

        private static void EnemySegmentLabelsHideMiddleSegments()
        {
            if (!InformationOverlayService.ShouldDrawEnemySegmentLabel(1, 0))
            {
                throw new InvalidOperationException("Expected ordinary single enemies to draw labels.");
            }

            if (!InformationOverlayService.ShouldDrawEnemySegmentLabel(5, 1))
            {
                throw new InvalidOperationException("Expected segment endpoints to draw labels.");
            }

            if (InformationOverlayService.ShouldDrawEnemySegmentLabel(5, 2))
            {
                throw new InvalidOperationException("Expected middle segments with two neighbors to hide labels.");
            }
        }

        private static void KnownWormSegmentLabelsUseTerrariaNpcRoles()
        {
            AssertSegmentLabelRoles("Devourer", new[] { 7, 9 }, new[] { 8 });
            AssertSegmentLabelRoles("Giant Worm", new[] { 10, 12 }, new[] { 11 });
            AssertSegmentLabelRoles("Eater of Worlds", new[] { 13, 15 }, new[] { 14 });
            AssertSegmentLabelRoles("Bone Serpent", new[] { 39, 41 }, new[] { 40 });
            AssertSegmentLabelRoles("Wyvern", new[] { 87, 92 }, new[] { 88, 89, 90, 91 });
            AssertSegmentLabelRoles("Digger", new[] { 95, 97 }, new[] { 96 });
            AssertSegmentLabelRoles("Seeker", new[] { 98, 100 }, new[] { 99 });
            AssertSegmentLabelRoles("Leech", new[] { 117, 119 }, new[] { 118 });
            AssertSegmentLabelRoles("Destroyer", new[] { 134, 136 }, new[] { 135 });
            AssertSegmentLabelRoles("Stardust Worm", new[] { 402, 404 }, new[] { 403 });
            AssertSegmentLabelRoles("Solar Crawltipede", new[] { 412, 414 }, new[] { 413 });
            AssertSegmentLabelRoles("Cultist Dragon", new[] { 454, 459 }, new[] { 455, 456, 457, 458 });
            AssertSegmentLabelRoles("Dune Splicer", new[] { 510, 512 }, new[] { 511 });
            AssertSegmentLabelRoles("Tomb Crawler", new[] { 513, 515 }, new[] { 514 });
            AssertSegmentLabelRoles("Blood Eel", new[] { 621, 623 }, new[] { 622 });
        }

        private static void SkeletonMerchantCountsAsInformationNpcLabel()
        {
            const int skeletonMerchantNpcType = 453;
            const int ordinaryEnemyNpcType = 3;

            if (!InformationOverlayService.IsNpcNameLabelCandidateForTesting(skeletonMerchantNpcType, false))
            {
                throw new InvalidOperationException("Expected Skeleton Merchant to count as an NPC name label candidate.");
            }

            if (InformationOverlayService.IsEnemyNameLabelCandidateForTesting(skeletonMerchantNpcType, false, false, 250, 250))
            {
                throw new InvalidOperationException("Expected Skeleton Merchant to stay out of enemy name labels.");
            }

            if (InformationOverlayService.IsNpcNameLabelCandidateForTesting(ordinaryEnemyNpcType, false))
            {
                throw new InvalidOperationException("Expected ordinary enemies to stay out of NPC name labels.");
            }

            if (!InformationOverlayService.IsEnemyNameLabelCandidateForTesting(ordinaryEnemyNpcType, false, false, 40, 40))
            {
                throw new InvalidOperationException("Expected ordinary enemies to remain eligible for enemy name labels.");
            }
        }

        private static void EnemyHealthLabelUsesCompactHealthLine()
        {
            var healthText = InformationOverlayService.BuildEnemyHealthTextForTesting(238, 500);
            if (!string.Equals(healthText, "238/500", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected enemy health label text to use current/max format.");
            }

            var healthScale = InformationOverlayService.ResolveEnemyHealthFontScaleForTesting(0.80f);
            if (Math.Abs(healthScale - 0.67f) > 0.001f)
            {
                throw new InvalidOperationException("Expected enemy health label scale to be 0.13 below the name scale.");
            }

            var clampedHealthScale = InformationOverlayService.ResolveEnemyHealthFontScaleForTesting(0.50f);
            if (Math.Abs(clampedHealthScale - 0.50f) > 0.001f)
            {
                throw new InvalidOperationException("Expected enemy health label scale to respect the shared minimum font scale.");
            }

            var tightAdvance = InformationWorldLabelRenderer.ResolveTightStackedLineAdvanceForTesting(0.80f, 0.67f);
            if (Math.Abs(tightAdvance - 12.0f) > 0.001f)
            {
                throw new InvalidOperationException("Expected enemy health label rows to leave enough tight spacing for the larger health scale.");
            }
        }

        private static void EnemyHealthLabelSnapshotTracksLifeText()
        {
            if (!InformationOverlayService.CanReuseNpcLabelHealthValuesForTesting(80, 100, 80, 100))
            {
                throw new InvalidOperationException("Expected enemy health label cache to reuse unchanged life text.");
            }

            if (InformationOverlayService.CanReuseNpcLabelHealthValuesForTesting(80, 100, 79, 100))
            {
                throw new InvalidOperationException("Expected enemy health label cache to dirty when current life changes.");
            }

            if (InformationOverlayService.CanReuseNpcLabelHealthValuesForTesting(80, 100, 80, 120))
            {
                throw new InvalidOperationException("Expected enemy health label cache to dirty when max life changes.");
            }
        }

        private static void AssertSegmentLabelRoles(string name, int[] headOrTailTypes, int[] bodyTypes)
        {
            foreach (var npcType in headOrTailTypes)
            {
                if (!InformationOverlayService.ShouldDrawEnemyNpcTypeLabelForTesting(npcType))
                {
                    throw new InvalidOperationException("Expected " + name + " head/tail type " + npcType.ToString(CultureInfo.InvariantCulture) + " to draw labels.");
                }
            }

            foreach (var npcType in bodyTypes)
            {
                if (InformationOverlayService.ShouldDrawEnemyNpcTypeLabelForTesting(npcType))
                {
                    throw new InvalidOperationException("Expected " + name + " body type " + npcType.ToString(CultureInfo.InvariantCulture) + " to hide labels.");
                }
            }
        }

        private static void InformationSignTextAllModeKeepsVanillaLineCap()
        {
            var text = "1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12";
            var lines = InformationOverlayService.BuildSignTextDisplayLinesForTesting(text, InformationSignTextModes.All, 99, 999, 0.7f);
            if (lines.Length != InformationSignTextModes.VanillaDisplayMaxLines)
            {
                throw new InvalidOperationException("Expected all mode to keep vanilla 10-line display cap.");
            }

            if (!lines[lines.Length - 1].EndsWith("...", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected all mode to mark text hidden past the vanilla display cap.");
            }
        }

        private static void InformationSignTextLineModeRespectsConfiguredLines()
        {
            var lines = InformationOverlayService.BuildSignTextDisplayLinesForTesting("alpha\nbeta\ngamma", InformationSignTextModes.Lines, 2, 999, 0.7f);
            if (lines.Length != 2 ||
                !string.Equals(lines[0], "alpha", StringComparison.Ordinal) ||
                !lines[1].StartsWith("beta", StringComparison.Ordinal) ||
                !lines[1].EndsWith("...", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected line mode to show only the configured leading lines and mark truncation.");
            }

            if (InformationSignTextModes.ClampLines(99) != InformationSignTextModes.VanillaDisplayMaxLines)
            {
                throw new InvalidOperationException("Expected sign line limit to clamp to the vanilla 10-line display cap.");
            }
        }

        private static void InformationSignTextCharacterModeTruncatesBeforeWrapping()
        {
            var lines = InformationOverlayService.BuildSignTextDisplayLinesForTesting("abcdef ghij", InformationSignTextModes.Characters, 10, 5, 0.7f);
            if (lines.Length != 1 ||
                !lines[0].StartsWith("abcde", StringComparison.Ordinal) ||
                !lines[0].EndsWith("...", StringComparison.Ordinal) ||
                lines[0].Contains("f"))
            {
                throw new InvalidOperationException("Expected character mode to trim by characters before wrapping.");
            }

            if (InformationSignTextModes.ClampCharacters(9999) != InformationSignTextModes.MaxCharacters)
            {
                throw new InvalidOperationException("Expected sign character limit to clamp to configured maximum.");
            }

            if (InformationSignTextModes.MaxCharacters != 1200 ||
                InformationSignTextModes.AdjustCharacters(80, 1) != 81 ||
                InformationSignTextModes.AdjustCharacters(80, -1) != 79 ||
                InformationSignTextModes.AdjustCharacters(1, -1) != 1)
            {
                throw new InvalidOperationException("Expected sign character limit to use the vanilla virtual keyboard max and adjust one character at a time.");
            }
        }

        private static void InformationSignTextCentersEachDisplayedLineOnSign()
        {
            var x = InformationOverlayService.CalculateSignTextLineXForTesting(100f, 40, 240);
            if (Math.Abs(x - 80f) > 0.001f)
            {
                throw new InvalidOperationException("Expected sign text line to center on sign center.");
            }

            var leftClamped = InformationOverlayService.CalculateSignTextLineXForTesting(10f, 40, 240);
            if (Math.Abs(leftClamped - 4f) > 0.001f)
            {
                throw new InvalidOperationException("Expected centered sign text to clamp at the left screen edge.");
            }

            var rightClamped = InformationOverlayService.CalculateSignTextLineXForTesting(230f, 40, 240);
            if (Math.Abs(rightClamped - 196f) > 0.001f)
            {
                throw new InvalidOperationException("Expected centered sign text to clamp at the right screen edge.");
            }
        }

        private static void InformationSignTextLayoutCacheReusesWrappedLines()
        {
            InformationOverlayService.ResetSignTextLayoutCacheForTesting();
            try
            {
                var first = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta gamma", InformationSignTextModes.Lines, 3, 999, 0.7f);
                if (first.RebuildCount != 1 ||
                    first.LineCount <= 0 ||
                    string.IsNullOrEmpty(first.FirstLineText) ||
                    first.FirstLineWidth <= 0 ||
                    first.LineHeight <= 0 ||
                    first.TotalHeight != first.LineHeight * first.LineCount)
                {
                    throw new InvalidOperationException("Expected initial sign text layout to cache wrapped lines, widths and height.");
                }

                var same = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta gamma", InformationSignTextModes.Lines, 3, 999, 0.7f);
                if (same.RebuildCount != first.RebuildCount ||
                    same.LineCount != first.LineCount ||
                    !string.Equals(same.FirstLineText, first.FirstLineText, StringComparison.Ordinal) ||
                    same.FirstLineWidth != first.FirstLineWidth ||
                    same.TotalHeight != first.TotalHeight)
                {
                    throw new InvalidOperationException("Expected identical sign text layout key to reuse the cached layout.");
                }

                var signDiagnostics = InformationOverlayService.GetDiagnostics();
                if (signDiagnostics.SignTextLayoutCacheMissCount != 1 ||
                    signDiagnostics.SignTextLayoutCacheHitCount != 1)
                {
                    throw new InvalidOperationException("Expected sign text layout diagnostics to count cache hit and miss paths.");
                }

                var changedText = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta changed", InformationSignTextModes.Lines, 3, 999, 0.7f);
                if (changedText.RebuildCount <= same.RebuildCount)
                {
                    throw new InvalidOperationException("Expected changed sign text to invalidate the cached layout.");
                }

                var changedMode = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta changed", InformationSignTextModes.Characters, 3, 5, 0.7f);
                if (changedMode.RebuildCount <= changedText.RebuildCount ||
                    !changedMode.FirstLineText.EndsWith("...", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected sign text layout mode and character limit to be part of the cache key.");
                }

                var beforeFontInvalidation = changedMode.RebuildCount;
                UiTextRenderer.InvalidateCachedResources("sign text layout cache test");
                var fontInvalidated = InformationOverlayService.BuildSignTextLayoutSnapshotForTesting("alpha beta changed", InformationSignTextModes.Characters, 3, 5, 0.7f);
                if (fontInvalidated.RebuildCount <= beforeFontInvalidation)
                {
                    throw new InvalidOperationException("Expected UI text renderer cache invalidation to rebuild sign text layout.");
                }
            }
            finally
            {
                InformationOverlayService.ResetSignTextLayoutCacheForTesting();
            }
        }

        private static void InformationPageContentHeightIncludesLimitRows()
        {
            var settings = AppSettings.CreateDefault();
            settings.InformationSignTextLabelsMode = InformationSignTextModes.Off;
            settings.InformationTombstoneTextLabelsMode = InformationSignTextModes.Off;
            var baseHeight = LegacyMainWindow.CalculateInformationContentHeightForTesting(settings);
            var rowStep = LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;

            settings.InformationSignTextLabelsMode = InformationSignTextModes.Lines;
            var signLimitHeight = LegacyMainWindow.CalculateInformationContentHeightForTesting(settings);
            if (signLimitHeight != baseHeight + rowStep)
            {
                throw new InvalidOperationException("Expected information content height to include the sign limit row.");
            }

            settings.InformationTombstoneTextLabelsMode = InformationSignTextModes.Characters;
            var bothLimitHeight = LegacyMainWindow.CalculateInformationContentHeightForTesting(settings);
            if (bothLimitHeight != baseHeight + rowStep * 2)
            {
                throw new InvalidOperationException("Expected information content height to include both sign and tombstone limit rows.");
            }
        }

        private static void UiClippedSingleLineTextAvoidsBottomStick()
        {
            float drawX;
            float drawY;
            if (!UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("半露文字", 12, 86, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected top-clipped single line text to remain drawable.");
            }

            if (drawY >= 100f)
            {
                throw new InvalidOperationException("Expected top-clipped text to keep natural Y for partial clipping.");
            }

            if (!UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("底部可见", 12, 260, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected bottom-clipped single line text to draw when its natural position is still inside the viewport.");
            }

            var maxY = 100f + 180f - 16f * 0.86f;
            if (drawY > maxY + 0.01f)
            {
                throw new InvalidOperationException("Expected naturally visible bottom text to stay inside viewport.");
            }

            if (!UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("底部半露", 12, 268, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected bottom half-visible text to remain drawable.");
            }

            if (drawY <= maxY + 0.01f)
            {
                throw new InvalidOperationException("Expected bottom half-visible text to keep natural Y beyond clip edge.");
            }

            if (UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("底部越界", 12, 286, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected fully out-of-viewport bottom text to stay hidden.");
            }

            if (UiTextRenderer.TryCalculateClippedSingleLinePositionForTesting("顶部越界", 12, 68, 120, 20, UiTextHorizontalAlignment.Left, 0, 100, 240, 180, 0.86f, out drawX, out drawY))
            {
                throw new InvalidOperationException("Expected fully out-of-viewport top text to stay hidden.");
            }
        }

        private static void InformationWorldContextCacheScopesStatusProfile()
        {
            var player = new object();
            var cachedStatus = CreateInformationContextForCacheTest(player, "world-a#1", 100, 12f, 24f, 800, 600);
            var sameProbe = CreateInformationContextForCacheTest(player, "world-a#1", 100, 12f, 24f, 800, 600);
            if (!InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.Status,
                    InformationWorldContextProfile.Status,
                    sameProbe))
            {
                throw new InvalidOperationException("Expected same tick, screen, player and world to reuse the status context cache.");
            }

            if (InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.Status,
                    InformationWorldContextProfile.FullRecord,
                    sameProbe))
            {
                throw new InvalidOperationException("Expected a status-only cached context not to satisfy full record callers.");
            }

            if (!InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.FullRecord,
                    InformationWorldContextProfile.Status,
                    sameProbe))
            {
                throw new InvalidOperationException("Expected a full record cached context to satisfy status callers.");
            }

            var movedProbe = CreateInformationContextForCacheTest(player, "world-a#1", 100, 16f, 24f, 800, 600);
            if (InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.FullRecord,
                    InformationWorldContextProfile.Status,
                    movedProbe))
            {
                throw new InvalidOperationException("Expected screen movement to dirty the information world context cache.");
            }

            var worldProbe = CreateInformationContextForCacheTest(player, "world-b#2", 100, 12f, 24f, 800, 600);
            if (InformationWorldContextProvider.CanReuseCachedContextForTesting(
                    cachedStatus,
                    InformationWorldContextProfile.FullRecord,
                    InformationWorldContextProfile.Status,
                    worldProbe))
            {
                throw new InvalidOperationException("Expected world changes to dirty the information world context cache.");
            }

            if (InformationWorldContextProvider.RequiresFileDataForTesting(InformationWorldContextProfile.Status))
            {
                throw new InvalidOperationException("Expected status context profile to avoid full player/world file data.");
            }

            if (!InformationWorldContextProvider.RequiresFileDataForTesting(InformationWorldContextProfile.FullRecord))
            {
                throw new InvalidOperationException("Expected full record context profile to keep player/world file data.");
            }

            if (InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.Status, player, "world-a#1", 100, player, "world-a#1", 160))
            {
                throw new InvalidOperationException("Expected status profile to skip file data refresh checks.");
            }

            if (InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.FullRecord, player, "world-a#1", 100, player, "world-a#1", 159))
            {
                throw new InvalidOperationException("Expected full record file data to reuse within the low-frequency refresh window.");
            }

            if (!InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.FullRecord, player, "world-a#1", 100, player, "world-a#1", 160))
            {
                throw new InvalidOperationException("Expected full record file data to refresh at the low-frequency cadence.");
            }

            if (!InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.FullRecord, player, "world-a#1", 100, new object(), "world-a#1", 110))
            {
                throw new InvalidOperationException("Expected player changes to dirty full record file data.");
            }

            if (!InformationWorldContextProvider.ShouldRefreshFileDataForTesting(InformationWorldContextProfile.FullRecord, player, "world-a#1", 100, player, "world-b#2", 110))
            {
                throw new InvalidOperationException("Expected world changes to dirty full record file data.");
            }
        }

        private static void InformationStatusLineCacheTracksContextIdentity()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

                var settings = AppSettings.CreateDefault();
                settings.InformationBiomeDisplayEnabled = true;
                var player = new object();
                var context = CreateInformationContextForCacheTest(player, "world-a#1", 120, 12f, 24f, 800, 600);
                var signature = InformationOverlayService.BuildStatusLineCacheSignatureForTesting(context, settings);
                if (!InformationOverlayService.CanReuseStatusLinesForTesting(100, signature, context, settings))
                {
                    throw new InvalidOperationException("Expected unchanged status line context to reuse within the refresh cadence.");
                }

                context.GameUpdateCount = 130;
                if (InformationOverlayService.CanReuseStatusLinesForTesting(100, signature, context, settings))
                {
                    throw new InvalidOperationException("Expected status line cache to refresh at the 30 tick cadence.");
                }

                context.GameUpdateCount = 120;
                var worldChanged = CreateInformationContextForCacheTest(player, "world-b#2", 120, 12f, 24f, 800, 600);
                if (string.Equals(signature, InformationOverlayService.BuildStatusLineCacheSignatureForTesting(worldChanged, settings), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected status line cache signature to track world identity.");
                }

                var playerChanged = CreateInformationContextForCacheTest(new object(), "world-a#1", 120, 12f, 24f, 800, 600);
                if (string.Equals(signature, InformationOverlayService.BuildStatusLineCacheSignatureForTesting(playerChanged, settings), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected status line cache signature to track local player identity.");
                }

                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                if (string.Equals(signature, InformationOverlayService.BuildStatusLineCacheSignatureForTesting(context, settings), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected status line cache signature to dirty on UI language changes.");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        private static void InformationFishingStatusLineBuilderKeepsDisplayRows()
        {
            var settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.AllowList;
            settings.FishingFilterMatchMode = FishingFilterMatchModes.Exact;
            settings.FishingFilterAllowExactEntries.Add(new FishingFilterExactEntry
            {
                Kind = FishingCatchKinds.Item,
                Id = 1,
                DisplayNameSnapshot = "Bass"
            });

            var candidates = new List<FishingCatchCandidate>
            {
                new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = 1,
                    DisplayName = "Bass",
                    DisplayNameSnapshot = "Bass"
                },
                new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = 2,
                    DisplayName = "Old Shoe",
                    DisplayNameSnapshot = "Old Shoe"
                }
            };

            var color = new InformationColor(135, 206, 250, 255);
            var lines = new List<InformationStatusLine>();
            InformationFishingStatusLineBuilder.AddFishingCatchLines(lines, 40, true, candidates, string.Empty, color, 0.72d);
            InformationFishingStatusLineBuilder.AddFilteredFishingCatchLines(lines, 45, settings, true, true, candidates, string.Empty, color, 0.72d);

            if (lines.Count != 2 ||
                lines[0].Order != 40 ||
                !string.Equals(lines[0].Text, "完整鱼获: Bass、Old Shoe", StringComparison.Ordinal) ||
                lines[1].Order != 45 ||
                !string.Equals(lines[1].Text, "过滤鱼获: Bass", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fishing status builders to keep full and filtered display rows separate.");
            }

            settings.FishingFilterMode = FishingFilterModes.Disabled;
            lines.Clear();
            InformationFishingStatusLineBuilder.AddFilteredFishingCatchLines(lines, 45, settings, true, true, candidates, string.Empty, color, 0.72d);
            if (lines.Count != 1 || !string.Equals(lines[0].Text, "过滤鱼获: 过滤未启用", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected filtered fishing display to keep the disabled-filter row.");
            }
        }

        private static void InformationStatusPanelLayoutCacheReusesPreparedRows()
        {
            InformationStatusPanelService.ResetLayoutCacheForTesting();
            try
            {
                var lines = new List<InformationStatusLine>
                {
                    new InformationStatusLine
                    {
                        Text = "群系: 森林",
                        Color = new InformationColor(210, 235, 255, 240),
                        FontScale = 0.72d
                    },
                    new InformationStatusLine
                    {
                        Text = "幸运: 普通",
                        Color = new InformationColor(230, 230, 190, 240),
                        FontScale = 0.68d
                    }
                };

                var first = InformationStatusPanelService.BuildLayoutSnapshotForTesting(lines, 800, 600, true, 20, 200, false);
                if (first.RebuildCount != 1 || first.RowCount != 2 || !string.Equals(first.FirstRowText, "群系: 森林", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the initial status panel layout to build visible rows.");
                }

                var moved = InformationStatusPanelService.BuildLayoutSnapshotForTesting(lines, 800, 600, true, 140, 260, false);
                if (moved.RebuildCount != first.RebuildCount ||
                    moved.Width != first.Width ||
                    moved.Height != first.Height ||
                    moved.FirstRowDrawX != first.FirstRowDrawX + 120 ||
                    moved.FirstRowDrawY != first.FirstRowDrawY + 60)
                {
                    throw new InvalidOperationException("Expected position-only changes to reuse prepared row layout and only offset draw coordinates.");
                }

                if (InformationStatusPanelService.LayoutCacheMissCount != 1 ||
                    InformationStatusPanelService.LayoutCacheHitCount != 1)
                {
                    throw new InvalidOperationException("Expected status panel diagnostics to count layout cache hit and miss paths.");
                }

                lines[0].Text = "群系: 雪原";
                var changed = InformationStatusPanelService.BuildLayoutSnapshotForTesting(lines, 800, 600, true, 140, 260, false);
                if (changed.RebuildCount <= moved.RebuildCount || !string.Equals(changed.FirstRowText, "群系: 雪原", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected status panel text changes to rebuild cached layout.");
                }

                var resized = InformationStatusPanelService.BuildLayoutSnapshotForTesting(lines, 900, 600, true, 140, 260, false);
                if (resized.RebuildCount <= changed.RebuildCount)
                {
                    throw new InvalidOperationException("Expected screen size changes to rebuild cached layout.");
                }

                var adjusting = InformationStatusPanelService.BuildLayoutSnapshotForTesting(new List<InformationStatusLine>(), 800, 600, true, 20, 200, true);
                if (adjusting.RowCount != 1 || !string.Equals(adjusting.FirstRowText, "信息窗", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected adjustment placeholder text to remain available when there are no status rows.");
                }

                var beforeFontInvalidation = adjusting.RebuildCount;
                UiTextRenderer.InvalidateCachedResources("status panel layout cache test");
                var fontInvalidated = InformationStatusPanelService.BuildLayoutSnapshotForTesting(new List<InformationStatusLine>(), 800, 600, true, 20, 200, true);
                if (fontInvalidated.RebuildCount <= beforeFontInvalidation)
                {
                    throw new InvalidOperationException("Expected UI text renderer cache invalidation to rebuild status panel layout.");
                }
            }
            finally
            {
                InformationStatusPanelService.ResetLayoutCacheForTesting();
            }
        }

        private static void UiTextRendererFastPathKeepsSafeFallbacks()
        {
            if (UiTextRenderer.DrawText(null, "文字", 10f, 10f, 255, 255, 255, 255, 1f))
            {
                throw new InvalidOperationException("Expected null SpriteBatch draw to fail safely.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, true, string.Empty, 1f, 0f, 0f))
            {
                throw new InvalidOperationException("Expected empty text to skip the fast path.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(false, true, "文字", 1f, 0f, 0f))
            {
                throw new InvalidOperationException("Expected missing SpriteBatch to skip the fast path.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, false, "文字", 1f, 0f, 0f))
            {
                throw new InvalidOperationException("Expected missing font to skip the fast path.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, true, "文字", 1f, 0.5f, 0f))
            {
                throw new InvalidOperationException("Expected anchored text to keep the DrawBorderString fallback.");
            }

            if (!UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, true, "文字", 0.01f, 0f, 0f))
            {
                throw new InvalidOperationException("Expected tiny positive scale to remain eligible after clamping.");
            }

            if (Math.Abs(UiTextRenderer.ResolveEffectiveScaleForTesting(0.01f) - 0.1f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected tiny scale to clamp to the existing 0.1 minimum.");
            }

            if (UiTextRenderer.IsAnchorFreeFastPathEligibleForTesting(true, true, "文字", float.NaN, 0f, 0f))
            {
                throw new InvalidOperationException("Expected invalid scale to keep the fallback path.");
            }
        }

        private static void UiTextRendererFontSignatureChangeClearsCaches()
        {
            if (!UiTextRenderer.FontSignatureChangeClearsCachesForTesting())
            {
                throw new InvalidOperationException("Expected font signature change to clear UI text caches and fast path suspension.");
            }
        }

        private static void InformationTombstoneTextDefaultsToRedAndSplitsTileType()
        {
            var settings = AppSettings.CreateDefault();
            if (!string.Equals(settings.InformationTombstoneTextLabelsMode, InformationSignTextModes.Off, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tombstone text labels to default to Off.");
            }

            if (!string.Equals(InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.TombstoneTextFeatureId), "#FF5555", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tombstone text labels to default to red.");
            }

            if (!InformationOverlayService.IsTombstoneTileTypeForTesting(85) ||
                InformationOverlayService.IsTombstoneTileTypeForTesting(55))
            {
                throw new InvalidOperationException("Expected tombstone text labels to split TileID.Tombstones from normal signs.");
            }
        }

        private static void InformationManaCrystalHighlightDefaultsToOffAndUsesTileId()
        {
            var settings = AppSettings.CreateDefault();
            if (settings.InformationHighlightManaCrystalEnabled)
            {
                throw new InvalidOperationException("Expected mana crystal highlight to default to Off.");
            }

            var color = InformationColorHelper.ManaCrystal(settings);
            if (color.R != 102 || color.G != 204 || color.B != 255 || color.A != 255)
            {
                throw new InvalidOperationException("Expected mana crystal highlight to default to mana blue.");
            }

            if (!InformationOverlayService.IsManaCrystalTileTypeForTesting(639) ||
                InformationOverlayService.IsManaCrystalTileTypeForTesting(12))
            {
                throw new InvalidOperationException("Expected mana crystal highlight to use TileID.ManaCrystal=639 and stay distinct from life crystals.");
            }
        }

        private static void InformationTileAccessReadsCachedTileMembers()
        {
            bool active;
            int type;
            int frameX;
            int frameY;
            var propertyTile = new TestInformationPropertyTile
            {
                HasTile = true,
                TileType = 639,
                TileFrameX = 18,
                TileFrameY = 36
            };

            if (!InformationTileAccess.TryReadActiveTypeAndFrame(propertyTile, out active, out type, out frameX, out frameY) ||
                !active ||
                type != 639 ||
                frameX != 18 ||
                frameY != 36)
            {
                throw new InvalidOperationException("Expected cached tile accessor to read property-style tile members.");
            }

            var methodTile = new TestInformationMethodTile();
            if (!InformationTileAccess.TryReadActiveTypeAndFrame(methodTile, out active, out type, out frameX, out frameY) ||
                !active ||
                type != 236 ||
                frameX != 54 ||
                frameY != 72)
            {
                throw new InvalidOperationException("Expected cached tile accessor to read vanilla-style method and field tile members.");
            }
        }

        private static void InformationTileHighlightCacheSignatureTracksBoundsSettingsAndWorld()
        {
            var settings = AppSettings.CreateDefault();
            settings.InformationHighlightLifeCrystalEnabled = true;
            var context = CreateInformationTileHighlightContext(1025f, 1000f, 800, 600, 512f, 512f, "world-a", "world-record-a");
            var signature = InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(context, settings);

            var sameTileBounds = CreateInformationTileHighlightContext(1035f, 1000f, 800, 600, 520f, 512f, "world-a", "world-record-a");
            var sameTileBoundsSignature = InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(sameTileBounds, settings);
            if (!string.Equals(signature, sameTileBoundsSignature, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to survive sub-tile screen movement inside the same scan bounds and player chunk.");
            }

            var crossedTileBounds = CreateInformationTileHighlightContext(1041f, 1000f, 800, 600, 520f, 512f, "world-a", "world-record-a");
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(crossedTileBounds, settings), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to dirty when the screen tile scan bounds change.");
            }

            var crossedPlayerChunk = CreateInformationTileHighlightContext(1025f, 1000f, 800, 600, 900f, 512f, "world-a", "world-record-a");
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(crossedPlayerChunk, settings), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to include the player tile chunk.");
            }

            var otherWorld = CreateInformationTileHighlightContext(1025f, 1000f, 800, 600, 512f, 512f, "world-b", "world-record-b");
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(otherWorld, settings), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to dirty across world identity changes.");
            }

            var extraHighlight = AppSettings.CreateDefault();
            extraHighlight.InformationHighlightLifeCrystalEnabled = true;
            extraHighlight.InformationHighlightLifeFruitEnabled = true;
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(context, extraHighlight), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to dirty when enabled highlight types change.");
            }

            var changedColor = AppSettings.CreateDefault();
            changedColor.InformationHighlightLifeCrystalEnabled = true;
            changedColor.InformationLifeCrystalHighlightColor = "#00FF00";
            if (string.Equals(signature, InformationOverlayService.BuildTileHighlightCacheSignatureForTesting(context, changedColor), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected tile highlight signature to dirty when highlight color configuration changes.");
            }
        }

        private static void InformationOverlayContextProfilesRouteStatusAndWorldRecord()
        {
            var settings = AppSettings.CreateDefault();
            settings.InformationBiomeDisplayEnabled = true;

            if (InformationOverlayService.BuildStatusContextProfileForTesting(settings) != InformationWorldContextProfile.Status)
            {
                throw new InvalidOperationException("Expected status panel rendering to use the lightweight information context profile.");
            }

            if (InformationOverlayService.BuildWorldOverlayContextProfileForTesting(settings) != InformationWorldContextProfile.FullRecord)
            {
                throw new InvalidOperationException("Expected world overlay rendering to keep full record context for player/world scoped records.");
            }
        }

        private static InformationWorldContext CreateInformationContextForCacheTest(object player, string worldKey, ulong updateCount, float screenX, float screenY, int screenWidth, int screenHeight)
        {
            return new InformationWorldContext
            {
                MainType = typeof(Program),
                LocalPlayer = player,
                ScreenX = screenX,
                ScreenY = screenY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                PlayerCenterX = 240f,
                PlayerCenterY = 320f,
                GameUpdateCount = updateCount,
                WorldKey = worldKey
            };
        }

        private static void InformationTileHighlightCacheKeepsSafetyRefresh()
        {
            if (!InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(0, 1u, 10, 1u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to scan when no previous scan tick exists.");
            }

            if (InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(100, 1u, 159, 1u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to reuse before the safety refresh interval.");
            }

            if (!InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(100, 1u, 160, 1u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to keep the 60 tick safety refresh when no tile generation is available.");
            }

            if (!InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(100, 1u, 120, 2u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to refresh immediately when its stable signature changes.");
            }

            if (!InformationOverlayService.ShouldRefreshTileHighlightCacheForTesting(100, 1u, 90, 1u))
            {
                throw new InvalidOperationException("Expected tile highlight cache to refresh after a tick counter rewind.");
            }
        }

        private static void InformationTileHighlightScannerGroupsAdjacentEnabledTiles()
        {
            try
            {
                FakeTileHighlightMain.ConfigureLifeCrystalGroup();
                var context = CreateInformationTileHighlightContext(0f, 0f, 320, 320, 80f, 80f, "tile-highlight-world", "tile-highlight-record");
                context.MainType = typeof(FakeTileHighlightMain);
                context.GameUpdateCount = 100;

                var settings = AppSettings.CreateDefault();
                InformationOverlayService.ResetTileHighlightCacheForTesting();
                if (InformationOverlayService.GetTileHighlightCountForTesting(context, settings) != 0)
                {
                    throw new InvalidOperationException("Expected disabled tile highlights to skip scanner results.");
                }

                settings.InformationHighlightLifeCrystalEnabled = true;
                var count = InformationOverlayService.GetTileHighlightCountForTesting(context, settings);
                if (count != 1)
                {
                    throw new InvalidOperationException("Expected adjacent life crystal tiles to be merged into one highlight group, got " + count + ".");
                }

                context.GameUpdateCount = 159;
                var cachedCount = InformationOverlayService.GetTileHighlightCountForTesting(context, settings);
                if (cachedCount != 1)
                {
                    throw new InvalidOperationException("Expected cached tile highlight group to remain stable before safety refresh, got " + cachedCount + ".");
                }
            }
            finally
            {
                FakeTileHighlightMain.Reset();
                InformationOverlayService.ResetTileHighlightCacheForTesting();
            }
        }

        private static void InformationFishingCatchQueryKeyTracksEnvironment()
        {
            var player = new TestInformationFishingEnvironmentPlayer
            {
                luck = 0.25d,
                fishingSkill = 12,
                accLavaFishing = true,
                ZoneJungle = true
            };
            var context = new InformationWorldContext
            {
                LocalPlayer = player,
                WorldKey = "world-a#42",
                PlayerCenterY = 1200f
            };

            var signature = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-a");

            var same = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-a");
            if (!string.Equals(signature, same, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected identical fishing catch query inputs to reuse the same cache signature.");
            }

            var movedTile = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                81,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-a");
            if (string.Equals(signature, movedTile, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected bobber tile changes to dirty the fishing catch cache key.");
            }

            var changedFilter = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-b");
            if (string.Equals(signature, changedFilter, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fishing filter configuration changes to dirty the fishing catch cache key.");
            }

            player.ZoneJungle = false;
            var changedZone = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-a");
            if (string.Equals(signature, changedZone, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected player biome changes to dirty the fishing catch cache key.");
            }
        }

        private static void InformationFishingCatchQueryKeyTracksFullBaselineFields()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            TestInformationFishingQueryMain.Reset();
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                var player = new TestInformationFishingEnvironmentPlayer
                {
                    luck = 0.25d,
                    fishingSkill = 12,
                    accLavaFishing = true,
                    ZoneJungle = true
                };
                var context = new InformationWorldContext
                {
                    LocalPlayer = player,
                    MainType = typeof(TestInformationFishingQueryMain),
                    WorldKey = "query-field-world",
                    PlayerCenterY = 1200f
                };

                var signature = BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 45, false, "filter-a");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "lava", 320, 1, 300, false, 45, 45, false, "filter-a"), "liquid");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 321, 1, 300, false, 45, 45, false, "filter-a"), "water tiles");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 2, 300, false, 45, 45, false, "filter-a"), "chums");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 240, false, 45, 45, false, "filter-a"), "water needed");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, true, 45, 45, false, "filter-a"), "junk");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 44, false, "filter-a"), "effective fishing level");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 45, true, "filter-a"), "lava capability");

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 45, false, "filter-a"), "language");

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                TestInformationFishingQueryMain.hardMode = true;
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 45, false, "filter-a"), "world state");
            }
            finally
            {
                TestInformationFishingQueryMain.Reset();
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        private static void InformationFishingCatchEarlyKeyTracksEnvironment()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                var player = new TestInformationFishingEnvironmentPlayer
                {
                    luck = 0.25d,
                    fishingSkill = 12,
                    accLavaFishing = true,
                    ZoneJungle = true,
                    buffType = new[] { 111, 0, 0 },
                    buffTime = new[] { 30, 0, 0 }
                };
                var context = new InformationWorldContext
                {
                    LocalPlayer = player,
                    WorldKey = "world-a#42",
                    PlayerCenterY = 1200f
                };

                var signature = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);

                var same = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (!string.Equals(signature, same, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected identical early fishing catch inputs to reuse the same cache signature.");
                }

                var changedWorldContext = new InformationWorldContext
                {
                    LocalPlayer = player,
                    WorldKey = "world-b#99",
                    PlayerCenterY = 1200f
                };
                var changedWorld = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    changedWorldContext,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, changedWorld, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected world changes to dirty the early fishing catch cache key.");
                }

                var otherPlayer = new TestInformationFishingEnvironmentPlayer
                {
                    luck = 0.25d,
                    fishingSkill = 12,
                    accLavaFishing = true,
                    ZoneJungle = true,
                    buffType = new[] { 111, 0, 0 },
                    buffTime = new[] { 30, 0, 0 }
                };
                var changedPlayerContext = new InformationWorldContext
                {
                    LocalPlayer = otherPlayer,
                    WorldKey = "world-a#42",
                    PlayerCenterY = 1200f
                };
                var changedPlayer = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    changedPlayerContext,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, changedPlayer, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected player identity changes to dirty the early fishing catch cache key.");
                }

                var movedBobber = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    81,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, movedBobber, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected bobber tile changes to dirty the early fishing catch cache key.");
                }

                var changedQuest = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2455);
                if (string.Equals(signature, changedQuest, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected angler quest fish changes to dirty the early fishing catch cache key.");
                }

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                var changedLanguage = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, changedLanguage, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected language changes to dirty the early fishing catch cache key.");
                }

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                player.buffType[1] = 222;
                player.buffTime[1] = 60;
                var changedBuff = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, changedBuff, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected active player buff changes to dirty the early fishing catch cache key.");
                }

                var settings = AppSettings.CreateDefault();
                settings.InformationFishingFilteredCatchesEnabled = true;
                settings.FishingFilterMode = FishingFilterModes.AllowList;
                var filterSignature = InformationOverlayService.BuildStatusLineCacheSignatureForTesting(context, settings);
                settings.FishingFilterMode = FishingFilterModes.DenyList;
                var changedFilterSignature = InformationOverlayService.BuildStatusLineCacheSignatureForTesting(context, settings);
                if (string.Equals(filterSignature, changedFilterSignature, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected fishing filter changes to dirty status-line output without being part of the early environment cache.");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        private static void InformationFishingCatchEarlyCacheHitSkipsHeavyCounters()
        {
            // This is a performance regression guard: cache hits must not read
            // water or FishingConditions just to refresh display diagnostics.
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            var candidates = new List<FishingCatchCandidate>
            {
                new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = 1,
                    DisplayName = "Cached Fish",
                    DisplayNameSnapshot = "Cached Fish"
                }
            };

            InformationFishingCatchResolver.StoreEarlyCatchCacheForTesting("early:test", candidates, "cached");
            IList<FishingCatchCandidate> cached;
            string message;
            if (!InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:test", out cached, out message) ||
                cached.Count != 1 ||
                !string.Equals(message, "cached", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected early fishing catch cache to return stored candidates and message.");
            }

            if (InformationFishingCatchResolver.EarlyCacheHitCount != 1 ||
                InformationFishingCatchResolver.EarlyCacheMissCount != 0)
            {
                throw new InvalidOperationException("Expected early fishing catch cache hit diagnostics to increment once.");
            }

            if (InformationFishingCatchResolver.WaterScanCount != 0 ||
                InformationFishingCatchResolver.ConditionsReadCount != 0)
            {
                throw new InvalidOperationException("Expected early fishing catch cache hit to bypass water scan and fishing conditions read counters.");
            }
        }

        private static void InformationFishingCatchCachesKeepConfiguredLimits()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                var earlyLimit = InformationFishingCatchResolver.EarlyCatchCacheLimitForTesting;
                for (var index = 0; index <= earlyLimit; index++)
                {
                    InformationFishingCatchResolver.StoreEarlyCatchCacheForTesting(
                        "early:" + index.ToString(CultureInfo.InvariantCulture),
                        CreateSingleFishingCatchCandidate(index + 1, "Early Fish " + index.ToString(CultureInfo.InvariantCulture)),
                        "early message " + index.ToString(CultureInfo.InvariantCulture));
                }

                IList<FishingCatchCandidate> candidates;
                string message;
                if (InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:0", out candidates, out message))
                {
                    throw new InvalidOperationException("Expected early fishing catch cache to evict the oldest entry after exceeding its limit.");
                }

                if (!InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:" + earlyLimit.ToString(CultureInfo.InvariantCulture), out candidates, out message) ||
                    candidates.Count != 1 ||
                    candidates[0].Id != earlyLimit + 1 ||
                    !string.Equals(message, "early message " + earlyLimit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected newest early fishing catch cache entry to survive limit eviction.");
                }

                var queryLimit = InformationFishingCatchResolver.CatchCacheLimitForTesting;
                for (var index = 0; index <= queryLimit; index++)
                {
                    InformationFishingCatchResolver.StoreCatchCacheForTesting(
                        "query:" + index.ToString(CultureInfo.InvariantCulture),
                        CreateSingleFishingCatchCandidate(index + 100, "Query Fish " + index.ToString(CultureInfo.InvariantCulture)),
                        "query message " + index.ToString(CultureInfo.InvariantCulture));
                }

                if (InformationFishingCatchResolver.TryGetCatchCacheForTesting("query:0", out candidates, out message))
                {
                    throw new InvalidOperationException("Expected full fishing catch query cache to evict the oldest entry after exceeding its limit.");
                }

                if (!InformationFishingCatchResolver.TryGetCatchCacheForTesting("query:" + queryLimit.ToString(CultureInfo.InvariantCulture), out candidates, out message) ||
                    candidates.Count != 1 ||
                    candidates[0].Id != queryLimit + 100 ||
                    !string.Equals(message, "query message " + queryLimit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected newest full fishing catch query cache entry to survive limit eviction.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
            }
        }

        private static void InformationFishingCatchQueryCacheHitBackfillsEarlyCache()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                var candidates = CreateSingleFishingCatchCandidate(501, "Backfilled Fish");
                InformationFishingCatchResolver.StoreCatchCacheForTesting("query:backfill", candidates, "from query cache");

                IList<FishingCatchCandidate> queryCandidates;
                string message;
                if (!InformationFishingCatchResolver.TryGetCatchCacheAndBackfillEarlyForTesting(
                    "query:backfill",
                    "early:backfill",
                    out queryCandidates,
                    out message) ||
                    !object.ReferenceEquals(candidates, queryCandidates) ||
                    !string.Equals(message, "from query cache", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected full fishing catch query cache hit to return the stored entry before backfilling early cache.");
                }

                IList<FishingCatchCandidate> earlyCandidates;
                if (!InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:backfill", out earlyCandidates, out message) ||
                    !object.ReferenceEquals(candidates, earlyCandidates) ||
                    !string.Equals(message, "from query cache", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected full fishing catch query cache hit to backfill early fishing catch cache with the same entry semantics.");
                }

                if (InformationFishingCatchResolver.EarlyCacheHitCount != 1 ||
                    InformationFishingCatchResolver.EarlyCacheMissCount != 0)
                {
                    throw new InvalidOperationException("Expected query-cache backfill verification to keep early cache diagnostics to one hit and no miss.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
            }
        }

        private static void InformationFishingCatchResetClearsCachesAndCounters()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                var candidates = CreateSingleFishingCatchCandidate(601, "Reset Fish");
                InformationFishingCatchResolver.StoreEarlyCatchCacheForTesting("early:reset", candidates, "early reset");
                InformationFishingCatchResolver.StoreCatchCacheForTesting("query:reset", candidates, "query reset");

                IList<FishingCatchCandidate> cached;
                string message;
                if (!InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:reset", out cached, out message))
                {
                    throw new InvalidOperationException("Expected early fishing catch cache setup to hit before reset.");
                }

                if (InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:missing", out cached, out message))
                {
                    throw new InvalidOperationException("Expected missing early fishing catch cache setup probe to miss before reset.");
                }

                if (InformationFishingCatchResolver.EarlyCacheHitCount != 1 ||
                    InformationFishingCatchResolver.EarlyCacheMissCount != 1)
                {
                    throw new InvalidOperationException("Expected early fishing catch cache diagnostics setup to record one hit and one miss before reset.");
                }

                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                if (InformationFishingCatchResolver.EarlyCacheHitCount != 0 ||
                    InformationFishingCatchResolver.EarlyCacheMissCount != 0 ||
                    InformationFishingCatchResolver.WaterScanCount != 0 ||
                    InformationFishingCatchResolver.ConditionsReadCount != 0)
                {
                    throw new InvalidOperationException("Expected fishing catch cache reset to clear all cache diagnostics counters.");
                }

                if (InformationFishingCatchResolver.TryGetCatchCacheForTesting("query:reset", out cached, out message))
                {
                    throw new InvalidOperationException("Expected fishing catch cache reset to clear full query cache entries.");
                }

                if (InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:reset", out cached, out message))
                {
                    throw new InvalidOperationException("Expected fishing catch cache reset to clear early cache entries.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
            }
        }

        private static void InformationFishingWaterPenaltyKeepsSourceFormula()
        {
            TestInformationFishingQueryMain.Reset();
            var player = new TestInformationFishingEnvironmentPlayer();
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain),
                LocalPlayer = player
            };

            var enough = InformationFishingCatchResolver.ApplyFishingWaterPenaltyForTesting(context, 5120f, 300, 0, 50);
            if (enough.WaterNeeded != 300 ||
                enough.FishingLevel != 50 ||
                enough.JunkPossible)
            {
                throw new InvalidOperationException("Expected sufficient fishing water to keep waterNeeded 300, fishing level, and junk flag stable.");
            }

            var shortPositiveLuck = InformationFishingCatchResolver.ApplyFishingWaterPenaltyForTesting(context, 5120f, 250, 0, 90);
            if (shortPositiveLuck.WaterNeeded != 300 ||
                shortPositiveLuck.FishingLevel != 75 ||
                shortPositiveLuck.JunkPossible)
            {
                throw new InvalidOperationException("Expected water shortage to scale fishing level without enabling junk when luckLevel stays high.");
            }

            player.luck = -0.5d;
            var shortNegativeLuck = InformationFishingCatchResolver.ApplyFishingWaterPenaltyForTesting(context, 5120f, 250, 0, 90);
            if (shortNegativeLuck.WaterNeeded != 300 ||
                shortNegativeLuck.FishingLevel != 75 ||
                !shortNegativeLuck.JunkPossible)
            {
                throw new InvalidOperationException("Expected negative luck to use luckLevel for the water-shortage junk check.");
            }

            player.luck = 0d;
            var chummed = InformationFishingCatchResolver.ApplyFishingWaterPenaltyForTesting(context, 5120f, 300, 3, 50);
            if (chummed.WaterNeeded != 300 ||
                chummed.FishingLevel != 70 ||
                chummed.JunkPossible)
            {
                throw new InvalidOperationException("Expected chum bonuses to keep the existing +11/+6/+3 fishing level behavior.");
            }
        }

        private static void InformationFishingLiquidKindKeepsPriority()
        {
            if (!string.Equals(InformationFishingCatchResolver.ResolveLiquidKindForTesting(false, false), "water", StringComparison.Ordinal) ||
                !string.Equals(InformationFishingCatchResolver.ResolveLiquidKindForTesting(false, true), "honey", StringComparison.Ordinal) ||
                !string.Equals(InformationFishingCatchResolver.ResolveLiquidKindForTesting(true, false), "lava", StringComparison.Ordinal) ||
                !string.Equals(InformationFishingCatchResolver.ResolveLiquidKindForTesting(true, true), "lava", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fishing liquid kind priority to remain lava, then honey, then water.");
            }
        }

        private static void InformationFishingLavaCapabilityReadsEnvironment()
        {
            var player = new TestInformationFishingEnvironmentPlayer();
            var context = new InformationWorldContext
            {
                LocalPlayer = player
            };
            var conditions = new TestInformationFishingConditions();

            if (InformationFishingCatchResolver.CanFishInLavaForTesting(context, conditions))
            {
                throw new InvalidOperationException("Expected lava fishing capability to stay false without player accessory, lava pole, or lava bait.");
            }

            player.accLavaFishing = true;
            if (!InformationFishingCatchResolver.CanFishInLavaForTesting(context, conditions))
            {
                throw new InvalidOperationException("Expected lava fishing capability to read the player's accLavaFishing flag.");
            }
        }

        private static void InformationFishingWaterScanIncrementsOncePerScan()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                TestInformationFishingQueryMain.Reset();
                var context = new InformationWorldContext
                {
                    MainType = typeof(TestInformationFishingQueryMain)
                };

                var water = InformationFishingCatchResolver.ScanFishingWaterForTesting(context, 80, 120);
                if (water.TotalTiles != 0 ||
                    water.InLava ||
                    water.InHoney)
                {
                    throw new InvalidOperationException("Expected missing tile collection to return an empty fishing water scan.");
                }

                if (InformationFishingCatchResolver.WaterScanCount != 1 ||
                    InformationFishingCatchResolver.ConditionsReadCount != 0)
                {
                    throw new InvalidOperationException("Expected a real water scan entry to increment WaterScanCount once without reading fishing conditions.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                TestInformationFishingQueryMain.Reset();
            }
        }

        private static void InformationFishingConditionHeightRollsKeepOrder()
        {
            TestInformationFishingQueryMain.Reset();
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain)
            };
            var levels = new int[2];

            AssertHeightLevels(context, 100, levels, new[] { 0 }, "non-remix sky");
            AssertHeightLevels(context, 200, levels, new[] { 1 }, "non-remix surface");
            AssertHeightLevels(context, 400, levels, new[] { 2 }, "non-remix underground");
            AssertHeightLevels(context, 700, levels, new[] { 3 }, "non-remix cavern");
            AssertHeightLevels(context, 950, levels, new[] { 4 }, "non-remix underworld");

            TestInformationFishingQueryMain.remixWorld = true;
            AssertHeightLevels(context, 100, levels, new[] { 0 }, "remix sky");
            AssertHeightLevels(context, 200, levels, new[] { 1 }, "remix surface");
            AssertHeightLevels(context, 400, levels, new[] { 3 }, "remix underground");
            AssertHeightLevels(context, 700, levels, new[] { 2, 1 }, "remix cavern adds surface");
            AssertHeightLevels(context, 950, levels, new[] { 4 }, "remix underworld");
        }

        private static void InformationFishingConditionCorruptionRollsKeepOrder()
        {
            TestInformationFishingQueryMain.Reset();
            var player = new TestInformationFishingEnvironmentPlayer
            {
                ZoneCorrupt = true,
                ZoneCrimson = true
            };
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain),
                LocalPlayer = player
            };
            var rolls = new CorruptionRoll[2];
            var count = InformationFishingConditionRolls.BuildCorruptionRolls(context, 2, rolls);
            if (count != 2 ||
                !rolls[0].Corrupt ||
                rolls[0].Crimson ||
                rolls[1].Corrupt ||
                !rolls[1].Crimson)
            {
                throw new InvalidOperationException("Expected dual biome corruption rolls to stay corrupt-first then crimson.");
            }

            TestInformationFishingQueryMain.remixWorld = true;
            count = InformationFishingConditionRolls.BuildCorruptionRolls(context, 0, rolls);
            if (count != 1 || rolls[0].Corrupt || rolls[0].Crimson)
            {
                throw new InvalidOperationException("Expected remix sky fishing roll to disable corruption/crimson flags.");
            }
        }

        private static void InformationFishingConditionBooleanRollsKeepOrder()
        {
            TestInformationFishingQueryMain.Reset();
            TestInformationFishingQueryMain.notTheBeesWorld = true;
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain)
            };

            AssertBoolRolls(InformationFishingConditionRolls.BuildHoneyRolls(context, true), new[] { true, false }, "notTheBees honey");
            AssertBoolRolls(InformationFishingConditionRolls.BuildHoneyRolls(context, false), new[] { false }, "dry honey");
            AssertBoolRolls(InformationFishingConditionRolls.BuildBooleanRolls(true), new[] { false, true }, "enabled boolean");
            AssertBoolRolls(InformationFishingConditionRolls.BuildBooleanRolls(false), new[] { false }, "disabled boolean");
        }

        private static void InformationFishingConditionEnumerationKeepsOrderAndStops()
        {
            TestInformationFishingQueryMain.Reset();
            TestInformationFishingQueryMain.notTheBeesWorld = true;
            var player = new TestInformationFishingEnvironmentPlayer
            {
                ZoneCorrupt = true,
                ZoneCrimson = true
            };
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain),
                LocalPlayer = player
            };
            var summaries = new List<string>();
            InformationFishingConditionRolls.ForEachRoll(
                context,
                400,
                true,
                true,
                true,
                true,
                true,
                true,
                delegate(FishingConditionRoll roll)
                {
                    summaries.Add(SummarizeFishingConditionRoll(roll));
                    return summaries.Count < 5;
                });

            if (summaries.Count != 5)
            {
                throw new InvalidOperationException("Expected condition roll callback stop to prevent further enumeration.");
            }

            AssertStringEquals(summaries[0], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=0|crate=0|junk=0", "roll 0");
            AssertStringEquals(summaries[1], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=0|crate=0|junk=1", "roll 1");
            AssertStringEquals(summaries[2], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=0|crate=1|junk=0", "roll 2");
            AssertStringEquals(summaries[3], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=0|crate=1|junk=1", "roll 3");
            AssertStringEquals(summaries[4], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=1|crate=0|junk=0", "roll 4");
        }

        private static void InformationFishingConditionJunkDisabledStaysFalse()
        {
            TestInformationFishingQueryMain.Reset();
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain),
                LocalPlayer = new TestInformationFishingEnvironmentPlayer()
            };
            var count = 0;
            InformationFishingConditionRolls.ForEachRoll(
                context,
                400,
                false,
                false,
                false,
                false,
                false,
                false,
                delegate(FishingConditionRoll roll)
                {
                    count++;
                    if (roll.Junk)
                    {
                        throw new InvalidOperationException("Expected junk=false roll space not to include junk=true.");
                    }

                    return true;
                });

            if (count != 2)
            {
                throw new InvalidOperationException("Expected junk-disabled baseline to enumerate only crate false/true, got " + count.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static void InformationFishingContextFactoryMapsAttemptSpec()
        {
            InformationFishingContextFactory.ResetTypeCacheForTesting();
            var player = new TestInformationFishingEnvironmentPlayer();
            var fishingConditions = new TestInformationFishingConditions
            {
                PoleItemType = TestFishingRod,
                BaitItemType = 267
            };
            var spec = new FishingAttemptSpec
            {
                Context = new InformationWorldContext
                {
                    MainType = typeof(Terraria.Main),
                    LocalPlayer = player
                },
                FishingConditions = fishingConditions,
                TileX = 12,
                TileY = 34,
                InLava = true,
                InHoney = true,
                WaterTilesCount = 211,
                WaterNeeded = 300,
                Chums = 2,
                FishingLevel = 67,
                CanFishInLava = true,
                QuestFish = 2454,
                Roll = new FishingConditionRoll
                {
                    HeightLevel = 3,
                    Corrupt = true,
                    Crimson = false,
                    Jungle = true,
                    InHoney = true,
                    Snow = true,
                    Desert = false,
                    InfectedDesert = true,
                    RemixOcean = true,
                    Crate = true,
                    Junk = false
                }
            };

            var fishingContext = InformationFishingContextFactory.CreateContext(spec);
            if (fishingContext == null)
            {
                throw new InvalidOperationException("Expected fishing context factory to create a context when Terraria fishing stub types are available.");
            }

            AssertMemberReference(fishingContext, "Player", player, "context player");
            AssertMemberBool(fishingContext, "RolledCorruption", true, "rolled corruption");
            AssertMemberBool(fishingContext, "RolledCrimson", false, "rolled crimson");
            AssertMemberBool(fishingContext, "RolledJungle", true, "rolled jungle");
            AssertMemberBool(fishingContext, "RolledSnow", true, "rolled snow");
            AssertMemberBool(fishingContext, "RolledDesert", false, "rolled desert");
            AssertMemberBool(fishingContext, "RolledInfectedDesert", true, "rolled infected desert");
            AssertMemberBool(fishingContext, "RolledRemixOcean", true, "rolled remix ocean");

            var attempt = InformationReflection.GetMember(fishingContext, "Fisher");
            if (attempt == null)
            {
                throw new InvalidOperationException("Expected fishing context factory to attach a FishingAttempt instance.");
            }

            AssertMemberReference(attempt, "playerFishingConditions", fishingConditions, "attempt fishing conditions");
            AssertMemberInt(attempt, "X", 12, "attempt X");
            AssertMemberInt(attempt, "Y", 34, "attempt Y");
            AssertMemberInt(attempt, "bobberType", 0, "attempt bobber type");
            AssertMemberBool(attempt, "common", false, "attempt common");
            AssertMemberBool(attempt, "uncommon", false, "attempt uncommon");
            AssertMemberBool(attempt, "rare", false, "attempt rare");
            AssertMemberBool(attempt, "veryrare", false, "attempt very rare");
            AssertMemberBool(attempt, "legendary", false, "attempt legendary");
            AssertMemberBool(attempt, "crate", true, "attempt crate");
            AssertMemberBool(attempt, "junk", false, "attempt junk");
            AssertMemberBool(attempt, "inLava", true, "attempt lava");
            AssertMemberBool(attempt, "inHoney", true, "attempt honey");
            AssertMemberInt(attempt, "waterTilesCount", 211, "attempt water tiles");
            AssertMemberInt(attempt, "waterNeededToFish", 300, "attempt water needed");
            AssertMemberFloat(attempt, "waterQuality", 0f, "attempt water quality");
            AssertMemberInt(attempt, "chumsInWater", 2, "attempt chums");
            AssertMemberInt(attempt, "fishingLevel", 67, "attempt fishing level");
            AssertMemberBool(attempt, "CanFishInLava", true, "attempt lava capability");
            AssertMemberFloat(attempt, "atmo", 0f, "attempt atmo");
            AssertMemberInt(attempt, "questFish", 2454, "attempt quest fish");
            AssertMemberInt(attempt, "heightLevel", 3, "attempt height level");
            AssertMemberInt(attempt, "rolledItemDrop", 0, "attempt rolled item");
            AssertMemberInt(attempt, "rolledEnemySpawn", 0, "attempt rolled enemy");
        }

        private static void InformationFishRuleEvaluatorKeepsOrderAndDeduplicates()
        {
            InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
            var rules = new ArrayList
            {
                new TestInformationFishDropRule(new[] { 5, 3, 5 }),
                new TestInformationFishDropRule(new[] { 3, 7 }),
                new TestInformationFishDropRule(new[] { 9 }) { Allow = false },
                new TestInformationFishDropRule(new[] { 11 }) { IsStopper = true },
                new TestInformationFishDropRule(new[] { 13 })
            };
            var itemIds = new List<int>();
            var seen = new HashSet<int>();

            InformationFishDropRuleEvaluator.AddMatchingRuleItems(rules, new object(), itemIds, seen);

            AssertIntSequence(itemIds, new[] { 5, 3, 7, 11 }, "fish rule order and duplicate handling");
        }

        private static void InformationFishRuleEvaluatorRespectsMaxCatchItems()
        {
            InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
            var possibleItems = new int[InformationFishDropRuleEvaluator.MaxCatchItems + 10];
            for (var index = 0; index < possibleItems.Length; index++)
            {
                possibleItems[index] = index + 1;
            }

            var rules = new ArrayList
            {
                new TestInformationFishDropRule(possibleItems),
                new TestInformationFishDropRule(new[] { 9999 })
            };
            var itemIds = new List<int>();
            var seen = new HashSet<int>();

            InformationFishDropRuleEvaluator.AddMatchingRuleItems(rules, new object(), itemIds, seen);

            if (itemIds.Count != InformationFishDropRuleEvaluator.MaxCatchItems)
            {
                throw new InvalidOperationException("Expected fish rule evaluator to stop at MaxCatchItems.");
            }

            for (var index = 0; index < itemIds.Count; index++)
            {
                var expected = index + 1;
                if (itemIds[index] != expected)
                {
                    throw new InvalidOperationException("Expected bounded fish rule item " + index.ToString(CultureInfo.InvariantCulture) + " to be " + expected.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static void InformationFishRuleEvaluatorCachesMeetsConditionsLookup()
        {
            InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
            var rules = new ArrayList
            {
                new TestInformationFishDropRule(new[] { 1 }),
                new TestInformationFishDropRule(new[] { 2 })
            };
            var itemIds = new List<int>();
            var seen = new HashSet<int>();

            InformationFishDropRuleEvaluator.AddMatchingRuleItems(rules, new object(), itemIds, seen);
            if (InformationFishDropRuleEvaluator.MeetsConditionsMethodCacheCountForTesting != 1)
            {
                throw new InvalidOperationException("Expected fish rule evaluator to cache one MeetsConditions lookup for rules of the same type.");
            }

            InformationFishDropRuleEvaluator.AddMatchingRuleItems(rules, new object(), itemIds, seen);
            if (InformationFishDropRuleEvaluator.MeetsConditionsMethodCacheCountForTesting != 1)
            {
                throw new InvalidOperationException("Expected fish rule evaluator cache hits not to add duplicate MethodInfo entries.");
            }
        }

        private static void InformationFishingGlobalEmptyQueryKeepsHeavyCountersIdle()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                bool truncated;
                string message;
                var candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(null, "   ", 10, out truncated, out message);
                if (candidates.Count != 0 ||
                    truncated ||
                    !string.Equals(message, "请输入名称或 ID 搜索全游戏可钓鱼获", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected empty global fishing search query to return the stable prompt without candidates.");
                }

                if (InformationFishingCatchResolver.EarlyCacheHitCount != 0 ||
                    InformationFishingCatchResolver.EarlyCacheMissCount != 0 ||
                    InformationFishingCatchResolver.WaterScanCount != 0 ||
                    InformationFishingCatchResolver.ConditionsReadCount != 0)
                {
                    throw new InvalidOperationException("Expected empty global fishing search query to avoid catch cache, water scan, and conditions read counters.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
            }
        }

        private static void InformationFishingSearchHelpersKeepStableSemantics()
        {
            int itemId;
            if (!InformationFishingCatchResolver.TryParseSearchItemIdForTesting("  #701  ", out itemId) || itemId != 701)
            {
                throw new InvalidOperationException("Expected global fishing search to parse #item id queries.");
            }

            if (!InformationFishingCatchResolver.TryParseSearchItemIdForTesting("702", out itemId) || itemId != 702)
            {
                throw new InvalidOperationException("Expected global fishing search to parse numeric item id queries.");
            }

            if (InformationFishingCatchResolver.TryParseSearchItemIdForTesting("0", out itemId) ||
                InformationFishingCatchResolver.TryParseSearchItemIdForTesting("70x", out itemId))
            {
                throw new InvalidOperationException("Expected invalid global fishing search id queries to fall back to text matching.");
            }

            if (!InformationFishingCatchResolver.MatchesGlobalSearchForTesting(701, "Crate Fish", "WoodenCrate", "#701", true, 701))
            {
                throw new InvalidOperationException("Expected global fishing search to match parsed item ids.");
            }

            if (!InformationFishingCatchResolver.MatchesGlobalSearchForTesting(702, "Quest Fish", string.Empty, "quest", false, 0))
            {
                throw new InvalidOperationException("Expected global fishing search to match display names with current culture ignore case.");
            }

            if (!InformationFishingCatchResolver.MatchesGlobalSearchForTesting(703, string.Empty, "HardmodeCrateFish", "hardmodecrate", false, 0))
            {
                throw new InvalidOperationException("Expected global fishing search to match internal names ordinal-ignore-case.");
            }

            if (InformationFishingCatchResolver.MatchesGlobalSearchForTesting(704, "Other Fish", "OtherInternal", "missing", false, 0))
            {
                throw new InvalidOperationException("Expected unrelated global fishing search text not to match.");
            }
        }

        private static void InformationFishingItemNameResolverKeepsCacheBoundaries()
        {
            WithGlobalFishingSearchFixture(context =>
            {
                Terraria.Lang.ItemNames[700] = "First Bass";
                var firstName = InformationFishingCatchResolver.ResolveFishingItemNameForTesting(700);
                Terraria.Lang.ItemNames[700] = "Second Bass";
                var secondName = InformationFishingCatchResolver.ResolveFishingItemNameForTesting(700);
                if (!string.Equals(firstName, "First Bass", StringComparison.Ordinal) ||
                    !string.Equals(secondName, "Second Bass", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected fishing display names to follow current language data without a permanent display-name cache.");
                }

                var internalName = InformationFishingCatchResolver.ResolveFishingItemInternalNameForTesting(702);
                Terraria.ID.ItemID.Search.SetName(702, "ChangedInternalFish");
                var cachedInternalName = InformationFishingCatchResolver.ResolveFishingItemInternalNameForTesting(702);
                if (!string.Equals(internalName, "QuestInternalFish", StringComparison.Ordinal) ||
                    !string.Equals(cachedInternalName, "QuestInternalFish", StringComparison.Ordinal) ||
                    InformationFishingCatchResolver.FishingItemInternalNameCacheCountForTesting != 1)
                {
                    throw new InvalidOperationException("Expected fishing internal names to use the stable internal-name cache.");
                }

                if (!InformationFishingCatchResolver.IsFishingCrateItemForTesting(701) ||
                    !InformationFishingCatchResolver.IsFishingCrateItemForTesting(703) ||
                    InformationFishingCatchResolver.IsFishingCrateItemForTesting(700))
                {
                    throw new InvalidOperationException("Expected fishing crate item markers to read normal and hardmode crate sets.");
                }

                var questIds = InformationFishingCatchResolver.ReadAnglerQuestFishIdsForTesting(context);
                if (!questIds.Contains(702) || !questIds.Contains(704) || questIds.Contains(700))
                {
                    throw new InvalidOperationException("Expected fishing quest fish ids to come from Main.anglerQuestItemNetIDs.");
                }
            });
        }

        private static void InformationFishingGlobalSearchKeepsResultSemantics()
        {
            WithGlobalFishingSearchFixture(context =>
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();

                bool truncated;
                string message;
                var candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "fish", 2, out truncated, out message);
                if (candidates.Count != 2 ||
                    !truncated ||
                    !string.Equals(message, "结果较多，请继续输入缩小范围", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected global fishing search to honor maxResults and truncated message.");
                }

                if (InformationFishingCatchResolver.WaterScanCount != 0 ||
                    InformationFishingCatchResolver.ConditionsReadCount != 0)
                {
                    throw new InvalidOperationException("Expected global fishing search not to scan water or read FishingConditions.");
                }

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "#701", 10, out truncated, out message);
                AssertSingleGlobalFishingCandidate(candidates, 701, "Crate Fish", true, false, "id query crate result");
                if (truncated || !string.Equals(message, string.Empty, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected exact id global fishing search to avoid truncated and extra messages.");
                }

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "questinternal", 10, out truncated, out message);
                AssertSingleGlobalFishingCandidate(candidates, 702, "Quest Fish", false, true, "internal name quest result");

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "hardmodeinternal", 10, out truncated, out message);
                AssertSingleGlobalFishingCandidate(candidates, 703, "Hardmode Crate Fish", true, false, "hardmode crate result");

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "zombiemerman", 10, out truncated, out message);
                AssertSingleGlobalFishingEnemyCandidate(candidates, Terraria.ID.NPCID.ZombieMerman, "global fishable enemy result");

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(null, "fish", 10, out truncated, out message);
                if (candidates.Count != 0 ||
                    truncated ||
                    !string.Equals(message, "环境不可用", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected global fishing search to keep the unavailable environment message.");
                }

                var oldRules = Terraria.Main.FishDropsDB;
                Terraria.Main.FishDropsDB = null;
                InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "fish", 10, out truncated, out message);
                Terraria.Main.FishDropsDB = oldRules;
                InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
                if (candidates.Count != 0 ||
                    truncated ||
                    !string.Equals(message, "鱼获规则不可用", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected global fishing search to keep the unavailable rules message.");
                }

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "does-not-exist", 10, out truncated, out message);
                if (candidates.Count != 0 ||
                    truncated ||
                    !string.Equals(message, "无匹配鱼获", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected global fishing search to keep the no-match message.");
                }
            });
        }

        private static void AssertSingleGlobalFishingCandidate(
            IList<FishingCatchCandidate> candidates,
            int expectedId,
            string expectedName,
            bool expectedCrate,
            bool expectedQuestFish,
            string label)
        {
            if (candidates == null || candidates.Count != 1)
            {
                throw new InvalidOperationException("Expected one " + label + ".");
            }

            var candidate = candidates[0];
            if (candidate.Id != expectedId ||
                !string.Equals(candidate.DisplayName, expectedName, StringComparison.Ordinal) ||
                candidate.IsCrate != expectedCrate ||
                candidate.IsQuestFish != expectedQuestFish ||
                candidate.IsEnemy)
            {
                throw new InvalidOperationException("Unexpected " + label + ".");
            }
        }

        private static void AssertSingleGlobalFishingEnemyCandidate(
            IList<FishingCatchCandidate> candidates,
            int expectedId,
            string label)
        {
            if (candidates == null || candidates.Count != 1)
            {
                throw new InvalidOperationException("Expected one " + label + ".");
            }

            var candidate = candidates[0];
            if (candidate.Id != expectedId ||
                !string.Equals(candidate.Kind, FishingCatchKinds.NPC, StringComparison.OrdinalIgnoreCase) ||
                !candidate.IsEnemy ||
                candidate.IsCrate ||
                candidate.IsQuestFish)
            {
                throw new InvalidOperationException("Unexpected " + label + ".");
            }
        }

        private static void WithGlobalFishingSearchFixture(Action<InformationWorldContext> test)
        {
            var oldRules = Terraria.Main.FishDropsDB;
            var oldQuestIds = Terraria.Main.anglerQuestItemNetIDs;
            var oldQuestIndex = Terraria.Main.anglerQuest;
            var oldQuestFinished = Terraria.Main.anglerQuestFinished;
            try
            {
                InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
                InformationFishingCatchResolver.ResetFishingItemNameResolverForTesting();
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                InformationFishingEnemyCandidateResolver.ResetForTesting();
                Terraria.Lang.ItemNames.Clear();
                Terraria.Lang.NpcNames.Clear();
                Terraria.ID.ItemID.Search.Clear();
                Terraria.ID.NPCID.Search.Clear();
                Array.Clear(Terraria.ID.ItemID.Sets.IsFishingCrate, 0, Terraria.ID.ItemID.Sets.IsFishingCrate.Length);
                Array.Clear(Terraria.ID.ItemID.Sets.IsFishingCrateHardmode, 0, Terraria.ID.ItemID.Sets.IsFishingCrateHardmode.Length);

                Terraria.Main.FishDropsDB = new TestInformationFishDropsDb(new ArrayList
                {
                    new TestInformationFishDropRule(new[] { 700, 701, 702, 703 })
                });
                Terraria.Main.anglerQuestItemNetIDs = new[] { 702, 704 };
                Terraria.Main.anglerQuest = 0;
                Terraria.Main.anglerQuestFinished = false;

                Terraria.Lang.ItemNames[700] = "Bass Fish";
                Terraria.Lang.ItemNames[701] = "Crate Fish";
                Terraria.Lang.ItemNames[702] = "Quest Fish";
                Terraria.Lang.ItemNames[703] = "Hardmode Crate Fish";
                Terraria.Lang.ItemNames[704] = "Quest Bonus Fish";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.ZombieMerman] = "Zombie Merman";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.EyeballFlyingFish] = "Eyeball Flying Fish";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.GoblinShark] = "Goblin Shark";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.BloodEelHead] = "Blood Eel";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.BloodNautilus] = "Dreadnautilus";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.TownSlimeRed] = "Surly Slime";
                Terraria.ID.ItemID.Search.SetName(700, "BassInternalFish");
                Terraria.ID.ItemID.Search.SetName(701, "CrateInternalFish");
                Terraria.ID.ItemID.Search.SetName(702, "QuestInternalFish");
                Terraria.ID.ItemID.Search.SetName(703, "HardmodeInternalFish");
                Terraria.ID.ItemID.Search.SetName(704, "QuestBonusInternalFish");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.ZombieMerman, "ZombieMerman");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.EyeballFlyingFish, "EyeballFlyingFish");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.GoblinShark, "GoblinShark");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.BloodEelHead, "BloodEelHead");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.BloodNautilus, "BloodNautilus");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.TownSlimeRed, "TownSlimeRed");
                Terraria.ID.ItemID.Sets.IsFishingCrate[701] = true;
                Terraria.ID.ItemID.Sets.IsFishingCrateHardmode[703] = true;

                test(new InformationWorldContext
                {
                    MainType = typeof(Terraria.Main)
                });
            }
            finally
            {
                Terraria.Main.FishDropsDB = oldRules;
                Terraria.Main.anglerQuestItemNetIDs = oldQuestIds;
                Terraria.Main.anglerQuest = oldQuestIndex;
                Terraria.Main.anglerQuestFinished = oldQuestFinished;
                Terraria.Lang.ItemNames.Clear();
                Terraria.Lang.NpcNames.Clear();
                Terraria.ID.ItemID.Search.Clear();
                Terraria.ID.NPCID.Search.Clear();
                Array.Clear(Terraria.ID.ItemID.Sets.IsFishingCrate, 0, Terraria.ID.ItemID.Sets.IsFishingCrate.Length);
                Array.Clear(Terraria.ID.ItemID.Sets.IsFishingCrateHardmode, 0, Terraria.ID.ItemID.Sets.IsFishingCrateHardmode.Length);
                InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
                InformationFishingCatchResolver.ResetFishingItemNameResolverForTesting();
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                InformationFishingEnemyCandidateResolver.ResetForTesting();
            }
        }

        private static void InformationFishingDiagnosticsSnapshotKeepsStableFieldMapping()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            InformationOverlayService.ResetFishingBobberLookupDiagnosticsForTesting();
            try
            {
                InformationFishingCatchDiagnostics.IncrementEarlyCacheHit();
                InformationFishingCatchDiagnostics.IncrementEarlyCacheHit();
                InformationFishingCatchDiagnostics.IncrementEarlyCacheMiss();
                InformationFishingCatchDiagnostics.IncrementWaterScan();
                InformationFishingCatchDiagnostics.IncrementWaterScan();
                InformationFishingCatchDiagnostics.IncrementWaterScan();
                InformationFishingCatchDiagnostics.IncrementConditionsRead();
                InformationFishingCatchDiagnostics.IncrementConditionsRead();
                InformationFishingCatchDiagnostics.IncrementConditionsRead();
                InformationFishingCatchDiagnostics.IncrementConditionsRead();

                var snapshot = InformationFishingCatchDiagnostics.ReadSnapshot();
                if (snapshot.EarlyCacheHitCount != 2 ||
                    snapshot.EarlyCacheMissCount != 1 ||
                    snapshot.WaterScanCount != 3 ||
                    snapshot.ConditionsReadCount != 4)
                {
                    throw new InvalidOperationException("Expected fishing catch diagnostics snapshot to expose existing counters without recalculating them.");
                }

                var diagnostics = InformationOverlayService.GetDiagnostics();
                if (diagnostics.FishingCatchEarlyCacheHitCount != 2 ||
                    diagnostics.FishingCatchEarlyCacheMissCount != 1 ||
                    diagnostics.FishingWaterScanCount != 3 ||
                    diagnostics.FishingConditionsReadCount != 4 ||
                    diagnostics.FishingProjectileFallbackScanCount != 0)
                {
                    throw new InvalidOperationException("Expected information diagnostics to map fishing catch counters without merging projectile fallback diagnostics.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                InformationOverlayService.ResetFishingBobberLookupDiagnosticsForTesting();
            }
        }

        private static void InformationFishingBobberFreshInactiveSkipsProjectileFallback()
        {
            InformationOverlayService.ResetFishingBobberLookupDiagnosticsForTesting();
            FishingBobberObserver.RemoveMissing(null);
            FishingBobberObserver.MarkNoActiveObservation(200);
            Terraria.Main.myPlayer = 0;
            Terraria.Main.projectile = new object[]
            {
                new TestInformationProjectile
                {
                    active = true,
                    bobber = true,
                    owner = 0,
                    identity = 91,
                    Center = new Terraria.TestVector2 { X = 320f, Y = 480f }
                }
            };

            var context = new InformationWorldContext
            {
                MainType = typeof(Terraria.Main),
                GameUpdateCount = 201
            };
            float x;
            float y;
            if (InformationOverlayService.TryFindLocalBobberForTesting(context, out x, out y))
            {
                throw new InvalidOperationException("Expected fresh inactive observer state to skip projectile fallback.");
            }

            var diagnostics = InformationOverlayService.GetDiagnostics();
            if (diagnostics.FishingBobberObserverFreshInactiveSkipCount != 1 ||
                diagnostics.FishingProjectileFallbackScanCount != 0)
            {
                throw new InvalidOperationException("Expected fresh inactive observer diagnostics to record a skip without projectile fallback.");
            }

            InformationOverlayService.ResetFishingBobberLookupDiagnosticsForTesting();
            FishingBobberObserver.RemoveMissing(null);
            FishingBobberObserver.MarkNoActiveObservation(200);
            context.GameUpdateCount = 204;
            if (!InformationOverlayService.TryFindLocalBobberForTesting(context, out x, out y) ||
                Math.Abs(x - 320f) > 0.01f ||
                Math.Abs(y - 480f) > 0.01f)
            {
                throw new InvalidOperationException("Expected stale inactive observer state to fall back to projectile scanning.");
            }

            diagnostics = InformationOverlayService.GetDiagnostics();
            if (diagnostics.FishingBobberObserverFreshInactiveSkipCount != 0 ||
                diagnostics.FishingProjectileFallbackScanCount != 1)
            {
                throw new InvalidOperationException("Expected stale inactive observer diagnostics to record projectile fallback.");
            }
        }

        private static InformationWorldContext CreateInformationTileHighlightContext(float screenX, float screenY, int screenWidth, int screenHeight, float playerCenterX, float playerCenterY, string worldKey, string worldRecordKey)
        {
            return new InformationWorldContext
            {
                ScreenX = screenX,
                ScreenY = screenY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                PlayerCenterX = playerCenterX,
                PlayerCenterY = playerCenterY,
                WorldKey = worldKey,
                WorldRecordKey = worldRecordKey
            };
        }

        private static void InformationChestLabelsCacheSignatureChangesWithModeAndKnownKeys()
        {
            WithTemporaryBehaviorStore(() =>
            {
                var context = CreateInformationChestRecordContext("player-a", "world-a", "same-world#42");
                var settings = AppSettings.CreateDefault();

                bool added;
                string message;
                if (!PlayerWorldBehaviorStore.TryRecordOpenedChest(
                        new PlayerWorldBehaviorContext { PlayerKey = context.PlayerRecordKey, WorldKey = context.WorldRecordKey },
                        10,
                        20,
                        "test",
                        out added,
                        out message) ||
                    !added)
                {
                    throw new InvalidOperationException("Expected first opened chest record to be stored: " + message);
                }

                var opened = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(context, settings, "Opened");
                var always = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(context, settings, "Always");
                if (string.Equals(opened, always, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected chest label cache signature to include display mode.");
                }

                if (!PlayerWorldBehaviorStore.TryRecordOpenedChest(
                        new PlayerWorldBehaviorContext { PlayerKey = context.PlayerRecordKey, WorldKey = context.WorldRecordKey },
                        12,
                        20,
                        "test",
                        out added,
                        out message) ||
                    !added)
                {
                    throw new InvalidOperationException("Expected second opened chest record to be stored: " + message);
                }

                var openedAfterKnownChest = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(context, settings, "Opened");
                if (string.Equals(opened, openedAfterKnownChest, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected chest label cache signature to change when opened chest records change.");
                }
            });
        }

        private static void InformationChestAlwaysDirtyCacheTracksMovementWorldAndStyle()
        {
            WithTemporaryBehaviorStore(() =>
            {
                var settings = AppSettings.CreateDefault();
                var context = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1400f, 2300f, "world#42", "world-a", "player-a", 100);
                var smallMove = CreateInformationChestCacheContext(1055f, 2048f, 800, 600, 1406f, 2300f, "world#42", "world-a", "player-a", 101);

                var signature = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(context, settings, "Always");
                var smallMoveSignature = InformationOverlayService.BuildChestLabelCacheSignatureForTesting(smallMove, settings, "Always");
                if (!string.Equals(signature, smallMoveSignature, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected Always chest cache signature to survive movement within the same chunk.");
                }

                string reason;
                if (InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, smallMove, settings, "Always", out reason) ||
                    !string.Equals(reason, "cacheHit", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected Always chest cache to hit for small movement, got " + reason + ".");
                }

                var screenChunkMove = CreateInformationChestCacheContext(1088f, 2048f, 800, 600, 1406f, 2300f, "world#42", "world-a", "player-a", 101);
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, screenChunkMove, settings, "Always", out reason) ||
                    !string.Equals(reason, "screenChunkChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected screen chunk movement to dirty Always chest cache, got " + reason + ".");
                }

                var playerChunkMove = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1408f, 2300f, "world#42", "world-a", "player-a", 101);
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, playerChunkMove, settings, "Always", out reason) ||
                    !string.Equals(reason, "playerChunkChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected player chunk movement to dirty Always chest cache, got " + reason + ".");
                }

                var resized = CreateInformationChestCacheContext(1024f, 2048f, 801, 600, 1400f, 2300f, "world#42", "world-a", "player-a", 101);
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, resized, settings, "Always", out reason) ||
                    !string.Equals(reason, "screenSizeChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected screen size changes to dirty Always chest cache, got " + reason + ".");
                }

                var otherWorld = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1400f, 2300f, "world#43", "world-b", "player-a", 101);
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, otherWorld, settings, "Always", out reason) ||
                    !string.Equals(reason, "worldChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected world changes to dirty Always chest cache, got " + reason + ".");
                }

                var styled = AppSettings.CreateDefault();
                styled.InformationChestNameFontScale = 0.81d;
                if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 101, context, styled, "Always", out reason) ||
                    !string.Equals(reason, "styleChanged", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected chest label style changes to dirty Always chest cache, got " + reason + ".");
                }
            });
        }

        private static void InformationChestAlwaysDirtyCacheKeepsSafeRefresh()
        {
            var settings = AppSettings.CreateDefault();
            var context = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1400f, 2300f, "world#42", "world-a", "player-a", 100);

            string reason;
            if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(0, context, settings, "Always", 100, context, settings, "Always", out reason) ||
                !string.Equals(reason, "initial", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected initial Always chest cache scan, got " + reason + ".");
            }

            if (InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 399, context, settings, "Always", out reason) ||
                !string.Equals(reason, "cacheHit", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Always chest cache to hit before the safety refresh tick, got " + reason + ".");
            }

            if (!InformationOverlayService.ShouldRefreshChestAlwaysCacheForTesting(100, context, settings, "Always", 400, context, settings, "Always", out reason) ||
                !string.Equals(reason, "safeRefresh", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Always chest cache safety refresh at 300 ticks, got " + reason + ".");
            }
        }

        private static void InformationChestAlwaysCacheCountersIgnoreOpenedMode()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(1024f, 2048f, 800, 600, 1400f, 2300f, "world#42", "world-a", "player-a", 100);

                    InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    var diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysScanCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysScanCacheHitCount != 0 ||
                        !string.Equals(diagnostics.ChestAlwaysLastDirtyReason, "initial", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected first Always chest scan to record one miss and initial dirty reason.");
                    }

                    context.GameUpdateCount = 101;
                    InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysScanCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysScanCacheHitCount != 1)
                    {
                        throw new InvalidOperationException("Expected repeated Always scan to hit cache.");
                    }

                    context.GameUpdateCount = 102;
                    InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Opened");
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysScanCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysScanCacheHitCount != 1)
                    {
                        throw new InvalidOperationException("Expected Opened mode lookup not to mutate Always cache counters.");
                    }

                    context.GameUpdateCount = 103;
                    InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysScanCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysScanCacheHitCount != 2)
                    {
                        throw new InvalidOperationException("Expected Always cache to survive switching through Opened mode.");
                    }
                }
                finally
                {
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationChestAlwaysTypedScanDiagnosticsTrackFallbackTiles()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                FakeChestMain.ConfigureChest(5, 6, 21, 0);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    var count = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    var diagnostics = InformationOverlayService.GetDiagnostics();
                    if (count != 1)
                    {
                        throw new InvalidOperationException("Expected fake Always chest scan to find one chest, got " + count.ToString(CultureInfo.InvariantCulture) + ".");
                    }

                    if (diagnostics.ChestAlwaysTilesVisitedLast <= 0)
                    {
                        throw new InvalidOperationException("Expected Always chest scan diagnostics to record visited tiles.");
                    }

                    if (string.IsNullOrWhiteSpace(diagnostics.ChestAlwaysTypedTileFastPathStatus) ||
                        diagnostics.ChestAlwaysTypedTileFastPathStatus.IndexOf("fallback=", StringComparison.Ordinal) < 0)
                    {
                        throw new InvalidOperationException("Expected Always chest scan diagnostics to record typed/fallback tile status.");
                    }

                    if (diagnostics.ChestAlwaysNameCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysNameCacheHitCount != 0)
                    {
                        throw new InvalidOperationException("Expected first Always chest name resolve to miss the name cache.");
                    }
                }
                finally
                {
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationChestAlwaysNameCacheReusesAcrossDirtyScans()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                FakeChestMain.ConfigureChest(5, 6, 21, 0);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    if (InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always") != 1)
                    {
                        throw new InvalidOperationException("Expected first fake Always chest scan to find one chest.");
                    }

                    var moved = CreateInformationChestCacheContext(64f, 0f, 320, 240, 128f, 112f, "fake-world#1", "fake-world-record", "player-a", 101);
                    moved.MainType = typeof(FakeChestMain);
                    if (InformationOverlayService.GetChestLabelCountForTesting(moved, settings, "Always") != 1)
                    {
                        throw new InvalidOperationException("Expected dirty fake Always chest scan to keep the same visible chest.");
                    }

                    var diagnostics = InformationOverlayService.GetDiagnostics();
                    if (diagnostics.ChestAlwaysNameCacheMissCount != 1 ||
                        diagnostics.ChestAlwaysNameCacheHitCount != 1)
                    {
                        throw new InvalidOperationException(
                            "Expected Always chest name cache to miss once and hit once, got miss=" +
                            diagnostics.ChestAlwaysNameCacheMissCount.ToString(CultureInfo.InvariantCulture) +
                            " hit=" +
                            diagnostics.ChestAlwaysNameCacheHitCount.ToString(CultureInfo.InvariantCulture) +
                            ".");
                    }
                }
                finally
                {
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationChestAlwaysPartialScanPublishesStableSnapshots()
        {
            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.SetChestAlwaysPartialScanBudgetForTesting(10);
                FakeChestMain.ConfigureChest(5, 6, 21, 0);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    var firstCount = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    var diagnostics = InformationOverlayService.GetDiagnostics();
                    if (firstCount != 0 ||
                        diagnostics.ChestAlwaysPartialScanPendingCount <= 0 ||
                        diagnostics.ChestAlwaysStableSnapshotId != 0)
                    {
                        throw new InvalidOperationException("Expected initial partial Always scan to return no stable labels while pending.");
                    }

                    var completedCount = CompleteAlwaysChestPartialScanForTesting(context, settings, 1, 1);
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (completedCount != 1 ||
                        diagnostics.ChestAlwaysPartialScanPendingCount != 0 ||
                        diagnostics.ChestAlwaysPartialScanFrameCount <= 1 ||
                        diagnostics.ChestAlwaysStableSnapshotId != 1)
                    {
                        throw new InvalidOperationException(
                            "Expected initial partial Always scan to publish stable snapshot 1, count=" +
                            completedCount.ToString(CultureInfo.InvariantCulture) +
                            " pending=" +
                            diagnostics.ChestAlwaysPartialScanPendingCount.ToString(CultureInfo.InvariantCulture) +
                            " frames=" +
                            diagnostics.ChestAlwaysPartialScanFrameCount.ToString(CultureInfo.InvariantCulture) +
                            " stable=" +
                            diagnostics.ChestAlwaysStableSnapshotId.ToString(CultureInfo.InvariantCulture) +
                            ".");
                    }

                    var moved = CreateInformationChestCacheContext(64f, 0f, 320, 240, 96f, 112f, "fake-world#1", "fake-world-record", "player-a", context.GameUpdateCount + 1);
                    moved.MainType = typeof(FakeChestMain);
                    var pendingMoveCount = InformationOverlayService.GetChestLabelCountForTesting(moved, settings, "Always");
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (pendingMoveCount != 1 ||
                        diagnostics.ChestAlwaysPartialScanPendingCount <= 0 ||
                        diagnostics.ChestAlwaysStableSnapshotId != 1)
                    {
                        throw new InvalidOperationException("Expected dirty partial Always scan to keep drawing previous stable snapshot while pending.");
                    }

                    var movedCompletedCount = CompleteAlwaysChestPartialScanForTesting(moved, settings, 2, 1);
                    diagnostics = InformationOverlayService.GetDiagnostics();
                    if (movedCompletedCount != 1 ||
                        diagnostics.ChestAlwaysPartialScanPendingCount != 0 ||
                        diagnostics.ChestAlwaysStableSnapshotId != 2)
                    {
                        throw new InvalidOperationException("Expected dirty partial Always scan to publish a second stable snapshot.");
                    }
                }
                finally
                {
                    InformationOverlayService.SetChestAlwaysPartialScanBudgetForTesting(0);
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static int CompleteAlwaysChestPartialScanForTesting(InformationWorldContext context, AppSettings settings, long expectedStableSnapshotId, int expectedCount)
        {
            var count = 0;
            for (var attempt = 0; attempt < 200; attempt++)
            {
                context.GameUpdateCount++;
                count = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                var diagnostics = InformationOverlayService.GetDiagnostics();
                if (diagnostics.ChestAlwaysPartialScanPendingCount == 0 &&
                    diagnostics.ChestAlwaysStableSnapshotId >= expectedStableSnapshotId)
                {
                    return count;
                }
            }

            throw new InvalidOperationException(
                "Timed out waiting for partial Always chest scan to publish stable snapshot " +
                expectedStableSnapshotId.ToString(CultureInfo.InvariantCulture) +
                " with expected count " +
                expectedCount.ToString(CultureInfo.InvariantCulture) +
                ".");
        }

        private static void PlayerWorldBehaviorRecordsIsolateOpenedChests()
        {
            WithTemporaryBehaviorStore(() =>
            {
                var playerAWorldA = new PlayerWorldBehaviorContext
                {
                    PlayerKey = "player:a",
                    WorldKey = "world:a",
                    PlayerName = "A",
                    WorldName = "A World"
                };
                var playerBWorldA = new PlayerWorldBehaviorContext
                {
                    PlayerKey = "player:b",
                    WorldKey = "world:a",
                    PlayerName = "B",
                    WorldName = "A World"
                };
                var playerAWorldB = new PlayerWorldBehaviorContext
                {
                    PlayerKey = "player:a",
                    WorldKey = "world:b",
                    PlayerName = "A",
                    WorldName = "B World"
                };

                bool added;
                string message;
                if (!PlayerWorldBehaviorStore.TryRecordOpenedChest(playerAWorldA, 10, 20, "test", out added, out message) || !added)
                {
                    throw new InvalidOperationException("Expected opened chest record for player A/world A: " + message);
                }

                if (!PlayerWorldBehaviorStore.ContainsOpenedChest(playerAWorldA, 10, 20))
                {
                    throw new InvalidOperationException("Expected player A/world A to read its opened chest.");
                }

                if (PlayerWorldBehaviorStore.ContainsOpenedChest(playerBWorldA, 10, 20))
                {
                    throw new InvalidOperationException("Expected player B in the same world not to read player A's opened chest.");
                }

                if (PlayerWorldBehaviorStore.ContainsOpenedChest(playerAWorldB, 10, 20))
                {
                    throw new InvalidOperationException("Expected player A in another world not to read world A's opened chest.");
                }

                var fileA = PlayerWorldBehaviorStore.BuildScopedFileNameForTesting(playerAWorldA.PlayerKey, playerAWorldA.WorldKey);
                var fileB = PlayerWorldBehaviorStore.BuildScopedFileNameForTesting(playerBWorldA.PlayerKey, playerBWorldA.WorldKey);
                if (string.Equals(fileA, fileB, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected different player-world pairs to use different behavior files.");
                }
            });
        }

        private static void LegacyOpenedChestKeysMigrateToCurrentPlayerWorldOnly()
        {
            WithTemporaryBehaviorStore(() =>
            {
                var settings = AppSettings.CreateDefault();
                settings.InformationKnownChestKeys.Add("same-world#42|10|20");
                settings.InformationKnownChestKeys.Add("other-world#43|30|40");

                var current = CreateInformationChestRecordContext("player-a", "world-a", "same-world#42");
                var imported = InformationOverlayService.ImportLegacyKnownChestsForTesting(current, settings);
                if (imported != 1)
                {
                    throw new InvalidOperationException("Expected one legacy opened chest to migrate into the current player-world record.");
                }

                var currentBehavior = new PlayerWorldBehaviorContext { PlayerKey = current.PlayerRecordKey, WorldKey = current.WorldRecordKey };
                if (!PlayerWorldBehaviorStore.ContainsOpenedChest(currentBehavior, 10, 20))
                {
                    throw new InvalidOperationException("Expected migrated legacy chest to be readable for the current player-world.");
                }

                var otherPlayer = CreateInformationChestRecordContext("player-b", "world-a", "same-world#42");
                var otherBehavior = new PlayerWorldBehaviorContext { PlayerKey = otherPlayer.PlayerRecordKey, WorldKey = otherPlayer.WorldRecordKey };
                if (PlayerWorldBehaviorStore.ContainsOpenedChest(otherBehavior, 10, 20))
                {
                    throw new InvalidOperationException("Expected migrated legacy chest not to leak to another player in the same world.");
                }

                if (settings.InformationKnownChestKeys.Count != 1 ||
                    !string.Equals(settings.InformationKnownChestKeys[0], "other-world#43|30|40", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected migrated legacy key to be removed while other worlds remain.");
                }
            });
        }

        private static InformationWorldContext CreateInformationChestRecordContext(string playerKey, string worldKey, string legacyWorldKey)
        {
            return new InformationWorldContext
            {
                WorldKey = legacyWorldKey,
                PlayerRecordKey = playerKey,
                WorldRecordKey = worldKey,
                PlayerName = playerKey,
                WorldName = legacyWorldKey,
                ScreenX = 1280f,
                ScreenY = 640f,
                ScreenWidth = 1920,
                ScreenHeight = 1080
            };
        }

        private static InformationWorldContext CreateInformationChestCacheContext(
            float screenX,
            float screenY,
            int screenWidth,
            int screenHeight,
            float playerCenterX,
            float playerCenterY,
            string worldKey,
            string worldRecordKey,
            string playerRecordKey,
            ulong gameUpdateCount)
        {
            return new InformationWorldContext
            {
                LocalPlayer = new object(),
                ScreenX = screenX,
                ScreenY = screenY,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                PlayerCenterX = playerCenterX,
                PlayerCenterY = playerCenterY,
                WorldKey = worldKey,
                WorldRecordKey = worldRecordKey,
                PlayerRecordKey = playerRecordKey,
                PlayerName = playerRecordKey,
                WorldName = worldKey,
                GameUpdateCount = gameUpdateCount
            };
        }

        private static class FakeChestMain
        {
            public static FakeChestTile[,] tile = new FakeChestTile[1, 1];
            public static bool[] tileContainer = new bool[1024];
            public static int maxTilesX = 1;
            public static int maxTilesY = 1;

            public static void ConfigureChest(int chestX, int chestY, int tileType, int style)
            {
                maxTilesX = Math.Max(24, chestX + 4);
                maxTilesY = Math.Max(24, chestY + 4);
                tile = new FakeChestTile[maxTilesX, maxTilesY];
                tileContainer = new bool[1024];
                if (tileType >= 0 && tileType < tileContainer.Length)
                {
                    tileContainer[tileType] = true;
                }

                SetTile(chestX, chestY, tileType, style * 36, 0);
                SetTile(chestX + 1, chestY, tileType, style * 36 + 18, 0);
                SetTile(chestX, chestY + 1, tileType, style * 36, 18);
                SetTile(chestX + 1, chestY + 1, tileType, style * 36 + 18, 18);
            }

            public static void ConfigureDresser(int chestX, int chestY, int tileType, int style)
            {
                maxTilesX = Math.Max(24, chestX + 5);
                maxTilesY = Math.Max(24, chestY + 4);
                tile = new FakeChestTile[maxTilesX, maxTilesY];
                tileContainer = new bool[1024];
                if (tileType >= 0 && tileType < tileContainer.Length)
                {
                    tileContainer[tileType] = true;
                }

                var frameX = style * 54;
                SetTile(chestX, chestY, tileType, frameX, 0);
                SetTile(chestX + 1, chestY, tileType, frameX + 18, 0);
                SetTile(chestX + 2, chestY, tileType, frameX + 36, 0);
                SetTile(chestX, chestY + 1, tileType, frameX, 18);
                SetTile(chestX + 1, chestY + 1, tileType, frameX + 18, 18);
                SetTile(chestX + 2, chestY + 1, tileType, frameX + 36, 18);
            }

            public static void Reset()
            {
                tile = new FakeChestTile[1, 1];
                tileContainer = new bool[1024];
                maxTilesX = 1;
                maxTilesY = 1;
            }

            private static void SetTile(int x, int y, int tileType, int frameX, int frameY)
            {
                tile[x, y] = new FakeChestTile
                {
                    IsActive = true,
                    type = tileType,
                    frameX = frameX,
                    frameY = frameY
                };
            }
        }

        private sealed class FakeChestTile
        {
            public bool IsActive { get; set; }
            public int type;
            public int frameX;
            public int frameY;
        }

        private static class FakeTileHighlightMain
        {
            public static FakeChestTile[,] tile = new FakeChestTile[1, 1];

            public static void ConfigureLifeCrystalGroup()
            {
                tile = new FakeChestTile[32, 32];
                SetTile(4, 4, 12);
                SetTile(5, 4, 12);
                SetTile(4, 5, 12);
                SetTile(5, 5, 12);
                SetTile(10, 10, 639);
            }

            public static void Reset()
            {
                tile = new FakeChestTile[1, 1];
            }

            private static void SetTile(int x, int y, int tileType)
            {
                tile[x, y] = new FakeChestTile
                {
                    IsActive = true,
                    type = tileType
                };
            }
        }

        private static void WithTemporaryBehaviorStore(Action action)
        {
            var directory = Path.Combine(Path.GetTempPath(), "JueMingZ.Tests", "behavior-" + Guid.NewGuid().ToString("N"));
            try
            {
                PlayerWorldBehaviorStore.SetBehaviorDirectoryForTesting(directory);
                action();
            }
            finally
            {
                PlayerWorldBehaviorStore.ResetForTesting();
                TryDeleteDirectory(directory);
            }
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
            }
        }

        private static void InformationChestKeyParsingSurvivesWorldRenameWithSameId()
        {
            int x;
            int y;
            if (!InformationOverlayService.TryParseChestKeyForTesting("old-name#12345|88|91", "new-name#12345", out x, out y) ||
                x != 88 ||
                y != 91)
            {
                throw new InvalidOperationException("Expected opened chest keys to survive display-name-only world changes.");
            }

            if (InformationOverlayService.TryParseChestKeyForTesting("old-name#12345|88|91", "other-world#54321", out x, out y))
            {
                throw new InvalidOperationException("Expected opened chest keys from a different world id to stay isolated.");
            }
        }

        private static void InformationChestTileFallbackDetectsBasicContainerIds()
        {
            if (!InformationOverlayService.IsChestTileTypeForTesting(21) ||
                !InformationOverlayService.IsChestTileTypeForTesting(467))
            {
                throw new InvalidOperationException("Expected chest tile fallback to recognize vanilla container tile ids.");
            }

            if (InformationOverlayService.IsChestTileTypeForTesting(12))
            {
                throw new InvalidOperationException("Expected chest tile fallback to reject life crystal tile ids.");
            }
        }

        private static void InformationChestTileFallbackIncludesDressersAndExcludesDisplayContainers()
        {
            if (!InformationOverlayService.IsChestTileTypeForTesting(88) ||
                !InformationOverlayService.IsChestTileTypeForTesting(441) ||
                !InformationOverlayService.IsChestTileTypeForTesting(468))
            {
                throw new InvalidOperationException("Expected chest labels to include dressers and fake container tile ids.");
            }

            if (InformationOverlayService.IsChestTileTypeForTesting(470) ||
                InformationOverlayService.IsChestTileTypeForTesting(475) ||
                InformationOverlayService.IsChestTileTypeForTesting(128) ||
                InformationOverlayService.IsChestTileTypeForTesting(269))
            {
                throw new InvalidOperationException("Expected chest labels to reject display dolls, hat racks, and mannequin tile ids.");
            }
        }

        private static void InformationChestTileFallbackNormalizesTwoByTwoFrameOrigin()
        {
            int x;
            int y;
            if (!InformationOverlayService.TryNormalizeChestOriginFromFrameForTesting(101, 201, 18, 18, out x, out y) ||
                x != 100 ||
                y != 200)
            {
                throw new InvalidOperationException("Expected bottom-right chest frame tile to normalize to the chest origin.");
            }

            if (!InformationOverlayService.TryNormalizeChestOriginFromFrameForTesting(100, 200, 0, 0, out x, out y) ||
                x != 100 ||
                y != 200)
            {
                throw new InvalidOperationException("Expected top-left chest frame tile to keep its origin.");
            }
        }

        private static void InformationDresserChestLabelsUseThreeByTwoFrameRules()
        {
            int x;
            int y;
            if (!InformationOverlayService.TryNormalizeChestOriginFromFrameForTesting(88, 102, 201, 36, 18, out x, out y) ||
                x != 100 ||
                y != 200)
            {
                throw new InvalidOperationException("Expected bottom-right dresser frame tile to normalize with 3x2 dresser geometry.");
            }

            if (InformationOverlayService.BuildChestTileStyleForTesting(88, 54 * 2 + 36) != 2)
            {
                throw new InvalidOperationException("Expected dresser style to use 54px frame width.");
            }

            if (InformationOverlayService.BuildChestTileStyleForTesting(21, 36 * 2 + 18) != 2)
            {
                throw new InvalidOperationException("Expected normal chest style to keep 36px frame width.");
            }

            WithTemporaryBehaviorStore(() =>
            {
                InformationOverlayService.ResetChestLabelCacheForTesting();
                FakeChestMain.ConfigureDresser(5, 6, 88, 1);
                try
                {
                    var settings = AppSettings.CreateDefault();
                    var context = CreateInformationChestCacheContext(0f, 0f, 320, 240, 112f, 112f, "fake-world#1", "fake-world-record", "player-a", 100);
                    context.MainType = typeof(FakeChestMain);

                    var count = InformationOverlayService.GetChestLabelCountForTesting(context, settings, "Always");
                    if (count != 1)
                    {
                        throw new InvalidOperationException("Expected one 3x2 dresser to produce one chest label, got " + count.ToString(CultureInfo.InvariantCulture) + ".");
                    }
                }
                finally
                {
                    FakeChestMain.Reset();
                    InformationOverlayService.ResetChestLabelCacheForTesting();
                }
            });
        }

        private static void InformationDresserDisplayNameAvoidsMapObjectOptionBleed()
        {
            var name = InformationOverlayService.ResolveChestTileDisplayNameForTesting(88, 30);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Expected dresser display name to resolve to a dresser name or fallback.");
            }

            if (name.IndexOf("梳妆", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("Dresser", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("Expected dresser display name to use dresser naming, got " + name + ".");
            }

            if (string.Equals(name, "机关", StringComparison.Ordinal) ||
                string.Equals(name, "雕像", StringComparison.Ordinal) ||
                string.Equals(name, "长椅", StringComparison.Ordinal) ||
                string.Equals(name, "熔炉", StringComparison.Ordinal) ||
                string.Equals(name, "未录制的音乐盒", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected dresser display name not to bleed into unrelated map object names, got " + name + ".");
            }
        }

        private static void InformationChestDisplayNameAvoidsMapObjectOptionBleed()
        {
            var ivyName = InformationOverlayService.ResolveChestTileDisplayNameForTesting(21, 10);
            AssertChestDisplayNameDoesNotBleedIntoMapObject(ivyName, "primary chest style 10");
            AssertChestDisplayNameIsIvyOrGenericFallback(ivyName, "primary chest style 10");

            var trappedIvyName = InformationOverlayService.ResolveChestTileDisplayNameForTesting(441, 10);
            AssertChestDisplayNameDoesNotBleedIntoMapObject(trappedIvyName, "fake primary chest style 10");
            AssertChestDisplayNameIsIvyOrGenericFallback(trappedIvyName, "fake primary chest style 10");

            var secondaryGoldName = InformationOverlayService.ResolveChestTileDisplayNameForTesting(467, 4);
            AssertChestDisplayNameDoesNotBleedIntoMapObject(secondaryGoldName, "secondary chest style 4");
        }

        private static void AssertChestDisplayNameDoesNotBleedIntoMapObject(string name, string label)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Expected " + label + " to resolve to a non-empty chest display name.");
            }

            if (string.Equals(name, "猩红祭坛", StringComparison.Ordinal) ||
                string.Equals(name, "Crimson Altar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "恶魔祭坛", StringComparison.Ordinal) ||
                string.Equals(name, "Demon Altar", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected " + label + " not to bleed into map object names, got " + name + ".");
            }
        }

        private static void AssertChestDisplayNameIsIvyOrGenericFallback(string name, string label)
        {
            if (string.Equals(name, "宝箱", StringComparison.Ordinal) ||
                string.Equals(name, "Chest", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (name.IndexOf("常春藤", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Ivy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            throw new InvalidOperationException("Expected " + label + " to resolve to an ivy chest name or generic fallback, got " + name + ".");
        }

        private static void InformationChestLabelsFrameLimitAllowsDenseRooms()
        {
            if (InformationOverlayService.MaxChestLabelsPerFrameForTesting() < 240)
            {
                throw new InvalidOperationException("Expected chest label frame limit to support dense chest rooms beyond the old 80-label cap.");
            }
        }

        private static void InformationNpcLabelSnapshotReusesMovementOnly()
        {
            if (!InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    42,
                    25,
                    100,
                    false,
                    false,
                    false,
                    false))
            {
                throw new InvalidOperationException("Expected NPC label snapshot refresh to reuse text/color when only position and non-eligibility life changed.");
            }

            if (InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    42,
                    0,
                    100,
                    false,
                    false,
                    false,
                    false))
            {
                throw new InvalidOperationException("Expected dead NPC snapshot to dirty the label cache.");
            }

            if (InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    42,
                    80,
                    100,
                    false,
                    true,
                    false,
                    false))
            {
                throw new InvalidOperationException("Expected friendly-state changes to dirty the NPC label cache.");
            }

            if (InformationOverlayService.CanReuseNpcLabelSnapshotForTesting(
                    7,
                    42,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false,
                    7,
                    43,
                    80,
                    100,
                    false,
                    false,
                    false,
                    false))
            {
                throw new InvalidOperationException("Expected NPC type changes to dirty the label cache.");
            }
        }

        private static void InformationChestLabelsDrawOrderPrioritizesScreenCenter()
        {
            var context = new InformationWorldContext
            {
                ScreenX = 1000f,
                ScreenY = 1000f,
                ScreenWidth = 800,
                ScreenHeight = 600
            };
            var sorted = InformationOverlayService.SortChestLabelIndicesForTesting(
                context,
                new[] { 1010f, 1400f, 1780f, 500f },
                new[] { 1300f, 1300f, 1300f, 1300f });

            if (sorted.Length != 4 || sorted[0] != 1)
            {
                throw new InvalidOperationException("Expected chest label draw order to prioritize labels nearest the current screen center.");
            }

            if (sorted[sorted.Length - 1] != 3)
            {
                throw new InvalidOperationException("Expected off-screen padding labels to be drawn after labels inside the current screen.");
            }
        }

        private static void InformationChestLabelSortCacheDirtiesOnSourceAndMovementThreshold()
        {
            if (InformationOverlayService.ShouldRefreshChestLabelSortForTesting(
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u,
                    1063f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u))
            {
                throw new InvalidOperationException("Expected chest label sort cache to survive movement below the refresh threshold.");
            }

            if (!InformationOverlayService.ShouldRefreshChestLabelSortForTesting(
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u,
                    1064f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u))
            {
                throw new InvalidOperationException("Expected chest label sort cache to dirty once player movement reaches the threshold.");
            }

            if (!InformationOverlayService.ShouldRefreshChestLabelSortForTesting(
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u,
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    2u))
            {
                throw new InvalidOperationException("Expected chest label sort cache to dirty when the source label snapshot changes.");
            }

            if (!InformationOverlayService.ShouldRefreshChestLabelSortForTesting(
                    1000f,
                    1000f,
                    0f,
                    0f,
                    800,
                    600,
                    1u,
                    1000f,
                    1000f,
                    64f,
                    0f,
                    800,
                    600,
                    1u))
            {
                throw new InvalidOperationException("Expected chest label sort cache to dirty when screen center moves by the threshold.");
            }
        }

        private static void InformationChestLabelCacheCullCoversBucketMovement()
        {
            var context = new InformationWorldContext
            {
                LocalPlayer = new object(),
                ScreenX = 1000f,
                ScreenY = 1000f,
                ScreenWidth = 800,
                ScreenHeight = 600,
                PlayerCenterX = 1400f,
                PlayerCenterY = 1300f
            };

            if (!InformationOverlayService.CanCacheChestLabelForTesting(context, 1895f, 1300f))
            {
                throw new InvalidOperationException("Expected chest cache cull to prefetch labels that can enter the draw cull before the next screen bucket refresh.");
            }

            if (InformationOverlayService.CanCacheChestLabelForTesting(context, 1900f, 1300f))
            {
                throw new InvalidOperationException("Expected chest cache cull to keep a finite padding budget for dense-room performance.");
            }
        }

        private static void InformationLuckBreakdownFollowsTerrariaSourceFormula()
        {
            AssertNear(InformationLuckBreakdownBuilder.CalculateLadyBugContributionForTesting(43200, 43200, -10800), 0.2d, "positive ladybug luck");
            AssertNear(InformationLuckBreakdownBuilder.CalculateLadyBugContributionForTesting(-10800, 43200, -10800), -0.2d, "negative ladybug luck");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(0d), 0d, "zero coin luck");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(0.249d), 0.025d, "minimum nonzero coin luck");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(0.25d), 0.05d, "coin luck first threshold");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(25d), 0.1d, "coin luck mid threshold");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(249001d), 0.2d, "coin luck maximum threshold");
        }

        private static void InformationLuckBreakdownWrapsSourceDetails()
        {
            var contributions = new List<InformationLuckContribution>
            {
                new InformationLuckContribution { Label = "幸运药水", Amount = 0.3d, Detail = "等级3" },
                new InformationLuckContribution { Label = "花园侏儒", Amount = 0.2d },
                new InformationLuckContribution { Label = "火把", Amount = -0.06d, Detail = "原值-0.3" }
            };

            var lines = InformationLuckBreakdownBuilder.BuildDisplayLinesForTesting(0.54d, contributions, 24);
            if (lines.Length < 3)
            {
                throw new InvalidOperationException("Expected luck breakdown to wrap source details into multiple lines.");
            }

            AssertContains(lines[0], "幸运值: +0.54");
            AssertDoesNotContain(lines[0], "已解析");
            AssertContains(string.Join("|", lines), "其他/未解析 +0.1");
            AssertContains(string.Join("|", lines), "幸运药水 +0.3");
            AssertContains(string.Join("|", lines), "火把 -0.06");
        }

        private static void AssertHeightLevels(InformationWorldContext context, int tileY, int[] buffer, int[] expected, string label)
        {
            var count = InformationFishingConditionRolls.BuildHeightLevels(context, tileY, buffer);
            if (count != expected.Length)
            {
                throw new InvalidOperationException("Expected " + label + " height roll count " + expected.Length.ToString(CultureInfo.InvariantCulture) + ", got " + count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            for (var index = 0; index < count; index++)
            {
                if (buffer[index] != expected[index])
                {
                    throw new InvalidOperationException("Expected " + label + " height roll at " + index.ToString(CultureInfo.InvariantCulture) + " to be " + expected[index].ToString(CultureInfo.InvariantCulture) + ", got " + buffer[index].ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static void AssertBoolRolls(bool[] actual, bool[] expected, string label)
        {
            if (actual == null || actual.Length != expected.Length)
            {
                throw new InvalidOperationException("Expected " + label + " bool roll count " + expected.Length.ToString(CultureInfo.InvariantCulture) + ".");
            }

            for (var index = 0; index < actual.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    throw new InvalidOperationException("Expected " + label + " bool roll at " + index.ToString(CultureInfo.InvariantCulture) + " to be " + expected[index] + ", got " + actual[index] + ".");
                }
            }
        }

        private static void AssertMemberReference(object instance, string name, object expected, string label)
        {
            var actual = InformationReflection.GetMember(instance, name);
            if (!object.ReferenceEquals(actual, expected))
            {
                throw new InvalidOperationException("Expected " + label + " to preserve the original object reference.");
            }
        }

        private static void AssertMemberInt(object instance, string name, int expected, string label)
        {
            var actual = InformationFishingCatchResolver.ToInt(InformationReflection.GetMember(instance, name), int.MinValue);
            if (actual != expected)
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected.ToString(CultureInfo.InvariantCulture) + ", got " + actual.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static void AssertMemberFloat(object instance, string name, float expected, string label)
        {
            var actual = InformationFishingCatchResolver.ToFloat(InformationReflection.GetMember(instance, name), float.NaN);
            if (float.IsNaN(actual) || Math.Abs(actual - expected) > 0.0001f)
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected.ToString(CultureInfo.InvariantCulture) + ", got " + actual.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static void AssertMemberBool(object instance, string name, bool expected, string label)
        {
            bool actual;
            if (!InformationFishingCatchResolver.TryConvertBool(InformationReflection.GetMember(instance, name), out actual) || actual != expected)
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected + ".");
            }
        }

        private static void AssertIntSequence(IList<int> actual, int[] expected, string label)
        {
            if (actual == null || actual.Count != expected.Length)
            {
                throw new InvalidOperationException("Expected " + label + " count " + expected.Length.ToString(CultureInfo.InvariantCulture) + ".");
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    throw new InvalidOperationException("Expected " + label + " item " + index.ToString(CultureInfo.InvariantCulture) + " to be " + expected[index].ToString(CultureInfo.InvariantCulture) + ", got " + actual[index].ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static string SummarizeFishingConditionRoll(FishingConditionRoll roll)
        {
            return "h" + roll.HeightLevel.ToString(CultureInfo.InvariantCulture) +
                   "|corrupt=" + (roll.Corrupt ? "1" : "0") +
                   "|crimson=" + (roll.Crimson ? "1" : "0") +
                   "|honey=" + (roll.InHoney ? "1" : "0") +
                   "|jungle=" + (roll.Jungle ? "1" : "0") +
                   "|snow=" + (roll.Snow ? "1" : "0") +
                   "|desert=" + (roll.Desert ? "1" : "0") +
                   "|infected=" + (roll.InfectedDesert ? "1" : "0") +
                   "|crate=" + (roll.Crate ? "1" : "0") +
                   "|junk=" + (roll.Junk ? "1" : "0");
        }

        private sealed class TestInformationFishingEnvironmentPlayer
        {
            public double luck;
            public int fishingSkill;
            public bool accLavaFishing;
            public bool ZoneJungle;
            public bool ZoneSnow;
            public bool ZoneDesert;
            public bool ZoneUndergroundDesert;
            public bool ZoneCorrupt;
            public bool ZoneCrimson;
            public int[] buffType = new int[0];
            public int[] buffTime = new int[0];
        }

        private sealed class TestInformationFishingConditions
        {
            public int PoleItemType;
            public int BaitItemType;
        }

        private sealed class TestInformationFishDropRule
        {
            public int[] PossibleItems;
            public bool IsStopper;
            public bool Allow;
            public int MeetsConditionsCallCount;

            public TestInformationFishDropRule(int[] possibleItems)
            {
                PossibleItems = possibleItems;
                Allow = true;
            }

            private bool MeetsConditions(object fishingContext, bool includeHighQuality)
            {
                MeetsConditionsCallCount++;
                return Allow && fishingContext != null && includeHighQuality;
            }
        }

        private sealed class TestInformationFishDropsDb
        {
            public ArrayList Rules;

            public TestInformationFishDropsDb(ArrayList rules)
            {
                Rules = rules;
            }
        }

        private static string BuildDetailedFishingCatchQuerySignature(
            InformationWorldContext context,
            string liquidKind,
            int waterTiles,
            int chums,
            int waterNeeded,
            bool junkPossible,
            int finalFishingLevel,
            int fishingLevel,
            bool canFishInLava,
            string filterSignature)
        {
            return InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                liquidKind,
                waterTiles,
                chums,
                waterNeeded,
                junkPossible,
                finalFishingLevel,
                fishingLevel,
                25,
                TestFishingRod,
                15,
                267,
                canFishInLava,
                2454,
                filterSignature);
        }

        private static void AssertFishingCatchSignatureChanged(string baseline, string changed, string fieldName)
        {
            if (string.Equals(baseline, changed, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + fieldName + " changes to dirty the fishing catch query cache key.");
            }
        }

        private static IList<FishingCatchCandidate> CreateSingleFishingCatchCandidate(int id, string name)
        {
            return new List<FishingCatchCandidate>
            {
                new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = id,
                    DisplayName = name,
                    DisplayNameSnapshot = name
                }
            };
        }

        private static class TestInformationFishingQueryMain
        {
            public static bool hardMode;
            public static int moonPhase;
            public static int maxTilesX;
            public static int maxTilesY;
            public static double worldSurface;
            public static double rockLayer;
            public static double time;
            public static bool remixWorld;
            public static bool notTheBeesWorld;

            public static void Reset()
            {
                hardMode = false;
                moonPhase = 1;
                maxTilesX = 4200;
                maxTilesY = 1200;
                worldSurface = 320d;
                rockLayer = 520d;
                time = 7200d;
                remixWorld = false;
                notTheBeesWorld = false;
            }
        }

        private sealed class TestInformationProjectile
        {
            public bool active;
            public bool bobber;
            public int owner;
            public int identity;
            public Terraria.TestVector2 Center;
        }

        private sealed class TestInformationPropertyTile
        {
            public bool HasTile { get; set; }
            public ushort TileType { get; set; }
            public short TileFrameX { get; set; }
            public short TileFrameY { get; set; }
        }

        private sealed class TestInformationMethodTile
        {
            public ushort type = 236;
            public short frameX = 54;
            public short frameY = 72;

            public bool active()
            {
                return true;
            }
        }

        private static void CombatEquipmentWarningMatchesRequestedEquipmentNames()
        {
            if (!CombatEquipmentWarningService.IsCombatHazardForTesting(true, false) ||
                !CombatEquipmentWarningService.IsCombatHazardForTesting(false, true) ||
                CombatEquipmentWarningService.IsCombatHazardForTesting(false, false))
            {
                throw new InvalidOperationException("Expected equipment warning hazard gate to accept bosses or non-blood-moon events only.");
            }

            if (!CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("建筑师发明背包") ||
                !CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("R.E.K. 3000") ||
                !CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("Hand of Creation") ||
                !CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("FPV飞行眼镜") ||
                !CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("Music Box (Overworld Day)") ||
                CombatEquipmentWarningService.IsNonCombatEquipmentNameForTesting("Warrior Emblem"))
            {
                throw new InvalidOperationException("Expected requested equipment names to match and combat accessory names not to match.");
            }
        }

        private static void CombatEquipmentWarningPromptsOnlyOnHazardEntry()
        {
            if (Math.Abs(CombatEquipmentWarningService.PromptDurationSecondsForTesting - 2d) > 0.001d)
            {
                throw new InvalidOperationException("Expected combat equipment warning prompt to stay fully readable for 2 seconds.");
            }

            if (CombatEquipmentWarningService.PromptFadeDurationSecondsForTesting <= 0d ||
                CombatEquipmentWarningService.PromptTotalDurationSecondsForTesting <= 2d)
            {
                throw new InvalidOperationException("Expected combat equipment warning prompt to use a short fade after the 2 second readable window.");
            }

            if (Math.Abs(CombatEquipmentWarningService.CalculatePromptAlphaForTesting(0d) - 1d) > 0.001d ||
                Math.Abs(CombatEquipmentWarningService.CalculatePromptAlphaForTesting(1.99d) - 1d) > 0.001d ||
                CombatEquipmentWarningService.CalculatePromptAlphaForTesting(2.125d) >= 1d ||
                CombatEquipmentWarningService.CalculatePromptAlphaForTesting(2.3d) > 0.001d)
            {
                throw new InvalidOperationException("Expected combat equipment warning prompt alpha to remain full for 2 seconds, then fade out.");
            }

            var yAtStart = CombatEquipmentWarningPromptOverlay.CalculatePromptDrawYForTesting(100f, 20f, 0d);
            var yAtEnd = CombatEquipmentWarningPromptOverlay.CalculatePromptDrawYForTesting(100f, 20f, 1d);
            if (Math.Abs(yAtStart - yAtEnd) > 0.001f)
            {
                throw new InvalidOperationException("Expected combat equipment warning prompt to avoid vertical progress animation.");
            }

            if (CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(0, false, false, false, false, false, true, false, false, false, false, false))
            {
                throw new InvalidOperationException("Expected DD2 ready-to-find-bartender state not to count as an active event.");
            }

            if (CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(0, false, false, false, false, false, false, false, false, false, false, false))
            {
                throw new InvalidOperationException("Expected no active event when all event flags are false.");
            }

            if (!CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(1, false, false, false, false, false, false, false, false, false, false, false) ||
                !CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(0, true, false, false, false, false, false, false, false, false, false, false) ||
                !CombatEquipmentWarningService.IsNonBloodMoonEventForTesting(0, false, false, false, false, true, false, false, false, false, false, false))
            {
                throw new InvalidOperationException("Expected invasion, pumpkin moon, and ongoing DD2 to count as active events.");
            }

            if (!CombatEquipmentWarningService.ShouldPromptForHazardEntryForTesting(string.Empty, "boss:134", true))
            {
                throw new InvalidOperationException("Expected first boss hazard entry with non-combat equipment to prompt.");
            }

            if (CombatEquipmentWarningService.ShouldPromptForHazardEntryForTesting("boss:134", "boss:134", true))
            {
                throw new InvalidOperationException("Expected continuous same boss hazard not to prompt repeatedly.");
            }

            if (CombatEquipmentWarningService.ShouldPromptForHazardEntryForTesting(string.Empty, "event:eclipse", false))
            {
                throw new InvalidOperationException("Expected hazard entry without non-combat equipment not to prompt.");
            }
        }

        private static void CombatPerformanceCachesStableMetadataOnly()
        {
            if (TerrariaTypeCache.Find("System.String") != typeof(string) ||
                TerrariaTypeCache.Find("System.String, mscorlib") != typeof(string))
            {
                throw new InvalidOperationException("Expected shared Terraria type cache to resolve and reuse clean type names.");
            }

            if (CombatEquipmentWarningService.HazardScanIntervalTicksForTesting <= 0)
            {
                throw new InvalidOperationException("Expected combat equipment warning hazard scan to be throttled by a positive tick interval.");
            }

            if (!CombatEquipmentWarningService.ShouldRunHazardScanForTesting(120, 0) ||
                CombatEquipmentWarningService.ShouldRunHazardScanForTesting(125, 132) ||
                !CombatEquipmentWarningService.ShouldRunHazardScanForTesting(132, 132))
            {
                throw new InvalidOperationException("Expected hazard scan throttle to skip only intermediate ticks and run at the next due tick.");
            }
        }

        private static void RuntimePerformanceDiagnosticsRecordsSlowestOperation()
        {
            RuntimePerformanceDiagnostics.ResetForTesting();
            RuntimePerformanceDiagnostics.Record(8d, 9d, 1d, 2d, 3d, 6d, "stage-a", 4d, "dispatch.service-a", 5d);
            if (!string.Equals(RuntimePerformanceDiagnostics.LastSlowestOperationName, "dispatch.service-a", StringComparison.Ordinal) ||
                Math.Abs(RuntimePerformanceDiagnostics.LastSlowestOperationElapsedMs - 5d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.LastInformationDrawMs - 6d) > 0.001d)
            {
                throw new InvalidOperationException("Expected runtime performance diagnostics to keep the slowest sub-operation.");
            }

            var sample = new PerformanceHitchSample
            {
                UtcNow = DateTime.UtcNow,
                RuntimeUpdateMs = PerformanceHitchRecorder.RuntimeUpdateThresholdMs,
                SlowestStageName = "automation-request-dispatch",
                SlowestStageElapsedMs = 26d,
                SlowestOperationName = "dispatch.combat-equipment-warning",
                SlowestOperationElapsedMs = 12d
            };

            var reason = PerformanceHitchRecorder.BuildReason(sample);
            RuntimePerformanceDiagnostics.RecordHitch(sample, reason, string.Empty);
            if (!string.Equals(RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestOperationName, "dispatch.combat-equipment-warning", StringComparison.Ordinal) ||
                Math.Abs(RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestOperationMs - 12d) > 0.001d)
            {
                throw new InvalidOperationException("Expected hitch diagnostics to preserve the slowest sub-operation.");
            }

            RuntimePerformanceDiagnostics.ResetForTesting();
            var capacity = RuntimePerformanceDiagnostics.RecentWindowCapacitySamples;
            for (var index = 0; index < capacity; index++)
            {
                RuntimePerformanceDiagnostics.Record(1d, 0d, 1d, 1d, 1d, 1d, "stage-a", 1d, "dispatch.service-a", 1d);
            }

            RuntimePerformanceDiagnostics.Record(601d, 0d, 601d, 601d, 601d, 601d, "stage-b", 601d, "dispatch.service-b", 601d);
            if (RuntimePerformanceDiagnostics.RecentWindowSampleCount != capacity ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentRuntimeUpdateAverageMs - 2d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentGameStateReadAverageMs - 2d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentActionQueueUpdateAverageMs - 2d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentInputActionUpdateAverageMs - 2d) > 0.001d ||
                Math.Abs(RuntimePerformanceDiagnostics.RecentInformationDrawAverageMs - 2d) > 0.001d)
            {
                throw new InvalidOperationException("Expected runtime performance diagnostics to keep a fixed-size recent average window.");
            }
        }

        private static void InformationOverlayDiagnosticsWriterPreservesSectionCounts()
        {
            InformationOverlayDiagnosticsWriter.ResetForTesting();

            InformationOverlayDiagnosticsWriter.UpdateWorldOverlay(1, 2, 3, 4, 5, 6.5d, "worldOverlayProbe");
            var world = InformationOverlayService.GetDiagnostics();
            if (world.NpcLabelsDrawn != 1 ||
                world.ChestLabelsDrawn != 2 ||
                world.SignTextLabelsDrawn != 3 ||
                world.TombstoneTextLabelsDrawn != 4 ||
                world.TileHighlightsDrawn != 5 ||
                world.StatusLinesDrawn != 0 ||
                Math.Abs(world.LastDrawElapsedMs - 6.5d) > 0.001d ||
                !string.Equals(world.LastSkipReason, "worldOverlayProbe", StringComparison.Ordinal) ||
                Math.Abs(InformationOverlayService.GetLastDrawElapsedMs() - 6.5d) > 0.001d)
            {
                throw new InvalidOperationException("Expected information diagnostics writer to publish world overlay counts and last draw sample.");
            }

            InformationOverlayDiagnosticsWriter.UpdateStatusPanel(7, 8.25d, "statusPanelProbe");
            var status = InformationOverlayService.GetDiagnostics();
            if (status.NpcLabelsDrawn != 1 ||
                status.ChestLabelsDrawn != 2 ||
                status.SignTextLabelsDrawn != 3 ||
                status.TombstoneTextLabelsDrawn != 4 ||
                status.TileHighlightsDrawn != 5 ||
                status.StatusLinesDrawn != 7 ||
                Math.Abs(status.LastDrawElapsedMs - 8.25d) > 0.001d ||
                !string.Equals(status.LastSkipReason, "statusPanelProbe", StringComparison.Ordinal) ||
                Math.Abs(InformationOverlayService.GetLastDrawElapsedMs() - 8.25d) > 0.001d)
            {
                throw new InvalidOperationException("Expected status diagnostics update to preserve world counts and publish the latest draw sample.");
            }

            InformationOverlayDiagnosticsWriter.ResetForTesting();
        }

        private static void FishingFilterNestedScrollBubblesWhenListCannotMove()
        {
            FishingFilterUiState.Reset();
            var mouse = new LegacyMouseSnapshot
            {
                X = 20,
                Y = 20,
                ScrollDelta = -120
            };

            FishingFilterUiState.SetEntryViewport(new LegacyUiRect(10, 10, 100, 100), 80);
            if (FishingFilterUiState.TryConsumeNestedScroll(mouse, -120))
            {
                throw new InvalidOperationException("Expected a non-scrollable entry viewport to bubble to the main window.");
            }

            FishingFilterUiState.SetEntryViewport(new LegacyUiRect(10, 10, 100, 100), 240);
            if (!FishingFilterUiState.TryConsumeNestedScroll(mouse, -120))
            {
                throw new InvalidOperationException("Expected a scrollable entry viewport to consume downward wheel.");
            }

            if (FishingFilterUiState.EntryScrollOffset <= 0)
            {
                throw new InvalidOperationException("Expected entry scroll offset to increase.");
            }

            mouse.ScrollDelta = 120;
            if (!FishingFilterUiState.TryConsumeNestedScroll(mouse, 120))
            {
                throw new InvalidOperationException("Expected a scrollable entry viewport to consume upward wheel before reaching the top.");
            }

            if (FishingFilterUiState.EntryScrollOffset != 0)
            {
                throw new InvalidOperationException("Expected entry scroll offset to return to the top.");
            }

            if (FishingFilterUiState.TryConsumeNestedScroll(mouse, 120))
            {
                throw new InvalidOperationException("Expected wheel at the top edge to bubble to the main window.");
            }

            FishingFilterUiState.Reset();
        }

        private static void LegacyUiWindowCaptureAcceptsScaledScreenCoordinates()
        {
            LegacyMainUiState.EnsureLoaded();
            var window = LegacyMainUiState.WindowRect;
            Terraria.Main.screenWidth = 1536;
            Terraria.Main.screenHeight = 864;
            var effectiveScale = (864d - LegacyUiMetrics.VisualScreenMargin) / LegacyUiMetrics.DefaultHeight;
            var raw = new DiagnosticMouseState
            {
                UiScaleAvailable = true,
                UiScaleMatrixAvailable = true,
                UiScale = 1.25d,
                UiScaleX = 1.25d,
                UiScaleY = 1.25d,
                OsReadAvailable = true,
                OsClientMouseX = (int)Math.Round((window.X + 20) * effectiveScale),
                OsClientMouseY = (int)Math.Round((window.Y + 20) * effectiveScale)
            };

            if (!LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected screen-fit capped 125% OS coordinates over the visual F5 window to count as captured.");
            }

            raw.OsClientMouseX = (int)((window.X + 20) * 1.25d);
            raw.OsClientMouseY = (int)((window.Bottom - 10) * 1.25d);
            if (LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected old uncapped 125% bottom coordinates outside the capped visual F5 window not to count as captured.");
            }

            Terraria.Main.screenWidth = 2560;
            Terraria.Main.screenHeight = 1440;
            raw.UiScale = 1.3d;
            raw.UiScaleX = 1.3d;
            raw.UiScaleY = 1.3d;
            raw.OsClientMouseX = (int)Math.Round((window.X + 20) * 1.3d);
            raw.OsClientMouseY = (int)Math.Round((window.Y + 20) * 1.3d);
            if (!LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected 2560x1440 at 130% OS coordinates to keep following Terraria UI scale.");
            }

            raw.UiScale = 0.8d;
            raw.UiScaleX = 0.8d;
            raw.UiScaleY = 0.8d;
            raw.OsClientMouseX = (int)((window.X + 20) * 0.8d);
            raw.OsClientMouseY = (int)((window.Y + 20) * 0.8d);
            if (!LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected sub-100% OS coordinates to keep following Terraria UI scale.");
            }
        }
    }
}
