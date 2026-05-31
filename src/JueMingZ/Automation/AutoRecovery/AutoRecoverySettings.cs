using JueMingZ.Config;
using System;

namespace JueMingZ.Automation.AutoRecovery
{
    public sealed class AutoRecoverySettings
    {
        public const string HealModeOff = "Off";
        public const string HealModeQuick = "Quick";
        public const string HealModeSmart = "Smart";
        public const string ManaModeOff = "Off";
        public const string ManaModeManaFlower = "ManaFlower";
        public const int AutoManaImmediateCooldownTicks = 8;

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

        public static AutoRecoverySettings FromConfig()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            return FromSettings(settings);
        }

        public static AutoRecoverySettings FromSettings(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var healMode = NormalizeHealMode(settings.AutoHealMode, settings.AutoHealEnabled);
            var manaMode = NormalizeManaMode(settings.AutoManaMode, settings.AutoManaEnabled);
            return new AutoRecoverySettings
            {
                AutoHealMode = healMode,
                AutoManaMode = manaMode,
                AutoHealEnabled = !string.Equals(healMode, HealModeOff, StringComparison.OrdinalIgnoreCase),
                AutoManaEnabled = string.Equals(manaMode, ManaModeManaFlower, StringComparison.OrdinalIgnoreCase),
                AutoBuffEnabled = settings.AutoBuffEnabled,
                AutoNurseEnabled = settings.AutoNurseEnabled,
                AutoStationBuffEnabled = settings.AutoStationBuffEnabled,
                AutoHealThresholdPercent = Clamp(settings.AutoHealThresholdPercent <= 0 ? 50 : settings.AutoHealThresholdPercent, 1, 100),
                AutoManaThresholdPercent = Clamp(settings.AutoManaThresholdPercent <= 0 ? 35 : settings.AutoManaThresholdPercent, 1, 100),
                AutoHealCooldownTicks = settings.AutoHealCooldownTicks <= 0 ? 120 : settings.AutoHealCooldownTicks,
                AutoManaCooldownTicks = settings.AutoManaCooldownTicks <= 0
                    ? AutoManaImmediateCooldownTicks
                    : Math.Min(settings.AutoManaCooldownTicks, AutoManaImmediateCooldownTicks),
                AutoBuffCooldownTicks = settings.AutoBuffCooldownTicks <= 0 ? 1800 : settings.AutoBuffCooldownTicks
            };
        }

        public bool AnyEnabled
        {
            get { return AutoHealEnabled || AutoManaEnabled || AutoBuffEnabled || AutoNurseEnabled || AutoStationBuffEnabled; }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static string NormalizeHealMode(string mode, bool legacyEnabled)
        {
            if (string.Equals(mode, HealModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return HealModeOff;
            }

            if (string.Equals(mode, HealModeQuick, StringComparison.OrdinalIgnoreCase))
            {
                return HealModeQuick;
            }

            if (string.Equals(mode, HealModeSmart, StringComparison.OrdinalIgnoreCase))
            {
                return HealModeSmart;
            }

            return legacyEnabled ? HealModeQuick : HealModeOff;
        }

        public static string NormalizeManaMode(string mode, bool legacyEnabled)
        {
            if (string.Equals(mode, ManaModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return ManaModeOff;
            }

            if (string.Equals(mode, ManaModeManaFlower, StringComparison.OrdinalIgnoreCase))
            {
                return ManaModeManaFlower;
            }

            return legacyEnabled ? ManaModeManaFlower : ManaModeOff;
        }
    }
}
