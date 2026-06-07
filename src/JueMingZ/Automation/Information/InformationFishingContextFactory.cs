using System;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Automation.Information
{
    internal static class InformationFishingContextFactory
    {
        private static Type _attemptType;
        private static Type _contextType;
        private static Assembly _typeAssembly;

        public static object CreateContext(FishingAttemptSpec spec)
        {
            Type attemptType;
            Type contextType;
            if (!TryResolveTypes(spec.Context, out attemptType, out contextType))
            {
                return null;
            }

            try
            {
                var attempt = Activator.CreateInstance(attemptType);
                SetMember(attempt, "playerFishingConditions", spec.FishingConditions);
                SetMember(attempt, "X", spec.TileX);
                SetMember(attempt, "Y", spec.TileY);
                SetMember(attempt, "bobberType", 0);
                SetMember(attempt, "common", false);
                SetMember(attempt, "uncommon", false);
                SetMember(attempt, "rare", false);
                SetMember(attempt, "veryrare", false);
                SetMember(attempt, "legendary", false);
                SetMember(attempt, "crate", spec.Roll.Crate);
                SetMember(attempt, "junk", spec.Roll.Junk);
                SetMember(attempt, "inLava", spec.InLava);
                SetMember(attempt, "inHoney", spec.InHoney);
                SetMember(attempt, "waterTilesCount", spec.WaterTilesCount);
                SetMember(attempt, "waterNeededToFish", spec.WaterNeeded);
                SetMember(attempt, "waterQuality", 0f);
                SetMember(attempt, "chumsInWater", spec.Chums);
                SetMember(attempt, "fishingLevel", spec.FishingLevel);
                SetMember(attempt, "CanFishInLava", spec.CanFishInLava);
                SetMember(attempt, "atmo", 0f);
                SetMember(attempt, "questFish", spec.QuestFish);
                SetMember(attempt, "heightLevel", spec.Roll.HeightLevel);
                SetMember(attempt, "rolledItemDrop", 0);
                SetMember(attempt, "rolledEnemySpawn", 0);

                var fishContext = Activator.CreateInstance(contextType);
                SetMember(fishContext, "Player", spec.Context == null ? null : spec.Context.LocalPlayer);
                SetMember(fishContext, "Fisher", attempt);
                SetMember(fishContext, "RolledCorruption", spec.Roll.Corrupt);
                SetMember(fishContext, "RolledCrimson", spec.Roll.Crimson);
                SetMember(fishContext, "RolledJungle", spec.Roll.Jungle);
                SetMember(fishContext, "RolledSnow", spec.Roll.Snow);
                SetMember(fishContext, "RolledDesert", spec.Roll.Desert);
                SetMember(fishContext, "RolledInfectedDesert", spec.Roll.InfectedDesert);
                SetMember(fishContext, "RolledRemixOcean", spec.Roll.RemixOcean);
                return fishContext;
            }
            catch
            {
                return null;
            }
        }

        internal static void ResetTypeCacheForTesting()
        {
            _attemptType = null;
            _contextType = null;
            _typeAssembly = null;
        }

        private static bool TryResolveTypes(InformationWorldContext context, out Type attemptType, out Type contextType)
        {
            var contextAssembly = context == null || context.MainType == null ? null : context.MainType.Assembly;
            if (contextAssembly != null && !object.ReferenceEquals(contextAssembly, _typeAssembly))
            {
                _attemptType = null;
                _contextType = null;
                _typeAssembly = contextAssembly;
            }

            if (_attemptType == null)
            {
                _attemptType = FindTypeFromContextAssembly(context, "Terraria.DataStructures.FishingAttempt") ??
                               InformationReflection.FindType("Terraria.DataStructures.FishingAttempt");
            }

            if (_contextType == null)
            {
                _contextType = FindTypeFromContextAssembly(context, "Terraria.GameContent.FishDropRules.FishingContext") ??
                               InformationReflection.FindType("Terraria.GameContent.FishDropRules.FishingContext");
            }

            attemptType = _attemptType;
            contextType = _contextType;
            if (_typeAssembly == null && attemptType != null)
            {
                _typeAssembly = attemptType.Assembly;
            }

            return attemptType != null && contextType != null;
        }

        private static Type FindTypeFromContextAssembly(InformationWorldContext context, string fullName)
        {
            if (context == null || context.MainType == null || string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            try
            {
                return context.MainType.Assembly.GetType(fullName, false);
            }
            catch
            {
                return null;
            }
        }

        private static void SetMember(object instance, string name, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            try
            {
                var type = instance.GetType();
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(instance, ConvertForMember(field.FieldType, value));
                    return;
                }

                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, ConvertForMember(property.PropertyType, value), null);
                }
            }
            catch
            {
            }
        }

        private static object ConvertForMember(Type targetType, object value)
        {
            if (targetType == null || value == null)
            {
                return value;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (targetType.IsEnum)
            {
                try
                {
                    return Enum.ToObject(targetType, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                }
                catch
                {
                    return value;
                }
            }

            try
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }
    }
}
