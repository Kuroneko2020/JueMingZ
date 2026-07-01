using System;
using JueMingZ.Compat;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input.Hotkeys
{
    public static class UnifiedHotkeyRuntimeGate
    {
        public const string TextInputFocused = "textInputFocused";
        public const string MainMenu = "mainMenu";
        public const string NpcChatOpen = "npcChatOpen";
        public const string LegacyModalOpen = "legacyModalOpen";
        public const string F5TextInputFocused = "f5TextInputFocused";
        public const string ColorInputFocused = "colorInputFocused";
        public const string NameInputFocused = "nameInputFocused";

        public static UnifiedHotkeyRuntimeGateResult Evaluate(UnifiedHotkeyRuntimeGateContext context)
        {
            context = context ?? new UnifiedHotkeyRuntimeGateContext();

            if (context.MainMenu)
            {
                return UnifiedHotkeyRuntimeGateResult.Block(MainMenu);
            }

            if (!context.IsInWorld)
            {
                return UnifiedHotkeyRuntimeGateResult.Block("notInWorld");
            }

            if (!context.Foreground)
            {
                return UnifiedHotkeyRuntimeGateResult.Block("notForeground");
            }

            if (!context.GameInputAvailable)
            {
                return UnifiedHotkeyRuntimeGateResult.Block("gameInputUnavailable");
            }

            if (context.NpcChatOpen)
            {
                return UnifiedHotkeyRuntimeGateResult.Block(NpcChatOpen);
            }

            if (context.LegacyModalOpen)
            {
                return UnifiedHotkeyRuntimeGateResult.Block(LegacyModalOpen);
            }

            if (context.ColorInputFocused)
            {
                return UnifiedHotkeyRuntimeGateResult.Block(ColorInputFocused);
            }

            if (context.NameInputFocused)
            {
                return UnifiedHotkeyRuntimeGateResult.Block(NameInputFocused);
            }

            if (context.F5TextInputFocused)
            {
                return UnifiedHotkeyRuntimeGateResult.Block(F5TextInputFocused);
            }

            if (context.TerrariaTextInputFocused)
            {
                return UnifiedHotkeyRuntimeGateResult.Block(TextInputFocused, context.TerrariaTextInputReason);
            }

            if (context.HotkeyCaptureActive)
            {
                return UnifiedHotkeyRuntimeGateResult.Block("hotkeyCaptureActive");
            }

            if (context.LegacyUiActiveInteraction)
            {
                return UnifiedHotkeyRuntimeGateResult.Block("legacyUiActive");
            }

            if (context.LegacyUiVisible)
            {
                return UnifiedHotkeyRuntimeGateResult.Block("legacyUiVisible");
            }

            return UnifiedHotkeyRuntimeGateResult.Allow();
        }

        public static UnifiedHotkeyRuntimeGateContext CreateCurrentContext()
        {
            bool textFocused;
            string textReason;
            TerrariaInputCompat.TryReadTextInputFocus(out textFocused, out textReason);

            var activeTextId = LegacyTextInput.ActiveId;
            var activeMultilineId = LegacyMultilineTextInput.ActiveId;
            var activeColorId = LegacyHexColorInput.ActiveId;
            var nameFocused = IsNameInputId(activeTextId) || IsNameInputId(activeMultilineId);

            return new UnifiedHotkeyRuntimeGateContext
            {
                IsInWorld = TerrariaMainCompat.IsWorldReady,
                MainMenu = TerrariaMainCompat.IsInMainMenu,
                GameInputAvailable = TerrariaMainCompat.AllowsInputProcessing,
                Foreground = true,
                TerrariaTextInputFocused = textFocused,
                TerrariaTextInputReason = textReason ?? string.Empty,
                NpcChatOpen = !string.IsNullOrEmpty(TerrariaMainCompat.NpcChatText),
                LegacyModalOpen = LegacyUiOverlayCoordinator.Current.HasAnyActiveModal(),
                LegacyUiVisible = LegacyMainUiState.Visible,
                LegacyUiActiveInteraction = LegacyUiInput.IsActiveInteraction(),
                F5TextInputFocused = LegacyTextInput.IsAnyFocused || LegacyMultilineTextInput.IsAnyFocused,
                ColorInputFocused = LegacyHexColorInput.IsAnyFocused || !string.IsNullOrWhiteSpace(activeColorId),
                NameInputFocused = nameFocused,
                HotkeyCaptureActive = LegacyMainWindow.IsAnyHotkeyCaptureActive()
            };
        }

        private static bool IsNameInputId(string activeId)
        {
            if (string.IsNullOrWhiteSpace(activeId))
            {
                return false;
            }

            var text = activeId.Trim();
            return text.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("rename", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
