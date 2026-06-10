using System;
using System.Reflection;
using JueMingZ.Automation.Movement;

namespace JueMingZ.Compat
{
    public static partial class MovementSafeLandingCompat
    {
        private static bool TryReadJumpInputProfile(
            object player,
            MovementInputFrameCache.MovementInputFrame inputFrame,
            out JumpInputProfile jump,
            out string failureReason)
        {
            jump = null;
            failureReason = string.Empty;
            if (inputFrame != null && inputFrame.TryGetJumpProfile(out jump, out failureReason))
            {
                return true;
            }

            if (TerrariaInputCompat.TryReadJumpInputProfile(player, out jump) && jump != null)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = !string.IsNullOrWhiteSpace(TerrariaInputCompat.LastInputCompatError)
                ? TerrariaInputCompat.LastInputCompatError
                : failureReason ?? string.Empty;
            return false;
        }

        private static bool TryReadMountNoFallDamage(object player)
        {
            try
            {
                var mount = GetMember(player, "mount");
                if (mount == null)
                {
                    return false;
                }

                var active = TryReadBool(mount, "Active", false) || TryReadBool(mount, "_active", false);
                if (!active)
                {
                    return false;
                }

                var mountType = TryReadInt(mount, "Type", TryReadInt(mount, "_type", -1));
                if (mountType < 0)
                {
                    return false;
                }

                var mountTypeType = FindType("Terraria.Mount");
                if (mountTypeType == null)
                {
                    return false;
                }

                var mounts = GetStaticMember(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                return data != null && TryReadFloat(data, "fallDamage", 1f) <= 0f;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadVectorMember(object instance, string name, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            var vector = GetMember(instance, name);
            if (vector == null)
            {
                return false;
            }

            return TryReadVector2(vector, out x, out y);
        }

        private static bool TryReadVector2(object vector, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (vector == null)
            {
                return false;
            }

            return TryReadFloat(vector, "X", out x) && TryReadFloat(vector, "Y", out y);
        }

        private static bool TryReadBool(object instance, string name, bool fallback)
        {
            bool value;
            return TryReadBool(instance, name, out value) ? value : fallback;
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
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int TryReadInt(object instance, string name, int fallback)
        {
            int value;
            return TryReadInt(instance, name, out value) ? value : fallback;
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
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static float TryReadFloat(object instance, string name, float fallback)
        {
            float value;
            return TryReadFloat(instance, name, out value) ? value : fallback;
        }

        private static bool TryReadFloat(object instance, string name, out float value)
        {
            value = 0f;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToSingle(raw);
                return true;
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
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(instance.GetType(), name, false, out field) && field != null)
                {
                    return field.GetValue(instance);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(instance.GetType(), name, false, out property) && property != null && property.CanRead)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field) && field != null)
                {
                    return field.GetValue(null);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property != null && property.CanRead)
                {
                    return property.GetValue(null, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool TryInvokeInt(object instance, string methodName, out int value)
        {
            value = 0;
            object raw;
            if (!TryInvokeNoArg(instance, methodName, out raw) || raw == null)
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

        private static bool TryInvokeBool(object instance, string methodName, out bool value)
        {
            value = false;
            object raw;
            if (!TryInvokeNoArg(instance, methodName, out raw) || raw == null)
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

        private static bool TryInvokeNoArg(object instance, string methodName, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var method = instance.GetType().GetMethod(
                    methodName,
                    InstanceFlags,
                    null,
                    Type.EmptyTypes,
                    null);
                if (method == null)
                {
                    return false;
                }

                value = method.Invoke(instance, new object[0]);
                return true;
            }
            catch
            {
                return false;
            }
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

        private static bool ClearError()
        {
            _lastError = string.Empty;
            return true;
        }

        private static bool Fail(string message)
        {
            _lastError = message ?? string.Empty;
            return false;
        }
    }
}

