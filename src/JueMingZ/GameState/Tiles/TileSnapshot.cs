namespace JueMingZ.GameState.Tiles
{
    public sealed class TileSnapshot
    {
        // Tile snapshots are intentionally tiny read models; absent samples
        // should block automation rather than invent terrain.
        public int CenterTileX { get; set; }
        public int CenterTileY { get; set; }
        public int SampleRadius { get; set; }
        public int SampledTileCount { get; set; }
        public string Status { get; set; }

        public TileSnapshot()
        {
            Status = "Unavailable";
        }
    }
}
