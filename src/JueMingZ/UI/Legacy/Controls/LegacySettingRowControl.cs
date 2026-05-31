using JueMingZ.UI;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public sealed class LegacySettingRowControl : LegacyUiControl
    {
        public int LabelRight { get; set; }

        public LegacySettingRowControl()
        {
            Kind = "row";
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
            DrawBackgroundAndLabel(context, Bounds, Label, LabelRight);
        }

        protected override LegacyUiElement RegisterSelf(LegacyUiContext context)
        {
            return null;
        }

        public static void DrawBackgroundAndLabel(LegacyUiContext context, LegacyUiRect row, string label, int labelRight)
        {
            if (context == null)
            {
                return;
            }

            LegacyUiTheme.DrawRowClipped(context.SpriteBatch, row, context.HasClip ? context.ClipRect : row);
            var labelWidth = System.Math.Max(60, labelRight - row.X - 20);
            UiTextRenderer.DrawAlignedTextClipped(
                context.SpriteBatch,
                label ?? string.Empty,
                row.X + 10,
                row.Y,
                labelWidth,
                row.Height,
                UiTextHorizontalAlignment.Left,
                context.HasClip ? context.ClipRect.X : row.X,
                context.HasClip ? context.ClipRect.Y : row.Y,
                context.HasClip ? context.ClipRect.Width : row.Width,
                context.HasClip ? context.ClipRect.Height : row.Height,
                238,
                238,
                226,
                255,
                0.86f);
        }
    }
}
