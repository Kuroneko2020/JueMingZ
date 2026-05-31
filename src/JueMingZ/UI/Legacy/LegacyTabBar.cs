using System;
using System.Collections.Generic;

namespace JueMingZ.UI.Legacy
{
    public static class LegacyTabBar
    {
        public static readonly LegacyTabDefinition[] Tabs = new[]
        {
            new LegacyTabDefinition("home", "首页", "home", 0, 0),
            new LegacyTabDefinition("misc", "杂项", "grid", 0, 1),
            new LegacyTabDefinition("map_enhancement", "地图", "map_pin", 0, 2),
            new LegacyTabDefinition("search", "查询", "search", 0, 3),
            new LegacyTabDefinition("hotkeys", "按键", "keyboard", 0, 4),
            new LegacyTabDefinition("blueprint", "蓝图", "blueprint", 0, 5),
            new LegacyTabDefinition("about", "关于", "info", 1, 0),
            new LegacyTabDefinition("fishing", "钓鱼", "fish", 1, 1),
            new LegacyTabDefinition("combat", "战斗", "sword", 1, 2),
            new LegacyTabDefinition("information", "信息", "status_panel", 1, 3),
            new LegacyTabDefinition("buff", "增益", "flask", 1, 4),
            new LegacyTabDefinition("movement", "移动", "movement", 1, 5)
        };

        public static List<LegacyUiElement> Build(LegacyUiRect windowRect, string selectedPageId)
        {
            var elements = new List<LegacyUiElement>(Tabs.Length);
            for (var index = 0; index < Tabs.Length; index++)
            {
                var tab = Tabs[index];
                elements.Add(new LegacyUiElement
                {
                    Id = "tab:" + tab.Id,
                    Label = tab.DisplayName,
                    Kind = "tab",
                    Rect = GetTabRect(windowRect, index),
                    Selected = tab.Id == selectedPageId
                });
            }

            return elements;
        }

        public static LegacyUiRect GetTabRect(LegacyUiRect windowRect, int tabIndex)
        {
            var x = windowRect.X + LegacyUiMetrics.OuterPadding;
            var y = windowRect.Y + LegacyUiMetrics.TitleHeight + LegacyUiMetrics.OuterPadding + LegacyUiMetrics.TabBlockYOffset;
            var availableWidth = windowRect.Width - LegacyUiMetrics.OuterPadding * 2;
            var columns = GetColumnCount(windowRect.Width);
            var tabWidth = (availableWidth - LegacyUiMetrics.TabButtonGap * Math.Max(0, columns - 1)) / columns;
            var row = tabIndex / columns;
            var column = tabIndex % columns;
            return new LegacyUiRect(
                x + column * (tabWidth + LegacyUiMetrics.TabButtonGap),
                y + row * (LegacyUiMetrics.TabButtonHeight + LegacyUiMetrics.TabRowGap),
                tabWidth,
                LegacyUiMetrics.TabButtonHeight);
        }

        public static int GetBlockHeight(int windowWidth)
        {
            var rows = GetRowCount(windowWidth);
            return rows * LegacyUiMetrics.TabButtonHeight + Math.Max(0, rows - 1) * LegacyUiMetrics.TabRowGap;
        }

        private static int GetRowCount(int windowWidth)
        {
            var columns = GetColumnCount(windowWidth);
            return (Tabs.Length + columns - 1) / columns;
        }

        private static int GetColumnCount(int windowWidth)
        {
            return 6;
        }

        public static string GetDisplayName(string pageId)
        {
            for (var index = 0; index < Tabs.Length; index++)
            {
                if (Tabs[index].Id == pageId)
                {
                    return Tabs[index].DisplayName;
                }
            }

            return "增益";
        }

        public static string GetIconIdFromElementId(string elementId)
        {
            if (string.IsNullOrWhiteSpace(elementId))
            {
                return string.Empty;
            }

            const string prefix = "tab:";
            var pageId = elementId.StartsWith(prefix, StringComparison.Ordinal)
                ? elementId.Substring(prefix.Length)
                : elementId;
            return GetIconId(pageId);
        }

        public static string GetIconId(string pageId)
        {
            for (var index = 0; index < Tabs.Length; index++)
            {
                if (Tabs[index].Id == pageId)
                {
                    return Tabs[index].IconId;
                }
            }

            return string.Empty;
        }
    }

    public sealed class LegacyTabDefinition
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string IconId { get; private set; }
        public int Row { get; private set; }
        public int Column { get; private set; }

        public LegacyTabDefinition(string id, string displayName, string iconId, int row, int column)
        {
            Id = id;
            DisplayName = displayName;
            IconId = iconId;
            Row = row;
            Column = column;
        }
    }
}
