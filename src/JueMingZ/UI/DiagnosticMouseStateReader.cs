using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class DiagnosticMouseStateReader
    {
        private const int VkLeftButton = 0x01;
        private static readonly object ReadCacheSyncRoot = new object();
        private static bool _mainResolved;
        private static DiagnosticMouseState _cachedReadState;
        private static DiagnosticMouseState _cachedFullscreenMapOverlayReadState;
        private static DiagnosticMouseState _cachedBlueprintHandheldActionBarOverlayReadState;
        private static UiInputFrameKey _cachedFrameKey;
        private static UiInputFrameKey _cachedFullscreenMapOverlayFrameKey;
        private static UiInputFrameKey _cachedBlueprintHandheldActionBarOverlayFrameKey;
        private static bool _cachedFrameKeyValid;
        private static bool _cachedFullscreenMapOverlayFrameKeyValid;
        private static bool _cachedBlueprintHandheldActionBarOverlayFrameKeyValid;
        private static FieldInfo _mouseXField;
        private static FieldInfo _mouseYField;
        private static FieldInfo _mouseLeftField;
        private static FieldInfo _mouseLeftReleaseField;
        private static FieldInfo _mouseScrollWheelField;
        private static FieldInfo _oldMouseScrollWheelField;
        private static FieldInfo _uiScaleField;
        private static FieldInfo _uiScaleMatrixField;
        private static PropertyInfo _mouseXProperty;
        private static PropertyInfo _mouseYProperty;
        private static PropertyInfo _mouseLeftProperty;
        private static PropertyInfo _mouseLeftReleaseProperty;
        private static PropertyInfo _mouseScrollWheelProperty;
        private static PropertyInfo _oldMouseScrollWheelProperty;
        private static PropertyInfo _uiScaleProperty;
        private static PropertyInfo _uiScaleMatrixProperty;

        public static DiagnosticMouseState Read()
        {
            return ReadCore(GateClosedMousePreserveMode.None);
        }

        internal static DiagnosticMouseState ReadForFullscreenMapOverlay()
        {
            return ReadCore(GateClosedMousePreserveMode.FullscreenMapOverlay);
        }

        internal static DiagnosticMouseState ReadForBlueprintHandheldActionBarOverlay()
        {
            return ReadCore(GateClosedMousePreserveMode.BlueprintHandheldActionBarOverlay);
        }

        private static DiagnosticMouseState ReadCore(GateClosedMousePreserveMode preserveMode)
        {
            var preserveTerrariaInputWhenGateClosed = preserveMode != GateClosedMousePreserveMode.None;
            var frameKey = UiInputFrameClock.CurrentFrameKey;
            lock (ReadCacheSyncRoot)
            {
                var cachedState = _cachedReadState;
                var cachedKey = _cachedFrameKey;
                var cachedKeyValid = _cachedFrameKeyValid;
                if (preserveMode == GateClosedMousePreserveMode.FullscreenMapOverlay)
                {
                    cachedState = _cachedFullscreenMapOverlayReadState;
                    cachedKey = _cachedFullscreenMapOverlayFrameKey;
                    cachedKeyValid = _cachedFullscreenMapOverlayFrameKeyValid;
                }
                else if (preserveMode == GateClosedMousePreserveMode.BlueprintHandheldActionBarOverlay)
                {
                    cachedState = _cachedBlueprintHandheldActionBarOverlayReadState;
                    cachedKey = _cachedBlueprintHandheldActionBarOverlayFrameKey;
                    cachedKeyValid = _cachedBlueprintHandheldActionBarOverlayFrameKeyValid;
                }

                if (frameKey.IsValid &&
                    cachedKeyValid &&
                    cachedState != null &&
                    cachedKey.Equals(frameKey))
                {
                    return cachedState;
                }
            }

            var state = new DiagnosticMouseState();
            state.GameInputAvailable = TerrariaMainCompat.AllowsInputProcessing;
            var errors = string.Empty;

            try
            {
                ReadTerrariaMouse(state);
            }
            catch (Exception error)
            {
                errors = AppendError(errors, "Terraria mouse read failed: " + error.Message);
            }

            try
            {
                ReadOsMouse(state);
            }
            catch (Exception error)
            {
                errors = AppendError(errors, "OS mouse read failed: " + error.Message);
            }

            if (!state.GameInputAvailable)
            {
                if (!preserveTerrariaInputWhenGateClosed)
                {
                    state.TerrariaLeftDown = false;
                    state.TerrariaLeftRelease = false;
                }

                state.OsLeftDown = false;
                if (!preserveTerrariaInputWhenGateClosed ||
                    !state.TerrariaScrollWheelAvailable)
                {
                    state.ScrollDelta = 0;
                }
            }

            state.LastError = errors;
            state.ReadMode = BuildReadMode(state);
            if (preserveTerrariaInputWhenGateClosed && !state.GameInputAvailable)
            {
                state.ReadMode += "/" + GateBypassReadModeSuffix(preserveMode);
            }

            if (!state.TerrariaReadAvailable &&
                !state.OsReadAvailable &&
                string.IsNullOrWhiteSpace(state.LastError))
            {
                state.LastError = "Mouse read unavailable: Terraria mouse fields and OS client mouse were not available.";
            }

            lock (ReadCacheSyncRoot)
            {
                if (frameKey.IsValid)
                {
                    if (preserveMode == GateClosedMousePreserveMode.FullscreenMapOverlay)
                    {
                        _cachedFullscreenMapOverlayReadState = state;
                        _cachedFullscreenMapOverlayFrameKey = frameKey;
                        _cachedFullscreenMapOverlayFrameKeyValid = true;
                    }
                    else if (preserveMode == GateClosedMousePreserveMode.BlueprintHandheldActionBarOverlay)
                    {
                        _cachedBlueprintHandheldActionBarOverlayReadState = state;
                        _cachedBlueprintHandheldActionBarOverlayFrameKey = frameKey;
                        _cachedBlueprintHandheldActionBarOverlayFrameKeyValid = true;
                    }
                    else
                    {
                        _cachedReadState = state;
                        _cachedFrameKey = frameKey;
                        _cachedFrameKeyValid = true;
                    }
                }
                else
                {
                    if (preserveMode == GateClosedMousePreserveMode.FullscreenMapOverlay)
                    {
                        _cachedFullscreenMapOverlayReadState = null;
                        _cachedFullscreenMapOverlayFrameKey = UiInputFrameKey.None;
                        _cachedFullscreenMapOverlayFrameKeyValid = false;
                    }
                    else if (preserveMode == GateClosedMousePreserveMode.BlueprintHandheldActionBarOverlay)
                    {
                        _cachedBlueprintHandheldActionBarOverlayReadState = null;
                        _cachedBlueprintHandheldActionBarOverlayFrameKey = UiInputFrameKey.None;
                        _cachedBlueprintHandheldActionBarOverlayFrameKeyValid = false;
                    }
                    else
                    {
                        _cachedReadState = null;
                        _cachedFrameKey = UiInputFrameKey.None;
                        _cachedFrameKeyValid = false;
                    }
                }
            }

            return state;
        }

        internal static void ResetForTesting()
        {
            lock (ReadCacheSyncRoot)
            {
                _cachedReadState = null;
                _cachedFullscreenMapOverlayReadState = null;
                _cachedBlueprintHandheldActionBarOverlayReadState = null;
                _cachedFrameKey = UiInputFrameKey.None;
                _cachedFullscreenMapOverlayFrameKey = UiInputFrameKey.None;
                _cachedBlueprintHandheldActionBarOverlayFrameKey = UiInputFrameKey.None;
                _cachedFrameKeyValid = false;
                _cachedFullscreenMapOverlayFrameKeyValid = false;
                _cachedBlueprintHandheldActionBarOverlayFrameKeyValid = false;
                _mainResolved = false;
                _mouseXField = null;
                _mouseYField = null;
                _mouseLeftField = null;
                _mouseLeftReleaseField = null;
                _mouseScrollWheelField = null;
                _oldMouseScrollWheelField = null;
                _uiScaleField = null;
                _uiScaleMatrixField = null;
                _mouseXProperty = null;
                _mouseYProperty = null;
                _mouseLeftProperty = null;
                _mouseLeftReleaseProperty = null;
                _mouseScrollWheelProperty = null;
                _oldMouseScrollWheelProperty = null;
                _uiScaleProperty = null;
                _uiScaleMatrixProperty = null;
            }
        }

        private static void ReadTerrariaMouse(DiagnosticMouseState state)
        {
            EnsureMainAccessors();
            if (!_mainResolved || (_mouseXField == null && _mouseXProperty == null) || (_mouseYField == null && _mouseYProperty == null))
            {
                return;
            }

            state.TerrariaMouseX = ReadInt(_mouseXField, _mouseXProperty, -1);
            state.TerrariaMouseY = ReadInt(_mouseYField, _mouseYProperty, -1);
            state.TerrariaLeftDown = ReadBool(_mouseLeftField, _mouseLeftProperty, false);
            state.TerrariaLeftReleaseAvailable = _mouseLeftReleaseField != null || _mouseLeftReleaseProperty != null;
            state.TerrariaLeftRelease = ReadBool(_mouseLeftReleaseField, _mouseLeftReleaseProperty, false);
            state.TerrariaScrollWheelAvailable =
                (_mouseScrollWheelField != null || _mouseScrollWheelProperty != null) &&
                (_oldMouseScrollWheelField != null || _oldMouseScrollWheelProperty != null);
            if (state.TerrariaScrollWheelAvailable)
            {
                state.TerrariaScrollWheel = ReadInt(_mouseScrollWheelField, _mouseScrollWheelProperty, 0);
                state.TerrariaOldScrollWheel = ReadInt(_oldMouseScrollWheelField, _oldMouseScrollWheelProperty, state.TerrariaScrollWheel);
                state.ScrollDelta = state.TerrariaScrollWheel - state.TerrariaOldScrollWheel;
            }

            state.UiScaleMatrixAvailable = _uiScaleMatrixField != null || _uiScaleMatrixProperty != null;
            ReadUiScale(state);
            state.TerrariaReadAvailable = true;
        }

        private static void ReadOsMouse(DiagnosticMouseState state)
        {
            var window = ResolveWindowHandle();
            if (window == IntPtr.Zero)
            {
                state.OsLeftDown = IsLeftButtonDown();
                return;
            }

            POINT point;
            if (!GetCursorPos(out point))
            {
                state.OsLeftDown = IsLeftButtonDown();
                return;
            }

            if (!ScreenToClient(window, ref point))
            {
                state.OsLeftDown = IsLeftButtonDown();
                return;
            }

            state.OsClientMouseX = point.X;
            state.OsClientMouseY = point.Y;
            state.OsLeftDown = IsLeftButtonDown();
            state.OsReadAvailable = true;
        }

        private static void EnsureMainAccessors()
        {
            if (_mainResolved)
            {
                return;
            }

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                return;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return;
            }

            _mainResolved = true;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            _mouseXField = mainType.GetField("mouseX", flags);
            _mouseYField = mainType.GetField("mouseY", flags);
            _mouseLeftField = mainType.GetField("mouseLeft", flags);
            _mouseLeftReleaseField = mainType.GetField("mouseLeftRelease", flags);
            _mouseScrollWheelField = mainType.GetField("mouseScrollWheel", flags);
            _oldMouseScrollWheelField = mainType.GetField("oldMouseScrollWheel", flags);
            _uiScaleField = mainType.GetField("_uiScaleUsed", flags) ??
                            mainType.GetField("UIScale", flags) ??
                            mainType.GetField("_uiScaleWanted", flags) ??
                            mainType.GetField("temporaryGUIScaleSlider", flags);
            _uiScaleMatrixField = mainType.GetField("UIScaleMatrix", flags) ??
                                  mainType.GetField("_uiScaleMatrix", flags);
            _mouseXProperty = _mouseXField == null ? mainType.GetProperty("mouseX", flags) : null;
            _mouseYProperty = _mouseYField == null ? mainType.GetProperty("mouseY", flags) : null;
            _mouseLeftProperty = _mouseLeftField == null ? mainType.GetProperty("mouseLeft", flags) : null;
            _mouseLeftReleaseProperty = _mouseLeftReleaseField == null ? mainType.GetProperty("mouseLeftRelease", flags) : null;
            _mouseScrollWheelProperty = _mouseScrollWheelField == null ? mainType.GetProperty("mouseScrollWheel", flags) : null;
            _oldMouseScrollWheelProperty = _oldMouseScrollWheelField == null ? mainType.GetProperty("oldMouseScrollWheel", flags) : null;
            _uiScaleProperty = _uiScaleField == null ? mainType.GetProperty("UIScale", flags) : null;
            _uiScaleMatrixProperty = _uiScaleMatrixField == null ? mainType.GetProperty("UIScaleMatrix", flags) : null;
        }

        private static void ReadUiScale(DiagnosticMouseState state)
        {
            state.UiScale = 1d;
            state.UiScaleX = 1d;
            state.UiScaleY = 1d;
            state.UiTranslateX = 0d;
            state.UiTranslateY = 0d;
            state.UiScaleSource = string.Empty;

            if (state.UiScaleMatrixAvailable)
            {
                var matrix = ReadRaw(_uiScaleMatrixField, _uiScaleMatrixProperty);
                var scaleX = ReadNamedDouble(matrix, "M11", 0d);
                var scaleY = ReadNamedDouble(matrix, "M22", 0d);
                if (scaleX > 0.01d && scaleY > 0.01d)
                {
                    state.UiScaleX = scaleX;
                    state.UiScaleY = scaleY;
                    state.UiScale = Math.Abs(scaleX - scaleY) <= 0.01d ? scaleX : (scaleX + scaleY) / 2d;
                    state.UiTranslateX = ReadNamedDouble(matrix, "M41", 0d);
                    state.UiTranslateY = ReadNamedDouble(matrix, "M42", 0d);
                    state.UiScaleAvailable = true;
                    state.UiScaleSource = "UIScaleMatrix";
                    return;
                }
            }

            state.UiScaleAvailable = _uiScaleField != null || _uiScaleProperty != null;
            if (state.UiScaleAvailable)
            {
                state.UiScale = ReadDouble(_uiScaleField, _uiScaleProperty, 1d);
                if (state.UiScale <= 0.01d)
                {
                    state.UiScale = 1d;
                }

                state.UiScaleX = state.UiScale;
                state.UiScaleY = state.UiScale;
                state.UiScaleSource = "_uiScaleUsed";
            }
        }

        private static double ReadNamedDouble(object instance, string name, double fallback)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            try
            {
                var type = instance.GetType();
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    return Convert.ToDouble(field.GetValue(instance));
                }

                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanRead)
                {
                    return Convert.ToDouble(property.GetValue(instance, null));
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static object ReadRaw(FieldInfo field, PropertyInfo property)
        {
            if (field != null)
            {
                return field.GetValue(null);
            }

            return property != null && property.CanRead ? property.GetValue(null, null) : null;
        }

        private static int ReadInt(FieldInfo field, PropertyInfo property, int fallback)
        {
            var raw = ReadRaw(field, property);
            if (raw == null)
            {
                return fallback;
            }

            try { return Convert.ToInt32(raw); }
            catch { return fallback; }
        }

        private static bool ReadBool(FieldInfo field, PropertyInfo property, bool fallback)
        {
            var raw = ReadRaw(field, property);
            if (raw == null)
            {
                return fallback;
            }

            try { return Convert.ToBoolean(raw); }
            catch { return fallback; }
        }

        private static double ReadDouble(FieldInfo field, PropertyInfo property, double fallback)
        {
            var raw = ReadRaw(field, property);
            if (raw == null)
            {
                return fallback;
            }

            try { return Convert.ToDouble(raw); }
            catch { return fallback; }
        }

        private static IntPtr ResolveWindowHandle()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }

                var foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                int processId;
                GetWindowThreadProcessId(foreground, out processId);
                return processId == process.Id ? foreground : IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static bool IsLeftButtonDown()
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            return (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
        }

        private static string BuildReadMode(DiagnosticMouseState state)
        {
            if (state.TerrariaReadAvailable && state.OsReadAvailable)
            {
                return "Terraria+OsClient";
            }

            if (state.TerrariaReadAvailable)
            {
                return "TerrariaOnly";
            }

            if (state.OsReadAvailable)
            {
                return "OsClientOnly";
            }

            return "none";
        }

        private static string GateBypassReadModeSuffix(GateClosedMousePreserveMode preserveMode)
        {
            if (preserveMode == GateClosedMousePreserveMode.FullscreenMapOverlay)
            {
                return "FullscreenOverlayGateBypass";
            }

            if (preserveMode == GateClosedMousePreserveMode.BlueprintHandheldActionBarOverlay)
            {
                return "BlueprintHandheldOverlayGateBypass";
            }

            return "OverlayGateBypass";
        }

        private static string AppendError(string existing, string next)
        {
            if (string.IsNullOrWhiteSpace(next))
            {
                return existing ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(existing) ? next : existing + " | " + next;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr windowHandle, out int processId);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        private enum GateClosedMousePreserveMode
        {
            None,
            FullscreenMapOverlay,
            BlueprintHandheldActionBarOverlay
        }
    }
}
