using JueMingZ.Compat;
using JueMingZ.GameState.Ui;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAutomationDecisionDiagnostics
    {
        public static string BuildUiSkipReason(TerrariaUiInputContext ui)
        {
            if (ui == null)
            {
                return "uiBlocked:unknown";
            }

            if (ui.MainTypeUnavailable)
            {
                return "uiBlocked:mainTypeUnavailable";
            }

            if (ui.GameMenu)
            {
                return "uiBlocked:gameMenu";
            }

            if (ui.ChatOpen)
            {
                return "uiBlocked:chat";
            }

            if (ui.NpcChatOpen)
            {
                return "uiBlocked:npcChat";
            }

            if (ui.MouseCapturedByUi)
            {
                var reason = string.IsNullOrWhiteSpace(ui.MouseCaptureReason)
                    ? "unknown"
                    : ui.MouseCaptureReason;
                return "uiBlocked:mouseCapturedByUi:" + reason;
            }

            return "uiBlocked:unknown";
        }

        public static bool IsBlockingSnapshotUiForCombat(UiStateSnapshot ui)
        {
            if (ui == null)
            {
                return false;
            }

            return ui.IsInMainMenu ||
                   ui.ChatOpen ||
                   ui.NpcChatOpen;
        }

        public static string BuildSnapshotUiSkipReason(UiStateSnapshot ui)
        {
            if (ui == null)
            {
                return "uiBlocked:unknown";
            }

            if (ui.IsInMainMenu)
            {
                return "uiBlocked:gameMenu";
            }

            if (ui.ChatOpen)
            {
                return "uiBlocked:chat";
            }

            if (ui.NpcChatOpen)
            {
                return "uiBlocked:npcChat";
            }

            return "uiBlocked:unknown";
        }
    }
}
