using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace JueMingZ.Records
{
    internal static class PlayerWorldDeathMarkerCache
    {
        public const int DefaultMaxMarkers = 256;
        private static readonly object SyncRoot = new object();
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);
        private static PlayerWorldDeathMarkerReadResult _cachedResult;
        private static string _cachedPath = string.Empty;
        private static long _cachedFileLength = -1L;
        private static DateTime _cachedFileWriteUtc = DateTime.MinValue;
        private static DateTime _nextRefreshUtc = DateTime.MinValue;

        public static PlayerWorldDeathMarkerReadResult ReadCurrentMarkers(int maxMarkers)
        {
            maxMarkers = NormalizeMaxMarkers(maxMarkers);
            lock (SyncRoot)
            {
                var now = DateTime.UtcNow;
                if (_cachedResult != null && now < _nextRefreshUtc)
                {
                    return CloneResult(_cachedResult, "cached");
                }

                PlayerWorldIdentityResolution identity;
                if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out identity) ||
                    identity == null ||
                    !identity.IsResolved ||
                    string.IsNullOrWhiteSpace(identity.PairId))
                {
                    var result = new PlayerWorldDeathMarkerReadResult
                    {
                        Succeeded = false,
                        IdentityResolved = false,
                        Status = "identityUnavailable",
                        Message = identity == null ? "identity unavailable" : identity.FailureReason
                    };
                    CacheResult(result, string.Empty, -1L, DateTime.MinValue, now);
                    return CloneResult(result, result.Status);
                }

                return ReadMarkersForPairLocked(identity.PairId, maxMarkers, now);
            }
        }

        internal static PlayerWorldDeathMarkerReadResult ReadMarkersForPairForTesting(string pairId, int maxMarkers)
        {
            maxMarkers = NormalizeMaxMarkers(maxMarkers);
            lock (SyncRoot)
            {
                return ReadMarkersForPairLocked(pairId, maxMarkers, DateTime.UtcNow);
            }
        }

        private static PlayerWorldDeathMarkerReadResult ReadMarkersForPairLocked(string pairId, int maxMarkers, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                var invalid = new PlayerWorldDeathMarkerReadResult
                {
                    Succeeded = false,
                    IdentityResolved = false,
                    Status = "identityUnavailable",
                    Message = "pair id unavailable"
                };
                CacheResult(invalid, string.Empty, -1L, DateTime.MinValue, now);
                return CloneResult(invalid, invalid.Status);
            }

            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);
            FileInfo info = null;
            if (File.Exists(path))
            {
                info = new FileInfo(path);
            }

            var length = info == null ? -1L : info.Length;
            var writeUtc = info == null ? DateTime.MinValue : info.LastWriteTimeUtc;
            if (_cachedResult != null &&
                string.Equals(_cachedResult.PairId, pairId, StringComparison.Ordinal) &&
                string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase) &&
                _cachedFileLength == length &&
                _cachedFileWriteUtc == writeUtc)
            {
                _nextRefreshUtc = now + RefreshInterval;
                return CloneResult(_cachedResult, "cached");
            }

            var result = LoadMarkers(path, pairId, maxMarkers);
            CacheResult(result, path, length, writeUtc, now);
            return CloneResult(result, result.Status);
        }

        private static PlayerWorldDeathMarkerReadResult LoadMarkers(string path, string pairId, int maxMarkers)
        {
            var result = new PlayerWorldDeathMarkerReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "loaded",
                Message = "loaded",
                PairId = pairId
            };

            if (!File.Exists(path))
            {
                result.Status = "missing";
                result.Message = "missing";
                return result;
            }

            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    var lineNumber = 0;
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        lineNumber++;
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        PlayerWorldDeathEvent deathEvent;
                        if (!TryDeserializeEvent(line, out deathEvent) || deathEvent == null)
                        {
                            result.Succeeded = false;
                            result.HistoryReadFailed = true;
                            result.Status = "historyReadFailed";
                            result.Message = "invalidLine:" + lineNumber.ToString(CultureInfo.InvariantCulture);
                            break;
                        }

                        result.TotalEventCount++;
                        PlayerWorldDeathMarker marker;
                        if (!TryBuildMarker(pairId, deathEvent, out marker))
                        {
                            continue;
                        }

                        if (result.Markers.Count >= maxMarkers)
                        {
                            result.Markers.RemoveAt(0);
                            result.CulledByLimit = true;
                        }

                        result.Markers.Add(marker);
                    }
                }
            }
            catch (Exception error)
            {
                result.Succeeded = false;
                result.HistoryReadFailed = true;
                result.Status = "historyReadFailed";
                result.Message = error.GetType().Name + ": " + error.Message;
            }

            result.MarkerCount = result.Markers.Count;
            return result;
        }

        private static bool TryDeserializeEvent(string line, out PlayerWorldDeathEvent deathEvent)
        {
            deathEvent = null;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(line ?? string.Empty);
                using (var stream = new MemoryStream(bytes))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PlayerWorldDeathEvent));
                    deathEvent = serializer.ReadObject(stream) as PlayerWorldDeathEvent;
                    return deathEvent != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryBuildMarker(string pairId, PlayerWorldDeathEvent deathEvent, out PlayerWorldDeathMarker marker)
        {
            marker = null;
            if (deathEvent == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(deathEvent.IdentityPairId) &&
                !string.Equals(deathEvent.IdentityPairId, pairId, StringComparison.Ordinal))
            {
                return false;
            }

            float tileX;
            float tileY;
            if (deathEvent.PlayerWorldX >= 0f && deathEvent.PlayerWorldY >= 0f)
            {
                tileX = deathEvent.PlayerWorldX / 16f;
                tileY = deathEvent.PlayerWorldY / 16f;
            }
            else if (deathEvent.PlayerTileX >= 0 && deathEvent.PlayerTileY >= 0)
            {
                tileX = deathEvent.PlayerTileX + 0.5f;
                tileY = deathEvent.PlayerTileY + 0.5f;
            }
            else
            {
                return false;
            }

            marker = new PlayerWorldDeathMarker
            {
                EventId = deathEvent.EventId ?? string.Empty,
                PairId = pairId ?? string.Empty,
                TilePosition = new Microsoft.Xna.Framework.Vector2(tileX, tileY),
                Tooltip = BuildTooltip(deathEvent)
            };
            return true;
        }

        private static string BuildTooltip(PlayerWorldDeathEvent deathEvent)
        {
            var time = deathEvent == null ? string.Empty : (deathEvent.RealTimeLocalText ?? string.Empty).Trim();
            var text = deathEvent == null ? string.Empty : (deathEvent.DeathText ?? string.Empty).Trim();
            if (time.Length > 0 && text.Length > 0)
            {
                return "死亡点 " + time + " " + text;
            }

            if (text.Length > 0)
            {
                return "死亡点 " + text;
            }

            if (time.Length > 0)
            {
                return "死亡点 " + time;
            }

            return "死亡点";
        }

        private static void CacheResult(PlayerWorldDeathMarkerReadResult result, string path, long length, DateTime writeUtc, DateTime now)
        {
            _cachedResult = CloneResult(result, result == null ? string.Empty : result.Status);
            _cachedPath = path ?? string.Empty;
            _cachedFileLength = length;
            _cachedFileWriteUtc = writeUtc;
            _nextRefreshUtc = now + RefreshInterval;
        }

        private static PlayerWorldDeathMarkerReadResult CloneResult(PlayerWorldDeathMarkerReadResult source, string status)
        {
            var clone = new PlayerWorldDeathMarkerReadResult();
            if (source == null)
            {
                clone.Status = status ?? string.Empty;
                return clone;
            }

            clone.Succeeded = source.Succeeded;
            clone.IdentityResolved = source.IdentityResolved;
            clone.HistoryReadFailed = source.HistoryReadFailed;
            clone.CulledByLimit = source.CulledByLimit;
            clone.Status = string.IsNullOrWhiteSpace(status) ? source.Status : status;
            clone.Message = source.Message ?? string.Empty;
            clone.PairId = source.PairId ?? string.Empty;
            clone.TotalEventCount = source.TotalEventCount;
            clone.MarkerCount = source.MarkerCount;
            clone.Markers = new List<PlayerWorldDeathMarker>();
            if (source.Markers != null)
            {
                for (var index = 0; index < source.Markers.Count; index++)
                {
                    var marker = source.Markers[index];
                    if (marker == null)
                    {
                        continue;
                    }

                    clone.Markers.Add(new PlayerWorldDeathMarker
                    {
                        EventId = marker.EventId ?? string.Empty,
                        PairId = marker.PairId ?? string.Empty,
                        TilePosition = marker.TilePosition,
                        Tooltip = marker.Tooltip ?? string.Empty
                    });
                }
            }

            return clone;
        }

        private static int NormalizeMaxMarkers(int maxMarkers)
        {
            if (maxMarkers <= 0)
            {
                return DefaultMaxMarkers;
            }

            return Math.Min(maxMarkers, DefaultMaxMarkers);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _cachedResult = null;
                _cachedPath = string.Empty;
                _cachedFileLength = -1L;
                _cachedFileWriteUtc = DateTime.MinValue;
                _nextRefreshUtc = DateTime.MinValue;
            }
        }
    }
}
