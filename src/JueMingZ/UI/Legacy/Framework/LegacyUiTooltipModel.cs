namespace JueMingZ.UI.Legacy.Framework
{
    public sealed class LegacyUiTooltipModel
    {
        public string[] Lines { get; private set; }
        public bool Centered { get; private set; }

        public LegacyUiTooltipModel(string[] lines, bool centered)
        {
            Lines = lines ?? new string[0];
            Centered = centered;
        }
    }
}
