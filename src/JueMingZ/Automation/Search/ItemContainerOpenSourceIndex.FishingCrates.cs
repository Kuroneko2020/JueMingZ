namespace JueMingZ.Automation.Search
{
    internal static partial class ItemContainerOpenSourceIndex
    {
        private static ContainerDefinition[] CreateFishingCrateDefinitions()
        {
            return new[]
            {
                FishingCrate("WoodenCrate", 2334, "木匣",
                    Output("Extractinator", 997, "可能开出"),
                    Output("Aglet", 285, "可能开出"),
                    Output("ShoeSpikes", 975, "可能开出"),
                    Output("TsunamiInABottle", 3201, "可能开出"),
                    Output("HerbBag", 3093, "可能开出"),
                    Output("GoldCoin", 73, "可能开出"),
                    Output("PlatinumCoin", 74, "困难模式可能开出")),

                FishingCrate("WoodenCrateHard", 3979, "珍珠木匣",
                    Output("Extractinator", 997, "可能开出"),
                    Output("Aglet", 285, "可能开出"),
                    Output("ShoeSpikes", 975, "可能开出"),
                    Output("TsunamiInABottle", 3201, "可能开出"),
                    Output("HerbBag", 3093, "可能开出"),
                    Output("GoldCoin", 73, "可能开出"),
                    Output("PlatinumCoin", 74, "可能开出")),

                FishingCrate("IronCrate", 2335, "铁匣",
                    Output("EnchantedSword", 989, "可能开出"),
                    Output("HerbBag", 3093, "可能开出"),
                    Output("GoldCoin", 73, "可能开出")),

                FishingCrate("IronCrateHard", 3980, "秘银匣",
                    Output("EnchantedSword", 989, "可能开出"),
                    Output("HerbBag", 3093, "可能开出"),
                    Output("GoldCoin", 73, "可能开出"),
                    Output("PlatinumCoin", 74, "可能开出")),

                FishingCrate("GoldenCrate", 2336, "金匣",
                    Output("LifeCrystal", 29, "可能开出"),
                    Output("EnchantedSword", 989, "可能开出"),
                    Output("GoldCoin", 73, "可能开出")),

                FishingCrate("GoldenCrateHard", 3981, "钛金匣",
                    Output("LifeCrystal", 29, "可能开出"),
                    Output("EnchantedSword", 989, "可能开出"),
                    Output("GoldCoin", 73, "可能开出"),
                    Output("PlatinumCoin", 74, "可能开出")),

                FishingCrate("OasisCrate", 4407, "绿洲匣",
                    Output("SandstorminaBottle", 857, "可能开出"),
                    Output("FlyingCarpet", 934, "可能开出"),
                    Output("PharaohsMask", 848, "可能开出"),
                    Output("GoldCoin", 73, "可能开出")),

                FishingCrate("OasisCrateHard", 4408, "幻象匣",
                    Output("SandstorminaBottle", 857, "可能开出"),
                    Output("FlyingCarpet", 934, "可能开出"),
                    Output("PharaohsMask", 848, "可能开出"),
                    Output("GoldCoin", 73, "可能开出"),
                    Output("PlatinumCoin", 74, "可能开出")),

                FishingCrate("FloatingIslandFishingCrate", 3206, "天空匣",
                    Output("CreativeWings", 4978, "可能开出"),
                    Output("Starfury", 65, "可能开出"),
                    Output("LuckyHorseshoe", 158, "可能开出"),
                    Output("GoldCoin", 73, "可能开出")),

                FishingCrate("FloatingIslandFishingCrateHard", 3985, "天蓝匣",
                    Output("CreativeWings", 4978, "可能开出"),
                    Output("Starfury", 65, "可能开出"),
                    Output("LuckyHorseshoe", 158, "可能开出"),
                    Output("GoldCoin", 73, "可能开出"),
                    Output("PlatinumCoin", 74, "可能开出")),

                FishingCrate("DungeonFishingCrate", 3205, "地牢匣",
                    Output("Muramasa", 155, "可能开出"),
                    Output("CobaltShield", 156, "可能开出"),
                    Output("AquaScepter", 157, "可能开出"),
                    Output("MagicMissile", 113, "可能开出"),
                    Output("Valor", 328, "可能开出")),

                FishingCrate("DungeonFishingCrateHard", 3984, "围栏匣",
                    Output("Muramasa", 155, "可能开出"),
                    Output("CobaltShield", 156, "可能开出"),
                    Output("AquaScepter", 157, "可能开出"),
                    Output("MagicMissile", 113, "可能开出"),
                    Output("Valor", 328, "可能开出")),

                FishingCrate("LavaCrate", 4877, "黑曜石匣",
                    Output("LavaCharm", 906, "可能开出"),
                    Output("Hellstone", 174, "可能开出"),
                    Output("Obsidian", 543, "可能开出")),

                FishingCrate("LavaCrateHard", 4878, "狱石匣",
                    Output("LavaCharm", 906, "可能开出"),
                    Output("Hellstone", 174, "可能开出"),
                    Output("Obsidian", 543, "可能开出"))
            };
        }
    }
}
