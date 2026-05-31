namespace JueMingZ.GameState.Tiles
{
    public sealed class TileSnapshot
    {
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
