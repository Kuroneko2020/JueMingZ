using System;
using System.Collections.Generic;
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

                ReleasePlacementPreviewInitialLeftGate(40, 50);
                var result = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 40,
                    TileY = 50,
                    PhysicalLeftDown = true,
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

                ReleasePlacementPreviewInitialLeftGate(25, 35);
                var result = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 25,
                    TileY = 35,
                    PhysicalLeftDown = true,
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

        private static void BlueprintPlacementPreviewWaitsForPhysicalLeftReleaseBeforeConfirm()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placement-initial-release-gate");
            try
            {
                var templateStore = new BlueprintTemplateLibraryStore();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateEvenBlueprintTemplate("松手门闩"), out template), "create placement release-gate template");
                BlueprintEntryState.ResetForTesting();
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    templateStore,
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-release-gate", "world-release-gate"));

                var begin = BlueprintEntryState.SelectTemplateForPlacement(template);
                if (!begin.Succeeded)
                {
                    throw new InvalidOperationException("Expected template selection to enter placement preview before release-gate regression.");
                }

                var leakedPressInput = BlueprintPlacementPreviewOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    false,
                    true,
                    true,
                    false,
                    false,
                    true,
                    40,
                    50);
                if (!leakedPressInput.PhysicalLeftDown || !leakedPressInput.LeftPressed)
                {
                    throw new InvalidOperationException("Expected the simulated leaked UI click to look like a world press before the preview release gate.");
                }

                var leakedPress = BlueprintPlacementPreviewState.HandlePointer(leakedPressInput);
                if (!leakedPress.ShouldConsumeLeftInput ||
                    leakedPress.PlacedInstance ||
                    !BlueprintPlacementPreviewState.GetSnapshot().Active ||
                    !string.Equals(leakedPress.ResultCode, "awaitingInitialLeftRelease", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Placement preview must consume the activating held left button without creating an instance.");
                }

                BlueprintWorldInstanceSnapshot beforeRelease;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-release-gate", out beforeRelease), "load release-gate world before release");
                if (beforeRelease.Instances.Count != 0)
                {
                    throw new InvalidOperationException("A held activating click must not create a blueprint instance before physical release.");
                }

                var consumedHeldInput = BlueprintPlacementPreviewOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    false,
                    false,
                    true,
                    true,
                    false,
                    true,
                    41,
                    50);
                if (!consumedHeldInput.PhysicalLeftDown ||
                    consumedHeldInput.LeftDown ||
                    consumedHeldInput.LeftPressed ||
                    consumedHeldInput.LeftReleased)
                {
                    throw new InvalidOperationException("Consumed world-left while physical left is held must keep the initial release gate closed.");
                }

                var consumedHeld = BlueprintPlacementPreviewState.HandlePointer(consumedHeldInput);
                if (!consumedHeld.ShouldConsumeLeftInput ||
                    consumedHeld.PlacedInstance ||
                    !BlueprintPlacementPreviewState.GetSnapshot().Active)
                {
                    throw new InvalidOperationException("Consumed-held placement frame should stay active and wait for physical release.");
                }

                var releaseInput = BlueprintPlacementPreviewOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    false,
                    false,
                    false,
                    true,
                    false,
                    true,
                    42,
                    50);
                if (releaseInput.PhysicalLeftDown || !releaseInput.LeftReleased)
                {
                    throw new InvalidOperationException("Expected release frame to clear the placement preview initial gate.");
                }

                var release = BlueprintPlacementPreviewState.HandlePointer(releaseInput);
                if (!release.ShouldConsumeLeftInput ||
                    release.PlacedInstance ||
                    !BlueprintPlacementPreviewState.GetSnapshot().Active ||
                    !string.Equals(release.ResultCode, "initialLeftReleased", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Releasing the activating left button should unlock but not confirm placement.");
                }

                var freshClickInput = BlueprintPlacementPreviewOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                    true,
                    false,
                    false,
                    false,
                    true,
                    true,
                    false,
                    false,
                    true,
                    43,
                    50);
                var confirmed = BlueprintPlacementPreviewState.HandlePointer(freshClickInput);
                BlueprintEntryState.MarkPlacementConfirmed(confirmed);
                if (!confirmed.Succeeded || !confirmed.PlacedInstance || confirmed.Instance == null)
                {
                    throw new InvalidOperationException("A fresh click after physical release should confirm placement.");
                }

                BlueprintWorldInstanceSnapshot afterConfirm;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-release-gate", out afterConfirm), "load release-gate world after confirm");
                if (afterConfirm.Instances.Count != 1 ||
                    afterConfirm.Instances[0].OriginTileX != 42 ||
                    afterConfirm.Instances[0].OriginTileY != 50)
                {
                    throw new InvalidOperationException("Expected only the fresh post-release click to create the blueprint instance.");
                }
            }
            finally
            {
                BlueprintPlacementPreviewState.ResetForTesting();
                BlueprintPlacementPreviewOverlay.ResetInputForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                BlueprintEntryState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintPlacementPreviewWallContentUsesWorldLayerBeforeLatePreviewOverlay()
        {
            var snapshot = CreateActivePlacementPreviewSnapshot(CreatePlacementPreviewMixedWallTemplate("摆放墙层世界层"));
            var worldOrder = BlueprintPlacementPreviewWallWorldOverlay.BuildWorldWallDrawOrderForTesting(snapshot);
            var lateOrder = BlueprintPlacementPreviewOverlay.BuildLatePreviewDrawOrderForTesting(snapshot);

            var wallIndex = IndexOfOrder(worldOrder, "world-preview:wall:30,40");
            var objectIndex = IndexOfOrder(lateOrder, "late-preview:object:30,40");
            var wireIndex = IndexOfOrder(lateOrder, "late-preview:wire:30,40");
            if (wallIndex < 0)
            {
                throw new InvalidOperationException("Expected placement preview wall content to be drawn by the world-layer preview overlay.");
            }

            if (objectIndex < 0 || wireIndex < 0)
            {
                throw new InvalidOperationException("Expected late placement preview overlay to keep non-wall foreground hints for a mixed wall/object/wire cell.");
            }

            for (var index = 0; index < lateOrder.Count; index++)
            {
                if ((lateOrder[index] ?? string.Empty).IndexOf(":wall:", StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException("Late placement preview overlay must not redraw wall content over the real foreground.");
                }
            }

            var combined = new List<string>();
            combined.AddRange(worldOrder);
            combined.Add("terraria-foreground:door-platform-torch-frame");
            combined.AddRange(lateOrder);
            var combinedWallIndex = IndexOfOrder(combined, "world-preview:wall:30,40");
            var terrariaForegroundIndex = IndexOfOrder(combined, "terraria-foreground:door-platform-torch-frame");
            var combinedObjectIndex = IndexOfOrder(combined, "late-preview:object:30,40");
            var combinedWireIndex = IndexOfOrder(combined, "late-preview:wire:30,40");
            if (combinedWallIndex < 0 ||
                terrariaForegroundIndex < 0 ||
                combinedObjectIndex < 0 ||
                combinedWireIndex < 0 ||
                combinedWallIndex >= terrariaForegroundIndex ||
                combinedWallIndex >= combinedObjectIndex ||
                combinedWallIndex >= combinedWireIndex)
            {
                throw new InvalidOperationException("Placement preview wall content must be below Terraria foreground and late preview foreground hints.");
            }

            AssertContains(BlueprintPlacementPreviewWallWorldOverlay.GetVisualContractForTesting(), "before-terraria-foreground");
            AssertContains(BlueprintPlacementPreviewOverlay.GetVisualContractForTesting(), "skip-wall-content");
            AssertContains(BlueprintPlacementPreviewOverlay.GetVisualContractForTesting(), "wall-template-disables-late-range-fill");
            if (IndexOfOrder(lateOrder, "late-preview:range-fill") >= 0 ||
                BlueprintPlacementPreviewOverlay.ShouldDrawRangeFillForTesting(snapshot))
            {
                throw new InvalidOperationException("Placement preview wall templates must not draw a late filled range surface over Terraria foreground.");
            }

            if (IndexOfOrder(lateOrder, "late-preview:range-border") < 0 ||
                IndexOfOrder(lateOrder, "late-preview:anchor") < 0)
            {
                throw new InvalidOperationException("Placement preview wall templates should keep non-filled UI border and anchor hints.");
            }
        }

        private static void BlueprintPlacementPreviewKeepsLayersBeyondFormerDisplayCaps()
        {
            var template = CreatePlacementPreviewLargeTemplateWithLateWallAndForeground("大蓝图预览完整层");
            var snapshot = CreateActivePlacementPreviewSnapshot(template);
            var worldOrder = BlueprintPlacementPreviewWallWorldOverlay.BuildWorldWallDrawOrderForTesting(snapshot);
            var lateOrder = BlueprintPlacementPreviewOverlay.BuildLatePreviewDrawOrderForTesting(snapshot);
            var worldTileX = 30 + 2048 % template.Width;
            var worldTileY = 40 + 2048 / template.Width;

            if (snapshot.PreviewLayers == null ||
                snapshot.PreviewLayers.Count != 2050 ||
                IndexOfOrder(worldOrder, "world-preview:wall:" + worldTileX + "," + worldTileY) < 0 ||
                IndexOfOrder(lateOrder, "late-preview:object:" + worldTileX + "," + worldTileY) < 0)
            {
                throw new InvalidOperationException("Expected placement preview world and late overlays to keep layers beyond the former 512/1536/2048 prefix caps.");
            }

            if (BlueprintPlacementPreviewOverlay.ShouldDrawRangeFillForTesting(snapshot) ||
                IndexOfOrder(lateOrder, "late-preview:range-fill") >= 0)
            {
                throw new InvalidOperationException("Expected a wall layer beyond the former 512-cell scan cap to still disable the late filled range surface.");
            }
        }

        private static void BlueprintPlacementPreviewLateOverlaySkipsWallContentWhenWorldLayerActive()
        {
            var snapshot = CreateActivePlacementPreviewSnapshot(CreatePlacementPreviewMixedWallTemplate("摆放晚层跳墙"));
            var lateOrder = BlueprintPlacementPreviewOverlay.BuildLatePreviewDrawOrderForTesting(snapshot);
            if (BlueprintPlacementPreviewOverlay.ShouldDrawTemplateLayerInLateOverlayForTesting(BlueprintLayerKinds.Wall))
            {
                throw new InvalidOperationException("Expected placement preview late overlay to skip wall layers.");
            }

            if (!BlueprintPlacementPreviewOverlay.ShouldDrawTemplateLayerInLateOverlayForTesting(BlueprintLayerKinds.Object) ||
                !BlueprintPlacementPreviewOverlay.ShouldDrawTemplateLayerInLateOverlayForTesting(BlueprintLayerKinds.Wire) ||
                !BlueprintPlacementPreviewOverlay.ShouldDrawTemplateLayerInLateOverlayForTesting(BlueprintLayerKinds.Actuator))
            {
                throw new InvalidOperationException("Expected placement preview late overlay to retain non-wall foreground and wiring hints.");
            }

            if (IndexOfOrder(lateOrder, "late-preview:object:30,40") < 0 ||
                IndexOfOrder(lateOrder, "late-preview:tile:31,40") < 0 ||
                IndexOfOrder(lateOrder, "late-preview:actuator:31,40") < 0)
            {
                throw new InvalidOperationException("Expected placement preview mixed cells to keep foreground layers even when wall is present in the same template.");
            }

            if (IndexOfOrder(lateOrder, "late-preview:range-fill") >= 0 ||
                BlueprintPlacementPreviewOverlay.ShouldDrawRangeFillForTesting(snapshot))
            {
                throw new InvalidOperationException("Mixed wall placement preview must keep foreground hints without reintroducing a filled wall-like range surface.");
            }
        }

        private static void BlueprintPlacementPreviewWallTemplateDoesNotDrawLateRangeFillOverForeground()
        {
            var wallSnapshot = CreateActivePlacementPreviewSnapshot(CreatePlacementPreviewMixedWallTemplate("含墙禁范围面"));
            var wallOrder = BlueprintPlacementPreviewOverlay.BuildLatePreviewDrawOrderForTesting(wallSnapshot);
            if (BlueprintPlacementPreviewOverlay.ShouldDrawRangeFillForTesting(wallSnapshot) ||
                IndexOfOrder(wallOrder, "late-preview:range-fill") >= 0)
            {
                throw new InvalidOperationException("A placement preview containing wall content must not draw a late filled range rectangle over real foreground.");
            }

            if (IndexOfOrder(wallOrder, "late-preview:range-border") < 0 ||
                IndexOfOrder(wallOrder, "late-preview:anchor") < 0 ||
                IndexOfOrder(wallOrder, "late-preview:object:30,40") < 0)
            {
                throw new InvalidOperationException("Disabling the filled range surface must not remove border, anchor or non-wall foreground hints.");
            }

            var foregroundSnapshot = CreateActivePlacementPreviewSnapshot(CreatePlacementPreviewForegroundOnlyTemplate("纯前景保留极淡范围面"));
            var foregroundOrder = BlueprintPlacementPreviewOverlay.BuildLatePreviewDrawOrderForTesting(foregroundSnapshot);
            if (!BlueprintPlacementPreviewOverlay.ShouldDrawRangeFillForTesting(foregroundSnapshot) ||
                IndexOfOrder(foregroundOrder, "late-preview:range-fill") < 0 ||
                IndexOfOrder(foregroundOrder, "late-preview:tile:30,40") < 0)
            {
                throw new InvalidOperationException("A pure foreground placement preview may keep the weak range fill and tile hint.");
            }
        }

        private static void BlueprintPlacementPreviewMultitileObjectUsesOriginalGhostLayers()
        {
            BlueprintPlacementPreviewState.ResetForTesting();
            var reader = new FakeBlueprintWorldTileReader();
            var template = CreatePlacementPreviewObjectGroupTemplate("整件预览原贴图", string.Empty);
            try
            {
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    null,
                    null,
                    BlueprintPlacementWorldContext.Success("pair-preview-object", "world-preview-object"),
                    reader);
                var begin = BlueprintPlacementPreviewState.BeginPreview(template, "test");
                if (!begin.Succeeded)
                {
                    throw new InvalidOperationException("Expected object group template to enter placement preview.");
                }

                var hover = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 20,
                    TileY = 30
                });
                if (!hover.Succeeded)
                {
                    throw new InvalidOperationException("Expected placement preview hover to build object group preview layers.");
                }

                var snapshot = BlueprintPlacementPreviewState.GetSnapshot();
                var left = FindPreviewLayer(snapshot.PreviewLayers, BlueprintLayerKinds.Object, 20, 30);
                var right = FindPreviewLayer(snapshot.PreviewLayers, BlueprintLayerKinds.Object, 21, 30);
                if (left == null ||
                    right == null ||
                    !string.Equals(left.Status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal) ||
                    !string.Equals(right.Status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(left.ObjectGroupKey) ||
                    !string.Equals(left.ObjectGroupKey, right.ObjectGroupKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected placement preview to expose complete missing object group layers.");
                }

                if (BlueprintProjectionOverlay.ShouldDrawFallbackBlockForTesting(BlueprintProjectionLayerStatuses.Missing))
                {
                    throw new InvalidOperationException("Missing placement preview objects must use original ghost texture input instead of a fallback state block.");
                }

                var readsAfterHover = reader.ReadCount;
                var order = BlueprintPlacementPreviewOverlay.BuildLatePreviewDrawOrderForTesting(snapshot);
                if (IndexOfOrder(order, "late-preview:object:20,30") < 0 ||
                    IndexOfOrder(order, "late-preview:object:21,30") < 0 ||
                    reader.ReadCount != readsAfterHover)
                {
                    throw new InvalidOperationException("Expected late placement preview draw to consume cached original object layers without reading world tiles.");
                }

                AssertContains(BlueprintPlacementPreviewOverlay.GetVisualContractForTesting(), "foreground-original-ghost");
                AssertContains(BlueprintPlacementPreviewOverlay.GetVisualContractForTesting(), "draw-snapshot-only");
            }
            finally
            {
                BlueprintPlacementPreviewState.ResetForTesting();
            }
        }

        private static void BlueprintPlacementPreviewObjectGroupConflictMarksWholeGroup()
        {
            BlueprintPlacementPreviewState.ResetForTesting();
            var reader = new FakeBlueprintWorldTileReader();
            reader.Set(21, 30, new BlueprintWorldTileSnapshot { Active = true, TileType = 999 });
            var template = CreatePlacementPreviewObjectGroupTemplate("整件冲突预览", string.Empty);
            try
            {
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    null,
                    null,
                    BlueprintPlacementWorldContext.Success("pair-preview-conflict", "world-preview-conflict"),
                    reader);
                RequireBlueprintCommandSuccess(BlueprintPlacementPreviewState.BeginPreview(template, "test"), "begin object conflict preview");
                BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 20,
                    TileY = 30
                });

                var snapshot = BlueprintPlacementPreviewState.GetSnapshot();
                var left = FindPreviewLayer(snapshot.PreviewLayers, BlueprintLayerKinds.Object, 20, 30);
                var right = FindPreviewLayer(snapshot.PreviewLayers, BlueprintLayerKinds.Object, 21, 30);
                if (left == null ||
                    right == null ||
                    !string.Equals(left.Status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal) ||
                    !string.Equals(right.Status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal) ||
                    !string.Equals(left.ObjectGroupStatus, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal) ||
                    !string.Equals(right.ObjectGroupStatus, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected any conflicting object sub-tile to mark the whole placement preview group red.");
                }

                var conflictColor = BlueprintProjectionOverlay.ResolveProjectionColorForTesting(BlueprintProjectionLayerStatuses.Conflict);
                if (conflictColor[0] < 200 || conflictColor[1] > 120)
                {
                    throw new InvalidOperationException("Expected placement preview group conflict to reuse the projection red visual policy.");
                }
            }
            finally
            {
                BlueprintPlacementPreviewState.ResetForTesting();
            }
        }

        private static void BlueprintPlacementPreviewDegradedPartialObjectStaysUnavailable()
        {
            var reader = new FakeBlueprintWorldTileReader();
            var template = CreatePlacementPreviewLegacyPartialObjectTemplate("降级旧半件");
            var layers = BlueprintPlacementPreviewLayerBuilder.Build(template, template.TemplateId, template.Name, 10, 15, reader);
            var layer = FindPreviewLayer(layers, BlueprintLayerKinds.Object, 10, 15);
            if (layer == null ||
                !string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Unavailable, StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(layer.ObjectGroupKey))
            {
                throw new InvalidOperationException("Expected degraded legacy partial object to stay unavailable without pretending it has a complete object group.");
            }

            if (!BlueprintPlacementPreviewLayerBuilder.IsLegacyPartialObjectForTesting(template.Cells[0].Layers[0]))
            {
                throw new InvalidOperationException("Expected legacy partial object marker to remain visible to placement preview validation.");
            }
        }

        private static void BlueprintPlacementConfirmRefreshesObjectGroupValidationWithoutBlockingInstance()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placement-confirm-refresh-object-group");
            try
            {
                var reader = new FakeBlueprintWorldTileReader();
                var instanceStore = new BlueprintWorldInstanceStore();
                var template = CreatePlacementPreviewObjectGroupTemplate("确认前整件校验", string.Empty);
                BlueprintPlacementPreviewState.SetPlacementDependenciesForTesting(
                    null,
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-preview-confirm", "world-preview-confirm"),
                    reader);
                RequireBlueprintCommandSuccess(BlueprintPlacementPreviewState.BeginPreview(template, "test"), "begin confirm validation preview");
                BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 20,
                    TileY = 30
                });
                var readsAfterHover = reader.ReadCount;
                reader.Set(21, 30, new BlueprintWorldTileSnapshot { Active = true, TileType = 999 });

                var result = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
                {
                    WorldTileHit = true,
                    TileX = 20,
                    TileY = 30,
                    PhysicalLeftDown = true,
                    LeftDown = true,
                    LeftPressed = true
                });
                if (!result.Succeeded || !result.PlacedInstance || result.Instance == null)
                {
                    throw new InvalidOperationException("Placement confirmation should still create a blueprint hint instance when preview validation finds conflicts.");
                }

                if (reader.ReadCount <= readsAfterHover)
                {
                    throw new InvalidOperationException("Expected placement confirmation to refresh object group validation before creating the hint instance.");
                }
            }
            finally
            {
                BlueprintPlacementPreviewState.ResetForTesting();
                restore();
            }
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

            var physicalEdge = BlueprintPlacementPreviewOverlay.BuildPointerInputFromPhysicalEdgesForTesting(
                true,
                false,
                false,
                false,
                false,
                true,
                true,
                false,
                true,
                6,
                7);
            if (!physicalEdge.PhysicalLeftDown || physicalEdge.LeftDown || physicalEdge.LeftPressed || physicalEdge.LeftReleased)
            {
                throw new InvalidOperationException("Expected placement physical-left helper to keep consumed world-left from becoming a fake press or release.");
            }

            if (BlueprintPlacementPreviewOverlay.ShouldConsumeAfterPlayerInputForTesting(true, true, true) ||
                !BlueprintPlacementPreviewOverlay.ShouldConsumeAfterPlayerInputForTesting(true, false, true))
            {
                throw new InvalidOperationException("Expected placement after-PlayerInput consumption to preserve Legacy UI ownership while guarding world input.");
            }
        }

        private static int IndexOfOrder(IReadOnlyList<string> order, string expected)
        {
            if (order == null)
            {
                return -1;
            }

            for (var index = 0; index < order.Count; index++)
            {
                if (string.Equals(order[index], expected, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static BlueprintPlacementPreviewSnapshot CreateActivePlacementPreviewSnapshot(BlueprintTemplateRecord template)
        {
            var reader = new FakeBlueprintWorldTileReader();
            return new BlueprintPlacementPreviewSnapshot
            {
                Active = true,
                HoverTileHit = true,
                HoverTileX = 31,
                HoverTileY = 40,
                OriginTileX = 30,
                OriginTileY = 40,
                Width = template.Width,
                Height = template.Height,
                AnchorX = template.AnchorX,
                AnchorY = template.AnchorY,
                TemplateId = template.TemplateId,
                TemplateName = template.Name,
                TemplateSnapshot = template,
                PreviewLayers = BlueprintPlacementPreviewLayerBuilder.Build(template, template.TemplateId, template.Name, 30, 40, reader)
            };
        }

        private static BlueprintProjectionCellSnapshot FindPreviewLayer(
            IReadOnlyList<BlueprintProjectionCellSnapshot> layers,
            string layerKind,
            int worldTileX,
            int worldTileY)
        {
            for (var index = 0; layers != null && index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer != null &&
                    string.Equals(layer.LayerKind, layerKind, StringComparison.OrdinalIgnoreCase) &&
                    layer.WorldTileX == worldTileX &&
                    layer.WorldTileY == worldTileY)
                {
                    return layer;
                }
            }

            return null;
        }

        private static void RequireBlueprintCommandSuccess(BlueprintPlacementCommandResult result, string label)
        {
            if (result == null || !result.Succeeded)
            {
                throw new InvalidOperationException("Expected blueprint command success for " + label + ".");
            }
        }

        private static BlueprintTemplateRecord CreatePlacementPreviewMixedWallTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = 2,
                Height = 1,
                AnchorX = 0,
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
                        LayerKind = BlueprintLayerKinds.Wall,
                        ContentId = 2,
                        MaterialItemId = 2,
                        MaterialStack = 1
                    },
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Object,
                        ContentId = 4,
                        MaterialItemId = 4,
                        MaterialStack = 1
                    },
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Wire,
                        ContentId = BlueprintCaptureWireFlags.Red,
                        MaterialItemId = 530,
                        MaterialStack = 1
                    }
                }
            });
            template.Cells.Add(new BlueprintCellRecord
            {
                X = 1,
                Y = 0,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Wall,
                        ContentId = 2,
                        MaterialItemId = 2,
                        MaterialStack = 1
                    },
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Tile,
                        ContentId = 1,
                        MaterialItemId = 1,
                        MaterialStack = 1
                    },
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Actuator,
                        ContentId = 849,
                        MaterialItemId = 849,
                        MaterialStack = 1
                    }
                }
            });
            return template;
        }

        private static BlueprintTemplateRecord CreatePlacementPreviewForegroundOnlyTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = 1,
                Height = 1,
                AnchorX = 0,
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
            return template;
        }

        private static BlueprintTemplateRecord CreatePlacementPreviewLargeTemplateWithLateWallAndForeground(string name)
        {
            var width = 89;
            var wallIndex = 2048;
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = width,
                Height = (wallIndex + width) / width,
                AnchorX = 0,
                AnchorY = 0
            };

            for (var index = 0; index < wallIndex; index++)
            {
                template.Cells.Add(new BlueprintCellRecord
                {
                    X = index % width,
                    Y = index / width,
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
            }

            template.Cells.Add(new BlueprintCellRecord
            {
                X = wallIndex % width,
                Y = wallIndex / width,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Wall,
                        ContentId = 2,
                        MaterialItemId = 2,
                        MaterialStack = 1
                    },
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Object,
                        ContentId = 4,
                        MaterialItemId = 4,
                        MaterialStack = 1
                    }
                }
            });

            return template;
        }

        private static BlueprintTemplateRecord CreatePlacementPreviewObjectGroupTemplate(string name, string objectGroupStatus)
        {
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = 2,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            var groupId = BlueprintObjectGroupNormalizer.BuildObjectGroupId(21, 3, 0, 0, 2, 1);
            template.Cells.Add(new BlueprintCellRecord
            {
                X = 0,
                Y = 0,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Object,
                        ContentId = 21,
                        Style = 3,
                        FrameX = 0,
                        FrameY = 0,
                        MaterialItemId = 100,
                        MaterialStack = 1,
                        ObjectGroupId = groupId,
                        ObjectOriginX = 0,
                        ObjectOriginY = 0,
                        ObjectWidth = 2,
                        ObjectHeight = 1,
                        ObjectSubTileX = 0,
                        ObjectSubTileY = 0,
                        ObjectGroupStatus = objectGroupStatus ?? string.Empty
                    }
                }
            });
            template.Cells.Add(new BlueprintCellRecord
            {
                X = 1,
                Y = 0,
                Layers =
                {
                    new BlueprintCellLayerRecord
                    {
                        LayerKind = BlueprintLayerKinds.Object,
                        ContentId = 21,
                        Style = 3,
                        FrameX = 18,
                        FrameY = 0,
                        MaterialItemId = 100,
                        MaterialStack = 0,
                        ObjectGroupId = groupId,
                        ObjectOriginX = 0,
                        ObjectOriginY = 0,
                        ObjectWidth = 2,
                        ObjectHeight = 1,
                        ObjectSubTileX = 1,
                        ObjectSubTileY = 0,
                        ObjectGroupStatus = objectGroupStatus ?? string.Empty
                    }
                }
            });
            return template;
        }

        private static BlueprintTemplateRecord CreatePlacementPreviewLegacyPartialObjectTemplate(string name)
        {
            var template = new BlueprintTemplateRecord
            {
                TemplateId = "template-" + Guid.NewGuid().ToString("N"),
                Name = name,
                Width = 1,
                Height = 1,
                AnchorX = 0,
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
                        LayerKind = BlueprintLayerKinds.Object,
                        ContentId = 99999,
                        Style = 0,
                        FrameX = 0,
                        FrameY = 0,
                        MaterialItemId = 100,
                        MaterialStack = 1,
                        ObjectGroupStatus = BlueprintObjectGroupStatuses.LegacyPartialObject,
                        ObjectGroupReason = BlueprintCaptureMissingCapabilities.LegacyPartialObject
                    }
                }
            });
            return template;
        }

        private static void ReleasePlacementPreviewInitialLeftGate(int tileX, int tileY)
        {
            var result = BlueprintPlacementPreviewState.HandlePointer(new BlueprintPlacementPointerInput
            {
                WorldTileHit = true,
                TileX = tileX,
                TileY = tileY,
                LeftReleased = true
            });
            if (!result.Succeeded || result.PlacedInstance || !BlueprintPlacementPreviewState.GetSnapshot().Active)
            {
                throw new InvalidOperationException("Expected placement preview release-gate helper to keep preview active without creating an instance.");
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
