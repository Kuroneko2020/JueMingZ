using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Information
{
    internal static class InformationNpcNameCompat
    {
        private const ulong NameCacheRefreshTicks = 24;
        private const int MaxTypeNameCacheEntries = 1024;
        private const int MaxNameCacheEntries = 512;
        private static readonly Dictionary<int, string> TypeNameCache = new Dictionary<int, string>();
        private static readonly Dictionary<string, NameCacheEntry> NameCache = new Dictionary<string, NameCacheEntry>(StringComparer.Ordinal);
        private static readonly Queue<string> NameCacheOrder = new Queue<string>();
        private static bool _langMethodMissingLogged;

        public static string ResolveDisplayName(object npc, int npcType, int whoAmI, string mode, ulong gameUpdateCount)
        {
            if (npc == null || string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (string.Equals(mode, "Type", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveTypeName(npcType);
            }

            var key = whoAmI.ToString(CultureInfo.InvariantCulture) + ":" +
                      npcType.ToString(CultureInfo.InvariantCulture) + ":Name";
            NameCacheEntry cached;
            if (NameCache.TryGetValue(key, out cached) && gameUpdateCount < cached.RefreshAfterTick)
            {
                return cached.Value;
            }

            var label = FirstNonEmpty(
                InformationReflection.TryReadString(npc, "GivenName"),
                InformationReflection.TryReadString(npc, "GivenOrTypeName"),
                InformationReflection.TryReadString(npc, "FullName"),
                InformationReflection.TryReadString(npc, "TypeName"),
                InformationReflection.TryReadString(npc, "name"),
                ResolveTypeName(npcType),
                npcType.ToString(CultureInfo.InvariantCulture));
            AddNameCache(key, label, gameUpdateCount + NameCacheRefreshTicks);
            return label;
        }

        public static string ResolveTypeName(int npcType)
        {
            string cached;
            if (TypeNameCache.TryGetValue(npcType, out cached))
            {
                return cached;
            }

            var value = string.Empty;
            object raw;
            var langType = InformationReflection.FindType("Terraria.Lang") ??
                           InformationReflection.FindType("Terraria.Localization.Lang");
            if (InformationReflection.TryInvokeStatic(langType, "GetNPCNameValue", new object[] { npcType }, out raw))
            {
                value = Convert.ToString(raw, CultureInfo.InvariantCulture);
            }
            else if (!_langMethodMissingLogged)
            {
                _langMethodMissingLogged = true;
                LogThrottle.WarnThrottled(
                    "information-lang-npc-name-unavailable",
                    TimeSpan.FromSeconds(30),
                    "InformationNpcNameCompat",
                    "Lang.GetNPCNameValue is unavailable; NPC labels will fall back to type ids.");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                value = npcType.ToString(CultureInfo.InvariantCulture);
            }

            if (TypeNameCache.Count < MaxTypeNameCacheEntries)
            {
                TypeNameCache[npcType] = value;
            }

            return value;
        }

        public static string ResolveNpcTypeName(object npc, int fallbackType)
        {
            if (npc != null)
            {
                var typeName = InformationReflection.TryReadString(npc, "TypeName");
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    return typeName;
                }

                int netId;
                if (InformationReflection.TryReadInt(npc, "netID", out netId) && netId != 0)
                {
                    return ResolveTypeName(netId);
                }
            }

            return ResolveTypeName(fallbackType);
        }

        private static void AddNameCache(string key, string value, ulong refreshAfterTick)
        {
            if (!NameCache.ContainsKey(key))
            {
                NameCacheOrder.Enqueue(key);
            }

            NameCache[key] = new NameCacheEntry(value ?? string.Empty, refreshAfterTick);
            while (NameCacheOrder.Count > MaxNameCacheEntries)
            {
                var oldest = NameCacheOrder.Dequeue();
                NameCache.Remove(oldest);
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
                    return values[index];
                }
            }

            return string.Empty;
        }

        private struct NameCacheEntry
        {
            public string Value;
            public ulong RefreshAfterTick;

            public NameCacheEntry(string value, ulong refreshAfterTick)
            {
                Value = value ?? string.Empty;
                RefreshAfterTick = refreshAfterTick;
            }
        }
    }
}
