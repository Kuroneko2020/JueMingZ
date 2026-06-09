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
        private static int BuildFishingFilterFloatingHeight(LegacyScrollArea area, LegacyUiRect anchor, int maxHeight, int minHeight)
        {
            var availableAbove = Math.Max(0, anchor.Y - area.Viewport.Y - FishingFilterFloatingGap - 4);
            var preferredMin = Math.Min(Math.Max(1, minHeight), Math.Max(1, maxHeight));
            if (availableAbove <= 0)
            {
                return preferredMin;
            }

            return Math.Min(Math.Max(preferredMin, availableAbove), Math.Max(preferredMin, maxHeight));
        }

        private static LegacyUiRect BuildFishingFilterFloatingRect(LegacyScrollArea area, LegacyUiRect host, LegacyUiRect anchor, int height)
        {
            var width = Math.Max(140, host.Width - 20);
            width = Math.Min(width, Math.Max(80, area.Viewport.Width - 8));
            var x = host.X + 10;
            x = Math.Max(area.Viewport.X + 4, Math.Min(x, area.Viewport.Right - width - 4));
            var y = anchor.Y - height - FishingFilterFloatingGap;
            y = Math.Max(area.Viewport.Y + 4, y);
            return new LegacyUiRect(x, y, width, height);
        }

        private static void DrawFishingFilterFloatingConnector(object spriteBatch, LegacyScrollArea area, LegacyUiRect popup, LegacyUiRect anchor)
        {
            var centerX = anchor.X + anchor.Width / 2;
            centerX = Math.Max(popup.X + 12, Math.Min(popup.Right - 12, centerX));
            var stemTop = popup.Bottom - 1;
            var stemBottom = anchor.Y + 1;
            if (stemBottom <= stemTop)
            {
                return;
            }

            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, centerX - 1, stemTop, 2, stemBottom - stemTop, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, FishingFilterLinkR, FishingFilterLinkG, FishingFilterLinkB, FishingFilterLinkA);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, centerX - 4, popup.Bottom - 3, 8, 3, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, FishingFilterLinkR, FishingFilterLinkG, FishingFilterLinkB, FishingFilterLinkA);
        }

        private static void DrawFishingFilterFloatingBorder(object spriteBatch, LegacyScrollArea area, LegacyUiRect rect)
        {
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 1, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, FishingFilterLinkR, FishingFilterLinkG, FishingFilterLinkB, FishingFilterLinkA);
        }

        private static bool RegisterFishingFilterExactPickerOverlay(LegacyScrollArea area, LegacyUiRect picker, LegacyUiRect anchor)
        {
            if (area == null || picker.Width <= 0 || picker.Height <= 0)
            {
                FishingFilterUiState.ClearPickerViewport();
                return false;
            }

            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = "fishing-filter-exact-picker",
                OwnerPageId = "fishing",
                Bounds = picker,
                Kind = LegacyUiOverlayKind.Popup,
                ZIndex = 30,
                CacheSignature = BuildFishingFilterPickerOverlayCacheSignature(),
                State = new FishingFilterOverlayDrawState
                {
                    Area = area,
                    Anchor = anchor
                },
                Draw = DrawFishingFilterExactPickerOverlay
            });
        }

        private static bool RegisterFishingFilterPresetListOverlay(LegacyScrollArea area, LegacyUiRect rect, LegacyUiRect anchor, AppSettings settings, string filterMode, string matchMode)
        {
            if (area == null || rect.Width <= 0 || rect.Height <= 0)
            {
                FishingFilterUiState.ClearPresetViewport();
                return false;
            }

            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = "fishing-filter-preset-list",
                OwnerPageId = "fishing",
                Bounds = rect,
                Kind = LegacyUiOverlayKind.Popup,
                ZIndex = 20,
                CacheSignature = BuildFishingFilterPresetOverlayCacheSignature(settings, filterMode, matchMode),
                State = new FishingFilterOverlayDrawState
                {
                    Area = area,
                    Anchor = anchor,
                    Settings = settings,
                    FilterMode = filterMode,
                    MatchMode = matchMode
                },
                Draw = DrawFishingFilterPresetListOverlay
            });
        }

        private static void DrawFishingFilterExactPickerOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var state = request == null ? null : request.State as FishingFilterOverlayDrawState;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || state == null || state.Area == null || elements == null)
            {
                return;
            }

            DrawFishingFilterFloatingConnector(context.SpriteBatch, state.Area, request.Bounds, state.Anchor);
            DrawFishingFilterExactPicker(context.SpriteBatch, state.Area, context.Mouse, elements, request.Bounds);
        }

        private static void DrawFishingFilterPresetListOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var state = request == null ? null : request.State as FishingFilterOverlayDrawState;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || state == null || state.Area == null || elements == null)
            {
                return;
            }

            DrawFishingFilterFloatingConnector(context.SpriteBatch, state.Area, request.Bounds, state.Anchor);
            DrawFishingFilterPresetList(
                context.SpriteBatch,
                state.Area,
                context.Mouse,
                elements,
                request.Bounds,
                state.Settings ?? context.Settings ?? AppSettings.CreateDefault(),
                state.FilterMode,
                state.MatchMode);
        }

        private static int BuildFishingFilterPickerOverlayCacheSignature()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + FishingFilterUiState.PickerCandidateCount;
                hash = hash * 31 + FishingFilterUiState.PickerSelectedCount;
                hash = hash * 31 + FishingFilterUiState.PickerScrollOffset;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(FishingFilterUiState.PickerSource ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(FishingFilterUiState.GlobalSearchQuery ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(FishingFilterUiState.PickerMessage ?? string.Empty);
                return hash;
            }
        }

        private static int BuildFishingFilterPresetOverlayCacheSignature(AppSettings settings, string filterMode, string matchMode)
        {
            unchecked
            {
                settings = settings ?? AppSettings.CreateDefault();
                var hash = 17;
                hash = hash * 31 + settings.ConfigVersion;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(FishingFilterModes.Normalize(filterMode) ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(FishingFilterMatchModes.Normalize(matchMode) ?? string.Empty);
                hash = hash * 31 + Count(settings.FishingFilterPresets);
                hash = hash * 31 + FishingFilterUiState.PresetScrollOffset;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(FishingFilterUiState.PresetSaveNotice ?? string.Empty);
                return hash;
            }
        }

        internal static bool RegisterFishingFilterExactPickerOverlayForTesting(LegacyScrollArea area, LegacyUiRect picker, LegacyUiRect anchor)
        {
            return RegisterFishingFilterExactPickerOverlay(area, picker, anchor);
        }

        internal static bool RegisterFishingFilterPresetListOverlayForTesting(LegacyScrollArea area, LegacyUiRect rect, LegacyUiRect anchor, AppSettings settings, string filterMode, string matchMode)
        {
            return RegisterFishingFilterPresetListOverlay(area, rect, anchor, settings, filterMode, matchMode);
        }
    }
}
