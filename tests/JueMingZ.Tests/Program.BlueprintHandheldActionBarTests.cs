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
            if (!visible.Visible || visible.Buttons.Count != 5)
            {
                throw new InvalidOperationException("Expected blueprint handheld action bar to show five buttons for enabled gel-in-hand world play.");
            }

            AssertStringEquals(visible.Buttons[0].Id, "create", "handheld button 0 id");
            AssertStringEquals(visible.Buttons[0].Label, "创建蓝图", "handheld button 0 label");
            AssertStringEquals(visible.Buttons[1].Id, "open-library", "handheld button 1 id");
            AssertStringEquals(visible.Buttons[1].Label, "打开蓝图库", "handheld button 1 label");
            AssertStringEquals(visible.Buttons[2].Id, "delete", "handheld button 2 id");
            AssertStringEquals(visible.Buttons[2].Label, "删除蓝图", "handheld button 2 label");
            AssertStringEquals(visible.Buttons[3].Id, "move", "handheld button 3 id");
            AssertStringEquals(visible.Buttons[3].Label, "移动蓝图", "handheld button 3 label");
            AssertStringEquals(visible.Buttons[4].Id, "red-map", "handheld button 4 id");
            AssertStringEquals(visible.Buttons[4].Label, "红图", "handheld button 4 label");

            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, mapFullscreenOpen: true)),
                BlueprintHandheldActionBarState.HiddenReasonMapFullscreen);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, legacyMainUiVisible: true)),
                BlueprintHandheldActionBarState.HiddenReasonLegacyMainUiVisible);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, gameInputAvailable: false)),
                BlueprintHandheldActionBarState.HiddenReasonGameInputUnavailable);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, vanillaBlocked: true, vanillaReason: "gameMenu")),
                BlueprintHandheldActionBarState.HiddenReasonVanillaMenuGameMenu);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, vanillaBlocked: true, vanillaReason: "ingameOptionsWindow")),
                BlueprintHandheldActionBarState.HiddenReasonVanillaMenuIngameOptions);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720, vanillaBlocked: true, vanillaReason: "inFancyUI")),
                BlueprintHandheldActionBarState.HiddenReasonVanillaMenuFancyUi);

            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), playerInventoryOpen: true),
                BlueprintHandheldActionBarState.HiddenReasonPlayerInventoryOpen);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), chatOpen: true),
                BlueprintHandheldActionBarState.HiddenReasonChatOpen);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), chestOpen: true),
                BlueprintHandheldActionBarState.HiddenReasonChestOpen);
            AssertBlueprintHandheldHidden(
                BuildBlueprintHandheldFrame(true, BlueprintSettings.DefaultToolItemId, BlueprintHandheldEnvironment(1280, 720), npcChatOpen: true),
                BlueprintHandheldActionBarState.HiddenReasonNpcChatOpen);
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
                AssertContains(contract, "ui-only-placeholder-click");
                AssertContains(contract, "no-blueprint-refresh");
                AssertContains(contract, "mouse-consume");
                AssertContains(contract, "no-input-action-queue");
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

        private static void BlueprintHandheldActionBarCommandsStayUiOnly()
        {
            BlueprintHandheldActionBarState.ResetInteractionForTesting();
            BlueprintEntryState.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            var before = BlueprintEntryState.GetSnapshot(settings);
            foreach (var definition in BlueprintHandheldActionBarState.GetButtonDefinitions())
            {
                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(new LegacyUiCommand
                {
                    ElementId = BlueprintHandheldActionBarState.BuildCommandElementId(definition.Id),
                    Label = definition.Label,
                    Kind = "button",
                    IntValue = BlueprintSettings.DefaultToolItemId,
                    MouseCaptured = true,
                    Rect = new LegacyUiRect(10, 20, 80, 24)
                });

                var interaction = BlueprintHandheldActionBarState.GetInteractionSnapshotForTesting();
                AssertStringEquals(interaction.LastClickedButtonId, definition.Id, "handheld command clicked id");
                AssertStringEquals(interaction.LastClickedButtonLabel, definition.Label, "handheld command clicked label");
                AssertStringEquals(interaction.LastResultCode, BlueprintHandheldActionBarState.ResultCodeUiOnlyNotImplemented, "handheld command result");
                AssertIntEquals(interaction.LastHeldItemType, BlueprintSettings.DefaultToolItemId, "handheld command held item type");
                AssertContains(interaction.LastNotice, definition.Label);
            }

            var after = BlueprintEntryState.GetSnapshot(settings);
            AssertStringEquals(after.Mode, before.Mode, "handheld command must not change blueprint entry mode");
            AssertStringEquals(after.SelectedTemplateId, before.SelectedTemplateId, "handheld command must not select a template");
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
            AssertStringEquals(visible.LastResultCode, BlueprintHandheldActionBarState.ResultCodeUiOnlyNotImplemented, "handheld diagnostics result");

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
            AssertContains(json, "\"BlueprintHandheldActionBarLastResultCode\": \"uiOnlyNotImplemented\"");
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

        private static BlueprintHandheldActionBarFrame BuildBlueprintHandheldFrame(
            bool enabled,
            int selectedItemType,
            BlueprintHandheldActionBarEnvironment environment = null,
            bool playerInventoryOpen = false,
            bool chatOpen = false,
            bool chestOpen = false,
            bool npcChatOpen = false)
        {
            return BlueprintHandheldActionBarOverlay.BuildFrameForTesting(
                BlueprintHandheldSettings(enabled),
                BlueprintHandheldSnapshot(selectedItemType, playerInventoryOpen, chatOpen, chestOpen, npcChatOpen),
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
            bool npcChatOpen = false)
        {
            return new GameStateSnapshot
            {
                TerrariaDetected = true,
                IsInMainMenu = false,
                IsInWorld = true,
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
                    GameInputAvailable = true,
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
            string vanillaReason = "")
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
    }
}
