namespace JueMingZ.UI.Legacy
{
    public sealed class LegacyMouseSnapshot
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool LeftDown { get; set; }
        public bool LeftPressed { get; set; }
        public bool LeftReleased { get; set; }
        public int ScrollDelta { get; set; }
        public bool ReadAvailable { get; set; }
        public string ReadMode { get; set; }
        public bool WindowHit { get; set; }

        public LegacyMouseSnapshot()
        {
            X = -1;
            Y = -1;
            ReadMode = "none";
        }
    }
}
