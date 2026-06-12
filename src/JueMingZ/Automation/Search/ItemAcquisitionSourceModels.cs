namespace JueMingZ.Automation.Search
{
    internal static class ItemAcquisitionSourceTypes
    {
        public const string NpcDrop = "npcDrop";
        public const string NpcShop = "npcShop";
        public const string MiningGatheringTag = "miningGatheringTag";
    }

    internal sealed class ItemAcquisitionSourceSummary
    {
        public string SourceType { get; set; }

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
