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
                instance.CompletedLayers.Add(new BlueprintCompletedLayerRecord
                {
                    X = 0,
                    Y = 0,
                    LayerKind = BlueprintLayerKinds.Tile,
                    CoverageGroup = BlueprintLayerKinds.Tile,
                    ContentId = 17,
                    Style = 0
                });
                instance.CompletedLayers.Add(new BlueprintCompletedLayerRecord
                {
                    X = 0,
                    Y = 0,
                    LayerKind = BlueprintLayerKinds.Tile,
                    CoverageGroup = BlueprintLayerKinds.Tile,
                    ContentId = 17,
                    Style = 0
                });

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

                if (roundtrip.CompletedLayers.Count != 1 ||
                    roundtrip.CompletedLayers[0].X != 0 ||
                    roundtrip.CompletedLayers[0].Y != 0 ||
                    !string.Equals(roundtrip.CompletedLayers[0].LayerKind, BlueprintLayerKinds.Tile, StringComparison.Ordinal) ||
                    roundtrip.CompletedLayers[0].ContentId != 17)
                {
                    throw new InvalidOperationException("Expected completed projection layers to persist and deduplicate by per-instance layer identity.");
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

        private static void BlueprintObjectGroupMetadataPersistsThroughInstanceExportAndImport()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-object-group-roundtrip");
            try
            {
                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord source;
                RequireBlueprintSuccess(
                    templateStore.CreateTemplate(CreateExplicitGroupedTableTemplate("显式组蓝图"), out source),
                    "create explicit object group template");

                AssertCompleteTableGroup(source, "created explicit template");

                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate("playerA-worldA", "worldA", source, 12, 34, 1, out instance),
                    "create object group instance");
                AssertCompleteTableGroup(instance.TemplateSnapshot, "instance object group snapshot");

                string exportPath;
                RequireBlueprintSuccess(templateStore.ExportTemplate(source.TemplateId, string.Empty, out exportPath), "export object group template");
                BlueprintTemplateExportFile export;
                RequireBlueprintSuccess(BlueprintTemplateLibraryStore.ReadExportForTesting(exportPath, out export), "read object group export");
                AssertCompleteTableGroup(export.Template, "exported object group template");

                BlueprintTemplateRecord imported;
                RequireBlueprintSuccess(templateStore.ImportTemplate(exportPath, out imported), "import object group template");
                AssertCompleteTableGroup(imported, "imported object group template");
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintLegacyCompleteObjectRestoresGroupMetadata()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-legacy-object-complete");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord saved;
                RequireBlueprintSuccess(
                    store.CreateTemplate(CreateLegacyCompleteTableTemplate("旧完整家具"), out saved),
                    "create legacy complete object template");

                AssertCompleteTableGroup(saved, "legacy complete object template");
                if (ContainsFlag(saved, BlueprintCaptureMissingCapabilities.LegacyObjectRepaired) ||
                    ContainsFlag(saved, BlueprintCaptureMissingCapabilities.LegacyPartialObject))
                {
                    throw new InvalidOperationException("A complete legacy object should restore group metadata without degraded repair flags.");
                }
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintLegacyPartialObjectRepairsMissingCellsAndFlags()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-legacy-object-repair");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord saved;
                RequireBlueprintSuccess(
                    store.CreateTemplate(CreateLegacyPartialTableTemplate("旧半截家具"), out saved),
                    "create repairable legacy partial object template");

                AssertCompleteTableGroup(saved, "repaired legacy partial object template");
                if (!ContainsFlag(saved, BlueprintCaptureMissingCapabilities.LegacyObjectRepaired) ||
                    saved.Width != 3 ||
                    saved.Height != 2 ||
                    saved.AnchorX != 1)
                {
                    throw new InvalidOperationException("Expected repairable legacy partial object to expand template bounds and record a repair flag.");
                }

                var materialStack = SumObjectMaterialStack(saved);
                if (materialStack != 1)
                {
                    throw new InvalidOperationException("Expected repaired legacy object to keep material counted once, got " + materialStack.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                var layer = RequireObjectLayer(saved, 0, 0);
                if (!string.Equals(layer.ObjectGroupStatus, BlueprintObjectGroupStatuses.RepairedLegacyObject, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected synthesized legacy object cells to be marked as repaired.");
                }
            }
            finally
            {
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintLegacyPartialObjectMarksDegradedWhenUnverifiable()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-legacy-object-degraded");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                var template = CreateLegacyPartialTableTemplate("旧不可证明家具");
                template.Cells[0].Layers[0].Style = 99;

                BlueprintTemplateRecord saved;
                RequireBlueprintSuccess(
                    store.CreateTemplate(template, out saved),
                    "create unverifiable legacy partial object template");

                if (!ContainsFlag(saved, BlueprintCaptureMissingCapabilities.LegacyPartialObject) ||
                    saved.Cells.Count != 1)
                {
                    throw new InvalidOperationException("Expected unverifiable legacy object to stay partial and record a degraded flag.");
                }

                var layer = saved.Cells[0].Layers[0];
                if (!string.Equals(layer.ObjectGroupStatus, BlueprintObjectGroupStatuses.LegacyPartialObject, StringComparison.Ordinal) ||
                    !string.IsNullOrWhiteSpace(layer.ObjectGroupId))
                {
                    throw new InvalidOperationException("Expected unverifiable legacy object to be marked degraded without pretending it has a complete group.");
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

        private static void BlueprintTemplateImportUsesSuffixAndKeepsExistingLibraryOnFailure()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-template-import");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord source;
                RequireBlueprintSuccess(store.CreateTemplate(CreateSampleBlueprintTemplate("Shared Import"), out source), "create source blueprint template");

                string exportPath;
                RequireBlueprintSuccess(store.ExportTemplate(source.TemplateId, string.Empty, out exportPath), "export source blueprint template");

                BlueprintTemplateRecord imported;
                RequireBlueprintSuccess(store.ImportTemplate(exportPath, out imported), "import duplicate blueprint template");
                if (string.Equals(imported.TemplateId, source.TemplateId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Imported blueprint templates must receive a fresh template id.");
                }

                AssertStringEquals(imported.Name, "Shared Import 2", "import duplicate template suffix");

                BlueprintTemplateRecord importedAgain;
                RequireBlueprintSuccess(store.ImportTemplate(exportPath, out importedAgain), "import duplicate blueprint template again");
                AssertStringEquals(importedAgain.Name, "Shared Import 3", "second import duplicate template suffix");

                BlueprintTemplateLibrarySnapshot snapshot;
                RequireBlueprintSuccess(store.TryLoad(out snapshot), "load templates after imports");
                if (snapshot.Templates.Count != 3 || !TemplateExists(snapshot, source.TemplateId))
                {
                    throw new InvalidOperationException("Importing a duplicate blueprint must append templates without overwriting the existing one.");
                }

                var oldText = File.ReadAllText(store.LibraryPath, Encoding.UTF8);
                var badImportPath = Path.Combine(store.RootDirectory, "bad-import.json");
                File.WriteAllText(badImportPath, "{ broken blueprint import", Encoding.UTF8);
                BlueprintTemplateRecord failed;
                var failedImport = store.ImportTemplate(badImportPath, out failed);
                if (failedImport.Succeeded || !string.Equals(failedImport.ResultCode, "readFailed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected corrupt blueprint import JSON to fail with a diagnostic result.");
                }

                AssertFileTextEquals(store.LibraryPath, oldText, "blueprint library after failed import");
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

        private static BlueprintTemplateRecord CreateExplicitGroupedTableTemplate(string name)
        {
            var template = CreateLegacyCompleteTableTemplate(name);
            for (var cellIndex = 0; cellIndex < template.Cells.Count; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                var layer = cell.Layers[0];
                BlueprintObjectGroupNormalizer.SetCapturedObjectGroup(layer, cell.X, cell.Y, 0, 0, 3, 2);
            }

            return template;
        }

        private static BlueprintTemplateRecord CreateLegacyCompleteTableTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
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
                    template.Cells.Add(CreateLegacyTableCell(x, y, x * 18, y * 18, x == 0 && y == 0 ? 1 : 0));
                }
            }

            template.Materials.Add(new BlueprintMaterialEntry
            {
                ItemId = 1006,
                RequiredStack = 1,
                DisplayNameSnapshot = "桌子",
                LayerKind = BlueprintLayerKinds.Object,
                Source = "object"
            });
            return template;
        }

        private static BlueprintTemplateRecord CreateLegacyPartialTableTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
                Name = name,
                Width = 1,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateLegacyTableCell(0, 0, 18, 0, 1));
            template.Materials.Add(new BlueprintMaterialEntry
            {
                ItemId = 1006,
                RequiredStack = 1,
                DisplayNameSnapshot = "桌子",
                LayerKind = BlueprintLayerKinds.Object,
                Source = "object"
            });
            return template;
        }

        private static BlueprintCellRecord CreateLegacyTableCell(int x, int y, int frameX, int frameY, int materialStack)
        {
            return new BlueprintCellRecord
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
                        FrameX = frameX,
                        FrameY = frameY,
                        MaterialItemId = 1006,
                        MaterialStack = materialStack
                    }
                }
            };
        }

        private static void AssertCompleteTableGroup(BlueprintTemplateRecord template, string label)
        {
            if (template == null || template.Cells == null || template.Cells.Count != 6)
            {
                throw new InvalidOperationException("Expected " + label + " to contain all six table cells.");
            }

            var groupId = string.Empty;
            for (var y = 0; y < 2; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    var layer = RequireObjectLayer(template, x, y);
                    if (string.IsNullOrWhiteSpace(layer.ObjectGroupId) ||
                        layer.ObjectOriginX != 0 ||
                        layer.ObjectOriginY != 0 ||
                        layer.ObjectWidth != 3 ||
                        layer.ObjectHeight != 2 ||
                        layer.ObjectSubTileX != x ||
                        layer.ObjectSubTileY != y)
                    {
                        throw new InvalidOperationException("Expected " + label + " to carry complete table object group metadata.");
                    }

                    if (string.IsNullOrEmpty(groupId))
                    {
                        groupId = layer.ObjectGroupId;
                    }
                    else if (!string.Equals(groupId, layer.ObjectGroupId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected " + label + " table cells to share one object group id.");
                    }
                }
            }
        }

        private static BlueprintCellLayerRecord RequireObjectLayer(BlueprintTemplateRecord template, int x, int y)
        {
            for (var cellIndex = 0; template != null && template.Cells != null && cellIndex < template.Cells.Count; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                if (cell == null || cell.X != x || cell.Y != y || cell.Layers == null)
                {
                    continue;
                }

                for (var layerIndex = 0; layerIndex < cell.Layers.Count; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    if (layer != null && string.Equals(layer.LayerKind, BlueprintLayerKinds.Object, StringComparison.Ordinal))
                    {
                        return layer;
                    }
                }
            }

            throw new InvalidOperationException("Missing object layer at " + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static bool ContainsFlag(BlueprintTemplateRecord template, string flag)
        {
            for (var index = 0; template != null && template.MissingCapabilityFlags != null && index < template.MissingCapabilityFlags.Count; index++)
            {
                if (string.Equals(template.MissingCapabilityFlags[index], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int SumObjectMaterialStack(BlueprintTemplateRecord template)
        {
            var sum = 0;
            for (var cellIndex = 0; template != null && template.Cells != null && cellIndex < template.Cells.Count; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                for (var layerIndex = 0; cell != null && cell.Layers != null && layerIndex < cell.Layers.Count; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    if (layer != null && string.Equals(layer.LayerKind, BlueprintLayerKinds.Object, StringComparison.Ordinal))
                    {
                        sum += Math.Max(0, layer.MaterialStack);
                    }
                }
            }

            return sum;
        }

        private static bool TemplateExists(BlueprintTemplateLibrarySnapshot snapshot, string templateId)
        {
            if (snapshot == null || snapshot.Templates == null)
            {
                return false;
            }

            for (var index = 0; index < snapshot.Templates.Count; index++)
            {
                var template = snapshot.Templates[index];
                if (template != null &&
                    string.Equals(template.TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
