using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public class LegacyPanelControl : LegacyUiControl
    {
        public LegacyPanelStyle Style { get; set; }

        public LegacyPanelControl()
        {
            Style = LegacyPanelStyle.Panel;
            Kind = string.Empty;
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
            if (Style == LegacyPanelStyle.Content)
            {
                LegacyUiTheme.DrawContentPanel(context.SpriteBatch, Bounds);
            }
            else
            {
                LegacyUiTheme.DrawPanel(context.SpriteBatch, Bounds);
            }
        }

        protected override LegacyUiElement RegisterSelf(LegacyUiContext context)
        {
            return null;
        }
    }

    public enum LegacyPanelStyle
    {
        Panel,
        Content
    }
}
