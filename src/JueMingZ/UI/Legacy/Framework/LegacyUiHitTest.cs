namespace JueMingZ.UI.Legacy.Framework
{
    public static class LegacyUiHitTest
    {
        public static bool IsHovered(LegacyUiContext context, LegacyUiRect rect)
        {
            return context != null && context.IsMouseOver(rect);
        }

        public static LegacyUiRect ClipHitRect(LegacyUiContext context, LegacyUiRect rect)
        {
            return context == null ? rect : context.ResolveHitRect(rect);
        }

        public static bool IsVisible(LegacyUiContext context, LegacyUiRect rect)
        {
            return context == null || context.IsRectVisible(rect);
        }
    }
}
