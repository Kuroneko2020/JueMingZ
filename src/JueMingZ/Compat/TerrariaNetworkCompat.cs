using System;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class TerrariaNetworkCompat
    {
        public static bool TryRequestChestSectionData(int sectionX, int sectionY, out string failureReason)
        {
            failureReason = string.Empty;
            if (Main.netMode != 1)
            {
                failureReason = "notMultiplayerClient";
                return false;
            }

            if (sectionX < 0 ||
                sectionY < 0 ||
                sectionX >= Main.maxSectionsX ||
                sectionY >= Main.maxSectionsY)
            {
                failureReason = "invalidSection";
                return false;
            }

            try
            {
                // Message 159 is Terraria's vanilla client request for one world section;
                // the server answers through SendSection, which includes chest contents.
                if (!NetMessage.TrySendData(159, number: sectionX, number2: sectionY))
                {
                    failureReason = "sendFailed";
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                failureReason = string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message;
                return false;
            }
        }
    }
}
