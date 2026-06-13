namespace JueMingZ.Automation.Search
{
    internal static partial class ItemFishingSourceIndex
    {
        private static SourceDefinition[] CreateAnglerRewardDefinitions()
        {
            return new[]
            {
                AnglerReward("FuzzyCarrot", 2428, "固定任务奖励", "渔夫", "第 5 次任务奖励", "完成第 5 次渔夫任务固定奖励"),
                AnglerReward("AnglerHat", 2367, "固定任务奖励", "渔夫", "第 10 次任务奖励", "完成第 10 次渔夫任务固定奖励"),
                AnglerReward("AnglerVest", 2368, "固定任务奖励", "渔夫", "第 15 次任务奖励", "完成第 15 次渔夫任务固定奖励"),
                AnglerReward("AnglerPants", 2369, "固定任务奖励", "渔夫", "第 20 次任务奖励", "完成第 20 次渔夫任务固定奖励"),
                AnglerReward("BottomlessBucket", 3031, "固定任务奖励", "渔夫", "第 25 次任务奖励", "完成第 25 次渔夫任务固定奖励；后续也可能随机奖励"),
                AnglerReward("GoldenFishingRod", 2294, "固定任务奖励", "渔夫", "第 30 次任务奖励", "完成第 30 次渔夫任务固定奖励；75 次后也可能随机奖励"),

                AnglerReward("HighTestFishingLine", 2373, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机饰品候选；不预测本次奖励"),
                AnglerReward("AnglerEarring", 2374, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机饰品候选；不预测本次奖励"),
                AnglerReward("TackleBox", 2375, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机饰品候选；不预测本次奖励"),
                AnglerReward("FishHook", 2360, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机奖励候选；不预测本次奖励"),
                AnglerReward("FishermansGuide", 3120, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机信息饰品候选；不预测本次奖励"),
                AnglerReward("WeatherRadio", 3037, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机信息饰品候选；不预测本次奖励"),
                AnglerReward("Sextant", 3096, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机信息饰品候选；不预测本次奖励"),
                AnglerReward("FishingBobber", 5139, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机浮标候选；不预测本次奖励"),
                AnglerReward("GoldenBugNet", 3183, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机工具候选；不预测本次奖励"),
                AnglerReward("BottomlessHoneyBucket", 5302, "随机任务奖励", "渔夫", "随机奖励候选", "蜂蜜任务鱼相关随机奖励候选；不预测本次奖励"),
                AnglerReward("HoneyAbsorbantSponge", 5303, "随机任务奖励", "渔夫", "随机奖励候选", "蜂蜜任务鱼相关随机奖励候选；不预测本次奖励"),
                AnglerReward("HotlineFishingHook", 2422, "随机任务奖励", "渔夫", "困难模式后随机候选", "困难模式且任务次数满足后可能奖励；不预测本次奖励"),
                AnglerReward("FinWings", 2494, "随机任务奖励", "渔夫", "困难模式后随机候选", "困难模式且任务次数满足后可能奖励；不预测本次奖励"),
                AnglerReward("SuperAbsorbantSponge", 3032, "随机任务奖励", "渔夫", "任务次数满足后随机候选", "任务次数满足后可能奖励；不预测本次奖励"),
                AnglerReward("FishMinecart", 4067, "随机任务奖励", "渔夫", "随机奖励候选", "渔夫任务随机奖励候选；不预测本次奖励"),

                AnglerReward("FishingPotion", 2354, "随机任务奖励", "渔夫", "药水奖励候选", "未获得主奖励时可能给药水；不预测本次奖励"),
                AnglerReward("SonarPotion", 2355, "随机任务奖励", "渔夫", "药水奖励候选", "未获得主奖励时可能给药水；不预测本次奖励"),
                AnglerReward("CratePotion", 2356, "随机任务奖励", "渔夫", "药水奖励候选", "未获得主奖励时可能给药水；不预测本次奖励"),
                AnglerReward("ApprenticeBait", 2674, "鱼饵奖励", "渔夫", "鱼饵候选", "渔夫任务鱼饵奖励候选；不预测数量"),
                AnglerReward("JourneymanBait", 2675, "鱼饵奖励", "渔夫", "鱼饵候选", "渔夫任务鱼饵奖励候选；不预测数量"),
                AnglerReward("MasterBait", 2676, "鱼饵奖励", "渔夫", "鱼饵候选", "渔夫任务鱼饵奖励候选；不预测数量"),

                AnglerReward("SeashellHairpin", 2417, "套装随机奖励", "渔夫", "随机奖励候选", "随机给出时会附带美人鱼套装其余部件；不预测本次奖励"),
                AnglerReward("MermaidAdornment", 2418, "套装随机奖励", "渔夫", "随套装奖励附带", "与贝壳发夹同组附带；不预测本次奖励"),
                AnglerReward("MermaidTail", 2419, "套装随机奖励", "渔夫", "随套装奖励附带", "与贝壳发夹同组附带；不预测本次奖励"),
                AnglerReward("FishCostumeMask", 2498, "套装随机奖励", "渔夫", "随机奖励候选", "随机给出时会附带鱼装套装其余部件；不预测本次奖励"),
                AnglerReward("FishCostumeShirt", 2499, "套装随机奖励", "渔夫", "随套装奖励附带", "与鱼装面具同组附带；不预测本次奖励"),
                AnglerReward("FishCostumeFinskirt", 2500, "套装随机奖励", "渔夫", "随套装奖励附带", "与鱼装面具同组附带；不预测本次奖励"),

                AnglerReward("LifePreserver", 2442, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("ShipsWheel", 2443, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("CompassRose", 2444, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("WallAnchor", 2445, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("GoldfishTrophy", 2446, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("BunnyfishTrophy", 2447, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("SwordfishTrophy", 2448, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("SharkteethTrophy", 2449, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("ShipInABottle", 2490, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("TreasureMap", 2495, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励"),
                AnglerReward("SeaweedPlanter", 2496, "装饰随机奖励", "渔夫", "装饰奖励候选", "渔夫任务装饰品随机候选；不预测本次奖励")
            };
        }
    }
}
