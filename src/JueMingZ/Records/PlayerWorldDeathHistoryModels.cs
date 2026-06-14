using System;
using System.Collections.Generic;

namespace JueMingZ.Records
{
    internal sealed class PlayerWorldDeathHistoryEntry
    {
        public string EventId { get; set; }
        public string RealTimeUtc { get; set; }
        public string RealTimeLocalText { get; set; }
        public string DisplayTimeText { get; set; }
        public string DeathText { get; set; }
        public string SourceKind { get; set; }
        public int OriginalIndex { get; set; }
        public DateTime? SortUtc { get; set; }

        public PlayerWorldDeathHistoryEntry()
        {
            EventId = string.Empty;
            RealTimeUtc = string.Empty;
            RealTimeLocalText = string.Empty;
            DisplayTimeText = string.Empty;
            DeathText = string.Empty;
            SourceKind = PlayerWorldDeathSourceKind.Unknown;
            OriginalIndex = -1;
        }
    }

    internal sealed class PlayerWorldDeathHistoryReadResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool SummaryReadFailed { get; set; }
        public bool HistoryReadFailed { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public int DeathCount { get; set; }
        public int TotalEventCount { get; set; }
        public int PageIndex { get; set; }
        public int PageCount { get; set; }
        public int PageSize { get; set; }
        public int DataSignature { get; set; }
        public DateTime LastReadUtc { get; set; }
        public List<PlayerWorldDeathHistoryEntry> Records { get; set; }

        public PlayerWorldDeathHistoryReadResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            Records = new List<PlayerWorldDeathHistoryEntry>();
        }
    }
}
