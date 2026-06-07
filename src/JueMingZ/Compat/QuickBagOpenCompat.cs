using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace JueMingZ.Compat
{
    internal static class QuickBagOpenCompat
    {
        // Bag opening is a hook-scoped right-click pulse; inventory state and
        // pending actions must be safe before invoking ItemSlot.
        private const int InventoryItemSlotContext = 0;
        private const int MaxInventorySlot = 50;
        private const int VkShift = 0x10;
        private const int VkLeftShift = 0xA0;
        private const int VkRightShift = 0xA1;
        private const int VkRightButton = 0x02;
        private const int HookPulseCooldownFrames = 1;
        private static readonly object SyncRoot = new object();
        private static MethodInfo _itemSlotRightClick;
        private static Array _openableBagSet;
        private static bool _openableBagSetResolved;
        private static int _hookPulseCooldown;

        public static bool TryIsRapidOpenInputActive(out string message)
        {
            message = string.Empty;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            var terrariaRightHeld = ReadStaticBool(mainType, "mouseRight", false);
            var osRightHeld = IsRightButtonDownFallback();
            if (!terrariaRightHeld && !osRightHeld)
            {
                message = "mouseRight not held";
                return false;
            }

            var shiftStateAvailable = TryIsShiftHeld(mainType, out var shiftHeld);
            shiftHeld = shiftHeld || IsShiftDownFallback();
            if (!shiftStateAvailable && !shiftHeld)
            {
                message = "shift key state unavailable";
                return false;
            }

            if (!shiftHeld)
            {
                message = "shift not held";
                return false;
            }

            return true;
        }

        public static void ResetHookPulseCooldown()
        {
            lock (SyncRoot)
            {
                _hookPulseCooldown = 0;
            }
        }

        public static bool TryApplyItemSlotRightClickReleasePulse(
            object inventoryObject,
            int context,
            int slot,
            out int itemType,
            out string itemName,
            out string message)
        {
            itemType = 0;
            itemName = string.Empty;
            message = string.Empty;

            if (context != InventoryItemSlotContext)
            {
                message = string.Empty;
                ResetHookPulseCooldown();
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable";
                ResetHookPulseCooldown();
                return false;
            }

            if (!ReadStaticBool(mainType, "playerInventory", false))
            {
                message = "inventory closed";
                ResetHookPulseCooldown();
                return false;
            }

            if (IsConfigUiVisible())
            {
                message = "config UI visible";
                ResetHookPulseCooldown();
                return false;
            }

            var terrariaRightHeld = ReadStaticBool(mainType, "mouseRight", false);
            var osRightHeld = IsRightButtonDownFallback();
            if (!terrariaRightHeld && !osRightHeld)
            {
                message = "mouseRight not held in ItemSlot hook";
                ResetHookPulseCooldown();
                return false;
            }

            var shiftAvailable = TryIsShiftHeld(mainType, out var shiftHeld);
            shiftHeld = shiftHeld || IsShiftDownFallback();
            if (!shiftAvailable && !shiftHeld)
            {
                message = "shift key state unavailable";
                ResetHookPulseCooldown();
                return false;
            }

            if (!shiftHeld)
            {
                message = "shift not held";
                ResetHookPulseCooldown();
                return false;
            }

            if (TryLocalPlayerHasPendingInventoryActions())
            {
                message = "pending inventory actions";
                ResetHookPulseCooldown();
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                message = "player unavailable: " + TerrariaInputCompat.LastInputCompatError;
                ResetHookPulseCooldown();
                return false;
            }

            if (IsPlayerItemAnimationBusy(player))
            {
                message = "player item animation busy";
                ResetHookPulseCooldown();
                return false;
            }

            var inventory = inventoryObject as IList;
            if (inventory == null || slot < 0 || slot >= inventory.Count)
            {
                message = "invalid inventory slot";
                ResetHookPulseCooldown();
                return false;
            }

            if (GetOpenableBagSet() == null)
            {
                message = "openable bag set unavailable";
                ResetHookPulseCooldown();
                return false;
            }

            if (!TryReadBagCandidate(inventory[slot], out itemType, out itemName, out var stack))
            {
                message = string.Empty;
                ResetHookPulseCooldown();
                return false;
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                message = "mouse item held";
                ResetHookPulseCooldown();
                return false;
            }

            lock (SyncRoot)
            {
                if (_hookPulseCooldown > 0)
                {
                    _hookPulseCooldown--;
                    message = "hook cooldown";
                    return false;
                }

                _hookPulseCooldown = HookPulseCooldownFrames;
            }

            SetStatic(mainType, "mouseRightRelease", true);
            return true;
        }

        public static bool TryFindBagSlot(
            object player,
            int preferredItemType,
            out int slot,
            out int itemType,
            out int stack,
            out string itemName,
            out string message)
        {
            slot = -1;
            itemType = 0;
            stack = 0;
            itemName = string.Empty;
            message = string.Empty;

            IList inventory;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) || inventory == null)
            {
                return false;
            }

            if (GetOpenableBagSet() == null)
            {
                message = "openable bag set unavailable";
                return false;
            }

            var bestSlot = -1;
            var bestType = 0;
            var bestStack = 0;
            var bestName = string.Empty;
            var max = Math.Min(MaxInventorySlot, inventory.Count);
            for (var index = 0; index < max; index++)
            {
                var item = inventory[index];
                if (!TryReadBagCandidate(item, out var type, out var name, out var currentStack))
                {
                    continue;
                }

                if (type == preferredItemType)
                {
                    slot = index;
                    itemType = type;
                    stack = currentStack;
                    itemName = name;
                    return true;
                }

                if (bestSlot < 0)
                {
                    bestSlot = index;
                    bestType = type;
                    bestStack = currentStack;
                    bestName = name;
                }
            }

            if (bestSlot < 0)
            {
                message = "no openable bag in inventory";
                return false;
            }

            slot = bestSlot;
            itemType = bestType;
            stack = bestStack;
            itemName = bestName;
            return true;
        }

        public static bool TryReadHoveredItemType(out int itemType)
        {
            itemType = 0;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return false;
            }

            var hoverItem = GetStaticMember(mainType, "HoverItem");
            if (hoverItem == null)
            {
                return false;
            }

            int stack;
            int buffType;
            int buffTime;
            bool summon;
            string itemName;
            if (!InventoryMutationCompat.TryReadItemFields(hoverItem, out itemType, out itemName, out stack, out buffType, out buffTime, out summon))
            {
                return false;
            }

            return itemType > 0;
        }

        public static bool TryRapidOpenSlot(object player, int slot, int repeatCount, out int openedCount, out string message)
        {
            openedCount = 0;
            message = string.Empty;
            if (slot < 0 || repeatCount <= 0)
            {
                message = "invalid slot or repeat count";
                return false;
            }

            IList inventory;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) || inventory == null)
            {
                return false;
            }

            if (slot >= inventory.Count)
            {
                message = "slot outside inventory bounds";
                return false;
            }

            MethodInfo rightClick;
            if (!TryFindItemSlotRightClick(out rightClick, out message))
            {
                return false;
            }

            for (var pulse = 0; pulse < repeatCount; pulse++)
            {
                var itemBefore = inventory[slot];
                if (!TryReadBagCandidate(itemBefore, out var typeBefore, out var nameBefore, out var stackBefore))
                {
                    break;
                }

                if (!TryInvokeBagRightClick(inventory, slot, rightClick, out message))
                {
                    if (openedCount > 0)
                    {
                        return true;
                    }

                    return false;
                }

                var itemAfter = inventory[slot];
                int typeAfter;
                int stackAfter;
                int buffType;
                int buffTime;
                bool summon;
                string itemName;
                if (!InventoryMutationCompat.TryReadItemFields(itemAfter, out typeAfter, out itemName, out stackAfter, out buffType, out buffTime, out summon))
                {
                    openedCount++;
                    continue;
                }

                var changed = typeAfter != typeBefore || stackAfter < stackBefore;
                if (changed)
                {
                    openedCount++;
                    continue;
                }

                if (stackBefore <= 0 || string.IsNullOrWhiteSpace(nameBefore))
                {
                    break;
                }
            }

            if (openedCount <= 0)
            {
                message = "no bag stack consumed";
                return false;
            }

            return true;
        }

        private static bool TryReadBagCandidate(object item, out int itemType, out string itemName, out int stack)
        {
            itemType = 0;
            itemName = string.Empty;
            stack = 0;
            int buffType;
            int buffTime;
            bool summon;
            if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon))
            {
                return false;
            }

            return itemType > 0 &&
                   stack > 0 &&
                   IsOpenableBagItemType(itemType);
        }

        private static bool IsOpenableBagItemType(int itemType)
        {
            if (itemType <= 0)
            {
                return false;
            }

            var set = GetOpenableBagSet();
            if (set == null || itemType >= set.Length)
            {
                return false;
            }

            try
            {
                return Convert.ToBoolean(set.GetValue(itemType));
            }
            catch
            {
                return false;
            }
        }

        private static bool TryIsShiftHeld(Type mainType, out bool held)
        {
            held = false;
            var keyState = GetStaticMember(mainType, "keyState");
            if (keyState == null)
            {
                return false;
            }

            var keyStateType = keyState.GetType();
            var isKeyDown = keyStateType.GetMethod("IsKeyDown", BindingFlags.Public | BindingFlags.Instance);
            if (isKeyDown == null || isKeyDown.GetParameters().Length != 1)
            {
                return false;
            }

            var keyType = isKeyDown.GetParameters()[0].ParameterType;
            if (!keyType.IsEnum)
            {
                return false;
            }

            object leftShift;
            object rightShift;
            try
            {
                leftShift = Enum.Parse(keyType, "LeftShift", false);
                rightShift = Enum.Parse(keyType, "RightShift", false);
            }
            catch
            {
                return false;
            }

            try
            {
                var left = Convert.ToBoolean(isKeyDown.Invoke(keyState, new[] { leftShift }));
                var right = Convert.ToBoolean(isKeyDown.Invoke(keyState, new[] { rightShift }));
                held = left || right;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFindItemSlotRightClick(out MethodInfo rightClick, out string message)
        {
            rightClick = _itemSlotRightClick;
            message = string.Empty;
            if (rightClick != null)
            {
                return true;
            }

            var itemSlotType = TerrariaTypeCache.Find("Terraria.UI.ItemSlot");
            if (itemSlotType == null)
            {
                message = "Terraria.UI.ItemSlot type unavailable";
                return false;
            }

            var methods = itemSlotType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "RightClick", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 3 &&
                    parameters[0].ParameterType.IsArray &&
                    parameters[1].ParameterType == typeof(int) &&
                    parameters[2].ParameterType == typeof(int))
                {
                    _itemSlotRightClick = method;
                    rightClick = method;
                    return true;
                }
            }

            message = "ItemSlot.RightClick inventory overload unavailable";
            return false;
        }

        private static bool TryInvokeBagRightClick(IList inventory, int slot, MethodInfo rightClick, out string message)
        {
            message = string.Empty;
            if (inventory == null || rightClick == null)
            {
                message = "inventory or ItemSlot.RightClick unavailable";
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable";
                return false;
            }

            var previousMouseLeft = ReadStaticBool(mainType, "mouseLeft", false);
            var previousMouseLeftRelease = ReadStaticBool(mainType, "mouseLeftRelease", false);
            var previousMouseRight = ReadStaticBool(mainType, "mouseRight", false);
            var previousMouseRightRelease = ReadStaticBool(mainType, "mouseRightRelease", false);
            // Rapid bag open borrows vanilla right-click state for one ItemSlot
            // invocation; the hook must restore both left/right flags.
            try
            {
                SetStatic(mainType, "mouseLeft", false);
                SetStatic(mainType, "mouseLeftRelease", false);
                SetStatic(mainType, "mouseRight", true);
                SetStatic(mainType, "mouseRightRelease", true);
                rightClick.Invoke(null, new object[] { inventory, InventoryItemSlotContext, slot });
                return true;
            }
            catch (Exception error)
            {
                message = "ItemSlot.RightClick invoke failed: " + Unwrap(error);
                return false;
            }
            finally
            {
                SetStatic(mainType, "mouseLeft", previousMouseLeft);
                SetStatic(mainType, "mouseLeftRelease", previousMouseLeftRelease);
                SetStatic(mainType, "mouseRight", previousMouseRight);
                SetStatic(mainType, "mouseRightRelease", previousMouseRightRelease);
            }
        }

        private static Array GetOpenableBagSet()
        {
            lock (SyncRoot)
            {
                if (_openableBagSetResolved)
                {
                    return _openableBagSet;
                }

                _openableBagSet = ResolveOpenableBagSet();
                _openableBagSetResolved = true;
                return _openableBagSet;
            }
        }

        private static Array ResolveOpenableBagSet()
        {
            var setsType = TerrariaTypeCache.Find("Terraria.ID.ItemID+Sets");
            if (setsType == null)
            {
                var itemIdType = TerrariaTypeCache.Find("Terraria.ID.ItemID");
                if (itemIdType != null)
                {
                    setsType = itemIdType.GetNestedType("Sets", BindingFlags.Public | BindingFlags.NonPublic);
                }
            }

            if (setsType == null)
            {
                return null;
            }

            var raw = GetStaticMember(setsType, "OpenableBag");
            return raw as Array;
        }

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
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

        private static bool IsConfigUiVisible()
        {
            try
            {
                var type = TerrariaTypeCache.Find("TerrariaHelper.UI.Backends.Terraria.TerrariaUIBootstrap");
                if (type == null)
                {
                    return false;
                }

                var raw = GetStaticMember(type, "IsConfigUIVisible");
                return raw != null && Convert.ToBoolean(raw);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryLocalPlayerHasPendingInventoryActions()
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return false;
            }

            try
            {
                var method = mainType.GetMethod("LocalPlayerHasPendingInventoryActions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null || method.GetParameters().Length != 0)
                {
                    return false;
                }

                return Convert.ToBoolean(method.Invoke(null, null));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPlayerItemAnimationBusy(object player)
        {
            if (player == null)
            {
                return true;
            }

            try
            {
                var itemAnimation = GetMember(player, "itemAnimation");
                return itemAnimation != null && Convert.ToInt32(itemAnimation) > 0;
            }
            catch
            {
                return false;
            }
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
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

        private static bool ReadStaticBool(Type type, string name, bool fallback)
        {
            var raw = GetStaticMember(type, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(raw);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool IsRightButtonDownFallback()
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            try
            {
                return (GetAsyncKeyState(VkRightButton) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsShiftDownFallback()
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            try
            {
                return (GetAsyncKeyState(VkShift) & 0x8000) != 0 ||
                       (GetAsyncKeyState(VkLeftShift) & 0x8000) != 0 ||
                       (GetAsyncKeyState(VkRightShift) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static void SetStatic(Type type, string name, bool value)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    field.SetValue(null, value);
                    return;
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, true, out var property) && property.CanWrite)
                {
                    property.SetValue(null, value, null);
                }
            }
            catch
            {
            }
        }

        private static string Unwrap(Exception error)
        {
            if (error == null)
            {
                return string.Empty;
            }

            if (error is TargetInvocationException invocation && invocation.InnerException != null)
            {
                return invocation.InnerException.Message ?? invocation.InnerException.GetType().Name;
            }

            return error.Message ?? error.GetType().Name;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }
}
