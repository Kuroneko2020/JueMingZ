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
using Terraria.ID;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
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

        private static void AutoStackUnsafeUiRetainsPendingTransaction()
        {
            AutoStackService.ResetForTesting();
            try
            {
                var settings = AppSettings.CreateDefault();
                settings.InventoryAutoStackEnabled = true;
                var runtimeSettings = RuntimeSettingsSnapshot.FromSettings(settings);
                var queue = new InputActionQueue();
                var runtime = new RuntimeState { UpdateCount = 0 };

                AutoStackService.Tick(
                    queue,
                    BuildAutoStackSnapshot(false, false, 2),
                    runtime,
                    runtimeSettings);

                runtime.UpdateCount = 5;
                AutoStackService.Tick(
                    queue,
                    BuildAutoStackSnapshot(false, true, 3),
                    runtime,
                    runtimeSettings);

                var diagnostics = AutoStackService.GetDiagnostics();
                // Regression guard: unsafe UI must keep the pending transaction;
                // refreshing the baseline here would silently lose new stackable items.
                if (diagnostics == null ||
                    diagnostics.LastDecision.IndexOf("unsafe UI open", StringComparison.OrdinalIgnoreCase) < 0 ||
                    !string.Equals(diagnostics.LastDetectedItemIds, "12", StringComparison.Ordinal) ||
                    !string.Equals(diagnostics.LastPendingItemIds, "12", StringComparison.Ordinal) ||
                    diagnostics.PendingSinceTick != 5 ||
                    diagnostics.LastPendingChangeTick != 5 ||
                    !string.Equals(diagnostics.PendingTransactionState, "Detected", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected unsafe UI auto stack detection to retain a pending transaction.");
                }
            }
            finally
            {
                AutoStackService.ResetForTesting();
            }
        }

        private static void AutoStackSuccessfulActionResultClearsPendingTransaction()
        {
            AutoStackService.ResetForTesting();
            try
            {
                var settings = AppSettings.CreateDefault();
                settings.InventoryAutoStackEnabled = true;
                var runtimeSettings = RuntimeSettingsSnapshot.FromSettings(settings);
                var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
                executors[InputActionKind.Chest] = new TerminalFakeExecutor(InputActionKind.Chest, InputActionStatus.Succeeded, "auto stack moved items");
                var queue = new InputActionQueue(executors);
                var runtime = new RuntimeState { UpdateCount = 0 };
                var snapshot = BuildAutoStackSnapshot(false, false, 2);

                AutoStackService.Tick(queue, snapshot, runtime, runtimeSettings);

                runtime.UpdateCount = 5;
                snapshot = BuildAutoStackSnapshot(false, false, 3);
                AutoStackService.Tick(queue, snapshot, runtime, runtimeSettings);

                var submitted = AutoStackService.GetDiagnostics();
                // Admission is not proof of movement; only the ActionQueue terminal
                // result is allowed to clear pending auto-stack work.
                if (submitted == null ||
                    string.IsNullOrWhiteSpace(submitted.LastSubmitRequestId) ||
                    !string.Equals(submitted.LastPendingItemIds, "12", StringComparison.Ordinal) ||
                    !string.Equals(submitted.PendingTransactionState, "Admitted", StringComparison.Ordinal) ||
                    queue.GetSnapshot().PendingCount != 1)
                {
                    throw new InvalidOperationException("Expected auto stack to submit without clearing pending transaction.");
                }

                queue.Update(snapshot);
                runtime.UpdateCount = 10;
                AutoStackService.Tick(queue, snapshot, runtime, runtimeSettings);

                var completed = AutoStackService.GetDiagnostics();
                if (completed == null ||
                    !string.IsNullOrWhiteSpace(completed.LastPendingItemIds) ||
                    !string.Equals(completed.PendingTransactionState, "VerifiedMoved", StringComparison.Ordinal) ||
                    completed.LastPendingClearReason.IndexOf("verified", StringComparison.OrdinalIgnoreCase) < 0 ||
                    completed.LastResult.IndexOf("Succeeded", StringComparison.OrdinalIgnoreCase) < 0 ||
                    AutoStackService.HasPendingAutomationWork())
                {
                    throw new InvalidOperationException("Expected successful auto stack result to clear pending transaction.");
                }
            }
            finally
            {
                AutoStackService.ResetForTesting();
            }
        }

        private static void AutoStackUnverifiedActionResultKeepsRetryPending()
        {
            AutoStackService.ResetForTesting();
            try
            {
                var settings = AppSettings.CreateDefault();
                settings.InventoryAutoStackEnabled = true;
                var runtimeSettings = RuntimeSettingsSnapshot.FromSettings(settings);
                var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
                executors[InputActionKind.Chest] = new TerminalFakeExecutor(InputActionKind.Chest, InputActionStatus.AttemptedButUnverified, "quick stack invoked but not verified");
                var queue = new InputActionQueue(executors);
                var runtime = new RuntimeState { UpdateCount = 0 };
                var snapshot = BuildAutoStackSnapshot(false, false, 2);

                AutoStackService.Tick(queue, snapshot, runtime, runtimeSettings);

                runtime.UpdateCount = 5;
                snapshot = BuildAutoStackSnapshot(false, false, 3);
                AutoStackService.Tick(queue, snapshot, runtime, runtimeSettings);
                queue.Update(snapshot);

                runtime.UpdateCount = 10;
                AutoStackService.Tick(queue, snapshot, runtime, runtimeSettings);

                var diagnostics = AutoStackService.GetDiagnostics();
                // Regression guard: QuickStack can run without verified movement,
                // so AttemptedButUnverified must remain retry-pending.
                if (diagnostics == null ||
                    !string.Equals(diagnostics.LastPendingItemIds, "12", StringComparison.Ordinal) ||
                    !string.Equals(diagnostics.PendingTransactionState, "RetryPending", StringComparison.Ordinal) ||
                    diagnostics.PendingRetryCount != 1 ||
                    diagnostics.LastUnverifiedReason.IndexOf("not verified", StringComparison.OrdinalIgnoreCase) < 0 ||
                    diagnostics.LastResult.IndexOf("AttemptedButUnverified", StringComparison.OrdinalIgnoreCase) < 0 ||
                    !AutoStackService.HasPendingAutomationWork())
                {
                    throw new InvalidOperationException("Expected unverified auto stack result to keep a retry-pending transaction.");
                }

                runtime.UpdateCount = 11;
                AutoStackService.Tick(queue, snapshot, runtime, runtimeSettings);
                var waiting = AutoStackService.GetDiagnostics();
                if (waiting == null || waiting.LastDecision.IndexOf("retry backoff", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new InvalidOperationException("Expected auto stack retry to respect backoff instead of immediately resubmitting.");
                }
            }
            finally
            {
                AutoStackService.ResetForTesting();
            }
        }

        private static GameStateSnapshot BuildAutoStackSnapshot(bool playerInventoryOpen, bool chestOpen, int copperOreStack)
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
            snapshot.Ui.PlayerInventoryOpen = playerInventoryOpen;
            snapshot.Ui.ChestOpen = chestOpen;
            snapshot.Inventory.Items = new List<InventoryItemSnapshot>
            {
                BuildInventoryItem(1, 12, "铜矿", copperOreStack, 0, false)
            };
            return snapshot;
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

        private static void QuickItemHotkeyRequestUsesFreshClickMetadata()
        {
            var request = QuickItemHotkeyService.BuildUseRequestForTesting(12, 4263, "魔法海螺", 4263, "回家", "Ctrl+G");
            if (request.Kind != InputActionKind.UseHotbarItem)
            {
                throw new InvalidOperationException("Quick item hotkey must use UseHotbarItem action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.InventoryQuickItemHotkeys, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, "Hotkey.QuickItemHotkeys");
            AssertMetadata(request, ActionMetadataKeys.SourceKind, "Hotkey");
            AssertMetadata(request, "Slot", "12");
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "12");
            AssertMetadata(request, "ApplyMainMouseLeftForItemCheck", "true");
            AssertMetadata(request, "SourceHotkey", "Ctrl+G");
            AssertMetadata(request, "QuickItemDisplayName", "回家");
            AssertMetadata(request, "QuickItemItemType", "4263");
            AssertMetadata(request, "QuickItemItemName", "魔法海螺");
            AssertMetadata(request, "QuickItemBoundItemType", "4263");
            AssertMetadata(request, "TargetItemTypeOverride", "4263");
        }

        private static void QuickBagOpenYieldsAfterBatchWhenCleanupEnabled()
        {
            QuickBagOpenService.ClearState("test reset");
            QuickBagOpenService.BeginCleanupYieldForTesting(100, true);
            // Regression guard: cleanup yield protects adjacent inventory automation
            // without turning quick bag open into a long global blocker.
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
            AssertMetadata(request, "SourceContainer", "Inventory");
            AssertMetadata(request, "KeepFavoritedContainer", "Inventory");
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

        private static void KeepFavoritedRestoresArmorSlot()
        {
            KeepFavoritedCompat.ClearState();
            string container;
            int slot;
            int itemType;
            string signature;
            string message;
            var tracked = BuildKeepFavoritedSnapshot(
                true,
                new[] { BuildInventoryItem(6, 75, "生命水晶", 1, 0, true) },
                null);
            KeepFavoritedCompat.TryFindLostFavoritedSlot(tracked, 30, out container, out slot, out itemType, out signature, out message);

            var equipped = BuildKeepFavoritedSnapshot(
                true,
                new InventoryItemSnapshot[0],
                new[] { BuildInventoryItem(3, 75, "生命水晶", 1, 0, false) },
                null,
                null);
            if (!KeepFavoritedCompat.TryFindLostFavoritedSlot(equipped, 32, out container, out slot, out itemType, out signature, out message) ||
                container != "Armor" ||
                slot != 3 ||
                itemType != 75 ||
                signature != "75|0|生命水晶")
            {
                throw new InvalidOperationException("Keep favorited should restore a tracked favorite item after it moves into armor.");
            }

            var player = new FakePlayer();
            player.armor[3] = new FakeItem { type = 75, stack = 1, prefix = 0, Name = "生命水晶", favorited = false };
            bool restored;
            if (!KeepFavoritedCompat.TryRestoreFavoritedInContainer(player, container, slot, itemType, signature, out restored, out message) ||
                !restored ||
                !player.armor[3].favorited)
            {
                throw new InvalidOperationException("Keep favorited should set the armor item favorite flag through the controlled compat path: " + message);
            }

            KeepFavoritedCompat.ClearState();
        }

        private static void KeepFavoritedRestoresSameInventorySlotAfterLeaving()
        {
            KeepFavoritedCompat.ClearState();
            string container;
            int slot;
            int itemType;
            string signature;
            string message;
            var tracked = BuildKeepFavoritedSnapshot(
                true,
                new[] { BuildInventoryItem(6, 75, "生命水晶", 1, 0, true) },
                null);
            KeepFavoritedCompat.TryFindLostFavoritedSlot(tracked, 40, out container, out slot, out itemType, out signature, out message);

            var absent = BuildKeepFavoritedSnapshot(
                true,
                new InventoryItemSnapshot[0],
                null);
            KeepFavoritedCompat.TryFindLostFavoritedSlot(absent, 42, out container, out slot, out itemType, out signature, out message);

            var returned = BuildKeepFavoritedSnapshot(
                true,
                new[] { BuildInventoryItem(6, 75, "生命水晶", 1, 0, false) },
                null);
            if (!KeepFavoritedCompat.TryFindLostFavoritedSlot(returned, 44, out container, out slot, out itemType, out signature, out message) ||
                container != "Inventory" ||
                slot != 6 ||
                itemType != 75)
            {
                throw new InvalidOperationException("Keep favorited should not treat an item returning to the same inventory slot as a manual unfavorite.");
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

        private static void KeepFavoritedRestoresBucketTransformSameSlot()
        {
            KeepFavoritedCompat.ClearState();
            string container;
            int slot;
            int itemType;
            string signature;
            string message;
            var tracked = BuildKeepFavoritedSnapshot(
                true,
                new[] { BuildInventoryItem(6, 206, "Water Bucket", 1, 0, true) },
                null);
            KeepFavoritedCompat.TryFindLostFavoritedSlot(tracked, 50, out container, out slot, out itemType, out signature, out message);

            var usedBucket = BuildKeepFavoritedSnapshot(
                true,
                new[] { BuildInventoryItem(6, 205, "Empty Bucket", 1, 0, false) },
                null);
            if (!KeepFavoritedCompat.TryFindLostFavoritedSlot(usedBucket, 52, out container, out slot, out itemType, out signature, out message) ||
                container != "Inventory" ||
                slot != 6 ||
                itemType != 205 ||
                signature != "205|0|Empty Bucket")
            {
                throw new InvalidOperationException("Keep favorited should restore favorite state after a regular bucket transforms in the same slot.");
            }

            var player = new FakePlayer();
            player.inventory[6] = new FakeItem { type = 205, stack = 1, prefix = 0, Name = "Empty Bucket", favorited = false };
            bool restored;
            if (!KeepFavoritedCompat.TryRestoreFavoritedInContainer(player, container, slot, itemType, signature, out restored, out message) ||
                !restored ||
                !player.inventory[6].favorited)
            {
                throw new InvalidOperationException("Keep favorited should set the transformed bucket favorite flag through the controlled compat path: " + message);
            }

            KeepFavoritedCompat.ClearState();
        }

        private static GameStateSnapshot BuildKeepFavoritedSnapshot(bool inventoryOpen, IReadOnlyList<InventoryItemSnapshot> items, InventoryItemSnapshot trashItem)
        {
            return BuildKeepFavoritedSnapshot(inventoryOpen, items, null, null, trashItem);
        }

        private static GameStateSnapshot BuildKeepFavoritedSnapshot(
            bool inventoryOpen,
            IReadOnlyList<InventoryItemSnapshot> items,
            IReadOnlyList<InventoryItemSnapshot> armorItems,
            IReadOnlyList<InventoryItemSnapshot> miscEquipItems,
            InventoryItemSnapshot trashItem)
        {
            var snapshot = new GameStateSnapshot
            {
                IsInWorld = true
            };
            snapshot.Ui.PlayerInventoryOpen = inventoryOpen;
            snapshot.Inventory.Items = items ?? new List<InventoryItemSnapshot>();
            snapshot.Inventory.ArmorItems = armorItems ?? new List<InventoryItemSnapshot>();
            snapshot.Inventory.MiscEquipItems = miscEquipItems ?? new List<InventoryItemSnapshot>();
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

        private static void QuickReforgeCompletedRollUsesSucceededStatus()
        {
            InputActionStatus status;
            DiagnosticResultCode code;
            ReforgeActionExecutor.ResolveInvokedReforgeCompletionForTesting(false, out status, out code);
            if (status != InputActionStatus.Succeeded || code != DiagnosticResultCode.Succeeded)
            {
                throw new InvalidOperationException("Non-target quick reforge rolls must be successful intermediate clicks, not unverified failures.");
            }

            ReforgeActionExecutor.ResolveInvokedReforgeCompletionForTesting(true, out status, out code);
            if (status != InputActionStatus.Succeeded || code != DiagnosticResultCode.Succeeded)
            {
                throw new InvalidOperationException("Matched quick reforge rolls must stay successful.");
            }
        }

        private static void QuickReforgeSucceededRollDoesNotCreateCleanupLease()
        {
            var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
            executors[InputActionKind.Reforge] = new TerminalFakeExecutor(
                InputActionKind.Reforge,
                InputActionStatus.Succeeded,
                "reforge completed");
            var queue = new InputActionQueue(executors);
            var first = QuickReforgeService.BuildQuickReforgeRequestForTesting(new[] { "虚幻" }, "强力");
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(first, out admission))
            {
                throw new InvalidOperationException("Expected first quick reforge request to be admitted.");
            }

            queue.Update(null);
            if (queue.GetSnapshot().ActionQueueCleanupLeaseCount != 0)
            {
                throw new InvalidOperationException("Succeeded quick reforge rolls must not create cleanup leases.");
            }

            var second = QuickReforgeService.BuildQuickReforgeRequestForTesting(new[] { "虚幻" }, "暴怒");
            if (!queue.TryEnqueue(second, out admission))
            {
                throw new InvalidOperationException("Expected next quick reforge request to be admitted immediately after a succeeded roll: " + (admission == null ? "null" : admission.Reason));
            }
        }

        private static void QuickReforgeMatchedResultLocksCurrentHold()
        {
            QuickReforgeService.ResetForTesting();
            try
            {
                var executors = new Dictionary<InputActionKind, IInputActionExecutor>();
                executors[InputActionKind.Reforge] = new TerminalFakeExecutor(
                    InputActionKind.Reforge,
                    InputActionStatus.Succeeded,
                    "matched target prefix: 虚幻");
                var queue = new InputActionQueue(executors);
                var request = QuickReforgeService.BuildQuickReforgeRequestForTesting(new[] { "神话", "虚幻" }, "暴怒");
                InputActionAdmissionResult admission;
                if (!queue.TryEnqueue(request, out admission))
                {
                    throw new InvalidOperationException("Expected matched quick reforge request to be admitted.");
                }

                QuickReforgeService.RememberSubmittedRequestForTesting(request.RequestId);
                queue.Update(null);

                if (!QuickReforgeService.RefreshHoldSessionFromLastSubmittedResultForTesting(queue, new[] { "神话", "虚幻" }) ||
                    !QuickReforgeService.IsHoldSessionMatchedForTesting() ||
                    !string.Equals(QuickReforgeService.GetHoldSessionMatchedPrefixForTesting(), "虚幻", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Matched quick reforge result must lock the current held reforge session until release.");
                }
            }
            finally
            {
                QuickReforgeService.ResetForTesting();
            }
        }

        private static void QuickReforgeExistingTargetDoesNotLockCurrentHold()
        {
            QuickReforgeService.ResetForTesting();
            try
            {
                QuickReforgeService.RecordAlreadyMatchedCurrentPrefixForTesting(new[] { "虚幻" }, "虚幻");
                if (QuickReforgeService.IsHoldSessionMatchedForTesting())
                {
                    throw new InvalidOperationException("An already-matched current prefix must not lock the held vanilla reforge button.");
                }
            }
            finally
            {
                QuickReforgeService.ResetForTesting();
            }
        }

        private static void QuickReforgeCooldownClearsOnlyBeforeTargetMatch()
        {
            if (!ReforgeCompat.ShouldClearCooldownAfterReforgeForTesting(false))
            {
                throw new InvalidOperationException("Quick reforge must clear vanilla cooldown for non-target rolls to stay fast.");
            }

            if (ReforgeCompat.ShouldClearCooldownAfterReforgeForTesting(true) ||
                ReforgeCompat.GetMatchedTargetCooldownTicksForTesting() <= 0)
            {
                throw new InvalidOperationException("Quick reforge must hold a short vanilla cooldown after target match to avoid rolling past the target.");
            }
        }

        private static void AutoTaxCollectRequestUsesNpcMetadata()
        {
            var request = AutoTaxCollectorService.BuildAutoTaxCollectRequestForTesting(
                new TaxCollectorTarget
                {
                    NpcIndex = 6,
                    WhoAmI = 42,
                    Name = "Tax Collector",
                    TaxMoney = 12345
                });

            if (request.Kind != InputActionKind.NpcInteract)
            {
                throw new InvalidOperationException("Expected auto tax collect to use NpcInteract action.");
            }

            if (request.Priority != InputActionPriority.Low)
            {
                throw new InvalidOperationException("Expected auto tax collect to stay low priority.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.NpcAutoTaxCollect, "source feature id");
            AssertStringEquals(request.AdmissionKey, FeatureIds.NpcAutoTaxCollect, "admission key");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.NpcAutoTaxCollect);
            AssertMetadata(request, ActionMetadataKeys.SourceKind, "Automation");
            AssertMetadata(request, "ExecutionMode", "TaxCollectorChatCollect");
            AssertMetadata(request, "Interaction", "TaxCollect");
            AssertMetadata(request, "NpcIndex", "6");
            AssertMetadata(request, "NpcType", "441");
            AssertMetadata(request, "NpcWhoAmI", "42");
            AssertMetadata(request, "NpcName", "Tax Collector");
            AssertMetadata(request, "TaxMoneyBefore", "12345");
        }

    }
}
