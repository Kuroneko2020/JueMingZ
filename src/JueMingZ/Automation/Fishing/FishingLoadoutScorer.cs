using System.Collections.Generic;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Fishing
{
    internal sealed class FishingLoadoutScore
    {
        public int LoadoutIndex { get; set; }
        public int Score { get; set; }
        public string Reason { get; set; }

        public FishingLoadoutScore()
        {
            LoadoutIndex = -1;
            Reason = string.Empty;
        }
    }

    internal static class FishingLoadoutScorer
    {
        public static FishingLoadoutScore Score(object player, int loadoutIndex)
        {
            var score = new FishingLoadoutScore { LoadoutIndex = loadoutIndex };
            IReadOnlyList<object> items;
            if (!FishingLoadoutCompat.TryGetLoadoutArmorItems(player, loadoutIndex, out items) || items == null)
            {
                score.Reason = "loadoutItemsUnavailable";
                return score;
            }

            bool expertMode;
            FishingLoadoutCompat.TryReadExpertMode(out expertMode);
            var informationDeviceFound = false;
            for (var slot = 0; slot < items.Count && slot < 10; slot++)
            {
                var item = items[slot];
                int itemScore;
                string effectGroup;
                string reason;
                if (!FishingEquipmentCatalog.TryScoreItemForSlot(player, item, slot, expertMode, out itemScore, out effectGroup, out reason))
                {
                    continue;
                }

                if (string.Equals(effectGroup, "fishingInformationDevice", System.StringComparison.Ordinal))
                {
                    if (informationDeviceFound)
                    {
                        continue;
                    }

                    informationDeviceFound = true;
                }

                score.Score += itemScore >= 60 && slot >= 3 ? 10 : 5;
            }

            score.Reason = score.Score > 0 ? "score:" + score.Score : "noFishingScore";
            return score;
        }
    }
}
