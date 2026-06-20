using System;
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
                BlueprintEntryState.ResetForTesting();

                RunPlacedInstanceCommand("select", third.InstanceId);
                var selected = BlueprintPlacedInstanceUiState.GetSnapshot();
                AssertStringEquals(selected.SelectedInstanceId, third.InstanceId, "placed blueprint select command");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault()).Mode, BlueprintEntryModes.PlacedManagement, "placed blueprint command opens management mode");

                RunPlacedInstanceCommand("toggle-hidden", second.InstanceId);
                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-c", out saved), "load after hidden toggle");
                if (!FindPlacedInstance(saved, second.InstanceId).Hidden)
                {
                    throw new InvalidOperationException("Expected placed blueprint hidden command to persist Hidden=true.");
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
