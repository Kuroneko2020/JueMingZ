using System.Collections.Generic;
using JueMingZ.Config;

namespace JueMingZ.Automation.AutoRecovery
{
    public static class AutoRecoveryItemFilter
    {
        public static bool IsHealItemEnabled(AppSettings settings, int itemType)
        {
            return IsItemEnabled(settings == null ? null : settings.AutoHealBlockedItemTypes, itemType);
        }

        public static bool IsManaItemEnabled(AppSettings settings, int itemType)
        {
            return IsItemEnabled(settings == null ? null : settings.AutoManaBlockedItemTypes, itemType);
        }

        public static bool ToggleHealItem(AppSettings settings, int itemType, out bool enabled)
        {
            return ToggleItem(settings, true, itemType, out enabled);
        }

        public static bool ToggleManaItem(AppSettings settings, int itemType, out bool enabled)
        {
            return ToggleItem(settings, false, itemType, out enabled);
        }

        public static int CountBlockedHealItems(AppSettings settings)
        {
            return CountPositive(settings == null ? null : settings.AutoHealBlockedItemTypes);
        }

        public static int CountBlockedManaItems(AppSettings settings)
        {
            return CountPositive(settings == null ? null : settings.AutoManaBlockedItemTypes);
        }

        private static bool ToggleItem(AppSettings settings, bool heal, int itemType, out bool enabled)
        {
            enabled = true;
            if (settings == null || itemType <= 0)
            {
                return false;
            }

            var blocked = heal ? settings.AutoHealBlockedItemTypes : settings.AutoManaBlockedItemTypes;
            if (blocked == null)
            {
                blocked = new List<int>();
                if (heal)
                {
                    settings.AutoHealBlockedItemTypes = blocked;
                }
                else
                {
                    settings.AutoManaBlockedItemTypes = blocked;
                }
            }

            var index = blocked.IndexOf(itemType);
            if (index >= 0)
            {
                for (var current = blocked.Count - 1; current >= 0; current--)
                {
                    if (blocked[current] == itemType)
                    {
                        blocked.RemoveAt(current);
                    }
                }

                enabled = true;
                return true;
            }

            blocked.Add(itemType);
            enabled = false;
            return true;
        }

        private static bool IsItemEnabled(IList<int> blocked, int itemType)
        {
            if (itemType <= 0)
            {
                return false;
            }

            if (blocked == null || blocked.Count <= 0)
            {
                return true;
            }

            for (var index = 0; index < blocked.Count; index++)
            {
                if (blocked[index] == itemType)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountPositive(IList<int> values)
        {
            if (values == null || values.Count <= 0)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index] > 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
