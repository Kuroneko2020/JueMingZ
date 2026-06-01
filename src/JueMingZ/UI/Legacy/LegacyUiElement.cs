using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    public sealed class LegacyUiElement
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Kind { get; set; }
        public LegacyUiRect Rect { get; set; }
        public bool Enabled { get; set; }
        public bool Selected { get; set; }
        public int IntValue { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public BuffPotionCandidate Candidate { get; set; }
        public BuffPotionWhitelistEntry WhitelistEntry { get; set; }
        public string[] TooltipLines { get; set; }

        public LegacyUiElement()
        {
            Id = string.Empty;
            Label = string.Empty;
            Kind = string.Empty;
            Enabled = true;
        }

        public void Reset(
            string id,
            string label,
            string kind,
            LegacyUiRect rect,
            bool enabled,
            bool selected,
            int intValue,
            int minValue,
            int maxValue,
            string[] tooltipLines,
            BuffPotionCandidate candidate,
            BuffPotionWhitelistEntry whitelistEntry)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Kind = kind ?? string.Empty;
            Rect = rect;
            Enabled = enabled;
            Selected = selected;
            IntValue = intValue;
            MinValue = minValue;
            MaxValue = maxValue;
            Candidate = candidate;
            WhitelistEntry = whitelistEntry;
            TooltipLines = tooltipLines;
        }
    }
}
