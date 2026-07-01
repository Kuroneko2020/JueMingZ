using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using Microsoft.Xna.Framework;

namespace JueMingZ.UI
{
    public static class BlueprintPlacementPreviewWallWorldOverlay
    {
        private const int TileSize = 16;
        private const int MaxPreviewWallLayersPerFrame = 512;
        private const int WallR = 92;
        private const int WallG = 144;
        private const int WallB = 214;
        private const int WallAlpha = 142;
        private const string VisualContract = "blueprint-placement-preview-wall-world-layer+complete-template-wall-target+before-terraria-foreground+before-preview-foreground+late-preview-skips-wall-content+draw-snapshot-only";
        private static readonly object SyncRoot = new object();
        private static readonly List<BlueprintProjectionCellSnapshot> ScratchWallLayers = new List<BlueprintProjectionCellSnapshot>(MaxPreviewWallLayersPerFrame);

        public static bool DrawWorldLayer()
        {
            try
            {
                var snapshot = BlueprintPlacementPreviewState.GetSnapshot();
                if (!HasDrawableWallLayers(snapshot))
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("BlueprintPlacementPreviewWallWorldOverlay", false, out spriteBatch))
                {
                    return true;
                }

                DrawWorldWallContent(spriteBatch, snapshot);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("BlueprintPlacementPreviewWallWorldOverlay", error);
                LogThrottle.ErrorThrottled(
                    "blueprint-placement-preview-wall-world-overlay-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintPlacementPreviewWallWorldOverlay",
                    "Blueprint placement preview wall world layer draw failed; exception swallowed.", error);
            }

            return true;
        }

        internal static string GetVisualContractForTesting()
        {
            return VisualContract;
        }

        internal static IReadOnlyList<string> BuildWorldWallDrawOrderForTesting(BlueprintPlacementPreviewSnapshot snapshot)
        {
            var order = new List<string>();
            AppendWorldWallDrawOrder(order, snapshot);
            return order;
        }

        private static void DrawWorldWallContent(object spriteBatch, BlueprintPlacementPreviewSnapshot snapshot)
        {
            lock (SyncRoot)
            {
                BuildWallLayers(snapshot, ScratchWallLayers);
                BlueprintProjectionService.ApplyBottomWallGhostVisualPolicy(ScratchWallLayers);

                var clipWidth = Math.Max(1, TerrariaMainCompat.ScreenWidth);
                var clipHeight = Math.Max(1, TerrariaMainCompat.ScreenHeight);
                var screenPosition = TerrariaMainCompat.ScreenPosition;
                for (var index = 0; index < ScratchWallLayers.Count; index++)
                {
                    var layer = ScratchWallLayers[index];
                    var x = (int)Math.Round(layer.WorldTileX * TileSize - screenPosition.X);
                    var y = (int)Math.Round(layer.WorldTileY * TileSize - screenPosition.Y);
                    if (x >= clipWidth || y >= clipHeight || x + TileSize <= 0 || y + TileSize <= 0)
                    {
                        continue;
                    }

                    BlueprintProjectionGhostRenderer.DrawLayer(spriteBatch, layer, x, y, clipWidth, clipHeight, WallR, WallG, WallB, WallAlpha, 0);
                }
            }
        }

        private static void AppendWorldWallDrawOrder(List<string> order, BlueprintPlacementPreviewSnapshot snapshot)
        {
            if (order == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                BuildWallLayers(snapshot, ScratchWallLayers);
                BlueprintProjectionService.ApplyBottomWallGhostVisualPolicy(ScratchWallLayers);
                for (var index = 0; index < ScratchWallLayers.Count; index++)
                {
                    var layer = ScratchWallLayers[index];
                    order.Add("world-preview:" + layer.LayerKind + ":" + layer.WorldTileX + "," + layer.WorldTileY);
                }
            }
        }

        private static void BuildWallLayers(BlueprintPlacementPreviewSnapshot snapshot, List<BlueprintProjectionCellSnapshot> layers)
        {
            layers.Clear();
            if (!HasDrawableWallLayers(snapshot))
            {
                return;
            }

            var previewLayers = snapshot.PreviewLayers;
            var maxLayers = Math.Min(previewLayers == null ? 0 : previewLayers.Count, MaxPreviewWallLayersPerFrame);
            for (var layerIndex = 0; layerIndex < maxLayers && layers.Count < MaxPreviewWallLayersPerFrame; layerIndex++)
            {
                var layer = previewLayers[layerIndex];
                if (layer == null ||
                    !IsWallLayer(layer) ||
                    !BlueprintProjectionGhostRenderer.ShouldDrawLayer(layer.Status))
                {
                    continue;
                }

                layers.Add(new BlueprintProjectionCellSnapshot
                {
                    InstanceId = layer.InstanceId ?? string.Empty,
                    InstanceName = layer.InstanceName ?? string.Empty,
                    WorldTileX = layer.WorldTileX,
                    WorldTileY = layer.WorldTileY,
                    RelativeX = layer.RelativeX,
                    RelativeY = layer.RelativeY,
                    LayerKind = BlueprintLayerKinds.Wall,
                    CoverageGroup = BlueprintLayerKinds.Wall,
                    ContentId = layer.ContentId,
                    Style = layer.Style,
                    FrameX = layer.FrameX,
                    FrameY = layer.FrameY,
                    PaintId = layer.PaintId,
                    CoatingFlags = layer.CoatingFlags,
                    Slope = layer.Slope,
                    HalfBrick = layer.HalfBrick,
                    Inactive = layer.Inactive,
                    MaterialItemId = layer.MaterialItemId,
                    MaterialStack = layer.MaterialStack,
                    MaterialDisplayName = layer.MaterialDisplayName ?? string.Empty,
                    Status = layer.Status ?? string.Empty
                });
            }
        }

        private static bool HasDrawableWallLayers(BlueprintPlacementPreviewSnapshot snapshot)
        {
            if (snapshot == null ||
                !snapshot.Active ||
                !snapshot.HoverTileHit ||
                snapshot.PreviewLayers == null)
            {
                return false;
            }

            var maxLayers = Math.Min(snapshot.PreviewLayers.Count, MaxPreviewWallLayersPerFrame);
            for (var index = 0; index < maxLayers; index++)
            {
                var layer = snapshot.PreviewLayers[index];
                if (IsWallLayer(layer) && BlueprintProjectionGhostRenderer.ShouldDrawLayer(layer.Status))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWallLayer(BlueprintCellLayerRecord layer)
        {
            return layer != null &&
                   layer.ContentId > 0 &&
                   string.Equals(layer.LayerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWallLayer(BlueprintProjectionCellSnapshot layer)
        {
            return layer != null &&
                   layer.ContentId > 0 &&
                   string.Equals(layer.LayerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase);
        }
    }
}
