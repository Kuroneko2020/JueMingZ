using System;
using System.Collections.Generic;
using JueMingZ.Diagnostics;

namespace JueMingZ.Records
{
    internal static class PlayerWorldFootprintStore
    {
        public static PlayerWorldFootprintReadResult ReadForPair(string pairId)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                return BuildIdentityFailure("pair id unavailable");
            }

            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.FootprintsFileName);
            var result = new PlayerWorldFootprintReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "loaded",
                Message = "loaded",
                PairId = pairId,
                LastReadUtc = DateTime.UtcNow
            };

            PlayerWorldFootprintFile file;
            string message;
            if (PlayerWorldFeatureDataStore.TryReadJson(path, out file, out message) && file != null)
            {
                NormalizeFileIntoResult(pairId, file, result);
            }
            else if (string.Equals(message, "missing", StringComparison.OrdinalIgnoreCase))
            {
                result.Status = "missing";
                result.Message = "missing";
            }
            else
            {
                result.Succeeded = false;
                result.ReadFailed = true;
                result.Status = "readFailed";
                result.Message = message ?? string.Empty;
            }

            PlayerWorldFootprintDiagnostics.RecordRead(result);
            return CloneReadResult(result, result.Status);
        }

        public static PlayerWorldFootprintWriteResult SaveForPair(
            string pairId,
            int worldSizeX,
            int worldSizeY,
            IList<PlayerWorldFootprintSegment> segments,
            string operation)
        {
            var file = new PlayerWorldFootprintFile
            {
                PairId = pairId ?? string.Empty,
                WorldSizeX = worldSizeX,
                WorldSizeY = worldSizeY,
                MaxRetainedTicks = PlayerWorldFootprintConstants.MaxRetainedTicks,
                Segments = CloneSegmentList(segments)
            };

            return SaveFileForPair(pairId, file, operation);
        }

        public static PlayerWorldFootprintWriteResult SaveFileForPair(
            string pairId,
            PlayerWorldFootprintFile file,
            string operation)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                var failed = new PlayerWorldFootprintWriteResult
                {
                    Succeeded = false,
                    IdentityResolved = false,
                    Status = "identityUnavailable",
                    Message = "pair id unavailable",
                    Operation = operation ?? string.Empty
                };
                PlayerWorldFootprintDiagnostics.RecordWrite(failed);
                return failed;
            }

            if (file != null &&
                !string.IsNullOrWhiteSpace(file.PairId) &&
                !string.Equals(file.PairId, pairId, StringComparison.Ordinal))
            {
                var mismatch = new PlayerWorldFootprintWriteResult
                {
                    Succeeded = false,
                    IdentityResolved = true,
                    Status = "pairMismatch",
                    Message = "pair id mismatch",
                    PairId = pairId,
                    Operation = operation ?? string.Empty
                };
                PlayerWorldFootprintDiagnostics.RecordWrite(mismatch);
                return mismatch;
            }

            var now = DateTime.UtcNow;
            bool retentionTrimmed;
            var normalized = NormalizeFileForPair(pairId, file, now, out retentionTrimmed);
            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.FootprintsFileName);
            string message;
            var succeeded = PlayerWorldFeatureDataStore.TryWriteJson(path, normalized, out message);
            var result = new PlayerWorldFootprintWriteResult
            {
                Succeeded = succeeded,
                IdentityResolved = true,
                Changed = succeeded,
                RetentionTrimmed = retentionTrimmed,
                Status = succeeded ? "saved" : "writeFailed",
                Message = message ?? string.Empty,
                PairId = pairId,
                SegmentCount = normalized.Segments == null ? 0 : normalized.Segments.Count,
                PointCount = CountPoints(normalized.Segments),
                Operation = operation ?? string.Empty,
                LastWriteUtc = succeeded ? now : (DateTime?)null
            };

            if (succeeded)
            {
                PlayerWorldFootprintCache.Invalidate(pairId);
            }

            PlayerWorldFootprintDiagnostics.RecordWrite(result);
            return result;
        }

        internal static string BuildPathForTesting(string pairId)
        {
            return PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.FootprintsFileName);
        }

        internal static PlayerWorldFootprintReadResult ReadForPairForTesting(string pairId)
        {
            return ReadForPair(pairId);
        }

        internal static PlayerWorldFootprintWriteResult SaveForPairForTesting(
            string pairId,
            int worldSizeX,
            int worldSizeY,
            IList<PlayerWorldFootprintSegment> segments,
            string operation)
        {
            return SaveForPair(pairId, worldSizeX, worldSizeY, segments, operation);
        }

        internal static PlayerWorldFootprintWriteResult SaveFileForPairForTesting(
            string pairId,
            PlayerWorldFootprintFile file,
            string operation)
        {
            return SaveFileForPair(pairId, file, operation);
        }

        private static void NormalizeFileIntoResult(
            string pairId,
            PlayerWorldFootprintFile file,
            PlayerWorldFootprintReadResult result)
        {
            if (!string.IsNullOrWhiteSpace(file.PairId) &&
                !string.Equals(file.PairId, pairId, StringComparison.Ordinal))
            {
                result.Succeeded = false;
                result.ReadFailed = true;
                result.Status = "readFailed";
                result.Message = "pairMismatch";
                return;
            }

            bool retentionTrimmed;
            var normalized = NormalizeFileForPair(pairId, file, DateTime.UtcNow, out retentionTrimmed);
            result.WorldSizeX = normalized.WorldSizeX;
            result.WorldSizeY = normalized.WorldSizeY;
            result.TimelineStartTicks = normalized.TimelineStartTicks;
            result.TimelineEndTicks = normalized.TimelineEndTicks;
            result.MaxRetainedTicks = normalized.MaxRetainedTicks;
            result.Segments = CloneSegmentList(normalized.Segments);
            result.SegmentCount = result.Segments.Count;
            result.PointCount = CountPoints(result.Segments);
            result.RetentionTrimmed = retentionTrimmed;
        }

        private static PlayerWorldFootprintFile NormalizeFileForPair(
            string pairId,
            PlayerWorldFootprintFile file,
            DateTime lastUpdatedUtc,
            out bool retentionTrimmed)
        {
            retentionTrimmed = false;
            file = file ?? new PlayerWorldFootprintFile();
            var normalizedSegments = NormalizeSegments(file.Segments);
            var maxRetainedTicks = NormalizeMaxRetainedTicks(file.MaxRetainedTicks);
            var timelineEndTicks = Math.Max(0L, file.TimelineEndTicks);
            for (var index = 0; index < normalizedSegments.Count; index++)
            {
                timelineEndTicks = Math.Max(timelineEndTicks, normalizedSegments[index].EndTicks);
            }

            var cutoffTicks = timelineEndTicks > maxRetainedTicks
                ? timelineEndTicks - maxRetainedTicks
                : 0L;
            var retainedSegments = new List<PlayerWorldFootprintSegment>();
            for (var index = 0; index < normalizedSegments.Count; index++)
            {
                var retained = TrimSegmentToCutoff(normalizedSegments[index], cutoffTicks, ref retentionTrimmed);
                if (retained != null && retained.Points != null && retained.Points.Count > 0)
                {
                    retainedSegments.Add(retained);
                }
            }

            long timelineStart = 0L;
            timelineEndTicks = 0L;
            if (retainedSegments.Count > 0)
            {
                timelineStart = retainedSegments[0].StartTicks;
                timelineEndTicks = retainedSegments[0].EndTicks;
                for (var index = 1; index < retainedSegments.Count; index++)
                {
                    timelineStart = Math.Min(timelineStart, retainedSegments[index].StartTicks);
                    timelineEndTicks = Math.Max(timelineEndTicks, retainedSegments[index].EndTicks);
                }
            }

            return new PlayerWorldFootprintFile
            {
                SchemaVersion = PlayerWorldFootprintConstants.SchemaVersion,
                PairId = pairId ?? string.Empty,
                WorldSizeX = Math.Max(0, file.WorldSizeX),
                WorldSizeY = Math.Max(0, file.WorldSizeY),
                TimelineStartTicks = timelineStart,
                TimelineEndTicks = timelineEndTicks,
                MaxRetainedTicks = maxRetainedTicks,
                Segments = retainedSegments,
                LastUpdatedUtc = PlayerWorldFootprintConstants.FormatUtc(lastUpdatedUtc)
            };
        }

        private static List<PlayerWorldFootprintSegment> NormalizeSegments(IList<PlayerWorldFootprintSegment> source)
        {
            var normalized = new List<PlayerWorldFootprintSegment>();
            if (source == null)
            {
                return normalized;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var segment = NormalizeSegment(source[index]);
                if (segment != null)
                {
                    normalized.Add(segment);
                }
            }

            return normalized;
        }

        private static PlayerWorldFootprintSegment NormalizeSegment(PlayerWorldFootprintSegment segment)
        {
            if (segment == null)
            {
                return null;
            }

            var points = new List<PlayerWorldFootprintPoint>();
            for (var index = 0; segment.Points != null && index < segment.Points.Count; index++)
            {
                points.Add(NormalizePoint(segment.Points[index]));
            }

            if (points.Count <= 0)
            {
                return null;
            }

            var startTicks = segment.StartTicks > 0L ? segment.StartTicks : points[0].StartTicks;
            var endTicks = Math.Max(startTicks, segment.EndTicks);
            for (var index = 0; index < points.Count; index++)
            {
                startTicks = Math.Min(startTicks, points[index].StartTicks);
                endTicks = Math.Max(endTicks, SafeAdd(points[index].StartTicks, points[index].DurationTicks));
            }

            return new PlayerWorldFootprintSegment
            {
                SegmentId = string.IsNullOrWhiteSpace(segment.SegmentId) ? Guid.NewGuid().ToString("N") : segment.SegmentId.Trim(),
                StartTicks = Math.Max(0L, startTicks),
                EndTicks = Math.Max(Math.Max(0L, startTicks), endTicks),
                BreakReason = string.IsNullOrWhiteSpace(segment.BreakReason) ? string.Empty : segment.BreakReason.Trim(),
                Points = points
            };
        }

        private static PlayerWorldFootprintPoint NormalizePoint(PlayerWorldFootprintPoint point)
        {
            point = point ?? new PlayerWorldFootprintPoint();
            return new PlayerWorldFootprintPoint
            {
                TileX = NormalizeCoordinate(point.TileX),
                TileY = NormalizeCoordinate(point.TileY),
                StartTicks = Math.Max(0L, point.StartTicks),
                DurationTicks = Math.Max(0L, point.DurationTicks),
                Flags = Math.Max(0, point.Flags)
            };
        }

        private static PlayerWorldFootprintSegment TrimSegmentToCutoff(
            PlayerWorldFootprintSegment segment,
            long cutoffTicks,
            ref bool retentionTrimmed)
        {
            if (segment == null || segment.Points == null)
            {
                return null;
            }

            var points = new List<PlayerWorldFootprintPoint>();
            for (var index = 0; index < segment.Points.Count; index++)
            {
                var point = segment.Points[index];
                if (point == null)
                {
                    continue;
                }

                var pointEnd = SafeAdd(point.StartTicks, point.DurationTicks);
                if (pointEnd < cutoffTicks)
                {
                    retentionTrimmed = true;
                    continue;
                }

                if (point.StartTicks < cutoffTicks)
                {
                    retentionTrimmed = true;
                    points.Add(new PlayerWorldFootprintPoint
                    {
                        TileX = point.TileX,
                        TileY = point.TileY,
                        StartTicks = cutoffTicks,
                        DurationTicks = Math.Max(0L, pointEnd - cutoffTicks),
                        Flags = point.Flags
                    });
                    continue;
                }

                points.Add(ClonePoint(point));
            }

            if (points.Count <= 0)
            {
                return null;
            }

            var startTicks = points[0].StartTicks;
            var endTicks = SafeAdd(points[0].StartTicks, points[0].DurationTicks);
            for (var index = 1; index < points.Count; index++)
            {
                startTicks = Math.Min(startTicks, points[index].StartTicks);
                endTicks = Math.Max(endTicks, SafeAdd(points[index].StartTicks, points[index].DurationTicks));
            }

            if (segment.StartTicks < cutoffTicks)
            {
                retentionTrimmed = true;
            }

            return new PlayerWorldFootprintSegment
            {
                SegmentId = segment.SegmentId ?? string.Empty,
                StartTicks = startTicks,
                EndTicks = endTicks,
                BreakReason = segment.BreakReason ?? string.Empty,
                Points = points
            };
        }

        private static long NormalizeMaxRetainedTicks(long value)
        {
            return value <= 0L ? PlayerWorldFootprintConstants.MaxRetainedTicks : value;
        }

        private static double NormalizeCoordinate(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                return 0d;
            }

            return value;
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

        private static PlayerWorldFootprintReadResult BuildIdentityFailure(string message)
        {
            var result = new PlayerWorldFootprintReadResult
            {
                Succeeded = false,
                IdentityResolved = false,
                Status = "identityUnavailable",
                Message = message ?? string.Empty,
                LastReadUtc = DateTime.UtcNow
            };
            PlayerWorldFootprintDiagnostics.RecordRead(result);
            return result;
        }

        private static List<PlayerWorldFootprintSegment> CloneSegmentList(IList<PlayerWorldFootprintSegment> source)
        {
            var clone = new List<PlayerWorldFootprintSegment>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var segment = source[index];
                if (segment == null)
                {
                    continue;
                }

                var clonedSegment = new PlayerWorldFootprintSegment
                {
                    SegmentId = segment.SegmentId ?? string.Empty,
                    StartTicks = segment.StartTicks,
                    EndTicks = segment.EndTicks,
                    BreakReason = segment.BreakReason ?? string.Empty,
                    Points = new List<PlayerWorldFootprintPoint>()
                };
                for (var pointIndex = 0; segment.Points != null && pointIndex < segment.Points.Count; pointIndex++)
                {
                    clonedSegment.Points.Add(ClonePoint(segment.Points[pointIndex]));
                }

                clone.Add(clonedSegment);
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

        private static PlayerWorldFootprintReadResult CloneReadResult(PlayerWorldFootprintReadResult source, string status)
        {
            var clone = new PlayerWorldFootprintReadResult();
            if (source == null)
            {
                clone.Status = status ?? string.Empty;
                return clone;
            }

            clone.Succeeded = source.Succeeded;
            clone.IdentityResolved = source.IdentityResolved;
            clone.ReadFailed = source.ReadFailed;
            clone.WriteFailed = source.WriteFailed;
            clone.RetentionTrimmed = source.RetentionTrimmed;
            clone.Status = string.IsNullOrWhiteSpace(status) ? source.Status : status;
            clone.Message = source.Message ?? string.Empty;
            clone.PairId = source.PairId ?? string.Empty;
            clone.WorldSizeX = source.WorldSizeX;
            clone.WorldSizeY = source.WorldSizeY;
            clone.TimelineStartTicks = source.TimelineStartTicks;
            clone.TimelineEndTicks = source.TimelineEndTicks;
            clone.MaxRetainedTicks = source.MaxRetainedTicks;
            clone.SegmentCount = source.SegmentCount;
            clone.PointCount = source.PointCount;
            clone.LastReadUtc = source.LastReadUtc;
            clone.LastWriteUtc = source.LastWriteUtc;
            clone.Segments = CloneSegmentList(source.Segments);
            clone.DataSignature = source.DataSignature;
            return clone;
        }
    }
}
