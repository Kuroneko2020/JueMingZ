namespace JueMingZ.GameState.Player
{
    public sealed class PlayerStateSnapshot
    {
        // Player snapshots separate observation from mutation; services must
        // not write life, mana, velocity, selection, or position from here.
        public bool Exists { get; set; }
        public bool Active { get; set; }
        public bool Dead { get; set; }
        public bool Ghost { get; set; }
        public int Life { get; set; }
        public int LifeMax { get; set; }
        public int Mana { get; set; }
        public int ManaMax { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public int SelectedItem { get; set; }
        public int Direction { get; set; }
        public bool Wet { get; set; }
        public bool LavaWet { get; set; }
        public bool HoneyWet { get; set; }
        public bool IsUsingItem { get; set; }
    }
}
