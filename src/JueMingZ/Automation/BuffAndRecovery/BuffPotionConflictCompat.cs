using System;
using System.Collections;
using System.Collections.Generic;
using JueMingZ.Compat;

namespace JueMingZ.Automation.BuffAndRecovery
{
    internal static class BuffPotionConflictCompat
    {
        public static string FindConflictGroup(int buffType, HashSet<int> activeBuffs, out bool supported)
        {
            supported = true;
            if (buffType <= 0 || activeBuffs == null || activeBuffs.Count <= 0)
            {
                return string.Empty;
            }

            string group;
            if (TryFindArrayConflict("Terraria.Main", "meleeBuff", "MeleeFlask", buffType, activeBuffs, out group))
            {
                return group;
            }

            if (TryFindArrayConflict("Terraria.Main", "lightPet", "LightPet", buffType, activeBuffs, out group))
            {
                return group;
            }

            if (TryFindArrayConflict("Terraria.Main", "vanityPet", "VanityPet", buffType, activeBuffs, out group))
            {
                return group;
            }

            bool wellFedSupported;
            if (TryFindWellFedConflict(buffType, activeBuffs, out group, out wellFedSupported))
            {
                return group;
            }

            supported = wellFedSupported;
            return string.Empty;
        }

        private static bool TryFindArrayConflict(string typeName, string memberName, string groupName, int buffType, HashSet<int> activeBuffs, out string group)
        {
            group = string.Empty;
            var type = FindType(typeName);
            var array = GetStaticMember(type, memberName) as IList;
            if (array == null || !ReadBoolArray(array, buffType))
            {
                return false;
            }

            foreach (var active in activeBuffs)
            {
                if (active != buffType && ReadBoolArray(array, active))
                {
                    group = groupName;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindWellFedConflict(int buffType, HashSet<int> activeBuffs, out string group, out bool supported)
        {
            group = string.Empty;
            supported = false;
            var type = FindType("Terraria.ID.BuffID+Sets");
            var array = GetStaticMember(type, "IsWellFed") as IList;
            if (array == null)
            {
                return false;
            }

            supported = true;
            if (!ReadBoolArray(array, buffType))
            {
                return false;
            }

            foreach (var active in activeBuffs)
            {
                if (active != buffType && ReadBoolArray(array, active))
                {
                    group = "WellFed";
                    return true;
                }
            }

            return false;
        }

        private static bool ReadBoolArray(IList array, int index)
        {
            if (array == null || index < 0 || index >= array.Count)
            {
                return false;
            }

            try
            {
                return Convert.ToBoolean(array[index]);
            }
            catch
            {
                return false;
            }
        }

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }

            if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
            {
                return field.GetValue(null);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, true, out var property)
                ? property.GetValue(null, null)
                : null;
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
