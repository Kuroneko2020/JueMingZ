using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState.Buffs;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Npcs;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Tiles;
using JueMingZ.GameState.Ui;
using JueMingZ.GameState.World;

namespace JueMingZ.GameState
{
    public static class GameStateReader
    {
        public static GameStateSnapshot LastSnapshot { get; private set; } = GameStateSnapshot.Unknown("Not read yet.");
        public static InventoryReadProfile LastInventoryProfile { get; private set; } = InventoryReadProfile.None;
        public static NpcReadProfile LastNpcProfile { get; private set; } = NpcReadProfile.None;
        public static TileReadProfile LastTileProfile { get; private set; } = TileReadProfile.None;

        public static GameStateReadResult Read(bool lateBootstrapCompleted)
        {
            return Read(lateBootstrapCompleted, GameStateReadOptions.Full);
        }

        public static GameStateReadResult Read(bool lateBootstrapCompleted, GameStateReadOptions options)
        {
            options = options ?? GameStateReadOptions.Full;
            LastInventoryProfile = options.InventoryProfile;
            LastNpcProfile = options.NpcProfile;
            LastTileProfile = options.TileProfile;

            if (!lateBootstrapCompleted)
            {
                LastSnapshot = GameStateSnapshot.Unknown("Skipped before LateBootstrap.");
                return new GameStateReadResult
                {
                    Status = GameStateReadStatus.SkippedEarly,
                    Snapshot = LastSnapshot,
                    Message = LastSnapshot.LastReadError
                };
            }

            try
            {
                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    LastSnapshot = GameStateSnapshot.Unknown(TerrariaRuntimeTypes.LastError);
                    return GameStateReadResult.Unavailable(LastSnapshot.LastReadError);
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                if (mainType == null)
                {
                    LastSnapshot = GameStateSnapshot.Unknown("Terraria.Main unavailable.");
                    return GameStateReadResult.Unavailable(LastSnapshot.LastReadError);
                }

                var isInMainMenu = TerrariaMainCompat.IsInMainMenu;
                var player = TerrariaMainCompat.LocalPlayer;
                var playerSnapshot = PlayerStateReader.Read(player);
                var inventorySnapshot = options.InventoryProfile != InventoryReadProfile.None
                    ? InventoryReader.Read(player, options.InventoryProfile)
                    : new InventorySnapshot();
                var buffs = options.IncludeActiveBuffs
                    ? BuffReader.Read(player)
                    : new BuffSnapshot[0];
                var isInWorld = !isInMainMenu && playerSnapshot.Exists && playerSnapshot.Active;

                var netMode = TerrariaMainCompat.NetMode;

                var snapshot = new GameStateSnapshot
                {
                    CapturedUtc = DateTime.UtcNow,
                    TerrariaDetected = true,
                    IsInMainMenu = isInMainMenu,
                    IsInWorld = isInWorld,
                    NetMode = netMode,
                    NetModeDescription = GetNetModeDescription(netMode),
                    Player = playerSnapshot,
                    Inventory = inventorySnapshot,
                    ActiveBuffs = buffs,
                    Npcs = options.NpcProfile != NpcReadProfile.None ? NpcReader.Read(options.NpcProfile) : new NpcSummarySnapshot(),
                    Tiles = options.TileProfile != TileReadProfile.None ? TileReader.Read(mainType, playerSnapshot) : new TileSnapshot(),
                    World = options.IncludeWorldSummary ? WorldStateReader.Read(mainType, isInWorld) : new WorldStateSnapshot(),
                    Ui = UiStateReader.Read(player, isInMainMenu),
                    LastReadUtc = DateTime.UtcNow,
                    LastReadError = string.Empty
                };

                LastSnapshot = snapshot;
                return GameStateReadResult.FromSnapshot(snapshot);
            }
            catch (Exception error)
            {
                LastSnapshot = GameStateSnapshot.Unknown(error.Message);
                LogThrottle.WarnThrottled(
                    "game-state-read-failed",
                    TimeSpan.FromSeconds(30),
                    "GameStateReader",
                    "GameState read failed: " + error.Message);
                return new GameStateReadResult
                {
                    Status = GameStateReadStatus.Failed,
                    Snapshot = LastSnapshot,
                    Message = error.Message
                };
            }
        }

        private static string GetNetModeDescription(int netMode)
        {
            if (TerrariaMainCompat.IsDedicatedServer || netMode == 2)
            {
                return "Server (netMode=" + netMode + ")";
            }

            if (netMode == 1)
            {
                return "MultiplayerClient (netMode=" + netMode + ")";
            }

            if (netMode == 0)
            {
                return "SinglePlayer (netMode=" + netMode + ")";
            }

            return "Unknown (netMode=" + netMode + ")";
        }
    }
}
