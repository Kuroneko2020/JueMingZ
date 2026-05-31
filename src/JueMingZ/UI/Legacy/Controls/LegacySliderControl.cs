using System;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public sealed class LegacySliderControl : LegacyUiControl
    {
        public string SliderLabel { get; set; }
        public string Suffix { get; set; }

        public LegacySliderControl()
        {
            Kind = "slider";
            SliderLabel = string.Empty;
            Suffix = "%";
        }

        public override LegacyUiElement Draw(LegacyUiContext context)
        {
            if (context == null || !Visible || !context.IsRectVisible(Bounds))
            {
                return null;
            }

            var element = RegisterAndUpdate(context);
            DrawSelf(context);
            return context.IsMouseOver(Bounds) ? element : null;
        }

        public LegacyUiElement RegisterAndUpdate(LegacyUiContext context)
        {
            if (context == null || !Visible)
            {
                return null;
            }

            var element = RegisterSelf(context);
            if (Enabled)
            {
                LegacyUiInput.BeginOrUpdateSlider(context.Mouse, element);
            }
            else
            {
                LegacyUiInput.CancelActiveSlider(Id);
            }

            return element;
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
            var dragging = Enabled && string.Equals(LegacyUiInput.ActiveSliderId, Id, StringComparison.Ordinal);
            var value = LegacyUiInput.GetSliderDisplayValue(Id, IntValue);
            LegacySlider.DrawValue(
                context.SpriteBatch,
                Bounds,
                string.IsNullOrWhiteSpace(SliderLabel) ? Label : SliderLabel,
                value,
                MinValue,
                MaxValue,
                Suffix,
                context.IsMouseOver(Bounds),
                dragging);
        }
    }
}
