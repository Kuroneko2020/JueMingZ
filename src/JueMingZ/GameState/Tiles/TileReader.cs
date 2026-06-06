using System;
using JueMingZ.Diagnostics;
using JueMingZ.GameState.Player;

namespace JueMingZ.GameState.Tiles
{
    public static class TileReader
    {
        public static TileSnapshot Read(Type mainType, PlayerStateSnapshot player)
        {
            var snapshot = new TileSnapshot { SampleRadius = 1 };
            // Tile snapshots are read-only and intentionally tiny; unavailable
            // tiles make upper automation yield instead of guessing map state.
            if (mainType == null || player == null || !player.Exists)
            {
                return snapshot;
            }

            try
            {
                snapshot.CenterTileX = (int)(player.PositionX / 16f);
                snapshot.CenterTileY = (int)(player.PositionY / 16f);
                snapshot.SampledTileCount = 9;
                snapshot.Status = "Player3x3SummaryOnly";
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "tile-state-read-failed",
                    TimeSpan.FromSeconds(30),
                    "TileReader",
                    "Tile state read failed: " + error.Message);
            }

            return snapshot;
        }
    }
}
