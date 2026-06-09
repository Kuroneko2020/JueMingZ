using System;
using System.Collections.Generic;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState.Npcs;
using Terraria;
using Terraria.ID;

namespace JueMingZ.Automation.WorldAutomation
{
    public sealed class AutoCaptureCritterCategoryDefinition
    {
        public string Id { get; set; }
        public string Label { get; set; }

        public AutoCaptureCritterCategoryDefinition()
        {
            Id = string.Empty;
            Label = string.Empty;
        }
    }

    public static class AutoCaptureCritterCategoryCatalog
    {
        public const string Bait = "bait";
        public const string Fairy = "fairy";
        public const string GoldCritter = "gold_critter";
        public const string GemCritter = "gem_critter";
        public const string NormalCritter = "normal_critter";
        public const string TruffleWorm = "truffle_worm";
        public const string EmpressButterfly = "empress_butterfly";
        public const string Other = "other";

        private static readonly object BaitPowerSyncRoot = new object();
        private static readonly Dictionary<int, int> BaitPowerByCatchItem = new Dictionary<int, int>();
        private static readonly HashSet<int> GoldCritterNpcTypes = new HashSet<int>
        {
            NPCID.GoldBird,
            NPCID.GoldBunny,
            NPCID.GoldButterfly,
            NPCID.GoldFrog,
            NPCID.GoldGrasshopper,
            NPCID.GoldMouse,
            NPCID.GoldWorm,
            NPCID.SquirrelGold,
            NPCID.GoldGoldfish,
            NPCID.GoldGoldfishWalker,
            NPCID.GoldDragonfly,
            NPCID.GoldLadyBug,
            NPCID.GoldWaterStrider,
            NPCID.GoldSeahorse
        };

        private static readonly HashSet<int> GoldCritterCatchItems = new HashSet<int>
        {
            ItemID.GoldBird,
            ItemID.GoldBunny,
            ItemID.GoldButterfly,
            ItemID.GoldFrog,
            ItemID.GoldGrasshopper,
            ItemID.GoldMouse,
            ItemID.GoldWorm,
            ItemID.SquirrelGold,
            ItemID.GoldGoldfish,
            ItemID.GoldDragonfly,
            ItemID.GoldLadyBug,
            ItemID.GoldWaterStrider,
            ItemID.GoldSeahorse
        };

        private static readonly HashSet<int> KnownBaitCatchItems = new HashSet<int>
        {
            ItemID.Firefly,
            ItemID.Worm,
            ItemID.LightningBug,
            ItemID.Grasshopper,
            ItemID.EnchantedNightcrawler,
            ItemID.Grubby,
            ItemID.Sluggy,
            ItemID.Buggy,
            ItemID.LadyBug,
            ItemID.WaterStrider
        };

        private static readonly AutoCaptureCritterCategoryDefinition[] OptionDefinitions =
        {
            new AutoCaptureCritterCategoryDefinition { Id = Bait, Label = "鱼饵" },
            new AutoCaptureCritterCategoryDefinition { Id = Fairy, Label = "仙灵" },
            new AutoCaptureCritterCategoryDefinition { Id = GoldCritter, Label = "金色动物" },
            new AutoCaptureCritterCategoryDefinition { Id = GemCritter, Label = "宝石动物" },
            new AutoCaptureCritterCategoryDefinition { Id = NormalCritter, Label = "普通动物" },
            new AutoCaptureCritterCategoryDefinition { Id = TruffleWorm, Label = "松露虫" },
            new AutoCaptureCritterCategoryDefinition { Id = EmpressButterfly, Label = "七彩草蛉" },
            new AutoCaptureCritterCategoryDefinition { Id = Other, Label = "其他" }
        };

        public static AutoCaptureCritterCategoryDefinition[] Options
        {
            get { return OptionDefinitions; }
        }

        public static void ApplyDefaultOptions(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.MiscAutoCaptureCritterBaitEnabled = true;
            settings.MiscAutoCaptureCritterFairyEnabled = true;
            settings.MiscAutoCaptureCritterGoldCritterEnabled = true;
            settings.MiscAutoCaptureCritterGemCritterEnabled = true;
            settings.MiscAutoCaptureCritterNormalCritterEnabled = true;
            settings.MiscAutoCaptureCritterTruffleWormEnabled = true;
            settings.MiscAutoCaptureCritterEmpressButterflyEnabled = true;
            settings.MiscAutoCaptureCritterOtherEnabled = true;
            settings.MiscAutoCaptureCritterCategoryDefaultsMigrated = true;
        }

        public static AutoCaptureCritterCategoryDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (var index = 0; index < OptionDefinitions.Length; index++)
            {
                var option = OptionDefinitions[index];
                if (option != null && string.Equals(option.Id, id, StringComparison.Ordinal))
                {
                    return option;
                }
            }

            return null;
        }

        public static AutoCaptureCritterCategoryDefinition Classify(NpcSnapshot critter)
        {
            return Find(ClassifyId(critter)) ?? Find(Other);
        }

        public static bool IsEnabledFor(AppSettings settings, NpcSnapshot critter)
        {
            return GetEnabled(settings, ClassifyId(critter));
        }

        public static bool GetEnabled(AppSettings settings, string id)
        {
            settings = settings ?? AppSettings.CreateDefault();
            switch (id)
            {
                case Bait:
                    return settings.MiscAutoCaptureCritterBaitEnabled;
                case Fairy:
                    return settings.MiscAutoCaptureCritterFairyEnabled;
                case GoldCritter:
                    return settings.MiscAutoCaptureCritterGoldCritterEnabled;
                case GemCritter:
                    return settings.MiscAutoCaptureCritterGemCritterEnabled;
                case NormalCritter:
                    return settings.MiscAutoCaptureCritterNormalCritterEnabled;
                case TruffleWorm:
                    return settings.MiscAutoCaptureCritterTruffleWormEnabled;
                case EmpressButterfly:
                    return settings.MiscAutoCaptureCritterEmpressButterflyEnabled;
                case Other:
                    return settings.MiscAutoCaptureCritterOtherEnabled;
                default:
                    return false;
            }
        }

        public static void SetEnabled(AppSettings settings, string id, bool enabled)
        {
            if (settings == null)
            {
                return;
            }

            switch (id)
            {
                case Bait:
                    settings.MiscAutoCaptureCritterBaitEnabled = enabled;
                    break;
                case Fairy:
                    settings.MiscAutoCaptureCritterFairyEnabled = enabled;
                    break;
                case GoldCritter:
                    settings.MiscAutoCaptureCritterGoldCritterEnabled = enabled;
                    break;
                case GemCritter:
                    settings.MiscAutoCaptureCritterGemCritterEnabled = enabled;
                    break;
                case NormalCritter:
                    settings.MiscAutoCaptureCritterNormalCritterEnabled = enabled;
                    break;
                case TruffleWorm:
                    settings.MiscAutoCaptureCritterTruffleWormEnabled = enabled;
                    break;
                case EmpressButterfly:
                    settings.MiscAutoCaptureCritterEmpressButterflyEnabled = enabled;
                    break;
                case Other:
                    settings.MiscAutoCaptureCritterOtherEnabled = enabled;
                    break;
            }
        }

        public static int CountDisabled(AppSettings settings)
        {
            var count = 0;
            for (var index = 0; index < OptionDefinitions.Length; index++)
            {
                var option = OptionDefinitions[index];
                if (option != null && !GetEnabled(settings, option.Id))
                {
                    count++;
                }
            }

            return count;
        }

        internal static void ResetForTesting()
        {
            lock (BaitPowerSyncRoot)
            {
                BaitPowerByCatchItem.Clear();
            }
        }

        private static string ClassifyId(NpcSnapshot critter)
        {
            if (critter == null)
            {
                return Other;
            }

            var npcType = critter.Type;
            var catchItem = critter.CatchItem;

            // This resolver is only a pre-capture filter; the actual net swing,
            // slot switching, and ItemCheck writes remain owned by ActionQueue/Compat.
            if (npcType == NPCID.TruffleWorm ||
                npcType == NPCID.TruffleWormDigger ||
                catchItem == ItemID.TruffleWorm)
            {
                return TruffleWorm;
            }

            if (npcType == NPCID.EmpressButterfly ||
                catchItem == ItemID.EmpressButterfly)
            {
                return EmpressButterfly;
            }

            if (npcType == NPCID.FairyCritterPink ||
                npcType == NPCID.FairyCritterGreen ||
                npcType == NPCID.FairyCritterBlue ||
                catchItem == ItemID.FairyCritterPink ||
                catchItem == ItemID.FairyCritterGreen ||
                catchItem == ItemID.FairyCritterBlue)
            {
                return Fairy;
            }

            if (GoldCritterNpcTypes.Contains(npcType) ||
                GoldCritterCatchItems.Contains(catchItem))
            {
                return GoldCritter;
            }

            if ((npcType >= NPCID.GemSquirrelAmethyst && npcType <= NPCID.GemBunnyAmber) ||
                (catchItem >= ItemID.GemSquirrelAmethyst && catchItem <= ItemID.GemBunnyAmber))
            {
                return GemCritter;
            }

            if (KnownBaitCatchItems.Contains(catchItem) ||
                HasBaitPower(catchItem))
            {
                return Bait;
            }

            return critter.Critter ? NormalCritter : Other;
        }

        private static bool HasBaitPower(int catchItem)
        {
            if (catchItem <= 0)
            {
                return false;
            }

            return ResolveBaitPower(catchItem) > 0;
        }

        private static int ResolveBaitPower(int catchItem)
        {
            lock (BaitPowerSyncRoot)
            {
                int cached;
                if (BaitPowerByCatchItem.TryGetValue(catchItem, out cached))
                {
                    return cached;
                }
            }

            var baitPower = 0;
            try
            {
                if (catchItem > 0 && catchItem < ItemID.Count)
                {
                    var item = new Item();
                    item.SetDefaults(catchItem);
                    baitPower = TerrariaItemReadCompat.BaitPower(item);
                }
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "auto-capture-critter-bait-power-read-failed",
                    TimeSpan.FromSeconds(30),
                    "AutoCaptureCritterCategoryCatalog",
                    "Failed to read catch item bait power for item " + catchItem + ": " + error.Message);
            }

            lock (BaitPowerSyncRoot)
            {
                if (!BaitPowerByCatchItem.ContainsKey(catchItem))
                {
                    BaitPowerByCatchItem[catchItem] = baitPower;
                }

                return BaitPowerByCatchItem[catchItem];
            }
        }
    }
}
