using System;
using JueMingZ.Automation.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Compat
{
    internal sealed class TerrariaBlueprintCreationPromptCompat : IBlueprintCreationPromptSink
    {
        private const float HeadPromptOffsetY = -18f;
        private const float PromptVelocityY = -1.2f;
        private const string LocalPromptContract = "PopupText.AdvancedPopupRequest local-only; no chat, network packet, or player-state mutation.";

        public static readonly TerrariaBlueprintCreationPromptCompat Instance = new TerrariaBlueprintCreationPromptCompat();

        private TerrariaBlueprintCreationPromptCompat()
        {
        }

        public bool TryShowBlueprintCreationPrompt(BlueprintCreationPromptRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            request = request ?? new BlueprintCreationPromptRequest();
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                failureReason = "empty prompt text";
                return false;
            }

            try
            {
                Player player;
                if (!TerrariaMainCompat.TryGetLocalPlayer(out player) ||
                    !TerrariaPlayerReadCompat.IsAliveLocalPlayer(player))
                {
                    failureReason = "local player unavailable";
                    return false;
                }

                var hitbox = TerrariaPlayerReadCompat.Hitbox(player);
                if (hitbox.Width <= 0 || hitbox.Height <= 0)
                {
                    failureReason = "local player hitbox unavailable";
                    return false;
                }

                var position = new Vector2(
                    hitbox.X + hitbox.Width * 0.5f,
                    hitbox.Y + HeadPromptOffsetY);
                var popup = new AdvancedPopupRequest
                {
                    Text = request.Text,
                    DurationInFrames = Math.Max(1, request.DurationFrames),
                    Velocity = new Vector2(0f, PromptVelocityY),
                    Color = new Color(
                        ClampColor(request.ColorR),
                        ClampColor(request.ColorG),
                        ClampColor(request.ColorB))
                };
                var popupIndex = PopupText.NewText(popup, position);
                if (popupIndex < 0)
                {
                    failureReason = "popup text unavailable";
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                failureReason = error.Message;
                return false;
            }
        }

        internal static string GetLocalPromptContractForTesting()
        {
            return LocalPromptContract;
        }

        internal static float GetHeadPromptOffsetYForTesting()
        {
            return HeadPromptOffsetY;
        }

        internal static float GetPromptVelocityYForTesting()
        {
            return PromptVelocityY;
        }

        private static int ClampColor(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 255)
            {
                return 255;
            }

            return value;
        }
    }
}
