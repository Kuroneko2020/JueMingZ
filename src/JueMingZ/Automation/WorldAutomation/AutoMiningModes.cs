using System;

namespace JueMingZ.Automation.WorldAutomation
{
    public static class AutoMiningModes
    {
        public const string Off = "Off";
        public const string Hotkey = "Hotkey";
        public const string Auto = "Auto";

        public static string Normalize(string mode)
        {
            if (string.Equals(mode, Hotkey, StringComparison.OrdinalIgnoreCase))
            {
                return Hotkey;
            }

            if (string.Equals(mode, Auto, StringComparison.OrdinalIgnoreCase))
            {
                return Auto;
            }

            return Off;
        }

        public static bool IsEnabled(string mode)
        {
            return !string.Equals(Normalize(mode), Off, StringComparison.OrdinalIgnoreCase);
        }
    }
}
