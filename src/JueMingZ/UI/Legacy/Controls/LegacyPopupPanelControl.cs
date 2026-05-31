using JueMingZ.UI;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public sealed class LegacyPopupPanelControl : LegacyUiControl
    {
        public bool DrawHeaderLine { get; set; }

        public LegacyPopupPanelControl()
        {
            Kind = "blocker";
            DrawHeaderLine = true;
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
            UiPrimitiveRenderer.DrawRoundedRect(context.SpriteBatch, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, LegacyUiTheme.Radius, 48, 58, 88, 238);
            UiPrimitiveRenderer.DrawRoundedRect(context.SpriteBatch, Bounds.X + 1, Bounds.Y + 1, Bounds.Width - 2, Bounds.Height - 2, LegacyUiTheme.Radius - 1, 18, 23, 38, 238);
            if (DrawHeaderLine)
            {
                UiPrimitiveRenderer.DrawFilledRect(context.SpriteBatch, Bounds.X + 10, Bounds.Y + 34, Bounds.Width - 20, 1, 116, 136, 176, 145);
            }
        }
    }
}
