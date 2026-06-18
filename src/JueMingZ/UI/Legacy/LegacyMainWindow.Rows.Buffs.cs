using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Movement;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawAutoBuffToggleRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, bool enabled)
        {
            return DrawBinaryModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "自动增益",
                enabled,
                "auto-buff-mode:",
                "缺失的已选 Buff 会自动补充；已选列表为空时不会消耗药水。",
                featureToggleTargetId: "buff.auto_buff");
        }

        private static LegacyUiElement DrawAutoBuffCooldownRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, int cooldown)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyFeatureRow.DrawLabel(spriteBatch, row, "检查冷却", string.Empty);
            var sliderId = "auto-buff-cooldown";
            var sliderLabel = "自动增益冷却";
            var sliderRect = new LegacyUiRect(row.X + 116, row.Y + 4, row.Width - 126, LegacyUiMetrics.SliderHeight);
            var sliderValue = LegacyMainUiState.Clamp(cooldown, AutoBuffCooldownMin, AutoBuffCooldownMax);
            var sliderElement = new LegacySliderControl
            {
                Id = sliderId,
                Label = sliderLabel,
                Kind = "slider",
                Bounds = sliderRect,
                IntValue = sliderValue,
                MinValue = AutoBuffCooldownMin,
                MaxValue = AutoBuffCooldownMax,
                SliderLabel = "冷却",
                Suffix = " tick"
            }.RegisterAndUpdate(LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault()));
            var dragging = string.Equals(LegacyUiInput.ActiveSliderId, sliderId, StringComparison.Ordinal);
            var value = LegacyUiInput.GetSliderDisplayValue(sliderId, sliderValue);
            var hovered = IsFrameElementHovered(sliderId, sliderRect, mouse);
            LegacySlider.DrawValue(spriteBatch, sliderRect, "冷却", value, AutoBuffCooldownMin, AutoBuffCooldownMax, " tick", hovered, dragging);
            return hovered ? sliderElement : null;
        }

        private static LegacyUiElement DrawDualBuffGrid(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY)
        {
            LegacyMainUiState.RefreshBuffCandidatesIfStale(TimeSpan.FromSeconds(1), "Ui.MainWindow.VisibleRefresh");
            var available = LegacyMainUiState.GetAvailableCandidates();
            var entries = LegacyMainUiState.GetWhitelistEntries();
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var paneGap = LegacyUiMetrics.GridCellGap * 2;
            var paneWidth = Math.Max(220, (area.Viewport.Width - paneGap) / 2);
            var totalPaneWidth = paneWidth * 2 + paneGap;
            var leftPane = new LegacyUiRect(area.Viewport.X + Math.Max(0, (area.Viewport.Width - totalPaneWidth) / 2), area.ToScreenY(contentY), paneWidth, DualGridHeight(paneWidth, available.Count, entries.Count));
            var rightPane = new LegacyUiRect(leftPane.Right + paneGap, leftPane.Y, paneWidth, leftPane.Height);
            var hovered = (LegacyUiElement)null;

            DrawPotionPane(spriteBatch, area, leftPane, "可选增益", available.Count <= 0 ? "未发现可选增益物品" : string.Empty);
            DrawPotionPane(spriteBatch, area, rightPane, "已选增益", entries.Count <= 0 ? "已选列表为空。" : string.Empty);
            hovered = DrawAvailablePotionPaneToolbar(spriteBatch, area, mouse, elements, leftPane, settings) ?? hovered;
            hovered = DrawSelectedPotionPaneToolbar(spriteBatch, area, mouse, elements, rightPane) ?? hovered;

            var gridY = contentY + BuffPaneHeaderHeight;
            hovered = DrawAvailablePotionCells(spriteBatch, area, mouse, elements, gridY, leftPane.X, paneWidth, available) ?? hovered;
            hovered = DrawWhitelistPotionCells(spriteBatch, area, mouse, elements, gridY, rightPane.X, paneWidth, entries) ?? hovered;
            return hovered;
        }

        private static void DrawPotionPane(object spriteBatch, LegacyScrollArea area, LegacyUiRect pane, string title, string emptyText)
        {
            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, pane, area.Viewport);
            var titleRect = new LegacyUiRect(pane.X + 6, pane.Y + 5, pane.Width - 12, BuffPaneTitleHeight);
            LegacyUiTheme.DrawSectionHeaderClipped(spriteBatch, titleRect, area.Viewport);
            UiTextRenderer.DrawTextClipped(spriteBatch, title, titleRect.X + 8, titleRect.Y + 8, titleRect.Width - 16, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 244, 238, 210, 255, 0.78f);

            if (!string.IsNullOrWhiteSpace(emptyText))
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, emptyText, pane.X + 12, pane.Y + BuffPaneHeaderHeight + 8, pane.Width - 24, 36, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 202, 214, 232, 255, 0.72f);
            }
        }

        private static LegacyUiElement DrawAvailablePotionPaneToolbar(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect pane, AppSettings settings)
        {
            var hovered = (LegacyUiElement)null;
            var titleRect = new LegacyUiRect(pane.X + 6, pane.Y + 5, pane.Width - 12, BuffPaneTitleHeight);
            var y = titleRect.Y + (titleRect.Height - BuffPaneHeaderButtonHeight) / 2;
            var gap = 3;
            var labelReserve = Math.Min(70, Math.Max(54, titleRect.Width / 4));
            var availableWidth = Math.Max(1, titleRect.Width - labelReserve - 18 - gap * 2);
            var buttonWidth = Math.Min(64, Math.Max(1, availableWidth / 3));
            var totalWidth = buttonWidth * 3 + gap * 2;
            var x = titleRect.Right - 8 - totalWidth;
            hovered = DrawPaneHeaderButton(spriteBatch, area, mouse, elements, new LegacyUiRect(x, y, buttonWidth, BuffPaneHeaderButtonHeight), "buff-refresh-candidates", "刷新候选", false, null) ?? hovered;
            x += buttonWidth + gap;
            hovered = DrawPaneHeaderButton(spriteBatch, area, mouse, elements, new LegacyUiRect(x, y, buttonWidth, BuffPaneHeaderButtonHeight), "buff-follow-add-toggle", "跟随加入", settings.AutoBuffFollowAddEnabled, new[] { "手动使用增益药水后自动加入右侧维持列表。" }) ?? hovered;
            x += buttonWidth + gap;
            hovered = DrawPaneHeaderButton(spriteBatch, area, mouse, elements, new LegacyUiRect(x, y, buttonWidth, BuffPaneHeaderButtonHeight), "buff-follow-remove-toggle", "跟随删除", settings.AutoBuffFollowRemoveEnabled, new[] { "手动取消左上 Buff 后自动移出右侧维持列表。" }) ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawSelectedPotionPaneToolbar(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect pane)
        {
            var titleRect = new LegacyUiRect(pane.X + 6, pane.Y + 5, pane.Width - 12, BuffPaneTitleHeight);
            var y = titleRect.Y + (titleRect.Height - BuffPaneHeaderButtonHeight) / 2;
            var width = Math.Min(92, Math.Max(64, titleRect.Width - 100));
            var rect = new LegacyUiRect(titleRect.Right - 8 - width, y, width, BuffPaneHeaderButtonHeight);
            return DrawPaneHeaderButton(spriteBatch, area, mouse, elements, rect, "buff-clear-whitelist", "清空列表", false, null);
        }

        private static LegacyUiElement DrawPaneHeaderButton(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, bool selected, string[] tooltipLines)
        {
            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, selected, true, area.Viewport);
            var scale = label != null && label.Length >= 4 && rect.Width < 62
                ? 0.48f
                : label != null && label.Length >= 4 && rect.Width < 72 ? 0.52f : 0.58f;
            var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, selected, true);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, label, rect.X + 2, contentRect.Y, rect.Width - 4, contentRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, selected ? LegacyUiTheme.SelectedTextR : 232, selected ? LegacyUiTheme.SelectedTextG : 232, selected ? LegacyUiTheme.SelectedTextB : 224, 255, scale);
            if (selected)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(spriteBatch, new LegacyUiRect(rect.X + 2, contentRect.Y, rect.Width - 4, contentRect.Height), area.Viewport, label, scale);
            }

            var element = AddFrameElement(elements, id, label, "button", elementRect, selected: selected, tooltipLines: tooltipLines);
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawAvailablePotionCells(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, int paneX, int paneWidth, List<BuffPotionCandidate> candidates)
        {
            var hovered = (LegacyUiElement)null;
            if (candidates == null || candidates.Count <= 0)
            {
                return null;
            }

            var columns = GridColumns(paneWidth);
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                var col = index % columns;
                var rowIndex = index / columns;
                var rowCount = Math.Min(columns, candidates.Count - rowIndex * columns);
                var startX = GridRowStartX(paneX, paneWidth, rowCount);
                var rect = new LegacyUiRect(
                    startX + col * (LegacyPotionGrid.CellWidth + LegacyUiMetrics.GridCellGap),
                    area.ToScreenY(contentY + rowIndex * (LegacyPotionGrid.CellHeight + LegacyUiMetrics.GridCellGap)),
                    LegacyPotionGrid.CellWidth,
                    LegacyPotionGrid.CellHeight);
                if (!area.IsVisible(rect))
                {
                    continue;
                }

                var hit = rect.Intersect(area.Viewport);
                var elementId = "candidate:" + candidate.ItemType.ToString(CultureInfo.InvariantCulture);
                var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
                var isHovered = IsFrameElementHovered(elementId, elementRect, mouse);
                LegacyPotionGrid.DrawCandidateCell(spriteBatch, rect, area.Viewport, candidate, isHovered);
                var element = AddFrameElement(elements, elementId, candidate.BuffName, "candidate", elementRect, candidate: candidate);
                element.TooltipContentSignature = BuildTooltipContentSignature(candidate);
                RecordFrameElementHover(element, isHovered);
                if (isHovered)
                {
                    hovered = element;
                }
            }

            return hovered;
        }

        private static LegacyUiElement DrawWhitelistPotionCells(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, int paneX, int paneWidth, List<BuffPotionWhitelistEntry> entries)
        {
            var hovered = (LegacyUiElement)null;
            if (entries == null || entries.Count <= 0)
            {
                return null;
            }

            var columns = GridColumns(paneWidth);
            var activeBuffs = BuffPotionDiagnostics.GetCurrentActiveBuffTypes();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var col = index % columns;
                var rowIndex = index / columns;
                var rowCount = Math.Min(columns, entries.Count - rowIndex * columns);
                var startX = GridRowStartX(paneX, paneWidth, rowCount);
                var rect = new LegacyUiRect(
                    startX + col * (LegacyPotionGrid.CellWidth + LegacyUiMetrics.GridCellGap),
                    area.ToScreenY(contentY + rowIndex * (LegacyPotionGrid.CellHeight + LegacyUiMetrics.GridCellGap)),
                    LegacyPotionGrid.CellWidth,
                    LegacyPotionGrid.CellHeight);
                if (!area.IsVisible(rect))
                {
                    continue;
                }

                var hit = rect.Intersect(area.Viewport);
                var liveCandidate = LegacyMainUiState.FindLiveCandidate(entry.ItemType);
                var elementId = "whitelist:" + entry.ItemType.ToString(CultureInfo.InvariantCulture);
                var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
                var isHovered = IsFrameElementHovered(elementId, elementRect, mouse);
                var active = entry.BuffType > 0 && activeBuffs.Contains(entry.BuffType);
                LegacyPotionGrid.DrawWhitelistCell(spriteBatch, rect, area.Viewport, entry, liveCandidate, isHovered, active);
                var element = AddFrameElement(elements, elementId, entry.BuffName, "whitelist", elementRect, whitelistEntry: entry);
                element.TooltipContentSignature = BuildTooltipContentSignature(entry, liveCandidate, active);
                RecordFrameElementHover(element, isHovered);
                if (isHovered)
                {
                    hovered = element;
                }
            }

            return hovered;
        }
    }
}
