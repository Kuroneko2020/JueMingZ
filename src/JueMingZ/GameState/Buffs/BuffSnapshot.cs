namespace JueMingZ.GameState.Buffs
{
    public sealed class BuffSnapshot
    {
        // Buff entries are observations only; duration or type changes must
        // use the controlled buff mutation paths.
        public int BuffType { get; set; }
        public int BuffTime { get; set; }
        public string BuffName { get; set; }

        public BuffSnapshot()
        {
            BuffName = string.Empty;
        }
    }
}
