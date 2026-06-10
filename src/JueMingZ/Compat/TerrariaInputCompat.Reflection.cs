using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public static bool TryReadItemUseVerificationState(object player, int slot, out ItemUseVerificationState state)
        {
            state = new ItemUseVerificationState();
            if (player == null)
            {
                return Fail("Cannot read item use verification: player unavailable.");
            }

            try
            {
                TryGetBool(player, "active", out var active);
                TryGetBool(player, "dead", out var dead);
                TryGetBool(player, "ghost", out var ghost);
                TryGetInt(player, "itemAnimation", out var itemAnimation);
                TryGetInt(player, "itemTime", out var itemTime);
                TryGetInt(player, "reuseDelay", out var reuseDelay);
                TryGetInt(player, "statLife", out var life);
                TryGetInt(player, "statLifeMax2", out var lifeMax);
                TryGetInt(player, "statMana", out var mana);
                TryGetInt(player, "statManaMax2", out var manaMax);
                TryGetSelectedItem(player, out var selectedSlot);

                state.PlayerActive = active;
                state.PlayerDead = dead;
                state.PlayerGhost = ghost;
                state.ItemAnimation = itemAnimation;
                state.ItemTime = itemTime;
                state.ReuseDelay = reuseDelay;
                state.Life = life;
                state.LifeMax = lifeMax;
                state.Mana = mana;
                state.ManaMax = manaMax;
                state.SelectedSlot = selectedSlot;

                ReadInventoryItemSummary(player, slot, state);
                ReadBuffSummary(player, state);
                return true;
            }
            catch (Exception error)
            {
                return Fail("Read item use verification failed: " + error.Message);
            }
        }

        public static bool TryReadRecoveryCooldowns(object player, out int potionDelay, out bool manaSickness, out int manaSickTime)
        {
            potionDelay = 0;
            manaSickness = false;
            manaSickTime = 0;
            if (player == null)
            {
                return Fail("Cannot read recovery cooldowns: player unavailable.");
            }

            var any = false;
            int value;
            if (TryGetInt(player, "potionDelay", out value))
            {
                potionDelay = value;
                any = true;
            }

            bool boolValue;
            if (TryGetBool(player, "manaSick", out boolValue))
            {
                manaSickness = boolValue;
                any = true;
            }

            if (TryGetBool(player, "manaSickness", out boolValue))
            {
                manaSickness = manaSickness || boolValue;
                any = true;
            }

            if (TryGetInt(player, "manaSickTime", out value))
            {
                manaSickTime = value;
                any = true;
            }

            if (TryGetInt(player, "manaSicknessTime", out value))
            {
                manaSickTime = Math.Max(manaSickTime, value);
                any = true;
            }

            return any;
        }

        private static bool TryGetBoolByNames(object instance, out bool value, params string[] names)
        {
            value = false;
            if (instance == null || names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryGetBool(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetIntByNames(object instance, out int value, params string[] names)
        {
            value = 0;
            if (instance == null || names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryGetInt(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetFloatByNames(object instance, out float value, params string[] names)
        {
            value = 0f;
            if (instance == null || names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryGetFloat(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static object GetStatic(Type type, string name)
        {
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
                return null;
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

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property)
                ? property.GetValue(instance, null)
                : null;
        }

        private static bool IsTerrariaPlayer(object player)
        {
            if (player == null)
            {
                return false;
            }

            var type = player.GetType();
            PlayerTypeName = type.FullName ?? type.Name;
            var expected = TerrariaRuntimeTypes.PlayerType;
            if (expected == null)
            {
                return string.Equals(PlayerTypeName, "Terraria.Player", StringComparison.Ordinal);
            }

            return expected.IsAssignableFrom(type);
        }

        private static bool SetStatic(Type type, string name, object value)
        {
            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    field.SetValue(null, value);
                    return true;
                }

                return Fail("Static member not found: " + name);
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
        }

        private static bool TrySetStaticIfExists(Type type, string name, object value)
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

        private static bool SetMember(object instance, string name, object value)
        {
            if (instance == null)
            {
                return Fail("Instance unavailable for " + name);
            }

            try
            {
                var type = instance.GetType();
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    field.SetValue(instance, value);
                    return true;
                }

                return Fail("Member not found: " + name);
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
        }

        private static bool TrySetMemberIfExists(object instance, string name, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
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
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Optional member set failed for " + name + ": " + error.Message);
            }

            return false;
        }

        private static bool TryGetInt(object instance, string name, out int value)
        {
            value = 0;
            if (instance == null)
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                object raw = null;
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    raw = field.GetValue(instance);
                }
                else if (TerrariaMemberCache.TryGetProperty(type, name, false, out var property))
                {
                    raw = property.GetValue(instance, null);
                }

                if (raw == null)
                {
                    return false;
                }

                value = Convert.ToInt32(raw);
                return true;
            }
            catch (Exception error)
            {
                Fail(error.Message);
                return false;
            }
        }

        private static bool TryGetFloat(object instance, string name, out float value)
        {
            value = 0f;
            if (instance == null)
            {
                return false;
            }

            try
            {
                var raw = GetMember(instance, name);
                if (raw == null)
                {
                    return false;
                }

                value = Convert.ToSingle(raw);
                return true;
            }
            catch (Exception error)
            {
                Fail(error.Message);
                return false;
            }
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
            catch (Exception error)
            {
                Fail(error.Message);
                return false;
            }
        }

        private static bool IsItemPresent(object item)
        {
            if (item == null)
            {
                return false;
            }

            bool isAir;
            if (TryGetBool(item, "IsAir", out isAir))
            {
                return !isAir;
            }

            int type;
            int stack;
            return TryGetInt(item, "type", out type) &&
                   TryGetInt(item, "stack", out stack) &&
                   type > 0 &&
                   stack > 0;
        }

        private static bool TryReadVector2(object vector, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (vector == null)
            {
                return false;
            }

            var type = vector.GetType();
            try
            {
                if (TerrariaMemberCache.TryGetField(type, "X", false, out var xField) &&
                    TerrariaMemberCache.TryGetField(type, "Y", false, out var yField))
                {
                    x = Convert.ToSingle(xField.GetValue(vector));
                    y = Convert.ToSingle(yField.GetValue(vector));
                    return true;
                }
            }
            catch (Exception error)
            {
                Fail(error.Message);
            }

            return false;
        }

        private static void TrySetOptionalStatic(FieldInfo field, PropertyInfo property, int value)
        {
            try
            {
                if (field != null)
                {
                    field.SetValue(null, value);
                }
                else if (property != null && property.CanWrite)
                {
                    property.SetValue(null, value, null);
                }
            }
            catch (Exception error)
            {
                if (LogThrottle.ShouldLog("optional-input-static-set-failed", TimeSpan.FromSeconds(30)))
                {
                    Logger.Debug("TerrariaInputCompat", "Optional input static set failed: " + error.Message);
                }
            }
        }

        private static bool TryGetOptionalStatic(FieldInfo field, PropertyInfo property, out int value)
        {
            value = 0;
            try
            {
                object raw = null;
                if (field != null)
                {
                    raw = field.GetValue(null);
                }
                else if (property != null && property.CanRead)
                {
                    raw = property.GetValue(null, null);
                }

                if (raw == null)
                {
                    return false;
                }

                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ReadInventoryItemSummary(object player, int slot, ItemUseVerificationState state)
        {
            if (slot < 0)
            {
                return;
            }

            var inventory = GetMember(player, "inventory") as IList;
            if (inventory == null || slot >= inventory.Count)
            {
                return;
            }

            var item = inventory[slot];
            if (item == null)
            {
                return;
            }

            TryGetInt(item, "type", out var type);
            TryGetInt(item, "stack", out var stack);
            TryGetInt(item, "useStyle", out var useStyle);
            TryGetBool(item, "consumable", out var consumable);
            TryGetInt(item, "healLife", out var healLife);
            TryGetInt(item, "healMana", out var healMana);
            TryGetInt(item, "buffType", out var buffType);
            TryGetInt(item, "buffTime", out var buffTime);
            var createTile = -1;
            var createWall = -1;
            TryGetInt(item, "createTile", out createTile);
            TryGetInt(item, "createWall", out createWall);
            state.ItemType = type;
            state.ItemStack = stack;
            state.UseStyle = useStyle;
            state.Consumable = consumable;
            state.HealLife = healLife;
            state.HealMana = healMana;
            state.BuffType = buffType;
            state.BuffTime = buffTime;
            state.CreateTile = createTile;
            state.CreateWall = createWall;
            var name = GetMember(item, "Name") ?? GetMember(item, "name");
            state.ItemName = name == null ? string.Empty : name.ToString();
        }

        private static void ReadBuffSummary(object player, ItemUseVerificationState state)
        {
            var buffTypes = GetMember(player, "buffType") as IList;
            var buffTimes = GetMember(player, "buffTime") as IList;
            if (buffTypes == null || buffTimes == null)
            {
                return;
            }

            var max = Math.Min(buffTypes.Count, buffTimes.Count);
            var count = 0;
            var total = 0;
            var types = new StringBuilder();
            types.Append("[");
            var firstType = true;
            for (var index = 0; index < max; index++)
            {
                var type = Convert.ToInt32(buffTypes[index]);
                var time = Convert.ToInt32(buffTimes[index]);
                if (type <= 0 || time <= 0)
                {
                    continue;
                }

                count++;
                total += time;
                if (!firstType)
                {
                    types.Append(",");
                }

                types.Append(type);
                firstType = false;
            }

            types.Append("]");
            state.ActiveBuffCount = count;
            state.BuffTimeTotal = total;
            state.BuffTypesJson = types.ToString();
        }

        private static int GetStaticInt(Type type, string name, int fallback)
        {
            var raw = GetStatic(type, name);
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

        private static bool TryGetStaticInt(Type type, string name, out int value)
        {
            value = 0;
            var raw = GetStatic(type, name);
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

        private static bool TryGetStaticBool(Type type, string name, out bool value)
        {
            value = false;
            object raw;
            try
            {
                raw = GetStatic(type, name);
            }
            catch
            {
                return false;
            }

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

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
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

        private static bool Fail(string message)
        {
            _lastError = message ?? string.Empty;
            InputCompatReady = false;
            LogThrottle.WarnThrottled(
                "terraria-input-compat-failed-" + (_lastError ?? string.Empty),
                TimeSpan.FromSeconds(30),
                "TerrariaInputCompat",
                "Input compat failed: " + _lastError);
            return false;
        }

        private static bool SelectionFail(string message)
        {
            _lastError = string.IsNullOrWhiteSpace(message) ? "selected item compat failed." : message;
            LogThrottle.WarnThrottled(
                "terraria-input-selection-failed-" + _lastError,
                TimeSpan.FromSeconds(30),
                "TerrariaInputCompat",
                "Selected item compat failed: " + _lastError);
            return false;
        }

        private static bool ClearSelectionError()
        {
            _lastError = string.Empty;
            InputCompatReady = true;
            return true;
        }

        private static bool ClearInputError()
        {
            _lastError = string.Empty;
            InputCompatReady = true;
            return true;
        }
    }
}
