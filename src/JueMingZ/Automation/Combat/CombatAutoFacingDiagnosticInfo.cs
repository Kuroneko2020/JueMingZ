using System;

namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAutoFacingDiagnosticInfo
    {
        public bool Enabled { get; set; }
        public string LastDecision { get; set; }
        public string LastSkipReason { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public long LastTick { get; set; }
        public int PendingActionCount { get; set; }
        public string RunningActionKind { get; set; }
        public bool ItemUseBridgeBusy { get; set; }
        public int SelectedSlot { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int CurrentDirection { get; set; }
        public int DesiredDirection { get; set; }
        public string TargetSource { get; set; }
        public int TargetWhoAmI { get; set; }
        public int TargetType { get; set; }
        public string TargetName { get; set; }
        public long SubmittedCount { get; set; }
        public long SkippedCount { get; set; }

        public CombatAutoFacingDiagnosticInfo()
        {
            LastDecision = string.Empty;
            LastSkipReason = string.Empty;
            RunningActionKind = string.Empty;
            ItemName = string.Empty;
            TargetSource = string.Empty;
            TargetName = string.Empty;
            SelectedSlot = -1;
            TargetWhoAmI = -1;
        }

        public CombatAutoFacingDiagnosticInfo Clone()
        {
            return new CombatAutoFacingDiagnosticInfo
            {
                Enabled = Enabled,
                LastDecision = LastDecision,
                LastSkipReason = LastSkipReason,
                LastDecisionUtc = LastDecisionUtc,
                LastTick = LastTick,
                PendingActionCount = PendingActionCount,
                RunningActionKind = RunningActionKind,
                ItemUseBridgeBusy = ItemUseBridgeBusy,
                SelectedSlot = SelectedSlot,
                ItemType = ItemType,
                ItemName = ItemName,
                CurrentDirection = CurrentDirection,
                DesiredDirection = DesiredDirection,
                TargetSource = TargetSource,
                TargetWhoAmI = TargetWhoAmI,
                TargetType = TargetType,
                TargetName = TargetName,
                SubmittedCount = SubmittedCount,
                SkippedCount = SkippedCount
            };
        }
    }
}
