namespace JueMingZ.Automation.Search
{
    internal static partial class ItemContainerOpenSourceIndex
    {
        private static ContainerDefinition[] CreateGenericOpenContainerDefinitions()
        {
            return new[]
            {
                OpenContainer("HerbBag", 3093, "草药袋",
                    Output("Daybloom", 313, "可能开出"),
                    Output("Moonglow", 314, "可能开出"),
                    Output("Blinkroot", 315, "可能开出"),
                    Output("Deathweed", 316, "可能开出"),
                    Output("Waterleaf", 317, "可能开出"),
                    Output("Fireblossom", 318, "可能开出"),
                    Output("Shiverthorn", 2358, "可能开出"),
                    Output("DaybloomSeeds", 307, "可能开出"),
                    Output("MoonglowSeeds", 308, "可能开出"),
                    Output("BlinkrootSeeds", 309, "可能开出"),
                    Output("DeathweedSeeds", 310, "可能开出"),
                    Output("WaterleafSeeds", 311, "可能开出"),
                    Output("FireblossomSeeds", 312, "可能开出"),
                    Output("ShiverthornSeeds", 2357, "可能开出"))
            };
        }
    }
}
