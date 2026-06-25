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
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();

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

        private static void BlueprintEraseRegionStage07ContinuousHoverAndCancelOnly()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-region-stage07-continuous");
            try
            {
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();

                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-region-07", "world-region-07", CreateEraseMaterialTemplate(), 30, 40, 0, out instance), "create stage07 region instance");

                var context = BlueprintPlacementWorldContext.Success("pair-region-07", "world-region-07");
                BlueprintEraseRegionState.SetDependenciesForTesting(store, context);
                BlueprintProjectionService.SetDependenciesForTesting(store, context, reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);

                var begin = BlueprintEraseRegionState.BeginErase(string.Empty);
                if (!begin.Succeeded ||
                    !string.Equals(begin.ResultCode, "eraseStartedSingleTarget", StringComparison.Ordinal) ||
                    !string.Equals(begin.TargetInstanceId, instance.InstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage07 region modify to preselect the single visible instance.");
                }

                var hover = BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
                {
                    WorldTileHit = true,
                    TileX = 30,
                    TileY = 40
                });
                var hoverSnapshot = BlueprintEraseRegionState.GetSnapshot();
                if (!hover.Succeeded ||
                    !hoverSnapshot.Active ||
                    !hoverSnapshot.HasHoverTile ||
                    hoverSnapshot.HoverTileX != 30 ||
                    hoverSnapshot.HoverTileY != 40 ||
                    hover.ShouldConsumeLeftInput)
                {
                    throw new InvalidOperationException("Expected stage07 active region modify to track a red hover mask without consuming a non-click frame.");
                }

                BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
                {
                    WorldTileHit = true,
                    TileX = 30,
                    TileY = 40,
                    LeftDown = true,
                    LeftPressed = true
                });
                var erased = BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
                {
                    WorldTileHit = true,
                    TileX = 30,
                    TileY = 40,
                    LeftReleased = true
                });
                var afterErase = BlueprintEraseRegionState.GetSnapshot();
                if (!erased.ErasedRegion ||
                    erased.ErasedCellCount != 1 ||
                    !afterErase.Active ||
                    afterErase.Dragging ||
                    !afterErase.HasHoverTile ||
                    afterErase.LastErasedCellCount != 1)
                {
                    throw new InvalidOperationException("Expected stage07 region modify to persist one erased cell but remain active until explicit cancel.");
                }

                AssertContains(BlueprintEraseRegionOverlay.GetVisualContractForTesting(), "cursor-red-follow-mask");
                AssertContains(BlueprintEraseRegionOverlay.GetVisualContractForTesting(), "cancel-only-exit");
                AssertContains(LegacyMainWindow.GetBlueprintEraseVisualContractForTesting(), "stage07-continuous-region-edit");

                var cancel = BlueprintEraseRegionState.Cancel();
                if (!cancel.Succeeded ||
                    !cancel.Changed ||
                    BlueprintEraseRegionState.GetSnapshot().Active ||
                    BlueprintEraseRegionState.GetSnapshot().HasHoverTile)
                {
                    throw new InvalidOperationException("Expected explicit cancel to be the only exit from stage07 continuous region modify.");
                }
            }
            finally
            {
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintEraseRegionSingleVisibleInstanceAllowsAirStartDrag()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-region-single-air-drag");
            try
            {
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();

                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    store.CreateInstanceFromTemplate(
                        "pair-region-air",
                        "world-region-air",
                        CreateProjectionTileOnlyTemplate("空气起拖", 90),
                        30,
                        40,
                        0,
                        out instance),
                    "create single-instance air-drag target");

                var context = BlueprintPlacementWorldContext.Success("pair-region-air", "world-region-air");
                BlueprintEraseRegionState.SetDependenciesForTesting(store, context);
                BlueprintProjectionService.SetDependenciesForTesting(store, context, reader, true);

                var begin = BlueprintEraseRegionState.BeginErase(string.Empty);
                if (!begin.Succeeded ||
                    !string.Equals(begin.ResultCode, "eraseStartedSingleTarget", StringComparison.Ordinal) ||
                    !string.Equals(begin.TargetInstanceId, instance.InstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected a single visible placed instance to be fixed when region modify starts.");
                }

                var start = BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
                {
                    WorldTileHit = true,
                    TileX = 28,
                    TileY = 40,
                    LeftDown = true,
                    LeftPressed = true
                });
                var afterStart = BlueprintEraseRegionState.GetSnapshot();
                if (!start.Succeeded ||
                    !start.ShouldConsumeLeftInput ||
                    !afterStart.Dragging ||
                    afterStart.DragStartX != 28 ||
                    !string.Equals(afterStart.TargetInstanceId, instance.InstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected air-origin press to start dragging against the fixed single target.");
                }

                var release = BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
                {
                    WorldTileHit = true,
                    TileX = 30,
                    TileY = 40,
                    LeftReleased = true
                });
                if (!release.ErasedRegion || release.ErasedCellCount != 1)
                {
                    throw new InvalidOperationException("Expected air-origin drag rectangle to erase the single target when it reaches blueprint content.");
                }

                BlueprintProjectionService.RefreshAfterWorldInstancesChanged();
                var projection = BlueprintProjectionService.GetDiagnostics();
                if (!projection.LoadSucceeded ||
                    projection.InstanceCount != 1 ||
                    projection.EffectiveLayerCount != 0)
                {
                    throw new InvalidOperationException("Expected air-origin region erase to leave the instance record but remove all effective projection layers.");
                }
            }
            finally
            {
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintEraseRegionPhysicalLeftEdgesIgnoreConsumedWorldLeft()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-region-physical-left-consumed");
            try
            {
                ResetUiInputFrameTestState();
                BlueprintUiClickDiagnostics.ResetForTesting();
                BlueprintEraseRegionOverlay.ResetInputForTesting();
                BlueprintEraseRegionState.ResetForTesting();

                var store = new BlueprintWorldInstanceStore();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    store.CreateInstanceFromTemplate(
                        "pair-region-physical-left",
                        "world-region-physical-left",
                        CreateEraseMaterialTemplate(),
                        30,
                        40,
                        0,
                        out instance),
                    "create physical-left erase instance");

                BlueprintEraseRegionState.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-region-physical-left", "world-region-physical-left"));

                var begin = BlueprintEraseRegionState.BeginErase(instance.InstanceId);
                if (!begin.Succeeded)
                {
                    throw new InvalidOperationException("Expected erase mode to start before physical-left consumed frame test.");
                }

                var startInput = BlueprintEraseRegionOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    false,
                    true,
                    true,
                    false,
                    false,
                    true,
                    30,
                    40);
                if (!startInput.LeftDown || !startInput.LeftPressed || startInput.LeftReleased)
                {
                    throw new InvalidOperationException("Expected physical/world left edge to start region erase drag.");
                }

                var start = BlueprintEraseRegionState.HandlePointer(startInput);
                if (!start.ShouldConsumeLeftInput || !BlueprintEraseRegionState.GetSnapshot().Dragging)
                {
                    throw new InvalidOperationException("Expected region erase drag to start before the consumed-left frame.");
                }

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-region-physical-left-consumed-held");
                UiPointerOwnershipService.RegisterPointerOwnerForCurrentFrame(
                    "blueprint-left-consumed",
                    "BlueprintHandheldActionBar",
                    new LegacyUiRect(500, 500, 80, 48),
                    true,
                    true,
                    false,
                    "left");
                var rawConsumedHeld = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 32,
                    TerrariaMouseY = 32,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 32,
                    OsClientMouseY = 32,
                    OsLeftDown = true,
                    ReadMode = "TestErasePhysicalConsumedHeld"
                };
                var consumedOwnership = UiPointerOwnershipService.ResolveWorldPointerOwnership(rawConsumedHeld);
                var consumedWorldLeft = UiPointerOwnershipService.ResolveWorldLeftDown(rawConsumedHeld);
                var consumedPhysicalLeft = BlueprintEraseRegionOverlay.ResolvePhysicalLeftDownForTesting(rawConsumedHeld);
                var consumedPointerBlock = BlueprintEraseRegionOverlay.ShouldBlockEraseForPointerOwnershipForTesting(consumedOwnership);
                if (!consumedOwnership.PointerBlocksWorldLeft ||
                    consumedOwnership.PointerBlocksHoverOrDrag ||
                    consumedPointerBlock ||
                    consumedWorldLeft ||
                    !consumedPhysicalLeft)
                {
                    throw new InvalidOperationException("Expected consumed-left outside owner bounds to block only world-left while region erase physical left remains held.");
                }

                var consumedInput = BlueprintEraseRegionOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    consumedPointerBlock,
                    consumedWorldLeft,
                    consumedPhysicalLeft,
                    true,
                    false,
                    true,
                    31,
                    40);
                if (consumedInput.UiOwned ||
                    consumedInput.LeftDown ||
                    consumedInput.LeftPressed ||
                    consumedInput.LeftReleased)
                {
                    throw new InvalidOperationException("Consumed world-left must not become UI-owned or fake a release while physical left is still held.");
                }

                var consumed = BlueprintEraseRegionState.HandlePointer(consumedInput);
                var afterConsumed = BlueprintEraseRegionState.GetSnapshot();
                if (consumed.ShouldConsumeLeftInput ||
                    consumed.ErasedRegion ||
                    !afterConsumed.Active ||
                    !afterConsumed.Dragging ||
                    !afterConsumed.HasHoverTile ||
                    afterConsumed.HoverTileX != 31 ||
                    afterConsumed.HoverTileY != 40 ||
                    afterConsumed.DragCurrentX != 30 ||
                    afterConsumed.DragCurrentY != 40)
                {
                    throw new InvalidOperationException("Consumed-held region frame should keep drag and hover alive without applying erase cells.");
                }

                UiInputFrameClock.BeginUpdateFrame("test.blueprint-region-physical-left-release");
                var rawRelease = new DiagnosticMouseState
                {
                    GameInputAvailable = true,
                    TerrariaReadAvailable = true,
                    TerrariaMouseX = 31 * 16,
                    TerrariaMouseY = 40 * 16,
                    TerrariaLeftDown = false,
                    OsReadAvailable = true,
                    OsClientMouseX = 31 * 16,
                    OsClientMouseY = 40 * 16,
                    OsLeftDown = false,
                    ReadMode = "TestErasePhysicalRelease"
                };
                var releaseInput = BlueprintEraseRegionOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    false,
                    UiPointerOwnershipService.ResolveWorldLeftDown(rawRelease),
                    BlueprintEraseRegionOverlay.ResolvePhysicalLeftDownForTesting(rawRelease),
                    true,
                    false,
                    true,
                    31,
                    40);
                if (!releaseInput.LeftReleased || releaseInput.LeftPressed || releaseInput.LeftDown)
                {
                    throw new InvalidOperationException("Expected true physical release to complete region erase after consumed-held frame.");
                }

                var released = BlueprintEraseRegionState.HandlePointer(releaseInput);
                var afterRelease = BlueprintEraseRegionState.GetSnapshot();
                if (!released.ErasedRegion ||
                    released.ErasedCellCount != 2 ||
                    afterRelease.Dragging ||
                    afterRelease.LastErasedCellCount != 2)
                {
                    throw new InvalidOperationException("Expected true physical release to apply the final erase rectangle exactly once.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                BlueprintUiClickDiagnostics.ResetForTesting();
                BlueprintEraseRegionOverlay.ResetInputForTesting();
                BlueprintEraseRegionState.ResetForTesting();
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
            var result = BlueprintEraseRegionState.HandlePointer(new BlueprintErasePointerInput
            {
                WorldTileHit = true,
                TileX = tileX,
                TileY = tileY,
                LeftReleased = true
            });
            if (result != null && result.ErasedRegion)
            {
                BlueprintProjectionService.RefreshAfterWorldInstancesChanged();
                BlueprintMaterialService.ForceRefreshForPlacedInstanceList();
            }

            return result;
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
