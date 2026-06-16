using System;
using JueMingZ.Records;

namespace JueMingZ.Diagnostics
{
    internal static class PlayerWorldFootprintDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static PlayerWorldFootprintDiagnosticsSnapshot _snapshot = new PlayerWorldFootprintDiagnosticsSnapshot();

        public static void RecordRead(PlayerWorldFootprintReadResult result)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = result == null ? string.Empty : result.Status ?? string.Empty;
                _snapshot.LastMessage = result == null ? string.Empty : result.Message ?? string.Empty;
                _snapshot.LastPairId = result == null ? string.Empty : result.PairId ?? string.Empty;
                _snapshot.IdentityResolved = result != null && result.IdentityResolved;
                _snapshot.ReadFailed = result != null && result.ReadFailed;
                _snapshot.WriteFailed = result != null && result.WriteFailed;
                _snapshot.RetentionTrimmed = result != null && result.RetentionTrimmed;
                _snapshot.SegmentCount = result == null ? 0 : result.SegmentCount;
                _snapshot.PointCount = result == null ? 0 : result.PointCount;
                _snapshot.TimelineStartTicks = result == null ? 0L : result.TimelineStartTicks;
                _snapshot.TimelineEndTicks = result == null ? 0L : result.TimelineEndTicks;
                _snapshot.LastReadUtc = result == null ? (DateTime?)null : result.LastReadUtc;
            }
        }

        public static void RecordWrite(PlayerWorldFootprintWriteResult result)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = result == null ? string.Empty : result.Status ?? string.Empty;
                _snapshot.LastMessage = result == null ? string.Empty : result.Message ?? string.Empty;
                _snapshot.LastPairId = result == null ? string.Empty : result.PairId ?? string.Empty;
                _snapshot.IdentityResolved = result != null && result.IdentityResolved;
                _snapshot.WriteFailed = result != null && !result.Succeeded;
                _snapshot.RetentionTrimmed = result != null && result.RetentionTrimmed;
                _snapshot.SegmentCount = result == null ? 0 : result.SegmentCount;
                _snapshot.PointCount = result == null ? 0 : result.PointCount;
                _snapshot.LastWriteUtc = result == null ? (DateTime?)null : result.LastWriteUtc;
            }
        }

        public static void RecordRuntime(PlayerWorldFootprintRecordResult result)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = result == null ? string.Empty : result.Status ?? string.Empty;
                _snapshot.LastDecision = result == null ? string.Empty : result.Decision ?? string.Empty;
                _snapshot.LastMessage = result == null ? string.Empty : result.Message ?? string.Empty;
                _snapshot.LastPairId = result == null ? string.Empty : result.PairId ?? string.Empty;
                _snapshot.IdentityResolved = result != null && result.IdentityResolved;
                _snapshot.IsRecording = result != null && result.Succeeded && result.IdentityResolved;
                _snapshot.WriteFailed = result != null && result.WriteFailed;
                _snapshot.RetentionTrimmed = result != null && result.RetentionTrimmed;
                _snapshot.SegmentCount = result == null ? 0 : result.SegmentCount;
                _snapshot.PointCount = result == null ? 0 : result.PointCount;
                _snapshot.TimelineStartTicks = result == null ? 0L : result.TimelineStartTicks;
                _snapshot.TimelineEndTicks = result == null ? 0L : result.TimelineEndTicks;
                _snapshot.BreakCount = result == null ? 0 : Math.Max(0, result.SegmentCount - 1);
                _snapshot.LastPointTileX = result == null ? 0d : result.LastPointTileX;
                _snapshot.LastPointTileY = result == null ? 0d : result.LastPointTileY;
                _snapshot.LastPointDurationTicks = result == null ? 0L : result.LastPointDurationTicks;
                _snapshot.LastRecordRuntimeTick = result == null ? 0L : result.LastRecordRuntimeTick;
                _snapshot.LastRecordUtc = result == null ? (DateTime?)null : result.LastRecordUtc;
                _snapshot.LastFlushStatus = result != null && result.Flushed ? "saved" : (result != null && result.WriteFailed ? "writeFailed" : "pending");
                _snapshot.LastWriteUtc = result == null ? (DateTime?)null : result.LastWriteUtc;
            }
        }

        public static void RecordRenderCache(
            string status,
            string message,
            string pairId,
            string source,
            bool displayEnabled,
            int segmentCount,
            int pointCount,
            int lineCount,
            int dataSignature,
            bool cacheLimitHit)
        {
            lock (SyncRoot)
            {
                _snapshot.MapFootprintsRenderCacheStatus = status ?? string.Empty;
                _snapshot.MapFootprintsRenderCacheMessage = message ?? string.Empty;
                _snapshot.MapFootprintsRenderCachePairId = pairId ?? string.Empty;
                _snapshot.MapFootprintsRenderCacheSource = source ?? string.Empty;
                _snapshot.MapFootprintsDisplayEnabled = displayEnabled;
                _snapshot.MapFootprintsRenderCacheSegmentCount = segmentCount;
                _snapshot.MapFootprintsRenderCachePointCount = pointCount;
                _snapshot.MapFootprintsRenderCacheLineCount = lineCount;
                _snapshot.MapFootprintsRenderCacheDataSignature = dataSignature;
                _snapshot.MapFootprintsRenderCacheLimitHit = cacheLimitHit;
            }
        }

        public static void RecordMapDraw(
            string status,
            string message,
            string pairId,
            int cachedLineCount,
            int drawnLineCount,
            int culledLineCount,
            int thinnedLineCount,
            int drawLimitSkippedLineCount,
            bool drawLimitHit)
        {
            lock (SyncRoot)
            {
                _snapshot.MapFootprintsLastDrawStatus = status ?? string.Empty;
                _snapshot.MapFootprintsLastDrawMessage = message ?? string.Empty;
                _snapshot.MapFootprintsLastDrawPairId = pairId ?? string.Empty;
                _snapshot.MapFootprintsCachedLineCount = cachedLineCount;
                _snapshot.MapFootprintsDrawnLineCount = drawnLineCount;
                _snapshot.MapFootprintsCulledLineCount = culledLineCount;
                _snapshot.MapFootprintsThinnedLineCount = thinnedLineCount;
                _snapshot.MapFootprintsDrawLimitSkippedLineCount = drawLimitSkippedLineCount;
                _snapshot.MapFootprintsDrawLimitHit = drawLimitHit;
                _snapshot.MapFootprintsLastDrawUtc = DateTime.UtcNow;
            }
        }

        public static void RecordPlaybackOverlay(
            string status,
            string message,
            string pairId,
            bool paused,
            int playbackRate,
            long cursorTicks,
            long timelineStartTicks,
            long latestTicks,
            bool atLatest,
            bool dragging,
            bool mouseCaptured,
            bool barHovered,
            string interaction)
        {
            lock (SyncRoot)
            {
                _snapshot.MapFootprintsPlaybackOverlayStatus = status ?? string.Empty;
                _snapshot.MapFootprintsPlaybackOverlayMessage = message ?? string.Empty;
                _snapshot.MapFootprintsPlaybackPairId = pairId ?? string.Empty;
                _snapshot.MapFootprintsPlaybackPaused = paused;
                _snapshot.MapFootprintsPlaybackRate = playbackRate;
                _snapshot.MapFootprintsPlaybackCursorTicks = cursorTicks;
                _snapshot.MapFootprintsPlaybackTimelineStartTicks = timelineStartTicks;
                _snapshot.MapFootprintsPlaybackLatestTicks = latestTicks;
                _snapshot.MapFootprintsPlaybackProgress = CalculatePlaybackProgress(timelineStartTicks, latestTicks, cursorTicks);
                _snapshot.MapFootprintsPlaybackAtLatest = atLatest;
                _snapshot.MapFootprintsPlaybackDragging = dragging;
                _snapshot.MapFootprintsPlaybackMouseCaptured = mouseCaptured;
                _snapshot.MapFootprintsPlaybackBarHovered = barHovered;
                _snapshot.MapFootprintsPlaybackLastInteraction = interaction ?? string.Empty;
                _snapshot.MapFootprintsPlaybackLastUpdateUtc = DateTime.UtcNow;
            }
        }

        internal static PlayerWorldFootprintDiagnosticsSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return _snapshot.Clone();
            }
        }

        internal static PlayerWorldFootprintDiagnosticsSnapshot GetSnapshotForTesting()
        {
            return GetSnapshot();
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = new PlayerWorldFootprintDiagnosticsSnapshot();
            }
        }

        private static double CalculatePlaybackProgress(long timelineStartTicks, long latestTicks, long cursorTicks)
        {
            var span = latestTicks - timelineStartTicks;
            if (span <= 0L)
            {
                return 1d;
            }

            var clamped = Math.Max(timelineStartTicks, Math.Min(latestTicks, cursorTicks));
            return (clamped - timelineStartTicks) / (double)span;
        }
    }

    internal sealed class PlayerWorldFootprintDiagnosticsSnapshot
    {
        public string LastStatus { get; set; }
        public string LastDecision { get; set; }
        public string LastMessage { get; set; }
        public string LastPairId { get; set; }
        public bool IdentityResolved { get; set; }
        public bool IsRecording { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public bool RetentionTrimmed { get; set; }
        public int SegmentCount { get; set; }
        public int PointCount { get; set; }
        public int BreakCount { get; set; }
        public long TimelineStartTicks { get; set; }
        public long TimelineEndTicks { get; set; }
        public double LastPointTileX { get; set; }
        public double LastPointTileY { get; set; }
        public long LastPointDurationTicks { get; set; }
        public long LastRecordRuntimeTick { get; set; }
        public string LastFlushStatus { get; set; }
        public DateTime? LastReadUtc { get; set; }
        public DateTime? LastRecordUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }
        public bool MapFootprintsDisplayEnabled { get; set; }
        public string MapFootprintsRenderCacheStatus { get; set; }
        public string MapFootprintsRenderCacheMessage { get; set; }
        public string MapFootprintsRenderCachePairId { get; set; }
        public string MapFootprintsRenderCacheSource { get; set; }
        public int MapFootprintsRenderCacheSegmentCount { get; set; }
        public int MapFootprintsRenderCachePointCount { get; set; }
        public int MapFootprintsRenderCacheLineCount { get; set; }
        public int MapFootprintsRenderCacheDataSignature { get; set; }
        public bool MapFootprintsRenderCacheLimitHit { get; set; }
        public string MapFootprintsLastDrawStatus { get; set; }
        public string MapFootprintsLastDrawMessage { get; set; }
        public string MapFootprintsLastDrawPairId { get; set; }
        public int MapFootprintsCachedLineCount { get; set; }
        public int MapFootprintsDrawnLineCount { get; set; }
        public int MapFootprintsCulledLineCount { get; set; }
        public int MapFootprintsThinnedLineCount { get; set; }
        public int MapFootprintsDrawLimitSkippedLineCount { get; set; }
        public bool MapFootprintsDrawLimitHit { get; set; }
        public DateTime? MapFootprintsLastDrawUtc { get; set; }
        public string MapFootprintsPlaybackOverlayStatus { get; set; }
        public string MapFootprintsPlaybackOverlayMessage { get; set; }
        public string MapFootprintsPlaybackPairId { get; set; }
        public bool MapFootprintsPlaybackPaused { get; set; }
        public int MapFootprintsPlaybackRate { get; set; }
        public long MapFootprintsPlaybackCursorTicks { get; set; }
        public long MapFootprintsPlaybackTimelineStartTicks { get; set; }
        public long MapFootprintsPlaybackLatestTicks { get; set; }
        public double MapFootprintsPlaybackProgress { get; set; }
        public bool MapFootprintsPlaybackAtLatest { get; set; }
        public bool MapFootprintsPlaybackDragging { get; set; }
        public bool MapFootprintsPlaybackMouseCaptured { get; set; }
        public bool MapFootprintsPlaybackBarHovered { get; set; }
        public string MapFootprintsPlaybackLastInteraction { get; set; }
        public DateTime? MapFootprintsPlaybackLastUpdateUtc { get; set; }

        public PlayerWorldFootprintDiagnosticsSnapshot()
        {
            LastStatus = string.Empty;
            LastDecision = string.Empty;
            LastMessage = string.Empty;
            LastPairId = string.Empty;
            LastFlushStatus = string.Empty;
            MapFootprintsRenderCacheStatus = string.Empty;
            MapFootprintsRenderCacheMessage = string.Empty;
            MapFootprintsRenderCachePairId = string.Empty;
            MapFootprintsRenderCacheSource = string.Empty;
            MapFootprintsLastDrawStatus = string.Empty;
            MapFootprintsLastDrawMessage = string.Empty;
            MapFootprintsLastDrawPairId = string.Empty;
            MapFootprintsPlaybackOverlayStatus = string.Empty;
            MapFootprintsPlaybackOverlayMessage = string.Empty;
            MapFootprintsPlaybackPairId = string.Empty;
            MapFootprintsPlaybackRate = 1;
            MapFootprintsPlaybackProgress = 1d;
            MapFootprintsPlaybackAtLatest = true;
            MapFootprintsPlaybackLastInteraction = string.Empty;
        }

        public PlayerWorldFootprintDiagnosticsSnapshot Clone()
        {
            return new PlayerWorldFootprintDiagnosticsSnapshot
            {
                LastStatus = LastStatus ?? string.Empty,
                LastDecision = LastDecision ?? string.Empty,
                LastMessage = LastMessage ?? string.Empty,
                LastPairId = LastPairId ?? string.Empty,
                IdentityResolved = IdentityResolved,
                IsRecording = IsRecording,
                ReadFailed = ReadFailed,
                WriteFailed = WriteFailed,
                RetentionTrimmed = RetentionTrimmed,
                SegmentCount = SegmentCount,
                PointCount = PointCount,
                BreakCount = BreakCount,
                TimelineStartTicks = TimelineStartTicks,
                TimelineEndTicks = TimelineEndTicks,
                LastPointTileX = LastPointTileX,
                LastPointTileY = LastPointTileY,
                LastPointDurationTicks = LastPointDurationTicks,
                LastRecordRuntimeTick = LastRecordRuntimeTick,
                LastFlushStatus = LastFlushStatus ?? string.Empty,
                LastReadUtc = LastReadUtc,
                LastRecordUtc = LastRecordUtc,
                LastWriteUtc = LastWriteUtc,
                MapFootprintsDisplayEnabled = MapFootprintsDisplayEnabled,
                MapFootprintsRenderCacheStatus = MapFootprintsRenderCacheStatus ?? string.Empty,
                MapFootprintsRenderCacheMessage = MapFootprintsRenderCacheMessage ?? string.Empty,
                MapFootprintsRenderCachePairId = MapFootprintsRenderCachePairId ?? string.Empty,
                MapFootprintsRenderCacheSource = MapFootprintsRenderCacheSource ?? string.Empty,
                MapFootprintsRenderCacheSegmentCount = MapFootprintsRenderCacheSegmentCount,
                MapFootprintsRenderCachePointCount = MapFootprintsRenderCachePointCount,
                MapFootprintsRenderCacheLineCount = MapFootprintsRenderCacheLineCount,
                MapFootprintsRenderCacheDataSignature = MapFootprintsRenderCacheDataSignature,
                MapFootprintsRenderCacheLimitHit = MapFootprintsRenderCacheLimitHit,
                MapFootprintsLastDrawStatus = MapFootprintsLastDrawStatus ?? string.Empty,
                MapFootprintsLastDrawMessage = MapFootprintsLastDrawMessage ?? string.Empty,
                MapFootprintsLastDrawPairId = MapFootprintsLastDrawPairId ?? string.Empty,
                MapFootprintsCachedLineCount = MapFootprintsCachedLineCount,
                MapFootprintsDrawnLineCount = MapFootprintsDrawnLineCount,
                MapFootprintsCulledLineCount = MapFootprintsCulledLineCount,
                MapFootprintsThinnedLineCount = MapFootprintsThinnedLineCount,
                MapFootprintsDrawLimitSkippedLineCount = MapFootprintsDrawLimitSkippedLineCount,
                MapFootprintsDrawLimitHit = MapFootprintsDrawLimitHit,
                MapFootprintsLastDrawUtc = MapFootprintsLastDrawUtc,
                MapFootprintsPlaybackOverlayStatus = MapFootprintsPlaybackOverlayStatus ?? string.Empty,
                MapFootprintsPlaybackOverlayMessage = MapFootprintsPlaybackOverlayMessage ?? string.Empty,
                MapFootprintsPlaybackPairId = MapFootprintsPlaybackPairId ?? string.Empty,
                MapFootprintsPlaybackPaused = MapFootprintsPlaybackPaused,
                MapFootprintsPlaybackRate = MapFootprintsPlaybackRate,
                MapFootprintsPlaybackCursorTicks = MapFootprintsPlaybackCursorTicks,
                MapFootprintsPlaybackTimelineStartTicks = MapFootprintsPlaybackTimelineStartTicks,
                MapFootprintsPlaybackLatestTicks = MapFootprintsPlaybackLatestTicks,
                MapFootprintsPlaybackProgress = MapFootprintsPlaybackProgress,
                MapFootprintsPlaybackAtLatest = MapFootprintsPlaybackAtLatest,
                MapFootprintsPlaybackDragging = MapFootprintsPlaybackDragging,
                MapFootprintsPlaybackMouseCaptured = MapFootprintsPlaybackMouseCaptured,
                MapFootprintsPlaybackBarHovered = MapFootprintsPlaybackBarHovered,
                MapFootprintsPlaybackLastInteraction = MapFootprintsPlaybackLastInteraction ?? string.Empty,
                MapFootprintsPlaybackLastUpdateUtc = MapFootprintsPlaybackLastUpdateUtc
            };
        }
    }
}
