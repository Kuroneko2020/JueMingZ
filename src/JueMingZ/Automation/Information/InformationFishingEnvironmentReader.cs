using System;

namespace JueMingZ.Automation.Information
{
    // Environment reads build display snapshots only; they must not correct player, bait, or world fishing state.
    internal static class InformationFishingEnvironmentReader
    {
        public static FishingEnvironmentSnapshot ReadEarlyEnvironmentSnapshot(InformationWorldContext context)
        {
            var snapshot = new FishingEnvironmentSnapshot();
            FillDisplayPoleAndBait(
                context,
                ref snapshot.PolePower,
                ref snapshot.PoleItemType,
                ref snapshot.BaitPower,
                ref snapshot.BaitItemType);
            snapshot.QuestFish = ReadQuestFishItem(context);
            return snapshot;
        }

        public static object ReadFishingConditionsForDisplay(InformationWorldContext context)
        {
            InformationFishingCatchDiagnostics.IncrementConditionsRead();
            var raw = context == null
                ? null
                : InformationFishingCatchResolver.InvokeInstance(context.LocalPlayer, "GetFishingConditions", null);
            return EnsureFishingConditionsForDisplay(context, raw);
        }

        public static FishingEnvironmentSnapshot ReadResolvedEnvironmentSnapshot(
            InformationWorldContext context,
            object fishingConditions,
            int questFish)
        {
            return new FishingEnvironmentSnapshot
            {
                FinalFishingLevel = InformationFishingCatchResolver.ReadInt(fishingConditions, "FinalFishingLevel", 0),
                BaitItemType = InformationFishingCatchResolver.ReadInt(fishingConditions, "BaitItemType", 0),
                BaitPower = InformationFishingCatchResolver.ReadInt(fishingConditions, "BaitPower", 0),
                PoleItemType = InformationFishingCatchResolver.ReadInt(fishingConditions, "PoleItemType", 0),
                PolePower = InformationFishingCatchResolver.ReadInt(fishingConditions, "PolePower", 0),
                CanFishInLava = CanFishInLava(context, fishingConditions),
                QuestFish = questFish
            };
        }

        public static int ReadQuestFishItem(InformationWorldContext context)
        {
            if (ReadAnglerQuestFinished(context))
            {
                return -1;
            }

            int questIndex;
            InformationReflection.TryReadStaticInt(context == null ? null : context.MainType, "anglerQuest", out questIndex);
            var itemIds = context == null || context.MainType == null
                ? null
                : InformationReflection.GetStaticMember(context.MainType, "anglerQuestItemNetIDs");
            var itemId = InformationFishingCatchResolver.ToInt(InformationReflection.GetIndexedValue(itemIds, questIndex), -1);
            if (itemId <= 0)
            {
                return -1;
            }

            var hasItem = InformationFishingCatchResolver.InvokeInstance(context == null ? null : context.LocalPlayer, "HasItem", new object[] { itemId });
            bool alreadyHasItem;
            if (InformationFishingCatchResolver.TryConvertBool(hasItem, out alreadyHasItem) && alreadyHasItem)
            {
                return -1;
            }

            return itemId;
        }

        public static bool CanFishInLava(InformationWorldContext context, object fishingConditions)
        {
            if (context != null && InformationFishingCatchResolver.ReadBool(context.LocalPlayer, "accLavaFishing", false))
            {
                return true;
            }

            var poleItemType = InformationFishingCatchResolver.ReadInt(fishingConditions, "PoleItemType", 0);
            var baitItemType = InformationFishingCatchResolver.ReadInt(fishingConditions, "BaitItemType", 0);
            var itemSetsType = InformationReflection.FindType("Terraria.ID.ItemID+Sets");
            return InformationFishingCatchResolver.ReadBoolArrayValue(InformationReflection.GetStaticMember(itemSetsType, "CanFishInLava"), poleItemType) ||
                   InformationFishingCatchResolver.ReadBoolArrayValue(InformationReflection.GetStaticMember(itemSetsType, "IsLavaBait"), baitItemType);
        }

        private static object EnsureFishingConditionsForDisplay(InformationWorldContext context, object fishingConditions)
        {
            var conditions = fishingConditions ?? CreatePlayerFishingConditions();
            if (conditions == null)
            {
                return fishingConditions;
            }

            var polePower = InformationFishingCatchResolver.ReadInt(conditions, "PolePower", 0);
            var poleItemType = InformationFishingCatchResolver.ReadInt(conditions, "PoleItemType", 0);
            var baitPower = InformationFishingCatchResolver.ReadInt(conditions, "BaitPower", 0);
            var baitItemType = InformationFishingCatchResolver.ReadInt(conditions, "BaitItemType", 0);

            FillDisplayPoleAndBait(context, ref polePower, ref poleItemType, ref baitPower, ref baitItemType);

            var finalFishingLevel = InformationFishingCatchResolver.ReadInt(conditions, "FinalFishingLevel", 0);
            if (finalFishingLevel <= 0)
            {
                var baseFishingLevel = polePower + baitPower + InformationFishingCatchResolver.ReadInt(context == null ? null : context.LocalPlayer, "fishingSkill", 0);
                if (baseFishingLevel <= 0 && poleItemType > 0)
                {
                    baseFishingLevel = Math.Max(1, polePower);
                }

                if (baseFishingLevel > 0)
                {
                    var multiplier = InformationFishingCatchResolver.ReadFloat(conditions, "LevelMultipliers", 0f);
                    if (multiplier <= 0f)
                    {
                        multiplier = ReadFishingPowerMultiplier(context);
                    }

                    if (multiplier <= 0f)
                    {
                        multiplier = 1f;
                    }

                    finalFishingLevel = Math.Max(1, (int)(baseFishingLevel * multiplier));
                }
            }

            InformationFishingCatchResolver.SetMember(conditions, "PolePower", polePower);
            InformationFishingCatchResolver.SetMember(conditions, "PoleItemType", poleItemType);
            InformationFishingCatchResolver.SetMember(conditions, "BaitPower", baitPower);
            InformationFishingCatchResolver.SetMember(conditions, "BaitItemType", baitItemType);
            if (InformationFishingCatchResolver.ReadFloat(conditions, "LevelMultipliers", 0f) <= 0f)
            {
                InformationFishingCatchResolver.SetMember(conditions, "LevelMultipliers", Math.Max(1f, ReadFishingPowerMultiplier(context)));
            }

            InformationFishingCatchResolver.SetMember(conditions, "FinalFishingLevel", finalFishingLevel);
            return conditions;
        }

        private static object CreatePlayerFishingConditions()
        {
            var conditionsType = InformationReflection.FindType("Terraria.DataStructures.PlayerFishingConditions");
            if (conditionsType == null)
            {
                return null;
            }

            try
            {
                return Activator.CreateInstance(conditionsType);
            }
            catch
            {
                return null;
            }
        }

        private static void FillDisplayPoleAndBait(InformationWorldContext context, ref int polePower, ref int poleItemType, ref int baitPower, ref int baitItemType)
        {
            if (context == null || context.LocalPlayer == null)
            {
                return;
            }

            var inventory = InformationReflection.GetMember(context.LocalPlayer, "inventory");
            var selectedItemIndex = InformationFishingCatchResolver.ReadInt(context.LocalPlayer, "selectedItem", -1);
            var selectedItem = InformationReflection.GetIndexedValue(inventory, selectedItemIndex);
            ConsiderFishingPole(selectedItem, ref polePower, ref poleItemType);

            var mouseItem = context.MainType == null ? null : InformationReflection.GetStaticMember(context.MainType, "mouseItem");
            ConsiderFishingPole(mouseItem, ref polePower, ref poleItemType);
            ConsiderBait(mouseItem, ref baitPower, ref baitItemType);

            var count = InformationFishingCatchResolver.GetCollectionCount(inventory);
            for (var slot = 0; slot < count && slot < 58; slot++)
            {
                ConsiderFishingPole(InformationReflection.GetIndexedValue(inventory, slot), ref polePower, ref poleItemType);
            }

            if (baitPower <= 0 && baitItemType <= 0)
            {
                for (var slot = 54; slot < count && slot < 58; slot++)
                {
                    if (ConsiderBait(InformationReflection.GetIndexedValue(inventory, slot), ref baitPower, ref baitItemType))
                    {
                        return;
                    }
                }

                for (var slot = 0; slot < count && slot < 50; slot++)
                {
                    if (ConsiderBait(InformationReflection.GetIndexedValue(inventory, slot), ref baitPower, ref baitItemType))
                    {
                        return;
                    }
                }
            }
        }

        private static void ConsiderFishingPole(object item, ref int polePower, ref int poleItemType)
        {
            if (item == null)
            {
                return;
            }

            var power = InformationFishingCatchResolver.ReadInt(item, "fishingPole", 0);
            if (power <= polePower)
            {
                return;
            }

            polePower = power;
            poleItemType = InformationFishingCatchResolver.ReadInt(item, "type", 0);
        }

        private static bool ConsiderBait(object item, ref int baitPower, ref int baitItemType)
        {
            if (item == null || InformationFishingCatchResolver.ReadInt(item, "stack", 0) <= 0)
            {
                return false;
            }

            var power = InformationFishingCatchResolver.ReadInt(item, "bait", 0);
            if (power <= 0)
            {
                return false;
            }

            baitPower = power;
            baitItemType = InformationFishingCatchResolver.ReadInt(item, "type", 0);
            return true;
        }

        private static float ReadFishingPowerMultiplier(InformationWorldContext context)
        {
            var playerType = context == null || context.LocalPlayer == null ? null : context.LocalPlayer.GetType();
            object raw;
            if (InformationReflection.TryInvokeStatic(playerType, "Fishing_GetPowerMultiplier", null, out raw))
            {
                return InformationFishingCatchResolver.ToFloat(raw, 1f);
            }

            return 1f;
        }

        private static bool ReadAnglerQuestFinished(InformationWorldContext context)
        {
            var raw = context == null || context.MainType == null
                ? null
                : InformationReflection.GetStaticMember(context.MainType, "anglerQuestFinished");
            bool direct;
            if (InformationFishingCatchResolver.TryConvertBool(raw, out direct))
            {
                return direct;
            }

            int myPlayer;
            InformationReflection.TryReadStaticInt(context == null ? null : context.MainType, "myPlayer", out myPlayer);
            var indexed = InformationReflection.GetIndexedValue(raw, myPlayer);
            return InformationFishingCatchResolver.TryConvertBool(indexed, out direct) && direct;
        }
    }
}
