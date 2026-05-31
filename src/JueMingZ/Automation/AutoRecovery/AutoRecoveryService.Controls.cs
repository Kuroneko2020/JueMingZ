using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.AutoRecovery
{
    public static partial class AutoRecoveryService
    {
        public static bool ToggleAutoHeal()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var current = AutoRecoverySettings.NormalizeHealMode(settings.AutoHealMode, settings.AutoHealEnabled);
            return !string.Equals(SetAutoHealMode(string.Equals(current, AutoRecoverySettings.HealModeOff, StringComparison.OrdinalIgnoreCase)
                ? AutoRecoverySettings.HealModeQuick
                : AutoRecoverySettings.HealModeOff), AutoRecoverySettings.HealModeOff, StringComparison.OrdinalIgnoreCase);
        }

        public static string SetAutoHealMode(string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var normalized = AutoRecoverySettings.NormalizeHealMode(mode, settings.AutoHealEnabled);
            settings.AutoHealMode = normalized;
            settings.AutoHealEnabled = !string.Equals(normalized, AutoRecoverySettings.HealModeOff, StringComparison.OrdinalIgnoreCase);
            ConfigService.SaveAll();
            lock (SyncRoot)
            {
                ApplySettingsLocked(AutoRecoverySettings.FromConfig());
                _lastF5ControlUtc = DateTime.UtcNow;
                if (!string.Equals(normalized, AutoRecoverySettings.HealModeOff, StringComparison.OrdinalIgnoreCase))
                {
                    State.LastAutoHealTick = ForceDueTick;
                }

                State.LastAutoHealResult = string.Equals(normalized, AutoRecoverySettings.HealModeSmart, StringComparison.OrdinalIgnoreCase)
                    ? "Smart heal mode selected; a suitable recovery potion will be used when missing life reaches its safe threshold."
                    : (string.Equals(normalized, AutoRecoverySettings.HealModeQuick, StringComparison.OrdinalIgnoreCase)
                        ? "Quick heal mode selected; the strongest available recovery potion will be used when life is missing."
                        : "AutoHeal disabled from F5 panel.");
            }

            return normalized;
        }

        public static bool ToggleAutoMana()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var current = AutoRecoverySettings.NormalizeManaMode(settings.AutoManaMode, settings.AutoManaEnabled);
            return !string.Equals(SetAutoManaMode(string.Equals(current, AutoRecoverySettings.ManaModeOff, StringComparison.OrdinalIgnoreCase)
                ? AutoRecoverySettings.ManaModeManaFlower
                : AutoRecoverySettings.ManaModeOff), AutoRecoverySettings.ManaModeOff, StringComparison.OrdinalIgnoreCase);
        }

        public static string SetAutoManaMode(string mode)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var normalized = AutoRecoverySettings.NormalizeManaMode(mode, settings.AutoManaEnabled);
            settings.AutoManaMode = normalized;
            settings.AutoManaEnabled = string.Equals(normalized, AutoRecoverySettings.ManaModeManaFlower, StringComparison.OrdinalIgnoreCase);
            ConfigService.SaveAll();
            lock (SyncRoot)
            {
                ApplySettingsLocked(AutoRecoverySettings.FromConfig());
                _lastF5ControlUtc = DateTime.UtcNow;
                if (settings.AutoManaEnabled)
                {
                    State.LastAutoManaTick = ForceDueTick;
                }

                State.LastAutoManaResult = settings.AutoManaEnabled
                    ? "Mana flower logic selected; an available mana potion will be used when the selected mana weapon cannot be used with current mana and mana can be restored."
                    : "AutoMana disabled from F5 panel.";
            }

            return normalized;
        }

        public static bool ToggleAutoBuff()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            settings.AutoBuffEnabled = !settings.AutoBuffEnabled;
            ConfigService.SaveAll();
            lock (SyncRoot)
            {
                ApplySettingsLocked(AutoRecoverySettings.FromConfig());
                _lastF5ControlUtc = DateTime.UtcNow;
                State.LastAutoBuffResult = settings.AutoBuffEnabled
                    ? (BuffPotionWhitelistService.Count <= 0
                        ? "AutoBuff enabled, but whitelist is empty; idle and will not consume buff potions."
                        : "AutoBuff strategy enabled; it will only use whitelisted buff potions.")
                    : "Disabled from F5 panel.";
                if (settings.AutoBuffEnabled)
                {
                    RequestImmediateAutoBuffReconcileLocked("AutoBuffEnabled");
                }
            }

            return settings.AutoBuffEnabled;
        }

        public static bool SetAutoNurseEnabled(bool enabled)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            settings.AutoNurseEnabled = enabled;
            ConfigService.SaveAll();
            lock (SyncRoot)
            {
                ApplySettingsLocked(AutoRecoverySettings.FromConfig());
                _lastF5ControlUtc = DateTime.UtcNow;
                if (enabled)
                {
                    State.LastAutoNurseTick = ForceDueTick;
                    State.LastAutoNurseResult = "AutoNurse enabled; reachable nurse healing will be attempted when life is missing or removable debuffs are present.";
                }
                else
                {
                    State.LastAutoNurseResult = "AutoNurse disabled from F5 panel.";
                }
            }

            return enabled;
        }

        public static bool SetAutoStationBuffEnabled(bool enabled)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            settings.AutoStationBuffEnabled = enabled;
            ConfigService.SaveAll();
            lock (SyncRoot)
            {
                ApplySettingsLocked(AutoRecoverySettings.FromConfig());
                _lastF5ControlUtc = DateTime.UtcNow;
                if (enabled)
                {
                    State.LastAutoStationBuffTick = ForceDueTick;
                    State.LastAutoStationBuffResult = "AutoStationBuff enabled; reachable buff furniture will be interacted with when its buff is missing.";
                }
                else
                {
                    State.LastAutoStationBuffResult = "AutoStationBuff disabled from F5 panel.";
                }
            }

            return enabled;
        }

        public static void RequestImmediateAutoBuffReconcile(string triggerReason)
        {
            lock (SyncRoot)
            {
                RequestImmediateAutoBuffReconcileLocked(triggerReason);
            }
        }

        private static void RequestImmediateAutoBuffReconcileLocked(string triggerReason)
        {
            var reason = string.IsNullOrWhiteSpace(triggerReason) ? "Unknown" : triggerReason;
            _immediateBuffReconcileRequested = true;
            if (string.IsNullOrWhiteSpace(_immediateBuffTriggerReason))
            {
                _immediateBuffTriggerReason = reason;
            }
            else if (_immediateBuffTriggerReason.IndexOf(reason, StringComparison.OrdinalIgnoreCase) < 0)
            {
                _immediateBuffTriggerReason += "+" + reason;
            }

            State.ImmediateBuffReconcileRequested = true;
            State.ImmediateBuffTriggerReason = _immediateBuffTriggerReason;
        }

        private static void ApplySettingsLocked(AutoRecoverySettings settings)
        {
            if (settings == null)
            {
                return;
            }

            State.AutoHealEnabled = settings.AutoHealEnabled;
            State.AutoManaEnabled = settings.AutoManaEnabled;
            State.AutoBuffEnabled = settings.AutoBuffEnabled;
            State.AutoNurseEnabled = settings.AutoNurseEnabled;
            State.AutoStationBuffEnabled = settings.AutoStationBuffEnabled;
            State.AutoHealMode = settings.AutoHealMode;
            State.AutoManaMode = settings.AutoManaMode;
            State.AutoHealThresholdPercent = settings.AutoHealThresholdPercent;
            State.AutoManaThresholdPercent = settings.AutoManaThresholdPercent;
            State.AutoHealCooldownTicks = settings.AutoHealCooldownTicks;
            State.AutoManaCooldownTicks = settings.AutoManaCooldownTicks;
            State.AutoBuffCooldownTicks = settings.AutoBuffCooldownTicks;
        }

    }
}
