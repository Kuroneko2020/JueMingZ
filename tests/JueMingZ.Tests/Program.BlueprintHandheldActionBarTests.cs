using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Ui;
using JueMingZ.Input;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintHandheldActionBarVisibilityMatrix()
        {
            var disabled = BuildBlueprintHandheldFrame(false, BlueprintSettings.DefaultToolItemId);
            AssertBlueprintHandheldHidden(disabled, BlueprintHandheldActionBarState.HiddenReasonFeatureDisabled);

            var mismatch = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId + 1);
            AssertBlueprintHandheldHidden(mismatch, BlueprintHandheldActionBarState.HiddenReasonSelectedItemMismatch);

            var unavailable = BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                BlueprintHandheldSettings(true),
                BlueprintHandheldSnapshot(0),
                BlueprintHandheldEnvironment(1280, 720));
            AssertBlueprintHandheldHidden(unavailable, BlueprintHandheldActionBarState.HiddenReasonSelectedItemUnavailable);

            var visible = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            if (!visible.Visible)
            {
                throw new InvalidOperationException("Expected blueprint handheld action bar to show empty-state commands for enabled gel-in-hand world play.");
            }

            AssertBlueprintHandheldButtons(
                visible,
                BlueprintHandheldActionBarState.ButtonIdCreate,
                BlueprintHandheldActionBarState.ButtonIdOpenLibrary);

            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, mapFullscreenOpen: true)),
                BlueprintHandheldActionBarState.HiddenReasonMapFullscreen);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, legacyMainUiVisible: true)),
                BlueprintHandheldActionBarState.HiddenReasonLegacyMainUiVisible);
            AssertBlueprintHandheldVisible(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, gameInputAvailable: false), gameInputAvailable: false),
                "game-input-unavailable display gate");
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, vanillaBlocked: true, vanillaReason: "gameMenu")),
                BlueprintHandheldActionBarState.HiddenReasonVanillaMenuGameMenu);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, vanillaBlocked: true, vanillaReason: "ingameOptionsWindow")),
                BlueprintHandheldActionBarState.HiddenReasonVanillaMenuIngameOptions);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, vanillaBlocked: true, vanillaReason: "inFancyUI")),
                BlueprintHandheldActionBarState.HiddenReasonVanillaMenuFancyUi);

            AssertBlueprintHandheldVisible(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), playerInventoryOpen: true),
                "player-inventory-open display gate");
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), chatOpen: true),
                BlueprintHandheldActionBarState.HiddenReasonChatOpen);
            AssertBlueprintHandheldVisible(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), chestOpen: true),
                "chest-open display gate");
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), npcChatOpen: true),
                BlueprintHandheldActionBarState.HiddenReasonNpcChatOpen);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), isInMainMenu: true, isInWorld: false),
                BlueprintHandheldActionBarState.HiddenReasonWorldNotReady);
        }

        private static void BlueprintHandheldActionBarDynamicButtonMatrix()
        {
            AssertBlueprintHandheldButtons(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId),
                BlueprintHandheldActionBarState.ButtonIdCreate,
                BlueprintHandheldActionBarState.ButtonIdOpenLibrary);

            var placed = BuildBlueprintHandheldFrame(
                true,
                BlueprintSettings.DefaultToolItemId,
                BlueprintHandheldEnvironment(1280, 720, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 2));
            AssertBlueprintHandheldButtons(
                placed,
                BlueprintHandheldActionBarState.ButtonIdCreate,
                BlueprintHandheldActionBarState.ButtonIdOpenLibrary,
                BlueprintHandheldActionBarState.ButtonIdOpenPlacedList,
                BlueprintHandheldActionBarState.ButtonIdClearPlaced,
                BlueprintHandheldActionBarState.ButtonIdMove,
                BlueprintHandheldActionBarState.ButtonIdRegionModify,
                BlueprintHandheldActionBarState.ButtonIdMirror);
            AssertBlueprintHandheldButton(placed, BlueprintHandheldActionBarState.ButtonIdOpenPlacedList, "已放置蓝图列表", "打开当前世界已放置蓝图列表");
            AssertBlueprintHandheldButton(placed, BlueprintHandheldActionBarState.ButtonIdClearPlaced, "清空放置", "清空当前世界已放置蓝图");
            AssertBlueprintHandheldButton(placed, BlueprintHandheldActionBarState.ButtonIdMove, "移动蓝图", "点击蓝图使其进入浮动状态重新放置");
            AssertBlueprintHandheldButton(placed, BlueprintHandheldActionBarState.ButtonIdRegionModify, "区域修改", "修改已放置的蓝图");
            AssertBlueprintHandheldButton(placed, BlueprintHandheldActionBarState.ButtonIdMirror, "镜像", "镜像已放置蓝图");

            AssertBlueprintHandheldButtons(
                BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true)),
                BlueprintHandheldActionBarState.ButtonIdSave,
                BlueprintHandheldActionBarState.ButtonIdExitCreate);
            AssertBlueprintHandheldButtonState(
                BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true)),
                BlueprintHandheldActionBarState.ButtonIdSave,
                "保存蓝图",
                BlueprintHandheldActionBarState.SaveDisabledTooltip,
                false);

            AssertBlueprintHandheldButtons(
                BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true, blueprintCreationHasPendingSelection: true, blueprintCreationSelectedCount: 3)),
                BlueprintHandheldActionBarState.ButtonIdSave,
                BlueprintHandheldActionBarState.ButtonIdExitCreate,
                BlueprintHandheldActionBarState.ButtonIdClearSelection);
            AssertBlueprintHandheldButton(
                BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true, blueprintCreationHasPendingSelection: true, blueprintCreationSelectedCount: 3)),
                BlueprintHandheldActionBarState.ButtonIdClearSelection,
                "清除选区",
                "清除所有选区");

            var exitedWithPreservedMask = BuildBlueprintHandheldFrame(
                true,
                BlueprintSettings.DefaultToolItemId,
                BlueprintHandheldEnvironment(1280, 720, blueprintCreationHasPendingSelection: true, blueprintCreationSelectedCount: 3));
            AssertBlueprintHandheldButtons(
                exitedWithPreservedMask,
                BlueprintHandheldActionBarState.ButtonIdCreate,
                BlueprintHandheldActionBarState.ButtonIdOpenLibrary);

            AssertBlueprintHandheldButtons(
                BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true, blueprintCreationHasPendingSelection: true, blueprintCreationSelectedCount: 3, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 2)),
                BlueprintHandheldActionBarState.ButtonIdSave,
                BlueprintHandheldActionBarState.ButtonIdExitCreate,
                BlueprintHandheldActionBarState.ButtonIdClearSelection);

            AssertBlueprintHandheldButtons(
                BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationCompletedPendingCapture: true, blueprintCreationHasPendingSelection: true, blueprintCreationSelectedCount: 3)),
                BlueprintHandheldActionBarState.ButtonIdSave,
                BlueprintHandheldActionBarState.ButtonIdExitCreate,
                BlueprintHandheldActionBarState.ButtonIdClearSelection);
        }

        private static void BlueprintHandheldActionBarUsesEffectiveProjectionForPlacedState()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-handheld-effective-projection");
            try
            {
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();

                var store = new BlueprintWorldInstanceStore();
                var context = BlueprintPlacementWorldContext.Success("pair-handheld-effective", "world-handheld-effective");
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    store.CreateInstanceFromTemplate(
                        "pair-handheld-effective",
                        "world-handheld-effective",
                        CreateProjectionTileOnlyTemplate("全擦空状态", 77),
                        10,
                        20,
                        0,
                        out instance),
                    "create effective-projection handheld instance");

                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(store, context, true);
                BlueprintEraseRegionState.SetDependenciesForTesting(store, context);
                BlueprintProjectionService.SetDependenciesForTesting(store, context, new FakeBlueprintWorldTileReader(), true);
                BlueprintMaterialService.SetInventoryReaderForTesting(new FakeBlueprintMaterialInventoryReader(), true);
                BlueprintProjectionService.GetSnapshot();

                var beforeEnvironment = BlueprintHandheldEnvironment(1280, 720);
                BlueprintHandheldActionBarOverlay.PopulateDynamicBlueprintStateForTesting(beforeEnvironment);
                if (!beforeEnvironment.BlueprintHasPlacedInstances)
                {
                    throw new InvalidOperationException("Expected visible projection layers to expose placed blueprint handheld commands.");
                }

                var erase = EraseOneCell(instance.InstanceId, 10, 20);
                if (!erase.ErasedRegion || erase.ErasedCellCount != 1)
                {
                    throw new InvalidOperationException("Expected the single visible projection cell to be fully erased.");
                }

                BlueprintPlacedInstanceUiState.NotifyInstancesChanged(instance.InstanceId);
                var projection = BlueprintProjectionService.GetDiagnostics();
                if (!projection.LoadSucceeded ||
                    projection.InstanceCount != 1 ||
                    projection.EffectiveLayerCount != 0)
                {
                    throw new InvalidOperationException("Expected erased instance record to remain while effective projection layers drop to zero.");
                }

                var afterEnvironment = BlueprintHandheldEnvironment(1280, 720);
                BlueprintHandheldActionBarOverlay.PopulateDynamicBlueprintStateForTesting(afterEnvironment);
                if (afterEnvironment.BlueprintHasPlacedInstances)
                {
                    throw new InvalidOperationException("Expected fully erased placed instance to stop exposing placed-management handheld state.");
                }

                var activeFrame = BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                    BlueprintHandheldSettings(true),
                    BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                    afterEnvironment);
                AssertBlueprintHandheldButtons(
                    activeFrame,
                    BlueprintHandheldActionBarState.ButtonIdCreate,
                    BlueprintHandheldActionBarState.ButtonIdOpenLibrary,
                    BlueprintHandheldActionBarState.ButtonIdRegionModify);
                AssertBlueprintHandheldButton(activeFrame, BlueprintHandheldActionBarState.ButtonIdRegionModify, "取消修改", "取消蓝图修改");

                BlueprintEraseRegionState.Cancel();
                var idleFrame = BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                    BlueprintHandheldSettings(true),
                    BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                    afterEnvironment);
                AssertBlueprintHandheldButtons(
                    idleFrame,
                    BlueprintHandheldActionBarState.ButtonIdCreate,
                    BlueprintHandheldActionBarState.ButtonIdOpenLibrary);
            }
            finally
            {
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintHandheldActionBarLayoutKeepsButtonsStable()
        {
            var frame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(320, 180));
            if (!frame.Visible)
            {
                throw new InvalidOperationException("Expected compact blueprint handheld action bar to remain visible.");
            }

            if (frame.Bounds.X < 0 || frame.Bounds.Right > 320 || frame.Bounds.Y < 0 || frame.Bounds.Bottom > 180)
            {
                throw new InvalidOperationException("Blueprint handheld action bar bounds must stay inside the screen.");
            }

            var previousRight = frame.Bounds.X;
            for (var index = 0; index < frame.Buttons.Count; index++)
            {
                var button = frame.Buttons[index];
                if (button.Rect.X < previousRight ||
                    button.Rect.Right > frame.Bounds.Right ||
                    button.Rect.Y < frame.Bounds.Y ||
                    button.Rect.Bottom > frame.Bounds.Bottom)
                {
                    throw new InvalidOperationException("Blueprint handheld buttons must not overlap or escape the bar bounds.");
                }

                var hit = BlueprintHandheldActionBarState.HitTest(frame, button.Rect.CenterX, button.Rect.CenterY);
                AssertStringEquals(hit, button.Id, "handheld action bar hit-test button " + index);
                previousRight = button.Rect.Right;
            }

            var repeated = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(320, 180));
            AssertStringEquals(repeated.LayoutSignature, frame.LayoutSignature, "handheld action bar layout signature");
        }

        private static void BlueprintHandheldActionBarVisualStyleUsesLegacyThemeAndStableTextScale()
        {
            var contract = BlueprintHandheldActionBarOverlay.GetVisualContractForTesting();
            AssertContains(contract, "physical-screen-bottom-action-bar");
            AssertContains(contract, "legacy-ui-theme");
            AssertContains(contract, "vanilla-ui-skin");
            AssertContains(contract, "button-text-scale-0.78");
            AssertContains(contract, "notice-text-scale-0.78");

            if (Math.Abs(BlueprintHandheldActionBarOverlay.ButtonTextScale - 0.78f) > 0.001f)
            {
                throw new InvalidOperationException("Expected blueprint handheld button text scale to be old 0.58 + 0.2.");
            }

            if (Math.Abs(BlueprintHandheldActionBarOverlay.NoticeTextScale - 0.78f) > 0.001f)
            {
                throw new InvalidOperationException("Expected blueprint handheld status notice text scale to match the enlarged button text scale.");
            }

            var normal = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720));
            foreach (var button in normal.Buttons)
            {
                var available = Math.Max(1, button.Rect.Width - 8);
                var scale = BlueprintHandheldActionBarOverlay.ResolveButtonTextScaleForTesting(button.Label, available);
                if (Math.Abs(scale - BlueprintHandheldActionBarOverlay.ButtonTextScale) > 0.001f)
                {
                    throw new InvalidOperationException("Expected normal blueprint handheld button text to use the enlarged base scale.");
                }

                if (UiTextRenderer.EstimateTextWidth(button.Label, scale) > available)
                {
                    throw new InvalidOperationException("Expected normal blueprint handheld button text to fit inside fixed geometry.");
                }
            }

            var compact = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(320, 180));
            AssertStringEquals(compact.LayoutSignature, BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(320, 180)).LayoutSignature, "compact handheld text scale must not mutate layout");
            foreach (var button in compact.Buttons)
            {
                var available = Math.Max(1, button.Rect.Width - 8);
                var scale = BlueprintHandheldActionBarOverlay.ResolveButtonTextScaleForTesting(button.Label, available);
                if (scale > BlueprintHandheldActionBarOverlay.ButtonTextScale)
                {
                    throw new InvalidOperationException("Expected compact blueprint handheld text scale never to grow beyond the enlarged base scale.");
                }

                if (UiTextRenderer.EstimateTextWidth(button.Label, scale) > available)
                {
                    throw new InvalidOperationException("Expected compact blueprint handheld button text to shrink before it overflows.");
                }
            }
        }

        private static void BlueprintHandheldActionBarStage04ButtonHitBoundsMatchVisibleRects()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var empty = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            AssertBlueprintHandheldButtons(
                empty,
                BlueprintHandheldActionBarState.ButtonIdCreate,
                BlueprintHandheldActionBarState.ButtonIdOpenLibrary);
            AssertBlueprintHandheldButtonHitBounds(empty, "empty two-button handheld bar");
            AssertBlueprintHandheldPanelGapConsumesWithoutCommand(empty, "empty two-button handheld bar");

            var outside = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                empty,
                BlueprintHandheldPointer(empty.Bounds.X - 1, empty.Bounds.CenterY, true, 0, true, true, "Test/Stage04Outside"));
            if (outside.ShouldCaptureMouse || outside.ShouldConsumeLeftInput || outside.Clicked)
            {
                throw new InvalidOperationException("Expected handheld click just outside the visual panel to pass through.");
            }

            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var placed = BuildBlueprintHandheldFrame(
                true,
                BlueprintSettings.DefaultToolItemId,
                BlueprintHandheldEnvironment(1280, 720, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 2));
            AssertBlueprintHandheldButtons(
                placed,
                BlueprintHandheldActionBarState.ButtonIdCreate,
                BlueprintHandheldActionBarState.ButtonIdOpenLibrary,
                BlueprintHandheldActionBarState.ButtonIdOpenPlacedList,
                BlueprintHandheldActionBarState.ButtonIdClearPlaced,
                BlueprintHandheldActionBarState.ButtonIdMove,
                BlueprintHandheldActionBarState.ButtonIdRegionModify,
                BlueprintHandheldActionBarState.ButtonIdMirror);
            AssertBlueprintHandheldButtonHitBounds(placed, "placed seven-button handheld bar");
            AssertBlueprintHandheldPanelGapConsumesWithoutCommand(placed, "placed seven-button handheld bar");
        }

        private static void BlueprintHandheldActionBarStage04NoticeTimingAndScale()
        {
            var contract = BlueprintHandheldActionBarOverlay.GetVisualContractForTesting();
            AssertContains(contract, "stage04-active-status-notice");
            AssertContains(contract, "stage04-after-player-input-cache");
            AssertContains(contract, "notice-text-scale-0.78");

            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintPlacedInstanceTransformState.ResetForTesting();
            BlueprintEraseRegionState.ResetForTesting();
            BlueprintPlacementPreviewState.ResetForTesting();

            BlueprintHandheldActionBarState.RecordCommandResultClick(
                BlueprintHandheldActionBarState.ButtonIdOpenLibrary,
                BlueprintSettings.DefaultToolItemId,
                true,
                "libraryOpened",
                "蓝图库已打开。");
            var idleFrame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            AssertStringEquals(BlueprintHandheldActionBarOverlay.ResolveNoticeForTesting(idleFrame), string.Empty, "idle handheld notice after one-shot command");

            var openLibrary = FindBlueprintHandheldButton(idleFrame, BlueprintHandheldActionBarState.ButtonIdOpenLibrary);
            BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                idleFrame,
                BlueprintHandheldPointer(openLibrary.Rect.CenterX, openLibrary.Rect.CenterY, false, 0, true, true, "Test/Stage04Hover"));
            var hoveredFrame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            AssertStringEquals(BlueprintHandheldActionBarOverlay.ResolveNoticeForTesting(hoveredFrame), "打开蓝图库", "hovered handheld notice");

            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            BlueprintCreationMaskState.BeginCreate();
            var creatingFrame = BuildBlueprintHandheldFrame(
                true,
                BlueprintSettings.DefaultToolItemId,
                BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true));
            AssertContains(BlueprintHandheldActionBarOverlay.ResolveNoticeForTesting(creatingFrame), "创建中");

            var restore = PushTemporaryConfigDirectory("blueprint-handheld-stage04-status");
            try
            {
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintPlacedInstanceTransformState.ResetForTesting();
                BlueprintEraseRegionState.ResetForTesting();
                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate(
                        "pair-handheld-stage04-status",
                        "world-handheld-stage04-status",
                        CreateSingleMaterialTemplate("状态提示", 21, 501, 1),
                        10,
                        20,
                        0,
                        out instance),
                    "create handheld stage04 status instance");
                var context = BlueprintPlacementWorldContext.Success("pair-handheld-stage04-status", "world-handheld-stage04-status");
                BlueprintPlacedInstanceTransformState.SetDependenciesForTesting(instanceStore, context);
                BlueprintEraseRegionState.SetDependenciesForTesting(instanceStore, context);

                var moveStatus = BlueprintPlacedInstanceTransformState.BeginMove();
                if (!moveStatus.Succeeded)
                {
                    throw new InvalidOperationException("Expected handheld stage04 move status to start: " + moveStatus.ResultCode + " " + moveStatus.Message);
                }

                var moveFrame = BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 1));
                AssertContains(BlueprintHandheldActionBarOverlay.ResolveNoticeForTesting(moveFrame), "请点击一个已放置蓝图作为移动目标");

                var mirrorStatus = BlueprintPlacedInstanceTransformState.BeginMirror();
                if (!mirrorStatus.Succeeded)
                {
                    throw new InvalidOperationException("Expected handheld stage04 mirror status to start: " + mirrorStatus.ResultCode + " " + mirrorStatus.Message);
                }

                var mirrorFrame = BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 1));
                AssertContains(BlueprintHandheldActionBarOverlay.ResolveNoticeForTesting(mirrorFrame), "请点击一个已放置蓝图进行镜像");

                BlueprintPlacedInstanceTransformState.ResetForTesting();
                var eraseStatus = BlueprintEraseRegionState.BeginErase(string.Empty);
                if (!eraseStatus.Succeeded)
                {
                    throw new InvalidOperationException("Expected handheld stage04 region status to start: " + eraseStatus.ResultCode + " " + eraseStatus.Message);
                }

                var eraseFrame = BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 1));
                AssertContains(BlueprintHandheldActionBarOverlay.ResolveNoticeForTesting(eraseFrame), "正在修改已放置蓝图区域");
            }
            finally
            {
                BlueprintPlacedInstanceTransformState.ResetForTesting();
                BlueprintEraseRegionState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintHandheldActionBarStage04MouseReaderCachesPrefixAndPostfixSeparately()
        {
            ResetUiInputFrameTestState();
            DiagnosticMouseStateReader.ResetForTesting();
            try
            {
                UiInputFrameClock.BeginUpdateFrame("test.blueprint-handheld-stage04-cache");
                var prefix = DiagnosticMouseStateReader.ReadForBlueprintHandheldActionBarOverlay();
                var prefixAgain = DiagnosticMouseStateReader.ReadForBlueprintHandheldActionBarOverlay();
                var afterPlayerInput = DiagnosticMouseStateReader.ReadForBlueprintHandheldActionBarOverlayAfterPlayerInput();
                var afterPlayerInputAgain = DiagnosticMouseStateReader.ReadForBlueprintHandheldActionBarOverlayAfterPlayerInput();

                if (!object.ReferenceEquals(prefix, prefixAgain))
                {
                    throw new InvalidOperationException("Expected blueprint handheld prefix mouse reader to keep a same-frame cache slot.");
                }

                if (object.ReferenceEquals(prefix, afterPlayerInput))
                {
                    throw new InvalidOperationException("Expected blueprint handheld after-PlayerInput mouse reader to use a separate same-frame cache slot.");
                }

                if (!object.ReferenceEquals(afterPlayerInput, afterPlayerInputAgain))
                {
                    throw new InvalidOperationException("Expected blueprint handheld after-PlayerInput mouse reader to cache inside its own phase slot.");
                }
            }
            finally
            {
                DiagnosticMouseStateReader.ResetForTesting();
                ResetUiInputFrameTestState();
            }
        }

        private static void BlueprintHandheldActionBarOverlayStaysUiOnlyAndNoScan()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-handheld-no-scan");
            BlueprintProjectionService.ResetForTesting();
            BlueprintMaterialService.ResetForTesting();
            BlueprintLibraryUiState.ResetForTesting();
            try
            {
                BlueprintLibraryUiState.SetStoreForTesting(new BlueprintTemplateLibraryStore(), true);
                var projectionBefore = BlueprintProjectionService.BuildStateSignature();
                var materialBefore = BlueprintMaterialService.BuildStateSignature();
                var libraryBefore = BlueprintLibraryUiState.BuildStateSignature();

                var frame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
                if (!frame.Visible)
                {
                    throw new InvalidOperationException("Expected blueprint handheld frame to be visible for no-scan contract test.");
                }

                var projectionAfter = BlueprintProjectionService.BuildStateSignature();
                var materialAfter = BlueprintMaterialService.BuildStateSignature();
                var libraryAfter = BlueprintLibraryUiState.BuildStateSignature();
                AssertIntEquals(projectionAfter, projectionBefore, "blueprint handheld projection signature");
                AssertIntEquals(materialAfter, materialBefore, "blueprint handheld material signature");
                AssertIntEquals(libraryAfter, libraryBefore, "blueprint handheld library signature");

                if (!BlueprintHandheldActionBarOverlay.ShouldRegisterUiOverlayForTesting())
                {
                    throw new InvalidOperationException("Blueprint handheld action bar must register through the UI overlay dispatcher.");
                }

                if (!BlueprintHandheldActionBarOverlay.ShouldRegisterInputGuardsForTesting())
                {
                    throw new InvalidOperationException("Blueprint handheld action bar must register prefix and after-PlayerInput input guards.");
                }

                var contract = BlueprintHandheldActionBarOverlay.GetVisualContractForTesting();
                AssertContains(contract, "physical-screen-bottom-action-bar");
                AssertContains(contract, "dynamic-buttons");
                AssertContains(contract, "create-enters-mask");
                AssertContains(contract, "save-captures-mask");
                AssertContains(contract, "clear-selection");
                AssertContains(contract, "open-library-real");
                AssertContains(contract, "open-placed-list-real");
                AssertContains(contract, "stage03-deferred-placed-commands");
                AssertContains(contract, "no-blueprint-refresh");
                AssertContains(contract, "no-library-refresh");
                AssertContains(contract, "mouse-consume");
                AssertContains(contract, "no-input-action-queue");
                AssertContains(contract, "legacy-ui-theme");
                AssertDoesNotContain(contract, "InputActionQueue");
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintHandheldActionBarInputCapturesOnlyInsideBar()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var frame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            var button = frame.Buttons[0];
            var press = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true));
            if (!press.ShouldCaptureMouse ||
                !press.ShouldConsumeLeftInput ||
                !press.Clicked ||
                !string.Equals(press.HoveredButtonId, button.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected handheld action bar button press to capture, consume, and click once.");
            }

            var pressedFrame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            AssertStringEquals(pressedFrame.HoveredButtonId, button.Id, "handheld hovered button after press");
            AssertStringEquals(pressedFrame.PressedButtonId, button.Id, "handheld pressed button after press");
            AssertStringEquals(pressedFrame.LastMouseReadMode, "Test/BlueprintHandheldPointer", "handheld press read mode");
            AssertStringEquals(pressedFrame.LastOwnershipReason, BlueprintHandheldActionBarState.PointerOwnershipReasonLeft, "handheld press ownership reason");

            var release = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, false, 0, true));
            if (release.Clicked)
            {
                throw new InvalidOperationException("Expected handheld action bar release not to enqueue a second click.");
            }

            var gap = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(frame.Bounds.X + 1, frame.Bounds.CenterY, false, 120, true));
            if (!gap.ShouldCaptureMouse || !gap.ShouldConsumeScroll || gap.Clicked)
            {
                throw new InvalidOperationException("Expected handheld action bar panel hover to capture scroll without clicking a button.");
            }

            var gapFrame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            AssertStringEquals(gapFrame.LastOwnershipReason, BlueprintHandheldActionBarState.PointerOwnershipReasonScroll, "blank handheld panel scroll ownership reason");

            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var outside = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(0, 0, true, 0, true));
            if (outside.ShouldCaptureMouse || outside.ShouldConsumeLeftInput || outside.Clicked)
            {
                throw new InvalidOperationException("Expected handheld action bar outside click to pass through.");
            }

            var outsideInteraction = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
            AssertStringEquals(outsideInteraction.LastMouseReadMode, string.Empty, "outside handheld click must not claim read mode");
            AssertStringEquals(outsideInteraction.LastOwnershipReason, string.Empty, "outside handheld click must not claim ownership reason");

            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var disabledFrame = BuildBlueprintHandheldFrame(
                true,
                BlueprintSettings.DefaultToolItemId,
                BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true));
            var disabledSave = disabledFrame.Buttons[0];
            if (disabledSave.Enabled)
            {
                throw new InvalidOperationException("Expected empty creation save button to be disabled.");
            }

            var disabledPress = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                disabledFrame,
                BlueprintHandheldPointer(disabledSave.Rect.CenterX, disabledSave.Rect.CenterY, true, 0, true));
            if (!disabledPress.ShouldCaptureMouse ||
                !disabledPress.ShouldConsumeLeftInput ||
                disabledPress.Clicked ||
                !string.Equals(disabledPress.HoveredButtonId, BlueprintHandheldActionBarState.ButtonIdSave, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected disabled handheld save to capture input and hover without submitting a click.");
            }

            var disabledInteraction = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
            AssertStringEquals(disabledInteraction.PressedButtonId, string.Empty, "disabled save must not enter pressed state");
            AssertStringEquals(disabledInteraction.LastMouseReadMode, "Test/BlueprintHandheldPointer", "disabled save read mode");
            AssertStringEquals(disabledInteraction.LastOwnershipReason, BlueprintHandheldActionBarState.PointerOwnershipReasonLeft, "disabled save ownership reason");
        }

        private static void BlueprintHandheldActionBarAfterPlayerInputGuardSubmitsFreshClickEdge()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var frame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            var button = frame.Buttons[0];

            var prefixWithoutClick = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, false, 0, true, false));
            if (!prefixWithoutClick.ShouldCaptureMouse ||
                prefixWithoutClick.ShouldConsumeLeftInput ||
                prefixWithoutClick.Clicked)
            {
                throw new InvalidOperationException("Expected prefix guard without a left edge to capture hover without submitting a handheld command.");
            }

            var afterPlayerInput = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true, true));
            if (!afterPlayerInput.ShouldCaptureMouse ||
                !afterPlayerInput.ShouldConsumeLeftInput ||
                !afterPlayerInput.Clicked ||
                !string.Equals(afterPlayerInput.HoveredButtonId, button.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected after-PlayerInput guard to submit the fresh handheld button click edge.");
            }

            var repeatedAfterPlayerInput = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true, true));
            if (repeatedAfterPlayerInput.Clicked)
            {
                throw new InvalidOperationException("Expected held after-PlayerInput handheld click to submit only once.");
            }
        }

        private static void BlueprintHandheldActionBarPostfixReplaysStalePrefixPress()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var frame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            var button = frame.Buttons[0];

            var stalePrefix = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(0, 0, true, 0, true, false));
            if (stalePrefix.Clicked || stalePrefix.ShouldCaptureMouse || stalePrefix.ShouldConsumeLeftInput)
            {
                throw new InvalidOperationException("Expected stale prefix coordinates outside the handheld bar to defer the left edge instead of clicking.");
            }

            var afterPlayerInput = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true, true));
            if (!afterPlayerInput.ShouldCaptureMouse ||
                !afterPlayerInput.ShouldConsumeLeftInput ||
                !afterPlayerInput.Clicked ||
                !string.Equals(afterPlayerInput.HoveredButtonId, button.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected PlayerInput postfix to replay the prefix left edge once after Terraria refreshed the handheld button coordinates.");
            }

            var repeatedAfterPlayerInput = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true, true));
            if (repeatedAfterPlayerInput.Clicked)
            {
                throw new InvalidOperationException("Expected replayed handheld postfix edge to be single-use while the button remains held.");
            }
        }

        private static void BlueprintHandheldActionBarGateClosedMouseKeepsTerrariaClick()
        {
            var scale = LegacyMainUiScale.ResolveForTesting(1d, 1280, 720);
            var raw = new DiagnosticMouseState
            {
                GameInputAvailable = false,
                TerrariaReadAvailable = true,
                TerrariaMouseX = 640,
                TerrariaMouseY = 650,
                TerrariaLeftDown = true,
                TerrariaScrollWheelAvailable = true,
                ScrollDelta = -120,
                OsReadAvailable = true,
                OsClientMouseX = 32,
                OsClientMouseY = 48,
                OsLeftDown = true,
                UiScaleAvailable = true,
                UiScaleMatrixAvailable = true,
                UiScale = 1.35d,
                UiScaleX = 1.35d,
                UiScaleY = 1.35d,
                UiTranslateX = 9d,
                UiTranslateY = 11d,
                UiScaleSource = "UIScaleMatrix",
                ReadMode = "Terraria+OsClient/BlueprintHandheldOverlayGateBypass"
            };

            var mouse = LegacyUiInput.ReadMouseForBlueprintHandheldOverlay(raw, scale);
            if (!mouse.ReadAvailable ||
                !mouse.LeftDown ||
                mouse.ScrollDelta != -120 ||
                mouse.X != 640 ||
                mouse.Y != 650 ||
                mouse.ReadMode.IndexOf("TerrariaRaw", StringComparison.Ordinal) < 0 ||
                mouse.ReadMode.IndexOf("ScreenToUi", StringComparison.Ordinal) >= 0 ||
                mouse.ReadMode.IndexOf("InterfaceOverlay", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected handheld overlay mouse to keep Terraria in-process click and scroll when the global input gate is closed.");
            }

            raw.TerrariaLeftDown = false;
            raw.OsLeftDown = true;
            var osFallback = LegacyUiInput.ReadMouseForBlueprintHandheldOverlay(raw, scale);
            if (osFallback.LeftDown)
            {
                throw new InvalidOperationException("Expected handheld overlay gate bypass not to restore OS physical mouse fallback when global input is unavailable.");
            }
        }

        private static void BlueprintHandheldActionBarGateOpenMousePrefersOsClientCoordinate()
        {
            var scale = LegacyMainUiScale.ResolveForTesting(1d, 1280, 720);
            var raw = new DiagnosticMouseState
            {
                GameInputAvailable = true,
                TerrariaReadAvailable = true,
                TerrariaMouseX = 12,
                TerrariaMouseY = 24,
                TerrariaLeftDown = false,
                OsReadAvailable = true,
                OsClientMouseX = 640,
                OsClientMouseY = 650,
                OsLeftDown = true,
                UiScaleAvailable = true,
                UiScaleMatrixAvailable = true,
                UiScale = 1.4d,
                UiScaleX = 1.4d,
                UiScaleY = 1.4d,
                UiTranslateX = 17d,
                UiTranslateY = 19d,
                UiScaleSource = "UIScaleMatrix",
                ReadMode = "Terraria+OsClient"
            };

            var mouse = LegacyUiInput.ReadMouseForBlueprintHandheldOverlay(raw, scale);
            if (!mouse.ReadAvailable ||
                !mouse.LeftDown ||
                mouse.X != 640 ||
                mouse.Y != 650 ||
                mouse.ReadMode.IndexOf("OsClientScreen", StringComparison.Ordinal) < 0 ||
                mouse.ReadMode.IndexOf("ScreenToUi", StringComparison.Ordinal) >= 0 ||
                mouse.ReadMode.IndexOf("InterfaceOverlay", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected handheld overlay to prefer fresh OS client physical coordinates while the game input gate is open.");
            }
        }

        private static void BlueprintHandheldActionBarPhysicalBottomCenterRejectsUiScaleLogicalExtent()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var physicalWidth = 2560;
            var physicalHeight = 1400;
            var uiScale = 1.25d;
            var translateX = 12d;
            var translateY = 8d;
            var raw = new DiagnosticMouseState
            {
                GameInputAvailable = true,
                TerrariaReadAvailable = true,
                TerrariaMouseX = 0,
                TerrariaMouseY = 0,
                TerrariaLeftDown = true,
                OsReadAvailable = true,
                OsLeftDown = true,
                UiScaleAvailable = true,
                UiScaleMatrixAvailable = true,
                UiScale = uiScale,
                UiScaleX = uiScale,
                UiScaleY = uiScale,
                UiTranslateX = translateX,
                UiTranslateY = translateY,
                UiScaleSource = "UIScaleMatrix",
                ReadMode = "Terraria+OsClient"
            };

            var frame = BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                BlueprintHandheldSettings(true),
                BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                BlueprintHandheldEnvironment(physicalWidth, physicalHeight),
                raw);
            var expectedLogicalWidth = (int)Math.Round((physicalWidth - translateX) / uiScale);
            var expectedLogicalHeight = (int)Math.Round((physicalHeight - translateY) / uiScale);
            var expectedLogicalCenterX = expectedLogicalWidth / 2;
            AssertBlueprintHandheldPhysicalLayout(frame, physicalWidth, physicalHeight, "2560x1400 physical bottom-center with UI-scale matrix");

            // The old 0.912 false positive used physical / UI scale as the visible layout target.
            var oldLogicalBottomTop = Math.Max(0, expectedLogicalHeight - 34 - 48);
            var oldLogicalBottom = Math.Max(0, expectedLogicalHeight - 34);
            if (frame.Bounds.Y <= oldLogicalBottomTop ||
                frame.Bounds.Bottom <= oldLogicalBottom ||
                frame.Bounds.CenterX == expectedLogicalCenterX ||
                frame.ScreenWidth == expectedLogicalWidth ||
                frame.ScreenHeight == expectedLogicalHeight)
            {
                throw new InvalidOperationException("Expected handheld frame to reject the old UI-scale logical extent false positive and use physical screen bottom-center.");
            }

            var button = frame.Buttons[0];
            raw.OsClientMouseX = button.Rect.CenterX;
            raw.OsClientMouseY = button.Rect.CenterY;
            var mouse = LegacyUiInput.ReadMouseForBlueprintHandheldOverlay(
                raw,
                LegacyMainUiScale.ResolveForScreen(raw, physicalWidth, physicalHeight));
            AssertIntEquals(mouse.X, button.Rect.CenterX, "physical OS handheld mouse X");
            AssertIntEquals(mouse.Y, button.Rect.CenterY, "physical OS handheld mouse Y");
            AssertContains(mouse.ReadMode, "OsClientScreen");
            AssertContains(mouse.ReadMode, "UIScaleMatrix");
            AssertContains(mouse.ReadMode, "InterfaceOverlay");
            AssertDoesNotContain(mouse.ReadMode, "ScreenToUi");
            AssertStringEquals(BlueprintHandheldActionBarState.HitTest(frame, mouse.X, mouse.Y), button.Id, "physical handheld hit-test");

            var oldLogicalMouseX = (int)Math.Round((button.Rect.CenterX - translateX) / uiScale);
            var oldLogicalMouseY = (int)Math.Round((button.Rect.CenterY - translateY) / uiScale);
            var stalePrefix = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(oldLogicalMouseX, oldLogicalMouseY, true, 0, true, false, "Test/OldLogicalPrefix"));
            if (stalePrefix.Clicked || stalePrefix.ShouldCaptureMouse || stalePrefix.ShouldConsumeLeftInput)
            {
                throw new InvalidOperationException("Expected old UI-scale logical prefix coordinates to stay outside the physical handheld frame.");
            }

            var afterPlayerInput = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(mouse.X, mouse.Y, true, 0, true, true, mouse.ReadMode));
            if (!afterPlayerInput.ShouldCaptureMouse ||
                !afterPlayerInput.ShouldConsumeLeftInput ||
                !afterPlayerInput.Clicked ||
                !string.Equals(afterPlayerInput.HoveredButtonId, button.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected physical after-PlayerInput mouse coordinate to replay and click the visible handheld button.");
            }

            var repeatedAfterPlayerInput = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(mouse.X, mouse.Y, true, 0, true, true, mouse.ReadMode));
            if (repeatedAfterPlayerInput.Clicked)
            {
                throw new InvalidOperationException("Expected physical prefix replay to remain single-use.");
            }

            var baseline1080 = BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                BlueprintHandheldSettings(true),
                BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                BlueprintHandheldEnvironment(1920, 1080));
            raw.UiScale = 1.5d;
            raw.UiScaleX = 1.5d;
            raw.UiScaleY = 1.5d;
            raw.UiTranslateX = -10d;
            raw.UiTranslateY = 16d;
            var scaled1080 = BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                BlueprintHandheldSettings(true),
                BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                BlueprintHandheldEnvironment(1920, 1080),
                raw);
            AssertBlueprintHandheldPhysicalLayout(scaled1080, 1920, 1080, "1920x1080 UI-scale matrix frame");
            AssertStringEquals(scaled1080.LayoutSignature, baseline1080.LayoutSignature, "UI scale must not rewrite physical handheld layout");

            var nonWide = BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                BlueprintHandheldSettings(true),
                BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                BlueprintHandheldEnvironment(1440, 900),
                raw);
            AssertBlueprintHandheldPhysicalLayout(nonWide, 1440, 900, "1440x900 physical frame");

            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var blank = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(frame.Bounds.X + 1, frame.Bounds.CenterY, true, 0, true, true, "Test/PhysicalScreenFrame"));
            if (!blank.ShouldCaptureMouse || !blank.ShouldConsumeLeftInput || blank.Clicked)
            {
                throw new InvalidOperationException("Expected physical handheld blank panel area to consume without submitting a command.");
            }

            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var disabledFrame = BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                BlueprintHandheldSettings(true),
                BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                BlueprintHandheldEnvironment(physicalWidth, physicalHeight, blueprintCreationActive: true),
                raw);
            AssertBlueprintHandheldPhysicalLayout(disabledFrame, physicalWidth, physicalHeight, "disabled 2560x1400 physical frame");
            var disabledSave = disabledFrame.Buttons[0];
            var disabledPress = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                disabledFrame,
                BlueprintHandheldPointer(disabledSave.Rect.CenterX, disabledSave.Rect.CenterY, true, 0, true, true, "Test/PhysicalScreenFrame"));
            if (!disabledPress.ShouldCaptureMouse ||
                !disabledPress.ShouldConsumeLeftInput ||
                disabledPress.Clicked ||
                !string.Equals(disabledPress.HoveredButtonId, BlueprintHandheldActionBarState.ButtonIdSave, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected physical disabled save button to hover and consume without a command.");
            }

            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            raw.OsReadAvailable = false;
            raw.OsClientMouseX = -1;
            raw.OsClientMouseY = -1;
            raw.TerrariaMouseX = button.Rect.CenterX;
            raw.TerrariaMouseY = button.Rect.CenterY;
            raw.ReadMode = "TerrariaOnly";
            var terrariaMouse = LegacyUiInput.ReadMouseForBlueprintHandheldOverlay(
                raw,
                LegacyMainUiScale.ResolveForScreen(raw, physicalWidth, physicalHeight));
            AssertIntEquals(terrariaMouse.X, button.Rect.CenterX, "physical Terraria fallback handheld mouse X");
            AssertIntEquals(terrariaMouse.Y, button.Rect.CenterY, "physical Terraria fallback handheld mouse Y");
            AssertContains(terrariaMouse.ReadMode, "TerrariaRaw");
            AssertDoesNotContain(terrariaMouse.ReadMode, "ScreenToUi");
            var terrariaPress = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(terrariaMouse.X, terrariaMouse.Y, true, 0, true, true, terrariaMouse.ReadMode));
            if (!terrariaPress.ShouldCaptureMouse ||
                !terrariaPress.ShouldConsumeLeftInput ||
                !terrariaPress.Clicked ||
                !string.Equals(terrariaPress.HoveredButtonId, button.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected Terraria in-process physical coordinate to hit the same visual handheld button when OS fallback is absent.");
            }
        }

        private static void BlueprintHandheldActionBarDisplayGatesStayVisibleAndUiOnly()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var inventory = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), playerInventoryOpen: true);
            var chest = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), chestOpen: true);
            var noInput = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, gameInputAvailable: false), gameInputAvailable: false);
            AssertBlueprintHandheldVisible(inventory, "player inventory display gate");
            AssertBlueprintHandheldVisible(chest, "chest display gate");
            AssertBlueprintHandheldVisible(noInput, "game input unavailable display gate");

            var button = inventory.Buttons[0];
            var limitedPress = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                inventory,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true, true));
            if (!limitedPress.ShouldCaptureMouse || !limitedPress.ShouldConsumeLeftInput || !limitedPress.Clicked)
            {
                throw new InvalidOperationException("Expected visible display-gate handheld bar press to submit its UI command while still staying outside InputActionQueue.");
            }

            var wheel = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                chest,
                BlueprintHandheldPointer(chest.Bounds.CenterX, chest.Bounds.CenterY, false, -120, true, true));
            if (!wheel.ShouldCaptureMouse || !wheel.ShouldConsumeScroll || wheel.Clicked)
            {
                throw new InvalidOperationException("Expected chest-visible handheld bar hover to consume wheel without submitting a command.");
            }

            var interaction = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
            AssertStringEquals(interaction.LastResultCode, string.Empty, "display gate must not submit a command");
        }

        private static void BlueprintHandheldActionBarRealCommandsAndDeferredPlacedCommands()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-handheld-real-commands");
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            BlueprintEntryState.ResetForTesting();
            BlueprintCreationMaskState.ResetForTesting();
            try
            {
                var settings = AppSettings.CreateDefault();
                var store = new BlueprintTemplateLibraryStore();
                var reader = new FakeBlueprintWorldTileReader();
                reader.Set(5, 6, new BlueprintWorldTileSnapshot
                {
                    Active = true,
                    TileType = 2,
                    TileMaterialItemId = 101,
                    TileDisplayName = "Dirt Block"
                });
                BlueprintCaptureService.SetCaptureDependenciesForTesting(reader, store);
                BlueprintLibraryUiState.SetStoreForTesting(store, true);

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdCreate, "创建蓝图"));
                var createInteraction = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(createInteraction.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdCreate, "handheld create clicked id");
                AssertStringEquals(createInteraction.LastResultCode, "entryStateChanged", "handheld create command result");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Creating, "handheld create entry mode");
                if (!BlueprintCreationMaskState.GetSnapshot().Active)
                {
                    throw new InvalidOperationException("Expected handheld create action to enter blueprint creation mask mode.");
                }

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdSave, "保存蓝图"));
                var failedSave = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(failedSave.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdSave, "handheld empty save clicked id");
                AssertStringEquals(failedSave.LastResultCode, "emptySelection", "handheld empty save result");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Creating, "handheld failed save keeps creating mode");
                if (!BlueprintCreationMaskState.GetSnapshot().Active)
                {
                    throw new InvalidOperationException("Expected failed handheld save without selection to keep creation mask active.");
                }

                ClickTileForBlueprintCreation(5, 6);
                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdExitCreate, "退出创建"));
                var exited = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(exited.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdExitCreate, "handheld exit-create clicked id");
                AssertStringEquals(exited.LastResultCode, "entryStateChanged", "handheld exit-create command result");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Tool, "handheld exit-create returns tool mode");
                var exitedMask = BlueprintCreationMaskState.GetSnapshot();
                if (exitedMask.Active ||
                    exitedMask.CompletedPendingCapture ||
                    exitedMask.SelectedCount != 1 ||
                    !HasBlueprintCell(exitedMask, 5, 6))
                {
                    throw new InvalidOperationException("Expected handheld exit-create to preserve the selected mask without saving or clearing it.");
                }

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdCreate, "创建蓝图"));
                var resumedMask = BlueprintCreationMaskState.GetSnapshot();
                if (!resumedMask.Active ||
                    resumedMask.SelectedCount != 1 ||
                    !HasBlueprintCell(resumedMask, 5, 6))
                {
                    throw new InvalidOperationException("Expected handheld create to resume the mask preserved by exit-create.");
                }

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdClearSelection, "清除选区"));
                var cleared = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(cleared.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdClearSelection, "handheld clear-selection clicked id");
                AssertStringEquals(cleared.LastResultCode, "selectionCleared", "handheld clear-selection command result");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Creating, "handheld clear-selection keeps create mode");
                var clearedMask = BlueprintCreationMaskState.GetSnapshot();
                if (!clearedMask.Active ||
                    clearedMask.SelectedCount != 0 ||
                    clearedMask.CompletedPendingCapture)
                {
                    throw new InvalidOperationException("Expected handheld clear-selection to empty only the pending creation mask.");
                }

                BlueprintTemplateLibrarySnapshot clearedStore;
                RequireBlueprintSuccess(store.TryLoad(out clearedStore), "load blueprint handheld clear-selection result");
                if (clearedStore.Templates.Count != 0)
                {
                    throw new InvalidOperationException("Expected handheld clear-selection not to create, delete, or modify blueprint templates.");
                }

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdSave, "保存蓝图"));
                var saveAfterClear = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(saveAfterClear.LastResultCode, "emptySelection", "handheld save after clear-selection result");

                ClickTileForBlueprintCreation(5, 6);
                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdSave, "保存蓝图"));
                var saved = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(saved.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdSave, "handheld save clicked id");
                AssertStringEquals(saved.LastResultCode, "templateSaved", "handheld save command result");

                BlueprintTemplateLibrarySnapshot snapshot;
                RequireBlueprintSuccess(store.TryLoad(out snapshot), "load blueprint handheld save result");
                if (snapshot.Templates.Count != 1 || snapshot.Templates[0].Cells.Count != 1)
                {
                    throw new InvalidOperationException("Expected handheld save command to write one captured blueprint template.");
                }

                AssertStringEquals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Tool, "handheld successful save returns to tool mode");
                if (BlueprintCreationMaskState.GetSnapshot().CompletedPendingCapture)
                {
                    throw new InvalidOperationException("Expected successful handheld save to clear pending mask.");
                }

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdOpenLibrary, "打开蓝图库"));
                var openLibrary = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(openLibrary.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdOpenLibrary, "handheld open-library clicked id");
                AssertStringEquals(openLibrary.LastResultCode, "libraryOpened", "handheld open-library command result");

                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintWorldInstanceRecord placedInstance;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate("pair-handheld-03", "world-handheld-03", snapshot.Templates[0], 7, 8, 0, out placedInstance),
                    "create handheld stage 03 placed instance");
                var placedContext = BlueprintPlacementWorldContext.Success("pair-handheld-03", "world-handheld-03");
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(
                    instanceStore,
                    placedContext,
                    true);
                BlueprintEraseRegionState.SetDependenciesForTesting(instanceStore, placedContext);
                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdOpenPlacedList, "已放置蓝图列表"));
                var openPlaced = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(openPlaced.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdOpenPlacedList, "handheld open-placed clicked id");
                AssertStringEquals(openPlaced.LastResultCode, "placedManagementOpened", "handheld open-placed command result");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.PlacedManagement, "handheld open-placed entry mode");
                var placedSnapshot = BlueprintPlacedInstanceUiState.GetSnapshot();
                if (!LegacyMainUiState.Visible ||
                    !string.Equals(LegacyMainUiState.SelectedPageId, "blueprint", StringComparison.Ordinal) ||
                    !placedSnapshot.LoadSucceeded ||
                    placedSnapshot.Instances.Count != 1)
                {
                    throw new InvalidOperationException("Expected handheld open-placed command to reveal the current-world placed blueprint list.");
                }

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdRegionModify, "区域修改"));
                var regionModify = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(regionModify.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdRegionModify, "handheld region-modify clicked id");
                AssertStringEquals(regionModify.LastResultCode, "eraseStartedSingleTarget", "handheld region-modify starts erase mode");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.EraseRegion, "handheld region-modify entry mode");
                var eraseSnapshot = BlueprintEraseRegionState.GetSnapshot();
                if (!eraseSnapshot.Active ||
                    !eraseSnapshot.HasFixedTarget ||
                    !string.Equals(eraseSnapshot.TargetInstanceId, placedInstance.InstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected handheld region-modify to preselect the single visible placed instance for trimming.");
                }

                var regionFrame = BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                    BlueprintHandheldSettings(true),
                    BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                    BlueprintHandheldEnvironment(1280, 720, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 1));
                AssertBlueprintHandheldButton(regionFrame, BlueprintHandheldActionBarState.ButtonIdRegionModify, "取消修改", "取消蓝图修改");
                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdRegionModify, "取消修改"));
                var regionCancel = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(regionCancel.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdRegionModify, "handheld region-modify cancel clicked id");
                AssertStringEquals(regionCancel.LastResultCode, "eraseCancelled", "handheld region-modify cancel result");
                if (BlueprintEraseRegionState.GetSnapshot().Active ||
                    !string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Tool, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected handheld region-modify cancel button to stop only the shared region modify state.");
                }

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdRegionModify, "区域修改"));

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdClearPlaced, "清空放置"));
                var clearPlaced = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(clearPlaced.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdClearPlaced, "handheld clear-placed clicked id");
                AssertStringEquals(clearPlaced.LastResultCode, "clearPlaced", "handheld clear-placed command result");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Tool, "handheld clear-placed returns to tool mode after erase");
                if (BlueprintEraseRegionState.GetSnapshot().Active)
                {
                    throw new InvalidOperationException("Expected clear-placed to cancel active region-modify erase mode.");
                }

                BlueprintWorldInstanceSnapshot clearedInstances;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-handheld-03", out clearedInstances), "load stage 06 clear-placed instances");
                if (clearedInstances.Instances.Count != 0 || BlueprintPlacedInstanceUiState.GetCachedSummary().InstanceCount != 0)
                {
                    throw new InvalidOperationException("Expected handheld clear-placed to clear current-world placed blueprint instances and cached summary.");
                }

                BlueprintTemplateLibrarySnapshot templatesAfterClear;
                RequireBlueprintSuccess(store.TryLoad(out templatesAfterClear), "load templates after handheld clear-placed");
                if (templatesAfterClear.Templates.Count != 1)
                {
                    throw new InvalidOperationException("Expected handheld clear-placed not to delete blueprint library templates.");
                }

                BlueprintWorldInstanceRecord transformInstance;
                RequireBlueprintSuccess(
                    instanceStore.CreateInstanceFromTemplate("pair-handheld-03", "world-handheld-03", templatesAfterClear.Templates[0], 17, 18, 0, out transformInstance),
                    "create handheld stage 07 transform instance");
                BlueprintPlacedInstanceTransformState.SetDependenciesForTesting(instanceStore, placedContext);
                BlueprintPlacedInstanceUiState.SetDependenciesForTesting(
                    instanceStore,
                    placedContext,
                    true);

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdMove, "移动蓝图"));
                var moveStart = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(moveStart.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdMove, "handheld move clicked id");
                AssertStringEquals(moveStart.LastResultCode, "moveTargetSelectStarted", "handheld move starts transform mode");
                AssertStringEquals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.PlacedManagement, "handheld move enters placed management mode");
                var moveTransform = BlueprintPlacedInstanceTransformState.GetSnapshot();
                if (!moveTransform.Active ||
                    !string.Equals(moveTransform.Mode, BlueprintPlacedInstanceTransformModes.Move, StringComparison.Ordinal) ||
                    !string.Equals(moveTransform.Phase, BlueprintPlacedInstanceTransformPhases.SelectTarget, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected handheld move to wait for a placed-instance world click.");
                }

                AssertBlueprintHandheldButton(
                    BuildBlueprintHandheldFrame(
                        true,
                        BlueprintSettings.DefaultToolItemId,
                        BlueprintHandheldEnvironment(1280, 720, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 1)),
                    BlueprintHandheldActionBarState.ButtonIdMove,
                    "取消移动",
                    "取消移动并回到原位置");

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdMirror, "镜像"));
                var mirrorStart = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(mirrorStart.LastClickedButtonId, BlueprintHandheldActionBarState.ButtonIdMirror, "handheld mirror clicked id");
                AssertStringEquals(mirrorStart.LastResultCode, "mirrorTargetSelectStarted", "handheld mirror starts transform mode");
                var mirrorTransform = BlueprintPlacedInstanceTransformState.GetSnapshot();
                if (!mirrorTransform.Active ||
                    !string.Equals(mirrorTransform.Mode, BlueprintPlacedInstanceTransformModes.Mirror, StringComparison.Ordinal) ||
                    !string.Equals(mirrorTransform.Phase, BlueprintPlacedInstanceTransformPhases.SelectTarget, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected handheld mirror to wait for a placed-instance world click.");
                }

                AssertBlueprintHandheldButton(
                    BuildBlueprintHandheldFrame(
                        true,
                        BlueprintSettings.DefaultToolItemId,
                        BlueprintHandheldEnvironment(1280, 720, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 1)),
                    BlueprintHandheldActionBarState.ButtonIdMirror,
                    "取消镜像",
                    "取消镜像选择");

                BlueprintWorldInstanceSnapshot unchangedInstances;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-handheld-03", out unchangedInstances), "load stage 07 transform start instances");
                if (unchangedInstances.Instances.Count != 1 ||
                    FindPlacedInstance(unchangedInstances, transformInstance.InstanceId).OriginTileX != 17)
                {
                    throw new InvalidOperationException("Expected stage 07 handheld move/mirror button clicks not to mutate placed blueprint instances before a world target click.");
                }
            }
            finally
            {
                BlueprintCaptureService.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintPlacedInstanceTransformState.ResetForTesting();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                LegacyMainUiState.SetVisible(false);
                restore();
            }
        }

        private static void BlueprintHandheldActionBarDiagnosticsSnapshotJson()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            BlueprintUiClickDiagnostics.ResetForTesting();
            var frame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            var button = frame.Buttons[0];
            var pointer = BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true, false, "Test/BlueprintHandheldOverlay");
            var interaction = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(frame, pointer);
            BlueprintUiClickDiagnostics.RecordHandheldInput("TestPrefix", frame, pointer, interaction);
            BlueprintUiClickDiagnostics.RecordWorldOverlayInput(
                "creation",
                "after-player-input",
                true,
                new DiagnosticMouseState
                {
                    ReadMode = "TestWorld",
                    GameInputAvailable = true,
                    TerrariaLeftDown = true,
                    OsLeftDown = true
                },
                false,
                false,
                true,
                false,
                false,
                true,
                12,
                34);
            LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(new LegacyUiCommand
            {
                ElementId = BlueprintHandheldActionBarState.BuildCommandElementId("create"),
                Label = "创建蓝图",
                Kind = "button",
                IntValue = BlueprintSettings.DefaultToolItemId,
                MouseCaptured = true,
                MouseReadMode = "Test/BlueprintHandheldOverlay",
                Rect = new LegacyUiRect(10, 20, 80, 24)
            });

            var visible = BlueprintHandheldActionBarState.BuildDiagnostics(
                BlueprintHandheldSettings(true),
                BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                BlueprintHandheldEnvironment(1280, 720));
            if (!visible.Visible)
            {
                throw new InvalidOperationException("Expected handheld diagnostics to report a visible action bar.");
            }

            AssertStringEquals(visible.BlockedReason, string.Empty, "handheld diagnostics visible blocked reason");
            AssertIntEquals(visible.ToolItemId, BlueprintSettings.DefaultToolItemId, "handheld diagnostics tool item");
            AssertIntEquals(visible.SelectedItemType, BlueprintSettings.DefaultToolItemId, "handheld diagnostics selected item");
            AssertStringEquals(visible.LastAction, "create", "handheld diagnostics last action");
            AssertStringEquals(visible.LastResultCode, "entryStateChanged", "handheld diagnostics result");
            AssertStringEquals(visible.HoveredButtonId, "create", "handheld diagnostics hovered button");
            AssertStringEquals(visible.PressedButtonId, "create", "handheld diagnostics pressed button");
            AssertStringEquals(visible.LastMouseReadMode, "Test/BlueprintHandheldOverlay", "handheld diagnostics mouse read mode");
            AssertStringEquals(visible.LastOwnershipReason, BlueprintHandheldActionBarState.PointerOwnershipReasonLeft, "handheld diagnostics ownership reason");
            var uiClick = BlueprintUiClickDiagnostics.GetSnapshot();
            AssertContains(uiClick.HandheldInputTrace, "source=TestPrefix");
            AssertContains(uiClick.HandheldInputTrace, "frameVisible=true");
            AssertContains(uiClick.HandheldInputTrace, "hovered=create");
            AssertContains(uiClick.HandheldInputTrace, "clicked=true");
            AssertContains(uiClick.HandheldOwnershipTrace, "ownerId=blueprint-handheld-action-bar:create");
            AssertContains(uiClick.HandheldOwnershipTrace, "leftConsumed=true");
            AssertContains(uiClick.WorldOverlayInputTrace, "overlay=creation");
            AssertContains(uiClick.WorldOverlayInputTrace, "pointerUiOwned=true");
            AssertContains(uiClick.WorldOverlayInputTrace, "resolvedLeft=false");

            var hidden = BlueprintHandheldActionBarState.BuildDiagnostics(
                BlueprintHandheldSettings(false),
                BlueprintHandheldSnapshot(BlueprintSettings.DefaultToolItemId),
                BlueprintHandheldEnvironment(1280, 720));
            if (hidden.Visible)
            {
                throw new InvalidOperationException("Expected disabled handheld diagnostics to report hidden.");
            }

            AssertStringEquals(hidden.BlockedReason, BlueprintHandheldActionBarState.HiddenReasonFeatureDisabled, "handheld diagnostics hidden reason");

            var snapshot = new DiagnosticSnapshot
            {
                BlueprintHandheldActionBarVisible = visible.Visible,
                BlueprintHandheldActionBarBlockedReason = visible.BlockedReason,
                BlueprintHandheldActionBarToolItemId = visible.ToolItemId,
                BlueprintHandheldActionBarSelectedItemType = visible.SelectedItemType,
                BlueprintHandheldActionBarLastAction = visible.LastAction,
                BlueprintHandheldActionBarLastResultCode = visible.LastResultCode,
                BlueprintHandheldActionBarHoveredButtonId = visible.HoveredButtonId,
                BlueprintHandheldActionBarPressedButtonId = visible.PressedButtonId,
                BlueprintHandheldActionBarLastMouseReadMode = visible.LastMouseReadMode,
                BlueprintHandheldActionBarLastOwnershipReason = visible.LastOwnershipReason,
                BlueprintHandheldActionBarLastInputTrace = uiClick.HandheldInputTrace,
                BlueprintHandheldActionBarLastOwnershipTrace = uiClick.HandheldOwnershipTrace,
                BlueprintWorldOverlayLastInputTrace = uiClick.WorldOverlayInputTrace
            };
            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"BlueprintHandheldActionBarVisible\": true");
            AssertContains(json, "\"BlueprintHandheldActionBarBlockedReason\": \"\"");
            AssertContains(json, "\"BlueprintHandheldActionBarToolItemId\": 23");
            AssertContains(json, "\"BlueprintHandheldActionBarSelectedItemType\": 23");
            AssertContains(json, "\"BlueprintHandheldActionBarLastAction\": \"create\"");
            AssertContains(json, "\"BlueprintHandheldActionBarLastResultCode\": \"entryStateChanged\"");
            AssertContains(json, "\"BlueprintHandheldActionBarHoveredButtonId\": \"create\"");
            AssertContains(json, "\"BlueprintHandheldActionBarPressedButtonId\": \"create\"");
            AssertContains(json, "\"BlueprintHandheldActionBarLastMouseReadMode\": \"Test/BlueprintHandheldOverlay\"");
            AssertContains(json, "\"BlueprintHandheldActionBarLastOwnershipReason\": \"left\"");
            AssertContains(json, "\"BlueprintHandheldActionBarLastInputTrace\": \"source=TestPrefix;");
            AssertContains(json, "\"BlueprintHandheldActionBarLastOwnershipTrace\": \"registered=true;");
            AssertContains(json, "\"BlueprintWorldOverlayLastInputTrace\": \"overlay=creation;");
            BlueprintEntryState.ResetForTesting();
            BlueprintCreationMaskState.ResetForTesting();
            BlueprintUiClickDiagnostics.ResetForTesting();
        }

        private static void BlueprintHandheldActionBarHiddenClearsInputState()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var visible = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
            var button = visible.Buttons[0];
            BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                visible,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true));

            var hidden = BuildBlueprintHandheldFrame(false, BlueprintSettings.DefaultToolItemId);
            AssertBlueprintHandheldHidden(hidden, BlueprintHandheldActionBarState.HiddenReasonFeatureDisabled);
            var blocked = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                hidden,
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, true));
            if (blocked.ShouldCaptureMouse || blocked.ShouldConsumeLeftInput || blocked.Clicked)
            {
                throw new InvalidOperationException("Expected hidden handheld action bar to ignore old button coordinates.");
            }

            var interaction = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
            AssertStringEquals(interaction.HoveredButtonId, string.Empty, "hidden handheld hover clear");
            AssertStringEquals(interaction.PressedButtonId, string.Empty, "hidden handheld press clear");
            AssertStringEquals(interaction.LastClickedButtonId, string.Empty, "hidden handheld click clear");
            AssertStringEquals(interaction.LastNotice, string.Empty, "hidden handheld notice clear");
            AssertStringEquals(interaction.LastMouseReadMode, string.Empty, "hidden handheld read mode clear");
            AssertStringEquals(interaction.LastOwnershipReason, string.Empty, "hidden handheld ownership reason clear");
        }

        private static LegacyUiCommand BuildBlueprintHandheldCommand(string buttonId, string label)
        {
            return new LegacyUiCommand
            {
                ElementId = BlueprintHandheldActionBarState.BuildCommandElementId(buttonId),
                Label = label,
                Kind = "button",
                IntValue = BlueprintSettings.DefaultToolItemId,
                MouseCaptured = true,
                MouseReadMode = "Test/BlueprintHandheldCommand",
                Rect = new LegacyUiRect(10, 20, 80, 24)
            };
        }

        private static void AssertBlueprintHandheldDeferredCommand(string buttonId, string label, string expectedStage)
        {
            LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(buttonId, label));
            var interaction = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
            AssertStringEquals(interaction.LastClickedButtonId, buttonId, "handheld deferred clicked id");
            AssertStringEquals(interaction.LastClickedButtonLabel, label, "handheld deferred clicked label");
            AssertStringEquals(interaction.LastResultCode, BlueprintHandheldActionBarState.ResultCodeEntryWiredDeferred, "handheld deferred command result");
            AssertIntEquals(interaction.LastHeldItemType, BlueprintSettings.DefaultToolItemId, "handheld deferred held item type");
            AssertContains(interaction.LastNotice, label);
            AssertContains(interaction.LastNotice, "入口已接线");
            AssertContains(interaction.LastNotice, expectedStage + " 阶段");
            AssertDoesNotContain(interaction.LastNotice, "暂未接入");
        }

        private static BlueprintHandheldActionBarFrame BuildBlueprintHandheldFrame(
            bool enabled,
            int selectedItemType,
            BlueprintHandheldActionBarEnvironment environment = null,
            bool playerInventoryOpen = false,
            bool chatOpen = false,
            bool chestOpen = false,
            bool npcChatOpen = false,
            bool gameInputAvailable = true,
            bool isInMainMenu = false,
            bool isInWorld = true)
        {
            return BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                BlueprintHandheldSettings(enabled),
                BlueprintHandheldSnapshot(selectedItemType, playerInventoryOpen, chatOpen, chestOpen, npcChatOpen, gameInputAvailable, isInMainMenu, isInWorld),
                environment ?? BlueprintHandheldEnvironment(1280, 720));
        }

        private static RuntimeSettingsSnapshot BlueprintHandheldSettings(bool enabled)
        {
            var settings = AppSettings.CreateDefault();
            settings.BlueprintHandheldEntryEnabled = enabled;
            settings.BlueprintToolItemId = BlueprintSettings.DefaultToolItemId;
            return RuntimeSettingsSnapshot.FromSettings(settings);
        }

        private static GameStateSnapshot BlueprintHandheldSnapshot(
            int selectedItemType,
            bool playerInventoryOpen = false,
            bool chatOpen = false,
            bool chestOpen = false,
            bool npcChatOpen = false,
            bool gameInputAvailable = true,
            bool isInMainMenu = false,
            bool isInWorld = true)
        {
            return new GameStateSnapshot
            {
                TerrariaDetected = true,
                IsInMainMenu = isInMainMenu,
                IsInWorld = isInWorld,
                Inventory = new InventorySnapshot
                {
                    SelectedItemSlot = selectedItemType > 0 ? 0 : -1,
                    SelectedItem = new InventoryItemSnapshot
                    {
                        SlotIndex = selectedItemType > 0 ? 0 : -1,
                        Type = selectedItemType,
                        Stack = selectedItemType > 0 ? 1 : 0,
                        Name = selectedItemType.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }
                },
                Ui = new UiStateSnapshot
                {
                    GameInputAvailable = gameInputAvailable,
                    PlayerInventoryOpen = playerInventoryOpen,
                    ChatOpen = chatOpen,
                    ChestOpen = chestOpen,
                    NpcChatOpen = npcChatOpen
                }
            };
        }

        private static BlueprintHandheldActionBarEnvironment BlueprintHandheldEnvironment(
            int screenWidth,
            int screenHeight,
            bool worldReady = true,
            bool gameInputAvailable = true,
            bool mapFullscreenOpen = false,
            bool legacyMainUiVisible = false,
            bool vanillaReadAvailable = true,
            bool vanillaBlocked = false,
            string vanillaReason = "",
            bool blueprintCreationActive = false,
            bool blueprintCreationHasPendingSelection = false,
            bool blueprintCreationCompletedPendingCapture = false,
            int blueprintCreationSelectedCount = 0,
            bool blueprintHasPlacedInstances = false,
            int blueprintPlacedInstanceCount = 0)
        {
            return new BlueprintHandheldActionBarEnvironment
            {
                WorldReady = worldReady,
                GameInputAvailable = gameInputAvailable,
                MapFullscreenOpen = mapFullscreenOpen,
                LegacyMainUiVisible = legacyMainUiVisible,
                VanillaMenuReadAvailable = vanillaReadAvailable,
                VanillaMenuBlocked = vanillaBlocked,
                VanillaMenuReason = vanillaReason ?? string.Empty,
                BlueprintCreationActive = blueprintCreationActive,
                BlueprintCreationHasPendingSelection = blueprintCreationHasPendingSelection,
                BlueprintCreationCompletedPendingCapture = blueprintCreationCompletedPendingCapture,
                BlueprintCreationSelectedCount = blueprintCreationSelectedCount,
                BlueprintHasPlacedInstances = blueprintHasPlacedInstances,
                BlueprintPlacedInstanceCount = blueprintPlacedInstanceCount,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight
            };
        }

        private static BlueprintHandheldActionBarPointerInput BlueprintHandheldPointer(
            int mouseX,
            int mouseY,
            bool leftDown,
            int scrollDelta,
            bool allowCommand,
            bool afterPlayerInput = false,
            string mouseReadMode = "Test/BlueprintHandheldPointer")
        {
            return new BlueprintHandheldActionBarPointerInput
            {
                MouseX = mouseX,
                MouseY = mouseY,
                LeftDown = leftDown,
                ScrollDelta = scrollDelta,
                MouseReadMode = mouseReadMode,
                ReadAvailable = true,
                AllowCommand = allowCommand,
                AfterPlayerInput = afterPlayerInput
            };
        }

        private static void AssertBlueprintHandheldPhysicalLayout(
            BlueprintHandheldActionBarFrame frame,
            int screenWidth,
            int screenHeight,
            string context)
        {
            if (frame == null || !frame.Visible)
            {
                throw new InvalidOperationException("Expected visible blueprint handheld action bar before checking physical layout for " + context + ".");
            }

            const int expectedBottomMargin = 34;
            const int expectedPanelHeight = 48;
            AssertIntEquals(frame.ScreenWidth, screenWidth, "handheld physical screen width " + context);
            AssertIntEquals(frame.ScreenHeight, screenHeight, "handheld physical screen height " + context);
            AssertIntEquals(frame.Bounds.CenterX, screenWidth / 2, "handheld physical center X " + context);
            AssertIntEquals(frame.Bounds.Y, Math.Max(0, screenHeight - expectedBottomMargin - expectedPanelHeight), "handheld physical top " + context);
            AssertIntEquals(frame.Bounds.Bottom, Math.Max(expectedPanelHeight, screenHeight - expectedBottomMargin), "handheld physical bottom " + context);
            if (frame.Bounds.X < 0 ||
                frame.Bounds.Right > screenWidth ||
                frame.Bounds.Bottom > screenHeight)
            {
                throw new InvalidOperationException("Expected blueprint handheld action bar to stay inside physical screen bounds for " + context + ".");
            }
        }

        private static void AssertBlueprintHandheldButtonHitBounds(BlueprintHandheldActionBarFrame frame, string context)
        {
            AssertBlueprintHandheldVisible(frame, context);
            for (var index = 0; index < frame.Buttons.Count; index++)
            {
                var button = frame.Buttons[index];
                var rect = button.Rect;
                AssertStringEquals(BlueprintHandheldActionBarState.HitTest(frame, rect.X, rect.CenterY), button.Id, context + " left edge " + button.Id);
                AssertStringEquals(BlueprintHandheldActionBarState.HitTest(frame, rect.CenterX, rect.CenterY), button.Id, context + " center " + button.Id);
                AssertStringEquals(BlueprintHandheldActionBarState.HitTest(frame, rect.Right - 1, rect.CenterY), button.Id, context + " right inside " + button.Id);
                AssertStringEquals(BlueprintHandheldActionBarState.HitTest(frame, rect.CenterX, rect.Y), button.Id, context + " top edge " + button.Id);
                AssertStringEquals(BlueprintHandheldActionBarState.HitTest(frame, rect.CenterX, rect.Bottom - 1), button.Id, context + " bottom inside " + button.Id);

                var rightExclusive = BlueprintHandheldActionBarState.HitTest(frame, rect.Right, rect.CenterY);
                if (string.Equals(rightExclusive, button.Id, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected handheld button right edge to be exclusive for " + context + " " + button.Id + ".");
                }

                var bottomExclusive = BlueprintHandheldActionBarState.HitTest(frame, rect.CenterX, rect.Bottom);
                if (string.Equals(bottomExclusive, button.Id, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected handheld button bottom edge to be exclusive for " + context + " " + button.Id + ".");
                }
            }
        }

        private static void AssertBlueprintHandheldPanelGapConsumesWithoutCommand(BlueprintHandheldActionBarFrame frame, string context)
        {
            AssertBlueprintHandheldVisible(frame, context);
            for (var index = 0; index < frame.Buttons.Count - 1; index++)
            {
                var left = frame.Buttons[index];
                var right = frame.Buttons[index + 1];
                if (right.Rect.X <= left.Rect.Right)
                {
                    continue;
                }

                var gapX = left.Rect.Right + (right.Rect.X - left.Rect.Right) / 2;
                AssertStringEquals(BlueprintHandheldActionBarState.HitTest(frame, gapX, frame.Bounds.CenterY), string.Empty, context + " gap hit " + index);
                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                var gap = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                    frame,
                    BlueprintHandheldPointer(gapX, frame.Bounds.CenterY, true, 0, true, true, "Test/Stage04Gap"));
                if (!gap.ShouldCaptureMouse || !gap.ShouldConsumeLeftInput || gap.Clicked)
                {
                    throw new InvalidOperationException("Expected handheld panel gap to consume left without submitting a command for " + context + ".");
                }
            }
        }

        private static BlueprintHandheldActionBarButtonFrame FindBlueprintHandheldButton(BlueprintHandheldActionBarFrame frame, string buttonId)
        {
            if (frame == null || frame.Buttons == null)
            {
                throw new InvalidOperationException("Expected blueprint handheld frame before finding button " + buttonId + ".");
            }

            for (var index = 0; index < frame.Buttons.Count; index++)
            {
                var button = frame.Buttons[index];
                if (button != null && string.Equals(button.Id, buttonId, StringComparison.Ordinal))
                {
                    return button;
                }
            }

            throw new InvalidOperationException("Expected blueprint handheld button " + buttonId + " to exist.");
        }

        private static void AssertBlueprintHandheldHidden(BlueprintHandheldActionBarFrame frame, string expectedReason)
        {
            if (frame == null || frame.Visible)
            {
                throw new InvalidOperationException("Expected blueprint handheld action bar to be hidden.");
            }

            AssertStringEquals(frame.HiddenReason, expectedReason, "blueprint handheld hidden reason");
        }

        private static void AssertBlueprintHandheldVisible(BlueprintHandheldActionBarFrame frame, string context)
        {
            if (frame == null || !frame.Visible)
            {
                throw new InvalidOperationException("Expected blueprint handheld action bar to be visible for " + context + ".");
            }

            AssertStringEquals(frame.HiddenReason, BlueprintHandheldActionBarState.HiddenReasonNone, "blueprint handheld visible reason " + context);
            if (frame.Buttons.Count <= 0)
            {
                throw new InvalidOperationException("Expected visible blueprint handheld action bar to expose at least one command button for " + context + ".");
            }
        }

        private static void AssertBlueprintHandheldButtons(BlueprintHandheldActionBarFrame frame, params string[] expectedIds)
        {
            if (frame == null || !frame.Visible)
            {
                throw new InvalidOperationException("Expected blueprint handheld action bar to be visible before checking button ids.");
            }

            if (frame.Buttons.Count != expectedIds.Length)
            {
                throw new InvalidOperationException("Expected " + expectedIds.Length + " handheld buttons, got " + frame.Buttons.Count + ".");
            }

            for (var index = 0; index < expectedIds.Length; index++)
            {
                AssertStringEquals(frame.Buttons[index].Id, expectedIds[index], "handheld dynamic button id " + index);
            }
        }

        private static void AssertBlueprintHandheldButton(BlueprintHandheldActionBarFrame frame, string buttonId, string label, string tooltip)
        {
            AssertBlueprintHandheldButtonState(frame, buttonId, label, tooltip, true);
        }

        private static void AssertBlueprintHandheldButtonState(BlueprintHandheldActionBarFrame frame, string buttonId, string label, string tooltip, bool enabled)
        {
            if (frame == null || frame.Buttons == null)
            {
                throw new InvalidOperationException("Expected blueprint handheld frame before checking button.");
            }

            for (var index = 0; index < frame.Buttons.Count; index++)
            {
                var button = frame.Buttons[index];
                if (button != null && string.Equals(button.Id, buttonId, StringComparison.Ordinal))
                {
                    AssertStringEquals(button.Label, label, "handheld button label " + buttonId);
                    AssertStringEquals(button.Tooltip, tooltip, "handheld button tooltip " + buttonId);
                    if (button.Enabled != enabled)
                    {
                        throw new InvalidOperationException("Expected handheld button " + buttonId + " enabled=" + enabled + ", got " + button.Enabled + ".");
                    }

                    return;
                }
            }

            throw new InvalidOperationException("Expected blueprint handheld button " + buttonId + " to exist.");
        }
    }
}
