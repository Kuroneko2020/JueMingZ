using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly Dictionary<string, int> ItemIdCache = new Dictionary<string, int>(StringComparer.Ordinal);

        private static bool CandidateMatchesCategory(object item, int itemType, string category)
        {
            if (item == null || itemType <= 0 || string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            if (string.Equals(category, "double_jump", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, DoubleJumpNames, DoubleJumpFallbackIds);
            }

            if (string.Equals(category, "rocket_boots", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, RocketBootNames, RocketBootFallbackIds);
            }

            if (string.Equals(category, "flying_carpet", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, FlyingCarpetNames, FlyingCarpetFallbackIds);
            }

            if (string.Equals(category, "fairy_boots", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, FairyLegNames, FairyLegFallbackIds) || ReadItemName(item).IndexOf("Djinn", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(category, "wings", StringComparison.Ordinal))
            {
                sbyte wingSlot;
                return TryReadItemSByte(item, "wingSlot", out wingSlot) && wingSlot > -1;
            }

            if (string.Equals(category, "horseshoe", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, HorseshoeNames, HorseshoeFallbackIds);
            }

            if (string.Equals(category, "flying_mount", StringComparison.Ordinal))
            {
                int mountType;
                bool canFly;
                return TryReadItemMountType(item, out mountType) &&
                       mountType >= 0 &&
                       TryResolveMountCanFly(mountType, out canFly) &&
                       canFly;
            }

            if (string.Equals(category, "safe_mount", StringComparison.Ordinal))
            {
                int mountType;
                bool canFly;
                bool noFallDamage;
                return TryReadItemMountType(item, out mountType) &&
                       mountType >= 0 &&
                       TryResolveMountCanFly(mountType, out canFly) &&
                       !canFly &&
                       TryResolveMountNoFallDamage(mountType, out noFallDamage) &&
                       noFallDamage;
            }

            if (string.Equals(category, "gravity_globe", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, GravityGlobeNames, GravityGlobeFallbackIds);
            }

            if (string.Equals(category, "umbrella", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, UmbrellaNames, UmbrellaFallbackIds);
            }

            return false;
        }

        internal static bool IsKnownItemTypeForDiagnostics(string category, int itemType)
        {
            if (itemType <= 0 || string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            if (string.Equals(category, "double_jump", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, DoubleJumpNames, DoubleJumpFallbackIds);
            }

            if (string.Equals(category, "rocket_boots", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, RocketBootNames, RocketBootFallbackIds);
            }

            if (string.Equals(category, "flying_carpet", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, FlyingCarpetNames, FlyingCarpetFallbackIds);
            }

            if (string.Equals(category, "fairy_boots", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, FairyLegNames, FairyLegFallbackIds);
            }

            if (string.Equals(category, "horseshoe", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, HorseshoeNames, HorseshoeFallbackIds);
            }

            if (string.Equals(category, "gravity_globe", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, GravityGlobeNames, GravityGlobeFallbackIds);
            }

            if (string.Equals(category, "umbrella", StringComparison.Ordinal))
            {
                return IsKnownItem(itemType, UmbrellaNames, UmbrellaFallbackIds);
            }

            return false;
        }

        private static bool IsKnownItem(int itemType, string[] names, int[] fallbackIds)
        {
            if (itemType <= 0)
            {
                return false;
            }

            if (fallbackIds != null)
            {
                for (var index = 0; index < fallbackIds.Length; index++)
                {
                    if (itemType == fallbackIds[index])
                    {
                        return true;
                    }
                }
            }

            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                var resolved = ResolveItemId(names[index], -1);
                if (resolved > 0 && itemType == resolved)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveItemId(string name, int fallback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            lock (ItemIdCache)
            {
                int cached;
                if (ItemIdCache.TryGetValue(name, out cached))
                {
                    return cached <= 0 ? fallback : cached;
                }

                var resolved = fallback;
                try
                {
                    var itemIdType = FindType("Terraria.ID.ItemID");
                    var field = itemIdType == null ? null : itemIdType.GetField(name, StaticFlags);
                    if (field != null)
                    {
                        resolved = Convert.ToInt32(field.GetValue(null), CultureInfo.InvariantCulture);
                    }
                }
                catch
                {
                    resolved = fallback;
                }

                ItemIdCache[name] = resolved;
                return resolved;
            }
        }

        private static string ReadItemName(object item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var value = GetMember(item, "Name") ?? GetMember(item, "HoverName") ?? GetMember(item, "name");
            return value == null ? string.Empty : value.ToString();
        }

        private static readonly string[] DoubleJumpNames =
        {
            "CloudinaBottle",
            "CloudinaBalloon",
            "SandstorminaBottle",
            "SandstorminaBalloon",
            "BlizzardinaBottle",
            "BlizzardinaBalloon",
            "BundleofBalloons",
            "FartinaJar",
            "FartInABalloon",
            "TsunamiInABottle",
            "SharkronBalloon",
            "BalloonHorseshoeFart",
            "BalloonHorseshoeHoney",
            "BalloonHorseshoeSharkron",
            "PartyBundleOfBalloonsAccessory",
            "HorseshoeBundle"
        };

        private static readonly int[] DoubleJumpFallbackIds = { 53, 399, 857, 983, 987, 1163, 1164, 1724, 1863, 3201, 3241, 3250, 3251, 3252, 3730, 5331 };

        private static readonly string[] RocketBootNames =
        {
            "RocketBoots",
            "SpectreBoots",
            "LightningBoots",
            "FrostsparkBoots",
            "HellfireTreads",
            "TerrasparkBoots",
            "FairyBoots"
        };

        private static readonly int[] RocketBootFallbackIds = { 128, 405, 898, 1862, 5000, 3993 };

        private static readonly string[] FlyingCarpetNames = { "FlyingCarpet" };

        private static readonly int[] FlyingCarpetFallbackIds = { 934 };

        private static readonly string[] FairyLegNames = { "DjinnsCurse" };

        private static readonly int[] FairyLegFallbackIds = { 3770 };

        private static readonly string[] GravityGlobeNames = { "GravityGlobe" };

        private static readonly int[] GravityGlobeFallbackIds = { 1131 };

        private static readonly string[] UmbrellaNames = { "Umbrella", "TragicUmbrella" };

        private static readonly int[] UmbrellaFallbackIds = { 946, 4707 };

        private static readonly string[] HorseshoeNames =
        {
            "LuckyHorseshoe",
            "ObsidianHorseshoe",
            "BlueHorseshoeBalloon",
            "WhiteHorseshoeBalloon",
            "YellowHorseshoeBalloon",
            "BalloonHorseshoeFart",
            "BalloonHorseshoeHoney",
            "BalloonHorseshoeSharkron",
            "HorseshoeBundle"
        };

        private static readonly int[] HorseshoeFallbackIds = { 158, 396, 1250, 1251, 1252, 3250, 3251, 3252, 5331 };
    }
}
