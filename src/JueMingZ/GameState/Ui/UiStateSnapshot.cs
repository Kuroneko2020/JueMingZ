namespace JueMingZ.GameState.Ui
{
    public sealed class UiStateSnapshot
    {
        // Mouse/UI capture flags describe ownership only; clearing vanilla
        // mouse capture flags belongs to Compat UI capture code.
        public UiStateSnapshot()
        {
            GameInputAvailable = true;
        }

        public bool IsInMainMenu { get; set; }
        public bool GameInputAvailable { get; set; }
        public bool PlayerInventoryOpen { get; set; }
        public bool ChatOpen { get; set; }
        public bool ChestOpen { get; set; }
        public bool NpcChatOpen { get; set; }
        public bool HasBlockingUi { get; set; }
    }
}
