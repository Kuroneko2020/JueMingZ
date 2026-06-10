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
        public static bool TrySetMouseScreenPosition(int x, int y)
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return Fail("Terraria.Main unavailable.");
            }

            // Controlled input write: Main.mouseX / Main.mouseY are only written here.
            var ok = SetStatic(mainType, "mouseX", x) & SetStatic(mainType, "mouseY", y);
            TrySetPlayerInputMousePosition(x, y);
            float screenX;
            float screenY;
            if (TryGetScreenPosition(out screenX, out screenY))
            {
                TrySetTileTargetWorldPosition(x + screenX, y + screenY);
            }

            return ok;
        }

        public static bool TryGetScreenPosition(out float x, out float y)
        {
            x = 0f;
            y = 0f;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return Fail("Terraria.Main unavailable.");
            }

            var screenPosition = GetStatic(mainType, "screenPosition");
            return TryReadVector2(screenPosition, out x, out y);
        }

        public static bool TryWorldToScreen(float worldX, float worldY, out int screenX, out int screenY)
        {
            screenX = 0;
            screenY = 0;
            if (!TryGetScreenPosition(out var screenXFloat, out var screenYFloat))
            {
                return false;
            }

            screenX = (int)Math.Round(worldX - screenXFloat);
            screenY = (int)Math.Round(worldY - screenYFloat);
            return true;
        }

        public static bool TrySetMouseWorldPosition(float worldX, float worldY)
        {
            int screenX;
            int screenY;
            if (!TryWorldToScreen(worldX, worldY, out screenX, out screenY))
            {
                return false;
            }

            var ok = TrySetMouseScreenPosition(screenX, screenY);
            TrySetTileTargetWorldPosition(worldX, worldY);
            return ok;
        }

        public static bool TryCaptureMouseTargetState(out MouseTargetInputState state)
        {
            return TryCaptureMouseTargetState(null, out state);
        }

        public static bool TryReadTileTarget(out int tileX, out int tileY)
        {
            tileX = -1;
            tileY = -1;
            if (!EnsureTileTargetAccessors())
            {
                return Fail("Cannot read Player.tileTargetX/Y.");
            }

            return TryGetOptionalStatic(_tileTargetXField, null, out tileX) &&
                   TryGetOptionalStatic(_tileTargetYField, null, out tileY)
                ? ClearInputError()
                : Fail("Cannot read Player.tileTargetX/Y.");
        }

        public static bool TryCaptureMouseTargetState(object player, out MouseTargetInputState state)
        {
            state = new MouseTargetInputState();
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

                // Smart cursor and tile-use fields are intent state, not proof
                // that Terraria accepted a tile interaction.
                CaptureSmartInteractionState(state);
                CaptureTileInteractionInputState(player, state);
                state.Captured = true;
                return true;
            }
            catch (Exception error)
            {
                return Fail("Capture mouse target state failed: " + error.Message);
            }
        }

        public static bool TryApplyMouseTargetOverride(MouseTargetInputState state)
        {
            if (state == null || !state.Captured)
            {
                return Fail("Cannot apply mouse target override: state was not captured.");
            }

            return TrySetMouseScreenPosition(state.MouseX + 1, state.MouseY);
        }

        public static bool TryRestoreMouseTargetState(MouseTargetInputState state)
        {
            if (state == null || !state.Captured)
            {
                return Fail("Cannot restore mouse target state: state was not captured.");
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

            RestoreSmartInteractionState(state);
            RestoreTileInteractionInputState(state);
            return ok;
        }

        private static bool TrySyncPlayerInputJumpTriggers(bool currentJump, bool justPressed, bool justReleased, out string message)
        {
            return TrySyncPlayerInputTrigger("Jump", currentJump, justPressed, justReleased, out message);
        }

        private static bool TrySyncPlayerInputTrigger(string triggerName, bool current, bool justPressed, bool justReleased, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                message = "PlayerInput trigger sync skipped: trigger name is empty.";
                return false;
            }

            object triggersPack;
            if (!TryGetPlayerInputTriggersPack(out triggersPack, out message))
            {
                return false;
            }

            var currentSynced = TrySetPlayerInputTriggerSet(triggersPack, "Current", triggerName, current);
            var justPressedSynced = TrySetPlayerInputTriggerSet(triggersPack, "JustPressed", triggerName, justPressed);
            var justReleasedSynced = TrySetPlayerInputTriggerSet(triggersPack, "JustReleased", triggerName, justReleased);
            var oldSynced = true;
            if (justPressed)
            {
                oldSynced = TrySetPlayerInputTriggerSet(triggersPack, "Old", triggerName, false);
            }
            else if (justReleased)
            {
                oldSynced = TrySetPlayerInputTriggerSet(triggersPack, "Old", triggerName, true);
            }

            var synced = currentSynced && justPressedSynced && justReleasedSynced && oldSynced;
            message = synced
                ? "PlayerInput " + triggerName + " triggers synced."
                : "PlayerInput " + triggerName + " trigger sync incomplete.";
            return synced;
        }

        private static bool TryGetPlayerInputTriggersPack(out object triggersPack, out string message)
        {
            triggersPack = null;
            message = string.Empty;
            if (!EnsurePlayerInputJumpTriggerAccessors())
            {
                message = "PlayerInput jump trigger sync skipped: PlayerInput.Triggers unavailable.";
                return false;
            }

            try
            {
                if (_playerInputTriggersField != null)
                {
                    triggersPack = _playerInputTriggersField.GetValue(null);
                }
                else if (_playerInputTriggersProperty != null && _playerInputTriggersProperty.CanRead)
                {
                    triggersPack = _playerInputTriggersProperty.GetValue(null, null);
                }

                if (triggersPack == null)
                {
                    message = "PlayerInput jump trigger sync skipped: Triggers pack is null.";
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                message = "PlayerInput jump trigger sync failed: " + error.Message;
                Logger.Debug("TerrariaInputCompat", message);
                return false;
            }
        }

        private static bool TrySetPlayerInputJumpTriggerSet(object triggersPack, string setName, bool value)
        {
            return TrySetPlayerInputTriggerSet(triggersPack, setName, "Jump", value);
        }

        private static bool TrySetPlayerInputTriggerSet(object triggersPack, string setName, string triggerName, bool value)
        {
            var triggerSet = GetMember(triggersPack, setName);
            return triggerSet != null && TrySetMemberIfExists(triggerSet, triggerName, value);
        }

        private static bool EnsurePlayerInputJumpTriggerAccessors()
        {
            if (_playerInputJumpTriggersResolved)
            {
                return _playerInputTriggersField != null || _playerInputTriggersProperty != null;
            }

            _playerInputJumpTriggersResolved = true;
            var playerInputType = FindType("Terraria.GameInput.PlayerInput");
            if (playerInputType == null)
            {
                Logger.Debug("TerrariaInputCompat", "Terraria.GameInput.PlayerInput not found; jump trigger sync skipped.");
                return false;
            }

            _playerInputTriggersField = playerInputType.GetField("Triggers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _playerInputTriggersProperty = _playerInputTriggersField == null
                ? playerInputType.GetProperty("Triggers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                : null;
            return _playerInputTriggersField != null || _playerInputTriggersProperty != null;
        }

        private static string AppendPlayerInputTriggerSyncMessage(string message, bool synced, string syncMessage)
        {
            if (string.IsNullOrWhiteSpace(syncMessage))
            {
                return message ?? string.Empty;
            }

            return (message ?? string.Empty) + " " + syncMessage;
        }

        private static void TrySetPlayerInputMousePosition(int x, int y)
        {
            if (!EnsurePlayerInputMouseAccessors())
            {
                return;
            }

            TrySetOptionalStatic(_playerInputMouseXField, _playerInputMouseXProperty, x);
            TrySetOptionalStatic(_playerInputMouseYField, _playerInputMouseYProperty, y);
        }

        private static bool EnsurePlayerInputMouseAccessors()
        {
            if (_playerInputMouseResolved)
            {
                return _playerInputMouseXField != null || _playerInputMouseXProperty != null;
            }

            _playerInputMouseResolved = true;
            _playerInputType = FindType("Terraria.GameInput.PlayerInput");
            if (_playerInputType == null)
            {
                Logger.Debug("TerrariaInputCompat", "Terraria.GameInput.PlayerInput not found; mouse sync fallback skipped.");
                return false;
            }

            _playerInputMouseXField = _playerInputType.GetField("MouseX", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _playerInputMouseYField = _playerInputType.GetField("MouseY", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _playerInputMouseXProperty = _playerInputMouseXField == null
                ? _playerInputType.GetProperty("MouseX", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                : null;
            _playerInputMouseYProperty = _playerInputMouseYField == null
                ? _playerInputType.GetProperty("MouseY", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                : null;
            return _playerInputMouseXField != null || _playerInputMouseXProperty != null;
        }

        private static void TrySetTileTargetWorldPosition(float worldX, float worldY)
        {
            if (!EnsureTileTargetAccessors())
            {
                return;
            }

            var tileX = (int)(worldX / 16f);
            var tileY = (int)(worldY / 16f);
            tileX = Clamp(tileX, 0, GetStaticInt(TerrariaRuntimeTypes.MainType, "maxTilesX", tileX + 1) - 1);
            tileY = Clamp(tileY, 0, GetStaticInt(TerrariaRuntimeTypes.MainType, "maxTilesY", tileY + 1) - 1);
            TrySetOptionalStatic(_tileTargetXField, null, tileX);
            TrySetOptionalStatic(_tileTargetYField, null, tileY);
        }

        private static void CaptureSmartInteractionState(MouseTargetInputState state)
        {
            if (state == null || TerrariaRuntimeTypes.MainType == null)
            {
                return;
            }

            bool genuine;
            bool fake;
            bool wantedMouse;
            bool wantedGamePad;
            bool showing;
            var captured = false;
            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartInteractShowingGenuine", out genuine))
            {
                state.SmartInteractShowingGenuine = genuine;
                captured = true;
            }

            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartInteractShowingFake", out fake))
            {
                state.SmartInteractShowingFake = fake;
                captured = true;
            }

            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartCursorWanted_Mouse", out wantedMouse))
            {
                state.SmartCursorWantedMouse = wantedMouse;
                captured = true;
            }

            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartCursorWanted_GamePad", out wantedGamePad))
            {
                state.SmartCursorWantedGamePad = wantedGamePad;
                captured = true;
            }

            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartCursorShowing", out showing))
            {
                state.SmartCursorShowing = showing;
                captured = true;
            }

            state.SmartStateCaptured = captured;
        }

        private static void SuppressSmartInteractionState()
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return;
            }

            SetOptionalStatic(mainType, "SmartInteractShowingGenuine", false);
            SetOptionalStatic(mainType, "SmartInteractShowingFake", false);
            SetOptionalStatic(mainType, "SmartCursorWanted_Mouse", false);
            SetOptionalStatic(mainType, "SmartCursorWanted_GamePad", false);
            SetOptionalStatic(mainType, "SmartCursorShowing", false);
        }

        private static void RestoreSmartInteractionState(MouseTargetInputState state)
        {
            if (state == null || !state.SmartStateCaptured || TerrariaRuntimeTypes.MainType == null)
            {
                return;
            }

            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartInteractShowingGenuine", state.SmartInteractShowingGenuine);
            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartInteractShowingFake", state.SmartInteractShowingFake);
            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartCursorWanted_Mouse", state.SmartCursorWantedMouse);
            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartCursorWanted_GamePad", state.SmartCursorWantedGamePad);
            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartCursorShowing", state.SmartCursorShowing);
        }

        private static void CaptureTileInteractionInputState(object player, MouseTargetInputState state)
        {
            if (player == null || state == null)
            {
                return;
            }

            bool controlUseTile;
            bool releaseUseTile;
            bool tileInteractAttempted;
            var captured = false;
            if (TryGetBool(player, "controlUseTile", out controlUseTile))
            {
                state.ControlUseTile = controlUseTile;
                captured = true;
            }

            if (TryGetBool(player, "releaseUseTile", out releaseUseTile))
            {
                state.ReleaseUseTile = releaseUseTile;
                captured = true;
            }

            if (TryGetBool(player, "tileInteractAttempted", out tileInteractAttempted))
            {
                state.TileInteractAttempted = tileInteractAttempted;
                captured = true;
            }

            state.TileInteractionInputCaptured = captured;
        }

        private static void RestoreTileInteractionInputState(MouseTargetInputState state)
        {
            if (state == null || !state.TileInteractionInputCaptured)
            {
                return;
            }

            object player;
            if (!TryGetLocalPlayer(out player) || player == null)
            {
                return;
            }

            // Restore captured tile intent exactly; leaving these primed can
            // leak a queued tile action into later vanilla input.
            SetMember(player, "controlUseTile", state.ControlUseTile);
            SetMember(player, "releaseUseTile", state.ReleaseUseTile);
            SetMember(player, "tileInteractAttempted", state.TileInteractAttempted);
        }

        private static void SetOptionalStatic(Type type, string name, object value)
        {
            if (type == null)
            {
                return;
            }

            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    field.SetValue(null, value);
                }
            }
            catch (Exception error)
            {
                if (LogThrottle.ShouldLog("optional-smart-static-set-failed", TimeSpan.FromSeconds(30)))
                {
                    Logger.Debug("TerrariaInputCompat", "Optional smart static set failed for " + name + ": " + error.Message);
                }
            }
        }

        private static bool EnsureTileTargetAccessors()
        {
            if (_tileTargetResolved)
            {
                return _tileTargetXField != null && _tileTargetYField != null;
            }

            _tileTargetResolved = true;
            var playerType = TerrariaRuntimeTypes.PlayerType;
            if (playerType == null)
            {
                return false;
            }

            _tileTargetXField = playerType.GetField("tileTargetX", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _tileTargetYField = playerType.GetField("tileTargetY", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (_tileTargetXField == null || _tileTargetYField == null)
            {
                Logger.Debug("TerrariaInputCompat", "Player.tileTargetX/Y not found; tile target sync skipped.");
            }

            return _tileTargetXField != null && _tileTargetYField != null;
        }

        private static bool EnsureTileInteractionUseMethod(object player)
        {
            if (_tileInteractionUseResolved)
            {
                return _tileInteractionUseMethod != null;
            }

            _tileInteractionUseResolved = true;
            if (player == null)
            {
                return false;
            }

            _tileInteractionUseMethod = player.GetType().GetMethod(
                "TileInteractionsUse",
                InstanceMemberFlags,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            return _tileInteractionUseMethod != null;
        }

        private static bool EnsureTileInteractionCheckMethod(object player)
        {
            if (_tileInteractionCheckResolved)
            {
                return _tileInteractionCheckMethod != null;
            }

            _tileInteractionCheckResolved = true;
            if (player == null)
            {
                return false;
            }

            _tileInteractionCheckMethod = player.GetType().GetMethod(
                "TileInteractionsCheck",
                InstanceMemberFlags,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            return _tileInteractionCheckMethod != null;
        }
    }
}
