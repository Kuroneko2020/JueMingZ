using System;

namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAutomationDecisionDiagnosticInfo
    {
        public string LastDecision { get; set; }
        public string LastSkipReason { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public long LastTick { get; set; }
        public long SubmittedCount { get; set; }
        public long SkippedCount { get; set; }
        public string PolicyDecision { get; set; }
        public string PolicyReason { get; set; }
        public int LastItemType { get; set; }
        public bool LastVanillaAutoReuseEnabled { get; set; }

        public CombatAutomationDecisionDiagnosticInfo()
        {
            LastDecision = string.Empty;
            LastSkipReason = string.Empty;
            PolicyDecision = string.Empty;
            PolicyReason = string.Empty;
        }

        public CombatAutomationDecisionDiagnosticInfo Clone()
        {
            return new CombatAutomationDecisionDiagnosticInfo
            {
                LastDecision = LastDecision,
                LastSkipReason = LastSkipReason,
                LastDecisionUtc = LastDecisionUtc,
                LastTick = LastTick,
                SubmittedCount = SubmittedCount,
                SkippedCount = SkippedCount,
                PolicyDecision = PolicyDecision,
                PolicyReason = PolicyReason,
                LastItemType = LastItemType,
                LastVanillaAutoReuseEnabled = LastVanillaAutoReuseEnabled
            };
        }
    }
}
