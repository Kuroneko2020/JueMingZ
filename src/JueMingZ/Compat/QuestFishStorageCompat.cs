using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace JueMingZ.Compat
{
    internal sealed class QuestFishStorageResult
    {
        public bool Invoked { get; set; }
        public bool InventoryCountDecreased { get; set; }
        public int InventoryCountBefore { get; set; }
        public int InventoryCountAfter { get; set; }
        public int SlotStackBefore { get; set; }
        public int SlotStackAfter { get; set; }
        public int NearbyContainerCountBefore { get; set; }
        public int NearbyContainerCountAfter { get; set; }
        public bool FallbackInvoked { get; set; }
        public string FallbackMode { get; set; }
        public string Message { get; set; }

        public QuestFishStorageResult()
        {
            FallbackMode = string.Empty;
            Message = string.Empty;
        }
    }

    internal static class QuestFishStorageCompat
    {
        public static bool TryGetCurrentAnglerQuestFishId(out int itemId, out string message)
        {
            itemId = -1;
            message = string.Empty;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            int questIndex;
            if (!TryReadStaticInt(mainType, "anglerQuest", out questIndex))
            {
                message = "Main.anglerQuest unavailable.";
                return false;
            }

            var itemIds = GetStatic(mainType, "anglerQuestItemNetIDs");
            itemId = ToInt(GetIndexed(itemIds, questIndex), -1);
            if (itemId <= 0)
            {
                message = "Current angler quest fish id is invalid.";
                return false;
            }

            return true;
        }

        public static bool TryIsQuestFish(int itemId, out bool isQuestFish)
        {
            isQuestFish = false;
            if (itemId <= 0)
            {
                return false;
            }

            var setsType = FindType("Terraria.ID.ItemID+Sets");
            var set = GetStatic(setsType, "IsQuestFish");
            var raw = GetIndexed(set, itemId);
            if (raw == null)
            {
                return false;
            }

            try
            {
                isQuestFish = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryIsAnglerQuestFinished(out bool finished)
        {
            finished = false;
            var raw = GetStatic(TerrariaRuntimeTypes.MainType, "anglerQuestFinished");
            if (raw == null)
            {
                return false;
            }

            if (TryConvertBool(raw, out finished))
            {
                return true;
            }

            int myPlayer;
            TryReadStaticInt(TerrariaRuntimeTypes.MainType, "myPlayer", out myPlayer);
            return TryConvertBool(GetIndexed(raw, myPlayer), out finished);
        }

        public static bool TryFindInventoryQuestFishSlots(object player, int questFishId, out List<int> slots)
        {
            slots = new List<int>();
            var inventory = GetMember(player, "inventory");
            if (inventory == null || questFishId <= 0)
            {
                return false;
            }

            var count = Math.Min(58, GetCollectionCount(inventory));
            for (var slot = 0; slot < count; slot++)
            {
                var item = GetIndexed(inventory, slot);
                if (item == null)
                {
                    continue;
                }

                var type = ReadInt(item, "type", 0);
                var stack = ReadInt(item, "stack", 0);
                var favorited = ReadBool(item, "favorited", false);
                if (type == questFishId && stack > 0 && !favorited)
                {
                    slots.Add(slot);
                }
            }

            return true;
        }

        public static bool TryNearbyContainersContainQuestFish(object player, int questFishId, out bool found)
        {
            found = false;
            List<object> containers;
            if (!TryGetNearbyContainers(player, out containers))
            {
                return false;
            }

            for (var index = 0; index < containers.Count; index++)
            {
                if (ContainerContainsItem(containers[index], questFishId))
                {
                    found = true;
                    return true;
                }
            }

            return true;
        }

        public static bool TrySelectiveQuickStackQuestFish(
            object player,
            int questFishId,
            IReadOnlyList<int> inventorySlots,
            out QuestFishStorageResult result)
        {
            result = new QuestFishStorageResult();
            if (player == null)
            {
                result.Message = "Player unavailable.";
                return false;
            }

            if (questFishId <= 0 || inventorySlots == null || inventorySlots.Count == 0)
            {
                result.Message = "No quest fish inventory slots to quick stack.";
                return false;
            }

            List<object> nearbyBefore;
            if (!TryGetNearbyContainers(player, out nearbyBefore))
            {
                result.Message = "Nearby container API unavailable.";
                return false;
            }

            result.NearbyContainerCountBefore = nearbyBefore.Count;
            if (nearbyBefore.Count <= 0)
            {
                result.Message = "No nearby containers in quick stack range.";
                return false;
            }

            result.InventoryCountBefore = CountInventoryQuestFish(player, questFishId);
            result.SlotStackBefore = SumInventorySlotStacks(player, questFishId, inventorySlots);
            if (result.InventoryCountBefore <= 0 || result.SlotStackBefore <= 0)
            {
                result.Message = "No movable quest fish found before quick stack.";
                return false;
            }

            Type quickStackingType;
            Type contextType;
            MethodInfo method;
            if (!TryResolveQuickStacking(player, out quickStackingType, out contextType, out method))
            {
                result.Message = "Selective QuickStacking method not found.";
                return false;
            }

            object context;
            string contextMessage;
            if (!TryCreateQuickStackingContext(player, CreateSingleItemIdSet(questFishId), inventorySlots, contextType, false, out context, out contextMessage))
            {
                result.Message = contextMessage;
                return false;
            }

            try
            {
                method.Invoke(null, new[] { player, context, false });
                result.Invoked = true;
            }
            catch (Exception error)
            {
                result.Message = "Selective QuickStack invocation failed: " + error.Message;
                return false;
            }

            List<object> nearbyAfter;
            TryGetNearbyContainers(player, out nearbyAfter);
            result.NearbyContainerCountAfter = nearbyAfter == null ? 0 : nearbyAfter.Count;
            result.InventoryCountAfter = CountInventoryQuestFish(player, questFishId);
            result.SlotStackAfter = SumInventorySlotStacks(player, questFishId, inventorySlots);
            result.InventoryCountDecreased = result.InventoryCountAfter < result.InventoryCountBefore ||
                                             result.SlotStackAfter < result.SlotStackBefore;
            result.Message = result.InventoryCountDecreased
                ? "Selective quest fish quick stack reduced inventory quest fish count."
                : "Selective quest fish quick stack invoked without observable inventory decrease.";
            return true;
        }

        public static bool TryFindInventoryItemSlots(object player, IReadOnlyList<int> itemIds, out List<int> slots, out int stackTotal)
        {
            slots = new List<int>();
            stackTotal = 0;
            var idSet = BuildPositiveItemIdSet(itemIds);
            if (idSet.Count == 0)
            {
                return false;
            }

            var inventory = GetMember(player, "inventory");
            if (inventory == null)
            {
                return false;
            }

            var count = Math.Min(58, GetCollectionCount(inventory));
            for (var slot = 0; slot < count; slot++)
            {
                var item = GetIndexed(inventory, slot);
                if (item == null)
                {
                    continue;
                }

                var type = ReadInt(item, "type", 0);
                var stack = ReadInt(item, "stack", 0);
                var favorited = ReadBool(item, "favorited", false);
                if (idSet.Contains(type) && stack > 0 && !favorited)
                {
                    slots.Add(slot);
                    stackTotal += stack;
                }
            }

            return true;
        }

        public static bool TryBuildInventoryItemSignature(object player, IReadOnlyList<int> itemIds, out string signature, out int slotCount, out int stackTotal)
        {
            signature = string.Empty;
            slotCount = 0;
            stackTotal = 0;
            List<int> slots;
            if (!TryFindInventoryItemSlots(player, itemIds, out slots, out stackTotal))
            {
                return false;
            }

            slotCount = slots.Count;
            var inventory = GetMember(player, "inventory");
            var builder = new StringBuilder();
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                var item = GetIndexed(inventory, slot);
                if (item == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('|');
                }

                builder.Append(slot.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(ReadInt(item, "type", 0).ToString(CultureInfo.InvariantCulture));
                builder.Append('x');
                builder.Append(Math.Max(0, ReadInt(item, "stack", 0)).ToString(CultureInfo.InvariantCulture));
            }

            signature = builder.ToString();
            return true;
        }

        public static bool TryGetNearbyContainerCount(object player, out int count)
        {
            count = 0;
            if (player == null)
            {
                return false;
            }

            List<object> containers;
            if (!TryGetNearbyContainers(player, out containers))
            {
                return false;
            }

            count = containers == null ? 0 : containers.Count;
            return true;
        }

        public static bool TrySelectiveQuickStackItems(
            object player,
            IReadOnlyList<int> itemIds,
            IReadOnlyList<int> inventorySlots,
            out QuestFishStorageResult result)
        {
            return TrySelectiveQuickStackItems(player, itemIds, inventorySlots, false, out result);
        }

        public static bool TrySelectiveQuickStackStackableItems(
            object player,
            IReadOnlyList<int> itemIds,
            IReadOnlyList<int> inventorySlots,
            out QuestFishStorageResult result)
        {
            return TrySelectiveQuickStackItems(player, itemIds, inventorySlots, true, out result);
        }

        private static bool TrySelectiveQuickStackItems(
            object player,
            IReadOnlyList<int> itemIds,
            IReadOnlyList<int> inventorySlots,
            bool requireStackable,
            out QuestFishStorageResult result)
        {
            result = new QuestFishStorageResult();
            if (player == null)
            {
                result.Message = "Player unavailable.";
                return false;
            }

            var idSet = BuildPositiveItemIdSet(itemIds);
            if (idSet.Count == 0 || inventorySlots == null || inventorySlots.Count == 0)
            {
                result.Message = "No caught item inventory slots to quick stack.";
                return false;
            }

            List<object> nearbyBefore;
            if (!TryGetNearbyContainers(player, out nearbyBefore))
            {
                result.Message = "Nearby container API unavailable.";
                return false;
            }

            result.NearbyContainerCountBefore = nearbyBefore.Count;
            if (nearbyBefore.Count <= 0)
            {
                result.Message = "No nearby containers in quick stack range.";
                return false;
            }

            result.InventoryCountBefore = CountInventoryItems(player, idSet);
            result.SlotStackBefore = SumInventorySlotStacks(player, idSet, inventorySlots);
            if (result.InventoryCountBefore <= 0 || result.SlotStackBefore <= 0)
            {
                result.Message = "No movable caught items found before quick stack.";
                return false;
            }

            Type quickStackingType;
            Type contextType;
            MethodInfo method;
            if (!TryResolveQuickStacking(player, out quickStackingType, out contextType, out method))
            {
                result.Message = "Selective QuickStacking method not found.";
                return false;
            }

            object context;
            string contextMessage;
            if (!TryCreateQuickStackingContext(player, idSet, inventorySlots, contextType, requireStackable, out context, out contextMessage))
            {
                result.Message = contextMessage;
                return false;
            }

            try
            {
                method.Invoke(null, new[] { player, context, false });
                result.Invoked = true;
            }
            catch (Exception error)
            {
                result.Message = "Selective caught item QuickStack invocation failed: " + error.Message;
                return false;
            }

            List<object> nearbyAfter;
            TryGetNearbyContainers(player, out nearbyAfter);
            result.NearbyContainerCountAfter = nearbyAfter == null ? 0 : nearbyAfter.Count;
            result.InventoryCountAfter = CountInventoryItems(player, idSet);
            result.SlotStackAfter = SumInventorySlotStacks(player, idSet, inventorySlots);
            result.InventoryCountDecreased = result.InventoryCountAfter < result.InventoryCountBefore ||
                                             result.SlotStackAfter < result.SlotStackBefore;
            result.Message = result.InventoryCountDecreased
                ? "Selective caught item quick stack reduced inventory caught item count."
                : "Selective caught item quick stack invoked without observable inventory decrease.";
            return true;
        }

        public static bool TryBuildInventoryQuickStackSignature(object player, out string signature, out int slotCount, out int stackTotal)
        {
            signature = string.Empty;
            slotCount = 0;
            stackTotal = 0;
            List<int> slots;
            if (!TryFindInventoryQuickStackSlots(player, out slots, out stackTotal))
            {
                return false;
            }

            slotCount = slots.Count;
            var inventory = GetMember(player, "inventory");
            var builder = new StringBuilder();
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                var item = GetIndexed(inventory, slot);
                if (item == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('|');
                }

                builder.Append(slot.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(ReadInt(item, "type", 0).ToString(CultureInfo.InvariantCulture));
                builder.Append('x');
                builder.Append(Math.Max(0, ReadInt(item, "stack", 0)).ToString(CultureInfo.InvariantCulture));
            }

            signature = builder.ToString();
            return true;
        }

        private static bool TryResolveQuickStacking(object player, out Type quickStackingType, out Type contextType, out MethodInfo method)
        {
            quickStackingType = FindTypeByName("QuickStacking");
            contextType = null;
            method = null;
            if (quickStackingType == null || player == null)
            {
                return false;
            }

            var playerType = player.GetType();
            var nested = quickStackingType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            for (var index = 0; index < nested.Length; index++)
            {
                var candidate = nested[index];
                if (!string.Equals(candidate.Name, "SourceInventory", StringComparison.Ordinal))
                {
                    continue;
                }

                var methods = quickStackingType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (var methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                {
                    if (!string.Equals(methods[methodIndex].Name, "QuickStackToNearbyChests", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = methods[methodIndex].GetParameters();
                    if (parameters.Length == 3 &&
                        parameters[0].ParameterType.IsAssignableFrom(playerType) &&
                        parameters[1].ParameterType == candidate &&
                        parameters[2].ParameterType == typeof(bool))
                    {
                        contextType = candidate;
                        method = methods[methodIndex];
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryCreateQuickStackingContext(
            object player,
            HashSet<int> itemIdFilter,
            IReadOnlyList<int> inventorySlots,
            Type contextType,
            bool requireStackable,
            out object context,
            out string message)
        {
            context = null;
            message = string.Empty;
            if (contextType == null)
            {
                message = "QuickStack context type unavailable.";
                return false;
            }

            try
            {
                context = Activator.CreateInstance(contextType, true);
            }
            catch (Exception error)
            {
                message = "Cannot create QuickStack context: " + error.Message;
                return false;
            }

            var inventory = GetMember(player, "inventory");
            var sourceItems = new List<object>();
            var sourceSlotReferences = new List<object>();
            var playerItemSlotIdBase = ResolvePlayerInventorySlotIdBase();
            for (var index = 0; index < inventorySlots.Count; index++)
            {
                var slot = inventorySlots[index];
                var item = GetIndexed(inventory, slot);
                if (item == null ||
                    ReadInt(item, "stack", 0) <= 0 ||
                    ReadBool(item, "favorited", false) ||
                    requireStackable && !IsAutoStackStackableItem(item) ||
                    (itemIdFilter != null && itemIdFilter.Count > 0 && !itemIdFilter.Contains(ReadInt(item, "type", 0))))
                {
                    continue;
                }

                sourceItems.Add(item);
                object slotReference;
                if (!TryCreateSlotReference(contextType, player, playerItemSlotIdBase + slot, out slotReference))
                {
                    message = "Cannot construct QuickStack slot reference.";
                    return false;
                }

                sourceSlotReferences.Add(slotReference);
            }

            if (sourceItems.Count == 0)
            {
                message = requireStackable
                    ? "No matching stackable inventory items remained while building QuickStack context."
                    : "No matching inventory items remained while building QuickStack context.";
                return false;
            }

            if (!TrySetItemsArray(context, sourceItems) ||
                !TrySetIntMember(context, "numItems", sourceItems.Count) &&
                !TrySetIntMember(context, "itemCount", sourceItems.Count) &&
                !TrySetIntMember(context, "ItemCount", sourceItems.Count) ||
                !TrySetSlotReferencesArray(context, sourceSlotReferences) ||
                !TrySetBoolFlags(context, sourceItems.Count))
            {
                message = "QuickStack context did not expose required items/count/slotReferences/flags members.";
                return false;
            }

            var center = GetMember(player, "Center");
            if (center != null)
            {
                TrySetVectorLikeMember(context, center);
            }

            return true;
        }

        private static bool IsAutoStackStackableItem(object item)
        {
            if (item == null)
            {
                return false;
            }

            if (ReadBool(item, "accessory", false) ||
                ReadInt(item, "wingSlot", -1) > -1 ||
                ReadInt(item, "defense", 0) > 0)
            {
                return false;
            }

            var maxStack = ReadInt(item, "maxStack", 0);
            return maxStack > 1;
        }

        private static bool TryFindInventoryQuickStackSlots(object player, out List<int> slots, out int stackTotal)
        {
            slots = new List<int>();
            stackTotal = 0;
            var inventory = GetMember(player, "inventory");
            if (inventory == null)
            {
                return false;
            }

            var count = Math.Min(58, GetCollectionCount(inventory));
            for (var slot = 0; slot < count; slot++)
            {
                var item = GetIndexed(inventory, slot);
                if (item == null)
                {
                    continue;
                }

                var type = ReadInt(item, "type", 0);
                var stack = ReadInt(item, "stack", 0);
                var favorited = ReadBool(item, "favorited", false);
                if (type > 0 && stack > 0 && !favorited)
                {
                    slots.Add(slot);
                    stackTotal += stack;
                }
            }

            return true;
        }

        private static HashSet<int> CreateSingleItemIdSet(int itemId)
        {
            var set = new HashSet<int>();
            if (itemId > 0)
            {
                set.Add(itemId);
            }

            return set;
        }

        private static HashSet<int> BuildPositiveItemIdSet(IReadOnlyList<int> itemIds)
        {
            var set = new HashSet<int>();
            if (itemIds == null)
            {
                return set;
            }

            for (var index = 0; index < itemIds.Count; index++)
            {
                if (itemIds[index] > 0)
                {
                    set.Add(itemIds[index]);
                }
            }

            return set;
        }

        private static int CountInventoryItems(object player, HashSet<int> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0)
            {
                return 0;
            }

            var inventory = GetMember(player, "inventory");
            var count = Math.Min(58, GetCollectionCount(inventory));
            var total = 0;
            for (var index = 0; index < count; index++)
            {
                var item = GetIndexed(inventory, index);
                if (itemIds.Contains(ReadInt(item, "type", 0)))
                {
                    total += Math.Max(0, ReadInt(item, "stack", 0));
                }
            }

            return total;
        }

        private static int SumInventorySlotStacks(object player, HashSet<int> itemIds, IReadOnlyList<int> slots)
        {
            if (itemIds == null || itemIds.Count == 0 || slots == null)
            {
                return 0;
            }

            var inventory = GetMember(player, "inventory");
            var total = 0;
            for (var index = 0; index < slots.Count; index++)
            {
                var item = GetIndexed(inventory, slots[index]);
                if (itemIds.Contains(ReadInt(item, "type", 0)))
                {
                    total += Math.Max(0, ReadInt(item, "stack", 0));
                }
            }

            return total;
        }

        private static int ResolvePlayerInventorySlotIdBase()
        {
            var type = FindType("Terraria.ID.PlayerItemSlotID") ?? FindTypeByName("PlayerItemSlotID");
            var raw = GetStatic(type, "Inventory0");
            return ToInt(raw, 0);
        }

        private static bool TryCreateSlotReference(Type contextType, object player, int slotId, out object slotReference)
        {
            slotReference = null;
            var field = contextType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(value => value.FieldType.IsArray && value.Name.IndexOf("slot", StringComparison.OrdinalIgnoreCase) >= 0);
            var slotReferenceType = field == null ? null : field.FieldType.GetElementType();
            if (slotReferenceType == null)
            {
                return false;
            }

            try
            {
                var playerType = player == null ? null : player.GetType();
                var constructor = slotReferenceType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(value =>
                    {
                        var parameters = value.GetParameters();
                        return parameters.Length == 2 &&
                               playerType != null &&
                               parameters[0].ParameterType.IsAssignableFrom(playerType) &&
                               parameters[1].ParameterType == typeof(int);
                    });
                if (constructor != null)
                {
                    slotReference = constructor.Invoke(new object[] { player, slotId });
                    return true;
                }

                constructor = slotReferenceType.GetConstructor(new[] { typeof(int) });
                if (constructor != null)
                {
                    slotReference = constructor.Invoke(new object[] { slotId });
                    return true;
                }

                slotReference = Activator.CreateInstance(slotReferenceType, true);
                TrySetMemberByPredicate(slotReference, member => string.Equals(member.Name, "Player", StringComparison.OrdinalIgnoreCase), player);
                return TrySetIntMember(slotReference, "slotId", slotId) ||
                       TrySetIntMember(slotReference, "SlotId", slotId) ||
                       TrySetIntMember(slotReference, "id", slotId) ||
                       TrySetIntMember(slotReference, "Id", slotId);
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetItemsArray(object context, List<object> items)
        {
            var field = context.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(value =>
                {
                    if (!value.FieldType.IsArray)
                    {
                        return false;
                    }

                    var candidateElementType = value.FieldType.GetElementType();
                    return candidateElementType != null &&
                           (string.Equals(candidateElementType.FullName, "Terraria.Item", StringComparison.Ordinal) ||
                            string.Equals(candidateElementType.Name, "Item", StringComparison.Ordinal)) &&
                           value.Name.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0;
                });
            if (field == null)
            {
                return false;
            }

            var elementType = field.FieldType.GetElementType();
            var array = Array.CreateInstance(elementType, items.Count);
            for (var index = 0; index < items.Count; index++)
            {
                array.SetValue(items[index], index);
            }

            field.SetValue(context, array);
            return true;
        }

        private static bool TrySetSlotReferencesArray(object context, List<object> slotReferences)
        {
            var field = context.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(value =>
                {
                    if (!value.FieldType.IsArray || value.Name.IndexOf("slot", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return false;
                    }

                    var candidateElementType = value.FieldType.GetElementType();
                    return candidateElementType != null &&
                           (candidateElementType.Name.IndexOf("SlotReference", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            candidateElementType.FullName.IndexOf("PlayerItemSlotID+SlotReference", StringComparison.OrdinalIgnoreCase) >= 0);
                });
            if (field == null)
            {
                return false;
            }

            var elementType = field.FieldType.GetElementType();
            var array = Array.CreateInstance(elementType, slotReferences.Count);
            for (var index = 0; index < slotReferences.Count; index++)
            {
                array.SetValue(slotReferences[index], index);
            }

            field.SetValue(context, array);
            return true;
        }

        private static bool TrySetBoolFlags(object context, int count)
        {
            var field = context.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(value => value.FieldType == typeof(bool[]) || value.FieldType == typeof(Boolean[]));
            if (field == null)
            {
                return true;
            }

            var flags = new bool[count];
            for (var index = 0; index < flags.Length; index++)
            {
                flags[index] = false;
            }

            field.SetValue(context, flags);
            return true;
        }

        private static bool TrySetVectorLikeMember(object instance, object value)
        {
            if (instance == null || value == null)
            {
                return false;
            }

            var valueType = value.GetType();
            return TrySetMemberByPredicate(
                instance,
                member =>
                {
                    var field = member as FieldInfo;
                    if (field != null)
                    {
                        return field.FieldType == valueType ||
                               field.Name.IndexOf("position", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               field.Name.IndexOf("center", StringComparison.OrdinalIgnoreCase) >= 0;
                    }

                    var property = member as PropertyInfo;
                    return property != null &&
                           property.CanWrite &&
                           (property.PropertyType == valueType ||
                            property.Name.IndexOf("position", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            property.Name.IndexOf("center", StringComparison.OrdinalIgnoreCase) >= 0);
                },
                value);
        }

        private static bool TrySetMemberByPredicate(object instance, Func<MemberInfo, bool> predicate, object value)
        {
            if (instance == null)
            {
                return false;
            }

            var type = instance.GetType();
            var field = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(member => predicate(member));
            if (field != null)
            {
                field.SetValue(instance, value);
                return true;
            }

            var property = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(member => predicate(member) && member.CanWrite);
            if (property != null)
            {
                property.SetValue(instance, value, null);
                return true;
            }

            return false;
        }

        private static bool TrySetIntMember(object instance, string name, int value)
        {
            if (instance == null)
            {
                return false;
            }

            var type = instance.GetType();
            try
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(instance, Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
                    return true;
                }

                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture), null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryGetNearbyContainers(object player, out List<object> containers)
        {
            containers = new List<object>();
            var type = FindTypeByName("NearbyChests");
            if (type == null || player == null)
            {
                return false;
            }

            var position = GetMember(player, "position");
            InvokeNearby(type, "GetChestsInRangeOf", new[] { position, 0f }, containers);
            InvokeNearby(type, "GetBanksInRangeOf", new[] { player, 0f }, containers);
            return true;
        }

        private static void InvokeNearby(Type nearbyChestsType, string methodName, object[] args, List<object> containers)
        {
            try
            {
                var method = nearbyChestsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(value => string.Equals(value.Name, methodName, StringComparison.Ordinal) &&
                                             value.GetParameters().Length == args.Length);
                if (method == null)
                {
                    return;
                }

                var result = method.Invoke(null, args);
                var count = GetCollectionCount(result);
                for (var index = 0; index < count; index++)
                {
                    var container = ResolveContainer(GetIndexed(result, index));
                    if (container != null)
                    {
                        containers.Add(container);
                    }
                }
            }
            catch
            {
            }
        }

        private static object ResolveContainer(object positionedOrContainer)
        {
            if (positionedOrContainer == null)
            {
                return null;
            }

            if (GetMember(positionedOrContainer, "item") != null)
            {
                return positionedOrContainer;
            }

            foreach (var name in new[] { "Chest", "chest", "Container", "container", "Bank", "bank" })
            {
                var value = GetMember(positionedOrContainer, name);
                if (value != null && GetMember(value, "item") != null)
                {
                    return value;
                }
            }

            var index = ReadInt(positionedOrContainer, "chest", -1);
            if (index < 0)
            {
                index = ReadInt(positionedOrContainer, "ChestIndex", -1);
            }

            if (index >= 0)
            {
                return GetIndexed(GetStatic(TerrariaRuntimeTypes.MainType, "chest"), index);
            }

            return null;
        }

        private static bool ContainerContainsItem(object container, int itemId)
        {
            var items = GetMember(container, "item");
            var count = GetCollectionCount(items);
            for (var index = 0; index < count; index++)
            {
                var item = GetIndexed(items, index);
                if (ReadInt(item, "type", 0) == itemId && ReadInt(item, "stack", 0) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountInventoryQuestFish(object player, int questFishId)
        {
            var inventory = GetMember(player, "inventory");
            var count = Math.Min(58, GetCollectionCount(inventory));
            var total = 0;
            for (var index = 0; index < count; index++)
            {
                var item = GetIndexed(inventory, index);
                if (ReadInt(item, "type", 0) == questFishId)
                {
                    total += Math.Max(0, ReadInt(item, "stack", 0));
                }
            }

            return total;
        }

        private static int SumInventorySlotStacks(object player, int questFishId, IReadOnlyList<int> slots)
        {
            var inventory = GetMember(player, "inventory");
            var total = 0;
            for (var index = 0; index < slots.Count; index++)
            {
                var item = GetIndexed(inventory, slots[index]);
                if (ReadInt(item, "type", 0) == questFishId)
                {
                    total += Math.Max(0, ReadInt(item, "stack", 0));
                }
            }

            return total;
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

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

        private static Type FindTypeByName(string name)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch
                {
                    continue;
                }

                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var type = types[typeIndex];
                    if (string.Equals(type.Name, name, StringComparison.Ordinal) ||
                        string.Equals(type.FullName, "Terraria." + name, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static object GetStatic(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }

            if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
            {
                return field.GetValue(null);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, true, out var property) && property.CanRead
                ? property.GetValue(null, null)
                : null;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
            {
                return field.GetValue(instance);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property) && property.CanRead
                ? property.GetValue(instance, null)
                : null;
        }

        private static bool TryReadStaticInt(Type type, string name, out int value)
        {
            value = 0;
            var raw = GetStatic(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            var raw = GetMember(instance, name);
            return ToInt(raw, fallback);
        }

        private static bool ReadBool(object instance, string name, bool fallback)
        {
            bool value;
            return TryConvertBool(GetMember(instance, name), out value) ? value : fallback;
        }

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
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

        private static int ToInt(object raw, int fallback)
        {
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
