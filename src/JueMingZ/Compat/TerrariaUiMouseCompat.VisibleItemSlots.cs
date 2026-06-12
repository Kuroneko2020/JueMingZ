using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaUiMouseCompat
    {
        private const int VanillaInventoryBackPixels = 52;
        private const int VanillaInventorySlotStridePixels = 56;
        private const float VanillaMainInventoryScale = 0.85f;
        private const float VanillaChestInventoryScale = 0.755f;
        private const float VanillaCoinAmmoInventoryScale = 0.6f;
        private const int VanillaInventoryContext = 0;
        private const int VanillaCoinContext = 1;
        private const int VanillaAmmoContext = 2;
        private const int VanillaChestContext = 3;
        private const int VanillaBankContext = 4;
        private const int VanillaArmorContext = 8;
        private const int VanillaVanityArmorContext = 9;
        private const int VanillaAccessoryContext = 10;
        private const int VanillaVanityAccessoryContext = 11;
        private const int VanillaDyeContext = 12;
        private const int VanillaGrappleContext = 16;
        private const int VanillaMountContext = 17;
        private const int VanillaMinecartContext = 18;
        private const int VanillaPetContext = 19;
        private const int VanillaLightPetContext = 20;
        private const int VanillaVoidVaultContext = 32;
        private const int VanillaMiscDyeContext = 33;
        private static MethodInfo _canDemonHeartAccessoryBeShownMethod;
        private static MethodInfo _canMasterModeAccessoryBeShownMethod;
        private static Type _extraAccessoryMethodPlayerType;

        public static bool TryReadVisibleItemSlotSnapshot(
            int uiMouseX,
            int uiMouseY,
            ulong gameUpdateCount,
            out TerrariaUiHoverSlotSnapshot snapshot)
        {
            snapshot = null;

            try
            {
                Type mainType;
                object player;
                if (!TryGetVisibleItemSlotRoot(out mainType, out player))
                {
                    return false;
                }

                bool playerInventoryOpen;
                if (!TryReadStaticBool(mainType, "playerInventory", out playerInventoryOpen) ||
                    !playerInventoryOpen)
                {
                    return false;
                }

                // This is an explicit UI slot hit-test, not a fallback target
                // resolver. Callers must pass Terraria UI logical coordinates,
                // after UIScaleMatrix conversion, so raw screen pixels do not
                // drift into neighboring inventory slots under UI scaling.
                // Empty slots must return a slot proof so callers can block
                // clicks from falling through to the world layer.
                return TryReadVisibleChestSlotSnapshot(mainType, player, uiMouseX, uiMouseY, gameUpdateCount, out snapshot) ||
                       TryReadVisiblePlayerInventorySlotSnapshot(player, uiMouseX, uiMouseY, gameUpdateCount, out snapshot) ||
                       TryReadVisibleCoinOrAmmoSlotSnapshot(player, uiMouseX, uiMouseY, gameUpdateCount, out snapshot) ||
                       TryReadVisibleEquipmentSlotSnapshot(mainType, player, uiMouseX, uiMouseY, gameUpdateCount, out snapshot);
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "visible-item-slot-read-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaUiMouseCompat",
                    "Read visible item slot failed: " + error.Message);
                return false;
            }
        }

        private static bool TryGetVisibleItemSlotRoot(out Type mainType, out object player)
        {
            mainType = TerrariaRuntimeTypes.MainType;
            player = null;
            return mainType != null &&
                   TerrariaInputCompat.TryGetLocalPlayer(out player) &&
                   player != null;
        }

        private static bool TryReadVisiblePlayerInventorySlotSnapshot(
            object player,
            int uiMouseX,
            int uiMouseY,
            ulong gameUpdateCount,
            out TerrariaUiHoverSlotSnapshot snapshot)
        {
            snapshot = null;
            object inventory;
            if (!TryReadInstanceMember(player, "inventory", out inventory) || inventory == null)
            {
                return false;
            }

            for (var column = 0; column < 10; column++)
            {
                for (var row = 0; row < 5; row++)
                {
                    var slot = column + row * 10;
                    var x = (int)(20.0 + column * VanillaInventorySlotStridePixels * VanillaMainInventoryScale);
                    var y = (int)(20.0 + row * VanillaInventorySlotStridePixels * VanillaMainInventoryScale);
                    if (IsPointInVanillaSlot(uiMouseX, uiMouseY, x, y, VanillaMainInventoryScale))
                    {
                        return TryBuildVisibleSlotSnapshot(
                            inventory,
                            VanillaInventoryContext,
                            slot,
                            uiMouseX,
                            uiMouseY,
                            gameUpdateCount,
                            out snapshot);
                    }
                }
            }

            return false;
        }

        private static bool TryReadVisibleCoinOrAmmoSlotSnapshot(
            object player,
            int uiMouseX,
            int uiMouseY,
            ulong gameUpdateCount,
            out TerrariaUiHoverSlotSnapshot snapshot)
        {
            snapshot = null;
            object inventory;
            if (!TryReadInstanceMember(player, "inventory", out inventory) || inventory == null)
            {
                return false;
            }

            for (var index = 0; index < 4; index++)
            {
                var y = (int)(85.0 + index * VanillaInventorySlotStridePixels * VanillaCoinAmmoInventoryScale + 20.0);
                if (IsPointInVanillaSlot(uiMouseX, uiMouseY, 497, y, VanillaCoinAmmoInventoryScale))
                {
                    return TryBuildVisibleSlotSnapshot(
                        inventory,
                        VanillaCoinContext,
                        50 + index,
                        uiMouseX,
                        uiMouseY,
                        gameUpdateCount,
                        out snapshot);
                }

                if (IsPointInVanillaSlot(uiMouseX, uiMouseY, 534, y, VanillaCoinAmmoInventoryScale))
                {
                    return TryBuildVisibleSlotSnapshot(
                        inventory,
                        VanillaAmmoContext,
                        54 + index,
                        uiMouseX,
                        uiMouseY,
                        gameUpdateCount,
                        out snapshot);
                }
            }

            return false;
        }

        private static bool TryReadVisibleChestSlotSnapshot(
            Type mainType,
            object player,
            int uiMouseX,
            int uiMouseY,
            ulong gameUpdateCount,
            out TerrariaUiHoverSlotSnapshot snapshot)
        {
            snapshot = null;
            int chestIndex;
            if (!TryReadIntMember(player, "chest", out chestIndex) || chestIndex == -1)
            {
                return false;
            }

            object chest;
            int context;
            if (!TryGetOpenChestObject(mainType, player, chestIndex, out chest, out context) || chest == null)
            {
                return false;
            }

            object inventory;
            if (!TryReadInstanceMember(chest, "item", out inventory) || inventory == null)
            {
                return false;
            }

            var maxItems = GetItemCollectionCount(inventory);
            int explicitMaxItems;
            if (TryReadIntMember(chest, "maxItems", out explicitMaxItems) && explicitMaxItems > 0)
            {
                maxItems = Math.Min(maxItems, explicitMaxItems);
            }

            if (maxItems <= 0)
            {
                return false;
            }

            var startingRow = ReadChestStartingRow(maxItems);
            var invBottom = ReadMainInstanceInt(mainType, "invBottom", 258);
            for (var column = 0; column < 10; column++)
            {
                for (var row = 0; row < 4; row++)
                {
                    var slot = column + row * 10 + startingRow * 10;
                    if (slot < 0 || slot >= maxItems)
                    {
                        continue;
                    }

                    var x = (int)(73.0 + column * VanillaInventorySlotStridePixels * VanillaChestInventoryScale);
                    var y = (int)(invBottom + row * VanillaInventorySlotStridePixels * VanillaChestInventoryScale);
                    if (IsPointInVanillaSlot(uiMouseX, uiMouseY, x, y, VanillaChestInventoryScale))
                    {
                        return TryBuildVisibleSlotSnapshot(
                            inventory,
                            context,
                            slot,
                            uiMouseX,
                            uiMouseY,
                            gameUpdateCount,
                            out snapshot);
                    }
                }
            }

            return false;
        }

        private static bool TryGetOpenChestObject(
            Type mainType,
            object player,
            int chestIndex,
            out object chest,
            out int context)
        {
            chest = null;
            context = VanillaChestContext;

            if (chestIndex > -1)
            {
                object chests;
                if (!TryGetStaticMember(mainType, "chest", out chests) ||
                    !TryGetIndexedObject(chests, chestIndex, out chest))
                {
                    return false;
                }

                return true;
            }

            if (chestIndex == -2)
            {
                context = VanillaBankContext;
                return TryReadInstanceMember(player, "bank", out chest) && chest != null;
            }

            if (chestIndex == -3)
            {
                context = VanillaBankContext;
                return TryReadInstanceMember(player, "bank2", out chest) && chest != null;
            }

            if (chestIndex == -4)
            {
                context = VanillaBankContext;
                return TryReadInstanceMember(player, "bank3", out chest) && chest != null;
            }

            if (chestIndex == -5)
            {
                context = VanillaVoidVaultContext;
                return TryReadInstanceMember(player, "bank4", out chest) && chest != null;
            }

            return false;
        }

        private static bool TryReadVisibleEquipmentSlotSnapshot(
            Type mainType,
            object player,
            int uiMouseX,
            int uiMouseY,
            ulong gameUpdateCount,
            out TerrariaUiHoverSlotSnapshot snapshot)
        {
            snapshot = null;
            var equipPage = ReadStaticInt(mainType, "EquipPage", 0);
            return equipPage == 2
                ? TryReadVisibleMiscEquipmentSlotSnapshot(mainType, player, uiMouseX, uiMouseY, gameUpdateCount, out snapshot)
                : TryReadVisibleArmorAndDyeSlotSnapshot(mainType, player, uiMouseX, uiMouseY, gameUpdateCount, out snapshot);
        }

        private static bool TryReadVisibleArmorAndDyeSlotSnapshot(
            Type mainType,
            object player,
            int uiMouseX,
            int uiMouseY,
            ulong gameUpdateCount,
            out TerrariaUiHoverSlotSnapshot snapshot)
        {
            snapshot = null;
            object armor;
            object dye;
            if (!TryReadInstanceMember(player, "armor", out armor) ||
                !TryReadInstanceMember(player, "dye", out dye) ||
                armor == null ||
                dye == null)
            {
                return false;
            }

            var screenWidth = ReadStaticInt(mainType, "screenWidth", 1280);
            var inventoryTop = CalculateEquipmentInventoryTop(mainType, player);
            var demonHeartSlotVisible = CanDemonHeartAccessoryBeShown(player);
            var masterModeSlotVisible = CanMasterModeAccessoryBeShown(player);
            const int accessoryOffsetPixels = 4;

            var displayedRow = -1;
            for (var slot = 0; slot < 10; slot++)
            {
                if ((slot == 8 && !demonHeartSlotVisible) || (slot == 9 && !masterModeSlotVisible))
                {
                    continue;
                }

                displayedRow++;
                var x = screenWidth - 64 - 28;
                var y = (int)(inventoryTop + displayedRow * VanillaInventorySlotStridePixels * VanillaMainInventoryScale);
                var context = VanillaArmorContext;
                if (slot > 2)
                {
                    y += accessoryOffsetPixels;
                    context = VanillaAccessoryContext;
                }

                if (IsPointInVanillaSlot(uiMouseX, uiMouseY, x, y, VanillaMainInventoryScale))
                {
                    return TryBuildVisibleSlotSnapshot(armor, context, slot, uiMouseX, uiMouseY, gameUpdateCount, out snapshot);
                }
            }

            displayedRow = -1;
            for (var slot = 10; slot < 20; slot++)
            {
                if ((slot == 18 && !demonHeartSlotVisible) || (slot == 19 && !masterModeSlotVisible))
                {
                    continue;
                }

                displayedRow++;
                var x = screenWidth - 64 - 28 - 47;
                var y = (int)(inventoryTop + displayedRow * VanillaInventorySlotStridePixels * VanillaMainInventoryScale);
                var context = VanillaVanityArmorContext;
                if (slot > 12)
                {
                    y += accessoryOffsetPixels;
                    context = VanillaVanityAccessoryContext;
                }

                if (IsPointInVanillaSlot(uiMouseX, uiMouseY, x, y, VanillaMainInventoryScale))
                {
                    return TryBuildVisibleSlotSnapshot(armor, context, slot, uiMouseX, uiMouseY, gameUpdateCount, out snapshot);
                }
            }

            displayedRow = -1;
            for (var slot = 0; slot < 10; slot++)
            {
                if ((slot == 8 && !demonHeartSlotVisible) || (slot == 9 && !masterModeSlotVisible))
                {
                    continue;
                }

                displayedRow++;
                var x = screenWidth - 64 - 28 - 47 - 47;
                var y = (int)(inventoryTop + displayedRow * VanillaInventorySlotStridePixels * VanillaMainInventoryScale);
                if (slot > 2)
                {
                    y += accessoryOffsetPixels;
                }

                if (IsPointInVanillaSlot(uiMouseX, uiMouseY, x, y, VanillaMainInventoryScale))
                {
                    return TryBuildVisibleSlotSnapshot(dye, VanillaDyeContext, slot, uiMouseX, uiMouseY, gameUpdateCount, out snapshot);
                }
            }

            return false;
        }

        private static bool TryReadVisibleMiscEquipmentSlotSnapshot(
            Type mainType,
            object player,
            int uiMouseX,
            int uiMouseY,
            ulong gameUpdateCount,
            out TerrariaUiHoverSlotSnapshot snapshot)
        {
            snapshot = null;
            object miscEquips;
            object miscDyes;
            if (!TryReadInstanceMember(player, "miscEquips", out miscEquips) ||
                !TryReadInstanceMember(player, "miscDyes", out miscDyes) ||
                miscEquips == null ||
                miscDyes == null)
            {
                return false;
            }

            var screenWidth = ReadStaticInt(mainType, "screenWidth", 1280);
            var xBase = screenWidth - 92;
            var inventoryTop = CalculateEquipmentInventoryTop(mainType, player);
            for (var slot = 0; slot < 5; slot++)
            {
                var y = inventoryTop + slot * 47;
                if (IsPointInVanillaSlot(uiMouseX, uiMouseY, xBase, y, VanillaMainInventoryScale))
                {
                    return TryBuildVisibleSlotSnapshot(
                        miscEquips,
                        GetMiscEquipContext(slot),
                        slot,
                        uiMouseX,
                        uiMouseY,
                        gameUpdateCount,
                        out snapshot);
                }

                if (IsPointInVanillaSlot(uiMouseX, uiMouseY, xBase - 47, y, VanillaMainInventoryScale))
                {
                    return TryBuildVisibleSlotSnapshot(
                        miscDyes,
                        VanillaMiscDyeContext,
                        slot,
                        uiMouseX,
                        uiMouseY,
                        gameUpdateCount,
                        out snapshot);
                }
            }

            return false;
        }

        private static int GetMiscEquipContext(int slot)
        {
            switch (slot)
            {
                case 0:
                    return VanillaPetContext;
                case 1:
                    return VanillaLightPetContext;
                case 2:
                    return VanillaMinecartContext;
                case 3:
                    return VanillaMountContext;
                case 4:
                    return VanillaGrappleContext;
                default:
                    return 0;
            }
        }

        private static int CalculateEquipmentInventoryTop(Type mainType, object player)
        {
            var visibleAccessorySlots = 8;
            if (CanDemonHeartAccessoryBeShown(player))
            {
                visibleAccessorySlots++;
            }

            if (CanMasterModeAccessoryBeShown(player))
            {
                visibleAccessorySlots++;
            }

            var inventoryTop = 174 + ReadStaticInt(mainType, "mH", 0);
            if (ReadStaticInt(mainType, "screenHeight", 800) < 950 && visibleAccessorySlots >= 10)
            {
                inventoryTop -= (int)(VanillaInventorySlotStridePixels * VanillaMainInventoryScale * (visibleAccessorySlots - 9));
            }

            return inventoryTop;
        }

        private static bool CanDemonHeartAccessoryBeShown(object player)
        {
            bool value;
            if (TryInvokeCachedBoolPlayerMethod(
                    player,
                    "CanDemonHeartAccessoryBeShown",
                    ref _canDemonHeartAccessoryBeShownMethod,
                    out value))
            {
                return value;
            }

            return PlayerSlotHasActiveItem(player, "armor", 8) ||
                   PlayerSlotHasActiveItem(player, "armor", 18) ||
                   PlayerSlotHasActiveItem(player, "dye", 8);
        }

        private static bool CanMasterModeAccessoryBeShown(object player)
        {
            bool value;
            if (TryInvokeCachedBoolPlayerMethod(
                    player,
                    "CanMasterModeAccessoryBeShown",
                    ref _canMasterModeAccessoryBeShownMethod,
                    out value))
            {
                return value;
            }

            return PlayerSlotHasActiveItem(player, "armor", 9) ||
                   PlayerSlotHasActiveItem(player, "armor", 19) ||
                   PlayerSlotHasActiveItem(player, "dye", 9);
        }

        private static bool TryInvokeCachedBoolPlayerMethod(
            object player,
            string methodName,
            ref MethodInfo cachedMethod,
            out bool value)
        {
            value = false;
            if (player == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var playerType = player.GetType();
                if (!ReferenceEquals(_extraAccessoryMethodPlayerType, playerType))
                {
                    _canDemonHeartAccessoryBeShownMethod = null;
                    _canMasterModeAccessoryBeShownMethod = null;
                    _extraAccessoryMethodPlayerType = playerType;
                }

                if (cachedMethod == null)
                {
                    cachedMethod = playerType.GetMethod(
                        methodName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        Type.EmptyTypes,
                        null);
                }

                if (cachedMethod == null)
                {
                    return false;
                }

                var raw = cachedMethod.Invoke(player, null);
                if (raw == null)
                {
                    return false;
                }

                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool PlayerSlotHasActiveItem(object player, string memberName, int slot)
        {
            object inventory;
            object item;
            return TryReadInstanceMember(player, memberName, out inventory) &&
                   TryGetInventoryItem(inventory, slot, out item) &&
                   IsActiveHoverItem(item);
        }

        private static bool TryBuildVisibleSlotSnapshot(
            object inventory,
            int context,
            int slot,
            int uiMouseX,
            int uiMouseY,
            ulong gameUpdateCount,
            out TerrariaUiHoverSlotSnapshot snapshot)
        {
            snapshot = null;
            object item;
            return TryGetInventoryItem(inventory, slot, out item) &&
                   TryBuildHoverSlotSnapshot(
                       item,
                       context,
                       slot,
                       gameUpdateCount,
                       uiMouseX,
                       uiMouseY,
                       "VisibleItemSlot",
                       out snapshot);
        }

        private static int ReadChestStartingRow(int maxItems)
        {
            var startingRow = 0;
            var chestUiType = TerrariaTypeCache.Find("Terraria.UI.ChestUI");
            object raw;
            if (TryGetStaticMember(chestUiType, "StartingRowForDrawing", out raw) && raw != null)
            {
                try
                {
                    startingRow = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                }
                catch
                {
                    startingRow = 0;
                }
            }

            var max = Math.Max(0, (int)Math.Ceiling(maxItems / 10.0) - 4);
            if (startingRow < 0)
            {
                return 0;
            }

            return startingRow > max ? max : startingRow;
        }

        private static int ReadMainInstanceInt(Type mainType, string memberName, int fallback)
        {
            object instance;
            object raw;
            if (TryGetStaticMember(mainType, "instance", out instance) &&
                instance != null &&
                TryReadInstanceMember(instance, memberName, out raw) &&
                raw != null)
            {
                try
                {
                    return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return fallback;
        }

        private static int ReadStaticInt(Type type, string memberName, int fallback)
        {
            object raw;
            if (TryGetStaticMember(type, memberName, out raw) && raw != null)
            {
                try
                {
                    return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return fallback;
        }

        private static bool TryReadStaticBool(Type type, string memberName, out bool value)
        {
            value = false;
            object raw;
            if (!TryGetStaticMember(type, memberName, out raw) || raw == null)
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

        private static int GetItemCollectionCount(object source)
        {
            if (source == null)
            {
                return 0;
            }

            var array = source as Array;
            if (array != null)
            {
                return array.Rank == 1 ? array.GetLength(0) : 0;
            }

            var list = source as IList;
            return list == null ? 0 : list.Count;
        }

        private static bool TryGetIndexedObject(object source, int index, out object value)
        {
            value = null;
            if (source == null || index < 0)
            {
                return false;
            }

            var array = source as Array;
            if (array != null)
            {
                if (array.Rank != 1 || index >= array.GetLength(0))
                {
                    return false;
                }

                value = array.GetValue(index);
                return value != null;
            }

            var list = source as IList;
            if (list == null || index >= list.Count)
            {
                return false;
            }

            value = list[index];
            return value != null;
        }

        private static bool IsPointInVanillaSlot(int uiMouseX, int uiMouseY, int slotX, int slotY, float scale)
        {
            var size = VanillaInventoryBackPixels * scale;
            return uiMouseX >= slotX &&
                   uiMouseY >= slotY &&
                   uiMouseX <= slotX + size &&
                   uiMouseY <= slotY + size;
        }
    }
}
