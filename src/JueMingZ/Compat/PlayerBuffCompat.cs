using System;
using System.Collections;
using System.Reflection;

namespace JueMingZ.Compat
{
    public static class PlayerBuffCompat
    {
        private static MethodInfo _addBuffMethod;
        private static bool _addBuffResolved;

        public static bool TryReadPlayerAvailability(object player, out bool active, out bool dead, out bool ghost)
        {
            active = false;
            dead = false;
            ghost = false;
            if (player == null)
            {
                return false;
            }

            TryGetBool(player, "active", out active);
            TryGetBool(player, "dead", out dead);
            TryGetBool(player, "ghost", out ghost);
            return true;
        }

        public static bool TryReadBuffTime(object player, int buffType, out int buffTime)
        {
            buffTime = 0;
            if (player == null || buffType <= 0)
            {
                return false;
            }

            var buffTypes = GetMember(player, "buffType") as IList;
            var buffTimes = GetMember(player, "buffTime") as IList;
            if (buffTypes == null || buffTimes == null)
            {
                return false;
            }

            var max = Math.Min(buffTypes.Count, buffTimes.Count);
            for (var index = 0; index < max; index++)
            {
                var type = Convert.ToInt32(buffTypes[index]);
                var time = Convert.ToInt32(buffTimes[index]);
                if (type == buffType && time > 0)
                {
                    buffTime = time;
                    return true;
                }
            }

            return true;
        }

        public static bool HasActiveBuff(object player, int buffType)
        {
            int buffTime;
            return TryReadBuffTime(player, buffType, out buffTime) && buffTime > 0;
        }

        public static bool TryAddBuff(object player, int buffType, int buffTime, out bool methodInvoked, out string message)
        {
            methodInvoked = false;
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            if (buffType <= 0 || buffTime <= 0)
            {
                message = "Invalid buffType or buffTime.";
                return false;
            }

            var method = ResolveAddBuff(player.GetType());
            if (method == null)
            {
                message = "Player.AddBuff method was not found.";
                return false;
            }

            try
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 3)
                {
                    method.Invoke(player, new object[] { buffType, buffTime, false });
                }
                else
                {
                    method.Invoke(player, new object[] { buffType, buffTime });
                }

                methodInvoked = true;
                message = "Player.AddBuff invoked.";
                return true;
            }
            catch (Exception error)
            {
                message = "Player.AddBuff failed: " + (error.InnerException == null ? error.Message : error.InnerException.Message);
                return false;
            }
        }

        public static bool TryApplyItemBuff(object player, int buffType, int buffTime, out bool methodInvoked, out string message)
        {
            if (buffType > 0 && buffTime <= 0)
            {
                buffTime = 3600;
            }

            return TryAddBuff(player, buffType, buffTime, out methodInvoked, out message);
        }

        public static int CountActiveBuffs(object player)
        {
            var count = 0;
            var buffTypes = GetMember(player, "buffType") as IList;
            var buffTimes = GetMember(player, "buffTime") as IList;
            if (buffTypes == null || buffTimes == null)
            {
                return count;
            }

            var max = Math.Min(buffTypes.Count, buffTimes.Count);
            for (var index = 0; index < max; index++)
            {
                if (Convert.ToInt32(buffTypes[index]) > 0 && Convert.ToInt32(buffTimes[index]) > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static MethodInfo ResolveAddBuff(Type playerType)
        {
            if (_addBuffResolved)
            {
                return _addBuffMethod;
            }

            _addBuffResolved = true;
            if (playerType == null)
            {
                return null;
            }

            var methods = playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "AddBuff", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if ((parameters.Length == 2 || parameters.Length == 3) &&
                    parameters[0].ParameterType == typeof(int) &&
                    parameters[1].ParameterType == typeof(int) &&
                    (parameters.Length == 2 || parameters[2].ParameterType == typeof(bool)))
                {
                    _addBuffMethod = method;
                    return _addBuffMethod;
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

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property)
                ? property.GetValue(instance, null)
                : null;
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
    }
}
