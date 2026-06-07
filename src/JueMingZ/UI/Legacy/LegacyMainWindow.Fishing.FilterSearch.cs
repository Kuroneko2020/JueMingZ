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
        private static LegacyUiElement DrawFishingFilterExactActions(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, AppSettings settings, string filterMode, out LegacyUiRect currentPickerAnchor, out LegacyUiRect globalSearchAnchor, out LegacyUiRect presetListAnchor)
        {
            var totalWidth = Math.Max(1, rect.Width - 20);
            var buttonGap = GetFishingFilterActionButtonGap(totalWidth);
            var y = rect.Y + 8;
            var x = rect.X + 10;
            currentPickerAnchor = new LegacyUiRect(x, y, 1, FishingFilterActionButtonHeight);
            globalSearchAnchor = currentPickerAnchor;
            presetListAnchor = currentPickerAnchor;
            if (LegacyTextInput.IsFocused(FishingFilterUiState.PresetNameInputId))
            {
                LegacyTextInput.Update(FishingFilterUiState.PresetNameInputId);
                return DrawFishingFilterPresetNameActions(spriteBatch, area, mouse, elements, new LegacyUiRect(x, y, totalWidth, FishingFilterActionButtonHeight));
            }

            var globalSearchFocused = LegacyTextInput.IsFocused(FishingFilterUiState.GlobalSearchInputId);
            var buttonWidths = BuildFishingFilterActionButtonWidths(totalWidth, buttonGap, new[] { "添加当前", globalSearchFocused ? "名称/#ID" : "+", "清空", "保存预设", "预设列表" });
            var hovered = (LegacyUiElement)null;
            var disabled = string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase);
            currentPickerAnchor = new LegacyUiRect(x, y, buttonWidths[0], FishingFilterActionButtonHeight);
            globalSearchAnchor = new LegacyUiRect(currentPickerAnchor.Right + buttonGap, y, buttonWidths[1], FishingFilterActionButtonHeight);
            var clearRect = new LegacyUiRect(globalSearchAnchor.Right + buttonGap, y, buttonWidths[2], FishingFilterActionButtonHeight);
            var presetSaveRect = new LegacyUiRect(clearRect.Right + buttonGap, y, buttonWidths[3], FishingFilterActionButtonHeight);
            presetListAnchor = new LegacyUiRect(presetSaveRect.Right + buttonGap, y, buttonWidths[4], FishingFilterActionButtonHeight);
            var currentSelected = FishingFilterUiState.PickerOpen && string.Equals(FishingFilterUiState.PickerSource, FishingFilterUiState.PickerSourceCurrent, StringComparison.Ordinal);
            var globalSelected = FishingFilterUiState.PickerOpen && string.Equals(FishingFilterUiState.PickerSource, FishingFilterUiState.PickerSourceGlobal, StringComparison.Ordinal);
            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, currentPickerAnchor, "fishing-filter-exact-picker:open-current", "添加当前", currentSelected, true, disabled ? "过滤未启用时不会添加条目。" : "展开当前本地鱼漂水域的可钓鱼获候选。") ?? hovered;
            if (globalSearchFocused)
            {
                LegacyTextInput.Update(FishingFilterUiState.GlobalSearchInputId);
                UpdateFishingFilterGlobalSearchPicker(settings);
                DrawFishingFilterGlobalSearchInput(spriteBatch, area, elements, globalSearchAnchor);
            }
            else
            {
                hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, globalSearchAnchor, "fishing-filter-exact-picker:open-global", "+", globalSelected, true, disabled ? "过滤未启用时不会添加条目。" : "输入名称或 ID，搜索全游戏可钓鱼获。") ?? hovered;
            }

            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, clearRect, "fishing-filter-list:clear", "清空", false, !disabled, null) ?? hovered;
            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, presetSaveRect, "fishing-filter-preset:save-start", "保存预设", false, !disabled, null) ?? hovered;
            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, presetListAnchor, "fishing-filter-preset:list-toggle", "预设列表", FishingFilterUiState.PresetListOpen, !disabled, null) ?? hovered;
            DrawFishingFilterPresetSaveNotice(spriteBatch, area, presetSaveRect);
            return hovered;
        }

        private static void UpdateFishingFilterGlobalSearchPicker(AppSettings settings)
        {
            var query = LegacyTextInput.GetDraft(FishingFilterUiState.GlobalSearchInputId);
            if (FishingFilterUiState.IsGlobalSearchQuery(query))
            {
                return;
            }

            string message;
            var candidates = ResolveGlobalFishingSearchCandidates(query, out message);
            FishingFilterUiState.OpenGlobalSearchPicker(settings, candidates, query, message);
        }

        private static void DrawFishingFilterGlobalSearchInput(object spriteBatch, LegacyScrollArea area, List<LegacyUiElement> elements, LegacyUiRect rect)
        {
            var hit = rect.Intersect(area.Viewport);
            LegacyUiTheme.DrawRowClipped(spriteBatch, rect, area.Viewport);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 1, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, FishingFilterLinkR, FishingFilterLinkG, FishingFilterLinkB, FishingFilterLinkA);
            var text = LegacyTextInput.GetDisplayText(FishingFilterUiState.GlobalSearchInputId, "名称 / #ID");
            if (string.IsNullOrEmpty(text))
            {
                text = "名称 / #ID";
            }

            UiTextRenderer.DrawTextClipped(spriteBatch, text, rect.X + 8, rect.Y + 7, rect.Width - 16, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.62f);
            var message = LegacyTextInput.DiagnosticMessage;
            var tooltip = string.IsNullOrWhiteSpace(message)
                ? "输入当前显示名、内部英文名或 #ID，搜索全游戏可钓鱼获。"
                : message;
            AddFrameElement(elements, FishingFilterUiState.GlobalSearchInputId, "全局可钓鱼获搜索", "blocker", hit.Width > 0 && hit.Height > 0 ? hit : rect, tooltipLines: new[] { tooltip });
        }

        private static List<FishingCatchCandidate> ResolveGlobalFishingSearchCandidates(string query, out string message)
        {
            message = string.Empty;
            var result = new List<FishingCatchCandidate>();
            var searchText = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            if (searchText.Length <= 0)
            {
                message = "请输入名称或 ID 搜索全游戏可钓鱼获";
                return result;
            }

            InformationWorldContext context;
            string skipReason;
            if (!InformationWorldContextProvider.TryBuild(out context, out skipReason))
            {
                message = NormalizeFishingGlobalSearchContextReason(skipReason);
                return result;
            }

            try
            {
                bool truncated;
                // Global search is a filter-preset index, not a current-water
                // query; keep it detached from bobber and auto-fish state.
                var candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(
                    context,
                    searchText,
                    FishingFilterGlobalSearchMaxResults,
                    out truncated,
                    out message);
                if (candidates != null)
                {
                    for (var index = 0; index < candidates.Count; index++)
                    {
                        var candidate = candidates[index];
                        if (candidate == null || candidate.Id <= 0)
                        {
                            continue;
                        }

                        var kind = FishingFilterUiState.NormalizeKind(candidate.Kind);
                        if (string.IsNullOrWhiteSpace(kind))
                        {
                            continue;
                        }

                        result.Add(new FishingCatchCandidate
                        {
                            Kind = kind,
                            Id = candidate.Id,
                            DisplayName = candidate.DisplayName,
                            DisplayNameSnapshot = candidate.DisplayNameSnapshot,
                            IsCrate = candidate.IsCrate,
                            IsQuestFish = candidate.IsQuestFish,
                            IsEnemy = candidate.IsEnemy
                        });
                    }
                }

                if (result.Count <= 0)
                {
                    message = FirstNonEmpty(message, "无匹配鱼获");
                }
                else if (truncated)
                {
                    message = "结果较多，请继续输入缩小范围";
                }
            }
            catch
            {
                message = "全局可钓鱼获搜索失败";
            }

            return result;
        }

        private static string NormalizeFishingGlobalSearchContextReason(string skipReason)
        {
            if (string.Equals(skipReason, "mainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return "当前不在世界内";
            }

            if (string.Equals(skipReason, "localPlayerUnavailable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skipReason, "localPlayerInactive", StringComparison.OrdinalIgnoreCase))
            {
                return "本地玩家不可用";
            }

            if (string.Equals(skipReason, "terrariaRuntimeTypesUnavailable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skipReason, "mainTypeUnavailable", StringComparison.OrdinalIgnoreCase))
            {
                return "环境不可用";
            }

            return string.IsNullOrWhiteSpace(skipReason) ? "环境不可用" : skipReason;
        }
    }
}
