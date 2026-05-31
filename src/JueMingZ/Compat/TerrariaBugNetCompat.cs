using System;

namespace JueMingZ.Compat
{
    public static class TerrariaBugNetCompat
    {
        public const int BugNetItemType = 1991;
        public const int GoldenBugNetItemType = 3183;
        public const int LavaproofBugNetItemType = 4821;

        public static bool TryResolveCatchToolTier(int itemType, int rawCatchTool, out int catchToolTier)
        {
            if (rawCatchTool > 0)
            {
                catchToolTier = ClampCatchToolTier(rawCatchTool);
                return true;
            }

            switch (itemType)
            {
                case BugNetItemType:
                    catchToolTier = 1;
                    return true;
                case GoldenBugNetItemType:
                    catchToolTier = 2;
                    return true;
                case LavaproofBugNetItemType:
                    catchToolTier = 3;
                    return true;
                default:
                    catchToolTier = 0;
                    return false;
            }
        }

        private static int ClampCatchToolTier(int catchToolTier)
        {
            return Math.Min(Math.Max(0, catchToolTier), 3);
        }
    }
}
