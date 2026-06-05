using System;
using System.Collections.Generic;
using JueMingZ.Automation.Information;
using JueMingZ.Config;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using JueMingZ.UI.Legacy.Framework;

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

        private static void LegacyPotionGridFitsSixButtonsPerDefaultBuffPane()
        {
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var shell = LegacyMainWindowShell.Create(window);
            var viewportWidth = shell.ContentRect.Width - LegacyUiMetrics.ContentPadding * 2 - LegacyUiMetrics.ScrollbarWidth - 8;
            var paneGap = LegacyUiMetrics.GridCellGap * 2;
            var paneWidth = Math.Max(220, (viewportWidth - paneGap) / 2);
            var innerWidth = Math.Max(1, paneWidth - LegacyUiMetrics.GridPanePadding * 2);
            var columns = Math.Max(1, (innerWidth + LegacyUiMetrics.GridCellGap) / (LegacyPotionGrid.CellWidth + LegacyUiMetrics.GridCellGap));

            if (columns < 6)
            {
                throw new InvalidOperationException("Expected the default auto buff pane to fit at least six potion buttons per row, got " + columns + ".");
            }
        }

        private static void LegacyMiscContentHeightIncludesBottomActionRows()
        {
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var hotkeys = ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault();
            var originalAutoSellIds = settings.InventoryAutoSellItemIds;
            var originalAutoDiscardIds = settings.InventoryAutoDiscardItemIds;
            var originalQuickReforgePrefixes = settings.NpcAutoReforgePrefixes;
            var originalQuickItemBindings = hotkeys.QuickItemHotkeyBindings;

            try
            {
                settings.InventoryAutoSellItemIds = new List<int>();
                settings.InventoryAutoDiscardItemIds = new List<int>();
                settings.NpcAutoReforgePrefixes = new List<string>();
                hotkeys.QuickItemHotkeyBindings = new List<QuickItemHotkeyBinding>();

                var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
                var shell = LegacyMainWindowShell.Create(window);
                var content = shell.ContentRect;
                var expectedHeight =
                    (LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap) * 4 +
                    LegacyUiMetrics.RowHeight * 12 +
                    LegacyUiMetrics.SettingRowGap * 11 +
                    24;
                var actualHeight = LegacyMainWindow.CalculateMiscContentHeightForTesting(content);

                if (actualHeight != expectedHeight)
                {
                    throw new InvalidOperationException("Expected default misc content height " + expectedHeight + ", got " + actualHeight + ".");
                }

                var snapshot = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("misc", window, content, 99999, settings);
                var finalRowTop = expectedHeight - 24 - LegacyUiMetrics.RowHeight;
                var finalRowBottomAtMaxScroll = snapshot.Viewport.Y + finalRowTop - snapshot.ScrollOffset + LegacyUiMetrics.RowHeight;
                if (finalRowBottomAtMaxScroll > snapshot.Viewport.Bottom)
                {
                    throw new InvalidOperationException("Expected the bottom misc action row to be fully visible at max scroll.");
                }
            }
            finally
            {
                settings.InventoryAutoSellItemIds = originalAutoSellIds;
                settings.InventoryAutoDiscardItemIds = originalAutoDiscardIds;
                settings.NpcAutoReforgePrefixes = originalQuickReforgePrefixes;
                hotkeys.QuickItemHotkeyBindings = originalQuickItemBindings;
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

        private static void LegacyUiTabsIgnoreContentScrollClip()
        {
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var shell = LegacyMainWindowShell.Create(window);
            var area = LegacyScrollArea.Create(shell.ContentRect, 700, 0);
            var elements = new List<LegacyUiElement>();
            var context = new LegacyUiContext(null, null, window, "buff", settings, elements);
            context.SetContentRect(shell.ContentRect);
            context.SetScrollArea(area);

            var firstTab = LegacyTabBar.GetTabRect(window, 0);
            if (context.IsRectVisible(firstTab))
            {
                throw new InvalidOperationException("Expected the content scroll clip to exclude the F5 tab bar.");
            }

            var added = LegacyMainWindow.DrawTabsForTesting(context);
            if (added != LegacyTabBar.Tabs.Length || elements.Count != LegacyTabBar.Tabs.Length)
            {
                throw new InvalidOperationException("Expected F5 tabs to register outside the content scroll clip.");
            }

            if (!string.Equals(elements[0].Id, "tab:home", StringComparison.Ordinal) ||
                !string.Equals(elements[elements.Count - 1].Id, "tab:movement", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the registered F5 tab sequence to remain intact.");
            }

            if (!context.HasClip || context.ClipRect.Y != area.Viewport.Y)
            {
                throw new InvalidOperationException("Expected drawing tabs to leave the caller content clip unchanged.");
            }
        }

        private static void LegacyUiSelectedButtonContentOffsetRequiresEnabledSelection()
        {
            var rect = new LegacyUiRect(10, 20, 60, 24);
            var selected = LegacyUiTheme.GetSelectedButtonContentRect(rect, true, true);
            if (selected.X != rect.X ||
                selected.Y != rect.Y + 1 ||
                selected.Width != rect.Width ||
                selected.Height != rect.Height - 1)
            {
                throw new InvalidOperationException("Expected enabled selected button content to sink by one pixel.");
            }

            var unselected = LegacyUiTheme.GetSelectedButtonContentRect(rect, false, true);
            if (unselected.X != rect.X ||
                unselected.Y != rect.Y ||
                unselected.Width != rect.Width ||
                unselected.Height != rect.Height)
            {
                throw new InvalidOperationException("Expected unselected button content to keep its original bounds.");
            }

            var disabled = LegacyUiTheme.GetSelectedButtonContentRect(rect, true, false);
            if (disabled.X != rect.X ||
                disabled.Y != rect.Y ||
                disabled.Width != rect.Width ||
                disabled.Height != rect.Height)
            {
                throw new InvalidOperationException("Expected disabled selected-looking button content to keep its original bounds.");
            }
        }

        private static void LegacyUiRetainedFrameModelReusesWindowTranslation()
        {
            LegacyMainWindow.ResetRetainedFrameModelForTesting();
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 180);
            var area = LegacyScrollArea.Create(content, 700, 24);
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("tab:buff", "增益", "tab", new LegacyUiRect(58, 88, 84, 31)),
                CreateLegacyUiElementForTesting("auto-buff-mode:On", "自动增益:开启", "button", new LegacyUiRect(420, 150, 64, 24)),
                CreateLegacyUiElementForTesting("candidate:299", "铁皮药水", "candidate", new LegacyUiRect(66, 220, 36, 36))
            };

            var first = LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting("buff", window, content, area, settings, 700, elements);
            if (first.CacheMissCount != 1 || first.CacheHitCount != 0 || first.VisibleElementCount != elements.Count)
            {
                throw new InvalidOperationException("Expected the first retained frame model build to miss and capture the visible elements.");
            }

            var movedWindow = new LegacyUiRect(240, 170, window.Width, window.Height);
            var movedContent = new LegacyUiRect(258, 254, content.Width, content.Height);
            var movedArea = LegacyScrollArea.Create(movedContent, 700, 24);
            var movedElements = OffsetElementsForTesting(elements, movedWindow.X - window.X, movedWindow.Y - window.Y);
            var moved = LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting("buff", movedWindow, movedContent, movedArea, settings, 700, movedElements);
            if (moved.CacheHitCount != 1 || moved.CacheMissCount != 1 || moved.FallbackCount != 0)
            {
                throw new InvalidOperationException("Expected moving the F5 window without resizing or scrolling to hit the retained frame model.");
            }

            if (moved.FirstElementRect.X != movedElements[0].Rect.X ||
                moved.FirstElementRect.Y != movedElements[0].Rect.Y ||
                !string.Equals(moved.FirstElementId, "tab:buff", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected retained frame elements to translate with the moved window.");
            }
        }

        private static void LegacyUiRetainedFrameModelDirtiesOnScrollSettingsAndFont()
        {
            LegacyMainWindow.ResetRetainedFrameModelForTesting();
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 180);
            var area = LegacyScrollArea.Create(content, 700, 24);
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("tab:buff", "增益", "tab", new LegacyUiRect(58, 88, 84, 31))
            };

            var first = LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting("buff", window, content, area, settings, 700, elements);
            var scrolledArea = LegacyScrollArea.Create(content, 700, 80);
            var scrolledElements = OffsetElementsForTesting(elements, 0, -56);
            var scrolled = LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting("buff", window, content, scrolledArea, settings, 700, scrolledElements);
            if (scrolled.CacheMissCount <= first.CacheMissCount || scrolled.CacheHitCount != first.CacheHitCount)
            {
                throw new InvalidOperationException("Expected scroll changes to dirty the retained frame model.");
            }

            var changedSettings = AppSettings.CreateDefault();
            changedSettings.ConfigVersion = settings.ConfigVersion + 1;
            var configChanged = LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting("buff", window, content, area, changedSettings, 700, elements);
            if (configChanged.CacheMissCount <= scrolled.CacheMissCount)
            {
                throw new InvalidOperationException("Expected settings version changes to dirty the retained frame model.");
            }

            UiTextRenderer.InvalidateCachedResources("legacy UI retained frame model test");
            var fontChanged = LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting("buff", window, content, area, changedSettings, 700, elements);
            if (fontChanged.CacheMissCount <= configChanged.CacheMissCount)
            {
                throw new InvalidOperationException("Expected UI font cache generation changes to dirty the retained frame model.");
            }
        }

        private static void LegacyUiRetainedFrameModelFallsBackOnElementMismatch()
        {
            LegacyMainWindow.ResetRetainedFrameModelForTesting();
            LegacyMainWindow.ResetPageLayoutCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 180);
            var area = LegacyScrollArea.Create(content, 700, 24);
            var firstElements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("tab:buff", "增益", "tab", new LegacyUiRect(58, 88, 84, 31))
            };
            LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting("buff", window, content, area, settings, 700, firstElements);

            var changedElements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("tab:misc", "杂项", "tab", new LegacyUiRect(58, 88, 84, 31))
            };
            var changed = LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting("buff", window, content, area, settings, 700, changedElements);
            if (changed.CacheHitCount != 1 || changed.FallbackCount != 1 || changed.VisibleElementCount != 1)
            {
                throw new InvalidOperationException("Expected retained frame model replay to fall back when the element sequence changes under the same signature.");
            }
        }

        private static LegacyUiElement CreateLegacyUiElementForTesting(string id, string label, string kind, LegacyUiRect rect)
        {
            var element = new LegacyUiElement();
            element.Reset(id, label, kind, rect, true, false, 0, 0, 0, null, null, null);
            return element;
        }

        private static List<LegacyUiElement> OffsetElementsForTesting(IList<LegacyUiElement> elements, int offsetX, int offsetY)
        {
            var moved = new List<LegacyUiElement>();
            if (elements == null)
            {
                return moved;
            }

            for (var index = 0; index < elements.Count; index++)
            {
                var element = elements[index];
                if (element == null)
                {
                    continue;
                }

                moved.Add(CreateLegacyUiElementForTesting(
                    element.Id,
                    element.Label,
                    element.Kind,
                    new LegacyUiRect(element.Rect.X + offsetX, element.Rect.Y + offsetY, element.Rect.Width, element.Rect.Height)));
            }

            return moved;
        }
    }
}
