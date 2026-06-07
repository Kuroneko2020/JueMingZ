namespace JueMingZ.Compat
{
    public sealed class TerrariaUiInputContext
    {
        // UI context separates input intent from capture ownership; blocking
        // flags should make actions yield, not clear Terraria state.
        public bool MainTypeUnavailable { get; set; }
        public bool GameMenu { get; set; }
        public bool ChatOpen { get; set; }
        public bool NpcChatOpen { get; set; }
        public bool PlayerInventoryOpen { get; set; }
        public bool ChestOpen { get; set; }
        public bool PlayerMouseInterface { get; set; }
        public bool MainMouseInterface { get; set; }
        public bool MainBlockMouse { get; set; }

        public bool MouseCapturedByUi
        {
            get { return PlayerMouseInterface || MainMouseInterface || MainBlockMouse; }
        }

        public string MouseCaptureReason
        {
            get
            {
                if (PlayerMouseInterface)
                {
                    return "playerMouseInterface";
                }

                if (MainMouseInterface)
                {
                    return "mainMouseInterface";
                }

                if (MainBlockMouse)
                {
                    return "mainBlockMouse";
                }

                return string.Empty;
            }
        }
    }
}
