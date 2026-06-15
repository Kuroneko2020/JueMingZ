using System;
using System.Collections.Generic;
using System.IO;
using JueMingZ.Diagnostics;

namespace JueMingZ.Records
{
    internal static class PlayerWorldMapMarkerStore
    {
        public static PlayerWorldMapMarkerReadResult ReadForPair(string pairId)
        {
            return ReadForPair(pairId, PlayerWorldMapMarkerConstants.MaxCachedMarkers);
        }

        public static PlayerWorldMapMarkerReadResult ReadForPair(string pairId, int maxMarkers)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                return BuildIdentityFailure("pair id unavailable");
            }

            maxMarkers = NormalizeMaxMarkers(maxMarkers);
            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.MapMarkersFileName);
            var result = new PlayerWorldMapMarkerReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "loaded",
                Message = "loaded",
                PairId = pairId,
                LastReadUtc = DateTime.UtcNow
            };

            PlayerWorldMapMarkerFile file;
            string message;
            if (PlayerWorldFeatureDataStore.TryReadJson(path, out file, out message) && file != null)
            {
                NormalizeFileIntoResult(pairId, file, maxMarkers, result);
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

            PlayerWorldMapMarkerDiagnostics.RecordRead(result);
            return CloneReadResult(result, result.Status);
        }

        public static PlayerWorldMapMarkerWriteResult SaveForPair(
            string pairId,
            int worldSizeX,
            int worldSizeY,
            IList<PlayerWorldMapMarkerRecord> markers,
            string operation)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                var failed = new PlayerWorldMapMarkerWriteResult
                {
                    Succeeded = false,
                    IdentityResolved = false,
                    Status = "identityUnavailable",
                    Message = "pair id unavailable",
                    Operation = operation ?? string.Empty
                };
                PlayerWorldMapMarkerDiagnostics.RecordWrite(failed);
                return failed;
            }

            var normalized = NormalizeMarkers(markers, PlayerWorldMapMarkerConstants.MaxMarkersPerPair);
            if (markers != null && markers.Count > PlayerWorldMapMarkerConstants.MaxMarkersPerPair)
            {
                var limited = new PlayerWorldMapMarkerWriteResult
                {
                    Succeeded = false,
                    IdentityResolved = true,
                    LimitExceeded = true,
                    Status = "limitExceeded",
                    Message = "marker limit exceeded",
                    PairId = pairId,
                    MarkerCount = PlayerWorldMapMarkerConstants.MaxMarkersPerPair,
                    Operation = operation ?? string.Empty
                };
                PlayerWorldMapMarkerDiagnostics.RecordWrite(limited);
                return limited;
            }

            var now = DateTime.UtcNow;
            var file = new PlayerWorldMapMarkerFile
            {
                SchemaVersion = PlayerWorldMapMarkerConstants.SchemaVersion,
                PairId = pairId,
                WorldSizeX = Math.Max(0, worldSizeX),
                WorldSizeY = Math.Max(0, worldSizeY),
                Markers = normalized,
                LastUpdatedUtc = PlayerWorldMapMarkerConstants.FormatUtc(now)
            };

            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.MapMarkersFileName);
            string message;
            var succeeded = PlayerWorldFeatureDataStore.TryWriteJson(path, file, out message);
            var result = new PlayerWorldMapMarkerWriteResult
            {
                Succeeded = succeeded,
                IdentityResolved = true,
                Changed = succeeded,
                Status = succeeded ? "saved" : "writeFailed",
                Message = message ?? string.Empty,
                PairId = pairId,
                MarkerCount = normalized.Count,
                Operation = operation ?? string.Empty,
                LastWriteUtc = succeeded ? now : (DateTime?)null
            };

            if (succeeded)
            {
                PlayerWorldMapMarkerCache.Invalidate(pairId);
            }

            PlayerWorldMapMarkerDiagnostics.RecordWrite(result);
            return result;
        }

        public static PlayerWorldMapMarkerWriteResult AddMarkerForPair(
            string pairId,
            int worldSizeX,
            int worldSizeY,
            PlayerWorldMapMarkerRecord marker)
        {
            var read = ReadForPair(pairId, PlayerWorldMapMarkerConstants.MaxMarkersPerPair);
            if (!read.Succeeded)
            {
                return BuildWriteFailureFromRead(read, "add");
            }

            if (read.Markers.Count >= PlayerWorldMapMarkerConstants.MaxMarkersPerPair)
            {
                var limited = new PlayerWorldMapMarkerWriteResult
                {
                    Succeeded = false,
                    IdentityResolved = true,
                    LimitExceeded = true,
                    Status = "limitExceeded",
                    Message = "marker limit exceeded",
                    PairId = pairId ?? string.Empty,
                    MarkerCount = read.Markers.Count,
                    Operation = "add"
                };
                PlayerWorldMapMarkerDiagnostics.RecordWrite(limited);
                return limited;
            }

            var markers = CloneMarkerList(read.Markers);
            markers.Add(NormalizeMarker(marker, markers.Count));
            return SaveForPair(pairId, worldSizeX, worldSizeY, markers, "add");
        }

        public static PlayerWorldMapMarkerWriteResult RenameMarkerForPair(string pairId, string markerId, string name)
        {
            var read = ReadForPair(pairId, PlayerWorldMapMarkerConstants.MaxMarkersPerPair);
            if (!read.Succeeded)
            {
                return BuildWriteFailureFromRead(read, "rename");
            }

            var normalizedMarkerId = (markerId ?? string.Empty).Trim();
            if (normalizedMarkerId.Length <= 0)
            {
                return BuildMarkerNotFound(pairId, read, "rename");
            }

            var markers = CloneMarkerList(read.Markers);
            var changed = false;
            var normalizedName = PlayerWorldMapMarkerConstants.NormalizeName(name);
            var now = PlayerWorldMapMarkerConstants.FormatUtc(DateTime.UtcNow);
            for (var index = 0; index < markers.Count; index++)
            {
                var marker = markers[index];
                if (marker == null ||
                    !string.Equals(marker.MarkerId, normalizedMarkerId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(marker.Name ?? string.Empty, normalizedName, StringComparison.Ordinal))
                {
                    var unchanged = new PlayerWorldMapMarkerWriteResult
                    {
                        Succeeded = true,
                        IdentityResolved = true,
                        Changed = false,
                        Status = "unchanged",
                        Message = "marker name unchanged",
                        PairId = pairId ?? string.Empty,
                        MarkerCount = markers.Count,
                        Operation = "rename"
                    };
                    PlayerWorldMapMarkerDiagnostics.RecordWrite(unchanged);
                    return unchanged;
                }

                marker.Name = normalizedName;
                marker.UpdatedUtc = now;
                changed = true;
                break;
            }

            if (!changed)
            {
                return BuildMarkerNotFound(pairId, read, "rename");
            }

            return SaveForPair(pairId, read.WorldSizeX, read.WorldSizeY, markers, "rename");
        }

        public static PlayerWorldMapMarkerWriteResult DeleteMarkerForPair(string pairId, string markerId)
        {
            var read = ReadForPair(pairId, PlayerWorldMapMarkerConstants.MaxMarkersPerPair);
            if (!read.Succeeded)
            {
                return BuildWriteFailureFromRead(read, "delete");
            }

            var normalizedMarkerId = (markerId ?? string.Empty).Trim();
            if (normalizedMarkerId.Length <= 0)
            {
                return BuildMarkerNotFound(pairId, read, "delete");
            }

            var markers = CloneMarkerList(read.Markers);
            var removed = false;
            for (var index = markers.Count - 1; index >= 0; index--)
            {
                var marker = markers[index];
                if (marker != null &&
                    string.Equals(marker.MarkerId, normalizedMarkerId, StringComparison.Ordinal))
                {
                    markers.RemoveAt(index);
                    removed = true;
                }
            }

            if (!removed)
            {
                return BuildMarkerNotFound(pairId, read, "delete");
            }

            return SaveForPair(pairId, read.WorldSizeX, read.WorldSizeY, markers, "delete");
        }

        internal static string BuildPathForTesting(string pairId)
        {
            return PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.MapMarkersFileName);
        }

        internal static PlayerWorldMapMarkerReadResult ReadForPairForTesting(string pairId)
        {
            return ReadForPair(pairId);
        }

        internal static PlayerWorldMapMarkerWriteResult SaveForPairForTesting(
            string pairId,
            int worldSizeX,
            int worldSizeY,
            IList<PlayerWorldMapMarkerRecord> markers,
            string operation)
        {
            return SaveForPair(pairId, worldSizeX, worldSizeY, markers, operation);
        }

        internal static PlayerWorldMapMarkerWriteResult RenameMarkerForPairForTesting(string pairId, string markerId, string name)
        {
            return RenameMarkerForPair(pairId, markerId, name);
        }

        internal static PlayerWorldMapMarkerWriteResult DeleteMarkerForPairForTesting(string pairId, string markerId)
        {
            return DeleteMarkerForPair(pairId, markerId);
        }

        private static void NormalizeFileIntoResult(
            string pairId,
            PlayerWorldMapMarkerFile file,
            int maxMarkers,
            PlayerWorldMapMarkerReadResult result)
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

            result.WorldSizeX = Math.Max(0, file.WorldSizeX);
            result.WorldSizeY = Math.Max(0, file.WorldSizeY);
            result.TotalMarkerCount = file.Markers == null ? 0 : file.Markers.Count;
            var normalized = NormalizeMarkers(file.Markers, maxMarkers);
            result.Markers = normalized;
            result.MarkerCount = normalized.Count;
            result.CulledByCacheLimit = result.TotalMarkerCount > normalized.Count;
        }

        private static List<PlayerWorldMapMarkerRecord> NormalizeMarkers(IList<PlayerWorldMapMarkerRecord> source, int maxMarkers)
        {
            var normalized = new List<PlayerWorldMapMarkerRecord>();
            if (source == null)
            {
                return normalized;
            }

            var limit = Math.Max(0, maxMarkers);
            for (var index = 0; index < source.Count && normalized.Count < limit; index++)
            {
                var marker = NormalizeMarker(source[index], index);
                if (marker == null)
                {
                    continue;
                }

                normalized.Add(marker);
            }

            return normalized;
        }

        private static PlayerWorldMapMarkerRecord NormalizeMarker(PlayerWorldMapMarkerRecord marker, int fallbackSortOrder)
        {
            if (marker == null)
            {
                return null;
            }

            var markerId = string.IsNullOrWhiteSpace(marker.MarkerId)
                ? Guid.NewGuid().ToString("N")
                : marker.MarkerId.Trim();
            var createdUtc = string.IsNullOrWhiteSpace(marker.CreatedUtc)
                ? PlayerWorldMapMarkerConstants.FormatUtc(DateTime.UtcNow)
                : marker.CreatedUtc.Trim();
            var updatedUtc = string.IsNullOrWhiteSpace(marker.UpdatedUtc)
                ? createdUtc
                : marker.UpdatedUtc.Trim();

            return new PlayerWorldMapMarkerRecord
            {
                MarkerId = markerId,
                TileX = Math.Max(0, marker.TileX),
                TileY = Math.Max(0, marker.TileY),
                IconItemId = PlayerWorldMapMarkerConstants.NormalizeIconItemId(marker.IconItemId),
                Name = PlayerWorldMapMarkerConstants.NormalizeName(marker.Name),
                CreatedUtc = createdUtc,
                UpdatedUtc = updatedUtc,
                SortOrder = marker.SortOrder == 0 ? fallbackSortOrder : marker.SortOrder
            };
        }

        private static PlayerWorldMapMarkerReadResult BuildIdentityFailure(string message)
        {
            var result = new PlayerWorldMapMarkerReadResult
            {
                Succeeded = false,
                IdentityResolved = false,
                Status = "identityUnavailable",
                Message = message ?? string.Empty,
                LastReadUtc = DateTime.UtcNow
            };
            PlayerWorldMapMarkerDiagnostics.RecordRead(result);
            return result;
        }

        private static PlayerWorldMapMarkerWriteResult BuildWriteFailureFromRead(PlayerWorldMapMarkerReadResult read, string operation)
        {
            var result = new PlayerWorldMapMarkerWriteResult
            {
                Succeeded = false,
                IdentityResolved = read != null && read.IdentityResolved,
                Status = read == null ? "readFailed" : read.Status,
                Message = read == null ? "read unavailable" : read.Message,
                PairId = read == null ? string.Empty : read.PairId,
                MarkerCount = read == null ? 0 : read.MarkerCount,
                Operation = operation ?? string.Empty
            };
            PlayerWorldMapMarkerDiagnostics.RecordWrite(result);
            return result;
        }

        private static PlayerWorldMapMarkerWriteResult BuildMarkerNotFound(string pairId, PlayerWorldMapMarkerReadResult read, string operation)
        {
            var result = new PlayerWorldMapMarkerWriteResult
            {
                Succeeded = false,
                IdentityResolved = read != null && read.IdentityResolved,
                Status = "notFound",
                Message = "marker not found",
                PairId = pairId ?? string.Empty,
                MarkerCount = read == null ? 0 : read.MarkerCount,
                Operation = operation ?? string.Empty
            };
            PlayerWorldMapMarkerDiagnostics.RecordWrite(result);
            return result;
        }

        private static List<PlayerWorldMapMarkerRecord> CloneMarkerList(IList<PlayerWorldMapMarkerRecord> source)
        {
            var clone = new List<PlayerWorldMapMarkerRecord>();
            if (source == null)
            {
                return clone;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var marker = NormalizeMarker(source[index], index);
                if (marker != null)
                {
                    clone.Add(marker);
                }
            }

            return clone;
        }

        private static PlayerWorldMapMarkerReadResult CloneReadResult(PlayerWorldMapMarkerReadResult source, string status)
        {
            var clone = new PlayerWorldMapMarkerReadResult();
            if (source == null)
            {
                clone.Status = status ?? string.Empty;
                return clone;
            }

            clone.Succeeded = source.Succeeded;
            clone.IdentityResolved = source.IdentityResolved;
            clone.ReadFailed = source.ReadFailed;
            clone.WriteFailed = source.WriteFailed;
            clone.CulledByCacheLimit = source.CulledByCacheLimit;
            clone.Status = string.IsNullOrWhiteSpace(status) ? source.Status : status;
            clone.Message = source.Message ?? string.Empty;
            clone.PairId = source.PairId ?? string.Empty;
            clone.WorldSizeX = source.WorldSizeX;
            clone.WorldSizeY = source.WorldSizeY;
            clone.MarkerCount = source.MarkerCount;
            clone.TotalMarkerCount = source.TotalMarkerCount;
            clone.LastOperation = source.LastOperation ?? string.Empty;
            clone.LastReadUtc = source.LastReadUtc;
            clone.LastWriteUtc = source.LastWriteUtc;
            clone.Markers = CloneMarkerList(source.Markers);
            return clone;
        }

        private static int NormalizeMaxMarkers(int maxMarkers)
        {
            if (maxMarkers <= 0)
            {
                return PlayerWorldMapMarkerConstants.MaxCachedMarkers;
            }

            return Math.Min(maxMarkers, PlayerWorldMapMarkerConstants.MaxCachedMarkers);
        }
    }
}
