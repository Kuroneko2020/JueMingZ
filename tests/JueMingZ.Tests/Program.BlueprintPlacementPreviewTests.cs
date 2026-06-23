using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintPlacementPreviewUsesUpperLeftCenterAnchorForEvenSize()
        {
            var template = CreateEvenBlueprintTemplate("偶数蓝图");
            int originX;
            int originY;
            BlueprintPlacementPreviewState.CalculateOriginForTesting(template, 100, 200, out originX, out originY);
            if (originX != 99 || originY != 200)
            {
                throw new InvalidOperationException("Expected even-sized blueprint preview center anchor to round toward the upper-left tile.");
            }
        }

        private static void BlueprintLibraryUseEntersPlacementPreviewWithTemplateSnapshot()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-use-placement-preview");
            try
            {
                var store = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(store.CreateTemplate(CreateSampleBlueprintTemplate("使用模板"), out template), "create blueprint template");
                BlueprintLibraryUiState.SetStoreForTesting(store, true);
                BlueprintEntryState.ResetForTesting();

                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.BuildBlueprintLibraryCommandIdForTesting("use", template.TemplateId),
                    Label = "蓝图库:使用",
                    Kind = "button",
                    MouseCaptured = true
                });

                var entry = BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault());
                var preview = BlueprintPlacementPreviewState.GetSnapshot();
                AssertStringEquals(entry.Mode, BlueprintEntryModes.PlacementPreview, "blueprint library use entry mode");
                AssertStringEquals(preview.TemplateId, template.TemplateId, "blueprint placement preview template id");
                if (!preview.Active || preview.TemplateSnapshot == null || preview.TemplateSnapshot.Cells.Count <= 0)
                {
                    throw new InvalidOperationException("Expected blueprint library use to retain a template snapshot for placement preview.");
                }

                AssertContains(LegacyMainWindow.BuildBlueprintPlacementSummaryForTesting(), "摆放预览");
            }
            finally
            {
                BlueprintPlacementPreviewState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                BlueprintEntryState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintPlacementConfirmCreatesWorldInstanceOnly()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placement-confirm");
            try
            {
                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateEvenBlueprintTemplate("待摆放"), out template), "create placement template");
                BlueprintEntryState.ResetForTesting();
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    templateStore,
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-a", "world-a"));
                var begin = BlueprintEntryState.SelectTemplateForPlacement(template);
                if (!begin.Succeeded)
                {
                    throw new InvalidOperationException("Expected template selection to enter placement preview.");
                }

                var result = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 40,
                    TileY = 50,
                    LeftDown = true,
                    LeftPressed = true
                });
                BlueprintEntryState.MarkPlacementConfirmed(result);
                if (!result.Succeeded || !result.PlacedInstance || result.Instance == null)
                {
                    throw new InvalidOperationException("Expected left click on a world tile to create a blueprint world instance.");
                }

                BlueprintWorldInstanceSnapshot instances;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-a", out instances), "load placed blueprint instances");
                if (instances.Instances.Count != 1)
                {
                    throw new InvalidOperationException("Expected exactly one blueprint instance after confirming placement.");
                }

                var placed = instances.Instances[0];
                if (placed.OriginTileX != 39 ||
                    placed.OriginTileY != 50 ||
                    !string.Equals(placed.WorldKey, "world-a", StringComparison.Ordinal) ||
                    !string.Equals(placed.TemplateSnapshot.TemplateId, template.TemplateId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected placement confirmation to save origin, world key and template snapshot only.");
                }

                var entry = BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault());
                AssertStringEquals(entry.Mode, BlueprintEntryModes.Tool, "blueprint entry mode after placement confirm");
                if (BlueprintPlacementPreviewState.GetSnapshot().Active)
                {
                    throw new InvalidOperationException("Expected placement preview to exit after creating an instance.");
                }
            }
            finally
            {
                BlueprintPlacementPreviewState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                BlueprintEntryState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintPlacementConfirmRefreshesProjectionAndPlacedList()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placement-refresh-projection");
            try
            {
                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var context = BlueprintPlacementWorldContext.Success("pair-stage04-placement", "world-stage04-placement");
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(
                    templateStore.CreateTemplate(CreateProjectionTileOnlyTemplate("04 可见实例", 77), out template),
                    "create stage04 placement template");
                reader.Set(25, 35, new BlueprintWorldTileSnapshot());

                BlueprintEntryState.ResetForTesting();
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(templateStore, instanceStore, context);
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(instanceStore, context, false);
                BlueprintProjectionService.SetDependenciesForTesting(instanceStore, context, reader, true);

                var begin = BlueprintEntryState.SelectTemplateForPlacement(template);
                if (!begin.Succeeded)
                {
                    throw new InvalidOperationException("Expected stage04 template selection to enter placement preview.");
                }

                var result = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 25,
                    TileY = 35,
                    LeftDown = true,
                    LeftPressed = true
                });
                BlueprintEntryState.MarkPlacementConfirmed(result);
                BlueprintPlacedInstanceUiState.NotifyInstanceCreated(result.Instance);
                if (!result.Succeeded || !result.PlacedInstance || result.Instance == null)
                {
                    throw new InvalidOperationException("Expected stage04 placement confirm to create a placed instance.");
                }

                var placed = BlueprintPlacedInstanceUiState.GetSnapshot();
                if (!placed.LoadSucceeded ||
                    placed.Instances.Count != 1 ||
                    !string.Equals(placed.SelectedInstanceId, result.Instance.InstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage04 placement confirm to refresh the current-world placed blueprint list.");
                }

                var diagnostics = BlueprintProjectionService.GetDiagnostics();
                var cached = BlueprintProjectionService.GetCachedSnapshotForDraw();
                if (diagnostics.EffectiveLayerCount != 1 ||
                    cached == null ||
                    !cached.LoadSucceeded ||
                    cached.ProjectedLayers == null ||
                    cached.ProjectedLayers.Count != 1 ||
                    !string.Equals(cached.ProjectedLayers[0].InstanceId, result.Instance.InstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage04 placement confirm to refresh projection cache outside Draw so the placed instance can be displayed.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                BlueprintPlacementPreviewState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                BlueprintEntryState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintPlacementUiHitConsumesWithoutCreatingInstance()
        {
            BlueprintPlacementPreviewState.ResetForTesting();
            BlueprintEntryState.ResetForTesting();
            var template = CreateEvenBlueprintTemplate("UI 命中");
            var begin = BlueprintEntryState.SelectTemplateForPlacement(template);
            if (!begin.Succeeded)
            {
                throw new InvalidOperationException("Expected preview to begin for UI hit test.");
            }

            var result = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
            {
                UiOwned = true,
                WorldTileHit = true,
                TileX = 10,
                TileY = 12,
                LeftDown = true,
                LeftPressed = true
            });
            if (!result.ShouldConsumeLeftInput || result.PlacedInstance || !BlueprintPlacementPreviewState.GetSnapshot().Active)
            {
                throw new InvalidOperationException("Expected UI-owned placement clicks to be consumed without creating an instance.");
            }

            BlueprintPlacementPreviewState.ResetForTesting();
            BlueprintEntryState.ResetForTesting();
        }

        private static void BlueprintPlacementOverlayRoutesAndPointerContract()
        {
            if (!BlueprintPlacementPreviewOverlay.ShouldRegisterWorldOverlayForTesting())
            {
                throw new InvalidOperationException("Expected blueprint placement preview overlay registration contract to stay enabled.");
            }

            var pointer = BlueprintPlacementPreviewOverlay.BuildPointerInputForTesting(
                true,
                false,
                true,
                true,
                true,
                false,
                true,
                4,
                5);
            if (!pointer.UiOwned || !pointer.LeftPressed || !pointer.WorldTileHit)
            {
                throw new InvalidOperationException("Expected blueprint placement pointer contract to preserve UI ownership and world hit metadata.");
            }

            if (BlueprintPlacementPreviewOverlay.ShouldConsumeAfterPlayerInputForTesting(true, true, true) ||
                !BlueprintPlacementPreviewOverlay.ShouldConsumeAfterPlayerInputForTesting(true, false, true))
            {
                throw new InvalidOperationException("Expected placement after-PlayerInput consumption to preserve Legacy UI ownership while guarding world input.");
            }
        }

        private static BlueprintTemplateRecord CreateEvenBlueprintTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = 4,
                Height = 2,
                AnchorX = 1,
                AnchorY = 0
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
                        ContentId = 1,
                        MaterialItemId = 1,
                        MaterialStack = 1
                    }
                }
            });
            template.Cells.Add(new BlueprintCellRecord
            {
                X = 3,
                Y = 1,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Wall,
                        ContentId = 2,
                        MaterialItemId = 2,
                        MaterialStack = 1
                    }
                }
            });
            return template;
        }
    }
}
