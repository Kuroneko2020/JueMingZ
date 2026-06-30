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

            var template = snapshot.TemplateSnapshot;
            var maxCells = Math.Min(template.Cells.Count, MaxPreviewWallLayersPerFrame);
            for (var cellIndex = 0; cellIndex < maxCells && layers.Count < MaxPreviewWallLayersPerFrame; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                if (cell == null || cell.Layers == null)
                {
                    continue;
                }

                for (var layerIndex = 0; layerIndex < cell.Layers.Count && layers.Count < MaxPreviewWallLayersPerFrame; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    if (!IsWallLayer(layer))
                    {
                        continue;
                    }

                    layers.Add(new BlueprintProjectionCellSnapshot
                    {
                        InstanceId = snapshot.TemplateId ?? string.Empty,
                        InstanceName = snapshot.TemplateName ?? string.Empty,
                        WorldTileX = snapshot.OriginTileX + cell.X,
                        WorldTileY = snapshot.OriginTileY + cell.Y,
                        RelativeX = cell.X,
                        RelativeY = cell.Y,
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
                        Status = BlueprintProjectionLayerStatuses.Missing
                    });
                }
            }
        }

        private static bool HasDrawableWallLayers(BlueprintPlacementPreviewSnapshot snapshot)
        {
            if (snapshot == null ||
                !snapshot.Active ||
                !snapshot.HoverTileHit ||
                snapshot.TemplateSnapshot == null ||
                snapshot.TemplateSnapshot.Cells == null)
            {
                return false;
            }

            var maxCells = Math.Min(snapshot.TemplateSnapshot.Cells.Count, MaxPreviewWallLayersPerFrame);
            for (var cellIndex = 0; cellIndex < maxCells; cellIndex++)
            {
                var cell = snapshot.TemplateSnapshot.Cells[cellIndex];
                if (cell == null || cell.Layers == null)
                {
                    continue;
                }

                for (var layerIndex = 0; layerIndex < cell.Layers.Count; layerIndex++)
                {
                    if (IsWallLayer(cell.Layers[layerIndex]))
                    {
                        return true;
                    }
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
    }
}
