using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Blueprint;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawBlueprintPlacedToolbar(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            BlueprintPlacedInstanceUiSnapshot snapshot)
        {
            snapshot = snapshot ?? BlueprintPlacedInstanceUiState.GetSnapshot();
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, BlueprintPlacedToolbarHeight);
            if (!area.IsVisible(rect))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, rect, area.Viewport);
            var pageText = snapshot.PageCount <= 0
                ? "实例 0 个"
                : "实例 " + snapshot.Instances.Count.ToString(CultureInfo.InvariantCulture) + " 个 / 第 " +
                  (snapshot.PageIndex + 1).ToString(CultureInfo.InvariantCulture) + "/" +
                  snapshot.PageCount.ToString(CultureInfo.InvariantCulture) + " 页";
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                pageText,
                rect.X + 10,
                rect.Y + 5,
                Math.Max(1, rect.Width - 194),
                14,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                238,
                238,
                226,
                255,
                0.66f);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                snapshot.LastNotice ?? string.Empty,
                rect.X + 10,
                rect.Y + 20,
                Math.Max(1, rect.Width - 194),
                12,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                206,
                218,
                238,
                230,
                0.54f);

            var buttonY = rect.Y + 5;
            var nextRect = new LegacyUiRect(rect.Right - 62, buttonY, 52, RowModeButtonHeight);
            var prevRect = new LegacyUiRect(nextRect.X - 58, buttonY, 52, RowModeButtonHeight);
            var hovered = (LegacyUiElement)null;
            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, prevRect, BlueprintPlacedInstanceUiState.BuildCommandId("page-prev", string.Empty), "上一页", "查看上一页实例") ?? hovered;
            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, nextRect, BlueprintPlacedInstanceUiState.BuildCommandId("page-next", string.Empty), "下一页", "查看下一页实例") ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawBlueprintPlacedList(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            BlueprintPlacedInstanceUiSnapshot snapshot)
        {
            snapshot = snapshot ?? BlueprintPlacedInstanceUiState.GetSnapshot();
            if (!snapshot.LoadSucceeded || snapshot.Instances == null || snapshot.Instances.Count <= 0)
            {
                var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, BlueprintPlacedEmptyHeight);
                if (area.IsVisible(rect))
                {
                    LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
                    var text = snapshot.LoadSucceeded ? "当前世界暂无已放置蓝图" : "当前世界实例读取失败";
                    var detail = snapshot.LoadSucceeded ? "摆放预览确认后，实例会显示在这里。" : snapshot.LoadMessage;
                    UiTextRenderer.DrawTextClipped(spriteBatch, text, rect.X + 12, rect.Y + 10, rect.Width - 24, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.72f);
                    UiTextRenderer.DrawTextClipped(spriteBatch, detail, rect.X + 12, rect.Y + 34, rect.Width - 24, 16, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 206, 218, 238, 230, 0.58f);
                }

                return null;
            }

            var hovered = (LegacyUiElement)null;
            var end = Math.Min(snapshot.Instances.Count, snapshot.VisibleStartIndex + snapshot.VisibleCount);
            for (var index = snapshot.VisibleStartIndex; index < end; index++)
            {
                var visibleIndex = index - snapshot.VisibleStartIndex;
                hovered = DrawBlueprintPlacedRow(
                    spriteBatch,
                    area,
                    mouse,
                    elements,
                    contentY + visibleIndex * (BlueprintPlacedRowHeight + BlueprintPlacedRowGap),
                    snapshot.Instances[index],
                    snapshot,
                    index) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawBlueprintPlacedMaterialPanel(
            object spriteBatch,
            LegacyScrollArea area,
            int contentY)
        {
            var snapshot = BlueprintMaterialService.GetCachedSnapshotForDraw();
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, BlueprintPlacedMaterialPanelHeight);
            if (!area.IsVisible(rect))
            {
                return null;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                BuildBlueprintMaterialSummary(snapshot),
                rect.X + 10,
                rect.Y + 7,
                rect.Width - 20,
                15,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                238,
                238,
                226,
                255,
                0.62f);

            var lines = BuildBlueprintPlacedMaterialLines(snapshot, 2);
            if (lines.Count <= 0)
            {
                var emptyText = snapshot == null || !snapshot.LoadSucceeded
                    ? "材料对照待刷新"
                    : "没有需要补齐的缺失材料";
                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    emptyText,
                    rect.X + 10,
                    rect.Y + 30,
                    rect.Width - 20,
                    14,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    198,
                    210,
                    228,
                    226,
                    0.54f);
                return null;
            }

            for (var index = 0; index < lines.Count; index++)
            {
                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    UiTextRenderer.Ellipsize(lines[index], rect.Width - 20, 0.54f),
                    rect.X + 10,
                    rect.Y + 29 + index * 18,
                    rect.Width - 20,
                    14,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    206,
                    218,
                    238,
                    235,
                    0.54f);
            }

            var itemCount = snapshot == null || snapshot.Items == null ? 0 : snapshot.Items.Count;
            if (itemCount > lines.Count)
            {
                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    "其余 " + (itemCount - lines.Count).ToString(CultureInfo.InvariantCulture) + " 项",
                    rect.X + 10,
                    rect.Y + 64,
                    rect.Width - 20,
                    11,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    170,
                    184,
                    202,
                    210,
                    0.48f);
            }

            return null;
        }

        private static LegacyUiElement DrawBlueprintPlacedRow(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            BlueprintWorldInstanceRecord instance,
            BlueprintPlacedInstanceUiSnapshot snapshot,
            int index)
        {
            if (instance == null)
            {
                return null;
            }

            var card = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, BlueprintPlacedRowHeight);
            if (!area.IsVisible(card))
            {
                return null;
            }

            var selected = string.Equals(instance.InstanceId, snapshot.SelectedInstanceId, StringComparison.OrdinalIgnoreCase);
            LegacyUiTheme.DrawRowClipped(spriteBatch, card, area.Viewport);
            if (selected)
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, card.X + 1, card.Y + 1, card.Width - 2, card.Height - 2, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, LegacyUiTheme.RadiusSmall - 1, 86, 112, 72, 58);
            }

            const int gap = 5;
            var buttonY = card.Y + 5;
            var removeConfirm = string.Equals(instance.InstanceId, snapshot.RemoveConfirmInstanceId, StringComparison.OrdinalIgnoreCase);
            var smallWidth = card.Width < 460 ? 48 : 58;
            var x = card.Right - 4 - smallWidth;
            var hovered = (LegacyUiElement)null;
            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), BlueprintPlacedInstanceUiState.BuildCommandId("remove", instance.InstanceId), removeConfirm ? "确认" : "移除", removeConfirm ? "再次点击移除实例数据" : "移除实例数据") ?? hovered;
            x -= smallWidth + gap;
            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), BlueprintPlacedInstanceUiState.BuildCommandId("toggle-hidden", instance.InstanceId), instance.Hidden ? "恢复显示" : "取消显示", instance.Hidden ? "恢复显示该实例" : "取消显示该实例") ?? hovered;
            x -= smallWidth + gap;
            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), BlueprintPlacedInstanceUiState.BuildCommandId("layer-up", instance.InstanceId), "上层", "提高实例层级") ?? hovered;
            x -= smallWidth + gap;
            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), BlueprintPlacedInstanceUiState.BuildCommandId("layer-down", instance.InstanceId), "下层", "降低实例层级") ?? hovered;
            x -= smallWidth + gap;
            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), BlueprintPlacedInstanceUiState.BuildCommandId("select", instance.InstanceId), "选中", "选中该实例") ?? hovered;

            var textWidth = Math.Max(1, x - gap - card.X - 8);
            var title = (instance.Hidden ? "[隐藏] " : string.Empty) + (instance.Name ?? string.Empty);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                UiTextRenderer.Ellipsize(title, textWidth, 0.66f),
                card.X + 8,
                card.Y + 8,
                textWidth,
                16,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                selected ? 255 : 230,
                selected ? 245 : 232,
                selected ? 205 : 224,
                255,
                0.66f);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                BlueprintPlacedInstanceUiState.BuildInstanceSummary(instance),
                card.X + 10,
                card.Y + 28,
                Math.Max(1, card.Width - 20),
                12,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                198,
                210,
                228,
                226,
                0.52f);
            return hovered;
        }

        private static List<string> BuildBlueprintPlacedMaterialLines(BlueprintMaterialSnapshot snapshot, int maxItems)
        {
            var lines = new List<string>();
            if (snapshot == null || snapshot.Items == null || snapshot.Items.Count <= 0 || maxItems <= 0)
            {
                return lines;
            }

            var count = Math.Min(maxItems, snapshot.Items.Count);
            for (var index = 0; index < count; index++)
            {
                var item = snapshot.Items[index];
                if (item == null)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(item.DisplayName)
                    ? "#" + item.ItemId.ToString(CultureInfo.InvariantCulture)
                    : item.DisplayName.Trim();
                lines.Add(name +
                          "：需要 " + item.RequiredStack.ToString(CultureInfo.InvariantCulture) +
                          " / 已有 " + item.AvailableStack.ToString(CultureInfo.InvariantCulture) +
                          " / 缺 " + item.MissingStack.ToString(CultureInfo.InvariantCulture) +
                          " / 主包 " + item.MainInventoryStack.ToString(CultureInfo.InvariantCulture) +
                          " / 虚空袋 " + item.VoidBagStack.ToString(CultureInfo.InvariantCulture));
            }

            return lines;
        }
    }
}
