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
                BlueprintHandheldActionBarState.ButtonIdDelete,
                BlueprintHandheldActionBarState.ButtonIdMove,
                BlueprintHandheldActionBarState.ButtonIdRedMap);
            AssertBlueprintHandheldButton(placed, BlueprintHandheldActionBarState.ButtonIdDelete, "删除蓝图", "删除已经放置的蓝图或已经选区待创建的区域");
            AssertBlueprintHandheldButton(placed, BlueprintHandheldActionBarState.ButtonIdMove, "移动蓝图", "移动已经放置的蓝图或已经选区待创建的区域");
            AssertBlueprintHandheldButton(placed, BlueprintHandheldActionBarState.ButtonIdRedMap, "红图", "对已放置的蓝图区域进行修改");

            AssertBlueprintHandheldButtons(
                BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true)),
                BlueprintHandheldActionBarState.ButtonIdSave,
                BlueprintHandheldActionBarState.ButtonIdOpenLibrary);

            AssertBlueprintHandheldButtons(
                BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true, blueprintCreationHasPendingSelection: true, blueprintCreationSelectedCount: 3, blueprintHasPlacedInstances: true, blueprintPlacedInstanceCount: 2)),
                BlueprintHandheldActionBarState.ButtonIdSave,
                BlueprintHandheldActionBarState.ButtonIdOpenLibrary,
                BlueprintHandheldActionBarState.ButtonIdDelete,
                BlueprintHandheldActionBarState.ButtonIdMove);
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
            AssertContains(contract, "legacy-ui-theme");
            AssertContains(contract, "vanilla-ui-skin");
            AssertContains(contract, "button-text-scale-0.78");

            if (Math.Abs(BlueprintHandheldActionBarOverlay.ButtonTextScale - 0.78f) > 0.001f)
            {
                throw new InvalidOperationException("Expected blueprint handheld button text scale to be old 0.58 + 0.2.");
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

        private static void BlueprintHandheldActionBarOverlayStaysUiOnlyAndNoScan()
        {
            BlueprintProjectionService.ResetForTesting();
            BlueprintMaterialService.ResetForTesting();
            try
            {
                var projectionBefore = BlueprintProjectionService.BuildStateSignature();
                var materialBefore = BlueprintMaterialService.BuildStateSignature();

                var frame = BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId);
                if (!frame.Visible)
                {
                    throw new InvalidOperationException("Expected blueprint handheld frame to be visible for no-scan contract test.");
                }

                var projectionAfter = BlueprintProjectionService.BuildStateSignature();
                var materialAfter = BlueprintMaterialService.BuildStateSignature();
                AssertIntEquals(projectionAfter, projectionBefore, "blueprint handheld projection signature");
                AssertIntEquals(materialAfter, materialBefore, "blueprint handheld material signature");

                if (!BlueprintHandheldActionBarOverlay.ShouldRegisterUiOverlayForTesting())
                {
                    throw new InvalidOperationException("Blueprint handheld action bar must register through the UI overlay dispatcher.");
                }

                if (!BlueprintHandheldActionBarOverlay.ShouldRegisterInputGuardsForTesting())
                {
                    throw new InvalidOperationException("Blueprint handheld action bar must register prefix and after-PlayerInput input guards.");
                }

                var contract = BlueprintHandheldActionBarOverlay.GetVisualContractForTesting();
                AssertContains(contract, "dynamic-buttons");
                AssertContains(contract, "create-enters-mask");
                AssertContains(contract, "save-captures-mask");
                AssertContains(contract, "open-library-real");
                AssertContains(contract, "unimplemented-buttons-ui-only");
                AssertContains(contract, "no-blueprint-refresh");
                AssertContains(contract, "mouse-consume");
                AssertContains(contract, "no-input-action-queue");
                AssertContains(contract, "legacy-ui-theme");
                AssertDoesNotContain(contract, "InputActionQueue");
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
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

            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            var outside = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                frame,
                BlueprintHandheldPointer(0, 0, true, 0, true));
            if (outside.ShouldCaptureMouse || outside.ShouldConsumeLeftInput || outside.Clicked)
            {
                throw new InvalidOperationException("Expected handheld action bar outside click to pass through.");
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
                BlueprintHandheldPointer(button.Rect.CenterX, button.Rect.CenterY, true, 0, false));
            if (!limitedPress.ShouldCaptureMouse || !limitedPress.ShouldConsumeLeftInput || limitedPress.Clicked)
            {
                throw new InvalidOperationException("Expected visible-but-limited handheld bar press to capture and consume without submitting a command.");
            }

            var wheel = BlueprintHandheldActionBarOverlay.HandlePointerForTesting(
                chest,
                BlueprintHandheldPointer(chest.Bounds.CenterX, chest.Bounds.CenterY, false, -120, false));
            if (!wheel.ShouldCaptureMouse || !wheel.ShouldConsumeScroll || wheel.Clicked)
            {
                throw new InvalidOperationException("Expected chest-visible handheld bar hover to consume wheel without submitting a command.");
            }

            var interaction = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
            AssertStringEquals(interaction.LastResultCode, string.Empty, "display gate must not submit a command");
        }

        private static void BlueprintHandheldActionBarRealCommandsAndUnimplementedButtons()
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

                var before = BlueprintEntryState.GetSnapshot(settings);
                AssertBlueprintHandheldPlaceholderCommand(BlueprintHandheldActionBarState.ButtonIdDelete, "删除蓝图");
                AssertBlueprintHandheldPlaceholderCommand(BlueprintHandheldActionBarState.ButtonIdMove, "移动蓝图");
                AssertBlueprintHandheldPlaceholderCommand(BlueprintHandheldActionBarState.ButtonIdRedMap, "红图");
                var after = BlueprintEntryState.GetSnapshot(settings);
                AssertStringEquals(after.Mode, before.Mode, "handheld unimplemented command must not change blueprint entry mode");
                AssertStringEquals(after.SelectedTemplateId, before.SelectedTemplateId, "handheld unimplemented command must not select a template");
            }
            finally
            {
                BlueprintCaptureService.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                restore();
            }
        }

        private static void BlueprintHandheldActionBarDiagnosticsSnapshotJson()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(new LegacyUiCommand
            {
                ElementId = BlueprintHandheldActionBarState.BuildCommandElementId("create"),
                Label = "创建蓝图",
                Kind = "button",
                IntValue = BlueprintSettings.DefaultToolItemId,
                MouseCaptured = true,
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
                BlueprintHandheldActionBarLastResultCode = visible.LastResultCode
            };
            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"BlueprintHandheldActionBarVisible\": true");
            AssertContains(json, "\"BlueprintHandheldActionBarBlockedReason\": \"\"");
            AssertContains(json, "\"BlueprintHandheldActionBarToolItemId\": 23");
            AssertContains(json, "\"BlueprintHandheldActionBarSelectedItemType\": 23");
            AssertContains(json, "\"BlueprintHandheldActionBarLastAction\": \"create\"");
            AssertContains(json, "\"BlueprintHandheldActionBarLastResultCode\": \"entryStateChanged\"");
            BlueprintEntryState.ResetForTesting();
            BlueprintCreationMaskState.ResetForTesting();
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
                Rect = new LegacyUiRect(10, 20, 80, 24)
            };
        }

        private static void AssertBlueprintHandheldPlaceholderCommand(string buttonId, string label)
        {
            LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(BuildBlueprintHandheldCommand(buttonId, label));
            var interaction = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
            AssertStringEquals(interaction.LastClickedButtonId, buttonId, "handheld placeholder clicked id");
            AssertStringEquals(interaction.LastClickedButtonLabel, label, "handheld placeholder clicked label");
            AssertStringEquals(interaction.LastResultCode, BlueprintHandheldActionBarState.ResultCodeUiOnlyNotImplemented, "handheld placeholder command result");
            AssertIntEquals(interaction.LastHeldItemType, BlueprintSettings.DefaultToolItemId, "handheld placeholder held item type");
            AssertContains(interaction.LastNotice, label);
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
            bool allowCommand)
        {
            return new BlueprintHandheldActionBarPointerInput
            {
                MouseX = mouseX,
                MouseY = mouseY,
                LeftDown = leftDown,
                ScrollDelta = scrollDelta,
                ReadAvailable = true,
                AllowCommand = allowCommand
            };
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
                    return;
                }
            }

            throw new InvalidOperationException("Expected blueprint handheld button " + buttonId + " to exist.");
        }
    }
}
