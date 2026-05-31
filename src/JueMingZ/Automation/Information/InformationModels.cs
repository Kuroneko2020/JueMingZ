using System;

namespace JueMingZ.Automation.Information
{
    public struct InformationColor
    {
        public int R;
        public int G;
        public int B;
        public int A;

        public InformationColor(int r, int g, int b, int a)
        {
            R = Clamp(r);
            G = Clamp(g);
            B = Clamp(b);
            A = Clamp(a);
        }

        private static int Clamp(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 255 ? 255 : value;
        }
    }

    public sealed class InformationWorldContext
    {
        public Type MainType { get; set; }
        public object LocalPlayer { get; set; }
        public float ScreenX { get; set; }
        public float ScreenY { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public float PlayerCenterX { get; set; }
        public float PlayerCenterY { get; set; }
        public ulong GameUpdateCount { get; set; }
        public string WorldKey { get; set; }
        public string PlayerRecordKey { get; set; }
        public string WorldRecordKey { get; set; }
        public string PlayerName { get; set; }
        public string WorldName { get; set; }

        public InformationWorldContext()
        {
            WorldKey = string.Empty;
            PlayerRecordKey = string.Empty;
            WorldRecordKey = string.Empty;
            PlayerName = string.Empty;
            WorldName = string.Empty;
        }
    }

    public sealed class InformationOverlayDiagnostics
    {
        public string EnabledSummary { get; set; }
        public int NpcLabelsDrawn { get; set; }
        public int ChestLabelsDrawn { get; set; }
        public int SignTextLabelsDrawn { get; set; }
        public int TombstoneTextLabelsDrawn { get; set; }
        public int TileHighlightsDrawn { get; set; }
        public int StatusLinesDrawn { get; set; }
        public double LastDrawElapsedMs { get; set; }
        public string LastSkipReason { get; set; }

        public InformationOverlayDiagnostics()
        {
            EnabledSummary = string.Empty;
            LastSkipReason = string.Empty;
        }
    }

    internal sealed class InformationLabelMeasure
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        public InformationLabelMeasure(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    internal sealed class InformationStatusLine
    {
        public int Order { get; set; }
        public string Text { get; set; }
        public InformationColor Color { get; set; }
        public double FontScale { get; set; }

        public InformationStatusLine()
        {
            Text = string.Empty;
            Color = new InformationColor(255, 255, 255, 255);
            FontScale = 0.72d;
        }
    }
}
