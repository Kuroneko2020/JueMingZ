using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Search;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const int SearchInputRowHeight = 36;
        private const int SearchInputLabelWidth = 84;
        private const int SearchInputPickWidth = 92;
        private const int SearchInputClearWidth = 48;
        private const int SearchInputControlGap = 6;
        private const string SearchInputLabelText = "查询物品";
        private const string SearchPickButtonText = "选择物品";
        private const string SearchPickButtonTooltipText = "点击需要查询的物品";
        private const string SearchAcquisitionSectionTitle = "获取来源（仅供参考，不包准确全面）";
        private const int SearchCandidatePopupMinHeight = 78;
        private const int SearchCandidatePopupMaxHeight = 226;
        private const int SearchCandidatePopupMinWidth = 220;
        private const int SearchCandidatePopupPadding = 8;
        private const int SearchCandidatePopupEdgeMargin = 4;
        private const int SearchCandidatePopupAnchorGap = 6;
        private const int SearchCandidatePopupVisibleRows = 6;
        private const int SearchCandidatePopupVerticalPadding = 10;
        private const int SearchCandidateHeaderHeight = 26;
        private const int SearchCandidateRowHeight = 30;
        private const int SearchCandidateRowVisualGap = 3;
        private const int SearchCandidateMoreMessageHeight = 24;
        private const int SearchSectionGap = 8;
        private const int SearchSectionTitleContentGap = 2;
        private const int SearchSectionBodyOffset = LegacyUiMetrics.SectionHeaderHeight + SearchSectionTitleContentGap;
        private const int SearchSectionTextRowHeight = 30;
        private const int SearchMessagePanelExtraHeight = 12;
        private const int SearchBasicPanelPadding = 10;
        private const int SearchBasicChipHeight = 30;
        private const int SearchBasicChipGap = 10;
        private const int SearchBasicFactRowHeight = 24;
        private const int SearchBasicFactLabelWidth = 82;
        private const int SearchBasicFactColumnGap = 10;
        private const int SearchBasicFactCount = 8;
        private const int SearchBasicTwoColumnMinWidth = 460;
        private const int SearchRecipeRowPadding = 6;
        private const int SearchRecipeTitleHeight = 16;
        private const int SearchRecipeTitleGap = 4;
        private const int SearchRecipeLabelWidth = 42;
        private const int SearchRecipeGroupGap = 4;
        private const int SearchRecipeRowGap = 5;
        private const int SearchResultChipHeight = 24;
        private const int SearchResultChipGap = 6;
        private const int SearchResultChipMaxColumns = 3;
        private const int SearchResultChipMinWidth = 92;
        private const int SearchAcquisitionSourceRowMinHeight = 52;
        private const int SearchAcquisitionSourceRowVisualGap = 5;
        private const int SearchAcquisitionSourceTypeWidth = 82;
        private const int SearchAcquisitionSourceDetailTop = 27;
        private const int SearchAcquisitionSourceDetailLineHeight = 16;
        private const int SearchAcquisitionSourceDetailBottomPadding = 6;
        private const float SearchAcquisitionSourceDetailScale = 0.54f;
        private const int SearchShimmerRowHeight = 36;
        private const int SearchReferenceRowVisualGap = 5;
        private const int SearchReferenceRowLabelWidth = 56;
        private const int SearchReferenceChipMaxWidth = 220;
        private const int SearchReferenceChipLeftInset = 3;
        private const int SearchReferenceChipTextGap = 8;
        private const int SearchReferenceChipTextHeight = 16;
        private const int SearchRelatedItemIconSize = 22;

        private sealed class SearchCandidateOverlayDrawState
        {
            public LegacyScrollArea Area { get; set; }
            public LegacyUiRect Anchor { get; set; }
        }

        private sealed class SearchChipGridLayout
        {
            public int ItemCount;
            public int Columns;
            public int Rows;
            public int ChipWidth;
            public int ChipHeight;
            public int Gap;
            public int Height;

            public LegacyUiRect GetRect(int originX, int originY, int index)
            {
                var safeColumns = Math.Max(1, Columns);
                var column = index % safeColumns;
                var row = index / safeColumns;
                return new LegacyUiRect(originX + column * (ChipWidth + Gap), originY + row * (ChipHeight + Gap), ChipWidth, ChipHeight);
            }
        }

        private static LegacyUiElement DrawSearchPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var y = 0;

            hovered = DrawSearchChestLocatorBlock(spriteBatch, area, mouse, elements, y) ?? hovered;
            y += CalculateSearchChestLocatorBlockHeight(area.Viewport.Width) + SearchSectionGap;

            LegacyUiRect inputRect;
            hovered = DrawSearchInputRow(spriteBatch, area, mouse, elements, y, out inputRect) ?? hovered;
            y += SearchInputRowHeight + SearchSectionGap;

            RegisterSearchCandidateOverlay(area, inputRect);
            hovered = DrawSearchResultContent(spriteBatch, area, mouse, elements, y) ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawSearchInputRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, out LegacyUiRect inputRect)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, SearchInputRowHeight);
            inputRect = new LegacyUiRect(row.X + SearchInputLabelWidth, row.Y + 4, Math.Max(120, row.Width - SearchInputLabelWidth - SearchInputPickWidth - SearchInputClearWidth - SearchInputControlGap * 3), SearchInputRowHeight - 8);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var hovered = (LegacyUiElement)null;
            var inputFocused = LegacyTextInput.IsFocused(SearchItemQueryUiState.InputId);
            ulong gameUpdateCount;
            TerrariaMainCompat.TryReadGameUpdateCount(out gameUpdateCount);
            SearchItemQueryUiState.TryRefreshHoverItemFromFreshSnapshot(
                gameUpdateCount,
                TerrariaMainCompat.MouseX,
                TerrariaMainCompat.MouseY);
            if (inputFocused)
            {
                LegacyTextInput.Update(SearchItemQueryUiState.InputId);
                SearchItemQueryUiState.UpdateDraft(LegacyTextInput.GetDraft(SearchItemQueryUiState.InputId));
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                SearchInputLabelText,
                row.X + 10,
                row.Y,
                SearchInputLabelWidth - 16,
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

            var inputHit = inputRect.Intersect(area.Viewport);
            var inputElementRect = inputHit.Width > 0 && inputHit.Height > 0 ? inputHit : inputRect;
            var inputHovered = IsFrameElementHovered(SearchItemQueryUiState.InputId, inputElementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, inputRect, inputHovered, inputHovered && mouse.LeftDown, inputFocused, true, area.Viewport);
            var inputContentRect = LegacyUiTheme.GetSelectedButtonContentRect(inputRect, inputFocused, true);
            var inputText = inputFocused
                ? LegacyTextInput.GetDisplayText(SearchItemQueryUiState.InputId, "物品名 / #ID")
                : FirstNonEmpty(SearchItemQueryUiState.QueryText, "物品名 / #ID");
            var muted = !inputFocused && string.IsNullOrWhiteSpace(SearchItemQueryUiState.QueryText);
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
            TryAttachLegacyTextInputImePanel(SearchItemQueryUiState.InputId, inputRect, area.Viewport);
            var inputElement = AddFrameElement(
                elements,
                SearchItemQueryUiState.InputId,
                "搜索查询:输入",
                "button",
                inputElementRect,
                selected: inputFocused,
                tooltipLines: new[] { "点击输入物品名、内部名或 #ID" });
            RecordFrameElementHover(inputElement, inputHovered);
            if (inputHovered)
            {
                hovered = inputElement;
            }

            var pickRect = new LegacyUiRect(inputRect.Right + SearchInputControlGap, inputRect.Y, SearchInputPickWidth, inputRect.Height);
            var pickHit = pickRect.Intersect(area.Viewport);
            var pickElementRect = pickHit.Width > 0 && pickHit.Height > 0 ? pickHit : pickRect;
            var pickButtonHovered = IsFrameElementHovered(SearchItemQueryUiState.PickItemButtonId, pickElementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, pickRect, pickButtonHovered, pickButtonHovered && mouse.LeftDown, false, true, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                SearchPickButtonText,
                pickRect.X + 4,
                pickRect.Y,
                pickRect.Width - 8,
                pickRect.Height,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                230,
                232,
                224,
                255,
                0.60f);
            var pickElement = AddFrameElement(
                elements,
                SearchItemQueryUiState.PickItemButtonId,
                "搜索查询:选择物品",
                "button",
                pickElementRect,
                tooltipLines: new[] { SearchPickButtonTooltipText });
            RecordFrameElementHover(pickElement, pickButtonHovered);
            if (pickButtonHovered)
            {
                hovered = pickElement;
            }

            var clearRect = new LegacyUiRect(pickRect.Right + SearchInputControlGap, inputRect.Y, SearchInputClearWidth, inputRect.Height);
            var clearHit = clearRect.Intersect(area.Viewport);
            var clearElementRect = clearHit.Width > 0 && clearHit.Height > 0 ? clearHit : clearRect;
            var clearHovered = IsFrameElementHovered("search-query:clear", clearElementRect, mouse);
            var clearEnabled = !string.IsNullOrWhiteSpace(SearchItemQueryUiState.QueryText) || SearchItemQueryUiState.HasSelectedResult;
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
                0.68f);
            var clearElement = AddFrameElement(elements, "search-query:clear", "搜索查询:清空", "button", clearElementRect, enabled: clearEnabled, tooltipLines: new[] { "清空当前查询" });
            RecordFrameElementHover(clearElement, clearHovered);
            return clearHovered ? clearElement : hovered;
        }

        private static LegacyUiElement DrawSearchResultContent(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY)
        {
            var result = SearchItemQueryUiState.GetSelectedResult();
            if (result == null)
            {
                return DrawSearchIdlePanel(spriteBatch, area, contentY);
            }

            if (!result.Found)
            {
                return DrawSearchMessagePanel(spriteBatch, area, contentY, "未找到物品", "#" + result.ItemType.ToString(CultureInfo.InvariantCulture) + " 暂无可展示资料。");
            }

            var hovered = (LegacyUiElement)null;
            var y = contentY;
            DrawSection(spriteBatch, area, y, "基础信息");
            y += SearchSectionBodyOffset;
            var basicPanelHeight = CalculateSearchBasicPanelHeight(area.Viewport.Width);
            DrawSearchBasicPanel(spriteBatch, area, mouse, elements, y, basicPanelHeight, result.Item);
            y += basicPanelHeight + SearchSectionGap;

            DrawSearchAcquisitionSection(spriteBatch, area, y, result.AcquisitionSources);
            y += CalculateSearchAcquisitionSectionHeight(result.AcquisitionSources, area.Viewport.Width) + SearchSectionGap;
            hovered = DrawSearchRecipeSection(spriteBatch, area, mouse, elements, y, "合成来源", result.CraftingSources, "source") ?? hovered;
            y += CalculateSearchRecipeSectionHeight(result.CraftingSources, area.Viewport.Width) + SearchSectionGap;
            hovered = DrawSearchRecipeSection(spriteBatch, area, mouse, elements, y, "合成用途", result.CraftingUses, "use") ?? hovered;
            y += CalculateSearchRecipeSectionHeight(result.CraftingUses, area.Viewport.Width) + SearchSectionGap;
            hovered = DrawSearchShimmerSection(spriteBatch, area, mouse, elements, y, result.Shimmer) ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawSearchIdlePanel(object spriteBatch, LegacyScrollArea area, int contentY)
        {
            var query = SearchItemQueryUiState.QueryText;
            var selectionFailed = SearchItemQueryUiState.SelectionState == SearchItemPickSelectionState.CancelledOrFailed;
            var title = SearchItemQueryUiState.IsSelectionPending
                ? "等待选择物品"
                : (selectionFailed ? "选择失败" : (string.IsNullOrWhiteSpace(query) ? "等待查询" : "请选择候选物品"));
            var detail = SearchItemQueryUiState.IsSelectionPending
                ? SearchItemQueryUiState.SelectionHintText
                : (selectionFailed ? SearchItemQueryUiState.SelectionHintText
                : (string.IsNullOrWhiteSpace(query)
                ? "输入物品名、内部名或 #ID 后会显示匹配候选。"
                : FirstNonEmpty(SearchItemQueryUiState.CandidateMessage, "点击上方候选后展示资料。")));
            return DrawSearchMessagePanel(spriteBatch, area, contentY, title, detail);
        }

        private static LegacyUiElement DrawSearchMessagePanel(object spriteBatch, LegacyScrollArea area, int contentY, string title, string detail)
        {
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, CalculateSearchMessagePanelHeight());
            if (!area.IsVisible(rect))
            {
                return null;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
            UiTextRenderer.DrawTextClipped(spriteBatch, title ?? string.Empty, rect.X + 12, rect.Y + 10, rect.Width - 24, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.76f);
            UiTextRenderer.DrawTextClipped(spriteBatch, detail ?? string.Empty, rect.X + 12, rect.Y + 36, rect.Width - 24, 22, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 202, 214, 232, 230, 0.64f);
            return null;
        }

        private static void DrawSearchBasicPanel(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, int panelHeight, ItemQueryReference item)
        {
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, panelHeight);
            if (!area.IsVisible(rect) || item == null)
            {
                return;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
            DrawSearchItemReferenceChip(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                new LegacyUiRect(rect.X + SearchBasicPanelPadding, rect.Y + SearchBasicPanelPadding, Math.Max(1, rect.Width - SearchBasicPanelPadding * 2), SearchBasicChipHeight),
                item,
                "basic");

            var columnCount = GetSearchBasicFactColumnCount(rect.Width);
            var availableWidth = Math.Max(1, rect.Width - SearchBasicPanelPadding * 2 - (columnCount - 1) * SearchBasicFactColumnGap);
            var columnWidth = Math.Max(1, availableWidth / columnCount);
            var firstFactY = rect.Y + SearchBasicPanelPadding + SearchBasicChipHeight + SearchBasicChipGap;
            for (var index = 0; index < SearchBasicFactCount; index++)
            {
                string label;
                string value;
                if (!TryBuildSearchBasicFact(item, index, out label, out value))
                {
                    continue;
                }

                DrawSearchFactLine(spriteBatch, area.Viewport, rect.X + SearchBasicPanelPadding, firstFactY, columnWidth, columnCount, index, label, value);
            }
        }

        private static void DrawSearchFactLine(object spriteBatch, LegacyUiRect clip, int originX, int firstY, int columnWidth, int columnCount, int factIndex, string label, string value)
        {
            var safeColumns = Math.Max(1, columnCount);
            var column = factIndex % safeColumns;
            var row = factIndex / safeColumns;
            var x = originX + column * (columnWidth + SearchBasicFactColumnGap);
            var y = firstY + row * SearchBasicFactRowHeight;
            var labelWidth = Math.Min(SearchBasicFactLabelWidth, Math.Max(42, columnWidth / 2));
            var valueX = x + labelWidth;
            var valueWidth = Math.Max(1, columnWidth - labelWidth);
            UiTextRenderer.DrawTextClipped(spriteBatch, (label ?? string.Empty) + "：", x, y + 3, labelWidth, 17, clip.X, clip.Y, clip.Width, clip.Height, 176, 198, 224, 230, 0.58f);
            UiTextRenderer.DrawTextClipped(spriteBatch, value ?? string.Empty, valueX, y + 3, valueWidth, 17, clip.X, clip.Y, clip.Width, clip.Height, 224, 232, 240, 240, 0.60f);
        }

        private static void DrawSearchAcquisitionSection(object spriteBatch, LegacyScrollArea area, int contentY, IList<ItemAcquisitionSourceSummary> sources)
        {
            var y = contentY;
            DrawSection(spriteBatch, area, y, SearchAcquisitionSectionTitle);
            y += SearchSectionBodyOffset;
            var count = sources == null ? 0 : sources.Count;
            if (count <= 0)
            {
                DrawSearchEmptySectionRow(spriteBatch, area, y, "暂无获取来源");
                return;
            }

            for (var index = 0; index < count; index++)
            {
                var rowHeight = CalculateSearchAcquisitionSourceRowHeight(sources[index], area.Viewport.Width);
                DrawSearchAcquisitionSourceRow(spriteBatch, area, y, rowHeight, sources[index]);
                y += rowHeight;
            }
        }

        private static void DrawSearchAcquisitionSourceRow(object spriteBatch, LegacyScrollArea area, int contentY, int rowHeight, ItemAcquisitionSourceSummary source)
        {
            var visualHeight = Math.Max(1, rowHeight - SearchAcquisitionSourceRowVisualGap);
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, visualHeight);
            if (!area.IsVisible(row) || source == null)
            {
                return;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                FormatSearchAcquisitionSourceType(source.SourceType),
                row.X + 10,
                row.Y + 4,
                SearchAcquisitionSourceTypeWidth,
                18,
                UiTextHorizontalAlignment.Left,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                176,
                198,
                224,
                230,
                0.56f);

            var detailX = row.X + SearchAcquisitionSourceTypeWidth + 16;
            var detailWidth = Math.Max(1, row.Right - detailX - 10);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                BuildSearchAcquisitionSourceTitle(source),
                detailX,
                row.Y + 5,
                detailWidth,
                16,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                236,
                236,
                224,
                248,
                0.60f);
            var detailLines = BuildSearchAcquisitionSourceDetailLines(source, detailWidth);
            var detailY = row.Y + SearchAcquisitionSourceDetailTop;
            for (var index = 0; index < detailLines.Length; index++)
            {
                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    detailLines[index],
                    detailX,
                    detailY + index * SearchAcquisitionSourceDetailLineHeight,
                    detailWidth,
                    SearchAcquisitionSourceDetailLineHeight,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    202,
                    214,
                    232,
                    230,
                    SearchAcquisitionSourceDetailScale);
            }
        }

        private static LegacyUiElement DrawSearchRecipeSection(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string title, IList<ItemQueryRecipeSummary> recipes, string scope)
        {
            var hovered = (LegacyUiElement)null;
            var y = contentY;
            DrawSection(spriteBatch, area, y, title);
            y += SearchSectionBodyOffset;
            var count = recipes == null ? 0 : recipes.Count;
            if (count <= 0)
            {
                DrawSearchEmptySectionRow(spriteBatch, area, y, "暂无" + title);
                return null;
            }

            for (var index = 0; index < count; index++)
            {
                hovered = DrawSearchRecipeRow(spriteBatch, area, mouse, elements, y, recipes[index], scope, index) ?? hovered;
                y += CalculateSearchRecipeRowHeight(recipes[index], area.Viewport.Width);
            }

            return hovered;
        }

        private static LegacyUiElement DrawSearchRecipeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, ItemQueryRecipeSummary recipe, string scope, int index)
        {
            var rowHeight = CalculateSearchRecipeRowHeight(recipe, area.Viewport.Width);
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, Math.Max(1, rowHeight - SearchRecipeRowGap));
            if (!area.IsVisible(row) || recipe == null)
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            var title = "#" + recipe.RecipeIndex.ToString(CultureInfo.InvariantCulture) + "  " + FormatRecipeMatchKind(recipe.MatchKind, recipe.MatchedRecipeGroupId);
            UiTextRenderer.DrawTextClipped(spriteBatch, title, row.X + 10, row.Y + 6, row.Width - 20, 16, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 226, 218, 194, 238, 0.56f);
            var hovered = (LegacyUiElement)null;
            var chipAreaX = row.X + SearchRecipeRowPadding + SearchRecipeLabelWidth + SearchResultChipGap;
            var chipAreaWidth = Math.Max(1, row.Width - SearchRecipeRowPadding * 2 - SearchRecipeLabelWidth - SearchResultChipGap);
            var groupY = row.Y + SearchRecipeRowPadding + SearchRecipeTitleHeight + SearchRecipeTitleGap;
            if (recipe.CreateItem != null)
            {
                var create = CloneWithStack(recipe.CreateItem, recipe.CreateStack);
                var productLayout = BuildSearchChipGridLayout(chipAreaWidth, 1, SearchResultChipMaxColumns, SearchResultChipGap, SearchResultChipHeight);
                DrawSearchRecipeGroupLabel(spriteBatch, area.Viewport, row, groupY, productLayout.Height, "产物");
                hovered = DrawSearchItemReferenceChip(
                    spriteBatch,
                    mouse,
                    elements,
                    area.Viewport,
                    productLayout.GetRect(chipAreaX, groupY, 0),
                    create,
                    scope + "-create-" + index.ToString(CultureInfo.InvariantCulture)) ?? hovered;
                groupY += productLayout.Height + SearchRecipeGroupGap;
            }

            var ingredientCount = CountSearchRecipeIngredientChips(recipe);
            if (ingredientCount > 0)
            {
                var ingredientLayout = BuildSearchChipGridLayout(chipAreaWidth, ingredientCount, SearchResultChipMaxColumns, SearchResultChipGap, SearchResultChipHeight);
                DrawSearchRecipeGroupLabel(spriteBatch, area.Viewport, row, groupY, ingredientLayout.Height, "材料");
                var drawn = 0;
                for (var ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Count; ingredientIndex++)
                {
                    var ingredient = recipe.Ingredients[ingredientIndex];
                    ItemQueryReference reference;
                    if (!TryGetSearchRecipeIngredientReference(ingredient, out reference))
                    {
                        continue;
                    }

                    hovered = DrawSearchItemReferenceChip(
                        spriteBatch,
                        mouse,
                        elements,
                        area.Viewport,
                        ingredientLayout.GetRect(chipAreaX, groupY, drawn),
                        CloneWithStack(reference, ingredient.Stack),
                        scope + "-ingredient-" + index.ToString(CultureInfo.InvariantCulture) + "-" + ingredientIndex.ToString(CultureInfo.InvariantCulture)) ?? hovered;
                    drawn++;
                }
            }

            return hovered;
        }

        private static void DrawSearchRecipeGroupLabel(object spriteBatch, LegacyUiRect clip, LegacyUiRect row, int groupY, int groupHeight, string label)
        {
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                label ?? string.Empty,
                row.X + SearchRecipeRowPadding,
                groupY,
                SearchRecipeLabelWidth,
                Math.Max(SearchResultChipHeight, groupHeight),
                UiTextHorizontalAlignment.Left,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                176,
                198,
                224,
                230,
                0.58f);
        }

        private static LegacyUiElement DrawSearchShimmerSection(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, ItemQueryShimmerSummary shimmer)
        {
            var hovered = (LegacyUiElement)null;
            var y = contentY;
            DrawSection(spriteBatch, area, y, "微光反应");
            y += SearchSectionBodyOffset;
            if (shimmer == null || !shimmer.HasAnyRelation)
            {
                DrawSearchEmptySectionRow(spriteBatch, area, y, "暂无直接微光关系");
                return null;
            }

            if (shimmer.ForwardResult != null)
            {
                hovered = DrawSearchReferenceRow(spriteBatch, area, mouse, elements, y, "转化为", shimmer.ForwardResult, "shimmer-forward") ?? hovered;
                y += SearchShimmerRowHeight;
            }

            var reverseCount = shimmer.ReverseSources == null ? 0 : shimmer.ReverseSources.Count;
            for (var index = 0; index < reverseCount; index++)
            {
                hovered = DrawSearchReferenceRow(spriteBatch, area, mouse, elements, y, "来源", shimmer.ReverseSources[index], "shimmer-reverse-" + index.ToString(CultureInfo.InvariantCulture)) ?? hovered;
                y += SearchShimmerRowHeight;
            }

            return hovered;
        }

        private static LegacyUiElement DrawSearchReferenceRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string label, ItemQueryReference item, string scope)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, SearchShimmerRowHeight - SearchReferenceRowVisualGap);
            if (!area.IsVisible(row) || item == null)
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, label, row.X + 10, row.Y, SearchReferenceRowLabelWidth, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 214, 224, 238, 238, 0.62f);
            return DrawSearchItemReferenceChip(spriteBatch, mouse, elements, area.Viewport, new LegacyUiRect(row.X + 70, row.Y + 4, Math.Min(SearchReferenceChipMaxWidth, row.Width - 80), SearchResultChipHeight), item, scope);
        }

        private static void DrawSearchEmptySectionRow(object spriteBatch, LegacyScrollArea area, int contentY, string text)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, SearchSectionTextRowHeight - SearchSectionTitleContentGap * 2);
            if (!area.IsVisible(row))
            {
                return;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawTextClipped(spriteBatch, text ?? string.Empty, row.X + 10, row.Y + 7, row.Width - 20, 16, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 202, 214, 232, 225, 0.62f);
        }

        private static LegacyUiElement DrawSearchItemReferenceChip(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, LegacyUiRect rect, ItemQueryReference item, string scope)
        {
            if (item == null || rect.Width <= 0 || rect.Height <= 0 || !rect.Intersects(clip))
            {
                return null;
            }

            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var elementId = "search-query:item:" + item.ItemType.ToString(CultureInfo.InvariantCulture) + ":" + (scope ?? string.Empty);
            var hovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, false, true, clip);
            DrawSearchItemIcon(spriteBatch, new LegacyUiRect(rect.X + SearchReferenceChipLeftInset, rect.Y + Math.Max(1, (rect.Height - SearchRelatedItemIconSize) / 2), SearchRelatedItemIconSize, SearchRelatedItemIconSize), clip, item.ItemType);
            var label = BuildItemReferenceLabel(item);
            UiTextRenderer.DrawTextClipped(spriteBatch, label, rect.X + SearchRelatedItemIconSize + SearchReferenceChipTextGap, rect.Y + Math.Max(3, (rect.Height - SearchReferenceChipTextHeight) / 2), rect.Width - SearchRelatedItemIconSize - SearchReferenceChipTextGap - SearchReferenceChipLeftInset, SearchReferenceChipTextHeight, clip.X, clip.Y, clip.Width, clip.Height, 236, 236, 224, 248, 0.56f);
            var element = AddFrameElement(elements, elementId, "查询 " + label, "button", elementRect, tooltipLines: new[] { "查看：" + label });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static string BuildItemReferenceLabel(ItemQueryReference item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var label = FirstNonEmpty(item.DisplayName, "#" + item.ItemType.ToString(CultureInfo.InvariantCulture));
            if (item.Stack > 1)
            {
                label += " x" + item.Stack.ToString(CultureInfo.InvariantCulture);
            }

            return label + " #" + item.ItemType.ToString(CultureInfo.InvariantCulture);
        }

        private static ItemQueryReference CloneWithStack(ItemQueryReference item, int stack)
        {
            if (item == null)
            {
                return null;
            }

            return new ItemQueryReference
            {
                ItemType = item.ItemType,
                DisplayName = item.DisplayName,
                InternalName = item.InternalName,
                Stack = stack,
                MaxStack = item.MaxStack,
                Rare = item.Rare,
                Value = item.Value,
                IsMaterial = item.IsMaterial,
                IsConsumable = item.IsConsumable,
                CreateTile = item.CreateTile,
                CreateWall = item.CreateWall
            };
        }

        private static string FormatRecipeMatchKind(string matchKind, int groupId)
        {
            if (string.Equals(matchKind, "recipeGroup", StringComparison.OrdinalIgnoreCase))
            {
                return "RecipeGroup " + groupId.ToString(CultureInfo.InvariantCulture);
            }

            if (string.Equals(matchKind, "direct", StringComparison.OrdinalIgnoreCase))
            {
                return "直接材料";
            }

            return "合成来源";
        }

        private static void DrawSearchItemIcon(object spriteBatch, LegacyUiRect iconRect, LegacyUiRect clip, int itemType)
        {
            LegacyUiTheme.DrawCellClipped(spriteBatch, iconRect, false, false, false, clip);
            object texture;
            if (itemType > 0 && VanillaUiSkinCompat.TryGetItemTexture(itemType, out texture))
            {
                UiPrimitiveRenderer.DrawTextureContainedClipped(spriteBatch, texture, iconRect.X + 2, iconRect.Y + 2, iconRect.Width - 4, iconRect.Height - 4, clip.X, clip.Y, clip.Width, clip.Height, 255, 255, 255, 255);
                return;
            }

            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, itemType.ToString(CultureInfo.InvariantCulture), iconRect.X + 1, iconRect.Y + 4, iconRect.Width - 2, 14, clip.X, clip.Y, clip.Width, clip.Height, 230, 230, 214, 255, 0.46f);
        }

        private static bool RegisterSearchCandidateOverlay(LegacyScrollArea area, LegacyUiRect anchor)
        {
            if (!LegacyTextInput.IsFocused(SearchItemQueryUiState.InputId) ||
                string.IsNullOrWhiteSpace(SearchItemQueryUiState.QueryText))
            {
                SearchItemQueryUiState.ClearCandidateViewport();
                return false;
            }

            var rect = BuildSearchCandidateOverlayRect(area, anchor);
            if (area == null || rect.Width <= 0 || rect.Height <= 0)
            {
                SearchItemQueryUiState.ClearCandidateViewport();
                return false;
            }

            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = "search-query-candidates",
                OwnerPageId = "search",
                Bounds = rect,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 25,
                CacheSignature = BuildSearchCandidateOverlayCacheSignature(rect),
                State = new SearchCandidateOverlayDrawState
                {
                    Area = area,
                    Anchor = anchor
                },
                Draw = DrawSearchCandidateOverlay,
                TryConsumeScroll = SearchItemQueryUiState.TryConsumeCandidateScroll
            });
        }

        private static LegacyUiRect BuildSearchCandidateOverlayRect(LegacyScrollArea area, LegacyUiRect anchor)
        {
            if (area == null)
            {
                return new LegacyUiRect();
            }

            var candidateCount = SearchItemQueryUiState.CandidateCount;
            var rows = Math.Max(1, Math.Min(candidateCount, SearchCandidatePopupVisibleRows));
            var height = SearchCandidateHeaderHeight + rows * SearchCandidateRowHeight + SearchCandidatePopupVerticalPadding;
            height = Math.Max(SearchCandidatePopupMinHeight, Math.Min(SearchCandidatePopupMaxHeight, height));
            var width = Math.Max(SearchCandidatePopupMinWidth, Math.Min(anchor.Width, area.Viewport.Width - SearchCandidatePopupEdgeMargin * 2));
            var x = Math.Max(area.Viewport.X + SearchCandidatePopupEdgeMargin, Math.Min(anchor.X, area.Viewport.Right - width - SearchCandidatePopupEdgeMargin));
            var y = anchor.Bottom + SearchCandidatePopupAnchorGap;
            if (y + height > area.Viewport.Bottom - SearchCandidatePopupEdgeMargin)
            {
                y = Math.Max(area.Viewport.Y + SearchCandidatePopupEdgeMargin, anchor.Y - height - SearchCandidatePopupAnchorGap);
            }

            return new LegacyUiRect(x, y, width, height);
        }

        private static void DrawSearchCandidateOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var state = request == null ? null : request.State as SearchCandidateOverlayDrawState;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || request == null || state == null || state.Area == null || elements == null)
            {
                return;
            }

            DrawSearchCandidateList(context.SpriteBatch, state.Area, context.Mouse, elements, request.Bounds);
        }

        private static LegacyUiElement DrawSearchCandidateList(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect popup)
        {
            if (popup.Width <= 0 || popup.Height <= 0)
            {
                SearchItemQueryUiState.ClearCandidateViewport();
                return null;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, popup, area.Viewport);
            UiTextRenderer.DrawTextClipped(spriteBatch, "匹配物品", popup.X + 10, popup.Y + 7, popup.Width - 20, 16, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.66f);
            var body = new LegacyUiRect(popup.X + SearchCandidatePopupPadding, popup.Y + SearchCandidateHeaderHeight, popup.Width - SearchCandidatePopupPadding * 2, Math.Max(0, popup.Height - SearchCandidateHeaderHeight - SearchCandidatePopupPadding));
            var candidates = SearchItemQueryUiState.GetCandidates();
            if (body.Height <= 0)
            {
                SearchItemQueryUiState.ClearCandidateViewport();
                return null;
            }

            if (candidates.Count <= 0)
            {
                SearchItemQueryUiState.SetCandidateViewport(body, body.Height);
                UiTextRenderer.DrawTextClipped(spriteBatch, FirstNonEmpty(SearchItemQueryUiState.CandidateMessage, "无匹配物品"), body.X + 4, body.Y + 8, body.Width - 8, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 206, 214, 228, 236, 0.62f);
                return null;
            }

            var hasMore = SearchItemQueryUiState.HasMoreCandidates;
            var contentHeight = candidates.Count * SearchCandidateRowHeight + (hasMore ? SearchCandidateMoreMessageHeight : 0);
            SearchItemQueryUiState.SetCandidateViewport(body, contentHeight);
            var scrollOffset = SearchItemQueryUiState.CandidateScrollOffset;
            var clip = body.Intersect(area.Viewport);
            var hovered = (LegacyUiElement)null;
            for (var index = 0; index < candidates.Count; index++)
            {
                var row = new LegacyUiRect(body.X, body.Y + index * SearchCandidateRowHeight - scrollOffset, body.Width, SearchCandidateRowHeight - SearchCandidateRowVisualGap);
                if (!row.Intersects(clip))
                {
                    continue;
                }

                hovered = DrawSearchCandidateRow(spriteBatch, mouse, elements, clip, row, candidates[index]) ?? hovered;
            }

            if (hasMore)
            {
                var moreRow = new LegacyUiRect(
                    body.X,
                    body.Y + candidates.Count * SearchCandidateRowHeight - scrollOffset,
                    body.Width,
                    SearchCandidateMoreMessageHeight);
                DrawSearchCandidateMoreMessage(spriteBatch, clip, moreRow);
            }

            return hovered;
        }

        private static void DrawSearchCandidateMoreMessage(object spriteBatch, LegacyUiRect clip, LegacyUiRect row)
        {
            if (!row.Intersects(clip))
            {
                return;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, clip);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                SearchItemQueryUiState.MoreCandidatesMessage,
                row.X + 8,
                row.Y + 4,
                row.Width - 16,
                16,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                210,
                222,
                238,
                235,
                0.56f);
        }

        private static LegacyUiElement DrawSearchCandidateRow(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, LegacyUiRect row, ItemQueryCandidate candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            var hit = row.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : row;
            var elementId = "search-query:candidate:" + candidate.ItemType.ToString(CultureInfo.InvariantCulture);
            var hovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, clip);
            DrawSearchItemIcon(spriteBatch, new LegacyUiRect(row.X + 5, row.Y + 4, 20, 20), clip, candidate.ItemType);
            var label = FirstNonEmpty(candidate.DisplayName, "#" + candidate.ItemType.ToString(CultureInfo.InvariantCulture));
            UiTextRenderer.DrawTextClipped(spriteBatch, label, row.X + 31, row.Y + 5, Math.Max(1, row.Width - 94), 16, clip.X, clip.Y, clip.Width, clip.Height, 236, 236, 224, 248, 0.62f);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, candidate.IdText, row.Right - 58, row.Y + 5, 52, 16, UiTextHorizontalAlignment.Right, clip.X, clip.Y, clip.Width, clip.Height, 206, 218, 236, 230, 0.54f);
            var element = AddFrameElement(elements, elementId, "选择 " + label, "button", elementRect, tooltipLines: new[] { "选择：" + label + " " + candidate.IdText });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static int CalculateSearchContentHeight(LegacyUiRect contentRect)
        {
            var height = CalculateSearchChestLocatorBlockHeight(contentRect.Width) + SearchSectionGap;
            height += SearchInputRowHeight + SearchSectionGap;
            var result = SearchItemQueryUiState.GetSelectedResult();
            if (result == null || !result.Found)
            {
                return height + CalculateSearchMessagePanelHeight() + PageContentBottomPadding;
            }

            height += SearchSectionBodyOffset + CalculateSearchBasicPanelHeight(contentRect.Width) + SearchSectionGap;
            height += CalculateSearchAcquisitionSectionHeight(result.AcquisitionSources, contentRect.Width) + SearchSectionGap;
            height += CalculateSearchRecipeSectionHeight(result.CraftingSources, contentRect.Width) + SearchSectionGap;
            height += CalculateSearchRecipeSectionHeight(result.CraftingUses, contentRect.Width) + SearchSectionGap;
            height += CalculateSearchShimmerSectionHeight(result.Shimmer) + PageContentBottomPadding;
            return height;
        }

        private static int CalculateSearchMessagePanelHeight()
        {
            return SearchSectionTextRowHeight * 2 + SearchMessagePanelExtraHeight;
        }

        private static int CalculateSearchBasicPanelHeight(int panelWidth)
        {
            // DrawSearchBasicPanel and content height must share this helper, or scrolling drifts from rendered rows.
            var columns = GetSearchBasicFactColumnCount(panelWidth);
            var rows = (SearchBasicFactCount + columns - 1) / columns;
            return SearchBasicPanelPadding * 2 + SearchBasicChipHeight + SearchBasicChipGap + rows * SearchBasicFactRowHeight;
        }

        private static int GetSearchBasicFactColumnCount(int panelWidth)
        {
            return panelWidth >= SearchBasicTwoColumnMinWidth ? 2 : 1;
        }

        private static bool TryBuildSearchBasicFact(ItemQueryReference item, int index, out string label, out string value)
        {
            label = string.Empty;
            value = string.Empty;
            if (item == null)
            {
                return false;
            }

            switch (index)
            {
                case 0:
                    label = "内部名";
                    value = FirstNonEmpty(item.InternalName, "未知");
                    return true;
                case 1:
                    label = "最大堆叠";
                    value = item.MaxStack.ToString(CultureInfo.InvariantCulture);
                    return true;
                case 2:
                    label = "稀有度";
                    value = item.Rare.ToString(CultureInfo.InvariantCulture);
                    return true;
                case 3:
                    label = "基础价值";
                    value = ItemValueFormatter.FormatBaseValue(item.Value);
                    return true;
                case 4:
                    label = "材料";
                    value = item.IsMaterial ? "是" : "否";
                    return true;
                case 5:
                    label = "消耗品";
                    value = item.IsConsumable ? "是" : "否";
                    return true;
                case 6:
                    label = "可放置方块";
                    value = FormatSearchPlacementValue("Tile", item.CreateTile);
                    return true;
                case 7:
                    label = "可放置背景墙";
                    value = FormatSearchPlacementValue("Wall", item.CreateWall);
                    return true;
                default:
                    return false;
            }
        }

        private static string FormatSearchPlacementValue(string prefix, int id)
        {
            return id >= 0 ? prefix + "#" + id.ToString(CultureInfo.InvariantCulture) : "无";
        }

        private static int CalculateSearchAcquisitionSectionHeight(IList<ItemAcquisitionSourceSummary> sources, int viewportWidth)
        {
            var count = sources == null ? 0 : sources.Count;
            if (count <= 0)
            {
                return SearchSectionBodyOffset + SearchSectionTextRowHeight;
            }

            var height = SearchSectionBodyOffset;
            for (var index = 0; index < count; index++)
            {
                height += CalculateSearchAcquisitionSourceRowHeight(sources[index], viewportWidth);
            }

            return height;
        }

        private static int CalculateSearchAcquisitionSourceRowHeight(ItemAcquisitionSourceSummary source, int viewportWidth)
        {
            var detailWidth = CalculateSearchAcquisitionDetailWidth(viewportWidth);
            var detailLines = BuildSearchAcquisitionSourceDetailLines(source, detailWidth);
            var visualHeight = SearchAcquisitionSourceDetailTop +
                Math.Max(1, detailLines.Length) * SearchAcquisitionSourceDetailLineHeight +
                SearchAcquisitionSourceDetailBottomPadding;
            return Math.Max(SearchAcquisitionSourceRowMinHeight, visualHeight + SearchAcquisitionSourceRowVisualGap);
        }

        private static string FormatSearchAcquisitionSourceType(string sourceType)
        {
            switch (sourceType ?? string.Empty)
            {
                case ItemAcquisitionSourceTypes.NpcDrop:
                    return "NPC掉落";
                case ItemAcquisitionSourceTypes.NpcShop:
                    return "NPC出售";
                case ItemAcquisitionSourceTypes.Other:
                    return "其他来源";
                default:
                    return "来源";
            }
        }

        private static string BuildSearchAcquisitionSourceTitle(ItemAcquisitionSourceSummary source)
        {
            if (source == null)
            {
                return "未知来源";
            }

            var title = FirstNonEmpty(source.Title, FormatSearchAcquisitionSourceType(source.SourceType));
            var name = FirstNonEmpty(source.SourceName, string.Empty);
            title = PrefixOtherSourceTag(source, title);
            return string.IsNullOrWhiteSpace(name) ? title : title + "：" + name;
        }

        private static string PrefixOtherSourceTag(ItemAcquisitionSourceSummary source, string title)
        {
            if (source == null ||
                !string.Equals(source.SourceType, ItemAcquisitionSourceTypes.Other, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(source.SourceTag))
            {
                return title ?? string.Empty;
            }

            return "[" + source.SourceTag.Trim() + "] " + (title ?? string.Empty);
        }

        private static string BuildSearchAcquisitionSourceDetail(ItemAcquisitionSourceSummary source)
        {
            if (source == null)
            {
                return "条件待确认";
            }

            var parts = BuildSearchAcquisitionSourceDetailParts(source);
            return parts.Count <= 0 ? "条件待确认" : string.Join(" / ", parts.ToArray());
        }

        private static string[] BuildSearchAcquisitionSourceDetailLines(ItemAcquisitionSourceSummary source, int detailWidth)
        {
            var parts = source == null ? new List<string>() : BuildSearchAcquisitionSourceDetailParts(source);
            if (parts.Count <= 0)
            {
                parts.Add("条件待确认");
            }

            var lines = new List<string>();
            var current = string.Empty;
            var safeWidth = Math.Max(48, detailWidth);
            for (var index = 0; index < parts.Count; index++)
            {
                AddSearchAcquisitionDetailWrappedPart(lines, ref current, parts[index], safeWidth);
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                lines.Add(current);
            }

            return lines.ToArray();
        }

        private static List<string> BuildSearchAcquisitionSourceDetailParts(ItemAcquisitionSourceSummary source)
        {
            var parts = new List<string>(4);
            if (source == null)
            {
                return parts;
            }

            AddSearchAcquisitionDetailPart(parts, source.QuantityText);
            AddSearchAcquisitionDetailPart(parts, source.ProbabilityText);
            AddSearchAcquisitionDetailPart(parts, source.ConditionText);
            return parts;
        }

        private static void AddSearchAcquisitionDetailPart(List<string> parts, string text)
        {
            if (parts == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var display = NormalizeSearchAcquisitionDetailPart(text);
            if (!string.IsNullOrWhiteSpace(display))
            {
                parts.Add(display);
            }
        }

        private static string NormalizeSearchAcquisitionDetailPart(string text)
        {
            var display = (text ?? string.Empty).Trim();
            if (display.Length <= 0)
            {
                return string.Empty;
            }

            display = display.Replace("原版商店低频索引", "原版商店资料");
            display = display.Replace("当前世界和玩家上下文", "当前条件");
            display = display.Replace("商店入口：", "店铺：");
            display = display.Replace("curated ", string.Empty);
            display = display.Replace("curated", string.Empty);
            display = display.Replace("非完整概率百科", "来源线索");
            display = display.Replace("非完整百科", "来源线索");
            display = display.Replace("低频索引", "资料");
            display = display.Replace("只读表", "整理表");
            display = display.Replace("只读来源表", "整理来源表");
            return display.Trim();
        }

        private static void AddSearchAcquisitionDetailWrappedPart(List<string> lines, ref string current, string part, int detailWidth)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                return;
            }

            var candidate = string.IsNullOrWhiteSpace(current) ? part : current + " / " + part;
            if (EstimateSearchAcquisitionTextWidth(candidate) <= detailWidth)
            {
                current = candidate;
                return;
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                lines.Add(current);
                current = string.Empty;
            }

            AddSearchAcquisitionDetailFragment(lines, ref current, part, detailWidth);
        }

        private static void AddSearchAcquisitionDetailFragment(List<string> lines, ref string current, string text, int detailWidth)
        {
            var segment = string.Empty;
            for (var index = 0; index < text.Length; index++)
            {
                var next = segment + text[index];
                if (segment.Length > 0 && EstimateSearchAcquisitionTextWidth(next) > detailWidth)
                {
                    lines.Add(segment);
                    segment = text[index].ToString();
                    continue;
                }

                segment = next;
            }

            current = segment;
        }

        private static int CalculateSearchAcquisitionDetailWidth(int viewportWidth)
        {
            var detailXOffset = SearchAcquisitionSourceTypeWidth + 16;
            return Math.Max(1, viewportWidth - detailXOffset - 10);
        }

        private static int EstimateSearchAcquisitionTextWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var width = 0f;
            for (var index = 0; index < text.Length; index++)
            {
                var ch = text[index];
                if (char.IsWhiteSpace(ch))
                {
                    width += 4f;
                }
                else if (ch <= 0x007F)
                {
                    width += 7f;
                }
                else if (char.IsPunctuation(ch))
                {
                    width += 10f;
                }
                else
                {
                    width += 15f;
                }
            }

            return (int)Math.Ceiling(width * SearchAcquisitionSourceDetailScale);
        }

        private static int CalculateSearchRecipeSectionHeight(IList<ItemQueryRecipeSummary> recipes, int viewportWidth)
        {
            var count = recipes == null ? 0 : recipes.Count;
            if (count <= 0)
            {
                return SearchSectionBodyOffset + SearchSectionTextRowHeight;
            }

            var height = SearchSectionBodyOffset;
            for (var index = 0; index < count; index++)
            {
                height += CalculateSearchRecipeRowHeight(recipes[index], viewportWidth);
            }

            return height;
        }

        private static int CalculateSearchRecipeRowHeight(ItemQueryRecipeSummary recipe, int viewportWidth)
        {
            if (recipe == null)
            {
                return SearchSectionTextRowHeight;
            }

            // Recipe drawing and scroll height share this helper so wrapped chips never desync from the scroll range.
            var contentWidth = Math.Max(1, viewportWidth - SearchRecipeRowPadding * 2);
            var chipAreaWidth = Math.Max(1, contentWidth - SearchRecipeLabelWidth - SearchResultChipGap);
            var height = SearchRecipeRowPadding + SearchRecipeTitleHeight + SearchRecipeTitleGap;
            if (recipe.CreateItem != null)
            {
                height += BuildSearchChipGridLayout(chipAreaWidth, 1, SearchResultChipMaxColumns, SearchResultChipGap, SearchResultChipHeight).Height + SearchRecipeGroupGap;
            }

            var ingredientCount = CountSearchRecipeIngredientChips(recipe);
            if (ingredientCount > 0)
            {
                height += BuildSearchChipGridLayout(chipAreaWidth, ingredientCount, SearchResultChipMaxColumns, SearchResultChipGap, SearchResultChipHeight).Height;
            }

            if (recipe.CreateItem == null && ingredientCount <= 0)
            {
                height += SearchSectionTextRowHeight;
            }

            return height + SearchRecipeRowPadding + SearchRecipeRowGap;
        }

        private static int CountSearchRecipeIngredientChips(ItemQueryRecipeSummary recipe)
        {
            if (recipe == null || recipe.Ingredients == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < recipe.Ingredients.Count; index++)
            {
                ItemQueryReference reference;
                if (TryGetSearchRecipeIngredientReference(recipe.Ingredients[index], out reference))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryGetSearchRecipeIngredientReference(ItemQueryIngredientSummary ingredient, out ItemQueryReference reference)
        {
            reference = null;
            if (ingredient == null)
            {
                return false;
            }

            reference = ingredient.Item;
            if (ingredient.IsRecipeGroup && ingredient.AcceptedItems != null && ingredient.AcceptedItems.Count > 0)
            {
                reference = ingredient.AcceptedItems[0];
            }

            return reference != null;
        }

        private static SearchChipGridLayout BuildSearchChipGridLayout(int availableWidth, int itemCount, int maxColumns, int gap, int chipHeight)
        {
            var layout = new SearchChipGridLayout
            {
                ItemCount = Math.Max(0, itemCount),
                ChipHeight = Math.Max(1, chipHeight),
                Gap = Math.Max(0, gap)
            };
            if (layout.ItemCount <= 0)
            {
                return layout;
            }

            var safeWidth = Math.Max(1, availableWidth);
            var columns = Math.Min(Math.Max(1, maxColumns), layout.ItemCount);
            while (columns > 1 && CalculateSearchChipWidth(safeWidth, columns, layout.Gap) < SearchResultChipMinWidth)
            {
                columns--;
            }

            layout.Columns = columns;
            layout.Rows = (layout.ItemCount + columns - 1) / columns;
            layout.ChipWidth = CalculateSearchChipWidth(safeWidth, columns, layout.Gap);
            layout.Height = layout.Rows * layout.ChipHeight + (layout.Rows - 1) * layout.Gap;
            return layout;
        }

        private static int CalculateSearchChipWidth(int availableWidth, int columns, int gap)
        {
            var safeColumns = Math.Max(1, columns);
            return Math.Max(1, (Math.Max(1, availableWidth) - Math.Max(0, gap) * (safeColumns - 1)) / safeColumns);
        }

        private static int CalculateSearchShimmerSectionHeight(ItemQueryShimmerSummary shimmer)
        {
            if (shimmer == null || !shimmer.HasAnyRelation)
            {
                return SearchSectionBodyOffset + SearchSectionTextRowHeight;
            }

            var rows = 0;
            if (shimmer.ForwardResult != null)
            {
                rows++;
            }

            var reverseCount = shimmer.ReverseSources == null ? 0 : shimmer.ReverseSources.Count;
            rows += reverseCount;
            return SearchSectionBodyOffset + rows * SearchShimmerRowHeight;
        }

        private static int BuildSearchCandidateOverlayCacheSignature(LegacyUiRect rect)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + SearchItemQueryUiState.BuildStateSignature();
                hash = hash * 31 + rect.Width;
                hash = hash * 31 + rect.Height;
                return hash;
            }
        }

        internal static bool RegisterSearchCandidateOverlayForTesting(LegacyScrollArea area, LegacyUiRect anchor)
        {
            return RegisterSearchCandidateOverlay(area, anchor);
        }

        internal static int CalculateSearchContentHeightForTesting(LegacyUiRect contentRect)
        {
            return CalculateSearchContentHeight(contentRect);
        }

        internal static int CalculateSearchBasicPanelHeightForTesting(int panelWidth)
        {
            return CalculateSearchBasicPanelHeight(panelWidth);
        }

        internal static int CalculateSearchAcquisitionSectionHeightForTesting(IList<ItemAcquisitionSourceSummary> sources)
        {
            return CalculateSearchAcquisitionSectionHeight(sources, 520);
        }

        internal static int CalculateSearchAcquisitionSectionHeightForTesting(IList<ItemAcquisitionSourceSummary> sources, int viewportWidth)
        {
            return CalculateSearchAcquisitionSectionHeight(sources, viewportWidth);
        }

        internal static string[] BuildSearchAcquisitionDetailLinesForTesting(ItemAcquisitionSourceSummary source, int detailWidth)
        {
            return BuildSearchAcquisitionSourceDetailLines(source, detailWidth);
        }

        internal static int CalculateSearchRecipeSectionHeightForTesting(IList<ItemQueryRecipeSummary> recipes, int viewportWidth)
        {
            return CalculateSearchRecipeSectionHeight(recipes, viewportWidth);
        }

        internal static int CalculateSearchRecipeRowHeightForTesting(ItemQueryRecipeSummary recipe, int viewportWidth)
        {
            return CalculateSearchRecipeRowHeight(recipe, viewportWidth);
        }

        internal static int CalculateSearchShimmerSectionHeightForTesting(ItemQueryShimmerSummary shimmer)
        {
            return CalculateSearchShimmerSectionHeight(shimmer);
        }

        internal static int[] GetSearchChipGridMetricsForTesting(int availableWidth, int itemCount)
        {
            var layout = BuildSearchChipGridLayout(availableWidth, itemCount, SearchResultChipMaxColumns, SearchResultChipGap, SearchResultChipHeight);
            return new[] { layout.Columns, layout.Rows, layout.ChipWidth, layout.Height };
        }

        internal static int[] GetSearchLayoutRhythmForTesting()
        {
            return new[] { SearchSectionGap, SearchSectionTitleContentGap, SearchSectionTextRowHeight, SearchResultChipGap, SearchBasicPanelPadding };
        }

        internal static string[] GetSearchBasicFactLinesForTesting(ItemQueryReference item)
        {
            var lines = new List<string>();
            for (var index = 0; index < SearchBasicFactCount; index++)
            {
                string label;
                string value;
                if (TryBuildSearchBasicFact(item, index, out label, out value))
                {
                    lines.Add(label + "：" + value);
                }
            }

            return lines.ToArray();
        }

        internal static string[] GetSearchResultSectionOrderForTesting()
        {
            return new[] { "基础信息", SearchAcquisitionSectionTitle, "合成来源", "合成用途", "微光反应" };
        }

        internal static string GetSearchAcquisitionEmptyTextForTesting()
        {
            return "暂无获取来源";
        }

        internal static string[] FormatSearchAcquisitionSourceForTesting(ItemAcquisitionSourceSummary source)
        {
            return new[] { FormatSearchAcquisitionSourceType(source == null ? null : source.SourceType), BuildSearchAcquisitionSourceTitle(source), BuildSearchAcquisitionSourceDetail(source) };
        }

        internal static string[] GetSearchInputRowTextForTesting()
        {
            return new[] { SearchInputLabelText, SearchPickButtonText, "清空" };
        }

        internal static string GetSearchPickButtonTooltipForTesting()
        {
            return SearchPickButtonTooltipText;
        }
    }
}
