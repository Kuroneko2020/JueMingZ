using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        private static bool? _physicalMouseLeftHeldOverrideForTesting;
        private static bool? _physicalMouseRightHeldOverrideForTesting;

        internal static void SetPhysicalMouseButtonOverridesForTesting(bool? leftHeld, bool? rightHeld)
        {
            _physicalMouseLeftHeldOverrideForTesting = leftHeld;
            _physicalMouseRightHeldOverrideForTesting = rightHeld;
        }

        public sealed class ScopedUseItemTakeover : IDisposable
        {
            // Scoped takeover is the combat-facing contract for temporary
            // controlUseItem/Main mouse writes; callers must restore or dispose.
            private bool _disposed;

            internal ScopedUseItemTakeover(object player, bool pressed, string scopeName)
            {
                Player = player;
                Pressed = pressed;
                ScopeName = scopeName ?? string.Empty;
            }

            internal object Player { get; private set; }
            public bool Captured { get; internal set; }
            public bool Applied { get; internal set; }
            public bool Pressed { get; private set; }
            public string ScopeName { get; private set; }
            public bool PlayerControlUseItemCaptured { get; internal set; }
            public bool PlayerControlUseItem { get; internal set; }
            public bool PlayerReleaseUseItemCaptured { get; internal set; }
            public bool PlayerReleaseUseItem { get; internal set; }
            public bool PlayerChannelCaptured { get; internal set; }
            public bool PlayerChannel { get; internal set; }
            public bool MainMouseLeftCaptured { get; internal set; }
            public bool MainMouseLeft { get; internal set; }
            public bool MainMouseLeftReleaseCaptured { get; internal set; }
            public bool MainMouseLeftRelease { get; internal set; }
            public bool MainMouseRightCaptured { get; internal set; }
            public bool MainMouseRight { get; internal set; }
            public bool MainMouseRightReleaseCaptured { get; internal set; }
            public bool MainMouseRightRelease { get; internal set; }
            public bool SuppressedPhysicalInput { get; internal set; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                TerrariaInputCompat.TryRestoreScopedUseItemTakeover(this);
            }
        }

        public static bool TrySetUseItem(object player, bool pressed)
        {
            // Controlled input write: player.controlUseItem / player.releaseUseItem.
            var ok = SetMember(player, "controlUseItem", pressed);
            if (pressed)
            {
                SetMember(player, "releaseUseItem", false);
            }

            return ok;
        }

        public static bool TryApplyUseItemTakeover(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item takeover: player unavailable.");
            }

            var ok = ApplyUseItemTakeoverFields(player, pressed);

            return ok ? ClearInputError() : Fail("Cannot apply use item takeover input override.");
        }

        public static bool TryApplyUseItemClickTakeover(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item click takeover: player unavailable.");
            }

            var ok = ApplyUseItemTakeoverFields(player, pressed, false);

            return ok ? ClearInputError() : Fail("Cannot apply use item click takeover input override.");
        }

        public static bool TryBeginScopedUseItemTakeover(object player, bool pressed, string scopeName, out ScopedUseItemTakeover takeover)
        {
            return TryBeginScopedUseItemTakeover(player, pressed, scopeName, true, false, out takeover);
        }

        public static bool TryBeginScopedUseItemClickTakeover(object player, bool pressed, string scopeName, out ScopedUseItemTakeover takeover)
        {
            return TryBeginScopedUseItemTakeover(player, pressed, scopeName, false, false, out takeover);
        }

        public static bool TryBeginScopedUseItemClickTakeoverSuppressingRightClick(object player, bool pressed, string scopeName, out ScopedUseItemTakeover takeover)
        {
            return TryBeginScopedUseItemTakeover(player, pressed, scopeName, false, true, out takeover);
        }

        private static bool TryBeginScopedUseItemTakeover(object player, bool pressed, string scopeName, bool applyChannel, bool suppressRightClick, out ScopedUseItemTakeover takeover)
        {
            takeover = null;
            if (player == null)
            {
                return Fail("Cannot begin scoped use item takeover: player unavailable.");
            }

            var state = new ScopedUseItemTakeover(player, pressed, scopeName);
            // Capture before writing so failed or completed takeover can fail
            // closed back to vanilla input state.
            bool value;
            if (TryGetBool(player, "controlUseItem", out value))
            {
                state.PlayerControlUseItemCaptured = true;
                state.PlayerControlUseItem = value;
            }

            if (TryGetBool(player, "releaseUseItem", out value))
            {
                state.PlayerReleaseUseItemCaptured = true;
                state.PlayerReleaseUseItem = value;
            }

            if (applyChannel && TryGetBool(player, "channel", out value))
            {
                state.PlayerChannelCaptured = true;
                state.PlayerChannel = value;
            }

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                if (TryGetStaticBool(mainType, "mouseLeft", out value))
                {
                    state.MainMouseLeftCaptured = true;
                    state.MainMouseLeft = value;
                }

                if (TryGetStaticBool(mainType, "mouseLeftRelease", out value))
                {
                    state.MainMouseLeftReleaseCaptured = true;
                    state.MainMouseLeftRelease = value;
                }

                if (suppressRightClick && TryGetStaticBool(mainType, "mouseRight", out value))
                {
                    state.MainMouseRightCaptured = true;
                    state.MainMouseRight = value;
                }

                if (suppressRightClick && TryGetStaticBool(mainType, "mouseRightRelease", out value))
                {
                    state.MainMouseRightReleaseCaptured = true;
                    state.MainMouseRightRelease = value;
                }
            }

            state.Captured = state.PlayerControlUseItemCaptured ||
                             state.PlayerReleaseUseItemCaptured ||
                             state.PlayerChannelCaptured ||
                             state.MainMouseLeftCaptured ||
                             state.MainMouseLeftReleaseCaptured ||
                             state.MainMouseRightCaptured ||
                             state.MainMouseRightReleaseCaptured;
            state.SuppressedPhysicalInput = !pressed &&
                                            ((state.PlayerControlUseItemCaptured && state.PlayerControlUseItem) ||
                                             (state.MainMouseLeftCaptured && state.MainMouseLeft));

            if (!state.PlayerControlUseItemCaptured || !state.PlayerReleaseUseItemCaptured)
            {
                return Fail("Cannot begin scoped use item takeover: player use item fields unavailable.");
            }

            if (!ApplyUseItemTakeoverFields(player, pressed, applyChannel, suppressRightClick))
            {
                TryRestoreScopedUseItemTakeover(state);
                return Fail("Cannot begin scoped use item takeover: " + LastInputCompatError);
            }

            state.Applied = true;
            takeover = state;
            return ClearInputError();
        }

        public static bool TryRestoreScopedUseItemTakeover(ScopedUseItemTakeover takeover)
        {
            if (takeover == null || !takeover.Captured || takeover.Player == null)
            {
                return false;
            }

            var ok = true;
            if (takeover.PlayerControlUseItemCaptured)
            {
                ok &= SetMember(takeover.Player, "controlUseItem", takeover.PlayerControlUseItem);
            }

            if (takeover.PlayerReleaseUseItemCaptured)
            {
                ok &= SetMember(takeover.Player, "releaseUseItem", takeover.PlayerReleaseUseItem);
            }

            if (takeover.PlayerChannelCaptured)
            {
                ok &= TrySetMemberIfExists(takeover.Player, "channel", takeover.PlayerChannel);
            }

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                if (takeover.MainMouseLeftCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeft", takeover.MainMouseLeft);
                }

                if (takeover.MainMouseLeftReleaseCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeftRelease", takeover.MainMouseLeftRelease);
                }

                if (takeover.MainMouseRightCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseRight", takeover.MainMouseRight);
                }

                if (takeover.MainMouseRightReleaseCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseRightRelease", takeover.MainMouseRightRelease);
                }
            }

            // Restore every captured Player/Main field; partial restore reports
            // failure instead of pretending the scoped click succeeded.
            return ok ? ClearInputError() : Fail("Cannot restore scoped use item takeover.");
        }

        public static bool TryReadUseItemHeld(object player, out bool held)
        {
            held = false;
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return ClearInputError();
            }

            if (player == null)
            {
                return Fail("Cannot read use item state: player unavailable.");
            }

            if (TryGetBool(player, "controlUseItem", out held))
            {
                held = held || IsSuppressedUseItemHeld() || IsLeftButtonDownFallback();
                return ClearInputError();
            }

            if (IsSuppressedUseItemHeld())
            {
                held = true;
                return ClearInputError();
            }

            if (IsLeftButtonDownFallback())
            {
                held = true;
                return ClearInputError();
            }

            return Fail("Cannot read player.controlUseItem.");
        }

        public static bool TryReadUseItemReleased(object player, out bool released)
        {
            released = false;
            if (player == null)
            {
                return Fail("Cannot read use item release state: player unavailable.");
            }

            return TryGetBool(player, "releaseUseItem", out released)
                ? ClearInputError()
                : Fail("Cannot read player.releaseUseItem.");
        }

        public static bool TryReadDelayUseItem(object player, out bool delayUseItem)
        {
            delayUseItem = false;
            if (player == null)
            {
                return Fail("Cannot read delayUseItem: player unavailable.");
            }

            if (TryGetBool(player, "delayUseItem", out delayUseItem))
            {
                return ClearInputError();
            }

            delayUseItem = false;
            return ClearInputError();
        }

        public static bool TryReadCombatAimUseInputSnapshot(object player, out CombatAimUseInputSnapshot snapshot)
        {
            snapshot = new CombatAimUseInputSnapshot();
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                snapshot.Available = true;
                snapshot.UseItemHeld = false;
                snapshot.UseItemReleased = true;
                snapshot.Reason = "gameInputUnavailable";
                return ClearInputError();
            }

            if (player == null)
            {
                snapshot.Reason = "playerUnavailable";
                return Fail("Cannot read combat aim use input: player unavailable.");
            }

            try
            {
                bool held;
                if (!TryGetBool(player, "controlUseItem", out held))
                {
                    snapshot.Reason = "useItemHeldUnavailable";
                    return Fail("Cannot read player.controlUseItem.");
                }

                bool released;
                if (!TryGetBool(player, "releaseUseItem", out released))
                {
                    snapshot.Reason = "useItemReleasedUnavailable";
                    return Fail("Cannot read player.releaseUseItem.");
                }

                int itemAnimation;
                int itemTime;
                TryGetInt(player, "itemAnimation", out itemAnimation);
                TryGetInt(player, "itemTime", out itemTime);

                int selectedSlot;
                TryGetSelectedItem(player, out selectedSlot);

                var itemType = 0;
                var inventory = GetMember(player, "inventory") as IList;
                if (inventory != null && selectedSlot >= 0 && selectedSlot < inventory.Count)
                {
                    var item = inventory[selectedSlot];
                    if (item != null)
                    {
                        TryGetInt(item, "type", out itemType);
                    }
                }

                long gameUpdateCount;
                TryReadGameUpdateCount(out gameUpdateCount);

                snapshot.Available = true;
                snapshot.UseItemHeld = held || IsSuppressedUseItemHeld();
                snapshot.UseItemReleased = released;
                snapshot.ItemAnimation = itemAnimation;
                snapshot.ItemTime = itemTime;
                snapshot.SelectedSlot = selectedSlot;
                snapshot.ItemType = itemType;
                snapshot.GameUpdateCount = gameUpdateCount;
                snapshot.Reason = string.Empty;
                return ClearInputError();
            }
            catch (Exception error)
            {
                snapshot.Reason = "snapshotFailed:" + error.Message;
                return Fail("Read combat aim use input failed: " + error.Message);
            }
        }

        public static bool TryReadPhysicalUseItemHeld(object player, out bool held)
        {
            held = false;
            if (player == null)
            {
                return Fail("Cannot read physical use item state: player unavailable.");
            }

            bool controlUseItem;
            TryGetBool(player, "controlUseItem", out controlUseItem);

            bool mainMouseLeft;
            TryGetStaticBool(TerrariaRuntimeTypes.MainType, "mouseLeft", out mainMouseLeft);

            held = controlUseItem || mainMouseLeft || IsLeftButtonDownFallback();
            return ClearInputError();
        }

        public static bool TryReadPhysicalMouseLeftHeld(out bool held)
        {
            held = IsLeftButtonDownFallback();
            try
            {
                var playerInputType = FindType("Terraria.GameInput.PlayerInput");
                if (playerInputType == null)
                {
                    return ClearInputError();
                }

                var triggers = GetStatic(playerInputType, "Triggers");
                var current = triggers == null ? null : GetMember(triggers, "Current");
                bool mouseLeft;
                if (current != null && TryGetBool(current, "MouseLeft", out mouseLeft))
                {
                    held = held || mouseLeft;
                }

                return ClearInputError();
            }
            catch (Exception error)
            {
                return Fail("Cannot read physical mouse left state: " + error.Message);
            }
        }

        public static bool TryReadPhysicalMouseRightHeld(out bool held)
        {
            held = IsRightButtonDownFallback();
            try
            {
                bool mainMouseRight;
                if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "mouseRight", out mainMouseRight))
                {
                    held = held || mainMouseRight;
                }

                var playerInputType = FindType("Terraria.GameInput.PlayerInput");
                if (playerInputType == null)
                {
                    return ClearInputError();
                }

                var triggers = GetStatic(playerInputType, "Triggers");
                var current = triggers == null ? null : GetMember(triggers, "Current");
                bool mouseRight;
                if (current != null && TryGetBool(current, "MouseRight", out mouseRight))
                {
                    held = held || mouseRight;
                }

                return ClearInputError();
            }
            catch (Exception error)
            {
                return Fail("Cannot read physical mouse right state: " + error.Message);
            }
        }

        public static bool TrySuppressHeldUseItemForPerfectRevolver(object player)
        {
            var ok = SuppressHeldUseItemForQueuedCombat(player);
            ArmPerfectRevolverSuppressedUseItemHeld();
            return ok;
        }

        public static void ClearPerfectRevolverSuppressedUseItem()
        {
            lock (AutoClickerUseItemSuppressionSync)
            {
                _perfectRevolverSuppressedUseItemHeldUntilUtc = DateTime.MinValue;
            }
        }

        public static bool TryReadGameUpdateCount(out long gameUpdateCount)
        {
            gameUpdateCount = 0;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return Fail("Cannot read Main.GameUpdateCount: Terraria.Main unavailable.");
            }

            try
            {
                var raw = GetStatic(mainType, "GameUpdateCount");
                if (raw == null)
                {
                    raw = GetStatic(mainType, "gameUpdateCount");
                }

                if (raw == null)
                {
                    return Fail("Cannot read Main.GameUpdateCount.");
                }

                gameUpdateCount = Convert.ToInt64(raw);
                return ClearInputError();
            }
            catch (Exception error)
            {
                return Fail("Read Main.GameUpdateCount failed: " + error.Message);
            }
        }

        public static TerrariaUiInputContext ReadUiInputContext(object player)
        {
            var context = new TerrariaUiInputContext();
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                context.MainTypeUnavailable = true;
                return context;
            }

            bool value;
            context.GameMenu = TryGetStaticBool(mainType, "gameMenu", out value) && value;
            context.ChatOpen = (TryGetStaticBool(mainType, "chatMode", out value) && value) ||
                               (TryGetStaticBool(mainType, "drawingPlayerChat", out value) && value);

            var npcChatText = GetStatic(mainType, "npcChatText");
            context.NpcChatOpen = npcChatText != null && !string.IsNullOrEmpty(npcChatText.ToString());
            context.PlayerInventoryOpen = TryGetStaticBool(mainType, "playerInventory", out value) && value;

            int chest;
            context.ChestOpen = TryGetInt(player, "chest", out chest) && chest >= 0;
            context.PlayerMouseInterface = TryGetBool(player, "mouseInterface", out value) && value;
            context.MainMouseInterface = TryGetStaticBool(mainType, "mouseInterface", out value) && value;
            context.MainBlockMouse = TryGetStaticBool(mainType, "blockMouse", out value) && value;
            return context;
        }

        public static bool IsMouseInputCapturedByUi(object player, out string reason)
        {
            var context = ReadUiInputContext(player);
            if (context.MainTypeUnavailable)
            {
                reason = "mainTypeUnavailable";
                return true;
            }

            reason = context.MouseCaptureReason;
            return context.MouseCapturedByUi;
        }

        public static bool IsInputBlockingUiActive(object player, out string reason)
        {
            var context = ReadUiInputContext(player);
            if (context.MainTypeUnavailable)
            {
                reason = "mainTypeUnavailable";
                return true;
            }

            if (context.GameMenu)
            {
                reason = "gameMenu";
                return true;
            }

            if (context.MouseCapturedByUi)
            {
                reason = context.MouseCaptureReason;
                return true;
            }

            reason = string.Empty;
            return false;
        }

        public static bool IsWorldRightClickInteractionActive(object player, out string reason)
        {
            var mainType = TerrariaRuntimeTypes.MainType ?? FindType("Terraria.Main");
            if (mainType == null)
            {
                reason = "mainTypeUnavailable";
                return true;
            }

            bool value;
            if (TryGetStaticBool(mainType, "SmartInteractShowingGenuine", out value) && value)
            {
                reason = "smartInteractGenuine";
                return true;
            }

            int target;
            if ((TryGetStaticInt(mainType, "SmartInteractNPC", out target) && target != -1) ||
                (TryGetStaticInt(mainType, "SmartInteractProj", out target) && target != -1))
            {
                reason = "smartInteractTarget";
                return true;
            }

            if (TryGetBool(player, "tileInteractionHappened", out value) && value)
            {
                reason = "tileInteractionHappened";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        public static bool TryIsLocalPlayer(object player)
        {
            if (player == null)
            {
                return false;
            }

            object localPlayer;
            if (TryGetLocalPlayer(out localPlayer) && ReferenceEquals(localPlayer, player))
            {
                return true;
            }

            try
            {
                int whoAmI;
                var rawMyPlayer = GetStatic(TerrariaRuntimeTypes.MainType, "myPlayer");
                if (rawMyPlayer != null && TryGetInt(player, "whoAmI", out whoAmI))
                {
                    return whoAmI == Convert.ToInt32(rawMyPlayer);
                }
            }
            catch
            {
            }

            return false;
        }

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

        private static bool ApplyUseItemTakeoverFields(object player, bool pressed)
        {
            return ApplyUseItemTakeoverFields(player, pressed, true);
        }

        private static bool ApplyUseItemTakeoverFields(object player, bool pressed, bool applyChannel)
        {
            return ApplyUseItemTakeoverFields(player, pressed, applyChannel, false);
        }

        private static bool ApplyUseItemTakeoverFields(object player, bool pressed, bool applyChannel, bool suppressRightClick)
        {
            // Controlled input write: combat takeover must override both Player and Main mouse use state for this tick/scope.
            var ok = SetMember(player, "controlUseItem", pressed);
            ok &= SetMember(player, "releaseUseItem", true);
            if (applyChannel)
            {
                TrySetMemberIfExists(player, "channel", pressed);
            }

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                TrySetStaticIfExists(mainType, "mouseLeft", pressed);
                TrySetStaticIfExists(mainType, "mouseLeftRelease", true);
                if (suppressRightClick)
                {
                    TrySetStaticIfExists(mainType, "mouseRight", false);
                    TrySetStaticIfExists(mainType, "mouseRightRelease", false);
                }
            }

            return ok;
        }

        private static Type ResolveMainTypeForInputWrite()
        {
            return FindType("Terraria.Main");
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

        private static bool SuppressHeldUseItemForQueuedCombat(object player)
        {
            var ok = SetMember(player, "controlUseItem", false);
            ok &= SetMember(player, "releaseUseItem", true);

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType != null)
            {
                TrySetStaticIfExists(mainType, "mouseLeft", false);
                TrySetStaticIfExists(mainType, "mouseLeftRelease", false);
            }

            return ok;
        }

        private static void ArmPerfectRevolverSuppressedUseItemHeld()
        {
            lock (AutoClickerUseItemSuppressionSync)
            {
                _perfectRevolverSuppressedUseItemHeldUntilUtc = DateTime.UtcNow.AddMilliseconds(250);
            }
        }

        private static bool IsSuppressedUseItemHeld()
        {
            lock (AutoClickerUseItemSuppressionSync)
            {
                var now = DateTime.UtcNow;
                var perfectRevolverHeld = _perfectRevolverSuppressedUseItemHeldUntilUtc > now;
                if (!perfectRevolverHeld)
                {
                    _perfectRevolverSuppressedUseItemHeldUntilUtc = DateTime.MinValue;
                }

                return perfectRevolverHeld;
            }
        }

        private static bool IsLeftButtonDownFallback()
        {
            if (_physicalMouseLeftHeldOverrideForTesting.HasValue)
            {
                return _physicalMouseLeftHeldOverrideForTesting.Value;
            }

            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            try
            {
                return (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRightButtonDownFallback()
        {
            if (_physicalMouseRightHeldOverrideForTesting.HasValue)
            {
                return _physicalMouseRightHeldOverrideForTesting.Value;
            }

            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            try
            {
                return (GetAsyncKeyState(VkRightButton) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }
}
