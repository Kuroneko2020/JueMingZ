using System;

namespace JueMingZ.UI
{
    public sealed class DiagnosticButtonCommand
    {
        public Guid CommandId { get; set; }
        public string ButtonId { get; set; }
        public string ButtonLabel { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string HitTestMode { get; set; }
        public int HitTestX { get; set; }
        public int HitTestY { get; set; }
        public bool HitTestConflict { get; set; }
        public string CandidateHits { get; set; }
        public int VisualRectX { get; set; }
        public int VisualRectY { get; set; }
        public int VisualRectWidth { get; set; }
        public int VisualRectHeight { get; set; }
        public int HitRectX { get; set; }
        public int HitRectY { get; set; }
        public int HitRectWidth { get; set; }
        public int HitRectHeight { get; set; }
        public int TerrariaMouseX { get; set; }
        public int TerrariaMouseY { get; set; }
        public bool TerrariaLeftDown { get; set; }
        public int OsClientMouseX { get; set; }
        public int OsClientMouseY { get; set; }
        public bool OsLeftDown { get; set; }
        public double UiScale { get; set; }
        public string ClickSource { get; set; }
        public bool UiMouseCaptureAvailable { get; set; }
        public string UiMouseCaptureMessage { get; set; }
        public string UiWindow { get; set; }
        public string UiElementId { get; set; }
        public bool MouseCaptured { get; set; }

        public DiagnosticButtonCommand()
        {
            CommandId = Guid.NewGuid();
            ButtonId = string.Empty;
            ButtonLabel = string.Empty;
            CreatedUtc = DateTime.UtcNow;
            HitTestMode = "none";
            HitTestX = -1;
            HitTestY = -1;
            CandidateHits = string.Empty;
            VisualRectX = -1;
            VisualRectY = -1;
            HitRectX = -1;
            HitRectY = -1;
            TerrariaMouseX = -1;
            TerrariaMouseY = -1;
            OsClientMouseX = -1;
            OsClientMouseY = -1;
            UiScale = 1d;
            ClickSource = "none";
            UiMouseCaptureMessage = string.Empty;
            UiWindow = "OperationWindow";
            UiElementId = string.Empty;
        }
    }
}
