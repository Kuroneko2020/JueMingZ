using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Records;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const int MapMarkerListCardHeight = 34;
        private const int MapMarkerListCardGap = 5;
        private const int MapMarkerListBodyPadding = 8;
        private const int MapMarkerListEmptyHeight = 30;
        private const int MapMarkerListTopGap = 8;
        private const string MapMarkerNameTooltip = "双击输入，限10个字";
        private const string MapMarkerNameConfirmTooltip = "确认保存名称";
        private const string MapMarkerJumpTooltip = "地图跳转到标记位置";
        private const string MapMarkerNavigateTooltip = "分析可达路径";
        private const string MapMarkerTeleportTooltip = "消耗虫洞药水*1 回忆药水*1传送";
        private const string MapMarkerAutopilotTooltip = "暂未实现";
        private const string MapMarkerConfirmAction = "confirm-name";
        private const string MapMarkerListVisualContract = "link-card+empty-text-only+focused-confirm";

        private static LegacyUiElement DrawMapMarkerList(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY)
        {
            var read = PlayerWorldMapMarkerCache.ReadCurrent();
            var markers = read == null || read.Markers == null
                ? new List<PlayerWorldMapMarkerRecord>()
                : read.Markers;
            var y = contentY + MapMarkerListTopGap;
            var hovered = (LegacyUiElement)null;

            if (markers.Count <= 0)
            {
                DrawMapMarkerListEmpty(spriteBatch, area, y, read);
                return null;
            }

            var cardY = y + MapMarkerListBodyPadding;
            for (var index = 0; index < markers.Count; index++)
            {
                hovered = DrawMapMarkerListRow(spriteBatch, area, mouse, elements, cardY + index * (MapMarkerListCardHeight + MapMarkerListCardGap), markers[index], index) ?? hovered;
            }

            return hovered;
        }

        private static void DrawMapMarkerListEmpty(object spriteBatch, LegacyScrollArea area, int contentY, PlayerWorldMapMarkerReadResult read)
        {
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, MapMarkerListEmptyHeight);
            if (!area.IsVisible(rect))
            {
                return;
            }

            var text = read == null || read.IdentityResolved ? "暂无地图标记" : "当前玩家-世界身份不可用";
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                text,
                rect.X + 10,
                rect.Y,
                Math.Max(1, rect.Width - 20),
                rect.Height,
                UiTextHorizontalAlignment.Left,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                178,
                192,
                214,
                230,
                0.70f);
        }

        private static LegacyUiElement DrawMapMarkerListRow(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            PlayerWorldMapMarkerRecord marker,
            int index)
        {
            if (marker == null)
            {
                return null;
            }

            var card = new LegacyUiRect(area.Viewport.X + MapMarkerListBodyPadding, area.ToScreenY(contentY), area.Viewport.Width - MapMarkerListBodyPadding * 2, MapMarkerListCardHeight);
            if (!area.IsVisible(card))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, card, area.Viewport);
            var iconCell = new LegacyUiRect(card.X + 4, card.Y + Math.Max(0, (card.Height - QuickItemIconCellSize) / 2), QuickItemIconCellSize, QuickItemIconCellSize);
            LegacyUiTheme.DrawCellClipped(spriteBatch, iconCell, false, false, false, area.Viewport);
            var iconRect = new LegacyUiRect(iconCell.X + 2, iconCell.Y + 2, iconCell.Width - 4, iconCell.Height - 4);
            DrawMapMarkerItemIcon(spriteBatch, iconRect, area.Viewport, marker.IconItemId);

            const int gap = 5;
            var confirmVisible = ShouldShowMapMarkerConfirmButton(marker.MarkerId);
            var buttonY = card.Y + Math.Max(0, (card.Height - RowModeButtonHeight) / 2);
            var smallWidth = ResolveMapMarkerSmallButtonWidth(card.Width);
            var deleteWidth = Math.Max(38, smallWidth);
            var x = card.Right - 4 - deleteWidth;
            var hovered = (LegacyUiElement)null;
            hovered = DrawMapMarkerSmallButton(spriteBatch, area, mouse, elements, new LegacyUiRect(x, buttonY, deleteWidth, RowModeButtonHeight), "map-custom-marker:delete:" + marker.MarkerId, "删除", "删除地图标记", true, false) ?? hovered;
            x -= smallWidth + gap;
            hovered = DrawMapMarkerSmallButton(spriteBatch, area, mouse, elements, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), "map-custom-marker:autopilot:" + marker.MarkerId, "智驾", MapMarkerAutopilotTooltip, false, false) ?? hovered;
            x -= smallWidth + gap;
            hovered = DrawMapMarkerSmallButton(spriteBatch, area, mouse, elements, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), "map-custom-marker:teleport:" + marker.MarkerId, "传送", MapMarkerTeleportTooltip, false, false) ?? hovered;
            x -= smallWidth + gap;
            hovered = DrawMapMarkerSmallButton(spriteBatch, area, mouse, elements, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), "map-custom-marker:navigate:" + marker.MarkerId, "导航", MapMarkerNavigateTooltip, false, false) ?? hovered;
            x -= smallWidth + gap;
            hovered = DrawMapMarkerSmallButton(spriteBatch, area, mouse, elements, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), "map-custom-marker:jump:" + marker.MarkerId, "跳转", MapMarkerJumpTooltip, true, false) ?? hovered;
            if (confirmVisible)
            {
                x -= smallWidth + gap;
                // Confirm belongs only to the active name input state; keeping it
                // hidden when unfocused avoids turning it back into a permanent row action.
                hovered = DrawMapMarkerSmallButton(spriteBatch, area, mouse, elements, new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight), BuildMapMarkerConfirmCommandId(marker.MarkerId), "确认", MapMarkerNameConfirmTooltip, true, false) ?? hovered;
            }

            var inputX = iconCell.Right + 5;
            var inputWidth = Math.Max(54, x - gap - inputX);
            var inputRect = new LegacyUiRect(inputX, buttonY, inputWidth, RowModeButtonHeight);
            hovered = DrawMapMarkerNameInput(spriteBatch, area, mouse, elements, inputRect, marker, index) ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawMapMarkerNameInput(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect rect,
            PlayerWorldMapMarkerRecord marker,
            int index)
        {
            var inputId = BuildMapMarkerNameInputId(marker == null ? string.Empty : marker.MarkerId);
            var focused = LegacyTextInput.IsFocused(inputId);
            if (focused)
            {
                LegacyTextInput.Update(inputId);
            }

            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered("map-custom-marker:name:" + (marker == null ? string.Empty : marker.MarkerId), elementRect, mouse);
            var hasName = marker != null && !string.IsNullOrWhiteSpace(marker.Name);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, focused, hasName || focused, area.Viewport);
            var content = LegacyUiTheme.GetSelectedButtonContentRect(rect, focused, hasName || focused);
            var text = focused
                ? LegacyTextInput.GetDisplayText(inputId, marker == null ? string.Empty : marker.Name)
                : marker == null ? string.Empty : marker.Name ?? string.Empty;
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                UiTextRenderer.Ellipsize(text, Math.Max(1, rect.Width - 16), 0.68f),
                rect.X + 8,
                content.Y + 4,
                rect.Width - 16,
                Math.Max(1, content.Height - 8),
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                focused ? 255 : 230,
                focused ? 245 : 232,
                focused ? 205 : 224,
                255,
                0.68f);
            TryAttachLegacyTextInputImePanel(inputId, rect, area.Viewport);

            var element = AddFrameElement(
                elements,
                "map-custom-marker:name:" + (marker == null ? string.Empty : marker.MarkerId),
                "地图标记:名称" + index.ToString(CultureInfo.InvariantCulture),
                "button",
                elementRect,
                enabled: true,
                selected: focused,
                tooltipLines: new[] { MapMarkerNameTooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawMapMarkerSmallButton(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect rect,
            string id,
            string label,
            string tooltip,
            bool enabled,
            bool selected)
        {
            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, selected, enabled, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                label,
                rect.X + 2,
                rect.Y,
                rect.Width - 4,
                rect.Height,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                enabled ? 230 : 158,
                enabled ? 232 : 166,
                enabled ? 224 : 176,
                enabled ? 255 : 220,
                FitMapMarkerButtonTextScale(label, rect.Width));
            var element = AddFrameElement(
                elements,
                id,
                "地图标记:" + label,
                "button",
                elementRect,
                enabled: enabled,
                selected: selected,
                tooltipLines: string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static void DrawMapMarkerItemIcon(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, int itemType)
        {
            object texture;
            if (VanillaUiSkinCompat.TryGetItemTexture(PlayerWorldMapMarkerConstants.NormalizeIconItemId(itemType), out texture))
            {
                UiPrimitiveRenderer.DrawTextureContainedClipped(
                    spriteBatch,
                    texture,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height,
                    clip.X,
                    clip.Y,
                    clip.Width,
                    clip.Height,
                    255,
                    255,
                    255,
                    255);
                return;
            }

            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "?", rect.X, rect.Y + 2, rect.Width, rect.Height - 4, clip.X, clip.Y, clip.Width, clip.Height, 232, 236, 220, 255, 0.58f);
        }

        private static int ResolveMapMarkerSmallButtonWidth(int rowWidth)
        {
            return rowWidth < 420 ? 36 : 42;
        }

        private static float FitMapMarkerButtonTextScale(string label, int width)
        {
            var scale = width <= 38 ? 0.58f : 0.64f;
            var measured = UiTextRenderer.EstimateTextWidth(label, scale);
            return measured <= width - 4 ? scale : Math.Max(0.50f, scale * (width - 4) / Math.Max(1, measured));
        }

        private static int CalculateMapMarkerListHeight()
        {
            var read = PlayerWorldMapMarkerCache.ReadCurrent();
            var count = read == null || read.Markers == null ? 0 : read.Markers.Count;
            return CalculateMapMarkerListHeightForCount(count);
        }

        internal static int CalculateMapMarkerListHeightForTesting(int markerCount)
        {
            return CalculateMapMarkerListHeightForCount(markerCount);
        }

        private static int CalculateMapMarkerListHeightForCount(int markerCount)
        {
            return MapMarkerListTopGap + CalculateMapMarkerListBodyHeightForCount(markerCount);
        }

        private static int CalculateMapMarkerListBodyHeightForCount(int markerCount)
        {
            return markerCount <= 0
                ? MapMarkerListEmptyHeight
                : MapMarkerListBodyPadding * 2 + markerCount * MapMarkerListCardHeight + Math.Max(0, markerCount - 1) * MapMarkerListCardGap;
        }

        internal static string BuildMapMarkerNameInputId(string markerId)
        {
            return "map-custom-marker:name-input:" + ((markerId ?? string.Empty).Trim());
        }

        internal static string BuildMapMarkerConfirmCommandIdForTesting(string markerId)
        {
            return BuildMapMarkerConfirmCommandId(markerId);
        }

        private static string BuildMapMarkerConfirmCommandId(string markerId)
        {
            return "map-custom-marker:" + MapMarkerConfirmAction + ":" + ((markerId ?? string.Empty).Trim());
        }

        private static bool ShouldShowMapMarkerConfirmButton(string markerId)
        {
            return LegacyTextInput.IsFocused(BuildMapMarkerNameInputId(markerId));
        }

        internal static bool ShouldShowMapMarkerConfirmButtonForTesting(string markerId)
        {
            return ShouldShowMapMarkerConfirmButton(markerId);
        }

        internal static string[] GetMapMarkerVisibleActionIdsForTesting(string markerId)
        {
            var normalizedMarkerId = (markerId ?? string.Empty).Trim();
            var ids = new List<string>();
            if (ShouldShowMapMarkerConfirmButton(normalizedMarkerId))
            {
                ids.Add(BuildMapMarkerConfirmCommandId(normalizedMarkerId));
            }

            ids.Add("map-custom-marker:jump:" + normalizedMarkerId);
            ids.Add("map-custom-marker:navigate:" + normalizedMarkerId);
            ids.Add("map-custom-marker:teleport:" + normalizedMarkerId);
            ids.Add("map-custom-marker:autopilot:" + normalizedMarkerId);
            ids.Add("map-custom-marker:delete:" + normalizedMarkerId);
            return ids.ToArray();
        }

        internal static int CalculateMapMarkerListBodyHeightForTesting(int markerCount)
        {
            return CalculateMapMarkerListBodyHeightForCount(markerCount);
        }

        internal static string GetMapMarkerListVisualContractForTesting()
        {
            return MapMarkerListVisualContract;
        }

        internal static string[] GetMapMarkerActionTooltipsForTesting()
        {
            return new[]
            {
                MapMarkerNameTooltip,
                MapMarkerNameConfirmTooltip,
                MapMarkerJumpTooltip,
                MapMarkerNavigateTooltip,
                MapMarkerTeleportTooltip,
                MapMarkerAutopilotTooltip
            };
        }
    }
}
