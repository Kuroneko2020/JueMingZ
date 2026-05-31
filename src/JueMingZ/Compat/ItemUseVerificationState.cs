namespace JueMingZ.Compat
{
    public sealed class ItemUseVerificationState
    {
        public bool PlayerActive { get; set; }
        public bool PlayerDead { get; set; }
        public bool PlayerGhost { get; set; }
        public int SelectedSlot { get; set; }
        public int ItemType { get; set; }
        public int ItemStack { get; set; }
        public string ItemName { get; set; }
        public int ItemAnimation { get; set; }
        public int ItemTime { get; set; }
        public int ReuseDelay { get; set; }
        public int Life { get; set; }
        public int LifeMax { get; set; }
        public int Mana { get; set; }
        public int ManaMax { get; set; }
        public int ActiveBuffCount { get; set; }
        public int BuffTimeTotal { get; set; }
        public string BuffTypesJson { get; set; }
        public int UseStyle { get; set; }
        public bool Consumable { get; set; }
        public int HealLife { get; set; }
        public int HealMana { get; set; }
        public int BuffType { get; set; }
        public int BuffTime { get; set; }
        public int CreateTile { get; set; }
        public int CreateWall { get; set; }

        public ItemUseVerificationState()
        {
            SelectedSlot = -1;
            ItemName = string.Empty;
            BuffTypesJson = "[]";
            CreateTile = -1;
            CreateWall = -1;
        }
    }
}
