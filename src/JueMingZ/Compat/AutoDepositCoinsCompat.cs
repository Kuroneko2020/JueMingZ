using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Compat
{
    internal static class AutoDepositCoinsCompat
    {
        // Coin deposit may invoke vanilla ChestUI or the guarded piggy-bank
        // seed fallback; callers must verify results instead of editing stacks.
        private static readonly object ResolveSync = new object();
        private static bool _nearbyBanksResolved;
        private static MethodInfo _getBanksInRangeMethod;
        private static bool _moveCoinsResolved;
        private static MethodInfo _moveCoinsMethod;
        private static bool _visualizeCoinsResolved;
        private static MethodInfo _visualizeCoinsMethod;
        private static object _playerToChestVisualizationSettings;

        public static bool TryGetNearbyBankCount(object player, out int count)
        {
            count = 0;
            List<BankTarget> banks;
            if (!TryGetNearbyBanks(player, out banks))
            {
                return false;
            }

            count = banks.Count;
            return true;
        }

        public static bool TryMoveCoinsToNearbyBanks(
            object player,
            IReadOnlyList<int> coinItemIds,
            IReadOnlyList<int> inventorySlots,
            out QuestFishStorageResult result)
        {
            result = new QuestFishStorageResult();
            if (player == null)
            {
                result.Message = "Player unavailable.";
                return false;
            }

            var idSet = BuildPositiveItemIdSet(coinItemIds);
            if (idSet.Count == 0 || inventorySlots == null || inventorySlots.Count == 0)
            {
                result.Message = "No coin inventory slots to deposit.";
                return false;
            }

            List<BankTarget> banks;
            if (!TryGetNearbyBanks(player, out banks))
            {
                result.Message = "Nearby bank API unavailable.";
                return false;
            }

            result.NearbyContainerCountBefore = banks.Count;
            if (banks.Count <= 0)
            {
                result.Message = "No nearby bank containers in coin deposit range.";
                return false;
            }

            var inventory = GetMember(player, "inventory");
            if (inventory == null)
            {
                result.Message = "Player inventory unavailable.";
                return false;
            }

            result.InventoryCountBefore = CountMovableCoinStacks(inventory, idSet);
            result.SlotStackBefore = SumMovableCoinSlotStacks(inventory, idSet, inventorySlots);
            if (result.InventoryCountBefore <= 0 || result.SlotStackBefore <= 0)
            {
                result.Message = "No movable coin stacks found before bank deposit.";
                return false;
            }

            MethodInfo moveCoins;
            if (!TryResolveMoveCoins(out moveCoins))
            {
                result.Message = "ChestUI.MoveCoins method unavailable.";
                return false;
            }

            long movedCoins = 0;
            for (var index = 0; index < banks.Count; index++)
            {
                var target = banks[index];
                if (target == null || target.Chest == null)
                {
                    continue;
                }

                try
                {
                    var raw = moveCoins.Invoke(null, new object[] { inventory, target.Chest });
                    result.Invoked = true;
                    var moved = ToLong(raw, 0L);
                    movedCoins += moved;
                    if (moved > 0L)
                    {
                        TryVisualizeCoinTransfer(player, target.Position, moved);
                    }
                }
                catch (Exception error)
                {
                    result.Message = "ChestUI.MoveCoins invocation failed: " + error.Message;
                    return false;
                }
            }

            long seedMovedCoins = 0L;
            string seedMessage = string.Empty;
            if (movedCoins <= 0L &&
                TrySeedPersonalBankWithFirstCoinStack(player, inventory, idSet, inventorySlots, banks, out seedMovedCoins, out seedMessage))
            {
                result.FallbackInvoked = true;
                result.FallbackMode = "PiggyBankFirstCoin";
                if (seedMovedCoins > 0L)
                {
                    movedCoins += seedMovedCoins;
                }
            }

            result.NearbyContainerCountAfter = banks.Count;
            result.InventoryCountAfter = CountMovableCoinStacks(inventory, idSet);
            result.SlotStackAfter = SumMovableCoinSlotStacks(inventory, idSet, inventorySlots);
            result.InventoryCountDecreased = result.InventoryCountAfter < result.InventoryCountBefore ||
                                             result.SlotStackAfter < result.SlotStackBefore;

            if (result.InventoryCountDecreased)
            {
                result.Message = result.FallbackInvoked
                    ? "Auto deposit coins seeded empty piggy bank with first coin stack."
                    : "Auto deposit coins moved coins through ChestUI.MoveCoins.";
                return true;
            }

            if (!result.Invoked)
            {
                result.Message = "No nearby bank could invoke ChestUI.MoveCoins.";
                return false;
            }

            result.Message = movedCoins <= 0L
                ? string.IsNullOrWhiteSpace(seedMessage)
                    ? "No nearby bank accepted coins through ChestUI.MoveCoins."
                    : seedMessage
                : "ChestUI.MoveCoins reported coin movement without observable inventory decrease.";
            return movedCoins > 0L;
        }

        private static bool TryGetNearbyBanks(object player, out List<BankTarget> banks)
        {
            banks = new List<BankTarget>();
            if (player == null)
            {
                return false;
            }

            MethodInfo method;
            if (!TryResolveNearbyBanks(out method))
            {
                return false;
            }

            try
            {
                object raw;
                var parameters = method.GetParameters();
                if (parameters.Length == 1)
                {
                    raw = method.Invoke(null, new object[] { player });
                }
                else
                {
                    raw = method.Invoke(null, new object[] { player, 0f });
                }

                var count = GetCollectionCount(raw);
                for (var index = 0; index < count; index++)
                {
                    var positioned = GetIndexed(raw, index);
                    var chest = ResolveBankChest(positioned);
                    if (chest == null)
                    {
                        continue;
                    }

                    AddBankTarget(player, banks, chest, GetMember(positioned, "position") ?? GetMember(positioned, "Position"));
                }

                AddCurrentOpenBank(player, banks);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveNearbyBanks(out MethodInfo method)
        {
            lock (ResolveSync)
            {
                if (!_nearbyBanksResolved)
                {
                    _nearbyBanksResolved = true;
                    var type = FindType("Terraria.GameContent.NearbyChests") ?? FindTypeByName("NearbyChests");
                    if (type != null)
                    {
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        for (var index = 0; index < methods.Length; index++)
                        {
                            var candidate = methods[index];
                            if (!string.Equals(candidate.Name, "GetBanksInRangeOf", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            var parameters = candidate.GetParameters();
                            if (parameters.Length == 1 || parameters.Length == 2)
                            {
                                _getBanksInRangeMethod = candidate;
                                break;
                            }
                        }
                    }
                }

                method = _getBanksInRangeMethod;
                return method != null;
            }
        }

        private static bool TryResolveMoveCoins(out MethodInfo method)
        {
            lock (ResolveSync)
            {
                if (!_moveCoinsResolved)
                {
                    _moveCoinsResolved = true;
                    var type = FindType("Terraria.UI.ChestUI") ?? FindTypeByName("ChestUI");
                    if (type != null)
                    {
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        for (var index = 0; index < methods.Length; index++)
                        {
                            var candidate = methods[index];
                            if (!string.Equals(candidate.Name, "MoveCoins", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            var parameters = candidate.GetParameters();
                            if (parameters.Length == 2 &&
                                parameters[0].ParameterType.IsArray &&
                                string.Equals(parameters[1].ParameterType.Name, "Chest", StringComparison.Ordinal))
                            {
                                _moveCoinsMethod = candidate;
                                break;
                            }
                        }
                    }
                }

                method = _moveCoinsMethod;
                return method != null;
            }
        }

        private static void TryVisualizeCoinTransfer(object player, object bankPosition, long movedCoins)
        {
            if (player == null || bankPosition == null || movedCoins <= 0L)
            {
                return;
            }

            try
            {
                MethodInfo method;
                object settings;
                if (!TryResolveVisualizeCoins(out method, out settings))
                {
                    return;
                }

                var playerCenter = GetMember(player, "Center");
                if (playerCenter == null || settings == null)
                {
                    return;
                }

                method.Invoke(null, new object[] { playerCenter, bankPosition, movedCoins, settings });
            }
            catch
            {
            }
        }

        private static bool TryResolveVisualizeCoins(out MethodInfo method, out object settings)
        {
            lock (ResolveSync)
            {
                if (!_visualizeCoinsResolved)
                {
                    _visualizeCoinsResolved = true;
                    var type = FindType("Terraria.Chest") ?? FindTypeByName("Chest");
                    if (type != null)
                    {
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        for (var index = 0; index < methods.Length; index++)
                        {
                            var candidate = methods[index];
                            if (!string.Equals(candidate.Name, "VisualizeChestTransfer_CoinsBatch", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            var parameters = candidate.GetParameters();
                            if (parameters.Length == 4)
                            {
                                _visualizeCoinsMethod = candidate;
                                _playerToChestVisualizationSettings = GetStatic(parameters[3].ParameterType, "PlayerToChest");
                                break;
                            }
                        }
                    }
                }

                method = _visualizeCoinsMethod;
                settings = _playerToChestVisualizationSettings;
                return method != null && settings != null;
            }
        }

        private static object ResolveBankChest(object positionedOrChest)
        {
            if (positionedOrChest == null)
            {
                return null;
            }

            if (GetMember(positionedOrChest, "item") != null)
            {
                return positionedOrChest;
            }

            return GetMember(positionedOrChest, "chest") ??
                   GetMember(positionedOrChest, "Chest") ??
                   GetMember(positionedOrChest, "bank") ??
                   GetMember(positionedOrChest, "Bank");
        }

        private static void AddCurrentOpenBank(object player, List<BankTarget> banks)
        {
            var chestIndex = ReadInt(player, "chest", -1);
            object bank = null;
            if (chestIndex == -2)
            {
                bank = GetMember(player, "bank");
            }
            else if (chestIndex == -3)
            {
                bank = GetMember(player, "bank2");
            }
            else if (chestIndex == -4)
            {
                bank = GetMember(player, "bank3");
            }
            else if (chestIndex == -5)
            {
                bank = GetMember(player, "bank4");
            }

            if (bank != null)
            {
                AddBankTarget(player, banks, bank, null);
            }
        }

        private static void AddBankTarget(object player, List<BankTarget> banks, object chest, object position)
        {
            if (banks == null || chest == null)
            {
                return;
            }

            var isPersonalBank = ReferenceEquals(chest, GetMember(player, "bank"));
            for (var index = 0; index < banks.Count; index++)
            {
                if (ReferenceEquals(banks[index].Chest, chest))
                {
                    if (banks[index].Position == null && position != null)
                    {
                        banks[index].Position = position;
                    }

                    return;
                }
            }

            banks.Add(new BankTarget { Chest = chest, Position = position });
            banks[banks.Count - 1].IsPersonalBank = isPersonalBank;
        }

        private static bool TrySeedPersonalBankWithFirstCoinStack(
            object player,
            object inventory,
            HashSet<int> itemIds,
            IReadOnlyList<int> inventorySlots,
            List<BankTarget> banks,
            out long movedCoinValue,
            out string message)
        {
            movedCoinValue = 0L;
            message = string.Empty;

            var target = FindPersonalBankTarget(banks);
            if (target == null || target.Chest == null)
            {
                message = "No nearby piggy bank target is eligible for first coin seed deposit.";
                return false;
            }

            var bankItems = GetMember(target.Chest, "item");
            if (bankItems == null)
            {
                message = "Piggy bank item array unavailable for first coin seed deposit.";
                return false;
            }

            if (ContainsCoinStack(bankItems))
            {
                message = "Piggy bank already contains a coin stack; ChestUI.MoveCoins should handle it.";
                return false;
            }

            var emptySlot = FindEmptyContainerSlot(target.Chest, bankItems);
            if (emptySlot < 0)
            {
                message = "Piggy bank has no empty slot for first coin seed deposit.";
                return false;
            }

            // Direct seed is this Compat fallback's whole mutation boundary;
            // roll back the cloned bank slot if the source stack cannot clear.
            int sourceSlot;
            object sourceItem;
            if (!TryFindSeedCoinSource(inventory, itemIds, inventorySlots, out sourceSlot, out sourceItem))
            {
                message = "No movable source coin stack found for piggy bank seed deposit.";
                return false;
            }

            var cloned = TryCloneItem(sourceItem);
            if (cloned == null)
            {
                message = "Cannot clone source coin stack for piggy bank seed deposit.";
                return false;
            }

            if (!TrySetIndexed(bankItems, emptySlot, cloned))
            {
                message = "Cannot write first coin stack into piggy bank slot.";
                return false;
            }

            if (!TryClearItem(sourceItem))
            {
                TryClearItem(GetIndexed(bankItems, emptySlot));
                message = "Cannot clear source coin stack for piggy bank seed deposit.";
                return false;
            }

            movedCoinValue = CoinValue(ReadInt(cloned, "type", 0), ReadInt(cloned, "stack", 0));
            TryVisualizeCoinTransfer(player, target.Position, movedCoinValue);
            message = "Piggy bank first coin seed deposit moved slot " + sourceSlot.ToString(CultureInfo.InvariantCulture) + ".";
            return true;
        }

        private static BankTarget FindPersonalBankTarget(List<BankTarget> banks)
        {
            if (banks == null)
            {
                return null;
            }

            for (var index = 0; index < banks.Count; index++)
            {
                var target = banks[index];
                if (target != null && target.IsPersonalBank && target.Chest != null)
                {
                    return target;
                }
            }

            return null;
        }

        private static bool ContainsCoinStack(object items)
        {
            var count = GetCollectionCount(items);
            for (var index = 0; index < count; index++)
            {
                var item = GetIndexed(items, index);
                var type = ReadInt(item, "type", 0);
                if (type >= 71 && type <= 74 && ReadInt(item, "stack", 0) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindEmptyContainerSlot(object chest, object items)
        {
            var count = GetCollectionCount(items);
            var maxItems = ReadInt(chest, "maxItems", count);
            if (maxItems > 0)
            {
                count = Math.Min(count, maxItems);
            }

            for (var index = 0; index < count; index++)
            {
                var item = GetIndexed(items, index);
                if (item == null || ReadInt(item, "type", 0) <= 0 || ReadInt(item, "stack", 0) <= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool TryFindSeedCoinSource(
            object inventory,
            HashSet<int> itemIds,
            IReadOnlyList<int> inventorySlots,
            out int sourceSlot,
            out object sourceItem)
        {
            sourceSlot = -1;
            sourceItem = null;
            if (inventory == null || inventorySlots == null)
            {
                return false;
            }

            var bestType = 0;
            for (var index = 0; index < inventorySlots.Count; index++)
            {
                var slot = inventorySlots[index];
                if (slot < 0 || slot == 58)
                {
                    continue;
                }

                var item = GetIndexed(inventory, slot);
                if (!IsMovableCoin(item, itemIds))
                {
                    continue;
                }

                var type = ReadInt(item, "type", 0);
                if (sourceItem == null || type > bestType)
                {
                    sourceSlot = slot;
                    sourceItem = item;
                    bestType = type;
                }
            }

            return sourceItem != null;
        }

        private static object TryCloneItem(object item)
        {
            if (item == null)
            {
                return null;
            }

            try
            {
                var method = item.GetType().GetMethod("Clone", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                return method == null ? null : method.Invoke(item, null);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryClearItem(object item)
        {
            if (item == null)
            {
                return false;
            }

            try
            {
                var method = item.GetType().GetMethod("SetDefaults", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                if (method != null)
                {
                    method.Invoke(item, new object[] { 0 });
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static long CoinValue(int itemType, int stack)
        {
            if (stack <= 0)
            {
                return 0L;
            }

            if (itemType == 71)
            {
                return stack;
            }

            if (itemType == 72)
            {
                return stack * 100L;
            }

            if (itemType == 73)
            {
                return stack * 10000L;
            }

            return itemType == 74 ? stack * 1000000L : 0L;
        }

        private static int CountMovableCoinStacks(object inventory, HashSet<int> itemIds)
        {
            var total = 0;
            var count = GetCollectionCount(inventory);
            for (var slot = 0; slot < count; slot++)
            {
                if (slot == 58)
                {
                    continue;
                }

                var item = GetIndexed(inventory, slot);
                if (IsMovableCoin(item, itemIds))
                {
                    total += Math.Max(0, ReadInt(item, "stack", 0));
                }
            }

            return total;
        }

        private static int SumMovableCoinSlotStacks(object inventory, HashSet<int> itemIds, IReadOnlyList<int> slots)
        {
            var total = 0;
            if (slots == null)
            {
                return total;
            }

            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                if (slot < 0 || slot == 58)
                {
                    continue;
                }

                var item = GetIndexed(inventory, slot);
                if (IsMovableCoin(item, itemIds))
                {
                    total += Math.Max(0, ReadInt(item, "stack", 0));
                }
            }

            return total;
        }

        private static bool IsMovableCoin(object item, HashSet<int> itemIds)
        {
            if (item == null || ReadInt(item, "stack", 0) <= 0 || ReadBool(item, "favorited", false))
            {
                return false;
            }

            var type = ReadInt(item, "type", 0);
            return type >= 71 &&
                   type <= 74 &&
                   (itemIds == null || itemIds.Count == 0 || itemIds.Contains(type));
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
                var itemId = itemIds[index];
                if (itemId > 0)
                {
                    set.Add(itemId);
                }
            }

            return set;
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

        private static int ReadInt(object instance, string name, int fallback)
        {
            return ToInt(GetMember(instance, name), fallback);
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

        private static long ToLong(object raw, long fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt64(raw, CultureInfo.InvariantCulture);
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

        private static bool TrySetIndexed(object source, int index, object value)
        {
            if (source == null || index < 0)
            {
                return false;
            }

            try
            {
                var list = source as IList;
                if (list != null)
                {
                    if (index >= list.Count)
                    {
                        return false;
                    }

                    list[index] = value;
                    return true;
                }

                var array = source as Array;
                if (array == null || array.Rank != 1 || index >= array.GetLength(0))
                {
                    return false;
                }

                array.SetValue(value, index);
                return true;
            }
            catch
            {
                return false;
            }
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

        private sealed class BankTarget
        {
            public object Chest { get; set; }
            public object Position { get; set; }
            public bool IsPersonalBank { get; set; }
        }
    }
}
