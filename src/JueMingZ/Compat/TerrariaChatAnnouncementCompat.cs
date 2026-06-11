using System;
using JueMingZ.Automation.Information;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.UI.Chat;

namespace JueMingZ.Compat
{
    internal sealed class TerrariaChatAnnouncementCompat :
        IMapQuickAnnouncementChatSink,
        IMapQuickAnnouncementCooldownPromptSink
    {
        private static readonly Color CooldownPromptColor = new Color(255, 217, 102);

        public static readonly TerrariaChatAnnouncementCompat Instance = new TerrariaChatAnnouncementCompat();

        private TerrariaChatAnnouncementCompat()
        {
        }

        public bool TrySendChat(string text, out string failureReason)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                failureReason = "empty chat text";
                return false;
            }

            try
            {
                var message = ChatManager.Commands.CreateOutgoingMessage(text);
                switch (TerrariaMainCompat.NetMode)
                {
                    case 0:
                        ChatManager.Commands.ProcessIncomingMessage(message, TerrariaMainCompat.MyPlayerIndex);
                        return true;
                    case 1:
                        ChatHelper.SendChatMessageFromClient(message);
                        return true;
                    default:
                        failureReason = "unsupported netMode: " + TerrariaMainCompat.NetMode;
                        return false;
                }
            }
            catch (Exception error)
            {
                failureReason = error.Message;
                return false;
            }
        }

        public bool TryShowCooldownPrompt(string text, out string failureReason)
        {
            failureReason = string.Empty;
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

                CombatText.NewText(hitbox, CooldownPromptColor, text ?? string.Empty);
                return true;
            }
            catch (Exception error)
            {
                failureReason = error.Message;
                return false;
            }
        }
    }
}
