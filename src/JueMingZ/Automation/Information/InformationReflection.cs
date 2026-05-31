using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Information
{
    internal static class InformationReflection
    {
        private static readonly object CacheSyncRoot = new object();
        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly Dictionary<string, MethodInfo> StaticMethodCache = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        private static readonly Dictionary<string, MethodInfo> ParameterlessMemberMethodCache = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        public static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            var cleanName = StripAssemblyName(fullName);
            Type cached;
            lock (CacheSyncRoot)
            {
                if (TypeCache.TryGetValue(cleanName, out cached))
                {
                    return cached;
                }
            }

            Type resolved = null;
            try
            {
                resolved = Type.GetType(fullName, false);
            }
            catch
            {
            }

            if (resolved == null && !string.Equals(fullName, cleanName, StringComparison.Ordinal))
            {
                try
                {
                    resolved = Type.GetType(cleanName, false);
                }
                catch
                {
                }
            }

            if (resolved == null)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (var index = 0; index < assemblies.Length; index++)
                {
                    try
                    {
                        resolved = assemblies[index].GetType(cleanName, false);
                        if (resolved != null)
                        {
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (resolved != null)
            {
                lock (CacheSyncRoot)
                {
                    TypeCache[cleanName] = resolved;
                }
            }

            return resolved;
        }

        public static object GetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    return field.GetValue(null);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanRead)
                {
                    return property.GetValue(null, null);
                }

                var method = ResolveParameterlessMemberMethod(type, name, true);
                return method == null ? null : method.Invoke(null, null);
            }
            catch
            {
                return null;
            }
        }

        public static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    return field.GetValue(instance);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanRead)
                {
                    return property.GetValue(instance, null);
                }

                var method = ResolveParameterlessMemberMethod(type, name, false);
                return method == null ? null : method.Invoke(instance, null);
            }
            catch
            {
                return null;
            }
        }

        public static bool TryReadStaticBool(Type type, string name, out bool value)
        {
            value = false;
            return TryConvert(GetStaticMember(type, name), out value);
        }

        public static bool TryReadStaticInt(Type type, string name, out int value)
        {
            value = 0;
            return TryConvert(GetStaticMember(type, name), out value);
        }

        public static bool TryReadStaticULong(Type type, string name, out ulong value)
        {
            value = 0;
            return TryConvert(GetStaticMember(type, name), out value);
        }

        public static bool TryReadBool(object instance, string name, out bool value)
        {
            value = false;
            return TryConvert(GetMember(instance, name), out value);
        }

        public static bool TryReadInt(object instance, string name, out int value)
        {
            value = 0;
            return TryConvert(GetMember(instance, name), out value);
        }

        public static bool TryReadFloat(object instance, string name, out float value)
        {
            value = 0f;
            return TryConvert(GetMember(instance, name), out value);
        }

        public static bool TryReadDouble(object instance, string name, out double value)
        {
            value = 0d;
            return TryConvert(GetMember(instance, name), out value);
        }

        public static string TryReadString(object instance, string name)
        {
            try
            {
                var raw = GetMember(instance, name);
                return raw == null ? string.Empty : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string TryReadStaticString(Type type, string name)
        {
            try
            {
                var raw = GetStaticMember(type, name);
                return raw == null ? string.Empty : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool TryReadVector2(object vector, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            return vector != null &&
                   TryReadFloat(vector, "X", out x) &&
                   TryReadFloat(vector, "Y", out y);
        }

        public static bool TryReadVectorMember(object source, string name, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            return TryReadVector2(GetMember(source, name), out x, out y);
        }

        public static bool TryReadRectangle(object rectangle, out int x, out int y, out int width, out int height)
        {
            x = 0;
            y = 0;
            width = 0;
            height = 0;
            return rectangle != null &&
                   TryReadInt(rectangle, "X", out x) &&
                   TryReadInt(rectangle, "Y", out y) &&
                   TryReadInt(rectangle, "Width", out width) &&
                   TryReadInt(rectangle, "Height", out height);
        }

        public static IList AsList(object value)
        {
            return value as IList;
        }

        public static object GetIndexedValue(object source, int index)
        {
            if (source == null || index < 0)
            {
                return null;
            }

            try
            {
                var list = source as IList;
                if (list != null)
                {
                    return index < list.Count ? list[index] : null;
                }

                var array = source as Array;
                if (array != null && array.Rank == 1 && index < array.GetLength(0))
                {
                    return array.GetValue(index);
                }
            }
            catch
            {
            }

            return null;
        }

        public static object GetTileAt(object tileCollection, int x, int y)
        {
            if (tileCollection == null || x < 0 || y < 0)
            {
                return null;
            }

            try
            {
                var array = tileCollection as Array;
                if (array != null)
                {
                    if (array.Rank == 2 &&
                        x < array.GetLength(0) &&
                        y < array.GetLength(1))
                    {
                        return array.GetValue(x, y);
                    }

                    if (array.Rank == 1 && x < array.GetLength(0))
                    {
                        var row = array.GetValue(x);
                        return GetIndexedValue(row, y);
                    }
                }

                var list = tileCollection as IList;
                if (list != null && x < list.Count)
                {
                    return GetIndexedValue(list[x], y);
                }
            }
            catch
            {
            }

            return null;
        }

        public static bool TryInvokeStatic(Type type, string methodName, object[] args, out object result)
        {
            result = null;
            if (type == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var method = ResolveStaticMethod(type, methodName, args == null ? 0 : args.Length);
                if (method == null)
                {
                    return false;
                }

                result = method.Invoke(null, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string StripAssemblyName(string fullName)
        {
            var cleanName = fullName == null ? string.Empty : fullName.Trim();
            var comma = cleanName.IndexOf(',');
            if (comma >= 0)
            {
                cleanName = cleanName.Substring(0, comma).Trim();
            }

            return cleanName;
        }

        private static MethodInfo ResolveStaticMethod(Type type, string methodName, int argCount)
        {
            var typeKey = string.IsNullOrWhiteSpace(type.AssemblyQualifiedName) ? type.FullName : type.AssemblyQualifiedName;
            var cacheKey = typeKey + "|" + methodName + "|" + argCount.ToString(CultureInfo.InvariantCulture);
            MethodInfo cached;
            lock (CacheSyncRoot)
            {
                if (StaticMethodCache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }
            }

            try
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != argCount)
                    {
                        continue;
                    }

                    lock (CacheSyncRoot)
                    {
                        StaticMethodCache[cacheKey] = method;
                    }

                    return method;
                }
            }
            catch
            {
            }

            return null;
        }

        private static MethodInfo ResolveParameterlessMemberMethod(Type type, string methodName, bool isStatic)
        {
            var typeKey = string.IsNullOrWhiteSpace(type.AssemblyQualifiedName) ? type.FullName : type.AssemblyQualifiedName;
            var cacheKey = typeKey + "|" + methodName + "|" + (isStatic ? "static" : "instance") + "|M0";
            MethodInfo cached;
            lock (CacheSyncRoot)
            {
                if (ParameterlessMemberMethodCache.ContainsKey(cacheKey))
                {
                    ParameterlessMemberMethodCache.TryGetValue(cacheKey, out cached);
                    return cached;
                }
            }

            MethodInfo resolved = null;
            try
            {
                resolved = type.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance),
                    null,
                    Type.EmptyTypes,
                    null);
            }
            catch
            {
                resolved = null;
            }

            lock (CacheSyncRoot)
            {
                ParameterlessMemberMethodCache[cacheKey] = resolved;
            }

            return resolved;
        }

        private static bool TryConvert(object raw, out bool value)
        {
            value = false;
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

        private static bool TryConvert(object raw, out int value)
        {
            value = 0;
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

        private static bool TryConvert(object raw, out ulong value)
        {
            value = 0;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToUInt64(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvert(object raw, out float value)
        {
            value = 0f;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvert(object raw, out double value)
        {
            value = 0d;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
