using System;
using System.IO;
using JueMingZ.Diagnostics;

namespace JueMingZ.Records
{
    internal static class PlayerWorldMapMarkerCache
    {
        private static readonly object SyncRoot = new object();
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);
        private static string _cachedPairId = string.Empty;
        private static string _cachedPath = string.Empty;
        private static long _cachedLength = -1L;
        private static DateTime _cachedWriteUtc = DateTime.MinValue;
        private static DateTime _nextRefreshUtc = DateTime.MinValue;
        private static PlayerWorldMapMarkerReadResult _cachedResult;
        private static int _lastStateSignature;

        public static int LastStateSignature
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastStateSignature;
                }
            }
        }

        public static PlayerWorldMapMarkerReadResult ReadCurrent()
        {
            lock (SyncRoot)
            {
                PlayerWorldIdentityResolution identity;
                if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out identity) ||
                    identity == null ||
                    !identity.IsResolved ||
                    string.IsNullOrWhiteSpace(identity.PairId))
                {
                    var failed = new PlayerWorldMapMarkerReadResult
                    {
                        Succeeded = false,
                        IdentityResolved = false,
                        Status = "identityUnavailable",
                        Message = identity == null ? "identity unavailable" : identity.FailureReason,
                        LastReadUtc = DateTime.UtcNow
                    };
                    RecordAndCacheLocked(failed, string.Empty, -1L, DateTime.MinValue, DateTime.UtcNow);
                    return failed;
                }

                return ReadForPairLocked(identity.PairId, DateTime.UtcNow);
            }
        }

        internal static PlayerWorldMapMarkerReadResult ReadForPairForTesting(string pairId)
        {
            return ReadForPair(pairId);
        }

        internal static PlayerWorldMapMarkerReadResult ReadForPair(string pairId)
        {
            lock (SyncRoot)
            {
                return ReadForPairLocked(pairId, DateTime.UtcNow);
            }
        }

        internal static void Invalidate(string pairId)
        {
            lock (SyncRoot)
            {
                if (string.IsNullOrWhiteSpace(pairId) ||
                    string.Equals(_cachedPairId, pairId, StringComparison.Ordinal))
                {
                    _nextRefreshUtc = DateTime.MinValue;
                    _cachedLength = -1L;
                    _cachedWriteUtc = DateTime.MinValue;
                }
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _cachedPairId = string.Empty;
                _cachedPath = string.Empty;
                _cachedLength = -1L;
                _cachedWriteUtc = DateTime.MinValue;
                _nextRefreshUtc = DateTime.MinValue;
                _cachedResult = null;
                _lastStateSignature = 0;
            }
        }

        private static PlayerWorldMapMarkerReadResult ReadForPairLocked(string pairId, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                var failed = new PlayerWorldMapMarkerReadResult
                {
                    Succeeded = false,
                    IdentityResolved = false,
                    Status = "identityUnavailable",
                    Message = "pair id unavailable",
                    LastReadUtc = now
                };
                RecordAndCacheLocked(failed, string.Empty, -1L, DateTime.MinValue, now);
                return failed;
            }

            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.MapMarkersFileName);
            FileInfo info = null;
            if (File.Exists(path))
            {
                info = new FileInfo(path);
            }

            var length = info == null ? -1L : info.Length;
            var writeUtc = info == null ? DateTime.MinValue : info.LastWriteTimeUtc;
            if (_cachedResult != null &&
                now < _nextRefreshUtc &&
                string.Equals(_cachedPairId, pairId, StringComparison.Ordinal) &&
                string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase) &&
                _cachedLength == length &&
                _cachedWriteUtc == writeUtc)
            {
                var cached = CloneReadResult(_cachedResult, "cached");
                PlayerWorldMapMarkerDiagnostics.RecordRead(cached);
                return cached;
            }

            var result = PlayerWorldMapMarkerStore.ReadForPair(pairId, PlayerWorldMapMarkerConstants.MaxCachedMarkers);
            RecordAndCacheLocked(result, path, length, writeUtc, now);
            return result;
        }

        private static void RecordAndCacheLocked(PlayerWorldMapMarkerReadResult result, string path, long length, DateTime writeUtc, DateTime now)
        {
            _cachedPairId = result == null ? string.Empty : result.PairId ?? string.Empty;
            _cachedPath = path ?? string.Empty;
            _cachedLength = length;
            _cachedWriteUtc = writeUtc;
            _cachedResult = result;
            _nextRefreshUtc = now + RefreshInterval;
            _lastStateSignature = BuildSignature(result, length, writeUtc);
            PlayerWorldMapMarkerDiagnostics.RecordRead(result);
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
            for (var index = 0; source.Markers != null && index < source.Markers.Count; index++)
            {
                var marker = source.Markers[index];
                if (marker == null)
                {
                    continue;
                }

                clone.Markers.Add(new PlayerWorldMapMarkerRecord
                {
                    MarkerId = marker.MarkerId ?? string.Empty,
                    TileX = marker.TileX,
                    TileY = marker.TileY,
                    IconItemId = marker.IconItemId,
                    Name = marker.Name ?? string.Empty,
                    CreatedUtc = marker.CreatedUtc ?? string.Empty,
                    UpdatedUtc = marker.UpdatedUtc ?? string.Empty,
                    SortOrder = marker.SortOrder
                });
            }

            return clone;
        }

        private static int BuildSignature(PlayerWorldMapMarkerReadResult result, long length, DateTime writeUtc)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (result == null ? 0 : (result.PairId ?? string.Empty).GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.MarkerCount);
                hash = hash * 31 + (int)(length & 0x7fffffff);
                hash = hash * 31 + writeUtc.GetHashCode();
                hash = hash * 31 + (result != null && result.ReadFailed ? 1 : 0);
                return hash;
            }
        }
    }
}
