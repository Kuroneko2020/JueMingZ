namespace JueMingZ.GameState.Buffs
{
    public sealed class BuffSnapshot
    {
        public int BuffType { get; set; }
        public int BuffTime { get; set; }
        public string BuffName { get; set; }

        public BuffSnapshot()
        {
            BuffName = string.Empty;
        }
    }
}
