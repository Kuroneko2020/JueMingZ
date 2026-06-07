namespace JueMingZ.GameState.World
{
    public sealed class WorldStateSnapshot
    {
        // World snapshots carry display/context data only; unavailable identity
        // must remain sparse instead of being guessed.
        public bool WorldAvailable { get; set; }
        public string WorldName { get; set; }

        public WorldStateSnapshot()
        {
            WorldName = string.Empty;
        }
    }
}
