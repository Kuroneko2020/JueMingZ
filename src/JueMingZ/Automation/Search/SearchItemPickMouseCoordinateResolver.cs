using System;
using System.Globalization;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;
using JueMingZ.UI;

namespace JueMingZ.Automation.Search
{
    internal static class SearchItemPickMouseCoordinateResolver
    {
        internal static SearchItemPickMouseCoordinateSnapshot CaptureForTesting(SearchItemPickMouseCoordinateInput input)
        {
            return Capture(input);
        }

        internal static SearchItemPickMouseCoordinateSnapshot Capture(
            int fallbackMouseX,
            int fallbackMouseY,
            ulong fallbackGameUpdateCount,
            InformationWorldContext worldContext)
        {
            var input = ReadCurrentMouseInput(fallbackMouseX, fallbackMouseY);
            input.ScreenX = worldContext == null ? 0f : worldContext.ScreenX;
            input.ScreenY = worldContext == null ? 0f : worldContext.ScreenY;
            input.GameUpdateCount = worldContext == null ? fallbackGameUpdateCount : worldContext.GameUpdateCount;

            float worldX;
            float worldY;
            string worldSource;
            if (TryReadMainMouseWorld(worldContext == null ? null : worldContext.MainType, out worldX, out worldY, out worldSource))
            {
                input.WorldMouseAvailable = true;
                input.WorldMouseX = worldX;
                input.WorldMouseY = worldY;
                input.WorldMouseSource = worldSource;
            }

            return Capture(input);
        }

        private static SearchItemPickMouseCoordinateInput ReadCurrentMouseInput(int fallbackMouseX, int fallbackMouseY)
        {
            var input = new SearchItemPickMouseCoordinateInput
            {
                TerrariaReadAvailable = fallbackMouseX >= 0 && fallbackMouseY >= 0,
                TerrariaMouseX = fallbackMouseX,
                TerrariaMouseY = fallbackMouseY
            };

            try
            {
                var raw = DiagnosticMouseStateReader.Read();
                if (raw == null)
                {
                    return input;
                }

                if (raw.TerrariaReadAvailable)
                {
                    input.TerrariaReadAvailable = true;
                    input.TerrariaMouseX = raw.TerrariaMouseX;
                    input.TerrariaMouseY = raw.TerrariaMouseY;
                }

                input.OsReadAvailable = raw.OsReadAvailable;
                input.OsClientMouseX = raw.OsClientMouseX;
                input.OsClientMouseY = raw.OsClientMouseY;
                input.UiScaleAvailable = raw.UiScaleAvailable;
                input.UiScale = raw.UiScale;
                input.UiScaleX = raw.UiScaleX;
                input.UiScaleY = raw.UiScaleY;
                input.UiTranslateX = raw.UiTranslateX;
                input.UiTranslateY = raw.UiTranslateY;
                input.UiScaleSource = raw.UiScaleSource ?? string.Empty;
                input.ReadMode = raw.ReadMode ?? string.Empty;
            }
            catch
            {
                // The click snapshot must remain usable even when OS or UI-scale
                // diagnostics are unavailable; the fallback still records raw
                // Terraria mouse coordinates and world fallback source.
            }

            return input;
        }

        private static SearchItemPickMouseCoordinateSnapshot Capture(SearchItemPickMouseCoordinateInput input)
        {
            input = input ?? new SearchItemPickMouseCoordinateInput();
            var rawX = input.TerrariaReadAvailable ? input.TerrariaMouseX : input.OsClientMouseX;
            var rawY = input.TerrariaReadAvailable ? input.TerrariaMouseY : input.OsClientMouseY;
            var ui = ResolveUiMouse(input);

            var worldX = input.WorldMouseAvailable ? input.WorldMouseX : input.ScreenX + rawX;
            var worldY = input.WorldMouseAvailable ? input.WorldMouseY : input.ScreenY + rawY;
            var worldSource = input.WorldMouseAvailable
                ? NormalizeSource(input.WorldMouseSource, "Main.MouseWorld")
                : "screenPosition+rawMouse";

            var snapshot = new SearchItemPickMouseCoordinateSnapshot
            {
                RawMouseX = rawX,
                RawMouseY = rawY,
                UiMouseX = ui.X,
                UiMouseY = ui.Y,
                WorldMouseX = worldX,
                WorldMouseY = worldY,
                TileX = (int)Math.Floor(worldX / TerrariaTileReadCompat.TileSize),
                TileY = (int)Math.Floor(worldY / TerrariaTileReadCompat.TileSize),
                UiMouseSource = ui.Source,
                WorldMouseSource = worldSource,
                GameUpdateCount = input.GameUpdateCount
            };
            snapshot.SourceSummary = BuildSourceSummary(input, snapshot);
            return snapshot;
        }

        private static UiMouseCoordinate ResolveUiMouse(SearchItemPickMouseCoordinateInput input)
        {
            var scaleX = input.UiScaleX > 0.01d ? input.UiScaleX : input.UiScale;
            var scaleY = input.UiScaleY > 0.01d ? input.UiScaleY : input.UiScale;
            if (scaleX <= 0.01d)
            {
                scaleX = 1d;
            }

            if (scaleY <= 0.01d)
            {
                scaleY = 1d;
            }

            var scaleActive = input.UiScaleAvailable &&
                              (Math.Abs(scaleX - 1d) > 0.01d ||
                               Math.Abs(scaleY - 1d) > 0.01d ||
                               Math.Abs(input.UiTranslateX) > 0.01d ||
                               Math.Abs(input.UiTranslateY) > 0.01d);
            var hasTerraria = input.TerrariaReadAvailable && input.TerrariaMouseX >= 0 && input.TerrariaMouseY >= 0;
            var hasOs = input.OsReadAvailable && input.OsClientMouseX >= 0 && input.OsClientMouseY >= 0;
            if (hasTerraria && hasOs && scaleActive)
            {
                var osLogicalX = ScreenToUiCoordinate(input.OsClientMouseX, scaleX, input.UiTranslateX);
                var osLogicalY = ScreenToUiCoordinate(input.OsClientMouseY, scaleY, input.UiTranslateY);
                var rawDistance = DistanceSquared(input.TerrariaMouseX, input.TerrariaMouseY, input.OsClientMouseX, input.OsClientMouseY);
                var logicalDistance = DistanceSquared(input.TerrariaMouseX, input.TerrariaMouseY, osLogicalX, osLogicalY);
                var close = CloseDistanceSquared(scaleX, scaleY);
                if (logicalDistance <= close && logicalDistance <= rawDistance)
                {
                    return new UiMouseCoordinate(input.TerrariaMouseX, input.TerrariaMouseY, BuildUiSource(input, "TerrariaLogical"));
                }

                if (rawDistance <= close || rawDistance < logicalDistance)
                {
                    return new UiMouseCoordinate(
                        ScreenToUiCoordinate(input.TerrariaMouseX, scaleX, input.UiTranslateX),
                        ScreenToUiCoordinate(input.TerrariaMouseY, scaleY, input.UiTranslateY),
                        BuildUiSource(input, "TerrariaScreenToUi"));
                }
            }

            if (hasTerraria)
            {
                return new UiMouseCoordinate(input.TerrariaMouseX, input.TerrariaMouseY, BuildUiSource(input, "TerrariaRaw"));
            }

            if (hasOs)
            {
                if (scaleActive)
                {
                    return new UiMouseCoordinate(
                        ScreenToUiCoordinate(input.OsClientMouseX, scaleX, input.UiTranslateX),
                        ScreenToUiCoordinate(input.OsClientMouseY, scaleY, input.UiTranslateY),
                        BuildUiSource(input, "OsClientScreenToUi"));
                }

                return new UiMouseCoordinate(input.OsClientMouseX, input.OsClientMouseY, BuildUiSource(input, "OsClientRaw"));
            }

            return new UiMouseCoordinate(-1, -1, BuildUiSource(input, "none"));
        }

        private static bool TryReadMainMouseWorld(Type mainType, out float worldX, out float worldY, out string source)
        {
            source = string.Empty;
            worldX = 0f;
            worldY = 0f;
            // Terraria's Main.MouseWorld is the authoritative world cursor,
            // including vanilla gravity-flip handling; raw/UI coordinates are
            // only fallbacks when this read is unavailable.
            return mainType != null &&
                   InformationReflection.TryReadVector2(InformationReflection.GetStaticMember(mainType, "MouseWorld"), out worldX, out worldY) &&
                   SetSource("Main.MouseWorld", out source);
        }

        private static bool SetSource(string value, out string source)
        {
            source = value ?? string.Empty;
            return true;
        }

        private static int ScreenToUiCoordinate(int value, double scale, double translate)
        {
            if (value < 0 || scale <= 0.01d)
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

        private static string BuildUiSource(SearchItemPickMouseCoordinateInput input, string source)
        {
            var mode = input == null ? string.Empty : input.ReadMode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = "none";
            }

            var uiScaleSource = input == null ? string.Empty : input.UiScaleSource ?? string.Empty;
            return string.IsNullOrWhiteSpace(uiScaleSource)
                ? mode + "/" + source
                : mode + "/" + source + "/" + uiScaleSource;
        }

        private static string NormalizeSource(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string BuildSourceSummary(
            SearchItemPickMouseCoordinateInput input,
            SearchItemPickMouseCoordinateSnapshot snapshot)
        {
            return "raw=" + snapshot.RawMouseX.ToString(CultureInfo.InvariantCulture) +
                   "," + snapshot.RawMouseY.ToString(CultureInfo.InvariantCulture) +
                   ";ui=" + snapshot.UiMouseX.ToString(CultureInfo.InvariantCulture) +
                   "," + snapshot.UiMouseY.ToString(CultureInfo.InvariantCulture) +
                   ";uiSource=" + NormalizeSource(snapshot.UiMouseSource, "unknown") +
                   ";world=" + snapshot.WorldMouseX.ToString("0.###", CultureInfo.InvariantCulture) +
                   "," + snapshot.WorldMouseY.ToString("0.###", CultureInfo.InvariantCulture) +
                   ";worldSource=" + NormalizeSource(snapshot.WorldMouseSource, "unknown") +
                   ";screen=" + input.ScreenX.ToString("0.###", CultureInfo.InvariantCulture) +
                   "," + input.ScreenY.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private sealed class UiMouseCoordinate
        {
            public UiMouseCoordinate(int x, int y, string source)
            {
                X = x;
                Y = y;
                Source = source ?? string.Empty;
            }

            public int X { get; private set; }
            public int Y { get; private set; }
            public string Source { get; private set; }
        }
    }
}
