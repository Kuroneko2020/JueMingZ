using System;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    public sealed class LegacyUiCommand
    {
        public Guid CommandId { get; set; }
        public string ElementId { get; set; }
        public string Label { get; set; }
        public string Kind { get; set; }
        public LegacyUiRect Rect { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public string MouseReadMode { get; set; }
        public int IntValue { get; set; }
        public BuffPotionCandidate Candidate { get; set; }
        public BuffPotionWhitelistEntry WhitelistEntry { get; set; }
        public bool MouseCaptured { get; set; }
        public bool IsDoubleClick { get; set; }

        public LegacyUiCommand()
        {
            CommandId = Guid.NewGuid();
            ElementId = string.Empty;
            Label = string.Empty;
            Kind = string.Empty;
            MouseX = -1;
            MouseY = -1;
            MouseReadMode = string.Empty;
        }
    }
}
