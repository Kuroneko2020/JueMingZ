using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintMirrorHorizontalMirrorsPreviewCoordinatesAnchorAndSlope()
        {
            BlueprintEntryState.ResetForTesting();
            try
            {
                var template = CreateMirrorableBlueprintTemplate("镜像预览");
                RequireBlueprintEntrySuccess(BlueprintEntryState.SelectTemplateForPlacement(template), "start mirror preview");
                BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 100,
                    TileY = 200
                });

                var result = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.MirrorPreviewHorizontal, AppSettings.CreateDefault());
                RequireBlueprintEntrySuccess(result, "mirror preview horizontally");
                var snapshot = BlueprintPlacementPreviewState.GetSnapshot();
                if (!snapshot.Active ||
                    snapshot.AnchorX != 2 ||
                    snapshot.OriginTileX != 98 ||
                    snapshot.OriginTileY != 200)
                {
                    throw new InvalidOperationException("Expected horizontal mirror to flip the preview anchor and recalculate the hover origin.");
                }

                var mirroredTile = FindLayerAt(snapshot.TemplateSnapshot, 3, 0, BlueprintLayerKinds.Tile);
                var mirroredWall = FindLayerAt(snapshot.TemplateSnapshot, 0, 1, BlueprintLayerKinds.Wall);
                if (mirroredTile == null ||
                    mirroredTile.Slope != 2 ||
                    mirroredWall == null ||
                    mirroredWall.ContentId != 2)
                {
                    throw new InvalidOperationException("Expected horizontal mirror to flip cell X coordinates and map slope 1 to 2.");
                }

                AssertContains(BlueprintPlacementPreviewState.BuildUiStateJson(), "\"mirrorLastStatus\":\"mirrorHorizontalApplied\"");
                AssertContains(LegacyMainWindow.BuildBlueprintPlacementSummaryForTesting(), "锚点 2,0");
                AssertContains(LegacyMainWindow.GetBlueprintMirrorVisualContractForTesting(), "tileObjectData-direction-flip");
            }
            finally
            {
                BlueprintEntryState.ResetForTesting();
            }
        }

        private static void BlueprintMirrorCompleteMultitileObjectMirrorsAndPartialFailsClosed()
        {
            BlueprintEntryState.ResetForTesting();
            try
            {
                var template = CreateCompleteTableBlueprintTemplate("完整多格家具镜像");
                RequireBlueprintEntrySuccess(BlueprintEntryState.SelectTemplateForPlacement(template), "start complete object mirror preview");
                var result = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.MirrorPreviewHorizontal, AppSettings.CreateDefault());
                if (!result.Succeeded || !result.Changed)
                {
                    throw new InvalidOperationException("Expected complete multitile object layer to mirror with precise TileObjectData frames.");
                }

                var snapshot = BlueprintPlacementPreviewState.GetSnapshot();
                var mirroredLeftTop = FindLayerAt(snapshot.TemplateSnapshot, 0, 0, BlueprintLayerKinds.Object);
                var mirroredRightTop = FindLayerAt(snapshot.TemplateSnapshot, 2, 0, BlueprintLayerKinds.Object);
                var mirroredLeftBottom = FindLayerAt(snapshot.TemplateSnapshot, 0, 1, BlueprintLayerKinds.Object);
                var mirroredRightBottom = FindLayerAt(snapshot.TemplateSnapshot, 2, 1, BlueprintLayerKinds.Object);
                if (!snapshot.Active ||
                    mirroredLeftTop == null ||
                    mirroredRightTop == null ||
                    mirroredLeftBottom == null ||
                    mirroredRightBottom == null ||
                    mirroredLeftTop.FrameX != 0 ||
                    mirroredRightTop.FrameX != 36 ||
                    mirroredLeftBottom.FrameY != 18 ||
                    mirroredRightBottom.FrameY != 18)
                {
                    throw new InvalidOperationException("Expected mirrored 3x2 object to keep all child cells and horizontally corrected frames.");
                }

                if (string.IsNullOrWhiteSpace(mirroredLeftTop.ObjectGroupId) ||
                    !string.Equals(mirroredLeftTop.ObjectGroupId, mirroredRightTop.ObjectGroupId, StringComparison.Ordinal) ||
                    mirroredLeftTop.ObjectSubTileX != 0 ||
                    mirroredRightTop.ObjectSubTileX != 2 ||
                    mirroredLeftTop.ObjectOriginX != 0 ||
                    mirroredLeftTop.ObjectWidth != 3)
                {
                    throw new InvalidOperationException("Expected mirrored object layers to keep explicit object group metadata in mirrored coordinates.");
                }

                var diagnostics = BlueprintMirrorService.GetDiagnostics();
                if (!string.Equals(diagnostics.LastStatus, "mirrorHorizontalApplied", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected complete multitile object mirror to succeed without object fallback warnings; got " + diagnostics.LastStatus);
                }

                var partial = CreatePartialTableBlueprintTemplate("半件多格家具镜像");
                var blocked = BlueprintMirrorService.TryMirrorHorizontal(partial);
                if (blocked.Succeeded ||
                    blocked.Changed ||
                    blocked.Template != null ||
                    !string.Equals(blocked.ResultCode, "mirrorBlocked", StringComparison.Ordinal) ||
                    blocked.BlockedReason.IndexOf("objectIncomplete", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Expected partial multitile object mirror to fail closed without producing a mirrored template; got " + blocked.ResultCode + " / " + blocked.BlockedReason);
                }
            }
            finally
            {
                BlueprintEntryState.ResetForTesting();
            }
        }

        private static void BlueprintMirrorSupportMatrixMapsSlopesAndFlipsObjectDirection()
        {
            if (BlueprintMirrorService.MirrorSlopeForTesting(1) != 2 ||
                BlueprintMirrorService.MirrorSlopeForTesting(2) != 1 ||
                BlueprintMirrorService.MirrorSlopeForTesting(3) != 4 ||
                BlueprintMirrorService.MirrorSlopeForTesting(4) != 3 ||
                BlueprintMirrorService.MirrorSlopeForTesting(9) != -1)
            {
                throw new InvalidOperationException("Expected horizontal mirror slope map to pair 1/2 and 3/4 only.");
            }

            string reason;
            // Table (ContentId=14): non-directional 3x2 object — frame must mirror by sub-tile, not fallback to old frame.
            var tableLeftTop = new BlueprintCellLayerRecord
            {
                LayerKind = BlueprintLayerKinds.Object,
                ContentId = 14,
                Style = 0,
                FrameX = 0,
                FrameY = 0
            };
            if (!BlueprintMirrorService.CanMirrorLayerForTesting(tableLeftTop, out reason))
            {
                throw new InvalidOperationException("Expected table object sub-tile to be mirrorable with TileObjectData frame math; got " + reason);
            }

            int newFrameX;
            int newFrameY;
            if (!BlueprintMirrorService.TryMirrorObjectFrameForTesting(tableLeftTop, out newFrameX, out newFrameY, out reason) ||
                newFrameX != 36 ||
                newFrameY != 0)
            {
                throw new InvalidOperationException("Expected table left sub-tile frame to mirror to the right sub-tile; got " + newFrameX + "," + newFrameY + " / " + reason);
            }

            // Unknown object frames must not fall back to old FrameX/Y and report success.
            var unknownObject = new BlueprintCellLayerRecord
            {
                LayerKind = BlueprintLayerKinds.Object,
                ContentId = 999
            };
            if (BlueprintMirrorService.CanMirrorLayerForTesting(unknownObject, out reason) ||
                !string.Equals(reason, "objectTileDataUnresolvable", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unknown object to fail closed instead of fallback-success; got " + reason);
            }
        }

        private static void BlueprintMirrorProjectionMaterialsAndAutoPlacementUseMirroredSnapshot()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-mirror-snapshot-chain");
            try
            {
                BlueprintEntryState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintAutoPlacementService.ResetForTesting();

                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    new BlueprintTemplateLibraryStore(),
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-mirror", "world-mirror"));
                var template = CreateMirrorSingleMaterialTemplate("镜像链路", 31, 901, 2);
                RequireBlueprintEntrySuccess(BlueprintEntryState.SelectTemplateForPlacement(template), "start mirror chain preview");
                RequireBlueprintEntrySuccess(BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.MirrorPreviewHorizontal, AppSettings.CreateDefault()), "mirror chain preview");

                ReleasePlacementPreviewInitialLeftGate(100, 50);
                var placed = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 100,
                    TileY = 50,
                    PhysicalLeftDown = true,
                    LeftDown = true,
                    LeftPressed = true
                });
                BlueprintEntryState.MarkPlacementConfirmed(placed);
                if (!placed.Succeeded || !placed.PlacedInstance)
                {
                    throw new InvalidOperationException("Expected mirrored preview to create a world instance.");
                }

                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-mirror", out saved), "load mirrored instance");
                var instance = saved.Instances[0];
                var mirroredLayer = FindLayerAt(instance.TemplateSnapshot, 2, 0, BlueprintLayerKinds.Tile);
                if (instance.OriginTileX != 99 ||
                    instance.OriginTileY != 50 ||
                    mirroredLayer == null)
                {
                    throw new InvalidOperationException("Expected placed instance to store the mirrored template snapshot before projection.");
                }

                var reader = new FakeBlueprintWorldTileReader();
                BlueprintProjectionService.SetDependenciesForTesting(
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-mirror", "world-mirror"),
                    reader,
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 901, 2 } },
                    new Dictionary<int, int>()), true);

                var projection = BlueprintProjectionService.GetSnapshot();
                if (projection.ProjectedLayers.Count != 1 ||
                    projection.ProjectedLayers[0].WorldTileX != 101 ||
                    projection.ProjectedLayers[0].WorldTileY != 50 ||
                    projection.MissingLayerCount != 1)
                {
                    throw new InvalidOperationException("Expected projection to derive world coordinates from the mirrored template snapshot.");
                }

                var materials = BlueprintMaterialService.GetSnapshot();
                if (materials.RequiredItemCount != 1 ||
                    materials.AvailableStackTotal != 2)
                {
                    throw new InvalidOperationException("Expected material stats to aggregate missing layers from the mirrored projection.");
                }

                var settings = AppSettings.CreateDefault();
                settings.BlueprintAutoPlacementEnabled = true;
                var candidates = BlueprintAutoPlacementService.ResolveCandidatesForTesting(RuntimeSettingsSnapshot.FromSettings(settings));
                if (candidates.Candidate == null ||
                    candidates.Candidate.WorldTileX != 101 ||
                    candidates.Candidate.WorldTileY != 50 ||
                    candidates.Candidate.MaterialItemId != 901)
                {
                    throw new InvalidOperationException("Expected auto placement candidates to target the mirrored projection coordinate.");
                }
            }
            finally
            {
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintEntryState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintMirrorDiagnosticsWriteRuntimeSnapshotJson()
        {
            BlueprintMirrorService.ResetForTesting();
            try
            {
                var result = BlueprintMirrorService.TryMirrorHorizontal(CreateMirrorableBlueprintTemplate("镜像诊断"));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException("Expected mirror diagnostic setup to succeed.");
                }

                var runtimeSnapshot = RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
                {
                    Initialized = true,
                    Version = "test-blueprint-mirror"
                });
                var json = InvokeDiagnosticSnapshotJson(runtimeSnapshot);
                AssertContains(json, "\"BlueprintMirrorLastStatus\": \"mirrorHorizontalApplied\"");
                AssertContains(json, "\"BlueprintMirrorMode\": \"horizontal\"");
                AssertContains(json, "\"BlueprintMirrorMirroredCellCount\": 2");
                AssertContains(json, "\"BlueprintMirrorRejectedLayerCount\": 0");
                AssertContains(BlueprintMirrorService.BuildUiStateJson(), "\"lastMirroredLayerCount\":2");
            }
            finally
            {
                BlueprintMirrorService.ResetForTesting();
            }
        }

        private static BlueprintTemplateRecord CreateMirrorableBlueprintTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = 4,
                Height = 2,
                AnchorX = 1,
                AnchorY = 0
            };
            template.Cells.Add(new BlueprintCellRecord
            {
                X = 0,
                Y = 0,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Tile,
                        ContentId = 1,
                        MaterialItemId = 1,
                        MaterialStack = 1,
                        Slope = 1
                    }
                }
            });
            template.Cells.Add(new BlueprintCellRecord
            {
                X = 3,
                Y = 1,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Wall,
                        ContentId = 2,
                        MaterialItemId = 2,
                        MaterialStack = 1
                    }
                }
            });
            AddMaterialEntries(template);
            return template;
        }

        private static void RequireBlueprintEntrySuccess(BlueprintEntryCommandResult result, string label)
        {
            if (result == null || !result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Expected blueprint entry command to succeed: " + label +
                    " / " + (result == null ? "null" : result.ResultCode + " / " + result.Message));
            }
        }

        private static BlueprintTemplateRecord CreateCompleteTableBlueprintTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = 3,
                Height = 2,
                AnchorX = 1,
                AnchorY = 0
            };

            for (var y = 0; y < 2; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    template.Cells.Add(new BlueprintCellRecord
                    {
                        X = x,
                        Y = y,
                        Layers =
                        {
                            new BlueprintCellLayerRecord
                            {
                                LayerKind = BlueprintLayerKinds.Object,
                                ContentId = 14,
                                Style = 0,
                                FrameX = x * 18,
                                FrameY = y * 18,
                                MaterialItemId = x == 0 && y == 0 ? 1006 : 0,
                                MaterialStack = x == 0 && y == 0 ? 1 : 0
                            }
                        }
                    });
                    BlueprintObjectGroupNormalizer.SetCapturedObjectGroup(
                        template.Cells[template.Cells.Count - 1].Layers[0],
                        x,
                        y,
                        0,
                        0,
                        3,
                        2);
                }
            }

            AddMaterialEntries(template);
            return template;
        }

        private static BlueprintTemplateRecord CreatePartialTableBlueprintTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = 3,
                Height = 2,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(new BlueprintCellRecord
            {
                X = 0,
                Y = 0,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Object,
                        ContentId = 14,
                        Style = 0,
                        FrameX = 0,
                        FrameY = 0,
                        MaterialItemId = 1006,
                        MaterialStack = 1
                    }
                }
            });
            AddMaterialEntries(template);
            return template;
        }

        private static BlueprintTemplateRecord CreateMirrorSingleMaterialTemplate(string name, int tileType, int materialItemId, int materialStack)
        {
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = 3,
                Height = 1,
                AnchorX = 1,
                AnchorY = 0
            };
            template.Cells.Add(new BlueprintCellRecord
            {
                X = 0,
                Y = 0,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Tile,
                        ContentId = tileType,
                        MaterialItemId = materialItemId,
                        MaterialStack = materialStack
                    }
                }
            });
            AddMaterialEntries(template);
            return template;
        }

        private static BlueprintCellLayerRecord FindLayerAt(BlueprintTemplateRecord template, int x, int y, string layerKind)
        {
            for (var cellIndex = 0; template != null && template.Cells != null && cellIndex < template.Cells.Count; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                if (cell == null || cell.X != x || cell.Y != y)
                {
                    continue;
                }

                for (var layerIndex = 0; cell.Layers != null && layerIndex < cell.Layers.Count; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    if (layer != null && string.Equals(layer.LayerKind, layerKind, StringComparison.OrdinalIgnoreCase))
                    {
                        return layer;
                    }
                }
            }

            return null;
        }
    }
}
