using System;
using System.Collections;
using System.Reflection;

namespace JueMingZ.Compat
{
    public static class InventoryMutationCompat
    {
        // This is the narrow inventory mutation boundary; readers may inspect
        // fields, but stack writes must verify item identity first.
        public static bool TryGetItem(object player, string sourceContainer, int sourceSlot, out object item, out string message)
        {
            item = null;
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            if (sourceSlot < 0)
            {
                message = "Invalid source slot.";
                return false;
            }

            IList items;
            if (!TryGetContainerItems(player, sourceContainer, out items, out message))
            {
                return false;
            }

            if (sourceSlot >= items.Count)
            {
                message = "Source slot is outside container bounds.";
                return false;
            }

            item = items[sourceSlot];
            if (item == null)
            {
                message = "Source item is null.";
                return false;
            }

            return true;
        }

        public static bool TryGetContainerItems(object player, string sourceContainer, out IList items, out string message)
        {
            items = null;
            message = string.Empty;
            var container = sourceContainer ?? string.Empty;
            if (string.Equals(container, "Inventory", StringComparison.OrdinalIgnoreCase))
            {
                items = GetMember(player, "inventory") as IList;
                if (items == null)
                {
                    message = "Player inventory is unavailable.";
                    return false;
                }

                return true;
            }

            if (string.Equals(container, "VoidBag", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryPlayerUsesVoidBag(player))
                {
                    message = "Void bag is not enabled or not readable.";
                    return false;
                }

                var bank4 = GetMember(player, "bank4");
                items = GetMember(bank4, "item") as IList;
                if (items == null)
                {
                    message = "Void bag item list is unavailable.";
                    return false;
                }

                return true;
            }

            message = "Unsupported source container: " + container;
            return false;
        }

        public static bool TryPlayerUsesVoidBag(object player)
        {
            if (player == null)
            {
                return false;
            }

            var method = player.GetType().GetMethod("useVoidBag", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null && method.GetParameters().Length == 0)
            {
                try
                {
                    return Convert.ToBoolean(method.Invoke(player, new object[0]));
                }
                catch
                {
                    return false;
                }
            }

            bool value;
            return TryGetBool(player, "useVoidBag", out value) && value;
        }

        public static bool TryReadItemFields(object item, out int itemType, out string itemName, out int stack, out int buffType, out int buffTime, out bool summon)
        {
            itemType = 0;
            itemName = string.Empty;
            stack = 0;
            buffType = 0;
            buffTime = 0;
            summon = false;
            if (item == null)
            {
                return false;
            }

            TryGetInt(item, "type", out itemType);
            TryGetInt(item, "stack", out stack);
            TryGetInt(item, "buffType", out buffType);
            TryGetInt(item, "buffTime", out buffTime);
            TryGetBool(item, "summon", out summon);
            var rawName = GetMember(item, "Name") ?? GetMember(item, "name");
            itemName = rawName == null ? string.Empty : rawName.ToString();
            return true;
        }

        public static bool TryReadRecoveryItemFields(
            object item,
            out int itemType,
            out string itemName,
            out int stack,
            out int healLife,
            out int healMana,
            out bool potion,
            out bool consumable,
            out int buffType,
            out int buffTime)
        {
            itemType = 0;
            itemName = string.Empty;
            stack = 0;
            healLife = 0;
            healMana = 0;
            potion = false;
            consumable = false;
            buffType = 0;
            buffTime = 0;
            if (item == null)
            {
                return false;
            }

            TryGetInt(item, "type", out itemType);
            TryGetInt(item, "stack", out stack);
            TryGetInt(item, "healLife", out healLife);
            TryGetInt(item, "healMana", out healMana);
            TryGetBool(item, "potion", out potion);
            TryGetBool(item, "consumable", out consumable);
            TryGetInt(item, "buffType", out buffType);
            TryGetInt(item, "buffTime", out buffTime);
            var rawName = GetMember(item, "Name") ?? GetMember(item, "name");
            itemName = rawName == null ? string.Empty : rawName.ToString();
            return true;
        }

        // Controlled inventory mutation for local fallback use only; callers
        // must verify item identity and stack before decrementing.
        public static bool TryConsumeOneItem(object player, string sourceContainer, int sourceSlot, int expectedItemType, out int stackBefore, out int stackAfter, out string message)
        {
            stackBefore = 0;
            stackAfter = 0;
            message = string.Empty;

            object item;
            if (!TryGetItem(player, sourceContainer, sourceSlot, out item, out message))
            {
                return false;
            }

            int itemType;
            int buffType;
            int buffTime;
            bool summon;
            string itemName;
            if (!TryReadItemFields(item, out itemType, out itemName, out stackBefore, out buffType, out buffTime, out summon))
            {
                message = "Cannot read source item fields.";
                return false;
            }

            if (itemType != expectedItemType || stackBefore <= 0)
            {
                message = "Source item changed before consume.";
                return false;
            }

            stackAfter = stackBefore - 1;
            if (stackAfter <= 0)
            {
                if (TryTurnToAir(item))
                {
                    stackAfter = 0;
                    message = "Source item consumed and TurnToAir invoked.";
                    return true;
                }

                if (!TrySetInt(item, "stack", 0))
                {
                    message = "Source item consumed, but stack could not be set to 0.";
                    return false;
                }

                stackAfter = 0;
                message = "Source item stack set to 0; TurnToAir unavailable.";
                return true;
            }

            if (!TrySetInt(item, "stack", stackAfter))
            {
                message = "Source item stack mutation failed.";
                return false;
            }

            message = "Source item stack decremented by one.";
            return true;
        }

        public static void ReadNetworkState(out int netMode, out string networkMode, out bool multiplayerClient)
        {
            netMode = 0;
            networkMode = "Unknown";
            multiplayerClient = false;
            var mainType = TerrariaRuntimeTypes.MainType;
            object raw = null;
            if (TerrariaMemberCache.TryGetField(mainType, "netMode", true, out var field))
            {
                raw = field.GetValue(null);
            }
            else if (TerrariaMemberCache.TryGetProperty(mainType, "netMode", true, out var property))
            {
                raw = property.GetValue(null, null);
            }

            if (raw != null)
            {
                netMode = Convert.ToInt32(raw);
            }

            if (netMode == 0)
            {
                networkMode = "SinglePlayer";
            }
            else if (netMode == 1)
            {
                networkMode = "MultiplayerClient";
                multiplayerClient = true;
            }
            else if (netMode == 2)
            {
                networkMode = "Server";
            }
            else
            {
                networkMode = "NetMode" + netMode;
            }
        }

        public static void DetermineSyncResult(bool multiplayerClient, out bool syncAttempted, out string syncMethod, out bool syncSucceeded, out string syncResult)
        {
            if (!multiplayerClient)
            {
                syncAttempted = false;
                syncMethod = "NotRequired";
                syncSucceeded = true;
                syncResult = "NoSyncRequired";
                return;
            }

            syncAttempted = false;
            syncMethod = "UnsupportedNetworking";
            syncSucceeded = false;
            syncResult = "UnsupportedNetworking";
        }

        private static bool TryTurnToAir(object item)
        {
            if (item == null)
            {
                return false;
            }

            var type = item.GetType();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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

        private static bool TrySetInt(object instance, string name, int value)
        {
            if (instance == null)
            {
                return false;
            }

            var type = instance.GetType();
            try
            {
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
                return false;
            }

            return false;
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
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetInt(object instance, string name, out int value)
        {
            value = 0;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
