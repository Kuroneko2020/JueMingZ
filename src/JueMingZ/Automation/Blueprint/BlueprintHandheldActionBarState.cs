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
            new BlueprintHandheldActionBarButtonDefinition("create", "创建蓝图", 0),
            new BlueprintHandheldActionBarButtonDefinition("open-library", "打开蓝图库", 1),
            new BlueprintHandheldActionBarButtonDefinition("delete", "删除蓝图", 2),
            new BlueprintHandheldActionBarButtonDefinition("move", "移动蓝图", 3),
            new BlueprintHandheldActionBarButtonDefinition("red-map", "红图", 4)
        };
        private static string _hoveredButtonId = string.Empty;
        private static string _pressedButtonId = string.Empty;
        private static string _lastClickedButtonId = string.Empty;
        private static string _lastClickedButtonLabel = string.Empty;
        private static string _lastNotice = string.Empty;
        private static string _lastResultCode = string.Empty;
        private static int _lastHeldItemType;
        private static bool _lastLeftDown;

        public static IReadOnlyList<BlueprintHandheldActionBarButtonDefinition> GetButtonDefinitions()
        {
            return ButtonDefinitions;
        }

        public static string BuildCommandElementId(string buttonId)
        {
            return CommandElementPrefix + (buttonId ?? string.Empty);
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

            if (!ui.GameInputAvailable || !environment.GameInputAvailable)
            {
                return Hidden(HiddenReasonGameInputUnavailable, toolItemId, ReadSelectedItemType(gameState));
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

            return BuildVisibleFrame(environment.ScreenWidth, environment.ScreenHeight, toolItemId, selectedSnapshot.Type);
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
                leftPressed = input.LeftDown && !_lastLeftDown;
                leftReleased = !input.LeftDown && _lastLeftDown;
                _lastLeftDown = input.LeftDown;
                _hoveredButtonId = hoveredButtonId;

                if (leftPressed && !string.IsNullOrWhiteSpace(hoveredButtonId))
                {
                    _pressedButtonId = hoveredButtonId;
                }

                clicked =
                    input.AllowCommand &&
                    leftPressed &&
                    !string.IsNullOrWhiteSpace(hoveredButtonId);

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
                    _lastHeldItemType);
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
                frame.LastResultCode);
        }

        internal static void ResetInteractionForTesting()
        {
            ClearTransientInteraction(string.Empty);
        }

        private static BlueprintHandheldActionBarFrame BuildVisibleFrame(
            int screenWidth,
            int screenHeight,
            int toolItemId,
            int selectedItemType)
        {
            var count = ButtonDefinitions.Length;
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
            for (var index = 0; index < count; index++)
            {
                var definition = ButtonDefinitions[index];
                var x = bounds.X + padding + index * (buttonWidth + gap);
                buttons.Add(new BlueprintHandheldActionBarButtonFrame(
                    definition.Id,
                    definition.Label,
                    definition.Order,
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
                CurrentLastClickedButtonId());
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
                string.Empty);
        }

        private static string GetUiHiddenReason(UiStateSnapshot ui)
        {
            if (ui.PlayerInventoryOpen)
            {
                return HiddenReasonPlayerInventoryOpen;
            }

            if (ui.ChatOpen)
            {
                return HiddenReasonChatOpen;
            }

            if (ui.ChestOpen)
            {
                return HiddenReasonChestOpen;
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
                _lastLeftDown = false;
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

    public sealed class BlueprintHandheldActionBarButtonDefinition
    {
        public BlueprintHandheldActionBarButtonDefinition(string id, string label, int order)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Order = order;
        }

        public string Id { get; private set; }
        public string Label { get; private set; }
        public int Order { get; private set; }
    }

    public sealed class BlueprintHandheldActionBarButtonFrame
    {
        public BlueprintHandheldActionBarButtonFrame(string id, string label, int order, LegacyUiRect rect)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Order = order;
            Rect = rect;
        }

        public string Id { get; private set; }
        public string Label { get; private set; }
        public int Order { get; private set; }
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
            string lastClickedButtonId)
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
    }

    public sealed class BlueprintHandheldActionBarPointerInput
    {
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public bool LeftDown { get; set; }
        public int ScrollDelta { get; set; }
        public bool ReadAvailable { get; set; }
        public bool AllowCommand { get; set; }

        public BlueprintHandheldActionBarPointerInput()
        {
            MouseX = -1;
            MouseY = -1;
            AllowCommand = true;
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
            int lastHeldItemType)
        {
            HoveredButtonId = hoveredButtonId ?? string.Empty;
            PressedButtonId = pressedButtonId ?? string.Empty;
            LastClickedButtonId = lastClickedButtonId ?? string.Empty;
            LastClickedButtonLabel = lastClickedButtonLabel ?? string.Empty;
            LastNotice = lastNotice ?? string.Empty;
            LastResultCode = lastResultCode ?? string.Empty;
            LastHeldItemType = lastHeldItemType;
        }

        public string HoveredButtonId { get; private set; }
        public string PressedButtonId { get; private set; }
        public string LastClickedButtonId { get; private set; }
        public string LastClickedButtonLabel { get; private set; }
        public string LastNotice { get; private set; }
        public string LastResultCode { get; private set; }
        public int LastHeldItemType { get; private set; }
    }

    internal sealed class BlueprintHandheldActionBarDiagnostics
    {
        public BlueprintHandheldActionBarDiagnostics(
            bool visible,
            string blockedReason,
            int toolItemId,
            int selectedItemType,
            string lastAction,
            string lastResultCode)
        {
            Visible = visible;
            BlockedReason = blockedReason ?? string.Empty;
            ToolItemId = toolItemId;
            SelectedItemType = selectedItemType;
            LastAction = lastAction ?? string.Empty;
            LastResultCode = lastResultCode ?? string.Empty;
        }

        public bool Visible { get; private set; }
        public string BlockedReason { get; private set; }
        public int ToolItemId { get; private set; }
        public int SelectedItemType { get; private set; }
        public string LastAction { get; private set; }
        public string LastResultCode { get; private set; }

        public static BlueprintHandheldActionBarDiagnostics Hidden(string blockedReason, int toolItemId, int selectedItemType)
        {
            return new BlueprintHandheldActionBarDiagnostics(false, blockedReason, toolItemId, selectedItemType, string.Empty, string.Empty);
        }
    }
}
