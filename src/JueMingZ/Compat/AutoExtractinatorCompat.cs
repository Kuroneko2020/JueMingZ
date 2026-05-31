using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingZ.Automation.Information;
using JueMingZ.GameState.Inventory;

namespace JueMingZ.Compat
{
    internal static class AutoExtractinatorCompat
    {
        private const int InventoryItemSlotContext = 0;
        public const int ExtractinatorTileType = 219;
        public const int ChlorophyteExtractinatorTileType = 642;
        private static readonly object SyncRoot = new object();
        private static Array _extractinatorModeSet;
        private static bool _extractinatorModeSetResolved;
        private static MethodInfo _itemSlotRightClick;

        public static bool TryResolveExtractinatorMode(int itemType, out int mode)
        {
            mode = -1;
            if (itemType <= 0)
            {
                return false;
            }

            var set = GetExtractinatorModeSet();
            if (set == null || itemType >= set.Length)
            {
                return false;
            }

            try
            {
                mode = Convert.ToInt32(set.GetValue(itemType));
                return mode >= 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryFindExtractableInventoryItem(
            IReadOnlyList<InventoryItemSnapshot> items,
            int selectedSlot,
            out int slot,
            out int itemType,
            out int extractinatorMode,
            out string itemName,
            out string message)
        {
            slot = -1;
            itemType = 0;
            extractinatorMode = -1;
            itemName = string.Empty;
            message = string.Empty;
            if (items == null || items.Count <= 0)
            {
                message = "inventory snapshot unavailable";
                return false;
            }

            if (GetExtractinatorModeSet() == null)
            {
                message = "extractinator mode set unavailable";
                return false;
            }

            var max = Math.Min(50, items.Count);
            if (selectedSlot >= 0 && selectedSlot < max)
            {
                var selected = items[selectedSlot];
                int selectedMode;
                if (selected != null &&
                    selected.Type > 0 &&
                    selected.Stack > 0 &&
                    TryResolveExtractinatorMode(selected.Type, out selectedMode))
                {
                    slot = selectedSlot;
                    itemType = selected.Type;
                    extractinatorMode = selectedMode;
                    itemName = selected.Name ?? string.Empty;
                    return true;
                }
            }

            var hotbarMax = Math.Min(10, max);
            for (var index = 0; index < hotbarMax; index++)
            {
                if (index == selectedSlot)
                {
                    continue;
                }

                var item = items[index];
                int mode;
                if (item == null ||
                    item.Type <= 0 ||
                    item.Stack <= 0 ||
                    !TryResolveExtractinatorMode(item.Type, out mode))
                {
                    continue;
                }

                slot = index;
                itemType = item.Type;
                extractinatorMode = mode;
                itemName = item.Name ?? string.Empty;
                return true;
            }

            for (var index = 10; index < max; index++)
            {
                if (index == selectedSlot)
                {
                    continue;
                }

                var item = items[index];
                int mode;
                if (item == null ||
                    item.Type <= 0 ||
                    item.Stack <= 0 ||
                    !TryResolveExtractinatorMode(item.Type, out mode))
                {
                    continue;
                }

                slot = index;
                itemType = item.Type;
                extractinatorMode = mode;
                itemName = item.Name ?? string.Empty;
                return true;
            }

            message = "no extractable item in inventory";
            return false;
        }

        public static bool TryFindExtractableInventoryItem(
            object player,
            int selectedSlot,
            out int slot,
            out int itemType,
            out int extractinatorMode,
            out string itemName,
            out string message)
        {
            slot = -1;
            itemType = 0;
            extractinatorMode = -1;
            itemName = string.Empty;
            message = string.Empty;
            if (player == null)
            {
                message = "player unavailable";
                return false;
            }

            IList inventory;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) || inventory == null)
            {
                return false;
            }

            if (GetExtractinatorModeSet() == null)
            {
                message = "extractinator mode set unavailable";
                return false;
            }

            if (selectedSlot < 0)
            {
                TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot);
            }

            var max = Math.Min(50, inventory.Count);
            if (selectedSlot >= 0 && selectedSlot < max &&
                TryReadExtractableInventoryItem(inventory[selectedSlot], out itemType, out extractinatorMode, out itemName))
            {
                slot = selectedSlot;
                return true;
            }

            var hotbarMax = Math.Min(10, max);
            for (var index = 0; index < hotbarMax; index++)
            {
                if (index == selectedSlot)
                {
                    continue;
                }

                if (TryReadExtractableInventoryItem(inventory[index], out itemType, out extractinatorMode, out itemName))
                {
                    slot = index;
                    return true;
                }
            }

            for (var index = 10; index < max; index++)
            {
                if (index == selectedSlot)
                {
                    continue;
                }

                if (TryReadExtractableInventoryItem(inventory[index], out itemType, out extractinatorMode, out itemName))
                {
                    slot = index;
                    return true;
                }
            }

            message = "no extractable item in inventory";
            return false;
        }

        public static bool TryReadExtractionReachBoost(object player, int slot, out int reachBoost, out string message)
        {
            reachBoost = 0;
            message = string.Empty;
            if (player == null || slot < 0)
            {
                message = "invalid player or slot";
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

            var item = inventory[slot];
            var tileBoost = ReadIntMember(item, "tileBoost", 0);
            var blockRange = ReadIntMember(player, "blockRange", 0);
            reachBoost = Math.Max(0, tileBoost + blockRange);
            return true;
        }

        public static bool TryFindNearestExtractinator(
            object tiles,
            int maxTilesX,
            int maxTilesY,
            object player,
            int scanRadiusTiles,
            int reachBoost,
            out int tileX,
            out int tileY,
            out int tileType,
            out string message)
        {
            tileX = -1;
            tileY = -1;
            tileType = 0;
            message = string.Empty;
            if (tiles == null || maxTilesX <= 0 || maxTilesY <= 0 || player == null)
            {
                message = "tile context unavailable";
                return false;
            }

            int centerTileX;
            int centerTileY;
            if (!AutoMiningCompat.TryGetPlayerCenterTile(out centerTileX, out centerTileY, out message))
            {
                message = "player tile unavailable: " + message;
                return false;
            }

            float playerCenterX;
            float playerCenterY;
            if (!AutoMiningCompat.TryGetMiningCenterWorld(player, out playerCenterX, out playerCenterY))
            {
                playerCenterX = centerTileX * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
                playerCenterY = centerTileY * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
            }

            int minX;
            int minY;
            int maxX;
            int maxY;
            if (!TryGetVanillaTileReachRegion(player, reachBoost, maxTilesX, maxTilesY, out minX, out minY, out maxX, out maxY))
            {
                minX = Math.Max(0, centerTileX - scanRadiusTiles);
                maxX = Math.Min(maxTilesX - 1, centerTileX + scanRadiusTiles);
                minY = Math.Max(0, centerTileY - scanRadiusTiles);
                maxY = Math.Min(maxTilesY - 1, centerTileY + scanRadiusTiles);
            }

            var bestDistance = float.MaxValue;
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var tile = InformationTileAccess.GetTileAt(tiles, x, y);
                    if (tile == null)
                    {
                        continue;
                    }

                    bool active;
                    int type;
                    int frameX;
                    int frameY;
                    if (!InformationTileAccess.TryReadActiveTypeAndFrame(tile, out active, out type, out frameX, out frameY) ||
                        !active ||
                        !IsExtractinatorTileType(type))
                    {
                        continue;
                    }

                    var worldX = x * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
                    var worldY = y * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
                    var dx = worldX - playerCenterX;
                    var dy = worldY - playerCenterY;
                    var distance = dx * dx + dy * dy;
                    if (tileX < 0 ||
                        distance < bestDistance ||
                        (Math.Abs(distance - bestDistance) < 0.001f && (y < tileY || (y == tileY && x < tileX))))
                    {
                        tileX = x;
                        tileY = y;
                        tileType = type;
                        bestDistance = distance;
                    }
                }
            }

            if (tileX < 0)
            {
                message = "no nearby extractinator in reach";
                return false;
            }

            return true;
        }

        public static bool IsExtractinatorTileType(int tileType)
        {
            return tileType == ExtractinatorTileType || tileType == ChlorophyteExtractinatorTileType;
        }

        private static bool TryReadExtractableInventoryItem(object item, out int itemType, out int extractinatorMode, out string itemName)
        {
            itemType = 0;
            extractinatorMode = -1;
            itemName = string.Empty;
            int stack;
            int buffType;
            int buffTime;
            bool summon;
            if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon))
            {
                return false;
            }

            return itemType > 0 &&
                   stack > 0 &&
                   TryResolveExtractinatorMode(itemType, out extractinatorMode);
        }

        private static bool TryGetVanillaTileReachRegion(
            object player,
            int reachBoost,
            int maxTilesX,
            int maxTilesY,
            out int minX,
            out int minY,
            out int maxX,
            out int maxY)
        {
            minX = 0;
            minY = 0;
            maxX = -1;
            maxY = -1;
            if (player == null)
            {
                return false;
            }

            try
            {
                var settingsType = TerrariaTypeCache.Find("Terraria.DataStructures.TileReachCheckSettings");
                if (settingsType == null)
                {
                    return false;
                }

                var simple = GetStaticMember(settingsType, "Simple");
                if (simple == null)
                {
                    return false;
                }

                var method = settingsType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(IsGetTileRegionCandidate);
                if (method == null)
                {
                    return false;
                }

                var args = new object[] { player, 0, 0, 0, 0, reachBoost };
                method.Invoke(simple, args);
                minX = Clamp(Convert.ToInt32(args[1]), 0, maxTilesX - 1);
                minY = Clamp(Convert.ToInt32(args[2]), 0, maxTilesY - 1);
                maxX = Clamp(Convert.ToInt32(args[3]), 0, maxTilesX - 1);
                maxY = Clamp(Convert.ToInt32(args[4]), 0, maxTilesY - 1);
                return maxX >= minX && maxY >= minY;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsGetTileRegionCandidate(MethodInfo method)
        {
            if (method == null || method.IsStatic || method.ReturnType != typeof(void))
            {
                return false;
            }

            if (!string.Equals(method.Name, "GetTileRegion", StringComparison.Ordinal))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 6 &&
                   parameters[1].ParameterType.IsByRef &&
                   parameters[2].ParameterType.IsByRef &&
                   parameters[3].ParameterType.IsByRef &&
                   parameters[4].ParameterType.IsByRef &&
                   parameters[5].ParameterType == typeof(int);
        }

        public static float TileCenterWorldX(int tileX)
        {
            return tileX * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
        }

        public static float TileCenterWorldY(int tileY)
        {
            return tileY * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
        }

        public static bool TryRapidExtractSlot(
            object player,
            int slot,
            int expectedItemType,
            int repeatCount,
            out int consumedCount,
            out string message)
        {
            consumedCount = 0;
            message = string.Empty;
            if (player == null || slot < 0 || repeatCount <= 0)
            {
                message = "invalid extract slot input";
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

            var pulseCount = Math.Max(1, Math.Min(repeatCount, 30));
            for (var pulse = 0; pulse < pulseCount; pulse++)
            {
                var itemBefore = inventory[slot];
                int typeBefore;
                int stackBefore;
                int buffType;
                int buffTime;
                bool summon;
                string itemName;
                if (!InventoryMutationCompat.TryReadItemFields(itemBefore, out typeBefore, out itemName, out stackBefore, out buffType, out buffTime, out summon) ||
                    typeBefore <= 0 ||
                    stackBefore <= 0)
                {
                    break;
                }

                if (expectedItemType > 0 && typeBefore != expectedItemType)
                {
                    message = "slot item changed before extract";
                    return consumedCount > 0;
                }

                if (!TryInvokeInventoryRightClick(inventory, slot, rightClick, out message))
                {
                    return consumedCount > 0;
                }

                var itemAfter = inventory[slot];
                int typeAfter;
                int stackAfter;
                if (!InventoryMutationCompat.TryReadItemFields(itemAfter, out typeAfter, out itemName, out stackAfter, out buffType, out buffTime, out summon))
                {
                    consumedCount++;
                    continue;
                }

                if (typeAfter != typeBefore || stackAfter < stackBefore)
                {
                    consumedCount++;
                    continue;
                }

                break;
            }

            if (consumedCount <= 0)
            {
                message = "no extractable stack consumed";
                return false;
            }

            return true;
        }

        private static Array GetExtractinatorModeSet()
        {
            lock (SyncRoot)
            {
                if (_extractinatorModeSetResolved)
                {
                    return _extractinatorModeSet;
                }

                _extractinatorModeSet = ResolveExtractinatorModeSet();
                _extractinatorModeSetResolved = true;
                return _extractinatorModeSet;
            }
        }

        private static Array ResolveExtractinatorModeSet()
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

            var raw = GetStaticMember(setsType, "ExtractinatorMode");
            return raw as Array;
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

        private static bool TryInvokeInventoryRightClick(IList inventory, int slot, MethodInfo rightClick, out string message)
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

        private static int ReadIntMember(object instance, string name, int fallback)
        {
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(raw);
            }
            catch
            {
                return fallback;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
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

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var type = instance.GetType();
            try
            {
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
    }
}
