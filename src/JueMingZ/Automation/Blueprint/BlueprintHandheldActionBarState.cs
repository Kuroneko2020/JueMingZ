using System;
using System.Collections.Generic;
using JueMingZ.Config;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Ui;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Blueprint
{
    public static class BlueprintHandheldActionBarState
    {
        public const string CommandElementPrefix = "blueprint-handheld-action-bar:";
        public const string HiddenReasonNone = "visible";
        public const string HiddenReasonFeatureDisabled = "feature-disabled";
        public const string HiddenReasonWorldNotReady = "world-not-ready";
        public const string HiddenReasonGameInputUnavailable = "game-input-unavailable";
        public const string HiddenReasonVanillaMenuGameMenu = "vanilla-menu-game-menu";
        public const string HiddenReasonVanillaMenuIngameOptions = "vanilla-menu-ingame-options";
        public const string HiddenReasonVanillaMenuFancyUi = "vanilla-menu-fancy-ui";
        public const string HiddenReasonVanillaMenuUnavailable = "vanilla-menu-unavailable";
        public const string HiddenReasonMapFullscreen = "map-fullscreen";
        public const string HiddenReasonLegacyMainUiVisible = "legacy-main-ui-visible";
        public const string HiddenReasonPlayerInventoryOpen = "player-inventory-open";
        public const string HiddenReasonChatOpen = "chat-open";
        public const string HiddenReasonChestOpen = "chest-open";
        public const string HiddenReasonNpcChatOpen = "npc-chat-open";
        public const string HiddenReasonSelectedItemUnavailable = "selected-item-unavailable";
        public const string HiddenReasonSelectedItemMismatch = "selected-item-mismatch";
        public const string HiddenReasonScreenUnavailable = "screen-unavailable";
        public const string ResultCodeUiOnlyNotImplemented = "uiOnlyNotImplemented";
        public const string ButtonIdCreate = "create";
        public const string ButtonIdSave = "save";
        public const string ButtonIdExitCreate = "exit-create";
        public const string ButtonIdClearSelection = "clear-selection";
        public const string ButtonIdOpenLibrary = "open-library";
        public const string ButtonIdDelete = "delete";
        public const string ButtonIdMove = "move";
        public const string ButtonIdRedMap = "red-map";
        public const string SaveDisabledTooltip = "还没有创建蓝图呢";
        public const string PointerOwnershipReasonLeft = "left";
        public const string PointerOwnershipReasonScroll = "scroll";
        public const string PointerOwnershipReasonHover = "hover";

        private const int PanelHeight = 48;
        private const int PanelPadding = 8;
        private const int CompactPanelPadding = 4;
        private const int ButtonHeight = 32;
        private const int ButtonGap = 8;
        private const int CompactButtonGap = 4;
        private const int BottomMargin = 34;
        private const int HorizontalMargin = 8;
        private const int PreferredButtonWidth = 108;

        private static readonly object InteractionSyncRoot = new object();
        private static readonly BlueprintHandheldActionBarButtonDefinition[] ButtonDefinitions =
        {
            new BlueprintHandheldActionBarButtonDefinition(ButtonIdCreate, "创建蓝图", 0, "创建新的蓝图选区"),
            new BlueprintHandheldActionBarButtonDefinition(ButtonIdSave, "保存蓝图", 0, "保存当前蓝图选区"),
            new BlueprintHandheldActionBarButtonDefinition(ButtonIdExitCreate, "退出创建", 1, "退出创建并保留当前选区"),
            new BlueprintHandheldActionBarButtonDefinition(ButtonIdClearSelection, "清除已有选区", 2, "只清除当前蓝图创建选区"),
            new BlueprintHandheldActionBarButtonDefinition(ButtonIdOpenLibrary, "打开蓝图库", 1, "打开蓝图库"),
            new BlueprintHandheldActionBarButtonDefinition(ButtonIdDelete, "删除蓝图", 3, "删除已经放置的蓝图或已经选区待创建的区域"),
            new BlueprintHandheldActionBarButtonDefinition(ButtonIdMove, "移动蓝图", 4, "移动已经放置的蓝图或已经选区待创建的区域"),
            new BlueprintHandheldActionBarButtonDefinition(ButtonIdRedMap, "红图", 5, "对已放置的蓝图区域进行修改")
        };
        private static string _hoveredButtonId = string.Empty;
        private static string _pressedButtonId = string.Empty;
        private static string _lastClickedButtonId = string.Empty;
        private static string _lastClickedButtonLabel = string.Empty;
        private static string _lastNotice = string.Empty;
        private static string _lastResultCode = string.Empty;
        private static int _lastHeldItemType;
        private static string _lastMouseReadMode = string.Empty;
        private static string _lastOwnershipReason = string.Empty;
        private static bool _lastCommandLeftDown;
        private static bool _lastCaptureLeftDown;
        private static bool _pendingAfterPlayerInputCommandEdge;

        public static IReadOnlyList<BlueprintHandheldActionBarButtonDefinition> GetButtonDefinitions()
        {
            return ButtonDefinitions;
        }

        public static string BuildCommandElementId(string buttonId)
        {
            return CommandElementPrefix + (buttonId ?? string.Empty);
        }

        public static string BuildPointerOwnerId(string buttonId)
        {
            return string.IsNullOrWhiteSpace(buttonId)
                ? CommandElementPrefix + "frame"
                : BuildCommandElementId(buttonId);
        }

        internal static BlueprintHandheldActionBarFrame BuildFrame(
            RuntimeSettingsSnapshot settings,
            GameStateSnapshot gameState,
            BlueprintHandheldActionBarEnvironment environment)
        {
            settings = settings ?? RuntimeSettingsSnapshot.FromSettings(AppSettings.CreateDefault());
            environment = environment ?? BlueprintHandheldActionBarEnvironment.Unavailable();

            var toolItemId = BlueprintSettings.NormalizeToolItemId(settings.BlueprintToolItemId);
            if (!settings.BlueprintHandheldEntryEnabled)
            {
                return Hidden(HiddenReasonFeatureDisabled, toolItemId, ReadSelectedItemType(gameState));
            }

            if (gameState == null || gameState.IsInMainMenu || !gameState.IsInWorld || !environment.WorldReady)
            {
                return Hidden(HiddenReasonWorldNotReady, toolItemId, ReadSelectedItemType(gameState));
            }

            var ui = gameState.Ui;
            if (ui == null)
            {
                return Hidden(HiddenReasonWorldNotReady, toolItemId, ReadSelectedItemType(gameState));
            }

            if (!environment.VanillaMenuReadAvailable)
            {
                return Hidden(HiddenReasonVanillaMenuUnavailable, toolItemId, ReadSelectedItemType(gameState));
            }

            if (environment.VanillaMenuBlocked)
            {
                return Hidden(MapVanillaMenuReason(environment.VanillaMenuReason), toolItemId, ReadSelectedItemType(gameState));
            }

            if (environment.MapFullscreenOpen)
            {
                return Hidden(HiddenReasonMapFullscreen, toolItemId, ReadSelectedItemType(gameState));
            }

            if (environment.LegacyMainUiVisible)
            {
                return Hidden(HiddenReasonLegacyMainUiVisible, toolItemId, ReadSelectedItemType(gameState));
            }

            var uiHiddenReason = GetUiHiddenReason(ui);
            if (!string.IsNullOrEmpty(uiHiddenReason))
            {
                return Hidden(uiHiddenReason, toolItemId, ReadSelectedItemType(gameState));
            }

            var selectedSnapshot = gameState.Inventory == null ? null : gameState.Inventory.SelectedItem;
            if (selectedSnapshot == null || selectedSnapshot.SlotIndex < 0 || selectedSnapshot.Type <= 0)
            {
                return Hidden(HiddenReasonSelectedItemUnavailable, toolItemId, ReadSelectedItemType(gameState));
            }

            if (selectedSnapshot.Type != toolItemId)
            {
                return Hidden(HiddenReasonSelectedItemMismatch, toolItemId, selectedSnapshot.Type);
            }

            if (environment.ScreenWidth <= 0 || environment.ScreenHeight <= 0)
            {
                return Hidden(HiddenReasonScreenUnavailable, toolItemId, selectedSnapshot.Type);
            }

            return BuildVisibleFrame(environment.ScreenWidth, environment.ScreenHeight, toolItemId, selectedSnapshot.Type, environment);
        }

        public static string HitTest(BlueprintHandheldActionBarFrame frame, int x, int y)
        {
            if (frame == null || !frame.Visible || frame.Buttons == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < frame.Buttons.Count; index++)
            {
                var button = frame.Buttons[index];
                if (button != null && button.Rect.Contains(x, y))
                {
                    return button.Id ?? string.Empty;
                }
            }

            return string.Empty;
        }

        internal static BlueprintHandheldActionBarInteraction HandlePointer(
            BlueprintHandheldActionBarFrame frame,
            BlueprintHandheldActionBarPointerInput input)
        {
            input = input ?? new BlueprintHandheldActionBarPointerInput();
            if (frame == null || !frame.Visible || !input.ReadAvailable)
            {
                ClearTransientInteraction(frame == null ? HiddenReasonWorldNotReady : frame.HiddenReason);
                return BlueprintHandheldActionBarInteraction.Hidden(frame == null ? HiddenReasonWorldNotReady : frame.HiddenReason);
            }

            var hoveredButtonId = HitTest(frame, input.MouseX, input.MouseY);
            var overBar = frame.Bounds.Contains(input.MouseX, input.MouseY);
            string pressedButtonId;
            bool leftPressed;
            bool leftReleased;
            bool clicked;

            lock (InteractionSyncRoot)
            {
                var previousLeftDown = input.AllowCommand ? _lastCommandLeftDown : _lastCaptureLeftDown;
                leftPressed = input.LeftDown && !previousLeftDown;
                leftReleased = !input.LeftDown && previousLeftDown;
                if (input.AllowCommand)
                {
                    _lastCommandLeftDown = input.LeftDown;
                }
                else
                {
                    // Capture-only readers are separate from command readers so
                    // future overlays cannot consume the handheld command edge.
                    _lastCaptureLeftDown = input.LeftDown;
                }

                _hoveredButtonId = hoveredButtonId;

                var hoveredButton = FindButtonFrame(frame, hoveredButtonId);
                var hoveredButtonEnabled = hoveredButton == null || hoveredButton.Enabled;
                var replayPendingAfterPlayerInputEdge =
                    input.AllowCommand &&
                    input.AfterPlayerInput &&
                    input.LeftDown &&
                    !leftPressed &&
                    _pendingAfterPlayerInputCommandEdge &&
                    hoveredButtonEnabled &&
                    !string.IsNullOrWhiteSpace(hoveredButtonId);

                if (leftPressed && !string.IsNullOrWhiteSpace(hoveredButtonId) && hoveredButtonEnabled)
                {
                    _pressedButtonId = hoveredButtonId;
                }
                else if (replayPendingAfterPlayerInputEdge)
                {
                    _pressedButtonId = hoveredButtonId;
                }

                clicked =
                    input.AllowCommand &&
                    (leftPressed || replayPendingAfterPlayerInputEdge) &&
                    hoveredButtonEnabled &&
                    !string.IsNullOrWhiteSpace(hoveredButtonId);

                // Prefix can observe a fresh OS button edge while Terraria UI
                // coordinates are still stale. Let the PlayerInput postfix replay
                // exactly that edge once after Terraria refreshes the mouse coordinates.
                if (input.AllowCommand && !input.AfterPlayerInput && leftPressed && !clicked)
                {
                    _pendingAfterPlayerInputCommandEdge = true;
                }
                else if (!input.LeftDown || input.AfterPlayerInput || clicked)
                {
                    _pendingAfterPlayerInputCommandEdge = false;
                }

                if (!input.LeftDown)
                {
                    _pressedButtonId = string.Empty;
                }

                pressedButtonId = _pressedButtonId;
            }

            var definition = FindButtonDefinition(hoveredButtonId);
            var shouldCaptureMouse = overBar || !string.IsNullOrWhiteSpace(pressedButtonId);
            var shouldConsumeLeft = shouldCaptureMouse && (input.LeftDown || leftPressed || leftReleased || clicked);
            var shouldConsumeScroll = overBar && input.ScrollDelta != 0;
            var ownershipReason = ResolvePointerOwnershipReason(shouldConsumeLeft, shouldConsumeScroll, shouldCaptureMouse);
            if (!string.IsNullOrWhiteSpace(ownershipReason))
            {
                lock (InteractionSyncRoot)
                {
                    _lastMouseReadMode = input.MouseReadMode ?? string.Empty;
                    _lastOwnershipReason = ownershipReason;
                }
            }

            return new BlueprintHandheldActionBarInteraction(
                shouldCaptureMouse,
                shouldConsumeLeft,
                shouldConsumeScroll,
                clicked,
                hoveredButtonId,
                pressedButtonId,
                definition == null ? string.Empty : definition.Label,
                frame.SelectedItemType,
                HiddenReasonNone);
        }

        internal static BlueprintHandheldActionBarCommandResult RecordPlaceholderClick(
            string buttonId,
            int heldItemType,
            bool mouseCaptured)
        {
            var definition = FindButtonDefinition(buttonId);
            var normalizedButtonId = definition == null ? buttonId ?? string.Empty : definition.Id;
            var label = definition == null ? buttonId ?? string.Empty : definition.Label;
            var message = string.IsNullOrWhiteSpace(label)
                ? "蓝图手持按钮暂未接入。"
                : label + " 暂未接入。";
            lock (InteractionSyncRoot)
            {
                _lastClickedButtonId = normalizedButtonId;
                _lastClickedButtonLabel = label;
                _lastHeldItemType = heldItemType;
                _lastResultCode = ResultCodeUiOnlyNotImplemented;
                _lastNotice = message;
            }

            return new BlueprintHandheldActionBarCommandResult(
                true,
                normalizedButtonId,
                label,
                ResultCodeUiOnlyNotImplemented,
                message,
                heldItemType,
                mouseCaptured);
        }

        internal static BlueprintHandheldActionBarCommandResult RecordCommandResultClick(
            string buttonId,
            int heldItemType,
            bool mouseCaptured,
            string resultCode,
            string message)
        {
            var definition = FindButtonDefinition(buttonId);
            var normalizedButtonId = definition == null ? buttonId ?? string.Empty : definition.Id;
            var label = definition == null ? buttonId ?? string.Empty : definition.Label;
            resultCode = resultCode ?? string.Empty;
            message = string.IsNullOrWhiteSpace(message) ? label + " 已执行。" : message;
            lock (InteractionSyncRoot)
            {
                _lastClickedButtonId = normalizedButtonId;
                _lastClickedButtonLabel = label;
                _lastHeldItemType = heldItemType;
                _lastResultCode = resultCode;
                _lastNotice = message;
            }

            return new BlueprintHandheldActionBarCommandResult(
                true,
                normalizedButtonId,
                label,
                resultCode,
                message,
                heldItemType,
                mouseCaptured);
        }

        internal static BlueprintHandheldActionBarInteractionSnapshot GetInteractionSnapshotForTesting()
        {
            lock (InteractionSyncRoot)
            {
                return new BlueprintHandheldActionBarInteractionSnapshot(
                    _hoveredButtonId,
                    _pressedButtonId,
                    _lastClickedButtonId,
                    _lastClickedButtonLabel,
                    _lastNotice,
                    _lastResultCode,
                    _lastHeldItemType,
                    _lastMouseReadMode,
                    _lastOwnershipReason);
            }
        }

        internal static BlueprintHandheldActionBarDiagnostics BuildDiagnostics(
            RuntimeSettingsSnapshot settings,
            GameStateSnapshot gameState,
            BlueprintHandheldActionBarEnvironment environment)
        {
            var frame = BuildFrame(settings, gameState, environment);
            if (frame == null)
            {
                return BlueprintHandheldActionBarDiagnostics.Hidden(HiddenReasonWorldNotReady, 0, 0);
            }

            return new BlueprintHandheldActionBarDiagnostics(
                frame.Visible,
                frame.Visible ? string.Empty : frame.HiddenReason,
                frame.ToolItemId,
                frame.SelectedItemType,
                frame.LastClickedButtonId,
                frame.LastResultCode,
                frame.HoveredButtonId,
                frame.PressedButtonId,
                frame.LastMouseReadMode,
                frame.LastOwnershipReason);
        }

        internal static void ResetInteractionForTesting()
        {
            ClearTransientInteraction(string.Empty);
        }

        private static BlueprintHandheldActionBarFrame BuildVisibleFrame(
            int screenWidth,
            int screenHeight,
            int toolItemId,
            int selectedItemType,
            BlueprintHandheldActionBarEnvironment environment)
        {
            var visibleButtons = SelectVisibleButtonSpecs(environment);
            var count = Math.Max(1, visibleButtons.Count);
            var compact = screenWidth < 480;
            var gap = compact ? CompactButtonGap : ButtonGap;
            var padding = compact ? CompactPanelPadding : PanelPadding;
            var availableWidth = Math.Max(count, screenWidth - HorizontalMargin * 2);
            var preferredWidth = PreferredButtonWidth * count + gap * (count - 1) + padding * 2;
            var barWidth = Math.Min(preferredWidth, availableWidth);
            var buttonWidth = (barWidth - padding * 2 - gap * (count - 1)) / count;
            if (buttonWidth < 1)
            {
                gap = 0;
                padding = 0;
                buttonWidth = Math.Max(1, availableWidth / count);
                barWidth = buttonWidth * count;
            }
            else
            {
                barWidth = buttonWidth * count + gap * (count - 1) + padding * 2;
            }

            var barX = Math.Max(0, (screenWidth - barWidth) / 2);
            var barY = Math.Max(0, screenHeight - BottomMargin - PanelHeight);
            if (barY + PanelHeight > screenHeight)
            {
                barY = Math.Max(0, screenHeight - PanelHeight);
            }

            var bounds = new LegacyUiRect(barX, barY, Math.Min(barWidth, Math.Max(1, screenWidth - barX)), Math.Min(PanelHeight, Math.Max(1, screenHeight - barY)));
            var buttons = new List<BlueprintHandheldActionBarButtonFrame>(count);
            var buttonY = bounds.Y + Math.Max(0, (bounds.Height - ButtonHeight) / 2);
            for (var index = 0; index < visibleButtons.Count; index++)
            {
                var definition = visibleButtons[index];
                var x = bounds.X + padding + index * (buttonWidth + gap);
                buttons.Add(new BlueprintHandheldActionBarButtonFrame(
                    definition.Definition.Id,
                    definition.Definition.Label,
                    definition.Definition.Order,
                    definition.Tooltip,
                    definition.Enabled,
                    new LegacyUiRect(x, buttonY, buttonWidth, Math.Min(ButtonHeight, bounds.Height))));
            }

            return new BlueprintHandheldActionBarFrame(
                true,
                HiddenReasonNone,
                toolItemId,
                selectedItemType,
                screenWidth,
                screenHeight,
                bounds,
                buttons,
                BuildLayoutSignature(screenWidth, screenHeight, bounds, buttons),
                CurrentHoveredButtonId(),
                CurrentPressedButtonId(),
                CurrentLastNotice(),
                CurrentLastResultCode(),
                CurrentLastClickedButtonId(),
                CurrentLastMouseReadMode(),
                CurrentLastOwnershipReason());
        }

        private static IReadOnlyList<BlueprintHandheldActionBarButtonSpec> SelectVisibleButtonSpecs(BlueprintHandheldActionBarEnvironment environment)
        {
            environment = environment ?? new BlueprintHandheldActionBarEnvironment();
            var definitions = new List<BlueprintHandheldActionBarButtonSpec>();
            var creating = environment.BlueprintCreationActive;
            var pendingCapture = environment.BlueprintCreationCompletedPendingCapture;
            var creatingOrPendingCapture = creating || pendingCapture;
            var hasSelectedCells = environment.BlueprintCreationSelectedCount > 0;

            if (creatingOrPendingCapture)
            {
                definitions.Add(BuildButtonSpec(
                    ButtonIdSave,
                    hasSelectedCells,
                    hasSelectedCells ? string.Empty : SaveDisabledTooltip));
                definitions.Add(BuildButtonSpec(ButtonIdExitCreate, true, string.Empty));
                if (hasSelectedCells)
                {
                    definitions.Add(BuildButtonSpec(ButtonIdClearSelection, true, string.Empty));
                }
            }
            else
            {
                definitions.Add(BuildButtonSpec(ButtonIdCreate, true, string.Empty));
                definitions.Add(BuildButtonSpec(ButtonIdOpenLibrary, true, string.Empty));

                if (environment.BlueprintHasPlacedInstances)
                {
                    definitions.Add(BuildButtonSpec(ButtonIdDelete, true, string.Empty));
                    definitions.Add(BuildButtonSpec(ButtonIdMove, true, string.Empty));
                    definitions.Add(BuildButtonSpec(ButtonIdRedMap, true, string.Empty));
                }
            }

            for (var index = definitions.Count - 1; index >= 0; index--)
            {
                if (definitions[index] == null || definitions[index].Definition == null)
                {
                    definitions.RemoveAt(index);
                }
            }

            return definitions;
        }

        private static BlueprintHandheldActionBarButtonSpec BuildButtonSpec(string buttonId, bool enabled, string tooltipOverride)
        {
            var definition = FindButtonDefinition(buttonId);
            if (definition == null)
            {
                return null;
            }

            return new BlueprintHandheldActionBarButtonSpec(definition, enabled, string.IsNullOrWhiteSpace(tooltipOverride) ? definition.Tooltip : tooltipOverride);
        }

        private static BlueprintHandheldActionBarFrame Hidden(string reason, int toolItemId, int selectedItemType)
        {
            ClearTransientInteraction(reason);
            return new BlueprintHandheldActionBarFrame(
                false,
                string.IsNullOrWhiteSpace(reason) ? HiddenReasonWorldNotReady : reason,
                toolItemId,
                selectedItemType,
                0,
                0,
                new LegacyUiRect(0, 0, 0, 0),
                new BlueprintHandheldActionBarButtonFrame[0],
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static string GetUiHiddenReason(UiStateSnapshot ui)
        {
            if (ui.ChatOpen)
            {
                return HiddenReasonChatOpen;
            }

            if (ui.NpcChatOpen)
            {
                return HiddenReasonNpcChatOpen;
            }

            return string.Empty;
        }

        private static int ReadSelectedItemType(GameStateSnapshot gameState)
        {
            var selected = gameState == null || gameState.Inventory == null ? null : gameState.Inventory.SelectedItem;
            return selected == null ? 0 : selected.Type;
        }

        private static string MapVanillaMenuReason(string reason)
        {
            if (string.Equals(reason, "gameMenu", StringComparison.Ordinal))
            {
                return HiddenReasonVanillaMenuGameMenu;
            }

            if (string.Equals(reason, "ingameOptionsWindow", StringComparison.Ordinal))
            {
                return HiddenReasonVanillaMenuIngameOptions;
            }

            if (string.Equals(reason, "inFancyUI", StringComparison.Ordinal))
            {
                return HiddenReasonVanillaMenuFancyUi;
            }

            return HiddenReasonVanillaMenuUnavailable;
        }

        private static string BuildLayoutSignature(
            int screenWidth,
            int screenHeight,
            LegacyUiRect bounds,
            IReadOnlyList<BlueprintHandheldActionBarButtonFrame> buttons)
        {
            var signature = screenWidth + "x" + screenHeight + "|" +
                            bounds.X + "," + bounds.Y + "," + bounds.Width + "," + bounds.Height;
            for (var index = 0; index < buttons.Count; index++)
            {
                var button = buttons[index];
                signature += "|" + button.Id + ":" + button.Rect.X + "," + button.Rect.Y + "," + button.Rect.Width + "," + button.Rect.Height;
            }

            return signature;
        }

        private static BlueprintHandheldActionBarButtonDefinition FindButtonDefinition(string buttonId)
        {
            if (string.IsNullOrWhiteSpace(buttonId))
            {
                return null;
            }

            for (var index = 0; index < ButtonDefinitions.Length; index++)
            {
                var definition = ButtonDefinitions[index];
                if (definition != null && string.Equals(definition.Id, buttonId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private static BlueprintHandheldActionBarButtonFrame FindButtonFrame(BlueprintHandheldActionBarFrame frame, string buttonId)
        {
            if (frame == null || frame.Buttons == null || string.IsNullOrWhiteSpace(buttonId))
            {
                return null;
            }

            for (var index = 0; index < frame.Buttons.Count; index++)
            {
                var button = frame.Buttons[index];
                if (button != null && string.Equals(button.Id, buttonId, StringComparison.Ordinal))
                {
                    return button;
                }
            }

            return null;
        }

        private static void ClearTransientInteraction(string reason)
        {
            lock (InteractionSyncRoot)
            {
                _hoveredButtonId = string.Empty;
                _pressedButtonId = string.Empty;
                _lastClickedButtonId = string.Empty;
                _lastClickedButtonLabel = string.Empty;
                _lastNotice = string.Empty;
                _lastResultCode = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason;
                _lastHeldItemType = 0;
                _lastMouseReadMode = string.Empty;
                _lastOwnershipReason = string.Empty;
                _lastCommandLeftDown = false;
                _lastCaptureLeftDown = false;
                _pendingAfterPlayerInputCommandEdge = false;
            }
        }

        private static string CurrentHoveredButtonId()
        {
            lock (InteractionSyncRoot)
            {
                return _hoveredButtonId;
            }
        }

        private static string CurrentPressedButtonId()
        {
            lock (InteractionSyncRoot)
            {
                return _pressedButtonId;
            }
        }

        private static string CurrentLastNotice()
        {
            lock (InteractionSyncRoot)
            {
                return _lastNotice;
            }
        }

        private static string CurrentLastResultCode()
        {
            lock (InteractionSyncRoot)
            {
                return _lastResultCode;
            }
        }

        private static string CurrentLastClickedButtonId()
        {
            lock (InteractionSyncRoot)
            {
                return _lastClickedButtonId;
            }
        }

        private static string CurrentLastMouseReadMode()
        {
            lock (InteractionSyncRoot)
            {
                return _lastMouseReadMode;
            }
        }

        private static string CurrentLastOwnershipReason()
        {
            lock (InteractionSyncRoot)
            {
                return _lastOwnershipReason;
            }
        }

        private static string ResolvePointerOwnershipReason(bool shouldConsumeLeft, bool shouldConsumeScroll, bool shouldCaptureMouse)
        {
            if (shouldConsumeLeft)
            {
                return PointerOwnershipReasonLeft;
            }

            if (shouldConsumeScroll)
            {
                return PointerOwnershipReasonScroll;
            }

            return shouldCaptureMouse ? PointerOwnershipReasonHover : string.Empty;
        }
    }

    public sealed class BlueprintHandheldActionBarEnvironment
    {
        public bool WorldReady { get; set; }
        public bool GameInputAvailable { get; set; }
        public bool VanillaMenuReadAvailable { get; set; }
        public bool VanillaMenuBlocked { get; set; }
        public string VanillaMenuReason { get; set; }
        public bool MapFullscreenOpen { get; set; }
        public bool LegacyMainUiVisible { get; set; }
        public bool BlueprintCreationActive { get; set; }
        public bool BlueprintCreationHasPendingSelection { get; set; }
        public bool BlueprintCreationCompletedPendingCapture { get; set; }
        public int BlueprintCreationSelectedCount { get; set; }
        public bool BlueprintHasPlacedInstances { get; set; }
        public int BlueprintPlacedInstanceCount { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }

        public BlueprintHandheldActionBarEnvironment()
        {
            VanillaMenuReadAvailable = true;
            VanillaMenuReason = string.Empty;
        }

        public static BlueprintHandheldActionBarEnvironment Unavailable()
        {
            return new BlueprintHandheldActionBarEnvironment
            {
                WorldReady = false,
                GameInputAvailable = false,
                VanillaMenuReadAvailable = false,
                VanillaMenuReason = "unavailable",
                ScreenWidth = 0,
                ScreenHeight = 0
            };
        }
    }

    internal sealed class BlueprintHandheldActionBarButtonSpec
    {
        public BlueprintHandheldActionBarButtonSpec(BlueprintHandheldActionBarButtonDefinition definition, bool enabled, string tooltip)
        {
            Definition = definition;
            Enabled = enabled;
            Tooltip = tooltip ?? string.Empty;
        }

        public BlueprintHandheldActionBarButtonDefinition Definition { get; private set; }
        public bool Enabled { get; private set; }
        public string Tooltip { get; private set; }
    }

    public sealed class BlueprintHandheldActionBarButtonDefinition
    {
        public BlueprintHandheldActionBarButtonDefinition(string id, string label, int order, string tooltip)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Order = order;
            Tooltip = tooltip ?? string.Empty;
        }

        public string Id { get; private set; }
        public string Label { get; private set; }
        public int Order { get; private set; }
        public string Tooltip { get; private set; }
    }

    public sealed class BlueprintHandheldActionBarButtonFrame
    {
        public BlueprintHandheldActionBarButtonFrame(string id, string label, int order, string tooltip, bool enabled, LegacyUiRect rect)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Order = order;
            Tooltip = tooltip ?? string.Empty;
            Enabled = enabled;
            Rect = rect;
        }

        public string Id { get; private set; }
        public string Label { get; private set; }
        public int Order { get; private set; }
        public string Tooltip { get; private set; }
        public bool Enabled { get; private set; }
        public LegacyUiRect Rect { get; private set; }
    }

    public sealed class BlueprintHandheldActionBarFrame
    {
        public BlueprintHandheldActionBarFrame(
            bool visible,
            string hiddenReason,
            int toolItemId,
            int selectedItemType,
            int screenWidth,
            int screenHeight,
            LegacyUiRect bounds,
            IReadOnlyList<BlueprintHandheldActionBarButtonFrame> buttons,
            string layoutSignature,
            string hoveredButtonId,
            string pressedButtonId,
            string lastNotice,
            string lastResultCode,
            string lastClickedButtonId,
            string lastMouseReadMode,
            string lastOwnershipReason)
        {
            Visible = visible;
            HiddenReason = hiddenReason ?? string.Empty;
            ToolItemId = toolItemId;
            SelectedItemType = selectedItemType;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            Bounds = bounds;
            Buttons = buttons ?? new BlueprintHandheldActionBarButtonFrame[0];
            LayoutSignature = layoutSignature ?? string.Empty;
            HoveredButtonId = hoveredButtonId ?? string.Empty;
            PressedButtonId = pressedButtonId ?? string.Empty;
            LastNotice = lastNotice ?? string.Empty;
            LastResultCode = lastResultCode ?? string.Empty;
            LastClickedButtonId = lastClickedButtonId ?? string.Empty;
            LastMouseReadMode = lastMouseReadMode ?? string.Empty;
            LastOwnershipReason = lastOwnershipReason ?? string.Empty;
        }

        public bool Visible { get; private set; }
        public string HiddenReason { get; private set; }
        public int ToolItemId { get; private set; }
        public int SelectedItemType { get; private set; }
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }
        public LegacyUiRect Bounds { get; private set; }
        public IReadOnlyList<BlueprintHandheldActionBarButtonFrame> Buttons { get; private set; }
        public string LayoutSignature { get; private set; }
        public string HoveredButtonId { get; private set; }
        public string PressedButtonId { get; private set; }
        public string LastNotice { get; private set; }
        public string LastResultCode { get; private set; }
        public string LastClickedButtonId { get; private set; }
        public string LastMouseReadMode { get; private set; }
        public string LastOwnershipReason { get; private set; }
    }

    public sealed class BlueprintHandheldActionBarPointerInput
    {
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public bool LeftDown { get; set; }
        public int ScrollDelta { get; set; }
        public string MouseReadMode { get; set; }
        public bool ReadAvailable { get; set; }
        public bool AllowCommand { get; set; }
        public bool AfterPlayerInput { get; set; }

        public BlueprintHandheldActionBarPointerInput()
        {
            MouseX = -1;
            MouseY = -1;
            MouseReadMode = string.Empty;
            AllowCommand = true;
            AfterPlayerInput = false;
        }
    }

    public sealed class BlueprintHandheldActionBarInteraction
    {
        public BlueprintHandheldActionBarInteraction(
            bool shouldCaptureMouse,
            bool shouldConsumeLeftInput,
            bool shouldConsumeScroll,
            bool clicked,
            string hoveredButtonId,
            string pressedButtonId,
            string buttonLabel,
            int heldItemType,
            string visibleReason)
        {
            ShouldCaptureMouse = shouldCaptureMouse;
            ShouldConsumeLeftInput = shouldConsumeLeftInput;
            ShouldConsumeScroll = shouldConsumeScroll;
            Clicked = clicked;
            HoveredButtonId = hoveredButtonId ?? string.Empty;
            PressedButtonId = pressedButtonId ?? string.Empty;
            ButtonLabel = buttonLabel ?? string.Empty;
            HeldItemType = heldItemType;
            VisibleReason = visibleReason ?? string.Empty;
        }

        public bool ShouldCaptureMouse { get; private set; }
        public bool ShouldConsumeLeftInput { get; private set; }
        public bool ShouldConsumeScroll { get; private set; }
        public bool Clicked { get; private set; }
        public string HoveredButtonId { get; private set; }
        public string PressedButtonId { get; private set; }
        public string ButtonLabel { get; private set; }
        public int HeldItemType { get; private set; }
        public string VisibleReason { get; private set; }

        public static BlueprintHandheldActionBarInteraction Hidden(string blockedReason)
        {
            return new BlueprintHandheldActionBarInteraction(
                false,
                false,
                false,
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                blockedReason);
        }
    }

    public sealed class BlueprintHandheldActionBarCommandResult
    {
        public BlueprintHandheldActionBarCommandResult(
            bool succeeded,
            string buttonId,
            string buttonLabel,
            string resultCode,
            string message,
            int heldItemType,
            bool mouseCaptured)
        {
            Succeeded = succeeded;
            ButtonId = buttonId ?? string.Empty;
            ButtonLabel = buttonLabel ?? string.Empty;
            ResultCode = resultCode ?? string.Empty;
            Message = message ?? string.Empty;
            HeldItemType = heldItemType;
            MouseCaptured = mouseCaptured;
        }

        public bool Succeeded { get; private set; }
        public string ButtonId { get; private set; }
        public string ButtonLabel { get; private set; }
        public string ResultCode { get; private set; }
        public string Message { get; private set; }
        public int HeldItemType { get; private set; }
        public bool MouseCaptured { get; private set; }
    }

    public sealed class BlueprintHandheldActionBarInteractionSnapshot
    {
        public BlueprintHandheldActionBarInteractionSnapshot(
            string hoveredButtonId,
            string pressedButtonId,
            string lastClickedButtonId,
            string lastClickedButtonLabel,
            string lastNotice,
            string lastResultCode,
            int lastHeldItemType,
            string lastMouseReadMode,
            string lastOwnershipReason)
        {
            HoveredButtonId = hoveredButtonId ?? string.Empty;
            PressedButtonId = pressedButtonId ?? string.Empty;
            LastClickedButtonId = lastClickedButtonId ?? string.Empty;
            LastClickedButtonLabel = lastClickedButtonLabel ?? string.Empty;
            LastNotice = lastNotice ?? string.Empty;
            LastResultCode = lastResultCode ?? string.Empty;
            LastHeldItemType = lastHeldItemType;
            LastMouseReadMode = lastMouseReadMode ?? string.Empty;
            LastOwnershipReason = lastOwnershipReason ?? string.Empty;
        }

        public string HoveredButtonId { get; private set; }
        public string PressedButtonId { get; private set; }
        public string LastClickedButtonId { get; private set; }
        public string LastClickedButtonLabel { get; private set; }
        public string LastNotice { get; private set; }
        public string LastResultCode { get; private set; }
        public int LastHeldItemType { get; private set; }
        public string LastMouseReadMode { get; private set; }
        public string LastOwnershipReason { get; private set; }
    }

    internal sealed class BlueprintHandheldActionBarDiagnostics
    {
        public BlueprintHandheldActionBarDiagnostics(
            bool visible,
            string blockedReason,
            int toolItemId,
            int selectedItemType,
            string lastAction,
            string lastResultCode,
            string hoveredButtonId,
            string pressedButtonId,
            string lastMouseReadMode,
            string lastOwnershipReason)
        {
            Visible = visible;
            BlockedReason = blockedReason ?? string.Empty;
            ToolItemId = toolItemId;
            SelectedItemType = selectedItemType;
            LastAction = lastAction ?? string.Empty;
            LastResultCode = lastResultCode ?? string.Empty;
            HoveredButtonId = hoveredButtonId ?? string.Empty;
            PressedButtonId = pressedButtonId ?? string.Empty;
            LastMouseReadMode = lastMouseReadMode ?? string.Empty;
            LastOwnershipReason = lastOwnershipReason ?? string.Empty;
        }

        public bool Visible { get; private set; }
        public string BlockedReason { get; private set; }
        public int ToolItemId { get; private set; }
        public int SelectedItemType { get; private set; }
        public string LastAction { get; private set; }
        public string LastResultCode { get; private set; }
        public string HoveredButtonId { get; private set; }
        public string PressedButtonId { get; private set; }
        public string LastMouseReadMode { get; private set; }
        public string LastOwnershipReason { get; private set; }

        public static BlueprintHandheldActionBarDiagnostics Hidden(string blockedReason, int toolItemId, int selectedItemType)
        {
            return new BlueprintHandheldActionBarDiagnostics(false, blockedReason, toolItemId, selectedItemType, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }
}
