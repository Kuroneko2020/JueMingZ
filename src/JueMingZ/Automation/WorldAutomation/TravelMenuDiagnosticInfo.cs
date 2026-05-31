using System;

namespace JueMingZ.Automation.WorldAutomation
{
    public sealed class TravelMenuDiagnosticInfo
    {
        public bool Enabled { get; set; }
        public bool SessionActive { get; set; }
        public bool SaveGuardHookInstalled { get; set; }
        public string SaveGuardHookMessage { get; set; }
        public bool CreativeUiHookInstalled { get; set; }
        public string CreativeUiHookMessage { get; set; }
        public bool ScopedPowerHookInstalled { get; set; }
        public string ScopedPowerHookMessage { get; set; }
        public string PlayerPath { get; set; }
        public string WorldPath { get; set; }
        public int OriginalPlayerDifficulty { get; set; }
        public int OriginalWorldGameMode { get; set; }
        public int OriginalMainGameMode { get; set; }
        public string LastDecision { get; set; }
        public string LastReason { get; set; }
        public string LastMessage { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public long ApplyCount { get; set; }
        public long RestoreCount { get; set; }
        public long SaveGuardCount { get; set; }
        public long ScopedApplyCount { get; set; }
        public long ScopedRestoreCount { get; set; }
        public long ScopedCleanupCount { get; set; }

        public TravelMenuDiagnosticInfo()
        {
            SaveGuardHookMessage = string.Empty;
            CreativeUiHookMessage = string.Empty;
            ScopedPowerHookMessage = string.Empty;
            PlayerPath = string.Empty;
            WorldPath = string.Empty;
            LastDecision = string.Empty;
            LastReason = string.Empty;
            LastMessage = string.Empty;
        }

        public TravelMenuDiagnosticInfo Clone()
        {
            return new TravelMenuDiagnosticInfo
            {
                Enabled = Enabled,
                SessionActive = SessionActive,
                SaveGuardHookInstalled = SaveGuardHookInstalled,
                SaveGuardHookMessage = SaveGuardHookMessage,
                CreativeUiHookInstalled = CreativeUiHookInstalled,
                CreativeUiHookMessage = CreativeUiHookMessage,
                ScopedPowerHookInstalled = ScopedPowerHookInstalled,
                ScopedPowerHookMessage = ScopedPowerHookMessage,
                PlayerPath = PlayerPath,
                WorldPath = WorldPath,
                OriginalPlayerDifficulty = OriginalPlayerDifficulty,
                OriginalWorldGameMode = OriginalWorldGameMode,
                OriginalMainGameMode = OriginalMainGameMode,
                LastDecision = LastDecision,
                LastReason = LastReason,
                LastMessage = LastMessage,
                LastDecisionUtc = LastDecisionUtc,
                ApplyCount = ApplyCount,
                RestoreCount = RestoreCount,
                SaveGuardCount = SaveGuardCount,
                ScopedApplyCount = ScopedApplyCount,
                ScopedRestoreCount = ScopedRestoreCount,
                ScopedCleanupCount = ScopedCleanupCount
            };
        }
    }
}
