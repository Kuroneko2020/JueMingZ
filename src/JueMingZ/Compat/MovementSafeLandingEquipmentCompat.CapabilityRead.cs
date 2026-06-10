using System;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
        private static bool TryReadItemType(object item, out int itemType)
        {
            itemType = 0;
            if (item == null)
            {
                return false;
            }

            if (!TryReadItemInt(item, "type", out itemType))
            {
                return false;
            }

            int stack;
            if (TryReadItemInt(item, "stack", out stack) && stack <= 0)
            {
                return false;
            }

            bool isAir;
            if (TryReadItemBool(item, "IsAir", out isAir) && isAir)
            {
                return false;
            }

            return itemType > 0;
        }

        private static bool TryReadItemMountType(object item, out int mountType)
        {
            mountType = -1;
            return item != null && TryReadItemIntByNames(item, out mountType, "mountType", "MountType", "mountId", "MountId") && mountType >= 0;
        }

        private static bool TryResolveMountCanFly(int mountType, out bool canFly)
        {
            canFly = false;
            if (mountType < 0)
            {
                return false;
            }

            try
            {
                var mountTypeType = FindType("Terraria.Mount");
                var mounts = mountTypeType == null ? null : GetStatic(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                if (data == null)
                {
                    return false;
                }

                bool boolValue;
                int intValue;
                float floatValue;
                if (TryReadIntByNames(data, out intValue, "flightTimeMax", "FlightTimeMax") && intValue > 0)
                {
                    canFly = true;
                    return true;
                }

                if (TryReadBoolByNames(data, out boolValue, "usesHover", "UsesHover", "canFly", "CanFly") && boolValue)
                {
                    canFly = true;
                    return true;
                }

                if (TryReadFloatByNames(data, out floatValue, "flySpeed", "FlySpeed") && floatValue > 0.1f)
                {
                    canFly = true;
                    return true;
                }

                canFly = false;
                return true;
            }
            catch (Exception error)
            {
                Logger.Debug("MovementSafeLandingEquipmentCompat", "Mount fly detection failed: " + error.Message);
                return false;
            }
        }

        private static bool TryResolveMountNoFallDamage(int mountType, out bool noFallDamage)
        {
            noFallDamage = false;
            if (mountType < 0)
            {
                return false;
            }

            try
            {
                var mountTypeType = FindType("Terraria.Mount");
                var mounts = mountTypeType == null ? null : GetStatic(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                if (data == null)
                {
                    return false;
                }

                float fallDamage;
                if (TryReadFloatByNames(data, out fallDamage, "fallDamage", "FallDamage"))
                {
                    noFallDamage = fallDamage <= 0f;
                    return true;
                }

                return false;
            }
            catch (Exception error)
            {
                Logger.Debug("MovementSafeLandingEquipmentCompat", "Mount no-fall detection failed: " + error.Message);
                return false;
            }
        }

        private static bool TryReadExpertOrMasterMode()
        {
            bool expert;
            bool master;
            var hasExpert = TryReadStaticBool(TerrariaRuntimeTypes.MainType, "expertMode", out expert);
            var hasMaster = TryReadStaticBool(TerrariaRuntimeTypes.MainType, "masterMode", out master);
            return (hasExpert && expert) || (hasMaster && master);
        }
    }
}
