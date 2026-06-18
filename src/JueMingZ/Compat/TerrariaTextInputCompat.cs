using System;
using System.Reflection;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaTextInputCompat
    {
        // Native text input capture must be explicitly released; unresolved APIs
        // leave IME handling unavailable rather than guessed.
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly object SyncRoot = new object();
        private static bool _resolvedGetInputText;
        private static MethodInfo _getInputTextMethod;
        private static bool _resolvedHandleIme;
        private static MethodInfo _handleImeMethod;
        private static bool _resolvedImeComposition;
        private static MethodInfo _platformGetImeServiceMethod;
        private static PropertyInfo _imeCompositionStringProperty;
        private static bool _captureArmed;
        private static Func<string, string> _inputTextOverrideForTesting;
        private static string _imeCompositionOverrideForTesting;

        public static string LastMessage { get; private set; } = string.Empty;
        public static bool NativeInputAvailable { get; private set; }

        public static bool TryGetInputText(string currentText, out string updatedText, out string message)
        {
            TextInputControlState controlState;
            return TryGetInputText(currentText, false, out updatedText, out controlState, out message);
        }

        public static bool TryGetInputText(string currentText, bool allowMultiLine, out string updatedText, out TextInputControlState controlState, out string message)
        {
            updatedText = currentText ?? string.Empty;
            controlState = new TextInputControlState();
            message = string.Empty;

            try
            {
                if (_inputTextOverrideForTesting != null)
                {
                    updatedText = _inputTextOverrideForTesting(updatedText) ?? updatedText;
                    message = "Terraria native text input test override OK.";
                    LastMessage = message;
                    NativeInputAvailable = true;
                    return true;
                }

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
                TryInvokeHandleIme(mainType);
                var args = BuildGetInputTextArgs(method, updatedText, allowMultiLine);
                var result = method.Invoke(null, args);
                updatedText = result as string ?? updatedText;
                controlState = ReadTextInputControlState(mainType);
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

        public static bool TryGetImeCompositionString(out string compositionString, out string message)
        {
            compositionString = string.Empty;
            message = string.Empty;

            try
            {
                if (_imeCompositionOverrideForTesting != null)
                {
                    compositionString = _imeCompositionOverrideForTesting;
                    return true;
                }

                var method = ResolvePlatformGetImeService();
                var property = _imeCompositionStringProperty;
                if (method == null || property == null)
                {
                    message = "ReLogic IImeService.CompositionString not found; IME preview falls back to committed text.";
                    return false;
                }

                var service = method.Invoke(null, null);
                if (service == null)
                {
                    message = "ReLogic IImeService unavailable; IME preview falls back to committed text.";
                    return false;
                }

                compositionString = property.GetValue(service, null) as string ?? string.Empty;
                return true;
            }
            catch (Exception error)
            {
                message = "Read IME composition failed: " + error.Message;
                LogThrottle.WarnThrottled(
                    "terraria-text-input-ime-composition-read-failed",
                    TimeSpan.FromSeconds(10),
                    "TerrariaTextInputCompat",
                    message);
                return false;
            }
        }

        internal static void SetTextInputOverridesForTesting(Func<string, string> inputTextOverride, string imeCompositionOverride)
        {
            _inputTextOverrideForTesting = inputTextOverride;
            _imeCompositionOverrideForTesting = imeCompositionOverride;
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

        private static void TryInvokeHandleIme(Type mainType)
        {
            try
            {
                var method = ResolveHandleIme(mainType);
                if (method == null)
                {
                    return;
                }

                object instance = null;
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(mainType, "instance", true, out field))
                {
                    instance = field.GetValue(null);
                }

                PropertyInfo property;
                if (instance == null &&
                    TerrariaMemberCache.TryGetProperty(mainType, "instance", true, out property))
                {
                    instance = property.GetValue(null, null);
                }

                if (instance != null)
                {
                    method.Invoke(instance, null);
                }
            }
            catch (Exception error)
            {
                LastMessage = "Handle IME failed: " + error.Message;
                LogThrottle.WarnThrottled(
                    "terraria-text-input-handle-ime-failed",
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

        private static MethodInfo ResolveHandleIme(Type mainType)
        {
            lock (SyncRoot)
            {
                if (_resolvedHandleIme)
                {
                    return _handleImeMethod;
                }

                _resolvedHandleIme = true;
                if (mainType == null)
                {
                    return null;
                }

                _handleImeMethod = mainType.GetMethod(
                    "HandleIME",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                return _handleImeMethod;
            }
        }

        private static MethodInfo ResolvePlatformGetImeService()
        {
            lock (SyncRoot)
            {
                if (_resolvedImeComposition)
                {
                    return _platformGetImeServiceMethod;
                }

                _resolvedImeComposition = true;
                var platformType = FindType("ReLogic.OS.Platform");
                var imeServiceType = FindType("ReLogic.OS.IImeService");
                if (platformType == null || imeServiceType == null)
                {
                    return null;
                }

                _imeCompositionStringProperty = imeServiceType.GetProperty("CompositionString", BindingFlags.Public | BindingFlags.Instance);
                if (_imeCompositionStringProperty == null ||
                    _imeCompositionStringProperty.PropertyType != typeof(string))
                {
                    _imeCompositionStringProperty = null;
                    return null;
                }

                var methods = platformType.GetMethods(StaticFlags);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (method == null ||
                        !method.IsGenericMethodDefinition ||
                        !string.Equals(method.Name, "Get", StringComparison.Ordinal) ||
                        method.GetGenericArguments().Length != 1 ||
                        method.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    _platformGetImeServiceMethod = method.MakeGenericMethod(imeServiceType);
                    return _platformGetImeServiceMethod;
                }

                _imeCompositionStringProperty = null;
                return null;
            }
        }

        private static object[] BuildGetInputTextArgs(MethodInfo method, string currentText, bool allowMultiLine)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            args[0] = currentText ?? string.Empty;
            for (var index = 1; index < parameters.Length; index++)
            {
                var name = parameters[index].Name ?? string.Empty;
                args[index] = name.IndexOf("allowEmpty", StringComparison.OrdinalIgnoreCase) >= 0
                    ? true
                    : allowMultiLine;
            }

            return args;
        }

        private static TextInputControlState ReadTextInputControlState(Type mainType)
        {
            if (mainType == null)
            {
                return new TextInputControlState();
            }

            return new TextInputControlState
            {
                EnterPressed = TryReadStaticBool(mainType, "inputTextEnter"),
                EscapePressed = TryReadStaticBool(mainType, "inputTextEscape")
            };
        }

        private static bool TryReadStaticBool(Type type, string name)
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
                    return (bool)field.GetValue(null);
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) &&
                    property.CanRead &&
                    property.PropertyType == typeof(bool))
                {
                    return (bool)property.GetValue(null, null);
                }
            }
            catch
            {
            }

            return false;
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

    public struct TextInputControlState
    {
        public bool EnterPressed { get; set; }
        public bool EscapePressed { get; set; }
    }
}
