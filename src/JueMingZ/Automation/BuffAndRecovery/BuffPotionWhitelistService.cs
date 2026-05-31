using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Config;

namespace JueMingZ.Automation.BuffAndRecovery
{
    public static class BuffPotionWhitelistService
    {
        private static readonly object SyncRoot = new object();

        public static int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    EnsureList();
                    return ConfigService.AppSettings.AutoBuffWhitelist.Count;
                }
            }
        }

        public static bool ContainsItemType(int itemType)
        {
            if (itemType <= 0)
            {
                return false;
            }

            lock (SyncRoot)
            {
                EnsureList();
                var list = ConfigService.AppSettings.AutoBuffWhitelist;
                for (var index = 0; index < list.Count; index++)
                {
                    if (list[index] != null && list[index].ItemType == itemType)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool ContainsBuffType(int buffType)
        {
            if (buffType <= 0)
            {
                return false;
            }

            lock (SyncRoot)
            {
                EnsureList();
                var list = ConfigService.AppSettings.AutoBuffWhitelist;
                for (var index = 0; index < list.Count; index++)
                {
                    if (list[index] != null && list[index].BuffType == buffType)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool Add(BuffPotionCandidate candidate, out string message)
        {
            message = string.Empty;
            if (candidate == null || candidate.ItemType <= 0 || candidate.BuffType <= 0)
            {
                message = "No selected buff potion candidate.";
                return false;
            }

            lock (SyncRoot)
            {
                EnsureList();
                if (ContainsItemTypeLocked(candidate.ItemType))
                {
                    message = "Candidate is already whitelisted.";
                    return true;
                }

                ConfigService.AppSettings.AutoBuffWhitelist.Add(new BuffPotionWhitelistEntry
                {
                    ItemType = candidate.ItemType,
                    BuffType = candidate.BuffType,
                    ItemName = candidate.ItemName ?? string.Empty,
                    BuffName = candidate.BuffName ?? string.Empty
                });
                ConfigService.SaveAll();
            }

            BuffPotionDiagnostics.RefreshWhitelistFlags();
            message = "Added buff potion itemType=" + candidate.ItemType.ToString(CultureInfo.InvariantCulture) + " to whitelist.";
            return true;
        }

        public static bool Remove(BuffPotionCandidate candidate, out string message)
        {
            message = string.Empty;
            if (candidate == null || candidate.ItemType <= 0)
            {
                message = "No selected buff potion candidate.";
                return false;
            }

            var removed = false;
            lock (SyncRoot)
            {
                EnsureList();
                var list = ConfigService.AppSettings.AutoBuffWhitelist;
                for (var index = list.Count - 1; index >= 0; index--)
                {
                    if (list[index] != null && list[index].ItemType == candidate.ItemType)
                    {
                        list.RemoveAt(index);
                        removed = true;
                    }
                }

                if (removed)
                {
                    ConfigService.SaveAll();
                }
            }

            BuffPotionDiagnostics.RefreshWhitelistFlags();
            message = removed
                ? "Removed buff potion itemType=" + candidate.ItemType.ToString(CultureInfo.InvariantCulture) + " from whitelist."
                : "Selected candidate was not whitelisted.";
            return removed;
        }

        public static int RemoveByBuffType(int buffType, out BuffPotionWhitelistEntry firstRemoved, out string message)
        {
            firstRemoved = null;
            message = string.Empty;
            if (buffType <= 0)
            {
                message = "Invalid buffType.";
                return 0;
            }

            var removed = 0;
            lock (SyncRoot)
            {
                EnsureList();
                var list = ConfigService.AppSettings.AutoBuffWhitelist;
                for (var index = list.Count - 1; index >= 0; index--)
                {
                    var entry = list[index];
                    if (entry == null || entry.BuffType != buffType)
                    {
                        continue;
                    }

                    firstRemoved = firstRemoved ?? new BuffPotionWhitelistEntry
                    {
                        ItemType = entry.ItemType,
                        BuffType = entry.BuffType,
                        ItemName = entry.ItemName ?? string.Empty,
                        BuffName = entry.BuffName ?? string.Empty
                    };
                    list.RemoveAt(index);
                    removed++;
                }

                if (removed > 0)
                {
                    ConfigService.SaveAll();
                }
            }

            BuffPotionDiagnostics.RefreshWhitelistFlags();
            message = removed > 0
                ? "Removed " + removed.ToString(CultureInfo.InvariantCulture) + " buff potion whitelist entr" + (removed == 1 ? "y" : "ies") + " for buffType=" + buffType.ToString(CultureInfo.InvariantCulture) + "."
                : "No whitelist entry matched buffType=" + buffType.ToString(CultureInfo.InvariantCulture) + ".";
            return removed;
        }

        public static int Clear()
        {
            int count;
            lock (SyncRoot)
            {
                EnsureList();
                count = ConfigService.AppSettings.AutoBuffWhitelist.Count;
                ConfigService.AppSettings.AutoBuffWhitelist.Clear();
                ConfigService.SaveAll();
            }

            BuffPotionDiagnostics.RefreshWhitelistFlags();
            return count;
        }

        public static string BuildWhitelistJson()
        {
            lock (SyncRoot)
            {
                EnsureList();
                var list = ConfigService.AppSettings.AutoBuffWhitelist;
                var builder = new StringBuilder();
                builder.Append("[");
                for (var index = 0; index < list.Count; index++)
                {
                    var entry = list[index];
                    if (entry == null)
                    {
                        continue;
                    }

                    if (index > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append("{");
                    AppendRaw(builder, "itemType", entry.ItemType.ToString(CultureInfo.InvariantCulture), true);
                    AppendRaw(builder, "buffType", entry.BuffType.ToString(CultureInfo.InvariantCulture), true);
                    AppendString(builder, "itemName", entry.ItemName, true);
                    AppendString(builder, "buffName", entry.BuffName, false);
                    builder.Append("}");
                }

                builder.Append("]");
                return builder.ToString();
            }
        }

        public static HashSet<int> GetWhitelistedItemTypes()
        {
            lock (SyncRoot)
            {
                EnsureList();
                var set = new HashSet<int>();
                var list = ConfigService.AppSettings.AutoBuffWhitelist;
                for (var index = 0; index < list.Count; index++)
                {
                    if (list[index] != null && list[index].ItemType > 0)
                    {
                        set.Add(list[index].ItemType);
                    }
                }

                return set;
            }
        }

        private static bool ContainsItemTypeLocked(int itemType)
        {
            var list = ConfigService.AppSettings.AutoBuffWhitelist;
            for (var index = 0; index < list.Count; index++)
            {
                if (list[index] != null && list[index].ItemType == itemType)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureList()
        {
            if (ConfigService.AppSettings.AutoBuffWhitelist == null)
            {
                ConfigService.AppSettings.AutoBuffWhitelist = new List<BuffPotionWhitelistEntry>();
            }
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":\"").Append(EscapeJson(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
