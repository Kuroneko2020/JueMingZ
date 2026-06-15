using System;
using System.Globalization;
using JueMingZ.Automation.MapEnhancement;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.Records;
using JueMingZ.UI.Legacy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace JueMingZ.UI
{
    internal static class MapCustomMarkerStylePickerOverlay
    {
        internal const string UiOverlayRoute = "uiOverlay";
        internal const string FullscreenMapRoute = "fullscreenMap";
        private const string VisualContract = "icon-cells-only";
        private const int CellSize = 34;
        private const int CellGap = 6;
        private const int Padding = 0;
        private const int HoverTextHeight = 20;

        public static bool DrawInterfaceLayer()
        {
            var placement = MapCustomMarkerInteractionService.GetPlacementSnapshot();
            if (!ShouldUseDrawRoute(placement != null, Main.mapFullscreen))
            {
                return true;
            }

            object spriteBatch;
            if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("MapCustomMarkerStylePickerOverlay", true, out spriteBatch))
            {
                PlayerWorldMapMarkerDiagnostics.RecordPickerDrawSkipped(UiOverlayRoute, "interfaceSpriteBatchUnavailable");
                return true;
            }

            DrawPickerSafe(spriteBatch, placement, UiOverlayRoute);
            return true;
        }

        public static void DrawFullscreenMapLayer(Vector2 mapTopLeft, float scale)
        {
            var transform = MapCustomMarkerMapCompat.RecordFullscreenTransform(
                mapTopLeft,
                scale,
                TerrariaMainCompat.ScreenWidth,
                TerrariaMainCompat.ScreenHeight,
                FullscreenMapRoute);
            PlayerWorldMapMarkerDiagnostics.RecordFullscreenTransform(transform);

            var placement = MapCustomMarkerInteractionService.GetPlacementSnapshot();
            if (!ShouldUseDrawRoute(placement != null, Main.mapFullscreen))
            {
                return;
            }

            UiInputFrameClock.BeginDrawFrame("MapCustomMarkerFullscreenMap");

            SpriteBatch spriteBatch;
            if (!TerrariaDrawCompat.TryGetSpriteBatch(out spriteBatch))
            {
                PlayerWorldMapMarkerDiagnostics.RecordPickerDrawSkipped(FullscreenMapRoute, "spriteBatchUnavailable");
                return;
            }

            if (!VanillaUiSkinCompat.PrepareForDraw("MapCustomMarkerFullscreenMap"))
            {
                PlayerWorldMapMarkerDiagnostics.RecordPickerDrawSkipped(FullscreenMapRoute, "resourcesNotReady");
                return;
            }

            if (!UiPrimitiveRenderer.EnsureReady(spriteBatch))
            {
                PlayerWorldMapMarkerDiagnostics.RecordPickerDrawSkipped(FullscreenMapRoute, "primitiveRendererNotReady");
                return;
            }

            // Terraria raises OnPostFullscreenMapDraw after ending its map SpriteBatch.
            // This route owns a short UI-scale Begin/End pair and leaves marker storage
            // and the IMapLayer marker body untouched.
            var begun = false;
            try
            {
                spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    Main.SamplerStateForCursor,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise,
                    null,
                    Main.UIScaleMatrix);
                begun = true;
                DrawPickerSafe(spriteBatch, placement, FullscreenMapRoute);
            }
            catch (Exception error)
            {
                PlayerWorldMapMarkerDiagnostics.RecordPickerDrawSkipped(FullscreenMapRoute, begun ? "drawException" : "spriteBatchBeginFailed");
                UiDrawLifecycleGuard.RecordDrawException("MapCustomMarkerFullscreenMap", error);
                LogThrottle.ErrorThrottled(
                    "map-custom-marker-fullscreen-draw-error",
                    TimeSpan.FromSeconds(10),
                    "MapCustomMarkerStylePickerOverlay",
                    "Fullscreen map marker style picker draw failed; exception swallowed.",
                    error);
            }
            finally
            {
                if (begun)
                {
                    try
                    {
                        spriteBatch.End();
                    }
                    catch (Exception error)
                    {
                        PlayerWorldMapMarkerDiagnostics.RecordPickerDrawSkipped(FullscreenMapRoute, "spriteBatchEndFailed");
                        LogThrottle.ErrorThrottled(
                            "map-custom-marker-fullscreen-draw-end-error",
                            TimeSpan.FromSeconds(10),
                            "MapCustomMarkerStylePickerOverlay",
                            "Fullscreen map marker style picker SpriteBatch.End failed; exception swallowed.",
                            error);
                    }
                }
            }
        }

        internal static bool ShouldUseFullscreenMapDrawRouteForTesting(bool pickerOpen, bool mapFullscreen)
        {
            return ShouldUseDrawRoute(pickerOpen, mapFullscreen);
        }

        internal static string[] GetDrawRoutesForTesting()
        {
            return new[] { UiOverlayRoute, FullscreenMapRoute };
        }

        internal static LegacyUiRect CalculatePanelRect(int anchorX, int anchorY, int screenWidth, int screenHeight)
        {
            return CalculatePanelPlacement(anchorX, anchorY, screenWidth, screenHeight).Panel;
        }

        internal static bool CalculatePanelClampedForTesting(int anchorX, int anchorY, int screenWidth, int screenHeight)
        {
            return CalculatePanelPlacement(anchorX, anchorY, screenWidth, screenHeight).Clamped;
        }

        internal static string GetVisualContractForTesting()
        {
            return VisualContract;
        }

        private static MapCustomMarkerPickerPanelPlacement CalculatePanelPlacement(int anchorX, int anchorY, int screenWidth, int screenHeight)
        {
            var styles = PlayerWorldMapMarkerStyles.All;
            var width = Padding * 2 + styles.Count * CellSize + Math.Max(0, styles.Count - 1) * CellGap;
            var height = Padding * 2 + CellSize + HoverTextHeight;
            // User-visible contract: the picker belongs at the right-click
            // point; only screen-edge clamping is allowed to move it.
            var desiredX = anchorX;
            var desiredY = anchorY;
            var x = Clamp(desiredX, 8, Math.Max(8, screenWidth - width - 8));
            var y = Clamp(desiredY, 8, Math.Max(8, screenHeight - height - 8));
            return new MapCustomMarkerPickerPanelPlacement(
                new LegacyUiRect(x, y, width, height),
                x != desiredX || y != desiredY);
        }

        private static bool ShouldUseDrawRoute(bool pickerOpen, bool mapFullscreen)
        {
            return pickerOpen && mapFullscreen;
        }

        private static void DrawPickerSafe(object spriteBatch, MapCustomMarkerPendingPlacement placement, string route)
        {
            try
            {
                DrawPicker(spriteBatch, placement, route);
            }
            catch (Exception error)
            {
                PlayerWorldMapMarkerDiagnostics.RecordPickerDrawSkipped(route, "drawException");
                UiDrawLifecycleGuard.RecordDrawException("MapCustomMarkerStylePickerOverlay", error);
                LogThrottle.ErrorThrottled(
                    "map-custom-marker-style-picker-draw-error-" + route,
                    TimeSpan.FromSeconds(10),
                    "MapCustomMarkerStylePickerOverlay",
                    "Map marker style picker draw failed; exception swallowed.",
                    error);
            }
        }

        private static void DrawPicker(object spriteBatch, MapCustomMarkerPendingPlacement placement, string route)
        {
            PlayerWorldMapMarkerDiagnostics.RecordPickerDraw(route);
            var mouse = LegacyUiInput.ReadMouse();
            var panelPlacement = CalculatePanelPlacement(placement.ScreenX, placement.ScreenY, TerrariaMainCompat.ScreenWidth, TerrariaMainCompat.ScreenHeight);
            var panel = panelPlacement.Panel;
            PlayerWorldMapMarkerDiagnostics.RecordPickerAnchor(placement.ScreenX, placement.ScreenY, panel.X, panel.Y, panelPlacement.Clamped);

            var styles = PlayerWorldMapMarkerStyles.All;
            var hoveredAny = panel.Contains(mouse.X, mouse.Y);
            for (var index = 0; index < styles.Count; index++)
            {
                var style = styles[index];
                var cell = new LegacyUiRect(
                    panel.X + Padding + index * (CellSize + CellGap),
                    panel.Y + Padding,
                    CellSize,
                    CellSize);
                var hovered = cell.Contains(mouse.X, mouse.Y);
                hoveredAny |= hovered;
                DrawStyleCell(spriteBatch, cell, panel, style.IconItemId, hovered);
                if (hovered)
                {
                    UiTextRenderer.DrawText(
                        spriteBatch,
                        style.DisplayName,
                        cell.X,
                        cell.Bottom + 4,
                        232,
                        236,
                        220,
                        245,
                        0.54f);
                    if (mouse.LeftPressed)
                    {
                        MapCustomMarkerInteractionService.RequestStyleSelection(style.IconItemId);
                    }
                }
            }

            if (hoveredAny)
            {
                UiMouseCaptureService.CaptureForOperationWindow();
            }
        }

        private static void DrawStyleCell(object spriteBatch, LegacyUiRect cell, LegacyUiRect clip, int itemType, bool hovered)
        {
            LegacyUiTheme.DrawCellClipped(spriteBatch, cell, hovered, false, false, clip);
            object texture;
            if (VanillaUiSkinCompat.TryGetItemTexture(itemType, out texture))
            {
                UiPrimitiveRenderer.DrawTextureContainedClipped(
                    spriteBatch,
                    texture,
                    cell.X + 3,
                    cell.Y + 3,
                    cell.Width - 6,
                    cell.Height - 6,
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

            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                itemType.ToString(CultureInfo.InvariantCulture),
                cell.X,
                cell.Y + 8,
                cell.Width,
                14,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                232,
                236,
                220,
                255,
                0.46f);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private sealed class MapCustomMarkerPickerPanelPlacement
        {
            public MapCustomMarkerPickerPanelPlacement(LegacyUiRect panel, bool clamped)
            {
                Panel = panel;
                Clamped = clamped;
            }

            public LegacyUiRect Panel { get; private set; }
            public bool Clamped { get; private set; }
        }
    }
}
