namespace JueMingZ.Input.Hotkeys
{
    public sealed class UnifiedHotkeyRuntimeGateContext
    {
        public UnifiedHotkeyRuntimeGateContext()
        {
            IsInWorld = true;
            GameInputAvailable = true;
            Foreground = true;
            TerrariaTextInputReason = string.Empty;
        }

        public bool IsInWorld { get; set; }
        public bool MainMenu { get; set; }
        public bool GameInputAvailable { get; set; }
        public bool Foreground { get; set; }
        public bool TerrariaTextInputFocused { get; set; }
        public string TerrariaTextInputReason { get; set; }
        public bool NpcChatOpen { get; set; }
        public bool LegacyModalOpen { get; set; }
        public bool LegacyUiVisible { get; set; }
        public bool LegacyUiActiveInteraction { get; set; }
        public bool F5TextInputFocused { get; set; }
        public bool ColorInputFocused { get; set; }
        public bool NameInputFocused { get; set; }
        public bool HotkeyCaptureActive { get; set; }
    }
}
