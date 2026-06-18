using System;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public sealed class LegacyModeButtonGroup : LegacyUiControl
    {
        public string SelectedValue { get; set; }
        public string ElementPrefix { get; set; }
        public string[] ButtonLabels { get; set; }
        public string[] ButtonValues { get; set; }
        public string[] ButtonTooltips { get; set; }
        public int ButtonGap { get; set; }
        public int RightReserveWidth { get; set; }
        public string ElementLabelPrefix { get; set; }

        public LegacyModeButtonGroup()
        {
            SelectedValue = string.Empty;
            ElementPrefix = string.Empty;
            ButtonLabels = new string[0];
            ButtonValues = new string[0];
            ButtonGap = 6;
            RightReserveWidth = 0;
            ElementLabelPrefix = string.Empty;
        }

        public override LegacyUiElement Draw(LegacyUiContext context)
        {
            if (context == null || !Visible || !context.IsRectVisible(Bounds))
            {
                return null;
            }

            var totalWidth = LegacyUiLayout.TotalModeButtonWidth(ButtonLabels, ButtonGap);
            var x = Bounds.Right - totalWidth - 10 - Math.Max(0, RightReserveWidth);
            LegacySettingRowControl.DrawBackgroundAndLabel(context, Bounds, Label, x);
            var hovered = (LegacyUiElement)null;
            var y = LegacyUiLayout.RowModeButtonY(Bounds);
            var count = Math.Min(ButtonLabels == null ? 0 : ButtonLabels.Length, ButtonValues == null ? 0 : ButtonValues.Length);
            for (var index = 0; index < count; index++)
            {
                var width = LegacyUiLayout.ModeButtonWidth(ButtonLabels[index]);
                var rect = new LegacyUiRect(x, y, width, LegacyUiLayout.RowModeButtonHeight);
                var selected = string.Equals(SelectedValue, ButtonValues[index], StringComparison.OrdinalIgnoreCase);
                var buttonLabel = ButtonLabels[index];
                var elementLabel = string.IsNullOrWhiteSpace(ElementLabelPrefix) ? buttonLabel : ElementLabelPrefix + ":" + buttonLabel;
                var button = new LegacyButtonControl
                {
                    Id = ElementPrefix + ButtonValues[index],
                    Label = buttonLabel,
                    Text = buttonLabel,
                    ElementLabel = elementLabel,
                    Kind = "button",
                    Bounds = rect,
                    Selected = selected,
                    TextScale = 0.78f,
                    TooltipLines = ButtonTooltips != null && index < ButtonTooltips.Length && !string.IsNullOrWhiteSpace(ButtonTooltips[index])
                        ? new[] { ButtonTooltips[index] }
                        : null
                };
                var element = button.Draw(context);
                if (element != null && context.IsElementHovered(element.Id, rect))
                {
                    hovered = element;
                }

                x += width + ButtonGap;
            }

            return context.HoveredElement ?? hovered;
        }

        protected override void DrawSelf(LegacyUiContext context)
        {
        }
    }
}
