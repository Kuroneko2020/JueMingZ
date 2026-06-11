using System;
using System.Globalization;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementNameResolver
    {
        public static string ResolveItemName(int itemType, string fallback)
        {
            var value = ResolveLangName("GetItemNameValue", itemType);
            return FirstNonEmpty(value, fallback, itemType > 0 ? itemType.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        public static string ResolveNpcTypeName(int npcType, string fallback)
        {
            return FirstNonEmpty(
                InformationNpcNameCompat.ResolveTypeName(npcType),
                fallback,
                npcType > 0 ? npcType.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        public static string ResolveTileName(int tileType, int option, string fallback)
        {
            string source;
            return ResolveTileName(tileType, option, fallback, out source);
        }

        public static string ResolveTileName(int tileType, int option, string fallback, out string source)
        {
            source = string.Empty;
            int itemId;
            if (MapQuickAnnouncementPlacementNameCache.TryResolveTileItem(tileType, option, out itemId))
            {
                var itemName = ResolvePlacementItemName(itemId);
                if (!string.IsNullOrWhiteSpace(itemName))
                {
                    source = "placementItem";
                    return itemName;
                }
            }

            var value = ResolveMapObjectName(ResolveTileLookup(tileType, option));
            if (!string.IsNullOrWhiteSpace(value))
            {
                source = "mapObject";
                return value.Trim();
            }

            value = ResolveIdSearchName("Terraria.ID.TileID", tileType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                source = "idSearch";
                return value.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                source = "fallback";
                return fallback.Trim();
            }

            source = "tileId";
            return FirstNonEmpty(
                tileType >= 0 ? "Tile#" + tileType.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        public static string ResolveWallName(int wallType, string fallback)
        {
            string source;
            return ResolveWallName(wallType, fallback, out source);
        }

        public static string ResolveWallName(int wallType, string fallback, out string source)
        {
            source = string.Empty;
            int itemId;
            if (MapQuickAnnouncementPlacementNameCache.TryResolveWallItem(wallType, out itemId))
            {
                var itemName = ResolvePlacementItemName(itemId);
                if (!string.IsNullOrWhiteSpace(itemName))
                {
                    source = "placementItem";
                    return itemName;
                }
            }

            var value = ResolveMapObjectName(ResolveWallLookup(wallType));
            if (!string.IsNullOrWhiteSpace(value))
            {
                source = "mapObject";
                return value.Trim();
            }

            value = ResolveIdSearchName("Terraria.ID.WallID", wallType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                source = "idSearch";
                return value.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                source = "fallback";
                return fallback.Trim();
            }

            source = "wallId";
            return FirstNonEmpty(
                wallType > 0 ? "Wall#" + wallType.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        private static int ResolveTileLookup(int tileType, int option)
        {
            if (tileType < 0)
            {
                return -1;
            }

            object raw;
            var mapHelperType = InformationReflection.FindType("Terraria.Map.MapHelper");
            if (InformationReflection.TryInvokeStatic(mapHelperType, "TileToLookup", new object[] { tileType, Math.Max(0, option) }, out raw))
            {
                try
                {
                    return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return -1;
        }

        private static int ResolveWallLookup(int wallType)
        {
            if (wallType <= 0)
            {
                return -1;
            }

            var mapHelperType = InformationReflection.FindType("Terraria.Map.MapHelper");
            var wallLookup = InformationReflection.GetStaticMember(mapHelperType, "wallLookup");
            var raw = InformationReflection.GetIndexedValue(wallLookup, wallType);
            if (raw == null)
            {
                return -1;
            }

            try
            {
                return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return -1;
            }
        }

        private static string ResolveMapObjectName(int lookup)
        {
            if (lookup < 0)
            {
                return string.Empty;
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetMapObjectName", new object[] { lookup }, out raw))
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return string.Empty;
        }

        private static string ResolveLangName(string methodName, int id)
        {
            if (id <= 0)
            {
                return string.Empty;
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, methodName, new object[] { id }, out raw))
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return string.Empty;
        }

        private static string ResolvePlacementItemName(int itemId)
        {
            var value = ResolveLangName("GetItemNameValue", itemId);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            return string.Equals(value, itemId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                ? string.Empty
                : value;
        }

        private static string ResolveIdSearchName(string idTypeName, int id)
        {
            if (id < 0)
            {
                return string.Empty;
            }

            var idType = InformationReflection.FindType(idTypeName);
            var search = InformationReflection.GetStaticMember(idType, "Search");
            if (search == null)
            {
                return string.Empty;
            }

            try
            {
                var method = search.GetType().GetMethod("GetName", new[] { typeof(int) });
                if (method == null)
                {
                    return string.Empty;
                }

                return Convert.ToString(method.Invoke(search, new object[] { id }), CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index].Trim();
                }
            }

            return string.Empty;
        }
    }
}
