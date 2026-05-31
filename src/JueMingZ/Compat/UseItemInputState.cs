namespace JueMingZ.Compat
{
    public sealed class UseItemInputState
    {
        public bool Captured { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public bool PlayerInputMouseCaptured { get; set; }
        public int PlayerInputMouseX { get; set; }
        public int PlayerInputMouseY { get; set; }
        public bool TileTargetCaptured { get; set; }
        public int TileTargetX { get; set; }
        public int TileTargetY { get; set; }
        public int SelectedSlot { get; set; }
        public bool UseItemHeld { get; set; }
        public bool UseItemReleased { get; set; }
        public bool MainMouseLeftCaptured { get; set; }
        public bool MainMouseLeft { get; set; }
        public bool MainMouseRightCaptured { get; set; }
        public bool MainMouseRight { get; set; }
        public bool MainMouseLeftReleaseCaptured { get; set; }
        public bool MainMouseLeftRelease { get; set; }
        public bool MainMouseRightReleaseCaptured { get; set; }
        public bool MainMouseRightRelease { get; set; }
    }
}
