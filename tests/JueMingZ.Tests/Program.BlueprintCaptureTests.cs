using System;
using System.Collections.Generic;
using System.Linq;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Input;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintCaptureRecordsLayersAndFiltersAir()
        {
            BlueprintEntryState.ResetForTesting();
            BlueprintCreationMaskState.BeginCreate();
            ClickTileForBlueprintCreation(10, 20);
            ClickTileForBlueprintCreation(11, 20);
            ClickTileForBlueprintCreation(12, 20);
            BlueprintCreationMaskState.FinishCreate(false);

            var reader = new FakeBlueprintWorldTileReader();
            reader.Set(10, 20, new BlueprintWorldTileSnapshot
            {
                Active = true,
                TileType = 1,
                WallType = 2,
                FrameX = 18,
                FrameY = 0,
                TilePaintId = 3,
                WallPaintId = 4,
                TileFullbright = true,
                WallInvisible = true,
                Slope = 2,
                HalfBrick = true,
                Inactive = true,
                HasRedWire = true,
                HasBlueWire = true,
                HasActuator = true,
                TileMaterialItemId = 100,
                WallMaterialItemId = 200,
                WireMaterialItemId = 530,
                ActuatorMaterialItemId = 849,
                TileDisplayName = "Stone Block",
                WallDisplayName = "Stone Wall"
            });
            reader.Set(11, 20, new BlueprintWorldTileSnapshot());
            reader.Set(12, 20, new BlueprintWorldTileSnapshot
            {
                LiquidAmount = 128,
                LiquidType = 0
            });

            var result = BlueprintCaptureService.CaptureSnapshotToTemplate(BlueprintCreationMaskState.GetSnapshot(), reader, false);
            if (!result.Succeeded || result.CapturedCellCount != 1 || result.SkippedAirCellCount != 2)
            {
                throw new InvalidOperationException("Expected blueprint capture to save only non-air content cells.");
            }

            var template = result.Template;
            if (template.Width != 1 || template.Height != 1 || template.Cells.Count != 1)
            {
                throw new InvalidOperationException("Expected blueprint capture bounds to shrink around real content.");
            }

            var cell = template.Cells[0];
            var tile = RequireLayer(cell, BlueprintLayerKinds.Tile);
            if (tile.ContentId != 1 ||
                tile.FrameX != 18 ||
                tile.PaintId != 3 ||
                tile.CoatingFlags != BlueprintCaptureCoatingFlags.Fullbright ||
                tile.Slope != 2 ||
                !tile.HalfBrick ||
                !tile.Inactive)
            {
                throw new InvalidOperationException("Expected tile layer to preserve frame, paint, coating, slope, half brick and inactive state.");
            }

            var wall = RequireLayer(cell, BlueprintLayerKinds.Wall);
            if (wall.ContentId != 2 ||
                wall.PaintId != 4 ||
                wall.CoatingFlags != BlueprintCaptureCoatingFlags.Invisible)
            {
                throw new InvalidOperationException("Expected wall layer to preserve wall paint and coating.");
            }

            var wire = RequireLayer(cell, BlueprintLayerKinds.Wire);
            if (wire.ContentId != (BlueprintCaptureWireFlags.Red | BlueprintCaptureWireFlags.Blue) ||
                wire.MaterialStack != 2)
            {
                throw new InvalidOperationException("Expected wire layer to encode selected wire colors and material count.");
            }

            var actuator = RequireLayer(cell, BlueprintLayerKinds.Actuator);
            if (actuator.ContentId != 1 || actuator.MaterialItemId != 849)
            {
                throw new InvalidOperationException("Expected actuator layer to be captured as its own layer.");
            }

            if (!template.MissingCapabilityFlags.Contains(BlueprintCaptureMissingCapabilities.LiquidNotSupported))
            {
                throw new InvalidOperationException("Expected liquid-only selected cells to be recorded as an unsupported blueprint gap.");
            }
        }

        private static void BlueprintCaptureRejectsEmptyContentAfterAirFilter()
        {
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintCreationMaskState.BeginCreate();
            ClickTileForBlueprintCreation(1, 1);
            BlueprintCreationMaskState.FinishCreate(false);

            var reader = new FakeBlueprintWorldTileReader();
            reader.Set(1, 1, new BlueprintWorldTileSnapshot());
            var result = BlueprintCaptureService.CaptureSnapshotToTemplate(BlueprintCreationMaskState.GetSnapshot(), reader, false);
            if (result.Succeeded || !string.Equals(result.ResultCode, "emptyContent", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected all-air selection to be rejected after capture filtering.");
            }
        }

        private static void BlueprintCaptureCountsMultitileMaterialOnceAndFlagsExternalContent()
        {
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintCreationMaskState.BeginCreate();
            ClickTileForBlueprintCreation(30, 40);
            ClickTileForBlueprintCreation(31, 40);
            BlueprintCreationMaskState.FinishCreate(false);

            var reader = new FakeBlueprintWorldTileReader();
            reader.Set(30, 40, CreateObjectPart(21, 30, 40, 3, 300, "container"));
            reader.Set(31, 40, CreateObjectPart(21, 30, 40, 3, 300, "container"));

            var result = BlueprintCaptureService.CaptureSnapshotToTemplate(BlueprintCreationMaskState.GetSnapshot(), reader, false);
            if (!result.Succeeded || result.Template.Cells.Count != 2)
            {
                throw new InvalidOperationException("Expected multi-tile object parts to be retained as blueprint cells.");
            }

            var material = result.Template.Materials.FirstOrDefault(item => item.ItemId == 300);
            if (material == null || material.RequiredStack != 1)
            {
                throw new InvalidOperationException("Expected multi-tile object material to be counted once per object origin.");
            }

            var objectLayers = result.Template.Cells.Select(cell => RequireLayer(cell, BlueprintLayerKinds.Object)).ToList();
            if (objectLayers.Count != 2 ||
                objectLayers[0].Style != 3 ||
                objectLayers[0].MaterialStack + objectLayers[1].MaterialStack != 1)
            {
                throw new InvalidOperationException("Expected object layers to preserve style while only one part carries material stack.");
            }

            if (!result.Template.MissingCapabilityFlags.Contains(BlueprintCaptureMissingCapabilities.ContainerContentNotCaptured) ||
                objectLayers.Any(layer => !string.Equals(layer.Note, "container-content-not-captured", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Expected containers to record only their placed body and flag skipped contents.");
            }
        }

        private static void BlueprintCaptureSaveWritesTemplateAndRefreshesLibrary()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-capture-save");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                var reader = new FakeBlueprintWorldTileReader();
                reader.Set(5, 6, new BlueprintWorldTileSnapshot
                {
                    Active = true,
                    TileType = 2,
                    TileMaterialItemId = 101,
                    TileDisplayName = "Dirt Block"
                });
                BlueprintCaptureService.SetCaptureDependenciesForTesting(reader, store);
                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                BlueprintEntryState.ResetForTesting();

                BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, AppSettings.CreateDefault());
                ClickTileForBlueprintCreation(5, 6);
                LegacyUiActionService.HandleBlueprintEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = "blueprint-entry:finish-create-save",
                    Label = "蓝图:完成保存",
                    Kind = "button",
                    MouseCaptured = true
                });

                BlueprintTemplateLibrarySnapshot snapshot;
                RequireBlueprintSuccess(store.TryLoad(out snapshot), "load blueprint capture save result");
                if (snapshot.Templates.Count != 1 ||
                    snapshot.Templates[0].Cells.Count != 1 ||
                    !string.Equals(snapshot.Templates[0].Name, BlueprintStorageConstants.DefaultTemplateName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected finish-save command to write a default-named blueprint template.");
                }

                var entry = BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault());
                if (!string.Equals(entry.Mode, BlueprintEntryModes.Tool, StringComparison.Ordinal) ||
                    BlueprintCreationMaskState.GetSnapshot().CompletedPendingCapture)
                {
                    throw new InvalidOperationException("Expected successful blueprint capture save to clear pending mask and return to tool mode.");
                }

                var library = BlueprintLibraryUiState.GetSnapshot();
                if (library.Templates.Count != 1 ||
                    !string.Equals(library.SelectedTemplateId, snapshot.Templates[0].TemplateId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected blueprint library UI state to refresh and select the newly captured template.");
                }
            }
            finally
            {
                BlueprintCaptureService.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                BlueprintEntryState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintCaptureFinishUseSavesAndEntersPlacementPreview()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-capture-use");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                var reader = new FakeBlueprintWorldTileReader();
                reader.Set(8, 9, new BlueprintWorldTileSnapshot
                {
                    Active = true,
                    TileType = 3,
                    TileMaterialItemId = 102,
                    TileDisplayName = "Grass Block"
                });
                BlueprintCaptureService.SetCaptureDependenciesForTesting(reader, store);
                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                BlueprintEntryState.ResetForTesting();

                BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, AppSettings.CreateDefault());
                ClickTileForBlueprintCreation(8, 9);
                LegacyUiActionService.HandleBlueprintEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = "blueprint-entry:finish-create-use",
                    Label = "蓝图:完成使用",
                    Kind = "button",
                    MouseCaptured = true
                });

                BlueprintTemplateLibrarySnapshot snapshot;
                RequireBlueprintSuccess(store.TryLoad(out snapshot), "load blueprint finish-use save result");
                if (snapshot.Templates.Count != 1)
                {
                    throw new InvalidOperationException("Expected finish-use to save the captured blueprint template in 06.");
                }

                var entry = BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault());
                var placement = BlueprintPlacementPreviewState.GetSnapshot();
                if (!string.Equals(entry.Mode, BlueprintEntryModes.PlacementPreview, StringComparison.Ordinal) ||
                    !placement.Active ||
                    !string.Equals(placement.TemplateId, snapshot.Templates[0].TemplateId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected 07 finish-use to save the template and enter placement preview.");
                }
            }
            finally
            {
                BlueprintCaptureService.ResetForTesting();
                BlueprintPlacementPreviewState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                BlueprintEntryState.ResetForTesting();
                restore();
            }
        }

        private static BlueprintWorldTileSnapshot CreateObjectPart(
            int tileType,
            int originX,
            int originY,
            int style,
            int itemId,
            string externalKind)
        {
            return new BlueprintWorldTileSnapshot
            {
                Active = true,
                TileType = tileType,
                FrameImportant = true,
                ObjectOriginX = originX,
                ObjectOriginY = originY,
                ObjectWidth = 2,
                ObjectHeight = 1,
                ObjectStyle = style,
                TileMaterialItemId = itemId,
                TileDisplayName = "Object " + itemId,
                ExternalDataKind = externalKind
            };
        }

        private static BlueprintCellLayerRecord RequireLayer(BlueprintCellRecord cell, string layerKind)
        {
            if (cell == null || cell.Layers == null)
            {
                throw new InvalidOperationException("Blueprint cell has no layers.");
            }

            for (var index = 0; index < cell.Layers.Count; index++)
            {
                var layer = cell.Layers[index];
                if (layer != null && string.Equals(layer.LayerKind, layerKind, StringComparison.Ordinal))
                {
                    return layer;
                }
            }

            throw new InvalidOperationException("Missing blueprint layer " + layerKind + ".");
        }

        private sealed class FakeBlueprintWorldTileReader : IBlueprintWorldTileReader
        {
            private readonly Dictionary<string, BlueprintWorldTileSnapshot> _tiles =
                new Dictionary<string, BlueprintWorldTileSnapshot>(StringComparer.Ordinal);

            public bool IsWorldReady { get; set; }
            public int ReadCount { get; private set; }

            public FakeBlueprintWorldTileReader()
            {
                IsWorldReady = true;
            }

            public void Set(int tileX, int tileY, BlueprintWorldTileSnapshot snapshot)
            {
                _tiles[BuildKey(tileX, tileY)] = snapshot ?? new BlueprintWorldTileSnapshot();
            }

            public bool TryReadTile(int tileX, int tileY, out BlueprintWorldTileSnapshot snapshot)
            {
                ReadCount++;
                if (!_tiles.TryGetValue(BuildKey(tileX, tileY), out snapshot))
                {
                    snapshot = new BlueprintWorldTileSnapshot();
                }

                snapshot.TileX = tileX;
                snapshot.TileY = tileY;
                return true;
            }

            private static string BuildKey(int tileX, int tileY)
            {
                return tileX.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                       tileY.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
