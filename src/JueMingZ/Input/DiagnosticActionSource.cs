using JueMingZ.UI;

namespace JueMingZ.Input
{
    public sealed class DiagnosticActionSource
    {
        public string Kind { get; private set; }
        public string Hotkey { get; private set; }
        public string Ui { get; private set; }
        public string ButtonId { get; private set; }
        public string ButtonLabel { get; private set; }
        public string HitTestMode { get; private set; }
        public int HitTestX { get; private set; }
        public int HitTestY { get; private set; }
        public bool HitTestConflict { get; private set; }
        public string CandidateHits { get; private set; }
        public int VisualRectX { get; private set; }
        public int VisualRectY { get; private set; }
        public int VisualRectWidth { get; private set; }
        public int VisualRectHeight { get; private set; }
        public int HitRectX { get; private set; }
        public int HitRectY { get; private set; }
        public int HitRectWidth { get; private set; }
        public int HitRectHeight { get; private set; }
        public string ClickSource { get; private set; }
        public int TerrariaMouseX { get; private set; }
        public int TerrariaMouseY { get; private set; }
        public int OsClientMouseX { get; private set; }
        public int OsClientMouseY { get; private set; }
        public bool TerrariaLeftDown { get; private set; }
        public bool OsLeftDown { get; private set; }
        public double UiScale { get; private set; }
        public string UiWindow { get; private set; }
        public string UiElementId { get; private set; }
        public bool MouseCaptured { get; private set; }

        private DiagnosticActionSource()
        {
            Kind = string.Empty;
            Hotkey = string.Empty;
            Ui = string.Empty;
            ButtonId = string.Empty;
            ButtonLabel = string.Empty;
            HitTestMode = string.Empty;
            HitTestX = -1;
            HitTestY = -1;
            CandidateHits = string.Empty;
            VisualRectX = -1;
            VisualRectY = -1;
            HitRectX = -1;
            HitRectY = -1;
            ClickSource = string.Empty;
            TerrariaMouseX = -1;
            TerrariaMouseY = -1;
            OsClientMouseX = -1;
            OsClientMouseY = -1;
            UiScale = 1d;
            UiWindow = string.Empty;
            UiElementId = string.Empty;
        }

        public static DiagnosticActionSource ForHotkey(string hotkey)
        {
            return new DiagnosticActionSource
            {
                Kind = "Hotkey",
                Hotkey = hotkey ?? string.Empty
            };
        }

        public static DiagnosticActionSource ForButton(string buttonId, string buttonLabel)
        {
            return new DiagnosticActionSource
            {
                Kind = "Button",
                Ui = "F5DiagnosticsOverlay",
                ButtonId = buttonId ?? string.Empty,
                ButtonLabel = buttonLabel ?? string.Empty
            };
        }

        public static DiagnosticActionSource ForButton(DiagnosticButtonCommand command)
        {
            if (command == null)
            {
                return ForButton(string.Empty, string.Empty);
            }

            return new DiagnosticActionSource
            {
                Kind = "Button",
                Ui = "F5DiagnosticsOverlay",
                ButtonId = command.ButtonId ?? string.Empty,
                ButtonLabel = command.ButtonLabel ?? string.Empty,
                HitTestMode = command.HitTestMode ?? string.Empty,
                HitTestX = command.HitTestX,
                HitTestY = command.HitTestY,
                HitTestConflict = command.HitTestConflict,
                CandidateHits = command.CandidateHits ?? string.Empty,
                VisualRectX = command.VisualRectX,
                VisualRectY = command.VisualRectY,
                VisualRectWidth = command.VisualRectWidth,
                VisualRectHeight = command.VisualRectHeight,
                HitRectX = command.HitRectX,
                HitRectY = command.HitRectY,
                HitRectWidth = command.HitRectWidth,
                HitRectHeight = command.HitRectHeight,
                ClickSource = command.ClickSource ?? string.Empty,
                TerrariaMouseX = command.TerrariaMouseX,
                TerrariaMouseY = command.TerrariaMouseY,
                TerrariaLeftDown = command.TerrariaLeftDown,
                OsClientMouseX = command.OsClientMouseX,
                OsClientMouseY = command.OsClientMouseY,
                OsLeftDown = command.OsLeftDown,
                UiScale = command.UiScale,
                UiWindow = command.UiWindow ?? string.Empty,
                UiElementId = command.UiElementId ?? string.Empty,
                MouseCaptured = command.MouseCaptured
            };
        }
    }
}
