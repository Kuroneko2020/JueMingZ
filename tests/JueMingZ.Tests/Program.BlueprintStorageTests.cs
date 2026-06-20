using System;
using System.IO;
using System.Text;
using JueMingZ.Automation.Blueprint;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintTemplateNamesUseDefaultAndNumericSuffix()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-template-names");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                AssertPathUnderConfigDirectory(store.RootDirectory, "blueprint root");
                AssertPathUnderConfigDirectory(store.LibraryPath, "blueprint library");

                BlueprintTemplateRecord first;
                BlueprintTemplateRecord second;
                BlueprintTemplateRecord third;
                RequireBlueprintSuccess(store.CreateDefaultTemplate(3, 2, out first), "create first blueprint");
                RequireBlueprintSuccess(store.CreateDefaultTemplate(3, 2, out second), "create second blueprint");
                RequireBlueprintSuccess(store.CreateDefaultTemplate(3, 2, out third), "create third blueprint");

                AssertStringEquals(first.Name, BlueprintStorageConstants.DefaultTemplateName, "first blueprint name");
                AssertStringEquals(second.Name, BlueprintStorageConstants.DefaultTemplateName + " 2", "second blueprint name");
                AssertStringEquals(third.Name, BlueprintStorageConstants.DefaultTemplateName + " 3", "third blueprint name");
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintWorldInstancesKeepTemplateSnapshotAfterTemplateDelete()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-template-instance-separation");
            try
            {
                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateSampleBlueprintTemplate("塔楼"), out template), "create blueprint template");

                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate("playerA-worldA", "worldA", template, 100, 200, 7, out instance),
                    "create blueprint instance");
                RequireBlueprintSuccess(templateStore.DeleteTemplate(template.TemplateId), "delete source template");

                BlueprintTemplateLibrarySnapshot templates;
                RequireBlueprintSuccess(templateStore.TryLoad(out templates), "reload templates after delete");
                if (templates.Templates.Count != 0)
                {
                    throw new InvalidOperationException("Deleting a template should remove it from the template library.");
                }

                BlueprintWorldInstanceSnapshot instances;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("playerA-worldA", out instances), "reload world instances");
                if (instances.Instances.Count != 1)
                {
                    throw new InvalidOperationException("Deleting a template must not delete placed blueprint instances.");
                }

                var saved = instances.Instances[0];
                AssertStringEquals(saved.Name, "塔楼", "instance name snapshot");
                AssertStringEquals(saved.TemplateSnapshot.Name, "塔楼", "instance template snapshot name");
                if (saved.TemplateSnapshot.Cells.Count != 1 || saved.TemplateSnapshot.Materials.Count != 1)
                {
                    throw new InvalidOperationException("Instance must keep a deep template snapshot after template deletion.");
                }
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintModelsPersistCellsMaterialsEraseMaskAndLayerOrder()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-model-roundtrip");
            try
            {
                var instanceStore = new BlueprintWorldInstanceStore();
                var template = CreateSampleBlueprintTemplate("样本蓝图");
                template.TemplateId = "template-sample";
                var instance = new BlueprintWorldInstanceRecord
                {
                    InstanceId = "instance-sample",
                    Name = template.Name,
                    TemplateIdSnapshot = template.TemplateId,
                    WorldPairKey = "playerA-worldA",
                    WorldKey = "worldA",
                    OriginTileX = 10,
                    OriginTileY = 20,
                    LayerOrder = 12,
                    Hidden = true,
                    MaterialWindowVisible = false,
                    TemplateSnapshot = template.Clone()
                };
                instance.EraseMask.Add(new BlueprintEraseMaskCellRecord { X = 1, Y = 0 });
                instance.EraseMask.Add(new BlueprintEraseMaskCellRecord { X = 1, Y = 0 });

                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(
                    instanceStore.SaveWorldInstances("playerA-worldA", "worldA", new[] { instance }, out saved),
                    "save explicit blueprint instance");

                BlueprintWorldInstanceSnapshot loaded;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("playerA-worldA", out loaded), "load explicit blueprint instance");
                if (loaded.Instances.Count != 1)
                {
                    throw new InvalidOperationException("Expected one blueprint instance after reload.");
                }

                var roundtrip = loaded.Instances[0];
                if (roundtrip.LayerOrder != 12 || !roundtrip.Hidden || roundtrip.MaterialWindowVisible)
                {
                    throw new InvalidOperationException("Expected instance layer order, hidden state and material window state to persist.");
                }

                if (roundtrip.EraseMask.Count != 1 || roundtrip.EraseMask[0].X != 1 || roundtrip.EraseMask[0].Y != 0)
                {
                    throw new InvalidOperationException("Expected erase mask to persist and deduplicate cells.");
                }

                var cell = roundtrip.TemplateSnapshot.Cells[0];
                var layer = cell.Layers[0];
                if (!string.Equals(layer.LayerKind, BlueprintLayerKinds.Tile, StringComparison.Ordinal) ||
                    layer.ContentId != 17 ||
                    layer.MaterialItemId != 9 ||
                    roundtrip.TemplateSnapshot.Materials[0].RequiredStack != 4)
                {
                    throw new InvalidOperationException("Expected cell layer and material entries to roundtrip.");
                }
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintSafeWriteFailureKeepsExistingTemplateLibrary()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-safe-write-failure");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord seed;
                RequireBlueprintSuccess(store.CreateTemplate(CreateSampleBlueprintTemplate("旧蓝图"), out seed), "seed blueprint template");
                var oldText = File.ReadAllText(store.LibraryPath, Encoding.UTF8);

                BlueprintTemplateLibraryStore.SetCommitFailurePredicateForTesting(
                    path => string.Equals(path, store.LibraryPath, StringComparison.OrdinalIgnoreCase));
                BlueprintTemplateRecord failed;
                var result = store.CreateTemplate(CreateSampleBlueprintTemplate("新蓝图"), out failed);
                if (result.Succeeded)
                {
                    throw new InvalidOperationException("Expected simulated blueprint library commit failure.");
                }

                AssertFileTextEquals(store.LibraryPath, oldText, "blueprint library after failed commit");
                BlueprintTemplateLibrarySnapshot snapshot;
                RequireBlueprintSuccess(store.TryLoad(out snapshot), "reload blueprint library after failed commit");
                if (snapshot.Templates.Count != 1 || !string.Equals(snapshot.Templates[0].Name, "旧蓝图", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Failed commit must keep the original template library.");
                }
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintCorruptJsonFailsSoftWithoutOverwrite()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-corrupt-json");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                Directory.CreateDirectory(store.RootDirectory);
                File.WriteAllText(store.LibraryPath, "{ broken blueprint library", Encoding.UTF8);

                BlueprintTemplateLibrarySnapshot snapshot;
                var result = store.TryLoad(out snapshot);
                if (result.Succeeded || !string.Equals(result.ResultCode, "readFailed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected corrupt blueprint JSON to fail softly.");
                }

                AssertFileTextEquals(store.LibraryPath, "{ broken blueprint library", "corrupt blueprint library");
                if (snapshot.Templates.Count != 0)
                {
                    throw new InvalidOperationException("Corrupt blueprint library should expose an empty fallback snapshot.");
                }
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintTemplateExportContainsTemplateOnly()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-template-export");
            try
            {
                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateSampleBlueprintTemplate("导出蓝图"), out template), "create export template");
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate("playerA-worldA", "worldA", template, 7, 8, 1, out instance),
                    "create instance before export");

                string exportPath;
                RequireBlueprintSuccess(templateStore.ExportTemplate(template.TemplateId, string.Empty, out exportPath), "export blueprint template");
                var exportText = File.ReadAllText(exportPath, Encoding.UTF8);
                AssertContains(exportText, "Template");
                AssertContains(exportText, "JueMingZ.Blueprint.Template");
                AssertDoesNotContain(exportText, "Instances");
                AssertDoesNotContain(exportText, "WorldPairKey");

                BlueprintTemplateExportFile export;
                RequireBlueprintSuccess(BlueprintTemplateLibraryStore.ReadExportForTesting(exportPath, out export), "read blueprint export");
                AssertStringEquals(export.Template.Name, "导出蓝图", "export template name");
                if (export.Template.Cells.Count != 1 || export.Template.Materials.Count != 1)
                {
                    throw new InvalidOperationException("Export must carry template data.");
                }
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static BlueprintTemplateRecord CreateSampleBlueprintTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
                Name = name,
                Width = 3,
                Height = 2,
                AnchorX = 1,
                AnchorY = 1
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
                        ContentId = 17,
                        Style = 2,
                        FrameX = 18,
                        FrameY = 0,
                        PaintId = 3,
                        CoatingFlags = 1,
                        Slope = 0,
                        HalfBrick = false,
                        Inactive = false,
                        MaterialItemId = 9,
                        MaterialStack = 4
                    }
                }
            });
            template.Materials.Add(new BlueprintMaterialEntry
            {
                ItemId = 9,
                RequiredStack = 4,
                DisplayNameSnapshot = "木材",
                LayerKind = BlueprintLayerKinds.Tile,
                Source = "tile"
            });
            template.MissingCapabilityFlags.Add("liquid-not-supported");
            return template;
        }

        private static void RequireBlueprintSuccess(BlueprintStorageOperationResult result, string label)
        {
            if (result == null || !result.Succeeded)
            {
                throw new InvalidOperationException("Expected " + label + " to succeed, got " + (result == null ? "<null>" : result.ResultCode + ":" + result.Message));
            }
        }
    }
}
