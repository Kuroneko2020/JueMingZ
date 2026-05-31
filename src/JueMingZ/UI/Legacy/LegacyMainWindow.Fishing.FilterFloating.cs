using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static int BuildFishingFilterFloatingHeight(LegacyScrollArea area, LegacyUiRect anchor, int maxHeight, int minHeight)
        {
            var availableAbove = Math.Max(0, anchor.Y - area.Viewport.Y - FishingFilterFloatingGap - 4);
            var preferredMin = Math.Min(Math.Max(1, minHeight), Math.Max(1, maxHeight));
            if (availableAbove <= 0)
            {
                return preferredMin;
            }

            return Math.Min(Math.Max(preferredMin, availableAbove), Math.Max(preferredMin, maxHeight));
        }

        private static LegacyUiRect BuildFishingFilterFloatingRect(LegacyScrollArea area, LegacyUiRect host, LegacyUiRect anchor, int height)
        {
            var width = Math.Max(140, host.Width - 20);
            width = Math.Min(width, Math.Max(80, area.Viewport.Width - 8));
            var x = host.X + 10;
            x = Math.Max(area.Viewport.X + 4, Math.Min(x, area.Viewport.Right - width - 4));
            var y = anchor.Y - height - FishingFilterFloatingGap;
            y = Math.Max(area.Viewport.Y + 4, y);
            return new LegacyUiRect(x, y, width, height);
        }

        private static void DrawFishingFilterFloatingConnector(object spriteBatch, LegacyScrollArea area, LegacyUiRect popup, LegacyUiRect anchor)
        {
            var centerX = anchor.X + anchor.Width / 2;
            centerX = Math.Max(popup.X + 12, Math.Min(popup.Right - 12, centerX));
            var stemTop = popup.Bottom - 1;
            var stemBottom = anchor.Y + 1;
            if (stemBottom <= stemTop)
            {
                return;
            }

            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, centerX - 1, stemTop, 2, stemBottom - stemTop, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, FishingFilterLinkR, FishingFilterLinkG, FishingFilterLinkB, FishingFilterLinkA);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, centerX - 4, popup.Bottom - 3, 8, 3, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, FishingFilterLinkR, FishingFilterLinkG, FishingFilterLinkB, FishingFilterLinkA);
        }

        private static void DrawFishingFilterFloatingBorder(object spriteBatch, LegacyScrollArea area, LegacyUiRect rect)
        {
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 1, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, FishingFilterLinkR, FishingFilterLinkG, FishingFilterLinkB, FishingFilterLinkA);
        }
    }
}
