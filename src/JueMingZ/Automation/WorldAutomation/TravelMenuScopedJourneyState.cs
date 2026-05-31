namespace JueMingZ.Automation.WorldAutomation
{
    public sealed class TravelMenuScopedJourneyState
    {
        public string Scope { get; set; }
        public bool Applied { get; set; }
        public string Message { get; set; }
        public object WorldFile { get; set; }
        public object Player { get; set; }
        public object FilePlayer { get; set; }
        public object LocalPlayer { get; set; }
        public bool HasMainGameMode { get; set; }
        public bool HasWorldGameMode { get; set; }
        public bool HasPlayerDifficulty { get; set; }
        public bool HasFilePlayerDifficulty { get; set; }
        public bool HasLocalPlayerDifficulty { get; set; }
        public int MainGameMode { get; set; }
        public int WorldGameMode { get; set; }
        public int PlayerDifficulty { get; set; }
        public int FilePlayerDifficulty { get; set; }
        public int LocalPlayerDifficulty { get; set; }
        public bool InputBypassApplied { get; set; }
        public bool HasPlayerMouseInterface { get; set; }
        public bool PlayerMouseInterface { get; set; }
        public bool HasMainMouseInterface { get; set; }
        public bool MainMouseInterface { get; set; }
        public bool HasMainBlockMouse { get; set; }
        public bool MainBlockMouse { get; set; }
        public bool HasMainMouseLeftRelease { get; set; }
        public bool MainMouseLeftRelease { get; set; }
        public bool MainMouseLeftReleaseRestore { get; set; }
        public bool HasMainMouseRightRelease { get; set; }
        public bool MainMouseRightRelease { get; set; }
        public bool MainMouseRightReleaseRestore { get; set; }
        public bool HasMainMouseLeft { get; set; }
        public bool MainMouseLeft { get; set; }
        public bool MainMouseLeftRestore { get; set; }
        public bool HasMainMouseRight { get; set; }
        public bool MainMouseRight { get; set; }
        public bool MainMouseRightRestore { get; set; }
        public bool HasPlayerInputCurrentMouseLeft { get; set; }
        public bool PlayerInputCurrentMouseLeft { get; set; }
        public bool PlayerInputCurrentMouseLeftRestore { get; set; }
        public bool HasPlayerInputCurrentMouseRight { get; set; }
        public bool PlayerInputCurrentMouseRight { get; set; }
        public bool PlayerInputCurrentMouseRightRestore { get; set; }
        public bool HasPlayerItemAnimation { get; set; }
        public int PlayerItemAnimation { get; set; }
        public bool HasPlayerReuseDelay { get; set; }
        public int PlayerReuseDelay { get; set; }
        public bool HasPlayerChannel { get; set; }
        public bool PlayerChannel { get; set; }
        public bool HasPlayerPendingItemReuse { get; set; }
        public bool PlayerPendingItemReuse { get; set; }
        public bool IgnoreMouseInterfaceOverrideApplied { get; set; }

        public TravelMenuScopedJourneyState()
        {
            Scope = string.Empty;
            Message = string.Empty;
        }
    }
}
