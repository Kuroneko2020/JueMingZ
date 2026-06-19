using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public sealed class LegacySectionHeaderControl : LegacyUiControl
    {
        public LegacySectionHeaderControl()
        {
            Kind = "section";
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
            var clip = context.HasClip ? context.ClipRect : Bounds;
            LegacyUiTheme.DrawSectionHeaderClipped(context.SpriteBatch, Bounds, clip);
            UiTextRenderer.DrawTextClipped(
                context.SpriteBatch,
                Label,
                Bounds.X + 10,
                Bounds.Y + 3,
                Bounds.Width - 20,
                20,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                244,
                238,
                210,
                255,
                LegacyUiMetrics.SectionHeaderTextScale);
        }

        protected override LegacyUiElement RegisterSelf(LegacyUiContext context)
        {
            return null;
        }
    }
}
