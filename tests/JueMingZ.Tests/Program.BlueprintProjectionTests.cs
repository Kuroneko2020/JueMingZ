using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintProjectionClassifiesFulfilledMissingAndConflict()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-statuses");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionStatusTemplate();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-proj", "world-proj", template, 30, 40, 0, out instance), "create projection status instance");
                reader.Set(30, 40, new BlueprintWorldTileSnapshot { Active = true, TileType = 11 });
                reader.Set(31, 40, new BlueprintWorldTileSnapshot());
                reader.Set(32, 40, new BlueprintWorldTileSnapshot { Active = true, TileType = 44 });

                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-proj", "world-proj"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                if (!snapshot.LoadSucceeded ||
                    snapshot.EffectiveLayerCount != 3 ||
                    snapshot.FulfilledLayerCount != 1 ||
                    snapshot.MissingLayerCount != 1 ||
                    snapshot.ConflictLayerCount != 1 ||
                    snapshot.UnavailableLayerCount != 0)
                {
                    throw new InvalidOperationException("Expected blueprint projection to classify fulfilled, missing and conflict layers independently.");
                }

                AssertContains(BlueprintProjectionService.BuildUiStateJson(), "\"conflictLayerCount\":1");
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionIgnoresAirOnlyTemplateBounds()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-air-bounds");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionAirBoundsTemplate();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-proj-air", "world-proj-air", template, 30, 40, 0, out instance), "create projection air-bound instance");
                reader.Set(30, 40, new BlueprintWorldTileSnapshot { Active = true, TileType = 999 });
                reader.Set(31, 41, new BlueprintWorldTileSnapshot { Active = true, TileType = 77 });
                reader.Set(32, 42, new BlueprintWorldTileSnapshot { Active = true, TileType = 998 });
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-proj-air", "world-proj-air"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                if (!snapshot.LoadSucceeded ||
                    snapshot.EffectiveLayerCount != 1 ||
                    snapshot.FulfilledLayerCount != 1 ||
                    snapshot.MissingLayerCount != 0 ||
                    snapshot.ConflictLayerCount != 0 ||
                    snapshot.ProjectedLayers.Count != 1 ||
                    snapshot.ProjectedLayers[0].WorldTileX != 31 ||
                    snapshot.ProjectedLayers[0].WorldTileY != 41)
                {
                    throw new InvalidOperationException("Expected projection to ignore air-only template bounds and resolve only content cells.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionComposesLayerOrderAndSkipsHidden()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-layer-order-hidden");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var lower = CreateProjectionLayerTemplate("下层", 17, 7);
                var upper = CreateProjectionTileOnlyTemplate("上层", 18);
                var hidden = CreateProjectionTileOnlyTemplate("隐藏", 99);
                BlueprintWorldInstanceRecord lowerInstance;
                BlueprintWorldInstanceRecord upperInstance;
                BlueprintWorldInstanceRecord hiddenInstance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-layer", "world-layer", lower, 10, 20, 0, out lowerInstance), "create lower projection instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-layer", "world-layer", upper, 10, 20, 1, out upperInstance), "create upper projection instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-layer", "world-layer", hidden, 10, 20, 2, out hiddenInstance), "create hidden projection instance");

                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(store.TryLoadWorld("pair-layer", out saved), "load projection instances for hidden toggle");
                var instances = new[]
                {
                    saved.Instances[0],
                    saved.Instances[1],
                    saved.Instances[2]
                };
                instances[2].Hidden = true;
                RequireBlueprintSuccess(store.SaveWorldInstances("pair-layer", "world-layer", instances, out saved), "save hidden projection instance");

                reader.Set(10, 20, new BlueprintWorldTileSnapshot
                {
                    Active = true,
                    TileType = 18,
                    WallType = 7
                });
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-layer", "world-layer"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                if (snapshot.InstanceCount != 3 ||
                    snapshot.VisibleInstanceCount != 2 ||
                    snapshot.HiddenInstanceCount != 1 ||
                    snapshot.EffectiveLayerCount != 2 ||
                    snapshot.CoveredLayerCount != 1 ||
                    snapshot.FulfilledLayerCount != 2)
                {
                    throw new InvalidOperationException("Expected blueprint projection to skip hidden instances and let higher tile layers cover lower tile layers without covering walls.");
                }

                var layers = snapshot.ProjectedLayers;
                if (layers.Count != 2 ||
                    !HasProjectedLayer(layers, upperInstance.InstanceId, BlueprintLayerKinds.Tile) ||
                    !HasProjectedLayer(layers, lowerInstance.InstanceId, BlueprintLayerKinds.Wall))
                {
                    throw new InvalidOperationException("Expected effective projection layers to keep upper tile and lower wall only.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionStage04LaterInstanceCoversEarlierWithoutMutatingSnapshots()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-stage04-overlap");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var fullTemplate = CreateProjectionTwoTileTemplate("04 完整模板", 41, 42);
                var coverTemplate = CreateProjectionTileOnlyTemplate("04 后放覆盖", 99);
                BlueprintWorldInstanceRecord lower;
                BlueprintWorldInstanceRecord upper;
                BlueprintWorldInstanceRecord replacedFull;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-stage04-overlap", "world-stage04-overlap", fullTemplate, 10, 20, 0, out lower), "create lower stage04 projection instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-stage04-overlap", "world-stage04-overlap", coverTemplate, 10, 20, 1, out upper), "create upper stage04 projection instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-stage04-overlap", "world-stage04-overlap", fullTemplate, 30, 20, 2, out replacedFull), "create re-placed full stage04 projection instance");

                reader.Set(10, 20, new BlueprintWorldTileSnapshot());
                reader.Set(11, 20, new BlueprintWorldTileSnapshot());
                reader.Set(30, 20, new BlueprintWorldTileSnapshot());
                reader.Set(31, 20, new BlueprintWorldTileSnapshot());
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-stage04-overlap", "world-stage04-overlap"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                if (!snapshot.LoadSucceeded ||
                    snapshot.CoveredLayerCount != 1 ||
                    snapshot.EffectiveLayerCount != 4 ||
                    !HasProjectedLayerAt(snapshot.ProjectedLayers, upper.InstanceId, BlueprintLayerKinds.Tile, 10, 20) ||
                    HasProjectedLayerAt(snapshot.ProjectedLayers, lower.InstanceId, BlueprintLayerKinds.Tile, 10, 20) ||
                    !HasProjectedLayerAt(snapshot.ProjectedLayers, lower.InstanceId, BlueprintLayerKinds.Tile, 11, 20) ||
                    !HasProjectedLayerAt(snapshot.ProjectedLayers, replacedFull.InstanceId, BlueprintLayerKinds.Tile, 30, 20) ||
                    !HasProjectedLayerAt(snapshot.ProjectedLayers, replacedFull.InstanceId, BlueprintLayerKinds.Tile, 31, 20))
                {
                    throw new InvalidOperationException("Expected stage04 overlap projection to let the later instance cover only the shared coordinate while keeping non-overlapped cells.");
                }

                BlueprintWorldInstanceSnapshot stored;
                RequireBlueprintSuccess(store.TryLoadWorld("pair-stage04-overlap", out stored), "load stage04 overlap instances");
                var lowerStored = FindPlacedInstance(stored, lower.InstanceId);
                var replacedStored = FindPlacedInstance(stored, replacedFull.InstanceId);
                if (fullTemplate.Cells.Count != 2 ||
                    lowerStored.TemplateSnapshot.Cells.Count != 2 ||
                    replacedStored.TemplateSnapshot.Cells.Count != 2)
                {
                    throw new InvalidOperationException("Stage04 overlap resolution must not trim templates or placed instance snapshots.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionStage05CompletedProgressPersistsAndSkipsDugCells()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-stage05-completed-progress");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionTwoTileTemplate("05 完成进度", 41, 42);
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-stage05-progress", "world-stage05-progress", template, 10, 20, 0, out instance), "create stage05 projection instance");

                reader.Set(10, 20, new BlueprintWorldTileSnapshot { Active = true, TileType = 41 });
                reader.Set(11, 20, new BlueprintWorldTileSnapshot { Active = true, TileType = 99 });
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-stage05-progress", "world-stage05-progress"),
                    reader,
                    true);

                var first = BlueprintProjectionService.GetSnapshot();
                if (!first.LoadSucceeded ||
                    first.FulfilledLayerCount != 1 ||
                    first.CompletedLayerCount != 0 ||
                    first.ConflictLayerCount != 1 ||
                    first.MissingLayerCount != 0)
                {
                    throw new InvalidOperationException("Expected stage05 first projection to persist only the newly fulfilled layer while keeping unfinished wrong content as conflict.");
                }

                BlueprintWorldInstanceSnapshot stored;
                RequireBlueprintSuccess(store.TryLoadWorld("pair-stage05-progress", out stored), "load stage05 completed progress");
                var storedInstance = FindPlacedInstance(stored, instance.InstanceId);
                if (storedInstance.CompletedLayers.Count != 1 ||
                    storedInstance.TemplateSnapshot.Cells.Count != 2 ||
                    template.Cells.Count != 2)
                {
                    throw new InvalidOperationException("Expected stage05 completed progress to be stored on the placed instance without trimming the template snapshot.");
                }

                reader.Set(10, 20, new BlueprintWorldTileSnapshot());
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-stage05-progress", "world-stage05-progress"),
                    reader,
                    true);

                var second = BlueprintProjectionService.GetSnapshot();
                var completedLayer = FindProjectedLayerAt(second.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Tile, 10, 20);
                var conflictLayer = FindProjectedLayerAt(second.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Tile, 11, 20);
                if (!second.LoadSucceeded ||
                    second.FulfilledLayerCount != 0 ||
                    second.CompletedLayerCount != 1 ||
                    second.ConflictLayerCount != 1 ||
                    second.MissingLayerCount != 0 ||
                    completedLayer == null ||
                    conflictLayer == null ||
                    !string.Equals(completedLayer.Status, BlueprintProjectionLayerStatuses.Completed, StringComparison.Ordinal) ||
                    !string.Equals(conflictLayer.Status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal) ||
                    BlueprintProjectionOverlay.ShouldDrawProjectionLayerForTesting(BlueprintProjectionLayerStatuses.Completed))
                {
                    throw new InvalidOperationException("Expected stage05 completed cells to stay hidden after being dug while unfinished wrong cells remain red conflicts.");
                }

                AssertContains(BlueprintProjectionService.BuildUiStateJson(), "\"completedLayerCount\":1");
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionWallFramesUseNeighborContinuity()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-wall-frames");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionWallBlockTemplate("02 墙帧连续", 7);
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-wall-frame", "world-wall-frame", template, 20, 30, 0, out instance), "create wall frame projection instance");
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-wall-frame", "world-wall-frame"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                if (!snapshot.LoadSucceeded ||
                    snapshot.EffectiveLayerCount != 4 ||
                    snapshot.MissingLayerCount != 4 ||
                    snapshot.ProjectedLayers.Count != 4)
                {
                    throw new InvalidOperationException("Expected 2x2 blueprint wall projection to resolve four missing wall layers.");
                }

                var frames = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < snapshot.ProjectedLayers.Count; index++)
                {
                    var layer = snapshot.ProjectedLayers[index];
                    if (layer == null || !string.Equals(layer.LayerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Expected wall frame projection test to contain only wall layers.");
                    }

                    frames.Add(layer.FrameX.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + layer.FrameY.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                if (frames.Count < 4)
                {
                    throw new InvalidOperationException("Expected neighboring blueprint walls to resolve varied wall frames instead of a fixed top-left source rectangle.");
                }

                var topLeft = FindProjectedLayerAt(snapshot.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Wall, 20, 30);
                var expectedTopLeft = BlueprintWallProjectionFrameResolver.ResolveFrameForTesting(20, 30, false, false, true, true);
                if (topLeft == null ||
                    topLeft.FrameX != expectedTopLeft[0] ||
                    topLeft.FrameY != expectedTopLeft[1])
                {
                    throw new InvalidOperationException("Expected projection cache to assign Terraria-style wall frame data from neighboring blueprint wall cells.");
                }

                var source = BlueprintProjectionGhostRenderer.ResolveWallSourceRectForTesting(topLeft.FrameX, topLeft.FrameY, 512, 288);
                if (source[0] != topLeft.FrameX ||
                    source[1] != topLeft.FrameY ||
                    source[2] != 32 ||
                    source[3] != 32)
                {
                    throw new InvalidOperationException("Expected wall ghost renderer to consume cached wall frame source rectangles instead of always drawing from 0,0.");
                }

                var destination = BlueprintProjectionGhostRenderer.ResolveWallGhostDestinationForTesting(160, 96);
                if (destination[0] != 152 ||
                    destination[1] != 88 ||
                    destination[2] != 32 ||
                    destination[3] != 32)
                {
                    throw new InvalidOperationException("Expected wall ghost renderer to draw a 32x32 wall texture centered on the 16x16 tile cell.");
                }

                var outerMask = BlueprintWallGhostOcclusionMask.TopLeft |
                                BlueprintWallGhostOcclusionMask.TopRight |
                                BlueprintWallGhostOcclusionMask.BottomLeft;
                var interiorAlpha = BlueprintProjectionGhostRenderer.ResolveWallGhostInteriorAlphaForTesting(142);
                var outerAlpha = BlueprintProjectionGhostRenderer.ResolveWallGhostQuadrantAlphaForTesting(interiorAlpha, topLeft.WallGhostOuterEdgeMask, BlueprintWallGhostOcclusionMask.TopLeft);
                var innerAlpha = BlueprintProjectionGhostRenderer.ResolveWallGhostQuadrantAlphaForTesting(interiorAlpha, topLeft.WallGhostOuterEdgeMask, BlueprintWallGhostOcclusionMask.BottomRight);
                if (topLeft.WallGhostOuterEdgeMask != outerMask ||
                    interiorAlpha < 80 ||
                    outerAlpha >= innerAlpha ||
                    outerAlpha > 60)
                {
                    throw new InvalidOperationException("Expected bottom-layer wall ghost to keep cached wall frames while de-emphasizing only the outer perimeter spill.");
                }

                if (reader.ReadCount > 16)
                {
                    throw new InvalidOperationException("Expected wall frame projection to reuse cache-build tile reads while resolving bottom-layer wall visual policy.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionWallGhostKeepsCompleteBehindFullTileForeground()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-wall-occlusion");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionSingleWallTemplate("墙遮挡", 7);
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-wall-occlusion", "world-wall-occlusion", template, 12, 13, 0, out instance), "create wall occlusion projection instance");
                reader.Set(12, 13, new BlueprintWorldTileSnapshot { WallBlockedByFullTile = true });

                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-wall-occlusion", "world-wall-occlusion"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                var layer = FindProjectedLayerAt(snapshot.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Wall, 12, 13);
                if (!snapshot.LoadSucceeded ||
                    snapshot.EffectiveLayerCount != 1 ||
                    snapshot.MissingLayerCount != 1 ||
                    snapshot.WallTargetLayerCount != 1 ||
                    snapshot.WallTypeMissingLayerCount != 1 ||
                    layer == null ||
                    layer.WallGhostBlockedByFullTile ||
                    layer.WallGhostOcclusionMask != 0 ||
                    layer.WallGhostProjectionForegroundMask != 0 ||
                    layer.WallGhostOuterEdgeMask != BlueprintWallGhostOcclusionMask.All ||
                    BlueprintProjectionGhostRenderer.ShouldSkipWallGhostForTesting(layer) ||
                    !BlueprintProjectionGhostRenderer.DrawLayer(null, layer, 192, 208, 800, 600, 255, 255, 255, 142, 0) ||
                    !BlueprintProjectionOverlay.GetVisualContractForTesting().Contains("wall-bottom-layer-complete"))
                {
                    throw new InvalidOperationException("Expected bottom-layer wall projection to stay complete behind a real full tile foreground without falling back to a whole-cell fill.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionWallGhostKeepsNeighborForegroundSpillComplete()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-wall-neighbor-occlusion");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionSingleWallTemplate("wall-neighbor-occlusion", 7);
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-wall-neighbor-occlusion", "world-wall-neighbor-occlusion", template, 12, 13, 0, out instance), "create wall neighbor occlusion projection instance");
                reader.Set(13, 13, new BlueprintWorldTileSnapshot { Active = true, WallBlockedByFullTile = true });

                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-wall-neighbor-occlusion", "world-wall-neighbor-occlusion"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                var layer = FindProjectedLayerAt(snapshot.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Wall, 12, 13);
                if (!snapshot.LoadSucceeded ||
                    snapshot.MissingLayerCount != 1 ||
                    layer == null ||
                    layer.WallGhostBlockedByFullTile ||
                    layer.WallGhostOcclusionMask != 0 ||
                    layer.WallGhostProjectionForegroundMask != 0 ||
                    layer.WallGhostOuterEdgeMask != BlueprintWallGhostOcclusionMask.All ||
                    BlueprintProjectionGhostRenderer.ShouldSkipWallGhostForTesting(layer) ||
                    !BlueprintProjectionGhostRenderer.DrawLayer(null, layer, 192, 208, 800, 600, 255, 255, 255, 142, 0) ||
                    !BlueprintProjectionOverlay.GetVisualContractForTesting().Contains("wall-bottom-layer-complete"))
                {
                    throw new InvalidOperationException("Expected neighboring foreground touched by the 32x32 wall spill to no longer cut the bottom wall projection.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionWallGhostKeepsFrameImportantForegroundComplete()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-wall-object-occlusion");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionSingleWallTemplate("wall-object-occlusion", 7);
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-wall-object-occlusion", "world-wall-object-occlusion", template, 12, 13, 0, out instance), "create wall object occlusion projection instance");
                reader.Set(12, 13, new BlueprintWorldTileSnapshot { Active = true, TileType = 19, FrameImportant = true });

                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-wall-object-occlusion", "world-wall-object-occlusion"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                var layer = FindProjectedLayerAt(snapshot.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Wall, 12, 13);
                if (!snapshot.LoadSucceeded ||
                    snapshot.MissingLayerCount != 1 ||
                    layer == null ||
                    layer.WallGhostBlockedByFullTile ||
                    layer.WallGhostOcclusionMask != 0 ||
                    layer.WallGhostProjectionForegroundMask != 0 ||
                    layer.WallGhostOuterEdgeMask != BlueprintWallGhostOcclusionMask.All ||
                    BlueprintProjectionGhostRenderer.ShouldSkipWallGhostForTesting(layer) ||
                    !BlueprintProjectionGhostRenderer.DrawLayer(null, layer, 192, 208, 800, 600, 255, 255, 255, 142, 0))
                {
                    throw new InvalidOperationException("Expected visible frame-important foreground objects to no longer hard-cut bottom wall ghost projection.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionWallGhostKeepsCompleteBehindSameProjectionObjectGhost()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-wall-same-object-softening");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionWallWithObjectTemplate("wall-same-object-softening", 7, 10);
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-wall-same-object-softening", "world-wall-same-object-softening", template, 12, 13, 0, out instance), "create wall same projection object softening instance");

                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-wall-same-object-softening", "world-wall-same-object-softening"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                var wall = FindProjectedLayerAt(snapshot.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Wall, 12, 13);
                var foreground = FindProjectedLayerAt(snapshot.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Object, 12, 13);
                if (!snapshot.LoadSucceeded ||
                    snapshot.MissingLayerCount != 2 ||
                    wall == null ||
                    foreground == null ||
                    wall.WallGhostBlockedByFullTile ||
                    wall.WallGhostOcclusionMask != 0 ||
                    wall.WallGhostProjectionForegroundMask != 0 ||
                    wall.WallGhostOuterEdgeMask != BlueprintWallGhostOcclusionMask.All ||
                    BlueprintProjectionGhostRenderer.ShouldSkipWallGhostForTesting(wall) ||
                    !BlueprintProjectionGhostRenderer.DrawLayer(null, wall, 192, 208, 800, 600, 255, 255, 255, 142, 0))
                {
                    throw new InvalidOperationException("Expected a visible object ghost in the same projection cell to leave the bottom wall layer complete.");
                }

                var drawOrder = BlueprintProjectionOverlay.BuildProjectionDrawOrderForTesting(snapshot, null);
                var objectIndex = IndexOfDrawOrder(drawOrder, "placed:object:12,13");
                var wallIndex = IndexOfDrawOrder(drawOrder, "world-placed:wall:12,13");
                if (objectIndex < 0 || wallIndex < 0 || wallIndex > objectIndex)
                {
                    throw new InvalidOperationException("Expected world-layer bottom wall projection to draw before the same-cell object projection foreground.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionWallGhostKeepsCompleteBehindSameProjectionNeighborTileGhost()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-wall-neighbor-tile-softening");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionWallWithNeighborTileTemplate("wall-neighbor-tile-softening", 7, 19);
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-wall-neighbor-tile-softening", "world-wall-neighbor-tile-softening", template, 12, 13, 0, out instance), "create wall neighbor projection tile softening instance");

                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-wall-neighbor-tile-softening", "world-wall-neighbor-tile-softening"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                var wall = FindProjectedLayerAt(snapshot.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Wall, 12, 13);
                var foreground = FindProjectedLayerAt(snapshot.ProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Tile, 13, 13);
                if (!snapshot.LoadSucceeded ||
                    snapshot.MissingLayerCount != 2 ||
                    wall == null ||
                    foreground == null ||
                    wall.WallGhostBlockedByFullTile ||
                    wall.WallGhostOcclusionMask != 0 ||
                    wall.WallGhostProjectionForegroundMask != 0 ||
                    wall.WallGhostOuterEdgeMask != BlueprintWallGhostOcclusionMask.All ||
                    BlueprintProjectionGhostRenderer.ShouldSkipWallGhostForTesting(wall) ||
                    !BlueprintProjectionGhostRenderer.DrawLayer(null, wall, 192, 208, 800, 600, 255, 255, 255, 142, 0))
                {
                    throw new InvalidOperationException("Expected a visible neighboring tile ghost touched by wall spill to leave the bottom wall layer complete.");
                }

                var drawOrder = BlueprintProjectionOverlay.BuildProjectionDrawOrderForTesting(snapshot, null);
                var tileIndex = IndexOfDrawOrder(drawOrder, "placed:tile:13,13");
                var wallIndex = IndexOfDrawOrder(drawOrder, "world-placed:wall:12,13");
                if (tileIndex < 0 || wallIndex < 0 || wallIndex > tileIndex)
                {
                    throw new InvalidOperationException("Expected world-layer bottom wall projection to draw before the neighboring tile projection foreground.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionFloatingWallGhostKeepsCompleteBelowPlacedProjectionForeground()
        {
            var placedLayers = new List<BlueprintProjectionCellSnapshot>
            {
                new BlueprintProjectionCellSnapshot
                {
                    InstanceId = "placed",
                    LayerKind = BlueprintLayerKinds.Tile,
                    CoverageGroup = BlueprintLayerKinds.Tile,
                    ContentId = 19,
                    Status = BlueprintProjectionLayerStatuses.Missing,
                    WorldTileX = 13,
                    WorldTileY = 13
                }
            };
            BlueprintProjectionService.ApplyBottomWallGhostVisualPolicy(placedLayers);
            var placed = new BlueprintProjectionSnapshot
            {
                LoadSucceeded = true,
                ProjectedLayers = placedLayers,
                AllProjectedLayers = placedLayers
            };
            var floatingLayers = new List<BlueprintProjectionCellSnapshot>
            {
                new BlueprintProjectionCellSnapshot
                {
                    InstanceId = "floating",
                    LayerKind = BlueprintLayerKinds.Wall,
                    CoverageGroup = BlueprintLayerKinds.Wall,
                    ContentId = 7,
                    Status = BlueprintProjectionLayerStatuses.Missing,
                    WorldTileX = 12,
                    WorldTileY = 13
                }
            };

            BlueprintProjectionService.ApplyBottomWallGhostVisualPolicy(floatingLayers);
            var wall = floatingLayers[0];
            if (wall.WallGhostBlockedByFullTile ||
                wall.WallGhostOcclusionMask != 0 ||
                wall.WallGhostProjectionForegroundMask != 0 ||
                wall.WallGhostOuterEdgeMask != BlueprintWallGhostOcclusionMask.All ||
                BlueprintProjectionGhostRenderer.ShouldSkipWallGhostForTesting(wall) ||
                !BlueprintProjectionGhostRenderer.DrawLayer(null, wall, 192, 208, 800, 600, 255, 255, 255, 142, 0))
            {
                throw new InvalidOperationException("Expected floating wall ghost to ignore placed foreground occupancy as a cut/soften source and stay complete.");
            }

            var floating = new BlueprintProjectionSnapshot
            {
                LoadSucceeded = true,
                ProjectedLayers = floatingLayers,
                AllProjectedLayers = floatingLayers
            };
            var drawOrder = BlueprintProjectionOverlay.BuildProjectionDrawOrderForTesting(placed, floating);
            var floatingWallIndex = IndexOfDrawOrder(drawOrder, "world-floating:wall:12,13");
            var placedTileIndex = IndexOfDrawOrder(drawOrder, "placed:tile:13,13");
            if (floatingWallIndex < 0 ||
                placedTileIndex < 0 ||
                floatingWallIndex > placedTileIndex ||
                !BlueprintProjectionOverlay.GetVisualContractForTesting().Contains("wall-bottom-layer-complete"))
            {
                throw new InvalidOperationException("Expected cross-projection draw order to draw floating wall in the world wall layer below visible placed tile/object foreground.");
            }
        }

        private static void BlueprintProjectionWallGhostUsesWorldLayerBeforeLateProjectionOverlay()
        {
            var layers = new List<BlueprintProjectionCellSnapshot>
            {
                new BlueprintProjectionCellSnapshot
                {
                    InstanceId = "placed",
                    LayerKind = BlueprintLayerKinds.Wall,
                    CoverageGroup = BlueprintLayerKinds.Wall,
                    ContentId = 7,
                    Status = BlueprintProjectionLayerStatuses.Missing,
                    WorldTileX = 12,
                    WorldTileY = 13
                },
                new BlueprintProjectionCellSnapshot
                {
                    InstanceId = "placed",
                    LayerKind = BlueprintLayerKinds.Object,
                    CoverageGroup = BlueprintLayerKinds.Object,
                    ContentId = 10,
                    Status = BlueprintProjectionLayerStatuses.Missing,
                    WorldTileX = 12,
                    WorldTileY = 13
                },
                new BlueprintProjectionCellSnapshot
                {
                    InstanceId = "placed",
                    LayerKind = BlueprintLayerKinds.Wire,
                    CoverageGroup = BlueprintLayerKinds.Wire,
                    ContentId = BlueprintCaptureWireFlags.Red,
                    Status = BlueprintProjectionLayerStatuses.Missing,
                    WorldTileX = 12,
                    WorldTileY = 13
                }
            };
            BlueprintProjectionService.ApplyBottomWallGhostVisualPolicy(layers);
            var snapshot = new BlueprintProjectionSnapshot
            {
                LoadSucceeded = true,
                ProjectedLayers = layers,
                AllProjectedLayers = layers
            };

            var worldOrder = BlueprintProjectionOverlay.BuildWorldWallDrawOrderForTesting(snapshot, null);
            var lateOrder = BlueprintProjectionOverlay.BuildLateProjectionDrawOrderForTesting(snapshot, null);
            var combinedOrder = BlueprintProjectionOverlay.BuildProjectionDrawOrderForTesting(snapshot, null);
            var pixelStack = BlueprintProjectionOverlay.BuildProjectionPixelStackForTesting(snapshot, null, 12, 13, true);

            if (IndexOfDrawOrder(worldOrder, "world-placed:wall:12,13") < 0 ||
                IndexOfDrawOrder(worldOrder, "world-placed:object:12,13") >= 0 ||
                IndexOfDrawOrder(worldOrder, "world-placed:wire:12,13") >= 0)
            {
                throw new InvalidOperationException("Expected the early world layer to draw only blueprint wall projection.");
            }

            if (IndexOfDrawOrder(lateOrder, "placed:wall:12,13") >= 0 ||
                IndexOfDrawOrder(lateOrder, "placed:object:12,13") < 0 ||
                IndexOfDrawOrder(lateOrder, "placed:wire:12,13") < 0)
            {
                throw new InvalidOperationException("Expected the late projection overlay to skip wall and keep foreground projection layers.");
            }

            var wallIndex = IndexOfDrawOrder(combinedOrder, "world-placed:wall:12,13");
            var objectIndex = IndexOfDrawOrder(combinedOrder, "placed:object:12,13");
            var wireIndex = IndexOfDrawOrder(combinedOrder, "placed:wire:12,13");
            if (wallIndex < 0 ||
                objectIndex < 0 ||
                wireIndex < 0 ||
                wallIndex > objectIndex ||
                wallIndex > wireIndex ||
                !BlueprintProjectionOverlay.GetVisualContractForTesting().Contains("late-overlay-skips-wall") ||
                !BlueprintProjectionWallWorldOverlay.GetVisualContractForTesting().Contains("before-terraria-foreground"))
            {
                throw new InvalidOperationException("Expected wall ghost lifecycle to be world-layer first, then late projection foreground.");
            }

            var pixelWallIndex = IndexOfDrawOrder(pixelStack, "world-placed:wall:12,13");
            var pixelTerrariaForegroundIndex = IndexOfDrawOrder(pixelStack, "terraria-foreground:12,13");
            var pixelObjectIndex = IndexOfDrawOrder(pixelStack, "placed:object:12,13");
            var pixelWireIndex = IndexOfDrawOrder(pixelStack, "placed:wire:12,13");
            if (pixelWallIndex < 0 ||
                pixelTerrariaForegroundIndex < 0 ||
                pixelObjectIndex < 0 ||
                pixelWireIndex < 0 ||
                pixelWallIndex > pixelTerrariaForegroundIndex ||
                pixelTerrariaForegroundIndex > pixelObjectIndex ||
                pixelObjectIndex > pixelWireIndex ||
                !BlueprintProjectionOverlay.GetVisualContractForTesting().Contains("terraria-foreground-between-wall-and-late-projection") ||
                !BlueprintProjectionWallWorldOverlay.GetVisualContractForTesting().Contains("world-draw-guard") ||
                !UiDrawLifecycleGuard.GetWorldDrawLifecycleContractForTesting().Contains("no-begin-end"))
            {
                throw new InvalidOperationException("Expected same-pixel stack validation to keep Terraria foreground above world wall and below late projection foreground.");
            }
        }

        private static void BlueprintProjectionWallDiagnosticsSeparateTypePresenceAndFrameMismatch()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-wall-diagnostics");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionWallBlockTemplate("02 墙诊断", 7);
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-wall-diag", "world-wall-diag", template, 20, 30, 0, out instance), "create wall diagnostics instance");
                reader.Set(20, 30, new BlueprintWorldTileSnapshot { WallType = 7, WallFrameX = 0, WallFrameY = 0 });
                reader.Set(21, 30, new BlueprintWorldTileSnapshot { WallType = 7, WallFrameX = 0, WallFrameY = 0 });
                reader.Set(20, 31, new BlueprintWorldTileSnapshot { WallType = 7, WallFrameX = 0, WallFrameY = 0 });
                reader.Set(21, 31, new BlueprintWorldTileSnapshot { WallType = 7, WallFrameX = 0, WallFrameY = 0 });

                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-wall-diag", "world-wall-diag"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                if (!snapshot.LoadSucceeded ||
                    snapshot.EffectiveLayerCount != 4 ||
                    snapshot.FulfilledLayerCount != 4 ||
                    snapshot.MissingLayerCount != 0 ||
                    snapshot.WallTargetLayerCount != 4 ||
                    snapshot.WallTypePresentLayerCount != 4 ||
                    snapshot.WallTypeMissingLayerCount != 0 ||
                    snapshot.WallTypeConflictLayerCount != 0 ||
                    snapshot.WallCompletedLayerCount != 0 ||
                    snapshot.WallCompletedCurrentMismatchCount != 0 ||
                    snapshot.WallFrameMismatchLayerCount <= 0)
                {
                    throw new InvalidOperationException("Expected wall diagnostics to prove all wall types are present while wall frames still mismatch target continuity.");
                }

                AssertContains(BlueprintProjectionService.BuildUiStateJson(), "\"wallTypePresentLayerCount\":4");
                AssertContains(BlueprintProjectionService.BuildUiStateJson(), "\"wallFrameMismatchLayerCount\":");
                var runtimeSnapshot = RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
                {
                    Initialized = true,
                    Version = "test-blueprint-wall-diagnostics"
                });
                var json = InvokeDiagnosticSnapshotJson(runtimeSnapshot);
                AssertContains(json, "\"BlueprintProjectionWallTargetLayerCount\": 4");
                AssertContains(json, "\"BlueprintProjectionWallTypePresentLayerCount\": 4");
                AssertContains(json, "\"BlueprintProjectionWallFrameMismatchLayerCount\": ");
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionWallDiagnosticsExposeCompletedCurrentMismatch()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-wall-completed-diag");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateProjectionSingleWallTemplate("02 墙完成遮蔽诊断", 7);
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-wall-completed", "world-wall-completed", template, 40, 50, 0, out instance), "create completed wall diagnostics instance");
                reader.Set(40, 50, new BlueprintWorldTileSnapshot { WallType = 7 });

                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-wall-completed", "world-wall-completed"),
                    reader,
                    true);

                var first = BlueprintProjectionService.GetSnapshot();
                if (first.FulfilledLayerCount != 1 ||
                    first.WallTypePresentLayerCount != 1 ||
                    first.WallCompletedCurrentMismatchCount != 0)
                {
                    throw new InvalidOperationException("Expected first wall projection pass to fulfill and persist completed progress.");
                }

                var missingReader = new FakeBlueprintWorldTileReader();
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-wall-completed", "world-wall-completed"),
                    missingReader,
                    true);

                var second = BlueprintProjectionService.GetSnapshot();
                if (second.CompletedLayerCount != 1 ||
                    second.MissingLayerCount != 0 ||
                    second.WallTargetLayerCount != 1 ||
                    second.WallCompletedLayerCount != 1 ||
                    second.WallTypePresentLayerCount != 0 ||
                    second.WallTypeMissingLayerCount != 1 ||
                    second.WallCompletedCurrentMismatchCount != 1 ||
                    second.WallFrameMismatchLayerCount != 0)
                {
                    throw new InvalidOperationException("Expected wall diagnostics to expose current world mismatch behind CompletedLayers masking.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionCacheAvoidsImmediateRecompute()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-cache");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-cache", "world-cache", CreateProjectionTileOnlyTemplate("缓存", 21), 4, 5, 0, out instance), "create cache projection instance");
                reader.Set(4, 5, new BlueprintWorldTileSnapshot());
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-cache", "world-cache"),
                    reader,
                    true);

                var first = BlueprintProjectionService.GetSnapshot();
                var second = BlueprintProjectionService.GetSnapshot();
                if (first.CacheMissCount <= 0 ||
                    second.CacheHitCount <= first.CacheHitCount ||
                    second.CacheMissCount != first.CacheMissCount)
                {
                    throw new InvalidOperationException("Expected immediate blueprint projection resolve to reuse the cadence cache.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionReplacementRulesFulfillConfiguredSameCategory()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-replacement-rules");
            try
            {
                ConfigService.Initialize();
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateAutoPlacementLayerTemplate("火把替换投影", BlueprintLayerKinds.Object, 4, 1004, 1);
                template.Cells[0].Layers[0].Style = 1;
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-proj-replace", "world-proj-replace", template, 12, 13, 0, out instance), "create replacement projection instance");
                reader.Set(12, 13, new BlueprintWorldTileSnapshot { Active = true, TileType = 4, ObjectStyle = 2 });
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-proj-replace", "world-proj-replace"),
                    reader,
                    true);

                var disabled = BlueprintProjectionService.GetSnapshot();
                if (disabled.ConflictLayerCount != 1 || disabled.FulfilledLayerCount != 0)
                {
                    throw new InvalidOperationException("Expected torch style mismatch to stay conflict while replacement rules are disabled.");
                }

                ConfigService.AppSettings.BlueprintReplacementEnabled = true;
                ConfigService.AppSettings.BlueprintReplacementTorchesEnabled = true;
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-proj-replace", "world-proj-replace"),
                    reader,
                    true);
                var enabled = BlueprintProjectionService.GetSnapshot();
                if (enabled.FulfilledLayerCount != 1 || enabled.ConflictLayerCount != 0)
                {
                    throw new InvalidOperationException("Expected configured torch replacement to fulfill same-category projection mismatch.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionMultitileObjectConflictMarksWholeGroup()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-multitile-object-group");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    store.CreateInstanceFromTemplate(
                        "pair-proj-object-group",
                        "world-proj-object-group",
                        CreateAutoPlacementTwoCellObjectTemplate("整件家具", 14, 1010),
                        20,
                        30,
                        0,
                        out instance),
                    "create multitile object projection instance");
                reader.Set(20, 30, new BlueprintWorldTileSnapshot());
                reader.Set(21, 30, new BlueprintWorldTileSnapshot { Active = true, TileType = 999 });
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-proj-object-group", "world-proj-object-group"),
                    reader,
                    true);

                var snapshot = BlueprintProjectionService.GetSnapshot();
                var left = FindProjectedLayerAt(snapshot.AllProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Object, 20, 30);
                var right = FindProjectedLayerAt(snapshot.AllProjectedLayers, instance.InstanceId, BlueprintLayerKinds.Object, 21, 30);
                if (snapshot.MissingLayerCount != 0 ||
                    snapshot.ConflictLayerCount != 2 ||
                    left == null ||
                    right == null ||
                    !string.Equals(left.Status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal) ||
                    !string.Equals(right.Status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(left.ObjectGroupKey) ||
                    !string.Equals(left.ObjectGroupKey, right.ObjectGroupKey, StringComparison.Ordinal) ||
                    !string.Equals(left.ObjectGroupStatus, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal) ||
                    !string.Equals(right.ObjectGroupStatus, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected one conflicted cell to mark the whole multitile object group as blocked for projection.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintProjectionUiOverlayAndDiagnosticsContracts()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-contracts");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-contract", "world-contract", CreateProjectionTileOnlyTemplate("契约", 31), 6, 7, 0, out instance), "create projection contract instance");
                reader.Set(6, 7, new BlueprintWorldTileSnapshot { Active = true, TileType = 31 });
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-contract", "world-contract"),
                    reader,
                    true);

                if (!BlueprintProjectionOverlay.ShouldRegisterWorldOverlayForTesting())
                {
                    throw new InvalidOperationException("Expected blueprint projection overlay to register as a world overlay.");
                }

                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "appearance-ghost");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "original-missing");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "red-conflict");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "gray-unavailable");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "fulfilled-no-mask");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "completed-progress");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "no-cell-border");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "move-floating-follow-preview");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "wire-actuator-original-color");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "missing-no-state-block");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "wall-bottom-layer-complete");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "wall-outer-edge-deemphasis");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "multitile-object-group-conflict");
                AssertContains(LegacyMainWindow.GetBlueprintProjectionVisualContractForTesting(), "appearance-ghost");
                AssertContains(LegacyMainWindow.GetBlueprintProjectionVisualContractForTesting(), "original-missing");
                AssertContains(LegacyMainWindow.GetBlueprintProjectionVisualContractForTesting(), "move-floating-follow-preview");
                AssertContains(LegacyMainWindow.GetBlueprintProjectionVisualContractForTesting(), "wire-actuator-original-color");
                AssertContains(LegacyMainWindow.GetBlueprintProjectionVisualContractForTesting(), "layer-cover");
                AssertContains(LegacyMainWindow.GetBlueprintProjectionVisualContractForTesting(), "multitile-object-group-conflict");
                AssertContains(LegacyMainWindow.BuildBlueprintProjectionSummaryForTesting(), "投影：有效");
                var missingColor = BlueprintProjectionOverlay.ResolveProjectionColorForTesting(BlueprintProjectionLayerStatuses.Missing);
                if (missingColor == null || missingColor.Length != 3 || missingColor[0] != 255 || missingColor[1] != 255 || missingColor[2] != 255)
                {
                    throw new InvalidOperationException("Expected blueprint projection missing render input to keep original texture color instead of yellow state tint.");
                }

                var conflictColor = BlueprintProjectionOverlay.ResolveProjectionColorForTesting(BlueprintProjectionLayerStatuses.Conflict);
                if (conflictColor == null || conflictColor.Length != 3 || conflictColor[0] <= conflictColor[1])
                {
                    throw new InvalidOperationException("Expected blueprint projection conflict color to emphasize red.");
                }

                var unavailableColor = BlueprintProjectionOverlay.ResolveProjectionColorForTesting(BlueprintProjectionLayerStatuses.Unavailable);
                if (unavailableColor == null || unavailableColor.Length != 3 || unavailableColor[0] < 120 || unavailableColor[1] < 120 || unavailableColor[2] < 120 || unavailableColor[0] == missingColor[0])
                {
                    throw new InvalidOperationException("Expected blueprint projection unavailable render input to use a gray unknown state.");
                }

                var redWire = BlueprintProjectionGhostRenderer.ResolveWireChannelColorForTesting(BlueprintProjectionLayerStatuses.Missing, BlueprintCaptureWireFlags.Red, missingColor[0], missingColor[1], missingColor[2]);
                var blueWire = BlueprintProjectionGhostRenderer.ResolveWireChannelColorForTesting(BlueprintProjectionLayerStatuses.Missing, BlueprintCaptureWireFlags.Blue, missingColor[0], missingColor[1], missingColor[2]);
                var greenWire = BlueprintProjectionGhostRenderer.ResolveWireChannelColorForTesting(BlueprintProjectionLayerStatuses.Missing, BlueprintCaptureWireFlags.Green, missingColor[0], missingColor[1], missingColor[2]);
                var yellowWire = BlueprintProjectionGhostRenderer.ResolveWireChannelColorForTesting(BlueprintProjectionLayerStatuses.Missing, BlueprintCaptureWireFlags.Yellow, missingColor[0], missingColor[1], missingColor[2]);
                if (redWire[0] <= redWire[1] ||
                    blueWire[2] <= blueWire[0] ||
                    greenWire[1] <= greenWire[0] ||
                    yellowWire[0] <= yellowWire[2] ||
                    yellowWire[1] <= yellowWire[2])
                {
                    throw new InvalidOperationException("Expected missing blueprint wire details to keep red, blue, green and yellow channel colors.");
                }

                var conflictWire = BlueprintProjectionGhostRenderer.ResolveWireChannelColorForTesting(BlueprintProjectionLayerStatuses.Conflict, BlueprintCaptureWireFlags.Blue, conflictColor[0], conflictColor[1], conflictColor[2]);
                if (conflictWire[0] != conflictColor[0] || conflictWire[1] != conflictColor[1] || conflictWire[2] != conflictColor[2])
                {
                    throw new InvalidOperationException("Expected conflicting blueprint wire details to turn red instead of keeping original channel color.");
                }

                if (BlueprintProjectionOverlay.ResolveProjectionFillAlphaForTesting(BlueprintProjectionLayerStatuses.Fulfilled) != 0 ||
                    BlueprintProjectionOverlay.ResolveProjectionFillAlphaForTesting(BlueprintProjectionLayerStatuses.Completed) != 0 ||
                    BlueprintProjectionOverlay.ResolveProjectionBorderAlphaForTesting(BlueprintProjectionLayerStatuses.Missing) != 0 ||
                    BlueprintProjectionOverlay.ResolveProjectionBorderAlphaForTesting(BlueprintProjectionLayerStatuses.Conflict) != 0 ||
                    BlueprintProjectionOverlay.ShouldDrawFallbackBlockForTesting(BlueprintProjectionLayerStatuses.Missing) ||
                    !BlueprintProjectionOverlay.ShouldDrawFallbackBlockForTesting(BlueprintProjectionLayerStatuses.Conflict) ||
                    !BlueprintProjectionOverlay.ShouldDrawFallbackBlockForTesting(BlueprintProjectionLayerStatuses.Unavailable) ||
                    BlueprintProjectionOverlay.ShouldDrawProjectionLayerForTesting(BlueprintProjectionLayerStatuses.Fulfilled) ||
                    BlueprintProjectionOverlay.ShouldDrawProjectionLayerForTesting(BlueprintProjectionLayerStatuses.Completed) ||
                    !BlueprintProjectionOverlay.ShouldDrawProjectionLayerForTesting(BlueprintProjectionLayerStatuses.Missing) ||
                    !BlueprintProjectionOverlay.ShouldDrawProjectionLayerForTesting(BlueprintProjectionLayerStatuses.Unavailable) ||
                    BlueprintProjectionOverlay.ResolveLayerDrawPassForTesting(BlueprintLayerKinds.Wall) >= BlueprintProjectionOverlay.ResolveLayerDrawPassForTesting(BlueprintLayerKinds.Tile) ||
                    BlueprintProjectionOverlay.ResolveLayerDrawPassForTesting(BlueprintLayerKinds.Tile) >= BlueprintProjectionOverlay.ResolveLayerDrawPassForTesting(BlueprintLayerKinds.Wire))
                {
                    throw new InvalidOperationException("Expected projection overlay to draw original missing ghosts, red conflicts, gray unknowns, bottom-wall order, hidden completed cells and no missing fallback blocks.");
                }

                var runtimeSnapshot = RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
                {
                    Initialized = true,
                    Version = "test-blueprint-projection"
                });
                var json = InvokeDiagnosticSnapshotJson(runtimeSnapshot);
                AssertContains(json, "\"BlueprintProjectionLastStatus\": \"resolved\"");
                AssertContains(json, "\"BlueprintProjectionEffectiveLayerCount\": 1");
                AssertContains(json, "\"BlueprintProjectionFulfilledLayerCount\": 1");
                AssertContains(json, "\"BlueprintProjectionCompletedLayerCount\": 0");
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static BlueprintTemplateRecord CreateProjectionStatusTemplate()
        {
            var template = new BlueprintTemplateRecord
            {
                Name = "投影状态",
                Width = 3,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateTileCell(0, 0, 11));
            template.Cells.Add(CreateWallCell(1, 0, 22));
            template.Cells.Add(CreateTileCell(2, 0, 33));
            return template;
        }

        private static BlueprintTemplateRecord CreateProjectionAirBoundsTemplate()
        {
            var template = new BlueprintTemplateRecord
            {
                Name = "空气边界投影",
                Width = 3,
                Height = 3,
                AnchorX = 1,
                AnchorY = 1
            };
            template.Cells.Add(CreateTileCell(1, 1, 77));
            return template;
        }

        private static BlueprintTemplateRecord CreateProjectionLayerTemplate(string name, int tileType, int wallType)
        {
            var template = CreateProjectionTileOnlyTemplate(name, tileType);
            template.Cells[0].Layers.Add(new BlueprintCellLayerRecord
            {
                LayerKind = BlueprintLayerKinds.Wall,
                ContentId = wallType
            });
            return template;
        }

        private static BlueprintTemplateRecord CreateProjectionTwoTileTemplate(string name, int firstTileType, int secondTileType)
        {
            var template = new BlueprintTemplateRecord
            {
                Name = name,
                Width = 2,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateTileCell(0, 0, firstTileType));
            template.Cells.Add(CreateTileCell(1, 0, secondTileType));
            return template;
        }

        private static BlueprintTemplateRecord CreateProjectionTileOnlyTemplate(string name, int tileType)
        {
            var template = new BlueprintTemplateRecord
            {
                Name = name,
                Width = 1,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateTileCell(0, 0, tileType));
            return template;
        }

        private static BlueprintCellRecord CreateTileCell(int x, int y, int tileType)
        {
            return new BlueprintCellRecord
            {
                X = x,
                Y = y,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Tile,
                        ContentId = tileType
                    }
                }
            };
        }

        private static BlueprintCellRecord CreateWallCell(int x, int y, int wallType)
        {
            return new BlueprintCellRecord
            {
                X = x,
                Y = y,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Wall,
                        ContentId = wallType
                    }
                }
            };
        }

        private static BlueprintTemplateRecord CreateProjectionWallBlockTemplate(string name, int wallType)
        {
            var template = new BlueprintTemplateRecord
            {
                Name = name,
                Width = 2,
                Height = 2,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateWallCell(0, 0, wallType));
            template.Cells.Add(CreateWallCell(1, 0, wallType));
            template.Cells.Add(CreateWallCell(0, 1, wallType));
            template.Cells.Add(CreateWallCell(1, 1, wallType));
            return template;
        }

        private static BlueprintTemplateRecord CreateProjectionSingleWallTemplate(string name, int wallType)
        {
            var template = new BlueprintTemplateRecord
            {
                Name = name,
                Width = 1,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateWallCell(0, 0, wallType));
            return template;
        }

        private static BlueprintTemplateRecord CreateProjectionWallWithObjectTemplate(string name, int wallType, int objectType)
        {
            var template = CreateProjectionSingleWallTemplate(name, wallType);
            template.Cells[0].Layers.Add(new BlueprintCellLayerRecord
            {
                LayerKind = BlueprintLayerKinds.Object,
                ContentId = objectType
            });
            return template;
        }

        private static BlueprintTemplateRecord CreateProjectionWallWithNeighborTileTemplate(string name, int wallType, int tileType)
        {
            var template = new BlueprintTemplateRecord
            {
                Name = name,
                Width = 2,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateWallCell(0, 0, wallType));
            template.Cells.Add(CreateTileCell(1, 0, tileType));
            return template;
        }

        private static int IndexOfDrawOrder(IReadOnlyList<string> drawOrder, string value)
        {
            for (var index = 0; drawOrder != null && index < drawOrder.Count; index++)
            {
                if (string.Equals(drawOrder[index], value, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool HasProjectedLayer(
            System.Collections.Generic.IReadOnlyList<BlueprintProjectionCellSnapshot> layers,
            string instanceId,
            string layerKind)
        {
            for (var index = 0; layers != null && index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer != null &&
                    string.Equals(layer.InstanceId, instanceId, StringComparison.Ordinal) &&
                    string.Equals(layer.LayerKind, layerKind, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasProjectedLayerAt(
            System.Collections.Generic.IReadOnlyList<BlueprintProjectionCellSnapshot> layers,
            string instanceId,
            string layerKind,
            int worldTileX,
            int worldTileY)
        {
            for (var index = 0; layers != null && index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer != null &&
                    string.Equals(layer.InstanceId, instanceId, StringComparison.Ordinal) &&
                    string.Equals(layer.LayerKind, layerKind, StringComparison.OrdinalIgnoreCase) &&
                    layer.WorldTileX == worldTileX &&
                    layer.WorldTileY == worldTileY)
                {
                    return true;
                }
            }

            return false;
        }

        private static BlueprintProjectionCellSnapshot FindProjectedLayerAt(
            System.Collections.Generic.IReadOnlyList<BlueprintProjectionCellSnapshot> layers,
            string instanceId,
            string layerKind,
            int worldTileX,
            int worldTileY)
        {
            for (var index = 0; layers != null && index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer != null &&
                    string.Equals(layer.InstanceId, instanceId, StringComparison.Ordinal) &&
                    string.Equals(layer.LayerKind, layerKind, StringComparison.OrdinalIgnoreCase) &&
                    layer.WorldTileX == worldTileX &&
                    layer.WorldTileY == worldTileY)
                {
                    return layer;
                }
            }

            return null;
        }
    }
}
