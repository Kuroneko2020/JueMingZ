using System;

namespace JueMingZ.Diagnostics
{
    public static class PlayerWorldPlaytimeDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static PlayerWorldPlaytimeSnapshot _snapshot = new PlayerWorldPlaytimeSnapshot();

        public static void Record(
            string status,
            string message,
            string pairId,
            double totalGameTicks,
            int wholeDayCount,
            bool readFailed,
            bool writeFailed,
            double lastDeltaGameTicks,
            string lastSkippedDeltaReason,
            DateTime? lastSampleUtc,
            DateTime? lastWriteUtc)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = status ?? string.Empty;
                _snapshot.LastMessage = message ?? string.Empty;
                _snapshot.LastPairId = pairId ?? string.Empty;
                _snapshot.TotalGameTicks = totalGameTicks;
                _snapshot.WholeDayCount = wholeDayCount;
                _snapshot.ReadFailed = readFailed;
                _snapshot.WriteFailed = writeFailed;
                _snapshot.LastDeltaGameTicks = lastDeltaGameTicks;
                _snapshot.LastSkippedDeltaReason = lastSkippedDeltaReason ?? string.Empty;
                _snapshot.LastSampleUtc = lastSampleUtc;
                _snapshot.LastWriteUtc = lastWriteUtc;
            }
        }

        public static PlayerWorldPlaytimeSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return new PlayerWorldPlaytimeSnapshot
                {
                    LastStatus = _snapshot.LastStatus,
                    LastMessage = _snapshot.LastMessage,
                    LastPairId = _snapshot.LastPairId,
                    TotalGameTicks = _snapshot.TotalGameTicks,
                    WholeDayCount = _snapshot.WholeDayCount,
                    ReadFailed = _snapshot.ReadFailed,
                    WriteFailed = _snapshot.WriteFailed,
                    LastDeltaGameTicks = _snapshot.LastDeltaGameTicks,
                    LastSkippedDeltaReason = _snapshot.LastSkippedDeltaReason,
                    LastSampleUtc = _snapshot.LastSampleUtc,
                    LastWriteUtc = _snapshot.LastWriteUtc
                };
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = new PlayerWorldPlaytimeSnapshot();
            }
        }
    }

    public sealed class PlayerWorldPlaytimeSnapshot
    {
        public string LastStatus { get; set; }
        public string LastMessage { get; set; }
        public string LastPairId { get; set; }
        public double TotalGameTicks { get; set; }
        public int WholeDayCount { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public double LastDeltaGameTicks { get; set; }
        public string LastSkippedDeltaReason { get; set; }
        public DateTime? LastSampleUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldPlaytimeSnapshot()
        {
            LastStatus = string.Empty;
            LastMessage = string.Empty;
            LastPairId = string.Empty;
            LastSkippedDeltaReason = string.Empty;
        }
    }
}
