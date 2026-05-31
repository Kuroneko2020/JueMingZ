using System;

namespace JueMingZ.Config
{
    public static class FishingAutoStoreModes
    {
        public const string All = "All";
        public const string QuestFish = "QuestFish";
        public const string Off = "Off";

        public static string Normalize(string mode, bool legacyQuestFishEnabled)
        {
            if (string.Equals(mode, All, StringComparison.OrdinalIgnoreCase))
            {
                return All;
            }

            if (string.Equals(mode, QuestFish, StringComparison.OrdinalIgnoreCase))
            {
                return QuestFish;
            }

            if (string.Equals(mode, Off, StringComparison.OrdinalIgnoreCase))
            {
                return legacyQuestFishEnabled ? QuestFish : Off;
            }

            return legacyQuestFishEnabled ? QuestFish : Off;
        }

        public static bool IsEnabled(string mode)
        {
            var normalized = Normalize(mode, false);
            return !string.Equals(normalized, Off, StringComparison.OrdinalIgnoreCase);
        }
    }
}
