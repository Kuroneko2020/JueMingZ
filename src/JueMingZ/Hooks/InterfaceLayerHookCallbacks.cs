using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Diagnostics;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Hooks
{
    public static class InterfaceLayerHookCallbacks
    {
        private const string LegacyInputGuardLayerName = "JueMing-Z: Legacy Main UI Input Guard";
        private const string GameOverlayDispatcherLayerName = "JueMing-Z: Game Overlay Dispatcher";
        private const string UiOverlayDispatcherLayerName = "JueMing-Z: UI Overlay Dispatcher";
        private const string LegacyMouseTextGuardLayerName = "JueMing-Z: Legacy Main UI Mouse Text Guard";

        private static readonly object ReflectionCacheSyncRoot = new object();
        private static readonly Dictionary<Type, LayerNameAccessor> LayerNameAccessorCache = new Dictionary<Type, LayerNameAccessor>();

        private static int _firstLegacyInputGuardInsertLogged;
        private static int _firstGameOverlayDispatcherInsertLogged;
        private static int _firstUiOverlayDispatcherInsertLogged;
        private static int _firstLegacyMouseTextGuardInsertLogged;

        private static Type _gameInterfaceLayerType;
        private static Type _legacyLayerType;
        private static Type _scaleType;
        private static Type _drawDelegateType;
        private static ConstructorInfo _legacyLayerConstructor;
        private static object _uiScaleValue;
        private static object _gameScaleValue;

        private static Type _layerListOwnerType;
        private static FieldInfo _layerListField;
        private static PropertyInfo _layerListProperty;
        private static bool _layerListAccessorResolved;

        private static void Postfix(object __instance)
        {
            try
            {
                InsertDiagnosticsLayer(__instance);
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "interface-layer-postfix-error",
                    TimeSpan.FromSeconds(10),
                    "InterfaceLayerHookCallbacks",
                    "Interface layer postfix failed; exception swallowed.", error);
            }
        }

        private static void InsertDiagnosticsLayer(object mainInstance)
        {
            if (mainInstance == null)
            {
                return;
            }

            if (!EnsureLayerReflectionReady())
            {
                LogThrottle.WarnThrottled(
                    "interface-layer-types-missing",
                    TimeSpan.FromSeconds(10),
                    "InterfaceLayerHookCallbacks",
                    "Interface layer types are unavailable; diagnostics overlay layer not inserted.");
                return;
            }

            var layers = FindLayerList(mainInstance, _gameInterfaceLayerType);
            if (layers == null)
            {
                LogThrottle.WarnThrottled(
                    "interface-layer-list-missing",
                    TimeSpan.FromSeconds(10),
                    "InterfaceLayerHookCallbacks",
                    "GameInterfaceLayer list was not found; diagnostics overlay layer not inserted.");
                return;
            }

            var layerState = BuildLayerSearchState(layers);

            InsertLayerIfMissing(
                layers,
                layerState,
                LegacyInputGuardLayerName,
                typeof(LegacyMainWindow).GetMethod("DrawInputGuardLayer", BindingFlags.Public | BindingFlags.Static),
                _uiScaleValue,
                ref _firstLegacyInputGuardInsertLogged,
                "Legacy main UI input guard layer inserted.",
                0);

            InsertLayerIfMissing(
                layers,
                layerState,
                GameOverlayDispatcherLayerName,
                typeof(InterfaceLayerHookCallbacks).GetMethod("DrawGameOverlayDispatcherLayer", BindingFlags.Public | BindingFlags.Static),
                _gameScaleValue,
                ref _firstGameOverlayDispatcherInsertLogged,
                "Game overlay dispatcher interface layer inserted.",
                -1);

            InsertLayerIfMissing(
                layers,
                layerState,
                UiOverlayDispatcherLayerName,
                typeof(InterfaceLayerHookCallbacks).GetMethod("DrawUiOverlayDispatcherLayer", BindingFlags.Public | BindingFlags.Static),
                _uiScaleValue,
                ref _firstUiOverlayDispatcherInsertLogged,
                "UI overlay dispatcher interface layer inserted.",
                -1);

            InsertLayerIfMissing(
                layers,
                layerState,
                LegacyMouseTextGuardLayerName,
                typeof(LegacyMainWindow).GetMethod("DrawMouseTextGuardLayer", BindingFlags.Public | BindingFlags.Static),
                _uiScaleValue,
                ref _firstLegacyMouseTextGuardInsertLogged,
                "Legacy main UI mouse text guard layer inserted.",
                layerState.MouseTextIndex);
        }

        public static bool DrawGameOverlayDispatcherLayer()
        {
            UiInputFrameClock.BeginDrawFrame("GameOverlayDispatcher");
            var keepDrawing = true;
            keepDrawing &= InformationWorldOverlay.DrawInterfaceLayer();
            keepDrawing &= FishingStatusPromptOverlay.DrawInterfaceLayer();
            keepDrawing &= FirstWorldLoadPromptOverlay.DrawInterfaceLayer();
            keepDrawing &= CombatEquipmentWarningPromptOverlay.DrawInterfaceLayer();
            keepDrawing &= CombatAimMarkerOverlay.DrawInterfaceLayer();
            return keepDrawing;
        }

        public static bool DrawUiOverlayDispatcherLayer()
        {
            UiInputFrameClock.BeginDrawFrame("UiOverlayDispatcher");
            var keepDrawing = true;
            keepDrawing &= InformationStatusPanelOverlay.DrawInterfaceLayer();
            keepDrawing &= LegacyMainWindow.DrawInterfaceLayer();
            return keepDrawing;
        }

        private static IList FindLayerList(object mainInstance, Type gameInterfaceLayerType)
        {
            var mainType = mainInstance.GetType();
            EnsureLayerListAccessor(mainType, gameInterfaceLayerType);

            try
            {
                if (_layerListField != null)
                {
                    return _layerListField.GetValue(_layerListField.IsStatic ? null : mainInstance) as IList;
                }

                if (_layerListProperty != null)
                {
                    var getter = _layerListProperty.GetGetMethod(true);
                    return _layerListProperty.GetValue(getter != null && getter.IsStatic ? null : mainInstance, null) as IList;
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool IsLayerListType(Type type, string memberName, Type gameInterfaceLayerType)
        {
            if (type == null || !typeof(IList).IsAssignableFrom(type))
            {
                return false;
            }

            if (!type.IsGenericType)
            {
                return !string.IsNullOrEmpty(memberName) &&
                       memberName.IndexOf("Interface", StringComparison.OrdinalIgnoreCase) >= 0 &&
                       memberName.IndexOf("Layer", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return type.GetGenericArguments().Any(argument =>
                string.Equals(argument.FullName, gameInterfaceLayerType.FullName, StringComparison.Ordinal));
        }

        private static void InsertLayerIfMissing(
            IList layers,
            LayerSearchState layerState,
            string layerName,
            MethodInfo drawMethod,
            object scaleValue,
            ref int firstInsertLogged,
            string logMessage,
            int preferredInsertIndex)
        {
            if (layerState.Contains(layerName))
            {
                return;
            }

            if (drawMethod == null)
            {
                LogThrottle.WarnThrottled(
                    "interface-layer-draw-method-missing-" + layerName,
                    TimeSpan.FromSeconds(10),
                    "InterfaceLayerHookCallbacks",
                    "Draw method missing for layer: " + layerName);
                return;
            }

            try
            {
                var layer = CreateLegacyLayer(layerName, drawMethod, scaleValue);
                var insertIndex = preferredInsertIndex >= 0 ? preferredInsertIndex : layerState.MouseTextIndex;
                if (insertIndex >= 0 && insertIndex <= layers.Count)
                {
                    layers.Insert(insertIndex, layer);
                    layerState.MarkInserted(layerName, insertIndex);
                }
                else
                {
                    layers.Add(layer);
                    layerState.MarkInserted(layerName, layers.Count - 1);
                }

                if (Interlocked.Exchange(ref firstInsertLogged, 1) == 0)
                {
                    Logger.Info("InterfaceLayerHookCallbacks", logMessage);
                }
                else
                {
                    LogThrottle.InfoThrottled(
                        "interface-layer-inserted-" + layerName,
                        TimeSpan.FromSeconds(10),
                        "InterfaceLayerHookCallbacks",
                        logMessage);
                }
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "interface-layer-insert-failed-" + layerName,
                    TimeSpan.FromSeconds(10),
                    "InterfaceLayerHookCallbacks",
                    "Interface layer insert failed for " + layerName + ": " + error.Message);
            }
        }

        private static object CreateLegacyLayer(string layerName, MethodInfo drawMethod, object scaleValue)
        {
            var drawDelegate = Delegate.CreateDelegate(_drawDelegateType, drawMethod);
            if (_legacyLayerConstructor != null)
            {
                return _legacyLayerConstructor.Invoke(new object[] { layerName, drawDelegate, scaleValue });
            }

            return Activator.CreateInstance(_legacyLayerType, layerName, drawDelegate, scaleValue);
        }

        private static object ParseScaleValue(Type scaleType, string scaleName)
        {
            try
            {
                return Enum.Parse(scaleType, scaleName);
            }
            catch
            {
                return Enum.Parse(scaleType, "UI");
            }
        }

        private static bool EnsureLayerReflectionReady()
        {
            if (_gameInterfaceLayerType != null &&
                _legacyLayerType != null &&
                _scaleType != null &&
                _drawDelegateType != null &&
                _legacyLayerConstructor != null &&
                _uiScaleValue != null &&
                _gameScaleValue != null)
            {
                return true;
            }

            lock (ReflectionCacheSyncRoot)
            {
                if (_gameInterfaceLayerType == null)
                {
                    _gameInterfaceLayerType = FindType("Terraria.UI.GameInterfaceLayer");
                }

                if (_legacyLayerType == null)
                {
                    _legacyLayerType = FindType("Terraria.UI.LegacyGameInterfaceLayer");
                }

                if (_scaleType == null)
                {
                    _scaleType = FindType("Terraria.UI.InterfaceScaleType");
                }

                if (_drawDelegateType == null)
                {
                    _drawDelegateType = FindType("Terraria.UI.GameInterfaceDrawMethod");
                }

                if (_legacyLayerConstructor == null &&
                    _legacyLayerType != null &&
                    _drawDelegateType != null &&
                    _scaleType != null)
                {
                    _legacyLayerConstructor = _legacyLayerType
                        .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(constructor =>
                        {
                            var parameters = constructor.GetParameters();
                            return parameters.Length == 3 &&
                                   parameters[0].ParameterType == typeof(string) &&
                                   parameters[1].ParameterType.IsAssignableFrom(_drawDelegateType) &&
                                   parameters[2].ParameterType == _scaleType;
                        });
                }

                if (_uiScaleValue == null && _scaleType != null)
                {
                    _uiScaleValue = ParseScaleValue(_scaleType, "UI");
                }

                if (_gameScaleValue == null && _scaleType != null)
                {
                    _gameScaleValue = ParseScaleValue(_scaleType, "Game");
                }
            }

            return _gameInterfaceLayerType != null &&
                   _legacyLayerType != null &&
                   _scaleType != null &&
                   _drawDelegateType != null &&
                   _legacyLayerConstructor != null &&
                   _uiScaleValue != null &&
                   _gameScaleValue != null;
        }

        private static string GetLayerName(object layer)
        {
            if (layer == null)
            {
                return string.Empty;
            }

            var type = layer.GetType();
            var accessor = GetLayerNameAccessor(type);
            try
            {
                if (accessor.Property != null)
                {
                    return accessor.Property.GetValue(layer, null) as string ?? string.Empty;
                }

                if (accessor.Field != null)
                {
                    return accessor.Field.GetValue(layer) as string ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
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

        private static void EnsureLayerListAccessor(Type mainType, Type gameInterfaceLayerType)
        {
            if (_layerListAccessorResolved && _layerListOwnerType == mainType)
            {
                return;
            }

            lock (ReflectionCacheSyncRoot)
            {
                if (_layerListAccessorResolved && _layerListOwnerType == mainType)
                {
                    return;
                }

                _layerListOwnerType = mainType;
                _layerListField = null;
                _layerListProperty = null;
                _layerListAccessorResolved = true;

                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                foreach (var field in mainType.GetFields(flags))
                {
                    if (!IsLayerListType(field.FieldType, field.Name, gameInterfaceLayerType))
                    {
                        continue;
                    }

                    _layerListField = field;
                    return;
                }

                foreach (var property in mainType.GetProperties(flags))
                {
                    if (!property.CanRead ||
                        property.GetIndexParameters().Length != 0 ||
                        !IsLayerListType(property.PropertyType, property.Name, gameInterfaceLayerType))
                    {
                        continue;
                    }

                    _layerListProperty = property;
                    return;
                }
            }
        }

        private static LayerSearchState BuildLayerSearchState(IList layers)
        {
            var state = new LayerSearchState();
            if (layers == null)
            {
                return state;
            }

            for (var index = 0; index < layers.Count; index++)
            {
                state.ObserveExisting(GetLayerName(layers[index]), index);
            }

            return state;
        }

        private static LayerNameAccessor GetLayerNameAccessor(Type type)
        {
            lock (ReflectionCacheSyncRoot)
            {
                if (LayerNameAccessorCache.TryGetValue(type, out var accessor))
                {
                    return accessor;
                }

                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                accessor = new LayerNameAccessor
                {
                    Property = type.GetProperty("Name", flags),
                    Field = type.GetField("Name", flags) ?? type.GetField("_name", flags)
                };
                LayerNameAccessorCache[type] = accessor;
                return accessor;
            }
        }

        private sealed class LayerSearchState
        {
            private readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);

            public int MouseTextIndex { get; private set; } = -1;

            public bool Contains(string layerName)
            {
                return _names.Contains(layerName ?? string.Empty);
            }

            public void ObserveExisting(string layerName, int index)
            {
                var name = layerName ?? string.Empty;
                _names.Add(name);
                if (MouseTextIndex < 0 &&
                    name.IndexOf("Mouse Text", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MouseTextIndex = index;
                }
            }

            public void MarkInserted(string layerName, int index)
            {
                var name = layerName ?? string.Empty;
                _names.Add(name);
                if (MouseTextIndex >= 0 && index >= 0 && index <= MouseTextIndex)
                {
                    MouseTextIndex++;
                }
            }
        }

        private sealed class LayerNameAccessor
        {
            public PropertyInfo Property { get; set; }

            public FieldInfo Field { get; set; }
        }
    }
}
