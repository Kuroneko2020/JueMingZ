using System;
using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace JueMingZ.Automation.Information
{
    internal static class InformationFishingCatchSignatureBuilder
    {
        private const int TileSize = 16;
        private static readonly string[] QueryPlayerZoneFields =
        {
            "ZoneCorrupt",
            "ZoneCrimson",
            "ZoneJungle",
            "ZoneSnow",
            "ZoneDesert",
            "ZoneUndergroundDesert",
            "ZoneBeach",
            "ZoneDungeon",
            "ZoneHallow",
            "ZoneMeteor",
            "ZoneGlowshroom",
            "ZoneUnderworldHeight",
            "ZoneOverworldHeight",
            "ZoneSkyHeight",
            "ZoneDirtLayerHeight",
            "ZoneRockLayerHeight",
            "ZoneRain"
        };
        private static readonly string[] QueryWorldBoolFields =
        {
            "hardMode",
            "expertMode",
            "masterMode",
            "dayTime",
            "bloodMoon",
            "eclipse",
            "pumpkinMoon",
            "snowMoon",
            "raining",
            "remixWorld",
            "notTheBeesWorld",
            "drunkWorld",
            "getGoodWorld",
            "tenthAnniversaryWorld",
            "dontStarveWorld",
            "zenithWorld",
            "xMas",
            "halloween",
            "slimeRain"
        };

        public static FishingCatchEarlyCacheKey BuildEarlyCatchCacheKey(FishingCatchEarlyQuerySpec spec)
        {
            var builder = new StringBuilder(512);
            AppendKeyPart(builder, "world", spec.Context == null ? string.Empty : spec.Context.WorldKey);
            AppendKeyPart(builder, "bobber", spec.BobberIdentity.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "tile", spec.TileX.ToString(CultureInfo.InvariantCulture) + "," + spec.TileY.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "pole", spec.PolePower.ToString(CultureInfo.InvariantCulture) + ":" + spec.PoleItemType.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "bait", spec.BaitPower.ToString(CultureInfo.InvariantCulture) + ":" + spec.BaitItemType.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "quest", spec.QuestFish.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "playerIdentity", BuildPlayerIdentitySignature(spec.Context));
            AppendKeyPart(builder, "player", BuildPlayerEnvironmentSignature(spec.Context));
            AppendKeyPart(builder, "buffs", BuildPlayerActiveBuffSignature(spec.Context));
            AppendKeyPart(builder, "worldState", BuildWorldStateSignature(spec.Context));
            AppendKeyPart(builder, "language", BuildLanguageSignature());
            return new FishingCatchEarlyCacheKey(builder.ToString());
        }

        public static FishingCatchQueryKey BuildCatchQueryKey(FishingCatchQuerySpec spec)
        {
            var builder = new StringBuilder(512);
            AppendKeyPart(builder, "world", spec.Context == null ? string.Empty : spec.Context.WorldKey);
            AppendKeyPart(builder, "tile", spec.TileX.ToString(CultureInfo.InvariantCulture) + "," + spec.TileY.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "liquid", spec.LiquidKind);
            AppendKeyPart(builder, "water", spec.WaterTiles.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "chums", spec.Chums.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "waterNeeded", spec.WaterNeeded.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "junk", spec.JunkPossible ? "1" : "0");
            AppendKeyPart(builder, "final", spec.FinalFishingLevel.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "level", spec.FishingLevel.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "pole", spec.PolePower.ToString(CultureInfo.InvariantCulture) + ":" + spec.PoleItemType.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "bait", spec.BaitPower.ToString(CultureInfo.InvariantCulture) + ":" + spec.BaitItemType.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "lava", spec.CanFishInLava ? "1" : "0");
            AppendKeyPart(builder, "quest", spec.QuestFish.ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "filter", spec.FilterSignature);
            AppendKeyPart(builder, "playerIdentity", BuildPlayerIdentitySignature(spec.Context));
            AppendKeyPart(builder, "player", BuildPlayerEnvironmentSignature(spec.Context));
            AppendKeyPart(builder, "worldState", BuildWorldStateSignature(spec.Context));
            AppendKeyPart(builder, "language", BuildLanguageSignature());
            return new FishingCatchQueryKey(builder.ToString());
        }

        private static string BuildPlayerEnvironmentSignature(InformationWorldContext context)
        {
            var player = context == null ? null : context.LocalPlayer;
            var builder = new StringBuilder(192);
            double luck;
            AppendKeyPart(builder, "luck", TryReadNumber(player, "luck", out luck) ? luck.ToString("0.###", CultureInfo.InvariantCulture) : "unknown");
            AppendKeyPart(builder, "fishSkill", ReadInt(player, "fishingSkill", 0).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "accLavaFishing", ReadBool(player, "accLavaFishing", false) ? "1" : "0");
            AppendKeyPart(builder, "heightTile", context == null ? "0" : ((int)Math.Floor(context.PlayerCenterY / TileSize)).ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < QueryPlayerZoneFields.Length; index++)
            {
                AppendKeyPart(builder, QueryPlayerZoneFields[index], HasZone(player, QueryPlayerZoneFields[index]) ? "1" : "0");
            }

            return builder.ToString();
        }

        private static string BuildPlayerIdentitySignature(InformationWorldContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(context.PlayerRecordKey))
            {
                return "record:" + context.PlayerRecordKey.Trim();
            }

            var player = context.LocalPlayer;
            if (player == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(96);
            AppendKeyPart(builder, "object", RuntimeHelpers.GetHashCode(player).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "whoAmI", ReadInt(player, "whoAmI", -1).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(
                builder,
                "name",
                FirstNonEmpty(
                    InformationReflection.TryReadString(player, "name"),
                    InformationReflection.TryReadString(player, "Name")));
            return builder.ToString();
        }

        private static string BuildPlayerActiveBuffSignature(InformationWorldContext context)
        {
            var player = context == null ? null : context.LocalPlayer;
            var buffTypes = InformationReflection.GetMember(player, "buffType");
            var buffTimes = InformationReflection.GetMember(player, "buffTime");
            var count = Math.Min(GetCollectionCount(buffTypes), GetCollectionCount(buffTimes));
            if (count <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(128);
            for (var index = 0; index < count && index < 64; index++)
            {
                var type = ToInt(InformationReflection.GetIndexedValue(buffTypes, index), 0);
                var time = ToInt(InformationReflection.GetIndexedValue(buffTimes, index), 0);
                if (type <= 0 || time <= 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(',');
                }

                builder.Append(type.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string BuildWorldStateSignature(InformationWorldContext context)
        {
            var mainType = context == null ? null : context.MainType;
            var builder = new StringBuilder(256);
            for (var index = 0; index < QueryWorldBoolFields.Length; index++)
            {
                AppendKeyPart(builder, QueryWorldBoolFields[index], ReadStaticBool(mainType, QueryWorldBoolFields[index], false) ? "1" : "0");
            }

            AppendKeyPart(builder, "moonPhase", ReadStaticInt(mainType, "moonPhase", -1).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "maxTilesX", ReadStaticInt(mainType, "maxTilesX", 0).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "maxTilesY", ReadStaticInt(mainType, "maxTilesY", 0).ToString(CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "worldSurface", ReadStaticDouble(mainType, "worldSurface", 0d).ToString("0.###", CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "rockLayer", ReadStaticDouble(mainType, "rockLayer", 0d).ToString("0.###", CultureInfo.InvariantCulture));
            AppendKeyPart(builder, "timeBucket", ((int)(ReadStaticDouble(mainType, "time", 0d) / 3600d)).ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string BuildLanguageSignature()
        {
            return CultureInfo.CurrentCulture.Name + "/" +
                   CultureInfo.CurrentUICulture.Name + "/" +
                   ReadTerrariaLanguageSignature();
        }

        private static string ReadTerrariaLanguageSignature()
        {
            try
            {
                var managerType = InformationReflection.FindType("Terraria.Localization.LanguageManager");
                var manager = InformationReflection.GetStaticMember(managerType, "Instance");
                var activeCulture = InformationReflection.GetMember(manager, "ActiveCulture");
                var cultureName = FirstNonEmpty(
                    InformationReflection.TryReadString(activeCulture, "Name"),
                    InformationReflection.TryReadString(activeCulture, "CultureInfoName"),
                    InformationReflection.TryReadString(activeCulture, "LegacyId"));
                if (!string.IsNullOrWhiteSpace(cultureName))
                {
                    return cultureName.Trim();
                }

                return activeCulture == null ? string.Empty : Convert.ToString(activeCulture, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AppendKeyPart(StringBuilder builder, string name, string value)
        {
            if (builder == null)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            builder.Append(name ?? string.Empty);
            builder.Append('=');
            builder.Append(value ?? string.Empty);
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

        private static int GetCollectionCount(object source)
        {
            if (source == null)
            {
                return 0;
            }

            var list = source as IList;
            if (list != null)
            {
                return list.Count;
            }

            var array = source as Array;
            return array == null || array.Rank != 1 ? 0 : array.GetLength(0);
        }

        private static bool HasZone(object player, string member)
        {
            bool value;
            return InformationReflection.TryReadBool(player, member, out value) && value;
        }

        private static int ReadStaticInt(Type type, string name, int fallback)
        {
            return ToInt(InformationReflection.GetStaticMember(type, name), fallback);
        }

        private static double ReadStaticDouble(Type type, string name, double fallback)
        {
            return ToDouble(InformationReflection.GetStaticMember(type, name), fallback);
        }

        private static bool ReadStaticBool(Type type, string name, bool fallback)
        {
            bool value;
            return TryConvertBool(InformationReflection.GetStaticMember(type, name), out value) ? value : fallback;
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            return ToInt(InformationReflection.GetMember(instance, name), fallback);
        }

        private static bool ReadBool(object instance, string name, bool fallback)
        {
            bool value;
            return TryConvertBool(InformationReflection.GetMember(instance, name), out value) ? value : fallback;
        }

        private static bool TryReadNumber(object instance, string name, out double value)
        {
            value = ToDouble(InformationReflection.GetMember(instance, name), double.NaN);
            return !double.IsNaN(value);
        }

        private static int ToInt(object raw, int fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static double ToDouble(object raw, double fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
