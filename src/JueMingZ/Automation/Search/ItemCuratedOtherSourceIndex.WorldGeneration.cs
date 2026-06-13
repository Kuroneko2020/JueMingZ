namespace JueMingZ.Automation.Search
{
    internal static partial class ItemCuratedOtherSourceIndex
    {
        private static SourceDefinition[] CreateWorldGenerationDefinitions()
        {
            return new[]
            {
                World("LifeCrystal", 29, "世界生成", "生命水晶", "自然生成", "地下/洞穴层自然生成，可被采集；不扫描当前世界"),
                World("EnchantedSword", 989, "世界生成", "附魔剑剑冢", "自然生成候选", "剑冢或相关世界生成代表物；不解析完整生成条件"),

                World("Musket", 96, "特殊入口", "暗影珠", "破坏入口候选", "腐化世界暗影珠破坏来源；不根据当前世界邪恶隐藏"),
                World("Vilethorn", 64, "特殊入口", "暗影珠", "破坏入口候选", "腐化世界暗影珠武器候选；不预测当前世界"),
                World("BallOHurt", 162, "特殊入口", "暗影珠", "破坏入口候选", "腐化世界暗影珠武器候选；不预测当前世界"),
                World("BandofStarpower", 111, "特殊入口", "暗影珠", "破坏入口候选", "腐化世界暗影珠饰品候选；不预测当前世界"),

                World("TheUndertaker", 800, "特殊入口", "猩红之心", "破坏入口候选", "猩红世界猩红之心破坏来源；不根据当前世界邪恶隐藏"),
                World("TheRottedFork", 802, "特殊入口", "猩红之心", "破坏入口候选", "猩红世界猩红之心武器候选；不预测当前世界"),
                World("CrimsonRod", 1256, "特殊入口", "猩红之心", "破坏入口候选", "猩红世界猩红之心武器候选；不预测当前世界"),
                World("PanicNecklace", 1290, "特殊入口", "猩红之心", "破坏入口候选", "猩红世界猩红之心饰品候选；不预测当前世界"),

                World("CreativeWings", 4978, "待确认特殊来源", "空岛宝箱 / 特殊世界生成", "待确认候选", "空岛房屋宝箱低概率世界生成证据；待确认正常生存来源口径")
            };
        }
    }
}
