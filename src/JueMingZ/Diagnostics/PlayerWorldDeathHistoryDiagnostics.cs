using System;

namespace JueMingZ.Diagnostics
{
    public static class PlayerWorldDeathHistoryDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static PlayerWorldDeathHistorySnapshot _snapshot = new PlayerWorldDeathHistorySnapshot();

        public static void RecordRead(
            string status,
            string message,
            string pairId,
            int deathCount,
            int totalEventCount,
            int pageIndex,
            int pageCount,
            bool summaryReadFailed,
            bool historyReadFailed)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = status ?? string.Empty;
                _snapshot.LastMessage = message ?? string.Empty;
                _snapshot.LastPairId = pairId ?? string.Empty;
                _snapshot.DeathCount = deathCount;
                _snapshot.TotalEventCount = totalEventCount;
                _snapshot.PageIndex = pageIndex;
                _snapshot.PageCount = pageCount;
                _snapshot.SummaryReadFailed = summaryReadFailed;
                _snapshot.HistoryReadFailed = historyReadFailed;
                _snapshot.LastReadUtc = DateTime.UtcNow;
            }
        }

        public static PlayerWorldDeathHistorySnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return new PlayerWorldDeathHistorySnapshot
                {
                    LastStatus = _snapshot.LastStatus,
                    LastMessage = _snapshot.LastMessage,
                    LastPairId = _snapshot.LastPairId,
                    DeathCount = _snapshot.DeathCount,
                    TotalEventCount = _snapshot.TotalEventCount,
                    PageIndex = _snapshot.PageIndex,
                    PageCount = _snapshot.PageCount,
                    SummaryReadFailed = _snapshot.SummaryReadFailed,
                    HistoryReadFailed = _snapshot.HistoryReadFailed,
                    LastReadUtc = _snapshot.LastReadUtc
                };
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = new PlayerWorldDeathHistorySnapshot();
            }
        }
    }

    public sealed class PlayerWorldDeathHistorySnapshot
    {
        public string LastStatus { get; set; }
        public string LastMessage { get; set; }
        public string LastPairId { get; set; }
        public int DeathCount { get; set; }
        public int TotalEventCount { get; set; }
        public int PageIndex { get; set; }
        public int PageCount { get; set; }
        public bool SummaryReadFailed { get; set; }
        public bool HistoryReadFailed { get; set; }
        public DateTime? LastReadUtc { get; set; }

        public PlayerWorldDeathHistorySnapshot()
        {
            LastStatus = string.Empty;
            LastMessage = string.Empty;
            LastPairId = string.Empty;
        }
    }
}
