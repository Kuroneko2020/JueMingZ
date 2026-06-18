using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Config;
using JueMingZ.Records;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const string MapPersistentDeathMarkersTooltip = "大地图常驻显示死亡点（仅显示最近256次）";
        private const string MapWorldDayCountTooltip = "当前玩家-世界累计游戏天数";
        private const string MapRevealedAreaRatioTooltip = "当前玩家-世界地图揭示区域占比";
        private const string MapRevealedAreaRatioClickTooltip = "点击打开详情";
        private const string MapCustomMarkersOnTooltip = "右键大地图试试吧";
        private const string MapFootprintsDisplayTooltip = "在大地图展示足迹";
        private const string MapRareCreatureDirectionTooltip = "装备生命体分析仪时显示稀有生物方向";
        private const string MapTravellingMerchantDirectionTooltip = "显示旅商方位";
        private const string MapQuickAnnouncementKeyboardSlotTooltip = "双击进行改键，不支持鼠标按键";
        private const string MapQuickAnnouncementTriggerSlotTooltip = "双击进行改键，支持鼠标按键";
        private const string MapQuickAnnouncementOnTooltip = "按下三个快捷键对光标位置内容进行广播";
        private const string MapQuickAnnouncementOffTooltip = "";
        private const int MapQuickAnnouncementSlotWidth = 64;
        private const int MapQuickAnnouncementSeparatorWidth = 12;
        private const int MapQuickAnnouncementGap = 6;
        private const int MapDeathHistoryRowIndex = 0;
        private const int MapWorldDayCountRowIndex = 1;
        private const int MapRevealedAreaRatioRowIndex = 2;
        private const int MapPersistentDeathMarkersRowIndex = 3;
        private const int MapFootprintsDisplayRowIndex = 4;
        private const int MapRareCreatureDirectionRowIndex = 5;
        private const int MapTravellingMerchantDirectionRowIndex = 6;
        private const int MapQuickAnnouncementRowIndex = 7;
        private const int MapCustomMarkersRowIndex = 8;

        private static LegacyUiElement DrawMapEnhancementPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            _mapDeathHistoryAnchorVisible = false;
            _mapRevealedAreaDetailsAnchorVisible = false;
            UpdateMapQuickAnnouncementHotkeyCapture(settings);

            var deathHistorySummary = PlayerWorldDeathHistoryCache.ReadCurrentSummary();
            hovered = DrawMapDeathHistoryRow(spriteBatch, area, mouse, elements, CalculateMapEnhancementRowContentY(MapDeathHistoryRowIndex), deathHistorySummary) ?? hovered;
            var playtime = PlayerWorldPlaytimeCache.ReadCurrent();
            hovered = DrawMapWorldDayCountRow(spriteBatch, area, mouse, elements, CalculateMapEnhancementRowContentY(MapWorldDayCountRowIndex), playtime) ?? hovered;
            var exploration = PlayerWorldExplorationCache.ReadCurrent();
            hovered = DrawMapRevealedAreaRatioRow(spriteBatch, area, mouse, elements, CalculateMapEnhancementRowContentY(MapRevealedAreaRatioRowIndex), exploration) ?? hovered;
            hovered = DrawMapPersistentDeathMarkersRow(spriteBatch, area, mouse, elements, CalculateMapEnhancementRowContentY(MapPersistentDeathMarkersRowIndex), settings) ?? hovered;
            hovered = DrawMapFootprintsDisplayRow(spriteBatch, area, mouse, elements, CalculateMapEnhancementRowContentY(MapFootprintsDisplayRowIndex), settings) ?? hovered;
            hovered = DrawMapRareCreatureDirectionRow(spriteBatch, area, mouse, elements, CalculateMapEnhancementRowContentY(MapRareCreatureDirectionRowIndex), settings) ?? hovered;
            hovered = DrawMapTravellingMerchantDirectionRow(spriteBatch, area, mouse, elements, CalculateMapEnhancementRowContentY(MapTravellingMerchantDirectionRowIndex), settings) ?? hovered;
            hovered = DrawMapQuickAnnouncementRow(spriteBatch, area, mouse, elements, CalculateMapEnhancementRowContentY(MapQuickAnnouncementRowIndex), settings) ?? hovered;
            hovered = DrawMapCustomMarkersRow(spriteBatch, area, mouse, elements, CalculateMapEnhancementRowContentY(MapCustomMarkersRowIndex), settings) ?? hovered;
            var markerListY = CalculateMapMarkerListContentY();
            hovered = DrawMapMarkerList(spriteBatch, area, mouse, elements, markerListY) ?? hovered;
            RegisterMapDeathHistoryPopupOverlay(area);
            RegisterMapRevealedAreaDetailsPopupOverlay(area, exploration);
            return hovered;
        }

        private static int CalculateMapEnhancementRowContentY(int rowIndex)
        {
            return LegacyUiMetrics.RowHeight * rowIndex + LegacyUiMetrics.SettingRowGap * rowIndex;
        }

        private static int CalculateMapMarkerListContentY()
        {
            return CalculateMapEnhancementRowContentY(MapCustomMarkersRowIndex) + LegacyUiMetrics.RowHeight;
        }

        private static LegacyUiElement DrawMapPersistentDeathMarkersRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "死亡点常驻",
                settings.MapPersistentDeathMarkersEnabled ? "On" : "Off",
                new[] { "开启", "关闭" },
                new[] { "On", "Off" },
                "map-persistent-death-markers-mode:",
                new[] { MapPersistentDeathMarkersTooltip, string.Empty },
                featureToggleTargetId: "map.persistent_death_markers");
        }

        private static LegacyUiElement DrawMapFootprintsDisplayRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "足迹",
                settings.MapFootprintsDisplayEnabled ? "On" : "Off",
                new[] { "开启", "关闭" },
                new[] { "On", "Off" },
                "map-footprints-display-mode:",
                new[] { MapFootprintsDisplayTooltip, string.Empty },
                featureToggleTargetId: "map.footprints");
        }

        private static LegacyUiElement DrawMapRareCreatureDirectionRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "稀有生物显示方向",
                settings.MapRareCreatureDirectionEnabled ? "On" : "Off",
                new[] { "开启", "关闭" },
                new[] { "On", "Off" },
                "map-rare-creature-direction-mode:",
                new[] { MapRareCreatureDirectionTooltip, string.Empty },
                featureToggleTargetId: "map.rare_creature_direction");
        }

        private static LegacyUiElement DrawMapTravellingMerchantDirectionRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "旅商显示方向",
                settings.MapTravellingMerchantDirectionEnabled ? "On" : "Off",
                new[] { "开启", "关闭" },
                new[] { "On", "Off" },
                "map-travelling-merchant-direction-mode:",
                new[] { MapTravellingMerchantDirectionTooltip, string.Empty },
                featureToggleTargetId: "map.travelling_merchant_direction");
        }

        private static LegacyUiElement DrawMapCustomMarkersRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            const string targetId = "map.custom_markers";
            settings = settings ?? AppSettings.CreateDefault();
            var read = PlayerWorldMapMarkerCache.ReadCurrent();
            var markerCount = read == null || read.Markers == null ? 0 : read.Markers.Count;
            var pageCount = CalculateMapMarkerPageCount(markerCount);
            var pageIndex = ClampMapMarkerPageIndex(GetMapCustomMarkerPageIndex(), markerCount);
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings);
            var buttonY = RowModeButtonY(row);
            const int gap = 6;
            var onWidth = ModeButtonWidth("开启");
            var offWidth = ModeButtonWidth("关闭");
            var pageButtonWidth = ModeButtonWidth("上一页");
            var switchWidth = onWidth + offWidth + gap;
            var pageWidth = pageCount > 1 ? pageButtonWidth * 2 + gap : 0;
            var totalWidth = switchWidth + (pageWidth > 0 ? pageWidth + gap : 0);
            var x = row.Right - totalWidth - 10 - GetFeatureToggleHotkeyReserveWidth(targetId);
            var label = BuildMapCustomMarkersLabel(markerCount);
            LegacySettingRowControl.DrawBackgroundAndLabel(context, row, label, x);

            var hovered = (LegacyUiElement)null;
            if (pageWidth > 0)
            {
                hovered = DrawMapCustomMarkerPageButton(
                    context,
                    new LegacyUiRect(x, buttonY, pageButtonWidth, RowModeButtonHeight),
                    "prev",
                    "上一页",
                    MapMarkerPreviousPageTooltip,
                    pageIndex > 0) ?? hovered;
                x += pageButtonWidth + gap;
                hovered = DrawMapCustomMarkerPageButton(
                    context,
                    new LegacyUiRect(x, buttonY, pageButtonWidth, RowModeButtonHeight),
                    "next",
                    "下一页",
                    MapMarkerNextPageTooltip,
                    pageIndex < pageCount - 1) ?? hovered;
                x += pageButtonWidth + gap;
            }

            hovered = DrawMapCustomMarkerModeButton(
                context,
                new LegacyUiRect(x, buttonY, onWidth, RowModeButtonHeight),
                "On",
                "开启",
                settings.MapCustomMarkersEnabled,
                MapCustomMarkersOnTooltip) ?? hovered;
            x += onWidth + gap;
            hovered = DrawMapCustomMarkerModeButton(
                context,
                new LegacyUiRect(x, buttonY, offWidth, RowModeButtonHeight),
                "Off",
                "关闭",
                !settings.MapCustomMarkersEnabled,
                string.Empty) ?? hovered;
            hovered = DrawFeatureToggleHotkeyButton(context, row, targetId) ?? hovered;
            return context.HoveredElement ?? hovered;
        }

        private static string BuildMapCustomMarkersLabel(int markerCount)
        {
            return markerCount >= PlayerWorldMapMarkerConstants.MaxMarkersPerPair
                ? "地图标记（已到标记上限）"
                : "地图标记";
        }

        private static LegacyUiElement DrawMapCustomMarkerPageButton(LegacyUiContext context, LegacyUiRect rect, string action, string label, string tooltip, bool enabled)
        {
            var button = new LegacyButtonControl
            {
                Id = "map-custom-markers-page:" + action,
                Label = label,
                Text = label,
                ElementLabel = "地图标记:" + label,
                Kind = "button",
                Bounds = rect,
                Enabled = enabled,
                TextScale = 0.70f,
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            };
            var element = button.Draw(context);
            return element != null && context.IsElementHovered(element.Id, rect) ? element : null;
        }

        private static LegacyUiElement DrawMapCustomMarkerModeButton(LegacyUiContext context, LegacyUiRect rect, string value, string label, bool selected, string tooltip)
        {
            var button = new LegacyButtonControl
            {
                Id = "map-custom-markers-mode:" + value,
                Label = label,
                Text = label,
                ElementLabel = "地图标记:" + label,
                Kind = "button",
                Bounds = rect,
                Selected = selected,
                TextScale = 0.78f,
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            };
            var element = button.Draw(context);
            return element != null && context.IsElementHovered(element.Id, rect) ? element : null;
        }

        private static LegacyUiElement DrawMapDeathHistoryRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, PlayerWorldDeathHistoryReadResult summary)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            LegacyUiRect detailsRect;
            LegacyUiRect countRect;
            CalculateMapDeathHistoryButtonRects(row, out detailsRect, out countRect);
            var labelWidth = Math.Max(60, detailsRect.X - row.X - 20);

            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                "死亡信息",
                row.X + 10,
                row.Y,
                labelWidth,
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
                0.86f);

            LegacyUiTheme.DrawButtonClipped(spriteBatch, countRect, false, false, true, false, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                BuildMapDeathHistoryCountText(summary),
                countRect.X + 3,
                countRect.Y,
                countRect.Width - 6,
                countRect.Height,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                255,
                245,
                205,
                255,
                0.76f);

            var element = new LegacyButtonControl
            {
                Id = "map-death-history:toggle",
                Label = "详情",
                Text = "详情",
                ElementLabel = "死亡信息:详情",
                Kind = "button",
                Bounds = detailsRect,
                Selected = _mapDeathHistoryPopupOpen,
                TextScale = 0.76f,
                TooltipLines = null
            }.Draw(context);

            if (_mapDeathHistoryPopupOpen)
            {
                _mapDeathHistoryAnchor = detailsRect;
                _mapDeathHistoryAnchorVisible = true;
            }

            return element != null && context.IsElementHovered(element.Id, detailsRect) ? element : null;
        }

        private static void CalculateMapDeathHistoryButtonRects(LegacyUiRect row, out LegacyUiRect detailsRect, out LegacyUiRect countRect)
        {
            const int buttonWidth = 68;
            const int countWidth = 82;
            const int gap = 6;
            var buttonY = RowModeButtonY(row);
            countRect = new LegacyUiRect(row.Right - countWidth - 10, buttonY, countWidth, RowModeButtonHeight);
            detailsRect = new LegacyUiRect(countRect.X - gap - buttonWidth, buttonY, buttonWidth, RowModeButtonHeight);
        }

        private static string BuildMapDeathHistoryCountText(PlayerWorldDeathHistoryReadResult summary)
        {
            if (summary == null || !summary.IdentityResolved)
            {
                return "--";
            }

            return Math.Max(0, summary.DeathCount).ToString(CultureInfo.InvariantCulture) + " 次";
        }

        private static LegacyUiElement DrawMapWorldDayCountRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, PlayerWorldPlaytimeReadResult playtime)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            const int countWidth = 82;
            var buttonY = RowModeButtonY(row);
            var countRect = new LegacyUiRect(row.Right - countWidth - 10, buttonY, countWidth, RowModeButtonHeight);
            var labelWidth = Math.Max(60, countRect.X - row.X - 20);

            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                "世界天数",
                row.X + 10,
                row.Y,
                labelWidth,
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
                0.86f);

            LegacyUiTheme.DrawButtonClipped(spriteBatch, countRect, false, false, true, false, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                BuildMapWorldDayCountText(playtime),
                countRect.X + 3,
                countRect.Y,
                countRect.Width - 6,
                countRect.Height,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                255,
                245,
                205,
                255,
                0.76f);

            var hit = countRect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : countRect;
            var element = new LegacyUiElement
            {
                Id = "map-world-day-count:value",
                Label = "世界天数",
                Kind = "info",
                Rect = elementRect,
                Enabled = false,
                TooltipLines = BuildMapWorldDayCountTooltipLines(playtime)
            };
            elements.Add(element);
            return context.IsElementHovered(element.Id, elementRect) ? element : null;
        }

        private static string BuildMapWorldDayCountText(PlayerWorldPlaytimeReadResult playtime)
        {
            if (playtime == null || !playtime.IdentityResolved)
            {
                return "--";
            }

            return Math.Max(0, playtime.WholeDayCount).ToString(CultureInfo.InvariantCulture) + " 天";
        }

        private static string[] BuildMapWorldDayCountTooltipLines(PlayerWorldPlaytimeReadResult playtime)
        {
            if (playtime == null || !playtime.IdentityResolved)
            {
                return new[] { "当前玩家-世界身份不可用" };
            }

            if (playtime.ReadFailed)
            {
                return new[] { MapWorldDayCountTooltip, "playtime.json 读取失败，暂按 0 天显示" };
            }

            return new[] { MapWorldDayCountTooltip };
        }

        private static LegacyUiElement DrawMapRevealedAreaRatioRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, PlayerWorldExplorationReadResult exploration)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            const int countWidth = 82;
            var buttonY = RowModeButtonY(row);
            var countRect = new LegacyUiRect(row.Right - countWidth - 10, buttonY, countWidth, RowModeButtonHeight);
            var labelWidth = Math.Max(60, countRect.X - row.X - 20);

            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                "揭示区域",
                row.X + 10,
                row.Y,
                labelWidth,
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
                0.86f);

            var hit = countRect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : countRect;
            var element = new LegacyButtonControl
            {
                Id = "map-revealed-area-ratio:toggle",
                Label = "揭示区域",
                Text = BuildMapRevealedAreaRatioText(exploration),
                ElementLabel = "揭示区域:详情",
                Kind = "button",
                Bounds = elementRect,
                Selected = _mapRevealedAreaDetailsPopupOpen,
                TextScale = 0.76f,
                TooltipLines = BuildMapRevealedAreaRatioTooltipLines(exploration)
            }.Draw(context);

            if (_mapRevealedAreaDetailsPopupOpen)
            {
                _mapRevealedAreaDetailsAnchor = countRect;
                _mapRevealedAreaDetailsAnchorVisible = true;
            }

            return element != null && context.IsElementHovered(element.Id, elementRect) ? element : null;
        }

        private static string BuildMapRevealedAreaRatioText(PlayerWorldExplorationReadResult exploration)
        {
            if (exploration == null || !exploration.IdentityResolved || exploration.ReadFailed)
            {
                return "--";
            }

            return Math.Max(0d, exploration.RevealedPercent).ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }

        private static string[] BuildMapRevealedAreaRatioTooltipLines(PlayerWorldExplorationReadResult exploration)
        {
            return new[] { MapRevealedAreaRatioClickTooltip };
        }

        private static bool RegisterMapDeathHistoryPopupOverlay(LegacyScrollArea area)
        {
            if (!_mapDeathHistoryPopupOpen || !_mapDeathHistoryAnchorVisible || area == null)
            {
                return false;
            }

            var page = PlayerWorldDeathHistoryCache.ReadCurrentPage(_mapDeathHistoryPageIndex, MapDeathHistoryPopupPageSize);
            _mapDeathHistoryPageIndex = page == null ? 0 : page.PageIndex;
            var popup = CalculateMapDeathHistoryPopupRect(area.Viewport, _mapDeathHistoryAnchor);
            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = "map-death-history-popup",
                OwnerPageId = "map_enhancement",
                Bounds = popup,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 20,
                CacheSignature = BuildMapDeathHistoryPopupCacheSignature(page, popup),
                State = new MapDeathHistoryPopupDrawState
                {
                    Area = area,
                    Page = page
                },
                Draw = DrawMapDeathHistoryPopupOverlay
            });
        }

        private static void DrawMapDeathHistoryPopupOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var state = request == null ? null : request.State as MapDeathHistoryPopupDrawState;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || state == null || state.Area == null || elements == null)
            {
                return;
            }

            DrawMapDeathHistoryPopup(context.SpriteBatch, state.Area, context.Mouse, elements, state.Page);
        }

        private static LegacyUiElement DrawMapDeathHistoryPopup(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, PlayerWorldDeathHistoryReadResult page)
        {
            if (!_mapDeathHistoryPopupOpen || !_mapDeathHistoryAnchorVisible || area == null)
            {
                return null;
            }

            page = page ?? new PlayerWorldDeathHistoryReadResult();
            var popup = CalculateMapDeathHistoryPopupRect(area.Viewport, _mapDeathHistoryAnchor);
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            new LegacyPopupPanelControl
            {
                Id = "map-death-history-popup",
                Label = "死亡信息",
                Kind = "blocker",
                Bounds = popup
            }.Draw(context);

            UiTextRenderer.DrawText(spriteBatch, "死亡信息", popup.X + 16, popup.Y + 11, 238, 238, 226, 255, 0.82f);
            var hovered = (LegacyUiElement)null;
            hovered = DrawMapDeathHistorySmallButton(
                spriteBatch,
                mouse,
                elements,
                new LegacyUiRect(popup.Right - 54, popup.Y + 8, 40, 20),
                "map-death-history:close",
                "关闭",
                "关闭死亡信息") ?? hovered;

            var summaryText = page.IdentityResolved
                ? "当前记录 " + Math.Max(0, page.DeathCount).ToString(CultureInfo.InvariantCulture) + " 次"
                : "当前玩家-世界身份不可用";
            UiTextRenderer.DrawText(spriteBatch, summaryText, popup.X + 16, popup.Y + 44, 218, 230, 244, 235, 0.68f);

            if (!page.IdentityResolved)
            {
                DrawMapDeathHistoryEmptyMessage(spriteBatch, popup, page.Message, "无法识别当前玩家-世界");
            }
            else if (page.PageCount <= 0 || page.Records == null || page.Records.Count <= 0)
            {
                DrawMapDeathHistoryEmptyMessage(spriteBatch, popup, string.Empty, "暂无死亡记录");
            }
            else
            {
                DrawMapDeathHistoryRows(spriteBatch, popup, page);
            }

            var footerY = popup.Bottom - 36;
            var previousEnabled = page.IdentityResolved && page.PageIndex > 0;
            var nextEnabled = page.IdentityResolved && page.PageCount > 0 && page.PageIndex < page.PageCount - 1;
            hovered = DrawMapDeathHistorySmallButton(
                spriteBatch,
                mouse,
                elements,
                new LegacyUiRect(popup.X + 16, footerY, 70, 24),
                "map-death-history:prev",
                "上一页",
                "查看上一页",
                previousEnabled) ?? hovered;
            hovered = DrawMapDeathHistorySmallButton(
                spriteBatch,
                mouse,
                elements,
                new LegacyUiRect(popup.Right - 86, footerY, 70, 24),
                "map-death-history:next",
                "下一页",
                "查看下一页",
                nextEnabled) ?? hovered;

            var pageText = page.PageCount <= 0
                ? "0 / 0"
                : (page.PageIndex + 1).ToString(CultureInfo.InvariantCulture) + " / " + page.PageCount.ToString(CultureInfo.InvariantCulture);
            UiTextRenderer.DrawCenteredText(spriteBatch, pageText, popup.X + 96, footerY, Math.Max(1, popup.Width - 192), 24, 238, 242, 232, 245, 0.72f);

            return hovered;
        }

        private static void DrawMapDeathHistoryRows(object spriteBatch, LegacyUiRect popup, PlayerWorldDeathHistoryReadResult page)
        {
            var startY = popup.Y + 70;
            var rowHeight = 28;
            for (var index = 0; index < page.Records.Count; index++)
            {
                var entry = page.Records[index];
                var y = startY + index * rowHeight;
                var rowRect = new LegacyUiRect(popup.X + 14, y, popup.Width - 28, rowHeight - 3);
                if (index % 2 == 0)
                {
                    UiPrimitiveRenderer.DrawFilledRect(spriteBatch, rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height, 36, 44, 70, 128);
                }

                var timeWidth = Math.Min(168, Math.Max(118, rowRect.Width / 3));
                var timeText = UiTextRenderer.Ellipsize(entry == null ? string.Empty : entry.DisplayTimeText, timeWidth - 8, 0.62f);
                UiTextRenderer.DrawAlignedText(spriteBatch, timeText, rowRect.X + 6, rowRect.Y + 1, timeWidth - 8, rowRect.Height, UiTextHorizontalAlignment.Left, 205, 218, 238, 235, 0.62f);
                var textX = rowRect.X + timeWidth + 6;
                var textWidth = Math.Max(1, rowRect.Right - textX - 4);
                var deathText = UiTextRenderer.Ellipsize(entry == null ? string.Empty : entry.DeathText, textWidth, 0.66f);
                UiTextRenderer.DrawAlignedText(spriteBatch, deathText, textX, rowRect.Y + 1, textWidth, rowRect.Height, UiTextHorizontalAlignment.Left, 246, 242, 220, 245, 0.66f);
            }
        }

        private static void DrawMapDeathHistoryEmptyMessage(object spriteBatch, LegacyUiRect popup, string detail, string message)
        {
            UiTextRenderer.DrawCenteredText(spriteBatch, message, popup.X + 18, popup.Y + 122, popup.Width - 36, 26, 226, 232, 242, 240, 0.76f);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                UiTextRenderer.DrawCenteredText(spriteBatch, UiTextRenderer.Ellipsize(detail, popup.Width - 40, 0.62f), popup.X + 20, popup.Y + 150, popup.Width - 40, 22, 178, 192, 214, 220, 0.62f);
            }
        }

        private static LegacyUiElement DrawMapDeathHistorySmallButton(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, string tooltip, bool enabled = true)
        {
            var context = new LegacyUiContext(spriteBatch, mouse, LegacyMainUiState.WindowRect, LegacyMainUiState.SelectedPageId, ConfigService.AppSettings ?? AppSettings.CreateDefault(), elements);
            var element = new LegacySmallButtonControl
            {
                Id = id,
                Label = label,
                Text = label,
                ElementLabel = label,
                Kind = "button",
                Bounds = rect,
                Enabled = enabled,
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            }.Draw(context);
            return element != null && context.IsElementHovered(id, rect) ? element : null;
        }

        private static LegacyUiRect CalculateMapDeathHistoryPopupRect(LegacyUiRect viewport, LegacyUiRect anchor)
        {
            var width = Math.Min(MapDeathHistoryPopupMaxWidth, Math.Max(MapDeathHistoryPopupMinWidth, viewport.Width - 24));
            width = Math.Min(width, Math.Max(1, viewport.Width - 12));
            var height = Math.Min(MapDeathHistoryPopupHeight, Math.Max(168, viewport.Height - 12));
            var x = ClampInt(anchor.X - width + anchor.Width, viewport.X + 6, viewport.Right - width - 6);
            var y = anchor.Bottom + 8;
            if (y + height > viewport.Bottom - 6)
            {
                y = anchor.Y - height - 8;
            }

            y = ClampInt(y, viewport.Y + 6, viewport.Bottom - height - 6);
            return new LegacyUiRect(x, y, width, height);
        }

        private static int BuildMapDeathHistoryPopupCacheSignature(PlayerWorldDeathHistoryReadResult page, LegacyUiRect popup)
        {
            unchecked
            {
                var hash = 17;
                AddHash(ref hash, _mapDeathHistoryPopupOpen);
                AddHash(ref hash, _mapDeathHistoryPageIndex);
                AddHash(ref hash, popup.X);
                AddHash(ref hash, popup.Y);
                AddHash(ref hash, popup.Width);
                AddHash(ref hash, popup.Height);
                AddHash(ref hash, page == null ? 0 : page.DataSignature);
                AddHash(ref hash, page == null ? string.Empty : page.Status);
                return hash;
            }
        }

        private static LegacyUiElement DrawMapQuickAnnouncementRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            const string targetId = "map.quick_announcement";
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            settings = settings ?? AppSettings.CreateDefault();
            var hotkey = MapQuickAnnouncementSettings.NormalizeHotkey(
                settings.MapQuickAnnouncementHotkeySlot1,
                settings.MapQuickAnnouncementHotkeySlot2,
                settings.MapQuickAnnouncementTriggerKey);
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings);
            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                "快捷宣告",
                row.X + 10,
                row.Y,
                92,
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
                0.86f);

            var layout = CalculateMapQuickAnnouncementLayout(row);
            var hovered = (LegacyUiElement)null;

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                layout[0],
                "map-quick-announcement-key:1",
                "快捷宣告:前置键1",
                BuildMapQuickAnnouncementKeyText(hotkey.Slot1, MapQuickAnnouncementSettings.HotkeySlot1Id),
                IsMapQuickAnnouncementHotkeyCaptureSlot(MapQuickAnnouncementSettings.HotkeySlot1Id),
                MapQuickAnnouncementKeyboardSlotTooltip) ?? hovered;
            DrawMapQuickAnnouncementSeparator(spriteBatch, area.Viewport, layout[1]);

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                layout[2],
                "map-quick-announcement-key:2",
                "快捷宣告:前置键2",
                BuildMapQuickAnnouncementKeyText(hotkey.Slot2, MapQuickAnnouncementSettings.HotkeySlot2Id),
                IsMapQuickAnnouncementHotkeyCaptureSlot(MapQuickAnnouncementSettings.HotkeySlot2Id),
                MapQuickAnnouncementKeyboardSlotTooltip) ?? hovered;
            DrawMapQuickAnnouncementSeparator(spriteBatch, area.Viewport, layout[3]);

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                layout[4],
                "map-quick-announcement-key:trigger",
                "快捷宣告:触发键",
                BuildMapQuickAnnouncementKeyText(hotkey.TriggerKey, MapQuickAnnouncementSettings.HotkeyTriggerId),
                IsMapQuickAnnouncementHotkeyCaptureSlot(MapQuickAnnouncementSettings.HotkeyTriggerId),
                MapQuickAnnouncementTriggerSlotTooltip) ?? hovered;

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                layout[5],
                "map-quick-announcement-mode:On",
                "快捷宣告:开启",
                "开启",
                settings.MapQuickAnnouncementEnabled,
                MapQuickAnnouncementOnTooltip) ?? hovered;

            hovered = DrawMapQuickAnnouncementKeyButton(
                context,
                area.Viewport,
                layout[6],
                "map-quick-announcement-mode:Off",
                "快捷宣告:关闭",
                "关闭",
                !settings.MapQuickAnnouncementEnabled,
                MapQuickAnnouncementOffTooltip) ?? hovered;

            hovered = DrawFeatureToggleHotkeyButton(context, row, targetId) ?? hovered;
            return context.HoveredElement ?? hovered;
        }

        private static LegacyUiRect[] CalculateMapQuickAnnouncementLayout(LegacyUiRect row)
        {
            var buttonY = RowModeButtonY(row);
            var onWidth = ModeButtonWidth("开启");
            var offWidth = ModeButtonWidth("关闭");
            var totalWidth =
                MapQuickAnnouncementSlotWidth * 3 +
                MapQuickAnnouncementSeparatorWidth * 2 +
                onWidth +
                offWidth +
                MapQuickAnnouncementGap * 6;
            var x = row.Right - totalWidth - 10 - GetFeatureToggleHotkeyReserveWidth("map.quick_announcement");
            var rects = new LegacyUiRect[7];
            rects[0] = new LegacyUiRect(x, buttonY, MapQuickAnnouncementSlotWidth, RowModeButtonHeight);
            x += MapQuickAnnouncementSlotWidth + MapQuickAnnouncementGap;
            rects[1] = new LegacyUiRect(x, buttonY, MapQuickAnnouncementSeparatorWidth, RowModeButtonHeight);
            x += MapQuickAnnouncementSeparatorWidth + MapQuickAnnouncementGap;
            rects[2] = new LegacyUiRect(x, buttonY, MapQuickAnnouncementSlotWidth, RowModeButtonHeight);
            x += MapQuickAnnouncementSlotWidth + MapQuickAnnouncementGap;
            rects[3] = new LegacyUiRect(x, buttonY, MapQuickAnnouncementSeparatorWidth, RowModeButtonHeight);
            x += MapQuickAnnouncementSeparatorWidth + MapQuickAnnouncementGap;
            rects[4] = new LegacyUiRect(x, buttonY, MapQuickAnnouncementSlotWidth, RowModeButtonHeight);
            x += MapQuickAnnouncementSlotWidth + MapQuickAnnouncementGap;
            rects[5] = new LegacyUiRect(x, buttonY, onWidth, RowModeButtonHeight);
            x += onWidth + MapQuickAnnouncementGap;
            rects[6] = new LegacyUiRect(x, buttonY, offWidth, RowModeButtonHeight);
            return rects;
        }

        private static void DrawMapQuickAnnouncementSeparator(object spriteBatch, LegacyUiRect clip, LegacyUiRect rect)
        {
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                "+",
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                205,
                218,
                238,
                235,
                0.74f);
        }

        private static LegacyUiElement DrawMapQuickAnnouncementKeyButton(
            LegacyUiContext context,
            LegacyUiRect clip,
            LegacyUiRect rect,
            string id,
            string label,
            string text,
            bool selected,
            string tooltip)
        {
            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var button = new LegacyButtonControl
            {
                Id = id,
                Label = label,
                Text = text,
                ElementLabel = label,
                Kind = "button",
                Bounds = elementRect,
                Selected = selected,
                TextScale = ResolveMapQuickAnnouncementKeyTextScale(text, elementRect.Width - 8),
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            };
            var element = button.Draw(context);
            return element != null && context.IsElementHovered(id, elementRect) ? element : null;
        }

        private static string BuildMapQuickAnnouncementKeyText(string token, string slotId = "")
        {
            if (IsMapQuickAnnouncementHotkeyCaptureSlot(slotId))
            {
                return "录入";
            }

            var display = MapQuickAnnouncementSettings.DisplayKey(token);
            return string.IsNullOrWhiteSpace(display) ? "空" : display;
        }

        private static bool IsMapQuickAnnouncementHotkeyCaptureSlot(string slotId)
        {
            var normalized = MapQuickAnnouncementSettings.NormalizeHotkeySlotId(slotId);
            return normalized.Length > 0 &&
                   string.Equals(_mapQuickAnnouncementHotkeyCaptureSlot, normalized, StringComparison.Ordinal);
        }

        private static float ResolveMapQuickAnnouncementKeyTextScale(string text, int availableWidth)
        {
            var scale = 0.72f;
            if (availableWidth <= 0 || string.IsNullOrWhiteSpace(text))
            {
                return scale;
            }

            var width = UiTextRenderer.EstimateTextWidth(text, scale);
            return width <= availableWidth ? scale : Math.Max(0.56f, scale * availableWidth / Math.Max(1, width));
        }

        internal static string BuildMapQuickAnnouncementHotkeyDisplayForTesting(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var hotkey = MapQuickAnnouncementSettings.NormalizeHotkey(
                settings.MapQuickAnnouncementHotkeySlot1,
                settings.MapQuickAnnouncementHotkeySlot2,
                settings.MapQuickAnnouncementTriggerKey);
            return BuildMapQuickAnnouncementKeyText(hotkey.Slot1) + "|" +
                   BuildMapQuickAnnouncementKeyText(hotkey.Slot2) + "|" +
                   BuildMapQuickAnnouncementKeyText(hotkey.TriggerKey);
        }

        internal static int CalculateMapMarkerListContentYForTesting()
        {
            return CalculateMapMarkerListContentY();
        }

        internal static int GetMapEnhancementRowContentYForTesting(string rowName)
        {
            switch (rowName)
            {
                case "死亡信息":
                    return CalculateMapEnhancementRowContentY(MapDeathHistoryRowIndex);
                case "世界天数":
                    return CalculateMapEnhancementRowContentY(MapWorldDayCountRowIndex);
                case "揭示区域":
                    return CalculateMapEnhancementRowContentY(MapRevealedAreaRatioRowIndex);
                case "死亡点常驻":
                    return CalculateMapEnhancementRowContentY(MapPersistentDeathMarkersRowIndex);
                case "足迹":
                    return CalculateMapEnhancementRowContentY(MapFootprintsDisplayRowIndex);
                case "稀有生物显示方向":
                    return CalculateMapEnhancementRowContentY(MapRareCreatureDirectionRowIndex);
                case "旅商显示方向":
                    return CalculateMapEnhancementRowContentY(MapTravellingMerchantDirectionRowIndex);
                case "快捷宣告":
                    return CalculateMapEnhancementRowContentY(MapQuickAnnouncementRowIndex);
                case "地图标记":
                    return CalculateMapEnhancementRowContentY(MapCustomMarkersRowIndex);
                default:
                    return -1;
            }
        }

        internal static string[] GetMapEnhancementRowOrderForTesting()
        {
            return new[]
            {
                "死亡信息",
                "世界天数",
                "揭示区域",
                "死亡点常驻",
                "足迹",
                "稀有生物显示方向",
                "旅商显示方向",
                "快捷宣告",
                "地图标记"
            };
        }

        internal static LegacyUiRect[] CalculateMapDeathHistoryButtonRectsForTesting(LegacyUiRect row)
        {
            LegacyUiRect detailsRect;
            LegacyUiRect countRect;
            CalculateMapDeathHistoryButtonRects(row, out detailsRect, out countRect);
            return new[] { detailsRect, countRect };
        }

        internal static LegacyUiRect[] CalculateMapQuickAnnouncementLayoutForTesting(LegacyUiRect row)
        {
            return CalculateMapQuickAnnouncementLayout(row);
        }

        internal static string BuildMapCustomMarkersLabelForTesting(int markerCount)
        {
            return BuildMapCustomMarkersLabel(markerCount);
        }

        internal static string[] GetMapQuickAnnouncementButtonTooltipsForTesting()
        {
            return new[]
            {
                MapQuickAnnouncementKeyboardSlotTooltip,
                MapQuickAnnouncementKeyboardSlotTooltip,
                MapQuickAnnouncementTriggerSlotTooltip,
                MapQuickAnnouncementOnTooltip,
                MapQuickAnnouncementOffTooltip
            };
        }

        internal static string GetMapPersistentDeathMarkersTooltipForTesting()
        {
            return MapPersistentDeathMarkersTooltip;
        }

        internal static string[] GetMapPersistentDeathMarkersButtonTooltipsForTesting()
        {
            return new[]
            {
                MapPersistentDeathMarkersTooltip,
                string.Empty
            };
        }

        internal static string[] GetMapCustomMarkersButtonTooltipsForTesting()
        {
            return new[]
            {
                MapCustomMarkersOnTooltip,
                string.Empty
            };
        }

        internal static string[] GetMapFootprintsDisplayButtonTooltipsForTesting()
        {
            return new[]
            {
                MapFootprintsDisplayTooltip,
                string.Empty
            };
        }

        internal static string[] GetMapDirectionHintButtonTooltipsForTesting()
        {
            return new[]
            {
                MapRareCreatureDirectionTooltip,
                string.Empty,
                MapTravellingMerchantDirectionTooltip,
                string.Empty
            };
        }

        internal static string GetMapDeathHistoryDetailsTooltipForTesting()
        {
            return string.Empty;
        }

        internal static int GetMapDeathHistoryPageSizeForTesting()
        {
            return MapDeathHistoryPopupPageSize;
        }

        internal static string BuildMapDeathHistoryCountTextForTesting(PlayerWorldDeathHistoryReadResult summary)
        {
            return BuildMapDeathHistoryCountText(summary);
        }

        internal static string BuildMapWorldDayCountTextForTesting(PlayerWorldPlaytimeReadResult playtime)
        {
            return BuildMapWorldDayCountText(playtime);
        }

        internal static string GetMapWorldDayCountTooltipForTesting()
        {
            return MapWorldDayCountTooltip;
        }

        internal static string BuildMapRevealedAreaRatioTextForTesting(PlayerWorldExplorationReadResult exploration)
        {
            return BuildMapRevealedAreaRatioText(exploration);
        }

        internal static string[] BuildMapRevealedAreaRatioTooltipLinesForTesting(PlayerWorldExplorationReadResult exploration)
        {
            return BuildMapRevealedAreaRatioTooltipLines(exploration);
        }

        internal static string GetMapRevealedAreaRatioTooltipForTesting()
        {
            return MapRevealedAreaRatioClickTooltip;
        }

        internal static bool RegisterMapDeathHistoryPopupOverlayForTesting(LegacyScrollArea area, LegacyUiRect anchor, int pageIndex)
        {
            _mapDeathHistoryPopupOpen = true;
            _mapDeathHistoryAnchor = anchor;
            _mapDeathHistoryAnchorVisible = true;
            _mapDeathHistoryPageIndex = Math.Max(0, pageIndex);
            return RegisterMapDeathHistoryPopupOverlay(area);
        }

        private sealed class MapDeathHistoryPopupDrawState
        {
            public LegacyScrollArea Area { get; set; }
            public PlayerWorldDeathHistoryReadResult Page { get; set; }
        }
    }
}
