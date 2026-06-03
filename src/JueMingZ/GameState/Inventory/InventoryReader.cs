using System;
using System.Collections.Generic;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using TerrariaItem = Terraria.Item;
using TerrariaPlayer = Terraria.Player;

namespace JueMingZ.GameState.Inventory
{
    public static class InventoryReader
    {
        private const int SnapshotInventorySlots = 58;
        private const int SnapshotArmorSlots = 20;
        private const int SnapshotMiscEquipSlots = 5;

        public static InventorySnapshot Read(TerrariaPlayer player)
        {
            return Read(player, InventoryReadProfile.Full);
        }

        public static InventorySnapshot Read(TerrariaPlayer player, InventoryReadProfile profile)
        {
            var snapshot = new InventorySnapshot();
            if (player == null || profile == InventoryReadProfile.None)
            {
                return snapshot;
            }

            try
            {
                var selectedItem = TerrariaPlayerReadCompat.SelectedItemSlot(player);
                snapshot.SelectedItemSlot = selectedItem;

                var inventory = TerrariaPlayerReadCompat.Inventory(player);
                if (inventory == null)
                {
                    return snapshot;
                }

                if (Has(profile, InventoryReadProfile.TrashItem))
                {
                    snapshot.TrashItem = ReadItem(TerrariaPlayerReadCompat.TrashItem(player), -2, profile);
                }

                if (Has(profile, InventoryReadProfile.InventorySlots))
                {
                    var max = Math.Min(inventory.Length, SnapshotInventorySlots);
                    var items = new List<InventoryItemSnapshot>(max);
                    for (var index = 0; index < max; index++)
                    {
                        var item = inventory[index];
                        var itemSnapshot = ReadItem(item, index, profile);
                        items.Add(itemSnapshot);
                        if (itemSnapshot.Type > 0 && itemSnapshot.Stack > 0)
                        {
                            snapshot.NonEmptyCount++;
                        }

                        if (index == selectedItem)
                        {
                            snapshot.SelectedItem = itemSnapshot;
                        }
                    }

                    snapshot.Items = items;
                }

                if (Has(profile, InventoryReadProfile.EquippedItems))
                {
                    snapshot.ArmorItems = ReadItems(TerrariaPlayerReadCompat.Armor(player), SnapshotArmorSlots, profile);
                    snapshot.MiscEquipItems = ReadItems(TerrariaPlayerReadCompat.MiscEquips(player), SnapshotMiscEquipSlots, profile);
                }

                if (Has(profile, InventoryReadProfile.SelectedItem) &&
                    (snapshot.SelectedItem == null || snapshot.SelectedItem.SlotIndex != selectedItem) &&
                    selectedItem >= 0 &&
                    selectedItem < Math.Min(inventory.Length, SnapshotInventorySlots))
                {
                    snapshot.SelectedItem = ReadItem(inventory[selectedItem], selectedItem, profile);
                }
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "inventory-state-read-failed",
                    TimeSpan.FromSeconds(30),
                    "InventoryReader",
                    "Inventory state read failed: " + error.Message);
            }

            return snapshot;
        }

        private static IReadOnlyList<InventoryItemSnapshot> ReadItems(TerrariaItem[] source, int maxSlots, InventoryReadProfile profile)
        {
            if (source == null || source.Length <= 0 || maxSlots <= 0)
            {
                return new List<InventoryItemSnapshot>();
            }

            var max = Math.Min(source.Length, maxSlots);
            var items = new List<InventoryItemSnapshot>(max);
            for (var index = 0; index < max; index++)
            {
                items.Add(ReadItem(source[index], index, profile));
            }

            return items;
        }

        public static InventorySnapshot ReadSelectedItem(TerrariaPlayer player)
        {
            return Read(player, InventoryReadProfile.SelectedOnly);
        }

        private static InventoryItemSnapshot ReadItem(TerrariaItem item, int slotIndex, InventoryReadProfile profile)
        {
            var snapshot = new InventoryItemSnapshot { SlotIndex = slotIndex };
            if (item == null)
            {
                return snapshot;
            }

            var type = TerrariaItemReadCompat.Type(item);
            var stack = TerrariaItemReadCompat.Stack(item);
            snapshot.Type = type;
            snapshot.Stack = stack;

            if (Has(profile, InventoryReadProfile.Stackability))
            {
                snapshot.MaxStack = TerrariaItemReadCompat.MaxStack(item);
            }

            if (Has(profile, InventoryReadProfile.Prefix))
            {
                snapshot.Prefix = TerrariaItemReadCompat.Prefix(item);
            }

            if (Has(profile, InventoryReadProfile.Favorited))
            {
                snapshot.Favorited = TerrariaItemReadCompat.IsFavorited(item);
            }

            if (Has(profile, InventoryReadProfile.Name))
            {
                snapshot.Name = TerrariaItemReadCompat.Name(item);
            }

            if (Has(profile, InventoryReadProfile.RecoveryFields))
            {
                snapshot.UseStyle = TerrariaItemReadCompat.UseStyle(item);
                snapshot.Consumable = TerrariaItemReadCompat.IsConsumable(item);
                snapshot.HealLife = TerrariaItemReadCompat.HealLife(item);
                snapshot.HealMana = TerrariaItemReadCompat.HealMana(item);
                snapshot.BuffType = TerrariaItemReadCompat.BuffType(item);
                snapshot.BuffTime = TerrariaItemReadCompat.BuffTime(item);
            }

            if (Has(profile, InventoryReadProfile.TileCreationFields))
            {
                snapshot.CreateTile = TerrariaItemReadCompat.CreateTile(item);
                snapshot.CreateWall = TerrariaItemReadCompat.CreateWall(item);
            }

            if (Has(profile, InventoryReadProfile.BugNetFields))
            {
                var catchTool = TerrariaItemReadCompat.CatchTool(item);
                if (catchTool <= 0)
                {
                    TerrariaBugNetCompat.TryResolveCatchToolTier(type, catchTool, out catchTool);
                }

                snapshot.CatchTool = catchTool;
            }

            if (Has(profile, InventoryReadProfile.EquipmentFields))
            {
                snapshot.Accessory = TerrariaItemReadCompat.IsAccessory(item);
                snapshot.WingSlot = TerrariaItemReadCompat.WingSlot(item);
                snapshot.Defense = TerrariaItemReadCompat.Defense(item);
            }

            if (Has(profile, InventoryReadProfile.ToolFields))
            {
                snapshot.Pick = TerrariaItemReadCompat.PickPower(item);
                snapshot.Axe = TerrariaItemReadCompat.AxePower(item);
                snapshot.Hammer = TerrariaItemReadCompat.HammerPower(item);
            }

            return snapshot;
        }

        private static bool Has(InventoryReadProfile profile, InventoryReadProfile flag)
        {
            return (profile & flag) == flag;
        }

    }
}
