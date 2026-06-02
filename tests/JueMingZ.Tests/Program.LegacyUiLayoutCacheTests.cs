using System;
using JueMingZ.Automation.Information;
using JueMingZ.Config;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void LegacyUiPageLayoutCacheIgnoresWindowPosition()
        {
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            settings.InformationEnemyNameLabelsEnabled = true;
            settings.InformationSignTextLabelsMode = InformationSignTextModes.Lines;

            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 128);
            var first = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("information", window, content, 24, settings);

            if (first.MaxScroll <= 0)
            {
                throw new InvalidOperationException("Expected test layout to be scrollable.");
            }

            var movedWindow = new LegacyUiRect(240, 170, window.Width, window.Height);
            var movedContent = new LegacyUiRect(258, 254, content.Width, content.Height);
            var moved = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("information", movedWindow, movedContent, 24, settings);

            if (moved.RebuildCount != first.RebuildCount || moved.HitCount <= first.HitCount)
            {
                throw new InvalidOperationException("Expected moving the F5 window without resizing to hit the page layout cache.");
            }

            if (moved.Viewport.X - first.Viewport.X != movedContent.X - content.X ||
                moved.Viewport.Y - first.Viewport.Y != movedContent.Y - content.Y ||
                moved.ScrollbarThumb.X - first.ScrollbarThumb.X != movedContent.X - content.X ||
                moved.ScrollbarThumb.Y - first.ScrollbarThumb.Y != movedContent.Y - content.Y)
            {
                throw new InvalidOperationException("Expected cached layout rectangles to translate with the current content origin.");
            }

            if (moved.PageStateSignature != first.PageStateSignature ||
                moved.ContentHeight != first.ContentHeight ||
                moved.ScrollOffset != first.ScrollOffset)
            {
                throw new InvalidOperationException("Expected window position to stay outside the page layout signature.");
            }
        }

        private static void LegacyUiPageLayoutCacheDirtiesOnScrollSizeAndState()
        {
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 128);

            var first = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("information", window, content, 0, settings);
            var scrolled = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("information", window, content, 64, settings);
            if (scrolled.RebuildCount <= first.RebuildCount || scrolled.ScrollOffset == first.ScrollOffset)
            {
                throw new InvalidOperationException("Expected scroll offset changes to dirty the page layout cache.");
            }

            var resizedContent = new LegacyUiRect(content.X, content.Y, content.Width + 16, content.Height);
            var resized = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("information", window, resizedContent, 64, settings);
            if (resized.RebuildCount <= scrolled.RebuildCount)
            {
                throw new InvalidOperationException("Expected content width changes to dirty the page layout cache.");
            }

            settings.InformationSignTextLabelsMode = InformationSignTextModes.Lines;
            var stateChanged = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("information", window, resizedContent, 64, settings);
            if (stateChanged.RebuildCount <= resized.RebuildCount ||
                stateChanged.PageStateSignature == resized.PageStateSignature)
            {
                throw new InvalidOperationException("Expected information page state changes to dirty the page layout cache.");
            }
        }

        private static void LegacyUiPageLayoutCacheDirtiesOnFontGeneration()
        {
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 128);

            var first = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("movement", window, content, 0, settings);
            UiTextRenderer.InvalidateCachedResources("legacy UI layout cache test");
            var fontInvalidated = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("movement", window, content, 0, settings);
            if (fontInvalidated.RebuildCount <= first.RebuildCount ||
                fontInvalidated.FontCacheGeneration == first.FontCacheGeneration)
            {
                throw new InvalidOperationException("Expected UI text renderer cache generation to dirty F5 page layout.");
            }
        }

        private static void LegacyUiHoverLayoutTokenIgnoresWindowAndContentPosition()
        {
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 180);
            var area = LegacyScrollArea.Create(content, 700, 24);
            var first = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("information", window, content, area, settings);

            var movedWindow = new LegacyUiRect(240, 170, window.Width, window.Height);
            var movedContent = new LegacyUiRect(258, 254, content.Width, content.Height);
            var movedArea = LegacyScrollArea.Create(movedContent, 700, 24);
            var moved = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("information", movedWindow, movedContent, movedArea, settings);

            if (moved != first)
            {
                throw new InvalidOperationException("Expected hover layout token to ignore window/content origin-only movement.");
            }
        }

        private static void LegacyUiHoverLayoutTokenDirtiesOnPageSizeScrollSettingsAndFont()
        {
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 180);
            var area = LegacyScrollArea.Create(content, 700, 24);
            var first = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("information", window, content, area, settings);

            var pageChanged = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("movement", window, content, area, settings);
            if (pageChanged == first)
            {
                throw new InvalidOperationException("Expected page changes to dirty the hover layout token.");
            }

            var resizedContent = new LegacyUiRect(content.X, content.Y, content.Width + 16, content.Height);
            var resizedArea = LegacyScrollArea.Create(resizedContent, 700, 24);
            var resized = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("information", window, resizedContent, resizedArea, settings);
            if (resized == first)
            {
                throw new InvalidOperationException("Expected content size changes to dirty the hover layout token.");
            }

            var scrolledArea = LegacyScrollArea.Create(content, 700, 80);
            var scrolled = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("information", window, content, scrolledArea, settings);
            if (scrolled == first)
            {
                throw new InvalidOperationException("Expected scroll changes to dirty the hover layout token.");
            }

            var configChanged = AppSettings.CreateDefault();
            configChanged.ConfigVersion = settings.ConfigVersion + 1;
            var configToken = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("information", window, content, area, configChanged);
            if (configToken == first)
            {
                throw new InvalidOperationException("Expected config version changes to dirty the hover layout token.");
            }

            var stateChanged = AppSettings.CreateDefault();
            stateChanged.InformationEnemyNameLabelsEnabled = !settings.InformationEnemyNameLabelsEnabled;
            var stateToken = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("information", window, content, area, stateChanged);
            if (stateToken == first)
            {
                throw new InvalidOperationException("Expected page state changes to dirty the hover layout token.");
            }

            UiTextRenderer.InvalidateCachedResources("legacy UI hover token test");
            var fontToken = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("information", window, content, area, settings);
            if (fontToken == first)
            {
                throw new InvalidOperationException("Expected UI font cache generation changes to dirty the hover layout token.");
            }
        }
    }
}
