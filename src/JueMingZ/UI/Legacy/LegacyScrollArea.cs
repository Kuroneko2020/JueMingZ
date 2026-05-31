using System;

namespace JueMingZ.UI.Legacy
{
    public sealed class LegacyScrollArea
    {
        public LegacyUiRect Viewport { get; set; }
        public LegacyUiRect ContentRect { get; set; }
        public LegacyUiRect ScrollbarTrack { get; set; }
        public LegacyUiRect ScrollbarThumb { get; set; }
        public int ScrollOffset { get; set; }
        public int MaxScroll { get; set; }

        public bool NeedsScroll
        {
            get { return MaxScroll > 0; }
        }

        public static LegacyScrollArea Create(LegacyUiRect contentRect, int contentHeight, int scrollOffset)
        {
            var viewport = new LegacyUiRect(
                contentRect.X + LegacyUiMetrics.ContentPadding,
                contentRect.Y + LegacyUiMetrics.ContentPadding,
                contentRect.Width - LegacyUiMetrics.ContentPadding * 2 - LegacyUiMetrics.ScrollbarWidth - 8,
                contentRect.Height - LegacyUiMetrics.ContentPadding * 2);
            var maxScroll = Math.Max(0, contentHeight - viewport.Height);
            var offset = Clamp(scrollOffset, 0, maxScroll);
            var track = new LegacyUiRect(contentRect.Right - LegacyUiMetrics.ContentPadding - LegacyUiMetrics.ScrollbarWidth, viewport.Y, LegacyUiMetrics.ScrollbarWidth, viewport.Height);
            var thumbHeight = maxScroll <= 0
                ? track.Height
                : Math.Max(32, (int)Math.Round(track.Height * (viewport.Height / (double)Math.Max(viewport.Height, contentHeight))));
            var thumbTravel = Math.Max(0, track.Height - thumbHeight);
            var thumbY = track.Y + (maxScroll <= 0 ? 0 : (int)Math.Round(thumbTravel * (offset / (double)maxScroll)));
            return new LegacyScrollArea
            {
                Viewport = viewport,
                ContentRect = contentRect,
                ScrollbarTrack = track,
                ScrollbarThumb = new LegacyUiRect(track.X, thumbY, track.Width, thumbHeight),
                ScrollOffset = offset,
                MaxScroll = maxScroll
            };
        }

        public int ToScreenY(int contentY)
        {
            return Viewport.Y + contentY - ScrollOffset;
        }

        public bool IsVisible(LegacyUiRect rect)
        {
            return rect.Intersects(Viewport);
        }

        public int ThumbToScroll(int mouseY)
        {
            if (MaxScroll <= 0)
            {
                return 0;
            }

            var thumbTravel = Math.Max(1, ScrollbarTrack.Height - ScrollbarThumb.Height);
            var relative = Clamp(mouseY - ScrollbarTrack.Y - ScrollbarThumb.Height / 2, 0, thumbTravel);
            return Clamp((int)Math.Round(MaxScroll * (relative / (double)thumbTravel)), 0, MaxScroll);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
