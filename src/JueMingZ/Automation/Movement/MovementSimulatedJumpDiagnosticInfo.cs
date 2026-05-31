using System;

namespace JueMingZ.Automation.Movement
{
    public sealed class MovementSimulatedJumpDiagnosticInfo
    {
        public bool Enabled { get; set; }
        public bool LastTriggered { get; set; }
        public DateTime? LastTriggerUtc { get; set; }
        public string LastDecision { get; set; }
        public string LastSkipReason { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public long LastTick { get; set; }
        public int PendingActionCount { get; set; }
        public string RunningActionKind { get; set; }
        public bool ItemUseBridgeBusy { get; set; }
        public bool TextInputFocused { get; set; }
        public string TextInputReason { get; set; }
        public bool JumpHeld { get; set; }
        public bool DownHeld { get; set; }
        public bool PlayerControllable { get; set; }
        public bool AvailableJumpOpportunity { get; set; }
        public bool GroundedOrSliding { get; set; }
        public bool AerialJumpWindow { get; set; }
        public bool HasAirJump { get; set; }
        public bool HasRocketJump { get; set; }
        public bool HasWingFlight { get; set; }
        public bool MountActive { get; set; }
        public bool MountCanFly { get; set; }
        public bool MountCanFlyKnown { get; set; }
        public string CapabilitySummary { get; set; }
        public long SubmittedCount { get; set; }
        public long SkippedCount { get; set; }

        public MovementSimulatedJumpDiagnosticInfo()
        {
            LastDecision = string.Empty;
            LastSkipReason = string.Empty;
            RunningActionKind = string.Empty;
            TextInputReason = string.Empty;
            CapabilitySummary = string.Empty;
        }

        public MovementSimulatedJumpDiagnosticInfo Clone()
        {
            return new MovementSimulatedJumpDiagnosticInfo
            {
                Enabled = Enabled,
                LastTriggered = LastTriggered,
                LastTriggerUtc = LastTriggerUtc,
                LastDecision = LastDecision,
                LastSkipReason = LastSkipReason,
                LastDecisionUtc = LastDecisionUtc,
                LastTick = LastTick,
                PendingActionCount = PendingActionCount,
                RunningActionKind = RunningActionKind,
                ItemUseBridgeBusy = ItemUseBridgeBusy,
                TextInputFocused = TextInputFocused,
                TextInputReason = TextInputReason,
                JumpHeld = JumpHeld,
                DownHeld = DownHeld,
                PlayerControllable = PlayerControllable,
                AvailableJumpOpportunity = AvailableJumpOpportunity,
                GroundedOrSliding = GroundedOrSliding,
                AerialJumpWindow = AerialJumpWindow,
                HasAirJump = HasAirJump,
                HasRocketJump = HasRocketJump,
                HasWingFlight = HasWingFlight,
                MountActive = MountActive,
                MountCanFly = MountCanFly,
                MountCanFlyKnown = MountCanFlyKnown,
                CapabilitySummary = CapabilitySummary,
                SubmittedCount = SubmittedCount,
                SkippedCount = SkippedCount
            };
        }
    }
}
