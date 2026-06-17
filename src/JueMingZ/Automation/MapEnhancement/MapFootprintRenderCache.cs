using System;
using System.Collections.Generic;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Records;
using JueMingZ.Runtime;
using Microsoft.Xna.Framework;

namespace JueMingZ.Automation.MapEnhancement
{
    internal static class MapFootprintRenderCache
    {
        internal const int DefaultMaxDrawnLines = 6000;
        internal const int MaxCachedLines = 200000;
        internal const long RefreshCadenceTicks = PlayerWorldFootprintService.SampleCadenceTicks;
        internal const float DefaultMinDrawPixelStep = 1.5f;
        private const int ViewportPaddingPixels = 8;

        private static readonly object SyncRoot = new object();
        private static MapFootprintRenderSnapshot _snapshot = MapFootprintRenderSnapshot.Empty("notInitialized", "render cache not initialized");
        private static long _nextRefreshTick;
        private static string _lastPairId = string.Empty;
        private static int _lastDataSignature;
        private static bool _lastDisplayEnabled;

        public static void Tick(RuntimeSettingsSnapshot settings, GameStateSnapshot gameState, long runtimeTick)
        {
            var displayEnabled = settings != null && settings.MapFootprintsDisplayEnabled;
            if (!displayEnabled)
            {
                Publish(BuildStatusSnapshot(false, "displayHidden", "map footprints display is off", string.Empty));
                return;
            }

            if (gameState == null || !gameState.IsInWorld || gameState.IsInMainMenu)
            {
                Publish(BuildStatusSnapshot(true, "notInWorld", "map footprints draw cache only refreshes in world", string.Empty));
                return;
            }

            if (!ShouldRefresh(runtimeTick))
            {
                return;
            }

            PlayerWorldIdentityResolution identity;
            if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out identity) ||
                identity == null ||
                !identity.IsResolved ||
                string.IsNullOrWhiteSpace(identity.PairId))
            {
                Publish(BuildStatusSnapshot(true, "identityUnavailable", identity == null ? "identity unavailable" : identity.FailureReason, string.Empty));
                return;
            }

            PlayerWorldFootprintReadResult read;
            int stateSignature;
            var source = "memory";
            if (!PlayerWorldFootprintService.TryGetInMemoryForPair(identity.PairId, out read, out stateSignature))
            {
                read = PlayerWorldFootprintCache.ReadForPair(identity.PairId);
                stateSignature = PlayerWorldFootprintCache.LastStateSignature;
                source = read == null ? "fileCacheUnavailable" : "fileCache";
            }

            if (read != null)
            {
                read.DataSignature = stateSignature;
            }

            if (IsCurrentSnapshot(identity.PairId, stateSignature, displayEnabled))
            {
                return;
            }

            Publish(BuildSnapshot(read, displayEnabled, source, stateSignature));
        }

        public static MapFootprintRenderSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return _snapshot == null ? MapFootprintRenderSnapshot.Empty("notInitialized", "render cache not initialized") : _snapshot.Clone();
            }
        }

        internal static MapFootprintRenderSnapshot BuildSnapshotForTesting(
            PlayerWorldFootprintReadResult read,
            bool displayEnabled,
            string source,
            int dataSignature)
        {
            return BuildSnapshot(read, displayEnabled, source, dataSignature);
        }

        internal static MapFootprintDrawPlan BuildDrawPlanForTesting(
            MapFootprintRenderSnapshot snapshot,
            MapFootprintDrawTransform transform,
            Rectangle screen,
            int maxDrawnLines,
            float minDrawPixelStep)
        {
            return BuildDrawPlan(snapshot, transform, screen, maxDrawnLines, minDrawPixelStep);
        }

        internal static MapFootprintDrawPlan BuildDrawPlanForTesting(
            MapFootprintRenderSnapshot snapshot,
            MapFootprintDrawTransform transform,
            Rectangle screen,
            int maxDrawnLines,
            float minDrawPixelStep,
            long cursorTicks)
        {
            return BuildDrawPlan(snapshot, transform, screen, maxDrawnLines, minDrawPixelStep, cursorTicks);
        }

        internal static void PublishForTesting(MapFootprintRenderSnapshot snapshot)
        {
            Publish(snapshot);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = MapFootprintRenderSnapshot.Empty("notInitialized", "render cache not initialized");
                _nextRefreshTick = 0L;
                _lastPairId = string.Empty;
                _lastDataSignature = 0;
                _lastDisplayEnabled = false;
            }
        }

        internal static MapFootprintDrawPlan BuildDrawPlan(
            MapFootprintRenderSnapshot snapshot,
            MapFootprintDrawTransform transform,
            Rectangle screen,
            int maxDrawnLines,
            float minDrawPixelStep)
        {
            return BuildDrawPlan(snapshot, transform, screen, maxDrawnLines, minDrawPixelStep, long.MaxValue);
        }

        internal static MapFootprintDrawPlan BuildDrawPlan(
            MapFootprintRenderSnapshot snapshot,
            MapFootprintDrawTransform transform,
            Rectangle screen,
            int maxDrawnLines,
            float minDrawPixelStep,
            long cursorTicks)
        {
            var plan = new MapFootprintDrawPlan();
            if (snapshot == null || !snapshot.DisplayEnabled || snapshot.Lines == null || snapshot.Lines.Length == 0)
            {
                plan.Status = snapshot == null ? "cacheUnavailable" : snapshot.Status ?? string.Empty;
                plan.Message = snapshot == null ? "render cache unavailable" : snapshot.Message ?? string.Empty;
                plan.PairId = snapshot == null ? string.Empty : snapshot.PairId ?? string.Empty;
                plan.CursorTicks = snapshot == null ? 0L : ResolveCursorTicks(snapshot, cursorTicks);
                return plan;
            }

            var safeScreen = NormalizeScreen(screen);
            var clipScreen = NormalizeClipScreen(screen);
            var safeLimit = Math.Max(0, maxDrawnLines);
            var minStepSquared = Math.Max(0.25f, minDrawPixelStep * minDrawPixelStep);
            var resolvedCursorTicks = ResolveCursorTicks(snapshot, cursorTicks);
            var commands = new List<MapFootprintDrawCommand>(Math.Min(snapshot.Lines.Length, Math.Max(0, safeLimit)));
            var currentSegmentIndex = int.MinValue;
            var hasPendingStart = false;
            var pendingStart = Vector2.Zero;

            for (var index = 0; index < snapshot.Lines.Length; index++)
            {
                var line = snapshot.Lines[index];
                if (line == null)
                {
                    continue;
                }

                if (line.SegmentIndex != currentSegmentIndex)
                {
                    currentSegmentIndex = line.SegmentIndex;
                    hasPendingStart = false;
                }

                double endTileX;
                double endTileY;
                bool partialLine;
                if (!TryApplyCursorToLine(line, resolvedCursorTicks, out endTileX, out endTileY, out partialLine))
                {
                    plan.TimeSlicedLineCount++;
                    if (line.IsSegmentEnd)
                    {
                        hasPendingStart = false;
                    }

                    continue;
                }

                var start = transform.Project(line.StartTileX, line.StartTileY);
                var end = transform.Project(endTileX, endTileY);
                if (!IsFinite(start) || !IsFinite(end))
                {
                    plan.CulledLineCount++;
                    if (line.IsSegmentEnd)
                    {
                        hasPendingStart = false;
                    }

                    continue;
                }

                if (!hasPendingStart)
                {
                    pendingStart = start;
                    hasPendingStart = true;
                }

                var lengthSquared = Vector2.DistanceSquared(pendingStart, end);
                if (!line.IsSegmentEnd && lengthSquared < minStepSquared)
                {
                    plan.ThinnedLineCount++;
                    continue;
                }

                if (!LineBoundsIntersectsScreen(pendingStart, end, safeScreen))
                {
                    plan.CulledLineCount++;
                    pendingStart = end;
                    if (line.IsSegmentEnd)
                    {
                        hasPendingStart = false;
                    }

                    continue;
                }

                Vector2 clippedStart;
                Vector2 clippedEnd;
                if (!TryClipLineToScreen(pendingStart, end, clipScreen, out clippedStart, out clippedEnd))
                {
                    plan.CulledLineCount++;
                    pendingStart = end;
                    if (line.IsSegmentEnd)
                    {
                        hasPendingStart = false;
                    }

                    continue;
                }

                var clippedLengthSquared = Vector2.DistanceSquared(clippedStart, clippedEnd);
                if (clippedLengthSquared <= 0.01f)
                {
                    plan.CulledLineCount++;
                    pendingStart = end;
                    if (line.IsSegmentEnd)
                    {
                        hasPendingStart = false;
                    }

                    continue;
                }

                if (!line.IsSegmentEnd && clippedLengthSquared < minStepSquared)
                {
                    plan.ThinnedLineCount++;
                    pendingStart = end;
                    continue;
                }

                if (commands.Count >= safeLimit)
                {
                    plan.DrawLimitHit = true;
                    plan.DrawLimitSkippedLineCount = snapshot.Lines.Length - index;
                    break;
                }

                commands.Add(new MapFootprintDrawCommand
                {
                    Start = clippedStart,
                    End = clippedEnd,
                    SegmentIndex = line.SegmentIndex,
                    LineIndex = index,
                    StartTileX = line.StartTileX,
                    StartTileY = line.StartTileY,
                    EndTileX = endTileX,
                    EndTileY = endTileY,
                    PartialLine = partialLine
                });

                pendingStart = end;
                if (line.IsSegmentEnd || partialLine)
                {
                    hasPendingStart = false;
                }
            }

            plan.Status = "ready";
            plan.Message = "draw plan ready";
            plan.PairId = snapshot.PairId ?? string.Empty;
            plan.CachedLineCount = snapshot.LineCount;
            plan.DrawnLineCount = commands.Count;
            plan.CursorTicks = resolvedCursorTicks;
            plan.Commands = commands.ToArray();
            return plan;
        }

        private static bool ShouldRefresh(long runtimeTick)
        {
            var normalizedTick = Math.Max(0L, runtimeTick);
            lock (SyncRoot)
            {
                if (normalizedTick < _nextRefreshTick)
                {
                    return false;
                }

                _nextRefreshTick = normalizedTick + RefreshCadenceTicks;
                return true;
            }
        }

        private static bool IsCurrentSnapshot(string pairId, int dataSignature, bool displayEnabled)
        {
            lock (SyncRoot)
            {
                return _snapshot != null &&
                       _snapshot.DisplayEnabled == displayEnabled &&
                       _lastDisplayEnabled == displayEnabled &&
                       _lastDataSignature == dataSignature &&
                       string.Equals(_lastPairId, pairId ?? string.Empty, StringComparison.Ordinal);
            }
        }

        private static void Publish(MapFootprintRenderSnapshot snapshot)
        {
            snapshot = snapshot ?? MapFootprintRenderSnapshot.Empty("cacheUnavailable", "render cache unavailable");
            lock (SyncRoot)
            {
                _snapshot = snapshot.Clone();
                _lastPairId = _snapshot.PairId ?? string.Empty;
                _lastDataSignature = _snapshot.DataSignature;
                _lastDisplayEnabled = _snapshot.DisplayEnabled;
                if (!_snapshot.DisplayEnabled)
                {
                    _nextRefreshTick = 0L;
                }
            }

            PlayerWorldFootprintDiagnostics.RecordRenderCache(
                snapshot.Status,
                snapshot.Message,
                snapshot.PairId,
                snapshot.Source,
                snapshot.DisplayEnabled,
                snapshot.SegmentCount,
                snapshot.PointCount,
                snapshot.LineCount,
                snapshot.DataSignature,
                snapshot.CacheLimitHit);
        }

        private static MapFootprintRenderSnapshot BuildSnapshot(
            PlayerWorldFootprintReadResult read,
            bool displayEnabled,
            string source,
            int dataSignature)
        {
            if (!displayEnabled)
            {
                return BuildStatusSnapshot(false, "displayHidden", "map footprints display is off", string.Empty);
            }

            if (read == null)
            {
                return BuildStatusSnapshot(true, "readUnavailable", "footprint data unavailable", string.Empty);
            }

            var snapshot = new MapFootprintRenderSnapshot
            {
                DisplayEnabled = true,
                Source = source ?? string.Empty,
                PairId = read.PairId ?? string.Empty,
                WorldSizeX = read.WorldSizeX,
                WorldSizeY = read.WorldSizeY,
                TimelineStartTicks = read.TimelineStartTicks,
                TimelineEndTicks = read.TimelineEndTicks,
                SegmentCount = read.SegmentCount,
                PointCount = read.PointCount,
                DataSignature = dataSignature
            };

            if (!read.Succeeded)
            {
                snapshot.Status = string.IsNullOrWhiteSpace(read.Status) ? "readFailed" : read.Status;
                snapshot.Message = read.Message ?? string.Empty;
                snapshot.Lines = new MapFootprintRenderLine[0];
                return snapshot;
            }

            var lines = new List<MapFootprintRenderLine>();
            var renderedSegmentIndex = 0;
            var cacheLimitHit = false;
            for (var segmentIndex = 0; read.Segments != null && segmentIndex < read.Segments.Count; segmentIndex++)
            {
                var segment = read.Segments[segmentIndex];
                if (segment == null || segment.Points == null || segment.Points.Count < 2)
                {
                    continue;
                }

                var validPoints = new List<PlayerWorldFootprintPoint>(segment.Points.Count);
                for (var pointIndex = 0; pointIndex < segment.Points.Count; pointIndex++)
                {
                    var point = segment.Points[pointIndex];
                    if (IsValidPoint(point))
                    {
                        validPoints.Add(point);
                    }
                }

                if (validPoints.Count < 2)
                {
                    continue;
                }

                for (var pointIndex = 1; pointIndex < validPoints.Count; pointIndex++)
                {
                    if (lines.Count >= MaxCachedLines)
                    {
                        cacheLimitHit = true;
                        break;
                    }

                    var start = validPoints[pointIndex - 1];
                    var end = validPoints[pointIndex];
                    lines.Add(new MapFootprintRenderLine
                    {
                        SegmentIndex = renderedSegmentIndex,
                        StartTileX = start.TileX,
                        StartTileY = start.TileY,
                        EndTileX = end.TileX,
                        EndTileY = end.TileY,
                        StartTicks = start.StartTicks,
                        EndTicks = end.StartTicks,
                        IsSegmentEnd = pointIndex == validPoints.Count - 1
                    });
                }

                renderedSegmentIndex++;
                if (cacheLimitHit)
                {
                    break;
                }
            }

            snapshot.RenderedSegmentCount = renderedSegmentIndex;
            snapshot.LineCount = lines.Count;
            snapshot.CacheLimitHit = cacheLimitHit;
            ApplyLineTimelineFallback(snapshot, lines);
            snapshot.Status = lines.Count > 0 ? (cacheLimitHit ? "readyLimited" : "ready") : "empty";
            snapshot.Message = lines.Count > 0 ? "footprint render cache ready" : "no footprint lines to draw";
            snapshot.Lines = lines.ToArray();
            return snapshot;
        }

        private static MapFootprintRenderSnapshot BuildStatusSnapshot(
            bool displayEnabled,
            string status,
            string message,
            string pairId)
        {
            return new MapFootprintRenderSnapshot
            {
                DisplayEnabled = displayEnabled,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                PairId = pairId ?? string.Empty,
                Lines = new MapFootprintRenderLine[0]
            };
        }

        private static bool IsValidPoint(PlayerWorldFootprintPoint point)
        {
            if (point == null)
            {
                return false;
            }

            return !double.IsNaN(point.TileX) &&
                   !double.IsInfinity(point.TileX) &&
                   !double.IsNaN(point.TileY) &&
                   !double.IsInfinity(point.TileY);
        }

        private static void ApplyLineTimelineFallback(MapFootprintRenderSnapshot snapshot, IList<MapFootprintRenderLine> lines)
        {
            if (snapshot == null || lines == null || lines.Count <= 0)
            {
                return;
            }

            var minTicks = long.MaxValue;
            var maxTicks = 0L;
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (line == null)
                {
                    continue;
                }

                minTicks = Math.Min(minTicks, Math.Max(0L, line.StartTicks));
                maxTicks = Math.Max(maxTicks, Math.Max(line.StartTicks, line.EndTicks));
            }

            if (minTicks == long.MaxValue)
            {
                return;
            }

            if (snapshot.TimelineStartTicks <= 0L && snapshot.TimelineEndTicks <= 0L)
            {
                snapshot.TimelineStartTicks = minTicks;
                snapshot.TimelineEndTicks = maxTicks;
                return;
            }

            snapshot.TimelineStartTicks = Math.Min(Math.Max(0L, snapshot.TimelineStartTicks), minTicks);
            snapshot.TimelineEndTicks = Math.Max(Math.Max(snapshot.TimelineStartTicks, snapshot.TimelineEndTicks), maxTicks);
        }

        private static Rectangle NormalizeScreen(Rectangle screen)
        {
            return new Rectangle(
                screen.X - ViewportPaddingPixels,
                screen.Y - ViewportPaddingPixels,
                Math.Max(1, screen.Width) + ViewportPaddingPixels * 2,
                Math.Max(1, screen.Height) + ViewportPaddingPixels * 2);
        }

        private static Rectangle NormalizeClipScreen(Rectangle screen)
        {
            return new Rectangle(screen.X, screen.Y, Math.Max(1, screen.Width), Math.Max(1, screen.Height));
        }

        private static bool LineBoundsIntersectsScreen(Vector2 start, Vector2 end, Rectangle screen)
        {
            var minX = Math.Min(start.X, end.X);
            var minY = Math.Min(start.Y, end.Y);
            var maxX = Math.Max(start.X, end.X);
            var maxY = Math.Max(start.Y, end.Y);
            return maxX >= screen.Left &&
                   minX <= screen.Right &&
                   maxY >= screen.Top &&
                   minY <= screen.Bottom;
        }

        private static bool TryClipLineToScreen(Vector2 start, Vector2 end, Rectangle screen, out Vector2 clippedStart, out Vector2 clippedEnd)
        {
            clippedStart = start;
            clippedEnd = end;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var t0 = 0d;
            var t1 = 1d;

            // Draw commands are screen-space; keep the cached tile endpoints for diagnostics, but never rasterize outside the visible viewport.
            if (!ClipLineParameter(-dx, start.X - screen.Left, ref t0, ref t1) ||
                !ClipLineParameter(dx, screen.Right - start.X, ref t0, ref t1) ||
                !ClipLineParameter(-dy, start.Y - screen.Top, ref t0, ref t1) ||
                !ClipLineParameter(dy, screen.Bottom - start.Y, ref t0, ref t1))
            {
                return false;
            }

            if (t1 < t0)
            {
                return false;
            }

            clippedStart = new Vector2((float)(start.X + dx * t0), (float)(start.Y + dy * t0));
            clippedEnd = new Vector2((float)(start.X + dx * t1), (float)(start.Y + dy * t1));
            return IsFinite(clippedStart) && IsFinite(clippedEnd);
        }

        private static bool ClipLineParameter(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < 0.000001d)
            {
                return q >= 0d;
            }

            var r = q / p;
            if (p < 0d)
            {
                if (r > t1)
                {
                    return false;
                }

                if (r > t0)
                {
                    t0 = r;
                }

                return true;
            }

            if (r < t0)
            {
                return false;
            }

            if (r < t1)
            {
                t1 = r;
            }

            return true;
        }

        private static long ResolveCursorTicks(MapFootprintRenderSnapshot snapshot, long cursorTicks)
        {
            if (snapshot == null)
            {
                return 0L;
            }

            var start = Math.Max(0L, snapshot.TimelineStartTicks);
            var end = Math.Max(start, snapshot.TimelineEndTicks);
            if (cursorTicks == long.MaxValue)
            {
                return end;
            }

            if (cursorTicks < start)
            {
                return start;
            }

            return cursorTicks > end ? end : cursorTicks;
        }

        private static bool TryApplyCursorToLine(
            MapFootprintRenderLine line,
            long cursorTicks,
            out double endTileX,
            out double endTileY,
            out bool partialLine)
        {
            endTileX = line == null ? 0d : line.EndTileX;
            endTileY = line == null ? 0d : line.EndTileY;
            partialLine = false;
            if (line == null)
            {
                return false;
            }

            if (cursorTicks == long.MaxValue || cursorTicks >= line.EndTicks || line.EndTicks <= line.StartTicks)
            {
                return cursorTicks > line.StartTicks || cursorTicks == long.MaxValue;
            }

            if (cursorTicks <= line.StartTicks)
            {
                return false;
            }

            var denominator = Math.Max(1d, line.EndTicks - line.StartTicks);
            var fraction = (cursorTicks - line.StartTicks) / denominator;
            fraction = Math.Max(0d, Math.Min(1d, fraction));
            endTileX = line.StartTileX + (line.EndTileX - line.StartTileX) * fraction;
            endTileY = line.StartTileY + (line.EndTileY - line.StartTileY) * fraction;
            partialLine = true;
            return true;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.X) &&
                   !float.IsInfinity(value.X) &&
                   !float.IsNaN(value.Y) &&
                   !float.IsInfinity(value.Y);
        }
    }

    internal sealed class MapFootprintRenderSnapshot
    {
        public bool DisplayEnabled { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public string PairId { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
        public long TimelineStartTicks { get; set; }
        public long TimelineEndTicks { get; set; }
        public int SegmentCount { get; set; }
        public int RenderedSegmentCount { get; set; }
        public int PointCount { get; set; }
        public int LineCount { get; set; }
        public int DataSignature { get; set; }
        public bool CacheLimitHit { get; set; }
        public MapFootprintRenderLine[] Lines { get; set; }

        public MapFootprintRenderSnapshot()
        {
            Status = string.Empty;
            Message = string.Empty;
            Source = string.Empty;
            PairId = string.Empty;
            Lines = new MapFootprintRenderLine[0];
        }

        public static MapFootprintRenderSnapshot Empty(string status, string message)
        {
            return new MapFootprintRenderSnapshot
            {
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                Lines = new MapFootprintRenderLine[0]
            };
        }

        public MapFootprintRenderSnapshot Clone()
        {
            var clone = new MapFootprintRenderSnapshot
            {
                DisplayEnabled = DisplayEnabled,
                Status = Status ?? string.Empty,
                Message = Message ?? string.Empty,
                Source = Source ?? string.Empty,
                PairId = PairId ?? string.Empty,
                WorldSizeX = WorldSizeX,
                WorldSizeY = WorldSizeY,
                TimelineStartTicks = TimelineStartTicks,
                TimelineEndTicks = TimelineEndTicks,
                SegmentCount = SegmentCount,
                RenderedSegmentCount = RenderedSegmentCount,
                PointCount = PointCount,
                LineCount = LineCount,
                DataSignature = DataSignature,
                CacheLimitHit = CacheLimitHit,
                Lines = new MapFootprintRenderLine[Lines == null ? 0 : Lines.Length]
            };

            for (var index = 0; Lines != null && index < Lines.Length; index++)
            {
                clone.Lines[index] = Lines[index] == null ? null : Lines[index].Clone();
            }

            return clone;
        }
    }

    internal sealed class MapFootprintRenderLine
    {
        public int SegmentIndex { get; set; }
        public double StartTileX { get; set; }
        public double StartTileY { get; set; }
        public double EndTileX { get; set; }
        public double EndTileY { get; set; }
        public long StartTicks { get; set; }
        public long EndTicks { get; set; }
        public bool IsSegmentEnd { get; set; }

        public MapFootprintRenderLine Clone()
        {
            return new MapFootprintRenderLine
            {
                SegmentIndex = SegmentIndex,
                StartTileX = StartTileX,
                StartTileY = StartTileY,
                EndTileX = EndTileX,
                EndTileY = EndTileY,
                StartTicks = StartTicks,
                EndTicks = EndTicks,
                IsSegmentEnd = IsSegmentEnd
            };
        }
    }

    internal struct MapFootprintDrawTransform
    {
        public Vector2 MapPosition { get; set; }
        public Vector2 MapOffset { get; set; }
        public float MapScale { get; set; }
        public float Opacity { get; set; }

        public Vector2 Project(double tileX, double tileY)
        {
            return (new Vector2((float)tileX, (float)tileY) - MapPosition) * MapScale + MapOffset;
        }
    }

    internal sealed class MapFootprintDrawPlan
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public int CachedLineCount { get; set; }
        public int DrawnLineCount { get; set; }
        public int CulledLineCount { get; set; }
        public int ThinnedLineCount { get; set; }
        public int TimeSlicedLineCount { get; set; }
        public int DrawLimitSkippedLineCount { get; set; }
        public bool DrawLimitHit { get; set; }
        public long CursorTicks { get; set; }
        public MapFootprintDrawCommand[] Commands { get; set; }

        public MapFootprintDrawPlan()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            Commands = new MapFootprintDrawCommand[0];
        }
    }

    internal sealed class MapFootprintDrawCommand
    {
        public Vector2 Start { get; set; }
        public Vector2 End { get; set; }
        public int SegmentIndex { get; set; }
        public int LineIndex { get; set; }
        public double StartTileX { get; set; }
        public double StartTileY { get; set; }
        public double EndTileX { get; set; }
        public double EndTileY { get; set; }
        public bool PartialLine { get; set; }
    }
}
