using System;
using System.Collections.Generic;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Player;
using JueMingZ.Runtime;

namespace JueMingZ.Records
{
    public static class PlayerWorldFootprintService
    {
        internal const long SampleCadenceTicks = 6;
        internal const long FlushCadenceTicks = 1800;
        internal const long MaxSampleGapTicks = 600;
        internal const double MaxWallClockGapSeconds = 30d;
        internal const double RecordDistanceTiles = 4d;
        internal const double StationaryMergeDistanceTiles = 2d;
        internal const double DirectionChangeMinDistanceTiles = 1.5d;
        internal const double DirectionChangeDotThreshold = 0.5d;
        internal const double AbnormalJumpDistanceTiles = 160d;

        private const int PointFlagSegmentStart = 1;
        private const int PointFlagDirectionChange = 2;
        private const double TileSize = 16d;

        private static readonly object SyncRoot = new object();
        private static string _currentPairId = string.Empty;
        private static PlayerWorldFootprintFile _currentFile;
        private static PlayerWorldFootprintSample _lastSample;
        private static bool _dirty;
        private static long _nextSampleTick;
        private static long _nextFlushTick;
        private static string _pendingBreakReason = "seed";
        private static double _lastDirectionX;
        private static double _lastDirectionY;
        private static int _lastStateSignature;
        private static DateTime? _lastRecordUtc;
        private static DateTime? _lastWriteUtc;

        public static void Tick(GameStateSnapshot snapshot, RuntimeState state)
        {
            var runtimeTick = state == null ? 0L : state.UpdateCount;
            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                FlushAndBreakRecording("notInWorld");
                return;
            }

            lock (SyncRoot)
            {
                if (runtimeTick >= 0 && runtimeTick < _nextSampleTick)
                {
                    return;
                }
            }

            PlayerWorldIdentityResolution identity;
            if (!PlayerWorldIdentityRuntimeCache.TryResolveCurrentCached(runtimeTick, out identity) ||
                identity == null ||
                !identity.IsResolved ||
                string.IsNullOrWhiteSpace(identity.PairId))
            {
                FlushAndBreakRecording(identity == null ? "identityUnavailable" : identity.FailureReason);
                return;
            }

            PlayerWorldFootprintSample sample;
            string sampleMessage;
            if (!TryBuildSample(snapshot, runtimeTick, identity.WorldSizeX, identity.WorldSizeY, DateTime.UtcNow, out sample, out sampleMessage))
            {
                FlushAndBreakRecording(sampleMessage);
                return;
            }

            lock (SyncRoot)
            {
                ProcessSampleLocked(identity.PairId, sample, false);
            }
        }

        public static void FlushPending()
        {
            lock (SyncRoot)
            {
                FlushLocked("explicitFlush");
            }
        }

        internal static bool TryGetInMemoryForPair(
            string pairId,
            out PlayerWorldFootprintReadResult result,
            out int stateSignature)
        {
            lock (SyncRoot)
            {
                if (_currentFile == null ||
                    string.IsNullOrWhiteSpace(pairId) ||
                    !string.Equals(_currentPairId, pairId, StringComparison.Ordinal))
                {
                    result = null;
                    stateSignature = 0;
                    return false;
                }

                result = BuildReadResultFromFile(_currentFile, "memory", "memory", true, false, false, _lastWriteUtc);
                result.DataSignature = _lastStateSignature;
                stateSignature = _lastStateSignature;
                return true;
            }
        }

        internal static PlayerWorldFootprintRecordResult TickForTesting(
            GameStateSnapshot snapshot,
            long runtimeTick,
            string pairId,
            int worldSizeX,
            int worldSizeY,
            DateTime sampleUtc)
        {
            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                return FlushAndBreakRecording("notInWorld");
            }

            lock (SyncRoot)
            {
                if (runtimeTick >= 0 && runtimeTick < _nextSampleTick)
                {
                    var skipped = BuildResult(false, false, false, false, false, false, false, "cadenceSkipped", "cadenceSkipped", string.Empty, runtimeTick);
                    RecordDiagnosticsLocked(skipped);
                    return skipped;
                }
            }

            if (string.IsNullOrWhiteSpace(pairId))
            {
                return FlushAndBreakRecording("identityUnavailable");
            }

            PlayerWorldFootprintSample sample;
            string sampleMessage;
            if (!TryBuildSample(snapshot, runtimeTick, worldSizeX, worldSizeY, sampleUtc, out sample, out sampleMessage))
            {
                return FlushAndBreakRecording(sampleMessage);
            }

            lock (SyncRoot)
            {
                return ProcessSampleLocked(pairId, sample, false);
            }
        }

        internal static PlayerWorldFootprintRecordResult ProcessSampleForTesting(
            string pairId,
            PlayerWorldFootprintSample sample,
            bool forceFlush)
        {
            lock (SyncRoot)
            {
                return ProcessSampleLocked(pairId, sample, forceFlush);
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _currentPairId = string.Empty;
                _currentFile = null;
                _lastSample = null;
                _dirty = false;
                _nextSampleTick = 0;
                _nextFlushTick = 0;
                _pendingBreakReason = "seed";
                _lastDirectionX = 0d;
                _lastDirectionY = 0d;
                _lastStateSignature = 0;
                _lastRecordUtc = null;
                _lastWriteUtc = null;
            }
        }

        private static PlayerWorldFootprintRecordResult FlushAndBreakRecording(string reason)
        {
            lock (SyncRoot)
            {
                var hadDirtyData = _dirty && _currentFile != null && !string.IsNullOrWhiteSpace(_currentPairId);
                var flushed = FlushLocked(reason);
                _pendingBreakReason = string.IsNullOrWhiteSpace(reason) ? "segmentBreak" : reason;
                _lastSample = null;
                _nextSampleTick = 0;
                _lastDirectionX = 0d;
                _lastDirectionY = 0d;
                if (hadDirtyData && !flushed)
                {
                    var failed = BuildResult(false, true, false, false, false, false, false, "writeFailed", _pendingBreakReason, _pendingBreakReason, 0L);
                    RecordDiagnosticsLocked(failed);
                    return failed;
                }

                _currentPairId = string.Empty;
                _currentFile = null;
                _dirty = false;
                _nextFlushTick = 0;
                UpdateStateSignatureLocked();
                var result = BuildResult(false, false, false, false, true, false, flushed, _pendingBreakReason, _pendingBreakReason, _pendingBreakReason, 0L);
                RecordDiagnosticsLocked(result);
                return result;
            }
        }

        private static PlayerWorldFootprintRecordResult ProcessSampleLocked(
            string pairId,
            PlayerWorldFootprintSample sample,
            bool forceFlush)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                var failed = BuildResult(false, false, false, false, false, false, false, "identityUnavailable", "pair id unavailable", "identityUnavailable", sample == null ? 0L : sample.RuntimeTick);
                RecordDiagnosticsLocked(failed);
                return failed;
            }

            if (sample == null || !sample.IsValidPosition)
            {
                var failed = BuildResult(false, true, false, false, false, false, false, "positionUnavailable", "player position unavailable", "positionUnavailable", sample == null ? 0L : sample.RuntimeTick);
                RecordDiagnosticsLocked(failed);
                return failed;
            }

            if (!string.Equals(_currentPairId, pairId, StringComparison.Ordinal))
            {
                var previousPairId = _currentPairId;
                var hadDirtyData = _dirty && _currentFile != null && !string.IsNullOrWhiteSpace(_currentPairId);
                var flushedPrevious = FlushLocked("pairChanged");
                if (hadDirtyData && !flushedPrevious)
                {
                    var failed = BuildResult(false, true, false, false, false, false, false, "writeFailed", "pairChanged", "pairChanged", sample.RuntimeTick);
                    RecordDiagnosticsLocked(failed);
                    return failed;
                }

                LoadPairLocked(pairId);
                if (!string.IsNullOrWhiteSpace(previousPairId))
                {
                    _pendingBreakReason = "pairChanged";
                }

                var seeded = SeedNewSegmentLocked(sample, NormalizeBreakReason(_pendingBreakReason, "pairSeed"));
                RecordDiagnosticsLocked(seeded);
                return seeded;
            }

            if (_lastSample == null)
            {
                var seeded = SeedNewSegmentLocked(sample, NormalizeBreakReason(_pendingBreakReason, "seed"));
                RecordDiagnosticsLocked(seeded);
                return seeded;
            }

            var tickGap = sample.RuntimeTick - _lastSample.RuntimeTick;
            if (tickGap <= 0L)
            {
                _lastSample = CloneSample(sample);
                _nextSampleTick = sample.RuntimeTick + SampleCadenceTicks;
                var backwards = BuildResult(true, true, false, false, false, false, false, "runtimeTickBackwards", "runtime tick did not advance", "runtimeTickBackwards", sample.RuntimeTick);
                RecordDiagnosticsLocked(backwards);
                return backwards;
            }

            if (tickGap > MaxSampleGapTicks)
            {
                var broken = StartSegmentAfterBreakLocked(sample, "tickGap");
                RecordDiagnosticsLocked(broken);
                return broken;
            }

            var wallClockGapSeconds = Math.Max(0d, (sample.SampleUtc - _lastSample.SampleUtc).TotalSeconds);
            var expectedWallClockSeconds = tickGap / (double)PlayerWorldFootprintConstants.TicksPerSecond;
            if (wallClockGapSeconds > MaxWallClockGapSeconds &&
                wallClockGapSeconds > Math.Max(MaxWallClockGapSeconds, expectedWallClockSeconds * 3d))
            {
                var broken = StartSegmentAfterBreakLocked(sample, "wallClockGap");
                RecordDiagnosticsLocked(broken);
                return broken;
            }

            var lastPoint = GetLastPoint(_currentFile);
            if (lastPoint == null)
            {
                var seeded = SeedNewSegmentLocked(sample, "missingPoint");
                RecordDiagnosticsLocked(seeded);
                return seeded;
            }

            var distance = Distance(lastPoint.TileX, lastPoint.TileY, sample.TileX, sample.TileY);
            if (distance > AbnormalJumpDistanceTiles)
            {
                var broken = StartSegmentAfterBreakLocked(sample, "abnormalJump");
                RecordDiagnosticsLocked(broken);
                return broken;
            }

            var timelineEnd = SafeAdd(_currentFile.TimelineEndTicks, tickGap);
            var decision = "observed";
            var pointAdded = false;
            var idleMerged = false;
            if (distance <= StationaryMergeDistanceTiles)
            {
                lastPoint.DurationTicks = SafeAdd(lastPoint.DurationTicks, tickGap);
                idleMerged = true;
                decision = "idleMerged";
            }
            else if (distance >= RecordDistanceTiles || IsDirectionChange(lastPoint, sample, distance))
            {
                var directionChange = distance < RecordDistanceTiles;
                AddPointLocked(sample, timelineEnd, directionChange ? PointFlagDirectionChange : 0);
                pointAdded = true;
                decision = directionChange ? "directionPointAdded" : "pointAdded";
            }

            _currentFile.TimelineEndTicks = timelineEnd;
            UpdateCurrentSegmentEndLocked(timelineEnd);
            _lastSample = CloneSample(sample);
            _lastRecordUtc = sample.SampleUtc;
            _nextSampleTick = sample.RuntimeTick + SampleCadenceTicks;
            _dirty = true;
            var retentionTrimmed = TrimRetentionLocked();
            UpdateStateSignatureLocked();

            var flushAttempted = forceFlush || sample.RuntimeTick >= _nextFlushTick;
            var flushed = false;
            var writeFailed = false;
            if (flushAttempted)
            {
                flushed = FlushLocked(forceFlush ? "forceFlush" : "cadenceFlush");
                writeFailed = !flushed && _dirty;
            }

            var result = BuildResult(!writeFailed, true, pointAdded, idleMerged, false, retentionTrimmed, flushed, writeFailed ? "writeFailed" : decision, writeFailed ? "write failed" : decision, string.Empty, sample.RuntimeTick);
            RecordDiagnosticsLocked(result);
            return result;
        }

        private static void LoadPairLocked(string pairId)
        {
            _currentPairId = pairId ?? string.Empty;
            var read = PlayerWorldFootprintStore.ReadForPair(pairId);
            _currentFile = new PlayerWorldFootprintFile
            {
                PairId = pairId ?? string.Empty,
                WorldSizeX = read == null ? 0 : Math.Max(0, read.WorldSizeX),
                WorldSizeY = read == null ? 0 : Math.Max(0, read.WorldSizeY),
                TimelineStartTicks = read == null ? 0L : Math.Max(0L, read.TimelineStartTicks),
                TimelineEndTicks = read == null ? 0L : Math.Max(0L, read.TimelineEndTicks),
                MaxRetainedTicks = read == null || read.MaxRetainedTicks <= 0L ? PlayerWorldFootprintConstants.MaxRetainedTicks : read.MaxRetainedTicks,
                Segments = CloneSegmentList(read == null ? null : read.Segments),
                LastUpdatedUtc = string.Empty
            };
            _dirty = false;
            _lastSample = null;
            _nextFlushTick = 0;
            _lastWriteUtc = read == null ? (DateTime?)null : read.LastWriteUtc;
            _lastDirectionX = 0d;
            _lastDirectionY = 0d;
            EnsureFileShapeLocked(0, 0);
            UpdateStateSignatureLocked();
        }

        private static PlayerWorldFootprintRecordResult SeedNewSegmentLocked(PlayerWorldFootprintSample sample, string breakReason)
        {
            EnsureFileShapeLocked(sample.WorldSizeX, sample.WorldSizeY);
            var timeline = Math.Max(0L, _currentFile.TimelineEndTicks);
            var segment = new PlayerWorldFootprintSegment
            {
                SegmentId = Guid.NewGuid().ToString("N"),
                StartTicks = timeline,
                EndTicks = timeline,
                BreakReason = NormalizeBreakReason(breakReason, "seed"),
                Points = new List<PlayerWorldFootprintPoint>
                {
                    new PlayerWorldFootprintPoint
                    {
                        TileX = sample.TileX,
                        TileY = sample.TileY,
                        StartTicks = timeline,
                        DurationTicks = 0L,
                        Flags = PointFlagSegmentStart
                    }
                }
            };

            _currentFile.Segments.Add(segment);
            _currentFile.TimelineStartTicks = _currentFile.Segments.Count == 1 ? timeline : Math.Min(_currentFile.TimelineStartTicks, timeline);
            _currentFile.TimelineEndTicks = timeline;
            _lastSample = CloneSample(sample);
            _lastRecordUtc = sample.SampleUtc;
            _nextSampleTick = sample.RuntimeTick + SampleCadenceTicks;
            _nextFlushTick = sample.RuntimeTick + FlushCadenceTicks;
            _pendingBreakReason = string.Empty;
            _lastDirectionX = 0d;
            _lastDirectionY = 0d;
            _dirty = true;
            var retentionTrimmed = TrimRetentionLocked();
            UpdateStateSignatureLocked();
            return BuildResult(true, true, true, false, true, retentionTrimmed, false, "seeded", breakReason, breakReason, sample.RuntimeTick);
        }

        private static PlayerWorldFootprintRecordResult StartSegmentAfterBreakLocked(PlayerWorldFootprintSample sample, string breakReason)
        {
            EnsureFileShapeLocked(sample.WorldSizeX, sample.WorldSizeY);
            var seeded = SeedNewSegmentLocked(sample, breakReason);
            seeded.Status = "segmentBreak";
            seeded.Decision = "segmentBreak";
            seeded.Message = breakReason ?? string.Empty;
            seeded.SegmentBreak = true;
            seeded.BreakReason = breakReason ?? string.Empty;
            return seeded;
        }

        private static void AddPointLocked(PlayerWorldFootprintSample sample, long timelineTicks, int flags)
        {
            var segment = GetCurrentSegment(_currentFile);
            if (segment == null)
            {
                return;
            }

            var previous = GetLastPoint(_currentFile);
            segment.Points.Add(new PlayerWorldFootprintPoint
            {
                TileX = sample.TileX,
                TileY = sample.TileY,
                StartTicks = timelineTicks,
                DurationTicks = 0L,
                Flags = Math.Max(0, flags)
            });

            segment.EndTicks = Math.Max(segment.EndTicks, timelineTicks);
            if (previous != null)
            {
                var dx = sample.TileX - previous.TileX;
                var dy = sample.TileY - previous.TileY;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length > 0.0001d)
                {
                    _lastDirectionX = dx / length;
                    _lastDirectionY = dy / length;
                }
            }
        }

        private static bool TrimRetentionLocked()
        {
            if (_currentFile == null || _currentFile.Segments == null)
            {
                return false;
            }

            var maxRetainedTicks = _currentFile.MaxRetainedTicks <= 0L
                ? PlayerWorldFootprintConstants.MaxRetainedTicks
                : _currentFile.MaxRetainedTicks;
            var cutoff = _currentFile.TimelineEndTicks > maxRetainedTicks
                ? _currentFile.TimelineEndTicks - maxRetainedTicks
                : 0L;
            if (cutoff <= 0L)
            {
                return false;
            }

            var trimmed = false;
            var retained = new List<PlayerWorldFootprintSegment>();
            for (var segmentIndex = 0; segmentIndex < _currentFile.Segments.Count; segmentIndex++)
            {
                var segment = _currentFile.Segments[segmentIndex];
                if (segment == null || segment.Points == null)
                {
                    trimmed = true;
                    continue;
                }

                var points = new List<PlayerWorldFootprintPoint>();
                for (var pointIndex = 0; pointIndex < segment.Points.Count; pointIndex++)
                {
                    var point = segment.Points[pointIndex];
                    if (point == null)
                    {
                        trimmed = true;
                        continue;
                    }

                    var pointEnd = SafeAdd(point.StartTicks, point.DurationTicks);
                    if (pointEnd < cutoff)
                    {
                        trimmed = true;
                        continue;
                    }

                    if (point.StartTicks < cutoff)
                    {
                        trimmed = true;
                        points.Add(new PlayerWorldFootprintPoint
                        {
                            TileX = point.TileX,
                            TileY = point.TileY,
                            StartTicks = cutoff,
                            DurationTicks = Math.Max(0L, pointEnd - cutoff),
                            Flags = point.Flags
                        });
                    }
                    else
                    {
                        points.Add(ClonePoint(point));
                    }
                }

                if (points.Count <= 0)
                {
                    trimmed = true;
                    continue;
                }

                retained.Add(CloneSegmentWithPoints(segment, points));
            }

            if (!trimmed)
            {
                return false;
            }

            _currentFile.Segments = retained;
            RecalculateTimelineLocked();
            _dirty = true;
            return true;
        }

        private static bool FlushLocked(string reason)
        {
            if (!_dirty || _currentFile == null || string.IsNullOrWhiteSpace(_currentPairId))
            {
                return false;
            }

            EnsureFileShapeLocked(_currentFile.WorldSizeX, _currentFile.WorldSizeY);
            var write = PlayerWorldFootprintStore.SaveFileForPair(_currentPairId, _currentFile, reason);
            if (write != null && write.Succeeded)
            {
                _dirty = false;
                _lastWriteUtc = write.LastWriteUtc;
                _nextFlushTick = (_lastSample == null ? 0L : _lastSample.RuntimeTick) + FlushCadenceTicks;
            }

            UpdateStateSignatureLocked();
            return write != null && write.Succeeded;
        }

        private static bool TryBuildSample(
            GameStateSnapshot snapshot,
            long runtimeTick,
            int worldSizeX,
            int worldSizeY,
            DateTime sampleUtc,
            out PlayerWorldFootprintSample sample,
            out string message)
        {
            sample = null;
            message = string.Empty;
            var player = snapshot == null ? null : snapshot.Player;
            if (player == null || !player.Exists || !player.Active)
            {
                message = "playerUnavailable";
                return false;
            }

            if (player.Dead || player.Ghost)
            {
                message = "playerDead";
                return false;
            }

            double worldX;
            double worldY;
            if ((Math.Abs(player.CenterX) > 0.0001d || Math.Abs(player.CenterY) > 0.0001d) &&
                IsValidCoordinate(player.CenterX) &&
                IsValidCoordinate(player.CenterY))
            {
                worldX = player.CenterX;
                worldY = player.CenterY;
            }
            else
            {
                worldX = player.PositionX;
                worldY = player.PositionY;
            }

            if (!IsValidCoordinate(worldX) || !IsValidCoordinate(worldY))
            {
                message = "positionUnavailable";
                return false;
            }

            sample = new PlayerWorldFootprintSample
            {
                RuntimeTick = Math.Max(0L, runtimeTick),
                SampleUtc = sampleUtc.ToUniversalTime(),
                TileX = Math.Max(0d, worldX / TileSize),
                TileY = Math.Max(0d, worldY / TileSize),
                WorldSizeX = Math.Max(0, worldSizeX),
                WorldSizeY = Math.Max(0, worldSizeY)
            };
            return true;
        }

        private static bool IsDirectionChange(PlayerWorldFootprintPoint lastPoint, PlayerWorldFootprintSample sample, double distance)
        {
            if (lastPoint == null || sample == null || distance < DirectionChangeMinDistanceTiles)
            {
                return false;
            }

            var dx = sample.TileX - lastPoint.TileX;
            var dy = sample.TileY - lastPoint.TileY;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0.0001d)
            {
                return false;
            }

            if (Math.Abs(_lastDirectionX) <= 0.0001d && Math.Abs(_lastDirectionY) <= 0.0001d)
            {
                return false;
            }

            var dot = dx / length * _lastDirectionX + dy / length * _lastDirectionY;
            return dot < DirectionChangeDotThreshold;
        }

        private static void EnsureFileShapeLocked(int worldSizeX, int worldSizeY)
        {
            if (_currentFile == null)
            {
                _currentFile = new PlayerWorldFootprintFile();
            }

            _currentFile.SchemaVersion = PlayerWorldFootprintConstants.SchemaVersion;
            _currentFile.PairId = _currentPairId ?? string.Empty;
            if (_currentFile.WorldSizeX <= 0)
            {
                _currentFile.WorldSizeX = Math.Max(0, worldSizeX);
            }

            if (_currentFile.WorldSizeY <= 0)
            {
                _currentFile.WorldSizeY = Math.Max(0, worldSizeY);
            }

            if (_currentFile.MaxRetainedTicks <= 0L)
            {
                _currentFile.MaxRetainedTicks = PlayerWorldFootprintConstants.MaxRetainedTicks;
            }

            if (_currentFile.Segments == null)
            {
                _currentFile.Segments = new List<PlayerWorldFootprintSegment>();
            }

            _currentFile.TimelineStartTicks = Math.Max(0L, _currentFile.TimelineStartTicks);
            _currentFile.TimelineEndTicks = Math.Max(_currentFile.TimelineStartTicks, _currentFile.TimelineEndTicks);
            _currentFile.LastUpdatedUtc = _currentFile.LastUpdatedUtc ?? string.Empty;
        }

        private static void RecalculateTimelineLocked()
        {
            if (_currentFile == null || _currentFile.Segments == null || _currentFile.Segments.Count <= 0)
            {
                if (_currentFile != null)
                {
                    _currentFile.TimelineStartTicks = 0L;
                    _currentFile.TimelineEndTicks = 0L;
                }

                return;
            }

            var start = long.MaxValue;
            var end = 0L;
            for (var index = 0; index < _currentFile.Segments.Count; index++)
            {
                var segment = _currentFile.Segments[index];
                if (segment == null || segment.Points == null || segment.Points.Count <= 0)
                {
                    continue;
                }

                var segmentStart = long.MaxValue;
                var segmentEnd = 0L;
                for (var pointIndex = 0; pointIndex < segment.Points.Count; pointIndex++)
                {
                    var point = segment.Points[pointIndex];
                    if (point == null)
                    {
                        continue;
                    }

                    segmentStart = Math.Min(segmentStart, point.StartTicks);
                    segmentEnd = Math.Max(segmentEnd, SafeAdd(point.StartTicks, point.DurationTicks));
                }

                if (segmentStart == long.MaxValue)
                {
                    continue;
                }

                segment.StartTicks = segmentStart;
                segment.EndTicks = Math.Max(segmentStart, segmentEnd);
                start = Math.Min(start, segment.StartTicks);
                end = Math.Max(end, segment.EndTicks);
            }

            _currentFile.TimelineStartTicks = start == long.MaxValue ? 0L : start;
            _currentFile.TimelineEndTicks = Math.Max(_currentFile.TimelineStartTicks, end);
        }

        private static void UpdateCurrentSegmentEndLocked(long timelineEnd)
        {
            var segment = GetCurrentSegment(_currentFile);
            if (segment != null)
            {
                segment.EndTicks = Math.Max(segment.EndTicks, timelineEnd);
            }
        }

        private static PlayerWorldFootprintSegment GetCurrentSegment(PlayerWorldFootprintFile file)
        {
            return file == null || file.Segments == null || file.Segments.Count <= 0
                ? null
                : file.Segments[file.Segments.Count - 1];
        }

        private static PlayerWorldFootprintPoint GetLastPoint(PlayerWorldFootprintFile file)
        {
            var segment = GetCurrentSegment(file);
            return segment == null || segment.Points == null || segment.Points.Count <= 0
                ? null
                : segment.Points[segment.Points.Count - 1];
        }

        private static PlayerWorldFootprintRecordResult BuildResult(
            bool succeeded,
            bool identityResolved,
            bool pointAdded,
            bool idleMerged,
            bool segmentBreak,
            bool retentionTrimmed,
            bool flushed,
            string status,
            string message,
            string breakReason,
            long runtimeTick)
        {
            var lastPoint = GetLastPoint(_currentFile);
            return new PlayerWorldFootprintRecordResult
            {
                Succeeded = succeeded,
                IdentityResolved = identityResolved,
                Recorded = pointAdded || idleMerged || segmentBreak,
                PointAdded = pointAdded,
                IdleMerged = idleMerged,
                SegmentBreak = segmentBreak,
                RetentionTrimmed = retentionTrimmed,
                Flushed = flushed,
                WriteFailed = string.Equals(status, "writeFailed", StringComparison.Ordinal),
                Status = status ?? string.Empty,
                Decision = status ?? string.Empty,
                Message = message ?? string.Empty,
                BreakReason = breakReason ?? string.Empty,
                PairId = _currentPairId ?? string.Empty,
                WorldSizeX = _currentFile == null ? 0 : _currentFile.WorldSizeX,
                WorldSizeY = _currentFile == null ? 0 : _currentFile.WorldSizeY,
                SegmentCount = _currentFile == null || _currentFile.Segments == null ? 0 : _currentFile.Segments.Count,
                PointCount = CountPoints(_currentFile == null ? null : _currentFile.Segments),
                TimelineStartTicks = _currentFile == null ? 0L : _currentFile.TimelineStartTicks,
                TimelineEndTicks = _currentFile == null ? 0L : _currentFile.TimelineEndTicks,
                LastPointTileX = lastPoint == null ? 0d : lastPoint.TileX,
                LastPointTileY = lastPoint == null ? 0d : lastPoint.TileY,
                LastPointDurationTicks = lastPoint == null ? 0L : lastPoint.DurationTicks,
                LastRecordRuntimeTick = runtimeTick,
                NextSampleTick = _nextSampleTick,
                LastRecordUtc = _lastRecordUtc,
                LastWriteUtc = _lastWriteUtc
            };
        }

        private static PlayerWorldFootprintReadResult BuildReadResultFromFile(
            PlayerWorldFootprintFile file,
            string status,
            string message,
            bool identityResolved,
            bool readFailed,
            bool writeFailed,
            DateTime? writeUtc)
        {
            var result = new PlayerWorldFootprintReadResult
            {
                Succeeded = !readFailed && !writeFailed,
                IdentityResolved = identityResolved,
                ReadFailed = readFailed,
                WriteFailed = writeFailed,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                PairId = file == null ? string.Empty : file.PairId ?? string.Empty,
                WorldSizeX = file == null ? 0 : file.WorldSizeX,
                WorldSizeY = file == null ? 0 : file.WorldSizeY,
                TimelineStartTicks = file == null ? 0L : file.TimelineStartTicks,
                TimelineEndTicks = file == null ? 0L : file.TimelineEndTicks,
                MaxRetainedTicks = file == null || file.MaxRetainedTicks <= 0L ? PlayerWorldFootprintConstants.MaxRetainedTicks : file.MaxRetainedTicks,
                LastReadUtc = DateTime.UtcNow,
                LastWriteUtc = writeUtc,
                Segments = CloneSegmentList(file == null ? null : file.Segments)
            };
            result.SegmentCount = result.Segments.Count;
            result.PointCount = CountPoints(result.Segments);
            return result;
        }

        private static void RecordDiagnosticsLocked(PlayerWorldFootprintRecordResult result)
        {
            if (result != null)
            {
                PlayerWorldFootprintDiagnostics.RecordRuntime(result);
            }
        }

        private static void UpdateStateSignatureLocked()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_currentPairId ?? string.Empty);
                hash = hash * 31 + (_currentFile == null ? 0 : _currentFile.TimelineStartTicks.GetHashCode());
                hash = hash * 31 + (_currentFile == null ? 0 : _currentFile.TimelineEndTicks.GetHashCode());
                hash = hash * 31 + (_currentFile == null || _currentFile.Segments == null ? 0 : _currentFile.Segments.Count);
                hash = hash * 31 + CountPoints(_currentFile == null ? null : _currentFile.Segments);
                hash = hash * 31 + (_dirty ? 1 : 0);
                _lastStateSignature = hash;
            }
        }

        private static List<PlayerWorldFootprintSegment> CloneSegmentList(IList<PlayerWorldFootprintSegment> source)
        {
            var clone = new List<PlayerWorldFootprintSegment>();
            for (var index = 0; source != null && index < source.Count; index++)
            {
                var segment = source[index];
                if (segment == null)
                {
                    continue;
                }

                clone.Add(CloneSegmentWithPoints(segment, segment.Points));
            }

            return clone;
        }

        private static PlayerWorldFootprintSegment CloneSegmentWithPoints(
            PlayerWorldFootprintSegment segment,
            IList<PlayerWorldFootprintPoint> points)
        {
            var clone = new PlayerWorldFootprintSegment
            {
                SegmentId = segment == null ? string.Empty : segment.SegmentId ?? string.Empty,
                StartTicks = segment == null ? 0L : Math.Max(0L, segment.StartTicks),
                EndTicks = segment == null ? 0L : Math.Max(0L, segment.EndTicks),
                BreakReason = segment == null ? string.Empty : segment.BreakReason ?? string.Empty,
                Points = new List<PlayerWorldFootprintPoint>()
            };

            for (var index = 0; points != null && index < points.Count; index++)
            {
                clone.Points.Add(ClonePoint(points[index]));
            }

            if (clone.Points.Count > 0)
            {
                clone.StartTicks = clone.Points[0].StartTicks;
                clone.EndTicks = SafeAdd(clone.Points[0].StartTicks, clone.Points[0].DurationTicks);
                for (var index = 1; index < clone.Points.Count; index++)
                {
                    clone.StartTicks = Math.Min(clone.StartTicks, clone.Points[index].StartTicks);
                    clone.EndTicks = Math.Max(clone.EndTicks, SafeAdd(clone.Points[index].StartTicks, clone.Points[index].DurationTicks));
                }
            }

            return clone;
        }

        private static PlayerWorldFootprintPoint ClonePoint(PlayerWorldFootprintPoint point)
        {
            point = point ?? new PlayerWorldFootprintPoint();
            return new PlayerWorldFootprintPoint
            {
                TileX = point.TileX,
                TileY = point.TileY,
                StartTicks = point.StartTicks,
                DurationTicks = point.DurationTicks,
                Flags = point.Flags
            };
        }

        private static PlayerWorldFootprintSample CloneSample(PlayerWorldFootprintSample sample)
        {
            if (sample == null)
            {
                return null;
            }

            return new PlayerWorldFootprintSample
            {
                RuntimeTick = sample.RuntimeTick,
                SampleUtc = sample.SampleUtc,
                TileX = sample.TileX,
                TileY = sample.TileY,
                WorldSizeX = sample.WorldSizeX,
                WorldSizeY = sample.WorldSizeY
            };
        }

        private static string NormalizeBreakReason(string reason, string fallback)
        {
            return string.IsNullOrWhiteSpace(reason) ? fallback : reason;
        }

        private static bool IsValidCoordinate(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Distance(double leftX, double leftY, double rightX, double rightY)
        {
            var dx = rightX - leftX;
            var dy = rightY - leftY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static long SafeAdd(long left, long right)
        {
            if (right <= 0L)
            {
                return left;
            }

            return long.MaxValue - left < right ? long.MaxValue : left + right;
        }

        private static int CountPoints(IList<PlayerWorldFootprintSegment> segments)
        {
            var count = 0;
            for (var index = 0; segments != null && index < segments.Count; index++)
            {
                count += segments[index] == null || segments[index].Points == null ? 0 : segments[index].Points.Count;
            }

            return count;
        }
    }

    internal sealed class PlayerWorldFootprintSample
    {
        public long RuntimeTick { get; set; }
        public DateTime SampleUtc { get; set; }
        public double TileX { get; set; }
        public double TileY { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }

        public bool IsValidPosition
        {
            get
            {
                return !double.IsNaN(TileX) &&
                       !double.IsInfinity(TileX) &&
                       !double.IsNaN(TileY) &&
                       !double.IsInfinity(TileY) &&
                       TileX >= 0d &&
                       TileY >= 0d;
            }
        }
    }

    internal sealed class PlayerWorldFootprintRecordResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool Recorded { get; set; }
        public bool PointAdded { get; set; }
        public bool IdleMerged { get; set; }
        public bool SegmentBreak { get; set; }
        public bool RetentionTrimmed { get; set; }
        public bool Flushed { get; set; }
        public bool WriteFailed { get; set; }
        public string Status { get; set; }
        public string Decision { get; set; }
        public string Message { get; set; }
        public string BreakReason { get; set; }
        public string PairId { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
        public int SegmentCount { get; set; }
        public int PointCount { get; set; }
        public long TimelineStartTicks { get; set; }
        public long TimelineEndTicks { get; set; }
        public double LastPointTileX { get; set; }
        public double LastPointTileY { get; set; }
        public long LastPointDurationTicks { get; set; }
        public long LastRecordRuntimeTick { get; set; }
        public long NextSampleTick { get; set; }
        public DateTime? LastRecordUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldFootprintRecordResult()
        {
            Status = string.Empty;
            Decision = string.Empty;
            Message = string.Empty;
            BreakReason = string.Empty;
            PairId = string.Empty;
        }
    }
}
