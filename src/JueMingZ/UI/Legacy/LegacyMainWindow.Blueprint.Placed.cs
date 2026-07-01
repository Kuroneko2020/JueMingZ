using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Blueprint;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
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
            var baseY = area.ToScreenY(contentY);
            for (var index = snapshot.VisibleStartIndex; index < end; index++)
            {
                var visibleIndex = index - snapshot.VisibleStartIndex;
                hovered = DrawBlueprintPlacedRow(
                    spriteBatch,
                    area,
                    mouse,
                    elements,
                    CalculateBlueprintPlacedCardRect(area.Viewport, baseY, visibleIndex),
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
            // Draw/layout must stay cache-only; instance changes refresh this snapshot explicitly.
            var snapshot = BlueprintMaterialService.GetCachedSnapshotForDraw();
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, CalculateBlueprintPlacedMaterialPanelHeight(snapshot));
            if (!area.IsVisible(rect))
            {
                return null;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                BuildBlueprintPlacedMaterialSummary(snapshot),
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
                0.72f);

            var lines = BuildBlueprintPlacedMaterialLines(snapshot, int.MaxValue);
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
                    BlueprintPlacedMaterialLineTextScale);
                return null;
            }

            for (var index = 0; index < lines.Count; index++)
            {
                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    UiTextRenderer.Ellipsize(lines[index], rect.Width - 20, BlueprintPlacedMaterialLineTextScale),
                    rect.X + 10,
                    rect.Y + BlueprintPlacedMaterialPanelHeaderHeight + index * BlueprintPlacedMaterialPanelRowHeight,
                    rect.Width - 20,
                    BlueprintPlacedMaterialPanelRowHeight - 4,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    206,
                    218,
                    238,
                    235,
                    BlueprintPlacedMaterialLineTextScale);
            }

            return null;
        }

        private static LegacyUiElement DrawBlueprintPlacedRow(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect card,
            BlueprintWorldInstanceRecord instance,
            BlueprintPlacedInstanceUiSnapshot snapshot,
            int index)
        {
            if (instance == null)
            {
                return null;
            }

            if (!area.IsVisible(card))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, card, area.Viewport);
            AddUiBlocker(elements, "blueprint-placed:card:" + instance.InstanceId, "已放置蓝图卡片" + index.ToString(CultureInfo.InvariantCulture), card.Intersect(area.Viewport));

            const int gap = BlueprintLibraryCardButtonGap;
            var innerX = card.X + BlueprintLibraryCardPadding;
            var innerWidth = Math.Max(1, card.Width - BlueprintLibraryCardPadding * 2);
            var buttonY = card.Y + BlueprintLibraryCardPadding;
            var removeConfirm = string.Equals(instance.InstanceId, snapshot.RemoveConfirmInstanceId, StringComparison.OrdinalIgnoreCase);
            var materialExpanded = string.Equals(instance.InstanceId, snapshot.ExpandedMaterialInstanceId, StringComparison.OrdinalIgnoreCase);
            var materialWidth = Math.Min(52, Math.Max(42, innerWidth / 5));
            var toggleWidth = Math.Min(52, Math.Max(42, innerWidth / 5));
            var removeWidth = Math.Min(74, Math.Max(54, innerWidth / 4));
            var materialRect = new LegacyUiRect(card.Right - BlueprintLibraryCardPadding - materialWidth, buttonY, materialWidth, BlueprintLibraryCardButtonHeight);
            var toggleRect = new LegacyUiRect(materialRect.X - gap - toggleWidth, buttonY, toggleWidth, BlueprintLibraryCardButtonHeight);
            var removeRect = new LegacyUiRect(toggleRect.X - gap - removeWidth, buttonY, removeWidth, BlueprintLibraryCardButtonHeight);
            var nameWidth = Math.Max(44, removeRect.X - gap - innerX);
            var hovered = (LegacyUiElement)null;
            hovered = DrawBlueprintSmallButton(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                removeRect,
                BlueprintPlacedInstanceUiState.BuildCommandId("remove", instance.InstanceId),
                removeConfirm ? "确认" : "取消放置",
                "取消放置",
                removeConfirm) ?? hovered;
            hovered = DrawBlueprintSmallButton(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                toggleRect,
                BlueprintPlacedInstanceUiState.BuildCommandId("toggle-hidden", instance.InstanceId),
                instance.Hidden ? "显示" : "隐藏",
                instance.Hidden ? "点击显示此蓝图" : "点击隐藏此蓝图") ?? hovered;
            hovered = DrawBlueprintSmallButton(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                materialRect,
                BlueprintPlacedInstanceUiState.BuildCommandId("materials", instance.InstanceId),
                "材料",
                "查看材料清单",
                materialExpanded) ?? hovered;

            var title = instance.Name ?? string.Empty;
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                UiTextRenderer.Ellipsize(title, nameWidth, 0.66f),
                innerX,
                card.Y + 12,
                nameWidth,
                16,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                230,
                232,
                224,
                255,
                0.66f);
            var previewRect = new LegacyUiRect(innerX, buttonY + BlueprintLibraryCardButtonHeight + 6, innerWidth, 68);
            DrawBlueprintTemplatePreviewGrid(spriteBatch, previewRect, area.Viewport, instance.TemplateSnapshot);
            return hovered;
        }

        private static bool RegisterBlueprintPlacedMaterialModalOverlay(LegacyScrollArea area, BlueprintPlacedInstanceUiSnapshot snapshot)
        {
            var instance = BlueprintPlacedInstanceUiState.GetExpandedMaterialInstance(snapshot);
            if (area == null || instance == null)
            {
                return false;
            }

            var bounds = CalculateBlueprintPlacedMaterialModalRect(area.Viewport, instance);
            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = BlueprintPlacedMaterialModalElementId,
                OwnerPageId = "blueprint",
                Bounds = bounds,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 24,
                CacheSignature = BuildBlueprintPlacedMaterialModalCacheSignature(instance),
                State = new BlueprintPlacedMaterialModalState(area, instance),
                Draw = DrawBlueprintPlacedMaterialModalOverlay,
                TryConsumeScroll = TryConsumeBlueprintPlacedMaterialModalScroll
            });
        }

        private static void DrawBlueprintPlacedMaterialModalOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var state = request == null ? null : request.State as BlueprintPlacedMaterialModalState;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || state == null || state.Area == null || state.Instance == null || elements == null)
            {
                return;
            }

            DrawBlueprintPlacedMaterialModal(context.SpriteBatch, state.Area, context.Mouse, elements, state.Instance);
        }

        private static LegacyUiElement DrawBlueprintPlacedMaterialModal(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            BlueprintWorldInstanceRecord instance)
        {
            var bounds = CalculateBlueprintPlacedMaterialModalRect(area.Viewport, instance);
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, JueMingZ.Config.ConfigService.AppSettings ?? JueMingZ.Config.AppSettings.CreateDefault());
            new LegacyPopupPanelControl
            {
                Id = BlueprintPlacedMaterialModalElementId,
                Label = "已放置材料清单",
                Kind = "blocker",
                Bounds = bounds
            }.Draw(context);

            var title = string.IsNullOrWhiteSpace(instance.Name) ? "材料清单" : "材料清单：" + instance.Name.Trim();
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                UiTextRenderer.Ellipsize(title, Math.Max(1, bounds.Width - 84), 0.82f),
                bounds.X + 16,
                bounds.Y + 11,
                Math.Max(1, bounds.Width - 84),
                20,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                238,
                238,
                226,
                255,
                0.82f);

            var closeRect = new LegacyUiRect(bounds.Right - 58, bounds.Y + 8, 44, 22);
            var hovered = DrawBlueprintSmallButton(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                closeRect,
                BlueprintPlacedInstanceUiState.BuildCommandId("materials-close", instance.InstanceId),
                "关闭",
                "关闭材料清单") ?? null;

            var lines = BuildBlueprintPlacedInstanceMaterialLines(instance);
            var columns = ResolveBlueprintLibraryMaterialModalColumns(bounds.Width, lines.Count);
            var body = CalculateBlueprintMaterialModalBodyRect(bounds);
            var contentHeight = CalculateBlueprintMaterialModalContentHeight(lines.Count, columns);
            SetBlueprintPlacedMaterialModalViewport(instance, body.Intersect(area.Viewport), contentHeight);
            var scrollOffset = GetBlueprintPlacedMaterialModalScrollOffset(instance);
            var clip = body.Intersect(area.Viewport);
            var contentWidth = Math.Max(1, body.Width);
            var columnWidth = Math.Max(1, (contentWidth - Math.Max(0, columns - 1) * BlueprintLibraryMaterialModalColumnGap) / columns);
            for (var index = 0; index < lines.Count; index++)
            {
                var column = index % columns;
                var row = index / columns;
                var rect = new LegacyUiRect(
                    body.X + column * (columnWidth + BlueprintLibraryMaterialModalColumnGap),
                    body.Y + row * BlueprintLibraryMaterialModalRowHeight - scrollOffset,
                    columnWidth,
                    BlueprintLibraryMaterialModalRowHeight);
                if (!rect.Intersects(clip))
                {
                    continue;
                }

                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    UiTextRenderer.Ellipsize(lines[index], Math.Max(1, rect.Width - 4), 0.66f),
                    rect.X + 2,
                    rect.Y + 3,
                    rect.Width - 4,
                    rect.Height - 4,
                    clip.X,
                    clip.Y,
                    clip.Width,
                    clip.Height,
                    218,
                    226,
                    238,
                    245,
                    0.66f);
            }

            return context.HoveredElement ?? hovered;
        }

        private static LegacyUiRect CalculateBlueprintPlacedMaterialModalRect(LegacyUiRect viewport, BlueprintWorldInstanceRecord instance)
        {
            var lines = BuildBlueprintPlacedInstanceMaterialLines(instance);
            var width = ClampInt(
                Math.Min(BlueprintLibraryMaterialModalMaxWidth, Math.Max(BlueprintLibraryMaterialModalMinWidth, viewport.Width - 24)),
                Math.Min(BlueprintLibraryMaterialModalMinWidth, Math.Max(1, viewport.Width - 12)),
                Math.Max(1, viewport.Width - 12));
            var columns = ResolveBlueprintLibraryMaterialModalColumns(width, lines.Count);
            var rows = (lines.Count + columns - 1) / columns;
            var desiredHeight = BlueprintLibraryMaterialModalHeaderHeight +
                                rows * BlueprintLibraryMaterialModalRowHeight +
                                BlueprintLibraryMaterialModalPadding;
            var height = ClampInt(desiredHeight, 96, Math.Max(96, viewport.Height - 12));
            var x = viewport.X + Math.Max(0, (viewport.Width - width) / 2);
            var y = viewport.Y + Math.Max(0, (viewport.Height - height) / 2);
            return new LegacyUiRect(x, y, width, height);
        }

        private static int BuildBlueprintPlacedMaterialModalCacheSignature(BlueprintWorldInstanceRecord instance)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(instance == null ? string.Empty : instance.InstanceId ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(instance == null ? string.Empty : instance.Name ?? string.Empty);
                var lines = BuildBlueprintPlacedInstanceMaterialLines(instance);
                for (var index = 0; index < lines.Count; index++)
                {
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(lines[index] ?? string.Empty);
                }

                hash = hash * 31 + GetBlueprintPlacedMaterialModalScrollOffset(instance);
                return hash;
            }
        }

        private static List<string> BuildBlueprintPlacedInstanceMaterialLines(BlueprintWorldInstanceRecord instance)
        {
            // The per-card modal lists the placed snapshot materials, not live inventory/world demand.
            return BlueprintLibraryUiState.BuildTemplateMaterialLines(instance == null ? null : instance.TemplateSnapshot);
        }

        private static string BuildBlueprintPlacedMaterialSummary(BlueprintMaterialSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "材料总计：未就绪";
            }

            if (!snapshot.LoadSucceeded && string.Equals(snapshot.ResultCode, "projectionUnavailable", StringComparison.Ordinal))
            {
                return "材料总计：投影不可用 / " + snapshot.ProjectionResultCode;
            }

            if (snapshot.RequiredItemCount <= 0)
            {
                return "材料总计：当前无缺失材料 / 跳过已完成 " + snapshot.SkippedFulfilledLayerCount.ToString(CultureInfo.InvariantCulture) + " 层";
            }

            return "材料总计：需要 " + snapshot.RequiredItemCount.ToString(CultureInfo.InvariantCulture) +
                   " 项 / 总量 " + snapshot.RequiredStackTotal.ToString(CultureInfo.InvariantCulture) +
                   " / 已有 " + snapshot.AvailableStackTotal.ToString(CultureInfo.InvariantCulture) +
                   " / 仍缺 " + snapshot.MissingStackTotal.ToString(CultureInfo.InvariantCulture) +
                   " / 主包 " + snapshot.InventoryMainStackTotal.ToString(CultureInfo.InvariantCulture) +
                   " / 虚空袋 " + snapshot.InventoryVoidBagStackTotal.ToString(CultureInfo.InvariantCulture);
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
