using System;
using JueMingZ.UI;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public class LegacyButtonControl : LegacyUiControl
    {
        public string Text { get; set; }
        public string ElementLabel { get; set; }
        public string IconId { get; set; }
        public float TextScale { get; set; }
        public int TextR { get; set; }
        public int TextG { get; set; }
        public int TextB { get; set; }
        public int TextA { get; set; }
        public int IconSize { get; set; }
        public int IconTextGap { get; set; }

        public LegacyButtonControl()
        {
            Text = string.Empty;
            ElementLabel = string.Empty;
            IconId = string.Empty;
            TextScale = 0.78f;
            TextR = 230;
            TextG = 232;
            TextB = 224;
            TextA = 255;
            IconSize = 18;
            IconTextGap = 6;
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
            var hovered = context.IsElementHovered(Id, Bounds);
            var pressed = hovered && context.Mouse != null && context.Mouse.LeftDown;
            var text = string.IsNullOrWhiteSpace(Text) ? Label : Text;
            var selectedOffset = Selected && Enabled ? 1 : 0;
            if (context.HasClip)
            {
                LegacyUiTheme.DrawButtonClipped(context.SpriteBatch, Bounds, hovered, pressed, Selected, Enabled, context.ClipRect);
                if (!string.IsNullOrWhiteSpace(IconId))
                {
                    DrawIconTextClipped(context.SpriteBatch, text, context.ClipRect);
                    return;
                }

                UiTextRenderer.DrawCenteredTextClipped(
                    context.SpriteBatch,
                    text,
                    Bounds.X + 3,
                    Bounds.Y + selectedOffset,
                    Bounds.Width - 6,
                    Math.Max(1, Bounds.Height - selectedOffset),
                    context.ClipRect.X,
                    context.ClipRect.Y,
                    context.ClipRect.Width,
                    context.ClipRect.Height,
                    Selected ? LegacyUiTheme.SelectedTextR : TextR,
                    Selected ? LegacyUiTheme.SelectedTextG : TextG,
                    Selected ? LegacyUiTheme.SelectedTextB : TextB,
                    TextA,
                    TextScale);

                return;
            }

            LegacyUiTheme.DrawButton(context.SpriteBatch, Bounds, hovered, pressed, Selected, Enabled);
            if (!string.IsNullOrWhiteSpace(IconId))
            {
                DrawIconTextClipped(context.SpriteBatch, text, Bounds);
                return;
            }

            UiTextRenderer.DrawCenteredText(
                context.SpriteBatch,
                text,
                Bounds.X + 3,
                Bounds.Y + selectedOffset,
                Bounds.Width - 6,
                Math.Max(1, Bounds.Height - selectedOffset),
                Selected ? LegacyUiTheme.SelectedTextR : TextR,
                Selected ? LegacyUiTheme.SelectedTextG : TextG,
                Selected ? LegacyUiTheme.SelectedTextB : TextB,
                TextA,
                TextScale);
        }

        private void DrawIconTextClipped(object spriteBatch, string text, LegacyUiRect clip)
        {
            var iconSize = Math.Max(12, Math.Min(20, IconSize));
            var gap = Math.Max(4, IconTextGap);
            var availableWidth = Math.Max(1, Bounds.Width - 10);
            var textAvailable = Math.Max(1, availableWidth - iconSize - gap);
            var scale = TextScale;
            while (scale > 0.66f && UiTextRenderer.EstimateTextWidth(text, scale) > textAvailable)
            {
                scale -= 0.02f;
            }

            var display = UiTextRenderer.Ellipsize(text, textAvailable, scale);
            var textWidth = Math.Min(textAvailable, UiTextRenderer.EstimateTextWidth(display, scale));
            var groupWidth = iconSize + gap + textWidth;
            var startX = Bounds.X + Math.Max(5, (Bounds.Width - groupWidth) / 2);
            var selectedOffset = Selected && Enabled ? 1 : 0;
            var contentY = Bounds.Y + selectedOffset;
            var contentHeight = Math.Max(1, Bounds.Height - selectedOffset);
            var iconY = contentY + (contentHeight - iconSize) / 2;
            var textX = startX + iconSize + gap;
            var textRectWidth = Math.Max(1, Bounds.Right - textX - 4);
            var textR = Selected ? LegacyUiTheme.SelectedTextR : TextR;
            var textG = Selected ? LegacyUiTheme.SelectedTextG : TextG;
            var textB = Selected ? LegacyUiTheme.SelectedTextB : TextB;

            LegacyVectorIconRenderer.Draw(
                spriteBatch,
                IconId,
                new LegacyUiRect(startX, iconY, iconSize, iconSize),
                clip,
                Selected,
                Enabled);

            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                display,
                textX,
                contentY,
                textRectWidth,
                contentHeight,
                UiTextHorizontalAlignment.Left,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                textR,
                textG,
                textB,
                TextA,
                scale);
        }

        protected override LegacyUiElement RegisterSelf(LegacyUiContext context)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                return null;
            }

            return context.RegisterElement(
                Id,
                string.IsNullOrWhiteSpace(ElementLabel) ? Label : ElementLabel,
                Kind,
                Bounds,
                Enabled,
                Selected,
                IntValue,
                MinValue,
                MaxValue,
                TooltipLines);
        }
    }

    public sealed class LegacySmallButtonControl : LegacyButtonControl
    {
        public LegacySmallButtonControl()
        {
            TextScale = 0.70f;
        }
    }
}
