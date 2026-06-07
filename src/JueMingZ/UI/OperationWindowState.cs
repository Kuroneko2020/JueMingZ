using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class OperationWindowState
    {
        public const int DefaultX = 420;
        public const int DefaultY = 120;
        public const int DefaultWidth = 520;
        public const int DefaultHeight = 420;
        public const int MinWidth = 360;
        public const int MinHeight = 260;
        public const int TitleHeight = 30;
        public const int ResizeGripSize = 18;

        private static readonly object SyncRoot = new object();
        private static bool _loaded;
        private static int _x = DefaultX;
        private static int _y = DefaultY;
        private static int _width = DefaultWidth;
        private static int _height = DefaultHeight;
        private static bool _dragging;
        private static bool _resizing;
        private static bool _wasLeftDown;
        private static int _dragOffsetX;
        private static int _dragOffsetY;
        private static int _resizeStartMouseX;
        private static int _resizeStartMouseY;
        private static int _resizeStartWidth;
        private static int _resizeStartHeight;
        private static int _startX;
        private static int _startY;
        private static int _startWidth;
        private static int _startHeight;

        public static int X { get { EnsureLoaded(); lock (SyncRoot) { return _x; } } }
        public static int Y { get { EnsureLoaded(); lock (SyncRoot) { return _y; } } }
        public static int Width { get { EnsureLoaded(); lock (SyncRoot) { return _width; } } }
        public static int Height { get { EnsureLoaded(); lock (SyncRoot) { return _height; } } }
        public static bool Dragging { get { lock (SyncRoot) { return _dragging; } } }
        public static bool Resizing { get { lock (SyncRoot) { return _resizing; } } }

        public static void EnsureLoaded()
        {
            lock (SyncRoot)
            {
                if (_loaded)
                {
                    return;
                }

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                _x = Clamp(settings.OperationWindowX <= 0 ? DefaultX : settings.OperationWindowX, 0, 4096);
                _y = Clamp(settings.OperationWindowY <= 0 ? DefaultY : settings.OperationWindowY, 0, 4096);
                _width = Clamp(settings.OperationWindowWidth <= 0 ? DefaultWidth : settings.OperationWindowWidth, MinWidth, 1600);
                _height = Clamp(settings.OperationWindowHeight <= 0 ? DefaultHeight : settings.OperationWindowHeight, MinHeight, 1200);
                ClampToSafeBoundsLocked();
                _loaded = true;
            }
        }

        public static OperationWindowInteraction UpdateMouse(DiagnosticMouseState mouse)
        {
            // Drag/resize changes are clamped and saved on release; capture state here
            // only protects UI interaction, not gameplay execution.
            EnsureLoaded();
            var result = new OperationWindowInteraction();
            if (mouse == null)
            {
                return result;
            }

            var x = ChooseCoordinate(mouse.TerrariaReadAvailable, mouse.TerrariaMouseX, mouse.OsClientMouseX);
            var y = ChooseCoordinate(mouse.TerrariaReadAvailable, mouse.TerrariaMouseY, mouse.OsClientMouseY);
            var leftDown = mouse.TerrariaLeftDown || mouse.OsLeftDown;
            var released = !leftDown && _wasLeftDown;

            lock (SyncRoot)
            {
                result.MouseX = x;
                result.MouseY = y;
                result.InWindow = ContainsLocked(x, y);
                result.InTitle = ContainsTitleLocked(x, y);
                result.InResizeGrip = ContainsResizeGripLocked(x, y);

                if (leftDown && !_wasLeftDown)
                {
                    if (result.InResizeGrip)
                    {
                        BeginResizeLocked(x, y);
                        result.StartedResize = true;
                    }
                    else if (result.InTitle)
                    {
                        BeginDragLocked(x, y);
                        result.StartedDrag = true;
                    }
                }

                if (leftDown && _dragging)
                {
                    _x = Clamp(x - _dragOffsetX, 0, 4096);
                    _y = Clamp(y - _dragOffsetY, 0, 4096);
                    ClampToSafeBoundsLocked();
                    result.Dragging = true;
                    result.InWindow = true;
                }

                if (leftDown && _resizing)
                {
                    _width = Clamp(_resizeStartWidth + (x - _resizeStartMouseX), MinWidth, 1600);
                    _height = Clamp(_resizeStartHeight + (y - _resizeStartMouseY), MinHeight, 1200);
                    ClampToSafeBoundsLocked();
                    result.Resizing = true;
                    result.InWindow = true;
                }

                if (released && (_dragging || _resizing))
                {
                    var scenario = _dragging ? "Ui.OperationWindow.Move" : "Ui.OperationWindow.Resize";
                    var uiElement = _dragging ? "operation-window-title" : "operation-window-resize-grip";
                    var message = _dragging ? "操作窗口位置已更新。" : "操作窗口尺寸已更新。";
                    var wasDragging = _dragging;
                    var wasResizing = _resizing;
                    _dragging = false;
                    _resizing = false;
                    SaveLocked();
                    RecordWindowEvent(scenario, uiElement, message, wasDragging, wasResizing);
                    result.EndedInteraction = true;
                }

                _wasLeftDown = leftDown;
                result.Dragging = result.Dragging || _dragging;
                result.Resizing = result.Resizing || _resizing;
                result.CapturesMouse = result.InWindow || _dragging || _resizing;
                result.UiElementId = result.InResizeGrip || _resizing
                    ? "operation-window-resize-grip"
                    : (result.InTitle || _dragging ? "operation-window-title" : (result.InWindow ? "operation-window-content" : string.Empty));
            }

            return result;
        }

        public static bool ContainsPoint(int x, int y)
        {
            EnsureLoaded();
            lock (SyncRoot)
            {
                return ContainsLocked(x, y);
            }
        }

        public static string BuildUiStateJson()
        {
            EnsureLoaded();
            lock (SyncRoot)
            {
                return "{" +
                       "\"diagnosticsUiVisible\":true," +
                       "\"infoHudVisible\":true," +
                       "\"operationWindowVisible\":true," +
                       "\"operationWindowX\":" + IntRaw(_x) + "," +
                       "\"operationWindowY\":" + IntRaw(_y) + "," +
                       "\"operationWindowWidth\":" + IntRaw(_width) + "," +
                       "\"operationWindowHeight\":" + IntRaw(_height) + "," +
                       "\"operationWindowDragging\":" + BoolRaw(_dragging) + "," +
                       "\"operationWindowResizing\":" + BoolRaw(_resizing) +
                       "}";
            }
        }

        private static void BeginDragLocked(int mouseX, int mouseY)
        {
            _dragging = true;
            _resizing = false;
            _dragOffsetX = mouseX - _x;
            _dragOffsetY = mouseY - _y;
            CaptureStartLocked();
        }

        private static void BeginResizeLocked(int mouseX, int mouseY)
        {
            _resizing = true;
            _dragging = false;
            _resizeStartMouseX = mouseX;
            _resizeStartMouseY = mouseY;
            _resizeStartWidth = _width;
            _resizeStartHeight = _height;
            CaptureStartLocked();
        }

        private static void CaptureStartLocked()
        {
            _startX = _x;
            _startY = _y;
            _startWidth = _width;
            _startHeight = _height;
        }

        private static bool ContainsLocked(int mouseX, int mouseY)
        {
            return mouseX >= _x && mouseY >= _y && mouseX < _x + _width && mouseY < _y + _height;
        }

        private static bool ContainsTitleLocked(int mouseX, int mouseY)
        {
            return mouseX >= _x && mouseY >= _y && mouseX < _x + _width && mouseY < _y + TitleHeight;
        }

        private static bool ContainsResizeGripLocked(int mouseX, int mouseY)
        {
            return mouseX >= _x + _width - ResizeGripSize &&
                   mouseY >= _y + _height - ResizeGripSize &&
                   mouseX < _x + _width &&
                   mouseY < _y + _height;
        }

        private static void ClampToSafeBoundsLocked()
        {
            if (_x > 4096)
            {
                _x = DefaultX;
            }

            if (_y > 4096)
            {
                _y = DefaultY;
            }
        }

        private static void SaveLocked()
        {
            var settings = ConfigService.AppSettings;
            if (settings == null)
            {
                return;
            }

            settings.OperationWindowX = _x;
            settings.OperationWindowY = _y;
            settings.OperationWindowWidth = _width;
            settings.OperationWindowHeight = _height;
            ConfigService.SaveAll();
        }

        private static void RecordWindowEvent(string scenario, string uiElementId, string message, bool wasDragging, bool wasResizing)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                scenario,
                "Ui",
                string.Empty,
                "Succeeded",
                DiagnosticResultCode.Succeeded.ToString(),
                message,
                0,
                "{" +
                    "\"uiWindow\":\"OperationWindow\"," +
                    "\"uiElementId\":\"" + EscapeJson(uiElementId) + "\"," +
                    "\"x\":" + IntRaw(_startX) + "," +
                    "\"y\":" + IntRaw(_startY) + "," +
                    "\"width\":" + IntRaw(_startWidth) + "," +
                    "\"height\":" + IntRaw(_startHeight) +
                "}",
                "{" +
                    "\"uiWindow\":\"OperationWindow\"," +
                    "\"uiElementId\":\"" + EscapeJson(uiElementId) + "\"," +
                    "\"x\":" + IntRaw(_x) + "," +
                    "\"y\":" + IntRaw(_y) + "," +
                    "\"width\":" + IntRaw(_width) + "," +
                    "\"height\":" + IntRaw(_height) +
                "}",
                "{" +
                    "\"mouseCaptured\":true," +
                    "\"operationWindowDragging\":" + BoolRaw(wasDragging) + "," +
                    "\"operationWindowResizing\":" + BoolRaw(wasResizing) +
                "}",
                "Ui",
                "F5OperationWindow",
                string.Empty,
                string.Empty);
        }

        private static int ChooseCoordinate(bool terrariaAvailable, int terrariaValue, int osValue)
        {
            return terrariaAvailable && terrariaValue >= -32 ? terrariaValue : osValue;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }

    public sealed class OperationWindowInteraction
    {
        public bool InWindow { get; set; }
        public bool InTitle { get; set; }
        public bool InResizeGrip { get; set; }
        public bool StartedDrag { get; set; }
        public bool StartedResize { get; set; }
        public bool Dragging { get; set; }
        public bool Resizing { get; set; }
        public bool EndedInteraction { get; set; }
        public bool CapturesMouse { get; set; }
        public string UiElementId { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }

        public OperationWindowInteraction()
        {
            UiElementId = string.Empty;
            MouseX = -1;
            MouseY = -1;
        }
    }
}
