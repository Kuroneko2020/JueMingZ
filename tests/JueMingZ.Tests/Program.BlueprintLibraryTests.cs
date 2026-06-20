using System;
using System.IO;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Input;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintTemplateRenameUsesSuffixAndKeepsInstances()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-template-rename");
            try
            {
                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord castle;
                BlueprintTemplateRecord tower;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateSampleBlueprintTemplate("城堡"), out castle), "create castle blueprint");
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateSampleBlueprintTemplate("塔楼"), out tower), "create tower blueprint");

                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate("player-world", "world", tower, 10, 20, 1, out instance),
                    "create instance before rename");

                BlueprintTemplateRecord emptyRename;
                var empty = templateStore.RenameTemplate(tower.TemplateId, "   ", out emptyRename);
                if (empty.Succeeded || !string.Equals(empty.ResultCode, "invalidTemplateName", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected empty blueprint rename to fail closed.");
                }

                BlueprintTemplateRecord renamed;
                RequireBlueprintSuccess(templateStore.RenameTemplate(tower.TemplateId, "城堡", out renamed), "rename tower blueprint");
                AssertStringEquals(renamed.Name, "城堡 2", "renamed blueprint unique suffix");

                BlueprintWorldInstanceSnapshot instances;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("player-world", out instances), "load instances after template rename");
                if (instances.Instances.Count != 1 ||
                    !string.Equals(instances.Instances[0].TemplateSnapshot.Name, "塔楼", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Renaming a template must not rewrite placed instance snapshots.");
                }
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintLibraryUiStateEmptyAndPagingContracts()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-ui-state");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                var empty = BlueprintLibraryUiState.GetSnapshot();
                if (!empty.LoadSucceeded || empty.Templates.Count != 0 || empty.PageCount != 0 || empty.VisibleCount != 0)
                {
                    throw new InvalidOperationException("Expected empty blueprint library snapshot to stay readable and unpaged.");
                }

                for (var index = 0; index < BlueprintLibraryUiState.PageSize + 1; index++)
                {
                    BlueprintTemplateRecord template;
                    RequireBlueprintSuccess(
                        store.CreateTemplate(CreateSampleBlueprintTemplate("分页 " + index.ToString(System.Globalization.CultureInfo.InvariantCulture)), out template),
                        "create paged blueprint template");
                }

                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                var firstPage = BlueprintLibraryUiState.GetSnapshot();
                if (firstPage.PageCount != 2 || firstPage.VisibleCount != BlueprintLibraryUiState.PageSize || firstPage.VisibleStartIndex != 0)
                {
                    throw new InvalidOperationException("Expected blueprint library to expose a stable six-item page.");
                }

                BlueprintLibraryUiState.MovePage(1);
                var secondPage = BlueprintLibraryUiState.GetSnapshot();
                if (secondPage.PageIndex != 1 || secondPage.VisibleStartIndex != BlueprintLibraryUiState.PageSize || secondPage.VisibleCount != 1)
                {
                    throw new InvalidOperationException("Expected blueprint library next-page command to show the remaining template.");
                }

                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "main-menu-open-row");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "state-paged-6");
                AssertStringEquals(
                    LegacyMainWindow.GetBlueprintLibraryOpenElementIdForTesting(),
                    "blueprint-entry:open-library",
                    "blueprint library open row command id");
                if (LegacyMainWindow.GetBlueprintMenuOpenRowCountForTesting() != 2)
                {
                    throw new InvalidOperationException("Expected blueprint page to expose exactly two deferred submenu open rows.");
                }
            }
            finally
            {
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintLibraryCommandsRenameDeleteUseAndExport()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-commands");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(store.CreateTemplate(CreateSampleBlueprintTemplate("旧名"), out template), "create command blueprint");
                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                BlueprintEntryState.ResetForTesting();

                var inputId = LegacyMainWindow.BuildBlueprintLibraryNameInputIdForTesting(template.TemplateId);
                LegacyTextInput.Focus(inputId, "新名");
                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.BuildBlueprintLibraryCommandIdForTesting(BlueprintLibraryUiState.ConfirmNameAction, template.TemplateId),
                    Label = "蓝图库:保存",
                    Kind = "button",
                    MouseCaptured = true
                });

                BlueprintTemplateLibrarySnapshot templates;
                RequireBlueprintSuccess(store.TryLoad(out templates), "load renamed blueprint");
                AssertStringEquals(templates.Templates[0].Name, "新名", "blueprint renamed through UI command");

                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.BuildBlueprintLibraryCommandIdForTesting("use", template.TemplateId),
                    Label = "蓝图库:使用",
                    Kind = "button",
                    MouseCaptured = true
                });
                var entry = BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault());
                AssertStringEquals(entry.Mode, BlueprintEntryModes.PlacementPreview, "blueprint use switches entry preview mode");
                AssertStringEquals(entry.SelectedTemplateId, template.TemplateId, "blueprint use selected template id");

                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.BuildBlueprintLibraryCommandIdForTesting("export", template.TemplateId),
                    Label = "蓝图库:导出",
                    Kind = "button",
                    MouseCaptured = true
                });
                var exported = BlueprintLibraryUiState.GetSnapshot().LastExportPath;
                if (string.IsNullOrWhiteSpace(exported) || !File.Exists(exported))
                {
                    throw new InvalidOperationException("Expected blueprint export command to write a template export file.");
                }

                var exportText = File.ReadAllText(exported);
                AssertContains(exportText, "JueMingZ.Blueprint.Template");
                if (exportText.Contains("Instances"))
                {
                    throw new InvalidOperationException("Blueprint template export command must not include world instances.");
                }

                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.BuildBlueprintLibraryCommandIdForTesting("delete", template.TemplateId),
                    Label = "蓝图库:删除",
                    Kind = "button",
                    MouseCaptured = true
                });
                if (!string.Equals(BlueprintLibraryUiState.GetSnapshot().DeleteConfirmTemplateId, template.TemplateId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected first blueprint delete click to arm confirmation only.");
                }

                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.BuildBlueprintLibraryCommandIdForTesting("delete", template.TemplateId),
                    Label = "蓝图库:确认删除",
                    Kind = "button",
                    MouseCaptured = true
                });
                RequireBlueprintSuccess(store.TryLoad(out templates), "load after delete");
                if (templates.Templates.Count != 0 || !string.IsNullOrWhiteSpace(BlueprintLibraryUiState.GetSnapshot().DeleteConfirmTemplateId))
                {
                    throw new InvalidOperationException("Expected confirmed blueprint delete to remove template and clear confirmation.");
                }
            }
            finally
            {
                LegacyTextInput.ClearFocus();
                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

    }
}
