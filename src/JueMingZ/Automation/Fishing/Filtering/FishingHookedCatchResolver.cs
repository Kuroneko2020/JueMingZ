using System;
using System.Globalization;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Fishing.Filtering
{
    internal static class FishingHookedCatchResolver
    {
        private static bool _crateSetsInitialized;
        private static object _isFishingCrateSet;
        private static object _isFishingCrateHardmodeSet;

        public static bool TryResolve(FishingBobberObservation observation, out FishingCatchCandidate candidate)
        {
            candidate = CreateUnknownCandidate();
            if (observation == null)
            {
                return false;
            }

            try
            {
                int localAi1;
                if (!TryConvertLocalAi1(observation.LocalAi1, out localAi1) || localAi1 == 0)
                {
                    return false;
                }

                if (localAi1 > 0)
                {
                    var itemName = ResolveItemName(localAi1);
                    candidate = new FishingCatchCandidate
                    {
                        Kind = FishingCatchKinds.Item,
                        Id = localAi1,
                        DisplayName = itemName,
                        DisplayNameSnapshot = itemName,
                        IsCrate = IsFishingCrateItem(localAi1),
                        IsQuestFish = IsCurrentAnglerQuestFish(localAi1),
                        IsEnemy = false
                    };
                    return true;
                }

                var npcId = -localAi1;
                var npcName = ResolveNpcName(npcId);
                candidate = new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.NPC,
                    Id = npcId,
                    DisplayName = npcName,
                    DisplayNameSnapshot = npcName,
                    IsCrate = false,
                    IsQuestFish = false,
                    IsEnemy = true
                };
                return true;
            }
            catch
            {
                candidate = CreateUnknownCandidate();
                return false;
            }
        }

        private static FishingCatchCandidate CreateUnknownCandidate()
        {
            return new FishingCatchCandidate
            {
                Kind = FishingCatchKinds.Unknown,
                Id = 0,
                DisplayName = "Unknown",
                DisplayNameSnapshot = "Unknown",
                IsCrate = false,
                IsQuestFish = false,
                IsEnemy = false
            };
        }

        private static bool TryConvertLocalAi1(float value, out int result)
        {
            result = 0;
            if (float.IsNaN(value) ||
                float.IsInfinity(value) ||
                value > int.MaxValue ||
                value < int.MinValue)
            {
                return false;
            }

            result = (int)value;
            return true;
        }

        private static string ResolveItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return "Unknown";
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetItemNameValue", new object[] { itemId }, out raw) && raw != null)
            {
                var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return "Item #" + itemId.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveNpcName(int npcId)
        {
            if (npcId <= 0)
            {
                return "NPC #0";
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetNPCNameValue", new object[] { npcId }, out raw) && raw != null)
            {
                var value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            var compatName = InformationNpcNameCompat.ResolveTypeName(npcId);
            if (!string.IsNullOrWhiteSpace(compatName) &&
                !string.Equals(compatName.Trim(), npcId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return compatName.Trim();
            }

            return "NPC #" + npcId.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsFishingCrateItem(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            EnsureFishingCrateSets();
            return ReadBoolArrayValue(_isFishingCrateSet, itemId) ||
                   ReadBoolArrayValue(_isFishingCrateHardmodeSet, itemId);
        }

        private static void EnsureFishingCrateSets()
        {
            if (_crateSetsInitialized)
            {
                return;
            }

            _crateSetsInitialized = true;
            var itemSetsType = InformationReflection.FindType("Terraria.ID.ItemID+Sets");
            _isFishingCrateSet = InformationReflection.GetStaticMember(itemSetsType, "IsFishingCrate");
            _isFishingCrateHardmodeSet = InformationReflection.GetStaticMember(itemSetsType, "IsFishingCrateHardmode");
        }

        private static bool IsCurrentAnglerQuestFish(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            bool finished;
            if (QuestFishStorageCompat.TryIsAnglerQuestFinished(out finished) && finished)
            {
                return false;
            }

            int questFishId;
            string message;
            return QuestFishStorageCompat.TryGetCurrentAnglerQuestFishId(out questFishId, out message) &&
                   questFishId == itemId;
        }

        private static bool ReadBoolArrayValue(object source, int index)
        {
            if (source == null || index < 0)
            {
                return false;
            }

            try
            {
                var raw = InformationReflection.GetIndexedValue(source, index);
                return raw != null && Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }
    }
}
