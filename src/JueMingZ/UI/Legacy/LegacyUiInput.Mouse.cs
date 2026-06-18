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
        public static LegacyMouseSnapshot ReadMouse()
        {
            var raw = DiagnosticMouseStateReader.Read();
            return BuildMouseSnapshot(raw, true, LegacyMainUiScale.Resolve(raw));
        }

        internal static LegacyMouseSnapshot ReadMouse(DiagnosticMouseState raw, LegacyMainUiScaleSnapshot scale)
        {
            return BuildMouseSnapshot(raw, true, scale);
        }

        internal static LegacyMouseSnapshot ReadMouseForOverlay(DiagnosticMouseState raw, LegacyMainUiScaleSnapshot scale)
        {
            return BuildMouseSnapshot(raw, false, scale);
        }

        internal static LegacyMouseSnapshot ReadMouseForInterfaceOverlay(DiagnosticMouseState raw)
        {
            return BuildMouseSnapshot(raw, false, LegacyMainUiScale.Resolve(raw), false);
        }

        private static LegacyMouseSnapshot BuildMouseSnapshot(DiagnosticMouseState raw, bool consumePendingScroll)
        {
            return BuildMouseSnapshot(raw, consumePendingScroll, LegacyMainUiScale.Resolve(raw));
        }

        private static LegacyMouseSnapshot BuildMouseSnapshot(DiagnosticMouseState raw, bool consumePendingScroll, LegacyMainUiScaleSnapshot scale)
        {
            return BuildMouseSnapshot(raw, consumePendingScroll, scale, true);
        }

        private static LegacyMouseSnapshot BuildMouseSnapshot(DiagnosticMouseState raw, bool consumePendingScroll, LegacyMainUiScaleSnapshot scale, bool applyMainDrawScale)
        {
            if (raw == null)
            {
                raw = new DiagnosticMouseState();
            }

            if (scale == null)
            {
                scale = LegacyMainUiScale.Resolve(raw);
            }

            var coordinate = ResolveLogicalMouse(raw);
            var x = applyMainDrawScale ? scale.ToBaseLogicalX(coordinate.X) : coordinate.X;
            var y = applyMainDrawScale ? scale.ToBaseLogicalY(coordinate.Y) : coordinate.Y;
            var readMode = applyMainDrawScale ? AppendLegacyScaleMode(coordinate.Mode, scale) : AppendInterfaceOverlayMode(coordinate.Mode);

            var inputAvailable = raw.GameInputAvailable;
            var down = inputAvailable && (raw.TerrariaLeftDown || raw.OsLeftDown);
            var scrollDelta = inputAvailable && !consumePendingScroll ? raw.ScrollDelta : 0;
            if (consumePendingScroll)
            {
                lock (SyncRoot)
                {
                    if (!inputAvailable)
                    {
                        _pendingUiScrollDelta = 0;
                    }
                    else if (_pendingUiScrollDelta != 0)
                    {
                        scrollDelta = _pendingUiScrollDelta;
                    }
                    else if (!_wheelConsumedThisFrame)
                    {
                        scrollDelta = raw.ScrollDelta;
                    }

                    _pendingUiScrollDelta = 0;
                }
            }

            return new LegacyMouseSnapshot
            {
                X = x,
                Y = y,
                LeftDown = down,
                LeftPressed = inputAvailable && down && !_wasLeftDown,
                LeftReleased = inputAvailable && !down && _wasLeftDown,
                ScrollDelta = scrollDelta,
                ReadAvailable = raw.TerrariaReadAvailable || raw.OsReadAvailable,
                ReadMode = readMode,
                WindowHit = IsWindowHit(raw, x, y, scale)
            };
        }

        private static bool IsWindowHit(DiagnosticMouseState raw, int resolvedX, int resolvedY, LegacyMainUiScaleSnapshot scale)
        {
            var window = LegacyMainUiState.WindowRect;
            if (window.Contains(resolvedX, resolvedY))
            {
                return true;
            }

            if (raw == null)
            {
                return false;
            }

            if (scale == null)
            {
                scale = LegacyMainUiScale.Resolve(raw);
            }

            if (raw.TerrariaReadAvailable && raw.TerrariaMouseX >= 0 && raw.TerrariaMouseY >= 0)
            {
                if (scale.ContainsScreenPoint(window, raw.TerrariaMouseX, raw.TerrariaMouseY))
                {
                    return true;
                }

                if (window.Contains(scale.ScreenToBaseLogicalX(raw.TerrariaMouseX), scale.ScreenToBaseLogicalY(raw.TerrariaMouseY)))
                {
                    return true;
                }
            }

            if (raw.OsReadAvailable && raw.OsClientMouseX >= 0 && raw.OsClientMouseY >= 0)
            {
                if (scale.ContainsScreenPoint(window, raw.OsClientMouseX, raw.OsClientMouseY))
                {
                    return true;
                }

                return window.Contains(scale.ScreenToBaseLogicalX(raw.OsClientMouseX), scale.ScreenToBaseLogicalY(raw.OsClientMouseY));
            }

            return false;
        }

        private static MouseCoordinate ResolveLogicalMouse(DiagnosticMouseState raw)
        {
            // Reconcile Terraria and OS coordinates under UI scale so hit tests match
            // the scaled F5 window instead of the raw screen cursor.
            var scaleX = raw.UiScaleX > 0.01d ? raw.UiScaleX : raw.UiScale;
            var scaleY = raw.UiScaleY > 0.01d ? raw.UiScaleY : raw.UiScale;
            if (scaleX <= 0.01d)
            {
                scaleX = 1d;
            }

            if (scaleY <= 0.01d)
            {
                scaleY = 1d;
            }

            var scaleActive = raw.UiScaleAvailable &&
                              (Math.Abs(scaleX - 1d) > 0.01d ||
                               Math.Abs(scaleY - 1d) > 0.01d ||
                               Math.Abs(raw.UiTranslateX) > 0.01d ||
                               Math.Abs(raw.UiTranslateY) > 0.01d);
            var hasTerraria = raw.TerrariaReadAvailable && raw.TerrariaMouseX >= 0 && raw.TerrariaMouseY >= 0;
            var hasOs = raw.OsReadAvailable && raw.OsClientMouseX >= 0 && raw.OsClientMouseY >= 0;
            if (hasTerraria && hasOs && scaleActive)
            {
                var osLogicalX = ScreenToUiCoordinate(raw.OsClientMouseX, scaleX, raw.UiTranslateX);
                var osLogicalY = ScreenToUiCoordinate(raw.OsClientMouseY, scaleY, raw.UiTranslateY);
                var rawDistance = DistanceSquared(raw.TerrariaMouseX, raw.TerrariaMouseY, raw.OsClientMouseX, raw.OsClientMouseY);
                var logicalDistance = DistanceSquared(raw.TerrariaMouseX, raw.TerrariaMouseY, osLogicalX, osLogicalY);
                var close = CloseDistanceSquared(scaleX, scaleY);
                if (logicalDistance <= close && logicalDistance <= rawDistance)
                {
                    return new MouseCoordinate(raw.TerrariaMouseX, raw.TerrariaMouseY, BuildMouseMode(raw, "TerrariaLogical"));
                }

                if (rawDistance <= close || rawDistance < logicalDistance)
                {
                    return new MouseCoordinate(
                        ScreenToUiCoordinate(raw.TerrariaMouseX, scaleX, raw.UiTranslateX),
                        ScreenToUiCoordinate(raw.TerrariaMouseY, scaleY, raw.UiTranslateY),
                        BuildMouseMode(raw, "TerrariaScreenToUi"));
                }
            }

            if (hasTerraria)
            {
                return new MouseCoordinate(raw.TerrariaMouseX, raw.TerrariaMouseY, BuildMouseMode(raw, "TerrariaRaw"));
            }

            if (hasOs)
            {
                if (scaleActive)
                {
                    return new MouseCoordinate(
                        ScreenToUiCoordinate(raw.OsClientMouseX, scaleX, raw.UiTranslateX),
                        ScreenToUiCoordinate(raw.OsClientMouseY, scaleY, raw.UiTranslateY),
                        BuildMouseMode(raw, "OsClientScreenToUi"));
                }

                return new MouseCoordinate(raw.OsClientMouseX, raw.OsClientMouseY, BuildMouseMode(raw, "OsClientRaw"));
            }

            return new MouseCoordinate(-1, -1, BuildMouseMode(raw, "none"));
        }

        private static int ScreenToUiCoordinate(int value, double scale, double translate)
        {
            if (value < 0)
            {
                return value;
            }

            if (scale <= 0.01d)
            {
                return value;
            }

            return (int)Math.Round((value - translate) / scale);
        }

        private static int DistanceSquared(int ax, int ay, int bx, int by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return dx * dx + dy * dy;
        }

        private static int CloseDistanceSquared(double scaleX, double scaleY)
        {
            var scale = Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
            var tolerance = Math.Max(4, (int)Math.Ceiling(scale * 3d));
            return tolerance * tolerance;
        }

        private static string BuildMouseMode(DiagnosticMouseState raw, string source)
        {
            var mode = raw == null ? string.Empty : raw.ReadMode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = "none";
            }

            if (raw != null && !string.IsNullOrWhiteSpace(raw.UiScaleSource))
            {
                return mode + "/" + source + "/" + raw.UiScaleSource;
            }

            return mode + "/" + source;
        }

        private static string AppendLegacyScaleMode(string mode, LegacyMainUiScaleSnapshot scale)
        {
            if (scale == null || !scale.Capped)
            {
                return mode ?? string.Empty;
            }

            return (mode ?? string.Empty) + "/LegacyScaleCap";
        }

        private static string AppendInterfaceOverlayMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = "none";
            }

            return mode + "/InterfaceOverlay";
        }
    }
}
