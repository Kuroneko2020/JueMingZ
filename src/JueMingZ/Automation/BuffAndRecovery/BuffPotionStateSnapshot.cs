namespace JueMingZ.Automation.BuffAndRecovery
{
    // This snapshot reports scan state only; request admission and executor results own the real buff-use state machine.
    public sealed class BuffPotionStateSnapshot
    {
        public int CandidateCount { get; set; }
        public int WhitelistCount { get; set; }
        public int SelectedCandidateIndex { get; set; }
        public string SelectedCandidateItemName { get; set; }
        public string SelectedCandidateBuffName { get; set; }
        public string LastBuffPotionResult { get; set; }
        public string LastBuffPotionExecutionMode { get; set; }
        public string LastBuffPotionNetworkMode { get; set; }
        public string LastBuffPotionSyncResult { get; set; }
        public string LastScanMessage { get; set; }

        public BuffPotionStateSnapshot()
        {
            SelectedCandidateIndex = -1;
            SelectedCandidateItemName = string.Empty;
            SelectedCandidateBuffName = string.Empty;
            LastBuffPotionResult = string.Empty;
            LastBuffPotionExecutionMode = string.Empty;
            LastBuffPotionNetworkMode = string.Empty;
            LastBuffPotionSyncResult = string.Empty;
            LastScanMessage = string.Empty;
        }
    }
}
