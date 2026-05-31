namespace JueMingZ.Automation.BuffAndRecovery
{
    public sealed class RecoveryPotionCandidate
    {
        public const int RestorationPotionItemType = 227;
        public const int StrangeBrewItemType = 3001;

        public string SourceContainer { get; set; }
        public int SourceSlot { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int Stack { get; set; }
        public int HealLife { get; set; }
        public int HealMana { get; set; }
        public bool Potion { get; set; }
        public bool Consumable { get; set; }
        public int BuffType { get; set; }
        public int BuffTime { get; set; }

        public int EffectiveHealMax
        {
            get
            {
                if (ItemType == StrangeBrewItemType)
                {
                    return HealLife > 120 ? HealLife : 120;
                }

                return HealLife;
            }
        }

        public int SmartHealTriggerAmount
        {
            get
            {
                if (ItemType == StrangeBrewItemType)
                {
                    return EffectiveHealMax;
                }

                return HealLife;
            }
        }

        public RecoveryPotionCandidate()
        {
            SourceContainer = string.Empty;
            SourceSlot = -1;
            ItemName = string.Empty;
        }
    }
}
