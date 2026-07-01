using System;
using System.Collections.Generic;
using System.Globalization;
using Terraria;
using Terraria.ObjectData;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintObjectGroupStatuses
    {
        public const string RepairedLegacyObject = "repaired-legacy-object";
        public const string LegacyPartialObject = "legacy-partial-object";
    }

    internal static class BlueprintObjectGroupNormalizer
    {
        public static void NormalizeTemplateInPlace(BlueprintTemplateRecord template)
        {
            if (template == null)
            {
                return;
            }

            template.Cells = template.Cells ?? new List<BlueprintCellRecord>();
            template.MissingCapabilityFlags = template.MissingCapabilityFlags ?? new List<string>();

            var groups = BuildLegacyGroups(template);
            if (groups.Count <= 0)
            {
                return;
            }

            var shiftX = 0;
            var shiftY = 0;
            for (var index = 0; index < groups.Count; index++)
            {
                var group = groups[index];
                if (group == null || group.Invalid)
                {
                    continue;
                }

                if (group.OriginX < 0)
                {
                    shiftX = Math.Max(shiftX, -group.OriginX);
                }

                if (group.OriginY < 0)
                {
                    shiftY = Math.Max(shiftY, -group.OriginY);
                }
            }

            if (shiftX > 0 || shiftY > 0)
            {
                ShiftTemplate(template, shiftX, shiftY);
                for (var index = 0; index < groups.Count; index++)
                {
                    var group = groups[index];
                    if (group == null)
                    {
                        continue;
                    }

                    group.OriginX += shiftX;
                    group.OriginY += shiftY;
                }
            }

            var changed = false;
            for (var index = 0; index < groups.Count; index++)
            {
                var group = groups[index];
                if (group == null)
                {
                    continue;
                }

                if (group.Invalid)
                {
                    MarkGroupPartial(template, group, group.InvalidReason);
                    continue;
                }

                string repairBlockReason;
                if (!CanRepairOrCompleteGroup(template, group, out repairBlockReason))
                {
                    MarkGroupPartial(template, group, repairBlockReason);
                    continue;
                }

                var repaired = CompleteGroup(template, group);
                changed = changed || repaired;
                if (repaired)
                {
                    AddMissingCapability(template, BlueprintCaptureMissingCapabilities.LegacyObjectRepaired);
                }
            }

            if (changed || shiftX > 0 || shiftY > 0)
            {
                ResizeToFitCells(template);
                template.Cells.Sort(CompareCells);
            }
        }

        public static void SetCapturedObjectGroup(
            BlueprintCellLayerRecord layer,
            int tileX,
            int tileY,
            int originX,
            int originY,
            int width,
            int height)
        {
            if (layer == null || !IsObjectLayer(layer))
            {
                return;
            }

            width = Math.Max(1, width);
            height = Math.Max(1, height);
            if (width <= 1 && height <= 1)
            {
                ClearObjectGroup(layer);
                return;
            }

            var subTileX = tileX - originX;
            var subTileY = tileY - originY;
            SetObjectGroup(
                layer,
                originX,
                originY,
                width,
                height,
                Clamp(subTileX, 0, width - 1),
                Clamp(subTileY, 0, height - 1),
                string.Empty,
                string.Empty);
        }

        public static void OffsetObjectGroupOrigin(BlueprintCellLayerRecord layer, int offsetX, int offsetY)
        {
            if (layer == null || !HasObjectGroupMetadata(layer))
            {
                return;
            }

            layer.ObjectOriginX -= offsetX;
            layer.ObjectOriginY -= offsetY;
            layer.ObjectGroupId = BuildObjectGroupId(
                layer.ContentId,
                layer.Style,
                layer.ObjectOriginX,
                layer.ObjectOriginY,
                Math.Max(1, layer.ObjectWidth),
                Math.Max(1, layer.ObjectHeight));
        }

        public static void MirrorObjectGroupHorizontal(BlueprintCellLayerRecord layer, int templateWidth)
        {
            if (layer == null || !HasObjectGroupMetadata(layer))
            {
                return;
            }

            var width = Math.Max(1, layer.ObjectWidth);
            var height = Math.Max(1, layer.ObjectHeight);
            layer.ObjectOriginX = Math.Max(0, templateWidth - (layer.ObjectOriginX + width));
            layer.ObjectSubTileX = Clamp(width - 1 - layer.ObjectSubTileX, 0, width - 1);
            layer.ObjectWidth = width;
            layer.ObjectHeight = height;
            layer.ObjectSubTileY = Clamp(layer.ObjectSubTileY, 0, height - 1);
            layer.ObjectGroupId = BuildObjectGroupId(
                layer.ContentId,
                layer.Style,
                layer.ObjectOriginX,
                layer.ObjectOriginY,
                width,
                height);
        }

        public static bool HasObjectGroupMetadata(BlueprintCellLayerRecord layer)
        {
            return layer != null &&
                   (!string.IsNullOrWhiteSpace(layer.ObjectGroupId) ||
                    layer.ObjectWidth > 0 ||
                    layer.ObjectHeight > 0);
        }

        public static string BuildObjectGroupId(int contentId, int style, int originX, int originY, int width, int height)
        {
            return "object:" +
                   Math.Max(0, contentId).ToString(CultureInfo.InvariantCulture) + ":" +
                   Math.Max(0, style).ToString(CultureInfo.InvariantCulture) + ":" +
                   originX.ToString(CultureInfo.InvariantCulture) + "," +
                   originY.ToString(CultureInfo.InvariantCulture) + ":" +
                   Math.Max(1, width).ToString(CultureInfo.InvariantCulture) + "x" +
                   Math.Max(1, height).ToString(CultureInfo.InvariantCulture);
        }

        private static List<LegacyObjectGroup> BuildLegacyGroups(BlueprintTemplateRecord template)
        {
            var groups = new List<LegacyObjectGroup>();
            var groupsByKey = new Dictionary<string, LegacyObjectGroup>(StringComparer.Ordinal);
            for (var cellIndex = 0; template.Cells != null && cellIndex < template.Cells.Count; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                if (cell == null)
                {
                    continue;
                }

                cell.Layers = cell.Layers ?? new List<BlueprintCellLayerRecord>();
                for (var layerIndex = 0; layerIndex < cell.Layers.Count; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    if (layer == null)
                    {
                        continue;
                    }

                    NormalizeLayerMetadata(layer, cell);
                    if (!IsObjectLayer(layer))
                    {
                        continue;
                    }

                    if (HasObjectGroupMetadata(layer))
                    {
                        NormalizeExplicitObjectGroup(layer);
                        continue;
                    }

                    LegacyObjectFrameInfo info;
                    string reason;
                    if (!TryReadObjectFrameInfo(layer, out info, out reason))
                    {
                        MarkLayerPartial(template, layer, reason);
                        continue;
                    }

                    if (info.Width <= 1 && info.Height <= 1)
                    {
                        continue;
                    }

                    var originX = cell.X - info.ObjectColumn;
                    var originY = cell.Y - info.ObjectRow;
                    var key = BuildObjectGroupId(layer.ContentId, info.Style, originX, originY, info.Width, info.Height);
                    LegacyObjectGroup group;
                    if (!groupsByKey.TryGetValue(key, out group))
                    {
                        group = new LegacyObjectGroup
                        {
                            GroupId = key,
                            ContentId = Math.Max(0, layer.ContentId),
                            Style = info.Style,
                            OriginX = originX,
                            OriginY = originY,
                            Width = info.Width,
                            Height = info.Height,
                            StyleColumn = info.StyleColumn,
                            StyleRow = info.StyleRow,
                            Data = info.Data,
                            RepresentativeLayer = layer
                        };
                        groupsByKey.Add(key, group);
                        groups.Add(group);
                    }

                    var subKey = BuildCellKey(info.ObjectColumn, info.ObjectRow);
                    if (group.SubTiles.ContainsKey(subKey))
                    {
                        group.Invalid = true;
                        group.InvalidReason = "legacy-object-duplicate-subtile";
                    }
                    else
                    {
                        group.SubTiles.Add(subKey, new LegacyObjectSubTile
                        {
                            Cell = cell,
                            Layer = layer,
                            SubTileX = info.ObjectColumn,
                            SubTileY = info.ObjectRow
                        });
                    }
                }
            }

            return groups;
        }

        private static void NormalizeLayerMetadata(BlueprintCellLayerRecord layer, BlueprintCellRecord cell)
        {
            layer.LayerKind = string.IsNullOrWhiteSpace(layer.LayerKind) ? BlueprintLayerKinds.Tile : layer.LayerKind.Trim();
            layer.Note = layer.Note == null ? string.Empty : layer.Note.Trim();
            layer.ObjectGroupId = layer.ObjectGroupId == null ? string.Empty : layer.ObjectGroupId.Trim();
            layer.ObjectGroupStatus = layer.ObjectGroupStatus == null ? string.Empty : layer.ObjectGroupStatus.Trim();
            layer.ObjectGroupReason = layer.ObjectGroupReason == null ? string.Empty : layer.ObjectGroupReason.Trim();

            if (!IsObjectLayer(layer))
            {
                ClearObjectGroup(layer);
            }
        }

        private static void NormalizeExplicitObjectGroup(BlueprintCellLayerRecord layer)
        {
            var width = Math.Max(1, layer.ObjectWidth);
            var height = Math.Max(1, layer.ObjectHeight);
            layer.ObjectWidth = width;
            layer.ObjectHeight = height;
            layer.ObjectSubTileX = Clamp(layer.ObjectSubTileX, 0, width - 1);
            layer.ObjectSubTileY = Clamp(layer.ObjectSubTileY, 0, height - 1);
            if (string.IsNullOrWhiteSpace(layer.ObjectGroupId))
            {
                layer.ObjectGroupId = BuildObjectGroupId(
                    layer.ContentId,
                    layer.Style,
                    layer.ObjectOriginX,
                    layer.ObjectOriginY,
                    width,
                    height);
            }
        }

        private static bool CanRepairOrCompleteGroup(
            BlueprintTemplateRecord template,
            LegacyObjectGroup group,
            out string reason)
        {
            reason = string.Empty;
            for (var x = 0; x < group.Width; x++)
            {
                for (var y = 0; y < group.Height; y++)
                {
                    if (group.SubTiles.ContainsKey(BuildCellKey(x, y)))
                    {
                        continue;
                    }

                    var cell = FindCell(template, group.OriginX + x, group.OriginY + y);
                    if (cell == null || cell.Layers == null)
                    {
                        continue;
                    }

                    for (var index = 0; index < cell.Layers.Count; index++)
                    {
                        var layer = cell.Layers[index];
                        if (layer == null)
                        {
                            continue;
                        }

                        if (IsObjectLayer(layer) || string.Equals(layer.LayerKind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
                        {
                            reason = "legacy-object-repair-target-occupied";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool CompleteGroup(BlueprintTemplateRecord template, LegacyObjectGroup group)
        {
            var repaired = false;
            for (var x = 0; x < group.Width; x++)
            {
                for (var y = 0; y < group.Height; y++)
                {
                    LegacyObjectSubTile existing;
                    if (group.SubTiles.TryGetValue(BuildCellKey(x, y), out existing))
                    {
                        SetObjectGroup(existing.Layer, group.OriginX, group.OriginY, group.Width, group.Height, x, y, string.Empty, string.Empty);
                        continue;
                    }

                    var cell = FindOrCreateCell(template, group.OriginX + x, group.OriginY + y);
                    var layer = CreateRepairedLayer(group, x, y);
                    SetObjectGroup(
                        layer,
                        group.OriginX,
                        group.OriginY,
                        group.Width,
                        group.Height,
                        x,
                        y,
                        BlueprintObjectGroupStatuses.RepairedLegacyObject,
                        BlueprintCaptureMissingCapabilities.LegacyObjectRepaired);
                    cell.Layers.Add(layer);
                    repaired = true;
                }
            }

            if (repaired)
            {
                foreach (var pair in group.SubTiles)
                {
                    if (pair.Value != null && pair.Value.Layer != null)
                    {
                        SetObjectGroup(
                            pair.Value.Layer,
                            group.OriginX,
                            group.OriginY,
                            group.Width,
                            group.Height,
                            pair.Value.SubTileX,
                            pair.Value.SubTileY,
                            BlueprintObjectGroupStatuses.RepairedLegacyObject,
                            BlueprintCaptureMissingCapabilities.LegacyObjectRepaired);
                    }
                }
            }

            return repaired;
        }

        private static BlueprintCellLayerRecord CreateRepairedLayer(LegacyObjectGroup group, int subTileX, int subTileY)
        {
            var source = group.RepresentativeLayer == null ? new BlueprintCellLayerRecord() : group.RepresentativeLayer.Clone();
            source.LayerKind = BlueprintLayerKinds.Object;
            source.ContentId = group.ContentId;
            source.Style = group.Style;
            source.FrameX = BuildFrameX(group.Data, group.StyleColumn, subTileX);
            source.FrameY = BuildFrameY(group.Data, group.StyleRow, subTileY);
            source.MaterialStack = 0;
            if (string.IsNullOrWhiteSpace(source.Note))
            {
                source.Note = BlueprintCaptureMissingCapabilities.LegacyObjectRepaired;
            }

            return source;
        }

        private static void SetObjectGroup(
            BlueprintCellLayerRecord layer,
            int originX,
            int originY,
            int width,
            int height,
            int subTileX,
            int subTileY,
            string status,
            string reason)
        {
            if (layer == null)
            {
                return;
            }

            width = Math.Max(1, width);
            height = Math.Max(1, height);
            layer.ObjectOriginX = originX;
            layer.ObjectOriginY = originY;
            layer.ObjectWidth = width;
            layer.ObjectHeight = height;
            layer.ObjectSubTileX = Clamp(subTileX, 0, width - 1);
            layer.ObjectSubTileY = Clamp(subTileY, 0, height - 1);
            layer.ObjectGroupId = BuildObjectGroupId(layer.ContentId, layer.Style, originX, originY, width, height);
            layer.ObjectGroupStatus = status ?? string.Empty;
            layer.ObjectGroupReason = reason ?? string.Empty;
        }

        private static void ClearObjectGroup(BlueprintCellLayerRecord layer)
        {
            if (layer == null)
            {
                return;
            }

            layer.ObjectGroupId = string.Empty;
            layer.ObjectOriginX = 0;
            layer.ObjectOriginY = 0;
            layer.ObjectWidth = 0;
            layer.ObjectHeight = 0;
            layer.ObjectSubTileX = 0;
            layer.ObjectSubTileY = 0;
            layer.ObjectGroupStatus = string.Empty;
            layer.ObjectGroupReason = string.Empty;
        }

        private static void MarkGroupPartial(BlueprintTemplateRecord template, LegacyObjectGroup group, string reason)
        {
            if (group == null)
            {
                return;
            }

            foreach (var pair in group.SubTiles)
            {
                if (pair.Value != null)
                {
                    MarkLayerPartial(template, pair.Value.Layer, reason);
                }
            }
        }

        private static void MarkLayerPartial(BlueprintTemplateRecord template, BlueprintCellLayerRecord layer, string reason)
        {
            if (layer == null)
            {
                return;
            }

            layer.ObjectGroupStatus = BlueprintObjectGroupStatuses.LegacyPartialObject;
            layer.ObjectGroupReason = string.IsNullOrWhiteSpace(reason) ? BlueprintCaptureMissingCapabilities.LegacyPartialObject : reason.Trim();
            AddMissingCapability(template, BlueprintCaptureMissingCapabilities.LegacyPartialObject);
        }

        private static bool TryReadObjectFrameInfo(
            BlueprintCellLayerRecord layer,
            out LegacyObjectFrameInfo info,
            out string reason)
        {
            info = null;
            reason = string.Empty;
            if (layer == null || !IsObjectLayer(layer))
            {
                reason = "legacy-object-missing-layer";
                return false;
            }

            var data = ResolveTileObjectData((ushort)Math.Max(0, layer.ContentId), layer.FrameX, layer.FrameY);
            if (data == null)
            {
                reason = "legacy-object-data-unavailable";
                return false;
            }

            if (data.Width <= 0 ||
                data.Height <= 0 ||
                data.CoordinateWidth <= 0 ||
                data.CoordinatePadding < 0 ||
                data.CoordinateFullWidth <= 0 ||
                data.CoordinateFullHeight <= 0)
            {
                reason = "legacy-object-frame-dimensions-invalid";
                return false;
            }

            var frameX = Math.Max(0, layer.FrameX);
            var frameY = Math.Max(0, layer.FrameY);
            var styleColumn = frameX / data.CoordinateFullWidth;
            var styleRow = frameY / data.CoordinateFullHeight;
            var subTileX = frameX % data.CoordinateFullWidth;
            var subTileY = frameY % data.CoordinateFullHeight;

            int objectColumn;
            if (!TryResolveObjectColumn(data, subTileX, out objectColumn))
            {
                reason = "legacy-object-frame-x-invalid";
                return false;
            }

            int objectRow;
            if (!TryResolveObjectRow(data, subTileY, out objectRow))
            {
                reason = "legacy-object-frame-y-invalid";
                return false;
            }

            var style = ComputeTileStyle(data, styleColumn, styleRow);
            if (Math.Max(0, layer.Style) != style)
            {
                reason = "legacy-object-style-mismatch";
                return false;
            }

            info = new LegacyObjectFrameInfo
            {
                Data = data,
                StyleColumn = styleColumn,
                StyleRow = styleRow,
                Style = style,
                ObjectColumn = objectColumn,
                ObjectRow = objectRow,
                Width = Math.Max(1, data.Width),
                Height = Math.Max(1, data.Height)
            };
            return true;
        }

        private static TileObjectData ResolveTileObjectData(ushort tileType, int frameX, int frameY)
        {
            try
            {
                var tile = new Tile();
                tile.active(true);
                tile.type = tileType;
                tile.frameX = (short)Clamp(frameX, 0, 32767);
                tile.frameY = (short)Clamp(frameY, 0, 32767);
                var data = TryGetTileData(tile);
                if (data != null)
                {
                    return data;
                }

                // Terraria owns the startup-only TileObjectData writer; injected
                // runtime code must not force that path or it can lock vanilla setup.
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static TileObjectData TryGetTileData(Tile tile)
        {
            try
            {
                return TileObjectData.GetTileData(tile);
            }
            catch
            {
                return null;
            }
        }

        private static int ComputeTileStyle(TileObjectData data, int styleColumn, int styleRow)
        {
            if (data == null)
            {
                return 0;
            }

            var wrapLimit = data.StyleWrapLimit <= 0 ? 1 : data.StyleWrapLimit;
            var placementStyle = data.StyleHorizontal
                ? styleRow * wrapLimit + styleColumn
                : styleColumn * wrapLimit + styleRow;
            var multiplier = data.StyleMultiplier <= 0 ? 1 : data.StyleMultiplier;
            return Math.Max(0, placementStyle / multiplier);
        }

        private static bool TryResolveObjectColumn(TileObjectData data, int subTileX, out int column)
        {
            column = 0;
            if (data == null || data.Width <= 0 || data.CoordinateWidth <= 0 || data.CoordinatePadding < 0)
            {
                return false;
            }

            var step = data.CoordinateWidth + data.CoordinatePadding;
            if (step <= 0)
            {
                return false;
            }

            column = subTileX / step;
            var innerX = subTileX - column * step;
            return column >= 0 && column < data.Width && innerX >= 0 && innerX < data.CoordinateWidth;
        }

        private static bool TryResolveObjectRow(TileObjectData data, int subTileY, out int row)
        {
            row = 0;
            if (data == null || data.Height <= 0 || data.CoordinatePadding < 0)
            {
                return false;
            }

            var offset = 0;
            for (var index = 0; index < data.Height; index++)
            {
                var rowHeight = GetCoordinateHeight(data, index);
                if (rowHeight <= 0)
                {
                    return false;
                }

                if (subTileY >= offset && subTileY < offset + rowHeight)
                {
                    row = index;
                    return true;
                }

                offset += rowHeight;
                if (index < data.Height - 1)
                {
                    if (subTileY >= offset && subTileY < offset + data.CoordinatePadding)
                    {
                        return false;
                    }

                    offset += data.CoordinatePadding;
                }
            }

            return false;
        }

        private static int BuildFrameX(TileObjectData data, int styleColumn, int objectColumn)
        {
            var step = Math.Max(1, data.CoordinateWidth + data.CoordinatePadding);
            return styleColumn * data.CoordinateFullWidth + objectColumn * step;
        }

        private static int BuildFrameY(TileObjectData data, int styleRow, int objectRow)
        {
            var offset = 0;
            for (var index = 0; index < objectRow; index++)
            {
                offset += Math.Max(1, GetCoordinateHeight(data, index)) + data.CoordinatePadding;
            }

            return styleRow * data.CoordinateFullHeight + offset;
        }

        private static int GetCoordinateHeight(TileObjectData data, int row)
        {
            if (data == null || data.CoordinateHeights == null || data.CoordinateHeights.Length == 0)
            {
                return 0;
            }

            if (row < data.CoordinateHeights.Length)
            {
                return data.CoordinateHeights[row];
            }

            return data.CoordinateHeights[data.CoordinateHeights.Length - 1];
        }

        private static BlueprintCellRecord FindOrCreateCell(BlueprintTemplateRecord template, int x, int y)
        {
            var cell = FindCell(template, x, y);
            if (cell != null)
            {
                cell.Layers = cell.Layers ?? new List<BlueprintCellLayerRecord>();
                return cell;
            }

            cell = new BlueprintCellRecord
            {
                X = x,
                Y = y
            };
            template.Cells.Add(cell);
            return cell;
        }

        private static BlueprintCellRecord FindCell(BlueprintTemplateRecord template, int x, int y)
        {
            for (var index = 0; template != null && template.Cells != null && index < template.Cells.Count; index++)
            {
                var cell = template.Cells[index];
                if (cell != null && cell.X == x && cell.Y == y)
                {
                    return cell;
                }
            }

            return null;
        }

        private static void ShiftTemplate(BlueprintTemplateRecord template, int shiftX, int shiftY)
        {
            for (var index = 0; template.Cells != null && index < template.Cells.Count; index++)
            {
                var cell = template.Cells[index];
                if (cell == null)
                {
                    continue;
                }

                cell.X += shiftX;
                cell.Y += shiftY;
            }

            template.AnchorX += shiftX;
            template.AnchorY += shiftY;
            template.Width += shiftX;
            template.Height += shiftY;
        }

        private static void ResizeToFitCells(BlueprintTemplateRecord template)
        {
            var maxX = Math.Max(0, template.Width - 1);
            var maxY = Math.Max(0, template.Height - 1);
            for (var index = 0; template.Cells != null && index < template.Cells.Count; index++)
            {
                var cell = template.Cells[index];
                if (cell == null)
                {
                    continue;
                }

                maxX = Math.Max(maxX, cell.X);
                maxY = Math.Max(maxY, cell.Y);
            }

            template.Width = Math.Max(1, maxX + 1);
            template.Height = Math.Max(1, maxY + 1);
            template.AnchorX = Clamp(template.AnchorX, 0, template.Width - 1);
            template.AnchorY = Clamp(template.AnchorY, 0, template.Height - 1);
        }

        private static void AddMissingCapability(BlueprintTemplateRecord template, string flag)
        {
            if (template == null || string.IsNullOrWhiteSpace(flag))
            {
                return;
            }

            template.MissingCapabilityFlags = template.MissingCapabilityFlags ?? new List<string>();
            for (var index = 0; index < template.MissingCapabilityFlags.Count; index++)
            {
                if (string.Equals(template.MissingCapabilityFlags[index], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            template.MissingCapabilityFlags.Add(flag);
        }

        private static bool IsObjectLayer(BlueprintCellLayerRecord layer)
        {
            return layer != null &&
                   string.Equals(layer.LayerKind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCellKey(int x, int y)
        {
            return x.ToString(CultureInfo.InvariantCulture) + ":" + y.ToString(CultureInfo.InvariantCulture);
        }

        private static int CompareCells(BlueprintCellRecord left, BlueprintCellRecord right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private sealed class LegacyObjectFrameInfo
        {
            public TileObjectData Data { get; set; }
            public int StyleColumn { get; set; }
            public int StyleRow { get; set; }
            public int Style { get; set; }
            public int ObjectColumn { get; set; }
            public int ObjectRow { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private sealed class LegacyObjectGroup
        {
            public LegacyObjectGroup()
            {
                SubTiles = new Dictionary<string, LegacyObjectSubTile>(StringComparer.Ordinal);
                InvalidReason = string.Empty;
            }

            public string GroupId { get; set; }
            public int ContentId { get; set; }
            public int Style { get; set; }
            public int OriginX { get; set; }
            public int OriginY { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int StyleColumn { get; set; }
            public int StyleRow { get; set; }
            public TileObjectData Data { get; set; }
            public BlueprintCellLayerRecord RepresentativeLayer { get; set; }
            public Dictionary<string, LegacyObjectSubTile> SubTiles { get; private set; }
            public bool Invalid { get; set; }
            public string InvalidReason { get; set; }
        }

        private sealed class LegacyObjectSubTile
        {
            public BlueprintCellRecord Cell { get; set; }
            public BlueprintCellLayerRecord Layer { get; set; }
            public int SubTileX { get; set; }
            public int SubTileY { get; set; }
        }
    }
}
