namespace JueMingZ.Compat
{
    public sealed class LegacyUiSkinPalette
    {
        public int PanelR { get; private set; }
        public int PanelG { get; private set; }
        public int PanelB { get; private set; }
        public int PanelA { get; private set; }
        public int ContentR { get; private set; }
        public int ContentG { get; private set; }
        public int ContentB { get; private set; }
        public int ContentA { get; private set; }
        public int RowR { get; private set; }
        public int RowG { get; private set; }
        public int RowB { get; private set; }
        public int RowA { get; private set; }
        public int HeaderR { get; private set; }
        public int HeaderG { get; private set; }
        public int HeaderB { get; private set; }
        public int HeaderA { get; private set; }
        public int BorderR { get; private set; }
        public int BorderG { get; private set; }
        public int BorderB { get; private set; }
        public int BorderA { get; private set; }
        public int OverlayR { get; private set; }
        public int OverlayG { get; private set; }
        public int OverlayB { get; private set; }
        public int OverlayA { get; private set; }
        public string Source { get; private set; }
        public bool FallbackUsed { get; private set; }

        public static LegacyUiSkinPalette CreateFallback()
        {
            return CreateFromBase(75, 98, 154, "Fallback", true);
        }

        public static LegacyUiSkinPalette CreateFromBase(int r, int g, int b, string source, bool fallbackUsed)
        {
            var palette = new LegacyUiSkinPalette();
            palette.Source = string.IsNullOrWhiteSpace(source) ? "Fallback" : source;
            palette.FallbackUsed = fallbackUsed;

            palette.PanelR = Mix(r, 15, 0.08d);
            palette.PanelG = Mix(g, 18, 0.08d);
            palette.PanelB = Mix(b, 30, 0.08d);
            palette.PanelA = 240;
            palette.ContentR = Mix(r, 8, 0.14d);
            palette.ContentG = Mix(g, 10, 0.14d);
            palette.ContentB = Mix(b, 22, 0.14d);
            palette.ContentA = 224;
            palette.RowR = Mix(r, 10, 0.20d);
            palette.RowG = Mix(g, 13, 0.20d);
            palette.RowB = Mix(b, 28, 0.20d);
            palette.RowA = 188;
            palette.HeaderR = Mix(r, 255, 0.06d);
            palette.HeaderG = Mix(g, 255, 0.06d);
            palette.HeaderB = Mix(b, 255, 0.06d);
            palette.HeaderA = 218;
            palette.BorderR = Mix(r, 255, 0.30d);
            palette.BorderG = Mix(g, 255, 0.30d);
            palette.BorderB = Mix(b, 255, 0.30d);
            palette.BorderA = 220;
            palette.OverlayR = 0;
            palette.OverlayG = 0;
            palette.OverlayB = 0;
            palette.OverlayA = 38;
            return palette;
        }

        private static int Mix(int value, int other, double amount)
        {
            return Clamp((int)(value + (other - value) * amount));
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
}
