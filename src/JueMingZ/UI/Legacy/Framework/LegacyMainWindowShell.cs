namespace JueMingZ.UI.Legacy.Framework
{
    public sealed class LegacyMainWindowShell
    {
        public LegacyUiRect Window { get; private set; }
        public LegacyUiRect TitleRect { get; private set; }
        public LegacyUiRect ResizeRect { get; private set; }
        public LegacyUiRect ContentRect { get; private set; }

        private LegacyMainWindowShell()
        {
        }

        public static LegacyMainWindowShell Create(LegacyUiRect window)
        {
            var titleRect = new LegacyUiRect(window.X, window.Y, window.Width, LegacyUiMetrics.TitleHeight);
            var resizeRect = LegacyUiMetrics.AllowResize
                ? new LegacyUiRect(window.Right - LegacyUiMetrics.ResizeGripSize, window.Bottom - LegacyUiMetrics.ResizeGripSize, LegacyUiMetrics.ResizeGripSize, LegacyUiMetrics.ResizeGripSize)
                : new LegacyUiRect(window.Right, window.Bottom, 0, 0);
            var tabsTop = window.Y + LegacyUiMetrics.TitleHeight + LegacyUiMetrics.OuterPadding + LegacyUiMetrics.TabBlockYOffset;
            var tabsHeight = LegacyTabBar.GetBlockHeight(window.Width);
            var y = tabsTop + tabsHeight + LegacyUiMetrics.ContentGap;
            var bottomPad = LegacyUiMetrics.OuterPadding;
            return new LegacyMainWindowShell
            {
                Window = window,
                TitleRect = titleRect,
                ResizeRect = resizeRect,
                ContentRect = new LegacyUiRect(
                    window.X + LegacyUiMetrics.OuterPadding,
                    y,
                    window.Width - LegacyUiMetrics.OuterPadding * 2,
                    window.Bottom - y - bottomPad)
            };
        }
    }
}
