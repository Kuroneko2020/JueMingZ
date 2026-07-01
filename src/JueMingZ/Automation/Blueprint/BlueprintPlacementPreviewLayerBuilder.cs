using System;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintPlacementPreviewLayerBuilder
    {
        public static IReadOnlyList<BlueprintProjectionCellSnapshot> Build(
            BlueprintTemplateRecord template,
            string templateId,
            string templateName,
            int originTileX,
            int originTileY,
            IBlueprintWorldTileReader reader)
        {
            var layers = new List<BlueprintProjectionCellSnapshot>();
            if (template == null || template.Cells == null || template.Cells.Count <= 0)
            {
                return layers;
            }

            var replacementSettings = BlueprintReplacementRuleService.GetSettingsFromCurrentConfig();
            var worldTileCache = new Dictionary<string, BlueprintWorldTileSnapshot>(StringComparer.Ordinal);
            for (var cellIndex = 0; cellIndex < template.Cells.Count; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                if (cell == null || cell.Layers == null)
                {
                    continue;
                }

                for (var layerIndex = 0; layerIndex < cell.Layers.Count; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    if (layer == null)
                    {
                        continue;
                    }

                    var preview = CreatePreviewLayer(
                        template,
                        templateId,
                        templateName,
                        originTileX,
                        originTileY,
                        cell,
                        layer);
                    if (IsLegacyPartialObject(layer))
                    {
                        preview.Status = BlueprintProjectionLayerStatuses.Unavailable;
                    }
                    else
                    {
                        BlueprintWorldTileSnapshot world;
                        preview.Status = ClassifyLayer(layer, reader, replacementSettings, worldTileCache, preview.WorldTileX, preview.WorldTileY, out world);
                    }

                    layers.Add(preview);
                }
            }

            ApplyExplicitObjectGroupPolicy(layers);
            BlueprintProjectionService.ApplyBottomWallGhostVisualPolicy(layers);
            return layers;
        }

        internal static bool IsLegacyPartialObjectForTesting(BlueprintCellLayerRecord layer)
        {
            return IsLegacyPartialObject(layer);
        }

        private static BlueprintProjectionCellSnapshot CreatePreviewLayer(
            BlueprintTemplateRecord template,
            string templateId,
            string templateName,
            int originTileX,
            int originTileY,
            BlueprintCellRecord cell,
            BlueprintCellLayerRecord layer)
        {
            return new BlueprintProjectionCellSnapshot
            {
                InstanceId = templateId ?? string.Empty,
                InstanceName = templateName ?? string.Empty,
                LayerOrder = 0,
                WorldTileX = originTileX + cell.X,
                WorldTileY = originTileY + cell.Y,
                RelativeX = cell.X,
                RelativeY = cell.Y,
                LayerKind = layer.LayerKind ?? string.Empty,
                CoverageGroup = ResolveCoverageGroup(layer.LayerKind),
                ObjectGroupKey = ResolveObjectGroupKey(template, layer),
                ObjectGroupStatus = string.Empty,
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
                MaterialDisplayName = ResolveMaterialDisplayName(template, layer.MaterialItemId)
            };
        }

        private static string ClassifyLayer(
            BlueprintCellLayerRecord layer,
            IBlueprintWorldTileReader reader,
            BlueprintReplacementSettings replacementSettings,
            IDictionary<string, BlueprintWorldTileSnapshot> worldTileCache,
            int worldTileX,
            int worldTileY,
            out BlueprintWorldTileSnapshot world)
        {
            world = null;
            if (!TryReadWorldTile(reader, worldTileCache, worldTileX, worldTileY, out world) || world == null)
            {
                return BlueprintProjectionLayerStatuses.Unavailable;
            }

            var kind = NormalizeLayerKind(layer == null ? string.Empty : layer.LayerKind);
            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyWall(layer, world);
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyWire(layer, world);
            }

            if (string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyActuator(layer, world);
            }

            return ClassifyTile(layer, world, replacementSettings);
        }

        private static bool TryReadWorldTile(
            IBlueprintWorldTileReader reader,
            IDictionary<string, BlueprintWorldTileSnapshot> worldTileCache,
            int tileX,
            int tileY,
            out BlueprintWorldTileSnapshot world)
        {
            world = null;
            bool worldReady;
            try
            {
                worldReady = reader != null && reader.IsWorldReady;
            }
            catch
            {
                return false;
            }

            if (!worldReady)
            {
                return false;
            }

            var key = BuildWorldTileCacheKey(tileX, tileY);
            if (worldTileCache != null && worldTileCache.TryGetValue(key, out world))
            {
                return world != null;
            }

            var read = false;
            try
            {
                read = reader.TryReadTile(tileX, tileY, out world);
            }
            catch
            {
                world = null;
            }

            if (!read || world == null)
            {
                if (worldTileCache != null)
                {
                    worldTileCache[key] = null;
                }

                return false;
            }

            if (worldTileCache != null)
            {
                worldTileCache[key] = world;
            }

            return true;
        }

        private static string ClassifyTile(
            BlueprintCellLayerRecord layer,
            BlueprintWorldTileSnapshot world,
            BlueprintReplacementSettings replacementSettings)
        {
            if (layer == null || world == null)
            {
                return BlueprintProjectionLayerStatuses.Unavailable;
            }

            if (!world.Active)
            {
                return BlueprintProjectionLayerStatuses.Missing;
            }

            if (world.TileType != layer.ContentId)
            {
                string category;
                if (BlueprintReplacementRuleService.IsWorldReplacementFulfilled(layer, world, replacementSettings, out category))
                {
                    return BlueprintProjectionLayerStatuses.Fulfilled;
                }

                return BlueprintProjectionLayerStatuses.Conflict;
            }

            if (layer.Style > 0 && world.ObjectStyle != layer.Style)
            {
                string category;
                if (BlueprintReplacementRuleService.IsWorldReplacementFulfilled(layer, world, replacementSettings, out category))
                {
                    return BlueprintProjectionLayerStatuses.Fulfilled;
                }

                return BlueprintProjectionLayerStatuses.Conflict;
            }

            if (world.TilePaintId != layer.PaintId ||
                BuildTileCoatingFlags(world) != layer.CoatingFlags ||
                world.Slope != layer.Slope ||
                world.HalfBrick != layer.HalfBrick ||
                world.Inactive != layer.Inactive)
            {
                return BlueprintProjectionLayerStatuses.Conflict;
            }

            return BlueprintProjectionLayerStatuses.Fulfilled;
        }

        private static string ClassifyWall(BlueprintCellLayerRecord layer, BlueprintWorldTileSnapshot world)
        {
            if (layer == null || world == null)
            {
                return BlueprintProjectionLayerStatuses.Unavailable;
            }

            if (world.WallType <= 0)
            {
                return BlueprintProjectionLayerStatuses.Missing;
            }

            if (world.WallType != layer.ContentId ||
                world.WallPaintId != layer.PaintId ||
                BuildWallCoatingFlags(world) != layer.CoatingFlags)
            {
                return BlueprintProjectionLayerStatuses.Conflict;
            }

            return BlueprintProjectionLayerStatuses.Fulfilled;
        }

        private static string ClassifyWire(BlueprintCellLayerRecord layer, BlueprintWorldTileSnapshot world)
        {
            if (layer == null || world == null)
            {
                return BlueprintProjectionLayerStatuses.Unavailable;
            }

            var actual = BuildWireFlags(world);
            if (actual == 0)
            {
                return BlueprintProjectionLayerStatuses.Missing;
            }

            return actual == layer.ContentId
                ? BlueprintProjectionLayerStatuses.Fulfilled
                : BlueprintProjectionLayerStatuses.Conflict;
        }

        private static string ClassifyActuator(BlueprintCellLayerRecord layer, BlueprintWorldTileSnapshot world)
        {
            if (layer == null || world == null)
            {
                return BlueprintProjectionLayerStatuses.Unavailable;
            }

            if (!world.HasActuator)
            {
                return BlueprintProjectionLayerStatuses.Missing;
            }

            return layer.ContentId != 0
                ? BlueprintProjectionLayerStatuses.Fulfilled
                : BlueprintProjectionLayerStatuses.Conflict;
        }

        private static void ApplyExplicitObjectGroupPolicy(IReadOnlyList<BlueprintProjectionCellSnapshot> layers)
        {
            if (layers == null || layers.Count <= 0)
            {
                return;
            }

            var groups = new Dictionary<string, List<BlueprintProjectionCellSnapshot>>(StringComparer.Ordinal);
            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null ||
                    !IsObjectLayer(layer) ||
                    string.IsNullOrWhiteSpace(layer.ObjectGroupKey))
                {
                    continue;
                }

                List<BlueprintProjectionCellSnapshot> bucket;
                if (!groups.TryGetValue(layer.ObjectGroupKey, out bucket))
                {
                    bucket = new List<BlueprintProjectionCellSnapshot>();
                    groups.Add(layer.ObjectGroupKey, bucket);
                }

                bucket.Add(layer);
            }

            foreach (var pair in groups)
            {
                ApplyExplicitObjectGroupStatus(pair.Value);
            }
        }

        private static void ApplyExplicitObjectGroupStatus(List<BlueprintProjectionCellSnapshot> layers)
        {
            if (layers == null || layers.Count <= 0)
            {
                return;
            }

            var groupStatus = string.Empty;
            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null)
                {
                    continue;
                }

                if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
                {
                    groupStatus = BlueprintProjectionLayerStatuses.Conflict;
                    break;
                }

                if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Unavailable, StringComparison.Ordinal))
                {
                    groupStatus = BlueprintProjectionLayerStatuses.Unavailable;
                }
            }

            if (string.IsNullOrEmpty(groupStatus))
            {
                return;
            }

            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null)
                {
                    continue;
                }

                layer.ObjectGroupStatus = groupStatus;
                layer.Status = groupStatus;
            }
        }

        private static bool IsLegacyPartialObject(BlueprintCellLayerRecord layer)
        {
            return layer != null &&
                   IsObjectLayer(layer) &&
                   string.Equals(layer.ObjectGroupStatus, BlueprintObjectGroupStatuses.LegacyPartialObject, StringComparison.Ordinal);
        }

        private static bool IsObjectLayer(BlueprintProjectionCellSnapshot layer)
        {
            return layer != null &&
                   string.Equals(layer.LayerKind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsObjectLayer(BlueprintCellLayerRecord layer)
        {
            return layer != null &&
                   string.Equals(layer.LayerKind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveObjectGroupKey(BlueprintTemplateRecord template, BlueprintCellLayerRecord layer)
        {
            if (!IsObjectLayer(layer) || !BlueprintObjectGroupNormalizer.HasObjectGroupMetadata(layer))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(layer.ObjectGroupId))
            {
                return (template == null ? string.Empty : template.TemplateId ?? string.Empty) + "|" + layer.ObjectGroupId.Trim();
            }

            return (template == null ? string.Empty : template.TemplateId ?? string.Empty) + "|" +
                   BlueprintObjectGroupNormalizer.BuildObjectGroupId(
                       layer.ContentId,
                       layer.Style,
                       layer.ObjectOriginX,
                       layer.ObjectOriginY,
                       Math.Max(1, layer.ObjectWidth),
                       Math.Max(1, layer.ObjectHeight));
        }

        private static string ResolveCoverageGroup(string layerKind)
        {
            var kind = NormalizeLayerKind(layerKind);
            if (string.Equals(kind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintLayerKinds.Tile;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintLayerKinds.Wall;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintLayerKinds.Wire;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return BlueprintLayerKinds.Actuator;
            }

            return kind;
        }

        private static string ResolveMaterialDisplayName(BlueprintTemplateRecord template, int itemId)
        {
            if (template == null || template.Materials == null || itemId <= 0)
            {
                return string.Empty;
            }

            for (var index = 0; index < template.Materials.Count; index++)
            {
                var material = template.Materials[index];
                if (material != null && material.ItemId == itemId && !string.IsNullOrWhiteSpace(material.DisplayNameSnapshot))
                {
                    return material.DisplayNameSnapshot.Trim();
                }
            }

            return string.Empty;
        }

        private static int BuildTileCoatingFlags(BlueprintWorldTileSnapshot world)
        {
            var flags = 0;
            if (world != null && world.TileFullbright)
            {
                flags |= BlueprintCaptureCoatingFlags.Fullbright;
            }

            if (world != null && world.TileInvisible)
            {
                flags |= BlueprintCaptureCoatingFlags.Invisible;
            }

            return flags;
        }

        private static int BuildWallCoatingFlags(BlueprintWorldTileSnapshot world)
        {
            var flags = 0;
            if (world != null && world.WallFullbright)
            {
                flags |= BlueprintCaptureCoatingFlags.Fullbright;
            }

            if (world != null && world.WallInvisible)
            {
                flags |= BlueprintCaptureCoatingFlags.Invisible;
            }

            return flags;
        }

        private static int BuildWireFlags(BlueprintWorldTileSnapshot world)
        {
            var flags = 0;
            if (world == null)
            {
                return flags;
            }

            if (world.HasRedWire) flags |= BlueprintCaptureWireFlags.Red;
            if (world.HasBlueWire) flags |= BlueprintCaptureWireFlags.Blue;
            if (world.HasGreenWire) flags |= BlueprintCaptureWireFlags.Green;
            if (world.HasYellowWire) flags |= BlueprintCaptureWireFlags.Yellow;
            return flags;
        }

        private static string BuildWorldTileCacheKey(int tileX, int tileY)
        {
            return tileX.ToString(CultureInfo.InvariantCulture) + ":" + tileY.ToString(CultureInfo.InvariantCulture);
        }

        private static string NormalizeLayerKind(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
