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
        public static bool TrySetUseTile(object player, bool pressed)
        {
            // Controlled input write: player.controlUseTile / player.releaseUseTile.
            var ok = SetMember(player, "controlUseTile", pressed);
            if (pressed)
            {
                SetMember(player, "releaseUseTile", true);
            }

            return ok;
        }

        public static bool TryPrimeTileInteractionAttempt(object player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot prime tile interaction: player unavailable.";
                return Fail(message);
            }

            // Controlled input write: these fields express right-click tile
            // intent only; they are not confirmation of a vanilla interaction.
            var ok = SetMember(player, "controlUseTile", true);
            ok &= SetMember(player, "releaseUseTile", true);
            ok &= SetMember(player, "tileInteractAttempted", true);
            message = ok
                ? "Tile interaction input primed."
                : "Tile interaction input prime failed: " + LastInputCompatError;
            return ok;
        }

        public static void BeginTileInteractionOverride(Guid requestId, int tileX, int tileY)
        {
            lock (TileInteractionOverrideSync)
            {
                _tileInteractionOverrideRequestId = requestId;
                _tileInteractionOverrideTileX = tileX;
                _tileInteractionOverrideTileY = tileY;
                _tileInteractionOverrideExpiresUtc = DateTime.UtcNow.AddSeconds(2);
                _lastTileInteractionOverrideMessage = "Tile interaction mouse override armed for tile " + tileX + "," + tileY + ".";
            }
        }

        public static void EndTileInteractionOverride(Guid requestId)
        {
            lock (TileInteractionOverrideSync)
            {
                if (_tileInteractionOverrideRequestId != Guid.Empty && _tileInteractionOverrideRequestId != requestId)
                {
                    return;
                }

                _tileInteractionOverrideRequestId = Guid.Empty;
                _tileInteractionOverrideTileX = -1;
                _tileInteractionOverrideTileY = -1;
                _tileInteractionOverrideExpiresUtc = DateTime.MinValue;
            }
        }

        public static bool TryApplyTileInteractionOverride(object player, out MouseTargetInputState restoreState, out string message)
        {
            restoreState = null;
            message = string.Empty;
            int tileX;
            int tileY;
            lock (TileInteractionOverrideSync)
            {
                if (_tileInteractionOverrideRequestId == Guid.Empty)
                {
                    return false;
                }

                if (DateTime.UtcNow > _tileInteractionOverrideExpiresUtc)
                {
                    _tileInteractionOverrideRequestId = Guid.Empty;
                    _tileInteractionOverrideTileX = -1;
                    _tileInteractionOverrideTileY = -1;
                    _tileInteractionOverrideExpiresUtc = DateTime.MinValue;
                    message = "Tile interaction mouse override expired.";
                    _lastTileInteractionOverrideMessage = message;
                    return false;
                }

                tileX = _tileInteractionOverrideTileX;
                tileY = _tileInteractionOverrideTileY;
            }

            if (player == null || !TryIsLocalPlayer(player))
            {
                message = "Tile interaction mouse override skipped for non-local player.";
                _lastTileInteractionOverrideMessage = message;
                return false;
            }

            // The override is scoped to this queued tile action and must leave
            // mouse, smart cursor, and tile intent state restorable.
            if (!TryCaptureMouseTargetState(player, out restoreState))
            {
                message = "Tile interaction mouse override capture failed: " + LastInputCompatError;
                _lastTileInteractionOverrideMessage = message;
                return false;
            }

            var worldX = tileX * 16f + 8f;
            var worldY = tileY * 16f + 8f;
            if (!TrySetMouseWorldPosition(worldX, worldY))
            {
                TryRestoreMouseTargetState(restoreState);
                restoreState = null;
                message = "Tile interaction mouse override apply failed: " + LastInputCompatError;
                _lastTileInteractionOverrideMessage = message;
                return false;
            }

            SuppressSmartInteractionState();
            string primeMessage;
            TryPrimeTileInteractionAttempt(player, out primeMessage);
            message = "Tile interaction mouse override applied for tile " + tileX + "," + tileY + ".";
            _lastTileInteractionOverrideMessage = message;
            return true;
        }

        public static bool TryInvokeTileInteractionUse(object player, int tileX, int tileY, out bool invoked, out string message)
        {
            invoked = false;
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot invoke tile interaction: player unavailable.";
                return Fail(message);
            }

            if (!EnsureTileInteractionUseMethod(player))
            {
                message = "Player.TileInteractionsUse(int,int) was not found.";
                return Fail(message);
            }

            try
            {
                _tileInteractionUseMethod.Invoke(player, new object[] { tileX, tileY });
                invoked = true;
                message = "Player.TileInteractionsUse invoked.";
                return true;
            }
            catch (Exception error)
            {
                message = "Player.TileInteractionsUse failed: " + (error.InnerException == null ? error.Message : error.InnerException.Message);
                return Fail(message);
            }
        }

        public static bool TryInvokeTileInteractionCheck(object player, int tileX, int tileY, out bool invoked, out string message)
        {
            invoked = false;
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot invoke tile interaction check: player unavailable.";
                return Fail(message);
            }

            if (!EnsureTileInteractionCheckMethod(player))
            {
                message = "Player.TileInteractionsCheck(int,int) was not found.";
                return Fail(message);
            }

            try
            {
                _tileInteractionCheckMethod.Invoke(player, new object[] { tileX, tileY });
                invoked = true;
                message = "Player.TileInteractionsCheck invoked.";
                return true;
            }
            catch (Exception error)
            {
                message = "Player.TileInteractionsCheck failed: " + (error.InnerException == null ? error.Message : error.InnerException.Message);
                return Fail(message);
            }
        }

        public static bool TryInvokeTileInteractionUseAtPlayerPosition(object player, out bool invoked, out int playerTileX, out int playerTileY, out string message)
        {
            invoked = false;
            playerTileX = -1;
            playerTileY = -1;
            if (!TryGetPlayerCenterTile(player, out playerTileX, out playerTileY))
            {
                message = "Cannot invoke tile interaction: player center tile is unavailable.";
                return Fail(message);
            }

            return TryInvokeTileInteractionUse(player, playerTileX, playerTileY, out invoked, out message);
        }

        public static bool TryInvokeTileInteractionCheckAtPlayerPosition(object player, out bool invoked, out int playerTileX, out int playerTileY, out string message)
        {
            invoked = false;
            playerTileX = -1;
            playerTileY = -1;
            if (!TryGetPlayerCenterTile(player, out playerTileX, out playerTileY))
            {
                message = "Cannot invoke tile interaction check: player center tile is unavailable.";
                return Fail(message);
            }

            return TryInvokeTileInteractionCheck(player, playerTileX, playerTileY, out invoked, out message);
        }

        public static bool TryGetPlayerCenterTile(object player, out int tileX, out int tileY)
        {
            tileX = -1;
            tileY = -1;
            if (player == null)
            {
                return Fail("Cannot read player center tile: player unavailable.");
            }

            var position = GetMember(player, "position");
            float x;
            float y;
            if (!TryReadVector2(position, out x, out y))
            {
                return Fail("Cannot read player center tile: player position unavailable.");
            }

            int width;
            int height;
            TryGetInt(player, "width", out width);
            TryGetInt(player, "height", out height);

            tileX = (int)((x + width / 2f) / 16f);
            tileY = (int)((y + height / 2f) / 16f);
            tileX = Clamp(tileX, 0, GetStaticInt(TerrariaRuntimeTypes.MainType, "maxTilesX", tileX + 1) - 1);
            tileY = Clamp(tileY, 0, GetStaticInt(TerrariaRuntimeTypes.MainType, "maxTilesY", tileY + 1) - 1);
            return ClearInputError();
        }

        public static bool TryReleaseUseItem(object player)
        {
            var ok = SetMember(player, "controlUseItem", false);
            SetMember(player, "releaseUseItem", true);
            return ok;
        }

        public static bool TryReleaseUseTile(object player)
        {
            var ok = SetMember(player, "controlUseTile", false);
            SetMember(player, "releaseUseTile", true);
            return ok;
        }

        public static bool TryReadPlayerInventoryOpen(out bool open)
        {
            open = false;
            if (TerrariaRuntimeTypes.MainType == null)
            {
                return Fail("Cannot read Main.playerInventory: Terraria.Main unavailable.");
            }

            return TryGetStaticBool(TerrariaRuntimeTypes.MainType, "playerInventory", out open)
                ? ClearInputError()
                : Fail("Cannot read Main.playerInventory.");
        }

        public static bool TrySetPlayerInventoryOpen(bool open, out string message)
        {
            message = string.Empty;
            if (TerrariaRuntimeTypes.MainType == null)
            {
                message = "Cannot set Main.playerInventory: Terraria.Main unavailable.";
                return Fail(message);
            }

            var ok = SetStatic(TerrariaRuntimeTypes.MainType, "playerInventory", open);
            if (ok)
            {
                ClearInputError();
            }

            message = ok
                ? "Main.playerInventory restored to " + (open ? "open" : "closed") + "."
                : "Main.playerInventory restore failed: " + LastInputCompatError;
            return ok;
        }
    }
}
