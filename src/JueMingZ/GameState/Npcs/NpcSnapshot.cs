namespace JueMingZ.GameState.Npcs
{
    public sealed class NpcSnapshot
    {
        public int WhoAmI { get; set; }
        public int Type { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public bool TownNpc { get; set; }
        public bool Friendly { get; set; }
        public bool Hostile { get; set; }
        public bool Critter { get; set; }
        public int CatchItem { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public NpcSnapshot()
        {
            Name = string.Empty;
        }
    }
}
