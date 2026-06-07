using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Compat
{
    public sealed class AutoSellShopTarget
    {
        public int NpcIndex { get; set; }
        public int NpcType { get; set; }
        public int ShopIndex { get; set; }
        public string Name { get; set; }

        public AutoSellShopTarget()
        {
            NpcIndex = -1;
            Name = string.Empty;
        }
    }

    public sealed class AutoSellInventoryCandidate
    {
        public int Slot { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int Stack { get; set; }
        public int Value { get; set; }

        public AutoSellInventoryCandidate()
        {
            ItemName = string.Empty;
        }
    }

    public sealed class AutoSellResult
    {
        public int NpcIndex { get; set; }
        public int NpcType { get; set; }
        public int ShopIndex { get; set; }
        public string NpcName { get; set; }
        public bool ShopOpened { get; set; }
        public bool ShopRestored { get; set; }
        public bool ShopLeftOpen { get; set; }
        public bool SellInvoked { get; set; }
        public int CandidateSlotCountBefore { get; set; }
        public int CandidateStackTotalBefore { get; set; }
        public int CandidateSlotCountAfter { get; set; }
        public int CandidateStackTotalAfter { get; set; }
        public int SoldSlotCount { get; set; }
        public int SoldStackTotal { get; set; }
        public int ZeroValueRemovedCount { get; set; }
        public string SoldSlots { get; set; }
        public string SoldItemIds { get; set; }
        public string Message { get; set; }

        public AutoSellResult()
        {
            NpcIndex = -1;
            NpcName = string.Empty;
            SoldSlots = string.Empty;
            SoldItemIds = string.Empty;
            Message = string.Empty;
        }
    }

    public static class AutoSellCompat
    {
        // Shop selling opens vanilla shop state and verifies stack deltas;
        // business services must not remove items directly.
        public static readonly int[] DefaultAutoSellItemIds = { 2337, 2338, 2339 };

        private static readonly Dictionary<int, int> ShopIndexByNpcType = new Dictionary<int, int>
        {
            { 17, 1 },
            { 19, 2 },
            { 20, 3 },
            { 38, 4 },
            { 54, 5 },
            { 107, 6 },
            { 108, 7 },
            { 124, 8 },
            { 142, 9 },
            { 160, 10 },
            { 178, 11 },
            { 207, 12 },
            { 208, 13 },
            { 209, 14 },
            { 227, 15 },
            { 228, 16 },
            { 229, 17 },
            { 353, 18 },
            { 368, 19 },
            { 453, 20 },
            { 550, 21 },
            { 588, 22 },
            { 633, 23 },
            { 663, 24 }
        };

        public static bool TryFindReachableShopNpc(object player, out AutoSellShopTarget target, out string message)
        {
            target = null;
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            var npcs = GetStatic(TerrariaRuntimeTypes.MainType, "npc") as IList;
            if (npcs == null)
            {
                message = "Main.npc is unavailable.";
                return false;
            }

            var playerCenterX = ReadCenterX(player);
            var playerCenterY = ReadCenterY(player);
            var bestDistance = float.MaxValue;
            for (var index = 0; index < npcs.Count; index++)
            {
                var npc = npcs[index];
                if (npc == null || !ReadBool(npc, "active", false))
                {
                    continue;
                }

                var npcType = ReadInt(npc, "type", 0);
                int shopIndex;
                if (!ShopIndexByNpcType.TryGetValue(npcType, out shopIndex))
                {
                    continue;
                }

                if (!IsNpcReachable(player, npc))
                {
                    continue;
                }

                var dx = ReadCenterX(npc) - playerCenterX;
                var dy = ReadCenterY(npc) - playerCenterY;
                var distance = dx * dx + dy * dy;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                target = new AutoSellShopTarget
                {
                    NpcIndex = index,
                    NpcType = npcType,
                    ShopIndex = shopIndex,
                    Name = ReadName(npc)
                };
            }

            if (target == null)
            {
                message = "No reachable shop NPC found.";
                return false;
            }

            return true;
        }

        public static bool TryFindSellableInventorySlots(
            object player,
            ICollection<int> itemIds,
            out List<AutoSellInventoryCandidate> candidates,
            out string signature,
            out string message)
        {
            candidates = new List<AutoSellInventoryCandidate>();
            signature = string.Empty;
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            if (itemIds == null || itemIds.Count == 0)
            {
                message = "Auto sell item list is empty.";
                return true;
            }

            IList inventory;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) || inventory == null)
            {
                return false;
            }

            var max = Math.Min(58, inventory.Count);
            for (var slot = 0; slot < max; slot++)
            {
                AutoSellInventoryCandidate candidate;
                if (TryReadSellableCandidate(inventory[slot], slot, itemIds, out candidate))
                {
                    candidates.Add(candidate);
                }
            }

            signature = BuildInventorySignature(candidates);
            if (candidates.Count == 0)
            {
                message = "No auto sell list items found in inventory.";
            }

            return true;
        }

        public static bool TryOpenShopAndSell(
            int npcIndex,
            int shopIndex,
            ICollection<int> allowedItemIds,
            ICollection<int> sourceSlots,
            out AutoSellResult result)
        {
            result = new AutoSellResult
            {
                NpcIndex = npcIndex,
                ShopIndex = shopIndex
            };

            if (allowedItemIds == null || allowedItemIds.Count == 0)
            {
                result.Message = "Auto sell item id list is empty.";
                return false;
            }

            if (sourceSlots == null || sourceSlots.Count == 0)
            {
                result.Message = "Auto sell source slot list is empty.";
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                result.Message = "Local player unavailable for auto sell.";
                return false;
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                result.Message = "Mouse item is not empty.";
                return false;
            }

            var npcs = GetStatic(TerrariaRuntimeTypes.MainType, "npc") as IList;
            if (npcs == null || npcIndex < 0 || npcIndex >= npcs.Count)
            {
                result.Message = "NPC index is outside Main.npc bounds.";
                return false;
            }

            var npc = npcs[npcIndex];
            if (npc == null || !ReadBool(npc, "active", false))
            {
                result.Message = "Target shop NPC is no longer active.";
                return false;
            }

            result.NpcType = ReadInt(npc, "type", 0);
            result.NpcName = ReadName(npc);
            int expectedShopIndex;
            if (!ShopIndexByNpcType.TryGetValue(result.NpcType, out expectedShopIndex))
            {
                result.Message = "Target NPC type does not expose a supported shop.";
                return false;
            }

            if (shopIndex <= 0)
            {
                shopIndex = expectedShopIndex;
                result.ShopIndex = shopIndex;
            }

            if (shopIndex != expectedShopIndex)
            {
                result.Message = "Target shop index no longer matches NPC type.";
                return false;
            }

            if (!IsNpcReachable(player, npc))
            {
                result.Message = "Target shop NPC is no longer reachable.";
                return false;
            }

            IList inventory;
            string inventoryMessage;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out inventoryMessage) || inventory == null)
            {
                result.Message = inventoryMessage;
                return false;
            }

            List<AutoSellInventoryCandidate> beforeCandidates;
            var slotFilter = new HashSet<int>(sourceSlots);
            CountMatchingSlots(inventory, allowedItemIds, slotFilter, out beforeCandidates);
            result.CandidateSlotCountBefore = beforeCandidates.Count;
            result.CandidateStackTotalBefore = SumStacks(beforeCandidates);
            if (result.CandidateSlotCountBefore == 0)
            {
                result.Message = "Auto sell source slots no longer contain matching items.";
                return false;
            }

            var state = CaptureShopState(player);
            var opened = false;
            var leaveShopOpen = false;
            try
            {
                if (!TryApplyShoppingSettings(player, npc))
                {
                    result.Message = "Cannot apply Terraria shopping settings.";
                    return false;
                }

                if (!TrySetTalkNpc(player, npcIndex))
                {
                    result.Message = "Cannot set player talkNPC for shop.";
                    return false;
                }

                if (!TryOpenShop(shopIndex, out var openMessage))
                {
                    result.Message = openMessage;
                    return false;
                }

                opened = true;
                result.ShopOpened = true;

                object shopChest;
                MethodInfo addItemToShop;
                if (!TryGetCurrentShopChest(out shopChest, out addItemToShop, out var shopMessage))
                {
                    result.Message = shopMessage;
                    return false;
                }

                foreach (var slot in sourceSlots)
                {
                    if (slot < 0 || slot >= inventory.Count)
                    {
                        continue;
                    }

                    var item = inventory[slot];
                    AutoSellInventoryCandidate candidate;
                    if (!TryReadSellableCandidate(item, slot, allowedItemIds, out candidate))
                    {
                        continue;
                    }

                    if (TrySellItem(player, shopChest, addItemToShop, item, candidate, result))
                    {
                        result.SellInvoked = true;
                    }
                }

                CountMatchingSlots(inventory, allowedItemIds, slotFilter, out var afterCandidates);
                result.CandidateSlotCountAfter = afterCandidates.Count;
                result.CandidateStackTotalAfter = SumStacks(afterCandidates);

                leaveShopOpen = ShouldLeaveShopOpenAfterSuccess(state, result);
                result.ShopLeftOpen = leaveShopOpen;
                result.Message = result.SoldStackTotal > 0
                    ? (leaveShopOpen ? "Auto sell completed; shop left open." : "Auto sell completed.")
                    : "Shop opened, but no matching item could be sold.";
                return result.SoldStackTotal > 0;
            }
            finally
            {
                // Shop state is restored unless success intentionally leaves the
                // vanilla shop visible; failures must not leak talkNPC/NPCShop.
                if (leaveShopOpen)
                {
                    result.ShopRestored = false;
                }
                else
                {
                    result.ShopRestored = RestoreShopState(player, state, opened);
                }
            }
        }

        private static bool ShouldLeaveShopOpenAfterSuccess(AutoSellShopState state, AutoSellResult result)
        {
            return state != null &&
                   result != null &&
                   result.ShopOpened &&
                   result.SoldStackTotal > 0 &&
                   !state.PlayerInventoryOpen &&
                   state.NpcShop <= 0;
        }

        private static bool TrySellItem(
            object player,
            object shopChest,
            MethodInfo addItemToShop,
            object item,
            AutoSellInventoryCandidate candidate,
            AutoSellResult result)
        {
            if (player == null || shopChest == null || addItemToShop == null || item == null || candidate == null)
            {
                return false;
            }

            var soldForValue = false;
            if (candidate.Value > 0)
            {
                soldForValue = InvokeSellItem(player, item);
                if (!soldForValue)
                {
                    return false;
                }
            }
            else if (candidate.Value == 0)
            {
                result.ZeroValueRemovedCount++;
            }
            else
            {
                return false;
            }

            try
            {
                addItemToShop.Invoke(shopChest, new[] { item });
                // After the vanilla sell path accepts the item, clear only this
                // verified source item; callers must not bulk-edit inventory.
                if (!TryTurnToAir(item))
                {
                    return false;
                }

                result.SoldSlotCount++;
                result.SoldStackTotal += Math.Max(1, candidate.Stack);
                var soldSlots = result.SoldSlots;
                var soldItemIds = result.SoldItemIds;
                AppendCsv(ref soldSlots, candidate.Slot.ToString(CultureInfo.InvariantCulture));
                AppendCsv(ref soldItemIds, candidate.ItemType.ToString(CultureInfo.InvariantCulture));
                result.SoldSlots = soldSlots;
                result.SoldItemIds = soldItemIds;
                return soldForValue || candidate.Value == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool InvokeSellItem(object player, object item)
        {
            try
            {
                var method = FindInstanceMethod(player.GetType(), "SellItem", item.GetType(), typeof(int));
                if (method == null)
                {
                    return false;
                }

                return Convert.ToBoolean(method.Invoke(player, new[] { item, (object)(-1) }), CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadSellableCandidate(
            object item,
            int slot,
            ICollection<int> itemIds,
            out AutoSellInventoryCandidate candidate)
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

            candidate = new AutoSellInventoryCandidate
            {
                Slot = slot,
                ItemType = itemType,
                ItemName = itemName ?? string.Empty,
                Stack = stack,
                Value = ReadInt(item, "value", 0)
            };
            return true;
        }

        private static void CountMatchingSlots(
            IList inventory,
            ICollection<int> allowedItemIds,
            ICollection<int> sourceSlots,
            out List<AutoSellInventoryCandidate> candidates)
        {
            candidates = new List<AutoSellInventoryCandidate>();
            if (inventory == null || allowedItemIds == null || allowedItemIds.Count == 0)
            {
                return;
            }

            var max = Math.Min(58, inventory.Count);
            for (var slot = 0; slot < max; slot++)
            {
                if (sourceSlots != null && sourceSlots.Count > 0 && !sourceSlots.Contains(slot))
                {
                    continue;
                }

                AutoSellInventoryCandidate candidate;
                if (TryReadSellableCandidate(inventory[slot], slot, allowedItemIds, out candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        private static bool TryGetCurrentShopChest(out object shopChest, out MethodInfo addItemToShop, out string message)
        {
            shopChest = null;
            addItemToShop = null;
            message = string.Empty;
            var mainType = TerrariaRuntimeTypes.MainType;
            var instance = GetStatic(mainType, "instance");
            var shops = GetMember(instance, "shop") as IList;
            var npcShop = ReadStaticInt(mainType, "npcShop", 0);
            if (shops == null || npcShop <= 0 || npcShop >= shops.Count)
            {
                message = "Current NPC shop chest is unavailable.";
                return false;
            }

            shopChest = shops[npcShop];
            if (shopChest == null)
            {
                message = "Current NPC shop chest is null.";
                return false;
            }

            addItemToShop = FindInstanceMethodByParameterCount(shopChest.GetType(), "AddItemToShop", 1);
            if (addItemToShop == null)
            {
                message = "Chest.AddItemToShop was not found.";
                return false;
            }

            return true;
        }

        private static bool TryOpenShop(int shopIndex, out string message)
        {
            message = string.Empty;
            var mainType = TerrariaRuntimeTypes.MainType;
            var instance = GetStatic(mainType, "instance");
            if (instance == null)
            {
                message = "Terraria.Main.instance is unavailable.";
                return false;
            }

            var method = FindInstanceMethod(instance.GetType(), "OpenShop", typeof(int));
            if (method == null)
            {
                message = "Main.OpenShop(int) was not found.";
                return false;
            }

            try
            {
                method.Invoke(instance, new object[] { shopIndex });
                if (ReadStaticInt(mainType, "npcShop", 0) <= 0)
                {
                    message = "Main.OpenShop returned without opening npcShop.";
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                message = "Main.OpenShop failed: " + Unwrap(error);
                return false;
            }
        }

        private static bool TryApplyShoppingSettings(object player, object npc)
        {
            try
            {
                var shopHelper = GetStatic(TerrariaRuntimeTypes.MainType, "ShopHelper");
                if (shopHelper == null)
                {
                    return false;
                }

                var method = FindInstanceMethod(shopHelper.GetType(), "GetShoppingSettings", player.GetType(), npc.GetType());
                if (method == null)
                {
                    return false;
                }

                var settings = method.Invoke(shopHelper, new[] { player, npc });
                return SetMember(player, "currentShoppingSettings", settings);
            }
            catch
            {
                return false;
            }
        }

        private static AutoSellShopState CaptureShopState(object player)
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            return new AutoSellShopState
            {
                PlayerInventoryOpen = ReadStaticBool(mainType, "playerInventory", false),
                NpcShop = ReadStaticInt(mainType, "npcShop", 0),
                NpcChatText = ReadStaticString(mainType, "npcChatText"),
                StackSplit = ReadStaticInt(mainType, "stackSplit", 0),
                TalkNpc = ReadInt(player, "talkNPC", -1),
                CurrentShoppingSettings = GetMember(player, "currentShoppingSettings")
            };
        }

        private static bool RestoreShopState(object player, AutoSellShopState state, bool opened)
        {
            if (state == null)
            {
                return false;
            }

            var ok = true;
            var mainType = TerrariaRuntimeTypes.MainType;
            ok &= SetStatic(mainType, "playerInventory", state.PlayerInventoryOpen);
            ok &= TrySetNpcShopIndex(state.NpcShop);
            ok &= SetStatic(mainType, "npcChatText", state.NpcChatText ?? string.Empty);
            ok &= SetStatic(mainType, "stackSplit", state.StackSplit);
            if (player != null)
            {
                ok &= TrySetTalkNpc(player, state.TalkNpc);
                ok &= SetMember(player, "currentShoppingSettings", state.CurrentShoppingSettings);
            }

            if (opened && state.NpcShop == 0)
            {
                SetStatic(mainType, "npcChatCornerItem", 0);
                SetStatic(mainType, "npcChatFocus1", false);
                SetStatic(mainType, "npcChatFocus2", false);
                SetStatic(mainType, "npcChatFocus3", false);
                SetStatic(mainType, "npcChatFocus4", false);
                SetStatic(mainType, "npcChatRelease", false);
            }

            return ok;
        }

        private static bool TrySetNpcShopIndex(int index)
        {
            var method = FindStaticMethod(TerrariaRuntimeTypes.MainType, "SetNPCShopIndex", typeof(int));
            if (method != null)
            {
                try
                {
                    method.Invoke(null, new object[] { index });
                    return true;
                }
                catch
                {
                }
            }

            return SetStatic(TerrariaRuntimeTypes.MainType, "npcShop", index);
        }

        private static bool TrySetTalkNpc(object player, int npcIndex)
        {
            if (player == null)
            {
                return false;
            }

            var method = FindInstanceMethod(player.GetType(), "SetTalkNPC", typeof(int));
            if (method != null)
            {
                try
                {
                    method.Invoke(player, new object[] { npcIndex });
                    return true;
                }
                catch
                {
                }
            }

            return SetMember(player, "talkNPC", npcIndex);
        }

        private static bool IsNpcReachable(object player, object npc)
        {
            int left;
            int top;
            int right;
            int bottom;
            if (!NurseServiceCompat.TryGetTileReachRegion(player, out left, out top, out right, out bottom))
            {
                var dx = ReadCenterX(player) - ReadCenterX(npc);
                var dy = ReadCenterY(player) - ReadCenterY(npc);
                return dx * dx + dy * dy <= 12f * 16f * 12f * 16f;
            }

            var npcLeft = (int)(ReadFloat(npc, "position", "X") / 16f);
            var npcTop = (int)(ReadFloat(npc, "position", "Y") / 16f);
            var npcRight = (int)((ReadFloat(npc, "position", "X") + ReadInt(npc, "width", 18)) / 16f);
            var npcBottom = (int)((ReadFloat(npc, "position", "Y") + ReadInt(npc, "height", 40)) / 16f);
            return npcRight >= left && npcLeft <= right && npcBottom >= top && npcTop <= bottom;
        }

        private static int SumStacks(List<AutoSellInventoryCandidate> candidates)
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

        private static string BuildInventorySignature(List<AutoSellInventoryCandidate> candidates)
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

        private static bool TryTurnToAir(object item)
        {
            if (item == null)
            {
                return false;
            }

            var methods = item.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "TurnToAir", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                try
                {
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                    {
                        method.Invoke(item, new object[] { false });
                        return true;
                    }

                    if (parameters.Length == 0)
                    {
                        method.Invoke(item, new object[0]);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool IsCoin(int itemType)
        {
            return itemType >= 71 && itemType <= 74;
        }

        private static bool IsFavorited(object item)
        {
            bool favorited;
            if (TryGetBool(item, "favorited", out favorited) && favorited)
            {
                return true;
            }

            return TryGetBool(item, "favorite", out favorited) && favorited;
        }

        private static string ReadName(object instance)
        {
            var value = GetMember(instance, "FullName") ?? GetMember(instance, "GivenName") ?? GetMember(instance, "TypeName");
            return value == null ? string.Empty : value.ToString();
        }

        private static float ReadCenterX(object instance)
        {
            return ReadFloat(instance, "position", "X") + ReadInt(instance, "width", 0) / 2f;
        }

        private static float ReadCenterY(object instance)
        {
            return ReadFloat(instance, "position", "Y") + ReadInt(instance, "height", 0) / 2f;
        }

        private static float ReadFloat(object instance, string vectorName, string componentName)
        {
            var vector = GetMember(instance, vectorName);
            var raw = GetMember(vector, componentName);
            try { return raw == null ? 0f : Convert.ToSingle(raw, CultureInfo.InvariantCulture); }
            catch { return 0f; }
        }

        private static int ReadStaticInt(Type type, string name, int fallback)
        {
            var raw = GetStatic(type, name);
            try { return raw == null ? fallback : Convert.ToInt32(raw, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static bool ReadStaticBool(Type type, string name, bool fallback)
        {
            var raw = GetStatic(type, name);
            try { return raw == null ? fallback : Convert.ToBoolean(raw, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static string ReadStaticString(Type type, string name)
        {
            var raw = GetStatic(type, name);
            return raw == null ? string.Empty : raw.ToString();
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            var raw = GetMember(instance, name);
            try { return raw == null ? fallback : Convert.ToInt32(raw, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static bool ReadBool(object instance, string name, bool fallback)
        {
            var raw = GetMember(instance, name);
            try { return raw == null ? fallback : Convert.ToBoolean(raw, CultureInfo.InvariantCulture); }
            catch { return fallback; }
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

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property)
                ? property.GetValue(instance, null)
                : null;
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

            return TerrariaMemberCache.TryGetProperty(type, name, true, out var property)
                ? property.GetValue(null, null)
                : null;
        }

        private static bool SetMember(object instance, string name, object value)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    field.SetValue(instance, value);
                    return true;
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, false, out var property) && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
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

        private static MethodInfo FindInstanceMethod(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameterTypes == null || parameterTypes.Length == 0)
                {
                    if (parameters.Length == 0)
                    {
                        return method;
                    }

                    continue;
                }

                if (parameters.Length != parameterTypes.Length)
                {
                    continue;
                }

                var ok = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (!parameters[i].ParameterType.IsAssignableFrom(parameterTypes[i]))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindStaticMethod(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameterTypes == null || parameterTypes.Length == 0)
                {
                    return parameters.Length == 0 ? method : null;
                }

                if (parameters.Length != parameterTypes.Length)
                {
                    continue;
                }

                var ok = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != parameterTypes[i])
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindInstanceMethodByParameterCount(Type type, string name, int parameterCount)
        {
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (method.GetParameters().Length == parameterCount)
                {
                    return method;
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

        private sealed class AutoSellShopState
        {
            public bool PlayerInventoryOpen { get; set; }
            public int NpcShop { get; set; }
            public string NpcChatText { get; set; }
            public int StackSplit { get; set; }
            public int TalkNpc { get; set; }
            public object CurrentShoppingSettings { get; set; }
        }
    }
}
