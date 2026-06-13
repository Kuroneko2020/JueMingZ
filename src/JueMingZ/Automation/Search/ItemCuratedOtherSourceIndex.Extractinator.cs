namespace JueMingZ.Automation.Search
{
    internal static partial class ItemCuratedOtherSourceIndex
    {
        private static SourceDefinition[] CreateExtractinatorDefinitions()
        {
            return new[]
            {
                // Machine blocks such as Extractinator #997 stay in chest sources; this table is only input/output clues.
                ExtractinatorInput("SiltBlock", 424, "泥沙块", "可作为普通提炼机输入材料；材料本身仍需采集，不承诺当前材料可提炼"),
                ExtractinatorInput("SlushBlock", 1103, "雪泥块", "可作为普通提炼机输入材料；材料本身仍需采集，不承诺当前材料可提炼"),
                ExtractinatorInput("DesertFossil", 3347, "沙漠化石块", "可作为普通提炼机输入材料；地下沙漠是否存在不由查询判断"),

                Extractinator("CopperOre", 12, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下矿脉采集"),
                Extractinator("TinOre", 699, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下矿脉采集"),
                Extractinator("IronOre", 11, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下矿脉采集"),
                Extractinator("LeadOre", 700, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下矿脉采集"),
                Extractinator("SilverOre", 14, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下矿脉采集"),
                Extractinator("TungstenOre", 701, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下矿脉采集"),
                Extractinator("GoldOre", 13, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下矿脉采集"),
                Extractinator("PlatinumOre", 702, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下矿脉采集"),

                Extractinator("Amethyst", 181, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下宝石采集"),
                Extractinator("Topaz", 180, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下宝石采集"),
                Extractinator("Sapphire", 177, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下宝石采集"),
                Extractinator("Emerald", 179, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下宝石采集"),
                Extractinator("Ruby", 178, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下宝石采集"),
                Extractinator("Diamond", 182, "常见提炼", "泥沙/雪泥", "提炼产物候选", "可由提炼机处理泥沙或雪泥获得；也可能来自地下宝石采集"),
                Extractinator("Amber", 999, "常见提炼", "泥沙/雪泥/沙漠化石", "提炼产物候选", "可由提炼机处理泥沙、雪泥或沙漠化石获得；也可能来自地下沙漠相关采集"),
                Extractinator("FossilOre", 3380, "沙漠化石提炼", "沙漠化石块", "提炼产物候选", "可由提炼机处理沙漠化石获得；不承诺当前世界有沙漠化石")
            };
        }
    }
}
