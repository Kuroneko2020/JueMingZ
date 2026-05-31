using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy.Controls
{
    public sealed class LegacyScrollContainerControl
    {
        public LegacyScrollArea Area { get; private set; }

        private LegacyScrollContainerControl(LegacyScrollArea area)
        {
            Area = area;
        }

        public static LegacyScrollContainerControl Create(LegacyUiRect contentRect, int contentHeight, int scrollOffset)
        {
            return new LegacyScrollContainerControl(LegacyScrollArea.Create(contentRect, contentHeight, scrollOffset));
        }

        public void DrawScrollbar(object spriteBatch)
        {
            if (Area != null)
            {
                LegacyUiTheme.DrawScrollbar(spriteBatch, Area.ScrollbarTrack, Area.ScrollbarThumb);
            }
        }
    }
}
