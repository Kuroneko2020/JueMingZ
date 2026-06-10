using System;
using JueMingZ.Actions;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public static bool TryCaptureUseItemInputState(object player, out UseItemInputState state)
        {
            state = new UseItemInputState();
            if (player == null)
            {
                return Fail("Cannot capture use item input: player unavailable.");
            }

            try
            {
                state.MouseX = GetStaticInt(TerrariaRuntimeTypes.MainType, "mouseX", 0);
                state.MouseY = GetStaticInt(TerrariaRuntimeTypes.MainType, "mouseY", 0);

                if (EnsurePlayerInputMouseAccessors())
                {
                    int playerInputX;
                    int playerInputY;
                    if (TryGetOptionalStatic(_playerInputMouseXField, _playerInputMouseXProperty, out playerInputX) &&
                        TryGetOptionalStatic(_playerInputMouseYField, _playerInputMouseYProperty, out playerInputY))
                    {
                        state.PlayerInputMouseCaptured = true;
                        state.PlayerInputMouseX = playerInputX;
                        state.PlayerInputMouseY = playerInputY;
                    }
                }

                if (EnsureTileTargetAccessors())
                {
                    int tileX;
                    int tileY;
                    if (TryGetOptionalStatic(_tileTargetXField, null, out tileX) &&
                        TryGetOptionalStatic(_tileTargetYField, null, out tileY))
                    {
                        state.TileTargetCaptured = true;
                        state.TileTargetX = tileX;
                        state.TileTargetY = tileY;
                    }
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                bool mouseButton;
                if (TryGetStaticBool(mainType, "mouseLeft", out mouseButton))
                {
                    state.MainMouseLeftCaptured = true;
                    state.MainMouseLeft = mouseButton;
                }

                if (TryGetStaticBool(mainType, "mouseRight", out mouseButton))
                {
                    state.MainMouseRightCaptured = true;
                    state.MainMouseRight = mouseButton;
                }

                if (TryGetStaticBool(mainType, "mouseLeftRelease", out mouseButton))
                {
                    state.MainMouseLeftReleaseCaptured = true;
                    state.MainMouseLeftRelease = mouseButton;
                }

                if (TryGetStaticBool(mainType, "mouseRightRelease", out mouseButton))
                {
                    state.MainMouseRightReleaseCaptured = true;
                    state.MainMouseRightRelease = mouseButton;
                }

                int selectedSlot;
                if (!TryGetSelectedItem(player, out selectedSlot))
                {
                    return false;
                }

                state.SelectedSlot = selectedSlot;
                TryGetBool(player, "controlUseItem", out var held);
                TryGetBool(player, "releaseUseItem", out var released);
                state.UseItemHeld = held;
                state.UseItemReleased = released;
                state.Captured = true;
                return true;
            }
            catch (Exception error)
            {
                return Fail("Capture use item input failed: " + error.Message);
            }
        }

        public static bool TryApplyUseItemOverrideForItemCheck(object player, ItemUseBridgeContext context)
        {
            if (player == null || context == null)
            {
                return Fail("Cannot apply use item override: missing player or context.");
            }

            var ok = true;
            if (context.SkipSelectInItemCheck)
            {
                int selectedSlot;
                if (!TryGetSelectedItem(player, out selectedSlot))
                {
                    return false;
                }

                var expectedSlot = context.ExpectedSelectedSlot >= 0
                    ? context.ExpectedSelectedSlot
                    : context.TargetSlot;
                if (expectedSlot >= 0 && selectedSlot != expectedSlot)
                {
                    return Fail("ItemCheck reached but selectedSlot was not targetSlot. selectedSlot=" +
                                selectedSlot + ", expected=" + expectedSlot + ".");
                }
            }
            else if (context.TargetSlot >= 0)
            {
                ok &= TrySelectInventorySlot(player, context.TargetSlot);
                if (ok && context.TargetSlot != 58)
                {
                    int selectedSlot;
                    if (!TryGetSelectedItem(player, out selectedSlot))
                    {
                        return false;
                    }

                    if (selectedSlot != context.TargetSlot)
                    {
                        return Fail(
                            "ItemCheck input override failed to select target slot. selectedSlot=" +
                            selectedSlot + ", targetSlot=" + context.TargetSlot + ".");
                    }
                }
            }

            if (context.HasMouseWorldTarget)
            {
                ok &= TrySetMouseWorldPosition(context.MouseWorldX, context.MouseWorldY);
            }
            else if (context.HasMouseScreenTarget)
            {
                ok &= TrySetMouseScreenPosition(context.MouseScreenX, context.MouseScreenY);
            }

            if (context.HasMouseWorldTarget || context.HasMouseScreenTarget)
            {
                SuppressSmartInteractionState();
            }

            // Controlled input write: player.controlUseItem / player.releaseUseItem for Player.ItemCheck bridge.
            ok &= SetMember(player, "controlUseItem", true);
            ok &= SetMember(player, "releaseUseItem", true);
            if (context.ApplyMainMouseLeftForItemCheck)
            {
                TryApplyMainMouseLeftForItemUseBridge();
            }
            else
            {
                TrySuppressMainMouseButtonsForItemUseBridge();
            }
            return ok;
        }

        public static bool TryApplyAutoHarvestSustainedUseForItemCheck(object player, int targetSlot, float mouseWorldX, float mouseWorldY, int direction)
        {
            if (player == null)
            {
                return Fail("Cannot apply auto harvest sustained use: player unavailable.");
            }

            var ok = true;
            ok &= TrySelectInventorySlot(player, targetSlot);
            ok &= TrySetMouseWorldPosition(mouseWorldX, mouseWorldY);
            SuppressSmartInteractionState();

            if (direction != 0)
            {
                int beforeDirection;
                int afterDirection;
                string method;
                TryChangePlayerDirection(player, direction, false, out beforeDirection, out afterDirection, out method);
            }

            // Controlled input write: auto harvest RawInput session supplies one scoped Player.ItemCheck use tick.
            ok &= SetMember(player, "controlUseItem", true);
            ok &= SetMember(player, "releaseUseItem", true);
            TryApplyMainMouseLeftForItemUseBridge();
            return ok ? ClearInputError() : Fail("Cannot apply auto harvest sustained use input override.");
        }

        public static bool TryApplyAutoMiningSustainedUseForItemCheck(object player, int targetSlot, float mouseWorldX, float mouseWorldY, int direction)
        {
            if (player == null)
            {
                return Fail("Cannot apply auto mining sustained use: player unavailable.");
            }

            int selectedSlot;
            if (!TryGetSelectedItem(player, out selectedSlot))
            {
                return false;
            }

            if (selectedSlot != targetSlot)
            {
                return Fail("Cannot apply auto mining sustained use: selected slot changed. selectedSlot=" + selectedSlot + ", targetSlot=" + targetSlot + ".");
            }

            var ok = true;
            ok &= TrySetMouseWorldPosition(mouseWorldX, mouseWorldY);
            SuppressSmartInteractionState();

            if (direction != 0)
            {
                int beforeDirection;
                int afterDirection;
                string method;
                TryChangePlayerDirection(player, direction, false, out beforeDirection, out afterDirection, out method);
            }

            // Controlled input write: auto mining supplies one scoped held-pickaxe ItemCheck tick without selecting a slot.
            ok &= SetMember(player, "controlUseItem", true);
            ok &= SetMember(player, "releaseUseItem", true);
            TryApplyMainMouseLeftForItemUseBridge();
            return ok ? ClearInputError() : Fail("Cannot apply auto mining sustained use input override.");
        }

        public static bool TryApplyAutoCaptureCritterSustainedUseForItemCheck(object player, int targetSlot, float mouseWorldX, float mouseWorldY, int direction)
        {
            if (player == null)
            {
                return Fail("Cannot apply auto capture critter sustained use: player unavailable.");
            }

            var ok = true;
            ok &= TrySelectInventorySlot(player, targetSlot);
            ok &= TrySetMouseWorldPosition(mouseWorldX, mouseWorldY);
            SuppressSmartInteractionState();

            if (direction != 0)
            {
                int beforeDirection;
                int afterDirection;
                string method;
                ok &= TryChangePlayerDirection(player, direction, true, out beforeDirection, out afterDirection, out method);
            }

            // Controlled input write: auto capture supplies one scoped Player.ItemCheck use tick while keeping the bug net selected.
            ok &= SetMember(player, "controlUseItem", true);
            ok &= SetMember(player, "releaseUseItem", true);
            TryApplyMainMouseLeftForItemUseBridge();
            return ok ? ClearInputError() : Fail("Cannot apply auto capture critter sustained use input override.");
        }

        public static bool TryApplyUseItemPulseForItemCheck(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item pulse: player unavailable.");
            }

            // Controlled input write: player.controlUseItem / player.releaseUseItem for queued RawInput pulse.
            var ok = SetMember(player, "controlUseItem", pressed);
            ok &= SetMember(player, "releaseUseItem", true);
            return ok ? ClearInputError() : Fail("Cannot apply use item pulse input override.");
        }

        public static bool TryApplyPhasebladeQuickSwitchForItemCheck(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply phaseblade quick switch: player unavailable.");
            }

            // Controlled input write: phaseblade quick switch uses a scoped
            // left-click press/release while suppressing the held right-click gate.
            return ApplyUseItemTakeoverFields(player, pressed, false, true)
                ? ClearInputError()
                : Fail("Cannot apply phaseblade quick switch input override.");
        }

        public static bool TryApplyPhasebladeQuickSwitchPostItemCheckState(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply phaseblade quick switch post-ItemCheck state: player unavailable.");
            }

            // Phaseblade projectile AI checks the owner's held item and controlUseItem
            // after ItemCheck. Keep only the synthetic left-button lifecycle visible
            // across that boundary; mouse target and slot restoration stay scoped.
            var ok = SetMember(player, "controlUseItem", pressed);
            ok &= SetMember(player, "releaseUseItem", !pressed);

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                TrySetStaticIfExists(mainType, "mouseLeft", pressed);
                TrySetStaticIfExists(mainType, "mouseLeftRelease", !pressed);
                TrySetStaticIfExists(mainType, "mouseRight", false);
                TrySetStaticIfExists(mainType, "mouseRightRelease", false);
            }

            return ok
                ? ClearInputError()
                : Fail("Cannot apply phaseblade quick switch post-ItemCheck input state.");
        }

        public static bool TryApplyUseItemReleaseForItemCheck(object player)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item release: player unavailable.");
            }

            // Controlled input write: release-only phase for vanilla delayUseItem gates.
            var ok = SetMember(player, "controlUseItem", false);
            ok &= SetMember(player, "releaseUseItem", true);
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType != null)
            {
                TrySetStaticIfExists(mainType, "mouseLeft", false);
                TrySetStaticIfExists(mainType, "mouseLeftRelease", true);
                TrySetStaticIfExists(mainType, "mouseRight", false);
                TrySetStaticIfExists(mainType, "mouseRightRelease", false);
            }

            return ok ? ClearInputError() : Fail("Cannot apply use item release input override.");
        }

        public static bool TryRestoreUseItemInputState(object player, UseItemInputState state)
        {
            return TryRestoreUseItemInputState(player, state, -1);
        }

        public static bool TryRestoreUseItemInputState(object player, UseItemInputState state, int restoreSelectedSlotOverride)
        {
            if (player == null || state == null || !state.Captured)
            {
                return false;
            }

            var ok = true;
            ok &= SetStatic(TerrariaRuntimeTypes.MainType, "mouseX", state.MouseX);
            ok &= SetStatic(TerrariaRuntimeTypes.MainType, "mouseY", state.MouseY);

            if (state.PlayerInputMouseCaptured)
            {
                TrySetOptionalStatic(_playerInputMouseXField, _playerInputMouseXProperty, state.PlayerInputMouseX);
                TrySetOptionalStatic(_playerInputMouseYField, _playerInputMouseYProperty, state.PlayerInputMouseY);
            }

            if (state.TileTargetCaptured)
            {
                TrySetOptionalStatic(_tileTargetXField, null, state.TileTargetX);
                TrySetOptionalStatic(_tileTargetYField, null, state.TileTargetY);
            }

            var restoreSlot = IsSupportedItemUseSlot(restoreSelectedSlotOverride)
                ? restoreSelectedSlotOverride
                : state.SelectedSlot;
            if (IsSupportedItemUseSlot(restoreSlot))
            {
                ok &= TrySelectInventorySlot(player, restoreSlot);
            }

            // Controlled input write: restore player.controlUseItem / player.releaseUseItem after Player.ItemCheck bridge.
            ok &= SetMember(player, "controlUseItem", state.UseItemHeld);
            ok &= SetMember(player, "releaseUseItem", state.UseItemReleased);
            if (state.MainMouseLeftCaptured)
            {
                ok &= TrySetStaticIfExists(TerrariaRuntimeTypes.MainType, "mouseLeft", state.MainMouseLeft);
            }

            if (state.MainMouseRightCaptured)
            {
                ok &= TrySetStaticIfExists(TerrariaRuntimeTypes.MainType, "mouseRight", state.MainMouseRight);
            }

            if (state.MainMouseLeftReleaseCaptured)
            {
                ok &= TrySetStaticIfExists(TerrariaRuntimeTypes.MainType, "mouseLeftRelease", state.MainMouseLeftRelease);
            }

            if (state.MainMouseRightReleaseCaptured)
            {
                ok &= TrySetStaticIfExists(TerrariaRuntimeTypes.MainType, "mouseRightRelease", state.MainMouseRightRelease);
            }

            return ok;
        }

        public static bool TryRestoreUseItemButtonInputState(object player, UseItemInputState state)
        {
            if (player == null || state == null || !state.Captured)
            {
                return false;
            }

            var ok = true;
            ok &= SetMember(player, "controlUseItem", state.UseItemHeld);
            ok &= SetMember(player, "releaseUseItem", state.UseItemReleased);

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType != null)
            {
                if (state.MainMouseLeftCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeft", state.MainMouseLeft);
                }

                if (state.MainMouseRightCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseRight", state.MainMouseRight);
                }

                if (state.MainMouseLeftReleaseCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeftRelease", state.MainMouseLeftRelease);
                }

                if (state.MainMouseRightReleaseCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseRightRelease", state.MainMouseRightRelease);
                }
            }

            return ok ? ClearInputError() : Fail("Cannot restore use item button input state.");
        }

        private static void TrySuppressMainMouseButtonsForItemUseBridge()
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return;
            }

            // Controlled input write: isolate queued ItemCheck pulses from the user's held physical mouse button.
            TrySetStaticIfExists(mainType, "mouseLeft", false);
            TrySetStaticIfExists(mainType, "mouseRight", false);
            TrySetStaticIfExists(mainType, "mouseLeftRelease", false);
            TrySetStaticIfExists(mainType, "mouseRightRelease", false);
        }

        private static void TryApplyMainMouseLeftForItemUseBridge()
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return;
            }

            // Controlled input write: some vanilla item paths still check Main.mouseLeftRelease during ItemCheck.
            TrySetStaticIfExists(mainType, "mouseLeft", true);
            TrySetStaticIfExists(mainType, "mouseLeftRelease", true);
            TrySetStaticIfExists(mainType, "mouseRight", false);
            TrySetStaticIfExists(mainType, "mouseRightRelease", false);
        }
    }
}
