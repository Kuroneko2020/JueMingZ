using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class TerrariaMainCompat
    {
        // Main wrappers expose read-mostly vanilla state; UI/input capture
        // writes stay in the dedicated Compat helpers.
        private static bool? _allowsInputProcessingOverrideForTesting;
        private static ulong? _gameUpdateCountOverrideForTesting;

        internal static void SetAllowsInputProcessingOverrideForTesting(bool? value)
        {
            _allowsInputProcessingOverrideForTesting = value;
        }

        internal static void SetGameUpdateCountOverrideForTesting(ulong? value)
        {
            _gameUpdateCountOverrideForTesting = value;
        }

        public static bool IsInMainMenu
        {
            get { return Main.gameMenu; }
        }

        public static bool IsGamePaused
        {
            get { return Main.gamePaused; }
        }

        public static int NetMode
        {
            get { return Main.netMode; }
        }

        public static bool IsDedicatedServer
        {
            get { return Main.dedServ; }
        }

        public static bool AllowsInputProcessing
        {
            get
            {
                if (_allowsInputProcessingOverrideForTesting.HasValue)
                {
                    return _allowsInputProcessingOverrideForTesting.Value;
                }

                bool foreground;
                if (TryIsCurrentProcessForeground(out foreground))
                {
                    return foreground;
                }

                bool focusHelperAllowsInput;
                if (TryReadFocusHelperAllowInput(out focusHelperAllowsInput))
                {
                    return focusHelperAllowsInput;
                }

                return true;
            }
        }

        private static bool TryReadFocusHelperAllowInput(out bool allowsInput)
        {
            allowsInput = true;
            try
            {
                var property = typeof(FocusHelper).GetProperty(
                    "AllowInputProcessing",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (property == null || !property.CanRead)
                {
                    return false;
                }

                var raw = property.GetValue(null, null);
                if (raw == null)
                {
                    return false;
                }

                allowsInput = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryIsCurrentProcessForeground(out bool foreground)
        {
            foreground = true;
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    return false;
                }

                int processId;
                GetWindowThreadProcessId(foregroundWindow, out processId);
                var process = Process.GetCurrentProcess();
                if (processId == process.Id)
                {
                    foreground = true;
                    return true;
                }

                var mainWindow = process.MainWindowHandle;
                if (mainWindow == IntPtr.Zero)
                {
                    foreground = true;
                    return true;
                }

                var owner = GetWindow(foregroundWindow, GwOwner);
                while (owner != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(owner, out processId);
                    if (processId == process.Id)
                    {
                        foreground = true;
                        return true;
                    }

                    owner = GetWindow(owner, GwOwner);
                }

                foreground = false;
                return true;
            }
            catch
            {
                foreground = true;
                return false;
            }
        }

        private const uint GwOwner = 4;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr windowHandle, uint command);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr windowHandle, out int processId);

        public static bool IsPlayerInventoryOpen
        {
            get { return Main.playerInventory; }
        }

        public static bool IsChatMode
        {
            // Terraria 1.4.5.6 has no Main.chatMode member; drawingPlayerChat is the typed chat signal.
            get { return false; }
        }

        public static bool IsDrawingPlayerChat
        {
            get { return Main.drawingPlayerChat; }
        }

        public static string NpcChatText
        {
            get { return Main.npcChatText ?? string.Empty; }
        }

        public static int MyPlayerIndex
        {
            get { return Main.myPlayer; }
        }

        public static int MaxTilesX
        {
            get { return Main.maxTilesX; }
        }

        public static int MaxTilesY
        {
            get { return Main.maxTilesY; }
        }

        public static int MouseX
        {
            get { return Main.mouseX; }
        }

        public static int MouseY
        {
            get { return Main.mouseY; }
        }

        public static int ScreenWidth
        {
            get { return Main.screenWidth; }
        }

        public static int ScreenHeight
        {
            get { return Main.screenHeight; }
        }

        public static Vector2 ScreenPosition
        {
            get { return Main.screenPosition; }
        }

        public static uint GameUpdateCount
        {
            get
            {
                if (_gameUpdateCountOverrideForTesting.HasValue)
                {
                    var value = _gameUpdateCountOverrideForTesting.Value;
                    return value > uint.MaxValue ? uint.MaxValue : (uint)value;
                }

                return Main.GameUpdateCount;
            }
        }

        public static bool TryReadGameUpdateCount(out ulong gameUpdateCount)
        {
            if (_gameUpdateCountOverrideForTesting.HasValue)
            {
                gameUpdateCount = _gameUpdateCountOverrideForTesting.Value;
                return true;
            }

            try
            {
                // Console tests can load Terraria refs without a fully initialized
                // Main; UI-only callers should fail closed instead of crashing.
                gameUpdateCount = Main.GameUpdateCount;
                return true;
            }
            catch
            {
                gameUpdateCount = 0;
                return false;
            }
        }

        public static Player LocalPlayer
        {
            get { return Main.LocalPlayer; }
        }

        public static Player[] Players
        {
            get { return Main.player; }
        }

        public static NPC[] Npcs
        {
            get { return Main.npc; }
        }

        public static Projectile[] Projectiles
        {
            get { return Main.projectile; }
        }

        public static WorldItem[] WorldItems
        {
            get { return Main.item; }
        }

        public static Chest[] Chests
        {
            get { return Main.chest; }
        }

        public static Tile[,] Tiles
        {
            get { return Main.tile; }
        }

        public static bool IsWorldReady
        {
            get
            {
                var player = Main.LocalPlayer;
                return !Main.gameMenu && player != null && player.active;
            }
        }

        public static bool TryGetLocalPlayer(out Player player)
        {
            player = Main.LocalPlayer;
            return player != null && player.active;
        }

        public static bool TryGetPlayer(int index, out Player player)
        {
            player = null;
            var players = Main.player;
            if (players == null || index < 0 || index >= players.Length)
            {
                return false;
            }

            player = players[index];
            return player != null;
        }

        public static bool TryGetNpc(int index, out NPC npc)
        {
            npc = null;
            var npcs = Main.npc;
            if (npcs == null || index < 0 || index >= npcs.Length)
            {
                return false;
            }

            npc = npcs[index];
            return npc != null;
        }

        public static bool TryGetProjectile(int index, out Projectile projectile)
        {
            projectile = null;
            var projectiles = Main.projectile;
            if (projectiles == null || index < 0 || index >= projectiles.Length)
            {
                return false;
            }

            projectile = projectiles[index];
            return projectile != null;
        }

        public static bool TryGetWorldItem(int index, out WorldItem item)
        {
            item = null;
            var items = Main.item;
            if (items == null || index < 0 || index >= items.Length)
            {
                return false;
            }

            item = items[index];
            return item != null;
        }

        public static bool TryGetChest(int index, out Chest chest)
        {
            chest = null;
            var chests = Main.chest;
            if (chests == null || index < 0 || index >= chests.Length)
            {
                return false;
            }

            chest = chests[index];
            return chest != null;
        }

        public static bool TryFindChestIndex(int tileX, int tileY, out int index)
        {
            index = -1;
            if (tileX < 0 || tileY < 0)
            {
                return false;
            }

            try
            {
                // Chest.FindChest is Terraria's coordinate index for world chests; callers
                // still read contents through TryGetChest so this remains a read-only lookup.
                index = Chest.FindChest(tileX, tileY);
                return index >= 0;
            }
            catch
            {
                index = -1;
                return false;
            }
        }

        public static bool IsTileCoordinateInWorld(int tileX, int tileY)
        {
            return tileX >= 0 &&
                   tileY >= 0 &&
                   tileX < Main.maxTilesX &&
                   tileY < Main.maxTilesY;
        }

        public static bool TryGetTile(int tileX, int tileY, out Tile tile)
        {
            tile = null;
            var tiles = Main.tile;
            if (tiles == null || !IsTileCoordinateInWorld(tileX, tileY))
            {
                return false;
            }

            tile = tiles[tileX, tileY];
            return tile != null;
        }
    }
}
