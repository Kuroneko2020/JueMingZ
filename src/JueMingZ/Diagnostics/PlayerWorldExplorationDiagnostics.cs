using System;

namespace JueMingZ.Diagnostics
{
    public static class PlayerWorldExplorationDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static PlayerWorldExplorationSnapshot _snapshot = new PlayerWorldExplorationSnapshot();

        public static void Record(
            string status,
            string message,
            string pairId,
            int worldWidth,
            int worldHeight,
            long totalTileCount,
            long revealedTileCount,
            long workingRevealedTileCount,
            long scannedTileCount,
            long nextTileIndex,
            int lastScannedTileBudget,
            double revealedPercent,
            bool scanComplete,
            bool readFailed,
            bool writeFailed,
            DateTime? lastScanUtc,
            DateTime? lastCompletedScanUtc,
            DateTime? lastWriteUtc)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = status ?? string.Empty;
                _snapshot.LastMessage = message ?? string.Empty;
                _snapshot.LastPairId = pairId ?? string.Empty;
                _snapshot.WorldWidth = worldWidth;
                _snapshot.WorldHeight = worldHeight;
                _snapshot.TotalTileCount = totalTileCount;
                _snapshot.RevealedTileCount = revealedTileCount;
                _snapshot.WorkingRevealedTileCount = workingRevealedTileCount;
                _snapshot.ScannedTileCount = scannedTileCount;
                _snapshot.NextTileIndex = nextTileIndex;
                _snapshot.LastScannedTileBudget = lastScannedTileBudget;
                _snapshot.RevealedPercent = revealedPercent;
                _snapshot.ScanComplete = scanComplete;
                _snapshot.ReadFailed = readFailed;
                _snapshot.WriteFailed = writeFailed;
                _snapshot.LastScanUtc = lastScanUtc;
                _snapshot.LastCompletedScanUtc = lastCompletedScanUtc;
                _snapshot.LastWriteUtc = lastWriteUtc;
            }
        }

        public static PlayerWorldExplorationSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return new PlayerWorldExplorationSnapshot
                {
                    LastStatus = _snapshot.LastStatus,
                    LastMessage = _snapshot.LastMessage,
                    LastPairId = _snapshot.LastPairId,
                    WorldWidth = _snapshot.WorldWidth,
                    WorldHeight = _snapshot.WorldHeight,
                    TotalTileCount = _snapshot.TotalTileCount,
                    RevealedTileCount = _snapshot.RevealedTileCount,
                    WorkingRevealedTileCount = _snapshot.WorkingRevealedTileCount,
                    ScannedTileCount = _snapshot.ScannedTileCount,
                    NextTileIndex = _snapshot.NextTileIndex,
                    LastScannedTileBudget = _snapshot.LastScannedTileBudget,
                    RevealedPercent = _snapshot.RevealedPercent,
                    ScanComplete = _snapshot.ScanComplete,
                    ReadFailed = _snapshot.ReadFailed,
                    WriteFailed = _snapshot.WriteFailed,
                    LastScanUtc = _snapshot.LastScanUtc,
                    LastCompletedScanUtc = _snapshot.LastCompletedScanUtc,
                    LastWriteUtc = _snapshot.LastWriteUtc
                };
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = new PlayerWorldExplorationSnapshot();
            }
        }
    }

    public sealed class PlayerWorldExplorationSnapshot
    {
        public string LastStatus { get; set; }
        public string LastMessage { get; set; }
        public string LastPairId { get; set; }
        public int WorldWidth { get; set; }
        public int WorldHeight { get; set; }
        public long TotalTileCount { get; set; }
        public long RevealedTileCount { get; set; }
        public long WorkingRevealedTileCount { get; set; }
        public long ScannedTileCount { get; set; }
        public long NextTileIndex { get; set; }
        public int LastScannedTileBudget { get; set; }
        public double RevealedPercent { get; set; }
        public bool ScanComplete { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public DateTime? LastScanUtc { get; set; }
        public DateTime? LastCompletedScanUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldExplorationSnapshot()
        {
            LastStatus = string.Empty;
            LastMessage = string.Empty;
            LastPairId = string.Empty;
        }
    }
}
