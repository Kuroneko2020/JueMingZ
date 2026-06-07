namespace JueMingZ.Automation.Information
{
    internal delegate bool FishingConditionRollCallback(FishingConditionRoll roll);

    internal static class InformationFishingConditionRolls
    {
        private static readonly bool[] FalseOnly = { false };
        private static readonly bool[] TrueOnly = { true };
        private static readonly bool[] FalseTrue = { false, true };
        private static readonly bool[] TrueFalse = { true, false };

        public static void ForEachRoll(
            InformationWorldContext context,
            int tileY,
            bool inHoney,
            bool jungle,
            bool snow,
            bool desert,
            bool infectedDesert,
            bool junkPossible,
            FishingConditionRollCallback callback)
        {
            if (callback == null)
            {
                return;
            }

            var heightLevels = new int[2];
            var heightLevelCount = BuildHeightLevels(context, tileY, heightLevels);
            var corruptionRolls = new CorruptionRoll[2];
            var honeyRolls = BuildHoneyRolls(context, inHoney);
            var snowRolls = BuildBooleanRolls(snow);
            var desertRolls = BuildBooleanRolls(desert);
            var infectedDesertRolls = BuildBooleanRolls(infectedDesert);
            var crateRolls = FalseTrue;
            var junkRolls = BuildBooleanRolls(junkPossible);

            for (var heightIndex = 0; heightIndex < heightLevelCount; heightIndex++)
            {
                var heightLevel = heightLevels[heightIndex];
                var corruptionRollCount = BuildCorruptionRolls(context, heightLevel, corruptionRolls);
                for (var corruptionIndex = 0; corruptionIndex < corruptionRollCount; corruptionIndex++)
                {
                    var corruption = corruptionRolls[corruptionIndex];
                    for (var honeyIndex = 0; honeyIndex < honeyRolls.Length; honeyIndex++)
                    {
                        for (var snowIndex = 0; snowIndex < snowRolls.Length; snowIndex++)
                        {
                            for (var desertIndex = 0; desertIndex < desertRolls.Length; desertIndex++)
                            {
                                for (var infectedDesertIndex = 0; infectedDesertIndex < infectedDesertRolls.Length; infectedDesertIndex++)
                                {
                                    for (var crateIndex = 0; crateIndex < crateRolls.Length; crateIndex++)
                                    {
                                        for (var junkIndex = 0; junkIndex < junkRolls.Length; junkIndex++)
                                        {
                                            var roll = new FishingConditionRoll
                                            {
                                                HeightLevel = heightLevel,
                                                Corrupt = corruption.Corrupt,
                                                Crimson = corruption.Crimson,
                                                Jungle = jungle,
                                                InHoney = honeyRolls[honeyIndex],
                                                Snow = snowRolls[snowIndex],
                                                Desert = desertRolls[desertIndex],
                                                InfectedDesert = infectedDesertRolls[infectedDesertIndex],
                                                RemixOcean = false,
                                                Crate = crateRolls[crateIndex],
                                                Junk = junkRolls[junkIndex]
                                            };

                                            if (!callback(roll))
                                            {
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        internal static int BuildHeightLevels(InformationWorldContext context, int tileY, int[] levels)
        {
            if (levels == null || levels.Length <= 0)
            {
                return 0;
            }

            var worldSurface = InformationFishingCatchResolver.ReadStaticDouble(context == null ? null : context.MainType, "worldSurface", 1200d);
            var rockLayer = InformationFishingCatchResolver.ReadStaticDouble(context == null ? null : context.MainType, "rockLayer", worldSurface + 200d);
            var maxTilesY = InformationFishingCatchResolver.ReadStaticInt(context == null ? null : context.MainType, "maxTilesY", 1800);
            var remix = InformationFishingCatchResolver.ReadStaticBool(context == null ? null : context.MainType, "remixWorld", false);
            int level;
            var count = 0;
            if (remix)
            {
                level = tileY < worldSurface * 0.5d
                    ? 0
                    : (tileY < worldSurface ? 1 : (tileY < rockLayer ? 3 : (tileY >= maxTilesY - 300 ? 4 : 2)));
                levels[count++] = level;
                if (level == 2 && count < levels.Length)
                {
                    levels[count++] = 1;
                }
            }
            else
            {
                level = tileY < worldSurface * 0.5d
                    ? 0
                    : (tileY < worldSurface ? 1 : (tileY < rockLayer ? 2 : (tileY >= maxTilesY - 300 ? 4 : 3)));
                levels[count++] = level;
            }

            return count;
        }

        internal static int BuildCorruptionRolls(InformationWorldContext context, int heightLevel, CorruptionRoll[] rolls)
        {
            if (rolls == null || rolls.Length <= 0)
            {
                return 0;
            }

            if (InformationFishingCatchResolver.ReadStaticBool(context == null ? null : context.MainType, "remixWorld", false) && heightLevel == 0)
            {
                rolls[0] = new CorruptionRoll(false, false);
                return 1;
            }

            var player = context == null ? null : context.LocalPlayer;
            var corrupt = InformationFishingCatchResolver.HasZone(player, "ZoneCorrupt");
            var crimson = InformationFishingCatchResolver.HasZone(player, "ZoneCrimson");
            if (corrupt && crimson)
            {
                rolls[0] = new CorruptionRoll(true, false);
                if (rolls.Length > 1)
                {
                    rolls[1] = new CorruptionRoll(false, true);
                    return 2;
                }

                return 1;
            }

            rolls[0] = new CorruptionRoll(corrupt, crimson);
            return 1;
        }

        internal static bool[] BuildHoneyRolls(InformationWorldContext context, bool inHoney)
        {
            if (inHoney && InformationFishingCatchResolver.ReadStaticBool(context == null ? null : context.MainType, "notTheBeesWorld", false))
            {
                return TrueFalse;
            }

            return inHoney ? TrueOnly : FalseOnly;
        }

        internal static bool[] BuildBooleanRolls(bool enabled)
        {
            return enabled ? FalseTrue : FalseOnly;
        }
    }
}
