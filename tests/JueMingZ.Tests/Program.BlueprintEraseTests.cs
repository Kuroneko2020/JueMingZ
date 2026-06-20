using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Diagnostics;
using JueMingZ.Hooks;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintEraseSingleInstanceClipsProjectionAndMaterials()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-erase-single-instance");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var template = CreateEraseMaterialTemplate();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-erase", "world-erase", template, 10, 20, 0, out instance), "create erase target instance");

                BlueprintEraseRegionState.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-erase", "world-erase"));
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-erase", "world-erase"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);

                var begin = BlueprintEraseRegionState.BeginErase(instance.InstanceId);
                if (!begin.Succeeded || !begin.Changed)
                {
                    throw new InvalidOperationException("Expected selected instance erase mode to start.");
                }

                BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
                {
                    WorldTileHit = true,
                    TileX = 10,
                    TileY = 20,
                    LeftDown = true,
                    LeftPressed = true
                });
                var erase = BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
                {
                    WorldTileHit = true,
                    TileX = 10,
                    TileY = 20,
                    LeftReleased = true
                });
                if (!erase.Succeeded || !erase.ErasedRegion || erase.ErasedCellCount != 1)
                {
                    throw new InvalidOperationException("Expected a one-cell erase rectangle to persist one instance-local erase mask cell.");
                }

                var projection = BlueprintProjectionService.GetSnapshot();
                var materials = BlueprintMaterialService.GetSnapshot();
                if (projection.ErasedLayerCount != 1 ||
                    projection.EffectiveLayerCount != 1 ||
                    projection.MissingLayerCount != 1 ||
                    materials.RequiredItemCount != 1 ||
                    materials.RequiredStackTotal != 6 ||
                    materials.Items.Count != 1 ||
                    materials.Items[0].ItemId != 702)
                {
                    throw new InvalidOperationException("Expected erased blueprint cells to disappear from projection and material demand.");
                }

                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(store.TryLoadWorld("pair-erase", out saved), "load erased instance world");
                var savedInstance = FindPlacedInstance(saved, instance.InstanceId);
                if (savedInstance.EraseMask.Count != 1 ||
                    savedInstance.TemplateSnapshot.Cells.Count != 2 ||
                    template.Cells.Count != 2)
                {
                    throw new InvalidOperationException("Expected erase mode to mutate only instance erase mask while keeping template cells intact.");
                }
            }
            finally
            {
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintEraseSelectionPrefersSelectedAndTopLayerFallback()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-erase-target-selection");
            try
            {
                var selectedStore = new BlueprintWorldInstanceStore();
                var selectedReader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord selectedLower;
                BlueprintWorldInstanceRecord selectedUpper;
                RequireBlueprintSuccess(selectedStore.CreateInstanceFromTemplate("pair-selected", "world-selected", CreateProjectionTileOnlyTemplate("选中下层", 71), 5, 6, 0, out selectedLower), "create selected lower");
                RequireBlueprintSuccess(selectedStore.CreateInstanceFromTemplate("pair-selected", "world-selected", CreateProjectionTileOnlyTemplate("选中上层", 72), 5, 6, 1, out selectedUpper), "create selected upper");

                BlueprintEraseRegionState.SetDependenciesForTesting(selectedStore, BlueprintPlacementWorldContext.Success("pair-selected", "world-selected"));
                BlueprintProjectionService.SetDependenciesForTesting(selectedStore, BlueprintPlacementWorldContext.Success("pair-selected", "world-selected"), selectedReader, true);
                EraseOneCell(selectedLower.InstanceId, 5, 6);

                var selectedProjection = BlueprintProjectionService.GetSnapshot();
                if (selectedProjection.ErasedLayerCount != 1 ||
                    !HasProjectedLayer(selectedProjection.ProjectedLayers, selectedUpper.InstanceId, BlueprintLayerKinds.Tile) ||
                    HasProjectedLayer(selectedProjection.ProjectedLayers, selectedLower.InstanceId, BlueprintLayerKinds.Tile))
                {
                    throw new InvalidOperationException(
                        "Expected explicit selected instance erase to clip the selected lower layer, not the topmost layer. " +
                        "erased=" + selectedProjection.ErasedLayerCount +
                        ", hasUpper=" + HasProjectedLayer(selectedProjection.ProjectedLayers, selectedUpper.InstanceId, BlueprintLayerKinds.Tile) +
                        ", hasLower=" + HasProjectedLayer(selectedProjection.ProjectedLayers, selectedLower.InstanceId, BlueprintLayerKinds.Tile) +
                        ", layers=" + selectedProjection.ProjectedLayers.Count);
                }

                BlueprintEraseRegionState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();

                var fallbackStore = new BlueprintWorldInstanceStore();
                var fallbackReader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord fallbackLower;
                BlueprintWorldInstanceRecord fallbackUpper;
                RequireBlueprintSuccess(fallbackStore.CreateInstanceFromTemplate("pair-fallback", "world-fallback", CreateProjectionTileOnlyTemplate("fallback 下层", 81), 7, 8, 0, out fallbackLower), "create fallback lower");
                RequireBlueprintSuccess(fallbackStore.CreateInstanceFromTemplate("pair-fallback", "world-fallback", CreateProjectionTileOnlyTemplate("fallback 上层", 82), 7, 8, 1, out fallbackUpper), "create fallback upper");

                BlueprintEraseRegionState.SetDependenciesForTesting(fallbackStore, BlueprintPlacementWorldContext.Success("pair-fallback", "world-fallback"));
                BlueprintProjectionService.SetDependenciesForTesting(fallbackStore, BlueprintPlacementWorldContext.Success("pair-fallback", "world-fallback"), fallbackReader, true);
                var fallbackErase = EraseOneCell(string.Empty, 7, 8);
                if (!string.Equals(fallbackErase.TargetInstanceId, fallbackUpper.InstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected hover-target erase mode to choose the topmost visible instance under the mouse.");
                }

                var fallbackProjection = BlueprintProjectionService.GetSnapshot();
                if (fallbackProjection.ErasedLayerCount != 1 ||
                    !HasProjectedLayer(fallbackProjection.ProjectedLayers, fallbackLower.InstanceId, BlueprintLayerKinds.Tile) ||
                    HasProjectedLayer(fallbackProjection.ProjectedLayers, fallbackUpper.InstanceId, BlueprintLayerKinds.Tile))
                {
                    throw new InvalidOperationException("Expected top-layer fallback erase to reveal the lower projected layer.");
                }
            }
            finally
            {
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintEraseUiOverlayAndDiagnosticsContracts()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-erase-contracts");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-erase-diag", "world-erase-diag", CreateProjectionTileOnlyTemplate("擦除诊断", 91), 11, 12, 0, out instance), "create erase diagnostic instance");

                BlueprintEraseRegionState.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-erase-diag", "world-erase-diag"));
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-erase-diag", "world-erase-diag"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);
                var erase = EraseOneCell(instance.InstanceId, 11, 12);
                if (!erase.ErasedRegion)
                {
                    throw new InvalidOperationException("Expected diagnostic erase setup to persist one erased cell.");
                }

                if (!BlueprintEraseRegionOverlay.ShouldRegisterWorldOverlayForTesting())
                {
                    throw new InvalidOperationException("Expected blueprint erase region overlay to stay registered as a world overlay.");
                }

                var pointer = BlueprintEraseRegionOverlay.BuildPointerInputForTesting(
                    true,
                    false,
                    true,
                    true,
                    true,
                    false,
                    true,
                    11,
                    12);
                if (!pointer.UiOwned || !pointer.LeftPressed || !pointer.WorldTileHit)
                {
                    throw new InvalidOperationException("Expected erase pointer contract to preserve UI ownership and world tile hit metadata.");
                }

                if (BlueprintEraseRegionOverlay.ShouldConsumeAfterPlayerInputForTesting(true, true, true) ||
                    !BlueprintEraseRegionOverlay.ShouldConsumeAfterPlayerInputForTesting(true, false, true))
                {
                    throw new InvalidOperationException("Expected erase after-PlayerInput guard to respect Legacy UI ownership while consuming world input.");
                }
                BlueprintProjectionService.GetSnapshot();

                AssertContains(BlueprintEraseRegionOverlay.GetVisualContractForTesting(), "store-mask-only");
                AssertContains(LegacyMainWindow.GetBlueprintEraseVisualContractForTesting(), "selected-priority");
                AssertContains(LegacyMainWindow.BuildBlueprintEraseSummaryForTesting(), "擦除：目标");
                AssertContains(string.Join(";", InterfaceLayerHookCallbacks.GetGameOverlayDispatcherRouteNamesForTesting(true)), "BlueprintEraseRegionOverlay.DrawInterfaceLayer");
                AssertContains(string.Join(";", InterfaceLayerHookCallbacks.GetGameOverlayDispatcherRouteNamesForTesting(false)), "BlueprintEraseRegionOverlay.DrawInterfaceLayer");

                var runtimeSnapshot = RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
                {
                    Initialized = true,
                    Version = "test-blueprint-erase"
                });
                var json = InvokeDiagnosticSnapshotJson(runtimeSnapshot);
                AssertContains(json, "\"BlueprintProjectionErasedLayerCount\": 1");
                AssertContains(json, "\"BlueprintEraseRegionActive\": true");
                AssertContains(json, "\"BlueprintEraseRegionLastErasedCellCount\": 1");
                AssertContains(json, "\"BlueprintEraseRegionTargetInstanceId\": \"" + instance.InstanceId + "\"");
            }
            finally
            {
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static BlueprintEraseInteractionResult EraseOneCell(string preferredInstanceId, int tileX, int tileY)
        {
            var begin = BlueprintEraseRegionState.BeginErase(preferredInstanceId);
            if (!begin.Succeeded)
            {
                throw new InvalidOperationException("Expected erase mode to start: " + begin.ResultCode + " / " + begin.Message);
            }

            BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
            {
                WorldTileHit = true,
                TileX = tileX,
                TileY = tileY,
                LeftDown = true,
                LeftPressed = true
            });
            return BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
            {
                WorldTileHit = true,
                TileX = tileX,
                TileY = tileY,
                LeftReleased = true
            });
        }

        private static BlueprintTemplateRecord CreateEraseMaterialTemplate()
        {
            var template = new BlueprintTemplateRecord
            {
                Name = "擦除材料",
                Width = 2,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateMaterialTileCell(0, 0, 61, 701, 4, "被擦除材料"));
            template.Cells.Add(CreateMaterialTileCell(1, 0, 62, 702, 6, "保留材料"));
            AddMaterialEntries(template);
            return template;
        }
    }
}
