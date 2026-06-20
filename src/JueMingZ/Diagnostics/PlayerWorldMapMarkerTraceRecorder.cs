using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using JueMingZ.Config;

namespace JueMingZ.Diagnostics
{
    internal sealed class PlayerWorldMapMarkerTraceEvent
    {
        public DateTime UtcNow { get; set; }
        public string RuntimeVersion { get; set; }
        public string EventType { get; set; }
        public string PairId { get; set; }
        public string MarkerId { get; set; }
        public int IconItemId { get; set; }
        public bool WriteAttempted { get; set; }
        public bool WriteSucceeded { get; set; }
        public string WriteStatus { get; set; }
        public string WriteMessage { get; set; }
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
        public bool DrawAttempted { get; set; }
        public bool DrawVisible { get; set; }
        public int DrawScreenWidth { get; set; }
        public int DrawScreenHeight { get; set; }
        public int DrawRegionX { get; set; }
        public int DrawRegionY { get; set; }
        public int DrawRegionWidth { get; set; }
        public int DrawRegionHeight { get; set; }
        public float DrawCenterScreenX { get; set; }
        public float DrawCenterScreenY { get; set; }
        public float DrawDeltaFromRightClickX { get; set; }
        public float DrawDeltaFromRightClickY { get; set; }
        public string DrawSkippedReason { get; set; }

        public PlayerWorldMapMarkerTraceEvent()
        {
            RuntimeVersion = string.Empty;
            EventType = string.Empty;
            PairId = string.Empty;
            MarkerId = string.Empty;
            IconItemId = -1;
            WriteStatus = string.Empty;
            WriteMessage = string.Empty;
            TransformSource = string.Empty;
            FallbackReason = string.Empty;
            CurrentGameUpdateCount = -1;
            TransformAgeUpdates = -1;
            DrawSkippedReason = string.Empty;
        }
    }

    internal static class PlayerWorldMapMarkerTraceRecorder
    {
        private static readonly object SyncRoot = new object();
        private static readonly object WriteQueueSyncRoot = new object();
        private static readonly System.Collections.Generic.Queue<PendingMapMarkerTraceWrite> PendingWrites =
            new System.Collections.Generic.Queue<PendingMapMarkerTraceWrite>();
        private const int MaxPendingWrites = 256;
        private const int MaxPendingDrawSamples = 256;
        private static readonly Dictionary<string, PlayerWorldMapMarkerTraceEvent> PendingDrawSamples =
            new Dictionary<string, PlayerWorldMapMarkerTraceEvent>(StringComparer.Ordinal);
        private static readonly HashSet<string> RecordedDrawMarkerIds = new HashSet<string>(StringComparer.Ordinal);
        private static DateTime _lastWriteFailureUtc = DateTime.MinValue;
        private static DateTime? _lastTraceEventWrittenAtUtc;
        private static string _lastTraceEventType = string.Empty;
        private static string _lastTraceMarkerId = string.Empty;
        private static bool _writeWorkerScheduled;

        public static string TraceEventsPath
        {
            get
            {
                return Path.Combine(
                    DiagnosticSnapshotWriter.DiagnosticsDirectory,
                    "map-marker-events-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");
            }
        }

        public static string TraceEventsPathForSnapshot
        {
            get { return TraceRecordingEnabled ? TraceEventsPath : string.Empty; }
        }

        public static bool TraceRecordingEnabled
        {
            get
            {
                var settings = ConfigService.AppSettings;
                return settings != null && settings.EnableTraceLog;
            }
        }

        public static DateTime? LastTraceEventWrittenAtUtc
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastTraceEventWrittenAtUtc;
                }
            }
        }

        public static string LastTraceEventType
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastTraceEventType;
                }
            }
        }

        public static string LastTraceMarkerId
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastTraceMarkerId;
                }
            }
        }

        public static void Record(PlayerWorldMapMarkerTraceEvent sample)
        {
            if (sample == null)
            {
                return;
            }

            if (!TraceRecordingEnabled)
            {
                return;
            }

            try
            {
                if (sample.UtcNow == default(DateTime))
                {
                    sample.UtcNow = DateTime.UtcNow;
                }

                RememberPendingDrawIfNeeded(sample);
                EnqueueWrite(TraceEventsPath, BuildEventJson(sample), sample.EventType, sample.MarkerId);
            }
            catch (Exception error)
            {
                if (DateTime.UtcNow - _lastWriteFailureUtc > TimeSpan.FromSeconds(30))
                {
                    _lastWriteFailureUtc = DateTime.UtcNow;
                    Logger.Warn("PlayerWorldMapMarkerTraceRecorder", "Map marker trace write failed: " + error.Message);
                }
            }
        }

        public static void RecordDrawIfPending(
            string markerId,
            int tileX,
            int tileY,
            int iconItemId,
            int drawRegionX,
            int drawRegionY,
            int drawRegionWidth,
            int drawRegionHeight,
            int screenWidth,
            int screenHeight,
            bool visible,
            string skippedReason)
        {
            if (!TraceRecordingEnabled)
            {
                return;
            }

            PlayerWorldMapMarkerTraceEvent drawSample;
            lock (SyncRoot)
            {
                drawSample = TryBuildPendingDrawEvent(
                    markerId,
                    tileX,
                    tileY,
                    iconItemId,
                    drawRegionX,
                    drawRegionY,
                    drawRegionWidth,
                    drawRegionHeight,
                    screenWidth,
                    screenHeight,
                    visible,
                    skippedReason);
            }

            if (drawSample != null)
            {
                Record(drawSample);
            }
        }

        internal static string BuildEventJsonForTesting(PlayerWorldMapMarkerTraceEvent sample)
        {
            return BuildEventJson(sample);
        }

        internal static PlayerWorldMapMarkerTraceEvent BuildDrawEventForTesting(
            PlayerWorldMapMarkerTraceEvent createSample,
            int drawRegionX,
            int drawRegionY,
            int drawRegionWidth,
            int drawRegionHeight,
            int screenWidth,
            int screenHeight,
            bool visible,
            string skippedReason)
        {
            return BuildDrawEvent(
                createSample,
                createSample == null ? string.Empty : createSample.MarkerId,
                createSample == null ? 0 : createSample.TileX,
                createSample == null ? 0 : createSample.TileY,
                createSample == null ? -1 : createSample.IconItemId,
                drawRegionX,
                drawRegionY,
                drawRegionWidth,
                drawRegionHeight,
                screenWidth,
                screenHeight,
                visible,
                skippedReason);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _lastTraceEventWrittenAtUtc = null;
                _lastTraceEventType = string.Empty;
                _lastTraceMarkerId = string.Empty;
                PendingDrawSamples.Clear();
                RecordedDrawMarkerIds.Clear();
            }

            lock (WriteQueueSyncRoot)
            {
                PendingWrites.Clear();
                _writeWorkerScheduled = false;
            }
        }

        private static void RememberPendingDrawIfNeeded(PlayerWorldMapMarkerTraceEvent sample)
        {
            if (sample == null ||
                !string.Equals(sample.EventType, "markerCreate", StringComparison.Ordinal) ||
                !sample.WriteSucceeded ||
                string.IsNullOrWhiteSpace(sample.MarkerId))
            {
                return;
            }

            lock (SyncRoot)
            {
                if (RecordedDrawMarkerIds.Contains(sample.MarkerId))
                {
                    return;
                }

                if (PendingDrawSamples.Count >= MaxPendingDrawSamples)
                {
                    string firstKey = null;
                    foreach (var key in PendingDrawSamples.Keys)
                    {
                        firstKey = key;
                        break;
                    }

                    if (firstKey != null)
                    {
                        PendingDrawSamples.Remove(firstKey);
                    }
                }

                PendingDrawSamples[sample.MarkerId] = CloneTraceEvent(sample);
            }
        }

        private static PlayerWorldMapMarkerTraceEvent TryBuildPendingDrawEvent(
            string markerId,
            int tileX,
            int tileY,
            int iconItemId,
            int drawRegionX,
            int drawRegionY,
            int drawRegionWidth,
            int drawRegionHeight,
            int screenWidth,
            int screenHeight,
            bool visible,
            string skippedReason)
        {
            if (string.IsNullOrWhiteSpace(markerId) || RecordedDrawMarkerIds.Contains(markerId))
            {
                return null;
            }

            PlayerWorldMapMarkerTraceEvent createSample;
            if (!PendingDrawSamples.TryGetValue(markerId, out createSample))
            {
                return null;
            }

            PendingDrawSamples.Remove(markerId);
            RecordedDrawMarkerIds.Add(markerId);
            return BuildDrawEvent(
                createSample,
                markerId,
                tileX,
                tileY,
                iconItemId,
                drawRegionX,
                drawRegionY,
                drawRegionWidth,
                drawRegionHeight,
                screenWidth,
                screenHeight,
                visible,
                skippedReason);
        }

        private static PlayerWorldMapMarkerTraceEvent BuildDrawEvent(
            PlayerWorldMapMarkerTraceEvent createSample,
            string markerId,
            int tileX,
            int tileY,
            int iconItemId,
            int drawRegionX,
            int drawRegionY,
            int drawRegionWidth,
            int drawRegionHeight,
            int screenWidth,
            int screenHeight,
            bool visible,
            string skippedReason)
        {
            if (createSample == null)
            {
                return null;
            }

            var drawCenterX = drawRegionX + drawRegionWidth / 2f;
            var drawCenterY = drawRegionY + drawRegionHeight / 2f;
            var sample = CloneTraceEvent(createSample);
            sample.UtcNow = DateTime.UtcNow;
            sample.EventType = "markerDraw";
            sample.MarkerId = markerId ?? string.Empty;
            sample.IconItemId = iconItemId;
            sample.TileX = tileX;
            sample.TileY = tileY;
            sample.DrawAttempted = true;
            sample.DrawVisible = visible;
            sample.DrawScreenWidth = Math.Max(1, screenWidth);
            sample.DrawScreenHeight = Math.Max(1, screenHeight);
            sample.DrawRegionX = drawRegionX;
            sample.DrawRegionY = drawRegionY;
            sample.DrawRegionWidth = drawRegionWidth;
            sample.DrawRegionHeight = drawRegionHeight;
            sample.DrawCenterScreenX = drawCenterX;
            sample.DrawCenterScreenY = drawCenterY;
            sample.DrawDeltaFromRightClickX = drawCenterX - createSample.ScreenX;
            sample.DrawDeltaFromRightClickY = drawCenterY - createSample.ScreenY;
            sample.DrawSkippedReason = visible ? string.Empty : (skippedReason ?? string.Empty);
            return sample;
        }

        private static PlayerWorldMapMarkerTraceEvent CloneTraceEvent(PlayerWorldMapMarkerTraceEvent sample)
        {
            if (sample == null)
            {
                return null;
            }

            return new PlayerWorldMapMarkerTraceEvent
            {
                UtcNow = sample.UtcNow,
                RuntimeVersion = sample.RuntimeVersion ?? string.Empty,
                EventType = sample.EventType ?? string.Empty,
                PairId = sample.PairId ?? string.Empty,
                MarkerId = sample.MarkerId ?? string.Empty,
                IconItemId = sample.IconItemId,
                WriteAttempted = sample.WriteAttempted,
                WriteSucceeded = sample.WriteSucceeded,
                WriteStatus = sample.WriteStatus ?? string.Empty,
                WriteMessage = sample.WriteMessage ?? string.Empty,
                TileX = sample.TileX,
                TileY = sample.TileY,
                ScreenX = sample.ScreenX,
                ScreenY = sample.ScreenY,
                ScreenWidth = sample.ScreenWidth,
                ScreenHeight = sample.ScreenHeight,
                WorldSizeX = sample.WorldSizeX,
                WorldSizeY = sample.WorldSizeY,
                TransformSource = sample.TransformSource ?? string.Empty,
                FallbackReason = sample.FallbackReason ?? string.Empty,
                MapTopLeftX = sample.MapTopLeftX,
                MapTopLeftY = sample.MapTopLeftY,
                MapScale = sample.MapScale,
                CurrentMapFullscreenPosX = sample.CurrentMapFullscreenPosX,
                CurrentMapFullscreenPosY = sample.CurrentMapFullscreenPosY,
                CurrentMapScale = sample.CurrentMapScale,
                CurrentGameUpdateCount = sample.CurrentGameUpdateCount,
                TransformAgeUpdates = sample.TransformAgeUpdates,
                DrawAttempted = sample.DrawAttempted,
                DrawVisible = sample.DrawVisible,
                DrawScreenWidth = sample.DrawScreenWidth,
                DrawScreenHeight = sample.DrawScreenHeight,
                DrawRegionX = sample.DrawRegionX,
                DrawRegionY = sample.DrawRegionY,
                DrawRegionWidth = sample.DrawRegionWidth,
                DrawRegionHeight = sample.DrawRegionHeight,
                DrawCenterScreenX = sample.DrawCenterScreenX,
                DrawCenterScreenY = sample.DrawCenterScreenY,
                DrawDeltaFromRightClickX = sample.DrawDeltaFromRightClickX,
                DrawDeltaFromRightClickY = sample.DrawDeltaFromRightClickY,
                DrawSkippedReason = sample.DrawSkippedReason ?? string.Empty
            };
        }

        private static void EnqueueWrite(string path, string line, string eventType, string markerId)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrEmpty(line))
            {
                return;
            }

            lock (SyncRoot)
            {
                _lastTraceEventType = eventType ?? string.Empty;
                _lastTraceMarkerId = markerId ?? string.Empty;
            }

            lock (WriteQueueSyncRoot)
            {
                if (PendingWrites.Count >= MaxPendingWrites)
                {
                    PendingWrites.Dequeue();
                }

                PendingWrites.Enqueue(new PendingMapMarkerTraceWrite(path, line));
                if (_writeWorkerScheduled)
                {
                    return;
                }

                _writeWorkerScheduled = true;
                ThreadPool.QueueUserWorkItem(FlushPendingWrites);
            }
        }

        private static void FlushPendingWrites(object ignored)
        {
            while (true)
            {
                PendingMapMarkerTraceWrite[] batch;
                lock (WriteQueueSyncRoot)
                {
                    if (PendingWrites.Count == 0)
                    {
                        _writeWorkerScheduled = false;
                        return;
                    }

                    batch = PendingWrites.ToArray();
                    PendingWrites.Clear();
                }

                try
                {
                    WriteBatch(batch);
                }
                catch (Exception error)
                {
                    if (DateTime.UtcNow - _lastWriteFailureUtc > TimeSpan.FromSeconds(30))
                    {
                        _lastWriteFailureUtc = DateTime.UtcNow;
                        Logger.Warn("PlayerWorldMapMarkerTraceRecorder", "Map marker trace async write failed: " + error.Message);
                    }
                }
            }
        }

        private static void WriteBatch(PendingMapMarkerTraceWrite[] batch)
        {
            if (batch == null || batch.Length == 0)
            {
                return;
            }

            Directory.CreateDirectory(DiagnosticSnapshotWriter.DiagnosticsDirectory);
            var index = 0;
            while (index < batch.Length)
            {
                var path = batch[index].Path;
                var builder = new StringBuilder();
                do
                {
                    builder.AppendLine(batch[index].Line);
                    index++;
                }
                while (index < batch.Length && string.Equals(path, batch[index].Path, StringComparison.OrdinalIgnoreCase));

                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(builder.ToString());
                }

                lock (SyncRoot)
                {
                    _lastTraceEventWrittenAtUtc = DateTime.UtcNow;
                }
            }
        }

        private static string BuildEventJson(PlayerWorldMapMarkerTraceEvent sample)
        {
            if (sample == null)
            {
                return "{}";
            }

            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "time", sample.UtcNow.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture), true);
            AppendString(builder, "scenario", "MapCustomMarker.CoordinateTrace", true);
            AppendString(builder, "runtimeVersion", sample.RuntimeVersion, true);
            AppendString(builder, "eventType", sample.EventType, true);
            AppendString(builder, "pairId", sample.PairId, true);
            AppendString(builder, "markerId", sample.MarkerId, true);
            AppendRaw(builder, "iconItemId", sample.IconItemId.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "rightClick", BuildRightClickJson(sample), true);
            AppendRaw(builder, "transform", BuildTransformJson(sample), true);
            AppendRaw(builder, "projection", BuildProjectionJson(sample), true);
            AppendRaw(builder, "write", BuildWriteJson(sample), true);
            AppendRaw(builder, "draw", BuildDrawJson(sample), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildRightClickJson(PlayerWorldMapMarkerTraceEvent sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "mouseX", IntRaw(sample.ScreenX), true);
            AppendRaw(builder, "mouseY", IntRaw(sample.ScreenY), true);
            AppendRaw(builder, "tileX", IntRaw(sample.TileX), true);
            AppendRaw(builder, "tileY", IntRaw(sample.TileY), true);
            AppendRaw(builder, "screenWidth", IntRaw(sample.ScreenWidth), true);
            AppendRaw(builder, "screenHeight", IntRaw(sample.ScreenHeight), true);
            AppendRaw(builder, "worldSizeX", IntRaw(sample.WorldSizeX), true);
            AppendRaw(builder, "worldSizeY", IntRaw(sample.WorldSizeY), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildTransformJson(PlayerWorldMapMarkerTraceEvent sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "source", sample.TransformSource, true);
            AppendString(builder, "fallbackReason", sample.FallbackReason, true);
            AppendRaw(builder, "mapTopLeftX", FloatRaw(sample.MapTopLeftX), true);
            AppendRaw(builder, "mapTopLeftY", FloatRaw(sample.MapTopLeftY), true);
            AppendRaw(builder, "mapScale", FloatRaw(sample.MapScale), true);
            AppendRaw(builder, "currentMapFullscreenPosX", FloatRaw(sample.CurrentMapFullscreenPosX), true);
            AppendRaw(builder, "currentMapFullscreenPosY", FloatRaw(sample.CurrentMapFullscreenPosY), true);
            AppendRaw(builder, "currentMapScale", FloatRaw(sample.CurrentMapScale), true);
            AppendRaw(builder, "currentGameUpdateCount", sample.CurrentGameUpdateCount.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "transformAgeUpdates", sample.TransformAgeUpdates.ToString(CultureInfo.InvariantCulture), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildProjectionJson(PlayerWorldMapMarkerTraceEvent sample)
        {
            var projectedX = sample.MapTopLeftX + (sample.TileX + 0.5f) * sample.MapScale;
            var projectedY = sample.MapTopLeftY + (sample.TileY + 0.5f) * sample.MapScale;
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "tileCenterScreenX", FloatRaw(projectedX), true);
            AppendRaw(builder, "tileCenterScreenY", FloatRaw(projectedY), true);
            AppendRaw(builder, "tileCenterDeltaX", FloatRaw(projectedX - sample.ScreenX), true);
            AppendRaw(builder, "tileCenterDeltaY", FloatRaw(projectedY - sample.ScreenY), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildWriteJson(PlayerWorldMapMarkerTraceEvent sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "attempted", BoolRaw(sample.WriteAttempted), true);
            AppendRaw(builder, "succeeded", BoolRaw(sample.WriteSucceeded), true);
            AppendString(builder, "status", sample.WriteStatus, true);
            AppendString(builder, "message", sample.WriteMessage, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildDrawJson(PlayerWorldMapMarkerTraceEvent sample)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "attempted", BoolRaw(sample.DrawAttempted), true);
            AppendRaw(builder, "visible", BoolRaw(sample.DrawVisible), true);
            AppendRaw(builder, "screenWidth", IntRaw(sample.DrawScreenWidth), true);
            AppendRaw(builder, "screenHeight", IntRaw(sample.DrawScreenHeight), true);
            AppendRaw(builder, "regionX", IntRaw(sample.DrawRegionX), true);
            AppendRaw(builder, "regionY", IntRaw(sample.DrawRegionY), true);
            AppendRaw(builder, "regionWidth", IntRaw(sample.DrawRegionWidth), true);
            AppendRaw(builder, "regionHeight", IntRaw(sample.DrawRegionHeight), true);
            AppendRaw(builder, "centerScreenX", FloatRaw(sample.DrawCenterScreenX), true);
            AppendRaw(builder, "centerScreenY", FloatRaw(sample.DrawCenterScreenY), true);
            AppendRaw(builder, "deltaFromRightClickX", FloatRaw(sample.DrawDeltaFromRightClickX), true);
            AppendRaw(builder, "deltaFromRightClickY", FloatRaw(sample.DrawDeltaFromRightClickY), true);
            AppendString(builder, "skippedReason", sample.DrawSkippedReason, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FloatRaw(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":\"").Append(Escape(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string rawValue, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":").Append(string.IsNullOrWhiteSpace(rawValue) ? "null" : rawValue);
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string Escape(string value)
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

        private struct PendingMapMarkerTraceWrite
        {
            public PendingMapMarkerTraceWrite(string path, string line)
            {
                Path = path;
                Line = line;
            }

            public readonly string Path;
            public readonly string Line;
        }
    }
}
