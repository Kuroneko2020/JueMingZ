using System;
using System.Reflection;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class WorldGenDebugCompat
    {
        // Debug command enabling is opt-in and reflected once; unresolved fields
        // leave vanilla debug settings unchanged.
        private const BindingFlags StaticFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly object SyncRoot = new object();
        private static bool _attempted;
        private static bool _enabled;
        private static bool _worldGenSessionConfiguredEnabled;
        private static bool _sessionConfiguredEnabled;
        private static string _status = "notAttempted";
        private static string _message = string.Empty;
        private static string _fieldOwner = string.Empty;
        private static DateTime? _lastAttemptUtc;
        private static FieldInfo _cachedEnableDebugCommandsField;
        private static DateTime _nextKeepAliveUtc = DateTime.MinValue;
        private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(2);

        public static bool Attempted
        {
            get { lock (SyncRoot) { return _attempted; } }
        }

        public static bool Enabled
        {
            get { lock (SyncRoot) { return _enabled; } }
        }

        public static bool SessionConfiguredEnabled
        {
            get { lock (SyncRoot) { return _sessionConfiguredEnabled; } }
        }

        public static bool WorldGenSessionConfiguredEnabled
        {
            get { lock (SyncRoot) { return _worldGenSessionConfiguredEnabled; } }
        }

        public static string Status
        {
            get { lock (SyncRoot) { return _status; } }
        }

        public static string Message
        {
            get { lock (SyncRoot) { return _message; } }
        }

        public static string FieldOwner
        {
            get { lock (SyncRoot) { return _fieldOwner; } }
        }

        public static DateTime? LastAttemptUtc
        {
            get { lock (SyncRoot) { return _lastAttemptUtc; } }
        }

        public static bool IsRestartRequired(bool configuredEnabled)
        {
            return false;
        }

        public static bool IsWorldGenRestartRequired(bool configuredEnabled)
        {
            return false;
        }

        public static bool CanOpenDebugCommands(bool configuredEnabled, out string reasonCode, out string message)
        {
            reasonCode = "ready";
            message = string.Empty;
            return true;
        }

        public static void TryEnableAfterLateBootstrap(bool worldGenDebugViewerConfiguredEnabled, bool developerDebugCommandsConfiguredEnabled)
        {
            lock (SyncRoot)
            {
                if (_attempted)
                {
                    return;
                }

                _attempted = true;
                _lastAttemptUtc = DateTime.UtcNow;
                _worldGenSessionConfiguredEnabled = worldGenDebugViewerConfiguredEnabled;
                _sessionConfiguredEnabled = developerDebugCommandsConfiguredEnabled;
            }

            try
            {
                if (!worldGenDebugViewerConfiguredEnabled && !developerDebugCommandsConfiguredEnabled)
                {
                    RecordResult(
                        "notRequested",
                        false,
                        string.Empty,
                        "Debug commands field left untouched because no caller requested WorldGen Debug Viewer or Developer /hh menu for this session.");
                    Logger.Info("WorldGenDebugCompat", Message);
                    return;
                }

                Type ownerType;
                FieldInfo field;
                if (!TryFindEnableDebugCommandsField(out ownerType, out field))
                {
                    RecordResult(
                        "fieldNotFound",
                        false,
                        string.Empty,
                        "enableDebugCommands field not found; WorldGen Debug viewer patch skipped.");
                    Logger.Warn("WorldGenDebugCompat", Message);
                    return;
                }

                if (field.FieldType != typeof(bool))
                {
                    RecordResult(
                        "fieldNotBoolean",
                        false,
                        ownerType.FullName + "." + field.Name,
                        "enableDebugCommands field is not Boolean; WorldGen Debug viewer patch skipped.");
                    Logger.Warn("WorldGenDebugCompat", Message);
                    return;
                }

                var wasEnabled = false;
                try
                {
                    wasEnabled = Convert.ToBoolean(field.GetValue(null));
                }
                catch
                {
                    wasEnabled = false;
                }

                field.SetValue(null, true);
                var enabled = Convert.ToBoolean(field.GetValue(null));
                var fieldOwner = ownerType.FullName + "." + field.Name;
                if (!enabled)
                {
                    RecordResult(
                        "setFailed",
                        false,
                        fieldOwner,
                        "enableDebugCommands field remained false after set; WorldGen Debug viewer patch skipped.");
                    Logger.Warn("WorldGenDebugCompat", Message);
                    return;
                }

                RecordResult(
                    "enabled",
                    true,
                    fieldOwner,
                    (wasEnabled ? "enableDebugCommands was already true; " : "enableDebugCommands set to true; ") +
                    BuildEnabledMessage(worldGenDebugViewerConfiguredEnabled, developerDebugCommandsConfiguredEnabled));
                lock (SyncRoot)
                {
                    _cachedEnableDebugCommandsField = field;
                }
                Logger.Info("WorldGenDebugCompat", Message + " field=" + fieldOwner);
            }
            catch (Exception error)
            {
                RecordResult(
                    "failed",
                    false,
                    string.Empty,
                    "enableDebugCommands reflection set failed: " + error.Message);
                RuntimeDiagnostics.RecordError("WorldGenDebugCompat.TryEnableAfterLateBootstrap", error);
                Logger.Error("WorldGenDebugCompat", "WorldGen Debug viewer patch failed; exception swallowed.", error);
            }
        }

        private static string BuildEnabledMessage(bool worldGenDebugViewerConfiguredEnabled, bool developerDebugCommandsConfiguredEnabled)
        {
            if (worldGenDebugViewerConfiguredEnabled && developerDebugCommandsConfiguredEnabled)
            {
                return "shared vanilla debug field enabled for WorldGen Debug / Worldgen Viewer and Developer /hh menu.";
            }

            if (worldGenDebugViewerConfiguredEnabled)
            {
                return "vanilla WorldGen Debug / Worldgen Viewer entry is enabled. Developer /hh menu was not requested by caller.";
            }

            return "shared vanilla debug field enabled for Developer /hh menu. WorldGen Viewer setting is off, but Terraria may still expose shared debug entry points while the developer menu is enabled.";
        }

        public static void TryKeepEnabled()
        {
            FieldInfo field;
            var now = DateTime.UtcNow;
            lock (SyncRoot)
            {
                if (!_attempted || !_enabled || _cachedEnableDebugCommandsField == null)
                {
                    return;
                }

                if (now < _nextKeepAliveUtc)
                {
                    return;
                }

                _nextKeepAliveUtc = now.Add(KeepAliveInterval);
                field = _cachedEnableDebugCommandsField;
            }

            try
            {
                var current = Convert.ToBoolean(field.GetValue(null));
                if (current)
                {
                    return;
                }

                field.SetValue(null, true);
                var restored = Convert.ToBoolean(field.GetValue(null));
                if (!restored)
                {
                    LogThrottle.WarnThrottled(
                        "worldgen-debug-keepalive-set-failed",
                        TimeSpan.FromSeconds(10),
                        "WorldGenDebugCompat",
                        "WorldGen debug keep-alive attempted, but enableDebugCommands stayed false.");
                    return;
                }

                LogThrottle.InfoThrottled(
                    "worldgen-debug-keepalive-restored",
                    TimeSpan.FromSeconds(10),
                    "WorldGenDebugCompat",
                    "WorldGen debug keep-alive restored enableDebugCommands after it was reset by runtime.");
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("WorldGenDebugCompat.TryKeepEnabled", error);
                LogThrottle.WarnThrottled(
                    "worldgen-debug-keepalive-error",
                    TimeSpan.FromSeconds(10),
                    "WorldGenDebugCompat",
                    "WorldGen debug keep-alive check failed: " + error.Message);
            }
        }

        private static void RecordResult(string status, bool enabled, string fieldOwner, string message)
        {
            lock (SyncRoot)
            {
                _status = status ?? string.Empty;
                _enabled = enabled;
                _fieldOwner = fieldOwner ?? string.Empty;
                _message = message ?? string.Empty;
            }
        }

        private static bool TryFindEnableDebugCommandsField(out Type ownerType, out FieldInfo field)
        {
            ownerType = null;
            field = null;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                var assembly = assemblies[index];
                if (!IsTerrariaAssembly(assembly))
                {
                    continue;
                }

                var exact = assembly.GetType("Terraria.Testing.DebugOptions", false);
                if (TryGetEnableDebugCommandsField(exact, out field))
                {
                    ownerType = exact;
                    return true;
                }

                exact = assembly.GetType("Terraria.DebugOptions", false);
                if (TryGetEnableDebugCommandsField(exact, out field))
                {
                    ownerType = exact;
                    return true;
                }
            }

            for (var index = 0; index < assemblies.Length; index++)
            {
                var assembly = assemblies[index];
                if (!IsTerrariaAssembly(assembly))
                {
                    continue;
                }

                var types = GetTypesNoThrow(assembly);
                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var type = types[typeIndex];
                    if (type == null)
                    {
                        continue;
                    }

                    if (!string.Equals(type.Name, "DebugOptions", StringComparison.Ordinal) &&
                        !string.Equals(type.Namespace, "Terraria.Testing", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (TryGetEnableDebugCommandsField(type, out field))
                    {
                        ownerType = type;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetEnableDebugCommandsField(Type type, out FieldInfo field)
        {
            field = null;
            if (type == null)
            {
                return false;
            }

            try
            {
                field = type.GetField("enableDebugCommands", StaticFieldFlags);
                return field != null && field.IsStatic;
            }
            catch
            {
                field = null;
                return false;
            }
        }

        private static Type[] GetTypesNoThrow(Assembly assembly)
        {
            if (assembly == null)
            {
                return new Type[0];
            }

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException error)
            {
                return error.Types ?? new Type[0];
            }
            catch
            {
                return new Type[0];
            }
        }

        private static bool IsTerrariaAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                return false;
            }

            try
            {
                var name = assembly.GetName();
                return name != null && string.Equals(name.Name, "Terraria", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
