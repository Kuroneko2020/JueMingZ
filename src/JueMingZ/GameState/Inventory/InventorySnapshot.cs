using System.Collections.Generic;

namespace JueMingZ.GameState.Inventory
{
    public sealed class InventorySnapshot
    {
        public int NonEmptyCount { get; set; }
        public int SelectedItemSlot { get; set; }
        public InventoryItemSnapshot SelectedItem { get; set; }
        public InventoryItemSnapshot TrashItem { get; set; }
        public IReadOnlyList<InventoryItemSnapshot> Items { get; set; }

        public InventorySnapshot()
        {
            SelectedItemSlot = -1;
            SelectedItem = new InventoryItemSnapshot { SlotIndex = -1 };
            TrashItem = new InventoryItemSnapshot { SlotIndex = -2 };
            Items = new List<InventoryItemSnapshot>();
        }
    }
}
