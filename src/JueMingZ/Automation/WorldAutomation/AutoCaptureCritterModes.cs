using System;

namespace JueMingZ.Automation.WorldAutomation
{
    public static class AutoCaptureCritterModes
    {
        public const string Off = "Off";
        public const string Manual = "Manual";
        public const string Auto = "Auto";

        public static string Normalize(string mode)
        {
            return Normalize(mode, false);
        }

        public static string Normalize(string mode, bool legacyEnabled)
        {
            if (string.Equals(mode, Auto, StringComparison.OrdinalIgnoreCase))
            {
                return Auto;
            }

            if (string.Equals(mode, Manual, StringComparison.OrdinalIgnoreCase))
            {
                return Manual;
            }

            if (string.Equals(mode, Off, StringComparison.OrdinalIgnoreCase))
            {
                return Off;
            }

            return legacyEnabled ? Auto : Off;
        }

        public static bool IsEnabled(string mode)
        {
            return !string.Equals(Normalize(mode), Off, StringComparison.Ordinal);
        }

        public static bool IsEnabled(string mode, bool legacyEnabled)
        {
            return !string.Equals(Normalize(mode, legacyEnabled), Off, StringComparison.Ordinal);
        }
    }
}
