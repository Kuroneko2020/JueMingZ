namespace JueMingZ.UI
{
    public sealed class DiagnosticMouseState
    {
        public bool TerrariaReadAvailable { get; set; }
        public int TerrariaMouseX { get; set; }
        public int TerrariaMouseY { get; set; }
        public bool GameInputAvailable { get; set; }
        public bool TerrariaLeftDown { get; set; }
        public bool TerrariaLeftReleaseAvailable { get; set; }
        public bool TerrariaLeftRelease { get; set; }
        public bool TerrariaScrollWheelAvailable { get; set; }
        public int TerrariaScrollWheel { get; set; }
        public int TerrariaOldScrollWheel { get; set; }
        public int ScrollDelta { get; set; }
        public bool OsReadAvailable { get; set; }
        public int OsClientMouseX { get; set; }
        public int OsClientMouseY { get; set; }
        public bool OsLeftDown { get; set; }
        public bool UiScaleAvailable { get; set; }
        public double UiScale { get; set; }
        public double UiScaleX { get; set; }
        public double UiScaleY { get; set; }
        public double UiTranslateX { get; set; }
        public double UiTranslateY { get; set; }
        public bool UiScaleMatrixAvailable { get; set; }
        public string UiScaleSource { get; set; }
        public string ReadMode { get; set; }
        public string LastError { get; set; }

        public DiagnosticMouseState()
        {
            TerrariaMouseX = -1;
            TerrariaMouseY = -1;
            GameInputAvailable = true;
            OsClientMouseX = -1;
            OsClientMouseY = -1;
            UiScale = 1d;
            UiScaleX = 1d;
            UiScaleY = 1d;
            UiTranslateX = 0d;
            UiTranslateY = 0d;
            UiScaleSource = string.Empty;
            ReadMode = "none";
            LastError = string.Empty;
        }
    }
}
