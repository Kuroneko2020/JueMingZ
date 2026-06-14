using System;
using System.Globalization;
using JueMingZ.Compat;

namespace JueMingZ.Records
{
    internal static class PlayerWorldPlaytimeClockReader
    {
        public static bool TryReadCurrent(long runtimeTick, out PlayerWorldClockSample sample, out string message)
        {
            sample = null;
            message = string.Empty;

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                message = "terrariaRuntimeTypesUnavailable:" + TerrariaRuntimeTypes.LastError;
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "mainTypeUnavailable";
                return false;
            }

            bool dayTime;
            if (!TryReadStaticBool(mainType, "dayTime", out dayTime))
            {
                message = "dayTimeUnavailable";
                return false;
            }

            double worldTime;
            if (!TryReadStaticDouble(mainType, "time", out worldTime) || double.IsNaN(worldTime) || double.IsInfinity(worldTime))
            {
                message = "worldTimeUnavailable";
                return false;
            }

            var maxTime = dayTime ? PlayerWorldPlaytimeConstants.DayLengthTicks : PlayerWorldPlaytimeConstants.NightLengthTicks;
            if (worldTime < 0d || worldTime > maxTime + 1d)
            {
                message = "worldTimeOutOfRange:" + worldTime.ToString("0.###", CultureInfo.InvariantCulture);
                return false;
            }

            double dayRate;
            if (!TryReadStaticDouble(mainType, "dayRate", out dayRate) || double.IsNaN(dayRate) || double.IsInfinity(dayRate))
            {
                dayRate = 1d;
            }

            sample = new PlayerWorldClockSample
            {
                DayTime = dayTime,
                WorldTime = worldTime,
                DayRate = Math.Max(0d, dayRate),
                RuntimeTick = runtimeTick,
                SampleUtc = DateTime.UtcNow
            };
            message = "ok";
            return true;
        }

        internal static PlayerWorldClockSample BuildSampleForTesting(bool dayTime, double worldTime, double dayRate, long runtimeTick, DateTime? utc = null)
        {
            return new PlayerWorldClockSample
            {
                DayTime = dayTime,
                WorldTime = worldTime,
                DayRate = dayRate,
                RuntimeTick = runtimeTick,
                SampleUtc = utc ?? DateTime.UtcNow
            };
        }

        private static bool TryReadStaticBool(Type type, string name, out bool value)
        {
            value = false;
            object raw;
            if (!TryReadStaticMember(type, name, out raw))
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

        private static bool TryReadStaticDouble(Type type, string name, out double value)
        {
            value = 0d;
            object raw;
            if (!TryReadStaticMember(type, name, out raw))
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadStaticMember(Type type, string name, out object value)
        {
            value = null;
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                System.Reflection.FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    value = field.GetValue(null);
                    return true;
                }

                System.Reflection.PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanRead)
                {
                    value = property.GetValue(null, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
