using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.Information;

namespace JueMingZ.Compat
{
    internal static class ItemSwapFamilyCompat
    {
        private static readonly int[][] SwapFamilies =
        {
            new[] { 2611, 5526 }, // Flairoon / Flairon
            new[] { 4131, 5325 }, // Void Lens / Closed Void Bag
            new[] { 4346, 5391 }, // Encumbering Stone / Uncumbering Stone
            new[] { 4767, 5453 }, // Guide to Critter Companionship / Inactive
            new[] { 5059, 5060 }, // Capricorn Legs / Capricorn Tail
            new[] { 5309, 5454 }, // Guide to Environmental Preservation / Inactive
            new[] { 5323, 5455 }, // Guide to Peaceful Coexistence / Inactive
            new[] { 5324, 5329, 5330 }, // Rubblemaker small / medium / large
            new[] { 5358, 5359, 5360, 5361, 5437 } // Shellphone modes
        };

        private static readonly Dictionary<int, int[]> FamilyByItemType = BuildFamilyMap();

        private static readonly Dictionary<int, string> KnownFallbackNames = new Dictionary<int, string>
        {
            { 2611, "Flairoon" },
            { 5526, "Flairon" },
            { 4131, "Void Lens" },
            { 5325, "Closed Void Bag" },
            { 4346, "Encumbering Stone" },
            { 5391, "Uncumbering Stone" },
            { 4767, "Guide to Critter Companionship" },
            { 5453, "Guide to Critter Companionship (Inactive)" },
            { 5059, "Capricorn Legs" },
            { 5060, "Capricorn Tail" },
            { 5309, "Guide to Environmental Preservation" },
            { 5454, "Guide to Environmental Preservation (Inactive)" },
            { 5323, "Guide to Peaceful Coexistence" },
            { 5455, "Guide to Peaceful Coexistence (Inactive)" },
            { 5324, "Rubblemaker (Small)" },
            { 5329, "Rubblemaker (Medium)" },
            { 5330, "Rubblemaker (Large)" },
            { 5358, "Shellphone (Home)" },
            { 5359, "Shellphone (Spawn)" },
            { 5360, "Shellphone (Ocean)" },
            { 5361, "Shellphone (Underworld)" },
            { 5437, "Shellphone (Dummy)" }
        };

        public static bool TryGetSwapFamily(int itemType, out int[] family)
        {
            return FamilyByItemType.TryGetValue(itemType, out family);
        }

        public static int[] GetVisibleSwapFamily(int itemType)
        {
            int[] family;
            if (!TryGetSwapFamily(itemType, out family) || family == null)
            {
                return null;
            }

            if (Contains(family, 5437))
            {
                return new[] { 5358, 5359, 5360, 5361 };
            }

            return family;
        }

        public static bool AreEquivalentItemTypes(int firstItemType, int secondItemType)
        {
            if (firstItemType <= 0 || secondItemType <= 0)
            {
                return false;
            }

            if (firstItemType == secondItemType)
            {
                return true;
            }

            int[] family;
            return TryGetSwapFamily(firstItemType, out family) &&
                   family != null &&
                   Contains(family, secondItemType);
        }

        public static void AddEquivalentItemTypes(int itemType, HashSet<int> destination)
        {
            if (destination == null || itemType <= 0)
            {
                return;
            }

            int[] family;
            if (TryGetSwapFamily(itemType, out family) && family != null)
            {
                for (var index = 0; index < family.Length; index++)
                {
                    if (family[index] > 0)
                    {
                        destination.Add(family[index]);
                    }
                }

                return;
            }

            destination.Add(itemType);
        }

        public static string ResolveItemDisplayName(int itemType, string fallbackName)
        {
            if (itemType <= 0)
            {
                return string.Empty;
            }

            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetItemNameValue", new object[] { itemType }, out raw))
            {
                var localized = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(localized))
                {
                    return localized.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackName))
            {
                return fallbackName.Trim();
            }

            string known;
            if (KnownFallbackNames.TryGetValue(itemType, out known))
            {
                return known;
            }

            return itemType.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryChangeInventoryItemType(
            object player,
            int slot,
            int targetItemType,
            out int beforeItemType,
            out string beforeItemName,
            out int afterItemType,
            out string afterItemName,
            out string message)
        {
            beforeItemType = 0;
            beforeItemName = string.Empty;
            afterItemType = 0;
            afterItemName = string.Empty;
            message = string.Empty;

            if (targetItemType <= 0)
            {
                message = "Target item type is invalid.";
                return false;
            }

            object item;
            if (!InventoryMutationCompat.TryGetItem(player, "Inventory", slot, out item, out message))
            {
                return false;
            }

            int stack;
            int buffType;
            int buffTime;
            bool summon;
            if (!InventoryMutationCompat.TryReadItemFields(item, out beforeItemType, out beforeItemName, out stack, out buffType, out buffTime, out summon))
            {
                message = "Cannot read inventory item before form change.";
                return false;
            }

            if (beforeItemType == targetItemType)
            {
                afterItemType = beforeItemType;
                afterItemName = beforeItemName ?? string.Empty;
                message = "Inventory item already has target form.";
                return true;
            }

            if (stack <= 0 || !AreEquivalentItemTypes(beforeItemType, targetItemType))
            {
                message = "Inventory item is not in the requested swap family.";
                return false;
            }

            if (!TryInvokeItemTypeChange(item, targetItemType, out message))
            {
                return false;
            }

            if (!InventoryMutationCompat.TryReadItemFields(item, out afterItemType, out afterItemName, out stack, out buffType, out buffTime, out summon))
            {
                message = "Cannot read inventory item after form change.";
                return false;
            }

            if (afterItemType != targetItemType)
            {
                message = "Inventory item form change did not reach target type; afterType=" + afterItemType.ToString(CultureInfo.InvariantCulture) + ".";
                return false;
            }

            message = "Inventory item form changed from " + beforeItemType.ToString(CultureInfo.InvariantCulture) + " to " + afterItemType.ToString(CultureInfo.InvariantCulture) + ".";
            return true;
        }

        private static Dictionary<int, int[]> BuildFamilyMap()
        {
            var map = new Dictionary<int, int[]>();
            for (var familyIndex = 0; familyIndex < SwapFamilies.Length; familyIndex++)
            {
                var family = SwapFamilies[familyIndex];
                if (family == null || family.Length <= 0)
                {
                    continue;
                }

                for (var itemIndex = 0; itemIndex < family.Length; itemIndex++)
                {
                    var itemType = family[itemIndex];
                    if (itemType > 0)
                    {
                        map[itemType] = family;
                    }
                }
            }

            return map;
        }

        private static bool TryInvokeItemTypeChange(object item, int targetItemType, out string message)
        {
            message = string.Empty;
            if (item == null)
            {
                message = "Item instance is null.";
                return false;
            }

            try
            {
                var methods = item.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, "ChangeItemType", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                    {
                        method.Invoke(item, new object[] { targetItemType });
                        return true;
                    }
                }
            }
            catch (Exception error)
            {
                message = "ChangeItemType invocation failed: " + (error.InnerException == null ? error.Message : error.InnerException.Message);
                return false;
            }

            message = "Item.ChangeItemType(int) was not found.";
            return false;
        }

        private static bool Contains(IList values, int itemType)
        {
            if (values == null)
            {
                return false;
            }

            for (var index = 0; index < values.Count; index++)
            {
                try
                {
                    if (Convert.ToInt32(values[index], CultureInfo.InvariantCulture) == itemType)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }
    }
}
