using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawFishingFilterExactPicker(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect picker)
        {
            if (picker.Height <= 0 || picker.Width <= 0)
            {
                FishingFilterUiState.ClearPickerViewport();
                return null;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, picker, area.Viewport);
            DrawFishingFilterFloatingBorder(spriteBatch, area, picker);
            AddUiBlocker(elements, "fishing-filter-exact-picker:blocker", "鱼获候选窗口", picker.Intersect(area.Viewport));
            var hovered = (LegacyUiElement)null;
            var pickerSource = FishingFilterUiState.PickerSource;
            var globalSearch = string.Equals(pickerSource, FishingFilterUiState.PickerSourceGlobal, StringComparison.Ordinal);
            var candidates = FishingFilterUiState.GetPickerCandidates();
            var closeRect = new LegacyUiRect(picker.Right - FishingFilterCloseButtonSize - 8, picker.Y + 5, FishingFilterCloseButtonSize, FishingFilterCloseButtonSize);
            var addRect = new LegacyUiRect(closeRect.X - 92, picker.Y + 5, 88, 22);
            var titleWidth = Math.Max(1, addRect.X - picker.X - 18);
            UiTextRenderer.DrawTextClipped(spriteBatch, globalSearch ? "全局搜索候选" : "当前水域候选", picker.X + 10, picker.Y + 8, titleWidth, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.70f);
            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, addRect, "fishing-filter-exact-picker:add-selected", "添加至名单", false, true, "把已勾选候选加入当前白名单或黑名单精确列表。") ?? hovered;

            hovered = DrawFishingFilterActionButton(spriteBatch, area, mouse, elements, closeRect, "fishing-filter-exact-picker:close", "x", false, true, "关闭候选窗口。") ?? hovered;

            var body = new LegacyUiRect(picker.X + 8, picker.Y + FishingFilterPickerHeaderHeight, picker.Width - 16, Math.Max(0, picker.Height - FishingFilterPickerHeaderHeight - 7));
            if (body.Height <= 0)
            {
                FishingFilterUiState.ClearPickerViewport();
                return hovered;
            }

            if (candidates.Count <= 0)
            {
                FishingFilterUiState.SetPickerViewport(body, body.Height);
                var emptyTitle = globalSearch
                    ? (string.IsNullOrWhiteSpace(FishingFilterUiState.GlobalSearchQuery) ? "输入名称或 ID" : "无匹配物品")
                    : "未获取鱼获列表";
                UiTextRenderer.DrawTextClipped(spriteBatch, emptyTitle, body.X + 4, body.Y + 7, body.Width - 8, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 226, 218, 194, 245, 0.70f);
                UiTextRenderer.DrawTextClipped(spriteBatch, FirstNonEmpty(FishingFilterUiState.PickerMessage, "暂无可解析鱼获"), body.X + 4, body.Y + 31, body.Width - 8, 24, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 196, 208, 226, 230, 0.62f);
                return hovered;
            }

            if (globalSearch && !string.IsNullOrWhiteSpace(FishingFilterUiState.PickerMessage))
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, FishingFilterUiState.PickerMessage, picker.X + 104, picker.Y + 9, Math.Max(1, addRect.X - picker.X - 112), 16, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 226, 218, 194, 230, 0.54f);
            }

            var columnGap = 6;
            var columnWidth = Math.Max(72, (body.Width - columnGap * (FishingFilterPickerColumnCount - 1)) / FishingFilterPickerColumnCount);
            var rows = (candidates.Count + FishingFilterPickerColumnCount - 1) / FishingFilterPickerColumnCount;
            var contentHeight = rows * FishingFilterPickerCandidateHeight;
            FishingFilterUiState.SetPickerViewport(body, contentHeight);
            var scrollOffset = FishingFilterUiState.PickerScrollOffset;
            var clip = body.Intersect(area.Viewport);
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate == null)
                {
                    continue;
                }

                var row = index / FishingFilterPickerColumnCount;
                var col = index % FishingFilterPickerColumnCount;
                var itemRect = new LegacyUiRect(
                    body.X + col * (columnWidth + columnGap),
                    body.Y + row * FishingFilterPickerCandidateHeight - scrollOffset,
                    columnWidth,
                    FishingFilterPickerCandidateHeight - 3);
                if (!itemRect.Intersects(clip))
                {
                    continue;
                }

                var selected = FishingFilterUiState.IsSelected(candidate.Kind, candidate.Id);
                hovered = DrawFishingFilterPickerCandidate(spriteBatch, mouse, elements, itemRect, clip, candidate, selected) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawFishingFilterPickerCandidate(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, LegacyUiRect clip, FishingCatchCandidate candidate, bool selected)
        {
            var hit = rect.Intersect(clip);
            var elementId = "fishing-filter-exact-picker:toggle:" + FishingFilterUiState.BuildKey(candidate.Kind, candidate.Id);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawRowClipped(spriteBatch, rect, clip);
            if (selected)
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, 3, 70, 104, 64, 92);
            }

            DrawFishingFilterIcon(spriteBatch, candidate.Kind, candidate.Id, new LegacyUiRect(rect.X + 5, rect.Y + 4, 18, 18), clip);
            var checkRect = new LegacyUiRect(rect.Right - 18, rect.Y + 7, 11, 11);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, checkRect.X, checkRect.Y, checkRect.Width, checkRect.Height, 1, clip.X, clip.Y, clip.Width, clip.Height, selected ? 230 : 156, selected ? 220 : 166, selected ? 156 : 182, 230);
            if (selected)
            {
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, checkRect.X + 3, checkRect.Y + 3, checkRect.Width - 6, checkRect.Height - 6, clip.X, clip.Y, clip.Width, clip.Height, 226, 208, 126, 245);
            }

            var label = BuildFishingFilterDisplayLabel(candidate.Kind, candidate.Id, candidate.DisplayName);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                label,
                rect.X + 27,
                rect.Y,
                Math.Max(1, checkRect.X - rect.X - 31),
                rect.Height,
                UiTextHorizontalAlignment.Left,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                232,
                234,
                224,
                245,
                0.56f);
            var element = AddFrameElement(elements, elementId, label, "button", elementRect, selected: selected, tooltipLines: new[] { "点击选择 / 取消选择：" + label });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawFishingFilterExactEntries(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, AppSettings settings, string filterMode)
        {
            if (rect.Height <= 0)
            {
                FishingFilterUiState.ClearEntryViewport();
                return null;
            }

            var entries = ResolveActiveExactEntries(settings, filterMode);
            if (entries == null || entries.Count <= 0)
            {
                FishingFilterUiState.SetEntryViewport(rect, rect.Height);
                UiTextRenderer.DrawTextClipped(spriteBatch, "暂无条目", rect.X + 2, rect.Y + 4, rect.Width - 4, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 202, 214, 232, 215, 0.72f);
                UiTextRenderer.DrawTextClipped(spriteBatch, "精确匹配只按 Kind + ID 生效，显示名仅用于兜底展示。", rect.X + 2, rect.Y + 28, rect.Width - 4, 22, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 202, 214, 232, 195, 0.60f);
                return null;
            }

            var visibleEntries = new List<FishingFilterExactEntry>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null)
                {
                    visibleEntries.Add(entry);
                }
            }

            if (visibleEntries.Count <= 0)
            {
                FishingFilterUiState.SetEntryViewport(rect, rect.Height);
                UiTextRenderer.DrawTextClipped(spriteBatch, "暂无条目", rect.X + 2, rect.Y + 4, rect.Width - 4, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 202, 214, 232, 215, 0.72f);
                return null;
            }

            var hovered = (LegacyUiElement)null;
            var clip = rect.Intersect(area.Viewport);
            var columnWidth = Math.Max(72, (rect.Width - FishingFilterEntryColumnGap) / 2);
            var rows = (visibleEntries.Count + 1) / 2;
            var rowStep = FishingFilterExactEntryHeight + 4;
            var contentHeight = rows * rowStep - 4;
            FishingFilterUiState.SetEntryViewport(rect, contentHeight);
            var scrollOffset = FishingFilterUiState.EntryScrollOffset;
            for (var index = 0; index < visibleEntries.Count; index++)
            {
                var entry = visibleEntries[index];
                var rowIndex = index / 2;
                var columnIndex = index % 2;
                var row = new LegacyUiRect(
                    rect.X + columnIndex * (columnWidth + FishingFilterEntryColumnGap),
                    rect.Y + rowIndex * rowStep - scrollOffset,
                    columnWidth,
                    FishingFilterExactEntryHeight);
                if (row.Y >= rect.Bottom)
                {
                    break;
                }

                if (!row.Intersects(clip))
                {
                    continue;
                }

                hovered = DrawFishingFilterExactEntry(spriteBatch, mouse, elements, row, clip, entry) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawFishingFilterExactEntry(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect row, LegacyUiRect clip, FishingFilterExactEntry entry)
        {
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, clip);
            DrawFishingFilterIcon(spriteBatch, entry.Kind, entry.Id, new LegacyUiRect(row.X + 7, row.Y + 5, 20, 20), clip);
            var label = BuildFishingFilterDisplayLabel(entry.Kind, entry.Id, entry.DisplayNameSnapshot);
            var deleteRect = new LegacyUiRect(row.Right - 25, row.Y + 5, 20, 20);
            UiTextRenderer.DrawTextClipped(spriteBatch, label, row.X + 33, row.Y + 8, Math.Max(1, deleteRect.X - row.X - 39), 16, clip.X, clip.Y, clip.Width, clip.Height, 238, 236, 220, 248, 0.66f);
            var elementId = "fishing-filter-exact-entry:delete:" + FishingFilterUiState.BuildKey(entry.Kind, entry.Id);
            var elementRect = deleteRect.Intersect(clip);
            var deleteHovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, deleteRect, deleteHovered, deleteHovered && mouse.LeftDown, false, true, clip);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "x", deleteRect.X + 2, deleteRect.Y + 1, deleteRect.Width - 4, deleteRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 238, 196, 180, 255, 0.74f);

            var element = AddFrameElement(elements, elementId, "删除 " + label, "button", elementRect, tooltipLines: new[] { "删除：" + label });
            RecordFrameElementHover(element, deleteHovered);
            return deleteHovered ? element : null;
        }

        private static void DrawFishingFilterIcon(object spriteBatch, string kind, int id, LegacyUiRect iconRect, LegacyUiRect clip)
        {
            LegacyUiTheme.DrawCellClipped(spriteBatch, iconRect, false, false, false, clip);
            if (string.Equals(kind, FishingCatchKinds.Item, StringComparison.OrdinalIgnoreCase))
            {
                object itemTexture;
                if (VanillaUiSkinCompat.TryGetItemTexture(id, out itemTexture))
                {
                    UiPrimitiveRenderer.DrawTextureContainedClipped(spriteBatch, itemTexture, iconRect.X + 2, iconRect.Y + 2, iconRect.Width - 4, iconRect.Height - 4, clip.X, clip.Y, clip.Width, clip.Height, 255, 255, 255, 255);
                    return;
                }
            }

            var fallback = string.Equals(kind, FishingCatchKinds.NPC, StringComparison.OrdinalIgnoreCase)
                ? "NPC"
                : id.ToString(CultureInfo.InvariantCulture);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, fallback, iconRect.X, iconRect.Y + 4, iconRect.Width, 14, clip.X, clip.Y, clip.Width, clip.Height, 230, 230, 214, 255, 0.48f);
        }

        private static string BuildFishingFilterDisplayLabel(string kind, int id, string snapshot)
        {
            var name = FishingFilterUiDisplay.ResolveDisplayName(kind, id, snapshot);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = id.ToString(CultureInfo.InvariantCulture);
            }

            return name + " #" + id.ToString(CultureInfo.InvariantCulture);
        }

        private static IList<FishingFilterExactEntry> ResolveActiveExactEntries(AppSettings settings, string filterMode)
        {
            if (settings == null ||
                string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                ? settings.FishingFilterDenyExactEntries
                : settings.FishingFilterAllowExactEntries;
        }
    }
}
