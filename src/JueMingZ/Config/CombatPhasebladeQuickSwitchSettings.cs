namespace JueMingZ.Config
{
    public static class CombatPhasebladeQuickSwitchSettings
    {
        public const int MinIntervalTicks = 1;
        public const int MaxIntervalTicks = 30;
        public const int DefaultIntervalTicks = 12;

        public static int NormalizeIntervalTicks(int value)
        {
            if (value <= 0)
            {
                return DefaultIntervalTicks;
            }

            if (value < MinIntervalTicks)
            {
                return MinIntervalTicks;
            }

            return value > MaxIntervalTicks ? MaxIntervalTicks : value;
        }
    }
}
