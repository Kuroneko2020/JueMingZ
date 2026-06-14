using System;
using System.IO;
using JueMingZ.Diagnostics;

namespace JueMingZ.Records
{
    internal static class PlayerWorldPlaytimeCache
    {
        private static readonly object SyncRoot = new object();
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);
        private static string _cachedPairId = string.Empty;
        private static string _cachedPath = string.Empty;
        private static long _cachedLength = -1L;
        private static DateTime _cachedWriteUtc = DateTime.MinValue;
        private static DateTime _nextRefreshUtc = DateTime.MinValue;
        private static PlayerWorldPlaytimeReadResult _cachedResult;
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

        public static PlayerWorldPlaytimeReadResult ReadCurrent()
        {
            lock (SyncRoot)
            {
                PlayerWorldIdentityResolution identity;
                if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out identity) ||
                    identity == null ||
                    !identity.IsResolved ||
                    string.IsNullOrWhiteSpace(identity.PairId))
                {
                    var failed = BuildIdentityFailure(identity);
                    RecordDiagnosticsLocked(failed);
                    return CloneResult(failed, failed.Status);
                }

                var result = ReadForPairLocked(identity.PairId, DateTime.UtcNow);
                RecordDiagnosticsLocked(result);
                return CloneResult(result, result.Status);
            }
        }

        internal static PlayerWorldPlaytimeReadResult ReadForPairForTesting(string pairId)
        {
            lock (SyncRoot)
            {
                var result = ReadForPairLocked(pairId, DateTime.UtcNow);
                RecordDiagnosticsLocked(result);
                return CloneResult(result, result.Status);
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

        private static PlayerWorldPlaytimeReadResult ReadForPairLocked(string pairId, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                return BuildIdentityFailure(null);
            }

            double memoryTicks;
            int memoryDays;
            DateTime? memorySampleUtc;
            DateTime? memoryWriteUtc;
            int memorySignature;
            if (PlayerWorldPlaytimeService.TryGetInMemoryForPair(
                    pairId,
                    out memoryTicks,
                    out memoryDays,
                    out memorySampleUtc,
                    out memoryWriteUtc,
                    out memorySignature))
            {
                return new PlayerWorldPlaytimeReadResult
                {
                    Succeeded = true,
                    IdentityResolved = true,
                    Status = "memory",
                    Message = "memory",
                    PairId = pairId,
                    TotalGameTicks = memoryTicks,
                    WholeDayCount = memoryDays,
                    DataSignature = memorySignature,
                    LastReadUtc = now,
                    LastSampleUtc = memorySampleUtc,
                    LastWriteUtc = memoryWriteUtc
                };
            }

            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.PlaytimeFileName);
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
                return CloneResult(_cachedResult, "cached");
            }

            var result = Load(pairId, path, length, writeUtc, now);
            _cachedPairId = pairId;
            _cachedPath = path;
            _cachedLength = length;
            _cachedWriteUtc = writeUtc;
            _cachedResult = CloneResult(result, result.Status);
            _nextRefreshUtc = now + RefreshInterval;
            return result;
        }

        private static PlayerWorldPlaytimeReadResult Load(string pairId, string path, long length, DateTime writeUtc, DateTime now)
        {
            var result = new PlayerWorldPlaytimeReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "loaded",
                Message = "loaded",
                PairId = pairId ?? string.Empty,
                LastReadUtc = now
            };

            PlayerWorldPlaytimeFile file;
            string message;
            if (PlayerWorldFeatureDataStore.TryReadJson(path, out file, out message) && file != null)
            {
                if (!string.IsNullOrWhiteSpace(file.PairId) &&
                    !string.Equals(file.PairId, pairId, StringComparison.Ordinal))
                {
                    result.Succeeded = false;
                    result.ReadFailed = true;
                    result.Status = "readFailed";
                    result.Message = "pairMismatch";
                }
                else
                {
                    result.TotalGameTicks = Math.Max(0d, file.TotalGameTicks);
                    result.WholeDayCount = Math.Max(0, file.WholeDayCount);
                    result.LastSampleUtc = TryParseUtc(file.LastSampleUtc);
                    result.LastWriteUtc = TryParseUtc(file.LastUpdatedUtc);
                }
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

            result.DataSignature = BuildSignature(pairId, length, writeUtc, result.TotalGameTicks, result.WholeDayCount, result.ReadFailed);
            return result;
        }

        private static PlayerWorldPlaytimeReadResult BuildIdentityFailure(PlayerWorldIdentityResolution identity)
        {
            return new PlayerWorldPlaytimeReadResult
            {
                Succeeded = false,
                IdentityResolved = false,
                Status = "identityUnavailable",
                Message = identity == null ? "identity unavailable" : identity.FailureReason,
                LastReadUtc = DateTime.UtcNow
            };
        }

        private static PlayerWorldPlaytimeReadResult CloneResult(PlayerWorldPlaytimeReadResult source, string status)
        {
            var clone = new PlayerWorldPlaytimeReadResult();
            if (source == null)
            {
                clone.Status = status ?? string.Empty;
                return clone;
            }

            clone.Succeeded = source.Succeeded;
            clone.IdentityResolved = source.IdentityResolved;
            clone.ReadFailed = source.ReadFailed;
            clone.Status = string.IsNullOrWhiteSpace(status) ? source.Status : status;
            clone.Message = source.Message ?? string.Empty;
            clone.PairId = source.PairId ?? string.Empty;
            clone.TotalGameTicks = Math.Max(0d, source.TotalGameTicks);
            clone.WholeDayCount = Math.Max(0, source.WholeDayCount);
            clone.DataSignature = source.DataSignature;
            clone.LastReadUtc = source.LastReadUtc;
            clone.LastSampleUtc = source.LastSampleUtc;
            clone.LastWriteUtc = source.LastWriteUtc;
            return clone;
        }

        private static int BuildSignature(string pairId, long length, DateTime writeUtc, double totalGameTicks, int wholeDayCount, bool readFailed)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(pairId ?? string.Empty);
                hash = hash * 31 + length.GetHashCode();
                hash = hash * 31 + writeUtc.Ticks.GetHashCode();
                hash = hash * 31 + totalGameTicks.GetHashCode();
                hash = hash * 31 + wholeDayCount;
                hash = hash * 31 + (readFailed ? 1 : 0);
                return hash;
            }
        }

        private static void RecordDiagnosticsLocked(PlayerWorldPlaytimeReadResult result)
        {
            if (result == null)
            {
                return;
            }

            _lastStateSignature = result.DataSignature;
            PlayerWorldPlaytimeDiagnostics.Record(
                result.Status,
                result.Message,
                result.PairId,
                result.TotalGameTicks,
                result.WholeDayCount,
                result.ReadFailed,
                false,
                0d,
                string.Empty,
                result.LastSampleUtc,
                result.LastWriteUtc);
        }

        private static DateTime? TryParseUtc(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(
                    value ?? string.Empty,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out parsed))
            {
                return parsed.ToUniversalTime();
            }

            return null;
        }
    }
}
