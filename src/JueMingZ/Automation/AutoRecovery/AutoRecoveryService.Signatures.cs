using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.AutoRecovery
{
    public static partial class AutoRecoveryService
    {
        private static string BuildInventorySignature(GameStateSnapshot snapshot)
        {
            var whitelist = GetWhitelistedItemTypesForSignature();
            if (whitelist.Count <= 0)
            {
                return string.Empty;
            }

            var totals = new Dictionary<int, int>();
            object player;
            if (TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                AppendContainerInventorySignature(player, "Inventory", whitelist, totals);
                if (InventoryMutationCompat.TryPlayerUsesVoidBag(player))
                {
                    AppendContainerInventorySignature(player, "VoidBag", whitelist, totals);
                }
            }
            else if (snapshot != null && snapshot.Inventory != null && snapshot.Inventory.Items != null)
            {
                for (var index = 0; index < snapshot.Inventory.Items.Count; index++)
                {
                    var item = snapshot.Inventory.Items[index];
                    if (item == null || item.Type <= 0 || item.Stack <= 0 || !whitelist.Contains(item.Type))
                    {
                        continue;
                    }

                    int stack;
                    totals.TryGetValue(item.Type, out stack);
                    totals[item.Type] = stack + item.Stack;
                }
            }

            var keys = new List<int>(totals.Keys);
            keys.Sort();
            var builder = new StringBuilder();
            for (var index = 0; index < keys.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append("|");
                }

                var key = keys[index];
                builder.Append(key.ToString(CultureInfo.InvariantCulture));
                builder.Append(":");
                builder.Append(totals[key].ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string BuildBuffSignature(IReadOnlyList<BuffSnapshot> activeBuffs)
        {
            var whitelist = GetWhitelistedBuffTypesForSignature();
            if (whitelist.Count <= 0)
            {
                return string.Empty;
            }

            var active = new HashSet<int>();
            if (activeBuffs != null)
            {
                for (var index = 0; index < activeBuffs.Count; index++)
                {
                    var buff = activeBuffs[index];
                    if (buff != null && buff.BuffType > 0)
                    {
                        active.Add(buff.BuffType);
                    }
                }
            }

            var types = new List<int>(whitelist);
            types.Sort();
            var builder = new StringBuilder();
            for (var index = 0; index < types.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append("|");
                }

                var buffType = types[index];
                builder.Append(buffType.ToString(CultureInfo.InvariantCulture));
                builder.Append(":");
                builder.Append(active.Contains(buffType) ? "1" : "0");
            }

            return builder.ToString();
        }

        private static void AppendContainerInventorySignature(object player, string sourceContainer, HashSet<int> whitelist, Dictionary<int, int> totals)
        {
            IList items;
            string message;
            if (player == null || whitelist == null || totals == null ||
                !InventoryMutationCompat.TryGetContainerItems(player, sourceContainer, out items, out message) ||
                items == null)
            {
                return;
            }

            for (var slot = 0; slot < items.Count; slot++)
            {
                try
                {
                    int itemType;
                    int stack;
                    int buffType;
                    int buffTime;
                    bool summon;
                    string itemName;
                    if (!InventoryMutationCompat.TryReadItemFields(items[slot], out itemType, out itemName, out stack, out buffType, out buffTime, out summon) ||
                        itemType <= 0 ||
                        stack <= 0 ||
                        !whitelist.Contains(itemType))
                    {
                        continue;
                    }

                    int current;
                    totals.TryGetValue(itemType, out current);
                    totals[itemType] = current + stack;
                }
                catch
                {
                }
            }
        }

        private static HashSet<int> GetWhitelistedItemTypesForSignature()
        {
            var result = new HashSet<int>();
            var settings = ConfigService.AppSettings;
            if (settings == null || settings.AutoBuffWhitelist == null)
            {
                return result;
            }

            for (var index = 0; index < settings.AutoBuffWhitelist.Count; index++)
            {
                var entry = settings.AutoBuffWhitelist[index];
                if (entry != null && entry.ItemType > 0)
                {
                    result.Add(entry.ItemType);
                }
            }

            return result;
        }

        private static HashSet<int> GetWhitelistedBuffTypesForSignature()
        {
            var result = new HashSet<int>();
            var settings = ConfigService.AppSettings;
            if (settings == null || settings.AutoBuffWhitelist == null)
            {
                return result;
            }

            for (var index = 0; index < settings.AutoBuffWhitelist.Count; index++)
            {
                var entry = settings.AutoBuffWhitelist[index];
                if (entry != null && entry.BuffType > 0)
                {
                    result.Add(entry.BuffType);
                }
            }

            return result;
        }

    }
}
