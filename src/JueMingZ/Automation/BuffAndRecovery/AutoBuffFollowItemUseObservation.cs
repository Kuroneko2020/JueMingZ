namespace JueMingZ.Automation.BuffAndRecovery
{
    public sealed class AutoBuffFollowItemUseObservation
    {
        public int SelectedSlot { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int StackBefore { get; set; }
        public int BuffType { get; set; }
        public string BuffName { get; set; }
        public int BuffTime { get; set; }
        public int ActiveBuffTimeBefore { get; set; }

        public AutoBuffFollowItemUseObservation()
        {
            SelectedSlot = -1;
            ItemName = string.Empty;
            BuffName = string.Empty;
        }
    }
}
