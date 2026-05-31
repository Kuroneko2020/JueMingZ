namespace JueMingZ.GameState.World
{
    public sealed class WorldStateSnapshot
    {
        public bool WorldAvailable { get; set; }
        public string WorldName { get; set; }

        public WorldStateSnapshot()
        {
            WorldName = string.Empty;
        }
    }
}
