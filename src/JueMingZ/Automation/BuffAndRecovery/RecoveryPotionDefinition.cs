namespace JueMingZ.Automation.BuffAndRecovery
{
    public sealed class RecoveryPotionDefinition
    {
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int HealLife { get; set; }
        public int HealMana { get; set; }
        public bool Potion { get; set; }
        public bool Consumable { get; set; }
        public int BuffType { get; set; }
        public int BuffTime { get; set; }

        public RecoveryPotionDefinition()
        {
            ItemName = string.Empty;
        }
    }
}
