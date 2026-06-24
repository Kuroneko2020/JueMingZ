using System;
using System.Collections.Generic;
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

                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "main-menu-hotkey-open-row");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "same-content-submenu");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "state-paged-6");
                AssertStringEquals(
                    LegacyMainWindow.GetBlueprintLibraryOpenElementIdForTesting(),
                    "blueprint-action-entry:open-library",
                    "blueprint library open row command id");
                if (LegacyMainWindow.GetBlueprintMenuOpenRowCountForTesting() != 1)
                {
                    throw new InvalidOperationException("Expected blueprint page to keep only the placed-blueprint deferred open row after the library row becomes a shortcut row.");
                }
            }
            finally
            {
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintLibraryTwoColumnCardsPreviewAndLayoutButtons()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-two-column-cards");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord wide;
                BlueprintTemplateRecord tall;
                RequireBlueprintSuccess(store.CreateTemplate(CreatePreviewTemplate("宽蓝图", 80, 4), out wide), "create wide blueprint");
                RequireBlueprintSuccess(store.CreateTemplate(CreatePreviewTemplate("高蓝图", 4, 80), out tall), "create tall blueprint");
                for (var index = 0; index < 4; index++)
                {
                    BlueprintTemplateRecord ignored;
                    RequireBlueprintSuccess(store.CreateTemplate(CreatePreviewTemplate("补位 " + index.ToString(System.Globalization.CultureInfo.InvariantCulture), 6, 6), out ignored), "create filler blueprint");
                }

                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                var fakeDialog = new FakeBlueprintFileDialogService
                {
                    ExportPath = Path.Combine(BlueprintStoragePaths.BuildDefaultExportDirectory(store.RootDirectory), "stage02-two-column-export.json")
                };
                BlueprintLibraryUiState.SetFileDialogServiceForTesting(fakeDialog);
                var opened = BlueprintLibraryUiState.OpenLibrary();
                if (!opened.Succeeded || !BlueprintLibraryUiState.IsOpen)
                {
                    throw new InvalidOperationException("Expected blueprint library to open before validating the stage 06 layout.");
                }

                var snapshot = BlueprintLibraryUiState.GetSnapshot();
                if (snapshot.VisibleCount != BlueprintLibraryUiState.PageSize)
                {
                    throw new InvalidOperationException("Expected stage 06 card layout to keep the existing six-template page.");
                }

                if (LegacyMainWindow.GetBlueprintLibraryCardColumnsForTesting() != 2)
                {
                    throw new InvalidOperationException("Expected blueprint library cards to use exactly two columns.");
                }

                var viewport = new LegacyUiRect(12, 34, 456, 278);
                var first = LegacyMainWindow.CalculateBlueprintLibraryCardRectForTesting(viewport, 120, 0);
                var second = LegacyMainWindow.CalculateBlueprintLibraryCardRectForTesting(viewport, 120, 1);
                var third = LegacyMainWindow.CalculateBlueprintLibraryCardRectForTesting(viewport, 120, 2);
                if (first.Y != second.Y ||
                    second.X <= first.X ||
                    first.Width != second.Width ||
                    third.X != first.X ||
                    third.Y != first.Y + LegacyMainWindow.GetBlueprintLibraryCardHeightForTesting() + LegacyMainWindow.GetBlueprintLibraryCardGapForTesting())
                {
                    throw new InvalidOperationException("Expected blueprint library cards to flow left-to-right in fixed two-column rows.");
                }

                var expectedHeight = LegacyMainWindow.GetBlueprintLibraryCardHeightForTesting() * 3 +
                                     LegacyMainWindow.GetBlueprintLibraryCardGapForTesting() * 2;
                var actualHeight = LegacyMainWindow.CalculateBlueprintLibraryListHeightForTesting(true, 6, 6);
                if (actualHeight != expectedHeight)
                {
                    throw new InvalidOperationException("Expected blueprint library list height to be fixed by card rows, not by dynamic content.");
                }

                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage06-two-column-fixed-cards");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "preview-scales-to-fit");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage07-name-edit-delete-confirm");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage08-import-export-windows-dialog");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage09-layout-use-real-template-snapshot");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage02-title-row-tools");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "card-material-toggle");
                AssertContains(
                    LegacyMainWindow.BuildBlueprintLibraryLayoutCommandIdForTesting("use", wide.TemplateId),
                    "blueprint-library:layout-use:");
                AssertContains(
                    LegacyMainWindow.BuildBlueprintLibraryLayoutCommandIdForTesting("materials", wide.TemplateId),
                    "blueprint-library:layout-materials:");

                var toolbar = new LegacyUiRect(12, 34, 456, 42);
                var importRect = LegacyMainWindow.CalculateBlueprintLibraryImportButtonRectForTesting(toolbar);
                var prevRect = LegacyMainWindow.CalculateBlueprintLibraryPreviousButtonRectForTesting(toolbar);
                var nextRect = LegacyMainWindow.CalculateBlueprintLibraryNextButtonRectForTesting(toolbar);
                if (importRect.Width >= toolbar.Width / 3 ||
                    importRect.Height != 24 ||
                    importRect.X <= toolbar.X ||
                    prevRect.X <= importRect.X ||
                    nextRect.X <= prevRect.X ||
                    !string.Equals(LegacyMainWindow.GetBlueprintLibraryImportElementIdForTesting(), "blueprint-library:import", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage 02 to expose compact title-row import and page buttons.");
                }

                if (LegacyMainWindow.GetBlueprintLibraryHeaderToolTooltipLinesForTesting("import") != null ||
                    LegacyMainWindow.GetBlueprintLibraryHeaderToolTooltipLinesForTesting("page-prev") != null ||
                    LegacyMainWindow.GetBlueprintLibraryHeaderToolTooltipLinesForTesting("page-next") != null)
                {
                    throw new InvalidOperationException("Stage 02 title-row import and page tools should not use hover instructions.");
                }

                int originX;
                int originY;
                int drawWidth;
                int drawHeight;
                double scale;
                var preview = new LegacyUiRect(0, 0, 120, 60);
                if (!LegacyMainWindow.TryResolveBlueprintTemplatePreviewLayoutForTesting(wide, preview, out originX, out originY, out drawWidth, out drawHeight, out scale) ||
                    drawWidth > preview.Width - 10 ||
                    drawHeight > preview.Height - 10 ||
                    scale <= 0d)
                {
                    throw new InvalidOperationException("Expected wide blueprint preview to scale fully inside the fixed preview bounds.");
                }

                if (!LegacyMainWindow.TryResolveBlueprintTemplatePreviewLayoutForTesting(tall, preview, out originX, out originY, out drawWidth, out drawHeight, out scale) ||
                    drawWidth > preview.Width - 10 ||
                    drawHeight > preview.Height - 10 ||
                    scale <= 0d)
                {
                    throw new InvalidOperationException("Expected tall blueprint preview to scale fully inside the fixed preview bounds.");
                }

                if (LegacyMainWindow.TryResolveBlueprintTemplatePreviewLayoutForTesting(new BlueprintTemplateRecord(), preview, out originX, out originY, out drawWidth, out drawHeight, out scale))
                {
                    throw new InvalidOperationException("Empty blueprint previews must fail closed instead of inventing shape data.");
                }

                var beforeProjectionSignature = BlueprintProjectionService.BuildStateSignature();
                var beforeMaterialSignature = BlueprintMaterialService.BuildStateSignature();
                var materialsShell = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-materials", wide.TemplateId);
                if (materialsShell == null ||
                    materialsShell.PlaceholderOnly ||
                    !materialsShell.Succeeded ||
                    !string.Equals(materialsShell.ResultCode, "materialsExpanded", StringComparison.Ordinal) ||
                    !string.Equals(BlueprintLibraryUiState.GetSnapshot().ExpandedMaterialTemplateId, wide.TemplateId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage 02 materials button to open the selected template material modal.");
                }

                LegacyMainUiState.SetVisible(true);
                var useShell = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-use", wide.TemplateId);
                var exportShell = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-export", wide.TemplateId);
                var nameShell = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-name", wide.TemplateId);
                var deleteShell = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-delete", wide.TemplateId);
                if (useShell == null || exportShell == null || deleteShell == null || nameShell == null ||
                    useShell.PlaceholderOnly ||
                    !useShell.Succeeded ||
                    !string.Equals(useShell.ResultCode, "previewStarted", StringComparison.Ordinal) ||
                    exportShell.PlaceholderOnly ||
                    !exportShell.Succeeded ||
                    deleteShell.PlaceholderOnly ||
                    nameShell.PlaceholderOnly ||
                    !string.Equals(nameShell.ResultCode, "needsDoubleClick", StringComparison.Ordinal) ||
                    !string.Equals(deleteShell.ResultCode, "deleteConfirmArmed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage 09 to make layout-use real while layout-export, name, and delete stay real UI commands.");
                }

                if (BlueprintLibraryUiState.IsOpen ||
                    !string.IsNullOrWhiteSpace(BlueprintLibraryUiState.GetSnapshot().ExpandedMaterialTemplateId) ||
                    LegacyMainUiState.Visible)
                {
                    throw new InvalidOperationException("Expected layout-use to close the blueprint library submenu, material modal, and F5 main menu.");
                }

                var previewAfterUse = BlueprintPlacementPreviewState.GetSnapshot();
                if (!previewAfterUse.Active ||
                    !string.Equals(previewAfterUse.TemplateId, wide.TemplateId, StringComparison.Ordinal) ||
                    previewAfterUse.TemplateSnapshot == null ||
                    previewAfterUse.TemplateSnapshot.Cells.Count <= 0)
                {
                    throw new InvalidOperationException("Expected layout-use to enter placement preview with a template snapshot.");
                }

                if (BlueprintProjectionService.BuildStateSignature() != beforeProjectionSignature ||
                    BlueprintMaterialService.BuildStateSignature() != beforeMaterialSignature)
                {
                    throw new InvalidOperationException("layout-use must not refresh projection or material caches while entering placement preview.");
                }

                var afterEntry = BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault());
                BlueprintTemplateLibrarySnapshot afterTemplates;
                RequireBlueprintSuccess(store.TryLoad(out afterTemplates), "load templates after layout actions");
                if (!string.Equals(afterEntry.Mode, BlueprintEntryModes.PlacementPreview, StringComparison.Ordinal) ||
                    afterTemplates.Templates.Count != 6 ||
                    !string.Equals(BlueprintLibraryUiState.GetSnapshot().DeleteConfirmTemplateId, wide.TemplateId, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(BlueprintLibraryUiState.GetSnapshot().LastExportPath) ||
                    !File.Exists(BlueprintLibraryUiState.GetSnapshot().LastExportPath))
                {
                    throw new InvalidOperationException("Stage 09 layout-use must start preview while layout export writes a template export and delete only arms confirmation.");
                }
            }
            finally
            {
                BlueprintEntryState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                LegacyMainUiState.SetVisible(false);
                restore();
            }
        }

        private static void BlueprintLibraryStage07NamingRenameDeleteConfirmKeepsInstances()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-stage07-naming-delete");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord firstDefault;
                BlueprintTemplateRecord secondDefault;
                BlueprintTemplateRecord thirdDefault;
                RequireBlueprintSuccess(store.CreateDefaultTemplate(3, 2, out firstDefault), "create first default blueprint");
                RequireBlueprintSuccess(store.CreateDefaultTemplate(3, 2, out secondDefault), "create second default blueprint");
                RequireBlueprintSuccess(store.CreateDefaultTemplate(3, 2, out thirdDefault), "create third default blueprint");
                AssertStringEquals(firstDefault.Name, BlueprintStorageConstants.DefaultTemplateName, "stage07 first default name");
                AssertStringEquals(secondDefault.Name, BlueprintStorageConstants.DefaultTemplateName + " 2", "stage07 second default name");
                AssertStringEquals(thirdDefault.Name, BlueprintStorageConstants.DefaultTemplateName + " 3", "stage07 third default name");

                BlueprintTemplateRecord castle;
                BlueprintTemplateRecord tower;
                RequireBlueprintSuccess(store.CreateTemplate(CreateSampleBlueprintTemplate("城堡"), out castle), "create existing castle blueprint");
                RequireBlueprintSuccess(store.CreateTemplate(CreateSampleBlueprintTemplate("塔楼"), out tower), "create tower blueprint for rename/delete");
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate("stage07-player-world", "stage07-world", tower, 30, 40, 2, out instance),
                    "create instance before stage07 rename/delete");

                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                RequireBlueprintCommandSuccess(BlueprintLibraryUiState.OpenLibrary(), "open stage07 blueprint library");

                var inputId = LegacyMainWindow.BuildBlueprintLibraryNameInputIdForTesting(tower.TemplateId);
                var singleName = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-name", tower.TemplateId);
                if (singleName == null ||
                    singleName.PlaceholderOnly ||
                    !string.Equals(singleName.ResultCode, "needsDoubleClick", StringComparison.Ordinal) ||
                    LegacyTextInput.IsFocused(inputId))
                {
                    throw new InvalidOperationException("Expected blueprint template names to require a double-click before editing.");
                }

                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.BuildBlueprintLibraryCommandIdForTesting("name", tower.TemplateId),
                    Label = "蓝图库:名称",
                    Kind = "button",
                    MouseCaptured = true,
                    IsDoubleClick = true,
                    Rect = new LegacyUiRect(0, 0, 120, 22)
                });
                if (!LegacyTextInput.IsFocused(inputId))
                {
                    throw new InvalidOperationException("Expected double-clicking the blueprint template name to enter edit mode.");
                }

                LegacyTextInput.Focus(inputId, "临时名称");
                LegacyTextInput.ClearFocus();
                BlueprintTemplateLibrarySnapshot templates;
                RequireBlueprintSuccess(store.TryLoad(out templates), "load templates after cancelled rename");
                AssertStringEquals(FindTemplateName(templates, tower.TemplateId), "塔楼", "cancelled blueprint rename leaves template name unchanged");

                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.BuildBlueprintLibraryCommandIdForTesting("name", tower.TemplateId),
                    Label = "蓝图库:名称",
                    Kind = "button",
                    MouseCaptured = true,
                    IsDoubleClick = true,
                    Rect = new LegacyUiRect(0, 0, 120, 22)
                });
                LegacyTextInput.Focus(inputId, "城堡");
                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.BuildBlueprintLibraryCommandIdForTesting(BlueprintLibraryUiState.ConfirmNameAction, tower.TemplateId),
                    Label = "蓝图库:保存名称",
                    Kind = "button",
                    MouseCaptured = true
                });
                RequireBlueprintSuccess(store.TryLoad(out templates), "load templates after stage07 rename");
                AssertStringEquals(FindTemplateName(templates, tower.TemplateId), "城堡 2", "stage07 rename uses store suffix rule");
                if (LegacyTextInput.IsFocused(inputId))
                {
                    throw new InvalidOperationException("Expected successful blueprint rename to clear the inline name editor.");
                }

                var beforeDeleteCount = templates.Templates.Count;
                var deleteArm = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-delete", tower.TemplateId);
                if (deleteArm == null ||
                    deleteArm.PlaceholderOnly ||
                    !string.Equals(deleteArm.ResultCode, "deleteConfirmArmed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected first blueprint card delete click to arm confirmation only.");
                }

                RequireBlueprintSuccess(store.TryLoad(out templates), "load templates after first delete click");
                if (templates.Templates.Count != beforeDeleteCount ||
                    !string.Equals(BlueprintLibraryUiState.GetSnapshot().DeleteConfirmTemplateId, tower.TemplateId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected first blueprint card delete click to keep the template and remember the confirmation target.");
                }

                var deleteConfirm = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-delete", tower.TemplateId);
                if (deleteConfirm == null || !deleteConfirm.Succeeded || deleteConfirm.PlaceholderOnly)
                {
                    throw new InvalidOperationException("Expected second blueprint card delete click to delete the template.");
                }

                RequireBlueprintSuccess(store.TryLoad(out templates), "load templates after confirmed delete");
                if (!string.IsNullOrWhiteSpace(FindTemplateName(templates, tower.TemplateId)) ||
                    !string.IsNullOrWhiteSpace(BlueprintLibraryUiState.GetSnapshot().DeleteConfirmTemplateId))
                {
                    throw new InvalidOperationException("Expected confirmed blueprint delete to remove only the template and clear confirmation.");
                }

                BlueprintWorldInstanceSnapshot instances;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("stage07-player-world", out instances), "load instances after stage07 rename/delete");
                if (instances.Instances.Count != 1)
                {
                    throw new InvalidOperationException("Expected template delete to leave placed blueprint instances intact.");
                }

                AssertStringEquals(instances.Instances[0].Name, "塔楼", "stage07 instance name snapshot");
                AssertStringEquals(instances.Instances[0].TemplateSnapshot.Name, "塔楼", "stage07 instance template snapshot name");
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

        private static void BlueprintLibraryStage08ImportExportDiagnostics()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-stage08-import-export");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord source;
                RequireBlueprintSuccess(store.CreateTemplate(CreateSampleBlueprintTemplate("Stage08 Import"), out source), "create stage08 source blueprint");

                string exportPath;
                RequireBlueprintSuccess(store.ExportTemplate(source.TemplateId, string.Empty, out exportPath), "export stage08 source blueprint");
                var importDirectory = BlueprintStoragePaths.BuildDefaultImportDirectory(store.RootDirectory);
                Directory.CreateDirectory(importDirectory);
                var importPath = Path.Combine(importDirectory, "stage08-import.json");
                File.Copy(exportPath, importPath, true);

                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                var fakeDialog = new FakeBlueprintFileDialogService
                {
                    ImportPath = importPath
                };
                BlueprintLibraryUiState.SetFileDialogServiceForTesting(fakeDialog);
                RequireBlueprintCommandSuccess(BlueprintLibraryUiState.OpenLibrary(), "open stage08 blueprint library");

                var imported = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("import", string.Empty);
                if (imported == null ||
                    !imported.Succeeded ||
                    imported.PlaceholderOnly ||
                    !string.Equals(imported.ResultCode, "imported", StringComparison.Ordinal) ||
                    !string.Equals(imported.ImportPath, importPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(imported.TemplateId, source.TemplateId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Expected blueprint library import to read the single imports JSON, create a fresh template, and return diagnostic metadata.");
                }

                BlueprintTemplateLibrarySnapshot templates;
                RequireBlueprintSuccess(store.TryLoad(out templates), "load templates after stage08 import");
                if (templates.Templates.Count != 2 ||
                    !string.Equals(FindTemplateName(templates, imported.TemplateId), "Stage08 Import 2", StringComparison.Ordinal) ||
                    !string.Equals(BlueprintLibraryUiState.GetSnapshot().LastImportPath, importPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Expected stage08 import to suffix duplicate names without overwriting the original template.");
                }

                var expectedFailedExportPath = Path.Combine(BlueprintStoragePaths.BuildDefaultExportDirectory(store.RootDirectory), "stage08-write-failed.json");
                fakeDialog.ExportPath = expectedFailedExportPath;
                BlueprintTemplateLibraryStore.SetCommitFailurePredicateForTesting(
                    path => string.Equals(path, expectedFailedExportPath, StringComparison.OrdinalIgnoreCase));
                var failedExport = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-export", source.TemplateId);
                if (failedExport == null ||
                    failedExport.Succeeded ||
                    failedExport.PlaceholderOnly ||
                    !string.Equals(failedExport.ResultCode, "writeFailed", StringComparison.Ordinal) ||
                    !string.IsNullOrWhiteSpace(BlueprintLibraryUiState.GetSnapshot().LastExportPath))
                {
                    throw new InvalidOperationException("Expected stage08 export failure to stay real, fail closed, and clear stale export path diagnostics.");
                }

                RequireBlueprintSuccess(store.TryLoad(out templates), "load templates after failed stage08 export");
                if (templates.Templates.Count != 2)
                {
                    throw new InvalidOperationException("Failed stage08 export must not alter the template library.");
                }
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                BlueprintLibraryUiState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintLibraryStage02FileDialogAndMaterialContracts()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-stage02-file-dialog-materials");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(store.CreateTemplate(CreateSampleBlueprintTemplate("Stage02 Dialog"), out template), "create stage02 dialog blueprint");

                var fakeDialog = new FakeBlueprintFileDialogService
                {
                    CancelImport = true,
                    ExportPath = Path.Combine(BlueprintStoragePaths.BuildDefaultExportDirectory(store.RootDirectory), "stage02-export.json")
                };
                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                BlueprintLibraryUiState.SetFileDialogServiceForTesting(fakeDialog);
                RequireBlueprintCommandSuccess(BlueprintLibraryUiState.OpenLibrary(), "open stage02 blueprint library");

                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage02-title-row-tools");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "card-material-toggle");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "card-material-modal");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "card-buttons-no-tooltips");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "summary-placed-count");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "summary-only");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "no-empty-gap-text");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "larger-card-summary");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "use-closes-f5");
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "mutual-submenus");
                AssertContains(LegacyMainWindow.GetBlueprintPlacedInstanceVisualContractForTesting(), "same-content-submenu");
                AssertStringEquals(
                    LegacyMainWindow.BuildBlueprintLibraryHeaderSummaryForTesting(3, 2),
                    "共保存3个蓝图，已放置2个蓝图",
                    "stage02 library summary only keeps saved and placed counts");
                var noGapTemplate = CreateSampleBlueprintTemplate("Stage02 No Gap");
                noGapTemplate.MissingCapabilityFlags.Clear();
                AssertStringEquals(
                    LegacyMainWindow.BuildBlueprintCapabilityTextForTesting(noGapTemplate),
                    string.Empty,
                    "stage02 library cards hide the old empty capability gap text");
                AssertContains(
                    LegacyMainWindow.BuildBlueprintCapabilityTextForTesting(template),
                    "liquid-not-supported");
                if (LegacyMainWindow.GetBlueprintLibraryCardSummaryTextScaleForTesting() <= 0.52f)
                {
                    throw new InvalidOperationException("Stage 02 blueprint library card summary text scale should be larger than the old compact baseline.");
                }

                AssertStringEquals(
                    BlueprintLibraryUiState.BuildDefaultExportFileNameForTesting(new DateTime(2026, 6, 23, 15, 4, 0)),
                    "JM-2606230000.json",
                    "stage02 default export file name");

                var header = new LegacyUiRect(12, 34, 456, 42);
                var importRect = LegacyMainWindow.CalculateBlueprintLibraryImportButtonRectForTesting(header);
                var prevRect = LegacyMainWindow.CalculateBlueprintLibraryPreviousButtonRectForTesting(header);
                var nextRect = LegacyMainWindow.CalculateBlueprintLibraryNextButtonRectForTesting(header);
                if (importRect.X <= header.X ||
                    importRect.Right >= prevRect.X ||
                    prevRect.Right >= nextRect.X ||
                    importRect.Height != 24)
                {
                    throw new InvalidOperationException("Stage 02 title-row tools must fit without falling back to the old long import button.");
                }

                var materialText = LegacyMainWindow.BuildBlueprintTemplateMaterialListTextForTesting(template, 4);
                AssertContains(materialText, "木材 x4");
                var materialLines = BlueprintLibraryUiState.BuildTemplateMaterialLines(template);
                if (materialLines.Count != 1 ||
                    !string.Equals(materialLines[0], "木材 x4", StringComparison.Ordinal) ||
                    materialLines[0].Contains("已有"))
                {
                    throw new InvalidOperationException("Stage 02 material modal must list template material requirements without player-owned counts.");
                }

                var projectionSignature = BlueprintProjectionService.BuildStateSignature();
                var materialSignature = BlueprintMaterialService.BuildStateSignature();
                var expanded = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-materials", template.TemplateId);
                if (expanded == null ||
                    !expanded.Succeeded ||
                    !string.Equals(expanded.ResultCode, "materialsExpanded", StringComparison.Ordinal) ||
                    !string.Equals(BlueprintLibraryUiState.GetSnapshot().ExpandedMaterialTemplateId, template.TemplateId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Stage 02 material list button must open template-local modal state only.");
                }

                var coordinator = LegacyUiOverlayCoordinator.Current;
                coordinator.ResetForTesting();
                var viewport = new LegacyUiRect(20, 20, 460, 260);
                var area = LegacyScrollArea.Create(viewport, 620, 0);
                var elements = new List<LegacyUiElement>
                {
                    CreateLegacyUiElementForTesting("blueprint-entry:open-placed", "Lower", "button", viewport)
                };
                var mouse = new LegacyMouseSnapshot
                {
                    ReadAvailable = true,
                    LeftPressed = true
                };

                coordinator.BeginFrame("blueprint");
                if (!LegacyMainWindow.RegisterBlueprintLibraryMaterialModalOverlayForTesting(area))
                {
                    throw new InvalidOperationException("Expected blueprint library material list to register as a modal overlay.");
                }

                coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 560, 340), "blueprint", AppSettings.CreateDefault(), elements);
                var modal = FindLegacyUiElementForTesting(elements, LegacyMainWindow.GetBlueprintLibraryMaterialModalElementIdForTesting());
                mouse.X = modal.Rect.X + 12;
                mouse.Y = modal.Rect.Y + 40;
                var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
                bool blocked;
                var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
                if (hovered == null || string.Equals(hovered.Id, "blueprint-entry:open-placed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected blueprint library material modal to own hover above lower blueprint rows.");
                }

                if (!blocked || clickId.Length != 0)
                {
                    throw new InvalidOperationException("Expected blueprint library material modal blocker to stop lower blueprint row clicks.");
                }

                var close = FindLegacyUiElementForTesting(elements, LegacyMainWindow.BuildBlueprintLibraryMaterialCloseCommandIdForTesting(template.TemplateId));
                mouse.X = close.Rect.X + Math.Max(1, close.Rect.Width / 2);
                mouse.Y = close.Rect.Y + Math.Max(1, close.Rect.Height / 2);
                hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
                clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
                coordinator.EndFrame();
                if (hovered == null || hovered.Id != close.Id || blocked || clickId != close.Id)
                {
                    throw new InvalidOperationException("Expected blueprint library material modal close button to remain clickable above the modal blocker.");
                }

                var collapsed = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("materials-close", template.TemplateId);
                if (collapsed == null ||
                    !collapsed.Succeeded ||
                    !collapsed.Changed ||
                    !string.Equals(collapsed.ResultCode, "materialsClosed", StringComparison.Ordinal) ||
                    !string.IsNullOrWhiteSpace(BlueprintLibraryUiState.GetSnapshot().ExpandedMaterialTemplateId))
                {
                    throw new InvalidOperationException("Stage 02 material modal close command must collapse only the selected template material state.");
                }

                if (BlueprintProjectionService.BuildStateSignature() != projectionSignature ||
                    BlueprintMaterialService.BuildStateSignature() != materialSignature)
                {
                    throw new InvalidOperationException("Stage 02 material list toggle must not refresh projection or runtime material caches.");
                }

                var cancelledImport = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("import", string.Empty);
                if (cancelledImport == null ||
                    !cancelledImport.Succeeded ||
                    cancelledImport.Changed ||
                    !string.Equals(cancelledImport.Outcome, "NotApplicable", StringComparison.Ordinal) ||
                    !string.Equals(cancelledImport.ResultCode, "dialogCancelled", StringComparison.Ordinal) ||
                    !string.IsNullOrWhiteSpace(BlueprintLibraryUiState.GetSnapshot().LastImportPath) ||
                    !string.Equals(fakeDialog.LastImportInitialDirectory, BlueprintStoragePaths.BuildDefaultImportDirectory(store.RootDirectory), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Stage 02 import cancel must be a no-op and expose the default import directory to the dialog wrapper.");
                }

                var exported = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-export", template.TemplateId);
                if (exported == null ||
                    !exported.Succeeded ||
                    !File.Exists(fakeDialog.ExportPath) ||
                    !string.Equals(BlueprintLibraryUiState.GetSnapshot().LastExportPath, fakeDialog.ExportPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(fakeDialog.LastExportInitialDirectory, BlueprintStoragePaths.BuildDefaultExportDirectory(store.RootDirectory), StringComparison.OrdinalIgnoreCase) ||
                    !fakeDialog.LastExportDefaultFileName.StartsWith("JM-", StringComparison.Ordinal) ||
                    !fakeDialog.LastExportDefaultFileName.EndsWith("0000.json", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Stage 02 export must route through the dialog wrapper with a stable export directory and JM date file name.");
                }

                fakeDialog.FailExport = true;
                var failedExport = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-export", template.TemplateId);
                if (failedExport == null ||
                    failedExport.Succeeded ||
                    !string.Equals(failedExport.ResultCode, "dialogFailed", StringComparison.Ordinal) ||
                    !string.IsNullOrWhiteSpace(BlueprintLibraryUiState.GetSnapshot().LastExportPath))
                {
                    throw new InvalidOperationException("Stage 02 export dialog failure must fail closed and clear stale export diagnostics.");
                }
            }
            finally
            {
                LegacyUiOverlayCoordinator.Current.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintLibraryStage02MutualSubmenusAndUseCloseF5()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-stage02-mutual-submenus");
            try
            {
                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateSampleBlueprintTemplate("Stage02 Mutual"), out template), "create stage02 mutual blueprint");
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate("stage02-pair", "stage02-world", template, 12, 18, 1, out instance),
                    "create stage02 placed instance");

                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.SetStoreForTesting(templateStore, true);
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("stage02-pair", "stage02-world"),
                    true);
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    templateStore,
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("stage02-pair", "stage02-world"));

                RequireBlueprintCommandSuccess(BlueprintLibraryUiState.OpenLibrary(), "open stage02 mutual library");
                LegacyMainUiState.SetVisible(true);
                LegacyUiActionService.HandleBlueprintEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintPlacedOpenElementIdForTesting(),
                    Label = "已放置蓝图列表",
                    Kind = "button",
                    MouseCaptured = true
                });
                if (BlueprintLibraryUiState.IsOpen ||
                    !string.Equals(BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault()).Mode, BlueprintEntryModes.PlacedManagement, StringComparison.Ordinal) ||
                    !LegacyMainUiState.Visible)
                {
                    throw new InvalidOperationException("Opening placed blueprints must close the library submenu and show the placed submenu.");
                }

                LegacyUiActionService.HandleBlueprintActionEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintLibraryActionElementIdForTesting(),
                    Label = "蓝图库:打开",
                    Kind = "button",
                    MouseCaptured = true
                });
                if (!BlueprintLibraryUiState.IsOpen ||
                    string.Equals(BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault()).Mode, BlueprintEntryModes.PlacedManagement, StringComparison.Ordinal) ||
                    !LegacyMainUiState.Visible)
                {
                    throw new InvalidOperationException("Opening the blueprint library must close the placed submenu state.");
                }

                var use = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-use", template.TemplateId);
                if (use == null ||
                    !use.Succeeded ||
                    !string.Equals(use.ResultCode, "previewStarted", StringComparison.Ordinal) ||
                    BlueprintLibraryUiState.IsOpen ||
                    LegacyMainUiState.Visible ||
                    !string.Equals(BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault()).Mode, BlueprintEntryModes.PlacementPreview, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Placing from the blueprint library must enter world preview and close F5.");
                }
            }
            finally
            {
                BlueprintEntryState.ResetForTesting();
                BlueprintPlacementPreviewState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                LegacyMainUiState.SetVisible(false);
                restore();
            }
        }

        private static void BlueprintLibraryStage09UseSnapshotAndInstanceBoundary()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-stage09-use-instance-boundary");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord source;
                RequireBlueprintSuccess(store.CreateTemplate(CreateSampleBlueprintTemplate("Stage09 Source"), out source), "create stage09 source blueprint");

                BlueprintEntryState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    store,
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("stage09-pair", "stage09-world"));
                RequireBlueprintCommandSuccess(BlueprintLibraryUiState.OpenLibrary(), "open stage09 blueprint library");

                var projectionSignature = BlueprintProjectionService.BuildStateSignature();
                var materialSignature = BlueprintMaterialService.BuildStateSignature();
                var use = LegacyUiActionService.HandleBlueprintLibraryActionForTesting("layout-use", source.TemplateId);
                if (use == null ||
                    !use.Succeeded ||
                    use.PlaceholderOnly ||
                    !string.Equals(use.ResultCode, "previewStarted", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage09 layout-use to enter placement preview through the real template-use path.");
                }

                var preview = BlueprintPlacementPreviewState.GetSnapshot();
                AssertStringEquals(preview.TemplateName, "Stage09 Source", "stage09 preview template name");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault()).Mode, BlueprintEntryModes.PlacementPreview, "stage09 entry preview mode");
                if (!preview.Active ||
                    preview.TemplateSnapshot == null ||
                    GetFirstTemplateContentId(preview.TemplateSnapshot) != 17)
                {
                    throw new InvalidOperationException("Expected stage09 preview to hold a cloned template snapshot.");
                }

                preview.TemplateSnapshot.Name = "Mutated Preview Clone";
                preview.TemplateSnapshot.Cells[0].Layers[0].ContentId = 999;
                var previewAgain = BlueprintPlacementPreviewState.GetSnapshot();
                AssertStringEquals(previewAgain.TemplateSnapshot.Name, "Stage09 Source", "stage09 preview snapshot accessor clone");
                if (GetFirstTemplateContentId(previewAgain.TemplateSnapshot) != 17)
                {
                    throw new InvalidOperationException("Mutating a returned preview snapshot must not change the active preview template.");
                }

                if (BlueprintProjectionService.BuildStateSignature() != projectionSignature ||
                    BlueprintMaterialService.BuildStateSignature() != materialSignature)
                {
                    throw new InvalidOperationException("Using a library template must not refresh projection or material caches.");
                }

                var placement = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 50,
                    TileY = 60,
                    LeftDown = true,
                    LeftPressed = true
                });
                BlueprintEntryState.MarkPlacementConfirmed(placement);
                if (!placement.Succeeded || !placement.PlacedInstance || placement.Instance == null)
                {
                    throw new InvalidOperationException("Expected stage09 placement confirmation to create an instance from the preview snapshot.");
                }

                BlueprintWorldInstanceSnapshot instances;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("stage09-pair", out instances), "load stage09 instances after placement");
                if (instances.Instances.Count != 1)
                {
                    throw new InvalidOperationException("Expected exactly one stage09 placed instance.");
                }

                var placed = instances.Instances[0];
                AssertStringEquals(placed.Name, "Stage09 Source", "stage09 placed instance name snapshot");
                AssertStringEquals(placed.TemplateSnapshot.Name, "Stage09 Source", "stage09 placed instance template snapshot");
                if (GetFirstTemplateContentId(placed.TemplateSnapshot) != 17)
                {
                    throw new InvalidOperationException("Expected placed instance to save the template content snapshot from placement time.");
                }

                var edited = placed.Clone();
                edited.Name = "Edited Instance";
                edited.TemplateSnapshot.Name = "Edited Instance Snapshot";
                edited.TemplateSnapshot.Cells[0].Layers[0].ContentId = 1234;
                BlueprintWorldInstanceSnapshot editedSnapshot;
                RequireBlueprintSuccess(
                    instanceStore.SaveWorldInstances("stage09-pair", "stage09-world", new[] { edited }, out editedSnapshot),
                    "save edited stage09 instance snapshot");

                BlueprintTemplateLibrarySnapshot templates;
                RequireBlueprintSuccess(store.TryLoad(out templates), "load templates after editing instance snapshot");
                AssertStringEquals(FindTemplateName(templates, source.TemplateId), "Stage09 Source", "stage09 template name after instance edit");
                if (GetFirstTemplateContentId(templates.Templates[0]) != 17)
                {
                    throw new InvalidOperationException("Editing an instance snapshot must not write back to the template library.");
                }

                BlueprintTemplateRecord renamed;
                RequireBlueprintSuccess(store.RenameTemplate(source.TemplateId, "Renamed Template", out renamed), "rename stage09 template");
                RequireBlueprintSuccess(store.DeleteTemplate(source.TemplateId), "delete stage09 source template");
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("stage09-pair", out instances), "load stage09 instances after template rename and delete");
                if (instances.Instances.Count != 1)
                {
                    throw new InvalidOperationException("Template rename/delete must not remove placed stage09 instances.");
                }

                AssertStringEquals(instances.Instances[0].Name, "Edited Instance", "stage09 edited instance name after template delete");
                AssertStringEquals(instances.Instances[0].TemplateSnapshot.Name, "Edited Instance Snapshot", "stage09 edited instance snapshot name after template delete");
                if (GetFirstTemplateContentId(instances.Instances[0].TemplateSnapshot) != 1234)
                {
                    throw new InvalidOperationException("Template rename/delete must not overwrite edited instance snapshot content.");
                }
            }
            finally
            {
                BlueprintEntryState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintLibraryStage10DiagnosticsAuditContractsStayWired()
        {
            BlueprintHandheldActionBarOverlayStaysUiOnlyAndNoScan();
            BlueprintHandheldActionBarInputCapturesOnlyInsideBar();
            BlueprintHandheldActionBarDynamicButtonMatrix();
            BlueprintCreateActionButtonSyncsExitStateWithSharedToggle();
            BlueprintLibrarySubmenuAndShortcutRowsOpenSameUiState();
            BlueprintLibraryTwoColumnCardsPreviewAndLayoutButtons();
            BlueprintLibraryStage07NamingRenameDeleteConfirmKeepsInstances();
            BlueprintLibraryStage08ImportExportDiagnostics();
            BlueprintLibraryStage02FileDialogAndMaterialContracts();
            BlueprintLibraryStage02MutualSubmenusAndUseCloseF5();
            BlueprintLibraryStage09UseSnapshotAndInstanceBoundary();
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
                BlueprintLibraryUiState.SetFileDialogServiceForTesting(new FakeBlueprintFileDialogService
                {
                    ExportPath = Path.Combine(BlueprintStoragePaths.BuildDefaultExportDirectory(store.RootDirectory), "commands-export.json")
                });
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

        private sealed class FakeBlueprintFileDialogService : IBlueprintFileDialogService
        {
            public bool CancelImport { get; set; }
            public bool CancelExport { get; set; }
            public bool FailImport { get; set; }
            public bool FailExport { get; set; }
            public string ImportPath { get; set; }
            public string ExportPath { get; set; }
            public string LastImportInitialDirectory { get; private set; }
            public string LastExportInitialDirectory { get; private set; }
            public string LastExportDefaultFileName { get; private set; }

            public FakeBlueprintFileDialogService()
            {
                ImportPath = string.Empty;
                ExportPath = string.Empty;
                LastImportInitialDirectory = string.Empty;
                LastExportInitialDirectory = string.Empty;
                LastExportDefaultFileName = string.Empty;
            }

            public BlueprintFileDialogResult ChooseImportJsonPath(string initialDirectory)
            {
                LastImportInitialDirectory = initialDirectory ?? string.Empty;
                if (FailImport)
                {
                    return BlueprintFileDialogResult.Failed("dialogFailed", "fake import failed");
                }

                if (CancelImport)
                {
                    return BlueprintFileDialogResult.CancelledResult("dialogCancelled", "导入已取消。");
                }

                return BlueprintFileDialogResult.Selected(ImportPath ?? string.Empty);
            }

            public BlueprintFileDialogResult ChooseExportJsonPath(string initialDirectory, string defaultFileName)
            {
                LastExportInitialDirectory = initialDirectory ?? string.Empty;
                LastExportDefaultFileName = defaultFileName ?? string.Empty;
                if (FailExport)
                {
                    return BlueprintFileDialogResult.Failed("dialogFailed", "fake export failed");
                }

                if (CancelExport)
                {
                    return BlueprintFileDialogResult.CancelledResult("dialogCancelled", "导出已取消。");
                }

                var selected = string.IsNullOrWhiteSpace(ExportPath)
                    ? Path.Combine(LastExportInitialDirectory, LastExportDefaultFileName)
                    : ExportPath;
                return BlueprintFileDialogResult.Selected(selected);
            }
        }

        private static void RequireBlueprintCommandSuccess(BlueprintLibraryCommandResult result, string label)
        {
            if (result == null || !result.Succeeded)
            {
                throw new InvalidOperationException("Expected blueprint library command success: " + label);
            }
        }

        private static string FindTemplateName(BlueprintTemplateLibrarySnapshot snapshot, string templateId)
        {
            if (snapshot == null || snapshot.Templates == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < snapshot.Templates.Count; index++)
            {
                var template = snapshot.Templates[index];
                if (template != null &&
                    string.Equals(template.TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
                {
                    return template.Name ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static int GetFirstTemplateContentId(BlueprintTemplateRecord template)
        {
            if (template == null ||
                template.Cells == null ||
                template.Cells.Count <= 0 ||
                template.Cells[0] == null ||
                template.Cells[0].Layers == null ||
                template.Cells[0].Layers.Count <= 0 ||
                template.Cells[0].Layers[0] == null)
            {
                return -1;
            }

            return template.Cells[0].Layers[0].ContentId;
        }

        private static BlueprintTemplateRecord CreatePreviewTemplate(string name, int width, int height)
        {
            var template = new BlueprintTemplateRecord
            {
                Name = name,
                Width = width,
                Height = height,
                AnchorX = width / 2,
                AnchorY = height / 2
            };

            for (var x = 0; x < width; x++)
            {
                template.Cells.Add(CreatePreviewCell(x, 0));
                template.Cells.Add(CreatePreviewCell(x, height - 1));
            }

            for (var y = 1; y < height - 1; y++)
            {
                template.Cells.Add(CreatePreviewCell(0, y));
                template.Cells.Add(CreatePreviewCell(width - 1, y));
            }

            return template;
        }

        private static BlueprintCellRecord CreatePreviewCell(int x, int y)
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
                        ContentId = 1,
                        MaterialItemId = 2,
                        MaterialStack = 1
                    }
                }
            };
        }

    }
}
