namespace JueMingZ.Automation.BuffAndRecovery
{
    public sealed class BuffPotionCandidate
    {
        public string SourceContainer { get; set; }
        public int SourceSlot { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int Stack { get; set; }
        public int BuffType { get; set; }
        public string BuffName { get; set; }
        public int BuffTime { get; set; }
        public int EstimatedDurationSeconds { get; set; }
        public bool IsActive { get; set; }
        public bool IsWhitelisted { get; set; }
        public bool CanApply { get; set; }
        public string SkipReason { get; set; }
        public string ConflictGroup { get; set; }
        public string NetworkMode { get; set; }

        public BuffPotionCandidate()
        {
            SourceContainer = string.Empty;
            SourceSlot = -1;
            ItemName = string.Empty;
            BuffName = string.Empty;
            SkipReason = string.Empty;
            ConflictGroup = string.Empty;
            NetworkMode = string.Empty;
        }

        public BuffPotionCandidate Clone()
        {
            return new BuffPotionCandidate
            {
                SourceContainer = SourceContainer,
                SourceSlot = SourceSlot,
                ItemType = ItemType,
                ItemName = ItemName,
                Stack = Stack,
                BuffType = BuffType,
                BuffName = BuffName,
                BuffTime = BuffTime,
                EstimatedDurationSeconds = EstimatedDurationSeconds,
                IsActive = IsActive,
                IsWhitelisted = IsWhitelisted,
                CanApply = CanApply,
                SkipReason = SkipReason,
                ConflictGroup = ConflictGroup,
                NetworkMode = NetworkMode
            };
        }
    }
}
