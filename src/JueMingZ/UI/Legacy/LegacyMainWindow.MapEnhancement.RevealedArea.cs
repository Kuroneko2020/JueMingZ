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
        private static bool RegisterMapRevealedAreaDetailsPopupOverlay(LegacyScrollArea area, PlayerWorldExplorationReadResult exploration)
        {
            if (!_mapRevealedAreaDetailsPopupOpen || !_mapRevealedAreaDetailsAnchorVisible || area == null)
            {
                return false;
            }

            var popup = CalculateMapRevealedAreaDetailsPopupRect(area.Viewport, _mapRevealedAreaDetailsAnchor);
            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = "map-revealed-area-details-popup",
                OwnerPageId = "map_enhancement",
                Bounds = popup,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 21,
                CacheSignature = BuildMapRevealedAreaDetailsPopupCacheSignature(exploration, popup),
                State = new MapRevealedAreaDetailsPopupDrawState
                {
                    Area = area,
                    Exploration = exploration
                },
                Draw = DrawMapRevealedAreaDetailsPopupOverlay
            });
        }

        private static void DrawMapRevealedAreaDetailsPopupOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var state = request == null ? null : request.State as MapRevealedAreaDetailsPopupDrawState;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || state == null || state.Area == null || elements == null)
            {
                return;
            }

            DrawMapRevealedAreaDetailsPopup(context.SpriteBatch, state.Area, context.Mouse, elements, state.Exploration);
        }

        private static LegacyUiElement DrawMapRevealedAreaDetailsPopup(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            PlayerWorldExplorationReadResult exploration)
        {
            if (!_mapRevealedAreaDetailsPopupOpen || !_mapRevealedAreaDetailsAnchorVisible || area == null)
            {
                return null;
            }

            exploration = exploration ?? new PlayerWorldExplorationReadResult();
            var popup = CalculateMapRevealedAreaDetailsPopupRect(area.Viewport, _mapRevealedAreaDetailsAnchor);
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            new LegacyPopupPanelControl
            {
                Id = "map-revealed-area-details-popup",
                Label = "揭示区域",
                Kind = "blocker",
                Bounds = popup
            }.Draw(context);

            UiTextRenderer.DrawText(spriteBatch, "揭示区域", popup.X + 16, popup.Y + 11, 238, 238, 226, 255, 0.82f);
            var hovered = (LegacyUiElement)null;
            hovered = DrawMapRevealedAreaDetailsSmallButton(
                spriteBatch,
                mouse,
                elements,
                new LegacyUiRect(popup.Right - 54, popup.Y + 8, 40, 20),
                "map-revealed-area-ratio:close",
                "关闭",
                "关闭揭示区域详情") ?? hovered;

            var lines = BuildMapRevealedAreaDetailsLines(exploration);
            var textY = popup.Y + 44;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = UiTextRenderer.Ellipsize(lines[index] ?? string.Empty, popup.Width - 32, 0.66f);
                UiTextRenderer.DrawText(spriteBatch, line, popup.X + 16, textY + index * 24, 218, 230, 244, 235, 0.66f);
            }

            var footerY = popup.Bottom - 38;
            var mode = NormalizeMapRevealedAreaMode(exploration);
            var scanning = IsMapRevealedAreaScanning(exploration);
            var controlText = scanning ? "停止扫描" : "开始扫描";
            var startEnabled = scanning || IsMapRevealedAreaStartEnabled(exploration);

            hovered = DrawMapRevealedAreaDetailsSmallButton(
                spriteBatch,
                mouse,
                elements,
                new LegacyUiRect(popup.X + 16, footerY, 64, 24),
                "map-revealed-area-ratio:mode:performance",
                "性能",
                "切换到性能模式",
                true,
                string.Equals(mode, PlayerWorldExplorationScanModes.Performance, StringComparison.Ordinal)) ?? hovered;
            hovered = DrawMapRevealedAreaDetailsSmallButton(
                spriteBatch,
                mouse,
                elements,
                new LegacyUiRect(popup.X + 88, footerY, 64, 24),
                "map-revealed-area-ratio:mode:fast",
                "快速",
                "切换到快速模式",
                true,
                string.Equals(mode, PlayerWorldExplorationScanModes.Fast, StringComparison.Ordinal)) ?? hovered;
            hovered = DrawMapRevealedAreaDetailsSmallButton(
                spriteBatch,
                mouse,
                elements,
                new LegacyUiRect(popup.Right - 112, footerY, 96, 24),
                scanning ? "map-revealed-area-ratio:pause" : "map-revealed-area-ratio:start",
                controlText,
                startEnabled ? (scanning ? "停止扫描" : "开始扫描") : "当前身份或地图不可用",
                startEnabled) ?? hovered;

            return hovered;
        }

        private static LegacyUiElement DrawMapRevealedAreaDetailsSmallButton(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect rect,
            string id,
            string label,
            string tooltip,
            bool enabled = true,
            bool selected = false)
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
                Selected = selected,
                TextScale = ResolveMapRevealedAreaDetailsButtonTextScale(label, rect.Width - 8),
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            }.Draw(context);
            return element != null && context.IsElementHovered(id, rect) ? element : null;
        }

        private static string[] BuildMapRevealedAreaDetailsLines(PlayerWorldExplorationReadResult exploration)
        {
            if (exploration == null || !exploration.IdentityResolved)
            {
                return new[]
                {
                    MapRevealedAreaRatioTooltip,
                    "已揭示 -- / --",
                    "当前玩家-世界身份不可用"
                };
            }

            var total = Math.Max(0L, exploration.TotalTileCount);
            var revealed = Math.Max(0L, exploration.RevealedTileCount);
            var countLine = "已揭示 " + revealed.ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture);
            if (IsMapRevealedAreaMapUnavailable(exploration))
            {
                return new[] { MapRevealedAreaRatioTooltip, countLine, "当前地图不可用" };
            }

            if (exploration.ReadFailed)
            {
                return new[] { MapRevealedAreaRatioTooltip, countLine, "exploration-summary.json 读取失败" };
            }

            if (total <= 0L)
            {
                return new[] { MapRevealedAreaRatioTooltip, countLine, "等待地图扫描" };
            }

            var scanned = Math.Max(0L, exploration.ScannedTileCount).ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture);
            if (!exploration.ScanComplete)
            {
                var prefix = string.Equals(exploration.ControlState, PlayerWorldExplorationControlStates.PausedByUser, StringComparison.Ordinal)
                    ? "已停止 "
                    : "扫描中 ";
                return new[] { MapRevealedAreaRatioTooltip, countLine, prefix + scanned };
            }

            var completed = exploration.LastCompletedScanUtc.HasValue
                ? exploration.LastCompletedScanUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : "尚未记录";
            return new[] { MapRevealedAreaRatioTooltip, countLine, "上次统计 " + completed };
        }

        private static LegacyUiRect CalculateMapRevealedAreaDetailsPopupRect(LegacyUiRect viewport, LegacyUiRect anchor)
        {
            var width = Math.Min(MapRevealedAreaDetailsPopupMaxWidth, Math.Max(MapRevealedAreaDetailsPopupMinWidth, viewport.Width - 24));
            width = Math.Min(width, Math.Max(1, viewport.Width - 12));
            var height = Math.Min(MapRevealedAreaDetailsPopupHeight, Math.Max(168, viewport.Height - 12));
            var x = ClampInt(anchor.X - width + anchor.Width, viewport.X + 6, viewport.Right - width - 6);
            var y = anchor.Bottom + 8;
            if (y + height > viewport.Bottom - 6)
            {
                y = anchor.Y - height - 8;
            }

            y = ClampInt(y, viewport.Y + 6, viewport.Bottom - height - 6);
            return new LegacyUiRect(x, y, width, height);
        }

        private static int BuildMapRevealedAreaDetailsPopupCacheSignature(PlayerWorldExplorationReadResult exploration, LegacyUiRect popup)
        {
            unchecked
            {
                var hash = 17;
                AddHash(ref hash, _mapRevealedAreaDetailsPopupOpen);
                AddHash(ref hash, popup.X);
                AddHash(ref hash, popup.Y);
                AddHash(ref hash, popup.Width);
                AddHash(ref hash, popup.Height);
                AddHash(ref hash, exploration == null ? 0 : exploration.DataSignature);
                AddHash(ref hash, exploration == null ? string.Empty : exploration.Status);
                AddHash(ref hash, exploration == null ? string.Empty : exploration.ScanMode);
                AddHash(ref hash, exploration == null ? string.Empty : exploration.ControlState);
                AddHash(ref hash, exploration != null && exploration.ScanComplete);
                AddHash(ref hash, exploration == null ? 0 : exploration.ScannedTileCount.GetHashCode());
                AddHash(ref hash, exploration == null ? 0 : exploration.TotalTileCount.GetHashCode());
                return hash;
            }
        }

        private static string NormalizeMapRevealedAreaMode(PlayerWorldExplorationReadResult exploration)
        {
            return PlayerWorldExplorationScanModes.Normalize(exploration == null ? string.Empty : exploration.ScanMode);
        }

        private static bool IsMapRevealedAreaScanning(PlayerWorldExplorationReadResult exploration)
        {
            return exploration != null &&
                   exploration.IdentityResolved &&
                   !IsMapRevealedAreaMapUnavailable(exploration) &&
                   string.Equals(exploration.ControlState, PlayerWorldExplorationControlStates.Scanning, StringComparison.Ordinal);
        }

        private static bool IsMapRevealedAreaStartEnabled(PlayerWorldExplorationReadResult exploration)
        {
            return exploration != null &&
                   exploration.IdentityResolved &&
                   !IsMapRevealedAreaMapUnavailable(exploration);
        }

        private static bool IsMapRevealedAreaMapUnavailable(PlayerWorldExplorationReadResult exploration)
        {
            return exploration != null &&
                   !string.IsNullOrWhiteSpace(exploration.Status) &&
                   exploration.Status.IndexOf("mapUnavailable", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float ResolveMapRevealedAreaDetailsButtonTextScale(string text, int availableWidth)
        {
            var scale = 0.70f;
            if (availableWidth <= 0 || string.IsNullOrWhiteSpace(text))
            {
                return scale;
            }

            var width = UiTextRenderer.EstimateTextWidth(text, scale);
            return width <= availableWidth ? scale : Math.Max(0.58f, scale * availableWidth / Math.Max(1, width));
        }

        internal static string[] BuildMapRevealedAreaDetailsLinesForTesting(PlayerWorldExplorationReadResult exploration)
        {
            return BuildMapRevealedAreaDetailsLines(exploration);
        }

        internal static bool RegisterMapRevealedAreaDetailsPopupOverlayForTesting(
            LegacyScrollArea area,
            LegacyUiRect anchor,
            PlayerWorldExplorationReadResult exploration)
        {
            _mapRevealedAreaDetailsPopupOpen = true;
            _mapRevealedAreaDetailsAnchor = anchor;
            _mapRevealedAreaDetailsAnchorVisible = true;
            return RegisterMapRevealedAreaDetailsPopupOverlay(area, exploration);
        }

        internal static string GetMapRevealedAreaDetailsControlButtonTextForTesting(PlayerWorldExplorationReadResult exploration)
        {
            return IsMapRevealedAreaScanning(exploration) ? "停止扫描" : "开始扫描";
        }

        internal static bool IsMapRevealedAreaDetailsStartEnabledForTesting(PlayerWorldExplorationReadResult exploration)
        {
            return IsMapRevealedAreaStartEnabled(exploration);
        }

        private sealed class MapRevealedAreaDetailsPopupDrawState
        {
            public LegacyScrollArea Area { get; set; }
            public PlayerWorldExplorationReadResult Exploration { get; set; }
        }
    }
}
