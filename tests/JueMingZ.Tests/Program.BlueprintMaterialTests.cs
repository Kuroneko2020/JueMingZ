using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Hooks;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintMaterialsCountOnlyMissingEffectiveProjectionLayers()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-material-effective-missing");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord lower;
                BlueprintWorldInstanceRecord upper;
                BlueprintWorldInstanceRecord hidden;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-material", "world-material", CreateSingleMaterialTemplate("下层", 11, 400, 7), 10, 20, 0, out lower), "create covered material instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-material", "world-material", CreateMixedMaterialTemplate(), 10, 20, 1, out upper), "create effective material instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-material", "world-material", CreateSingleMaterialTemplate("隐藏", 15, 404, 9), 40, 20, 2, out hidden), "create hidden material instance");

                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(store.TryLoadWorld("pair-material", out saved), "load material instances");
                var instances = new[]
                {
                    saved.Instances[0],
                    saved.Instances[1],
                    saved.Instances[2]
                };
                instances[2].Hidden = true;
                RequireBlueprintSuccess(store.SaveWorldInstances("pair-material", "world-material", instances, out saved), "save hidden material instance");

                reader.Set(11, 20, new BlueprintWorldTileSnapshot { Active = true, TileType = 13 });
                reader.Set(12, 20, new BlueprintWorldTileSnapshot { Active = true, TileType = 99 });
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-material", "world-material"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);

                var projection = BlueprintProjectionService.GetSnapshot();
                var materials = BlueprintMaterialService.GetSnapshot();
                if (projection.CoveredLayerCount != 1 ||
                    projection.HiddenInstanceCount != 1 ||
                    materials.RequiredItemCount != 1 ||
                    materials.RequiredStackTotal != 5 ||
                    materials.MissingStackTotal != 5 ||
                    materials.ProjectionMissingLayerCount != 1 ||
                    materials.SkippedFulfilledLayerCount != 1 ||
                    materials.SkippedConflictLayerCount != 1 ||
                    materials.Items.Count != 1 ||
                    materials.Items[0].ItemId != 401)
                {
                    throw new InvalidOperationException("Expected blueprint materials to count only missing effective projection layers and skip fulfilled, conflict, hidden and covered content.");
                }

                AssertContains(BlueprintMaterialService.BuildUiStateJson(), "\"requiredItemCount\":1");
                AssertContains(LegacyMainWindow.BuildBlueprintMaterialSummaryForTesting(), "材料：需求 1 项");
            }
            finally
            {
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintMaterialsIgnoreAirOnlyTemplateBounds()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-material-air-bounds");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-material-air", "world-material-air", CreateProjectionAirBoundsTemplate(), 50, 60, 0, out instance), "create material air-bound instance");
                reader.Set(50, 60, new BlueprintWorldTileSnapshot { Active = true, TileType = 999 });
                reader.Set(51, 61, new BlueprintWorldTileSnapshot { Active = true, TileType = 77 });
                reader.Set(52, 62, new BlueprintWorldTileSnapshot { Active = true, TileType = 998 });
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-material-air", "world-material-air"),
                    reader,
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);

                var projection = BlueprintProjectionService.GetSnapshot();
                var materials = BlueprintMaterialService.GetSnapshot();
                if (projection.EffectiveLayerCount != 1 ||
                    projection.FulfilledLayerCount != 1 ||
                    materials.ProjectionMissingLayerCount != 0 ||
                    materials.RequiredItemCount != 0 ||
                    materials.MissingStackTotal != 0 ||
                    materials.SkippedConflictLayerCount != 0)
                {
                    throw new InvalidOperationException("Expected material statistics to ignore air-only template bounds and count only content layers.");
                }
            }
            finally
            {
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintMaterialsStage05SubtractCompletedProgressFromDemand()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-material-stage05-completed-progress");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-material-stage05", "world-material-stage05", CreateStage05NineCellMaterialTemplate(), 20, 30, 0, out instance), "create stage05 material instance");
                reader.Set(20, 30, new BlueprintWorldTileSnapshot { Active = true, TileType = 100 });
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-material-stage05", "world-material-stage05"),
                    reader,
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);

                var firstProjection = BlueprintProjectionService.GetSnapshot();
                var firstMaterials = BlueprintMaterialService.GetSnapshot();
                if (!firstProjection.LoadSucceeded ||
                    firstProjection.FulfilledLayerCount != 1 ||
                    firstProjection.MissingLayerCount != 8 ||
                    firstMaterials.RequiredItemCount != 1 ||
                    firstMaterials.RequiredStackTotal != 8 ||
                    firstMaterials.ProjectionMissingLayerCount != 8 ||
                    firstMaterials.SkippedFulfilledLayerCount != 1)
                {
                    throw new InvalidOperationException("Expected stage05 materials to subtract the first fulfilled layer from the 9-cell requirement.");
                }

                reader.Set(20, 30, new BlueprintWorldTileSnapshot());
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-material-stage05", "world-material-stage05"),
                    reader,
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);

                var secondProjection = BlueprintProjectionService.GetSnapshot();
                var secondMaterials = BlueprintMaterialService.GetSnapshot();
                if (!secondProjection.LoadSucceeded ||
                    secondProjection.FulfilledLayerCount != 0 ||
                    secondProjection.CompletedLayerCount != 1 ||
                    secondProjection.MissingLayerCount != 8 ||
                    secondMaterials.RequiredItemCount != 1 ||
                    secondMaterials.RequiredStackTotal != 8 ||
                    secondMaterials.ProjectionMissingLayerCount != 8 ||
                    secondMaterials.SkippedFulfilledLayerCount != 1 ||
                    secondMaterials.Items.Count != 1 ||
                    secondMaterials.Items[0].RequiredStack != 8)
                {
                    throw new InvalidOperationException("Expected stage05 materials to keep completed progress deducted after the completed cell is mined again.");
                }
            }
            finally
            {
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintMaterialsReadMainInventoryAndVoidBagAvailability()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-material-inventory");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-inv", "world-inv", CreateSingleMaterialTemplate("需求", 21, 501, 10), 3, 4, 0, out instance), "create material inventory instance");
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-inv", "world-inv"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 501, 3 } },
                    new Dictionary<int, int> { { 501, 4 } }), true);

                var materials = BlueprintMaterialService.GetSnapshot();
                if (!materials.LoadSucceeded ||
                    !materials.InventoryReadSucceeded ||
                    materials.ResultCode != "missing" ||
                    materials.RequiredStackTotal != 10 ||
                    materials.AvailableStackTotal != 7 ||
                    materials.MissingStackTotal != 3 ||
                    materials.InventoryMainStackTotal != 3 ||
                    materials.InventoryVoidBagStackTotal != 4 ||
                    materials.Items.Count != 1 ||
                    materials.Items[0].MainInventoryStack != 3 ||
                    materials.Items[0].VoidBagStack != 4)
                {
                    throw new InvalidOperationException("Expected blueprint materials to read availability from main inventory and void bag only.");
                }
            }
            finally
            {
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintPlacedListRefreshesMaterialComparisonWithoutDrawScan()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placed-material-comparison");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var inventory = new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 501, 3 } },
                    new Dictionary<int, int> { { 501, 4 } });
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-placed-material", "world-placed-material", CreateSingleMaterialTemplate("需求", 21, 501, 10), 3, 4, 0, out instance), "create placed material instance");
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-placed-material", "world-placed-material"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(inventory, true);
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-placed-material", "world-placed-material"), true);

                var open = BlueprintPlacedInstanceUiState.OpenManagement();
                if (!open.Succeeded || inventory.ReadCount != 1)
                {
                    throw new InvalidOperationException("Expected placed list open to force exactly one material comparison refresh.");
                }

                AssertContains(LegacyMainWindow.GetBlueprintPlacedInstanceVisualContractForTesting(), "material-comparison");
                AssertContains(LegacyMainWindow.GetBlueprintPlacedInstanceVisualContractForTesting(), "hide-show-cancel-place");
                AssertContains(LegacyMainWindow.GetBlueprintMaterialVisualContractForTesting(), "placed-list-comparison");
                var comparison = LegacyMainWindow.BuildBlueprintPlacedMaterialComparisonForTesting(1);
                AssertContains(comparison, "需求材料");
                AssertContains(comparison, "需要 10");
                AssertContains(comparison, "已有 7");
                AssertContains(comparison, "缺 3");
                AssertContains(comparison, "主包 3");
                AssertContains(comparison, "虚空袋 4");
                LegacyMainWindow.BuildBlueprintPlacedMaterialComparisonForTesting(1);
                if (inventory.ReadCount != 1)
                {
                    throw new InvalidOperationException("Expected placed-list material comparison draw helper to read the cached material snapshot only.");
                }
            }
            finally
            {
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintPlacedListStage03LayoutMaterialAndCards()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placed-stage03-layout-materials");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                var inventory = new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 501, 3 } },
                    new Dictionary<int, int> { { 501, 4 }, { 502, 1 } });
                BlueprintWorldInstanceRecord wood;
                BlueprintWorldInstanceRecord stone;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-stage03", "world-stage03", CreateSingleMaterialTemplate("木材需求", 21, 501, 10), 3, 4, 0, out wood), "create stage03 wood material instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-stage03", "world-stage03", CreateSingleMaterialTemplate("石材需求", 22, 502, 5), 6, 4, 1, out stone), "create stage03 stone material instance");

                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-stage03", "world-stage03"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(inventory, true);
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-stage03", "world-stage03"), true);

                var open = BlueprintPlacedInstanceUiState.OpenManagement();
                if (!open.Succeeded || inventory.ReadCount != 1)
                {
                    throw new InvalidOperationException("Expected stage 03 placed list open to refresh aggregate materials once.");
                }

                var contract = LegacyMainWindow.GetBlueprintPlacedInstanceVisualContractForTesting();
                AssertContains(contract, "stage03-title-count");
                AssertContains(contract, "stage03-header-page-back");
                AssertContains(contract, "stage03-material-total-all-lines");
                AssertContains(contract, "stage03-card-preview");
                AssertContains(contract, "stage03-library-sized-cards");
                AssertContains(contract, "read-only-name");
                AssertContains(contract, "hide-show-cancel-place");
                AssertContains(contract, "card-material-modal");
                AssertDoesNotContain(contract, "select");
                AssertDoesNotContain(contract, "cancel-display");
                AssertDoesNotContain(contract, "layer-up-down");

                AssertStringEquals(
                    LegacyMainWindow.BuildBlueprintPlacedHeaderSummaryForTesting(2),
                    "当前世界已放置2个蓝图",
                    "stage03 placed header count text");
                var summary = LegacyMainWindow.BuildBlueprintPlacedMaterialSummaryForTesting();
                AssertContains(summary, "材料总计");
                AssertContains(summary, "总量 15");
                AssertContains(summary, "已有 8");
                AssertContains(summary, "仍缺 7");

                var allMaterials = LegacyMainWindow.BuildBlueprintPlacedMaterialComparisonForTesting(int.MaxValue);
                AssertContains(allMaterials, "木材需求材料");
                AssertContains(allMaterials, "石材需求材料");
                AssertContains(allMaterials, "需要 10");
                AssertContains(allMaterials, "需要 5");
                if (LegacyMainWindow.GetBlueprintPlacedMaterialLineTextScaleForTesting() < 0.84f)
                {
                    throw new InvalidOperationException("Expected stage 03 placed material line text scale to increase by 0.3 from the old 0.54 baseline.");
                }

                if (LegacyMainWindow.CalculateBlueprintPlacedMaterialPanelHeightForTesting() <= 78)
                {
                    throw new InvalidOperationException("Expected stage 03 material panel height to grow when all material rows are shown.");
                }

                var viewport = new LegacyUiRect(20, 20, 460, 260);
                var card0 = LegacyMainWindow.CalculateBlueprintPlacedCardRectForTesting(viewport, 40, 0);
                var card1 = LegacyMainWindow.CalculateBlueprintPlacedCardRectForTesting(viewport, 40, 1);
                if (card0.Height != card1.Height ||
                    card0.Y != card1.Y ||
                    card1.X <= card0.X ||
                    LegacyMainWindow.CalculateBlueprintPlacedListHeightForTesting(true, 2, 2) != card0.Height)
                {
                    throw new InvalidOperationException("Expected stage 03 placed cards to use two-column library-sized layout.");
                }

                RunPlacedInstanceCommand("materials", wood.InstanceId);
                var materialSnapshot = BlueprintPlacedInstanceUiState.GetSnapshot();
                if (!string.Equals(materialSnapshot.ExpandedMaterialInstanceId, wood.InstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage 03 placed material button to open the selected instance material modal.");
                }

                var modalText = LegacyMainWindow.BuildBlueprintPlacedInstanceMaterialListTextForTesting(wood);
                AssertContains(modalText, "木材需求材料 x10");
                var projectionSignature = BlueprintProjectionService.BuildStateSignature();
                var materialSignature = BlueprintMaterialService.BuildStateSignature();
                var elements = new List<LegacyUiElement>
                {
                    CreateLegacyUiElementForTesting("blueprint-entry:start-create", "Lower", "button", viewport)
                };
                var mouse = new LegacyMouseSnapshot
                {
                    ReadAvailable = true,
                    LeftPressed = true
                };
                var area = LegacyScrollArea.Create(viewport, 620, 0);
                var coordinator = LegacyUiOverlayCoordinator.Current;
                coordinator.ResetForTesting();
                coordinator.BeginFrame("blueprint");
                if (!LegacyMainWindow.RegisterBlueprintPlacedMaterialModalOverlayForTesting(area))
                {
                    throw new InvalidOperationException("Expected stage 03 placed material list to register as a modal overlay.");
                }

                coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 560, 340), "blueprint", AppSettings.CreateDefault(), elements);
                var modal = FindLegacyUiElementForTesting(elements, LegacyMainWindow.GetBlueprintPlacedMaterialModalElementIdForTesting());
                mouse.X = modal.Rect.X + 12;
                mouse.Y = modal.Rect.Y + 40;
                bool blocked;
                var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
                if (!blocked || clickId.Length != 0)
                {
                    throw new InvalidOperationException("Expected stage 03 placed material modal blocker to stop lower blueprint page clicks.");
                }

                var close = FindLegacyUiElementForTesting(elements, LegacyMainWindow.BuildBlueprintPlacedMaterialCloseCommandIdForTesting(wood.InstanceId));
                mouse.X = close.Rect.X + Math.Max(1, close.Rect.Width / 2);
                mouse.Y = close.Rect.Y + Math.Max(1, close.Rect.Height / 2);
                clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
                coordinator.EndFrame();
                if (blocked || clickId != close.Id)
                {
                    throw new InvalidOperationException("Expected stage 03 placed material modal close button to remain clickable above the modal blocker.");
                }

                RunPlacedInstanceCommand("materials-close", wood.InstanceId);
                if (!string.IsNullOrWhiteSpace(BlueprintPlacedInstanceUiState.GetSnapshot().ExpandedMaterialInstanceId))
                {
                    throw new InvalidOperationException("Expected stage 03 placed material close command to clear modal state.");
                }

                if (BlueprintProjectionService.BuildStateSignature() != projectionSignature ||
                    BlueprintMaterialService.BuildStateSignature() != materialSignature ||
                    inventory.ReadCount != 1)
                {
                    throw new InvalidOperationException("Expected stage 03 placed material modal draw and close to read only cached projection/material state.");
                }
            }
            finally
            {
                LegacyUiOverlayCoordinator.Current.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintSubmenusKeepBodyVisibleAfterHeaderScroll()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-submenu-header-scroll");
            try
            {
                ConfigService.Initialize();
                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                LegacyUiOverlayCoordinator.Current.ResetForTesting();

                var templateStore = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord libraryTemplate;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateSampleBlueprintTemplate("标题滚出蓝图库"), out libraryTemplate), "create header-scroll library template");
                BlueprintLibraryUiState.SetStoreForTesting(templateStore, true);
                RequireBlueprintCommandSuccess(BlueprintLibraryUiState.OpenLibrary(), "open header-scroll library");

                var content = new LegacyUiRect(20, 20, 520, 180);
                var mouse = new LegacyMouseSnapshot { ReadAvailable = true, X = 0, Y = 0 };
                var libraryArea = LegacyScrollArea.Create(content, LegacyMainWindow.CalculateBlueprintContentHeightForTesting(), 64);
                if (LegacyMainWindow.IsBlueprintSubmenuHeaderVisibleForTesting(libraryArea))
                {
                    throw new InvalidOperationException("Expected library submenu title row to be outside the scrolled viewport.");
                }

                var libraryElements = new List<LegacyUiElement>();
                LegacyMainWindow.DrawBlueprintLibrarySubmenuForTesting(libraryArea, mouse, libraryElements);
                FindLegacyUiElementForTesting(
                    libraryElements,
                    LegacyMainWindow.BuildBlueprintLibraryLayoutCommandIdForTesting("use", libraryTemplate.TemplateId));

                BlueprintLibraryUiState.CloseLibrary();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintWorldInstanceRecord placed;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate(
                        "pair-header-scroll",
                        "world-header-scroll",
                        CreateSingleMaterialTemplate("标题滚出已放置", 21, 501, 10),
                        3,
                        4,
                        0,
                        out placed),
                    "create header-scroll placed instance");
                BlueprintProjectionService.SetDependenciesForTesting(
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-header-scroll", "world-header-scroll"),
                    new FakeBlueprintWorldTileReader(),
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(
                    new FakeBlueprintMaterialInventoryReader(new Dictionary<int, int> { { 501, 4 } }, new Dictionary<int, int>()),
                    true);
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-header-scroll", "world-header-scroll"),
                    true);

                var entry = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.OpenPlacedInstances, ConfigService.AppSettings ?? AppSettings.CreateDefault());
                if (entry == null || !entry.Succeeded)
                {
                    throw new InvalidOperationException("Expected header-scroll placed list entry mode to open.");
                }

                var open = BlueprintPlacedInstanceUiState.OpenManagement();
                if (open == null || !open.Succeeded)
                {
                    throw new InvalidOperationException("Expected header-scroll placed list to load.");
                }

                var placedArea = LegacyScrollArea.Create(content, LegacyMainWindow.CalculateBlueprintContentHeightForTesting(), 64);
                if (LegacyMainWindow.IsBlueprintSubmenuHeaderVisibleForTesting(placedArea))
                {
                    throw new InvalidOperationException("Expected placed submenu title row to be outside the scrolled viewport.");
                }

                var placedElements = new List<LegacyUiElement>();
                LegacyMainWindow.DrawBlueprintPlacedSubmenuForTesting(placedArea, mouse, placedElements);
                FindLegacyUiElementForTesting(
                    placedElements,
                    LegacyMainWindow.BuildBlueprintPlacedInstanceCommandIdForTesting("materials", placed.InstanceId));
                AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "scroll-header-nonblocking");
                AssertContains(LegacyMainWindow.GetBlueprintPlacedInstanceVisualContractForTesting(), "scroll-header-nonblocking");
            }
            finally
            {
                LegacyUiOverlayCoordinator.Current.ResetForTesting();
                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintPlacedListLayoutCacheTracksManagementMaterials()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-placed-layout-cache-materials");
            try
            {
                ConfigService.Initialize();
                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                LegacyMainWindow.ResetPageLayoutCacheForTesting();

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
                var content = new LegacyUiRect(58, 134, 520, 180);
                var toolLayout = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("blueprint", window, content, 0, settings);

                var store = new BlueprintWorldInstanceStore();
                BlueprintWorldInstanceRecord placed;
                RequireBlueprintSuccess(
                    store.CreateInstanceFromTemplate(
                        "pair-placed-layout-cache",
                        "world-placed-layout-cache",
                        CreateSingleMaterialTemplate("布局缓存材料", 21, 501, 10),
                        3,
                        4,
                        0,
                        out placed),
                    "create layout-cache placed instance");
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-placed-layout-cache", "world-placed-layout-cache"),
                    new FakeBlueprintWorldTileReader(),
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(
                    new FakeBlueprintMaterialInventoryReader(new Dictionary<int, int> { { 501, 2 }, { 502, 0 } }, new Dictionary<int, int>()),
                    true);
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-placed-layout-cache", "world-placed-layout-cache"),
                    true);

                var entry = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.OpenPlacedInstances, settings);
                var open = BlueprintPlacedInstanceUiState.OpenManagement();
                if (entry == null || !entry.Succeeded || open == null || !open.Succeeded)
                {
                    throw new InvalidOperationException("Expected placed-list layout cache test to enter management mode.");
                }

                var placedLayout = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("blueprint", window, content, 0, settings);
                if (placedLayout.RebuildCount <= toolLayout.RebuildCount ||
                    placedLayout.PageStateSignature == toolLayout.PageStateSignature)
                {
                    throw new InvalidOperationException("Expected blueprint page layout cache to dirty when entering placed management mode.");
                }

                BlueprintWorldInstanceRecord second;
                RequireBlueprintSuccess(
                    store.CreateInstanceFromTemplate(
                        "pair-placed-layout-cache",
                        "world-placed-layout-cache",
                        CreateSingleMaterialTemplate("布局缓存第二材料", 22, 502, 5),
                        5,
                        4,
                        1,
                        out second),
                    "create second layout-cache placed instance");

                open = BlueprintPlacedInstanceUiState.OpenManagement();
                if (open == null || !open.Succeeded)
                {
                    throw new InvalidOperationException("Expected placed-list layout cache test to refresh management mode after material state changes.");
                }

                var materialChangedLayout = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("blueprint", window, content, 0, settings);
                if (materialChangedLayout.RebuildCount <= placedLayout.RebuildCount ||
                    materialChangedLayout.ContentHeight <= placedLayout.ContentHeight ||
                    materialChangedLayout.PageStateSignature == placedLayout.PageStateSignature)
                {
                    throw new InvalidOperationException("Expected blueprint page layout cache to dirty when placed material row count changes content height.");
                }
            }
            finally
            {
                LegacyMainWindow.ResetPageLayoutCacheForTesting();
                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintMaterialsUseReplacementItemWhenConfigured()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-material-replacement");
            try
            {
                ConfigService.Initialize();
                ConfigService.AppSettings.BlueprintReplacementEnabled = true;
                ConfigService.AppSettings.BlueprintReplacementTorchesEnabled = true;
                RegisterReplacementItemForTesting(104, 4, 2);

                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-material-replace", "world-material-replace", CreateAutoPlacementLayerTemplate("替换火把材料", BlueprintLayerKinds.Object, 4, 1004, 1), 3, 4, 0, out instance), "create replacement material instance");
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-material-replace", "world-material-replace"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 104, 1 } },
                    new Dictionary<int, int>()), true);

                var materials = BlueprintMaterialService.GetSnapshot();
                if (!materials.LoadSucceeded ||
                    materials.RequiredItemCount != 1 ||
                    materials.RequiredStackTotal != 1 ||
                    materials.MissingStackTotal != 0 ||
                    materials.Items.Count != 1 ||
                    materials.Items[0].ItemId != 104 ||
                    materials.Items[0].MainInventoryStack != 1)
                {
                    var itemId = materials.Items.Count <= 0 ? 0 : materials.Items[0].ItemId;
                    var mainStack = materials.Items.Count <= 0 ? 0 : materials.Items[0].MainInventoryStack;
                    var candidates = BlueprintReplacementRuleService.GetCandidateItemIdsForLayer(
                        new BlueprintProjectionCellSnapshot { LayerKind = BlueprintLayerKinds.Object, ContentId = 4 },
                        BlueprintReplacementRuleService.FromSettings(ConfigService.AppSettings));
                    throw new InvalidOperationException(
                        "Expected configured torch replacement to satisfy material requirements with the replacement item. actual result=" +
                        materials.ResultCode +
                        ", requiredItems=" + materials.RequiredItemCount +
                        ", missing=" + materials.MissingStackTotal +
                        ", itemId=" + itemId +
                        ", main=" + mainStack +
                        ", inventoryStatus=" + materials.InventoryReadStatus +
                        ", candidateCount=" + candidates.Count);
                }
            }
            finally
            {
                ResetReplacementItemsForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintMaterialWindowRoutesAndConsumesInput()
        {
            BlueprintMaterialWindowOverlay.ResetForTesting();
            try
            {
                BlueprintMaterialWindowState.Show();
                var snapshot = new BlueprintMaterialSnapshot
                {
                    LoadSucceeded = true,
                    ResultCode = "missing",
                    Message = "仍缺少材料堆叠 3。",
                    RequiredItemCount = 2,
                    RequiredStackTotal = 12,
                    AvailableStackTotal = 9,
                    MissingStackTotal = 3,
                    Items = new List<BlueprintMaterialItemSnapshot>
                    {
                        new BlueprintMaterialItemSnapshot { ItemId = 1, DisplayName = "石块", RequiredStack = 10, AvailableStack = 7, MissingStack = 3 },
                        new BlueprintMaterialItemSnapshot { ItemId = 2, DisplayName = "木材", RequiredStack = 2, AvailableStack = 2, MissingStack = 0 }
                    }
                };
                var frame = BlueprintMaterialWindowOverlay.BuildFrameForTesting(snapshot, 800, 600, 0, 0);
                if (!frame.Visible || !BlueprintMaterialWindowOverlay.ShouldCaptureMouseForTesting(frame, frame.BodyRect.X + 4, frame.BodyRect.Y + 4))
                {
                    throw new InvalidOperationException("Expected blueprint material window to build a visible mouse-capturing frame.");
                }

                AssertContains(BlueprintMaterialWindowOverlay.GetVisualContractForTesting(), "drag-opacity-close");
                AssertContains(LegacyMainWindow.GetBlueprintMaterialVisualContractForTesting(), "void-bag");
                AssertContains(string.Join(";", InterfaceLayerHookCallbacks.GetUiOverlayDispatcherRouteNamesForTesting(true)), "BlueprintMaterialWindowOverlay.DrawInterfaceLayer");
                AssertContains(string.Join(";", InterfaceLayerHookCallbacks.GetUiOverlayDispatcherRouteNamesForTesting(false)), "BlueprintMaterialWindowOverlay.DrawInterfaceLayer");

                var wheel = BlueprintMaterialWindowOverlay.HandleInputForTesting(frame, frame.BodyRect.X + 4, frame.BodyRect.Y + 4, false, false, false, -120, 800, 600);
                if (!wheel.CapturedMouse || !wheel.ScrollConsumed)
                {
                    throw new InvalidOperationException("Expected material window wheel input to be consumed.");
                }

                var oldOpacity = frame.OpacityPercent;
                var opacity = BlueprintMaterialWindowOverlay.HandleInputForTesting(frame, frame.OpacityDownRect.X + 2, frame.OpacityDownRect.Y + 2, true, true, false, 0, 800, 600);
                var afterOpacity = BlueprintMaterialWindowOverlay.BuildFrameForTesting(snapshot, 800, 600, 0, 0);
                if (!opacity.OpacityChanged || afterOpacity.OpacityPercent >= oldOpacity)
                {
                    throw new InvalidOperationException("Expected material window opacity button to reduce opacity.");
                }

                var dragStart = BlueprintMaterialWindowOverlay.HandleInputForTesting(afterOpacity, afterOpacity.HeaderRect.X + 12, afterOpacity.HeaderRect.Y + 12, true, true, false, 0, 800, 600);
                var dragMove = BlueprintMaterialWindowOverlay.HandleInputForTesting(afterOpacity, afterOpacity.HeaderRect.X + 62, afterOpacity.HeaderRect.Y + 28, true, false, false, 0, 800, 600);
                var dragged = BlueprintMaterialWindowOverlay.BuildFrameForTesting(snapshot, 800, 600, 0, 0);
                if (!dragStart.DragStarted || !dragMove.Dragging || dragged.WindowRect.X == afterOpacity.WindowRect.X)
                {
                    throw new InvalidOperationException("Expected material window header drag to move the frame.");
                }

                var dragRelease = BlueprintMaterialWindowOverlay.HandleInputForTesting(dragged, dragged.HeaderRect.X + 62, dragged.HeaderRect.Y + 28, false, false, true, 0, 800, 600);
                if (!dragRelease.DragEnded)
                {
                    throw new InvalidOperationException("Expected material window drag release to end dragging before the next click.");
                }

                var released = BlueprintMaterialWindowOverlay.BuildFrameForTesting(snapshot, 800, 600, 0, 0);
                var close = BlueprintMaterialWindowOverlay.HandleInputForTesting(released, released.CloseRect.X + 2, released.CloseRect.Y + 2, true, true, false, 0, 800, 600);
                if (!close.Closed || BlueprintMaterialWindowState.Visible)
                {
                    throw new InvalidOperationException("Expected material window close button to hide the window.");
                }
            }
            finally
            {
                BlueprintMaterialWindowOverlay.ResetForTesting();
            }
        }

        private static void BlueprintMaterialDiagnosticsWriteRuntimeSnapshotJson()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-material-diagnostics");
            try
            {
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-diag", "world-diag", CreateSingleMaterialTemplate("诊断", 61, 601, 6), 7, 8, 0, out instance), "create material diagnostic instance");
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-diag", "world-diag"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 601, 6 } },
                    new Dictionary<int, int>()), true);
                BlueprintMaterialWindowState.Show();
                BlueprintMaterialService.ForceRefreshForMaterialWindow();

                var runtimeSnapshot = RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
                {
                    Initialized = true,
                    Version = "test-blueprint-materials"
                });
                var json = InvokeDiagnosticSnapshotJson(runtimeSnapshot);
                AssertContains(json, "\"BlueprintMaterialsLastStatus\": \"complete\"");
                AssertContains(json, "\"BlueprintMaterialsRequiredItemCount\": 1");
                AssertContains(json, "\"BlueprintMaterialsInventoryReadSucceeded\": true");
                AssertContains(json, "\"BlueprintMaterialsWindowVisible\": true");
            }
            finally
            {
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static BlueprintTemplateRecord CreateStage05NineCellMaterialTemplate()
        {
            var template = new BlueprintTemplateRecord
            {
                Name = "05 九格材料",
                Width = 9,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };

            for (var index = 0; index < 9; index++)
            {
                template.Cells.Add(CreateMaterialTileCell(index, 0, 100 + index, 501, 1, "05材料"));
            }

            AddMaterialEntries(template);
            return template;
        }

        private static BlueprintTemplateRecord CreateMixedMaterialTemplate()
        {
            var template = new BlueprintTemplateRecord
            {
                Name = "混合材料",
                Width = 3,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateMaterialTileCell(0, 0, 12, 401, 5, "有效缺失材料"));
            template.Cells.Add(CreateMaterialTileCell(1, 0, 13, 402, 3, "已完成材料"));
            template.Cells.Add(CreateMaterialTileCell(2, 0, 14, 403, 4, "冲突材料"));
            AddMaterialEntries(template);
            return template;
        }

        private static BlueprintTemplateRecord CreateSingleMaterialTemplate(string name, int tileType, int materialItemId, int materialStack)
        {
            var template = new BlueprintTemplateRecord
            {
                Name = name,
                Width = 1,
                Height = 1,
                AnchorX = 0,
                AnchorY = 0
            };
            template.Cells.Add(CreateMaterialTileCell(0, 0, tileType, materialItemId, materialStack, name + "材料"));
            AddMaterialEntries(template);
            return template;
        }

        private static BlueprintCellRecord CreateMaterialTileCell(int x, int y, int tileType, int materialItemId, int materialStack, string displayName)
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
                        ContentId = tileType,
                        MaterialItemId = materialItemId,
                        MaterialStack = materialStack,
                        Note = displayName ?? string.Empty
                    }
                }
            };
        }

        private static void AddMaterialEntries(BlueprintTemplateRecord template)
        {
            if (template == null || template.Cells == null)
            {
                return;
            }

            var seen = new HashSet<int>();
            for (var cellIndex = 0; cellIndex < template.Cells.Count; cellIndex++)
            {
                var cell = template.Cells[cellIndex];
                for (var layerIndex = 0; cell != null && cell.Layers != null && layerIndex < cell.Layers.Count; layerIndex++)
                {
                    var layer = cell.Layers[layerIndex];
                    if (layer == null || layer.MaterialItemId <= 0 || !seen.Add(layer.MaterialItemId))
                    {
                        continue;
                    }

                    template.Materials.Add(new BlueprintMaterialEntry
                    {
                        ItemId = layer.MaterialItemId,
                        RequiredStack = layer.MaterialStack,
                        DisplayNameSnapshot = string.IsNullOrWhiteSpace(layer.Note) ? "#" + layer.MaterialItemId : layer.Note,
                        LayerKind = layer.LayerKind,
                        Source = "test"
                    });
                }
            }
        }

        private sealed class FakeBlueprintMaterialInventoryReader : IBlueprintMaterialInventoryReader
        {
            private readonly Dictionary<int, int> _mainStacks;
            private readonly Dictionary<int, int> _voidBagStacks;

            public FakeBlueprintMaterialInventoryReader()
                : this(new Dictionary<int, int>(), new Dictionary<int, int>())
            {
            }

            public FakeBlueprintMaterialInventoryReader(Dictionary<int, int> mainStacks, Dictionary<int, int> voidBagStacks)
            {
                _mainStacks = mainStacks ?? new Dictionary<int, int>();
                _voidBagStacks = voidBagStacks ?? new Dictionary<int, int>();
            }

            public int ReadCount { get; private set; }

            public bool TryReadStacks(IReadOnlyCollection<int> requiredItemIds, out BlueprintMaterialInventorySnapshot snapshot, out string message)
            {
                ReadCount++;
                snapshot = new BlueprintMaterialInventorySnapshot
                {
                    Succeeded = true,
                    Status = "fake",
                    Message = "fake inventory"
                };
                message = snapshot.Message;
                foreach (var itemId in requiredItemIds ?? new int[0])
                {
                    int main;
                    if (_mainStacks.TryGetValue(itemId, out main))
                    {
                        snapshot.AddMainStack(itemId, main, "Item " + itemId);
                    }

                    int voidBag;
                    if (_voidBagStacks.TryGetValue(itemId, out voidBag))
                    {
                        snapshot.AddVoidBagStack(itemId, voidBag, "Item " + itemId);
                    }
                }

                return true;
            }
        }
    }
}
