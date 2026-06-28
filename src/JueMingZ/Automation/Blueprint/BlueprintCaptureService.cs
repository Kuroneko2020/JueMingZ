using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintCaptureService
    {
        private const int WireItemId = ItemID.Wire;
        private const int ActuatorItemId = ItemID.Actuator;
        private static readonly object TestingSyncRoot = new object();
        private static IBlueprintWorldTileReader _testingReader;
        private static BlueprintTemplateLibraryStore _testingStore;

        public static BlueprintCaptureResult CapturePendingMaskAndSave(bool useAfterSave)
        {
            IBlueprintWorldTileReader reader;
            BlueprintTemplateLibraryStore store;
            lock (TestingSyncRoot)
            {
                reader = _testingReader;
                store = _testingStore;
            }

            return CapturePendingMaskAndSave(
                useAfterSave,
                reader ?? new BlueprintTerrariaWorldTileReader(),
                store ?? new BlueprintTemplateLibraryStore());
        }

        internal static void SetCaptureDependenciesForTesting(IBlueprintWorldTileReader reader, BlueprintTemplateLibraryStore store)
        {
            lock (TestingSyncRoot)
            {
                _testingReader = reader;
                _testingStore = store;
            }
        }

        internal static void ResetForTesting()
        {
            SetCaptureDependenciesForTesting(null, null);
        }

        internal static IBlueprintWorldTileReader CreateWorldTileReader()
        {
            return new BlueprintTerrariaWorldTileReader();
        }

        internal static BlueprintCaptureResult CapturePendingMaskAndSave(
            bool useAfterSave,
            IBlueprintWorldTileReader reader,
            BlueprintTemplateLibraryStore store)
        {
            var snapshot = BlueprintCreationMaskState.GetSnapshot();
            if (snapshot == null || !snapshot.CompletedPendingCapture)
            {
                return BlueprintCaptureResult.Failure(
                    "noPendingMask",
                    "没有待采集的蓝图创建 mask。",
                    null,
                    null,
                    snapshot == null ? 0 : snapshot.SelectedCount,
                    0,
                    0,
                    0,
                    0,
                    useAfterSave);
            }

            var capture = CaptureSnapshotToTemplate(snapshot, reader, useAfterSave);
            if (!capture.Succeeded)
            {
                return capture;
            }

            store = store ?? new BlueprintTemplateLibraryStore();
            BlueprintTemplateRecord saved;
            var write = store.CreateTemplate(capture.Template, out saved);
            if (!write.Succeeded)
            {
                return BlueprintCaptureResult.Failure(
                    write.ResultCode,
                    "蓝图模板保存失败：" + write.Message,
                    capture.Template,
                    write,
                    capture.MaskSelectedCount,
                    capture.CapturedCellCount,
                    capture.CapturedLayerCount,
                    capture.SkippedAirCellCount,
                    capture.UnavailableCellCount,
                    useAfterSave);
            }

            return BlueprintCaptureResult.Success(
                "templateSaved",
                "已保存蓝图模板 " + (saved == null ? string.Empty : saved.Name) + "。",
                capture.Template,
                saved,
                write,
                capture.MaskSelectedCount,
                capture.CapturedCellCount,
                capture.CapturedLayerCount,
                capture.SkippedAirCellCount,
                capture.UnavailableCellCount,
                useAfterSave);
        }

        internal static BlueprintCaptureResult CaptureSnapshotToTemplate(
            BlueprintCreationMaskSnapshot mask,
            IBlueprintWorldTileReader reader,
            bool useAfterSave)
        {
            if (mask == null || mask.SelectedCells == null || mask.SelectedCells.Count <= 0)
            {
                return BlueprintCaptureResult.Failure("emptySelection", "没有可采集的蓝图选区。", null, null, 0, 0, 0, 0, 0, useAfterSave);
            }

            if (reader == null || !reader.IsWorldReady)
            {
                return BlueprintCaptureResult.Failure("worldUnavailable", "世界尚未就绪，不能采集蓝图。", null, null, mask.SelectedCount, 0, 0, 0, 0, useAfterSave);
            }

            var selected = CloneAndSortSelectedCells(mask.SelectedCells);
            var state = new BlueprintCaptureBuildState();
            List<BlueprintCaptureCellRequest> captureRequests;
            BlueprintCaptureResult requestFailure;
            if (!TryBuildCaptureCellRequests(selected, reader, mask.SelectedCount, useAfterSave, out captureRequests, out requestFailure))
            {
                return requestFailure;
            }

            int minMaskX;
            int minMaskY;
            int maxMaskX;
            int maxMaskY;
            if (!TryResolveCaptureBounds(captureRequests, out minMaskX, out minMaskY, out maxMaskX, out maxMaskY))
            {
                return BlueprintCaptureResult.Failure("emptySelection", "没有可采集的蓝图选区。", null, null, mask.SelectedCount, 0, 0, 0, 0, useAfterSave);
            }

            for (var index = 0; index < captureRequests.Count; index++)
            {
                var request = captureRequests[index];
                BlueprintWorldTileSnapshot sample;
                if (!TryReadCaptureSample(reader, request, out sample))
                {
                    state.UnavailableCount++;
                    state.MissingFlags.Add(BlueprintCaptureMissingCapabilities.TileReadUnavailable);
                    if (request.ExpectedObject != null)
                    {
                        return BuildObjectExpansionFailureResult(
                            "objectExpansionTileReadUnavailable",
                            "多格家具采集补齐失败：无法读取整件家具的格子 " +
                            request.X.ToString(CultureInfo.InvariantCulture) + "," +
                            request.Y.ToString(CultureInfo.InvariantCulture) + "。",
                            mask.SelectedCount,
                            state,
                            useAfterSave);
                    }

                    continue;
                }

                sample.TileX = request.X;
                sample.TileY = request.Y;
                if (request.ExpectedObject != null && !MatchesObjectSignature(sample, request.ExpectedObject))
                {
                    return BuildObjectExpansionFailureResult(
                        "objectExpansionIncomplete",
                        "多格家具采集补齐失败：整件家具的格子 " +
                        request.X.ToString(CultureInfo.InvariantCulture) + "," +
                        request.Y.ToString(CultureInfo.InvariantCulture) + " 与起始家具不一致。",
                        mask.SelectedCount,
                        state,
                        useAfterSave);
                }

                var cell = request.CaptureAllLayers
                    ? BuildCell(sample, state)
                    : BuildObjectExpansionCell(sample, state);
                if (cell == null || cell.Layers == null || cell.Layers.Count <= 0)
                {
                    if (request.ExpectedObject != null)
                    {
                        return BuildObjectExpansionFailureResult(
                            "objectExpansionEmptyCell",
                            "多格家具采集补齐失败：整件家具的格子 " +
                            request.X.ToString(CultureInfo.InvariantCulture) + "," +
                            request.Y.ToString(CultureInfo.InvariantCulture) + " 没有可采集的 object 层。",
                            mask.SelectedCount,
                            state,
                            useAfterSave);
                    }

                    if (request.CaptureAllLayers)
                    {
                        state.SkippedAirCount++;
                    }

                    continue;
                }

                state.Cells.Add(cell);
            }

            if (state.Cells.Count <= 0)
            {
                return BlueprintCaptureResult.Failure(
                    "emptyContent",
                    "选区内啥也没有喔",
                    null,
                    null,
                    mask.SelectedCount,
                    0,
                    0,
                    state.SkippedAirCount,
                    state.UnavailableCount,
                    useAfterSave);
            }

            // Air cells are stored as template bounds, not as content layers.
            // Projection/material/auto-place only consume Cells, so blank mask
            // edges never mean "remove world content".
            NormalizeCellCoordinates(state.Cells, minMaskX, minMaskY);
            var width = maxMaskX - minMaskX + 1;
            var height = maxMaskY - minMaskY + 1;
            var template = new BlueprintTemplateRecord
            {
                Width = Math.Max(1, width),
                Height = Math.Max(1, height),
                AnchorX = Math.Max(0, (width - 1) / 2),
                AnchorY = Math.Max(0, (height - 1) / 2)
            };
            template.Cells.AddRange(state.Cells);
            template.Materials.AddRange(BuildMaterialList(state.MaterialsByKey));
            foreach (var flag in state.MissingFlags)
            {
                template.MissingCapabilityFlags.Add(flag);
            }

            return BlueprintCaptureResult.Success(
                "templateCaptured",
                "已采集蓝图内容，等待保存模板。",
                template,
                null,
                BlueprintStorageOperationResult.Success("captured", "captured", string.Empty),
                mask.SelectedCount,
                template.Cells.Count,
                state.LayerCount,
                state.SkippedAirCount,
                state.UnavailableCount,
                useAfterSave);
        }

        internal static bool TryHasSelectableContent(
            IBlueprintWorldTileReader reader,
            int tileX,
            int tileY,
            out bool hasContent)
        {
            hasContent = false;
            if (reader == null || !reader.IsWorldReady)
            {
                return false;
            }

            BlueprintWorldTileSnapshot sample;
            if (!reader.TryReadTile(tileX, tileY, out sample) || sample == null)
            {
                return false;
            }

            sample.TileX = tileX;
            sample.TileY = tileY;
            hasContent = HasSelectableContent(sample);
            return true;
        }

        internal static bool HasSelectableContent(BlueprintWorldTileSnapshot sample)
        {
            if (sample == null)
            {
                return false;
            }

            return (sample.Active && sample.TileType >= 0) ||
                   sample.WallType > 0 ||
                   BuildWireFlags(sample) != 0 ||
                   sample.HasActuator;
        }

        private static BlueprintCellRecord BuildCell(BlueprintWorldTileSnapshot sample, BlueprintCaptureBuildState state)
        {
            var cell = new BlueprintCellRecord
            {
                X = sample.TileX,
                Y = sample.TileY
            };

            if (sample.LiquidAmount > 0)
            {
                state.MissingFlags.Add(BlueprintCaptureMissingCapabilities.LiquidNotSupported);
            }

            TryAddActiveTileLayer(cell, sample, state, false);

            if (sample.WallType > 0)
            {
                var layer = new BlueprintCellLayerRecord
                {
                    LayerKind = BlueprintLayerKinds.Wall,
                    ContentId = sample.WallType,
                    PaintId = Math.Max(0, sample.WallPaintId),
                    CoatingFlags = BuildCoatingFlags(sample.WallFullbright, sample.WallInvisible),
                    MaterialItemId = Math.Max(0, sample.WallMaterialItemId),
                    MaterialStack = sample.WallMaterialItemId > 0 ? 1 : 0
                };
                cell.Layers.Add(layer);
                state.LayerCount++;
                AddMaterial(state, layer.MaterialItemId, layer.MaterialStack, sample.WallDisplayName, BlueprintLayerKinds.Wall, "wall");
            }

            var wireFlags = BuildWireFlags(sample);
            if (wireFlags != 0)
            {
                var stack = CountBits(wireFlags);
                var layer = new BlueprintCellLayerRecord
                {
                    LayerKind = BlueprintLayerKinds.Wire,
                    ContentId = wireFlags,
                    MaterialItemId = Math.Max(0, sample.WireMaterialItemId),
                    MaterialStack = sample.WireMaterialItemId > 0 ? stack : 0
                };
                cell.Layers.Add(layer);
                state.LayerCount++;
                AddMaterial(state, layer.MaterialItemId, layer.MaterialStack, "Wire", BlueprintLayerKinds.Wire, "wire");
            }

            if (sample.HasActuator)
            {
                var layer = new BlueprintCellLayerRecord
                {
                    LayerKind = BlueprintLayerKinds.Actuator,
                    ContentId = 1,
                    MaterialItemId = Math.Max(0, sample.ActuatorMaterialItemId),
                    MaterialStack = sample.ActuatorMaterialItemId > 0 ? 1 : 0
                };
                cell.Layers.Add(layer);
                state.LayerCount++;
                AddMaterial(state, layer.MaterialItemId, layer.MaterialStack, "Actuator", BlueprintLayerKinds.Actuator, "actuator");
            }

            return cell.Layers.Count <= 0 ? null : cell;
        }

        private static BlueprintCellRecord BuildObjectExpansionCell(BlueprintWorldTileSnapshot sample, BlueprintCaptureBuildState state)
        {
            var cell = new BlueprintCellRecord
            {
                X = sample.TileX,
                Y = sample.TileY
            };

            // Object capture expansion protects mirror integrity before the
            // mirror service ever sees a template. Only the object layer is
            // imported here; wall/wire/actuator layers still belong to the
            // user's original selected cells.
            return TryAddActiveTileLayer(cell, sample, state, true) ? cell : null;
        }

        private static bool TryAddActiveTileLayer(
            BlueprintCellRecord cell,
            BlueprintWorldTileSnapshot sample,
            BlueprintCaptureBuildState state,
            bool requireFrameImportant)
        {
            if (cell == null || sample == null || state == null || !sample.Active || sample.TileType < 0)
            {
                return false;
            }

            if (requireFrameImportant && !sample.FrameImportant)
            {
                return false;
            }

            var kind = sample.FrameImportant ? BlueprintLayerKinds.Object : BlueprintLayerKinds.Tile;
            var materialStack = ResolveTileMaterialStack(sample, state);
            var note = ResolveExternalDataNote(sample, state);
            var layer = new BlueprintCellLayerRecord
            {
                LayerKind = kind,
                ContentId = sample.TileType,
                Style = Math.Max(0, sample.ObjectStyle),
                FrameX = sample.FrameX,
                FrameY = sample.FrameY,
                PaintId = Math.Max(0, sample.TilePaintId),
                CoatingFlags = BuildCoatingFlags(sample.TileFullbright, sample.TileInvisible),
                Slope = Math.Max(0, sample.Slope),
                HalfBrick = sample.HalfBrick,
                Inactive = sample.Inactive,
                MaterialItemId = Math.Max(0, sample.TileMaterialItemId),
                MaterialStack = materialStack,
                Note = note
            };
            cell.Layers.Add(layer);
            state.LayerCount++;
            AddMaterial(state, layer.MaterialItemId, layer.MaterialStack, sample.TileDisplayName, kind, "tile");
            return true;
        }

        private static int ResolveTileMaterialStack(BlueprintWorldTileSnapshot sample, BlueprintCaptureBuildState state)
        {
            if (sample.TileMaterialItemId <= 0)
            {
                return 0;
            }

            if (!sample.FrameImportant)
            {
                return 1;
            }

            var key = sample.TileType.ToString(CultureInfo.InvariantCulture) + "|" +
                      sample.ObjectStyle.ToString(CultureInfo.InvariantCulture) + "|" +
                      sample.ObjectOriginX.ToString(CultureInfo.InvariantCulture) + "," +
                      sample.ObjectOriginY.ToString(CultureInfo.InvariantCulture);
            return state.CountedObjects.Add(key) ? 1 : 0;
        }

        private static string ResolveExternalDataNote(BlueprintWorldTileSnapshot sample, BlueprintCaptureBuildState state)
        {
            var kind = sample.ExternalDataKind ?? string.Empty;
            if (kind.Length <= 0)
            {
                kind = ResolveKnownExternalDataKind(sample.TileType);
            }

            switch (kind)
            {
                case "text":
                    state.MissingFlags.Add(BlueprintCaptureMissingCapabilities.ExternalTextNotCaptured);
                    return "external-text-not-captured";
                case "container":
                    state.MissingFlags.Add(BlueprintCaptureMissingCapabilities.ContainerContentNotCaptured);
                    return "container-content-not-captured";
                case "equipment":
                    state.MissingFlags.Add(BlueprintCaptureMissingCapabilities.EquipmentContentNotCaptured);
                    return "equipment-content-not-captured";
                default:
                    return string.Empty;
            }
        }

        private static string ResolveKnownExternalDataKind(int tileType)
        {
            switch (tileType)
            {
                case 21:
                case 88:
                case 441:
                case 467:
                case 468:
                    return "container";
                case 55:
                case 85:
                    return "text";
                case 128:
                case 269:
                case 334:
                case 395:
                case 470:
                case 475:
                    return "equipment";
                default:
                    return string.Empty;
            }
        }

        private static int BuildCoatingFlags(bool fullbright, bool invisible)
        {
            var flags = 0;
            if (fullbright)
            {
                flags |= BlueprintCaptureCoatingFlags.Fullbright;
            }

            if (invisible)
            {
                flags |= BlueprintCaptureCoatingFlags.Invisible;
            }

            return flags;
        }

        private static int BuildWireFlags(BlueprintWorldTileSnapshot sample)
        {
            var flags = 0;
            if (sample.HasRedWire) flags |= BlueprintCaptureWireFlags.Red;
            if (sample.HasBlueWire) flags |= BlueprintCaptureWireFlags.Blue;
            if (sample.HasGreenWire) flags |= BlueprintCaptureWireFlags.Green;
            if (sample.HasYellowWire) flags |= BlueprintCaptureWireFlags.Yellow;
            return flags;
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }

        private static void AddMaterial(
            BlueprintCaptureBuildState state,
            int itemId,
            int stack,
            string displayName,
            string layerKind,
            string source)
        {
            if (itemId <= 0 || stack <= 0)
            {
                return;
            }

            var key = itemId.ToString(CultureInfo.InvariantCulture) + "|" + (layerKind ?? string.Empty) + "|" + (source ?? string.Empty);
            BlueprintMaterialEntry entry;
            if (!state.MaterialsByKey.TryGetValue(key, out entry))
            {
                entry = new BlueprintMaterialEntry
                {
                    ItemId = itemId,
                    DisplayNameSnapshot = string.IsNullOrWhiteSpace(displayName) ? itemId.ToString(CultureInfo.InvariantCulture) : displayName,
                    LayerKind = layerKind ?? string.Empty,
                    Source = source ?? string.Empty
                };
                state.MaterialsByKey[key] = entry;
            }

            entry.RequiredStack += stack;
        }

        private static List<BlueprintMaterialEntry> BuildMaterialList(Dictionary<string, BlueprintMaterialEntry> materialsByKey)
        {
            var list = new List<BlueprintMaterialEntry>();
            if (materialsByKey == null)
            {
                return list;
            }

            foreach (var entry in materialsByKey.Values)
            {
                if (entry != null && entry.ItemId > 0 && entry.RequiredStack > 0)
                {
                    list.Add(entry.Clone());
                }
            }

            list.Sort((left, right) =>
            {
                var item = left.ItemId.CompareTo(right.ItemId);
                if (item != 0) return item;
                var layer = string.Compare(left.LayerKind, right.LayerKind, StringComparison.Ordinal);
                return layer != 0 ? layer : string.Compare(left.Source, right.Source, StringComparison.Ordinal);
            });
            return list;
        }

        private static List<BlueprintCreationMaskCell> CloneAndSortSelectedCells(IList<BlueprintCreationMaskCell> source)
        {
            var cells = new List<BlueprintCreationMaskCell>();
            for (var index = 0; source != null && index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    cells.Add(source[index].Clone());
                }
            }

            cells.Sort((left, right) =>
            {
                var y = left.Y.CompareTo(right.Y);
                return y != 0 ? y : left.X.CompareTo(right.X);
            });
            return cells;
        }

        private static bool TryBuildCaptureCellRequests(
            IList<BlueprintCreationMaskCell> selected,
            IBlueprintWorldTileReader reader,
            int maskSelectedCount,
            bool useAfterSave,
            out List<BlueprintCaptureCellRequest> requests,
            out BlueprintCaptureResult failure)
        {
            requests = new List<BlueprintCaptureCellRequest>();
            failure = null;
            var requestsByKey = new Dictionary<string, BlueprintCaptureCellRequest>(StringComparer.Ordinal);
            if (selected == null || selected.Count <= 0)
            {
                return true;
            }

            for (var index = 0; index < selected.Count; index++)
            {
                var cell = selected[index];
                if (cell == null)
                {
                    continue;
                }

                GetOrCreateCaptureRequest(requestsByKey, cell.X, cell.Y).CaptureAllLayers = true;
            }

            for (var index = 0; index < selected.Count; index++)
            {
                var cell = selected[index];
                if (cell == null)
                {
                    continue;
                }

                var request = GetOrCreateCaptureRequest(requestsByKey, cell.X, cell.Y);
                BlueprintWorldTileSnapshot sample;
                if (!TryReadCaptureSample(reader, request, out sample) || sample == null)
                {
                    continue;
                }

                sample.TileX = cell.X;
                sample.TileY = cell.Y;
                BlueprintCaptureObjectSignature signature;
                BlueprintCaptureResult signatureFailure;
                if (!TryCreateObjectExpansionSignature(sample, maskSelectedCount, useAfterSave, out signature, out signatureFailure))
                {
                    failure = signatureFailure;
                    return false;
                }

                if (signature == null)
                {
                    continue;
                }

                for (var y = signature.OriginY; y < signature.OriginY + signature.Height; y++)
                {
                    for (var x = signature.OriginX; x < signature.OriginX + signature.Width; x++)
                    {
                        BlueprintCaptureResult addFailure;
                        if (!TryAddObjectExpansionRequest(requestsByKey, x, y, signature, maskSelectedCount, useAfterSave, out addFailure))
                        {
                            failure = addFailure;
                            return false;
                        }
                    }
                }
            }

            foreach (var request in requestsByKey.Values)
            {
                requests.Add(request);
            }

            requests.Sort((left, right) =>
            {
                var y = left.Y.CompareTo(right.Y);
                return y != 0 ? y : left.X.CompareTo(right.X);
            });
            return true;
        }

        private static bool TryCreateObjectExpansionSignature(
            BlueprintWorldTileSnapshot sample,
            int maskSelectedCount,
            bool useAfterSave,
            out BlueprintCaptureObjectSignature signature,
            out BlueprintCaptureResult failure)
        {
            signature = null;
            failure = null;
            if (sample == null || !sample.Active || !sample.FrameImportant || sample.TileType < 0)
            {
                return true;
            }

            var width = Math.Max(1, sample.ObjectWidth);
            var height = Math.Max(1, sample.ObjectHeight);
            if (width <= 1 && height <= 1)
            {
                return true;
            }

            var rightExclusive = sample.ObjectOriginX + width;
            var bottomExclusive = sample.ObjectOriginY + height;
            if (sample.TileX < sample.ObjectOriginX ||
                sample.TileX >= rightExclusive ||
                sample.TileY < sample.ObjectOriginY ||
                sample.TileY >= bottomExclusive)
            {
                failure = BlueprintCaptureResult.Failure(
                    "objectExpansionInvalidBounds",
                    "多格家具采集补齐失败：object 边界不包含起始格。",
                    null,
                    null,
                    maskSelectedCount,
                    0,
                    0,
                    0,
                    0,
                    useAfterSave);
                return false;
            }

            var area = (long)width * height;
            if (area > BlueprintCreationMaskState.MaxSelectedCells)
            {
                failure = BlueprintCaptureResult.Failure(
                    "objectExpansionTooLarge",
                    "多格家具采集补齐失败：object 范围超过蓝图选区上限。",
                    null,
                    null,
                    maskSelectedCount,
                    0,
                    0,
                    0,
                    0,
                    useAfterSave);
                return false;
            }

            signature = new BlueprintCaptureObjectSignature
            {
                TileType = sample.TileType,
                Style = Math.Max(0, sample.ObjectStyle),
                OriginX = sample.ObjectOriginX,
                OriginY = sample.ObjectOriginY,
                Width = width,
                Height = height
            };
            return true;
        }

        private static bool TryAddObjectExpansionRequest(
            Dictionary<string, BlueprintCaptureCellRequest> requestsByKey,
            int tileX,
            int tileY,
            BlueprintCaptureObjectSignature signature,
            int maskSelectedCount,
            bool useAfterSave,
            out BlueprintCaptureResult failure)
        {
            failure = null;
            var request = GetOrCreateCaptureRequest(requestsByKey, tileX, tileY);
            if (request.ExpectedObject == null)
            {
                request.ExpectedObject = signature;
                return true;
            }

            if (request.ExpectedObject.Matches(signature))
            {
                return true;
            }

            failure = BlueprintCaptureResult.Failure(
                "objectExpansionOverlap",
                "多格家具采集补齐失败：多个 object 边界重叠，已拒绝保存半件家具。",
                null,
                null,
                maskSelectedCount,
                0,
                0,
                0,
                0,
                useAfterSave);
            return false;
        }

        private static BlueprintCaptureCellRequest GetOrCreateCaptureRequest(
            Dictionary<string, BlueprintCaptureCellRequest> requestsByKey,
            int tileX,
            int tileY)
        {
            var key = BuildCoordinateKey(tileX, tileY);
            BlueprintCaptureCellRequest request;
            if (!requestsByKey.TryGetValue(key, out request))
            {
                request = new BlueprintCaptureCellRequest
                {
                    X = tileX,
                    Y = tileY
                };
                requestsByKey[key] = request;
            }

            return request;
        }

        private static bool TryReadCaptureSample(
            IBlueprintWorldTileReader reader,
            BlueprintCaptureCellRequest request,
            out BlueprintWorldTileSnapshot sample)
        {
            sample = null;
            if (reader == null || request == null)
            {
                return false;
            }

            if (request.CachedReadAttempted)
            {
                sample = request.CachedSample;
                return request.CachedReadSucceeded;
            }

            request.CachedReadAttempted = true;
            request.CachedReadSucceeded = reader.TryReadTile(request.X, request.Y, out sample) && sample != null;
            request.CachedSample = sample;
            return request.CachedReadSucceeded;
        }

        private static bool MatchesObjectSignature(BlueprintWorldTileSnapshot sample, BlueprintCaptureObjectSignature signature)
        {
            if (sample == null || signature == null)
            {
                return false;
            }

            return sample.Active &&
                   sample.FrameImportant &&
                   sample.TileType == signature.TileType &&
                   Math.Max(0, sample.ObjectStyle) == signature.Style &&
                   sample.ObjectOriginX == signature.OriginX &&
                   sample.ObjectOriginY == signature.OriginY &&
                   Math.Max(1, sample.ObjectWidth) == signature.Width &&
                   Math.Max(1, sample.ObjectHeight) == signature.Height;
        }

        private static BlueprintCaptureResult BuildObjectExpansionFailureResult(
            string resultCode,
            string message,
            int maskSelectedCount,
            BlueprintCaptureBuildState state,
            bool useAfterSave)
        {
            return BlueprintCaptureResult.Failure(
                resultCode,
                message,
                null,
                null,
                maskSelectedCount,
                state == null ? 0 : state.Cells.Count,
                state == null ? 0 : state.LayerCount,
                state == null ? 0 : state.SkippedAirCount,
                state == null ? 0 : state.UnavailableCount,
                useAfterSave);
        }

        private static bool TryResolveCaptureBounds(
            IList<BlueprintCaptureCellRequest> requests,
            out int minX,
            out int minY,
            out int maxX,
            out int maxY)
        {
            minX = 0;
            minY = 0;
            maxX = 0;
            maxY = 0;
            if (requests == null || requests.Count <= 0)
            {
                return false;
            }

            minX = requests[0].X;
            maxX = requests[0].X;
            minY = requests[0].Y;
            maxY = requests[0].Y;
            for (var index = 1; index < requests.Count; index++)
            {
                var request = requests[index];
                if (request == null)
                {
                    continue;
                }

                if (request.X < minX) minX = request.X;
                if (request.X > maxX) maxX = request.X;
                if (request.Y < minY) minY = request.Y;
                if (request.Y > maxY) maxY = request.Y;
            }

            return true;
        }

        private static string BuildCoordinateKey(int tileX, int tileY)
        {
            return tileX.ToString(CultureInfo.InvariantCulture) + "," +
                   tileY.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class BlueprintCaptureCellRequest
        {
            public int X { get; set; }
            public int Y { get; set; }
            public bool CaptureAllLayers { get; set; }
            public BlueprintCaptureObjectSignature ExpectedObject { get; set; }
            public bool CachedReadAttempted { get; set; }
            public bool CachedReadSucceeded { get; set; }
            public BlueprintWorldTileSnapshot CachedSample { get; set; }
        }

        private sealed class BlueprintCaptureObjectSignature
        {
            public int TileType { get; set; }
            public int Style { get; set; }
            public int OriginX { get; set; }
            public int OriginY { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }

            public bool Matches(BlueprintCaptureObjectSignature other)
            {
                return other != null &&
                       TileType == other.TileType &&
                       Style == other.Style &&
                       OriginX == other.OriginX &&
                       OriginY == other.OriginY &&
                       Width == other.Width &&
                       Height == other.Height;
            }
        }

        private static void NormalizeCellCoordinates(IList<BlueprintCellRecord> cells, int originX, int originY)
        {
            for (var index = 0; cells != null && index < cells.Count; index++)
            {
                var cell = cells[index];
                if (cell == null)
                {
                    continue;
                }

                cell.X -= originX;
                cell.Y -= originY;
            }
        }

        private sealed class BlueprintTerrariaWorldTileReader : IBlueprintWorldTileReader
        {
            public bool IsWorldReady
            {
                get { return TerrariaMainCompat.IsWorldReady; }
            }

            public bool TryReadTile(int tileX, int tileY, out BlueprintWorldTileSnapshot snapshot)
            {
                snapshot = null;
                Tile tile;
                if (!TerrariaTileReadCompat.TryGetTile(tileX, tileY, out tile))
                {
                    return false;
                }

                var active = TerrariaTileReadCompat.IsActive(tile);
                var tileType = TerrariaTileReadCompat.Type(tile);
                var wallType = TerrariaTileReadCompat.Wall(tile);
                var style = 0;
                var frameImportant = active && TryGetBool(Main.tileFrameImportant, tileType);
                var objectOriginX = tileX;
                var objectOriginY = tileY;
                var objectWidth = 1;
                var objectHeight = 1;
                if (active)
                {
                    TryResolveTileObjectData(tile, tileX, tileY, out style, out objectOriginX, out objectOriginY, out objectWidth, out objectHeight);
                }

                int tileItemId;
                var tileMaterial = active && BlueprintPlacementMaterialCache.TryResolveTileItem(tileType, style, out tileItemId) ? tileItemId : 0;
                int wallItemId;
                var wallMaterial = wallType > 0 && BlueprintPlacementMaterialCache.TryResolveWallItem(wallType, out wallItemId) ? wallItemId : 0;
                snapshot = new BlueprintWorldTileSnapshot
                {
                    TileX = tileX,
                    TileY = tileY,
                    Active = active,
                    TileType = tileType,
                    WallType = wallType,
                    FrameX = TerrariaTileReadCompat.FrameX(tile),
                    FrameY = TerrariaTileReadCompat.FrameY(tile),
                    WallFrameX = TerrariaTileReadCompat.WallFrameX(tile),
                    WallFrameY = TerrariaTileReadCompat.WallFrameY(tile),
                    TilePaintId = TerrariaTileReadCompat.TilePaint(tile),
                    WallPaintId = TerrariaTileReadCompat.WallPaint(tile),
                    TileFullbright = TerrariaTileReadCompat.IsTileFullbright(tile),
                    WallFullbright = TerrariaTileReadCompat.IsWallFullbright(tile),
                    TileInvisible = TerrariaTileReadCompat.IsTileInvisible(tile),
                    WallInvisible = TerrariaTileReadCompat.IsWallInvisible(tile),
                    Slope = TerrariaTileReadCompat.Slope(tile),
                    HalfBrick = TerrariaTileReadCompat.IsHalfBlock(tile),
                    Inactive = TerrariaTileReadCompat.IsActuated(tile),
                    FrameImportant = frameImportant,
                    ObjectOriginX = objectOriginX,
                    ObjectOriginY = objectOriginY,
                    ObjectWidth = objectWidth,
                    ObjectHeight = objectHeight,
                    ObjectStyle = style,
                    HasRedWire = TerrariaTileReadCompat.HasRedWire(tile),
                    HasBlueWire = TerrariaTileReadCompat.HasBlueWire(tile),
                    HasGreenWire = TerrariaTileReadCompat.HasGreenWire(tile),
                    HasYellowWire = TerrariaTileReadCompat.HasYellowWire(tile),
                    HasActuator = TerrariaTileReadCompat.HasActuator(tile),
                    LiquidAmount = TerrariaTileReadCompat.LiquidAmount(tile),
                    LiquidType = TerrariaTileReadCompat.LiquidType(tile),
                    TileMaterialItemId = tileMaterial,
                    WallMaterialItemId = wallMaterial,
                    WireMaterialItemId = WireItemId,
                    ActuatorMaterialItemId = ActuatorItemId,
                    TileDisplayName = tileMaterial > 0 ? Lang.GetItemNameValue(tileMaterial) : string.Empty,
                    WallDisplayName = wallMaterial > 0 ? Lang.GetItemNameValue(wallMaterial) : string.Empty
                };
                return true;
            }

            private static bool TryResolveTileObjectData(
                Tile tile,
                int tileX,
                int tileY,
                out int style,
                out int originX,
                out int originY,
                out int width,
                out int height)
            {
                style = 0;
                originX = tileX;
                originY = tileY;
                width = 1;
                height = 1;
                try
                {
                    var data = TileObjectData.GetTileData(tile);
                    if (data == null)
                    {
                        return false;
                    }

                    style = ResolveStyle(tile, data);
                    Rectangle bounds;
                    if (TileObjectData.TryGetTileBounds(tileX, tileY, out bounds))
                    {
                        originX = bounds.X;
                        originY = bounds.Y;
                        width = Math.Max(1, bounds.Width);
                        height = Math.Max(1, bounds.Height);
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static int ResolveStyle(Tile tile, TileObjectData data)
            {
                if (tile == null || data == null || data.CoordinateFullWidth <= 0 || data.CoordinateFullHeight <= 0)
                {
                    return 0;
                }

                var styleColumn = Math.Max(0, (int)tile.frameX) / data.CoordinateFullWidth;
                var styleRow = Math.Max(0, (int)tile.frameY) / data.CoordinateFullHeight;
                var wrapLimit = data.StyleWrapLimit <= 0 ? 1 : data.StyleWrapLimit;
                var placementStyle = data.StyleHorizontal
                    ? styleRow * wrapLimit + styleColumn
                    : styleColumn * wrapLimit + styleRow;
                var multiplier = data.StyleMultiplier <= 0 ? 1 : data.StyleMultiplier;
                return Math.Max(0, placementStyle / multiplier);
            }

            private static bool TryGetBool(bool[] values, int index)
            {
                return values != null && index >= 0 && index < values.Length && values[index];
            }
        }

        private static class BlueprintPlacementMaterialCache
        {
            private static readonly object SyncRoot = new object();
            private static bool _initialized;
            private static Dictionary<long, int> _tileItemsByKey = new Dictionary<long, int>();
            private static Dictionary<int, int> _wallItemsByType = new Dictionary<int, int>();

            public static bool TryResolveTileItem(int tileType, int tileStyle, out int itemId)
            {
                EnsureInitialized();
                lock (SyncRoot)
                {
                    return _tileItemsByKey.TryGetValue(BuildTileKey(tileType, tileStyle), out itemId) && itemId > 0;
                }
            }

            public static bool TryResolveWallItem(int wallType, out int itemId)
            {
                EnsureInitialized();
                lock (SyncRoot)
                {
                    return _wallItemsByType.TryGetValue(wallType, out itemId) && itemId > 0;
                }
            }

            private static void EnsureInitialized()
            {
                if (_initialized)
                {
                    return;
                }

                lock (SyncRoot)
                {
                    if (_initialized)
                    {
                        return;
                    }

                    var tileItems = new Dictionary<long, int>();
                    var wallItems = new Dictionary<int, int>();
                    try
                    {
                        var details = ItemID.Sets.DerivedPlacementDetails;
                        for (var itemId = 1; details != null && itemId < details.Length; itemId++)
                        {
                            var detail = details[itemId];
                            AddTileItem(tileItems, detail.tileType, detail.tileStyle, itemId);
                        }

                        foreach (var pair in ContentSamples.ItemsByType)
                        {
                            var item = pair.Value;
                            if (item == null)
                            {
                                continue;
                            }

                            if (item.createTile >= 0)
                            {
                                AddTileItem(tileItems, item.createTile, item.placeStyle, pair.Key);
                            }

                            if (item.createWall > 0)
                            {
                                AddWallItem(wallItems, item.createWall, pair.Key);
                            }
                        }
                    }
                    catch
                    {
                        tileItems.Clear();
                        wallItems.Clear();
                    }

                    _tileItemsByKey = tileItems;
                    _wallItemsByType = wallItems;
                    _initialized = true;
                }
            }

            private static void AddTileItem(Dictionary<long, int> tileItems, int tileType, int tileStyle, int itemId)
            {
                if (tileItems == null || tileType < 0 || itemId <= 0)
                {
                    return;
                }

                var key = BuildTileKey(tileType, tileStyle);
                if (!tileItems.ContainsKey(key))
                {
                    tileItems.Add(key, itemId);
                }
            }

            private static void AddWallItem(Dictionary<int, int> wallItems, int wallType, int itemId)
            {
                if (wallItems == null || wallType <= 0 || itemId <= 0 || wallItems.ContainsKey(wallType))
                {
                    return;
                }

                wallItems.Add(wallType, itemId);
            }

            private static long BuildTileKey(int tileType, int tileStyle)
            {
                return ((long)Math.Max(0, tileType) << 32) ^ (uint)Math.Max(0, tileStyle);
            }
        }
    }
}
