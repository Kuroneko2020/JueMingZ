namespace JueMingZ.Automation.Search
{
    internal static class ItemAcquisitionSourceTypes
    {
        public const string NpcDrop = "npcDrop";
        public const string NpcShop = "npcShop";
        public const string Other = "otherSource";
    }

    internal static class ItemAcquisitionSourceTags
    {
        public const string MiningGathering = "采集";
        public const string TreasureBag = "宝藏袋";
        public const string ContainerOpen = "开包";
        public const string Fishing = "钓鱼";
        public const string AnglerReward = "渔夫奖励";
        public const string Chest = "宝箱";
        public const string WorldGeneration = "世界生成";
        public const string Extractinator = "提炼机";
    }

    internal sealed class ItemAcquisitionSourceSummary
    {
        public string SourceType { get; set; }

        public string SourceTag { get; set; }

        public string Title { get; set; }

        public string SourceName { get; set; }

        public string QuantityText { get; set; }

        public string ProbabilityText { get; set; }

        public string ConditionText { get; set; }

        public string ContextText { get; set; }

        public int ItemType { get; set; }

        public int NpcNetId { get; set; }

        public int RelatedItemType { get; set; }

        public ItemAcquisitionSourceSummary()
        {
            SourceType = string.Empty;
            SourceTag = string.Empty;
            Title = string.Empty;
            SourceName = string.Empty;
            QuantityText = string.Empty;
            ProbabilityText = string.Empty;
            ConditionText = string.Empty;
            ContextText = string.Empty;
            NpcNetId = -1;
            RelatedItemType = -1;
        }
    }
}
