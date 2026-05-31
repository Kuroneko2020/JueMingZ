using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Npcs;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Ui;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void ChannelResolverUnknownActionDefaultsGlobalExclusive()
        {
            var profile = InputActionChannelResolver.Resolve(new InputActionRequest
            {
                Kind = (InputActionKind)999,
                SourceFeatureId = "test.unknown"
            });

            AssertHas(profile.RequiredChannels, InputActionChannel.GlobalExclusive, "unknown required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "unknown conflicts");
        }

        private static void ChannelResolverDiagnosticNoopUsesNoChannel()
        {
            var profile = InputActionChannelResolver.Resolve(InputActionRequest.CreateDiagnosticNoop("test", "noop"));
            if (profile.RequiredChannels != InputActionChannel.None || profile.GlobalExclusive)
            {
                throw new InvalidOperationException("Expected DiagnosticNoop to require no channel.");
            }
        }

        private static void ChannelResolverUseHotbarItemUsesItemAndHotbar()
        {
            var profile = InputActionChannelResolver.Resolve(new InputActionRequest { Kind = InputActionKind.UseHotbarItem });
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "use hotbar required");
            AssertHas(profile.RequiredChannels, InputActionChannel.HotbarSelection, "use hotbar required");
            AssertHas(profile.RequiredChannels, InputActionChannel.BridgeItemUse, "use hotbar required");
        }

        private static void ChannelResolverInventorySlotConflictsWithUseItem()
        {
            var profile = InputActionChannelResolver.Resolve(new InputActionRequest { Kind = InputActionKind.InventorySlot });
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "inventory required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "inventory conflicts");
        }

        private static void ChannelResolverSafeLandingQuickMount()
        {
            var request = new InputActionRequest { Kind = InputActionKind.Jump };
            request.Metadata["JumpMode"] = "SafeLandingTakeover";
            request.Metadata["SafeLandingActionType"] = "quick_mount";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.Jump, "quick mount required");
            AssertHas(profile.RequiredChannels, InputActionChannel.QuickMount, "quick mount required");
        }

        private static void ChannelResolverSafeLandingGravityFlip()
        {
            var request = new InputActionRequest { Kind = InputActionKind.Jump };
            request.Metadata["JumpMode"] = "SafeLandingTakeover";
            request.Metadata["SafeLandingActionType"] = "gravity_flip";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.Jump, "gravity flip required");
            AssertHas(profile.RequiredChannels, InputActionChannel.GravityFlip, "gravity flip required");
        }

        private static void ChannelResolverSafeLandingGrapple()
        {
            var request = new InputActionRequest { Kind = InputActionKind.Jump };
            request.Metadata["JumpMode"] = "SafeLandingTakeover";
            request.Metadata["SafeLandingActionType"] = "grapple";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.Jump, "grapple required");
            AssertHas(profile.RequiredChannels, InputActionChannel.Grapple, "grapple required");
            AssertHas(profile.RequiredChannels, InputActionChannel.MouseTarget, "grapple required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "grapple conflicts");
        }

        private static void ChannelResolverDashUsesDashAndDirection()
        {
            var profile = InputActionChannelResolver.Resolve(new InputActionRequest { Kind = InputActionKind.Dash });
            AssertHas(profile.RequiredChannels, InputActionChannel.Dash, "dash required");
            AssertHas(profile.RequiredChannels, InputActionChannel.Direction, "dash required");
        }

        private static void ChannelResolverMagicStringUsesPulseBridge()
        {
            var request = new InputActionRequest { Kind = InputActionKind.RawInput };
            request.Metadata["Scenario"] = "Combat.MagicStringClicker";
            request.Metadata["RawInputMode"] = "MagicStringClicker";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "magic string required");
            AssertHas(profile.RequiredChannels, InputActionChannel.RawInput, "magic string required");
            AssertHas(profile.RequiredChannels, InputActionChannel.BridgeUseItemPulse, "magic string required");
        }

        private static void ChannelResolverAutoHarvestSustainedUse()
        {
            var request = new InputActionRequest { Kind = InputActionKind.RawInput };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoHarvest;
            request.Metadata[ActionMetadataKeys.RawInputMode] = "AutoHarvestSustainedUse";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "auto harvest required");
            AssertHas(profile.RequiredChannels, InputActionChannel.MouseTarget, "auto harvest required");
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "auto harvest required");
            AssertHas(profile.RequiredChannels, InputActionChannel.HotbarSelection, "auto harvest required");
            AssertHas(profile.RequiredChannels, InputActionChannel.RawInput, "auto harvest required");
        }

        private static void ChannelResolverAutoCaptureCritterSustainedUse()
        {
            var request = new InputActionRequest { Kind = InputActionKind.RawInput };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoCaptureCritter;
            request.Metadata[ActionMetadataKeys.RawInputMode] = "AutoCaptureCritterSustainedUse";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "auto capture required");
            AssertHas(profile.RequiredChannels, InputActionChannel.MouseTarget, "auto capture required");
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "auto capture required");
            AssertHas(profile.RequiredChannels, InputActionChannel.HotbarSelection, "auto capture required");
            AssertHas(profile.RequiredChannels, InputActionChannel.RawInput, "auto capture required");
        }

        private static void ChannelResolverShopUsesNpcAndInventory()
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Shop,
                SourceFeatureId = FeatureIds.InventoryAutoSell
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryAutoSell;

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.NpcInteraction, "shop required");
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "shop required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "shop conflicts");
        }

        private static void ChannelResolverTrashSlotUsesInventory()
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.TrashSlot,
                SourceFeatureId = FeatureIds.InventoryAutoDiscard
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryAutoDiscard;

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "trash slot required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "trash slot conflicts");
        }

        private static void ChannelResolverReforgeUsesNpcAndInventory()
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Reforge,
                SourceFeatureId = FeatureIds.NpcAutoReforge
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.NpcQuickReforge;

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.NpcInteraction, "reforge required");
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "reforge required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "reforge conflicts");
        }

        private static void QuickRenameIncrementsTrailingNumericSuffix()
        {
            AssertStringEquals(PlayerRenameCompat.BuildIncrementedNameForTesting("孙笑川"), "孙笑川1", "plain name");
            AssertStringEquals(PlayerRenameCompat.BuildIncrementedNameForTesting("孙笑川1"), "孙笑川2", "single digit suffix");
            AssertStringEquals(PlayerRenameCompat.BuildIncrementedNameForTesting("孙笑川9"), "孙笑川10", "carry suffix");
            AssertStringEquals(PlayerRenameCompat.BuildIncrementedNameForTesting("Test009"), "Test010", "padded suffix");
        }

        private static void AutoStackDetectsOnlyIncreasedItemTypes()
        {
            var previous = new Dictionary<int, int>
            {
                { 10, 3 },
                { 20, 8 },
                { 30, 1 }
            };
            var current = new Dictionary<int, int>
            {
                { 10, 4 },
                { 20, 7 },
                { 30, 1 },
                { 40, 2 }
            };

            var increased = AutoStackService.FindIncreasedItemTypesForTesting(previous, current);
            if (increased.Count != 2 || increased[0] != 10 || increased[1] != 40)
            {
                throw new InvalidOperationException("Expected only increased item types 10 and 40.");
            }
        }

        private static void AutoStackIgnoresFavoriteToggleAndUnstackableMoves()
        {
            var favoriteToggle = AutoStackService.FindPickupIncreasedItemTypesForTesting(
                new Dictionary<int, int> { { 99, 20 } },
                new Dictionary<int, int> { { 99, 20 } },
                new HashSet<int> { 99 });
            if (favoriteToggle.Count != 0)
            {
                throw new InvalidOperationException("Auto stack should not treat manual unfavorite as a picked-up item.");
            }

            var equipmentMove = AutoStackService.FindPickupIncreasedItemTypesForTesting(
                new Dictionary<int, int>(),
                new Dictionary<int, int> { { 5000, 1 } },
                new HashSet<int>());
            if (equipmentMove.Count != 0)
            {
                throw new InvalidOperationException("Auto stack should not treat unstackable equipment entering inventory as picked-up stackable loot.");
            }

            var pickedStackable = AutoStackService.FindPickupIncreasedItemTypesForTesting(
                new Dictionary<int, int> { { 12, 3 } },
                new Dictionary<int, int> { { 12, 4 } },
                new HashSet<int> { 12 });
            if (pickedStackable.Count != 1 || pickedStackable[0] != 12)
            {
                throw new InvalidOperationException("Auto stack should still detect picked-up stackable item increases.");
            }
        }

        private static void AutoStackFinalSignatureExcludesUnstackableItems()
        {
            string signature;
            List<int> slots;
            int slotCount;
            int stackTotal;
            var mixedSnapshot = new GameStateSnapshot
            {
                IsInWorld = true
            };
            mixedSnapshot.Inventory.Items = new List<InventoryItemSnapshot>
            {
                BuildEquipmentItemWithMisleadingMaxStack(0, 4954, "Celestial Starboard"),
                BuildInventoryItem(1, 12, "铜矿", 4, 0, false)
            };

            if (!AutoStackService.TryBuildInventoryItemSignatureForTesting(mixedSnapshot, new[] { 4954, 12 }, out signature, out slots, out slotCount, out stackTotal) ||
                slotCount != 1 ||
                stackTotal != 4 ||
                slots.Count != 1 ||
                slots[0] != 1 ||
                !string.Equals(signature, "1:12x4", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto stack final signature should only include stackable pending items.");
            }

            var unstackableOnlySnapshot = new GameStateSnapshot
            {
                IsInWorld = true
            };
            unstackableOnlySnapshot.Inventory.Items = new List<InventoryItemSnapshot>
            {
                BuildEquipmentItemWithMisleadingMaxStack(0, 4954, "Celestial Starboard")
            };

            if (!AutoStackService.TryBuildInventoryItemSignatureForTesting(unstackableOnlySnapshot, new[] { 4954 }, out signature, out slots, out slotCount, out stackTotal) ||
                slotCount != 0 ||
                stackTotal != 0 ||
                slots.Count != 0 ||
                !string.IsNullOrEmpty(signature))
            {
                throw new InvalidOperationException("Auto stack should not build a transfer signature for unstackable equipment.");
            }
        }

        private static void AutoStackRequestUsesChestSelectiveQuickStackMetadata()
        {
            var request = AutoStackService.BuildAutoStackRequestForTesting(
                new[] { 12, 99 },
                new[] { 3, 11 },
                "3:12x2|11:99x1",
                2,
                3);

            if (request.Kind != InputActionKind.Chest)
            {
                throw new InvalidOperationException("Expected auto stack to use Chest action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.InventoryAutoStack, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoStack);
            AssertMetadata(request, "AutoStackItemIds", "12,99");
            AssertMetadata(request, "AutoStackInventorySlots", "3,11");
            AssertMetadata(request, "InventorySignature", "3:12x2|11:99x1");
            AssertMetadata(request, "MovableSlotCount", "2");
            AssertMetadata(request, "MovableStackTotal", "3");
            AssertMetadata(request, "AllowPlayerInventoryOpen", "true");
        }

        private static void AutoStackAllowsPlayerInventoryOpen()
        {
            var snapshot = new GameStateSnapshot
            {
                IsInWorld = true
            };
            snapshot.Ui.PlayerInventoryOpen = true;

            if (AutoStackService.IsExecutionBlockedForTesting(snapshot, 100))
            {
                throw new InvalidOperationException("Auto stack service should allow player inventory to stay open.");
            }

            if (ChestActionExecutor.IsBlockedForSelectiveQuickStackForTesting(snapshot, true))
            {
                throw new InvalidOperationException("Auto stack chest executor should allow player inventory to stay open.");
            }
        }

        private static void AutoStackStillBlocksChestUi()
        {
            var snapshot = new GameStateSnapshot
            {
                IsInWorld = true
            };
            snapshot.Ui.PlayerInventoryOpen = true;
            snapshot.Ui.ChestOpen = true;

            if (!AutoStackService.IsExecutionBlockedForTesting(snapshot, 100))
            {
                throw new InvalidOperationException("Auto stack service should still block while a chest UI is open.");
            }

            if (!ChestActionExecutor.IsBlockedForSelectiveQuickStackForTesting(snapshot, true))
            {
                throw new InvalidOperationException("Auto stack chest executor should still block while a chest UI is open.");
            }
        }

        private static void AutoStackUsesShortInventoryOpenSettleWindow()
        {
            if (!AutoStackService.IsInventoryOpenSettlePendingForTesting(10, 10))
            {
                throw new InvalidOperationException("Auto stack should wait immediately after an inventory-open stack change.");
            }

            if (AutoStackService.IsInventoryOpenSettlePendingForTesting(13, 10))
            {
                throw new InvalidOperationException("Auto stack inventory-open settle window should be short and bounded.");
            }
        }

        private static void AutoSellDefaultListIsConservativeFishingJunk()
        {
            var normalized = AutoSellService.NormalizeAutoSellItemIdsForTesting(null);
            if (normalized.Count != 3 ||
                normalized[0] != 2337 ||
                normalized[1] != 2338 ||
                normalized[2] != 2339)
            {
                throw new InvalidOperationException("Expected default auto sell list to be Old Shoe, Fishing Seaweed, and Tin Can.");
            }

            var custom = AutoSellService.NormalizeAutoSellItemIdsForTesting(new[] { 2337, 71, 2337, -1, 2339 });
            if (custom.Count != 2 || custom[0] != 2337 || custom[1] != 2339)
            {
                throw new InvalidOperationException("Expected auto sell list normalization to remove coins, invalid ids, and duplicates.");
            }
        }

        private static void AutoSellRequestUsesShopMetadata()
        {
            var request = AutoSellService.BuildAutoSellRequestForTesting(
                new AutoSellShopTarget
                {
                    NpcIndex = 4,
                    NpcType = 17,
                    ShopIndex = 1,
                    Name = "Merchant"
                },
                new[]
                {
                    new AutoSellInventoryCandidate { Slot = 8, ItemType = 2337, ItemName = "Old Shoe", Stack = 2 },
                    new AutoSellInventoryCandidate { Slot = 9, ItemType = 2339, ItemName = "Tin Can", Stack = 1 }
                },
                "8:2337x2|9:2339x1");

            if (request.Kind != InputActionKind.Shop)
            {
                throw new InvalidOperationException("Expected auto sell to use Shop action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.InventoryAutoSell, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoSell);
            AssertMetadata(request, "NpcIndex", "4");
            AssertMetadata(request, "NpcType", "17");
            AssertMetadata(request, "ShopIndex", "1");
            AssertMetadata(request, "AutoSellItemIds", "2337,2339");
            AssertMetadata(request, "AutoSellInventorySlots", "8,9");
            AssertMetadata(request, "InventorySignature", "8:2337x2|9:2339x1");
            AssertMetadata(request, "SellSlotCount", "2");
            AssertMetadata(request, "SellStackTotal", "3");
            AssertMetadata(request, "AllowPlayerInventoryOpen", "true");
        }

        private static void AutoSellAllowsPlayerInventoryOpen()
        {
            var snapshot = new GameStateSnapshot
            {
                IsInWorld = true
            };
            snapshot.Ui.PlayerInventoryOpen = true;

            var reason = AutoSellService.GetExecutionBlockedReasonForTesting(snapshot, false);
            if (!string.IsNullOrEmpty(reason))
            {
                throw new InvalidOperationException("Auto sell service should allow player inventory to stay open, got: " + reason);
            }
        }

        private static void AutoSellCandidatesUseInventorySnapshot()
        {
            var snapshot = new GameStateSnapshot
            {
                Inventory = new InventorySnapshot
                {
                    Items = new List<InventoryItemSnapshot>
                    {
                        new InventoryItemSnapshot { SlotIndex = 0, Type = 2337, Name = "Old Shoe", Stack = 2 },
                        new InventoryItemSnapshot { SlotIndex = 1, Type = 2338, Name = "Seaweed", Stack = 1, Favorited = true },
                        new InventoryItemSnapshot { SlotIndex = 2, Type = 71, Name = "Copper Coin", Stack = 10 },
                        new InventoryItemSnapshot { SlotIndex = 3, Type = 2339, Name = "Tin Can", Stack = 1 }
                    }
                }
            };

            List<AutoSellInventoryCandidate> candidates;
            string signature;
            string message;
            if (!AutoSellService.TryFindSellableInventoryCandidatesForTesting(
                    snapshot,
                    new HashSet<int> { 71, 2337, 2338, 2339 },
                    out candidates,
                    out signature,
                    out message))
            {
                throw new InvalidOperationException("Expected auto sell snapshot candidate scan to succeed: " + message);
            }

            if (candidates.Count != 2 ||
                candidates[0].Slot != 0 ||
                candidates[0].ItemType != 2337 ||
                candidates[1].Slot != 3 ||
                candidates[1].ItemType != 2339)
            {
                throw new InvalidOperationException("Expected auto sell snapshot scan to include only non-favorited non-coin matching items.");
            }

            AssertStringEquals(signature, "0:2337x2|3:2339x1", "auto sell inventory signature");
        }

        private static void AutoDiscardDefaultListIsEmpty()
        {
            var normalized = AutoDiscardService.NormalizeAutoDiscardItemIdsForTesting(null);
            if (normalized.Count != 0)
            {
                throw new InvalidOperationException("Expected default auto discard list to be empty.");
            }

            var custom = AutoDiscardService.NormalizeAutoDiscardItemIdsForTesting(new[] { 12, 71, 12, -1, 99 });
            if (custom.Count != 2 || custom[0] != 12 || custom[1] != 99)
            {
                throw new InvalidOperationException("Expected auto discard list normalization to remove coins, invalid ids, and duplicates.");
            }
        }

        private static void AutoDiscardRequestUsesTrashMetadata()
        {
            var request = AutoDiscardService.BuildAutoDiscardRequestForTesting(
                new[]
                {
                    new AutoDiscardInventoryCandidate { Slot = 8, ItemType = 12, ItemName = "Copper Ore", Stack = 2 },
                    new AutoDiscardInventoryCandidate { Slot = 9, ItemType = 99, ItemName = "Torch", Stack = 3 }
                },
                "8:12x2|9:99x3");

            if (request.Kind != InputActionKind.TrashSlot)
            {
                throw new InvalidOperationException("Expected auto discard to use TrashSlot action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.InventoryAutoDiscard, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoDiscard);
            AssertMetadata(request, "AutoDiscardItemIds", "12,99");
            AssertMetadata(request, "AutoDiscardInventorySlots", "8,9");
            AssertMetadata(request, "InventorySignature", "8:12x2|9:99x3");
            AssertMetadata(request, "DiscardSlotCount", "2");
            AssertMetadata(request, "DiscardStackTotal", "5");
            AssertMetadata(request, "AllowPlayerInventoryOpen", "true");
        }

        private static void AutoDiscardAllowsPlayerInventoryOpen()
        {
            var snapshot = new GameStateSnapshot
            {
                IsInWorld = true,
                Player = new PlayerStateSnapshot
                {
                    Exists = true,
                    Active = true
                }
            };
            snapshot.Ui.PlayerInventoryOpen = true;

            if (AutoDiscardService.IsExecutionBlockedForTesting(snapshot, false))
            {
                throw new InvalidOperationException("Auto discard service should allow player inventory to stay open.");
            }
        }

        private static void AutoDiscardCandidatesUseInventorySnapshot()
        {
            var snapshot = new GameStateSnapshot
            {
                Inventory = new InventorySnapshot
                {
                    Items = new List<InventoryItemSnapshot>
                    {
                        new InventoryItemSnapshot { SlotIndex = 0, Type = 12, Name = "Copper Ore", Stack = 2 },
                        new InventoryItemSnapshot { SlotIndex = 1, Type = 99, Name = "Torch", Stack = 3, Favorited = true },
                        new InventoryItemSnapshot { SlotIndex = 2, Type = 99, Name = "Torch", Stack = 4 },
                        new InventoryItemSnapshot { SlotIndex = 3, Type = 72, Name = "Silver Coin", Stack = 5 }
                    }
                }
            };

            List<AutoDiscardInventoryCandidate> candidates;
            string signature;
            string message;
            if (!AutoDiscardService.TryFindDiscardableInventoryCandidatesForTesting(
                    snapshot,
                    new HashSet<int> { 12, 72, 99 },
                    out candidates,
                    out signature,
                    out message))
            {
                throw new InvalidOperationException("Expected auto discard snapshot candidate scan to succeed: " + message);
            }

            if (candidates.Count != 2 ||
                candidates[0].Slot != 0 ||
                candidates[0].ItemType != 12 ||
                candidates[1].Slot != 2 ||
                candidates[1].ItemType != 99)
            {
                throw new InvalidOperationException("Expected auto discard snapshot scan to include only non-favorited non-coin matching items.");
            }

            AssertStringEquals(signature, "0:12x2|2:99x4", "auto discard inventory signature");
        }

        private static void QuickBagOpenRequestUsesInventorySlotMetadata()
        {
            var request = QuickBagOpenService.BuildRequestForTesting(11, 4405, "宝匣");
            if (request.Kind != InputActionKind.InventorySlot)
            {
                throw new InvalidOperationException("Expected quick bag open to use InventorySlot action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.InventoryQuickBagOpen, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.InventoryQuickBagOpen);
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "11");
            AssertMetadata(request, "QuickBagOpenItemType", "4405");
            AssertMetadata(request, "QuickBagOpenItemName", "宝匣");
            AssertMetadata(request, "QuickBagOpenRepeatCount", "8");
        }

        private static void QuickBagOpenYieldsAfterBatchWhenCleanupEnabled()
        {
            QuickBagOpenService.ClearState("test reset");
            QuickBagOpenService.BeginCleanupYieldForTesting(100, true);
            if (!QuickBagOpenService.IsCleanupYieldActiveForTesting(100) ||
                !QuickBagOpenService.IsCleanupYieldActiveForTesting(124))
            {
                throw new InvalidOperationException("Quick bag open should yield for a bounded cleanup window after a batch.");
            }

            if (QuickBagOpenService.IsCleanupYieldActiveForTesting(125))
            {
                throw new InvalidOperationException("Quick bag open cleanup yield window should expire after its bounded window.");
            }

            QuickBagOpenService.BeginCleanupYieldForTesting(200, false);
            if (QuickBagOpenService.IsCleanupYieldActiveForTesting(200))
            {
                throw new InvalidOperationException("Quick bag open should not yield when cleanup automation is disabled.");
            }
        }

        private static void AutoDepositCoinsRequestUsesChestMetadata()
        {
            var request = AutoDepositCoinsService.BuildAutoDepositCoinsRequestForTesting(
                new[] { 71, 72, 74 },
                new[] { 0, 3, 15 },
                "0:71x88|3:72x32|15:74x2",
                3,
                122);

            if (request.Kind != InputActionKind.Chest)
            {
                throw new InvalidOperationException("Expected auto deposit coins to use Chest action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.InventoryAutoDepositCoins, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoDepositCoins);
            AssertMetadata(request, "AutoDepositCoinItemIds", "71,72,74");
            AssertMetadata(request, "AutoDepositCoinInventorySlots", "0,3,15");
            AssertMetadata(request, "InventorySignature", "0:71x88|3:72x32|15:74x2");
            AssertMetadata(request, "MovableSlotCount", "3");
            AssertMetadata(request, "MovableStackTotal", "122");
            AssertMetadata(request, "AllowPlayerInventoryOpen", "true");
            AssertMetadata(request, "AutoDepositCoinsTransferPath", "ChestUI.MoveCoinsNearbyBanks");
            AssertMetadata(request, "AutoDepositCoinsEmptyBankFallback", "PiggyBankFirstCoin");
        }

        private static void AutoDepositCoinsCandidatesUseInventorySnapshot()
        {
            var snapshot = new GameStateSnapshot
            {
                Inventory = new InventorySnapshot
                {
                    Items = new List<InventoryItemSnapshot>
                    {
                        new InventoryItemSnapshot { SlotIndex = 0, Type = 71, Name = "Copper Coin", Stack = 88 },
                        new InventoryItemSnapshot { SlotIndex = 1, Type = 72, Name = "Silver Coin", Stack = 5, Favorited = true },
                        new InventoryItemSnapshot { SlotIndex = 3, Type = 72, Name = "Silver Coin", Stack = 32 },
                        new InventoryItemSnapshot { SlotIndex = 15, Type = 74, Name = "Platinum Coin", Stack = 2 },
                        new InventoryItemSnapshot { SlotIndex = 16, Type = 9, Name = "Wood", Stack = 99 }
                    }
                }
            };

            List<int> coinItemIds;
            List<int> slots;
            string signature;
            int slotCount;
            int stackTotal;
            string message;
            if (!AutoDepositCoinsService.TryFindCoinInventorySlotsForTesting(
                    snapshot,
                    out coinItemIds,
                    out slots,
                    out signature,
                    out slotCount,
                    out stackTotal,
                    out message))
            {
                throw new InvalidOperationException("Expected auto deposit coins snapshot scan to succeed: " + message);
            }

            if (coinItemIds.Count != 3 ||
                coinItemIds[0] != 71 ||
                coinItemIds[1] != 72 ||
                coinItemIds[2] != 74)
            {
                throw new InvalidOperationException("Expected auto deposit coins scan to keep unique non-favorited coin types only.");
            }

            if (slots.Count != 3 ||
                slots[0] != 0 ||
                slots[1] != 3 ||
                slots[2] != 15)
            {
                throw new InvalidOperationException("Expected auto deposit coins scan to keep only non-favorited coin slots.");
            }

            AssertStringEquals(signature, "0:71x88|3:72x32|15:74x2", "auto deposit coins inventory signature");
            if (slotCount != 3 || stackTotal != 122)
            {
                throw new InvalidOperationException("Expected auto deposit coins slot/stack totals to match selected coin slots.");
            }
        }

        private static void AutoExtractinatorRequestUsesItemUseMetadata()
        {
            var request = AutoExtractinatorService.BuildRequestForTesting(18, 3272, "沙漠化石", 120, 233, 219);
            if (request.Kind != InputActionKind.ItemUse)
            {
                throw new InvalidOperationException("Expected auto extractinator to use ItemUse action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.InventoryAutoExtractinator, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoExtractinator);
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "18");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "1928");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "3736");
            AssertMetadata(request, "ApplyMainMouseLeftForItemCheck", "true");
            AssertMetadata(request, "AllowEarlyItemCheck", "true");
            AssertMetadata(request, "EarlyItemCheckWindowTicks", "2");
            AssertMetadata(request, "AutoExtractinatorTileX", "120");
            AssertMetadata(request, "AutoExtractinatorTileY", "233");
            AssertMetadata(request, "AutoExtractinatorTileType", "219");
            AssertMetadata(request, "AutoExtractinatorItemType", "3272");
            AssertMetadata(request, "AutoExtractinatorItemName", "沙漠化石");
            AssertHas(request.RequiredChannels, InputActionChannel.UseItem, "auto extractinator required");
            AssertHas(request.RequiredChannels, InputActionChannel.MouseTarget, "auto extractinator required");
            AssertHas(request.RequiredChannels, InputActionChannel.InventorySlot, "auto extractinator required");
            AssertHas(request.RequiredChannels, InputActionChannel.HotbarSelection, "auto extractinator required");
            AssertHas(request.RequiredChannels, InputActionChannel.BridgeItemUse, "auto extractinator required");
        }

        private static void KeepFavoritedRequestUsesInventorySlotMetadata()
        {
            var request = KeepFavoritedService.BuildRequestForTesting(6, 75, "75|0|生命水晶");
            if (request.Kind != InputActionKind.InventorySlot)
            {
                throw new InvalidOperationException("Expected keep favorited to use InventorySlot action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.InventoryKeepFavorited, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.InventoryKeepFavorited);
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "6");
            AssertMetadata(request, "KeepFavoritedItemType", "75");
            AssertMetadata(request, "KeepFavoritedSignature", "75|0|生命水晶");
        }

        private static void KeepFavoritedManualUnfavoriteClearsTracking()
        {
            KeepFavoritedCompat.ClearState();
            int slot;
            int itemType;
            string signature;
            string message;
            var tracked = BuildKeepFavoritedSnapshot(
                true,
                new[] { BuildInventoryItem(6, 75, "生命水晶", 1, 0, true) },
                null);
            KeepFavoritedCompat.TryFindLostFavoritedSlot(tracked, 10, out slot, out itemType, out signature, out message);

            var unfavorited = BuildKeepFavoritedSnapshot(
                true,
                new[] { BuildInventoryItem(6, 75, "生命水晶", 1, 0, false) },
                null);
            if (KeepFavoritedCompat.TryFindLostFavoritedSlot(unfavorited, 12, out slot, out itemType, out signature, out message))
            {
                throw new InvalidOperationException("Keep favorited should allow a manual unfavorite while inventory is open.");
            }

            KeepFavoritedCompat.ClearState();
        }

        private static void KeepFavoritedRestoresTrashRoundTrip()
        {
            KeepFavoritedCompat.ClearState();
            int slot;
            int itemType;
            string signature;
            string message;
            var tracked = BuildKeepFavoritedSnapshot(
                true,
                new[] { BuildInventoryItem(6, 75, "生命水晶", 1, 0, true) },
                null);
            KeepFavoritedCompat.TryFindLostFavoritedSlot(tracked, 20, out slot, out itemType, out signature, out message);

            var inTrash = BuildKeepFavoritedSnapshot(
                true,
                new InventoryItemSnapshot[0],
                BuildInventoryItem(-2, 75, "生命水晶", 1, 0, false));
            KeepFavoritedCompat.TryFindLostFavoritedSlot(inTrash, 22, out slot, out itemType, out signature, out message);

            var backInInventory = BuildKeepFavoritedSnapshot(
                true,
                new[] { BuildInventoryItem(8, 75, "生命水晶", 1, 0, false) },
                null);
            if (!KeepFavoritedCompat.TryFindLostFavoritedSlot(backInInventory, 24, out slot, out itemType, out signature, out message) ||
                slot != 8 ||
                itemType != 75 ||
                signature != "75|0|生命水晶")
            {
                throw new InvalidOperationException("Keep favorited should restore a tracked favorite item after a trash slot round trip.");
            }

            KeepFavoritedCompat.ClearState();
        }

        private static GameStateSnapshot BuildKeepFavoritedSnapshot(bool inventoryOpen, IReadOnlyList<InventoryItemSnapshot> items, InventoryItemSnapshot trashItem)
        {
            var snapshot = new GameStateSnapshot
            {
                IsInWorld = true
            };
            snapshot.Ui.PlayerInventoryOpen = inventoryOpen;
            snapshot.Inventory.Items = items ?? new List<InventoryItemSnapshot>();
            snapshot.Inventory.TrashItem = trashItem ?? new InventoryItemSnapshot { SlotIndex = -2 };
            return snapshot;
        }

        private static InventoryItemSnapshot BuildInventoryItem(int slot, int type, string name, int stack, int prefix, bool favorited)
        {
            return new InventoryItemSnapshot
            {
                SlotIndex = slot,
                Type = type,
                Name = name,
                Stack = stack,
                MaxStack = stack > 1 ? 999 : 1,
                Prefix = prefix,
                Favorited = favorited
            };
        }

        private static InventoryItemSnapshot BuildEquipmentItemWithMisleadingMaxStack(int slot, int type, string name)
        {
            return new InventoryItemSnapshot
            {
                SlotIndex = slot,
                Type = type,
                Name = name,
                Stack = 1,
                MaxStack = 999,
                Accessory = true,
                WingSlot = 1
            };
        }

        private static void QuickReforgePrefixesNormalizeBlanksAndDuplicates()
        {
            var normalized = QuickReforgeService.NormalizeTargetPrefixesForTesting(null);
            if (normalized.Count != 0)
            {
                throw new InvalidOperationException("Expected quick reforge default prefix list to be empty.");
            }

            var custom = QuickReforgeService.NormalizeTargetPrefixesForTesting(new[] { "  神话  ", "神话", string.Empty, "  ", "虚幻" });
            if (custom.Count != 2 || custom[0] != "神话" || custom[1] != "虚幻")
            {
                throw new InvalidOperationException("Expected quick reforge prefix normalization to trim and deduplicate entries.");
            }
        }

        private static void QuickReforgePrefixMatchingAcceptsFullAffixNames()
        {
            string matched;
            if (!ReforgeCompat.TryMatchTargetPrefixText(new[] { "虚幻" }, "虚幻 代达罗斯风暴弓", out matched) ||
                matched != "虚幻")
            {
                throw new InvalidOperationException("Expected quick reforge prefix matching to accept Chinese full affix names.");
            }

            if (!ReforgeCompat.TryMatchTargetPrefixText(new[] { "Unreal" }, "Unreal Daedalus Stormbow", out matched) ||
                !string.Equals(matched, "Unreal", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected quick reforge prefix matching to accept English full affix names.");
            }

            if (!ReforgeCompat.TryMatchTargetPrefixText(new[] { "传奇" }, "传奇", out matched) ||
                matched != "传奇")
            {
                throw new InvalidOperationException("Expected quick reforge prefix matching to accept exact prefix names.");
            }

            if (ReforgeCompat.TryMatchTargetPrefixText(new[] { "虚幻" }, "虚幻弓", out matched))
            {
                throw new InvalidOperationException("Expected quick reforge prefix matching to require a prefix token boundary.");
            }
        }

        private static void QuickReforgeRequestUsesReforgeMetadata()
        {
            var request = QuickReforgeService.BuildQuickReforgeRequestForTesting(
                new[] { "神话", "虚幻" },
                "强力");

            if (request.Kind != InputActionKind.Reforge)
            {
                throw new InvalidOperationException("Expected quick reforge to use Reforge action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.NpcAutoReforge, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.NpcQuickReforge);
            AssertMetadata(request, "TargetPrefixes", "神话,虚幻");
            AssertMetadata(request, "CurrentAffix", "强力");
        }

        private static void FeatureCatalogExposesAutoDiscard()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.InventoryAutoDiscard, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected auto discard feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Auto discard must be visible and implemented.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.ListConfigWindow)
            {
                throw new InvalidOperationException("Auto discard must use list config UI metadata.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.InventoryAndItems ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Auto discard must stay InventoryAndItems code-domain and Misc UI category.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Auto discard must use original-action multiplayer metadata.");
            }
        }

        private static void FeatureCatalogExposesQuickReforge()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.NpcAutoReforge, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected quick reforge feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Quick reforge must be visible and implemented.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.ListConfigWindow)
            {
                throw new InvalidOperationException("Quick reforge must use list config UI metadata.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.NpcServices ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Quick reforge must stay NpcServices code-domain and Misc UI category.");
            }
        }

        private static void FeatureCatalogExposesAutoMining()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.WorldAutomationAutoMining, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected auto mining feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Auto mining must be visible and implemented.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.InlineHotkey)
            {
                throw new InvalidOperationException("Auto mining must use inline hotkey UI metadata.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.WorldAutomation ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Auto mining must stay WorldAutomation code-domain and Misc UI category.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Auto mining must use original-action multiplayer metadata.");
            }
        }

        private static void FeatureCatalogExposesAutoCaptureCritter()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.WorldAutomationAutoCaptureCritter, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected auto capture critter feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Auto capture critter must be visible and implemented.");
            }

            if (!string.Equals(feature.DisplayName, "自动捕捉", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto capture critter display name must be 自动捕捉.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.WorldAutomation ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Auto capture critter must stay WorldAutomation code-domain and Misc UI category.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Auto capture critter must use original-action multiplayer metadata.");
            }
        }

        private static void FeatureCatalogExposesAutoHarvest()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.WorldAutomationAutoHarvest, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected auto harvest feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Auto harvest must be visible and implemented.");
            }

            if (!string.Equals(feature.DisplayName, "自动收获", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto harvest display name must be 自动收获.");
            }

            if (feature.HasHotkey || feature.HotkeyListVisible)
            {
                throw new InvalidOperationException("Auto harvest should be a standard switch, not a hotkey feature.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.WorldAutomation ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Auto harvest must stay WorldAutomation code-domain and Misc UI category.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Auto harvest must use original-action multiplayer metadata.");
            }
        }

        private static void AutoMiningScannerLinksThreeTileGaps()
        {
            var points = new HashSet<string>(StringComparer.Ordinal)
            {
                "10,10",
                "13,10",
                "17,10"
            };

            var scan = AutoMiningVeinScanner.Scan(
                10,
                10,
                7,
                0,
                0,
                32,
                32,
                (x, y, tileType) => tileType == 7 && points.Contains(x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture)));

            if (scan.Tiles.Count != 2 ||
                scan.MinX != 10 ||
                scan.MaxX != 13)
            {
                throw new InvalidOperationException("Auto mining vein scanner should link same-type ore within three tiles and stop past the gap.");
            }
        }

        private static void AutoMiningScannerKeepsInactiveMinedSeedConnectivity()
        {
            var points = new HashSet<string>(StringComparer.Ordinal)
            {
                "10,10",
                "14,10"
            };

            var scan = AutoMiningVeinScanner.Scan(
                12,
                10,
                7,
                0,
                0,
                32,
                32,
                (x, y, tileType) => tileType == 7 && points.Contains(x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture)));

            if (scan.Tiles.Count != 2 ||
                scan.MinX != 10 ||
                scan.MaxX != 14)
            {
                throw new InvalidOperationException("Auto mode should keep both remaining ore sides selected even when the manually mined seed tile itself is gone.");
            }
        }

        private static void AutoMiningSelectedSlotSwitchInterruptsSelection()
        {
            if (!AutoMiningService.IsSelectedSlotInterruptForTesting(2, 3))
            {
                throw new InvalidOperationException("Auto mining should treat a player hotbar switch away from the pickaxe slot as an interrupt.");
            }

            if (AutoMiningService.IsSelectedSlotInterruptForTesting(2, 2))
            {
                throw new InvalidOperationException("Auto mining should keep mining while the recorded pickaxe slot remains selected.");
            }

            if (AutoMiningService.IsSelectedSlotInterruptForTesting(-1, 2) ||
                AutoMiningService.IsSelectedSlotInterruptForTesting(2, -1))
            {
                throw new InvalidOperationException("Auto mining should not interrupt on invalid slot sentinel values.");
            }
        }

        private static void AutoMiningRequestUsesItemUseMetadata()
        {
            var request = AutoMiningService.BuildMiningRequestForTesting(12, 34, 2, "Ctrl+M");
            if (request.Kind != InputActionKind.ItemUse)
            {
                throw new InvalidOperationException("Auto mining must use ItemUse action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.WorldAutomationAutoMining, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoMining);
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "2");
            AssertMetadata(request, ActionMetadataKeys.RequireSelectedSlotUnchanged, "true");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "200");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "552");
            AssertMetadata(request, "ApplyMainMouseLeftForItemCheck", "true");
            AssertMetadata(request, "SourceHotkey", "Ctrl+M");
        }

        private static void AutoCaptureCritterRequestUsesSustainedRawInputMetadata()
        {
            var request = AutoCaptureCritterService.BuildCaptureRequestForTesting(12, 1991, 1, 7, 616, 120.5f, 88.25f, true);
            if (request.Kind != InputActionKind.RawInput)
            {
                throw new InvalidOperationException("Auto capture critter must use sustained RawInput action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.WorldAutomationAutoCaptureCritter, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoCaptureCritter);
            AssertMetadata(request, ActionMetadataKeys.RawInputMode, "AutoCaptureCritterSustainedUse");
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "12");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "120.5");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "88.25");
            AssertMetadata(request, "AutoCaptureCritterAction", "SustainedUse");
            AssertMetadata(request, "AutoCaptureCritterNpcIndex", "7");
            AssertMetadata(request, "AutoCaptureCritterNpcType", "616");
            AssertMetadata(request, "BugNetCatchTool", "1");
            AssertMetadata(request, "FishingProtection", "true");
            AssertMetadata(request, "AutoCaptureCritterMode", AutoCaptureCritterModes.Auto);
        }

        private static void AutoCaptureCritterRangeUsesBugNetReach()
        {
            if (!AutoCaptureCritterService.IsWithinCaptureRangeForTesting(100f, 100f, 136f, 108f, 16, 16, 1))
            {
                throw new InvalidOperationException("Standard bug net reach should include critters intersecting the vanilla-like swing envelope.");
            }

            if (AutoCaptureCritterService.IsWithinCaptureRangeForTesting(100f, 100f, 154f, 108f, 16, 16, 1))
            {
                throw new InvalidOperationException("Auto capture critter must not swing before the bug net envelope can actually reach the critter.");
            }

            if (AutoCaptureCritterService.IsWithinCaptureRangeForTesting(100f, 100f, 148f, 108f, 16, 16, 1))
            {
                throw new InvalidOperationException("Standard bug net envelope should stay tighter than the previous radial pre-check.");
            }

            if (!AutoCaptureCritterService.IsWithinCaptureRangeForTesting(100f, 100f, 148f, 108f, 16, 16, 2))
            {
                throw new InvalidOperationException("Golden bug net reach should extend slightly beyond the standard bug net envelope.");
            }
        }

        private static void AutoCaptureCritterRestorePoleKeepsFishingSlotSelected()
        {
            var request = AutoCaptureCritterService.BuildRestorePoleRequestForTesting(1, 2294);
            if (request.Kind != InputActionKind.SelectHotbarSlot)
            {
                throw new InvalidOperationException("Fishing pole restore must keep using SelectHotbarSlot.");
            }

            AssertMetadata(request, "Slot", "1");
            AssertMetadata(request, "KeepSelected", "true");
        }

        private static void FishingFilterSkipHoldsSelectionUntilBobberGone()
        {
            var request = FishingAutomationService.BuildFilterSkipRequestForTesting(0, 1234, 1, 2294);
            if (request.Kind != InputActionKind.SelectHotbarSlot)
            {
                throw new InvalidOperationException("Fishing filter skip must keep using SelectHotbarSlot.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.FishingFilter, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.FishingFilterSkip);
            AssertMetadata(request, "Slot", "0");
            AssertMetadata(request, "PreferImmediateSelection", "true");
            AssertMetadata(request, "HoldUntilFishingBobberGone", "true");
            AssertMetadata(request, "FishingBobberIdentity", "1234");
            AssertMetadata(request, "FishingPoleSlot", "1");
            AssertMetadata(request, "FishingPoleItemType", "2294");
            AssertMetadata(request, "MaxBobberGoneWaitTicks", "90");
            if (request.Timeout < TimeSpan.FromSeconds(4))
            {
                throw new InvalidOperationException("Fishing filter skip timeout must cover bobber-gone hold and restore.");
            }
        }

        private static void SelectedItemStateForceSelectionUpdatesHotbarState()
        {
            var player = new TestSelectedItemStatePlayer(1);
            if (!TerrariaPlayerSelectionCompat.TryForceInventorySlotSelection(player, 0))
            {
                throw new InvalidOperationException("Expected selectedItemState force selection to support hotbar fallback: " + TerrariaPlayerSelectionCompat.LastError);
            }

            if (player.selectedItem != 0)
            {
                throw new InvalidOperationException("selectedItemState direct fallback did not update selected item.");
            }

            if (player.selectedItemState.HotbarForTesting != 0 ||
                player.selectedItemState.BufferedForTesting != -1 ||
                player.selectedItemState.OverriddenForTesting != -1)
            {
                throw new InvalidOperationException("selectedItemState direct fallback did not refresh hotbar and clear pending selection state.");
            }
        }

        private static void FishingLoadoutRestoreAttemptedKeepsSessionForRetry()
        {
            var requestId = Guid.NewGuid();
            FishingLoadoutService.ResetForTesting();
            FishingLoadoutService.SetRestoreSessionForTesting(requestId, 1, 0);
            FishingLoadoutService.OnActionCompleted(new InputActionResult
            {
                RequestId = requestId,
                Kind = InputActionKind.InventorySlot,
                Scenario = ScenarioNames.FishingAutoLoadoutRestore,
                Status = InputActionStatus.AttemptedButUnverified
            });

            if (!FishingLoadoutService.IsSessionActiveForTesting())
            {
                throw new InvalidOperationException("AttemptedButUnverified restore must keep fishing loadout session active for retry.");
            }

            var succeededRequestId = Guid.NewGuid();
            FishingLoadoutService.SetRestoreSessionForTesting(succeededRequestId, 1, 0);
            FishingLoadoutService.OnActionCompleted(new InputActionResult
            {
                RequestId = succeededRequestId,
                Kind = InputActionKind.InventorySlot,
                Scenario = ScenarioNames.FishingAutoLoadoutRestore,
                Status = InputActionStatus.Succeeded
            });

            if (FishingLoadoutService.IsSessionActiveForTesting())
            {
                throw new InvalidOperationException("Succeeded restore should clear fishing loadout session.");
            }

            FishingLoadoutService.ResetForTesting();
        }

        private sealed class TestSelectedItemStatePlayer
        {
            public TestSelectedItemState selectedItemState;

            public TestSelectedItemStatePlayer(int selectedItem)
            {
                selectedItemState = new TestSelectedItemState(selectedItem, selectedItem, 7, 8);
            }

            public int selectedItem
            {
                get { return selectedItemState.SelectedForTesting; }
            }
        }

        private struct TestSelectedItemState
        {
            private int selected;
            private int hotbar;
            private int buffered;
            private int overridden;

            public TestSelectedItemState(int selected, int hotbar, int buffered, int overridden)
            {
                this.selected = selected;
                this.hotbar = hotbar;
                this.buffered = buffered;
                this.overridden = overridden;
            }

            public int SelectedForTesting
            {
                get { return selected; }
            }

            public int HotbarForTesting
            {
                get { return hotbar; }
            }

            public int BufferedForTesting
            {
                get { return buffered; }
            }

            public int OverriddenForTesting
            {
                get { return overridden; }
            }
        }

        private static void AutoCaptureCritterRecognizesBugNetItemType()
        {
            var snapshot = new GameStateSnapshot
            {
                Player = new PlayerStateSnapshot
                {
                    Exists = true,
                    Active = true,
                    PositionX = 100f,
                    PositionY = 100f
                },
                Inventory = new InventorySnapshot
                {
                    Items = new List<InventoryItemSnapshot>
                    {
                        new InventoryItemSnapshot { SlotIndex = 6, Type = TerrariaBugNetCompat.BugNetItemType, Name = "Bug Net", Stack = 1 }
                    }
                },
                Npcs = new NpcSummarySnapshot
                {
                    CatchableCritters = new List<NpcSnapshot>
                    {
                        new NpcSnapshot { Active = true, WhoAmI = 7, Type = 616, CatchItem = 4464, PositionX = 136f, PositionY = 108f, CenterX = 144f, CenterY = 116f, Width = 16, Height = 16 }
                    }
                }
            };

            InputActionRequest request;
            string message;
            if (!AutoCaptureCritterService.TryBuildCaptureRequestForTesting(snapshot, out request, out message))
            {
                throw new InvalidOperationException("Expected bug net item type to produce a capture request: " + message);
            }

            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "6");
            AssertMetadata(request, "BugNetCatchTool", "1");
            AssertMetadata(request, "AutoCaptureCritterNpcIndex", "7");
        }

        private static void AutoCaptureCritterManualModeRequiresHeldBugNet()
        {
            var snapshot = new GameStateSnapshot
            {
                Player = new PlayerStateSnapshot
                {
                    Exists = true,
                    Active = true,
                    PositionX = 100f,
                    PositionY = 100f
                },
                Inventory = new InventorySnapshot
                {
                    SelectedItemSlot = 0,
                    SelectedItem = new InventoryItemSnapshot { SlotIndex = 0, Type = 4, Name = "Copper Shortsword", Stack = 1 },
                    Items = new List<InventoryItemSnapshot>
                    {
                        new InventoryItemSnapshot { SlotIndex = 0, Type = 4, Name = "Copper Shortsword", Stack = 1 },
                        new InventoryItemSnapshot { SlotIndex = 1 },
                        new InventoryItemSnapshot { SlotIndex = 2 },
                        new InventoryItemSnapshot { SlotIndex = 3 },
                        new InventoryItemSnapshot { SlotIndex = 4 },
                        new InventoryItemSnapshot { SlotIndex = 5 },
                        new InventoryItemSnapshot { SlotIndex = 6, Type = TerrariaBugNetCompat.BugNetItemType, Name = "Bug Net", Stack = 1 }
                    }
                },
                Npcs = new NpcSummarySnapshot
                {
                    CatchableCritters = new List<NpcSnapshot>
                    {
                        new NpcSnapshot { Active = true, WhoAmI = 7, Type = 616, CatchItem = 4464, PositionX = 136f, PositionY = 108f, CenterX = 144f, CenterY = 116f, Width = 16, Height = 16 }
                    }
                }
            };

            InputActionRequest request;
            string message;
            if (AutoCaptureCritterService.TryBuildCaptureRequestForTesting(snapshot, AutoCaptureCritterModes.Manual, out request, out message))
            {
                throw new InvalidOperationException("Manual auto capture mode must not use a bug net from backpack when the selected item is not a bug net.");
            }

            snapshot.Inventory.SelectedItemSlot = 6;
            snapshot.Inventory.SelectedItem = snapshot.Inventory.Items[6];
            if (!AutoCaptureCritterService.TryBuildCaptureRequestForTesting(snapshot, AutoCaptureCritterModes.Manual, out request, out message))
            {
                throw new InvalidOperationException("Manual auto capture mode should accept the selected bug net: " + message);
            }

            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "6");
            AssertMetadata(request, "AutoCaptureCritterMode", AutoCaptureCritterModes.Manual);
            AssertMetadata(request, "BugNetCatchTool", "1");
        }

        private static void AutoCaptureCritterTickEnqueuesRequestWhenNearby()
        {
            var originalMode = ConfigService.AppSettings.WorldAutomationAutoCaptureCritterMode;
            try
            {
                AutoCaptureCritterService.ResetForTesting();
                ConfigService.AppSettings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Auto;

                var queue = new InputActionQueue();
                var snapshot = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Player = new PlayerStateSnapshot
                    {
                        Exists = true,
                        Active = true,
                        PositionX = 100f,
                        PositionY = 100f
                    },
                    Inventory = new InventorySnapshot
                    {
                        Items = new List<InventoryItemSnapshot>
                        {
                            new InventoryItemSnapshot { SlotIndex = 8, Type = TerrariaBugNetCompat.BugNetItemType, Name = "Bug Net", Stack = 1 }
                        }
                    },
                    Npcs = new NpcSummarySnapshot
                    {
                    CatchableCritters = new List<NpcSnapshot>
                    {
                        new NpcSnapshot { Active = true, WhoAmI = 12, Type = 616, CatchItem = 4464, PositionX = 136f, PositionY = 108f, CenterX = 144f, CenterY = 116f, Width = 16, Height = 16 }
                    }
                }
            };
                var runtime = new RuntimeState
                {
                    UpdateCount = 100
                };

                AutoCaptureCritterService.Tick(queue, snapshot, runtime);

                var queueSnapshot = queue.GetSnapshot();
                if (queueSnapshot.PendingCount != 1)
                {
                    throw new InvalidOperationException("Expected auto capture critter to enqueue one request, got " + queueSnapshot.PendingCount.ToString(CultureInfo.InvariantCulture) + ".");
                }

                var diagnostics = AutoCaptureCritterService.GetDiagnostics();
                AssertStringEquals(diagnostics.LastDecision, "submitted sustained capture request", "auto capture last decision");
                if (diagnostics.BugNetSlot != 8 || diagnostics.TargetNpcIndex != 12)
                {
                    throw new InvalidOperationException("Expected diagnostics to keep the selected bug net slot and target NPC.");
                }
            }
            finally
            {
                ConfigService.AppSettings.WorldAutomationAutoCaptureCritterMode = originalMode;
                AutoCaptureCritterService.ResetForTesting();
            }
        }

        private static void AutoHarvestMapsExactHerbSeeds()
        {
            int seed;
            if (!AutoHarvestService.TryResolveSeedItemTypeForTesting(0, out seed) || seed != 307)
            {
                throw new InvalidOperationException("Daybloom herb style should map to Daybloom Seeds.");
            }

            if (!AutoHarvestService.TryResolveSeedItemTypeForTesting(6, out seed) || seed != 2357)
            {
                throw new InvalidOperationException("Shiverthorn herb style should map to Shiverthorn Seeds.");
            }

            if (AutoHarvestService.TryResolveSeedItemTypeForTesting(7, out seed))
            {
                throw new InvalidOperationException("Unknown herb styles must not map to a random seed.");
            }

            if (!AutoHarvestService.IsRegrowthToolForTesting(213) ||
                !AutoHarvestService.IsRegrowthToolForTesting(5295) ||
                AutoHarvestService.IsRegrowthToolForTesting(1991))
            {
                throw new InvalidOperationException("Auto harvest must only accept Staff of Regrowth or Axe of Regrowth as harvest tools.");
            }
        }

        private static void AutoHarvestRequestUsesSustainedRawInputMetadata()
        {
            var request = AutoHarvestService.BuildSustainedHarvestRequestForTesting(4, 213, 20, 30, 84, 2, 309);
            if (request.Kind != InputActionKind.RawInput)
            {
                throw new InvalidOperationException("Auto harvest must use RawInput sustained action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.WorldAutomationAutoHarvest, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoHarvest);
            AssertMetadata(request, ActionMetadataKeys.RawInputMode, "AutoHarvestSustainedUse");
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "4");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "328");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "488");
            AssertMetadata(request, "AutoHarvestAction", "HarvestSustainedUse");
            AssertMetadata(request, "AutoHarvestToolItemType", "213");
            AssertMetadata(request, "AutoHarvestTileX", "20");
            AssertMetadata(request, "AutoHarvestTileY", "30");
            AssertMetadata(request, "AutoHarvestHerbStyle", "2");
            AssertMetadata(request, "AutoHarvestSeedItemType", "309");
        }

        private static void AutoHarvestReplantRequestUsesExactSeedMetadata()
        {
            var request = AutoHarvestService.BuildReplantRequestForTesting(12, 2357, 42, 64, 6);
            if (request.Kind != InputActionKind.ItemUse)
            {
                throw new InvalidOperationException("Auto harvest replant must use ItemUse action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.WorldAutomationAutoHarvest, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoHarvestReplant);
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "12");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "680");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "1032");
            AssertMetadata(request, "AutoHarvestAction", "Replant");
            AssertMetadata(request, "AutoHarvestTileX", "42");
            AssertMetadata(request, "AutoHarvestTileY", "64");
            AssertMetadata(request, "AutoHarvestHerbStyle", "6");
            AssertMetadata(request, "AutoHarvestSeedItemType", "2357");
        }

        private static void AutoMiningTargetsNearestReachableFrontierTile()
        {
            var tiles = new List<AutoMiningTile>
            {
                new AutoMiningTile(9, 9),
                new AutoMiningTile(10, 9),
                new AutoMiningTile(11, 9),
                new AutoMiningTile(9, 10),
                new AutoMiningTile(10, 10),
                new AutoMiningTile(11, 10),
                new AutoMiningTile(9, 11),
                new AutoMiningTile(10, 11),
                new AutoMiningTile(11, 11),
                new AutoMiningTile(14, 10)
            };

            int remaining;
            var target = AutoMiningService.ChooseNextTargetForTesting(
                tiles,
                tile => true,
                tile => tile.X != 14,
                168f,
                168f,
                out remaining);

            if (remaining != 10)
            {
                throw new InvalidOperationException("Auto mining remaining count should include all active vein tiles.");
            }

            if (target == null || target.X != 10 || target.Y != 9)
            {
                throw new InvalidOperationException("Auto mining should skip the enclosed center tile and choose the nearest reachable frontier tile.");
            }
        }

        private static void AutoMiningReachExcludesRectangleCornersOutsideHelperRadius()
        {
            var centerX = 170f;
            var centerY = 181f;
            var maxDistanceWorld = 80f;

            if (!AutoMiningCompat.IsTileInsideReachShapeForTesting(11, 7, 5, 6, 16, 16, centerX, centerY, maxDistanceWorld))
            {
                throw new InvalidOperationException("Auto mining should keep tiles that are inside both the reach rectangle and helper-style radius.");
            }

            if (AutoMiningCompat.IsTileInsideReachShapeForTesting(16, 6, 5, 6, 16, 16, centerX, centerY, maxDistanceWorld))
            {
                throw new InvalidOperationException("Auto mining must not mark rectangle corner tiles green when they are outside the helper-style mining radius.");
            }
        }

        private static void AutoMiningGreenReachRespectsPickPower()
        {
            if (AutoMiningCompat.IsPickPowerSufficientForTileForTesting(111, 400, 149))
            {
                throw new InvalidOperationException("Auto mining must keep adamantite tiles red until the held pickaxe reaches the required power.");
            }

            if (!AutoMiningCompat.IsPickPowerSufficientForTileForTesting(111, 400, 150))
            {
                throw new InvalidOperationException("Auto mining should allow green reach once the held pickaxe meets the tile requirement.");
            }
        }

        private static void FeatureCatalogExposesTravelMenu()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition travelMenu;
            if (!registry.TryGet(FeatureIds.WorldAutomationTravelMenu, out travelMenu) || travelMenu == null)
            {
                throw new InvalidOperationException("Expected misc travel menu feature to be registered.");
            }

            if (!travelMenu.VisibleInMainUi)
            {
                throw new InvalidOperationException("Travel menu should be visible in the main UI after resuming the feature.");
            }

            if (!travelMenu.IsImplemented)
            {
                throw new InvalidOperationException("Travel menu should be marked implemented after resuming the feature.");
            }

            if (travelMenu.LifecycleStatus != FeatureLifecycleStatus.Implemented)
            {
                throw new InvalidOperationException("Expected implemented lifecycle for travel menu.");
            }

            if (travelMenu.MultiplayerSupport != FeatureMultiplayerSupport.SinglePlayerFallbackOnly)
            {
                throw new InvalidOperationException("Expected travel menu to remain single-player-only metadata.");
            }

            if (travelMenu.CodeDomain != FeatureCodeDomain.WorldAutomation ||
                travelMenu.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Travel menu must stay WorldAutomation code-domain and Misc UI category.");
            }
        }

        private static void TravelMenuRuntimePathIsResumed()
        {
            if (TravelMenuService.IsSuspended)
            {
                throw new InvalidOperationException("Travel menu should not remain suspended after the CreativeUI input bypass fix.");
            }

            var result = TravelMenuService.SetEnabledFromUi(true, true);
            if (string.Equals(result.ResultCode, TravelMenuService.SuspendedResultCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Travel menu enable path should no longer return suspended.");
            }

            var diagnostics = TravelMenuService.GetDiagnostics();
            if (diagnostics.Enabled ||
                diagnostics.SessionActive)
            {
                throw new InvalidOperationException("Failed enable attempt in tests should not leave an active travel menu session.");
            }
        }

        private static void FeatureCatalogExposesImplementedMiscInventoryAutomation()
        {
            var registry = FeatureRegistry.CreateDefault();
            AssertImplementedFeatureVisible(registry, "inventory.continuous_bag_open");
            AssertImplementedFeatureVisible(registry, "inventory.auto_deposit_coins");
            AssertImplementedFeatureVisible(registry, "inventory.auto_extractinator");
            AssertImplementedFeatureVisible(registry, "inventory.keep_favorited");
        }

        private static void WorldGenDebugViewerAndDeveloperMenuAlwaysAvailable()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.DiagnosticsWorldGenDebugViewer, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected WorldGen Debug Viewer feature to be registered.");
            }

            if (!feature.DefaultEnabled)
            {
                throw new InvalidOperationException("WorldGen Debug Viewer must default to enabled.");
            }

            if (!feature.VisibleInMainUi ||
                feature.IsInternalPlatform ||
                feature.HasConfig ||
                feature.CodeDomain != FeatureCodeDomain.Diagnostics ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("WorldGen Debug Viewer must be a visible informational Diagnostics-domain row on the Misc UI page.");
            }

            FeatureDefinition developerCommands;
            if (!registry.TryGet(FeatureIds.DiagnosticsDeveloperDebugCommands, out developerCommands) || developerCommands == null)
            {
                throw new InvalidOperationException("Expected developer debug commands feature to be registered separately.");
            }

            if (!developerCommands.DefaultEnabled)
            {
                throw new InvalidOperationException("Developer debug commands must default to available.");
            }

            if (!developerCommands.VisibleInMainUi ||
                developerCommands.HasConfig ||
                developerCommands.CodeDomain != FeatureCodeDomain.Diagnostics ||
                developerCommands.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Developer debug commands must stay a visible Diagnostics-domain open action on the Misc UI page.");
            }

            var defaults = AppSettings.CreateDefault();
            if (!defaults.DiagnosticsWorldGenDebugViewerEnabled || !defaults.DiagnosticsDeveloperDebugCommandsEnabled)
            {
                throw new InvalidOperationException("WorldGen Debug Viewer and developer debug commands must both be available by default.");
            }

            if (!LateBootstrap.ShouldInstallDebugUiLocalizationHooks(defaults))
            {
                throw new InvalidOperationException("Debug UI localization hooks must install for the default WorldGen viewer and developer menu entries.");
            }

            defaults.DiagnosticsWorldGenDebugViewerEnabled = false;
            defaults.DiagnosticsDeveloperDebugCommandsEnabled = false;
            if (!defaults.DiagnosticsWorldGenDebugViewerEnabled || !defaults.DiagnosticsDeveloperDebugCommandsEnabled)
            {
                throw new InvalidOperationException("Legacy switch setters must not disable the always-available debug entries.");
            }

            if (!LateBootstrap.ShouldInstallDebugUiLocalizationHooks(defaults))
            {
                throw new InvalidOperationException("Legacy switch setters must not disable debug UI localization hooks.");
            }
        }

        private static void DiagnosticSnapshotWritesWorldGenDebugState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                WorldGenDebugViewerConfiguredEnabled = true,
                DeveloperDebugCommandsConfiguredEnabled = true,
                WorldGenDebugViewerSessionConfiguredEnabled = true,
                DeveloperDebugCommandsSessionConfiguredEnabled = false,
                WorldGenDebugAttempted = true,
                WorldGenDebugFieldEnabled = true,
                WorldGenDebugStatus = "enabled",
                WorldGenDebugMessage = "enableDebugCommands set to true",
                WorldGenDebugFieldOwner = "Terraria.Testing.DebugOptions.enableDebugCommands",
                WorldGenDebugLastAttemptUtc = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"WorldGenDebugViewerConfiguredEnabled\": true");
            AssertContains(json, "\"DeveloperDebugCommandsConfiguredEnabled\": true");
            AssertContains(json, "\"WorldGenDebugViewerSessionConfiguredEnabled\": true");
            AssertContains(json, "\"DeveloperDebugCommandsSessionConfiguredEnabled\": false");
            AssertContains(json, "\"WorldGenDebugAttempted\": true");
            AssertContains(json, "\"WorldGenDebugFieldEnabled\": true");
            AssertContains(json, "\"WorldGenDebugStatus\": \"enabled\"");
            AssertContains(json, "\"WorldGenDebugMessage\": \"enableDebugCommands set to true\"");
            AssertContains(json, "\"WorldGenDebugFieldOwner\": \"Terraria.Testing.DebugOptions.enableDebugCommands\"");
            AssertContains(json, "\"WorldGenDebugLastAttemptUtc\": \"2026-05-25T00:00:00.0000000Z\"");
        }

        private static void DiagnosticSnapshotWritesAutoStackState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                AutoStackLastDecision = "waiting for inventory-open auto stack settle",
                AutoStackLastDecisionUtc = new DateTime(2026, 5, 26, 2, 3, 4, DateTimeKind.Utc),
                AutoStackLastInventorySignature = "3:12x2",
                AutoStackLastPendingItemIds = "12"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"AutoStackLastDecision\": \"waiting for inventory-open auto stack settle\"");
            AssertContains(json, "\"AutoStackLastDecisionUtc\": \"2026-05-26T02:03:04.0000000Z\"");
            AssertContains(json, "\"AutoStackLastInventorySignature\": \"3:12x2\"");
            AssertContains(json, "\"AutoStackLastPendingItemIds\": \"12\"");
        }

        private static void DiagnosticSnapshotWritesAutoDepositCoinsState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                AutoDepositCoinsLastDecision = "submitted auto deposit coins request",
                AutoDepositCoinsLastDecisionUtc = new DateTime(2026, 5, 26, 3, 4, 5, DateTimeKind.Utc),
                AutoDepositCoinsLastInventorySignature = "0:71x88|3:72x32|15:74x2",
                AutoDepositCoinsLastCoinItemIds = "71,72,74"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"AutoDepositCoinsLastDecision\": \"submitted auto deposit coins request\"");
            AssertContains(json, "\"AutoDepositCoinsLastDecisionUtc\": \"2026-05-26T03:04:05.0000000Z\"");
            AssertContains(json, "\"AutoDepositCoinsLastInventorySignature\": \"0:71x88|3:72x32|15:74x2\"");
            AssertContains(json, "\"AutoDepositCoinsLastCoinItemIds\": \"71,72,74\"");
        }

        private static void DiagnosticSnapshotWritesAutoCaptureCritterState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                AutoCaptureCritterLastDecision = "submitted sustained capture request",
                AutoCaptureCritterLastDecisionUtc = new DateTime(2026, 5, 25, 2, 3, 4, DateTimeKind.Utc),
                AutoCaptureCritterBugNetSlot = 8,
                AutoCaptureCritterBugNetItemType = TerrariaBugNetCompat.BugNetItemType,
                AutoCaptureCritterTargetNpcIndex = 12,
                AutoCaptureCritterTargetNpcType = 616,
                AutoCaptureCritterFishingProtectionState = "waiting=false,recast=false,poleSlot=0,poleItemType=0,bobber=-1"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"AutoCaptureCritterLastDecision\": \"submitted sustained capture request\"");
            AssertContains(json, "\"AutoCaptureCritterLastDecisionUtc\": \"2026-05-25T02:03:04.0000000Z\"");
            AssertContains(json, "\"AutoCaptureCritterBugNetSlot\": 8");
            AssertContains(json, "\"AutoCaptureCritterBugNetItemType\": 1991");
            AssertContains(json, "\"AutoCaptureCritterTargetNpcIndex\": 12");
            AssertContains(json, "\"AutoCaptureCritterTargetNpcType\": 616");
            AssertContains(json, "\"AutoCaptureCritterFishingProtectionState\": \"waiting=false,recast=false,poleSlot=0,poleItemType=0,bobber=-1\"");
        }

        private static void DiagnosticSnapshotWritesAutoHarvestState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                AutoHarvestLastDecision = "submitted replant request",
                AutoHarvestLastDecisionUtc = new DateTime(2026, 5, 26, 1, 2, 3, DateTimeKind.Utc),
                AutoHarvestLastAction = "Replant",
                AutoHarvestToolSlot = 5,
                AutoHarvestToolItemType = 5295,
                AutoHarvestTargetTileX = 120,
                AutoHarvestTargetTileY = 210,
                AutoHarvestTargetSeedItemType = 2357,
                AutoHarvestPendingReplantCount = 2
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"AutoHarvestLastDecision\": \"submitted replant request\"");
            AssertContains(json, "\"AutoHarvestLastDecisionUtc\": \"2026-05-26T01:02:03.0000000Z\"");
            AssertContains(json, "\"AutoHarvestLastAction\": \"Replant\"");
            AssertContains(json, "\"AutoHarvestToolSlot\": 5");
            AssertContains(json, "\"AutoHarvestToolItemType\": 5295");
            AssertContains(json, "\"AutoHarvestTargetTileX\": 120");
            AssertContains(json, "\"AutoHarvestTargetTileY\": 210");
            AssertContains(json, "\"AutoHarvestTargetSeedItemType\": 2357");
            AssertContains(json, "\"AutoHarvestPendingReplantCount\": 2");
        }

        private static void PerformanceHitchRecorderDetectsRuntimeGaps()
        {
            var normal = new PerformanceHitchSample
            {
                UpdateStartGapMs = PerformanceHitchRecorder.UpdateStartGapThresholdMs - 1d,
                RuntimeUpdateMs = PerformanceHitchRecorder.RuntimeUpdateThresholdMs - 1d,
                GameStateReadMs = PerformanceHitchRecorder.GameStateReadThresholdMs - 1d,
                ActionQueueUpdateMs = PerformanceHitchRecorder.ActionQueueUpdateThresholdMs - 1d,
                InputActionUpdateMs = PerformanceHitchRecorder.InputActionUpdateThresholdMs - 1d,
                InformationLastDrawElapsedMs = PerformanceHitchRecorder.InformationDrawThresholdMs - 1d
            };

            if (PerformanceHitchRecorder.ShouldRecord(normal))
            {
                throw new InvalidOperationException("Normal runtime timings must not produce a hitch event.");
            }

            if (PerformanceHitchRecorder.ShouldRecordFast(
                normal.UpdateStartGapMs,
                normal.RuntimeUpdateMs,
                normal.GameStateReadMs,
                normal.ActionQueueUpdateMs,
                normal.InputActionUpdateMs,
                normal.InformationLastDrawElapsedMs))
            {
                throw new InvalidOperationException("Normal runtime timings must not pass the fast hitch check.");
            }

            var factoryCalls = 0;
            PerformanceHitchRecorder.RecordIfNeeded(
                normal.UpdateStartGapMs,
                normal.RuntimeUpdateMs,
                normal.GameStateReadMs,
                normal.ActionQueueUpdateMs,
                normal.InputActionUpdateMs,
                normal.InformationLastDrawElapsedMs,
                () =>
                {
                    factoryCalls++;
                    return normal;
                });
            if (factoryCalls != 0)
            {
                throw new InvalidOperationException("Performance hitch sample factory must stay lazy below thresholds.");
            }

            var gap = new PerformanceHitchSample
            {
                UpdateStartGapMs = PerformanceHitchRecorder.UpdateStartGapThresholdMs
            };

            if (!PerformanceHitchRecorder.ShouldRecord(gap))
            {
                throw new InvalidOperationException("A long interval between Runtime.Update starts must produce a hitch event.");
            }

            if (!PerformanceHitchRecorder.ShouldRecordFast(
                0d,
                PerformanceHitchRecorder.RuntimeUpdateThresholdMs,
                0d,
                0d,
                0d,
                0d))
            {
                throw new InvalidOperationException("Runtime update threshold must pass the fast hitch check.");
            }

            if (!PerformanceHitchRecorder.ShouldRecordFast(
                0d,
                0d,
                0d,
                0d,
                0d,
                PerformanceHitchRecorder.InformationDrawThresholdMs))
            {
                throw new InvalidOperationException("Information draw threshold must pass the fast hitch check.");
            }

            var reason = PerformanceHitchRecorder.BuildReason(new PerformanceHitchSample
            {
                RuntimeUpdateMs = PerformanceHitchRecorder.RuntimeUpdateThresholdMs,
                InformationLastDrawElapsedMs = PerformanceHitchRecorder.InformationDrawThresholdMs
            });

            AssertContains(reason, "runtimeUpdate");
            AssertContains(reason, "informationDraw");
        }

        private static void GameStateReadOptionsMapCoinAutomationToCoinsProfile()
        {
            var settings = AppSettings.CreateDefault();
            settings.InventoryAutoDepositCoinsEnabled = true;

            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(settings, false);

            if ((options.InventoryProfile & InventoryReadProfile.CoinsOnly) != InventoryReadProfile.CoinsOnly)
            {
                throw new InvalidOperationException("Auto deposit coins must request the coins inventory profile.");
            }

            if ((options.InventoryProfile & InventoryReadProfile.RecoveryFields) == InventoryReadProfile.RecoveryFields)
            {
                throw new InvalidOperationException("Coins-only profile must not request recovery item fields.");
            }
        }

        private static void GameStateReadOptionsMergeCaptureAndStackProfiles()
        {
            var settings = AppSettings.CreateDefault();
            settings.InventoryAutoStackEnabled = true;
            settings.MiscAutoCaptureCritterEnabled = true;
            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Auto;

            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(settings, false);

            if ((options.InventoryProfile & InventoryReadProfile.StackCandidates) != InventoryReadProfile.StackCandidates)
            {
                throw new InvalidOperationException("Auto stack must request stack candidate inventory fields.");
            }

            if ((options.InventoryProfile & InventoryReadProfile.BugNetOnly) != InventoryReadProfile.BugNetOnly)
            {
                throw new InvalidOperationException("Auto capture critter must request bug net inventory fields.");
            }

            if ((options.NpcProfile & NpcReadProfile.CatchableCritters) != NpcReadProfile.CatchableCritters)
            {
                throw new InvalidOperationException("Auto capture critter must request catchable critter NPC data.");
            }
        }

        private static void GameStateReadOptionsKeepDiagnosticsFullProfile()
        {
            var settings = AppSettings.CreateDefault();
            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(settings, true);

            if (options.InventoryProfile != InventoryReadProfile.Full ||
                options.NpcProfile != NpcReadProfile.Full ||
                options.TileProfile != TileReadProfile.Full ||
                !options.IncludeWorldSummary)
            {
                throw new InvalidOperationException("Diagnostic snapshots must still request a full GameState profile.");
            }
        }

        private static void DiagnosticSnapshotWritesGameStateReadProfiles()
        {
            var snapshot = new DiagnosticSnapshot
            {
                LastGameStateInventoryProfile = "CoinsOnly",
                LastGameStateNpcProfile = "CatchableCrittersOnly",
                LastGameStateTileProfile = "None"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"LastGameStateInventoryProfile\": \"CoinsOnly\"");
            AssertContains(json, "\"LastGameStateNpcProfile\": \"CatchableCrittersOnly\"");
            AssertContains(json, "\"LastGameStateTileProfile\": \"None\"");
        }

        private static void RuntimeSettingsSnapshotNormalizesHotPathFields()
        {
            var settings = AppSettings.CreateDefault();
            settings.AutoHealEnabled = true;
            settings.AutoHealMode = AutoRecoverySettings.HealModeSmart;
            settings.AutoManaEnabled = true;
            settings.AutoManaMode = AutoRecoverySettings.ManaModeManaFlower;
            settings.CursorAimRadius = 99;
            settings.PlayerAimRadius = -3;
            settings.CombatAimAssistRadius = 75;
            settings.AimRangeOrigin = "invalid";
            settings.AimTargetPriority = CombatAimModes.TargetPriorityNearest;
            settings.WorldAutomationAutoMiningMode = AutoMiningModes.Auto;
            settings.MiscAutoCaptureCritterEnabled = true;
            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Auto;

            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);

            if (!snapshot.RecoveryAnyEnabled ||
                !string.Equals(snapshot.AutoHealMode, AutoRecoverySettings.HealModeSmart, StringComparison.Ordinal) ||
                !string.Equals(snapshot.AutoManaMode, AutoRecoverySettings.ManaModeManaFlower, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Runtime settings snapshot must normalize recovery modes.");
            }

            if (snapshot.CursorAimRadius != 50 ||
                snapshot.PlayerAimRadius != 0 ||
                snapshot.CombatAimAssistRadius != 50 ||
                !snapshot.CombatAimAnyEnabled)
            {
                throw new InvalidOperationException("Runtime settings snapshot must clamp combat aim radii.");
            }

            if (!string.Equals(snapshot.AimRangeOrigin, CombatAimModes.RangeOriginCursor, StringComparison.Ordinal) ||
                !string.Equals(snapshot.AimTargetPriority, CombatAimModes.TargetPriorityNearest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Runtime settings snapshot must normalize combat aim modes.");
            }

            if (!snapshot.WorldAutomationAutoMiningEnabled ||
                !snapshot.WorldAutomationAutoCaptureCritterEnabled)
            {
                throw new InvalidOperationException("Runtime settings snapshot must expose normalized world automation enabled flags.");
            }
        }

        private static void RuntimeSettingsSnapshotBuildsGameStateProfile()
        {
            var settings = AppSettings.CreateDefault();
            settings.AutoHealEnabled = true;
            settings.AutoHealMode = AutoRecoverySettings.HealModeQuick;
            settings.InventoryAutoDepositCoinsEnabled = true;
            settings.InventoryAutoStackEnabled = true;
            settings.MiscAutoCaptureCritterEnabled = true;
            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Auto;
            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);

            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(snapshot, false);

            if ((options.InventoryProfile & InventoryReadProfile.RecoveryItems) != InventoryReadProfile.RecoveryItems ||
                (options.InventoryProfile & InventoryReadProfile.CoinsOnly) != InventoryReadProfile.CoinsOnly ||
                (options.InventoryProfile & InventoryReadProfile.StackCandidates) != InventoryReadProfile.StackCandidates ||
                (options.InventoryProfile & InventoryReadProfile.BugNetOnly) != InventoryReadProfile.BugNetOnly)
            {
                throw new InvalidOperationException("Runtime settings snapshot must drive merged inventory read profiles.");
            }

            if ((options.NpcProfile & NpcReadProfile.CatchableCritters) != NpcReadProfile.CatchableCritters)
            {
                throw new InvalidOperationException("Runtime settings snapshot must drive catchable critter NPC profile.");
            }
        }

        private static void RuntimeSettingsSnapshotProviderRebuildsAfterConfigMutation()
        {
            var settings = ConfigService.AppSettings;
            var originalCursorAimRadius = settings.CursorAimRadius;
            var originalPlayerAimRadius = settings.PlayerAimRadius;

            try
            {
                RuntimeSettingsSnapshotProvider.ResetForTesting();
                settings.CursorAimRadius = 3;
                settings.PlayerAimRadius = 4;
                var first = RuntimeSettingsSnapshotProvider.GetCurrent();

                settings.CursorAimRadius = 8;
                var second = RuntimeSettingsSnapshotProvider.GetCurrent();

                if (object.ReferenceEquals(first, second))
                {
                    throw new InvalidOperationException("Runtime settings snapshot provider must rebuild after config mutation.");
                }

                if (second.CursorAimRadius != 8 || second.PlayerAimRadius != 4)
                {
                    throw new InvalidOperationException("Runtime settings snapshot provider returned stale normalized values.");
                }
            }
            finally
            {
                settings.CursorAimRadius = originalCursorAimRadius;
                settings.PlayerAimRadius = originalPlayerAimRadius;
                RuntimeSettingsSnapshotProvider.ResetForTesting();
            }
        }

        private static void RuntimeServiceSchedulerHonorsCadenceAndDisabledCleanup()
        {
            JueMingZRuntime.ResetServiceSchedulerForTesting();

            if (!JueMingZRuntime.ShouldRunServiceForTesting("test-service", true, 3, 0))
            {
                throw new InvalidOperationException("A newly enabled service must run immediately.");
            }

            if (JueMingZRuntime.ShouldRunServiceForTesting("test-service", true, 3, 1))
            {
                throw new InvalidOperationException("Service cadence must skip ticks before the interval elapses.");
            }

            if (!JueMingZRuntime.ShouldRunServiceForTesting("test-service", true, 3, 3))
            {
                throw new InvalidOperationException("Service cadence must run when the interval elapses.");
            }

            if (!JueMingZRuntime.ShouldRunServiceForTesting("test-service", false, 3, 4))
            {
                throw new InvalidOperationException("A just-disabled service must get one cleanup tick.");
            }

            if (JueMingZRuntime.ShouldRunServiceForTesting("test-service", false, 3, 5))
            {
                throw new InvalidOperationException("A disabled service must not keep running after cleanup.");
            }

            if (!JueMingZRuntime.ShouldRunServiceForTesting("test-service", true, 3, 6))
            {
                throw new InvalidOperationException("A re-enabled service must run immediately.");
            }
        }

        private static void RuntimeInputFocusGuardUsesGameStateFocus()
        {
            var focused = new GameStateSnapshot
            {
                Ui = new UiStateSnapshot { GameInputAvailable = true }
            };
            if (!JueMingZRuntime.IsGameInputAvailableForTesting(focused))
            {
                throw new InvalidOperationException("Focused game state must allow input dispatch.");
            }

            var unfocused = new GameStateSnapshot
            {
                Ui = new UiStateSnapshot { GameInputAvailable = false }
            };
            if (JueMingZRuntime.IsGameInputAvailableForTesting(unfocused))
            {
                throw new InvalidOperationException("Unfocused game state must pause physical/user input dispatch.");
            }

            if (!JueMingZRuntime.ShouldDispatchAutomationForTesting(unfocused))
            {
                throw new InvalidOperationException("Unfocused game state must still allow background automation dispatch.");
            }
        }

        private static void DiagnosticSnapshotWritesPerformanceHitchState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                LastUpdateStartGapMs = 87.5d,
                LastSlowestStageName = "game-state-read",
                LastSlowestStageElapsedMs = 12.25d,
                PerformanceEventsPath = "diagnostics/performance-events-20260525.jsonl",
                PerformanceHitchCount = 3,
                LastPerformanceHitchUtc = new DateTime(2026, 5, 25, 1, 2, 3, DateTimeKind.Utc),
                LastPerformanceHitchReason = "updateGap+gameStateRead",
                LastPerformanceHitchUpdateGapMs = 91.125d,
                LastPerformanceHitchRuntimeUpdateMs = 8.5d,
                LastPerformanceHitchGameStateReadMs = 11.75d,
                LastPerformanceHitchActionQueueUpdateMs = 1.25d,
                LastPerformanceHitchInputActionUpdateMs = 0.5d,
                LastPerformanceHitchInformationDrawMs = 3.25d,
                LastPerformanceHitchSlowestStageName = "game-state-read",
                LastPerformanceHitchSlowestStageMs = 11.75d
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"LastUpdateStartGapMs\": 87.5");
            AssertContains(json, "\"LastSlowestStageName\": \"game-state-read\"");
            AssertContains(json, "\"PerformanceEventsPath\": \"diagnostics/performance-events-20260525.jsonl\"");
            AssertContains(json, "\"PerformanceHitchCount\": 3");
            AssertContains(json, "\"LastPerformanceHitchUtc\": \"2026-05-25T01:02:03.0000000Z\"");
            AssertContains(json, "\"LastPerformanceHitchReason\": \"updateGap+gameStateRead\"");
            AssertContains(json, "\"LastPerformanceHitchUpdateGapMs\": 91.125");
            AssertContains(json, "\"LastPerformanceHitchSlowestStageName\": \"game-state-read\"");
        }

        private static void AssertPlannedFeatureHidden(FeatureRegistry registry, string featureId)
        {
            FeatureDefinition feature;
            if (!registry.TryGet(featureId, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected planned feature to be registered: " + featureId);
            }

            if (feature.IsImplemented)
            {
                throw new InvalidOperationException("Expected feature to be planned, not implemented: " + featureId);
            }

            if (feature.VisibleInMainUi)
            {
                throw new InvalidOperationException("Planned feature must not be visible in main UI: " + featureId);
            }

            if (feature.HasHotkey || feature.HotkeyListVisible)
            {
                throw new InvalidOperationException("Planned feature must not expose hotkey UI: " + featureId);
            }

            if (feature.LifecycleStatus != FeatureLifecycleStatus.Planned)
            {
                throw new InvalidOperationException("Expected planned lifecycle for: " + featureId);
            }
        }

        private static void AssertImplementedFeatureVisible(FeatureRegistry registry, string featureId)
        {
            FeatureDefinition feature;
            if (!registry.TryGet(featureId, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected implemented feature to be registered: " + featureId);
            }

            if (!feature.IsImplemented)
            {
                throw new InvalidOperationException("Expected feature to be implemented: " + featureId);
            }

            if (!feature.VisibleInMainUi)
            {
                throw new InvalidOperationException("Implemented feature must be visible in main UI: " + featureId);
            }

            if (feature.LifecycleStatus != FeatureLifecycleStatus.Implemented)
            {
                throw new InvalidOperationException("Expected implemented lifecycle for implemented feature: " + featureId);
            }
        }

        private static void FirstRunAppSettingsDefaultsMatchRequestedUiBaseline()
        {
            var settings = AppSettings.CreateDefault();

            AssertDefault(!settings.MiscQuickItemHotkeysEnabled, "misc quick item hotkeys off");
            AssertDefault(!settings.MiscAutoStackEnabled, "misc auto stack off");
            AssertDefault(!settings.MiscAutoSellEnabled, "misc auto sell off");
            AssertDefault(!settings.MiscAutoDiscardEnabled, "misc auto discard off");
            AssertDefault(!settings.MiscQuickReforgeEnabled, "misc quick reforge off");
            AssertDefault(!settings.WorldAutomationAutoMiningEnabled, "misc auto mining off");
            AssertDefault(!settings.MiscAutoCaptureCritterEnabled, "misc auto capture critter off");
            AssertStringEquals(settings.WorldAutomationAutoCaptureCritterMode, AutoCaptureCritterModes.Off, "misc auto capture critter mode off");
            AssertDefault(!settings.MiscAutoHarvestEnabled, "misc auto harvest off");
            AssertDefault(!settings.MiscQuickBagOpenEnabled, "misc quick bag open off");
            AssertDefault(!settings.MiscAutoDepositCoinsEnabled, "misc auto deposit coins off");
            AssertDefault(!settings.MiscAutoExtractinatorEnabled, "misc auto extractinator off");
            AssertDefault(!settings.MiscKeepFavoritedEnabled, "misc keep favorited off");
            AssertDefault(settings.DiagnosticsWorldGenDebugViewerEnabled, "worldgen debug viewer available");
            AssertDefault(settings.DiagnosticsDeveloperDebugCommandsEnabled, "developer debug commands available");

            AssertDefault(!settings.FishingAutoFishEnabled, "fishing auto fish off");
            AssertDefault(!settings.FishingAutoLoadoutEnabled, "fishing auto loadout off");
            AssertDefault(!settings.FishingAutoEquipmentEnabled, "fishing auto equipment off");
            AssertDefault(!settings.FishingAutoStoreQuestFishEnabled, "fishing quest fish store off");
            AssertStringEquals(FishingAutoStoreModes.Normalize(settings.FishingAutoStoreMode, settings.FishingAutoStoreQuestFishEnabled), FishingAutoStoreModes.Off, "fishing store mode");
            AssertDefault(!settings.FishingFilterCutRodSkipEnabled, "fishing cut rod skip off");
            AssertStringEquals(FishingFilterModes.Normalize(settings.FishingFilterMode), FishingFilterModes.Disabled, "fishing filter mode");
            AssertStringEquals(FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterCrateRule), FishingFilterSpecialRuleModes.Allow, "fishing crate rule");
            AssertStringEquals(FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterEnemyRule), FishingFilterSpecialRuleModes.Deny, "fishing enemy rule");
            AssertStringEquals(FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterQuestFishRule), FishingFilterSpecialRuleModes.Allow, "fishing quest fish rule");

            AssertDefault(settings.CombatAimAssistRadius == 0, "combat aim radius zero");
            AssertDefault(!settings.CombatAimTrackDummyEnabled, "combat aim track dummy off");
            AssertDefault(settings.CombatAimMarkerEnabled, "combat aim marker on");
            AssertDefault(!settings.CombatAutoClickerEnabled, "combat auto clicker off");
            AssertDefault(!settings.CombatPerfectRevolverEnabled, "combat perfect revolver off");
            AssertDefault(!settings.CombatMagicStringClickerEnabled, "combat magic string clicker off");
            AssertDefault(!settings.CombatAutoFacingEnabled, "combat auto facing off");
            AssertDefault(!settings.CombatEquipmentWarningEnabled, "combat equipment warning off");
            AssertStringEquals(CombatAimModes.NormalizeTargetPriority(settings.AimTargetPriority), CombatAimModes.TargetPriorityNearest, "combat aim target priority");
            AssertStringEquals(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, "combat aim range origin");

            AssertDefault(!settings.InformationEnemyNameLabelsEnabled, "information enemy labels off");
            AssertDefault(!settings.InformationCritterNameLabelsEnabled, "information critter labels off");
            AssertStringEquals(settings.InformationNpcNameLabelsMode, "Off", "information npc labels off");
            AssertStringEquals(settings.InformationChestNameLabelsMode, "Off", "information chest labels off");
            AssertStringEquals(settings.InformationSignTextLabelsMode, InformationSignTextModes.Off, "information sign text labels off");
            AssertStringEquals(settings.InformationTombstoneTextLabelsMode, InformationSignTextModes.Off, "information tombstone text labels off");
            AssertDefault(!settings.InformationHighlightLifeCrystalEnabled, "information life crystal off");
            AssertDefault(!settings.InformationHighlightManaCrystalEnabled, "information mana crystal off");
            AssertDefault(!settings.InformationHighlightDigtoiseEnabled, "information digtoise off");
            AssertDefault(!settings.InformationHighlightLifeFruitEnabled, "information life fruit off");
            AssertDefault(!settings.InformationHighlightDragonEggEnabled, "information dragon egg off");
            AssertDefault(!settings.InformationBiomeDisplayEnabled, "information biome off");
            AssertDefault(!settings.InformationWorldInfectionEnabled, "information infection off");
            AssertDefault(!settings.InformationLuckValueEnabled, "information luck off");
            AssertDefault(!settings.InformationFishingCatchesEnabled, "information fishing catches off");
            AssertDefault(!settings.InformationFishingFilteredCatchesEnabled, "information filtered catches off");
            AssertDefault(!settings.InformationAnglerQuestEnabled, "information angler quest off");

            AssertDefault(!settings.AutoHealEnabled, "auto heal off");
            AssertDefault(!settings.AutoManaEnabled, "auto mana off");
            AssertDefault(!settings.AutoBuffEnabled, "auto buff off");
            AssertDefault(!settings.AutoNurseEnabled, "auto nurse off");
            AssertDefault(!settings.AutoStationBuffEnabled, "auto station buff off");
            AssertDefault(!settings.AutoBuffFollowAddEnabled, "auto buff follow add off");
            AssertDefault(!settings.AutoBuffFollowRemoveEnabled, "auto buff follow remove off");
            AssertStringEquals(settings.AutoHealMode, "Off", "auto heal mode");
            AssertStringEquals(settings.AutoManaMode, "Off", "auto mana mode");

            AssertDefault(!settings.MovementSimulatedMultiJumpEnabled, "movement simulated multi jump off");
            AssertDefault(!settings.MovementContinuousDashEnabled, "movement continuous dash off");
            AssertDefault(!settings.MovementTeleportCorrectionEnabled, "movement teleport correction off");
            AssertDefault(!settings.MovementSafeLandingEnabled, "movement safe landing off");
        }

        private static void AutoCaptureCritterModeAliasesPreserveLegacyBool()
        {
            var settings = AppSettings.CreateDefault();

            settings.MiscAutoCaptureCritterEnabled = true;
            settings.MiscAutoCaptureCritterMode = null;
            AssertStringEquals(settings.WorldAutomationAutoCaptureCritterMode, AutoCaptureCritterModes.Auto, "legacy enabled auto capture mode");
            AssertDefault(settings.WorldAutomationAutoCaptureCritterEnabled, "legacy enabled auto capture feature");

            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Manual;
            AssertStringEquals(settings.MiscAutoCaptureCritterMode, AutoCaptureCritterModes.Manual, "manual mode storage");
            AssertDefault(settings.MiscAutoCaptureCritterEnabled, "manual mode enabled bool");

            settings.WorldAutomationAutoCaptureCritterEnabled = false;
            AssertStringEquals(settings.MiscAutoCaptureCritterMode, AutoCaptureCritterModes.Off, "disabled mode storage");
            AssertDefault(!settings.MiscAutoCaptureCritterEnabled, "disabled mode legacy bool");

            settings.WorldAutomationAutoCaptureCritterEnabled = true;
            AssertStringEquals(settings.MiscAutoCaptureCritterMode, AutoCaptureCritterModes.Auto, "enabled bool maps to auto mode");
        }

        private static void AppSettingsCodeDomainAliasesPreserveMiscStorage()
        {
            var settings = AppSettings.CreateDefault();
            settings.InventoryAutoStackEnabled = true;
            settings.InventoryAutoSellEnabled = true;
            settings.InventoryAutoDiscardEnabled = true;
            settings.InventoryQuickItemHotkeysEnabled = true;
            settings.InventoryAutoDepositCoinsEnabled = true;
            settings.NpcAutoReforgeEnabled = true;
            settings.WorldAutomationAutoMiningMode = AutoMiningModes.Auto;
            settings.WorldAutomationAutoCaptureCritterEnabled = true;
            settings.WorldAutomationTravelMenuEnabled = true;
            settings.DiagnosticsDeveloperDebugCommandsEnabled = true;
            settings.DiagnosticsWorldGenDebugViewerEnabled = true;

            if (!settings.MiscAutoStackEnabled ||
                !settings.MiscAutoSellEnabled ||
                !settings.MiscAutoDiscardEnabled ||
                !settings.MiscQuickItemHotkeysEnabled ||
                !settings.MiscAutoDepositCoinsEnabled ||
                !settings.MiscQuickReforgeEnabled ||
                !settings.WorldAutomationAutoMiningEnabled ||
                !string.Equals(settings.MiscAutoMiningMode, AutoMiningModes.Auto, StringComparison.Ordinal) ||
                !settings.MiscAutoCaptureCritterEnabled ||
                !string.Equals(settings.MiscAutoCaptureCritterMode, AutoCaptureCritterModes.Auto, StringComparison.Ordinal) ||
                !settings.MiscTravelMenuEnabled ||
                !settings.MiscDeveloperDebugCommandsEnabled ||
                !settings.MiscWorldGenDebugViewerEnabled ||
                !settings.DiagnosticsWorldGenDebugViewerEnabled)
            {
                throw new InvalidOperationException("Code-domain aliases must write through existing Misc storage fields.");
            }

            settings.MiscAutoSellItemIds = new List<int> { 2337 };
            settings.MiscAutoDiscardItemIds = new List<int> { 12 };
            settings.MiscQuickReforgePrefixes = new List<string> { "虚幻" };

            if (settings.InventoryAutoSellItemIds.Count != 1 ||
                settings.InventoryAutoSellItemIds[0] != 2337 ||
                settings.InventoryAutoDiscardItemIds.Count != 1 ||
                settings.InventoryAutoDiscardItemIds[0] != 12 ||
                settings.NpcAutoReforgePrefixes.Count != 1 ||
                !string.Equals(settings.NpcAutoReforgePrefixes[0], "虚幻", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Code-domain list aliases must read existing Misc storage lists.");
            }
        }

        private static void AssertDefault(bool condition, string label)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Unexpected first-run default: " + label);
            }
        }
    }
}
