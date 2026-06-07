using System;
using JueMingZ.Compat;
using JueMingZ.GameState;
using Terraria;

namespace JueMingZ.Automation.Combat
{
    // Player context reads fail closed; missing local-player fields must not be replaced with synthetic aim centers.
    public static class CombatAimPlayerContext
    {
        public static bool TryReadLocalPlayerCenter(out object player, out float x, out float y)
        {
            player = null;
            x = 0f;
            y = 0f;
            return TerrariaInputCompat.TryGetLocalPlayer(out player) && TryReadPlayerCenter(player, out x, out y);
        }

        public static bool TryReadPlayerCenter(object player, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (player == null)
            {
                return false;
            }

            var typedPlayer = player as Player;
            if (typedPlayer != null)
            {
                var center = typedPlayer.Center;
                x = center.X;
                y = center.Y;
                return true;
            }

            if (GameStateReflection.TryReadVector2(GameStateReflection.GetMember(player, "Center"), out x, out y))
            {
                return true;
            }

            float positionX;
            float positionY;
            if (!GameStateReflection.TryReadVector2(GameStateReflection.GetMember(player, "position"), out positionX, out positionY))
            {
                return false;
            }

            int width;
            int height;
            GameStateReflection.TryGetInt(player, "width", out width);
            GameStateReflection.TryGetInt(player, "height", out height);
            x = positionX + Math.Max(1, width) / 2f;
            y = positionY + Math.Max(1, height) / 2f;
            return true;
        }
    }
}
