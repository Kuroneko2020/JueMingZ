using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using TerrariaPlayer = Terraria.Player;

namespace JueMingZ.GameState.Player
{
    public static class PlayerStateReader
    {
        public static PlayerStateSnapshot Read(TerrariaPlayer player)
        {
            var snapshot = new PlayerStateSnapshot();
            if (player == null)
            {
                return snapshot;
            }

            try
            {
                snapshot.Exists = true;
                snapshot.Active = TerrariaPlayerReadCompat.IsActive(player);
                snapshot.Dead = TerrariaPlayerReadCompat.IsDead(player);
                snapshot.Ghost = TerrariaPlayerReadCompat.IsGhost(player);
                snapshot.Life = TerrariaPlayerReadCompat.CurrentLife(player);
                snapshot.LifeMax = TerrariaPlayerReadCompat.MaxLife(player);
                snapshot.Mana = TerrariaPlayerReadCompat.CurrentMana(player);
                snapshot.ManaMax = TerrariaPlayerReadCompat.MaxMana(player);
                snapshot.SelectedItem = TerrariaPlayerReadCompat.SelectedItemSlot(player);
                snapshot.Direction = TerrariaPlayerReadCompat.Direction(player);
                snapshot.Wet = TerrariaPlayerReadCompat.IsWet(player);
                snapshot.LavaWet = TerrariaPlayerReadCompat.IsLavaWet(player);
                snapshot.HoneyWet = TerrariaPlayerReadCompat.IsHoneyWet(player);
                snapshot.IsUsingItem = TerrariaPlayerReadCompat.ItemAnimation(player) > 0;

                var position = TerrariaPlayerReadCompat.Position(player);
                var velocity = TerrariaPlayerReadCompat.Velocity(player);
                snapshot.PositionX = position.X;
                snapshot.PositionY = position.Y;
                snapshot.VelocityX = velocity.X;
                snapshot.VelocityY = velocity.Y;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "player-state-read-failed",
                    TimeSpan.FromSeconds(30),
                    "PlayerStateReader",
                    "Player state read failed: " + error.Message);
            }

            return snapshot;
        }
    }
}
