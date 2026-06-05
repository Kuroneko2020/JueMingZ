using System;
using System.Collections.Generic;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using Terraria;
using Terraria.ID;

namespace JueMingZ.Automation.BuffAndRecovery
{
    public static class RecoveryPotionDefinitionCatalog
    {
        private static readonly object SyncRoot = new object();
        private static RecoveryPotionDefinition[] _healDefinitions;
        private static RecoveryPotionDefinition[] _manaDefinitions;
        private static bool _loaded;

        public static RecoveryPotionDefinition[] GetHealDefinitions()
        {
            EnsureLoaded();
            return _healDefinitions ?? new RecoveryPotionDefinition[0];
        }

        public static RecoveryPotionDefinition[] GetManaDefinitions()
        {
            EnsureLoaded();
            return _manaDefinitions ?? new RecoveryPotionDefinition[0];
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _loaded = false;
                _healDefinitions = null;
                _manaDefinitions = null;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_loaded)
                {
                    return;
                }

                try
                {
                    LoadDefinitions(out _healDefinitions, out _manaDefinitions);
                }
                catch (Exception error)
                {
                    _healDefinitions = new RecoveryPotionDefinition[0];
                    _manaDefinitions = new RecoveryPotionDefinition[0];
                    LogThrottle.WarnThrottled(
                        "recovery-potion-definition-load-failed",
                        TimeSpan.FromSeconds(30),
                        "RecoveryPotionDefinitionCatalog",
                        "Failed to load recovery item definitions: " + error.Message);
                }

                _loaded = true;
            }
        }

        private static void LoadDefinitions(out RecoveryPotionDefinition[] healDefinitions, out RecoveryPotionDefinition[] manaDefinitions)
        {
            var heal = new List<RecoveryPotionDefinition>();
            var mana = new List<RecoveryPotionDefinition>();
            var count = Math.Max(0, (int)ItemID.Count);
            for (var itemType = 1; itemType < count; itemType++)
            {
                var item = new Item();
                item.SetDefaults(itemType);
                if (TerrariaItemReadCompat.Type(item) <= 0)
                {
                    continue;
                }

                var healLife = TerrariaItemReadCompat.HealLife(item);
                var healMana = TerrariaItemReadCompat.HealMana(item);
                if (healLife <= 0 && healMana <= 0)
                {
                    continue;
                }

                var definition = new RecoveryPotionDefinition
                {
                    ItemType = TerrariaItemReadCompat.Type(item),
                    ItemName = TerrariaItemReadCompat.Name(item),
                    HealLife = healLife,
                    HealMana = healMana,
                    Potion = TerrariaItemReadCompat.IsPotion(item),
                    Consumable = TerrariaItemReadCompat.IsConsumable(item),
                    BuffType = TerrariaItemReadCompat.BuffType(item),
                    BuffTime = TerrariaItemReadCompat.BuffTime(item)
                };

                if (definition.Potion && definition.HealLife > 0)
                {
                    heal.Add(definition);
                }

                if (definition.HealMana > 0)
                {
                    mana.Add(definition);
                }
            }

            heal.Sort(CompareHealDefinitions);
            mana.Sort(CompareManaDefinitions);
            healDefinitions = heal.ToArray();
            manaDefinitions = mana.ToArray();
        }

        private static int CompareHealDefinitions(RecoveryPotionDefinition left, RecoveryPotionDefinition right)
        {
            var heal = right.HealLife.CompareTo(left.HealLife);
            return heal != 0 ? heal : left.ItemType.CompareTo(right.ItemType);
        }

        private static int CompareManaDefinitions(RecoveryPotionDefinition left, RecoveryPotionDefinition right)
        {
            var mana = right.HealMana.CompareTo(left.HealMana);
            return mana != 0 ? mana : left.ItemType.CompareTo(right.ItemType);
        }
    }
}
