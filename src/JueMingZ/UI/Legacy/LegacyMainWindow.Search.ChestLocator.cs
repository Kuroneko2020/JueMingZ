using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Search.ChestLocator;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const int SearchChestLocatorInputRowHeight = 36;
        private const int SearchChestLocatorLabelWidth = 96;
        private const int SearchChestLocatorSubmitWidth = 58;
        private const int SearchChestLocatorClearWidth = 48;
        private const int SearchChestLocatorControlGap = 6;
        private const int SearchChestLocatorInnerGap = 6;
        private const int SearchChestLocatorSummaryHeight = 58;
        private const int SearchChestLocatorCandidateRowHeight = 28;
        private const int SearchChestLocatorCandidateRowVisualGap = 3;
        private const int SearchChestLocatorMaxVisibleCandidates = 4;
        private const string SearchChestLocatorInputLabelText = "定位物品";
        private const string SearchChestLocatorSubmitButtonText = "定位";

        private static LegacyUiElement DrawSearchChestLocatorBlock(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY)
        {
            var hovered = (LegacyUiElement)null;
            var y = contentY;
            DrawSection(spriteBatch, area, y, "箱内物品定位");
            y += SearchSectionBodyOffset;

            hovered = DrawSearchChestLocatorInputRow(spriteBatch, area, mouse, elements, y) ?? hovered;
            y += SearchChestLocatorInputRowHeight + SearchChestLocatorInnerGap;

            var candidateHeight = CalculateSearchChestLocatorCandidateListHeight();
            if (candidateHeight > 0)
            {
                hovered = DrawSearchChestLocatorCandidateList(spriteBatch, area, mouse, elements, y) ?? hovered;
                y += candidateHeight + SearchChestLocatorInnerGap;
            }

            hovered = DrawSearchChestLocatorSummaryPanel(spriteBatch, area, y) ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawSearchChestLocatorInputRow(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, SearchChestLocatorInputRowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var hovered = (LegacyUiElement)null;
            var inputFocused = LegacyTextInput.IsFocused(SearchChestLocatorUiState.InputId);
            if (inputFocused)
            {
                LegacyTextInput.Update(SearchChestLocatorUiState.InputId);
                SearchChestLocatorUiState.UpdateDraft(LegacyTextInput.GetDraft(SearchChestLocatorUiState.InputId));
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                SearchChestLocatorInputLabelText,
                row.X + 10,
                row.Y,
                SearchChestLocatorLabelWidth - 16,
                row.Height,
                UiTextHorizontalAlignment.Left,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                238,
                238,
                226,
                255,
                0.82f);

            var inputWidth = Math.Max(
                120,
                row.Width - SearchChestLocatorLabelWidth - SearchChestLocatorSubmitWidth - SearchChestLocatorClearWidth - SearchChestLocatorControlGap * 3);
            var inputRect = new LegacyUiRect(row.X + SearchChestLocatorLabelWidth, row.Y + 4, inputWidth, SearchChestLocatorInputRowHeight - 8);
            var inputHit = inputRect.Intersect(area.Viewport);
            var inputElementRect = inputHit.Width > 0 && inputHit.Height > 0 ? inputHit : inputRect;
            var inputHovered = IsFrameElementHovered(SearchChestLocatorUiState.InputId, inputElementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, inputRect, inputHovered, inputHovered && mouse.LeftDown, inputFocused, true, area.Viewport);
            var inputContentRect = LegacyUiTheme.GetSelectedButtonContentRect(inputRect, inputFocused, true);
            var inputText = inputFocused
                ? LegacyTextInput.GetDisplayText(SearchChestLocatorUiState.InputId, "物品名 / #ID")
                : FirstNonEmpty(SearchChestLocatorUiState.QueryText, "物品名 / #ID");
            var muted = !inputFocused && string.IsNullOrWhiteSpace(SearchChestLocatorUiState.QueryText);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                inputText,
                inputRect.X + 8,
                inputContentRect.Y + 4,
                inputRect.Width - 16,
                Math.Max(1, inputContentRect.Height - 8),
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                muted ? 184 : 244,
                muted ? 194 : 240,
                muted ? 208 : 218,
                255,
                0.70f);
            var inputElement = AddFrameElement(
                elements,
                SearchChestLocatorUiState.InputId,
                "箱内定位:输入",
                "button",
                inputElementRect,
                selected: inputFocused,
                tooltipLines: new[] { "点击输入物品名、内部名或 #ID" });
            RecordFrameElementHover(inputElement, inputHovered);
            if (inputHovered)
            {
                hovered = inputElement;
            }

            var submitRect = new LegacyUiRect(inputRect.Right + SearchChestLocatorControlGap, inputRect.Y, SearchChestLocatorSubmitWidth, inputRect.Height);
            var submitHit = submitRect.Intersect(area.Viewport);
            var submitElementRect = submitHit.Width > 0 && submitHit.Height > 0 ? submitHit : submitRect;
            var submitEnabled = !string.IsNullOrWhiteSpace(SearchChestLocatorUiState.QueryText);
            var submitHovered = IsFrameElementHovered(SearchChestLocatorUiState.SubmitButtonId, submitElementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, submitRect, submitHovered, submitHovered && mouse.LeftDown, false, submitEnabled, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                SearchChestLocatorSubmitButtonText,
                submitRect.X + 4,
                submitRect.Y,
                submitRect.Width - 8,
                submitRect.Height,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                submitEnabled ? 230 : 150,
                submitEnabled ? 232 : 156,
                submitEnabled ? 224 : 170,
                255,
                0.66f);
            var submitElement = AddFrameElement(
                elements,
                SearchChestLocatorUiState.SubmitButtonId,
                "箱内定位:定位",
                "button",
                submitElementRect,
                enabled: submitEnabled,
                tooltipLines: new[] { "扫描附近已同步箱子" });
            RecordFrameElementHover(submitElement, submitHovered);
            if (submitHovered)
            {
                hovered = submitElement;
            }

            var clearRect = new LegacyUiRect(submitRect.Right + SearchChestLocatorControlGap, inputRect.Y, SearchChestLocatorClearWidth, inputRect.Height);
            var clearHit = clearRect.Intersect(area.Viewport);
            var clearElementRect = clearHit.Width > 0 && clearHit.Height > 0 ? clearHit : clearRect;
            var clearEnabled = SearchChestLocatorUiState.HasAnyState;
            var clearHovered = IsFrameElementHovered(SearchChestLocatorUiState.ClearButtonId, clearElementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, clearRect, clearHovered, clearHovered && mouse.LeftDown, false, clearEnabled, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                "清空",
                clearRect.X + 4,
                clearRect.Y,
                clearRect.Width - 8,
                clearRect.Height,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                clearEnabled ? 230 : 150,
                clearEnabled ? 232 : 156,
                clearEnabled ? 224 : 170,
                255,
                0.66f);
            var clearElement = AddFrameElement(
                elements,
                SearchChestLocatorUiState.ClearButtonId,
                "箱内定位:清空",
                "button",
                clearElementRect,
                enabled: clearEnabled,
                tooltipLines: new[] { "清空定位查询和命中快照" });
            RecordFrameElementHover(clearElement, clearHovered);
            return clearHovered ? clearElement : hovered;
        }

        private static LegacyUiElement DrawSearchChestLocatorCandidateList(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY)
        {
            var candidates = SearchChestLocatorUiState.GetCandidates();
            var count = Math.Min(candidates.Count, SearchChestLocatorMaxVisibleCandidates);
            var hovered = (LegacyUiElement)null;
            for (var index = 0; index < count; index++)
            {
                var row = new LegacyUiRect(
                    area.Viewport.X,
                    area.ToScreenY(contentY + index * SearchChestLocatorCandidateRowHeight),
                    area.Viewport.Width,
                    SearchChestLocatorCandidateRowHeight - SearchChestLocatorCandidateRowVisualGap);
                if (!area.IsVisible(row))
                {
                    continue;
                }

                hovered = DrawSearchChestLocatorCandidateRow(spriteBatch, area, mouse, elements, row, candidates[index]) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawSearchChestLocatorCandidateRow(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect row,
            ChestItemLocatorCandidate candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            var hit = row.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : row;
            var elementId = SearchChestLocatorUiState.CandidateElementPrefix + candidate.ItemType.ToString(CultureInfo.InvariantCulture);
            var hovered = IsFrameElementHovered(elementId, elementRect, mouse);
            var selected = SearchChestLocatorUiState.SelectedItemType == candidate.ItemType;
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            DrawSearchItemIcon(spriteBatch, new LegacyUiRect(row.X + 7, row.Y + 3, 20, 20), area.Viewport, candidate.ItemType);
            var label = FirstNonEmpty3(candidate.DisplayName, candidate.InternalName, candidate.IdText);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                label,
                row.X + 34,
                row.Y + 5,
                Math.Max(1, row.Width - 108),
                16,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                selected ? LegacyUiTheme.SelectedTextR : 236,
                selected ? LegacyUiTheme.SelectedTextG : 236,
                selected ? LegacyUiTheme.SelectedTextB : 224,
                248,
                0.60f);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                candidate.IdText,
                row.Right - 64,
                row.Y + 5,
                54,
                16,
                UiTextHorizontalAlignment.Right,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                206,
                218,
                236,
                230,
                0.54f);
            var element = AddFrameElement(
                elements,
                elementId,
                "定位 " + label,
                "button",
                elementRect,
                selected: selected,
                tooltipLines: new[] { "定位：" + label + " " + candidate.IdText });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawSearchChestLocatorSummaryPanel(object spriteBatch, LegacyScrollArea area, int contentY)
        {
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, SearchChestLocatorSummaryHeight);
            if (!area.IsVisible(rect))
            {
                return null;
            }

            var status = FirstNonEmpty(SearchChestLocatorUiState.StatusMessage, "暂无扫描结果。");
            var detail = FirstNonEmpty3(SearchChestLocatorUiState.DegradeMessage, SearchChestLocatorUiState.CandidateMessage, "暂无扫描结果。");
            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                status,
                rect.X + 12,
                rect.Y + 9,
                rect.Width - 24,
                20,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                238,
                238,
                226,
                255,
                0.70f);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                detail,
                rect.X + 12,
                rect.Y + 33,
                rect.Width - 24,
                18,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                202,
                214,
                232,
                230,
                0.58f);
            return null;
        }

        private static int CalculateSearchChestLocatorBlockHeight(int viewportWidth)
        {
            var candidateHeight = CalculateSearchChestLocatorCandidateListHeight();
            return SearchSectionBodyOffset +
                   SearchChestLocatorInputRowHeight +
                   SearchChestLocatorInnerGap +
                   candidateHeight +
                   (candidateHeight > 0 ? SearchChestLocatorInnerGap : 0) +
                   SearchChestLocatorSummaryHeight;
        }

        private static int CalculateSearchChestLocatorCandidateListHeight()
        {
            return Math.Min(SearchChestLocatorUiState.CandidateCount, SearchChestLocatorMaxVisibleCandidates) * SearchChestLocatorCandidateRowHeight;
        }

        internal static int CalculateSearchChestLocatorBlockHeightForTesting(int viewportWidth)
        {
            return CalculateSearchChestLocatorBlockHeight(viewportWidth);
        }

        internal static string[] GetSearchChestLocatorInputRowTextForTesting()
        {
            return new[] { SearchChestLocatorInputLabelText, SearchChestLocatorSubmitButtonText, "清空" };
        }

        internal static string[] GetSearchPageBlockOrderForTesting()
        {
            return new[] { "chestLocator", "querySearch" };
        }

        private static string FirstNonEmpty3(string first, string second, string third)
        {
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first.Trim();
            }

            if (!string.IsNullOrWhiteSpace(second))
            {
                return second.Trim();
            }

            return third ?? string.Empty;
        }
    }
}
