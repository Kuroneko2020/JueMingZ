using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyUiInput
    {
        public static void FinishFrame(LegacyMouseSnapshot mouse, bool hoverInWindow)
        {
            lock (SyncRoot)
            {
                if (mouse != null)
                {
                    _wasLeftDown = mouse.LeftDown;
                }

                _lastHoverInWindow = hoverInWindow;
            }
        }

        public static bool IsActiveInteraction()
        {
            bool active;
            lock (SyncRoot)
            {
                active = !string.IsNullOrWhiteSpace(_activeMode);
            }

            return active || LegacyTextInput.IsAnyFocused || LegacyHexColorInput.IsAnyFocused;
        }

        public static bool IsMouseInWindow(LegacyMouseSnapshot mouse)
        {
            if (mouse == null)
            {
                return false;
            }

            return mouse.WindowHit || LegacyMainUiState.WindowRect.Contains(mouse.X, mouse.Y);
        }

        public static bool IsMouseInWindowForDiagnostics(DiagnosticMouseState raw)
        {
            if (raw == null)
            {
                return false;
            }

            var coordinate = ResolveLogicalMouse(raw);
            return IsWindowHit(raw, coordinate.X, coordinate.Y);
        }

        public static void ResetInteractionState()
        {
            lock (SyncRoot)
            {
                _activeMode = string.Empty;
                _activeSliderId = string.Empty;
                _activeSliderValue = 0;
                _pendingSliderId = string.Empty;
                _pendingSliderValue = 0;
                _dragOffsetX = 0;
                _dragOffsetY = 0;
                _resizeStartMouseX = 0;
                _resizeStartMouseY = 0;
                _wasLeftDown = false;
                _lastClickElementId = string.Empty;
                _lastClickUtc = DateTime.MinValue;
                _lastClickX = -1;
                _lastClickY = -1;
            }

            LegacyTextInput.ClearFocus();
        }

        public static void HandleWindowFrame(LegacyMouseSnapshot mouse, LegacyUiRect titleRect, LegacyUiRect resizeRect)
        {
            if (mouse == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (LegacyUiMetrics.AllowResize && mouse.LeftPressed && resizeRect.Contains(mouse.X, mouse.Y))
                {
                    BeginResizeLocked(mouse);
                }
                else if (mouse.LeftPressed && titleRect.Contains(mouse.X, mouse.Y))
                {
                    BeginDragLocked(mouse);
                }

                if (mouse.LeftDown && string.Equals(_activeMode, "drag", StringComparison.Ordinal))
                {
                    LegacyMainUiState.SetWindow(mouse.X - _dragOffsetX, mouse.Y - _dragOffsetY, LegacyMainUiState.Width, LegacyMainUiState.Height, false);
                }

                if (LegacyUiMetrics.AllowResize && mouse.LeftDown && string.Equals(_activeMode, "resize", StringComparison.Ordinal))
                {
                    LegacyMainUiState.SetWindow(
                        _resizeStartX,
                        _resizeStartY,
                        _resizeStartWidth + (mouse.X - _resizeStartMouseX),
                        _resizeStartHeight + (mouse.Y - _resizeStartMouseY),
                        false);
                }

                if (mouse.LeftReleased && (string.Equals(_activeMode, "drag", StringComparison.Ordinal) || string.Equals(_activeMode, "resize", StringComparison.Ordinal)))
                {
                    var mode = _activeMode;
                    _activeMode = string.Empty;
                    LegacyMainUiState.SaveWindow();
                    RecordWindowMoveOrResize(mode);
                }
            }
        }

        public static void HandleScrollbarDrag(LegacyMouseSnapshot mouse, LegacyScrollArea area)
        {
            if (mouse == null || area == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (mouse.LeftPressed && area.ScrollbarThumb.Contains(mouse.X, mouse.Y))
                {
                    _activeMode = "scrollbar";
                }

                if (mouse.LeftDown && string.Equals(_activeMode, "scrollbar", StringComparison.Ordinal))
                {
                    LegacyMainUiState.SetScrollOffset(area.ThumbToScroll(mouse.Y), area.MaxScroll);
                }

                if (mouse.LeftReleased && string.Equals(_activeMode, "scrollbar", StringComparison.Ordinal))
                {
                    _activeMode = string.Empty;
                }
            }
        }

        private static void BeginDragLocked(LegacyMouseSnapshot mouse)
        {
            _activeMode = "drag";
            _dragOffsetX = mouse.X - LegacyMainUiState.X;
            _dragOffsetY = mouse.Y - LegacyMainUiState.Y;
            CaptureWindowStartLocked();
        }

        private static void BeginResizeLocked(LegacyMouseSnapshot mouse)
        {
            if (!LegacyUiMetrics.AllowResize)
            {
                return;
            }

            _activeMode = "resize";
            _resizeStartMouseX = mouse.X;
            _resizeStartMouseY = mouse.Y;
            _resizeStartWidth = LegacyMainUiState.Width;
            _resizeStartHeight = LegacyMainUiState.Height;
            _resizeStartX = LegacyMainUiState.X;
            _resizeStartY = LegacyMainUiState.Y;
            CaptureWindowStartLocked();
        }

        private static void CaptureWindowStartLocked()
        {
            _windowStartX = LegacyMainUiState.X;
            _windowStartY = LegacyMainUiState.Y;
            _windowStartWidth = LegacyMainUiState.Width;
            _windowStartHeight = LegacyMainUiState.Height;
        }

        private static void RecordWindowMoveOrResize(string mode)
        {
            if (string.Equals(mode, "resize", StringComparison.Ordinal) && !LegacyUiMetrics.AllowResize)
            {
                return;
            }

            var scenario = string.Equals(mode, "resize", StringComparison.Ordinal) ? "Ui.MainWindow.Resize" : "Ui.MainWindow.Move";
            var elementId = string.Equals(mode, "resize", StringComparison.Ordinal) ? "main-window-resize-grip" : "main-window-title";
            var button = new DiagnosticTestButton
            {
                Id = elementId,
                Label = elementId,
                X = LegacyMainUiState.X,
                Y = LegacyMainUiState.Y,
                Width = LegacyMainUiState.Width,
                Height = string.Equals(mode, "resize", StringComparison.Ordinal) ? LegacyUiMetrics.ResizeGripSize : LegacyUiMetrics.TitleHeight,
                Enabled = true
            };
            DiagnosticInteractionDiagnostics.RecordButton(elementId, elementId, "LegacyMainUi", "Mouse", LegacyMainUiState.X, LegacyMainUiState.Y, false, string.Empty, button);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                scenario,
                "UI",
                string.Empty,
                "Succeeded",
                "Succeeded",
                scenario + " completed.",
                0,
                "{" +
                    "\"windowX\":" + _windowStartX + "," +
                    "\"windowY\":" + _windowStartY + "," +
                    "\"windowWidth\":" + _windowStartWidth + "," +
                    "\"windowHeight\":" + _windowStartHeight +
                "}",
                LegacyMainUiState.BuildUiStateJson(),
                "{\"mouseCaptured\":true}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }
    }
}
