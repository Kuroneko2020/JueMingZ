using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Compat
{
    internal static class FishingLoadoutCompat
    {
        public static bool TryGetCurrentLoadoutIndex(object player, out int index)
        {
            index = -1;
            return TryReadInt(player, "CurrentLoadoutIndex", out index) ||
                   TryReadInt(player, "currentLoadoutIndex", out index);
        }

        public static bool TryGetLoadoutCount(object player, out int count)
        {
            count = 0;
            var loadouts = GetLoadouts(player);
            count = Math.Min(3, GetCollectionCount(loadouts));
            if (count > 0)
            {
                return true;
            }

            count = 3;
            return true;
        }

        public static bool TryGetLoadoutArmorItems(object player, int loadoutIndex, out IReadOnlyList<object> items)
        {
            var result = new List<object>();
            items = result;
            if (player == null || loadoutIndex < 0)
            {
                return false;
            }

            object armor = null;
            var loadouts = GetLoadouts(player);
            var loadout = GetIndexed(loadouts, loadoutIndex);
            if (loadout != null)
            {
                armor = FirstNonNullMember(loadout, "Armor", "armor", "Items", "items");
            }

            int current;
            if (armor == null &&
                TryGetCurrentLoadoutIndex(player, out current) &&
                current == loadoutIndex)
            {
                armor = FirstNonNullMember(player, "armor", "Armor");
            }

            if (armor == null)
            {
                return false;
            }

            var count = Math.Min(10, GetCollectionCount(armor));
            for (var index = 0; index < count; index++)
            {
                result.Add(GetIndexed(armor, index));
            }

            return result.Count > 0;
        }

        public static bool TryIsItemSlotUnlockedAndUsable(object player, int slot, out bool usable)
        {
            usable = false;
            if (player == null || slot < 0)
            {
                return false;
            }

            try
            {
                var method = FindInstanceMethod(player.GetType(), "IsItemSlotUnlockedAndUsable", typeof(int));
                if (method != null)
                {
                    usable = Convert.ToBoolean(method.Invoke(player, new object[] { slot }), CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                usable = false;
                return false;
            }

            usable = slot <= 7;
            return true;
        }

        public static bool TrySwitchLoadout(object player, int targetIndex, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Player unavailable.";
                return false;
            }

            int count;
            if (!TryGetLoadoutCount(player, out count) || targetIndex < 0 || targetIndex >= count)
            {
                message = "Target loadout index out of range: " + targetIndex;
                return false;
            }

            int before;
            TryGetCurrentLoadoutIndex(player, out before);
            if (before == targetIndex)
            {
                message = "Already on target loadout " + targetIndex + ".";
                return true;
            }

            try
            {
                var method = FindInstanceMethod(player.GetType(), "TrySwitchingLoadout", typeof(int));
                if (method == null)
                {
                    message = "Player.TrySwitchingLoadout(int) not found.";
                    return false;
                }

                method.Invoke(player, new object[] { targetIndex });
                int after;
                if (TryGetCurrentLoadoutIndex(player, out after) && after == targetIndex)
                {
                    message = "Switched loadout " + before + " -> " + after + ".";
                    return true;
                }

                message = "TrySwitchingLoadout invoked, but target index was not verified.";
                return true;
            }
            catch (Exception error)
            {
                message = "TrySwitchingLoadout failed: " + error.Message;
                return false;
            }
        }

        public static bool TryReadItemInt(object item, string name, out int value)
        {
            return TryReadInt(item, name, out value);
        }

        public static bool TryReadItemBool(object item, string name, out bool value)
        {
            return TryReadBool(item, name, out value);
        }

        public static bool TryIsItemAir(object item, out bool isAir)
        {
            isAir = true;
            if (item == null)
            {
                return false;
            }

            if (TryReadBool(item, "IsAir", out isAir))
            {
                return true;
            }

            int type;
            int stack;
            TryReadInt(item, "type", out type);
            TryReadInt(item, "stack", out stack);
            isAir = type <= 0 || stack <= 0;
            return true;
        }

        public static bool TryReadExpertMode(out bool expertMode)
        {
            expertMode = false;
            return TryReadStaticBool(TerrariaRuntimeTypes.MainType, "expertMode", out expertMode) ||
                   TryReadStaticBool(TerrariaRuntimeTypes.MainType, "masterMode", out expertMode);
        }

        private static object GetLoadouts(object player)
        {
            return FirstNonNullMember(player, "Loadouts", "loadouts");
        }

        private static MethodInfo FindInstanceMethod(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null)
            {
                return null;
            }

            return type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                parameterTypes ?? Type.EmptyTypes,
                null);
        }

        private static object FirstNonNullMember(object instance, params string[] names)
        {
            for (var index = 0; index < names.Length; index++)
            {
                var value = GetMember(instance, names[index]);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
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

        private static bool TryReadInt(object instance, string name, out int value)
        {
            value = 0;
            var raw = GetMember(instance, name);
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

        private static bool TryReadBool(object instance, string name, out bool value)
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

        private static bool TryReadStaticBool(Type type, string name, out bool value)
        {
            value = false;
            if (type == null)
            {
                return false;
            }

            object raw = null;
            if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
            {
                raw = field.GetValue(null);
            }
            else if (TerrariaMemberCache.TryGetProperty(type, name, true, out var property) && property.CanRead)
            {
                raw = property.GetValue(null, null);
            }

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
