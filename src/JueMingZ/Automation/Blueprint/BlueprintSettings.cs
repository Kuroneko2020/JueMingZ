namespace JueMingZ.Automation.Blueprint
{
    public static class BlueprintSettings
    {
        public const int DefaultToolItemId = 23;
        public const int MinToolItemId = 1;
        public const int MaxToolItemId = 9999;

        public static int NormalizeToolItemId(int itemId)
        {
            if (itemId < MinToolItemId || itemId > MaxToolItemId)
            {
                return DefaultToolItemId;
            }

            return itemId;
        }

        public static int AdjustToolItemId(int itemId, int delta)
        {
            var normalized = NormalizeToolItemId(itemId);
            var next = normalized + delta;
            if (next < MinToolItemId)
            {
                return MinToolItemId;
            }

            return next > MaxToolItemId ? MaxToolItemId : next;
        }
    }
}
