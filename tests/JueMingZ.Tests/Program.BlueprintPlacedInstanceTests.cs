using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Input;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintPlacedInstancesUiStateLoadsCurrentWorldAndKeepsSnapshots()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placed-list-current-world");
            try
            {
                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateSampleBlueprintTemplate("楼梯"), out template), "create placed-list template");

                var firstInstanceId = string.Empty;
                for (var index = 0; index < BlueprintPlacedInstanceUiState.PageSize + 1; index++)
                {
                    BlueprintWorldInstanceRecord instance;
                    RequireBlueprintSuccess(
                        instanceStore.CreateInstanceFromTemplate("pair-a", "world-a", template, 10 + index, 20, index, out instance),
                        "create current-world blueprint instance");
                    if (index == 0)
                    {
                        firstInstanceId = instance.InstanceId;
                    }
                }

                BlueprintWorldInstanceRecord otherWorld;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate("pair-b", "world-b", template, 99, 100, 0, out otherWorld),
                    "create other-world blueprint instance");

                BlueprintTemplateRecord renamed;
                RequireBlueprintSuccess(templateStore.RenameTemplate(template.TemplateId, "改名后模板", out renamed), "rename source template");
                RequireBlueprintSuccess(templateStore.DeleteTemplate(template.TemplateId), "delete source template after placement");

                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-a", "world-a"),
                    true);

                var snapshot = BlueprintPlacedInstanceUiState.GetSnapshot();
                if (!snapshot.LoadSucceeded ||
                    snapshot.Instances.Count != BlueprintPlacedInstanceUiState.PageSize + 1 ||
                    snapshot.PageCount != 2 ||
                    snapshot.VisibleCount != BlueprintPlacedInstanceUiState.PageSize ||
                    snapshot.VisibleStartIndex != 0)
                {
                    throw new InvalidOperationException("Expected placed blueprint UI state to load only the current world and expose stable paging.");
                }

                AssertStringEquals(snapshot.WorldPairKey, "pair-a", "placed blueprint world pair");
                AssertStringEquals(snapshot.SelectedInstanceId, firstInstanceId, "placed blueprint default selection");
                AssertStringEquals(snapshot.Instances[0].TemplateSnapshot.Name, "楼梯", "placed blueprint template snapshot remains isolated");
                AssertContains(LegacyMainWindow.GetBlueprintPlacedInstanceVisualContractForTesting(), "template-snapshot-isolated");
                if (LegacyMainWindow.GetBlueprintPlacedInstancePageSizeForTesting() != BlueprintPlacedInstanceUiState.PageSize)
                {
                    throw new InvalidOperationException("Expected placed blueprint page size testing hook to match UI state.");
                }

                BlueprintPlacedInstanceUiState.MovePage(1);
                var second = BlueprintPlacedInstanceUiState.GetSnapshot();
                if (second.PageIndex != 1 || second.VisibleStartIndex != BlueprintPlacedInstanceUiState.PageSize || second.VisibleCount != 1)
                {
                    throw new InvalidOperationException("Expected placed blueprint next page to expose the overflow instance.");
                }
            }
            finally
            {
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintPlacedInstanceCommandsToggleRemoveSelectAndLayer()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placed-list-commands");
            try
            {
                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();

                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateSampleBlueprintTemplate("实例命令模板"), out template), "create placed-command template");

                BlueprintWorldInstanceRecord first;
                BlueprintWorldInstanceRecord second;
                BlueprintWorldInstanceRecord third;
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-c", "world-c", template, 1, 2, 0, out first), "create first placed instance");
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-c", "world-c", template, 3, 4, 1, out second), "create second placed instance");
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-c", "world-c", template, 5, 6, 2, out third), "create third placed instance");

                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-c", "world-c"),
                    true);

                RunPlacedInstanceCommand("select", third.InstanceId);
                var selected = BlueprintPlacedInstanceUiState.GetSnapshot();
                AssertStringEquals(selected.SelectedInstanceId, third.InstanceId, "placed blueprint select command");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault()).Mode, BlueprintEntryModes.PlacedManagement, "placed blueprint command opens management mode");

                RunPlacedInstanceCommand("toggle-hidden", second.InstanceId);
                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-c", out saved), "load after hidden toggle");
                var hiddenUi = BlueprintPlacedInstanceUiState.GetSnapshot();
                var hiddenStored = FindPlacedInstance(saved, second.InstanceId);
                if (!hiddenStored.Hidden)
                {
                    throw new InvalidOperationException("Expected placed blueprint hidden command to persist Hidden=true; ui result=" + hiddenUi.LastResultCode + ", selected=" + hiddenUi.SelectedInstanceId + ", storedHidden=" + hiddenStored.Hidden + ".");
                }

                RunPlacedInstanceCommand("toggle-hidden", second.InstanceId);
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-c", out saved), "load after show toggle");
                if (FindPlacedInstance(saved, second.InstanceId).Hidden)
                {
                    throw new InvalidOperationException("Expected placed blueprint show command to persist Hidden=false.");
                }

                RunPlacedInstanceCommand("layer-down", third.InstanceId);
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-c", out saved), "load after layer down");
                if (FindPlacedInstance(saved, third.InstanceId).LayerOrder != 1 ||
                    FindPlacedInstance(saved, second.InstanceId).LayerOrder != 2)
                {
                    throw new InvalidOperationException("Expected placed blueprint layer-down to lower the selected instance and keep a higher layer for the other instance.");
                }

                RunPlacedInstanceCommand("layer-up", third.InstanceId);
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-c", out saved), "load after layer up");
                if (FindPlacedInstance(saved, third.InstanceId).LayerOrder != 2)
                {
                    throw new InvalidOperationException("Expected placed blueprint layer-up to restore the selected instance to the highest layer.");
                }

                RunPlacedInstanceCommand("remove", first.InstanceId);
                if (!string.Equals(BlueprintPlacedInstanceUiState.GetSnapshot().RemoveConfirmInstanceId, first.InstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected first placed blueprint remove click to arm confirmation only.");
                }

                RunPlacedInstanceCommand("remove", first.InstanceId);
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-c", out saved), "load after remove confirm");
                if (saved.Instances.Count != 2 || FindPlacedInstanceOrNull(saved, first.InstanceId) != null)
                {
                    throw new InvalidOperationException("Expected confirmed placed blueprint remove command to delete only the instance record.");
                }

                BlueprintTemplateLibrarySnapshot templates;
                RequireBlueprintSuccess(templateStore.TryLoad(out templates), "load templates after instance remove");
                if (templates.Templates.Count != 1)
                {
                    throw new InvalidOperationException("Removing a placed instance must not remove the source template.");
                }
            }
            finally
            {
                BlueprintEntryState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintPlacedInstanceClearAllCurrentWorldKeepsTemplatesAndRefreshesCaches()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placed-clear-all");
            try
            {
                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintEraseRegionState.ResetForTesting();

                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateEraseMaterialTemplate(), out template), "create clear-all source template");

                BlueprintWorldInstanceRecord first;
                BlueprintWorldInstanceRecord second;
                BlueprintWorldInstanceRecord otherWorld;
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-clear", "world-clear", template, 10, 20, 0, out first), "create first clear-all instance");
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-clear", "world-clear", template, 12, 20, 1, out second), "create second clear-all instance");
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-other-clear", "world-other-clear", template, 50, 60, 0, out otherWorld), "create other-world clear-all instance");

                var context = BlueprintPlacementWorldContext.Success("pair-clear", "world-clear");
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(instanceStore, context, true);
                BlueprintProjectionService.SetDependenciesForTesting(instanceStore, context, reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);

                var beforeProjection = BlueprintProjectionService.GetSnapshot();
                var beforeMaterials = BlueprintMaterialService.GetSnapshot();
                if (beforeProjection.EffectiveLayerCount <= 0 || beforeMaterials.RequiredItemCount <= 0)
                {
                    throw new InvalidOperationException("Expected clear-all setup to have projection layers and material demand.");
                }

                var result = BlueprintPlacedInstanceUiState.ClearAllCurrentWorld();
                if (!result.Succeeded ||
                    !result.Changed ||
                    !string.Equals(result.ResultCode, "clearPlaced", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected clear-all command to remove current-world placed instances.");
                }

                BlueprintWorldInstanceSnapshot current;
                BlueprintWorldInstanceSnapshot other;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-clear", out current), "load current world after clear-all");
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-other-clear", out other), "load other world after clear-all");
                if (current.Instances.Count != 0 || other.Instances.Count != 1)
                {
                    throw new InvalidOperationException("Clear-all must affect only the current world instance file.");
                }

                BlueprintTemplateLibrarySnapshot templates;
                RequireBlueprintSuccess(templateStore.TryLoad(out templates), "load templates after clear-all");
                if (templates.Templates.Count != 1 || templates.Templates[0].Cells.Count != template.Cells.Count)
                {
                    throw new InvalidOperationException("Clear-all must not delete or trim blueprint library templates.");
                }

                var summary = BlueprintPlacedInstanceUiState.GetCachedSummary();
                var afterProjection = BlueprintProjectionService.GetSnapshot();
                var afterMaterials = BlueprintMaterialService.GetSnapshot();
                if (summary.InstanceCount != 0 ||
                    afterProjection.EffectiveLayerCount != 0 ||
                    afterProjection.ProjectedLayers.Count != 0 ||
                    afterMaterials.RequiredItemCount != 0)
                {
                    throw new InvalidOperationException("Clear-all must refresh placed-list, projection and material caches to empty.");
                }
            }
            finally
            {
                BlueprintEntryState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintPlacedInstanceMoveKeepsSnapshotStateAndRefreshesCaches()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placed-transform-move");
            try
            {
                BlueprintPlacedInstanceTransformState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();

                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateEraseMaterialTemplate(), out template), "create move source template");

                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-move", "world-move", template, 10, 20, 5, out instance), "create move instance");

                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-move", out saved), "load move instance for erase mask");
                var edited = saved.Instances[0].Clone();
                edited.EraseMask.Add(new BlueprintEraseMaskCellRecord { X = 0, Y = 0 });
                RequireBlueprintSuccess(instanceStore.SaveWorldInstances("pair-move", "world-move", new List<BlueprintWorldInstanceRecord> { edited }, out saved), "save move erase mask");

                var context = BlueprintPlacementWorldContext.Success("pair-move", "world-move");
                BlueprintPlacedInstanceTransformState.SetDependenciesForTesting(instanceStore, context);
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(instanceStore, context, true);
                BlueprintProjectionService.SetDependenciesForTesting(instanceStore, context, reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);

                var beforeProjection = BlueprintProjectionService.GetSnapshot();
                var beforeMaterials = BlueprintMaterialService.GetSnapshot();
                if (beforeProjection.EffectiveLayerCount != 1 || beforeMaterials.RequiredItemCount != 1)
                {
                    throw new InvalidOperationException("Expected move setup to see only the non-erased template cell in projection and materials.");
                }

                var start = BlueprintPlacedInstanceTransformState.BeginMove();
                if (!start.Succeeded || !string.Equals(start.ResultCode, "moveTargetSelectStarted", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected move command to enter target selection.");
                }

                var select = BlueprintPlacedInstanceTransformState.HandlePointer(new BlueprintPlacedInstanceTransformPointerInput
                {
                    WorldTileHit = true,
                    TileX = 11,
                    TileY = 20,
                    LeftDown = true,
                    LeftPressed = true
                });
                if (!select.Succeeded ||
                    !select.Changed ||
                    !string.Equals(select.ResultCode, "moveTargetSelected", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected move target click to select the placed instance.");
                }

                var moved = BlueprintPlacedInstanceTransformState.HandlePointer(new BlueprintPlacedInstanceTransformPointerInput
                {
                    WorldTileHit = true,
                    TileX = 40,
                    TileY = 50,
                    LeftDown = true,
                    LeftPressed = true
                });
                if (!moved.Succeeded ||
                    !moved.Completed ||
                    !string.Equals(moved.ResultCode, "moveApplied", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected second move click to persist the new origin.");
                }

                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-move", out saved), "load moved instance");
                var stored = FindPlacedInstance(saved, instance.InstanceId);
                if (stored.OriginTileX != 39 ||
                    stored.OriginTileY != 50 ||
                    stored.LayerOrder != 5 ||
                    stored.EraseMask.Count != 1 ||
                    stored.EraseMask[0].X != 0 ||
                    stored.EraseMask[0].Y != 0 ||
                    stored.Hidden ||
                    stored.TemplateSnapshot.Cells.Count != template.Cells.Count)
                {
                    throw new InvalidOperationException("Expected move to change only origin while preserving hidden, erase mask, layer order and template snapshot.");
                }

                BlueprintTemplateLibrarySnapshot templates;
                RequireBlueprintSuccess(templateStore.TryLoad(out templates), "load templates after move");
                if (templates.Templates.Count != 1 ||
                    templates.Templates[0].Cells[0].X != 0 ||
                    templates.Templates[0].Cells.Count != template.Cells.Count)
                {
                    throw new InvalidOperationException("Moving a placed instance must not mutate the blueprint library template.");
                }

                var afterProjection = BlueprintProjectionService.GetSnapshot();
                var afterMaterials = BlueprintMaterialService.GetSnapshot();
                if (afterProjection.ProjectedLayers.Count != 1 ||
                    afterProjection.ProjectedLayers[0].WorldTileX != 40 ||
                    afterProjection.ProjectedLayers[0].WorldTileY != 50 ||
                    afterMaterials.RequiredItemCount != beforeMaterials.RequiredItemCount ||
                    afterMaterials.RequiredStackTotal != beforeMaterials.RequiredStackTotal)
                {
                    throw new InvalidOperationException("Expected move to refresh projection coordinates while keeping material demand consistent.");
                }
            }
            finally
            {
                BlueprintPlacedInstanceTransformState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintPlacedInstanceMirrorUsesServiceAndFailsClosed()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placed-transform-mirror");
            try
            {
                BlueprintPlacedInstanceTransformState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintMirrorService.ResetForTesting();

                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord mirrorable;
                BlueprintTemplateRecord unsupported;
                BlueprintTemplateRecord progressTemplate;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateMirrorableBlueprintTemplate("实例镜像"), out mirrorable), "create mirrorable template");
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateDirectionalFurnitureBlueprintTemplate(), out unsupported), "create unsupported mirror template");
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateMirrorableBlueprintTemplate("进度镜像"), out progressTemplate), "create progress mirror template");

                BlueprintWorldInstanceRecord mirrorableInstance;
                BlueprintWorldInstanceRecord unsupportedInstance;
                BlueprintWorldInstanceRecord progressInstance;
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-mirror-instance", "world-mirror-instance", mirrorable, 20, 30, 0, out mirrorableInstance), "create mirrorable instance");
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-mirror-instance", "world-mirror-instance", unsupported, 50, 60, 1, out unsupportedInstance), "create unsupported instance");
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-mirror-instance", "world-mirror-instance", progressTemplate, 80, 90, 2, out progressInstance), "create progress instance");

                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-mirror-instance", out saved), "load progress instance");
                var edited = new List<BlueprintWorldInstanceRecord>();
                for (var index = 0; index < saved.Instances.Count; index++)
                {
                    var clone = saved.Instances[index].Clone();
                    if (string.Equals(clone.InstanceId, progressInstance.InstanceId, StringComparison.Ordinal))
                    {
                        clone.AutoPlacementProgressState = "started";
                    }

                    edited.Add(clone);
                }

                RequireBlueprintSuccess(instanceStore.SaveWorldInstances("pair-mirror-instance", "world-mirror-instance", edited, out saved), "save progress marker");

                var context = BlueprintPlacementWorldContext.Success("pair-mirror-instance", "world-mirror-instance");
                BlueprintPlacedInstanceTransformState.SetDependenciesForTesting(instanceStore, context);
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(instanceStore, context, true);

                var start = BlueprintPlacedInstanceTransformState.BeginMirror();
                if (!start.Succeeded || !string.Equals(start.ResultCode, "mirrorTargetSelectStarted", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected mirror command to enter target selection.");
                }

                var mirrored = BlueprintPlacedInstanceTransformState.HandlePointer(new BlueprintPlacedInstanceTransformPointerInput
                {
                    WorldTileHit = true,
                    TileX = 20,
                    TileY = 30,
                    LeftDown = true,
                    LeftPressed = true
                });
                if (!mirrored.Succeeded ||
                    !mirrored.Completed ||
                    !string.Equals(mirrored.ResultCode, "mirrorHorizontalApplied", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected placed-instance mirror to reuse the horizontal mirror service.");
                }

                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-mirror-instance", out saved), "load mirrored instance");
                var storedMirror = FindPlacedInstance(saved, mirrorableInstance.InstanceId);
                if (storedMirror.OriginTileX != 20 ||
                    storedMirror.OriginTileY != 30 ||
                    FindLayerAt(storedMirror.TemplateSnapshot, 3, 0, BlueprintLayerKinds.Tile) == null ||
                    FindLayerAt(storedMirror.TemplateSnapshot, 0, 0, BlueprintLayerKinds.Tile) != null)
                {
                    throw new InvalidOperationException("Expected mirror to update only the placed instance template snapshot and keep origin stable.");
                }

                BlueprintTemplateLibrarySnapshot templates;
                RequireBlueprintSuccess(templateStore.TryLoad(out templates), "load templates after placed mirror");
                if (FindLayerAt(templates.Templates[0], 0, 0, BlueprintLayerKinds.Tile) == null ||
                    FindLayerAt(templates.Templates[0], 3, 0, BlueprintLayerKinds.Tile) != null)
                {
                    throw new InvalidOperationException("Placed-instance mirror must not write the blueprint library template.");
                }

                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-mirror-instance", out saved), "load before blocked mirror");
                var unsupportedBefore = FindPlacedInstance(saved, unsupportedInstance.InstanceId).TemplateSnapshot.Clone();
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-mirror-instance", out saved), "load before progress mirror");
                var progressBefore = FindPlacedInstance(saved, progressInstance.InstanceId).TemplateSnapshot.Clone();

                BlueprintPlacedInstanceTransformState.BeginMirror();
                var blocked = BlueprintPlacedInstanceTransformState.HandlePointer(new BlueprintPlacedInstanceTransformPointerInput
                {
                    WorldTileHit = true,
                    TileX = 50,
                    TileY = 60,
                    LeftDown = true,
                    LeftPressed = true
                });
                if (blocked.Succeeded ||
                    blocked.Changed ||
                    !string.Equals(blocked.ResultCode, "mirrorBlocked", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected unsupported frame/direction content to fail closed when mirroring an instance.");
                }

                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-mirror-instance", out saved), "load after blocked mirror");
                var unsupportedAfter = FindPlacedInstance(saved, unsupportedInstance.InstanceId).TemplateSnapshot;
                if (FindLayerAt(unsupportedAfter, 0, 0, BlueprintLayerKinds.Object) == null ||
                    FindLayerAt(unsupportedAfter, 1, 0, BlueprintLayerKinds.Object) != null ||
                    unsupportedAfter.Cells.Count != unsupportedBefore.Cells.Count)
                {
                    throw new InvalidOperationException("Blocked placed-instance mirror must leave the unsupported instance snapshot unchanged.");
                }

                BlueprintPlacedInstanceTransformState.BeginMirror();
                var progressBlocked = BlueprintPlacedInstanceTransformState.HandlePointer(new BlueprintPlacedInstanceTransformPointerInput
                {
                    WorldTileHit = true,
                    TileX = 80,
                    TileY = 90,
                    LeftDown = true,
                    LeftPressed = true
                });
                if (progressBlocked.Succeeded ||
                    progressBlocked.Changed ||
                    !string.Equals(progressBlocked.ResultCode, "autoPlacementProgressActive", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected non-idle auto-placement progress marker to block placed-instance mirror.");
                }

                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-mirror-instance", out saved), "load after progress blocked mirror");
                var progressAfter = FindPlacedInstance(saved, progressInstance.InstanceId).TemplateSnapshot;
                if (FindLayerAt(progressAfter, 0, 0, BlueprintLayerKinds.Tile) == null ||
                    progressAfter.Cells.Count != progressBefore.Cells.Count)
                {
                    throw new InvalidOperationException("Auto-placement progress mirror block must leave the instance snapshot unchanged.");
                }
            }
            finally
            {
                BlueprintPlacedInstanceTransformState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintMirrorService.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintWorldInstancesPersistHiddenEraseLayerAndSnapshotsOnReload()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-world-instance-stage04-reload");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var template = CreateProjectionTileOnlyTemplate("04 持久化模板", 88);
                template.TemplateId = "stage04-template";
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    store.CreateInstanceFromTemplate("pair-stage04-reload", "world-stage04-reload", template, 12, 34, 3, out instance),
                    "create stage04 reload instance");

                BlueprintWorldInstanceSnapshot snapshot;
                RequireBlueprintSuccess(store.TryLoadWorld("pair-stage04-reload", out snapshot), "load stage04 instance for edit");
                var edited = snapshot.Instances[0].Clone();
                edited.Hidden = true;
                edited.LayerOrder = 7;
                edited.EraseMask.Add(new BlueprintEraseMaskCellRecord { X = 1, Y = 2 });
                edited.TemplateSnapshot.Name = "04 实例副本";
                edited.TemplateSnapshot.Cells[0].Layers[0].ContentId = 188;
                RequireBlueprintSuccess(
                    store.SaveWorldInstances("pair-stage04-reload", "world-stage04-reload", new List<BlueprintWorldInstanceRecord> { edited }, out snapshot),
                    "save stage04 edited instance");

                var reloadedStore = new BlueprintWorldInstanceStore(store.RootDirectory);
                BlueprintWorldInstanceSnapshot reloaded;
                RequireBlueprintSuccess(reloadedStore.TryLoadWorld("pair-stage04-reload", out reloaded), "reload stage04 edited instance");
                if (reloaded.Instances.Count != 1)
                {
                    throw new InvalidOperationException("Expected stage04 reload to keep one placed blueprint instance.");
                }

                var loaded = reloaded.Instances[0];
                if (!loaded.Hidden ||
                    loaded.LayerOrder != 7 ||
                    loaded.EraseMask.Count != 1 ||
                    loaded.EraseMask[0].X != 1 ||
                    loaded.EraseMask[0].Y != 2 ||
                    !string.Equals(loaded.TemplateSnapshot.Name, "04 实例副本", StringComparison.Ordinal) ||
                    loaded.TemplateSnapshot.Cells[0].Layers[0].ContentId != 188)
                {
                    throw new InvalidOperationException("Expected stage04 reload to preserve hidden state, erase mask, layer order, and edited instance template snapshot.");
                }

                if (template.Cells[0].Layers[0].ContentId != 88)
                {
                    throw new InvalidOperationException("Editing a placed instance snapshot must not mutate the source template object.");
                }
            }
            finally
            {
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void RunPlacedInstanceCommand(string action, string instanceId)
        {
            LegacyUiActionService.HandleBlueprintPlacedInstanceCommandForTesting(new LegacyUiCommand
            {
                ElementId = LegacyMainWindow.BuildBlueprintPlacedInstanceCommandIdForTesting(action, instanceId),
                Label = "蓝图实例:" + action,
                Kind = "button",
                MouseCaptured = true
            });
        }

        private static BlueprintWorldInstanceRecord FindPlacedInstance(BlueprintWorldInstanceSnapshot snapshot, string instanceId)
        {
            var instance = FindPlacedInstanceOrNull(snapshot, instanceId);
            if (instance == null)
            {
                throw new InvalidOperationException("Expected placed blueprint instance to exist: " + instanceId);
            }

            return instance;
        }

        private static BlueprintWorldInstanceRecord FindPlacedInstanceOrNull(BlueprintWorldInstanceSnapshot snapshot, string instanceId)
        {
            if (snapshot == null || snapshot.Instances == null)
            {
                return null;
            }

            for (var index = 0; index < snapshot.Instances.Count; index++)
            {
                var instance = snapshot.Instances[index];
                if (instance != null && string.Equals(instance.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return instance;
                }
            }

            return null;
        }
    }
}
