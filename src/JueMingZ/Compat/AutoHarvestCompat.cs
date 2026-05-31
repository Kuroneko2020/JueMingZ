using System;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.WorldAutomation;

namespace JueMingZ.Compat
{
    internal static class AutoHarvestCompat
    {
        public const int StaffOfRegrowthItemType = 213;
        public const int AxeOfRegrowthItemType = 5295;
        public const int ClayPotTileType = 78;
        public const int ImmatureHerbsTileType = 82;
        public const int MatureHerbsTileType = 83;
        public const int BloomingHerbsTileType = 84;
        public const int PlanterBoxTileType = 380;

        public static bool IsRegrowthToolItemType(int itemType)
        {
            return itemType == StaffOfRegrowthItemType || itemType == AxeOfRegrowthItemType;
        }

        public static bool TryGetSeedItemTypeForHerbStyle(int herbStyle, out int seedItemType)
        {
            seedItemType = 0;
            switch (herbStyle)
            {
                case 0:
                    seedItemType = 307;
                    return true;
                case 1:
                    seedItemType = 308;
                    return true;
                case 2:
                    seedItemType = 309;
                    return true;
                case 3:
                    seedItemType = 310;
                    return true;
                case 4:
                    seedItemType = 311;
                    return true;
                case 5:
                    seedItemType = 312;
                    return true;
                case 6:
                    seedItemType = 2357;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryGetHerbStyleForSeedItemType(int seedItemType, out int herbStyle)
        {
            herbStyle = -1;
            switch (seedItemType)
            {
                case 307:
                    herbStyle = 0;
                    return true;
                case 308:
                    herbStyle = 1;
                    return true;
                case 309:
                    herbStyle = 2;
                    return true;
                case 310:
                    herbStyle = 3;
                    return true;
                case 311:
                    herbStyle = 4;
                    return true;
                case 312:
                    herbStyle = 5;
                    return true;
                case 2357:
                    herbStyle = 6;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryReadHarvestableHerb(object tiles, int tileX, int tileY, out AutoHarvestHerbTarget target)
        {
            target = null;
            AutoHarvestPlantSpot spot;
            if (!TryReadPlantSpot(tiles, tileX, tileY, out spot) ||
                spot == null ||
                !spot.Supported ||
                !spot.Active ||
                !IsSupportedHerbTileType(spot.TileType))
            {
                return false;
            }

            int seedItemType;
            if (!TryGetSeedItemTypeForHerbStyle(spot.HerbStyle, out seedItemType))
            {
                return false;
            }

            if (!IsHarvestableHerbWithSeed(spot.TileType, spot.HerbStyle, tileY))
            {
                return false;
            }

            target = new AutoHarvestHerbTarget
            {
                TileX = tileX,
                TileY = tileY,
                TileType = spot.TileType,
                HerbStyle = spot.HerbStyle,
                SeedItemType = seedItemType,
                SupportTileType = spot.SupportTileType
            };
            return true;
        }

        public static bool TryReadPlantSpot(object tiles, int tileX, int tileY, out AutoHarvestPlantSpot spot)
        {
            spot = new AutoHarvestPlantSpot();
            if (tiles == null || tileX < 0 || tileY < 0)
            {
                return false;
            }

            var tile = InformationTileAccess.GetTileAt(tiles, tileX, tileY);
            if (tile != null)
            {
                bool active;
                int type;
                int frameX;
                int frameY;
                if (InformationTileAccess.TryReadActiveTypeAndFrame(tile, out active, out type, out frameX, out frameY))
                {
                    spot.TileAvailable = true;
                    spot.Active = active;
                    spot.TileType = type;
                    spot.HerbStyle = Math.Max(0, frameX / 18);
                }
            }

            var support = InformationTileAccess.GetTileAt(tiles, tileX, tileY + 1);
            if (support != null)
            {
                bool supportActive;
                int supportType;
                int supportFrameX;
                int supportFrameY;
                if (InformationTileAccess.TryReadActiveTypeAndFrame(support, out supportActive, out supportType, out supportFrameX, out supportFrameY))
                {
                    spot.SupportTileType = supportType;
                    spot.Supported = supportActive && IsSupportedPlanterSupport(supportType);
                }
            }

            return spot.TileAvailable || spot.Supported;
        }

        public static bool IsSupportedPlanterSupport(int tileType)
        {
            return tileType == ClayPotTileType || tileType == PlanterBoxTileType;
        }

        public static bool IsSupportedHerbTileType(int tileType)
        {
            return tileType == BloomingHerbsTileType || tileType == MatureHerbsTileType;
        }

        public static bool IsPlantingSpotEmpty(AutoHarvestPlantSpot spot)
        {
            return spot != null && spot.Supported && !spot.Active;
        }

        public static float TileCenterWorldX(int tileX)
        {
            return tileX * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
        }

        public static float TileCenterWorldY(int tileY)
        {
            return tileY * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
        }

        private static bool IsHarvestableHerbWithSeed(int tileType, int herbStyle, int tileY)
        {
            var worldGenType = InformationReflection.FindType("Terraria.WorldGen");
            object raw;
            if (InformationReflection.TryInvokeStatic(worldGenType, "IsHarvestableHerbWithSeed", new object[] { tileType, herbStyle, tileY }, out raw) && raw != null)
            {
                try
                {
                    return Convert.ToBoolean(raw);
                }
                catch
                {
                }
            }

            return tileType == BloomingHerbsTileType;
        }
    }
}
