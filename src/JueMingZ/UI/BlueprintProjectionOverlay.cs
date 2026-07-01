using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using Microsoft.Xna.Framework;

namespace JueMingZ.UI
{
    public static class BlueprintProjectionOverlay
    {
        private const int TileSize = 16;
        private const string VisualContract = "placed-instance-projection+appearance-ghost+original-missing+red-conflict+gray-unavailable+fulfilled-no-mask+completed-progress+no-cell-border+move-floating-follow-preview+hidden-skip+layer-order-cover+wire-actuator-original-color+missing-no-state-block+wall-bottom-layer-complete+wall-world-layer-before-foreground+terraria-foreground-between-wall-and-late-projection+late-overlay-skips-wall+wall-outer-edge-deemphasis+foreground-mask-not-wall-cut+multitile-object-group-conflict+draw-cache-only";

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

                DrawProjectionForegroundPasses(spriteBatch, snapshot, floating);
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

        internal static bool ShouldDrawFallbackBlockForTesting(string status)
        {
            return ShouldDrawFallbackBlock(status);
        }

        internal static bool ShouldDrawProjectionLayerForTesting(string status)
        {
            return BlueprintProjectionGhostRenderer.ShouldDrawLayerForTesting(status);
        }

        internal static int ResolveLayerDrawPassForTesting(string layerKind)
        {
            return BlueprintProjectionGhostRenderer.ResolveLayerDrawPassForTesting(layerKind);
        }

        internal static IReadOnlyList<string> BuildProjectionDrawOrderForTesting(
            BlueprintProjectionSnapshot snapshot,
            BlueprintProjectionSnapshot floating)
        {
            var order = new List<string>();
            AppendProjectionDrawOrderForTesting(order, "world-placed", snapshot, true, false);
            AppendProjectionDrawOrderForTesting(order, "world-floating", floating, true, false);
            AppendProjectionDrawOrderForTesting(order, "placed", snapshot, false, true);
            AppendProjectionDrawOrderForTesting(order, "floating", floating, false, true);
            return order;
        }

        internal static IReadOnlyList<string> BuildWorldWallDrawOrderForTesting(
            BlueprintProjectionSnapshot snapshot,
            BlueprintProjectionSnapshot floating)
        {
            var order = new List<string>();
            AppendProjectionDrawOrderForTesting(order, "world-placed", snapshot, true, false);
            AppendProjectionDrawOrderForTesting(order, "world-floating", floating, true, false);
            return order;
        }

        internal static IReadOnlyList<string> BuildLateProjectionDrawOrderForTesting(
            BlueprintProjectionSnapshot snapshot,
            BlueprintProjectionSnapshot floating)
        {
            var order = new List<string>();
            AppendProjectionDrawOrderForTesting(order, "placed", snapshot, false, true);
            AppendProjectionDrawOrderForTesting(order, "floating", floating, false, true);
            return order;
        }

        internal static IReadOnlyList<string> BuildProjectionPixelStackForTesting(
            BlueprintProjectionSnapshot snapshot,
            BlueprintProjectionSnapshot floating,
            int worldTileX,
            int worldTileY,
            bool includeTerrariaForeground)
        {
            var stack = new List<string>();
            AppendProjectionPixelStackForTesting(stack, "world-placed", snapshot, true, false, worldTileX, worldTileY);
            AppendProjectionPixelStackForTesting(stack, "world-floating", floating, true, false, worldTileX, worldTileY);
            if (includeTerrariaForeground)
            {
                stack.Add("terraria-foreground:" + worldTileX + "," + worldTileY);
            }

            AppendProjectionPixelStackForTesting(stack, "placed", snapshot, false, true, worldTileX, worldTileY);
            AppendProjectionPixelStackForTesting(stack, "floating", floating, false, true, worldTileX, worldTileY);
            return stack;
        }

        internal static void DrawProjectionWorldWallLayer(
            object spriteBatch,
            BlueprintProjectionSnapshot snapshot,
            BlueprintProjectionSnapshot floating)
        {
            DrawProjectionPasses(spriteBatch, snapshot, floating, true, false);
        }

        private static bool ShouldDraw(BlueprintProjectionSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.LoadSucceeded &&
                   snapshot.ProjectedLayers != null &&
                   snapshot.ProjectedLayers.Count > 0;
        }

        private static void DrawProjectionForegroundPasses(
            object spriteBatch,
            BlueprintProjectionSnapshot snapshot,
            BlueprintProjectionSnapshot floating)
        {
            DrawProjectionPasses(spriteBatch, snapshot, floating, false, true);
        }

        private static void DrawProjectionPasses(
            object spriteBatch,
            BlueprintProjectionSnapshot snapshot,
            BlueprintProjectionSnapshot floating,
            bool drawWalls,
            bool drawForeground)
        {
            var clipWidth = Math.Max(1, TerrariaMainCompat.ScreenWidth);
            var clipHeight = Math.Max(1, TerrariaMainCompat.ScreenHeight);
            var screenPosition = TerrariaMainCompat.ScreenPosition;
            for (var pass = 0; pass <= 2; pass++)
            {
                DrawProjectionPass(spriteBatch, snapshot, pass, clipWidth, clipHeight, screenPosition, drawWalls, drawForeground);
                DrawProjectionPass(spriteBatch, floating, pass, clipWidth, clipHeight, screenPosition, drawWalls, drawForeground);
            }
        }

        private static void DrawProjectionPass(
            object spriteBatch,
            BlueprintProjectionSnapshot snapshot,
            int pass,
            int clipWidth,
            int clipHeight,
            Vector2 screenPosition,
            bool drawWalls,
            bool drawForeground)
        {
            if (!ShouldDraw(snapshot))
            {
                return;
            }

            var layers = snapshot.ProjectedLayers;
            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null ||
                    BlueprintProjectionGhostRenderer.ResolveLayerDrawPass(layer.LayerKind) != pass ||
                    !ShouldDrawLayerInPass(layer, drawWalls, drawForeground) ||
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

                if (ShouldDrawFallbackBlock(layer.Status))
                {
                    UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y, TileSize, TileSize, 0, 0, clipWidth, clipHeight, color[0], color[1], color[2], fillAlpha);
                    UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, TileSize, TileSize, 1, 0, 0, clipWidth, clipHeight, color[0], color[1], color[2], borderAlpha);
                }
            }
        }

        private static void AppendProjectionDrawOrderForTesting(
            List<string> order,
            string source,
            BlueprintProjectionSnapshot snapshot,
            bool drawWalls,
            bool drawForeground)
        {
            if (order == null || !ShouldDraw(snapshot))
            {
                return;
            }

            var layers = snapshot.ProjectedLayers;
            for (var pass = 0; pass <= 2; pass++)
            {
                for (var index = 0; index < layers.Count; index++)
                {
                    var layer = layers[index];
                    if (layer == null ||
                        BlueprintProjectionGhostRenderer.ResolveLayerDrawPass(layer.LayerKind) != pass ||
                        !ShouldDrawLayerInPass(layer, drawWalls, drawForeground) ||
                        !BlueprintProjectionGhostRenderer.ShouldDrawLayer(layer.Status))
                    {
                        continue;
                    }

                    order.Add((source ?? string.Empty) + ":" + (layer.LayerKind ?? string.Empty) + ":" + layer.WorldTileX + "," + layer.WorldTileY);
                }
            }
        }

        private static void AppendProjectionPixelStackForTesting(
            List<string> stack,
            string source,
            BlueprintProjectionSnapshot snapshot,
            bool drawWalls,
            bool drawForeground,
            int worldTileX,
            int worldTileY)
        {
            if (stack == null || !ShouldDraw(snapshot))
            {
                return;
            }

            var layers = snapshot.ProjectedLayers;
            for (var pass = 0; pass <= 2; pass++)
            {
                for (var index = 0; index < layers.Count; index++)
                {
                    var layer = layers[index];
                    if (layer == null ||
                        layer.WorldTileX != worldTileX ||
                        layer.WorldTileY != worldTileY ||
                        BlueprintProjectionGhostRenderer.ResolveLayerDrawPass(layer.LayerKind) != pass ||
                        !ShouldDrawLayerInPass(layer, drawWalls, drawForeground) ||
                        !BlueprintProjectionGhostRenderer.ShouldDrawLayer(layer.Status))
                    {
                        continue;
                    }

                    stack.Add((source ?? string.Empty) + ":" + (layer.LayerKind ?? string.Empty) + ":" + layer.WorldTileX + "," + layer.WorldTileY);
                }
            }
        }

        private static bool ShouldDrawLayerInPass(BlueprintProjectionCellSnapshot layer, bool drawWalls, bool drawForeground)
        {
            var isWall = layer != null && string.Equals(layer.LayerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase);
            return isWall ? drawWalls : drawForeground;
        }

        internal static int[] ResolveProjectionColor(string status)
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
                return new[] { 255, 255, 255 };
            }

            if (string.Equals(status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
            {
                return new[] { 236, 82, 92 };
            }

            return new[] { 156, 164, 180 };
        }

        internal static bool ShouldDrawFallbackBlock(string status)
        {
            return !string.Equals(status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal) &&
                   !string.Equals(status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal) &&
                   !string.Equals(status, BlueprintProjectionLayerStatuses.Completed, StringComparison.Ordinal);
        }

        internal static int ResolveProjectionFillAlpha(string status)
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

        internal static int ResolveProjectionBorderAlpha(string status)
        {
            return 0;
        }
    }
}
