using System;

namespace JueMingZ.Automation.AutoRecovery
{
    public sealed class AutoRecoveryState
    {
        public bool AutoHealEnabled { get; set; }
        public bool AutoManaEnabled { get; set; }
        public bool AutoBuffEnabled { get; set; }
        public bool AutoNurseEnabled { get; set; }
        public bool AutoStationBuffEnabled { get; set; }
        public string AutoHealMode { get; set; }
        public string AutoManaMode { get; set; }
        public int AutoHealThresholdPercent { get; set; }
        public int AutoManaThresholdPercent { get; set; }
        public int AutoHealCooldownTicks { get; set; }
        public int AutoManaCooldownTicks { get; set; }
        public int AutoBuffCooldownTicks { get; set; }
        public string LastAutoHealResult { get; set; }
        public string LastAutoManaResult { get; set; }
        public string LastAutoBuffResult { get; set; }
        public string LastAutoNurseResult { get; set; }
        public string LastAutoStationBuffResult { get; set; }
        public long LastAutoHealTick { get; set; }
        public long LastAutoManaTick { get; set; }
        public long LastAutoBuffTick { get; set; }
        public long LastAutoNurseTick { get; set; }
        public long LastAutoStationBuffTick { get; set; }
        public long AutoStationBuffCooldownFastSkipCount { get; set; }
        public long AutoStationBuffActiveBuffFastSkipCount { get; set; }
        public int LastAutoBuffCountBefore { get; set; }
        public int LastAutoBuffCountAfter { get; set; }
        public bool ImmediateBuffReconcileRequested { get; set; }
        public string ImmediateBuffTriggerReason { get; set; }
        public int ImmediateBuffPendingCount { get; set; }
        public int ImmediateBuffInflightCount { get; set; }
        public string QuickHealCapability { get; set; }
        public string QuickManaCapability { get; set; }
        public string QuickBuffCapability { get; set; }
        public Guid LastObservedActionRequestId { get; set; }

        public AutoRecoveryState()
        {
            LastAutoHealResult = string.Empty;
            LastAutoManaResult = string.Empty;
            LastAutoBuffResult = string.Empty;
            LastAutoNurseResult = string.Empty;
            LastAutoStationBuffResult = string.Empty;
            ImmediateBuffTriggerReason = string.Empty;
            AutoHealMode = AutoRecoverySettings.HealModeOff;
            AutoManaMode = AutoRecoverySettings.ManaModeOff;
            LastAutoHealTick = AutoRecoveryService.ForceDueTick;
            LastAutoManaTick = AutoRecoveryService.ForceDueTick;
            LastAutoBuffTick = AutoRecoveryService.ForceDueTick;
            LastAutoNurseTick = AutoRecoveryService.ForceDueTick;
            LastAutoStationBuffTick = AutoRecoveryService.ForceDueTick;
            QuickHealCapability = "UnknownUntilAttempted";
            QuickManaCapability = "UnknownUntilAttempted";
            QuickBuffCapability = "UnknownUntilAttempted";
        }

        public AutoRecoveryState Clone()
        {
            return new AutoRecoveryState
            {
                AutoHealEnabled = AutoHealEnabled,
                AutoManaEnabled = AutoManaEnabled,
                AutoBuffEnabled = AutoBuffEnabled,
                AutoNurseEnabled = AutoNurseEnabled,
                AutoStationBuffEnabled = AutoStationBuffEnabled,
                AutoHealMode = AutoHealMode,
                AutoManaMode = AutoManaMode,
                AutoHealThresholdPercent = AutoHealThresholdPercent,
                AutoManaThresholdPercent = AutoManaThresholdPercent,
                AutoHealCooldownTicks = AutoHealCooldownTicks,
                AutoManaCooldownTicks = AutoManaCooldownTicks,
                AutoBuffCooldownTicks = AutoBuffCooldownTicks,
                LastAutoHealResult = LastAutoHealResult,
                LastAutoManaResult = LastAutoManaResult,
                LastAutoBuffResult = LastAutoBuffResult,
                LastAutoNurseResult = LastAutoNurseResult,
                LastAutoStationBuffResult = LastAutoStationBuffResult,
                LastAutoHealTick = LastAutoHealTick,
                LastAutoManaTick = LastAutoManaTick,
                LastAutoBuffTick = LastAutoBuffTick,
                LastAutoNurseTick = LastAutoNurseTick,
                LastAutoStationBuffTick = LastAutoStationBuffTick,
                AutoStationBuffCooldownFastSkipCount = AutoStationBuffCooldownFastSkipCount,
                AutoStationBuffActiveBuffFastSkipCount = AutoStationBuffActiveBuffFastSkipCount,
                LastAutoBuffCountBefore = LastAutoBuffCountBefore,
                LastAutoBuffCountAfter = LastAutoBuffCountAfter,
                ImmediateBuffReconcileRequested = ImmediateBuffReconcileRequested,
                ImmediateBuffTriggerReason = ImmediateBuffTriggerReason,
                ImmediateBuffPendingCount = ImmediateBuffPendingCount,
                ImmediateBuffInflightCount = ImmediateBuffInflightCount,
                QuickHealCapability = QuickHealCapability,
                QuickManaCapability = QuickManaCapability,
                QuickBuffCapability = QuickBuffCapability,
                LastObservedActionRequestId = LastObservedActionRequestId
            };
        }
    }
}
