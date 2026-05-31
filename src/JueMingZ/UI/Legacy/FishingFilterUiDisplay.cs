using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;

namespace JueMingZ.UI.Legacy
{
    internal static class FishingFilterUiDisplay
    {
        private const int MaxItemNameCacheEntries = 512;
        private static readonly Dictionary<int, string> ItemNameCache = new Dictionary<int, string>();
        private static readonly Queue<int> ItemNameCacheOrder = new Queue<int>();

        public static string ResolveDisplayName(string kind, int id, string snapshot)
        {
            if (id <= 0)
            {
                return string.IsNullOrWhiteSpace(snapshot) ? string.Empty : snapshot.Trim();
            }

            if (string.Equals(kind, FishingCatchKinds.Item, StringComparison.OrdinalIgnoreCase))
            {
                return FirstNonEmpty(ResolveItemName(id), snapshot, "#" + id.ToString(CultureInfo.InvariantCulture));
            }

            if (string.Equals(kind, FishingCatchKinds.NPC, StringComparison.OrdinalIgnoreCase))
            {
                return FirstNonEmpty(InformationNpcNameCompat.ResolveTypeName(id), snapshot, "#" + id.ToString(CultureInfo.InvariantCulture));
            }

            return FirstNonEmpty(snapshot, "#" + id.ToString(CultureInfo.InvariantCulture));
        }

        private static string ResolveItemName(int itemId)
        {
            string cached;
            if (ItemNameCache.TryGetValue(itemId, out cached))
            {
                return cached;
            }

            var value = string.Empty;
            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetItemNameValue", new object[] { itemId }, out raw) && raw != null)
            {
                value = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                if (!ItemNameCache.ContainsKey(itemId))
                {
                    ItemNameCacheOrder.Enqueue(itemId);
                }

                ItemNameCache[itemId] = value;
                while (ItemNameCacheOrder.Count > MaxItemNameCacheEntries)
                {
                    ItemNameCache.Remove(ItemNameCacheOrder.Dequeue());
                }
            }

            return value;
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
