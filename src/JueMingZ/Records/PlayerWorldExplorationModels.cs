using System;
using System.Runtime.Serialization;

namespace JueMingZ.Records
{
    public static class PlayerWorldExplorationConstants
    {
        public const int SchemaVersion = 1;
        public const int PerformanceScanTileCap = 512;
        public const int FastScanTileCap = 4096;
        public const int ScanTileBudget = FastScanTileCap;
        public const long PerformanceScanCadenceTicks = 30;
        public const long PerformanceBackoffScanCadenceTicks = 90;
        public const long FastScanCadenceTicks = 1;
        public const long ScanCadenceTicks = PerformanceScanCadenceTicks;
        public const long FlushCadenceTicks = 1800;
        public const double PerformanceScanTimeBudgetMs = 0.35d;
        public const double FastScanTimeBudgetMs = 3.0d;
        public const string ScanSemantics = "mainMapIsRevealed;modeTimedBudgetWithBackoff;manualRefreshOnlyAfterComplete;publishedValueUsesLastCompleteScanWhenAvailable";
    }

    public static class PlayerWorldExplorationScanModes
    {
        public const string Performance = "Performance";
        public const string Fast = "Fast";

        public static string Normalize(string value)
        {
            if (string.Equals(value, Fast, StringComparison.OrdinalIgnoreCase))
            {
                return Fast;
            }

            return Performance;
        }
    }

    public static class PlayerWorldExplorationControlStates
    {
        public const string Scanning = "Scanning";
        public const string PausedByUser = "PausedByUser";
        public const string IdleComplete = "IdleComplete";
    }

    public sealed class PlayerWorldExplorationControlSnapshot
    {
        public string ScanMode { get; set; }
        public string ControlState { get; set; }
        public string PairId { get; set; }
        public bool ManualRefreshPending { get; set; }
        public bool ScanComplete { get; set; }
        public bool HasCursor { get; set; }
        public long ScannedTileCount { get; set; }
        public long NextTileIndex { get; set; }
        public long TotalTileCount { get; set; }
        public int ScanTileCap { get; set; }
        public double TimeBudgetMs { get; set; }
        public long CurrentCadenceTicks { get; set; }
        public bool BackoffApplied { get; set; }
        public double LastScanElapsedMs { get; set; }
        public int LastScanTileCount { get; set; }
        public string LastUserCommand { get; set; }
        public bool AutoRescanDisabled { get; set; }

        public PlayerWorldExplorationControlSnapshot()
        {
            ScanMode = PlayerWorldExplorationScanModes.Performance;
            ControlState = PlayerWorldExplorationControlStates.Scanning;
            PairId = string.Empty;
            ScanTileCap = PlayerWorldExplorationConstants.PerformanceScanTileCap;
            TimeBudgetMs = PlayerWorldExplorationConstants.PerformanceScanTimeBudgetMs;
            CurrentCadenceTicks = PlayerWorldExplorationConstants.PerformanceScanCadenceTicks;
            LastUserCommand = string.Empty;
            AutoRescanDisabled = true;
        }
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
        public string ScanMode { get; set; }
        public string ControlState { get; set; }
        public int ScanTileCap { get; set; }
        public double TimeBudgetMs { get; set; }
        public long CurrentCadenceTicks { get; set; }
        public bool BackoffApplied { get; set; }
        public double LastScanElapsedMs { get; set; }
        public int LastScanTileCount { get; set; }
        public string LastUserCommand { get; set; }
        public bool AutoRescanDisabled { get; set; }
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
            ScanMode = PlayerWorldExplorationScanModes.Performance;
            ControlState = PlayerWorldExplorationControlStates.Scanning;
            ScanTileCap = PlayerWorldExplorationConstants.PerformanceScanTileCap;
            TimeBudgetMs = PlayerWorldExplorationConstants.PerformanceScanTimeBudgetMs;
            CurrentCadenceTicks = PlayerWorldExplorationConstants.PerformanceScanCadenceTicks;
            LastUserCommand = string.Empty;
            AutoRescanDisabled = true;
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
        public string ScanMode { get; set; }
        public string ControlState { get; set; }
        public int ScanTileCap { get; set; }
        public double TimeBudgetMs { get; set; }
        public long CurrentCadenceTicks { get; set; }
        public bool BackoffApplied { get; set; }
        public double LastScanElapsedMs { get; set; }
        public int LastScanTileCount { get; set; }
        public string LastUserCommand { get; set; }
        public bool AutoRescanDisabled { get; set; }
        public double RevealedPercent { get; set; }
        public DateTime? LastScanUtc { get; set; }
        public DateTime? LastCompletedScanUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldExplorationScanResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            ScanMode = PlayerWorldExplorationScanModes.Performance;
            ControlState = PlayerWorldExplorationControlStates.Scanning;
            ScanTileCap = PlayerWorldExplorationConstants.PerformanceScanTileCap;
            TimeBudgetMs = PlayerWorldExplorationConstants.PerformanceScanTimeBudgetMs;
            CurrentCadenceTicks = PlayerWorldExplorationConstants.PerformanceScanCadenceTicks;
            LastUserCommand = string.Empty;
            AutoRescanDisabled = true;
        }
    }
}
