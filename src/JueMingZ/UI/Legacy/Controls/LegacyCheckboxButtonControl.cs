using JueMingZ.UI;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public sealed class LegacyCheckboxButtonControl : LegacyUiControl
    {
        public float TextScale { get; set; }

        public LegacyCheckboxButtonControl()
        {
            TextScale = 0.76f;
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
            var hovered = context.IsElementHovered(Id, Bounds);
            var pressed = hovered && context.Mouse != null && context.Mouse.LeftDown;
            var contentOffset = LegacyUiTheme.GetSelectedButtonContentOffset(Selected, Enabled);
            var contentHeight = System.Math.Max(1, Bounds.Height - contentOffset);
            LegacyUiTheme.DrawButton(context.SpriteBatch, Bounds, hovered, pressed, Selected, Enabled);
            UiTextRenderer.DrawAlignedText(
                context.SpriteBatch,
                Label,
                Bounds.X + 10,
                Bounds.Y + contentOffset,
                System.Math.Max(1, Bounds.Width - 40),
                contentHeight,
                UiTextHorizontalAlignment.Left,
                238,
                238,
                226,
                255,
                TextScale);

            var box = new LegacyUiRect(Bounds.Right - 25, Bounds.Y + contentOffset + System.Math.Max(0, (contentHeight - 12) / 2), 12, 12);
            UiPrimitiveRenderer.DrawRectBorder(context.SpriteBatch, box.X, box.Y, box.Width, box.Height, 1, Selected ? 88 : 170, Selected ? 250 : 186, Selected ? 136 : 200, 235);
            if (Selected)
            {
                UiPrimitiveRenderer.DrawFilledRect(context.SpriteBatch, box.X + 3, box.Y + 3, box.Width - 6, box.Height - 6, 88, 250, 136, 245);
            }
        }
    }
}
