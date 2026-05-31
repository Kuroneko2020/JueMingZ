namespace JueMingZ.Automation.WorldAutomation
{
    public sealed class TravelMenuContext
    {
        public string PlayerPath { get; set; }
        public string WorldPath { get; set; }
        public string PlayerName { get; set; }
        public string WorldName { get; set; }
        public int PlayerDifficulty { get; set; }
        public int WorldGameMode { get; set; }
        public int MainGameMode { get; set; }
        public int NetMode { get; set; }

        public TravelMenuContext()
        {
            PlayerPath = string.Empty;
            WorldPath = string.Empty;
            PlayerName = string.Empty;
            WorldName = string.Empty;
        }

        public TravelMenuContext Clone()
        {
            return new TravelMenuContext
            {
                PlayerPath = PlayerPath,
                WorldPath = WorldPath,
                PlayerName = PlayerName,
                WorldName = WorldName,
                PlayerDifficulty = PlayerDifficulty,
                WorldGameMode = WorldGameMode,
                MainGameMode = MainGameMode,
                NetMode = NetMode
            };
        }

        public static TravelMenuContext FromMarker(TravelMenuContext current, TravelMenuRestoreMarker marker)
        {
            var context = current == null ? new TravelMenuContext() : current.Clone();
            if (marker == null)
            {
                return context;
            }

            context.PlayerPath = marker.PlayerPath ?? string.Empty;
            context.WorldPath = marker.WorldPath ?? string.Empty;
            context.PlayerName = marker.PlayerName ?? string.Empty;
            context.WorldName = marker.WorldName ?? string.Empty;
            context.PlayerDifficulty = marker.OriginalPlayerDifficulty;
            context.WorldGameMode = marker.OriginalWorldGameMode;
            context.MainGameMode = marker.OriginalMainGameMode;
            return context;
        }
    }
}
