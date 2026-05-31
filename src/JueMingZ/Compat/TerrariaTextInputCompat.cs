using System;
using System.Reflection;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class TerrariaTextInputCompat
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly object SyncRoot = new object();
        private static bool _resolvedGetInputText;
        private static MethodInfo _getInputTextMethod;
        private static bool _captureArmed;

        public static string LastMessage { get; private set; } = string.Empty;
        public static bool NativeInputAvailable { get; private set; }

        public static bool TryGetInputText(string currentText, out string updatedText, out string message)
        {
            updatedText = currentText ?? string.Empty;
            message = string.Empty;

            try
            {
                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null)
                {
                    message = "Terraria.Main unavailable; native text input cannot be used.";
                    LastMessage = message;
                    NativeInputAvailable = false;
                    return false;
                }

                var method = ResolveGetInputText(mainType);
                if (method == null)
                {
                    message = "Terraria.Main.GetInputText(string, ...) not found; IME text input is unavailable.";
                    LastMessage = message;
                    NativeInputAvailable = false;
                    LogThrottle.WarnThrottled(
                        "terraria-text-input-native-api-missing",
                        TimeSpan.FromSeconds(10),
                        "TerrariaTextInputCompat",
                        message);
                    return false;
                }

                TrySetNativeInputCapture(true);
                var args = BuildGetInputTextArgs(method, updatedText);
                var result = method.Invoke(null, args);
                updatedText = result as string ?? updatedText;
                message = "Terraria native text input OK.";
                LastMessage = message;
                NativeInputAvailable = true;
                return true;
            }
            catch (Exception error)
            {
                message = "Terraria native text input failed: " + error.Message;
                LastMessage = message;
                NativeInputAvailable = false;
                LogThrottle.WarnThrottled(
                    "terraria-text-input-native-api-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaTextInputCompat",
                    message);
                return false;
            }
        }

        public static void EndTextInput()
        {
            try
            {
                if (!_captureArmed)
                {
                    return;
                }

                TrySetNativeInputCapture(false);
                _captureArmed = false;
            }
            catch (Exception error)
            {
                LastMessage = "End native text input failed: " + error.Message;
                LogThrottle.WarnThrottled(
                    "terraria-text-input-native-release-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaTextInputCompat",
                    LastMessage);
            }
        }

        private static MethodInfo ResolveGetInputText(Type mainType)
        {
            lock (SyncRoot)
            {
                if (_resolvedGetInputText)
                {
                    return _getInputTextMethod;
                }

                _resolvedGetInputText = true;
                if (mainType == null)
                {
                    return null;
                }

                var methods = mainType.GetMethods(StaticFlags);
                for (var pass = 0; pass < 2; pass++)
                {
                    for (var index = 0; index < methods.Length; index++)
                    {
                        var method = methods[index];
                        if (method == null ||
                            !string.Equals(method.Name, "GetInputText", StringComparison.Ordinal) ||
                            method.ReturnType != typeof(string))
                        {
                            continue;
                        }

                        var parameters = method.GetParameters();
                        if (parameters.Length <= 0 ||
                            parameters.Length > 3 ||
                            parameters[0].ParameterType != typeof(string))
                        {
                            continue;
                        }

                        var compatible = true;
                        for (var parameterIndex = 1; parameterIndex < parameters.Length; parameterIndex++)
                        {
                            if (parameters[parameterIndex].ParameterType != typeof(bool))
                            {
                                compatible = false;
                                break;
                            }
                        }

                        if (!compatible)
                        {
                            continue;
                        }

                        if (pass == 0 && parameters.Length != 2)
                        {
                            continue;
                        }

                        _getInputTextMethod = method;
                        return _getInputTextMethod;
                    }
                }

                return null;
            }
        }

        private static object[] BuildGetInputTextArgs(MethodInfo method, string currentText)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            args[0] = currentText ?? string.Empty;
            for (var index = 1; index < parameters.Length; index++)
            {
                var name = parameters[index].Name ?? string.Empty;
                args[index] = name.IndexOf("allowEmpty", StringComparison.OrdinalIgnoreCase) >= 0 || parameters.Length == 2;
            }

            return args;
        }

        private static void TrySetNativeInputCapture(bool active)
        {
            var changed = false;
            var mainType = TerrariaRuntimeTypes.MainType;
            changed |= TrySetStaticBool(mainType, "blockInput", active);

            var playerInputType = FindType("Terraria.GameInput.PlayerInput");
            changed |= TrySetStaticBool(playerInputType, "WritingText", active);

            if (active && changed)
            {
                _captureArmed = true;
            }
        }

        private static bool TrySetStaticBool(Type type, string name, bool value)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field) && field.FieldType == typeof(bool))
                {
                    field.SetValue(null, value);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) &&
                    property.CanWrite &&
                    property.PropertyType == typeof(bool))
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
    }
}
