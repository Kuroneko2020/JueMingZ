using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public sealed class LegacyEmbeddedPanelControl : LegacyUiControl
    {
        public string AdapterKind { get; set; }

        public LegacyEmbeddedPanelControl()
        {
            Kind = "adapter";
            AdapterKind = string.Empty;
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
        }

        protected override LegacyUiElement RegisterSelf(LegacyUiContext context)
        {
            return null;
        }
    }
}
