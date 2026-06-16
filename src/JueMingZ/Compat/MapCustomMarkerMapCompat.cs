using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class MapCustomMarkerMapCompat
    {
        private const string FullscreenTransformSource = "fullscreenTransform";
        private const string FullscreenDrawMouseSource = "fullscreenDrawMouse";
        private const string FallbackTransformSource = "fallback";
        private const float ScreenSizeScaleTolerance = 0.025f;
        private const int ScreenCoordinateTolerancePixels = 2;
        private const float MapScaleFreshnessTolerance = 0.0005f;
        private const float MapFullscreenPosFreshnessToleranceTiles = 0.01f;
        private const long UnknownGameUpdateCount = -1;
        private static readonly object TransformSyncRoot = new object();
        private static MapCustomMarkerFullscreenTransformSnapshot _lastFullscreenTransform;
        private static MapCustomMarkerMapPoint _lastFullscreenDrawMousePoint;

        public static bool TryReadFullscreenMapMouseTile(out MapCustomMarkerMapPoint point, out string message)
        {
            point = new MapCustomMarkerMapPoint();
            message = string.Empty;

            try
            {
                if (!Main.mapFullscreen)
                {
                    message = "fullscreen map is not open";
                    return false;
                }

                if (Main.gameMenu || Main.maxTilesX <= 0 || Main.maxTilesY <= 0 || Main.mapFullscreenScale <= 0f)
                {
                    message = "fullscreen map context unavailable";
                    return false;
                }

                var screenWidth = Math.Max(1, Main.screenWidth);
                var screenHeight = Math.Max(1, Main.screenHeight);
                var currentFullscreenPos = Main.mapFullscreenPos;
                var currentMapScale = Main.mapFullscreenScale;
                var currentGameUpdateCount = ReadGameUpdateCountOrUnknown();
                string fallbackReason;
                long transformAgeUpdates;
                point = ResolveFullscreenMouseTile(
                    Main.mouseX,
                    Main.mouseY,
                    screenWidth,
                    screenHeight,
                    Main.maxTilesX,
                    Main.maxTilesY,
                    currentFullscreenPos,
                    currentMapScale,
                    currentGameUpdateCount,
                    out fallbackReason,
                    out transformAgeUpdates);

                point.ScreenWidth = screenWidth;
                point.ScreenHeight = screenHeight;
                point.CurrentGameUpdateCount = currentGameUpdateCount;
                message = "ok";
                return true;
            }
            catch (Exception error)
            {
                message = "fullscreen map mouse tile read failed: " + error.Message;
                return false;
            }
        }

        public static MapCustomMarkerFullscreenTransformSnapshot RecordFullscreenTransform(
            Vector2 mapTopLeft,
            float mapScale,
            int screenWidth,
            int screenHeight,
            string route)
        {
            var snapshot = RecordFullscreenTransformCore(
                mapTopLeft,
                mapScale,
                screenWidth,
                screenHeight,
                route,
                ReadMapFullscreenPosOrZero(),
                ReadGameUpdateCountOrUnknown());
            RecordFullscreenDrawMousePoint(snapshot, Main.mouseX, Main.mouseY, Main.maxTilesX, Main.maxTilesY);
            return snapshot;
        }

        internal static MapCustomMarkerFullscreenTransformSnapshot RecordFullscreenTransformForTesting(
            Vector2 mapTopLeft,
            float mapScale,
            int screenWidth,
            int screenHeight,
            string route,
            Vector2 mapFullscreenPos,
            long gameUpdateCount)
        {
            return RecordFullscreenTransformCore(
                mapTopLeft,
                mapScale,
                screenWidth,
                screenHeight,
                route,
                mapFullscreenPos,
                gameUpdateCount);
        }

        internal static MapCustomMarkerMapPoint RecordFullscreenDrawMousePointForTesting(
            int mouseX,
            int mouseY,
            int maxTilesX,
            int maxTilesY)
        {
            var transform = GetLastFullscreenTransformForTesting();
            return RecordFullscreenDrawMousePoint(transform, mouseX, mouseY, maxTilesX, maxTilesY);
        }

        private static MapCustomMarkerFullscreenTransformSnapshot RecordFullscreenTransformCore(
            Vector2 mapTopLeft,
            float mapScale,
            int screenWidth,
            int screenHeight,
            string route,
            Vector2 mapFullscreenPos,
            long gameUpdateCount)
        {
            var snapshot = new MapCustomMarkerFullscreenTransformSnapshot
            {
                HasTransform = IsValidScale(mapScale),
                Route = route ?? string.Empty,
                ScreenWidth = Math.Max(1, screenWidth),
                ScreenHeight = Math.Max(1, screenHeight),
                MapTopLeftX = mapTopLeft.X,
                MapTopLeftY = mapTopLeft.Y,
                MapScale = IsValidScale(mapScale) ? mapScale : 0f,
                MapFullscreenPosX = mapFullscreenPos.X,
                MapFullscreenPosY = mapFullscreenPos.Y,
                GameUpdateCount = gameUpdateCount,
                Utc = DateTime.UtcNow
            };

            lock (TransformSyncRoot)
            {
                _lastFullscreenTransform = snapshot.Clone();
            }

            return snapshot.Clone();
        }

        private static MapCustomMarkerMapPoint RecordFullscreenDrawMousePoint(
            MapCustomMarkerFullscreenTransformSnapshot transform,
            int mouseX,
            int mouseY,
            int maxTilesX,
            int maxTilesY)
        {
            if (transform == null ||
                !transform.HasTransform ||
                maxTilesX <= 0 ||
                maxTilesY <= 0)
            {
                return null;
            }

            // Original fullscreen ping resolves Main.MouseScreen inside DrawMap.
            // Cache that draw-phase mouse sample so right-click creation can use
            // the same coordinate space instead of reinterpreting Update mouse.
            var point = ScreenToTileFromTransform(
                mouseX,
                mouseY,
                transform.MapTopLeftX,
                transform.MapTopLeftY,
                transform.MapScale,
                maxTilesX,
                maxTilesY);
            point.TransformSource = FullscreenDrawMouseSource;
            point.ScreenWidth = Math.Max(1, transform.ScreenWidth);
            point.ScreenHeight = Math.Max(1, transform.ScreenHeight);
            point.CurrentMapFullscreenPosX = transform.MapFullscreenPosX;
            point.CurrentMapFullscreenPosY = transform.MapFullscreenPosY;
            point.CurrentMapScale = transform.MapScale;
            point.CurrentGameUpdateCount = transform.GameUpdateCount;
            point.TransformAgeUpdates = 0;
            lock (TransformSyncRoot)
            {
                _lastFullscreenDrawMousePoint = CloneMapPoint(point);
            }

            return CloneMapPoint(point);
        }

        internal static MapCustomMarkerFullscreenTransformSnapshot GetLastFullscreenTransformForTesting()
        {
            lock (TransformSyncRoot)
            {
                return _lastFullscreenTransform == null ? null : _lastFullscreenTransform.Clone();
            }
        }

        internal static void ResetFullscreenTransformForTesting()
        {
            lock (TransformSyncRoot)
            {
                _lastFullscreenTransform = null;
                _lastFullscreenDrawMousePoint = null;
            }
        }

        internal static MapCustomMarkerMapPoint ScreenToTileFromTransformForTesting(
            int mouseX,
            int mouseY,
            Vector2 mapTopLeft,
            float scale,
            int maxTilesX,
            int maxTilesY)
        {
            return ScreenToTileFromTransform(mouseX, mouseY, mapTopLeft.X, mapTopLeft.Y, scale, maxTilesX, maxTilesY);
        }

        internal static bool TryScreenToTileFromLastTransformForTesting(
            int mouseX,
            int mouseY,
            int screenWidth,
            int screenHeight,
            int maxTilesX,
            int maxTilesY,
            out MapCustomMarkerMapPoint point,
            out string fallbackReason)
        {
            return TryScreenToTileFromLastTransform(
                mouseX,
                mouseY,
                screenWidth,
                screenHeight,
                maxTilesX,
                maxTilesY,
                CurrentMapFullscreenPosForTesting(),
                CurrentMapScaleForTesting(),
                CurrentGameUpdateCountForTesting(),
                out point,
                out fallbackReason,
                out _);
        }

        internal static bool TryScreenToTileFromLastTransformForTesting(
            int mouseX,
            int mouseY,
            int screenWidth,
            int screenHeight,
            int maxTilesX,
            int maxTilesY,
            Vector2 currentFullscreenPos,
            float currentMapScale,
            long currentGameUpdateCount,
            out MapCustomMarkerMapPoint point,
            out string fallbackReason,
            out long transformAgeUpdates)
        {
            return TryScreenToTileFromLastTransform(
                mouseX,
                mouseY,
                screenWidth,
                screenHeight,
                maxTilesX,
                maxTilesY,
                currentFullscreenPos,
                currentMapScale,
                currentGameUpdateCount,
                out point,
                out fallbackReason,
                out transformAgeUpdates);
        }

        internal static MapCustomMarkerMapPoint ResolveFullscreenMouseTileForTesting(
            int mouseX,
            int mouseY,
            int screenWidth,
            int screenHeight,
            int maxTilesX,
            int maxTilesY,
            Vector2 currentFullscreenPos,
            float currentMapScale,
            long currentGameUpdateCount,
            out string fallbackReason,
            out long transformAgeUpdates)
        {
            return ResolveFullscreenMouseTile(
                mouseX,
                mouseY,
                screenWidth,
                screenHeight,
                maxTilesX,
                maxTilesY,
                currentFullscreenPos,
                currentMapScale,
                currentGameUpdateCount,
                out fallbackReason,
                out transformAgeUpdates);
        }

        internal static MapCustomMarkerMapPoint ScreenToTile(
            int mouseX,
            int mouseY,
            Vector2 fullscreenPos,
            float scale,
            int screenWidth,
            int screenHeight,
            int maxTilesX,
            int maxTilesY)
        {
            if (scale <= 0f)
            {
                scale = 1f;
            }

            var mapTopLeft = BuildFallbackMapTopLeft(fullscreenPos, scale, screenWidth, screenHeight);
            var point = ScreenToTileFromTransform(mouseX, mouseY, mapTopLeft.X, mapTopLeft.Y, scale, maxTilesX, maxTilesY);
            point.TransformSource = FallbackTransformSource;
            point.FallbackReason = "fullscreenTransformUnavailable";
            point.CurrentMapFullscreenPosX = fullscreenPos.X;
            point.CurrentMapFullscreenPosY = fullscreenPos.Y;
            point.CurrentMapScale = scale;
            point.TransformAgeUpdates = UnknownGameUpdateCount;
            point.ScreenWidth = Math.Max(1, screenWidth);
            point.ScreenHeight = Math.Max(1, screenHeight);
            return point;
        }

        internal static Vector2 BuildFallbackMapTopLeftForTesting(
            Vector2 fullscreenPos,
            float scale,
            int screenWidth,
            int screenHeight)
        {
            return BuildFallbackMapTopLeft(fullscreenPos, scale, screenWidth, screenHeight);
        }

        private static bool TryScreenToTileFromLastTransform(
            int mouseX,
            int mouseY,
            int screenWidth,
            int screenHeight,
            int maxTilesX,
            int maxTilesY,
            Vector2 currentFullscreenPos,
            float currentMapScale,
            long currentGameUpdateCount,
            out MapCustomMarkerMapPoint point,
            out string fallbackReason,
            out long transformAgeUpdates)
        {
            point = null;
            fallbackReason = string.Empty;
            transformAgeUpdates = UnknownGameUpdateCount;
            MapCustomMarkerFullscreenTransformSnapshot transform;
            lock (TransformSyncRoot)
            {
                transform = _lastFullscreenTransform == null ? null : _lastFullscreenTransform.Clone();
            }

            if (transform == null || !transform.HasTransform)
            {
                fallbackReason = transform == null ? "noRecentFullscreenTransform" : "scaleMismatch";
                return false;
            }

            transformAgeUpdates = CalculateTransformAgeUpdates(transform, currentGameUpdateCount);
            if (!AreScreenSizesCompatible(transform, screenWidth, screenHeight, mouseX, mouseY))
            {
                fallbackReason = "screenSizeMismatch";
                return false;
            }

            if (!IsTransformViewStateFresh(transform, currentFullscreenPos, currentMapScale))
            {
                fallbackReason = "viewStateMismatch";
                return false;
            }

            point = ScreenToTileFromTransform(
                mouseX,
                mouseY,
                transform.MapTopLeftX,
                transform.MapTopLeftY,
                transform.MapScale,
                maxTilesX,
                maxTilesY);
            point.CurrentMapFullscreenPosX = currentFullscreenPos.X;
            point.CurrentMapFullscreenPosY = currentFullscreenPos.Y;
            point.CurrentMapScale = currentMapScale;
            point.CurrentGameUpdateCount = currentGameUpdateCount;
            point.TransformAgeUpdates = transformAgeUpdates;
            return true;
        }

        private static MapCustomMarkerMapPoint ResolveFullscreenMouseTile(
            int mouseX,
            int mouseY,
            int screenWidth,
            int screenHeight,
            int maxTilesX,
            int maxTilesY,
            Vector2 currentFullscreenPos,
            float currentMapScale,
            long currentGameUpdateCount,
            out string fallbackReason,
            out long transformAgeUpdates)
        {
            MapCustomMarkerMapPoint point;
            if (TryScreenToTileFromLastDrawMouse(
                screenWidth,
                screenHeight,
                currentFullscreenPos,
                currentMapScale,
                currentGameUpdateCount,
                out point,
                out fallbackReason,
                out transformAgeUpdates))
            {
                return point;
            }

            if (TryScreenToTileFromLastTransform(
                mouseX,
                mouseY,
                screenWidth,
                screenHeight,
                maxTilesX,
                maxTilesY,
                currentFullscreenPos,
                currentMapScale,
                currentGameUpdateCount,
                out point,
                out fallbackReason,
                out transformAgeUpdates))
            {
                return point;
            }

            point = ScreenToTile(
                mouseX,
                mouseY,
                currentFullscreenPos,
                currentMapScale,
                screenWidth,
                screenHeight,
                maxTilesX,
                maxTilesY);
            point.TransformSource = FallbackTransformSource;
            point.FallbackReason = fallbackReason;
            point.TransformAgeUpdates = transformAgeUpdates;
            return point;
        }

        private static bool TryScreenToTileFromLastDrawMouse(
            int screenWidth,
            int screenHeight,
            Vector2 currentFullscreenPos,
            float currentMapScale,
            long currentGameUpdateCount,
            out MapCustomMarkerMapPoint point,
            out string fallbackReason,
            out long transformAgeUpdates)
        {
            point = null;
            fallbackReason = string.Empty;
            transformAgeUpdates = UnknownGameUpdateCount;
            MapCustomMarkerMapPoint sample;
            lock (TransformSyncRoot)
            {
                sample = CloneMapPoint(_lastFullscreenDrawMousePoint);
            }

            if (sample == null)
            {
                fallbackReason = "noRecentFullscreenDrawMouse";
                return false;
            }

            transformAgeUpdates = CalculatePointAgeUpdates(sample, currentGameUpdateCount);
            if (!AreScreenSizesCompatible(
                Math.Max(1, sample.ScreenWidth),
                Math.Max(1, sample.ScreenHeight),
                screenWidth,
                screenHeight,
                sample.ScreenX,
                sample.ScreenY))
            {
                fallbackReason = "screenSizeMismatch";
                return false;
            }

            if (!IsViewStateFresh(
                sample.CurrentMapFullscreenPosX,
                sample.CurrentMapFullscreenPosY,
                sample.CurrentMapScale,
                currentFullscreenPos,
                currentMapScale))
            {
                fallbackReason = "viewStateMismatch";
                return false;
            }

            point = sample;
            point.CurrentMapFullscreenPosX = currentFullscreenPos.X;
            point.CurrentMapFullscreenPosY = currentFullscreenPos.Y;
            point.CurrentMapScale = currentMapScale;
            point.CurrentGameUpdateCount = currentGameUpdateCount;
            point.TransformAgeUpdates = transformAgeUpdates;
            return true;
        }

        private static bool AreScreenSizesCompatible(
            MapCustomMarkerFullscreenTransformSnapshot transform,
            int screenWidth,
            int screenHeight,
            int mouseX,
            int mouseY)
        {
            var transformWidth = Math.Max(1, transform.ScreenWidth);
            var transformHeight = Math.Max(1, transform.ScreenHeight);
            return AreScreenSizesCompatible(transformWidth, transformHeight, screenWidth, screenHeight, mouseX, mouseY);
        }

        private static bool AreScreenSizesCompatible(
            int transformWidth,
            int transformHeight,
            int screenWidth,
            int screenHeight,
            int mouseX,
            int mouseY)
        {
            var currentWidth = Math.Max(1, screenWidth);
            var currentHeight = Math.Max(1, screenHeight);
            if (transformWidth == currentWidth && transformHeight == currentHeight)
            {
                return true;
            }

            var widthScale = currentWidth / (float)transformWidth;
            var heightScale = currentHeight / (float)transformHeight;
            var scaleEquivalent =
                IsValidScale(widthScale) &&
                IsValidScale(heightScale) &&
                Math.Abs(widthScale - heightScale) <= ScreenSizeScaleTolerance &&
                widthScale >= 0.5f &&
                widthScale <= 2.5f;
            if (!scaleEquivalent)
            {
                return false;
            }

            // OnPostFullscreenMapDraw can report UI-zoom logical bounds while
            // Update sees the backing screen bounds. Keep the cached transform
            // only when the mouse coordinate is already inside that UI-space.
            return mouseX >= -ScreenCoordinateTolerancePixels &&
                   mouseY >= -ScreenCoordinateTolerancePixels &&
                   mouseX <= transformWidth + ScreenCoordinateTolerancePixels &&
                   mouseY <= transformHeight + ScreenCoordinateTolerancePixels;
        }

        private static Vector2 BuildFallbackMapTopLeft(
            Vector2 fullscreenPos,
            float scale,
            int screenWidth,
            int screenHeight)
        {
            var currentScale = IsValidScale(scale) ? scale : 1f;
            var iconOriginX = -fullscreenPos.X * currentScale + Math.Max(1, screenWidth) / 2f;
            var iconOriginY = -fullscreenPos.Y * currentScale + Math.Max(1, screenHeight) / 2f;
            return new Vector2(iconOriginX, iconOriginY);
        }

        private static bool IsTransformViewStateFresh(
            MapCustomMarkerFullscreenTransformSnapshot transform,
            Vector2 currentFullscreenPos,
            float currentMapScale)
        {
            if (!IsValidScale(currentMapScale))
            {
                return false;
            }

            // The previous draw transform is safe for right-click only while
            // the fullscreen map view state still matches the current Update.
            // Dragging or wheel zooming changes these fields before the next
            // draw, so stale transforms must fall back to current map state.
            return Math.Abs(transform.MapScale - currentMapScale) <= MapScaleFreshnessTolerance &&
                   Math.Abs(transform.MapFullscreenPosX - currentFullscreenPos.X) <= MapFullscreenPosFreshnessToleranceTiles &&
                   Math.Abs(transform.MapFullscreenPosY - currentFullscreenPos.Y) <= MapFullscreenPosFreshnessToleranceTiles;
        }

        private static bool IsViewStateFresh(
            float snapshotFullscreenPosX,
            float snapshotFullscreenPosY,
            float snapshotMapScale,
            Vector2 currentFullscreenPos,
            float currentMapScale)
        {
            if (!IsValidScale(currentMapScale))
            {
                return false;
            }

            return Math.Abs(snapshotMapScale - currentMapScale) <= MapScaleFreshnessTolerance &&
                   Math.Abs(snapshotFullscreenPosX - currentFullscreenPos.X) <= MapFullscreenPosFreshnessToleranceTiles &&
                   Math.Abs(snapshotFullscreenPosY - currentFullscreenPos.Y) <= MapFullscreenPosFreshnessToleranceTiles;
        }

        private static long CalculateTransformAgeUpdates(
            MapCustomMarkerFullscreenTransformSnapshot transform,
            long currentGameUpdateCount)
        {
            if (transform == null ||
                transform.GameUpdateCount < 0 ||
                currentGameUpdateCount < 0 ||
                currentGameUpdateCount < transform.GameUpdateCount)
            {
                return UnknownGameUpdateCount;
            }

            return currentGameUpdateCount - transform.GameUpdateCount;
        }

        private static long CalculatePointAgeUpdates(
            MapCustomMarkerMapPoint point,
            long currentGameUpdateCount)
        {
            if (point == null ||
                point.CurrentGameUpdateCount < 0 ||
                currentGameUpdateCount < 0 ||
                currentGameUpdateCount < point.CurrentGameUpdateCount)
            {
                return UnknownGameUpdateCount;
            }

            return currentGameUpdateCount - point.CurrentGameUpdateCount;
        }

        private static MapCustomMarkerMapPoint ScreenToTileFromTransform(
            int mouseX,
            int mouseY,
            float mapTopLeftX,
            float mapTopLeftY,
            float scale,
            int maxTilesX,
            int maxTilesY)
        {
            if (!IsValidScale(scale))
            {
                scale = 1f;
            }

            // OnPostFullscreenMapDraw fires after Terraria rebases num3/num4 to
            // the same fullscreen overlay origin used by MapIcons.Draw. Do not
            // subtract the 10-tile map margin again here.
            var tileX = (int)Math.Floor((mouseX - mapTopLeftX) / scale);
            var tileY = (int)Math.Floor((mouseY - mapTopLeftY) / scale);
            return new MapCustomMarkerMapPoint
            {
                TileX = Clamp(tileX, 0, Math.Max(0, maxTilesX - 1)),
                TileY = Clamp(tileY, 0, Math.Max(0, maxTilesY - 1)),
                ScreenX = mouseX,
                ScreenY = mouseY,
                WorldSizeX = Math.Max(0, maxTilesX),
                WorldSizeY = Math.Max(0, maxTilesY),
                TransformSource = FullscreenTransformSource,
                FallbackReason = string.Empty,
                MapTopLeftX = mapTopLeftX,
                MapTopLeftY = mapTopLeftY,
                MapScale = scale,
                TransformAgeUpdates = UnknownGameUpdateCount,
                CurrentMapScale = scale
            };
        }

        private static long ReadGameUpdateCountOrUnknown()
        {
            ulong gameUpdateCount;
            if (!TerrariaMainCompat.TryReadGameUpdateCount(out gameUpdateCount))
            {
                return UnknownGameUpdateCount;
            }

            return gameUpdateCount > (ulong)long.MaxValue ? long.MaxValue : (long)gameUpdateCount;
        }

        private static Vector2 ReadMapFullscreenPosOrZero()
        {
            try
            {
                return Main.mapFullscreenPos;
            }
            catch
            {
                return Vector2.Zero;
            }
        }

        private static Vector2 CurrentMapFullscreenPosForTesting()
        {
            var transform = GetLastFullscreenTransformForTesting();
            return transform == null
                ? Vector2.Zero
                : new Vector2(transform.MapFullscreenPosX, transform.MapFullscreenPosY);
        }

        private static float CurrentMapScaleForTesting()
        {
            var transform = GetLastFullscreenTransformForTesting();
            return transform == null || !IsValidScale(transform.MapScale) ? 1f : transform.MapScale;
        }

        private static long CurrentGameUpdateCountForTesting()
        {
            var transform = GetLastFullscreenTransformForTesting();
            return transform == null ? UnknownGameUpdateCount : transform.GameUpdateCount;
        }

        private static bool IsValidScale(float scale)
        {
            return !(float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static MapCustomMarkerMapPoint CloneMapPoint(MapCustomMarkerMapPoint source)
        {
            if (source == null)
            {
                return null;
            }

            return new MapCustomMarkerMapPoint
            {
                TileX = source.TileX,
                TileY = source.TileY,
                ScreenX = source.ScreenX,
                ScreenY = source.ScreenY,
                ScreenWidth = source.ScreenWidth,
                ScreenHeight = source.ScreenHeight,
                WorldSizeX = source.WorldSizeX,
                WorldSizeY = source.WorldSizeY,
                TransformSource = source.TransformSource ?? string.Empty,
                FallbackReason = source.FallbackReason ?? string.Empty,
                MapTopLeftX = source.MapTopLeftX,
                MapTopLeftY = source.MapTopLeftY,
                MapScale = source.MapScale,
                CurrentMapFullscreenPosX = source.CurrentMapFullscreenPosX,
                CurrentMapFullscreenPosY = source.CurrentMapFullscreenPosY,
                CurrentMapScale = source.CurrentMapScale,
                CurrentGameUpdateCount = source.CurrentGameUpdateCount,
                TransformAgeUpdates = source.TransformAgeUpdates
            };
        }
    }

    internal sealed class MapCustomMarkerMapPoint
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
        public string TransformSource { get; set; }
        public string FallbackReason { get; set; }
        public float MapTopLeftX { get; set; }
        public float MapTopLeftY { get; set; }
        public float MapScale { get; set; }
        public float CurrentMapFullscreenPosX { get; set; }
        public float CurrentMapFullscreenPosY { get; set; }
        public float CurrentMapScale { get; set; }
        public long CurrentGameUpdateCount { get; set; }
        public long TransformAgeUpdates { get; set; }

        public MapCustomMarkerMapPoint()
        {
            TransformSource = string.Empty;
            FallbackReason = string.Empty;
            CurrentGameUpdateCount = -1;
            TransformAgeUpdates = -1;
        }
    }

    internal sealed class MapCustomMarkerFullscreenTransformSnapshot
    {
        public bool HasTransform { get; set; }
        public string Route { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public float MapTopLeftX { get; set; }
        public float MapTopLeftY { get; set; }
        public float MapScale { get; set; }
        public float MapFullscreenPosX { get; set; }
        public float MapFullscreenPosY { get; set; }
        public long GameUpdateCount { get; set; }
        public DateTime? Utc { get; set; }

        public MapCustomMarkerFullscreenTransformSnapshot()
        {
            Route = string.Empty;
            GameUpdateCount = -1;
        }

        public MapCustomMarkerFullscreenTransformSnapshot Clone()
        {
            return new MapCustomMarkerFullscreenTransformSnapshot
            {
                HasTransform = HasTransform,
                Route = Route ?? string.Empty,
                ScreenWidth = ScreenWidth,
                ScreenHeight = ScreenHeight,
                MapTopLeftX = MapTopLeftX,
                MapTopLeftY = MapTopLeftY,
                MapScale = MapScale,
                MapFullscreenPosX = MapFullscreenPosX,
                MapFullscreenPosY = MapFullscreenPosY,
                GameUpdateCount = GameUpdateCount,
                Utc = Utc
            };
        }
    }
}
