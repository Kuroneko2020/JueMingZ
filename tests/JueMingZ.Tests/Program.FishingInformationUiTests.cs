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

        private static void InformationChestLabelsFrameLimitAllowsDenseRooms()
        {
            if (InformationOverlayService.MaxChestLabelsPerFrameForTesting() < 240)
            {
                throw new InvalidOperationException("Expected chest label frame limit to support dense chest rooms beyond the old 80-label cap.");
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
            RuntimePerformanceDiagnostics.Record(8d, 9d, 1d, 2d, 3d, "stage-a", 4d, "dispatch.service-a", 5d);
            if (!string.Equals(RuntimePerformanceDiagnostics.LastSlowestOperationName, "dispatch.service-a", StringComparison.Ordinal) ||
                Math.Abs(RuntimePerformanceDiagnostics.LastSlowestOperationElapsedMs - 5d) > 0.001d)
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
            var raw = new DiagnosticMouseState
            {
                UiScaleAvailable = true,
                UiScaleMatrixAvailable = true,
                UiScale = 1.5d,
                UiScaleX = 1.5d,
                UiScaleY = 1.5d,
                OsReadAvailable = true,
                OsClientMouseX = (int)((window.X + 20) * 1.5d),
                OsClientMouseY = (int)((window.Y + 20) * 1.5d)
            };

            if (!LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected scaled OS coordinates over the visual F5 window to count as captured.");
            }

            raw.OsClientMouseX = (int)((window.Right + 50) * 1.5d);
            raw.OsClientMouseY = (int)((window.Bottom + 50) * 1.5d);
            if (LegacyUiInput.IsMouseInWindowForDiagnostics(raw))
            {
                throw new InvalidOperationException("Expected scaled OS coordinates outside the visual F5 window not to count as captured.");
            }
        }
    }
}
