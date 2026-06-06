using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class TerrariaMemberCache
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
        private static readonly Dictionary<string, PropertyInfo> Properties = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        private static bool _initialized;
        private static int _cacheMissCount;
        private static string _lastError = string.Empty;
        private static string _lastMissKey = string.Empty;
        private static DateTime? _lastMissUtc;

        public static bool IsInitialized { get { return _initialized; } }
        public static int CacheMissCount { get { return _cacheMissCount; } }
        public static string LastError { get { return _lastError; } }
        public static string LastMissKey { get { return _lastMissKey; } }
        public static DateTime? LastMissUtc { get { return _lastMissUtc; } }

        public static bool EnsureInitializedLateOnly()
        {
            if (_initialized)
            {
                return true;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return true;
                }

                try
                {
                    if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                    {
                        _lastError = TerrariaRuntimeTypes.LastError;
                        return false;
                    }

                    _initialized = true;
                    _lastError = string.Empty;
                    return true;
                }
                catch (Exception error)
                {
                    _lastError = error.Message;
                    LogThrottle.WarnThrottled(
                        "terraria-member-cache-init-failed",
                        TimeSpan.FromSeconds(30),
                        "TerrariaMemberCache",
                        "Terraria member cache init failed: " + error.Message);
                    return false;
                }
            }
        }

        // Cache successful lookups and misses; false is a fail-closed signal,
        // not permission for callers to invent a compatible member shape.
        public static bool TryGetField(Type type, string name, bool isStatic, out FieldInfo field)
        {
            field = null;
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            EnsureInitializedLateOnly();
            var key = BuildKey(type, name, isStatic, "F");

            lock (SyncRoot)
            {
                if (Fields.TryGetValue(key, out field))
                {
                    return field != null;
                }

                try
                {
                    field = type.GetField(name, isStatic ? StaticFlags : InstanceFlags);
                    Fields[key] = field;
                    if (field == null)
                    {
                        RecordMiss(key, "field not found");
                    }

                    return field != null;
                }
                catch (Exception error)
                {
                    RecordMiss(key, error.GetType().Name + ": " + error.Message);
                    Fields[key] = null;
                    return false;
                }
            }
        }

        public static bool TryGetProperty(Type type, string name, bool isStatic, out PropertyInfo property)
        {
            property = null;
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            EnsureInitializedLateOnly();
            var key = BuildKey(type, name, isStatic, "P");

            lock (SyncRoot)
            {
                if (Properties.TryGetValue(key, out property))
                {
                    return property != null;
                }

                try
                {
                    property = type.GetProperty(name, isStatic ? StaticFlags : InstanceFlags);
                    if (property != null && property.GetIndexParameters().Length != 0)
                    {
                        property = null;
                    }

                    Properties[key] = property;
                    if (property == null)
                    {
                        RecordMiss(key, "property not found or indexed property skipped");
                    }

                    return property != null;
                }
                catch (Exception error)
                {
                    RecordMiss(key, error.GetType().Name + ": " + error.Message);
                    Properties[key] = null;
                    return false;
                }
            }
        }

        private static void RecordMiss(string key, string message)
        {
            _cacheMissCount++;
            _lastMissKey = key ?? string.Empty;
            _lastMissUtc = DateTime.UtcNow;
            _lastError = message ?? string.Empty;
        }

        private static string BuildKey(Type type, string name, bool isStatic, string kind)
        {
            return type.AssemblyQualifiedName + "|" + name + "|" + isStatic + "|" + kind;
        }
    }
}
