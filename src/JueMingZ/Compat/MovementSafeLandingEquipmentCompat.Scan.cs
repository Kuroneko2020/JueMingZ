using System;
using System.Collections;
using System.Collections.Generic;
using JueMingZ.Automation.Movement;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
        private sealed class SourceCandidate
        {
            public MovementSafeLandingEquipmentContainerKind Kind;
            public int Slot;
            public int SourcePriority;
            public object Item;
            public int ItemType;
            public int MountType;
            public MovementSafeLandingEquipmentItemSignature Signature;
        }

        private static List<SourceCandidate> ScanSources(object player, IList armor)
        {
            var result = new List<SourceCandidate>();
            IList inventory;
            if (TryGetInventoryItems(player, out inventory) && inventory != null)
            {
                var count = Math.Min(50, GetCollectionCount(inventory));
                for (var index = 0; index < count; index++)
                {
                    AddSourceCandidate(result, MovementSafeLandingEquipmentContainerKind.Inventory, index, 2, GetIndexed(inventory, index));
                }
            }

            if (armor != null)
            {
                var count = GetCollectionCount(armor);
                var socialArmorEnd = Math.Min(FirstSocialAccessorySlot, count);
                for (var index = FirstSocialArmorSlot; index < socialArmorEnd; index++)
                {
                    AddSourceCandidate(result, MovementSafeLandingEquipmentContainerKind.SocialArmor, index, 1, GetIndexed(armor, index));
                }

                for (var index = FirstSocialAccessorySlot; index < count; index++)
                {
                    AddSourceCandidate(result, MovementSafeLandingEquipmentContainerKind.SocialAccessory, index, 1, GetIndexed(armor, index));
                }
            }

            result.Sort(CompareSourceCandidate);
            return result;
        }

        private static int CompareSourceCandidate(SourceCandidate left, SourceCandidate right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var priorityCompare = left.SourcePriority.CompareTo(right.SourcePriority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            var kindCompare = ((int)left.Kind).CompareTo((int)right.Kind);
            return kindCompare != 0 ? kindCompare : left.Slot.CompareTo(right.Slot);
        }

        private static void AddSourceCandidate(
            List<SourceCandidate> result,
            MovementSafeLandingEquipmentContainerKind kind,
            int slot,
            int priority,
            object item)
        {
            if (result == null || item == null)
            {
                return;
            }

            var signature = CreateSignature(item);
            if (signature.IsAir)
            {
                return;
            }

            int itemType;
            if (!TryReadItemType(item, out itemType) || itemType <= 0)
            {
                return;
            }

            int mountType;
            TryReadItemMountType(item, out mountType);
            result.Add(new SourceCandidate
            {
                Kind = kind,
                Slot = slot,
                SourcePriority = priority,
                Item = item,
                ItemType = itemType,
                MountType = mountType,
                Signature = signature
            });
        }

        private static bool TryIsContainerSlotEmpty(object player, MovementSafeLandingEquipmentContainerKind kind, int slot)
        {
            object item;
            return TryGetContainerItem(player, kind, slot, out item) && CreateSignature(item).IsAir;
        }

        private static bool TryFindEmptyInventorySlot(object player, out int slot)
        {
            slot = -1;
            IList inventory;
            if (!TryGetInventoryItems(player, out inventory) || inventory == null)
            {
                return false;
            }

            var count = Math.Min(50, GetCollectionCount(inventory));
            for (var index = 0; index < count; index++)
            {
                if (CreateSignature(GetIndexed(inventory, index)).IsAir)
                {
                    slot = index;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetContainerItem(object player, MovementSafeLandingEquipmentContainerKind kind, int slot, out object item)
        {
            item = null;
            IList items;
            if (!TryGetContainerItems(player, kind, out items) || items == null || slot < 0 || slot >= GetCollectionCount(items))
            {
                return false;
            }

            item = GetIndexed(items, slot);
            return true;
        }

        private static bool TryGetContainerItems(object player, MovementSafeLandingEquipmentContainerKind kind, out IList items)
        {
            items = null;
            if (player == null)
            {
                return false;
            }

            if (kind == MovementSafeLandingEquipmentContainerKind.Inventory)
            {
                return TryGetInventoryItems(player, out items);
            }

            if (kind == MovementSafeLandingEquipmentContainerKind.Hotbar)
            {
                return TryGetInventoryItems(player, out items);
            }

            if (kind == MovementSafeLandingEquipmentContainerKind.Accessory ||
                kind == MovementSafeLandingEquipmentContainerKind.SocialArmor ||
                kind == MovementSafeLandingEquipmentContainerKind.SocialAccessory)
            {
                return TryGetArmorItems(player, out items);
            }

            if (kind == MovementSafeLandingEquipmentContainerKind.MiscEquip)
            {
                return TryGetMiscEquipItems(player, out items);
            }

            return false;
        }

        private static bool TryGetInventoryItems(object player, out IList items)
        {
            items = GetMember(player, "inventory") as IList;
            return items != null;
        }

        private static bool TryGetArmorItems(object player, out IList items)
        {
            items = GetMember(player, "armor") as IList;
            return items != null;
        }

        private static bool TryGetMiscEquipItems(object player, out IList items)
        {
            items = GetMember(player, "miscEquips") as IList;
            return items != null;
        }

        private static bool TryGetItemAt(IList items, int index, out object item)
        {
            item = null;
            if (items == null || index < 0 || index >= GetCollectionCount(items))
            {
                return false;
            }

            item = GetIndexed(items, index);
            return item != null;
        }

        private static object GetIndexed(object source, int index)
        {
            if (source == null || index < 0)
            {
                return null;
            }

            var list = source as IList;
            if (list != null)
            {
                return index < list.Count ? list[index] : null;
            }

            var array = source as Array;
            return array != null && array.Rank == 1 && index < array.GetLength(0)
                ? array.GetValue(index)
                : null;
        }

        private static int GetCollectionCount(object source)
        {
            var list = source as IList;
            if (list != null)
            {
                return list.Count;
            }

            var array = source as Array;
            return array == null || array.Rank != 1 ? 0 : array.GetLength(0);
        }
    }
}
