using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Compat
{
    public sealed class AutoDiscardInventoryCandidate
    {
        public int Slot { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int Stack { get; set; }

        public AutoDiscardInventoryCandidate()
        {
            ItemName = string.Empty;
        }
    }

    public sealed class AutoDiscardResult
    {
        public bool OriginalTrashSlotPathInvoked { get; set; }
        public int CandidateSlotCountBefore { get; set; }
        public int CandidateStackTotalBefore { get; set; }
        public int CandidateSlotCountAfter { get; set; }
        public int CandidateStackTotalAfter { get; set; }
        public int DiscardedSlotCount { get; set; }
        public int DiscardedStackTotal { get; set; }
        public int TrashItemTypeBefore { get; set; }
        public int TrashItemTypeAfter { get; set; }
        public string DiscardedSlots { get; set; }
        public string DiscardedItemIds { get; set; }
        public string Message { get; set; }

        public AutoDiscardResult()
        {
            DiscardedSlots = string.Empty;
            DiscardedItemIds = string.Empty;
            Message = string.Empty;
        }
    }

    public static class AutoDiscardCompat
    {
        // Trash movement stays on Terraria ItemSlot paths with before/after
        // verification; callers must not clear item stacks directly.
        private const int InventorySlotCount = 58;
        private const int InventoryItemSlotContext = 0;
        private const int TrashCursorOverride = 6;

        public static bool TryFindDiscardableInventorySlots(
            object player,
            ICollection<int> itemIds,
            out List<AutoDiscardInventoryCandidate> candidates,
            out string signature,
            out string message)
        {
            candidates = new List<AutoDiscardInventoryCandidate>();
            signature = string.Empty;
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            if (itemIds == null || itemIds.Count == 0)
            {
                message = "Auto discard item list is empty.";
                return true;
            }

            IList inventory;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) || inventory == null)
            {
                return false;
            }

            var max = Math.Min(InventorySlotCount, inventory.Count);
            for (var slot = 0; slot < max; slot++)
            {
                AutoDiscardInventoryCandidate candidate;
                if (TryReadDiscardableCandidate(inventory[slot], slot, itemIds, out candidate))
                {
                    candidates.Add(candidate);
                }
            }

            signature = BuildInventorySignature(candidates);
            if (candidates.Count == 0)
            {
                message = "No auto discard list items found in inventory.";
            }

            return true;
        }

        public static bool TryMoveInventorySlotsToTrash(
            ICollection<int> allowedItemIds,
            ICollection<int> sourceSlots,
            out AutoDiscardResult result)
        {
            result = new AutoDiscardResult();
            if (allowedItemIds == null || allowedItemIds.Count == 0)
            {
                result.Message = "Auto discard item id list is empty.";
                return false;
            }

            if (sourceSlots == null || sourceSlots.Count == 0)
            {
                result.Message = "Auto discard source slot list is empty.";
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                result.Message = "Local player unavailable for auto discard.";
                return false;
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                result.Message = "Mouse item is not empty.";
                return false;
            }

            var trashItemType = ReadTrashItemType(player);
            result.TrashItemTypeBefore = trashItemType;

            IList inventory;
            string inventoryMessage;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out inventoryMessage) || inventory == null)
            {
                result.Message = inventoryMessage;
                return false;
            }

            var slotFilter = new HashSet<int>(sourceSlots);
            List<AutoDiscardInventoryCandidate> beforeCandidates;
            CountMatchingSlots(inventory, allowedItemIds, slotFilter, out beforeCandidates);
            result.CandidateSlotCountBefore = beforeCandidates.Count;
            result.CandidateStackTotalBefore = SumStacks(beforeCandidates);
            if (result.CandidateSlotCountBefore == 0)
            {
                result.Message = "Auto discard source slots no longer contain matching items.";
                return false;
            }

            MethodInfo leftClick;
            if (!TryFindItemSlotLeftClick(out leftClick, out var leftClickMessage))
            {
                result.Message = leftClickMessage;
                return false;
            }

            foreach (var slot in sourceSlots)
            {
                if (slot < 0 || slot >= inventory.Count)
                {
                    continue;
                }

                AutoDiscardInventoryCandidate candidate;
                if (!TryReadDiscardableCandidate(inventory[slot], slot, allowedItemIds, out candidate))
                {
                    continue;
                }

                string invokeMessage;
                if (!TryInvokeTrashLeftClick(inventory, slot, leftClick, out invokeMessage))
                {
                    result.Message = invokeMessage;
                    continue;
                }

                result.OriginalTrashSlotPathInvoked = true;
                var discardedStack = CountDiscardedStack(inventory[slot], candidate);
                if (discardedStack <= 0)
                {
                    continue;
                }

                result.DiscardedSlotCount++;
                result.DiscardedStackTotal += discardedStack;
                var discardedSlots = result.DiscardedSlots;
                var discardedItemIds = result.DiscardedItemIds;
                AppendCsv(ref discardedSlots, candidate.Slot.ToString(CultureInfo.InvariantCulture));
                AppendCsv(ref discardedItemIds, candidate.ItemType.ToString(CultureInfo.InvariantCulture));
                result.DiscardedSlots = discardedSlots;
                result.DiscardedItemIds = discardedItemIds;
            }

            List<AutoDiscardInventoryCandidate> afterCandidates;
            CountMatchingSlots(inventory, allowedItemIds, slotFilter, out afterCandidates);
            result.CandidateSlotCountAfter = afterCandidates.Count;
            result.CandidateStackTotalAfter = SumStacks(afterCandidates);
            result.TrashItemTypeAfter = ReadTrashItemType(player);
            result.Message = result.DiscardedStackTotal > 0
                ? "Auto discard completed."
                : (string.IsNullOrWhiteSpace(result.Message) ? "Trash slot path invoked, but no matching item was discarded." : result.Message);
            return result.DiscardedStackTotal > 0;
        }

        private static bool TryReadDiscardableCandidate(
            object item,
            int slot,
            ICollection<int> itemIds,
            out AutoDiscardInventoryCandidate candidate)
        {
            candidate = null;
            if (item == null || itemIds == null || itemIds.Count == 0)
            {
                return false;
            }

            int itemType;
            int stack;
            int buffType;
            int buffTime;
            bool summon;
            string itemName;
            if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon) ||
                itemType <= 0 ||
                stack <= 0 ||
                !itemIds.Contains(itemType) ||
                IsCoin(itemType) ||
                IsFavorited(item))
            {
                return false;
            }

            candidate = new AutoDiscardInventoryCandidate
            {
                Slot = slot,
                ItemType = itemType,
                ItemName = itemName ?? string.Empty,
                Stack = stack
            };
            return true;
        }

        private static void CountMatchingSlots(
            IList inventory,
            ICollection<int> allowedItemIds,
            ICollection<int> sourceSlots,
            out List<AutoDiscardInventoryCandidate> candidates)
        {
            candidates = new List<AutoDiscardInventoryCandidate>();
            if (inventory == null || allowedItemIds == null || allowedItemIds.Count == 0)
            {
                return;
            }

            var max = Math.Min(InventorySlotCount, inventory.Count);
            for (var slot = 0; slot < max; slot++)
            {
                if (sourceSlots != null && sourceSlots.Count > 0 && !sourceSlots.Contains(slot))
                {
                    continue;
                }

                AutoDiscardInventoryCandidate candidate;
                if (TryReadDiscardableCandidate(inventory[slot], slot, allowedItemIds, out candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        private static bool TryFindItemSlotLeftClick(out MethodInfo leftClick, out string message)
        {
            leftClick = null;
            message = string.Empty;
            var itemSlotType = FindType("Terraria.UI.ItemSlot");
            if (itemSlotType == null)
            {
                message = "Terraria.UI.ItemSlot type was not found.";
                return false;
            }

            var methods = itemSlotType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "LeftClick", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 3 &&
                    parameters[0].ParameterType.IsArray &&
                    parameters[1].ParameterType == typeof(int) &&
                    parameters[2].ParameterType == typeof(int))
                {
                    leftClick = method;
                    return true;
                }
            }

            message = "ItemSlot.LeftClick inventory overload was not found.";
            return false;
        }

        private static bool TryInvokeTrashLeftClick(IList inventory, int slot, MethodInfo leftClick, out string message)
        {
            message = string.Empty;
            if (inventory == null || leftClick == null)
            {
                message = "Inventory or ItemSlot.LeftClick is unavailable.";
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            var previousCursorOverride = ReadStaticInt(mainType, "cursorOverride", 0);
            var previousMouseLeft = ReadStaticBool(mainType, "mouseLeft", false);
            var previousMouseLeftRelease = ReadStaticBool(mainType, "mouseLeftRelease", false);
            var previousMouseRight = ReadStaticBool(mainType, "mouseRight", false);
            var previousMouseRightRelease = ReadStaticBool(mainType, "mouseRightRelease", false);
            // Trash uses a transient ItemSlot click with cursor override; always
            // restore Main input flags before reporting the result.
            try
            {
                SetStatic(mainType, "cursorOverride", TrashCursorOverride);
                SetStatic(mainType, "mouseLeft", true);
                SetStatic(mainType, "mouseLeftRelease", true);
                SetStatic(mainType, "mouseRight", false);
                SetStatic(mainType, "mouseRightRelease", false);
                leftClick.Invoke(null, new object[] { inventory, InventoryItemSlotContext, slot });
                return true;
            }
            catch (Exception error)
            {
                message = "ItemSlot trash click failed: " + Unwrap(error);
                return false;
            }
            finally
            {
                SetStatic(mainType, "cursorOverride", previousCursorOverride);
                SetStatic(mainType, "mouseLeft", previousMouseLeft);
                SetStatic(mainType, "mouseLeftRelease", previousMouseLeftRelease);
                SetStatic(mainType, "mouseRight", previousMouseRight);
                SetStatic(mainType, "mouseRightRelease", previousMouseRightRelease);
            }
        }

        private static int CountDiscardedStack(object currentItem, AutoDiscardInventoryCandidate before)
        {
            if (before == null)
            {
                return 0;
            }

            int itemType;
            int stack;
            int buffType;
            int buffTime;
            bool summon;
            string itemName;
            if (!InventoryMutationCompat.TryReadItemFields(currentItem, out itemType, out itemName, out stack, out buffType, out buffTime, out summon) ||
                itemType <= 0 ||
                stack <= 0)
            {
                return Math.Max(0, before.Stack);
            }

            if (itemType != before.ItemType)
            {
                return Math.Max(0, before.Stack);
            }

            return Math.Max(0, before.Stack - stack);
        }

        private static int ReadTrashItemType(object player)
        {
            var trashItem = GetMember(player, "trashItem");
            int itemType;
            int stack;
            int buffType;
            int buffTime;
            bool summon;
            string itemName;
            return InventoryMutationCompat.TryReadItemFields(trashItem, out itemType, out itemName, out stack, out buffType, out buffTime, out summon) && stack > 0
                ? itemType
                : 0;
        }

        private static int SumStacks(List<AutoDiscardInventoryCandidate> candidates)
        {
            var total = 0;
            if (candidates == null)
            {
                return total;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                total += Math.Max(0, candidates[index].Stack);
            }

            return total;
        }

        private static string BuildInventorySignature(List<AutoDiscardInventoryCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return string.Empty;
            }

            var parts = new string[candidates.Count];
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                parts[index] = candidate.Slot.ToString(CultureInfo.InvariantCulture) +
                               ":" +
                               candidate.ItemType.ToString(CultureInfo.InvariantCulture) +
                               "x" +
                               candidate.Stack.ToString(CultureInfo.InvariantCulture);
            }

            return string.Join("|", parts);
        }

        private static bool IsCoin(int itemType)
        {
            return itemType >= 71 && itemType <= 74;
        }

        private static bool IsFavorited(object item)
        {
            bool favorited;
            return TryGetBool(item, "favorited", out favorited) && favorited;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    return field.GetValue(instance);
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, false, out var property))
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool TryGetBool(object instance, string name, out bool value)
        {
            value = false;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadStaticInt(Type type, string name, int fallback)
        {
            var raw = GetStatic(type, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool ReadStaticBool(Type type, string name, bool fallback)
        {
            var raw = GetStatic(type, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static object GetStatic(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }

            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    return field.GetValue(null);
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, true, out var property))
                {
                    return property.GetValue(null, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool SetStatic(Type type, string name, object value)
        {
            if (type == null)
            {
                return false;
            }

            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    field.SetValue(null, value);
                    return true;
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, true, out var property) && property.CanWrite)
                {
                    property.SetValue(null, value, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static void AppendCsv(ref string target, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            target = string.IsNullOrWhiteSpace(target) ? value : target + "," + value;
        }

        private static string Unwrap(Exception error)
        {
            return error == null ? string.Empty : (error.InnerException == null ? error.Message : error.InnerException.Message);
        }
    }
}
