using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public static bool TryGetSelectedItem(object player, out int selectedItem)
        {
            var ok = TerrariaPlayerSelectionCompat.TryGetSelectedItem(player, out selectedItem);
            return ok ? ClearSelectionError() : SelectionFail(TerrariaPlayerSelectionCompat.LastError);
        }

        public static bool TryGetMouseItem(out object mouseItem)
        {
            mouseItem = null;
            var mainType = TerrariaRuntimeTypes.MainType ?? FindType("Terraria.Main");
            if (mainType == null)
            {
                return Fail("Cannot read Main.mouseItem: Terraria.Main unavailable.");
            }

            // Main.mouseItem is a read-only profile source for selectedItem 58;
            // actual use still belongs to scoped ItemCheck takeover paths.
            mouseItem = GetStatic(mainType, "mouseItem");
            if (mouseItem == null)
            {
                return Fail("Cannot read Main.mouseItem.");
            }

            return ClearInputError();
        }

        public static bool TryReadMouseItemPresent(out bool present)
        {
            present = false;
            object mouseItem;
            if (!TryGetMouseItem(out mouseItem))
            {
                return false;
            }

            present = IsItemPresent(mouseItem);
            return ClearInputError();
        }

        public static bool TrySetSelectedItem(object player, int slot)
        {
            return TrySelectInventorySlot(player, slot);
        }

        public static bool TrySelectInventorySlot(object player, int slot)
        {
            if (!IsSupportedItemUseSlot(slot))
            {
                return Fail("Item use slot out of range: " + slot);
            }

            var ok = TerrariaPlayerSelectionCompat.TrySelectInventorySlot(player, slot);
            return ok ? ClearSelectionError() : SelectionFail(TerrariaPlayerSelectionCompat.LastError);
        }

        public static bool TryRequestInventorySlotSelection(object player, int slot, out bool selectedImmediately)
        {
            selectedImmediately = false;
            if (!IsSupportedItemUseSlot(slot))
            {
                return Fail("Item use slot out of range: " + slot);
            }

            var ok = TerrariaPlayerSelectionCompat.TryRequestInventorySlotSelection(player, slot, out selectedImmediately);
            return ok ? ClearSelectionError() : SelectionFail(TerrariaPlayerSelectionCompat.LastError);
        }

        public static bool TryForceInventorySlotSelection(object player, int slot)
        {
            if (!IsSupportedItemUseSlot(slot))
            {
                return Fail("Item use slot out of range: " + slot);
            }

            var ok = TerrariaPlayerSelectionCompat.TryForceInventorySlotSelection(player, slot);
            return ok ? ClearSelectionError() : SelectionFail(TerrariaPlayerSelectionCompat.LastError);
        }

        public static bool IsSupportedItemUseSlot(int slot)
        {
            return (slot >= 0 && slot < 50) || slot == 58;
        }
    }
}
