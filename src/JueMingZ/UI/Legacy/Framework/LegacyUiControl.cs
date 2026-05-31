namespace JueMingZ.UI.Legacy.Framework
{
    public abstract class LegacyUiControl
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Kind { get; set; }
        public LegacyUiRect Bounds { get; set; }
        public bool Visible { get; set; }
        public bool Enabled { get; set; }
        public bool Selected { get; set; }
        public int IntValue { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public string[] TooltipLines { get; set; }

        protected LegacyUiControl()
        {
            Id = string.Empty;
            Label = string.Empty;
            Kind = "button";
            Visible = true;
            Enabled = true;
        }

        public virtual LegacyUiElement Draw(LegacyUiContext context)
        {
            if (context == null || !Visible || !context.IsRectVisible(Bounds))
            {
                return null;
            }

            DrawSelf(context);
            return RegisterSelf(context);
        }

        protected abstract void DrawSelf(LegacyUiContext context);

        protected virtual LegacyUiElement RegisterSelf(LegacyUiContext context)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                return null;
            }

            return context.RegisterElement(
                Id,
                Label,
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
}
