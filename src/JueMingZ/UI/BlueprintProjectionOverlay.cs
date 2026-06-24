using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using Microsoft.Xna.Framework;

namespace JueMingZ.UI
{
    public static class BlueprintProjectionOverlay
    {
        private const int TileSize = 16;
        private const int MaxDrawLayersPerFrame = 1536;
        private const string VisualContract = "placed-instance-projection+appearance-ghost+yellow-missing+red-conflict+fulfilled-no-mask+completed-progress+no-cell-border+move-floating-follow-preview+hidden-skip+layer-order-cover+draw-cache-only";

        public static bool DrawInterfaceLayer()
        {
            try
            {
                var snapshot = BlueprintProjectionService.GetCachedSnapshotForDraw();
                var floating = BlueprintPlacedInstanceTransformState.GetFloatingProjectionForDraw();
                if (!ShouldDraw(snapshot) && !ShouldDraw(floating))
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("BlueprintProjectionOverlay", false, out spriteBatch))
                {
                    return true;
                }

                DrawProjection(spriteBatch, snapshot);
                DrawProjection(spriteBatch, floating);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("BlueprintProjectionOverlay", error);
                LogThrottle.ErrorThrottled(
                    "blueprint-projection-overlay-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintProjectionOverlay",
                    "Blueprint projection overlay draw failed; exception swallowed.", error);
            }

            return true;
        }

        internal static bool ShouldRegisterWorldOverlayForTesting()
        {
            return true;
        }

        internal static string GetVisualContractForTesting()
        {
            return VisualContract;
        }

        internal static int[] ResolveProjectionColorForTesting(string status)
        {
            return ResolveProjectionColor(status);
        }

        internal static int ResolveProjectionFillAlphaForTesting(string status)
        {
            return ResolveProjectionFillAlpha(status);
        }

        internal static int ResolveProjectionBorderAlphaForTesting(string status)
        {
            return ResolveProjectionBorderAlpha(status);
        }

        internal static bool ShouldDrawProjectionLayerForTesting(string status)
        {
            return BlueprintProjectionGhostRenderer.ShouldDrawLayerForTesting(status);
        }

        internal static int ResolveLayerDrawPassForTesting(string layerKind)
        {
            return BlueprintProjectionGhostRenderer.ResolveLayerDrawPassForTesting(layerKind);
        }

        private static bool ShouldDraw(BlueprintProjectionSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.LoadSucceeded &&
                   snapshot.ProjectedLayers != null &&
                   snapshot.ProjectedLayers.Count > 0;
        }

        private static void DrawProjection(object spriteBatch, BlueprintProjectionSnapshot snapshot)
        {
            if (!ShouldDraw(snapshot))
            {
                return;
            }

            var clipWidth = Math.Max(1, TerrariaMainCompat.ScreenWidth);
            var clipHeight = Math.Max(1, TerrariaMainCompat.ScreenHeight);
            var screenPosition = TerrariaMainCompat.ScreenPosition;
            var layers = snapshot.ProjectedLayers;
            var max = Math.Min(layers.Count, MaxDrawLayersPerFrame);
            for (var pass = 0; pass <= 2; pass++)
            {
                for (var index = 0; index < max; index++)
                {
                    var layer = layers[index];
                    if (layer == null ||
                        BlueprintProjectionGhostRenderer.ResolveLayerDrawPass(layer.LayerKind) != pass ||
                        !BlueprintProjectionGhostRenderer.ShouldDrawLayer(layer.Status))
                    {
                        continue;
                    }

                    var x = (int)Math.Round(layer.WorldTileX * TileSize - screenPosition.X);
                    var y = (int)Math.Round(layer.WorldTileY * TileSize - screenPosition.Y);
                    if (x >= clipWidth || y >= clipHeight || x + TileSize <= 0 || y + TileSize <= 0)
                    {
                        continue;
                    }

                    var color = ResolveProjectionColor(layer.Status);
                    var fillAlpha = ResolveProjectionFillAlpha(layer.Status);
                    var borderAlpha = ResolveProjectionBorderAlpha(layer.Status);
                    if (BlueprintProjectionGhostRenderer.DrawLayer(spriteBatch, layer, x, y, clipWidth, clipHeight, color[0], color[1], color[2], fillAlpha, borderAlpha))
                    {
                        continue;
                    }

                    UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, TileSize, TileSize, 0, 0, clipWidth, clipHeight, color[0], color[1], color[2], fillAlpha);
                    UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, TileSize, TileSize, 1, 0, 0, clipWidth, clipHeight, color[0], color[1], color[2], borderAlpha);
                }
            }
        }

        private static int[] ResolveProjectionColor(string status)
        {
            if (string.Equals(status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal))
            {
                return new[] { 0, 0, 0 };
            }

            if (string.Equals(status, BlueprintProjectionLayerStatuses.Completed, StringComparison.Ordinal))
            {
                return new[] { 0, 0, 0 };
            }

            if (string.Equals(status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal))
            {
                return new[] { 248, 216, 72 };
            }

            if (string.Equals(status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
            {
                return new[] { 236, 82, 92 };
            }

            return new[] { 156, 164, 180 };
        }

        private static int ResolveProjectionFillAlpha(string status)
        {
            if (string.Equals(status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(status, BlueprintProjectionLayerStatuses.Completed, StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
            {
                return 176;
            }

            if (string.Equals(status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal))
            {
                return 142;
            }

            return 118;
        }

        private static int ResolveProjectionBorderAlpha(string status)
        {
            return 0;
        }
    }
}
