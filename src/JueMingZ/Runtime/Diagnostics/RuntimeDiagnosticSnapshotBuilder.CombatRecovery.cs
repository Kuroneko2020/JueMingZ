using System;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Runtime
{
    internal static partial class RuntimeDiagnosticSnapshotBuilder
    {
        private static void WriteCombatAndRecovery(DiagnosticSnapshot snapshot, RuntimeDiagnosticSnapshotSource source)
        {
            var autoFacing = CombatAutoFacingService.GetDiagnostics();
            var perfectRevolver = CombatPerfectRevolverService.GetDiagnostics();
            var flailCombo = CombatFlailComboService.GetDiagnostics();
            var phasebladeQuickSwitch = CombatPhasebladeQuickSwitchRuntimeService.GetDiagnostics();
            var phasebladeQuickSwitchBridge = PhasebladeQuickSwitchBridge.GetSnapshot();
            var itemCheckAutoClicker = CombatItemCheckAutoClickService.GetDiagnostics();
            var magicStringClicker = CombatMagicStringClickerService.GetDiagnostics();
            var autoBossDamageReport = CombatAutoBossDamageReportService.GetDiagnostics();
            var autoRecovery = AutoRecoveryService.GetStateSnapshot();
            var stationBuff = StationBuffCompat.GetDiagnostics();
            var settingsSnapshot = source.SettingsSnapshot;
            var phasebladeQuickSwitchEnabled = settingsSnapshot != null && settingsSnapshot.CombatPhasebladeQuickSwitchEnabled;
            var phasebladeQuickSwitchIntervalTicks = phasebladeQuickSwitch != null && phasebladeQuickSwitch.IntervalTicks > 0
                ? phasebladeQuickSwitch.IntervalTicks
                : settingsSnapshot == null
                    ? CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks
                    : settingsSnapshot.CombatPhasebladeQuickSwitchIntervalTicks;

            snapshot.CombatAutoFacingEnabled = autoFacing != null && autoFacing.Enabled;
            snapshot.CombatAutoFacingLastDecision = autoFacing == null ? string.Empty : autoFacing.LastDecision;
            snapshot.CombatAutoFacingLastSkipReason = autoFacing == null ? string.Empty : autoFacing.LastSkipReason;
            snapshot.CombatAutoFacingLastDecisionUtc = autoFacing == null ? null : autoFacing.LastDecisionUtc;
            snapshot.CombatAutoFacingLastTick = autoFacing == null ? 0 : autoFacing.LastTick;
            snapshot.CombatAutoFacingSelectedSlot = autoFacing == null ? -1 : autoFacing.SelectedSlot;
            snapshot.CombatAutoFacingItemType = autoFacing == null ? 0 : autoFacing.ItemType;
            snapshot.CombatAutoFacingItemName = autoFacing == null ? string.Empty : autoFacing.ItemName;
            snapshot.CombatAutoFacingCurrentDirection = autoFacing == null ? 0 : autoFacing.CurrentDirection;
            snapshot.CombatAutoFacingDesiredDirection = autoFacing == null ? 0 : autoFacing.DesiredDirection;
            snapshot.CombatAutoFacingTargetSource = autoFacing == null ? string.Empty : autoFacing.TargetSource;
            snapshot.CombatAutoFacingTargetWhoAmI = autoFacing == null ? -1 : autoFacing.TargetWhoAmI;
            snapshot.CombatAutoFacingTargetType = autoFacing == null ? 0 : autoFacing.TargetType;
            snapshot.CombatAutoFacingTargetName = autoFacing == null ? string.Empty : autoFacing.TargetName;
            snapshot.CombatAutoFacingSubmittedCount = autoFacing == null ? 0 : autoFacing.SubmittedCount;
            snapshot.CombatAutoFacingSkippedCount = autoFacing == null ? 0 : autoFacing.SkippedCount;
            snapshot.CombatPerfectRevolverLastDecision = perfectRevolver == null ? string.Empty : perfectRevolver.LastDecision;
            snapshot.CombatPerfectRevolverLastSkipReason = perfectRevolver == null ? string.Empty : perfectRevolver.LastSkipReason;
            snapshot.CombatPerfectRevolverLastDecisionUtc = perfectRevolver == null ? null : perfectRevolver.LastDecisionUtc;
            snapshot.CombatFlailComboEnabled = flailCombo != null && flailCombo.Enabled;
            snapshot.CombatFlailComboRightHeld = flailCombo != null && flailCombo.RightHeld;
            snapshot.CombatFlailComboEligible = flailCombo != null && flailCombo.Eligible;
            snapshot.CombatFlailComboLastDecision = flailCombo == null ? string.Empty : flailCombo.LastDecision;
            snapshot.CombatFlailComboLastReason = flailCombo == null ? string.Empty : flailCombo.LastReason;
            snapshot.CombatFlailComboLastDecisionUtc = flailCombo == null ? null : flailCombo.LastDecisionUtc;
            snapshot.CombatFlailComboItemType = flailCombo == null ? 0 : flailCombo.ItemType;
            snapshot.CombatFlailComboProjectileType = flailCombo == null ? 0 : flailCombo.ProjectileType;
            snapshot.CombatFlailComboProjectileAi0 = flailCombo == null ? 0d : flailCombo.ProjectileAi0;
            snapshot.CombatFlailComboHitDetected = flailCombo != null && flailCombo.HitDetected;
            snapshot.CombatFlailComboCollisionDetected = flailCombo != null && flailCombo.CollisionDetected;
            snapshot.CombatFlailComboVanillaRightClickBlocked = flailCombo != null && flailCombo.VanillaRightClickBlocked;
            snapshot.CombatFlailComboUiBlocked = flailCombo != null && flailCombo.UiBlocked;
            snapshot.CombatFlailComboScopedPress = flailCombo != null && flailCombo.ScopedPress;
            snapshot.CombatFlailComboScopedRelease = flailCombo != null && flailCombo.ScopedRelease;
            snapshot.CombatFlailComboRestoreOk = flailCombo == null || flailCombo.RestoreOk;
            snapshot.CombatFlailComboAppliedCount = flailCombo == null ? 0 : flailCombo.AppliedCount;
            snapshot.CombatFlailComboSkippedCount = flailCombo == null ? 0 : flailCombo.SkippedCount;
            snapshot.CombatPhasebladeQuickSwitchEnabled = phasebladeQuickSwitchEnabled;
            snapshot.CombatPhasebladeQuickSwitchRightHeld = phasebladeQuickSwitchEnabled && phasebladeQuickSwitch != null && phasebladeQuickSwitch.RightHeld;
            snapshot.CombatPhasebladeQuickSwitchEligible = phasebladeQuickSwitchEnabled && phasebladeQuickSwitch != null && phasebladeQuickSwitch.Eligible;
            snapshot.CombatPhasebladeQuickSwitchLastDecision = phasebladeQuickSwitchEnabled
                ? phasebladeQuickSwitch == null ? string.Empty : phasebladeQuickSwitch.LastDecision
                : "disabled";
            snapshot.CombatPhasebladeQuickSwitchLastReason = phasebladeQuickSwitchEnabled
                ? phasebladeQuickSwitch == null ? string.Empty : phasebladeQuickSwitch.LastReason
                : "disabled";
            snapshot.CombatPhasebladeQuickSwitchLastDecisionUtc = phasebladeQuickSwitch == null ? null : phasebladeQuickSwitch.LastDecisionUtc;
            snapshot.CombatPhasebladeQuickSwitchCurrentSlot = phasebladeQuickSwitchEnabled && phasebladeQuickSwitch != null ? phasebladeQuickSwitch.CurrentSlot : -1;
            snapshot.CombatPhasebladeQuickSwitchNextSlot = phasebladeQuickSwitchEnabled && phasebladeQuickSwitch != null ? phasebladeQuickSwitch.NextSlot : -1;
            snapshot.CombatPhasebladeQuickSwitchEligibleSlotCount = phasebladeQuickSwitchEnabled && phasebladeQuickSwitch != null ? phasebladeQuickSwitch.EligibleSlotCount : 0;
            snapshot.CombatPhasebladeQuickSwitchIntervalTicks = phasebladeQuickSwitchIntervalTicks;
            snapshot.CombatPhasebladeQuickSwitchScopedPress = phasebladeQuickSwitchBridge != null &&
                phasebladeQuickSwitchBridge.LastAppliedTick != long.MinValue &&
                phasebladeQuickSwitchBridge.LastAppliedPress;
            snapshot.CombatPhasebladeQuickSwitchScopedRelease = phasebladeQuickSwitchBridge != null &&
                phasebladeQuickSwitchBridge.LastAppliedTick != long.MinValue &&
                !phasebladeQuickSwitchBridge.LastAppliedPress;
            snapshot.CombatPhasebladeQuickSwitchRestoreOk = phasebladeQuickSwitchBridge == null || phasebladeQuickSwitchBridge.LastRestoreSucceeded;
            snapshot.CombatPhasebladeQuickSwitchAppliedCount = phasebladeQuickSwitchBridge == null ? 0 : phasebladeQuickSwitchBridge.ApplyCount;
            snapshot.CombatPhasebladeQuickSwitchSkippedCount = phasebladeQuickSwitch == null ? 0 : phasebladeQuickSwitch.SkippedCount;
            snapshot.CombatItemCheckAutoClickerLastDecision = itemCheckAutoClicker == null ? string.Empty : itemCheckAutoClicker.LastDecision;
            snapshot.CombatItemCheckAutoClickerLastReason = itemCheckAutoClicker == null ? string.Empty : itemCheckAutoClicker.LastReason;
            snapshot.CombatItemCheckAutoClickerLastDecisionUtc = itemCheckAutoClicker == null ? null : itemCheckAutoClicker.LastDecisionUtc;
            snapshot.CombatItemCheckAutoClickerLastItemType = itemCheckAutoClicker == null ? 0 : itemCheckAutoClicker.LastItemType;
            snapshot.CombatItemCheckAutoClickerVanillaAutoReuseAllAvailable = itemCheckAutoClicker != null && itemCheckAutoClicker.LastVanillaAutoReuseAllAvailable;
            snapshot.CombatItemCheckAutoClickerVanillaAutoReuseAllWeapons = itemCheckAutoClicker != null && itemCheckAutoClicker.LastVanillaAutoReuseAllWeapons;
            snapshot.CombatItemCheckAutoClickerScopedPress = itemCheckAutoClicker != null && itemCheckAutoClicker.LastScopedPress;
            snapshot.CombatItemCheckAutoClickerScopedRelease = itemCheckAutoClicker != null && itemCheckAutoClicker.LastScopedRelease;
            snapshot.CombatItemCheckAutoClickerRestored = itemCheckAutoClicker != null && itemCheckAutoClicker.LastRestored;
            snapshot.CombatItemCheckAutoClickerAppliedCount = itemCheckAutoClicker == null ? 0 : itemCheckAutoClicker.AppliedCount;
            snapshot.CombatItemCheckAutoClickerSkippedCount = itemCheckAutoClicker == null ? 0 : itemCheckAutoClicker.SkippedCount;
            snapshot.CombatMagicStringClickerLastDecision = magicStringClicker == null ? string.Empty : magicStringClicker.LastDecision;
            snapshot.CombatMagicStringClickerLastSkipReason = magicStringClicker == null ? string.Empty : magicStringClicker.LastSkipReason;
            snapshot.CombatMagicStringClickerLastDecisionUtc = magicStringClicker == null ? null : magicStringClicker.LastDecisionUtc;
            snapshot.CombatAutoBossDamageReportEnabled = settingsSnapshot != null && settingsSnapshot.CombatAutoBossDamageReportEnabled;
            snapshot.CombatAutoBossDamageReportLastDecision = autoBossDamageReport == null ? string.Empty : autoBossDamageReport.LastDecision;
            snapshot.CombatAutoBossDamageReportLastReason = autoBossDamageReport == null ? string.Empty : autoBossDamageReport.LastReason;
            snapshot.CombatAutoBossDamageReportLastDecisionUtc = autoBossDamageReport == null ? null : autoBossDamageReport.LastDecisionUtc;
            snapshot.CombatAutoBossDamageReportRecentAttemptCount = autoBossDamageReport == null ? 0 : autoBossDamageReport.LastRecentAttemptCount;
            snapshot.CombatAutoBossDamageReportNewAttemptCount = autoBossDamageReport == null ? 0 : autoBossDamageReport.LastNewAttemptCount;
            snapshot.CombatAutoBossDamageReportLastAttemptKey = autoBossDamageReport == null ? 0 : autoBossDamageReport.LastAttemptKey;
            snapshot.CombatAutoBossDamageReportLastSendAttempted = autoBossDamageReport != null && autoBossDamageReport.LastSendAttempted;
            snapshot.CombatAutoBossDamageReportLastSendSucceeded = autoBossDamageReport != null && autoBossDamageReport.LastSendSucceeded;
            snapshot.CombatAutoBossDamageReportLastFailureReason = autoBossDamageReport == null ? string.Empty : autoBossDamageReport.LastFailureReason;
            snapshot.CombatAutoBossDamageReportSentCount = autoBossDamageReport == null ? 0 : autoBossDamageReport.SentCount;
            snapshot.CombatAutoBossDamageReportSkippedCount = autoBossDamageReport == null ? 0 : autoBossDamageReport.SkippedCount;
            snapshot.AutoHealEnabled = autoRecovery.AutoHealEnabled;
            snapshot.AutoManaEnabled = autoRecovery.AutoManaEnabled;
            snapshot.AutoBuffEnabled = autoRecovery.AutoBuffEnabled;
            snapshot.AutoNurseEnabled = autoRecovery.AutoNurseEnabled;
            snapshot.AutoStationBuffEnabled = autoRecovery.AutoStationBuffEnabled;
            snapshot.AutoHealMode = autoRecovery.AutoHealMode;
            snapshot.AutoManaMode = autoRecovery.AutoManaMode;
            snapshot.AutoHealThresholdPercent = autoRecovery.AutoHealThresholdPercent;
            snapshot.AutoManaThresholdPercent = autoRecovery.AutoManaThresholdPercent;
            snapshot.AutoHealCooldownTicks = autoRecovery.AutoHealCooldownTicks;
            snapshot.AutoManaCooldownTicks = autoRecovery.AutoManaCooldownTicks;
            snapshot.AutoBuffCooldownTicks = autoRecovery.AutoBuffCooldownTicks;
            snapshot.LastAutoHealResult = autoRecovery.LastAutoHealResult;
            snapshot.LastAutoManaResult = autoRecovery.LastAutoManaResult;
            snapshot.LastAutoBuffResult = autoRecovery.LastAutoBuffResult;
            snapshot.LastAutoNurseResult = autoRecovery.LastAutoNurseResult;
            snapshot.LastAutoStationBuffResult = autoRecovery.LastAutoStationBuffResult;
            snapshot.LastAutoHealTick = autoRecovery.LastAutoHealTick;
            snapshot.LastAutoManaTick = autoRecovery.LastAutoManaTick;
            snapshot.LastAutoBuffTick = autoRecovery.LastAutoBuffTick;
            snapshot.LastAutoNurseTick = autoRecovery.LastAutoNurseTick;
            snapshot.LastAutoStationBuffTick = autoRecovery.LastAutoStationBuffTick;
            snapshot.AutoStationBuffCooldownFastSkipCount = autoRecovery.AutoStationBuffCooldownFastSkipCount;
            snapshot.AutoStationBuffActiveBuffFastSkipCount = autoRecovery.AutoStationBuffActiveBuffFastSkipCount;
            snapshot.AutoStationBuffScanCount = stationBuff == null ? 0 : stationBuff.ScanCount;
            snapshot.AutoStationBuffScanCacheHitCount = stationBuff == null ? 0 : stationBuff.CacheHitCount;
            snapshot.AutoStationBuffScanCacheMissCount = stationBuff == null ? 0 : stationBuff.CacheMissCount;
            snapshot.AutoStationBuffTilesVisitedLast = stationBuff == null ? 0 : stationBuff.TilesVisitedLast;
            snapshot.AutoStationBuffLastScanMs = stationBuff == null ? 0d : stationBuff.LastScanMs;
            snapshot.AutoStationBuffTileFastPathStatus = stationBuff == null ? string.Empty : stationBuff.TileFastPathStatus ?? string.Empty;
            snapshot.AutoStationBuffLastDecision = stationBuff == null ? string.Empty : stationBuff.LastDecision ?? string.Empty;
            snapshot.LastAutoBuffCountBefore = autoRecovery.LastAutoBuffCountBefore;
            snapshot.LastAutoBuffCountAfter = autoRecovery.LastAutoBuffCountAfter;
            snapshot.QuickHealCapability = autoRecovery.QuickHealCapability;
            snapshot.QuickManaCapability = autoRecovery.QuickManaCapability;
            snapshot.QuickBuffCapability = autoRecovery.QuickBuffCapability;
            snapshot.LastError = RuntimeDiagnostics.LastError;
        }
    }
}
