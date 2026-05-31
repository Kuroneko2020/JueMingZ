using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.BuffAndRecovery
{
    public sealed class BuffPotionScanResult
    {
        public DateTime ScannedUtc { get; set; }
        public List<BuffPotionCandidate> Candidates { get; private set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public bool PlayerAvailable { get; set; }
        public string NetworkMode { get; set; }
        public bool VoidBagScanned { get; set; }
        public bool UnsupportedConflictCheck { get; set; }

        public BuffPotionScanResult()
        {
            ScannedUtc = DateTime.UtcNow;
            Candidates = new List<BuffPotionCandidate>();
            Message = string.Empty;
            Error = string.Empty;
            NetworkMode = string.Empty;
        }
    }
}
