using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Fishing
{
    internal sealed class FishingEquipmentProfile
    {
        public int ItemType { get; set; }
        public int Score { get; set; }
        public string EffectGroup { get; set; }
        public string Reason { get; set; }
        public bool IsAccessory { get; set; }
        public bool IsTackleBag { get; set; }
        public bool IsLavaproofTackleBag { get; set; }
        public bool CoveredByAnyTackleBag { get; set; }
        public bool CoveredByLavaproofTackleBag { get; set; }

        public FishingEquipmentProfile()
        {
            EffectGroup = string.Empty;
            Reason = string.Empty;
        }
    }

    internal static class FishingEquipmentCatalog
    {
        private static readonly int AnglerHat = ResolveItemId("AnglerHat", 2367);
        private static readonly int AnglerVest = ResolveItemId("AnglerVest", 2368);
        private static readonly int AnglerPants = ResolveItemId("AnglerPants", 2369);
        private static readonly int UpgradedFishingHead = ResolveItemId("UpgradedFishingHead", 5591);
        private static readonly int UpgradedFishingBody = ResolveItemId("UpgradedFishingBody", 5592);
        private static readonly int UpgradedFishingLegs = ResolveItemId("UpgradedFishingLegs", 5593);
        private static readonly int HighTestFishingLine = ResolveItemId("HighTestFishingLine", 2373);
        private static readonly int AnglerEarring = ResolveItemId("AnglerEarring", 2374);
        private static readonly int TackleBox = ResolveItemId("TackleBox", 2375);
        private static readonly int AnglerTackleBag = ResolveItemId("AnglerTackleBag", 3721);
        private static readonly int LavaFishingHook = ResolveItemId("LavaFishingHook", 4881);
        private static readonly int LavaproofTackleBag = ResolveItemId("LavaproofTackleBag", 5064);
        private static readonly int FishingBobber = ResolveItemId("FishingBobber", 5139);
        private static readonly int FishingBobberGlowingStar = ResolveItemId("FishingBobberGlowingStar", 5140);
        private static readonly int FishingBobberGlowingLava = ResolveItemId("FishingBobberGlowingLava", 5141);
        private static readonly int FishingBobberGlowingKrypton = ResolveItemId("FishingBobberGlowingKrypton", 5142);
        private static readonly int FishingBobberGlowingXenon = ResolveItemId("FishingBobberGlowingXenon", 5143);
        private static readonly int FishingBobberGlowingArgon = ResolveItemId("FishingBobberGlowingArgon", 5144);
        private static readonly int FishingBobberGlowingViolet = ResolveItemId("FishingBobberGlowingViolet", 5145);
        private static readonly int FishingBobberGlowingRainbow = ResolveItemId("FishingBobberGlowingRainbow", 5146);
        private static readonly int LuckyCoin = ResolveItemId("LuckyCoin", 855);
        private static readonly int CoinRing = ResolveItemId("CoinRing", 3034);
        private static readonly int GreedyRing = ResolveItemId("GreedyRing", 3035);
        private static readonly int LuckyHorseshoe = ResolveItemId("LuckyHorseshoe", 158);
        private static readonly int ObsidianHorseshoe = ResolveItemId("ObsidianHorseshoe", 396);
        private static readonly int BlueHorseshoeBalloon = ResolveItemId("BlueHorseshoeBalloon", 1250);
        private static readonly int WhiteHorseshoeBalloon = ResolveItemId("WhiteHorseshoeBalloon", 1251);
        private static readonly int YellowHorseshoeBalloon = ResolveItemId("YellowHorseshoeBalloon", 1252);
        private static readonly int BalloonHorseshoeFart = ResolveItemId("BalloonHorseshoeFart", 3250);
        private static readonly int BalloonHorseshoeHoney = ResolveItemId("BalloonHorseshoeHoney", 3251);
        private static readonly int BalloonHorseshoeSharkron = ResolveItemId("BalloonHorseshoeSharkron", 3252);
        private static readonly int HorseshoeBundle = ResolveItemId("HorseshoeBundle", 5331);

        private static readonly HashSet<int> ClothingItems = new HashSet<int>
        {
            AnglerHat,
            AnglerVest,
            AnglerPants,
            UpgradedFishingHead,
            UpgradedFishingBody,
            UpgradedFishingLegs
        };

        private static readonly HashSet<int> BobberItems = new HashSet<int>
        {
            FishingBobber,
            FishingBobberGlowingStar,
            FishingBobberGlowingLava,
            FishingBobberGlowingKrypton,
            FishingBobberGlowingXenon,
            FishingBobberGlowingArgon,
            FishingBobberGlowingViolet,
            FishingBobberGlowingRainbow
        };

        private static readonly Dictionary<int, int> LuckyCoinScores = new Dictionary<int, int>
        {
            { LuckyCoin, 3000 },
            { CoinRing, 3200 },
            { GreedyRing, 3400 }
        };

        private static readonly Dictionary<int, int> LuckyHorseshoeScores = new Dictionary<int, int>
        {
            { LuckyHorseshoe, 2500 },
            { ObsidianHorseshoe, 2600 },
            { BlueHorseshoeBalloon, 2700 },
            { WhiteHorseshoeBalloon, 2700 },
            { YellowHorseshoeBalloon, 2700 },
            { BalloonHorseshoeFart, 2800 },
            { BalloonHorseshoeHoney, 2800 },
            { BalloonHorseshoeSharkron, 2800 },
            { HorseshoeBundle, 2900 }
        };

        public static bool TryScoreItemForSlot(object player, object item, int slot, bool expertOrMasterMode, out int score, out string effectGroup, out string reason)
        {
            return TryScoreItemForSlot(player, item, slot, expertOrMasterMode, FishingLiquidKind.Unknown, out score, out effectGroup, out reason);
        }

        public static bool TryScoreItemForSlot(object player, object item, int slot, bool expertOrMasterMode, FishingLiquidKind liquidKind, out int score, out string effectGroup, out string reason)
        {
            FishingEquipmentProfile profile;
            if (TryBuildProfile(player, item, slot, expertOrMasterMode, liquidKind, out profile))
            {
                score = profile.Score;
                effectGroup = profile.EffectGroup;
                reason = profile.Reason;
                return true;
            }

            score = 0;
            effectGroup = string.Empty;
            reason = "notFishingEquipment";
            return false;
        }

        public static int ScoreEquippedSlot(object player, object item, int slot, bool expertOrMasterMode)
        {
            return ScoreEquippedSlot(player, item, slot, expertOrMasterMode, FishingLiquidKind.Unknown);
        }

        public static int ScoreEquippedSlot(object player, object item, int slot, bool expertOrMasterMode, FishingLiquidKind liquidKind)
        {
            FishingEquipmentProfile profile;
            return TryBuildProfile(player, item, slot, expertOrMasterMode, liquidKind, out profile)
                ? profile.Score
                : 0;
        }

        public static bool TryBuildProfile(object player, object item, int slot, bool expertOrMasterMode, FishingLiquidKind liquidKind, out FishingEquipmentProfile profile)
        {
            profile = null;
            if (!IsUsableItemForSlot(player, item, slot, expertOrMasterMode))
            {
                return false;
            }

            int itemType;
            FishingLoadoutCompat.TryReadItemInt(item, "type", out itemType);
            if (ClothingItems.Contains(itemType))
            {
                profile = new FishingEquipmentProfile
                {
                    ItemType = itemType,
                    Score = IsUpgradedFishingClothing(itemType) ? 70 : 50,
                    EffectGroup = "fishingClothing:" + slot.ToString(CultureInfo.InvariantCulture),
                    Reason = IsUpgradedFishingClothing(itemType) ? "upgradedFishingClothing" : "fishingClothing"
                };
                return true;
            }

            if (slot < 3)
            {
                return false;
            }

            if (itemType == LavaproofTackleBag)
            {
                profile = AccessoryProfile(itemType, 10000, "fishing.lavaproofTackleBag", "lavaproofTackleBag");
                profile.IsTackleBag = true;
                profile.IsLavaproofTackleBag = true;
                return true;
            }

            if (itemType == AnglerTackleBag)
            {
                profile = AccessoryProfile(itemType, 9000, "fishing.anglerTackleBag", "anglerTackleBag");
                profile.IsTackleBag = true;
                return true;
            }

            if (itemType == AnglerEarring)
            {
                profile = AccessoryProfile(itemType, 8000, "fishing.anglerEarring", "anglerEarring");
                return true;
            }

            if (BobberItems.Contains(itemType))
            {
                profile = AccessoryProfile(itemType, 7000 + BobberRank(itemType), "fishing.bobber", "fishingBobber");
                return true;
            }

            if (itemType == HighTestFishingLine)
            {
                profile = AccessoryProfile(itemType, 5200, "fishing.highTestLine", "highTestFishingLine");
                profile.CoveredByAnyTackleBag = true;
                return true;
            }

            if (itemType == TackleBox)
            {
                profile = AccessoryProfile(itemType, 5100, "fishing.tackleBox", "tackleBox");
                profile.CoveredByAnyTackleBag = true;
                return true;
            }

            if (itemType == LavaFishingHook)
            {
                if (liquidKind != FishingLiquidKind.Lava)
                {
                    return false;
                }

                profile = AccessoryProfile(itemType, 5000, "fishing.lavaHook", "lavaFishingHook");
                profile.CoveredByLavaproofTackleBag = true;
                return true;
            }

            int score;
            if (LuckyCoinScores.TryGetValue(itemType, out score))
            {
                profile = AccessoryProfile(itemType, score, "fishing.luckyCoin", "luckyCoinLine");
                return true;
            }

            if (LuckyHorseshoeScores.TryGetValue(itemType, out score))
            {
                profile = AccessoryProfile(itemType, score, "fishing.luckyHorseshoe", "luckyHorseshoeLine");
                return true;
            }

            return false;
        }

        private static FishingEquipmentProfile AccessoryProfile(int itemType, int score, string effectGroup, string reason)
        {
            return new FishingEquipmentProfile
            {
                ItemType = itemType,
                Score = score,
                EffectGroup = effectGroup ?? string.Empty,
                Reason = reason ?? string.Empty,
                IsAccessory = true
            };
        }

        private static bool IsUpgradedFishingClothing(int itemType)
        {
            return itemType == UpgradedFishingHead ||
                   itemType == UpgradedFishingBody ||
                   itemType == UpgradedFishingLegs;
        }

        private static int BobberRank(int itemType)
        {
            if (itemType == FishingBobberGlowingRainbow)
            {
                return 7;
            }

            if (itemType == FishingBobberGlowingViolet)
            {
                return 6;
            }

            if (itemType == FishingBobberGlowingArgon)
            {
                return 5;
            }

            if (itemType == FishingBobberGlowingXenon)
            {
                return 4;
            }

            if (itemType == FishingBobberGlowingKrypton)
            {
                return 3;
            }

            if (itemType == FishingBobberGlowingLava)
            {
                return 2;
            }

            return itemType == FishingBobberGlowingStar ? 1 : 0;
        }

        private static bool IsUsableItemForSlot(object player, object item, int slot, bool expertOrMasterMode)
        {
            bool isAir;
            if (item == null ||
                !FishingLoadoutCompat.TryIsItemAir(item, out isAir) ||
                isAir)
            {
                return false;
            }

            int stack;
            FishingLoadoutCompat.TryReadItemInt(item, "stack", out stack);
            if (stack <= 0)
            {
                return false;
            }

            bool usable;
            if (!FishingLoadoutCompat.TryIsItemSlotUnlockedAndUsable(player, slot, out usable) || !usable)
            {
                return false;
            }

            bool expertOnly;
            if (FishingLoadoutCompat.TryReadItemBool(item, "expertOnly", out expertOnly) && expertOnly && !expertOrMasterMode)
            {
                return false;
            }

            if (slot == 0)
            {
                int headSlot;
                return FishingLoadoutCompat.TryReadItemInt(item, "headSlot", out headSlot) && headSlot > -1;
            }

            if (slot == 1)
            {
                int bodySlot;
                return FishingLoadoutCompat.TryReadItemInt(item, "bodySlot", out bodySlot) && bodySlot > -1;
            }

            if (slot == 2)
            {
                int legSlot;
                return FishingLoadoutCompat.TryReadItemInt(item, "legSlot", out legSlot) && legSlot > -1;
            }

            bool accessory;
            return slot >= 3 &&
                   slot <= 9 &&
                   FishingLoadoutCompat.TryReadItemBool(item, "accessory", out accessory) &&
                   accessory;
        }

        private static int ResolveItemId(string memberName, int fallback)
        {
            var itemIdType = FindType("Terraria.ID.ItemID");
            if (itemIdType == null)
            {
                return fallback;
            }

            var field = itemIdType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(field.GetValue(null), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

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
