using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using JueMingZ.Diagnostics;

namespace JueMingZ.Records
{
    internal static class PlayerWorldDeathHistoryCache
    {
        public const int DefaultPageSize = 6;
        private static readonly object SyncRoot = new object();
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);
        private static PlayerWorldDeathHistoryReadResult _cachedSummaryResult;
        private static string _cachedSummaryPairId = string.Empty;
        private static string _cachedSummaryPath = string.Empty;
        private static long _cachedSummaryLength = -1L;
        private static DateTime _cachedSummaryWriteUtc = DateTime.MinValue;
        private static DateTime _nextSummaryRefreshUtc = DateTime.MinValue;
        private static DeathHistoryFileCache _cachedHistory = new DeathHistoryFileCache();
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

        public static PlayerWorldDeathHistoryReadResult ReadCurrentSummary()
        {
            lock (SyncRoot)
            {
                PlayerWorldIdentityResolution identity;
                if (!TryResolveCurrentReadOnly(out identity))
                {
                    var failed = BuildIdentityFailure(identity, DefaultPageSize);
                    RecordDiagnosticsLocked(failed);
                    return CloneResult(failed, failed.Status);
                }

                var result = ReadSummaryForPairLocked(identity.PairId, DateTime.UtcNow);
                RecordDiagnosticsLocked(result);
                return CloneResult(result, result.Status);
            }
        }

        public static PlayerWorldDeathHistoryReadResult ReadCurrentPage(int pageIndex, int pageSize)
        {
            lock (SyncRoot)
            {
                PlayerWorldIdentityResolution identity;
                if (!TryResolveCurrentReadOnly(out identity))
                {
                    var failed = BuildIdentityFailure(identity, NormalizePageSize(pageSize));
                    RecordDiagnosticsLocked(failed);
                    return CloneResult(failed, failed.Status);
                }

                var result = ReadPageForPairLocked(identity.PairId, pageIndex, pageSize, DateTime.UtcNow);
                RecordDiagnosticsLocked(result);
                return CloneResult(result, result.Status);
            }
        }

        internal static PlayerWorldDeathHistoryReadResult ReadSummaryForPairForTesting(string pairId)
        {
            lock (SyncRoot)
            {
                var result = ReadSummaryForPairLocked(pairId, DateTime.UtcNow);
                RecordDiagnosticsLocked(result);
                return CloneResult(result, result.Status);
            }
        }

        internal static PlayerWorldDeathHistoryReadResult ReadPageForPairForTesting(string pairId, int pageIndex, int pageSize)
        {
            lock (SyncRoot)
            {
                var result = ReadPageForPairLocked(pairId, pageIndex, pageSize, DateTime.UtcNow);
                RecordDiagnosticsLocked(result);
                return CloneResult(result, result.Status);
            }
        }

        private static bool TryResolveCurrentReadOnly(out PlayerWorldIdentityResolution identity)
        {
            return PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out identity) &&
                   identity != null &&
                   identity.IsResolved &&
                   !string.IsNullOrWhiteSpace(identity.PairId);
        }

        private static PlayerWorldDeathHistoryReadResult ReadSummaryForPairLocked(string pairId, DateTime now)
        {
            var pageSize = DefaultPageSize;
            if (string.IsNullOrWhiteSpace(pairId))
            {
                return BuildIdentityFailure(null, pageSize);
            }

            var summaryPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.DeathSummaryFileName);
            FileInfo info = null;
            if (File.Exists(summaryPath))
            {
                info = new FileInfo(summaryPath);
            }

            var length = info == null ? -1L : info.Length;
            var writeUtc = info == null ? DateTime.MinValue : info.LastWriteTimeUtc;
            if (_cachedSummaryResult != null &&
                now < _nextSummaryRefreshUtc &&
                string.Equals(_cachedSummaryPairId, pairId, StringComparison.Ordinal) &&
                string.Equals(_cachedSummaryPath, summaryPath, StringComparison.OrdinalIgnoreCase) &&
                _cachedSummaryLength == length &&
                _cachedSummaryWriteUtc == writeUtc)
            {
                return CloneResult(_cachedSummaryResult, "cached");
            }

            var result = LoadSummary(pairId, summaryPath, length, writeUtc, pageSize, now);
            _cachedSummaryPairId = pairId;
            _cachedSummaryPath = summaryPath;
            _cachedSummaryLength = length;
            _cachedSummaryWriteUtc = writeUtc;
            _cachedSummaryResult = CloneResult(result, result.Status);
            _nextSummaryRefreshUtc = now + RefreshInterval;
            return result;
        }

        private static PlayerWorldDeathHistoryReadResult LoadSummary(string pairId, string summaryPath, long length, DateTime writeUtc, int pageSize, DateTime now)
        {
            var result = new PlayerWorldDeathHistoryReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "loaded",
                Message = "loaded",
                PairId = pairId ?? string.Empty,
                PageSize = pageSize,
                LastReadUtc = now
            };

            PlayerWorldDeathSummaryFile summary;
            string message;
            if (PlayerWorldFeatureDataStore.TryReadJson(summaryPath, out summary, out message) && summary != null)
            {
                if (!string.IsNullOrWhiteSpace(summary.PairId) &&
                    !string.Equals(summary.PairId, pairId, StringComparison.Ordinal))
                {
                    result.Succeeded = false;
                    result.SummaryReadFailed = true;
                    result.Status = "summaryReadFailed";
                    result.Message = "pairMismatch";
                    FillCountFromHistory(pairId, result, now);
                    result.DataSignature = BuildSignature(pairId, length, writeUtc, result.DeathCount, result.TotalEventCount, result.PageIndex, result.PageCount, result.SummaryReadFailed, result.HistoryReadFailed);
                    return result;
                }

                result.DeathCount = Math.Max(0, summary.DeathCount);
                result.TotalEventCount = result.DeathCount;
                result.SummaryReadFailed = summary.DeathSummaryReadFailed;
                result.HistoryReadFailed = summary.DeathHistoryReadFailed;
                result.Message = string.IsNullOrWhiteSpace(summary.DeathSummaryReadMessage) ? "loaded" : summary.DeathSummaryReadMessage;
                result.PageCount = CalculatePageCount(result.DeathCount, pageSize);
                result.DataSignature = BuildSignature(pairId, length, writeUtc, result.DeathCount, result.TotalEventCount, result.PageIndex, result.PageCount, result.SummaryReadFailed, result.HistoryReadFailed);
                return result;
            }

            if (!string.Equals(message, "missing", StringComparison.OrdinalIgnoreCase))
            {
                result.Succeeded = false;
                result.SummaryReadFailed = true;
                result.Status = "summaryReadFailed";
                result.Message = message ?? string.Empty;
            }
            else
            {
                result.Status = "missing";
                result.Message = "missing";
            }

            FillCountFromHistory(pairId, result, now);
            result.PageCount = CalculatePageCount(result.DeathCount, pageSize);
            result.DataSignature = BuildSignature(pairId, length, writeUtc, result.DeathCount, result.TotalEventCount, result.PageIndex, result.PageCount, result.SummaryReadFailed, result.HistoryReadFailed);
            return result;
        }

        private static void FillCountFromHistory(string pairId, PlayerWorldDeathHistoryReadResult result, DateTime now)
        {
            var history = LoadHistoryForPairLocked(pairId, now);
            result.DeathCount = history.Records.Count;
            result.TotalEventCount = history.TotalEventCount;
            result.HistoryReadFailed = result.HistoryReadFailed || history.HistoryReadFailed;
            if (history.HistoryReadFailed)
            {
                result.Succeeded = false;
                if (string.Equals(result.Status, "missing", StringComparison.Ordinal))
                {
                    result.Status = "historyReadFailed";
                }

                result.Message = history.Message;
            }
        }

        private static PlayerWorldDeathHistoryReadResult ReadPageForPairLocked(string pairId, int pageIndex, int pageSize, DateTime now)
        {
            pageSize = NormalizePageSize(pageSize);
            if (string.IsNullOrWhiteSpace(pairId))
            {
                return BuildIdentityFailure(null, pageSize);
            }

            var history = LoadHistoryForPairLocked(pairId, now);
            var pageCount = CalculatePageCount(history.Records.Count, pageSize);
            var clampedPage = pageCount <= 0 ? 0 : ClampInt(pageIndex, 0, pageCount - 1);
            var result = new PlayerWorldDeathHistoryReadResult
            {
                Succeeded = !history.HistoryReadFailed,
                IdentityResolved = true,
                HistoryReadFailed = history.HistoryReadFailed,
                Status = history.Status,
                Message = history.Message,
                PairId = pairId ?? string.Empty,
                DeathCount = history.Records.Count,
                TotalEventCount = history.TotalEventCount,
                PageIndex = clampedPage,
                PageCount = pageCount,
                PageSize = pageSize,
                LastReadUtc = now,
                DataSignature = BuildSignature(pairId, history.Length, history.WriteUtc, history.Records.Count, history.TotalEventCount, clampedPage, pageCount, false, history.HistoryReadFailed)
            };

            if (pageCount > 0)
            {
                var start = clampedPage * pageSize;
                var end = Math.Min(history.Records.Count, start + pageSize);
                for (var index = start; index < end; index++)
                {
                    result.Records.Add(CloneEntry(history.Records[index]));
                }
            }

            return result;
        }

        private static DeathHistoryFileCache LoadHistoryForPairLocked(string pairId, DateTime now)
        {
            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);
            FileInfo info = null;
            if (File.Exists(path))
            {
                info = new FileInfo(path);
            }

            var length = info == null ? -1L : info.Length;
            var writeUtc = info == null ? DateTime.MinValue : info.LastWriteTimeUtc;
            if (_cachedHistory != null &&
                string.Equals(_cachedHistory.PairId, pairId, StringComparison.Ordinal) &&
                string.Equals(_cachedHistory.Path, path, StringComparison.OrdinalIgnoreCase) &&
                _cachedHistory.Length == length &&
                _cachedHistory.WriteUtc == writeUtc &&
                now < _cachedHistory.NextRefreshUtc)
            {
                return _cachedHistory.Clone("cached");
            }

            var loaded = LoadHistory(path, pairId, length, writeUtc, now);
            _cachedHistory = loaded.Clone(loaded.Status);
            _cachedHistory.NextRefreshUtc = now + RefreshInterval;
            return loaded;
        }

        private static DeathHistoryFileCache LoadHistory(string path, string pairId, long length, DateTime writeUtc, DateTime now)
        {
            var cache = new DeathHistoryFileCache
            {
                PairId = pairId ?? string.Empty,
                Path = path ?? string.Empty,
                Length = length,
                WriteUtc = writeUtc,
                Status = "loaded",
                Message = "loaded",
                LastReadUtc = now
            };

            if (!File.Exists(path))
            {
                cache.Status = "missing";
                cache.Message = "missing";
                return cache;
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
                            cache.HistoryReadFailed = true;
                            cache.Status = "historyReadFailed";
                            cache.Message = "invalidLine:" + lineNumber.ToString(CultureInfo.InvariantCulture);
                            continue;
                        }

                        cache.TotalEventCount++;
                        PlayerWorldDeathHistoryEntry entry;
                        if (TryBuildEntry(pairId, deathEvent, cache.TotalEventCount - 1, out entry))
                        {
                            cache.Records.Add(entry);
                        }
                    }
                }

                cache.Records.Sort(CompareEntries);
            }
            catch (Exception error)
            {
                cache.HistoryReadFailed = true;
                cache.Status = "historyReadFailed";
                cache.Message = error.GetType().Name + ": " + error.Message;
            }

            return cache;
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

        private static bool TryBuildEntry(string pairId, PlayerWorldDeathEvent deathEvent, int originalIndex, out PlayerWorldDeathHistoryEntry entry)
        {
            entry = null;
            if (deathEvent == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(deathEvent.IdentityPairId) &&
                !string.Equals(deathEvent.IdentityPairId, pairId, StringComparison.Ordinal))
            {
                return false;
            }

            DateTime parsedUtc;
            DateTime? sortUtc = TryParseUtc(deathEvent.RealTimeUtc, out parsedUtc) ? parsedUtc : (DateTime?)null;
            entry = new PlayerWorldDeathHistoryEntry
            {
                EventId = deathEvent.EventId ?? string.Empty,
                RealTimeUtc = deathEvent.RealTimeUtc ?? string.Empty,
                RealTimeLocalText = deathEvent.RealTimeLocalText ?? string.Empty,
                DisplayTimeText = BuildDisplayTimeText(deathEvent, sortUtc),
                DeathText = BuildDeathText(deathEvent),
                SourceKind = string.IsNullOrWhiteSpace(deathEvent.SourceKind) ? PlayerWorldDeathSourceKind.Unknown : deathEvent.SourceKind,
                OriginalIndex = originalIndex,
                SortUtc = sortUtc
            };
            return true;
        }

        private static string BuildDisplayTimeText(PlayerWorldDeathEvent deathEvent, DateTime? sortUtc)
        {
            var local = deathEvent == null ? string.Empty : (deathEvent.RealTimeLocalText ?? string.Empty).Trim();
            if (local.Length > 0)
            {
                return local;
            }

            if (sortUtc.HasValue)
            {
                return sortUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }

            var utc = deathEvent == null ? string.Empty : (deathEvent.RealTimeUtc ?? string.Empty).Trim();
            return utc.Length > 0 ? utc : "时间未记录";
        }

        private static string BuildDeathText(PlayerWorldDeathEvent deathEvent)
        {
            var text = deathEvent == null ? string.Empty : (deathEvent.DeathText ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                return text;
            }

            text = deathEvent == null ? string.Empty : (deathEvent.SourceCustomReason ?? string.Empty).Trim();
            return text.Length > 0 ? text : "死亡原因未记录";
        }

        private static bool TryParseUtc(string value, out DateTime utc)
        {
            if (DateTime.TryParse(
                    value ?? string.Empty,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out utc))
            {
                utc = utc.ToUniversalTime();
                return true;
            }

            utc = DateTime.MinValue;
            return false;
        }

        private static int CompareEntries(PlayerWorldDeathHistoryEntry left, PlayerWorldDeathHistoryEntry right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            if (left.SortUtc.HasValue && right.SortUtc.HasValue)
            {
                var timeCompare = left.SortUtc.Value.CompareTo(right.SortUtc.Value);
                if (timeCompare != 0)
                {
                    return timeCompare;
                }
            }
            else if (left.SortUtc.HasValue)
            {
                return -1;
            }
            else if (right.SortUtc.HasValue)
            {
                return 1;
            }

            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static PlayerWorldDeathHistoryReadResult BuildIdentityFailure(PlayerWorldIdentityResolution identity, int pageSize)
        {
            return new PlayerWorldDeathHistoryReadResult
            {
                Succeeded = false,
                IdentityResolved = false,
                Status = "identityUnavailable",
                Message = identity == null ? "identity unavailable" : identity.FailureReason,
                PageSize = NormalizePageSize(pageSize),
                LastReadUtc = DateTime.UtcNow
            };
        }

        private static PlayerWorldDeathHistoryReadResult CloneResult(PlayerWorldDeathHistoryReadResult source, string status)
        {
            var clone = new PlayerWorldDeathHistoryReadResult();
            if (source == null)
            {
                clone.Status = status ?? string.Empty;
                return clone;
            }

            clone.Succeeded = source.Succeeded;
            clone.IdentityResolved = source.IdentityResolved;
            clone.SummaryReadFailed = source.SummaryReadFailed;
            clone.HistoryReadFailed = source.HistoryReadFailed;
            clone.Status = string.IsNullOrWhiteSpace(status) ? source.Status : status;
            clone.Message = source.Message ?? string.Empty;
            clone.PairId = source.PairId ?? string.Empty;
            clone.DeathCount = source.DeathCount;
            clone.TotalEventCount = source.TotalEventCount;
            clone.PageIndex = source.PageIndex;
            clone.PageCount = source.PageCount;
            clone.PageSize = source.PageSize;
            clone.DataSignature = source.DataSignature;
            clone.LastReadUtc = source.LastReadUtc;
            if (source.Records != null)
            {
                for (var index = 0; index < source.Records.Count; index++)
                {
                    clone.Records.Add(CloneEntry(source.Records[index]));
                }
            }

            return clone;
        }

        private static PlayerWorldDeathHistoryEntry CloneEntry(PlayerWorldDeathHistoryEntry source)
        {
            if (source == null)
            {
                return null;
            }

            return new PlayerWorldDeathHistoryEntry
            {
                EventId = source.EventId ?? string.Empty,
                RealTimeUtc = source.RealTimeUtc ?? string.Empty,
                RealTimeLocalText = source.RealTimeLocalText ?? string.Empty,
                DisplayTimeText = source.DisplayTimeText ?? string.Empty,
                DeathText = source.DeathText ?? string.Empty,
                SourceKind = source.SourceKind ?? string.Empty,
                OriginalIndex = source.OriginalIndex,
                SortUtc = source.SortUtc
            };
        }

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize <= 0)
            {
                return DefaultPageSize;
            }

            return Math.Max(1, Math.Min(20, pageSize));
        }

        private static int CalculatePageCount(int totalCount, int pageSize)
        {
            if (totalCount <= 0)
            {
                return 0;
            }

            pageSize = NormalizePageSize(pageSize);
            return (totalCount + pageSize - 1) / pageSize;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static int BuildSignature(string pairId, long length, DateTime writeUtc, int deathCount, int totalEventCount, int pageIndex, int pageCount, bool summaryReadFailed, bool historyReadFailed)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(pairId ?? string.Empty);
                hash = hash * 31 + length.GetHashCode();
                hash = hash * 31 + writeUtc.Ticks.GetHashCode();
                hash = hash * 31 + deathCount;
                hash = hash * 31 + totalEventCount;
                hash = hash * 31 + pageIndex;
                hash = hash * 31 + pageCount;
                hash = hash * 31 + (summaryReadFailed ? 1 : 0);
                hash = hash * 31 + (historyReadFailed ? 1 : 0);
                return hash;
            }
        }

        private static void RecordDiagnosticsLocked(PlayerWorldDeathHistoryReadResult result)
        {
            if (result == null)
            {
                return;
            }

            _lastStateSignature = result.DataSignature;
            PlayerWorldDeathHistoryDiagnostics.RecordRead(
                result.Status,
                result.Message,
                result.PairId,
                result.DeathCount,
                result.TotalEventCount,
                result.PageIndex,
                result.PageCount,
                result.SummaryReadFailed,
                result.HistoryReadFailed);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _cachedSummaryResult = null;
                _cachedSummaryPairId = string.Empty;
                _cachedSummaryPath = string.Empty;
                _cachedSummaryLength = -1L;
                _cachedSummaryWriteUtc = DateTime.MinValue;
                _nextSummaryRefreshUtc = DateTime.MinValue;
                _cachedHistory = new DeathHistoryFileCache();
                _lastStateSignature = 0;
            }
        }

        private sealed class DeathHistoryFileCache
        {
            public string PairId { get; set; }
            public string Path { get; set; }
            public long Length { get; set; }
            public DateTime WriteUtc { get; set; }
            public DateTime NextRefreshUtc { get; set; }
            public DateTime LastReadUtc { get; set; }
            public bool HistoryReadFailed { get; set; }
            public string Status { get; set; }
            public string Message { get; set; }
            public int TotalEventCount { get; set; }
            public List<PlayerWorldDeathHistoryEntry> Records { get; private set; }

            public DeathHistoryFileCache()
            {
                PairId = string.Empty;
                Path = string.Empty;
                Length = -1L;
                WriteUtc = DateTime.MinValue;
                NextRefreshUtc = DateTime.MinValue;
                LastReadUtc = DateTime.MinValue;
                Status = string.Empty;
                Message = string.Empty;
                Records = new List<PlayerWorldDeathHistoryEntry>();
            }

            public DeathHistoryFileCache Clone(string status)
            {
                var clone = new DeathHistoryFileCache
                {
                    PairId = PairId ?? string.Empty,
                    Path = Path ?? string.Empty,
                    Length = Length,
                    WriteUtc = WriteUtc,
                    NextRefreshUtc = NextRefreshUtc,
                    LastReadUtc = LastReadUtc,
                    HistoryReadFailed = HistoryReadFailed,
                    Status = string.IsNullOrWhiteSpace(status) ? Status : status,
                    Message = Message ?? string.Empty,
                    TotalEventCount = TotalEventCount
                };
                for (var index = 0; index < Records.Count; index++)
                {
                    var entry = CloneEntry(Records[index]);
                    if (entry != null)
                    {
                        clone.Records.Add(entry);
                    }
                }

                return clone;
            }
        }
    }
}
