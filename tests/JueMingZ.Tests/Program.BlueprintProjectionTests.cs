using System;
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

        private static void BlueprintProjectionCacheAvoidsImmediateRecompute()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-projection-cache");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-cache", "world-cache", CreateProjectionTileOnlyTemplate("缓存", 21), 4, 5, 0, out instance), "create cache projection instance");
                reader.Set(4, 5, new BlueprintWorldTileSnapshot { Active = true, TileType = 21 });
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
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "yellow-missing");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "red-conflict");
                AssertContains(BlueprintProjectionOverlay.GetVisualContractForTesting(), "fulfilled-no-mask");
                AssertContains(LegacyMainWindow.GetBlueprintProjectionVisualContractForTesting(), "appearance-ghost");
                AssertContains(LegacyMainWindow.GetBlueprintProjectionVisualContractForTesting(), "layer-cover");
                AssertContains(LegacyMainWindow.BuildBlueprintProjectionSummaryForTesting(), "投影：有效");
                var missingColor = BlueprintProjectionOverlay.ResolveProjectionColorForTesting(BlueprintProjectionLayerStatuses.Missing);
                if (missingColor == null || missingColor.Length != 3 || missingColor[0] < 240 || missingColor[1] < 200 || missingColor[2] > 120)
                {
                    throw new InvalidOperationException("Expected blueprint projection missing color to use the stage-05 yellow ghost semantics.");
                }

                var conflictColor = BlueprintProjectionOverlay.ResolveProjectionColorForTesting(BlueprintProjectionLayerStatuses.Conflict);
                if (conflictColor == null || conflictColor.Length != 3 || conflictColor[0] <= conflictColor[1])
                {
                    throw new InvalidOperationException("Expected blueprint projection conflict color to emphasize red.");
                }

                if (BlueprintProjectionOverlay.ResolveProjectionFillAlphaForTesting(BlueprintProjectionLayerStatuses.Fulfilled) != 0 ||
                    BlueprintProjectionOverlay.ShouldDrawProjectionLayerForTesting(BlueprintProjectionLayerStatuses.Fulfilled) ||
                    !BlueprintProjectionOverlay.ShouldDrawProjectionLayerForTesting(BlueprintProjectionLayerStatuses.Missing) ||
                    BlueprintProjectionOverlay.ResolveLayerDrawPassForTesting(BlueprintLayerKinds.Wall) >= BlueprintProjectionOverlay.ResolveLayerDrawPassForTesting(BlueprintLayerKinds.Tile) ||
                    BlueprintProjectionOverlay.ResolveLayerDrawPassForTesting(BlueprintLayerKinds.Wire) <= BlueprintProjectionOverlay.ResolveLayerDrawPassForTesting(BlueprintLayerKinds.Tile))
                {
                    throw new InvalidOperationException("Expected stage-05 projection overlay to draw wall/tile/wire ghosts in order and skip fulfilled masks.");
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
    }
}
