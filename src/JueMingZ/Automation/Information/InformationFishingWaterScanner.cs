using System;

namespace JueMingZ.Automation.Information
{
    internal static class InformationFishingWaterScanner
    {
        private const int TileSize = 16;

        public static FishingWaterScan ScanFishingWater(InformationWorldContext context, int tileX, int tileY)
        {
            // Scan only the current bobber water pocket. Do not widen this into a
            // per-frame world scan during resolver or overlay refactors.
            InformationFishingCatchDiagnostics.IncrementWaterScan();
            var result = new FishingWaterScan();
            result.Chums = 0;
            var tiles = context == null || context.MainType == null
                ? null
                : InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                return result;
            }

            var minX = tileX;
            var maxX = tileX;
            while (minX > 10 && IsLiquidOpenTile(context, tiles, minX, tileY))
            {
                minX--;
            }

            while (maxX < InformationFishingCatchResolver.ReadStaticInt(context.MainType, "maxTilesX", tileX + 10) - 10 &&
                   IsLiquidOpenTile(context, tiles, maxX, tileY))
            {
                maxX++;
            }

            var maxY = InformationFishingCatchResolver.ReadStaticInt(context.MainType, "maxTilesY", tileY + 10);
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = tileY; y < maxY - 10 && IsLiquidOpenTile(context, tiles, x, y); y++)
                {
                    var tile = InformationTileAccess.GetTileAt(tiles, x, y);
                    var liquidType = ReadLiquidType(tile);
                    result.TotalTiles++;
                    if (liquidType == 1)
                    {
                        result.InLava = true;
                    }
                    else if (liquidType == 2)
                    {
                        result.InHoney = true;
                    }
                }
            }

            if (result.InHoney)
            {
                result.TotalTiles = (int)(result.TotalTiles * 1.5d);
            }

            return result;
        }

        public static string ResolveLiquidKind(FishingWaterScan water)
        {
            if (water != null && water.InLava)
            {
                return "lava";
            }

            if (water != null && water.InHoney)
            {
                return "honey";
            }

            return "water";
        }

        public static FishingWaterPenaltyResult ApplyFishingWaterPenalty(
            InformationWorldContext context,
            float bobberWorldY,
            FishingWaterScan water,
            int finalFishingLevel)
        {
            var waterNeeded = 300;
            var maxTilesX = InformationFishingCatchResolver.ReadStaticInt(context == null ? null : context.MainType, "maxTilesX", 4200);
            var worldSurface = InformationFishingCatchResolver.ReadStaticDouble(context == null ? null : context.MainType, "worldSurface", 1200d);
            var worldScale = maxTilesX / 4200f;
            worldScale *= worldScale;
            var multiplier = ((bobberWorldY / TileSize) - (60f + 10f * worldScale)) / Math.Max(1f, (float)(worldSurface / 6d));
            if (multiplier < 0.25f)
            {
                multiplier = 0.25f;
            }

            if (multiplier > 1f)
            {
                multiplier = 1f;
            }

            waterNeeded = (int)(waterNeeded * multiplier);
            var fishingLevel = finalFishingLevel;
            var safeWater = water ?? new FishingWaterScan();
            if (safeWater.Chums > 0)
            {
                fishingLevel += 11;
            }

            if (safeWater.Chums > 1)
            {
                fishingLevel += 6;
            }

            if (safeWater.Chums > 2)
            {
                fishingLevel += 3;
            }

            if (safeWater.TotalTiles < waterNeeded)
            {
                var ratio = safeWater.TotalTiles / Math.Max(1f, waterNeeded);
                if (ratio < 1f)
                {
                    fishingLevel = (int)(fishingLevel * ratio);
                }
            }

            var luckLevel = fishingLevel;
            double luck;
            if (context != null &&
                InformationFishingCatchResolver.TryReadNumber(context.LocalPlayer, "luck", out luck) &&
                luck < 0d)
            {
                luckLevel = Math.Min(luckLevel, (int)(fishingLevel * 0.6f));
            }

            return new FishingWaterPenaltyResult
            {
                FishingLevel = fishingLevel,
                WaterNeeded = waterNeeded,
                JunkPossible = safeWater.TotalTiles < waterNeeded && luckLevel < 49
            };
        }

        public static bool IsLiquidOpenTile(InformationWorldContext context, object tiles, int x, int y)
        {
            var tile = InformationTileAccess.GetTileAt(tiles, x, y);
            return tile != null && ReadLiquidAmount(tile) > 0 && !IsSolidTile(context, x, y);
        }

        public static bool IsSolidTile(InformationWorldContext context, int x, int y)
        {
            object raw;
            var worldGenType = InformationReflection.FindType("Terraria.WorldGen");
            if (InformationReflection.TryInvokeStatic(worldGenType, "SolidTile", new object[] { x, y, false }, out raw))
            {
                bool value;
                if (InformationFishingCatchResolver.TryConvertBool(raw, out value))
                {
                    return value;
                }
            }

            return false;
        }

        public static int ReadLiquidAmount(object tile)
        {
            return InformationTileAccess.ReadLiquidAmount(tile);
        }

        public static int ReadLiquidType(object tile)
        {
            var type = InformationTileAccess.ReadLiquidType(tile);
            if (type != 0)
            {
                return type;
            }

            if (InformationFishingCatchResolver.ReadBool(tile, "lava", false))
            {
                return 1;
            }

            return InformationFishingCatchResolver.ReadBool(tile, "honey", false) ? 2 : 0;
        }
    }
}
