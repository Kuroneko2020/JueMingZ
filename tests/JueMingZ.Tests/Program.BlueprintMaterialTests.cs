using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Hooks;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

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
