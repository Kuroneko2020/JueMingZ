using System;

namespace JueMingZ.Config
{
    public static class MovementContinuousDashModes
    {
        public const string Off = "Off";
        public const string HoldDirection = "HoldDirection";
        public const string DoubleTapAndHold = "DoubleTapAndHold";

        public static string Normalize(string mode)
        {
            if (string.Equals(mode, DoubleTapAndHold, StringComparison.OrdinalIgnoreCase))
            {
                return DoubleTapAndHold;
            }

            return HoldDirection;
        }

        public static string DisplayName(string mode)
        {
            if (string.Equals(mode, Off, StringComparison.OrdinalIgnoreCase))
            {
                return "关闭";
            }

            return string.Equals(Normalize(mode), DoubleTapAndHold, StringComparison.Ordinal)
                ? "双击并按住冲刺"
                : "按住方向键冲刺";
        }
    }
}
