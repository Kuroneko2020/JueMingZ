using System;
using System.Collections;
using JueMingZ.Compat;

namespace JueMingZ.GameState
{
    internal static class GameStateReflection
    {
        // Reflection reads return neutral failures only; readers must surface
        // Unavailable/Unknown snapshots instead of manufacturing state.
        public static object GetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
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

        public static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
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

        public static bool TryGetBool(object instance, string name, out bool value)
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

        public static bool TryGetInt(object instance, string name, out int value)
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

        public static bool TryGetFloat(object instance, string name, out float value)
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

        public static bool TryReadVector2(object vector, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (vector == null)
            {
                return false;
            }

            return TryGetFloat(vector, "X", out x) && TryGetFloat(vector, "Y", out y);
        }

        public static IList AsList(object value)
        {
            return value as IList;
        }
    }
}
