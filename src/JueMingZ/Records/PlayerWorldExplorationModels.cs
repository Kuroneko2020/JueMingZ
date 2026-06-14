using System;
using System.Runtime.Serialization;

namespace JueMingZ.Records
{
    public static class PlayerWorldExplorationConstants
    {
        public const int SchemaVersion = 1;
        public const int ScanTileBudget = 4096;
        public const long ScanCadenceTicks = 10;
        public const long FlushCadenceTicks = 1800;
        public const long RescanCadenceTicks = 3600;
        public const string ScanSemantics = "mainMapIsRevealed;chunked4096tilesPer10ticks;publishedValueUsesLastCompleteScanWhenAvailable";
    }

    [DataContract]
    public sealed class PlayerWorldExplorationSummaryFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string PairId { get; set; }

        [DataMember(Order = 3)]
        public int WorldWidth { get; set; }

        [DataMember(Order = 4)]
        public int WorldHeight { get; set; }

        [DataMember(Order = 5)]
        public long TotalTileCount { get; set; }

        [DataMember(Order = 6)]
        public long RevealedTileCount { get; set; }

        [DataMember(Order = 7)]
        public long WorkingRevealedTileCount { get; set; }

        [DataMember(Order = 8)]
        public long ScannedTileCount { get; set; }

        [DataMember(Order = 9)]
        public long NextTileIndex { get; set; }

        [DataMember(Order = 10)]
        public bool ScanComplete { get; set; }

        [DataMember(Order = 11)]
        public string LastCompletedScanUtc { get; set; }

        [DataMember(Order = 12)]
        public string LastScanUtc { get; set; }

        [DataMember(Order = 13)]
        public int LastScannedTileBudget { get; set; }

        [DataMember(Order = 14)]
        public string LastResetReason { get; set; }

        [DataMember(Order = 15)]
        public bool LastWriteSucceeded { get; set; }

        [DataMember(Order = 16)]
        public string LastWriteStatus { get; set; }

        [DataMember(Order = 17)]
        public string LastWriteMessage { get; set; }

        [DataMember(Order = 18)]
        public string LastUpdatedUtc { get; set; }

        [DataMember(Order = 19)]
        public string ScanSemantics { get; set; }

        public PlayerWorldExplorationSummaryFile()
        {
            SchemaVersion = PlayerWorldExplorationConstants.SchemaVersion;
            PairId = string.Empty;
            LastCompletedScanUtc = string.Empty;
            LastScanUtc = string.Empty;
            LastResetReason = string.Empty;
            LastWriteStatus = string.Empty;
            LastWriteMessage = string.Empty;
            LastUpdatedUtc = string.Empty;
            ScanSemantics = PlayerWorldExplorationConstants.ScanSemantics;
        }
    }

    internal sealed class PlayerWorldExplorationReadResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public int WorldWidth { get; set; }
        public int WorldHeight { get; set; }
        public long TotalTileCount { get; set; }
        public long RevealedTileCount { get; set; }
        public long WorkingRevealedTileCount { get; set; }
        public long ScannedTileCount { get; set; }
        public long NextTileIndex { get; set; }
        public int LastScannedTileBudget { get; set; }
        public bool ScanComplete { get; set; }
        public double RevealedPercent { get; set; }
        public int DataSignature { get; set; }
        public DateTime LastReadUtc { get; set; }
        public DateTime? LastScanUtc { get; set; }
        public DateTime? LastCompletedScanUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldExplorationReadResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            LastReadUtc = DateTime.UtcNow;
        }
    }

    internal sealed class PlayerWorldExplorationScanResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool Reset { get; set; }
        public bool ScanComplete { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public int WorldWidth { get; set; }
        public int WorldHeight { get; set; }
        public long TotalTileCount { get; set; }
        public long RevealedTileCount { get; set; }
        public long WorkingRevealedTileCount { get; set; }
        public long ScannedTileCount { get; set; }
        public long NextTileIndex { get; set; }
        public int TilesScannedThisTick { get; set; }
        public int RevealedTilesThisTick { get; set; }
        public double RevealedPercent { get; set; }
        public DateTime? LastScanUtc { get; set; }
        public DateTime? LastCompletedScanUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldExplorationScanResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
        }
    }
}
