using System;

namespace JueMingZ.Automation.Movement
{
    public sealed class MovementContinuousDashDiagnosticInfo
    {
        public bool Enabled { get; set; }
        public string Mode { get; set; }
        public bool LastTriggered { get; set; }
        public int LastTriggerDirection { get; set; }
        public DateTime? LastTriggerUtc { get; set; }
        public string LastDecision { get; set; }
        public string LastSkipReason { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public long LastTick { get; set; }
        public int PendingActionCount { get; set; }
        public string RunningActionKind { get; set; }
        public bool TextInputFocused { get; set; }
        public string TextInputReason { get; set; }
        public bool PlayerControllable { get; set; }
        public bool LeftHeld { get; set; }
        public bool RightHeld { get; set; }
        public int HeldDirection { get; set; }
        public bool HasDashAbility { get; set; }
        public string DashAbilitySource { get; set; }
        public int DashType { get; set; }
        public int DashDelay { get; set; }
        public bool DashCooldownReady { get; set; }
        public bool MountActive { get; set; }
        public int MountType { get; set; }
        public bool MountCanDashKnown { get; set; }
        public bool MountCanDash { get; set; }
        public string CapabilitySummary { get; set; }
        public int ArmedDirection { get; set; }
        public string ArmedCancelReason { get; set; }
        public long ArmedCancelCount { get; set; }
        public bool DashMovementHookInstalled { get; set; }
        public string DashMovementHookMessage { get; set; }
        public bool QueuedPulsePending { get; set; }
        public bool LastPulseApplied { get; set; }
        public int LastPulseDirection { get; set; }
        public DateTime? LastPulseUtc { get; set; }
        public string LastPulseMessage { get; set; }
        public bool LastPulseWasFallback { get; set; }
        public string LastPulseResetMessage { get; set; }
        public string LastCompatError { get; set; }
        public long SubmittedCount { get; set; }
        public long SkippedCount { get; set; }

        public MovementContinuousDashDiagnosticInfo()
        {
            Mode = string.Empty;
            LastDecision = string.Empty;
            LastSkipReason = string.Empty;
            RunningActionKind = string.Empty;
            TextInputReason = string.Empty;
            DashAbilitySource = string.Empty;
            CapabilitySummary = string.Empty;
            ArmedCancelReason = string.Empty;
            DashMovementHookMessage = string.Empty;
            LastPulseMessage = string.Empty;
            LastPulseResetMessage = string.Empty;
            LastCompatError = string.Empty;
            MountType = -1;
        }

        public MovementContinuousDashDiagnosticInfo Clone()
        {
            return new MovementContinuousDashDiagnosticInfo
            {
                Enabled = Enabled,
                Mode = Mode,
                LastTriggered = LastTriggered,
                LastTriggerDirection = LastTriggerDirection,
                LastTriggerUtc = LastTriggerUtc,
                LastDecision = LastDecision,
                LastSkipReason = LastSkipReason,
                LastDecisionUtc = LastDecisionUtc,
                LastTick = LastTick,
                PendingActionCount = PendingActionCount,
                RunningActionKind = RunningActionKind,
                TextInputFocused = TextInputFocused,
                TextInputReason = TextInputReason,
                PlayerControllable = PlayerControllable,
                LeftHeld = LeftHeld,
                RightHeld = RightHeld,
                HeldDirection = HeldDirection,
                HasDashAbility = HasDashAbility,
                DashAbilitySource = DashAbilitySource,
                DashType = DashType,
                DashDelay = DashDelay,
                DashCooldownReady = DashCooldownReady,
                MountActive = MountActive,
                MountType = MountType,
                MountCanDashKnown = MountCanDashKnown,
                MountCanDash = MountCanDash,
                CapabilitySummary = CapabilitySummary,
                ArmedDirection = ArmedDirection,
                ArmedCancelReason = ArmedCancelReason,
                ArmedCancelCount = ArmedCancelCount,
                DashMovementHookInstalled = DashMovementHookInstalled,
                DashMovementHookMessage = DashMovementHookMessage,
                QueuedPulsePending = QueuedPulsePending,
                LastPulseApplied = LastPulseApplied,
                LastPulseDirection = LastPulseDirection,
                LastPulseUtc = LastPulseUtc,
                LastPulseMessage = LastPulseMessage,
                LastPulseWasFallback = LastPulseWasFallback,
                LastPulseResetMessage = LastPulseResetMessage,
                LastCompatError = LastCompatError,
                SubmittedCount = SubmittedCount,
                SkippedCount = SkippedCount
            };
        }
    }
}
