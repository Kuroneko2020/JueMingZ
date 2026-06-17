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
        // Interface-layer injection may add draw dispatchers only. It must preserve
        // Terraria SpriteBatch phase and leave input or game-state mutation elsewhere.
        private const string LegacyInputGuardLayerName = "JueMing-Z: Legacy Main UI Input Guard";
        private const string InformationWorldUnderVanillaUiDispatcherLayerName = "JueMing-Z: Information World Under Vanilla UI Dispatcher";
        private const string InformationStatusPanelUnderVanillaUiDispatcherLayerName = "JueMing-Z: Information Status Panel Under Vanilla UI Dispatcher";
        private const string GameOverlayDispatcherLayerName = "JueMing-Z: Game Overlay Dispatcher";
        private const string UiOverlayDispatcherLayerName = "JueMing-Z: UI Overlay Dispatcher";
        private const string LegacyFinalMouseTextGuardLayerName = "JueMing-Z: Legacy Final MouseText Guard";

        private static readonly object ReflectionCacheSyncRoot = new object();
        private static readonly Dictionary<Type, LayerNameAccessor> LayerNameAccessorCache = new Dictionary<Type, LayerNameAccessor>();
        private static readonly Func<bool>[] InformationWorldUnderVanillaUiDispatcherDrawers =
        {
            InformationWorldOverlay.DrawInformationInterfaceLayer,
            SearchChestLocatorWorldOverlay.DrawInterfaceLayer
        };

        private static readonly Func<bool>[] InformationStatusPanelUnderVanillaUiDispatcherDrawers =
        {
            InformationStatusPanelOverlay.DrawInterfaceLayer
        };

        private static readonly Func<bool>[] GameOverlayDispatcherDrawers =
        {
            InformationWorldOverlay.DrawAutoMiningInterfaceLayer,
            FishingStatusPromptOverlay.DrawInterfaceLayer,
            FirstWorldLoadPromptOverlay.DrawInterfaceLayer,
            CombatEquipmentWarningPromptOverlay.DrawInterfaceLayer,
            CombatAimMarkerOverlay.DrawInterfaceLayer,
            MapDirectionHintOverlay.DrawInterfaceLayer
        };

        private static readonly Func<bool>[] GameOverlayFallbackDispatcherDrawers =
        {
            InformationWorldOverlay.DrawInformationInterfaceLayer,
            SearchChestLocatorWorldOverlay.DrawInterfaceLayer,
            InformationWorldOverlay.DrawAutoMiningInterfaceLayer,
            FishingStatusPromptOverlay.DrawInterfaceLayer,
            FirstWorldLoadPromptOverlay.DrawInterfaceLayer,
            CombatEquipmentWarningPromptOverlay.DrawInterfaceLayer,
            CombatAimMarkerOverlay.DrawInterfaceLayer,
            MapDirectionHintOverlay.DrawInterfaceLayer
        };

        private static readonly Func<bool>[] UiOverlayDispatcherDrawers =
        {
            LegacyMainWindow.DrawInterfaceLayer,
            // The map marker style picker uses screen/UI coordinates; keeping it
            // in the UI-scale dispatcher prevents fullscreen map clicks from
            // being transformed by the Game/world matrix.
            MapCustomMarkerStylePickerOverlay.DrawInterfaceLayer
        };

        private static readonly Func<bool>[] UiOverlayFallbackDispatcherDrawers =
        {
            InformationStatusPanelOverlay.DrawInterfaceLayer,
            LegacyMainWindow.DrawInterfaceLayer,
            MapCustomMarkerStylePickerOverlay.DrawInterfaceLayer
        };

        private static int _firstLegacyInputGuardInsertLogged;
        private static int _firstInformationWorldUnderVanillaUiDispatcherInsertLogged;
        private static int _firstInformationStatusPanelUnderVanillaUiDispatcherInsertLogged;
        private static int _firstGameOverlayDispatcherInsertLogged;
        private static int _firstUiOverlayDispatcherInsertLogged;
        private static int _firstLegacyFinalMouseTextGuardInsertLogged;
        private static int _informationWorldUnderVanillaUiDispatcherActive;
        private static int _informationStatusPanelUnderVanillaUiDispatcherActive;

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
            var informationWorldUnderUiLayerActive = layerState.Contains(InformationWorldUnderVanillaUiDispatcherLayerName);
            var informationStatusPanelUnderUiLayerActive = layerState.Contains(InformationStatusPanelUnderVanillaUiDispatcherLayerName);

            InsertLayerIfMissing(
                layers,
                layerState,
                LegacyInputGuardLayerName,
                typeof(LegacyMainWindow).GetMethod("DrawInputGuardLayer", BindingFlags.Public | BindingFlags.Static),
                _uiScaleValue,
                ref _firstLegacyInputGuardInsertLogged,
                "Legacy main UI input guard layer inserted.",
                0);

            var informationUnderUiInsertIndex = layerState.InformationUnderVanillaUiIndex;
            if (informationUnderUiInsertIndex >= 0)
            {
                InsertLayerIfMissing(
                    layers,
                    layerState,
                    InformationWorldUnderVanillaUiDispatcherLayerName,
                    typeof(InterfaceLayerHookCallbacks).GetMethod("DrawInformationWorldUnderVanillaUiDispatcherLayer", BindingFlags.Public | BindingFlags.Static),
                    _gameScaleValue,
                    ref _firstInformationWorldUnderVanillaUiDispatcherInsertLogged,
                    "Information world under vanilla UI dispatcher interface layer inserted.",
                    informationUnderUiInsertIndex);
                informationWorldUnderUiLayerActive = layerState.Contains(InformationWorldUnderVanillaUiDispatcherLayerName);

                InsertLayerIfMissing(
                    layers,
                    layerState,
                    InformationStatusPanelUnderVanillaUiDispatcherLayerName,
                    typeof(InterfaceLayerHookCallbacks).GetMethod("DrawInformationStatusPanelUnderVanillaUiDispatcherLayer", BindingFlags.Public | BindingFlags.Static),
                    _uiScaleValue,
                    ref _firstInformationStatusPanelUnderVanillaUiDispatcherInsertLogged,
                    "Information status panel under vanilla UI dispatcher interface layer inserted.",
                    layerState.InformationUnderVanillaUiIndex);
                informationStatusPanelUnderUiLayerActive = layerState.Contains(InformationStatusPanelUnderVanillaUiDispatcherLayerName);
            }
            else
            {
                LogThrottle.WarnThrottled(
                    "interface-layer-information-under-ui-anchor-missing",
                    TimeSpan.FromSeconds(10),
                    "InterfaceLayerHookCallbacks",
                    "Vanilla UI anchor was not found; information under vanilla UI dispatcher layers not inserted.");
            }

            Interlocked.Exchange(ref _informationWorldUnderVanillaUiDispatcherActive, informationWorldUnderUiLayerActive ? 1 : 0);
            Interlocked.Exchange(ref _informationStatusPanelUnderVanillaUiDispatcherActive, informationStatusPanelUnderUiLayerActive ? 1 : 0);

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

            // This guard is intentionally late: vanilla Mouse Over has already
            // populated pending MouseText/NPC hover caches, but final UI text is
            // still ahead of us.
            InsertLayerIfMissing(
                layers,
                layerState,
                LegacyFinalMouseTextGuardLayerName,
                typeof(LegacyMainWindow).GetMethod("DrawMouseTextGuardLayer", BindingFlags.Public | BindingFlags.Static),
                _uiScaleValue,
                ref _firstLegacyFinalMouseTextGuardInsertLogged,
                "Legacy main UI final mouse text guard layer inserted.",
                layerState.FinalMouseTextGuardIndex);
        }

        public static bool DrawInformationWorldUnderVanillaUiDispatcherLayer()
        {
            return DrawDispatcher(InformationWorldUnderVanillaUiDispatcherDrawers);
        }

        public static bool DrawInformationStatusPanelUnderVanillaUiDispatcherLayer()
        {
            UiInputFrameClock.BeginDrawFrame("InformationStatusPanelUnderVanillaUiDispatcher");
            return DrawDispatcher(InformationStatusPanelUnderVanillaUiDispatcherDrawers);
        }

        public static bool DrawGameOverlayDispatcherLayer()
        {
            UiInputFrameClock.BeginDrawFrame("GameOverlayDispatcher");
            return DrawDispatcher(SelectGameOverlayDispatcherDrawers());
        }

        public static bool DrawUiOverlayDispatcherLayer()
        {
            UiInputFrameClock.BeginDrawFrame("UiOverlayDispatcher");
            return DrawDispatcher(SelectUiOverlayDispatcherDrawers());
        }

        private static Func<bool>[] SelectGameOverlayDispatcherDrawers()
        {
            return Volatile.Read(ref _informationWorldUnderVanillaUiDispatcherActive) == 0
                ? GameOverlayFallbackDispatcherDrawers
                : GameOverlayDispatcherDrawers;
        }

        private static Func<bool>[] SelectUiOverlayDispatcherDrawers()
        {
            return Volatile.Read(ref _informationStatusPanelUnderVanillaUiDispatcherActive) == 0
                ? UiOverlayFallbackDispatcherDrawers
                : UiOverlayDispatcherDrawers;
        }

        private static bool DrawDispatcher(Func<bool>[] drawers)
        {
            var keepDrawing = true;
            if (drawers == null)
            {
                return keepDrawing;
            }

            for (var index = 0; index < drawers.Length; index++)
            {
                var drawer = drawers[index];
                if (drawer != null)
                {
                    keepDrawing &= drawer();
                }
            }

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

        internal static int FindInformationUnderVanillaUiInsertIndexForTesting(IList<string> layerNames)
        {
            return BuildLayerSearchStateForTesting(layerNames).InformationUnderVanillaUiIndex;
        }

        internal static int FindFinalMouseTextGuardInsertIndexForTesting(IList<string> layerNames)
        {
            return BuildLayerSearchStateForTesting(layerNames).FinalMouseTextGuardIndex;
        }

        internal static int[] SimulateInformationUnderVanillaUiInsertIndicesForTesting(IList<string> layerNames)
        {
            var state = BuildLayerSearchStateForTesting(layerNames);
            var worldInsertIndex = state.InformationUnderVanillaUiIndex;
            state.MarkInserted(InformationWorldUnderVanillaUiDispatcherLayerName, worldInsertIndex);

            var statusPanelInsertIndex = state.InformationUnderVanillaUiIndex;
            state.MarkInserted(InformationStatusPanelUnderVanillaUiDispatcherLayerName, statusPanelInsertIndex);

            return new[] { worldInsertIndex, statusPanelInsertIndex };
        }

        internal static string[] GetInformationWorldUnderVanillaUiDispatcherRouteNamesForTesting()
        {
            return GetDispatcherRouteNamesForTesting(InformationWorldUnderVanillaUiDispatcherDrawers);
        }

        internal static string[] GetInformationStatusPanelUnderVanillaUiDispatcherRouteNamesForTesting()
        {
            return GetDispatcherRouteNamesForTesting(InformationStatusPanelUnderVanillaUiDispatcherDrawers);
        }

        internal static string[] GetGameOverlayDispatcherRouteNamesForTesting(bool informationWorldUnderVanillaUiDispatcherActive)
        {
            return GetDispatcherRouteNamesForTesting(
                informationWorldUnderVanillaUiDispatcherActive
                    ? GameOverlayDispatcherDrawers
                    : GameOverlayFallbackDispatcherDrawers);
        }

        internal static string[] GetUiOverlayDispatcherRouteNamesForTesting(bool informationStatusPanelUnderVanillaUiDispatcherActive)
        {
            return GetDispatcherRouteNamesForTesting(
                informationStatusPanelUnderVanillaUiDispatcherActive
                    ? UiOverlayDispatcherDrawers
                    : UiOverlayFallbackDispatcherDrawers);
        }

        private static string[] GetDispatcherRouteNamesForTesting(Func<bool>[] drawers)
        {
            if (drawers == null)
            {
                return new string[0];
            }

            var routeNames = new string[drawers.Length];
            for (var index = 0; index < drawers.Length; index++)
            {
                var method = drawers[index] == null ? null : drawers[index].Method;
                var declaringType = method == null ? null : method.DeclaringType;
                routeNames[index] = (declaringType == null ? string.Empty : declaringType.Name) + "." + (method == null ? string.Empty : method.Name);
            }

            return routeNames;
        }

        private static LayerSearchState BuildLayerSearchStateForTesting(IList<string> layerNames)
        {
            var state = new LayerSearchState();
            if (layerNames == null)
            {
                return state;
            }

            for (var index = 0; index < layerNames.Count; index++)
            {
                state.ObserveExisting(layerNames[index], index);
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

            public int MouseOverIndex { get; private set; } = -1;

            public int InteractItemIconIndex { get; private set; } = -1;

            public int MapMinimapIndex { get; private set; } = -1;

            public int ResourceBarsIndex { get; private set; } = -1;

            public int InventoryIndex { get; private set; } = -1;

            public int InformationUnderVanillaUiIndex
            {
                get
                {
                    if (MapMinimapIndex >= 0)
                    {
                        return MapMinimapIndex;
                    }

                    if (ResourceBarsIndex >= 0)
                    {
                        return ResourceBarsIndex;
                    }

                    if (InventoryIndex >= 0)
                    {
                        return InventoryIndex;
                    }

                    return MouseTextIndex;
                }
            }

            public int FinalMouseTextGuardIndex
            {
                get
                {
                    if (InteractItemIconIndex >= 0)
                    {
                        return InteractItemIconIndex;
                    }

                    if (MouseOverIndex >= 0)
                    {
                        return MouseOverIndex + 1;
                    }

                    return MouseTextIndex;
                }
            }

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

                if (MouseOverIndex < 0 &&
                    name.IndexOf("Mouse Over", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MouseOverIndex = index;
                }

                if (InteractItemIconIndex < 0 &&
                    name.IndexOf("Interact Item Icon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    InteractItemIconIndex = index;
                }

                if (MapMinimapIndex < 0 &&
                    name.IndexOf("Map / Minimap", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MapMinimapIndex = index;
                }

                if (ResourceBarsIndex < 0 &&
                    name.IndexOf("Resource Bars", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ResourceBarsIndex = index;
                }

                if (InventoryIndex < 0 &&
                    name.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    InventoryIndex = index;
                }
            }

            public void MarkInserted(string layerName, int index)
            {
                var name = layerName ?? string.Empty;
                _names.Add(name);
                MouseTextIndex = ShiftIndexAfterInsert(MouseTextIndex, index);
                MouseOverIndex = ShiftIndexAfterInsert(MouseOverIndex, index);
                InteractItemIconIndex = ShiftIndexAfterInsert(InteractItemIconIndex, index);
                MapMinimapIndex = ShiftIndexAfterInsert(MapMinimapIndex, index);
                ResourceBarsIndex = ShiftIndexAfterInsert(ResourceBarsIndex, index);
                InventoryIndex = ShiftIndexAfterInsert(InventoryIndex, index);
            }

            private static int ShiftIndexAfterInsert(int currentIndex, int insertIndex)
            {
                if (currentIndex >= 0 && insertIndex >= 0 && insertIndex <= currentIndex)
                {
                    return currentIndex + 1;
                }

                return currentIndex;
            }
        }

        private sealed class LayerNameAccessor
        {
            public PropertyInfo Property { get; set; }

            public FieldInfo Field { get; set; }
        }
    }
}
