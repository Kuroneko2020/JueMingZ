using System;
using System.Collections.Generic;
using JueMingZ.GameState.Buffs;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Npcs;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Tiles;
using JueMingZ.GameState.Ui;
using JueMingZ.GameState.World;

namespace JueMingZ.GameState
{
    public sealed class GameStateSnapshot
    {
        // Snapshot DTOs are observations from GameStateReader; services may
        // read them but must not treat them as writable vanilla state.
        public DateTime CapturedUtc { get; set; }
        public bool TerrariaDetected { get; set; }
        public bool IsInMainMenu { get; set; }
        public bool IsInWorld { get; set; }
        public int NetMode { get; set; }
        public string NetModeDescription { get; set; }
        public PlayerStateSnapshot Player { get; set; }
        public InventorySnapshot Inventory { get; set; }
        public IReadOnlyList<BuffSnapshot> ActiveBuffs { get; set; }
        public NpcSummarySnapshot Npcs { get; set; }
        public TileSnapshot Tiles { get; set; }
        public WorldStateSnapshot World { get; set; }
        public UiStateSnapshot Ui { get; set; }
        public DateTime? LastReadUtc { get; set; }
        public string LastReadError { get; set; }

        public GameStateSnapshot()
        {
            CapturedUtc = DateTime.UtcNow;
            NetModeDescription = "Unknown";
            Player = new PlayerStateSnapshot();
            Inventory = new InventorySnapshot();
            ActiveBuffs = new List<BuffSnapshot>();
            Npcs = new NpcSummarySnapshot();
            Tiles = new TileSnapshot();
            World = new WorldStateSnapshot();
            Ui = new UiStateSnapshot();
            LastReadUtc = CapturedUtc;
            LastReadError = string.Empty;
        }

        public static GameStateSnapshot Unknown(string error)
        {
            return new GameStateSnapshot
            {
                CapturedUtc = DateTime.UtcNow,
                NetModeDescription = "Unknown",
                LastReadUtc = DateTime.UtcNow,
                LastReadError = error ?? string.Empty
            };
        }
    }
}
