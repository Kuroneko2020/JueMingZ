using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Ui;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintAutoPlacementDisabledDoesNotEnqueue()
        {
            BlueprintAutoPlacementService.ResetForTesting();
            try
            {
                var queue = new InputActionQueue();
                var settings = RuntimeSettingsSnapshot.FromSettings(AppSettings.CreateDefault());
                var result = BlueprintAutoPlacementService.TickForTesting(queue, CreateBlueprintInWorldSnapshot(), settings);

                if (result.Submitted ||
                    queue.GetSnapshot().PendingCount != 0 ||
                    !string.Equals(BlueprintAutoPlacementService.GetDiagnostics().ResultCode, "disabled", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected disabled blueprint auto placement to avoid ActionQueue submission.");
                }
            }
            finally
            {
                BlueprintAutoPlacementService.ResetForTesting();
            }
        }

        private static void BlueprintAutoPlacementCandidatesSortAndSkipUnsafeLayers()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-auto-placement-candidates");
            try
            {
                BlueprintAutoPlacementService.ResetForTesting();
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord lower;
                BlueprintWorldInstanceRecord upper;
                BlueprintWorldInstanceRecord conflict;
                BlueprintWorldInstanceRecord fulfilled;
                BlueprintWorldInstanceRecord noMaterial;
                BlueprintWorldInstanceRecord insufficient;
                BlueprintWorldInstanceRecord unsupported;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-candidates", "world-auto-candidates", CreateSingleMaterialTemplate("旧实例", 11, 701, 1), 10, 10, 0, out lower), "create lower auto placement instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-candidates", "world-auto-candidates", CreateSingleMaterialTemplate("新实例", 12, 701, 1), 11, 10, 5, out upper), "create upper auto placement instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-candidates", "world-auto-candidates", CreateSingleMaterialTemplate("冲突", 13, 702, 1), 12, 10, 4, out conflict), "create conflict auto placement instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-candidates", "world-auto-candidates", CreateSingleMaterialTemplate("已完成", 14, 703, 1), 13, 10, 3, out fulfilled), "create fulfilled auto placement instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-candidates", "world-auto-candidates", CreateProjectionTileOnlyTemplate("无材料", 15), 14, 10, 2, out noMaterial), "create no-material auto placement instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-candidates", "world-auto-candidates", CreateSingleMaterialTemplate("材料不足", 16, 704, 5), 15, 10, 1, out insufficient), "create insufficient-material auto placement instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-candidates", "world-auto-candidates", CreateAutoPlacementLayerTemplate("暂不支持物体", BlueprintLayerKinds.Object, 999, 705, 1), 16, 10, 6, out unsupported), "create unsupported auto placement instance");
                reader.Set(12, 10, new BlueprintWorldTileSnapshot { Active = true, TileType = 99 });
                reader.Set(13, 10, new BlueprintWorldTileSnapshot { Active = true, TileType = 14 });
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-auto-candidates", "world-auto-candidates"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int>
                    {
                        { 701, 2 },
                        { 702, 2 },
                        { 703, 2 },
                        { 704, 1 },
                        { 705, 2 }
                    },
                    new Dictionary<int, int>()), true);

                var settings = AppSettings.CreateDefault();
                settings.BlueprintAutoPlacementEnabled = true;
                var result = BlueprintAutoPlacementService.ResolveCandidatesForTesting(RuntimeSettingsSnapshot.FromSettings(settings));
                var snapshot = result.Snapshot;
                if (result.Candidate == null ||
                    snapshot.CandidateCount != 2 ||
                    !string.Equals(result.Candidate.InstanceId, upper.InstanceId, StringComparison.Ordinal) ||
                    snapshot.SkippedConflictLayerCount != 1 ||
                    snapshot.SkippedFulfilledLayerCount != 1 ||
                    snapshot.SkippedUnsupportedLayerCount != 1 ||
                    snapshot.SkippedNoMaterialLayerCount != 1 ||
                    snapshot.SkippedInsufficientMaterialLayerCount != 1)
                {
                    throw new InvalidOperationException("Expected blueprint auto placement to prefer later instances and skip unsafe layers.");
                }

                AssertContains(result.Candidate.AdmissionKey, "blueprint.auto-place|pair-auto-candidates|world-auto-candidates");
            }
            finally
            {
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintAutoPlacementStage14SupportsFurnitureTrackActuatorAndSkipsWire()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-auto-placement-stage14-support");
            try
            {
                BlueprintAutoPlacementService.ResetForTesting();
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord tile;
                BlueprintWorldInstanceRecord wall;
                BlueprintWorldInstanceRecord platform;
                BlueprintWorldInstanceRecord torch;
                BlueprintWorldInstanceRecord rope;
                BlueprintWorldInstanceRecord table;
                BlueprintWorldInstanceRecord door;
                BlueprintWorldInstanceRecord track;
                BlueprintWorldInstanceRecord actuator;
                BlueprintWorldInstanceRecord wire;
                BlueprintWorldInstanceRecord openDoor;
                BlueprintWorldInstanceRecord duplicateFurniture;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("基础方块", BlueprintLayerKinds.Tile, 1, 1001, 1), 1, 1, 0, out tile), "create tile support instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("背景墙", BlueprintLayerKinds.Wall, 2, 1002, 1), 2, 1, 0, out wall), "create wall support instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("平台", BlueprintLayerKinds.Tile, 19, 1003, 1), 3, 1, 0, out platform), "create platform support instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("火把", BlueprintLayerKinds.Object, 4, 1004, 1), 4, 1, 0, out torch), "create torch support instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("绳子", BlueprintLayerKinds.Object, 213, 1005, 1), 5, 1, 0, out rope), "create rope support instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("桌子", BlueprintLayerKinds.Object, 14, 1006, 1), 6, 1, 0, out table), "create table support instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("关门", BlueprintLayerKinds.Object, 10, 1007, 1), 7, 1, 0, out door), "create closed door support instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("轨道", BlueprintLayerKinds.Object, 314, 1008, 1), 8, 1, 0, out track), "create track support instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("制动器", BlueprintLayerKinds.Actuator, 1, 849, 1), 9, 3, 0, out actuator), "create actuator support instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("电线", BlueprintLayerKinds.Wire, 1, 530, 1), 1, 4, 0, out wire), "create wire skip instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementLayerTemplate("开门", BlueprintLayerKinds.Object, 11, 1009, 1), 1, 5, 0, out openDoor), "create open door skip instance");
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-support", "world-auto-support", CreateAutoPlacementTwoCellObjectTemplate("双格家具代表", 14, 1010), 1, 6, 0, out duplicateFurniture), "create multi-cell representative support instance");
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-auto-support", "world-auto-support"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int>
                    {
                        { 1001, 1 },
                        { 1002, 1 },
                        { 1003, 1 },
                        { 1004, 1 },
                        { 1005, 1 },
                        { 1006, 1 },
                        { 1007, 1 },
                        { 1008, 1 },
                        { 849, 1 },
                        { 530, 1 },
                        { 1009, 1 },
                        { 1010, 1 }
                    },
                    new Dictionary<int, int>()), true);

                var settings = AppSettings.CreateDefault();
                settings.BlueprintAutoPlacementEnabled = true;
                var result = BlueprintAutoPlacementService.ResolveCandidatesForTesting(RuntimeSettingsSnapshot.FromSettings(settings));
                if (result.Snapshot.CandidateCount != 10 ||
                    result.Snapshot.SkippedUnsupportedLayerCount != 2 ||
                    result.Snapshot.SkippedNoMaterialLayerCount != 1 ||
                    result.Candidate == null ||
                    result.Candidate.ContentId != 1 ||
                    !string.Equals(result.Candidate.LayerKind, BlueprintLayerKinds.Tile, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage 14 auto placement to support dependency-ordered tile, wall, furniture, track and actuator candidates while skipping wire and unresolved frames.");
                }

                var decorated = new BlueprintProjectionCellSnapshot
                {
                    LayerKind = BlueprintLayerKinds.Tile,
                    ContentId = 1,
                    PaintId = 1
                };
                string reason;
                if (BlueprintAutoPlacementService.IsStage14SupportedLayerForTesting(decorated, out reason) ||
                    !string.Equals(reason, "tileDecorationOrShape", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected decorated tile auto placement to stay outside stage 14.");
                }

                var wireLayer = new BlueprintProjectionCellSnapshot
                {
                    LayerKind = BlueprintLayerKinds.Wire,
                    ContentId = 1
                };
                if (BlueprintAutoPlacementService.IsStage14SupportedLayerForTesting(wireLayer, out reason) ||
                    !string.Equals(reason, "wireRequiresMechanicalTool", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected wire auto placement to stay deferred until a mechanical tool path exists.");
                }
            }
            finally
            {
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintAutoPlacementSubmitsActionQueueAndVerifiesPlacement()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-auto-placement-action");
            try
            {
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintAutoPlaceActionExecutor.SetExecutionDriverForTesting(new FakeBlueprintAutoPlaceExecutionDriver(ItemUseBridgeStatus.AttemptedButUnverified));
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-action", "world-auto-action", CreateSingleMaterialTemplate("动作", 21, 801, 1), 7, 8, 0, out instance), "create auto placement action instance");
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-auto-action", "world-auto-action"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 801, 1 } },
                    new Dictionary<int, int>()), true);

                var settings = AppSettings.CreateDefault();
                settings.BlueprintAutoPlacementEnabled = true;
                var queue = new InputActionQueue();
                var tick = BlueprintAutoPlacementService.TickForTesting(queue, CreateBlueprintInWorldSnapshot(), RuntimeSettingsSnapshot.FromSettings(settings));
                var queueSnapshot = queue.GetSnapshot();
                if (!tick.Submitted ||
                    tick.Request == null ||
                    tick.Request.Kind != InputActionKind.BlueprintAutoPlace ||
                    tick.Request.Priority != InputActionPriority.Low ||
                    !string.Equals(tick.Request.SourceFeatureId, FeatureIds.BlueprintMain, StringComparison.Ordinal) ||
                    queueSnapshot.PendingCount != 1 ||
                    !string.Equals(queueSnapshot.ActionQueueLastAdmissionStatus, "Accepted", StringComparison.Ordinal) ||
                    !string.Equals(queueSnapshot.ActionQueueLastAdmissionScenario, ScenarioNames.BlueprintAutoPlace, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected blueprint auto placement to submit one low-priority ActionQueue request.");
                }

                var profile = InputActionChannelResolver.Resolve(tick.Request);
                AssertHas(profile.RequiredChannels, InputActionChannel.MouseTarget, "blueprint auto placement required");
                AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "blueprint auto placement required");
                AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "blueprint auto placement required");
                AssertHas(profile.RequiredChannels, InputActionChannel.HotbarSelection, "blueprint auto placement required");
                AssertHas(profile.RequiredChannels, InputActionChannel.BridgeItemUse, "blueprint auto placement required");

                queue.Update(CreateBlueprintInWorldSnapshot());
                reader.Set(7, 8, new BlueprintWorldTileSnapshot { Active = true, TileType = 21 });
                queue.Update(CreateBlueprintInWorldSnapshot());
                queueSnapshot = queue.GetSnapshot();
                if (queueSnapshot.PendingCount != 0 ||
                    queueSnapshot.LastResult == null ||
                    queueSnapshot.LastResult.Status != InputActionStatus.Succeeded ||
                    !string.Equals(queueSnapshot.LastResult.ResultCode, DiagnosticResultCode.Succeeded.ToString(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected stage 14 blueprint auto placement executor to verify world projection after bridge use.");
                }

                var diagnostics = BlueprintAutoPlacementService.GetDiagnostics();
                if (diagnostics.SucceededCount != 1 ||
                    !string.Equals(diagnostics.LastResultCode, DiagnosticResultCode.Succeeded.ToString(), StringComparison.Ordinal) ||
                    !string.Equals(diagnostics.ResultCode, "succeeded", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected blueprint auto placement diagnostics to record verified executor success.");
                }

                var repeated = BlueprintAutoPlacementService.TickForTesting(queue, CreateBlueprintInWorldSnapshot(), RuntimeSettingsSnapshot.FromSettings(settings));
                if (repeated.Submitted ||
                    !string.Equals(BlueprintAutoPlacementService.GetDiagnostics().ResultCode, "noCandidate", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected fulfilled projection to stop repeat auto placement submissions.");
                }
            }
            finally
            {
                BlueprintAutoPlaceActionExecutor.SetExecutionDriverForTesting(null);
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintAutoPlacementUsesConfiguredReplacementMaterial()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-auto-placement-replacement");
            try
            {
                ConfigService.Initialize();
                ConfigService.AppSettings.BlueprintAutoPlacementEnabled = true;
                ConfigService.AppSettings.BlueprintReplacementEnabled = true;
                ConfigService.AppSettings.BlueprintReplacementTorchesEnabled = true;
                RegisterReplacementItemForTesting(104, 4, 2);

                BlueprintAutoPlacementService.ResetForTesting();
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-replace", "world-auto-replace", CreateAutoPlacementLayerTemplate("替换火把自动摆放", BlueprintLayerKinds.Object, 4, 1004, 1), 4, 5, 0, out instance), "create auto replacement instance");
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-auto-replace", "world-auto-replace"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 104, 1 } },
                    new Dictionary<int, int>()), true);

                var result = BlueprintAutoPlacementService.ResolveCandidatesForTesting(RuntimeSettingsSnapshot.FromSettings(ConfigService.AppSettings));
                if (result.Candidate == null ||
                    result.Candidate.MaterialItemId != 104 ||
                    result.Candidate.OriginalMaterialItemId != 1004 ||
                    !result.Candidate.ReplacementApplied ||
                    !string.Equals(result.Candidate.ReplacementCategory, BlueprintReplacementCategories.Torch, StringComparison.Ordinal) ||
                    result.Snapshot.SelectedMaterialItemId != 104 ||
                    result.Snapshot.SelectedOriginalMaterialItemId != 1004 ||
                    !result.Snapshot.SelectedReplacementApplied)
                {
                    throw new InvalidOperationException(
                        "Expected blueprint auto placement to choose configured torch replacement material. actual candidate=" +
                        (result.Candidate == null ? "null" : result.Candidate.MaterialItemId + "/" + result.Candidate.OriginalMaterialItemId + "/" + result.Candidate.ReplacementApplied + "/" + result.Candidate.ReplacementCategory) +
                        ", selected=" + result.Snapshot.SelectedMaterialItemId +
                        "/" + result.Snapshot.SelectedOriginalMaterialItemId +
                        "/" + result.Snapshot.SelectedReplacementApplied +
                        "/" + result.Snapshot.SelectedReplacementCategory +
                        ", candidates=" + result.Snapshot.CandidateCount +
                        ", noMaterial=" + result.Snapshot.SkippedNoMaterialLayerCount +
                        ", insufficient=" + result.Snapshot.SkippedInsufficientMaterialLayerCount +
                        ", unsupported=" + result.Snapshot.SkippedUnsupportedLayerCount +
                        ", message=" + result.Snapshot.Message);
                }

                var request = BlueprintAutoPlacementService.BuildRequestForTesting(result.Candidate, "pair-auto-replace", "world-auto-replace");
                if (!string.Equals(request.Metadata["BlueprintContractStage"], "15", StringComparison.Ordinal) ||
                    !string.Equals(request.Metadata[ActionMetadataKeys.BlueprintReplacementApplied], "true", StringComparison.Ordinal) ||
                    !string.Equals(request.Metadata[ActionMetadataKeys.BlueprintReplacementCategory], BlueprintReplacementCategories.Torch, StringComparison.Ordinal) ||
                    request.Metadata[ActionMetadataKeys.BlueprintMaterialItemId] != "104" ||
                    request.Metadata[ActionMetadataKeys.BlueprintOriginalMaterialItemId] != "1004")
                {
                    throw new InvalidOperationException("Expected blueprint auto placement request metadata to describe stage 15 replacement material.");
                }
            }
            finally
            {
                ResetReplacementItemsForTesting();
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintAutoPlacementReplacementFailClosedWhenDisabledOrWrongCategory()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-auto-placement-replacement-fail-closed");
            try
            {
                ConfigService.Initialize();
                ConfigService.AppSettings.BlueprintAutoPlacementEnabled = true;
                ConfigService.AppSettings.BlueprintReplacementEnabled = true;
                ConfigService.AppSettings.BlueprintReplacementTorchesEnabled = false;
                RegisterReplacementItemForTesting(104, 4, 2);

                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-replace-off", "world-auto-replace-off", CreateAutoPlacementLayerTemplate("替换关闭", BlueprintLayerKinds.Object, 4, 1004, 1), 4, 5, 0, out instance), "create disabled replacement instance");
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-auto-replace-off", "world-auto-replace-off"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 104, 1 } },
                    new Dictionary<int, int>()), true);

                var disabled = BlueprintAutoPlacementService.ResolveCandidatesForTesting(RuntimeSettingsSnapshot.FromSettings(ConfigService.AppSettings));
                if (disabled.Candidate != null || disabled.Snapshot.SkippedInsufficientMaterialLayerCount != 1)
                {
                    throw new InvalidOperationException("Expected disabled torch replacement category to avoid using replacement material.");
                }

                ResetReplacementItemsForTesting();
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-auto-replace-off", "world-auto-replace-off"), reader, true);
                ConfigService.AppSettings.BlueprintReplacementTorchesEnabled = true;
                RegisterReplacementItemForTesting(115, 15, 0);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 115, 1 } },
                    new Dictionary<int, int>()), true);

                var wrongCategory = BlueprintAutoPlacementService.ResolveCandidatesForTesting(RuntimeSettingsSnapshot.FromSettings(ConfigService.AppSettings));
                if (wrongCategory.Candidate != null || wrongCategory.Snapshot.SkippedInsufficientMaterialLayerCount != 1)
                {
                    throw new InvalidOperationException("Expected wrong-category replacement material to stay fail-closed.");
                }
            }
            finally
            {
                ResetReplacementItemsForTesting();
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintAutoPlacementDiagnosticsWriteRuntimeSnapshotJson()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-auto-placement-diagnostics");
            try
            {
                BlueprintAutoPlacementService.ResetForTesting();
                var store = new BlueprintWorldInstanceStore();
                var reader = new FakeBlueprintWorldTileReader();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(store.CreateInstanceFromTemplate("pair-auto-diag", "world-auto-diag", CreateSingleMaterialTemplate("诊断", 31, 901, 2), 17, 18, 0, out instance), "create auto placement diagnostic instance");
                BlueprintProjectionService.SetDependenciesForTesting(store, BlueprintPlacementWorldContext.Success("pair-auto-diag", "world-auto-diag"), reader, true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(
                    new Dictionary<int, int> { { 901, 2 } },
                    new Dictionary<int, int>()), true);

                var settings = AppSettings.CreateDefault();
                settings.BlueprintAutoPlacementEnabled = true;
                BlueprintAutoPlacementService.TickForTesting(new InputActionQueue(), CreateBlueprintInWorldSnapshot(), RuntimeSettingsSnapshot.FromSettings(settings));

                var runtimeSnapshot = RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
                {
                    Initialized = true,
                    Version = "test-blueprint-auto-placement"
                });
                var json = InvokeDiagnosticSnapshotJson(runtimeSnapshot);
                AssertContains(json, "\"BlueprintAutoPlacementEnabled\": true");
                AssertContains(json, "\"BlueprintAutoPlacementLastStatus\": \"submitted\"");
                AssertContains(json, "\"BlueprintAutoPlacementCandidateCount\": 1");
                AssertContains(json, "\"BlueprintAutoPlacementSelectedLayerKind\": \"" + BlueprintLayerKinds.Tile + "\"");
                AssertContains(json, "\"BlueprintAutoPlacementSelectedReplacementApplied\": false");
                AssertContains(json, "\"BlueprintAutoPlacementSubmittedCount\": 1");
                AssertContains(json, "\"BlueprintAutoPlacementSkippedUnsupportedLayerCount\": 0");
                AssertContains(json, "\"BlueprintAutoPlacementSucceededCount\": 0");
                AssertContains(BlueprintAutoPlacementService.BuildUiStateJson(), "\"lastAdmissionStatus\":\"Accepted\"");
                AssertContains(LegacyMainWindow.GetBlueprintAutoPlacementVisualContractForTesting(), "stage15");
                AssertContains(LegacyMainWindow.BuildBlueprintAutoPlacementSummaryForTesting(), "自动摆放：候选 1");
            }
            finally
            {
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                restore();
            }
        }

        private static BlueprintTemplateRecord CreateAutoPlacementLayerTemplate(
            string name,
            string layerKind,
            int contentId,
            int materialItemId,
            int materialStack)
        {
            var template = new BlueprintTemplateRecord
            {
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
                        LayerKind = layerKind,
                        ContentId = contentId,
                        MaterialItemId = materialItemId,
                        MaterialStack = materialStack,
                        Note = name + "材料"
                    }
                }
            });
            AddMaterialEntries(template);
            return template;
        }

        private static BlueprintTemplateRecord CreateAutoPlacementTwoCellObjectTemplate(string name, int contentId, int materialItemId)
        {
            var template = new BlueprintTemplateRecord
            {
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
                        LayerKind = BlueprintLayerKinds.Object,
                        ContentId = contentId,
                        MaterialItemId = materialItemId,
                        MaterialStack = 1,
                        Note = name + "代表材料"
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
                        ContentId = contentId,
                        MaterialItemId = materialItemId,
                        MaterialStack = 0,
                        Note = name + "重复格"
                    }
                }
            });
            AddMaterialEntries(template);
            return template;
        }

        private static GameStateSnapshot CreateBlueprintInWorldSnapshot()
        {
            return new GameStateSnapshot
            {
                IsInWorld = true,
                IsInMainMenu = false,
                Ui = new UiStateSnapshot
                {
                    GameInputAvailable = true,
                    HasBlockingUi = false
                }
            };
        }

        private static void RegisterReplacementItemForTesting(int itemId, int createTile, int placeStyle)
        {
            var details = Terraria.ID.ItemID.Sets.DerivedPlacementDetails;
            if (details != null && itemId > 0 && itemId < details.Length)
            {
                details[itemId] = new Terraria.DataStructures.PlacementDetails
                {
                    tileType = createTile,
                    tileStyle = (short)placeStyle
                };
            }

            Terraria.ID.ContentSamples.ItemsByType[itemId] = new Terraria.ID.TestContentSampleItem
            {
                type = itemId,
                createTile = createTile,
                placeStyle = placeStyle
            };
            BlueprintReplacementRuleService.SetCandidateItemIdsForTesting(ResolveReplacementCategoryForTesting(createTile), new[] { itemId });
        }

        private static void ResetReplacementItemsForTesting()
        {
            Terraria.ID.ContentSamples.ItemsByType.Clear();
            Terraria.ID.ItemID.Sets.ResetPlacementDetailsForTesting();
            BlueprintReplacementRuleService.ResetForTesting();
        }

        private static string ResolveReplacementCategoryForTesting(int createTile)
        {
            if (createTile == 4) return BlueprintReplacementCategories.Torch;
            if (createTile == 15) return BlueprintReplacementCategories.Chair;
            if (createTile == 19) return BlueprintReplacementCategories.Platform;
            if (createTile == 18) return BlueprintReplacementCategories.WorkBench;
            if (createTile == 10) return BlueprintReplacementCategories.Door;
            if (createTile == 14) return BlueprintReplacementCategories.Table;
            if (createTile == 21) return BlueprintReplacementCategories.Chest;
            if (createTile == 55) return BlueprintReplacementCategories.Sign;
            return BlueprintReplacementCategories.None;
        }

        private sealed class FakeBlueprintAutoPlaceExecutionDriver : IBlueprintAutoPlaceExecutionDriver
        {
            private readonly ItemUseBridgeStatus _terminalStatus;
            private int _resultCalls;

            public FakeBlueprintAutoPlaceExecutionDriver(ItemUseBridgeStatus terminalStatus)
            {
                _terminalStatus = terminalStatus;
            }

            public bool TryBeginUse(
                InputActionExecution execution,
                BlueprintAutoPlacementCandidate candidate,
                out BlueprintAutoPlaceUsePlan plan,
                out DiagnosticResultCode failureCode,
                out string message)
            {
                failureCode = DiagnosticResultCode.Failed;
                message = "fake bridge queued";
                plan = new BlueprintAutoPlaceUsePlan
                {
                    MaterialSlot = 0,
                    MaterialItemName = "Item " + (candidate == null ? 0 : candidate.MaterialItemId),
                    MaterialStack = candidate == null ? 0 : candidate.MaterialAvailableStack,
                    OriginalSelectedSlot = 0,
                    MouseWorldX = candidate == null ? 0f : candidate.WorldTileX * 16f + 8f,
                    MouseWorldY = candidate == null ? 0f : candidate.WorldTileY * 16f + 8f
                };
                return true;
            }

            public ItemUseBridgeResult GetResult(Guid requestId)
            {
                _resultCalls++;
                if (_resultCalls == 1)
                {
                    return new ItemUseBridgeResult
                    {
                        RequestId = requestId,
                        Status = ItemUseBridgeStatus.WaitingForItemCheck,
                        ResultCode = DiagnosticResultCode.Failed.ToString(),
                        Message = "fake bridge waiting",
                        TargetSlot = 0,
                        OriginalSelectedSlot = 0,
                        SelectedSlotAtUseStart = 0
                    };
                }

                return new ItemUseBridgeResult
                {
                    RequestId = requestId,
                    Status = _terminalStatus,
                    ResultCode = _terminalStatus == ItemUseBridgeStatus.Succeeded
                        ? DiagnosticResultCode.Succeeded.ToString()
                        : DiagnosticResultCode.AttemptedButUnverified.ToString(),
                    Message = "fake bridge terminal",
                    TargetSlot = 0,
                    OriginalSelectedSlot = 0,
                    SelectedSlotAtUseStart = 0,
                    ConsumedByItemCheck = true
                };
            }

            public void Cancel(Guid requestId, string reason)
            {
            }

            public void ReleaseUseItem()
            {
            }

            public BlueprintProjectionSnapshot ForceRefreshProjection()
            {
                return BlueprintProjectionService.ForceRefreshForAutoPlacement();
            }
        }
    }
}
