namespace JueMingZ.GameState.Inventory
{
    public sealed class InventoryItemSnapshot
    {
        // Item snapshots preserve observable inventory facts; they are not
        // handles for stack, prefix, or favorite mutation.
        public int SlotIndex { get; set; }
        public int Type { get; set; }
        public int Stack { get; set; }
        public int MaxStack { get; set; }
        public int Prefix { get; set; }
        public bool Favorited { get; set; }
        public string Name { get; set; }
        public int UseStyle { get; set; }
        public bool Consumable { get; set; }
        public int HealLife { get; set; }
        public int HealMana { get; set; }
        public int BuffType { get; set; }
        public int BuffTime { get; set; }
        public int CreateTile { get; set; }
        public int CreateWall { get; set; }
        public int CatchTool { get; set; }
        public bool Accessory { get; set; }
        public int WingSlot { get; set; }
        public int Defense { get; set; }
        public int Pick { get; set; }
        public int Axe { get; set; }
        public int Hammer { get; set; }

        public InventoryItemSnapshot()
        {
            Name = string.Empty;
            CreateTile = -1;
            CreateWall = -1;
            WingSlot = -1;
        }
    }
}
