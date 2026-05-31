using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using TerrariaPlayer = Terraria.Player;

namespace JueMingZ.GameState.Ui
{
    public static class UiStateReader
    {
        public static UiStateSnapshot Read(TerrariaPlayer player, bool isInMainMenu)
        {
            var snapshot = new UiStateSnapshot { IsInMainMenu = isInMainMenu };

            try
            {
                snapshot.GameInputAvailable = TerrariaMainCompat.AllowsInputProcessing;
                snapshot.PlayerInventoryOpen = TerrariaMainCompat.IsPlayerInventoryOpen;
                snapshot.ChatOpen = TerrariaMainCompat.IsChatMode ||
                                    TerrariaMainCompat.IsDrawingPlayerChat;
                snapshot.NpcChatOpen = !string.IsNullOrEmpty(TerrariaMainCompat.NpcChatText);

                if (player != null)
                {
                    var chest = TerrariaPlayerReadCompat.ChestIndex(player);
                    snapshot.ChestOpen = chest >= 0;
                }

                snapshot.HasBlockingUi = snapshot.IsInMainMenu ||
                                         snapshot.ChatOpen ||
                                         snapshot.ChestOpen ||
                                         snapshot.NpcChatOpen;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "ui-state-read-failed",
                    TimeSpan.FromSeconds(30),
                    "UiStateReader",
                    "UI state read failed: " + error.Message);
            }

            return snapshot;
        }
    }
}
