using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.Information;

namespace JueMingZ.Automation.Search
{
    internal static class SearchReflection
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, MethodInfo> InstanceMethodCache = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        public static Type FindType(string fullName)
        {
            return InformationReflection.FindType(fullName);
        }

        public static object GetStaticMember(Type type, string name)
        {
            return InformationReflection.GetStaticMember(type, name);
        }

        public static object GetMember(object instance, string name)
        {
            return InformationReflection.GetMember(instance, name);
        }

        public static object GetIndexedValue(object source, int index)
        {
            return InformationReflection.GetIndexedValue(source, index);
        }

        public static bool TryReadInt(object instance, string name, out int value)
        {
            return InformationReflection.TryReadInt(instance, name, out value);
        }

        public static string TryReadString(object instance, string name)
        {
            return InformationReflection.TryReadString(instance, name);
        }

        public static int GetCollectionCount(object source)
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

            var collection = source as ICollection;
            return collection == null ? 0 : collection.Count;
        }

        public static bool TryConvertInt(object raw, out int value)
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

        public static bool TryConvertBool(object raw, out bool value)
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

        public static bool TryInvokeStatic(Type type, string methodName, object[] args, out object result)
        {
            return InformationReflection.TryInvokeStatic(type, methodName, args, out result);
        }

        public static bool TryInvokeInstance(object instance, string methodName, object[] args, out object result)
        {
            result = null;
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                var method = ResolveInstanceMethod(type, methodName, args == null ? 0 : args.Length);
                if (method == null)
                {
                    return false;
                }

                result = method.Invoke(instance, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryReadDictionaryEntry(object entry, out object key, out object value)
        {
            key = null;
            value = null;
            if (entry == null)
            {
                return false;
            }

            if (entry is DictionaryEntry)
            {
                var dictionaryEntry = (DictionaryEntry)entry;
                key = dictionaryEntry.Key;
                value = dictionaryEntry.Value;
                return true;
            }

            key = GetMember(entry, "Key");
            value = GetMember(entry, "Value");
            return key != null || value != null;
        }

        private static MethodInfo ResolveInstanceMethod(Type type, string methodName, int argCount)
        {
            var typeKey = string.IsNullOrWhiteSpace(type.AssemblyQualifiedName) ? type.FullName : type.AssemblyQualifiedName;
            var cacheKey = typeKey + "|" + methodName + "|" + argCount.ToString(CultureInfo.InvariantCulture);
            MethodInfo cached;
            lock (SyncRoot)
            {
                if (InstanceMethodCache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }
            }

            MethodInfo resolved = null;
            try
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (method.GetParameters().Length == argCount)
                    {
                        resolved = method;
                        break;
                    }
                }
            }
            catch
            {
                resolved = null;
            }

            lock (SyncRoot)
            {
                InstanceMethodCache[cacheKey] = resolved;
            }

            return resolved;
        }
    }
}
