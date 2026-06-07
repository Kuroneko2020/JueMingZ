namespace JueMingZ.Compat
{
    public sealed class MouseTargetInputState
    {
        // Captured flags are restore contracts for mouse/tile input, including
        // controlUseTile intent versus confirmed interaction.
        public bool Captured { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public bool PlayerInputMouseCaptured { get; set; }
        public int PlayerInputMouseX { get; set; }
        public int PlayerInputMouseY { get; set; }
        public bool TileTargetCaptured { get; set; }
        public int TileTargetX { get; set; }
        public int TileTargetY { get; set; }
        public bool SmartStateCaptured { get; set; }
        public bool SmartInteractShowingGenuine { get; set; }
        public bool SmartInteractShowingFake { get; set; }
        public bool SmartCursorWantedMouse { get; set; }
        public bool SmartCursorWantedGamePad { get; set; }
        public bool SmartCursorShowing { get; set; }
        public bool TileInteractionInputCaptured { get; set; }
        public bool ControlUseTile { get; set; }
        public bool ReleaseUseTile { get; set; }
        public bool TileInteractAttempted { get; set; }
    }
}
