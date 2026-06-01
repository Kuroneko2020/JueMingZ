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
        private static LegacyUiElement DrawFishingFilterKeywordListContent(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, AppSettings settings, string filterMode)
        {
            var hovered = (LegacyUiElement)null;
            var contentTop = rect.Y + 8;
            var contentBottom = rect.Bottom - 8;
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                LegacyTextInput.ClearFocus();
                FishingFilterUiState.ClearPresetViewport();
                FishingFilterUiState.ClearEntryViewport();
                UiTextRenderer.DrawTextClipped(spriteBatch, "过滤未启用，请选择白名单或黑名单后编辑关键词。", rect.X + 10, contentTop + 8, rect.Width - 20, 44, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 226, 218, 194, 240, 0.70f);
                return hovered;
            }

            LegacyUiRect presetListAnchor;
            hovered = DrawFishingFilterKeywordActions(spriteBatch, area, mouse, elements, rect, contentTop, out presetListAnchor) ?? hovered;
            contentTop += FishingFilterActionButtonHeight + 8;
            var inputDiagnostic = LegacyTextInput.IsFocused(FishingFilterUiState.KeywordInputId) ? LegacyTextInput.DiagnosticMessage : string.Empty;
            if (!string.IsNullOrWhiteSpace(inputDiagnostic))
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, inputDiagnostic, rect.X + 10, contentTop, rect.Width - 20, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 196, 180, 230, 0.56f);
                contentTop += 20;
            }

            var entriesRect = new LegacyUiRect(rect.X + 10, contentTop, rect.Width - 20, Math.Max(0, contentBottom - contentTop));
            hovered = DrawFishingFilterKeywordEntries(spriteBatch, area, mouse, elements, entriesRect, settings, filterMode) ?? hovered;

            if (FishingFilterUiState.PresetListOpen)
            {
                var presetHeight = BuildFishingFilterFloatingHeight(area, presetListAnchor, FishingFilterPresetMaxHeight, 78);
                if (presetHeight > 0)
                {
                    var presetRect = BuildFishingFilterFloatingRect(area, rect, presetListAnchor, presetHeight);
                    DrawFishingFilterFloatingConnector(spriteBatch, area, presetRect, presetListAnchor);
                    hovered = DrawFishingFilterPresetList(spriteBatch, area, mouse, elements, presetRect, settings, filterMode, FishingFilterMatchModes.Keyword) ?? hovered;
                }
            }
            else
            {
                FishingFilterUiState.ClearPresetViewport();
            }

            return hovered;
        }

        private static LegacyUiElement DrawFishingFilterKeywordActions(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, int y, out LegacyUiRect presetListAnchor)
        {
            var totalWidth = Math.Max(1, rect.Width - 20);
            var buttonGap = GetFishingFilterActionButtonGap(totalWidth);
            var width = Math.Max(1, (totalWidth - buttonGap * 2) / 3);
            var x = rect.X + 10;
            presetListAnchor = new LegacyUiRect(x, y, 1, FishingFilterActionButtonHeight);
            if (LegacyTextInput.IsFocused(FishingFilterUiState.PresetNameInputId))
            {
                LegacyTextInput.Update(FishingFilterUiState.PresetNameInputId);
                return DrawFishingFilterPresetNameActions(spriteBatch, area, mouse, elements, new LegacyUiRect(x, y, totalWidth, FishingFilterActionButtonHeight));
            }

            if (!LegacyTextInput.IsFocused(FishingFilterUiState.KeywordInputId))
            {
                var actionsHovered = (LegacyUiElement)null;
                var buttonWidths = BuildFishingFilterActionButtonWidths(totalWidth, buttonGap, new[] { "+", "清空", "保存预设", "预设列表" });
                var plusRect = new LegacyUiRect(x, y, buttonWidths[0], FishingFilterActionButtonHeight);
                var clearRect = new LegacyUiRect(plusRect.Right + buttonGap, y, buttonWidths[1], FishingFilterActionButtonHeight);
                var presetSaveRect = new LegacyUiRect(clearRect.Right + buttonGap, y, buttonWidths[2], FishingFilterActionButtonHeight);
                presetListAnchor = new LegacyUiRect(presetSaveRect.Right + buttonGap, y, buttonWidths[3], FishingFilterActionButtonHeight);
                actionsHovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, plusRect, "fishing-filter-keyword:add-start", "+", false, true, "添加关键词。关键词按当前显示名匹配，会受语言和材质包影响。") ?? actionsHovered;
                actionsHovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, clearRect, "fishing-filter-list:clear", "清空", false, true, null) ?? actionsHovered;
                actionsHovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, presetSaveRect, "fishing-filter-preset:save-start", "保存预设", false, true, null) ?? actionsHovered;
                actionsHovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, presetListAnchor, "fishing-filter-preset:list-toggle", "预设列表", FishingFilterUiState.PresetListOpen, true, null) ?? actionsHovered;
                DrawFishingFilterPresetSaveNotice(spriteBatch, area, presetSaveRect);
                return actionsHovered;
            }

            LegacyTextInput.Update(FishingFilterUiState.KeywordInputId);
            var confirmWidth = Math.Max(42, Math.Min(56, width / 2));
            var cancelWidth = confirmWidth;
            var inputWidth = Math.Max(80, totalWidth - buttonGap * 2 - confirmWidth - cancelWidth);
            var inputRect = new LegacyUiRect(x, y, inputWidth, FishingFilterActionButtonHeight);
            DrawFishingFilterKeywordInput(spriteBatch, area, elements, inputRect);
            var hovered = (LegacyUiElement)null;
            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, new LegacyUiRect(inputRect.Right + buttonGap, y, confirmWidth, FishingFilterActionButtonHeight), "fishing-filter-keyword:confirm", "确认", false, true, "把输入内容加入当前关键词名单。") ?? hovered;
            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, new LegacyUiRect(inputRect.Right + buttonGap + confirmWidth + buttonGap, y, cancelWidth, FishingFilterActionButtonHeight), "fishing-filter-keyword:cancel", "取消", false, true, "取消关键词输入。") ?? hovered;
            return hovered;
        }

        private static void DrawFishingFilterKeywordInput(object spriteBatch, LegacyScrollArea area, List<LegacyUiElement> elements, LegacyUiRect rect)
        {
            var hit = rect.Intersect(area.Viewport);
            LegacyUiTheme.DrawRowClipped(spriteBatch, rect, area.Viewport);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 1, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 218, 198, 128, 230);
            var text = LegacyTextInput.GetDisplayText(FishingFilterUiState.KeywordInputId, "输入关键词");
            if (string.IsNullOrEmpty(text))
            {
                text = "输入关键词";
            }

            UiTextRenderer.DrawTextClipped(spriteBatch, text, rect.X + 8, rect.Y + 7, rect.Width - 16, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.68f);
            var message = LegacyTextInput.DiagnosticMessage;
            var tooltip = string.IsNullOrWhiteSpace(message)
                ? "关键词按当前显示名匹配，会受语言和材质包影响。"
                : message;
            AddFrameElement(elements, FishingFilterUiState.KeywordInputId, "关键词输入", "blocker", hit.Width > 0 && hit.Height > 0 ? hit : rect, tooltipLines: new[] { tooltip });
        }

        private static LegacyUiElement DrawFishingFilterKeywordEntries(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, AppSettings settings, string filterMode)
        {
            if (rect.Height <= 0)
            {
                FishingFilterUiState.ClearEntryViewport();
                return null;
            }

            var keywords = ResolveActiveKeywords(settings, filterMode);
            if (keywords == null || keywords.Count <= 0)
            {
                FishingFilterUiState.SetEntryViewport(rect, rect.Height);
                UiTextRenderer.DrawTextClipped(spriteBatch, "暂无条目", rect.X + 2, rect.Y + 4, rect.Width - 4, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 202, 214, 232, 215, 0.72f);
                UiTextRenderer.DrawTextClipped(spriteBatch, "关键词按当前显示名匹配，会受语言和材质包影响。", rect.X + 2, rect.Y + 28, rect.Width - 4, 28, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 202, 214, 232, 195, 0.60f);
                return null;
            }

            var visibleKeywords = new List<string>();
            var visibleKeywordIndexes = new List<int>();
            for (var index = 0; index < keywords.Count; index++)
            {
                var keyword = keywords[index];
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    visibleKeywords.Add(keyword);
                    visibleKeywordIndexes.Add(index);
                }
            }

            if (visibleKeywords.Count <= 0)
            {
                FishingFilterUiState.SetEntryViewport(rect, rect.Height);
                UiTextRenderer.DrawTextClipped(spriteBatch, "暂无条目", rect.X + 2, rect.Y + 4, rect.Width - 4, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 202, 214, 232, 215, 0.72f);
                return null;
            }

            var hovered = (LegacyUiElement)null;
            var clip = rect.Intersect(area.Viewport);
            var columnWidth = Math.Max(72, (rect.Width - FishingFilterEntryColumnGap) / 2);
            var rows = (visibleKeywords.Count + 1) / 2;
            var rowStep = FishingFilterKeywordEntryHeight + 4;
            var contentHeight = rows * rowStep - 4;
            FishingFilterUiState.SetEntryViewport(rect, contentHeight);
            var scrollOffset = FishingFilterUiState.EntryScrollOffset;
            for (var index = 0; index < visibleKeywords.Count; index++)
            {
                var keyword = visibleKeywords[index];
                var originalIndex = visibleKeywordIndexes[index];
                var rowIndex = index / 2;
                var columnIndex = index % 2;
                var row = new LegacyUiRect(
                    rect.X + columnIndex * (columnWidth + FishingFilterEntryColumnGap),
                    rect.Y + rowIndex * rowStep - scrollOffset,
                    columnWidth,
                    FishingFilterKeywordEntryHeight);
                if (row.Y >= rect.Bottom)
                {
                    break;
                }

                if (!row.Intersects(clip))
                {
                    continue;
                }

                hovered = DrawFishingFilterKeywordEntry(spriteBatch, mouse, elements, row, clip, keyword, originalIndex) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawFishingFilterKeywordEntry(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect row, LegacyUiRect clip, string keyword, int index)
        {
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, clip);
            var deleteRect = new LegacyUiRect(row.Right - 25, row.Y + 4, 20, 20);
            UiTextRenderer.DrawTextClipped(spriteBatch, keyword, row.X + 9, row.Y + 7, Math.Max(1, deleteRect.X - row.X - 15), 16, clip.X, clip.Y, clip.Width, clip.Height, 238, 236, 220, 248, 0.66f);
            var elementId = "fishing-filter-keyword:delete:" + index.ToString(CultureInfo.InvariantCulture);
            var elementRect = deleteRect.Intersect(clip);
            var deleteHovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, deleteRect, deleteHovered, deleteHovered && mouse.LeftDown, false, true, clip);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "x", deleteRect.X + 2, deleteRect.Y + 1, deleteRect.Width - 4, deleteRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 238, 196, 180, 255, 0.74f);

            var element = AddFrameElement(elements, elementId, "删除 " + keyword, "button", elementRect, tooltipLines: new[] { "删除关键词：" + keyword, "关键词按当前显示名匹配，会受语言和材质包影响。" });
            RecordFrameElementHover(element, deleteHovered);
            return deleteHovered ? element : null;
        }

        private static IList<string> ResolveActiveKeywords(AppSettings settings, string filterMode)
        {
            if (settings == null ||
                string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                ? settings.FishingFilterDenyKeywords
                : settings.FishingFilterAllowKeywords;
        }
    }
}
